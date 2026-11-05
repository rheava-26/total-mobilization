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
	///
	/// PHASE 2 (owner-lab-2) additions. Kinds: gear (a run with worn gear) | scan (IL scan: accessories / armor that set a ModPlayer field an on-hit hook reads) | list (dump a mod's items).
	///   flags: vslot netlock = per-soldier owner slot 100+k, and NetLock held while the Dummy stands in it (Netplay.ServerLoop clears Main.player[i].active for client-less slots; vanilla Projectile.Update kills a projectile whose owner is inactive)
	///          trace = log every kill / silent deactivation of a tagged projectile with the stack and the owner state at that moment
	///          nonpc = npcProj false on soldier projectiles (the owner's ModPlayer on-hit hooks and vanilla Main.player[owner] on-hit flags then run)
	///          g=A+B+C (worn gear: V:/T:/C:/M: item keys; S:Mod/HeadPiece = the whole armor set)  cg=... (the same on the commander)  refresh (RefreshStats once at setup)  halflife  hurt=N (Dummy.Hurt every N ticks)  every=N (press cadence)
	///          pu=s1|s2|s3|full = per-tick Player.Update subset run inside the soldier's swap: s1 = timers + ResetEffects + UpdateEquips + UpdateArmorSets; s2 = s1 + PreUpdateBuffs/UpdateBuffs/PostUpdateBuffs + PostUpdateEquips + PostUpdateMiscEffects; s3 = s2 + PlayerLoader.PreUpdate/PostUpdate; full = Player.Update
	///          diag = melee diagnostics every 30 ticks   ded0 = flip Main.dedServ off around ItemCheck (crashes in asset loading; kept as a record of the attempt)
	/// Viewers: tools/lab_show.py, lab_scale.py, lab_gearmatrix.py.  Runner: tools/run_lab.sh (-N new toolchain for Calamity, -x CalamityModMusic).  Build: tools/lab_build.sh [-N].
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
			public Dictionary<string, int> KillTrace = new(); public Dictionary<string, string> KillSample = new(); public int SilentDeaths, NetClears, NetThreadPasses;
			public List<string> GearKeys = new(), CmdGearKeys = new(); public int HurtEvery, Every;
			public string FlagsOn = "", CmdFlagsOn = "";
			public long DummyLifeGain, DummyManaGain; public HashSet<int> TargetBuffs = new();
			public long SubsetTs, RefreshTs; public int SubsetCalls, RefreshCalls;
			public Dictionary<string, int[]> Loc = new();
			public int HookProjCmd, HookProjOther, HookItemCmd, HookItemOther, HookKillCmd, HookKillOther, HookHurtCmd, HookHurtOther;
			public float StealthStart = -1f, StealthMax, StealthPeak; public HashSet<int> StealthProjs = new(); public int ProjsTotal; public HashSet<int> SeenProj = new();
			public long[] SubBreak = new long[8]; public int HurtCalls; public string GearNote = "", DummyTp0 = "", DummyTp1 = "";
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
		internal static void NoteHitProc(int who) { var s = _sc; if (s == null || _stage != 1) return; if (who == 0) { s.CmdProcs++; s.HookProjCmd++; } else { s.OtherProcs++; s.HookProjOther++; } }
		internal static void NoteHook(int kind, int who) { var s = _sc; if (s == null || _stage != 1) return; bool c = who == 0; if (kind == 1) { if (c) s.HookItemCmd++; else s.HookItemOther++; } else if (kind == 2) { if (c) s.HookKillCmd++; else s.HookKillOther++; } else if (kind == 3) { if (c) s.HookHurtCmd++; else s.HookHurtOther++; } }
		internal static void NoteNetPass() { var s = _sc; if (s != null) s.NetThreadPasses++; }
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
						else if (f.StartsWith("g=")) s.GearKeys.AddRange(f.Substring(2).Split('+', StringSplitOptions.RemoveEmptyEntries));
						else if (f.StartsWith("cg=")) s.CmdGearKeys.AddRange(f.Substring(3).Split('+', StringSplitOptions.RemoveEmptyEntries));
						else if (f.StartsWith("hurt=") && int.TryParse(f.Substring(5), out int he)) s.HurtEvery = he;
						else if (f.StartsWith("every=") && int.TryParse(f.Substring(6), out int ev)) s.Every = ev;
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
						if (_stageTick == 12 && (_sc.GearKeys.Count > 0 || Has(_sc, "refresh"))) RefreshGear(_sc);
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
			GearUpkeep.SetOverride(UpkMode(s)); // the setup ticks run in the scenario's mode too
			// single-player emulation from the first setup tick: the commander is Main.myPlayer and the game menu is off (a dedicated server has myPlayer 255 and gameMenu on)
			if (s.Flags.Contains("gm") && !_gmOn) { _savedGameMenu = Main.gameMenu; Main.gameMenu = false; _gmOn = true; }
			if (s.Flags.Contains("spsim") && !_spsimOn) { _savedMyForSim = Main.myPlayer; Main.myPlayer = 0; _spsimOn = true; }
			if (s.Kind == "list") { try { RunList(s); } catch (Exception e) { s.Exc.Add("list:" + Short(e)); } s.Note = "list"; s.Done = true; s.Ticks = 0; s.ItemType = 0; return; }
			if (s.Kind == "scan") { try { RunScan(s); } catch (Exception e) { s.Exc.Add("scan:" + Short(e)); } s.Note = "scan"; s.Done = true; s.Ticks = 0; s.ItemType = 0; return; }
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
				if (s.GearKeys.Count > 0) EquipGear(s, rec);
				string wk = s.Flags.FirstOrDefault(f => f.StartsWith("wpn="));
				if (wk != null)
				{
					// an armed, engaged soldier (the production brain and combat run): the hand-made accessory procs and the leaf only act while he fights
					int wt = ResolveItem(wk.Substring(4));
					if (wt > 0)
					{
						var w = new Item(); w.SetDefaults(wt); rec.Gear.Weapon = w;
						int at = AmmoFor(w); if (at > 0) { var a = new Item(); a.SetDefaults(at); a.stack = 999; rec.Gear.Ammo = a; }
						rec.HoldFire = false;
						sol.Body?.NotifyGearChanged();
					}
				}
			}
			// commander
			Player c = Main.player[0];
			for (int ai = 0; ai < c.armor.Length; ai++) c.armor[ai] = new Item();
			if (s.CmdGearKeys.Count > 0) EquipCommander(s, c);
			bool heal = s.Fam == "heal";
			c.active = true; c.dead = false; c.name = "LevyTester"; c.statLifeMax = c.statLifeMax2 = 500; c.statLife = heal ? 250 : 500;
			c.position = new Vector2(CxPx - CmdDistOf(s) * 16f, CombatHarness.FloorY * 16f - c.height); c.velocity = Vector2.Zero;
			for (int b = 0; b < c.buffType.Length; b++) { c.buffType[b] = 0; c.buffTime[b] = 0; }
			Array.Clear(c.ownedProjectileCounts, 0, c.ownedProjectileCounts.Length);
			// target
			SpawnTarget(s, s.Fam == "summon" ? 90f : s.Dist);
			s.Fp0 = Fp(c); s.Tp0 = TpSnap(c); _snap0 = CmdSnap(c);
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
			GearUpkeep.SetOverride(-1); GearUpkeep.CountSpawns = false;
			RestoreSlots(s);
			_h.LabClearAll();
			try { Main.player[1].active = false; } catch { }
			if (s.Flags.Contains("gm")) Main.gameMenu = _savedGameMenu;
			if (s.Flags.Contains("spsim") && _spsimOn) { Main.myPlayer = _savedMyForSim; _spsimOn = false; }
		}

		private static bool _spsimOn, _gmOn, _savedGameMenu; private static int _savedMyForSim;
		/// <summary>Everything about the commander (and the machine) a gear frame must not touch: vitals, transform, buffs, every numeric / bool field of the Thorium and Calamity ModPlayers, and the global statics.</summary>
		private static Dictionary<string, string> CmdSnap(Player c)
		{
			var d = new Dictionary<string, string>();
			d["statLife"] = c.statLife.ToString(); d["statMana"] = c.statMana.ToString(); d["pos"] = c.position.ToString(); d["vel"] = c.velocity.ToString();
			d["defense"] = c.statDefense.ToString(); d["whoAmI"] = c.whoAmI.ToString(); d["active"] = c.active.ToString(); d["dead"] = c.dead.ToString();
			d["buffs"] = string.Join(",", Enumerable.Range(0, c.buffType.Length).Where(b => c.buffType[b] > 0).Select(b => c.buffType[b] + ":" + c.buffTime[b]));
			d["soundVolume"] = Main.soundVolume.ToString(); d["musicVolume"] = Main.musicVolume.ToString(); d["myPlayer"] = Main.myPlayer.ToString();
			d["slot255"] = Main.player[255] != null ? (Main.player[255].active ? "active" : "idle") : "null"; d["gameMenu"] = Main.gameMenu.ToString();
			d["screenPos"] = Main.screenPosition.ToString(); d["screenShake"] = "n/a";
			try { d["activeFilters"] = string.Join(",", Terraria.Graphics.Effects.Filters.Scene == null ? new string[0] : new string[] { Terraria.Graphics.Effects.Filters.Scene.GetType().Name }); } catch (Exception) { d["activeFilters"] = "?"; }
			foreach (ModPlayer mp in c.ModPlayers)
			{
				string mn = mp.Mod.Name; if (mn != "ThoriumMod" && mn != "CalamityMod") continue;
				var t = mp.GetType();
				foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(bool)))
				{ try { d[t.Name + "." + f.Name] = Convert.ToString(f.GetValue(mp), System.Globalization.CultureInfo.InvariantCulture); } catch { } }
			}
			return d;
		}
		private static string SnapDiff(Dictionary<string, string> a, Dictionary<string, string> b)
		{
			var ch = new List<string>();
			foreach (var k in a.Keys) if (b.TryGetValue(k, out var v) && v != a[k]) ch.Add(k + ":" + a[k] + ">" + v);
			return ch.Count == 0 ? "" : " || COMMANDER/GLOBAL CHANGED{" + string.Join("; ", ch.Take(20)) + "}";
		}
		private static Dictionary<string, string> _snap0;

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
				if (s.Flags.Any(f => f.StartsWith("wpn=")) && s.TargetIdx >= 0 && Main.npc[s.TargetIdx].active) { sol.Body.State = SoldierState.Engage; sol.Body.TargetIndex = s.TargetIdx; } // armed scenarios: keep the soldier engaged on the target (the lab has no brain run for a Hold soldier here)
				if (Has(s, "halflife")) { sol.Body.Record.CurrentHp = sol.Body.Record.MaxHp / 2; n.life = n.lifeMax / 2; } else { sol.Body.Record.CurrentHp = sol.Body.Record.MaxHp; n.life = n.lifeMax; }
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
			Array.Clear(SubT, 0, SubT.Length);
			GearUpkeep.ResetCounters(); GearUpkeep.CountSpawns = s.Flags.Any(f => f.StartsWith("upk=")); GearUpkeep.SetOverride(UpkMode(s)); _modeMin.Clear(); _modeMax.Clear();
			s.Alive0 = s.Sols.Count(x => x.Body != null && x.Body.NPC.active && x.Body.IsAlive);
			if (s.Flags.Contains("gm")) { _savedGameMenu = Main.gameMenu; Main.gameMenu = false; } // a dedicated server has gameMenu = true, which turns Thorium's local-player cache off; SP / client have it false
			if (s.Flags.Contains("spsim") && !_spsimOn) { _savedMyForSim = Main.myPlayer; Main.myPlayer = 0; _spsimOn = true; }
			if (s.Flags.Contains("slots")) PlaceSlots(s);
			s.CmdPrevLife = Main.player[0].statLife; { var dd = s.Sols.FirstOrDefault(x => x.Body?.Dummy != null)?.Body?.Dummy; if (dd != null) s.DummyTp0 = TpSnap(dd); }
			_prepStamp = uint.MaxValue;
		}

		private static void EndRun(Sc s)
		{
			Array.Copy(SubT, s.SubBreak, SubT.Length);
			s.Note = UpkNote(s);
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
			if (d0 != null) s.DummyTp1 = TpSnap(d0);
			s.Fp1 = Fp(Main.player[0]); s.Tp1 = TpSnap(Main.player[0]);
			string snapDiff = _snap0 == null ? "" : SnapDiff(_snap0, CmdSnap(Main.player[0]));
			s.CmdDiff = (snapDiff == "" ? "" : "LEAK" + snapDiff + " | ") + (s.Fp0 == s.Fp1 ? "none" : "BEFORE " + s.Fp0 + " || AFTER " + s.Fp1) + (s.Tp0 == s.Tp1 ? "" : " || THORIUMPLAYER(commander) BEFORE " + s.Tp0 + " AFTER " + s.Tp1);
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

			// target debuffs
			if (s.TargetIdx >= 0 && Main.npc[s.TargetIdx].active) { NPC tn = Main.npc[s.TargetIdx]; for (int b = 0; b < NPC.maxBuffs; b++) if (tn.buffType[b] > 0) s.TargetBuffs.Add(tn.buffType[b]); }
			foreach (var sol in s.Sols) SampleDummy(s, sol);
			UpkSample(s);
			if (s.HurtEvery > 0 && s.Tick > 0 && s.Tick % s.HurtEvery == 0) foreach (var sol in s.Sols) if (sol.Body != null && sol.Body.NPC.active) HurtOnce(s, sol);

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
				if (s.SeenProj.Add(p.identity)) s.ProjsTotal++;
				if (HarnessCalamityOn) { try { if (HarnessCalamityProbe.IsStealthStrike(p, out _)) s.StealthProjs.Add(p.identity); } catch (Exception) { } }
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


		// ---------------------------------------------------------------------------------------------
		// gear
		// ---------------------------------------------------------------------------------------------
		private static void EquipGear(Sc s, SoldierRecord rec)
		{
			rec.Grade = Grade.Champion; // 5 accessory slots
			var g = rec.Gear; int acc = 0; var notes = new List<string>();
			foreach (string key in s.GearKeys)
			{
				if (key.StartsWith("S:")) { if (!ResolveSet(key.Substring(2), g, notes)) notes.Add("NOSET:" + key); continue; } // S:Mod/HeadPiece = the whole armor set that piece belongs to
				int t = ResolveItem(key);
				if (t <= 0) { notes.Add("NOTFOUND:" + key); continue; }
				var it = new Item(); it.SetDefaults(t);
				if (it.headSlot >= 0 && !it.accessory) g.Head = it;
				else if (it.bodySlot >= 0 && !it.accessory) g.Body = it;
				else if (it.legSlot >= 0 && !it.accessory) g.Legs = it;
				else if (it.accessory && acc < g.Accessories.Length) g.Accessories[acc++] = it;
				else notes.Add("NOSLOT:" + key);
			}
			if (notes.Count > 0) s.GearNote = string.Join(",", notes);
		}


		/// <summary>Finds body and legs pieces that complete the set the given head piece belongs to (ModItem.IsArmorSet) and puts all three on the gear.</summary>
		private static bool ResolveSet(string headName, SoldierGear g, List<string> notes)
		{
			if (!ModContent.TryFind<ModItem>(headName, out var hm)) return false;
			var head = new Item(); head.SetDefaults(hm.Type);
			var bodies = ModContent.GetContent<ModItem>().Where(m => m.Mod == hm.Mod && ContentSamples.ItemsByType.TryGetValue(m.Type, out var it) && it.bodySlot >= 0 && !it.accessory).ToList();
			var legs = ModContent.GetContent<ModItem>().Where(m => m.Mod == hm.Mod && ContentSamples.ItemsByType.TryGetValue(m.Type, out var it) && it.legSlot >= 0 && !it.accessory).ToList();
			foreach (var b in bodies)
			{
				var bi = new Item(); bi.SetDefaults(b.Type);
				foreach (var l in legs)
				{
					var li = new Item(); li.SetDefaults(l.Type);
					bool ok = false; try { ok = hm.IsArmorSet(head, bi, li); } catch (Exception) { }
					if (ok) { g.Head = head; g.Body = bi; g.Legs = li; notes.Add("SET:" + hm.Name + "+" + b.Name + "+" + l.Name); return true; }
				}
			}
			return false;
		}

		private static void EquipCommander(Sc s, Player c)
		{
			int acc = 0;
			foreach (string key in s.CmdGearKeys)
			{
				int t = ResolveItem(key); if (t <= 0) continue;
				var it = new Item(); it.SetDefaults(t);
				if (it.headSlot >= 0 && !it.accessory) c.armor[0] = it;
				else if (it.bodySlot >= 0 && !it.accessory) c.armor[1] = it;
				else if (it.legSlot >= 0 && !it.accessory) c.armor[2] = it;
				else if (it.accessory && acc < 5) c.armor[3 + acc++] = it;
			}
		}

		private static HashSet<string> _cleanTrue, _cleanModTrue;
		private static readonly Dictionary<Type, FieldInfo[]> _mpBools = new();
		/// <summary>"Type.field" of every bool field that is true on any of this player's ModPlayers from Thorium / Calamity.</summary>
		private static HashSet<string> ModTrue(Player p)
		{
			var res = new HashSet<string>();
			foreach (ModPlayer mp in p.ModPlayers)
			{
				string mn = mp.Mod.Name; if (mn != "ThoriumMod" && mn != "CalamityMod") continue;
				var t = mp.GetType();
				if (!_mpBools.TryGetValue(t, out var fs)) _mpBools[t] = fs = t.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => f.FieldType == typeof(bool)).ToArray();
				foreach (var f in fs) { try { if ((bool)f.GetValue(mp)) res.Add(t.Name + "." + f.Name); } catch { } }
			}
			return res;
		}
		private static List<FieldInfo> _boolFields;
		/// <summary>Public bool fields of Player that are true now and are not true on a freshly reset player: the flags a piece of gear switched on.</summary>
		private static string FlagsOnOf(Player p)
		{
			try
			{
				if (_boolFields == null) _boolFields = typeof(Player).GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => f.FieldType == typeof(bool)).ToList();
				if (_cleanTrue == null)
				{
					var clean = new Player(); clean.ResetEffects();
					_cleanTrue = new HashSet<string>(_boolFields.Where(f => (bool)f.GetValue(clean)).Select(f => f.Name));
				}
				var modOn = new List<string>();
				try
				{
					_cleanModTrue ??= ModTrue(new Player());
					foreach (var kv in ModTrue(p)) if (!_cleanModTrue.Contains(kv)) modOn.Add(kv);
				}
				catch (Exception) { }
				var on = _boolFields.Where(f => { try { return (bool)f.GetValue(p); } catch { return false; } }).Select(f => f.Name).Where(n => !_cleanTrue.Contains(n) && n != "active" && n != "dead" && n != "ghost" && n != "Male" && n != "releaseUseItem" && n != "mouseInterface" && n != "controlLeft").ToList();
				on.AddRange(modOn);
				return string.Join(" ", on);
			}
			catch (Exception e) { return "?" + e.Message; }
		}

		private static void RefreshGear(Sc s)
		{
			foreach (var sol in s.Sols)
			{
				if (sol.Body == null) continue;
				long t0 = Stopwatch.GetTimestamp();
				try { sol.Body.RefreshStats(); } catch (Exception e) { if (s.Exc.Count < 8) s.Exc.Add("refresh:" + Short(e)); }
				s.RefreshTs += Stopwatch.GetTimestamp() - t0; s.RefreshCalls++;
			}
			var d0 = s.Sols.FirstOrDefault(x => x.Body?.Dummy != null)?.Body?.Dummy;
			if (d0 != null) s.FlagsOn = FlagsOnOf(d0);
			s.CmdFlagsOn = FlagsOnOf(Main.player[0]);
		}

		// ---------------------------------------------------------------------------------------------
		// Player.Update subsets (flag pu=s1|s2|full), run inside the soldier's swap once per tick
		// ---------------------------------------------------------------------------------------------
		private static int[] _cntScratch;
		private static readonly long[] SubT = new long[8];
		private static MethodInfo _ppPreUpdate, _ppPostUpdate, _ppPreBuffs, _ppPostBuffs, _ppPostEquips, _ppPostMisc, _updEquips, _updArmorSets, _updDyes, _updLife, _updMana;
		private static void RunSubset(Sc s, Sol sol, string mode)
		{
			Player d = sol.Body.Dummy;
			const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			if (_updEquips == null)
			{
				_updEquips = typeof(Player).GetMethod("UpdateEquips", F, null, new[] { typeof(int) }, null);
				_updArmorSets = typeof(Player).GetMethod("UpdateArmorSets", F, null, new[] { typeof(int) }, null);
				_updateBuffs ??= typeof(Player).GetMethod("UpdateBuffs", F, null, new[] { typeof(int) }, null);
				var pl = typeof(PlayerLoader);
				_ppPreUpdate = pl.GetMethod("PreUpdate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_ppPostUpdate = pl.GetMethod("PostUpdate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_ppPreBuffs = pl.GetMethod("PreUpdateBuffs", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_ppPostBuffs = pl.GetMethod("PostUpdateBuffs", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_ppPostEquips = pl.GetMethod("PostUpdateEquips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_ppPostMisc = pl.GetMethod("PostUpdateMiscEffects", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			int slot = d.whoAmI;
			if (mode == "full")
			{
				d.Update(slot);
				return;
			}
			// what Player.Update decrements every tick and the dummy never ran
			if (d.petalTimer > 0) d.petalTimer--;
			if (d.shadowDodgeTimer > 0) d.shadowDodgeTimer--;
			if (d.boneGloveTimer > 0) d.boneGloveTimer--;
			if (d.crystalLeafCooldown > 0) d.crystalLeafCooldown--;
			// ResetEffects ends with ResetProjectileCaches (it zeroes ownedProjectileCounts); Player.Update re-counts right after it, so the counts PrepareTick just made are put back
			_cntScratch ??= new int[d.ownedProjectileCounts.Length];
			Array.Copy(d.ownedProjectileCounts, _cntScratch, _cntScratch.Length); int hs = d.highestStormTigerGemOriginalDamage, ha = d.highestAbigailCounterOriginalDamage;
			if (mode == "s3") _ppPreUpdate?.Invoke(null, new object[] { d });
			long t0 = Stopwatch.GetTimestamp();
			d.ResetEffects();
			Array.Copy(_cntScratch, d.ownedProjectileCounts, _cntScratch.Length); d.highestStormTigerGemOriginalDamage = hs; d.highestAbigailCounterOriginalDamage = ha;
			long t1 = Stopwatch.GetTimestamp(); SubT[0] += t1 - t0;
			if (mode == "s2" || mode == "s3")
			{
				_ppPreBuffs?.Invoke(null, new object[] { d });
				if (d.buffType.Any(b => b > 0)) _updateBuffs.Invoke(d, new object[] { slot });
				_ppPostBuffs?.Invoke(null, new object[] { d });
			}
			long t2 = Stopwatch.GetTimestamp(); SubT[1] += t2 - t1;
			d.head = d.armor[0].headSlot; d.body = d.armor[1].bodySlot; d.legs = d.armor[2].legSlot;
			_updEquips.Invoke(d, new object[] { slot });
			long t3 = Stopwatch.GetTimestamp(); SubT[2] += t3 - t2;
			_updArmorSets.Invoke(d, new object[] { slot });
			long t4 = Stopwatch.GetTimestamp(); SubT[3] += t4 - t3;
			if (mode == "s2" || mode == "s3") _ppPostEquips?.Invoke(null, new object[] { d });
			long t5 = Stopwatch.GetTimestamp(); SubT[4] += t5 - t4;
			if (mode == "s2" || mode == "s3") _ppPostMisc?.Invoke(null, new object[] { d });
			long t6 = Stopwatch.GetTimestamp(); SubT[5] += t6 - t5;
			if (mode == "s3") _ppPostUpdate?.Invoke(null, new object[] { d });
			SubT[6] += Stopwatch.GetTimestamp() - t6;
		}

		private static void SampleDummy(Sc s, Sol sol)
		{
			var d = sol.Body?.Dummy; if (d == null) return;
			if (HarnessCalamityOn)
			{
				try
				{
					float[] st = HarnessCalamityProbe.Stealth(d);
					if (st != null) { if (s.StealthStart < 0) s.StealthStart = st[0]; if (st[1] > s.StealthMax) s.StealthMax = st[1]; if (st[0] > s.StealthPeak) s.StealthPeak = st[0]; }
				}
				catch (Exception) { }
			}
		}
		// ---- gear-upkeep measurement (flags: upk=0|1|2 sets GearUpkeep.Mode for the scenario, phurt=N calls the soldier's hurt-gear step every N ticks) ----
		private static readonly Dictionary<string, float> _modeMin = new(), _modeMax = new();
		private static bool[] _pa; private static int[] _pt, _pid;
		private static readonly Dictionary<Type, FieldInfo[]> _modeFields = new();
		private static int UpkMode(Sc s) { foreach (var f in s.Flags) if (f.StartsWith("upk=") && int.TryParse(f.Substring(4), out int m)) return m; return -1; }
		private static int PHurt(Sc s) { foreach (var f in s.Flags) if (f.StartsWith("phurt=") && int.TryParse(f.Substring(6), out int m)) return m; return 0; }
		private static void UpkSample(Sc s)
		{
			// every soldier-tagged projectile seen for the first time, by type (the doubling / adoption evidence)
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				_pa ??= new bool[Main.maxProjectiles]; _pt ??= new int[Main.maxProjectiles]; _pid ??= new int[Main.maxProjectiles];
				bool tagged = p.active && p.TryGetGlobalProjectile(out SoldierProjectileTag t0) && t0.IsSoldierShot;
				bool fresh = tagged && (!_pa[i] || _pt[i] != p.type || _pid[i] != p.identity);
				_pa[i] = tagged; _pt[i] = tagged ? p.type : 0; _pid[i] = tagged ? p.identity : 0;
				if (!fresh) continue;
				string n = TypeName(p.type); s.Child[n] = s.Child.TryGetValue(n, out int c) ? c + 1 : 1;
				if (p.owner != Main.myPlayer) s.OwnerNon255++;
			}
			int ph = PHurt(s);
			if (ph > 0 && s.Tick > 0 && s.Tick % ph == 0) foreach (var sol in s.Sols) if (sol.Body != null && sol.Body.NPC.active && sol.Body.IsAlive) { try { sol.Body.GearOnHurt(25, -1); } catch (Exception e) { if (s.Exc.Count < 8) s.Exc.Add("phurt:" + Short(e)); } }
			// ModPlayer fields of the first soldier's dummy that MOVE during the run: the timers / counters / meters the upkeep advances
			var d = s.Sols.FirstOrDefault(x => x.Body?.Dummy != null)?.Body?.Dummy; if (d == null) return;
			foreach (ModPlayer mp in d.ModPlayers)
			{
				string mn = mp.Mod.Name; if (mn != "ThoriumMod" && mn != "CalamityMod") continue;
				var t = mp.GetType();
				if (!_modeFields.TryGetValue(t, out var fs)) _modeFields[t] = fs = t.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(bool)).ToArray();
				foreach (var f in fs)
				{
					float v; try { object o = f.GetValue(mp); v = o is bool b ? (b ? 1f : 0f) : Convert.ToSingle(o); } catch { continue; }
					string key = t.Name + "." + f.Name;
					if (!_modeMin.TryGetValue(key, out float lo)) { _modeMin[key] = v; _modeMax[key] = v; continue; }
					if (v < lo) _modeMin[key] = v;
					if (v > _modeMax[key]) _modeMax[key] = v;
				}
			}
		}
		private static string UpkNote(Sc s)
		{
			var moved = _modeMin.Where(k => _modeMax[k.Key] != k.Value).Select(k => k.Key + "[" + k.Value.ToString("0.##") + ".." + _modeMax[k.Key].ToString("0.##") + "]").Take(24);
			double us = GearUpkeep.Frames > 0 ? GearUpkeep.Ticks * 1e6 / Stopwatch.Frequency / GearUpkeep.Frames : 0;
			int tk = Math.Max(1, s.Tick);
			double msTick = GearUpkeep.Ticks * 1000.0 / Stopwatch.Frequency / tk;
			var b0 = s.Sols.FirstOrDefault(x => x.Body != null)?.Body;
			string st = b0 == null ? "nobody" : $"state={b0.State} tgt={b0.TargetIndex} weapon={(b0.Weapon != null ? b0.Weapon.ItemType.ToString() : "null")} hold={b0.Record?.HoldFire} order={b0.Record?.Order} useSignals={GearUpkeep.UseSignals} ";
			return st + "upk mode=" + GearUpkeep.Mode + " frames=" + GearUpkeep.Frames + " us/frame=" + us.ToString("0.0") + " ms/tick=" + msTick.ToString("0.000") + " adopted=" + GearUpkeep.Adopted + "{" + string.Join(",", GearUpkeep.AdoptedByType.Select(k => TypeName(k.Key) + "x" + k.Value)) + "} dropped{" + string.Join(",", GearUpkeep.Dropped.Select(k => TypeName(k.Key) + "x" + k.Value)) + "} ownerBound{" + string.Join(",", GearUpkeep.OwnerBoundTypes.Select(k => TypeName(k.Key) + "x" + k.Value)) + "} spawned{" + string.Join(",", GearUpkeep.SpawnCounts.OrderByDescending(k => k.Value).Take(14).Select(k => TypeName(k.Key) + "x" + k.Value)) + "} fails=" + GearUpkeep.Failures + " hookfails=" + GearUpkeep.HookFailures + " lifeWB=" + GearUpkeep.LifeWriteBacks + " manaWB=" + GearUpkeep.ManaWriteBacks + " hurts=" + GearUpkeep.HurtCalls + " moved{" + string.Join(" ", moved) + "}";
		}

		private static bool HarnessCalamityOn => ModLoader.HasMod("CalamityMod");

		private static void HurtOnce(Sc s, Sol sol)
		{
			int savedMy = Enter(s, sol);
			PendingBodySet(null);
			SoldierNPC of = FrameBody; FrameBody = sol.Body; FrameSlot = sol.SlotIdx;
			try
			{
				Player d = sol.Body.Dummy;
				var info = new Player.HurtInfo { DamageSource = Terraria.DataStructures.PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral("lab")), Damage = 25, HitDirection = 1, CooldownCounter = -1, Dodgeable = false, Knockback = 0f, PvP = false, SoundDisabled = true, DustDisabled = true };
				d.immune = false; d.immuneTime = 0;
				d.Hurt(info, true);
				s.HurtCalls++;
			}
			catch (Exception e) { if (s.Exc.Count < 8) s.Exc.Add("hurt:" + Short(e)); }
			finally { FrameBody = of; Leave(s, sol, savedMy); }
		}


		// ---------------------------------------------------------------------------------------------
		// IL scan (kind "scan"): which accessories / armor sets set a ModPlayer field that an on-hit hook of the same mod reads?
		// ---------------------------------------------------------------------------------------------
		private static Dictionary<short, System.Reflection.Emit.OpCode> _ops;
		private static void FieldsTouched(MethodBase m, bool store, HashSet<FieldInfo> into, HashSet<MethodBase> seen, int depth)
		{
			if (m == null || !seen.Add(m) || depth < 0) return;
			MethodBody body; try { body = m.GetMethodBody(); } catch { return; }
			byte[] il = body?.GetILAsByteArray(); if (il == null) return;
			if (_ops == null)
			{
				_ops = new();
				foreach (var f in typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
					if (f.GetValue(null) is System.Reflection.Emit.OpCode oc) _ops[oc.Value] = oc;
			}
			int i = 0;
			while (i < il.Length)
			{
				short v = il[i++]; if (v == 0xFE && i < il.Length) v = (short)(0xFE00 | il[i++]);
				if (!_ops.TryGetValue(v, out var op)) return;
				int size = op.OperandType switch
				{
					System.Reflection.Emit.OperandType.InlineNone => 0,
					System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
					System.Reflection.Emit.OperandType.InlineVar => 2,
					System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
					System.Reflection.Emit.OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
					_ => 4,
				};
				if (op.OperandType == System.Reflection.Emit.OperandType.InlineField && i + 4 <= il.Length)
				{
					bool isStore = op == System.Reflection.Emit.OpCodes.Stfld;
					bool isLoad = op == System.Reflection.Emit.OpCodes.Ldfld || op == System.Reflection.Emit.OpCodes.Ldflda;
					if ((store && isStore) || (!store && isLoad)) { try { var fi = m.Module.ResolveField(BitConverter.ToInt32(il, i), m.DeclaringType?.IsGenericType == true ? m.DeclaringType.GetGenericArguments() : null, null); if (fi != null) into.Add(fi); } catch { } }
				}
				else if ((op == System.Reflection.Emit.OpCodes.Call || op == System.Reflection.Emit.OpCodes.Callvirt) && depth > 0 && i + 4 <= il.Length)
				{
					try { var cm = m.Module.ResolveMethod(BitConverter.ToInt32(il, i)); if (cm != null && cm.DeclaringType != null && cm.DeclaringType.Assembly == m.DeclaringType.Assembly) FieldsTouched(cm, store, into, seen, depth - 1); } catch { }
				}
				i += size;
			}
		}


		private static void RunList(Sc s)
		{
			string modName = s.ItemKey; if (!ModLoader.TryGetMod(modName, out Mod mod)) return;
			var sb = new StringBuilder();
			foreach (ModItem mi in ModContent.GetContent<ModItem>().Where(m => m.Mod == mod))
			{
				if (!ContentSamples.ItemsByType.TryGetValue(mi.Type, out var it)) continue;
				string kind = it.accessory ? "acc" : it.headSlot >= 0 ? "head" : it.bodySlot >= 0 ? "body" : it.legSlot >= 0 ? "legs" : it.damage > 0 && it.shoot > 0 ? "weapon" : it.damage > 0 ? "melee" : "other";
				sb.AppendLine($"{mi.Name}\t{kind}\t{it.DamageType?.Name}\tdmg{it.damage}\tuse{it.useStyle}\tshoot{it.shoot}\tammo{it.useAmmo}\tmana{it.mana}\trarity{it.rare}");
			}
			File.WriteAllText(CombatHarness.OutPath + ".list." + modName + ".txt", sb.ToString());
		}

		private static void RunScan(Sc s)
		{
			string modName = s.ItemKey; if (!ModLoader.TryGetMod(modName, out Mod mod)) { s.Exc.Add("scan: mod not loaded " + modName); return; }
			var hooks = new HashSet<FieldInfo>();
			string[] hookNames = { "OnHitNPC", "OnHitNPCWithProj", "OnHitNPCWithItem", "ModifyHitNPC", "ModifyHitNPCWithProj", "ModifyHitNPCWithItem", "OnHurt", "OnHitByNPC", "OnHitByProjectile", "OnHitAnything", "OnKillNPC" };
			var sb = new StringBuilder();
			foreach (ModPlayer mp in ModContent.GetContent<ModPlayer>().Where(m => m.Mod == mod))
			{
				var t = mp.GetType();
				foreach (var mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					if (hookNames.Contains(mi.Name)) FieldsTouched(mi, false, hooks, new HashSet<MethodBase>(), 2);
			}
			var hookFields = new HashSet<FieldInfo>(hooks.Where(f => f.DeclaringType != null && typeof(ModPlayer).IsAssignableFrom(f.DeclaringType) && (f.FieldType == typeof(bool) || f.FieldType == typeof(int) || f.FieldType == typeof(float))));
			sb.AppendLine("# on-hit-read ModPlayer fields: " + hookFields.Count);
			foreach (ModItem mi in ModContent.GetContent<ModItem>().Where(m => m.Mod == mod))
			{
				Item it = ContentSamples.ItemsByType.TryGetValue(mi.Type, out var sample) ? sample : null; if (it == null) continue;
				bool acc = it.accessory, armor = it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0;
				if (!acc && !armor) continue;
				var stores = new HashSet<FieldInfo>();
				var seen = new HashSet<MethodBase>();
				var t = mi.GetType();
				foreach (var name in new[] { "UpdateAccessory", "UpdateArmorSet", "UpdateEquip" })
					foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(x => x.Name == name))
						FieldsTouched(m, true, stores, seen, 2);
				var hit = stores.Where(f => hookFields.Contains(f)).Select(f => f.DeclaringType.Name + "." + f.Name).Distinct().ToList();
				if (hit.Count > 0) sb.AppendLine($"{(acc ? "ACC" : "ARMOR")}\t{mod.Name}/{mi.Name}\tslots h{it.headSlot} b{it.bodySlot} l{it.legSlot}\t{string.Join(",", hit)}");
			}
			File.WriteAllText(CombatHarness.OutPath + ".scan." + modName + ".txt", sb.ToString());
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
		/// <summary>Held while a soldier's Dummy stands in a real player slot (&lt; 255); Netplay.UpdateConnectedClients takes it too (OwnerLabSystem.NetDetour).</summary>
		internal static readonly object NetLock = new object();
		internal static volatile int FrameDepth;
		private static bool _lifted;
		private static int _trueMy;
		private static int _targetIdxForFrame = -1;

		/// <summary>Puts the soldier's Dummy where its owner reads it. Returns the saved Main.myPlayer.</summary>
		private static int Enter(Sc s, Sol sol)
		{
			int saved = Main.myPlayer; FrameDepth++;
			if (Has(s, "netlock") && (Has(s, "vslot") || Has(s, "slots"))) System.Threading.Monitor.Enter(NetLock);
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
			Main.myPlayer = savedMy; FrameDepth--;
			if (Has(s, "vslot")) { Main.player[sol.SlotIdx] = sol.Bk ?? new Player(); sol.Bk = null; sol.Body.Dummy.whoAmI = Slot; }
			else if (!Has(s, "slots")) { EnemyTargeting.Unswap(); EnemyTargeting.EnsureSlotRestored(); }
			if (Has(s, "tpcache")) TpSetLocal(Main.player[Main.myPlayer]); // put the real local player's cached ThoriumPlayer back
			if (Has(s, "netlock") && (Has(s, "vslot") || Has(s, "slots"))) System.Threading.Monitor.Exit(NetLock);
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
			else press = phaseB && (s.Every > 0 ? s.Tick % s.Every == 0 : (hold ? (s.Tick % 45 != 0) : (s.Tick % 2 == 0)));

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
				int life0 = d.statLife, mana0 = d.statMana;
				if (d.itemAnimation == 0 && (d.lastVisualizedSelectedItem == null || d.lastVisualizedSelectedItem.type != d.HeldItem.type)) d.lastVisualizedSelectedItem = d.HeldItem.Clone(); // what Player.ItemCheckWrapped keeps; without it the melee use-style/hitbox sees an air item
				bool savedDed = Main.dedServ; if (Has(s, "ded0")) Main.dedServ = false; // diagnostic: Player.ItemCheck_ApplyUseStyle_Inner returns at once on a dedicated server
				try { _itemCheck.Invoke(d, null); }
				catch (TargetInvocationException tie) when ((tie.InnerException?.StackTrace ?? "").Contains("ItemCheck_PlayInstruments") || (tie.InnerException?.StackTrace ?? "").Contains("AchievementsHelper")) { s.PlayInstrNre++; } // server has no audio engine; vanilla's own baseline hits it too
				finally { Main.dedServ = savedDed; if (d.itemAnimation == 0) d.lastVisualizedSelectedItem = d.HeldItem.Clone(); icTs = Stopwatch.GetTimestamp() - c0; s.UseTs += icTs; if (d.statLife > life0) s.DummyLifeGain += d.statLife - life0; }
				if (press) sol.Uses++;
				if (Has(s, "diag") && s.Tick % 30 == 2 && s.TargetIdx >= 0) { var tn = Main.npc[s.TargetIdx]; _h.Mod.Logger.Info($"LABDIAG tick{s.Tick} anim={d.itemAnimation}/{d.itemAnimationMax} itemTime={d.itemTime} loc={d.itemLocation} rot={d.itemRotation} noMelee={d.inventory[0].noMelee} useStyle={d.inventory[0].useStyle} dummyPos={d.position} dir={d.direction} tgt={tn.Hitbox} immune={tn.immune[Math.Min(d.whoAmI, 254)]} tgtLife={tn.life}/{tn.lifeMax} dmgItem={d.GetWeaponDamage(d.inventory[0])} myP={Main.myPlayer} slotP={d.whoAmI} press={press}"); }
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
			if (s == null || _stage != 1 || s.Sols.Count == 0 || UpkMode(s) >= 0) return; // upk scenarios run the PRODUCTION upkeep: none of the lab's own per-tick prep
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
				string pu = Has(s, "pu=s1") ? "s1" : Has(s, "pu=s2") ? "s2" : Has(s, "pu=s3") ? "s3" : Has(s, "pu=full") ? "full" : null;
				if (pu != null)
				{
					long ts0 = Stopwatch.GetTimestamp();
					int savedMy = Enter(s, sol);
					SoldierNPC of = FrameBody; FrameBody = sol.Body; FrameSlot = sol.SlotIdx;
					try { RunSubset(s, sol, pu); }
					catch (Exception e) { if (s.Exc.Count < 8) s.Exc.Add("subset:" + Short(e)); }
					finally { FrameBody = of; Leave(s, sol, savedMy); }
					s.SubsetTs += Stopwatch.GetTimestamp() - ts0; s.SubsetCalls++;
				}
				else if (buffs && d.buffType.Any(b => b > 0))
				{
					int savedMy = Enter(s, sol);
					SoldierNPC of = FrameBody; FrameBody = sol.Body; FrameSlot = sol.SlotIdx;
					try
					{
						_updateBuffs ??= typeof(Player).GetMethod("UpdateBuffs", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
						_updateBuffs.Invoke(d, new object[] { d.whoAmI });
					}
					catch (Exception e) { if (s.Exc.Count < 8) s.Exc.Add("buffs:" + Short(e)); }
					finally { FrameBody = of; Leave(s, sol, savedMy); }
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
				bool wasActive = p.active; int killsBefore = KillCount(s); int lifeB = d.statLife;
				orig(p, i);
				if (d.statLife > lifeB) s.DummyLifeGain += d.statLife - lifeB;
				if (!d.active) { s.NetClears++; d.active = true; }
				if (Has(s, "trace") && wasActive && !p.active && KillCount(s) == killsBefore)
				{
					s.SilentDeaths++; string key = (p.minion ? "M:" : "P:") + TypeName(p.type) + " | SILENT active=false (no Kill call)";
					s.KillTrace[key] = s.KillTrace.TryGetValue(key, out int c0) ? c0 + 1 : 1; if (!s.KillSample.ContainsKey(key)) s.KillSample[key] = DeathState(p, sol, "silent");
				}
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


		// ---------------------------------------------------------------------------------------------
		// minion-death tracing (flag "trace")
		// ---------------------------------------------------------------------------------------------
		private static FieldInfo[] _minionFlagFields;
		private static FieldInfo[] MinionFlagFields()
		{
			if (_minionFlagFields != null) return _minionFlagFields;
			var l = new List<FieldInfo>();
			foreach (var fi in typeof(Player).GetFields(BindingFlags.Instance | BindingFlags.Public))
				if (fi.FieldType == typeof(bool) && fi.Name.Contains("Minion")) l.Add(fi);
			return _minionFlagFields = l.ToArray();
		}

		/// <summary>Everything a projectile's death could depend on, at the moment it dies.</summary>
		private static string DeathState(Projectile p, Sol sol, string how)
		{
			var d = sol?.Body?.Dummy;
			var sb = new StringBuilder();
			sb.Append(how).Append(" owner=").Append(p.owner).Append(" myPlayer=").Append(Main.myPlayer);
			Player op = (p.owner >= 0 && p.owner < Main.player.Length) ? Main.player[p.owner] : null;
			sb.Append(" slotActive=").Append(op != null && op.active ? 1 : 0).Append(" slotIsDummy=").Append(op != null && d != null && ReferenceEquals(op, d) ? 1 : 0);
			sb.Append(" dummyActive=").Append(d != null && d.active ? 1 : 0).Append(" dummyDead=").Append(d != null && d.dead ? 1 : 0);
			sb.Append(" timeLeft=").Append(p.timeLeft).Append(" ai0=").Append(p.ai[0].ToString("0.#")).Append(" minionSlots=").Append(p.minionSlots);
			if (op != null) sb.Append(" slotsMinions=").Append(op.slotsMinions.ToString("0.##")).Append(" max=").Append(op.maxMinions).Append(" num=").Append(op.numMinions).Append(" owned=").Append(p.type < op.ownedProjectileCounts.Length ? op.ownedProjectileCounts[p.type] : -1);
			sb.Append(" overCap=").Append(op != null && op.slotsMinions + p.minionSlots > op.maxMinions ? 1 : 0);
			if (op != null) { sb.Append(" flags["); foreach (var fi in MinionFlagFields()) { try { if ((bool)fi.GetValue(op)) sb.Append(fi.Name).Append(' '); } catch { } } sb.Append(']'); }
			if (op != null) sb.Append(" buffs[").Append(string.Join(",", Enumerable.Range(0, op.buffType.Length).Where(b => op.buffType[b] > 0).Select(b => op.buffType[b] + ":" + op.buffTime[b]))).Append(']');
			return sb.ToString();
		}

		private static string StackKey()
		{
			var st = new StackTrace(2, false);
			var parts = new List<string>();
			foreach (var fr in st.GetFrames())
			{
				var m = fr.GetMethod(); if (m == null) continue;
				string tn = m.DeclaringType?.Name ?? "?";
				if (tn.Contains("hook_") || tn.StartsWith("DMD") || tn.Contains("OwnerLab") || tn.Contains("DynamicClass")) continue;
				parts.Add(tn + "." + m.Name);
				if (parts.Count >= 4) break;
			}
			return string.Join(" < ", parts);
		}

		private static int KillCount(Sc s) { int n = 0; foreach (var v in s.KillTrace.Values) n += v; return n; }

		internal static void RunKill(On_Projectile.orig_Kill orig, Projectile p, LabTag tag)
		{
			var s = _sc;
			if (s != null && _stage == 1 && Has(s, "trace") && p.active)
			{
				try
				{
					Sol sol = FindSol(s, tag.Soldier);
					string how = p.timeLeft <= 0 ? "timeLeft<=0" : "kill";
					string key = (p.minion ? "M:" : "P:") + TypeName(p.type) + " | " + how + " | " + StackKey();
					s.KillTrace[key] = s.KillTrace.TryGetValue(key, out int c) ? c + 1 : 1;
					if (!s.KillSample.ContainsKey(key)) s.KillSample[key] = DeathState(p, sol, how);
				}
				catch (Exception e) { if (s.Exc.Count < 8) s.Exc.Add("trace:" + Short(e)); }
			}
			orig(p);
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
			if (tag.Soldier >= 0 && Main.npc[tag.Soldier].active)
			{
				Vector2 cc2 = p.Center; float r2 = 5 * 16f;
				bool aS = Vector2.Distance(cc2, Main.npc[tag.Soldier].Center) < r2, aC = Vector2.Distance(cc2, Main.player[0].Center) < r2;
				bool aT = s.TargetIdx >= 0 && Main.npc[s.TargetIdx].active && Vector2.Distance(cc2, Main.npc[s.TargetIdx].Center) < r2;
				string lk = nm + (child ? " (child)" : " (use)");
				if (!s.Loc.TryGetValue(lk, out var la)) s.Loc[lk] = la = new int[4];
				la[aC && !aS ? 1 : aS ? 0 : aT ? 2 : 3]++;
			}
			var dict = child ? s.Child : s.Proj;
			dict[nm] = dict.TryGetValue(nm, out int c) ? c + 1 : 1;
			s.Spawned++;
		}

		internal static void NoteItem(Item it)
		{
			var s = _sc; if (s == null || _stage != 1) return;
			try
			{
				string nm0 = it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : (ItemID.Search.TryGetName(it.type, out string vn0) ? vn0 : "item" + it.type);
				Vector2 ic = it.Center; var sol0 = s.Sols.FirstOrDefault(x => x.Body != null && x.Body.NPC.active);
				bool aS = sol0 != null && Vector2.Distance(ic, sol0.Body.NPC.Center) < 80f, aC = Vector2.Distance(ic, Main.player[0].Center) < 80f;
				bool aT = s.TargetIdx >= 0 && Main.npc[s.TargetIdx].active && Vector2.Distance(ic, Main.npc[s.TargetIdx].Center) < 80f;
				string lk = "item:" + nm0; if (!s.Loc.TryGetValue(lk, out var la)) s.Loc[lk] = la = new int[4];
				la[aC && !aS ? 1 : aS ? 0 : aT ? 2 : 3]++;
			}
			catch (Exception) { }
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
			+ "\tms_useTotal/tick\tms_useItemCheck/tick\tms_useOverhead/tick\tms_projOrig/tick\tms_projSwapOverhead/tick\tms_prep/tick\tprojUpdates/tick\tms_wholeTick\tus_swapPerProj\tnetClears\tnetThreadPassesInFrame\tkillTrace\tflagsOn\tcmdFlagsOn\tdummyLifeGain\ttargetBuffs\tspawnLoc\thooksProj(cmd/other)\thooksMelee(cmd/other)\thooksHurt(cmd/other)\tms_subset/tick\tms_refresh/call\tsubsetCalls\tstealth(start/max/peak)\tstealthStrikeProjs/projs\thurtCalls\tgearNote\tdummyThorium(start>end)\tsubsetBreakdown(ms/tick: reset|buffs|equips|armorSets|postEquips|postMisc|postUpdate)";

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
				s.Kind, s.ItemKey, name, s.Fam, s.N.ToString(), s.Tick.ToString(), string.Join(",", s.Flags.OrderBy(x => x)) + (s.GearKeys.Count > 0 ? ",g=" + string.Join("+", s.GearKeys) : "") + (s.CmdGearKeys.Count > 0 ? ",cg=" + string.Join("+", s.CmdGearKeys) : ""), s.Note == "" ? "-" : s.Note,
				$"{s.Alive0}>{s.AliveEnd}", s.Sols.Sum(x => x.Uses).ToString(), s.Sols.Sum(x => x.UseExc).ToString(), Fmt(s.Proj), Fmt(s.Child), s.MaxLive.ToString(), s.MinionsEnd.ToString(),
				s.Dmg.ToString(), s.DmgA.ToString(), s.DmgB.ToString(), s.HitTicks.ToString(),
				s.DistSamples > 0 ? (s.SumDistSol / s.DistSamples).ToString("0.0") : "-", s.DistSamples > 0 ? (s.SumDistCmd / s.DistSamples).ToString("0.0") : "-", s.DistTgtSamples > 0 ? (s.SumDistTgt / s.DistTgtSamples).ToString("0.0") : "-",
				s.DummyEnd == "" ? "-" : s.DummyEnd, s.BuffsEnd == "" ? "-" : s.BuffsEnd, s.CmdDiff == "" ? "-" : s.CmdDiff, s.CmdManaMin == int.MaxValue ? "-" : s.CmdManaMin.ToString(), s.CmdLifeMin == int.MaxValue ? "-" : s.CmdLifeMin.ToString(), s.CmdLifeGain.ToString(),
				Fmt(s.Items), string.Join(";", s.OwnerHist.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value)), s.OrphanKills.ToString(), s.BossDeadTick.ToString(),
				(s.Exc.Count == 0 ? "-" : string.Join(" || ", s.Exc.Take(6))) + (s.PlayInstrNre > 0 ? " [benign server-only NRE (PlayInstruments/Achievements) x" + s.PlayInstrNre + "]" : "") + (s.DummyDeaths > 0 ? " [DUMMY KillMe SUPPRESSED x" + s.DummyDeaths + "]" : "") + (s.TpToCmd + s.TpToSelf + s.TpOther > 0 ? $" [GetThoriumPlayer(dummy) during use resolved: commander={s.TpToCmd} self={s.TpToSelf} other={s.TpOther}]" : ""),
				s.ChildAtSol.ToString(), s.ChildAtCmd.ToString(), s.ChildAtTgt.ToString(), s.ChildOther.ToString(), s.CmdProcs.ToString(), s.OtherProcs.ToString(),
				F(useTot), F(useIC), F(useOv), F(projOrig), F(projOv), F(prep), F((double)s.ProjUpdates / tk), F(whole), F(usPerProj),
					s.NetClears.ToString(), s.NetThreadPasses.ToString(),
					s.KillTrace.Count == 0 ? "-" : string.Join(" ## ", s.KillTrace.OrderByDescending(k => k.Value).Select(k => k.Value + "x " + k.Key + " => " + (s.KillSample.TryGetValue(k.Key, out var smp) ? smp : ""))),
					s.FlagsOn == "" ? "-" : s.FlagsOn, s.CmdFlagsOn == "" ? "-" : s.CmdFlagsOn, s.DummyLifeGain.ToString(), s.TargetBuffs.Count == 0 ? "-" : string.Join(",", s.TargetBuffs.OrderBy(b => b).Select(b => BuffLoader.GetBuff(b)?.Name ?? (BuffID.Search.TryGetName(b, out string bn) ? bn : "buff" + b))),
					s.Loc.Count == 0 ? "-" : string.Join(";", s.Loc.OrderBy(k => k.Key).Select(k => k.Key + " S" + k.Value[0] + " C" + k.Value[1] + " T" + k.Value[2] + " O" + k.Value[3])),
					s.HookProjCmd + "/" + s.HookProjOther, s.HookItemCmd + "/" + s.HookItemOther, s.HookHurtCmd + "/" + s.HookHurtOther, F(Ms(s.SubsetTs, tk)), F(s.RefreshCalls > 0 ? s.RefreshTs * 1000.0 / Stopwatch.Frequency / s.RefreshCalls : 0),
					s.SubsetCalls.ToString(), s.StealthStart.ToString("0.00") + "/" + s.StealthMax.ToString("0.00") + "/" + s.StealthPeak.ToString("0.00"), s.StealthProjs.Count + "/" + s.ProjsTotal, s.HurtCalls.ToString(), s.GearNote == "" ? "-" : s.GearNote, s.DummyTp0 + " > " + s.DummyTp1, string.Join("|", Enumerable.Range(0, 7).Select(i => F(Ms(s.SubBreak[i], tk)))),
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
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { if (OwnerLab.Active) OwnerLab.NoteHook(1, Player.whoAmI); }
		public override void OnHurt(Player.HurtInfo info) { if (OwnerLab.Active) OwnerLab.NoteHook(3, Player.whoAmI); }
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
			On_Projectile.Kill += KillTraceDetour;
			On_Netplay.UpdateConnectedClients += NetDetour;
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
			if (OwnerLab.Active) { try { On_Projectile.Update -= UpdateDetour; On_Projectile.Kill -= KillTraceDetour; On_Netplay.UpdateConnectedClients -= NetDetour; On_NPC.StrikeNPC_HitInfo_bool_bool -= StrikeDetour; On_Player.KillMe -= KillMeDetour; On_Player.ItemCheck_PlayInstruments -= PlayInstrDetour; On_AchievementsHelper.HandleSpecialEvent -= AchDetour; } catch { } }
			OwnerLab.Active = false;
		}

		public override void PreUpdateProjectiles() { if (OwnerLab.Active) { EnemyTargeting.EnsureSlotRestored(); OwnerLab.PrepareTick(); } }
		public override void PostUpdateProjectiles() { if (OwnerLab.Active) EnemyTargeting.EnsureSlotRestored(); }

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (OwnerLab.InScenario && self.TryGetGlobalProjectile(out LabTag tag) && tag.Soldier >= 0) OwnerLab.RunUpdate(orig, self, i, tag);
			else orig(self, i);
		}

		/// <summary>The dedicated server's Netplay.ServerLoop thread clears Main.player[i].active for every client-less slot i &lt; 255, continuously. A soldier Dummy swapped into such a slot is deactivated mid-frame, and vanilla Projectile.Update then kills any projectile whose owner is inactive.</summary>
		private static void NetDetour(On_Netplay.orig_UpdateConnectedClients orig)
		{
			if (OwnerLab.Active && OwnerLab.InScenario)
			{
				if (OwnerLab.FrameDepth > 0) OwnerLab.NoteNetPass();
				if (OwnerLab.Flag("netlock")) { lock (OwnerLab.NetLock) orig(); return; }
			}
			orig();
		}

		private static void KillTraceDetour(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (OwnerLab.InScenario && self.active && self.TryGetGlobalProjectile(out LabTag tag) && tag.Soldier >= 0) OwnerLab.RunKill(orig, self, tag);
			else orig(self);
		}

		private static int StrikeDetour(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction) =>
			OwnerLab.InScenario && OwnerLab.FrameBody != null ? OwnerLab.RunStrike(orig, self, hit, fromNet, noPlayerInteraction) : orig(self, hit, fromNet, noPlayerInteraction);
	}
}
