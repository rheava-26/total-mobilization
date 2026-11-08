using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.Team
{
	/// <summary>Drives the team items: registers the packets, builds the registry once every mod's content exists, installs the hooks and runs the pulse (server / single player) every <see cref="LevyConstants.TeamPulseTicks"/> ticks.</summary>
	public sealed class TeamSystem : ModSystem
	{
		/// <summary>Harness / self-check switch: when false the pulse and hooks stand down.</summary>
		public static bool Enabled = true;

		public override void Load() => TeamNet.Register();

		public override void PostSetupContent()
		{
			try
			{
				TeamItems.Build();
				TeamSpectre.Install();
				TeamNebula.Install();
				TeamInfo.Install();
				if (ModLoader.HasMod("ThoriumMod")) InstallThorium();
				Mod.Logger.Info("Levy team items: " + TeamItems.BuildReport);
			}
			catch (Exception e) { Mod.Logger.Warn("Levy team items: setup failed, team items stay off for levies: " + e); }
		}

		private void InstallThorium()
		{
			try { TeamThorium.Install(); }
			catch (Exception e) { Mod.Logger.Warn("Levy team items: Thorium bard share hook failed, it stays off: " + e.Message); }
		}

		/// <summary>After every mod's content and recipes exist: the boot self-check (a throw-away Player runs a mod buff through the dummy path).</summary>
		public override void PostAddRecipes() => global::Levy.Common.Util.LoadSafe.Run(() => { TeamCheck.Run(Mod); TeamAccessory.SelfCheck(Mod); });

		public override void PostUpdateNPCs()
		{
			if (!Enabled || LevyUtil.IsClient || !TeamItems.Built) return;
			if (Main.GameUpdateCount % (uint)LevyConstants.TeamPulseTicks != 0u) return;
			try { TeamItems.Pulse(); }
			catch (Exception e) { TeamItems.WarnOnce("pulse", e); }
		}

		public override void Unload()
		{
			TeamSpectre.Uninstall();
			TeamNebula.Uninstall();
			TeamInfo.Uninstall();
			TeamItems.Auras.Clear(); TeamItems.StatBuffs.Clear(); TeamItems.ProjAuras.Clear(); TeamItems.HitAuras.Clear(); TeamItems.FrameBuffs.Clear(); TeamItems.ClearState();
		}
	}
}
