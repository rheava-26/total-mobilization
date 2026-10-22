using Terraria.ID;

namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Targeting numbers (soldier → enemy choice, enemy → soldier detour). OWNED BY THE TARGETING AGENT.</summary>
		public static class Targeting
		{
			/// <summary>An enemy counts as "attacking us" for this long after it last hurt a player or a soldier.</summary>
			public const int AttackerMemoryTicks = 120;
			/// <summary>While an enemy is locked on a soldier, re-run its vanilla target search this often so a nearer player can win it back.</summary>
			public const int RetargetIntervalTicks = 60;
			/// <summary>Soldiers keep at least this many tiles from a flee target (Dungeon Guardian) and never shoot it.</summary>
			public const int FleeDistanceTiles = 60;
			/// <summary>Old One's Army enemies only consider players within 200 px (vanilla filter); soldiers compete under the same rule.</summary>
			public const float OldOnesSoldierRangePx = 200f;
			/// <summary>
			/// A disguised mimic (ai[0] == 0) wakes when its target comes within about 100 px (chest mimic: its rectangle grown by 100) or 80 px (biome mimic).
			/// A soldier only competes for one this close, so a sleeping mimic is woken by a soldier walking up to it and never hunts one across the map.
			/// </summary>
			public const float MimicWakeSoldierRangePx = 200f;
			/// <summary>A beam-firing swordsman holding a target switches to a nearer enemy only when that one is closer than this fraction of the current distance (stops flapping between near-equals).</summary>
			public const float NearestSwitchRatio = 0.85f;
			/// <summary>Accessory-issue score per point of aggro an accessory adds (Flesh Knuckles +400 = 100, about two defense points). A tie-breaker, never a rank override.</summary>
			public const float AccessoryAggroWeight = 0.25f;

			// ---------- Army-vs-commander aggro weighting (EnemyTargeting.Compete) ----------
			// Vanilla weighs a player as (distance - player.aggro). The soldier side gets the same formula PLUS the standing advantages below, so the army soaks fire
			// and the commander is the exception: he is targeted mainly when alone, when no soldier is near the enemy, or when he is clearly the closest by a wide margin.
			// All of it is a soft distance weighting (px): a far-off army never overrides a nearby commander, and with no soldier in reach the player is picked as before.

			/// <summary>Flat standing aggro every soldier carries (px of distance discount): all else equal, an enemy prefers a soldier to the commander unless the commander is this much closer.</summary>
			public const float SoldierBaseAggroPx = 250f;
			/// <summary>Multiplier on a soldier's POSITIVE gear aggro (Flesh Knuckles +400 counts as 400 x this). Taunt gear on a soldier out-pulls a commander who wears the same gear. Negative (stealth) aggro is used as-is.</summary>
			public const float SoldierTauntMultiplier = 2.5f;
			/// <summary>Soldiers within this radius of the enemy pull collectively: each one adds <see cref="ArmyPullPerSoldierPx"/> to the commander's weighted distance.</summary>
			public const float ArmyPullRadiusPx = 700f;
			/// <summary>Penalty on the commander's weighted distance per soldier inside <see cref="ArmyPullRadiusPx"/> (a cluster of soldiers outweighs one player).</summary>
			public const float ArmyPullPerSoldierPx = 40f;
			/// <summary>Soldiers counted for the collective pull stop here (so the penalty tops out at this x <see cref="ArmyPullPerSoldierPx"/> px).</summary>
			public const int ArmyPullMaxSoldiers = 8;
			/// <summary>Each (enemy, soldier) pair gets a fixed random offset of up to +/- this many px, so enemies spread across the army instead of all picking the single nearest soldier.</summary>
			public const float SpreadJitterPx = 120f;

			// ---------- Event towers (Lunar Pillars) and event hostile projectiles ----------
			/// <summary>
			/// The four Lunar Pillars. They never attack directly; they spawn their event enemies / lightning only while their target is in reach. They are boss-class for loot and
			/// shield purposes but use the swap even without the boss unlock, so soldiers standing at a pillar draw its attacks and share them with the commander.
			/// </summary>
			public static readonly int[] EventTowerTypes = { NPCID.LunarTowerSolar, NPCID.LunarTowerVortex, NPCID.LunarTowerNebula, NPCID.LunarTowerStardust };
			/// <summary>A pillar heals itself when its target is more than 2000 px away, so soldiers only compete for a pillar's attention inside this distance.</summary>
			public const float EventTowerSoldierRangePx = 1800f;
			/// <summary>Chance that an event hostile projectile that locked on the commander is re-aimed at the nearest soldier instead (it always goes to a soldier that is closer than the commander).</summary>
			public const float EventProjectileSoldierShare = 0.6f;
			/// <summary>An event projectile only re-aims at a soldier within this distance of itself.</summary>
			public const float EventProjectileShareRangePx = 1600f;

			// ---------- Combat orders: Focus Attack (a designated world position) ----------
			/// <summary>Hostiles and critters within this many tiles of the focus position are attacked (critters are otherwise never targets).</summary>
			public const float FocusAttackRadiusTiles = 12f;
			/// <summary>A soldier only takes up the focus position when it is within the larger of this and weapon reach x FocusRangeMult of it (tiles).</summary>
			public const float FocusAttackEngageTiles = 60f;
			/// <summary>Designating within this many tiles of the standing focus position clears it instead (tiles).</summary>
			public const float FocusAttackToggleTiles = 2f;
			/// <summary>Size of the stand-in target used to aim and position at a bare spot (px).</summary>
			public const int FocusAttackPhantomPx = 16;
			/// <summary>Step used to walk a focus point out of solid ground toward the shooter (px).</summary>
			public const float FocusAttackExposeStepPx = 8f;

			/// <summary>
			/// AI styles whose vanilla code reads <c>Main.player[target]</c> directly and therefore work with the slot-255 dummy swap.
			/// Verified per style in NPC.cs (see EnemyTargeting.cs header). Bosses and boss parts are excluded separately.
			/// </summary>
			public static readonly int[] SwapAiStyles =
			{
				NPCAIStyleID.Slime, NPCAIStyleID.DemonEye, NPCAIStyleID.Fighter, NPCAIStyleID.Worm, NPCAIStyleID.Caster, NPCAIStyleID.Bat,
				NPCAIStyleID.Piranha, NPCAIStyleID.Vulture, NPCAIStyleID.Jellyfish, NPCAIStyleID.Antlion, NPCAIStyleID.HoveringFighter,
				NPCAIStyleID.Mimic, NPCAIStyleID.BiomeMimic, NPCAIStyleID.Spider, NPCAIStyleID.FlyingFish,
				// Added after reading each AI in NPC.cs: every target read is Main.player[target] (never GetTargetData) and nothing treats 255 as "no target".
				// Unicorn (26: Unicorn, Wolf, Hellhound, Headless Horseman, Evolution Beast, Twinkle, Tumbleweed), Snowman (38: Snowman Gangsta, Mister Stabby,
				// Snow Balla) and GiantTortoise (39: tortoises, Sroller, Giant Shelly) all re-run TargetClosest on their own cadence.
				NPCAIStyleID.Unicorn, NPCAIStyleID.Snowman, NPCAIStyleID.GiantTortoise,
			};

			/// <summary>
			/// Individual types that never use the swap even though their AI style is in <see cref="SwapAiStyles"/>.
			/// Tumbleweed (Unicorn AI) despawns itself unless Main.player[target].ZoneDesert, and the soldier's dummy never stands in a desert zone.
			/// </summary>
			public static readonly int[] SwapExcludedTypes = { NPCID.Tumbleweed };

			// Styles deliberately left out (correctness over coverage), verified in NPC.cs:
			//  * Flying (5: Eater of Souls, Hornets, Meteor Head, Probe, Moth, Parrot, Blood Squid, Servant of Cthulhu) reads its target through GetTargetData(),
			//    which rejects slot 255 (a swapped enemy would idle); with a 300+ index its retarget line evaluates Main.player[target].dead and the hornet branch
			//    Main.player[target].position, both out of range. Only Queen Bee's bees (UsesNewTargetting, TargetClosestNonBees branch) are redirected, natively.
			//  * Herpling (41) returns early when target >= 255, so a swapped enemy would lose its air control.
			//  * Granite Elemental (91) tests target == 255 as "lost target". Every style not named in SwapAiStyles/NativeAiStyles was not audited and stays out.

			/// <summary>
			/// AI styles that read targets through <c>NPC.GetTargetData()</c>; their NPC types carry <c>NPCID.Sets.UsesNewTargetting</c> and are
			/// pointed at a soldier with the native 300+ NPC-target index instead of the swap (Old One's Army fighters/flyers/dark mages, Queen Bee's bees).
			/// </summary>
			public static readonly int[] NativeAiStyles = { NPCAIStyleID.Flying, NPCAIStyleID.DD2Fighter, NPCAIStyleID.DD2Flying, NPCAIStyleID.DD2DarkMage };

			/// <summary>
			/// Styles that can never use the swap even when added through config: their AI goes through GetTargetData(), which rejects
			/// slot 255 (HasPlayerTarget is target &lt; 255), and reads Main.player[target] unguarded for 300+ indices, so neither mechanism is safe.
			/// </summary>
			public static readonly int[] SwapIncompatibleAiStyles = { NPCAIStyleID.Flying };

			/// <summary>
			/// Styles an unlocked boss (BossesTargetSoldiers) still never takes the swap for, on top of <see cref="SwapIncompatibleAiStyles"/>. Empress of Light (120) reads its
			/// target only through GetTargetData() (12 call sites) without carrying UsesNewTargetting, so slot 255 reads as "no target" and she would idle.
			/// </summary>
			public static readonly int[] BossSwapIncompatibleAiStyles = { NPCAIStyleID.EmpressOfLight };

			/// <summary>Boss segments that carry neither <c>boss</c> nor ShouldBeCountedAsBoss but use an allowlisted AI style (Eater of Worlds is Worm AI).</summary>
			public static readonly int[] BossPartTypes = { NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail, NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail };
		}
	}
}
