using System;
using System.Collections.Generic;
using Terraria;

namespace Levy.Common.UI
{
	/// <summary>
	/// Which real <see cref="Content.TileEntities.ArmoryTE.Storage"/> indices the armory grid shows while a search or filter is on. The grid packs the matching items with no gaps, so on its own it has no empty cell:
	/// an item the player took out leaves no slot to put it back into (and a cell dropped on would swap, not deposit). The view therefore always ends with one real empty slot, the deposit cell.
	/// Pure (no UI types) so the boot self-check can run it.
	/// </summary>
	public static class ArmoryView
	{
		/// <summary>Indices of every item that passes <paramref name="match"/>, in storage order.</summary>
		public static List<int> Matches(Item[] storage, Func<Item, bool> match)
		{
			var list = new List<int>();
			if (storage == null) return list;
			for (int i = 0; i < storage.Length; i++) if (match(storage[i])) list.Add(i);
			return list;
		}

		/// <summary>The matches followed by the first empty storage slot (when there is one), so any item can always be put back. Returns a new list.</summary>
		public static List<int> WithDepositCell(Item[] storage, List<int> matches)
		{
			var shown = new List<int>(matches);
			if (storage == null) return shown;
			for (int i = 0; i < storage.Length; i++)
				if (storage[i] == null || storage[i].IsAir || storage[i].stack <= 0) { shown.Add(i); break; }
			return shown;
		}
	}
}
