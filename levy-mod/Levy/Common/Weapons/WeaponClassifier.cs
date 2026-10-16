using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>Cached numbers a soldier borrows from an item. Built once per item type by <see cref="WeaponClassifier"/>.</summary>
	public class WeaponProfile
	{
		public int ItemType;
		public WeaponFamily Family = WeaponFamily.Unsupported;
		public int UseTime = 30, UseAnimation = 30, ReuseDelay;
		public int Shoot; public float ShootSpeed;
		public int Damage; public float Knockback; public int Crit;
		public int UseAmmo; public int Mana;
		public int RangeTiles = LevyConstants.RangeMelee;
		public bool Gravity; public bool Homing;
		public int MultishotCount = 1; public float SpreadDeg;
		public DamageClass DamageType = DamageClass.Default;
		/// <summary>True when the weapon is only accepted by the widened rules (non-bullet/arrow ammo, non-magic casters, plain shooters).</summary>
		public bool Extended;
		/// <summary>Spears/shortswords: how far the thrust reaches, in pixels.</summary>
		public float ThrustReachPx;
		/// <summary>Boomerangs count as ranged for the brain (hold a standoff, need a line of sight); they use no ammo.</summary>
		public bool IsRanged => Family == WeaponFamily.Gun || Family == WeaponFamily.Bow || Family == WeaponFamily.Magic || Family == WeaponFamily.Boomerang;
		/// <summary>Whips count as melee for the brain (close to the lash's reach, avoid bosses); they stop short at a fraction of their reach.</summary>
		public bool IsMelee => Family == WeaponFamily.Broadsword || Family == WeaponFamily.Shortsword || Family == WeaponFamily.Spear || Family == WeaponFamily.Whip;
		public bool IsSummon => Family == WeaponFamily.Summon;
		public bool IsWhip => Family == WeaponFamily.Whip;
		public bool IsBoomerang => Family == WeaponFamily.Boomerang;
		public bool IsThrust => Family == WeaponFamily.Shortsword || Family == WeaponFamily.Spear;
		public bool UsesAmmo => Family == WeaponFamily.Gun || Family == WeaponFamily.Bow;
		public bool Supported => Family != WeaponFamily.Unsupported && Family != WeaponFamily.None;
		/// <summary>Broadswords whose swing also launches the item's projectile (sword beams).</summary>
		public bool SwordBeam => Family == WeaponFamily.Broadsword && Shoot > 0;
		public int ProjectilesPerShot => Math.Max(1, MultishotCount);
	}

	/// <summary>Sorts every item into a family from its fields at load. OWNED BY THE WEAPONS AGENT.</summary>
	public static class WeaponClassifier
	{
		private static readonly Dictionary<int, WeaponProfile> _profiles = new();

		/// <summary>Accessories that do nothing on a soldier: information, building, fishing, coin and vanity trinkets.</summary>
		private static readonly HashSet<int> _uselessAccessories = new()
		{
			// information
			ItemID.GPS, ItemID.Compass, ItemID.DepthMeter, ItemID.Radar, ItemID.LifeformAnalyzer, ItemID.TallyCounter, ItemID.Stopwatch, ItemID.DPSMeter,
			ItemID.MetalDetector, ItemID.FishermansGuide, ItemID.WeatherRadio, ItemID.Sextant, ItemID.GoblinTech, ItemID.REK, ItemID.PDA, ItemID.CellPhone,
			ItemID.MechanicalLens, ItemID.Ruler, ItemID.LaserRuler,
			// building
			ItemID.Toolbelt, ItemID.Toolbox, ItemID.ExtendoGrip, ItemID.PaintSprayer, ItemID.BrickLayer, ItemID.ArchitectGizmoPack, ItemID.AncientChisel,
			ItemID.HandOfCreation, ItemID.ActuationAccessory, ItemID.WireKite,
			// fishing
			ItemID.AnglerEarring, ItemID.TackleBox, ItemID.LavaproofTackleBag, ItemID.AnglerTackleBag, ItemID.HighTestFishingLine, ItemID.FishingBobber,
			ItemID.FishingBobberGlowingStar, ItemID.FishingBobberGlowingArgon, ItemID.FishingBobberGlowingKrypton, ItemID.FishingBobberGlowingLava,
			ItemID.FishingBobberGlowingViolet, ItemID.FishingBobberGlowingXenon, ItemID.FishingBobberGlowingRainbow,
			// coins / shops
			ItemID.CoinRing, ItemID.LuckyCoin, ItemID.GoldRing, ItemID.DiscountCard, ItemID.GreedyRing, ItemID.TreasureMagnet,
			// harmful or pointless
			ItemID.EncumberingStone, ItemID.GuideVoodooDoll, ItemID.ClothierVoodooDoll,
		};

		public static WeaponFamily Classify(Item item) => ClassifyCore(item, out _);

		/// <summary>
		/// Accepts the long tail on purpose: every ammo type (bullets and arrows keep their families, everything else fires like a gun), any mana
		/// weapon that shoots, melee swings/thrusts, and finally any plain shooter, all fired through the real shoot pipeline. Rejects what a
		/// stationary-ish soldier cannot use: tools, consumables, channeled/held weapons, yoyos, flails, boomerangs, whips, minions and sentries.
		/// <paramref name="extended"/> is set when only the widened rules accepted it.
		/// </summary>
		private static WeaponFamily ClassifyCore(Item item, out bool extended)
		{
			extended = false;
			if (item == null || item.IsAir) return WeaponFamily.None;
			if (item.damage <= 0) return WeaponFamily.Unsupported;
			if (item.pick > 0 || item.axe > 0 || item.hammer > 0) return WeaponFamily.Unsupported;
			if (item.consumable) return WeaponFamily.Unsupported;
			if (item.channel) return WeaponFamily.Unsupported;
			if (item.sentry) return WeaponFamily.Unsupported;
			if (IsSet(ItemID.Sets.Yoyo, item.type)) return WeaponFamily.Unsupported;
			if (item.useStyle == ItemUseStyleID.None) return WeaponFamily.Unsupported;

			int shoot = item.shoot;
			Projectile sample = ProjectileSample(shoot);

			// Soldier-anchored families (SoldierMinion / SoldierWhip / SoldierBoomerang): whips first, since whips are Summon-class items.
			if (sample != null && !sample.sentry)
			{
				if (IsSet(ProjectileID.Sets.IsAWhip, shoot) || sample.aiStyle == ProjAIStyleID.Whip) return WeaponFamily.Whip;
				if (item.CountsAsClass(DamageClass.Summon) && (sample.minion || sample.minionSlots > 0f)) return WeaponFamily.Summon;
				if (sample.aiStyle == ProjAIStyleID.Boomerang && item.useAmmo == AmmoID.None) return WeaponFamily.Boomerang;
			}
			if (item.type == ItemID.Harpoon) return WeaponFamily.Unsupported; // tethered to its owner (see the projectile check below)
			// Night's Edge family: a no-melee swing whose "projectile" is a slash drawn around the owner's body. A soldier swings the blade instead (the hitbox is the damage).
			if (sample != null && item.useStyle == ItemUseStyleID.Swing && item.CountsAsClass(DamageClass.Melee) && IsHeldSlashProjectile(shoot)) return WeaponFamily.Broadsword;
			if (item.CountsAsClass(DamageClass.Summon)) return WeaponFamily.Unsupported; // sentries, summon tools and anything without a minion shot
			if (sample != null)
			{
				int ai = sample.aiStyle;
				if (ai == ProjAIStyleID.Yoyo || ai == ProjAIStyleID.Flail || ai == ProjAIStyleID.Boomerang || ai == ProjAIStyleID.Whip) return WeaponFamily.Unsupported;
				// Tethered lines (Harpoon, Golem Fist, Boxing Glove, Chain Knife) retract to Main.player[owner] and allow one out per owner, and grappling hooks
				// anchor to it: soldier shots are owned by the commander, so they would tether to the commander and take over the commander's own slot.
				if (ai == ProjAIStyleID.Harpoon || ai == ProjAIStyleID.Hook) return WeaponFamily.Unsupported;
				if (IsSet(ProjectileID.Sets.IsAWhip, shoot)) return WeaponFamily.Unsupported;
				if (shoot < ProjectileID.Sets.YoyosLifeTimeMultiplier.Length && ProjectileID.Sets.YoyosLifeTimeMultiplier[shoot] != -1f) return WeaponFamily.Unsupported;
				if (sample.minion || sample.sentry || sample.minionSlots > 0f) return WeaponFamily.Unsupported;
			}

			if (item.useAmmo != AmmoID.None)
			{
				if (item.useAmmo == AmmoID.Bullet) return WeaponFamily.Gun;
				if (item.useAmmo == AmmoID.Arrow) return WeaponFamily.Bow;
				if (item.useAmmo == AmmoID.Coin) return WeaponFamily.Unsupported; // would burn the player's money
				extended = true;
				return WeaponFamily.Gun; // rockets, darts, gel, stakes, snowballs, flares, sand, solutions, modded ammo: all fire through the gun path
			}

			if (item.mana > 0 && shoot > 0)
			{
				extended = !item.CountsAsClass(DamageClass.Magic);
				return WeaponFamily.Magic;
			}

			bool meleeish = item.CountsAsClass(DamageClass.Melee) || !(item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Magic));
			if (meleeish)
			{
				bool viaMeleeClass = item.CountsAsClass(DamageClass.Melee);
				WeaponFamily melee = WeaponFamily.Unsupported;
				if (sample != null && sample.aiStyle == ProjAIStyleID.Spear) melee = WeaponFamily.Spear;
				else if (IsSet(ItemID.Sets.Spears, item.type)) melee = WeaponFamily.Spear;
				else if (item.useStyle == ItemUseStyleID.Rapier) melee = WeaponFamily.Shortsword;
				else if (sample != null && sample.aiStyle == ProjAIStyleID.ShortSword) melee = WeaponFamily.Shortsword;
				else if (item.useStyle == ItemUseStyleID.Swing && !item.noMelee) melee = WeaponFamily.Broadsword;
				if (melee != WeaponFamily.Unsupported) { extended = !viaMeleeClass; return melee; }
			}

			// Plain shooter: a projectile weapon with no ammo and no mana (cannons, wands, modded launchers). The shoot pipeline decides what happens.
			if (shoot > 0 && shoot != ProjectileID.PurificationPowder && sample != null)
			{
				extended = true;
				return WeaponFamily.Magic; // fired by the caster path at zero mana cost
			}
			return WeaponFamily.Unsupported;
		}

		/// <summary>Per-type profile (prefix-free numbers from ContentSamples). Live prefix values are read from the item at fire time.</summary>
		public static WeaponProfile GetProfile(Item item)
		{
			if (item == null || item.IsAir) return new WeaponProfile { ItemType = 0, Family = WeaponFamily.None };
			if (_profiles.TryGetValue(item.type, out var cached)) return cached;
			Item src = ContentSamples.ItemsByType.TryGetValue(item.type, out var sample) && sample != null && !sample.IsAir ? sample : item;
			var p = Build(src);
			_profiles[item.type] = p;
			return p;
		}

		private static WeaponProfile Build(Item src)
		{
			var family = ClassifyCore(src, out bool extended);
			var p = new WeaponProfile
			{
				ItemType = src.type,
				Family = family,
				Extended = extended,
				UseTime = Math.Max(1, src.useTime),
				UseAnimation = Math.Max(1, src.useAnimation),
				ReuseDelay = Math.Max(0, src.reuseDelay),
				Shoot = src.shoot,
				ShootSpeed = src.shootSpeed,
				Damage = src.damage,
				Knockback = src.knockBack,
				Crit = src.crit,
				UseAmmo = src.useAmmo,
				Mana = src.mana,
				DamageType = src.DamageType ?? DamageClass.Default,
			};
			if (LevyConstants.Multishot.TryGetValue(src.type, out var ms)) { p.MultishotCount = Math.Max(1, ms.count); p.SpreadDeg = ms.spreadDeg; }
			switch (p.Family)
			{
				case WeaponFamily.Gun: p.RangeTiles = p.Extended ? ShortShotRange(src, LevyConstants.RangeGun) : LevyConstants.RangeGun; p.Gravity = false; break;
				case WeaponFamily.Bow: p.RangeTiles = LevyConstants.RangeBow; p.Gravity = true; break;
				case WeaponFamily.Magic: p.RangeTiles = p.Extended ? ShortShotRange(src, LevyConstants.RangeMagic) : LevyConstants.RangeMagic; p.Gravity = ProjectileHasGravity(src.shoot); break;
				case WeaponFamily.Summon: p.RangeTiles = LevyConstants.RangeSummon; break;
				case WeaponFamily.Boomerang: p.RangeTiles = LevyConstants.RangeBoomerang; break;
				case WeaponFamily.Whip:
					p.ThrustReachPx = WhipReach(src.shootSpeed);
					p.RangeTiles = Math.Clamp((int)Math.Round(p.ThrustReachPx / LevyConstants.TilePx), LevyConstants.WhipRangeMinTiles, LevyConstants.WhipRangeMaxTiles);
					break;
				case WeaponFamily.Spear:
				case WeaponFamily.Shortsword:
					p.ThrustReachPx = ThrustReach(src.shootSpeed);
					p.RangeTiles = Math.Clamp((int)Math.Round(p.ThrustReachPx / LevyConstants.TilePx), LevyConstants.ThrustRangeMinTiles, LevyConstants.ThrustRangeMaxTiles);
					break;
				default: p.RangeTiles = LevyConstants.RangeMelee; break;
			}
			return p;
		}

		/// <summary>Engagement range for an extended ranged weapon: the family range, cut down to the real reach when its projectile has a short lifetime (flames).</summary>
		private static int ShortShotRange(Item src, int familyRange)
		{
			var sample = ProjectileSample(src.shoot);
			if (sample == null || sample.timeLeft > LevyConstants.LongShotLifetimeTicks) return familyRange;
			float speed = Math.Max(1f, src.shootSpeed) * (1 + Math.Max(0, sample.extraUpdates));
			float tiles = speed * Math.Max(1, sample.timeLeft) / LevyConstants.TilePx * LevyConstants.ShortShotReachFraction;
			return Math.Clamp((int)Math.Round(tiles), LevyConstants.ShortShotRangeMinTiles, familyRange);
		}

		public static float WhipReach(float shootSpeed) => Math.Clamp(shootSpeed * LevyConstants.WhipReachPerShootSpeed, LevyConstants.WhipReachMinPx, LevyConstants.WhipReachMaxPx);

		public static float ThrustReach(float shootSpeed) => Math.Max(LevyConstants.ThrustReachMinPx, shootSpeed * LevyConstants.ThrustReachPerShootSpeed);

		/// <summary>Whether a projectile type falls: vanilla aiStyle 1 adds 0.1/tick after 15 ticks unless the type is on the exempt list (bullets, lasers, Jester's arrows).</summary>
		public static bool ProjectileHasGravity(int projType)
		{
			var sample = ProjectileSample(projType);
			if (sample == null) return false;
			return sample.aiStyle == ProjAIStyleID.Arrow && !LevyConstants.AiStyle1NoGravity.Contains(projType);
		}

		/// <summary>Projectiles that swing around their owner's body as the weapon's visible blade (aiStyle NightsEdge / TrueNightsEdge): Night's Edge, Excalibur, Terra Blade, The Horseman's Blade.</summary>
		public static bool IsHeldSlashProjectile(int projType)
		{
			var sample = ProjectileSample(projType);
			return sample != null && (sample.aiStyle == ProjAIStyleID.NightsEdge || sample.aiStyle == ProjAIStyleID.TrueNightsEdge);
		}

		public static Projectile ProjectileSample(int projType)
		{
			if (projType <= 0) return null;
			return ContentSamples.ProjectilesByType.TryGetValue(projType, out var p) ? p : null;
		}

		private static bool IsSet(bool[] set, int index) => set != null && index >= 0 && index < set.Length && set[index];

		/// <summary>True for weapons the armory may issue (supported family, not a tool/consumable).</summary>
		public static bool IsIssuableWeapon(Item item) => item != null && !item.IsAir && GetProfile(item).Supported;
		public static bool IsArmorPiece(Item item, int part) => item != null && !item.IsAir && (part == 0 ? item.headSlot >= 0 : part == 1 ? item.bodySlot >= 0 : item.legSlot >= 0);
		/// <summary>Accessories that do something on a soldier (plain stats + movement). Info/fishing/building accessories are refused.</summary>
		public static bool IsUsefulAccessory(Item item) => item != null && !item.IsAir && item.accessory && !item.vanity && !_uselessAccessories.Contains(item.type);
		public static bool IsAmmo(Item item) => item != null && !item.IsAir && item.ammo > 0;
		public static bool IsHealingPotion(Item item) => item != null && !item.IsAir && item.healLife > 0 && item.consumable;
		/// <summary>The vanilla damage class a family belongs to: guns/bows Ranged, Magic Magic, blades/spears Melee. Null for None/Unsupported.</summary>
		public static DamageClass FamilyDamageClass(WeaponFamily f) => f switch
		{
			WeaponFamily.Gun or WeaponFamily.Bow => DamageClass.Ranged,
			WeaponFamily.Magic => DamageClass.Magic,
			WeaponFamily.Broadsword or WeaponFamily.Shortsword or WeaponFamily.Spear or WeaponFamily.Boomerang => DamageClass.Melee,
			WeaponFamily.Summon or WeaponFamily.Whip => DamageClass.Summon,
			_ => null,
		};

		/// <summary>The damage class an item counts as for doctrine matching: the family class, except a plain shooter / non-magic caster in the caster family reports its own class (or null when it matches none).</summary>
		public static DamageClass ItemDamageClass(Item item)
		{
			if (item == null || item.IsAir) return null;
			var f = GetProfile(item).Family;
			if (f == WeaponFamily.Boomerang) return item.CountsAsClass(DamageClass.Ranged) && !item.CountsAsClass(DamageClass.Melee) ? DamageClass.Ranged : DamageClass.Melee;
			if (f == WeaponFamily.Magic && !item.CountsAsClass(DamageClass.Magic))
				return item.CountsAsClass(DamageClass.Ranged) ? DamageClass.Ranged : item.CountsAsClass(DamageClass.Melee) ? DamageClass.Melee : null;
			return FamilyDamageClass(f);
		}

		/// <summary>Progression tier of a rarity: 0 and below is 0, Expert/Master count as <see cref="LevyConstants.SpecialRarityTier"/>, capped at <see cref="LevyConstants.MaxRarityTier"/>.</summary>
		public static int RarityTier(int rare)
		{
			if (rare == ItemRarityID.Expert || rare == ItemRarityID.Master) return LevyConstants.SpecialRarityTier;
			return Math.Clamp(rare, 0, LevyConstants.MaxRarityTier);
		}

		/// <summary>Rank weapons for best-first issue: damage per second scaled up by rarity tier and value, so sensible high-tier weapons rank up. Higher is better.</summary>
		public static float Score(Item item)
		{
			if (item == null || item.IsAir) return 0f;
			var p = GetProfile(item);
			if (!p.Supported) return 0f;
			float dps = item.damage * p.ProjectilesPerShot * 60f / Math.Max(1, item.useTime);
			if (p.Family == WeaponFamily.Summon) dps = item.damage * LevyConstants.SummonScoreDamageFactor; // the minions do the work: useTime means nothing
			else if (p.Family == WeaponFamily.Whip) dps *= LevyConstants.WhipScoreFactor;
			else if (p.Family == WeaponFamily.Boomerang) dps *= LevyConstants.BoomerangScoreFactor;
			// Short-legged extended weapons (flamethrowers) are worth less than their raw DPS: they only work at point-blank.
			if (p.Extended && p.IsRanged) dps *= Math.Clamp(p.RangeTiles / (float)LevyConstants.RangeMagic, 0.4f, 1f);
			float tier = 1f + LevyConstants.WeaponRarityWeight * RarityTier(item.rare) + LevyConstants.WeaponValueWeight * (float)Math.Log10(1.0 + Math.Max(0, item.value));
			return dps * tier;
		}
	}
}
