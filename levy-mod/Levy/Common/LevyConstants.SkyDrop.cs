using System.Collections.Generic;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>
	/// Sky-drop weapons: items whose shot is called down from above onto the aim point instead of leaving the wielder (Common/Weapons/SkyDrop.cs). Vanilla hard-codes each one in
	/// Player.ItemCheck_Shoot: the Starfury and Star Wrath stars, the Blizzard and Meteor staves, the Lunar Flare tome, the Daedalus Stormbow and the Blood Rain Bow. A soldier rains them
	/// onto his target, the target standing in for the cursor. To add a weapon, add a row to <see cref="SkyDropWeapons"/>: every number of the spawn geometry is read from the row.
	/// </summary>
	public static partial class LevyConstants
	{
		/// <summary>Which X a row's horizontal spread is measured from.</summary>
		public enum SkyDropAnchor
		{
			/// <summary>Around the aim point (plus half the wielder's width), rolled toward the wielder's back: the Starfury, the staves and the bows.</summary>
			Aim,
			/// <summary>Around the wielder himself, rolled toward his back: Star Wrath.</summary>
			Wielder,
		}

		/// <summary>What a row writes into the projectile's ai[1] (ai[0] is always 0).</summary>
		public enum SkyDropAi1
		{
			None,
			/// <summary>A random animation frame, 0 to <see cref="SkyDropSpec.Ai1Frames"/> - 1 (the Blizzard Staff's hail).</summary>
			Frame,
			/// <summary>A random scale, <see cref="SkyDropSpec.Ai1ScaleMin"/> plus up to <see cref="SkyDropSpec.Ai1ScaleRange"/> (the Meteor Staff's meteor: it blasts 128 px x scale and draws at that scale).</summary>
			Scale,
			/// <summary>The height from which the shot collides with terrain: the target's. Until then it flies through the ground (Starfury, Lunar Flare).</summary>
			TargetY,
			/// <summary>As <see cref="TargetY"/> but no lower than <see cref="SkyDropSpec.Ai1CapAbovePx"/> above the wielder (Star Wrath). If the fall from there is walled off it is the target's height after all.</summary>
			TargetYAboveWielder,
		}

		/// <summary>
		/// One sky-drop weapon's spawn geometry, read from its branch of Player.ItemCheck_Shoot. A field left out of a row keeps the plain default (one projectile, 600 px up, aimed straight at the
		/// target, no scatter), so a row lists only what its branch does.
		/// </summary>
		public sealed class SkyDropSpec
		{
			// ----- how many -----
			/// <summary>Projectiles per cast, at least. <see cref="CountExtra"/> adds a random 0 to N on top.</summary>
			public int Count { get; init; } = 1;
			public int CountExtra { get; init; }
			/// <summary>One more projectile on a 1-in-N roll (0 = never).</summary>
			public int BonusOneIn { get; init; }
			/// <summary>The <see cref="BonusOneIn"/> roll takes one away instead for a heavy arrow (ProjectileID.Sets.FiresFewerFromDaedalusStormbow).</summary>
			public bool FewerWhenHeavy { get; init; }

			// ----- where they start -----
			/// <summary>How far above the higher of the wielder and the aim point the first projectile starts. Vanilla measures from the wielder; a target above him lifts the start so the rain still falls onto it.</summary>
			public float DropHeightPx { get; init; } = 600f;
			/// <summary>Each further projectile starts this much higher than the last (or, with <see cref="StackRandom"/>, a random fraction of it higher than the first).</summary>
			public float StackStepPx { get; init; }
			public bool StackRandom { get; init; }
			public SkyDropAnchor Anchor { get; init; } = SkyDropAnchor.Aim;
			/// <summary>The start's X is rolled 0 to N px from the anchor toward the wielder's back.</summary>
			public int SpreadPx { get; init; }
			/// <summary>That X is then averaged with the wielder's, weighted AimParts : WielderParts (1 : 0 keeps it, 1 : 1 is the midpoint, 10 : 1 stays close to the aim point).</summary>
			public int AimParts { get; init; } = 1;
			public int WielderParts { get; init; }
			/// <summary>Plus a random +-N px.</summary>
			public int ScatterPx { get; init; }
			/// <summary>After the aim is taken from the start, the start itself slides by +-N px (the arrows of the two bows land spread out by it).</summary>
			public int ShiftPx { get; init; }

			// ----- how they fly -----
			/// <summary>Launch speed relative to the weapon's shootSpeed, applied after the scatter below.</summary>
			public float SpeedFactor { get; init; } = 1f;
			/// <summary>The aim's X is nudged by a random +-N times step before it is normalised (the Meteor Staff).</summary>
			public int AimJitter { get; init; }
			public float AimJitterStep { get; init; }
			/// <summary>The launch velocity gets a random +-N times step on each axis after it is normalised.</summary>
			public int JitterX { get; init; }
			public float JitterXStep { get; init; }
			public int JitterY { get; init; }
			public float JitterYStep { get; init; }
			/// <summary>The launch X is then multiplied by a percentage rolled with Random.Next(min, max) (equal values mean none).</summary>
			public int XMulMinPct { get; init; } = 100;
			public int XMulMaxPct { get; init; } = 100;

			// ----- each projectile -----
			/// <summary>When the shot is <see cref="VariantBase"/>, it becomes that type plus a random 0 to Variants - 1 (the Meteor Staff's three sizes).</summary>
			public int Variants { get; init; } = 1;
			public int VariantBase { get; init; }
			public float DamageFactor { get; init; } = 1f;
			public float KnockbackFactor { get; init; } = 1f;
			public SkyDropAi1 Ai1 { get; init; } = SkyDropAi1.None;
			public int Ai1Frames { get; init; }
			public float Ai1ScaleMin { get; init; }
			public float Ai1ScaleRange { get; init; }
			public float Ai1CapAbovePx { get; init; }
			/// <summary>The shot collides with terrain all the way down (arrows, hail, meteors), so its start needs an open path to the target. False for the shots that fly through the ground until <see cref="Ai1"/> says.</summary>
			public bool TerrainBlocks { get; init; }

			// ----- the wielder -----
			/// <summary>The weapon is held pointed this many px above the target while it fires (the two bows point at the sky their arrows fall from).</summary>
			public float HeldAimLiftPx { get; init; }
		}

		/// <summary>Vanilla never lets a sky-drop shot fly flatter than this: the vertical part of its aim is the absolute value, at least this many pixels (Player.ItemCheck_Shoot: if (num3 &lt; 20f) num3 = 20f).</summary>
		public const float SkyDropMinFallPx = 20f;
		/// <summary>A start is kept this far inside the top and the sides of the world: a projectile at or past an edge is deactivated at once (Projectile.Update).</summary>
		public const float SkyDropWorldMarginPx = 64f;
		/// <summary>
		/// When vanilla's start is walled in (a roof or a hill between it and the target) the rain starts straight above the target instead: tried at these fractions of the row's drop height (plus the
		/// projectile's stack), highest first, never lower than <see cref="SkyDropMinOpenHeightPx"/>, and scattered sideways by up to <see cref="SkyDropOpenScatterPx"/>.
		/// </summary>
		public static readonly float[] SkyDropOpenHeightFractions = { 1f, 0.75f, 0.5f, 0.3f };
		public const float SkyDropMinOpenHeightPx = 160f;
		public const int SkyDropOpenScatterPx = 60;
		/// <summary>The open-path test walks a thin ray from the start to the target and looks at a tile every this many pixels (half a tile).</summary>
		public const float SkyDropRayStepPx = 8f;

		/// <summary>
		/// The sky-drop weapons: item type to spawn geometry. Rows are the Player.ItemCheck_Shoot branches, read off the decompiled source (line numbers are Terraria/Player.cs):
		/// vanilla's cursor is the target, its player the soldier, MountedCenter.Y / Center the soldier's centre. Each shot's AI is vanilla's own (Projectile.AI_005 aiStyle 5 for the stars,
		/// aiStyle 1 for the rest; the type-specific terrain gates at Projectile.cs 47803 / 47893 / 47821 / 20630).
		/// </summary>
		public static readonly Dictionary<int, SkyDropSpec> SkyDropWeapons = new()
		{
			// 43706, projToShoot == 9: one star at X = aim + half width - Next(201) * facing, 600 up; damage x1.5, no knockback; aimed straight at the cursor (no scatter).
			// Vanilla leaves ai[1] 0, so its star collides with terrain from the start (Projectile.cs 20652: tileCollide = Bottom.Y >= ai[1]); a soldier's star flies through the ground to the target's height instead.
			[ItemID.Starfury] = new SkyDropSpec
			{
				SpreadPx = 200, AimParts = 1, WielderParts = 0,
				DamageFactor = 1.5f, KnockbackFactor = 0f, Ai1 = SkyDropAi1.TargetY,
			},
			// 44199, item 3065: three stars at X = wielder - Next(0, 401) * facing, 600 up and 100 higher each; vy gets +-40 * 0.02; ai[1] = min(cursor Y, wielder Y - 200) (Projectile.cs 20638: solid from there down).
			[ItemID.StarWrath] = new SkyDropSpec
			{
				Count = 3, StackStepPx = 100f, Anchor = SkyDropAnchor.Wielder, SpreadPx = 400, AimParts = 1, WielderParts = 0,
				JitterY = 40, JitterYStep = 0.02f, Ai1 = SkyDropAi1.TargetYAboveWielder, Ai1CapAbovePx = 200f,
			},
			// 43874, item 3029: three arrows (a heavy arrow loses one on a 1-in-3 roll, any other gains one), X = ((aim + half width - Next(201) * facing) * 10 + wielder) / 11 + Next(-100, 101),
			// 600 up and 150 higher each; +-40 * 0.03 on both axes, X x Next(75, 150)%, then the start slides +-50. The bow is held pointing 1000 px above the cursor (43804).
			[ItemID.DaedalusStormbow] = new SkyDropSpec
			{
				Count = 3, BonusOneIn = 3, FewerWhenHeavy = true, StackStepPx = 150f, SpreadPx = 200, AimParts = 10, WielderParts = 1, ScatterPx = 100, ShiftPx = 50,
				JitterX = 40, JitterXStep = 0.03f, JitterY = 40, JitterYStep = 0.03f, XMulMinPct = 75, XMulMaxPct = 150, TerrainBlocks = true, HeldAimLiftPx = 1000f,
			},
			// 43915, item 4381: Next(1, 3) arrows, +1 on a 1-in-3 roll, X spread Next(61) and +-30, 600 up and up to 150 higher; X +-20 * 0.03, Y +-40 * 0.03, X x Next(55, 80)%, start slides +-50.
			[ItemID.BloodRainBow] = new SkyDropSpec
			{
				Count = 1, CountExtra = 1, BonusOneIn = 3, StackStepPx = 150f, StackRandom = true, SpreadPx = 60, AimParts = 10, WielderParts = 1, ScatterPx = 30, ShiftPx = 50,
				JitterX = 20, JitterXStep = 0.03f, JitterY = 40, JitterYStep = 0.03f, XMulMinPct = 55, XMulMaxPct = 80, TerrainBlocks = true, HeldAimLiftPx = 1000f,
			},
			// 44104, item 1931: two hail shards, X = midpoint of (aim + half width - Next(201) * facing) and the wielder, +-200, 600 up and 100 higher each; +-40 * 0.02 on both axes; ai[1] = frame Next(5).
			[ItemID.BlizzardStaff] = new SkyDropSpec
			{
				Count = 2, StackStepPx = 100f, SpreadPx = 200, AimParts = 1, WielderParts = 1, ScatterPx = 200,
				JitterX = 40, JitterXStep = 0.02f, JitterY = 40, JitterYStep = 0.02f, Ai1 = SkyDropAi1.Frame, Ai1Frames = 5, TerrainBlocks = true,
			},
			// 44135, item 2750: one meteor, the Blizzard Staff's start; aim X +-40 * 0.03 before normalising, vy +-40 * 0.02, all x0.75; type 424 + Next(3); ai[1] = scale 0.5 + U(0, 0.3).
			[ItemID.MeteorStaff] = new SkyDropSpec
			{
				Count = 1, StackStepPx = 100f, SpreadPx = 200, AimParts = 1, WielderParts = 1, ScatterPx = 200,
				AimJitter = 40, AimJitterStep = 0.03f, JitterY = 40, JitterYStep = 0.02f, SpeedFactor = 0.75f,
				Variants = 3, VariantBase = ProjectileID.Meteor1, Ai1 = SkyDropAi1.Scale, Ai1ScaleMin = 0.5f, Ai1ScaleRange = 0.3f, TerrainBlocks = true,
			},
			// 44166, item 3570: three flares, the Blizzard Staff's start, no velocity scatter, half speed; ai[1] = the cursor's Y (Projectile.cs 47823: solid below it).
			[ItemID.LunarFlareBook] = new SkyDropSpec
			{
				Count = 3, StackStepPx = 100f, SpreadPx = 200, AimParts = 1, WielderParts = 1, ScatterPx = 200,
				SpeedFactor = 0.5f, Ai1 = SkyDropAi1.TargetY,
			},
		};
	}
}
