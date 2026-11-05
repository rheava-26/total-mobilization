using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEST-ONLY PROTOTYPE of a "how far into progression is this weapon" calculator (nothing in the live picker uses it). Enabled by LEVY_TIERPROBE=<out.tsv> (plus LEVY_PICKPROBE=1 to get the server tick).
	/// Progression scale = Boss Checklist's (King Slime 1, Eye 2, Evil boss 3, Bee 4, Skeletron 5, Deerclops 6, WoF 7, Queen Slime 8, Twins 9, Destroyer 10, Prime 11, Plantera 12, Golem 13, Fishron 14,
	/// Empress 15, Cultist 17, Moon Lord 18); Calamity's own registered values and Thorium's are used for their bosses. Boss Checklist itself is NOT installed in the test rig, so the data it would expose
	/// (<c>loot</c> per boss = EntryInfo.cs:89-109) is rebuilt from the same source it reads: Main.ItemDropsDB rules of each boss NPC and of its treasure bag.
	/// Routes to an item (the tier is the MIN over routes that exist): boss drop (that boss's progression), shop entry (progression of its Condition), recipe (MAX over ingredients and stations, recursive, cycle-safe),
	/// a hand table for raw ore / natural materials. Items with no route fall back to the rarity/value signals.
	/// </summary>
	public static class ProgressionTierProbe
	{
		public const float Inf = 1e9f;
		private static readonly Dictionary<int, float> _route = new(); private static readonly Dictionary<int, string> _src = new(); private static readonly HashSet<int> _busy = new();
		private static Dictionary<int, float> _bossLoot = new(), _shop = new();
		private static Dictionary<int, string> _bossLootSrc = new(), _shopSrc = new();
		private static Dictionary<int, float> _tileTier = new();
		private static Dictionary<int, List<Recipe>> _recipesFor = new();
		private static Dictionary<int, List<int>> _tileItems = new();
		private static readonly Dictionary<string, int> _condKeys = new();
		public static readonly List<string> Notes = new();

		// --- vanilla boss progression (Boss Checklist BossTracker.cs:~13-30) ---
		private static readonly Dictionary<int, float> VanillaBoss = new()
		{
			{ NPCID.KingSlime, 1f }, { NPCID.EyeofCthulhu, 2f }, { NPCID.EaterofWorldsHead, 3f }, { NPCID.BrainofCthulhu, 3f }, { NPCID.QueenBee, 4f }, { NPCID.SkeletronHead, 5f }, { NPCID.Deerclops, 6f },
			{ NPCID.WallofFlesh, 7f }, { NPCID.QueenSlimeBoss, 8f }, { NPCID.Retinazer, 9f }, { NPCID.Spazmatism, 9f }, { NPCID.TheDestroyer, 10f }, { NPCID.SkeletronPrime, 11f }, { NPCID.Plantera, 12f }, { NPCID.Golem, 13f },
			{ NPCID.DukeFishron, 14f }, { NPCID.HallowBoss, 15f }, { NPCID.CultistBoss, 17f }, { NPCID.MoonLordCore, 18f },
			{ NPCID.Pumpking, 13.25f }, { NPCID.MourningWood, 13.25f }, { NPCID.Everscream, 13.5f }, { NPCID.SantaNK1, 13.5f }, { NPCID.IceQueen, 13.5f }, { NPCID.Mothron, 11.5f }, { NPCID.PirateShip, 7.7f }, { NPCID.DD2Betsy, 16f },
			{ NPCID.MartianSaucerCore, 13.75f }, { NPCID.GoblinSummoner, 3.4f }, { NPCID.DD2OgreT2, 11.01f }, { NPCID.DD2DarkMageT3, 3.7f },
		};
		// --- modded boss progression by NPC internal name prefix (Calamity: WeakReferenceSupport.cs:141-181; Thorium: the comments in the same table + its public BC registrations) ---
		private static readonly (string name, float p)[] ModBoss =
		{
			("DesertScourgeHead", 1.6f), ("GiantClam", 1.61f), ("Crabulon", 2.7f), ("HiveMind", 3.98f), ("PerforatorHive", 3.99f), ("SlimeGodCore", 6.7f), ("EbonianPaladin", 6.7f), ("CrimulanPaladin", 6.7f),
			("Cryogen", 8.5f), ("AquaticScourgeHead", 9.5f), ("CragmawMire", 9.52f), ("BrimstoneElemental", 10.5f), ("CalamitasClone", 11.7f), ("Calamitas", 11.7f), ("GreatSandShark", 12.09f),
			("Leviathan", 12.8f), ("Anahita", 12.8f), ("AstrumAureus", 12.81f), ("PlaguebringerGoliath", 14.5f), ("RavagerBody", 16.5f), ("AstrumDeusHead", 17.5f), ("ProfanedGuardianCommander", 18.5f),
			("Bumblefuck", 18.6f), ("Providence", 19f), ("CeaselessVoid", 19.6f), ("StormWeaverHead", 19.61f), ("Signus", 19.62f), ("Polterghast", 20f), ("OldDuke", 20.5f), ("DevourerofGodsHead", 21f),
			("Yharon", 22f), ("Apollo", 22.99f), ("Artemis", 22.99f), ("AresBody", 22.99f), ("ThanatosHead", 22.99f), ("SupremeCalamitas", 23f), ("SupremeCataclysm", 23f), ("SupremeCatastrophe", 23f),
			// Thorium
			("TheGrandThunderBird", 1.4f), ("GraniteEnergyStorm", 6.4f), ("TheBuriedChampion", 6.5f), ("TheStarScouter", 6.9f), ("BoreanStrider", 8.2f), ("FallenBeholder", 9.2f), ("CoznixTheFallenBeholder", 9.2f),
			("Lich", 11.6f), ("TheLich", 11.6f), ("Abyssion", 14.2f), ("ForgottenOne", 14.2f), ("TheQueenJellyfish", 3.4f), ("QueenJellyfish", 3.4f), ("Viscount", 4.4f), ("TheViscount", 4.4f),
			("BuriedChampion", 6.5f), ("StarScouter", 6.9f), ("LichHeadless", 11.6f), ("Primordials", 19.5f), ("RealityBreaker", 19.5f), ("ThePrimordials", 19.5f), ("DreamEater", 19.5f), ("Omnicide", 19.5f), ("SlagFury", 19.5f),
		};

		private static readonly Dictionary<string, float> NpcArrival = new()
		{
			{ "Merchant", 0f }, { "Nurse", 0f }, { "Demolitionist", 0f }, { "DyeTrader", 0f }, { "Angler", 0f }, { "Zoologist", 0f }, { "Dryad", 0f }, { "Painter", 0f }, { "ArmsDealer", 0f }, { "Stylist", 3f },
			{ "GoblinTinkerer", 3.3f }, { "WitchDoctor", 4f }, { "Mechanic", 5f }, { "Clothier", 5f }, { "Wizard", 7f }, { "TaxCollector", 7f }, { "Tavernkeep", 7f }, { "Pirate", 7.7f }, { "Steampunker", 9f },
			{ "Truffle", 11f }, { "Cyborg", 12f }, { "Princess", 12f }, { "SantaClaus", 13.5f }, { "SkeletonMerchant", 0f }, { "TravellingMerchant", 0f }, { "BestiaryGirl", 0f }, { "GolferGirl", 0f }, { "Golfer", 0f },
		};

		// Ores / natural materials with no craft or boss route (BC-scale availability). Lookups by ItemID or by name.
		private static float RawTier(Item it)
		{
			if (it.type < ItemID.Count)
			{
				switch (it.type)
				{
					case ItemID.CopperOre: case ItemID.TinOre: case ItemID.IronOre: case ItemID.LeadOre: case ItemID.SilverOre: case ItemID.TungstenOre: case ItemID.GoldOre: case ItemID.PlatinumOre: return 0f;
					case ItemID.DemoniteOre: case ItemID.CrimtaneOre: case ItemID.Meteorite: return 2f;
					case ItemID.Obsidian: return 3f; // lava + water; late pre-HM
					case ItemID.Hellstone: return 5f;
					case ItemID.CobaltOre: case ItemID.PalladiumOre: case ItemID.MythrilOre: case ItemID.OrichalcumOre: case ItemID.AdamantiteOre: case ItemID.TitaniumOre: return 7f;
					case ItemID.SoulofLight: case ItemID.SoulofNight: case ItemID.CrystalShard: return 7f;
					case ItemID.ChlorophyteOre: return 11f;
					case ItemID.ShroomiteBar: case ItemID.SpectreBar: return 12f;
					case ItemID.BeetleHusk: return 13f;
					case ItemID.LunarOre: return 18f;
					case ItemID.FragmentSolar: case ItemID.FragmentVortex: case ItemID.FragmentNebula: case ItemID.FragmentStardust: return 17.5f;
				}
				if (it.type == ItemID.SoulofFright || it.type == ItemID.SoulofMight || it.type == ItemID.SoulofSight) return 9f;
			}
			string n = it.ModItem?.Name ?? "";
			if (it.ModItem != null)
			{
				if (n.Contains("Ore") && it.createTile >= 0) return -1f; // modded ores: leave to signals
			}
			return -1f;
		}

		/// <summary>Progression a rarity implies (vanilla rarity ladder: Blue/Green early pre-boss, Orange ~Skeletron, LightRed ~WoF, Pink ~mechs, LightPurple ~Plantera, Lime/Yellow ~Golem, Cyan..Red lunar, Purple Moon Lord, modded > 11 end-game).</summary>
		public static float RarityP(int rare) => rare switch { <= -1 => 12f, 0 => 0f, 1 => 1f, 2 => 2f, 3 => 4.5f, 4 => 7f, 5 => 9.5f, 6 => 11.5f, 7 => 12.5f, 8 => 13.5f, 9 => 16f, 10 => 17f, 11 => 18f, _ => 19f };

		/// <summary>Boss Checklist scale of a boss NPC, or -1.</summary>
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
				foreach (var d in l) into.Add(d.itemId);
			}
		}

		private static float CondP(IEnumerable<Condition> conds, out string desc)
		{
			float p = 0f; desc = "";
			if (conds == null) return 0f;
			foreach (var c in conds)
			{
				string key = c.Description?.Key ?? "?";
				_condKeys[key] = _condKeys.GetValueOrDefault(key) + 1;
				float v = CondKeyP(key);
				if (v > p) { p = v; desc = key; }
			}
			return p;
		}

		private static float CondKeyP(string k)
		{
			if (k.Contains("PreHardmode") || k.Contains("Conditions.Not") || k.EndsWith("Boss1") && false) return 0f;
			(string frag, float p)[] map =
			{
				("DownedBoss1", 2f), ("DownedBoss2", 3f), ("DownedBoss3", 5f), ("DownedFallenBeholder", 9.2f), ("DownedLich", 11.6f), ("DownedDoG", 21f), ("DownedKingSlime", 1f), ("DownedEyeOfCthulhu", 2f), ("DownedEowOrBoc", 3f), ("DownedEaterOfWorlds", 3f), ("DownedBrain", 3f), ("DownedQueenBee", 4f), ("DownedSkeletron", 5f), ("DownedDeerclops", 6f),
				("IsHardmode", 7f), ("Hardmode", 7f), ("DownedQueenSlime", 8f), ("DownedTwins", 9f), ("DownedMechBossAny", 9f), ("DownedDestroyer", 10f), ("DownedMechBossAll", 11f), ("DownedSkeletronPrime", 11f),
				("DownedPlantera", 12f), ("DownedGolem", 13f), ("DownedFishron", 14f), ("DownedEmpress", 15f), ("DownedCultist", 17f), ("DownedMoonLord", 18f), ("DownedPirates", 7.7f), ("DownedGoblin", 3.3f),
				("DownedClown", 7f), ("DownedFrost", 13.5f), ("DownedPumpkin", 13.25f), ("DownedMartian", 13.75f), ("Eclipse", 11.5f), ("BloodMoon", 2.5f),
			};
			foreach (var (f, p) in map) if (k.Contains(f)) return p;
			return 0f;
		}

		public static void Build(StringBuilder log)
		{
			_route.Clear(); _src.Clear(); _busy.Clear(); _bossLoot.Clear(); _shop.Clear(); _bossLootSrc.Clear(); _shopSrc.Clear(); _tileTier.Clear(); _recipesFor.Clear(); _tileItems.Clear(); _leaf.Clear(); _condKeys.Clear(); Notes.Clear();
			// boss loot
			int bosses = 0; var unmapped = new List<string>();
			for (int t = 1; t < NPCLoader.NPCCount; t++)
			{
				NPC s; try { s = ContentSamples.NpcsByNetId.TryGetValue(t, out var q) ? q : null; } catch { s = null; }
				if (s == null) continue;
				float p = BossP(t);
				bool isBoss = s.boss || p >= 0f;
				if (!isBoss) continue;
				if (p < 0f) { unmapped.Add((s.ModNPC != null ? s.ModNPC.Mod.Name + "/" + s.ModNPC.Name : s.FullName)); continue; }
				bosses++;
				var items = new HashSet<int>();
				try { Collect(Main.ItemDropsDB.GetRulesForNPCID(t, false), items); } catch (Exception) { }
				foreach (var id in items.ToList())
				{
					bool bag = id < ItemID.Sets.BossBag.Length && ItemID.Sets.BossBag[id] || (id >= ItemID.Count && ItemLoader.GetItem(id) != null && ItemID.Sets.BossBag.Length > id && ItemID.Sets.BossBag[id]);
					if (bag) { try { Collect(Main.ItemDropsDB.GetRulesForItemID(id), items); } catch (Exception) { } }
				}
				string bn = s.ModNPC != null ? s.ModNPC.Mod.Name + "/" + s.ModNPC.Name : s.FullName;
				foreach (var id in items)
					if (!_bossLoot.TryGetValue(id, out float old) || p < old) { _bossLoot[id] = p; _bossLootSrc[id] = bn + "@" + p; }
			}
			log.AppendLine($"# boss NPCs mapped with a progression: {bosses}; boss-flag NPCs with NO mapping ({unmapped.Count}): {string.Join(", ", unmapped.Take(60))}");
			// shops: a shop route counts only when the selling NPC's arrival is known (vanilla table) or the conditions pin it (all conditions understood); a modded condition we cannot read voids the route
			foreach (var shop in NPCShopDatabase.AllShops)
			{
				string[] parts = shop.FullName.Split('/'); string npcName = parts.Length >= 2 ? parts[parts.Length - 2] : "?"; bool vanillaNpc = parts[0] == "Terraria";
				float arrival = vanillaNpc && NpcArrival.TryGetValue(npcName, out float av) ? av : -1f;
				foreach (var e in shop.ActiveEntries)
				{
					var item = e.Item; if (item == null || item.IsAir) continue;
					float p = CondP(e.Conditions, out string d);
					bool unknownMod = e.Conditions != null && e.Conditions.Any(c => (c.Description?.Key ?? "").StartsWith("Mods.") && CondKeyP(c.Description.Key) <= 0f);
					if (unknownMod) continue;
					if (arrival < 0f && p <= 0f) continue; // unknown seller, no usable condition
					p = Math.Max(p, Math.Max(0f, arrival));
					if (!_shop.TryGetValue(item.type, out float old) || p < old) { _shop[item.type] = p; _shopSrc[item.type] = shop.FullName + (d.Length > 0 ? ":" + d : ""); }
				}
			}
			log.AppendLine("# shop condition keys seen: " + string.Join("; ", _condKeys.OrderByDescending(k => k.Value).Take(60).Select(k => k.Key + " x" + k.Value + " ->" + CondKeyP(k.Key))));
			// recipes
			for (int i = 0; i < Recipe.numRecipes; i++)
			{
				var r = Main.recipe[i]; if (r == null || r.createItem == null || r.createItem.IsAir) continue;
				if (!_recipesFor.TryGetValue(r.createItem.type, out var l)) _recipesFor[r.createItem.type] = l = new List<Recipe>();
				l.Add(r);
			}
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				var s = ContentSamples.ItemsByType.TryGetValue(t, out var q) ? q : null; if (s == null || s.createTile < 0) continue;
				if (!_tileItems.TryGetValue(s.createTile, out var l)) _tileItems[s.createTile] = l = new List<int>();
				l.Add(t);
			}
		}

		private static float TileTier(int tile)
		{
			if (_tileTier.TryGetValue(tile, out float v)) return v;
			_tileTier[tile] = Inf; // cycle guard
			float best = Inf;
			if (_tileItems.TryGetValue(tile, out var items)) foreach (var id in items) best = Math.Min(best, Tier(id, out _));
			if (best >= Inf) best = 0f; // altars, natural-only tiles: no gating
			// vanilla stations whose placing item is cheap but whose recipes are the real gate are covered by their own item routes
			_tileTier[tile] = best;
			return best;
		}

		private static readonly Dictionary<int, double> _leaf = new(); private static readonly HashSet<int> _leafBusy = new();
		/// <summary>Total material cost (copper): the cheapest recipe's ingredients expanded recursively down to items nothing crafts, summed at their own item.value (a stack counts stack x value). An item with no recipe costs its own value.</summary>
		public static double LeafCost(int type)
		{
			if (_leaf.TryGetValue(type, out double c)) return c;
			Item samp = ContentSamples.ItemsByType.TryGetValue(type, out var q) ? q : null;
			double own = samp?.value ?? 0;
			if (!_leafBusy.Add(type)) return own;
			double best = double.MaxValue;
			if (_recipesFor.TryGetValue(type, out var recs))
			{
				foreach (var rec in recs)
				{
					double sum = 0;
					foreach (var ing in rec.requiredItem)
					{
						double b = LeafCost(ing.type);
						foreach (var gid in rec.acceptedGroups)
						{
							if (!RecipeGroup.recipeGroups.TryGetValue(gid, out var grp) || !grp.ContainsItem(ing.type)) continue;
							foreach (var alt in grp.ValidItems) b = Math.Min(b, LeafCost(alt));
						}
						sum += b * ing.stack;
					}
					sum /= Math.Max(1, rec.createItem.stack);
					if (sum > 0 && sum < best) best = sum;
				}
			}
			_leafBusy.Remove(type);
			double res = best == double.MaxValue ? own : best;
			_leaf[type] = res; return res;
		}

		/// <summary>Route tier of an item type (min over its routes), Inf when nothing reaches it. src names the winning route.</summary>
		public static float Tier(int type, out string src)
		{
			if (_route.TryGetValue(type, out float r)) { src = _src.GetValueOrDefault(type, "?"); return r; }
			if (!_busy.Add(type)) { src = "cycle"; return Inf; }
			float best = Inf; string bs = "none";
			void Try(float p, string s) { if (p < best) { best = p; bs = s; } }
			if (_bossLoot.TryGetValue(type, out float bp)) Try(bp, "boss:" + _bossLootSrc[type]);
			if (_shop.TryGetValue(type, out float sp)) Try(sp, "shop:" + _shopSrc[type]);
			Item samp = ContentSamples.ItemsByType.TryGetValue(type, out var q) ? q : null;
			if (samp != null) { float rt = RawTier(samp); if (rt >= 0f) Try(rt, "raw"); }
			if (_recipesFor.TryGetValue(type, out var recs))
			{
				foreach (var rec in recs)
				{
					float m = 0f; string lim = "";
					foreach (var ing in rec.requiredItem)
					{
						float b = Inf;
						b = Math.Min(b, Tier(ing.type, out _));
						foreach (var gid in rec.acceptedGroups)
						{
							if (!RecipeGroup.recipeGroups.TryGetValue(gid, out var grp) || !grp.ContainsItem(ing.type)) continue;
							foreach (var alt in grp.ValidItems) { b = Math.Min(b, Tier(alt, out _)); if (b <= 0f) break; }
						}
						if (b > m) { m = b; lim = ing.Name; }
						if (m >= Inf) break;
					}
					if (m < Inf) foreach (var tile in rec.requiredTile) { if (tile < 0) break; float tt = TileTier(tile); if (tt > m) { m = tt; lim = "tile" + tile; } }
					Try(m, "recipe<-" + lim);
				}
			}
			_busy.Remove(type);
			if (best >= Inf && samp != null) { bool custom = (samp.rare > 11 || samp.rare < -1) && samp.ModItem?.Mod.Name != "CalamityMod"; best = custom ? Math.Clamp((float)(Math.Log10(Math.Max(1, samp.value)) - 2.5) * 4.5f, 0f, 18f) : RarityP(samp.rare); bs = custom ? "sig:value" : "sig:rarity"; } // chest loot, enemy drops, anything with no reachable route: the rarity signal stands in (also stops Inf poisoning the recipes that use it)
			_route[type] = best; _src[type] = bs;
			src = bs; return best;
		}
	}
}
