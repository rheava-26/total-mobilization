using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Util;
using Levy.Common.Vanity;

namespace Levy.Content.Items
{
	/// <summary>
	/// Vanity accessory. While it is worn (accessory slot or vanity slot) this client draws every levy, its held weapon, its projectiles, its summoned units and its officers faint, so a boss fight is easy
	/// to read through the army. Right-click it in the inventory to step its setting through 85%, 95% and 99% transparent (<see cref="LevyConstants.LensOpacities"/>); the setting is saved on the item.
	/// Draw-only: see <see cref="LensDraw"/>. Art: SpectralLens.png.
	/// </summary>
	public class SpectralLens : ModItem
	{
		/// <summary>Index into <see cref="LevyConstants.LensOpacities"/>.</summary>
		public int Setting;

		/// <summary>The setting held in range (a hand-edited or future save can never index past the table).</summary>
		public static int Clamp(int setting) => Math.Clamp(setting, 0, LevyConstants.LensOpacities.Length - 1);

		/// <summary>The setting after one right-click.</summary>
		public static int Next(int setting) => (Clamp(setting) + 1) % LevyConstants.LensOpacities.Length;

		/// <summary>The opacity a setting draws at.</summary>
		public static float OpacityOf(int setting) => LevyConstants.LensOpacities[Clamp(setting)];

		/// <summary>The transparency a setting shows, in whole percent (85, 95, 99).</summary>
		public static int TransparencyPercent(int setting) => (int)Math.Round((1f - OpacityOf(setting)) * 100f);

		public override void SetDefaults()
		{
			Item.width = 32; Item.height = 20; Item.maxStack = 1;
			Item.accessory = true; Item.vanity = true; Item.hasVanityEffects = true;
			Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 50);
		}

		public override void UpdateAccessory(Player player, bool hideVisual) => player.GetModPlayer<SpectralLensPlayer>().Wear(OpacityOf(Setting));

		public override void UpdateVanity(Player player) => player.GetModPlayer<SpectralLensPlayer>().Wear(OpacityOf(Setting));

		// ---- the setting: right-click in the inventory steps it ----
		public override bool CanRightClick() => true;
		public override bool ConsumeItem(Player player) => false;

		public override void RightClick(Player player)
		{
			Setting = Next(Setting);
			if (player.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				SoundEngine.PlaySound(SoundID.MenuTick);
				CombatText.NewText(player.getRect(), new Color(200, 220, 255), LevyUtil.T("Vanity.LensSetTo", TransparencyPercent(Setting)));
			}
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "LensSetting", LevyUtil.T("Vanity.LensSetting", TransparencyPercent(Setting))) { OverrideColor = new Color(170, 210, 255) });
		}

		public override void SaveData(TagCompound tag) { if (Setting != 0) tag["setting"] = (byte)Clamp(Setting); }
		public override void LoadData(TagCompound tag) => Setting = tag.ContainsKey("setting") ? Clamp(tag.GetByte("setting")) : 0;
		public override void NetSend(BinaryWriter writer) => writer.Write((byte)Clamp(Setting));
		public override void NetReceive(BinaryReader reader) => Setting = Clamp(reader.ReadByte());

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(LevyConstants.RecipeGroupAnyGrave, 1)
				.AddIngredient(ItemID.InvisibilityPotion, LevyConstants.LensRecipePotions)
				.AddIngredient(ItemID.Goggles, 1)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
