using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HOOKS LAB (feasibility study for grappling-hook movement; harness-only, inert unless LEVY_HARNESS and a LEVY_HARNESS_LAB spec whose first kind starts with "hook").
	///
	/// QUESTION: can a soldier use the REAL hook item from its Dummy's gear (QuickGrapple, the hook projectile's own aiStyle-7 AI, vanilla Player.GrappleMovement) and really swing / reel / release with momentum?
	///
	/// PROTOTYPE (all inside the "owner frame" of OwnerLab: the Dummy stands in a player slot, Main.myPlayer is faked to it, the cursor is aimed):
	///   CAST   : frame { miscEquips[4] = hook; controlHook / releaseHook; Player.QuickGrapple() }. The hook projectile is owned by the slot and tagged (HookTag) with the soldier.
	///   LIVE   : every tagged hook Projectile.Update runs in the same frame, after the Dummy is moved onto the soldier (the hook reads Main.player[owner].MountedCenter and fills grappling[] / grapCount).
	///   REEL   : GlobalNPC.PreAI of the soldier, while grappling[0] >= 0: frame { sync Dummy from NPC; Player.GrappleMovement(); } then NPC.velocity = Dummy.velocity, NPC.noGravity while hooked, and the
	///            end-of-Player.Update reset (grappling[0] = -1, grapCount = 0) that vanilla does and the Dummy never would. The soldier's own AI is skipped while reeling (and, with after=ballistic, until it lands).
	///   RELEASE: vanilla's own path (controlJump + releaseJump inside GrappleMovement, which calls RemoveAllGrapplingHooks) or rel mode "drop" (RemoveAllGrapplingHooks directly).
	///
	/// SPEC (LEVY_HARNESS_LAB): ';'-separated  kind|item|N|ticks|flags     kind: hook | hooklist (dump every hook item)    item: V:Name | T:Name | C:Name | M:Mod/Name | ALL (every hook item)
	///   course (tiles): ceil=H (slab, underside H above the floor)  gap=G (G-tile pit right of the soldier, beam over it at beamh=14)  wall=D (3-thick wall D right)  ledge=H,ledgex=X (block whose top is H up, from X right)  free (nothing)
	///   aim=dx:dy[/dx:dy..] tile offset from the soldier's feet tile (defaults per course)   press=n (casts, 8 ticks apart)   at=T (first cast tick, 3)
	///   rel=none|T|y:H|x:D|arr|arr:T (release rule: T ticks after latch / risen H tiles / moved D tiles / within 40px of the anchor for 20 or T ticks)   relmode=jump (vanilla: controlJump+releaseJump) | drop (RemoveAllGrapplingHooks)
	///   after=ballistic (soldier AI skipped while airborne after a release; vx drag 0.1/tick like a player, drag=0 to disable) | ai (the soldier's own Movement_Update runs)   goal=dx (gives the body a MoveGoal after a release)
	///   steer=up,left,.. (controls for Static Hook)   slot=255 (all soldiers own slot 255)   nonet (no NetLock)   noreset (skip the grappling[] reset)   noframe (hook projectiles update without the owner frame)   nohook   killat=T   trace
	/// The arena floor is moved below the 'space' gravity band (floor row = LEVY_HOOKS_FLOOR or spaceBand + 75); at the old range height (row 120) NPC gravity is scaled to 0.25x.
	/// OUT: LEVY_HARNESS_OUT (summary TSV) and OUT.trace.tsv (per-tick trace).
	/// </summary>
	public static class HooksLab
	{
		internal static bool Active, Running;
		private static string _spec = "";
		internal static readonly object NetLock = new object();
		internal static volatile int FrameDepth;
		internal static HS CastingSol;

		internal static bool Claims(string spec) => !string.IsNullOrEmpty(spec) && spec.TrimStart().StartsWith("hook", StringComparison.OrdinalIgnoreCase);
		internal static void ReadEnv()
		{
			string v = Environment.GetEnvironmentVariable("LEVY_HARNESS_LAB");
			_spec = v ?? "";
			Active = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS")) && Claims(_spec);
		}

		internal sealed class HS
		{
			public SoldierRecord Rec; public int Idx = -1; public SoldierNPC Body; public int Slot; public Item Hook;
			public Vector2 Start;                       // NPC.Bottom at run start
			public int Casts, CastOk, FirstCastTick = -1, LatchTick = -1, ReleaseTick = -1, LandTick = -1, ArriveTick = -1, ArrivedFor;
			public bool PendingRelease, Ballistic, Released;
			public Vector2 RelPos, RelVel, LandPos, LatchTile; public float PeakUp, MaxSpeed, MaxMismatch;
			public int HookType = -1; public string HookName = "";
			public float MaxReach, ReachAtFlip; public string Why = ""; public int PreTrue; public Vector2 PreVel; public bool PreSet; public float AiDvx, AiDvy; public int AiRuns; public int ReelTicks, Adopted, LastHookProjTick = -10; public int NetUpd; public int HookKills, HookKillsExpected;
			public Vector2 VelAfter5; public int Rel5 = -1; public float MinDistToAnchor = float.MaxValue; public int MaxGrapCount;
		}

		internal sealed class Sc
		{
			public string Kind = "hook", ItemKey = ""; public int N = 1, Ticks = 300; public Dictionary<string, string> F = new();
			public int ItemType = -1; public string ItemName = "";
			public List<HS> Sols = new(); public int Tick; public bool Done; public string Note = "";
			public List<Vector2> Aims = new();    // world px
			public int Press = 1, At = 3;
			public string Rel = "none", RelMode = "jump", After = "ballistic";
			public bool Slot255, UseNetLock = true, NoReset, NoFrame, Trace, NoHook, Nolimit;
			public int CeilH, GapG, WallD, BeamH = 14, LedgeH;
			public long StepTs, StepInnerTs, ProjOvhTs, ProjOrigTs, WholeTs; public int StepCalls, ProjCalls, WholeSamples;
			public long CastTs; public int CastCalls;
			public int NetClears, NetPasses; public Dictionary<string, int> Kills = new(), Sends = new(); public List<string> Exc = new();
			public int HooksSpawned, ProjDeadInactive;
		}

		private static List<Sc> _queue = new(); private static int _qi, _stage, _stageTick; private static Sc _sc; private static CombatHarness _h; private static readonly List<string> _rows = new();
		private static readonly StringBuilder _trace = new(); private static long _lastTick; private static int _scIdx;
		private static string RowHeader = "idx\tkind\titem\tname\thookProj\tN\tcastOk/casts\tlatchTicksAfterCast\tlatchTile(dx:dy)\tarriveTick\trelTick\trelVel(vx,vy)\trelPos(dxT,dyT)\tpeakUpT\tlandTick\tlandPos(dxT,dyT)\tvelAfter5\tmaxSpeed\tmaxGrapCount\treelTicks\tnetUpd\thookKills(expected/total)\tkillKeys\tstepMsPerSolTick\tinnerGrappleMs\tprojFrameOvhMs\tprojOrigMs\twholeMsTick\tmaxDummyNpcMismatchPx\tnetClears\tnetPasses\tsends\texceptions\tnote";
		private static string TraceHeader = "idx\ttick\tphase\tnpcDxT\tnpcDyT\tvx\tvy\tdummyDx\tdummyDy\tgrapCount\tg0\thookState\thooksAlive\tnoGrav";

		internal static void Boot(CombatHarness h)
		{
			_h = h; _qi = 0; _stage = 0; _stageTick = 0; _sc = null; Running = true; _scIdx = 0;
			PickFloor(); _queue = Parse(_spec);
			h.Mod.Logger.Info($"HOOKSLAB: {_queue.Count} scenarios queued");
			_trace.Clear(); _trace.AppendLine(TraceHeader);
		}

		private static List<Sc> Parse(string spec)
		{
			var l = new List<Sc>();
			foreach (var raw in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				var p = raw.Split('|');
				if (p.Length < 2) continue;
				var s = new Sc { Kind = p[0], ItemKey = p[1] };
				if (p.Length > 2 && int.TryParse(p[2], out int n)) s.N = Math.Clamp(n, 1, 60);
				if (p.Length > 3 && int.TryParse(p[3], out int t)) s.Ticks = Math.Clamp(t, 30, 6000);
				if (p.Length > 4) foreach (var f in p[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					int eq = f.IndexOf('=');
					if (eq > 0) s.F[f.Substring(0, eq)] = f.Substring(eq + 1); else s.F[f] = "1";
				}
				string G(string k, string d) => s.F.TryGetValue(k, out var v) ? v : d;
				int GI(string k, int d) => int.TryParse(G(k, ""), out int v) ? v : d;
				s.CeilH = GI("ceil", 0); s.GapG = GI("gap", 0); s.WallD = GI("wall", 0); s.LedgeH = GI("ledge", 0); s.BeamH = GI("beamh", 14);
				s.Press = Math.Max(1, GI("press", 1)); s.At = GI("at", 3);
				s.Rel = G("rel", "none"); s.RelMode = G("relmode", "jump"); s.After = G("after", "ballistic");
				s.Slot255 = G("slot", "v") == "255"; s.UseNetLock = !s.F.ContainsKey("nonet"); s.NoReset = s.F.ContainsKey("noreset"); s.NoFrame = s.F.ContainsKey("noframe");
				s.Trace = s.F.ContainsKey("trace") || s.N <= 1; s.NoHook = s.F.ContainsKey("nohook");
				if (s.CeilH == 0 && s.GapG == 0 && s.WallD == 0 && s.LedgeH == 0 && !s.F.ContainsKey("free")) s.CeilH = 15;
				if (s.Kind == "hook" && s.ItemKey == "ALL")
				{
					foreach (int it in AllHookItems())
					{
						var c = Clone(s); c.ItemKey = "#" + it; l.Add(c);
					}
				}
				else l.Add(s);
			}
			return l;
		}

		private static Sc Clone(Sc s)
		{
			var c = new Sc { Kind = s.Kind, ItemKey = s.ItemKey, N = s.N, Ticks = s.Ticks, F = new Dictionary<string, string>(s.F), CeilH = s.CeilH, LedgeH = s.LedgeH, GapG = s.GapG, WallD = s.WallD, BeamH = s.BeamH, Press = s.Press, At = s.At, Rel = s.Rel, RelMode = s.RelMode, After = s.After, Slot255 = s.Slot255, UseNetLock = s.UseNetLock, NoReset = s.NoReset, NoFrame = s.NoFrame, Trace = s.Trace, NoHook = s.NoHook };
			return c;
		}

		private static IEnumerable<int> AllHookItems()
		{
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				Item it = ContentSamples.ItemsByType[t];
				if (it.shoot > 0 && it.shoot < Main.projHook.Length && Main.projHook[it.shoot] && !string.IsNullOrEmpty(it.Name)) yield return t;
			}
		}

		private static string ItemKeyName(int type)
		{
			var mi = ItemLoader.GetItem(type);
			if (mi != null) return mi.Mod.Name + "/" + mi.Name;
			return ItemID.Search.TryGetName(type, out string n) ? n : "item" + type;
		}

		private static int ResolveItem(string key)
		{
			try
			{
				if (key.StartsWith("#") && int.TryParse(key.Substring(1), out int tt)) return tt;
				if (key.Length < 3) return -1;
				string kind = key.Substring(0, 2), name = key.Substring(2);
				if (kind == "V:") return ItemID.Search.ContainsName(name) ? ItemID.Search.GetId(name) : -1;
				if (kind == "T:") return ModContent.TryFind<ModItem>("ThoriumMod/" + name, out var mi) ? mi.Type : -1;
				if (kind == "M:") return ModContent.TryFind<ModItem>(name, out var m2) ? m2.Type : -1;
				if (kind == "C:") return ModContent.TryFind<ModItem>("CalamityMod/" + name, out var m3) ? m3.Type : -1;
			}
			catch (Exception) { }
			return -1;
		}

		// ------------------------------------------------------------------------------------------------
		// driver
		// ------------------------------------------------------------------------------------------------
		internal static void Tick(CombatHarness h)
		{
			_h = h;
			long now = Stopwatch.GetTimestamp();
			if (_sc != null && _stage == 1 && _lastTick != 0) { _sc.WholeTs += now - _lastTick; _sc.WholeSamples++; }
			_lastTick = now;
			if (_sc == null)
			{
				if (_qi >= _queue.Count) { Finish(); return; }
				_sc = _queue[_qi]; _stage = 0; _stageTick = 0;
			}
			try
			{
				switch (_stage)
				{
					case 0:
						if (_stageTick == 0) Setup(_sc);
						Keep(_sc);
						if (++_stageTick >= 30 || _sc.Done) { _stage = 1; _stageTick = 0; _lastTick = 0; Begin(_sc); }
						break;
					case 1:
						Keep(_sc);
						RunTick(_sc);
						if (++_sc.Tick >= _sc.Ticks || _sc.Done) { _stage = 2; _stageTick = 0; }
						break;
					case 2:
						if (_stageTick == 0) Teardown(_sc);
						if (++_stageTick >= 6) { _rows.Add(Row(_sc, _qi)); Write(); _sc = null; _qi++; }
						break;
				}
			}
			catch (Exception e)
			{
				_h.Mod.Logger.Warn("HOOKSLAB: driver exception: " + e);
				if (_sc != null) { _sc.Exc.Add("driver:" + e.GetType().Name + ":" + e.Message); try { Teardown(_sc); } catch { } _rows.Add(Row(_sc, _qi)); Write(); _sc = null; _qi++; _stage = 0; }
			}
		}

		private static void Finish()
		{
			Running = false; Write();
			_h.LabFinish($"hooks lab complete, {_rows.Count} rows");
		}

		internal static bool InScenario => Running && _sc != null && _stage == 1;
		internal static bool NetLockOn => _sc == null || _sc.UseNetLock;

		// ------------------------------------------------------------------------------------------------
		// course
		// ------------------------------------------------------------------------------------------------
		private static int Cx => _h.Cx;
		internal static int Fy = 120;
		private static void PickFloor()
		{
			float num = Main.maxTilesX / 4200f; num *= num;
			int spaceBand = (int)(60f + 10f * num + Main.worldSurface / 6.0) + 2;      // above this row NPC / player gravity is scaled down (UpdateNPC_UpdateGravity)
			int want = spaceBand + 75;
			int fy = int.TryParse(Environment.GetEnvironmentVariable("LEVY_HOOKS_FLOOR"), out int f) ? f : want;
			Fy = Math.Clamp(fy, 100, Main.maxTilesY - 120);
			_h.Mod.Logger.Info($"HOOKSLAB: world {Main.maxTilesX}x{Main.maxTilesY} surface={Main.worldSurface:0} spaceBand<{spaceBand} floorRow={Fy}");
		}
		private static void Fill(int x0, int y0, int x1, int y1, bool solid)
		{
			for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++)
			{
				if (x < 5 || y < 5 || x >= Main.maxTilesX - 5 || y >= Main.maxTilesY - 5) continue;
				Tile t = Main.tile[x, y];
				t.HasTile = solid; t.WallType = 0; t.LiquidAmount = 0; t.TileFrameX = 0; t.TileFrameY = 0;
				if (solid) { t.TileType = TileID.GrayBrick; t.BlockType = Terraria.ID.BlockType.Solid; } else t.TileType = 0;
			}
		}

		private static int LedgeX(Sc s) => s.F.TryGetValue("ledgex", out var v) && int.TryParse(v, out int x) ? x : 4;
		private static void BuildCourse(Sc s)
		{
			int cx = Cx, fy = Fy;
			Fill(cx - 96, (Fy - 70), cx + 96, fy - 1, false);          // wipe the last course
			Fill(cx - 96, fy, cx + 96, fy + 3, true);                              // floor
			Fill(cx - 96, fy + 4, cx + 96, fy + 25, true);                         // deep fill so a pit has a bottom
			if (s.CeilH > 0) Fill(cx - 70, fy - 1 - s.CeilH - 2, cx + 70, fy - 1 - s.CeilH, true);
			if (s.GapG > 0)
			{
				Fill(cx + 2, fy, cx + 1 + s.GapG, fy + 20, false);                  // pit, 21 tiles deep
				Fill(cx - 3, fy - 1 - s.BeamH - 2, cx + s.GapG + 10, fy - 1 - s.BeamH, true); // beam over the gap
			}
			if (s.WallD > 0) Fill(cx + s.WallD, (Fy - 70), cx + s.WallD + 2, fy - 1, true);
			if (s.LedgeH > 0) Fill(cx + LedgeX(s), fy - s.LedgeH, cx + 60, fy - 1, true);               // a block whose top is LedgeH tiles above the floor
			// aims (world px). dx:dy are TILE offsets from the soldier's feet tile (cx, floor row); defaults per course
			string a = s.F.TryGetValue("aim", out var av) ? av : null;
			if (a == null)
			{
				if (s.CeilH > 0 && s.GapG == 0 && s.WallD == 0) a = $"0:{-(s.CeilH + 1)}";
				else if (s.GapG > 0) a = $"{Math.Min(12, s.GapG - 2)}:{-(s.BeamH + 1)}";
				else if (s.WallD > 0) a = $"{s.WallD}:-10";
				else if (s.LedgeH > 0) a = $"{LedgeX(s)}:{-s.LedgeH}";
				else a = "10:-10";
			}
			foreach (var part in a.Split('/', StringSplitOptions.RemoveEmptyEntries))
			{
				var xy = part.Split(':');
				float dx = float.Parse(xy[0], CultureInfo.InvariantCulture), dy = float.Parse(xy[1], CultureInfo.InvariantCulture);
				s.Aims.Add(new Vector2((cx + dx) * 16f + 8f, (fy + dy) * 16f + 8f));
			}
		}

		private static void Setup(Sc s)
		{
			_h.LabClearAll();
			s.ItemType = s.Kind == "hooklist" ? 0 : ResolveItem(s.ItemKey);
			s.ItemName = s.ItemType > 0 ? ItemKeyName(s.ItemType) : s.ItemKey;
			if (s.Kind == "hooklist") { RunList(s); s.Done = true; s.Ticks = 0; return; }
			if (s.ItemType <= 0) { s.Note = "ITEM NOT FOUND"; s.Done = true; s.Ticks = 0; return; }
			BuildCourse(s);
			int cx = Cx;
			Player c = Main.player[0];
			c.active = true; c.dead = false; c.name = "LevyTester"; c.statLifeMax = c.statLifeMax2 = 500; c.statLife = 500;
			c.position = new Vector2((cx - 90) * 16f, Fy * 16f - c.height); c.velocity = Vector2.Zero;
			for (int k = 0; k < s.N; k++)
			{
				var rec = _h.LabMakeRecord("Pvt. Lab" + k);
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold; rec.HoldFire = true; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec);
				float x = cx * 16f + 8f + (k - s.N / 2) * 48f;
				int idx = RosterSystem.Instance.SpawnBody(rec, new Vector2(x, Fy * 16f));
				var sol = new HS { Rec = rec, Idx = idx, Slot = 100 + k };
				if (idx >= 0) { sol.Body = Main.npc[idx].ModNPC as SoldierNPC; if (sol.Body != null) sol.Body.NPC.Bottom = new Vector2(x, Fy * 16f); }
				sol.Hook = new Item(); sol.Hook.SetDefaults(s.ItemType); sol.HookType = sol.Hook.shoot; sol.HookName = ProjName(sol.Hook.shoot);
				s.Sols.Add(sol);
			}
		}

		private static string ProjName(int t)
		{
			var mp = ProjectileLoader.GetProjectile(t);
			if (mp != null) return mp.Mod.Name + "/" + mp.Name;
			return ProjectileID.Search.TryGetName(t, out string n) ? n : "proj" + t;
		}

		private static void Keep(Sc s)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = "LevyTester"; p.statLife = p.statLifeMax2 = 500;
			p.position = new Vector2((Cx - 90) * 16f, Fy * 16f - p.height); p.velocity = Vector2.Zero;
			if (_stage == 0)
				foreach (var sol in s.Sols) if (sol.Body != null && sol.Body.NPC.active) { sol.Body.Record.CurrentHp = sol.Body.Record.MaxHp; sol.Body.NPC.life = sol.Body.NPC.lifeMax; }
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC) continue;
				if (!n.friendly || n.type == NPCID.TargetDummy) n.active = false;
			}
		}

		private static void Begin(Sc s)
		{
			foreach (var sol in s.Sols)
			{
				if (sol.Body == null) continue;
				sol.Start = sol.Body.NPC.Bottom;
				sol.Body.Dummy.miscEquips[4] = sol.Hook.Clone();
			}
		}

		private static void Teardown(Sc s)
		{
			foreach (var sol in s.Sols) { try { if (sol.Body?.NPC != null) sol.Body.NPC.noGravity = false; } catch { } }
			_h.LabClearAll();
			for (int k = 0; k < 64; k++) if (100 + k < 255 && Main.player[100 + k] != null && Main.player[100 + k].name != null && Main.player[100 + k].name.StartsWith("Pvt. Lab")) Main.player[100 + k] = new Player();
			EnemyTargeting_Restore();
		}

		private static void EnemyTargeting_Restore() { global::Levy.Common.Targeting.EnemyTargeting.EnsureSlotRestored(); }

		// ------------------------------------------------------------------------------------------------
		// the owner frame
		// ------------------------------------------------------------------------------------------------
		internal struct Fr { public int My; public Player Bk; public int Slot; public bool Locked; public HS Sol; }

		private static Fr Enter(Sc s, HS sol, bool full)
		{
			var f = new Fr { My = Main.myPlayer, Sol = sol };
			Player d = sol.Body.Dummy; NPC n = sol.Body.NPC;
			if (full) sol.Body.SyncDummyTransform();
			else { d.position = n.position; d.velocity = n.velocity; d.direction = n.direction == 0 ? 1 : n.direction; d.gravDir = 1f; d.active = true; d.dead = false; }
			int slot = s.Slot255 ? 255 : sol.Slot; f.Slot = slot;
			if (s.UseNetLock && slot < 255) { System.Threading.Monitor.Enter(NetLock); f.Locked = true; }
			d.whoAmI = slot; f.Bk = Main.player[slot]; Main.player[slot] = d; Main.myPlayer = slot; FrameDepth++;
			return f;
		}

		private static void Leave(Sc s, HS sol, Fr f)
		{
			Main.myPlayer = f.My; FrameDepth--;
			Player d = sol.Body.Dummy;
			if (!d.active) { s.NetClears++; d.active = true; }
			Main.player[f.Slot] = f.Bk ?? new Player();
			d.whoAmI = SoldierNPC.DummySlot;
			if (f.Locked) System.Threading.Monitor.Exit(NetLock);
		}

		private static HS FindSol(Sc s, int npcIdx)
		{
			for (int i = 0; i < s.Sols.Count; i++) if (s.Sols[i].Idx == npcIdx) return s.Sols[i];
			return null;
		}

		// ------------------------------------------------------------------------------------------------
		// per-tick logic (end of tick)
		// ------------------------------------------------------------------------------------------------
		private static bool OnFloor(NPC n) => n.velocity.Y == 0f && Collision.SolidCollision(new Vector2(n.position.X, n.position.Y + n.height), n.width, 2);

		private static Projectile FirstHook(Sc s, HS sol)
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && Main.projHook[p.type] && p.TryGetGlobalProjectile(out HookTag t) && t.Soldier == sol.Idx) return p;
			}
			return null;
		}

		private static int HooksAlive(HS sol, out int latched)
		{
			int n = 0; latched = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && Main.projHook[p.type] && p.TryGetGlobalProjectile(out HookTag t) && t.Soldier == sol.Idx) { n++; if (p.ai[0] == 2f) latched++; }
			}
			return n;
		}

		private static void RunTick(Sc s)
		{
			for (int si = 0; si < s.Sols.Count; si++)
			{
				var sol = s.Sols[si];
				if (sol.Body == null || !sol.Body.NPC.active) continue;
				NPC n = sol.Body.NPC; Player d = sol.Body.Dummy;
				// casts
				if (!s.NoHook)
					for (int ci = 0; ci < s.Press; ci++)
						if (s.Tick == s.At + ci * 8) Cast(s, sol, s.Aims[Math.Min(ci, s.Aims.Count - 1)] + new Vector2(sol.Start.X - (Cx * 16f + 8f), 0f));
				// sample
				int alive = (si == 0 || s.Trace) ? HooksAlive(sol, out _) : 0;
				Vector2 mis = d.position - n.position;
				if (mis.Length() > sol.MaxMismatch && s.Tick > 0) sol.MaxMismatch = mis.Length();
				if (n.velocity.Length() > sol.MaxSpeed) sol.MaxSpeed = n.velocity.Length();
				if (d.grapCount > sol.MaxGrapCount) sol.MaxGrapCount = d.grapCount;
				float up = (sol.Start.Y - n.Bottom.Y) / 16f; if (up > sol.PeakUp) sol.PeakUp = up;
				var hk = si == 0 ? FirstHook(s, sol) : null;
				if (hk != null) { float rd = Vector2.Distance(hk.Center, n.Center); if (hk.ai[0] == 0f && rd > sol.MaxReach) sol.MaxReach = rd; if (hk.ai[0] == 1f && sol.ReachAtFlip == 0f) sol.ReachAtFlip = rd; }
				if (hk != null && hk.ai[0] == 2f && sol.LatchTick < 0 && s.Tick >= 0) { sol.LatchTick = s.Tick; sol.LatchTile = new Vector2(hk.Center.X / 16f - Cx, hk.Center.Y / 16f - Fy); }
				if (hk != null && hk.ai[0] == 2f) { float dd = Vector2.Distance(n.Center, hk.Center); if (dd < sol.MinDistToAnchor) sol.MinDistToAnchor = dd; if (dd < 40f) { sol.ArrivedFor++; if (sol.ArriveTick < 0) sol.ArriveTick = s.Tick; } else sol.ArrivedFor = 0; }
				if (s.F.TryGetValue("goal", out var gv) && sol.Released && sol.LandTick < 0 && int.TryParse(gv, out int gdx)) { sol.Body.HasMoveGoal = true; sol.Body.MoveGoal = new Vector2((Cx + gdx) * 16f + 8f, Fy * 16f); }
				if (s.F.TryGetValue("killat", out var kv) && int.TryParse(kv, out int kt) && si == 0)
				{
					if (s.Tick == kt) { try { n.SimpleStrikeNPC(99999, 0, false, 0f, null, false, 0f, true); } catch (Exception e) { s.Exc.Add("kill:" + e.Message); } }
					if (s.Tick == kt + 6) s.Note += $"afterDeath: npcActive={n.active} life={n.life} hooksAlive={HooksAlive(sol, out _)} ";
				}
				// release rules
				if (sol.LatchTick >= 0 && !sol.Released && !sol.PendingRelease)
				{
					bool rel = false; string r = s.Rel;
					if (r == "none") rel = false;
					else if (r == "arr") rel = sol.ArrivedFor >= 20;
					else if (r.StartsWith("arr:")) rel = sol.ArrivedFor >= int.Parse(r.Substring(4));
					else if (r.StartsWith("y:")) rel = up >= float.Parse(r.Substring(2), CultureInfo.InvariantCulture);
					else if (r.StartsWith("x:")) rel = Math.Abs((n.Center.X - sol.Start.X) / 16f) >= float.Parse(r.Substring(2), CultureInfo.InvariantCulture);
					else if (int.TryParse(r, out int rt)) rel = s.Tick - sol.LatchTick >= rt;
					if (rel) sol.PendingRelease = true;
				}
				if (sol.Released && sol.LandTick < 0 && s.Tick > sol.ReleaseTick + 2 && OnFloor(n)) { sol.LandTick = s.Tick; sol.LandPos = n.Bottom; sol.Ballistic = false; }
				if (sol.Released && sol.Rel5 < 0 && s.Tick == sol.ReleaseTick + 5) { sol.VelAfter5 = n.velocity; sol.Rel5 = 1; }
				if (si == 0 && s.Trace)
				{
					string st = hk == null ? "-" : $"{hk.type}:ai0={hk.ai[0]:0}:{(hk.Center.X / 16f - Cx):0.0},{(hk.Center.Y / 16f - Fy):0.0}";
					string phase = sol.LandTick >= 0 ? "landed" : sol.Released ? "ballistic" : sol.LatchTick >= 0 ? "reel" : sol.Casts > 0 ? "flight" : "idle";
					_trace.AppendLine(string.Join("\t", _qi, s.Tick, phase, F((n.Bottom.X - sol.Start.X) / 16f), F((n.Bottom.Y - sol.Start.Y) / 16f), F(n.velocity.X), F(n.velocity.Y), F(mis.X), F(mis.Y), d.grapCount, d.grappling[0], st, alive, n.noGravity ? 1 : 0, sol.Why));
				}
			}
		}

		private static void Cast(Sc s, HS sol, Vector2 aim)
		{
			long t0 = Stopwatch.GetTimestamp();
			Player d = sol.Body.Dummy;
			int before = CountHooks(sol);
			sol.Casts++; if (sol.FirstCastTick < 0) sol.FirstCastTick = s.Tick;
			Fr f = Enter(s, sol, true);
			CastingSol = sol;
			try
			{
				d.miscEquips[4] = sol.Hook;                       // the soldier's hook slot
				d.noItems = false; d.frozen = false; d.dead = false;
				Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
				d.direction = aim.X >= d.Center.X ? 1 : -1;
				d.controlHook = true; d.releaseHook = true;
				d.QuickGrapple();
			}
			catch (Exception e) { if (s.Exc.Count < 6) s.Exc.Add("cast:" + e.GetType().Name + ":" + e.Message); }
			finally { CastingSol = null; Leave(s, sol, f); }
			int after = CountHooks(sol);
			if (after > before) { sol.CastOk++; s.HooksSpawned += after - before; }
			s.CastTs += Stopwatch.GetTimestamp() - t0; s.CastCalls++;
		}

		private static int CountHooks(HS sol)
		{
			int c = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && Main.projHook[p.type] && p.TryGetGlobalProjectile(out HookTag t) && t.Soldier == sol.Idx) c++; }
			return c;
		}

		// ------------------------------------------------------------------------------------------------
		// soldier AI replacement while reeling (GlobalNPC.PreAI)
		// ------------------------------------------------------------------------------------------------
		internal static bool PreAI(NPC npc, SoldierNPC sn)
		{
			var s = _sc; if (s == null || _stage != 1) return true;
			HS sol = FindSol(s, npc.whoAmI); if (sol == null || sol.Body == null) return true;
			Player d = sn.Dummy; if (d == null) return true;
			if (d.grappling[0] >= 0)
			{
				long t0 = Stopwatch.GetTimestamp();
				Fr f = Enter(s, sol, true);
				long t1 = Stopwatch.GetTimestamp();
				bool released = false;
				try
				{
					if (sol.PendingRelease)
					{
						if (s.RelMode == "drop") { d.RemoveAllGrapplingHooks(); }
						else { d.controlJump = true; d.releaseJump = true; }
					}
					else { d.controlJump = false; }
					if (s.F.TryGetValue("steer", out var sv)) { d.controlUp = sv.Contains("up"); d.controlDown = sv.Contains("down"); d.controlLeft = sv.Contains("left"); d.controlRight = sv.Contains("right"); }
					d.GrappleMovement();
					released = d.grappling[0] < 0;
					if (!s.NoReset) { d.grappling[0] = -1; d.grapCount = 0; }   // what the end of Player.Update does every tick
					else if (released) { d.grapCount = 0; }
				}
				catch (Exception e) { if (s.Exc.Count < 6) s.Exc.Add("reel:" + e.GetType().Name + ":" + e.Message); }
				finally { s.StepInnerTs += Stopwatch.GetTimestamp() - t1; Leave(s, sol, f); }
				npc.velocity = d.velocity;
				sol.ReelTicks++;
				if (sol.PendingRelease)
				{
					bool still = false; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && Main.projHook[p.type] && p.ai[0] == 2f && p.TryGetGlobalProjectile(out HookTag t) && t.Soldier == sol.Idx) { still = true; break; } }
					if (!still)
					{
						sol.Released = true; sol.PendingRelease = false; sol.ReleaseTick = s.Tick; sol.RelVel = npc.velocity; sol.RelPos = new Vector2((npc.Bottom.X - sol.Start.X) / 16f, (npc.Bottom.Y - sol.Start.Y) / 16f);
						npc.noGravity = false; sol.Ballistic = s.After == "ballistic";
					}
					else npc.noGravity = true;
				}
				else npc.noGravity = true;
				if ((sol.ReelTicks & 3) == 0 || sol.Released) { npc.netUpdate = true; sol.NetUpd++; }
				s.StepTs += Stopwatch.GetTimestamp() - t0; s.StepCalls++;
				return false;
			}
			if (!s.NoReset) { d.grappling[0] = -1; d.grapCount = 0; }     // end of Player.Update, every tick, hooked or not
			npc.noGravity = false;
			sol.Why = $"b{(sol.Ballistic ? 1 : 0)}f{(OnFloor(npc) ? 1 : 0)}h{s.Tick - sol.LastHookProjTick}";
			if (sol.Ballistic && !OnFloor(npc))
			{
				float drag = s.F.TryGetValue("drag", out var dv) && float.TryParse(dv, NumberStyles.Float, CultureInfo.InvariantCulture, out float dd) ? dd : 0.1f; // a player in the air with no input loses runSlowdown * 0.5 = 0.1 px/tick of vx
				if (drag > 0f) npc.velocity.X = Math.Abs(npc.velocity.X) > drag ? npc.velocity.X - Math.Sign(npc.velocity.X) * drag : 0f;
				return false;
			}
			if (s.Tick - sol.LastHookProjTick <= 1) return false; // a hook projectile is alive and may be driving the body itself (Spring Hook)
			if (sol.Released && sol.LandTick < 0) { sol.PreVel = npc.velocity; sol.PreSet = true; sol.PreTrue++; }
			return true;
		}

		internal static void PostAI(NPC npc)
		{
			var s = _sc; if (s == null || _stage != 1) return;
			HS sol = FindSol(s, npc.whoAmI); if (sol == null || !sol.PreSet) return;
			sol.PreSet = false; sol.AiRuns++; sol.AiDvx += npc.velocity.X - sol.PreVel.X; sol.AiDvy += npc.velocity.Y - sol.PreVel.Y;
		}

		// ------------------------------------------------------------------------------------------------
		// hook projectile frame
		// ------------------------------------------------------------------------------------------------
		internal static void RunProj(On_Projectile.orig_Update orig, Projectile p, int i, HookTag tag)
		{
			var s = _sc;
			if (s == null || _stage != 1) { orig(p, i); return; }
			HS sol = FindSol(s, tag.Soldier);
			if (sol == null || sol.Body == null || !sol.Body.NPC.active) { p.Kill(); return; }
			if (s.NoFrame) { orig(p, i); return; }
			long t0 = Stopwatch.GetTimestamp();
			Fr f = Enter(s, sol, false);
			long t1 = Stopwatch.GetTimestamp();
			sol.LastHookProjTick = s.Tick;
			Vector2 pre = sol.Body.Dummy.velocity;
			try { orig(p, i); }
			finally
			{
				if (sol.Body.Dummy.velocity != pre) { sol.Body.NPC.velocity = sol.Body.Dummy.velocity; sol.Adopted++; } // hooks that move the player themselves (Thorium Spring Hook)
				long t2 = Stopwatch.GetTimestamp();
				Leave(s, sol, f);
				long t3 = Stopwatch.GetTimestamp();
				s.ProjOvhTs += (t1 - t0) + (t3 - t2); s.ProjOrigTs += t2 - t1; s.ProjCalls++;
			}
		}

		internal static void RunKill(On_Projectile.orig_Kill orig, Projectile p, HookTag tag)
		{
			var s = _sc;
			if (s != null && _stage == 1 && p.active)
			{
				try
				{
					var sol = FindSol(s, tag.Soldier);
					var st = new StackTrace(0, false).GetFrames();
					string key = string.Join("<", st.Select(fr => fr.GetMethod()?.Name ?? "?").Where(nm => !nm.Contains("Detour") && !nm.Contains("Hook") && !nm.Contains("RunKill") && !nm.StartsWith("DMD") && !nm.Contains("lambda") && nm != "orig_Kill" && nm != "Kill" || nm == "RemoveAllGrapplingHooks" || nm == "AI_007_GrapplingHooks").Take(4));
					Player op = Main.player[p.owner];
					bool ownerActive = op.active;
					string k = $"{(p.ai[0] == 2f ? "latched" : p.ai[0] == 1f ? "retract" : "flight")}|ownerActive={ownerActive}|{key}";
					s.Kills[k] = s.Kills.TryGetValue(k, out int c) ? c + 1 : 1;
					if (sol != null) { sol.HookKills++; if (key.Contains("RemoveAllGrapplingHooks") || key.Contains("QuickGrapple") || (p.ai[0] == 1f && key.Contains("AI_007"))) sol.HookKillsExpected++; }
				}
				catch (Exception) { }
			}
			orig(p);
		}

		internal static void NoteSend(int msgType, int number)
		{
			var s = _sc; if (s == null || _stage != 1 || FrameDepth <= 0) return;
			string k = "msg" + msgType; s.Sends[k] = s.Sends.TryGetValue(k, out int c) ? c + 1 : 1;
		}

		internal static void NoteNetPass() { var s = _sc; if (s != null && _stage == 1 && FrameDepth > 0) s.NetPasses++; }

		// ------------------------------------------------------------------------------------------------
		// hook list
		// ------------------------------------------------------------------------------------------------
		private static void RunList(Sc s)
		{
			foreach (int t in AllHookItems())
			{
				Item it = ContentSamples.ItemsByType[t];
				var pr = ContentSamples.ProjectilesByType.TryGetValue(it.shoot, out var pp) ? pp : null;
				_rows.Add(string.Join("\t", "-", "hooklist", ItemKeyName(t), it.Name, ProjName(it.shoot) + "#" + it.shoot, "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", $"shootSpeed={it.shootSpeed:0.##}", $"aiStyle={pr?.aiStyle}", $"aiType={pr?.ModProjectile?.AIType}", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "rarity=" + it.rare));
			}
		}

		// ------------------------------------------------------------------------------------------------
		// output
		// ------------------------------------------------------------------------------------------------
		private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
		private static string F4(double v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
		private static double Ms(long ts, int per) => per <= 0 ? 0 : ts * 1000.0 / Stopwatch.Frequency / per;
		private static string Clean(string x) => (x ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		private static string Row(Sc s, int idx)
		{
			var a = s.Sols.FirstOrDefault(x => x.Body != null) ?? new HS();
			string flags = string.Join(",", s.F.Select(kv => kv.Value == "1" ? kv.Key : kv.Key + "=" + kv.Value));
			int castOk = s.Sols.Sum(x => x.CastOk), casts = s.Sols.Sum(x => x.Casts);
			int hk = s.Sols.Sum(x => x.HookKills), hke = s.Sols.Sum(x => x.HookKillsExpected);
			string killKeys = s.Kills.Count == 0 ? "-" : string.Join(";", s.Kills.OrderByDescending(k => k.Value).Take(4).Select(k => k.Key + "x" + k.Value));
			int solTicks = s.StepCalls;
			return string.Join("\t", idx, s.Kind, s.ItemName, flags, a.HookName + "#" + a.HookType, s.N, castOk + "/" + casts,
				a.LatchTick >= 0 && a.FirstCastTick >= 0 ? (a.LatchTick - a.FirstCastTick).ToString() : "-",
				a.LatchTick >= 0 ? F(a.LatchTile.X) + ":" + F(a.LatchTile.Y) : "-",
				a.ArriveTick, a.ReleaseTick,
				a.ReleaseTick >= 0 ? F(a.RelVel.X) + "," + F(a.RelVel.Y) : "-", a.ReleaseTick >= 0 ? F(a.RelPos.X) + "," + F(a.RelPos.Y) : "-",
				F(a.PeakUp), a.LandTick, a.LandTick >= 0 ? F((a.LandPos.X - a.Start.X) / 16f) + "," + F((a.LandPos.Y - a.Start.Y) / 16f) : "-",
				a.Rel5 > 0 ? F(a.VelAfter5.X) + "," + F(a.VelAfter5.Y) : "-", F(a.MaxSpeed), a.MaxGrapCount, a.ReelTicks + (a.Adopted > 0 ? "(adopt" + a.Adopted + ")" : ""), a.NetUpd, hke + "/" + hk, killKeys,
				F4(Ms(s.StepTs, solTicks)), F4(Ms(s.StepInnerTs, solTicks)), F4(Ms(s.ProjOvhTs, s.ProjCalls)), F4(Ms(s.ProjOrigTs, s.ProjCalls)), F4(Ms(s.WholeTs, s.WholeSamples)),
				F(a.MaxMismatch), s.NetClears, s.NetPasses, s.Sends.Count == 0 ? "-" : string.Join(";", s.Sends.Select(k => k.Key + "x" + k.Value)),
				s.Exc.Count == 0 ? "-" : Clean(string.Join(" | ", s.Exc)), Clean(s.Note + (s.CastCalls > 0 ? $"castMs={Ms(s.CastTs, s.CastCalls):0.000} " : "") + (a.MaxReach > 0 ? $"reachTiles={a.MaxReach / 16f:0.0} flipTiles={a.ReachAtFlip / 16f:0.0} " : "") + (a.PreTrue > 0 ? $"preTrue={a.PreTrue} aiRunsAirborne={a.AiRuns} sumDvx={a.AiDvx:0.00} sumDvy={a.AiDvy:0.00}" : "")));
		}

		private static void Write()
		{
			try
			{
				var sb = new StringBuilder(); sb.AppendLine(RowHeader);
				foreach (var r in _rows) sb.AppendLine(r);
				string tmp = CombatHarness.OutPath + ".tmp"; File.WriteAllText(tmp, sb.ToString()); File.Move(tmp, CombatHarness.OutPath, true);
				File.WriteAllText(CombatHarness.OutPath + ".trace.tsv", _trace.ToString());
			}
			catch (Exception e) { _h?.Mod.Logger.Warn("HOOKSLAB: write failed: " + e.Message); }
		}
	}

	/// <summary>Tags a lab hook projectile with the soldier that cast it.</summary>
	public class HookTag : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		public int Soldier = -1;
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!HooksLab.Active) return;
			var cs = HooksLab.CastingSol;
			if (cs != null) { Soldier = cs.Idx; projectile.npcProj = false; }
		}
	}

	public class HooksLabNpc : GlobalNPC
	{
		public override void PostAI(NPC npc)
		{
			if (HooksLab.Running && npc.ModNPC is SoldierNPC) HooksLab.PostAI(npc);
		}

		public override bool PreAI(NPC npc)
		{
			if (HooksLab.Running && npc.ModNPC is SoldierNPC sn) return HooksLab.PreAI(npc, sn);
			return true;
		}
	}

	public class HooksLabSystem : ModSystem
	{
		public override void Load()
		{
			HooksLab.ReadEnv();
			if (!HooksLab.Active) return;
			On_Projectile.Update += UpdateDetour;
			On_Projectile.Kill += KillDetour;
			On_Netplay.UpdateConnectedClients += NetDetour;
			On_NetMessage.SendData += SendDetour;
		}

		public override void Unload()
		{
			if (!HooksLab.Active) return;
			try { On_Projectile.Update -= UpdateDetour; On_Projectile.Kill -= KillDetour; On_Netplay.UpdateConnectedClients -= NetDetour; On_NetMessage.SendData -= SendDetour; } catch { }
			HooksLab.Active = false; HooksLab.Running = false;
		}

		public override void PreUpdateProjectiles() { if (HooksLab.Running) global::Levy.Common.Targeting.EnemyTargeting.EnsureSlotRestored(); }

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (HooksLab.InScenario && self.active && Main.projHook[self.type] && self.TryGetGlobalProjectile(out HookTag tag) && tag.Soldier >= 0) HooksLab.RunProj(orig, self, i, tag);
			else orig(self, i);
		}

		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (HooksLab.InScenario && self.active && Main.projHook[self.type] && self.TryGetGlobalProjectile(out HookTag tag) && tag.Soldier >= 0) HooksLab.RunKill(orig, self, tag);
			else orig(self);
		}

		private static void NetDetour(On_Netplay.orig_UpdateConnectedClients orig)
		{
			if (HooksLab.InScenario)
			{
				HooksLab.NoteNetPass();
				if (HooksLab.NetLockOn) { lock (HooksLab.NetLock) orig(); return; }   // the real server thread: held off while a Dummy stands in a slot
			}
			orig();
		}

		private static void SendDetour(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, Terraria.Localization.NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
		{
			if (HooksLab.FrameDepth > 0) HooksLab.NoteSend(msgType, number);
			orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
		}
	}
}
