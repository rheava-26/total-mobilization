using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Perf
{
	/// <summary>How much of the army's visual effects are drawn. Reduced hides nameplates and thins the dust levy weapons throw; it changes nothing about what the army does.</summary>
	public enum LevyEffectsQuality { Full = 0, Reduced = 1 }

	/// <summary>Client-side settings of the performance analyzer. Field names are saved in the player's config file: never rename or remove one.</summary>
	public class PerfConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static PerfConfig Get => ModContent.GetInstance<PerfConfig>();

		public override void OnLoaded() => PerfAnalyzer.ConfigChanged();
		public override void OnChanged() => PerfAnalyzer.ConfigChanged();

		[Header("Analyzer")]
		/// <summary>Always-on light profiling: a window of numbers every eight seconds in the perf folder next to the recordings. Costs well under a tenth of a millisecond per tick.</summary>
		[DefaultValue(true)] public bool PerfLogAlwaysOn;
		/// <summary>Log files older than this many days are deleted.</summary>
		[Range(1, 90)] [DefaultValue(7)] public int PerfLogDays;
		/// <summary>The perf folder is kept under this size; the oldest files go first.</summary>
		[Range(5, 2000)] [DefaultValue(100)] public int PerfLogMaxMB;
		/// <summary>Times the AI hooks other mods add to NPCs and projectiles, so a mod's cost can be named. Needs a reload.</summary>
		[ReloadRequired] [DefaultValue(true)] public bool MeasureOtherModHooks;

		[Header("LagPopup")]
		/// <summary>After a boss fight that ran badly, a small window says where the time went.</summary>
		[DefaultValue(true)] public bool LagPopup;
		/// <summary>Set by "Don't ask again" in the popup. Turn Lag popup off or this on to stop it.</summary>
		[DefaultValue(false)] public bool LagPopupDontAskAgain;

		[Header("Effects")]
		/// <summary>Reduced hides nameplates and thins the dust levy weapons throw. The army fights exactly the same.</summary>
		[DefaultValue(LevyEffectsQuality.Full)] [DrawTicks] public LevyEffectsQuality LevyEffects;
	}
}
