using System;
using System.IO;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
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
		private delegate void OrigSendExtraAI(GlobalNPC self, NPC npc, BitWriter bits, BinaryWriter writer);
		private static readonly bool[] _hid = new bool[Main.maxNPCs + 1];
		public static bool Installed { get; private set; }
		public static bool AiHookInstalled { get; private set; }
		/// <summary>The SendExtraAI hook that keeps a dead NPC's override through its own AI pass (see <see cref="SendExtraAiDetour"/>) is installed.</summary>
		public static bool SendHookInstalled { get; private set; }
		/// <summary>LEVY_CALOVERRIDESLOT=0 turns the flyer-slot route for Calamity-driven native types off (A/B switch for the lab and the world check).</summary>
		internal static readonly bool OverrideSlotOn = Environment.GetEnvironmentVariable("LEVY_CALOVERRIDESLOT") != "0";
		private static GlobalNPC _overrideGlobal;
		private static FieldInfo _overrideField;
		/// <summary>
		/// True while Calamity's vanilla-AI override (CalamityVanillaAIOverrideNPC.AIOverride, bound in its SetDefaults from Revengeance up) drives this NPC. Those AIs index <c>Main.player[NPC.target]</c> without a range check
		/// at their attack lines (Deerclops' shadow hands, the bees' steering), so a 300+ NPC-target index throws IndexOutOfRange there. <see cref="EnemyTargeting.UsesNativeTargeting"/> reads this to keep such an enemy off the native
		/// route and on the flyer slot (a real player index).
		/// </summary>
		internal static bool OverrideBound(NPC npc)
		{
			if (!OverrideSlotOn || _overrideGlobal == null || _overrideField == null) return false;
			try { return npc.TryGetGlobalNPC(_overrideGlobal, out GlobalNPC inst) && _overrideField.GetValue(inst) != null; }
			catch (Exception) { return false; }
		}

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
				if (g != null && cal.TryFind("CalamityVanillaAIOverrideNPC", out GlobalNPC og)) { _overrideGlobal = og; _overrideField = g.GetField("AIOverride", BindingFlags.Public | BindingFlags.Instance); }
				MethodInfo pre = g?.GetMethod("PreAI", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(NPC) }, null);
				if (pre != null) { MonoModHooks.Add(pre, (Func<OrigPreAI, GlobalNPC, NPC, bool>)PreAiDetour); AiHookInstalled = true; }
				MethodInfo send = g?.GetMethod("SendExtraAI", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(NPC), typeof(BitWriter), typeof(BinaryWriter) }, null);
				if (send != null && pre != null && _overrideField != null) { MonoModHooks.Add(send, (Action<OrigSendExtraAI, GlobalNPC, NPC, BitWriter, BinaryWriter>)SendExtraAiDetour); SendHookInstalled = true; }
				Mod.Logger.Info($"Levy Calamity targeting compat: CalamityTargeting hook {(Installed ? "installed" : "NOT found")}, PreAI hook {(AiHookInstalled ? "installed" : "NOT found")}, SendExtraAI hook {(SendHookInstalled ? "installed" : "NOT found")}");
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

		/// <summary>The NPC (whoAmI) whose Calamity PreAI is running right now, or -1.</summary>
		private static int _inPreAi = -1;
		/// <summary>How many times a dead NPC's override was put back after its own AI cleared it, and how many NullReferenceExceptions the fallback below answered (lab counters for the world check).</summary>
		public static int OverrideKept, DeathNreAnswered;

		/// <summary>
		/// Moon Lord's override AI finishes the core's death INSIDE the AI pass: it deactivates the parts and the core and calls NetMessage.SendData(SyncNPC) for each (the dedicated-server path). SendData runs Calamity's
		/// SendExtraAI, which clears <c>AIOverride</c> of an NPC that is no longer active. When the AI returns, CalamityVanillaAIOverrideNPC.PreAI reads the field once more (<c>AIOverride.DisableMultiplayerSmoothing</c>)
		/// and throws a NullReferenceException, on the last tick of every Moon Lord kill on a server (in single player SendData does nothing and the field stays set).
		/// While an NPC's own PreAI is running, the SendExtraAI hook below lets Calamity write its packet as it always does and puts the override back afterwards, so the pass finishes with the same field a single-player
		/// pass has. Nothing runs the override again: the NPC is dead.
		/// </summary>
		private static void SendExtraAiDetour(OrigSendExtraAI orig, GlobalNPC self, NPC npc, BitWriter bits, BinaryWriter writer)
		{
			if (_overrideField == null || npc == null || npc.whoAmI != _inPreAi) { orig(self, npc, bits, writer); return; }
			object bound = _overrideField.GetValue(self);
			orig(self, npc, bits, writer);
			if (bound != null && _overrideField.GetValue(self) == null) { _overrideField.SetValue(self, bound); OverrideKept++; }
		}

		private static bool PreAiDetour(OrigPreAI orig, GlobalNPC self, NPC npc)
		{
			try { EnemyTargetingGlobalNPC.Prepare(npc); } catch (Exception) { }
			object bound = _overrideField?.GetValue(self);
			int outer = _inPreAi; _inPreAi = npc.whoAmI;
			try { return orig(self, npc); }
			// Fallback for a Calamity whose SendExtraAI could not be hooked: the same exception, answered only when it is provably this one (the override was bound when the pass began, is cleared now, and the NPC
			// is dead). The pass has run, so the vanilla AI it replaces is skipped. Any other NullReferenceException (the override still bound, or a live NPC) is not this and goes on up.
			catch (NullReferenceException) when (bound != null && _overrideField.GetValue(self) == null && (!npc.active || npc.life <= 0)) { DeathNreAnswered++; return false; }
			finally { _inPreAi = outer; }
		}
	}
}
