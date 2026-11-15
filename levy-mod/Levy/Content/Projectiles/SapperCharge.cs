using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// The Demolitionist officer's stick of dynamite. It looks, arcs, bounces and blows like vanilla's (same sprite, a 300 tick fuse, a 250 pixel blast hitting enemies in the last three ticks) but it is its own
	/// projectile, so vanilla's tile-breaking explosion code (which belongs to ProjectileID.Dynamite) never runs for it: the blast hurts enemies and changes nothing in the world. The army never alters the world
	/// (<see cref="Levy.Common.Weapons.WorldAlter"/>); vanilla's Dynamite measured 211 tiles and walls gone in the sandbox, this one measures none (TileSweepCheck).
	/// </summary>
	public class SapperCharge : ModProjectile
	{
		public const int FuseTicks = 300, BlastTicks = 3, BlastSize = 250;

		/// <summary>No asset of its own: vanilla's stick of dynamite.</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Dynamite;

		public override void SetDefaults()
		{
			Projectile.width = 10; Projectile.height = 10;
			Projectile.aiStyle = -1;
			Projectile.friendly = true; Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.timeLeft = FuseTicks;
			Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 10;
			Projectile.npcProj = true; Projectile.noDropItem = true;
		}

		public override bool? CanCutTiles() => false;
		/// <summary>Nothing is hurt by the stick while it flies, only by the blast.</summary>
		public override bool? CanHitNPC(NPC target) => Projectile.timeLeft <= BlastTicks ? null : false;
		public override bool CanHitPlayer(Player target) => false;
		public override bool CanHitPvp(Player target) => false;

		public override void AI()
		{
			if (Projectile.timeLeft > BlastTicks)
			{
				Projectile.rotation += Projectile.velocity.X * 0.06f;
				Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + 0.2f, 16f);
				if (Projectile.velocity.Y == 0f) Projectile.velocity.X *= 0.97f;
				if (!Main.dedServ && Main.rand.NextBool(3)) Dust.NewDust(Projectile.position + new Vector2(0f, -6f), Projectile.width, 4, DustID.Smoke, 0f, -1f, 100, default, 0.8f); // the burning fuse
				return;
			}
			if (Projectile.timeLeft == BlastTicks)
			{
				Projectile.tileCollide = false; Projectile.velocity = Vector2.Zero; Projectile.alpha = 255;
				Projectile.Resize(BlastSize, BlastSize);
				Projectile.knockBack = 8f;
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.4f;
			if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = oldVelocity.Y > 0.7f ? -oldVelocity.Y * 0.3f : 0f;
			Projectile.velocity.X *= 0.9f;
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ) return;
			SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
			for (int i = 0; i < 30; i++) { var d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.8f); d.velocity *= 1.4f; }
			for (int i = 0; i < 20; i++) { var d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 2.6f); d.noGravity = true; d.velocity *= 5f; }
		}
	}
}
