using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Mark;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.TileEntities;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE TAX COLLECTOR'S BOUNTY (enemy side). When a marked enemy dies near a commissioned Tax Collector, his corpse is taxed: a coin bounty — scaled by
	/// the enemy's max life and capped — is deposited into the nearest barracks treasury and synced. Server / single player only, one deposit per kill. The
	/// mark is the shared army <see cref="Levy.Content.Buffs.MarkedBuff"/>, so any marked enemy that falls near him pays; that keeps the detection a single
	/// server-side buff check with no per-officer bookkeeping.
	/// </summary>
	public class TaxBountyGlobalNPC : GlobalNPC
	{
		public override void OnKill(NPC npc)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			if (!npc.HasBuff(MarkSystem.BuffType)) return;              // only a marked corpse is taxed

			// The nearest commissioned Tax Collector whose avatar is on the field within bounty reach of the kill.
			float reach = TaxBountyRadiusTiles * LevyConstants.TilePx;
			float bestSq = reach * reach;
			OfficerBody nearest = null;
			var officers = OfficerSystem.Instance.ActiveBodies;
			for (int i = 0; i < officers.Count; i++)
			{
				OfficerBody b = officers[i];
				if (!b.IsAlive || b.TownNpc == null || b.TownNpc.type != NPCID.TaxCollector) continue;
				float dSq = Vector2.DistanceSquared(npc.Center, b.NPC.Center);
				if (dSq <= bestSq) { bestSq = dSq; nearest = b; }
			}
			if (nearest == null) return;                               // no Tax Collector in reach: nobody to collect

			long bounty = Math.Clamp((long)(npc.lifeMax * TaxBountyCopperPerMaxHp), TaxBountyMinCopper, TaxBountyMaxCopper);
			BarracksTE barracks = SettlementSystem.Instance.NearestBarracks(nearest.NPC.Center);
			if (barracks == null) return;
			EconomySystem.Deposit(barracks, bounty);
			RosterSystem.SyncBarracks(barracks);
		}
	}
}
