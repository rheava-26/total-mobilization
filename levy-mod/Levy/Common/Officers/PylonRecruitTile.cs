using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.UI;

namespace Levy.Common.Officers
{
	/// <summary>
	/// The pylon network is the recruiting office: right-clicking an ACTIVE pylon opens the Officers panel, bound to the nearest barracks whose settlement is on the network, with no Barracks panel behind it.
	/// The commander commissions from there exactly as he would at a barracks (<see cref="OfficerSystem.CanCommission"/> accepts him beside any active pylon of the network). A pylon with no
	/// networked barracks anywhere does nothing, as vanilla's pylon does. Client only: it opens a menu.
	/// </summary>
	public class PylonRecruitTile : GlobalTile
	{
		public override void RightClick(int i, int j, int type)
		{
			if (Main.dedServ || Main.netMode == NetmodeID.Server) return;
			if (type != TileID.TeleportationPylon && TileLoader.GetTile(type) is not ModPylon) return; // vanilla's pylon tile, or a modded pylon (Calamity's)
			if (!SettlementSystem.Instance.IsActivePylonAt(i, j, out Vector2 center)) return;
			var te = SettlementSystem.Instance.RecruitingBarracksFor(center);
			if (te == null || UISystem.Instance == null) return;
			UISystem.Instance.OpenOfficersAtPylon(te, center);
		}
	}
}
