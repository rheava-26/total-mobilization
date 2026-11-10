using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Warbands.Common;

namespace Warbands.Content.Items
{
	/// <summary>
	/// The summoning item of one band type (20 of them, added by Warbands.Load): a Wanted Poster, Challenge Flag, Effigy or Battle Standard crafted from wood and a few cheap materials at a Work Bench. Using it calls that band
	/// 24 to 34 tiles to one side, so any band can be fought on demand. Consumed on use. In multiplayer the client asks the server (packet), which forms the band near that player.
	/// </summary>
	[Autoload(false)]
	public class BandSummonItem : ModItem
	{
		public readonly BandRoster Band; public readonly int Index;
		public BandSummonItem(BandRoster band, int index) { Band = band; Index = index; }
		public override string Name => "Band_" + Band.Id;
		public override string Texture => "Warbands/Content/Items/" + Band.Id;
		protected override bool CloneNewInstances => true;

		public override LocalizedText DisplayName => Language.GetOrRegister("Mods.Warbands.Items." + Name + ".DisplayName", () => Band.ItemName);
		public override LocalizedText Tooltip => Language.GetOrRegister("Mods.Warbands.Items." + Name + ".Tooltip", () => TooltipText(Band));

		/// <summary>1 to 4 short declarative sentences: what it is, reach, the numbers that matter, one special rule.</summary>
		public static string TooltipText(BandRoster b)
		{
			string kit = b.Members[0].Role == 'R' ? "bows and guns" : "staves and blades";
			string s = $"Calls the {b.Name} to 24 to 34 tiles away, four soldiers with {kit}.\n";
			s += b.Warns ? "They warn you off and attack only if you stay.\n" : "They fight you and your levies at once.\n";
			s += "Beating the whole band pays a bounty.";
			return s;
		}

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 1; }

		public override void SetDefaults()
		{
			Item.width = 32; Item.height = 32; Item.maxStack = 20; Item.consumable = true;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = Item.useAnimation = 30; Item.UseSound = SoundID.Item4;
			Item.rare = Band.Stage <= 1 ? ItemRarityID.Blue : Band.Stage <= 2 ? ItemRarityID.Green : Band.Stage <= 3 ? ItemRarityID.LightRed : Band.Stage <= 4 ? ItemRarityID.Pink : Band.Stage <= 5 ? ItemRarityID.Lime : ItemRarityID.Yellow;
			Item.value = Item.buyPrice(silver: 5 + Band.Stage * 12);
		}

		public override bool CanUseItem(Player player) => WarbandsConfig.Get != null && WarbandsConfig.Get.Enabled;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket p = Mod.GetPacket(); p.Write((byte)WarbandsPacket.SummonBand); p.Write((byte)Index); p.Send();
			}
			else BandSpawner.Summon(player, Index);
			return true;
		}

		public override void AddRecipes()
		{
			var r = CreateRecipe();
			foreach (var (item, count) in Band.Mats) { int t = BandData.ItemType(item); if (t > 0) r.AddIngredient(t, count); }
			r.AddTile(TileID.WorkBenches).Register();
		}
	}

	public enum WarbandsPacket : byte { SummonBand = 1 }
}
