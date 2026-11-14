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

		/// <summary>A blank cell in <see cref="Layout"/> (pads the row before the in-use block).</summary>
		public const int Spacer = int.MinValue;

		/// <summary>
		/// The grid's cells, in order: a value >= 0 is a real storage index, <c>~k</c> (negative) is the k-th in-use entry (a read-only cell for gear a levy carries or a weapon on loan), <see cref="Spacer"/> is a blank.
		/// Unfiltered (<paramref name="shown"/> null): every storage slot in order, with the in-use block on its own rows right after the last occupied slot, so the empty slots stay reachable behind it.
		/// Filtered: the shown slots, then the in-use block on its own rows. With no in-use entries the result is exactly the old one.
		/// </summary>
		public static List<int> Layout(Item[] storage, List<int> shown, List<int> inUse, int columns)
		{
			var cells = new List<int>((shown?.Count ?? storage?.Length ?? 0) + (inUse?.Count ?? 0) + columns);
			int len = storage?.Length ?? 0, next = 0;
			if (shown != null) cells.AddRange(shown);
			else
			{
				int last = -1;
				for (int i = 0; i < len; i++) if (storage[i] != null && !storage[i].IsAir && storage[i].stack > 0) last = i;
				for (; next <= last; next++) cells.Add(next);
			}
			if (inUse != null && inUse.Count > 0)
			{
				while (columns > 1 && cells.Count % columns != 0) cells.Add(shown == null && next < len ? next++ : Spacer);
				foreach (int k in inUse) cells.Add(~k);
			}
			for (; shown == null && next < len; next++) cells.Add(next);
			return cells;
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
