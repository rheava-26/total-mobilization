using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Warbands.Common
{
	/// <summary>Server-side Warbands options. Everything is a dial; nothing here removes a player's tools.</summary>
	public class WarbandsConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;
		public static WarbandsConfig Get => ModContent.GetInstance<WarbandsConfig>();

		/// <summary>Master switch for the surface spawner. The Lunatic Cultist band has its own switch.</summary>
		[DefaultValue(true)] public bool Enabled;

		/// <summary>No bands until the world reaches this stage: 0 pre-boss, 1 after the Eater of Worlds / Brain of Cthulhu, 2 after Skeletron, 3 early Hardmode, 4 after the mechanical bosses, 5 after Plantera, 6 after Golem.</summary>
		[Range(0, 6)] [DefaultValue(1)] public int FirstStage;

		/// <summary>Chance per real minute, in percent, that a band forms near a player standing in the open. Rolled once per minute per player, only when fewer than the maximum number of bands are out. (Named afresh when the default went from 4 to 12, so a saved config does not hold the old figure.)</summary>
		[Range(0f, 100f)] [DefaultValue(12f)] public float NaturalChancePercentPerMinute;

		/// <summary>No new natural band forms while any boss is alive (a band already out stays and keeps fighting). Summoned bands and event bands are not held back.</summary>
		[DefaultValue(true)] public bool NoSpawnsDuringBosses;

		[Range(1, 12)] [DefaultValue(3)] public int BandSizeMin;
		[Range(1, 12)] [DefaultValue(5)] public int BandSizeMax;

		/// <summary>Bands that may be out at once within 200 tiles of one player. A natural roll for a player is skipped while this many bands are already about.</summary>
		[Range(1, 6)] [DefaultValue(1)] public int MaxActiveBands;

		/// <summary>Bands out at once across the whole world. Bands stay out until beaten, so this is the number of rare enemies a world can hold.</summary>
		[Range(1, 20)] [DefaultValue(6)] public int MaxWorldBands;

		/// <summary>Minutes a band may stand with no player within 220 tiles before it is cleaned up. Bands never despawn otherwise.</summary>
		[Range(5, 240)] [DefaultValue(30)] public int BandCleanupMinutes;

		/// <summary>Band soldiers also say lines from Levy's own fighting dialogue (hit, shaken, routed, victory and so on), mixed with Warbands/Dialogue.txt.</summary>
		[DefaultValue(true)] public bool UseLevyLines;

		/// <summary>Band members drop their own gear (weapon, ammo, armor, hook). Off by default: the reward is the bounty notice.</summary>
		[DefaultValue(false)] public bool GearDrops;

		/// <summary>With gear drops on: chance, in percent, that each piece of a dead band member's real gear (weapon, ammo, armor) drops.</summary>
		[Range(0f, 100f)] [DefaultValue(35f)] public float LootSharePercent;

		/// <summary>Chance, in percent, that a naturally forming band is a neutral one: it ignores you and your levies until one of you hits it. Bands in the Sunken Sea are always neutral. Summoned bands are always hostile.</summary>
		[Range(0f, 100f)] [DefaultValue(90f)] public float NeutralBandPercent;

		/// <summary>Multiplies every band member's life. 1 is the stage table in BandStats.</summary>
		[Range(0.25f, 4f)] [DefaultValue(1f)] public float LifeScale;

		/// <summary>Multiplies every band member's damage. 1 is the stage table in BandStats.</summary>
		[Range(0.25f, 4f)] [DefaultValue(1f)] public float DamageScale;

		/// <summary>Band members run Levy's own soldier brain (movement, hooks, wings, dodging, weapon use). Off: the older Fighter-AI soldiers.</summary>
		[DefaultValue(true)] public bool LevyBrain;

		/// <summary>Soldiers a summoning item calls before the army bonus (one more per ten active levies, at most 8 in all).</summary>
		[Range(1, 8)] [DefaultValue(4)] public int SummonedBandSize;

		/// <summary>The Lunatic Cultist's Archers and Devotees fight as a band: archers strafe and hover, devotees close in and cast.</summary>
		[DefaultValue(true)] public bool CultistBand;

		/// <summary>Event bands: a band that joins a Goblin Army, a Pirate Invasion, a Sandstorm or a Slime Rain while it lasts. Always hostile, part of the event, gone when it ends. Needs the Levy brain.</summary>
		[DefaultValue(true)] public bool EventBands;
		[DefaultValue(true)] public bool EventGoblins;
		[DefaultValue(true)] public bool EventPirates;
		[DefaultValue(true)] public bool EventSandstorm;
		[DefaultValue(true)] public bool EventSlimeRain;

		/// <summary>Seconds between one event band being beaten and the next forming, while the event lasts.</summary>
		[Range(10, 600)] [DefaultValue(90)] public int EventBandCooldownSeconds;

		/// <summary>Most event bands one run of an event sends.</summary>
		[Range(1, 12)] [DefaultValue(4)] public int EventBandMaxPerEvent;
	}
}
