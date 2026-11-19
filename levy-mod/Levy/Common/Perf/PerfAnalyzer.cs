using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Recording;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Perf
{
	/// <summary>
	/// THE ALWAYS-ON PERFORMANCE ANALYZER. On a client (single player, or a player in multiplayer) it closes a window of numbers every eight seconds (frames and updates per second, ms per update and per frame, counts of NPCs, projectiles,
	/// dust, gore and levies, what the player is doing, who costs the time) and appends it to a rolling log in the "perf" folder next to the recordings; /levyperf shows the last minute; after a laggy boss fight the popup says where the time went
	/// (<see cref="PerfPopup"/>). Nothing here changes how the game plays.
	/// <para/>Cost: the update and the draw are timed on every tick and frame (two clock reads); the per-entity timing (NPC and projectile updates by owning mod and type, army shots, Levy's own buckets, drawing of each NPC and projectile) runs on one tick in
	/// eleven and one frame in four and is scaled up, so the work per tick averages well under a tenth of a millisecond (the world check measures it with the analyzer on and off in the same boot). Map layers and interface layers are timed on every call (a few a frame).
	/// <para/>Fail-safe: every analyzer code path is inside its own try/catch; on an error <see cref="Fail"/> turns the analyzer off (the detours step aside at once and are removed next tick) and the game carries on. The game's own calls are never inside those try blocks.
	/// </summary>
	public partial class PerfAnalyzer : ModSystem
	{
		public const int SampleEvery = 11, LevySampleEvery = 66, DrawSampleEvery = 4, LogFlushSecs = 9;
		public const double WindowSecs = 8.0;
		private static readonly double Freq = Stopwatch.Frequency;

		// ------------------------------------------------------------------------------------------ state
		/// <summary>True while the timing detours are attached.</summary>
		public static bool TimingOn { get; private set; }
		/// <summary>True when the army's effects are reduced (nameplates hidden, levy dust thinned).</summary>
		public static bool EffectsReduced { get; private set; }
		private static volatile bool _dead; private static bool _attachedCtx, _attachedTiming, _attachedDust;
		internal static string DeadWhy;
		/// <summary>Self-check: allow the analyzer on a dedicated server (it never runs there otherwise), and replace the window length and the config.</summary>
		internal static bool TestServer = Environment.GetEnvironmentVariable("LEVY_PERFAN_SERVER") == "1"; internal static double? TestWindowSecs; internal static bool? TestLog, TestReduced; internal static string TestLogDir;
		private static long _tick, _frame; private static bool _sample, _dsample;
		private static long _winStart, _updTicks, _drawTicks, _worstUpd, _worstFrame, _npcPhase, _projPhase, _npcStart, _projStart;
		private static int _updates, _frames, _sUpdates, _sFrames, _bUpdates;
		private static readonly long[] _draw = new long[6]; // npcs, projectiles, dust, gore, interface, players (ticks, every frame)
		private static bool _userProf;
		private static readonly long[] _lvSnap = new long[LevyProf.N], _lvNow = new long[LevyProf.N];

		/// <summary>Counters the self-check reads: hook calls timed, windows closed, entities seen.</summary>
		internal static long Timed, WindowsClosed, NpcCalls, ProjCalls;

		/// <summary>The last windows (about two minutes), newest last.</summary>
		internal static readonly List<PerfWindow> Recent = new();
		internal static PerfWindow Last;
		internal static LagVerdict LastVerdict;

		// ------------------------------------------------------------------------------------------ lifecycle
		public override void Load()
		{
			if (Main.dedServ && !TestServer) return;
			try { ConfigChanged(); } catch (Exception e) { Fail("load", e); }
		}

		public override void PostSetupContent()
		{
			if (Main.dedServ && !TestServer) return;
			try { WrapContent(); } catch (Exception e) { Fail("wrap", e); }
		}

		public override void Unload()
		{
			try { Detach(); FlushLog(true); } catch (Exception) { }
			try { DisposeWraps(); } catch (Exception) { }
			Recent.Clear(); Last = null; LastVerdict = null; _dead = false; DeadWhy = null;
		}

		public override void OnWorldLoad() { try { ResetWindow(Stopwatch.GetTimestamp()); _fight.Clear(); _inFight = false; _announced = false; ConfigChanged(); } catch (Exception e) { Fail("world load", e); } }
		public override void OnWorldUnload() { try { if (_inFight) EndFight(); FlushLog(true); } catch (Exception) { } }

		/// <summary>The config changed (or was loaded): attach or remove the detours to match.</summary>
		public static void ConfigChanged()
		{
			if (Main.dedServ && !TestServer) return;
			PerfConfig c = null; try { c = PerfConfig.Get; } catch (Exception) { }
			bool log = TestLog ?? c?.PerfLogAlwaysOn ?? true;
			bool reduced = TestReduced ?? ((c?.LevyEffects ?? LevyEffectsQuality.Full) == LevyEffectsQuality.Reduced);
			EffectsReduced = reduced;
			if (_dead) return;
			try
			{
				if (log) AttachTiming(); else DetachTiming();
				if (log || reduced) AttachContext(); else DetachContext();
				if (log || reduced) AttachDust(); else DetachDust(); // reduced effects thin the army's dust always, the pressure rule only while the screen is full (both need the army context above)
				_logDir = null; // the folder is looked up again
			}
			catch (Exception e) { Fail("config", e); }
		}

		/// <summary>Turns the analyzer off after an error in it. The game is untouched: the detours step aside at once (<see cref="_dead"/>), their removal follows on the next tick.</summary>
		internal static void Fail(string where, Exception e)
		{
			if (_dead) return;
			_dead = true; DeadWhy = where + ": " + e.GetType().Name + ": " + e.Message;
			try { Levy.Instance?.Logger.Warn("[PerfAnalyzer] error in " + where + ", the performance analyzer is off for this session: " + e); } catch (Exception) { }
			EffectsReduced = false;
		}

		/// <summary>Self-check: forget a failure (the check provoked one) and attach again.</summary>
		internal static void TestRevive() { _dead = false; DeadWhy = null; }

		public override void PostUpdateEverything()
		{
			if (Main.dedServ && !TestServer) return;
			if (_dead) { try { Detach(); } catch (Exception) { } return; }
			try
			{
				if (_tick % 60 == 0) ScanBoss();
				if (_tick % 15 == 0 && _attachedDust) MeasurePressure();
				if (!_dead) PerfPopup.Tick();
			}
			catch (Exception e) { Fail("tick", e); }
		}

		public override void PreUpdateNPCs() { if (TimingOn && !_dead) _npcStart = Stopwatch.GetTimestamp(); }
		public override void PostUpdateNPCs() { if (_npcStart != 0) { _npcPhase += Stopwatch.GetTimestamp() - _npcStart; _npcStart = 0; } }
		public override void PreUpdateProjectiles() { if (TimingOn && !_dead) _projStart = Stopwatch.GetTimestamp(); }
		public override void PostUpdateProjectiles() { if (_projStart != 0) { _projPhase += Stopwatch.GetTimestamp() - _projStart; _projStart = 0; } }

		// ------------------------------------------------------------------------------------------ the update and the draw
		private static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, ref GameTime gameTime)
		{ _hl_DoUpdate_2++;
			if (_dead) { orig(self, ref gameTime); return; }
			long t0 = Begin();
			try { orig(self, ref gameTime); }
			finally { End(t0); }
		} private static int _hl_DoUpdate_2;

		/// <summary>The start of one game update. Decides whether this one is sampled and, when it is, switches Levy's own bucket timers on for its length.</summary>
		private static long Begin()
		{
			try
			{
				_tick++; _army = false; _gsp = 0;
				_sample = !TestNoSample && _tick % SampleEvery == 0;
				if (_sample)
				{
					_userProf = ClientProf.IsRunning || LevyProf.OnFromStart;
					if (!_userProf && _tick % LevySampleEvery == 0) LevyProf.On = true; // Levy's own bucket timers are the dearest to run, so they get one tick in 66
				}
				return Stopwatch.GetTimestamp();
			}
			catch (Exception e) { Fail("begin", e); return 0; }
		}

		private static void End(long t0)
		{
			if (t0 == 0 || _dead) return;
			try
			{
				long now = Stopwatch.GetTimestamp(), dt = now - t0;
				_updTicks += dt; _updates++; if (dt > _worstUpd) _worstUpd = dt;
				LastTickSampled = _sample; LastTickLevy = _sample && LevyProf.On;
				// Levy's bucket timers ran for this update: count it. While the player's profiler or a recorded fight's profiling is on they run on EVERY update, not only on the sampled ones, so counting
				// only sampled updates divided a whole window's bucket time by one update in eleven (Eve's 11-17 log: "stats 37 ms" per update inside a 17 ms update).
				if (LevyProf.On) _bUpdates++;
				if (_sample) { _sUpdates++; if (!_userProf) LevyProf.On = false; }
				_sample = false;
				double win = TestWindowSecs ?? WindowSecs;
				if ((now - _winStart) >= win * Freq) CloseWindow(now);
			}
			catch (Exception e) { Fail("end", e); }
		}

		/// <summary>Self-check: one game update as the detour sees it (the caller runs the update between the two calls).</summary>
		internal static long TestBegin() => TimingOn && !_dead ? Begin() : 0; // not used by the world checks: the real DoUpdate detour fires inside Main.Update
		internal static void TestEnd(long t0) { if (t0 != 0) End(t0); }

		private static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
		{ _hl_DoDraw_1++;
			if (_dead) { orig(self, gameTime); return; }
			long t0;
			try { _frame++; _dsp = 0; _dsample = _frame % DrawSampleEvery == 0; t0 = Stopwatch.GetTimestamp(); } catch (Exception e) { Fail("draw begin", e); orig(self, gameTime); return; }
			try { orig(self, gameTime); }
			finally
			{
				try { long dt = Stopwatch.GetTimestamp() - t0; _drawTicks += dt; _frames++; if (dt > _worstFrame) _worstFrame = dt; if (_dsample) _sFrames++; _dsample = false; }
				catch (Exception e) { Fail("draw end", e); }
			}
		} private static int _hl_DoDraw_1;

		private static void DrawNpcs(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles) { _hl_DrawNpcs_6++; if (_dead) { orig(self, behindTiles); return; } long t0 = Stopwatch.GetTimestamp(); try { orig(self, behindTiles); } finally { _draw[0] += Stopwatch.GetTimestamp() - t0; } } private static int _hl_DrawNpcs_6;
		private static void DrawProjectiles(On_Main.orig_DrawProjectiles orig, Main self) { _hl_DrawProjectiles_8++; if (_dead) { orig(self); return; } long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { _draw[1] += Stopwatch.GetTimestamp() - t0; } } private static int _hl_DrawProjectiles_8;
		private static void DrawDust(On_Main.orig_DrawDust orig, Main self) { _hl_DrawDust_3++; if (_dead) { orig(self); return; } long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { _draw[2] += Stopwatch.GetTimestamp() - t0; } } private static int _hl_DrawDust_3;
		private static void DrawGore(On_Main.orig_DrawGore orig, Main self) { _hl_DrawGore_4++; if (_dead) { orig(self); return; } long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { _draw[3] += Stopwatch.GetTimestamp() - t0; } } private static int _hl_DrawGore_4;
		private static void DrawInterface(On_Main.orig_DrawInterface orig, Main self, GameTime gameTime) { _hl_DrawInterface_5++; if (_dead) { orig(self, gameTime); return; } long t0 = Stopwatch.GetTimestamp(); try { orig(self, gameTime); } finally { _draw[4] += Stopwatch.GetTimestamp() - t0; } } private static int _hl_DrawInterface_5;
		private static void DrawPlayers(On_Main.orig_DrawPlayers_AfterProjectiles orig, Main self) { _hl_DrawPlayers_7++; if (_dead) { orig(self); return; } long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { _draw[5] += Stopwatch.GetTimestamp() - t0; } } private static int _hl_DrawPlayers_7;

		private static int DustNew(On_Dust.orig_NewDust orig, Vector2 pos, int w, int h, int type, float sx, float sy, int alpha, Color c, float scale)
		{ _hl_DustNew_9++;
			if (_army && !_dead && (EffectsReduced || DustPressure) && (++_dustN % 3) != 0) { DustThinned++; if (!EffectsReduced) DustThinnedPressure++; return Main.maxDust; } // the game's own "no dust" answer: callers index Main.dust[maxDust], a spare slot
			return orig(pos, w, h, type, sx, sy, alpha, c, scale);
		} private static int _hl_DustNew_9;

		// ------------------------------------------------------------------------------------------ effects under pressure (cosmetic only)
		/// <summary>
		/// The screen is already full of particles: 2,500 dust or 400 gore alive (Eve's 11-17 pillar fights held 3,000 to 5,800 dust and gore pinned at the 600 cap, against about 360 and 45 in a smooth fight). While it is,
		/// two of every three dust particles made inside a levy's update or an army shot's are dropped, and one of every two gore pieces, exactly as "Lower levy effects" thins dust. Damage, hits, projectiles and everything
		/// a fight does are untouched: only particles and gore the army's own updates would add on top of a full screen. Checked every 15 updates; a client only (a server makes neither).
		/// </summary>
		public const int DustPressureAt = 2500, GorePressureAt = 400;
		internal static bool DustPressure, GorePressure;
		internal static long DustThinnedPressure, GoreThinned;
		private static int _goreN;
		/// <summary>Self-check: pressure forced on or off (null = measured).</summary>
		internal static bool? TestPressure;

		private static void MeasurePressure()
		{
			if (TestPressure.HasValue) { DustPressure = GorePressure = TestPressure.Value; return; }
			if (Main.dedServ) { DustPressure = GorePressure = false; return; }
			int d = 0, g = 0;
			for (int i = 0; i < Main.maxDust; i++) if (Main.dust[i] != null && Main.dust[i].active) d++;
			for (int i = 0; i < Main.maxGore; i++) if (Main.gore[i] != null && Main.gore[i].active) g++;
			DustPressure = d >= DustPressureAt; GorePressure = g >= GorePressureAt;
		}

		private static int GoreNew(On_Gore.orig_NewGore_IEntitySource_Vector2_Vector2_int_float orig, Terraria.DataStructures.IEntitySource source, Vector2 pos, Vector2 vel, int type, float scale)
		{
			if (_army && !_dead && GorePressure && (++_goreN % 2) != 0) { GoreThinned++; return Main.maxGore; } // the game's own "no gore" answer (a server returns it): callers index Main.gore[maxGore], a spare slot
			return orig(source, pos, vel, type, scale);
		}

		// ------------------------------------------------------------------------------------------ attaching
		private static void AttachTiming()
		{
			if (_attachedTiming) return;
			On_Main.DoUpdate += DoUpdate; On_Main.DoDraw += DoDraw; On_Main.DrawNPCs += DrawNpcs; On_Main.DrawProjectiles += DrawProjectiles; On_Main.DrawDust += DrawDust; On_Main.DrawGore += DrawGore;
			On_Main.DrawInterface += DrawInterface; On_Main.DrawPlayers_AfterProjectiles += DrawPlayers; 
			AttachEntityDraws();
			_attachedTiming = true; TimingOn = true; ResetWindow(Stopwatch.GetTimestamp());
		}
		private static void DetachTiming()
		{
			if (!_attachedTiming) return;
			On_Main.DoUpdate -= DoUpdate; On_Main.DoDraw -= DoDraw; On_Main.DrawNPCs -= DrawNpcs; On_Main.DrawProjectiles -= DrawProjectiles; On_Main.DrawDust -= DrawDust; On_Main.DrawGore -= DrawGore;
			On_Main.DrawInterface -= DrawInterface; On_Main.DrawPlayers_AfterProjectiles -= DrawPlayers;
			DetachList(_ilDraw);
			_attachedTiming = false; TimingOn = false; _sample = _dsample = false;
		}
		internal static int AttachCount;
		private static void AttachContext() { if (_attachedCtx) return; AttachCount++; AttachEntityUpdates(); _attachedCtx = true; }
		private static void DetachContext() { if (!_attachedCtx) return; DetachList(_ilCtx); _attachedCtx = false; }
		private static void AttachDust() { if (_attachedDust) return; On_Dust.NewDust += DustNew; On_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float += GoreNew; _attachedDust = true; }
		private static void DetachDust() { if (!_attachedDust) return; On_Dust.NewDust -= DustNew; On_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float -= GoreNew; _attachedDust = false; DustPressure = GorePressure = false; }

		private static void Detach()
		{
			try { DetachTiming(); } catch (Exception) { }
			try { DetachContext(); } catch (Exception) { }
			try { DetachDust(); } catch (Exception) { }
			TimingOn = false; _army = false;
			if (!_userProf) { try { LevyProf.On = LevyProf.OnFromStart || ClientProf.IsRunning; } catch (Exception) { } }
		}

		// ------------------------------------------------------------------------------------------ owners (mod of each type) and per-mod accumulators
		private const int MaxMods = 256;
		private static readonly string[] _modNames = new string[MaxMods]; private static int _modCount;
		private static readonly Dictionary<string, int> _modIds = new();
		private static readonly long[] _npcT = new long[MaxMods], _npcN = new long[MaxMods], _projT = new long[MaxMods], _projN = new long[MaxMods], _dnpcT = new long[MaxMods], _dnpcN = new long[MaxMods], _dprojT = new long[MaxMods], _dprojN = new long[MaxMods];
		private static long _armyT, _armyN, _darmyT, _darmyN;
		private static short[] _npcOwner = new short[0], _projOwner = new short[0];
		private static long[] _typeNpcT = new long[0], _typeNpcN = new long[0], _typeProjT = new long[0], _typeProjN = new long[0];

		/// <summary>The id of a mod's name ("Terraria" for the base game), made on first use. The table is full at 256 mods; the rest share the last id.</summary>
		internal static int ModId(string name)
		{
			lock (_modIds)
			{
				if (_modIds.TryGetValue(name, out int id)) return id;
				if (_modCount >= MaxMods) return MaxMods - 1;
				id = _modCount++; _modNames[id] = name; _modIds[name] = id; return id;
			}
		}

		private static int NpcOwner(int type)
		{
			var a = _npcOwner;
			if (type >= a.Length || type < 0) { a = _npcOwner = new short[Math.Max(NPCLoader.NPCCount, type + 1) + 16]; }
			int v = a[type]; if (v != 0) return v - 1;
			ModNPC m = NPCLoader.GetNPC(type); int id = ModId(m?.Mod?.Name ?? "Terraria"); a[type] = (short)(id + 1); return id;
		}

		private static int ProjOwner(int type)
		{
			var a = _projOwner;
			if (type >= a.Length || type < 0) { a = _projOwner = new short[Math.Max(ProjectileLoader.ProjectileCount, type + 1) + 16]; }
			int v = a[type]; if (v != 0) return v - 1;
			ModProjectile m = ProjectileLoader.GetProjectile(type); int id = ModId(m?.Mod?.Name ?? "Terraria"); a[type] = (short)(id + 1); return id;
		}

		/// <summary>The owning mod's name of an NPC type, as the analyzer files its cost ("Terraria" for the base game). The self-check uses it.</summary>
		internal static string NpcOwnerName(int type) => _modNames[NpcOwner(type)];
		internal static string ProjOwnerName(int type) => _modNames[ProjOwner(type)];

		private static string NpcTypeName(int type) { ModNPC m = NPCLoader.GetNPC(type); return m != null ? m.Mod.Name + "/" + m.Name : "Terraria/" + (NPCID.Search.TryGetName(type, out string n) ? n : type.ToString()); }
		private static string ProjTypeName(int type) { ModProjectile m = ProjectileLoader.GetProjectile(type); return m != null ? m.Mod.Name + "/" + m.Name : "Terraria/" + (ProjectileID.Search.TryGetName(type, out string n) ? n : type.ToString()); }

		// ------------------------------------------------------------------------------------------ the window
		private static void ResetWindow(long now)
		{
			_winStart = now; _updTicks = _drawTicks = _worstUpd = _worstFrame = _npcPhase = _projPhase = 0; _updates = _frames = _sUpdates = _sFrames = _bUpdates = 0;
			Array.Clear(_draw, 0, _draw.Length);
			Array.Clear(_npcT, 0, MaxMods); Array.Clear(_npcN, 0, MaxMods); Array.Clear(_projT, 0, MaxMods); Array.Clear(_projN, 0, MaxMods);
			Array.Clear(_dnpcT, 0, MaxMods); Array.Clear(_dnpcN, 0, MaxMods); Array.Clear(_dprojT, 0, MaxMods); Array.Clear(_dprojN, 0, MaxMods);
			_armyT = _armyN = _darmyT = _darmyN = 0;
			int nc = NPCLoader.NPCCount + 16, pc = ProjectileLoader.ProjectileCount + 16;
			if (_typeNpcT.Length != nc) { _typeNpcT = new long[nc]; _typeNpcN = new long[nc]; } else { Array.Clear(_typeNpcT, 0, nc); Array.Clear(_typeNpcN, 0, nc); }
			if (_typeProjT.Length != pc) { _typeProjT = new long[pc]; _typeProjN = new long[pc]; } else { Array.Clear(_typeProjT, 0, pc); Array.Clear(_typeProjN, 0, pc); }
			ResetSlots(); ResetWeapons();
			LevyProf.CopyTo(_lvSnap);
			_winBoss = ""; _winBossFar = "";
		}

		private static void CloseWindow(long now)
		{
			try
			{
				double secs = (now - _winStart) / Freq;
				if (_updates >= 5 && secs >= 0.01 && !Main.gameMenu)
				{
					PerfWindow w = Build(secs);
					Commit(w);
				}
			}
			catch (Exception e) { Fail("window", e); }
			finally { try { ResetWindow(Stopwatch.GetTimestamp()); } catch (Exception e) { Fail("reset", e); } }
		}

		private static double Ms(long ticks) => ticks * 1000.0 / Freq;

		private static PerfWindow Build(double secs)
		{
			if (TestFault) throw new InvalidOperationException("self-check fault");
			double upd = Math.Max(1, _updates), frm = Math.Max(1, _frames);
			double sc = _sUpdates > 0 ? _updates / (double)_sUpdates : 0, scF = _sFrames > 0 ? _frames / (double)_sFrames : 0;
			var w = new PerfWindow
			{
				Utc = DateTime.UtcNow, Tick = (long)Main.GameUpdateCount, Secs = secs, Updates = _updates, Frames = _frames, SampledUpdates = _sUpdates, SampledFrames = _sFrames,
				Fps = _frames / secs, Ups = _updates / secs, UpdMs = Ms(_updTicks) / upd, DrawMs = Ms(_drawTicks) / frm, WorstUpdMs = Ms(_worstUpd), WorstFrameMs = Ms(_worstFrame),
				Focus = Main.hasFocus, NetMode = Main.netMode, Effects = EffectsReduced ? 1 : 0, FrameSkip = Main.dedServ ? -1 : (int)Main.FrameSkipMode,
			};
			for (int i = 0; i < 6; i++) w.DrawSplit[i] = Ms(_draw[i]) / frm;
			w.UpdSplit[0] = Ms(_npcPhase) / upd; w.UpdSplit[1] = Ms(_projPhase) / upd;
			CountEntities(w);
			// what the player is doing
			Player p = Main.LocalPlayer;
			if (p != null && p.active) { Item it = p.HeldItem; if (it != null && !it.IsAir) { w.Held = it.type; w.HeldName = it.Name; } }
			w.Map = Main.mapFullscreen ? 2 : (Main.mapEnabled && Main.mapStyle > 0 ? 1 : 0);
			w.Boss = _winBoss; w.BossFar = _winBossFar;
			// Levy's own buckets (ms per update, over the updates that were timed)
			LevyProf.CopyTo(_lvNow);
			double lvUpd = Math.Max(1, _bUpdates); var lv = new List<KeyValuePair<string, double>>();
			for (int k = 0; k < LevyProf.N; k++) { long d = _lvNow[k] - _lvSnap[k]; if (d > 0) { double ms = Ms(d) / lvUpd; if (ms >= 0.01) lv.Add(new KeyValuePair<string, double>(LevyProf.NameOf(k), ms)); } }
			lv.Sort((a, b) => b.Value.CompareTo(a.Value)); if (lv.Count > 8) lv.RemoveRange(8, lv.Count - 8);
			w.Levy = lv;
			// who costs time
			var items = new List<PerfItem>();
			void Mods(string cat, long[] t, long[] n, double scale) { for (int i = 0; i < _modCount; i++) if (t[i] > 0) items.Add(new PerfItem(cat, _modNames[i], Ms(t[i]) * scale, n[i] * scale)); }
			Mods(PerfCat.Npc, _npcT, _npcN, sc); Mods(PerfCat.Proj, _projT, _projN, sc); Mods(PerfCat.DrawNpc, _dnpcT, _dnpcN, scF); Mods(PerfCat.DrawProj, _dprojT, _dprojN, scF);
			if (_armyT > 0) items.Add(new PerfItem(PerfCat.Army, "all", Ms(_armyT) * sc, _armyN * sc));
			if (_darmyT > 0) items.Add(new PerfItem(PerfCat.DrawArmy, "all", Ms(_darmyT) * scF, _darmyN * scF));
			AddSlots(items, sc);
			AddTypes(items, PerfCat.NpcType, _typeNpcT, _typeNpcN, sc, NpcTypeName); AddTypes(items, PerfCat.ProjType, _typeProjT, _typeProjN, sc, ProjTypeName);
			items.RemoveAll(it => it.Ms < 0.5);
			items.Sort((a, b) => b.Ms.CompareTo(a.Ms));
			if (items.Count > 18) items.RemoveRange(18, items.Count - 18);
			w.Top = items;
			BuildWeapons(w);
			return w;
		}

		private static void AddTypes(List<PerfItem> items, string cat, long[] t, long[] n, double sc, Func<int, string> name)
		{
			int a = -1, b = -1, c = -1;
			for (int i = 0; i < t.Length; i++)
			{
				long v = t[i]; if (v <= 0) continue;
				if (a < 0 || v > t[a]) { c = b; b = a; a = i; } else if (b < 0 || v > t[b]) { c = b; b = i; } else if (c < 0 || v > t[c]) c = i;
			}
			foreach (int i in new[] { a, b, c }) if (i >= 0) items.Add(new PerfItem(cat, name(i), Ms(t[i]) * sc, n[i] * sc));
		}

		private static void CountEntities(PerfWindow w)
		{
			int npcs = 0, levies = 0, projs = 0, minions = 0, dust = 0, gore = 0;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (!n.active) continue; npcs++; if (n.ModNPC is SoldierNPC) levies++; }
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (!p.active) continue; projs++; if (p.minion) minions++; }
			for (int i = 0; i < Main.maxDust; i++) if (Main.dust[i].active) dust++;
			for (int i = 0; i < Main.gore.Length; i++) if (Main.gore[i] != null && Main.gore[i].active) gore++;
			w.Npcs = npcs; w.Levies = levies; w.Projs = projs; w.Minions = minions; w.Dust = dust; w.Gore = gore;
		}

		// ------------------------------------------------------------------------------------------ boss and fights
		private static string _winBoss = "", _winBossFar = ""; private static bool _inFight;
		private static readonly List<PerfWindow> _fight = new();
		private const int BossRangeTiles = 250;
		private static long _bossNearUntil;
		/// <summary>True while a fight boss was seen near the player within the last couple of seconds.</summary>
		internal static bool BossNear => _tick < _bossNearUntil;
		internal static void TestSetBossNear(bool on) { _bossNearUntil = on ? _tick + 100000 : 0; }

		private static void ScanBoss()
		{
			Player p = Main.LocalPlayer; if (p == null || !p.active || Main.gameMenu) return;
			float r = BossRangeTiles * 16f;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || !FightRecorder.IsFightBoss(n)) continue;
				if (Math.Abs(n.Center.X - p.Center.X) < r && Math.Abs(n.Center.Y - p.Center.Y) < r) { _winBoss = n.FullName; _bossNearUntil = _tick + 130; return; }
				if (_winBossFar.Length == 0) _winBossFar = n.FullName;
			}
		}

		/// <summary>Self-check: feeds the scan's answer for the window being built.</summary>
		internal static void TestSetBoss(string near) { _winBoss = near ?? ""; }

		private static void Commit(PerfWindow w)
		{
			WindowsClosed++; Last = w;
			Recent.Add(w); while (Recent.Count > 16) Recent.RemoveAt(0);
			WriteLog(w.ToJson());
			if (!string.IsNullOrEmpty(w.Boss)) { if (!_inFight) { _inFight = true; _fight.Clear(); } _fight.Add(w); }
			else if (_inFight) EndFight();
		}

		/// <summary>The fight is over (a whole window passed without a boss near the player): judge it, log the verdict, and offer the popup when it earns one.</summary>
		internal static void EndFight()
		{
			_inFight = false;
			var fight = _fight.ToList(); _fight.Clear();
			if (fight.Count == 0) return;
			LagVerdict v = PerfAnalysis.Judge(fight);
			LastVerdict = v;
			WriteLog(FightLine(v));
			if (v.ShowPopup) PerfPopup.Offer(v);
		}

		internal static string FightLine(LagVerdict v)
		{
			return "{\"k\":\"fight\",\"t\":" + PerfWindow.Esc(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)) + ",\"boss\":" + PerfWindow.Esc(v.Boss) + ",\"secs\":" + (int)v.FightSecs
				+ ",\"laggy\":" + (v.Laggy ? 1 : 0) + ",\"kind\":" + PerfWindow.Esc(v.Kind.ToString()) + ",\"share\":" + (int)Math.Round(v.LaggyShare * 100) + ",\"medfps\":" + Math.Round(v.MedianFps, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"levy\":" + (int)Math.Round(v.LevyShare * 100) + ",\"top\":" + PerfWindow.Esc(v.TopMod) + ",\"topshare\":" + (int)Math.Round(v.TopShare * 100) + ",\"popup\":" + (v.ShowPopup ? 1 : 0) + WeaponJson(v) + "}";
		}

		/// <summary>The fight's levy weapons for the log line: all of them together (ms per update) and the top five as [name, levies, ms per update, share of the army %].</summary>
		private static string WeaponJson(LagVerdict v)
		{
			if (v.Weapons == null || v.Weapons.Count == 0) return "";
			var inv = System.Globalization.CultureInfo.InvariantCulture; double tot = Math.Max(1e-9, v.WeaponAttribMs);
			return ",\"wpa\":" + Math.Round(v.WeaponAttribMs, 3).ToString(inv) + ",\"wpn\":[" + string.Join(",", v.Weapons.Take(5).Select(r => "[" + PerfWindow.Esc(r.Name) + "," + Math.Round(r.Levies, 1).ToString(inv) + "," + Math.Round(r.Ms, 3).ToString(inv) + "," + (int)Math.Round(r.Ms / tot * 100) + "]")) + "]";
		}

		// ------------------------------------------------------------------------------------------ the log
		private static readonly ConcurrentQueue<string> _q = new();
		private static string _logDir; private static bool _announced, _logBroken; private static int _ioFails; private static long _lastFlush; private static DateTime _lastEnforce = DateTime.MinValue;
		private static readonly object _ioGate = new();
		/// <summary>The log file last written (the self-check and /levyperf read it).</summary>
		internal static string LastLogFile;

		private static void WriteLog(string line)
		{
			if (_logBroken) return;
			if (!_announced) { _announced = true; _q.Enqueue(HeaderLine()); }
			_q.Enqueue(line);
			long now = Stopwatch.GetTimestamp();
			if ((now - _lastFlush) >= LogFlushSecs * Freq) { _lastFlush = now; ThreadPool.QueueUserWorkItem(_ => FlushLog(false)); }
		}

		private static string LogDirectory()
		{
			if (_logDir != null) return _logDir;
			string root;
			if (TestLogDir != null) root = TestLogDir;
			else
			{
				string cfg = null; try { cfg = FightRecConfig.Get?.RecordingFolder; } catch (Exception) { }
				root = FightRecFolder.Resolve(cfg, FightRecFolder.DefaultDir, out _);
			}
			_logDir = root == null ? "" : PerfLogFile.Dir(root);
			return _logDir;
		}

		/// <summary>Writes the queued lines (any thread). <paramref name="sync"/>: on the caller's thread, with the folder cleanup too.</summary>
		internal static void FlushLog(bool sync)
		{
			if (_q.IsEmpty) return;
			lock (_ioGate)
			{
				var lines = new List<string>();
				while (_q.TryDequeue(out string l)) lines.Add(l);
				if (lines.Count == 0 || _logBroken) return;
				try
				{
					string dir = LogDirectory(); if (string.IsNullOrEmpty(dir)) { _logBroken = true; return; }
					PerfConfig c = null; try { c = PerfConfig.Get; } catch (Exception) { }
					long maxFile = Math.Max(1, (c?.PerfLogMaxMB ?? 100)) * 1024L * 1024L / 4; // a day's file is cut into pieces of a quarter of the folder's cap
					LastLogFile = PerfLogFile.Append(dir, DateTime.Now, lines, maxFile);
					_ioFails = 0;
					if ((DateTime.UtcNow - _lastEnforce).TotalMinutes >= 10 || sync)
					{
						_lastEnforce = DateTime.UtcNow;
						PerfLogFile.Enforce(dir, c?.PerfLogDays ?? 7, (c?.PerfLogMaxMB ?? 100) * 1024L * 1024L, DateTime.Now, LastLogFile, out _);
					}
				}
				catch (Exception e)
				{
					foreach (string l in lines) _q.Enqueue(l); // keep them for the next try
					if (++_ioFails >= 3) { _logBroken = true; while (_q.TryDequeue(out _)) { } try { Levy.Instance?.Logger.Warn("[PerfAnalyzer] the perf log cannot be written (" + e.Message + "); logging to disk is off for this session"); } catch (Exception) { } }
				}
			}
		}

		private static string HeaderLine()
		{
			var mods = string.Join(",", ModLoader.Mods.Where(m => m.Name != "ModLoader").Select(m => "[" + PerfWindow.Esc(m.Name) + "," + PerfWindow.Esc(m.Version?.ToString()) + "]"));
			string gm = Main.GameMode switch { 0 => "Classic", 1 => "Expert", 2 => "Master", 3 => "Journey", _ => "Mode" + Main.GameMode };
			return "{\"k\":\"hdr\",\"t\":" + PerfWindow.Esc(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)) + ",\"levy\":" + PerfWindow.Esc(Levy.Instance?.Version?.ToString()) + ",\"tml\":" + PerfWindow.Esc(BuildInfo.tMLVersion?.ToString())
				+ ",\"cores\":" + Environment.ProcessorCount + ",\"os\":" + PerfWindow.Esc(System.Runtime.InteropServices.RuntimeInformation.OSDescription) + ",\"screen\":" + PerfWindow.Esc(Main.screenWidth + "x" + Main.screenHeight) + ",\"mode\":" + PerfWindow.Esc(gm)
				+ ",\"win\":" + WindowSecs + ",\"sampleUpdates\":" + SampleEvery + ",\"sampleFrames\":" + DrawSampleEvery + ",\"mods\":[" + mods + "]}";
		}

		// ------------------------------------------------------------------------------------------ for the self-check and the command
		/// <summary>The windows of the last <paramref name="secs"/> seconds, newest last.</summary>
		internal static List<PerfWindow> LastSeconds(double secs)
		{
			var o = new List<PerfWindow>(); double acc = 0;
			for (int i = Recent.Count - 1; i >= 0 && acc < secs; i--) { o.Insert(0, Recent[i]); acc += Recent[i].Secs; }
			return o;
		}

		/// <summary>Self-check: closes the window now (the check's own clock), and returns it.</summary>
		internal static PerfWindow TestClose() { long now = Stopwatch.GetTimestamp(); CloseWindow(now); return Last; }
		internal static void TestReset() { ResetWindow(Stopwatch.GetTimestamp()); Recent.Clear(); Last = null; _fight.Clear(); _inFight = false; }
		internal static void TestResetLog() { lock (_ioGate) { while (_q.TryDequeue(out _)) { } _logDir = null; _logBroken = false; _ioFails = 0; _announced = false; LastLogFile = null; _lastFlush = 0; } }
		internal static bool InFight => _inFight;
		/// <summary>Self-check: makes the next window fail, to show an error turns the analyzer off and nothing else.</summary>
		internal static bool TestFault; internal static bool LastTickSampled, LastTickLevy;
		internal static long DustThinned;
		internal static void TestWrapContent() => WrapContent();
		/// <summary>Self-check A/B only: attach just the pieces asked for (the entity IL edits, the Main detours), and with noSample never sample.</summary>
		internal static bool TestNoSample;
		internal static void TestPieces(bool il, bool main, bool noSample)
		{
			TestNoSample = noSample;
			if (il) AttachContext(); else DetachContext();
			if (main) AttachTiming(); else DetachTiming();
		}
		internal static void TestCommit(PerfWindow w) { Commit(w); }
		internal static void TestSetArmyCtx(bool on) { _army = on; }
		/// <summary>True while dust made now would be thinned (effects reduced or the screen full): the army context is set.</summary>
		internal static bool ThinCtx => EffectsReduced || DustPressure || GorePressure;
		internal static void TestTickPressure() => MeasurePressure();
	}
}
