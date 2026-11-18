using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// Whether a boss fight is on, by the same rule Levy's fight recorder uses (FightRecorder.IsFightBoss): a boss flag, the game's "counts as a boss" set, Levy's main-boss list, or a worm head that owns its segments' life and has a boss map icon
	/// (the vanilla Eater of Worlds never sets NPC.boss while it lives). Tracks the tick a fight boss was last alive, so a band does not form the moment a fight ends: the config grace window runs on after the last boss falls.
	/// Server and single player. Natural bands, event bands and the band hunt all read <see cref="Held"/>; a band the player summons is the player's own choice and is never held.
	/// </summary>
	public class BossFight : ModSystem
	{
		/// <summary>A fight boss is alive right now (rescanned every 10 ticks).</summary>
		public static bool Alive;
		/// <summary>The game tick a fight boss was last seen alive; uint.MaxValue = none seen yet this session.</summary>
		public static uint LastSeen = uint.MaxValue;

		/// <summary>Levy's recorder rule, minus Levy's own bodies and friendly or town NPCs.</summary>
		public static bool IsFightBoss(NPC n)
		{
			if (n == null || !n.active || n.life <= 0 || n.friendly || n.townNPC || n.ModNPC is SoldierNPC) return false;
			if (n.boss || NPCID.Sets.ShouldBeCountedAsBoss[n.type] || BossRule.IsMainBossType(n.type)) return true;
			return n.realLife == n.whoAmI && NPCID.Sets.BossHeadTextures[n.type] >= 0;
		}

		/// <summary>Rescans the NPC array and returns whether a fight boss is alive.</summary>
		public static bool Scan()
		{
			uint now = Main.GameUpdateCount;
			bool any = false;
			for (int i = 0; i < Main.maxNPCs; i++) if (IsFightBoss(Main.npc[i])) { any = true; break; }
			Alive = any;
			if (any) LastSeen = now;
			return any;
		}

		/// <summary>True when no band may form or hunt: a fight boss is alive, or one was within the grace window (<paramref name="graceTicks"/>) before <paramref name="now"/>.</summary>
		public static bool HeldAt(bool bossAlive, uint lastSeen, uint now, int graceTicks) => bossAlive || (lastSeen != uint.MaxValue && now >= lastSeen && now - lastSeen < (uint)System.Math.Max(0, graceTicks));

		public static int GraceTicks(WarbandsConfig cfg) => 60 * System.Math.Max(0, cfg?.BossGraceSeconds ?? 0);

		/// <summary>The config's hold, live (rescans the NPCs): NoSpawnsDuringBosses and (a fight boss alive or inside the grace window). For the once-a-minute and event rolls.</summary>
		public static bool Held(WarbandsConfig cfg)
		{
			if (cfg == null || !cfg.NoSpawnsDuringBosses) return false;
			bool alive = Scan();
			return HeldAt(alive, LastSeen, Main.GameUpdateCount, GraceTicks(cfg));
		}

		/// <summary>The same hold from the last scan (at most 10 ticks old): for per-member, per-tick callers such as the hunt.</summary>
		public static bool HeldCached(WarbandsConfig cfg) => cfg != null && cfg.NoSpawnsDuringBosses && HeldAt(Alive, LastSeen, Main.GameUpdateCount, GraceTicks(cfg));

		public override void OnWorldLoad() { Alive = false; LastSeen = uint.MaxValue; }
		public override void OnWorldUnload() { Alive = false; LastSeen = uint.MaxValue; }

		public override void PostUpdateNPCs()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			if (Main.GameUpdateCount % 10 == 0) Scan();
		}
	}
}
