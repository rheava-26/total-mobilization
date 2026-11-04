using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Dialogue;
using Levy.Common.Movement;
using Levy.Common.Officers;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// OFFICER MODE. An officer is a conscripted town NPC, but it fights as a soldier: the real town NPC is parked (its town AI skipped, its
	/// physics off, non-targetable) and co-located with a transient AVATAR — this same <see cref="SoldierNPC"/> flipped into officer mode.
	/// The avatar reuses the soldier movement AI unchanged (platforms, ledges, jumps, follow-the-commander), carries a SYNTHETIC record that
	/// never enters the roster (no manpower, no command points, not in the barracks list), and runs the officer kit instead of the soldier
	/// brain/combat. The avatar is the body enemies chase and draws nothing but its nameplate: VANILLA draws the parked town NPC in its place
	/// (stock frames, party hat, texture packs), because the avatar feeds it its own velocity and facing every tick (<see cref="Officer_ParkTownNpc"/>),
	/// so the stock idle / walk / jump frames follow the body. The town NPC is also what the player shops with.
	/// Avatar death ends the commission ("fell"); dismissal / commander-offline removes the avatar and un-parks the town NPC.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- officer state (OfficerMode avatars only; synced so clients can find and drive the town NPC and route clicks) ----
		public bool OfficerMode;
		/// <summary>whoAmI of the parked town NPC this avatar represents (co-located with it, routes shop/chat to it).</summary>
		public int OfficerTownIndex = -1;
		/// <summary>Town NPC type: guards the link to <see cref="OfficerTownIndex"/>, and the key the <see cref="OfficerDef"/> is looked up by on every machine.</summary>
		public int OfficerTownType = -1;

		private OfficerDef _offDef;
		private object _offKitState;
		private OfficerContext _offCtx;
		private BarracksTE _offBarracks; private int _offBarracksTimer;
		// combat
		private int _offScanTimer, _offCooldown, _offTarget = -1; private bool _offRetreating;
		// the REAL weapon in his hand (null = none; he fights with the signature OfficerWeapon): the item, the round it takes and the shot worked out from them once
		private Item _offHeld, _offRound; private DroneShot _offShot;
		private bool _offDismissedCalled;
		// march chatter throttle (server)
		private int _offMarchTimer;
		// orders (server): a fresh order is read at the next tick; the patrol leg and pause; the army's middle (support officers keep to it), refreshed every few thinks
		private bool _offThinkNow;
		private int _offPatrolLeg, _offPatrolPauseUntil;
		private int _offHubTimer; private Vector2 _offHub;

		public OfficerDef OfficerDef => _offDef ??= (OfficerTownType > 0 && OfficerRegistry.TryGet(OfficerTownType, out var d) ? d : null);

		/// <summary>The parked town NPC, if it still exists as the enrolled officer this avatar belongs to; else null.</summary>
		public NPC OfficerTownNpc
		{
			get
			{
				if (OfficerTownIndex < 0 || OfficerTownIndex >= Main.maxNPCs) return null;
				NPC n = Main.npc[OfficerTownIndex];
				if (!n.active || !n.townNPC || n.type != OfficerTownType) return null;
				return n;
			}
		}

		// =====================================================================================================
		// Setup (server): turn a freshly spawned SoldierNPC into an officer avatar for a town NPC.
		// =====================================================================================================
		public void OfficerSetup(NPC townNpc, OfficerDef def, string commanderName)
		{
			OfficerMode = true;
			OfficerTownIndex = townNpc.whoAmI;
			OfficerTownType = townNpc.type;
			_offDef = def;
			_offKitState = def?.Kit?.CreateState();

			// A synthetic record carries only identity/looks for drawing and the nameplate. It is NEVER added to RosterSystem.Regulars,
			// so it costs no manpower or command points and never shows in the barracks roster. The avatar is not saved.
			var rec = new SoldierRecord
			{
				Id = -1,                           // not a roster id; never matched by FindById
				Kind = SoldierKind.Regular,        // Regular so dialogue barks are allowed (Militia are gagged for most triggers)
				Grade = Grade.Levy,
				Temperament = Temperament.Cynic,
				Name = OfficerName(townNpc),
				CommanderName = commanderName ?? "",
				HomeBarracksId = -1,
			};
			rec.CommanderIndex = LevyUtil.FindPlayerByName(rec.CommanderName);
			Record = rec;
			rec.BodyIndex = NPC.whoAmI;

			NPC.GivenName = rec.Name;
			NPC.friendly = true; NPC.townNPC = false; NPC.dontTakeDamage = false; NPC.immortal = false;
			NPC.knockBackResist = AvatarKnockbackResist;
			State = SoldierState.Follow; HoldPosition = false; TargetIndex = -1; HasMoveGoal = false;
			ThinkOffset = NPC.whoAmI % LevyConstants.ThinkIntervalTicks;
			_deathHandled = false;
			EnsureDummy();
			Abilities = new MovementAbilities();   // plain ground mover: walk/run/3-tile jumps, no flight gear
			Officer_RefreshStats();
			Officer_EquipDefaultWeapon(def);
			NPC.netAlways = true; NPC.netUpdate = true;
			def?.Kit?.OnMobilized(OfficerMakeContext());
		}

		// =====================================================================================================
		// The officer's REAL weapon. A town NPC that wields something (the Guide's wooden bow) fires THAT item; the signature OfficerWeapon is the fallback
		// for one that wields nothing usable. The held item is a plain field here (never in the roster or the armory), so a later "issue a weapon to an
		// officer" feature only has to call Officer_IssueWeapon.
		// =====================================================================================================
		/// <summary>True while the officer fires a real item instead of his signature weapon.</summary>
		public bool OfficerHasRealWeapon => _offShot != null;

		/// <summary>The real weapon item in his hand, or null when he fights with his signature weapon.</summary>
		public Item OfficerHeldItem => _offHeld;

		private void Officer_EquipDefaultWeapon(OfficerDef def)
		{
			OfficerHeldWeapon hw = def?.HeldWeapon;
			if (hw == null || hw.Item <= 0) { Officer_IssueWeapon(null, null); return; }
			var weapon = new Item(); weapon.SetDefaults(hw.Item);
			Item round = null;
			if (hw.Ammo > 0) { round = new Item(); round.SetDefaults(hw.Ammo); }
			Officer_IssueWeapon(weapon, round);
		}

		/// <summary>
		/// Puts <paramref name="weapon"/> (and the <paramref name="round"/> it fires, or null for the default round of its kind) in the officer's hand. If a ranged weapon cannot fire it
		/// (<see cref="DroneArsenal.IsMountable"/>) or yields no shot, he keeps his signature weapon. Pass null to clear. Server.
		/// </summary>
		public void Officer_IssueWeapon(Item weapon, Item round)
		{
			_offHeld = null; _offRound = null; _offShot = null;
			if (weapon == null || weapon.IsAir || !DroneArsenal.IsMountable(weapon)) return;
			DroneShot shot = DroneArsenal.BuildShot(weapon, round);
			if (!shot.Valid) return;
			_offHeld = weapon; _offRound = round; _offShot = shot;
		}

		/// <summary>The name shown over the officer and used for the record: the town NPC's given name ("Claire"), else its type name.</summary>
		private static string OfficerName(NPC townNpc) => string.IsNullOrEmpty(townNpc.GivenName) ? townNpc.TypeName : townNpc.GivenName;

		// =====================================================================================================
		// Stats: sturdier than a baseline town NPC, plus the Advanced Combat Techniques books (server).
		// =====================================================================================================
		public void Officer_RefreshStats()
		{
			StatsDirty = false;
			NPC town = OfficerTownNpc;
			int baseHp = town != null ? Math.Max(1, town.lifeMax) : LevyConstants.BaseHp;
			int maxHp = baseHp + AvatarBonusHp;
			if (NPC.lifeMax != maxHp)
			{
				NPC.life = NPC.life <= 0 ? maxHp : (int)Math.Clamp(NPC.life * (float)maxHp / Math.Max(1, NPC.lifeMax), 1, maxHp);
				NPC.lifeMax = maxHp;
			}
			int def = AvatarBaseDefense;
			if (Terraria.NPC.combatBookWasUsed) def += CombatBookDefensePerBook;          // Advanced Combat Techniques
			if (Terraria.NPC.combatBookVolumeTwoWasUsed) def += CombatBookDefensePerBook;  // ...Volume Two
			Endurance = 0f; MoveSpeedMult = 1f; Aggro = _offDef?.Aggro ?? 0;
			// Aura buffs (e.g. the Angler's Well Fed) lift an officer too: the avatar never runs Player.UpdateBuffs, so fold the buff deltas in by
			// hand through the dummy, exactly as a soldier does. ResetEffects zeroes the stat accumulators first, so nothing drifts across ticks.
			if (Dummy != null && OfficerHasBuff())
			{
				try
				{
					Dummy.ResetEffects();
					for (int i = 0; i < NPC.maxBuffs; i++)
						if (NPC.buffType[i] > 0 && NPC.buffTime[i] > 0) ApplyBuffEffect(Dummy, NPC.buffType[i]);
					if (ExtraBuffStats != null) { try { ExtraBuffStats(this, Dummy); } catch (Exception) { } } // a companion mod's own stat deltas (Thorium bard empowerments): defense, damage reduction, move speed
					def += Dummy.statDefense;
					Endurance = Math.Clamp(Dummy.endurance, 0f, LevyConstants.MaxEndurance);
					MoveSpeedMult = Math.Max(0.5f, Dummy.moveSpeed); // ResetEffects set the base to 1
				}
				catch (Exception e) { Levy.Instance.Logger.Warn("Levy: officer buff stats failed: " + e.Message); }
			}
			Defense = def; NPC.defense = def;
			if (Record != null) Record.CurrentHp = NPC.life;
		}

		/// <summary>True when the avatar carries any active buff (an aura's, usually): the cheap guard before the buff-fold does the ResetEffects work.</summary>
		private bool OfficerHasBuff()
		{
			for (int i = 0; i < NPC.maxBuffs; i++) if (NPC.buffType[i] > 0 && NPC.buffTime[i] > 0) return true;
			return false;
		}

		/// <summary>Vanilla town-NPC damage scale (boss progression) plus the two combat books, so an officer's shots hit like a book-trained villager's.</summary>
		public static float OfficerDamageScale()
		{
			float m = OfficerBrain.TownDamageScale();
			if (Terraria.NPC.combatBookWasUsed) m += CombatBookDamagePerBook;
			if (Terraria.NPC.combatBookVolumeTwoWasUsed) m += CombatBookDamagePerBook;
			return m;
		}

		// =====================================================================================================
		// Per-tick (server): the standing ORDER + posture → move goal, then the caller runs Movement_Update, then combat + kit + park.
		// An officer takes the same orders a soldier does (Follow / Hold / Patrol / Return to Barracks / Search & Destroy / Free Rein) and reads
		// them the same way; posture decides how he carries them out. FRONTLINE closes on a fight inside the order's leash and holds the line
		// at his weapon's reach. SUPPORT keeps to the rear of the army and never seeks a fight. He follows the commander only on a Follow order.
		// Every goal here is a FEET position, like the soldier brain's.
		// =====================================================================================================
		/// <summary>Order, posture and retreat: writes HasMoveGoal/MoveGoal/State for the shared soldier movement to walk to. Server.</summary>
		private void Officer_Think()
		{
			Player cmd = Record != null && Record.CommanderIndex >= 0 && Record.CommanderIndex < Main.maxPlayers && Main.player[Record.CommanderIndex].active ? Main.player[Record.CommanderIndex] : null;
			if (cmd == null) { HasMoveGoal = false; return; }

			float life = NPC.life / (float)Math.Max(1, NPC.lifeMax);
			if (_offRetreating) { if (life >= ResumeLifeFrac) _offRetreating = false; }
			else if (life < RetreatLifeFrac) _offRetreating = true;

			int now = (int)Main.GameUpdateCount;
			if (!_offThinkNow && now % LevyConstants.ThinkIntervalTicks != ThinkOffset) return; // decide on the soldiers' cadence; the movement walks the last goal in between
			_offThinkNow = false;

			SoldierOrder order = Record.Order;
			bool support = (_offDef?.Posture ?? OfficerPosture.Frontline) == OfficerPosture.Support;
			Vector2 goal; SoldierState st; bool go;
			if (_offRetreating && order != SoldierOrder.ReturnToBarracks)
			{
				// badly hurt: fall back to the commander's rear whatever the order says (he does not fight again until he has recovered)
				goal = Officer_FrontlineGoal(cmd); st = SoldierState.Follow; go = true;
			}
			else switch (order)
			{
				case SoldierOrder.Hold:
					go = Officer_ThinkPost(Record.HoldPos, LevyConstants.HoldChaseLimit, false, support, SoldierState.SearchDestroy, out goal, out st);
					break;
				case SoldierOrder.Patrol:
					go = Officer_ThinkPatrol(support, now, out goal, out st);
					break;
				case SoldierOrder.ReturnToBarracks:
					go = Officer_ThinkReturn(cmd, support, now, out goal, out st);
					break;
				case SoldierOrder.SearchDestroy:
				case SoldierOrder.FreeRein:
					// a roaming order: a fighter hunts inside the leash around where the order found him; support has nothing to hunt and keeps to the army
					go = support ? Officer_ThinkFollow(cmd, true, now, out goal, out st)
						: Officer_ThinkPost(Record.HoldPos, order == SoldierOrder.FreeRein ? LevyConstants.FreeReinLeashTiles : LevyConstants.SearchLeashTiles, true, false, SoldierState.SearchDestroy, out goal, out st);
					break;
				default:
					go = Officer_ThinkFollow(cmd, support, now, out goal, out st);
					break;
			}
			State = st;
			if (go) SetGoal(goal); else ClearGoal();
		}

		/// <summary>An order arrived from the commander (server). The avatar reads it on its next think. Hold, Search &amp; Destroy and Free Rein anchor where he stood.</summary>
		public void Officer_ReceiveOrder(SoldierOrder order)
		{
			if (Record == null) return;
			Record.Order = order;
			Record.HoldPos = NPC.Bottom;
			_offPatrolLeg = 0; _offPatrolPauseUntil = 0;
			Record.PatrolA = Record.PatrolB = Point16.NegativeOne;
			if (order == SoldierOrder.Patrol)
			{
				// the same banner pair a soldier is sent to: the two nearest his paying barracks (else where he stands)
				BarracksTE home = OfficerPayingBarracks();
				if (Common.UI.UIActions.FindPatrolPair(home != null ? home.Center : NPC.Center, LevyConstants.UI.PatrolBannerSearchTiles, out Point16 a, out Point16 b)) { Record.PatrolA = a; Record.PatrolB = b; }
			}
			_offThinkNow = true;
			NPC.netUpdate = true;
		}

		/// <summary>FOLLOW: the commander's rear, as a soldier follows. A fighter breaks off to close on a fight within the soldiers' follow leash; support keeps to the rear of the army. Out of sight and far behind, he catches up.</summary>
		private bool Officer_ThinkFollow(Player cmd, bool support, int now, out Vector2 goal, out SoldierState st)
		{
			st = SoldierState.Follow;
			if (LevyUtil.TileDistance(NPC.Center, cmd.Center) > LevyConstants.TeleportDistance && now - _lastTeleportTick >= LevyConstants.TeleportCooldownTicks && !VisibleToAnyPlayer())
			{
				CatchUpTeleport(cmd);
				_lastTeleportTick = now;
				goal = NPC.Bottom;
				return false;
			}
			if (support) { goal = Officer_SupportGoal(cmd); return true; }
			float reach = Officer_ReachTiles();
			NPC t = reach > 0f && !_offRetreating ? Officer_SeekTarget(cmd.Center, LevyConstants.FollowChaseLimit + reach) : null;
			if (t != null) return Officer_EngageGoal(t, reach, cmd.Bottom, LevyConstants.FollowChaseLimit, out goal, out st);
			goal = Officer_FrontlineGoal(cmd);
			return true;
		}

		/// <summary>
		/// HOLD a post (Hold, Search &amp; Destroy, Free Rein, and a stand-down once home): a fighter closes on a fight within <paramref name="leashTiles"/> of the post
		/// (or, when <paramref name="seekFromSelf"/>, within sight of wherever he is) and never wanders past that leash; with nothing to fight he goes back to the post.
		/// Support just stands at the post, stepping back from anything that comes close.
		/// </summary>
		private bool Officer_ThinkPost(Vector2 post, float leashTiles, bool seekFromSelf, bool support, SoldierState travel, out Vector2 goal, out SoldierState st)
		{
			st = travel; goal = post;
			if (support)
			{
				goal = Officer_AvoidThreat(post, post.Y, post, leashTiles);
			}
			else
			{
				float reach = Officer_ReachTiles();
				NPC t = reach > 0f && !_offRetreating
					? (seekFromSelf ? Officer_SeekTarget(NPC.Center, SearchSeekTiles + reach, post, leashTiles + reach) : Officer_SeekTarget(post, leashTiles + reach))
					: null;
				if (t != null) return Officer_EngageGoal(t, reach, post, leashTiles, out goal, out st);
			}
			if (LevyUtil.WithinTiles(NPC.Bottom, goal, SettleTiles)) st = SoldierState.Idle;
			return true;
		}

		/// <summary>PATROL: walk the banner pair as a soldier does, pausing at each end. A fighter closes on a fight within the soldiers' post leash of the nearest banner; support holds at it. With no banners within reach of his barracks he holds where the order found him.</summary>
		private bool Officer_ThinkPatrol(bool support, int now, out Vector2 goal, out SoldierState st)
		{
			Point16 a = Record.PatrolA, b = Record.PatrolB;
			bool hasA = a != Point16.NegativeOne, hasB = b != Point16.NegativeOne;
			if (!hasA && !hasB) return Officer_ThinkPost(Record.HoldPos, LevyConstants.HoldChaseLimit, false, support, SoldierState.SearchDestroy, out goal, out st);
			if (!hasA) a = b;
			if (!hasB) b = a;
			Vector2 wa = PatrolWorld(a), wb = PatrolWorld(b);
			Vector2 near = Vector2.DistanceSquared(NPC.Bottom, wa) <= Vector2.DistanceSquared(NPC.Bottom, wb) ? wa : wb;
			if (support) return Officer_ThinkPost(near, LevyConstants.PostChaseLimit, false, true, SoldierState.SearchDestroy, out goal, out st);

			float reach = Officer_ReachTiles();
			NPC t = reach > 0f && !_offRetreating ? Officer_SeekTarget(near, LevyConstants.PostChaseLimit + reach) : null;
			if (t != null) return Officer_EngageGoal(t, reach, near, LevyConstants.PostChaseLimit, out goal, out st);

			st = SoldierState.Patrol; goal = NPC.Bottom;
			if (now < _offPatrolPauseUntil) { st = SoldierState.Idle; return false; }
			Vector2 target = _offPatrolLeg == 0 ? wa : wb;
			bool arrived = Math.Abs(NPC.Center.X - target.X) < LevyConstants.PatrolArriveXTiles * LevyConstants.TilePx
				&& Math.Abs(NPC.Bottom.Y - target.Y) < LevyConstants.PatrolArriveYTiles * LevyConstants.TilePx;
			if (arrived)
			{
				_offPatrolPauseUntil = now + LevyConstants.PatrolPauseTicks;
				_offPatrolLeg ^= 1;
				st = SoldierState.Idle;
				return false;
			}
			goal = target;
			return true;
		}

		/// <summary>RETURN TO BARRACKS (stand-down): walk to the paying barracks' muster point and hold there. A barracks-less officer falls in behind the commander, as a homeless soldier does.</summary>
		private bool Officer_ThinkReturn(Player cmd, bool support, int now, out Vector2 goal, out SoldierState st)
		{
			BarracksTE home = OfficerPayingBarracks();
			if (home == null) return Officer_ThinkFollow(cmd, support, now, out goal, out st);
			Vector2 muster = home.MusterPoint;
			if (LevyUtil.WithinTiles(NPC.Bottom, muster, LevyConstants.NearHomeTiles)) return Officer_ThinkPost(muster, LevyConstants.HoldChaseLimit, false, support, SoldierState.ReturnHome, out goal, out st);
			goal = muster; st = SoldierState.ReturnHome;
			return true;
		}

		/// <summary>The reach (tiles) he shoots at: his signature weapon's, else the real weapon's. 0 for a pure support kit with no weapon.</summary>
		private float Officer_ReachTiles()
		{
			OfficerWeapon w = _offDef?.Weapon;
			if (w != null) return w.RangeTiles;
			return _offShot != null ? _offShot.RangePx / LevyConstants.TilePx : 0f;
		}

		/// <summary>The nearest hostile to the officer that stands within <paramref name="tiles"/> of <paramref name="around"/> (and, if given, within <paramref name="leashTiles"/> of <paramref name="leashAround"/>), or null.</summary>
		private NPC Officer_SeekTarget(Vector2 around, float tiles, Vector2? leashAround = null, float leashTiles = 0f)
		{
			NPC best = null; float bestD = float.MaxValue;
			float r = tiles * LevyConstants.TilePx; r *= r;
			float lr = leashTiles * LevyConstants.TilePx; lr *= lr;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active || !LevyUtil.IsHostileTarget(t)) continue;
				if (Vector2.DistanceSquared(t.Center, around) > r) continue;
				if (leashAround.HasValue && Vector2.DistanceSquared(t.Center, leashAround.Value) > lr) continue;
				float d = Vector2.DistanceSquared(NPC.Center, t.Center);
				if (d < bestD) { bestD = d; best = t; }
			}
			return best;
		}

		/// <summary>
		/// A fighter's approach to a fight: nothing to do once the target is in reach with a clear shot (hold the line and shoot, never walk at it), else walk to a standoff
		/// from it, never past the leash circle around <paramref name="post"/>. A flier is met on the ground at his own height.
		/// </summary>
		private bool Officer_EngageGoal(NPC t, float reachTiles, Vector2 post, float leashTiles, out Vector2 goal, out SoldierState st)
		{
			goal = NPC.Bottom; st = SoldierState.Idle;
			if (Officer_Shootable(t, reachTiles)) return false;
			int dir = Math.Sign(t.Center.X - NPC.Center.X); if (dir == 0) dir = NPC.direction == 0 ? 1 : NPC.direction;
			bool aloft = t.noGravity || t.Bottom.Y < NPC.Bottom.Y - AirborneTargetRiseTiles * LevyConstants.TilePx;
			goal = new Vector2(t.Center.X - dir * StandoffTiles(reachTiles) * LevyConstants.TilePx, aloft ? NPC.Bottom.Y : t.Bottom.Y);
			ClampToCircle(ref goal, post, leashTiles);
			st = SoldierState.SearchDestroy;
			return true;
		}

		/// <summary>FRONTLINE, following: stand at the slot's follow distance beside the commander (the soldier default), fighting what comes in reach.</summary>
		private Vector2 Officer_FrontlineGoal(Player cmd)
		{
			int slot = Record != null ? Math.Max(0, OfficerSlot()) : 0;
			float stand = (FollowDistTiles + slot * FollowSlotStepTiles) * LevyConstants.TilePx;
			int side = NPC.Center.X <= cmd.Center.X ? -1 : 1;
			if (Math.Abs(NPC.Center.X - cmd.Center.X) < 32f) side = slot % 2 == 0 ? -1 : 1;
			return new Vector2(cmd.Center.X + side * stand, cmd.Bottom.Y);
		}

		/// <summary>
		/// SUPPORT (the Nurse): hang BEHIND the commander's soldiers, near a wounded one the kit points at, and back off from any enemy that gets close.
		/// The rear is the side of the army away from the nearest enemy (the side she is already on when there is none). Never seeks melee, never advances to the
		/// front: her goal is behind the army, close to her work, out of enemy reach. Not glued to the commander: with soldiers about she keeps to them.
		/// </summary>
		private Vector2 Officer_SupportGoal(Player cmd)
		{
			float rear = SupportRearStandoffTiles * LevyConstants.TilePx;
			Vector2 hub = Officer_ArmyHub(cmd);
			int side = NPC.Center.X <= hub.X ? -1 : 1;
			NPC front = Officer_NearestHostileTo(hub, SupportFrontScanTiles * LevyConstants.TilePx);
			int back = front != null && front.Center.X != hub.X ? -Math.Sign(front.Center.X - hub.X) : side;
			Vector2 goal = new Vector2(hub.X + back * rear, hub.Y);
			// anchor: the kit's current patient if any (stay in heal reach), else the standoff behind the army above
			if (_offDef?.Kit != null && _offDef.Kit.SupportAnchor(OfficerMakeContext(), out Vector2 anchor))
			{
				float tend = SupportTendRadiusTiles * LevyConstants.TilePx;
				anchor.Y += NPC.height * 0.5f; // the kit hands back a body centre; goals are feet
				goal = anchor;
				if (Vector2.Distance(NPC.Center, anchor) < tend * 0.6f) goal = new Vector2(anchor.X + back * rear * 0.5f, anchor.Y);
			}
			return Officer_AvoidThreat(goal, hub.Y, hub, float.MaxValue);
		}

		/// <summary>The enemy-avoid rule: the nearest hostile pushes her directly away from it, but never past <paramref name="leashTiles"/> of <paramref name="leashCentre"/>.</summary>
		private Vector2 Officer_AvoidThreat(Vector2 goal, float groundY, Vector2 leashCentre, float leashTiles)
		{
			NPC threat = Officer_NearestHostile(SupportEnemyAvoidRadiusTiles * LevyConstants.TilePx);
			if (threat == null) return goal;
			float away = Math.Sign(NPC.Center.X - threat.Center.X);
			if (away == 0f) away = NPC.direction == 0 ? 1 : -NPC.direction;
			goal = new Vector2(NPC.Center.X + away * SupportEnemyAvoidRadiusTiles * LevyConstants.TilePx, groundY);
			if (leashTiles < float.MaxValue) ClampToCircle(ref goal, leashCentre, leashTiles);
			return goal;
		}

		/// <summary>The middle (feet level) of the commander's soldiers about him, else the commander's feet. Refreshed a few times a second.</summary>
		private Vector2 Officer_ArmyHub(Player cmd)
		{
			if (--_offHubTimer > 0) return _offHub;
			_offHubTimer = HubRefreshThinks;
			float reach = SupportArmyRangeTiles * LevyConstants.TilePx;
			Vector2 sum = Vector2.Zero; int n = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (!s.IsAlive || s.OfficerMode || s.State == SoldierState.Desert || s.State == SoldierState.Dead || s.Record.CommanderName != Record.CommanderName) continue;
				if (Vector2.Distance(cmd.Center, s.NPC.Center) > reach) continue;
				sum += s.NPC.Bottom; n++;
			}
			_offHub = n > 0 ? sum / n : cmd.Bottom;
			return _offHub;
		}

		private NPC Officer_NearestHostileTo(Vector2 around, float rangePx)
		{
			NPC best = null; float bestD = rangePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active || !LevyUtil.IsHostileTarget(t)) continue;
				float d = Vector2.Distance(around, t.Center);
				if (d < bestD) { bestD = d; best = t; }
			}
			return best;
		}

		private int OfficerSlot()
		{
			NPC town = OfficerTownNpc;
			var g = town != null ? OfficerSystem.Data(town) : null;
			return g?.Slot ?? 0;
		}

		/// <summary>Combat + kit + parking + march chatter, after the movement step (server).</summary>
		private void Officer_Act()
		{
			Officer_Combat(_offDef?.Weapon);
			if (_offDef?.Kit != null)
			{
				if (--_offBarracksTimer <= 0) { _offBarracksTimer = 60; _offBarracks = OfficerPayingBarracks(); }
				OfficerContext ctx = OfficerMakeContext();
				_offDef.Kit.Tick(ctx);
			}
			Officer_ParkTownNpc();
			Officer_MarchChatter();
			if (Record != null) Record.CurrentHp = NPC.life;
		}

		private OfficerContext OfficerMakeContext()
		{
			_offCtx ??= new OfficerContext();
			NPC town = OfficerTownNpc;
			Player cmd = Record != null && Record.CommanderIndex >= 0 && Record.CommanderIndex < Main.maxPlayers && Main.player[Record.CommanderIndex].active ? Main.player[Record.CommanderIndex] : null;
			_offCtx.Npc = NPC;            // the AVATAR is the officer's body for kit purposes (heal range, aim)
			_offCtx.Def = _offDef;
			_offCtx.Commander = cmd;
			_offCtx.State = _offKitState;
			_offCtx.Barracks = _offBarracks ??= OfficerPayingBarracks();
			_offCtx.Slot = OfficerSlot();
			return _offCtx;
		}

		private BarracksTE OfficerPayingBarracks()
		{
			NPC town = OfficerTownNpc;
			if (town != null && OfficerSystem.Data(town) is OfficerGlobalNPC g) return OfficerSystem.Instance.PayingBarracks(town, g);
			return SettlementSystem.Instance.NearestBarracks(NPC.Center);
		}

		// ---- combat: fire the officer's own weapon at the nearest hostile in reach (fire only; movement is the posture's job) ----
		private void Officer_Combat(OfficerWeapon w)
		{
			DroneShot real = _offShot; // the real item in his hand, if he has a usable one: it supplies the shot and the cadence
			if (w == null && real == null) return;
			// Reach: the officer's own engagement range when he has one (so a real bow does not turn a frontline fighter into a sniper), else the real weapon's.
			float reachTiles = w != null ? w.RangeTiles : real.RangePx / LevyConstants.TilePx;
			if (_offCooldown > 0) _offCooldown--;
			if (_offRetreating) { _offTarget = -1; return; } // badly hurt: no new fights (support officers especially)
			if (++_offScanTimer >= ScanIntervalTicks) { _offScanTimer = 0; _offTarget = Officer_FindTarget(reachTiles); }
			else if (_offTarget >= 0 && !Officer_Shootable(Main.npc[_offTarget], reachTiles)) _offTarget = -1;
			if (_offCooldown > 0 || _offTarget < 0) return;

			NPC t = Main.npc[_offTarget];
			NPC.direction = t.Center.X < NPC.Center.X ? -1 : 1; NPC.spriteDirection = NPC.direction;
			if (real != null)
			{
				Officer_FireReal(t, real, w);
				_offCooldown = real.IntervalTicks; // the weapon's own cadence
			}
			else
			{
				Officer_Fire(t, w);
				_offCooldown = w.Cooldown + Main.rand.Next(Math.Max(1, w.RandomCooldown));
			}
		}

		private NPC Officer_NearestHostile(float rangePx)
		{
			NPC best = null; float bestD = rangePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active || !LevyUtil.IsHostileTarget(t)) continue;
				float d = Vector2.Distance(NPC.Center, t.Center);
				if (d < bestD) { bestD = d; best = t; }
			}
			return best;
		}

		private int Officer_FindTarget(float reachTiles)
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float d = Vector2.Distance(NPC.Center, t.Center);
				if (d >= bestD || !Officer_Shootable(t, reachTiles)) continue;
				bestD = d; best = i;
			}
			return best;
		}

		private bool Officer_Shootable(NPC t, float reachTiles)
		{
			if (!LevyUtil.IsHostileTarget(t)) return false;
			if (Vector2.Distance(NPC.Center, t.Center) > reachTiles * LevyConstants.TilePx) return false;
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
		}

		/// <summary>
		/// One shot of the officer's REAL weapon: the held item's own projectile (resolved with its round, as the Drone Arsenal does), led and arced at the target, one to several per shot as the
		/// weapon fires. Damage is the item's own scaled with the world's progress like a town NPC's shot, never below the signature weapon's damage (<paramref name="floor"/>), so a starter
		/// bow does not leave an officer a toy in the late game. Nothing is spent: an officer's quiver is bottomless.
		/// </summary>
		private void Officer_FireReal(NPC target, DroneShot shot, OfficerWeapon floor)
		{
			Vector2 from = NPC.Center + new Vector2(0f, -2f);
			Vector2 vel = Officer_AimReal(shot, from, target);
			Vector2 dir = vel.SafeNormalize(Vector2.UnitX * (NPC.direction == 0 ? 1 : NPC.direction));
			Vector2 muzzle = from + dir * 16f;
			if (Collision.SolidCollision(muzzle - new Vector2(2f, 2f), 4, 4)) muzzle = from; // against a wall: the shot would die inside the tile, so it leaves the body

			int baseDmg = Math.Max(shot.BaseDamage, floor != null ? floor.Damage : 0);
			int dmg = (int)(baseDmg * OfficerDamageScale() * AttackDamageMult);
			if (Main.expertMode) dmg = (int)(dmg * Main.GameModeInfo.TownNPCDamageMultiplier);
			dmg = Math.Max(1, dmg);

			int owner = SoldierMinions.TrueMyPlayer; // a shot owned by any other index is an unsynced ghost
			for (int k = 0; k < shot.Count; k++)
			{
				float deg = shot.Count > 1 ? -shot.SpreadDeg * 0.5f + shot.SpreadDeg * k / (shot.Count - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, v.X, v.Y, shot.Projectile, dmg, shot.Knockback, owner);
				if (idx < 0 || idx >= Main.maxProjectiles) continue;
				Projectile p = Main.projectile[idx];
				if (!p.active || p.type != shot.Projectile) continue;
				p.owner = owner;
				p.npcProj = true; p.noDropItem = true; // commander credit is tagged in SoldierProjectileTag.OnSpawn
				p.friendly = true; p.hostile = false;
				if (shot.Class != null) p.DamageType = shot.Class;
				p.CritChance = shot.Crit;
				if (p.penetrate != 1 && !p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity) { p.usesLocalNPCImmunity = true; if (p.localNPCHitCooldown <= 0) p.localNPCHitCooldown = 10; }
				p.netUpdate = true;
			}
			if (Main.netMode != NetmodeID.Server && _offHeld != null) { var useSound = WeaponUse.SoundOf(_offHeld); if (useSound.HasValue) SoundEngine.PlaySound(useSound.Value, NPC.Center); }
		}

		/// <summary>Lead, and the arc of a falling arrow: the same intercept solve every soldier and drone shot uses, with a small officer's wobble.</summary>
		private static Vector2 Officer_AimReal(DroneShot shot, Vector2 from, NPC target)
		{
			Vector2 at = target.Center;
			Vector2 vel;
			if (shot.Homing)
			{
				Vector2 d = at - from;
				if (d == Vector2.Zero) d = Vector2.UnitX;
				vel = Vector2.Normalize(d) * shot.Speed;
			}
			else
			{
				float steps = shot.StepMult; // the shot advances this many times a tick, and gravity runs per advance
				float leadSpeed = Math.Max(0.01f, shot.Speed) * steps;
				vel = Aiming.SolveIntercept(from, leadSpeed, at, target.velocity, shot.Gravity,
					shot.Gravity ? LevyConstants.ArrowGravity * steps * steps : 0f, shot.Gravity ? (int)Math.Round(LevyConstants.ArrowGravityDelayTicks / steps) : 0) / steps;
			}
			float jitter = RealShotAimJitterDeg * Aiming.LongRangeErrorScale(Vector2.Distance(from, at));
			return Aiming.ApplyError(vel, jitter, 0f, Vector2.Zero, from, at);
		}

		private void Officer_Fire(NPC target, OfficerWeapon w)
		{
			int dir = NPC.spriteDirection == 0 ? 1 : NPC.spriteDirection;
			Vector2 aim = new Vector2(dir, -1f);
			if (target != null)
			{
				float frac = MathHelper.Clamp(Vector2.Distance(NPC.Center, target.Center) / (w.RangeTiles * LevyConstants.TilePx), 0f, 1f);
				Vector2 v = NPC.DirectionTo(target.Center + new Vector2(0f, -w.GravityCorrection * frac));
				if (!v.HasNaNs() && Math.Sign(v.X) == dir) aim = v;
			}
			aim *= w.Speed;
			int dmg = (int)(w.Damage * OfficerDamageScale() * AttackDamageMult);
			if (Main.expertMode) dmg = (int)(dmg * Main.GameModeInfo.TownNPCDamageMultiplier);
			int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center.X + dir * 16f, NPC.Center.Y - 2f, aim.X, aim.Y, w.Projectile, Math.Max(1, dmg), w.Knockback, SoldierMinions.TrueMyPlayer);
			if (idx >= 0 && idx < Main.maxProjectiles) { Main.projectile[idx].npcProj = true; Main.projectile[idx].noDropItem = true; }
		}

		// ---- march chatter (server): a throttled on-the-move line while the avatar is actually walking ----
		private void Officer_MarchChatter()
		{
			if (--_offMarchTimer > 0) return;
			bool marching = Math.Abs(NPC.velocity.X) > 0.4f && HasMoveGoal;
			if (!marching) { _offMarchTimer = 30; return; }
			_offMarchTimer = Main.rand.Next(LevyConstants.Officer.MarchChatterMinTicks, LevyConstants.Officer.MarchChatterMaxTicks);
			if (Main.rand.NextFloat() < LevyConstants.Officer.MarchChatterChance)
				DialogueSystem.Bark(this, _offDef?.MarchTrigger ?? DialogueTrigger.OFFICER_MARCH);
		}

		// =====================================================================================================
		// Parking the real town NPC: co-locate it with the avatar and drive it as a puppet, on every machine.
		// The town NPC is what the player SEES (vanilla draws it: stock frames, party hat, texture packs); the avatar is its invisible body.
		// =====================================================================================================
		/// <summary>
		/// Drives the real town NPC as a puppet of this avatar. Runs on every machine, twice a tick: from the avatar's own AI (so the town NPC's update reads fresh
		/// inputs) and again from <see cref="OfficerSystem.PostUpdateNPCs"/> once every NPC has moved (so the drawn sprite is never a step behind the body, whichever
		/// of the two has the lower slot). The town AI itself stays skipped by <see cref="OfficerGlobalNPC.PreAI"/>. This feeds vanilla's own FindFrame what it reads:
		/// no velocity = the standing frame, ground speed = the stock walk cycle, vertical speed = the airborne pose. There is no frame math here.
		/// </summary>
		public void Officer_ParkTownNpc()
		{
			if (!IsAlive) return; // a dying avatar must not re-puppet the town NPC its death just released
			NPC town = OfficerTownNpc;
			OfficerGlobalNPC g = town != null ? OfficerSystem.Data(town) : null;
			if (g == null || !g.Enrolled) return; // link not resolved yet, or the commission already ended: never puppet an NPC that is not this officer

			// Puppet physics. Its own gravity and tile collision would re-simulate the body and fight the pin (a platform or a ledge reads as a fall, and the airborne
			// frame would stick). Neither flag is synced, so every machine sets them here and OfficerGlobalNPC.PreAI hands them back once the avatar is gone.
			g.Parked = true;
			town.noGravity = true;
			town.noTileCollide = true;

			// Inert: shoppable, not hurtable (the avatar is the body enemies chase).
			town.dontTakeDamage = true;
			town.dontTakeDamageFromHostiles = true;
			town.immortal = true;

			// What vanilla's FindFrame reads. ai[0] = 0 is the plain idle/walk state, so no sleeping, sitting or chatting pose is carried over from before the commission.
			// A body that is only coasting to a stop reads as standing; the horizontal speed is capped at the quickest a stock town NPC ever moves, so the stride keeps a
			// stock cadence at the avatar's run speed (the body itself still moves at its real speed).
			float vx = Math.Abs(NPC.velocity.X) < LevyConstants.Movement.StopBelowPx ? 0f : Math.Clamp(NPC.velocity.X, -TownStrideMaxSpeedPx, TownStrideMaxSpeedPx);
			town.velocity = new Vector2(vx, NPC.velocity.Y);
			town.direction = NPC.direction == 0 ? 1 : NPC.direction;
			town.spriteDirection = town.direction;
			town.ai[0] = 0f;

			// Position last, feet on the avatar's feet: the puppet cannot drift, and vanilla's draw (which seats the sprite on Bottom) lands where the avatar stands.
			// The avatar's smoothing offsets ride along, so the sprite eases between network corrections exactly as the body does.
			town.Bottom = NPC.Bottom;
			town.oldPosition = town.position;
			town.netOffset = NPC.netOffset;
			town.gfxOffY = NPC.gfxOffY;
		}

		/// <summary>Client tick for an officer avatar: keep the parked town NPC driven (clients run no officer brain; vanilla animates the town NPC from what this feeds it).</summary>
		private void Officer_ClientTick()
		{
			Officer_ParkTownNpc();
		}

		// =====================================================================================================
		// Death / cleanup
		// =====================================================================================================
		/// <summary>The avatar was killed by enemies: the officer fell. End the commission and let the town NPC un-park where it died. Server.</summary>
		private void Officer_OnDeath()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			Officer_Dismissed();
			OfficerSystem.Instance.OnAvatarFell(OfficerTownNpc, this);
		}

		/// <summary>Runs the kit's dismissal hook exactly once, however the avatar leaves (death, dismissal, commander offline). Server.</summary>
		public void Officer_Dismissed()
		{
			if (_offDismissedCalled || !LevyUtil.IsServerOrSingle) return;
			_offDismissedCalled = true;
			_offDef?.Kit?.OnDismissed(OfficerMakeContext());
		}

		// Drawing: none. The avatar's body is invisible (SoldierNPC.Draw.cs); vanilla draws the parked town NPC, and only the nameplate is the avatar's own.
	}
}
