using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Dialogue
{
	/// <summary>Line selection and placeholder filling, plus the small amount of runtime state the budgets need.</summary>
	public static partial class DialogueSystem
	{
		internal const int MaxBodies = 256;
		private const int Never = int.MinValue / 2;

		private static int Now => (int)Main.GameUpdateCount;
		/// <summary>True when 'last' happened less than 'cooldown' ticks before 'now'. Safe against a tick counter that restarts.</summary>
		private static bool Within(int last, int now, int cooldown) => (uint)(now - last) < (uint)cooldown;

		// ---- runtime state (all reset with the world) ----
		private static readonly Dictionary<int, int> LineUsed = new();
		private static readonly int[] LastEmote = NewTicks();
		private static readonly int[] LastChatTick = NewTicks();
		private static readonly int[] LastChatLine = new int[MaxBodies];
		private static readonly RecentLine[] Recent = new RecentLine[MaxBodies];
		private static readonly Dictionary<string, SquadClock> RegularSquads = new(StringComparer.Ordinal);
		private static readonly Dictionary<int, SquadClock> MilitiaSquads = new();

		private static readonly List<DialogueLine> Candidates = new();
		private static readonly StringBuilder Scratch = new(160);
		private static readonly Filler Fill = new();

		private struct RecentLine
		{
			public bool Valid;
			public DialogueTrigger Trigger;
			public string Line;
			public int Tick;
			public int RecordId;
			/// <summary>Repeat-cooldown id of the remembered line (-1 on a client, where the line arrives as text only).</summary>
			public int LineId;
		}

		/// <summary>Per-squad floating-text clocks. Chatter competes with everything; big moments only with each other.</summary>
		private sealed class SquadClock
		{
			public int Any = Never, Big = Never;
		}

		private static int[] NewTicks()
		{
			var a = new int[MaxBodies];
			Array.Fill(a, Never);
			return a;
		}

		internal static void ResetRuntime()
		{
			LineUsed.Clear();
			Array.Fill(LastEmote, Never);
			Array.Fill(LastChatTick, Never);
			Array.Clear(LastChatLine);
			Array.Clear(Recent);
			RegularSquads.Clear();
			MilitiaSquads.Clear();
		}

		private static SquadClock Squad(SoldierRecord rec)
		{
			SquadClock c;
			if (rec.Kind == SoldierKind.Militia)
			{
				if (!MilitiaSquads.TryGetValue(rec.HomeBarracksId, out c)) MilitiaSquads[rec.HomeBarracksId] = c = new SquadClock();
			}
			else
			{
				string key = rec.CommanderName ?? "";
				if (!RegularSquads.TryGetValue(key, out c)) RegularSquads[key] = c = new SquadClock();
			}
			return c;
		}

		// =====================================================================================================
		// Selection
		// =====================================================================================================

		/// <summary>
		/// Picks and fills a line. 'body' is the speaking soldier's body when the caller has it (else it is found through the record).
		/// Marks the line used. Returns null when nothing fits.
		/// </summary>
		internal static string Pick(DialogueTrigger trigger, SoldierRecord rec, DialogueContext ctx, SoldierNPC body, bool ignoreRepeat, int avoidId, out DialogueLine picked, bool mark = true)
		{
			picked = null;
			List<DialogueLine> pool = PoolFor(trigger, ctx);
			if (pool == null) return null;

			int now = Now;
			Fill.Begin(rec, ctx, body);
			Candidates.Clear();
			int totalWeight = 0;
			for (int i = 0; i < pool.Count; i++)
			{
				DialogueLine line = pool[i];
				if (!FilterMatches(line.Filter, rec)) continue;
				if (line.Id == avoidId) continue;
				if (!ignoreRepeat && LineUsed.TryGetValue(line.Id, out int last) && Within(last, now, LevyConstants.LineRepeatCooldownTicks)) continue;
				if (!NpcsAlive(line)) continue;
				if (!Fill.Can(line.Needs)) continue;
				Candidates.Add(line);
				totalWeight += line.Filter == LineFilter.Any ? 1 : LevyConstants.DialoguePersonalityWeight;
			}
			if (Candidates.Count == 0) return null;

			int roll = Main.rand.Next(totalWeight);
			DialogueLine chosen = Candidates[Candidates.Count - 1];
			for (int i = 0; i < Candidates.Count; i++)
			{
				roll -= Candidates[i].Filter == LineFilter.Any ? 1 : LevyConstants.DialoguePersonalityWeight;
				if (roll < 0) { chosen = Candidates[i]; break; }
			}

			string text = Fill.Build(chosen);
			Candidates.Clear();
			if (mark) LineUsed[chosen.Id] = now;
			picked = chosen;
			return text;
		}

		/// <summary>
		/// The pool for a trigger. BOSS uses BOSS_&lt;BossKey&gt;, and only when the table has no line for that boss at all
		/// does it fall back to the generic BOSS_SIGHTED pool.
		/// </summary>
		private static List<DialogueLine> PoolFor(DialogueTrigger trigger, DialogueContext ctx)
		{
			if (trigger == DialogueTrigger.BOSS)
			{
				string bossKey = ctx?.BossKey;
				if (!string.IsNullOrEmpty(bossKey))
				{
					var specific = DialogueTable.Get(DialogueTable.BossPrefix + bossKey);
					if (specific != null) return specific;
				}
				return DialogueTable.Get(DialogueTrigger.BOSS_SIGHTED);
			}
			return DialogueTable.Get(trigger);
		}

		private static bool FilterMatches(LineFilter filter, SoldierRecord rec)
		{
			switch (filter)
			{
				case LineFilter.Any: return true;
				case LineFilter.Veteran: return rec != null && rec.Veteran;
				default: return rec != null && (int)rec.Temperament + 1 == (int)filter;
			}
		}

		/// <summary>A line naming a town NPC only fires while every NPC it names is alive.</summary>
		private static bool NpcsAlive(DialogueLine line)
		{
			int[] types = line.NpcTypes;
			for (int i = 0; i < types.Length; i++)
				if (NPC.FindFirstNPC(types[i]) < 0) return false;
			return true;
		}

		private static string Localized(string key, string fallback)
		{
			string full = "Mods.Levy.Dialogue." + key;
			return Language.Exists(full) ? Language.GetTextValue(full) : fallback;
		}

		/// <summary>The soldier body bound to a record, if it exists on this machine.</summary>
		internal static SoldierNPC BodyOf(SoldierRecord rec)
		{
			if (rec == null || !rec.HasBody) return null;
			return Main.npc[rec.BodyIndex].ModNPC as SoldierNPC;
		}

		// =====================================================================================================
		// Placeholder filling
		// =====================================================================================================

		/// <summary>Resolves placeholder values lazily (one scan of the NPC array at most) for the candidate lines of a single Pick.</summary>
		private sealed class Filler
		{
			private SoldierRecord _rec;
			private DialogueContext _ctx;
			private SoldierNPC _body;
			private LineNeeds _resolved;
			private string _name, _comrade, _boss, _killer, _town, _wage;
			private string _player;

			public void Begin(SoldierRecord rec, DialogueContext ctx, SoldierNPC body)
			{
				_rec = rec; _ctx = ctx; _body = body ?? BodyOf(rec);
				_resolved = LineNeeds.None;
				_name = _comrade = _boss = _killer = _town = _wage = _player = null;
			}

			public bool Can(LineNeeds needs)
			{
				if (needs == LineNeeds.None) return true;
				for (int bit = 1; bit <= (int)LineNeeds.Wage; bit <<= 1)
					if (((int)needs & bit) != 0 && Value((LineNeeds)bit) == null) return false;
				return true;
			}

			private string Value(LineNeeds need)
			{
				if ((_resolved & need) == 0)
				{
					_resolved |= need;
					try
					{
						switch (need)
						{
							case LineNeeds.Name: _name = NonEmpty(_rec?.Name); break;
							case LineNeeds.Comrade: _comrade = NonEmpty(_ctx?.Comrade) ?? FindComrade(); break;
							case LineNeeds.Boss: _boss = NonEmpty(_ctx?.Boss) ?? BossName(_ctx?.BossKey); break;
							case LineNeeds.Killer: _killer = NonEmpty(_ctx?.Killer) ?? NonEmpty(_body?.LastAttackerName); break;
							case LineNeeds.Town: _town = NonEmpty(_ctx?.Town) ?? NonEmpty(HomeTown()); break;
							case LineNeeds.Wage: _wage = NonEmpty(_ctx?.Wage) ?? WageText(); break;
						}
					}
					catch (Exception)
					{
						// Another system is not ready (no roster, no config): the placeholder stays unfilled and its lines are skipped.
						// A bark must never throw into the soldier AI.
					}
				}
				switch (need)
				{
					case LineNeeds.Name: return _name;
					case LineNeeds.Comrade: return _comrade;
					case LineNeeds.Boss: return _boss;
					case LineNeeds.Killer: return _killer;
					case LineNeeds.Town: return _town;
					default: return _wage;
				}
			}

			private static string NonEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

			private string Player()
			{
				return _player ??= NonEmpty(_ctx?.Player) ?? NonEmpty(_rec?.CommanderName) ?? Localized("DefaultPlayer", "the boss");
			}

			private string HomeTown()
			{
				if (_rec == null) return null;
				var settlement = SettlementSystem.Instance;
				return settlement?.TownName(settlement.GetBarracks(_rec.HomeBarracksId));
			}

			private string WageText()
			{
				if (_rec == null) return null;
				return LevyUtil.Coins(EconomySystem.WagePerDay(_rec.Grade, false));
			}

			private static string BossName(string bossKey)
			{
				if (string.IsNullOrEmpty(bossKey)) return null;
				return NPCID.Search.TryGetId(bossKey, out int id) ? Terraria.Lang.GetNPCNameValue(id) : bossKey;
			}

			/// <summary>A random living squad-mate: same commander (Regulars) or same barracks (militia), near the speaker when it has a body.</summary>
			private string FindComrade()
			{
				if (_rec == null) return null;
				float maxSq = LevyConstants.DialogueComradeSearchTiles * LevyConstants.TilePx;
				maxSq *= maxSq;
				string found = null;
				int seen = 0;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active || n.ModNPC is not SoldierNPC other) continue;
					SoldierRecord o = other.Record;
					if (o == null || ReferenceEquals(o, _rec) || !other.IsAlive || string.IsNullOrWhiteSpace(o.Name)) continue;
					if (_rec.Kind == SoldierKind.Militia ? (o.Kind != SoldierKind.Militia || o.HomeBarracksId != _rec.HomeBarracksId) : (o.Kind != SoldierKind.Regular || o.CommanderName != _rec.CommanderName)) continue;
					if (_body != null && Microsoft.Xna.Framework.Vector2.DistanceSquared(_body.NPC.Center, n.Center) > maxSq) continue;
					seen++;
					if (Main.rand.Next(seen) == 0) found = o.Name; // reservoir sample: uniform without allocating
				}
				return found;
			}

			public string Build(DialogueLine line)
			{
				Scratch.Clear();
				LinePart[] parts = line.Parts;
				for (int i = 0; i < parts.Length; i++)
				{
					LinePart p = parts[i];
					switch (p.Kind)
					{
						case PlaceholderKind.Literal: Scratch.Append(p.Literal); break;
						case PlaceholderKind.Player: Scratch.Append(Player()); break;
						case PlaceholderKind.Name: Scratch.Append(Value(LineNeeds.Name)); break;
						case PlaceholderKind.Comrade: Scratch.Append(Value(LineNeeds.Comrade)); break;
						case PlaceholderKind.Boss: Scratch.Append(Value(LineNeeds.Boss)); break;
						case PlaceholderKind.Killer: Scratch.Append(Value(LineNeeds.Killer)); break;
						case PlaceholderKind.Town: Scratch.Append(Value(LineNeeds.Town)); break;
						case PlaceholderKind.Wage: Scratch.Append(Value(LineNeeds.Wage)); break;
						case PlaceholderKind.TownNpc:
						{
							int idx = NPC.FindFirstNPC(p.NpcType);
							Scratch.Append(idx >= 0 ? Main.npc[idx].GivenOrTypeName : "");
							break;
						}
					}
				}
				return Scratch.ToString();
			}
		}
	}
}
