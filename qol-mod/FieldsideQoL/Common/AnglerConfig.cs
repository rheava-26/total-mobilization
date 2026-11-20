using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FieldsideQoL.Common
{
	/// <summary>Server-side Angler options.</summary>
	public class AnglerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;
		public static AnglerConfig Get => ModContent.GetInstance<AnglerConfig>();

		/// <summary>
		/// On (default): while Wrath of the Gods' rift eclipse is running, the Angler rotates quests normally. Wrath of the Gods' own Veilray quest stays one entry of that rotation.
		/// Off: nothing changes; Wrath of the Gods sets every quest to Veilray during the rift.
		/// </summary>
		[DefaultValue(true)] public bool NormalQuestsDuringRift = true;
	}
}
