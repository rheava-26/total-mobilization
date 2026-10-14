using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Boss;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using FearK = Levy.Common.LevyConstants.Fear;

namespace Levy.Common.Fear
{
	/// <summary>
	/// Squad-wide fear events: comrades hit or killed, the commander dying, boss phases and kills, rallies, and the gain
	/// multiplier every fear input goes through. The per-soldier meter and the Shaken/Routed transitions live in
	/// SoldierNPC.Fear.cs. Server / single player only: every entry point ignores multiplayer clients.
	/// </summary>
	public static class FearSystem
	{
		// ---- per-tick cache of living player positions (GainMultiplier runs per body per tick during boss fights) ----
		private static uint _playersTick = uint.MaxValue;
		private static int _playerCount;
		private static readonly Vector2[] _playerPos = new Vector2[Main.maxPlayers];

		// ---- the most recent comrade to fall (SHAKEN lines may name him) ----
		private static string _fallenName;
		private static uint _fallenTick;

		// =====================================================================================================
		// Inputs
		// =====================================================================================================

		/// <summary>A comrade took a hit: soldiers who can see a boss and are near him get scared in proportion to the hit.</summary>
		public static void OnComradeHit(SoldierNPC victim, int damage)
		{
			if (victim == null || victim.Record == null || damage <= 0 || LevyUtil.IsClient) return;
			float amount = FearK.ComradeHitMult * damage / Math.Max(1, victim.NPC.lifeMax);
			AddToWitnesses(victim, amount);
		}

		/// <summary>A comrade died: a flat fright for witnesses, and the nearest soldiers call it out.</summary>
		public static void OnComradeDied(SoldierNPC victim)
		{
			if (victim == null || victim.Record == null || LevyUtil.IsClient) return;
			RosterSystem roster = RosterSystem.Instance;
			if (roster == null) return;

			_fallenName = victim.SoldierName;
			_fallenTick = Main.GameUpdateCount;
			victim.Record.Fear = 0f; // a respawned soldier starts steady

			Vector2 at = victim.NPC.Center;
			float viewPx = FearK.BossViewRange * LevyConstants.TilePx;
			float barkPx = LevyConstants.FearComradeBarkTiles * LevyConstants.TilePx;
			SoldierNPC near1 = null, near2 = null;
			float d1 = float.MaxValue, d2 = float.MaxValue;

			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null || ReferenceEquals(b, victim) || !b.IsAlive) continue;
				float d = Vector2.Distance(b.NPC.Center, at);
				if (d > viewPx) continue;

				if (b.Fear_BossInView() != null) b.Fear += FearK.ComradeDeath * GainMultiplier(b);

				if (d > barkPx || b.State == SoldierState.Routed) continue;
				if (d < d1) { near2 = near1; d2 = d1; near1 = b; d1 = d; }
				else if (d < d2) { near2 = b; d2 = d; }
			}

			if (near1 == null) return;
			var ctx = new DialogueContext { Comrade = victim.SoldierName };
			DialogueSystem.Bark(near1, DialogueTrigger.COMRADE_DOWN, ctx);
			if (LevyConstants.FearComradeBarkCount > 1 && near2 != null) DialogueSystem.Bark(near2, DialogueTrigger.COMRADE_DOWN, ctx);
		}

		/// <summary>The commander fell: every soldier who can see a main boss breaks and runs.</summary>
		public static void OnPlayerDied(Player player)
		{
			if (player == null || LevyUtil.IsClient) return;
			RosterSystem roster = RosterSystem.Instance;
			if (roster == null) return;

			DialogueContext ctx = null;
			int barked = 0;
			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null || !b.IsAlive) continue;
				if (b.Fear_BossInView() == null) continue;
				bool bark = barked < LevyConstants.FearRoutBarkCount;
				if (bark) ctx ??= new DialogueContext { Player = player.name };
				if (b.Fear_Rout(bark ? ctx : null, bark) && bark) barked++;
			}
		}

		/// <summary>
		/// A main boss died. When that ends the fight every soldier calms down, broken ones go back to their orders,
		/// and the ones who were near cheer. If another main boss still lives the fight goes on and only the
		/// "already saw this boss" memory is cleared.
		/// </summary>
		public static void OnBossKilled(NPC boss)
		{
			if (boss == null || LevyUtil.IsClient) return;
			RosterSystem roster = RosterSystem.Instance;
			if (roster == null) return;

			bool fightOver = !AnotherMainBossAlive(boss);
			DialogueContext ctx = null;
			Vector2 at = boss.Center;

			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null) continue;
				b.Fear_ForgetBoss(boss.whoAmI);
				if (!fightOver) continue;

				bool broken = b.State == SoldierState.Shaken || b.State == SoldierState.Routed;
				b.Record.Fear = 0f;
				b.ResetRoutTimer();
				if (broken && b.IsAlive) b.Fear_ReenterOrder();

				if (b.IsAlive && LevyUtil.WithinTiles(b.NPC.Center, at, FearK.BossViewRange))
				{
					ctx ??= new DialogueContext { BossKey = BossRule.BossKey(boss), Boss = boss.FullName };
					DialogueSystem.Bark(b, DialogueTrigger.VICTORY, ctx);
				}
			}

			// Soldiers waiting to respawn have no body; their record must not carry the fear into the next fight.
			if (fightOver) foreach (SoldierRecord r in roster.Regulars) r.Fear = 0f;
		}

		/// <summary>A boss entered a new phase: everyone who can see it flinches.</summary>
		public static void OnBossPhase(NPC boss, int phase)
		{
			if (boss == null || LevyUtil.IsClient) return;
			RosterSystem roster = RosterSystem.Instance;
			if (roster == null) return;

			Vector2 at = boss.Center;
			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null || !b.IsAlive) continue;
				if (!LevyUtil.WithinTiles(b.NPC.Center, at, FearK.BossViewRange)) continue;
				b.Fear += FearK.PhaseChange * GainMultiplier(b);
			}
		}

		/// <summary>Rally: soldiers within radius settle to at most RallyValue; shaken and routed ones return to their orders.</summary>
		public static void Rally(Vector2 center, float radiusTiles)
		{
			if (LevyUtil.IsClient) return;
			RosterSystem roster = RosterSystem.Instance;
			if (roster == null) return;

			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null || !b.IsAlive) continue;
				if (!LevyUtil.WithinTiles(b.NPC.Center, center, radiusTiles)) continue;
				b.Fear_Rally(true);
			}
		}

		// =====================================================================================================
		// Gain multiplier
		// =====================================================================================================

		/// <summary>
		/// Scales every fear input for one soldier: temperament, veteran, hazard pay, the config multiplier, and halved
		/// while any living player stands close.
		/// </summary>
		public static float GainMultiplier(SoldierNPC s)
		{
			if (s == null || s.Record == null) return 1f;
			SoldierRecord r = s.Record;

			float m = r.Temperament switch
			{
				Temperament.Greenhorn => FearK.GreenhornMult,
				Temperament.Zealot => FearK.ZealotMult,
				_ => 1f,
			};
			if (r.Veteran) m *= FearK.VeteranMult;
			if (r.HazardPaid) m *= LevyConstants.HazardPayFearMult;
			m *= LevyConfig.Get.FearMultiplier;
			if (PlayerNear(s.NPC.Center)) m *= FearK.PlayerNearMult;
			return m;
		}

		/// <summary>True if any living player is within <see cref="FearK.PlayerNearTiles"/> tiles of the point (not only the commander).</summary>
		public static bool PlayerNear(Vector2 pos)
		{
			RefreshPlayers();
			float r = FearK.PlayerNearTiles * LevyConstants.TilePx;
			float r2 = r * r;
			for (int i = 0; i < _playerCount; i++)
				if (Vector2.DistanceSquared(_playerPos[i], pos) <= r2) return true;
			return false;
		}

		/// <summary>Name of the comrade who fell in the last 30 seconds, or null.</summary>
		public static string RecentFallenName()
		{
			if (_fallenName == null || Main.GameUpdateCount - _fallenTick > (uint)LevyConstants.FearFallenRecallTicks) return null;
			return _fallenName;
		}

		// =====================================================================================================
		// Helpers
		// =====================================================================================================

		/// <summary>Adds scaled fear to every other living soldier within view range of the victim who can see a boss.</summary>
		private static void AddToWitnesses(SoldierNPC victim, float amount)
		{
			RosterSystem roster = RosterSystem.Instance;
			if (roster == null) return;
			Vector2 at = victim.NPC.Center;
			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null || ReferenceEquals(b, victim) || !b.IsAlive) continue;
				if (!LevyUtil.WithinTiles(b.NPC.Center, at, FearK.BossViewRange)) continue;
				if (b.Fear_BossInView() == null) continue;
				b.Fear += amount * GainMultiplier(b);
			}
		}

		/// <summary>Is a main boss other than <paramref name="dead"/> (and its own parts) still alive?</summary>
		private static bool AnotherMainBossAlive(NPC dead)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n == null || !n.active || n.life <= 0 || i == dead.whoAmI || n.realLife == dead.whoAmI) continue;
				if (BossRule.IsMainBoss(n)) return true;
			}
			return false;
		}

		private static void RefreshPlayers()
		{
			uint now = Main.GameUpdateCount;
			if (_playersTick == now) return;
			_playersTick = now; _playerCount = 0;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active || p.dead || p.ghost) continue;
				_playerPos[_playerCount++] = p.Center;
			}
		}
	}
}
