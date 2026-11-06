using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Talking to a levy, in or out of combat. Two things happen here:
	/// 1. TALK-HOLD: while a player has this soldier's chat window open (or just called it over), the soldier walks up to that player, stops, faces them and keeps shooting its current target.
	///    It does not pick new targets or chase while the window is open, so a conversation never drags the soldier into a fight and never loses it.
	/// 2. CALL-OVER: a right-click on a far levy (out of vanilla's ~5 tile chat reach) sends <c>TalkCall</c>; the soldier walks to the caller, and the caller's client opens the chat once it is close.
	/// Server / single player logic (Brain_Think runs there); the chat window itself is vanilla's.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _talkCallPlayer = -1;
		private int _talkCallUntil;

		/// <summary>Server: the commander asked for this soldier. It walks to them for <see cref="LevyConstants.TalkCallTicks"/>.</summary>
		public void ReceiveTalkCall(int playerIndex)
		{
			if (Record == null || playerIndex < 0 || playerIndex >= Main.maxPlayers || !Main.player[playerIndex].active) return;
			if (MutinySystem.IsMutinous(Record)) return;
			_talkCallPlayer = playerIndex;
			_talkCallUntil = (int)Main.GameUpdateCount + LevyConstants.TalkCallTicks;
		}

		/// <summary>The player this soldier is being talked to by (chat open) or has been called by, or null.</summary>
		public Player TalkPartner(int now)
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && !p.dead && p.talkNPC == NPC.whoAmI) { _talkCallPlayer = i; _talkCallUntil = now + 60; return p; } // a live chat keeps the hold from lapsing
			}
			if (_talkCallPlayer >= 0 && now < _talkCallUntil)
			{
				Player p = Main.player[_talkCallPlayer];
				if (p.active && !p.dead) return p;
			}
			_talkCallPlayer = -1;
			return null;
		}

		/// <summary>True while a player is talking to (or has called) this soldier.</summary>
		public bool BeingTalkedTo => Record != null && TalkPartner((int)Main.GameUpdateCount) != null;

		/// <summary>
		/// One brain think of the talk-hold. Returns true when it owns the think (the caller skips targeting and orders).
		/// The held target is kept only while it is still valid, so a soldier keeps firing at what it was fighting.
		/// </summary>
		private bool ThinkTalk(int now)
		{
			Player talker = TalkPartner(now);
			if (talker == null) return false;
			if (TargetIndex >= 0 && !TargetStillValid(TargetIndex, AcquireRangeTiles())) TargetIndex = -1;
			float dx = talker.Center.X - NPC.Center.X;
			float tiles = LevyUtil.TileDistance(NPC.Center, talker.Center);
			if (tiles > LevyConstants.TalkStandTiles)
			{
				float side = dx >= 0f ? -1f : 1f; // stand on the side the soldier came from
				SetGoal(talker.Bottom + new Vector2(side * LevyConstants.TalkStandTiles * 0.8f * LevyConstants.TilePx, 0f));
			}
			else ClearGoal();
			if (Math.Abs(dx) > 4f) { NPC.direction = dx > 0f ? 1 : -1; NPC.spriteDirection = NPC.direction; }
			return true;
		}
	}
}
