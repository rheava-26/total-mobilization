using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// PVP LAB (measurement only; levy-mod/lab/pvp). A calrev line `fight pvp MODE SEED CAP lv=A.json lvb=B.json ta=NAME tb=NAME order=sd arena=a3` puts two lab-kit armies in the arena, army A on the left
	/// (team 1, spawned first) and army B on the right (team 2), and lets them fight until one is wiped or the cap runs out. Both armies are the stand-in commander's levies: the same brain, gear rules,
	/// stations, buffs, potions (the harness's drink rule), healing and damage intake. <see cref="PvpLab"/> makes each side the other's enemy. The commander stands at the arena centre, invulnerable
	/// under the Spectator buff. No boss. Every hit one team lands on the other is credited to the levy that fired it (projectiles, children, minions, swings). With LEVY_REC_DIR set the fight is
	/// recorded (both armies are levies, so names and gear are in the file; names carry the army label). Rows: LEVY_CR_OUT (one calrev row, result A / B / DRAW) and LEVY_CR_OUT.pvp.tsv.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class PvState
		{
			public CrRun Run; public CrState St; public List<CrLevy> A = new(), B = new();
			public Dictionary<int, CrLevy> ByIdx = new(); public Dictionary<CrLevy, int> TeamOfLevy = new();
			public Dictionary<CrLevy, long> Dealt = new(); public long[] Unattr = new long[3], Dot = new long[3]; public Dictionary<string, long> Kind = new();
			public int Tick, EndTick = -1; public string Result = ""; public bool Recording;
			// staggered format (pf=N): N levies of each side on the field, the rest wait in a pool and enter one by one at the side's spawn points
			public int Field; public List<(CrLevySpec spec, int k)> PoolA = new(), PoolB = new(); public int[] NextSpawn = new int[3];
			public Dictionary<CrLevy, int> Entry = new(), Kills = new(); public string[] Label = new string[3];
			public int[] StealthThrown = new int[3], StealthHits = new int[3], StealthKills = new int[3], StealthMax = new int[3]; public long[] StealthDmg = new long[3]; // Calamity stealth strikes (and their children) by army
		}
		private static PvState _pvS;
		private static Projectile _pvProj;

		private void StepPvp(CrRun run)
		{
			if (!_crStarted)
			{
				ClearAll(); CrPurgeNpcs(); PvpLab.Reset();
				CrApplyMode(run.Mode); CrDrGlobal.Dr = run.Dr; _crRunCur = run;
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
				_crCmdPos = new Vector2(_crCx * 16f, (run.Arena == "pvpbig" ? CrF - 48 : CrF) * 16f); // the commander stands at the arena centre, between the armies (above the top level in the big arena)
				CrPinCommander();
				// the whole arena counts as watched (a player would be looking at her army): no levy takes the off-screen hop across it
				global::Levy.Common.Movement.OffscreenHop.LabViewers = new List<Vector2>();
				for (int vx = -150; vx <= 150; vx += 75) { global::Levy.Common.Movement.OffscreenHop.LabViewers.Add(new Vector2((_crCx + vx) * 16f, (CrF - 10) * 16f)); global::Levy.Common.Movement.OffscreenHop.LabViewers.Add(new Vector2((_crCx + vx) * 16f, (CrF - 40) * 16f)); }
				// and no Search & Destroy catch-up hop to the commander: a server's assumed screen is widened to the whole arena for the fight
				_pvScreen = (Main.screenWidth, Main.screenHeight); Main.screenWidth = 12000; Main.screenHeight = 4000;
				CrSetHpScale(run); CrSetFear(run); CrSetOptions(run);
				if (run.L3) Lab3Stations(run);
				if (run.Arena == "pvpbig") PvArenaFloor(); // the stations clear the low floor; its cover goes back where no station stands
				if (run.Dl != "") DlBegin(run);
				// champions (grade C in an army file): the config's Champion slots for the fight, from the army file's champSlots (Eve's world: 5 + 1 milestone = 6)
				int cs = Math.Max(run.Lv?.champSlots ?? 0, run.LvB?.champSlots ?? 0);
				if (cs > 0) { _pvChampSaved = global::Levy.Common.Config.LevyConfig.Get.ChampionAccessorySlots; global::Levy.Common.Config.LevyConfig.Get.ChampionAccessorySlots = cs; }
				if (PvIProbe(run)) return;
				var specsB = KitSpecs(run.LvB);
				int total = run.Specs.Count + specsB.Count;
				if (total > global::Levy.Common.Config.LevyConfig.Get.RenderCap) global::Levy.Common.Config.LevyConfig.Get.RenderCap = total;
				var s = new PvState { Run = run, St = new CrState { Run = run, BossIdx = Main.maxNPCs, BossType = -1 } };
				PvpLab.On = true;
				s.Label[1] = run.TeamA; s.Label[2] = run.TeamB;
				if (run.PvField > 0)
				{
					// staggered: each pool in a seeded order (the same seed shuffles both pools independently), spawn points rotate from a seeded start
					s.Field = run.PvField;
					var ra = new Random(run.Seed * 7919 + 101); var rb = new Random(run.Seed * 7919 + 202);
					s.PoolA = run.Specs.Select((sp, k) => (sp, k)).OrderBy(_ => ra.Next()).ToList();
					s.PoolB = specsB.Select((sp, k) => (sp, k)).OrderBy(_ => rb.Next()).ToList();
					s.NextSpawn[1] = run.Seed % 7; s.NextSpawn[2] = run.Seed % 7;
					for (int q = 0; q < s.Field; q++) { PvEnter(s, run, 1); PvEnter(s, run, 2); }
				}
				else
				{
					PvSpawn(s, run, run.Specs, 1, run.TeamA, s.A);
					PvSpawn(s, run, specsB, 2, run.TeamB, s.B);
				}
				s.St.Levies.Clear(); s.St.Levies.AddRange(s.A); s.St.Levies.AddRange(s.B);
				PvNoCatchUp(s);
				if (run.L3) Lab3Begin(run, s.St);
				_pvS = s; _crStarted = true; _crTickInRun = 0;
				PvStartRecording(s, run);
				Mod.Logger.Info($"HARNESS-PVP: start {run.Key}: {run.TeamA} x{s.A.Count} (left, team 1) vs {run.TeamB} x{s.B.Count} (right, team 2), mode {run.Mode}, order {run.Order}, arena {run.Arena}, cap {run.Cap / 60}s, recording={s.Recording}");
				return;
			}
			var st = _pvS; st.Tick = ++_crTickInRun; st.St.Tick = st.Tick;
			CrPinCommander();
			// the commander's own facing still reaches the lashes outside the per-levy scopes (see PvFaceOwner): it turns every tick, so each army has it half the time. LEVY_PVP_CMDDIR=1|-1 pins it (lab probe).
			Main.player[0].direction = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PVP_CMDDIR"), out int cdir) && cdir != 0 ? cdir : (st.Tick & 1) == 0 ? 1 : -1;
			CrPinStrays(-1);
			int aliveA = PvTickArmy(st, run, st.A), aliveB = PvTickArmy(st, run, st.B);
			PvNoCatchUp(st);
			if (st.Field > 0 && st.Result == "")
			{
				// the next levy of a side enters the moment one of its levies on the field falls; a side with nobody on the field and an empty pool has lost
				while (aliveA < st.Field && st.PoolA.Count > 0) { PvEnter(st, run, 1); aliveA++; }
				while (aliveB < st.Field && st.PoolB.Count > 0) { PvEnter(st, run, 2); aliveB++; }
				PvAnchors(st);
				if (st.Tick >= run.Cap && aliveA > 0 && aliveB > 0)
				{
					int mA = aliveA + st.PoolA.Count, mB = aliveB + st.PoolB.Count; // cap: the side with more manpower left wins
					st.Result = (mA != mB ? (mA > mB ? "A" : "B") : PvHpShare(st.A) >= PvHpShare(st.B) ? "A" : "B") + "-TIMEOUT"; st.EndTick = st.Tick;
				}
			}
			if (st.Result == "")
			{
				if (aliveA == 0 || aliveB == 0) { st.Result = aliveA > 0 ? "A" : aliveB > 0 ? "B" : "DRAW"; st.EndTick = st.Tick; }
				else if (st.Tick >= run.Cap)
				{
					// cap: the army with more levies standing wins; a tie goes to the larger share of life left
					double hpA = PvHpShare(st.A), hpB = PvHpShare(st.B);
					st.Result = aliveA != aliveB ? (aliveA > aliveB ? "A" : "B") : Math.Abs(hpA - hpB) < 1e-6 ? "DRAW" : hpA > hpB ? "A" : "B";
					st.Result += "-TIMEOUT"; st.EndTick = st.Tick;
				}
			}
			if (st.Result != "" && st.Tick >= st.EndTick + 30) PvFinish(run, st); // a second of recording past the last death
		}

		private void PvSpawn(PvState s, CrRun run, List<CrLevySpec> specs, int team, string label, List<CrLevy> into)
		{
			int side = (team == 1 ? -1 : 1) * (Environment.GetEnvironmentVariable("LEVY_PVP_FLIP") == "1" ? -1 : 1); // LEVY_PVP_FLIP=1: army A (team 1, spawned first) on the right
			for (int k = 0; k < specs.Count; k++)
			{
				var l = CrSpawnLevy(specs[k], run, k, specs.Count, new Vector2((_crCx + side * 70) * 16f, CrF * 16f));
				l.Rec.Name = PvName(label, specs[k], k);
				NPC n = Main.npc[l.Idx];
				n.Bottom = new Vector2((_crCx + side * (35f + 25f * k / Math.Max(1, specs.Count - 1))) * 16f, CrF * 16f); n.velocity = Vector2.Zero; // 35-60 tiles out from the centre (inside Search & Destroy's 60-tile catch-up), the two fronts 70 tiles apart
				n.direction = -side;
				PvpLab.Add(l.Idx, team);
				s.ByIdx[l.Idx] = l; s.TeamOfLevy[l] = team; s.Dealt[l] = 0; into.Add(l);
			}
		}

		private static (int w, int h) _pvScreen;
		private static void PvScreenRestore() { if (_pvScreen.w > 0 || _pvScreen.h > 0) { Main.screenWidth = _pvScreen.w; Main.screenHeight = _pvScreen.h; _pvScreen = (0, 0); } }

		// -------------------------------------------------------------------------------------------------- the big arena (arena=pvpbig)
		// 350 tiles wide (walls at +-176), mirrored left / right: a floor with mounds and pillars, and three levels above it (12, 24 and 36 tiles up) of solid ledges and platform bridges with cover blocks,
		// gaps to drop through and platforms to jump up through. Seven spawn points a side: two on the floor and two, two and one on the levels (dx from the centre, level).
		private const int PvHalf = 175, PvL1 = 12, PvL2 = 24, PvL3 = 36;
		private static readonly (int dx, int lv)[] PvSpawns = { (165, 0), (112, 0), (120, PvL1), (50, PvL1), (160, PvL2), (80, PvL2), (140, PvL3) };

		private void PvM(int dx0, int dx1, int y0, int y1, bool solid) { FaFill(_crCx + dx0, y0, _crCx + dx1, y1, solid); FaFill(_crCx - dx1, y0, _crCx - dx0, y1, solid); }
		private static void PvPlat(int cx, int dx0, int dx1, int y)
		{
			for (int dx = dx0; dx <= dx1; dx++)
				foreach (int x in dx == 0 ? new[] { cx } : new[] { cx + dx, cx - dx })
				{
					Tile t = Main.tile[x, y];
					t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.WallType = 0; t.LiquidAmount = 0;
				}
		}

		private void PvBuildArena()
		{
			float n = Main.maxTilesX / 4200f; n *= n;
			int band = (int)(60f + 10f * n + Main.worldSurface / 6.0) + 2;
			_crCx = _cx; int cx = _cx, fy = Math.Clamp(band + 75, 120, Main.maxTilesY - 120);
			CrF = fy; CombatHarnessFloor = fy;
			FaFill(cx - PvHalf - 20, fy - 110, cx + PvHalf + 20, fy - 1, false);
			FaFill(cx - PvHalf - 20, fy, cx + PvHalf + 20, fy + 25, true);
			FaFill(cx - PvHalf - 1, fy - 110, cx - PvHalf - 1, fy + 25, true);
			FaFill(cx + PvHalf + 1, fy - 110, cx + PvHalf + 1, fy + 25, true);
			int y1 = fy - PvL1, y2 = fy - PvL2, y3 = fy - PvL3;
			// level 1: two solid ledges a side, platform bridges between them and over the centre
			PvM(20, 60, y1, y1 + 1, true); PvM(85, 130, y1, y1 + 1, true); PvPlat(cx, 0, 19, y1); PvPlat(cx, 61, 84, y1);
			PvM(40, 41, y1 - 3, y1 - 1, true); PvM(110, 111, y1 - 3, y1 - 1, true);
			// level 2: a ledge in the middle of each half and one against each wall; a platform bridge over the centre with a hole in its middle, and one between the ledges
			PvM(45, 95, y2, y2 + 1, true); PvM(140, PvHalf, y2, y2 + 1, true); PvPlat(cx, 10, 44, y2); PvPlat(cx, 96, 139, y2);
			PvM(70, 71, y2 - 3, y2 - 1, true); PvM(155, 156, y2 - 3, y2 - 1, true);
			// level 3: a platform walk near the centre and a solid ledge with cover further out
			PvPlat(cx, 25, 70, y3); PvM(110, 150, y3, y3 + 1, true); PvM(130, 131, y3 - 3, y3 - 1, true);
			// paths: platform stairs from the floor up to level 1 and from level 1 up to level 2, near each wall
			for (int k = 1; k <= 5; k++) { PvPlat(cx, 132 + 3 * k, 134 + 3 * k, fy - 2 * k); PvPlat(cx, 131 + 2 * k, 133 + 2 * k, y1 - 2 * k); }
			PvArenaFloor();
			_crArenaBuilt = "pvpbig";
			Mod.Logger.Info($"HARNESS-PVP: big arena built: floor row {fy}, half-width {PvHalf}, levels at {PvL1}/{PvL2}/{PvL3} tiles, {PvSpawns.Length} spawn points a side");
		}

		/// <summary>Floor cover (mounds and pillars), kept clear of the station groups (x from the centre: -90..-50, -15..25, 60..100).</summary>
		private void PvArenaFloor()
		{
			int fy = CrF;
			foreach (int a in new[] { 34, 120, 158 }) { PvM(a, a + 12, fy - 1, fy - 1, true); PvM(a + 2, a + 10, fy - 2, fy - 2, true); PvM(a + 4, a + 8, fy - 3, fy - 3, true); }
			foreach (int a in new[] { 28, 105, 150 }) PvM(a, a + 1, fy - 4, fy - 1, true);
		}

		/// <summary>The next levy of a side's pool walks onto the field at the side's next spawn point.</summary>
		private void PvEnter(PvState s, CrRun run, int team)
		{
			var pool = team == 1 ? s.PoolA : s.PoolB;
			if (pool.Count == 0) return;
			var (spec, k) = pool[0]; pool.RemoveAt(0);
			int side = (team == 1 ? -1 : 1) * (Environment.GetEnvironmentVariable("LEVY_PVP_FLIP") == "1" ? -1 : 1);
			var sp = PvSpawns[s.NextSpawn[team]++ % PvSpawns.Length];
			var at = new Vector2((_crCx + side * sp.dx) * 16f, (CrF - sp.lv) * 16f);
			var l = CrSpawnLevy(spec, run, k, 1, at);
			l.Rec.Name = PvName(s.Label[team], spec, k);
			NPC n = Main.npc[l.Idx]; n.Bottom = at; n.velocity = Vector2.Zero; n.direction = -side;
			PvpLab.Add(l.Idx, team);
			s.ByIdx[l.Idx] = l; s.TeamOfLevy[l] = team; s.Dealt[l] = 0; s.Entry[l] = s.Tick;
			(team == 1 ? s.A : s.B).Add(l); s.St.Levies.Add(l);
			PvAnchors(s);
			PvNoCatchUp(s);
		}

		private static System.Reflection.FieldInfo _pvTeleF;
		/// <summary>
		/// No catch-up hop to the commander. Search &amp; Destroy (and Follow) hop a levy that is over 60 tiles from the commander and out of every player's sight to her side (SoldierNPC.CatchUpTeleport); in the lab the
		/// commander is a spectator at the arena centre, so that hop dropped levies of both armies into the middle of the fight (3182 hops in 40 big-arena recordings, every one landing on the same two spots near her).
		/// The hop's cooldown is kept running for every PvP levy, every tick.
		/// </summary>
		private static void PvNoCatchUp(PvState s)
		{
			_pvTeleF ??= typeof(SoldierNPC).GetField("_lastTeleportTick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			if (_pvTeleF == null) return;
			int now = (int)Main.GameUpdateCount;
			foreach (var l in s.A.Concat(s.B)) if (!l.Dead && Main.npc[l.Idx].ModNPC is SoldierNPC sn) _pvTeleF.SetValue(sn, now);
		}

		private static System.Reflection.FieldInfo _pvAnchorF, _pvAnchorSetF;
		/// <summary>Search &amp; Destroy roams around a levy's first position; in the big arena every levy roams around the arena centre instead (its 120-tile leash reaches every spawn point).</summary>
		private void PvAnchors(PvState s)
		{
			if (s.Run.Arena != "pvpbig") return;
			_pvAnchorF ??= typeof(SoldierNPC).GetField("_searchAnchor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			_pvAnchorSetF ??= typeof(SoldierNPC).GetField("_searchAnchorSet", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			var c = new Vector2(_crCx * 16f, (CrF - PvL1) * 16f);
			foreach (var l in s.A.Concat(s.B))
			{
				if (l.Dead || !(Main.npc[l.Idx].ModNPC is SoldierNPC sn)) continue;
				_pvAnchorF?.SetValue(sn, c); _pvAnchorSetF?.SetValue(sn, true);
			}
		}

		/// <summary>A levy's name in the fight and its recording: the real name the army file carries (Eve's levies), else "Opt. Ranger 3" for an optimal army, else "&lt;army&gt; &lt;class&gt; &lt;n&gt;".</summary>
		private static string PvName(string label, CrLevySpec spec, int k)
		{
			if (!string.IsNullOrWhiteSpace(spec.Name)) return spec.Name;
			string cls = spec.Cls switch { 'E' => "Melee", 'R' => "Ranger", 'M' => "Mage", 'S' => "Summoner", 'T' => "Rogue", _ => spec.Cls.ToString() };
			return (label.StartsWith("opt") ? "Opt." : label) + " " + cls + " " + (k + 1);
		}

		private static int _pvChampSaved = -1;

		/// <summary>
		/// LEVY_PVP_IPROBE="Acc1+Acc2+..." (use _ for spaces; "none" = a bare levy): the hit-window probe. For each accessory, one levy wearing only it (army A's first loadout otherwise) takes one 100-damage hit through the levy's own
		/// enemy-hit intake (ReceiveHostileMelee: the same AfterHit the PvP lab and an enemy's blade use) and the next tick; the immune ticks it got are logged and written to LEVY_CR_OUT.iprobe.tsv. No fight.
		/// </summary>
		private bool PvIProbe(CrRun run)
		{
			string list = Environment.GetEnvironmentVariable("LEVY_PVP_IPROBE");
			if (string.IsNullOrEmpty(list)) return false;
			var baseSpec = run.Specs[0];
			var rows = new List<string>();
			var att = CrSpawnLevy(baseSpec, run, 0, 1, new Vector2((_crCx + 20) * 16f, CrF * 16f)); // the attacker body (only its slot is used)
			int k = 1;
			foreach (string raw in list.Split('+'))
			{
				string acc = raw.Replace('_', ' ');
				var spec = new CrLevySpec { Cls = baseSpec.Cls, Weapon = baseSpec.Weapon, Ammo = baseSpec.Ammo, Armor = baseSpec.Armor, Acc = acc == "none" ? Array.Empty<string>() : new[] { acc }, LifeCrystals = baseSpec.LifeCrystals, ManaCrystals = baseSpec.ManaCrystals, Potions = 0, Potion = baseSpec.Potion, LifeFruit = baseSpec.LifeFruit };
				string got;
				try
				{
					var l = CrSpawnLevy(spec, run, k++, 1, new Vector2((_crCx - 20 - 3 * k) * 16f, CrF * 16f));
					var sn = (SoldierNPC)Main.npc[l.Idx].ModNPC; sn.StatsDirty = true; sn.RefreshStats();
					bool longI = sn.Dummy != null && sn.Dummy.longInvince;
					sn.ImmuneTime = 0; sn.AllImmune = 0;
					int done = sn.ReceiveHostileMelee(Main.npc[att.Idx], 100, 1);
					int slotMax = 0; foreach (int v in sn.SlotImmune) slotMax = Math.Max(slotMax, v);
					got = $"{acc}\tlongInvince={(longI ? 1 : 0)}\tdamage={done}\timmuneTicks={sn.ImmuneTime}\tallImmune={sn.AllImmune}\tslotMax={slotMax}\tdummyImmuneTime={(sn.Dummy?.immuneTime ?? -1)}\tworn={string.Join("+", l.Rec.Gear.AllItems().Where(it => it != null && !it.IsAir && it.accessory).Select(it => it.Name))}";
				}
				catch (Exception e) { got = acc + "\terror " + e.Message; }
				rows.Add(got); Mod.Logger.Info("HARNESS-PVP: iprobe " + got.Replace('\t', ' '));
			}
			try { File.AppendAllText(_crOut + ".iprobe.tsv", string.Join("\n", rows) + "\n"); } catch { }
			var cols = CrHeader.Split('\t'); var v0 = new string[cols.Length]; for (int i = 0; i < v0.Length; i++) v0[i] = "0";
			v0[0] = run.Key; v0[1] = run.Kind; v0[2] = "pvp"; v0[3] = run.Mode; v0[4] = run.Order; v0[5] = run.Seed.ToString(CultureInfo.InvariantCulture); v0[6] = "PROBE";
			File.AppendAllText(_crOut, string.Join("\t", v0) + "\n");
			if (run.Dl != "") DlEnd();
			if (_pvChampSaved >= 0) { global::Levy.Common.Config.LevyConfig.Get.ChampionAccessorySlots = _pvChampSaved; _pvChampSaved = -1; }
			PvpLab.Reset(); global::Levy.Common.Movement.OffscreenHop.LabViewers = null; PvScreenRestore();
			_crStarted = false; _crI++; ClearAll(); CrPurgeNpcs();
			return true;
		}

		private static void PvNoteStrikes(int team, int n) { if (_pvS != null && team > 0 && team < 3) _pvS.StealthThrown[team] += n; }

		private static double PvHpShare(List<CrLevy> army)
		{
			double left = 0, max = 0;
			foreach (var l in army) { max += l.MaxHp; if (!l.Dead) { NPC n = Main.npc[l.Idx]; if (n.active) left += Math.Max(0, n.life); } }
			return max <= 0 ? 0 : left / max;
		}

		/// <summary>One tick of one army: the harness drink rule, the stations / buffs tick, life lost, deaths. Returns the levies standing.</summary>
		private int PvTickArmy(PvState st, CrRun run, List<CrLevy> army)
		{
			int alive = 0;
			foreach (var l in army)
			{
				if (l.Dead) continue;
				NPC n = Main.npc[l.Idx];
				bool body = l.Rec.HasBody && n.active && n.ModNPC is SoldierNPC sn0 && ReferenceEquals(sn0.Record, l.Rec) && n.life > 0;
				if (body)
				{
					var sn = (SoldierNPC)n.ModNPC;
					if (!run.L3) sn.Stations = StationFlags.Campfire | StationFlags.HeartLantern; else Lab3Tick(run, st.St, l, sn, n);
					if (l.PotionsLeft > 0 && sn.PotionSickness == 0 && n.life * 100 < n.lifeMax * LevyConstants.PotionDrinkBelowPercent)
					{
						Item pot = CrMake(l.Spec.Potion);
						n.life = Math.Min(n.lifeMax, n.life + pot.healLife); l.Rec.CurrentHp = n.life; sn.PotionSickness = LevyConstants.PotionSicknessTicks; l.PotionsLeft--; l.PotionsDrunk++;
						l.LastLife = n.life;
					}
					int drop = l.LastLife - n.life;
					if (drop > 0) { l.Hits++; l.TakenOther += drop; }
					l.BodyTicks++; l.DefSum += n.defense; if (sn.State == SoldierState.Shaken) l.ShakenTicks++; else if (sn.State == SoldierState.Routed) l.RoutedTicks++;
					l.LastLife = n.life; l.LifeMin = Math.Min(l.LifeMin, n.life);
					alive++;
				}
				else
				{
					l.Dead = true; l.DeathTick = st.Tick; l.TakenOther += Math.Max(0, l.LastLife);
					l.Rec.RespawnTimer = 1_000_000_000; // no respawn inside the lab
					PvpLab.Remove(l.Idx); st.ByIdx.Remove(l.Idx);
				}
			}
			return alive;
		}

		/// <summary>
		/// The owner's facing. Every levy shot is owned by the commander's player slot, and the commander's facing changes what a levy's shots and lashes do: with her facing right the army on the right
		/// (shooting left) out-damaged the army on the left 2-3 to 1 in identical mirrors and won 10 of 12; facing left, the left army won 6 of 6. So for the length of each levy's update and each of its
		/// shots' updates the commander faces the way that levy faces, as a player faces the way she shoots. LEVY_PVP_OWNERDIR=0 leaves her facing alone (the biased lab).
		/// </summary>
		internal static readonly bool PvOwnerDir = Environment.GetEnvironmentVariable("LEVY_PVP_OWNERDIR") != "0";
		private static int PvFaceOwner(NPC shooter)
		{
			if (!PvOwnerDir || shooter == null || !(shooter.ModNPC is SoldierNPC)) return 0;
			Player o = Main.player[SoldierMinions.TrueMyPlayer >= 0 && SoldierMinions.TrueMyPlayer < Main.maxPlayers ? SoldierMinions.TrueMyPlayer : 0];
			int prev = o.direction; o.direction = shooter.direction == 0 ? 1 : shooter.direction;
			return prev == 0 ? 2 : prev; // 0 = nothing to put back
		}
		private static void PvFaceRestore(int prev)
		{
			if (prev == 0) return;
			Player o = Main.player[SoldierMinions.TrueMyPlayer >= 0 && SoldierMinions.TrueMyPlayer < Main.maxPlayers ? SoldierMinions.TrueMyPlayer : 0];
			o.direction = prev == 2 ? 0 : prev;
		}

		/// <summary>LEVY_PVP_IFRAMES=0: the other army's hits land as on any NPC (vanilla per-shot immunity only). On (default): a levy hit by the other army gets the levy's own player-style hit window, as from an enemy's hit (SoldierNPC.AfterHit: immune ticks, fear, barks, hurt-driven gear).</summary>
		internal static readonly bool PvIFrames = Environment.GetEnvironmentVariable("LEVY_PVP_IFRAMES") != "0";
		private static System.Reflection.MethodInfo _pvAfterHit;

		/// <summary>The strike detour for a hit on a team body: the player-style hit window, the strike, the credit, the levy's own after-hit bookkeeping.</summary>
		private static int PvStrike(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, SoldierNPC victim, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{
			int src = _crProjDepth > 0 ? PvpLab.ProjSoldier(_pvProj) : _crNpcCtx;
			int vt = PvpLab.TeamOf(self.whoAmI), at = PvpLab.TeamOf(src);
			bool enemy = at != 0 && at != vt;
			if (enemy && PvIFrames && victim.ImmuneTime > 0) return 0; // inside the window a hit from the other army does nothing (a projectile is stopped earlier, in PvpImmunity)
			int done = orig(self, hit, fromNet, noPlayerInteraction);
			PvCredit(self, done, src);
			if (enemy && done > 0 && !victim.IsAlive && _pvS != null && _pvS.ByIdx.TryGetValue(src, out var killer)) { _pvS.Kills.TryGetValue(killer, out int kn); _pvS.Kills[killer] = kn + 1; }
			if (enemy && done > 0 && _pvS != null && _crProjDepth > 0 && _pvProj != null)
			{
				bool ss = false; try { ss = HarnessCalamityProbe.IsStealthStrike(_pvProj, out bool sub) || sub; } catch { }
				if (ss) { _pvS.StealthHits[at]++; _pvS.StealthDmg[at] += done; _pvS.StealthMax[at] = Math.Max(_pvS.StealthMax[at], done); if (!victim.IsAlive) _pvS.StealthKills[at]++; }
			}
			if (enemy && PvIFrames && done > 0)
			{
				try
				{
					_pvAfterHit ??= typeof(SoldierNPC).GetMethod("AfterHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
					_pvAfterHit.Invoke(victim, new object[] { done, src, -1 });
				}
				catch (Exception e) { global::Levy.Levy.Instance.Logger.Warn("HARNESS-PVP: AfterHit failed: " + e.Message); victim.ImmuneTime = victim.HitImmuneTicks(done); }
			}
			return done;
		}

		/// <summary>Strike detour: a hit on a team body. <paramref name="src"/> is the soldier slot that caused it (the projectile's soldier, or the soldier whose update swung), -1 unknown.</summary>
		private static void PvCredit(NPC victim, int done, int src)
		{
			var st = _pvS; if (st == null || done <= 0) return;
			int vt = PvpLab.TeamOf(victim.whoAmI); if (vt == 0) return;
			if (src >= 0 && st.ByIdx.TryGetValue(src, out var l) && st.TeamOfLevy[l] != vt)
			{
				st.Dealt[l] += done;
				var pj = _crProjDepth > 0 ? _pvProj : null; // what landed it: a lash, a minion (or its shot), another shot, or the levy's own swing
				string k = (st.TeamOfLevy[l] == 1 ? "A:" : "B:") + (pj == null ? "swing" : global::Levy.Common.Weapons.RealWhips.IsWhipProjectile(pj) ? "whip" : pj.minion || pj.sentry || (pj.type < Terraria.ID.ProjectileID.Sets.MinionShot.Length && Terraria.ID.ProjectileID.Sets.MinionShot[pj.type]) ? "minion" : "shot");
				st.Kind.TryGetValue(k, out long kv); st.Kind[k] = kv + done;
				return;
			}
			if (src >= 0 && PvpLab.TeamOf(src) == vt) st.Dot[vt] += done; // the body's own update (debuff damage, a hazard)
			else st.Unattr[vt] += done;
		}

		private void PvStartRecording(PvState s, CrRun run)
		{
			if (!global::Levy.Common.Recording.FightRecAuto.Enabled) return; // LEVY_REC_DIR unset: no recording
			try
			{
				if (global::Levy.Common.Recording.FightRecorder.On) global::Levy.Common.Recording.FightRecorder.Stop("next fight started");
				global::Levy.Common.Recording.FightRecorder.Headless = true;
				global::Levy.Common.Recording.FightRecorder.OutDirOverride = global::Levy.Common.Recording.FightRecAuto.Dir;
				global::Levy.Common.Recording.FightRecorder.IgnoreInactive = true;
				string ids(List<CrLevy> a) => string.Join(",", a.Select(l => l.Rec.Id));
				var meta = new Dictionary<string, string>
				{
					["harness"] = "calrev", ["pvp"] = "1", ["label"] = run.TeamA + " vs " + run.TeamB, ["teamA"] = run.TeamA, ["teamB"] = run.TeamB, ["sideA"] = "left", ["sideB"] = "right",
					["idsA"] = ids(s.A), ["idsB"] = ids(s.B), ["arena"] = run.Arena, ["seed"] = run.Seed.ToString(CultureInfo.InvariantCulture), ["mode"] = run.Mode, ["order"] = run.Order, ["cellkey"] = run.Key,
					["note"] = "Two levy armies of the same commander fight each other (PvP lab). Levy names start with the army label; the commander stands at the arena centre and takes no part.",
				};
				global::Levy.Common.Recording.FightRecorder.FightLabel = "PvP " + run.TeamA + " vs " + run.TeamB;
				string path = global::Levy.Common.Recording.FightRecorder.Start(Main.player[0], "PvP " + run.TeamA + " vs " + run.TeamB, out string err, meta, $"pvp_{run.TeamA}_vs_{run.TeamB}_s{run.Seed}");
				s.Recording = path != null;
				Mod.Logger.Info("HARNESS-PVP: recording " + (path ?? "failed: " + err));
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-PVP: recording failed: " + e.Message); }
		}

		private void PvFinish(CrRun run, PvState st)
		{
			int end = st.EndTick > 0 ? st.EndTick : st.Tick;
			string f1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
			string Army(List<CrLevy> a) => string.Join(";", a.Select(l => $"{l.Cls}:{l.Weapon.Replace(' ', '_')}:dmg{st.Dealt[l]}:{(l.Dead ? "dead@" + f1(l.DeathTick / 60.0) : "alive")}:hp{l.MaxHp}:pot{l.PotionsDrunk}:in{f1((st.Entry.TryGetValue(l, out int e0) ? e0 : 0) / 60.0)}:k{(st.Kills.TryGetValue(l, out int k0) ? k0 : 0)}"));
			string Cls(List<CrLevy> a) => string.Join(" ", a.GroupBy(l => l.Cls).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Sum(l => st.Dealt[l])));
			string Top(List<CrLevy> a) => string.Join(" ", a.GroupBy(l => l.Weapon).Select(g => (w: g.Key, d: g.Sum(l => st.Dealt[l]), n: g.Count())).OrderByDescending(x => x.d).Take(6).Select(x => x.w.Replace("CalamityMod/", "").Replace(' ', '_') + "x" + x.n + "=" + x.d));
			string Deaths(List<CrLevy> a) => string.Join(",", a.Where(l => l.Dead).Select(l => l.DeathTick / 60.0).OrderBy(x => x).Select(f1));
			int aliveA = st.A.Count(l => !l.Dead), aliveB = st.B.Count(l => !l.Dead);
			long dA = st.A.Sum(l => st.Dealt[l]), dB = st.B.Sum(l => st.Dealt[l]);
			string winner = st.Result.StartsWith("A") ? run.TeamA : st.Result.StartsWith("B") ? run.TeamB : "draw";
			try
			{
				string path = _crOut + ".pvp.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\tseed\tteamA\tteamB\tresult\twinner\tsec\tnA\tnB\taliveA\taliveB\thpLeftA\thpLeftB\tdmgA\tdmgB\tclassA\tclassB\ttopA\ttopB\tdeathsA\tdeathsB\tpotionsA\tpotionsB\ttakenA\ttakenB\tunattrA\tunattrB\tdotA\tdotB\tperA\tperB\tstealthA\tstealthB\n");
				File.AppendAllText(path, string.Join("\t", new object[] { run.Key, run.Seed, run.TeamA, run.TeamB, st.Result, winner, f1(end / 60.0), st.A.Count, st.B.Count, aliveA, aliveB,
					(100 * PvHpShare(st.A)).ToString("0", CultureInfo.InvariantCulture), (100 * PvHpShare(st.B)).ToString("0", CultureInfo.InvariantCulture), dA, dB, Cls(st.A), Cls(st.B), Top(st.A), Top(st.B), Deaths(st.A), Deaths(st.B),
					st.A.Sum(l => l.PotionsDrunk), st.B.Sum(l => l.PotionsDrunk), st.A.Sum(l => l.TakenOther), st.B.Sum(l => l.TakenOther), st.Unattr[1], st.Unattr[2], st.Dot[1], st.Dot[2], Army(st.A), Army(st.B), $"{st.StealthHits[1]}/{st.StealthDmg[1]}/{st.StealthKills[1]}/{st.StealthMax[1]}/{st.StealthThrown[1]}", $"{st.StealthHits[2]}/{st.StealthDmg[2]}/{st.StealthKills[2]}/{st.StealthMax[2]}/{st.StealthThrown[2]}" }) + "\n");
				// the ordinary calrev row, so drivers see the fight as done (result A / B / DRAW, deaths of both armies)
				var cols = CrHeader.Split('\t'); var v = new string[cols.Length]; for (int i = 0; i < v.Length; i++) v[i] = "0";
				void Set(string c, object o) { int i = Array.IndexOf(cols, c); if (i >= 0) v[i] = Convert.ToString(o, CultureInfo.InvariantCulture); }
				Set("key", run.Key); Set("kind", run.Kind); Set("boss", "pvp"); Set("mode", run.Mode); Set("order", run.Order); Set("seed", run.Seed); Set("result", st.Result); Set("ticks", end); Set("sec", f1(end / 60.0));
				Set("dmgBoss", dA); Set("dmgAdd", dB); Set("deaths", st.A.Count(l => l.Dead) + st.B.Count(l => l.Dead)); Set("n", st.A.Count + st.B.Count); Set("arena", run.Arena); Set("gear", run.TeamA + "_vs_" + run.TeamB);
				Set("potionsDrunk", st.A.Sum(l => l.PotionsDrunk) + st.B.Sum(l => l.PotionsDrunk)); Set("perLevy", "aliveA=" + aliveA + " aliveB=" + aliveB);
				File.AppendAllText(_crOut, string.Join("\t", v) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-PVP: row failed: " + e); }
			Mod.Logger.Info($"HARNESS-CR: RESULT {run.Key} -> {st.Result} t={end / 60.0:0.0}s winner={winner} alive {run.TeamA}={aliveA}/{st.A.Count} {run.TeamB}={aliveB}/{st.B.Count} dmg {run.TeamA}={dA} {run.TeamB}={dB} unattr={st.Unattr[1]}/{st.Unattr[2]} dot={st.Dot[1]}/{st.Dot[2]} by={string.Join(",", st.Kind.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value))} stealth hits/dmg/kills/max/thrown A={st.StealthHits[1]}/{st.StealthDmg[1]}/{st.StealthKills[1]}/{st.StealthMax[1]}/{st.StealthThrown[1]} B={st.StealthHits[2]}/{st.StealthDmg[2]}/{st.StealthKills[2]}/{st.StealthMax[2]}/{st.StealthThrown[2]}");
			PvStopRecording(st);
			PvpLab.Reset(); _pvS = null; _pvProj = null; global::Levy.Common.Movement.OffscreenHop.LabViewers = null; PvScreenRestore();
			_crStarted = false; _crI++;
			if (run.Dl != "") DlEnd();
			if (_pvChampSaved >= 0) { global::Levy.Common.Config.LevyConfig.Get.ChampionAccessorySlots = _pvChampSaved; _pvChampSaved = -1; }
			CrSetHpScale(null); CrSetFear(null); CrSetOptions(null);
			ClearAll(); CrPurgeNpcs();
		}

		private static void PvStopRecording(PvState st)
		{
			if (st == null || !st.Recording) return;
			try { global::Levy.Common.Recording.FightRecorder.Stop("fight over"); } catch (Exception e) { global::Levy.Levy.Instance.Logger.Warn("HARNESS-PVP: recorder stop failed: " + e.Message); }
			st.Recording = false;
		}

		/// <summary>A run threw: drop the pvp state so the next line starts clean.</summary>
		private static void PvAbort()
		{
			PvStopRecording(_pvS);
			PvpLab.Reset(); _pvS = null; _pvProj = null; global::Levy.Common.Movement.OffscreenHop.LabViewers = null; PvScreenRestore();
		}
	}

	/// <summary>PvP lab: a projectile of the other army does not hit a levy inside his player-style hit window (the levy's own rule for an enemy's projectile, SoldierProjectileImmunity).</summary>
	public sealed class PvpImmunity : GlobalNPC
	{
		public override bool IsLoadingEnabled(Mod mod) => Environment.GetEnvironmentVariable("LEVY_HARNESS") == "1"; // the harness only: no hook in a game
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<SoldierNPC>();
		public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
		{
			if (!PvpLab.On || !CombatHarness.PvIFrames || !(npc.ModNPC is SoldierNPC s)) return null;
			return s.ImmuneTime > 0 && PvpLab.ShotMayHit(projectile, npc) ? false : null;
		}
	}
}
