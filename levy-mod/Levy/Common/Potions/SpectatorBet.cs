using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Potions
{
	/// <summary>
	/// The Spectator's bet. While a Spectator potion is active against a boss, the army is the player's whole stake: when every levy is dead and no manpower is left to field another, the player dies
	/// (<see cref="SpectatorPlayer"/> asks once a tick). Manpower is a barracks pool (BarracksTE.Manpower): a fallen regular returns to duty only while his home pool has someone left,
	/// and militia are raised from the same pools. A soldier in reserve or waiting to deploy still counts as someone who can be fielded.
	/// </summary>
	public static class SpectatorBet
	{
		/// <summary>The number of "Potions.LostBet.BetN" death messages.</summary>
		public const int MessageCount = 5;

		/// <summary>A boss-class NPC that is not an enemy-event tower. Parts and minions count through their boss's own flag.</summary>
		public static bool IsBoss(NPC n)
		{
			if (n == null || n.friendly || n.townNPC) return false;
			int t = n.type;
			if (t < 0 || t >= NPCID.Sets.ShouldBeCountedAsBoss.Length) return false;
			if (!n.boss && !NPCID.Sets.ShouldBeCountedAsBoss[t]) return false;
			return Array.IndexOf(LevyConstants.Targeting.EventTowerTypes, t) < 0;
		}

		/// <summary>Any active boss.</summary>
		public static bool AnyActiveBoss()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
				if (Main.npc[i].active && IsBoss(Main.npc[i])) return true;
			return false;
		}

		// ------------------------------------------------------------------ a boss that leaves
		/// <summary>The number of "Potions.LostBetLeft.LeftN" death messages.</summary>
		public const int LeftMessageCount = 4;
		/// <summary>No boss for this many ticks after one was there: it has left (a boss between two forms or two bodies is not).</summary>
		public const int LeaveGraceTicks = 180;
		/// <summary>A boss that hid a spectator from its AI this recently before leaving left because it had nobody to fight, not because the bet was lost.</summary>
		public const int BlindWindowTicks = 300;
		/// <summary>Game ticks of the last boss death and of the last time a boss AI was handed a spectator it could not see (<see cref="SpectatorNpcHide"/>).</summary>
		internal static uint BossKillTick, BossBlindTick;
		public static void NoteBossKilled() => BossKillTick = Main.GameUpdateCount;
		public static void NoteBossBlind() => BossBlindTick = Main.GameUpdateCount;
		public static void ResetLeave() { BossKillTick = 0; BossBlindTick = 0; }

		/// <summary>What became of a boss fight whose boss is gone.</summary>
		public enum LeaveResult { Won, Blinded, NoArmy, Lost }

		/// <summary>
		/// The verdict for a boss that is no longer there. <paramref name="fightStart"/>: the tick the fight began; <paramref name="goneAt"/>: the tick the last boss was seen. A boss killed since the fight began is a win;
		/// a boss whose AI was handed a spectator it could not see just before it went fled for want of a target, which is not the player's loss; with no army ever fielded there was no bet; otherwise it left on its own
		/// (dawn, its own time running out, a despawn rule) and the bet is lost.
		/// </summary>
		public static LeaveResult LeaveVerdict(uint fightStart, uint goneAt, uint killTick, uint blindTick, bool armySeen)
		{
			if (killTick >= fightStart && killTick > 0) return LeaveResult.Won;
			if (blindTick > 0 && blindTick + BlindWindowTicks >= goneAt && blindTick >= fightStart) return LeaveResult.Blinded;
			if (!armySeen) return LeaveResult.NoArmy;
			return LeaveResult.Lost;
		}

		/// <summary>The death message for a boss that left, with the player's name filled in.</summary>
		public static string LeftMessage(int pick, string playerName) => LevyUtil.T("Potions.LostBetLeft.Left" + (1 + Math.Clamp(pick, 0, LeftMessageCount - 1)), playerName);

		/// <summary>Kills the player with one of the boss-left messages (a random one when <paramref name="pick"/> is negative).</summary>
		public static void KillLeft(Player p, int pick = -1)
		{
			if (pick < 0) pick = Main.rand.Next(LeftMessageCount);
			p.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(LeftMessage(pick, p.name))), 9999.0, 0);
		}

		/// <summary>True while a levy of this player (a regular of his, or any militiaman) is alive on the field.</summary>
		public static bool ArmyAlive(Player p)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive) continue;
				if (s.Record.Kind == SoldierKind.Militia || s.Record.CommanderName == p.name) return true;
			}
			return false;
		}

		/// <summary>True when the player could still put a levy on the field: a soldier of his not yet deployed or in reserve, a fallen regular whose pool still has someone, or a barracks that can raise or reinforce militia.</summary>
		public static bool CanFieldMore(Player p)
		{
			var settle = SettlementSystem.Instance;
			foreach (var r in RosterSystem.Instance.RegularsOf(p))
			{
				if (!r.Dead) { if (!r.HasBody) return true; continue; }
				Vector2 at = r.HomePos != Point16.NegativeOne ? new Vector2((r.HomePos.X + 1.5f) * 16f, (r.HomePos.Y + 1.5f) * 16f) : p.Center;
				var home = settle.GetBarracks(r.HomeBarracksId) ?? settle.NearestBarracks(at, LevyConstants.HomelessRebaseRadius);
				if (home != null && home.Manpower > 0) return true;
			}
			foreach (var b in settle.AllBarracks())
				if (b.Manpower > 0 && (b.Mobilized || settle.CanRaiseMilitia(b))) return true;
			return false;
		}

		/// <summary>The death message for message <paramref name="pick"/> (0 to <see cref="MessageCount"/> - 1), with the player's name filled in.</summary>
		public static string Message(int pick, string playerName) => LevyUtil.T("Potions.LostBet.Bet" + (1 + Math.Clamp(pick, 0, MessageCount - 1)), playerName);

		/// <summary>The lost-bet verdict for one tick: true when the player has lost it (an army that was on the field is wiped and no manpower is left).</summary>
		public static bool Lost(Player p, bool armySeen) => armySeen && !ArmyAlive(p) && !CanFieldMore(p);

		/// <summary>"{name} forfeited."</summary>
		public static string ForfeitMessage(string playerName) => LevyUtil.T("Potions.Forfeit", playerName);

		/// <summary>Kills the player for taking the Spectator off during a boss fight.</summary>
		public static void Forfeit(Player p) => p.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(ForfeitMessage(p.name))), 9999.0, 0);

		/// <summary>Kills the player with one of the lost-bet messages (a random one when <paramref name="pick"/> is negative).</summary>
		public static void Kill(Player p, int pick = -1)
		{
			if (pick < 0) pick = Main.rand.Next(MessageCount);
			p.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(Message(pick, p.name))), 9999.0, 0);
		}
	}
}
