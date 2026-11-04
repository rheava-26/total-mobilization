using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Movement;
using Levy.Common.Util;
using Levy.Content.TileEntities;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>Brain state of one officer on duty. Server only; never saved or synced (the NPC's position, velocity and ai[0] carry what clients need).</summary>
	public sealed class OfficerRuntime
	{
		public OfficerBody Body;
		public object KitState;
		public OfficerContext Ctx;
		public BarracksTE Barracks; public int BarracksTimer;
		// ---- fight ----
		public int Target = -1, ScanTimer, Cooldown, WindUpTick;
		public bool Attacking, Retreating;
		// ---- move ----
		public int Side, FarTicks, StuckTicks, SampleTimer, JumpCooldown;
		public float SampleDist;

		/// <summary>Back to a clean slate (keeps only the targeting body, which is bound to the NPC).</summary>
		public void Reset()
		{
			KitState = null; Ctx = null; Barracks = null; BarracksTimer = 0;
			Target = -1; ScanTimer = 0; Cooldown = 0; WindUpTick = 0; Attacking = false; Retreating = false;
			Side = 0; FarTicks = 0; StuckTicks = 0; SampleTimer = 0; JumpCooldown = 0; SampleDist = 0f;
		}
	}

	/// <summary>
	/// What an officer does on duty (server / single player, every tick, instead of the vanilla town AI): stay a few tiles behind the commander, shoot the nearest
	/// hostile in reach with his own weapon, run for the commander when badly hurt, and run his kit. Vanilla physics (gravity, tile collision, damage intake) is
	/// untouched; the brain only sets velocity, jumps, opens doors and drives ai[0] so the stock town frames animate (0 idle, 1 walk, 10 ranged attack).
	/// Deliberately small and forgiving: anything that fails to make progress ends with the officer brought to the commander.
	/// </summary>
	public static class OfficerBrain
	{
		public static void Tick(NPC npc, OfficerGlobalNPC g, OfficerDef def, Player commander)
		{
			OfficerRuntime rt = g.Rt;
			npc.netAlways = true; // client copies run no brain: stream position and velocity every tick (soldiers do the same)
			npc.directionY = -1;  // vanilla town NPCs never drop through platforms by choice

			float lifeFrac = npc.life / (float)Math.Max(1, npc.lifeMax);
			if (rt.Retreating) { if (lifeFrac >= ResumeLifeFrac) rt.Retreating = false; }
			else if (lifeFrac < RetreatLifeFrac) rt.Retreating = true;

			if (def.Weapon != null) TickCombat(npc, rt, def.Weapon); else rt.Attacking = false;

			if (rt.Attacking || TalkingTo(npc)) { npc.velocity.X *= 0.8f; rt.StuckTicks = 0; }
			else Follow(npc, rt, g, commander);

			npc.ai[0] = rt.Attacking ? 10f : (Math.Abs(npc.velocity.X) > 0.1f ? 1f : 0f);

			if (def.Kit != null)
			{
				if (--rt.BarracksTimer <= 0) { rt.BarracksTimer = 60; rt.Barracks = OfficerSystem.Instance.PayingBarracks(npc, g); }
				OfficerContext ctx = rt.Ctx ??= new OfficerContext();
				ctx.Npc = npc; ctx.Def = def; ctx.Commander = commander; ctx.State = rt.KitState; ctx.Barracks = rt.Barracks; ctx.Slot = g.Slot;
				def.Kit.Tick(ctx);
			}
		}

		private static bool TalkingTo(NPC npc)
		{
			for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i].active && Main.player[i].talkNPC == npc.whoAmI) return true;
			return false;
		}

		// =====================================================================================================
		// Combat (the vanilla town-NPC ranged attack: wind-up animation, one projectile at tick Delay, then a cooldown)
		// =====================================================================================================
		private static void TickCombat(NPC npc, OfficerRuntime rt, OfficerWeapon w)
		{
			if (rt.Attacking)
			{
				rt.WindUpTick++;
				npc.ai[1] = Math.Max(0, w.WindUp - rt.WindUpTick); npc.localAI[3] = rt.WindUpTick; // what FindFrame reads for the stock attack frames
				NPC t = TargetNpc(rt);
				if (t != null) Face(npc, t.Center.X);
				if (rt.WindUpTick == w.Delay) Fire(npc, t, w);
				if (rt.WindUpTick >= w.WindUp)
				{
					rt.Attacking = false; rt.WindUpTick = 0;
					rt.Cooldown = w.Cooldown + Main.rand.Next(Math.Max(1, w.RandomCooldown));
					npc.ai[1] = 0f; npc.localAI[3] = 0f;
				}
				return;
			}
			if (rt.Cooldown > 0) rt.Cooldown--;
			if (rt.Retreating) { rt.Target = -1; return; } // a badly hurt officer starts no new fight
			if (++rt.ScanTimer >= ScanIntervalTicks) { rt.ScanTimer = 0; rt.Target = FindTarget(npc, w); }
			else if (rt.Target >= 0 && !Shootable(npc, Main.npc[rt.Target], w)) rt.Target = -1;
			if (rt.Cooldown > 0 || rt.Target < 0) return;

			NPC target = Main.npc[rt.Target];
			rt.Attacking = true; rt.WindUpTick = 0;
			npc.frameCounter = 0.0; npc.localAI[3] = 0f; npc.ai[1] = w.WindUp;
			Face(npc, target.Center.X);
			npc.netUpdate = true;
		}

		private static NPC TargetNpc(OfficerRuntime rt) => rt.Target >= 0 && rt.Target < Main.maxNPCs && Main.npc[rt.Target].active ? Main.npc[rt.Target] : null;

		private static void Face(NPC npc, float x)
		{
			int dir = x < npc.Center.X ? -1 : 1;
			npc.direction = dir; npc.spriteDirection = dir;
		}

		private static bool Shootable(NPC npc, NPC t, OfficerWeapon w)
		{
			if (!LevyUtil.IsHostileTarget(t)) return false;
			if (Vector2.Distance(npc.Center, t.Center) > w.RangeTiles * LevyConstants.TilePx) return false;
			return Collision.CanHitLine(npc.position, npc.width, npc.height, t.position, t.width, t.height);
		}

		/// <summary>Nearest hostile NPC in reach with a clear line, or -1.</summary>
		private static int FindTarget(NPC npc, OfficerWeapon w)
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float d = Vector2.Distance(npc.Center, t.Center);
				if (d >= bestD || !Shootable(npc, t, w)) continue;
				bestD = d; best = i;
			}
			return best;
		}

		/// <summary>The vanilla town NPC damage scale: each boss down adds a slice (NPC.AI_007_TownEntities); Expert and Master scale on top.</summary>
		public static float TownDamageScale()
		{
			float m = 1f;
			if (NPC.downedBoss1) m += 0.1f;
			if (NPC.downedBoss2) m += 0.1f;
			if (NPC.downedBoss3) m += 0.1f;
			if (NPC.downedQueenBee) m += 0.1f;
			if (Main.hardMode) m += 0.4f;
			if (NPC.downedQueenSlime) m += 0.15f;
			if (NPC.downedMechBoss1) m += 0.15f;
			if (NPC.downedMechBoss2) m += 0.15f;
			if (NPC.downedMechBoss3) m += 0.15f;
			if (NPC.downedPlantBoss) m += 0.15f;
			if (NPC.downedEmpressOfLight) m += 0.15f;
			if (NPC.downedGolemBoss) m += 0.15f;
			if (NPC.downedAncientCultist) m += 0.15f;
			return m;
		}

		private static void Fire(NPC npc, NPC target, OfficerWeapon w)
		{
			int dir = npc.spriteDirection == 0 ? 1 : npc.spriteDirection;
			Vector2 aim = new Vector2(dir, -1f);
			if (target != null)
			{
				float frac = MathHelper.Clamp(Vector2.Distance(npc.Center, target.Center) / (w.RangeTiles * LevyConstants.TilePx), 0f, 1f);
				Vector2 v = npc.DirectionTo(target.Center + new Vector2(0f, -w.GravityCorrection * frac));
				if (!v.HasNaNs() && Math.Sign(v.X) == dir) aim = v;
			}
			aim *= w.Speed;
			int dmg = (int)(w.Damage * TownDamageScale() * AttackDamageMult);
			if (Main.expertMode) dmg = (int)(dmg * Main.GameModeInfo.TownNPCDamageMultiplier);
			int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center.X + dir * 16f, npc.Center.Y - 2f, aim.X, aim.Y, w.Projectile, Math.Max(1, dmg), w.Knockback, Main.myPlayer);
			if (idx >= 0 && idx < Main.maxProjectiles) { Main.projectile[idx].npcProj = true; Main.projectile[idx].noDropItem = true; }
		}

		// =====================================================================================================
		// Follow the commander
		// =====================================================================================================
		private static void Follow(NPC npc, OfficerRuntime rt, OfficerGlobalNPC g, Player cmd)
		{
			Vector2 cc = cmd.Center;
			float fromCmd = npc.Center.X - cc.X;
			if (Math.Abs(fromCmd) > 48f) rt.Side = Math.Sign(fromCmd); // keep the side he is already on: no oscillating around the commander
			else if (rt.Side == 0) rt.Side = g.Slot % 2 == 0 ? -1 : 1;
			float stand = (FollowDistTiles + g.Slot * FollowSlotStepTiles) * LevyConstants.TilePx;
			float dx = cc.X + rt.Side * stand - npc.Center.X;
			float adx = Math.Abs(dx);
			float dist = Vector2.Distance(npc.Center, cc);

			// left behind (pylon, mirror, recall, a long fall): after a few seconds he is simply brought along
			if (dist > TeleportDistTiles * LevyConstants.TilePx)
			{
				if (++rt.FarTicks >= TeleportAfterTicks) { rt.FarTicks = 0; if (TeleportToCommander(npc, cmd)) { rt.StuckTicks = 0; return; } }
			}
			else rt.FarTicks = 0;

			if (rt.JumpCooldown > 0) rt.JumpCooldown--;
			bool grounded = npc.velocity.Y == 0f;
			int dir = 0;
			if (adx > StopDistTiles * LevyConstants.TilePx)
			{
				dir = dx > 0f ? 1 : -1;
				npc.direction = dir; npc.spriteDirection = dir;
				float max = rt.Retreating || adx > RunDistTiles * LevyConstants.TilePx ? RunSpeedPx : WalkSpeedPx;
				float vx = npc.velocity.X * dir; // speed along the walking direction
				vx += vx < 0f ? Accel * 3f : Accel;
				if (vx > max) vx = Math.Max(max, npc.velocity.X * dir - Accel * 2f); // over the cap (slowed from a run): ease down, do not snap
				npc.velocity.X = vx * dir;
			}
			else
			{
				npc.velocity.X *= 0.8f;
				if (Math.Abs(npc.velocity.X) < 0.1f) npc.velocity.X = 0f;
			}

			if (dir != 0 && grounded) Walk(npc, rt, dir);
			if (npc.wet && !npc.lavaWet && npc.velocity.Y > -3f) npc.velocity.Y -= 0.35f; // float up and swim out; gravity and water drag still apply

			// progress watchdog: a far commander that is not getting closer for a while means a wall, a pit or a cliff the walk cannot solve
			if (++rt.SampleTimer >= StuckSampleTicks)
			{
				rt.SampleTimer = 0;
				bool wantsCloser = dist > stand + 4f * LevyConstants.TilePx;
				if (wantsCloser && rt.SampleDist - dist < StuckProgressPx) rt.StuckTicks += StuckSampleTicks; else rt.StuckTicks = 0;
				rt.SampleDist = dist;
				if (rt.StuckTicks >= StuckTeleportTicks) { rt.StuckTicks = 0; TeleportToCommander(npc, cmd); }
			}
		}

		/// <summary>Grounded and walking: step up a ledge, open doors, jump walls, refuse lava and bottomless drops.</summary>
		private static void Walk(NPC npc, OfficerRuntime rt, int dir)
		{
			Collision.StepUp(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY);
			int fx = (int)((npc.Center.X + dir * (npc.width / 2f + 4f)) / 16f);
			int fy = (int)((npc.Bottom.Y - 1f) / 16f);
			for (int dy = 0; dy <= 2; dy++) if (TileNavDoor(fx, fy - dy)) TryOpenDoor(fx, fy - dy, dir);

			int h = 0;
			while (h <= MaxJumpTiles + 1 && TileNav.IsSolid(fx, fy - h)) h++;
			if (h >= 2 && h <= MaxJumpTiles && rt.JumpCooldown <= 0 && !TileNav.IsSolid(fx, fy - h - 1) && !Collision.SolidCollision(npc.position - new Vector2(0f, 16f), npc.width, npc.height))
			{
				npc.velocity.Y = -(float)Math.Sqrt(2f * Gravity * (h * LevyConstants.TilePx + JumpClearPx));
				rt.JumpCooldown = 20;
				npc.netUpdate = true;
			}
			else if (h == 0)
			{
				int drop = TileNav.DropDepth(fx, fy);
				if (drop == -2 || drop == -1) npc.velocity.X = 0f; // lava, or no floor in sight: wait (the watchdog brings him along if the commander stays gone)
			}
			else if (h > MaxJumpTiles) npc.velocity.X *= 0.5f; // a wall too tall to jump: stand against it until the watchdog fires
		}

		private static bool TileNavDoor(int x, int y) => TileNav.InBounds(x, y) && TileNav.IsClosedDoor(Main.tile[x, y]);

		private static void TryOpenDoor(int x, int y, int dir)
		{
			Tile t = Framing.GetTileSafely(x, y);
			if (t.TileType == TileID.TallGateClosed)
			{
				if (WorldGen.ShiftTallGate(x, y, closing: false) && Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 4, x, y);
				return;
			}
			if (WorldGen.OpenDoor(x, y, dir)) { if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 0, x, y, dir); }
			else if (WorldGen.OpenDoor(x, y, -dir)) { if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 0, x, y, -dir); }
		}

		// =====================================================================================================
		// Placement
		// =====================================================================================================
		/// <summary>Brings the officer to a free standing spot beside the commander. False when there is no spot (the commander is airborne over nothing).</summary>
		public static bool TeleportToCommander(NPC npc, Player commander)
		{
			if (!FollowerTravel.FindStandingSpot(commander.Center, SpotRadiusTiles, out Vector2 spot)) return false;
			Place(npc, spot);
			return true;
		}

		/// <summary>Puts the NPC with his feet at <paramref name="bottom"/> and tells everyone.</summary>
		public static void Place(NPC npc, Vector2 bottom)
		{
			npc.position = new Vector2(bottom.X - npc.width / 2f, bottom.Y - npc.height);
			npc.oldPosition = npc.position;
			npc.velocity = Vector2.Zero;
			npc.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
		}
	}
}
