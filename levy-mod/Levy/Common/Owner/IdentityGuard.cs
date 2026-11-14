using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Owner
{
	/// <summary>
	/// THE IDENTITY GUARD: another mod's ModPlayer never mistakes a levy's dummy Player for the human at the keyboard.
	/// <para/><b>Why it exists.</b> A levy's dummy has to be "local" for vanilla and for gear: <c>whoAmI == Main.myPlayer</c> blocks (accessory casts, set bonuses, owner-gated spawns) only run for the local
	/// player, and Main.myPlayer is the one switch that opens them. Levy therefore lends Main.myPlayer to the dummy for the length of a gear frame or a weapon swap (<see cref="SoldierOwner"/>, SoldierNPC.GearFrame,
	/// HeldWeaponSwap, SoldierMinions). Every ModPlayer of every other mod is dispatched for the dummy inside that window (ResetEffects, UpdateEquips, PostUpdateMiscEffects ...), and a mod that reads
	/// <c>Main.player[Main.myPlayer]</c> / <c>Main.LocalPlayer</c>, or runs UI code under <c>Player.whoAmI == Main.myPlayer</c>, then acts on the dummy as if it were the player.
	/// Proved with AlchemistNPC Lite: its ModPlayer.ResetEffects hides the shop-chooser side menus whenever <c>Main.player[Main.myPlayer].talkNPC == -1</c>, so every levy's gear frame (SoldierNPC.AI -> RefreshStats ->
	/// GearFrame -> Player.ResetEffects -> that hook) closed the menu in the tick it opened. LuiAFK's UI reset did the same through <c>whoAmI == Main.myPlayer</c>.
	/// <para/><b>The two views.</b> A ModPlayer that is dispatched for a dummy that is the local player gets one of two views, decided per mod (<see cref="ViewOf"/>):
	/// <list type="bullet">
	/// <item><b>Hidden</b> (the default): Main.myPlayer is put back to the real local player for the length of the hook (or, on a dedicated server, which has none, to a slot that is nobody's). The dummy looks like any
	/// other non-local player to that ModPlayer: <c>whoAmI != Main.myPlayer</c> and <c>Main.LocalPlayer</c> is the commander with their own talk, chest and sign. UI, shop and utility mods are never fooled.</item>
	/// <item><b>Local</b>: for mods that provide gear a levy can wear (Thorium, Calamity, ...). Their ModPlayer IS the gear logic (accessory casts, timers, set effects gated on <c>whoAmI == Main.myPlayer</c>), measured
	/// by the gear-parity self-check: hiding the dummy from them stops those casts for levies. They keep seeing the dummy as local, but its talk / chest / sign state is mirrored from the real local player for the
	/// length of the hook, so a "no conversation, close the panel" test reads the commander's real state.</item></list>
	/// The equip pass, ModItem and ModBuff hooks, set bonuses and weapon use are never touched: they keep running with the dummy local, so everything gear does for a levy keeps working.
	/// <para/><b>Where.</b> The state hooks (<see cref="StateHooks"/>: the per-tick update family) of every ModPlayer type of every loaded mod are wrapped once, at load (IL: enter at the top, exit before each return).
	/// The item-use family (CanUseItem, ModifyShootStats, Shoot, ...) is deliberately left alone: those run inside a use window, where the dummy IS the player using an item, and the real-player side of that window
	/// is protected by <see cref="UseGuard"/>. Env LEVY_IDENTITY_GUARD=0 switches the guard off (the unfixed behaviour, for A/B in the self-checks).
	/// </summary>
	public sealed class IdentityGuard : ModSystem
	{
		/// <summary>Lab A/B switch.</summary>
		public static bool Disabled = Environment.GetEnvironmentVariable("LEVY_IDENTITY_GUARD") == "0";

		/// <summary>The ModPlayer hooks that run in a levy's per-tick update (see the class summary). Everything else (use, shoot, hit, hurt, draw, fishing, shop ...) is untouched.</summary>
		public static readonly HashSet<string> StateHooks = new()
		{
			"ResetEffects", "ResetInfoAccessories", "RefreshInfoAccessoriesFromTeamPlayers", "ModifyMaxStats", "UpdateDead", "UpdateBadLifeRegen", "UpdateLifeRegen",
			"NaturalLifeRegen", "UpdateAutopause", "PreUpdate", "SetControls", "PreUpdateBuffs", "PostUpdateBuffs", "UpdateEquips", "PostUpdateEquips", "UpdateVisibleAccessories",
			"UpdateVisibleVanityAccessories", "UpdateDyes", "PostUpdateMiscEffects", "PostUpdateRunSpeeds", "PreUpdateMovement", "PostUpdate", "FrameEffects", "ModifyScreenPosition", "ModifyZoom",
			"ArmorSetBonusActivated", "ArmorSetBonusHeld", "OnRespawn", "SyncPlayer", "CopyClientState", "SendClientChanges",
		};

		public enum View { Local, Hidden }

		/// <summary>Dispatches made while a dummy was the local player: hidden ones (Main.myPlayer put back), and local ones (state mirrored). Total and per mod.</summary>
		public static long Hidden, Mirrored;
		public static readonly Dictionary<string, long> HiddenBy = new(), MirroredBy = new();
		/// <summary>Number of ModPlayer hook overrides wrapped at load.</summary>
		public static int Wrapped;
		/// <summary>"Mod:Type.Hook" of each wrapped override (the self-check lists them).</summary>
		public static readonly List<string> WrappedNames = new();
		/// <summary>Types whose view is fixed by a test (the identity probes).</summary>
		internal static readonly Dictionary<Type, View> Forced = new();

		private static readonly Dictionary<Mod, bool> _gearMod = new();
		private static readonly Dictionary<Type, View> _views = new();
		private static readonly HashSet<string> _own = new() { "Levy", "Mechanization", "Warbands" };

		private struct Entry { public int Kind, MyPlayer, Talk, Chest, Sign; public Player Dummy; public string Key; }
		// per thread: the network thread dispatches some ModPlayer hooks too, and its calls must never meet the main thread's
		[ThreadStatic] private static Entry[] _stack; [ThreadStatic] private static int _depth;
		private const int KNone = 0, KHidden = 1, KMirror = 2;

		/// <summary>True when the mod provides armor or an accessory that a levy can wear: its ModPlayer is gear logic and must keep the local view.</summary>
		public static bool IsGearMod(Mod mod)
		{
			if (mod == null) return false;
			if (_gearMod.TryGetValue(mod, out bool g)) return g;
			g = false;
			try
			{
				foreach (ModItem mi in mod.GetContent<ModItem>())
				{
					Item s = mi.Item;
					if (s == null || s.vanity) continue;
					if (s.accessory || s.headSlot >= 0 || s.bodySlot >= 0 || s.legSlot >= 0) { g = true; break; }
				}
			}
			catch (Exception) { g = true; } // unknown: keep what worked before
			_gearMod[mod] = g;
			return g;
		}

		/// <summary>The view a ModPlayer gets of a dummy that is the local player.</summary>
		public static View ViewOf(ModPlayer mp)
		{
			Type t = mp.GetType();
			if (_views.TryGetValue(t, out View v)) return v;
			if (!Forced.TryGetValue(t, out v)) v = _own.Contains(mp.Mod.Name) || IsGearMod(mp.Mod) ? View.Local : View.Hidden;
			_views[t] = v;
			return v;
		}

		public override void PostSetupContent()
		{
			try { Install(); }
			catch (Exception e) { Mod.Logger.Warn("Levy: identity guard could not be installed: " + e.Message); }
		}

		private static void Install()
		{
			var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
			Forced[typeof(IdentityProbeHidden)] = View.Hidden; Forced[typeof(IdentityProbeGear)] = View.Local; // the stand-ins the identity check reads
			var seen = new HashSet<MethodInfo>();
			foreach (Mod mod in ModLoader.Mods)
			{
				if (mod.Name == "ModLoader") continue; // tML's own ModPlayers are not another mod's
				// the mod's own ModPlayer content (never GetTypes on its assembly: a weak reference that is not loaded makes that throw)
				foreach (ModPlayer proto in mod.GetContent<ModPlayer>())
				{
					// the overrides declared by the type and by every base class below ModPlayer (an abstract base of a family of ModPlayers declares them once)
					for (Type t = proto.GetType(); t != null && t != typeof(ModPlayer) && t != typeof(object); t = t.BaseType)
					{
						if (t.ContainsGenericParameters) continue;
						foreach (MethodInfo m in t.GetMethods(flags))
						{
							if (!StateHooks.Contains(m.Name) || !m.IsVirtual || m.IsAbstract || m.ReturnType != typeof(void) || m.GetBaseDefinition().DeclaringType != typeof(ModPlayer) || m.GetMethodBody() == null || !seen.Add(m)) continue;
							try { MonoModHooks.Modify(m, Wrap); Wrapped++; WrappedNames.Add(mod.Name + ":" + t.Name + "." + m.Name); }
							catch (Exception e) { mod.Logger.Debug("Levy identity guard: " + t.Name + "." + m.Name + " not wrapped: " + e.Message); }
						}
					}
				}
			}
		}

		/// <summary>
		/// The hook's body becomes <c>Enter(); try { original } finally { Exit(); }</c>: every return of the original is turned into a <c>leave</c> to one shared return (branches that jumped to a
		/// shared return still land on it), so the view is closed on every path, a thrown exception included.
		/// </summary>
		private static void Wrap(ILContext il)
		{
			var c = new ILCursor(il);
			var rets = il.Instrs.Where(i => i.OpCode == OpCodes.Ret).ToList();
			Instruction first = il.Instrs[0];
			c.Index = 0;
			c.Emit(OpCodes.Ldarg_0);
			c.Emit(OpCodes.Ldstr, il.Method.Name);
			c.EmitDelegate<Action<ModPlayer, string>>(Enter);
			c.Index = il.Instrs.Count;
			int handlerAt = c.Index;
			c.EmitDelegate<Action>(Exit);
			c.Emit(OpCodes.Endfinally);
			c.Emit(OpCodes.Ret);
			Instruction handler = il.Instrs[handlerAt], finalRet = il.Instrs[il.Instrs.Count - 1];
			foreach (Instruction r in rets) { r.OpCode = OpCodes.Leave; r.Operand = finalRet; }
			il.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally) { TryStart = first, TryEnd = handler, HandlerStart = handler, HandlerEnd = finalRet });
		}

		/// <summary>True when <paramref name="p"/> is a levy's dummy standing in as the machine's local player.</summary>
		public static bool IsDummyLocal(Player p) => p != null && p.whoAmI == Main.myPlayer && SoldierOwner.IsDummy(p);

		/// <summary>The slot a hidden ModPlayer sees as Main.myPlayer: the real local player's, or on a machine with none (a dedicated server, where the dummy's slot IS Main.myPlayer) a slot nobody holds.</summary>
		private static int PresentedLocal(Player dummy)
		{
			int real = SoldierMinions.TrueMyPlayer;
			if (real != dummy.whoAmI && real >= 0 && real < Main.maxPlayers) return real;
			int s = Main.maxPlayers - 1; // 254: a parked null player; never the dummy's slot (255)
			return s == dummy.whoAmI ? Main.maxPlayers - 2 : s;
		}

		/// <summary>Lab only: per "Mod:Type.Hook", how many dispatches were for a dummy and how many of those had the dummy as the local player.</summary>
		public static long Enters, Exits, Leaked; public static bool Instrument; public static readonly Dictionary<string, (long dummy, long local)> Census = new();

		private static void Enter(ModPlayer mp, string hook)
		{
			if (Instrument && mp.Player != null && SoldierOwner.IsDummy(mp.Player))
			{
				string key = mp.Mod.Name + ":" + mp.GetType().Name + "." + hook; Census.TryGetValue(key, out var c);
				Census[key] = (c.dummy + 1, c.local + (mp.Player.whoAmI == Main.myPlayer ? 1 : 0));
			}
			Enters++;
			_stack ??= new Entry[64];
			if (_depth >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
			ref Entry e = ref _stack[_depth++];
			e.Kind = KNone; e.Key = Instrument ? mp.GetType().Name + "." + hook : null; if (e.Key != null) EnterKeys[e.Key] = EnterKeys.TryGetValue(e.Key, out int en) ? en + 1 : 1;
			Player p = mp.Player;
			if (Disabled || p == null || p.whoAmI != Main.myPlayer || !SoldierOwner.IsDummy(p)) return;
			string modName = mp.Mod.Name;
			if (ViewOf(mp) == View.Hidden)
			{
				e.Kind = KHidden; e.MyPlayer = Main.myPlayer;
				Main.myPlayer = PresentedLocal(p);
				Hidden++; HiddenBy[modName] = HiddenBy.TryGetValue(modName, out long n) ? n + 1 : 1;
			}
			else
			{
				int real = PresentedLocal(p);
				Player rp = real >= 0 && real < Main.player.Length ? Main.player[real] : null;
				if (rp == null || rp == p) return;
				e.Kind = KMirror; e.Dummy = p; e.Talk = p.talkNPC; e.Chest = p.chest; e.Sign = p.sign;
				p.SetTalkNPC(rp.talkNPC, true); p.chest = rp.chest; p.sign = rp.sign;
				Mirrored++; MirroredBy[modName] = MirroredBy.TryGetValue(modName, out long n) ? n + 1 : 1;
			}
		}

		private static void Exit()
		{
			Exits++;
			if (_depth <= 0) return;
			ref Entry e = ref _stack[--_depth];
			if (Instrument && e.Key != null) ExitKeys[e.Key] = ExitKeys.TryGetValue(e.Key, out int xn) ? xn + 1 : 1;
			if (e.Kind == KHidden) Main.myPlayer = e.MyPlayer;
			else if (e.Kind == KMirror) { e.Dummy.SetTalkNPC(e.Talk, true); e.Dummy.chest = e.Chest; e.Dummy.sign = e.Sign; e.Dummy = null; }
			e.Kind = KNone;
		}

		/// <summary>Marks the guard's depth; <see cref="Release"/> unwinds anything a hook that threw left open (the hook's own Exit never ran).</summary>
		public static int Mark() => _depth;
		public static void Release(int mark) { while (_depth > mark) Exit(); }

		public static readonly Dictionary<string, int> LeakedKeys = new(), ExitKeys = new(), EnterKeys = new();
		public override void PreUpdateNPCs() { if (_depth != 0) { Leaked += _depth; if (Instrument) for (int i = 0; i < _depth; i++) { string k = _stack[i].Key ?? "?"; LeakedKeys[k] = LeakedKeys.TryGetValue(k, out int n) ? n + 1 : 1; } Release(0); } }
	}
}
