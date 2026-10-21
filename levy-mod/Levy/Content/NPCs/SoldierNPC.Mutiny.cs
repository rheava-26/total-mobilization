using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Dialogue;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Wage Mutiny brain (LevyConfig.WageMutiny). While <see cref="MutinySystem.IsMutinous"/> the soldier ignores orders and enemies and goes after his
	/// back pay: half the garrison (by record id) goes for the commander first, the rest rob town NPCs first; each falls back to the other when
	/// his first choice is not there. Server / single player only. Money moves in <see cref="MutinySystem"/>.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _mutinyStrikeTick = -100000, _mutinyShoutTick = -100000, _mutinyRefuseTick = -100000, _mutinyScanTick = -100000, _mutinyCoinsTick = -100000;
		private int _mutinyNpc = -1;
		private bool _mutinyCmdHasCoins;
		private const int MutinyRefuseShoutTicks = 120, MutinyScanTicks = 30, MutinyCoinCheckTicks = 30;

		/// <summary>The mutiny just began: drop what was being hauled, forget the old goal, and shout it.</summary>
		public void OnMutinyBegun()
		{
			DropCarriedItems();
			TargetIndex = -1; ClearGoal(); HoldPosition = false; _idleAnchorSet = false; _haulTargetItem = -1; _mutinyNpc = -1;
			_mutinyShoutTick = (int)Main.GameUpdateCount;
			SetState(SoldierState.Mutiny);
			NPC.netUpdate = true;
		}

		/// <summary>An order reached this mutineer and was refused: a short, rate-limited shout over his head.</summary>
		public void MutinyRefuse()
		{
			int now = (int)Main.GameUpdateCount;
			if (now - _mutinyRefuseTick < MutinyRefuseShoutTicks) return;
			_mutinyRefuseTick = now;
			DialogueSystem.FloatingText(this, LevyUtil.T("Mutiny.Refuse"), DialogueTrigger.MUTINY);
		}

		private void ThinkMutiny(int now)
		{
			TargetIndex = -1; HoldPosition = false;
			SetState(SoldierState.Mutiny);

			if (now - _mutinyShoutTick >= LevyConstants.Mutiny.ShoutIntervalTicks)
			{
				_mutinyShoutTick = now;
				DialogueSystem.FloatingText(this, LevyUtil.T("Mutiny.Cry" + Main.rand.Next(1, 5)), DialogueTrigger.MUTINY);
			}

			var cmd = Commander;
			if (cmd != null && (!cmd.active || cmd.dead)) cmd = null;
			if (now - _mutinyCoinsTick >= MutinyCoinCheckTicks) { _mutinyCoinsTick = now; _mutinyCmdHasCoins = cmd != null && MutinySystem.CoinsOf(cmd) > 0; }
			bool cmdWorth = cmd != null && _mutinyCmdHasCoins;
			NPC victim = FindMutinyVictim(now);

			bool preferCommander = (Record.Id & 1) == 0;
			bool goCommander = cmdWorth && (preferCommander || victim == null);
			if (!goCommander && victim == null) { ClearGoal(); return; } // nobody left to take from: he stands about, furious, until paid

			Vector2 goal = goCommander ? cmd.Bottom : victim.Bottom;
			Vector2 center = goCommander ? cmd.Center : victim.Center;
			float reach = goCommander ? LevyConstants.Mutiny.StrikeReachTiles : LevyConstants.Mutiny.RobReachTiles;
			if (!LevyUtil.WithinTiles(NPC.Center, center, reach)) { SetGoal(goal); return; }

			ClearGoal();
			NPC.direction = NPC.spriteDirection = center.X >= NPC.Center.X ? 1 : -1;
			if (goCommander)
			{
				if (now - _mutinyStrikeTick < LevyConstants.Mutiny.StrikeCooldownTicks) return;
				_mutinyStrikeTick = now;
				DialogueSystem.Emote(this, EmoteID.EmotionAnger);
				MutinySystem.StrikePlayer(this, cmd);
				_mutinyCoinsTick = -100000; // re-read the purse after a strike
			}
			else
			{
				MutinySystem.RobNpc(this, victim);
				_mutinyNpc = -1;
			}
		}

		/// <summary>The nearest town NPC this mutineer can still rob, kept between thinks and rescanned every <see cref="MutinyScanTicks"/>.</summary>
		private NPC FindMutinyVictim(int now)
		{
			if (_mutinyNpc >= 0 && MutinySystem.Robbable(Main.npc[_mutinyNpc])
				&& LevyUtil.WithinTiles(NPC.Center, Main.npc[_mutinyNpc].Center, LevyConstants.Mutiny.NpcSearchTiles)) return Main.npc[_mutinyNpc];
			_mutinyNpc = -1;
			if (now - _mutinyScanTick < MutinyScanTicks) return null;
			_mutinyScanTick = now;
			float best = LevyConstants.Mutiny.NpcSearchTiles * LevyConstants.TilePx; best *= best;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!MutinySystem.Robbable(n)) continue;
				float d = Vector2.DistanceSquared(NPC.Center, n.Center);
				if (d < best) { best = d; _mutinyNpc = i; }
			}
			return _mutinyNpc >= 0 ? Main.npc[_mutinyNpc] : null;
		}
	}
}
