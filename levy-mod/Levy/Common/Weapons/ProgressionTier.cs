using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// How far into progression an item is, on Boss Checklist's scale (King Slime 1, Eye of Cthulhu 2, Evil boss 3, Queen Bee 4, Skeletron 5, Deerclops 6, Wall of Flesh 7, Queen Slime 8, Twins 9, Destroyer 10, Prime 11,
	/// Plantera 12, Golem 13, Fishron 14, Empress 15, Cultist 17, Moon Lord 18; Calamity and Thorium bosses sit between and beyond on their own registered values). Computed once, when a world loads, on a server or in
	/// single player (a multiplayer client does not rank weapons). <see cref="WeaponRank"/> compares it with the world's own progress to discount weapons the world has long outgrown.
	/// ROUTES. An item's tier is the lowest of: the boss that drops it (rules of the boss NPC and of its treasure bag, read from <c>Main.ItemDropsDB</c>; rows that exist only in a Remix-seed world are ignored), a shop that sells it
	/// (honouring the entry's conditions; the Travelling and Skeleton Merchants' random stock gives no route), a raw-material table (ores, souls), and a recipe (the highest tier among its ingredients, stations and conditions).
	/// Recipes are solved as a fixpoint, so loops (Wood and Wood Platform, Hellstone and its wall) cannot poison a tree. An item reached by no route is UNKNOWN, never tier 0: it takes its rarity's stage, or, for a custom
	/// rarity, a haircut of its sell value. A rarity is also a floor: no route puts an item earlier than its rarity implies.
	/// </summary>
	public static class ProgressionTier
	{
		public const float Inf = 1e9f;
		private static float[] _t;
		private static bool[] _found;
		private static bool _built;
		internal static double BuildMs; internal static int BossRoutes, ShopRoutes, Recipes;

		/// <summary>True once the table is built.</summary>
		public static bool Ready => _t != null;

		/// <summary>The tier of an item type, or -1 when it has none (not built yet, or a type outside the table): callers then apply no discount.</summary>
		public static float Of(int type)
		{
			var t = _t;
			return t != null && type > 0 && type < t.Length && t[type] < Inf ? t[type] : -1f;
		}

		/// <summary>
		/// True for a damaging item the player does not craft or buy: no recipe makes it and no shop sells it (any shop, any condition), so it is found: a boss or enemy drop, a chest or shrine, fishing.
		/// False when the table is not built. <see cref="WeaponRank"/> ranks such a weapon a little above crafted ones of the same price.
		/// </summary>
		public static bool IsFound(int type)
		{
			var f = _found;
			return f != null && type > 0 && type < f.Length && f[type];
		}

		/// <summary>Builds the table (once per session). Never throws; on failure the table stays empty and nothing is discounted.</summary>
		public static void EnsureBuilt(Action<string> log = null)
		{
			if (_built || Main.netMode == NetmodeID.MultiplayerClient) return;
			_built = true;
			var sw = Stopwatch.StartNew();
			try { _t = Solve(Gather()); }
			catch (Exception e) { _t = null; _found = null; log?.Invoke("progression tiers failed: " + e.GetType().Name + ": " + e.Message); return; }
			BuildMs = sw.Elapsed.TotalMilliseconds;
			log?.Invoke($"progression tiers: {BossRoutes} boss-loot items, {ShopRoutes} shop items, {Recipes} recipes, {BuildMs:F0} ms");
		}

		internal static void Reset() { _t = null; _found = null; _built = false; }

		/// <summary>Self-check seam: installs a made-up table (and, optionally, which types count as found).</summary>
		internal static void SetForTest(float[] table, bool[] found = null) { _t = table; _found = found; _built = true; }

		// ------------------------------------------------------------------ data

		private static readonly Dictionary<int, float> VanillaBoss = new()
		{
			{ NPCID.KingSlime, 1f }, { NPCID.EyeofCthulhu, 2f }, { NPCID.EaterofWorldsHead, 3f }, { NPCID.BrainofCthulhu, 3f }, { NPCID.QueenBee, 4f }, { NPCID.SkeletronHead, 5f }, { NPCID.Deerclops, 6f },
			{ NPCID.WallofFlesh, 7f }, { NPCID.QueenSlimeBoss, 8f }, { NPCID.Retinazer, 9f }, { NPCID.Spazmatism, 9f }, { NPCID.TheDestroyer, 10f }, { NPCID.SkeletronPrime, 11f }, { NPCID.Plantera, 12f }, { NPCID.Golem, 13f },
			{ NPCID.DukeFishron, 14f }, { NPCID.HallowBoss, 15f }, { NPCID.CultistBoss, 17f }, { NPCID.MoonLordCore, 18f },
			{ NPCID.Pumpking, 13.25f }, { NPCID.MourningWood, 13.25f }, { NPCID.Everscream, 13.5f }, { NPCID.SantaNK1, 13.5f }, { NPCID.IceQueen, 13.5f }, { NPCID.Mothron, 11.5f }, { NPCID.PirateShip, 7.7f }, { NPCID.DD2Betsy, 16f },
			{ NPCID.MartianSaucerCore, 13.75f }, { NPCID.GoblinSummoner, 3.4f }, { NPCID.DD2OgreT2, 11.01f }, { NPCID.DD2DarkMageT3, 3.7f },
		};

		// Modded bosses by NPC internal name (Calamity's registered Boss Checklist values; Thorium's from its public registrations).
		private static readonly (string name, float p)[] ModBoss =
		{
			("DesertScourgeHead", 1.6f), ("GiantClam", 1.61f), ("Crabulon", 2.7f), ("HiveMind", 3.98f), ("PerforatorHive", 3.99f), ("SlimeGodCore", 6.7f), ("EbonianPaladin", 6.7f), ("CrimulanPaladin", 6.7f),
			("Cryogen", 8.5f), ("AquaticScourgeHead", 9.5f), ("CragmawMire", 9.52f), ("BrimstoneElemental", 10.5f), ("CalamitasClone", 11.7f), ("Calamitas", 11.7f), ("GreatSandShark", 12.09f),
			("Leviathan", 12.8f), ("Anahita", 12.8f), ("AstrumAureus", 12.81f), ("PlaguebringerGoliath", 14.5f), ("RavagerBody", 16.5f), ("AstrumDeusHead", 17.5f), ("ProfanedGuardianCommander", 18.5f),
			("Bumblefuck", 18.6f), ("Providence", 19f), ("CeaselessVoid", 19.6f), ("StormWeaverHead", 19.61f), ("Signus", 19.62f), ("Polterghast", 20f), ("OldDuke", 20.5f), ("DevourerofGodsHead", 21f),
			("Yharon", 22f), ("Apollo", 22.99f), ("Artemis", 22.99f), ("AresBody", 22.99f), ("ThanatosHead", 22.99f), ("SupremeCalamitas", 23f), ("SupremeCataclysm", 23f), ("SupremeCatastrophe", 23f),
			("TheGrandThunderBird", 1.4f), ("GraniteEnergyStorm", 6.4f), ("TheBuriedChampion", 6.5f), ("TheStarScouter", 6.9f), ("BoreanStrider", 8.2f), ("FallenBeholder", 9.2f), ("CoznixTheFallenBeholder", 9.2f),
			("Lich", 11.6f), ("TheLich", 11.6f), ("Abyssion", 14.2f), ("ForgottenOne", 14.2f), ("TheQueenJellyfish", 3.4f), ("QueenJellyfish", 3.4f), ("Viscount", 4.4f), ("TheViscount", 4.4f),
			("BuriedChampion", 6.5f), ("StarScouter", 6.9f), ("LichHeadless", 11.6f), ("Primordials", 19.5f), ("RealityBreaker", 19.5f), ("ThePrimordials", 19.5f), ("DreamEater", 19.5f), ("Omnicide", 19.5f), ("SlagFury", 19.5f),
		};

		// When a town NPC can first be expected, so its shop is no earlier.
		private static readonly Dictionary<string, float> NpcArrival = new()
		{
			{ "Merchant", 0f }, { "Nurse", 0f }, { "Demolitionist", 0f }, { "DyeTrader", 0f }, { "Angler", 0f }, { "Zoologist", 0f }, { "Dryad", 0f }, { "Painter", 0f }, { "ArmsDealer", 0f }, { "Stylist", 3f },
			{ "GoblinTinkerer", 3.3f }, { "WitchDoctor", 4f }, { "Mechanic", 5f }, { "Clothier", 5f }, { "Wizard", 7f }, { "TaxCollector", 7f }, { "Tavernkeep", 7f }, { "Pirate", 7.7f }, { "Steampunker", 9f },
			{ "Truffle", 11f }, { "Cyborg", 12f }, { "Princess", 12f }, { "SantaClaus", 13.5f }, { "SkeletonMerchant", 0f }, { "TravellingMerchant", 0f }, { "BestiaryGirl", 0f }, { "GolferGirl", 0f }, { "Golfer", 0f },
		};

		/// <summary>Ores and natural materials with no craft or boss route, by availability (-1 = not in the table).</summary>
		private static float RawTier(Item it)
		{
			if (it.type >= ItemID.Count) return -1f;
			switch (it.type)
			{
				case ItemID.CopperOre: case ItemID.TinOre: case ItemID.IronOre: case ItemID.LeadOre: case ItemID.SilverOre: case ItemID.TungstenOre: case ItemID.GoldOre: case ItemID.PlatinumOre: return 0f;
				case ItemID.DemoniteOre: case ItemID.CrimtaneOre: case ItemID.Meteorite: return 2f;
				case ItemID.Obsidian: return 3f;
				case ItemID.Hellstone: return 5f;
				case ItemID.CobaltOre: case ItemID.PalladiumOre: case ItemID.MythrilOre: case ItemID.OrichalcumOre: case ItemID.AdamantiteOre: case ItemID.TitaniumOre: return 7f;
				case ItemID.SoulofLight: case ItemID.SoulofNight: case ItemID.CrystalShard: return 7f;
				case ItemID.SoulofFright: case ItemID.SoulofMight: case ItemID.SoulofSight: return 9f;
				case ItemID.ChlorophyteOre: return 11f;
				case ItemID.ShroomiteBar: case ItemID.SpectreBar: return 12f;
				case ItemID.BeetleHusk: return 13f;
				case ItemID.LunarOre: return 18f;
				case ItemID.FragmentSolar: case ItemID.FragmentVortex: case ItemID.FragmentNebula: case ItemID.FragmentStardust: return 17.5f;
			}
			return -1f;
		}

		/// <summary>The stage a rarity implies where it means something (vanilla 0..11, and Calamity's custom rarities, all past Moon Lord). -1 for Thorium and other custom rarities and for a negative (Expert / Master) one.</summary>
		public static float RarityFloor(Item it)
		{
			if (it == null) return -1f;
			int r = it.rare;
			if (r >= 0 && r <= 11) return r switch { 0 => 0f, 1 => 0.5f, 2 => 1.5f, 3 => 4.5f, 4 => 7f, 5 => 9.5f, 6 => 11.5f, 7 => 12.5f, 8 => 13.5f, 9 => 16f, 10 => 17f, _ => 18f };
			if (r > 11 && it.ModItem?.Mod.Name == "CalamityMod") return 19f;
			return -1f;
		}

		private static float BossP(int npcType)
		{
			if (VanillaBoss.TryGetValue(npcType, out float p)) return p;
			var n = NPCLoader.GetNPC(npcType);
			if (n == null) return -1f;
			string name = n.Name;
			float best = -1f; int bl = 0;
			foreach (var (m, v) in ModBoss) if (name == m || (name.StartsWith(m) && m.Length > bl && m.Length >= 5)) { if (name == m) return v; best = v; bl = m.Length; }
			return best;
		}

		private static void Collect(IEnumerable<IItemDropRule> rules, HashSet<int> into)
		{
			foreach (var rule in rules)
			{
				var l = new List<DropRateInfo>();
				try { rule.ReportDroprates(l, new DropRateInfoChainFeed(1f)); } catch (Exception) { }
				foreach (var d in l) if (!RemixOnly(d)) into.Add(d.itemId);
			}
		}

		/// <summary>True for a drop that exists only in a Remix-seed world (King Slime lists Keybrand under "Drops: In a Remix world"). Those rows are ignored.</summary>
		private static bool RemixOnly(DropRateInfo d)
		{
			if (d.conditions == null) return false;
			foreach (var c in d.conditions)
			{
				if (c.GetType().Name == "RemixSeed") return true;
				string desc = ""; try { desc = c.GetConditionDescription() ?? ""; } catch (Exception) { }
				if (desc.Contains("In a Remix world")) return true;
			}
			return false;
		}

		/// <summary>A shop condition that is a special event or world seed (the Princess's anniversary stock, Halloween, Christmas, a party): no progression route.</summary>
		private static bool EventOnly(string k) => k.Contains("Anniversary") || k.Contains("Halloween") || k.Contains("Xmas") || k.Contains("Christmas") || k.Contains("Party") || k.Contains("Celebration") || k.Contains("Remix") || k.Contains("Drunk") || k.Contains("NotTheBees") || k.Contains("ForTheWorthy") || k.Contains("TheConstant") || k.Contains("DontStarve") || k.Contains("GetFixed") || k.Contains("TenthAnniversary");

		private static float CondP(IEnumerable<Condition> conds)
		{
			float p = 0f;
			if (conds == null) return 0f;
			foreach (var c in conds) { float v = CondKeyP(c.Description?.Key ?? "?"); if (v > p) p = v; }
			return p;
		}

		private static float CondKeyP(string k)
		{
			if (k.Contains("PreHardmode") || k.Contains("Conditions.Not")) return 0f;
			(string frag, float p)[] map =
			{
				("DownedBoss1", 2f), ("DownedBoss2", 3f), ("DownedBoss3", 5f), ("DownedFallenBeholder", 9.2f), ("DownedLich", 11.6f), ("DownedDoG", 21f), ("DownedKingSlime", 1f), ("DownedEyeOfCthulhu", 2f), ("DownedEowOrBoc", 3f), ("DownedEaterOfWorlds", 3f), ("DownedBrain", 3f), ("DownedQueenBee", 4f), ("DownedSkeletron", 5f), ("DownedDeerclops", 6f),
				("IsHardmode", 7f), ("Hardmode", 7f), ("DownedQueenSlime", 8f), ("DownedTwins", 9f), ("DownedMechBossAny", 9f), ("DownedDestroyer", 10f), ("DownedMechBossAll", 11f), ("DownedAllMechBosses", 11f), ("DownedAnyMech", 9f), ("DownedSkeletronPrime", 11f),
				("DownedPlantera", 12f), ("DownedGolem", 13f), ("DownedFishron", 14f), ("DownedEmpress", 15f), ("DownedCultist", 17f), ("DownedMoonLord", 18f), ("DownedPirates", 7.7f), ("DownedGoblin", 3.3f),
				("DownedClown", 7f), ("DownedFrost", 13.5f), ("DownedPumpkin", 13.25f), ("DownedMartian", 13.75f), ("Eclipse", 11.5f), ("BloodMoon", 2.5f),
			};
			foreach (var (f, p) in map) if (k.Contains(f)) return p;
			return 0f;
		}

		// ------------------------------------------------------------------ build

		private sealed class Data
		{
			public Dictionary<int, float> Boss = new(), Shop = new();
			public HashSet<int> Sold = new(); // sold by any shop, whatever its conditions
			public Dictionary<int, List<Recipe>> RecipesFor = new();
			public Dictionary<int, List<int>> TileItems = new();
		}

		private static Data Gather()
		{
			var d = new Data();
			// boss loot: the boss NPC's rules, and the rules of any treasure bag it drops
			for (int t = 1; t < NPCLoader.NPCCount; t++)
			{
				NPC s; try { s = ContentSamples.NpcsByNetId.TryGetValue(t, out var q) ? q : null; } catch { s = null; }
				if (s == null) continue;
				float p = BossP(t);
				if (p < 0f) continue;
				var items = new HashSet<int>();
				try { Collect(Main.ItemDropsDB.GetRulesForNPCID(t, false), items); } catch (Exception) { }
				foreach (var id in items.ToList())
					if (id > 0 && id < ItemID.Sets.BossBag.Length && ItemID.Sets.BossBag[id]) { try { Collect(Main.ItemDropsDB.GetRulesForItemID(id), items); } catch (Exception) { } }
				foreach (var id in items)
					if (!d.Boss.TryGetValue(id, out float old) || p < old) d.Boss[id] = p;
			}
			BossRoutes = d.Boss.Count;
			// shops: each entry's conditions count. A random-stock shop (Travelling / Skeleton Merchant) gives no route to an unconditional entry; an NPC of unknown arrival needs a usable condition.
			foreach (var shop in NPCShopDatabase.AllShops)
			{
				string[] parts = shop.FullName.Split('/'); string npcName = parts.Length >= 2 ? parts[parts.Length - 2] : "?"; bool vanillaNpc = parts[0] == "Terraria";
				bool randomPool = vanillaNpc && (npcName == "TravellingMerchant" || npcName == "SkeletonMerchant");
				float arrival = vanillaNpc && NpcArrival.TryGetValue(npcName, out float av) ? av : -1f;
				foreach (var e in shop.ActiveEntries)
				{
					var item = e.Item; if (item == null || item.IsAir) continue;
					d.Sold.Add(item.type);
					float p = CondP(e.Conditions);
					bool unknownMod = e.Conditions != null && e.Conditions.Any(c => (c.Description?.Key ?? "").StartsWith("Mods.") && CondKeyP(c.Description.Key) <= 0f);
					if (unknownMod || e.Conditions != null && e.Conditions.Any(c => EventOnly(c.Description?.Key ?? ""))) continue;
					if (randomPool && p <= 0f) continue;
					if (arrival < 0f && p <= 0f) continue;
					p = Math.Max(p, Math.Max(0f, arrival));
					if (!d.Shop.TryGetValue(item.type, out float old) || p < old) d.Shop[item.type] = p;
				}
			}
			ShopRoutes = d.Shop.Count;
			// recipes and the stations' items
			Recipes = 0;
			for (int i = 0; i < Recipe.numRecipes; i++)
			{
				var r = Main.recipe[i]; if (r == null || r.createItem == null || r.createItem.IsAir) continue;
				if (!d.RecipesFor.TryGetValue(r.createItem.type, out var l)) d.RecipesFor[r.createItem.type] = l = new List<Recipe>();
				l.Add(r); Recipes++;
			}
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				var s = ContentSamples.ItemsByType.TryGetValue(t, out var q) ? q : null; if (s == null || s.createTile < 0) continue;
				if (!d.TileItems.TryGetValue(s.createTile, out var l)) d.TileItems[s.createTile] = l = new List<int>();
				l.Add(t);
			}
			return d;
		}

		private sealed class Rec { public int Out; public int[][] Alts; public int[] Tiles; public float CondP; }

		/// <summary>The fixpoint: every item starts unreached; direct routes seed it, recipes relax it to the lowest consistent tier, then whatever nothing reaches takes its rarity or value signal.</summary>
		private static float[] Solve(Data d)
		{
			int N = ItemLoader.ItemCount;
			var T = new float[N]; var floors = new float[N]; var samples = new Item[N]; var found = new bool[N];
			for (int t = 0; t < N; t++) { T[t] = Inf; floors[t] = -1f; }
			for (int t = 1; t < N; t++)
			{
				samples[t] = ContentSamples.ItemsByType.TryGetValue(t, out var q) ? q : null;
				if (samples[t] == null) continue;
				floors[t] = RarityFloor(samples[t]);
				float best = Inf; bool raw = false;
				if (d.Boss.TryGetValue(t, out float bp)) best = Math.Min(best, bp);
				if (d.Shop.TryGetValue(t, out float sp)) best = Math.Min(best, sp);
				float rt = RawTier(samples[t]); if (rt >= 0f && rt < best) { best = rt; raw = true; }
				// a plain common material (no damage, common rarity) that a boss happens to drop is not itself late
				if (best < Inf && !raw && samples[t].damage <= 0 && floors[t] >= 0f && floors[t] <= 2f && best > floors[t] + 4f) best = floors[t];
				if (best < Inf) T[t] = Math.Max(best, floors[t]);
				found[t] = samples[t].damage > 0 && !d.RecipesFor.ContainsKey(t) && !d.Sold.Contains(t) && !raw;
			}
			_found = found;
			var recs = new List<Rec>();
			foreach (var kv in d.RecipesFor)
				foreach (var r in kv.Value)
				{
					var alts = new List<int[]>();
					foreach (var ing in r.requiredItem)
					{
						var set = new HashSet<int> { ing.type };
						foreach (var gid in r.acceptedGroups)
							if (RecipeGroup.recipeGroups.TryGetValue(gid, out var grp) && grp.ContainsItem(ing.type)) foreach (var a in grp.ValidItems) set.Add(a);
						alts.Add(set.Where(x => x > 0 && x < N).ToArray());
					}
					recs.Add(new Rec { Out = kv.Key, Alts = alts.ToArray(), Tiles = r.requiredTile.Where(x => x >= 0).ToArray(), CondP = (r.Conditions != null && r.Conditions.Count > 0) ? CondP(r.Conditions) : 0f });
				}
			var tileT = new Dictionary<int, float>();
			float Tile(int tile) => tileT.TryGetValue(tile, out float v) ? v : 0f; // natural-only stations (altars) gate nothing
			for (int round = 0; round < 3; round++)
			{
				for (int pass = 0; pass < 80; pass++)
				{
					tileT.Clear();
					foreach (var kv in d.TileItems) { float m = Inf; foreach (var id in kv.Value) if (id < N) m = Math.Min(m, T[id]); tileT[kv.Key] = m; }
					bool changed = false;
					foreach (var rc in recs)
					{
						if (rc.Out >= N) continue;
						float m = rc.CondP; bool dead = false;
						for (int i = 0; i < rc.Alts.Length; i++)
						{
							float b = Inf; foreach (var a in rc.Alts[i]) if (T[a] < b) b = T[a];
							if (b >= Inf) { dead = true; break; }
							if (b > m) m = b;
						}
						if (dead) continue;
						foreach (var tile in rc.Tiles) { float tt = Tile(tile); if (tt >= Inf) { dead = true; break; } if (tt > m) m = tt; }
						if (dead) continue;
						if (floors[rc.Out] > m) m = floors[rc.Out];
						if (m < T[rc.Out] - 1e-4f) { T[rc.Out] = m; changed = true; }
					}
					if (!changed) break;
				}
				if (round == 2) break;
				// items nothing reaches are UNKNOWN: round 0 the base materials nothing crafts, round 1 whatever is still unreached (cyclic-only items). Rarity, else a value signal; never tier 0 by default.
				for (int t = 1; t < N; t++)
				{
					if (samples[t] == null || T[t] < Inf) continue;
					if (round == 0 && d.RecipesFor.ContainsKey(t)) continue;
					if (floors[t] >= 0f) T[t] = floors[t];
					else if (samples[t].value >= 500000) T[t] = Math.Clamp((float)(Math.Log10(samples[t].value) - 2.5) * 4.5f, 0f, 18f);
					else T[t] = Math.Clamp((float)(Math.Log10(Math.Max(1, samples[t].value)) - 2.5) * 2.25f, 0f, 9f);
				}
			}
			return T;
		}
	}

	/// <summary>Builds <see cref="ProgressionTier"/> when a world loads (every item, recipe, shop and drop table is final by then).</summary>
	public class ProgressionTierSystem : ModSystem
	{
		public override void OnWorldLoad() => ProgressionTier.EnsureBuilt(s => Mod.Logger.Info("Levy " + s));
		public override void Unload() => ProgressionTier.Reset();
	}
}
