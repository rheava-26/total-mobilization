using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Targeting;

namespace Warbands.Common
{
	/// <summary>
	/// The Lunatic Cultist's circle fights as a band. The Cultist Archers and Lunatic Devotees that stand worshipping outside the Dungeon become flying skirmishers once a player or a levy comes within 60 tiles:
	/// archers hold about 20 tiles off and strafe, devotees close to about 9 tiles and weave, both fire real hostile projectiles at the nearest of the player and the player's levies. Damage comes from
	/// BandStats at the world's stage, so the circle obeys the same DPS cap as any other band. Switch: WarbandsConfig.CultistBand.
	/// </summary>
	public class CultistBandGlobal : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		private int _timer;

		public static bool IsCircle(int type) => type == NPCID.CultistArcherBlue || type == NPCID.CultistArcherWhite || type == NPCID.CultistDevote;

		public override bool PreAI(NPC npc)
		{
			if (!IsCircle(npc.type)) return true;
			var cfg = WarbandsConfig.Get; if (cfg == null || !cfg.CultistBand) return true;
			Player pl = npc.target >= 0 && npc.target < Main.maxPlayers ? Main.player[npc.target] : null;
			if (pl == null || !pl.active || pl.dead) { npc.TargetClosest(true); pl = Main.player[npc.target]; }
			if (!pl.active || pl.dead) return true;
			Vector2 aimAt = pl.Center; float best = Vector2.DistanceSquared(npc.Center, aimAt);
			var bodies = EnemyTargeting.Bodies;
			for (int i = 0; i < bodies.Count; i++)
			{
				var b = bodies[i]; if (b == null || !b.IsAlive || b.NPC == null || !b.NPC.active) continue;
				float d = Vector2.DistanceSquared(npc.Center, b.NPC.Center); if (d < best) { best = d; aimAt = b.NPC.Center; }
			}
			if (best > 60f * 16f * 60f * 16f) return true; // far: the vanilla worshipper
			bool archer = npc.type != NPCID.CultistDevote;
			npc.noGravity = true; npc.noTileCollide = false; npc.knockBackResist = 0.2f;
			float t = Main.GameUpdateCount * 0.035f + npc.whoAmI * 1.7f;
			float side = (npc.whoAmI & 1) == 0 ? 1f : -1f;
			Vector2 post = aimAt + new Vector2(side * (archer ? 320f : 140f) + (float)Math.Sin(t) * 90f, -(archer ? 130f : 60f) + (float)Math.Cos(t * 1.3f) * 45f);
			Vector2 want = post - npc.Center;
			float speed = archer ? 5.2f : 6.4f;
			if (want.LengthSquared() > 1f) want = Vector2.Normalize(want) * Math.Min(speed, want.Length() * 0.08f + 1.5f);
			npc.velocity = Vector2.Lerp(npc.velocity, want, 0.06f);
			npc.direction = aimAt.X >= npc.Center.X ? 1 : -1; npc.spriteDirection = npc.direction;
			if (Main.netMode != NetmodeID.MultiplayerClient && --_timer <= 0 && Collision.CanHitLine(npc.position, npc.width, npc.height, aimAt - new Vector2(4, 4), 8, 8))
			{
				int stage = Stage.Current;
				int hit = BandStats.Hit(stage, archer ? 'R' : 'M', archer ? 60f : 55f, archer ? 0.8f : 1f);
				int dmg = Math.Max(1, hit / (int)Levy.Common.LevyConstants.HostileProjectileDamageMult);
				Vector2 from = npc.Center, vel = Vector2.Normalize(aimAt - from + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-14f, 14f))) * (archer ? 11f : 8.5f);
				Projectile.NewProjectile(npc.GetSource_FromAI(), from, vel, archer ? ProjectileID.WoodenArrowHostile : ProjectileID.EyeLaser, dmg, 1f, Main.myPlayer);
				_timer = archer ? 75 : 55;
			}
			return false;
		}
	}
}
