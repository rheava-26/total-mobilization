using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.Buffs;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// FIZZY LAB (measurement only; a calrev `fight` line whose tokens carry tp=fz=1;k=v;..., driven by levy-mod/lab/levylab through a `tipsy:` spec, see docs/fizzrogue/).
	/// One levy stands on the arena floor with fall damage ON. Phase 1: `hits` identical blows land on it (the real hostile-melee intake, 12 damage, alternating sides) with a gap between them; per blow the lab records how far the body
	/// travelled, how fast, how high and how long it was airborne. Phase 2: the body is dropped from `drop` tiles up (fall damage test). Phase 3: the commander stands `far` tiles away and the levy must walk back to him (stuck test).
	///   arm=new|old  new = the Fizzy buff; old = the pre-rework Fizzy (knockback resistance 0.75, normal gravity, the movement code never suspended)   kb=none|cobalt  (Cobalt Shield on the levy: knockback immunity)
	///   hits=N  gap=ticks  win=ticks measured per blow  drop=tiles  far=tiles  walk=ticks.  Rows go to LEVY_CR_OUT.tipsy.tsv.
	/// </summary>
	public partial class CombatHarness
	{
		private int _fzPhase, _fzTick, _fzHit, _fzDummyNpc = -1; private CrLevy _fzLevy; private bool _fzStarted;
		private Vector2 _fzP0; private float _fzDx, _fzDy, _fzV; private int _fzAir, _fzSettle; private bool _fzSettled;
		private readonly List<float> _fzDxs = new(), _fzDys = new(), _fzVs = new(), _fzAirs = new(), _fzSettles = new();
		private int _fzHp0, _fzFallDmg0, _fzFallHits0, _fzDeaths, _fzFlights0; private float _fzDropMinGrav = 1f; private float _fzGravSample; private float _fzWalkDist0, _fzWalkPath; private Vector2 _fzPrevPos;
		private float _fzDropTop; private int _fzDropHp0; private bool _fzDropped; private float _fzFarX;
		private int _fzStillRun, _fzStillMax;

		private void StepFizz(CrRun run)
		{
			var kv = BtKv(run.Tp);
			string G(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
			float F(string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : d;
			int hits = (int)F("hits", 8), gap = (int)F("gap", 150), win = (int)F("win", 300), walk = (int)F("walk", 900); float dropT = F("drop", 55f), farT = F("far", 45f);
			bool neu = G("arm", "new") == "new"; string kb = G("kb", "none");
			float x0 = _crCx - 30f;
			if (!_fzStarted)
			{
				ClearAll(); CrPurgeNpcs(); CrApplyMode("exp"); _crRunCur = run;
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
				_crCmdPos = new Vector2((x0 - 3f) * 16f, CrF * 16f);
				CrPinCommander();
				var spec = new CrLevySpec { Cls = 'R', Weapon = "Minishark", Ammo = "Musket Ball", LifeCrystals = 15, ManaCrystals = 9, Potions = 0, Potion = "Healing Potion", ManAtArms = false };
				if (kb == "cobalt") spec.Acc = new[] { "Cobalt Shield" };
				_fzLevy = CrSpawnLevy(spec, run, 0, 1, new Vector2(x0 * 16f, CrF * 16f));
				NPC ln = Main.npc[_fzLevy.Idx];
				// a harmless friendly "attacker" the hits are credited to
				int di = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("LevyFizz"), (int)((x0 + 60f) * 16f), (int)((CrF - 3f) * 16f), NPCID.Zombie);
				NPC d = Main.npc[di]; d.friendly = true; d.damage = 0; d.noGravity = true; d.noTileCollide = true; d.aiStyle = -1; d.dontTakeDamage = true; d.lifeMax = d.life = 1000; d.velocity = Vector2.Zero; d.GetGlobalNPC<HarnessTarget>().Pin = d.position; _fzDummyNpc = di;
				SoldierNPC.FallDamageOverride = 1;
				_fzPhase = 0; _fzTick = 0; _fzHit = 0; _fzStarted = true; _fzDxs.Clear(); _fzDys.Clear(); _fzVs.Clear(); _fzAirs.Clear(); _fzSettles.Clear(); _fzDeaths = 0; _fzDropped = false; _fzStillRun = _fzStillMax = 0; _fzFlights0 = SoldierNPC.FizzFlights;
				Mod.Logger.Info($"HARNESS-FZ: start {run.Key} tp={run.Tp}");
				return;
			}
			_fzTick++;
			Main.dayTime = false; Main.time = 16200;
			CrPinCommander();
			NPC lv = Main.npc[_fzLevy.Idx]; var sn = lv.ModNPC as SoldierNPC;
			bool alive = _fzLevy.Rec.HasBody && lv.active && sn != null && lv.life > 0;
			if (!alive) { _fzDeaths++; FzFinish(run, kv, "dead"); return; }
			sn.Stations = Systems.StationFlags.Campfire | Systems.StationFlags.HeartLantern;
			// the arm
			if (neu) { int ft = ModContent.BuffType<FizzyBuff>(); if (!lv.HasBuff(ft)) { lv.AddBuff(ft, 60 * 60 * 5); if (!lv.HasBuff(ft)) { lv.buffType[0] = ft; lv.buffTime[0] = 60 * 60 * 5; } sn.StatsDirty = true; } }
			else lv.knockBackResist = 0.75f;
			_fzGravSample = lv.gravity;
			float posx = lv.Center.X / 16f - x0, posy = (CrF * 16f - lv.Bottom.Y) / 16f; // tiles right of the start, tiles above the floor
			float speed = lv.velocity.Length();

			if (_fzPhase == 0)
			{
				// phase 1: blows. The levy stands still between them (the commander is beside it; order follow keeps it where it is)
				int t = _fzTick - 60; // 1 s of settling first
				if (t < 0) return;
				int hitIdx = t / (gap + win); int inHit = t % (gap + win);
				if (hitIdx >= hits) { _fzPhase = 1; _fzTick = 0; return; }
				if (inHit == 0)
				{
					_fzP0 = lv.Center; _fzDx = _fzDy = _fzV = 0f; _fzAir = 0; _fzSettle = -1; _fzSettled = false; _fzHp0 = lv.life;
					sn.ImmuneTime = 0;
					int dir = (hitIdx % 2 == 0) ? 1 : -1; if (Main.rand.NextBool()) dir = -dir;
					sn.ReceiveHostileMelee(Main.npc[_fzDummyNpc], 12, dir);
				}
				else if (inHit < win)
				{
					_fzDx = Math.Max(_fzDx, Math.Abs(lv.Center.X - _fzP0.X) / 16f); _fzDy = Math.Max(_fzDy, (_fzP0.Y - lv.Center.Y) / 16f); _fzV = Math.Max(_fzV, speed);
					if (lv.velocity.Y != 0f) _fzAir++;
					if (!_fzSettled && inHit > 8 && Math.Abs(lv.velocity.X) < 0.3f && lv.velocity.Y == 0f && !sn.FizzFlying) { _fzSettled = true; _fzSettle = inHit; }
					if (inHit == win - 1) { _fzDxs.Add(_fzDx); _fzDys.Add(_fzDy); _fzVs.Add(_fzV); _fzAirs.Add(_fzAir); _fzSettles.Add(_fzSettled ? _fzSettle : win); }
				}
				return;
			}
			if (_fzPhase == 1)
			{
				// phase 2: a fall from `drop` tiles, with fall damage on
				if (!_fzDropped)
				{
					if (_fzTick < 30) return;
					lv.position = new Vector2(lv.position.X, (CrF - dropT) * 16f); lv.velocity = Vector2.Zero; sn.NotifyTeleported(); _fzDropped = true; _fzDropHp0 = lv.life; _fzFallDmg0 = sn.FallDamageTotal; _fzFallHits0 = sn.FallHits; _fzDropMinGrav = 1f; _fzTick = 31;
					return;
				}
				_fzDropMinGrav = Math.Min(_fzDropMinGrav, lv.gravity);
				if (_fzTick > 31 + 900) { _fzPhase = 2; _fzTick = 0; _fzFarX = (x0 + farT) * 16f; _crCmdPos = new Vector2(_fzFarX, CrF * 16f); _fzWalkDist0 = Math.Abs(lv.Center.X - _fzFarX) / 16f; _fzWalkPath = 0f; _fzPrevPos = lv.Center; _fzStillRun = _fzStillMax = 0; }
				return;
			}
			// phase 3: the commander is far; the levy must come to him
			_fzWalkPath += Vector2.Distance(lv.Center, _fzPrevPos) / 16f;
			if (Vector2.DistanceSquared(lv.Center, _fzPrevPos) < 0.04f) { _fzStillRun++; _fzStillMax = Math.Max(_fzStillMax, _fzStillRun); } else _fzStillRun = 0;
			_fzPrevPos = lv.Center;
			if (_fzTick >= walk) FzFinish(run, kv, "ok");
		}

		private void FzFinish(CrRun run, Dictionary<string, string> kv, string status)
		{
			string G(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
			string f(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
			double Mean(List<float> l) { if (l.Count == 0) return -1; double s = 0; foreach (var x in l) s += x; return s / l.Count; }
			double Max(List<float> l) { double m = 0; foreach (var x in l) m = Math.Max(m, x); return m; }
			NPC lv = Main.npc[_fzLevy.Idx]; var sn = lv.ModNPC as SoldierNPC;
			bool alive = lv.active && sn != null && lv.life > 0;
			double endDist = alive ? Math.Abs(lv.Center.X - _fzFarX) / 16f : -1;
			try
			{
				string path = _crOut + ".tipsy.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\ttag\tseed\tfz_arm\tfz_kb\tdx_mean\tdx_max\tdy_mean\tdy_max\tvmax\tair_ticks\tsettle_ticks\tblows\tgravity\tdrop_dmg\tdrop_falls\tdrop_dead\twalk_start\twalk_end\twalk_path\tstill_max\tstatus\tflights\n");
				File.AppendAllText(path, string.Join("\t", new[] { run.Key, run.Tag, run.Seed.ToString(), G("arm", "new"), G("kb", "none"), f(Mean(_fzDxs)), f(Max(_fzDxs)), f(Mean(_fzDys)), f(Max(_fzDys)), f(Max(_fzVs)), f(Mean(_fzAirs)), f(Mean(_fzSettles)), _fzDxs.Count.ToString(),
					f(_fzGravSample), (sn != null ? sn.FallDamageTotal - _fzFallDmg0 : -1).ToString(), (sn != null ? sn.FallHits - _fzFallHits0 : -1).ToString(), (alive ? 0 : 1).ToString(), f(_fzWalkDist0), f(endDist), f(_fzWalkPath), _fzStillMax.ToString(), status, (SoldierNPC.FizzFlights - _fzFlights0).ToString() }) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-FZ: row failed: " + e.Message); }
			Mod.Logger.Info($"HARNESS-FZ: done {run.Tag} seed {run.Seed}: {status} dx {Mean(_fzDxs):0.0} air {Mean(_fzAirs):0} fall {(sn != null ? sn.FallDamageTotal - _fzFallDmg0 : -1)} walk {_fzWalkDist0:0}->{endDist:0}");
			var cols = CrHeader.Split('\t').Length; var v = new string[cols]; for (int i = 0; i < cols; i++) v[i] = "0";
			v[0] = run.Key; v[1] = run.Kind; v[2] = run.Boss; v[3] = run.Mode; v[4] = run.Order; v[5] = run.Seed.ToString(); v[6] = "FIZZ"; v[7] = _fzTick.ToString(); v[8] = f(_fzTick / 60.0); v[cols - 1] = "fizz";
			File.AppendAllText(_crOut, string.Join("\t", v) + "\n");
			SoldierNPC.FallDamageOverride = -1;
			_fzStarted = false; _cr = null; _crS = null; _crStarted = false; _crI++;
			ClearAll(); CrPurgeNpcs();
		}
	}
}
