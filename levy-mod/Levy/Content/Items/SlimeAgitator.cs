using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Common.Progression;

namespace Levy.Content.Items
{
	/// <summary>Starts a slime rain that also brings Calamity's ore slimes (see <see cref="AgitatedRain"/>). Consumed on use; inert without Calamity.</summary>
	public class SlimeAgitator : ModItem
	{
		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 3; }

		public override void SetDefaults()
		{
			Item.width = 26; Item.height = 28; Item.useTime = 45; Item.useAnimation = 45; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item4; Item.consumable = true; Item.maxStack = Item.CommonMaxStack; Item.rare = ItemRarityID.Yellow; Item.value = Item.sellPrice(gold: 2);
		}

		public override bool CanUseItem(Player player) => CalamityLink.Loaded && !Main.slimeRain;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;
			if (Main.netMode == NetmodeID.MultiplayerClient) { var p = LevyPackets.New(LevyPacket.ProgItemUse); p.Write((byte)1); p.Send(); }
			else AgitatedRain.Start();
			return true;
		}
	}
}
