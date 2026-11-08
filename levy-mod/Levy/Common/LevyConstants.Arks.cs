using System.Collections.Generic;

namespace Levy.Common
{
	/// <summary>Calamity's Ark family (Fractured Ark, True Ark of the Ancients, Ark of the Elements, Ark of the Cosmos): how a soldier swings, parries and empowers them. Additive partial; see ArkSwings.cs.</summary>
	public static partial class LevyConstants
	{
		/// <summary>One Ark's reach and throw numbers, keyed by the Ark's swung-blade projectile class name (found from the item's own Shoot hook).</summary>
		public sealed class ArkNumbers
		{
			/// <summary>Tiles (body centre to the target's hitbox) a swing starts at: a share of the blade's peak reach (hold point 30 px + blade length x its peak scale).</summary>
			public readonly float BlowTiles;
			/// <summary>Tiles a parry stab is thrown at: the stab only exists for 15 ticks, so it is thrown only when the target is well inside the blade.</summary>
			public readonly float ParryTiles;
			/// <summary>Tiles the Big Rip blast reaches (90 px out plus a 242 px line); 0 for an Ark that has none.</summary>
			public readonly float BlastTiles;
			/// <summary>The thrown scissors: the projectile's life in updates and the fraction of it the snap window opens at; 0 for an Ark that never throws.</summary>
			public readonly int ThrowMax; public readonly float SnapStart;
			public ArkNumbers(float blow, float parry, float blast, int throwMax, float snapStart) { BlowTiles = blow; ParryTiles = parry; BlastTiles = blast; ThrowMax = throwMax; SnapStart = snapStart; }
		}

		public static readonly Dictionary<string, ArkNumbers> ArkTable = new()
		{
			["ArkoftheAncientsSwungBlade"] = new ArkNumbers(9f, 4f, 0f, 0, 0f),
			["TrueArkoftheAncientsSwungBlade"] = new ArkNumbers(11f, 5f, 0f, 0, 0f),
			["ArkoftheElementsSwungBlade"] = new ArkNumbers(13f, 6f, 0f, 80, 0.25f),
			["ArkoftheCosmosSwungBlade"] = new ArkNumbers(15f, 6f, 19f, 140, 0.35f),
		};

		/// <summary>Fallback numbers for an Ark-shaped weapon from another build that is not in <see cref="ArkTable"/>.</summary>
		public static readonly ArkNumbers ArkDefault = new ArkNumbers(9f, 5f, 0f, 0, 0f);

		/// <summary>The thrown scissors are held until this far past the opening of the snap window (fraction of the throw), then let go: the snap lands inside the window and well before its end.</summary>
		public const float ArkSnapMargin = 0.05f;
		/// <summary>The Big Rip is thrown when the soldier holds at least this much charge.</summary>
		public const float ArkBlastMinCharge = 4f;
		/// <summary>The parry's immunity window in ticks (Calamity's vanilla parry), plus this much more for the Cross Necklace.</summary>
		public const int ArkParryIFrames = 60, ArkParryIFramesCross = 30;
		/// <summary>A target is worth the Big Rip when it is a boss or has at least this much life.</summary>
		public const int ArkBlastToughLife = 3000;
		/// <summary>The Cosmos parry is cast five times as fast as its swing (Calamity's UseSpeedMultiplier): the cooldown after it is the use time over this.</summary>
		public const float ArkFastParryDivisor = 5f;

		/// <summary>
		/// Sustained single-target damage of an Ark relative to the same swings with no parry, for ranking (ArkSwings.SustainedDps): the share of the time the Ark is empowered, the empowered
		/// swing's damage multiplier and the projectiles each empowered swing throws. Calibrated against the lab (docs/boss_lab/arks): measured damage per second per levy over the plain swing rate.
		/// </summary>
		public static readonly Dictionary<string, float> ArkSustainedMult = new()
		{
			["ArkoftheAncientsSwungBlade"] = 1.75f,
			["TrueArkoftheAncientsSwungBlade"] = 2.4f,
			["ArkoftheElementsSwungBlade"] = 1.8f,
			["ArkoftheCosmosSwungBlade"] = 2.0f,
		};
	}
}
