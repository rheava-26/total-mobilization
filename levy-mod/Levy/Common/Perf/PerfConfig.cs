using System.ComponentModel;
using Newtonsoft.Json;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Perf
{
	/// <summary>How much of the army's visual effects are drawn. Reduced hides nameplates and thins the dust levy weapons throw; it changes nothing about what the army does.</summary>
	public enum LevyEffectsQuality { Full = 0, Reduced = 1 }

	/// <summary>The game's own Frame Skip setting, shown in Levy's settings (the order the choices are listed in).</summary>
	public enum FrameSkipChoice { On = 0, Subtle = 1, Off = 2 }

	/// <summary>Client-side settings of the performance analyzer. Field names are saved in the player's config file: never rename or remove one.</summary>
	public class PerfConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static PerfConfig Get => ModContent.GetInstance<PerfConfig>();

		public override void OnLoaded() => PerfAnalyzer.ConfigChanged();
		public override void OnChanged() { PerfAnalyzer.ConfigChanged(); global::Levy.Common.UI.UIActions.SendActiveArmy(false); }

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
		/// <summary>Set by "Don't ask again" in the note that says the army is more than this computer can run smoothly.</summary>
		[DefaultValue(false)] public bool ArmyNoticeDontAskAgain;

		[Header("Effects")]
		/// <summary>Reduced hides nameplates and thins the dust levy weapons throw. The army fights exactly the same.</summary>
		[DefaultValue(LevyEffectsQuality.Full)] [DrawTicks] public LevyEffectsQuality LevyEffects;

		[Header("Speed")]
		/// <summary>
		/// How often a fighting levy rebuilds its gear's stats (the equipment pass: ResetEffects, buffs, equips, armor sets), in ticks. 1 rebuilds every tick, as a player does. Above 1 the stats of the last rebuild
		/// are kept in between while the per-tick part (Rage, Adrenaline, stealth, cooldowns) still runs every tick, and a change of gear, buffs, held weapon or a life step rebuilds at once (SoldierNPC.GearApprox.cs).
		/// </summary>
		[Range(1, 8)] [DefaultValue(Content.NPCs.SoldierNPC.GearApproxDefaultInterval)] [Slider] public int GearUpdateIntervalWhenFighting;

		/// <summary>
		/// Active army size: how many of this player's Regulars fight at once (0: all of them). The rest wait on active duty, and when one in the field falls the next takes his place at once (RosterSystem.ActiveArmy.cs).
		/// A setting of this computer, like Frame Skip: sent to the server when the player enters a world and whenever it changes. Also set from the Barracks and the lag notes.
		/// </summary>
		[Range(0, LevyConstants.ActiveArmyMax)] [DefaultValue(0)] public int ActiveArmySize;

		/// <summary>The game's own Frame Skip setting (Settings, Video), read and written here and saved the game's way. Not stored in this config.</summary>
		[JsonIgnore] [ShowDespiteJsonIgnore] [DrawTicks]
		public FrameSkipChoice FrameSkip
		{
			get => Main.FrameSkipMode switch { Terraria.Enums.FrameSkipMode.Subtle => FrameSkipChoice.Subtle, Terraria.Enums.FrameSkipMode.Off => FrameSkipChoice.Off, _ => FrameSkipChoice.On };
			set => SetFrameSkip(value);
		}

		/// <summary>Sets the game's Frame Skip and saves the game's settings, as the Video menu does. Nothing is saved on a server.</summary>
		internal static void SetFrameSkip(FrameSkipChoice v)
		{
			var mode = v switch { FrameSkipChoice.Subtle => Terraria.Enums.FrameSkipMode.Subtle, FrameSkipChoice.Off => Terraria.Enums.FrameSkipMode.Off, _ => Terraria.Enums.FrameSkipMode.On };
			if (Main.FrameSkipMode == mode) return;
			Main.FrameSkipMode = mode;
			if (!Main.dedServ) { try { Main.SaveSettings(); } catch (System.Exception e) { Levy.Instance?.Logger.Warn("Levy: saving the Frame Skip setting failed: " + e.Message); } }
		}
	}
}
