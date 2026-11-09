using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// COMBATFIX parts that live on the soldier: where his cursor is for the projectiles he owns (never the commander's), the melee commitment that makes a levy who has stopped landing hits close in on a boss, and
	/// the counters the lab reads. See <see cref="CombatFix"/> for the A/B mask.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---------------------------------------------------------------------------------------------
		// The soldier's cursor
		// ---------------------------------------------------------------------------------------------
		private Vector2 _cursorHold; private uint _cursorHoldTick;
		/// <summary>Cursor reads by this soldier's shots that fell back to the held point / the point ahead of him (the lab counts these: before the fix they read the commander's cursor).</summary>
		internal static int CursorFallbacks, CursorPuppets;

		/// <summary>
		/// The world point a projectile this soldier owns should read as "the cursor": his target's centre; with no target this tick the last one he had (held for six seconds, so a bolt that picks its line late
		/// still flies where he was fighting); with none ever, a point ahead of him. Never the commander's cursor.
		/// </summary>
		internal Vector2 AimCursor()
		{
			uint now = Main.GameUpdateCount + 1u;
			int ti = TargetIndex;
			if (ti >= 0 && ti < Main.maxNPCs && Main.npc[ti].active) { _cursorHold = Main.npc[ti].Center; _cursorHoldTick = now; return _cursorHold; }
			CursorFallbacks++;
			if (_cursorHoldTick != 0 && now - _cursorHoldTick < 360u) return _cursorHold;
			return NPC.Center + new Vector2((NPC.direction == 0 ? 1 : NPC.direction) * 480f, -24f);
		}

		/// <summary>Consecutive ticks a rogue throw was held for the stealth bar / for a boss (see <see cref="StealthPolicy"/>).</summary>
		internal int StealthHoldRun, StealthSaveRun;

		// ---------------------------------------------------------------------------------------------
		// Melee commitment: a levy that has landed nothing for a few seconds stops keeping its distance from a boss
		// ---------------------------------------------------------------------------------------------
		/// <summary>Tick of the last hit this soldier's projectile landed (melee blows set <see cref="LastMeleeHitTick"/>). Server / single player.</summary>
		internal int ProjHitTick = -100000;
		internal const int MeleeStallTicks = 240, MeleeChargeTicks = 480, MeleeChargeHitExtend = 240;
		private int _chargeTarget = -1, _chargeSince, _chargeUntil, _chargeLastCheck;
		/// <summary>Lab counters: charges started, think ticks spent committed, think ticks spent holding off a lethal target.</summary>
		internal static int ChargesStarted, ChargeThinks, HoldThinks;

		private int LastLandedTick => Math.Max(LastMeleeHitTick, ProjHitTick);

		/// <summary>How far (tiles) a stalled melee levy looks for a boss; about half a screen each way.</summary>
		internal const float MeleeSeekTiles = 70f;
		private int _seekSince;
		/// <summary>
		/// A melee levy normally only notices what is within <see cref="LevyConstants.MeleeAcquireRangeTiles"/>, so a boss that hangs back, or that the army walked away from, leaves it standing with its commander and swinging at nothing.
		/// Once a boss is alive and the levy has landed no hit for <see cref="MeleeStallTicks"/>, its sight is <see cref="MeleeSeekTiles"/> and it goes for the boss (the charge itself is <see cref="MeleeStalled"/>).
		/// </summary>
		private bool MeleeSeeksBoss(int now)
		{
			if (!CombatFix.On(CombatFix.Charge) || !PositionsAsMelee || !IsRegular || Record == null || Record.HoldFire) return false;
			if (!SoldierTargeting.AnyBossNpcActive()) { _seekSince = 0; return false; }
			if (_seekSince == 0) _seekSince = now;
			if (MeleeCommitted) return true; // charging: it keeps its long sight until the blows stop for good
			return now - Math.Max(_seekSince, LastLandedTick) >= MeleeStallTicks;
		}

		/// <summary>Lab only (FixProbe): this soldier positions as a melee fighter and is alive; the nearest live boss's distance in tiles (hitbox) and the ticks since this soldier last landed a hit.</summary>
		internal bool ProbeMelee(out float bossTiles, out int sinceHit, out bool avoiding, out int mode)
		{
			bossTiles = float.MaxValue; sinceHit = 0; avoiding = false; mode = 0;
			if (!IsAlive || Weapon == null || Record == null || !PositionsAsMelee || OfficerMode) return false;
			NPC best = null; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || !LevyUtil.IsBossNpc(n)) continue;
				float d = Aiming.TilesToHitbox(NPC.Center, n);
				if (d < bestD) { bestD = d; best = n; }
			}
			if (best == null) return false;
			bossTiles = bestD; sinceHit = (int)Main.GameUpdateCount - LastLandedTick;
			avoiding = _dangerIdx >= 0 && ValidNpc(_dangerIdx) && LevyUtil.IsBossNpc(Main.npc[_dangerIdx]) && !MeleeCommitted;
			int ti = TargetIndex; // 0 no target, 1 the boss held off, 2 the boss engaged, 3 another target
			mode = ti < 0 || !ValidNpc(ti) ? 0 : LevyUtil.IsBossNpc(Main.npc[ti]) ? (MeleeCommitted ? 2 : 1) : 3;
			return true;
		}

		/// <summary>Lab only: the held weapon is a Calamity rogue weapon.</summary>
		internal bool HoldsRogueWeapon() { Item it = WeaponUse.HeldItem(this); return it != null && !it.IsAir && ExtraDamageClasses.IsRogue(it.DamageType); }

		/// <summary>True while this melee soldier is committed to a target it would otherwise keep away from.</summary>
		private bool MeleeCommitted => CombatFix.On(CombatFix.Charge) && (int)Main.GameUpdateCount < _chargeUntil;

		/// <summary>
		/// The no-suicide rule keeps a melee levy off a boss (or any hit that would maim it). A levy that then lands nothing for <see cref="MeleeStallTicks"/> has been standing around: it commits for
		/// <see cref="MeleeChargeTicks"/> (long enough to walk in), and every hit it lands keeps the charge alive. When the blows stop it falls back to the old caution and the clock starts again.
		/// </summary>
		private bool MeleeStalled(NPC target)
		{
			if (!CombatFix.On(CombatFix.Charge) || target == null || !PositionsAsMelee) return false;
			int now = (int)Main.GameUpdateCount;
			if (target.whoAmI != _chargeTarget || now - _chargeLastCheck > 90) { _chargeTarget = target.whoAmI; _chargeSince = _seekSince != 0 && LevyUtil.IsBossNpc(target) ? _seekSince : now; }
			_chargeLastCheck = now;
			if (now < _chargeUntil)
			{
				if (LastLandedTick > _chargeSince && now - LastLandedTick < MeleeChargeHitExtend) _chargeUntil = Math.Max(_chargeUntil, LastLandedTick + MeleeChargeHitExtend);
				ChargeThinks++;
				return true;
			}
			if (_chargeUntil != 0) { _chargeUntil = 0; _chargeSince = now; } // the charge ran out: the clock starts again
			if (now - Math.Max(_chargeSince, LastLandedTick) >= MeleeStallTicks)
			{
				_chargeUntil = now + MeleeChargeTicks; ChargesStarted++; ChargeThinks++;
				return true;
			}
			HoldThinks++;
			return false;
		}
	}
}
