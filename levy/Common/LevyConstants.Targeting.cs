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
			/// AI styles whose vanilla code reads <c>Main.player[target]</c> directly and therefore work with the slot-255 dummy swap.
			/// Verified per style in NPC.cs (see EnemyTargeting.cs header). Bosses and boss parts are excluded separately.
			/// </summary>
			public static readonly int[] SwapAiStyles =
			{
				NPCAIStyleID.Slime, NPCAIStyleID.DemonEye, NPCAIStyleID.Fighter, NPCAIStyleID.Worm, NPCAIStyleID.Caster, NPCAIStyleID.Bat,
				NPCAIStyleID.Piranha, NPCAIStyleID.Vulture, NPCAIStyleID.Jellyfish, NPCAIStyleID.Antlion, NPCAIStyleID.HoveringFighter,
				NPCAIStyleID.Mimic, NPCAIStyleID.Spider, NPCAIStyleID.FlyingFish,
			};

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

			/// <summary>Boss segments that carry neither <c>boss</c> nor ShouldBeCountedAsBoss but use an allowlisted AI style (Eater of Worlds is Worm AI).</summary>
			public static readonly int[] BossPartTypes = { NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail, NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail };
		}
	}
}
