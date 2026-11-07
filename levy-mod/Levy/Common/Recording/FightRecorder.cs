using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Globals;
using Levy.Common.Observe;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Fight recorder: samples the commander (a Player), every boss part, nearby hostile projectiles and every levy body at 30 Hz into a JSON-lines file.
	/// Off by default; while off the only cost is one static bool test per tick (<see cref="FightRecSystem"/>). Reads public state only and changes nothing in the game.
	/// </summary>
	internal static class FightRecorder
	{
		public static bool On;
		public const int SampleEvery = 2, MaxTicks = 10 * 60 * 60, GraceTicks = 90, HeadlessGraceTicks = 40, ProjRangePx = 80 * 16, LevyRangePx = 120 * 16, MaxProj = 250;
		public const int ArenaW = 200, ArenaH = 120;
		/// <summary>Format 4 terrain growth: tiles around any recorded entity that get captured, the step the captured area grows in, and the largest area one recording may cover.</summary>
		public const int GrowMargin = 24, GrowStep = 20, MaxCoverW = 1600, MaxCoverH = 700, MaxCoverTiles = 600000, EntityReachX = 400, EntityReachY = 250, EndGapTicks = 20;
		public const int CamMaxW = 420, CamMaxH = 260, CamNearTiles = 160;

		/// <summary>Override for the output folder (synthetic test). Null = the tModLoader save folder.</summary>
		public static string OutDirOverride;
		/// <summary>Synthetic test only: a client-less server keeps flipping the stand-in player inactive.</summary>
		public static bool IgnoreInactive;

		private static StreamWriter _w;
		private static Player _cmd;
		private static string _path, _boss = "none";
		private static int _startTick, _frames, _events, _lastBossSeen, _bossesSeen;
		private static bool _bossSeen;
		// previous-sample state, for edge detection
		private static int _pJump, _pAnim, _pDashDelay, _pTimeDash;
		private static bool _pFly, _pHookOut, _pHookAtt;
		private static readonly Dictionary<int, bool> _pXJump = new();
		private static readonly Dictionary<int, string> _items = new(), _npcs = new(), _projs = new(), _buffs = new(), _soldiers = new();
		private static int[] _accLast = new int[10];
		private static readonly HashSet<int> _bossIdsSeen = new();

		// ---- format 2: levy health, hits, deaths and the boss's current target
		/// <summary>Headless auto-recording (LEVY_REC_DIR): the stand-in player may flip inactive, and the grace after the boss is gone is short so the next trial gets its own file.</summary>
		public static bool Headless;
		private static Dictionary<string, string> _meta;
		private static string _fileBase;
		private static int _bossGoneTick, _primaryWho = -1, _primarySerial;
		/// <summary>Highest spawn serial alive when the recording started. A boss-like NPC with a HIGHER serial was spawned afterwards by someone other than this fight (the next trial); serial 0 (a worm segment that became a head: SetDefaults resets its tag) and a lower one belong to this fight.</summary>
		private static int _startMaxSerial;
		private static bool IsNewFightSerial(NPC n) { int sr = FightRecSpawnTag.SerialOf(n); return sr > _startMaxSerial; }
		private static int _tkNone, _tkPlayer, _tkLevy, _lastTk = -1, _lastTl = int.MinValue, _levyHits, _levyDeaths;
		private sealed class LTrack { public int Life, Max; public float X, Y; public int Who; public bool Seen; }
		private static readonly Dictionary<int, LTrack> _lt = new();
		private static readonly HashSet<int> _deadNoted = new();
		private static readonly HashSet<int> _bossTypes = new();
		private static readonly Dictionary<int, (uint tick, int proj)> _projHit = new();
		private static readonly List<int> _gone = new();
		private static readonly Dictionary<int, string> _lbuf = new();
		private static readonly Dictionary<int, int> _lgear = new();
		private static readonly Dictionary<int, string> _prefixes = new();
		private static int _cmdGear = int.MinValue;
		public const int AddRangePx = 140 * 16, MaxAdds = 160;

		// ---- format 4: terrain that follows the fight, static camera view, boss death / despawn
		private static int _cx0, _cy0, _cx1, _cy1, _chunks, _coverTiles, _startCx, _startCy;
		private static bool _hasCover;
		private static int _ex0, _ey0, _ex1, _ey1;
		private static string _camName;
		private static int _camId = -1;
		private sealed class BTrack { public int Slot, Type, Serial, Life, Max; public float X, Y; public bool Seen; public string Name; public int HitT = -1, HitDmg, HitType = -1; public string HitSrc = "other", HitName = ""; }
		private static readonly Dictionary<int, BTrack> _bt = new();
		private static readonly Dictionary<int, (int tick, int dmg, string src, int type, string name)> _bhook = new();
		private static readonly HashSet<int> _bkilled = new();
		private static readonly List<(BTrack t, bool killed, int tick)> _bpend = new();
		private static readonly List<int> _bgone = new();
		private static bool _bAlive;
		private static string _outcome = "none";

		public static string Path => _path;
		public static int Elapsed => (int)(Main.GameUpdateCount - (uint)_startTick);

		private static string Names(Dictionary<int, string> d)
		{
			var sb = new StringBuilder("{"); bool first = true;
			foreach (var kv in d) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":").Append(FightRecFormat.Esc(kv.Value)); }
			return sb.Append('}').ToString();
		}

		/// <summary>
		/// A boss for recording purposes. Vanilla Eater of Worlds never has NPC.boss set while it lives (the flag is raised only on the last segment's death, NPC.DropEoWLoot),
		/// so the head is recognised by NPCID.Sets.ShouldBeCountedAsBoss / the main-boss list; a modded worm head that owns its segments' life (realLife == itself) and has a boss map icon counts too.
		/// </summary>
		internal static bool IsFightBoss(NPC n)
		{
			if (n.boss || NPCID.Sets.ShouldBeCountedAsBoss[n.type] || BossRule.IsMainBossType(n.type)) return true;
			return n.realLife == n.whoAmI && NPCID.Sets.BossHeadTextures[n.type] >= 0;
		}

		private static bool IsBossOrPart(NPC n)
		{
			if (n.boss || NPCID.Sets.ShouldBeCountedAsBoss[n.type]) return true;
			if (n.realLife >= 0 && n.realLife < Main.maxNPCs && Main.npc[n.realLife].active && Main.npc[n.realLife].boss) return true;
			foreach (int t in LevyConstants.Targeting.BossPartTypes) if (t == n.type) return true;
			return BossRule.IsMainBoss(LevyUtil.RootNpc(n));
		}

		private static string SafeName(string s)
		{
			var sb = new StringBuilder();
			foreach (char c in s ?? "") sb.Append(char.IsLetterOrDigit(c) ? c : '_');
			return sb.Length == 0 ? "none" : sb.ToString();
		}

		private static string Dir() => OutDirOverride ?? System.IO.Path.Combine(Main.SavePath, "Levy", "recordings");

		private static string ModList(out string gameMode)
		{
			var sb = new StringBuilder("[");
			bool first = true;
			foreach (Mod m in ModLoader.Mods)
			{
				if (m.Name == "ModLoader") continue;
				if (!first) sb.Append(','); first = false;
				sb.Append('[').Append(FightRecFormat.Esc(m.Name)).Append(',').Append(FightRecFormat.Esc(m.Version?.ToString())).Append(']');
			}
			sb.Append(']');
			gameMode = Main.GameMode switch { 0 => "Classic", 1 => "Expert", 2 => "Master", 3 => "Journey", _ => "Mode" + Main.GameMode };
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
			{
				try
				{
					bool rev = cal.Call("GetDifficultyActive", "revengeance") is true, death = cal.Call("GetDifficultyActive", "death") is true;
					if (death) gameMode += "+Death"; else if (rev) gameMode += "+Revengeance";
				}
				catch { gameMode += "+CalamityUnknown"; }
			}
			return sb.ToString();
		}

		/// <summary>Starts a recording of <paramref name="cmd"/>. Returns the path, or null with a reason.</summary>
		public static string Start(Player cmd, string name, out string error, Dictionary<string, string> meta = null, string fileBase = null)
		{
			error = null;
			if (On) { error = "already recording to " + _path; return null; }
			try
			{
				Directory.CreateDirectory(Dir());
				_cmd = cmd; _frames = 0; _events = 0; _bossSeen = false; _bossesSeen = 0; _bossIdsSeen.Clear(); _lastBossSeen = 0;
				_items.Clear(); _npcs.Clear(); _projs.Clear(); _buffs.Clear(); _soldiers.Clear(); _pXJump.Clear();
				_meta = meta; _fileBase = fileBase; _bossGoneTick = 0; _primaryWho = -1; _tkNone = _tkPlayer = _tkLevy = 0; _lastTk = -1; _lastTl = int.MinValue; _levyHits = _levyDeaths = 0;
				_lt.Clear(); _deadNoted.Clear(); _bossTypes.Clear(); _projHit.Clear();
				_lbuf.Clear(); _lgear.Clear(); _prefixes.Clear(); _cmdGear = int.MinValue; _bt.Clear(); _bhook.Clear(); _bkilled.Clear(); _bpend.Clear(); _bAlive = false; _outcome = "none"; _hasCover = false; _chunks = 0; _camName = null; _camId = -1;
				_startTick = (int)Main.GameUpdateCount;
				_startMaxSerial = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) _startMaxSerial = Math.Max(_startMaxSerial, FightRecSpawnTag.SerialOf(Main.npc[i]));
				string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
				NPC nearest = null; float bd = float.MaxValue;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || !IsFightBoss(n)) continue;
					float d = Vector2.DistanceSquared(n.Center, cmd.Center);
					if (d < bd) { bd = d; nearest = n; }
				}
				_boss = SafeName(!string.IsNullOrWhiteSpace(name) ? name : nearest?.FullName);
				string stem = string.IsNullOrEmpty(fileBase) ? stamp + "_" + _boss : SafeName(fileBase);
				_path = System.IO.Path.Combine(Dir(), stem + ".jsonl");
				if (!string.IsNullOrEmpty(fileBase)) { int dup = 2; while (File.Exists(_path)) _path = System.IO.Path.Combine(Dir(), stem + "_" + dup++ + ".jsonl"); }
				_w = new StreamWriter(_path, false, new UTF8Encoding(false), 1 << 16);
				string mods = ModList(out string mode);
				string ver = Levy.Instance?.Version?.ToString() ?? "?";
				string metaJson = FightRecFormat.MetaJson(meta);
				_w.WriteLine("{\"k\":\"hdr\",\"format\":" + FightRecFormat.Version + ",\"meta\":" + metaJson + ",\"headless\":" + (Headless ? "1" : "0") + ",\"levy\":" + FightRecFormat.Esc(ver) + ",\"mods\":" + mods + ",\"mode\":" + FightRecFormat.Esc(mode)
					+ ",\"boss\":" + FightRecFormat.Esc(_boss) + ",\"player\":" + FightRecFormat.Esc(cmd.name) + ",\"startTick\":" + _startTick + ",\"hz\":" + (60 / SampleEvery)
					+ ",\"world\":" + FightRecFormat.Esc(Main.worldName) + ",\"date\":" + FightRecFormat.Esc(DateTime.Now.ToString("s"))
					+ ",\"stateNames\":" + FightRecFormat.Esc(string.Join(",", Enum.GetNames(typeof(SoldierState))))
					+ ",\"schemaP\":" + FightRecFormat.Esc(FightRecFormat.SchemaP) + ",\"flagsP\":" + FightRecFormat.Esc(FightRecFormat.SchemaPFlags)
					+ ",\"schemaB\":" + FightRecFormat.Esc(FightRecFormat.SchemaB) + ",\"schemaJ\":" + FightRecFormat.Esc(FightRecFormat.SchemaJ)
					+ ",\"tkinds\":" + FightRecFormat.Esc(FightRecFormat.TKinds) + ",\"schemaA\":" + FightRecFormat.Esc(FightRecFormat.SchemaA) + ",\"schemaL\":" + FightRecFormat.Esc(FightRecFormat.SchemaL) + ",\"flagsL\":" + FightRecFormat.Esc(FightRecFormat.SchemaLFlags) + "}");
				WriteArena(cmd);
				Array.Clear(_accLast); _accLast[0] = int.MinValue; // force the first accessory event
				_pJump = cmd.jump; _pAnim = cmd.itemAnimation; _pDashDelay = cmd.dashDelay; _pFly = false; _pHookOut = false; _pHookAtt = false;
				On = true;
				return _path;
			}
			catch (Exception e) { error = e.Message; Abort(); return null; }
		}

		private static void Abort()
		{
			On = false;
			try { _w?.Dispose(); } catch { }
			_w = null; _cmd = null;
		}

		public static string Stop(string reason)
		{
			if (!On) return null;
			On = false;
			if (Headless) FightRecAuto.Stopped();
			string final = _path;
			try
			{
				FlushBossEnd(true);
				_w.WriteLine("{\"k\":\"names\",\"items\":" + Names(_items) + ",\"npcs\":" + Names(_npcs) + ",\"projectiles\":" + Names(_projs) + ",\"buffs\":" + Names(_buffs) + ",\"soldiers\":" + Names(_soldiers) + ",\"prefixes\":" + Names(_prefixes) + "}");
				_w.WriteLine("{\"k\":\"end\",\"reason\":" + FightRecFormat.Esc(reason) + ",\"ticks\":" + Elapsed + ",\"frames\":" + _frames + ",\"events\":" + _events
					+ ",\"target\":{\"none\":" + _tkNone + ",\"player\":" + _tkPlayer + ",\"levy\":" + _tkLevy + "},\"levyHits\":" + _levyHits + ",\"levyDeaths\":" + _levyDeaths + ",\"outcome\":" + FightRecFormat.Esc(_outcome) + ",\"arenaChunks\":" + _chunks + "}");
				_w.Dispose(); _w = null;
				if (Headless && _frames < 90) { File.Delete(_path); Levy.Instance?.Logger.Info("[FightRec] discarded a " + _frames + "-sample recording (" + reason + ")"); _path = null; _cmd = null; _bossNameSeen = null; return null; }
				if (Headless) Levy.Instance?.Logger.Info("[FightRec] saved " + _path + " (" + reason + ", " + _frames + " samples, levy hits " + _levyHits + ", deaths " + _levyDeaths + ", target none/player/levy " + _tkNone + "/" + _tkPlayer + "/" + _tkLevy + ")");
				// the boss may have appeared after the start: rename once to carry its name
				if (_boss == "none" && _bossNameSeen != null && string.IsNullOrEmpty(_fileBase))
				{
					string np = System.IO.Path.Combine(Dir(), System.IO.Path.GetFileName(_path).Replace("_none.jsonl", "_" + SafeName(_bossNameSeen) + ".jsonl"));
					if (np != _path && !File.Exists(np)) { File.Move(_path, np); final = np; }
				}
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRec] stop failed: " + e.Message); }
			_path = final; _cmd = null; _bossNameSeen = null;
			if (!Main.dedServ && reason != "stopped by command") Main.NewText("Fight recording saved (" + reason + "): " + final, 160, 255, 160);
			return final;
		}
		private static string _bossNameSeen;

		private static void Line(string s) { _w.WriteLine(s); }
		private static void Ev(string kind, params (string, string)[] extra) { Line(FightRecFormat.Event(Elapsed, kind, extra)); _events++; }
		private static string ItemName(Item it)
		{
			if (it == null || it.IsAir) return "0";
			if (!_items.ContainsKey(it.type)) _items[it.type] = it.Name;
			return it.type.ToString();
		}

		/// <summary>Called by the system hooks when a player takes damage (ModPlayer.OnHurt).</summary>
		public static void NoteHit(Player p, int damage, int srcNpcIndex, int srcProjType, int srcOther)
		{
			if (!On || p != _cmd) return;
			try
			{
				int npcType = -1;
				if (srcNpcIndex >= 0 && srcNpcIndex < Main.maxNPCs) { npcType = Main.npc[srcNpcIndex].type; _npcs[npcType] = Main.npc[srcNpcIndex].FullName; }
				if (srcProjType > 0 && !_projs.ContainsKey(srcProjType)) _projs[srcProjType] = Lang.GetProjectileName(srcProjType).Value;
				Ev("hit", ("dmg", damage.ToString()), ("npc", npcType.ToString()), ("proj", srcProjType.ToString()), ("other", srcOther.ToString()), ("life", p.statLife.ToString()));
			}
			catch { }
		}

		// ------------------------------------------------------------------------------------------ arena
		private static void WriteArena(Player p)
		{
			int cx = (int)(p.Center.X / 16f), cy = (int)(p.Center.Y / 16f);
			_startCx = cx; _startCy = cy;
			int x0 = Math.Max(0, Math.Min(Main.maxTilesX - ArenaW, cx - ArenaW / 2)), y0 = Math.Max(0, Math.Min(Main.maxTilesY - ArenaH, cy - ArenaH / 2));
			Cover(x0, y0, x0 + Math.Min(ArenaW, Main.maxTilesX), y0 + Math.Min(ArenaH, Main.maxTilesY));
			try { CoverCamera(p); } catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRec] static camera terrain failed: " + e.Message); }
		}

		/// <summary>A Static Camera placed near the commander (or the boss) gives the player's own arena: capture the terrain its view shows. Falls back to the plain arena growth when there is none.</summary>
		private static void CoverCamera(Player p)
		{
			StaticCameraTE best = null; float bd = float.MaxValue;
			Vector2 boss = Vector2.Zero; bool haveBoss = false;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && !(n.ModNPC is SoldierNPC) && IsFightBoss(n)) { boss = n.Center; haveBoss = true; break; } }
			foreach (var te in StaticCameraTE.All())
			{
				float d = Vector2.Distance(te.ViewCenter, p.Center);
				if (haveBoss) d = Math.Min(d, Vector2.Distance(te.ViewCenter, boss));
				if (d <= CamNearTiles * 16f && d < bd) { bd = d; best = te; }
			}
			if (best == null) return;
			float f = 2f; try { f = ObserveSystem.MaxZoom; } catch { }
			if (!(f >= 1f)) f = 2f;
			int sw = Main.dedServ ? 1920 : Math.Max(800, Main.screenWidth), sh = Main.dedServ ? 1080 : Math.Max(600, Main.screenHeight);
			int wt = Math.Min(CamMaxW, (int)Math.Ceiling(sw * f / 16f)), ht = Math.Min(CamMaxH, (int)Math.Ceiling(sh * f / 16f));
			int vx = (int)(best.ViewCenter.X / 16f), vy = (int)(best.ViewCenter.Y / 16f);
			int x0 = vx - wt / 2, y0 = vy - ht / 2;
			_camName = best.Display; _camId = best.ID;
			Line("{\"k\":\"cam\",\"id\":" + best.ID + ",\"name\":" + FightRecFormat.Esc(best.Display) + ",\"x\":" + vx + ",\"y\":" + vy + ",\"x0\":" + Math.Max(0, x0) + ",\"y0\":" + Math.Max(0, y0)
				+ ",\"w\":" + wt + ",\"h\":" + ht + ",\"zoom\":" + FightRecFormat.N(f) + "}");
			Cover(x0, y0, x0 + wt, y0 + ht);
		}

		/// <summary>Captures the tiles of [x0,x1) x [y0,y1) that are not covered yet (as non-overlapping strips) and grows the covered area to the union. Returns false when nothing was added.</summary>
		private static bool Cover(int x0, int y0, int x1, int y1)
		{
			x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(Main.maxTilesX, x1); y1 = Math.Min(Main.maxTilesY, y1);
			if (x1 <= x0 || y1 <= y0) return false;
			if (!_hasCover)
			{
				EmitChunk(x0, y0, x1, y1);
				_cx0 = x0; _cy0 = y0; _cx1 = x1; _cy1 = y1; _hasCover = true; _coverTiles = (x1 - x0) * (y1 - y0);
				return true;
			}
			if (x0 >= _cx0 && y0 >= _cy0 && x1 <= _cx1 && y1 <= _cy1) return false;
			int vx0 = Math.Min(_cx0, x0), vy0 = Math.Min(_cy0, y0), vx1 = Math.Max(_cx1, x1), vy1 = Math.Max(_cy1, y1);
			if (vx1 - vx0 > MaxCoverW || vy1 - vy0 > MaxCoverH || (long)(vx1 - vx0) * (vy1 - vy0) > MaxCoverTiles) return false;
			EmitChunk(vx0, vy0, _cx0, vy1);      // left of the old area, full height
			EmitChunk(_cx1, vy0, vx1, vy1);      // right
			EmitChunk(_cx0, vy0, _cx1, _cy0);    // above
			EmitChunk(_cx0, _cy1, _cx1, vy1);    // below
			_cx0 = vx0; _cy0 = vy0; _cx1 = vx1; _cy1 = vy1; _coverTiles = (vx1 - vx0) * (vy1 - vy0);
			return true;
		}

		private static void EmitChunk(int x0, int y0, int x1, int y1)
		{
			int w = x1 - x0, h = y1 - y0;
			if (w <= 0 || h <= 0) return;
			var rows = new List<int[]>(h); var codes = new int[w];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					Tile t = Framing.GetTileSafely(x0 + x, y0 + y);
					int c = 0;
					if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) c = 1;
					else if (t.HasUnactuatedTile && Main.tileSolidTop[t.TileType]) c = 2;
					else if (t.LiquidAmount > 100) c = 3;
					codes[x] = c;
				}
				rows.Add(FightRecFormat.RleEncode(codes).ToArray());
			}
			Line(FightRecFormat.Arena(x0, y0, w, h, rows));
			_chunks++;
		}

		private static void SeeEntity(float px, float py)
		{
			int tx = (int)(px / 16f), ty = (int)(py / 16f);
			if (Math.Abs(tx - _startCx) > EntityReachX || Math.Abs(ty - _startCy) > EntityReachY) return;
			if (tx < _ex0) _ex0 = tx; if (tx > _ex1) _ex1 = tx; if (ty < _ey0) _ey0 = ty; if (ty > _ey1) _ey1 = ty;
		}

		private static int FloorStep(int v) => (int)Math.Floor(v / (double)GrowStep) * GrowStep;
		private static int CeilStep(int v) => (int)Math.Ceiling(v / (double)GrowStep) * GrowStep;

		/// <summary>After each sample: when the commander, a boss part or a levy went outside the captured terrain (plus a margin), capture the missing tiles.</summary>
		private static void GrowTerrain()
		{
			if (!_hasCover || _ex1 < _ex0) return;
			int m = GrowMargin;
			if (_ex0 - m >= _cx0 && _ex1 + m < _cx1 && _ey0 - m >= _cy0 && _ey1 + m < _cy1) return;
			Cover(FloorStep(_ex0 - m), FloorStep(_ey0 - m), CeilStep(_ex1 + m + 1), CeilStep(_ey1 + m + 1));
		}

		// ------------------------------------------------------------------------------------------ sampling
		private static bool Grounded(Entity e) => Math.Abs(e.velocity.Y) < 0.01f;

		/// <summary>One call per game tick while recording. Samples on every SampleEvery-th tick.</summary>
		public static void Tick()
		{
			if (!On) return;
			try
			{
				if (_cmd == null || (!_cmd.active && !IgnoreInactive && !Headless)) { Stop("player gone"); return; }
				int el = Elapsed;
				PollLevies(el);
				PollBosses(el);
				if (el >= MaxTicks) { Stop("10 minute limit"); return; }
				if (el % SampleEvery != 0) return;
				Sample(el);
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRec] sample failed, stopping: " + e); Stop("error: " + e.Message); }
		}

		private static void Sample(int el)
		{
			Player p = _cmd;
			var f = new RecFrame { T = el };
			_ex0 = _ey0 = int.MaxValue; _ex1 = _ey1 = int.MinValue; SeeEntity(p.Center.X, p.Center.Y);
			bool grounded = Grounded(p) || p.sliding;
			// hook
			bool hookOut = false, hookAtt = false; Vector2 hookPos = Vector2.Zero;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile pr = Main.projectile[i];
				if (!pr.active || pr.owner != p.whoAmI || pr.aiStyle != 7) continue;
				hookOut = true; if (pr.ai[0] == 2f) { hookAtt = true; hookPos = pr.Center; }
			}
			bool flyKit = (p.wingsLogic > 0 && p.wingTime > 0) || (p.rocketBoots > 0 && p.rocketTime > 0);
			bool fly = !grounded && p.controlJump && flyKit && p.jump == 0;
			bool dashing = p.dashDelay < 0 || p.timeSinceLastDashStarted < 8;
			int flags = (grounded ? 1 : 0) | (fly ? 2 : 0) | (hookOut ? 4 : 0) | (hookAtt ? 8 : 0) | (p.controlLeft ? 16 : 0) | (p.controlRight ? 32 : 0) | (p.controlJump ? 64 : 0)
				| (p.controlDown ? 128 : 0) | (p.controlUp ? 256 : 0) | (p.controlHook ? 512 : 0) | (p.controlUseItem ? 1024 : 0) | (dashing ? 2048 : 0) | (p.mount.Active ? 4096 : 0) | (p.dead ? 8192 : 0);
			Item held = p.HeldItem;
			f.P = new double[] { p.Center.X, p.Center.Y, p.velocity.X, p.velocity.Y, p.direction, flags, p.statLife, p.statMana, p.immuneTime, p.wingTime, p.rocketTime,
				held != null && !held.IsAir ? held.type : 0, p.mount.Active ? p.mount.Type : -1, hookPos.X, hookPos.Y, p.dashDelay };
			if (held != null && !held.IsAir && !_items.ContainsKey(held.type)) _items[held.type] = held.Name;

			// events
			if (p.jump > 0 && _pJump == 0) Ev("jump", ("x", FightRecFormat.N(p.Center.X)), ("y", FightRecFormat.N(p.Center.Y)));
			_pJump = p.jump;
			ExtraJumpEvents(p);
			if (fly != _pFly) Ev("fly", ("on", fly ? "1" : "0"), ("wing", p.wings.ToString()), ("wt", FightRecFormat.N(p.wingTime)), ("rocket", p.rocketBoots.ToString()));
			_pFly = fly;
			bool dashStart = (_pDashDelay >= 0 && p.dashDelay < 0) || p.timeSinceLastDashStarted < _pTimeDash;
			if (dashStart) Ev("dash", ("type", p.dash.ToString()), ("dashType", p.dashType.ToString()), ("dir", p.direction.ToString()), ("vx", FightRecFormat.N(p.velocity.X)));
			_pDashDelay = p.dashDelay; _pTimeDash = p.timeSinceLastDashStarted;
			if (hookOut && !_pHookOut) Ev("hook", ("phase", "\"launch\""));
			if (hookAtt && !_pHookAtt) Ev("hook", ("phase", "\"attach\""), ("x", FightRecFormat.N(hookPos.X)), ("y", FightRecFormat.N(hookPos.Y)));
			if (!hookOut && _pHookOut) Ev("hook", ("phase", "\"release\""));
			_pHookOut = hookOut; _pHookAtt = hookAtt;
			if (p.itemAnimation > _pAnim && held != null && !held.IsAir) Ev("use", ("item", ItemName(held)), ("mana", p.statMana.ToString()));
			_pAnim = p.itemAnimation;
			if (el % 60 == 0) { WriteBuffs(p, el); CommanderGear(p); }
			AccessoryEvents(p);

			// bosses and parts
			bool anyBoss = false, otherSerial = false;
			if (Headless && _primaryWho >= 0)
			{
				// a harness that clears the arena and spawns the next trial's boss in the same step leaves no gap: the boss we follow is gone and a boss with a newer spawn serial stands in the world
				NPC pn = Main.npc[_primaryWho];
				if (!(pn.active && IsFightBoss(pn) && !IsNewFightSerial(pn)))
					for (int i = 0; i < Main.maxNPCs; i++)
					{
						NPC n = Main.npc[i];
						if (n.active && IsFightBoss(n) && !n.friendly && !n.townNPC && !(n.ModNPC is SoldierNPC) && IsNewFightSerial(n)) { Stop("next fight started"); return; }
					}
			}
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC) continue;
				if (!IsBossOrPart(n)) continue;
				bool isBoss = IsFightBoss(n);
				if (isBoss) { anyBoss = true; if (_primaryWho >= 0 && IsNewFightSerial(n)) otherSerial = true; }
				bool part = !isBoss;
				SeeEntity(n.Center.X, n.Center.Y);
				if (!_npcs.ContainsKey(n.type)) _npcs[n.type] = n.FullName;
				if (isBoss && _bossIdsSeen.Add(n.whoAmI)) { _bossesSeen++; Ev("boss", ("npc", n.type.ToString()), ("name", FightRecFormat.Esc(n.FullName)), ("id", n.whoAmI.ToString())); if (_bossNameSeen == null) _bossNameSeen = n.FullName; }
				_bossTypes.Add(n.type);
				TargetOf(n, out int tk, out int tl);
				f.B.Add(new double[] { n.type, n.whoAmI, n.Center.X, n.Center.Y, n.velocity.X, n.velocity.Y, n.life, n.lifeMax, n.ai[0], n.ai[1], n.ai[2], n.ai[3], n.target, part ? 1 : 0, tk, tl, n.width, n.height });
				if (isBoss)
				{
					if (_primaryWho < 0) { _primaryWho = n.whoAmI; _primarySerial = FightRecSpawnTag.SerialOf(n); }
					if (n.whoAmI == _primaryWho && FightRecSpawnTag.SerialOf(n) == _primarySerial)
					{
						if (tk == 2) _tkLevy++; else if (tk == 1) _tkPlayer++; else _tkNone++;
						if (tk != _lastTk || tl != _lastTl) { _lastTk = tk; _lastTl = tl; Ev("tgt", ("kind", tk.ToString()), ("levy", tl.ToString()), ("boss", n.whoAmI.ToString())); }
					}
				}
			}
			int grace = Headless ? HeadlessGraceTicks : GraceTicks;
			if (anyBoss)
			{
				if (Headless && _bossGoneTick > 0)
				{
					// the boss slot filled again after a gap: a new trial (a different spawn serial) gets its own file. The same serial is the same fight (Eater of Worlds: when its head dies a body segment becomes the head a few ticks later).
					if (otherSerial) { Stop("next fight started"); return; }
					_bossGoneTick = 0;
				}
				_bossSeen = true; _lastBossSeen = el;
			}
			else if (_bossSeen)
			{
				if (_bossGoneTick == 0) _bossGoneTick = el;
				if (el - _lastBossSeen > grace) { Stop("boss gone"); return; }
			}

			// hostile projectiles near the player, nearest first when there are too many
			int pc = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile pr = Main.projectile[i];
				if (!pr.active || !pr.hostile) continue;
				if (Math.Abs(pr.Center.X - p.Center.X) > ProjRangePx || Math.Abs(pr.Center.Y - p.Center.Y) > ProjRangePx) continue;
				if (!_projs.ContainsKey(pr.type)) _projs[pr.type] = Lang.GetProjectileName(pr.type).Value;
				if (pc++ >= MaxProj) break;
				f.J.Add(new double[] { pr.type, pr.Center.X, pr.Center.Y, pr.velocity.X, pr.velocity.Y, pr.damage, pr.width, pr.height });
			}

			// levies
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.ModNPC is SoldierNPC s)) continue;
				if (Math.Abs(n.Center.X - p.Center.X) > LevyRangePx || Math.Abs(n.Center.Y - p.Center.Y) > LevyRangePx) continue;
				int id = s.Record != null ? s.Record.Id : -n.whoAmI;
				if (s.Record != null && !_soldiers.ContainsKey(id)) _soldiers[id] = s.Record.Name;
				SeeEntity(n.Center.X, n.Center.Y);
				if (el % 30 == 0) LevyBuffs(n, id, el);
				if (el % 60 == 0) LevyGear(s, id);
				Item lh = s.Dummy?.HeldItem;
				int lflags = (Grounded(n) ? 1 : 0) | (s.IsFlying ? 2 : 0) | (s.IsAerial ? 4 : 0) | (s.IsHooking ? 8 : 0) | (s.MoveBlocked ? 16 : 0);
				if (lh != null && !lh.IsAir && !_items.ContainsKey(lh.type)) _items[lh.type] = lh.Name;
				f.L.Add(new double[] { id, n.Center.X, n.Center.Y, n.velocity.X, n.velocity.Y, lflags, n.life, (int)s.State, lh != null && !lh.IsAir ? lh.type : 0, s.WingTime, n.lifeMax, n.width, n.height });
			}
			// hostile adds and minions that are not boss parts (creepers, servants, worms, bees, slimes ...)
			int ac = 0;
			for (int i = 0; i < Main.maxNPCs && ac < MaxAdds; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || n.damage <= 0 || n.lifeMax <= 1 || n.dontCountMe) continue;
				if (Math.Abs(n.Center.X - p.Center.X) > AddRangePx || Math.Abs(n.Center.Y - p.Center.Y) > AddRangePx) continue;
				if (IsBossOrPart(n)) continue;
				if (!_npcs.ContainsKey(n.type)) _npcs[n.type] = n.FullName;
				TargetOf(n, out int atk, out int atl);
				SeeEntity(n.Center.X, n.Center.Y);
				f.A.Add(new double[] { n.type, n.whoAmI, n.Center.X, n.Center.Y, n.velocity.X, n.velocity.Y, n.life, n.lifeMax, atk, atl, n.width, n.height });
				ac++;
			}
			GrowTerrain();
			Line(FightRecFormat.Frame(f));
			_frames++;
		}

		// ------------------------------------------------------------------------------------------ boss target, levy hits and deaths
		/// <summary>What the boss is attacking right now: kind 0 none, 1 player, 2 levy (levy = its record id, or -whoAmI without a record). Reads the targeting lock and the boss-rule spite target.</summary>
		private static void TargetOf(NPC n, out int kind, out int levyId)
		{
			kind = 0; levyId = -1;
			try
			{
				var data = LevyNpcData.Of(n);
				int st = data.SoldierTarget;
				if (!(st >= 0 && st < Main.maxNPCs && Main.npc[st].active && Main.npc[st].ModNPC is SoldierNPC))
				{
					st = -1;
					if (n.target >= 300 && n.target - 300 < Main.maxNPCs && Main.npc[n.target - 300].active && Main.npc[n.target - 300].ModNPC is SoldierNPC) st = n.target - 300;
					else { var sp = BossRule.SpiteTargetOf(n); if (sp != null && sp.NPC.active) st = sp.NPC.whoAmI; }
				}
				if (st >= 0) { var s = (SoldierNPC)Main.npc[st].ModNPC; kind = 2; levyId = s.Record != null ? s.Record.Id : -st; return; }
				if (n.target >= 0 && n.target < Main.maxPlayers) kind = 1; // the stand-in commander of a headless server flips inactive, so a valid player slot counts
				else if (n.target == SoldierNPC.DummySlot) kind = 0;
			}
			catch { }
		}

		/// <summary>Called from a GlobalNPC just before a hostile projectile strikes a levy, so the hit event can name the projectile.</summary>
		public static void NoteLevyProjectile(NPC levy, Projectile pr)
		{
			if (!On) return;
			_projHit[levy.whoAmI] = (Main.GameUpdateCount, pr.type);
			if (!_projs.ContainsKey(pr.type)) _projs[pr.type] = Lang.GetProjectileName(pr.type).Value;
		}

		private static bool AttackerIsBoss(SoldierNPC s)
		{
			int idx = s.LastAttackerIndex;
			if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].type == s.LastAttackerType) return IsBossOrPart(Main.npc[idx]);
			return s.LastAttackerType > 0 && _bossTypes.Contains(s.LastAttackerType);
		}

		private static void LevyHitEvent(SoldierNPC s, NPC n, int id, int dmg, int lifeAfter, bool kill)
		{
			bool proj = _projHit.TryGetValue(n.whoAmI, out var ph) && Main.GameUpdateCount - ph.tick <= 1;
			bool boss = AttackerIsBoss(s);
			bool none = s.LastAttackerType <= 0 && s.LastAttackerIndex < 0;
			string src = proj ? "proj" : none ? "other" : boss ? "body" : "minion";
			string by = none ? "world" : boss ? "boss" : "minion";
			int at = s.LastAttackerType;
			if (at > 0 && !_npcs.ContainsKey(at)) _npcs[at] = Lang.GetNPCNameValue(at);
			Ev("lhit", ("id", id.ToString()), ("dmg", dmg.ToString()), ("src", FightRecFormat.Esc(src)), ("by", FightRecFormat.Esc(by)), ("npc", at.ToString()), ("proj", (proj ? ph.proj : -1).ToString()),
				("life", Math.Max(0, lifeAfter).ToString()), ("x", FightRecFormat.N(n.Center.X)), ("y", FightRecFormat.N(n.Center.Y)), ("kill", kill ? "1" : "0"));
			_levyHits++;
		}

		/// <summary>Called from the levy's GlobalNPC.CheckDead (the lethal blow). Logs the killing hit and the death.</summary>
		public static void NoteLevyDeath(NPC n)
		{
			if (!On || !(n.ModNPC is SoldierNPC s)) return;
			try
			{
				int id = s.Record != null ? s.Record.Id : -n.whoAmI;
				if (!_deadNoted.Add(id)) return;
				_lt.TryGetValue(id, out var t);
				LevyHitEvent(s, n, id, t != null ? Math.Max(1, t.Life) : 1, 0, true);
				Ev("ldeath", ("id", id.ToString()), ("x", FightRecFormat.N(n.Center.X)), ("y", FightRecFormat.N(n.Center.Y)), ("npc", s.LastAttackerType.ToString()), ("by", FightRecFormat.Esc(AttackerIsBoss(s) ? "boss" : s.LastAttackerType > 0 ? "minion" : "world")));
				_levyDeaths++;
				_lt.Remove(id);
			}
			catch { }
		}

		/// <summary>Once per tick: a levy whose life dropped since the last tick was hit (the lethal blow comes through NoteLevyDeath).</summary>
		private static void PollLevies(int el)
		{
			foreach (var t in _lt.Values) t.Seen = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.ModNPC is SoldierNPC s) || n.life <= 0) continue;
				int id = s.Record != null ? s.Record.Id : -n.whoAmI;
				if (_deadNoted.Contains(id)) continue;
				if (!_lt.TryGetValue(id, out var t)) { t = new LTrack { Life = n.life }; _lt[id] = t; }
				if (n.life < t.Life) LevyHitEvent(s, n, id, t.Life - n.life, n.life, false);
				t.Life = n.life; t.Max = n.lifeMax; t.X = n.Center.X; t.Y = n.Center.Y; t.Who = n.whoAmI; t.Seen = true;
			}
			// a tracked levy that vanished without a lethal blow was removed by the harness (cleanup, replacement); not a death
			_gone.Clear();
			foreach (var kv in _lt) if (!kv.Value.Seen) _gone.Add(kv.Key);
			foreach (int id in _gone) _lt.Remove(id);
		}

		// ------------------------------------------------------------------------------------------ boss death / despawn (format 4)
		/// <summary>GlobalNPC hook: a fight boss was struck (damage from NPC.HitEffect).</summary>
		public static void NoteBossHit(NPC n, int dmg)
		{
			if (!On || !IsFightBoss(n)) return;
			_bhook[n.whoAmI] = (Elapsed, dmg, "other", -1, "");
		}

		/// <summary>GlobalNPC hook: names what struck the boss (a player item or a projectile), called right after the hit.</summary>
		public static void NoteBossSource(NPC n, string src, int type, string name)
		{
			if (!On || !IsFightBoss(n)) return;
			if (_bhook.TryGetValue(n.whoAmI, out var h) && Elapsed - h.tick <= 1) _bhook[n.whoAmI] = (h.tick, h.dmg, src, type, name ?? "");
			else _bhook[n.whoAmI] = (Elapsed, 0, src, type, name ?? "");
		}

		/// <summary>GlobalNPC.OnKill of a fight boss: it died (as opposed to leaving).</summary>
		public static void NoteBossKill(NPC n)
		{
			if (!On || !IsFightBoss(n)) return;
			_bkilled.Add(n.whoAmI);
		}

		private static void PollBosses(int el)
		{
			foreach (var t in _bt.Values) t.Seen = false;
			bool alive = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || !IsFightBoss(n)) continue;
				int serial = FightRecSpawnTag.SerialOf(n);
				if (_bt.TryGetValue(i, out var t) && (t.Type != n.type || (serial != t.Serial && serial > 0 && t.Serial > 0)))
				{
					// the slot now holds a different NPC: the old one left
					_bt.Remove(i); BossGone(t, el); _bkilled.Remove(i); _bhook.Remove(i); t = null;
				}
				if (t == null)
				{
					t = new BTrack { Slot = i, Type = n.type, Serial = serial, Life = n.life, Name = n.FullName };
					_bt[i] = t;
				}
				if (n.life < t.Life)
				{
					if (_bhook.TryGetValue(i, out var h) && el - h.tick <= 2) { t.HitT = h.tick; t.HitDmg = h.dmg > 0 ? h.dmg : t.Life - n.life; t.HitSrc = h.src; t.HitType = h.type; t.HitName = h.name; }
					else { t.HitT = el; t.HitDmg = t.Life - n.life; t.HitSrc = "other"; t.HitType = -1; t.HitName = ""; }
				}
				t.Life = n.life; t.Max = n.lifeMax; t.X = n.Center.X; t.Y = n.Center.Y; t.Seen = true;
				if (!(Headless && IsNewFightSerial(n))) alive = true;
			}
			_bgone.Clear();
			foreach (var kv in _bt) if (!kv.Value.Seen) _bgone.Add(kv.Key);
			foreach (int id in _bgone) { var t = _bt[id]; _bt.Remove(id); BossGone(t, el); _bkilled.Remove(id); _bhook.Remove(id); }
			_bAlive = alive;
			FlushBossEnd(false);
		}

		private static void BossGone(BTrack t, int el)
		{
			// killed: the game's OnKill fired for that slot, or its last seen life was already 0
			bool killed = t.Life <= 0 || _bkilled.Contains(t.Slot);
			// the lethal blow lands after the last poll: take the hook's note, if any
			if (_bhook.TryGetValue(t.Slot, out var h) && h.tick >= t.HitT) { t.HitT = h.tick; t.HitDmg = h.dmg > 0 ? h.dmg : t.HitDmg; t.HitSrc = h.src; t.HitType = h.type; t.HitName = h.name; }
			_bpend.Add((t, killed, el));
		}

		/// <summary>Writes bdeath / bdespawn once the boss has been gone for a few ticks (a worm's head is replaced by a segment within a few ticks; that is not an end).</summary>
		private static void FlushBossEnd(bool force)
		{
			if (_bpend.Count == 0) return;
			int el = Elapsed;
			if (!force && el - _bpend[_bpend.Count - 1].tick < EndGapTicks) return;
			if (_bAlive)
			{
				// others still stand: a different boss type that ended is an event of its own (Twins), a same-type successor is a handoff
				foreach (var p in _bpend)
				{
					bool successor = false; foreach (var o in _bt.Values) if (o.Type == p.t.Type) successor = true;
					if (!successor) WriteBossEnd(p.t, p.killed, p.tick, false);
				}
				_bpend.Clear();
				return;
			}
			var last = _bpend[_bpend.Count - 1];
			WriteBossEnd(last.t, last.killed, last.tick, true); // earlier worm heads that died one after another are dropped; the final one is the outcome
			_bpend.Clear();
		}

		private static void WriteBossEnd(BTrack t, bool killed, int tick, bool final)
		{
			if (final) _outcome = killed ? "killed" : "despawn";
			var ex = new List<(string, string)> { ("npc", t.Type.ToString()), ("name", FightRecFormat.Esc(t.Name)), ("id", t.Slot.ToString()), ("x", FightRecFormat.N(t.X)), ("y", FightRecFormat.N(t.Y)),
				("life", Math.Max(0, t.Life).ToString()), ("max", t.Max.ToString()), ("final", final ? "1" : "0") };
			if (t.HitT >= 0) { ex.Add(("ht", t.HitT.ToString())); ex.Add(("hd", t.HitDmg.ToString())); ex.Add(("hs", FightRecFormat.Esc(t.HitSrc))); ex.Add(("hy", t.HitType.ToString())); ex.Add(("hn", FightRecFormat.Esc(t.HitName ?? ""))); }
			Line(FightRecFormat.Event(tick, killed ? "bdeath" : "bdespawn", ex.ToArray())); _events++;
		}

		private static string GearItem(Item it, int slot, bool stack)
		{
			if (it == null || it.IsAir) return null;
			if (!_items.ContainsKey(it.type)) _items[it.type] = it.Name;
			if (it.prefix > 0 && !_prefixes.ContainsKey(it.prefix)) { try { _prefixes[it.prefix] = Lang.prefix[it.prefix].Value; } catch { } }
			return stack ? "[" + slot + "," + it.type + "," + it.prefix + "," + it.stack + "]" : "[" + slot + "," + it.type + "," + it.prefix + "]";
		}

		private static int GearHash(Item it, int h) => it == null || it.IsAir ? h * 31 + 7 : (h * 31 + it.type) * 31 + it.prefix * 7;

		/// <summary>The commander's equipment: armor, accessories, vanity, hotbar and ammo. Written when it changes.</summary>
		private static void CommanderGear(Player p)
		{
			int h = 17;
			for (int i = 0; i < 20; i++) h = GearHash(p.armor[i], h);
			for (int i = 0; i < 10; i++) h = GearHash(p.inventory[i], h);
			for (int i = 54; i < 58; i++) h = GearHash(p.inventory[i], h);
			if (h == _cmdGear) return;
			_cmdGear = h;
			var arm = new List<string>(); var inv = new List<string>();
			for (int i = 0; i < 20; i++) { string g = GearItem(p.armor[i], i, false); if (g != null) arm.Add(g); }
			for (int i = 0; i < 10; i++) { string g = GearItem(p.inventory[i], i, true); if (g != null) inv.Add(g); }
			for (int i = 54; i < 58; i++) { string g = GearItem(p.inventory[i], i, true); if (g != null) inv.Add(g); }
			Ev("gear", ("who", "\"cmd\""), ("arm", "[" + string.Join(",", arm) + "]"), ("inv", "[" + string.Join(",", inv) + "]"));
		}

		/// <summary>One levy's gear (weapon, sidearm, armor, accessories, ammo pouch, hook), written at first sight and when it changes.</summary>
		private static void LevyGear(SoldierNPC s, int id)
		{
			var g = s.Record?.Gear; if (g == null) return;
			int h = 17;
			h = GearHash(g.Weapon, h); h = GearHash(g.Sidearm, h); h = GearHash(g.Head, h); h = GearHash(g.Body, h); h = GearHash(g.Legs, h);
			for (int i = 0; i < g.Accessories.Length; i++) h = GearHash(g.Accessories[i], h);
			h = GearHash(g.Ammo, h); h = GearHash(g.Hook, h);
			if (_lgear.TryGetValue(id, out int was) && was == h) return;
			_lgear[id] = h;
			var l = new List<string>();
			void Add(Item it, int slot) { string x = GearItem(it, slot, true); if (x != null) l.Add(x); }
			Add(g.Weapon, 0); Add(g.Sidearm, 1); Add(g.Head, 2); Add(g.Body, 3); Add(g.Legs, 4);
			for (int i = 0; i < g.Accessories.Length; i++) Add(g.Accessories[i], 5 + i);
			Add(g.Ammo, 11); Add(g.Hook, 12);
			Ev("lgear", ("id", id.ToString()), ("g", "[" + string.Join(",", l) + "]"));
		}

		/// <summary>A levy's buffs, written when the set changes (checked twice a second).</summary>
		private static void LevyBuffs(NPC n, int id, int el)
		{
			var sb = new StringBuilder("["); bool first = true; string key = "";
			for (int i = 0; i < NPC.maxBuffs; i++)
			{
				if (n.buffType[i] <= 0 || n.buffTime[i] <= 0) continue;
				if (!_buffs.ContainsKey(n.buffType[i])) _buffs[n.buffType[i]] = Lang.GetBuffName(n.buffType[i]);
				if (!first) sb.Append(','); first = false;
				sb.Append('[').Append(n.buffType[i]).Append(',').Append(n.buffTime[i]).Append(']'); key += n.buffType[i] + ",";
			}
			sb.Append(']');
			_lbuf.TryGetValue(id, out string was);
			if ((was ?? "") == key) return;
			_lbuf[id] = key;
			Ev("lbuf", ("id", id.ToString()), ("b", sb.ToString()));
		}

		private static void ExtraJumpEvents(Player p)
		{
			foreach (var (key, jump) in ExtraJumpList())
			{
				bool active = p.GetJumpState(jump).Active;
				_pXJump.TryGetValue(key, out bool was);
				if (active && !was) Ev("xjump", ("name", FightRecFormat.Esc(jump.Name)), ("x", FightRecFormat.N(p.Center.X)), ("y", FightRecFormat.N(p.Center.Y)));
				_pXJump[key] = active;
			}
		}

		private static List<(int, ExtraJump)> _xj;
		private static List<(int, ExtraJump)> ExtraJumpList()
		{
			if (_xj != null) return _xj;
			_xj = new List<(int, ExtraJump)>();
			var seen = new HashSet<ExtraJump>();
			void Add(ExtraJump j) { if (j != null && seen.Add(j)) _xj.Add((_xj.Count, j)); }
			try
			{
				const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
				foreach (var pi in typeof(ExtraJump).GetProperties(bf)) if (pi.PropertyType == typeof(ExtraJump) && pi.GetIndexParameters().Length == 0) Add(pi.GetValue(null) as ExtraJump);
				foreach (var fi in typeof(ExtraJumpLoader).GetFields(bf))
					if (fi.GetValue(null) is System.Collections.IEnumerable en) foreach (object o in en) if (o is ExtraJump j) Add(j);
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRec] extra jump list: " + e.Message); }
			return _xj;
		}

		private static void WriteBuffs(Player p, int el)
		{
			var sb = new StringBuilder("[");
			bool first = true;
			for (int i = 0; i < Player.MaxBuffs; i++)
			{
				if (p.buffType[i] <= 0) continue;
				if (!_buffs.ContainsKey(p.buffType[i])) _buffs[p.buffType[i]] = Lang.GetBuffName(p.buffType[i]);
				if (!first) sb.Append(','); first = false;
				sb.Append('[').Append(p.buffType[i]).Append(',').Append(p.buffTime[i]).Append(']');
			}
			sb.Append(']');
			if (!first) Ev("buffs", ("b", sb.ToString()));
		}

		private static void AccessoryEvents(Player p)
		{
			bool changed = false; var cur = new int[10];
			for (int i = 0; i < 10; i++) { cur[i] = p.armor[i] != null && !p.armor[i].IsAir ? p.armor[i].type : 0; if (cur[i] != _accLast[i]) changed = true; }
			if (!changed) return;
			var sb = new StringBuilder("[");
			for (int i = 0; i < 10; i++) { if (i > 0) sb.Append(','); sb.Append(cur[i]); if (cur[i] != 0 && !_items.ContainsKey(cur[i])) _items[cur[i]] = p.armor[i].Name; }
			sb.Append(']');
			Ev("acc", ("slots", sb.ToString()));
			_accLast = cur;
		}
	}
}
