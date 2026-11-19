using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// What a crafting station outside the armory (Magic Storage's Crafting Interface, through an Armory Heart) may draw from: the racks (bulk <see cref="Storage"/> and the weapon rack <see cref="IssueWeapons"/>).
	/// Never offered: a favorited item, coins, an item a Re-arm holds back for its levy this tick (<see cref="IsReservedRef"/>, the hand-assigned gear <see cref="ReclaimLocked"/> gives back), and anything a levy
	/// carries or a drone borrows (those are not on the racks). Server / single player only: on a multiplayer client the racks are a copy and are never offered.
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>The rack arrays a craft may draw from, in the order they are offered.</summary>
		public IEnumerable<Item[]> CraftRacks() { yield return Storage; yield return IssueWeapons; }

		/// <summary>True when a craft may use this rack item.</summary>
		public bool CraftOffers(Item it) => Main.netMode != NetmodeID.MultiplayerClient && !IsAir(it) && !it.favorited && !it.IsACoin && !IsReservedRef(it);

		/// <summary>True when this exact instance sits on one of the racks.</summary>
		public bool OnRacks(Item it)
		{
			if (IsAir(it)) return false;
			foreach (var arr in CraftRacks()) if (IndexOfRef(arr, it) >= 0) return true;
			return false;
		}

		/// <summary>
		/// Takes up to <paramref name="n"/> of THIS instance off the racks for a craft (it must still be on a rack and still offered); an emptied slot becomes air. Returns how many were taken.
		/// The taken items leave the game: the craft that asked has consumed them.
		/// </summary>
		public int TakeForCraft(Item backing, int n)
		{
			if (n <= 0 || !CraftOffers(backing)) return 0;
			foreach (var arr in CraftRacks())
			{
				int idx = IndexOfRef(arr, backing);
				if (idx < 0) continue;
				int k = System.Math.Min(n, backing.stack);
				backing.stack -= k;
				if (backing.stack <= 0) arr[idx] = new Item();
				MarkDirty();
				return k;
			}
			return 0;
		}
	}
}
