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

		[Header("Seeker")]
		/// <summary>How bright the Seeker's outlines are. 1 is the brightest; lower lets the ore show through.</summary>
		[Range(0.05f, 1f)] [Increment(0.05f)] [DefaultValue(0.35f)] public float SeekerOutlineBrightness;

		[Header("ChatBars")]
		/// <summary>Under the Wrath of the Gods rift, silences and despairing lines tremble a little on the chat bar.</summary>
		[DefaultValue(true)] public bool ShakingChatText;
		/// <summary>Set once the one-time note about boss dialogue has been shown (Compat/Calamity/BossLines.cs). Turn it off to see the note again.</summary>
		[DefaultValue(false)] public bool BossLineNoteShown;
	}
}
