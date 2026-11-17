using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Test-only content for the performance analyzer's world check: an NPC, a projectile, a GlobalNPC hook and a map layer that burn a set number of microseconds when they update or draw. They exist only when LEVY_SELFCHECK_WORLD=1 is set
	/// in the environment (the ship world run); a player's game never loads them. Every one is idle until the check sets its busy time.
	/// </summary>
	public static class PerfTestTypes
	{
		public static bool Loaded => Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1";
		public static volatile int NpcUs, ProjUs, GlobUs, LayerUs;
		public static long NpcCalls, ProjCalls, GlobCalls, LayerCalls;
		/// <summary>Wall-clock milliseconds the busy-waits of the current phase actually took (a call preempted by a loaded box runs longer than asked). The analyzer measures wall time too, so its row is judged against this, and against the nominal burn from below.</summary>
		public static double WallMs;
		/// <summary>Busy-wait for <paramref name="us"/> microseconds of wall clock; records how long it really took.</summary>
		public static void Spin(int us) { if (us <= 0) return; long t0 = Stopwatch.GetTimestamp(); long end = t0 + us * Stopwatch.Frequency / 1000000; long now; while ((now = Stopwatch.GetTimestamp()) < end) { } WallMs += (now - t0) * 1000.0 / Stopwatch.Frequency; }
		public static void Reset() { NpcUs = ProjUs = GlobUs = LayerUs = 0; NpcCalls = ProjCalls = GlobCalls = LayerCalls = 0; WallMs = 0; }
	}

	public class PerfTestNpc : ModNPC
	{
		public override bool IsLoadingEnabled(Mod mod) => PerfTestTypes.Loaded;
		public override string Texture => "Terraria/Images/NPC_" + NPCID.Bunny;
		public override void SetDefaults()
		{
			NPC.width = NPC.height = 16; NPC.lifeMax = NPC.life = 10; NPC.damage = 0; NPC.friendly = true; NPC.noGravity = true; NPC.noTileCollide = true; NPC.knockBackResist = 0f; NPC.aiStyle = -1; NPC.dontTakeDamage = true; NPC.dontCountMe = true;
		}
		public override bool CheckActive() => false;
		public override void AI() { NPC.velocity = Vector2.Zero; NPC.timeLeft = 99999; PerfTestTypes.NpcCalls++; PerfTestTypes.Spin(PerfTestTypes.NpcUs); }
	}

	public class PerfTestProj : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => PerfTestTypes.Loaded;
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;
		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 8; Projectile.friendly = false; Projectile.hostile = false; Projectile.tileCollide = false; Projectile.timeLeft = 100000; Projectile.penetrate = -1; Projectile.aiStyle = -1; Projectile.damage = 0;
		}
		public override void AI() { Projectile.velocity = Vector2.Zero; Projectile.timeLeft = 100000; PerfTestTypes.ProjCalls++; PerfTestTypes.Spin(PerfTestTypes.ProjUs); }
	}

	public class PerfTestGlobal : GlobalNPC
	{
		public override bool IsLoadingEnabled(Mod mod) => PerfTestTypes.Loaded;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.ModNPC is PerfTestNpc;
		public override void AI(NPC npc) { PerfTestTypes.GlobCalls++; PerfTestTypes.Spin(PerfTestTypes.GlobUs); }
	}

	public class PerfTestLayer : ModMapLayer
	{
		public override bool IsLoadingEnabled(Mod mod) => PerfTestTypes.Loaded;
		public override Position GetDefaultPosition() => new Before(IMapLayer.Pings);
		public override void Draw(ref MapOverlayDrawContext context, ref string text) { PerfTestTypes.LayerCalls++; PerfTestTypes.Spin(PerfTestTypes.LayerUs); }
	}
}
