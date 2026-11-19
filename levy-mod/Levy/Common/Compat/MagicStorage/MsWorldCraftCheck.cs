using System;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// World check of crafting with armory racks through Magic Storage's Craft button entry (<c>MsWorldCraft</c>). Runs when a world loads on a dedicated server, only with LEVY_MSCRAFT_WORLD=1 and Magic Storage loaded
	/// (shipms.sh boots the ship world that way); LEVY_MSCRAFT_EXIT=1 stops the server afterwards. Names no Magic Storage type.
	/// </summary>
	public class MsWorldCraftCheck : ModSystem
	{
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_MSCRAFT_WORLD") != "1") return;
			int pass = 0, fail = 0;
			void Check(bool ok, string what) { if (ok) pass++; else fail++; Mod.Logger.Info($"[MsWorldCraft] {(ok ? "PASS" : "FAIL")} {what}"); }
			if (!ModLoader.HasMod("MagicStorage") || !MagicStorageCompatSystem.Active) Check(false, "Magic Storage is loaded and the compat is active");
			else { try { Run(Check); } catch (Exception e) { Check(false, "threw " + e.GetType().Name + ": " + e.Message); } }
			Mod.Logger.Info($"[MsWorldCraft] self-check pass={pass} fail={fail}");
			if (Environment.GetEnvironmentVariable("LEVY_MSCRAFT_EXIT") == "1") { try { Netplay.SaveOnServerExit = false; Netplay.Disconnect = true; } catch { } }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Run(Action<bool, string> check) => MsWorldCraft.Run(check, s => Mod.Logger.Info(s));
	}
}
