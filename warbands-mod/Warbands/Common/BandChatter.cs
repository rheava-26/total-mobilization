using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Dialogue;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// What a band says over its heads. The lines live in Warbands/Dialogue.txt (plain text, grouped by situation). Levy's dialogue voice: sincere, messy, run-on, aimed at someone (a band mate by name, or the target), never a setup and punchline. Lines are chosen by event; {mate} is another living member's
	/// first name, {who} the player's name, {armor} the set the player wears (Shroomite, Turtle...), {n} a count. Shown as floating text over the speaker; each speaker and each band has a cool-down so a fight does not become a wall of text.
	/// </summary>
	public static class BandChatter
	{
		/// <summary>Group name (as in Dialogue.txt, e.g. "SPOTTED" or "WARNING CorruptZealots") to its lines.</summary>
		public static readonly Dictionary<string, List<string>> Lines = new();

		/// <summary>Reads Warbands/Dialogue.txt (plain text, see the header of that file) into <see cref="Lines"/>.</summary>
		public static void Load(Mod mod)
		{
			Lines.Clear();
			try
			{
				string text = System.Text.Encoding.UTF8.GetString(mod.GetFileBytes("Dialogue.txt"));
				// Player-editable copy: <tModLoader save folder>/Warbands/Dialogue.txt. Written with the default lines on first run; if present, it replaces the built-in lines.
				try
				{
					string dir = System.IO.Path.Combine(Terraria.Main.SavePath, "Warbands"), path = System.IO.Path.Combine(dir, "Dialogue.txt");
					string shipped = text;
					if (System.IO.File.Exists(path))
					{
						string mine = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
						if (Hash(mine) == Hash(shipped)) { }
						else if (ShippedBefore(mine))
						{
							// an untouched copy of an older version: replace it with this version's lines
							System.IO.File.WriteAllText(path, shipped, System.Text.Encoding.UTF8);
							mod.Logger.Info("[Warbands] updated your unedited dialogue file to this version: " + path);
						}
						else
						{
							// an edited copy: keep it, and fill any group it lacks from this version's lines
							text = mine + "\n" + MissingGroups(mine, shipped);
							mod.Logger.Info("[Warbands] using your dialogue file (groups it lacks come from the mod): " + path);
						}
					}
					else { System.IO.Directory.CreateDirectory(dir); System.IO.File.WriteAllText(path, text, System.Text.Encoding.UTF8); }
				}
				catch (Exception e) { mod.Logger.Warn("[Warbands] could not use the editable dialogue file: " + e.Message); }
				List<string> cur = null;
				foreach (var raw in text.Split('\n'))
				{
					string l = raw.Trim();
					if (l.Length == 0 || l[0] == '#') continue;
					if (l[0] == '[' && l[l.Length - 1] == ']') { string name = l.Substring(1, l.Length - 2).Trim(); if (!Lines.TryGetValue(name, out cur)) Lines[name] = cur = new List<string>(); continue; }
					cur?.Add(l);
				}
			}
			catch (Exception e) { mod.Logger.Warn("[Warbands] could not read Dialogue.txt: " + e.Message); }
		}

		/// <summary>SHA-256 of a dialogue file's text, line endings and byte-order mark ignored.</summary>
		private static string Hash(string t)
		{
			t = t.TrimStart((char)0xFEFF).Replace("\r\n", "\n");
			using var sha = System.Security.Cryptography.SHA256.Create();
			return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(t))).ToLowerInvariant();
		}

		/// <summary>Hashes of every Dialogue.txt an earlier version wrote to the save folder. A copy matching one was never edited.</summary>
		private static readonly HashSet<string> OlderShipped = new()
		{
			"0de1383e1856af493cb5686c5bb804c5374b4fd051ee051734a1c2507dd9766f",
			"60429b7a96bc6cbbd5fb731878f49ab1278de071d158c4114cedb2dd9e758aef",
		};
		private static bool ShippedBefore(string t) => OlderShipped.Contains(Hash(t));

		/// <summary>The groups of <paramref name="shipped"/> (header and lines) whose [NAME] does not appear in <paramref name="mine"/>.</summary>
		private static string MissingGroups(string mine, string shipped)
		{
			var have = new HashSet<string>();
			foreach (var raw in mine.Split('\n')) { string l = raw.Trim(); if (l.Length > 1 && l[0] == '[' && l[l.Length - 1] == ']') have.Add(l.Substring(1, l.Length - 2).Trim()); }
			var sb = new System.Text.StringBuilder(); bool take = false;
			foreach (var raw in shipped.Split('\n'))
			{
				string l = raw.Trim();
				if (l.Length > 1 && l[0] == '[' && l[l.Length - 1] == ']') take = !have.Contains(l.Substring(1, l.Length - 2).Trim());
				if (take) sb.Append(l).Append('\n');
			}
			return sb.ToString();
		}

		// ---------------------------------------------------------------- Levy's own fighting lines
		/// <summary>Which of Levy's dialogue triggers (Levy/Dialogue/en-US.txt, or the player's edited copy of it) feed each Warbands situation. Only situations a soldier on the other side has in common with a levy.</summary>
		public static readonly Dictionary<string, string[]> LevyFeeds = new()
		{
			["SPOTTED"] = new[] { "ENGAGE" }, ["WARNING_BROKEN"] = new[] { "ENGAGE" },
			["HURT"] = new[] { "HURT" }, ["LOW_LIFE"] = new[] { "LOW_HP" },
			["MATE_DIED"] = new[] { "COMRADE_DOWN" }, ["ONE_HIT"] = new[] { "COMRADE_DOWN", "SHAKEN" },
			["PANIC"] = new[] { "SHAKEN" }, ["RETREAT"] = new[] { "ROUTED" }, ["DEFEAT"] = new[] { "SHAKEN", "ROUTED" },
			["VICTORY"] = new[] { "VICTORY", "KILL" }, ["LEVY_BANTER"] = new[] { "HURT", "KILL" },
		};
		/// <summary>Words that only make sense for the player's own army (the commander, the barracks, wages, the boss fight, the town's healers); a Levy line with one is not borrowed.</summary>
		private static readonly Regex OwnArmy = new(@"\b(pay|paid|payday|payroll|wage|wages|salary|barracks|nurse|commander|boss|contract|army|levy|levies|banner|tally|dryad|guide|loot|oath)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Dictionary<string, List<string>> _levy = new(); private static int _levyStamp = -1;

		/// <summary>True when a Levy line can be spoken by an enemy soldier: no condition tag, no placeholder but the comrade, no wording about the player's own army.</summary>
		public static bool Borrowable(DialogueLine l)
		{
			if (l == null || l.Cond != LineCond.None || (l.Needs & ~LineNeeds.Comrade) != LineNeeds.None) return false;
			foreach (var part in l.Parts) if (part.Kind != PlaceholderKind.Literal && part.Kind != PlaceholderKind.Comrade) return false;
			return !OwnArmy.IsMatch(l.Text);
		}

		/// <summary>Levy's lines for a Warbands situation that an enemy soldier can say ({Comrade} becomes {mate}); empty when the situation has no feed or Levy's table is not loaded. Rebuilt when Levy reloads its table.</summary>
		public static List<string> LevyPool(string key)
		{
			try
			{
				int stamp = DialogueTable.LineCount * 31 + DialogueTable.Lines.Count + (DialogueTable.LoadedFrom?.Length ?? 0);
				if (stamp != _levyStamp)
				{
					_levyStamp = stamp; _levy = new();
					foreach (var kv in LevyFeeds)
					{
						var list = new List<string>();
						foreach (var trig in kv.Value)
						{
							var src = DialogueTable.Get(trig); if (src == null) continue;
							foreach (var l in src) if (Borrowable(l)) list.Add(l.Text.Replace("{Comrade}", "{mate}"));
						}
						_levy[kv.Key] = list;
					}
				}
				return _levy.TryGetValue(key, out var r) ? r : null;
			}
			catch { return null; }
		}

		private static List<string> Group(string name) => Lines.TryGetValue(name, out var l) && l.Count > 0 ? l : null;

		private static readonly Dictionary<int, uint> _speakerAt = new();
		/// <summary>Lab only (env WARBANDS_CHAT_LEGACY=1): the old chatter, every line at once as vanilla combat text over its speaker, a two second cool-down across all bands.</summary>
		public static readonly bool Legacy = Environment.GetEnvironmentVariable("WARBANDS_CHAT_LEGACY") == "1";
		/// <summary>Lab probe: called with the speaker and the line whenever a line is shown.</summary>
		public static Action<NPC, string> Probe;
		private static uint _bandAt;

		/// <summary>One band's speech: it says one line at a time. A line that arrives while another is on show waits its turn (an urgent one, at most two of them, for up to five seconds); an ordinary one is dropped.</summary>
		private class BandTalk { public uint BusyUntil; public readonly List<(BandLevyNPC who, string text, string key, uint expire)> Queue = new(); }
		private static readonly Dictionary<int, BandTalk> _talk = new();
		private static BandTalk Talk(int band) { if (!_talk.TryGetValue(band, out var t)) _talk[band] = t = new BandTalk(); return t; }
		public static void ResetTalk() { _talk.Clear(); _speakerAt.Clear(); }

		/// <summary>One line for an event: the band type's own group (warnings always, other situations half the time), else the band style's group, else the plain group.</summary>
		public static string Pick(string key, BandLevyNPC speaker, string who, string armor, int n)
		{
			var r = speaker?.Roster; List<string> pool = null;
			if (r != null)
			{
				var own = Group(key + " " + r.Id) ?? (r.FlavorKey != null ? Group(key + " " + r.FlavorKey) : null);
				if (own != null && (key.StartsWith("WARNING") || Main.rand.NextBool(2))) pool = own;
			}
			pool ??= Group(key);
			// half the time (when the config allows it) the line is one of Levy's own for that situation, otherwise one of ours
			if (WarbandsConfig.Get?.UseLevyLines != false && Main.rand.NextBool(2))
			{
				var levy = LevyPool(key);
				if (levy != null && levy.Count > 0) pool = levy;
			}
			if (pool == null) return null;
			string line = pool[Main.rand.Next(pool.Count)];
			string mate = speaker?.RandomMateName() ?? "mate";
			return line.Replace("{mate}", mate).Replace("{who}", who ?? "you").Replace("{armor}", armor ?? "plate").Replace("{n}", n.ToString());
		}

		/// <summary>Says a line for an event, if the speaker is not on cool-down and the band is not already talking (an urgent line waits its turn). Returns the text (or null when it was dropped or queued).</summary>
		public static string Say(BandLevyNPC speaker, string key, string who = null, string armor = null, int n = 0, string literal = null, bool force = false)
		{
			if (speaker == null || !speaker.NPC.active || Main.netMode == NetmodeID.MultiplayerClient) return null;
			uint now = Main.GameUpdateCount;
			if (Legacy)
			{
				if (!force)
				{
					if (_speakerAt.TryGetValue(speaker.NPC.whoAmI, out uint t0) && now - t0 < 60 * 7) return null;
					if (now - _bandAt < 60 * 2) return null;
				}
				string lt = literal ?? Pick(key, speaker, who, armor, n);
				if (lt == null) return null;
				_speakerAt[speaker.NPC.whoAmI] = now; _bandAt = now;
				Emit(speaker.NPC, lt); Log.Add(key + ": " + lt);
				return lt;
			}
			var talk = Talk(speaker.BandId);
			if (!force && _speakerAt.TryGetValue(speaker.NPC.whoAmI, out uint t) && now - t < 60 * 7) return null;
			bool busy = now < talk.BusyUntil || talk.Queue.Count > 0;
			if (busy && !force) return null;
			string text = literal ?? Pick(key, speaker, who, armor, n);
			if (text == null) return null;
			_speakerAt[speaker.NPC.whoAmI] = now;
			if (!busy) { Speak(talk, speaker, text, key, now); return text; }
			if (talk.Queue.Count >= 2) talk.Queue.RemoveAt(0);
			talk.Queue.Add((speaker, text, key, now + 300));
			return null;
		}

		private static void Speak(BandTalk talk, BandLevyNPC speaker, string text, string key, uint now)
		{
			talk.BusyUntil = now + (uint)BandBubbles.LifeFor(text) + 18u;
			Emit(speaker.NPC, text);
			Log.Add(key + ": " + text);
		}

		/// <summary>Every tick on the server and in single player: the next queued line of each band starts once its band is quiet.</summary>
		public static void Pump()
		{
			if (_talk.Count == 0) return;
			uint now = Main.GameUpdateCount;
			foreach (var kv in _talk)
			{
				var t = kv.Value;
				while (t.Queue.Count > 0 && now >= t.BusyUntil)
				{
					var q = t.Queue[0]; t.Queue.RemoveAt(0);
					if (now > q.expire || q.who == null || !q.who.NPC.active || q.who.NPC.life <= 0) continue;
					Speak(t, q.who, q.text, q.key, now);
				}
			}
		}

		/// <summary>A line over the speaker: a speech bubble on every screen (single player draws it itself, the server sends it to the clients).</summary>
		public static void Emit(NPC npc, string text)
		{
			Probe?.Invoke(npc, text);
			if (Legacy)
			{
				var color = new Color(240, 130, 110);
				var rect = new Rectangle((int)npc.Center.X, (int)npc.Top.Y - 20, 1, 1);
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.CombatTextString, -1, -1, NetworkText.FromLiteral(text), (int)color.PackedValue, rect.X, rect.Y);
				else { int i = CombatText.NewText(rect, color, text); if (i >= 0 && i < Main.combatText.Length) Main.combatText[i].lifeTime = 210; }
				return;
			}
			var b = npc.ModNPC as BandLevyNPC; int band = b?.BandId ?? 0; string name = b?.ShortName;
			if (Main.netMode == NetmodeID.Server) BandBubbles.Send(npc, band, name, text);
			else if (!Main.dedServ) BandBubbles.Add(npc.whoAmI, band, name, text, new Color(240, 130, 110), Main.GameUpdateCount);
		}

		/// <summary>Lines said this session, newest last (self-check and lab read it).</summary>
		public static class Log { public static readonly List<string> Lines = new(); public static void Add(string s) { if (Lines.Count < 400) Lines.Add(s); } }

		/// <summary>The name of the set a player wears, from the body slot: "Shroomite Breastplate" gives "Shroomite".</summary>
		public static string ArmorSetName(Player p)
		{
			if (p == null) return null;
			var body = p.armor[1];
			if (body == null || body.IsAir) return null;
			string n = body.Name;
			foreach (var suf in new[] { " Breastplate", " Chainmail", " Scale Mail", " Plate Mail", " Mail", " Scalemail", " Shirt", " Armor", " Suit", " Vest", " Jacket", " Tunic", " Robe", " Plate" })
				if (n.EndsWith(suf, StringComparison.Ordinal)) { n = n.Substring(0, n.Length - suf.Length); break; }
			return n;
		}
	}
}
