using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>Commander hotkeys. Runs only for the local player on a client / in single player.</summary>
	public class UIPlayer : ModPlayer
	{
		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Main.dedServ || Main.gameMenu || Player.whoAmI != Main.myPlayer) return;

			if (UISystem.FollowKey?.JustPressed == true) UIActions.Order(SoldierOrder.Follow, false);
			if (UISystem.HoldKey?.JustPressed == true) UIActions.Order(SoldierOrder.Hold, false);
			if (UISystem.ReturnKey?.JustPressed == true) UIActions.Order(SoldierOrder.ReturnToBarracks, false);
			if (UISystem.HoldFireKey?.JustPressed == true) UIActions.HoldFire(false);
			if (UISystem.RallyKey?.JustPressed == true) UIActions.Rally();
			if (UISystem.ArmyStripKey?.JustPressed == true) UISystem.Instance.ToggleArmyStrip();
			if (UISystem.MobilizeKey?.JustPressed == true)
			{
				var te = UIActions.NearestBarracks(Player.Center, LevyConstants.UI.MobilizeKeyRadiusTiles);
				if (te == null) Main.NewText(LevyUtil.T("UI2.NoBarracksNearby"), 200, 160, 120);
				else UIActions.ToggleMobilize(te);
			}
		}
	}
}
