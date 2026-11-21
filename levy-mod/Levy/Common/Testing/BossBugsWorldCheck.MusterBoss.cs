using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// MUSTER BOSS world check, logged as "[MusterBoss]" lines: the "Auto-muster for boss fights" setting (Eve, 2026-11-21: she farms bosses and does not want the militia called for them).
	/// The rule table first, then a real Barracks with auto-muster on, two housed townspeople and manpower: a boss beside the commander musters the militia with the setting on and never with it off; a Blood Moon musters
	/// them either way; with the setting off the militia a Blood Moon raised stand down when it ends, though the boss still lives. Runs after the lost-fight check (LEVY_MUSTERBOSS=0 skips it; LEVY_MUSTERBOSS_ONLY=1 runs it alone).
	/// </summary>
	public partial class BossBugsWorldCheck
	{
		private static bool _mbDone; private static int _mbPass, _mbFail;
		private const int MbBarracksId = 900141;

		private static void MbCheck(string name, bool ok, Func<string> why = null) { if (ok) _mbPass++; else _mbFail++; _mod.Logger.Info($"[MusterBoss] {(ok ? "PASS" : "FAIL")} {name}{(!ok && why != null ? " :: " + why() : "")}"); }

		public static void MusterBossRunIfArmed()
		{
			if (!_armed || _mbDone || !Main.dedServ || Environment.GetEnvironmentVariable("LEVY_MUSTERBOSS") == "0") return;
			_mbDone = true; _running = true; _mbPass = _mbFail = 0;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time; bool blood = Main.bloodMoon;
			var cfg = global::Levy.Common.Config.LevyConfig.Get; bool saved = cfg?.AutoMusterBosses ?? true;
			BarracksTE bar = null; NPC g1 = null, g2 = null;
			try
			{
				global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Main.player[0] = Main.player[0] ?? new Player();
				// 1. the rule
				bool ok = true; string bad = "";
				foreach (bool ev in new[] { false, true }) foreach (bool boss in new[] { false, true }) foreach (bool on in new[] { false, true })
				{
					bool want = ev || (boss && on);
					if (SettlementSystem.AutoMusterAnswers(ev, boss, on) != want) { ok = false; bad += $" event={ev} boss={boss} setting={on}"; }
				}
				MbCheck("the rule: an event always musters, a boss fight only with the setting on", ok, () => bad);
				MbCheck("the setting is on by default", new global::Levy.Common.Config.LevyConfig().AutoMusterBosses || DefaultOn());
				if (cfg == null) { MbCheck("the config is loaded", false); return; }

				// 2. a real Barracks
				Arena(FloorY, Half); PlaceCommander(0); Main.bloodMoon = false;
				bar = TestTileEntities.Add<BarracksTE>(MbBarracksId, new Point16(Cx - 30, FloorY - 3)); bar.TownName = "MusterBossTown"; bar.AutoMuster = true; bar.Manpower = 20; bar.Mobilized = false;
				g1 = Townsperson(bar, -4); g2 = Townsperson(bar, 4);
				Step(5);
				MbCheck("the town can raise militia (two housed townspeople, manpower)", SettlementSystem.Instance.CanRaiseMilitia(bar), () => $"townspeople {SettlementSystem.Instance.TownNpcsOf(bar).Count}, manpower {bar.Manpower}");

				bool Trial(bool setting, bool bloodMoon, out string why)
				{
					cfg.AutoMusterBosses = setting; Main.bloodMoon = bloodMoon;
					NPC boss = SpawnNpc(NPCID.EyeofCthulhu, _cmdAt + new Vector2(0, -25 * 16f));
					bool mustered = false;
					for (int t = 0; t < 30 && !mustered; t++) { if (boss != null && boss.active) boss.timeLeft = 99999; Step(1); mustered = bar.Mobilized; }
					why = $"boss {(boss != null && boss.active ? "alive" : "gone")}, blood moon {Main.bloodMoon}, mobilized {bar.Mobilized}, militia {bar.Militia.Count}";
					return mustered;
				}
				void Reset()
				{
					ClearHostiles(); Main.bloodMoon = false;
					for (int t = 0; t < 10; t++) Step(1);
					if (bar.Mobilized) SettlementSystem.Instance.StandDown(bar);
					for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC s && s.IsMilitia) n.active = false; }
					bar.Militia.Clear(); bar.Mobilized = false; bar.Manpower = 20;
					Step(3);
				}

				bool m = Trial(true, false, out string w1);
				MbCheck("setting on: a boss beside the commander musters the militia", m, () => w1);
				Reset();
				MbCheck("the militia stood down after that boss", !bar.Mobilized);
				m = Trial(false, false, out string w2);
				MbCheck("setting off: the same boss never musters the militia", !m, () => w2);
				Reset();
				m = Trial(false, true, out string w3);
				MbCheck("setting off: a Blood Moon still musters the militia, with the boss alive", m, () => w3);
				// the Blood Moon ends while the boss lives: with the setting off nothing holds the militia out
				Main.bloodMoon = false; bool stood = false;
				for (int t = 0; t < 30 && !stood; t++) { for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].type == NPCID.EyeofCthulhu) Main.npc[i].timeLeft = 99999; Step(1); stood = !bar.Mobilized; }
				MbCheck("setting off: the militia stand down when the Blood Moon ends, the boss still alive", stood, () => $"mobilized {bar.Mobilized}, boss alive {NPC.AnyNPCs(NPCID.EyeofCthulhu)}");
				Reset();
				m = Trial(true, true, out string w4);
				MbCheck("setting on: a Blood Moon with a boss musters the militia", m, () => w4);
				Main.bloodMoon = false; bool held = true;
				for (int t = 0; t < 30; t++) { for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].type == NPCID.EyeofCthulhu) Main.npc[i].timeLeft = 99999; Step(1); if (!bar.Mobilized) { held = false; break; } }
				MbCheck("setting on: the boss holds the militia out after the Blood Moon ends", held, () => $"mobilized {bar.Mobilized}");
				Reset();
			}
			catch (Exception e) { MbCheck("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				if (cfg != null) cfg.AutoMusterBosses = saved;
				Main.bloodMoon = blood;
				if (g1 != null) g1.active = false; if (g2 != null) g2.active = false;
				if (bar != null) { if (bar.Mobilized) try { SettlementSystem.Instance.StandDown(bar); } catch (Exception) { } TestTileEntities.Remove(bar); }
				_local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server;
				Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time;
			}
			_mod.Logger.Info($"[MusterBoss] self-check pass={_mbPass} fail={_mbFail}");
		}

		private static bool DefaultOn()
		{
			var f = typeof(global::Levy.Common.Config.LevyConfig).GetField("AutoMusterBosses");
			var a = f == null ? null : (System.ComponentModel.DefaultValueAttribute)Attribute.GetCustomAttribute(f, typeof(System.ComponentModel.DefaultValueAttribute));
			return a?.Value is bool b && b;
		}

		/// <summary>A Guide housed at the barracks: a townsperson the settlement counts.</summary>
		private static NPC Townsperson(BarracksTE bar, int dx)
		{
			NPC n = SpawnNpc(NPCID.Guide, new Vector2((bar.Position.X + dx) * 16f, (_floorY - 3) * 16f));
			if (n == null) return null;
			n.homeless = false; n.homeTileX = bar.Position.X + dx; n.homeTileY = bar.Position.Y; n.immortal = true;
			return n;
		}
	}
}
