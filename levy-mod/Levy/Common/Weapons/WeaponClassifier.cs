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
		/// <summary>How far the weapon's own blow reaches, in tiles (the swing/thrust/lash). Equals <see cref="RangeTiles"/> except for projectile-melee weapons, whose RangeTiles is the longer beam reach.</summary>
		public int MeleeReachTiles = LevyConstants.RangeMelee;
		/// <summary>A melee swing that also launches a real friendly projectile (Enchanted Sword, Ice Blade, Beam Sword, Starfury, Terra Blade...). The soldier swings from out to <see cref="RangeTiles"/>.</summary>
		public bool FiresProjectileMelee;
		/// <summary>The projectile type a <see cref="FiresProjectileMelee"/> swing launches (0 otherwise).</summary>
		public int BeamProjType;
		public bool Gravity; public bool Homing;
		public int MultishotCount = 1; public float SpreadDeg;
		public DamageClass DamageType = DamageClass.Default;
		/// <summary>True when the weapon is only accepted by the widened rules (non-bullet/arrow ammo, non-magic casters, plain shooters).</summary>
		public bool Extended;
		/// <summary>Gun / bow / magic / thrown / held ranged: picks targets and opens fire out to <see cref="RangeTiles"/> x <see cref="LevyConstants.RangedFireRangeBuffer"/>.</summary>
		public bool OpensEarly;
		/// <summary>Reach multiplier against a boss target (1 for weapons whose shot cannot fly that far: short-lived flames and bolts, thrown, melee).</summary>
		public float BossReachMult = 1f;
		/// <summary>Spears/shortswords: how far the thrust reaches, in pixels.</summary>
		public float ThrustReachPx;
		/// <summary>Boomerangs count as ranged for the brain (hold a standoff, need a line of sight); they use no ammo.</summary>
		public bool IsRanged => Family == WeaponFamily.Gun || Family == WeaponFamily.Bow || Family == WeaponFamily.Magic || Family == WeaponFamily.Boomerang || Family == WeaponFamily.Thrown || Family == WeaponFamily.ThrownMelee || (Family == WeaponFamily.HeldWeapon && !HeldMelee);
		/// <summary>Held-projectile weapon of the melee class (Starlight, Solar Eruption): positions and engages as melee. Ranged/magic held weapons use the ranged standoff.</summary>
		public bool HeldMelee;
		public bool IsHeld => Family == WeaponFamily.HeldWeapon;
		/// <summary>Whips count as melee for the brain (close to the lash's reach, avoid bosses); they stop short at a fraction of their reach.</summary>
		public bool IsMelee => Family == WeaponFamily.Broadsword || Family == WeaponFamily.Shortsword || Family == WeaponFamily.Spear || Family == WeaponFamily.Whip || Family == WeaponFamily.Yoyo || Family == WeaponFamily.Flail || (Family == WeaponFamily.HeldWeapon && HeldMelee);
		/// <summary>Yoyos and flails are melee-class tethered weapons: the soldier throws a Levy-owned projectile out on a short leash and it comes back. No ammo.</summary>
		public bool IsYoyo => Family == WeaponFamily.Yoyo;
		public bool IsFlail => Family == WeaponFamily.Flail;
		/// <summary>Lash-style melee that holds off its target at a fraction of its reach (whips, yoyos, flails) instead of closing to blade range.</summary>
		public bool IsTethered => Family == WeaponFamily.Whip || Family == WeaponFamily.Yoyo || Family == WeaponFamily.Flail;
		/// <summary>Fraction of <see cref="RangeTiles"/> a tethered soldier holds at (0 for everything else).</summary>
		public float TetherHoldFraction;
		public bool IsSummon => Family == WeaponFamily.Summon;
		public bool IsSentry => Family == WeaponFamily.Sentry;
		/// <summary>Weapons that are never swung or fired: the soldier keeps a flock (summon staff) or a tower (sentry) up instead.</summary>
		public bool IsDeployer => Family == WeaponFamily.Summon || Family == WeaponFamily.Sentry;
		public bool IsWhip => Family == WeaponFamily.Whip;
		public bool IsBoomerang => Family == WeaponFamily.Boomerang;
		/// <summary>A consumable the soldier throws from a stack in his weapon slot (knives, shuriken, javelins, grenades, bombs). Each throw uses one up.</summary>
		public bool IsThrown => Family == WeaponFamily.Thrown;
		/// <summary>A reusable melee-class thrower (Daybreak, Shadowflame Knife, Vampire Knives): thrown again and again, never used up. Counts as ranged for the brain, at a short melee-thrower range.</summary>
		public bool IsThrownMelee => Family == WeaponFamily.ThrownMelee;
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
		/// stationary-ish soldier cannot use: tools, consumables, held-beam weapons, custom-resource modded weapons and tethered lines (harpoons, hooks).
		/// Sentries (item.sentry) get their own family: the soldier plants the tower and keeps it up (see SoldierNPC.MaintainSentries).
		/// <paramref name="extended"/> is set when only the widened rules accepted it.
		/// </summary>
		private static WeaponFamily ClassifyCore(Item item, out bool extended)
		{
			WeaponFamily family = ClassifyFamily(item, out extended);
			// Families that fire the item's OWN projectile (soldier shots are owned by the commander): refuse a modded shot that pins its owner's item-use state and never
			// releases it (Mechanica's Serpent's Spear sets owner.heldProj/itemAnimation/itemTime every tick with no input check), or the commander can no longer attack.
			// Spear/Shortsword/Whip/Boomerang/Summon soldiers use Levy's own projectiles instead, so a weapon of those families never spawns its locking shot.
			// A MELEE owner-locker (Serpent's Spear) is thrown as a Levy SoldierBoomerang: its own projectile, drawn with the item's shot sprite, returning to the soldier, so the commander is never pinned.
			// A non-melee one (a gun or magic bolt) cannot sensibly be thrown and stays Unsupported.
			if ((family == WeaponFamily.Gun || family == WeaponFamily.Bow || family == WeaponFamily.Magic || family == WeaponFamily.Broadsword || family == WeaponFamily.ThrownMelee || family == WeaponFamily.Thrown)
				&& item.shoot > 0 && OwnerLockProbe.LocksOwner(item.shoot))
			{
				extended = false;
				return item.CountsAsClass(DamageClass.Melee) ? WeaponFamily.Boomerang : WeaponFamily.Unsupported;
			}
			return family;
		}

		private static WeaponFamily ClassifyFamily(Item item, out bool extended)
		{
			extended = false;
			if (item == null || item.IsAir) return WeaponFamily.None;
			// Thrown consumables (knives, shuriken, javelins, grenades, bombs) are decided before the consumable / zero-damage rejections below: a bomb item has damage 0
			// (the blast damage lives on its projectile) and every one of them is a consumable.
			if (item.consumable && IsThrownConsumable(item)) return WeaponFamily.Thrown;
			if (item.damage <= 0) return WeaponFamily.Unsupported;
			// Soldiers do not mine: any pickaxe / drill is refused. An axe or hammer is accepted only when it is a plain melee swing that launches nothing (Chlorophyte Greataxe / Warhammer);
			// channeled saws and drills (Chlorophyte Chainsaw: useStyle Shoot, shoot > 0, owner-anchored) fail the Swing / shoot == 0 gate and stay benched.
			if (item.pick > 0) return WeaponFamily.Unsupported;
			if ((item.axe > 0 || item.hammer > 0) && !IsPlainMeleeSwingTool(item)) return WeaponFamily.Unsupported;
			if (item.consumable) return WeaponFamily.Unsupported;
			if (item.useStyle == ItemUseStyleID.None) return WeaponFamily.Unsupported;
			// A modded weapon that counts as none of the five combat classes runs on a custom resource (Mechanica's MechanicDamage power weapons): not issued.
			if (item.type >= ItemID.Count && !CountsAsAnyCombatClass(item)) return WeaponFamily.Unsupported;
			// Sentries are Summon-class items but not minion staves: decided by the item flag, BEFORE any of the Summon/Whip/shooter rules below.
			if (item.sentry) return item.shoot > 0 && ProjectileSample(item.shoot) != null ? WeaponFamily.Sentry : WeaponFamily.Unsupported;

			int shoot = item.shoot;
			Projectile sample = ProjectileSample(shoot);
			// Channeled weapons are accepted like any other (modded "hold to fire" guns set channel for that reason). Those whose shot is a held projectile anchored
			// to its owner (Last Prism, Charged Blaster, Laser Machinegun, Vortex Beater, Phantasm, Phantom Phoenix, Starlight...) become HeldWeapon: the real vanilla
			// held AI is run on the soldier (see HeldWeapons). Any other owner-anchored shot (a bare thick-laser beam) stays benched.
			if (item.channel && sample != null && IsHeldOwnerProjectile(sample))
			{
				bool realClass = item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Magic) || item.CountsAsClass(DamageClass.Melee);
				return sample.aiStyle == ProjAIStyleID.HeldProjectile && realClass ? WeaponFamily.HeldWeapon : WeaponFamily.Unsupported;
			}

			// Soldier-anchored families (SoldierMinion / SoldierWhip / SoldierBoomerang): whips first, since whips are Summon-class items.
			if (sample != null && !sample.sentry)
			{
				if (IsSet(ProjectileID.Sets.IsAWhip, shoot) || sample.aiStyle == ProjAIStyleID.Whip) return WeaponFamily.Whip;
				if (item.CountsAsClass(DamageClass.Summon) && (sample.minion || sample.minionSlots > 0f)) return WeaponFamily.Summon;
				if (sample.aiStyle == ProjAIStyleID.Boomerang && item.useAmmo == AmmoID.None) return WeaponFamily.Boomerang;
				// Yoyos and flails: Levy's own SoldierYoyo / SoldierFlail (the vanilla ones are held by, and tethered to, their owner).
				if (item.useAmmo == AmmoID.None && IsYoyoProjectile(shoot, sample)) return WeaponFamily.Yoyo;
				if (item.useAmmo == AmmoID.None && sample.aiStyle == ProjAIStyleID.Flail) return WeaponFamily.Flail;
			}
			if (item.useAmmo == AmmoID.None && IsSet(ItemID.Sets.Yoyo, item.type) && item.shoot > 0) return WeaponFamily.Yoyo; // a yoyo whose shot has no loadable sample
			if (item.type == ItemID.Harpoon) return WeaponFamily.Unsupported; // tethered to its owner (see the projectile check below)
			// Night's Edge family: a no-melee swing whose "projectile" is a slash drawn around the owner's body. A soldier swings the blade instead (the hitbox is the damage).
			if (sample != null && item.useStyle == ItemUseStyleID.Swing && item.CountsAsClass(DamageClass.Melee) && IsHeldSlashProjectile(shoot)) return WeaponFamily.Broadsword;
			if (item.CountsAsClass(DamageClass.Summon)) return WeaponFamily.Unsupported; // sentries, summon tools and anything without a minion shot
			if (sample != null)
			{
				int ai = sample.aiStyle;
				if (ai == ProjAIStyleID.Boomerang || ai == ProjAIStyleID.Whip) return WeaponFamily.Unsupported;
				// Tethered lines (Harpoon, Golem Fist, Boxing Glove, Chain Knife) retract to Main.player[owner] and allow one out per owner, and grappling hooks
				// anchor to it: soldier shots are owned by the commander, so they would tether to the commander and take over the commander's own slot.
				if (ai == ProjAIStyleID.Harpoon || ai == ProjAIStyleID.Hook) return WeaponFamily.Unsupported;
				if (IsSet(ProjectileID.Sets.IsAWhip, shoot)) return WeaponFamily.Unsupported;
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
				// Zenith (noMelee, its swords orbit the body): the swing hitbox is the melee damage, so it fights at blade reach like any broadsword (see MiscProjSwap for the sword anchor).
				else if (item.useStyle == ItemUseStyleID.Swing && (!item.noMelee || item.type == ItemID.Zenith)) melee = WeaponFamily.Broadsword;
				if (melee != WeaponFamily.Unsupported) { extended = !viaMeleeClass; return melee; }
			}

			// Reusable melee-class throwers (Daybreak, Shadowflame Knife, Vampire Knives): no-melee, NOT consumable, thrown shot. Decided before the plain-shooter fallback so they fight at a short
			// melee-thrower range instead of kiting like a caster. (Consumable throwables were already taken by the Thrown family above.)
			if (IsReusableMeleeThrower(item, sample)) return WeaponFamily.ThrownMelee;

			// Plain shooter: a projectile weapon with no ammo and no mana (cannons, wands, modded launchers). The shoot pipeline decides what happens.
			if (shoot > 0 && shoot != ProjectileID.PurificationPowder && sample != null)
			{
				extended = true;
				return WeaponFamily.Magic; // fired by the caster path at zero mana cost
			}
			return WeaponFamily.Unsupported;
		}

		/// <summary>
		/// A consumable that is thrown as a weapon: it shoots a friendly, non-minion projectile that flies on a thrown AI (ThrownProjectile 2, Arrow 1 for javelins,
		/// Explosive 16 for grenades and bombs, GroundProjectile 14 for the spiky ball and beenade, StickProjectile 113 for the bone javelin). Never ammo, a launcher
		/// shot, a placeable, a potion, a liquid / dirt bomb or a modded item with no combat class. A zero-damage item (a bomb) must be an Explosive-AI shot, so a
		/// glowstick or any other harmless thrown item is rejected.
		/// </summary>
		public static bool IsThrownConsumable(Item item)
		{
			if (item == null || item.IsAir || !item.consumable || item.shoot <= 0) return false;
			if (item.ammo > 0 || item.useAmmo != AmmoID.None || item.accessory || item.IsACoin || item.sentry) return false;
			if (item.createTile >= 0 || item.createWall >= 0 || item.healLife > 0 || item.healMana > 0 || item.buffType > 0) return false;
			if (item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.useStyle == ItemUseStyleID.None) return false;
			if (item.CountsAsClass(DamageClass.Summon)) return false;
			if (item.type < ItemID.Count ? LevyConstants.ThrownToolBombItems.Contains(item.type) : !CountsAsAnyCombatClass(item)) return false;
			var sample = ProjectileSample(item.shoot);
			if (sample == null || !sample.friendly || sample.hostile || sample.minion || sample.sentry || sample.minionSlots > 0f) return false;
			int ai = sample.aiStyle;
			bool thrownAi = ai == ProjAIStyleID.ThrownProjectile || ai == ProjAIStyleID.Arrow || ai == ProjAIStyleID.Explosive || ai == ProjAIStyleID.GroundProjectile || ai == LevyConstants.ThrownStickAiStyle;
			if (!thrownAi) return false;
			return item.damage > 0 || ai == ProjAIStyleID.Explosive;
		}

		/// <summary>
		/// A reusable melee thrower: melee class, no-melee, NOT consumable, with damage and a friendly non-minion shot that flies on a thrown AI
		/// (ThrownProjectile 2: knives; StickProjectile 113: Daybreak's spear). Never ammo-fed, never a mana weapon, never a channeled / held one.
		/// </summary>
		public static bool IsReusableMeleeThrower(Item item, Projectile sample)
		{
			if (item == null || item.IsAir || item.consumable || item.damage <= 0 || item.shoot <= 0 || sample == null) return false;
			if (!item.noMelee || !item.CountsAsClass(DamageClass.Melee)) return false;
			if (item.useAmmo != AmmoID.None || item.mana > 0 || item.channel || item.sentry) return false;
			if (!sample.friendly || sample.hostile || sample.minion || sample.sentry || sample.minionSlots > 0f) return false;
			return sample.aiStyle == ProjAIStyleID.ThrownProjectile || sample.aiStyle == LevyConstants.ThrownStickAiStyle;
		}

		/// <summary>Damage used to rank a thrown item: its own, else the blast its bomb projectile writes when it goes off.</summary>
		public static int ThrownRankDamage(Item src) =>
			src.damage > 0 ? src.damage : LevyConstants.ThrownDynamiteItems.Contains(src.type) ? LevyConstants.ThrownDynamiteBlastDamage : LevyConstants.ThrownBlastDamage;

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
				case WeaponFamily.Gun: p.RangeTiles = p.Extended ? ShortShotRange(src, BumpedRange(LevyConstants.RangeGun)) : BumpedRange(LevyConstants.RangeGun); p.Gravity = false; break;
				case WeaponFamily.Bow: p.RangeTiles = LevyConstants.RangeBow; p.Gravity = true; break;
				case WeaponFamily.Magic: p.RangeTiles = p.Extended ? ShortShotRange(src, BumpedRange(LevyConstants.RangeMagic)) : BumpedRange(LevyConstants.RangeMagic); p.Gravity = ProjectileHasGravity(src.shoot); p.RangeTiles = ClampToDriverReach(src.shoot, p.RangeTiles); break;
				case WeaponFamily.Summon: p.RangeTiles = LevyConstants.RangeSummon; break;
				case WeaponFamily.Sentry: p.RangeTiles = LevyConstants.RangeSentry; break;
				case WeaponFamily.Boomerang: p.RangeTiles = LevyConstants.RangeBoomerang; break;
				case WeaponFamily.Thrown: p.RangeTiles = LevyConstants.RangeThrown; p.Gravity = true; break;
				case WeaponFamily.ThrownMelee: p.RangeTiles = LevyConstants.RangeThrownMelee; p.Gravity = true; break;
				case WeaponFamily.Whip:
					p.ThrustReachPx = WhipReach(src.shootSpeed);
					p.RangeTiles = Math.Clamp((int)Math.Round(p.ThrustReachPx / LevyConstants.TilePx), LevyConstants.WhipRangeMinTiles, LevyConstants.WhipRangeMaxTiles);
					p.TetherHoldFraction = LevyConstants.WhipHoldFraction;
					break;
				case WeaponFamily.Yoyo:
					p.ThrustReachPx = YoyoReach(src.shoot);
					p.RangeTiles = Math.Clamp((int)Math.Round(p.ThrustReachPx / LevyConstants.TilePx), LevyConstants.YoyoRangeMinTiles, LevyConstants.YoyoRangeMaxTiles);
					p.TetherHoldFraction = LevyConstants.YoyoHoldFraction;
					break;
				case WeaponFamily.Flail:
					p.ThrustReachPx = FlailReach(src.shootSpeed);
					p.RangeTiles = Math.Clamp((int)Math.Round(p.ThrustReachPx / LevyConstants.TilePx), LevyConstants.FlailRangeMinTiles, LevyConstants.FlailRangeMaxTiles);
					p.TetherHoldFraction = LevyConstants.FlailHoldFraction;
					break;
				case WeaponFamily.Spear:
				case WeaponFamily.Shortsword:
					p.ThrustReachPx = ThrustReach(src.shootSpeed);
					p.RangeTiles = Math.Clamp((int)Math.Round(p.ThrustReachPx / LevyConstants.TilePx), LevyConstants.ThrustRangeMinTiles, LevyConstants.ThrustRangeMaxTiles);
					break;
				case WeaponFamily.HeldWeapon:
					p.HeldMelee = src.CountsAsClass(DamageClass.Melee) && !src.CountsAsClass(DamageClass.Ranged) && !src.CountsAsClass(DamageClass.Magic);
					p.RangeTiles = p.HeldMelee ? LevyConstants.HeldMeleeReachTiles
						: src.useAmmo == AmmoID.Arrow ? LevyConstants.RangeBow : src.useAmmo == AmmoID.Bullet ? BumpedRange(LevyConstants.RangeGun) : BumpedRange(LevyConstants.RangeMagic);
					break;
				default: p.RangeTiles = LevyConstants.RangeMelee; break;
			}
			p.MeleeReachTiles = p.RangeTiles;
			if (p.Family == WeaponFamily.Broadsword) ApplyProjectileMelee(src, p);
			ApplyLongReach(src, p);
			return p;
		}

		/// <summary>
		/// A broadsword whose swing launches a real friendly projectile gets FiresProjectileMelee and the projectile's reach as RangeTiles (never below the
		/// melee reach, capped at ProjectileMeleeReachCapTiles). Held slashes drawn around the body (Night's Edge family) do not count; Terra Blade's
		/// flown beam comes from the SwordBeams table.
		/// </summary>
		private static void ApplyProjectileMelee(Item src, WeaponProfile p)
		{
			// Zenith's swords swirl round the soldier (not a flown beam): it stays a pure blade-reach fighter.
			if (src.type == ItemID.Zenith) return;
			bool table = SwordBeams.HasBeam(src.type);
			int beam = table ? SwordBeams.BeamProjectile(src.type) : src.shoot;
			if (beam <= 0) return;
			var sample = ProjectileSample(beam);
			if (sample == null || !sample.friendly || sample.hostile || sample.minion || sample.sentry) return;
			if (!table && IsHeldSlashProjectile(beam)) return;
			if (IsHeldOwnerProjectile(sample)) return;
			int reach = LevyConstants.ProjectileMeleeReachCapTiles;
			if (!table && sample.timeLeft <= LevyConstants.LongShotLifetimeTicks)
			{
				float speed = Math.Max(1f, src.shootSpeed) * (1 + Math.Max(0, sample.extraUpdates));
				float tiles = speed * Math.Max(1, sample.timeLeft) / LevyConstants.TilePx * LevyConstants.ShortShotReachFraction;
				reach = Math.Clamp((int)Math.Round(tiles), LevyConstants.ShortShotRangeMinTiles, LevyConstants.ProjectileMeleeReachCapTiles);
			}
			p.FiresProjectileMelee = true;
			p.BeamProjType = beam;
			p.RangeTiles = Math.Max(p.MeleeReachTiles, reach);
		}

		/// <summary>
		/// Magic shots whose behaviour is a soldier-side driver with a hard reach shorter than the family range (Medusa Head's ray reaches
		/// <see cref="LevyConstants.MedusaRangePx"/>) must not have the soldier stall at the family range, out of the ray's reach. Drivers that home from
		/// anywhere (Flying Knife, guided missiles) or reach past the family range (Soul Drain) are left alone.
		/// </summary>
		private static int ClampToDriverReach(int projType, int rangeTiles)
		{
			float reachPx = projType == ProjectileID.MedusaHead ? LevyConstants.MedusaRangePx : 0f;
			if (reachPx <= 0f) return rangeTiles;
			return Math.Min(rangeTiles, Math.Max(LevyConstants.ShortShotRangeMinTiles, (int)Math.Round(reachPx / LevyConstants.TilePx * LevyConstants.DriverReachFraction)));
		}

		private static int BumpedRange(int familyRange) => (int)Math.Round(familyRange * LevyConstants.RangedRangeMult);

		/// <summary>
		/// Fire-early and boss-reach flags. Every ranged family opens fire a little past its range; guns, bows and magic also reach out much further against a
		/// boss, unless the shot dies young (flames, short bolts) and could not fly that far. Bows use a smaller multiplier: arrows drop.
		/// </summary>
		private static void ApplyLongReach(Item src, WeaponProfile p)
		{
			p.OpensEarly = p.IsRanged && p.Family != WeaponFamily.Boomerang && p.Family != WeaponFamily.ThrownMelee;
			if (p.Family != WeaponFamily.Gun && p.Family != WeaponFamily.Bow && p.Family != WeaponFamily.Magic) return;
			var sample = src.useAmmo == AmmoID.Bullet || src.useAmmo == AmmoID.Arrow ? null : ProjectileSample(src.shoot); // bullet / arrow weapons fly their ammo's shot, not the placeholder
			if (sample != null && (sample.timeLeft <= LevyConstants.LongShotLifetimeTicks || sample.aiStyle == ProjAIStyleID.Flamethrower)) return;
			p.BossReachMult = p.Family == WeaponFamily.Bow ? LevyConstants.RangedBossBowRangeMult : p.Family == WeaponFamily.Magic ? LevyConstants.RangedBossMagicRangeMult : LevyConstants.RangedBossRangeMult;
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

		/// <summary>True for a yoyo's shot: the Yoyo AI style or any type with a yoyo flight-time multiplier registered.</summary>
		public static bool IsYoyoProjectile(int projType, Projectile sample)
		{
			if (sample != null && sample.aiStyle == ProjAIStyleID.Yoyo) return true;
			var set = ProjectileID.Sets.YoyosLifeTimeMultiplier;
			return set != null && projType > 0 && projType < set.Length && set[projType] != -1f;
		}

		/// <summary>How far (px) a yoyo's string lets it fly: the shot's registered maximum range, else a default, clamped into the yoyo band.</summary>
		public static float YoyoReach(int projType)
		{
			var set = ProjectileID.Sets.YoyosMaximumRange;
			float r = set != null && projType > 0 && projType < set.Length ? set[projType] : 0f;
			if (r <= 0f) r = LevyConstants.YoyoDefaultReachPx;
			return Math.Clamp(r, LevyConstants.YoyoReachMinPx, LevyConstants.YoyoReachMaxPx);
		}

		/// <summary>How long (ticks) a yoyo stays out: its registered flight-time multiplier in seconds (non-positive means no limit), clamped into the loiter band.</summary>
		public static int YoyoLoiterTicks(int projType)
		{
			var set = ProjectileID.Sets.YoyosLifeTimeMultiplier;
			float m = set != null && projType > 0 && projType < set.Length ? set[projType] : -1f;
			int ticks = m > 0f ? (int)(m * 60f) : LevyConstants.YoyoDefaultLoiterTicks;
			return Math.Clamp(ticks, LevyConstants.YoyoLoiterMinTicks, LevyConstants.YoyoLoiterMaxTicks);
		}

		public static float FlailReach(float shootSpeed) => Math.Clamp(shootSpeed * LevyConstants.FlailReachPerShootSpeed, LevyConstants.FlailReachMinPx, LevyConstants.FlailReachMaxPx);

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

		/// <summary>True for projectiles that live attached to their owner's hands: the held-weapon style (Laser Machinegun, Vortex Beater, Phantasm, Last Prism's holder) the thick-laser beam style (Last Prism ray, Charged Blaster beam) and the drill / chainsaw style (aiStyle 20, reads owner.channel and MountedCenter).</summary>
		public static bool IsHeldOwnerProjectile(Projectile sample) => sample != null
			&& (sample.aiStyle == ProjAIStyleID.HeldProjectile || sample.aiStyle == ProjAIStyleID.ThickLaser || sample.aiStyle == ProjAIStyleID.Drill);

		/// <summary>An axe or hammer that is just a melee blade: melee class, swung (useStyle Swing) and launching no projectile (shoot == 0), so nothing owner-anchored can come out of it.</summary>
		private static bool IsPlainMeleeSwingTool(Item item) => item.CountsAsClass(DamageClass.Melee) && item.useStyle == ItemUseStyleID.Swing && item.shoot == 0;

		private static bool CountsAsAnyCombatClass(Item item) =>
			item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Magic) || item.CountsAsClass(DamageClass.Melee)
			|| item.CountsAsClass(DamageClass.Summon) || item.CountsAsClass(DamageClass.Throwing);

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
			WeaponFamily.Broadsword or WeaponFamily.Shortsword or WeaponFamily.Spear or WeaponFamily.Boomerang or WeaponFamily.Yoyo or WeaponFamily.Flail or WeaponFamily.ThrownMelee => DamageClass.Melee,
			WeaponFamily.Summon or WeaponFamily.Whip or WeaponFamily.Sentry => DamageClass.Summon,
			_ => null,
		};

		/// <summary>The damage class an item counts as for doctrine matching: the family class, except a plain shooter / non-magic caster in the caster family reports its own class (or null when it matches none).</summary>
		public static DamageClass ItemDamageClass(Item item)
		{
			if (item == null || item.IsAir) return null;
			var f = GetProfile(item).Family;
			if (f == WeaponFamily.HeldWeapon) return item.CountsAsClass(DamageClass.Magic) ? DamageClass.Magic : item.CountsAsClass(DamageClass.Ranged) ? DamageClass.Ranged : DamageClass.Melee;
			if (f == WeaponFamily.Thrown) return item.CountsAsClass(DamageClass.Melee) && !item.CountsAsClass(DamageClass.Ranged) ? DamageClass.Melee : DamageClass.Ranged;
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

		/// <summary>Representative damage of the ammo a weapon fires (0 for a weapon that uses none, or ammo not worth counting): the part of a shooter's output that does not sit on the weapon itself.</summary>
		public static int AmmoScoreDamage(Item item)
		{
			int id = item.useAmmo;
			if (id <= 0) return 0;
			if (id == AmmoID.Bullet) return LevyConstants.AmmoScoreBulletDamage;
			if (id == AmmoID.Arrow) return LevyConstants.AmmoScoreArrowDamage;
			if (id == AmmoID.Rocket) return LevyConstants.AmmoScoreRocketDamage;
			if (id == AmmoID.Dart) return LevyConstants.AmmoScoreDartDamage;
			return 0;
		}

		/// <summary>Minions a summon staff is assumed to keep out when ranked: the kit's assumed slots (grow with the staff's rarity tier) over the staff's slot cost per minion. See <see cref="LevyConstants.SummonScoreBaseSlots"/>.</summary>
		public static float SummonScoreMinions(Item item)
		{
			float slots = Math.Min(LevyConstants.SummonScoreMaxSlots, LevyConstants.SummonScoreBaseSlots + LevyConstants.SummonScoreSlotsPerTier * RarityTier(item.rare));
			return Math.Clamp(slots / global::Levy.Content.NPCs.SoldierNPC.SlotsPerMinion(item), 1f, LevyConstants.MinionMaxPerSoldier);
		}

		/// <summary>Rank weapons for best-first issue: damage per second scaled up by rarity tier and value, so sensible high-tier weapons rank up. Higher is better.</summary>
		public static float Score(Item item)
		{
			if (item == null || item.IsAir) return 0f;
			var p = GetProfile(item);
			if (!p.Supported) return 0f;
			// Burst weapons fire a capped run of shots per cycle, then play out the animation + reuseDelay: a reuseDelay weapon fires ceil(useAnimation / useTime) shots per
			// useAnimation + reuseDelay ticks, and a useLimitPerAnimation weapon (Tome of Infinite Wisdom: 4 per 25 ticks) fires min(limit, natural) then idles (see WeaponUse.FinishUse).
			float cadence = Math.Max(1, item.useTime);
			int anim = Math.Max(1, item.useAnimation);
			int naturalShots = Math.Max(1, (anim + (int)cadence - 1) / (int)cadence);
			int shots = item.useLimitPerAnimation.HasValue && item.useLimitPerAnimation.Value > 0 ? Math.Min(item.useLimitPerAnimation.Value, naturalShots) : naturalShots;
			if (shots < naturalShots || item.reuseDelay > 0) cadence = (anim + Math.Max(0, item.reuseDelay)) / (float)shots;
			// A held weapon with a known real cadence (Vortex Beater) is ranked on it, undamped.
			bool heldKnown = p.Family == WeaponFamily.HeldWeapon && LevyConstants.HeldRealCadence.TryGetValue(item.type, out int heldCadence) && heldCadence > 0;
			if (heldKnown) cadence = LevyConstants.HeldRealCadence[item.type];
			float shotDamage = p.Family == WeaponFamily.Thrown ? ThrownRankDamage(item) : item.damage + AmmoScoreDamage(item); // a shooter's round adds to the weapon's own damage
			float dps = shotDamage * p.ProjectilesPerShot * 60f / cadence;
			if (p.Family == WeaponFamily.Summon) dps = item.damage * LevyConstants.SummonScoreDamageFactor * SummonScoreMinions(item); // the minions do the work: useTime means nothing
			else if (p.Family == WeaponFamily.Sentry) dps = item.damage * LevyConstants.SentryScoreDamageFactor; // the tower does the work: useTime means nothing
			else if (p.Family == WeaponFamily.Whip) dps *= LevyConstants.WhipScoreFactor;
			else if (p.Family == WeaponFamily.Boomerang) dps *= LevyConstants.BoomerangScoreFactor;
			else if (p.Family == WeaponFamily.Yoyo) dps *= LevyConstants.YoyoScoreFactor;
			else if (p.Family == WeaponFamily.Flail) dps *= LevyConstants.FlailScoreFactor;
			else if (p.Family == WeaponFamily.Thrown) dps *= LevyConstants.ThrownScoreFactor;
			else if (p.Family == WeaponFamily.HeldWeapon && !heldKnown) dps *= LevyConstants.HeldScoreFactor;
			// Short-legged extended weapons (flamethrowers) are worth less than their raw DPS: they only work at point-blank.
			if (p.Extended && p.IsRanged) dps *= Math.Clamp(p.RangeTiles / (float)LevyConstants.RangeMagic, 0.4f, 1f);
			float tier = 1f + LevyConstants.WeaponRarityWeight * RarityTier(item.rare) + LevyConstants.WeaponValueWeight * (float)Math.Log10(1.0 + Math.Max(0, item.value));
			return dps * tier;
		}
	}
}
