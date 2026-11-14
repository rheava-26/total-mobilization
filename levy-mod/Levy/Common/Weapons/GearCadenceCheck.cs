using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot-time self-check of the per-levy gear check (<see cref="ArmoryTE.UpgradeWeapons"/> on each levy's own 10 to 15 second timer, no army-wide pass): one levy's look touches only his own kit and the shelf, new armor
	/// and accessories reach the army as each levy looks, the best set of a class stays with a levy of that class, a settled army does not churn in either timer order, two mobility accessories and a better non-mobility one
	/// do not rotate every look, and the timers are spread (a fresh army never looks on one tick). Logs one summary line (or the failures); builds throwaway armories and soldiers, touches no world state, never throws.
	/// </summary>
	public class GearCadenceCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }
		private static readonly int[] Wood = { ItemID.WoodHelmet, ItemID.WoodBreastplate, ItemID.WoodGreaves };
		private static readonly int[] Copper = { ItemID.CopperHelmet, ItemID.CopperChainmail, ItemID.CopperGreaves };
		private static readonly int[] Cactus = { ItemID.CactusHelmet, ItemID.CactusBreastplate, ItemID.CactusLeggings };
		private static readonly int[] Iron = { ItemID.IronHelmet, ItemID.IronChainmail, ItemID.IronGreaves };
		private static readonly int[] Shadow = { ItemID.ShadowHelmet, ItemID.ShadowScalemail, ItemID.ShadowGreaves };

		private static SoldierRecord Levy(int id, int[] set, int weapon = ItemID.CopperShortsword, Grade grade = Grade.Levy)
		{
			var r = new SoldierRecord { Id = id, Grade = grade };
			r.Gear.Head = Make(set[0]); r.Gear.Body = Make(set[1]); r.Gear.Legs = Make(set[2]);
			r.Gear.Weapon = Make(weapon);
			return r;
		}

		private static string Kit(SoldierRecord r) => r.Gear.Head.type + "/" + r.Gear.Body.type + "/" + r.Gear.Legs.type;
		private static string Snap(SoldierRecord r) => Kit(r) + "|" + string.Join(",", r.Gear.Accessories.Select(a => a == null || a.IsAir ? 0 : a.type));
		private static float KitScore(SoldierRecord r) => ArmoryTE.ArmorScoreOf(r.Gear.Head) + ArmoryTE.ArmorScoreOf(r.Gear.Body) + ArmoryTE.ArmorScoreOf(r.Gear.Legs);

		public override void PostSetupRecipes()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			try {
				MaterialCost.Ensure();
				WeaponRank.Force(WeaponRankMode.SellPrice, false);
				try {
					Guard("designer example", () => Example(Check, false), failures);
					Guard("designer example, other timer order", () => Example(Check, true), failures);
					Guard("own kit only", () => OwnKitOnly(Check), failures);
					Guard("class", () => ClassStaysHome(Check), failures);
					Guard("accessories", () => Accessories(Check), failures);
					Guard("mobility rotation", () => MobilityRotation(Check), failures);
					Guard("timers", () => Timers(Check), failures);
					Guard("summoner staff", () => SummonerStaff(Check), failures);
				}
				finally { WeaponRank.Release(); ArmoryTE.TestServed = null; }
				if (failures.Count == 0) Mod.Logger.Info($"Levy gear cadence self-check: {ok} checks OK");
				else { Mod.Logger.Warn($"Levy gear cadence self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (var f in failures) Mod.Logger.Warn("Levy gear cadence self-check FAILED: " + f); }
			}
			catch (Exception e) { try { Mod.Logger.Warn("Levy gear cadence self-check could not run: " + e); } catch (Exception) { } }
		}

		private static void Guard(string name, Action body, List<string> failures)
		{
			try { body(); } catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
		}

		/// <summary>Run the gear check of every levy, repeatedly, until nothing changes (as the real timers eventually do). Returns how many checks changed something.</summary>
		private static int Settle(ArmoryTE te, List<SoldierRecord> army, bool reverse)
		{
			int changes = 0;
			for (int round = 0; round < 12; round++) {
				bool any = false;
				var ord = reverse ? Enumerable.Reverse(army).ToList() : army;
				foreach (var r in ord) {
					var before = army.Select(Snap).ToList();
					te.UpgradeWeapons(r);
					if (!before.SequenceEqual(army.Select(Snap).ToList())) { any = true; changes++; }
				}
				if (!any) break;
			}
			return changes;
		}

		/// <summary>The designer's example: ten levies, three of them in wood, three in copper, two in cactus, two in iron; three new Shadow sets arrive. Each levy looks on his own; the three sets are worn, nobody ends worse off than he began, the army ends at least as well armored as when the three worst took them, and a second round changes nothing.</summary>
		private static void Example(Action<bool, string> check, bool reverse)
		{
			string tag = reverse ? " (timers in reverse)" : "";
			var army = new List<SoldierRecord> {
				Levy(1, Iron), Levy(2, Copper), Levy(3, Wood), Levy(4, Cactus), Levy(5, Wood), Levy(6, Copper), Levy(7, Iron), Levy(8, Cactus), Levy(9, Copper), Levy(10, Wood),
			};
			var start = army.ToDictionary(r => r.Id, KitScore);
			float startSum = start.Values.Sum(), shadow = ArmoryTE.ArmorScoreOf(Make(Shadow[0])) + ArmoryTE.ArmorScoreOf(Make(Shadow[1])) + ArmoryTE.ArmorScoreOf(Make(Shadow[2]));
			float woodKit = Wood.Sum(t => ArmoryTE.ArmorScoreOf(Make(t)));
			var te = new ArmoryTE();
			for (int s = 0; s < 3; s++) foreach (int t in Shadow) te.Deposit(Make(t));
			ArmoryTE.TestServed = army;
			int changes, again;
			try { changes = Settle(te, army, reverse); again = Settle(te, army, reverse); } finally { ArmoryTE.TestServed = null; }
			var shadowKit = Shadow[0] + "/" + Shadow[1] + "/" + Shadow[2];
			int worn = army.Count(r => Kit(r) == shadowKit);
			check(worn == 3, "the three Shadow sets end up worn" + tag + "; got " + worn);
			check(army.All(r => KitScore(r) >= start[r.Id]), "nobody ends in worse armor than he began" + tag);
			float endSum = army.Sum(KitScore);
			check(endSum >= startSum + 3 * (shadow - woodKit) - 0.5f, "the army ends at least as well armored as when the three worst took the sets" + tag + $" ({endSum:F0} vs {startSum + 3 * (shadow - woodKit):F0})");
			check(again == 0, "a settled army does not churn when every gear check comes round again" + tag + " (" + again + " changes)");
			check(changes > 0, "the upgrade did happen" + tag);
		}

		/// <summary>One levy's own look changes his armor and accessories only: nobody else's kit moves, and what he puts down is on the shelf.</summary>
		private static void OwnKitOnly(Action<bool, string> check)
		{
			var a = Levy(1, Wood); var b = Levy(2, Wood); var c = Levy(3, Copper);
			var army = new List<SoldierRecord> { a, b, c };
			var te = new ArmoryTE();
			foreach (int t in Shadow) te.Deposit(Make(t));
			var bBefore = Snap(b); var cBefore = Snap(c);
			ArmoryTE.TestServed = army;
			try { te.UpgradeWeapons(a); } finally { ArmoryTE.TestServed = null; }
			check(Kit(a) == Shadow[0] + "/" + Shadow[1] + "/" + Shadow[2], "the levy who looks takes the new set (wears " + Kit(a) + ")");
			check(Snap(b) == bBefore && Snap(c) == cBefore, "the other levies' armor and accessories are untouched by his look");
			check(te.CountInStorage(Wood[0]) == 1 && te.CountInStorage(Wood[1]) == 1 && te.CountInStorage(Wood[2]) == 1, "the wood set he put down is on the shelf for the next levy");
		}

		/// <summary>A ranged set goes to the ranged levy, not to a worse-armored melee levy.</summary>
		private static void ClassStaysHome(Action<bool, string> check)
		{
			var melee = Levy(1, Wood, ItemID.CopperBroadsword);   // the worst kit, but a swordsman
			var gunner = Levy(2, Iron, ItemID.Musket);             // a better kit, firing ranged
			var army = new List<SoldierRecord> { melee, gunner };
			var te = new ArmoryTE();
			te.Storage[0] = Make(ItemID.ShroomiteHeadgear); te.Storage[1] = Make(ItemID.ShroomiteBreastplate); te.Storage[2] = Make(ItemID.ShroomiteLeggings);
			ArmoryTE.TestServed = army;
			try { Settle(te, army, false); Settle(te, army, true); } finally { ArmoryTE.TestServed = null; }
			check(gunner.Gear.Head.type == ItemID.ShroomiteHeadgear, "the bow-and-gun set goes to the ranged levy although the swordsman was worse off (gunner wears " + gunner.Gear.Head.Name + ")");
			check(melee.Gear.Head.type != ItemID.ShroomiteHeadgear, "the swordsman did not take the ranged set");
		}

		/// <summary>A new accessory is worn by exactly one levy who had room for it, and nothing churns afterwards, whichever order the checks come in.</summary>
		private static void Accessories(Action<bool, string> check)
		{
			foreach (bool reverse in new[] { false, true }) {
				var a = Levy(1, Iron, grade: Grade.ManAtArms); // one accessory
				var b = Levy(2, Iron, grade: Grade.ManAtArms); // nothing
				var c = Levy(3, Iron, grade: Grade.ManAtArms); // two
				a.Gear.Accessories[0] = Make(ItemID.CloudinaBottle);
				c.Gear.Accessories[0] = Make(ItemID.CloudinaBottle); c.Gear.Accessories[1] = Make(ItemID.HermesBoots);
				var army = new List<SoldierRecord> { a, b, c };
				var te = new ArmoryTE();
				te.Deposit(Make(ItemID.ObsidianShield));
				ArmoryTE.TestServed = army;
				int again;
				try { Settle(te, army, reverse); again = Settle(te, army, reverse); } finally { ArmoryTE.TestServed = null; }
				int Count(SoldierRecord r) => r.Gear.Accessories.Count(x => x != null && !x.IsAir && x.type == ItemID.ObsidianShield);
				string tag = reverse ? " (timers in reverse)" : "";
				check(Count(a) + Count(b) + Count(c) == 1 && te.CountInStorage(ItemID.ObsidianShield) == 0, "the one new accessory is worn by exactly one levy" + tag + $" (a={Count(a)} b={Count(b)} c={Count(c)})");
				check(a.Gear.Accessories.Any(x => x.type == ItemID.CloudinaBottle) && c.Gear.Accessories.Any(x => x.type == ItemID.HermesBoots), "nobody loses a mobility accessory to it" + tag);
				check(again == 0, "no churn once the accessory is dealt" + tag);
			}
		}

		/// <summary>Two mobility accessories, one other, and a better non-mobility piece on the shelf (three slots): the looks settle, they do not rotate the three every time.</summary>
		private static void MobilityRotation(Action<bool, string> check)
		{
			foreach (int weapon in new[] { ItemID.Musket, ItemID.CopperBroadsword, ItemID.SlimeStaff }) {
				var r = Levy(1, Iron, weapon, Grade.ManAtArms);
				r.Gear.Accessories[0] = Make(ItemID.WingsVortex); r.Gear.Accessories[1] = Make(ItemID.LightningBoots); r.Gear.Accessories[2] = Make(ItemID.StalkersQuiver);
				var te = new ArmoryTE();
				te.Deposit(Make(ItemID.CharmofMyths));
				var army = new List<SoldierRecord> { r };
				ArmoryTE.TestServed = army;
				int late = 0;
				try { for (int i = 0; i < 10; i++) { var before = Snap(r); te.UpgradeWeapons(r); if (i >= 4 && before != Snap(r)) late++; } } finally { ArmoryTE.TestServed = null; }
				check(late == 0, $"a levy with {Make(weapon).Name} settles on his accessories instead of rotating them every look ({late} late changes)");
				check(r.Gear.Accessories.Any(x => x != null && !x.IsAir && AccessoryMobility.Is(x)), $"a levy with {Make(weapon).Name} still wears a mobility accessory");
			}
		}

		/// <summary>A summoner carrying a better minion staff as a sidearm than the one in his hand ends with the better one in hand, whichever look came first; a melee set on the shelf is not his to take.</summary>
		private static void SummonerStaff(Action<bool, string> check)
		{
			var r = Levy(1, Iron, ItemID.SlimeStaff, Grade.ManAtArms);
			r.Gear.Head = Make(ItemID.StardustHelmet); r.Gear.Body = Make(ItemID.StardustBreastplate); r.Gear.Legs = Make(ItemID.StardustLeggings);
			r.Gear.ExtraSidearms[0] = Make(ItemID.StardustDragonStaff);
			var te = new ArmoryTE();
			foreach (int t in new[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings }) te.Deposit(Make(t));
			ArmoryTE.TestServed = new List<SoldierRecord> { r };
			try { for (int i = 0; i < 4; i++) te.UpgradeWeapons(r); } finally { ArmoryTE.TestServed = null; }
			var carried = new[] { r.Gear.Weapon, r.Gear.Sidearm }.Concat(r.Gear.ExtraSidearms).Where(x => x != null && !x.IsAir).Select(x => x.type).ToList();
			check(r.Gear.Weapon.type == ItemID.StardustDragonStaff, "the better minion staff leads (hand: " + r.Gear.Weapon.Name + ")");
			check(r.Gear.Head.type == ItemID.StardustHelmet, "a summoner does not take a melee set off the shelf (wears " + r.Gear.Head.Name + ")");
		}

		/// <summary>The timers: the wait is 10 to 15 seconds each time, and a muster's first looks are spread out.</summary>
		private static void Timers(Action<bool, string> check)
		{
			uint lo = uint.MaxValue, hi = 0;
			for (int i = 0; i < 2000; i++) { uint d = ArmoryTE.GearCheckDelay(); lo = Math.Min(lo, d); hi = Math.Max(hi, d); }
			check(lo >= 600 && hi <= 900, $"the wait between looks is 10 to 15 seconds (saw {lo / 60f:F1} to {hi / 60f:F1})");
			check(hi - lo > 150, "the wait is drawn fresh each time, not one fixed number");
			var first = new HashSet<uint>(); uint fmax = 0;
			for (int i = 0; i < 40; i++) { uint d = ArmoryTE.GearCheckFirstDelay(new SoldierRecord { Id = 1000 + i }); first.Add(d); fmax = Math.Max(fmax, d); }
			check(first.Count >= 30 && fmax <= 900, $"a muster of 40 levies does not look on one tick ({first.Count} distinct first looks, latest {fmax / 60f:F1} s)");
		}
	}
}
