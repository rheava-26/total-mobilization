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
		/// <summary>The test NPC's busy time is timed into Levy's own "ai" bucket, the way a levy's AI is (the bucket check).</summary>
		public static volatile bool NpcAsLevy;
		/// <summary>Wall time the test NPC actually spent in its busy-waits as Levy's "ai" bucket (Stopwatch ticks): what the bucket must read, preemption included.</summary>
		public static long NpcSpinTicks;
		public static long NpcCalls, ProjCalls, GlobCalls, LayerCalls;
		public static void Spin(int us) { if (us <= 0) return; long end = Stopwatch.GetTimestamp() + us * Stopwatch.Frequency / 1000000; while (Stopwatch.GetTimestamp() < end) { } }
		public static void Reset() { NpcUs = ProjUs = GlobUs = LayerUs = 0; NpcAsLevy = false; NpcSpinTicks = 0; NpcCalls = ProjCalls = GlobCalls = LayerCalls = 0; }
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
		public override void AI()
		{
			NPC.velocity = Vector2.Zero; NPC.timeLeft = 99999; PerfTestTypes.NpcCalls++;
			if (!PerfTestTypes.NpcAsLevy) { PerfTestTypes.Spin(PerfTestTypes.NpcUs); return; }
			long t0 = global::Levy.Common.Util.LevyProf.Begin(), s0 = Stopwatch.GetTimestamp(); PerfTestTypes.Spin(PerfTestTypes.NpcUs); PerfTestTypes.NpcSpinTicks += Stopwatch.GetTimestamp() - s0; global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.AI, t0);
		}
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
