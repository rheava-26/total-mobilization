using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>One drone's shot, worked out once when the weapon is mounted so firing it costs almost nothing. See <see cref="DroneArsenal.BuildShot"/>.</summary>
	public sealed class DroneShot
	{
		/// <summary>The projectile fired per round (the weapon's own, or its ammo's), and its launch speed (px/tick) and knockback.</summary>
		public int Projectile; public float Speed, Knockback;
		/// <summary>Weapon damage plus the ammo's, before the world-progress scale.</summary>
		public int BaseDamage;
		/// <summary>Average ticks between shots (the weapon's own cadence), and how many projectiles one shot spreads over a fan of <see cref="SpreadDeg"/> degrees.</summary>
		public int IntervalTicks, Count = 1; public float SpreadDeg;
		/// <summary>The shot falls (an arrow): the aim leads the drop. Homing ammo flies straight at the target and needs no lead.</summary>
		public bool Gravity, Homing;
		/// <summary>1 + the projectile's extra updates: it advances this many times a tick, which the lead has to count.</summary>
		public float StepMult = 1f;
		public DamageClass Class = DamageClass.Default;
		public int Crit;
		/// <summary>How far (px) the drone engages with this weapon.</summary>
		public float RangePx;
		/// <summary>The shot is a stand-in for a held weapon whose real shot is owner-anchored (see <see cref="LevyConstants.Arsenal.HeldStandInProjectile"/>): approximate.</summary>
		public bool StandIn;

		public bool Valid => Projectile > ProjectileID.None && IntervalTicks > 0;
	}

	/// <summary>
	/// What a combat drone may mount and how it fires it: the LIGHT approach. A drone does not run the soldier weapon pipeline (that is welded to <see cref="Content.NPCs.SoldierNPC"/>); it
	/// spawns the item's own projectile the way Player.ItemCheck_Shoot's default path does. Pure functions over items and the cached <see cref="WeaponClassifier"/> profiles: no world state.
	///
	/// FAITHFUL: every gun, bow and launcher (the round's own projectile, resolved as Player.PickAmmo does: specific launcher matches, rocket offsets, the per-weapon swaps), every caster whose
	/// shot is a plain projectile (staves, wands, tomes, magic guns), multi-projectile weapons through the same Multishot table soldiers use, and the weapon's own cadence.
	/// APPROXIMATE: a held / channeled weapon never spawns its owner-anchored shot (it would grab the commander); one that fires ammo fires that ammo's shot at its real cadence, a held caster
	/// fires <see cref="LevyConstants.Arsenal.HeldStandInProjectile"/>. Sky-drop weapons (Daedalus Stormbow, Blizzard Staff...) shoot straight from the drone instead of raining from above.
	/// A modded weapon's own Shoot hook is not run. REFUSED: melee, summon, whip, sentry, thrown, boomerang, yoyo, flail, tools, and <see cref="LevyConstants.Arsenal.UnmountableItems"/>.
	/// </summary>
	public static class DroneArsenal
	{
		private static readonly Dictionary<int, bool> _mountable = new();
		private static readonly Dictionary<int, int> _defaultAmmo = new();

		// ---------------------------------------------------------------------------------------------------- eligibility

		/// <summary>
		/// True when this item can be mounted on a drone: a single ranged or magic weapon that fires a projectile, of a family the light shot can reproduce, that is not on the refusal list, and
		/// (when it takes ammo) whose kind of ammo has some to fire. The verdict depends on the item TYPE (a prefix never changes it) and is cached.
		/// </summary>
		public static bool IsMountable(Item it)
		{
			if (it == null || it.IsAir || it.stack != 1) return false;
			if (_mountable.TryGetValue(it.type, out bool cached)) return cached;
			bool verdict = false;
			try { verdict = ComputeMountable(it); }
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: drone arsenal could not classify item " + it.type + ": " + e.Message); }
			_mountable[it.type] = verdict;
			return verdict;
		}

		private static bool ComputeMountable(Item it)
		{
			Item sample = ContentSamples.ItemsByType.TryGetValue(it.type, out Item s) && s != null && !s.IsAir ? s : it;
			if (sample.damage <= 0 || sample.shoot <= ProjectileID.None) return false;
			if (sample.maxStack > 1 || sample.consumable || sample.accessory || sample.sentry || sample.pick > 0 || sample.axe > 0 || sample.hammer > 0) return false;
			if (LevyConstants.Arsenal.UnmountableItems.Contains(sample.type) || LevyConstants.DeployPersistItems.Contains(sample.type)) return false;

			WeaponProfile profile = WeaponClassifier.GetProfile(it);
			if (!profile.Supported) return false;
			switch (profile.Family)
			{
				case WeaponFamily.Gun:
				case WeaponFamily.Bow:
				case WeaponFamily.Magic:
					break;
				case WeaponFamily.HeldWeapon:
					if (profile.HeldMelee) return false; // Starlight, Solar Eruption: melee stays with soldiers
					break;
				default:
					return false; // blades, spears, whips, summon staves, sentries, thrown, boomerangs, yoyos, flails
			}
			DamageClass cls = WeaponClassifier.ItemDamageClass(it);
			if (cls != DamageClass.Ranged && cls != DamageClass.Magic) return false;
			if (sample.useAmmo > 0 && DefaultAmmoType(sample.useAmmo) <= 0) return false; // takes ammo of a kind nothing supplies
			// A caster whose own shot grows a LINE anchored to its spawn point (Vilethorn / Nettle Burst / Crystal Vile Shard: aiStyle Vilethorn) needs the weapon's own Shoot to aim it.
			// A turret / drone fires the raw projectile with no such setup, so the thorn line grows in a fixed direction straight through terrain and never stops. Not mountable.
			if (sample.useAmmo == AmmoID.None && profile.Family != WeaponFamily.HeldWeapon && sample.shoot > ProjectileID.None)
			{
				Projectile shotSample = WeaponClassifier.ProjectileSample(sample.shoot);
				if (shotSample != null && !shotSample.tileCollide && shotSample.aiStyle == ProjAIStyleID.Vilethorn) return false;
			}
			return true;
		}

		/// <summary>True when firing this weapon takes a round (guns, bows, launchers); false for casters.</summary>
		public static bool TakesAmmo(Item weapon) => weapon != null && !weapon.IsAir && weapon.useAmmo > 0;

		// ---------------------------------------------------------------------------------------------------- ammo

		/// <summary>
		/// The round a drone fires when its Armory has none to spare: the Musket Ball for bullets, the Wooden Arrow for arrows, and for any other kind (rockets, darts, gel, stakes, modded ammo) the
		/// lowest-numbered item that is that ammo. 0 when no item is. Cached.
		/// </summary>
		public static int DefaultAmmoType(int useAmmo)
		{
			if (useAmmo <= 0) return 0;
			if (_defaultAmmo.TryGetValue(useAmmo, out int cached)) return cached;
			int type = 0;
			if (useAmmo == AmmoID.Bullet) type = ItemID.MusketBall;
			else if (useAmmo == AmmoID.Arrow) type = ItemID.WoodenArrow;
			else
			{
				foreach (KeyValuePair<int, Item> kv in ContentSamples.ItemsByType)
				{
					Item s = kv.Value;
					if (s == null || s.IsAir || s.ammo != useAmmo || s.IsACoin) continue;
					if (type == 0 || s.type < type) type = s.type;
				}
			}
			_defaultAmmo[useAmmo] = type;
			return type;
		}

		/// <summary>The sample item of the default round for an ammo kind, or null.</summary>
		public static Item DefaultAmmoSample(int useAmmo)
		{
			int type = DefaultAmmoType(useAmmo);
			return type > 0 && ContentSamples.ItemsByType.TryGetValue(type, out Item s) && s != null && !s.IsAir ? s : null;
		}

		// ---------------------------------------------------------------------------------------------------- toughness

		/// <summary>The rarity tier (see <see cref="WeaponClassifier.RarityTier"/>) of an item TYPE, read from its sample so a client and the server agree whatever the prefix.</summary>
		public static int RarityTierOf(int itemType) =>
			WeaponClassifier.RarityTier(ContentSamples.ItemsByType.TryGetValue(itemType, out Item s) && s != null ? s.rare : 0);

		/// <summary>Max life of a drone carrying this weapon type: the weapon sets the drone's toughness as it sets its damage.</summary>
		public static int MaxHpFor(int itemType) =>
			Math.Min(LevyConstants.Arsenal.HpMax, LevyConstants.Arsenal.HpBase + LevyConstants.Arsenal.HpPerRarityTier * RarityTierOf(itemType));

		public static int DefenseFor(int itemType) => LevyConstants.Arsenal.DefenseBase + LevyConstants.Arsenal.DefensePerRarityTier * RarityTierOf(itemType);

		// ---------------------------------------------------------------------------------------------------- the shot

		/// <summary>
		/// Works out one drone's shot from its mounted <paramref name="weapon"/> and the <paramref name="ammo"/> round it borrowed (null: the default round of the weapon's kind). Returns an invalid
		/// shot (see <see cref="DroneShot.Valid"/>) when the weapon cannot be fired, so the caller never mounts what it cannot use. Never throws.
		/// </summary>
		public static DroneShot BuildShot(Item weapon, Item ammo)
		{
			var shot = new DroneShot();
			try
			{
				if (weapon == null || weapon.IsAir || weapon.shoot <= ProjectileID.None) return shot;
				WeaponProfile profile = WeaponClassifier.GetProfile(weapon);
				shot.Class = weapon.DamageType ?? profile.DamageType;
				shot.Crit = Math.Max(0, weapon.crit) + LevyConstants.BaseCritChance;
				shot.Count = Math.Max(1, profile.MultishotCount);
				shot.SpreadDeg = profile.SpreadDeg;

				float speed = weapon.shootSpeed, knockback = weapon.knockBack;
				int damage = weapon.damage, projectile;
				if (weapon.useAmmo > 0)
				{
					Item round = ammo != null && !ammo.IsAir ? ammo : DefaultAmmoSample(weapon.useAmmo);
					if (round == null) return shot;
					projectile = ResolveAmmoProjectile(weapon, round);
					speed += round.shootSpeed; knockback += round.knockBack; damage += Math.Max(0, round.damage);
					shot.Homing = LevyConstants.HomingAmmo.Contains(round.type);
				}
				else if (profile.Family == WeaponFamily.HeldWeapon)
				{
					projectile = LevyConstants.Arsenal.HeldStandInProjectile; // the held AI is owner-anchored: a bolt stands in for it
					speed = LevyConstants.Arsenal.HeldStandInSpeed;
					shot.StandIn = true;
				}
				else
				{
					projectile = weapon.shoot;
					if (speed <= 0f) speed = LevyConstants.SwordBeamSpeedFallback; // a caster that lists no shoot speed
				}
				if (projectile <= ProjectileID.None) return shot;
				if (profile.Family == WeaponFamily.HeldWeapon && weapon.useAmmo > 0) shot.StandIn = true; // the held AI's own volley, flown as plain ammo shots

				shot.Projectile = projectile;
				shot.Speed = Math.Max(1f, speed);
				shot.Knockback = Math.Max(0f, knockback);
				shot.BaseDamage = Math.Max(1, damage);
				Projectile sample = WeaponClassifier.ProjectileSample(projectile);
				shot.StepMult = 1f + Math.Max(0, sample?.extraUpdates ?? 0);
				shot.Gravity = WeaponClassifier.ProjectileHasGravity(projectile);
				shot.IntervalTicks = IntervalTicks(weapon, profile);
				shot.RangePx = Math.Clamp(profile.RangeTiles, LevyConstants.Arsenal.RangeMinTiles, LevyConstants.Arsenal.RangeMaxTiles) * LevyConstants.TilePx;
			}
			catch (Exception e)
			{
				ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: building a drone shot failed: " + e.Message);
				shot.Projectile = ProjectileID.None; // invalid: the weapon goes back to the Armory
			}
			return shot;
		}

		/// <summary>
		/// Average ticks between shots, from the weapon's own use time the way <see cref="WeaponClassifier.Score"/> counts it: a burst weapon fires its run of shots then rests out the animation and
		/// reuse delay, so its average is that period over the shots. A held weapon with a known real cadence uses it. Held within the drone's bounds.
		/// </summary>
		public static int IntervalTicks(Item weapon, WeaponProfile profile)
		{
			float cadence = Math.Max(1, weapon.useTime);
			if (profile != null && profile.Family == WeaponFamily.HeldWeapon && LevyConstants.HeldRealCadence.TryGetValue(weapon.type, out int real) && real > 0) cadence = real;
			else
			{
				int anim = Math.Max(1, weapon.useAnimation);
				int natural = Math.Max(1, (anim + (int)cadence - 1) / (int)cadence);
				int shots = weapon.useLimitPerAnimation.HasValue && weapon.useLimitPerAnimation.Value > 0 ? Math.Min(weapon.useLimitPerAnimation.Value, natural) : natural;
				if (shots < natural || weapon.reuseDelay > 0) cadence = (anim + Math.Max(0, weapon.reuseDelay)) / (float)shots;
			}
			return Math.Clamp((int)Math.Round(cadence), LevyConstants.Arsenal.MinShotIntervalTicks, LevyConstants.Arsenal.MaxShotIntervalTicks);
		}

		/// <summary>
		/// The projectile a weapon fires with a round of <paramref name="ammo"/>, as Player.PickAmmo decides it: a specific launcher/ammo pairing first, then rocket and solution ammo add their
		/// offset to the launcher's base projectile, any other ammo replaces it; then the per-weapon swaps vanilla makes (Hellwing bats, Shadowflame / Frost / Pulse arrows, high-velocity bullets...).
		/// Kept here, beside the drone, rather than shared with the soldier pipeline.
		/// </summary>
		public static int ResolveAmmoProjectile(Item weapon, Item ammo)
		{
			int projectile = weapon.shoot, match;
			bool matched = false;
			if (AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.TryGetValue(weapon.type, out var table) && table.TryGetValue(ammo.type, out match)) { projectile = match; matched = true; }
			else
			{
				int[] fallbacks = AmmoID.Sets.SpecificLauncherAmmoProjectileFallback;
				int fallback = fallbacks != null && weapon.type >= 0 && weapon.type < fallbacks.Length ? fallbacks[weapon.type] : -1;
				if (fallback != -1 && AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.TryGetValue(fallback, out var table2) && table2.TryGetValue(ammo.type, out match)) { projectile = match; matched = true; }
			}
			if (!matched)
			{
				if (weapon.useAmmo == AmmoID.Rocket || weapon.useAmmo == AmmoID.Solution) projectile = weapon.shoot + Math.Max(0, ammo.shoot);
				else if (ammo.shoot > ProjectileID.None) projectile = ammo.shoot;
			}
			if (projectile == ProjectileID.SandBallGun) // the Sandgun: each sand has its own bespoke shot (never the block that would place tiles)
			{
				var sand = ammo.type >= 0 && ammo.type < ItemID.Sets.SandgunAmmoProjectileData.Length ? ItemID.Sets.SandgunAmmoProjectileData[ammo.type] : null;
				if (sand != null) projectile = sand.ProjectileType;
			}
			switch (weapon.type)
			{
				case ItemID.HellwingBow: if (projectile == ProjectileID.WoodenArrowFriendly) projectile = ProjectileID.Hellwing; break;
				case ItemID.MoltenFury: if (projectile == ProjectileID.WoodenArrowFriendly) projectile = ProjectileID.FireArrow; break;
				case ItemID.SniperRifle:
				case ItemID.VenusMagnum:
				case ItemID.Uzi: if (projectile == ProjectileID.Bullet) projectile = ProjectileID.BulletHighVelocity; break;
				case ItemID.ShadowFlameBow: projectile = ProjectileID.ShadowFlameArrow; break;
				case ItemID.FairyQueenRangedItem: if (projectile == ProjectileID.WoodenArrowFriendly) projectile = ProjectileID.FairyQueenRangedItemShot; break;
				case ItemID.BloodRainBow: projectile = ProjectileID.BloodArrow; break;
				case ItemID.SkeletonBow: if (projectile == ProjectileID.BoneArrowFromMerchant) projectile = ProjectileID.BoneArrow; break;
				case ItemID.BeesKnees: if (projectile == ProjectileID.WoodenArrowFriendly) projectile = ProjectileID.BeeArrow; break;
				case ItemID.Marrow: projectile = ProjectileID.BoneArrow; break;
				case ItemID.IceBow: projectile = ProjectileID.FrostArrow; break;
				case ItemID.PulseBow: projectile = ProjectileID.PulseBolt; break;
			}
			return projectile;
		}
	}
}
