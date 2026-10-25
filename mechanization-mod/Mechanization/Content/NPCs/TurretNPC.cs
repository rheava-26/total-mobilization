using System;
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
using Levy.Common.Globals;              // SoldierProjectileTag  (commander kill-credit, exactly as the drones tag their shots)
using Levy.Common.Targeting;            // ITargetBody / EnemyTargeting  (hostiles path to the turret exactly as they do to a drone)
using Levy.Common.Util;                 // LevyUtil.IsHostileTarget / IsClient  (cross-mod reference in content)
using Levy.Common.Weapons;              // DroneArsenal / DroneShot / Aiming / WeaponClassifier  (the drone's proven weapon-mount + shot path, reused)
using Levy.Content.NPCs;                // SoldierNPC.DummySlot  (the slot-255 swap the enemy-targeting system uses)
using Levy.Content.TileEntities;        // ArmoryTE  (the Levy Armory the surplus weapon is drawn from)
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
		/// <summary>The weapon pattern the turret currently fires (the drone's prebuilt shot), or null to fire the fixed fallback round.</summary>
		private DroneShot _shot;
		/// <summary>The mounted weapon's firing sound (played client-side when it shoots), or null.</summary>
		private SoundStyle? _weaponUseSound;
		/// <summary>The item type of the mounted weapon (server picks it; synced to clients so they can draw which gun it fires). 0 = none / fixed fallback round.</summary>
		private int _weaponType;
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
				if (_cooldown <= 0) { Fire(t); _cooldown = CurrentFireInterval; }
			}
			else wantAim = NPC.direction < 0 ? MathHelper.Pi : 0f; // idle: barrel level, facing its last way

			_aim = EaseAngle(_aim, wantAim, T.AimTurnLerp);
			NPC.ai[0] = _aim; // rides the NPC sync so clients draw the barrel where the server aims it

			if ((Main.GameUpdateCount + (uint)NPC.whoAmI) % 30u == 0) NPC.netUpdate = true;
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
			if (++_scan >= T.ScanIntervalTicks) { _scan = 0; _target = FindTarget(); }
			else if (_target >= 0 && (_target >= Main.maxNPCs || !Shootable(Main.npc[_target]))) _target = -1;
		}

		/// <summary>Nearest hostile in reach with a clear line. Range cut runs first so the hostile test and line test are only paid for the slots actually in reach.</summary>
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
				if (!LevyUtil.IsHostileTarget(t)) continue;   // Levy's proven hostile filter (cross-mod)
				if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height)) continue;
				bestSq = dSq; best = i;
			}
			return best;
		}

		private bool Shootable(NPC t)
		{
			if (!LevyUtil.IsHostileTarget(t)) return false;
			float range = CurrentRangePx;
			if (Vector2.DistanceSquared(NPC.Center, t.Center) > range * range) return false;
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
		}

		/// <summary>Reach, in px: the mounted weapon's range when one is up (never shorter than the turret's own base reach), else the base reach.</summary>
		private float CurrentRangePx => _shot != null && _shot.Valid ? Math.Max(T.RangePx, _shot.RangePx) : T.RangePx;

		/// <summary>Ticks between shots: the mounted weapon's own cadence when one is up, else the fallback cadence.</summary>
		private int CurrentFireInterval => _shot != null && _shot.Valid ? _shot.IntervalTicks : T.FireIntervalTicks;

		// =====================================================================================================
		// The gun: pull the best surplus ranged weapon from the nearest Levy Armory and fire its real projectile.
		// =====================================================================================================
		/// <summary>Re-picks the mounted weapon from the nearest Armory in reach (a non-consuming read of its racks). Null shot = the fixed fallback round.</summary>
		private void RefreshWeapon()
		{
			ArmoryTE armory = NearestArmory(NPC.Center, T.ArmoryReachPx);
			if (armory == null) { ClearWeapon(); return; }
			Item weapon = BestSurplus(armory);
			if (weapon == null) { ClearWeapon(); return; }
			Item ammo = DroneArsenal.TakesAmmo(weapon) ? BestAmmoFor(armory, weapon.useAmmo) : null;
			DroneShot shot = DroneArsenal.BuildShot(weapon, ammo); // the drone's own shot builder: projectile, speed, damage, cadence, spread, gravity, homing
			if (shot == null || !shot.Valid) { ClearWeapon(); return; }
			_shot = shot;
			_weaponUseSound = weapon.UseSound;
			SetWeaponType(weapon.type); // clients draw this item's sprite over the cannon
		}

		private void ClearWeapon() { _shot = null; _weaponUseSound = null; SetWeaponType(0); }

		/// <summary>Records the mounted weapon's item type and flags a sync when it changes, so clients redraw the right gun.</summary>
		private void SetWeaponType(int type)
		{
			if (_weaponType == type) return;
			_weaponType = type;
			NPC.netUpdate = true;
		}

		/// <summary>The nearest Levy Armory tile entity within <paramref name="reachPx"/>, or null.</summary>
		private static ArmoryTE NearestArmory(Vector2 from, float reachPx)
		{
			float reachSq = reachPx * reachPx;
			ArmoryTE best = null; float bestSq = float.MaxValue;
			foreach (TileEntity te in TileEntity.ByID.Values)
			{
				if (te is not ArmoryTE a) continue;
				float d = Vector2.DistanceSquared(from, a.Center);
				if (d < bestSq && d <= reachSq) { bestSq = d; best = a; }
			}
			return best;
		}

		/// <summary>
		/// The strongest surplus weapon on an Armory's racks that a turret can fire: issue rows and bulk storage, scored by
		/// <see cref="WeaponClassifier.Score"/>, filtered to RANGED or MAGIC weapons the drone arsenal accepts as mountable. A non-consuming
		/// read — the weapon stays the army's; the turret only mirrors its projectile. Melee, summon, whip and sentry are excluded by
		/// <see cref="DroneArsenal.IsMountable"/>. A caster just fires its projectile — a turret keeps no mana economy (generous by design).
		/// </summary>
		private static Item BestSurplus(ArmoryTE armory)
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
			foreach (Item it in armory.IssueWeapons) Consider(it);
			foreach (Item it in armory.Storage) Consider(it);
			return best;
		}

		/// <summary>The best stocked round of a given ammo kind (highest damage, then rarity, as the drone's ammo loan ranks it), or null to let the shot builder use the default round. Non-consuming.</summary>
		private static Item BestAmmoFor(ArmoryTE armory, int ammoId)
		{
			if (ammoId <= 0) return null;
			Item best = null; float bestScore = float.NegativeInfinity;
			void Consider(Item it)
			{
				if (it == null || it.IsAir || it.ammo != ammoId || it.IsACoin) return;
				float s = it.damage * 100f + WeaponClassifier.RarityTier(it.rare);
				if (s > bestScore) { bestScore = s; best = it; }
			}
			foreach (Item it in armory.IssueAmmo) Consider(it);
			foreach (Item it in armory.Storage) Consider(it);
			return best;
		}

		/// <summary>One cannonade: the mounted weapon's real projectile (one to several as it fires) aimed with lead/arc at the target, else the fixed fallback round. Owned by the real player and credited to the nearest commander.</summary>
		private void Fire(NPC target)
		{
			int commander = PickCommander(NPC.Center);
			if (_shot != null && _shot.Valid) FireSurplus(target, commander);
			else FireFixed(target, commander);
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

			int dmg = (int)Math.Round(shot.BaseDamage * T.SurplusDamageMult);
			if (Main.expertMode) dmg = (int)(dmg * Main.GameModeInfo.TownNPCDamageMultiplier);
			dmg = Math.Max(1, dmg);

			int owner = Main.myPlayer; // 255 on a dedicated server: a shot owned by any other index is an unsynced ghost
			for (int k = 0; k < shot.Count; k++)
			{
				float deg = shot.Count > 1 ? -shot.SpreadDeg * 0.5f + shot.SpreadDeg * k / (shot.Count - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, v.X, v.Y, shot.Projectile, dmg, shot.Knockback, owner);
				if (idx < 0 || idx >= Main.maxProjectiles) continue;
				TagTurretShot(Main.projectile[idx], commander, shot.Class, shot.Crit);
			}
			if (Main.netMode != NetmodeID.Server && _weaponUseSound.HasValue) SoundEngine.PlaySound(_weaponUseSound.Value, NPC.Center);
		}

		/// <summary>Fires the fixed fallback round straight at the target (no Armory in reach, or nothing mountable in it), so a turret is never inert.</summary>
		private void FireFixed(NPC target, int commander)
		{
			Vector2 from = NPC.Center;
			Vector2 dir = (target.Center - from).SafeNormalize(Vector2.UnitX * (NPC.direction == 0 ? 1 : NPC.direction));
			Vector2 vel = dir * T.ShotSpeed;
			Vector2 muzzle = from + dir * T.MuzzleOffsetPx;
			if (Collision.SolidCollision(muzzle - new Vector2(2f, 2f), 4, 4)) muzzle = from;

			int owner = Main.myPlayer;
			int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, vel.X, vel.Y, T.ProjectileType, T.ShotDamage, T.ShotKnockback, owner);
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
		public override bool CanChat() => false;
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
				int item = T.BodyDrawItemType;
				Main.instance.LoadItem(item);
				Texture2D tex = TextureAssets.Item[item]?.Value;
				if (tex == null || tex.Width <= 0 || tex.Height <= 0) return false;
				float scale = Math.Min(1f, T.BodyDrawMaxPx / Math.Max(tex.Width, tex.Height));
				Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
				Vector2 pos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);
				Color c = Lighting.GetColor((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f), drawColor);
				// Flip the sprite upright when the barrel points left, so the cannon never draws upside-down.
				SpriteEffects fx = Math.Cos(_aim) < 0.0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
				spriteBatch.Draw(tex, pos, null, c, _aim, origin, scale, fx, 0f);
				DrawMountedWeapon(spriteBatch, pos, c); // the actual gun (Megashark, gem staff...) over the cannon, so you can see what it fires
				return false;
			}
			catch (Exception e)
			{
				Mechanization.Instance.Logger.Warn("Mechanization: drawing a turret failed: " + e.Message);
				return false;
			}
		}

		/// <summary>The mounted weapon's item sprite, pushed out along the aim and turned to it (kept upright when aiming left). Nothing when no weapon is mounted — the cannon shows alone.</summary>
		private void DrawMountedWeapon(SpriteBatch spriteBatch, Vector2 bodyPos, Color light)
		{
			if (_weaponType <= 0 || _weaponType >= ItemLoader.ItemCount) return;
			Main.instance.LoadItem(_weaponType);
			Texture2D wt = TextureAssets.Item[_weaponType]?.Value;
			if (wt == null || wt.Width <= 0 || wt.Height <= 0) return;
			float scale = Math.Min(1f, T.WeaponDrawMaxPx / Math.Max(wt.Width, wt.Height));
			Vector2 muzzle = bodyPos + new Vector2((float)Math.Cos(_aim), (float)Math.Sin(_aim)) * T.WeaponDrawOffsetPx;
			SpriteEffects fx = Math.Cos(_aim) < 0.0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
			spriteBatch.Draw(wt, muzzle, null, light, _aim, new Vector2(wt.Width, wt.Height) * 0.5f, scale, fx, 0f);
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
		public override void SendExtraAI(System.IO.BinaryWriter writer) => writer.Write7BitEncodedInt(_weaponType);

		public override void ReceiveExtraAI(System.IO.BinaryReader reader) => _weaponType = reader.Read7BitEncodedInt();
	}
}
