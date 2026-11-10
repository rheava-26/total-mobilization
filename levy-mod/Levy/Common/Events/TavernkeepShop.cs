using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Content.Items.OOA;

namespace Levy.Common.Events
{
	/// <summary>
	/// The Tavernkeep (DD2 Bartender) sells the three levy sentry staves for Defender Medals (tier 1 always, tier 2 after any mechanical boss, tier 3 after Golem) and the three
	/// drinks for coins. His vanilla shop already fills all 40 slots once Golem is down (3 plain entries + 37 slot-reserving ones), so nothing can be added to it. The Levy wares
	/// live on a SECOND SHOP PAGE (<see cref="PageName"/>, 7 entries, never near the 40-slot limit): the Bartender's second chat button (vanilla "Help") also opens that page.
	/// </summary>
	public class TavernkeepShop : GlobalNPC
	{
		public const string PageName = "LevyWares";
		public static string PageFullName => NPCShopDatabase.GetShopName(NPCID.DD2Bartender, PageName);

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.DD2Bartender;

		/// <summary>Builds the second page. Staves are priced in Defender Medals, drinks in coins; staves are gated by progression.</summary>
		public static NPCShop BuildPage()
		{
			NPCShop shop = new NPCShop(NPCID.DD2Bartender, PageName);
			Item Staff(int type, int tier) { var it = new Item(type); it.shopCustomPrice = LevyConstants.OoaMedalPricePerTier[tier - 1]; it.shopSpecialCurrency = CustomCurrencyID.DefenderMedals; return it; }
			shop.Add(Staff(ModContent.ItemType<LevyRod>(), 1));
			shop.Add(Staff(ModContent.ItemType<LevyCane>(), 2), Condition.DownedMechBossAny);
			shop.Add(Staff(ModContent.ItemType<LevyStaff>(), 3), Condition.DownedGolem);
			shop.Add(ModContent.ItemType<BarracksAle>());
			shop.Add(ModContent.ItemType<BlackStout>());
			shop.Add(ModContent.ItemType<SpicedRum>());
			shop.Add(ModContent.ItemType<Content.Items.Provisions.FizzTank>());
			return shop;
		}

		/// <summary>The second chat button opens the wares page (the vanilla help text still shows).</summary>
		public override void OnChatButtonClicked(NPC npc, bool firstButton)
		{
			if (firstButton || Main.netMode == NetmodeID.Server) return;
			Main.playerInventory = true;
			Main.stackSplit = 9999;
			Main.SetNPCShopIndex(1);
			Main.instance.shop[Main.npcShop].SetupShop(PageFullName, npc);
		}
	}

	/// <summary>Registers the wares page once the vanilla and modded shops exist, and relabels the Bartender's second button.</summary>
	public class TavernkeepShopSystem : ModSystem
	{
		public const string ButtonKey = "UI.BartenderHelp";
		public const string ButtonSuffix = " / Levy Wares";

		public override void AddRecipes()
		{
			if (!NPCShopDatabase.TryGetNPCShop(TavernkeepShop.PageFullName, out _)) TavernkeepShop.BuildPage().Register();
		}

		public override void OnLocalizationsLoaded() => Relabel();
		public override void PostSetupContent() => Relabel();

		private static void Relabel()
		{
			try
			{
				var text = Language.GetText(ButtonKey);
				var set = typeof(LocalizedText).GetMethod("SetValue", BindingFlags.Instance | BindingFlags.NonPublic);
				if (text != null && set != null && !text.Value.EndsWith(ButtonSuffix)) set.Invoke(text, new object[] { text.Value + ButtonSuffix });
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: could not relabel the Tavernkeep button: " + e.Message); }
		}
	}
}
