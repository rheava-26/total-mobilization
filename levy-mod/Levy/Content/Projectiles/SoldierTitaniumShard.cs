using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// One shard of the Titanium storm of a soldier in the full Titanium set: ai[0] = the soldier's whoAmI. Vanilla's shard (ProjectileID.TitaniumStormShard, aiStyle 175) orbits
	/// the player it reads off Main.player[owner] and dies unless that player has the Titanium Storm buff, which for a soldier's shard (owned by the commander) would be the
	/// commander's. This is the same orbit (a flat ellipse 24 px + 6 per shard wide, six turns every 300 ticks, 0.3 lerp, carried along with the wearer) around the soldier,
	/// kept alive by SoldierNPC.TitaniumStormTicks instead of a buff. Like vanilla's it is a one-hit projectile (50 damage, 15 knockback): the first enemy it touches uses it up.
	/// Spawned by SoldierNPC.SetHitEffects, at most 7 per soldier; it removes itself when the storm runs out or its soldier dies, so a missed beat strands nothing.
	/// </summary>
	public class SoldierTitaniumShard : ModProjectile
	{
		/// <summary>No asset of its own: vanilla's twelve-frame shard sheet.</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TitaniumStormShard;

		public override void SetStaticDefaults() => Main.projFrames[Type] = 12;

		public override void SetDefaults()
		{
			Projectile.width = 14; // vanilla's shard hitbox
			Projectile.height = 14;
			Projectile.aiStyle = -1;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 3600;
			Projectile.netImportant = true;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
		}

		public override bool? CanCutTiles() => false;

		private SoldierNPC Soldier
		{
			get
			{
				int i = (int)Projectile.ai[0];
				if (i < 0 || i >= Main.maxNPCs) return null;
				NPC n = Main.npc[i];
				return n.active && n.ModNPC is SoldierNPC s ? s : null;
			}
		}

		public override void AI()
		{
			bool authority = Main.netMode != NetmodeID.MultiplayerClient;
			SoldierNPC s = Soldier;
			if (s == null || (authority && (!s.IsAlive || s.TitaniumStormTicks <= 0)))
			{
				// A client may see the shard before the body arrives: let the server decide when it is really gone.
				if (authority) Projectile.Kill();
				return;
			}

			if (Projectile.frameCounter == 0)
			{
				Projectile.frameCounter = 1;
				Projectile.frame = Main.rand.Next(12);
				Projectile.rotation = Main.rand.NextFloat() * MathHelper.TwoPi;
			}
			Projectile.rotation += MathHelper.Pi / 200f;

			// This shard's place in the ring: every shard of this soldier, in slot order.
			int index = 0, total = 0, type = Projectile.type, me = (int)Projectile.ai[0];
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != type || (int)p.ai[0] != me) continue;
				if (i == Projectile.whoAmI) index = total;
				total++;
			}
			total = Math.Max(1, total);
			NPC body = s.NPC;
			float turn = (Main.GameUpdateCount % 300u) / 300f * LevyConstants.TitaniumTurnsPer300Ticks;
			float f = (index / (float)total + turn) * MathHelper.TwoPi;
			float radius = LevyConstants.TitaniumOrbitBasePx + total * LevyConstants.TitaniumOrbitPerShardPx;
			Projectile.Center += body.position - body.oldPosition; // carried along with the wearer
			Vector2 spot = body.Center + f.ToRotationVector2() * new Vector2(1f, LevyConstants.TitaniumOrbitFlatten) * radius;
			Projectile.Center = Vector2.Lerp(Projectile.Center, spot, LevyConstants.TitaniumFollowLerp);
			Projectile.velocity = Vector2.Zero;
		}
	}
}
