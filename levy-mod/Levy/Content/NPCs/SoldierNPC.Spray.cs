using Terraria;
using Levy.Common;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// The Emptiness Spray's kill. The gas would erase any NPC (active = false: no death, no loot, no roster update); a levy instead dies the way it dies in battle: a lethal strike through NPC.SimpleStrikeNPC, which runs
	/// HitEffect (carried items drop), CheckDead (roster OnBodyDied: death line, respawn timer, deaths count, gear rules by difficulty; an officer's commission ends) and the normal NPC death. Server and single player.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Lab: levies the spray has killed.</summary>
		internal static long SprayKills;

		/// <summary>Kills this body through its normal death path. False when it was not alive or the kill did not land.</summary>
		internal bool KillBySpray()
		{
			if (LevyUtil.IsClient || !IsAlive) return false;
			LastAttackerIndex = -1; LastAttackerType = LevyConstants.EnvKillerSpray; LastAttackerName = LevyUtil.T("Damage.KillerSpray");
			_contactStriking = true;
			try { NPC.SimpleStrikeNPC(100_000_000, 0, false, 0f, null, false, 0f, true); }
			finally { _contactStriking = false; }
			if (IsAlive) { NPC.life = 0; NPC.checkDead(); } // a body that shrugged the strike off (a damage window, a ward) still falls: the spray ignores them
			if (IsAlive) return false;
			SprayKills++;
			return true;
		}
	}
}
