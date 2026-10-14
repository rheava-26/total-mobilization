using System;
using Terraria;
using Terraria.Localization;
using Levy.Common.Util;

namespace Levy.Common.Dialogue
{
	/// <summary>Soldier and settlement names. Soldier names come from the vanilla town-NPC name lists (Town.json categories).</summary>
	internal static class DialogueNames
	{
		// Verified against Terraria.Localization.Content.en_US.Town.json: every key below is a top-level category of that file.
		private static readonly string[] MaleCategories =
		{
			"MerchantNames", "ArmsDealerNames", "GuideNames", "ClothierNames", "TaxCollectorNames", "AnglerNames", "PainterNames",
			"DemolitionistNames", "DyeTraderNames", "BartenderNames", "GolferNames", "WizardNames", "PirateNames", "TravelingMerchantNames",
		};

		private static readonly string[] FemaleCategories =
		{
			"NurseNames", "MechanicNames", "StylistNames", "SteampunkerNames", "DryadNames", "PartyGirlNames", "PrincessNames", "BestiaryGirlNames",
		};

		// Used only when no category is available (a stripped or mistranslated language pack).
		private static readonly string[] MaleFallback = { "Andrew", "Wyatt", "Gilbert", "Harold", "Tanner", "Walter", "Cole", "Dante", "Milton", "Asher" };
		private static readonly string[] FemaleFallback = { "Molly", "Hannah", "Claire", "Lola", "Ruby", "Helen", "Holly", "Stella", "Faye", "Ada" };

		private static readonly string[] TownFallback = { "Fort Hallow", "Fort Hope", "Fort Nowhere", "Fort Last", "Millbrook", "Ashford", "Stonegate", "Oakwall" };

		public static string RandomSoldierName(bool male)
		{
			string[] categories = male ? MaleCategories : FemaleCategories;
			// A few attempts: a category can be missing or empty in a language pack.
			for (int attempt = 0; attempt < 4; attempt++)
			{
				string category = categories[Main.rand.Next(categories.Length)];
				if (Language.GetCategorySize(category) <= 0) continue;
				string name = Language.RandomFromCategory(category, Main.rand).Value;
				if (!string.IsNullOrWhiteSpace(name) && !name.EndsWith(".RANDOM", StringComparison.Ordinal)) return name.Trim();
			}
			string[] fallback = male ? MaleFallback : FemaleFallback;
			return fallback[Main.rand.Next(fallback.Length)];
		}

		public static string RandomTownName()
		{
			const string key = "Mods.Levy.Towns.Names";
			string[] names = null;
			if (Language.Exists(key))
				names = Language.GetTextValue(key).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (names == null || names.Length == 0) names = TownFallback;
			return names[Main.rand.Next(names.Length)];
		}
	}
}
