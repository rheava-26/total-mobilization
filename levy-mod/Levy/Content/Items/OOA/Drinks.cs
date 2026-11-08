using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Net;
using Levy.Content.Buffs;

namespace Levy.Content.Items.OOA
{
	/// <summary>
	/// A round for the army: using a drink buffs every levy of yours within <see cref="LevyConstants.DrinkRadiusTiles"/> tiles for five minutes, and they aim worse for it. Each drink states its exact effect in its tooltip.
	/// Sold by the Tavernkeep for coins. Single player serves the round directly; a client asks the server (the soldiers' stats live there).
	/// </summary>
	public abstract class DrinkItem : ModItem
	{
		public abstract int Drink { get; }
		public abstract int PriceCopper { get; }
		/// <summary>The drink's number as the tooltip shows it (8 defense, 15% damage, 10% critical strike chance).</summary>
		public abstract int Amount { get; }

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 1; }

		public override void SetDefaults()
		{
			Item.width = 20; Item.height = 28; Item.useTime = 20; Item.useAnimation = 20; Item.useStyle = ItemUseStyleID.DrinkLiquid;
			Item.UseSound = SoundID.Item3; Item.consumable = true; Item.maxStack = 30; Item.rare = ItemRarityID.Blue; Item.value = PriceCopper;
		}

		public override Terraria.Localization.LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LevyConstants.DrinkRadiusTiles, LevyConstants.DrinkDurationTicks / 3600, Amount, (int)System.Math.Round((LevyConstants.DrunkAimErrorMult - 1f) * 100f));

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				var p = LevyPackets.New(LevyPacket.DrinkRound);
				p.Write((byte)Drink);
				p.Send();
			}
			else DrinkBuffs.Serve(player.Center, Drink, player.name);
			return true;
		}

		/// <summary>Server side of <see cref="LevyPacket.DrinkRound"/>: serve the round around the sender.</summary>
		internal static void OnPacket(BinaryReader r, int whoAmI)
		{
			int drink = r.ReadByte();
			if (whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active || drink > 2) return;
			DrinkBuffs.Serve(Main.player[whoAmI].Center, drink, Main.player[whoAmI].name);
		}
	}

	public class BarracksAle : DrinkItem { public override int Drink => 0; public override int PriceCopper => Item.buyPrice(silver: 4); public override int Amount => LevyConstants.AleDefense; }
	public class BlackStout : DrinkItem { public override int Drink => 1; public override int PriceCopper => Item.buyPrice(silver: 5); public override int Amount => (int)System.Math.Round(LevyConstants.StoutDamage * 100f); }
	public class SpicedRum : DrinkItem { public override int Drink => 2; public override int PriceCopper => Item.buyPrice(silver: 5); public override int Amount => (int)System.Math.Round(LevyConstants.RumCrit); }

	public class DrinkNet : ModSystem
	{
		public override void Load() => LevyPackets.Register(LevyPacket.DrinkRound, DrinkItem.OnPacket);
	}
}
