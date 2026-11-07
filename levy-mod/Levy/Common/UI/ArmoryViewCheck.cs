using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

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
