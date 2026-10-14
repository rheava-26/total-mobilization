using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Flavor;
using Levy.Common.Util;

namespace Levy.Content.Items.Books
{
	/// <summary>
	/// Shared behaviour of the five event books: consumed on use, once per world. The effect itself is a world flag on
	/// <see cref="Common.Systems.SettlementSystem"/>; the other modules read it.
	/// </summary>
	public abstract class EventBook : ModItem
	{
		/// <summary>Flag key understood by <c>SettlementSystem.TrySetBook</c>.</summary>
		public abstract string Key { get; }

		private static uint _lastNoticeTick;

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 1;
		}

		public override void SetDefaults()
		{
			Item.width = 24; Item.height = 28; Item.useTime = 30; Item.useAnimation = 30; Item.useStyle = ItemUseStyleID.HoldUp; Item.consumable = true; Item.maxStack = 1;
			Item.rare = ItemRarityID.Orange; Item.value = Item.sellPrice(gold: 1); Item.UseSound = SoundID.Item4;
		}

		public override bool CanUseItem(Player player)
		{
			if (!FlavorSystem.IsStudied(Key)) return true;
			// Only the person who clicked sees the notice, and not on every frame of a held click.
			if (Main.netMode != NetmodeID.Server && player.whoAmI == Main.myPlayer && Main.GameUpdateCount - _lastNoticeTick >= LevyConstants.BookNoticeCooldownTicks)
			{
				_lastNoticeTick = Main.GameUpdateCount;
				Main.NewText(LevyUtil.T("Flavor.AlreadyStudied"), FlavorSystem.NoticeColor);
			}
			return false;
		}

		public override bool? UseItem(Player player)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				// Called on every client for every player's use; only the user's own client reports it.
				if (player.whoAmI == Main.myPlayer) FlavorSystem.SendBookUsed(Key);
				return true;
			}
			// Server (the server also runs UseItem for remote players) and single player.
			return FlavorSystem.Study(Key);
		}
	}
}
