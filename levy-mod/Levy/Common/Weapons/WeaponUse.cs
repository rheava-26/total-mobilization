using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Globals;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Weapons
{
	/// <summary>Per-family firing/swinging. Called from SoldierNPC.Combat every tick while engaging. OWNED BY THE WEAPONS AGENT.</summary>
	public static class WeaponUse
	{
		/// <summary>Companion per-shot veto, consulted just before a shot is dispatched: (soldier, weapon, its profile, target). Returning false makes the soldier hold this tick
		/// (no shot). Used by companion mods for resource systems and for role weapons that must not fire at the current target. Null = none; a throwing delegate allows the shot.</summary>
		public static Func<SoldierNPC, Item, WeaponProfile, NPC, bool> PreFireGate;
		/// <summary>Ticks a vetoed soldier waits before the gate is asked again (capped by the weapon's own use time).</summary>
		private const int GateRetryTicks = 10;

		/// <summary>Try to attack <paramref name="target"/> with the soldier's current weapon. Returns true if a shot/swing started this tick.</summary>
		public static bool TryAttack(SoldierNPC s, NPC target)
		{
			if (s == null || target == null || s.Record == null || !target.active) return false;
			if (s.AttackCooldown > 0) return false;
			Item item = HeldItem(s);
			if (item == null || item.IsAir) return false;
			WeaponProfile profile = s.Weapon;
			if (profile == null || profile.ItemType != item.type) { profile = WeaponClassifier.GetProfile(item); s.Weapon = profile.Supported ? profile : null; }
			if (profile == null || !profile.Supported) return false;
			NPC seg = Aiming.NearestSegment(target, s.NPC.Center) ?? target;
			// Companion per-shot veto (a resource gate, or a role weapon that must not fire at this target). False = hold this tick; retry shortly.
			if (PreFireGate != null)
			{
				bool allow = true;
				try { allow = PreFireGate(s, item, profile, seg); } catch (Exception) { }
				if (!allow) { s.AttackCooldown = Math.Max(1, Math.Min(item.useTime, GateRetryTicks)); return false; }
			}
			try
			{
				// Right-click alternate attack: mixed in on a cadence, falling back to the primary when it cannot fire.
				if (AltEligible(profile))
				{
					if (TryAltAttack(s, item, profile, seg, out bool hold)) return true;
					if (hold) return false; // a caster saving mana for the alt holds fire for a moment
				}
				return Dispatch(s, item, profile, seg);
			}
			catch (Exception e)
			{
				// A weapon (usually a modded one the widened classifier let in) threw while firing: the soldier just holds and retries later.
				s.AttackCooldown = Math.Max(1, Math.Max(item.useTime, LevyConstants.DudWeaponRetryTicks));
				if (_reportedDuds.Add(item.type)) ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: weapon {item.Name} ({item.type}) threw while a soldier fired it: {e.Message}");
			}
			return false;
		}

		/// <summary>Families with a real case in <see cref="Dispatch"/>. Keep in sync with the switch: WeaponWiringCheck asserts every WeaponFamily is in exactly one of this and <see cref="NotFiredFamilies"/>.</summary>
		internal static readonly HashSet<WeaponFamily> FiredFamilies = new()
		{
			WeaponFamily.Gun, WeaponFamily.Bow, WeaponFamily.Magic, WeaponFamily.Broadsword, WeaponFamily.Spear, WeaponFamily.Shortsword, WeaponFamily.Whip,
			WeaponFamily.Boomerang, WeaponFamily.Yoyo, WeaponFamily.Flail, WeaponFamily.HeldWeapon, WeaponFamily.Thrown, WeaponFamily.ThrownMelee,
		};

		/// <summary>Families <see cref="Dispatch"/> deliberately never fires (summons are kept up by SoldierNPC.MaintainMinions; the rest are unsupported or absent).</summary>
		internal static readonly HashSet<WeaponFamily> NotFiredFamilies = new()
		{
			WeaponFamily.None, WeaponFamily.Unsupported, WeaponFamily.Summon, WeaponFamily.Sentry,
		};

		/// <summary>Fires the weapon's family attack (the primary, or the alt while <see cref="_altActive"/>). Returns true if a shot/swing started.</summary>
		private static bool Dispatch(SoldierNPC s, Item item, WeaponProfile profile, NPC seg)
		{
			switch (profile.Family)
			{
				case WeaponFamily.Gun:
				case WeaponFamily.Bow: return FireRanged(s, item, profile, seg);
				case WeaponFamily.Magic: return FireMagic(s, item, profile, seg);
				case WeaponFamily.Broadsword: return StartSwing(s, item, profile, seg);
				case WeaponFamily.Spear:
				case WeaponFamily.Shortsword: return StartThrust(s, item, profile, seg);
				case WeaponFamily.Whip: return StartWhip(s, item, profile, seg);
				case WeaponFamily.Boomerang: return ThrowBoomerang(s, item, profile, seg);
				case WeaponFamily.Yoyo: return ThrowYoyo(s, item, profile, seg);
				case WeaponFamily.Flail: return ThrowFlail(s, item, profile, seg);
				case WeaponFamily.HeldWeapon: return HeldWeapons.TryFire(s, item, profile, seg);
				case WeaponFamily.Thrown: return ThrowConsumable(s, item, profile, seg);
				case WeaponFamily.ThrownMelee: return ThrowMelee(s, item, profile, seg);
				case WeaponFamily.Summon: return false; // summon weapons are not fired: the flock is kept up by SoldierNPC.MaintainMinions
			}
			return false;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Alt-fire (right-click). Only the shoot-pipeline families (guns, bows, staves): their shot goes through TryRealShoot, which runs the
		// item's ModifyShootStats/Shoot hooks with the dummy as the player, so setting the dummy's altFunctionUse to 2 around the attack makes
		// the weapon take its alt branch exactly as a right-click does. Melee swings, held weapons, thrown stacks, whips, boomerangs, yoyos and
		// flails keep their left-click-only behaviour.
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>True while an alt attack is being fired: the plain-spread fallback is off, so an alt whose hooks spawn nothing fails (and the primary is used) instead of firing a stand-in.</summary>
		private static bool _altActive;

		/// <summary>Item types whose alt repeatedly did nothing: not tried again this session.</summary>
		private static readonly System.Collections.Generic.HashSet<int> _altDudItems = new();

		private static bool AltEligible(WeaponProfile profile) =>
			LevyConstants.AltFireEnabled && (profile.Family == WeaponFamily.Gun || profile.Family == WeaponFamily.Bow || profile.Family == WeaponFamily.Magic);

		/// <summary>The item fields an alt's CanUseItem/ModifyShootStats may rewrite; put back after the alt so the primary is unchanged.</summary>
		private struct ItemStatSnapshot
		{
			private readonly int useTime, useAnimation, reuseDelay, shoot, mana, damage, crit, useAmmo, useStyle;
			private readonly float shootSpeed, knockBack, scale;
			private readonly bool autoReuse, channel, noMelee, noUseGraphic;
			private readonly SoundStyle? sound;
			public ItemStatSnapshot(Item i)
			{
				useTime = i.useTime; useAnimation = i.useAnimation; reuseDelay = i.reuseDelay; shoot = i.shoot; mana = i.mana; damage = i.damage; crit = i.crit;
				useAmmo = i.useAmmo; useStyle = i.useStyle; shootSpeed = i.shootSpeed; knockBack = i.knockBack; scale = i.scale;
				autoReuse = i.autoReuse; channel = i.channel; noMelee = i.noMelee; noUseGraphic = i.noUseGraphic; sound = i.UseSound;
			}
			public void Restore(Item i)
			{
				i.useTime = useTime; i.useAnimation = useAnimation; i.reuseDelay = reuseDelay; i.shoot = shoot; i.mana = mana; i.damage = damage; i.crit = crit;
				i.useAmmo = useAmmo; i.useStyle = useStyle; i.shootSpeed = shootSpeed; i.knockBack = knockBack; i.scale = scale;
				i.autoReuse = autoReuse; i.channel = channel; i.noMelee = noMelee; i.noUseGraphic = noUseGraphic; i.UseSound = sound;
			}
		}

		/// <summary>
		/// Maybe fires the weapon's right-click attack instead of the primary. Returns true when the alt fired. Returns false (and the caller uses the
		/// primary) when the alt is on cooldown, not rolled this time, absent, refused, short of mana, or produced nothing. <paramref name="hold"/> is
		/// set when a caster is a moment from the alt's mana and should hold fire to save for it.
		/// </summary>
		private static bool TryAltAttack(SoldierNPC s, Item item, WeaponProfile profile, NPC seg, out bool hold)
		{
			hold = false;
			uint now = Main.GameUpdateCount;
			Player d = s.Dummy;
			if (d == null || !LevyConfig.Get.RealWeaponShoot || now < s.AltReadyTick || _altDudItems.Contains(item.type)) return false;
			if (s.AltHoldSince == 0 && Main.rand.NextFloat() >= LevyConstants.AltUseChance) return false;

			bool has = false;
			try { has = ItemLoader.AltFunctionUse(item, d); } catch (Exception) { }
			if (!has) { s.AltReadyTick = now + (uint)LevyConstants.AltNoAltRecheckTicks; s.AltHoldSince = 0; return false; }

			var snap = new ItemStatSnapshot(item);
			int savedAlt = d.altFunctionUse; Item savedSlot = d.inventory[0]; int savedSel = d.selectedItem;
			bool fired = false, soft = false;
			try
			{
				d.inventory[0] = item; d.selectedItem = 0;
				d.altFunctionUse = 2;
				bool can = true;
				try { can = ItemLoader.CanUseItem(item, d); } catch (Exception) { can = false; }
				if (!can) soft = true; // the weapon's own gate (alt on its own cooldown, a projectile already out): primary now, retry shortly
				else if (profile.Family == WeaponFamily.Magic && s.Record.Mana < Math.Max(0, item.mana))
				{
					soft = true;
					int missing = Math.Max(0, item.mana) - s.Record.Mana;
					if (LevyConstants.AltSaveManaTicks > 0 && s.ManaWaitTicks(missing) <= LevyConstants.AltSaveManaTicks)
					{
						if (s.AltHoldSince == 0) s.AltHoldSince = now;
						if (now - s.AltHoldSince <= (uint)LevyConstants.AltHoldMaxTicks) hold = true;
					}
				}
				else
				{
					_altActive = true;
					try { fired = Dispatch(s, item, profile, seg); } finally { _altActive = false; }
				}
			}
			catch (Exception e)
			{
				fired = false;
				if (_reportedDuds.Add(item.type)) ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: weapon {item.Name} ({item.type}) threw on its alt-fire: {e.Message}");
			}
			finally
			{
				_altActive = false;
				try { d.altFunctionUse = 0; ItemLoader.CanUseItem(item, d); } catch (Exception) { } // let the weapon reset itself to primary mode
				d.altFunctionUse = savedAlt == 2 ? 0 : savedAlt;
				snap.Restore(item);
				d.inventory[0] = savedSlot; d.selectedItem = savedSel;
			}

			if (fired)
			{
				s.AltFailStreak = 0; s.AltHoldSince = 0;
				s.AltReadyTick = now + (uint)(LevyConstants.AltMinGapTicks + Main.rand.Next(0, LevyConstants.AltGapJitterTicks + 1));
				return true;
			}
			s.AttackCooldown = 0; // a failed alt attempt must not leave a dud backoff on the primary
			if (hold) return false;
			s.AltHoldSince = 0;
			if (soft) { s.AltReadyTick = now + (uint)LevyConstants.AltRetryTicks; return false; }
			// Ran, but nothing came out: back off, and stop trying a weapon whose alt never does anything.
			s.AltReadyTick = now + (uint)LevyConstants.AltFailBackoffTicks;
			if (++s.AltFailStreak >= LevyConstants.AltGiveUpFailures && _altDudItems.Add(item.type))
				ModContent.GetInstance<Levy>()?.Logger.Info($"Levy: alt-fire of {item.Name} ({item.type}) never fired; soldiers will use only its primary.");
			return false;
		}

		/// <summary>Item types already reported as throwing, so the log gets one line per weapon instead of one per shot.</summary>
		private static readonly System.Collections.Generic.HashSet<int> _reportedDuds = new();

		/// <summary>A weapon spawned nothing this use: back off for a while so the soldier does not rerun its hooks every tick.</summary>
		internal static bool Dud(SoldierNPC s, Item item)
		{
			s.AttackCooldown = Math.Max(1, Math.Max(item.useTime, LevyConstants.DudWeaponRetryTicks));
			return false;
		}

		/// <summary>Ticks until the weapon can be used again.</summary>
		public static int Cooldown(SoldierNPC s) => s == null ? 0 : Math.Max(0, s.AttackCooldown);

		/// <summary>The item in hand: sidearm while <see cref="SoldierNPC.UsingSidearm"/>, else the main weapon.</summary>
		public static Item HeldItem(SoldierNPC s)
		{
			var g = s?.Record?.Gear; if (g == null) return null;
			return s.UsingSidearm ? g.Sidearm : g.Weapon;
		}

		/// <summary>Whether a weapon can fire right now: ammo in the pouch for guns/bows, mana for magic, always for melee.</summary>
		public static bool HasResources(SoldierNPC s, Item item, WeaponProfile profile)
		{
			if (s?.Record == null || item == null || item.IsAir || profile == null || !profile.Supported) return false;
			switch (profile.Family)
			{
				case WeaponFamily.Gun:
				case WeaponFamily.Bow:
					var pouch = s.Record.Gear.Ammo;
					return pouch != null && !pouch.IsAir && pouch.stack > 0 && pouch.ammo == profile.UseAmmo && (pouch.shoot > 0 || item.shoot > 0);
				case WeaponFamily.Magic:
					return s.Record.Mana >= Math.Max(0, item.mana);
				case WeaponFamily.HeldWeapon:
					if (profile.UseAmmo > 0) { var hp = s.Record.Gear.Ammo; return hp != null && !hp.IsAir && hp.stack > 0 && hp.ammo == profile.UseAmmo; }
					return s.Record.Mana >= Math.Max(0, item.mana);
				case WeaponFamily.Thrown:
					return item.stack > 0; // a stack of throwables is the weapon: out of them, out of that weapon
				default:
					return true;
			}
		}

		/// <summary>Muzzle in world space: a little in front of the chest on the facing side.</summary>
		public static Vector2 Muzzle(SoldierNPC s)
		{
			int dir = s.NPC.direction == 0 ? 1 : s.NPC.direction;
			return s.NPC.Center + new Vector2(dir * LevyConstants.MuzzleOffsetX, LevyConstants.MuzzleOffsetY);
		}

		// CONTRACT-REQUEST: SoldierNPC.ProjectileOwner returns the commander's index (or 254 on a server with no commander). Projectile.NewProjectile
		// only assigns ai[0..2], only sends the spawn packet and only runs Damage() when Owner == Main.myPlayer (255 on a dedicated server), so a
		// server-spawned shot owned by a player index is a ghost: unsynced, ai[] zeroed, never hits. Town-NPC attacks use Main.myPlayer for this
		// reason. Soldier shots therefore use Main.myPlayer (identical to the commander in single player) and loot credit goes through LootCredit.
		public static int OwnerFor(SoldierNPC s) => SoldierMinions.TrueMyPlayer; // Main.myPlayer, except inside a real minion's single-player Update where the machine claims 255 (see SoldierMinions)

		/// <summary>Spawns a tagged soldier projectile (server/single player). Returns null when the array is full.</summary>
		public static Projectile Spawn(SoldierNPC s, Vector2 pos, Vector2 vel, int type, int damage, float knockback, DamageClass damageType, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f)
		{
			if (type <= 0) return null;
			int idx = Projectile.NewProjectile(s.NPC.GetSource_FromAI(), pos, vel, type, Math.Max(1, damage), knockback, OwnerFor(s), ai0, ai1, ai2);
			if (idx < 0 || idx >= Main.maxProjectiles) return null;
			Projectile p = Main.projectile[idx];
			if (!p.active || p.type != type) return null;
			TagSoldierProjectile(s, p, damageType);
			return p;
		}

		/// <summary>
		/// Marks a projectile as this soldier's shot: owned by <see cref="OwnerFor"/> (never the dummy slot, or it becomes an unsynced ghost), friendly,
		/// loot-credited through <see cref="SoldierProjectileTag"/>, and (when piercing) given its own local NPC immunity. Used by <see cref="Spawn"/>
		/// and by the real-weapon-shoot path for every projectile a weapon hook spawned.
		/// </summary>
		public static void TagSoldierProjectile(SoldierNPC s, Projectile p, DamageClass dmgType)
		{
			if (s == null || p == null || !p.active) return;
			p.owner = OwnerFor(s);
			p.npcProj = true;
			p.noDropItem = true;
			p.friendly = true;
			p.hostile = false;
			if (dmgType != null) p.DamageType = dmgType;
			if (p.TryGetGlobalProjectile<SoldierProjectileTag>(out var tag)) tag.SoldierIndex = s.NPC.whoAmI;
			// Soldier shots are all owned by Main.myPlayer, so a piercing projectile would share the owner immunity slot with other
			// soldiers' shots and with the commander. Give each piercing soldier shot its own local immunity instead.
			if (p.penetrate != 1 && !p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity) { p.usesLocalNPCImmunity = true; if (p.localNPCHitCooldown <= 0) p.localNPCHitCooldown = 10; }
			p.netUpdate = true;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Ranged (guns / bows): projectile, damage, speed and knockback come from the weapon plus the ammo in the pouch.
		// ------------------------------------------------------------------------------------------------------------
		private static bool FireRanged(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			Item pouch = s.Record.Gear.Ammo;
			if (pouch == null || pouch.IsAir || pouch.stack <= 0 || pouch.ammo != profile.UseAmmo) return false;
			int projType = ResolveProjectile(s, item, pouch);
			if (projType <= 0) return Dud(s, item);
			int damage = Math.Max(1, (int)Math.Round((s.WeaponDamage(item) + Math.Max(0, pouch.damage)) * s.DamageMultiplier));
			float knockback = item.knockBack + pouch.knockBack;
			float speed = item.shootSpeed + pouch.shootSpeed;  // Player.PickAmmo adds the ammo's shootSpeed
			ModifyForAmmo(s, item, pouch, ref projType, ref speed, ref knockback);
			if (speed < 1f) speed = 1f;

			Vector2 muzzle = Muzzle(s);
			Vector2 vel = AimShot(s, muzzle, target, projType, speed, pouch.type);
			bool fired = TryRealShoot(s, item, profile, target, pouch.type, muzzle, vel, projType, damage, knockback);
			if (!fired && !_altActive) fired = FirePlain(s, item, profile, target, muzzle, vel, projType, damage, knockback);
			if (!fired) return Dud(s, item); // nothing came out: keep the ammo, hold

			if (ConsumesAmmo(s, item, pouch))
			{
				pouch.stack--;
				if (pouch.stack <= 0)
				{
					pouch.TurnToAir();
					DialogueSystem.Bark(s, DialogueTrigger.OUT_OF_AMMO);
					s.NPC.netUpdate = true;
				}
			}
			FinishUse(s, item, SkyDrop.HeldAim(item, muzzle, target, vel)); // the Daedalus Stormbow and Blood Rain Bow are held pointing at the sky their arrows fall from
			return true;
		}

		/// <summary>The ammo item's own PickAmmo hooks (ModItem/GlobalItem) may swap the projectile or speed, as in Player.PickAmmo. A throwing hook is ignored.</summary>
		private static void ModifyForAmmo(SoldierNPC s, Item weapon, Item ammo, ref int projType, ref float speed, ref float knockback)
		{
			if (!LevyConfig.Get.RealWeaponShoot || s.Dummy == null) return;
			try
			{
				var dmg = StatModifier.Default;
				int t = projType; float sp = speed, kb = knockback;
				ItemLoader.PickAmmo(weapon, ammo, s.Dummy, ref t, ref sp, ref dmg, ref kb);
				if (t > 0) projType = t;
				if (sp > 0f) speed = sp;
				knockback = kb;
			}
			catch (Exception) { }
		}

		private static bool FireMagic(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			int cost = Math.Max(0, item.mana);
			if (s.Record.Mana < cost) return false;
			int projType = item.shoot;
			if (projType <= 0) return Dud(s, item);
			// Flying Knife: one per soldier. While it is out the soldier just keeps aiming, like a player holding the knife at the cursor.
			if (projType == ProjectileID.FlyingKnife && SoldierProjectileTag.SoldierHasLiveKnife(s.NPC.whoAmI)) return Dud(s, item);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			float speed = item.shootSpeed > 0f ? item.shootSpeed : LevyConstants.SwordBeamSpeedFallback;
			Vector2 muzzle = Muzzle(s);
			Vector2 vel = AimShot(s, muzzle, target, projType, speed, -1);
			ItemShots.PreShoot(s, projType); // vanilla's kill-the-previous-one rule (Rainbow Gun) runs before any shot path spawns
			bool fired = TryRealShoot(s, item, profile, target, 0, muzzle, vel, projType, damage, item.knockBack);
			if (!fired && !_altActive) fired = FirePlain(s, item, profile, target, muzzle, vel, projType, damage, item.knockBack);
			if (!fired) return Dud(s, item); // nothing came out: keep the mana, hold
			s.Record.Mana -= cost;
			FinishUse(s, item, vel);
			return true;
		}

		/// <summary>
		/// The manual shot, for when the real Shoot pipeline is off or spawned nothing: a sky-drop weapon rains onto the target (<see cref="SkyDrop"/>), anything else fires the profile's
		/// (multishot) volley from the muzzle. Returns whether something spawned.
		/// </summary>
		private static bool FirePlain(SoldierNPC s, Item item, WeaponProfile profile, NPC target, Vector2 muzzle, Vector2 vel, int projType, int damage, float knockback) =>
			SkyDrop.TryFire(s, item, profile, target, muzzle, vel, projType, damage, knockback) || FireSpread(s, muzzle, vel, projType, damage, knockback, profile) > 0;

		/// <summary>Fires the manual (multishot) volley and returns how many projectiles actually spawned.</summary>
		private static int FireSpread(SoldierNPC s, Vector2 muzzle, Vector2 vel, int projType, int damage, float knockback, WeaponProfile profile)
		{
			int n = profile.ProjectilesPerShot, spawned = 0;
			float spread = profile.SpreadDeg;
			for (int k = 0; k < n; k++)
			{
				float deg = n > 1 ? -spread * 0.5f + spread * k / (n - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				if (Spawn(s, muzzle, v, projType, damage, knockback, profile.DamageType) != null) spawned++;
			}
			return spawned;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Real weapon Shoot pipeline
		// ------------------------------------------------------------------------------------------------------------
		private static bool[] _projBefore;

		/// <summary>
		/// The projectile a gun/bow fires with <paramref name="ammo"/>: the ammo's projectile (or the weapon's own), then the weapon-specific
		/// swaps vanilla's Player.PickAmmo applies (Hellwing Bow bats, Shadowflame Bow, launcher/ammo pairings, Molten Quiver...).
		/// Without <see cref="LevyConfig.RealWeaponShoot"/> this is just the ammo's own projectile.
		/// </summary>
		public static int ResolveProjectile(SoldierNPC s, Item weapon, Item ammo)
		{
			bool haveAmmo = ammo != null && !ammo.IsAir;
			int proj;
			// Player.PickAmmo: rocket and solution ammo ADD their shoot offset to the launcher's base projectile; everything else replaces it.
			if (haveAmmo && (weapon.useAmmo == AmmoID.Rocket || weapon.useAmmo == AmmoID.Solution)) proj = weapon.shoot + Math.Max(0, ammo.shoot);
			else proj = haveAmmo && ammo.shoot > 0 ? ammo.shoot : weapon.shoot;
			if (!LevyConfig.Get.RealWeaponShoot || proj <= 0) return proj;
			// PickAmmo_TryFindingSpecificMatches: per-launcher projectile for specific ammo items, falling back to a related launcher
			if (haveAmmo)
			{
				int launcher = weapon.type, match;
				if (AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.TryGetValue(launcher, out var table) && table.TryGetValue(ammo.type, out match)) proj = match;
				else
				{
					int fallback = launcher >= 0 && launcher < AmmoID.Sets.SpecificLauncherAmmoProjectileFallback.Length ? AmmoID.Sets.SpecificLauncherAmmoProjectileFallback[launcher] : -1;
					if (fallback != -1 && AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.TryGetValue(fallback, out var table2) && table2.TryGetValue(ammo.type, out match)) proj = match;
				}
			}
			// Sandgun: sand ammo maps to a bespoke projectile (never the falling-sand block that would place tiles).
			if (haveAmmo && proj == ProjectileID.SandBallGun)
			{
				var sand = ammo.type >= 0 && ammo.type < ItemID.Sets.SandgunAmmoProjectileData.Length ? ItemID.Sets.SandgunAmmoProjectileData[ammo.type] : null;
				if (sand != null) proj = sand.ProjectileType;
			}
			switch (weapon.type)
			{
				case ItemID.HellwingBow: if (proj == ProjectileID.WoodenArrowFriendly) proj = ProjectileID.Hellwing; break;
				case ItemID.MoltenFury: if (proj == ProjectileID.WoodenArrowFriendly) proj = ProjectileID.FireArrow; break;
				case ItemID.SniperRifle:
				case ItemID.VenusMagnum:
				case ItemID.Uzi: if (proj == ProjectileID.Bullet) proj = ProjectileID.BulletHighVelocity; break;
				case ItemID.ShadowFlameBow: proj = ProjectileID.ShadowFlameArrow; break;
				case ItemID.FairyQueenRangedItem: if (proj == ProjectileID.WoodenArrowFriendly) proj = ProjectileID.FairyQueenRangedItemShot; break;
				case ItemID.BloodRainBow: proj = ProjectileID.BloodArrow; break;
				case ItemID.SkeletonBow: if (proj == ProjectileID.BoneArrowFromMerchant) proj = ProjectileID.BoneArrow; break;
				case ItemID.BeesKnees: if (proj == ProjectileID.WoodenArrowFriendly) proj = ProjectileID.BeeArrow; break;
				case ItemID.Marrow: proj = ProjectileID.BoneArrow; break;
				case ItemID.IceBow: proj = ProjectileID.FrostArrow; break;
				case ItemID.PulseBow: proj = ProjectileID.PulseBolt; break;
			}
			if (s?.Dummy != null && s.Dummy.hasMoltenQuiver && proj == ProjectileID.WoodenArrowFriendly) proj = ProjectileID.FireArrow;
			return proj;
		}

		/// <summary>
		/// Fires through the weapon's own hooks: CombinedHooks.ModifyShootStats then CombinedHooks.Shoot (ModItem/GlobalItem/ModPlayer Shoot) with the
		/// soldier's dummy as the player, exactly the pair Player.ItemCheck_Shoot calls. Projectiles the hooks spawned are re-owned and tagged.
		/// When the hooks leave the default shot to us (they return true) the weapon's vanilla special (Daedalus Stormbow rain, Blood Rain Bow) or the
		/// profile's multishot spread is fired with the hook-modified stats. Returns false when nothing was fired and the caller should use the
		/// plain manual path: feature disabled, no dummy, or the hooks returned false without spawning anything.
		/// </summary>
		private static bool TryRealShoot(SoldierNPC s, Item item, WeaponProfile profile, NPC target, int ammoItemId, Vector2 muzzle, Vector2 vel, int projType, int damage, float knockback)
		{
			if (!LevyConfig.Get.RealWeaponShoot) return false;
			Player d = s.Dummy;
			if (d == null || item == null || item.IsAir) return false;

			bool[] before = _projBefore ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;

			int savedWho = d.whoAmI, savedDir = d.direction; Vector2 savedCenter = d.Center; bool savedUse = d.controlUseItem;
			Vector2 pos = muzzle, v = vel; int type = projType, dmg = damage; float kb = knockback;
			bool useDefault = false, threw = false;
			try
			{
				// Hooks spawn their projectiles with player.whoAmI as the owner: make that the soldier owner so ai[] is assigned and the spawn is synced.
				d.whoAmI = OwnerFor(s);
				d.Center = s.NPC.Center;
				d.direction = s.NPC.direction == 0 ? 1 : s.NPC.direction;
				if (!ReferenceEquals(d.inventory[0], item)) d.inventory[0] = item;
				d.selectedItem = 0;
				CombinedHooks.ModifyShootStats(d, item, ref pos, ref v, ref type, ref dmg, ref kb);
				// Hold-to-fire weapons (modded channeled guns) only spawn their shot while the owner's use button is down: the soldier is firing.
				d.controlUseItem = true;
				var source = new EntitySource_ItemUse_WithAmmo(d, item, ammoItemId);
				useDefault = CombinedHooks.Shoot(d, item, source, pos, v, type, dmg, kb);
			}
			catch (Exception) { threw = true; }
			finally { d.whoAmI = savedWho; d.direction = savedDir; d.Center = savedCenter; d.controlUseItem = savedUse; }

			DamageClass dmgType = profile.Family == WeaponFamily.Broadsword ? (item.DamageType ?? DamageClass.Melee) : profile.DamageType;
			int adopted = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || before[i]) continue;
				TagSoldierProjectile(s, p, dmgType);
				adopted++;
			}
			// A hook that threw may already have fired: keep what it spawned rather than firing a second shot on top of it.
			if (threw) return adopted > 0;
			if (!useDefault) return adopted > 0;

			DefaultShoot(s, item, profile, target, pos, v, type, dmg, kb);
			return true;
		}

		/// <summary>The shot Player.ItemCheck_Shoot makes after the hooks return true: a sky-drop weapon's rain, else the weapon's other hard-coded vanilla special, else the (multishot) default.</summary>
		private static void DefaultShoot(SoldierNPC s, Item item, WeaponProfile profile, NPC target, Vector2 pos, Vector2 vel, int type, int damage, float knockback)
		{
			if (SkyDrop.TryFire(s, item, profile, target, pos, vel, type, damage, knockback)) return;
			if (ItemShots.TryShoot(s, item, profile, target, pos, vel, type, damage, knockback)) return;
			FireSpread(s, pos, vel, type, damage, knockback, profile);
		}

		/// <summary>Lead, ballistics and grade error for a shot of <paramref name="projType"/>. Homing ammo flies straight at the target.</summary>
		internal static Vector2 AimShot(SoldierNPC s, Vector2 muzzle, NPC target, int projType, float speed, int ammoType)
		{
			Vector2 targetPos = target.Center;
			Vector2 targetVel = target.velocity;
			bool homing = ammoType >= 0 && LevyConstants.HomingAmmo.Contains(ammoType);
			Vector2 vel;
			Vector2 lead = Vector2.Zero;
			if (homing)
			{
				Vector2 d = targetPos - muzzle; if (d == Vector2.Zero) d = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
				vel = Vector2.Normalize(d) * speed;
			}
			else
			{
				bool gravity = WeaponClassifier.ProjectileHasGravity(projType);
				// The projectile advances (1 + extraUpdates) times per tick (High Velocity Bullet: extraUpdates 7 = 8x), so the speed the lead must use is the spawn
				// velocity times that, not the spawn velocity itself (which stays the shot's real magnitude). Gravity runs per update too: g x (1+e)^2 per tick^2, delay / (1+e) ticks.
				float steps = 1f + Math.Max(0, WeaponClassifier.ProjectileSample(projType)?.extraUpdates ?? 0);
				float leadSpeed = Math.Max(0.01f, speed) * steps;
				vel = Aiming.SolveIntercept(muzzle, leadSpeed, targetPos, targetVel, gravity,
					gravity ? LevyConstants.ArrowGravity * steps * steps : 0f, gravity ? (int)Math.Round(LevyConstants.ArrowGravityDelayTicks / steps) : 0) / steps;
				float t = MathHelper.Clamp(Vector2.Distance(muzzle, targetPos) / leadSpeed, 0f, LevyConstants.AimMaxLeadTicks);
				lead = targetVel * t;
			}
			GradeError(s, muzzle, targetPos, out float jitter, out float leadErr);
			return Aiming.ApplyError(vel, jitter, leadErr, lead, muzzle, targetPos);
		}

		/// <summary>
		/// The soldier's aim error for a shot at <paramref name="targetPos"/> from <paramref name="from"/>: the grade's angle jitter (degrees) and lead error (fraction of the lead), scaled by
		/// <see cref="AimErrorMultiplier"/> and eased at long range. Feed both to <see cref="Aiming.ApplyError(Vector2, float, float, Vector2, Vector2, Vector2)"/>.
		/// </summary>
		internal static void GradeError(SoldierNPC s, Vector2 from, Vector2 targetPos, out float jitterDeg, out float leadError)
		{
			float errMult = AimErrorMultiplier(s) * Aiming.LongRangeErrorScale(Vector2.Distance(from, targetPos));
			int grade = Math.Clamp((int)s.Grade, 0, LevyConstants.GradeAimJitterDeg.Length - 1);
			jitterDeg = LevyConstants.GradeAimJitterDeg[grade] * errMult;
			leadError = LevyConstants.GradeLeadError[grade] * errMult;
		}

		/// <summary>Veteran ×0.6, Shaken ×2, militia with A Guide to Drill ×0.75, any soldier with A Guide to Discipline ×0.65, and every on-duty officer whose accuracy reach covers him (the Arms Dealer ×0.6, the Guide ×0.75 — see <see cref="OfficerSupport"/>). Routed (×3) is applied on top via Aiming.ExtraErrorScale.</summary>
		public static float AimErrorMultiplier(SoldierNPC s)
		{
			float m = 1f;
			if (s.Record != null && s.Record.Veteran) m *= LevyConstants.VeteranAimMult;
			if (s.Shaken) m *= LevyConstants.ShakenAimMult;
			if (s.IsMilitia && SettlementSystem.Instance != null && SettlementSystem.Instance.BookDrill) m *= LevyConstants.DrillMilitiaAimMult;
			if (SettlementSystem.Instance != null && SettlementSystem.Instance.BookDiscipline) m *= LevyConstants.DisciplineAimErrorMult;
			m *= OfficerSupport.AimErrorFactor(s); // exactly 1 with no officer in reach, so an army without them aims as it always did
			return m;
		}

		/// <summary>Whether this shot uses up a piece of ammo: not for endless (non-consumable) ammo, not within reach of an Arms Dealer (bottomless), and not when a hook or the weapon's own rules spare it.</summary>
		private static bool ConsumesAmmo(SoldierNPC s, Item weapon, Item pouch)
		{
			if (!pouch.consumable) return false;
			if (OfficerSupport.AmmoIsFree(s)) return false; // an Arms Dealer stands close: the pouch never runs down
			if (AmmoFreeThisShot(s, weapon, pouch)) return false;
			if (s.Dummy != null)
			{
				try { CombinedHooks.OnConsumeAmmo(s.Dummy, weapon, pouch); } catch (Exception) { }
			}
			return true;
		}

		/// <summary>Ammo conservation from the dummy's equipment flags (Player.IsAmmoFreeThisShot odds).</summary>
		private static bool AmmoFreeThisShot(SoldierNPC s, Item weapon, Item ammo)
		{
			Player d = s.Dummy;
			if (d == null) return false;
			bool free = false;
			try { if (!CombinedHooks.CanConsumeAmmo(d, weapon, ammo)) return true; } catch (Exception) { }
			if (weapon.consumeAmmoOnFirstShotOnly && s.ItemAnimation > 0) return true;
			if (weapon.consumeAmmoOnLastShotOnly && s.ItemAnimation > Math.Max(1, weapon.useTime)) return true;
			if (d.huntressAmmoCost90 && Main.rand.NextBool(LevyConstants.AmmoConserve90Denominator)) free = true;
			if (d.chloroAmmoCost80 && Main.rand.NextBool(LevyConstants.AmmoConserve80Denominator)) free = true;
			if (d.ammoCost80 && Main.rand.NextBool(LevyConstants.AmmoConserve80Denominator)) free = true;
			if (d.ammoCost75 && Main.rand.NextBool(LevyConstants.AmmoConserve75Denominator)) free = true;
			if (d.magicQuiver && weapon.useAmmo == AmmoID.Arrow && Main.rand.NextBool(LevyConstants.MagicQuiverDenominator)) free = true;
			return free;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Thrown consumables (knives, shuriken, javelins, grenades, bombs, dynamite): the weapon slot holds the STACK and each throw uses one up.
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>
		/// Throws one item from the soldier's stack at the target: the item's own projectile, lobbed on a simulated arc (<see cref="ThrownAim"/>), owned by the
		/// commander and tagged like every soldier shot. The projectile's own AI then does the rest (grenades and bombs blast, the poisoned knife poisons...).
		/// Vanilla's Shoot hooks run first (<see cref="TryRealShoot"/>) so modded throwables keep their special shots. The stack drops by one; at zero the slot
		/// empties, the type is remembered for the armory's restock, and the soldier barks that he is out.
		/// </summary>
		private static bool ThrowConsumable(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (item.stack <= 0 || item.shoot <= 0) return Dud(s, item);
			int projType = item.shoot;
			Vector2 muzzle = Muzzle(s);
			Vector2 vel = AimThrow(s, muzzle, target, projType, item.shootSpeed);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier)); // a bomb item has no damage: the blast writes its own (PrepareBombToBlow)
			bool fired = TryRealShoot(s, item, profile, target, 0, muzzle, vel, projType, damage, item.knockBack);
			if (!fired) fired = FireSpread(s, muzzle, vel, projType, damage, item.knockBack, profile) > 0;
			if (!fired) return Dud(s, item); // nothing came out: keep the stack, hold

			FinishUse(s, item, vel); // before the stack can empty: the cooldown and use sound come from the item
			if (ConsumesThrown(s, item))
			{
				item.stack--;
				if (item.stack <= 0)
				{
					s.Record.Gear.ThrownRestockType = item.type;
					item.TurnToAir();
					DialogueSystem.Bark(s, DialogueTrigger.OUT_OF_AMMO);
				}
				s.NPC.netUpdate = true;
			}
			return true;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Reusable melee throwers (Daybreak, Shadowflame Knife, Vampire Knives): thrown again and again, nothing is used up.
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>
		/// Throws the weapon's shot at the target on a simulated arc (<see cref="ThrownAim"/>), owned by the commander and tagged like every soldier shot; the item is never touched.
		/// Daybreak (636) and the Shadowflame Knife (497) are not owner-anchored, so their REAL shots go out through <see cref="TryRealShoot"/> and just work.
		/// The Vampire Knife (304) is special-cased: vanilla heals Main.player[Main.myPlayer] (the commander) from <c>Projectile.vampireHeal</c> whenever a type-304 shot hits,
		/// so a soldier throws Levy's own <see cref="SoldierVampireKnife"/> instead, which heals the SOLDIER. Cadence is the item's useTime / reuseDelay (see <see cref="FinishUse"/>).
		/// </summary>
		private static bool ThrowMelee(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (item.shoot <= 0) return Dud(s, item);
			int projType = item.shoot;
			Vector2 muzzle = Muzzle(s);
			float speed = item.shootSpeed > 0f ? item.shootSpeed : LevyConstants.SwordBeamSpeedFallback;
			Vector2 vel = AimThrowMelee(s, muzzle, target, projType, speed);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			bool fired;
			if (projType == ProjectileID.VampireKnife) fired = FireVampireKnives(s, muzzle, vel, damage, item.knockBack) > 0;
			else
			{
				fired = TryRealShoot(s, item, profile, target, 0, muzzle, vel, projType, damage, item.knockBack);
				if (!fired) fired = FireSpread(s, muzzle, vel, projType, damage, item.knockBack, profile) > 0;
			}
			if (!fired) return Dud(s, item); // nothing came out: hold
			FinishUse(s, item, vel);
			return true;
		}

		/// <summary>The flight model of a melee thrower's shot, from its real AI (not the consumable-knife default), and the fraction of a tick one AI update lasts (a shot with extra updates flies several updates per tick).</summary>
		private static LevyConstants.ThrownFlight MeleeThrowFlight(int projType, out float tickPerUpdate)
		{
			tickPerUpdate = 1f / (1 + Math.Max(0, WeaponClassifier.ProjectileSample(projType)?.extraUpdates ?? 0));
			switch (projType)
			{
				case ProjectileID.VampireKnife: return LevyConstants.ThrownMeleeFlightStraight;
				case ProjectileID.ShadowFlameKnife: return LevyConstants.ThrownMeleeFlightShadowflameKnife;
				case ProjectileID.Daybreak: return LevyConstants.ThrownMeleeFlightDaybreak;
				default: return ThrownAim.FlightFor(projType);
			}
		}

		/// <summary>Launch velocity (per AI update) for a melee thrower's shot, with the same grade / veteran / shaken aim error every ranged soldier gets.</summary>
		private static Vector2 AimThrowMelee(SoldierNPC s, Vector2 muzzle, NPC target, int projType, float shootSpeed)
		{
			LevyConstants.ThrownFlight flight = MeleeThrowFlight(projType, out float tickPerUpdate);
			// The solver steps once per AI update, so a shot with extra updates sees the target move only a fraction of its per-tick velocity between steps.
			Vector2 vel = ThrownAim.Solve(muzzle, target.Center, target.velocity * tickPerUpdate, flight, shootSpeed);
			float errMult = AimErrorMultiplier(s) * Aiming.LongRangeErrorScale(Vector2.Distance(muzzle, target.Center));
			int grade = Math.Clamp((int)s.Grade, 0, LevyConstants.GradeAimJitterDeg.Length - 1);
			return Aiming.ApplyError(vel, LevyConstants.GradeAimJitterDeg[grade] * errMult, 0f, Vector2.Zero);
		}

		/// <summary>
		/// A fan of Levy-owned Vampire Knives, vanilla's count and spread (4 knives, +1 on 1/2, 1/4, 1/8 and 1/16 rolls; knife k scatters 0.05 * k * U(-35, 35) px/tick per axis).
		/// Each is owned by the commander and tagged to this soldier like every soldier shot; none is a type-304 projectile, so vanilla's commander lifesteal never runs.
		/// </summary>
		private static int FireVampireKnives(SoldierNPC s, Vector2 muzzle, Vector2 vel, int damage, float knockback)
		{
			int count = LevyConstants.VampKnifeBaseCount;
			foreach (int denominator in LevyConstants.VampKnifeExtraRolls) if (Main.rand.NextBool(denominator)) count++;
			int type = ModContent.ProjectileType<SoldierVampireKnife>();
			float speed = vel.Length();
			int spawned = 0;
			int range = LevyConstants.VampKnifeSpreadRange;
			for (int k = 0; k < count; k++)
			{
				float spread = LevyConstants.VampKnifeSpreadStep * k;
				Vector2 v = vel + new Vector2(Main.rand.Next(-range, range + 1), Main.rand.Next(-range, range + 1)) * spread;
				v = v.SafeNormalize(vel) * speed;
				if (Spawn(s, muzzle, v, type, damage, knockback, DamageClass.Melee, s.NPC.whoAmI) != null) spawned++;
			}
			return spawned;
		}

		/// <summary>Launch velocity for a throw: the ballistic lob, then the same grade / veteran / shaken aim error every ranged soldier gets (angle only: a lob has no lead to mis-judge).</summary>
		private static Vector2 AimThrow(SoldierNPC s, Vector2 muzzle, NPC target, int projType, float shootSpeed)
		{
			Vector2 vel = ThrownAim.Solve(muzzle, target.Center, target.velocity, ThrownAim.FlightFor(projType), shootSpeed);
			float errMult = AimErrorMultiplier(s) * Aiming.LongRangeErrorScale(Vector2.Distance(muzzle, target.Center));
			int grade = Math.Clamp((int)s.Grade, 0, LevyConstants.GradeAimJitterDeg.Length - 1);
			return Aiming.ApplyError(vel, LevyConstants.GradeAimJitterDeg[grade] * errMult, 0f, Vector2.Zero);
		}

		/// <summary>Whether this throw uses up an item: the Ninja armor's thrown-cost saving (on the dummy) and the item's own ConsumeItem hooks can spare it, as in Player.ItemCheck.</summary>
		private static bool ConsumesThrown(SoldierNPC s, Item item)
		{
			Player d = s.Dummy;
			if (d != null && item.CountsAsClass(DamageClass.Throwing))
			{
				if (d.ThrownCost50 && Main.rand.Next(100) < 50) return false;
				if (d.ThrownCost33 && Main.rand.Next(100) < 33) return false;
			}
			if (d != null)
			{
				try { if (!ItemLoader.ConsumeItem(item, d)) return false; } catch (Exception) { }
			}
			return true;
		}

		/// <summary>
		/// Called every sidearm check from the combat tick. A soldier with an empty thrown slot (the type is remembered), or a stack under
		/// <see cref="LevyConstants.ThrownTopUpBelowFraction"/> of the carry limit, asks the armory he is standing at (within
		/// <see cref="LevyConstants.TopUpArmoryTiles"/>) to restock it, at most every <see cref="LevyConstants.ThrownRestockIntervalTicks"/>.
		/// </summary>
		public static void ThrownRestockTick(SoldierNPC s)
		{
			var g = s?.Record?.Gear;
			if (g == null) return;
			bool empty = !g.HasWeapon && g.ThrownRestockType > 0;
			bool low = !empty && g.HasWeapon && g.Weapon.stack < LevyConstants.ThrownCarryMax * LevyConstants.ThrownTopUpBelowFraction && WeaponClassifier.Classify(g.Weapon) == WeaponFamily.Thrown;
			if (!empty && !low) return;
			uint now = Main.GameUpdateCount;
			if (now - g.ThrownCheckTick < LevyConstants.ThrownRestockIntervalTicks) return;
			g.ThrownCheckTick = now;
			var armory = SettlementSystem.Instance?.NearestArmory(s.NPC.Center, LevyConstants.TopUpArmoryTiles);
			if (armory == null || !Terraria.DataStructures.TileEntity.ByID.TryGetValue(armory.ID, out var live) || !ReferenceEquals(live, armory)) return;
			if (armory.RestockThrown(s.Record)) s.NotifyGearChanged();
		}

		/// <summary>
		/// Automatic weapon upgrade (the body only ticks on the server / single player). Every <see cref="LevyConstants.AutoUpgradeCheckIntervalTicks"/> a soldier
		/// asks his home barracks' linked armory for a clearly better weapon (any damage class unless fixed-class companies is on) or armor set aimed at it (<see cref="ArmoryTE.UpgradeWeapons"/>); after a swap he waits
		/// <see cref="LevyConstants.AutoUpgradeSwapCooldownTicks"/>. Skipped when switched off in the config, while the soldier's loadout is locked, and mid-fight or mid-swing.
		/// </summary>
		public static void WeaponUpgradeTick(SoldierNPC s)
		{
			var rec = s?.Record;
			var g = rec?.Gear;
			if (g == null || rec.LoadoutLocked || !Common.Config.LevyConfig.Get.AutoUpgradeWeapons) return;
			uint now = Main.GameUpdateCount;
			if (g.UpgradeCheckTick == 0) { g.UpgradeCheckTick = now + (uint)rec.Id % (uint)LevyConstants.AutoUpgradeCheckIntervalTicks; return; } // stagger by id
			if (now < g.UpgradeCheckTick) return;
			if (s.State == SoldierState.Engage || s.State == SoldierState.Shaken || s.State == SoldierState.Routed || s.ItemAnimation > 0) return; // look again at the next sidearm check
			g.UpgradeCheckTick = now + (uint)LevyConstants.AutoUpgradeCheckIntervalTicks;
			var armory = s.Home == null ? null : SettlementSystem.Instance?.LinkedArmory(s.Home);
			if (armory == null || !Terraria.DataStructures.TileEntity.ByID.TryGetValue(armory.ID, out var live) || !ReferenceEquals(live, armory)) return;
			if (!armory.UpgradeWeapons(rec)) return;
			g.UpgradeCheckTick = now + (uint)LevyConstants.AutoUpgradeSwapCooldownTicks;
			s.NotifyGearChanged(); // the periodic roster snapshot carries the new kit to clients
		}

		// ------------------------------------------------------------------------------------------------------------
		// Melee
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>Starts a broadsword swing; the per-tick hitbox runs in SoldierNPC.Combat. Launches the sword beam if the item has one.</summary>
		private static bool StartSwing(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			Vector2 toTarget = target.Center - s.NPC.Center;
			if (toTarget == Vector2.Zero) toTarget = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			// Held slash projectiles (Night's Edge family) are drawn around the commander's body, not flown: the swing hitbox is the weapon.
			if (item.shoot > 0 && !WeaponClassifier.IsHeldSlashProjectile(item.shoot))
			{
				float speed = item.shootSpeed > 0f ? item.shootSpeed : LevyConstants.SwordBeamSpeedFallback;
				int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
				if (item.type == ItemID.IceSickle) damage = Math.Max(1, (int)(damage * 0.67)); // Player.ItemCheck_Shoot: totalDamage * 0.67
				Vector2 vel = Aiming.SolveIntercept(s.NPC.Center, speed, target.Center, target.velocity, WeaponClassifier.ProjectileHasGravity(item.shoot), LevyConstants.ArrowGravity, LevyConstants.ArrowGravityDelayTicks);
				DamageClass beamType = item.DamageType ?? DamageClass.Melee;
				if (!TryRealShoot(s, item, profile, target, 0, s.NPC.Center, vel, item.shoot, damage, item.knockBack)
					&& !SkyDrop.TryFire(s, item, profile, target, s.NPC.Center, vel, item.shoot, damage, item.knockBack))
					Spawn(s, s.NPC.Center, vel, item.shoot, damage, item.knockBack, beamType);
			}
			// Night's Edge family: the held slash stays a swing hitbox; the one beam that flies on its own velocity (Terra Blade) is launched from the soldier.
			// The IsHeldSlashProjectile guard above also keeps item.shoot (Terra Blade's 985) from firing a second, commander-bound shot.
			SwordBeams.TryFire(s, item, profile, target);
			s.BeginSwing();
			FinishUse(s, item, toTarget);
			return true;
		}

		/// <summary>Spawns the shared thrust projectile unless this soldier's thrust is still out.</summary>
		private static bool StartThrust(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (s.HasActiveThrust()) return false;
			Vector2 dir = target.Center - s.NPC.Center;
			if (dir == Vector2.Zero) dir = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			dir = Vector2.Normalize(dir);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			int life = Math.Max(1, item.useAnimation);
			Projectile p = Spawn(s, s.NPC.Center, dir, ModContent.ProjectileType<SoldierThrust>(), damage, item.knockBack, item.DamageType ?? DamageClass.Melee, s.NPC.whoAmI, item.type, life);
			if (p == null) { s.AttackCooldown = Math.Max(1, item.useTime); return false; } // projectile array full: wait a use before rescanning
			p.timeLeft = Math.Max(p.timeLeft, life);
			s.NoteThrust(p.whoAmI);
			FinishUse(s, item, dir);
			return true;
		}

		/// <summary>Lash period in ticks: close to the weapon's useTime (a little brisker), clamped. The lash lives for the whole period, so the next one follows immediately.</summary>
		public static int WhipPeriod(Item item) => Math.Clamp((int)Math.Round(Math.Max(1, item.useTime) * LevyConstants.WhipCadenceMult), LevyConstants.WhipCadenceMinTicks, LevyConstants.WhipCadenceMaxTicks);

		/// <summary>Spawns a lash toward the target unless this soldier's lash is still out. Reach grows with the dummy's whip range bonus.</summary>
		private static bool StartWhip(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			int type = ModContent.ProjectileType<SoldierWhip>();
			if (s.CountAnchored<SoldierWhip>() > 0) { s.AttackCooldown = 1; return false; } // the old lash is on its last tick: lash again the moment it clears
			Vector2 dir = target.Center - s.NPC.Center;
			if (dir == Vector2.Zero) dir = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			dir = Vector2.Normalize(dir);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier * LevyConstants.WhipDamageMult));
			int period = WhipPeriod(item);
			int life = Math.Max(LevyConstants.WhipCadenceMinTicks - 1, period - 1);
			float mult = s.Dummy != null && s.Dummy.whipRangeMultiplier > 0.5f ? s.Dummy.whipRangeMultiplier : 1f;
			Projectile p = Spawn(s, s.NPC.Center, dir, type, damage, item.knockBack, item.DamageType ?? DamageClass.SummonMeleeSpeed, s.NPC.whoAmI, item.type, SoldierWhip.PackAi2(life, mult));
			if (p == null) { s.AttackCooldown = Math.Max(1, period); return false; }
			p.timeLeft = Math.Max(p.timeLeft, life + 2);
			FinishUse(s, item, dir);
			s.AttackCooldown = period; // brisk: not the full useTime
			s.ItemAnimation = s.ItemAnimationMax = period;
			return true;
		}

		/// <summary>How many of this boomerang one soldier may have out at once: the per-weapon override (Bananarang), else the shared cap.</summary>
		public static int BoomerangCap(Item item) => item != null && LevyConstants.BoomerangMaxOutByItem.TryGetValue(item.type, out int n) ? n : LevyConstants.BoomerangMaxOut;

		/// <summary>Throws the soldier's boomerang at the target unless the allowed number are already out.</summary>
		private static bool ThrowBoomerang(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (s.CountAnchored<SoldierBoomerang>() >= BoomerangCap(item)) { s.AttackCooldown = 6; return false; }
			if (item.shoot <= 0) return Dud(s, item);
			float speed = MathHelper.Clamp(item.shootSpeed > 0f ? item.shootSpeed : LevyConstants.BoomerangMinSpeed, LevyConstants.BoomerangMinSpeed, LevyConstants.BoomerangMaxSpeed);
			Vector2 muzzle = Muzzle(s);
			Vector2 vel = AimShot(s, muzzle, target, item.shoot, speed, -1);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			Projectile p = Spawn(s, muzzle, vel, ModContent.ProjectileType<SoldierBoomerang>(), damage, item.knockBack, item.DamageType ?? DamageClass.Melee, s.NPC.whoAmI, item.type, 0f);
			if (p == null) return Dud(s, item);
			FinishUse(s, item, vel);
			return true;
		}

		/// <summary>Sends the soldier's yoyo out at the target unless one of its yoyos is already out. The yoyo is a Levy-owned SoldierYoyo: the vanilla held yoyo is never spawned.</summary>
		private static bool ThrowYoyo(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (s.CountAnchored<SoldierYoyo>() >= LevyConstants.YoyoMaxOut) { s.AttackCooldown = 6; return false; }
			if (item.shoot <= 0) return Dud(s, item);
			Vector2 dir = target.Center - s.NPC.Center;
			if (dir == Vector2.Zero) dir = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			dir = Vector2.Normalize(dir);
			Vector2 muzzle = Muzzle(s);
			if (!Aiming.HasLineOfSight(muzzle, target.Center)) { s.AttackCooldown = 10; return false; } // a yoyo does not fly through walls: wait for a clear line
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			Projectile p = Spawn(s, muzzle, dir * LevyConstants.YoyoMaxSpeed, ModContent.ProjectileType<SoldierYoyo>(), damage, item.knockBack, item.DamageType ?? DamageClass.Melee, s.NPC.whoAmI, item.type, 0f);
			if (p == null) return Dud(s, item);
			FinishUse(s, item, dir);
			return true;
		}

		/// <summary>Swings the soldier's flail out at the target unless one of its flails is already out. The ball is a Levy-owned SoldierFlail: the vanilla held flail is never spawned.</summary>
		private static bool ThrowFlail(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (s.CountAnchored<SoldierFlail>() >= LevyConstants.FlailMaxOut) { s.AttackCooldown = 4; return false; }
			if (item.shoot <= 0) return Dud(s, item);
			Vector2 dir = target.Center - s.NPC.Center;
			if (dir == Vector2.Zero) dir = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			dir = Vector2.Normalize(dir);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			int life = Math.Clamp(item.useAnimation, LevyConstants.FlailLifeMinTicks, LevyConstants.FlailLifeMaxTicks);
			Projectile p = Spawn(s, s.NPC.Center, dir, ModContent.ProjectileType<SoldierFlail>(), damage, item.knockBack, item.DamageType ?? DamageClass.Melee, s.NPC.whoAmI, item.type, life);
			if (p == null) return Dud(s, item);
			p.timeLeft = Math.Max(p.timeLeft, life + 2);
			FinishUse(s, item, dir);
			return true;
		}

		/// <summary>Cooldown, animation, aim rotation, facing and use sound shared by every family.</summary>
		private static void FinishUse(SoldierNPC s, Item item, Vector2 aimVel)
		{
			// A running animation keeps going (burst weapons fire several shots per animation, one every useTime); a new use starts a fresh one.
			if (s.ItemAnimation <= 0) { s.ItemAnimation = s.ItemAnimationMax = Math.Max(1, item.useAnimation); s.UsesThisAnimation = 0; }
			s.UsesThisAnimation++;
			s.AttackCooldown = Math.Max(1, item.useTime);
			// useLimitPerAnimation caps the uses per animation (Player.ItemCheck_OwnerOnlyCode; the Tome of Infinite Wisdom fires 4 shots then waits out the rest of its 25-tick animation):
			// once spent, the weapon is silent until the animation ends, then reuseDelay (applied by Combat_Update) adds its pause.
			if (item.useLimitPerAnimation.HasValue && s.UsesThisAnimation >= item.useLimitPerAnimation.Value) s.AttackCooldown = Math.Max(s.AttackCooldown, s.ItemAnimation);
			s.ReuseDelayPending = Math.Max(0, item.reuseDelay);
			if (aimVel != Vector2.Zero && !aimVel.HasNaNs())
			{
				s.AimRotation = aimVel.ToRotation();
				if (Math.Abs(aimVel.X) > 0.01f) s.NPC.direction = aimVel.X < 0f ? -1 : 1;
			}
			if (item.UseSound.HasValue) SoundEngine.PlaySound(item.UseSound.Value, s.NPC.Center);
			s.NPC.netUpdate = true;
		}
	}
}
