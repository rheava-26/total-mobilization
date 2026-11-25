using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Compat.Wotg;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THE AVATAR OF EMPTINESS FIGHT: the army in the air and in the Avatar's dimensions (Common/Compat/Wotg/AvatarFight.cs says what the boss does to the space). A body owns its movement in the fight in three ways, and
	/// only while <see cref="AvatarFight.Applies"/> (the setting is on and the Avatar or its rift is up):
	///  * <b>Flies</b>: a levy on Follow with wings keeps to an air slot beside the commander, as a player does who has the infinite flight the boss gives every player (the wing tank is full every tick). It takes the firing
	///    position its weapon needs toward the boss when that lies within <see cref="AvLeashTiles"/> of the commander, and goes on shooting.
	///  * <b>Is carried</b>: a levy on Follow with no wings, while the dimension is up (no tile holds anything up for anyone: the world answers "nothing" to every collision query) or while the commander is far above where it can
	///    stand, keeps the same air slot. It has nothing to fly with, so this is the one part with no player parity: it is what stops an army that walked in being left to fall forever.
	///  * <b>Is held</b>: any other levy in the dimension (a Hold or Patrol order, a body in no state to follow) stays where it is, in the air, until the tiles are back.
	/// A body more than <see cref="AvRejoinTiles"/> from its commander in the fight (he was moved to the middle of the world, through a portal, out again) is put in its slot at once when no player could see it do so or
	/// when the commander was moved a moment ago. The hook, the ground route, the dodge's ground manoeuvres and the lava catch are not run while the body is owned (all of them read tiles that no longer hold anything up).
	/// The dodge that remains is the projectile sidestep (<see cref="TickDodgeLegacy"/>), which works aloft. Nothing here is keyed to anything but the fight: with no Avatar every entry is one static check.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Tiles an engaged levy may be drawn from the commander toward its firing position; tiles from the commander at which a body is brought to its slot.</summary>
		private const float AvLeashTiles = 26f, AvRejoinTiles = 45f, AvRejoinFarTiles = 120f;
		/// <summary>Tiles above its own feet the commander must be before a levy with no wings is carried (outside the dimension).</summary>
		private const float AvCarryAboveTiles = 28f;
		/// <summary>The speed and acceleration (px a tick) of a body leaving a marked lane or reaching Solyn's shield: a player with infinite flight crosses 800 px in the 77 ticks the slam's telegraph lasts.</summary>
		private const float AvUrgentSpeedPx = 17f, AvUrgentAccelPx = 1.7f;
		/// <summary>Harness telemetry: ticks a body flew, was carried, was held; slots it was brought to; times the ground took it back.</summary>
		internal static long AvFlyTicks, AvCarryTicks, AvHoldTicks, AvRejoins, AvYields;
		/// <summary>Harness telemetry: why a body was not owned outside the dimension: 0 no record or dead, 1 yielded to the ground movement, 2 not following, 3 the commander is not far above.</summary>
		internal static readonly long[] AvWhy = new long[4];
		private uint _avOwnedTick = uint.MaxValue; private int _avSlot = -1, _avSlotTick = -10000, _avFar, _avYieldUntil, _avProgTick; private float _avProgDist = float.MaxValue; private bool _avWasOwned;

		/// <summary>True when the fight owned this body's movement this tick (the air layer and the other tile-reading layers stand aside).</summary>
		private bool AvatarOwnsTick => _avOwnedTick == Main.GameUpdateCount;
		/// <summary>True when the fight owned this body this tick or last (the brain runs before the movement in a tick).</summary>
		private bool AvatarOwnsRecently => Main.GameUpdateCount - _avOwnedTick <= 8u;

		/// <summary>The slot's offset from the commander (px, centre to centre): a golden-angle ring, wider than tall, a little above him (6 tiles out for the first, about 14 for the fifteenth: the Avatar's area attacks are aimed at him).</summary>
		private Vector2 AvatarSlotOffset(int idx)
		{
			float ang = idx * 2.3999632f;
			float rad = 6f + 2f * (float)Math.Sqrt(idx);
			float spread = AvatarFight.SpreadRadiusTiles;
			// an attack aimed at the commander's own spot: a round ring beyond its reach
			if (spread > 0f) { float r = Math.Max(spread, rad * 1.6f); return new Vector2((float)Math.Cos(ang) * r, (float)Math.Sin(ang) * r) * LevyConstants.TilePx; }
			return new Vector2((float)Math.Cos(ang) * rad * 1.25f, (float)Math.Sin(ang) * rad * 0.55f - 1.5f) * LevyConstants.TilePx;
		}

		/// <summary>This body's place in the army beside the commander, re-read twice a second (the roster walk is not for every tick).</summary>
		private int AvatarSlot()
		{
			int now = (int)Main.GameUpdateCount;
			if (_avSlot < 0 || now - _avSlotTick >= 30) { _avSlot = FollowSlotIndex(); _avSlotTick = now; }
			return _avSlot;
		}

		internal Vector2 AvatarSlotCenter(Player cmd) => cmd.Center + AvatarSlotOffset(AvatarSlot());

		/// <summary>The commander moved this body's way: put it in its slot now (no ground search; the air of the fight is where the army is). A body that cannot fly and is not in the dimension is put on the ground near him when there is some.</summary>
		internal void AvatarRejoin(Player cmd, bool air)
		{
			if (!AvatarFight.Applies || cmd == null) return;
			NPC npc = NPC;
			Vector2 at = AvatarSlotCenter(cmd);
			bool wings = Abilities != null && Abilities.HasWings;
			if (!air && !wings && FollowerTravel.FindSafeGround(cmd.Center, StandingSpotRadiusTiles, out Vector2 ground)) { npc.position = new Vector2(ground.X - npc.width / 2f, ground.Y - npc.height); }
			else npc.position = at - npc.Size / 2f;
			npc.oldPosition = npc.position; npc.velocity = Vector2.Zero;
			_avFar = 0; AvRejoins++;
			NotifyTeleported();
			npc.netUpdate = true;
		}

		/// <summary>0 not owned, 1 flies, 2 carried, 3 held.</summary>
		private int AvatarMode(out Player cmd)
		{
			cmd = null;
			if (Record == null || State == SoldierState.Dead) { AvWhy[0]++; return 0; }
			if (_avYieldUntil > (int)Main.GameUpdateCount && !AvatarFight.Void) { AvWhy[1]++; return 0; }
			bool voidNow = AvatarFight.Void;
			Player c = Commander;
			bool follows = c != null && !c.dead && Record.Order == SoldierOrder.Follow && !Record.StoodDown && State != SoldierState.Routed && State != SoldierState.Desert && State != SoldierState.ReturnHome;
			bool wings = Abilities != null && Abilities.HasWings && Abilities.WingTimeMax > 0;
			if (voidNow) { cmd = c; return follows ? (wings ? 1 : 2) : 3; }
			if (!follows) { AvWhy[2]++; return 0; }
			cmd = c;
			if (wings) return 1;
			// no wings, no dimension: only when the commander is far above anywhere this body can stand (the arena is open sky)
			float above = (NPC.Bottom.Y - c.Bottom.Y) / 16f;
			if (above < AvCarryAboveTiles) { AvWhy[3]++; return 0; }
			return 2;
		}

		/// <summary>The fight let go of this body (it is over, or the body came down to the ground it can use): a body let go in the air comes down as after a teleport, no fall that counts against it.</summary>
		private void AvRelease()
		{
			if (!_avWasOwned) return;
			_avWasOwned = false;
			_fallImmuneTicks = TeleportFallImmunityTicks; _fallStartRow = int.MinValue;
		}

		/// <summary>One tick of the fight's movement. True when it owned the tick (the caller skips the ground movement).</summary>
		private bool AvatarFlightTick()
		{
			if (!AvatarFight.Applies) { AvRelease(); return false; }
			int mode = AvatarMode(out Player cmd);
			NPC npc = NPC; uint now = Main.GameUpdateCount;
			if (mode == 0) { AvRelease(); return false; }
			if (!_avWasOwned) { _avWasOwned = true; HookReset(false); _path.Clear(); _flying = false; _dodgeTicks = 0; _dashTicks = 0; ResetStuck(); _avProgDist = float.MaxValue; _avProgTick = (int)now; }
			_avOwnedTick = now;
			_jumpsUsed = 0; _rocketTime = _kitRocketMax; _drainAcc = 0f;
			bool wings = Abilities != null && Abilities.HasWings;
			if (wings) _wingTime = Abilities.WingTimeMax; // the boss gives every player infinite flight; the army gets the same tank
			if (mode == 3) { AvHoldTicks++; npc.velocity = new Vector2(0f, -Gravity()); return true; }

			Vector2 slot = AvatarSlotCenter(cmd);
			// the commander was moved, or out of sight and far: back in the slot
			float distTiles = Vector2.Distance(npc.Center, cmd.Center) / 16f;
			if (distTiles > (AvatarFight.Zones.Count > 0 ? AvRejoinFarTiles : AvRejoinTiles))   // a body clearing a marked lane is far from the commander on purpose
			{
				bool moved = AvatarFightSystem.MovedRecently(cmd.whoAmI, 600);
				if (++_avFar >= 6 && (moved || AvatarFight.Void || distTiles > AvRejoinFarTiles || !VisibleToAnyPlayer())) { AvatarRejoin(cmd, AvatarFight.Void || mode == 2); slot = AvatarSlotCenter(cmd); }
			}
			else _avFar = 0;

			Vector2 want = slot;
			// engaged: the slot, drawn toward the firing position its weapon needs, never beyond the leash around the commander
			if (State == SoldierState.Engage && TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Weapon != null)
			{
				NPC t = Main.npc[TargetIndex];
				if (t.active)
				{
					float reach = Math.Max(2f, FireRangeTiles(t) * 0.75f) * LevyConstants.TilePx;
					Vector2 toT = t.Center - slot; float dT = toT.Length();
					if (dT > reach) want = slot + toT / dT * (dT - reach);
					Vector2 off = want - cmd.Center; float lim = AvLeashTiles * LevyConstants.TilePx;
					if (off.Length() > lim) want = cmd.Center + off / off.Length() * lim;
				}
			}
			// the attack's own rules: out of a marked lane or column, under Solyn's shield (Common/Compat/Wotg/AvatarFight.cs, Zones)
			bool urgent = false;
			if (AvatarFight.State != null) want = AvatarFight.AdjustForZones(npc, want, AvatarSlot(), cmd, out urgent);
			bool carried = mode == 2;
			if (carried) AvCarryTicks++; else AvFlyTicks++;
			_flying = true; _aerialVisualTicks = AerialVisualHoldTicks;

			// the projectile sidestep works aloft; everything else of the dodge reads tiles
			_flying = false;
			if (TickDodgeLegacy(false, true)) { _flying = true; npc.velocity.Y -= Gravity(); npc.netUpdate = true; return true; }
			_flying = true;

			Vector2 to = want - npc.Center; float d = to.Length();
			float cap = d > 40f * 16f ? 18f : urgent ? AvUrgentSpeedPx : d > 16f * 16f ? 11f : Math.Max(FlySpeed * 1.4f, 7.5f);
			Vector2 desired = d < 4f ? Vector2.Zero : to / d * (urgent ? cap : Math.Min(cap, d * 0.1f + 0.5f));
			float acc = urgent ? AvUrgentAccelPx : d > 16f * 16f ? 0.9f : 0.45f;
			npc.velocity.X += MathHelper.Clamp(desired.X - npc.velocity.X, -acc, acc);
			npc.velocity.Y += MathHelper.Clamp(desired.Y - npc.velocity.Y, -acc, acc);
			npc.velocity.Y -= Gravity(); // UpdateNPC adds gravity after the AI
			if (TargetIndex >= 0 && State == SoldierState.Engage && TargetIndex < Main.maxNPCs && Main.npc[TargetIndex].active) { int f = Main.npc[TargetIndex].Center.X >= npc.Center.X ? 1 : -1; npc.direction = npc.spriteDirection = f; }
			else if (Math.Abs(npc.velocity.X) > 0.6f) { int f = npc.velocity.X > 0f ? 1 : -1; npc.direction = npc.spriteDirection = f; }
			HasMoveGoal = false;
			_blocked = false; _blockedHard = false;
			if (((uint)now + (uint)npc.whoAmI) % (uint)AerialNetSyncTicks == 0) npc.netUpdate = true;

			// outside the dimension the world's own tiles still hold things: a body held against them for long yields to the ground movement for a while
			if (!AvatarFight.Void)
			{
				if (now % 30u == (uint)npc.whoAmI % 30u)
				{
					if (d > 8f * 16f && _avProgDist - d < 2f * 16f && (int)now - _avProgTick >= 90) { _avYieldUntil = (int)now + 180; AvYields++; _avWasOwned = false; _avProgDist = float.MaxValue; _avProgTick = (int)now; }
					else if (_avProgDist - d >= 2f * 16f || d <= 8f * 16f) { _avProgDist = d; _avProgTick = (int)now; }
				}
			}
			return true;
		}
	}
}
