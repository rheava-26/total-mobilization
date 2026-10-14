using System.Collections.Generic;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>Boss-rule numbers and structure tables. Additive to LevyConstants (never rename).</summary>
	public static partial class LevyConstants
	{
		// ---------- Boss rule: structure ----------
		/// <summary>
		/// Boss parts that do not share a life pool with their boss, mapped to the boss root type. Parts WITH realLife
		/// (Destroyer body/tail, Wall of Flesh eyes) resolve through realLife first; this table is their fallback and the
		/// only route for Skeletron/Prime limbs, Golem parts, Plantera hooks, Moon Lord head/hands and Eater of Worlds segments.
		/// </summary>
		public static readonly Dictionary<int, int> BossParts = new()
		{
			{ NPCID.SkeletronHand, NPCID.SkeletronHead },
			{ NPCID.PrimeCannon, NPCID.SkeletronPrime }, { NPCID.PrimeSaw, NPCID.SkeletronPrime },
			{ NPCID.PrimeVice, NPCID.SkeletronPrime }, { NPCID.PrimeLaser, NPCID.SkeletronPrime },
			{ NPCID.GolemHead, NPCID.Golem }, { NPCID.GolemFistLeft, NPCID.Golem }, { NPCID.GolemFistRight, NPCID.Golem }, { NPCID.GolemHeadFree, NPCID.Golem },
			{ NPCID.PlanterasHook, NPCID.Plantera }, { NPCID.PlanterasTentacle, NPCID.Plantera },
			{ NPCID.MoonLordHead, NPCID.MoonLordCore }, { NPCID.MoonLordHand, NPCID.MoonLordCore },
			{ NPCID.EaterofWorldsBody, NPCID.EaterofWorldsHead }, { NPCID.EaterofWorldsTail, NPCID.EaterofWorldsHead },
			{ NPCID.TheDestroyerBody, NPCID.TheDestroyer }, { NPCID.TheDestroyerTail, NPCID.TheDestroyer },
			{ NPCID.WallofFleshEye, NPCID.WallofFlesh },
		};

		/// <summary>
		/// Trophy and relic items of each main boss. Tile type and place style are read from ContentSamples at runtime
		/// (trophies: TileID.Painting3X3, relics: TileID.MasterTrophyBase). The Twins share one relic.
		/// </summary>
		public static readonly Dictionary<int, int[]> BossTrophyItems = new()
		{
			{ NPCID.KingSlime, new int[] { ItemID.KingSlimeTrophy, ItemID.KingSlimeMasterTrophy } },
			{ NPCID.EyeofCthulhu, new int[] { ItemID.EyeofCthulhuTrophy, ItemID.EyeofCthulhuMasterTrophy } },
			{ NPCID.EaterofWorldsHead, new int[] { ItemID.EaterofWorldsTrophy, ItemID.EaterofWorldsMasterTrophy } },
			{ NPCID.BrainofCthulhu, new int[] { ItemID.BrainofCthulhuTrophy, ItemID.BrainofCthulhuMasterTrophy } },
			{ NPCID.QueenBee, new int[] { ItemID.QueenBeeTrophy, ItemID.QueenBeeMasterTrophy } },
			{ NPCID.Deerclops, new int[] { ItemID.DeerclopsTrophy, ItemID.DeerclopsMasterTrophy } },
			{ NPCID.SkeletronHead, new int[] { ItemID.SkeletronTrophy, ItemID.SkeletronMasterTrophy } },
			{ NPCID.WallofFlesh, new int[] { ItemID.WallofFleshTrophy, ItemID.WallofFleshMasterTrophy } },
			{ NPCID.QueenSlimeBoss, new int[] { ItemID.QueenSlimeTrophy, ItemID.QueenSlimeMasterTrophy } },
			{ NPCID.Retinazer, new int[] { ItemID.RetinazerTrophy, ItemID.TwinsMasterTrophy } },
			{ NPCID.Spazmatism, new int[] { ItemID.SpazmatismTrophy, ItemID.TwinsMasterTrophy } },
			{ NPCID.TheDestroyer, new int[] { ItemID.DestroyerTrophy, ItemID.DestroyerMasterTrophy } },
			{ NPCID.SkeletronPrime, new int[] { ItemID.SkeletronPrimeTrophy, ItemID.SkeletronPrimeMasterTrophy } },
			{ NPCID.Plantera, new int[] { ItemID.PlanteraTrophy, ItemID.PlanteraMasterTrophy } },
			{ NPCID.Golem, new int[] { ItemID.GolemTrophy, ItemID.GolemMasterTrophy } },
			{ NPCID.HallowBoss, new int[] { ItemID.FairyQueenTrophy, ItemID.FairyQueenMasterTrophy } },
			{ NPCID.DukeFishron, new int[] { ItemID.DukeFishronTrophy, ItemID.DukeFishronMasterTrophy } },
			{ NPCID.CultistBoss, new int[] { ItemID.AncientCultistTrophy, ItemID.LunaticCultistMasterTrophy } },
			{ NPCID.MoonLordCore, new int[] { ItemID.MoonLordTrophy, ItemID.MoonLordMasterTrophy } },
		};

		// ---------- Boss rule: numbers ----------
		/// <summary>How long a trophy scan result is reused per (boss, barracks).</summary>
		public const int TrophyCacheTicks = 60 * 10;
		/// <summary>Upper bound when walking a worm's ai[1] chain toward its head.</summary>
		public const int WormChainMaxSteps = 200;
		/// <summary>Hostile projectiles slower than this (px/tick) are never redirected (beams, stationary fields).</summary>
		public const float RedirectMinSpeed = 0.5f;
		/// <summary>Spite bookkeeping for departed soldiers is pruned every N ticks.</summary>
		public const int SpitePruneIntervalTicks = 64;

		/// <summary>
		/// Vanilla Duke Fishron hover timers (NPC.AI_069_DukeFishron, decompiled lines 43102-43120 and 43172-43175), replicated so the
		/// boss rule can predict the tick on which Fishron picks its next attack and computes its dash velocity from the target.
		/// </summary>
		public static class FishronVanilla
		{
			public const int HoverExpert = 40, HoverNormal = 60;
			public const int HoverPhase3 = 30;
			public const int HoverPhase2FreshExpert = 40, HoverPhase2FreshNormal = 20;
			public const int HoverPhase1Fresh = 30;
			public const int HoverEnraged = 10;
			public const float EnrageSkyY = 800f, EnrageEdgePx = 6400f;
			public const float FreshAttackCount = 10f;
		}
	}
}
