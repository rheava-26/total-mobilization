using Terraria;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	public partial class ArmoryTE
	{
		/// <summary>
		/// True when a clear-out must leave this stored item alone: it is reserved for a levy (a hand-assigned piece handed back by a re-arm), or the player prefers or sets its kind on the Rules tab.
		/// </summary>
		internal bool ClearOutProtected(Item it)
		{
			if (it == null || it.IsAir) return false;
			if (IsReservedRef(it)) return true;
			var o = Rules?.OverrideOf(it.type);
			return o != null && (o.Rank == RankOverrideMode.Prefer || o.Rank == RankOverrideMode.Set);
		}
	}
}
