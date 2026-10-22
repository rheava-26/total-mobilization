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
	/// The Nurse officer's thrown HEALING SYRINGE: ai[0] = the wounded soldier's whoAmI, ai[1] = the HP it carries. It flies to that soldier with a
	/// boosted launch speed (<see cref="LevyConstants.Officer.NurseSyringeSpeed"/>) and lightly homes so it connects, then restores the carried HP to full
	/// (capped at what is still missing) and shows the green number. The Nurse has already paid the treasury for the heal when she threw it, so a missed
	/// beat strands nothing: the syringe removes itself if its soldier dies or leaves. Harmless to everything else (it never deals damage).
	/// </summary>
	public class OfficerHealSyringe : ModProjectile
	{
		/// <summary>No asset of its own: vanilla's healing-syringe sprite.</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NurseSyringeHeal;

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.aiStyle = -1;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.penetrate = -1;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LevyConstants.Officer.NurseHealSyringeLifeTicks;
			Projectile.netImportant = true;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
		}

		public override bool? CanDamage() => false;
		public override bool? CanCutTiles() => false;

		private SoldierNPC Target
		{
			get
			{
				int i = (int)Projectile.ai[0];
				if (i < 0 || i >= Main.maxNPCs) return null;
				NPC n = Main.npc[i];
				return n.active && n.ModNPC is SoldierNPC s && !s.OfficerMode ? s : null;
			}
		}

		public override void AI()
		{
			bool authority = Main.netMode != NetmodeID.MultiplayerClient;
			SoldierNPC s = Target;
			if (s == null || (authority && !s.IsAlive))
			{
				if (authority) Projectile.Kill(); // a client may see it before the body arrives: let the authority decide
				return;
			}

			Vector2 aim = s.NPC.Center - Projectile.Center;
			float dist = aim.Length();
			if (authority && dist < LevyConstants.Officer.NurseProximityTiles * LevyConstants.TilePx && Projectile.Hitbox.Intersects(s.NPC.Hitbox))
			{
				Deliver(s);
				Projectile.Kill();
				return;
			}
			float sp = LevyConstants.Officer.NurseSyringeSpeed;
			aim = dist > 0.001f ? aim * (sp / dist) : Vector2.Zero;
			Projectile.velocity = (Projectile.velocity * 8f + aim) / 9f; // blend to the target: a thrown dart that curves in
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

			if (Main.dedServ) return;
			if (Main.rand.NextBool(2))
			{
				int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.HealingPlus, 0f, 0f, 120, default, 0.9f);
				Main.dust[d].noGravity = true;
				Main.dust[d].velocity *= 0.2f;
			}
		}

		/// <summary>Restores the carried HP to the soldier (authority only), capped at what is missing, and shows the green number.</summary>
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
