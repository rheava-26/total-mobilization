using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Event / pillar hostile projectiles that choose their victim themselves (Player.FindClosest, or the parent NPC's target index) normally lock onto the commander only. This lets
	/// the army share that incoming fire. It is deliberately scoped to the attacks below; every other hostile projectile keeps vanilla aim.
	///  * VortexVortexLightning (578, the Vortex Pillar's lightning bolt): fixes its direction ONCE at its first tick toward the nearest player. After that tick the server re-aims it
	///    at the nearest soldier when the soldier is the closer of the two, or with <see cref="LevyConstants.Targeting.EventProjectileSoldierShare"/> chance otherwise. Direction is
	///    ai[1], which the projectile's own netUpdate already sends.
	///  * SaucerMissile (448, Martian Madness homing rocket): its homing target is a player index in ai[1] picked once. The server may change that pick to 255 (the null-player slot,
	///    which no real player can occupy), and every machine then puts the nearest soldier's dummy in Main.player[255] for the length of that one AI call, exactly like an enemy swap.
	///  * NebulaBolt (573) / NebulaEye (574), Nebula Pillar enemies' shots: they read Main.player[parent.target]. When that parent enemy is locked on a soldier its target is 255, which outside
	///    the parent's own AI call is the null player at (0,0); the soldier is swapped in for the projectile's AI so the shot goes where the parent aimed.
	/// Boss-class sources keep the boss gate (the shot is only touched once that boss type is unlocked); Lunar Pillars are exempt, as for their own targeting.
	/// Slot discipline: Swap in PreAI, Unswap in PostAI, EnsureSlotRestored every projectile pass (EnemyTargetingSystem) and when anything throws.
	/// </summary>
	public class EventProjectileAim : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private bool _swappedHere;
		/// <summary>Snapshot taken in PreAI of the tick on which a one-shot pick can happen.</summary>
		private bool _pickPending;

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
		{
			if (!lateInstantiation) return false;
			int t = entity.type;
			return t == ProjectileID.VortexVortexLightning || t == ProjectileID.SaucerMissile || t == ProjectileID.NebulaBolt || t == ProjectileID.NebulaEye;
		}

		public override bool PreAI(Projectile projectile)
		{
			_swappedHere = false; _pickPending = false;
			if (!EnemyTargeting.Enabled || EnemyTargeting.Bodies.Count == 0) return true;
			try
			{
				switch (projectile.type)
				{
					case ProjectileID.VortexVortexLightning:
						_pickPending = projectile.localAI[0] == 0f; // the AI aims it at the nearest player on this very tick
						break;
					case ProjectileID.SaucerMissile:
						_pickPending = projectile.ai[0] == 0f && projectile.ai[1] == 0f; // the AI picks its player on this tick
						if (projectile.ai[0] == 1f && (int)projectile.ai[1] == SoldierNPC.DummySlot)
						{
							ITargetBody body = EnemyTargeting.NearestBody(projectile.Center, float.PositiveInfinity, out _);
							if (body != null) { EnemyTargeting.Swap(body); _swappedHere = true; }
						}
						break;
					case ProjectileID.NebulaBolt:
					case ProjectileID.NebulaEye:
					{
						int parentIdx = (int)projectile.ai[1];
						if (parentIdx < 0 || parentIdx >= Main.maxNPCs) break;
						NPC parent = Main.npc[parentIdx];
						if (!parent.active || parent.target != SoldierNPC.DummySlot) break; // not locked on a soldier: its target is a real player
						ITargetBody body = EnemyTargeting.LiveTarget(LevyNpcData.Of(parent).SoldierTarget);
						if (body != null) { EnemyTargeting.Swap(body); _swappedHere = true; }
						break;
					}
				}
			}
			catch (Exception e)
			{
				EnemyTargeting.EnsureSlotRestored();
				_swappedHere = false;
				Levy.Instance.Logger.Warn("Levy: EventProjectileAim.PreAI threw: " + e.Message);
			}
			return true;
		}

		public override void PostAI(Projectile projectile)
		{
			if (_swappedHere) { EnemyTargeting.Unswap(); _swappedHere = false; }
			if (!_pickPending) return;
			_pickPending = false;
			if (!LevyUtil.IsServerOrSingle || !EnemyTargeting.Enabled || EnemyTargeting.Bodies.Count == 0) return;
			try
			{
				if (!SourceAllows(projectile)) return;
				if (projectile.type == ProjectileID.VortexVortexLightning)
				{
					if (projectile.localAI[0] != 1f) return; // the first-tick aim did not run
					if (!ChooseSoldier(projectile, out ITargetBody body)) return;
					Vector2 d = body.NPC.Center - projectile.Center;
					projectile.ai[1] = (d == Vector2.Zero ? Vector2.UnitY : d).ToRotation();
					projectile.netUpdate = true;
				}
				else if (projectile.type == ProjectileID.SaucerMissile)
				{
					if (projectile.ai[0] != 1f) return; // still counting down its launch delay
					if (!ChooseSoldier(projectile, out _)) return;
					projectile.ai[1] = SoldierNPC.DummySlot; // 255: PreAI swaps the nearest soldier in on every machine
					projectile.netUpdate = true;
				}
			}
			catch (Exception e)
			{
				EnemyTargeting.EnsureSlotRestored();
				Levy.Instance.Logger.Warn("Levy: EventProjectileAim.PostAI threw: " + e.Message);
			}
		}

		/// <summary>The nearest soldier, when it is closer to the projectile than the nearest player (the pillar's target) or the share roll succeeds.</summary>
		private static bool ChooseSoldier(Projectile projectile, out ITargetBody body)
		{
			body = EnemyTargeting.NearestBody(projectile.Center, LevyConstants.Targeting.EventProjectileShareRangePx, out float soldierDist);
			if (body == null) return false;
			int pi = Player.FindClosest(projectile.position, projectile.width, projectile.height);
			Player p = pi >= 0 && pi < Main.maxPlayers ? Main.player[pi] : null;
			float playerDist = p != null && p.active && !p.dead ? Vector2.Distance(projectile.Center, p.Center) : float.PositiveInfinity;
			return soldierDist < playerDist || Main.rand.NextFloat() < LevyConstants.Targeting.EventProjectileSoldierShare;
		}

		/// <summary>Unknown source: untouched. Boss-class source: only once that boss type is unlocked (Lunar Pillars exempt). Anything else (event enemies): allowed.</summary>
		private static bool SourceAllows(Projectile projectile)
		{
			int idx = projectile.GetGlobalProjectile<SoldierProjectileTag>().SourceNpcIndex;
			// Nebula shots are driven by their parent enemy (ai[1]); the swap there already follows the parent's own gated choice.
			if (idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active) return false;
			NPC src = Main.npc[idx];
			if (Array.IndexOf(LevyConstants.Targeting.EventTowerTypes, src.type) >= 0) return true;
			if (src.ModNPC is SoldierNPC) return false;
			bool bossClass = src.boss || (src.type >= 0 && src.type < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[src.type]) || BossRule.IsMainBoss(LevyUtil.RootNpc(src));
			return !bossClass || EnemyTargeting.BossAggroUnlocked(src);
		}
	}
}
