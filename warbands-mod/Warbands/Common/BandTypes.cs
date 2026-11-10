using System;
using System.Collections.Generic;
using Terraria;

namespace Warbands.Common
{
	/// <summary>
	/// The 20 named band types: one per biome and stage flavour, each with its own roster, summoning item (crafted from wood and a few cheap things at a Work Bench), warning behaviour and unique payout.
	/// The members carry real vanilla gear from the stage's tier (the self-check holds every item to Levy's progression ceiling). The legacy biome x stage rosters (BandData.All) stay as the fallback for natural spawns.
	/// </summary>
	public static class BandTypes
	{
		private static readonly string[] Copper = { "CopperHelmet", "CopperChainmail", "CopperGreaves" };
		private static readonly string[] Iron = { "IronHelmet", "IronChainmail", "IronGreaves" };
		private static readonly string[] Gold = { "GoldHelmet", "GoldChainmail", "GoldGreaves" };
		private static readonly string[] Cactus = { "CactusHelmet", "CactusBreastplate", "CactusLeggings" };
		private static readonly string[] Shadow = { "ShadowHelmet", "ShadowScalemail", "ShadowGreaves" };
		private static readonly string[] Crimson = { "CrimsonHelmet", "CrimsonScalemail", "CrimsonGreaves" };
		private static readonly string[] Jungle = { "JungleHat", "JungleShirt", "JunglePants" };
		private static readonly string[] Meteor = { "MeteorHelmet", "MeteorSuit", "MeteorLeggings" };
		private static readonly string[] Molten = { "MoltenHelmet", "MoltenBreastplate", "MoltenGreaves" };
		private static readonly string[] Necro = { "NecroHelmet", "NecroBreastplate", "NecroGreaves" };
		private static readonly string[] Bee = { "BeeHeadgear", "BeeBreastplate", "BeeGreaves" };
		private static readonly string[] Hallowed = { "HallowedHelmet", "HallowedPlateMail", "HallowedGreaves" };
		private static readonly string[] Mythril = { "MythrilHelmet", "MythrilChainmail", "MythrilGreaves" };
		private static readonly string[] Palladium = { "PalladiumHelmet", "PalladiumBreastplate", "PalladiumLeggings" };
		private static readonly string[] Titanium = { "TitaniumHelmet", "TitaniumBreastplate", "TitaniumLeggings" };
		private static readonly string[] Chlorophyte = { "ChlorophyteHelmet", "ChlorophytePlateMail", "ChlorophyteGreaves" };
		private static readonly string[] Shroomite = { "ShroomiteHeadgear", "ShroomiteBreastplate", "ShroomiteLeggings" };
		private static readonly string[] Frost = { "FrostHelmet", "FrostBreastplate", "FrostLeggings" };
		private static readonly string[] Spectre = { "SpectreHood", "SpectreRobe", "SpectrePants" };
		private static readonly string[] Cult = { "WizardHat", "AmethystRobe", "GoldGreaves" };

		private static BandMember R(string w, string a, string[] ar) => new BandMember('R', w, a, ar);
		private static BandMember M(string w, string[] ar) => new BandMember('M', w, null, ar);
		private static BandMember E(string w, string[] ar) => new BandMember('E', w, null, ar);

		private static BandRoster T(string id, BandBiome biome, int stage, string name, string title, string kind, string item, bool warns, string warn, string flavor, int tr, int tg, int tb, (string, int)[] mats, (string, int, int)[] bounty, int copper, params BandMember[] m)
			=> new BandRoster { Id = id, Biome = biome, Stage = stage, Name = name, Title = title, ItemKind = kind, ItemName = item, Warns = warns, WarnLine = warn, Flavor = flavor, TintR = tr, TintG = tg, TintB = tb, Mats = mats, Bounty = bounty, BountyCopper = copper, Members = m };

		public static readonly BandRoster[] All =
		{
			// ---- pre-boss ----
			T("ForestDeserters", BandBiome.Forest, 0, "Forest Deserters", "Deserter", "Wanted Poster", "Deserters' Wanted Poster", false, null, null, 150, 120, 80,
				new[] { ("Wood", 8), ("Acorn", 3) }, new[] { ("CopperBar", 6, 10), ("Wood", 20, 40) }, 8000,
				R("WoodenBow", "WoodenArrow", Copper), R("WoodenBow", "WoodenArrow", Copper), E("CopperShortsword", Copper), E("CopperBroadsword", Iron)),
			T("IronBrigands", BandBiome.Underground, 0, "Iron Brigands", "Brigand", "Wanted Poster", "Brigands' Wanted Poster", false, null, null, 120, 120, 130,
				new[] { ("Wood", 8), ("IronOre", 4) }, new[] { ("IronBar", 6, 10), ("Torch", 20, 40) }, 20000,
				R("Musket", "MusketBall", Iron), M("AmethystStaff", Iron), E("IronBroadsword", Gold), R("FlintlockPistol", "MusketBall", Iron)),
			T("DuneRaiders", BandBiome.Desert, 0, "Dune Raiders", "Raider", "Challenge Flag", "Raiders' Challenge Flag", false, null, null, 210, 170, 80,
				new[] { ("Wood", 8), ("Cactus", 4) }, new[] { ("Cactus", 8, 16), ("AntlionMandible", 1, 2) }, 15000,
				R("WoodenBow", "FlamingArrow", Cactus), E("CopperBroadsword", Cactus), M("TopazStaff", Cactus), E("CopperShortsword", Cactus)),
			T("SnowRangers", BandBiome.Snow, 0, "Snow Rangers", "Ranger", "Challenge Flag", "Rangers' Challenge Flag", true, "Turn back.", "Rangers", 150, 200, 230,
				new[] { ("Wood", 8), ("BorealWood", 4) }, new[] { ("BorealWood", 20, 30), ("IceBlock", 10, 20) }, 15000,
				R("Musket", "MusketBall", Iron), R("WoodenBow", "FrostburnArrow", Iron), M("SapphireStaff", Iron), R("Musket", "MusketBall", Iron)),

			// ---- after the Eater of Worlds / Brain of Cthulhu ----
			T("CorruptZealots", BandBiome.Corruption, 1, "Corrupt Zealots", "Zealot", "Effigy", "Zealots' Effigy", true, "Turn back. The Mind hears you.", "Zealots", 150, 90, 190,
				new[] { ("Wood", 8), ("RottenChunk", 3) }, new[] { ("DemoniteBar", 4, 8), ("ShadowScale", 3, 6) }, 25000,
				M("Vilethorn", Shadow), R("DemonBow", "UnholyArrow", Shadow), E("LightsBane", Shadow), M("Vilethorn", Shadow)),
			T("CrimsonFleshCult", BandBiome.Crimson, 1, "Crimson Flesh-Cult", "Acolyte", "Effigy", "Flesh-Cult Effigy", true, "Turn back. The flesh remembers.", "Zealots", 200, 50, 60,
				new[] { ("Wood", 8), ("Vertebrae", 3) }, new[] { ("CrimtaneBar", 4, 8), ("TissueSample", 3, 6) }, 25000,
				M("CrimsonRod", Crimson), R("TendonBow", "FlamingArrow", Crimson), E("BloodButcherer", Crimson), M("CrimsonRod", Crimson)),
			T("JunglePoachers", BandBiome.Jungle, 1, "Jungle Poachers", "Poacher", "Wanted Poster", "Poachers' Wanted Poster", false, null, null, 90, 170, 80,
				new[] { ("Wood", 8), ("Stinger", 3) }, new[] { ("JungleSpores", 6, 12), ("Stinger", 4, 8) }, 30000,
				R("PhoenixBlaster", "MeteorShot", Jungle), E("BladeofGrass", Jungle), M("MagicMissile", Jungle), R("Handgun", "MeteorShot", Jungle)),
			T("MeteorMarauders", BandBiome.Forest, 1, "Meteor Marauders", "Marauder", "Battle Standard", "Marauders' Battle Standard", false, null, null, 230, 120, 60,
				new[] { ("Wood", 8), ("FallenStar", 2), ("Gel", 5) }, new[] { ("MeteoriteBar", 6, 10), ("FallenStar", 3, 6) }, 30000,
				R("PhoenixBlaster", "MeteorShot", Meteor), R("Handgun", "MeteorShot", Meteor), M("Flamelash", Meteor), E("LightsBane", Meteor)),
			T("AshScavengers", BandBiome.Underworld, 1, "Ashland Scavengers", "Scavenger", "Battle Standard", "Scavengers' Battle Standard", false, null, null, 220, 90, 40,
				new[] { ("Wood", 8), ("Obsidian", 3) }, new[] { ("HellstoneBar", 4, 8), ("Obsidian", 10, 20) }, 30000,
				R("PhoenixBlaster", "MeteorShot", Molten), E("LightsBane", Molten), M("Flamelash", Molten), R("Handgun", "MeteorShot", Molten)),

			// ---- after Skeletron ----
			T("DungeonWardens", BandBiome.Dungeon, 2, "Dungeon Wardens", "Warden", "Challenge Flag", "Wardens' Challenge Flag", true, "Turn back. These halls are not yours.", "Wardens", 110, 130, 190,
				new[] { ("Wood", 8), ("Bone", 6) }, new[] { ("Bone", 20, 40), ("GoldenKey", 1, 1) }, 50000,
				E("Muramasa", Necro), M("WaterBolt", Necro), R("Handgun", "MeteorShot", Necro), E("Muramasa", Necro)),
			T("HiveRaiders", BandBiome.Jungle, 2, "Hive Raiders", "Raider", "Challenge Flag", "Hive Raiders' Challenge Flag", false, null, null, 230, 190, 60,
				new[] { ("Wood", 8), ("BottledHoney", 3) }, new[] { ("BeeWax", 10, 20), ("Stinger", 6, 12) }, 50000,
				M("BeeGun", Bee), R("BeesKnees", "WoodenArrow", Bee), E("BladeofGrass", Bee), R("BeesKnees", "WoodenArrow", Bee)),

			// ---- early Hardmode ----
			T("HallowPaladins", BandBiome.Hallow, 3, "Hallow Paladins", "Paladin", "Effigy", "Paladins' Effigy", true, "Turn back, heathen.", "Paladins", 240, 220, 150,
				new[] { ("Wood", 8), ("PixieDust", 3) }, new[] { ("SoulofLight", 6, 12), ("PixieDust", 10, 20) }, 80000,
				R("MythrilRepeater", "HolyArrow", Hallowed), E("Excalibur", Hallowed), M("CrystalStorm", Hallowed), E("Excalibur", Hallowed)),
			T("MythrilMercenaries", BandBiome.Underground, 3, "Mythril Mercenaries", "Mercenary", "Wanted Poster", "Mercenaries' Wanted Poster", false, null, null, 110, 200, 150,
				new[] { ("Wood", 8), ("CobaltOre", 4) }, new[] { ("MythrilBar", 4, 8), ("CobaltBar", 4, 8) }, 80000,
				R("MythrilRepeater", "HellfireArrow", Mythril), M("CrystalStorm", Mythril), E("Excalibur", Mythril), R("PalladiumRepeater", "HolyArrow", Palladium)),
			T("HellfireLegion", BandBiome.Underworld, 3, "Hellfire Legion", "Legionnaire", "Battle Standard", "Hellfire Legion Standard", false, null, null, 230, 70, 30,
				new[] { ("Wood", 8), ("Hellstone", 3) }, new[] { ("HellstoneBar", 8, 14), ("SoulofNight", 4, 8) }, 90000,
				R("PalladiumRepeater", "HellfireArrow", Molten), E("Excalibur", Molten), M("CursedFlames", Molten), R("CobaltRepeater", "HellfireArrow", Molten)),

			// ---- after the mechanical bosses ----
			T("TitaniumLegion", BandBiome.Desert, 4, "Titanium Legion", "Legionnaire", "Challenge Flag", "Titanium Legion Flag", false, null, null, 190, 190, 200,
				new[] { ("Wood", 8), ("AntlionMandible", 2) }, new[] { ("TitaniumBar", 6, 10), ("AntlionMandible", 3, 5) }, 110000,
				R("TitaniumRepeater", "HolyArrow", Titanium), E("TitaniumSword", Titanium), M("CrystalStorm", Titanium), R("TitaniumRepeater", "HolyArrow", Titanium)),

			// ---- after Plantera ----
			T("ChlorophyteWardens", BandBiome.Jungle, 5, "Chlorophyte Wardens", "Warden", "Battle Standard", "Wardens' Battle Standard", true, "Turn back. The grove is closed.", "Wardens", 120, 220, 70,
				new[] { ("Wood", 8), ("JungleSpores", 6) }, new[] { ("ChlorophyteOre", 40, 70), ("JungleSpores", 6, 12) }, 100000,
				R("ChlorophyteShotbow", "HolyArrow", Chlorophyte), E("TerraBlade", Chlorophyte), M("NettleBurst", Chlorophyte), R("ChlorophyteShotbow", "HolyArrow", Chlorophyte)),
			T("ShroomiteRaiders", BandBiome.Mushroom, 5, "Shroomite Raiders", "Raider", "Battle Standard", "Shroomite Raiders' Standard", false, null, null, 70, 120, 230,
				new[] { ("Wood", 8), ("GlowingMushroom", 10) }, new[] { ("ShroomiteBar", 6, 10), ("GlowingMushroom", 30, 60) }, 120000,
				R("Megashark", "ChlorophyteBullet", Shroomite), R("Megashark", "ChlorophyteBullet", Shroomite), M("NettleBurst", Shroomite), E("TerraBlade", Shroomite)),
			T("FrostLegion", BandBiome.Snow, 5, "Frost Legion", "Legionnaire", "Challenge Flag", "Frost Legion Flag", false, null, null, 170, 220, 255,
				new[] { ("Wood", 8), ("IceBlock", 8) }, new[] { ("ChlorophyteBar", 4, 8), ("IceBlock", 30, 60) }, 120000,
				R("ChlorophyteShotbow", "FrostburnArrow", Frost), E("TerraBlade", Frost), M("NettleBurst", Frost), R("Megashark", "ChlorophyteBullet", Frost)),
			T("DungeonRitualists", BandBiome.Dungeon, 5, "Dungeon Ritualists", "Ritualist", "Effigy", "Ritualists' Effigy", true, "Turn back. The rite is not finished.", "Zealots", 140, 150, 230,
				new[] { ("Wood", 8), ("Bone", 10) }, new[] { ("Ectoplasm", 6, 10), ("GoldenKey", 1, 2) }, 120000,
				M("ShadowbeamStaff", Spectre), M("SpectreStaff", Spectre), E("Muramasa", Spectre), R("ChlorophyteShotbow", "HolyArrow", Spectre)),

			// ---- after Golem: the circle outside the Dungeon ----
			T("CultistArchers", BandBiome.Dungeon, 6, "Cultist Archers", "Cultist", "Effigy", "Cultist Archers' Sigil", false, null, "Zealots", 70, 90, 220,
				new[] { ("Wood", 8), ("Bone", 10), ("Silk", 4) }, new[] { ("LunarTabletFragment", 4, 8), ("Ectoplasm", 4, 8) }, 150000,
				R("ChlorophyteShotbow", "HolyArrow", Cult), R("ChlorophyteShotbow", "HolyArrow", Cult), M("NettleBurst", Cult), R("Megashark", "ChlorophyteBullet", Cult)),
		};

		/// <summary>Adds the tier material to a type's Work Bench recipe: a band's summoning item needs a material of the band's own stage (the world must already have it).</summary>
		private static void Gate(string id, string item, int n)
		{
			var t = ById(id); if (t == null) return;
			var l = new List<(string, int)>(t.Mats); l.Add((item, n)); t.Mats = l.ToArray(); t.Gate = item;
		}

		/// <summary>The vanity a band wears (head, body, legs; null keeps the real armor for that slot): masks and sets that read as the band. Mages add a robe.</summary>
		private static void Dress(string id, string head, string body = null, string legs = null)
		{
			var t = ById(id); if (t != null) t.Vanity = new[] { head, body, legs };
		}

		static BandTypes()
		{
			Gate("ForestDeserters", "CopperBar", 3); Gate("IronBrigands", "IronBar", 3); Gate("DuneRaiders", "Cactus", 4); Gate("SnowRangers", "BorealWood", 4);
			Gate("CorruptZealots", "DemoniteBar", 3); Gate("CrimsonFleshCult", "CrimtaneBar", 3); Gate("JunglePoachers", "JungleSpores", 3); Gate("MeteorMarauders", "MeteoriteBar", 3);
			Gate("AshScavengers", "HellstoneBar", 3); Gate("DungeonWardens", "Bone", 6); Gate("HiveRaiders", "BeeWax", 4);
			Gate("HallowPaladins", "SoulofLight", 3); Gate("MythrilMercenaries", "MythrilBar", 3); Gate("HellfireLegion", "HellstoneBar", 4);
			Gate("TitaniumLegion", "TitaniumBar", 3);
			Gate("ChlorophyteWardens", "ChlorophyteBar", 3); Gate("ShroomiteRaiders", "ShroomiteBar", 3); Gate("FrostLegion", "ChlorophyteBar", 3);
			Gate("DungeonRitualists", "Ectoplasm", 3); Gate("CultistArchers", "Ectoplasm", 4);

			Dress("ForestDeserters", "GreenCap"); Dress("IronBrigands", "GuyFawkesMask"); Dress("DuneRaiders", "CowboyHat", "CowboyJacket", "CowboyPants");
			Dress("SnowRangers", "EskimoHood", "EskimoCoat", "EskimoPants"); Dress("CorruptZealots", "EaterMask"); Dress("CrimsonFleshCult", "BrainMask");
			Dress("JunglePoachers", "TikiMask"); Dress("MeteorMarauders", "SpaceCreatureMask"); Dress("AshScavengers", "DemonHorns");
			Dress("DungeonWardens", "SkeletronMask"); Dress("HiveRaiders", "BeeMask"); Dress("HallowPaladins", "SquireGreatHelm", "SquirePlating", "SquireGreaves");
			Dress("MythrilMercenaries", "GangstaHat"); Dress("HellfireLegion", "DevilHorns"); Dress("TitaniumLegion", "GladiatorHelmet", "GladiatorBreastplate", "GladiatorLeggings");
			Dress("ChlorophyteWardens", "PlanteraMask"); Dress("ShroomiteRaiders", "MushroomCap"); Dress("FrostLegion", "ParkaHood", "ParkaCoat", "ParkaPants");
			Dress("DungeonRitualists", "ReaperHood", "ReaperRobe"); Dress("CultistArchers", "BlueLunaticHood");
		}
		public static BandRoster ById(string id) { foreach (var t in All) if (t.Id == id) return t; return null; }

		/// <summary>The named type for a biome at a world stage: the highest-stage type of that biome at or below the stage (a random one among ties). Null when the biome has none yet.</summary>
		public static BandRoster Pick(BandBiome biome, int stage, Terraria.Utilities.UnifiedRandom rng)
		{
			int best = -1; var pool = new List<BandRoster>();
			foreach (var t in All) if (t.Biome == biome && t.Stage <= stage && t.Id != "CultistArchers") { if (t.Stage > best) { best = t.Stage; pool.Clear(); } if (t.Stage == best) pool.Add(t); }
			return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
		}

		/// <summary>0..19 for a named type; 200 + n for the n-th legacy biome roster (BandData.All); -1 when unknown.</summary>
		public static int IndexOf(BandRoster r)
		{
			for (int i = 0; i < All.Length; i++) if (ReferenceEquals(All[i], r)) return i;
			for (int i = 0; i < BandData.All.Length; i++) if (ReferenceEquals(BandData.All[i], r)) return 200 + i;
			return -1;
		}
		public static BandRoster FromIndex(int i) => i >= 0 && i < All.Length ? All[i] : i >= 200 && i - 200 < BandData.All.Length ? BandData.All[i - 200] : null;
	}
}
