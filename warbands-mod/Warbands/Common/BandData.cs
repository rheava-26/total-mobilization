using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Warbands.Common
{
	public enum BandBiome : byte { Forest, Underground, Corruption, Crimson, Desert, Snow, Jungle, Hallow, Underworld }

	/// <summary>One soldier of a band: a role, the real weapon and ammo it carries, and the armor set it wears. All names are vanilla internal item names (ItemID.Search).</summary>
	public class BandMember
	{
		/// <summary>'R' ranged, 'M' magic, 'E' melee.</summary>
		public char Role;
		public string Weapon, Ammo;
		public string[] Armor;
		public BandMember(char role, string weapon, string ammo, string[] armor) { Role = role; Weapon = weapon; Ammo = ammo; Armor = armor; }
	}

	public class BandRoster
	{
		public BandBiome Biome; public int Stage; public string Name; public BandMember[] Members;
	}

	/// <summary>
	/// The biome x stage roster table, drawn from the same tiers Levy's gear presets use (levy-mod/lab/presets/gear.yaml: what a normal player carries at that point). A band picks its members from its roster with
	/// the Lunatic Cultist excepted (see CultistBand). Stages with no roster of their own use the nearest earlier one with every weapon kept: the world is ahead of the band, which is the point of a band of deserters.
	/// </summary>
	public static class BandData
	{
		private static readonly string[] Copper = { "CopperHelmet", "CopperChainmail", "CopperGreaves" };
		private static readonly string[] Iron = { "IronHelmet", "IronChainmail", "IronGreaves" };
		private static readonly string[] Gold = { "GoldHelmet", "GoldChainmail", "GoldGreaves" };
		private static readonly string[] Platinum = { "PlatinumHelmet", "PlatinumChainmail", "PlatinumGreaves" };
		private static readonly string[] Cactus = { "CactusHelmet", "CactusBreastplate", "CactusLeggings" };
		private static readonly string[] Fossil = { "FossilHelm", "FossilShirt", "FossilPants" };
		private static readonly string[] Jungle = { "JungleHat", "JungleShirt", "JunglePants" };
		private static readonly string[] Shadow = { "ShadowHelmet", "ShadowScalemail", "ShadowGreaves" };
		private static readonly string[] Crimson = { "CrimsonHelmet", "CrimsonScalemail", "CrimsonGreaves" };
		private static readonly string[] Meteor = { "MeteorHelmet", "MeteorSuit", "MeteorLeggings" };
		private static readonly string[] Molten = { "MoltenHelmet", "MoltenBreastplate", "MoltenGreaves" };
		private static readonly string[] Bee = { "BeeHeadgear", "BeeBreastplate", "BeeGreaves" };
		private static readonly string[] Cobalt = { "CobaltHelmet", "CobaltBreastplate", "CobaltLeggings" };
		private static readonly string[] Mythril = { "MythrilHelmet", "MythrilChainmail", "MythrilGreaves" };
		private static readonly string[] Palladium = { "PalladiumHelmet", "PalladiumBreastplate", "PalladiumLeggings" };
		private static readonly string[] Hallowed = { "HallowedHelmet", "HallowedPlateMail", "HallowedGreaves" };
		private static readonly string[] Frost = { "FrostHelmet", "FrostBreastplate", "FrostLeggings" };
		private static readonly string[] Turtle = { "TurtleHelmet", "TurtleScaleMail", "TurtleLeggings" };
		private static readonly string[] Chlorophyte = { "ChlorophyteHelmet", "ChlorophytePlateMail", "ChlorophyteGreaves" };
		private static readonly string[] Obsidian = { "ObsidianHelm", "ObsidianShirt", "ObsidianPants" };

		private static BandMember R(string w, string a, string[] ar) => new BandMember('R', w, a, ar);
		private static BandMember M(string w, string[] ar) => new BandMember('M', w, null, ar);
		private static BandMember E(string w, string[] ar) => new BandMember('E', w, null, ar);
		private static BandRoster B(BandBiome b, int stage, string name, params BandMember[] m) => new BandRoster { Biome = b, Stage = stage, Name = name, Members = m };

		public static readonly BandRoster[] All =
		{
			// ---- stage 0: pre-boss ----
			B(BandBiome.Forest, 0, "Forest deserters", R("WoodenBow", "WoodenArrow", Copper), R("WoodenBow", "WoodenArrow", Copper), E("CopperShortsword", Copper), E("CopperBroadsword", Iron)),
			B(BandBiome.Underground, 0, "Cavern prospectors", R("Musket", "MusketBall", Iron), M("AmethystStaff", Iron), E("IronBroadsword", Gold), R("FlintlockPistol", "MusketBall", Iron)),
			B(BandBiome.Corruption, 0, "Corruption cultists", M("Vilethorn", Shadow), R("WoodenBow", "UnholyArrow", Platinum), E("LightsBane", Platinum)),
			B(BandBiome.Crimson, 0, "Crimson cultists", M("CrimsonRod", Crimson), R("TendonBow", "WoodenArrow", Platinum), E("BloodButcherer", Platinum)),
			B(BandBiome.Desert, 0, "Dune raiders", R("WoodenBow", "FlamingArrow", Cactus), E("CopperBroadsword", Cactus), M("TopazStaff", Cactus)),
			B(BandBiome.Snow, 0, "Tundra trappers", R("Musket", "MusketBall", Iron), E("IronBroadsword", Iron), M("SapphireStaff", Iron)),
			B(BandBiome.Jungle, 0, "Jungle brigands", R("WoodenBow", "JestersArrow", Gold), E("IronBroadsword", Gold), M("EmeraldStaff", Gold)),
			B(BandBiome.Hallow, 0, "Pearl-wood skirmishers", R("WoodenBow", "FlamingArrow", Gold), E("GoldBroadsword", Gold), M("DiamondStaff", Gold)),
			B(BandBiome.Underworld, 0, "Ashland scavengers", R("DemonBow", "FlamingArrow", Obsidian), E("Katana", Obsidian), M("DiamondStaff", Obsidian)),

			// ---- stage 1: after the Eater of Worlds / Brain of Cthulhu ----
			B(BandBiome.Forest, 1, "Forest deserters", R("PhoenixBlaster", "MeteorShot", Shadow), R("Handgun", "MeteorShot", Meteor), E("LightsBane", Meteor), M("Flamelash", Meteor)),
			B(BandBiome.Underground, 1, "Cavern prospectors", R("TheUndertaker", "MeteorShot", Molten), M("Flamelash", Meteor), E("BloodButcherer", Molten)),
			B(BandBiome.Corruption, 1, "Corruption cultists", M("Vilethorn", Shadow), R("DemonBow", "UnholyArrow", Shadow), E("LightsBane", Shadow)),
			B(BandBiome.Crimson, 1, "Crimson cultists", M("CrimsonRod", Crimson), R("TendonBow", "FlamingArrow", Crimson), E("BloodButcherer", Crimson)),
			B(BandBiome.Desert, 1, "Dune raiders", R("PhoenixBlaster", "MeteorShot", Fossil), E("LightsBane", Fossil), M("Flamelash", Fossil)),
			B(BandBiome.Snow, 1, "Tundra trappers", R("TheUndertaker", "MeteorShot", Shadow), E("BloodButcherer", Shadow), M("AquaScepter", Shadow)),
			B(BandBiome.Jungle, 1, "Jungle brigands", R("PhoenixBlaster", "MeteorShot", Jungle), E("BladeofGrass", Jungle), M("MagicMissile", Jungle)),
			B(BandBiome.Hallow, 1, "Pearl-wood skirmishers", R("Handgun", "MeteorShot", Gold), E("LightsBane", Platinum), M("MagicMissile", Platinum)),
			B(BandBiome.Underworld, 1, "Ashland scavengers", R("PhoenixBlaster", "MeteorShot", Molten), E("LightsBane", Molten), M("Flamelash", Molten)),

			// ---- stage 3: early Hardmode ----
			B(BandBiome.Forest, 3, "Forest deserters", R("MythrilRepeater", "HolyArrow", Mythril), R("CobaltRepeater", "HolyArrow", Cobalt), E("Excalibur", Mythril), M("CrystalStorm", Mythril)),
			B(BandBiome.Underground, 3, "Cavern prospectors", R("MythrilRepeater", "HellfireArrow", Palladium), M("CrystalStorm", Palladium), E("Excalibur", Palladium)),
			B(BandBiome.Corruption, 3, "Corruption cultists", M("CursedFlames", Cobalt), R("CobaltRepeater", "CursedArrow", Cobalt), E("NightsEdge", Cobalt)),
			B(BandBiome.Crimson, 3, "Crimson cultists", M("CrimsonRod", Cobalt), R("PalladiumRepeater", "IchorArrow", Palladium), E("NightsEdge", Palladium)),
			B(BandBiome.Desert, 3, "Dune raiders", R("MythrilRepeater", "HolyArrow", Mythril), E("Excalibur", Mythril), M("CrystalStorm", Mythril)),
			B(BandBiome.Snow, 3, "Tundra trappers", R("CobaltRepeater", "FrostburnArrow", Cobalt), E("Excalibur", Cobalt), M("CrystalStorm", Cobalt)),
			B(BandBiome.Jungle, 3, "Jungle brigands", R("PalladiumRepeater", "HolyArrow", Palladium), E("BladeofGrass", Palladium), M("CrystalStorm", Palladium)),
			B(BandBiome.Hallow, 3, "Pearl-wood skirmishers", R("MythrilRepeater", "HolyArrow", Hallowed), E("Excalibur", Hallowed), M("CrystalStorm", Hallowed)),
			B(BandBiome.Underworld, 3, "Ashland scavengers", R("PalladiumRepeater", "HellfireArrow", Molten), E("Excalibur", Molten), M("CursedFlames", Molten)),

			// ---- stage 5: after Plantera ----
			B(BandBiome.Forest, 5, "Forest deserters", R("Megashark", "ChlorophyteBullet", Turtle), R("ChlorophyteShotbow", "HolyArrow", Chlorophyte), E("TerraBlade", Turtle), M("NettleBurst", Chlorophyte)),
			B(BandBiome.Underground, 5, "Cavern prospectors", R("Megashark", "ChlorophyteBullet", Chlorophyte), M("NettleBurst", Chlorophyte), E("TerraBlade", Chlorophyte)),
			B(BandBiome.Corruption, 5, "Corruption cultists", M("NettleBurst", Chlorophyte), R("Megashark", "CursedBullet", Turtle), E("TerraBlade", Turtle)),
			B(BandBiome.Crimson, 5, "Crimson cultists", M("NettleBurst", Chlorophyte), R("Megashark", "IchorBullet", Turtle), E("TerraBlade", Turtle)),
			B(BandBiome.Desert, 5, "Dune raiders", R("Megashark", "ChlorophyteBullet", Turtle), E("TerraBlade", Turtle), M("NettleBurst", Chlorophyte)),
			B(BandBiome.Snow, 5, "Tundra trappers", R("ChlorophyteShotbow", "FrostburnArrow", Frost), E("TerraBlade", Frost), M("NettleBurst", Frost)),
			B(BandBiome.Jungle, 5, "Jungle brigands", R("ChlorophyteShotbow", "HolyArrow", Chlorophyte), E("TerraBlade", Chlorophyte), M("NettleBurst", Chlorophyte)),
			B(BandBiome.Hallow, 5, "Pearl-wood skirmishers", R("Megashark", "ChlorophyteBullet", Hallowed), E("TerraBlade", Hallowed), M("NettleBurst", Hallowed)),
			B(BandBiome.Underworld, 5, "Ashland scavengers", R("Megashark", "ChlorophyteBullet", Molten), E("TerraBlade", Molten), M("NettleBurst", Molten)),
		};

		/// <summary>The roster for a biome at a stage: the highest stage at or below it that has one. Null only if the biome has no roster at all.</summary>
		public static BandRoster For(BandBiome biome, int stage)
		{
			BandRoster best = null;
			foreach (var r in All)
				if (r.Biome == biome && r.Stage <= stage && (best == null || r.Stage > best.Stage)) best = r;
			return best;
		}

		/// <summary>Resolves an internal item name; -1 when it does not exist.</summary>
		public static int ItemType(string name) => name != null && ItemID.Search.TryGetId(name, out int id) ? id : -1;

		/// <summary>The biome the player stands in (vanilla zone flags; the evil and Hallow biomes beat the depth layers, the Underworld beats everything).</summary>
		public static BandBiome BiomeOf(Player p)
		{
			if (p.ZoneUnderworldHeight) return BandBiome.Underworld;
			if (p.ZoneCorrupt) return BandBiome.Corruption;
			if (p.ZoneCrimson) return BandBiome.Crimson;
			if (p.ZoneHallow) return BandBiome.Hallow;
			if (p.ZoneJungle) return BandBiome.Jungle;
			if (p.ZoneSnow) return BandBiome.Snow;
			if (p.ZoneDesert) return BandBiome.Desert;
			if (p.ZoneRockLayerHeight || p.ZoneDirtLayerHeight) return BandBiome.Underground;
			return BandBiome.Forest;
		}
	}
}
