using System;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Net;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// Loads Dialogue/&lt;culture&gt;.txt (falling back to en-US), registers the Bark packet handler and resets dialogue state
	/// with the world. Runs on clients, servers and single player alike: clients need the table for right-click chat.
	/// </summary>
	public class DialogueLoader : ModSystem
	{
		public override void Load()
		{
			LevyPackets.Register(LevyPacket.Bark, DialogueSystem.HandleBarkPacket);
		}

		public override void OnModLoad() => Reload(force: true);

		/// <summary>Fires on a language change and on resource-pack reload: re-read the table for the new culture.</summary>
		public override void OnLocalizationsLoaded() => Reload(force: false);

		public override void OnWorldLoad() => DialogueSystem.ResetRuntime();
		public override void OnWorldUnload() => DialogueSystem.ResetRuntime();

		public override void Unload()
		{
			DialogueTable.Clear();
			DialogueSystem.ResetRuntime();
		}

		private void Reload(bool force)
		{
			string culture = "en-US";
			try { culture = Language.ActiveCulture?.Name ?? "en-US"; }
			catch (Exception) { /* culture not ready: en-US is always present */ }

			if (!force && DialogueTable.LineCount > 0 && DialogueTable.LoadedCulture != "" && IsSameFile(culture)) return;
			DialogueTable.Load(Mod, culture);
			DialogueSystem.ResetRuntime();
		}

		/// <summary>True when the file that would be loaded for this culture is the one already loaded.</summary>
		private bool IsSameFile(string culture)
		{
			string wanted = Mod.FileExists("Dialogue/" + culture + ".txt") ? culture : "en-US";
			return string.Equals(wanted, DialogueTable.LoadedCulture, StringComparison.Ordinal);
		}
	}
}
