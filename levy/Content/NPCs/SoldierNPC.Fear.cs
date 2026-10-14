using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Boss;
using Levy.Common.Dialogue;
using Levy.Common.Fear;
using Levy.Common.Util;
using FearK = Levy.Common.LevyConstants.Fear;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Per-soldier fear meter (PLAN 5.8). The meter only moves while a main boss is within view range; it feeds the
	/// Shaken and Routed states. Server / single player only: <c>AI()</c> never calls into this on a client.
	/// Squad-wide events live in <see cref="FearSystem"/>, which drives the internal helpers below.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- per-body fear bookkeeping (server only, never saved) ----
		private int _fearOutOfSightTicks;
		private HashSet<int> _fearSeen;                          // boss whoAmI this body has already reacted to this fight
		private NPC _fearBoss;                                   // boss lookup cached for one tick
		private uint _fearBossTick = uint.MaxValue;
		private int _fearNetBucket;                              // coarse meter reading last sent to clients
		private uint _fearShakenBarkAt = uint.MaxValue;          // tick of the last SHAKEN bark (guards a brain/fear ping-pong)

		private HashSet<int> FearSeen => _fearSeen ??= new HashSet<int>();

		// =====================================================================================================
		// Partial method contracts
		// =====================================================================================================

		partial void Fear_Update()
		{
			if (Record == null || LevyUtil.IsClient || !IsAlive) return;

			NPC boss = Fear_BossInView();
			if (boss == null)
			{
				// No main boss in view: the meter drains and a routed body counts the seconds until it rallies.
				if (Record.Fear > 0f) Fear -= FearK.NoBossDecayPerSecond / 60f;

				if (State == SoldierState.Routed)
				{
					if (++_fearOutOfSightTicks >= LevyConstants.RoutRallyOutOfSightTicks) Fear_Rally(true);
				}
				else _fearOutOfSightTicks = 0;

				// Once no main boss lives anywhere the fight is over; forget it so a recycled NPC slot counts as a new sighting.
				if (_fearSeen != null && _fearSeen.Count > 0 && !BossRule.AnyMainBossAlive) _fearSeen.Clear();
			}
			else
			{
				_fearOutOfSightTicks = 0;
				if (FearSeen.Add(boss.whoAmI)) Fear_OnBossSighted(boss);

				float gain = FearK.BossInViewPerTick * Fear_Terror(boss);
				if (gain > 0f) Fear += gain * FearSystem.GainMultiplier(this);
			}

			// Shaken in, Shaken out. Routed is left alone here: only a rally, the boss dying or the out-of-sight timer ends it.
			if (Fear >= FearK.ShakenThreshold)
			{
				if (State is SoldierState.Idle or SoldierState.Follow or SoldierState.Patrol or SoldierState.Engage or SoldierState.Haul or SoldierState.Resupply)
					Fear_BecomeShaken();
			}
			else if (State == SoldierState.Shaken) Fear_ReenterOrder();

			Fear_SyncNet();
		}

		partial void Fear_OnDamaged(int damage, int attackerIndex)
		{
			if (Record == null || damage <= 0 || LevyUtil.IsClient || !IsAlive) return;
			if (Fear_BossInView() == null) return; // fear only exists during a main-boss fight

			bool wasShaken = State == SoldierState.Shaken; // shaken BEFORE this hit
			Fear += FearK.SelfHitMult * damage / Math.Max(1, NPC.lifeMax) * FearSystem.GainMultiplier(this);
			if (!wasShaken) return;

			float chance = (Fear - FearK.ShakenThreshold) / 50f;
			if (chance > 0f && Main.rand.NextFloat() < chance) Fear_Rout(null, true);
		}

		// =====================================================================================================
		// Public surface used by other modules
		// =====================================================================================================

		/// <summary>Restarts the out-of-sight countdown of a routed body (called by rallies).</summary>
		public void ResetRoutTimer() => _fearOutOfSightTicks = 0;

		// =====================================================================================================
		// Internals (also driven by FearSystem)
		// =====================================================================================================

		/// <summary>Nearest living main boss within view range of this body, cached for the current tick.</summary>
		internal NPC Fear_BossInView()
		{
			uint now = Main.GameUpdateCount;
			if (_fearBossTick == now && (_fearBoss == null || _fearBoss.active)) return _fearBoss;
			_fearBoss = BossRule.NearestMainBossInView(NPC.Center, FearK.BossViewRange);
			_fearBossTick = now;
			return _fearBoss;
		}

		// INTEGRATION: BossRule may grow TerrorFor(NPC boss, SoldierNPC soldier) (trophies near the home barracks lower the terror).
		// When it exists, this is the single call to switch: return BossRule.TerrorFor(boss, this);
		private int Fear_Terror(NPC boss) => BossRule.TerrorFor(boss, this);

		private void Fear_OnBossSighted(NPC boss)
		{
			DialogueSystem.Bark(this, DialogueTrigger.BOSS_SIGHTED);
			DialogueSystem.Bark(this, DialogueTrigger.BOSS, new DialogueContext { BossKey = BossRule.BossKey(boss), Boss = boss.FullName });
		}

		/// <summary>Forgets a boss so a future one in the same NPC slot is a fresh sighting.</summary>
		internal void Fear_ForgetBoss(int bossWhoAmI) => _fearSeen?.Remove(bossWhoAmI);

		private void Fear_BecomeShaken()
		{
			State = SoldierState.Shaken; StateTimer = 0;
			NPC.netUpdate = true;

			uint now = Main.GameUpdateCount;
			if (_fearShakenBarkAt != uint.MaxValue && now - _fearShakenBarkAt < (uint)LevyConstants.ChatBubbleCooldown) return;
			_fearShakenBarkAt = now;
			string fallen = FearSystem.RecentFallenName();
			DialogueSystem.Bark(this, DialogueTrigger.SHAKEN, fallen == null ? null : new DialogueContext { Comrade = fallen });
		}

		/// <summary>Routs this body: it runs, ignores enemies and drops its target. Returns false if it could not rout.</summary>
		internal bool Fear_Rout(DialogueContext ctx, bool bark)
		{
			if (Record == null || State == SoldierState.Routed || State == SoldierState.Dead || State == SoldierState.Desert) return false;
			if (Record.Fear < FearK.ShakenThreshold) Record.Fear = FearK.ShakenThreshold; // a routed soldier reads as afraid everywhere
			State = SoldierState.Routed; StateTimer = 0;
			TargetIndex = -1;
			_fearOutOfSightTicks = 0;
			NPC.netUpdate = true;
			if (bark) DialogueSystem.Bark(this, DialogueTrigger.ROUTED, ctx);
			return true;
		}

		/// <summary>
		/// Calms this body down to at most <see cref="FearK.RallyValue"/>. Shaken and Routed bodies go back to the state of their
		/// standing order. Returns true if the soldier was actually steadied (and so barks).
		/// </summary>
		internal bool Fear_Rally(bool bark)
		{
			if (Record == null) return false;
			bool wasBroken = State == SoldierState.Shaken || State == SoldierState.Routed;
			bool steadied = wasBroken || Record.Fear > FearK.RallyValue;
			if (Record.Fear > FearK.RallyValue) Record.Fear = FearK.RallyValue; // a rally never makes a calm soldier more afraid
			_fearOutOfSightTicks = 0;
			if (wasBroken) Fear_ReenterOrder();
			if (steadied)
			{
				NPC.netUpdate = true;
				if (bark) DialogueSystem.Bark(this, DialogueTrigger.RALLIED);
			}
			return steadied;
		}

		/// <summary>Leaves Shaken/Routed and re-enters the state of the standing order through the brain.</summary>
		internal void Fear_ReenterOrder()
		{
			if (Record == null) return;
			Vector2 post = Record.HoldPos;  // ReceiveOrder(Hold) re-posts the soldier where he stands; keep the original post
			ReceiveOrder(Record.Order);
			Record.HoldPos = post;
			// If the brain did not move the body out of a fear state, fall back to Idle so it can think again.
			if (State == SoldierState.Shaken || State == SoldierState.Routed) { State = SoldierState.Idle; StateTimer = 0; }
			_fearOutOfSightTicks = 0;
			NPC.netUpdate = true;
		}

		/// <summary>Clients draw the fear icon from the synced meter, so refresh the body whenever it moves by a coarse step.</summary>
		private void Fear_SyncNet()
		{
			float f = Record.Fear;
			int bucket = f <= 0f ? 0 : 1 + (int)(f / LevyConstants.FearNetStep);
			if (bucket == _fearNetBucket) return;
			_fearNetBucket = bucket;
			NPC.netUpdate = true;
		}
	}
}
