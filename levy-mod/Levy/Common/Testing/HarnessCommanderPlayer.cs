using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>Harness only (inert unless LEVY_HARNESS is set): keeps the stand-in commander's life/mana maxima at 500/200 through the player update (a client-less player slot is otherwise reset to 100/20).</summary>
	public class HarnessCommanderPlayer : ModPlayer
	{
		public override void PostUpdateEquips()
		{
			if (!CombatHarness.Enabled || (Player.name != "LevyTester" && Player.name != "LevyTester2")) return;
			Player.statLifeMax2 = 500; Player.statManaMax2 = 200;
		}
	}
}
