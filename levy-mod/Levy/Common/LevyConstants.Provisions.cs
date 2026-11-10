namespace Levy.Common
{
	/// <summary>Provisions: army meals, Fizz Pop and Magma Shot (items, buffs and bulk containers). Additive to LevyConstants.</summary>
	public static partial class LevyConstants
	{
		// ---- Fizzy (Fizz Pop / Fizz Tank) ----
		/// <summary>Defense a Fizzy levy loses.</summary>
		public const int FizzDefense = 6;
		/// <summary>A Fizzy levy takes enemy knockback at this fraction of a regular enemy's (levies are otherwise immovable).</summary>
		public const float FizzKnockbackResist = 0.75f;
		/// <summary>Knockback strength of a contact hit on a Fizzy levy.</summary>
		public const float FizzContactKnockback = 4f;

		// ---- Magma Shot / Magma Crucible ----
		/// <summary>Ten minutes of lava immunity.</summary>
		public const int LavaShotDurationTicks = 60 * 60 * 10;

		// ---- Meals ----
		/// <summary>Thirty minutes of Well Fed.</summary>
		public const int MealDurationTicks = 60 * 60 * 30;
		/// <summary>Five minutes of Morale on top of the meal.</summary>
		public const int MealMoraleTicks = 60 * 60 * 5;
		/// <summary>Levies each meal feeds (nearest first): Fish Fry Platter, Fruit Basket, Happy Burger Box, Grand Feast.</summary>
		public static readonly int[] MealFeeds = { 10, 15, 25, 50 };
		/// <summary>Well Fed tier each meal gives: 1 Well Fed, 2 Plenty Satisfied, 3 Exquisitely Stuffed.</summary>
		public static readonly int[] MealWellFedTier = { 1, 1, 2, 3 };
		/// <summary>The Armory serves a meal at most this often (ticks), and checks every MealMessCheckTicks.</summary>
		public const int MealMessCooldownTicks = 60 * 30, MealMessCheckTicks = 60;

		// ---- Recipes ----
		public const int FizzPopsPerBrew = 4, MagmaShotsPerBrew = 5, FizzTankPops = 12, CrucibleShots = 10;
		public const int FishFryFish = 8, FruitBasketFruit = 10, BurgerBoxDishes = 6, FeastDishes = 3;
	}
}
