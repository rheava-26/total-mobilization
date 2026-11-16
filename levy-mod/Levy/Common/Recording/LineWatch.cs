using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// "The line is breaking!": while a boss lives near the commander, watches the army twice a second and warns once when it is collapsing (see <see cref="LineBreakDetector"/>: a third of the line shaken, routed or deserting
	/// for three seconds, or four tenths of the army fallen within twenty seconds). The warning is combat text over the commander plus one chat line, at most once a minute whatever happens, once per fight.
	/// It is a message only: nothing here reads or changes a levy's morale, orders or stats, and it runs whether or not a recording does. A dedicated server and a remote client in multiplayer have no commander of their own to warn.
	/// </summary>
	public class LineWatch : ModSystem
	{
		public const string Text = "The line is breaking!";
		public const int EveryTicks = 30, QuietEndTicks = 600, RateLimitTicks = 3600, BossRangeTiles = 250, ArmyRangeTiles = 120;

		private static readonly LineBreakDetector Det = new();
		private static bool _inFight; private static int _fightStart, _lastBossTick, _deaths, _lastWarnTick = -RateLimitTicks;
		/// <summary>Self-check counters: warnings shown (combat text and chat both), and the tick of the last one.</summary>
		public static int Warnings; public static string LastWhy; public static int LastWarnAt;
		/// <summary>Self-check: the texts shown (also on a dedicated test server, where nothing is drawn).</summary>
		public static readonly System.Collections.Generic.List<string> Shown = new();
		/// <summary>Self-check: act as a client on a dedicated test server (the commander is Main.player[0]); shown warnings are counted but no text is drawn.</summary>
		public static bool TestClient;
		/// <summary>Self-check: stands in for the config's switch.</summary>
		public static bool? TestEnabled;
		/// <summary>Self-check clock: when set, replaces Main.GameUpdateCount.</summary>
		public static int? TestTick;

		private static int Now => TestTick ?? (int)Main.GameUpdateCount;
		private static Player Commander => Main.dedServ ? Main.player[0] : Main.LocalPlayer;
		private static bool Active => (!Main.dedServ || TestClient) && Main.netMode != NetmodeID.MultiplayerClient;
		private static bool Enabled { get { if (TestEnabled.HasValue) return TestEnabled.Value; try { return FightRecConfig.Get?.LineBreakingWarning ?? true; } catch (Exception) { return true; } } }

		public override void PostUpdateEverything() => Step();
		public override void OnWorldUnload() => Reset();

		/// <summary>Forgets the fight (a new world, or the self-check starting over). The one-a-minute pause is kept unless <paramref name="rate"/>.</summary>
		public static void Reset(bool rate = false) { _inFight = false; Det.Reset(); _deaths = 0; if (rate) _lastWarnTick = -RateLimitTicks; }

		/// <summary>A levy fell (GlobalNPC.CheckDead): counted when it fell inside the army's range of the commander, while a fight runs.</summary>
		public static void NoteLevyDeath(NPC n)
		{
			if (!_inFight || !Active) return;
			Player cmd = Commander; if (cmd == null || !cmd.active) return;
			float r = ArmyRangeTiles * 16f;
			if (Math.Abs(n.Center.X - cmd.Center.X) <= r && Math.Abs(n.Center.Y - cmd.Center.Y) <= r) _deaths++;
		}

		/// <summary>One game tick: every half second, look at the boss and the army.</summary>
		public static void Step()
		{
			if (!Active || Now % EveryTicks != 0) return;
			Player cmd = Commander;
			if (cmd == null || !cmd.active || cmd.dead) return;
			bool boss = false; float bossR = BossRangeTiles * 16f, armyR = ArmyRangeTiles * 16f; int n = 0, broken = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC o = Main.npc[i];
				if (!o.active) continue;
				float dx = Math.Abs(o.Center.X - cmd.Center.X), dy = Math.Abs(o.Center.Y - cmd.Center.Y);
				if (o.ModNPC is SoldierNPC s)
				{
					if (o.life <= 0 || dx > armyR || dy > armyR) continue;
					n++; if (s.State == SoldierState.Shaken || s.State == SoldierState.Routed || s.State == SoldierState.Desert) broken++;
					continue;
				}
				if (!boss && !o.friendly && !o.townNPC && dx < bossR && dy < bossR && FightRecorder.IsFightBoss(o)) boss = true;
			}
			Evaluate(Now, boss, n, broken, cmd);
		}

		/// <summary>The decision, given what the scan saw (the self-check feeds it a scripted fight). Returns true when a warning was shown.</summary>
		internal static bool Evaluate(int tick, bool boss, int n, int broken, Player cmd)
		{
			if (!boss)
			{
				if (_inFight && tick - _lastBossTick >= QuietEndTicks) Reset();
				return false;
			}
			_lastBossTick = tick;
			if (!_inFight) { _inFight = true; _fightStart = tick; _deaths = 0; Det.Reset(); }
			if (!Enabled) return false;
			if (!Det.Feed(tick - _fightStart, n, broken, _deaths)) return false;
			if (tick - _lastWarnTick < RateLimitTicks) return false; // the detector fires once per fight; the pause keeps a second fight a few seconds later from warning again
			_lastWarnTick = tick; Warnings++; LastWhy = Det.Why; LastWarnAt = tick;
			if (FightRecorder.On) FightRecorder.NoteWarning(Det.Why, n, broken, _deaths);
			Show(cmd);
			return true;
		}

		private static void Show(Player cmd)
		{
			Shown.Add(Text);
			if (Main.dedServ) return;
			try
			{
				CombatText.NewText(cmd.getRect(), new Color(255, 96, 72), Text, true);
				Main.NewText(Text, 255, 120, 90);
			}
			catch (Exception) { }
		}
	}

	/// <summary>Counts every levy death for <see cref="LineWatch"/>, whether or not a recording runs.</summary>
	public class LineWatchDeaths : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.ModNPC is SoldierNPC;
		public override bool CheckDead(NPC npc) { LineWatch.NoteLevyDeath(npc); return true; }
	}
}
