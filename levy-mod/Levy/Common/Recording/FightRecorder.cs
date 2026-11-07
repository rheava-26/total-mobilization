using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
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
		public const int SampleEvery = 2, MaxTicks = 10 * 60 * 60, GraceTicks = 90, ProjRangePx = 80 * 16, LevyRangePx = 120 * 16, MaxProj = 250;
		public const int ArenaW = 200, ArenaH = 120;

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

		public static string Path => _path;
		public static int Elapsed => (int)(Main.GameUpdateCount - (uint)_startTick);

		private static string Names(Dictionary<int, string> d)
		{
			var sb = new StringBuilder("{"); bool first = true;
			foreach (var kv in d) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":").Append(FightRecFormat.Esc(kv.Value)); }
			return sb.Append('}').ToString();
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
		public static string Start(Player cmd, string name, out string error)
		{
			error = null;
			if (On) { error = "already recording to " + _path; return null; }
			try
			{
				Directory.CreateDirectory(Dir());
				_cmd = cmd; _frames = 0; _events = 0; _bossSeen = false; _bossesSeen = 0; _bossIdsSeen.Clear(); _lastBossSeen = 0;
				_items.Clear(); _npcs.Clear(); _projs.Clear(); _buffs.Clear(); _soldiers.Clear(); _pXJump.Clear();
				_startTick = (int)Main.GameUpdateCount;
				string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
				NPC nearest = null; float bd = float.MaxValue;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || !n.boss) continue;
					float d = Vector2.DistanceSquared(n.Center, cmd.Center);
					if (d < bd) { bd = d; nearest = n; }
				}
				_boss = SafeName(!string.IsNullOrWhiteSpace(name) ? name : nearest?.FullName);
				_path = System.IO.Path.Combine(Dir(), stamp + "_" + _boss + ".jsonl");
				_w = new StreamWriter(_path, false, new UTF8Encoding(false), 1 << 16);
				string mods = ModList(out string mode);
				string ver = Levy.Instance?.Version?.ToString() ?? "?";
				_w.WriteLine("{\"k\":\"hdr\",\"format\":" + FightRecFormat.Version + ",\"levy\":" + FightRecFormat.Esc(ver) + ",\"mods\":" + mods + ",\"mode\":" + FightRecFormat.Esc(mode)
					+ ",\"boss\":" + FightRecFormat.Esc(_boss) + ",\"player\":" + FightRecFormat.Esc(cmd.name) + ",\"startTick\":" + _startTick + ",\"hz\":" + (60 / SampleEvery)
					+ ",\"world\":" + FightRecFormat.Esc(Main.worldName) + ",\"date\":" + FightRecFormat.Esc(DateTime.Now.ToString("s"))
					+ ",\"stateNames\":" + FightRecFormat.Esc(string.Join(",", Enum.GetNames(typeof(SoldierState))))
					+ ",\"schemaP\":" + FightRecFormat.Esc(FightRecFormat.SchemaP) + ",\"flagsP\":" + FightRecFormat.Esc(FightRecFormat.SchemaPFlags)
					+ ",\"schemaB\":" + FightRecFormat.Esc(FightRecFormat.SchemaB) + ",\"schemaJ\":" + FightRecFormat.Esc(FightRecFormat.SchemaJ)
					+ ",\"schemaL\":" + FightRecFormat.Esc(FightRecFormat.SchemaL) + ",\"flagsL\":" + FightRecFormat.Esc(FightRecFormat.SchemaLFlags) + "}");
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
			string final = _path;
			try
			{
				_w.WriteLine("{\"k\":\"names\",\"items\":" + Names(_items) + ",\"npcs\":" + Names(_npcs) + ",\"projectiles\":" + Names(_projs) + ",\"buffs\":" + Names(_buffs) + ",\"soldiers\":" + Names(_soldiers) + "}");
				_w.WriteLine("{\"k\":\"end\",\"reason\":" + FightRecFormat.Esc(reason) + ",\"ticks\":" + Elapsed + ",\"frames\":" + _frames + ",\"events\":" + _events + "}");
				_w.Dispose(); _w = null;
				// the boss may have appeared after the start: rename once to carry its name
				if (_boss == "none" && _bossNameSeen != null)
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
			int x0 = Math.Max(0, Math.Min(Main.maxTilesX - ArenaW, cx - ArenaW / 2)), y0 = Math.Max(0, Math.Min(Main.maxTilesY - ArenaH, cy - ArenaH / 2));
			int w = Math.Min(ArenaW, Main.maxTilesX), h = Math.Min(ArenaH, Main.maxTilesY);
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
		}

		// ------------------------------------------------------------------------------------------ sampling
		private static bool Grounded(Entity e) => Math.Abs(e.velocity.Y) < 0.01f;

		/// <summary>One call per game tick while recording. Samples on every SampleEvery-th tick.</summary>
		public static void Tick()
		{
			if (!On) return;
			try
			{
				if (_cmd == null || (!_cmd.active && !IgnoreInactive)) { Stop("player gone"); return; }
				int el = Elapsed;
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
			if (el % 60 == 0) { WriteBuffs(p, el); }
			AccessoryEvents(p);

			// bosses and parts
			bool anyBoss = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC) continue;
				if (!IsBossOrPart(n)) continue;
				if (n.boss) anyBoss = true;
				bool part = !n.boss;
				if (!_npcs.ContainsKey(n.type)) _npcs[n.type] = n.FullName;
				if (n.boss && _bossIdsSeen.Add(n.whoAmI)) { _bossesSeen++; Ev("boss", ("npc", n.type.ToString()), ("name", FightRecFormat.Esc(n.FullName)), ("id", n.whoAmI.ToString())); if (_bossNameSeen == null) _bossNameSeen = n.FullName; }
				f.B.Add(new double[] { n.type, n.whoAmI, n.Center.X, n.Center.Y, n.velocity.X, n.velocity.Y, n.life, n.lifeMax, n.ai[0], n.ai[1], n.ai[2], n.ai[3], n.target, part ? 1 : 0 });
			}
			if (anyBoss) { _bossSeen = true; _lastBossSeen = el; }
			else if (_bossSeen && el - _lastBossSeen > GraceTicks) { Stop("boss gone"); return; }

			// hostile projectiles near the player, nearest first when there are too many
			int pc = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile pr = Main.projectile[i];
				if (!pr.active || !pr.hostile) continue;
				if (Math.Abs(pr.Center.X - p.Center.X) > ProjRangePx || Math.Abs(pr.Center.Y - p.Center.Y) > ProjRangePx) continue;
				if (!_projs.ContainsKey(pr.type)) _projs[pr.type] = Lang.GetProjectileName(pr.type).Value;
				if (pc++ >= MaxProj) break;
				f.J.Add(new double[] { pr.type, pr.Center.X, pr.Center.Y, pr.velocity.X, pr.velocity.Y, pr.damage });
			}

			// levies
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.ModNPC is SoldierNPC s)) continue;
				if (Math.Abs(n.Center.X - p.Center.X) > LevyRangePx || Math.Abs(n.Center.Y - p.Center.Y) > LevyRangePx) continue;
				int id = s.Record != null ? s.Record.Id : -n.whoAmI;
				if (s.Record != null && !_soldiers.ContainsKey(id)) _soldiers[id] = s.Record.Name;
				Item lh = s.Dummy?.HeldItem;
				int lflags = (Grounded(n) ? 1 : 0) | (s.IsFlying ? 2 : 0) | (s.IsAerial ? 4 : 0) | (s.IsHooking ? 8 : 0) | (s.MoveBlocked ? 16 : 0);
				if (lh != null && !lh.IsAir && !_items.ContainsKey(lh.type)) _items[lh.type] = lh.Name;
				f.L.Add(new double[] { id, n.Center.X, n.Center.Y, n.velocity.X, n.velocity.Y, lflags, n.life, (int)s.State, lh != null && !lh.IsAir ? lh.type : 0, s.WingTime });
			}
			Line(FightRecFormat.Frame(f));
			_frames++;
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
