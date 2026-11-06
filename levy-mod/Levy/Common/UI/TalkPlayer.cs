using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.UI
{
	/// <summary>
	/// Talk to a levy that is too far for vanilla's chat reach (about 5 tiles) - which is every levy in a fight.
	/// A right-click on one of your own levies beyond that reach calls it over (TalkCall); once it is within reach this opens its chat window for you,
	/// so one click gets you a conversation. Right-clicks inside reach are left to vanilla. Local player only.
	/// </summary>
	public class TalkPlayer : ModPlayer
	{
		private bool _wasRight;
		private int _pendingNpc = -1, _pendingUntil;

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer || Main.dedServ) return;
			int now = (int)Main.GameUpdateCount;
			bool click = Main.mouseRight && !_wasRight;
			_wasRight = Main.mouseRight;

			if (_pendingNpc >= 0) TickPending(now);

			if (!click || Player.dead || Player.talkNPC >= 0 || Player.mouseInterface || UISystem.FocusDesignating || Main.gameMenu || Main.ingameOptionsWindow) return;
			NPC hit = HoveredOwnLevy();
			if (hit == null || InChatReach(hit)) return; // inside reach vanilla opens the chat itself
			if (Vector2.Distance(hit.Center, Player.Center) > LevyConstants.TalkCallMaxTiles * LevyConstants.TilePx) return;
			var rec = (hit.ModNPC as SoldierNPC)?.Record;
			if (rec == null) return;
			UIActions.CallOver(rec);
			_pendingNpc = hit.whoAmI; _pendingUntil = now + LevyConstants.TalkCallTicks;
		}

		private void TickPending(int now)
		{
			NPC n = _pendingNpc < Main.maxNPCs ? Main.npc[_pendingNpc] : null;
			if (n == null || !n.active || n.ModNPC is not SoldierNPC || now > _pendingUntil || Player.dead || Player.talkNPC >= 0) { _pendingNpc = -1; return; }
			if (!InChatReach(n)) return;
			_pendingNpc = -1;
			Main.npcChatCornerItem = 0;
			Main.playerInventory = false;
			Player.SetTalkNPC(n.whoAmI);
			Main.npcChatText = n.GetChat();
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		private NPC HoveredOwnLevy()
		{
			Vector2 m = Main.MouseWorld;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || s.Record.Kind != SoldierKind.Regular || s.OfficerMode) continue;
				if (s.Record.CommanderName != Player.name) continue;
				var box = n.Hitbox; box.Inflate(8, 8);
				if (box.Contains((int)m.X, (int)m.Y)) return n;
			}
			return null;
		}

		/// <summary>The rectangle test vanilla uses to let the player start a chat: the player's reach box against the NPC's hitbox.</summary>
		private bool InChatReach(NPC n)
		{
			var reach = new Rectangle((int)(Player.position.X + Player.width / 2 - Player.tileRangeX * 16), (int)(Player.position.Y + Player.height / 2 - Player.tileRangeY * 16), Player.tileRangeX * 16 * 2, Player.tileRangeY * 16 * 2);
			return reach.Intersects(new Rectangle((int)n.position.X, (int)n.position.Y, n.width, n.height));
		}
	}
}
