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

		/// <summary>Chance per real minute, in percent, that a band forms near a player standing in the open. Rolled once per minute per player, only when fewer than the maximum number of bands are out.</summary>
		[Range(0f, 100f)] [DefaultValue(4f)] public float SpawnChancePercentPerMinute;

		[Range(1, 12)] [DefaultValue(3)] public int BandSizeMin;
		[Range(1, 12)] [DefaultValue(5)] public int BandSizeMax;

		/// <summary>Bands out at once across the world.</summary>
		[Range(1, 6)] [DefaultValue(1)] public int MaxActiveBands;

		/// <summary>Chance, in percent, that each piece of a dead band member's real gear (weapon, ammo, armor) drops.</summary>
		[Range(0f, 100f)] [DefaultValue(35f)] public float LootSharePercent;

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
	}
}
