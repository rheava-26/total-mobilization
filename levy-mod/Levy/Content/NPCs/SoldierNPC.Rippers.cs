using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Compat.Calamity;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Calamity's Rage and Adrenaline on a levy (see <see cref="LevyRippers"/>). The bars live on the dummy's CalamityPlayer; this part keeps the two mode latches, runs Calamity's fill/burn step for levies the per-tick gear frame
	/// does not already cover, switches a full bar on by itself while a boss is alive near the levy, and lends the modes to the levy's projectile hits and incoming hits. Server / single player only. Revengeance only
	/// (Calamity's own RageEnabled / AdrenalineEnabled decide), so without Calamity, or with Revengeance off, every method returns at once.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Rage Mode / Adrenaline Mode are burning on this levy right now.</summary>
		public bool RageOn, AdrOn;
		private object _rippersPlayer; private bool _rippersLooked;
		private int _boostTimer, _rogueHold; private float _prevStealth; private bool _stealthWasReady;
		private static int _fxStamp, _fxCount;

		/// <summary>Calamity's activation sound at the levy (client side, through the army throttle) and a small floating text, itself capped so 90 levies filling together do not paper the screen.</summary>
		private void Rippers_Announce(object cp, bool adrenaline)
		{
			LevyRippers.Announce(this, LevyRippers.SoundKind(cp, adrenaline));
			int now = (int)Main.GameUpdateCount;
			if (now - _fxStamp > 30) { _fxStamp = now; _fxCount = 0; }
			if (++_fxCount <= 4) global::Levy.Common.Dialogue.DialogueSystem.FloatingText(this, adrenaline ? "ADRENALINE!" : "RAGE!");
		}

		private object RippersPlayer()
		{
			if (_rippersPlayer == null && !_rippersLooked && Dummy != null) { _rippersLooked = LevyRippers.Available; _rippersPlayer = LevyRippers.Of(Dummy); }
			return _rippersPlayer;
		}

		/// <summary>The mode flags ResetEffects clears each pass. Called right before Calamity's fill/burn step (inside the gear frame) and after every pass.</summary>
		internal void Rippers_Restore()
		{
			if (!LevyRippers.Active) return;
			object cp = RippersPlayer(); if (cp == null) return;
			LevyRippers.SetModes(cp, RageOn, AdrOn);
		}

		private bool BossNear()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC b = Main.npc[i];
				if (b.active && b.boss && !b.friendly && b.Distance(NPC.Center) < LevyRippers.ActivateRangePx) return true;
			}
			return false;
		}

		/// <summary>Once per tick after the stat pass: fill/burn, switch a full bar on, end a spent mode.</summary>
		internal void Rippers_Tick()
		{
			if (!LevyRippers.Active || !LevyUtil.IsServerOrSingle || OfficerMode || Dummy == null || Record == null || !IsAlive) return;
			object cp = RippersPlayer(); if (cp == null) return;
			bool rageEn = LevyRippers.RageEnabled(cp), adrEn = LevyRippers.AdrenalineEnabled(cp);
			if (!rageEn && !adrEn)
			{
				if (RageOn || AdrOn) { RageOn = AdrOn = false; LevyRippers.SetModes(cp, false, false); }
				return;
			}
			if (--_boostTimer <= 0) { _boostTimer = 30; LevyRippers.CopyBoosters(Commander, cp); } // the commander's permanent Rage / Adrenaline upgrades, re-read twice a second
			LevyRippers.SetModes(cp, RageOn, AdrOn);
			if (!UpkeepActive) LevyRippers.RunStep(cp); // the gear frame runs Calamity's PostUpdateMiscEffects (and so this step) itself
			bool rogue = LevyRippers.IsRogue(cp);
			if (rogue)
			{
				// a stealth strike spends the bar: a drop from "strike ready" is one, and it counts as aligned when a mode is burning
				float st = LevyRippers.Stealth(cp);
				if (_stealthWasReady && st < _prevStealth - 0.01f) { LevyRippers.Strikes++; if (RageOn || AdrOn) LevyRippers.StrikesBoosted++; }
				_prevStealth = st; _stealthWasReady = LevyRippers.StealthStrikeReady(cp);
			}
			bool rageFull = rageEn && !RageOn && LevyRippers.Rage(cp) >= LevyRippers.RageMax(cp);
			bool adrFull = adrEn && !AdrOn && LevyRippers.Adrenaline(cp) >= LevyRippers.AdrenalineMax(cp);
			bool bossNear = (rageFull || adrFull) && LevyRippers.BossAlive && BossNear();
			bool rageReady = rageFull && (bossNear || KeyTarget() != null); // Rage: any fight. Adrenaline: a boss near.
			bool adrReady = adrFull && bossNear;
			if (rageReady || adrReady)
			{
				bool go = true;
				if (rogue)
				{
					// hold the full bar until the next throw is a stealth strike, so the whole boosted window starts with it; never forever
					bool aligned = LevyRippers.StealthStrikeReady(cp);
					go = aligned || ++_rogueHold > LevyRippers.RogueHoldTicks;
					if (go) { if (aligned) LevyRippers.RogueAligned++; else LevyRippers.RogueTimeouts++; }
				}
				if (go)
				{
					_rogueHold = 0;
					if (rageReady) { RageOn = true; LevyRippers.RageActivations++; Rippers_Announce(cp, false); }
					if (adrReady) { AdrOn = true; LevyRippers.AdrenalineActivations++; Rippers_Announce(cp, true); }
				}
			}
			else _rogueHold = 0;
			if (RageOn && LevyRippers.Rage(cp) <= 0f) RageOn = false;
			if (AdrOn && LevyRippers.Adrenaline(cp) <= 0f) AdrOn = false;
			LevyRippers.SetModes(cp, RageOn, AdrOn);
		}

		/// <summary>Calamity's damage multiplier from this levy's burning modes (1 when none): lent to the levy's projectile hits, whose Calamity hook would otherwise read the commander's.</summary>
		internal float Rippers_DamageMult()
		{
			if (!RageOn && !AdrOn) return 1f;
			object cp = RippersPlayer(); if (cp == null) return 1f;
			return LevyRippers.DamageMult(cp);
		}

		/// <summary>Incoming-hit multiplier: a full, unspent adrenaline bar halves the hit (and the hit then empties it, see <see cref="Rippers_OnHurt"/>).</summary>
		internal float Rippers_IncomingMult()
		{
			if (!LevyRippers.Active || !LevyUtil.IsServerOrSingle || Dummy == null) return 1f;
			object cp = RippersPlayer(); if (cp == null) return 1f;
			return LevyRippers.IncomingMult(cp);
		}

		/// <summary>A hit landed: Calamity's adrenaline loss. A levy in the gear frame already got it through the dummy's own Player.Hurt (GearOnHurt).</summary>
		internal void Rippers_OnHurt(int damage)
		{
			if (damage <= 0 || !LevyRippers.Active || !LevyUtil.IsServerOrSingle || UpkeepActive) return;
			object cp = RippersPlayer(); if (cp == null || !LevyRippers.AdrenalineEnabled(cp)) return;
			LevyRippers.LoseOnHurt(cp, damage);
		}
	}
}
