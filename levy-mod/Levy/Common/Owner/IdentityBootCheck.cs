using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Owner
{
	/// <summary>
	/// Boot self-check of the identity guard (no world needed), logged as "Levy identity self-check: pass=N fail=M". A levy's dummy is posed as a gear frame poses it (slot 255, Main.myPlayer = 255,
	/// the commander in slot 0 as the real local player), and PlayerLoader.ResetEffects is dispatched for it:
	/// the two stand-in ModPlayers must see it as the guard promises (hidden: not the local player; local view: the local player, with the commander's talk state), and, when AlchemistNPC Lite is loaded, its
	/// shop-chooser side menu must stay open while the commander is in conversation (and, with the guard off, close: the defect the guard exists for).
	/// A headless boot has no client: the drawn menu and the click are NOT exercised; the check runs the hook the click's menu is closed by.
	/// </summary>
	public class IdentityBootCheck : ModSystem
	{
		public override void PostAddRecipes() => global::Levy.Common.Util.LoadSafe.Run(Run);

		private int pass, fail;
		private readonly List<string> lines = new();

		private void Check(string what, bool ok, string detail)
		{
			if (ok) pass++; else fail++;
			lines.Add($"  [{(ok ? "PASS" : "FAIL")}] {what}: {detail}");
		}

		private void Run()
		{
			pass = fail = 0; lines.Clear();
			int my = Main.myPlayer, net = Main.netMode; Player slot255 = Main.player[255]; Player real = Main.player[0];
			int realTalk = real?.talkNPC ?? -1; bool disabled = IdentityGuard.Disabled;
			int token = 0; var dummy = new Player();
			try
			{
				if (real == null) { Check("setup", false, "no player in slot 0"); return; }
				dummy.whoAmI = 255; SoldierOwner.RegisterDummy(dummy);
				Main.player[255] = dummy; Main.myPlayer = 255;
				token = SoldierMinions.BeginFake(0); // the commander is slot 0, as in a single-player gear frame
				real.SetTalkNPC(3, true);            // the commander is talking to NPC 3

				// 1. the two stand-ins
				Array.Clear(IdentityProbeHidden.Seen); Array.Clear(IdentityProbeGear.Seen); IdentityProbeBase.On = true;
				IdentityGuard.Disabled = false;
				try { PlayerLoader.ResetEffects(dummy); } finally { IdentityProbeBase.On = false; }
				long[] h = IdentityProbeHidden.Seen, g = IdentityProbeGear.Seen;
				bool installed = IdentityGuard.Wrapped > 0 && IdentityGuard.WrappedNames.Any(n => n.StartsWith("Levy:IdentityProbeHidden"));
				Check("guard installed", installed, $"{IdentityGuard.Wrapped} ModPlayer hooks wrapped");
				Check("hidden view", h[0] == 1 && h[1] == 0 && h[2] == 0 && h[3] == 0, $"a ModPlayer on the hidden view was handed the dummy {h[0]}x, as the local player {h[1]}x, LocalPlayer a dummy {h[2]}x, wrong talk NPC {h[3]}x");
				Check("local view", g[0] == 1 && g[1] == 1 && g[3] == 0, $"a ModPlayer on the local view was handed the dummy {g[0]}x, as the local player {g[1]}x, wrong talk NPC {g[3]}x");

				// 2. AlchemistNPC Lite: the side menu of a shop NPC
				if (ModLoader.TryGetMod("AlchemistNPCLite", out Mod alch))
				{
					Type ui = alch.Code.GetType("AlchemistNPCLite.Interface.ShopChangeUI");
					FieldInfo vis = ui?.GetField("visible", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					if (vis == null) Check("AlchemistNPC side menu", false, "ShopChangeUI.visible not found (the mod changed?)");
					else
					{
						bool Ticks(bool guardOn)
						{
							IdentityGuard.Disabled = !guardOn; vis.SetValue(null, true);
							Main.netMode = NetmodeID.SinglePlayer; // its reset only runs on a client or in single player
							try { dummy.ResetEffects(); } finally { Main.netMode = net; }
							return (bool)vis.GetValue(null);
						}
						bool openOn = Ticks(true), openOff = Ticks(false);
						vis.SetValue(null, false);
						Check("AlchemistNPC side menu", openOn, $"a levy's gear frame with the commander in conversation: menu {(openOn ? "stays open" : "CLOSED")} with the guard on; with the guard off it {(openOff ? "stays open (defect not reproduced)" : "closes")}");
					}
				}
			}
			catch (Exception e) { Check("self-check", false, "threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
			finally
			{
				IdentityGuard.Disabled = disabled; IdentityProbeBase.On = false;
				if (token != 0 || true) SoldierMinions.EndFake(token);
				Main.player[255] = slot255; Main.myPlayer = my; Main.netMode = net;
				try { if (real != null) real.SetTalkNPC(realTalk, true); } catch (Exception) { }
			}
			var sb = new StringBuilder();
			sb.Append($"Levy identity self-check: pass={pass} fail={fail}");
			foreach (string l in lines) sb.Append('\n').Append(l);
			if (fail == 0) Mod.Logger.Info(sb.ToString()); else Mod.Logger.Warn(sb.ToString());
		}
	}
}
