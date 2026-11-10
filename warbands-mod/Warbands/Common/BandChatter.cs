using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
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

		private static List<string> Group(string name) => Lines.TryGetValue(name, out var l) && l.Count > 0 ? l : null;

		private static readonly Dictionary<int, uint> _speakerAt = new();
		private static uint _bandAt;

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
			if (pool == null) return null;
			string line = pool[Main.rand.Next(pool.Count)];
			string mate = speaker?.RandomMateName() ?? "mate";
			return line.Replace("{mate}", mate).Replace("{who}", who ?? "you").Replace("{armor}", armor ?? "plate").Replace("{n}", n.ToString());
		}

		/// <summary>Says a line for an event, if the speaker and the band are not on cool-down. Returns the text (or null).</summary>
		public static string Say(BandLevyNPC speaker, string key, string who = null, string armor = null, int n = 0, string literal = null, bool force = false)
		{
			if (speaker == null || !speaker.NPC.active || Main.netMode == NetmodeID.MultiplayerClient) return null;
			uint now = Main.GameUpdateCount;
			if (!force)
			{
				if (_speakerAt.TryGetValue(speaker.NPC.whoAmI, out uint t) && now - t < 60 * 7) return null;
				if (now - _bandAt < 60 * 2) return null;
			}
			string text = literal ?? Pick(key, speaker, who, armor, n);
			if (text == null) return null;
			_speakerAt[speaker.NPC.whoAmI] = now; _bandAt = now;
			Emit(speaker.NPC, text);
			Log.Add(key + ": " + text);
			return text;
		}

		/// <summary>Floating text over the speaker (single player and server; the server broadcasts it).</summary>
		public static void Emit(NPC npc, string text)
		{
			var color = new Color(240, 130, 110);
			var rect = new Rectangle((int)npc.Center.X, (int)npc.Top.Y - 20, 1, 1);
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.CombatTextString, -1, -1, NetworkText.FromLiteral(text), (int)color.PackedValue, rect.X, rect.Y);
			else
			{
				int i = CombatText.NewText(rect, color, text);
				if (i >= 0 && i < Main.combatText.Length) Main.combatText[i].lifeTime = 210;
			}
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
