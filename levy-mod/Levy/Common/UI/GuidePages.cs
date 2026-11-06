using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>
	/// The page list of the in-game Field Manual. A page is a name (its text lives at Mods.Levy.Guide.Pages.&lt;Name&gt;.Title / .Body in Localization/en-US_Mods.Levy.Guide.hjson) and an icon item.
	/// The order here is the order in the contents list. <see cref="GuideCheck"/> verifies every key at boot.
	/// </summary>
	public static class GuidePages
	{
		/// <summary>How many pages the book has. GuideCheck fails the boot if the table and this number disagree.</summary>
		public const int ExpectedCount = 30;

		public readonly struct Page
		{
			public readonly string Name;
			/// <summary>"Levy/ItemName" for a Levy item, else a vanilla item id as a number.</summary>
			public readonly string Icon;
			public Page(string name, string icon) { Name = name; Icon = icon; }
			public string TitleKey => "Guide.Pages." + Name + ".Title";
			public string BodyKey => "Guide.Pages." + Name + ".Body";
			public string Title => LevyUtil.T(TitleKey);
			public string Body => LevyUtil.T(BodyKey);

			/// <summary>The icon's item type, or 0 when it does not resolve (the page then shows no icon).</summary>
			public int IconType
			{
				get
				{
					if (Icon == null) return 0;
					if (int.TryParse(Icon, out int vanilla)) return vanilla > 0 && vanilla < ItemLoader.ItemCount ? vanilla : 0;
					return ModContent.TryFind<ModItem>(Icon, out var mi) ? mi.Type : 0;
				}
			}
		}

		private static Page P(string name, string icon) => new(name, icon);
		private static Page V(string name, int vanilla) => new(name, vanilla.ToString());

		public static readonly Page[] All =
		{
			P("Welcome", "Levy/FieldManual"),
			P("Barracks", "Levy/BarracksItem"),
			V("Wages", ItemID.GoldCoin),
			P("Militia", "Levy/WarHorn"),
			P("Orders", "Levy/CommandRadio"),
			P("OrdersList", "Levy/WarHorn"),
			P("BarracksOrders", "Levy/BarracksItem"),
			V("MapOrders", ItemID.GPS),
			P("Armory", "Levy/ArmoryItem"),
			V("ItemsPutIn", ItemID.Chest),
			V("ItemsWeapons", ItemID.Handgun),
			V("ItemsGear", ItemID.GrapplingHook),
			V("ItemsConsumables", ItemID.GreaterHealingPotion),
			V("Rules", ItemID.Book),
			V("Vanity", ItemID.FamiliarShirt),
			V("LogisticsCraft", ItemID.IronAnvil),
			V("LogisticsTrade", ItemID.PlatinumCoin),
			V("LogisticsFind", ItemID.Binoculars),
			V("Reforge", ItemID.GoldBar),
			P("Heart", "Levy/LogisticalCenterItem"),
			V("Officers", ItemID.GoldCrown),
			V("OfficersSupport", ItemID.NurseHat),
			V("OfficersUtility", ItemID.GoldCoin),
			V("OfficersSummoners", ItemID.SlimeStaff),
			P("Potions", "Levy/SpectatorPotion"),
			P("Cameras", "Levy/StaticCameraItem"),
			P("OldOnesArmy", "Levy/LevyStaff"),
			P("EventBooks", "Levy/GuideToConscription"),
			P("ProgressionGuides", "Levy/GuideToFirepower"),
			V("Settings", ItemID.Wrench),
		};
	}

	/// <summary>
	/// Boot self-check of the Field Manual: the page count, that every page's title and body resolve to real text (not a raw key, no stray format braces), that a body is 3 to 6 sentences,
	/// that an icon resolves, and a light style lint. Logged as [GuideCheck] lines; a failure is a FAIL line plus a logged error. Pure data, never throws.
	/// </summary>
	public class GuideCheck : ModSystem
	{
		private static readonly string[] Banned = { "no longer", "used to", "instead of", "unlike", "previously", "formerly" };

		public override void PostSetupRecipes()
		{
			string env = Environment.GetEnvironmentVariable("LEVY_GUIDECHECK");
			if (env == "0" || (!Main.dedServ && env != "1")) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "")
			{
				if (ok) pass++; else { fail++; Mod.Logger.Error($"[GuideCheck] FAIL {name} :: {detail}"); }
			}
			try
			{
				Check("page count", GuidePages.All.Length == GuidePages.ExpectedCount, $"{GuidePages.All.Length} pages, expected {GuidePages.ExpectedCount}");
				Check("chrome strings", Resolves("Guide.Title") && Resolves("Guide.Contents") && Resolves("Guide.Previous") && Resolves("Guide.Next") && Resolves("Guide.PageOf")
					&& Resolves("Guide.CloseHint") && Resolves("Guide.PreviousHint") && Resolves("Guide.NextHint") && Resolves("Guide.StorageHint") && Resolves("Guide.DepositJunkHint"), "a Guide.* chrome key is missing");
				Check("page format", SafeFormat("Guide.PageOf", 3, 30), "Guide.PageOf does not format with two arguments");
				var seen = new System.Collections.Generic.HashSet<string>();
				int lines = 0;
				foreach (var page in GuidePages.All)
				{
					Check(page.Name + " unique", seen.Add(page.Name), "duplicate page name");
					bool t = Resolves(page.TitleKey), b = Resolves(page.BodyKey);
					Check(page.Name + " title", t, page.TitleKey + " does not resolve");
					Check(page.Name + " body", b, page.BodyKey + " does not resolve");
					Check(page.Name + " icon", page.IconType > 0, "icon '" + page.Icon + "' does not resolve");
					if (!t || !b) continue;
					string body = page.Body;
					string[] ls = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
					lines += ls.Length;
					Check(page.Name + " sentences", ls.Length >= 3 && ls.Length <= 6, ls.Length + " lines");
					Check(page.Name + " braces", !body.Contains('{') && !body.Contains('}') && !page.Title.Contains('{'), "stray brace in text");
					string low = body.ToLowerInvariant();
					foreach (string w in Banned) Check(page.Name + " style '" + w + "'", !low.Contains(w), "history/comparison wording");
				}
				Mod.Logger.Info($"[GuideCheck] pages={GuidePages.All.Length} lines={lines}");
			}
			catch (Exception e) { fail++; Mod.Logger.Error("[GuideCheck] FAIL exception :: " + e); }
			Mod.Logger.Info($"[GuideCheck] SUMMARY pass={pass} fail={fail}");
		}

		private static bool Resolves(string key)
		{
			string full = "Mods.Levy." + key;
			if (!Terraria.Localization.Language.Exists(full)) return false;
			string v = Terraria.Localization.Language.GetTextValue(full);
			return !string.IsNullOrWhiteSpace(v) && v != full && !v.StartsWith("Mods.Levy.");
		}

		private static bool SafeFormat(string key, params object[] args)
		{
			try { string s = LevyUtil.T(key, args); return s.Contains(args[0].ToString()) && !s.Contains("{"); } catch (FormatException) { return false; }
		}
	}
}
