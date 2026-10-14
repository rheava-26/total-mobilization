using Microsoft.Xna.Framework;

namespace Levy.Common
{
	/// <summary>Roster numbers: deployment cadence, respawn/desertion housekeeping, looks palette. Additive to LevyConstants.</summary>
	public static partial class LevyConstants
	{
		// ---------- Roster: deployment / sync ----------
		/// <summary>How often the roster deploys bodies, resolves commanders and advances respawn timers (ticks).</summary>
		public const int DeployIntervalTicks = 30;
		/// <summary>Periodic roster snapshot to clients so HP/fear/pay state in the UI stays fresh (ticks, server only).</summary>
		public const int SnapshotIntervalTicks = 180;
		/// <summary>A deserter's body is force-despawned this long after it starts walking off (ticks).</summary>
		public const int DesertDespawnTicks = 60 * 20;
		/// <summary>A soldier whose barracks is gone re-bases to the nearest barracks within this many tiles, else goes homeless.</summary>
		public const int HomelessRebaseRadius = 300;
		/// <summary>Followers without a body appear within this many tiles of their commander.</summary>
		public const int SpawnNearCommanderRadius = 20;
		/// <summary>Sanity cap on roster snapshot sizes read from the network.</summary>
		public const int MaxSnapshotRecords = 4096;

		// ---------- Roster: growth ----------
		public static readonly int[] MaxLifeCrystalsByGrade = { MaxLifeCrystalsLevy, MaxLifeCrystalsManAtArms, MaxLifeCrystalsChampion };

		// ---------- Roster: environmental killers ----------
		// CONTRACT-REQUEST: SoldierNPC.LastAttackerType codes for deaths without an attacker (Damage agent): -1 fall, -2 lava, -3 drowning.
		// The roster reads these to pick the "killed by" text; any other non-positive value reads as misadventure.
		public const int EnvKillerFall = -1, EnvKillerLava = -2, EnvKillerDrowning = -3;

		// ---------- Roster: looks rolled at enlistment ----------
		/// <summary>Hair styles available at character creation (the stylist's extra styles are not used).</summary>
		public const int HairStyleCount = 51;
		/// <summary>Skin variants by gender (PlayerVariantID); display-doll variants excluded.</summary>
		public static readonly int[] MaleSkinVariants = { 0, 1, 2, 3, 8 };
		public static readonly int[] FemaleSkinVariants = { 4, 5, 6, 7, 9 };
		public static readonly Color[] HairPalette =
		{
			new(35, 25, 20), new(60, 40, 30), new(110, 70, 40), new(150, 100, 55), new(200, 150, 80), new(230, 200, 120),
			new(160, 60, 40), new(200, 80, 50), new(215, 90, 55), new(90, 90, 95),
		};
		public static readonly Color[] SkinPalette =
		{
			new(255, 125, 90), new(240, 150, 120), new(225, 170, 130), new(200, 140, 100), new(160, 100, 70), new(120, 75, 50), new(90, 55, 40),
		};
		public static readonly Color[] EyePalette = { new(105, 90, 75), new(70, 110, 140), new(60, 120, 80), new(120, 80, 50), new(80, 80, 90) };
		public static readonly Color[] ShirtPalette =
		{
			new(175, 165, 140), new(120, 100, 80), new(90, 110, 140), new(140, 60, 50), new(80, 100, 70), new(150, 130, 90), new(100, 100, 110),
		};
		public static readonly Color[] UnderShirtPalette = { new(160, 180, 215), new(220, 220, 210), new(190, 170, 140), new(140, 140, 150) };
		public static readonly Color[] PantsPalette = { new(255, 230, 175), new(90, 80, 70), new(70, 80, 100), new(120, 100, 80), new(60, 60, 65) };
		public static readonly Color[] ShoePalette = { new(160, 105, 60), new(80, 60, 45), new(50, 45, 40), new(110, 80, 55) };
	}
}
