using Terraria;
using Levy.Common;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// ARMORY_GOT: a levy who has just been handed better kit says so over his head. Every gear change in the game (the armory's cadence, an issue, a swap through the barracks) ends in
	/// <see cref="SoldierNPC.NotifyGearChanged"/>, so that is the one place watched. A body remembers what it wore the last time it looked; a change that raises what the worn kit is worth
	/// is a gift. Quiet for a while after the body appears (the muster issue is not a gift), once per levy per few minutes, and once per army per few seconds, so a whole army upgrading on
	/// one cadence is a few cheers and not a wall of text. Server and single player decide; the line reaches clients through the floating-text path.
	/// </summary>
	public partial class SoldierNPC
	{
		private long _gtPrint, _gtValue;
		private uint _gtBorn, _gtLast;
		private static uint _gtArmyLast;
		/// <summary>Harness telemetry: gear lines said since boot.</summary>
		internal static int GearLines;

		private static long GearPrint(SoldierGear g)
		{
			long h = 17;
			void Mix(Item it) { h = h * 31 + (it == null || it.IsAir ? 0 : it.type * 7 + it.prefix); }
			Mix(g.Weapon); Mix(g.Head); Mix(g.Body); Mix(g.Legs);
			if (g.Accessories != null) foreach (Item a in g.Accessories) Mix(a);
			return h;
		}

		/// <summary>A record was just bound to this body: what it wears now is the starting point, not a gift.</summary>
		private void GearTalk_Bound() { _gtBorn = 0; GearTalk_Check(); }

		private void GearTalk_Check()
		{
			try { GearTalk_CheckCore(); }
			catch (System.Exception) { /* a gear line must never throw into a gear change */ }
		}

		private void GearTalk_CheckCore()
		{
			if (LevyUtil.IsClient || Record == null || OfficerMode || Hostile || !IsRegular) return;
			uint now = Main.GameUpdateCount;
			long print = GearPrint(Record.Gear);
			long value = RosterFacts.KitValue(Record);
			if (_gtBorn == 0) { _gtBorn = now == 0 ? 1u : now; _gtPrint = print; _gtValue = value; return; }
			if (print == _gtPrint) return;
			bool better = value > _gtValue;
			_gtPrint = print; _gtValue = value;
			if (!better || now - _gtBorn < (uint)LevyConstants.GearTalkGraceTicks || !IsAlive) return;
			if (_gtLast != 0 && now - _gtLast < (uint)LevyConstants.GearTalkSoldierTicks) return;
			if (_gtArmyLast != 0 && now - _gtArmyLast < (uint)LevyConstants.GearTalkArmyTicks) return;
			if (DialogueSystem.React(this, DialogueTrigger.ARMORY_GOT, new DialogueContext { Player = Record.CommanderName })) { _gtLast = _gtArmyLast = now; GearLines++; }
		}
	}
}
