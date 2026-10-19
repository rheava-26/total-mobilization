using System.Collections.Generic;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>Thrown-consumable numbers (knives, shuriken, javelins, grenades, bombs, dynamite). Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		// ---------- Reach / carry ----------
		/// <summary>Engagement range (tiles) of a thrown weapon: a lob or toss, well short of a gun.</summary>
		public const int RangeThrown = 20;
		/// <summary>The most of one throwable a soldier carries in his weapon slot (the rest stays on the armory shelf).</summary>
		public const int ThrownCarryMax = 60;
		/// <summary>A soldier walking past an armory with fewer than this fraction of ThrownCarryMax left has the stack topped up; checked at most every ThrownRestockIntervalTicks.</summary>
		public const float ThrownTopUpBelowFraction = 0.34f;
		public const int ThrownRestockIntervalTicks = 120;
		/// <summary>Thrown stacks run dry, so the armory ranks them below an endless gun of the same raw damage.</summary>
		public const float ThrownScoreFactor = 0.3f;

		// ---------- Bombs ----------
		/// <summary>A bomb item carries damage 0: the blast damage is written by the projectile when it goes off (Projectile.PrepareBombToBlow). Used for ranking only.</summary>
		public const int ThrownBlastDamage = 100, ThrownDynamiteBlastDamage = 250;
		public static readonly HashSet<int> ThrownDynamiteItems = new() { ItemID.Dynamite, ItemID.StickyDynamite, ItemID.BouncyDynamite };
		/// <summary>Explosives that place liquid or dirt, or dig, rather than hurt: never thrown by a soldier.</summary>
		public static readonly HashSet<int> ThrownToolBombItems = new() { ItemID.WetBomb, ItemID.LavaBomb, ItemID.HoneyBomb, ItemID.DryBomb, ItemID.DirtBomb, ItemID.DirtStickyBomb };
		/// <summary>Bombs thrown by soldiers still blast enemies, but leave the player's tiles and walls standing.</summary>
		public const bool ThrownBlastsHarmTiles = false;

		// ---------- Throw arc ----------
		/// <summary>Launch speeds (px/tick) tried from slow to fast; the first that lands on the target wins, so close targets get a gentle toss.</summary>
		public static readonly float[] ThrownSpeedSteps = { 7f, 9f, 11f, 13f, 15.5f };
		/// <summary>Launch angles are swept across this many steps over +-ThrownAngleSweepDeg of the straight line to the target.</summary>
		public const int ThrownAngleSteps = 61;
		public const float ThrownAngleSweepDeg = 75f;
		/// <summary>Longest simulated flight (ticks) and the miss distance (px) under which a throw counts as landing on the target.</summary>
		public const int ThrownSimMaxTicks = 150;
		public const float ThrownHitRadiusPx = 22f;
		/// <summary>Projectile aiStyle 113 (sticks to what it hits: Bone Javelin).</summary>
		public const int ThrownStickAiStyle = 113;

		/// <summary>Flight model of a thrown projectile: ticks of straight flight, then gravity per tick, then horizontal air drag per tick.</summary>
		public readonly struct ThrownFlight
		{
			public readonly int Delay; public readonly float Gravity, Drag;
			public ThrownFlight(int delay, float gravity, float drag) { Delay = delay; Gravity = gravity; Drag = drag; }
		}
		/// <summary>Read from Projectile.AI: aiStyle 2 (knives, shuriken, daggers), 1 (javelin), 14 (spiky ball, beenade), 16 (bombs, grenades), 113 (bone javelin).</summary>
		public static readonly ThrownFlight ThrownFlightKnife = new(19, 0.4f, 0.97f);
		/// <summary>The one aiStyle-2 type with its own branch (ProjectileID 162): gravity after 17 straight ticks.</summary>
		public static readonly ThrownFlight ThrownFlightKnife162 = new(17, 0.28f, 0.99f);
		/// <summary>Terminal fall speed (px/tick) vanilla's aiStyle-2 AI clamps velocity.Y to.</summary>
		public const float ThrownKnifeTerminalVelY = 16f;
		public static readonly ThrownFlight ThrownFlightArrow = new(15, 0.10f, 1f);
		public static readonly ThrownFlight ThrownFlightGround = new(15, 0.20f, 1f);
		public static readonly ThrownFlight ThrownFlightBomb = new(5, 0.20f, 1f);
		public static readonly ThrownFlight ThrownFlightGrenade = new(10, 0.20f, 1f);
		public static readonly ThrownFlight ThrownFlightStick = new(45, 0.35f, 0.98f);
	}
}
