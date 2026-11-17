using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Perf
{
	/// <summary>
	/// The per-entity timing: how long each NPC and each projectile takes to update, and to draw. The game calls NPC.UpdateNPC and Projectile.Update for every slot, active or not (200 and 1000 calls a tick), so a detour (a delegate call
	/// through MonoMod's trampoline) costs about as much as the work it measures. Instead the method's own IL is edited: a call to <see cref="NpcEnter"/> at the start and to <see cref="NpcExit"/> before each return, a static call and a flag test when
	/// the tick is not sampled. The edits are made with an ILHook the analyzer keeps, and removed when the analyzer is switched off.
	/// <para/>The Enter and Exit methods never throw: an error inside them turns the analyzer off (<see cref="Fail"/>) and they return at once from then on.
	/// </summary>
	public partial class PerfAnalyzer
	{
		private static bool _army; // inside the update of a levy or an army shot while effects are reduced: dust made now is thinned
		private static int _dustN;
		private static long _eT0, _eG0, _dT0; // the update (and the draw) in flight: its start and the global hook time already counted

		/// <summary>True for a projectile fired by a levy, an officer or another army body (the same test the army's own profiler uses).</summary>
		private static bool IsArmyShot(Projectile p) => p.TryGetGlobalProjectile(out SoldierProjectileTag t) && (t.SoldierIndex >= 0 || t.CommanderIndex >= 0);

		public static void NpcEnter(NPC n)
		{
			if (_dead) { _eT0 = 0; return; }
			try
			{
				if (ThinCtx) _army = n.ModNPC is SoldierNPC;
				if (_sample && n.active) { _eG0 = _globAcc; _gsp = 0; _eT0 = Stopwatch.GetTimestamp(); } else _eT0 = 0;
			}
			catch (Exception e) { Fail("npc enter", e); }
		}

		public static void NpcExit(NPC n)
		{
			try
			{
				_army = false; long t0 = _eT0; if (t0 == 0) return; _eT0 = 0;
				long dt = Stopwatch.GetTimestamp() - t0 - (_globAcc - _eG0); if (dt < 0) dt = 0;
				int type = n.type, o = NpcOwner(type);
				_npcT[o] += dt; _npcN[o]++; NpcCalls++;
				var tt = _typeNpcT; if (type < tt.Length) { tt[type] += dt; _typeNpcN[type]++; }
			}
			catch (Exception e) { Fail("npc exit", e); }
		}

		private static bool _eArmy;

		public static void ProjEnter(Projectile p)
		{
			if (_dead) { _eT0 = 0; return; }
			try
			{
				bool timed = _sample && p.active;
				bool thin = ThinCtx;
				if (thin || timed) { _eArmy = IsArmyShot(p); if (thin) _army = _eArmy; }
				if (timed) { _eG0 = _globAcc; _gsp = 0; _eT0 = Stopwatch.GetTimestamp(); } else _eT0 = 0;
			}
			catch (Exception e) { Fail("proj enter", e); }
		}

		public static void ProjExit(Projectile p)
		{
			try
			{
				_army = false; long t0 = _eT0; if (t0 == 0) return; _eT0 = 0;
				long dt = Stopwatch.GetTimestamp() - t0 - (_globAcc - _eG0); if (dt < 0) dt = 0;
				int type = p.type; ProjCalls++;
				if (_eArmy) { _armyT += dt; _armyN++; }
				else { int o = ProjOwner(type); _projT[o] += dt; _projN[o]++; }
				var tt = _typeProjT; if (type < tt.Length) { tt[type] += dt; _typeProjN[type]++; }
			}
			catch (Exception e) { Fail("proj exit", e); }
		}

		public static void DrawNpcEnter(int i) { _dT0 = _dsample && !_dead ? Stopwatch.GetTimestamp() : 0; }
		public static void DrawNpcExit(int i)
		{
			long t0 = _dT0; if (t0 == 0) return; _dT0 = 0;
			try { long dt = Stopwatch.GetTimestamp() - t0; int o = NpcOwner(Main.npc[i].type); _dnpcT[o] += dt; _dnpcN[o]++; }
			catch (Exception e) { Fail("draw npc", e); }
		}

		public static void DrawProjEnter(int i) { _dT0 = _dsample && !_dead ? Stopwatch.GetTimestamp() : 0; }
		public static void DrawProjExit(int i)
		{
			long t0 = _dT0; if (t0 == 0) return; _dT0 = 0;
			try
			{
				long dt = Stopwatch.GetTimestamp() - t0; Projectile p = Main.projectile[i];
				if (IsArmyShot(p)) { _darmyT += dt; _darmyN++; } else { int o = ProjOwner(p.type); _dprojT[o] += dt; _dprojN[o]++; }
			}
			catch (Exception e) { Fail("draw proj", e); }
		}

		// ------------------------------------------------------------------------------------------ the IL edits
		private static readonly List<IDisposable> _ilCtx = new(), _ilDraw = new();
		/// <summary>Entity hooks that could not be attached (the others work without them).</summary>
		internal static int EntityHooksFailed;

		private static MethodInfo Own(string name) => typeof(PerfAnalyzer).GetMethod(name, BindingFlags.Public | BindingFlags.Static);

		/// <summary>Edits <paramref name="m"/> so it calls <paramref name="enter"/> first and <paramref name="exit"/> before every return, each with the method's argument number <paramref name="arg"/> (0 = this, 1 = the first parameter).</summary>
		private static IDisposable HookIL(MethodBase m, MethodInfo enter, MethodInfo exit, int arg)
		{
			OpCode ld = arg == 0 ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1;
			return new ILHook(m, il =>
			{
				var ins = il.Instrs; if (ins.Count == 0) return;
				for (int i = ins.Count - 1; i >= 0; i--)
				{
					if (ins[i].OpCode != OpCodes.Ret) continue;
					Instruction ret = ins[i]; ret.OpCode = ld; ret.Operand = null; // the ret stays the branch target: it becomes the argument push, then the exit call and a fresh ret
					ins.Insert(i + 1, Instruction.Create(OpCodes.Call, il.Import(exit))); ins.Insert(i + 2, Instruction.Create(OpCodes.Ret));
				}
				ins.Insert(0, Instruction.Create(ld)); ins.Insert(1, Instruction.Create(OpCodes.Call, il.Import(enter)));
			});
		}

		private static void TryHook(List<IDisposable> into, MethodBase m, string enter, string exit, int arg)
		{
			try { if (m == null) { EntityHooksFailed++; return; } into.Add(HookIL(m, Own(enter), Own(exit), arg)); }
			catch (Exception e) { EntityHooksFailed++; try { Levy.Instance?.Logger.Warn("[PerfAnalyzer] could not time " + m.Name + ": " + e.Message); } catch (Exception) { } }
		}

		private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private static void AttachEntityUpdates()
		{
			TryHook(_ilCtx, typeof(NPC).GetMethod("UpdateNPC", Any, null, new[] { typeof(int) }, null), nameof(NpcEnter), nameof(NpcExit), 0);
			TryHook(_ilCtx, typeof(Projectile).GetMethod("Update", Any, null, new[] { typeof(int) }, null), nameof(ProjEnter), nameof(ProjExit), 0);
		}

		private static void AttachEntityDraws()
		{
			TryHook(_ilDraw, typeof(Main).GetMethod("DrawNPC", Any, null, new[] { typeof(int), typeof(bool) }, null), nameof(DrawNpcEnter), nameof(DrawNpcExit), 1);
			TryHook(_ilDraw, typeof(Main).GetMethod("DrawProj", Any, null, new[] { typeof(int) }, null), nameof(DrawProjEnter), nameof(DrawProjExit), 1);
		}

		private static void DetachList(List<IDisposable> l)
		{
			for (int i = l.Count - 1; i >= 0; i--) { try { l[i].Dispose(); } catch (Exception) { } }
			l.Clear();
		}

		// ------------------------------------------------------------------------------------------ self-check: the analyzer's own code, timed call by call
		/// <summary>Nanoseconds per call pair (enter and exit) of the analyzer's own hooks on an NPC, a projectile and a wrapped global hook, with the tick not sampled and sampled, and one clock read. Self-check only; leaves the window empty.</summary>
		internal static double[] MicroBench(NPC n, Projectile p)
		{
			bool was = _sample; var r = new double[7];
			int slot = Slot(PerfCat.Glob, "PerfMicro");
			double Per(int count, Action a) { a(); long t0 = Stopwatch.GetTimestamp(); a(); return (Stopwatch.GetTimestamp() - t0) * 1e9 / Stopwatch.Frequency / count; }
			const int N = 300000;
			try
			{
				_sample = false;
				r[0] = Per(N, () => { for (int i = 0; i < N; i++) { NpcEnter(n); NpcExit(n); } });
				r[1] = Per(N, () => { for (int i = 0; i < N; i++) { ProjEnter(p); ProjExit(p); } });
				r[2] = Per(N, () => { for (int i = 0; i < N; i++) { HookEnter(slot); HookExit(slot); } });
				_sample = true;
				r[3] = Per(N / 4, () => { for (int i = 0; i < N / 4; i++) { NpcEnter(n); NpcExit(n); } });
				r[4] = Per(N / 4, () => { for (int i = 0; i < N / 4; i++) { ProjEnter(p); ProjExit(p); } });
				r[5] = Per(N / 4, () => { for (int i = 0; i < N / 4; i++) { HookEnter(slot); HookExit(slot); } });
				r[6] = Per(N, () => { long x = 0; for (int i = 0; i < N; i++) x += Stopwatch.GetTimestamp(); if (x == 1) _dustN++; });
			}
			finally { _sample = was; ResetWindow(Stopwatch.GetTimestamp()); }
			return r;
		}
	}
}
