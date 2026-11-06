using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Observe
{
	/// <summary>Client-side settings for the camera views (a view is drawn by this player's machine only).</summary>
	public class ObserveConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ObserveConfig Get => ModContent.GetInstance<ObserveConfig>();

		[Header("ArenaView")]
		/// <summary>How many times wider and taller than a normal screen a static camera shows (2 = four times the area). Mouse wheel changes it while connected, up to this value.</summary>
		[Range(1f, 3f)] [Increment(0.25f)] [DefaultValue(2f)] public float ArenaZoomPerSide;
		/// <summary>No dust and no gore while a camera is zoomed out.</summary>
		[DefaultValue(true)] public bool SkipDustAndGoreInArena;
		/// <summary>When you die, take control of one of your levies until you respawn.</summary>
		[DefaultValue(true)] public bool MindControlOnDeath;
	}
}
