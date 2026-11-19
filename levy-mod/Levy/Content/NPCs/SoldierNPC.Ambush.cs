using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Compat.Calamity;
using Levy.Common.Movement;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THE ROGUE AMBUSH CYCLE (see <see cref="Ambush"/>). Runs in the engage think before the ranged / melee goal: FALL BACK to a safe charge spot, CHARGE standing still, STRIKE (close to reach, the strike
	/// and a couple of plain throws), fall back again. The fire gate reads <see cref="AmbushGate"/>; Rage and Adrenaline wait for <see cref="AmStrikeWindow"/>. Server / single player only.
	/// </summary>
	public partial class SoldierNPC
	{
		internal AmbushPhase AmPhase;
		private int _amPhaseAt, _amLastTick = -9999, _amTarget = -1, _amPlainLeft, _amStrikeAt, _amOffUntil;
		private uint _amUseSeen;
		private Vector2 _amSpot; private bool _amSpotOk, _amBarWasReady, _amThrown, _amCameFromCharge;
		/// <summary>True while the levy is in the strike window and in reach of its target (the moment to switch Rage / Adrenaline on).</summary>
		internal bool AmStrikeWindow;
		/// <summary>Harness telemetry: completed cycles (fall back, charge, strike, fall back), strikes thrown in the window, and the last phases entered (oldest first, at most 48).</summary>
		internal int AmCycles, AmStrikes;
		internal readonly List<AmbushPhase> AmTrace = new(48);
		private static readonly List<Threat> _amNear = new(16);

		/// <summary>The cycle is live this tick (it ran in the last think).</summary>
		private bool AmLive => AmPhase != AmbushPhase.Off && (int)Main.GameUpdateCount - _amLastTick <= 2 * LevyConstants.ThinkIntervalTicks;

		private void AmSet(AmbushPhase p, int now)
		{
			if (p == AmPhase) return;
			if (p == AmbushPhase.FallBack) { Ambush.FallBacks++; if (AmPhase == AmbushPhase.Strike && _amThrown && _amCameFromCharge) { AmCycles++; Ambush.Cycles++; } _amSpotOk = false; _amCameFromCharge = false; }
			else if (p == AmbushPhase.Charge) { Ambush.Charges++; _amCameFromCharge = AmPhase == AmbushPhase.FallBack || _amCameFromCharge; }
			else if (p == AmbushPhase.Strike) { Ambush.Strikes++; _amThrown = false; _amPlainLeft = 0; _amCameFromCharge = AmPhase == AmbushPhase.Charge && _amCameFromCharge; }
			AmPhase = p; _amPhaseAt = now; AmStrikeWindow = false;
			if (AmTrace.Count >= 48) AmTrace.RemoveAt(0);
			AmTrace.Add(p);
		}

		/// <summary>The weapon and target want the ambush (a rogue with a stealth bar whose weapon's strike is worth the wait here). Out: Calamity's player for the bar.</summary>
		private bool AmbushWanted(NPC target, Item item, out object cp)
		{
			cp = null;
			if (Ambush.Mode <= 0 || !CombatFix.On(CombatFix.Stealth) || !LevyUtil.IsServerOrSingle || Hostile || OfficerMode || Dummy == null || Record == null || MarchingOrder) return false;
			if (item == null || item.IsAir || !ExtraDamageClasses.IsRogue(item.DamageType) || Ambush.Excepted(item)) return false;
			cp = LevyRippers.Of(Dummy);
			if (cp == null || !LevyRippers.IsRogue(cp) || LevyRippers.StealthMax(cp) <= 0f) return false;
			if (Ambush.Mode >= 2 || RogueGrenade.Is(item)) return true;
			bool boss = target.boss || LevyUtil.IsBossNpc(target);
			switch (RoguePolicy.For(item))
			{
				case RogueMode.Wait: return true;
				case RogueMode.Situational: return boss || StealthPolicy.Crowd(target);
				case RogueMode.SmallTargets: return !boss;
				default: return false; // its plain throws beat the strike: it throws at its own rate
			}
		}

		/// <summary>The commander is in danger: low with a boss after her, or near death with a hostile close.</summary>
		private bool AmCommanderInDanger()
		{
			Player cmd = Commander;
			if (cmd == null || cmd.dead || cmd.statLifeMax2 <= 0) return false;
			float frac = cmd.statLife / (float)cmd.statLifeMax2;
			if (frac >= Ambush.CmdDangerLife) return false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.life <= 0 || n.lifeMax <= 5) continue;
				float d = Vector2.Distance(n.Center, cmd.Center);
				if (n.boss && n.target == cmd.whoAmI && d < 50f * LevyConstants.TilePx) return true;
				if (frac < Ambush.CmdCriticalLife && d < 15f * LevyConstants.TilePx) return true;
			}
			return false;
		}

		/// <summary>The danger radius (tiles from the hitbox) of a target: a boss or a hard hitter wider, a big body wider still.</summary>
		private static float AmSafeTiles(NPC target)
		{
			bool danger = target.boss || LevyUtil.IsBossNpc(target) || target.damage >= LevyConstants.StrafeDangerDamage;
			float size = Math.Max(target.width, target.height) / LevyConstants.TilePx;
			return Math.Min(Ambush.SafeTilesMax, (danger ? Ambush.SafeTilesBoss : Ambush.SafeTilesMob) + size * Ambush.SafeSizeFrac);
		}

		/// <summary>
		/// Called from the engage think before the ranged / melee goal. True when the cycle owns the body this think (fall back, charge, or a strike in reach); false hands the think to the usual goal
		/// (the cycle is off, skipped, or the strike window is still closing in through the usual approach search).
		/// </summary>
		private bool AmbushGoal(NPC target, int now)
		{
			bool r = AmbushGoalBody(target, now);
			if (Ambush.Log && AmPhase != AmbushPhase.Off)
			{
				switch (AmPhase)
				{
					case AmbushPhase.FallBack: Ambush.ThinksFall++; break;
					case AmbushPhase.Charge: Ambush.ThinksCharge++; Ambush.ChargeTilesSum += Aiming.TilesToHitbox(NPC.Center, Aiming.NearestSegment(target, NPC.Center) ?? target); break;
					case AmbushPhase.Strike: Ambush.ThinksStrike++; if (AmStrikeWindow) Ambush.ThinksInReach++; break;
				}
				Ambush.MaybeLog();
			}
			return r;
		}

		private bool AmbushGoalBody(NPC target, int now)
		{
			Item item = WeaponUse.HeldItem(this);
			if (now < _amOffUntil || !AmbushWanted(target, item, out object cp)) { if (AmPhase != AmbushPhase.Off) AmSet(AmbushPhase.Off, now); return false; }
			// the exceptions: a target about to die, or a commander in danger: no cycle, just throw
			if (StealthPolicy.Finishing(this, item, target, Ambush.DyingThrows)) { if (AmPhase != AmbushPhase.Off) AmSet(AmbushPhase.Off, now); Ambush.SkipDying++; return false; }
			if (AmCommanderInDanger()) { if (AmPhase != AmbushPhase.Off) AmSet(AmbushPhase.Off, now); Ambush.SkipCommander++; return false; }
			if (now - _amLastTick > 4 * LevyConstants.ThinkIntervalTicks || _amTarget != target.whoAmI) { if (AmPhase != AmbushPhase.Off) AmSet(AmbushPhase.Off, now); _amTarget = target.whoAmI; } // a new fight (or a new target): start over
			_amLastTick = now;

			float max = LevyRippers.StealthMax(cp), bar = LevyRippers.Stealth(cp);
			bool ready = LevyRippers.StealthStrikeReady(cp);
			// a strike spent the bar since the last think
			if (_amBarWasReady && !ready && bar < 0.5f * max && AmPhase == AmbushPhase.Strike && !_amThrown) { _amThrown = true; AmStrikes++; Ambush.StrikesThrown++; _amStrikeAt = now; _amPlainLeft = Ambush.PlainAfter; _amUseSeen = LastUseTick; }
			_amBarWasReady = ready;

			NPC seg = Aiming.NearestSegment(target, NPC.Center) ?? target;
			float range = PlanningRange(target);
			float reach = range * LevyConstants.ShootableRangeFrac;
			float dT = Aiming.TilesToHitbox(NPC.Center, seg);
			float safe = AmSafeTiles(target);
			float chargeAt = reach * 0.85f >= safe ? Math.Min(reach * 0.85f, safe + Ambush.ChargeSlackTiles) : safe;
			bool keepsBar = Ambush.KeepsBar(item);

			if (AmPhase == AmbushPhase.Off) AmSet(ready ? AmbushPhase.Strike : AmbushPhase.FallBack, now);

			if (AmPhase == AmbushPhase.Strike)
			{
				bool burning = RageOn || AdrOn;
				if (_amThrown)
				{
					if (LastUseTick != _amUseSeen) { _amUseSeen = LastUseTick; if (_amPlainLeft > 0) _amPlainLeft--; }
					bool done = keepsBar || _amPlainLeft <= 0 || now - _amStrikeAt > Ambush.PlainWindowTicks;
					if (done && !burning) AmSet(AmbushPhase.FallBack, now);
				}
				else if (now - _amPhaseAt > Ambush.StrikeMaxTicks) { AmSet(AmbushPhase.Off, now); _amOffUntil = now + 120; return false; } // the window never opened (unreachable): the usual goal for a while
			}

			if (AmPhase == AmbushPhase.FallBack)
			{
				if (ready) AmSet(AmbushPhase.Strike, now);
				else
				{
					bool here = dT >= safe * 0.9f && dT <= chargeAt + 3f && AmSpotQuiet(NPC.Center);
					if (here || now - _amPhaseAt > Ambush.FallBackMaxTicks || (_amSpotOk && Math.Abs(NPC.Center.X - _amSpot.X) <= 1.5f * LevyConstants.TilePx && NPC.velocity.Y == 0f)) AmSet(AmbushPhase.Charge, now);
					else
					{
						if (!_amSpotOk) { _amSpotOk = AmPickSpot(seg, safe, chargeAt, out _amSpot); if (!_amSpotOk) { AmSet(AmbushPhase.Charge, now); } }
						if (_amSpotOk && AmPhase == AmbushPhase.FallBack)
						{
							Vector2 g = _amSpot; ApplyChaseClamp(ref g);
							if (_perchActive) AbandonPerch(now);
							WantsAltitude = false; _altHeld = false;
							_engMode = EngageMode.Retreat; WhyB = Why.BAmbushBack;
							SetGoal(g);
							return true;
						}
					}
				}
			}

			if (AmPhase == AmbushPhase.Charge)
			{
				if (ready) AmSet(AmbushPhase.Strike, now);
				else if (now - _amPhaseAt > Ambush.ChargeMaxTicks) AmSet(AmbushPhase.Strike, now);
				else if (dT < safe * Ambush.CrowdedFrac || dT > chargeAt + Ambush.DriftTiles || (TicksSinceDamaged < Ambush.HitRepickTicks && now - _amPhaseAt > Ambush.HitRepickTicks))
				{
					// the target came close or something hit the levy: another spot (this think walks to it; with none it holds)
					Ambush.Repicks++; AmSet(AmbushPhase.FallBack, now);
					_amSpotOk = AmPickSpot(seg, safe, chargeAt, out _amSpot);
					WantsAltitude = false; _altHeld = false; WhyB = Why.BAmbushBack;
					if (_amSpotOk) { Vector2 g = _amSpot; ApplyChaseClamp(ref g); _engMode = EngageMode.Retreat; SetGoal(g); }
					else { _engMode = EngageMode.Hold; ClearGoal(); }
					return true;
				}
				else
				{
					// a short run in starts before the bar is full, so it is full on arrival: the bar's remaining fill against the run to reach
					float runTicks = Math.Max(0f, dT - reach * 0.9f) * LevyConstants.TilePx / 3.5f;
					float gen = LevyRippers.StealthGen(cp, true);
					float fillMoving = gen > 0f ? (1f - bar / max) * 120f / gen : float.MaxValue;
					if (runTicks > 0f && fillMoving <= runTicks) AmSet(AmbushPhase.Strike, now);
					else
					{
						if (_perchActive) AbandonPerch(now);
						WantsAltitude = false; _altHeld = false;
						_engMode = EngageMode.Hold; WhyB = Why.BAmbushCharge;
						ClearGoal();
						return true;
					}
				}
			}

			// STRIKE: in reach with a line, stand and throw; otherwise close in (the first seconds straight at it, then the usual approach search)
			bool los = HasFiringLine(seg);
			bool inReach = dT <= reach * 0.95f && los;
			AmStrikeWindow = inReach;
			if (inReach)
			{
				_engMode = EngageMode.Hold; WhyB = Why.BAmbushStrike;
				ClearGoal();
				return true;
			}
			if (now - _amPhaseAt <= 120)
			{
				float side = NPC.Center.X >= seg.Center.X ? 1f : -1f;
				float hold = Math.Max(1f, reach * 0.75f) * LevyConstants.TilePx + seg.width * 0.5f;
				Vector2 g = new Vector2(seg.Center.X + side * hold, NPC.Bottom.Y);
				if (Math.Abs(g.X - NPC.Center.X) > LevyConstants.TilePx) { ApplyChaseClamp(ref g); _engMode = EngageMode.Approach; WhyB = Why.BAmbushStrike; SetGoal(g); return true; }
			}
			return false; // the usual approach search finds a firing line
		}

		/// <summary>No hostile shot or charging body can come within 4 tiles of this spot in the next second.</summary>
		private static bool AmSpotQuiet(Vector2 c)
		{
			var all = ThreatField.Snapshot();
			if (all == null || all.Count == 0) return true;
			_amNear.Clear(); ThreatField.Near(all, c, 4f * LevyConstants.TilePx, 60, _amNear);
			for (int i = 0; i < _amNear.Count; i++) if (!_amNear[i].PlayerSide) return false;
			return true;
		}

		/// <summary>
		/// The charge spot: <paramref name="chargeAt"/> tiles out from the target's hitbox on the levy's own side (the far side when this one is crowded with shots or walled in), on standable ground near the
		/// levy's row, inside its leash (the order's chase clamp) and inside an arena box. Scored by hostile shots near it, the walk, and a side switch.
		/// </summary>
		private bool AmPickSpot(NPC seg, float safe, float chargeAt, out Vector2 spot)
		{
			spot = default; float best = float.MaxValue; bool found = false;
			float tile = LevyConstants.TilePx;
			int side0 = NPC.Center.X >= seg.Center.X ? 1 : -1;
			Vector2 tl = default, br = default;
			bool box = ArenaBoxes.Active && ArenaBoxes.TryBounds(NPC, out tl, out br);
			Point feet = TileNav.FeetTile(NPC.Bottom);
			foreach (int side in new[] { side0, -side0 })
			{
				float x0 = seg.Center.X + side * (seg.width * 0.5f + chargeAt * tile);
				Vector2 want = new Vector2(x0, NPC.Bottom.Y);
				ApplyChaseClamp(ref want);
				if (box) want.X = MathHelper.Clamp(want.X, tl.X + 2f * tile, br.X - 2f * tile);
				int cx = (int)(want.X / tile);
				for (int dx = 0; dx <= 4; dx++)
				{
					for (int sgn = -1; sgn <= 1; sgn += 2)
					{
						if (dx == 0 && sgn > 0) continue;
						int x = cx + sgn * dx;
						for (int dy = 0; dy <= 8; dy++)
						{
							for (int sy = -1; sy <= 1; sy += 2)
							{
								if (dy == 0 && sy > 0) continue;
								int y = feet.Y + sy * dy;
								if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
								Vector2 b = TileNav.BottomCentre(x, y);
								Vector2 c = b - new Vector2(0f, NPC.height * 0.5f);
								float dHit = Aiming.TilesToHitbox(c, seg);
								float score = Math.Abs(b.X - NPC.Center.X) / tile * 0.1f + dy * 0.5f + (side != side0 ? 6f : 0f) + (dHit < safe * 0.9f ? 40f : 0f);
								if (score >= best) continue;
								if (!AmSpotQuiet(c)) score += 20f;
								if (score < best) { best = score; spot = b; found = true; }
								break; // the nearest row in this column
							}
						}
					}
				}
				if (found && best < 6f) break; // a good spot on our own side: no need to look across
			}
			return found;
		}

		/// <summary>
		/// The fire gate's view of the cycle: -1 no opinion (the stealth policy decides), 0 throw now, 1 hold. Falling back and charging hold every throw (any throw that is not a strike empties the bar);
		/// in the strike window the strike goes at once and then the plain throws; with Rage or Adrenaline burning the stealth policy decides. A weapon whose plain throw keeps the bar has no gate here.
		/// </summary>
		internal int AmbushGate(Item item, object cp)
		{
			if (!AmLive || Ambush.KeepsBar(item)) return -1;
			switch (AmPhase)
			{
				case AmbushPhase.FallBack:
				case AmbushPhase.Charge:
					return 1;
				case AmbushPhase.Strike:
					if (RageOn || AdrOn) return -1;
					if (LevyRippers.StealthStrikeReady(cp)) return 0;
					return _amThrown ? 0 : 1;
			}
			return -1;
		}
	}
}
