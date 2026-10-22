using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Fear;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Globals
{
	/// <summary>Commander lifecycle hooks owned by the roster: death sends the followers home and routs the squad.</summary>
	public class LevyPlayer : ModPlayer
	{
		// Soldier sentries are owned by the commander, so vanilla's turret cap (Player.UpdateMaxTurrets, run inside the commander's own Update) would count and cull them.
		// They are hidden from it for exactly that window: parked before the commander's update, restored after (and again before their own AI as a backstop).
		public override void PreUpdate()
		{
			if (Player.whoAmI == Main.myPlayer) SoldierProjectileTag.ParkSoldierSentries();
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI == Main.myPlayer)
			{
				SoldierProjectileTag.RestoreSoldierSentries();
				SoldierNPC.RedirectOfficerShopTalk(Player); // talk to a deployed officer -> its parked town NPC's shop/chat
			}
		}

		/// <summary>Runs on the server (remote deaths arrive through KillMe) and in single player; clients only report.</summary>
		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			if (LevyUtil.IsClient) return;
			RosterSystem.Instance.OnCommanderDied(Player);
			FearSystem.OnPlayerDied(Player);
		}
	}
}
