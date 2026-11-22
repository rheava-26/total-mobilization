using System;
using System.Collections.Generic;
using System.Globalization;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Soldiers
{
	public enum SetAbilityKind { Never, Dash, Ram, AtTarget, Fight, FightToggle }

	/// <summary>One row of <c>Common/Soldiers/SetAbilities.tsv</c>: when a levy uses one armor set ability a key starts.</summary>
	public sealed class SetAbilityPolicy
	{
		public string Set = "", Mod = "", Note = "";
		public SetAbilityKind Kind;
		public readonly Dictionary<string, float> P = new(StringComparer.OrdinalIgnoreCase);
		public float Get(string key, float fallback) => P.TryGetValue(key, out float v) ? v : fallback;
		public bool Flag(string key) => P.TryGetValue(key, out float v) && v != 0f;
	}

	/// <summary>
	/// The set-ability policy table (SoldierNPC.SetBonusKey.cs and SoldierNPC.SetDash.cs read it). A set with no row, or a NEVER row, is never used. Loaded in PostSetupContent; a row whose mod is not loaded is kept
	/// (the code that reads it checks the mod itself). Other mods can add or replace rows with <see cref="Register"/>.
	/// </summary>
	public static class SetAbilityPolicies
	{
		private static readonly Dictionary<string, SetAbilityPolicy> Rows = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, SetAbilityKind> Names = new(StringComparer.OrdinalIgnoreCase)
		{
			["NEVER"] = SetAbilityKind.Never, ["DASH"] = SetAbilityKind.Dash, ["RAM"] = SetAbilityKind.Ram, ["AT_TARGET"] = SetAbilityKind.AtTarget, ["FIGHT"] = SetAbilityKind.Fight, ["FIGHT_TOGGLE"] = SetAbilityKind.FightToggle,
		};
		private static readonly SetAbilityPolicy NeverRow = new() { Kind = SetAbilityKind.Never };

		public static int Count => Rows.Count;
		/// <summary>The row for a set id ("godslayer", "forbidden" ...); a NEVER row when it has none.</summary>
		public static SetAbilityPolicy For(string set) => set != null && Rows.TryGetValue(set, out var p) ? p : NeverRow;
		public static bool Uses(string set) => For(set).Kind != SetAbilityKind.Never;

		public static void Register(SetAbilityPolicy p) { if (p != null && !string.IsNullOrEmpty(p.Set)) Rows[p.Set] = p; }

		/// <summary>Parses the table text (header <c>set mod policy params note</c>, tab separated, <c>#</c> comments).</summary>
		public static List<SetAbilityPolicy> Parse(string text)
		{
			var list = new List<SetAbilityPolicy>();
			foreach (string raw in text.Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				if (line.Length == 0 || line[0] == '#') continue;
				string[] c = line.Split('\t');
				if (c.Length < 3 || c[0] == "set") continue;
				if (!Names.TryGetValue(c[2].Trim(), out SetAbilityKind kind)) continue;
				var p = new SetAbilityPolicy { Set = c[0].Trim(), Mod = c[1].Trim(), Kind = kind, Note = c.Length > 4 ? c[4] : "" };
				if (c.Length > 3)
					foreach (string kv in c[3].Split(';', StringSplitOptions.RemoveEmptyEntries))
					{
						int eq = kv.IndexOf('=');
						if (eq <= 0) continue;
						if (float.TryParse(kv.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) p.P[kv.Substring(0, eq).Trim()] = v;
					}
				list.Add(p);
			}
			return list;
		}

		internal static void Load(Mod levy)
		{
			Rows.Clear();
			try
			{
				foreach (var p in Parse(System.Text.Encoding.UTF8.GetString(levy.GetFileBytes("Common/Soldiers/SetAbilities.tsv")))) Rows[p.Set] = p;
				levy.Logger.Info($"Levy set abilities: {Rows.Count} policy rows.");
			}
			catch (Exception e) { levy.Logger.Warn("Levy set abilities: the policy table failed to load, so levies use no set ability: " + e.Message); }
		}

		internal static void Unload() => Rows.Clear();
	}

	public class SetAbilityPoliciesSystem : ModSystem
	{
		public override void PostSetupContent() => global::Levy.Common.Util.LoadSafe.Run(() => SetAbilityPolicies.Load(Mod));
		public override void Unload() => SetAbilityPolicies.Unload();
	}
}
