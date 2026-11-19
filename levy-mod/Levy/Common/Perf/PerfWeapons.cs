using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Perf
{
	/// <summary>
	/// PER-WEAPON COST: on the sampled updates (one in eleven) every levy's own update and every update of its shots is charged to the weapon the levy holds at that moment (its sidearm when it fights with the sidearm),
	/// together with the owner swap around each shot's update (<see cref="Weapons.OwnerPuppet"/>, which wraps the timed body from outside). Per weapon the window keeps: levies holding it, ms per update of their bodies (AI and gear frame,
	/// other mods' hooks on them included), ms per update of their shots (AI, the hit pass and its swaps, other mods' hooks included), shots alive, and the projectile type that costs it the most.
	/// Cost: a type test and an array read per levy and a table lookup per army shot on the sampled updates only; the self-check measures it call by call (<see cref="MicroBench"/>).
	/// </summary>
	public partial class PerfAnalyzer
	{
		/// <summary>Lab A/B: LEVY_PERFWPN=0 leaves the per-weapon attribution out.</summary>
		internal static bool WeaponsOn = Environment.GetEnvironmentVariable("LEVY_PERFWPN") != "0";
		private const int MaxW = 256, MaxPairs = 1024;
		// per weapon slot, this window (slot 0: a shot whose levy was not seen this update)
		private static readonly long[] _wBody = new long[MaxW], _wShot = new long[MaxW], _wSwap = new long[MaxW];
		private static readonly int[] _wLevyN = new int[MaxW], _wShotN = new int[MaxW], _wKeyOf = new int[MaxW];
		private static int _wCount = 1; private static int[] _wSlotByKey = new int[0];
		// per levy (NPC slot): the weapon slot it held on the update it was last timed, and that update
		private static readonly int[] _lvW = new int[Main.maxNPCs + 1]; private static readonly long[] _lvAt = new long[Main.maxNPCs + 1];
		// (weapon slot, projectile type) pairs
		private static readonly Dictionary<int, int> _pairOf = new(); private static readonly long[] _pT = new long[MaxPairs]; private static readonly int[] _pN = new int[MaxPairs], _pW = new int[MaxPairs], _pType = new int[MaxPairs];
		private static int _pCount;
		// the reference the attribution is checked against: levy bodies and army shots with other mods' hooks taken out (the analyzer's own rows), and every global hook's time
		private static long _levyExcl, _shotExcl, _levyIncl, _shotIncl, _swapT;
		private static int _eSoldier = -1;

		/// <summary>The weapon slot of a levy's held weapon or sidearm (item type x 2, +1 for the sidearm), made on first use.</summary>
		private static int WeaponSlot(SoldierNPC s)
		{
			var g = s.Record?.Gear; if (g == null) return 0;
			bool side = s.UsingSidearm && g.HasSidearm; Item it = side ? g.Sidearm : g.HasWeapon ? g.Weapon : null;
			int key = it == null ? 1 : it.type * 2 + (side ? 1 : 0) + 2; // 1: no weapon
			var a = _wSlotByKey;
			if (key >= a.Length) { var b = new int[Math.Max(key + 64, (ItemLoader.ItemCount + 8) * 2 + 4)]; Array.Copy(a, b, a.Length); _wSlotByKey = a = b; }
			int v = a[key]; if (v != 0) return v;
			if (_wCount >= MaxW) return 0;
			v = _wCount++; _wKeyOf[v] = key; a[key] = v; return v;
		}

		/// <summary>A levy's body update was timed: inclusive (other mods' hooks on it too) and exclusive (the analyzer's own NPC row).</summary>
		private static void NoteLevy(NPC n, SoldierNPC s, long incl, long excl)
		{
			int w = WeaponSlot(s); int i = n.whoAmI;
			_lvW[i] = w; _lvAt[i] = _tick;
			_wBody[w] += incl; _wLevyN[w]++; _levyIncl += incl; _levyExcl += excl;
		}

		private static int LevyWeapon(int soldier) => soldier >= 0 && soldier < Main.maxNPCs && _lvAt[soldier] == _tick ? _lvW[soldier] : 0;

		/// <summary>An army shot's update was timed.</summary>
		private static void NoteShot(int soldier, int type, long incl, long excl)
		{
			_shotIncl += incl; _shotExcl += excl;
			if (soldier < 0) return; // an officer's or a summoned unit's shot: not a levy's
			int w = LevyWeapon(soldier);
			_wShot[w] += incl; _wShotN[w]++;
			int key = (w << 20) | (type & 0xFFFFF);
			if (!_pairOf.TryGetValue(key, out int k)) { if (_pCount >= MaxPairs) return; k = _pCount++; _pairOf[key] = k; _pW[k] = w; _pType[k] = type; }
			_pT[k] += incl; _pN[k]++;
		}

		/// <summary>True while army shots are being timed (OwnerPuppet times its swap around the update then).</summary>
		internal static bool SampleArmy => _sample && WeaponsOn && !_dead;

		/// <summary>The owner swap around one soldier shot's update (entry and exit), timed by OwnerPuppet on sampled updates.</summary>
		internal static void NoteSwap(int soldier, long ticks)
		{
			if (!SampleArmy || ticks <= 0) return;
			_swapT += ticks; _wSwap[LevyWeapon(soldier)] += ticks;
		}

		private static void ResetWeapons()
		{
			Array.Clear(_wBody, 0, _wCount); Array.Clear(_wShot, 0, _wCount); Array.Clear(_wSwap, 0, _wCount); Array.Clear(_wLevyN, 0, _wCount); Array.Clear(_wShotN, 0, _wCount);
			Array.Clear(_pT, 0, _pCount); Array.Clear(_pN, 0, _pCount);
			_levyExcl = _shotExcl = _levyIncl = _shotIncl = _swapT = 0;
		}

		internal static string WeaponName(int slot)
		{
			if (slot <= 0) return "(levy gone)";
			int key = _wKeyOf[slot]; if (key == 1) return "(no weapon)";
			int type = (key - 2) / 2; bool side = ((key - 2) & 1) == 1;
			string n; try { n = Lang.GetItemNameValue(type); } catch (Exception) { n = null; }
			if (string.IsNullOrEmpty(n)) n = "item " + type;
			return side ? n + " (sidearm)" : n;
		}

		/// <summary>The window's weapon rows, ms per update (sampled updates scaled to all), biggest first, and the army totals they are checked against.</summary>
		private static void BuildWeapons(PerfWindow w)
		{
			if (!WeaponsOn || _sUpdates <= 0) return;
			double su = _sUpdates;
			var rows = new List<WeaponRow>();
			for (int i = 0; i < _wCount; i++)
			{
				long tot = _wBody[i] + _wShot[i] + _wSwap[i]; if (tot <= 0 && _wLevyN[i] == 0) continue;
				var r = new WeaponRow { Name = WeaponName(i), Levies = _wLevyN[i] / su, BodyMs = Ms(_wBody[i]) / su, ShotMs = Ms(_wShot[i] + _wSwap[i]) / su, Shots = _wShotN[i] / su };
				int best = -1; for (int k = 0; k < _pCount; k++) if (_pW[k] == i && _pT[k] > 0 && (best < 0 || _pT[k] > _pT[best])) best = k;
				if (best >= 0) { r.TopProj = ProjTypeName(_pType[best]); r.TopProjMs = Ms(_pT[best]) / su; }
				rows.Add(r);
			}
			rows.Sort((a, b) => b.Ms.CompareTo(a.Ms));
			double sum = 0; foreach (var r in rows) sum += r.Ms;
			w.WeaponAttribMs = sum;
			if (rows.Count > PerfWindow.MaxWeaponRows) rows.RemoveRange(PerfWindow.MaxWeaponRows, rows.Count - PerfWindow.MaxWeaponRows);
			w.Weapons = rows;
			w.ArmyRefMs = new[] { Ms(_levyExcl) / su, Ms(_shotExcl) / su, Ms(_levyIncl - _levyExcl + _shotIncl - _shotExcl) / su, Ms(_swapT) / su };
		}

		// ------------------------------------------------------------------------------------------ self-check
		/// <summary>Self-check: milliseconds one window's weapon rows take to build from the accumulators as they stand (done once a window).</summary>
		internal static double TestBuildWeaponsMs()
		{
			var w = new PerfWindow(); long t0 = Stopwatch.GetTimestamp(); BuildWeapons(w); return (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
		}
		/// <summary>Nanoseconds per sampled enter+exit pair of a levy body and of one of its shots, with the attribution on and off, and per timed swap note. Self-check only; leaves the window empty.</summary>
		internal static double[] MicroBenchWeapons(NPC levy, Projectile shot)
		{
			bool was = _sample, wasOn = WeaponsOn; var r = new double[6];
			double Per(int count, Action a) { a(); long t0 = Stopwatch.GetTimestamp(); a(); return (Stopwatch.GetTimestamp() - t0) * 1e9 / Stopwatch.Frequency / count; }
			const int N = 60000;
			try
			{
				_sample = true;
				WeaponsOn = false;
				r[0] = Per(N, () => { for (int i = 0; i < N; i++) { NpcEnter(levy); NpcExit(levy); } });
				r[1] = Per(N, () => { for (int i = 0; i < N; i++) { ProjEnter(shot); ProjExit(shot); } });
				WeaponsOn = true;
				r[2] = Per(N, () => { for (int i = 0; i < N; i++) { NpcEnter(levy); NpcExit(levy); } });
				r[3] = Per(N, () => { for (int i = 0; i < N; i++) { ProjEnter(shot); ProjExit(shot); } });
				r[4] = Per(N, () => { for (int i = 0; i < N; i++) { long t0 = Stopwatch.GetTimestamp(); NoteSwap(levy.whoAmI, Stopwatch.GetTimestamp() - t0 + 1); } });
				r[5] = Per(N, () => { long x = 0; for (int i = 0; i < N; i++) x += Stopwatch.GetTimestamp(); if (x == 1) _wCount += 0; });
			}
			finally { _sample = was; WeaponsOn = wasOn; ResetWindow(Stopwatch.GetTimestamp()); }
			return r;
		}
	}
}
