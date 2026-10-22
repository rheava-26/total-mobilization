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
	/// The white healing bolt of a soldier in the full Spectre Hood set: ai[0] = the soldier's whoAmI, ai[1] = the HP it delivers. Vanilla's bolt (ProjectileID.SpiritHeal, aiStyle 52)
	/// flies to a PLAYER read from Main.player[ai[0]] and heals through Main.player[...].statLife, so it cannot fetch a soldier home. This is the same flight (4 px per extra-update,
	/// steering (v*15 + aim)/16, a 5-dust trail, landing inside 50 px) with the soldier in the player's place. Spawned by SoldierNPC.SpectreHealOnHit from the soldier's own magic hit.
	/// Owned by the commander and tagged like every soldier shot (see WeaponUse.Spawn); it removes itself when its soldier dies or leaves, so a missed beat strands nothing.
	/// </summary>
	public class SoldierSpectreHeal : ModProjectile
	{
		/// <summary>No asset of its own: vanilla's bolt sprite (an invisible 6x6; the trail dust is the visible part).</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpiritHeal;

		public override void SetDefaults()
		{
			Projectile.width = 6; // vanilla's bolt hitbox
			Projectile.height = 6;
			Projectile.aiStyle = -1;
			Projectile.friendly = false; // it never hurts anything (WeaponUse.Spawn tags it friendly: CanDamage below keeps it harmless)
			Projectile.hostile = false;
			Projectile.alpha = 255;
			Projectile.tileCollide = false;
			Projectile.penetrate = -1;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 3;
			Projectile.timeLeft = LevyConstants.HoodBoltTimeLeftTicks;
			Projectile.netImportant = true;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
		}

		public override bool? CanDamage() => false;
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
			if (s == null || (authority && !s.IsAlive))
			{
				// A client may see the bolt before the body arrives: let the server decide when it is really gone.
				if (authority) Projectile.Kill();
				return;
			}

			Vector2 aim = s.NPC.Center - Projectile.Center;
			float dist = aim.Length();
			if (authority && dist < LevyConstants.HoodBoltArriveRangePx && Projectile.Hitbox.Intersects(s.NPC.Hitbox))
			{
				Deliver(s);
				Projectile.Kill();
				return;
			}
			// aiStyle 52: steer toward the wearer at 4 px, blending 15 parts of the old velocity to 1 of the new.
			aim = dist > 0.001f ? aim * (LevyConstants.HoodBoltSpeed / dist) : Vector2.Zero;
			Projectile.velocity = (Projectile.velocity * 15f + aim) / 16f;

			if (Main.dedServ) return;
			for (int i = 0; i < 5; i++)
			{
				Vector2 trail = new Vector2(Projectile.velocity.X * 0.2f * i, -Projectile.velocity.Y * 0.2f * i);
				int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.SpectreStaff, 0f, 0f, 100, default, 1.3f);
				Main.dust[d].noGravity = true;
				Main.dust[d].velocity *= 0f;
				Main.dust[d].position.X -= trail.X;
				Main.dust[d].position.Y -= trail.Y;
			}
		}

		/// <summary>Adds the carried HP to the soldier (authority only), clamped to his maximum, and shows the green number.</summary>
		private void Deliver(SoldierNPC s)
		{
			NPC body = s.NPC;
			int heal = Math.Min((int)Projectile.ai[1], body.lifeMax - body.life);
			if (heal <= 0) return;
			body.life += heal;
			if (s.Record != null) s.Record.CurrentHp = body.life;
			body.HealEffect(heal, true);
			body.netUpdate = true;
		}
	}
}
