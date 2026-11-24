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
			Player.statLifeMax2 = CombatHarness.HlActive ? CombatHarness.HlBigLife : 500; Player.statManaMax2 = 200;
		}

		/// <summary>
		/// A parity run's stand-in commander uses her weapon once a tick through the world check's own ItemCheck call (<see cref="IdentityWorldCheck.HoldCommanderUse"/>). The server's player update ran her ItemCheck a second time in the
		/// same tick, so her use animation and use time counted down twice a tick: a Wooden Bow drew every 16 ticks instead of 30, a Minishark fired every 4 instead of 8, and an auto-reusing gun started a second, shotless use (observed
		/// 2026-11-23, gunparity). While the check drives her use, the server's own pass is skipped: one ItemCheck a tick, as a real player gets.
		/// </summary>
		public override bool PreItemCheck() => !(IdentityWorldCheck.StandInDrivesUse && !IdentityWorldCheck.InStandInUse && Player.whoAmI == 0);
	}

	/// <summary>Harness only: a client-less slot is flagged inactive between frames by the server loop, so a levy's commander lookup (every 60 ticks) sometimes found nobody and the squad idled. Re-asserts the stand-in before NPCs think.</summary>
	public class HarnessCommanderPinNpc : GlobalNPC
	{
		public override bool PreAI(NPC npc) { if (CombatHarness.Enabled) { Player p = Main.player[0]; if (p.name == "LevyTester" || p.name == "LevyTester2") { p.active = true; p.dead = false; } } return true; }
	}

	public class HarnessCommanderPin : ModSystem
	{
		public override void PreUpdateNPCs()
		{
			if (!CombatHarness.Enabled) return;
			Player p = Main.player[0];
			if (p.name == "LevyTester" || p.name == "LevyTester2") { p.active = true; p.dead = false; }
		}
	}
}
