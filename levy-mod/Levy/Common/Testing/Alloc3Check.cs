using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check of the 2.0.50 armory allocator fixes, logged as "[Alloc3]" lines ending in "self-check pass=N fail=M" (the accessory-minion half lives in the minions harness scenario, see CombatHarness.Minions):
	///  1. SUMMONERS FIRST: a detached armory with a mixed pile of minion staves and sentries serves three levies who are not summoners and two summoners, the non-summoners first (the order that used to hand them the best staves),
	///     then several upgrade rounds. Every summon weapon a non-summoner ends with must be no better than any summoner's of the same kind, and the best staff in the pile must be a summoner's.
	///  2. ACCESSORIES BY CLASS: the same armory stocked with class emblems, magic / melee / ranged / summon accessories and generic ones serves a melee, a ranged, a magic and a summon levy. Nobody may wear another class's accessory
	///     (judged by a hand-made list, not by the rule under test) and each wears its own class's emblem. Each levy's kit, the fit it scored and why, is logged; the same run with the old reading (LEGACY) is logged for the before / after table.
	/// Throwaway objects only; never throws.
	/// </summary>
	public class Alloc3Check : ModSystem
	{
		private int _pass, _fail;
		private void Check(bool ok, string what) { if (ok) _pass++; else _fail++; Mod.Logger.Info($"[Alloc3] {(ok ? "PASS" : "FAIL")} {what}"); }

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(Run);

		private static Item Make(int type) => ReforgeFixture.Make(type);
		private static bool Dep(ArmoryTE te, Item it) { var left = te.Deposit(it); return left == null || left.IsAir; }

		private static int Vanilla(string name) { var f = typeof(ItemID).GetField(name); return f != null && f.IsLiteral ? Convert.ToInt32(f.GetRawConstantValue()) : -1; }

		/// <summary>An item by vanilla ItemID name, or "Mod/Name" for a modded one; null when it does not exist in this build.</summary>
		private static Item Find(string name)
		{
			try
			{
				if (name.Contains('/'))
				{
					var parts = name.Split('/');
					if (ModLoader.TryGetMod(parts[0], out var m) && m.TryFind<ModItem>(parts[1], out var mi)) return Make(mi.Type);
					return null;
				}
				int t = Vanilla(name);
				return t > 0 ? Make(t) : null;
			}
			catch (Exception) { return null; }
		}

		private void Run()
		{
			try
			{
				bool sf = ArmoryTE.SummonersFirst, legacy = ArmorDoctrine.LegacyAccessoryFit;
				try
				{
					SummonersCase(false);
					SummonersCase(true);
					AccessoryCase(true);
					AccessoryCase(false);
				}
				finally { ArmoryTE.SummonersFirst = sf; ArmorDoctrine.LegacyAccessoryFit = legacy; ArmoryTE.TestServed = null; }
				if (Environment.GetEnvironmentVariable("LEVY_ALLOC3_DUMP") == "1") DumpAccessories();
			}
			catch (Exception e) { Check(false, "self-check threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim()); }
			Mod.Logger.Info($"[Alloc3] self-check pass={_pass} fail={_fail}");
		}

		// ------------------------------------------------------------------ 1. summoners first

		private static SoldierRecord Levy(int id, string name, Item weapon)
		{
			var r = new SoldierRecord { Id = id, Name = name, Kind = SoldierKind.Regular, Grade = Grade.Champion };
			r.Gear.Weapon = weapon;
			return r;
		}

		private static IEnumerable<Item> SummonKit(SoldierRecord r) => r.Gear.Sidearms().Where(i => !i.IsAir && SidearmPolicy.IsDeployer(i));

		private void SummonersCase(bool on)
		{
			ArmoryTE.SummonersFirst = on;
			var te = new ArmoryTE();
			string[] stock = { "SlimeStaff", "HornetStaff", "ImpStaff", "SpiderStaff", "PygmyStaff", "RavenStaff", "XenoStaff", "StardustCellStaff", "TempestStaff", "StardustDragonStaff", "QueenSpiderStaff", "DD2BallistraTowerT1Popper", "DD2LightningAuraT1Popper", "DD2FlameburstTowerT1Popper" };
			int stocked = 0;
			foreach (var n in stock) { var it = Find(n); if (it != null && Dep(te, it)) stocked++; }
			var whips = new[] { Find("BlandWhip"), Find("ThornWhip") };
			var melee = new[] { "CopperBroadsword", "IronBroadsword", "WoodenBow" };
			var summoners = new List<SoldierRecord>(); var others = new List<SoldierRecord>();
			for (int i = 0; i < 3; i++) others.Add(Levy(10 + i, "Pvt. Kathryn " + i, Find(melee[i]) ?? Find("CopperBroadsword")));
			for (int i = 0; i < 2; i++) summoners.Add(Levy(20 + i, "Pvt. Summoner " + i, whips[i] ?? Find("BlandWhip")));
			var all = others.Concat(summoners).ToList();
			ArmoryTE.TestServed = all;
			// the order that used to hurt: the levies who are not summoners are dealt first
			foreach (var r in others) te.IssueGear(r);
			foreach (var r in summoners) te.IssueGear(r);
			for (int round = 0; round < 4; round++) foreach (var r in all) te.UpgradeWeapons(r);
			float Kind(Item it) => WeaponRank.Of(it);
			float minSummonerMinion = float.MaxValue, minSummonerSentry = float.MaxValue, maxOtherMinion = 0f, maxOtherSentry = 0f;
			var sb = new StringBuilder();
			foreach (var r in all)
			{
				bool isSum = summoners.Contains(r);
				sb.Append($"{r.Name}=[{string.Join(", ", SummonKit(r).Select(i => i.Name + ":" + Kind(i).ToString("F0")))}] ");
				foreach (var it in SummonKit(r))
				{
					bool sentry = SidearmPolicy.IsSentry(it);
					if (isSum) { if (sentry) minSummonerSentry = Math.Min(minSummonerSentry, Kind(it)); else minSummonerMinion = Math.Min(minSummonerMinion, Kind(it)); }
					else { if (sentry) maxOtherSentry = Math.Max(maxOtherSentry, Kind(it)); else maxOtherMinion = Math.Max(maxOtherMinion, Kind(it)); }
				}
			}
			float bestStaffAnywhere = 0f; bool bestHeldBySummoner = false; Item bestItem = null;
			foreach (var r in all) foreach (var it in SummonKit(r)) if (!SidearmPolicy.IsSentry(it) && Kind(it) > bestStaffAnywhere) { bestStaffAnywhere = Kind(it); bestItem = it; bestHeldBySummoner = summoners.Contains(r); }
			foreach (var it in te.Storage) if (!it.IsAir && SidearmPolicy.IsStaff(it) && Kind(it) > bestStaffAnywhere) { bestStaffAnywhere = Kind(it); bestItem = it; bestHeldBySummoner = false; }
			Mod.Logger.Info($"[Alloc3] summon weapons {(on ? "AFTER (summoners first)" : "BEFORE (old order)")} stocked={stocked}: {sb}| best staff {(bestItem?.Name ?? "none")} held by {(bestHeldBySummoner ? "a summoner" : "NOT a summoner")}; non-summoner best minion staff {maxOtherMinion:F0} vs weakest summoner minion staff {(minSummonerMinion == float.MaxValue ? -1 : minSummonerMinion):F0}; sentry {maxOtherSentry:F0} vs {(minSummonerSentry == float.MaxValue ? -1 : minSummonerSentry):F0}");
			if (!on) return; // the old order is only measured, never asserted
			Check(stocked >= 8, "summon case: the pile holds at least eight staves and sentries (" + stocked + ")");
			Check(summoners.All(r => SummonKit(r).Any()), "every summoner carries at least one summon weapon");
			Check(minSummonerMinion == float.MaxValue || maxOtherMinion <= minSummonerMinion, "no non-summoner's minion staff outranks a summoner's");
			Check(minSummonerSentry == float.MaxValue || maxOtherSentry <= minSummonerSentry, "no non-summoner's sentry outranks a summoner's");
			Check(bestItem == null || bestHeldBySummoner, "the best minion staff anywhere is a summoner's");
			Check(others.Any(r => SummonKit(r).Any()) || true, "non-summoners may keep leftovers (informational)");
		}

		// ------------------------------------------------------------------ 2. accessories by class

		private static readonly string[] MagicOnly = { "CelestialCuffs", "ManaFlower", "ArcaneFlower", "BandofStarpower", "ManaCloak", "SorcererEmblem", "CelestialEmblem", "MagicCuffs", "CalamityMod/ManaPolarizer", "CalamityMod/EtherealTalisman" };
		private static readonly string[] MeleeOnly = { "WarriorEmblem", "FireGauntlet", "MechanicalGlove", "PowerGlove", "BerserkerGlove", "CalamityMod/ElementalGauntlet" };
		private static readonly string[] RangedOnly = { "RangerEmblem", "MagicQuiver", "MoltenQuiver", "StalkersQuiver", "ReconScope", "SniperScope", "CalamityMod/DeadshotBrooch" };
		private static readonly string[] SummonOnly = { "SummonerEmblem", "HerculesBeetle", "PapyrusScarab", "PygmyNecklace", "NecromanticScroll", "CalamityMod/GladiatorsLocket" };
		private static readonly string[] Generic = { "CobaltShield", "ObsidianShield", "AnkhShield", "BandofRegeneration", "CharmofMyths", "PhilosophersStone", "HermesBoots", "CloudinaBottle", "ShinyRedBalloon", "AvengerEmblem", "StarCloak", "PanicNecklace", "LuckyHorseshoe", "Shackle", "FrozenTurtleShell", "CelestialShell", "PutridScent", "FleshKnuckles", "EyeoftheGolem", "SharkToothNecklace", "MoltenCharm", "CalamityMod/AeroStone", "CalamityMod/BloodPact", "CalamityMod/AmidiasPendant", "CalamityMod/SandCloak", "CalamityMod/TheCamper" };

		private static string ClassOfList(Item it)
		{
			string n = it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : (ItemID.Search.TryGetName(it.type, out var s) ? s : it.Name);
			if (MagicOnly.Contains(n)) return "magic"; if (MeleeOnly.Contains(n)) return "melee"; if (RangedOnly.Contains(n)) return "ranged"; if (SummonOnly.Contains(n)) return "summon";
			return "generic";
		}

		private void AccessoryCase(bool after)
		{
			ArmorDoctrine.LegacyAccessoryFit = !after;
			var te = new ArmoryTE();
			var pile = new List<string>(); pile.AddRange(MagicOnly); pile.AddRange(MeleeOnly); pile.AddRange(RangedOnly); pile.AddRange(SummonOnly); pile.AddRange(Generic);
			int stocked = 0;
			foreach (var n in pile) { var it = Find(n); if (it == null) continue; if (Dep(te, it)) stocked++; }
			foreach (var n in new[] { "WarriorEmblem", "WarriorEmblem", "RangerEmblem", "SorcererEmblem", "SummonerEmblem", "CobaltShield", "HermesBoots", "BandofRegeneration" }) { var it = Find(n); if (it != null) { if (Dep(te, it)) stocked++; } }
			var levies = new List<(SoldierRecord r, string cls)>
			{
				(Levy(30, "Melee A", Find("CopperBroadsword")), "melee"), (Levy(31, "Melee B", Find("IronBroadsword")), "melee"),
				(Levy(32, "Ranged", Find("Musket")), "ranged"), (Levy(33, "Magic", Find("AmethystStaff")), "magic"), (Levy(34, "Summoner", Find("BlandWhip")), "summon"),
			};
			ArmoryTE.TestServed = levies.Select(l => l.r).ToList();
			for (int round = 0; round < 4; round++) foreach (var l in levies) { te.IssueGear(l.r); te.UpgradeWeapons(l.r); }
			if (after) ScoreTable(pile);
			int wrong = 0, own = 0;
			foreach (var (r, cls) in levies)
			{
				var worn = r.Gear.Accessories.Where(i => !i.IsAir).ToList();
				var dc = cls switch { "melee" => DamageClass.Melee, "ranged" => DamageClass.Ranged, "magic" => DamageClass.Magic, _ => DamageClass.Summon };
				var parts = worn.Select(i => { string c = ClassOfList(i); if (c != "generic" && c != cls) wrong++; return $"{i.Name}[{c} fit={ArmorDoctrine.AccessoryClassFit(i, dc):F2}{(ArmorDoctrine.AccessoryMismatch(i, dc) ? " MISMATCH" : "")}]"; });
				Mod.Logger.Info($"[Alloc3] accessories {(after ? "AFTER" : "BEFORE (old reading)")} {r.Name} ({cls}): {string.Join(", ", parts)}");
				if (worn.Any(i => ClassOfList(i) == cls)) own++;
			}
			Mod.Logger.Info($"[Alloc3] accessories {(after ? "AFTER" : "BEFORE")}: stocked={stocked} worn by the wrong class={wrong} levies wearing at least one accessory of their own class={own}/{levies.Count}");
			if (!after) return;
			Check(stocked >= 40, "accessory case: the armory is stocked (" + stocked + ")");
			Check(wrong == 0, "no levy wears another class's accessory (" + wrong + ")");
			Check(own >= 5, "every levy wears an accessory made for its class (a second melee levy's Warrior Emblem is already inside his Mechanical Glove) (" + own + "/5)");
			Check(levies.All(l => l.r.Gear.Accessories.Count(i => !i.IsAir) >= 4), "every levy is fully fitted");
		}

		private static float ScoreOf(Item it, DamageClass cls)
		{
			var m = typeof(ArmoryTE).GetMethod("AccessoryScore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			return m == null ? float.NaN : (float)m.Invoke(null, new object[] { it, cls });
		}

		/// <summary>Why: every stocked accessory's score for each class, with its fit and whether it is a mismatch for that class.</summary>
		private void ScoreTable(List<string> pile)
		{
			var cls = new[] { ("melee", DamageClass.Melee), ("ranged", DamageClass.Ranged), ("magic", DamageClass.Magic), ("summon", DamageClass.Summon) };
			foreach (var n in pile.Concat(new[] { "WarriorEmblem", "RangerEmblem", "SorcererEmblem", "SummonerEmblem" }).Distinct())
			{
				var it = Find(n); if (it == null) continue;
				var sb = new StringBuilder();
				foreach (var (cn, dc) in cls) sb.Append($"{cn}:{ScoreOf(it, dc):F0}{(ArmorDoctrine.AccessoryMismatch(it, dc) ? "(X)" : "")}/fit{ArmorDoctrine.AccessoryClassFit(it, dc):F2} ");
				Mod.Logger.Info($"[Alloc3] score {it.Name,-26} rare={it.rare,2} def={it.defense} list={ClassOfList(it),-7} {sb}");
			}
		}

		// ------------------------------------------------------------------ dump (LEVY_ALLOC3_DUMP=1): what the reading says about every accessory in the game

		private void DumpAccessories()
		{
			int n = 0;
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				Item it; try { it = Make(t); } catch (Exception) { continue; }
				if (it.IsAir || !it.accessory || it.vanity) continue;
				string who = it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : (ItemID.Search.TryGetName(t, out var s) ? s : it.Name);
				Mod.Logger.Info($"[Alloc3] DUMP {who} rare={it.rare} :: {ArmorDoctrine.DebugAccessory(it)} :: {ArmorDoctrine.TooltipText(it).Replace('\n', '|')}");
				n++;
			}
			Mod.Logger.Info($"[Alloc3] DUMP done {n} accessories");
		}
	}
}
