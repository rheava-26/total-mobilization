using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>How a weapon measured in levy hands, against a stand-in player firing it at the same dummy (docs/ARMYSWEEP_DPS.tsv, docs/ENDGAME_SWEEP.tsv).</summary>
	public enum FitStatus : byte { Unmeasured, Ok, Weak, Broken }

	/// <summary>
	/// The measured levy table (<c>Common/Weapons/LevyFit.tsv</c>, written by <c>tools/gen_levy_fit.py</c> from the two sweeps): per weapon, OK / WEAK / BROKEN, the levy-to-player damage ratio and the levy's own damage per
	/// second on the dummy. <see cref="WeaponRank"/> reads it: BROKEN is never issued (unless the armory's per-item override says so), WEAK loses one progression band, and among working weapons of one band the
	/// measured levy DPS decides (<see cref="OkFactor"/>). Rows whose mod is not loaded are skipped. Weapons nobody measured are <see cref="FitStatus.Unmeasured"/> and keep factor 1.
	/// </summary>
	/// <summary>Loads the measured table once every mod's items exist.</summary>
	public class LevyFitSystem : ModSystem
	{
		public override void PostSetupContent() => LevyFit.Load(Mod);
	}

	public static class LevyFit
	{
		public const string TablePath = "Common/Weapons/LevyFit.tsv";
		/// <summary>The sweeps call a weapon WEAK below this ratio of a player's damage.</summary>
		public const float OkRatio = 0.6f;
		/// <summary>A WEAK weapon's factor never falls below this (before the band it loses).</summary>
		public const float WeakFloor = 0.05f;
		/// <summary>Within a band the measured DPS moves a working weapon's rank by sqrt(dps / band median), held to this range.</summary>
		public const float OkMin = 0.75f, OkMax = 1.33f;
		/// <summary>A band needs this many measured working weapons before their DPS is compared at all.</summary>
		public const int MinBandSample = 3;

		public struct Entry { public FitStatus Status; public float Ratio, Dps; }

		private static Dictionary<int, Entry> _byType = new();
		internal static int TableRows, SkippedRows;
		internal static readonly List<string> Unresolved = new();

		public static int Count => _byType.Count;

		public static Entry Of(int type) => _byType.TryGetValue(type, out var e) ? e : default;
		public static FitStatus StatusOf(int type) => _byType.TryGetValue(type, out var e) ? e.Status : FitStatus.Unmeasured;

		/// <summary>Self-check seam: installs a made-up table (null restores the loaded one).</summary>
		private static Dictionary<int, Entry> _saved;
		internal static void SetForTest(Dictionary<int, Entry> table)
		{
			if (table != null) { _saved ??= _byType; _byType = table; }
			else if (_saved != null) { _byType = _saved; _saved = null; }
			_medianVersion = -1;
		}

		internal static void Load(Mod levy)
		{
			_byType = new(); _saved = null; Unresolved.Clear(); TableRows = SkippedRows = 0; _medianVersion = -1;
			try
			{
				string text = System.Text.Encoding.UTF8.GetString(levy.GetFileBytes(TablePath));
				foreach (string line in text.Split('\n').Skip(1))
				{
					string[] c = line.TrimEnd('\r').Split('\t');
					if (c.Length < 5 || c[0].Length == 0) continue;
					TableRows++;
					int type = Resolve(c[0], c[1]);
					if (type <= 0) { SkippedRows++; if (c[0] == "Terraria" || ModLoader.HasMod(c[0])) Unresolved.Add(c[0] + "/" + c[1]); continue; }
					var st = c[2] switch { "OK" => FitStatus.Ok, "WEAK" => FitStatus.Weak, "BROKEN" => FitStatus.Broken, _ => FitStatus.Unmeasured };
					if (st == FitStatus.Unmeasured) { SkippedRows++; continue; }
					float.TryParse(c[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ratio);
					float.TryParse(c[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dps);
					_byType[type] = new Entry { Status = st, Ratio = ratio, Dps = dps };
				}
				levy.Logger.Info($"Levy measured weapon table: {_byType.Count} weapons resolved of {TableRows} rows ({SkippedRows} skipped: mod not loaded){(Unresolved.Count > 0 ? "; UNRESOLVED " + string.Join(", ", Unresolved) : "")}.");
			}
			catch (Exception e) { _byType = new(); levy.Logger.Warn("Levy measured weapon table failed to load; ranking uses no measured data: " + e.Message); }
		}

		private static int Resolve(string mod, string name)
		{
			if (mod == "Terraria") return ItemID.Search.TryGetId(name, out int id) ? id : 0;
			return ModLoader.HasMod(mod) && ModContent.TryFind(mod, name, out ModItem mi) ? mi.Type : 0;
		}

		// ------------------------------------------------------------------ the factors WeaponRank applies (Balanced mode)

		/// <summary>The factor of a WEAK weapon before its lost band: its ratio over the OK line (0.45 -> 0.75), never below <see cref="WeakFloor"/>.</summary>
		public static float WeakFactor(float ratio) => Math.Clamp(ratio / OkRatio, WeakFloor, 1f);

		private static int _medianVersion = -1;
		private static readonly Dictionary<int, float> _bandMedian = new();

		/// <summary>The factor of a working (OK) weapon in its band: sqrt(levy DPS / median levy DPS of the measured working weapons of that band), within [<see cref="OkMin"/>, <see cref="OkMax"/>]; 1 when the band has too few.</summary>
		public static float OkFactor(int type, float dps, float tier)
		{
			if (tier < 0f || dps <= 0f) return 1f;
			if (_medianVersion != ProgressionTier.Version) RebuildMedians();
			if (!_bandMedian.TryGetValue(WeaponRank.Band(tier), out float med) || med <= 0f) return 1f;
			return Math.Clamp((float)Math.Sqrt(dps / med), OkMin, OkMax);
		}

		private static void RebuildMedians()
		{
			_bandMedian.Clear();
			_medianVersion = ProgressionTier.Version;
			var byBand = new Dictionary<int, List<float>>();
			foreach (var kv in _byType)
			{
				if (kv.Value.Status != FitStatus.Ok || kv.Value.Dps <= 0f) continue;
				float t = ProgressionTier.Of(kv.Key); if (t < 0f) continue;
				int b = WeaponRank.Band(t);
				if (!byBand.TryGetValue(b, out var l)) byBand[b] = l = new();
				l.Add(kv.Value.Dps);
			}
			foreach (var kv in byBand)
			{
				if (kv.Value.Count < MinBandSample) continue;
				kv.Value.Sort();
				int n = kv.Value.Count;
				_bandMedian[kv.Key] = n % 2 == 1 ? kv.Value[n / 2] : 0.5f * (kv.Value[n / 2 - 1] + kv.Value[n / 2]);
			}
		}
	}
}
