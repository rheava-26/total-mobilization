using System;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// DROWNING (config <c>SoldiersCanDrown</c>, off by default). The player's breath rules, run on the dummy: <c>Player.CheckDrowning</c> as the local player. The breath meter (<c>breath</c> of <c>breathMax</c>) falls one point every
	/// <c>breathCDMax</c> ticks while the head is under water and comes back three points a tick in air; Gills, the Diving Helmet, the Fish Bowl, the Merfolk set and every other piece of breath gear is the dummy's own, set by
	/// the equip pass. At an empty meter the player loses 2 life each time the meter would drop (defense does not count); the levy loses the same and dies of it ("the water") like a player does.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Harness override: -1 follows the config, 0 off, 1 on.</summary>
		internal static int DrownOverride = -1;
		private static bool DrownOn
		{
			get
			{
				if (DrownOverride >= 0) return DrownOverride == 1;
				try { return LevyConfig.Get.SoldiersCanDrown; } catch (Exception) { return false; }
			}
		}

		/// <summary>Harness telemetry: drowning losses taken and the life they cost.</summary>
		internal int DrownHits, DrownDamageTotal;
		/// <summary>The breath meter (the dummy's), for the harness.</summary>
		internal int BreathNow => Dummy?.breath ?? 0;

		private void Drown()
		{
			Player d = Dummy;
			if (!DrownOn || d == null || OfficerMode || LevyUtil.IsClient || !IsAlive) return;
			if (!NPC.wet && d.breath >= d.breathMax) return; // dry with a full meter: nothing to run
			const int Room = 1000000;
			d.statLifeMax2 = Room; d.statLife = Room; d.dead = false; // the dummy's own life is never what drowns: the loss is read off and taken from the body
			try { InFrame(() => d.CheckDrowning()); }
			finally { d.dead = false; }
			int lost = Room - d.statLife;
			d.statLifeMax2 = Math.Max(1, NPC.lifeMax); d.statLife = NPC.life;
			if (lost <= 0) return;
			TicksSinceDamaged = 0; _regenCounter = 0;
			DrownHits++; DrownDamageTotal += lost;
			LastAttackerIndex = -1; LastAttackerType = LevyConstants.EnvKillerDrowning; LastAttackerName = LevyUtil.T("Damage.KillerDrowning");
			if (NPC.life > lost)
			{
				NPC.life -= lost; // Player.CheckDrowning: statLife -= 2, no defense
				if (Record != null) Record.CurrentHp = NPC.life;
				NPC.netUpdate = true;
				return;
			}
			_contactStriking = true;
			try { NPC.SimpleStrikeNPC(NPC.life + Math.Max(0, NPC.defense) * 2 + 10, 0, false, 0f, null, false, 0f, true); }
			finally { _contactStriking = false; }
		}
	}
}
