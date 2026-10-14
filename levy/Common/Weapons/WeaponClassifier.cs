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
		/// <summary>Spears/shortswords: how far the thrust reaches, in pixels.</summary>
		public float ThrustReachPx;
		public bool IsRanged => Family == WeaponFamily.Gun || Family == WeaponFamily.Bow || Family == WeaponFamily.Magic;
		public bool IsMelee => Family == WeaponFamily.Broadsword || Family == WeaponFamily.Shortsword || Family == WeaponFamily.Spear;
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

		public static WeaponFamily Classify(Item item)
		{
			if (item == null || item.IsAir) return WeaponFamily.None;
			if (item.damage <= 0) return WeaponFamily.Unsupported;
			if (item.pick > 0 || item.axe > 0 || item.hammer > 0) return WeaponFamily.Unsupported;
			if (item.consumable) return WeaponFamily.Unsupported;
			if (item.channel) return WeaponFamily.Unsupported;
			if (item.CountsAsClass(DamageClass.Summon)) return WeaponFamily.Unsupported;
			if (IsSet(ItemID.Sets.Yoyo, item.type)) return WeaponFamily.Unsupported;

			int shoot = item.shoot;
			Projectile sample = ProjectileSample(shoot);
			if (sample != null)
			{
				int ai = sample.aiStyle;
				if (ai == ProjAIStyleID.Yoyo || ai == ProjAIStyleID.Flail || ai == ProjAIStyleID.Boomerang || ai == ProjAIStyleID.Whip) return WeaponFamily.Unsupported;
				if (IsSet(ProjectileID.Sets.IsAWhip, shoot)) return WeaponFamily.Unsupported;
				if (shoot < ProjectileID.Sets.YoyosLifeTimeMultiplier.Length && ProjectileID.Sets.YoyosLifeTimeMultiplier[shoot] != -1f) return WeaponFamily.Unsupported;
			}

			if (item.useAmmo != AmmoID.None)
			{
				if (item.useAmmo == AmmoID.Bullet) return WeaponFamily.Gun;
				if (item.useAmmo == AmmoID.Arrow) return WeaponFamily.Bow;
				return WeaponFamily.Unsupported; // darts, rockets, coins, snowballs, gel, solutions, flares, stakes: out of v1
			}

			if (item.mana > 0 && shoot > 0 && item.CountsAsClass(DamageClass.Magic)) return WeaponFamily.Magic;

			if (item.CountsAsClass(DamageClass.Melee))
			{
				if (sample != null && sample.aiStyle == ProjAIStyleID.Spear) return WeaponFamily.Spear;
				if (IsSet(ItemID.Sets.Spears, item.type)) return WeaponFamily.Spear;
				if (item.useStyle == ItemUseStyleID.Rapier) return WeaponFamily.Shortsword;
				if (sample != null && sample.aiStyle == ProjAIStyleID.ShortSword) return WeaponFamily.Shortsword;
				if (item.useStyle == ItemUseStyleID.Swing && !item.noMelee) return WeaponFamily.Broadsword;
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
			var p = new WeaponProfile
			{
				ItemType = src.type,
				Family = Classify(src),
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
				case WeaponFamily.Gun: p.RangeTiles = LevyConstants.RangeGun; p.Gravity = false; break;
				case WeaponFamily.Bow: p.RangeTiles = LevyConstants.RangeBow; p.Gravity = true; break;
				case WeaponFamily.Magic: p.RangeTiles = LevyConstants.RangeMagic; p.Gravity = ProjectileHasGravity(src.shoot); break;
				case WeaponFamily.Spear:
				case WeaponFamily.Shortsword:
					p.ThrustReachPx = ThrustReach(src.shootSpeed);
					p.RangeTiles = Math.Clamp((int)Math.Round(p.ThrustReachPx / LevyConstants.TilePx), LevyConstants.ThrustRangeMinTiles, LevyConstants.ThrustRangeMaxTiles);
					break;
				default: p.RangeTiles = LevyConstants.RangeMelee; break;
			}
			return p;
		}

		public static float ThrustReach(float shootSpeed) => Math.Max(LevyConstants.ThrustReachMinPx, shootSpeed * LevyConstants.ThrustReachPerShootSpeed);

		/// <summary>Whether a projectile type falls: vanilla aiStyle 1 adds 0.1/tick after 15 ticks unless the type is on the exempt list (bullets, lasers, Jester's arrows).</summary>
		public static bool ProjectileHasGravity(int projType)
		{
			var sample = ProjectileSample(projType);
			if (sample == null) return false;
			return sample.aiStyle == ProjAIStyleID.Arrow && !LevyConstants.AiStyle1NoGravity.Contains(projType);
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
		/// <summary>Rank weapons for best-first issue: damage per second-ish. Higher is better.</summary>
		public static float Score(Item item)
		{
			if (item == null || item.IsAir) return 0f;
			var p = GetProfile(item);
			if (!p.Supported) return 0f;
			return item.damage * p.ProjectilesPerShot * 60f / Math.Max(1, item.useTime);
		}
	}
}
