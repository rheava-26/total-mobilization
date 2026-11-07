using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using LC = global::Levy.Common.Config.LevyConfig;
using ET = global::Levy.Common.Targeting.EnemyTargeting;
using LND = global::Levy.Common.Globals.LevyNpcData;
using LU = global::Levy.Common.Util.LevyUtil;
using BDS = global::Levy.Common.Boss.BossDownedSystem;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using global::Levy.Common.Potions;
using global::Levy.Common.Soldiers;
using global::Levy.Common.Systems;
using Levy.Content.Buffs;
using Levy.Content.Items.Potions;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// SPECTATOR SCENARIO (LEVY_HARNESS_SCENARIO=spectator, LEVY_SPEC_CASES = comma list; default "boss:KingSlime"). Measurement only; "SPEC-" lines in the server log.
	/// The whole world update runs with Main.myPlayer = 0 (a single-player client), so the Spectator's tether / dash / teleport code (all local-player only) runs as in real play.
	/// Cases:
	///   boss:NAME[:flag...]  fresh world (no boss downed), the stand-in DRINKS a Spectator Potion through Player.ItemCheck (the real item use path), six levies on Free Rein, the boss is summoned with
	///                        NPC.SpawnOnPlayer (what every summon item calls). Logs the boss's target slot (0 = the player, 255 / 300+ = a levy) and its levy lock every 20 ticks.
	///                        NAME: vanilla NPC name (KingSlime, EyeofCthulhu, BrainofCthulhu) or Calamity internal NPC name (DesertScourgeHead, Crabulon).
	///                        flags: nopotion (control), death, rev (Calamity Revengeance/Death flags), sd (Search &amp; Destroy instead of Free Rein), cfg (EnemiesTargetSoldiers off, BossesTargetSoldiers off = shipped defaults, the default anyway), downed (mark the boss downed first)
	///   dash                 Shield of Cthulhu dash into a pinned target with and without the buff; damage dealt is logged.
	///   tele:KIND            KIND = short (Teleport 30 tiles, Rod of Discord style), long (Teleport 400 tiles, Teleportation Potion style), mirror (Magic Mirror through ItemCheck, recall to the world spawn), setpos (position set 400 tiles, what a mod teleport does)
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class SxCase { public string Spec, Kind, Arg; public HashSet<string> Flags = new(); }
		private readonly List<SxCase> _sx = new();
		private int _sxI = -1, _sxTick; private bool _sxStarted;
		private static bool _sxNight; private bool _sxServerUpdatesPlayer;
		private static bool _sxActive, _sxHooked, _sxSp = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_SPEC_SP"));
		private readonly List<SoldierNPC> _sxSol = new();
		private int _sxBoss = -1, _sxTarget = -1;
		private int _sxTickPlayer, _sxTickLevy, _sxTickOther, _sxFirstLevy = -1;
		private long _sxLife0; private string _sxLog;
		private Vector2 _sxStandPos;
		private int _sxDealt, _sxPotionTick;
		private Vector2 _sxTeleTo; private bool _sxTeleDone; private int _sxTeleAt; private float _sxMaxPullAfter; private Vector2 _sxPosAtTele;
		private bool _sxBuffAtTele, _sxBuffEnd; private Vector2 _sxAnchorAtTele;
		private int _sxBuffGoneTick = -1;

		private static void SxDoUpdate(On_Main.orig_DoUpdate orig, Main self, ref GameTime gt)
		{
			if (!_sxActive) { orig(self, ref gt); return; }
			int s = Main.myPlayer, nm = Main.netMode; Main.myPlayer = 0;
			if (_sxSp) Main.netMode = 0; // single-player emulation (LEVY_SPEC_SP=1): the world update runs the netMode 0 code paths
			try { orig(self, ref gt); } finally { Main.myPlayer = s; Main.netMode = nm; }
		}

		// leak probes: does any hostile AI end a call with the Spectator as its target, or ask Player.FindClosest and get the Spectator?
		private static int _sxCurAi = -1;
		internal static int SxLeakTargetEnd, SxLeakFindClosest, SxAiCalls, SxLeakTargetStart;
		private static readonly Dictionary<string, int> SxLeakByType = new();
		/// <summary>Runs the stand-in's Player.Update (the dedicated server never does; equips, dash input, the tether and teleports all live in it) as a local client would.</summary>
		private static void SxUpdatePlayer()
		{
			Player p = Main.player[0];
			int sm = Main.myPlayer; Main.myPlayer = 0;
			try { p.Update(0); } finally { Main.myPlayer = sm; }
		}
		private static bool SxSpec(int idx) => idx >= 0 && idx < Main.maxPlayers && SpectatorPlayer.IsSpectator(Main.player[idx]);
		private static void SxAiDetour(On_NPC.orig_AI orig, NPC self)
		{
			if (!_sxActive || self.friendly || self.townNPC || self.ModNPC is SoldierNPC) { orig(self); return; }
			int prev = _sxCurAi; _sxCurAi = self.whoAmI; SxAiCalls++;
			if (SxSpec(self.target)) SxLeakTargetStart++;
			try { orig(self); }
			finally
			{
				_sxCurAi = prev;
				if (SxSpec(self.target)) { SxLeakTargetEnd++; string k = self.FullName + (self.boss ? "(boss)" : ""); SxLeakByType[k] = SxLeakByType.GetValueOrDefault(k) + 1; }
			}
		}
		private static byte SxFindClosestDetour(On_Player.orig_FindClosest orig, Vector2 Position, int Width, int Height)
		{
			byte r = orig(Position, Width, Height);
			if (_sxActive && _sxCurAi >= 0 && SxSpec(r)) { SxLeakFindClosest++; string k = "FindClosest by " + Main.npc[_sxCurAi].FullName; SxLeakByType[k] = SxLeakByType.GetValueOrDefault(k) + 1; }
			return r;
		}
		private static string SxLeakReport()
		{
			var sb = new StringBuilder($"aiCalls={SxAiCalls} targetOnSpectatorAtAiStart={SxLeakTargetStart} targetOnSpectatorAtAiEnd={SxLeakTargetEnd} findClosestReturnedSpectator={SxLeakFindClosest}");
			foreach (var kv in SxLeakByType.OrderByDescending(k => k.Value).Take(12)) sb.Append($" [{kv.Key} x{kv.Value}]");
			SxAiCalls = SxLeakTargetStart = SxLeakTargetEnd = SxLeakFindClosest = 0; SxLeakByType.Clear();
			return sb.ToString();
		}

		private static void SxStandinDetour(OrigUpdateConnectedClients orig)
		{
			if (_sxActive) return; // no real clients: keep the stand-in commander
			orig();
		}

		private bool BeginSpectator()
		{
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", BindingFlags.Static | BindingFlags.NonPublic), new HookUpdateConnectedClients(SxStandinDetour)); }
			catch (Exception e) { Mod.Logger.Warn("SPEC: could not hook UpdateConnectedClients: " + e.Message); }
			try { On_Main.DoUpdate += SxDoUpdate; _sxHooked = true; }
			catch (Exception e) { Mod.Logger.Warn("SPEC: could not hook Main.DoUpdate: " + e.Message); }
			try { On_Player.ItemCheck_PlayInstruments += (orig, self, it) => { if (_sxActive && Main.dedServ) return; orig(self, it); }; On_AchievementsHelper.HandleSpecialEvent += (orig, pl, id) => { if (_sxActive && Main.dedServ) return; orig(pl, id); }; } catch (Exception e) { Mod.Logger.Warn("SPEC: could not hook the server-only NRE sites: " + e.Message); }
			try
			{
				On_NPC.AI += SxAiDetour;
				On_AchievementsHelper.HandleRunning += (orig, px) => { if (_sxActive && Main.dedServ) return; orig(px); };
				On_AchievementsHelper.HandleOnEquip += (orig, pl, it, ctx) => { if (_sxActive && Main.dedServ) return; orig(pl, it, ctx); };
				On_Player.UpdateBiomes += (orig, self) => { if (_sxActive && Main.dedServ) return; orig(self); };
				On_Player.ManageSpecialBiomeVisuals += (orig, self, name, inZone, src) => { if (_sxActive && Main.dedServ) return; orig(self, name, inZone, src); };
				On_Player.FindClosest += SxFindClosestDetour;
			}
			catch (Exception e) { Mod.Logger.Warn("SPEC: could not hook the leak probes: " + e.Message); }
			string spec = Environment.GetEnvironmentVariable("LEVY_SPEC_CASES");
			if (string.IsNullOrWhiteSpace(spec)) spec = "boss:KingSlime";
			foreach (string raw in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				var p = raw.Split(':', StringSplitOptions.TrimEntries);
				var c = new SxCase { Spec = raw, Kind = p[0], Arg = p.Length > 1 ? p[1] : "" };
				for (int i = 2; i < p.Length; i++) c.Flags.Add(p[i]);
				_sx.Add(c);
			}
			_sxActive = true; _sxI = 0; _sxStarted = false;
			Mod.Logger.Info($"SPEC: {_sx.Count} cases queued; DoUpdate hook={_sxHooked}");
			return true;
		}

		private void SxFinish()
		{
			_sxActive = false;
			Finish("spectator scenario complete");
		}

		private static string SxTargetName(NPC n)
		{
			if (n.target >= 300) return "npc" + (n.target - 300);
			if (n.target == SoldierNPC.DummySlot) return "levy255";
			return "player" + n.target;
		}

		private void SxKeepCommander(bool hurtable = false)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			if (!hurtable) { p.statLife = 500; }
		}

		private void SxPinCommander(Vector2 pos) { Player p = Main.player[0]; p.position = pos; p.velocity = Vector2.Zero; p.fallStart = (int)(p.position.Y / 16f); }

		private void SxClearHostiles(int keep = -1)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || i == keep) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				if (!n.friendly) n.active = false;
			}
		}

		private static Type SxCalType(string name) { return ModLoader.TryGetMod("CalamityMod", out var m) ? m.Code.GetType(name) : null; }

		private static void SxResetDowned()
		{
			try
			{
				var f = typeof(BDS).GetField("_downed", BindingFlags.NonPublic | BindingFlags.Static);
				((HashSet<string>)f.GetValue(null)).Clear();
			}
			catch (Exception e) { Instance?.Mod.Logger.Warn("SPEC: could not clear the levy downed set: " + e.Message); }
			NPC.downedSlimeKing = NPC.downedBoss1 = NPC.downedBoss2 = NPC.downedBoss3 = false;
			try
			{
				Type t = SxCalType("CalamityMod.Systems.DownedBossSystem");
				if (t != null) foreach (var fi in t.GetFields(BindingFlags.NonPublic | BindingFlags.Static)) if (fi.FieldType == typeof(bool) && fi.Name.StartsWith("_downed")) fi.SetValue(null, false);
			}
			catch (Exception) { }
		}

		private static void SxSetCalamityModes(bool death, bool rev)
		{
			Type t = SxCalType("CalamityMod.World.CalamityWorld");
			if (t == null) return;
			t.GetField("revenge", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, rev || death);
			t.GetField("death", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, death);
		}

		private static int SxBossType(string name)
		{
			if (ModContent.TryFind<ModNPC>("CalamityMod/" + name, out var m)) return m.Type;
			return NPCID.Search.ContainsName(name) ? NPCID.Search.GetId(name) : -1;
		}

		/// <summary>Drinks a Spectator Potion the way a player does: the item is in the hotbar and Player.ItemCheck runs with the use button held.</summary>
		private void SxDrink()
		{
			Player p = Main.player[0];
			p.inventory[0] = new Item(); p.inventory[0].SetDefaults(ModContent.ItemType<SpectatorPotion>()); p.inventory[0].stack = 1;
			p.selectedItem = 0; p.controlUseItem = true; p.releaseUseItem = true; p.itemAnimation = 0; p.itemTime = 0;
			int saved = Main.myPlayer; Main.myPlayer = 0;
			try { p.ItemCheck(); } finally { Main.myPlayer = saved; }
			p.controlUseItem = false;
		}

		private void SxSpawnLevies(SoldierOrder order, int n)
		{
			_sxSol.Clear();
			for (int i = 0; i < n; i++)
			{
				var rec = MakeRecord("Pvt. Spec" + i);
				rec.LoadoutLocked = true; rec.Order = order; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				var gear = rec.Gear;
				var w = new Item(ItemID.CopperShortsword);
				if (gear.CanHold(SoldierGear.SlotWeapon, w)) { gear.Weapon = w; gear.SetLocked(SoldierGear.SlotWeapon, true); }
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 at = new Vector2((_cx - 12 + i * 3) * 16f, FloorY * 16f);
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (idx < 0 || !(Main.npc[idx].ModNPC is SoldierNPC b)) { Mod.Logger.Warn("SPEC: SpawnBody failed"); continue; }
				b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; b.RefreshStats();
				_sxSol.Add(b);
			}
		}

		private void StepSpectator()
		{
			if (_sxI < 0 || _sxI >= _sx.Count) { SxFinish(); return; }
			SxCase c = _sx[_sxI];
			bool ok;
			try { ok = c.Kind switch { "boss" => SxBoss(c), "dash" => SxDash(c), "tele" => SxTele(c), _ => true }; }
			catch (Exception e) { Mod.Logger.Warn($"SPEC-{c.Spec}: threw {e}"); ok = true; }
			if (ok) { _sxI++; _sxStarted = false; _sxTick = 0; }
		}

		// ------------------------------------------------------------------------------------------------ boss targeting
		private const int SxBossTicks = 900;

		private bool SxBoss(SxCase c)
		{
			bool potion = !c.Flags.Contains("nopotion"), late = c.Flags.Contains("late"); // late: the boss is out and on the player first, the potion is drunk afterwards
			if (!_sxStarted)
			{
				ClearAll(); SpBuildFloor("run");
				SxClearHostiles(); SxResetDowned();
				Player p = Main.player[0];
				for (int b = 0; b < p.buffType.Length; b++) { p.buffType[b] = 0; p.buffTime[b] = 0; }
				SxKeepCommander();
				_sxStandPos = new Vector2(_cx * 16f + 8f - p.width / 2f, FloorY * 16f - p.height);
				SxPinCommander(_sxStandPos);
				Main.GameMode = c.Flags.Contains("master") ? 2 : c.Flags.Contains("expert") ? 1 : 0;
				SxSetCalamityModes(c.Flags.Contains("death"), c.Flags.Contains("rev"));
				LC.Get.EnemiesTargetSoldiers = !c.Flags.Contains("cfgE0"); LC.Get.BossesTargetSoldiers = c.Flags.Contains("cfgB1"); ET.RefreshConfig();
				SxSpawnLevies(c.Flags.Contains("sd") ? SoldierOrder.SearchDestroy : SoldierOrder.FreeRein, 6);
				if (c.Flags.Contains("downed")) { NPC.downedSlimeKing = NPC.downedBoss1 = NPC.downedBoss2 = true; }
				_sxBoss = -1; _sxTickPlayer = _sxTickLevy = _sxTickOther = 0; _sxFirstLevy = -1; _sxLog = ""; _sxPotionTick = -1; _sxStarted = true; _sxTick = 0;
				Mod.Logger.Info($"SPEC-{c.Spec}: BEGIN gameMode={Main.GameMode} expert={Main.expertMode} potion={potion} bossDownedNpcFlags={NPC.downedSlimeKing}/{NPC.downedBoss1}/{NPC.downedBoss2} levyDownedSetCleared; cfg Enemies={LC.Get.EnemiesTargetSoldiers} Bosses={LC.Get.BossesTargetSoldiers}");
				return false;
			}
			_sxTick++;
			SxKeepCommander(); SxPinCommander(_sxStandPos);
			Player pl = Main.player[0];
			if (potion && _sxPotionTick < 0 && _sxTick >= (late ? 220 : 20) && (!late || _sxBoss >= 0))
			{
				SxDrink();
				if (pl.HasBuff<SpectatorBuff>()) { _sxPotionTick = _sxTick; Mod.Logger.Info($"SPEC-{c.Spec}: potion drunk through ItemCheck at tick {_sxTick}; buff on; potion left in slot: {pl.inventory[0].stack}"); }
				else if (_sxTick > 100) { Mod.Logger.Warn($"SPEC-{c.Spec}: the potion was NOT drunk (buff absent) - harness problem"); _sxPotionTick = -2; }
			}
			bool ready = late || !potion || _sxPotionTick >= 0 || _sxPotionTick == -2;
			if (_sxBoss < 0 && ready && _sxTick >= 60)
			{
				int type = SxBossType(c.Arg);
				if (type < 0) { Mod.Logger.Warn($"SPEC-{c.Spec}: unknown boss '{c.Arg}'"); return true; }
				_sxNight = c.Arg == "EyeofCthulhu" || c.Arg == "BrainofCthulhu" || c.Flags.Contains("night"); Main.dayTime = !_sxNight; Main.time = _sxNight ? 16000 : 27000;
				NPC.SpawnOnPlayer(0, type);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].type == type) { _sxBoss = i; break; }
				if (_sxBoss < 0) { Mod.Logger.Warn($"SPEC-{c.Spec}: SpawnOnPlayer did not create the boss"); return true; }
				NPC bn = Main.npc[_sxBoss];
				_sxLife0 = bn.life;
				Mod.Logger.Info($"SPEC-{c.Spec}: boss spawned idx={_sxBoss} type={bn.type} boss={bn.boss} aiStyle={bn.aiStyle} life={bn.life} target={SxTargetName(bn)} allowlisted={ET.IsAllowlisted(bn)} bossUnlock={ET.BossAggroUnlocked(bn)} bodies={ET.Bodies.Count} spectatorAny={SpectatorPlayer.AnyActive} enabled={ET.Enabled}");
				_sxTick = late ? 100 : 100; // measurement ticks count from here
				return false;
			}
			if (_sxBoss < 0) return false;
			// the boss is out; keep any other hostile away, keep the boss alive
			// (no clearing here: the boss's own minions - creepers, servants, slimes - must live; the range is sealed and vanilla spawning is suppressed)
			NPC b2 = Main.npc[_sxBoss];
			if (!b2.active) { Mod.Logger.Info($"SPEC-{c.Spec}: boss gone/dead at measurement tick {_sxTick - 100}"); return SxBossReport(c, "boss inactive"); }
			if (b2.life < b2.lifeMax / 3) b2.life = b2.lifeMax / 2; // do not let it die in the middle of the measurement
			int t = b2.target;
			if ((_sxTick - 100) % 100 == 50)
			{
				int cr = 0, crOnPlayer = 0, crOnLevy = 0, crAllow = 0;
				for (int i = 0; i < Main.maxNPCs; i++) { NPC m = Main.npc[i]; if (!m.active || m.type != NPCID.Creeper) continue; cr++; if (ET.IsAllowlisted(m)) crAllow++; if (m.target == SoldierNPC.DummySlot || m.target >= 300) crOnLevy++; else if (m.target < 255) crOnPlayer++; }
				if (cr > 0) for (int i = 0; i < Main.maxNPCs; i++) { NPC m = Main.npc[i]; if (m.active && m.type == NPCID.Creeper) { Mod.Logger.Info($"SPEC-creeper0 aiStyle={m.aiStyle} boss={m.boss} friendly={m.friendly} realLife={m.realLife} countedAsBoss={NPCID.Sets.ShouldBeCountedAsBoss[m.type]} native={ET.UsesNativeTargeting(m)} root={LU.RootNpc(m).type} enabled={ET.Enabled}"); break; } }
				if (cr > 0) Mod.Logger.Info($"SPEC-{c.Spec}: creepers at t={_sxTick - 100}: n={cr} allowlisted={crAllow} onLevy={crOnLevy} onPlayer={crOnPlayer}");
			}
			if (c.Flags.Contains("dbg") && _sxTick - 100 < 520 && (_sxTick - 100) % 40 < 2) Mod.Logger.Info($"SPEC-dbg t={_sxTick - 100} target={SxTargetName(b2)} timeLeft={b2.timeLeft} despawnEnc={b2.despawnEncouraged} ai={b2.ai[0]},{b2.ai[1]},{b2.ai[2]},{b2.ai[3]} life={b2.life} pos={b2.Center / 16f} dayTime={Main.dayTime}");
			if (t >= 0 && t < 255 && t != 255) _sxTickPlayer++; else if (t == SoldierNPC.DummySlot || t >= 300) { _sxTickLevy++; if (_sxFirstLevy < 0) _sxFirstLevy = _sxTick - 100; } else _sxTickOther++;
			if ((_sxTick - 100) % 20 == 0)
			{
				var data = LND.Of(b2);
				float dc = Vector2.Distance(b2.Center, pl.Center) / 16f;
				float dl = _sxSol.Where(s => s.NPC.active).Select(s => Vector2.Distance(b2.Center, s.NPC.Center) / 16f).DefaultIfEmpty(-1f).Min();
				_sxLog += $"{_sxTick - 100}:{SxTargetName(b2)}/lock{data.SoldierTarget}/dC{dc:0}/dL{dl:0}  ";
			}
			if (_sxTick - 100 >= SxBossTicks)
			{
				return SxBossReport(c, "complete");
			}
			return false;
		}

		private bool SxBossReport(SxCase c, string why)
		{
			NPC b = _sxBoss >= 0 ? Main.npc[_sxBoss] : null;
			string lifeLost = b != null && b.active ? (_sxLife0 - b.life).ToString() : "n/a";
			Mod.Logger.Info($"SPEC-{c.Spec}: RESULT {why}: ticks target=player {_sxTickPlayer}, target=levy {_sxTickLevy}, other {_sxTickOther}; first levy-target tick={_sxFirstLevy}; boss life lost={lifeLost}; spectatorBuffEnd={Main.player[0].HasBuff<SpectatorBuff>()}");
			Mod.Logger.Info($"SPEC-{c.Spec}: LEAKS (whole case, incl. the potion-less lead-in) {SxLeakReport()}");
			Mod.Logger.Info($"SPEC-{c.Spec}: TRACE (tick:target/lock/distCmd tiles/distNearestLevy tiles) {_sxLog}");
			if (_sxBoss >= 0 && Main.npc[_sxBoss].active) Main.npc[_sxBoss].active = false;
			SxClearHostiles();
			return true;
		}

		// ------------------------------------------------------------------------------------------------ dash
		private bool SxDash(SxCase c)
		{
			// two runs: control (no buff) then spectator
			int phase = _sxTick < 400 ? 0 : 1;
			if (!_sxStarted)
			{
				ClearAll(); SpBuildFloor("run"); SxClearHostiles();
				_sxStarted = true; _sxTick = 0; _sxDealt = 0;
				return false;
			}
			_sxTick++;
			int sub = _sxTick % 400;
			if (_sxTick > 800) return true;
			Player p = Main.player[0];
			SxKeepCommander();
			if (sub == 1 || _sxTick == 401)
			{
				for (int b = 0; b < p.buffType.Length; b++) { p.buffType[b] = 0; p.buffTime[b] = 0; }
				for (int i = 3; i < 8; i++) p.armor[i] = new Item();
				p.armor[3] = new Item(ItemID.EoCShield);
				p.position = new Vector2((_cx - 20) * 16f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
				p.dashDelay = 0; p.eocDash = 0; p.eocHit = -1; p.dashTime = 0;
				SxClearHostiles();
				if (_sxTarget >= 0 && Main.npc[_sxTarget].active) Main.npc[_sxTarget].active = false;
				_sxTarget = SpSpawnTarget((_cx - 12) * 16f, 48, 48, 0, NPCID.Zombie);
				var tn = Main.npc[_sxTarget]; tn.friendly = false; tn.lifeMax = tn.life = 1_000_000; tn.defense = 0;
				_sxDealt = 0; _sxLife0 = tn.life;
				if (_sxTick >= 400) { p.inventory[0] = new Item(ModContent.ItemType<SpectatorPotion>()); }
				return false;
			}
			if (_sxTick > 400 && sub == 3 && !p.HasBuff<SpectatorBuff>()) SxDrink();
			if (sub == 6) Mod.Logger.Info($"SPEC-dash: phase {(_sxTick > 400 ? "SPECTATOR" : "control")} buff={p.HasBuff<SpectatorBuff>()} dash={p.dash} (2 = Shield of Cthulhu)");
			// the dash input (double-tap) is read from the keyboard on a local client and cannot be injected here, so the dash is started in the state vanilla leaves it in right after the trigger: eocDash running, full speed toward the target
			int s2 = sub - 10;
			if (s2 >= 0 && s2 < 40 && p.eocHit < 0) { p.eocDash = 10; p.velocity.X = 12f; p.controlRight = true; }
			SxUpdatePlayer();
			NPC tgt = Main.npc[_sxTarget];
			if (tgt.active) { tgt.velocity = Vector2.Zero; if (tgt.life < tgt.lifeMax) _sxDealt = (int)(_sxLife0 - tgt.life); }
			if (sub == 100)
				Mod.Logger.Info($"SPEC-dash: {(_sxTick > 400 ? "SPECTATOR" : "control")} dashed (playerX moved {(p.position.X / 16f - (_cx - 20)):0.0} tiles, eocDash/dashDelay now {p.eocDash}/{p.dashDelay}); damage dealt to the target = {_sxDealt}");
			return false;
		}

		// ------------------------------------------------------------------------------------------------ teleports
		private bool SxTele(SxCase c)
		{
			Player p = Main.player[0];
			if (!_sxStarted)
			{
				ClearAll(); SpBuildFloor("run"); SxClearHostiles(); SxResetDowned();
				for (int b = 0; b < p.buffType.Length; b++) { p.buffType[b] = 0; p.buffTime[b] = 0; }
				SxKeepCommander();
				_sxStandPos = new Vector2(_cx * 16f + 8f - p.width / 2f, FloorY * 16f - p.height);
				SxPinCommander(_sxStandPos);
				_sxStarted = true; _sxTick = 0; _sxPotionTick = -1; _sxTeleDone = false; _sxMaxPullAfter = 0f; _sxBuffGoneTick = -1;
				return false;
			}
			_sxTick++;
			SxKeepCommander();
			if (_sxTick < 20) { SxPinCommander(_sxStandPos); return false; }
			if (_sxPotionTick < 0)
			{
				SxDrink();
				if (p.HasBuff<SpectatorBuff>()) { _sxPotionTick = _sxTick; Mod.Logger.Info($"SPEC-{c.Spec}: potion drunk at tick {_sxTick}"); }
				else if (_sxTick > 100) { Mod.Logger.Warn($"SPEC-{c.Spec}: potion not drunk"); return true; }
				SxPinCommander(_sxStandPos);
				return false;
			}
			int since = _sxTick - _sxPotionTick;
			var sp = p.GetModPlayer<SpectatorPlayer>();
			if (since < 30) { SxPinCommander(_sxStandPos); return false; }
			if (!_sxTeleDone)
			{
				_sxTeleDone = true; _sxTeleAt = _sxTick;
				Vector2 from = p.position;
				switch (c.Arg)
				{
					case "short": _sxTeleTo = new Vector2(from.X + 30 * 16f, from.Y); p.Teleport(_sxTeleTo, 1); break;
					case "long": _sxTeleTo = new Vector2(from.X + 400 * 16f, from.Y - 10 * 16f); p.Teleport(_sxTeleTo, 2); break;
					case "setpos": _sxTeleTo = new Vector2(from.X + 400 * 16f, from.Y - 10 * 16f); p.position = _sxTeleTo; p.velocity = Vector2.Zero; break;
					case "mirror":
						Main.spawnTileX = Math.Min(Main.maxTilesX - 60, _cx + 300); Main.spawnTileY = FloorY - 4; p.SpawnX = -1; p.SpawnY = -1;
						_sxTeleTo = new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f);
						p.inventory[0] = new Item(ItemID.MagicMirror); p.selectedItem = 0;
						break;
				}
				_sxPosAtTele = p.position; _sxAnchorAtTele = sp.Anchor;
				Mod.Logger.Info($"SPEC-{c.Spec}: teleport issued at tick {_sxTick}: from {from / 16f} to {_sxTeleTo / 16f}; anchor before {sp.Anchor / 16f}");
				return false;
			}
			if (c.Arg == "mirror" && _sxTick - _sxTeleAt < 200 && p.HasBuff<SpectatorBuff>())
			{
				// hold the use button on the first tick, then let the item animation run (the recall fires at the end of it)
				p.controlUseItem = _sxTick - _sxTeleAt < 2; p.releaseUseItem = true;
				int saved = Main.myPlayer; Main.myPlayer = 0;
				try { p.ItemCheck(); } finally { Main.myPlayer = saved; }
			}
			if (_sxBuffGoneTick < 0 && !p.HasBuff<SpectatorBuff>()) { _sxBuffGoneTick = _sxTick; Mod.Logger.Info($"SPEC-{c.Spec}: Spectator buff ENDED at tick {_sxTick} ({_sxTick - _sxTeleAt} ticks after the teleport)"); }
			int after = _sxTick - _sxTeleAt;
			if (after == 5 || after == 60 || after == 180 || after == 360)
				Mod.Logger.Info($"SPEC-{c.Spec}: +{after}t: pos {p.Center / 16f}, vel {p.velocity}, buff={p.HasBuff<SpectatorBuff>()}, anchor {sp.Anchor / 16f}, distToAnchor={Vector2.Distance(p.Center, sp.Anchor) / 16f:0.0} tiles");
			if (_sxTick - _sxTeleAt >= 400)
			{
				float drift = Vector2.Distance(p.position, _sxPosAtTele) / 16f;
				Mod.Logger.Info($"SPEC-{c.Spec}: RESULT buffAtEnd={p.HasBuff<SpectatorBuff>()} anchorNow={sp.Anchor / 16f} pos={p.position / 16f} (teleport target {_sxTeleTo / 16f})");
				return true;
			}
			return false;
		}
	}
}
