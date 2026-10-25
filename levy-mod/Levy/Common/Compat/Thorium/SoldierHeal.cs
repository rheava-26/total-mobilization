using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;
using Levy.Common.Util;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// A soldier's healing bolt. ai[0] = the wounded soldier's NPC index (or <see cref="PlayerTarget"/> for the commander player), ai[1] = hit points restored. It homes onto that ally and restores
	/// its life on contact (server / single player), then vanishes. Deals no damage and ignores terrain, so a heal never harms an enemy
	/// and never gets lost behind a ledge.
	/// </summary>
	public class SoldierHeal : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.AmethystBolt;

		public override void SetDefaults()
		{
			Projectile.width = 12; Projectile.height = 12;
			Projectile.aiStyle = -1;
			Projectile.friendly = false; Projectile.hostile = false;
			Projectile.tileCollide = false; Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 240;
			Projectile.alpha = 255;
		}

		public override bool? CanHitNPC(NPC target) => false;
		public override bool CanHitPlayer(Player target) => false;
		public override bool? CanDamage() => false;
		public override bool PreDraw(ref Color lightColor) => false;

		/// <summary>ai[0] value that targets a PLAYER: -(playerIndex + 1). A non-negative value is a soldier's NPC index.</summary>
		public static float PlayerTarget(int playerIndex) => -(playerIndex + 1);

		public override void AI()
		{
			int code = (int)Projectile.ai[0];
			NPC t = null; Player pl = null;
			if (code < 0)
			{
				int pi = -code - 1;
				pl = pi >= 0 && pi < Main.maxPlayers ? Main.player[pi] : null;
				if (pl == null || !pl.active || pl.dead) { Projectile.Kill(); return; }
			}
			else
			{
				t = code < Main.maxNPCs ? Main.npc[code] : null;
				if (t == null || !t.active || t.life <= 0 || t.ModNPC is not SoldierNPC) { Projectile.Kill(); return; }
			}
			Vector2 center = pl != null ? pl.Center : t.Center;
			Rectangle box = pl != null ? pl.Hitbox : t.Hitbox;

			Vector2 to = center - Projectile.Center;
			float dist = to.Length();
			if (dist < 14f || Projectile.Hitbox.Intersects(box)) { Arrive(t, pl); return; }

			float speed = System.Math.Max(7f, Projectile.velocity.Length());
			Vector2 want = to / dist * speed;
			Vector2 v = Vector2.Lerp(Projectile.velocity, want, 0.25f);
			Projectile.velocity = v.LengthSquared() > 0.01f ? Vector2.Normalize(v) * speed : want;
			Projectile.rotation = Projectile.velocity.ToRotation();

			if (Main.netMode != NetmodeID.Server)
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Vector2.Zero, 100, default, 1.1f);
				d.noGravity = true;
			}
		}

		private void Arrive(NPC t, Player pl)
		{
			Vector2 at = pl != null ? pl.Center : t.Center;
			if (LevyUtil.IsServerOrSingle)
			{
				int heal = System.Math.Max(0, (int)Projectile.ai[1]);
				if (pl != null) CompanionFx.HealPlayer(pl, heal);
				else
				{
					int before = t.life;
					t.life = System.Math.Min(t.lifeMax, t.life + heal);
					int gained = t.life - before;
					if (gained > 0)
					{
						if (t.ModNPC is SoldierNPC { Record: not null } s) s.Record.CurrentHp = t.life;
						t.HealEffect(gained, true);
						t.netUpdate = true;
					}
				}
			}
			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 8; i++)
				{
					Dust d = Dust.NewDustPerfect(at, DustID.GoldFlame, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.2f);
					d.noGravity = true;
				}
			}
			Projectile.Kill();
		}
	}
}
