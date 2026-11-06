using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot-time self-check of the raise-the-floor gear deal (<see cref="ArmoryTE.FloorSweep"/>): new armor and accessories go to the worst-equipped levies first, the middle stays put, a set goes to its own class,
	/// nothing churns once settled, and the result does not depend on whose upgrade timer fires first. Logs one summary line (or the failures); builds throwaway armories and soldiers, touches no world state, never throws.
	/// </summary>
	public class GearFloorCheck : ModSystem
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

		public override void PostSetupRecipes()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			bool prev = ArmoryTE.FloorFirst;
			try {
				MaterialCost.Ensure();
				WeaponRank.Force(WeaponRankMode.SellPrice, false);
				try {
					Guard("designer example", () => Example(Check, false), failures);
					Guard("designer example, other timer order", () => Example(Check, true), failures);
					Guard("class", () => ClassStaysHome(Check), failures);
					Guard("accessories", () => Accessories(Check), failures);
				}
				finally { WeaponRank.Release(); ArmoryTE.TestServed = null; }
				if (failures.Count == 0) Mod.Logger.Info($"Levy gear floor self-check: {ok} checks OK");
				else { Mod.Logger.Warn($"Levy gear floor self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (var f in failures) Mod.Logger.Warn("Levy gear floor self-check FAILED: " + f); }
			}
			catch (Exception e) { try { Mod.Logger.Warn("Levy gear floor self-check could not run: " + e); } catch (Exception) { } }
			finally { ArmoryTE.FloorFirst = prev; }
		}

		private static void Guard(string name, Action body, List<string> failures)
		{
			try { body(); } catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
		}

		/// <summary>Run the upgrade timers of every levy, repeatedly, until nothing changes (as the real timers eventually do). Returns how many timers changed something.</summary>
		private static int Settle(ArmoryTE te, List<SoldierRecord> army, bool reverse)
		{
			int changes = 0;
			for (int round = 0; round < 8; round++) {
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

		private static string Snap(SoldierRecord r) => Kit(r) + "|" + string.Join(",", r.Gear.Accessories.Select(a => a == null || a.IsAir ? 0 : a.type));

		/// <summary>The designer's example: ten levies, three of them in wood, three in copper, two in cactus, two in iron; three new Shadow sets arrive. The three worst take them; the middle stays where it was.</summary>
		private static void Example(Action<bool, string> check, bool reverse)
		{
			string tag = reverse ? " (timers in reverse)" : "";
			ArmoryTE.FloorFirst = true;
			var army = new List<SoldierRecord> {
				Levy(1, Iron), Levy(2, Copper), Levy(3, Wood), Levy(4, Cactus), Levy(5, Wood), Levy(6, Copper), Levy(7, Iron), Levy(8, Cactus), Levy(9, Copper), Levy(10, Wood),
			};
			var start = army.ToDictionary(r => r.Id, Kit);
			var te = new ArmoryTE();
			for (int s = 0; s < 3; s++) foreach (int t in Shadow) te.Deposit(Make(t));
			ArmoryTE.TestServed = army;
			int changes, again;
			try { changes = Settle(te, army, reverse); again = Settle(te, army, reverse); } finally { ArmoryTE.TestServed = null; }
			var shadowKit = Shadow[0] + "/" + Shadow[1] + "/" + Shadow[2];
			var shadowed = army.Where(r => Kit(r) == shadowKit).Select(r => r.Id).OrderBy(i => i).ToList();
			check(shadowed.SequenceEqual(new[] { 3, 5, 10 }), "the three Shadow sets go to the three worst-armored levies (the wood ones)" + tag + "; got [" + string.Join(",", shadowed) + "]");
			check(army.Where(r => !new[] { 3, 5, 10 }.Contains(r.Id)).All(r => Kit(r) == start[r.Id]), "everyone else keeps what he wore, the copper, cactus and iron middle included" + tag);
			check(again == 0, "a settled army does not churn when every upgrade timer fires again" + tag + " (" + again + " changes)");
			check(changes > 0, "the upgrade did happen" + tag);
		}

		/// <summary>A ranged set goes to the ranged levy, not to a worse-armored melee levy.</summary>
		private static void ClassStaysHome(Action<bool, string> check)
		{
			ArmoryTE.FloorFirst = true;
			var melee = Levy(1, Wood, ItemID.CopperBroadsword);   // the worst kit, but a swordsman
			var gunner = Levy(2, Iron, ItemID.Musket);             // a better kit, firing ranged
			var army = new List<SoldierRecord> { melee, gunner };
			var te = new ArmoryTE();
			te.Storage[0] = Make(ItemID.ShroomiteHeadgear); te.Storage[1] = Make(ItemID.ShroomiteBreastplate); te.Storage[2] = Make(ItemID.ShroomiteLeggings);
			ArmoryTE.TestServed = army;
			try { Settle(te, army, false); } finally { ArmoryTE.TestServed = null; }
			check(gunner.Gear.Head.type == ItemID.ShroomiteHeadgear, "the bow-and-gun set goes to the ranged levy although the swordsman was worse off (gunner wears " + gunner.Gear.Head.Name + ")");
			check(melee.Gear.Head.type != ItemID.ShroomiteHeadgear, "the swordsman did not take the ranged set");
		}

		/// <summary>New accessories go to the levy with the weakest accessories, empty slots first.</summary>
		private static void Accessories(Action<bool, string> check)
		{
			ArmoryTE.FloorFirst = true;
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
				try { Settle(te, army, reverse); } finally { ArmoryTE.TestServed = null; }
				int Count(SoldierRecord r) => r.Gear.Accessories.Count(x => x != null && !x.IsAir && x.type == ItemID.ObsidianShield);
				check(Count(b) == 1 && Count(a) == 0 && Count(c) == 0, "the one new accessory goes to the levy with none" + (reverse ? " (timers in reverse)" : "") + $" (a={Count(a)} b={Count(b)} c={Count(c)})");
			}
		}
	}
}
