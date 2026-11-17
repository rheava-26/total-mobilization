using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Globals;
using Levy.Common.Testing;

namespace Levy.Common.Perf
{
	/// <summary>Boot self-check of the performance analyzer ("[PerfAnalyzer] self-check pass=N fail=M", every load, on a client too): the pure checks of <see cref="PerfSelfTest"/> plus the settings' defaults and the test-only content's gate. Touches no game state.</summary>
	public class PerfCheck : ModSystem
	{
		public override void PostSetupRecipes()
		{
			var fails = new List<string>();
			int pass = 0;
			void Chk(bool c, string w) { if (c) pass++; else fails.Add(w); }
			try
			{
				pass += PerfSelfTest.Run(fails);
				object Def(string f) => ((DefaultValueAttribute)Attribute.GetCustomAttribute(typeof(PerfConfig).GetField(f), typeof(DefaultValueAttribute)))?.Value;
				Chk(Def("PerfLogAlwaysOn") is true, "the always-on log defaults to on");
				Chk(Def("LagPopup") is true && Def("LagPopupDontAskAgain") is false, "the popup defaults to on and not silenced");
				Chk(Def("LevyEffects") is LevyEffectsQuality q && q == LevyEffectsQuality.Full, "army effects default to full");
				Chk(Def("PerfLogDays") is int d && d >= 1 && Def("PerfLogMaxMB") is int mb && mb >= 5, "the log keeps some days and some megabytes by default");
				Chk(ModContent.TryFind("Levy/PerfTestNpc", out ModNPC _) == PerfTestTypes.Loaded && ModContent.TryFind("Levy/PerfTestLayer", out ModMapLayer _) == PerfTestTypes.Loaded, "the test-only NPC and map layer exist only when the self-check variable is set");
				Chk(PerfAnalysis.ReserveBankAvailable == false, "the reserve bank hook is off");
			}
			catch (Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim()); }
			if (fails.Count == 0) Mod.Logger.Info($"[PerfAnalyzer] self-check pass={pass} fail=0");
			else { Mod.Logger.Warn($"[PerfAnalyzer] self-check pass={pass} fail={fails.Count}"); foreach (var f in fails) Mod.Logger.Warn("[PerfAnalyzer] FAIL " + f); }
		}
	}

	/// <summary>
	/// THE ANALYZER IN A REAL WORLD ("[PerfAnalyzer]" lines; LEVY_SELFCHECK_WORLD=1 on a dedicated server, chained after the boss-fight cost check; LEVY_PERFAN_ONLY=1 runs it alone). The real game ticks step through the real detours with the analyzer attached.
	/// Attribution: a test NPC, a test projectile, a GlobalNPC hook, a map layer and an interface layer each burn a known number of microseconds, one after another, and the window the analyzer closes must put that cost on the right row (category,
	/// mod "Levy", the test type's name) at about the size burned, and nowhere else; an army shot lands on the army row; the global hook's time comes out of its NPC's owner. Third-party mods: with Calamity loaded, its global AI hooks are timed on real NPCs.
	/// Fight and popup: scripted fights go through the analyzer's state machine and the popup's rules. Fail-safe: a fault in the analyzer turns it off, the world keeps ticking. The cost with the analyzer on and off is measured inside <see cref="PerfBossCheck"/> (same scene, same boot).
	/// What a headless server cannot show: the draw hooks (nothing is drawn), a real map layer drawn by the game, a real interface layer list, and the popup's window.
	/// </summary>
	public class PerfWorldCheck : ModSystem
	{
		private static bool _armed, _done; private static int _calls, _pass, _fail; private static Mod _mod;
		private const int Cx = 1800, Cy = 300; private const string Commander = "PerfAnTester";
		private static readonly List<int> Spawned = new(), SpawnedProj = new();

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_PERFANWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (Environment.GetEnvironmentVariable("LEVY_PERFAN_ONLY") == "1") Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		private static bool _running;
		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; string tmp = null;
			try
			{
				global::Levy.Common.Util.LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player();
				tmp = Path.Combine(Path.GetTempPath(), "levy_perfan_world_" + Guid.NewGuid().ToString("N").Substring(0, 8));
				Run(tmp);
			}
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())), false); }
			finally
			{
				_running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server;
				try { Cleanup(); PerfTestTypes.Reset(); PerfAnalyzer.TestFault = false; PerfAnalyzer.TestRevive(); PerfAnalyzer.TestLog = false; PerfAnalyzer.TestReduced = false; PerfAnalyzer.ConfigChanged(); PerfAnalyzer.TestServer = false; PerfAnalyzer.TestWindowSecs = null; PerfAnalyzer.TestLogDir = null; PerfAnalyzer.TestResetLog(); PerfAnalyzer.TestReset(); PerfPopup.TestMode = false; PerfPopup.TestNow = null; PerfPopup.TestEnabled = null; PerfPopup.TestDontAsk = null; } catch (Exception) { }
				try { if (tmp != null && Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception) { }
			}
			_mod.Logger.Info($"[PerfAnalyzer] self-check pass={_pass} fail={_fail}");
			_mod.Logger.Info("[PerfAnalyzer] done");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[PerfAnalyzer] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[PerfAnalyzer] OBSERVED " + s);

		// ------------------------------------------------------------------------------------------ helpers
		private static void Keep()
		{
			Player p = Main.player[0]; p.active = true; p.dead = false; p.name = Commander; Main.dayTime = true; Main.time = 18000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500; p.statLife = p.statLifeMax2;
			p.position = new Vector2(Cx * 16f, Cy * 16f); p.velocity = Vector2.Zero;
		}

		private static void Tick(int n, bool removeNatural = true)
		{
			for (int i = 0; i < n; i++)
			{
				Keep();
				if (removeNatural) for (int k = 0; k < Main.maxNPCs; k++) { NPC o = Main.npc[k]; if (o.active && WorldStep.IsNaturalHostile(o)) o.active = false; }
				WorldStep.Update(); // Main.Update calls DoUpdate, so the analyzer's own detour times every step
			}
		}

		private static void Cleanup()
		{
			foreach (int i in Spawned) if (i >= 0 && i < Main.maxNPCs) Main.npc[i].active = false;
			foreach (int i in SpawnedProj) if (i >= 0 && i < Main.maxProjectiles) Main.projectile[i].active = false;
			Spawned.Clear(); SpawnedProj.Clear();
		}

		private static int SpawnNpc(int type, float dx = 0)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("PerfAn"), (int)((Cx + dx) * 16f), (int)((Cy - 10) * 16f), type);
			if (i >= 0 && i < Main.maxNPCs) { Spawned.Add(i); Main.npc[i].timeLeft = 99999; }
			return i;
		}

		private static int SpawnProj(int type, bool army, float dx = 0)
		{
			int keep = Main.myPlayer; Main.myPlayer = 0;
			int i = Projectile.NewProjectile(new EntitySource_WorldEvent("PerfAn"), new Vector2((Cx + dx) * 16f, (Cy - 20) * 16f), Vector2.Zero, type, 0, 0f, 0);
			Main.myPlayer = keep;
			if (i < 0 || i >= Main.maxProjectiles) return -1;
			SpawnedProj.Add(i);
			if (army) Main.projectile[i].GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex = 0;
			return i;
		}

		private static PerfWindow Window(Action load, int ticks, bool removeNatural = true)
		{
			PerfAnalyzer.TestReset(); PerfAnalyzer.TestWindowSecs = 1e9;
			load?.Invoke();
			Tick(ticks, removeNatural);
			var w = PerfAnalyzer.TestClose();
			return w;
		}

		private static PerfItem Row(PerfWindow w, string cat, string name) => w?.Top.FirstOrDefault(t => t.Cat == cat && t.Name == name);
		private static string Dump(PerfWindow w) => w == null ? "no window" : string.Join("; ", w.Top.Take(6).Select(t => $"{t.Cat} {t.Name} {t.Ms:0.0} ms x{t.N:0}"));

		// ------------------------------------------------------------------------------------------ the check
		private static void Run(string tmp)
		{
			PerfAnalyzer.TestServer = true; PerfAnalyzer.TestLog = true; PerfAnalyzer.TestLogDir = tmp; PerfAnalyzer.TestWindowSecs = 1e9;
			PerfAnalyzer.TestRevive(); PerfAnalyzer.ConfigChanged();
			Check("the analyzer attaches on a server when the check asks (timing on)", PerfAnalyzer.TimingOn);
			PerfAnalyzer.TestWrapContent();
			Note($"wrapped {PerfAnalyzer.WrappedLayers} map layer draws and {PerfAnalyzer.WrappedGlobals} global AI hooks; {PerfAnalyzer.FailedLayers + PerfAnalyzer.FailedGlobals} could not be wrapped; mods loaded: {string.Join(",", ModLoader.Mods.Where(m => m.Name != "ModLoader").Select(m => m.Name))}");
			Check("the test layer and Levy's own map layers were wrapped", PerfAnalyzer.WrappedLayers >= 2);
			Check("every map layer and global hook that could be wrapped was (none failed)", PerfAnalyzer.FailedLayers == 0 && PerfAnalyzer.FailedGlobals == 0);
			Check("the test GlobalNPC hook was wrapped", PerfAnalyzer.WrappedGlobals >= 1);

			// ---- who owns what
			Check("a base game NPC is filed under Terraria and a Levy NPC under Levy", PerfAnalyzer.NpcOwnerName(NPCID.BlueSlime) == "Terraria" && PerfAnalyzer.NpcOwnerName(ModContent.NPCType<PerfTestNpc>()) == "Levy");
			Check("a base game projectile is filed under Terraria and a Levy one under Levy", PerfAnalyzer.ProjOwnerName(ProjectileID.Bullet) == "Terraria" && PerfAnalyzer.ProjOwnerName(ModContent.ProjectileType<PerfTestProj>()) == "Levy");
			foreach (string modName in new[] { "Mechanization", "Warbands", "CalamityMod", "ThoriumMod" })
			{
				ModNPC mn = ModContent.GetContent<ModNPC>().FirstOrDefault(n => n.Mod.Name == modName);
				if (mn != null) Check($"an NPC of {modName} ({mn.Name}) is filed under {modName}", PerfAnalyzer.NpcOwnerName(mn.Type) == modName);
				ModProjectile mp = ModContent.GetContent<ModProjectile>().FirstOrDefault(n => n.Mod.Name == modName);
				if (mp != null) Check($"a projectile of {modName} ({mp.Name}) is filed under {modName}", PerfAnalyzer.ProjOwnerName(mp.Type) == modName);
			}

			// ---- NPC update
			PerfTestTypes.Reset(); PerfTestTypes.NpcUs = 1000; PerfTestTypes.NpcCalls = 0;
			var w1 = Window(() => SpawnNpc(ModContent.NPCType<PerfTestNpc>()), 210);
			double burned1 = PerfTestTypes.NpcCalls * 1.0; // ms, one per call
			var nt = Row(w1, PerfCat.NpcType, "Levy/PerfTestNpc"); var nm = Row(w1, PerfCat.Npc, "Levy");
			Note($"NPC phase: {PerfTestTypes.NpcCalls} calls of 1 ms; window updates {w1?.Updates} sampled {w1?.SampledUpdates}, timed NPC calls {PerfAnalyzer.NpcCalls}, context attached {PerfAnalyzer.AttachCount} time(s); window rows: {Dump(w1)}");
			Check("a test NPC that burns 1 ms an update shows as NPC Levy/PerfTestNpc at about the cost burned", nt != null && nt.Ms > 0.6 * burned1 && nt.Ms < 1.6 * burned1);
			Check("and the same cost on the Levy mod row", nm != null && nm.Ms > 0.6 * burned1 && nm.Ms < 1.6 * burned1);
			Check("and it is the biggest row of the window", w1 != null && w1.Top.Count > 0 && w1.Top[0].Name.StartsWith("Levy") && !w1.Top.Any(t => !t.Name.StartsWith("Levy") && t.Ms > 0.5 * (nm?.Ms ?? 1e9) && !PerfCat.IsDrill(t.Cat)));

			// ---- projectile update
			Cleanup(); PerfTestTypes.Reset(); PerfTestTypes.ProjUs = 600;
			var w2 = Window(() => SpawnProj(ModContent.ProjectileType<PerfTestProj>(), false), 210);
			double burned2 = PerfTestTypes.ProjCalls * 0.6;
			var pt = Row(w2, PerfCat.ProjType, "Levy/PerfTestProj"); var pm = Row(w2, PerfCat.Proj, "Levy");
			Note($"projectile phase: {PerfTestTypes.ProjCalls} calls of 0.6 ms; window rows: {Dump(w2)}");
			Check("a test projectile that burns 0.6 ms an update shows as projectile Levy/PerfTestProj at about the cost burned", pt != null && pt.Ms > 0.6 * burned2 && pt.Ms < 1.6 * burned2);
			Check("and on the Levy projectile row, not on the army row", pm != null && pm.Ms > 0.6 * burned2 && Row(w2, PerfCat.Army, "all") == null);

			// ---- an army shot
			Cleanup(); PerfTestTypes.Reset(); PerfTestTypes.ProjUs = 600;
			var w3 = Window(() => SpawnProj(ModContent.ProjectileType<PerfTestProj>(), true), 210);
			double burned3 = PerfTestTypes.ProjCalls * 0.6; var am = Row(w3, PerfCat.Army, "all");
			Note($"army shot phase: {PerfTestTypes.ProjCalls} calls; window rows: {Dump(w3)}");
			Check("the same projectile fired by the army lands on the army row", am != null && am.Ms > 0.6 * burned3 && am.Ms < 1.6 * burned3 && Row(w3, PerfCat.Proj, "Levy") == null);

			// ---- a global hook: its time is charged to its mod and taken out of the NPC's owner
			// A busy-wait is wall-clock time, so a world autosave or a GC pause that lands inside the window inflates it. Judge the best of three windows.
			PerfWindow w4 = null; double burned4 = 0; PerfItem gm = null, nm4 = null;
			for (int attempt = 0; attempt < 3; attempt++)
			{
				Cleanup(); PerfTestTypes.Reset(); PerfTestTypes.GlobUs = 400; PerfTestTypes.NpcUs = 0;
				w4 = Window(() => SpawnNpc(ModContent.NPCType<PerfTestNpc>()), 210);
				burned4 = PerfTestTypes.GlobCalls * 0.4; gm = Row(w4, PerfCat.Glob, "Levy"); nm4 = Row(w4, PerfCat.Npc, "Levy");
				if (gm != null && gm.Ms > 0.6 * burned4 && gm.Ms < 1.6 * burned4) break;
				Note($"global hook phase attempt {attempt + 1} was outside the band ({gm?.Ms:0.0} ms for {burned4:0.0} burned); trying again");
			}
			Note($"global hook phase: {PerfTestTypes.GlobCalls} calls of 0.4 ms; window rows: {Dump(w4)}");
			Check("a GlobalNPC hook that burns 0.4 ms an update shows as a global hook of Levy at about the cost burned", gm != null && gm.Ms > 0.6 * burned4 && gm.Ms < 1.6 * burned4);
			Check("and its time is taken out of the NPC's own row (that row stays small)", nm4 == null || nm4.Ms < 0.5 * gm.Ms);

			// ---- Levy's own buckets are ms per update whether or not the player's profiler is on. With it on (and with a recorded fight's in-fight profiling) the bucket timers run on every update, and the
			// window used to divide them by the sampled updates only: Eve's 11-17 log read "stats 37 ms" per update inside updates of 17 ms.
			foreach (bool user in new[] { false, true })
			{
				double ai = 0; PerfWindow wl = null;
				for (int attempt = 0; attempt < 3; attempt++)
				{
					Cleanup(); PerfTestTypes.Reset(); PerfTestTypes.NpcUs = 300; PerfTestTypes.NpcAsLevy = true;
					bool tsWas = global::Levy.Common.Util.ClientProf.TestServer;
					if (user) { global::Levy.Common.Util.ClientProf.TestServer = true; global::Levy.Common.Util.ClientProf.Enable(chat: false); }
					try { wl = Window(() => SpawnNpc(ModContent.NPCType<PerfTestNpc>()), 264); }
					finally { if (user) { global::Levy.Common.Util.ClientProf.Disable(); global::Levy.Common.Util.ClientProf.TestServer = tsWas; } PerfTestTypes.NpcAsLevy = false; }
					ai = wl?.Levy.FirstOrDefault(kv => kv.Key == "ai").Value ?? 0;
					if (ai > 0.15 && ai < 0.6) break;
				}
				Note($"Levy bucket phase ({(user ? "the player's profiler on" : "profiler off")}): ai {ai:0.000} ms per update for 0.3 ms burned an update; buckets {string.Join(", ", wl?.Levy.Select(kv => kv.Key + " " + kv.Value.ToString("0.000")) ?? Array.Empty<string>())}");
				Check($"Levy's own bucket reads the time per update ({(user ? "with the player's profiler on" : "profiler off")}: ai {ai:0.000} ms for 0.3 ms burned)", ai > 0.15 && ai < 0.6);
			}
			Cleanup(); PerfTestTypes.Reset();

			// ---- map layer, every call
			Cleanup(); PerfTestTypes.Reset(); PerfTestTypes.LayerUs = 1500;
			ModMapLayer layer = ModContent.GetContent<ModMapLayer>().FirstOrDefault(l => l is PerfTestLayer);
			var w5 = Window(() =>
			{
				var ctx = new MapOverlayDrawContext(Vector2.Zero, Vector2.Zero, null, 1f, 1f); string text = null;
				for (int i = 0; i < 40; i++) layer.Draw(ref ctx, ref text);
			}, 40);
			var mr = Row(w5, PerfCat.Map, "Levy/PerfTestLayer");
			Note($"map layer phase: {PerfTestTypes.LayerCalls} calls of 1.5 ms; window rows: {Dump(w5)}");
			Check("a map layer that burns 1.5 ms a frame shows as map layer Levy/PerfTestLayer, 40 calls at about 1.5 ms", mr != null && Math.Abs(mr.N - 40) < 0.5 && mr.Ms / mr.N > 1.4 && mr.Ms / mr.N < 2.3);
			Check("and it is the biggest row of the window", w5 != null && w5.Top.Count > 0 && w5.Top[0].Cat == PerfCat.Map && w5.Top[0].Name == "Levy/PerfTestLayer");
			if (mr != null) Note("how the report words it: " + PerfAnalysis.Describe(mr, PerfSpan.Of(new[] { w5 })));

			// ---- interface layer, every call
			Cleanup(); PerfTestTypes.Reset();
			var layers = new List<GameInterfaceLayer> { new LegacyGameInterfaceLayer("PerfTest: Busy", () => { PerfTestTypes.Spin(1000); return true; }, InterfaceScaleType.UI), new LegacyGameInterfaceLayer("Vanilla: PerfTest Idle", () => { PerfTestTypes.Spin(600); return true; }, InterfaceScaleType.UI) };
			var w6 = Window(() =>
			{
				int wrapped = PerfAnalyzer.WrapLayers(layers);
				FieldInfo f = typeof(LegacyGameInterfaceLayer).GetField("_drawMethod", BindingFlags.Instance | BindingFlags.NonPublic);
				for (int k = 0; k < 30; k++) foreach (var l in layers) ((GameInterfaceDrawMethod)f.GetValue(l))();
			}, 40);
			var ur = Row(w6, PerfCat.Ui, "PerfTest: Busy"); var ui2 = Row(w6, PerfCat.Ui, "Terraria: PerfTest Idle");
			Note($"interface layer phase; window rows: {Dump(w6)}");
			Check("an interface layer that burns 1 ms a frame is filed under its mod and name, 30 calls", ur != null && Math.Abs(ur.N - 30) < 0.5 && ur.Ms / ur.N > 0.9 && ur.Ms / ur.N < 2.0);
			Check("a vanilla layer (0.6 ms) is filed under Terraria", ui2 != null && Math.Abs(ui2.N - 30) < 0.5 && ui2.Ms / ui2.N > 0.5 && ui2.Ms / ui2.N < 1.4);
			Check("wrapping the same list twice does not wrap a layer twice", PerfAnalyzer.WrapLayers(layers) == 0);

			// ---- third-party mods: their global AI hooks are timed on real NPCs and projectiles
			Cleanup(); PerfTestTypes.Reset();
			bool cal = ModLoader.HasMod("CalamityMod");
			var w7 = Window(() =>
			{
				for (int i = 0; i < 24; i++) SpawnNpc(i % 2 == 0 ? NPCID.BlueSlime : NPCID.Zombie, i * 2);
				for (int i = 0; i < 40; i++) { int pj = SpawnProj(ProjectileID.Bullet, false, i); if (pj >= 0) { Main.projectile[pj].timeLeft = 100000; Main.projectile[pj].tileCollide = false; Main.projectile[pj].penetrate = -1; } } // pinned: a loaded mod's own hooks may remove a bullet at rest, and the window must still carry its count
			}, 280, false);
			long timedBefore = PerfAnalyzer.Timed;
			Note($"real NPCs and projectiles, global hooks of every loaded mod: {Dump(w7)}; wrapped hook calls timed {PerfAnalyzer.Timed}");
			var globs = w7.Top.Where(t => t.Cat == PerfCat.Glob).ToList();
			Note("global hook rows: " + (globs.Count == 0 ? "none" : string.Join("; ", globs.Select(t => $"{t.Name} {t.Ms:0.00} ms x{t.N:0}"))));
			if (cal) Check("with Calamity loaded its global AI hooks are timed on real NPCs (a row for CalamityMod)", globs.Any(t => t.Name == "CalamityMod" && t.N > 0));
			Check("the NPCs and projectiles of the base game are filed under Terraria", Row(w7, PerfCat.Npc, "Terraria") != null && Row(w7, PerfCat.Proj, "Terraria") != null);
			Check($"the window carries the counts and the context (NPCs {w7.Npcs}, projectiles {w7.Projs}, updates {w7.Updates})", w7.Npcs >= 20 && w7.Projs >= 30 && w7.Updates >= 280 && !string.IsNullOrEmpty(w7.ToJson()));

			// ---- dust thinning while effects are reduced
			PerfAnalyzer.TestReduced = true; PerfAnalyzer.ConfigChanged();
			Check("reduced effects are on", PerfAnalyzer.EffectsReduced);
			long d0 = PerfAnalyzer.DustThinned; PerfAnalyzer.TestSetArmyCtx(true);
			for (int i = 0; i < 30; i++) Dust.NewDust(new Vector2(Cx * 16f, Cy * 16f), 4, 4, DustID.Torch);
			long thinned = PerfAnalyzer.DustThinned - d0; PerfAnalyzer.TestSetArmyCtx(false);
			for (int i = 0; i < 30; i++) Dust.NewDust(new Vector2(Cx * 16f, Cy * 16f), 4, 4, DustID.Torch);
			Check("with effects reduced, two of every three dust particles a levy makes are dropped (" + thinned + " of 30)", thinned == 20);
			Check("and none outside a levy's update (" + (PerfAnalyzer.DustThinned - d0 - thinned) + ")", PerfAnalyzer.DustThinned - d0 == thinned);
			PerfAnalyzer.TestReduced = false; PerfAnalyzer.ConfigChanged();
			Check("full effects drop nothing", !PerfAnalyzer.EffectsReduced);

			// ---- a fault in the analyzer turns the analyzer off and nothing else
			Cleanup(); PerfAnalyzer.TestReset(); PerfAnalyzer.TestFault = true;
			Tick(40);
			PerfAnalyzer.TestClose();
			Check("a fault while closing a window turns the analyzer off", PerfAnalyzer.DeadWhy != null && PerfAnalyzer.DeadWhy.StartsWith("window"));
			ulong before = Main.GameUpdateCount; bool threw = false;
			try { Tick(30); } catch (Exception e) { threw = true; Note("the world step threw " + e.GetType().Name + ": " + e.Message); }
			Check("the world keeps ticking afterwards (30 ticks, " + (Main.GameUpdateCount - before) + " counted)", !threw && Main.GameUpdateCount - before >= 29);
			Check("the detours were removed", !PerfAnalyzer.TimingOn);
			PerfAnalyzer.TestFault = false; PerfAnalyzer.TestRevive(); PerfAnalyzer.ConfigChanged();
			Check("and a revive attaches them again", PerfAnalyzer.TimingOn);

			// ---- fights, the log and the popup, scripted through the analyzer's own state machine
			Fights();
		}

		private static PerfWindow Synth(string boss, double fps, double levy, string mod, double modShare) { var w = PerfAnalysis.Synth(8, fps, 60, levy, mod, modShare, boss); w.Utc = DateTime.UtcNow; return w; }

		private static void Fights()
		{
			PerfAnalyzer.TestReset(); PerfAnalyzer.TestResetLog(); PerfAnalyzer.TestLogDir = Path.Combine(Path.GetTempPath(), "levy_perfan_fights_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			PerfPopup.TestMode = true; PerfPopup.TestEnabled = true; PerfPopup.TestDontAsk = false; DateTime t0 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc); PerfPopup.TestNow = t0;
			PerfAnalyzer.TestSetBossNear(false);
			void Run(string boss, double fps, double levy, string mod, double share, int windows = 5) { for (int i = 0; i < windows; i++) PerfAnalyzer.TestCommit(Synth(boss, fps, levy, mod, share)); PerfAnalyzer.TestCommit(Synth("", 60, 0.1, null, 0)); }
			int shown0 = PerfPopup.Shown;
			Run("Plantera", 60, 0.1, "CalamityMod", 0.1);
			Check("a smooth fight ends with a verdict and no popup", PerfAnalyzer.LastVerdict != null && !PerfAnalyzer.LastVerdict.Laggy && PerfPopup.Shown == shown0);
			Run("Plantera", 30, 0.45, "CalamityMod", 0.1);
			Check("a laggy fight in which the army was big shows the popup with the army's options", PerfAnalyzer.LastVerdict.Kind == LagKind.Levy && PerfPopup.Shown == shown0 + 1 && PerfPopup.Current != null && string.Join(",", PerfPopup.Current.Options) == "LowerLevyEffects,ItsFine,DontAskAgain,OpenSettings");
			Run("Golem", 30, 0.45, "CalamityMod", 0.1);
			Check("a second laggy fight a minute later shows nothing (" + PerfPopup.LastRefusal + ")", PerfPopup.Shown == shown0 + 1 && PerfPopup.LastRefusal == "rate limit");
			PerfPopup.TestNow = t0.AddMinutes(11); PerfAnalyzer.TestSetBossNear(true);
			Run("Golem", 30, 0.45, "CalamityMod", 0.1);
			Check("never while a boss is near (" + PerfPopup.LastRefusal + ")", PerfPopup.Shown == shown0 + 1 && PerfPopup.LastRefusal == "a fight is on");
			PerfAnalyzer.TestSetBossNear(false);
			Run("The Twins", 30, 0.1, "CalamityMod", 0.6);
			Check("a laggy fight that was another mod's names it and offers the short list", PerfAnalyzer.LastVerdict.Kind == LagKind.OtherMod && PerfPopup.Shown == shown0 + 2 && PerfPopup.Current.Detail[0] == "Most of the cost came from CalamityMod." && string.Join(",", PerfPopup.Current.Options) == "ItsFine,DontAskAgain,OpenSettings");
			PerfPopup.TestNow = t0.AddMinutes(30); PerfPopup.TestDontAsk = true;
			Run("Golem", 30, 0.45, "CalamityMod", 0.1);
			Check("after don't ask again nothing shows (" + PerfPopup.LastRefusal + ")", PerfPopup.Shown == shown0 + 2 && PerfPopup.LastRefusal == "don't ask again");
			PerfPopup.TestDontAsk = false; PerfPopup.TestEnabled = false; PerfPopup.TestNow = t0.AddMinutes(60);
			Run("Golem", 30, 0.45, "CalamityMod", 0.1);
			Check("nor with the setting off (" + PerfPopup.LastRefusal + ")", PerfPopup.Shown == shown0 + 2 && PerfPopup.LastRefusal == "turned off");
			PerfPopup.TestEnabled = true;
			// the fight lines and the windows reach the log, in order, readable by the tool
			PerfAnalyzer.FlushLog(true);
			string dir = PerfLogFile.Dir(PerfAnalyzer.TestLogDir);
			var ws = PerfLogFile.ReadAll(dir);
			string[] lines = Directory.Exists(dir) ? Directory.GetFiles(dir).SelectMany(File.ReadAllLines).ToArray() : Array.Empty<string>();
			Check($"every window and every fight reached the log file ({ws.Count} windows, {lines.Count(l => l.StartsWith("{\"k\":\"fight\""))} fight lines, {lines.Count(l => l.StartsWith("{\"k\":\"hdr\""))} header)", ws.Count >= 36 && lines.Count(l => l.StartsWith("{\"k\":\"fight\"")) >= 6 && lines.Count(l => l.StartsWith("{\"k\":\"hdr\"")) == 1);
			Note("the log file: " + PerfAnalyzer.LastLogFile + " (" + (lines.Length) + " lines); a fight line: " + lines.LastOrDefault(l => l.StartsWith("{\"k\":\"fight\"")));
			try { Directory.Delete(PerfAnalyzer.TestLogDir, true); } catch (Exception) { }
		}
	}
}
