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
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Targeting;            // ITargetBody / SoldierTargeting
using Levy.Common.Util;                 // LevyUtil
using Levy.Common.Weapons;              // DroneArsenal / DroneShot
using Levy.Content.NPCs;                // SoldierNPC.DummySlot
using Levy.Content.TileEntities;        // ArmoryTE / BarracksTE
using Mechanization.Common;
using D = Mechanization.Common.MechConstants.Drone;

namespace Mechanization.Content.NPCs
{
	/// <summary>Where a drone is in its day: flying its loiter, running home to recharge, running home on orders, charging on its pad armed, or sitting on its pad unarmed.</summary>
	public enum DronePhase : byte { Loiter = 0, Refuel = 1, Recall = 2, Charging = 3, Parked = 4 }

	/// <summary>
	/// A combat drone: a flying, real NPC with a healthbar that mounts ONE ranged or magic weapon lent by its Barracks' Armory and fires it at nearby enemies.
	///
	/// THE WEAPON. <see cref="Weapon"/> (and the one round <see cref="Ammo"/> a gun or bow borrows) are the very <see cref="Item"/> instances the Armory lent: they sit on the Armory's ledger
	/// (<see cref="ArmoryTE.Lent"/>), never in the drone's keeping. Whatever happens to the drone (destroyed, recalled, its Barracks gone, a soldier taking the weapon back, the world saved) the
	/// weapon is the Armory's, so none is ever lost or duplicated. <see cref="ReleaseLoan"/> files it back; <see cref="DroneSystem"/>'s reconcile pass catches a drone that vanished some other way.
	///
	/// THE SHOT. The light approach: <see cref="DroneArsenal.BuildShot"/> works the item's own projectile, speed, damage, cadence and spread out once; every shot just spawns that projectile at the
	/// target (<see cref="ShotKit"/>). A drone fires for free: no mana, no ammo used up, no upkeep.
	///
	/// LOITER. The drone flies a lazy patrol inside a box above its Barracks: it picks a waypoint in its own sector of the box (and altitude row), drifts there, hangs a moment and picks the next, so a
	/// garrison covers the whole base. It presses toward an enemy that comes in reach but never strays past a leash around the box, and returns to loitering when the enemy is dead or out of reach.
	///
	/// BATTERY. Flying drains it slowly; sitting on a pad refills it fast. A low battery (<see cref="DroneBattery.ReturnThreshold"/>) sends the drone home to land and recharge, then it takes off again
	/// armed with the same weapon. A flat battery is a refuel trip and nothing worse: the drone is never weaker, slower, capped or destroyed for it.
	///
	/// RECALL. Ordered from the chat panel or the keybind (<see cref="DroneSystem"/>): the drone flies to its pad beside the Barracks, lands, and files its weapon back into the Armory
	/// (<see cref="DronePhase.Parked"/>), so the player can swap what the Armory holds. A parked drone stays on its pad until a launch order re-arms it with the best spare weapon.
	///
	/// Server / single player drives the brain and fires; clients draw the hand-built side view (phase in ai[1], battery in ai[2], bound flag in ai[3], weapon type and shot counter on the extra-AI sync),
	/// the mounted weapon (aim in ai[0]) and the bars. Transient: never saved. The manager re-garrisons on reload.
	/// </summary>
	public class DroneNPC : ModNPC, ITargetBody
	{
		// ---- bound data (server; clients learn what they draw from the sync) ----
		/// <summary>Tile-entity ids of the Barracks that fields this drone and the Armory that lent the weapon. -1 until bound.</summary>
		public int BarracksId = -1, ArmoryId = -1;
		/// <summary>Player index credited for this drone's kills (the player nearest its Barracks), or -1.</summary>
		public int CommanderIndex = -1;
		/// <summary>The weapon and the borrowed round of ammo: the Armory's own instances, on its ledger (server / single player only; null on clients and while unarmed).</summary>
		public Item Weapon, Ammo;
		/// <summary>The item type of the mounted weapon: what a client draws, and what its max life and defense follow. 0 while unarmed.</summary>
		public int WeaponType;
		/// <summary>Formation slot over the Barracks (0..<see cref="D.PerBarracksCap"/>-1): picks its pad, its loiter sector and its altitude row.</summary>
		public int Slot;
		/// <summary>True when the player ordered this drone parked: the garrison leaves it alone until a launch order. A drone that stood down on its own (its weapon was taken) is re-armed by the garrison.</summary>
		public bool Held;

		/// <summary>Free-standing Player swapped into Main.player[255] for one enemy AI call so the enemy chases this body (never in Main.player[] otherwise).</summary>
		public Player Dummy { get; private set; }

		/// <summary>Bound to a Barracks (ai[3], so a client knows too).</summary>
		public bool IsBound => NPC.ai[3] > 0.5f;
		public DronePhase Phase
		{
			get => (DronePhase)(int)NPC.ai[1];
			private set { if ((int)NPC.ai[1] != (int)value) { NPC.ai[1] = (float)(int)value; NPC.netUpdate = true; } }
		}
		/// <summary>On its pad with the rotors still (charging armed, or parked unarmed).</summary>
		public bool Landed => Phase == DronePhase.Charging || Phase == DronePhase.Parked;
		/// <summary>Carrying a weapon it can fire (server).</summary>
		public bool Armed => Weapon != null && _shot != null && _shot.Valid;
		/// <summary>Battery as a fraction of capacity, 0..1 (every netmode: it rides ai[2]).</summary>
		public float BatteryFraction => Math.Clamp(NPC.ai[2], 0f, 1f);
		/// <summary>The drone's battery, in seconds of flight (server).</summary>
		public float Battery => _battery;

		// ---- brain state (server) ----
		private DroneShot _shot;
		private int _age, _immuneTime, _scan, _target = -1, _cooldown, _validate, _sinceHit, _lastLife, _regenTimer, _padClock;
		private int _takeoff, _stuck, _farTicks, _dwell = -1;
		private float _battery;
		private float _baseX, _roofY;
		private Vector2 _pad;
		private Vector2 _wp;
		private bool _hasWp;
		/// <summary>The drawn aim of the mounted weapon (radians), eased toward the wanted one; on a client it eases toward the synced ai[0].</summary>
		private float _aim;
		/// <summary>Counts shots fired. Rides the extra-AI sync so a remote client hears each volley and kicks the weapon back.</summary>
		private int _shotSeq;
		private bool _seqSeen;
		private float _recoil;

		public override string Texture => "Terraria/Images/Item_" + D.BodyDrawItemType; // a valid vanilla asset; the real body is drawn from rectangles in PreDraw

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.ActsLikeTownNPC[Type] = true;          // keeps it out of hostile spawn logic and consistent for targeting
			NPCID.Sets.NoTownNPCHappiness[Type] = true;
			NPCID.Sets.CantTakeLunchMoney[Type] = true;
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
		}

		public override void SetDefaults()
		{
			NPC.width = D.BodyWidth; NPC.height = D.BodyHeight;
			NPC.friendly = true; NPC.townNPC = false;
			NPC.aiStyle = -1;
			NPC.damage = 0; NPC.defense = DroneArsenal.DefenseFor(0);
			NPC.lifeMax = DroneArsenal.MaxHpFor(0); NPC.life = NPC.lifeMax; // until a weapon is mounted
			NPC.knockBackResist = D.KnockbackResist;
			NPC.npcSlots = 0f; // a garrison must never thin out the enemy spawn pressure
			NPC.HitSound = SoundID.NPCHit42; NPC.DeathSound = SoundID.NPCDeath44;
			NPC.lavaImmune = true; NPC.noGravity = true; NPC.noTileCollide = false; // flies; solid ground and walls still stop it
			NPC.rarity = 0; NPC.value = 0f;
			NPC.netAlways = false; // a slow loiter: the server refreshes it on a cadence (NetRefreshTicks) and clients carry it on its last velocity between
			EnsureDummy();
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }

		// =====================================================================================================
		// Binding and arming (server / single player)
		// =====================================================================================================
		/// <summary>
		/// Binds a freshly spawned drone to its Barracks and its pad, then arms it with the Armory's loan and sends it up. Call once right after NPC.NewNPC, then sync.
		/// <paramref name="weapon"/> and <paramref name="ammo"/> must be on <paramref name="armoryId"/>'s ledger already. False (and nothing bound) when the weapon yields no usable shot.
		/// </summary>
		public bool Bind(BarracksTE barracks, int armoryId, Item weapon, Item ammo, int slot, int commander)
		{
			BarracksId = barracks.ID; Slot = slot; CommanderIndex = commander;
			RefreshBase(barracks);
			NPC.Center = _pad;
			NPC.velocity = Vector2.Zero;
			NPC.direction = slot % 2 == 0 ? 1 : -1; NPC.spriteDirection = NPC.direction;
			_battery = DroneBattery.Capacity;
			_age = 0; _immuneTime = 0; _scan = 0; _target = -1; _validate = 0; _sinceHit = 0; _regenTimer = 0; _padClock = 0;
			_stuck = 0; _farTicks = 0; _dwell = -1; _hasWp = false; _takeoff = 0; _aim = 0f;
			NPC.ai[1] = (float)(int)DronePhase.Parked; NPC.ai[3] = 1f;
			if (!Arm(armoryId, weapon, ammo)) { NPC.ai[3] = 0f; return false; }
			NPC.life = NPC.lifeMax; _lastLife = NPC.life;
			NPC.netUpdate = true;
			return true;
		}

		/// <summary>
		/// Mounts a loan and sends the drone up (it charges first when its battery is low). The weapon and ammo must already be on <paramref name="armoryId"/>'s ledger. False when the weapon
		/// has no usable shot. A docked drone is repaired as it is armed.
		/// </summary>
		public bool Arm(int armoryId, Item weapon, Item ammo)
		{
			DroneShot shot = DroneArsenal.BuildShot(weapon, ammo);
			if (shot == null || !shot.Valid) return false;
			ArmoryId = armoryId; Weapon = weapon; Ammo = ammo; _shot = shot; WeaponType = weapon.type;
			Held = false;
			ApplyWeaponStats();
			if (Landed) { NPC.life = NPC.lifeMax; _lastLife = NPC.life; }
			_cooldown = shot.IntervalTicks / 2; _target = -1;
			if (Landed) Phase = DroneBattery.Recharged(_battery) ? DronePhase.Loiter : DronePhase.Charging;
			else if (Phase == DronePhase.Recall) Phase = DronePhase.Loiter;
			if (Phase == DronePhase.Loiter) { _takeoff = D.TakeoffTicks; _hasWp = false; _dwell = -1; }
			NPC.netUpdate = true;
			return true;
		}

		/// <summary>Max life and defense follow the weapon's rarity tier (the same on a client, which only knows the weapon type). The life is never left above the max.</summary>
		private void ApplyWeaponStats()
		{
			NPC.lifeMax = DroneArsenal.MaxHpFor(WeaponType);
			NPC.defense = DroneArsenal.DefenseFor(WeaponType);
			if (NPC.life > NPC.lifeMax) NPC.life = NPC.lifeMax;
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
		public bool IsAlive => NPC.active && NPC.life > 0 && IsBound;

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
			if (_recoil > 0f) _recoil = Math.Max(0f, _recoil - D.RecoilReturnPx);
			if (!IsBound) return; // not yet bound / synced
			if (LevyUtil.IsClient) { ClientTick(); return; }

			_age++;
			if (_immuneTime > 0) _immuneTime--;
			if (++_validate >= D.ValidateIntervalTicks || _age == 1) { _validate = 0; if (!Validate()) return; }

			if (_immuneTime == 0) ContactDamage();                                   // melee enemies (vanilla deals no NPC-vs-NPC contact); hostile projectiles already strike friendly NPCs
			if (!NPC.active || NPC.life <= 0) return;

			Regen();
			UpdateBattery();
			if (!Armed && (Phase == DronePhase.Loiter || Phase == DronePhase.Charging)) StandDown(); // lost its weapon: go park
			Combat();

			bool engaging = Armed && _target >= 0 && _target < Main.maxNPCs && Shootable(Main.npc[_target]) && (Phase == DronePhase.Loiter);
			float wantAim = _aim;
			if (engaging)
			{
				NPC t = Main.npc[_target];
				Vector2 to = t.Center - NPC.Center;
				if (to != Vector2.Zero) wantAim = to.ToRotation();
				if (Math.Abs(to.X) > 4f) { NPC.direction = to.X < 0f ? -1 : 1; NPC.spriteDirection = NPC.direction; }
				if (_cooldown <= 0 && _takeoff <= 0) { Fire(t); _cooldown = _shot.IntervalTicks; }
			}
			else
			{
				if (Math.Abs(NPC.velocity.X) > 0.4f) { NPC.direction = NPC.velocity.X < 0f ? -1 : 1; NPC.spriteDirection = NPC.direction; }
				wantAim = NPC.direction < 0 ? MathHelper.Pi : 0f; // idle: the weapon rests level, pointing the way it faces
			}
			_aim = EaseAngle(_aim, wantAim, D.AimTurnLerp);
			NPC.ai[0] = _aim; // rides the NPC sync so clients draw the weapon where the server aims it

			Move(engaging);

			uint clock = Main.GameUpdateCount + (uint)NPC.whoAmI;
			if (engaging ? clock % (uint)D.AimSyncTicks == 0 : clock % (uint)D.NetRefreshTicks == 0) NPC.netUpdate = true;
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
		/// Every <see cref="D.ValidateIntervalTicks"/>: false (and the drone gone) when its Barracks is destroyed. A drone whose Armory is gone or whose weapon is no longer on the Armory's ledger
		/// (a soldier took it back, or the Armory spilled it) drops the loan and stands down to its pad. Also refreshes the base geometry and the commander.
		/// </summary>
		private bool Validate()
		{
			if (!(TileEntity.ByID.TryGetValue(BarracksId, out TileEntity te) && te is BarracksTE barracks)) { Dismantle(); return false; }
			if (_age == 1 || ++_padClock >= 10) { _padClock = 0; RefreshBase(barracks); }
			if (Weapon != null)
			{
				ArmoryTE armory = ArmoryOrNull();
				if (armory == null || !armory.IsLent(Weapon)) { DropLoan(); StandDown(); }  // the Armory was destroyed (its ledger spilled with it), or a soldier recalled the weapon: it is no longer ours to fire or return
			}
			if (CommanderIndex < 0 || CommanderIndex >= Main.maxPlayers || !Main.player[CommanderIndex].active) CommanderIndex = ShotKit.PickCommander(NPC.Center);
			return true;
		}

		private ArmoryTE ArmoryOrNull() => TileEntity.ByID.TryGetValue(ArmoryId, out TileEntity te) && te is ArmoryTE a ? a : null;

		/// <summary>Where the drone's base geometry sits: the loiter box's centre and roof line, and this slot's pad.</summary>
		private void RefreshBase(BarracksTE barracks)
		{
			_baseX = barracks.Center.X;
			_roofY = barracks.Position.Y * MechConstants.TilePx;
			_pad = PadFor(barracks, Slot);
		}

		/// <summary>Files the weapon and the borrowed round back into the Armory (idempotent: a second call, or one after a soldier already took the weapon, changes nothing), then forgets them and goes unarmed.</summary>
		public void ReleaseLoan()
		{
			ArmoryTE armory = ArmoryOrNull();
			if (armory != null)
			{
				if (Ammo != null) armory.ReturnLent(Ammo);
				if (Weapon != null) armory.ReturnLent(Weapon);
			}
			ClearWeapon();
		}

		/// <summary>The weapon is not ours to return (the Armory spilled it, or a soldier took it back): only a borrowed round, if still on the ledger, goes home.</summary>
		private void DropLoan()
		{
			ArmoryTE armory = ArmoryOrNull();
			if (armory != null && Ammo != null) armory.ReturnLent(Ammo);
			ClearWeapon();
		}

		private void ClearWeapon()
		{
			Weapon = null; Ammo = null; _shot = null; WeaponType = 0; _target = -1;
			ApplyWeaponStats();
			NPC.netUpdate = true;
		}

		/// <summary>The drone stands down and its loan goes home: used when its Barracks is gone, its Armory changed, or the manager dismisses it.</summary>
		public void Dismantle()
		{
			ReleaseLoan();
			Despawn();
		}

		private void Despawn()
		{
			NPC.life = 0; NPC.active = false; NPC.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
		}

		/// <summary>The drone lost its weapon on its own account: it flies to its pad and parks there UN-held, so the garrison re-arms it when a spare weapon turns up.</summary>
		private void StandDown()
		{
			Held = false;
			_target = -1;
			if (Landed) Phase = DronePhase.Parked;
			else if (Phase != DronePhase.Recall) Phase = DronePhase.Recall;
		}

		// =====================================================================================================
		// Orders (server / single player; DroneSystem validates the player)
		// =====================================================================================================
		/// <summary>Recall: fly home, land, file the weapon back, and stay parked until launched. A drone already parked is simply held. False when not bound.</summary>
		public bool OrderRecall()
		{
			if (!IsBound || !NPC.active) return false;
			Held = true;
			if (Phase == DronePhase.Parked) return true;
			_target = -1;
			Phase = DronePhase.Recall;
			NPC.netUpdate = true;
			return true;
		}

		/// <summary>True for a drone sitting parked and unarmed, ready for a launch order.</summary>
		public bool IsParked => Phase == DronePhase.Parked;

		// ---- upkeep: heal and recharge ----
		/// <summary>A drone not hurt for a while mends a little (twice as fast on its pad): a lost drone costs nothing to replace, but a scratched one should not stay scratched for ever.</summary>
		private void Regen()
		{
			if (NPC.life < _lastLife) _sinceHit = 0; else _sinceHit++;
			_lastLife = NPC.life;
			if (NPC.life >= NPC.lifeMax || _sinceHit < D.RegenDelayTicks) { _regenTimer = 0; return; }
			if (++_regenTimer < D.RegenIntervalTicks / (Landed ? D.PadRegenMult : 1)) return;
			_regenTimer = 0;
			NPC.life = Math.Min(NPC.lifeMax, NPC.life + Math.Max(1, (int)(NPC.lifeMax * D.RegenFraction)));
			_lastLife = NPC.life;
			NPC.netUpdate = true;
		}

		/// <summary>Drains the battery while airborne, refills it on the pad, and turns the corners: low sends a loitering drone home; a full battery sends a charging one back up.</summary>
		private void UpdateBattery()
		{
			_battery = DroneBattery.Step(_battery, !Landed, 1f / 60f);
			NPC.ai[2] = DroneBattery.Fraction(_battery);
			switch (Phase)
			{
				case DronePhase.Loiter:
					if (_takeoff <= 0 && DroneBattery.ShouldReturn(_battery, Vector2.Distance(NPC.Center, _pad))) { _target = -1; Phase = DronePhase.Refuel; }
					break;
				case DronePhase.Charging:
					if (Armed && DroneBattery.Recharged(_battery)) TakeOff();
					break;
			}
		}

		private void TakeOff()
		{
			Phase = DronePhase.Loiter;
			_takeoff = D.TakeoffTicks; _hasWp = false; _dwell = -1; _stuck = 0;
			NPC.netUpdate = true;
		}

		/// <summary>Takes contact damage from an overlapping hostile (vanilla applies no NPC-vs-NPC contact, so the unit applies it itself, as a soldier does). One hit per immune window.</summary>
		private void ContactDamage()
		{
			Rectangle mine = NPC.Hitbox;
			float cx = NPC.Center.X, cy = NPC.Center.Y;
			float halfW = NPC.width * 0.5f + Levy.Common.LevyConstants.ContactBroadPhasePx, halfH = NPC.height * 0.5f + Levy.Common.LevyConstants.ContactBroadPhasePx;
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
				_immuneTime = Levy.Common.LevyConstants.ImmuneTicksOnHit;
				if (NPC.active && NPC.life > 0) NPC.netUpdate = true;
				return; // one contact hit per tick
			}
		}

		// =====================================================================================================
		// Combat
		// =====================================================================================================
		private void Combat()
		{
			if (_cooldown > 0) _cooldown--;
			if (!Armed || (Phase != DronePhase.Loiter && Phase != DronePhase.Charging)) { _target = -1; return; }
			if (++_scan >= D.ScanIntervalTicks) { _scan = 0; _target = FindTarget(); }
			else if (_target >= 0 && (_target >= Main.maxNPCs || !Shootable(Main.npc[_target]))) _target = -1;

			// A charging drone with enough in the tank scrambles to meet an enemy that comes in reach of its pad.
			if (Phase == DronePhase.Charging && _target >= 0 && DroneBattery.CanScramble(_battery)) TakeOff();
		}

		/// <summary>The nearest hostile in the weapon's reach (and the leash's) with a clear line. The range cut runs first so the never-target test and the line test are only paid for the few slots actually in reach.</summary>
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
				if (!WithinLeashReach(t)) continue;
				if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height)) continue;
				bestSq = dSq; best = i;
			}
			return best;
		}

		private bool Shootable(NPC t)
		{
			if (!t.active || !LevyUtil.IsHostileTarget(t) || SoldierTargeting.IsNeverTarget(t)) return false;
			if (Vector2.DistanceSquared(NPC.Center, t.Center) > _shot.RangePx * _shot.RangePx) return false;
			if (!WithinLeashReach(t)) return false;
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
		}

		/// <summary>The enemy is one the drone can actually fight from inside its leash: the nearest point it may stand at is within the weapon's reach of it (so it never locks onto something it can only reach by leaving the line).</summary>
		private bool WithinLeashReach(NPC t)
		{
			if (_shot == null) return false;
			float reach = _shot.RangePx * 0.95f;
			return Vector2.DistanceSquared(t.Center, ClampToLeash(t.Center)) <= reach * reach;
		}

		/// <summary>One shot: the item's own projectile, aimed (led, and arced for an arrow) at the target, one to several per shot as the weapon fires, owned and tagged like a unit's shot.</summary>
		private void Fire(NPC target)
		{
			DroneShot shot = _shot;
			ArmoryTE armory = ArmoryOrNull();
			if (armory == null || !armory.IsLent(Weapon)) { _validate = D.ValidateIntervalTicks; return; } // the weapon was recalled this tick: the next upkeep stands the drone down

			Vector2 from = NPC.Center + new Vector2(0f, D.MuzzleDropPx);
			Vector2 vel = ShotKit.AimVelocity(shot, from, target, D.AimJitterDeg);
			Vector2 dir = vel.SafeNormalize(Vector2.UnitX * (NPC.direction == 0 ? 1 : NPC.direction));
			Vector2 muzzle = from + dir * D.MuzzleOffsetPx;
			if (Collision.SolidCollision(muzzle - new Vector2(2f, 2f), 4, 4)) muzzle = from; // a drone hugging a wall: the shot would die inside the tile, so it leaves the body instead
			_aim = NPC.ai[0] = dir.ToRotation(); // the weapon is always pointing where the round leaves

			int dmg = ShotKit.ScaledDamage(shot.BaseDamage * D.DamageMult);
			int owner = Main.myPlayer; // 255 on a dedicated server: a shot owned by any other index is an unsynced ghost
			for (int k = 0; k < shot.Count; k++)
			{
				float deg = shot.Count > 1 ? -shot.SpreadDeg * 0.5f + shot.SpreadDeg * k / (shot.Count - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, v.X, v.Y, shot.Projectile, dmg, shot.Knockback, owner);
				if (idx < 0 || idx >= Main.maxProjectiles) continue;
				ShotKit.TagShot(Main.projectile[idx], CommanderIndex, shot.Class, shot.Crit);
			}
			_shotSeq++;
			_recoil = D.RecoilPx;
			if (_cooldown >= D.AimSyncTicks) NPC.netUpdate = true; // a slow gun: sync each shot now (a fast one rides the aim-sync cadence)
			if (Main.netMode == NetmodeID.SinglePlayer) PlayFireSound(); // a remote client hears it from the synced shot counter instead
		}

		/// <summary>The thump of a shot: the mounted weapon's own use sound.</summary>
		private void PlayFireSound()
		{
			SoundStyle? style = null;
			if (WeaponType > 0 && WeaponType < ItemLoader.ItemCount && ContentSamples.ItemsByType.TryGetValue(WeaponType, out Item sample) && sample != null) style = sample.UseSound;
			SoundEngine.PlaySound(style ?? SoundID.Item11, NPC.Center);
		}

		// =====================================================================================================
		// Geometry: the pad, the loiter box and its leash
		// =====================================================================================================
		/// <summary>True when the tile can be stood on (solid ground or a platform).</summary>
		private static bool Standable(int tx, int ty)
		{
			if (!WorldGen.InWorld(tx, ty, 4)) return false;
			Tile t = Main.tile[tx, ty];
			return t.HasTile && !t.IsActuated && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);
		}

		private static bool SolidAt(int tx, int ty) => WorldGen.InWorld(tx, ty, 4) && WorldGen.SolidTile(tx, ty);

		/// <summary>
		/// Where a drone in formation slot <paramref name="slot"/> rests (its body centre): on the ground beside the Barracks, slots alternating left and right and fanning outward. The ground is found by
		/// climbing out of any solid at the pad's column and then dropping to the first thing to stand on; with none the pad is a hover point at the roof line.
		/// </summary>
		public static Vector2 PadFor(BarracksTE barracks, int slot)
		{
			float x = barracks.Center.X + DroneFlight.PadOffsetTiles(slot) * MechConstants.TilePx;
			int tx = (int)(x / MechConstants.TilePx);
			int ty = barracks.Position.Y + 2; // the Barracks' lowest row
			int climbed = 0;
			while (climbed < D.PadScanUpTiles && SolidAt(tx, ty)) { ty--; climbed++; }
			if (!SolidAt(tx, ty))
			{
				for (int down = 0; down < D.PadScanDownTiles; down++)
				{
					if (Standable(tx, ty + 1)) return new Vector2(x, (ty + 1) * MechConstants.TilePx - D.BodyHeight * 0.5f);
					ty++;
				}
			}
			return new Vector2(x, barracks.Position.Y * MechConstants.TilePx - D.BodyHeight * 0.5f - 2f);
		}

		/// <summary>The leash box: the loiter box widened by <see cref="D.LeashTiles"/> out the sides and up, and reaching <see cref="D.LeashDownTiles"/> below the roof line.</summary>
		private Vector2 ClampToLeash(Vector2 p)
		{
			float hx = (D.LoiterHalfWidthTiles + D.LeashTiles) * MechConstants.TilePx;
			float top = _roofY - (D.LoiterMaxAltTiles + D.LeashTiles) * MechConstants.TilePx, bottom = _roofY + D.LeashDownTiles * MechConstants.TilePx;
			return new Vector2(MathHelper.Clamp(p.X, _baseX - hx, _baseX + hx), MathHelper.Clamp(p.Y, top, bottom));
		}

		/// <summary>A random loiter waypoint in this slot's sector and altitude row, lifted out of any terrain; a column that is solid all the way up falls back to a point over the Barracks.</summary>
		private Vector2 PickWaypoint()
		{
			DroneFlight.PickWaypointTiles(Slot, Main.rand, out float dx, out float alt);
			float x = _baseX + dx * MechConstants.TilePx;
			int tx = (int)(x / MechConstants.TilePx);
			int ty = (int)((_roofY - alt * MechConstants.TilePx) / MechConstants.TilePx);
			int lifted = 0;
			while (lifted < 24 && (SolidAt(tx, ty) || SolidAt(tx, ty - 1) || SolidAt(tx, ty + 1))) { ty--; lifted++; }
			if (lifted >= 24) return new Vector2(_baseX, _roofY - D.LoiterMinAltTiles * MechConstants.TilePx);
			return new Vector2(x, ty * MechConstants.TilePx + MechConstants.TilePx * 0.5f);
		}

		// =====================================================================================================
		// Movement
		// =====================================================================================================
		private void Move(bool engaging)
		{
			switch (Phase)
			{
				case DronePhase.Charging:
				case DronePhase.Parked:
					HoldOnPad();
					return;
				case DronePhase.Refuel:
				case DronePhase.Recall:
					MoveHome();
					break;
				default:
					MoveLoiter(engaging);
					break;
			}
			WatchTrapped();
		}

		/// <summary>Rotors still, wheels down: the drone sits exactly on its pad.</summary>
		private void HoldOnPad()
		{
			NPC.velocity = Vector2.Zero;
			NPC.rotation = 0f;
			if (Vector2.DistanceSquared(NPC.Center, _pad) > 4f) { NPC.Center = _pad; NPC.netUpdate = true; }
			_stuck = 0; _farTicks = 0;
		}

		/// <summary>The loiter: climb out of the pad after a takeoff, press toward an enemy inside the leash, otherwise drift waypoint to waypoint with a pause at each.</summary>
		private void MoveLoiter(bool engaging)
		{
			float bob = (float)Math.Sin(_age * D.BobSpeed + NPC.whoAmI * 0.9f) * D.BobAmplitudePx;
			if (_takeoff > 0)
			{
				_takeoff--;
				NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0f, -D.TakeoffRisePx), D.SteerLerp * 2f);
				NPC.rotation = 0f;
				return;
			}
			Vector2 goal; float speed;
			if (engaging)
			{
				goal = EngageGoal(Main.npc[_target]); speed = D.EngageSpeedPx;
			}
			else
			{
				if (!_hasWp) { _wp = PickWaypoint(); _hasWp = true; _dwell = -1; _stuck = 0; }
				if (Vector2.Distance(NPC.Center, _wp) <= D.WaypointArriveTiles * MechConstants.TilePx)
				{
					if (_dwell < 0) _dwell = Main.rand.Next(D.DwellMinTicks, D.DwellMaxTicks + 1);
					if (--_dwell <= 0) { _hasWp = false; _dwell = -1; }
				}
				else _dwell = -1;
				goal = _wp; speed = D.LoiterSpeedPx;
			}
			goal.Y += bob;
			Steer(goal, speed, true);
		}

		/// <summary>Where a loitering drone goes to fight <paramref name="t"/>: toward it until it is within <see cref="D.EngageStandoffFraction"/> of the weapon's reach (then it holds), never beyond the leash.</summary>
		private Vector2 EngageGoal(NPC t)
		{
			Vector2 to = t.Center - NPC.Center;
			float d = to.Length(), stand = _shot.RangePx * D.EngageStandoffFraction;
			Vector2 want = d > stand && d > 1f ? NPC.Center + to / d * (d - stand) : NPC.Center;
			return ClampToLeash(want);
		}

		/// <summary>The run home: fly to a point above the pad, then settle straight down onto it. Touching down files the weapon back (a recall) or starts the charge (a refuel).</summary>
		private void MoveHome()
		{
			if (Vector2.Distance(NPC.Center, _pad) <= D.TouchdownPx) { Touchdown(); return; }
			float hd = Math.Abs(NPC.Center.X - _pad.X);
			bool approach = hd > 24f;
			Vector2 goal = approach ? _pad + new Vector2(0f, -D.ApproachAltTiles * MechConstants.TilePx) : _pad;
			Steer(goal, approach ? D.ReturnSpeedPx : D.LandSpeedPx, approach);
		}

		private void Touchdown()
		{
			NPC.Center = _pad;
			NPC.velocity = Vector2.Zero; NPC.rotation = 0f;
			_target = -1; _stuck = 0;
			if (Phase == DronePhase.Recall) { ReleaseLoan(); Phase = DronePhase.Parked; }
			else Phase = Armed ? DronePhase.Charging : DronePhase.Parked;
			NPC.netUpdate = true;
		}

		/// <summary>Eases the velocity toward the goal, slowing smoothly as it closes in. With <paramref name="assist"/> the drone rises over a hill in its path and climbs when it makes no headway.</summary>
		private void Steer(Vector2 goal, float maxSpeed, bool assist)
		{
			Vector2 to = goal - NPC.Center;
			float dist = to.Length();
			Vector2 wanted = Vector2.Zero;
			if (dist > 1f) wanted = to / dist * Math.Min(maxSpeed, dist * D.ArriveGain);
			if (assist && dist > 1f)
			{
				Vector2 ahead = NPC.Center + to / dist * Math.Min(dist, D.LookAheadPx);
				if (Collision.SolidCollision(ahead - new Vector2(NPC.width * 0.5f, NPC.height * 0.5f), NPC.width, NPC.height)) wanted.Y -= D.LookAheadClimbPx;
				if (NPC.velocity.Length() < D.StuckSpeedPx && dist > D.WaypointArriveTiles * MechConstants.TilePx * 2f) _stuck++; else _stuck = 0;
				if (_stuck >= D.StuckClimbTicks) wanted.Y -= D.StuckClimbPx;
				if (_stuck >= D.StuckGiveUpTicks) { _stuck = 0; _hasWp = false; }
			}
			NPC.velocity = Vector2.Lerp(NPC.velocity, wanted, D.SteerLerp);
			if (NPC.velocity.HasNaNs()) NPC.velocity = Vector2.Zero;
			NPC.rotation = MathHelper.Clamp(NPC.velocity.X * D.TiltPerSpeed, -D.MaxTilt, D.MaxTilt);
		}

		/// <summary>A flying drone far outside its leash for too long is trapped (a wall, a pit): put it back over its Barracks.</summary>
		private void WatchTrapped()
		{
			float far = D.SetBackFarTiles * MechConstants.TilePx;
			if (Vector2.DistanceSquared(NPC.Center, ClampToLeash(NPC.Center)) > far * far)
			{
				if (++_farTicks >= D.SetBackAfterTicks)
				{
					_farTicks = 0;
					NPC.Center = new Vector2(_baseX, _roofY - D.LoiterMinAltTiles * MechConstants.TilePx);
					NPC.oldPosition = NPC.position; NPC.velocity = Vector2.Zero; _hasWp = false;
					NPC.netUpdate = true;
					if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
				}
			}
			else _farTicks = 0;
		}

		// =====================================================================================================
		// Vanilla NPC hooks
		// =====================================================================================================
		public override bool CheckDead()
		{
			if (LevyUtil.IsServerOrSingle) ReleaseLoan(); // destroyed: the weapon returns to the Armory
			return true;
		}

		public override bool PreKill() => false;              // no vanilla loot
		public override bool CheckActive() => false;          // the garrison owns the lifecycle, not distance
		public override bool NeedSaving() => false;           // transient: the manager re-garrisons on load
		public override bool UsesPartyHat() => false;
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool? CanBeHitByItem(Player player, Item item) => false;
		public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.friendly && !projectile.hostile ? false : null;
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false; // we draw our own bar

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			modifiers.DefenseEffectiveness *= LevyUtil.ModeDefenseEffectiveness() / 0.5f;
		}

		// ---- right-click: status, and the recall / launch orders ----
		public override bool CanChat() => IsAlive;

		public override string GetChat()
		{
			int pct = (int)Math.Round(BatteryFraction * 100f);
			string weapon = WeaponType > 0 && WeaponType < ItemLoader.ItemCount ? Lang.GetItemNameValue(WeaponType) : "";
			return Phase switch
			{
				DronePhase.Loiter => Language.GetTextValue("Mods.Mechanization.UI.DroneChatLoiter", weapon, pct),
				DronePhase.Refuel => Language.GetTextValue("Mods.Mechanization.UI.DroneChatRefuel", pct),
				DronePhase.Charging => Language.GetTextValue("Mods.Mechanization.UI.DroneChatCharging", weapon, pct),
				DronePhase.Recall => Language.GetTextValue("Mods.Mechanization.UI.DroneChatRecall"),
				_ => Language.GetTextValue("Mods.Mechanization.UI.DroneChatParked", pct),
			};
		}

		public override void SetChatButtons(ref string button, ref string button2)
		{
			bool parked = Phase == DronePhase.Parked;
			button = Language.GetTextValue(parked ? "Mods.Mechanization.UI.DroneLaunch" : "Mods.Mechanization.UI.DroneRecall");
			button2 = Language.GetTextValue(parked ? "Mods.Mechanization.UI.DroneLaunchAll" : "Mods.Mechanization.UI.DroneRecallAll");
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			Player me = Main.LocalPlayer;
			bool parked = Phase == DronePhase.Parked;
			DroneOrder order = parked ? (firstButton ? DroneOrder.Launch : DroneOrder.LaunchAll) : (firstButton ? DroneOrder.Recall : DroneOrder.RecallAll);
			if (LevyUtil.IsClient) MechPackets.SendDroneOrder(order, NPC.whoAmI);   // the server owns the garrison and the Armory ledger
			else DroneSystem.Instance?.HandleOrder(me, order, NPC.whoAmI);
			me.SetTalkNPC(-1);
			Main.npcChatText = "";
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
		// Draw: a functional side-view built from rectangles (placeholder until the sprites land), the mounted weapon, and two bars.
		// =====================================================================================================
		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!IsBound || Main.dedServ) return false; // nothing to show until the sync says it is bound
			try
			{
				Texture2D px = TextureAssets.MagicPixel.Value;
				Vector2 c = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);
				Color light = Lighting.GetColor((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f), drawColor);
				float rot = NPC.rotation;
				int sd = NPC.direction == 0 ? 1 : NPC.direction;
				bool landed = Landed;

				// parts are laid out facing +x and mirrored by sd; every part turns with the body's tilt
				void Part(float x, float y, float w, float h, Color col) =>
					spriteBatch.Draw(px, c + new Vector2(x * sd, y).RotatedBy(rot), null, col, rot, new Vector2(0.5f, 0.5f), new Vector2(w, h), SpriteEffects.None, 0f);

				Color steel = Lit(new Color(88, 96, 110), light), dark = Lit(new Color(54, 60, 70), light), edge = Lit(new Color(136, 146, 162), light);
				Part(0f, 11f, 26f, 2f, dark);                                       // skid bar
				Part(-9f, 7f, 2f, 7f, dark); Part(9f, 7f, 2f, 7f, dark);            // skid struts
				Part(-16f, -2f, 11f, 3f, dark);                                     // tail boom
				Part(-20f, -6f, 3f, 7f, steel);                                     // tail fin
				Part(0f, 0f, 24f, 10f, steel);                                      // hull
				Part(0f, -4f, 24f, 1f, edge);                                       // hull highlight
				Part(12f, 1f, 6f, 6f, Lit(new Color(70, 170, 200), light));         // nose sensor
				Part(0f, -8f, 3f, 5f, dark);                                        // rotor mast
				Color led = Phase switch
				{
					DronePhase.Loiter => new Color(110, 235, 130),
					DronePhase.Refuel => new Color(255, 190, 80),
					DronePhase.Charging => new Color(255, 190, 80),
					DronePhase.Recall => new Color(110, 170, 255),
					_ => new Color(150, 150, 160),
				};
				Part(-6f, -1f, 3f, 3f, led);                                        // status light
				if (landed) Part(0f, -11f, 32f, 2f, dark);                          // rotor at rest
				else
				{
					Part(0f, -11f, 34f, 2f, new Color(190, 200, 212, 150));         // rotor, a flickering disc
					if (((Main.GameUpdateCount + (uint)NPC.whoAmI) & 2u) == 0u) Part(0f, -11f, 40f, 1f, new Color(190, 200, 212, 80));
				}
				DrawWeapon(spriteBatch, c, rot, light);
				return false;
			}
			catch (Exception e)
			{
				Mechanization.Instance.Logger.Warn("Mechanization: drawing a combat drone failed: " + e.Message);
				return false;
			}
		}

		private static Color Lit(Color baseColor, Color light) => new Color(baseColor.R * light.R / 255, baseColor.G * light.G / 255, baseColor.B * light.B / 255, (int)baseColor.A);

		/// <summary>The mounted weapon, hung under the body and turned to its aim (kept upright when aiming left), kicking back on a shot.</summary>
		private void DrawWeapon(SpriteBatch spriteBatch, Vector2 c, float rot, Color light)
		{
			if (WeaponType <= 0 || WeaponType >= ItemLoader.ItemCount) return;
			Main.instance.LoadItem(WeaponType);
			Texture2D wt = TextureAssets.Item[WeaponType]?.Value;
			if (wt == null || wt.Width <= 0 || wt.Height <= 0) return;
			float scale = Math.Min(1f, D.WeaponDrawMaxPx / Math.Max(wt.Width, wt.Height));
			SpriteEffects fx = Math.Cos(_aim) < 0.0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
			Vector2 kick = new Vector2((float)-Math.Cos(_aim), (float)-Math.Sin(_aim)) * _recoil;
			Vector2 at = c + new Vector2(0f, D.WeaponDrawOffsetY).RotatedBy(rot) + kick;
			spriteBatch.Draw(wt, at, null, light, _aim, new Vector2(wt.Width, wt.Height) * 0.5f, scale, fx, 0f);
		}

		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ || !IsBound) return;
			var pixel = TextureAssets.MagicPixel.Value;
			Vector2 top = NPC.Top - screenPos;

			int w = D.HpBarWidthPx, h = D.HpBarHeightPx;
			float frac = MathHelper.Clamp(NPC.life / (float)Math.Max(1, NPC.lifeMax), 0f, 1f);
			int bx = (int)(top.X - w * 0.5f), by = (int)(top.Y - D.HpBarOffsetY);
			spriteBatch.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), new Color(20, 20, 20, 200));
			spriteBatch.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), Color.Lerp(Color.Red, Color.LimeGreen, frac));

			// The battery: a thin bar over the healthbar, shown only when it matters (hovered, charging, or running low) so a healthy garrison stays uncluttered.
			float bat = BatteryFraction;
			bool hovered = !Main.LocalPlayer.mouseInterface && NPC.Hitbox.Contains(Main.MouseWorld.ToPoint());
			if (hovered || Phase == DronePhase.Charging || Phase == DronePhase.Refuel || Phase == DronePhase.Parked || bat <= D.ShowBarFraction)
			{
				int bw = D.BatteryBarWidthPx, bh = D.BatteryBarHeightPx;
				int cx = (int)(top.X - bw * 0.5f), cy = by - bh - D.BatteryBarGapPx;
				Color fill = bat > 0.5f ? new Color(90, 200, 255) : Color.Lerp(new Color(255, 90, 70), new Color(255, 200, 80), bat / 0.5f);
				spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 1, bw + 2, bh + 2), new Color(20, 20, 20, 200));
				spriteBatch.Draw(pixel, new Rectangle(cx, cy, Math.Max(bat > 0f ? 1 : 0, (int)(bw * bat)), bh), fill);
			}
		}

		// =====================================================================================================
		// Net: clients need the weapon type (sprite, bar size) and the shot counter. Life rides the vanilla NPC sync; aim, phase, battery and the bound flag ride ai[0..3].
		// =====================================================================================================
		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write7BitEncodedInt(WeaponType);
			writer.Write7BitEncodedInt(_shotSeq);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			int type = reader.Read7BitEncodedInt();
			if (type != WeaponType) { WeaponType = type; ApplyWeaponStats(); }
			int seq = reader.Read7BitEncodedInt();
			if (_seqSeen && seq != _shotSeq) { _recoil = D.RecoilPx; PlayFireSound(); } // the server fired since the last sync: kick and thump (the first read after joining stays quiet)
			_shotSeq = seq; _seqSeen = true;
			EnsureDummy();
		}
	}
}
