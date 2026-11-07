using System;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Calamity compat for enemy targeting. In Revengeance and Death Calamity replaces the AI of King Slime, the Eye of Cthulhu and every other vanilla boss (CalamityVanillaAIOverrideNPC.PreAI runs the override
	/// and returns false). Two things in that path never reached Levy:
	///  * Its overrides pick targets with <c>CalamityUtils.CalamityTargeting</c>, its own scan over Main.ActivePlayers. It is not <c>NPC.TargetClosest</c>, so none of Levy's search hooks run: a Spectator was a valid player to it,
	///    and it can only ever pick a player, never a levy.
	///  * Calamity loads before Levy, so its PreAI (and the whole override AI) ran BEFORE Levy's PreAI put a levy's stand-in in slot 255: the AI read the commander every tick.
	/// Fix: an IL hook on CalamityTargeting hides spectators from it (not from an unbeaten boss) and, on every return, lets the levies compete exactly as after a vanilla search; and a hook on Calamity's PreAI runs
	/// <see cref="EnemyTargetingGlobalNPC.Prepare"/> first so slot 255 holds the chosen levy during the override AI. Installed only when Calamity is loaded.
	/// </summary>
	public class CalamityTargetingCompat : ModSystem
	{
		private delegate bool OrigPreAI(GlobalNPC self, NPC npc);
		private static readonly bool[] _hid = new bool[Main.maxNPCs + 1];
		public static bool Installed { get; private set; }
		public static bool AiHookInstalled { get; private set; }

		public override void PostSetupContent()
		{
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				if (Environment.GetEnvironmentVariable("LEVY_CALCOMPAT") == "0") { Mod.Logger.Info("Levy Calamity targeting compat: disabled by LEVY_CALCOMPAT=0 (test switch)"); return; }
				Type util = cal.Code.GetType("CalamityMod.CalamityUtils");
				if (util != null)
					foreach (MethodInfo m in util.GetMethods(BindingFlags.Public | BindingFlags.Static))
					{
						if (m.Name != "CalamityTargeting" || m.GetParameters().Length != 2 || m.GetParameters()[0].ParameterType != typeof(NPC)) continue;
						MonoModHooks.Modify(m, Manipulate);
						Installed = true;
						break;
					}
				Type g = cal.Code.GetType("CalamityMod.NPCs.CalamityVanillaAIOverrideNPC");
				MethodInfo pre = g?.GetMethod("PreAI", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(NPC) }, null);
				if (pre != null) { MonoModHooks.Add(pre, (Func<OrigPreAI, GlobalNPC, NPC, bool>)PreAiDetour); AiHookInstalled = true; }
				Mod.Logger.Info($"Levy Calamity targeting compat: CalamityTargeting hook {(Installed ? "installed" : "NOT found")}, PreAI hook {(AiHookInstalled ? "installed" : "NOT found")}");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy Calamity targeting compat failed: " + e); }
		}

		private static void Manipulate(ILContext il)
		{
			var c = new ILCursor(il);
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Action<NPC>>(Enter);
			while (c.TryGotoNext(MoveType.Before, i => i.MatchRet()))
			{
				c.MoveBeforeLabels();
				c.Emit(OpCodes.Ldarg_0);
				c.EmitDelegate<Action<NPC>>(Leave);
				c.Index++; // past the ret
			}
		}

		private static void Enter(NPC npc)
		{
			if (npc == null || npc.whoAmI < 0 || npc.whoAmI >= Main.maxNPCs) return;
			if (EnemyTargeting.DiagOn) Potions.SpectatorDiagnostic.NoteCalamityTargeting(npc);
			bool hide = EnemyTargeting.HidesSpectatorFrom(npc);
			_hid[npc.whoAmI] = hide;
			if (hide) Potions.SpectatorPlayer.HideAll();
		}

		private static void Leave(NPC npc)
		{
			if (npc == null || npc.whoAmI < 0 || npc.whoAmI >= Main.maxNPCs) return;
			if (_hid[npc.whoAmI]) { _hid[npc.whoAmI] = false; Potions.SpectatorPlayer.ReleaseAll(); }
			EnemyTargeting.CompeteAfterExternalSearch(npc);
		}

		private static bool PreAiDetour(OrigPreAI orig, GlobalNPC self, NPC npc)
		{
			try { EnemyTargetingGlobalNPC.Prepare(npc); } catch (Exception) { }
			return orig(self, npc);
		}
	}
}
