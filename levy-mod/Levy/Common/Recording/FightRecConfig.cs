using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Recording
{
	public enum FightRecMode { Off = 0, BossFights = 1, AllFights = 2 }

	/// <summary>Client-side settings for the automatic fight recorder. Field names are saved in the player's config file: never rename or remove one.</summary>
	public class FightRecConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static FightRecConfig Get => ModContent.GetInstance<FightRecConfig>();

		[Header("Recording")]
		[DefaultValue(FightRecMode.BossFights)] [DrawTicks] public FightRecMode RecordFightsAutomatically;
		/// <summary>Empty = the Levy/Recordings folder inside the tModLoader save folder.</summary>
		[DefaultValue("")] public string RecordingFolder = "";

		/// <summary>While a recording started by the setting above runs, the Levy profiler measures it and the numbers go into the recording. Nothing is printed during the fight; one line follows it.</summary>
		[DefaultValue(true)] public bool ProfileRecordedFights = true;

		[Header("Report")]
		/// <summary>When a boss fight ends, two to four plain lines in chat: how it went, your damage, what killed you, what happened to the army. The full report is a text file next to the recording and is written either way.</summary>
		[DefaultValue(true)] public bool PostFightSummary = true;
		/// <summary>One warning ("The line is breaking!") when most of the army is shaken or falling fast while a boss lives. Shown over your head and once in chat, at most once a minute. It changes nothing about the fight.</summary>
		[DefaultValue(true)] public bool LineBreakingWarning = true;

		[Header("Limits")]
		/// <summary>Largest size one recording may reach on disk (compressed). Recording stops when it is reached.</summary>
		[Range(5, 1000)] [DefaultValue(200)] public int MaxFileSizeMB = 200;
		/// <summary>Total size of the recording folder. The oldest recordings are deleted first when it is exceeded.</summary>
		[Range(100, 100000)] [DefaultValue(2048)] public int MaxFolderSizeMB = 2048;
	}
}
