using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// LEVIATHAN / ANAHITA PROBE (boss=levi in a calrev spec; measurement only, nothing here changes behaviour). Calamity's Leviathan charges in NPC.ai[0] == 2: ai[1] even is the line-up (damage 0, it flies to 900 px
	/// beside its target and level with it), ai[1] odd is the dash (damage on, velocity aimed at the target once, then constant until it brakes). The probe writes
	///   .levi.tsv      one row per dash: when the line-up started, how long it lasted, where the boss launched from and along which line, who it was aimed at, how many levies stood in the lane, and what they did
	///   .levilv.tsv    one row per (dash, levy in or near the lane): where it stood relative to the line at launch, whether the threat field listed the Leviathan, its dodge counters, where it was 20/45 ticks later and whether it was hit
	///   .levihit.tsv   one row per hit a levy took from the Leviathan or Anahita (or anything else in a fight with them): tick, damage, attacker, which dash it belongs to, died or not
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class LvLevy
		{
			public int Idx; public char Cls; public Vector2 P0; public float Along, Perp; public bool InLane; public bool Wings, Dash;
			public int Man0, Step0, Jump0, Dash0, Drop0, Dj0, Hook0, Air0, Scan0, NoBetter0; public Vector2 P20, P45, PEnd; public bool Hit, Died; public int HitDmg, FirstHitDt = -1, FieldSeenDt = -1; public bool Grounded0; public Vector2 V0;
			public int FieldDuringWind; public int ManW0, ManWind; public Vector2 PWind; public int Lane0;
		}
		private sealed class LvCharge
		{
			public int Id, WindStart, Launch, End = -1; public Vector2 Boss0, Dir; public float Speed; public int TargetLevy = -1, AliveAtLaunch; public float TargetY; public List<LvLevy> Lv = new();
			public int FieldSeenDt = -1; public bool Open => End < 0;
			public int Deaths, Hits, HitDmg, DashNo; public float MinPerp = 1e9f; public int AimIdx = -1; public float WindBossY0;
		}
		private static int _lvLeviType = -2, _lvAnaType = -2;
		private LvCharge _lvCur; private int _lvChargeId, _lvLastAi0 = -1, _lvLastAi1 = -1, _lvWindStart = -1; private bool _lvWinding; private int _lvLeviIdx = -1;
		private readonly Dictionary<int, int> _lvLife = new();
		private int _lvLastChargeEnd = -1000; private int _lvLastChargeId = -1;
		private int _lvHeader;

		private static string LvF(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

		private void LeviProbe(CrState st)
		{
			if (st.Run.Boss != "levi") return;
			if (_lvLeviType == -2) { _lvLeviType = CrCalNpc("Leviathan"); _lvAnaType = CrCalNpc("Anahita"); }
			string key = st.Run.Key; int tick = st.Tick;
			if (_lvHeader == 0)
			{
				_lvHeader = 1;
				if (!File.Exists(_crOut + ".levi.tsv")) File.WriteAllText(_crOut + ".levi.tsv", "key\tcharge\twindStart\twindTicks\tlaunch\tend\tbossX\tbossY\tdirX\tdirY\tspeed\tdashNo\taimIdx\tminPerpPx\talive\tinLane\tfieldSeenDt\tdeaths\thits\thitDmg\tlaneEsc_noPlan_holdTicks\tbossLifePct\n");
				if (!File.Exists(_crOut + ".levilv.tsv")) File.WriteAllText(_crOut + ".levilv.tsv", "key\tcharge\tlevy\tcls\talong\tperp\tinLane\twings\tdash\tgrounded\tvx0\tvy0\tmanW\tmanLaunchToEnd\tstepD\tjumpD\tdashD\tdropD\tdjD\thookD\tairD\tperpAtWindStart\tfieldSeenDt\tlaneD\tdx20\tdy20\tdx45\tdy45\tdxEnd\tdyEnd\thit\thitDt\thitDmg\tdied\n");
				if (!File.Exists(_crOut + ".levihit.tsv")) File.WriteAllText(_crOut + ".levihit.tsv", "key\ttick\tlevy\tcls\tdmg\thpBefore\thpAfter\tattacker\tcharge\tdtLaunch\tdied\tgrounded\twet\tvy\tperpToLane\n");
			}
			// the Leviathan
			NPC levi = null;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.type == _lvLeviType) { levi = n; break; } }
			// hits first (before the dash state moves on)
			LvHits(st, levi, key, tick);
			if (levi == null) { if (_lvCur != null) LvClose(st, key, tick, null); _lvLastAi0 = -1; _lvWinding = false; return; }
			int ai0 = (int)levi.ai[0], ai1 = (int)levi.ai[1];
			bool chargeState = ai0 == 2 && levi.Opacity > 0.9f;
			// line-up start: the charge state with an even counter (also again after each dash, a charge is two counts)
			if (chargeState && (ai1 & 1) == 0)
			{
				if (!_lvWinding) { _lvWinding = true; _lvWindStart = tick; _lvWind0.Clear(); foreach (var l in st.Levies) { NPC n = Main.npc[l.Idx]; if (!l.Dead && n.active && n.ModNPC is SoldierNPC sn0) _lvWind0[l.Idx] = (n.Center, sn0.DodgeManeuvers); } }
			}
			else if (!(chargeState && (ai1 & 1) == 1)) _lvWinding = false;
			// launch: the counter just went odd
			if (chargeState && (ai1 & 1) == 1 && (_lvLastAi1 & 1) == 0 && _lvLastAi0 == 2 || (chargeState && (ai1 & 1) == 1 && _lvCur == null))
			{
				if (_lvCur != null) LvClose(st, key, tick, levi);
				LvOpen(st, levi, key, tick);
			}
			else if (_lvCur != null && !(chargeState && (ai1 & 1) == 1)) LvClose(st, key, tick, levi);
			if (_lvCur != null) LvTrack(st, levi, tick);
			// the field's view during the line-up / dash: does the snapshot hold a body threat at the Leviathan
			if (chargeState && (_lvWinding || _lvCur != null))
			{
				bool seen = false;
				var snap = ThreatField.Snapshot();
				for (int k = 0; k < snap.Count; k++) if (snap[k].Body && Vector2.DistanceSquared(snap[k].P, levi.Center) < 3600f) { seen = true; break; }
				if (seen)
				{
					if (_lvCur != null) { if (_lvCur.FieldSeenDt < 0) _lvCur.FieldSeenDt = tick - _lvCur.Launch; }
					else _lvWindSeen++;
				}
				else if (_lvCur == null) _lvWindNotSeen++;
			}
			_lvLastAi0 = chargeState ? 2 : ai0; _lvLastAi1 = ai1;
		}
		private int _lvWindSeen, _lvWindNotSeen; private readonly Dictionary<int, (Vector2 p, int man)> _lvWind0 = new();

		private void LvOpen(CrState st, NPC levi, string key, int tick)
		{
			_lvWinding = false; var c = new LvCharge { Id = ++_lvChargeId, DashNo = ((int)levi.ai[1] + 1) / 2, WindStart = _lvWindStart < 0 ? tick : _lvWindStart, Launch = tick, Boss0 = levi.Center, Speed = levi.velocity.Length() };
			c.Dir = levi.velocity.LengthSquared() > 0.01f ? Vector2.Normalize(levi.velocity) : new Vector2(levi.direction, 0f);
			int tl = global::Levy.Common.Globals.LevyNpcData.Of(levi).SoldierTarget; c.TargetLevy = tl; if (tl >= 0 && tl < Main.maxNPCs && Main.npc[tl].active) c.TargetY = Main.npc[tl].Center.Y;
			c.WindBossY0 = levi.Center.Y;
			Vector2 n2 = new Vector2(-c.Dir.Y, c.Dir.X);
			foreach (var l in st.Levies)
			{
				if (l.Dead) continue; NPC n = Main.npc[l.Idx]; if (!n.active || !(n.ModNPC is SoldierNPC sn)) continue;
				Vector2 rel = n.Center - levi.Center;
				float along = Vector2.Dot(rel, c.Dir), perp = Vector2.Dot(rel, n2);
				var lv = new LvLevy { Idx = l.Idx, Cls = l.Cls, P0 = n.Center, Along = along, Perp = perp, Wings = sn.Abilities != null && sn.Abilities.HasWings, Dash = sn.Abilities != null && sn.Abilities.HasDash, Grounded0 = n.velocity.Y == 0f, V0 = n.velocity,
					Man0 = sn.DodgeManeuvers, Step0 = sn.DodgeSteps, Jump0 = sn.DodgeJumps, Dash0 = sn.DodgeDashes, Drop0 = sn.DodgeDrops, Dj0 = sn.DodgeDoubleJumps, Lane0 = sn.LaneEscapes, Hook0 = sn.DodgeHooks, Air0 = sn.DodgeAirManeuvers, Scan0 = sn.DodgeBaseHitScans, NoBetter0 = sn.DodgeNoBetter };
				// in the lane: the boss rectangle (900 x 450) swept along the line, from behind the boss to well past the target
				if (along > 0 && Math.Abs(perp) < c.MinPerp) { c.MinPerp = Math.Abs(perp); c.AimIdx = l.Idx; } lv.InLane = Math.Abs(perp) <= levi.height * 0.5f + n.height * 0.5f + 4f && along >= -levi.width * 0.5f;
				if (_lvWind0.TryGetValue(l.Idx, out var w0)) { lv.PWind = w0.p; lv.ManWind = sn.DodgeManeuvers - w0.man; } c.Lv.Add(lv);
			}
			c.AliveAtLaunch = c.Lv.Count;
			_lvCur = c;
		}

		private void LvTrack(CrState st, NPC levi, int tick)
		{
			var c = _lvCur; int dt = tick - c.Launch;
			foreach (var lv in c.Lv)
			{
				NPC n = Main.npc[lv.Idx]; if (!n.active) continue;
				if (dt == 20) lv.P20 = n.Center; if (dt == 45) lv.P45 = n.Center; lv.PEnd = n.Center;
			}
			// boss brakes when the counter next goes even: closed by the caller
		}

		private void LvClose(CrState st, string key, int tick, NPC levi)
		{
			var c = _lvCur; if (c == null) return; _lvCur = null; c.End = tick; _lvLastChargeEnd = tick; _lvLastChargeId = c.Id;
			int inLane = c.Lv.Count(l => l.InLane);
			int windTicks = c.Launch - c.WindStart;
			var sbc = new System.Text.StringBuilder();
			double bl = levi != null && levi.lifeMax > 0 ? 100.0 * levi.life / levi.lifeMax : -1;
			string row = string.Join("\t", new object[] { key, c.Id, c.WindStart, windTicks, c.Launch, c.End, LvF(c.Boss0.X / 16f), LvF(c.Boss0.Y / 16f), LvF(c.Dir.X), LvF(c.Dir.Y), LvF(c.Speed), c.DashNo, c.AimIdx, LvF(c.MinPerp), c.AliveAtLaunch, inLane, c.FieldSeenDt, c.Deaths, c.Hits, c.HitDmg, st.Levies.Sum(x => Main.npc[x.Idx].ModNPC is SoldierNPC q ? q.LaneEscapes : 0) + "/" + st.Levies.Sum(x => Main.npc[x.Idx].ModNPC is SoldierNPC q ? q.LaneNoPlan : 0) + "/" + st.Levies.Sum(x => Main.npc[x.Idx].ModNPC is SoldierNPC q ? q.LaneTicks : 0), LvF(bl) });
			try { File.AppendAllText(_crOut + ".levi.tsv", row + "\n"); } catch { }
			foreach (var lv in c.Lv)
			{
				if (!lv.InLane && !lv.Hit) continue;
				NPC n = Main.npc[lv.Idx]; SoldierNPC sn = n.active ? n.ModNPC as SoldierNPC : null;
				int man = sn != null ? sn.DodgeManeuvers - lv.Man0 : -1;
				Vector2 d20 = lv.P20 == default ? Vector2.Zero : lv.P20 - lv.P0, d45 = lv.P45 == default ? Vector2.Zero : lv.P45 - lv.P0, dE = lv.PEnd == default ? Vector2.Zero : lv.PEnd - lv.P0;
				string line = string.Join("\t", new object[] { key, c.Id, lv.Idx, lv.Cls, LvF(lv.Along / 16f), LvF(lv.Perp / 16f), lv.InLane ? 1 : 0, lv.Wings ? 1 : 0, lv.Dash ? 1 : 0, lv.Grounded0 ? 1 : 0, LvF(lv.V0.X), LvF(lv.V0.Y), lv.ManWind, man,
					sn != null ? sn.DodgeSteps - lv.Step0 : -1, sn != null ? sn.DodgeJumps - lv.Jump0 : -1, sn != null ? sn.DodgeDashes - lv.Dash0 : -1, sn != null ? sn.DodgeDrops - lv.Drop0 : -1, sn != null ? sn.DodgeDoubleJumps - lv.Dj0 : -1, sn != null ? sn.DodgeHooks - lv.Hook0 : -1, sn != null ? sn.DodgeAirManeuvers - lv.Air0 : -1,
					lv.PWind == default ? "-" : LvF(Vector2.Dot(lv.PWind - c.Boss0, new Vector2(-c.Dir.Y, c.Dir.X)) / 16f), lv.FieldSeenDt, sn != null ? sn.LaneEscapes - lv.Lane0 : -1, LvF(d20.X / 16f), LvF(d20.Y / 16f), LvF(d45.X / 16f), LvF(d45.Y / 16f), LvF(dE.X / 16f), LvF(dE.Y / 16f), lv.Hit ? 1 : 0, lv.FirstHitDt, lv.HitDmg, lv.Died ? 1 : 0 });
				try { File.AppendAllText(_crOut + ".levilv.tsv", line + "\n"); } catch { }
			}
		}

		/// <summary>Per tick: a levy that lost life to the Leviathan or Anahita is one row; a dash it belongs to is the open one (or one that ended under 25 ticks ago: the body is long and the boss brakes slowly).</summary>
		private void LvHits(CrState st, NPC levi, string key, int tick)
		{
			foreach (var l in st.Levies)
			{
				NPC n = Main.npc[l.Idx];
				if (!_lvLife.TryGetValue(l.Idx, out int prev)) prev = n.lifeMax;
				int now = l.Dead || !n.active || n.life <= 0 ? 0 : n.life;
				if (now < prev)
				{
					var sn = n.ModNPC as SoldierNPC;
					string att = sn == null ? "?" : (sn.LastAttackerType > 0 ? CrNpcName(sn.LastAttackerType) : "env") + "/" + (sn.LastAttackerName ?? "");
					bool leviHit = sn != null && sn.LastAttackerType == _lvLeviType;
					LvCharge c = _lvCur; int cid = -1, dtl = -1;
					if (leviHit && c != null) { cid = c.Id; dtl = tick - c.Launch; }
					else if (leviHit && tick - _lvLastChargeEnd <= 25) { cid = _lvLastChargeId; }
					if (leviHit && c != null) { c.Hits++; c.HitDmg += prev - now; var lv = c.Lv.FirstOrDefault(x => x.Idx == l.Idx); if (lv != null) { if (!lv.Hit) lv.FirstHitDt = dtl; lv.Hit = true; lv.HitDmg += prev - now; if (now == 0) lv.Died = true; } if (now == 0) c.Deaths++; }
					float perp = float.NaN; if (c != null) perp = Vector2.Dot(n.Center - c.Boss0, new Vector2(-c.Dir.Y, c.Dir.X)) / 16f;
					try { File.AppendAllText(_crOut + ".levihit.tsv", string.Join("\t", new object[] { key, tick, l.Idx, l.Cls, prev - now, prev, now, att, cid, dtl, now == 0 ? 1 : 0, n.velocity.Y == 0f ? 1 : 0, n.wet ? 1 : 0, LvF(n.velocity.Y), LvF(perp) }) + "\n"); } catch { }
				}
				_lvLife[l.Idx] = now;
			}
			// the field-during-wind counter and the dodge counters inside the line-up of the next dash
			if (_lvWinding && levi != null && tick % 6 == 0) { }
		}

		private void LeviReset() { _lvCur = null; _lvChargeId = 0; _lvLastAi0 = _lvLastAi1 = -1; _lvWinding = false; _lvWindStart = -1; _lvLife.Clear(); _lvLastChargeEnd = -1000; _lvLastChargeId = -1; _lvWindSeen = _lvWindNotSeen = 0; _lvWind0.Clear(); }
	}
}
