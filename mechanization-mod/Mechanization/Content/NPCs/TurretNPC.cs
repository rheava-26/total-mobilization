using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;                      // LevyConstants.ArrowGravity / ArrowGravityDelayTicks  (shared with the drone's aim solve)
using Levy.Common.Officers;             // OfficerBrain.TownDamageScale  (world-progress damage scale, as the drones use)
using Levy.Common.Globals;              // SoldierProjectileTag  (commander kill-credit, exactly as the drones tag their shots)
using Levy.Common.Targeting;            // ITargetBody / EnemyTargeting  (hostiles path to the turret exactly as they do to a drone)
using Levy.Common.Util;                 // LevyUtil.IsHostileTarget / IsClient  (cross-mod reference in content)
using Levy.Common.Weapons;              // DroneArsenal / DroneShot / Aiming / WeaponClassifier  (the drone's proven weapon-mount + shot path, reused)
using Levy.Content.NPCs;                // SoldierNPC.DummySlot  (the slot-255 swap the enemy-targeting system uses)
using Levy.Content.TileEntities;        // ArmoryTE  (the Levy Armory the surplus weapon is drawn from)
using Mechanization.Common;             // ArmoryLink  (which armories a spot reaches)
using Terraria.Localization;
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// A deployable automatic turret: a friendly, non-town, <c>aiStyle = -1</c> NPC that does NOT move. It falls under
	/// gravity until it rests on terrain, then sits still, acquires the nearest hostile in range and fires. It carries its
	/// own HP and a drawn healthbar, takes contact damage from melee enemies (vanilla applies no NPC-vs-NPC contact, so the
	/// turret applies it itself, exactly as Levy's DroneNPC does) and is damaged by hostile projectiles, and it is destroyed
	/// when its HP reaches 0.
	///
	/// THE GUN. The turret draws the best SURPLUS ranged weapon out of the nearest Levy Armory in reach and fires THAT
	/// weapon's real projectile — ammo-resolved, at the weapon's own damage, cadence, speed and spread — the same light
	/// path the combat drone uses (<see cref="DroneArsenal.BuildShot"/> over a non-consuming read of the Armory's racks; the
	/// weapon is never removed, so the army keeps it). Kills credit the nearest player through Levy's
	/// <see cref="SoldierProjectileTag"/>, exactly as a drone's shots do. With no Armory in reach, or nothing mountable in
	/// it, the turret falls back to a fixed round so it is never inert.
	///
	/// PERSISTENCE. The turret body is transient (<see cref="NeedSaving"/> is false), but <see cref="TurretSystem"/> saves
	/// each placed turret's position and HP in the world and re-spawns it on load; a destroyed one is dropped from that set.
	/// <see cref="RecordId"/> pairs this NPC to its saved record.
	///
	/// Server / single player drives the brain, picks the weapon and fires; clients draw the borrowed sprite (turned to the
	/// aim synced in ai[0]), the mounted weapon (its item type rides the extra-AI sync) and the healthbar.
	///
	/// ENEMY AGGRO. The turret is an <see cref="ITargetBody"/>, fed into Levy's <see cref="EnemyTargeting.Bodies"/> each tick by
	/// <see cref="TurretSystem"/>, so hostiles acquire and chase it through the same slot-255 dummy swap that points them at a soldier or
	/// a combat drone. The contact damage of an enemy that reaches it is applied by the turret itself in <see cref="ContactDamage"/>.
	/// </summary>
	public class TurretNPC : ModNPC, ITargetBody
	{
		/// <summary>Id of this turret's saved record in <see cref="TurretSystem"/> (server / single player; -1 until placed or restored). Not synced: clients never touch the registry.</summary>
		public int RecordId = -1;

		/// <summary>Free-standing Player swapped into Main.player[255] for one enemy AI call so the enemy chases this body (never in Main.player[] otherwise).</summary>
		public Player Dummy { get; private set; }

		// ---- brain state (server / single player) ----
		private int _age, _immuneTime, _scan, _target = -1, _cooldown, _weaponScan;
		/// <summary>The Armories this turret reaches, and the tick they were read (see <see cref="ArmoriesInReach"/>). Read on every netmode for display; the server also mounts from it.</summary>
		private readonly List<ArmoryTE> _pool = new();
		private uint _poolStamp;
		/// <summary>The weapon pattern the turret currently fires (the drone's prebuilt shot), or null to fire the fixed fallback round.</summary>
		private DroneShot _shot;
		/// <summary>The item type of the mounted weapon (server picks it; synced to clients so they can draw which gun it fires). 0 = none / fixed fallback round.</summary>
		private int _weaponType;
		/// <summary>How far (px) the turret engages with what it has mounted. The server works it out; it rides the extra-AI sync so a client can draw the reach ring and read it back.</summary>
		private float _reachPx = T.RangePx;
		/// <summary>Counts shots fired. Rides the extra-AI sync so a remote client hears each volley and kicks the barrel back (the projectiles themselves are synced separately).</summary>
		private int _shotSeq;
		private bool _seqSeen;
		/// <summary>Barrel kick-back, px, decaying to 0 (drawn only).</summary>
		private float _recoil;
		/// <summary>The drawn aim of the barrel (radians), eased toward the wanted one; a client eases it toward the synced ai[0].</summary>
		private float _aim;

		// A valid vanilla asset so content loads; the real body is drawn in PreDraw and this is never actually blitted.
		public override string Texture => "Terraria/Images/Item_" + T.BodyDrawItemType;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.ActsLikeTownNPC[Type] = true;        // keeps it out of hostile spawn logic, as Levy's drone/soldier do
			NPCID.Sets.NoTownNPCHappiness[Type] = true;
			NPCID.Sets.CantTakeLunchMoney[Type] = true;
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
		}

		public override void SetDefaults()
		{
			NPC.width = T.BodyWidth; NPC.height = T.BodyHeight;
			NPC.friendly = true; NPC.townNPC = false;
			NPC.aiStyle = -1;
			NPC.damage = 0; NPC.defense = T.DefenseBase;
			NPC.lifeMax = T.HpBase; NPC.life = NPC.lifeMax;
			NPC.knockBackResist = 0f;                        // immovable
			NPC.npcSlots = 0f;                               // never thins the hostile spawn pressure
			NPC.HitSound = SoundID.NPCHit4;                  // metallic
			NPC.DeathSound = SoundID.NPCDeath14;             // small explosion
			NPC.lavaImmune = false;
			NPC.noGravity = true;                            // core adds no gravity for aiStyle -1; the turret applies its own in AI
			NPC.noTileCollide = false;                       // so it rests on solid ground
			NPC.rarity = 0; NPC.value = 0f;
			NPC.netAlways = true;                            // a stationary combat object is always relevant to nearby clients
			EnsureDummy();
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }

		// =====================================================================================================
		// ITargetBody — enemies chase the turret exactly like a soldier's or a drone's body (EnemyTargeting swaps Dummy into slot 255).
		// =====================================================================================================
		/// <summary>Taunt weight added to this body when enemies weigh it against the commander.</summary>
		public int TargetAggro => T.Aggro;
		/// <summary>Active and not knocked out: a live turret draws enemy fire; a destroyed one is dropped from the body list the same tick.</summary>
		public bool IsAlive => NPC.active && NPC.life > 0;

		private void EnsureDummy()
		{
			if (Dummy != null) return;
			Dummy = new Player { whoAmI = SoldierNPC.DummySlot, active = true, dead = false, ghost = false, difficulty = 0, direction = 1 };
			Dummy.width = T.BodyWidth; Dummy.height = T.BodyHeight;
		}

		/// <summary>Moves the dummy onto the turret just before it is swapped into slot 255, so the enemy's AI reads the turret's position as its target.</summary>
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
		// AI
		// =====================================================================================================
		public override void AI()
		{
			EnsureDummy();
			SyncDummyTransform(); // keep the targeting dummy co-located on every netmode (NPC AI, and the slot-255 swap, run on clients too)
			if (_recoil > 0f) _recoil = Math.Max(0f, _recoil - T.RecoilReturnPx);
			if (LevyUtil.IsClient) { _aim = EaseAngle(_aim, NPC.ai[0], T.AimTurnLerp); return; }

			_age++;
			if (_immuneTime > 0) _immuneTime--;
			if (--_weaponScan <= 0) { _weaponScan = T.WeaponRefreshTicks; RefreshWeapon(); } // re-pick the mounted weapon from the nearest Armory on a cadence

			ApplyGravity();
			if (_immuneTime == 0) ContactDamage();
			if (!NPC.active || NPC.life <= 0) return;

			Combat();
			bool engaging = _target >= 0 && _target < Main.maxNPCs && Shootable(Main.npc[_target]);
			float wantAim = _aim;
			if (engaging)
			{
				NPC t = Main.npc[_target];
				Vector2 to = t.Center - NPC.Center;
				if (to != Vector2.Zero) wantAim = to.ToRotation();
				if (Math.Abs(to.X) > 4f) { NPC.direction = to.X < 0f ? -1 : 1; NPC.spriteDirection = NPC.direction; }
				if (_cooldown <= 0)
				{
					Fire(t);
					_cooldown = CurrentFireInterval;
					if (_cooldown >= T.AimSyncTicks) NPC.netUpdate = true; // a slow gun: sync each shot now (a fast one rides the aim-sync cadence below)
				}
			}
			else wantAim = NPC.direction < 0 ? MathHelper.Pi : 0f; // idle: barrel level, facing its last way

			_aim = EaseAngle(_aim, wantAim, T.AimTurnLerp);
			NPC.ai[0] = _aim; // rides the NPC sync so clients draw the barrel where the server aims it

			// While engaging, push the aim (and the shot counter) to clients often so the barrel tracks and the volleys are heard; idle, a slow heartbeat is plenty.
			uint clock = Main.GameUpdateCount + (uint)NPC.whoAmI;
			if (engaging ? clock % (uint)T.AimSyncTicks == 0 : clock % 30u == 0) NPC.netUpdate = true;
		}

		private static float EaseAngle(float from, float to, float t) => MathHelper.WrapAngle(from + MathHelper.WrapAngle(to - from) * t);

		/// <summary>Falls until it rests on terrain, then stays put. The core applies this velocity with tile collision.</summary>
		private void ApplyGravity()
		{
			NPC.velocity.X = 0f;
			NPC.velocity.Y += T.GravityPerTick;
			if (NPC.velocity.Y > T.MaxFallSpeed) NPC.velocity.Y = T.MaxFallSpeed;
			NPC.rotation = 0f;
		}

		// ---- combat ----
		private void Combat()
		{
			if (_cooldown > 0) _cooldown--;
			// A turret holds the target it has while that target stays shootable, and only swaps for a hostile clearly closer: no flip-flopping between two enemies at the same range.
			if (_target >= 0 && (_target >= Main.maxNPCs || !Shootable(Main.npc[_target]))) { _target = -1; _scan = T.ScanIntervalTicks; } // lost it: look again this tick
			if (++_scan < T.ScanIntervalTicks) return;
			_scan = 0;
			int best = FindTarget();
			if (best == _target || best < 0) return;
			if (_target < 0) { _target = best; return; }
			float dCur = Vector2.Distance(NPC.Center, Main.npc[_target].Center), dBest = Vector2.Distance(NPC.Center, Main.npc[best].Center);
			if (dBest < dCur * T.RetargetCloserFraction) _target = best;
		}

		/// <summary>Nearest legitimate target in reach with a clear line. Range cut runs first so the never-target and line tests are only paid for the slots actually in reach.</summary>
		private int FindTarget()
		{
			float range = CurrentRangePx;
			float rangeSq = range * range;
			Vector2 from = NPC.Center;
			int best = -1; float bestSq = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float dSq = Vector2.DistanceSquared(from, t.Center);
				if (dSq >= bestSq || dSq > rangeSq) continue;
				if (SoldierTargeting.IsNeverTarget(t)) continue; // Levy's proven filter (hostile, not a set piece / bound NPC / projectile-NPC / flee target), the same one the drones use
				if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height)) continue;
				bestSq = dSq; best = i;
			}
			return best;
		}

		private bool Shootable(NPC t)
		{
			if (!t.active || SoldierTargeting.IsNeverTarget(t)) return false;
			float range = CurrentRangePx;
			if (Vector2.DistanceSquared(NPC.Center, t.Center) > range * range) return false;
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
		}

		/// <summary>Reach, in px: the mounted weapon's real reach when one is up (see <see cref="ReachFor"/>), else the plain bullet's.</summary>
		private float CurrentRangePx => _reachPx;

		/// <summary>Reach in whole tiles (what the hover text and the chat panel say).</summary>
		public int ReachTiles => (int)Math.Round(_reachPx / LevyConstants.TilePx);
		/// <summary>Reach in px, for drawing the reach ring.</summary>
		public float ReachPx => _reachPx;

		/// <summary>Ticks between shots: the mounted weapon's own cadence when one is up, else the fallback cadence.</summary>
		private int CurrentFireInterval => _shot != null && _shot.Valid ? _shot.IntervalTicks : T.FireIntervalTicks;

		/// <summary>
		/// How far (px) a turret should engage with <paramref name="weapon"/>'s <paramref name="shot"/>: the reach Levy's own classifier gives a soldier carrying it (so a long gun is
		/// not cut to a drone's tighter leash), cut down to where the round really flies when it dies young (flames, short bolts), never below <see cref="T.MinReachTiles"/>. A fixed
		/// floor used to make a flamethrower open fire on targets its flames could never reach.
		/// </summary>
		public static float ReachFor(Item weapon, DroneShot shot)
		{
			float reach = shot != null ? shot.RangePx : T.RangePx;
			try
			{
				WeaponProfile profile = WeaponClassifier.GetProfile(weapon);
				if (profile != null && profile.RangeTiles > 0) reach = profile.RangeTiles * LevyConstants.TilePx;
				Projectile sample = shot != null ? WeaponClassifier.ProjectileSample(shot.Projectile) : null;
				if (shot != null && sample != null && !shot.Homing)
				{
					// A flamethrower's flames list a long lifetime that their AI cuts to a few dozen updates; any other short-lived round just runs out its own. Velocity and lifetime both count in
					// updates (an extra-update projectile moves AND ages that many times a tick), so the distance flown is speed x lifetime with no extra-update factor.
					int life = sample.aiStyle == ProjAIStyleID.Flamethrower ? Math.Min(sample.timeLeft, T.FlameLifetimeUpdates) : sample.timeLeft;
					if (life <= LevyConstants.LongShotLifetimeTicks)
						reach = Math.Min(reach, Math.Max(1f, shot.Speed) * Math.Max(1, life) * LevyConstants.ShortShotReachFraction);
				}
			}
			catch (Exception) { /* keep the shot's own reach */ }
			return Math.Max(reach, T.MinReachTiles * LevyConstants.TilePx);
		}

		// =====================================================================================================
		// The gun: pull the best surplus ranged weapon from the Levy Armories in reach and fire its real projectile.
		// =====================================================================================================
		/// <summary>Re-picks the mounted weapon from the best of the Armories in reach (a non-consuming read of their racks). Null shot = the fixed fallback round.</summary>
		private void RefreshWeapon()
		{
			List<ArmoryTE> pool = ArmoriesInReach(true);
			if (pool.Count == 0) { ClearWeapon(); return; }
			Item weapon = BestSurplus(pool);
			if (weapon == null) { ClearWeapon(); return; }
			Item ammo = DroneArsenal.TakesAmmo(weapon) ? BestAmmoFor(pool, weapon.useAmmo) : null;
			DroneShot shot = DroneArsenal.BuildShot(weapon, ammo); // the drone's own shot builder: projectile, speed, damage, cadence, spread, gravity, homing
			if (shot == null || !shot.Valid) { ClearWeapon(); return; }
			_shot = shot;
			_cooldown = Math.Min(_cooldown, shot.IntervalTicks); // a slow gun's leftover delay never holds back a quick one that replaces it
			SetReach(ReachFor(weapon, shot));
			SetWeaponType(weapon.type); // clients draw this item's sprite on the barrel
		}

		private void ClearWeapon() { _shot = null; SetReach(T.RangePx); SetWeaponType(0); }

		private void SetReach(float px)
		{
			if (Math.Abs(_reachPx - px) < 1f) return;
			_reachPx = px;
			NPC.netUpdate = true;
		}

		/// <summary>Records the mounted weapon's item type and flags a sync when it changes, so clients redraw the right gun.</summary>
		private void SetWeaponType(int type)
		{
			if (_weaponType == type) return;
			_weaponType = type;
			NPC.netUpdate = true;
		}

		/// <summary>
		/// Every Levy Armory this turret can draw from (see <see cref="ArmoryLink"/>: the nearest in reach, plus its whole pylon network, plus the
		/// network when the turret stands at a pylon). Cached for <see cref="T.LinkRefreshTicks"/> so chat, hover and the brain share one read;
		/// <paramref name="force"/> re-reads now. Do not mutate the returned list.
		/// </summary>
		public List<ArmoryTE> ArmoriesInReach(bool force = false)
		{
			uint now = Main.GameUpdateCount;
			if (force || _poolStamp == 0 || now - _poolStamp >= (uint)T.LinkRefreshTicks || now < _poolStamp)
			{
				_poolStamp = now == 0 ? 1u : now;
				ArmoryLink.Find(NPC.Center, _pool);
			}
			return _pool;
		}

		/// <summary>The item type of the mounted weapon, or 0 when it fires the plain fallback bullet. Synced to clients.</summary>
		public int MountedWeaponType => _weaponType;

		/// <summary>
		/// The strongest surplus weapon on an Armory's racks that a turret can fire: issue rows and bulk storage, scored by
		/// <see cref="WeaponClassifier.Score"/>, filtered to RANGED or MAGIC weapons the drone arsenal accepts as mountable. A non-consuming
		/// read — the weapon stays the army's; the turret only mirrors its projectile. Melee, summon, whip and sentry are excluded by
		/// <see cref="DroneArsenal.IsMountable"/>. A caster just fires its projectile — a turret keeps no mana economy (generous by design).
		/// </summary>
		private static Item BestSurplus(List<ArmoryTE> armories)
		{
			Item best = null; float bestScore = float.NegativeInfinity;
			void Consider(Item it)
			{
				if (it == null || it.IsAir) return;
				DamageClass cls = WeaponClassifier.ItemDamageClass(it);
				if (cls != DamageClass.Ranged && cls != DamageClass.Magic) return;
				if (!DroneArsenal.IsMountable(it)) return;
				float s = WeaponClassifier.Score(it);
				if (s > bestScore) { bestScore = s; best = it; }
			}
			foreach (ArmoryTE armory in armories)
			{
				foreach (Item it in armory.IssueWeapons) Consider(it);
				foreach (Item it in armory.Storage) Consider(it);
			}
			return best;
		}

		/// <summary>The best stocked round of a given ammo kind (highest damage, then rarity, as the drone's ammo loan ranks it), or null to let the shot builder use the default round. Non-consuming.</summary>
		private static Item BestAmmoFor(List<ArmoryTE> armories, int ammoId)
		{
			if (ammoId <= 0) return null;
			Item best = null; float bestScore = float.NegativeInfinity;
			void Consider(Item it)
			{
				if (it == null || it.IsAir || it.ammo != ammoId || it.IsACoin) return;
				float s = it.damage * 100f + WeaponClassifier.RarityTier(it.rare);
				if (s > bestScore) { bestScore = s; best = it; }
			}
			foreach (ArmoryTE armory in armories)
			{
				foreach (Item it in armory.IssueAmmo) Consider(it);
				foreach (Item it in armory.Storage) Consider(it);
			}
			return best;
		}

		/// <summary>One cannonade: the mounted weapon's real projectile (one to several as it fires) aimed with lead/arc at the target, else the fixed fallback round. Owned by the real player and credited to the nearest commander.</summary>
		private void Fire(NPC target)
		{
			int commander = PickCommander(NPC.Center);
			if (_shot != null && _shot.Valid) FireSurplus(target, commander);
			else FireFixed(target, commander);
			_shotSeq++;
			_recoil = T.RecoilPx;
			if (Main.netMode == NetmodeID.SinglePlayer) PlayFireSound(); // a remote client hears it from the synced shot counter instead
		}

		/// <summary>The thump of a shot: the mounted weapon's own use sound, else the plain bullet's.</summary>
		private void PlayFireSound()
		{
			SoundStyle? style = null;
			if (_weaponType > 0 && _weaponType < ItemLoader.ItemCount && ContentSamples.ItemsByType.TryGetValue(_weaponType, out Item sample) && sample != null) style = sample.UseSound;
			SoundEngine.PlaySound(style ?? SoundID.Item11, NPC.Center);
		}

		/// <summary>The damage a round is fired for: <paramref name="baseDamage"/> scaled with world progress exactly as the combat drone's rounds are, and by the game mode's town-NPC multiplier.</summary>
		private static int ScaledDamage(float baseDamage)
		{
			float dmg = baseDamage * OfficerBrain.TownDamageScale();
			if (Main.expertMode) dmg *= Main.GameModeInfo.TownNPCDamageMultiplier;
			return Math.Max(1, (int)Math.Round(dmg));
		}

		/// <summary>Fires the mounted weapon's projectile — the drone's light shot path: lead/arc aim, the weapon's multishot spread, its damage and knockback, tagged for kill-credit.</summary>
		private void FireSurplus(NPC target, int commander)
		{
			DroneShot shot = _shot;
			Vector2 from = NPC.Center;
			Vector2 vel = AimVelocity(shot, from, target);
			Vector2 dir = vel.SafeNormalize(Vector2.UnitX * (NPC.direction == 0 ? 1 : NPC.direction));
			Vector2 muzzle = from + dir * T.MuzzleOffsetPx;
			if (Collision.SolidCollision(muzzle - new Vector2(2f, 2f), 4, 4)) muzzle = from; // a muzzle buried in a tile would kill the round on spawn
			_aim = NPC.ai[0] = dir.ToRotation(); // the barrel is always pointing where the round leaves (it had only eased toward it)

			int dmg = ScaledDamage(shot.BaseDamage * T.SurplusDamageMult);

			int owner = Main.myPlayer; // 255 on a dedicated server: a shot owned by any other index is an unsynced ghost
			for (int k = 0; k < shot.Count; k++)
			{
				float deg = shot.Count > 1 ? -shot.SpreadDeg * 0.5f + shot.SpreadDeg * k / (shot.Count - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, v.X, v.Y, shot.Projectile, dmg, shot.Knockback, owner);
				if (idx < 0 || idx >= Main.maxProjectiles) continue;
				TagTurretShot(Main.projectile[idx], commander, shot.Class, shot.Crit);
			}
		}

		/// <summary>Fires the fixed fallback round straight at the target (no Armory in reach, or nothing mountable in it), so a turret is never inert.</summary>
		private void FireFixed(NPC target, int commander)
		{
			Vector2 from = NPC.Center;
			Vector2 vel = Aiming.SolveIntercept(from, T.ShotSpeed, target.Center, target.velocity, false); // leads a moving target, like the mounted weapons do
			Vector2 dir = vel.SafeNormalize(Vector2.UnitX * (NPC.direction == 0 ? 1 : NPC.direction));
			Vector2 muzzle = from + dir * T.MuzzleOffsetPx;
			if (Collision.SolidCollision(muzzle - new Vector2(2f, 2f), 4, 4)) muzzle = from;
			_aim = NPC.ai[0] = dir.ToRotation();

			int owner = Main.myPlayer;
			int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, vel.X, vel.Y, T.ProjectileType, ScaledDamage(T.ShotDamage), T.ShotKnockback, owner);
			if (idx >= 0 && idx < Main.maxProjectiles) TagTurretShot(Main.projectile[idx], commander, DamageClass.Ranged, 0);
		}

		/// <summary>
		/// Makes a spawned round a friendly, NPC-style shot owned by the real player and credited to <paramref name="commander"/> through Levy's
		/// <see cref="SoldierProjectileTag"/> — exactly the credit a combat drone's shot gets, set here directly since the turret is not a Levy body
		/// its OnSpawn recognises. A piercing round gets its own hit-immunity so it does not share the owner's cooldown with every other shooter.
		/// </summary>
		private static void TagTurretShot(Projectile p, int commander, DamageClass cls, int crit)
		{
			if (!p.active) return;
			p.friendly = true; p.hostile = false;
			if (cls != null) p.DamageType = cls;
			if (crit > 0) p.CritChance = crit;
			if (p.penetrate != 1 && !p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity)
			{ p.usesLocalNPCImmunity = true; if (p.localNPCHitCooldown <= 0) p.localNPCHitCooldown = 10; }
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag))
			{
				tag.CommanderIndex = commander; // ModifyHitNPC credits this player before the kill drops loot (banners, bestiary, "defeated by")
				tag.DroneShot = true;           // spares the player's own base from the turret's blasts, as a drone's rocket is spared
			}
			p.npcProj = true; p.noDropItem = true; // the commander's own on-hit item effects and drops never run for the turret's shot
			p.netUpdate = true;
		}

		/// <summary>Lead, and the arc of a falling arrow: the same intercept solve the drone uses. Homing ammo flies straight at the target and needs no lead.</summary>
		private Vector2 AimVelocity(DroneShot shot, Vector2 from, NPC target)
		{
			Vector2 at = target.Center;
			if (shot.Homing)
			{
				Vector2 d = at - from;
				if (d == Vector2.Zero) d = Vector2.UnitX;
				return Vector2.Normalize(d) * shot.Speed;
			}
			float steps = shot.StepMult <= 0f ? 1f : shot.StepMult; // the shot advances this many times a tick, and gravity runs per advance
			float leadSpeed = Math.Max(0.01f, shot.Speed) * steps;
			return Aiming.SolveIntercept(from, leadSpeed, at, target.velocity, shot.Gravity,
				shot.Gravity ? LevyConstants.ArrowGravity * steps * steps : 0f,
				shot.Gravity ? (int)Math.Round(LevyConstants.ArrowGravityDelayTicks / steps) : 0) / steps;
		}

		/// <summary>The player a turret's kills are credited to: the nearest active player, a living one before a dead one. -1 when nobody is on. (A small re-implementation of the drone system's own pick, to avoid exposing a Levy internal.)</summary>
		private static int PickCommander(Vector2 near)
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active) continue;
				float d = Vector2.DistanceSquared(p.Center, near);
				if (p.dead) d += 1e12f;
				if (d < bestD) { bestD = d; best = i; }
			}
			return best;
		}

		// ---- taking contact hits (vanilla applies no NPC-vs-NPC contact, so the turret applies it itself) ----
		private void ContactDamage()
		{
			Rectangle mine = NPC.Hitbox;
			float cx = NPC.Center.X, cy = NPC.Center.Y;
			float halfW = NPC.width * 0.5f + T.ContactBroadPhasePx, halfH = NPC.height * 0.5f + T.ContactBroadPhasePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.friendly || a.damage <= 0 || i == NPC.whoAmI) continue; // only hostiles that deal contact damage
				if (Math.Abs(a.Center.X - cx) > a.width * 0.5f + halfW || Math.Abs(a.Center.Y - cy) > a.height * 0.5f + halfH) continue;
				if (!NPCLoader.CanHitNPC(a, NPC)) continue;
				int slot = -1; float mult = 1f;
				Rectangle rect = a.Hitbox;
				NPC.GetMeleeCollisionData(mine, i, ref slot, ref mult, ref rect);
				if (!mine.Intersects(rect)) continue;

				int dir = a.Center.X < cx ? 1 : -1;
				int raw = Math.Max(1, Main.DamageVar(a.damage * mult));
				NPC.SimpleStrikeNPC(raw, dir, false, 0f, null, false, 0f, true); // the server broadcasts the strike
				_immuneTime = T.ImmuneTicksOnHit;
				if (NPC.active && NPC.life > 0) NPC.netUpdate = true;
				return; // one contact hit per tick
			}
		}

		// =====================================================================================================
		// Vanilla NPC hooks
		// =====================================================================================================
		public override bool CheckDead() => true;
		public override bool PreKill() => false;              // no vanilla loot
		public override bool CheckActive() => false;          // a placed turret does not despawn by distance
		public override bool NeedSaving() => false;           // the NPC body is transient; TurretSystem saves the placement and re-spawns it on load
		public override bool UsesPartyHat() => false;
		// Right-click opens a small panel: what it has mounted, whether an Armory is in reach, and a DISMANTLE button that hands the turret back as an item.
		public override bool CanChat() => IsAlive;
		public override string GetChat()
		{
			int n = ArmoriesInReach(true).Count;
			if (_weaponType > 0 && _weaponType < ItemLoader.ItemCount)
				return Language.GetTextValue("Mods.Mechanization.UI.TurretChatMounted", Lang.GetItemNameValue(_weaponType), n, ReachTiles);
			return Language.GetTextValue(n > 0 ? "Mods.Mechanization.UI.TurretChatNoWeapon" : "Mods.Mechanization.UI.TurretChatNoArmory", ReachTiles);
		}
		public override void SetChatButtons(ref string button, ref string button2) => button = Language.GetTextValue("Mods.Mechanization.UI.TurretDismantle");
		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (!firstButton) return;
			Player me = Main.LocalPlayer;
			if (LevyUtil.IsClient) MechPackets.SendDismantle(NPC.whoAmI);   // the server owns the registry and the drop
			else TurretSystem.Instance?.Dismantle(NPC, me);
			me.SetTalkNPC(-1);
			Main.npcChatText = "";
		}
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool? CanBeHitByItem(Player player, Item item) => false;                 // no friendly fire from the owner's tools
		public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.friendly && !projectile.hostile ? false : null; // hostile projectiles hit; friendly ones do not
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false; // we draw our own bar

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (Main.netMode == NetmodeID.Server) return;
			int sparks = NPC.life > 0 ? 3 : 18;
			for (int i = 0; i < sparks; i++)
			{
				int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Iron, hit.HitDirection * 1.8f, -1.5f);
				Main.dust[d].scale = 0.9f;
			}
			if (NPC.life <= 0)
			{
				for (int i = 0; i < 8; i++)
					Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Smoke, hit.HitDirection, -2f, 100, default, 1.3f);
			}
		}

		// =====================================================================================================
		// Draw: borrow a vanilla item sprite (no new art) turned to the aim, plus a healthbar.
		// =====================================================================================================
		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ) return false;
			try
			{
				Texture2D px = TextureAssets.MagicPixel.Value;
				Vector2 c = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);
				Color light = Lighting.GetColor((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f), drawColor);

				// The fixed plinth: a wide slab with a pedestal on it. It never turns; only the gun does, so the thing always reads as an emplacement standing on the ground.
				float bottom = c.Y + NPC.height * 0.5f;
				float slabTop = bottom - T.PlinthHeightPx, pedTop = slabTop - T.PedestalHeightPx;
				Rect(spriteBatch, px, c.X - T.PlinthWidthPx * 0.5f, slabTop, T.PlinthWidthPx, T.PlinthHeightPx, Lit(new Color(62, 67, 76), light));
				Rect(spriteBatch, px, c.X - T.PlinthWidthPx * 0.5f, slabTop, T.PlinthWidthPx, 1, Lit(new Color(122, 130, 144), light));
				Rect(spriteBatch, px, c.X - T.PedestalWidthPx * 0.5f, pedTop, T.PedestalWidthPx, T.PedestalHeightPx, Lit(new Color(84, 90, 102), light));

				// The gun: a steel barrel that turns on a pivot atop the pedestal, kicks back on a shot, with the mounted weapon's own sprite on it.
				Vector2 pivot = new Vector2(c.X, pedTop - T.PivotUpPx);
				Vector2 dir = new Vector2((float)Math.Cos(_aim), (float)Math.Sin(_aim));
				Vector2 basePos = pivot - dir * _recoil;
				spriteBatch.Draw(px, basePos, null, Lit(new Color(104, 112, 126), light), _aim, new Vector2(0f, 0.5f), new Vector2(T.BarrelLengthPx, T.BarrelThicknessPx), SpriteEffects.None, 0f);
				DrawMountedWeapon(spriteBatch, basePos, dir, light);
				Rect(spriteBatch, px, pivot.X - 4f, pivot.Y - 4f, 8, 8, Lit(new Color(58, 63, 72), light)); // the pivot hub
				if (_recoil > T.RecoilPx * 0.6f) // muzzle flash, only for the first frames after a shot
				{
					Vector2 tip = basePos + dir * (T.WeaponDrawOffsetPx + T.WeaponDrawMaxPx * 0.5f);
					spriteBatch.Draw(px, tip, null, new Color(255, 220, 140, 0), _aim, new Vector2(0.5f, 0.5f), new Vector2(9f, 5f), SpriteEffects.None, 0f);
				}
				return false;
			}
			catch (Exception e)
			{
				Mechanization.Instance.Logger.Warn("Mechanization: drawing a turret failed: " + e.Message);
				return false;
			}
		}

		private static void Rect(SpriteBatch sb, Texture2D px, float x, float y, int w, int h, Color col) =>
			sb.Draw(px, new Rectangle((int)Math.Round(x), (int)Math.Round(y), w, h), col);

		/// <summary>A flat colour dimmed by the light at the turret's spot.</summary>
		private static Color Lit(Color baseColor, Color light) => new Color(baseColor.R * light.R / 255, baseColor.G * light.G / 255, baseColor.B * light.B / 255, (int)baseColor.A);

		/// <summary>
		/// The mounted weapon's item sprite, pushed out along the aim and turned to it (kept upright when aiming left). A staff's sprite points up and to the right, so it is turned a further
		/// 45 degrees (and mirrored, not flipped, when aiming left) to point down the barrel. Nothing when no weapon is mounted — the bare barrel shows alone.
		/// </summary>
		private void DrawMountedWeapon(SpriteBatch spriteBatch, Vector2 basePos, Vector2 dir, Color light)
		{
			if (_weaponType <= 0 || _weaponType >= ItemLoader.ItemCount) return;
			Main.instance.LoadItem(_weaponType);
			Texture2D wt = TextureAssets.Item[_weaponType]?.Value;
			if (wt == null || wt.Width <= 0 || wt.Height <= 0) return;
			float scale = Math.Min(1f, T.WeaponDrawMaxPx / Math.Max(wt.Width, wt.Height));
			Vector2 at = basePos + dir * T.WeaponDrawOffsetPx;
			bool left = dir.X < 0f;
			float rot = _aim;
			SpriteEffects fx = left ? SpriteEffects.FlipVertically : SpriteEffects.None;
			if (_weaponType < Item.staff.Length && Item.staff[_weaponType])
			{
				rot = _aim + (left ? 3f : 1f) * MathHelper.PiOver4;
				fx = left ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			}
			spriteBatch.Draw(wt, at, null, light, rot, new Vector2(wt.Width, wt.Height) * 0.5f, scale, fx, 0f);
		}

		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ) return;
			int w = T.HpBarWidthPx, h = T.HpBarHeightPx;
			Vector2 top = NPC.Top - screenPos;
			float frac = MathHelper.Clamp(NPC.life / (float)Math.Max(1, NPC.lifeMax), 0f, 1f);
			var pixel = TextureAssets.MagicPixel.Value;
			int bx = (int)(top.X - w * 0.5f), by = (int)(top.Y - T.HpBarOffsetY);
			spriteBatch.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), new Color(20, 20, 20, 200));
			spriteBatch.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), Color.Lerp(Color.Red, Color.LimeGreen, frac));
		}

		// =====================================================================================================
		// Net: clients need the mounted weapon's item type to draw the right gun. The aim rides ai[0]; life rides the vanilla NPC sync.
		// =====================================================================================================
		public override void SendExtraAI(System.IO.BinaryWriter writer)
		{
			writer.Write7BitEncodedInt(_weaponType);
			writer.Write7BitEncodedInt((int)Math.Round(_reachPx));
			writer.Write7BitEncodedInt(_shotSeq);
		}

		public override void ReceiveExtraAI(System.IO.BinaryReader reader)
		{
			_weaponType = reader.Read7BitEncodedInt();
			_reachPx = reader.Read7BitEncodedInt();
			int seq = reader.Read7BitEncodedInt();
			if (_seqSeen && seq != _shotSeq) { _recoil = T.RecoilPx; PlayFireSound(); } // the server fired since the last sync: kick and thump (the first read after joining stays quiet)
			_shotSeq = seq; _seqSeen = true;
		}
	}
}
