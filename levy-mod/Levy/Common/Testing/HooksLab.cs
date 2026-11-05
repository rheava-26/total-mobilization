using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Owner;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HOOKS LAB, production edition (harness-only, inert unless LEVY_HARNESS and a LEVY_HARNESS_LAB spec whose first kind starts with "hook"). It builds a course, spawns N levies with a real hook item in their gear and lets the
	/// PRODUCTION code (SoldierNPC.Hook.cs, HookPlanner, SoldierOwner, OwnerSlots, NetThreadGuard) do everything: the brain / movement set the goal and triggers, the planner picks the cast, the owner frame runs QuickGrapple,
	/// the hook AI and GrappleMovement. The lab only builds the world, holds the goal, and measures.
	///
	/// SPEC (LEVY_HARNESS_LAB): ';'-separated  kind|item|N|ticks|flags     kind: hook     item: V:Name | T:Name | C:Name | M:Mod/Name | #id | ALL (every hook item)
	///   course (tiles): ledge=H[,ledgex=X] (block whose top is H above the floor from X right)  gap=G (G-tile pit right of the soldier, beam over it at beamh=14)  wall=D (3-thick wall D right)
	///                   ceil=H (slab, underside H above the floor)  free (nothing)
	///   goal=dx:dy  tile offset from the soldier's feet tile (a standing spot); held as the MoveGoal every tick (the walking AI runs; defaults per course)
	///   force=goalabove|wall|gap|flee   start a plan at tick 'at' toward the goal without waiting for the trigger's hold time (the matrix of hook items)
	///   alt=dy  altitude intent for a hang dy tiles above the floor (stands in for the brain's UpdateAltitudeIntent output); altf=1 instead raises a real target for the brain
	///   pair (a multi-hook item casts two hooks whenever it can)  all (stand-off and Spring hooks count as usable)
	///   at=T (first start, 20)  nohook (no hook in the slot: the control run)  off (config off)  idle (hook, no goal, no trigger: cost of an idle army)  fleeing (State Routed + goal: the flee trigger)
	///   killat=T  freezeat=T  teleat=T  (events on soldier 0)  trace
	/// OUT: LEVY_HARNESS_OUT (summary TSV) and OUT.trace.tsv (per-tick trace of soldier 0).
	/// The arena floor is moved below the 'space' gravity band (floor row = LEVY_HOOKS_FLOOR or spaceBand + 75).
	/// </summary>
	public static class HooksLab
	{
		internal static bool Active, Running;
		private static string _spec = "";
		internal static bool Claims(string spec) => !string.IsNullOrEmpty(spec) && spec.TrimStart().StartsWith("hook", StringComparison.OrdinalIgnoreCase);
		internal static void ReadEnv()
		{
			_spec = Environment.GetEnvironmentVariable("LEVY_HARNESS_LAB") ?? "";
			Active = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS")) && Claims(_spec);
		}

		internal sealed class HS
		{
			public SoldierRecord Rec; public int Idx = -1; public SoldierNPC Body; public Item Hook;
			public Vector2 Start, GoalPx; public int ReachTick = -1, FirstStateTick = -1, LandedTick = -1; public float PeakUp, MaxSpeed, MinGoalDist = float.MaxValue; public int WalkTicksWhileHooked, OnGroundAtEnd;
			public float EndDx, EndDy; public bool Forced, Reached;
			public int MaxHooksAlive;
		}

		internal sealed class Sc
		{
			public string ItemKey = ""; public int N = 1, Ticks = 300; public Dictionary<string, string> F = new();
			public int ItemType = -1; public string ItemName = "";
			public List<HS> Sols = new(); public int Tick; public bool Done; public string Note = "";
			public int CeilH, GapG, WallD, BeamH = 14, LedgeH, PlatH; public int At = 20;
			public Vector2? Goal; public string Force = "";
			public long WholeTs; public int WholeSamples;
			public long TickTs0, ProjTs0, PlanTs0; public int TickCalls0, ProjCalls0, Plans0, Found0, Casts0, CastsOk0, Latches0, Releases0, Landings0, Aborts0, Orphans0, Sup0, Tele0;
			public int KillsInactive, KillsTotal; public long Frames0, Refused0; public int MaxSlots, ActiveTicks, OrphansAtEnd; public long ActiveTs; public string FpBefore = "", FpAfter = "";
			public int TargetIdx = -1; public List<string> Exc = new(); public int EventNote;
			public int NetSkip0;
		}

		private static List<Sc> _queue = new(); private static int _qi, _stage, _stageTick; private static Sc _sc; private static CombatHarness _h;
		private static readonly List<string> _rows = new(); private static readonly StringBuilder _trace = new(); private static long _lastTick;
		private static string RowHeader = "idx\titem\tflags\tN\treached\treachTick0\tpeakUpT0\tcasts\tcastOk\tlatches\treleases\tlandings\taborts\torphans\tkillsOwnerInactive\tkillsTotal\tminGoalDistT0\tendDxT0\tendDyT0\tstateTick0\tmaxSlots\tframes\trefused\tprodMsPerTick\tprodMsPerHookingTick\tplanMs\tplans\tplansFound\tidleCalls\twholeMsTick\tnetSuppressed\tfpUnchanged\texceptions\tnote";

		internal static void Boot(CombatHarness h)
		{
			_h = h; _qi = 0; _stage = 0; _stageTick = 0; _sc = null; Running = true;
			PickFloor(); _queue = Parse(_spec);
			h.Mod.Logger.Info($"HOOKSLAB: {_queue.Count} scenarios queued (production code)");
			_trace.Clear(); _trace.AppendLine("idx\ttick\tstate\tdxT\tdyT\tvx\tvy\thooksAlive\tgoalDistT");
		}

		private static List<Sc> Parse(string spec)
		{
			var l = new List<Sc>();
			foreach (var raw in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				var p = raw.Split('|');
				if (p.Length < 2) continue;
				var s = new Sc { ItemKey = p[1] };
				if (p.Length > 2 && int.TryParse(p[2], out int n)) s.N = Math.Clamp(n, 1, 60);
				if (p.Length > 3 && int.TryParse(p[3], out int t)) s.Ticks = Math.Clamp(t, 30, 8000);
				if (p.Length > 4) foreach (var f in p[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					int eq = f.IndexOf('=');
					if (eq > 0) s.F[f.Substring(0, eq)] = f.Substring(eq + 1); else s.F[f] = "1";
				}
				string G(string k, string d) => s.F.TryGetValue(k, out var v) ? v : d;
				int GI(string k, int d) => int.TryParse(G(k, ""), out int v) ? v : d;
				s.CeilH = GI("ceil", 0); s.GapG = GI("gap", 0); s.WallD = GI("wall", 0); s.LedgeH = GI("ledge", 0); s.PlatH = GI("plat", 0); s.BeamH = GI("beamh", 14); s.At = GI("at", 20); s.Force = G("force", "");
				if (s.CeilH == 0 && s.GapG == 0 && s.WallD == 0 && s.LedgeH == 0 && s.PlatH == 0 && !s.F.ContainsKey("free")) s.CeilH = 15;
				if (s.ItemKey == "ALL") foreach (int it in AllHookItems()) { var c = new Sc { ItemKey = "#" + it, N = s.N, Ticks = s.Ticks, F = new Dictionary<string, string>(s.F), CeilH = s.CeilH, GapG = s.GapG, WallD = s.WallD, BeamH = s.BeamH, LedgeH = s.LedgeH, PlatH = s.PlatH, At = s.At, Force = s.Force }; l.Add(c); }
				else l.Add(s);
			}
			return l;
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
						if (++_sc.Tick >= _sc.Ticks || _sc.Done) { _stage = 2; _stageTick = 0; End(_sc); }
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

		private static void Finish() { Running = false; Write(); _h.LabFinish($"hooks lab complete, {_rows.Count} rows"); }

		// ------------------------------------------------------------------------------------------------
		// course
		// ------------------------------------------------------------------------------------------------
		private static int Cx => _h.Cx;
		internal static int Fy = 120;
		private static void PickFloor()
		{
			float num = Main.maxTilesX / 4200f; num *= num;
			int spaceBand = (int)(60f + 10f * num + Main.worldSurface / 6.0) + 2;
			int want = spaceBand + 75;
			Fy = Math.Clamp(int.TryParse(Environment.GetEnvironmentVariable("LEVY_HOOKS_FLOOR"), out int f) ? f : want, 100, Main.maxTilesY - 120);
			_h.Mod.Logger.Info($"HOOKSLAB: world {Main.maxTilesX}x{Main.maxTilesY} surface={Main.worldSurface:0} spaceBand<{spaceBand} floorRow={Fy} maxNetPlayers={Main.maxNetPlayers} netMode={Main.netMode}");
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
			Fill(cx - 100, Fy - 80, cx + 100, fy - 1, false);
			Fill(cx - 100, fy, cx + 100, fy + 3, true);
			Fill(cx - 100, fy + 4, cx + 100, fy + 25, true);
			if (s.CeilH > 0) Fill(cx - 70, fy - 1 - s.CeilH - 2, cx + 70, fy - 1 - s.CeilH, true);
			if (s.GapG > 0)
			{
				Fill(cx + 2, fy, cx + 1 + s.GapG, fy + 20, false);
				Fill(cx - 3, fy - 1 - s.BeamH - 2, cx + s.GapG + 10, fy - 1 - s.BeamH, true);
			}
			if (s.WallD > 0) Fill(cx + s.WallD, Fy - 80, cx + s.WallD + 2, fy - 1, true);
			if (s.LedgeH > 0) Fill(cx + LedgeX(s), fy - s.LedgeH, cx + 70, fy - 1, true);
			if (s.PlatH > 0) Fill(cx - 1, fy - s.PlatH - 1, cx + 1, fy - s.PlatH, true);                 // a small floating platform right over the soldier, its top PlatH above the floor
			// goal (a standing spot, feet bottom): tile offsets from the feet tile (cx, floor row)
			string g = s.F.TryGetValue("goal", out var gv) ? gv : null;
			if (g == null)
			{
				if (s.LedgeH > 0) g = $"{LedgeX(s) + 10}:{-s.LedgeH}";
				else if (s.GapG > 0) g = $"{s.GapG + 12}:0";
				else if (s.WallD > 0) g = $"{s.WallD + 8}:0";
				else if (s.PlatH > 0) g = $"0:{-s.PlatH - 1}";
			}
			if (g != null && !s.F.ContainsKey("idle"))
			{
				var xy = g.Split(':');
				float dx = float.Parse(xy[0], CultureInfo.InvariantCulture), dy = float.Parse(xy[1], CultureInfo.InvariantCulture);
				s.Goal = new Vector2((cx + dx) * 16f + 8f, (fy + dy) * 16f);
			}
		}

		private static void Setup(Sc s)
		{
			_h.LabClearAll();
			s.ItemType = ResolveItem(s.ItemKey);
			s.ItemName = s.ItemType > 0 ? ItemKeyName(s.ItemType) : s.ItemKey;
			if (s.ItemType <= 0) { s.Note = "ITEM NOT FOUND"; s.Done = true; s.Ticks = 0; return; }
			BuildCourse(s);
			SoldierHooks.SetOverride(s.F.ContainsKey("off") ? 0 : -1);
			HookPlanner.PairMargin = s.F.ContainsKey("pair") ? -100000f : 4f; HookInfo.LabAllowAll = s.F.ContainsKey("all");
			int cx = Cx;
			Player c = Main.player[0];
			c.active = true; c.dead = false; c.name = "LevyTester"; c.statLifeMax = c.statLifeMax2 = 500; c.statLife = 500;
			c.position = new Vector2((cx - (s.F.ContainsKey("altf") ? 4 : 90)) * 16f, Fy * 16f - c.height); c.velocity = Vector2.Zero;
			for (int k = 0; k < s.N; k++)
			{
				var rec = _h.LabMakeRecord("Pvt. Lab" + k);
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold; rec.HoldFire = true; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				if (s.F.ContainsKey("altf"))
				{
					var wpn = new Item(); wpn.SetDefaults(ItemID.Minishark); rec.Gear.Weapon = wpn; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
					var ammo = new Item(); ammo.SetDefaults(ItemID.MusketBall); ammo.stack = 999; rec.Gear.Ammo = ammo;
					rec.HoldFire = false; rec.Order = SoldierOrder.Follow;
				}
				var hook = new Item(); hook.SetDefaults(s.ItemType);
				if (!s.F.ContainsKey("nohook")) { rec.Gear.Hook = hook; rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
				RosterSystem.Instance.Regulars.Add(rec);
				float x = cx * 16f + 8f + (k - s.N / 2) * 44f;
				int idx = RosterSystem.Instance.SpawnBody(rec, new Vector2(x, Fy * 16f));
				var sol = new HS { Rec = rec, Idx = idx, Hook = hook };
				if (idx >= 0) { sol.Body = Main.npc[idx].ModNPC as SoldierNPC; if (sol.Body != null) { sol.Body.NPC.Bottom = new Vector2(x, Fy * 16f); sol.Body.HookLabReset(); } }
				s.Sols.Add(sol);
			}
			if (s.F.TryGetValue("altf", out _)) SpawnTarget(s);
		}

		/// <summary>A pinned hostile hovering above the first soldier, for the brain-driven altitude trigger.</summary>
		private static void SpawnTarget(Sc s)
		{
			float dy = s.F.TryGetValue("alt", out var av) && float.TryParse(av, NumberStyles.Float, CultureInfo.InvariantCulture, out float a) ? a : 14f;
			float cx = Cx * 16f + 8f + 6 * 16f;
			int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("LevyLab"), (int)cx, (int)((Fy - dy) * 16f), NPCID.GreenSlime);
			if (i < 0 || i >= Main.maxNPCs) { s.Exc.Add("target NewNPC failed"); return; }
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true;
			n.lifeMax = n.life = 1_000_000; n.defense = 0; n.damage = 0; n.knockBackResist = 0f; n.width = 64; n.height = 96;
			n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.dontTakeDamage = false; n.value = 0f; n.velocity = Vector2.Zero;
			n.Bottom = new Vector2(cx, (Fy - dy) * 16f + 48f);
			ht.Pin = n.position; s.TargetIdx = i;
		}

		private static void Keep(Sc s)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = "LevyTester"; p.statLife = p.statLifeMax2 = 500;
			p.position = new Vector2((Cx - (s.F.ContainsKey("altf") ? 4 : 90)) * 16f, Fy * 16f - p.height); p.velocity = Vector2.Zero;
			if (_stage == 0)
				foreach (var sol in s.Sols) if (sol.Body != null && sol.Body.NPC.active) { sol.Body.Record.CurrentHp = sol.Body.Record.MaxHp; sol.Body.NPC.life = sol.Body.NPC.lifeMax; }
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				if (!n.friendly || n.type == NPCID.TargetDummy) n.active = false;
			}
		}

		private static void Begin(Sc s)
		{
			foreach (var sol in s.Sols)
			{
				if (sol.Body == null) continue;
				sol.Body.RefreshStats(); // pick up the hook in the movement abilities now
				sol.Start = sol.Body.NPC.Bottom;
			}
			s.FpBefore = Fp(Main.player[0]);
			s.TickTs0 = SoldierHooks.TickTs; s.ProjTs0 = SoldierHooks.ProjTs; s.PlanTs0 = SoldierHooks.PlanTs; s.TickCalls0 = SoldierHooks.TickCalls; s.ProjCalls0 = SoldierHooks.ProjCalls; s.Plans0 = SoldierHooks.Plans; s.Found0 = SoldierHooks.PlansFound;
			s.Casts0 = SoldierHooks.Casts; s.CastsOk0 = SoldierHooks.CastsOk; s.Latches0 = SoldierHooks.Latches; s.Releases0 = SoldierHooks.Releases; s.Landings0 = SoldierHooks.Landings; s.Aborts0 = SoldierHooks.Aborts; s.Orphans0 = SoldierHooks.OrphanKills; s.Sup0 = SoldierHooks.NetSuppressed; s.Tele0 = SoldierHooks.Teleports;
			s.Frames0 = SoldierOwner.Frames; s.Refused0 = SoldierOwner.Refused;
		}

		private static void End(Sc s) { s.FpAfter = Fp(Main.player[0]); s.OrphansAtEnd = SoldierHooks.OrphanKills; }

		private static void Teardown(Sc s)
		{
			SoldierHooks.SetOverride(-1); HookPlanner.PairMargin = 4f; HookInfo.LabAllowAll = false;
			foreach (var sol in s.Sols) { try { if (sol.Body?.NPC != null) sol.Body.NPC.noGravity = false; } catch { } }
			_h.LabClearAll();
			OwnerSlots.Sweep();
			global::Levy.Common.Targeting.EnemyTargeting.EnsureSlotRestored();
		}

		/// <summary>The commander's own state; a hook frame must leave all of it alone (and the machine's player / dedServ / cursor).</summary>
		private static string Fp(Player p)
		{
			var sb = new StringBuilder();
			sb.Append($"life{p.statLife}/{p.statLifeMax2} mana{p.statMana} pos{p.position.X:0.##},{p.position.Y:0.##} vel{p.velocity.X:0.##},{p.velocity.Y:0.##} g{p.grappling[0]}/{p.grapCount} hook{p.controlHook}/{p.releaseHook} jump{p.controlJump}/{p.releaseJump} sel{p.selectedItem} anim{p.itemAnimation} frz{p.frozen} who{p.whoAmI} my{Main.myPlayer} ded{Main.dedServ} vol{Main.soundVolume:0.##} mouse{Main.mouseX},{Main.mouseY} scr{Main.screenPosition.X:0.#},{Main.screenPosition.Y:0.#} p255{(ReferenceEquals(Main.player[255], null) ? 0 : Main.player[255].active ? 1 : 0)} num{p.numMinions} owned[");
			for (int i = 0; i < p.ownedProjectileCounts.Length; i++) if (p.ownedProjectileCounts[i] > 0) sb.Append(i).Append('x').Append(p.ownedProjectileCounts[i]).Append(' ');
			sb.Append("] buffs[");
			for (int b = 0; b < p.buffType.Length; b++) if (p.buffType[b] > 0) sb.Append(p.buffType[b]).Append(' ');
			sb.Append("] inv"); int h = 17; for (int i = 0; i < p.inventory.Length; i++) h = h * 31 + p.inventory[i].type * 7 + p.inventory[i].stack; sb.Append(h);
			int hooksOfCommander = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projHook[Main.projectile[i].type] && Main.projectile[i].owner == p.whoAmI) hooksOfCommander++;
			sb.Append(" hooksOwned").Append(hooksOfCommander);
			return sb.ToString();
		}

		// ------------------------------------------------------------------------------------------------
		// per-tick
		// ------------------------------------------------------------------------------------------------
		private static bool OnFloor(NPC n) => n.velocity.Y == 0f && Collision.SolidCollision(new Vector2(n.position.X, n.position.Y + n.height), n.width, 2);

		private static int HooksAlive(SoldierNPC b) { return b.HookCountAlive; }

		private static void RunTick(Sc s)
		{
			PostEvents(s);
			if (OwnerSlots.InUse > s.MaxSlots) s.MaxSlots = OwnerSlots.InUse;
			{ bool any = false; for (int q = 0; q < s.Sols.Count && !any; q++) any = s.Sols[q].Body != null && s.Sols[q].Body.NPC.active && s.Sols[q].Body.IsHooking; if (any) s.ActiveTicks++; }
			for (int si = 0; si < s.Sols.Count; si++)
			{
				var sol = s.Sols[si];
				if (sol.Body == null || !sol.Body.NPC.active) continue;
				NPC n = sol.Body.NPC; var b = sol.Body;
				if (s.Goal.HasValue)
				{
					sol.GoalPx = s.Goal.Value + new Vector2(sol.Start.X - (Cx * 16f + 8f), 0f);
					b.HasMoveGoal = true; b.MoveGoal = sol.GoalPx;
					if (s.F.ContainsKey("fleeing")) b.State = SoldierState.Routed;
				}
				if (s.Force.Length > 0 && s.Tick == s.At)
				{
					HookReason r = s.Force switch { "wall" => HookReason.Wall, "gap" => HookReason.Gap, "flee" => HookReason.Flee, _ => HookReason.GoalAbove };
					Vector2 gp = s.Goal.HasValue ? sol.GoalPx : sol.Start + new Vector2(0f, -s.CeilH * 16f);
					sol.Forced = b.HookLabStart(r, gp);
					if (!sol.Forced) s.Note += "forceStartRefused ";
				}
				if (s.F.TryGetValue("alt", out var av) && !s.F.ContainsKey("altf") && float.TryParse(av, NumberStyles.Float, CultureInfo.InvariantCulture, out float adt) && s.Tick >= s.At)
					b.HookLabAltitude(sol.Start.X, (Fy - adt) * 16f + 0f);
				// sample
				var st = b.HookState;
				if (st != SoldierNPC.HookPhase.Idle && sol.FirstStateTick < 0) sol.FirstStateTick = s.Tick;
				float up = (sol.Start.Y - n.Bottom.Y) / 16f; if (up > sol.PeakUp) sol.PeakUp = up;
				if (n.velocity.Length() > sol.MaxSpeed) sol.MaxSpeed = n.velocity.Length();
				int alive = si == 0 ? HooksAlive(b) : 0; if (alive > sol.MaxHooksAlive) sol.MaxHooksAlive = alive;
				if (s.Goal.HasValue)
				{
					float gd = Vector2.Distance(n.Bottom, sol.GoalPx) / 16f; if (gd < sol.MinGoalDist) sol.MinGoalDist = gd;
					if (!sol.Reached && Math.Abs(n.Bottom.X - sol.GoalPx.X) <= 40f && Math.Abs(n.Bottom.Y - sol.GoalPx.Y) <= 40f && OnFloor(n)) { sol.Reached = true; sol.ReachTick = s.Tick; }
				}
				if (si == 0)
				{
					float gd0 = s.Goal.HasValue ? Vector2.Distance(n.Bottom, sol.GoalPx) / 16f : 0f;
					_trace.AppendLine(string.Join("\t", _qi, s.Tick, st, F((n.Bottom.X - sol.Start.X) / 16f), F((n.Bottom.Y - sol.Start.Y) / 16f), F(n.velocity.X), F(n.velocity.Y), alive, F(gd0)));
				}
				// events on soldier 0
				if (si == 0)
				{
					if (s.F.TryGetValue("killat", out var kv) && int.TryParse(kv, out int kt))
					{
						if (s.Tick == kt) { s.Note += $"preKill(state={st},slots={OwnerSlots.InUse}) "; try { n.SimpleStrikeNPC(99999, 0, false, 0f, null, false, 0f, true); } catch (Exception e) { s.Exc.Add("kill:" + e.Message); } }
					}
					if (s.F.TryGetValue("freezeat", out var fv) && int.TryParse(fv, out int ft))
					{
						if (s.Tick == ft) { n.AddBuff(BuffID.Frozen, 40); s.Note += $"freezeAt(state={st}) "; }
						if (s.Tick == ft + 4) s.Note += $"afterFreeze(state={b.HookState},hooksAlive={AnyHooks()},noGrav={n.noGravity}) ";
					}
					if (s.F.TryGetValue("teleat", out var tv) && int.TryParse(tv, out int tt))
					{
						if (s.Tick == tt) { s.Note += $"preTele(state={st}) "; n.position += new Vector2(0f, -4f); b.NotifyTeleported(); }
						if (s.Tick == tt + 4) s.Note += $"afterTele(state={b.HookState},hooksAlive={AnyHooks()}) ";
					}
				}
			}
		}

		/// <summary>Events that outlive the soldier: after a kill the hooks must be gone and the slot free.</summary>
		private static void PostEvents(Sc s)
		{
			if (s.F.TryGetValue("killat", out var kv) && int.TryParse(kv, out int kt) && s.Tick == kt + 6)
			{
				var b = s.Sols.Count > 0 ? s.Sols[0].Body : null;
				s.Note += $"afterDeath(npcActive={b?.NPC.active},hooksAlive={AnyHooks()},slotsInUse={OwnerSlots.InUse}) ";
			}
		}

		private static int AnyHooks() { int c = 0; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && Main.projHook[p.type] && p.TryGetGlobalProjectile(out HookTag t) && t.Soldier >= 0) c++; } return c; }

		// ------------------------------------------------------------------------------------------------
		// output
		// ------------------------------------------------------------------------------------------------
		private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
		private static double Ms(long ts, int per) => per <= 0 ? 0 : ts * 1000.0 / Stopwatch.Frequency / per;
		private static string Clean(string x) => (x ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		private static string Row(Sc s, int idx)
		{
			var a = s.Sols.FirstOrDefault(x => x.Body != null) ?? new HS();
			string flags = string.Join(",", s.F.Select(kv => kv.Value == "1" ? kv.Key : kv.Key + "=" + kv.Value));
			if (s.CeilH > 0) flags = "ceil=" + s.CeilH + (flags.Length > 0 ? "," : "") + flags;
			int ticks = Math.Max(1, s.Tick);
			int reached = s.Sols.Count(x => x.Reached);
			long prodTs = (SoldierHooks.TickTs - s.TickTs0) + (SoldierHooks.ProjTs - s.ProjTs0);
			int casts = SoldierHooks.Casts - s.Casts0, castOk = SoldierHooks.CastsOk - s.CastsOk0;
			return string.Join("\t", idx, s.ItemName, flags, s.N, reached + "/" + s.Sols.Count(x => x.Body != null), a.ReachTick, F(a.PeakUp),
				casts, castOk, SoldierHooks.Latches - s.Latches0, SoldierHooks.Releases - s.Releases0, SoldierHooks.Landings - s.Landings0, SoldierHooks.Aborts - s.Aborts0, s.OrphansAtEnd - s.Orphans0, s.KillsInactive, s.KillsTotal,
				a.MinGoalDist == float.MaxValue ? "-" : F(a.MinGoalDist), a.Body != null ? F((a.Body.NPC.Bottom.X - a.Start.X) / 16f) : "-", a.Body != null ? F((a.Body.NPC.Bottom.Y - a.Start.Y) / 16f) : "-", a.FirstStateTick,
				s.MaxSlots, SoldierOwner.Frames - s.Frames0, SoldierOwner.Refused - s.Refused0, F(Ms(prodTs, ticks)), F(Ms(prodTs, Math.Max(1, s.ActiveTicks))), F(Ms(SoldierHooks.PlanTs - s.PlanTs0, Math.Max(1, SoldierHooks.Plans - s.Plans0))), SoldierHooks.Plans - s.Plans0, SoldierHooks.PlansFound - s.Found0,
				(SoldierHooks.TickCalls - s.TickCalls0) + (SoldierHooks.ProjCalls - s.ProjCalls0), F(Ms(s.WholeTs, s.WholeSamples)), SoldierHooks.NetSuppressed - s.Sup0, s.FpBefore == s.FpAfter ? "yes" : "NO:" + s.FpBefore + " != " + s.FpAfter,
				s.Exc.Count == 0 ? "-" : Clean(string.Join(" | ", s.Exc)), Clean(s.Note + $"maxHooksAlive={a.MaxHooksAlive} maxSpeed={F(a.MaxSpeed)} " + (s.F.ContainsKey("nohook") ? "" : $"reason={a.Body?.HookLastReason} sol0:casts={a.Body?.HkCasts} latch={a.Body?.HkLatches} rel={a.Body?.HkReleases} land={a.Body?.HkLandings} abort={a.Body?.HkAborts} ") + $"netSkip={NetThreadGuard.SkippedPasses}"));
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

		internal static void NoteKill(Projectile p)
		{
			var s = _sc; if (s == null || _stage != 1) return;
			s.KillsTotal++; if (!Main.player[p.owner].active) s.KillsInactive++;
		}
	}

	public class HooksLabSystem : ModSystem
	{
		public override void Load() { HooksLab.ReadEnv(); if (HooksLab.Active) On_Projectile.Kill += KillDetour; }
		public override void Unload() { if (!HooksLab.Active) return; try { On_Projectile.Kill -= KillDetour; } catch { } HooksLab.Active = false; HooksLab.Running = false; }
		public override void PreUpdateProjectiles() { if (HooksLab.Running) global::Levy.Common.Targeting.EnemyTargeting.EnsureSlotRestored(); }

		/// <summary>Counts kills of soldier hooks, and those that happened with an inactive owner (the network thread's doing).</summary>
		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (HooksLab.Running && self.active && self.type < Main.projHook.Length && Main.projHook[self.type] && self.TryGetGlobalProjectile(out HookTag tag) && tag.Soldier >= 0) HooksLab.NoteKill(self);
			orig(self);
		}
	}
}
