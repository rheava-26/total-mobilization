using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Observe;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.Items.Observe;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- MIND CONTROL audit: a scripted input into a mind-controlled levy against a real Player given the same input
		// Each movement scenario runs twice with the same key script and the same gear: once on the stand-in commander through the real Player.Update (the reference), once on a levy the stand-in drives through the Mind Link
		// (MindControl.SetInput each tick, the world stepped). Compared: top speed, ticks to reach it, ticks to stop, turn-around, jump apex (tap and held), double jump, wings, dash, platform drop, facing, and the ticks between a key and its effect.
		// The attack audit puts each weapon in a levy's hand, holds the attack button at the cursor (an empty spot, then on an enemy) and counts projectiles and damage against the same levy fighting on its own.
		// The lifecycle audit covers: the AI not moving a driven levy, release, the link ending when the levy falls, the next levy handed over, hotbar switching and the MindInput packet.
		// Env: LEVY_MIND=0 skips it, LEVY_MIND_LEGACY=1 runs the old goal-steered drive (the "before" numbers; checks are then only logged), LEVY_MIND_WEAPONS=all (every Calamity weapon; default a fixed list plus a sample).

		private static bool MindLegacy => Environment.GetEnvironmentVariable("LEVY_MIND_LEGACY") == "1";
		private const int MindPlatRow = FloorY - 7;

		private sealed class MTrace
		{
			public readonly List<float> X = new(), Y = new(), Vx = new(), Vy = new(); public readonly List<int> Dir = new();
			/// <summary>World spawns removed while this trace ran, and the hit points the driven body lost: a trace that took either is not a clean reference for the movement it measures.</summary>
			public int Spawns; public int HpLost; public string Info = ""; /// <summary>The first tick the body was no longer driven (the link dropped), or -1.</summary>
			public int LinkLost = -1;
			public void Add(float x, float y, float vx, float vy, int dir) { X.Add(x); Y.Add(y); Vx.Add(vx); Vy.Add(vy); Dir.Add(dir); }
			public float Top(int from, int to) { float m = 0f; for (int i = from; i < Math.Min(to, Vx.Count); i++) m = Math.Max(m, Math.Abs(Vx[i])); return m; }
			public int FirstAbove(float v, int from = 0) { for (int i = from; i < Vx.Count; i++) if (Math.Abs(Vx[i]) >= v) return i; return -1; }
			public int StopAfter(int release) { for (int i = release; i < Vx.Count; i++) if (Math.Abs(Vx[i]) < 0.05f) return i - release; return -1; }
			public float Apex(int from, int to, float baseY) { float m = 0f; for (int i = from; i < Math.Min(to, Y.Count); i++) m = Math.Max(m, baseY - Y[i]); return m; }
			public int Airborne(int from) { int n = 0; for (int i = from; i < Y.Count; i++) { if (Y[i] < -0.5f) n++; else if (n > 0) break; } return n; }
			public string Sample(params int[] at) => string.Join(" ", at.Where(i => i < Vx.Count).Select(i => $"v@{i}={Vx[i]:0.00}"));
		}

		/// <summary>Added to the seed of a weapon sample (the weapon audit's re-samples); 0 otherwise.</summary>
		private static int _mindSeedBump;
		private static bool _mindRefOn; private static int _mindKillMe; private static string _mindKillWhy;
		private static void MindKillMeDetour(On_Player.orig_KillMe orig, Player self, Terraria.DataStructures.PlayerDeathReason reason, double dmg, int dir, bool pvp)
		{
			if (!ReferenceEquals(self, Main.player[0]) || !_mindRefOn) { orig(self, reason, dmg, dir, pvp); return; }
			_mindKillMe++; _mindKillWhy = $"{reason?.GetDeathText(self.name)} dmg={dmg} life={self.statLife}/{self.statLifeMax2} pos={self.position}";
			self.statLife = Math.Max(1, self.statLifeMax2); self.dead = false;
		}

		private static void MindPlatform(bool on)
		{
			for (int x = Cx - 12; x <= Cx + 12; x++)
			{
				Tile t = Main.tile[x, MindPlatRow]; t.ClearEverything();
				if (on) { t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameY = 0; }
			}
		}

		private static void MindGiveLink()
		{
			Player c = Main.player[0];
			if (c.inventory[9] == null || c.inventory[9].type != ModContent.ItemType<MindLinkItem>()) { c.inventory[9] = new Item(); c.inventory[9].SetDefaults(ModContent.ItemType<MindLinkItem>()); }
		}

		private static SoldierRecord MindRecord(Item weapon, int ammoType, Item boots, Item wings, Item acc, Item sidearm = null)
		{
			var rec = MakeOriginRecord(weapon ?? new Item(), ammoType);
			rec.Name = "Pvt. Mind"; rec.Order = SoldierOrder.Hold;
			if (sidearm != null) { rec.Gear.Set(SoldierGear.SlotSidearm, sidearm); rec.Gear.SetLocked(SoldierGear.SlotSidearm, true); }
			foreach (Item it in new[] { boots, wings, acc })
			{
				if (it == null) continue;
				int s = AccessorySlotFor(rec.Gear, it); if (s >= 0) { rec.Gear.Set(s, it); rec.Gear.SetLocked(s, true); }
			}
			return rec;
		}

		private static Item MindItem(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		/// <summary>A levy stands on the floor (or the platform) with the given gear and the stand-in commander drives it through a Mind Link; <paramref name="inp"/> is the key state of each tick.</summary>
		private static MTrace MindLevyTrace(Item boots, Item wings, Item acc, int ticks, Func<int, MindInputState> inp, bool onPlatform, out SoldierRecord recOut, Action<int> each = null, Item weapon = null)
		{
			ClearSoldiers(); MindGiveLink();
			var tr = new MTrace();
			float floor = (onPlatform ? MindPlatRow : FloorY) * 16f;
			Vector2 at = new Vector2(Cx * 16f + 8f, floor);
			SpawnOriginTarget(at); _sweepTarget.active = false;
			SoldierRecord rec = MindRecord(weapon, 0, boots, wings, acc); recOut = rec;
			if (_mindHookGear != null) { rec.Gear.Set(SoldierGear.SlotHook, _mindHookGear.Clone()); rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
			NPC n = Main.npc[body]; n.Bottom = at; rec.HoldPos = n.Center;
			// the world's own spawns are removed from the first tick on: a slime that reaches the body before the script starts knocks it into the run (a 4.4 px/tick start on 11-18)
			KeepCommander(); for (int t = 0; t < 30; t++) { Step(1); MindClearSpawns(); }
			if (!MindRest(n)) Note($"  dbg mind trace: the body did not come to rest before the script (vx {n.velocity.X:0.00}, vy {n.velocity.Y:0.00})");
			if (!MindControl.Begin(Main.player[0], rec.Id, false)) throw new Exception("mind link refused");
			for (int t = 0; t < 3; t++) { MindControl.SetInput(0, K()); Step(1); MindClearSpawns(); }
			n.velocity = Vector2.Zero; n.Bottom = at;
			for (int t = 0; t < ticks; t++)
			{
				rec.Mana = rec.MaxMana;
				MindControl.SetInput(0, inp(t));
				each?.Invoke(t);
				Step(1); tr.Spawns += MindClearSpawns(); // nothing the world spawns may hit the body she drives
				if (!n.active) break;
				tr.HpLost = Math.Max(tr.HpLost, n.lifeMax - n.life);
				if (tr.LinkLost < 0 && !(n.ModNPC is SoldierNPC drv && drv.MindControlled)) tr.LinkLost = t;
				tr.Add(n.Center.X - at.X, n.Bottom.Y - floor, n.velocity.X, n.velocity.Y, n.direction);
				if (t == 149 && n.ModNPC is SoldierNPC fsn && fsn.Dummy != null) tr.Info = MindFingerprint(fsn.Dummy) + $" npc[vy {n.velocity.Y:0.000} collideY {n.collideY} wet {n.wet} knockBackResist {n.knockBackResist:0.00} buffs {string.Join("/", Enumerable.Range(0, NPC.maxBuffs).Where(i => n.buffType[i] > 0).Select(i => n.buffType[i]))}]";
			}
			if (Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1" && n.active) { Player dd = ((SoldierNPC)n.ModNPC).Dummy; Note($"  dbg levy dummy: maxRun={dd.maxRunSpeed} accRun={dd.accRunSpeed} accel={dd.runAcceleration} slow={dd.runSlowdown} moveSpeed={dd.moveSpeed} wings={dd.wingsLogic} wingMax={dd.wingTimeMax} dash={dd.dashType} jumpH={Player.jumpHeight} jumpS={Player.jumpSpeed} grav={dd.gravity} fall={dd.maxFallSpeed}"); }
			MindControl.End(0);
			return tr;
		}

		/// <summary>The same key script on the stand-in commander through the real Player.Update.</summary>
		private static MTrace MindPlayerTrace(Item boots, Item wings, Item acc, int ticks, Func<int, MindInputState> inp, bool onPlatform, Item weapon = null)
		{
			ClearSoldiers();
			var tr = new MTrace();
			float floor = (onPlatform ? MindPlatRow : FloorY) * 16f;
			Player p = Main.player[0];
			int savedMy = Main.myPlayer, savedSel = p.selectedItem; Vector2 savedPos = p.position;
			var savedArmor = new Item[p.armor.Length]; for (int i = 0; i < savedArmor.Length; i++) savedArmor[i] = p.armor[i];
			try
			{
				for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
				int ai = 3; foreach (Item it in new[] { boots, wings, acc }) if (it != null) p.armor[ai++] = it.Clone();
				p.active = true; p.dead = false; p.name = "IdentityTester"; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
				p.width = 20; p.height = 42; p.direction = 1; p.velocity = Vector2.Zero;
				p.position = new Vector2(Cx * 16f + 8f - p.width / 2f, floor - p.height); p.fallStart = p.fallStart2 = (int)(p.position.Y / 16f);
				Main.myPlayer = 255; _mindRefOn = true; _mindKillMe = 0; On_Player.KillMe += MindKillMeDetour; // not the local player: Player.Update then keeps the control fields it is given (a remote player on a server)
				for (int t = -30; t < ticks; t++)
				{
					MindInputState s = t < 0 ? default : inp(t);
					PlayerInput.Triggers.Current.Left = s.Left; PlayerInput.Triggers.Current.Right = s.Right; PlayerInput.Triggers.Current.Jump = s.Jump; PlayerInput.Triggers.Current.Down = s.Down;
					p.controlLeft = s.Left; p.controlRight = s.Right; p.controlJump = s.Jump; p.controlDown = s.Down; p.controlUp = false;
					p.statLifeMax = p.statLifeMax2 = 500; if (p.statLife < 500) p.statLife = 500; if (p.velocity.Y == 0f) p.fallStart = p.fallStart2 = (int)(p.position.Y / 16f);
					Vector2 pb = p.position;
					p.Update(0);
					if (Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1" && t >= 0 && t < 4 && s.Right) Note($"  dbg player t={t} right={p.controlRight} v={p.velocity} dpos={p.position - pb} active={p.active} dead={p.dead} ghost={p.ghost} frozen={p.frozen} noItems={p.noItems} CCed={p.CCed} maxRun={p.maxRunSpeed} acc={p.runAcceleration} dedServ={Main.dedServ} net={Main.netMode} gameMenu={Main.gameMenu} pause={Main.gamePaused}");
					p.active = true; p.dead = false;
					if (t >= 0) tr.Add(p.Center.X - (Cx * 16f + 8f), p.Bottom.Y - floor, p.velocity.X, p.velocity.Y, p.direction);
					if (t == 149) tr.Info = MindFingerprint(p);
				}
			}
			finally
			{
				if (Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1") Note($"  dbg player: maxRun={p.maxRunSpeed} accRun={p.accRunSpeed} accel={p.runAcceleration} slow={p.runSlowdown} moveSpeed={p.moveSpeed} wings={p.wingsLogic} wingMax={p.wingTimeMax} dash={p.dashType} jumpH={Player.jumpHeight} jumpS={Player.jumpSpeed} grav={p.gravity} fall={p.maxFallSpeed}");
				On_Player.KillMe -= MindKillMeDetour; _mindRefOn = false; if (_mindKillMe > 0) Note($"mind reference player was killed {_mindKillMe} time(s): {_mindKillWhy}");
				Main.myPlayer = savedMy; p.selectedItem = savedSel; for (int i = 0; i < savedArmor.Length; i++) p.armor[i] = savedArmor[i];
				PlayerInput.Triggers.Current.Left = PlayerInput.Triggers.Current.Right = PlayerInput.Triggers.Current.Jump = PlayerInput.Triggers.Current.Down = false;
				p.controlLeft = p.controlRight = p.controlJump = p.controlDown = false; p.position = savedPos; p.velocity = Vector2.Zero;
				p.fallStart = p.fallStart2 = (int)(p.position.Y / 16f); p.jump = 0; p.dashDelay = 0; p.dashTime = 0; p.wingTime = 0; p.immune = false;
			}
			return tr;
		}


		/// <summary>The state of a Player (the stand-in commander or a levy's dummy) that decides how it brakes: logged once at the tick before the key is released, so a run that stops late can be told from one that stops on time by what it carried.</summary>
		private static string MindFingerprint(Player p)
		{
			string buffs = string.Join("/", Enumerable.Range(0, Player.MaxBuffs).Where(i => p.buffType[i] > 0).Select(i => p.buffType[i]));
			return $"[vy {p.velocity.Y:0.000} maxRun {p.maxRunSpeed:0.00} accRun {p.accRunSpeed:0.00} accel {p.runAcceleration:0.000} slowdown {p.runSlowdown:0.000} moveSpeed {p.moveSpeed:0.00} slippy {p.slippy} slippy2 {p.slippy2} wet {p.wet} honey {p.honeyWet} lava {p.lavaWet} gravDir {p.gravDir:0} grav {p.gravity:0.00} sliding {p.sliding} mount {p.mount?.Active} frozen {p.frozen} webbed {p.webbed} stoned {p.stoned} slow {p.slow} chilled {p.chilled} dash {p.dashType}/{p.dashDelay} buffs {buffs} wind {Main.windSpeedCurrent:0.00} rain {Main.raining}]";
		}

		/// <summary>Why a stop-after-release check missed: the speed traces round the release, and what touched either body (world spawns removed, hit points lost). Logged only when the check fails.</summary>
		private static void MindDumpStop(string label, MTrace pl, MTrace lv)
		{
			string Vx(MTrace t) => string.Join(" ", Enumerable.Range(0, 12).Select(i => 148 + i * 4).Where(i => i < t.Vx.Count).Select(i => $"{t.Vx[i]:0.00}"));
			Note($"  dbg mind {label} stop miss: player vx@148,152.. [{Vx(pl)}] spawns {pl.Spawns} hp lost {pl.HpLost}; levy vx@148,152.. [{Vx(lv)}] spawns removed {lv.Spawns} hp lost {lv.HpLost} link dropped at tick {lv.LinkLost}; levy Y@150 {(lv.Y.Count > 150 ? lv.Y[150] : 0f):0.0} Vy@150..153 {string.Join(" ", Enumerable.Range(150, 4).Where(i => i < lv.Vy.Count).Select(i => $"{lv.Vy[i]:0.00}"))}");
		}

		private static void MindDumpAir(string label, MTrace lv)
		{
			int first = lv.Y.Select((y, i) => (y, i)).FirstOrDefault(a => a.y < -0.5f).i;
			Note($"  dbg mind {label} left the floor at tick {first}: Y {string.Join(" ", Enumerable.Range(Math.Max(0, first - 2), 8).Where(i => i < lv.Y.Count).Select(i => $"{lv.Y[i]:0.0}"))}; Vx {string.Join(" ", Enumerable.Range(Math.Max(0, first - 2), 8).Where(i => i < lv.Vx.Count).Select(i => $"{lv.Vx[i]:0.00}"))}; spawns removed {lv.Spawns} hp lost {lv.HpLost}");
		}
		/// <summary>
		/// Steps the world one tick at a time with the world's spawns removed after every tick (a slime that reaches a body knocks it into the run: seen as -0.52 px/tick on the very next tick of the MindInput packet check) until body <paramref name="n"/>
		/// has stood still for <paramref name="need"/> ticks in a row (|vx| under 0.01, no vertical speed), or <paramref name="cap"/> ticks pass. A body that cannot settle is a finding, not something to hide: false is returned and the caller says so.
		/// </summary>
		private static bool MindRest(NPC n, int need = 10, int cap = 240)
		{
			int still = 0;
			for (int t = 0; t < cap; t++)
			{
				Step(1); MindClearSpawns();
				if (!n.active) return false;
				if (Math.Abs(n.velocity.X) < 0.01f && Math.Abs(n.velocity.Y) < 0.01f) { if (++still >= need) return true; } else still = 0;
			}
			return false;
		}

		/// <summary>Spawns removed during the movement traces (the audit notes the count).</summary>
		private static int _mindSpawnsCleared;
		private static int MindClearSpawns()
		{
			int cleared = 0;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n != _sweepTarget && WorldStep.IsNaturalHostile(n)) { _mindSpawnsCleared++; cleared++; } }
			ClearNaturalHostiles();
			return cleared;
		}

		private static MindInputState K(bool l = false, bool r = false, bool j = false, bool d = false, bool use = false, Vector2 aim = default) => new MindInputState { Left = l, Right = r, Jump = j, Down = d, Use = use, Aim = aim };

		private static void MindMovementAudit(bool chk)
		{
			void Cmp(string name, string pl, string lv, bool ok) { Note($"mind {name}: player [{pl}] levy [{lv}]"); if (chk) Check($"mind control matches a player: {name} (player {pl}; levy {lv})", ok); }
			bool NearF(float a, float b, float rel, float abs) => Math.Abs(a - b) <= Math.Max(abs, rel * Math.Abs(b));
			bool NearI(int a, int b, int tol) => a >= 0 && b >= 0 && Math.Abs(a - b) <= tol;
			Item hermes = MindItem(ItemID.HermesBoots), wingsItem = MindItem(ItemID.AngelWings), cloud = MindItem(ItemID.CloudinaBottle), tabi = MindItem(ItemID.Tabi);
			SoldierRecord rec;

			// ---- run, stop, turn (no gear, then Hermes Boots) ----
			foreach (var (label, boots) in new[] { ("bare", (Item)null), ("hermes", hermes) })
			{
				Func<int, MindInputState> script = t => t < 150 ? K(r: true) : t < 240 ? K() : t < 330 ? K(l: true) : K();
				MTrace pl = MindPlayerTrace(boots, null, null, 360, script, false), lv = MindLevyTrace(boots, null, null, 360, script, false, out rec);
				float pTop = pl.Top(0, 150), lTop = lv.Top(0, 150);
				int p90 = pl.FirstAbove(pTop * 0.9f), l90 = lv.FirstAbove(lTop * 0.9f);
				int pStop = pl.StopAfter(150), lStop = lv.StopAfter(150);
				int pTurn = pl.Vx.Skip(240).Select((v, i) => (v, i)).FirstOrDefault(a => a.v < -0.05f).i, lTurn = lv.Vx.Skip(240).Select((v, i) => (v, i)).FirstOrDefault(a => a.v < -0.05f).i;
				Cmp($"{label} top speed", $"{pTop:0.00} px/tick", $"{lTop:0.00}", NearF(lTop, pTop, 0.05f, 0.1f));
				Cmp($"{label} ticks to 90% of top speed", $"{p90}", $"{l90}", NearI(l90, p90, Math.Max(4, p90 / 10)));
				Cmp($"{label} ticks to stop after releasing", $"{pStop}", $"{lStop}", NearI(lStop, pStop, 4));
				Note($"  dbg mind {label} stop state: player {pl.Info} levy {lv.Info}; vx around the release (ticks 148, 150, 152, ...): player [{string.Join(" ", Enumerable.Range(0, 12).Select(i => 148 + i * 2).Where(i => i < pl.Vx.Count).Select(i => pl.Vx[i].ToString("0.00")))}] levy [{string.Join(" ", Enumerable.Range(0, 12).Select(i => 148 + i * 2).Where(i => i < lv.Vx.Count).Select(i => lv.Vx[i].ToString("0.00")))}]");
				if (!NearI(lStop, pStop, 4)) MindDumpStop(label, pl, lv);
				if (Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1") { Note($"  dbg turn {label} player vx 236..256: " + string.Join(" ", Enumerable.Range(236, 20).Select(i => $"{pl.Vx[i]:0.00}"))); Note($"  dbg turn {label} levy  vx 236..256: " + string.Join(" ", Enumerable.Range(236, 20).Select(i => $"{lv.Vx[i]:0.00}"))); }
				Cmp($"{label} turning around: first tick moving the other way", $"{pTurn}", $"{lTurn}", NearI(lTurn, pTurn, 3));
				Cmp($"{label} speed curve", pl.Sample(5, 15, 30, 60, 120), lv.Sample(5, 15, 30, 60, 120), NearF(lv.Vx[30], pl.Vx[30], 0.1f, 0.15f) && NearF(lv.Vx[60], pl.Vx[60], 0.08f, 0.12f));
				Cmp($"{label} facing after the turn", $"{pl.Dir[300]}", $"{lv.Dir[300]}", pl.Dir[300] == lv.Dir[300] && pl.Dir[100] == lv.Dir[100]);
			}

			// ---- input latency: ticks from a key to its effect (stand still, then press) ----
			{
				Func<int, MindInputState> script = t => t < 20 ? K() : K(r: true);
				MTrace pl = MindPlayerTrace(null, null, null, 60, script, false), lv = MindLevyTrace(null, null, null, 60, script, false, out rec);
				int pf = pl.FirstAbove(0.01f, 20) - 20, lf = lv.FirstAbove(0.01f, 20) - 20;
				Cmp("run key latency (ticks until the body moves)", $"{pf}", $"{lf}", lf >= 0 && lf <= pf + 1);
				script = t => t < 20 ? K() : K(j: true);
				pl = MindPlayerTrace(null, null, null, 60, script, false); lv = MindLevyTrace(null, null, null, 60, script, false, out rec);
				int pj = pl.Vy.Select((v, i) => (v, i)).FirstOrDefault(a => a.v < -0.5f && a.i >= 20).i - 20, lj = lv.Vy.Select((v, i) => (v, i)).FirstOrDefault(a => a.v < -0.5f && a.i >= 20).i - 20;
				Cmp("jump key latency (ticks until the body rises)", $"{pj}", $"{lj}", lj >= 0 && lj <= pj + 1);
			}

			// ---- jump: a tap and a held key ----
			foreach (var (label, len) in new[] { ("tap", 2), ("held", 60) })
			{
				Func<int, MindInputState> script = t => t >= 10 && t < 10 + len ? K(j: true) : K();
				MTrace pl = MindPlayerTrace(null, null, null, 120, script, false), lv = MindLevyTrace(null, null, null, 120, script, false, out rec);
				float pa = pl.Apex(0, 120, 0f), la = lv.Apex(0, 120, 0f);
				if (Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1") { Note($"  dbg jump {label} player vy: " + string.Join(" ", Enumerable.Range(8, 24).Select(i => $"{pl.Vy[i]:0.00}/{-pl.Y[i]:0}"))); Note($"  dbg jump {label} levy  vy: " + string.Join(" ", Enumerable.Range(8, 24).Select(i => $"{lv.Vy[i]:0.00}/{-lv.Y[i]:0}"))); }
				int pair = pl.Airborne(10), lair = lv.Airborne(10);
				Cmp($"jump {label} apex", $"{pa:0.0} px", $"{la:0.0} px", NearF(la, pa, 0.08f, 3f));
				Cmp($"jump {label} airtime", $"{pair} ticks", $"{lair} ticks", NearI(lair, pair, 5));
			}

			// ---- double jump (Cloud in a Bottle): jump, then press again near the top ----
			{
				Func<int, MindInputState> script = t => (t >= 10 && t < 12) || (t >= 22 && t < 24) ? K(j: true) : K();
				MTrace pl = MindPlayerTrace(null, null, cloud, 160, script, false), lv = MindLevyTrace(null, null, cloud, 160, script, false, out rec);
				float pa = pl.Apex(0, 160, 0f), la = lv.Apex(0, 160, 0f);
				Cmp("double jump (Cloud in a Bottle) apex", $"{pa:0.0} px", $"{la:0.0} px", NearF(la, pa, 0.1f, 4f));
			}

			// ---- wings: jump and keep holding ----
			{
				Func<int, MindInputState> script = t => t >= 10 && t < 200 ? K(r: t > 12, j: true) : K();
				MTrace pl = MindPlayerTrace(null, wingsItem, null, 260, script, false), lv = MindLevyTrace(null, wingsItem, null, 260, script, false, out rec);
				// a Player that is not the local one is not charged for flight time on a server, so the reference cannot give a climb; its sideways speed in flight is the reference, the climb is checked against the wings' own tank (100 ticks)
				float la = lv.Apex(0, 260, 0f);
				float pTop = pl.Top(12, 100), lTop = lv.Top(12, 100);
				int airTicks = lv.Airborne(10);
				Cmp("wings (Angel Wings) holding jump and right: top sideways speed in flight", $"{pTop:0.00} px/tick", $"{lTop:0.00}", NearF(lTop, pTop, 0.08f, 0.25f));
				Cmp("wings (Angel Wings) holding jump: the climb lasts the tank (100 ticks) and rises", "tank 100 ticks", $"{la:0} px climbed, airborne {airTicks} ticks", la > 60f && airTicks > 100 && airTicks < 330);
			}

			// ---- dash (Tabi): double tap right ----
			{
				Func<int, MindInputState> script = t => (t >= 20 && t < 22) || (t >= 26 && t < 40) ? K(r: true) : K();
				MTrace pl = MindPlayerTrace(tabi, null, null, 80, script, false), lv = MindLevyTrace(null, null, tabi, 80, script, false, out rec);
				float pTop = pl.Top(0, 80), lTop = lv.Top(0, 80);
				// Calamity runs its own mid-dash speed handler inside the full player tick (CalamityPlayerDashEffects), which the dummy never runs: the levy's dash is vanilla's burst, so the check is that the double tap dashes well past the run speed
				Cmp("dash (double tap right with Tabi) peak speed, the levy must dash past Hermes-boot running speed (Calamity's own mid-dash handler needs the full player tick)", $"{pTop:0.00}", $"{lTop:0.00}", lTop > 8f);
			}

			// ---- platform: hold down on a platform ----
			MindPlatform(true);
			try
			{
				Func<int, MindInputState> script = t => t >= 10 && t < 40 ? K(d: true) : K();
				MTrace pl = MindPlayerTrace(null, null, null, 80, script, true), lv = MindLevyTrace(null, null, null, 80, script, true, out rec);
				float pDrop = pl.Y[79], lDrop = lv.Y[79];
				Cmp("platform drop-through (down held 30 ticks): feet below the platform after 80 ticks", $"{pDrop:0} px", $"{lDrop:0} px", pDrop > 40f && lDrop > 40f);
			}
			finally { MindPlatform(false); }
		}

		// ---------------------------------------------------------------------------------------------- attack
		/// <summary>Rounds the world made that a weapon run saw and did not count (by name, for the audit's note).</summary>
		private static readonly List<string> MindStrays = new();

		private static (int projs, long damage, int firstShotTick) MindWeaponRun(Item w, int ammo, bool onEnemy, int ticks, bool dead = false)
		{
			ClearSoldiers(); MindGiveLink(); _mindDead = dead; Main.player[0].dead = dead;
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at);
			Vector2 aim = _sweepTargetAt - new Vector2(0f, 24f);
			if (!onEnemy) { _sweepTarget.active = false; aim = at + new Vector2(20f * 16f, -24f); }
			SoldierRecord rec = MindRecord(w, ammo, null, null, null);
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
			Main.npc[body].Bottom = at; rec.HoldPos = Main.npc[body].Center;
			Main.rand = new Terraria.Utilities.UnifiedRandom(777 + _mindSeedBump);
			KeepCommander(); PinSweepTarget(); Step(20);
			if (!MindControl.Begin(Main.player[0], rec.Id, dead)) throw new Exception("mind link refused");
			int projs = 0, first = -1; long dmg = 0; var live = new Dictionary<int, (int type, int id)>();
			for (int t = 0; t < ticks; t++)
			{
				rec.Mana = rec.MaxMana;
				MindControl.SetInput(0, K(use: true, aim: aim));
				Step(1); ClearNaturalHostiles();
				if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) dmg += d; }
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.hostile) { live.Remove(i); continue; }
					if (live.TryGetValue(i, out var o) && o.type == p.type && o.id == p.identity) continue;
					live[i] = (p.type, p.identity);
					// a round the world made (a falling star from the top of the world) is not the levy's: it first appears more than 100 tiles above the cursor or 100 tiles to the side of both the levy and the cursor
					Vector2 lc = Main.npc[body].active ? Main.npc[body].Center : at;
					if ((aim.Y - p.Center.Y) / 16f > 100f || Math.Min(Math.Abs(p.Center.X - lc.X), Math.Abs(p.Center.X - aim.X)) / 16f > 100f) { MindStrays.Add(p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type)); continue; }
					projs++; if (first < 0) first = t;
				}
			}
			MindControl.End(0); ClearSoldiers(); _mindDead = false; Main.player[0].dead = false;
			return (projs, dmg, first);
		}

		private static void MindAttackAudit(bool chk, bool deadRun = false)
		{
			var ammoByKind = AmmoByKind();
			var list = new List<(int type, string label)>();
			foreach (int t in new[] { ItemID.Handgun, ItemID.WoodenBow, ItemID.RubyStaff, ItemID.CopperBroadsword, ItemID.Flamethrower, ItemID.SpaceGun, ItemID.WaterBolt })
				list.Add((t, "Terraria:" + ContentSamples.ItemsByType[t].Name));
			bool all = Environment.GetEnvironmentVariable("LEVY_MIND_WEAPONS") == "all";
			string listFile = Environment.GetEnvironmentVariable("LEVY_MIND_WEAPONS") is string lw && lw.StartsWith("list:") ? lw.Substring(5) : null;
			if (listFile != null)
			{
				// a weapon list (first column Mod:Name, as levy-mod/docs/ENDGAME_SWEEP.tsv): Calamity by internal name, vanilla by display name
				list.Clear();
				foreach (string line in System.IO.File.ReadAllLines(listFile).Skip(1))
				{
					string w = line.Split('\t')[0].Trim(); int c = w.IndexOf(':'); if (c <= 0) continue;
					string mod = w.Substring(0, c), name = w.Substring(c + 1); int type = 0;
					if (mod == "Terraria") { for (int i = 1; i < ItemID.Count; i++) if (Lang.GetItemNameValue(i) == name) { type = i; break; } }
					else if (ModContent.TryFind<ModItem>(mod + "/" + name, out var mi)) type = mi.Type;
					if (type > 0) list.Add((type, w)); else Note("mind attack audit: no such weapon " + w);
				}
			}
			else if (ModLoader.TryGetMod("CalamityMod", out Mod _))
			{
				var cal = OriginWeapons();
				var fixedNames = new[] { "StarShower", "ArcNovaDiffuser", "Tumbleweed", "Greentide", "Vesuvius", "Murasama", "Annihilator", "SpectralstormCannon", "RainbowPartyCannon" };
				foreach (var w in cal) if (fixedNames.Any(n => w.label.EndsWith(":" + n))) list.Add((w.type, w.label));
				int k = 0; foreach (var w in cal) { if (list.Any(x => x.type == w.type)) continue; if (all || k++ % 24 == 0) list.Add((w.type, w.label)); }
			}
			int ok = 0, bad = 0; var failed = new List<string>(); var sb = new StringBuilder();
			foreach (var (type, label) in list)
			{
				try
				{
					Item sample = ContentSamples.ItemsByType[type];
					int ammo = sample.useAmmo > 0 && ammoByKind.TryGetValue(sample.useAmmo, out int a) ? a : 0;
					if (Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1") Note($"  dbg mind attack: {label} ({(deadRun ? "dead" : "alive")})");
					Item w1 = MindItem(type), w2 = MindItem(type);
					// a weapon whose own use gate is shut in this world (Murasama before the Devourer of Gods: its CanUseItem) is used by nobody, a player included: there is nothing to compare
					if (global::Levy.Common.Weapons.ModWeaponGates.Locked(w1)) { sb.Append($"\n    {label}: skipped (its own use gate is shut in this world: a player cannot use it either)"); ok++; continue; }
					MindStrays.Clear();
					int wt = int.TryParse(Environment.GetEnvironmentVariable("LEVY_MIND_TICKS"), out int wtv) ? wtv : 90;
					var empty = MindWeaponRun(w1, ammo, false, wt, deadRun);
					var onEnemy = MindWeaponRun(w2, ammo, true, wt, deadRun);
					OResult ai = OriginOne(type, ammo, label, wt);
					if (sample.sentry || sample.mana >= 200) { sb.Append($"\n    {label}: skipped (a sentry, or {sample.mana} mana a cast, which a levy's mana pool does not hold)"); ok++; continue; }
					bool fires = empty.projs > 0 || onEnemy.projs > 0;
					int aiProjs = ai.Projs - Math.Max(0, ai.Far - ai.FarAbove); // the levy alone: rounds that first appeared high over the enemy outside the sky-rain window are the world's (a falling star), not his
					bool aiFires = aiProjs > 0;
					// a weapon that works for the levy on its own must work when she drives it: it shoots at an empty cursor spot, and on an enemy it does at least half the damage the levy deals by itself
					bool Judge(int emptyProjs, bool firesAny, long drivenDmg, long aloneDmg) => (sample.shoot <= 0 || firesAny) && ((emptyProjs > 0 || sample.shoot <= 0) && (aloneDmg == 0 || drivenDmg * 2 >= aloneDmg));
					bool good = !aiFires || Judge(empty.projs, fires, onEnemy.damage, ai.Damage);
					// A weapon with random output (Flurrystorm Cannon throws 5 to 13 rounds from the same hold) can miss the half-the-damage bar on one 90-tick sample by chance alone. The verdict then moves to more samples on other seeds:
					// the damage of every sample is summed on both sides and the same bar applied to the sums, so a weapon that really does less when driven still fails, and one that was only unlucky does not.
					int extra = 0;
					if (!good)
					{
						int ep = empty.projs; bool fa = fires; long dd = onEnemy.damage, ad = ai.Damage;
						for (int r = 1; r <= 3 && !good; r++)
						{
							_mindSeedBump = 101 * r;
							try
							{
								var e2 = MindWeaponRun(MindItem(type), ammo, false, wt, deadRun); var o2 = MindWeaponRun(MindItem(type), ammo, true, wt, deadRun); OResult a2 = OriginOne(type, ammo, label, wt);
								ep += e2.projs; fa |= e2.projs > 0 || o2.projs > 0; dd += o2.damage; ad += a2.Damage; extra = r;
							}
							finally { _mindSeedBump = 0; }
							good = Judge(ep, fa, dd, ad);
						}
						if (extra > 0) sb.Append($"\n    {label}: the first sample missed the bar, so {extra} more sample(s) on other seeds were added: driven damage {dd} over {extra + 1} samples against {ad} for the levy alone ({(good ? "now within" : "still below")} half)");
					}
					if (good) ok++; else { bad++; failed.Add(label.Replace("CalamityMod:", "")); }
					sb.Append($"\n    {label}: empty cursor {empty.projs} shots (first at tick {empty.firstShotTick}), on an enemy {onEnemy.projs} shots dmg {onEnemy.damage}, levy alone {aiProjs} shots dmg {ai.Damage}{(good ? "" : $"  <-- BROKEN (levy alone fired: {string.Join(", ", ai.ByType.Select(kv => kv.Key + " x" + kv.Value.n))})")}{(MindStrays.Count > 0 ? $" (not counted, the world's: {string.Join(", ", MindStrays)})" : "")}{(aiProjs != ai.Projs ? $" (not counted for the levy alone: {ai.Projs - aiProjs} first seen far over the enemy)" : "")}");
				}
				catch (Exception e) { bad++; failed.Add(label + " threw " + e.GetType().Name); sb.Append($"\n    {label} threw {e.GetType().Name}: {e.Message}"); }
			}
			string who = deadRun ? "the commander DEAD (death link)" : "the commander alive (Mind Link item)";
			Note($"mind attack audit, {who} ({list.Count} weapons, attack button held 90 ticks at the cursor): works {ok}, broken {bad}:{sb}");
			if (chk) Check($"mind control fires every weapon a levy fires on its own, at the cursor, {who} ({ok} of {list.Count}; broken: {string.Join(", ", failed)})", bad == 0);
		}

		// ---------------------------------------------------------------------------------------------- lifecycle
		private static void MindLifecycleAudit(bool chk)
		{
			void C(string name, bool ok) { Note("mind " + name + ": " + (ok ? "ok" : "WRONG")); if (chk) Check("mind control: " + name, ok); }
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);

			// 1. the levy's own AI stays out of a driven body: commander far to the left on Follow, an enemy near, no keys held; four runs, the link opened at four different moments of the brain's think cycle
			{
				float worst = 0f; string runs = ""; int bodyLast = -1;
				foreach (int pre in new[] { 1, 4, 9, 14 })
				{
					ClearSoldiers(); MindGiveLink();
					SpawnOriginTarget(at); _sweepTarget.active = true;
					SoldierRecord rec = MindRecord(MindItem(ItemID.Handgun), ammoByKindFirst(ItemID.MusketBall), null, null, null); rec.Order = SoldierOrder.Follow;
					RosterSystem.Instance.Regulars.Add(rec);
					int body = RosterSystem.Instance.SpawnBody(rec, at); Main.npc[body].Bottom = at; bodyLast = body;
					KeepCommander(); PinSweepTarget(); Step(pre);
					MindControl.Begin(Main.player[0], rec.Id, false);
					for (int t = 0; t < 40; t++) { MindControl.SetInput(0, K()); Step(1); ClearNaturalHostiles(); }
					float x0 = Main.npc[body].Center.X, y0 = Main.npc[body].Center.Y; float maxDx = 0f;
					SoldierNPC sn0 = (SoldierNPC)Main.npc[body].ModNPC;
					for (int t = 0; t < 180; t++)
					{
						MindControl.SetInput(0, K(aim: at + new Vector2(0f, -200f))); Step(1); ClearNaturalHostiles(); maxDx = Math.Max(maxDx, Math.Abs(Main.npc[body].Center.X - x0));
						if ((Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1" || maxDx > 2f) && t % 6 == 0) Note($"  dbg idle pre={pre} t={t} dx={Main.npc[body].Center.X - x0:0.0} dy={Main.npc[body].Center.Y - y0:0.0} v={Main.npc[body].velocity} grounded={sn0.MindGrounded} state={sn0.State} mind={sn0.MindControlled} direct={sn0.MindDirect} noGravity={Main.npc[body].noGravity} hasGoal={sn0.HasMoveGoal} target={sn0.TargetIndex}");
					}
					worst = Math.Max(worst, maxDx); runs += $" {maxDx:0.0}";
					if (pre != 14) { MindControl.End(0); Step(2); }
				}
				C($"a driven levy with no keys held stays put with its commander 45 tiles away and an enemy in sight (moved{runs} px in 180 ticks, four runs)", worst < 2f);
				// 2. release: the body goes back to the brain and the player's keys are not left held
				MindControl.End(0); Step(2);
				C("released: the levy is no longer controlled and holds no key", !((SoldierNPC)Main.npc[bodyLast].ModNPC).MindControlled && !Main.npc[bodyLast].noGravity);
				ClearSoldiers();
			}

			// 3. the controlled levy falls: the link ends at once; with other levies and a dead player the next one is handed over; stray input never re-links
			{
				ClearSoldiers(); MindGiveLink(); SpawnOriginTarget(at); _sweepTarget.active = false;
				SoldierRecord a = MindRecord(MindItem(ItemID.CopperShortsword), 0, null, null, null), b = MindRecord(MindItem(ItemID.CopperShortsword), 0, null, null, null);
				a.Name = "Pvt. A"; b.Name = "Pvt. B";
				RosterSystem.Instance.Regulars.Add(a); RosterSystem.Instance.Regulars.Add(b);
				int ia = RosterSystem.Instance.SpawnBody(a, at), ib = RosterSystem.Instance.SpawnBody(b, at + new Vector2(80f, 0f));
				Main.npc[ia].Bottom = at; Main.npc[ib].Bottom = at + new Vector2(80f, 0f);
				KeepCommander(); Step(20);
				MindControl.Begin(Main.player[0], a.Id, false); Step(5);
				Main.npc[ia].life = 0; Main.npc[ia].checkDead(); Step(3);
				C("the link ends when the levy it drives falls (item link, no hand-over)", MindControl.LinkOfPlayer(0) == null);
				MindControl.SetInput(0, K(l: true, r: true, j: true)); Step(5);
				C("movement keys with no link never start one", MindControl.LinkOfPlayer(0) == null);
				// dead player: the next levy is handed over on purpose
				Player pl = Main.player[0]; _mindDead = true; pl.dead = true;
				MindControl.Begin(pl, b.Id, true); Step(3);
				SoldierRecord c = MindRecord(MindItem(ItemID.CopperShortsword), 0, null, null, null); c.Name = "Pvt. C"; RosterSystem.Instance.Regulars.Add(c);
				int ic = RosterSystem.Instance.SpawnBody(c, at + new Vector2(-80f, 0f)); Main.npc[ic].Bottom = at + new Vector2(-80f, 0f);
				Step(5);
				int bi = RosterSystem.Instance.FindById(b.Id).BodyIndex;
				Main.npc[bi].life = 0; Main.npc[bi].checkDead(); Step(4);
				var l = MindControl.LinkOfPlayer(0);
				C($"a dead player whose levy falls is handed the next one (now driving {(l == null ? "nobody" : "#" + l.SoldierId)}, expected {c.Id})", l != null && l.SoldierId == c.Id);
				_mindDead = false; pl.dead = false; Step(3);
				C("the death link ends at respawn", MindControl.LinkOfPlayer(0) == null);
				ClearSoldiers();
			}

			// 4. hotbar: key 2 puts the sidearm in the hand, key 1 the main weapon
			{
				ClearSoldiers(); MindGiveLink(); SpawnOriginTarget(at); _sweepTarget.active = false;
				SoldierRecord rec = MindRecord(MindItem(ItemID.Handgun), ammoByKindFirst(ItemID.MusketBall), null, null, null, MindItem(ItemID.CopperShortsword));
				RosterSystem.Instance.Regulars.Add(rec);
				int body = RosterSystem.Instance.SpawnBody(rec, at); Main.npc[body].Bottom = at;
				KeepCommander(); Step(20);
				MindControl.Begin(Main.player[0], rec.Id, false);
				SoldierNPC sn = Main.npc[body].ModNPC as SoldierNPC;
				MindControl.SetInput(0, new MindInputState { Slot = 2 }); Step(3);
				int held2 = sn.Weapon?.ItemType ?? -1;
				MindControl.SetInput(0, new MindInputState { Slot = 1 }); Step(3);
				int held1 = sn.Weapon?.ItemType ?? -1;
				C($"hotbar 2 puts the sidearm in hand ({held2}, Copper Shortsword is {ItemID.CopperShortsword}) and 1 the main weapon ({held1}, Handgun is {ItemID.Handgun})", held2 == ItemID.CopperShortsword && held1 == ItemID.Handgun);
				MindControl.End(0); ClearSoldiers();
			}

			// 5. the MindInput packet: applied on arrival, the next tick of the levy has it
			{
				ClearSoldiers(); MindGiveLink(); SpawnOriginTarget(at); _sweepTarget.active = false;
				SoldierRecord rec = MindRecord(MindItem(ItemID.CopperShortsword), 0, null, null, null);
				RosterSystem.Instance.Regulars.Add(rec);
				int body = RosterSystem.Instance.SpawnBody(rec, at); Main.npc[body].Bottom = at;
				KeepCommander(); Step(20); bool rested = MindRest(Main.npc[body]); MindControl.Begin(Main.player[0], rec.Id, false); for (int q = 0; q < 2; q++) { Step(1); MindClearSpawns(); }
				C($"the levy is at rest before the packet arrives (vx {Main.npc[body].velocity.X:0.00}, vy {Main.npc[body].velocity.Y:0.00})", rested && Math.Abs(Main.npc[body].velocity.X) < 0.01f);
				var m = typeof(MindControl).GetMethod("OnInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
				using (var ms = new System.IO.MemoryStream())
				{
					using (var w = new System.IO.BinaryWriter(ms, Encoding.UTF8, true)) { w.Write(new MindInputState { Right = true }.Pack()); w.Write(at.X + 500f); w.Write(at.Y); }
					ms.Position = 0; using var r = new System.IO.BinaryReader(ms); m.Invoke(null, new object[] { r, 0 });
				}
				float x0 = Main.npc[body].Center.X; Step(1);
				C($"a MindInput packet moves the levy on the very next tick (moved {Main.npc[body].velocity.X:0.00} px/tick)", Main.npc[body].velocity.X > 0.01f);
				MindControl.End(0); ClearSoldiers();
			}
		}

		private static int ammoByKindFirst(int ammoItem) => ammoItem;

		private static int _mindSyncSlot = -1, _mindSyncCount;
		private static void MindSendDataDetour(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, Terraria.Localization.NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
		{
			if (msgType == MessageID.SyncNPC && number == _mindSyncSlot) _mindSyncCount++;
			orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
		}

		/// <summary>How often the server sends the driven body to her client: a body that streams only now and then reaches a client late, and the camera of a client follows exactly that body.</summary>
		private static void MindSyncAudit(bool chk)
		{
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			ClearSoldiers(); MindGiveLink(); SpawnOriginTarget(at); _sweepTarget.active = false;
			SoldierRecord rec = MindRecord(MindItem(ItemID.CopperShortsword), 0, null, null, null);
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at); Main.npc[body].Bottom = at;
			KeepCommander(); Step(20); MindControl.Begin(Main.player[0], rec.Id, false); Step(3);
			_mindSyncSlot = body; _mindSyncCount = 0; On_NetMessage.SendData += MindSendDataDetour;
			try { for (int t = 0; t < 120; t++) { MindControl.SetInput(0, K(r: t < 60, l: t >= 60)); Step(1); ClearNaturalHostiles(); } }
			finally { On_NetMessage.SendData -= MindSendDataDetour; _mindSyncSlot = -1; }
			MindControl.End(0); ClearSoldiers();
			Note($"mind body syncs sent to her client while she runs it (120 ticks): {_mindSyncCount}");
			if (chk) Check($"mind control: the driven body is sent to her client every tick it moves ({_mindSyncCount} syncs in 120 ticks)", _mindSyncCount >= 110);
		}

		private static void MindSweep()
		{
			if (Environment.GetEnvironmentVariable("LEVY_MIND") == "0" || !Main.dedServ) return;
			bool legacy = MindLegacy, chk = !legacy;
			using IDisposable quietSpawns = WorldStep.Quiet(); // the audits below measure one body against a real Player: no natural spawn may reach it
			Note($"mind sweep: drive = {(legacy ? "LEGACY goal-steered" : "direct")}");
			SetupOriginArena(); KeepCommander(); Step(2);
			_mindSpawnsCleared = 0;
			// lab: LEVY_MIND_REPEAT=N runs the movement, air and lifecycle audits N times in one boot (a flake shows as a failure in some of the repeats)
			int reps = int.TryParse(Environment.GetEnvironmentVariable("LEVY_MIND_REPEAT"), out int rp) ? Math.Max(1, rp) : 1;
			for (int rep = 1; rep < reps; rep++)
			{
				Note($"mind repeat {rep}/{reps}");
				try { MindMovementAudit(chk); } catch (Exception e) { Check("mind movement audit threw " + e.GetType().Name + ": " + e.Message, false); }
				ClearSoldiers();
				try { MindAirAudit(chk); } catch (Exception e) { Check("mind air audit threw " + e.GetType().Name + ": " + e.Message, false); }
				ClearSoldiers();
				try { MindLifecycleAudit(chk); } catch (Exception e) { Check("mind lifecycle audit threw " + e.GetType().Name + ": " + e.Message, false); }
				ClearSoldiers();
			}
			try { MindMovementAudit(chk); } catch (Exception e) { Check("mind movement audit threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			Note($"mind movement audit: {_mindSpawnsCleared} world spawns removed while the driven bodies ran");
			ClearSoldiers();
			try { MindAirAudit(chk); } catch (Exception e) { Check("mind air audit threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			ClearSoldiers();
			if (Environment.GetEnvironmentVariable("LEVY_MIND_NOATTACK") == "1") { try { MindSyncAudit(chk); } catch (Exception e) { Check("mind sync audit threw " + e.Message, false); } ClearSoldiers(); try { MindLifecycleAudit(chk); } catch (Exception e) { Check("mind lifecycle audit threw " + e.Message, false); } ClearSoldiers(); return; } // lab: skip the weapon audits
			try { MindAttackAudit(chk); } catch (Exception e) { Check("mind attack audit threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			ClearSoldiers();
			try { MindAttackAudit(chk, true); } catch (Exception e) { Check("mind attack audit (dead commander) threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			_mindDead = false; Main.player[0].dead = false;
			ClearSoldiers();
			try { MindSyncAudit(chk); } catch (Exception e) { Check("mind sync audit threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			ClearSoldiers();
			try { MindLifecycleAudit(chk); } catch (Exception e) { Check("mind lifecycle audit threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			ClearSoldiers();
		}
	}
}
