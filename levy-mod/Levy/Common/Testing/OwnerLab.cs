using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// OWNER LAB (feasibility study; harness-only). Active only when LEVY_HARNESS and LEVY_HARNESS_LAB are both set; changes no normal gameplay path.
	///
	/// QUESTION: can a Levy soldier be the REAL owner of the projectiles its weapon makes, so the weapon's own vanilla / modded code (minions, spears, yoyos, boomerangs, heal staves) runs unmodified?
	///
	/// PROTOTYPE A (default): "owner 255 + per-projectile swap". Generalises SoldierMinions / HeldWeapons / MiscProjSwap:
	///   USE   : the soldier's Dummy is put into Main.player[255] (EnemyTargeting.Swap), Main.myPlayer is faked to 255, the cursor is aimed at the target, and VANILLA Player.ItemCheck runs on the Dummy
	///           (the real item use: CanUseItem / ModifyShootStats / Shoot / UseItem / buffs / mana). Whatever it spawns is owned by 255 and tagged (LabTag) with the soldier.
	///   LIVE  : every Projectile.Update of a tagged projectile (On_Projectile.Update detour) is wrapped in the same swap + faked myPlayer + the Dummy's attack target. Children spawned inside inherit the tag.
	///   COUNTS: once a tick the Dummy's numMinions / slotsMinions / ownedProjectileCounts are zeroed and recounted from ITS projectiles only; its buffs are run (Player.UpdateBuffs) so ModBuff.Update sets ModPlayer minion flags.
	///   STRIKE: NPC.StrikeNPC inside a tagged projectile's Update is run with the swap lifted (real null player in 255, real Main.myPlayer) so a boss-death cascade sees the real machine state (flag nolift disables this).
	/// PROTOTYPE B (flag slots): every soldier gets a persistent real player slot 100+k (its Dummy lives in Main.player[slot]; owner = slot; no array swap; myPlayer faked to the slot).
	///
	/// SPEC (LEVY_HARNESS_LAB): ';'-separated scenarios  kind|item|N|ticks|flags   kind: fid (fidelity) | cost | casc (kill a boss with it) | ctl (N soldiers, no item: cost control)
	///   item: V:Name (ItemID name) | T:Name (ThoriumMod item) | M:Mod/Name   flags (comma): fam=summon|spear|yoyo|flail|boom|whip|heal|other  dist=tiles  nolift  slots  spsim  nobuffs  keepalive  boss=npcId  hp=N
	/// OUT: LEVY_HARNESS_OUT (TSV).
	/// </summary>
	public static class OwnerLab
	{
		internal static bool Active;
		internal static string Spec = "";
		internal const int Slot = SoldierNPC.DummySlot;

		internal static void ReadEnv()
		{
			string v = Environment.GetEnvironmentVariable("LEVY_HARNESS_LAB");
			Active = !string.IsNullOrEmpty(v) && v != "0";
			Spec = v ?? "";
		}

		// ---------------------------------------------------------------------------------------------
		// scenario model
		// ---------------------------------------------------------------------------------------------
		private sealed class Sol
		{
			public SoldierRecord Rec; public int NpcIdx = -1; public SoldierNPC Body; public Item Weapon; public int SlotIdx = Slot;
			public int Uses, UseExc, Pulse; public float PinX;
			public int Cast; public Vector2 Aim; public bool HasAim; public Player Bk;
		}

		private sealed class Sc
		{
			public string Kind = "fid", ItemKey = "", Fam = "other"; public int N = 1, Ticks = 600; public HashSet<string> Flags = new();
			public float Dist = 12f, CmdDist = -1f; public int BossId = 0, Hp = 0; public int ItemType = -1;
			public List<Sol> Sols = new(); public int TargetIdx = -1; public bool Done;
			public int Tick; public string Note = "";
			public Dictionary<string, int> Proj = new(), Child = new(); public int Spawned, MaxLive;
			public Dictionary<string, int> Items = new();
			public List<string> Exc = new();
			public long Dmg, DmgA, DmgB; public int HitTicks;
			public double SumDistSol, SumDistCmd; public int DistSamples; public double SumDistTgt; public int DistTgtSamples; public int Alive0, AliveEnd;
			public int MinionsEnd; public string DummyEnd = "", CmdDiff = "", BuffsEnd = "";
			public long UseTs, UseOverheadTs, ProjOrigTs, ProjOverheadTs, PrepTs, WholeTs; public int ProjUpdates, UseCalls, PrepCalls, WholeSamples;
			public int Kills, OrphanKills; public bool BossDead; public int BossDeadTick = -1; public string Fp0 = "", Fp1 = "", Tp0 = "", Tp1 = "";
			public int CmdLifeMin = int.MaxValue, CmdManaMin = int.MaxValue, CmdLifeGain;
			public int CmdPrevLife = -1;
			public int OwnerNon255; public Dictionary<int, int> OwnerHist = new();
			public int HealTicks, PlayInstrNre, DummyDeaths, TpToCmd, TpToSelf, TpOther; public int ChildAtSol, ChildAtCmd, ChildAtTgt, ChildOther, CmdProcs, OtherProcs, DummyProcs;
			public long StopTsAtStart;
		}

		private static Sc _sc;
		private static readonly List<Sc> _done = new();
		private static List<Sc> _queue = new();
		private static int _qi;
		private static int _stage; // 0 setup 1 run 2 cleanup
		private static int _stageTick;
		private static CombatHarness _h;
		private static bool _bootDone;
		private static readonly List<string> _rows = new();
		private static long _lastTickTs;

		internal static void NoteDummyDeath() { if (_sc != null) _sc.DummyDeaths++; }
		internal static bool Flag(string f) => _sc != null && _sc.Flags.Contains(f);
		internal static bool Legacy => _sc != null && _sc.Flags.Contains("legacy");
		internal static void NoteHitProc(int who) { var s = _sc; if (s == null || _stage != 1) return; if (who == 0) s.CmdProcs++; else s.OtherProcs++; }
		internal static bool InScenario => _sc != null && _stage == 1;

		// ---------------------------------------------------------------------------------------------
		// boot / driver (called by CombatHarness in Ph.Lab)
		// ---------------------------------------------------------------------------------------------
		internal static void Boot(CombatHarness h)
		{
			_h = h; _queue = Parse(Spec); _qi = 0; _stage = 0; _stageTick = 0; _bootDone = true; _sc = null;
			h.Mod.Logger.Info($"LAB: {_queue.Count} scenarios queued");
			StartWatchdog();
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
				if (p.Length > 3 && int.TryParse(p[3], out int t)) s.Ticks = Math.Clamp(t, 60, 6000);
				float explicitDist = -1f;
				if (p.Length > 4) foreach (var f in p[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					if (f.StartsWith("fam=")) s.Fam = f.Substring(4);
					else if (f.StartsWith("dist=") && float.TryParse(f.Substring(5), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d)) explicitDist = d;
					else if (f.StartsWith("cmd=") && float.TryParse(f.Substring(4), out float cd)) s.CmdDist = cd;
					else if (f.StartsWith("boss=") && int.TryParse(f.Substring(5), out int b)) s.BossId = b;
					else if (f.StartsWith("hp=") && int.TryParse(f.Substring(3), out int hp)) s.Hp = hp;
					else s.Flags.Add(f);
				}
				s.Dist = explicitDist > 0 ? explicitDist : DefaultDist(s.Fam);
				l.Add(s);
			}
			return l;
		}

		private static float CmdDistOf(Sc s) => s.CmdDist > 0 ? s.CmdDist : (s.Fam == "heal" ? 8f : 60f);
		private static float DefaultDist(string fam) => fam switch { "summon" => 12f, "spear" => 3.5f, "whip" => 5f, "yoyo" => 5f, "flail" => 5f, "boom" => 9f, "heal" => 8f, _ => 8f };

		private static int ResolveItem(string key)
		{
			try
			{
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

		internal static void Tick(CombatHarness h)
		{
			_h = h;
			long now = Stopwatch.GetTimestamp();
			if (_sc != null && _stage == 1 && _lastTickTs != 0) { _sc.WholeTs += now - _lastTickTs; _sc.WholeSamples++; }
			_lastTickTs = now;
			Beat();
			if (_sc == null)
			{
				if (_qi >= _queue.Count) { Finish(); return; }
				_sc = _queue[_qi];
				_stage = 0; _stageTick = 0;
			}
			try
			{
				switch (_stage)
				{
					case 0:
						if (_stageTick == 0) Setup(_sc);
						KeepSite(_sc);
						if (++_stageTick >= 30) { _stage = 1; _stageTick = 0; _lastTickTs = 0; BeginRun(_sc); }
						break;
					case 1:
						KeepSite(_sc);
						RunTick(_sc);
						if (++_sc.Tick >= _sc.Ticks || _sc.Done) { EndRun(_sc); _stage = 2; _stageTick = 0; }
						break;
					case 2:
						if (_stageTick == 0) Teardown(_sc);
						if (++_stageTick >= 8)
						{
							_done.Add(_sc); _rows.Add(Row(_sc)); WriteTsv();
							_sc = null; _qi++;
						}
						break;
				}
			}
			catch (Exception e)
			{
				_h.Mod.Logger.Warn("LAB: driver exception: " + e);
				if (_sc != null) { _sc.Exc.Add("driver:" + e.GetType().Name + ":" + e.Message); try { Teardown(_sc); } catch { } _done.Add(_sc); _rows.Add(Row(_sc)); WriteTsv(); _sc = null; _qi++; _stage = 0; }
			}
		}

		private static void Finish()
		{
			WriteTsv();
			_h.LabFinish($"lab complete, {_rows.Count} rows");
		}

		// ---------------------------------------------------------------------------------------------
		// watchdog: a hang inside a tick (the very hazard under study) is reported and ends the run
		// ---------------------------------------------------------------------------------------------
		private static long _beat; private static volatile string _where = "boot"; private static System.Threading.Thread _wd;
		private static void Beat() { System.Threading.Volatile.Write(ref _beat, Stopwatch.GetTimestamp()); _where = _sc == null ? "idle" : $"{_sc.Kind}|{_sc.ItemKey}|N{_sc.N} stage{_stage} tick{_sc.Tick}"; }
		private static void StartWatchdog()
		{
			if (_wd != null) return;
			Beat();
			_wd = new System.Threading.Thread(() =>
			{
				while (Active)
				{
					System.Threading.Thread.Sleep(1000);
					double idle = (Stopwatch.GetTimestamp() - System.Threading.Volatile.Read(ref _beat)) / (double)Stopwatch.Frequency;
					if (idle > 25)
					{
						try
						{
							File.AppendAllText(CombatHarness.OutPath + ".HANG", $"HANG: no lab tick for {idle:0}s while in [{_where}] proj={HangWatch.ProjType} slot={HangWatch.ProjSlot} npc={HangWatch.NpcType}\n");
							File.WriteAllText(CombatHarness.OutPath + ".partial", string.Join("\n", _rows));
						}
						catch { }
						Environment.Exit(7);
					}
				}
			}) { IsBackground = true, Name = "Lab watchdog" };
			_wd.Start();
		}

		// ---------------------------------------------------------------------------------------------
		// setup / teardown
		// ---------------------------------------------------------------------------------------------
		private static bool Has(Sc s, string f) => s.Flags.Contains(f);
		private static int CxPx => _h.Cx * 16;

		private static void Setup(Sc s)
		{
			_h.LabClearAll();
			s.ItemType = s.Kind == "ctl" ? 0 : ResolveItem(s.ItemKey);
			if (s.Kind != "ctl" && s.ItemType <= 0) { s.Note = "ITEM NOT FOUND"; s.Done = true; s.Ticks = 0; }
			s.Sols.Clear();
			bool slots = Has(s, "slots");
			for (int k = 0; k < s.N; k++)
			{
				var rec = _h.LabMakeRecord("Pvt. Lab" + k);
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold; rec.HoldFire = true;
				rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec);
				float x = CxPx + (k - s.N / 2) * 24f; // 1.5 tiles apart
				int idx = RosterSystem.Instance.SpawnBody(rec, new Vector2(x, CombatHarness.FloorY * 16f));
				var sol = new Sol { Rec = rec, NpcIdx = idx, PinX = x };
				if (idx >= 0) { sol.Body = Main.npc[idx].ModNPC as SoldierNPC; if (sol.Body != null) sol.Body.NPC.Bottom = new Vector2(x, CombatHarness.FloorY * 16f); }
				if (s.ItemType > 0) { sol.Weapon = new Item(); sol.Weapon.SetDefaults(s.ItemType); }
				sol.SlotIdx = (slots || Has(s, "vslot")) ? 100 + k : Slot;
				s.Sols.Add(sol);
			}
			// commander
			Player c = Main.player[0];
			bool heal = s.Fam == "heal";
			c.active = true; c.dead = false; c.name = "LevyTester"; c.statLifeMax = c.statLifeMax2 = 500; c.statLife = heal ? 250 : 500;
			c.position = new Vector2(CxPx - CmdDistOf(s) * 16f, CombatHarness.FloorY * 16f - c.height); c.velocity = Vector2.Zero;
			for (int b = 0; b < c.buffType.Length; b++) { c.buffType[b] = 0; c.buffTime[b] = 0; }
			Array.Clear(c.ownedProjectileCounts, 0, c.ownedProjectileCounts.Length);
			// target
			SpawnTarget(s, s.Fam == "summon" ? 90f : s.Dist);
			s.Fp0 = Fp(c); s.Tp0 = TpSnap(c);
		}

		private static void SpawnTarget(Sc s, float distTiles)
		{
			int type = s.BossId > 0 ? s.BossId : NPCID.GreenSlime;
			float cx = CxPx + distTiles * 16f;
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyLab"), (int)cx, (int)(CombatHarness.FloorY * 16f), type);
			if (i < 0 || i >= Main.maxNPCs) throw new Exception("target NewNPC failed");
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>();
			ht.IsTarget = true;
			int hp = s.Hp > 0 ? s.Hp : 1_000_000;
			n.lifeMax = hp; n.life = hp; n.defense = 0; n.damage = 0; n.knockBackResist = 0f;
			n.width = 64; n.height = 96;
			n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.dontTakeDamage = false; n.value = 0f;
			n.velocity = Vector2.Zero;
			n.Bottom = new Vector2(cx, CombatHarness.FloorY * 16f);
			ht.Pin = n.position;
			s.TargetIdx = i;
		}

		private static void MoveTarget(Sc s, float distTiles)
		{
			if (s.TargetIdx < 0) return;
			NPC n = Main.npc[s.TargetIdx];
			if (!n.active) return;
			n.Bottom = new Vector2(CxPx + distTiles * 16f, CombatHarness.FloorY * 16f);
			n.GetGlobalNPC<HarnessTarget>().Pin = n.position;
		}

		private static void Teardown(Sc s)
		{
			RestoreSlots(s);
			_h.LabClearAll();
			try { Main.player[1].active = false; } catch { }
			if (s.Flags.Contains("gm")) Main.gameMenu = _savedGameMenu;
			if (s.Flags.Contains("spsim") && _spsimOn) { Main.myPlayer = _savedMyForSim; _spsimOn = false; }
		}

		private static bool _spsimOn, _savedGameMenu; private static int _savedMyForSim;

		private static void KeepSite(Sc s)
		{
			Player p = Main.player[0];
			if (p.statLife < CmdLifeMinSeen(s)) { }
			p.active = true; p.dead = false; p.name = "LevyTester";
			p.statLifeMax = 500; p.statLifeMax2 = 500; p.statManaMax = 200; p.statManaMax2 = 200;
			bool heal = s.Fam == "heal";
			p.position = new Vector2(CxPx - CmdDistOf(s) * 16f, CombatHarness.FloorY * 16f - p.height);
			p.velocity = Vector2.Zero;
			if (p.statLife < 1) p.statLife = 1;
			if (!heal && p.statLife < 500 && _stage != 0) p.statLife = 500;
			if (p.statMana < 200) { /* sampled in RunTick before the refill below */ }
			if (_stage == 0) p.statMana = 200;
			// pin soldiers
			foreach (var sol in s.Sols)
			{
				if (sol.Body == null) continue;
				NPC n = sol.Body.NPC;
				if (!n.active) continue;
				n.position.X = sol.PinX - n.width / 2f; n.velocity = new Vector2(0f, n.velocity.Y);
				n.Bottom = new Vector2(sol.PinX, CombatHarness.FloorY * 16f);
				sol.Body.Record.CurrentHp = sol.Body.Record.MaxHp; n.life = n.lifeMax;
			}
			// sweep stray hostiles
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				if (!n.friendly || n.type == NPCID.TargetDummy) n.active = false;
			}
		}
		private static int CmdLifeMinSeen(Sc s) => 0;

		// ---------------------------------------------------------------------------------------------
		// the run
		// ---------------------------------------------------------------------------------------------
		private static void BeginRun(Sc s)
		{
			s.Alive0 = s.Sols.Count(x => x.Body != null && x.Body.NPC.active && x.Body.IsAlive);
			if (s.Flags.Contains("gm")) { _savedGameMenu = Main.gameMenu; Main.gameMenu = false; } // a dedicated server has gameMenu = true, which turns Thorium's local-player cache off; SP / client have it false
			if (s.Flags.Contains("spsim") && !_spsimOn) { _savedMyForSim = Main.myPlayer; Main.myPlayer = 0; _spsimOn = true; }
			if (s.Flags.Contains("slots")) PlaceSlots(s);
			s.CmdPrevLife = Main.player[0].statLife;
			_prepStamp = uint.MaxValue;
		}

		private static void EndRun(Sc s)
		{
			s.AliveEnd = s.Sols.Count(x => x.Body != null && x.Body.NPC.active && x.Body.IsAlive);
			// end state of the first dummy
			var d0 = s.Sols.FirstOrDefault(x => x.Body?.Dummy != null)?.Body?.Dummy;
			int live = 0; for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; if (p.active && p.TryGetGlobalProjectile(out LabTag t) && t.Soldier >= 0 && p.minion) live++; }
			s.MinionsEnd = live;
			if (d0 != null)
			{
				var sb = new StringBuilder($"num{d0.numMinions} slots{d0.slotsMinions:0.##} max{d0.maxMinions} owned[");
				for (int i = 0; i < d0.ownedProjectileCounts.Length; i++) if (d0.ownedProjectileCounts[i] > 0) sb.Append(TypeName(i)).Append('x').Append(d0.ownedProjectileCounts[i]).Append(' ');
				sb.Append("] arrLen").Append(d0.ownedProjectileCounts.Length);
				s.DummyEnd = sb.ToString();
				s.BuffsEnd = string.Join(",", Enumerable.Range(0, d0.buffType.Length).Where(b => d0.buffType[b] > 0).Select(b => (BuffLoader.GetBuff(d0.buffType[b])?.Name ?? ("buff" + d0.buffType[b])) + ":" + d0.buffTime[b]));
			}
			s.Fp1 = Fp(Main.player[0]); s.Tp1 = TpSnap(Main.player[0]);
			s.CmdDiff = (s.Fp0 == s.Fp1 ? "none" : "BEFORE " + s.Fp0 + " || AFTER " + s.Fp1) + (s.Tp0 == s.Tp1 ? "" : " || THORIUMPLAYER(commander) BEFORE " + s.Tp0 + " AFTER " + s.Tp1);
		}

		private static string TypeName(int type)
		{
			var mp = ProjectileLoader.GetProjectile(type);
			if (mp != null) return mp.Mod.Name + "/" + mp.Name;
			return ProjectileID.Search.TryGetName(type, out string n) ? n : "v" + type;
		}

		private static string Fp(Player p)
		{
			var sb = new StringBuilder();
			sb.Append($"num{p.numMinions}/{p.slotsMinions:0.##}/max{p.maxMinions} sel{p.selectedItem} anim{p.itemAnimation}/{p.itemTime} ch{p.channel} use{p.controlUseItem} held{p.heldProj} alt{p.altFunctionUse} owned[");
			for (int i = 0; i < p.ownedProjectileCounts.Length; i++) if (p.ownedProjectileCounts[i] > 0) sb.Append(TypeName(i)).Append('x').Append(p.ownedProjectileCounts[i]).Append(' ');
			sb.Append("] buffs[");
			for (int b = 0; b < p.buffType.Length; b++) if (p.buffType[b] > 0) sb.Append(BuffLoader.GetBuff(p.buffType[b])?.Name ?? ("b" + p.buffType[b])).Append(' ');
			sb.Append("] inv");
			int h = 17; for (int i = 0; i < p.inventory.Length; i++) h = h * 31 + p.inventory[i].type * 7 + p.inventory[i].stack;
			sb.Append(h);
			return sb.ToString();
		}

		private static bool IsAimAtCmd(Sc s) => s.Fam == "heal";

		private static void RunTick(Sc s)
		{
			// two-phase summon: idle follow (target far away), then the target is brought in
			if (s.Fam == "summon" && s.Tick == s.Ticks / 2) MoveTarget(s, s.Dist);
			bool phaseB = s.Fam != "summon" || s.Tick >= s.Ticks / 2;
			if (Has(s, "gm")) TpSetLocal(Main.player[0]); // emulates the commander's ThoriumPlayer hook that registers the local-player cache each tick in SP (it throws on this headless server before reaching it)

			// pre-use sampling of the real commander (before the harness refills it): did anything spend its mana / life?
			Player cmd = Main.player[0];
			if (cmd.statMana < s.CmdManaMin) s.CmdManaMin = cmd.statMana;
			if (cmd.statLife < s.CmdLifeMin) s.CmdLifeMin = cmd.statLife;
			if (s.CmdPrevLife >= 0 && cmd.statLife > s.CmdPrevLife) s.CmdLifeGain += cmd.statLife - s.CmdPrevLife;
			s.CmdPrevLife = cmd.statLife;
			if (!(s.Fam == "heal")) cmd.statMana = cmd.statManaMax2;

			// target damage
			if (s.TargetIdx >= 0)
			{
				NPC t = Main.npc[s.TargetIdx];
				if (t.active && t.life < t.lifeMax && s.BossId == 0) { int d = t.lifeMax - t.life; s.Dmg += d; if (s.Fam == "summon" && !phaseB) s.DmgA += d; else s.DmgB += d; s.HitTicks++; t.life = t.lifeMax; }
				else if (!t.active && s.BossId > 0 && !s.BossDead) { s.BossDead = true; s.BossDeadTick = s.Tick; s.Done = true; }
			}

			// drive soldiers
			if (s.Kind != "ctl" && s.ItemType > 0)
			{
				foreach (var sol in s.Sols)
				{
					if (sol.Body == null || !sol.Body.NPC.active || !sol.Body.IsAlive) continue;
					try { UseOnce(s, sol, phaseB); }
					catch (Exception e) { sol.UseExc++; if (s.Exc.Count < 8) s.Exc.Add("use:" + Short(e)); }
				}
			}

			// sampling of lab projectiles
			int live = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || !p.TryGetGlobalProjectile(out LabTag t) || t.Soldier < 0) continue;
				live++;
				s.OwnerHist[p.owner] = s.OwnerHist.TryGetValue(p.owner, out int c) ? c + 1 : 1;
				if (p.minion && s.Fam == "summon")
				{
					NPC sb = Main.npc[t.Soldier];
					if (sb.active)
					{
						float dS = Vector2.Distance(p.Center, sb.Center) / 16f, dC = Vector2.Distance(p.Center, cmd.Center) / 16f;
						if (!phaseB && s.Tick > s.Ticks / 4) { s.SumDistSol += dS; s.SumDistCmd += dC; s.DistSamples++; }
						if (phaseB && s.TargetIdx >= 0 && Main.npc[s.TargetIdx].active) { s.SumDistTgt += Vector2.Distance(p.Center, Main.npc[s.TargetIdx].Center) / 16f; s.DistTgtSamples++; }
					}
				}
			}
			if (live > s.MaxLive) s.MaxLive = live;
		}

		private static string Short(Exception e)
		{
			if (e is TargetInvocationException t && t.InnerException != null) e = t.InnerException;
			string st = e.StackTrace ?? "";
			return e.GetType().Name + ":" + e.Message + " @ " + string.Join(" < ", st.Split('\n').Take(3).Select(x => x.Trim()));
		}

		// ---------------------------------------------------------------------------------------------
		// the swap primitives
		// ---------------------------------------------------------------------------------------------
		internal static SoldierNPC FrameBody; internal static int FrameSlot = -1; internal static Sc_Info Pending;
		internal sealed class Sc_Info { public SoldierNPC Body; }
		private static bool _lifted;
		private static int _trueMy;
		private static int _targetIdxForFrame = -1;

		/// <summary>Puts the soldier's Dummy where its owner reads it. Returns the saved Main.myPlayer.</summary>
		private static int Enter(Sc s, Sol sol)
		{
			int saved = Main.myPlayer;
			if (Has(s, "vslot"))
			{
				// VIRTUAL PER-SOLDIER OWNER: the soldier owns slot 100+k for good (projectile.owner = that slot), but the dummy is only swapped INTO the slot for the length of a frame
				Player d = sol.Body.Dummy;
				sol.Body.SyncDummyTransform();
				d.whoAmI = sol.SlotIdx;
				sol.Bk = Main.player[sol.SlotIdx];
				Main.player[sol.SlotIdx] = d;
				Main.myPlayer = sol.SlotIdx;
			}
			else if (Has(s, "slots"))
			{
				Player d = sol.Body.Dummy;
				sol.Body.SyncDummyTransform(); // this resets whoAmI = 255 (production behaviour); re-assert the persistent slot below
				d.whoAmI = sol.SlotIdx;
				Main.player[sol.SlotIdx] = d;
				Main.myPlayer = sol.SlotIdx;
			}
			else
			{
				EnemyTargeting.Swap(sol.Body);
				Main.myPlayer = Slot;
			}
			if (Has(s, "tpcache")) TpSetLocal(sol.Body.Dummy);
			return saved;
		}

		private static void Leave(Sc s, Sol sol, int savedMy)
		{
			Main.myPlayer = savedMy;
			if (Has(s, "vslot")) { Main.player[sol.SlotIdx] = sol.Bk ?? new Player(); sol.Bk = null; sol.Body.Dummy.whoAmI = Slot; }
			else if (!Has(s, "slots")) { EnemyTargeting.Unswap(); EnemyTargeting.EnsureSlotRestored(); }
			if (Has(s, "tpcache")) TpSetLocal(Main.player[Main.myPlayer]); // put the real local player's cached ThoriumPlayer back
		}

		private static void TpSetLocal(Player p) { try { if (ModLoader.HasMod("ThoriumMod")) LabThorium.SetLocal(p); } catch (Exception) { } }
		private static string TpSnap(Player p) { try { return ModLoader.HasMod("ThoriumMod") ? LabThorium.Snap(p) : "-"; } catch (Exception) { return "?"; } }

		private static MethodInfo _itemCheck;
		private static void UseOnce(Sc s, Sol sol, bool phaseB)
		{
			long t0 = Stopwatch.GetTimestamp();
			Player d = sol.Body.Dummy;
			Item w = sol.Weapon;
			if (_itemCheck == null) _itemCheck = typeof(Player).GetMethod("ItemCheck", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			// aim
			Vector2 aim;
			if (IsAimAtCmd(s)) aim = Main.player[0].Center;
			else if (s.TargetIdx >= 0 && Main.npc[s.TargetIdx].active) aim = Main.npc[s.TargetIdx].Center;
			else aim = sol.Body.NPC.Center + new Vector2(200f, 0f);
			// when to press
			bool hold = w.autoReuse || w.channel;
			bool press;
			if (s.Fam == "summon")
			{
				int owned = CountOwned(sol);
				int want = Math.Max(1, (int)Math.Floor(Cap(s) / Math.Max(0.5f, Slots(w)) + 0.001f));
				press = owned < want && (s.Tick % 30 < 3);
			}
			else press = phaseB && (hold ? (s.Tick % 45 != 0) : (s.Tick % 2 == 0));

			sol.Aim = aim; sol.HasAim = true;
			int savedMy = Enter(s, sol);
			long t1 = Stopwatch.GetTimestamp();
			if (ModLoader.HasMod("ThoriumMod")) { try { int rr = LabThorium.Resolve(d, Main.player[0]); if (rr == 1) s.TpToCmd++; else if (rr == 2) s.TpToSelf++; else s.TpOther++; } catch (Exception) { } }
			int savedMax = d.maxMinions, savedSel = d.selectedItem, savedMana = d.statMana, savedManaMax = d.statManaMax2;
			Item saved0 = d.inventory[0];
			PendingBodySet(sol.Body);
			long icTs = 0;
			try
			{
				d.noItems = false; d.selectedItem = 0; d.inventory[0] = w; d.dead = false;
				d.maxMinions = Math.Max(d.maxMinions, Cap(s));
				d.statManaMax2 = 400; d.statMana = 400;
				if (w.useAmmo > 0) { int at = AmmoFor(w); if (at > 0 && (d.inventory[54].IsAir || d.inventory[54].type != at)) { d.inventory[54] = new Item(); d.inventory[54].SetDefaults(at); d.inventory[54].stack = 999; } }
				Main.screenPosition = Vector2.Zero;
				Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
				d.direction = aim.X >= d.Center.X ? 1 : -1;
				d.controlUseItem = press; d.controlUseTile = false; d.channel = press && w.channel;
				if (!d.controlUseItem) d.releaseUseItem = true;
				Main.mouseLeft = press; Main.mouseLeftRelease = !press;
				try { Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = press; } catch { }
				long c0 = Stopwatch.GetTimestamp();
				try { _itemCheck.Invoke(d, null); }
				catch (TargetInvocationException tie) when ((tie.InnerException?.StackTrace ?? "").Contains("ItemCheck_PlayInstruments") || (tie.InnerException?.StackTrace ?? "").Contains("AchievementsHelper")) { s.PlayInstrNre++; } // server has no audio engine; vanilla's own baseline hits it too
				finally { icTs = Stopwatch.GetTimestamp() - c0; s.UseTs += icTs; }
				if (press) sol.Uses++;
			}
			finally
			{
				d.maxMinions = savedMax; d.statManaMax2 = savedManaMax; d.statMana = savedMana;
				d.inventory[0] = saved0; d.selectedItem = savedSel;
				PendingBodySet(null);
				Leave(s, sol, savedMy);
			}
			long t2 = Stopwatch.GetTimestamp();
			s.UseCalls++;
			s.UseOverheadTs += (t2 - t0) - icTs;
		}
		private static long _useTsMark;

		private static int AmmoFor(Item w)
		{
			int want = w.useAmmo;
			if (want == AmmoID.Bullet) return ItemID.MusketBall;
			if (want == AmmoID.Arrow) return ItemID.WoodenArrow;
			if (want == AmmoID.Rocket) return ItemID.RocketI;
			if (want == AmmoID.Dart) return ItemID.PoisonDart;
			if (want == AmmoID.Gel) return ItemID.Gel;
			return 0;
		}

		private static int Cap(Sc s) => s.Fam == "summon" ? 3 : 1;
		private static float Slots(Item w)
		{
			float sl = 0f;
			if (w.type > 0 && w.type < ItemID.Sets.StaffMinionSlotsRequired.Length) sl = ItemID.Sets.StaffMinionSlotsRequired[w.type];
			if (sl <= 0f) { var smp = ContentSamples.ProjectilesByType.TryGetValue(w.shoot, out var pr) ? pr : null; sl = smp != null && smp.minionSlots > 0 ? smp.minionSlots : 1f; }
			return sl;
		}

		private static int CountOwned(Sol sol)
		{
			int n = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.minion && p.TryGetGlobalProjectile(out LabTag t) && t.Soldier == sol.NpcIdx) n++;
			}
			return n;
		}

		private static void PendingBodySet(SoldierNPC b) { Pending = b == null ? null : new Sc_Info { Body = b }; }

		// ---------------------------------------------------------------------------------------------
		// persistent slots (prototype B)
		// ---------------------------------------------------------------------------------------------
		private static readonly Dictionary<int, Player> _slotBackup = new();
		private static volatile bool _slotKeep;
		private static System.Threading.Thread _slotThread;
		private static int[] _keepSlots = Array.Empty<int>();

		private static void PlaceSlots(Sc s)
		{
			foreach (var sol in s.Sols)
			{
				if (sol.Body == null) continue;
				_slotBackup[sol.SlotIdx] = Main.player[sol.SlotIdx];
				sol.Body.Dummy.whoAmI = sol.SlotIdx;
				Main.player[sol.SlotIdx] = sol.Body.Dummy;
				sol.Body.Dummy.active = true;
			}
			_keepSlots = s.Sols.Select(x => x.SlotIdx).Append(0).ToArray();
			_slotKeep = true;
			EnsureKeepThread();
		}

		private static void EnsureKeepThread()
		{
			if (_slotThread == null || !_slotThread.IsAlive)
			{
				_slotThread = new System.Threading.Thread(() => { while (Active) { if (_slotKeep) { try { foreach (int k in _keepSlots) { var pl = Main.player[k]; if (pl != null) pl.active = true; } } catch { } } System.Threading.Thread.Sleep(0); } }) { IsBackground = true, Name = "Lab slot keepalive" };
				_slotThread.Start();
			}
		}

		private static void RestoreSlots(Sc s)
		{
			_slotKeep = false;
			foreach (var kv in _slotBackup) Main.player[kv.Key] = kv.Value ?? new Player();
			_slotBackup.Clear();
			foreach (var sol in s.Sols) if (sol.Body?.Dummy != null) sol.Body.Dummy.whoAmI = Slot;
			EnemyTargeting.EnsureSlotRestored();
		}

		// ---------------------------------------------------------------------------------------------
		// per-tick preparation (PreUpdateProjectiles): the dummy's own counters, buffs, attack target
		// ---------------------------------------------------------------------------------------------
		private static uint _prepStamp;
		private static MethodInfo _updateBuffs;
		internal static void PrepareTick()
		{
			var s = _sc;
			if (s == null || _stage != 1 || s.Sols.Count == 0) return;
			long t0 = Stopwatch.GetTimestamp();
			bool buffs = !Has(s, "nobuffs");
			// count each soldier's projectiles in one pass
			int n = s.Sols.Count;
			foreach (var sol in s.Sols) { var d = sol.Body?.Dummy; if (d == null) continue; d.numMinions = 0; d.slotsMinions = 0f; Array.Clear(d.ownedProjectileCounts, 0, d.ownedProjectileCounts.Length); d.highestStormTigerGemOriginalDamage = 0; d.highestAbigailCounterOriginalDamage = 0; }
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || !p.TryGetGlobalProjectile(out LabTag t) || t.Soldier < 0) continue;
				var sol = FindSol(s, t.Soldier);
				var d = sol?.Body?.Dummy;
				if (d == null) continue;
				if (p.type < d.ownedProjectileCounts.Length) d.ownedProjectileCounts[p.type]++;
				if (p.type == ProjectileID.StormTigerGem) d.highestStormTigerGemOriginalDamage = Math.Max(d.highestStormTigerGemOriginalDamage, p.originalDamage);
			}
			foreach (var sol in s.Sols)
			{
				var d = sol.Body?.Dummy; if (d == null || !sol.Body.NPC.active) continue;
				bool phaseB = s.Fam != "summon" || s.Tick >= s.Ticks / 2;
				d.MinionAttackTargetNPC = phaseB ? s.TargetIdx : -1;
				d.miscCounter = (int)(Main.GameUpdateCount % 300u);
				if (buffs && d.buffType.Any(b => b > 0))
				{
					int savedMy = Enter(s, sol);
					try
					{
						_updateBuffs ??= typeof(Player).GetMethod("UpdateBuffs", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
						_updateBuffs.Invoke(d, new object[] { d.whoAmI });
					}
					catch (Exception e) { if (s.Exc.Count < 8) s.Exc.Add("buffs:" + Short(e)); }
					finally { Leave(s, sol, savedMy); }
				}
			}
			s.PrepTs += Stopwatch.GetTimestamp() - t0; s.PrepCalls++;
		}

		private static Sol FindSol(Sc s, int npcIdx)
		{
			// small N: linear is fine and honest (the real implementation would index by NPC whoAmI)
			for (int i = 0; i < s.Sols.Count; i++) if (s.Sols[i].NpcIdx == npcIdx) return s.Sols[i];
			return null;
		}

		// ---------------------------------------------------------------------------------------------
		// Projectile.Update wrapper
		// ---------------------------------------------------------------------------------------------
		internal static void RunUpdate(On_Projectile.orig_Update orig, Projectile p, int i, LabTag tag)
		{
			var s = _sc;
			if (s == null || _stage != 1) { orig(p, i); return; }
			Sol sol = FindSol(s, tag.Soldier);
			if (sol == null || sol.Body == null || !sol.Body.NPC.active || !sol.Body.IsAlive || sol.Body.Dummy == null)
			{
				s.OrphanKills++;
				p.Kill();
				return;
			}
			if (Has(s, "legacy"))
			{
				// today's behaviour: owner = real commander, no swap; the frame only exists so child spawns are attributed
				SoldierNPC of = FrameBody; FrameBody = sol.Body; Main.player[0].active = true; // server clears a client-less slot; vanilla kills a projectile whose owner is inactive
				try { orig(p, i); } finally { FrameBody = of; }
				return;
			}
			long t0 = Stopwatch.GetTimestamp();
			Player d = sol.Body.Dummy;
			int savedMy = Main.myPlayer, savedMax = d.maxMinions;
			SoldierNPC outerFrame = FrameBody; int outerSlot = FrameSlot;
			long t1 = t0;
			try
			{
				Enter(s, sol);
				if (Has(s, "keepalive") && p.minion && p.timeLeft < 600) p.timeLeft = 3600;
				d.maxMinions = Math.Max(d.maxMinions, Cap(s));
				FrameBody = sol.Body; FrameSlot = sol.SlotIdx; _lifted = false; _trueMy = savedMy;
				if (Has(s, "clearimm") && s.TargetIdx >= 0) Main.npc[s.TargetIdx].immune[Slot] = 0; // diagnostic: is the sublinear scaling the shared npc.immune[255]?
				if (sol.HasAim) { Main.screenPosition = Vector2.Zero; Main.mouseX = (int)sol.Aim.X; Main.mouseY = (int)sol.Aim.Y; } // the soldier's cursor, as a player's mouse is for yoyos / channelled items
				t1 = Stopwatch.GetTimestamp();
				orig(p, i);
			}
			finally
			{
				long t2 = Stopwatch.GetTimestamp();
				FrameBody = outerFrame; FrameSlot = outerSlot; _lifted = false;
				d.maxMinions = savedMax;
				Leave(s, sol, savedMy);
				long t3 = Stopwatch.GetTimestamp();
				s.ProjOverheadTs += (t1 - t0) + (t3 - t2); s.ProjOrigTs += t2 - t1; s.ProjUpdates++;
			}
		}

		/// <summary>NPC.StrikeNPC inside a tagged projectile's Update: lift the swap so the kill cascade sees the real machine state (flag nolift skips).</summary>
		internal static int RunStrike(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC npc, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{
			var s = _sc; var body = FrameBody;
			if (s == null || body == null || _lifted || Has(s, "nolift") || _stage != 1) return orig(npc, hit, fromNet, noPlayerInteraction);
			Sol sol = FindSol(s, body.NPC.whoAmI);
			if (sol == null) return orig(npc, hit, fromNet, noPlayerInteraction);
			int fakedMy = Main.myPlayer, trueMy = _trueMy;
			try
			{
				_lifted = true;
				Main.myPlayer = trueMy;
				if (Has(s, "vslot")) { Main.player[sol.SlotIdx] = sol.Bk ?? new Player(); }
				else if (Has(s, "slots")) { /* persistent slot stays: that is the point of B */ }
				else EnemyTargeting.Unswap();
				return orig(npc, hit, fromNet, noPlayerInteraction);
			}
			finally
			{
				_lifted = false;
				if (Has(s, "vslot")) { sol.Bk = Main.player[sol.SlotIdx]; Main.player[sol.SlotIdx] = body.Dummy; }
				else if (!Has(s, "slots")) EnemyTargeting.Swap(body);
				Main.myPlayer = fakedMy;
			}
		}

		internal static void NoteSpawn(Projectile p, LabTag tag)
		{
			var s = _sc; if (s == null) return;
			string nm = TypeName(p.type);
			bool child = Pending == null;
			if (child && tag.Soldier >= 0 && Main.npc[tag.Soldier].active)
			{
				Vector2 cc = p.Center; float rS = 4 * 16f;
				bool atS = Vector2.Distance(cc, Main.npc[tag.Soldier].Center) < rS, atC = Vector2.Distance(cc, Main.player[0].Center) < rS;
				bool atT = s.TargetIdx >= 0 && Main.npc[s.TargetIdx].active && Vector2.Distance(cc, Main.npc[s.TargetIdx].Center) < rS;
				if (atC && !atS) s.ChildAtCmd++; else if (atS) s.ChildAtSol++; else if (atT) s.ChildAtTgt++; else s.ChildOther++;
			}
			var dict = child ? s.Child : s.Proj;
			dict[nm] = dict.TryGetValue(nm, out int c) ? c + 1 : 1;
			s.Spawned++;
		}

		internal static void NoteItem(Item it)
		{
			var s = _sc; if (s == null || _stage != 1) return;
			string n = it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : (ItemID.Search.TryGetName(it.type, out string vn) ? vn : "item" + it.type);
			s.Items[n] = s.Items.TryGetValue(n, out int c) ? c + it.stack : it.stack;
		}

		// ---------------------------------------------------------------------------------------------
		// output
		// ---------------------------------------------------------------------------------------------
		private static double Ms(long ts, int perTicks) => perTicks <= 0 ? 0 : ts * 1000.0 / Stopwatch.Frequency / perTicks;
		private static string F(double v) => v.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
		private static string Fmt(Dictionary<string, int> d) => d.Count == 0 ? "-" : string.Join(";", d.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value));
		private static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		private const string Header = "kind\titem\tname\tfam\tN\tticks\tflags\tnote\taliveStart>End\tuses\tuseExc\tprojSpawnedByUse\tchildSpawned\tmaxLiveTagged\tminionsEnd\tdmgTotal\tdmgIdle\tdmgActive\thitTicks\tavgDistSoldier\tavgDistCommander\tavgDistTargetActive\tdummyEnd\tdummyBuffs\tcmdDiff\tcmdManaMin\tcmdLifeMin\tcmdLifeGain\titems\townerHist\torphanKills\tbossDeadTick\texceptions"
			+ "\tchildAtSoldier\tchildAtCommander\tchildAtTarget\tchildElsewhere\tcmdModPlayerHitProcs\totherModPlayerHitProcs"
			+ "\tms_useTotal/tick\tms_useItemCheck/tick\tms_useOverhead/tick\tms_projOrig/tick\tms_projSwapOverhead/tick\tms_prep/tick\tprojUpdates/tick\tms_wholeTick\tus_swapPerProj";

		private static string Row(Sc s)
		{
			string name = s.ItemType > 0 ? (ContentSamples.ItemsByType.TryGetValue(s.ItemType, out var it) ? it.Name : "?") : "-";
			int tk = Math.Max(1, s.Tick);
			double useTot = Ms(s.UseOverheadTs + s.UseTs, tk), useIC = Ms(s.UseTs, tk), useOv = Ms(s.UseOverheadTs, tk);
			double projOrig = Ms(s.ProjOrigTs, tk), projOv = Ms(s.ProjOverheadTs, tk), prep = Ms(s.PrepTs, tk);
			double whole = s.WholeSamples > 0 ? s.WholeTs * 1000.0 / Stopwatch.Frequency / s.WholeSamples : 0;
			double usPerProj = s.ProjUpdates > 0 ? s.ProjOverheadTs * 1e6 / Stopwatch.Frequency / s.ProjUpdates : 0;
			var f = new List<string>
			{
				s.Kind, s.ItemKey, name, s.Fam, s.N.ToString(), s.Tick.ToString(), string.Join(",", s.Flags.OrderBy(x => x)), s.Note == "" ? "-" : s.Note,
				$"{s.Alive0}>{s.AliveEnd}", s.Sols.Sum(x => x.Uses).ToString(), s.Sols.Sum(x => x.UseExc).ToString(), Fmt(s.Proj), Fmt(s.Child), s.MaxLive.ToString(), s.MinionsEnd.ToString(),
				s.Dmg.ToString(), s.DmgA.ToString(), s.DmgB.ToString(), s.HitTicks.ToString(),
				s.DistSamples > 0 ? (s.SumDistSol / s.DistSamples).ToString("0.0") : "-", s.DistSamples > 0 ? (s.SumDistCmd / s.DistSamples).ToString("0.0") : "-", s.DistTgtSamples > 0 ? (s.SumDistTgt / s.DistTgtSamples).ToString("0.0") : "-",
				s.DummyEnd == "" ? "-" : s.DummyEnd, s.BuffsEnd == "" ? "-" : s.BuffsEnd, s.CmdDiff == "" ? "-" : s.CmdDiff, s.CmdManaMin == int.MaxValue ? "-" : s.CmdManaMin.ToString(), s.CmdLifeMin == int.MaxValue ? "-" : s.CmdLifeMin.ToString(), s.CmdLifeGain.ToString(),
				Fmt(s.Items), string.Join(";", s.OwnerHist.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value)), s.OrphanKills.ToString(), s.BossDeadTick.ToString(),
				(s.Exc.Count == 0 ? "-" : string.Join(" || ", s.Exc.Take(6))) + (s.PlayInstrNre > 0 ? " [benign server-only NRE (PlayInstruments/Achievements) x" + s.PlayInstrNre + "]" : "") + (s.DummyDeaths > 0 ? " [DUMMY KillMe SUPPRESSED x" + s.DummyDeaths + "]" : "") + (s.TpToCmd + s.TpToSelf + s.TpOther > 0 ? $" [GetThoriumPlayer(dummy) during use resolved: commander={s.TpToCmd} self={s.TpToSelf} other={s.TpOther}]" : ""),
				s.ChildAtSol.ToString(), s.ChildAtCmd.ToString(), s.ChildAtTgt.ToString(), s.ChildOther.ToString(), s.CmdProcs.ToString(), s.OtherProcs.ToString(),
				F(useTot), F(useIC), F(useOv), F(projOrig), F(projOv), F(prep), F((double)s.ProjUpdates / tk), F(whole), F(usPerProj),
			};
			return string.Join("\t", f.Select(Clean));
		}

		private static void WriteTsv()
		{
			try
			{
				var sb = new StringBuilder(); sb.AppendLine(Header);
				foreach (var r in _rows) sb.AppendLine(r);
				string tmp = CombatHarness.OutPath + ".tmp";
				File.WriteAllText(tmp, sb.ToString());
				File.Move(tmp, CombatHarness.OutPath, true);
			}
			catch (Exception e) { _h?.Mod.Logger.Warn("LAB: tsv write failed: " + e.Message); }
		}
	}

	/// <summary>Tags a lab projectile with the soldier that owns it (harness-only; inert unless the lab is active).</summary>
	public class LabTag : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		public int Soldier = -1;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!OwnerLab.Active) return;
			var pend = OwnerLab.Pending;
			if (pend != null) Soldier = pend.Body.NPC.whoAmI;
			else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.TryGetGlobalProjectile(out LabTag pt) && pt.Soldier >= 0) Soldier = pt.Soldier;
			else if (OwnerLab.FrameBody != null) Soldier = OwnerLab.FrameBody.NPC.whoAmI;
			if (Soldier < 0) return;
			projectile.npcProj = !OwnerLab.Flag("nonpc"); projectile.noDropItem = true;
			// all owner-255 projectiles share npc.immune[255]; give each its own hit cooldown (what HeldWeapons.JoinChain does) when the flag is on
			if (OwnerLab.Flag("localimm") && projectile.penetrate != 1 && !projectile.usesLocalNPCImmunity && !projectile.usesIDStaticNPCImmunity) { projectile.usesLocalNPCImmunity = true; if (projectile.localNPCHitCooldown <= 0) projectile.localNPCHitCooldown = 10; }
			if (OwnerLab.Legacy && OwnerLab.Pending != null) projectile.owner = 0; // today's ownership: the real commander
			if (projectile.TryGetGlobalProjectile(out SoldierProjectileTag st)) st.CommanderIndex = 0; // loot / kill credit to the commander through the existing officer-credit path
			OwnerLab.NoteSpawn(projectile, this);
		}
	}

	/// <summary>Thorium-touching half of the lab (JIT-gated). The cache GetThoriumPlayer() consults when player.whoAmI == Main.myPlayer.</summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class LabThorium
	{
		public static void SetLocal(Player p) { ThoriumMod.Utilities.PlayerHelper.SetLocalThoriumPlayer(p.GetModPlayer<ThoriumMod.ThoriumPlayer>()); }
		/// <summary>What GetThoriumPlayer() hands back for <paramref name="user"/> right now: 1 = the commander's ThoriumPlayer (the bug), 2 = the user's own, 0 = neither / no cache.</summary>
		public static int Resolve(Player user, Player commander)
		{
			var got = ThoriumMod.Utilities.PlayerHelper.GetThoriumPlayer(user);
			if (ReferenceEquals(got, commander.GetModPlayer<ThoriumMod.ThoriumPlayer>())) return 1;
			if (ReferenceEquals(got, user.GetModPlayer<ThoriumMod.ThoriumPlayer>())) return 2;
			return 0;
		}
		public static string Snap(Player p) { var t = p.GetModPlayer<ThoriumMod.ThoriumPlayer>(); return $"exh{t.throwerExhaustion:0.#}/{t.throwerExhaustionMax} penalty{t.throwerExhaustionPenalty} bard{t.bardResource}"; }
	}

	/// <summary>Counts ModPlayer on-hit hooks that fire for the commander (slot 0) versus anyone else, to show whose accessories/ModPlayer procs a lab projectile triggers.</summary>
	public class LabHitProbe : ModPlayer
	{
		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) { if (OwnerLab.Active) OwnerLab.NoteHitProc(Player.whoAmI); }
	}

	public class LabItemWatch : GlobalItem
	{
		public override void OnSpawn(Item item, IEntitySource source) { if (OwnerLab.Active) OwnerLab.NoteItem(item); }
	}

	public class OwnerLabSystem : ModSystem
	{
		public override void Load()
		{
			OwnerLab.ReadEnv();
			if (!OwnerLab.Active || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS"))) { OwnerLab.Active = false; return; }
			On_Projectile.Update += UpdateDetour;
			On_NPC.StrikeNPC_HitInfo_bool_bool += StrikeDetour;
			On_Player.KillMe += KillMeDetour;
			On_Player.ItemCheck_PlayInstruments += PlayInstrDetour;
			On_AchievementsHelper.HandleSpecialEvent += AchDetour;
		}

		private static void PlayInstrDetour(On_Player.orig_ItemCheck_PlayInstruments orig, Player self, Item sItem) { if (OwnerLab.InScenario && Main.dedServ) return; orig(self, sItem); }
		private static void AchDetour(On_AchievementsHelper.orig_HandleSpecialEvent orig, Player player, int eventID) { if (OwnerLab.InScenario && Main.dedServ) return; orig(player, eventID); }

		/// <summary>A real item can kill its user (Martyr's Chalice spends life): vanilla then runs the LOCAL-player death path (tombstone, coin drop, SavePlayer + process abort on a server). A soldier's dummy must never reach it.</summary>
		private static void KillMeDetour(On_Player.orig_KillMe orig, Player self, Terraria.DataStructures.PlayerDeathReason reason, double dmg, int hitDirection, bool pvp)
		{
			if (OwnerLab.Active && self.name != null && self.name.StartsWith("Pvt. Lab")) { OwnerLab.NoteDummyDeath(); self.statLife = Math.Max(1, self.statLife); self.dead = false; return; }
			orig(self, reason, dmg, hitDirection, pvp);
		}

		public override void Unload()
		{
			if (OwnerLab.Active) { try { On_Projectile.Update -= UpdateDetour; On_NPC.StrikeNPC_HitInfo_bool_bool -= StrikeDetour; On_Player.KillMe -= KillMeDetour; On_Player.ItemCheck_PlayInstruments -= PlayInstrDetour; On_AchievementsHelper.HandleSpecialEvent -= AchDetour; } catch { } }
			OwnerLab.Active = false;
		}

		public override void PreUpdateProjectiles() { if (OwnerLab.Active) { EnemyTargeting.EnsureSlotRestored(); OwnerLab.PrepareTick(); } }
		public override void PostUpdateProjectiles() { if (OwnerLab.Active) EnemyTargeting.EnsureSlotRestored(); }

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (OwnerLab.InScenario && self.TryGetGlobalProjectile(out LabTag tag) && tag.Soldier >= 0) OwnerLab.RunUpdate(orig, self, i, tag);
			else orig(self, i);
		}

		private static int StrikeDetour(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction) =>
			OwnerLab.InScenario && OwnerLab.FrameBody != null ? OwnerLab.RunStrike(orig, self, hit, fromNet, noPlayerInteraction) : orig(self, hit, fromNet, noPlayerInteraction);
	}
}
