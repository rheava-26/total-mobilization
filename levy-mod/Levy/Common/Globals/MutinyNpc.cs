using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Config;
using Levy.Common.Util;

namespace Levy.Common.Globals
{
	/// <summary>
	/// Wage Mutiny: a town NPC robbed by a mutineer stays sour for a day. While it is, its shop prices rise and its mood report says why.
	/// The countdown runs on every machine; the server sends the remaining ticks whenever the NPC is robbed (netUpdate).
	/// </summary>
	public class MutinyNpc : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.townNPC; // fields are only set up on the late pass

		/// <summary>Ticks of bad mood left (0 = fine).</summary>
		public int RobbedTicks;
		public bool IsRobbed => RobbedTicks > 0;

		public void Rob(NPC npc)
		{
			RobbedTicks = LevyConstants.Mutiny.RobbedMoodTicks;
			if (Main.netMode == Terraria.ID.NetmodeID.Server) npc.netUpdate = true;
		}

		public override void PostAI(NPC npc)
		{
			if (RobbedTicks > 0) RobbedTicks--;
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) => binaryWriter.Write(RobbedTicks);

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) => RobbedTicks = binaryReader.ReadInt32();

		/// <summary>The shop opens after the player's shopping settings were computed for this chat; the sour mood is applied once per chat.</summary>
		public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
		{
			if (RobbedTicks <= 0 || !LevyConfig.Get.WageMutiny) return;
			var player = Main.LocalPlayer;
			string tag = LevyUtil.T("Mutiny.RobbedMood");
			string report = player.currentShoppingSettings.HappinessReport ?? "";
			if (report.Contains(tag)) return;
			player.currentShoppingSettings.PriceAdjustment *= LevyConstants.Mutiny.RobbedPriceMult;
			player.currentShoppingSettings.HappinessReport = tag + " " + report;
		}
	}
}
