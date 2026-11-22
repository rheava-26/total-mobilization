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
	///  * <see cref="ModShotKind.ArkSwing"/>: a Calamity Ark (Fractured Ark ... Ark of the Cosmos): the item's shot is a placeholder and its Shoot hook spawns a swung blade anchored to its owner, or (right button) a parry holdout. The soldier swings, parries and empowers the next swings like a player (<see cref="ArkSwings"/>).
	///  * <see cref="ModShotKind.BiomeBlade"/>: a Calamity Biome Blade (Broken, Biome, Omega, Four Seasons Galaxia): the item carries attunements and its Shoot hook spawns the attunement's blade. The soldier attunes to the biome and uses the attunement's real attacks (<see cref="BiomeBlades"/>).
	///  Not a kind: a Calamity mace flail (<c>BaseMaceFlailProjectile</c> / <c>BaseFlailProjectile</c>: spun by its owner, thrown at the cursor when the button is let go) also reads the owner's button but is NOT held. A plain shot reads the owner's button as up whatever the commander is doing (OwnerPuppet bit 128),
	///  so a plain shot spins through its windup and throws at once; with the cursor on the target (<see cref="SteerToTargetProjectile"/>) the throw lands. Holding it as a melee holdout was measured worse (it stood at 7 tiles, below the throw's reach).
	/// </summary>
	public enum ModShotKind { None, Holdout, Shortsword, ArkSwing, BiomeBlade }

	/// <summary>
	/// How long a charge-by-button holdout is held before the button is let go, and how long it lingers after the release.
	/// A holdout that counts its own charge in <c>ai[0]</c> can be let go on the charge instead of the clock: <see cref="Stages"/> lists, per mode (the holdout's <c>ai[2]</c>), the charges worth holding for, each behind a condition
	/// read once when the hold starts; the first stage whose condition holds is the charge the holdout is released at, and with none the button is let go at once (an uncharged shot). <see cref="HoldTicks"/> then only caps the hold.
	/// Conditions: "always", "tough" (a boss or a target with 600 or more life), "crowd:N" (N or more enemies within 8 tiles of the target), "fast:V" (the target moves V or more px a tick), "far:T" (the target is T or more tiles away).
	/// </summary>
	public sealed class ChargePlan
	{
		public readonly int HoldTicks, LingerTicks;
		public Dictionary<int, (string cond, float ai0)[]> Stages;
		public ChargePlan(int hold, int linger) { HoldTicks = hold; LingerTicks = linger; }
	}

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
		/// measured lower (Nidhogg, Arc Nova Diffuser...), which would be a nerf (Phalanx Surge, Apoctosis Array and Ion Blaster were later routed for their fire rate, the Ontological Despoiler, Fetid Emesis and Purge Guzzler for the reasons noted at their entries). Later Calamity weapons default to the plain shot until they are measured.
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
			// the two-mode charge gun: as a plain shot it was owned by the commander's slot, its charge penalty and mode state lived on HER Calamity player, it could only ever fire the uncharged unstable volley (her button reads up) and it never swapped
			// (its right button is read from the shooter's own Calamity player). Held, the levy's dummy owns it: it charges (ChargeReleaseTable), fires its charged volleys, sphere and beam from the levy, and swaps modes (AltPolicies row HOLD_VARIANT cond=meter).
			"OntologicalDespoiler",
			// despoiler sweep (2026-11-21, one levy, a pinned target 12 tiles off, 360 ticks): plain -> held damage. Their plain holdouts read the commander's button as up and end before they fire.
			"FetidEmesis", "PurgeGuzzler", // 0 -> 16062, 0 -> 10433
			// brokenguns (2026-11-22): guns whose holdout is NOT the item's shot (HolderType): Condemnation and the Surge Driver make it in their Shoot hook, the Starfleet, Starmada, Heavenly Gale and Halley's Inferno are
			// held-item holdouts Calamity spawns for whoever holds the item (CanUseItem / CanShoot are false). As plain shots they made nothing, or a holdout owned by the commander that read her hand and cursor (Starfleet: a NaN position).
			// The Barracuda Gun's piranhas (vanilla aiStyle 39) latch on only while their owner channels and steer to the owner's cursor: held, they are the levy's. The Eternity book holds its hex only while channelled.
			"Condemnation", "SurgeDriver", "Starfleet", "Starmada", "HeavenlyGale", "HalleysInferno", "BarracudaGun", "Eternity",
		};

		/// <summary>
		/// Calamity's plain-shot gun holdouts whose fire rate is <c>Owner.HeldItem.useAnimation</c> (Ion Blaster, Phalanx Surge, Apoctosis Array). As plain shots they were owned by the commander and read HER hand: with it empty the rate was 0, so one
		/// holdout fired every tick (measured, one levy against a pinned enemy for 300 ticks: Ion Blaster 174,255, Phalanx Surge 1,253,093, Apoctosis Array 346,975 damage, against 1,492 / 6,621 / 3,396 for a player using the same weapon), and with
		/// anything else in her hand the rate was that item's. Held, the levy's own weapon is the item in the dummy's hand and the weapon fires at its real rate (1,300 / 7,031 / 3,137 in the same run). Switch: LEVY_DEADHOOKS_OFF=1 routes them plain again.
		/// </summary>
		private static readonly HashSet<string> HeldItemRateRouted = new() { "IonBlaster", "PhalanxSurge", "ApoctosisArray" };

		/// <summary>
		/// Hold-to-charge grenades whose AI reads the owner's raw left button, the owner's held item (<c>Owner.HeldItem.useAnimation</c>) and the owner's cursor (Calamity's Doomsday Device). As a plain shot the commander's hand supplied all three:
		/// an empty hand has useAnimation 0, so the throw completion divided by zero and the grenade's position went NaN (0 damage in 600 ticks, against 144 for a player). Held, the levy's dummy owns the grenade: its own item, button and cursor.
		/// Switch: LEVY_DEADHOOKS_OFF=1 routes it plain again.
		/// </summary>
		private static readonly HashSet<string> HeldGrenadeRouted = new() { "DoomsdayDevice" };

		/// <summary>Measurement switch (env LEVY_HOLD_FORCE=Name1,Name2 or "all"): routes these Calamity holdouts through the held path as if they were in <see cref="HoldoutRouted"/> (A/B runs of a weapon that is not routed yet).</summary>
		private static readonly string _force = Environment.GetEnvironmentVariable("LEVY_HOLD_FORCE");
		private static bool ForceHold(string name) => !string.IsNullOrEmpty(_force) && (_force == "all" || ("," + _force + ",").Contains("," + name + ","));
		/// <summary>Measurement switch (env LEVY_HOLD_PLAIN=Name1,Name2): these Calamity holdouts keep the plain shot although they are routed (the A/B of a newly routed weapon against its old behaviour).</summary>
		private static readonly string _plain = Environment.GetEnvironmentVariable("LEVY_HOLD_PLAIN");
		private static bool ForcePlain(string name) => !string.IsNullOrEmpty(_plain) && ("," + _plain + ",").Contains("," + name + ",");

		/// <summary>
		/// Held modded weapons whose damage comes from children bound to the holdout, which the held chain must carry: the dummy's owned-projectile counts are kept real for them (Rancor's circle ends when it counts no holdout), children that read
		/// the button or find the holdout by UUID stay in the chain (<see cref="OwnerLockProbe.BoundToHolder"/>), and a UUID-bound holdout gets the chain slot below 255. Every other routed weapon keeps the chain exactly as it was measured:
		/// its children are handed to the commander like ordinary shots (an earlier all-weapons version of this rule changed Roxcalibur from 5853 to 60 dmg/s per levy).
		/// </summary>
		private static readonly HashSet<string> ChainExtraNames = new() { "ArtAttack", "Rancor", "Starmageddon", "YharimsCrystal",
			// Gruesome Eminence: the holdout keeps one spirit congregation out (it counts it in its owner's ownedProjectileCounts) and the congregation lives only while its owner channels: handed to the commander it died at once and the holdout made
			// a new one every tick (322 in 360 ticks, no damage). Dark Spark: its beams find the prism by UUID (as Yharim's Crystal's do).
			"GruesomeEminence", "DarkSpark" };
		internal static bool ChainExtras(Item item) => item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && ChainExtraNames.Contains(item.ModItem.Name);

		/// <summary>A held modded weapon that deals its damage only when the button is LET GO after a charge: hold for <see cref="HoldTicks"/>, release, and end the holdout <see cref="LingerTicks"/> later so the next one is cast.</summary>
		private static readonly Dictionary<string, ChargePlan> ChargeReleaseTable = new()
		{
			// TitaniumRailgun: the scope charges while the button is down (ai1 = 60 ticks to full charge, 18 minimum) and fires on the release; held without a release it never fires (4.97 dmg/s per levy vs a x40 Eye).
			["TitaniumRailgun"] = new ChargePlan(66, 20),
			// DoomsdayDevice: the grenade charges while the button is down (about 30 ticks to full charge once the wind-up is done) and is thrown when it is let go; the linger covers the throw so the next one is cast after it has left the hand.
			["DoomsdayDevice"] = new ChargePlan(70, 90),
			// OntologicalDespoiler: ai[0] counts the charge (2 a tick, 0.5 a tick past the first level in null mode, 0.4x of that while its repeat penalty is on); 156 = the first level (the volley fires faster and holds 23 shots), 312 = full charge
			// (unstable: the homing disruption sphere; null: the always-crit hitscan beam). Measured on a pinned target (2 levies, 400 ticks): the uncharged unstable volley a player gets by tapping is the most damage by far (about 116k), a
			// first-level unstable volley about a quarter less per tick, the null volleys and the beam far less. So the unstable mode taps, and charges to the first level only to reach null mode (the swap needs a charged shot) when its
			// straight shots are poor: the target is fast or far. Null mode always charges (an uncharged null volley is two quarter-damage shots): the beam against a tough target, the homing volley otherwise. ai[2] 0 = unstable, 5 = null.
			["OntologicalDespoiler"] = new ChargePlan(460, 110) { Stages = new Dictionary<int, (string, float)[]> { [0] = new[] { ("fast:6", 158f), ("far:35", 158f) }, [5] = new[] { ("tough", 314f), ("always", 158f) } } },
			// brokenguns (2026-11-22): holdouts that load or charge while the button is down and fire everything when it is LET GO. Held without a release (a pinned target never pauses the feed) they never fired: 0 damage in 360 ticks.
			// ClockworkBow: a bolt loads every useAnimation (30) ticks, each 5% sooner, up to 6 (about 159 ticks); the release fires them all in a fan.
			["ClockworkBow"] = new ChargePlan(160, 30),
			// Condemnation: an arrow loads every 22 ticks up to 9 (198 ticks; the ninth makes the volley home); the release fires one arrow a tick.
			["Condemnation"] = new ChargePlan(200, 30),
			// TheHive: the charge counts to MaxCharge (90); past it the release fires the nuke (10x damage, 75-tick cooldown), before it four missiles (30-tick cooldown).
			["TheHive"] = new ChargePlan(92, 90),
			// TauCannon: 60 ticks a level; level 1 sprays bolts for 54 ticks, level 2 fires a 13x beam, level 3 a beam and four or five portals. The release fires; the holdout then lives out its fire and cooldown (up to 136 ticks).
			["TauCannon"] = new ChargePlan(181, 140),
			// HolofibreImmolator: the charge is full after 1.5 x the use animation (21 ticks); letting go then fires the 4.5x immolation arrow (before it, a spread of three sprays). It is spent and ends one use animation later.
			["HolofibreImmolator"] = new ChargePlan(23, 30),
		};
		/// <summary>Measurement switch (env LEVY_CHARGEPLAN=Name:hold:linger[,Name:hold:linger]): these Calamity holdouts are held and released on this plan instead of the table's (no stages), for A/B runs of a charge length.</summary>
		private static readonly Dictionary<string, ChargePlan> _planOverride = ParsePlanOverride(Environment.GetEnvironmentVariable("LEVY_CHARGEPLAN"));
		private static Dictionary<string, ChargePlan> ParsePlanOverride(string env)
		{
			var d = new Dictionary<string, ChargePlan>();
			if (string.IsNullOrEmpty(env)) return d;
			foreach (string part in env.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				string[] f = part.Split(':');
				if (f.Length >= 3 && int.TryParse(f[1], out int h) && int.TryParse(f[2], out int l)) d[f[0]] = new ChargePlan(h, l);
			}
			return d;
		}
		internal static ChargePlan ChargeRelease(Item item) => item?.ModItem == null || item.ModItem.Mod.Name != "CalamityMod" ? null : _planOverride.TryGetValue(item.ModItem.Name, out ChargePlan o) ? o : ChargeReleaseTable.TryGetValue(item.ModItem.Name, out ChargePlan p) ? p : null;

		/// <summary>
		/// Held weapons whose state lives in the holder's per-tick player step (Calamity's PostUpdateMiscEffects), not in the holdout: the Unstable Caster's Gauntlet's vis meter (+0.1 every 4 ticks while it is held) pays for every needle.
		/// The levy's gear frame (which runs that step) runs every tick while he carries one, as it does for modded accessories: on the once-a-second stat pass the meter filled 60 times too slowly and the holdout ended on its first needle.
		/// </summary>
		private static readonly HashSet<string> PlayerTickNames = new() { "UnstableCastersGauntlet" };

		/// <summary>
		/// Held weapons whose damage is dealt within a short reach of their holder, whatever the item's class says, in tiles: the Burning Sea's fireball follows the cursor to at most 8 tiles from its caster, the Hellborn's left button
		/// spins the gun as a 6-tile blade (each hit loads one of its right-button shots). At the class's range (a magic or gun stand-off of 12+ tiles) they never reached a target.
		/// </summary>
		private static readonly Dictionary<string, int> HeldReachTable = new() { ["BurningSea"] = 8, ["Hellborn"] = 6 };
		/// <summary>
		/// Held weapons whose variant is fixed when the holdout is made (the item's Shoot hook reads the right button once): the Hellborn spins (left) or fires a loaded shot (right). A player changes variant by letting go and using the
		/// other button; the levy does the same: when the wanted variant (AltPolicies.HoldVariant) is not the one his holdout was made with, he lets go and the next holdout is made with the other button.
		/// </summary>
		private static readonly HashSet<string> VariantAtSpawnNames = new() { "Hellborn" };
		internal static bool VariantAtSpawn(Item item) => item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && VariantAtSpawnNames.Contains(item.ModItem.Name);

		internal static int HeldReachTiles(Item item) => item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && HeldReachTable.TryGetValue(item.ModItem.Name, out int r) && Of(item) == ModShotKind.Holdout ? r : 0;
		internal static bool NeedsPlayerTick(Item item) => item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && PlayerTickNames.Contains(item.ModItem.Name) && Of(item) == ModShotKind.Holdout;

		// ---------------------------------------------------------------------------------------------
		// The holder: the projectile a held modded weapon is held through (usually its shot, sometimes another projectile its hooks or its holder make)
		// ---------------------------------------------------------------------------------------------
		private static readonly Dictionary<int, int> _holderCache = new();
		private static readonly Dictionary<int, bool> _holdSpawnedCache = new();
		private static Dictionary<int, int> _associated;

		/// <summary>
		/// The projectile type a held modded weapon is held through. Usually <c>item.shoot</c>; for a weapon whose shot is not the holdout it is found from the weapon's own code, never from a name:
		/// the idle holdout Calamity spawns for whoever holds the item (<c>BaseIdleHoldoutProjectile.ItemProjectileRelationship</c>: the Heavenly Gale), else the Calamity gun holdout whose <c>AssociatedItemID</c> is this item
		/// (<c>BaseGunHoldoutProjectile</c>: Condemnation, Starfleet, Starmada), else a projectile named in the item's own hooks that reads its owner's button (the Surge Driver's snipe holdout). Vanilla items: their shot.
		/// </summary>
		internal static int HolderType(Item item)
		{
			if (item == null || item.IsAir) return 0;
			if (item.type < ItemID.Count) return item.shoot;
			if (_holderCache.TryGetValue(item.type, out int h)) return h;
			h = item.shoot;
			try { h = FindHolder(item); } catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: holder probe failed for {item.Name}: {e.Message}"); }
			_holderCache[item.type] = h;
			return h;
		}

		private static int FindHolder(Item item)
		{
			if (item.shoot > 0 && WeaponClassifier.ProjectileSample(item.shoot)?.ModProjectile != null && OwnerLockProbe.ReadsOwnerButton(item.shoot)) return item.shoot;
			int idle = IdleHolder(item.type);
			if (idle > 0) return idle;
			if (Associated().TryGetValue(item.type, out int a)) return a;
			foreach (Type t in OwnerLockProbe.NamedProjectileTypes(item))
			{
				int pt = TypeOf(t);
				if (pt > 0 && pt != item.shoot && OwnerLockProbe.ReadsOwnerButton(pt)) return pt;
			}
			return item.shoot;
		}

		private static int TypeOf(Type t)
		{
			foreach (ModProjectile mp in ModContent.GetContent<ModProjectile>()) if (mp.GetType() == t) return mp.Type;
			return 0;
		}

		/// <summary>Calamity's idle holdouts: item type -> holdout type, read from <c>BaseIdleHoldoutProjectile.ItemProjectileRelationship</c> (0 when none or Calamity is not loaded).</summary>
		private static int IdleHolder(int itemType)
		{
			Type b = FindBase("BaseIdleHoldoutProjectile");
			var f = b?.GetField("ItemProjectileRelationship", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			return f?.GetValue(null) is Dictionary<int, int> rel && rel.TryGetValue(itemType, out int p) ? p : 0;
		}

		private static Type FindBase(string name)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return null;
			return cal.Code?.GetType("CalamityMod.Projectiles.BaseProjectiles." + name);
		}

		/// <summary>Every Calamity gun holdout (<c>BaseGunHoldoutProjectile</c>) by the item it belongs to (its <c>AssociatedItemID</c>), built once.</summary>
		private static Dictionary<int, int> Associated()
		{
			if (_associated != null) return _associated;
			var d = new Dictionary<int, int>();
			Type b = FindBase("BaseGunHoldoutProjectile");
			var prop = b?.GetProperty("AssociatedItemID");
			if (prop != null)
				foreach (ModProjectile mp in ModContent.GetContent<ModProjectile>())
				{
					if (!b.IsInstanceOfType(mp)) continue;
					try { if (prop.GetValue(mp) is int it && it > 0 && !d.ContainsKey(it)) d[it] = mp.Type; } catch (Exception) { }
				}
			return _associated = d;
		}

		/// <summary>
		/// The holder is not made by the item's use: the mod spawns it for whoever HOLDS the item and the use itself is shut (its <c>CanUseItem</c> or <c>CanShoot</c> is the constant false: Halley's Inferno, the Starfleet,
		/// the Heavenly Gale), or it is one of Calamity's idle holdouts. The levy's held chain then spawns the holder itself, at the levy, as the mod does for a player.
		/// </summary>
		internal static bool HoldSpawned(Item item)
		{
			if (item?.ModItem == null) return false;
			if (_holdSpawnedCache.TryGetValue(item.type, out bool c)) return c;
			bool r = false;
			try
			{
				r = IdleHolder(item.type) > 0;
				for (Type t = item.ModItem.GetType(); !r && t != null && t != typeof(ModItem) && t != typeof(object); t = t.BaseType)
					foreach (string hook in new[] { "CanUseItem", "CanShoot" })
					{
						var m = t.GetMethod(hook, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly, null, new[] { typeof(Player) }, null);
						byte[] il = m?.GetMethodBody()?.GetILAsByteArray();
						if (il != null && il.Length == 2 && il[0] == 0x16 && il[1] == 0x2A) { r = true; break; } // ldc.i4.0; ret
					}
			}
			catch (Exception) { r = false; }
			_holdSpawnedCache[item.type] = r;
			return r;
		}

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

		/// <summary>Vanilla AI styles that live only while their owner channels the item (and steer to the owner's cursor): 39 = the Piranha Gun's mechanical piranha.</summary>
		private static readonly HashSet<int> ChannelAiStyles = new() { 39 };

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
			// An Ark: a melee item whose Shoot hook spawns an owner-anchored swung blade and a parry holdout (found from the hook's own code, not from a name).
			if (item.ModItem?.Mod.Name == "CalamityMod" && item.CountsAsClass(DamageClass.Melee) && ArkSwings.IsArk(item)) return ModShotKind.ArkSwing;
			// A Biome Blade: a melee item that carries attunements (found from the item's own fields).
			if (item.ModItem?.Mod.Name == "CalamityMod" && item.CountsAsClass(DamageClass.Melee) && BiomeBlades.IsBiomeBlade(item)) return ModShotKind.BiomeBlade;
			// A melee weapon whose shot is a held stab is a melee weapon: it fights at blade reach with Levy's own stab.
			if (shortsword && item.CountsAsClass(DamageClass.Melee)) return ModShotKind.Shortsword;
			if (mace) return ModShotKind.None; // thrown at the cursor, not held (see the class summary)
			// A held / charge-and-release holdout reads its owner's button (and is the weapon's own shot).
			// Only holdouts measured as duds as plain shots are routed (see HoldoutRouted); everything else keeps the plain shot, which already works for it.
			if (item.ModItem?.Mod.Name == "CalamityMod" && ForcePlain(item.ModItem.Name)) return ModShotKind.None;
			if (item.ModItem?.Mod.Name == "CalamityMod" && !ProjAiHooks.Off && HeldGrenadeRouted.Contains(item.ModItem.Name) && item.channel && item.shoot > 0 && sample?.ModProjectile != null) return ModShotKind.Holdout;
			if (item.ModItem?.Mod.Name == "CalamityMod" && (HoldoutRouted.Contains(item.ModItem.Name) || (!ProjAiHooks.Off && HeldItemRateRouted.Contains(item.ModItem.Name)) || ForceHold(item.ModItem.Name)) && item.shoot > 0
				&& (item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Magic) || item.CountsAsClass(DamageClass.Melee)))
			{
				int holder = HolderType(item);
				Projectile hs = WeaponClassifier.ProjectileSample(holder);
				// the holder reads its owner's button (a modded holdout), or it is a vanilla channel shot that does (the piranha style: latched on while its owner channels, steered to the cursor)
				// (a holder that is not the item's shot may read the raw mouse button instead: the Heavenly Gale's idle bow fires while Main.mouseLeft is down)
				bool reads = hs?.ModProjectile != null && hs.aiStyle != 39 ? OwnerLockProbe.ReadsOwnerButton(holder) || (holder != item.shoot && OwnerLockProbe.ReadsUseButtons(holder)) : hs != null && ChannelAiStyles.Contains(hs.aiStyle);
				if (reads && (item.channel || holder != item.shoot || HoldSpawned(item))) return ModShotKind.Holdout;
			}
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
					Mod.Logger.Info($"MODSHOT-AUDIT {it.ModItem?.Mod.Name}/{it.ModItem?.Name} kind={k} family={p.Family} range={p.RangeTiles} class={it.DamageType?.Name} channel={it.channel} shoot={(it.shoot > 0 ? WeaponClassifier.ProjectileSample(it.shoot)?.ModProjectile?.GetType().Name : "-")}{(ModShotKinds.HolderType(it) != it.shoot ? " holder=" + (ProjectileLoader.GetProjectile(ModShotKinds.HolderType(it))?.Name ?? ModShotKinds.HolderType(it).ToString()) : "")}{(ModShotKinds.HoldSpawned(it) ? " held-spawn" : "")}");
					n++;
				}
				Mod.Logger.Info($"MODSHOT-AUDIT total {n}");
			}
			catch (Exception e) { Mod.Logger.Warn("MODSHOT-AUDIT failed: " + e.Message); }
		}
	}
}
