using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// What KIND of owner-anchored shot a modded weapon makes, decided from the weapon's own compiled code and its projectile's class chain (never from an item name).
	/// Soldier shots are owned by the commander (or, on a dedicated server, by the null player in slot 255), so a projectile whose AI is written around the player holding it (reading its owner's
	/// position, <c>channel</c> button, cursor and held item) does nothing useful as a plain shot: a charge-and-release staff expires uncharged the moment it spawns, a held shortsword / mace flail sits
	/// at the commander (or dies at once on slot 255) and a plain-shooter soldier stands 46 tiles away from a blade that reaches 5. Each kind maps to the soldier-side family that really delivers it:
	///  * <see cref="ModShotKind.Holdout"/>: a modded projectile that reads <c>Player.channel</c>. The soldier HOLDS it like a player (<see cref="HeldWeapons"/>, dummy in slot 255, cursor on the target) and lets go after a charge.
	///  * <see cref="ModShotKind.Shortsword"/>: a Calamity <c>BaseShortswordProjectile</c> blade (a stab that exists for 12 ticks next to its owner): the soldier stabs with Levy's own thrust at blade reach.
	///  Not a kind: a Calamity mace flail (<c>BaseMaceFlailProjectile</c> / <c>BaseFlailProjectile</c>: spun by its owner, thrown at the cursor when the button is let go) also reads the owner's button but is NOT held. The commander never holds the button,
	///  so a plain shot spins through its windup and throws at once; with the cursor on the target (<see cref="SteerToTargetProjectile"/>) the throw lands. Holding it as a melee holdout was measured worse (it stood at 7 tiles, below the throw's reach).
	/// </summary>
	public enum ModShotKind { None, Holdout, Shortsword }

	/// <summary>How long a charge-by-button holdout is held before the button is let go, and how long it lingers after the release.</summary>
	public sealed class ChargePlan { public readonly int HoldTicks, LingerTicks; public ChargePlan(int hold, int linger) { HoldTicks = hold; LingerTicks = linger; } }

	/// <summary>A circle the held cursor traces around the target: radius in px and radians per tick. The phase is the world tick, so a holdout and the children it spawns trace the same circle.</summary>
	public sealed class CursorScript
	{
		public readonly float Radius, Step;
		public CursorScript(float radius, float step) { Radius = radius; Step = step; }
		public Microsoft.Xna.Framework.Vector2 At(Microsoft.Xna.Framework.Vector2 center, uint tick) { float a = tick * Step; return center + new Microsoft.Xna.Framework.Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a)) * Radius; }
	}

	public static class ModShotKinds
	{
		private static readonly Dictionary<int, ModShotKind> _cache = new();
		/// <summary>Measurement switch (harness A/B, env LEVY_NO_MODSHOT=1): behave as before this feature (no holdout / blade routing and no cursor steer for modded shots).</summary>
		internal static readonly bool Off = Environment.GetEnvironmentVariable("LEVY_NO_MODSHOT") == "1";

		/// <summary>
		/// Calamity holdouts that were measured as DUDS (or far weaker) as plain shots and gain from being held (boss lab, docs/boss_lab/dud/holdout_routes.tsv: dmg/s per levy against a x40 Eye, single-player emulation, plain vs held).
		/// A holdout NOT in this table keeps the plain shot: for those the item's Shoot hook / the holdout's first tick already delivers the damage (it fires from the commander's mana pool at full rate), and holding them
		/// measured lower (Nidhogg, Phalanx Surge, Apoctosis Array, Ion Blaster, Ontological Despoiler, Fetid Emesis...), which would be a nerf. Later Calamity weapons default to the plain shot until they are measured.
		/// </summary>
		private static readonly HashSet<string> HoldoutRouted = new()
		{
			"AdamantiteParticleAccelerator", "AetherfluxCannon", "Auger", "BlissfulBombardier", "BurningSea", "Buzzkill", "ChickenCannon", "ChromaticEruption", "ChronomancersScythe", "ClockworkBow",
			"Contagion", "CoralSpout", "CountermeasureMitt", "CrescentMoon", "DaemonsFlame", "DarkSpark", "DeathsAscension", "DevilsSunrise", "DragonRage", "DragonsBreath", "Drataliornus",
			"Exoblade", "FirestormCannon", "FlakKraken", "FlakToxicannon", "FlurrystormCannon", "FreedomStar", "Genesis", "GhastlyVisage", "GruesomeEminence", "HadalUrn", "Hellborn", "Heresy",
			"HolofibreImmolator", "Kingsbane", "Lightspeed", "Megalodon", "MineralMortar", "NanoPurge", "Omicron", "OmniGun", "Omniblade", "Phaseslayer", "Photoviscerator", "PrismaticBreaker",
			"Riftburst", "Roxcalibur", "ScorchedEarth", "Scorpio", "Seadragon", "ShiftingSands", "SlagfireDouser", "SnowstormStaff", "SparklingEmpress", "SpectralstormCannon", "SpineOfThanatos",
			"StreamGouge", "SuperradiantSlaughterer", "Sylvestaff", "TauCannon", "Teslastaff", "TheCauldron", "TheHive", "TheMaelstrom", "Ultima", "UnstableCastersGauntlet", "Violence", "Voidragon",
			"Vulcan", "WildfireBloom", "Wingman",
			// dud2 (2026-11-07, mp=200 so the cast cost is payable: plain -> held dmg/s per levy vs a x40 Eye, SP emulation): GloriousEnd 1.6 -> 102, Volterion 0 -> 648, VitriolicViper 448 -> 1704, HeliumFlash 5135 -> 1218 (seed spread; plain was 0 at the lab pool: see the mana note), NebulousCataclysm, RainbowPartyCannon, StaffofBlushie 0 -> 561.
			"GloriousEnd", "HeliumFlash", "NebulousCataclysm", "RainbowPartyCannon", "StaffofBlushie", "VitriolicViper", "Volterion",
			// dud2, the holdouts the plain shot cannot run at all (the commander never holds the button, so the holdout ends on its first tick) and that a held chain now carries (children bound to the holdout stay in the chain, the dummy's owned-projectile counts are real, charge / cursor / start-mana tables below):
			// ArtAttack 0 -> ~170 (cursor circle), Rancor 0 -> ~80 (full-pool start, bound circle), TitaniumRailgun 5 -> ~40 (charge, release), GrandDad 0 -> ~70 (melee holdout; the plain one throws NaN from a zero cursor), Basher (same NaN).
			"ArtAttack", "Rancor", "TitaniumRailgun", "GrandDad", "Basher",
			// the UUID-bound holdouts (their children find the holdout through Projectile.GetByUUID, which refuses owner 255): the chain lives in the slot below 255 (HeldWeapons.OwnerSlotFor): Yharim's Crystal 0 -> ~290, Starmageddon 0 -> ~840.
			"YharimsCrystal", "Starmageddon",
		};

		/// <summary>Measurement switch (env LEVY_HOLD_FORCE=Name1,Name2 or "all"): routes these Calamity holdouts through the held path as if they were in <see cref="HoldoutRouted"/> (A/B runs of a weapon that is not routed yet).</summary>
		private static readonly string _force = Environment.GetEnvironmentVariable("LEVY_HOLD_FORCE");
		private static bool ForceHold(string name) => !string.IsNullOrEmpty(_force) && (_force == "all" || ("," + _force + ",").Contains("," + name + ","));

		/// <summary>
		/// Held modded weapons whose damage comes from children bound to the holdout, which the held chain must carry: the dummy's owned-projectile counts are kept real for them (Rancor's circle ends when it counts no holdout), children that read
		/// the button or find the holdout by UUID stay in the chain (<see cref="OwnerLockProbe.BoundToHolder"/>), and a UUID-bound holdout gets the chain slot below 255. Every other routed weapon keeps the chain exactly as it was measured:
		/// its children are handed to the commander like ordinary shots (an earlier all-weapons version of this rule changed Roxcalibur from 5853 to 60 dmg/s per levy).
		/// </summary>
		private static readonly HashSet<string> ChainExtraNames = new() { "ArtAttack", "Rancor", "Starmageddon", "YharimsCrystal" };
		internal static bool ChainExtras(Item item) => item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && ChainExtraNames.Contains(item.ModItem.Name);

		/// <summary>A held modded weapon that deals its damage only when the button is LET GO after a charge: hold for <see cref="HoldTicks"/>, release, and end the holdout <see cref="LingerTicks"/> later so the next one is cast.</summary>
		private static readonly Dictionary<string, ChargePlan> ChargeReleaseTable = new()
		{
			// TitaniumRailgun: the scope charges while the button is down (ai1 = 60 ticks to full charge, 18 minimum) and fires on the release; held without a release it never fires (4.97 dmg/s per levy vs a x40 Eye).
			["TitaniumRailgun"] = new ChargePlan(66, 20),
		};
		internal static ChargePlan ChargeRelease(Item item) => item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && ChargeReleaseTable.TryGetValue(item.ModItem.Name, out ChargePlan p) ? p : null;

		/// <summary>
		/// Held modded weapons whose holdout pays its mana INSIDE its own AI at a rate far above the pool (Rancor: 25 per 12 ticks, Yharim's Crystal: 15 every 5-15 ticks): started the moment one payment is affordable they end at their
		/// second payment, before their damage starts (the circle charges 180 ticks, the prism 30), and are cast again in a loop (Rancor holdouts ended at ticks 20-29 in 82 of 101 casts, Yharim's at 10-19 in 172 of 207).
		/// The soldier starts them with a full pool, like a player, so the hold lasts as long as the pool can carry it. Value = fraction of the soldier's maximum mana needed to start.
		/// </summary>
		private static readonly Dictionary<string, float> StartFractionTable = new() { ["Rancor"] = 1f, ["YharimsCrystal"] = 1f };
		internal static bool StartWait(Item item, int mana, int maxMana)
		{
			if (item?.ModItem == null || item.ModItem.Mod.Name != "CalamityMod" || !StartFractionTable.TryGetValue(item.ModItem.Name, out float f)) return false;
			return mana < Math.Min(maxMana, (int)Math.Ceiling(maxMana * f)) - 1; // one point of slack
		}

		/// <summary>A held modded weapon whose damage depends on WHERE the cursor goes, not only on what it points at: the cursor follows a path around the target.</summary>
		private static readonly Dictionary<string, CursorScript> CursorScripts = new()
		{
			// ArtAttack: the stars follow the cursor and the weapon strikes every enemy inside the closed shape their path draws; a fixed cursor never closes a shape (0 dmg/s per levy). A circle of 140 px closes in about 30 ticks.
			["ArtAttack"] = new CursorScript(140f, 0.22f),
		};
		internal static CursorScript CursorScriptOf(Item item) => item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && CursorScripts.TryGetValue(item.ModItem.Name, out CursorScript c) ? c : null;

		/// <summary>Class-chain names of the Calamity base projectiles whose AI is a held melee stab (matched by name: no compile-time reference to Calamity).</summary>
		private const string ShortswordBase = "BaseShortswordProjectile", MaceFlailBase = "BaseMaceFlailProjectile", FlailBase = "BaseFlailProjectile";

		public static ModShotKind Of(Item item)
		{
			if (Off || item == null || item.IsAir || item.type < ItemID.Count) return ModShotKind.None;
			if (_cache.TryGetValue(item.type, out ModShotKind k)) return k;
			k = ModShotKind.None;
			try { k = Decide(item); }
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: shot-kind probe failed for {item.Name}: {e.Message}"); }
			_cache[item.type] = k;
			return k;
		}

		private static ModShotKind Decide(Item item)
		{
			// every projectile the weapon can spawn: its own shoot plus any it names in its Shoot / UseItem hooks
			var types = new List<Type>();
			Projectile sample = item.shoot > 0 ? WeaponClassifier.ProjectileSample(item.shoot) : null;
			if (sample?.ModProjectile != null) types.Add(sample.ModProjectile.GetType());
			foreach (Type t in OwnerLockProbe.NamedProjectileTypes(item)) if (!types.Contains(t)) types.Add(t);

			bool shortsword = false, mace = false;
			foreach (Type t in types)
				for (Type b = t; b != null && b != typeof(ModProjectile) && b != typeof(object); b = b.BaseType)
				{
					if (b.Name == ShortswordBase) shortsword = true;
					else if (b.Name == MaceFlailBase || b.Name == FlailBase) mace = true;
				}
			// A melee weapon whose shot is a held stab is a melee weapon: it fights at blade reach with Levy's own stab.
			if (shortsword && item.CountsAsClass(DamageClass.Melee)) return ModShotKind.Shortsword;
			if (mace) return ModShotKind.None; // thrown at the cursor, not held (see the class summary)
			// A held / charge-and-release holdout reads its owner's button (and is the weapon's own shot).
			// Only holdouts measured as duds as plain shots are routed (see HoldoutRouted); everything else keeps the plain shot, which already works for it.
			if (item.ModItem?.Mod.Name == "CalamityMod" && (HoldoutRouted.Contains(item.ModItem.Name) || ForceHold(item.ModItem.Name)) && item.channel && item.shoot > 0 && sample?.ModProjectile != null && OwnerLockProbe.ReadsOwnerButton(item.shoot)
				&& (item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Magic) || item.CountsAsClass(DamageClass.Melee)))
				return ModShotKind.Holdout;
			return ModShotKind.None;
		}
	}

	/// <summary>Measurement only (env LEVY_MODSHOT_AUDIT=1): after all content is set up, logs every modded weapon whose shot kind is not None, with the family it gets, so a content mod's whole catalogue can be reviewed for misrouting.</summary>
	public class ModShotAudit : ModSystem
	{
		public override void PostSetupContent()
		{
			if (Environment.GetEnvironmentVariable("LEVY_MODSHOT_AUDIT") != "1") return;
			try
			{
				int n = 0;
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
				{
					Item it = ContentSamples.ItemsByType.TryGetValue(t, out var smp) ? smp : null;
					if (it == null || it.IsAir || it.damage <= 0) continue;
					ModShotKind k = ModShotKinds.Of(it);
					if (k == ModShotKind.None) continue;
					WeaponProfile p = WeaponClassifier.GetProfile(it);
					Mod.Logger.Info($"MODSHOT-AUDIT {it.ModItem?.Mod.Name}/{it.ModItem?.Name} kind={k} family={p.Family} range={p.RangeTiles} class={it.DamageType?.Name} channel={it.channel} shoot={(it.shoot > 0 ? WeaponClassifier.ProjectileSample(it.shoot)?.ModProjectile?.GetType().Name : "-")}");
					n++;
				}
				Mod.Logger.Info($"MODSHOT-AUDIT total {n}");
			}
			catch (Exception e) { Mod.Logger.Warn("MODSHOT-AUDIT failed: " + e.Message); }
		}
	}
}
