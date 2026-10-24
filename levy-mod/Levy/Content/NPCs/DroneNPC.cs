using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Officers;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;
using D = Levy.Common.LevyConstants.Drones;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// An Engineering combat drone: a hovering, real NPC with a healthbar that mounts ONE ranged or magic weapon lent by a Barracks' Armory and fires it at nearby enemies. Modeled on
	/// <see cref="SummonedUnitNPC"/> (a friendly, non-town, aiStyle -1 body that enemies chase through the slot-255 swap because it is an <see cref="ITargetBody"/> fed into
	/// <see cref="EnemyTargeting.Bodies"/> by <see cref="DroneSystem"/>), but it flies, it belongs to a BARRACKS rather than an officer, and the weapon it carries is its whole cost.
	///
	/// THE WEAPON. <see cref="Weapon"/> (and the one round <see cref="Ammo"/> a gun or bow borrows) are the very <see cref="Item"/> instances the Armory lent: they sit on the Armory's ledger
	/// (<see cref="ArmoryTE.Lent"/>), never in the drone's keeping. Whatever happens to the drone (destroyed, dismissed, its Barracks gone, a soldier recalling the weapon, the world saved) the
	/// weapon is the Armory's, so none is ever lost or duplicated. <see cref="ReleaseLoan"/> files it back; the manager's reconcile pass catches a drone that vanished some other way.
	///
	/// THE SHOT. The LIGHT approach, not the soldier weapon pipeline: <see cref="DroneArsenal.BuildShot"/> works the item's own projectile, speed, damage, cadence and spread out once; every
	/// shot just spawns that projectile at the target, owned by the real player (an NPC-owned shot is an unsynced ghost) and tagged like a summoned unit's for the commander's credit.
	/// A drone fires for free: no mana, no ammo used up, no IC, no upkeep.
	///
	/// Server / single player drives the brain and fires; clients draw the borrowed sprite, the mounted weapon (its item type rides the NPC extra-AI sync, its aim in ai[0]) and the healthbar.
	/// Transient: never saved. The manager re-garrisons on reload, lending the weapons again.
	/// </summary>
	public class DroneNPC : ModNPC, ITargetBody
	{
		// ---- bound data (set server-side in Bind; clients learn WeaponType through the extra-AI sync) ----
		/// <summary>Tile-entity ids of the Barracks that fields this drone (its hover anchor) and the Armory that lent the weapon. -1 until bound.</summary>
		public int BarracksId = -1, ArmoryId = -1;
		/// <summary>Player index credited for this drone's kills (the player nearest its Barracks), or -1. Read by <see cref="Common.Globals.SoldierProjectileTag"/>.</summary>
		public int CommanderIndex = -1;
		/// <summary>The weapon and the borrowed round of ammo: the Armory's own instances, on its ledger (server / single player only; null on clients).</summary>
		public Item Weapon, Ammo;
		/// <summary>The item type of the mounted weapon: what a client draws, and what its max life and defense follow. 0 until bound.</summary>
		public int WeaponType;
		/// <summary>Formation slot over the Barracks (0..<see cref="D.PerBarracksCap"/>-1), assigned by the manager so the drones fan out instead of stacking.</summary>
		public int Slot;

		/// <summary>Free-standing Player swapped into Main.player[255] for one enemy AI call so the enemy chases this body (never in Main.player[] otherwise).</summary>
		public Player Dummy { get; private set; }

		public bool IsBound => WeaponType > 0;

		// ---- brain state (server) ----
		private DroneShot _shot;
		private int _age, _immuneTime, _scan, _target = -1, _cooldown, _validate, _farTicks, _sinceHit, _lastLife, _regenTimer;
		private bool _deathHandled, _loanReleased;
		private Vector2 _anchor;
		/// <summary>The drawn aim of the mounted weapon (radians), eased toward the wanted one; on a client it eases toward the synced ai[0].</summary>
		private float _aim;

		public override string Texture => "Levy/Content/NPCs/SoldierNPC"; // reuse the soldier placeholder; the real draw borrows a vanilla sprite

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.ActsLikeTownNPC[Type] = true;          // same as SoldierNPC: keeps it out of hostile spawn logic and consistent for targeting
			NPCID.Sets.NoTownNPCHappiness[Type] = true;
			NPCID.Sets.CantTakeLunchMoney[Type] = true;
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
		}

		public override void SetDefaults()
		{
			NPC.width = D.BodyWidth; NPC.height = D.BodyHeight;
			NPC.friendly = true; NPC.townNPC = false;
			NPC.aiStyle = -1;
			NPC.damage = 0; NPC.defense = D.DefenseBase;
			NPC.lifeMax = D.HpBase; NPC.life = NPC.lifeMax; // placeholder until Bind / the sync sets it from the weapon
			NPC.knockBackResist = D.KnockbackResist;
			NPC.npcSlots = 0f; // a garrison must never thin out the enemy spawn pressure
			NPC.HitSound = SoundID.NPCHit42; NPC.DeathSound = SoundID.NPCDeath44;
			NPC.lavaImmune = true; NPC.noGravity = true; NPC.noTileCollide = false; // hovers; solid ground and walls still stop it
			NPC.rarity = 0; NPC.value = 0f;
			NPC.netAlways = false; // a slow hover: the server refreshes it on a cadence (NetRefreshTicks) and clients carry it on its last velocity between
			EnsureDummy();
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }

		/// <summary>
		/// Server / single player: binds a freshly spawned drone to its Barracks, its Armory and the loan it carries, and sizes it by the weapon. Call once right after NPC.NewNPC, then sync.
		/// <paramref name="weapon"/> and <paramref name="ammo"/> must be on <paramref name="armoryId"/>'s ledger already.
		/// </summary>
		public void Bind(int barracksId, int armoryId, Item weapon, Item ammo, int slot, int commander, Vector2 anchor)
		{
			BarracksId = barracksId; ArmoryId = armoryId; Weapon = weapon; Ammo = ammo; Slot = slot; CommanderIndex = commander;
			WeaponType = weapon?.type ?? 0;
			_shot = DroneArsenal.BuildShot(weapon, ammo);
			_anchor = anchor;
			ApplyWeaponStats();
			NPC.life = NPC.lifeMax;
			_lastLife = NPC.life;
			_deathHandled = false; _loanReleased = false;
			_age = 0; _immuneTime = 0; _scan = 0; _target = -1; _cooldown = _shot.IntervalTicks / 2; _validate = 0; _farTicks = 0; _sinceHit = 0; _regenTimer = 0;
			_aim = 0f;
			NPC.netUpdate = true;
		}

		/// <summary>Max life and defense follow the weapon's rarity tier (the same on a client, which only knows the weapon type).</summary>
		private void ApplyWeaponStats()
		{
			if (WeaponType <= 0) return;
			NPC.lifeMax = DroneArsenal.MaxHpFor(WeaponType);
			NPC.defense = DroneArsenal.DefenseFor(WeaponType);
		}

		private void EnsureDummy()
		{
			if (Dummy != null) return;
			Dummy = new Player { whoAmI = SoldierNPC.DummySlot, active = true, dead = false, ghost = false, difficulty = 0, direction = 1 };
			Dummy.width = D.BodyWidth; Dummy.height = D.BodyHeight;
		}

		// =====================================================================================================
		// ITargetBody — enemies chase this body exactly like a soldier's (EnemyTargeting swaps Dummy into slot 255).
		// =====================================================================================================
		public int TargetAggro => D.Aggro;
		public bool IsAlive => NPC.active && NPC.life > 0 && !_deathHandled && IsBound;

		public void SyncDummyTransform()
		{
			EnsureDummy();
			Dummy.width = NPC.width; Dummy.height = NPC.height;
			Dummy.position = NPC.position; Dummy.velocity = NPC.velocity;
			Dummy.direction = NPC.direction == 0 ? 1 : NPC.direction;
			Dummy.gravDir = 1f; Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.statLife = NPC.life; Dummy.statLifeMax = Dummy.statLifeMax2 = Math.Max(1, NPC.lifeMax);
			Dummy.whoAmI = SoldierNPC.DummySlot;
		}

		// =====================================================================================================
		// AI: server / single drives the brain; clients only keep the dummy co-located and ease the drawn aim.
		// =====================================================================================================
		public override void AI()
		{
			EnsureDummy();
			SyncDummyTransform();
			if (!IsBound) return; // not yet bound / synced
			if (LevyUtil.IsClient) { ClientTick(); return; }

			_age++;
			if (_immuneTime > 0) _immuneTime--;
			if (_shot == null || !_shot.Valid || Weapon == null) { Dismantle(); return; } // a weapon that cannot be fired goes straight back to the racks

			if (++_validate >= D.ValidateIntervalTicks || _age == 1) { _validate = 0; if (!Validate()) return; }

			if (_immuneTime == 0) ContactDamage();                                  // melee enemies (vanilla deals no NPC-vs-NPC contact); hostile projectiles already strike friendly NPCs
			if (!NPC.active || NPC.life <= 0) return;

			Regen();
			Combat();
			bool engaging = _target >= 0 && _target < Main.maxNPCs && Shootable(Main.npc[_target]);
			float wantAim = _aim;
			if (engaging)
			{
				NPC t = Main.npc[_target];
				Vector2 to = t.Center - NPC.Center;
				if (to != Vector2.Zero) wantAim = to.ToRotation();
				if (Math.Abs(to.X) > 4f) { NPC.direction = to.X < 0f ? -1 : 1; NPC.spriteDirection = NPC.direction; }
				if (_cooldown <= 0) { Fire(t); _cooldown = _shot.IntervalTicks; }
			}
			else
			{
				if (Math.Abs(NPC.velocity.X) > 0.4f) { NPC.direction = NPC.velocity.X < 0f ? -1 : 1; NPC.spriteDirection = NPC.direction; }
				wantAim = NPC.direction < 0 ? MathHelper.Pi : 0f; // idle: the weapon rests level, pointing the way it faces
			}
			_aim = EaseAngle(_aim, wantAim, D.AimTurnLerp);
			NPC.ai[0] = _aim; // rides the NPC sync so clients draw the weapon where the server aims it

			Hover();
			if ((Main.GameUpdateCount + (uint)NPC.whoAmI) % (uint)D.NetRefreshTicks == 0) NPC.netUpdate = true;
			SyncDummyTransform();
		}

		/// <summary>Client side of a networked drone: derive its toughness from the weapon type and ease the drawn aim toward what the server sent.</summary>
		private void ClientTick()
		{
			int want = DroneArsenal.MaxHpFor(WeaponType);
			if (NPC.lifeMax != want) NPC.lifeMax = want;
			_aim = EaseAngle(_aim, NPC.ai[0], D.AimTurnLerp);
		}

		private static float EaseAngle(float from, float to, float t) => MathHelper.WrapAngle(from + MathHelper.WrapAngle(to - from) * t);

		// ---- upkeep: the Barracks, the Armory and the loan are still there; the commander is a live player ----
		/// <summary>
		/// Every <see cref="D.ValidateIntervalTicks"/>: false (and the drone gone) when its Barracks is destroyed, or its Armory is gone, or its weapon is no longer on the Armory's ledger
		/// (a soldier recalled it, or the Armory spilled it). Also refreshes the hover anchor and the commander. Constant-time: two dictionary lookups and a short ledger scan.
		/// </summary>
		private bool Validate()
		{
			if (!(TileEntity.ByID.TryGetValue(BarracksId, out TileEntity te) && te is BarracksTE barracks)) { Dismantle(); return false; }
			ArmoryTE armory = ArmoryOrNull();
			if (armory == null) { Orphan(); return false; }                  // the Armory was destroyed: its ledger spilled with it, nothing left to return
			if (!armory.IsLent(Weapon)) { Orphan(); return false; }          // the weapon was recalled for a soldier (or spilled): it is no longer ours to fire or return
			if (CommanderIndex < 0 || CommanderIndex >= Main.maxPlayers || !Main.player[CommanderIndex].active) CommanderIndex = DroneSystem.PickCommander(NPC.Center);
			_anchor = AnchorFor(barracks);
			return true;
		}

		private ArmoryTE ArmoryOrNull() => TileEntity.ByID.TryGetValue(ArmoryId, out TileEntity te) && te is ArmoryTE a ? a : null;

		/// <summary>Files the weapon and the borrowed round back into the Armory (idempotent: a second call, or one after a soldier recalled the weapon, changes nothing), then forgets them.</summary>
		public void ReleaseLoan()
		{
			if (_loanReleased) return;
			_loanReleased = true;
			ArmoryTE armory = ArmoryOrNull();
			if (armory != null)
			{
				if (Ammo != null) armory.ReturnLent(Ammo);
				if (Weapon != null) armory.ReturnLent(Weapon);
			}
			Weapon = null; Ammo = null;
		}

		/// <summary>The drone stands down and its loan goes home: used when its Barracks is gone, its Armory changed, or the manager dismisses it.</summary>
		public void Dismantle()
		{
			ReleaseLoan();
			Despawn();
		}

		/// <summary>The drone stands down but its weapon is not ours to return (the Armory spilled it, or a soldier took it back): only a borrowed round, if still on the ledger, goes home.</summary>
		private void Orphan()
		{
			if (!_loanReleased)
			{
				_loanReleased = true;
				ArmoryTE armory = ArmoryOrNull();
				if (armory != null && Ammo != null) armory.ReturnLent(Ammo);
				Weapon = null; Ammo = null;
			}
			Despawn();
		}

		private void Despawn()
		{
			_deathHandled = true;
			NPC.life = 0; NPC.active = false; NPC.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
		}

		/// <summary>A drone not hurt for a while mends a little: a lost drone costs nothing to replace, but a scratched one should not stay scratched for ever.</summary>
		private void Regen()
		{
			if (NPC.life < _lastLife) _sinceHit = 0; else _sinceHit++;
			_lastLife = NPC.life;
			if (NPC.life >= NPC.lifeMax || _sinceHit < D.RegenDelayTicks) { _regenTimer = 0; return; }
			if (++_regenTimer < D.RegenIntervalTicks) return;
			_regenTimer = 0;
			NPC.life = Math.Min(NPC.lifeMax, NPC.life + Math.Max(1, (int)(NPC.lifeMax * D.RegenFraction)));
			_lastLife = NPC.life;
			NPC.netUpdate = true;
		}

		/// <summary>Takes contact damage from an overlapping hostile (vanilla applies no NPC-vs-NPC contact, so the unit applies it itself, as a soldier does). One hit per immune window.</summary>
		private void ContactDamage()
		{
			Rectangle mine = NPC.Hitbox;
			float cx = NPC.Center.X, cy = NPC.Center.Y;
			float halfW = NPC.width * 0.5f + LevyConstants.ContactBroadPhasePx, halfH = NPC.height * 0.5f + LevyConstants.ContactBroadPhasePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.friendly || a.damage <= 0 || i == NPC.whoAmI) continue; // friendly skips our own side (soldiers, avatars, units, other drones)
				if (Math.Abs(a.Center.X - cx) > a.width * 0.5f + halfW || Math.Abs(a.Center.Y - cy) > a.height * 0.5f + halfH) continue;
				if (!NPCLoader.CanHitNPC(a, NPC)) continue;
				int slot = -1; float mult = 1f;
				Rectangle rect = a.Hitbox;
				NPC.GetMeleeCollisionData(mine, i, ref slot, ref mult, ref rect);
				if (!mine.Intersects(rect)) continue;

				int dir = a.Center.X < cx ? 1 : -1;
				int raw = Math.Max(1, Main.DamageVar(a.damage * mult));
				NPC.SimpleStrikeNPC(raw, dir, false, 0f, null, false, 0f, true); // ModifyIncomingHit applies the player-defense formula; the server broadcasts the strike
				_immuneTime = LevyConstants.ImmuneTicksOnHit;
				if (NPC.active && NPC.life > 0) NPC.netUpdate = true;
				return; // one contact hit per tick
			}
		}

		// ---- combat ----
		private void Combat()
		{
			if (_cooldown > 0) _cooldown--;
			if (++_scan >= D.ScanIntervalTicks) { _scan = 0; _target = FindTarget(); }
			else if (_target >= 0 && (_target >= Main.maxNPCs || !Shootable(Main.npc[_target]))) _target = -1;
		}

		/// <summary>The nearest hostile in the weapon's reach with a clear line. The range cut runs first so the never-target test and the line test are only paid for the few slots actually in reach.</summary>
		private int FindTarget()
		{
			float rangeSq = _shot.RangePx * _shot.RangePx;
			Vector2 from = NPC.Center;
			int best = -1; float bestSq = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float dSq = Vector2.DistanceSquared(from, t.Center);
				if (dSq >= bestSq || dSq > rangeSq) continue;
				if (!LevyUtil.IsHostileTarget(t) || SoldierTargeting.IsNeverTarget(t)) continue;
				if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height)) continue;
				bestSq = dSq; best = i;
			}
			return best;
		}

		private bool Shootable(NPC t)
		{
			if (!LevyUtil.IsHostileTarget(t) || SoldierTargeting.IsNeverTarget(t)) return false;
			if (Vector2.DistanceSquared(NPC.Center, t.Center) > _shot.RangePx * _shot.RangePx) return false;
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
		}

		/// <summary>One shot: the item's own projectile, aimed (led, and arced for an arrow) at the target, one to several per shot as the weapon fires, owned and tagged like a unit's shot.</summary>
		private void Fire(NPC target)
		{
			DroneShot shot = _shot;
			ArmoryTE armory = ArmoryOrNull();
			if (armory == null || !armory.IsLent(Weapon)) { _validate = D.ValidateIntervalTicks; return; } // the weapon was recalled this tick: the next upkeep stands the drone down

			Vector2 from = NPC.Center + new Vector2(0f, D.MuzzleDropPx);
			Vector2 vel = AimVelocity(shot, from, target);
			Vector2 dir = vel.SafeNormalize(Vector2.UnitX * (NPC.direction == 0 ? 1 : NPC.direction));
			Vector2 muzzle = from + dir * D.MuzzleOffsetPx;
			if (Collision.SolidCollision(muzzle - new Vector2(2f, 2f), 4, 4)) muzzle = from; // a drone hugging a wall: the shot would die inside the tile, so it leaves the body instead

			int dmg = (int)Math.Round(shot.BaseDamage * D.DamageMult * OfficerBrain.TownDamageScale()); // scales with world progress so the drone stays tier-appropriate
			if (Main.expertMode) dmg = (int)(dmg * Main.GameModeInfo.TownNPCDamageMultiplier);
			dmg = Math.Max(1, dmg);

			int owner = SoldierMinions.TrueMyPlayer; // Main.myPlayer (255 on a dedicated server): a shot owned by any other index is an unsynced ghost
			for (int k = 0; k < shot.Count; k++)
			{
				float deg = shot.Count > 1 ? -shot.SpreadDeg * 0.5f + shot.SpreadDeg * k / (shot.Count - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, v.X, v.Y, shot.Projectile, dmg, shot.Knockback, owner);
				if (idx < 0 || idx >= Main.maxProjectiles) continue;
				TagShot(Main.projectile[idx], shot, owner);
			}
			if (Main.netMode != NetmodeID.Server && Weapon != null && Weapon.UseSound.HasValue) SoundEngine.PlaySound(Weapon.UseSound.Value, NPC.Center);
		}

		/// <summary>The drone's shot is a friendly, NPC-style projectile owned by the real player, with the world-progress crit of its weapon and its own hit immunity when it pierces.</summary>
		private static void TagShot(Projectile p, DroneShot shot, int owner)
		{
			if (!p.active || p.type != shot.Projectile) return;
			p.owner = owner;
			p.npcProj = true; p.noDropItem = true; // CommanderIndex credit and DroneShot are tagged in SoldierProjectileTag.OnSpawn
			p.friendly = true; p.hostile = false;
			if (shot.Class != null) p.DamageType = shot.Class;
			p.CritChance = shot.Crit;
			// Every shot is owned by the same real player, so a piercing one would share a hit-immunity slot with every other shooter's: give it its own.
			if (p.penetrate != 1 && !p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity) { p.usesLocalNPCImmunity = true; if (p.localNPCHitCooldown <= 0) p.localNPCHitCooldown = 10; }
			p.netUpdate = true;
		}

		/// <summary>Lead, and the arc of a falling arrow: the same intercept solve a soldier's shot uses, with the drone's own small wobble.</summary>
		private static Vector2 AimVelocity(DroneShot shot, Vector2 from, NPC target)
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
			float jitter = D.AimJitterDeg * Aiming.LongRangeErrorScale(Vector2.Distance(from, at));
			return Aiming.ApplyError(vel, jitter, 0f, Vector2.Zero, from, at);
		}

		// ---- movement: hover over the Barracks in a formation slot, bob, never wander; a far drone is set back ----
		/// <summary>Where a Barracks' drones hover: above its top, as high as <see cref="D.HoverHeightTiles"/> allows under any ceiling.</summary>
		public static Vector2 AnchorFor(BarracksTE barracks)
		{
			float x = barracks.Center.X;
			int tx = (int)(x / LevyConstants.TilePx), topTile = barracks.Position.Y, free = 0;
			for (int k = 1; k <= D.HoverHeightTiles; k++)
			{
				int ty = topTile - k;
				if (!WorldGen.InWorld(tx, ty, 4) || WorldGen.SolidTile(tx, ty)) break;
				free = k;
			}
			int up = Math.Clamp(free, D.HoverMinTiles, D.HoverHeightTiles);
			return new Vector2(x, topTile * LevyConstants.TilePx - up * LevyConstants.TilePx);
		}

		/// <summary>The offset of formation slot <paramref name="slot"/> from the anchor: rows of <see cref="D.FormationColumns"/> centred over the Barracks, further rows higher up.</summary>
		public static Vector2 FormationOffset(int slot)
		{
			int cols = Math.Max(1, D.FormationColumns);
			int col = slot % cols, row = slot / cols;
			return new Vector2((col - (cols - 1) * 0.5f) * D.FormationSpacingPx, -row * D.FormationRowSpacingPx);
		}

		/// <summary>Where a fresh drone appears: just above the roof of its Barracks, from where it climbs to its slot.</summary>
		public static Vector2 LaunchPoint(BarracksTE barracks) =>
			new Vector2(barracks.Center.X, barracks.Position.Y * LevyConstants.TilePx - D.BodyHeight * 0.5f - 2f);

		private void Hover()
		{
			Vector2 goal = _anchor + FormationOffset(Slot);
			goal.Y += (float)Math.Sin(_age * D.BobSpeed + NPC.whoAmI * 0.9f) * D.BobAmplitudePx;
			Vector2 to = goal - NPC.Center;
			float dist = to.Length();

			if (dist > D.LeashTiles * LevyConstants.TilePx)
			{
				if (++_farTicks >= D.TeleportAfterTicks) { _farTicks = 0; SetBack(goal); return; }
			}
			else _farTicks = 0;

			Vector2 wanted = Vector2.Zero;
			if (dist > 1f) wanted = to / dist * Math.Min(D.MaxSpeedPx, dist * D.ArriveGain);
			NPC.velocity = Vector2.Lerp(NPC.velocity, wanted, D.SteerLerp);
			if (NPC.velocity.HasNaNs()) NPC.velocity = Vector2.Zero;
			NPC.rotation = MathHelper.Clamp(NPC.velocity.X * D.TiltPerSpeed, -D.MaxTilt, D.MaxTilt);
		}

		/// <summary>Puts a drone that could not get home (a wall trap, a pit) back on its hover spot.</summary>
		private void SetBack(Vector2 goal)
		{
			NPC.Center = goal;
			NPC.oldPosition = NPC.position;
			NPC.velocity = Vector2.Zero;
			NPC.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
		}

		// =====================================================================================================
		// Vanilla NPC hooks
		// =====================================================================================================
		public override bool CheckDead()
		{
			_deathHandled = true;
			if (LevyUtil.IsServerOrSingle) ReleaseLoan(); // destroyed: the weapon returns to the Armory
			return true;
		}

		public override bool PreKill() => false;              // no vanilla loot
		public override bool CheckActive() => false;          // the garrison owns the lifecycle, not distance
		public override bool NeedSaving() => false;           // transient: the manager re-garrisons on load
		public override bool UsesPartyHat() => false;
		public override bool CanChat() => false;
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool? CanBeHitByItem(Player player, Item item) => false;
		public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.friendly && !projectile.hostile ? false : null;
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false; // we draw our own bar

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			modifiers.DefenseEffectiveness *= LevyUtil.ModeDefenseEffectiveness() / 0.5f;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (Main.netMode == NetmodeID.Server) return;
			int sparks = NPC.life > 0 ? 3 : 14;
			for (int i = 0; i < sparks; i++)
			{
				int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, hit.HitDirection * 1.8f, -1.5f);
				Main.dust[d].scale = 0.7f;
			}
			if (NPC.life <= 0)
			{
				Gore.NewGore(NPC.GetSource_Death(), NPC.Center - new Vector2(10f, 10f), NPC.velocity, 605, NPC.scale);
				Gore.NewGore(NPC.GetSource_Death(), NPC.Center - new Vector2(10f, 10f), NPC.velocity, 606, NPC.scale);
			}
		}

		// =====================================================================================================
		// Draw: borrow a vanilla NPC sprite (no new art), the mounted weapon under it, and a healthbar.
		// =====================================================================================================
		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!IsBound || Main.dedServ) return false; // nothing to show until the sync says what it carries
			global::Levy.Common.Util.HangWatch.DrawEnter(3); // watchdog breadcrumb: a frame stall here = a combat drone's sprite draw
			try
			{
				int borrowed = D.DrawNpcType;
				Main.instance.LoadNPC(borrowed);
				Texture2D tex = TextureAssets.Npc[borrowed]?.Value;
				if (tex == null) return false;
				int frames = Math.Max(1, Main.npcFrameCount[borrowed]);
				int frameH = tex.Height / frames;
				int frameIdx = frames > 1 ? (int)((Main.GameUpdateCount + (uint)NPC.whoAmI) / 7u) % frames : 0; // the hover thrusters flicker
				Rectangle frame = new Rectangle(0, frameIdx * frameH, tex.Width, frameH);
				Vector2 origin = new Vector2(frame.Width / 2f, frame.Height / 2f);
				Vector2 pos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);
				int sdir = NPC.direction == 0 ? 1 : NPC.direction;
				SpriteEffects fx = sdir == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				Color c = Lighting.GetColor((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f), drawColor);
				spriteBatch.Draw(tex, pos, frame, c, NPC.rotation, origin, 1f, fx, 0f);
				DrawWeapon(spriteBatch, pos, c);
				return false;
			}
			catch (Exception e)
			{
				Levy.Instance.Logger.Warn("Levy: drawing a combat drone failed: " + e.Message);
				return false;
			}
			finally { global::Levy.Common.Util.HangWatch.DrawExit(); }
		}

		/// <summary>The mounted weapon, hung under the body and turned to its aim (kept upright when aiming left).</summary>
		private void DrawWeapon(SpriteBatch spriteBatch, Vector2 bodyPos, Color light)
		{
			if (WeaponType <= 0 || WeaponType >= ItemLoader.ItemCount) return;
			Main.instance.LoadItem(WeaponType);
			Texture2D wt = TextureAssets.Item[WeaponType]?.Value;
			if (wt == null || wt.Width <= 0 || wt.Height <= 0) return;
			float scale = Math.Min(1f, D.WeaponDrawMaxPx / Math.Max(wt.Width, wt.Height));
			SpriteEffects fx = Math.Cos(_aim) < 0.0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
			spriteBatch.Draw(wt, bodyPos + new Vector2(0f, D.WeaponDrawOffsetY), null, light, _aim, new Vector2(wt.Width, wt.Height) * 0.5f, scale, fx, 0f);
		}

		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ || !IsBound) return;
			int w = D.HpBarWidthPx, h = D.HpBarHeightPx;
			Vector2 top = NPC.Top - screenPos;
			float frac = MathHelper.Clamp(NPC.life / (float)Math.Max(1, NPC.lifeMax), 0f, 1f);
			var pixel = TextureAssets.MagicPixel.Value;
			int bx = (int)(top.X - w * 0.5f), by = (int)(top.Y - D.HpBarOffsetY);
			spriteBatch.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), new Color(20, 20, 20, 200));
			spriteBatch.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), Color.Lerp(Color.Red, Color.LimeGreen, frac));
		}

		// =====================================================================================================
		// Net: clients need the weapon type (sprite, bar size). Life rides the vanilla NPC sync; the aim rides ai[0].
		// =====================================================================================================
		public override void SendExtraAI(BinaryWriter writer) => writer.Write7BitEncodedInt(WeaponType);

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			int type = reader.Read7BitEncodedInt();
			if (type != WeaponType) { WeaponType = type; ApplyWeaponStats(); }
			EnsureDummy();
		}
	}
}
