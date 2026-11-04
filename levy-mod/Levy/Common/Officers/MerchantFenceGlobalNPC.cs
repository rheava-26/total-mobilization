using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.TileEntities;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE MERCHANT'S FENCE (enemy side). When an enemy dies within <see cref="MerchantFenceRadiusTiles"/> tiles of a commissioned Merchant on the field, he fences its loot: the
	/// treasury that pays him is credited the corpse's own coin value times <see cref="MerchantFenceValueMult"/>, on top of whatever the kill drops, so every kill near him is worth more.
	/// Whoever made the kill, the army or the commander. Server / single player only, one credit per kill. Critters, town folk and friendly NPCs pay nothing, nor does anything spawned
	/// from a statue (vanilla pays no loot for those either, so a statue farm cannot print money). The credit is queued with <c>MarkDirty</c>, so a burst of kills costs one sync.
	/// </summary>
	public class MerchantFenceGlobalNPC : GlobalNPC
	{
		public override void OnKill(NPC npc)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			if (npc.value <= 0f || npc.friendly || npc.townNPC || npc.SpawnedFromStatue || npc.lifeMax <= 5 || NPCID.Sets.CountsAsCritter[npc.type]) return; // nothing worth fencing

			var officers = OfficerSystem.Instance.ActiveBodies;
			if (officers.Count == 0) return;
			float reach = MerchantFenceRadiusTiles * LevyConstants.TilePx;
			float bestSq = reach * reach;
			OfficerBody merchant = null;
			for (int i = 0; i < officers.Count; i++)
			{
				OfficerBody b = officers[i];
				if (!b.IsAlive || b.TownNpc == null || b.TownNpc.type != NPCID.Merchant) continue;
				float dSq = Vector2.DistanceSquared(npc.Center, b.NPC.Center);
				if (dSq <= bestSq) { bestSq = dSq; merchant = b; }
			}
			if (merchant == null) return;                                 // no Merchant in reach: nobody to fence it

			long credit = (long)(npc.value * MerchantFenceValueMult);
			if (credit <= 0) return;
			// The treasury that pays him is the one he enriches: his commissioning barracks if it still stands, else the nearest.
			OfficerGlobalNPC g = OfficerSystem.Data(merchant.TownNpc);
			BarracksTE barracks = g != null ? OfficerSystem.Instance.PayingBarracks(merchant.TownNpc, g) : SettlementSystem.Instance.NearestBarracks(merchant.NPC.Center);
			if (barracks == null) return;
			EconomySystem.Deposit(barracks, credit);
			barracks.MarkDirty();
		}
	}
}
