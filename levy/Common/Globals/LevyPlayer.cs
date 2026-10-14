using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Fear;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Common.Globals
{
	/// <summary>Commander lifecycle hooks owned by the roster: death sends the followers home and routs the squad.</summary>
	public class LevyPlayer : ModPlayer
	{
		/// <summary>Runs on the server (remote deaths arrive through KillMe) and in single player; clients only report.</summary>
		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			if (LevyUtil.IsClient) return;
			RosterSystem.Instance.OnCommanderDied(Player);
			FearSystem.OnPlayerDied(Player);
		}
	}
}
