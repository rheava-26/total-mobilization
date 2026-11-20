using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Common.Progression;

namespace Levy.Content.Items
{
	/// <summary>
	/// Re-forms Calamity's Exodium planetoids (see <see cref="ExodiumMagnet"/>). Only craftable with the progression option on, and only does anything with Calamity loaded and Moon Lord down.
	/// Not consumed; a cooldown from the config sits between uses. The world is only edited on the server (a client sends <see cref="LevyPacket.ProgItemUse"/>).
	/// </summary>
	public class SuperpoweredMagnet : ModItem
	{
		public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LevyProgressionConfig.Get?.SuperpoweredMagnetCooldownMinutes ?? 5);

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 1; }

		public override void SetDefaults()
		{
			Item.width = 30; Item.height = 30; Item.useTime = 45; Item.useAnimation = 45; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item4; Item.consumable = false; Item.maxStack = 1; Item.rare = ItemRarityID.Red; Item.value = Item.sellPrice(gold: 10);
		}

		public override bool CanUseItem(Player player) => CalamityLink.Loaded && NPC.downedMoonlord;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;
			if (Main.netMode == NetmodeID.MultiplayerClient) { var p = LevyPackets.New(LevyPacket.ProgItemUse); p.Write((byte)0); p.Send(); }
			else ExodiumMagnet.Say(ExodiumMagnet.TryUse(false), player.whoAmI);
			return true;
		}
	}
}
