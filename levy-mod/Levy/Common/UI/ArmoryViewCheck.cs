using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Boot self-check of the armory filter view: with a filter on, an item taken out of the armory can always be put back. Reproduces the reported bug (a packed filtered view has no empty cell)
	/// and checks the deposit cell that fixes it. Pure data, never throws.
	/// </summary>
	public class ArmoryViewCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }
		private static bool IsAirItem(Item it) => it == null || it.IsAir || it.stack <= 0;

		private static void SetTick(uint v) => typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, v);

		/// <summary>The items that are out of the racks: the cell layout (the out block on its own rows, nothing lost or doubled) and the cached in-use list (carried gear, loans, holder names, a stamp that moves only on a real change).</summary>
		private static void InUse(Action<bool, string> check)
		{
			// ---- layout
			var st = new Item[40]; for (int i = 0; i < st.Length; i++) st[i] = new Item();
			st[0] = Make(ItemID.CopperShortsword); st[1] = Make(ItemID.Handgun); st[4] = Make(ItemID.Torch);
			const int cols = 12;
			var none = ArmoryView.Layout(st, null, new List<int>(), cols);
			check(none.SequenceEqual(Enumerable.Range(0, 40)), "with nothing out the layout is every storage slot in order, as before");
			var outs = new List<int> { 0, 1, 2 };
			var lay = ArmoryView.Layout(st, null, outs, cols);
			int firstOut = lay.FindIndex(c => c < 0 && c != ArmoryView.Spacer);
			check(firstOut == cols, "the out block starts on the row after the last stored item (row 1 of 12; got cell " + firstOut + ")");
			check(lay.Where(c => c >= 0).OrderBy(c => c).SequenceEqual(Enumerable.Range(0, 40)), "every storage slot is still shown once, empties included, with the out block in");
			check(lay.Where(c => c < 0 && c != ArmoryView.Spacer).Select(c => ~c).SequenceEqual(outs), "every out entry is shown once, in order");
			var filt = ArmoryView.Layout(st, new List<int> { 0, 4, 5 }, outs, cols);
			check(filt.Take(3).SequenceEqual(new[] { 0, 4, 5 }) && filt.IndexOf(~0) == cols && filt.Skip(3).Take(cols - 3).All(c => c == ArmoryView.Spacer), "a filtered view pads to the row end and puts the out block on its own rows");
			check(ArmoryView.Layout(st, new List<int>(), outs, cols).Count == outs.Count, "a filter that shows no stored item still shows the out block, from the first row");

			// ---- the cached list
			var te = new ArmoryTE();
			var lev = new SoldierRecord { Id = 4001, Name = "Pvt. Carry", Grade = Grade.ManAtArms };
			lev.Gear.Weapon = Make(ItemID.Musket); lev.Gear.Head = Make(ItemID.IronHelmet); lev.Gear.Accessories[0] = Make(ItemID.HermesBoots); lev.Gear.Accessories[1] = Make(ItemID.CloudinaBottle);
			var loan = Make(ItemID.Handgun); te.Lent.Add(loan);
			var saved = ArmoryTE.TestServed; uint t0 = Main.GameUpdateCount;
			try
			{
				ArmoryTE.TestServed = new List<SoldierRecord> { lev };
				SetTick(500000);
				var a = te.InUse(out int s1);
				check(a.Count == 5, "the list holds the loan and the four carried items (" + a.Count + ")");
				check(a.Count(e => e.Kind == InUseKind.Lent && ReferenceEquals(e.Item, loan)) == 1 && a.Count(e => e.Kind == InUseKind.Carried && e.Holder == "Pvt. Carry") == 4, "a loan is Lent, gear is Carried by the levy's name");
				lev.Gear.Accessories[2] = Make(ItemID.ShinyRedBalloon);
				te.InUse(out int s2);
				check(s2 == s1, "a change inside the 30-tick window is not looked at (the list is cached)");
				SetTick(500031);
				te.InUse(out int s3);
				check(s3 != s2 && te.InUse(out _).Count == 6, "after 30 ticks the new item shows and the stamp moves");
				SetTick(500062);
				te.InUse(out int s4);
				check(s4 == s3, "30 ticks with nothing changed leave the stamp alone (the panel does not rebuild)");
				te.NoteLoan(loan, "Drone Mk II"); SetTick(500093);
				var b = te.InUse(out int s5);
				check(s5 != s4 && b.Any(e => e.Kind == InUseKind.Lent && e.Holder == "Drone Mk II"), "a named loan carries its holder's name");
				te.ReturnLent(loan); SetTick(500124);
				var c = te.InUse(out int s6);
				check(s6 != s5 && c.Count == 5 && c.All(e => e.Kind == InUseKind.Carried), "a returned loan leaves the list");
			}
			finally { ArmoryTE.TestServed = saved; SetTick(t0); }
		}

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			try
			{
				// "Melee" filter, as the type button builds it: weapon-shaped and classed Melee.
				bool Melee(Item it) => !IsAirItem(it) && it.damage > 0 && WeaponClassifier.ItemDamageClass(it) == DamageClass.Melee;
				var storage = new Item[20];
				for (int i = 0; i < storage.Length; i++) storage[i] = new Item();
				storage[0] = Make(ItemID.CopperShortsword);
				storage[1] = Make(ItemID.Handgun);
				storage[2] = Make(ItemID.IronBroadsword);
				storage[3] = Make(ItemID.WoodenSword);
				storage[4] = Make(ItemID.Torch); storage[4].stack = 30;

				var matches = ArmoryView.Matches(storage, Melee);
				Check(matches.SequenceEqual(new[] { 0, 2, 3 }), "melee filter matches the three swords");

				// The player takes a sword out: its slot is empty and no longer matches.
				var sword = storage[2];
				storage[2] = new Item();
				matches = ArmoryView.Matches(storage, Melee);
				Check(matches.All(i => !IsAirItem(storage[i])), "BEFORE the fix: the filtered view holds only occupied cells, so there is nowhere to put the sword back");

				var shown = ArmoryView.WithDepositCell(storage, matches);
				Check(shown.Take(matches.Count).SequenceEqual(matches), "the deposit cell comes after every match");
				Check(shown.Count == matches.Count + 1 && IsAirItem(storage[shown[shown.Count - 1]]), "the view ends with one real empty slot");
				Check(shown.Distinct().Count() == shown.Count, "no cell is shown twice");

				// Put the sword back through the cell, as a click with the sword on the cursor does.
				storage[shown[shown.Count - 1]] = sword;
				Check(storage.Contains(sword), "the sword is back in storage");

				// A non-matching item (a Handgun, under the melee filter) can be deposited too: it simply leaves the view afterwards.
				storage[2] = new Item();
				var gun = Make(ItemID.Minishark);
				shown = ArmoryView.WithDepositCell(storage, ArmoryView.Matches(storage, Melee));
				storage[shown[shown.Count - 1]] = gun;
				Check(storage.Contains(gun) && !ArmoryView.Matches(storage, Melee).Contains(Array.IndexOf(storage, gun)), "a non-matching item deposits into the cell and drops out of the filtered view");

				// A full armory has no cell (nothing can be deposited then, filtered or not).
				var full = new Item[3];
				for (int i = 0; i < full.Length; i++) full[i] = Make(ItemID.CopperShortsword);
				Check(ArmoryView.WithDepositCell(full, ArmoryView.Matches(full, Melee)).Count == 3, "a full armory adds no cell");

				// Nothing matches at all: the deposit cell is still shown.
				var none = ArmoryView.WithDepositCell(storage, new List<int>());
				Check(none.Count == 1 && IsAirItem(storage[none[0]]), "an empty match list still shows the deposit cell");

				InUse(Check);
			}
			catch (Exception e) { failures.Add($"threw {e.GetType().Name}: {e.Message}"); }

			if (failures.Count == 0) Mod.Logger.Info($"Levy armory filter view self-check: {ok} checks OK");
			else
			{
				Mod.Logger.Warn($"Levy armory filter view self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
				foreach (var f in failures) Mod.Logger.Warn("  armory view: " + f);
			}
		}
	}
}
