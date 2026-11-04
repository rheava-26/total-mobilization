using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// A Nebula booster on its way to a soldier in the full Nebula set: ai[0] = the soldier's whoAmI, ai[1] = the booster kind (0 life, 1 damage, 2 mana). Vanilla's Nebula armor drops one of the three
	/// booster PICKUPS (ItemID.NebulaPickup1/2/3, animated glowing orbs) out of an enemy a magic shot hits, and the wearer's pickup code pulls it in (Player.PullItem_Pickup, speed 12, blended 5) and
	/// turns it into a booster buff (Player.PickupItem -> NebulaLevelup). A soldier has no pickup code and a world item would be taken by whoever stood nearest (the commander), so the pickup is
	/// this courier instead: it pops out of the struck enemy on vanilla's drop velocity, then flies to the soldier on vanilla's pull, drawn with the pickup's own sprite, glow and light, and the
	/// soldier takes the booster the moment it reaches him (SoldierNPC.NebulaBoosterArrived). Spawned by SoldierNPC.SetHitEffects from the soldier's own magic hit; it removes itself when its
	/// soldier dies or leaves, so a missed beat strands nothing. MULTIPLAYER: the server (or single player) alone decides the delivery; every screen draws the flight and plays the take effect in OnKill.
	/// </summary>
	public class SoldierNebulaBooster : ModProjectile
	{
		/// <summary>No asset of its own: a vanilla pickup's sheet (the three share a layout; PreDraw picks the booster's own).</summary>
		public override string Texture => "Terraria/Images/Item_" + ItemID.NebulaPickup1;

		public override void SetStaticDefaults() => Main.projFrames[Type] = LevyConstants.NebulaOrbFrames;

		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.NebulaOrbSizePx;
			Projectile.height = LevyConstants.NebulaOrbSizePx;
			Projectile.aiStyle = -1;
			Projectile.friendly = false; // it never hurts anything (WeaponUse.Spawn tags it friendly: CanDamage below keeps it harmless)
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LevyConstants.NebulaOrbTimeLeftTicks;
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
				return n.active && n.ModNPC is SoldierNPC s && s.Record != null ? s : null;
			}
		}

		private int Kind => Math.Clamp((int)Projectile.ai[1], 0, LevyConstants.NebulaPickupItem.Length - 1);

		public override void AI()
		{
			bool authority = Main.netMode != NetmodeID.MultiplayerClient;
			SoldierNPC s = Soldier;
			if (s == null || (authority && !s.IsAlive))
			{
				// A client may see the orb before the body arrives: let the server decide when it is really gone.
				if (authority) Projectile.Kill();
				return;
			}

			int age = (int)Projectile.localAI[0]++;
			Vector2 aim = s.NPC.Center - Projectile.Center;
			float dist = aim.Length();
			if (authority && (dist < LevyConstants.NebulaOrbArriveRangePx || Projectile.Hitbox.Intersects(s.NPC.Hitbox)))
			{
				s.NebulaBoosterArrived(Kind);
				Projectile.Kill(); // OnKill plays the take effect on every screen
				return;
			}

			if (age < LevyConstants.NebulaOrbPopTicks)
			{
				Projectile.velocity *= LevyConstants.NebulaOrbPopFriction; // the pop out of the enemy, drifting on its own weight
			}
			else
			{
				// Player.PullItem_Pickup(item, 12, 5): steer at 12 px a tick, blending four parts of the old velocity with one of the new (faster when the soldier outruns it).
				float speed = Math.Max(LevyConstants.NebulaOrbSpeed, s.NPC.velocity.Length() + LevyConstants.NebulaOrbCatchUpPx);
				aim = dist > 0.001f ? aim * (speed / dist) : Vector2.Zero;
				Projectile.velocity = (Projectile.velocity * (LevyConstants.NebulaOrbPull - 1f) + aim) / LevyConstants.NebulaOrbPull;
			}

			int kind = Kind;
			Vector3 glow = LevyConstants.NebulaPickupLight[kind];
			Lighting.AddLight(Projectile.Center, glow.X, glow.Y, glow.Z);
			if (Main.dedServ || !LevyUtil.IsOnScreen(Projectile.Hitbox)) return;
			if (age % LevyConstants.NebulaOrbTrailEveryTicks == 0)
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, LevyConstants.NebulaPickupDust[kind], -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f), 100, default, 1.1f);
				d.noGravity = true;
			}
		}

		/// <summary>Taken: a burst in the booster's colour and the pickup sound, on every screen (the kill reaches them all). An orb that died on the way (its soldier fell, or it expired) makes no sound.</summary>
		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ) return;
			SoldierNPC s = Soldier;
			if (s == null || Vector2.Distance(Projectile.Center, s.NPC.Center) > LevyConstants.NebulaOrbBurstRangePx || !LevyUtil.IsOnScreen(s.NPC.Hitbox)) return;
			int kind = Kind;
			for (int i = 0; i < LevyConstants.NebulaOrbTakeDust; i++)
			{
				Dust d = Dust.NewDustPerfect(s.NPC.Center, LevyConstants.NebulaPickupDust[kind], Main.rand.NextVector2Circular(3.5f, 3.5f), 100, default, 1.4f);
				d.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.Grab, s.NPC.Center);
		}

		/// <summary>Main.DrawItem for a Nebula pickup: its own four-frame sheet (a frame every six ticks), full-bright with a faint additive edge (Color(255, 255, 255, 50)).</summary>
		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			try
			{
				int item = LevyConstants.NebulaPickupItem[Kind];
				Main.instance.LoadItem(item);
				Texture2D tex = TextureAssets.Item[item].Value;
				int frames = LevyConstants.NebulaOrbFrames;
				Rectangle src = tex.Frame(1, frames, 0, (int)(Main.GameUpdateCount / (uint)LevyConstants.NebulaOrbFrameTicks) % frames);
				Vector2 pos = Projectile.Center - Main.screenPosition;
				float pulse = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI);
				Main.EntitySpriteDraw(tex, pos, src, new Color(255, 255, 255, 50), 0f, src.Size() / 2f, pulse, SpriteEffects.None);
				return false;
			}
			catch (Exception) { return true; }
		}
	}
}
