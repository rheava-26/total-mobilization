namespace Levy.Common
{
	/// <summary>Provisions: army meals, Fizz Pop and Magma Shot (items, buffs and bulk containers). Additive to LevyConstants.</summary>
	public static partial class LevyConstants
	{
		// ---- Fizzy (Fizz Pop / Fizz Tank): low gravity, a double jump, and a body that is carried by every hit ----
		/// <summary>Gravity a Fizzy body falls under, as a fraction of normal: vanilla's own floor for a player at the top of the Space layer (Player.Update scales gravity down to 0.25).</summary>
		public const float FizzGravityMult = 0.25f;
		/// <summary>Knockback a Fizzy body takes, as a multiple of a regular enemy's (+500%).</summary>
		public const float FizzKnockbackMult = 6f;
		/// <summary>Knockback resistance a Fizzy levy body carries (a levy is otherwise immovable, 0): 1 is a plain NPC, the 6x comes on top.</summary>
		public const float FizzKnockbackResist = 1f;
		/// <summary>Knockback strength of a contact hit on a Fizzy levy before the multiple.</summary>
		public const float FizzContactKnockback = 4f;
		/// <summary>A Fizzy body that wears knockback immunity (Cobalt Shield and the like) loses the immunity and takes this fraction less damage instead.</summary>
		public const float FizzImmunityDr = 0.10f;
		/// <summary>Fastest a hit may throw a Fizzy body (px/tick sideways, px/tick up): vanilla caps a launch at 16, which in a quarter gravity is a rise of 60 tiles.</summary>
		public const float FizzMaxLaunchX = 7f, FizzMaxLaunchUp = 4.5f;
		/// <summary>Flight: the AI's movement stays off until the body settles. Longest flight (ticks), what a wall or a ceiling gives back, what a floor gives back, the ground slide, the air drag on x, and the speed below which a grounded body has settled.</summary>
		public const int FizzFlightMaxTicks = 420;
		public const float FizzBounceWall = 0.6f, FizzBounceFloor = 0.45f, FizzGroundSlide = 0.9f, FizzAirDrag = 0.975f, FizzSettleSpeed = 1.2f;

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
