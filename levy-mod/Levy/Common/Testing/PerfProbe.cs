using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// A call profiler for the lab and the PerfBoss world check: inclusive and exclusive wall time and call counts per method, with no source edits. <see cref="Install"/> rewrites the methods of this mod that
	/// tModLoader calls (every override of a ModNPC / ModProjectile / ModPlayer / ModSystem / Global* hook, <see cref="HookMethods"/>), and any further methods named by type (<see cref="Deep"/>), so each one reports to
	/// <see cref="Enter"/> / <see cref="Exit"/>. Inert unless something calls <see cref="Install"/> (the check does, only when it runs) and <see cref="On"/> is set; the main thread only. Exclusive time is the call's
	/// own time minus the time of the instrumented calls under it, so the exclusive column sums to the instrumented total without double counting.
	/// </summary>
	public static class PerfProbe
	{
		public static volatile bool On;
		private static int _mainThread = -1;
		private const int Max = 8192, Depth = 512;
		private static readonly string[] Names = new string[Max];
		private static readonly long[] Calls = new long[Max], Incl = new long[Max], Excl = new long[Max];
		private static readonly int[] StkId = new int[Depth];
		private static readonly long[] StkT = new long[Depth], StkChild = new long[Depth];
		private static int _sp, _n;
		private static readonly Dictionary<string, int> ByName = new();

		public static int Count => _n;
		public static void SetMainThread() { _mainThread = Environment.CurrentManagedThreadId; }

		/// <summary>The id of a named bucket (made on first use). Also how the check times its own sections.</summary>
		public static int Id(string name)
		{
			lock (ByName)
			{
				if (ByName.TryGetValue(name, out int id)) return id;
				if (_n >= Max) return Max - 1;
				id = _n++; Names[id] = name; ByName[name] = id; return id;
			}
		}

		public static void Enter(int id)
		{
			if (!On || Environment.CurrentManagedThreadId != _mainThread || _sp >= Depth - 1) return;
			StkId[_sp] = id; StkChild[_sp] = 0; StkT[_sp] = Stopwatch.GetTimestamp(); _sp++;
		}

		public static void Exit(int id)
		{
			if (!On || Environment.CurrentManagedThreadId != _mainThread) return;
			long now = Stopwatch.GetTimestamp();
			int k = _sp - 1; while (k >= 0 && StkId[k] != id) k--;
			if (k < 0) return; // began while off, or already unwound by an exception above it
			_sp = k;
			long el = now - StkT[k];
			Calls[id]++; Incl[id] += el; Excl[id] += el - StkChild[k];
			if (k > 0) StkChild[k - 1] += el;
		}

		public static void Reset() { Array.Clear(Calls, 0, Max); Array.Clear(Incl, 0, Max); Array.Clear(Excl, 0, Max); _sp = 0; }

		public readonly struct Row { public readonly string Name; public readonly double ExclMs, InclMs, CallsPerTick; public Row(string n, double e, double i, double c) { Name = n; ExclMs = e; InclMs = i; CallsPerTick = c; } }

		/// <summary>Per-tick rows (ms and calls per tick over <paramref name="ticks"/>), largest exclusive time first.</summary>
		public static List<Row> Rows(int ticks)
		{
			double f = 1000.0 / Stopwatch.Frequency / Math.Max(1, ticks); var l = new List<Row>();
			for (int i = 0; i < _n; i++) if (Calls[i] > 0) l.Add(new Row(Names[i], Excl[i] * f, Incl[i] * f, Calls[i] / (double)Math.Max(1, ticks)));
			l.Sort((a, b) => b.ExclMs.CompareTo(a.ExclMs));
			return l;
		}

		// ------------------------------------------------------------------------------------------ installing
		private static readonly MethodInfo EnterM = typeof(PerfProbe).GetMethod(nameof(Enter)), ExitM = typeof(PerfProbe).GetMethod(nameof(Exit));
		public static int Wrapped, WrapFailed;
		private static readonly HashSet<MethodBase> Done = new();
		private static readonly HashSet<string> SkipNames = new() { "AppliesToEntity", "Load", "Unload", "SetStaticDefaults", "SetDefaults", "IsLoadingEnabled", "PostSetupContent", "AddRecipes", "PostAddRecipes", "Clone", "NewInstance", "InstancePerEntity", "CloneNewInstances" };
		private static readonly Type[] HookBases = { typeof(ModNPC), typeof(ModProjectile), typeof(ModItem), typeof(ModPlayer), typeof(ModSystem), typeof(GlobalNPC), typeof(GlobalProjectile), typeof(GlobalItem), typeof(ModBuff), typeof(GlobalBuff), typeof(ModTile), typeof(GlobalTile), typeof(ModTileEntity) };

		private static bool IsHookOverride(MethodInfo m)
		{
			if (m.IsStatic || m.IsAbstract || m.IsGenericMethodDefinition || m.IsSpecialName) return false;
			MethodInfo b; try { b = m.GetBaseDefinition(); } catch (Exception) { return false; }
			if (b == m || b.DeclaringType == null) return false;
			return Array.IndexOf(HookBases, b.DeclaringType) >= 0 || b.DeclaringType.IsGenericType && HookBases.Any(h => h.Name == b.DeclaringType.Name);
		}

		/// <summary>Instruments every hook override of this mod's assembly outside the Testing namespace, plus the methods of the types named in <paramref name="deepTypes"/> (simple type names, a trailing * matches a prefix).</summary>
		public static void Install(Mod mod, IEnumerable<string> deepTypes)
		{
			SetMainThread();
			var deep = (deepTypes ?? Array.Empty<string>()).Where(s => s.Length > 0).ToList();
			Type[] all; try { all = mod.Code.GetTypes(); } catch (ReflectionTypeLoadException e) { all = e.Types.Where(x => x != null).ToArray(); }
			foreach (Type t in all)
			{ try {
				if (t.Namespace != null && t.Namespace.Contains("Testing")) continue;
				if (t.IsGenericTypeDefinition || t.Name.Contains('<')) continue;
				bool deepType = deep.Any(d => d.EndsWith("*") ? t.Name.StartsWith(d.TrimEnd('*')) : t.Name == d);
				if (t.Namespace != null && t.Namespace.Contains("Thorium")) continue;
				MethodInfo[] ms; try { ms = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); } catch (Exception) { continue; }
				foreach (MethodInfo m in ms)
				{ try {
					try { if (m.IsAbstract || m.IsGenericMethodDefinition || m.Name.Contains('<') || SkipNames.Contains(m.Name)) continue; } catch (Exception) { continue; }
					bool hook; try { hook = IsHookOverride(m); } catch (Exception) { continue; }
					if (!hook && !(deepType && !m.IsSpecialName)) continue;
					Wrap(m, (hook ? "" : "~") + t.Name + "." + m.Name + (ms.Count(x => x.Name == m.Name) > 1 ? "/" + m.GetParameters().Length : ""));
				} catch (Exception) { WrapFailed++; }
				}
			} catch (Exception) { WrapFailed++; } }
		}

		/// <summary>Instruments one method under <paramref name="label"/>.</summary>
		public static void Wrap(MethodBase m, string label)
		{
			if (!Done.Add(m)) return;
			int id = Id(label);
			try
			{
				MonoModHooks.Modify(m, il =>
				{
					var ins = il.Instrs; if (ins.Count == 0) return;
					for (int i = ins.Count - 1; i >= 0; i--)
					{
						if (ins[i].OpCode != OpCodes.Ret) continue;
						// the ret instruction object stays the branch target: it becomes the id push, followed by the exit call and a fresh ret
						Instruction ret = ins[i]; ret.OpCode = OpCodes.Ldc_I4; ret.Operand = id;
						ins.Insert(i + 1, Instruction.Create(OpCodes.Call, il.Import(ExitM))); ins.Insert(i + 2, Instruction.Create(OpCodes.Ret));
					}
					Instruction first = ins[0];
					ins.Insert(0, Instruction.Create(OpCodes.Ldc_I4, id)); ins.Insert(1, Instruction.Create(OpCodes.Call, il.Import(EnterM)));
					// a branch (or a try start) aimed at the old first instruction keeps pointing at it: nothing re-enters the prologue
					_ = first;
				});
				Wrapped++;
			}
			catch (Exception) { WrapFailed++; }
		}

		/// <summary>Vanilla-side phase timers (NPC and projectile updates by kind, the projectile hit loop), as buckets in the same table. Install once.</summary>
		private static bool _phases;
		private static int _npcLevy, _npcBoss, _npcOther, _projHostile, _projArmy, _projOther, _dmgHostile, _dmgFriendly;
		public static void InstallPhases()
		{
			if (_phases) return; _phases = true;
			_npcLevy = Id("V:NPC.Update levy"); _npcBoss = Id("V:NPC.Update boss"); _npcOther = Id("V:NPC.Update other");
			_projHostile = Id("V:Projectile.Update hostile"); _projArmy = Id("V:Projectile.Update army/friendly"); _projOther = Id("V:Projectile.Update other");
			_dmgHostile = Id("V:Projectile.Damage hostile"); _dmgFriendly = Id("V:Projectile.Damage friendly");
			On_NPC.UpdateNPC += (orig, self, i) =>
			{
				if (!On) { orig(self, i); return; }
				int k = self.ModNPC is Content.NPCs.SoldierNPC ? _npcLevy : self.boss || self.type == Terraria.ID.NPCID.Plantera ? _npcBoss : _npcOther;
				Enter(k); try { orig(self, i); } finally { Exit(k); }
			};
			On_Projectile.Update += (orig, self, i) =>
			{
				if (!On) { orig(self, i); return; }
				int k = self.hostile ? _projHostile : self.friendly ? _projArmy : _projOther;
				Enter(k); try { orig(self, i); } finally { Exit(k); }
			};
			On_Projectile.Damage += (orig, self) =>
			{
				if (!On) { orig(self); return; }
				int k = self.hostile ? _dmgHostile : _dmgFriendly;
				Enter(k); try { orig(self); } finally { Exit(k); }
			};
		}
	}
}
