using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
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
	/// OFFICER MODE. An officer is a conscripted town NPC, but it fights as a soldier: the real town NPC is parked (invisible, frozen,
	/// non-colliding, non-targetable) and co-located with a transient AVATAR — this same <see cref="SoldierNPC"/> flipped into officer mode.
	/// The avatar reuses the soldier movement AI unchanged (platforms, ledges, jumps, follow-the-commander), but draws the town NPC's sprite,
	/// carries a SYNTHETIC record that never enters the roster (no manpower, no command points, not in the barracks list), and runs the officer
	/// kit instead of the soldier brain/combat. The avatar is the body enemies chase; the parked town NPC is left for the player to shop with.
	/// Avatar death ends the commission ("fell"); dismissal / commander-offline removes the avatar and un-parks the town NPC.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- officer state (OfficerMode avatars only; synced so clients draw the town sprite and route clicks) ----
		public bool OfficerMode;
		/// <summary>whoAmI of the parked town NPC this avatar represents (co-located with it, routes shop/chat to it).</summary>
		public int OfficerTownIndex = -1;
		/// <summary>Town NPC type: the texture the avatar draws, and the key the <see cref="OfficerDef"/> is looked up by on every machine.</summary>
		public int OfficerTownType = -1;

		private OfficerDef _offDef;
		private object _offKitState;
		private OfficerContext _offCtx;
		private BarracksTE _offBarracks; private int _offBarracksTimer;
		// combat
		private int _offScanTimer, _offCooldown, _offTarget = -1; private bool _offRetreating;
		private bool _offDismissedCalled;
		// march chatter throttle (server)
		private int _offMarchTimer;
		// walk-frame animation of the drawn town sprite (client)
		private double _offFrameCounter; private int _offFrameY; private bool _offFrameInit;

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
			NPC.netAlways = true; NPC.netUpdate = true;
			def?.Kit?.OnMobilized(OfficerMakeContext());
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
		// Per-tick (server): posture → move goal, then the caller runs Movement_Update, then combat + kit + park.
		// =====================================================================================================
		/// <summary>Posture and retreat: writes HasMoveGoal/MoveGoal/State for the shared soldier movement to walk to. Server.</summary>
		private void Officer_Think()
		{
			Player cmd = Record != null ? Record.CommanderIndex >= 0 && Record.CommanderIndex < Main.maxPlayers && Main.player[Record.CommanderIndex].active ? Main.player[Record.CommanderIndex] : null : null;
			if (cmd == null) { HasMoveGoal = false; return; }

			float life = NPC.life / (float)Math.Max(1, NPC.lifeMax);
			if (_offRetreating) { if (life >= ResumeLifeFrac) _offRetreating = false; }
			else if (life < RetreatLifeFrac) _offRetreating = true;

			State = SoldierState.Follow;
			OfficerPosture posture = _offDef?.Posture ?? OfficerPosture.Frontline;
			Vector2 goal = posture == OfficerPosture.Support ? Officer_SupportGoal(cmd) : Officer_FrontlineGoal(cmd);
			MoveGoal = goal; HasMoveGoal = true;
		}

		/// <summary>FRONTLINE: march at the slot's follow distance behind the commander (the soldier default), fighting what comes in reach.</summary>
		private Vector2 Officer_FrontlineGoal(Player cmd)
		{
			int slot = Record != null ? Math.Max(0, OfficerSlot()) : 0;
			float stand = (FollowDistTiles + slot * FollowSlotStepTiles) * LevyConstants.TilePx;
			int side = NPC.Center.X <= cmd.Center.X ? -1 : 1;
			if (Math.Abs(NPC.Center.X - cmd.Center.X) < 32f) side = slot % 2 == 0 ? -1 : 1;
			return new Vector2(cmd.Center.X + side * stand, cmd.Center.Y);
		}

		/// <summary>
		/// SUPPORT (the Nurse): hang behind the commander near a wounded soldier the kit points at, and back off from any enemy that gets close.
		/// Never seeks melee, never advances to the front — her goal is behind the army, close to her work, out of enemy reach.
		/// </summary>
		private Vector2 Officer_SupportGoal(Player cmd)
		{
			float rear = SupportRearStandoffTiles * LevyConstants.TilePx;
			int side = NPC.Center.X <= cmd.Center.X ? -1 : 1;
			// anchor: the kit's current patient if any (stay in heal reach), else a standoff behind the commander
			Vector2 goal = new Vector2(cmd.Center.X + side * rear, cmd.Center.Y);
			if (_offDef?.Kit != null && _offDef.Kit.SupportAnchor(OfficerMakeContext(), out Vector2 anchor))
			{
				float tend = SupportTendRadiusTiles * LevyConstants.TilePx;
				// stay within tend radius of the patient, but not on top of the enemy side of them
				goal = anchor;
				if (Vector2.Distance(NPC.Center, anchor) < tend * 0.6f) goal = new Vector2(anchor.X + side * rear * 0.5f, anchor.Y);
			}
			// enemy-avoid: the nearest hostile pushes her directly away from it (toward the commander's rear)
			NPC threat = Officer_NearestHostile(SupportEnemyAvoidRadiusTiles * LevyConstants.TilePx);
			if (threat != null)
			{
				float away = Math.Sign(NPC.Center.X - threat.Center.X);
				if (away == 0f) away = side;
				goal = new Vector2(NPC.Center.X + away * SupportEnemyAvoidRadiusTiles * LevyConstants.TilePx, cmd.Center.Y);
			}
			return goal;
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
			if (w == null) return;
			if (_offCooldown > 0) _offCooldown--;
			if (_offRetreating) { _offTarget = -1; return; } // badly hurt: no new fights (support officers especially)
			if (++_offScanTimer >= ScanIntervalTicks) { _offScanTimer = 0; _offTarget = Officer_FindTarget(w); }
			else if (_offTarget >= 0 && !Officer_Shootable(Main.npc[_offTarget], w)) _offTarget = -1;
			if (_offCooldown > 0 || _offTarget < 0) return;

			NPC t = Main.npc[_offTarget];
			NPC.direction = t.Center.X < NPC.Center.X ? -1 : 1; NPC.spriteDirection = NPC.direction;
			Officer_Fire(t, w);
			_offCooldown = w.Cooldown + Main.rand.Next(Math.Max(1, w.RandomCooldown));
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

		private int Officer_FindTarget(OfficerWeapon w)
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float d = Vector2.Distance(NPC.Center, t.Center);
				if (d >= bestD || !Officer_Shootable(t, w)) continue;
				bestD = d; best = i;
			}
			return best;
		}

		private bool Officer_Shootable(NPC t, OfficerWeapon w)
		{
			if (!LevyUtil.IsHostileTarget(t)) return false;
			if (Vector2.Distance(NPC.Center, t.Center) > w.RangeTiles * LevyConstants.TilePx) return false;
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
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
		// Parking the real town NPC: co-locate it with the avatar and keep it inert, on every machine.
		// =====================================================================================================
		public void Officer_ParkTownNpc()
		{
			NPC town = OfficerTownNpc;
			if (town == null) return;
			town.Center = NPC.Center;
			town.velocity = Vector2.Zero;
			town.direction = NPC.direction == 0 ? 1 : NPC.direction;
			town.spriteDirection = town.direction;
			town.dontTakeDamage = true;
			town.dontTakeDamageFromHostiles = true;
			town.immortal = true;
			town.gfxOffY = 0f;
			town.oldPosition = town.position;
		}

		/// <summary>Client tick for an officer avatar: keep the parked town NPC co-located and advance the drawn sprite's walk frame.</summary>
		private void Officer_ClientTick()
		{
			Officer_ParkTownNpc();
			Officer_AdvanceWalkFrame();
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

		// =====================================================================================================
		// Draw: the town NPC's sprite, animated from the avatar's own movement (client).
		// =====================================================================================================
		private void Officer_AdvanceWalkFrame()
		{
			if (Main.dedServ) return;
			NPC town = OfficerTownNpc;
			if (town == null) return;
			int frameCount = Math.Max(1, Main.npcFrameCount[town.type]);
			if (frameCount <= 1) { _offFrameY = 0; return; }
			if (!_offFrameInit) { _offFrameInit = true; _offFrameCounter = 0; _offFrameY = 0; }
			bool airborne = NPC.velocity.Y != 0f;
			float speed = Math.Abs(NPC.velocity.X);
			// Town NPCs reserve frame 0 for standing and frames 1..last-1 for the attack pose; the WALK cycle is the trailing block of frames.
			// Walk when moving on the ground; otherwise stand. The cadence tracks ground speed so the stride reads right at walk and run.
			int walkStart = Math.Min(frameCount - 1, LevyConstants.Officer.TownWalkStartFrame);
			int walkFrames = frameCount - walkStart;
			if (!airborne && speed > 0.2f && walkFrames > 0)
			{
				_offFrameCounter += Math.Min(1.4f, 0.5f + speed * 0.35f);
				if (_offFrameCounter >= LevyConstants.Officer.TownWalkFrameTicks)
				{
					_offFrameCounter = 0;
					_offFrameY = walkStart + ((_offFrameY - walkStart + 1) % walkFrames);
					if (_offFrameY < walkStart) _offFrameY = walkStart;
				}
			}
			else { _offFrameCounter = 0; _offFrameY = 0; } // standing frame
		}

		/// <summary>Draws the town NPC's texture at the avatar's feet with the current walk/idle frame. Client; never throws into the NPC draw loop.</summary>
		private void Officer_Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, ref bool drawn)
		{
			if (Main.dedServ) return;
			NPC town = OfficerTownNpc;
			if (town == null) return; // leave drawn=false: the placeholder sprite shows until the link resolves
			try
			{
				Main.instance.LoadNPC(town.type);
				Texture2D tex = TextureAssets.Npc[town.type]?.Value;
				if (tex == null) return;
				int frameCount = Math.Max(1, Main.npcFrameCount[town.type]);
				int frameH = tex.Height / frameCount;
				int fy = Math.Clamp(_offFrameY, 0, frameCount - 1);
				Rectangle frame = new Rectangle(0, fy * frameH, tex.Width, frameH);
				Vector2 origin = new Vector2(frame.Width / 2f, frame.Height);
				Vector2 pos = new Vector2(NPC.Center.X, NPC.Bottom.Y + LevyConstants.Officer.TownDrawFootOffsetPx) - screenPos + new Vector2(0f, NPC.gfxOffY);
				int sdir = NPC.direction == 0 ? 1 : NPC.direction;
				SpriteEffects fx = sdir == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				Color c = Lighting.GetColor((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f), drawColor);
				spriteBatch.Draw(tex, pos, frame, c, 0f, origin, town.scale, fx, 0f);
				drawn = true;
			}
			catch (Exception e)
			{
				Levy.Instance.Logger.Warn("Levy: drawing an officer avatar's town sprite failed; it falls back to the placeholder: " + e.Message);
			}
		}
	}
}
