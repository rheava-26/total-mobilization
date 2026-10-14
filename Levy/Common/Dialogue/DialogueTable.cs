using System;
using System.Collections.Generic;
using System.Text;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Dialogue
{
	/// <summary>Who may speak a line. Any = everyone; the four temperaments; Veteran = only soldiers who earned it.</summary>
	public enum LineFilter : byte { Any = 0, Greenhorn = 1, Sellsword = 2, Zealot = 3, Cynic = 4, Veteran = 5 }

	/// <summary>Context placeholders a line needs before it can be spoken. {Player} is always satisfiable and not listed.</summary>
	[Flags]
	public enum LineNeeds : byte { None = 0, Name = 1, Comrade = 2, Boss = 4, Killer = 8, Town = 16, Wage = 32 }

	public enum PlaceholderKind : byte { Literal = 0, Player, Name, Comrade, Boss, Killer, Town, Wage, TownNpc }

	/// <summary>One literal run or one placeholder of a parsed line.</summary>
	public struct LinePart
	{
		public string Literal;
		public PlaceholderKind Kind;
		/// <summary>NPCID for Kind == TownNpc.</summary>
		public int NpcType;
	}

	/// <summary>One parsed entry of Dialogue/&lt;culture&gt;.txt.</summary>
	public sealed class DialogueLine
	{
		/// <summary>Repeat-cooldown id. Lines with identical text share an id, so they never both fire inside the cooldown.</summary>
		public int Id;
		public LineFilter Filter;
		public string Text;
		/// <summary>The first town-NPC placeholder of the line, if any.</summary>
		public int? NamedropNpcType;
		/// <summary>Every town NPC the line names. All of them must be alive in the world.</summary>
		public int[] NpcTypes = Array.Empty<int>();
		public LineNeeds Needs;
		public LinePart[] Parts = Array.Empty<LinePart>();
		/// <summary>1-based line number in the source file.</summary>
		public int SourceLine;
	}

	/// <summary>The loaded dialogue table: key (trigger name, or BOSS_&lt;npc name&gt;) to its lines.</summary>
	public static class DialogueTable
	{
		public const string BossPrefix = "BOSS_";

		/// <summary>Keys are case-insensitive: "EyeofCthulhu" and "EyeOfCthulhu" are the same boss.</summary>
		public static readonly Dictionary<string, List<DialogueLine>> Lines = new(StringComparer.OrdinalIgnoreCase);
		/// <summary>Culture whose file was loaded last ("" when nothing is loaded).</summary>
		public static string LoadedCulture = "";
		public static int LineCount;

		/// <summary>Trigger names indexed by enum value.</summary>
		public static readonly string[] TriggerNames = BuildTriggerNames();

		private static readonly Dictionary<string, int> TownNpcByName = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "Guide", NPCID.Guide }, { "Nurse", NPCID.Nurse }, { "ArmsDealer", NPCID.ArmsDealer }, { "Demolitionist", NPCID.Demolitionist },
			{ "Merchant", NPCID.Merchant }, { "Dryad", NPCID.Dryad }, { "Clothier", NPCID.Clothier }, { "PartyGirl", NPCID.PartyGirl },
			{ "Tavernkeep", NPCID.DD2Bartender }, { "TaxCollector", NPCID.TaxCollector }, { "Mechanic", NPCID.Mechanic },
			{ "GoblinTinkerer", NPCID.GoblinTinkerer }, { "Wizard", NPCID.Wizard }, { "Steampunker", NPCID.Steampunker },
			{ "Cyborg", NPCID.Cyborg }, { "Pirate", NPCID.Pirate }, { "Truffle", NPCID.Truffle }, { "Stylist", NPCID.Stylist },
			{ "Angler", NPCID.Angler }, { "DyeTrader", NPCID.DyeTrader }, { "Painter", NPCID.Painter }, { "WitchDoctor", NPCID.WitchDoctor },
			{ "Golfer", NPCID.Golfer }, { "Zoologist", NPCID.BestiaryGirl }, { "Princess", NPCID.Princess }, { "SantaClaus", NPCID.SantaClaus },
		};

		private static string[] BuildTriggerNames()
		{
			var values = (DialogueTrigger[])Enum.GetValues(typeof(DialogueTrigger));
			int max = 0;
			foreach (var v in values) max = Math.Max(max, (int)v);
			var names = new string[max + 1];
			foreach (var v in values) names[(int)v] = v.ToString();
			return names;
		}

		public static void Clear()
		{
			Lines.Clear();
			LoadedCulture = "";
			LineCount = 0;
		}

		/// <summary>Lines for a key (trigger name or BOSS_x), or null when there are none.</summary>
		public static List<DialogueLine> Get(string key)
		{
			return key != null && Lines.TryGetValue(key, out var list) && list.Count > 0 ? list : null;
		}

		public static List<DialogueLine> Get(DialogueTrigger trigger)
		{
			int i = (int)trigger;
			return i >= 0 && i < TriggerNames.Length ? Get(TriggerNames[i]) : null;
		}

		/// <summary>
		/// Reads Dialogue/&lt;culture&gt;.txt from the mod file, falling back to Dialogue/en-US.txt. Never throws; problems are logged.
		/// </summary>
		public static void Load(Mod mod, string cultureName)
		{
			Clear();
			if (mod == null) return;
			string path = null;
			string culture = null;
			try
			{
				if (!string.IsNullOrEmpty(cultureName) && mod.FileExists("Dialogue/" + cultureName + ".txt")) { path = "Dialogue/" + cultureName + ".txt"; culture = cultureName; }
				else if (mod.FileExists("Dialogue/en-US.txt")) { path = "Dialogue/en-US.txt"; culture = "en-US"; }
			}
			catch (Exception e)
			{
				mod.Logger.Warn("Levy: could not look for the dialogue table: " + e.Message);
			}
			if (path == null)
			{
				mod.Logger.Warn("Levy: Dialogue/en-US.txt is missing from the mod file; soldiers will be silent.");
				return;
			}
			string text;
			try
			{
				byte[] bytes = mod.GetFileBytes(path);
				if (bytes == null || bytes.Length == 0) { mod.Logger.Warn("Levy: " + path + " is empty."); return; }
				text = new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
			}
			catch (Exception e)
			{
				mod.Logger.Warn("Levy: could not read " + path + ": " + e.Message);
				return;
			}
			LoadedCulture = culture;
			int bad = Parse(mod, path, text);
			mod.Logger.Info($"Levy: dialogue table {path}: {LineCount} lines in {Lines.Count} pools" + (bad > 0 ? $", {bad} bad lines skipped." : "."));
		}

		/// <summary>Parses table text into <see cref="Lines"/>. Returns the number of rejected lines.</summary>
		public static int Parse(Mod mod, string source, string text)
		{
			int rejected = 0;
			var textIds = new Dictionary<string, int>(StringComparer.Ordinal);
			var parts = new List<LinePart>(8);
			var npcs = new List<int>(2);
			var sb = new StringBuilder(96);
			string[] rows = text.Split('\n');
			for (int n = 1; n <= rows.Length; n++)
			{
				string row = rows[n - 1].Trim();
				if (row.Length == 0 || row[0] == '#') continue;

				string[] cols = row.Split('|', 3);
				if (cols.Length < 3) { Warn(mod, source, n, "expected TRIGGER | Temperament | text"); rejected++; continue; }
				string trig = cols[0].Trim(), temp = cols[1].Trim(), body = cols[2].Trim();
				if (body.Length == 0) { Warn(mod, source, n, "empty text"); rejected++; continue; }

				if (!TryParseFilter(temp, out var filter)) { Warn(mod, source, n, "unknown temperament '" + temp + "' (use Greenhorn, Sellsword, Zealot, Cynic, Veteran or Any)"); rejected++; continue; }
				if (!TryParseKey(trig, out string key, out string keyProblem)) { Warn(mod, source, n, keyProblem); rejected++; continue; }

				parts.Clear(); npcs.Clear();
				if (!Tokenize(body, sb, parts, npcs, out var needs, out string tokenProblem)) { Warn(mod, source, n, tokenProblem); rejected++; continue; }

				if (!textIds.TryGetValue(body, out int id)) { id = textIds.Count; textIds[body] = id; }
				var line = new DialogueLine
				{
					Id = id, Filter = filter, Text = body, SourceLine = n, Needs = needs,
					Parts = parts.ToArray(), NpcTypes = npcs.ToArray(), NamedropNpcType = npcs.Count > 0 ? npcs[0] : null,
				};
				if (!Lines.TryGetValue(key, out var list))
				{
					list = new List<DialogueLine>(); Lines[key] = list;
					if (key.StartsWith(BossPrefix, StringComparison.OrdinalIgnoreCase) && !IsKnownBossName(key.Substring(BossPrefix.Length)))
						mod?.Logger.Warn($"Levy: {source} line {n}: {key} is not a vanilla NPC internal name; it only fires for a boss that reports exactly this name.");
				}
				list.Add(line);
				LineCount++;
			}
			return rejected;
		}

		private static void Warn(Mod mod, string source, int line, string problem)
		{
			mod?.Logger.Warn($"Levy: {source} line {line}: {problem}.");
		}

		private static bool TryParseFilter(string s, out LineFilter filter)
		{
			filter = LineFilter.Any;
			if (s.Length == 0 || !char.IsLetter(s[0])) return false;
			return Enum.TryParse(s, true, out filter) && Enum.IsDefined(typeof(LineFilter), filter);
		}

		/// <summary>Trigger column to pool key: a DialogueTrigger name, or BOSS_&lt;npc internal name&gt;.</summary>
		private static bool TryParseKey(string s, out string key, out string problem)
		{
			key = null; problem = null;
			if (s.Length == 0 || !char.IsLetter(s[0])) { problem = "missing trigger"; return false; }
			if (Enum.TryParse(s, true, out DialogueTrigger trigger) && Enum.IsDefined(typeof(DialogueTrigger), trigger))
			{
				if (trigger == DialogueTrigger.BOSS) { problem = "use BOSS_SIGHTED or BOSS_<npc name> as the trigger"; return false; }
				key = trigger.ToString();
				return true;
			}
			if (s.StartsWith(BossPrefix, StringComparison.OrdinalIgnoreCase) && s.Length > BossPrefix.Length)
			{
				key = BossPrefix + s.Substring(BossPrefix.Length);
				return true;
			}
			problem = "unknown trigger '" + s + "'";
			return false;
		}

		/// <summary>True for a vanilla NPC internal name (exact spelling, as NPCID.Search.GetName returns it) or a modded "Mod/Name".</summary>
		private static bool IsKnownBossName(string boss)
		{
			return boss.Contains('/') || NPCID.Search.TryGetId(boss, out _);
		}

		/// <summary>Splits a line into literal runs and placeholders. Fails on an unknown or unbalanced placeholder.</summary>
		private static bool Tokenize(string body, StringBuilder sb, List<LinePart> parts, List<int> npcs, out LineNeeds needs, out string problem)
		{
			needs = LineNeeds.None; problem = null;
			sb.Clear();
			int i = 0;
			while (i < body.Length)
			{
				char c = body[i];
				if (c == '{')
				{
					int close = body.IndexOf('}', i + 1);
					if (close < 0) { problem = "'{' without a closing '}'"; return false; }
					string name = body.Substring(i + 1, close - i - 1).Trim();
					if (!TryPlaceholder(name, out var kind, out int npcType)) { problem = "unknown placeholder {" + name + "}"; return false; }
					if (sb.Length > 0) { parts.Add(new LinePart { Literal = sb.ToString() }); sb.Clear(); }
					parts.Add(new LinePart { Kind = kind, NpcType = npcType });
					switch (kind)
					{
						case PlaceholderKind.Name: needs |= LineNeeds.Name; break;
						case PlaceholderKind.Comrade: needs |= LineNeeds.Comrade; break;
						case PlaceholderKind.Boss: needs |= LineNeeds.Boss; break;
						case PlaceholderKind.Killer: needs |= LineNeeds.Killer; break;
						case PlaceholderKind.Town: needs |= LineNeeds.Town; break;
						case PlaceholderKind.Wage: needs |= LineNeeds.Wage; break;
						case PlaceholderKind.TownNpc: if (!npcs.Contains(npcType)) npcs.Add(npcType); break;
					}
					i = close + 1;
				}
				else if (c == '}') { problem = "'}' without an opening '{'"; return false; }
				else { sb.Append(c); i++; }
			}
			if (sb.Length > 0) parts.Add(new LinePart { Literal = sb.ToString() });
			sb.Clear();
			return true;
		}

		private static bool TryPlaceholder(string name, out PlaceholderKind kind, out int npcType)
		{
			npcType = 0;
			switch (name.ToLowerInvariant())
			{
				case "player": kind = PlaceholderKind.Player; return true;
				case "name": kind = PlaceholderKind.Name; return true;
				case "comrade": kind = PlaceholderKind.Comrade; return true;
				case "boss": kind = PlaceholderKind.Boss; return true;
				case "killer": kind = PlaceholderKind.Killer; return true;
				case "town": kind = PlaceholderKind.Town; return true;
				case "wage": kind = PlaceholderKind.Wage; return true;
			}
			if (TownNpcByName.TryGetValue(name, out npcType)) { kind = PlaceholderKind.TownNpc; return true; }
			kind = PlaceholderKind.Literal;
			return false;
		}
	}
}
