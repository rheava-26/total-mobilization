using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;

namespace Levy.Content.Items
{
	/// <summary>
	/// An artificial meteor: used at night it calls a meteor down with vanilla <see cref="WorldGen.dropMeteor"/>. The world is only edited on the server (single player runs it directly;
	/// a client sends <see cref="LevyPacket.MeteorBeacon"/>). Crafted at a Demon Altar once the Eater of Worlds or Brain of Cthulhu is down.
	/// </summary>
	public class MeteorBeacon : ModItem
	{
		public const int FallenStars = 5, Bars = 6;

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 5; }

		public override void SetDefaults()
		{
			Item.width = 24; Item.height = 24; Item.useTime = 45; Item.useAnimation = 45; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item4; Item.consumable = true; Item.maxStack = Item.CommonMaxStack; Item.rare = ItemRarityID.Orange; Item.value = Item.sellPrice(silver: 40);
		}

		public override bool CanUseItem(Player player)
		{
			return !Main.dayTime && NPC.downedBoss2;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;
			if (Main.netMode == NetmodeID.MultiplayerClient) LevyPackets.New(LevyPacket.MeteorBeacon).Send();
			else Drop();
			return true;
		}

		/// <summary>Server / single player: drop one meteor, if it is night.</summary>
		internal static void Drop()
		{
			if (Main.dayTime || !NPC.downedBoss2) return;
			WorldGen.dropMeteor();
		}

		/// <summary>Server side of <see cref="LevyPacket.MeteorBeacon"/>: the sender must be a live player holding the item.</summary>
		internal static void OnPacket(BinaryReader r, int whoAmI)
		{
			if (whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active || Main.player[whoAmI].dead) return;
			if (Main.player[whoAmI].HeldItem.type != ModContent.ItemType<MeteorBeacon>()) return;
			Drop();
		}

		public override void AddRecipes()
		{
			foreach (int bar in new[] { ItemID.DemoniteBar, ItemID.CrimtaneBar })
				CreateRecipe().AddIngredient(ItemID.FallenStar, FallenStars).AddIngredient(bar, Bars).AddTile(TileID.DemonAltar).AddCondition(Condition.DownedEowOrBoc).Register();
		}
	}

	public class MeteorBeaconNet : ModSystem
	{
		public override void Load() => LevyPackets.Register(LevyPacket.MeteorBeacon, MeteorBeacon.OnPacket);
	}
}
