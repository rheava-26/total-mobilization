using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using H = Levy.Common.LevyConstants.Hooks;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// AIR CONTROL, the last word on a body's velocity each tick (Movement_Update runs it after the walking, flying and dodging layers).
	/// <para/>LAVA: any body in the air whose path ends in lava, in any state (gliding on spent wings, flying, knocked, jumping, falling), casts its hook at the nearest wall or ceiling it can reach and hangs or climbs out;
	/// a winged body with no hook (or none that will latch) hovers or climbs on its wings and leans toward the side with less lava, a glider with nothing left to flap leans the same way. A body already in lava casts out of it.
	/// <para/>WINGS: a winged body alternates holding its wings (a slow, held fall that costs flight time) and letting go (a fast fall): the dodge layer picks one when the threat field says it dodges a projectile or a body.
	/// <para/>HOLD: a hook cast to dodge hangs on the anchor while the danger lasts instead of releasing at the landing point.
	/// </summary>
	public partial class SoldierNPC
	{
		internal static readonly bool AirLavaOn = Environment.GetEnvironmentVariable("LEVY_AIRLAVA") != "0";
		internal static readonly bool HookMoreOn = Environment.GetEnvironmentVariable("LEVY_HOOKMORE") != "0";
		/// <summary>The three parts of "grapple more": shorter triggers (wall, gap, goal above, shortcut, altitude), longer hangs, and the dodge hook (winged bodies too, cheaper, held while the danger lasts). Each has its own switch for the lab.</summary>
		internal static readonly bool HmTrig = HookMoreOn && Environment.GetEnvironmentVariable("LEVY_HM_TRIG") != "0", HmHang = HookMoreOn && Environment.GetEnvironmentVariable("LEVY_HM_HANG") != "0", HmDodge = HookMoreOn && Environment.GetEnvironmentVariable("LEVY_HM_DODGE") != "0", HmHold = HookMoreOn && Environment.GetEnvironmentVariable("LEVY_HM_HOLD") != "0";
		internal static readonly bool WingCtlOn = Environment.GetEnvironmentVariable("LEVY_WINGCTL") != "0";

		private const int LavaLookRows = 24, LavaHookRows = 16, LavaWingRows = 24, WingHoldTicks = 26, WingReleaseTicks = 22;
		private const float EscapeAccelPx = 1.6f, WingReleaseFallPx = 7f, LavaHoverRisePx = 2.2f;
		private int _wingHoldTicks, _wingRelTicks, _lavaScanTick; private uint _lavaCatchAt; private bool _lavaDanger; private int _lavaDist = 99;
		internal int LavaCatches, WingHolds, WingReleases;
		internal uint HookHoldUntil;
		/// <summary>HARNESS: the wings are spent (a glide).</summary>
		internal void LabDrainWings() { _wingTime = 0; }
		/// <summary>Fight telemetry (the lab kit reads and resets these): body-ticks hooked (cast flying or reeling), hanging on purpose, wings held, wings released, lava catches.</summary>
		internal static long StatHookedTicks, StatHangTicks, StatWingHoldTicks, StatWingRelTicks, StatLavaCatches, StatLavaDangerTicks, StatEscapeTicks;

		/// <summary>True while the body falls at the wing's held speed: spent wings glide, and a held wing (the dodge's hold) does the same with flight time left.</summary>
		private bool WingGlideCap => Abilities != null && Abilities.HasWings && !_flying && (_wingTime <= 0 || _wingHoldTicks > 0);

		/// <summary>The fall speed cap the threat field should predict for this body (0 = none).</summary>
		private float PredictedGlideCap => WingCtlOn && WingGlideCap && _wingRelTicks <= 0 ? GlideFallSpeed : 0f;

		/// <summary>The dodge chose a wing hold or release: the air layer carries it out for a few ticks.</summary>
		private void WingDodgeBegin(bool hold)
		{
			if (!WingCtlOn || Abilities == null || !Abilities.HasWings) return;
			if (hold) { _wingHoldTicks = WingHoldTicks; _wingRelTicks = 0; WingHolds++; }
			else { _wingRelTicks = WingReleaseTicks; _wingHoldTicks = 0; WingReleases++; }
		}

		private int _escapeTicks; private Vector2 _bankGoal; private uint _bankAt;

		/// <summary>Powered flight to the nearest standable ground that is not in or beside lava (a wall is flown up, a bank is flown to).</summary>
		private void WingEscape()
		{
			NPC npc = NPC; uint now = Main.GameUpdateCount; StatEscapeTicks++;
			if (now >= _bankAt) { _bankAt = now + 12; if (!FindBank(out _bankGoal)) _bankGoal = new Vector2(npc.Bottom.X, npc.Bottom.Y - 24f * 16f); }
			int dir = _bankGoal.X >= npc.Center.X ? 1 : -1;
			if (Vector2.Distance(npc.Bottom, _bankGoal) < 20f) { _escapeTicks = 0; return; }
			// full thrust at the bank (the walking layer's own pull toward its goal is weaker than this and is overruled)
			Vector2 desired = (_bankGoal - npc.Bottom).SafeNormalize(Vector2.Zero) * FlySpeed;
			npc.velocity.X += MathHelper.Clamp(desired.X - npc.velocity.X, -EscapeAccelPx, EscapeAccelPx);
			npc.velocity.Y += MathHelper.Clamp(desired.Y - npc.velocity.Y, -EscapeAccelPx, EscapeAccelPx);
			npc.velocity.Y -= Gravity();
			_wingTime--; _flying = true; _blocked = false;
			npc.direction = npc.spriteDirection = dir;
			_aerialVisualTicks = AerialVisualHoldTicks; _usedRocket = false; npc.netUpdate = true;
		}

		private bool FindBank(out Vector2 goal)
		{
			goal = default; Point f = TileNav.FeetTile(NPC.Bottom); float best = float.MaxValue; bool found = false;
			for (int x = f.X - 28; x <= f.X + 28; x++)
				for (int y = f.Y - 36; y <= f.Y + 6; y++)
				{
					if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y) || TileNav.HasLava(x, y) || TileNav.HasLava(x, y + 1) || TileNav.HasLava(x - 1, y + 1) || TileNav.HasLava(x + 1, y + 1)) continue;
					float dx = x - f.X, dy = y - f.Y, d = dx * dx + dy * dy + (dy > 0f ? 40f : 0f);   // a bank below is dearer than one beside: the body would have to climb back
					if (d < best) { best = d; goal = TileNav.BottomCentre(x, y); found = true; }
				}
			return found;
		}

		private void AirPost()
		{
			NPC npc = NPC;
			if (npc.life <= 0 || OfficerMode) return;
			bool hookOwns = _hkPhase == HookPhase.InFlight || _hkPhase == HookPhase.Reel;
			if (hookOwns)
			{
				StatHookedTicks++;
				if (_hkPlan != null && (_hkPlan.Hang || Main.GameUpdateCount < HookHoldUntil)) StatHangTicks++;
				_wingHoldTicks = _wingRelTicks = 0; _lavaDanger = false;
				return;
			}
			if (npc.velocity.Y == 0f || npc.wet && !npc.lavaWet) { _wingHoldTicks = _wingRelTicks = 0; _lavaDanger = false; return; }
			if (npc.lavaWet || _clmOn || npc.noGravity) return;
			bool wings = Abilities != null && Abilities.HasWings;

			// ---- lava -----------------------------------------------------------------------------------------
			if (AirLavaOn)
			{
				if (--_lavaScanTick <= 0) { _lavaScanTick = 2; _lavaDanger = LavaAhead(out _lavaDist); }
				if (_lavaDanger)
				{
					StatLavaDangerTicks++;
					uint now = Main.GameUpdateCount;
					if (HookUsable && _hkPhase != HookPhase.InFlight && _hkPhase != HookPhase.Reel && now >= _lavaCatchAt && _lavaDist <= LavaHookRows)
					{
						_lavaCatchAt = now + 8;
						if (SoldierHooks.TakePlanBudget())
						{
							try
							{
								if (_hkPhase == HookPhase.Cooldown || _hkPhase == HookPhase.Ballistic) { _hkPhase = HookPhase.Idle; _hkProj.Clear(); }
								_flying = false;
								if (TryFallHang(new Vector2(npc.Bottom.X, npc.Bottom.Y - 14f * 16f))) { LavaCatches++; StatLavaCatches++; HookLog("lava catch (" + _lavaDist + " tiles of air above lava)"); return; }
							}
							catch (Exception e)
							{
								if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: lava catch failed on soldier '" + SoldierName + "': " + e);
								try { HookReset(true); } catch (Exception) { }
							}
						}
					}
					// no hook out: a body with wings flies for the nearest bank, any body in the air leans toward the side with less lava
					if (wings && _wingTime > 0 && _lavaDist <= LavaWingRows) _escapeTicks = 70;
				}
				if (_escapeTicks > 0)
				{
					_escapeTicks--;
					if (wings && _wingTime > 0) { WingEscape(); return; }
					_escapeTicks = 0;
				}
				if (_lavaDanger)
				{
					int side = LavaFreeSide(TileNav.FeetTile(npc.Bottom));
					if (wings && npc.velocity.Y > GlideFallSpeed) npc.velocity.Y = GlideFallSpeed;   // spent wings still glide
					if (side != 0 && wings)
					{
						float top = Math.Max(3f, FlySpeed * 0.7f);
						npc.velocity.X = MathHelper.Clamp(npc.velocity.X + side * FlyAccel, -top, top);
						npc.direction = npc.spriteDirection = side;
					}
					npc.netUpdate = true;
					return;
				}
			}

			// ---- wings: held or let go ------------------------------------------------------------------------
			if (wings && WingCtlOn)
			{
				if (_wingRelTicks > 0)
				{
					_wingRelTicks--; StatWingRelTicks++;
					if (npc.velocity.Y < WingReleaseFallPx) npc.velocity.Y = Math.Min(WingReleaseFallPx, npc.velocity.Y + 1.2f);
				}
				else if (_wingHoldTicks > 0)
				{
					_wingHoldTicks--; StatWingHoldTicks++;
					if (npc.velocity.Y > GlideFallSpeed) npc.velocity.Y = GlideFallSpeed;
					if (_wingTime > 0) { _wingTime--; _aerialVisualTicks = AerialVisualHoldTicks; _usedRocket = false; }
				}
			}
		}

		/// <summary>
		/// Lava on the way down: walks the rows under the body (at the speed it falls, or glides, drifting sideways as it does) and says so when lava comes before any floor. A body still rising counts too when the arc
		/// the engine predicts for it (gravity and its sideways speed) ends in lava. <paramref name="rows"/> is how many tiles of air lie above the lava.
		/// </summary>
		/// <summary>A flight a planned route asked for keeps its altitude over lava while the flight time left can carry it to the waypoint (with a margin); otherwise it is a body about to sink into lava.</summary>
		private bool PlannedFlightHolds(float vy) => _flying && _pathFromRoute && vy < 1f && HasMoveGoal && _wingTime * 4.5f >= Math.Abs(MoveGoal.X - NPC.Center.X) + 160f;

		private bool LavaAhead(out int rows)
		{
			rows = 99;
			NPC npc = NPC;
			Point feet = TileNav.FeetTile(npc.Bottom);
			float vy = npc.velocity.Y, vx = npc.velocity.X;
			bool glide = WingGlideCap || (Abilities != null && Abilities.HasWings && vy <= GlideFallSpeed + 0.2f);
			if (vy >= 0.3f || glide)
			{
				float speed = Math.Max(1.5f, vy);
				for (int r = 1; r <= LavaLookRows; r++)
				{
					int y = feet.Y + r;
					float t = r * 16f / speed;
					float px = npc.Center.X + vx * Math.Min(t, 45f);
					int c0 = (int)((px - npc.width * 0.4f) / 16f), c1 = (int)((px + npc.width * 0.4f) / 16f);
					bool floorMid = false;
					for (int c = c0; c <= c1; c++)
					{
						if (!TileNav.InBounds(c, y)) return false;
						if (TileNav.HasLava(c, y)) { if (r > 3 && PlannedFlightHolds(vy)) return false; rows = r; return true; }   // a planned flight over lava keeps its altitude
						
						if (TileNav.IsFloor(c, y) || TileNav.IsSolid(c, y)) floorMid = true;
					}
					if (floorMid) return false;
				}
				return false;
			}
			// rising: the arc decides (the same prediction the fall catch uses)
			if (PredictFall(out int ticks, out bool bad) && bad && _predLava) { rows = Math.Max(1, ticks / 6); return true; }
			return false;
		}
	}
}
