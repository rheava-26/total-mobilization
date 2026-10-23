using System;
using System.Diagnostics;
using System.Threading;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Util
{
	/// <summary>
	/// DIAGNOSTIC ONLY - it changes nothing about the game. The game update runs on one thread; when that thread hangs there is no log line, no stack and no clue where. This keeps a few
	/// breadcrumbs (which part of the tick is running, the NPC whose AI started last, the projectile whose Update started last, the soldier and step the brain is in) and a background
	/// thread that, if the update has stayed inside ONE part of the tick for <see cref="StallSeconds"/>, writes a warning with those breadcrumbs to the log (client.log / server.log) and
	/// repeats it every <see cref="ReportEverySeconds"/> while the stall lasts. Two reports with the same breadcrumbs mean the thread is stuck in one place; changing breadcrumbs mean a tick
	/// that is merely very slow. Nothing is logged while the game is paused, in a menu, or between ticks. A long world save or load inside a tick can trip it once (harmless).
	/// </summary>
	public static class HangWatch
	{
		internal const double StallSeconds = 5.0, ReportEverySeconds = 5.0;
		internal const int MaxReportsPerStall = 12;

		/// <summary>0 = between ticks (or paused / in a menu); otherwise the part of the tick the update thread entered last (see <see cref="PhaseName"/>).</summary>
		internal static volatile int Phase;
		internal static long PhaseSince;
		internal static volatile int NpcType = -1, NpcSlot = -1;
		internal static volatile int ProjType = -1, ProjSlot = -1;
		internal static volatile int BrainSlot = -1, BrainStep;

		internal static string PhaseName(int phase) => phase switch
		{
			1 => "pre-update (ambience, entities)",
			2 => "players",
			3 => "NPC update (spawns, every NPC's AI, soldier brains)",
			4 => "after NPCs (gores)",
			5 => "projectile update (movement, damage, kills)",
			6 => "after projectiles (items, dust)",
			7 => "time / world / events",
			_ => "between ticks",
		};

		internal static string BrainStepName(int step) => step switch
		{
			1 => "think start",
			2 => "world scan",
			3 => "engage / order",
			4 => "target pick",
			5 => "positioning",
			6 => "firing-spot search",
			7 => "perch / cover search",
			8 => "order (follow / idle / patrol / haul)",
			_ => "none",
		};

		internal static void Enter(int phase)
		{
			Volatile.Write(ref PhaseSince, Stopwatch.GetTimestamp());
			Phase = phase;
		}

		/// <summary>An NPC's AI is starting.</summary>
		public static void Npc(NPC n) { NpcType = n.type; NpcSlot = n.whoAmI; }

		/// <summary>A projectile's Update is starting.</summary>
		public static void Proj(Projectile p) { ProjType = p.type; ProjSlot = p.whoAmI; }

		/// <summary>A soldier's brain entered <paramref name="step"/> (see <see cref="BrainStepName"/>).</summary>
		public static void Brain(int slot, int step) { BrainSlot = slot; BrainStep = step; }
	}

	/// <summary>Marks the parts of the game tick and runs the watchdog thread. See <see cref="HangWatch"/>.</summary>
	public class HangWatchdogSystem : ModSystem
	{
		private Thread _thread;
		private volatile bool _stop;

		public override void Load()
		{
			_stop = false;
			try
			{
				_thread = new Thread(Run) { IsBackground = true, Name = "Levy hang watchdog" };
				_thread.Start();
			}
			catch (Exception) { _thread = null; } // a diagnostic must never stop the mod from loading
		}

		public override void Unload()
		{
			_stop = true;
			try { _thread?.Join(1500); } catch (Exception) { }
			_thread = null;
			HangWatch.Phase = 0;
		}

		public override void OnWorldLoad() => HangWatch.Phase = 0;
		public override void OnWorldUnload() => HangWatch.Phase = 0;

		public override void PreUpdateEntities() => HangWatch.Enter(1);
		public override void PreUpdatePlayers() => HangWatch.Enter(2);
		public override void PreUpdateNPCs() => HangWatch.Enter(3);
		public override void PostUpdateNPCs() => HangWatch.Enter(4);
		public override void PreUpdateProjectiles() => HangWatch.Enter(5);
		public override void PostUpdateProjectiles() => HangWatch.Enter(6);
		public override void PreUpdateTime() => HangWatch.Enter(7);
		public override void PostUpdateEverything() => HangWatch.Phase = 0;

		private void Run()
		{
			long lastReport = 0; int reports = 0;
			while (!_stop)
			{
				try
				{
					Thread.Sleep(1000);
					int phase = HangWatch.Phase;
					if (phase == 0) { reports = 0; lastReport = 0; continue; }
					long now = Stopwatch.GetTimestamp();
					double stalled = (now - Volatile.Read(ref HangWatch.PhaseSince)) / (double)Stopwatch.Frequency;
					if (stalled < HangWatch.StallSeconds) { if (lastReport != 0 && stalled < 1.0) { reports = 0; lastReport = 0; } continue; } // it moved on since the last report
					if (reports >= HangWatch.MaxReportsPerStall) continue;
					if (lastReport != 0 && (now - lastReport) / (double)Stopwatch.Frequency < HangWatch.ReportEverySeconds) continue;
					lastReport = now; reports++;
					Report(phase, stalled);
				}
				catch (Exception) { /* never let a diagnostic thread take the process down */ }
			}
		}

		private void Report(int phase, double stalled)
		{
			int npcs = 0, soldiers = 0, projectiles = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n == null || !n.active) continue;
				npcs++;
				if (n.ModNPC is SoldierNPC) soldiers++;
			}
			for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i] != null && Main.projectile[i].active) projectiles++;
			string brain = HangWatch.BrainSlot >= 0 ? $"soldier slot {HangWatch.BrainSlot}, step '{HangWatch.BrainStepName(HangWatch.BrainStep)}'" : "none yet";
			Mod.Logger.Warn($"Levy watchdog: the game update has stayed in '{HangWatch.PhaseName(phase)}' for {stalled:F0}s (tick {Main.GameUpdateCount}; {npcs} NPCs, {soldiers} of them soldiers; {projectiles} projectiles). "
				+ $"Last NPC AI started: type {HangWatch.NpcType} slot {HangWatch.NpcSlot}. Last projectile update started: type {HangWatch.ProjType} slot {HangWatch.ProjSlot}. Brain: {brain}. "
				+ "Identical lines a few seconds apart mean the update thread is stuck in that spot; changing numbers mean a very slow tick. (A long world save or load inside a tick can trip this once: ignore it.)");
		}
	}

	/// <summary>Leaves a breadcrumb at the start of every NPC's AI so a stall can name the NPC. See <see cref="HangWatch"/>.</summary>
	public class HangWatchdogNpc : GlobalNPC
	{
		public override bool PreAI(NPC npc)
		{
			HangWatch.Npc(npc);
			return true;
		}
	}
}
