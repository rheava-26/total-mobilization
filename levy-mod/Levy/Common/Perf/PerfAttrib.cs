using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace Levy.Common.Perf
{
	/// <summary>
	/// Attribution by mod for the things the game does not call through one common door: map layers, interface layers and the AI hooks other mods add to NPCs and projectiles.
	/// <para/>Map layers: the Draw method of every ModMapLayer (any mod) is wrapped once, after content loads, with a call that reads the clock on entry and exit. This is what finds a layer that redraws something every frame (a remote's range ring, say).
	/// <para/>Interface layers: the list of layers is rebuilt every frame by the game and the mods; after the last mod has had its say, each layer's draw delegate is swapped for one that times the original (a holder object per layer name, no allocation per frame).
	/// <para/>Global hooks: the AI, PreAI and PostAI overrides of every GlobalNPC and GlobalProjectile are wrapped the same way, but they only read the clock on the one update in eleven that is sampled. Their time is taken out of the NPC's or projectile's owner and charged to the mod that owns the hook.
	/// Every wrap is its own try/catch: a method that cannot be wrapped is left as it was.
	/// </summary>
	public partial class PerfAnalyzer
	{
		// ------------------------------------------------------------------------------------------ slots: map layers, interface layers, global hooks
		private const int MaxSlots = 1024;
		private static readonly string[] _slotCat = new string[MaxSlots], _slotName = new string[MaxSlots];
		private static readonly long[] _slotT = new long[MaxSlots], _slotN = new long[MaxSlots];
		private static int _slotCount; private static readonly Dictionary<string, int> _slotIds = new();

		/// <summary>The slot of a (category, name) pair, made on first use.</summary>
		internal static int Slot(string cat, string name)
		{
			lock (_slotIds)
			{
				string key = cat + "|" + name;
				if (_slotIds.TryGetValue(key, out int id)) return id;
				if (_slotCount >= MaxSlots) return MaxSlots - 1;
				id = _slotCount++; _slotCat[id] = cat; _slotName[id] = name; _slotIds[key] = id; return id;
			}
		}

		private static void ResetSlots() { Array.Clear(_slotT, 0, _slotCount); Array.Clear(_slotN, 0, _slotCount); }

		private static void AddSlots(List<PerfItem> items, double sampledScale)
		{
			for (int i = 0; i < _slotCount; i++)
			{
				if (_slotT[i] <= 0) continue;
				double scale = _slotCat[i] == PerfCat.Glob ? sampledScale : 1.0; // global hooks are timed on sampled updates only; map and interface layers on every call
				items.Add(new PerfItem(_slotCat[i], _slotName[i], Ms(_slotT[i]) * scale, _slotN[i] * scale));
			}
		}

		/// <summary>Time added to a slot (also how the self-check feeds the interface layer path).</summary>
		internal static void AddSlot(int slot, long ticks) { _slotT[slot] += ticks; _slotN[slot]++; }

		// ------------------------------------------------------------------------------------------ the enter and exit calls the wrapped methods make
		private const int Depth = 64;
		private static readonly int[] _hSlot = new int[Depth]; private static readonly long[] _hT = new long[Depth], _hChild = new long[Depth];
		private static int _gsp; private static long _globAcc;

		/// <summary>Global AI hooks, sampled: called first in a wrapped hook. Public because the wrapped code of other mods calls it.</summary>
		public static void HookEnter(int slot)
		{
			if (!_sample || _gsp >= Depth - 1) return;
			_hSlot[_gsp] = slot; _hChild[_gsp] = 0; _hT[_gsp] = Stopwatch.GetTimestamp(); _gsp++;
		}

		public static void HookExit(int slot)
		{
			if (!_sample || _gsp <= 0) return;
			long now = Stopwatch.GetTimestamp();
			int k = _gsp - 1; while (k >= 0 && _hSlot[k] != slot) k--;
			if (k < 0) return;
			_gsp = k;
			long el = now - _hT[k], excl = el - _hChild[k]; if (excl < 0) excl = 0;
			_slotT[slot] += excl; _slotN[slot]++; _globAcc += excl; Timed++;
			if (k > 0) _hChild[k - 1] += el;
		}

		private static readonly int[] _dSlot = new int[Depth]; private static readonly long[] _dT = new long[Depth]; private static int _dsp;

		/// <summary>Map layers, every call: called first and last in a wrapped Draw.</summary>
		public static void DrawEnter(int slot)
		{
			if (_dead || !TimingOn || _dsp >= Depth - 1) return;
			_dSlot[_dsp] = slot; _dT[_dsp] = Stopwatch.GetTimestamp(); _dsp++;
		}

		public static void DrawExit(int slot)
		{
			if (_dsp <= 0) return;
			long now = Stopwatch.GetTimestamp();
			int k = _dsp - 1; while (k >= 0 && _dSlot[k] != slot) k--;
			if (k < 0) return;
			_dsp = k; _slotT[slot] += now - _dT[k]; _slotN[slot]++; Timed++;
		}

		// ------------------------------------------------------------------------------------------ wrapping
		private static readonly MethodInfo HEnterM = typeof(PerfAnalyzer).GetMethod(nameof(HookEnter)), HExitM = typeof(PerfAnalyzer).GetMethod(nameof(HookExit)), DEnterM = typeof(PerfAnalyzer).GetMethod(nameof(DrawEnter)), DExitM = typeof(PerfAnalyzer).GetMethod(nameof(DrawExit));
		private static readonly HashSet<MethodBase> _wrapped = new();
		/// <summary>Methods wrapped, methods that could not be, per kind (the self-check and the log report them).</summary>
		internal static int WrappedGlobals, FailedGlobals, WrappedLayers, FailedLayers;

		private sealed class WrapSpec { public MethodBase M; public MethodInfo Enter, Exit; public int Slot; public bool Global; public IDisposable Hook; }
		private static readonly List<WrapSpec> _wrapSpecs = new();

		private static IDisposable MakeWrapHook(WrapSpec w) => new ILHook(w.M, il =>
		{
			var ins = il.Instrs; if (ins.Count == 0) return;
			for (int i = ins.Count - 1; i >= 0; i--)
			{
				if (ins[i].OpCode != OpCodes.Ret) continue;
				Instruction ret = ins[i]; ret.OpCode = OpCodes.Ldc_I4; ret.Operand = w.Slot; // the ret stays the branch target: it becomes the slot push, then the exit call and a fresh ret
				ins.Insert(i + 1, Instruction.Create(OpCodes.Call, il.Import(w.Exit))); ins.Insert(i + 2, Instruction.Create(OpCodes.Ret));
			}
			ins.Insert(0, Instruction.Create(OpCodes.Ldc_I4, w.Slot)); ins.Insert(1, Instruction.Create(OpCodes.Call, il.Import(w.Enter)));
		});

		/// <summary>Puts a call to <paramref name="enter"/> at the start of the method and one to <paramref name="exit"/> before every return, with the slot as their argument. The edit is an ILHook the analyzer keeps, so it can be taken off again.</summary>
		internal static bool Wrap(MethodBase m, MethodInfo enter, MethodInfo exit, int slot, bool global = false)
		{
			if (!_wrapped.Add(m)) return true;
			try
			{
				var w = new WrapSpec { M = m, Enter = enter, Exit = exit, Slot = slot, Global = global };
				w.Hook = MakeWrapHook(w); _wrapSpecs.Add(w);
				return true;
			}
			catch (Exception) { return false; }
		}

		/// <summary>Self-check only: takes the global AI hook wraps off (false) or puts them back (true), so the cost of wrapping can be measured in one boot.</summary>
		internal static void TestSetGlobalWraps(bool on)
		{
			foreach (var w in _wrapSpecs)
			{
				if (!w.Global) continue;
				if (on && w.Hook == null) w.Hook = MakeWrapHook(w);
				else if (!on && w.Hook != null) { try { w.Hook.Dispose(); } catch (Exception) { } w.Hook = null; }
			}
		}

		internal static void DisposeWraps() { foreach (var w in _wrapSpecs) { try { w.Hook?.Dispose(); } catch (Exception) { } w.Hook = null; } _wrapSpecs.Clear(); _wrapped.Clear(); }

		/// <summary>After content has loaded: wrap map layers and global hooks, and hook the interface layer list.</summary>
		private static void WrapContent()
		{
			bool hooks = true; try { hooks = PerfConfig.Get?.MeasureOtherModHooks ?? true; } catch (Exception) { }
			foreach (ModMapLayer layer in ModContent.GetContent<ModMapLayer>())
			{
				try
				{
					MethodInfo m = layer.GetType().GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(MapOverlayDrawContext).MakeByRefType(), typeof(string).MakeByRefType() }, null);
					if (m == null || m.IsAbstract) continue;
					if (Wrap(m, DEnterM, DExitM, Slot(PerfCat.Map, layer.Mod.Name + "/" + layer.Name))) WrappedLayers++; else FailedLayers++;
				}
				catch (Exception) { FailedLayers++; }
			}
			if (hooks)
			{
				foreach (GlobalNPC g in ModContent.GetContent<GlobalNPC>()) WrapGlobal(g.GetType(), g.Mod.Name, typeof(NPC));
				foreach (GlobalProjectile g in ModContent.GetContent<GlobalProjectile>()) WrapGlobal(g.GetType(), g.Mod.Name, typeof(Projectile));
			}
			try
			{
				MethodInfo sys = typeof(ModSystem).Assembly.GetType("Terraria.ModLoader.SystemLoader")?.GetMethod("ModifyInterfaceLayers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (sys != null) MonoModHooks.Add(sys, new HookModifyLayers(ModifyLayers));
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] interface layers are not timed: " + e.Message); }
			Levy.Instance?.Logger.Info($"[PerfAnalyzer] wrapped {WrappedLayers} map layer draws and {WrappedGlobals} global AI hooks ({FailedLayers + FailedGlobals} could not be wrapped)");
		}

		private static void WrapGlobal(Type t, string mod, Type arg)
		{
			foreach (string name in new[] { "PreAI", "AI", "PostAI" })
			{
				try
				{
					MethodInfo m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { arg }, null);
					if (m == null || m.IsAbstract) continue;
					if (Wrap(m, HEnterM, HExitM, Slot(PerfCat.Glob, mod), true)) WrappedGlobals++; else FailedGlobals++;
				}
				catch (Exception) { FailedGlobals++; }
			}
		}

		// ------------------------------------------------------------------------------------------ interface layers
		private delegate void OrigModifyLayers(List<GameInterfaceLayer> layers);
		private delegate void HookModifyLayers(OrigModifyLayers orig, List<GameInterfaceLayer> layers);
		private static readonly FieldInfo _layerMethod = typeof(LegacyGameInterfaceLayer).GetField("_drawMethod", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly Dictionary<string, LayerHolder> _holders = new();

		private sealed class LayerHolder
		{
			public GameInterfaceDrawMethod Inner; public int Slot; public GameInterfaceDrawMethod Wrapper;
			public bool Draw()
			{
				var inner = Inner; if (inner == null) return true;
				long t0 = Stopwatch.GetTimestamp();
				try { return inner(); }
				finally { if (!_dead) { _slotT[Slot] += Stopwatch.GetTimestamp() - t0; _slotN[Slot]++; } }
			}
		}

		private static void ModifyLayers(OrigModifyLayers orig, List<GameInterfaceLayer> layers)
		{ _hl_ModifyLayers_1++;
			orig(layers);
			if (!TimingOn || _dead) return;
			try { WrapLayers(layers); } catch (Exception e) { Fail("layers", e); }
		} private static int _hl_ModifyLayers_1;

		/// <summary>Swaps each interface layer's draw delegate for a timing one. Public to the self-check: it builds a layer list of its own.</summary>
		internal static int WrapLayers(List<GameInterfaceLayer> layers)
		{
			if (_layerMethod == null) return 0;
			int n = 0;
			foreach (GameInterfaceLayer l in layers)
			{
				if (!(l is LegacyGameInterfaceLayer lg)) continue;
				var cur = _layerMethod.GetValue(lg) as GameInterfaceDrawMethod; if (cur == null || cur.Target is LayerHolder) continue;
				if (!_holders.TryGetValue(lg.Name, out LayerHolder h))
				{
					string name = lg.Name ?? "?"; int c = name.IndexOf(": ", StringComparison.Ordinal);
					string mod = c > 0 ? name.Substring(0, c) : name, rest = c > 0 ? name.Substring(c + 2) : name; if (mod == "Vanilla") mod = "Terraria";
					h = new LayerHolder { Slot = Slot(PerfCat.Ui, mod + ": " + rest) }; h.Wrapper = h.Draw; _holders[lg.Name ?? "?"] = h;
				}
				h.Inner = cur; _layerMethod.SetValue(lg, h.Wrapper); n++;
			}
			return n;
		}
	}
}
