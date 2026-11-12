using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// IDLE SITTING AND CHATTING. A Regular that is calm (no target, nothing hostile near, not hurt lately, no walking to do: standing at a Hold post, standing by, or following a commander who stands still) walks to a chair,
	/// bench or throne close by, sits on it the way a player does (the dummy's <c>sitting</c> state, drawn by the vanilla player renderer), and chats with the levies seated near it: one speaks a line from the
	/// idle pool of the dialogue system, with an emote bubble over the listener, every so often. A fight, a threat, a hit, a new order, a talk or the commander walking off stands it up on the spot.
	/// Server logic; clients only mirror the seated flag from the extra AI packet and place the dummy on the seat.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>0 standing, 1 walking to a seat, 2 seated.</summary>
		private int _sitMode;
		private Vector2 _seatBottom; private int _seatDir; private Point _seatTile;
		private int _sitCalmSince, _sitBusyTick = -100000, _sitScanAt, _sitCooldownUntil, _sitWalkSince, _sitChatAt;
		private SoldierOrder _sitOrder;
		private const float SlotSaltSit = 0.61f;
		/// <summary>Client mirror of the seated flag.</summary>
		private bool _sitNet;
		private static readonly int[] SitEmotes = { EmoteID.EmoteLaugh, EmoteID.EmoteHappiness, EmoteID.EmoteConfused, EmoteID.EmoteKiss };

		/// <summary>Harness / config switch: -1 follows the config, 0 off, 1 on.</summary>
		internal static int SitOverride = -1;
		private static bool SitEnabled
		{
			get
			{
				if (SitOverride >= 0) return SitOverride == 1;
				try { return LevyConfig.Get.LeviesSitWhenIdle; } catch (Exception) { return true; }
			}
		}

		/// <summary>True while this body is seated (the server's state; on a client, the mirrored flag).</summary>
		internal bool Seated => LevyUtil.IsClient ? _sitNet : _sitMode == 2;
		internal bool SitWalking => _sitMode == 1;

		private bool Sit_NetFlag() => _sitMode == 2;
		private void Sit_ReadNet(bool seated) => _sitNet = seated;

		/// <summary>Pure rule: a calm levy sits once it has been calm for <paramref name="need"/> ticks.</summary>
		internal static bool SitDue(int calmTicks, int need) => calmTicks >= need;

		/// <summary>Stands the levy up (a no-op when it is standing). Safe to call from any server path: an order, a hit, a fight.</summary>
		internal void Sit_Stop()
		{
			if (_sitMode == 0) return;
			if (_sitMode == 2) NPC.netUpdate = true;
			else if (HasMoveGoal && MoveGoal == _seatBottom) ClearGoal();
			_sitMode = 0; _sitNet = false;
		}

		// ---- per tick (server): a seated body does not move ----
		private bool Sit_Hold()
		{
			if (_sitMode != 2) return false;
			if (TargetIndex >= 0 || !IsAlive || OfficerMode) { Sit_Stop(); return false; } // a target means a fight: up at once, before this tick's movement
			NPC.velocity = Vector2.Zero;
			NPC.Bottom = _seatBottom;
			NPC.direction = _seatDir; NPC.spriteDirection = _seatDir;
			return true;
		}

		// ---- every brain think, before the fight and the orders: bookkeeping and the reasons to stand up ----
		private void Sit_Guard(int now, int fleeIdx, float nearestHostileTiles)
		{
			bool threat = fleeIdx >= 0 || TargetIndex >= 0 || nearestHostileTiles < LevyConstants.SitWakeEnemyTiles || State == SoldierState.Engage
				|| State == SoldierState.Shaken || State == SoldierState.Routed || State == SoldierState.Desert || State == SoldierState.Dead;
			if (threat) _sitBusyTick = now;
			if (_sitMode == 0) return;
			if (threat || !SitEnabled || !IsRegular || OfficerMode || Record == null || MutinySystem.IsMutinous(Record) || Record.Order != _sitOrder || BeingTalkedTo
				|| TicksSinceDamaged < LevyConstants.ThinkIntervalTicks * 2)
			{ Sit_Stop(); return; }
			if (_sitMode == 2 && (!SeatStillValid() || !CommanderStillClose())) Sit_Stop();
		}

		private bool SeatStillValid()
		{
			return PlayerSittingHelper.GetSittingTargetInfo(Dummy, _seatTile.X, _seatTile.Y, out _, out Vector2 pos, out _, out _) && Vector2.DistanceSquared(pos, _seatBottom) < 4f;
		}

		/// <summary>A following levy stays seated only while the commander is close; a Hold or stand-by levy has no commander to follow.</summary>
		private bool CommanderStillClose()
		{
			if (Record.Order != SoldierOrder.Follow) return true;
			Player cmd = Commander;
			if (cmd == null || cmd.dead) return true;
			return LevyUtil.TileDistance(_seatBottom, cmd.Bottom) <= LevyConstants.SitFollowLeaveTiles;
		}

		/// <summary>Called where the brain would otherwise pick an order to follow. True when the levy sits, walks to a seat or is seated (the think is spent).</summary>
		private bool Sit_Think(int now, float nearestHostileTiles)
		{
			if (!SitEnabled || !IsRegular || OfficerMode) { Sit_Stop(); return false; }
			bool calm = Sit_Calm(now, nearestHostileTiles, out Vector2 anchor, out int radius);
			if (!calm)
			{
				if (!_sitSoft) _sitCalmSince = now; // a levy that is only walking (an idle bob, a step into his follow slot) keeps the time he has been out of a fight
				if (_sitMode != 0) Sit_Stop();
				return false;
			}
			if (_sitMode == 2) { Sit_Chat(now); return true; }
			if (_sitMode == 1) return Sit_Walk(now);
			int need = LevyConstants.SitIdleTicks + (int)(SlotFrac(SlotSaltSit) * LevyConstants.SitIdleSpreadTicks);
			if (!SitDue(now - _sitCalmSince, need) || now < _sitCooldownUntil || now < _sitScanAt) return false;
			_sitScanAt = now + LevyConstants.SitScanRetryTicks;
			if (!Sit_FindSeat(anchor, radius, out Point tile, out Vector2 bottom, out int dir)) return false;
			_seatTile = tile; _seatBottom = bottom; _seatDir = dir; _sitOrder = Record.Order;
			_sitMode = 1; _sitWalkSince = now;
			SetGoal(_seatBottom);
			return true;
		}

		/// <summary>Calm, with the anchor and radius to look for a seat around.</summary>
		private bool Sit_Calm(int now, float nearestHostileTiles, out Vector2 anchor, out int radius)
		{
			anchor = NPC.Bottom; radius = LevyConstants.SitIdleRadiusTiles; _sitSoft = false;
			if (Record == null || MutinySystem.IsMutinous(Record) || Shaken || State == SoldierState.Shaken || State == SoldierState.Routed) return SitNo("mutiny/shaken");
			if (TargetIndex >= 0) return SitNo("target");
			if (nearestHostileTiles < LevyConstants.SitWakeEnemyTiles) return SitNo("hostile near");
			if (now - _sitBusyTick < LevyConstants.SitQuietTicks) return SitNo("recent fight");
			if (TicksSinceDamaged < LevyConstants.SitQuietTicks) return SitNo("recent hit");
			if (BeingTalkedTo || Record.Order == SoldierOrder.GoTo || Record.Order == SoldierOrder.Patrol) return SitNo("talk/goto/patrol");
			if (_sitMode != 2 && (NPC.velocity.Y != 0f || NPC.wet || ClimbActive || _hkPhase != HookPhase.Idle)) return SitNo("airborne/wet/climb", true);
			SoldierState os = OrderState();
			if (os == SoldierState.Idle)
			{
				if (_sitMode != 2 && HasMoveGoal && !(_sitMode == 1 && MoveGoal == _seatBottom)) return SitNo("idle but walking", true);
				if (Record.Order == SoldierOrder.Hold) { anchor = Record.HoldPos; radius = LevyConstants.SitHoldRadiusTiles; }
				SitWhy = "calm idle"; return true;
			}
			if (os == SoldierState.Follow)
			{
				Player cmd = Commander;
				if (cmd == null || cmd.dead) return SitNo("no commander");
				if (_sitMode == 2) { if (LevyUtil.TileDistance(NPC.Bottom, cmd.Bottom) > LevyConstants.SitFollowLeaveTiles) return SitNo("commander left"); } // seated: up only when the commander walks well away
				else
				{
					if (cmd.velocity.LengthSquared() > LevyConstants.SitCommanderStillSpeed * LevyConstants.SitCommanderStillSpeed) return SitNo("commander moving");
					if (LevyUtil.TileDistance(NPC.Bottom, cmd.Bottom) > LevyConstants.SitFollowMaxTiles) return SitNo("commander far");
					if (_sitMode == 0 && HasMoveGoal) return SitNo("follow but walking", true);
				}
				anchor = cmd.Bottom; radius = LevyConstants.SitFollowRadiusTiles;
				SitWhy = "calm follow"; return true;
			}
			if (os == SoldierState.FreeRein && Leisure_SitWanted(now, ref anchor, ref radius)) { SitWhy = "free rein pastime"; return true; }
			return SitNo("state " + os);
		}

		/// <summary>Why the last calm check said no (diagnostics for the idle-sitting probe and the harness).</summary>
		internal string SitWhy = "";
		internal int SitModeForProbe => _sitMode;
		private bool _sitSoft;
		private bool SitNo(string why, bool soft = false) { SitWhy = why; _sitSoft = soft; return false; }

		private bool Sit_Walk(int now)
		{
			if (now - _sitWalkSince > LevyConstants.SitWalkTimeoutTicks || !SeatStillValid() || SeatTaken(_seatBottom, this))
			{
				_sitCooldownUntil = now + LevyConstants.SitFailCooldownTicks; Sit_Stop(); return false;
			}
			if (Math.Abs(NPC.Bottom.X - _seatBottom.X) <= LevyConstants.SitArriveXPx && Math.Abs(NPC.Bottom.Y - _seatBottom.Y) <= LevyConstants.SitArriveYPx && NPC.velocity.Y == 0f)
			{
				ClearGoal();
				_sitMode = 2;
				NPC.Bottom = _seatBottom; NPC.velocity = Vector2.Zero; NPC.direction = _seatDir; NPC.spriteDirection = _seatDir;
				_sitChatAt = now + Main.rand.Next(LevyConstants.SitChatMinTicks / 2, LevyConstants.SitChatMaxTicks);
				NPC.netUpdate = true;
				return true;
			}
			SetGoal(_seatBottom);
			return true;
		}

		/// <summary>The nearest free seat inside the radius of the anchor and within a few rows of the levy's feet.</summary>
		private bool Sit_FindSeat(Vector2 anchor, int radiusTiles, out Point tile, out Vector2 bottom, out int dir)
		{
			tile = default; bottom = default; dir = 1;
			if (Dummy == null) return false;
			int ax = (int)(anchor.X / 16f), feetY = (int)(NPC.Bottom.Y / 16f);
			float best = float.MaxValue; bool found = false;
			for (int x = ax - radiusTiles; x <= ax + radiusTiles; x++)
				for (int y = feetY - LevyConstants.SitSeatRows; y <= feetY + LevyConstants.SitSeatRows; y++)
				{
					if (!WorldGen.InWorld(x, y, 5)) continue;
					Tile t = Main.tile[x, y];
					if (!t.HasTile || !TileID.Sets.CanBeSatOnForPlayers[t.TileType]) continue;
					if (!PlayerSittingHelper.GetSittingTargetInfo(Dummy, x, y, out int d, out Vector2 pos, out _, out ExtraSeatInfo extra) || extra.IsAToilet) continue;
					if (Math.Abs(pos.Y - NPC.Bottom.Y) > LevyConstants.SitSeatRows * 16f || Math.Abs(pos.X - anchor.X) > radiusTiles * 16f) continue;
					if (SeatTaken(pos, this)) continue;
					float dist = Math.Abs(pos.X - NPC.Bottom.X) + Math.Abs(pos.Y - NPC.Bottom.Y);
					if (dist >= best) continue;
					best = dist; found = true; tile = new Point(x, y); bottom = pos; dir = d;
				}
			return found;
		}

		/// <summary>Another levy sitting or walking to the spot, a seated town NPC (the town AI's own sitting state) or a seated player.</summary>
		private static bool SeatTaken(Vector2 pos, SoldierNPC me)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) continue;
				if (n.ModNPC is SoldierNPC s)
				{
					if (!ReferenceEquals(s, me) && s._sitMode != 0 && Vector2.DistanceSquared(s._seatBottom, pos) < 64f) return true;
				}
				else if (n.townNPC && n.aiStyle == NPCAIStyleID.Passive && n.ai[0] == 5f && Vector2.DistanceSquared(n.Bottom, pos) < 400f) return true;
			}
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && p.sitting.isSitting && Vector2.DistanceSquared(p.Bottom, pos) < 400f) return true;
			}
			return false;
		}

		// ---- chatting: a seated levy speaks to the nearest seated levy now and then ----
		private void Sit_Chat(int now)
		{
			if (now < _sitChatAt) return;
			_sitChatAt = now + Main.rand.Next(LevyConstants.SitChatMinTicks, LevyConstants.SitChatMaxTicks);
			if (!AnyPlayerNear(LevyConstants.SitChatPlayerTiles)) return;
			SoldierNPC partner = null; float best = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.whoAmI == NPC.whoAmI || n.ModNPC is not SoldierNPC s || s._sitMode != 2) continue;
				float d = Vector2.Distance(n.Center, NPC.Center) / 16f;
				if (d < best && d <= LevyConstants.SitChatTiles) { best = d; partner = s; }
			}
			if (partner == null) partner = NearbyCalmRegular(LevyConstants.SitChatTiles); // nobody else is seated: a standing comrade hears it
			if (partner == null) return;
			var ctx = new DialogueContext { Comrade = partner.SoldierName, Player = Record.CommanderName };
			bool fire = NearLitCampfire(NPC.Center, 8) && Main.rand.Next(100) < 70; // by a fire the talk is fireside talk
			string line = (fire ? DialogueSystem.PickLine(DialogueTrigger.FIRESIDE, Record, ctx) : null) ?? DialogueSystem.PickLine(Main.dayTime ? DialogueTrigger.IDLE : DialogueTrigger.IDLE_NIGHT, Record, ctx) ?? DialogueSystem.PickLine(DialogueTrigger.IDLE, Record, ctx);
			if (!string.IsNullOrEmpty(line)) { DialogueSystem.FloatingText(this, line, fire ? DialogueTrigger.FIRESIDE : DialogueTrigger.IDLE); ChatterLines++; }
			DialogueSystem.Emote(partner, SitEmotes[Main.rand.Next(SitEmotes.Length)]);
			partner._sitChatAt = Math.Max(partner._sitChatAt, now + LevyConstants.SitChatMinTicks / 2); // the listener answers later, not at once
		}

		private bool AnyPlayerNear(float tiles)
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && !p.dead && LevyUtil.WithinTiles(p.Center, NPC.Center, tiles)) return true;
			}
			return false;
		}

		// ---- draw (client and single player): the dummy takes the seat ----
		/// <summary>Puts the dummy in the seated pose when this body is seated (the player renderer draws the sitting legs, the seat offset and the dropped torso), and takes it out again when not.</summary>
		private void Sit_PoseDummy()
		{
			var p = Dummy;
			if (!Seated) { if (p.sitting.isSitting) { p.sitting.isSitting = false; p.sitting.offsetForSeat = Vector2.Zero; p.sitting.sittingIndex = -1; p.sitting.details = default; } return; }
			Point at = (NPC.Bottom + new Vector2(0f, -2f)).ToTileCoordinates();
			if (!PlayerSittingHelper.GetSittingTargetInfo(p, at.X, at.Y, out _, out _, out Vector2 off, out ExtraSeatInfo extra)) { p.sitting.isSitting = false; return; }
			p.sitting.isSitting = true; p.sitting.offsetForSeat = off; p.sitting.details = extra; p.sitting.sittingIndex = 0;
			p.controlLeft = p.controlRight = p.controlJump = false;
		}
	}
}
