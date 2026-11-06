using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// SELLING ON THE MARCH. Vanilla pays for what you sell at value / PriceAdjustment and charges value * PriceAdjustment for what you buy, so one number is both halves of a
	/// fence's terms. While you talk to a commissioned Merchant, his shop runs at <see cref="MerchantMarchPriceAdjust"/> (better than vanilla's best, 0.75).
	/// </summary>
	public class OfficerPlayer : ModPlayer
	{
		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer || Player.talkNPC < 0 || Player.talkNPC >= Main.maxNPCs) return;
			NPC n = Main.npc[Player.talkNPC];
			if (!n.active || n.type != NPCID.Merchant || OfficerSystem.Data(n)?.Enrolled != true) return;
			var s = Player.currentShoppingSettings;
			if (s.PriceAdjustment <= MerchantMarchPriceAdjust) return;
			s.PriceAdjustment = MerchantMarchPriceAdjust;
			Player.currentShoppingSettings = s;
		}
	}
}
