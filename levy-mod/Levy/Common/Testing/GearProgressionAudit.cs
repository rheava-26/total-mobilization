using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEST-ONLY structured gear-progression audit (changes no game behaviour; does nothing unless LEVY_GEARAUDIT=&lt;output path&gt; is set). Simulates a playthrough on ONE army: ten levies in junk gear, then gear added in the order a
	/// player gets it (ore armor, Ninja, Shadow/Crimson, Meteor, Jungle, Necro, Molten, early Hardmode, ... Shroomite) with weapons by tier per class and accessories at the same points. After each step the levies run the REAL
	/// <see cref="ArmoryTE.UpgradeWeapons"/> / <see cref="ArmoryTE.IssueGear"/> until nothing changes, and the per-levy tables, the army floor / median / max and the FISHY list are logged.
	/// LEVY_GEARAUDIT_MODE=before|after|both (default both): "before" turns <see cref="ArmoryTE.FloorFirst"/> off (the first-come allocation) and restores the old class-less spread (see <see cref="WeaponAllocator.LegacySpreadRedeal"/>); "after" leaves the fixes on.
	/// Runs once, at PostSetupRecipes, on throwaway armories and soldiers; touches no world state.
	/// </summary>
	public class GearProgressionAudit : ModSystem
	{
		private sealed class Step { public string Name; public string[] ExtraHeads = Array.Empty<string>(); public string[][] Sets = Array.Empty<string[]>(); public string[] Weapons = Array.Empty<string>(); public string[] Accs = Array.Empty<string>(); }

		private static string[] S(params string[] p) => p;

		private static List<Step> Steps() => new List<Step>
		{
			new Step { Name = "Ore armor: Iron, Tungsten, Gold (+ ore weapons, Hermes Boots, Cloud in a Bottle)",
				Sets = new[] { S("IronHelmet","IronChainmail","IronGreaves"), S("TungstenHelmet","TungstenChainmail","TungstenGreaves"), S("GoldHelmet","GoldChainmail","GoldGreaves") },
				Weapons = S("IronBroadsword","GoldBroadsword","IronBow","GoldBow","TopazStaff","DiamondStaff","BlandWhip"),
				Accs = S("HermesBoots","CloudinaBottle","LuckyHorseshoe") },
			new Step { Name = "King Slime: Ninja armor",
				Sets = new[] { S("NinjaHood","NinjaShirt","NinjaPants") },
				Weapons = S("Katana","SlimeGun","SlimeStaff"),
				Accs = S("SlimeCrown") },
			new Step { Name = "Evil biome: Shadow and Crimson sets",
				Sets = new[] { S("ShadowHelmet","ShadowScalemail","ShadowGreaves"), S("CrimsonHelmet","CrimsonScalemail","CrimsonGreaves") },
				Weapons = S("LightsBane","BloodButcherer","Musket","TheUndertaker","Vilethorn","CrimsonRod","ThornWhip"),
				Accs = S("WormScarf","BrainOfConfusion","BandofStarpower") },
			new Step { Name = "Meteor armor",
				Sets = new[] { S("MeteorHelmet","MeteorSuit","MeteorLeggings") },
				Weapons = S("SpaceGun","PhoenixBlaster"),
				Accs = S("ObsidianShield") },
			new Step { Name = "Jungle armor",
				Sets = new[] { S("JungleHat","JungleShirt","JunglePants") },
				Weapons = S("BladeofGrass","Boomstick","HornetStaff"),
				Accs = S("AnkletoftheWind","FeralClaws") },
			new Step { Name = "Necro armor (Skeletron)",
				Sets = new[] { S("NecroHelmet","NecroBreastplate","NecroGreaves") },
				Weapons = S("Muramasa","WaterBolt","ImpStaff"),
				Accs = S("CobaltShield") },
			new Step { Name = "Molten armor",
				Sets = new[] { S("MoltenHelmet","MoltenBreastplate","MoltenGreaves") },
				Weapons = S("FieryGreatsword","MoltenFury","HellwingBow","FlowerofFire"),
				Accs = S("LavaWaders","MagicQuiver") },
			new Step { Name = "Early Hardmode: Cobalt and Palladium",
				Sets = new[] { S("CobaltHelmet","CobaltBreastplate","CobaltLeggings"), S("PalladiumHelmet","PalladiumBreastplate","PalladiumLeggings") },
				ExtraHeads = S("CobaltHat","CobaltMask","PalladiumHeadpiece","PalladiumMask"),
				Weapons = S("CobaltSword","PalladiumSword","CobaltRepeater","PalladiumRepeater","CrystalStorm","SpiderStaff"),
				Accs = S("PutridScent") },
			new Step { Name = "Mythril and Orichalcum",
				Sets = new[] { S("MythrilHelmet","MythrilChainmail","MythrilGreaves"), S("OrichalcumHelmet","OrichalcumBreastplate","OrichalcumLeggings") },
				ExtraHeads = S("MythrilHood","MythrilHat","OrichalcumHeadgear","OrichalcumMask"),
				Weapons = S("MythrilSword","OrichalcumSword","MythrilRepeater","OrichalcumRepeater","LaserRifle","PygmyStaff"),
				Accs = S("PaladinsShield","CharmofMyths") },
			new Step { Name = "Adamantite and Titanium",
				Sets = new[] { S("AdamantiteHelmet","AdamantiteBreastplate","AdamantiteLeggings"), S("TitaniumHelmet","TitaniumBreastplate","TitaniumLeggings") },
				ExtraHeads = S("AdamantiteHeadgear","AdamantiteMask","TitaniumHeadgear","TitaniumMask"),
				Weapons = S("AdamantiteSword","TitaniumSword","AdamantiteRepeater","TitaniumRepeater","RainbowRod","DeadlySphereStaff"),
				Accs = S("AnkhShield","FrozenTurtleShell") },
			new Step { Name = "Hallowed armor",
				Sets = new[] { S("HallowedHelmet","HallowedPlateMail","HallowedGreaves") }, ExtraHeads = S("HallowedMask","HallowedHeadgear"),
				Weapons = S("Excalibur","HallowedRepeater","ShadowbeamStaff"),
				Accs = S("AngelWings") },
			new Step { Name = "Post-Plantera: Chlorophyte, Spectre, Shroomite",
				Sets = new[] { S("ChlorophyteHeadgear","ChlorophytePlateMail","ChlorophyteGreaves"), S("SpectreHood","SpectreRobe","SpectrePants"), S("ShroomiteHeadgear","ShroomiteBreastplate","ShroomiteLeggings") },
				ExtraHeads = S("ChlorophyteHelmet","ChlorophyteMask","SpectreMask","ShroomiteHelmet","ShroomiteMask"),
				Weapons = S("ChlorophyteSaber","ChlorophyteShotbow","Megashark","SpectreStaff","TempestStaff"),
				Accs = S("CelestialShell","SharkToothNecklace","AnkhCharm") },
		};

		private static readonly string[][] StartSets =
		{
			S("WoodHelmet","WoodBreastplate","WoodGreaves"), S("WoodHelmet","WoodBreastplate","WoodGreaves"),
			S("CopperHelmet","CopperChainmail","CopperGreaves"), S("CopperHelmet","CopperChainmail","CopperGreaves"),
			S("CactusHelmet","CactusBreastplate","CactusLeggings"), S("CactusHelmet","CactusBreastplate","CactusLeggings"),
			S("CopperHelmet","WoodBreastplate","CactusLeggings"), // a mixed kit
			S("TinHelmet","TinChainmail","TinGreaves"), S("WoodHelmet","WoodBreastplate","WoodGreaves"), S("CactusHelmet","CactusBreastplate","CactusLeggings"),
		};
		// copper-tier weapons: 4 melee, 3 ranged, 2 magic, 1 summon
		private static readonly string[] StartWeapons = { "CopperShortsword", "CopperBroadsword", "TinShortsword", "WoodenSword", "CopperBow", "TinBow", "WoodenBow", "AmethystStaff", "AmberStaff", "SlimeStaff" };
		private static readonly Grade[] StartGrades = { Grade.Levy, Grade.Levy, Grade.Levy, Grade.Levy, Grade.ManAtArms, Grade.ManAtArms, Grade.ManAtArms, Grade.ManAtArms, Grade.Champion, Grade.Champion };
		private static readonly string[] StartAccs = { "Aglet", "Aglet", "Shackle", "ShinyRedBalloon", "BandofRegeneration" };

		public override void PostSetupRecipes()
		{
			string outp = Environment.GetEnvironmentVariable("LEVY_GEARAUDIT");
			if (string.IsNullOrEmpty(outp)) return;
			var sb = new StringBuilder();
			bool prevFloor = ArmoryTE.FloorFirst;
			try {
				MaterialCost.Ensure();
				string mode = Environment.GetEnvironmentVariable("LEVY_GEARAUDIT_MODE") ?? "both";
				if (Environment.GetEnvironmentVariable("LEVY_GEARAUDIT_DEBUG") == "1") { try { DebugSetBonus(sb); } catch (Exception e) { sb.AppendLine("DEBUG EXC " + e); } }
				WeaponRank.Force(WeaponRankMode.SellPrice, false);
				sb.AppendLine("# Levy gear progression audit. Levy " + Mod.Version + ". Weapon ranking forced to SellPrice (no world: no stage discount, no recency boost).");
				if (mode == "before" || mode == "both") { ArmoryTE.FloorFirst = false; WeaponAllocator.LegacySpreadRedeal = true; Playthrough(sb, "BEFORE (first-come: each levy upgrades from storage when his timer comes round)", null); }
				if (mode == "after" || mode == "both") { ArmoryTE.FloorFirst = true; WeaponAllocator.LegacySpreadRedeal = false; Playthrough(sb, "AFTER (raise the floor: worst-equipped levies take new gear first)", null); }
				// order sensitivity
				foreach (var m in new[] { false, true }) {
					ArmoryTE.FloorFirst = m; WeaponAllocator.LegacySpreadRedeal = !m;
					var order = new List<string>();
					foreach (var ord in new[] { "ascending ids", "descending ids", "shuffled A", "shuffled B" }) {
						var res = new StringBuilder();
						var sum = Playthrough(res, "", ord);
						order.Add($"{ord,-14}: final armor floor {sum.Floor:F0} / median {sum.Med:F0} / max {sum.Max:F0}, armor swaps {sum.Swaps}, weapon swaps {sum.WSwaps}, accessory swaps {sum.ASwaps}, fishy {sum.Fishy}");
					}
					sb.AppendLine("\n## ORDER SENSITIVITY (" + (m ? "AFTER" : "BEFORE") + "): the same playthrough with the levies' upgrade timers firing in a different order");
					foreach (var l in order) sb.AppendLine("    " + l);
				}
			}
			catch (Exception e) { sb.AppendLine("AUDIT EXCEPTION: " + e); }
			finally { ArmoryTE.FloorFirst = prevFloor; WeaponAllocator.LegacySpreadRedeal = false; WeaponRank.Release(); ArmoryTE.TestServed = null; }
			try { File.WriteAllText(outp, sb.ToString()); Mod.Logger.Info("GEARAUDIT: done -> " + outp); } catch (Exception e) { Mod.Logger.Warn("GEARAUDIT: could not write: " + e.Message); }
		}

		private static void DebugSetBonus(StringBuilder sb)
		{
			var p = new Player(); p.whoAmI = SoldierNPC.DummySlot; p.active = true; p.selectedItem = 0;
			foreach (var set in new[] { S("IronHelmet","IronChainmail","IronGreaves"), S("ShroomiteHeadgear","ShroomiteBreastplate","ShroomiteLeggings"), S("ShadowHelmet","ShadowScalemail","ShadowGreaves") }) {
				for (int v = 0; v < 8; v++) {
					int nm0 = Main.netMode; bool ds0 = Main.dedServ;
					if (v >= 3) Main.netMode = 0; if (v >= 4) Main.dedServ = false; if (v >= 5) { p.ResetEffects(); }
					
					try {
					for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
					for (int i = 0; i < 3; i++) p.armor[i] = Make(set[i]);
					p.setBonus = "";
					if (v >= 6) { p.head = p.armor[0].headSlot; p.body = p.armor[1].bodySlot; p.legs = p.armor[2].legSlot; }
					if (v >= 1) { p.ResetEffects(); }
					if (v >= 2) { p.UpdateEquips(SoldierNPC.DummySlot); }
					p.UpdateArmorSets(SoldierNPC.DummySlot);
					sb.AppendLine($"DEBUG {set[0]} variant {v}: setBonus='{p.setBonus}' dedServ={Main.dedServ} netMode={Main.netMode} lang={Terraria.Localization.Language.GetTextValue("ArmorSetBonus.MetalTier1")} def={p.statDefense}");
					} catch (Exception e) { sb.AppendLine("DEBUG variant " + v + " threw " + e.GetType().Name + ": " + e.Message); }
					finally { Main.netMode = nm0; Main.dedServ = ds0; }
				}
			}
		}

		private struct Summary { public float Floor, Med, Max; public int Swaps, WSwaps, ASwaps, Fishy; }

		private static Item Make(string spec) => WeaponChoiceProbe.MakeItem(spec.Contains('/') ? spec : "Vanilla/" + spec);

		private static string ClsName(DamageClass c) => c == null ? "-" : c == DamageClass.Melee ? "melee" : c == DamageClass.Ranged ? "ranged" : c == DamageClass.Magic ? "magic" : c == DamageClass.Summon ? "summon" : c.Name;

		private static float Median(List<float> v) { if (v.Count == 0) return 0; var s = v.OrderBy(x => x).ToList(); return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2f; }

		/// <summary>Runs the whole playthrough, appending the report to <paramref name="sb"/>. <paramref name="order"/> null = the report run (ascending ids); otherwise a quiet order-sensitivity run.</summary>
		private static Summary Playthrough(StringBuilder sb, string title, string order)
		{
			bool verbose = order == null;
			var tierOf = new Dictionary<int, int>(); // item type -> step it entered the game (0 = start)
			var te = new ArmoryTE();
			foreach (var ammo in new[] { "WoodenArrow", "MusketBall" }) { var am = Make(ammo); if (am != null) { am.stack = 999; te.Deposit(am); } } // ranged weapons need something to fire
			var levies = new List<SoldierRecord>();
			void Tier(Item it, int step) { if (!tierOf.ContainsKey(it.type)) tierOf[it.type] = step; }
			var unresolved = new List<string>();
			Item MakeT(string n, int step) { var it = Make(n); if (it == null) { unresolved.Add(n); return null; } Tier(it, step); return it; }

			for (int i = 0; i < 10; i++) {
				var r = new SoldierRecord { Id = i + 1, Grade = StartGrades[i] };
				var set = StartSets[i];
				r.Gear.Head = MakeT(set[0], 0) ?? new Item(); r.Gear.Body = MakeT(set[1], 0) ?? new Item(); r.Gear.Legs = MakeT(set[2], 0) ?? new Item();
				r.Gear.Weapon = MakeT(StartWeapons[i], 0) ?? new Item();
				if (i < StartAccs.Length) { var a = MakeT(StartAccs[i], 0); if (a != null) r.Gear.Accessories[0] = a; }
				levies.Add(r);
			}
			ArmoryTE.TestServed = levies;
			var rng = new Random(order == "shuffled A" ? 7 : 1234);
			Summary last = default;
			var swapLog = new List<(int levy, string slot, int from, int to, int round)>();
			if (verbose) { sb.AppendLine("\n\n# " + title); }
			try {
				var steps = Steps();
				for (int si = -1; si < steps.Count; si++) {
					var stepNo = si + 1;
					string stepName = si < 0 ? "Start: ten levies in junk gear" : steps[si].Name;
					var added = new List<string>();
					if (si >= 0) {
						foreach (var set in steps[si].Sets) {
							var items = set.Select(n => MakeT(n, stepNo)).ToArray();
							if (items.Any(x => x == null)) { continue; }
							foreach (var it in items) te.Deposit(it);
							added.Add("armor " + set[0].Replace("Helmet", "").Replace("Headgear", "").Replace("Hood", "").Replace("Mask", ""));
						}
						foreach (var n in steps[si].ExtraHeads) { var it = MakeT(n, stepNo); if (it == null) continue; te.Deposit(it); added.Add("head " + it.Name); }
						foreach (var n in steps[si].Weapons) { var it = MakeT(n, stepNo); if (it == null) continue; te.Deposit(it); added.Add("wpn " + it.Name); }
						foreach (var n in steps[si].Accs) { var it = MakeT(n, stepNo); if (it == null) continue; te.Deposit(it); added.Add("acc " + it.Name); }
					}
					// Pre-step snapshot
					var preArmor = levies.ToDictionary(l => l.Id, l => KitScore(l.Gear));
					var preTypes = levies.ToDictionary(l => l.Id, l => Types(l.Gear));
					var hist = new Dictionary<(int, string), List<int>>();
					foreach (var l in levies) foreach (var kv in Types(l.Gear)) hist[(l.Id, kv.Key)] = new List<int> { kv.Value };
					int rounds = 0; int armorSwaps = 0, weaponSwaps = 0, accSwaps = 0;
					if (si >= 0 || true) {
						for (int round = 1; round <= 14; round++) {
							if (verbose && Environment.GetEnvironmentVariable("LEVY_GEARAUDIT_TRACE") == "1") { bool on = round >= 8 && round <= 9; WeaponAllocator.Trace = on ? (Action<string>)(t => sb.AppendLine("TRACE s" + stepNo + " r" + round + " " + t)) : null; }
							var ord = levies.ToList();
							if (order == "descending ids") ord.Reverse();
							else if (order != null && order.StartsWith("shuffled")) ord = ord.OrderBy(_ => rng.Next()).ToList();
							bool any = false;
							foreach (var l in ord) {
								var snap = levies.ToDictionary(x => x.Id, x => Types(x.Gear));
								if (!l.Gear.HasWeapon || l.Gear.Accessories.Take(l.AccessorySlots).Any(a => a == null || a.IsAir)) te.IssueGear(l);
								te.UpgradeWeapons(l);
								foreach (var x in levies) {
									var after = Types(x.Gear);
									foreach (var kv in after) {
										if (snap[x.Id][kv.Key] == kv.Value) continue;
										any = true;
										string grp = kv.Key.StartsWith("acc") ? "acc" : kv.Key == "weapon" ? "weapon" : kv.Key == "sidearm" ? "sidearm" : "armor";
										if (grp == "armor") armorSwaps++; else if (grp == "weapon") weaponSwaps++; else if (grp == "acc") accSwaps++;
										swapLog.Add((x.Id, kv.Key, snap[x.Id][kv.Key], kv.Value, round));
										hist[(x.Id, kv.Key)].Add(kv.Value);
									}
								}
							}
							if (!any) break;
							rounds = round;
						}
					}
					// Report
					var rows = levies.Select(l => Row(l, tierOf)).ToList();
					var armSc = levies.Select(l => KitScore(l.Gear)).ToList();
					var defs = levies.Select(l => (float)Defense(l.Gear)).ToList();
					var dps = levies.Select(l => l.Gear.HasWeapon ? WeaponClassifier.Score(l.Gear.Weapon) : 0f).ToList();
					var fishy = Fishy(te, levies, preArmor, hist, tierOf, stepNo);
					last = new Summary { Floor = armSc.Min(), Med = Median(armSc), Max = armSc.Max(), Swaps = armorSwaps, WSwaps = weaponSwaps, ASwaps = accSwaps, Fishy = fishy.Count };
					if (verbose) {
						sb.AppendLine($"\n## Step {stepNo}: {stepName}");
						if (added.Count > 0) sb.AppendLine("Added: " + string.Join(", ", added));
						sb.AppendLine("| id | grade | armor (tier) | set bonus | defense | weapon (tier) | class | wpn score | accessories (tier) |");
						sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
						foreach (var r in rows) sb.AppendLine(r);
						sb.AppendLine($"Army: defense floor/median/max = {defs.Min():F0}/{Median(defs):F0}/{defs.Max():F0}; armor score floor/median/max = {armSc.Min():F0}/{Median(armSc):F0}/{armSc.Max():F0}; weapon score floor/median/max = {dps.Min():F0}/{Median(dps):F0}/{dps.Max():F0}.");
						sb.AppendLine($"Activity: {rounds} round(s) to settle; swaps this step: armor pieces {armorSwaps}, weapons {weaponSwaps}, accessories {accSwaps}.");
						sb.AppendLine("Storage left: " + StorageSummary(te, tierOf));
						if (fishy.Count == 0) sb.AppendLine("FISHY: none");
						else { sb.AppendLine("FISHY:"); foreach (var f in fishy) sb.AppendLine("- " + f); }
					}
					swapLog.Clear();
				}
			}
			finally { ArmoryTE.TestServed = null; }
			if (verbose && unresolved.Count > 0) sb.AppendLine("\n(items that did not resolve in this build and were skipped: " + string.Join(", ", unresolved.Distinct()) + ")");
			return last;
		}

		// ---- measurements

		internal static float KitScore(SoldierGear g)
		{
			float s = 0f;
			foreach (var it in new[] { g.Head, g.Body, g.Legs }) if (it != null && !it.IsAir) s += ArmoryTE.ArmorScoreOf(it);
			return s;
		}

		private static int Defense(SoldierGear g)
		{
			int d = 0;
			foreach (var it in new[] { g.Head, g.Body, g.Legs }) if (it != null && !it.IsAir) d += it.defense;
			foreach (var a in g.Accessories) if (a != null && !a.IsAir) d += a.defense;
			return d;
		}

		private static Dictionary<string, int> Types(SoldierGear g)
		{
			var d = new Dictionary<string, int> { ["head"] = g.Head?.type ?? 0, ["body"] = g.Body?.type ?? 0, ["legs"] = g.Legs?.type ?? 0, ["weapon"] = g.Weapon?.type ?? 0, ["sidearm"] = g.Sidearm?.type ?? 0 };
			if (g.Head == null || g.Head.IsAir) d["head"] = 0; if (g.Body == null || g.Body.IsAir) d["body"] = 0; if (g.Legs == null || g.Legs.IsAir) d["legs"] = 0;
			if (g.Weapon == null || g.Weapon.IsAir) d["weapon"] = 0; if (g.Sidearm == null || g.Sidearm.IsAir) d["sidearm"] = 0;
			for (int i = 0; i < g.Accessories.Length; i++) d["acc" + i] = g.Accessories[i] == null || g.Accessories[i].IsAir ? 0 : g.Accessories[i].type;
			return d;
		}

		private static string T(Item it, Dictionary<int, int> tierOf) => it == null || it.IsAir ? "-" : it.Name + "(S" + (tierOf.TryGetValue(it.type, out int t) ? t : -1) + ")";

		private static string Row(SoldierRecord l, Dictionary<int, int> tierOf)
		{
			var g = l.Gear;
			string armor = g.Head.type == 0 && g.Body.type == 0 && g.Legs.type == 0 ? "-" : string.Join(" / ", new[] { g.Head, g.Body, g.Legs }.Select(i => T(i, tierOf)));
			bool sb = !g.Head.IsAir && !g.Body.IsAir && !g.Legs.IsAir && ArmorDoctrine.FormsSetBonus(g.Head, g.Body, g.Legs);
			var cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			string accs = string.Join(", ", g.Accessories.Take(l.AccessorySlots).Select(a => T(a, tierOf)));
			return $"| {l.Id} | {l.Grade} ({l.AccessorySlots} acc) | {armor} | {(sb ? "yes" : "no")} | {Defense(g)} | {T(g.Weapon, tierOf)} | {ClsName(WeaponClassifier.ItemDamageClass(g.Weapon))} (armor {ClsName(cls)}) | {(g.HasWeapon ? WeaponClassifier.Score(g.Weapon).ToString("F0") : "0")} | {accs} |";
		}

		private static string StorageSummary(ArmoryTE te, Dictionary<int, int> tierOf)
		{
			var arm = new List<Item>(); var wpn = new List<Item>(); var acc = new List<Item>();
			foreach (var it in te.Storage) {
				if (it == null || it.IsAir) continue;
				if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) arm.Add(it);
				else if (it.accessory) acc.Add(it);
				else if (WeaponClassifier.IsIssuableWeapon(it)) wpn.Add(it);
			}
			string Join(List<Item> l) => l.Count == 0 ? "none" : string.Join(", ", l.OrderByDescending(i => i.value).Take(8).Select(i => T(i, tierOf))) + (l.Count > 8 ? $" (+{l.Count - 8} more)" : "");
			return $"armor pieces {arm.Count} [{Join(arm)}]; weapons {wpn.Count} [{Join(wpn)}]; accessories {acc.Count} [{Join(acc)}]";
		}

		// ---- FISHY detection (observed after the step settles)

		private static List<string> Fishy(ArmoryTE te, List<SoldierRecord> levies, Dictionary<int, float> preArmor, Dictionary<(int, string), List<int>> hist, Dictionary<int, int> tierOf, int stepNo)
		{
			var res = new List<string>();
			float margin = LevyConstants.AutoUpgradeScoreMargin;
			var stor = te.Storage.Where(i => i != null && !i.IsAir).ToList();

			// 1. a clearly better ARMOR PIECE sits unused in storage while a levy wears a worse one in that slot
			foreach (var l in levies) {
				var g = l.Gear;
				var parts = new[] { (0, "head", g.Head), (1, "body", g.Body), (2, "legs", g.Legs) };
				foreach (var (p, nm, worn) in parts) {
					float w = worn == null || worn.IsAir ? 0f : ArmoryTE.ArmorScoreOf(worn);
					var wcls = g.HasWeapon ? WeaponClassifier.ItemDamageClass(g.Weapon) : null;
					bool Fits(Item i) { // a stored piece is only a miss when a whole set of its stem is on hand and that set does not belong to another class
						var st = ArmoryTE.StemOf(i);
						var h = stor.Where(x => ArmoryTE.StemOf(x) == st && WeaponClassifier.IsArmorPiece(x, 0)).OrderByDescending(ArmoryTE.ArmorScoreOf).FirstOrDefault();
						var b = stor.Where(x => ArmoryTE.StemOf(x) == st && WeaponClassifier.IsArmorPiece(x, 1)).OrderByDescending(ArmoryTE.ArmorScoreOf).FirstOrDefault();
						var lg = stor.Where(x => ArmoryTE.StemOf(x) == st && WeaponClassifier.IsArmorPiece(x, 2)).OrderByDescending(ArmoryTE.ArmorScoreOf).FirstOrDefault();
						if (h == null || b == null || lg == null) return false;
						var sc = ArmorDoctrine.PreferredClass(h, b, lg);
						return sc == null || wcls == null || sc == wcls || ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs) != wcls;
					}
					var best = stor.Where(i => WeaponClassifier.IsArmorPiece(i, p) && !i.vanity && Fits(i)).OrderByDescending(ArmoryTE.ArmorScoreOf).FirstOrDefault();
					if (best != null && ArmoryTE.ArmorScoreOf(best) >= w * margin + 1f) {
						bool completeSet = stor.Count(i => ArmoryTE.StemOf(i) == ArmoryTE.StemOf(best) && WeaponClassifier.IsArmorPiece(i, 0)) > 0
							&& stor.Count(i => ArmoryTE.StemOf(i) == ArmoryTE.StemOf(best) && WeaponClassifier.IsArmorPiece(i, 1)) > 0
							&& stor.Count(i => ArmoryTE.StemOf(i) == ArmoryTE.StemOf(best) && WeaponClassifier.IsArmorPiece(i, 2)) > 0;
						res.Add($"[UNUSED-ARMOR] levy {l.Id} wears {T(worn, tierOf)} on the {nm} while {T(best, tierOf)} sits in storage ({(completeSet ? "its whole set is there" : "an incomplete set: only upgrades whole sets")})");
					}
				}
			}
			// 2. a clearly better ACCESSORY sits unused while a levy has a worse one (or an empty slot)
			foreach (var l in levies) {
				var g = l.Gear; var cls = WeaponClassifier.ItemDamageClass(g.Weapon);
				var worn = g.Accessories.Take(l.AccessorySlots).ToList();
				foreach (var cand in stor.Where(i => i.accessory && !i.vanity && WeaponClassifier.IsUsefulAccessory(i)).OrderByDescending(i => ArmoryTE.AccessoryScoreOf(i, cls)).Take(2)) {
					float cs = ArmoryTE.AccessoryScoreOf(cand, cls);
					if (worn.Any(w => w != null && !w.IsAir && w.type == cand.type)) continue;
					int empty = worn.Count(w => w == null || w.IsAir);
					var worst = worn.Where(w => w != null && !w.IsAir).OrderBy(w => ArmoryTE.AccessoryScoreOf(w, cls)).FirstOrDefault();
					if (empty > 0) res.Add($"[UNUSED-ACC] levy {l.Id} has an empty accessory slot while {T(cand, tierOf)} sits in storage");
					else if (worst != null && cs >= ArmoryTE.AccessoryScoreOf(worst, cls) * margin) res.Add($"[UNUSED-ACC] levy {l.Id} wears {T(worst, tierOf)} while {T(cand, tierOf)} sits in storage");
				}
			}
			// 3. a better WEAPON sits in storage that some levy of its class would take (a class-matched, clearly worse-armed levy)
			foreach (var l in levies) {
				var g = l.Gear; var cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs) ?? WeaponClassifier.ItemDamageClass(g.Weapon);
				float cur = g.HasWeapon ? WeaponRank.Of(g.Weapon) : 0f;
				var best = stor.Where(i => WeaponClassifier.IsIssuableWeapon(i) && i.maxStack <= 1 && WeaponClassifier.ItemDamageClass(i) == cls).OrderByDescending(WeaponRank.Of).FirstOrDefault();
				if (best != null && WeaponRank.Of(best) >= cur * margin && (g.Weapon == null || g.Weapon.IsAir || g.Weapon.useAmmo <= 0 || best.useAmmo <= 0 || true)) {
					if (best.useAmmo > 0 && !stor.Any(i => i.ammo == best.useAmmo) && g.Ammo.ammo != best.useAmmo) continue; // no ammo for it in stock: not a real miss
					res.Add($"[UNUSED-WEAPON] levy {l.Id} ({ClsName(cls)}) holds {T(g.Weapon, tierOf)} while {T(best, tierOf)} sits in storage");
				}
			}
			// 4. top weapon to a worse-suited class: a levy holds a weapon of class C although a levy whose ARMOR class is C holds a clearly weaker weapon
			foreach (var a in levies) foreach (var b in levies) {
				if (a == b || !a.Gear.HasWeapon || !b.Gear.HasWeapon) continue;
				var wc = WeaponClassifier.ItemDamageClass(a.Gear.Weapon);
				var ac = ArmorDoctrine.PreferredClass(a.Gear.Head, a.Gear.Body, a.Gear.Legs);
				var bc = ArmorDoctrine.PreferredClass(b.Gear.Head, b.Gear.Body, b.Gear.Legs);
				if (wc == null || bc != wc || ac == wc) continue; // b is the suited class, a is not
				if (WeaponRank.Of(a.Gear.Weapon) >= WeaponRank.Of(b.Gear.Weapon) * margin)
					res.Add($"[WEAPON-MISMATCH] levy {a.Id} (armor {ClsName(ac)}) holds {T(a.Gear.Weapon, tierOf)} while levy {b.Id}, whose armor favours {ClsName(bc)}, holds the weaker {T(b.Gear.Weapon, tierOf)}");
			}
			// 4b. top ARMOR to a worse-suited class: a set favouring class C is worn by a levy whose weapon is not C while a levy whose weapon is C wears a clearly weaker kit
			foreach (var a in levies) foreach (var b in levies) {
				if (a == b || !a.Gear.HasWeapon || !b.Gear.HasWeapon) continue;
				var ac = ArmorDoctrine.PreferredClass(a.Gear.Head, a.Gear.Body, a.Gear.Legs);
				var aw = WeaponClassifier.ItemDamageClass(a.Gear.Weapon);
				var bw = WeaponClassifier.ItemDamageClass(b.Gear.Weapon);
				if (ac == null || aw == ac || bw != ac) continue;
				if (KitScore(a.Gear) >= KitScore(b.Gear) * margin)
					res.Add($"[ARMOR-MISMATCH] levy {a.Id} (weapon {ClsName(aw)}) wears the {ClsName(ac)} set {T(a.Gear.Head, tierOf)} while levy {b.Id}, who fires {ClsName(bw)}, wears the weaker {T(b.Gear.Head, tierOf)}");
			}
			// 5. weapon of a class the levy's own armor does not favour (coherence)
			foreach (var l in levies) {
				var g = l.Gear; var ac = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs); var wc = g.HasWeapon ? WeaponClassifier.ItemDamageClass(g.Weapon) : null;
				if (ac != null && wc != null && ac != wc) res.Add($"[CLASS-INCOHERENT] levy {l.Id}: armor favours {ClsName(ac)} but he holds a {ClsName(wc)} weapon ({T(g.Weapon, tierOf)})");
			}
			// 6. a strong levy upgraded while a junk-armored levy stayed junk: compare armor scores before the step
			var sortedPre = preArmor.OrderBy(kv => kv.Value).ToList();
			var med = Median(preArmor.Values.ToList());
			foreach (var l in levies) {
				float pre = preArmor[l.Id], post = KitScore(l.Gear);
				if (post <= pre + 1f) continue;
				// did a levy with a clearly lower pre-step kit end the step without an upgrade although this levy's NEW set (or better) would have helped him?
				var lcls = ArmorDoctrine.PreferredClass(l.Gear.Head, l.Gear.Body, l.Gear.Legs);
				foreach (var u in levies) {
					if (u == l) continue;
					float upre = preArmor[u.Id], upost = KitScore(u.Gear);
					if (upost > upre + 1f) continue; // he got something
					var ucls = u.Gear.HasWeapon ? WeaponClassifier.ItemDamageClass(u.Gear.Weapon) : null;
					if (lcls != null && ucls != null && lcls != ucls) continue; // that set is another class's: not a miss for him
					if (upre * margin < pre && upost * margin < post && upre < med)
						res.Add($"[NOT-WORST-FIRST] levy {l.Id} (kit {pre:F0}) was upgraded to {post:F0} but levy {u.Id} (kit {upre:F0}, below the pre-step median {med:F0}) got nothing");
				}
			}
			// 7. split sets: a levy wears three pieces that form no set bonus although at least one complete set exists anywhere (storage or worn)
			var allPieces = new List<Item>(stor); foreach (var l in levies) { allPieces.Add(l.Gear.Head); allPieces.Add(l.Gear.Body); allPieces.Add(l.Gear.Legs); }
			foreach (var l in levies) {
				var g = l.Gear;
				if (g.Head.IsAir || g.Body.IsAir || g.Legs.IsAir) { res.Add($"[BARE-SLOT] levy {l.Id} has an empty armor slot"); continue; }
				if (ArmorDoctrine.FormsSetBonus(g.Head, g.Body, g.Legs)) continue;
				bool stockSet = stor.Select(ArmoryTE.StemOf).Distinct().Any(st => new[] { 0, 1, 2 }.All(p => stor.Any(i => ArmoryTE.StemOf(i) == st && WeaponClassifier.IsArmorPiece(i, p))));
				res.Add($"[NO-SET-BONUS] levy {l.Id} wears a mixed kit ({T(g.Head, tierOf)} / {T(g.Body, tierOf)} / {T(g.Legs, tierOf)}){(stockSet ? "; a complete set is in storage" : "")}");
			}
			// 8. churn: a slot that went A -> B -> A, or changed more than twice within the step
			foreach (var kv in hist) {
				var h = kv.Value;
				if (h.Count >= 3 && (h.Count - 1 > 2 || h.Skip(2).Any(t => h.Take(h.IndexOf(t)).Contains(t) && t != 0)))
					res.Add($"[CHURN] levy {kv.Key.Item1} {kv.Key.Item2}: {string.Join(" -> ", h.Select(t => t == 0 ? "-" : Lang.GetItemNameValue(t)))}");
			}
			return res.Distinct().ToList();
		}
	}
}
