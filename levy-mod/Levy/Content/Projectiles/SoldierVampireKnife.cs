using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// A soldier's Vampire Knife: ai[0] = the throwing soldier's whoAmI, ai[1] = ticks flown. Flies and damages like the vanilla knife (dead straight for a short flight, then fades
	/// out losing a tenth of its damage a tick; pierces nothing). Vanilla's own knife (type 304) heals Main.player[Main.myPlayer] on every hit, which for a soldier's shot is the
	/// COMMANDER, so soldiers throw this one instead: a hit gives HP back to the SOLDIER only (NPC.life + SoldierRecord.CurrentHp, clamped to his maximum). The commander is never touched.
	/// Owned by the commander and tagged like every soldier shot (see WeaponUse.Spawn), so kills and loot credit the soldier.
	/// </summary>
	public class SoldierVampireKnife : ModProjectile
	{
		/// <summary>No asset of its own: the vanilla Vampire Knife sprite.</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireKnife;

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.aiStyle = -1;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = 1;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.alpha = 255;
			Projectile.light = 0.2f;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 0;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
		}

		public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
		{
			width = LevyConstants.VampKnifeTileBoxPx;
			height = LevyConstants.VampKnifeTileBoxPx;
			return true;
		}

		/// <summary>The soldier that threw this knife, or null when he is gone.</summary>
		private SoldierNPC Soldier
		{
			get
			{
				int i = (int)Projectile.ai[0];
				if (i < 0 || i >= Main.maxNPCs) return null;
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null) return null;
				return s;
			}
		}

		public override void AI()
		{
			float age = ++Projectile.ai[1];
			if (age == 1f) Projectile.alpha = 0;
			float dir = Projectile.velocity.X < 0f ? -1f : 1f;
			Projectile.rotation += (Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y)) * 0.03f * dir;
			if (age >= LevyConstants.VampKnifeFlightTicks)
			{
				Projectile.alpha += LevyConstants.VampKnifeFadePerTick;
				Projectile.damage = (int)(Projectile.damage * 0.9);
				Projectile.knockBack = (int)(Projectile.knockBack * 0.9f);
				if (Projectile.alpha >= 255 && Main.netMode != NetmodeID.MultiplayerClient) Projectile.Kill();
			}
			else
			{
				Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + 1.57f;
			}
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Authoritative side only (the soldier's life lives on the server / in single player). Credit, boss-rule accounting and kill count come from LootCreditGlobalProjectile.
			if (!LevyUtil.IsServerOrSingle) return;
			SoldierNPC body = Soldier;
			if (body == null || !body.IsAlive) return;
			NPC s = body.NPC;
			int heal = Math.Clamp((int)Math.Round(damageDone * LevyConstants.VampKnifeHealFraction), LevyConstants.VampKnifeHealMin, LevyConstants.VampKnifeHealMax);
			heal = Math.Min(heal, s.lifeMax - s.life);
			if (heal <= 0) return;
			s.life += heal;
			body.Record.CurrentHp = s.life;
			s.netUpdate = true;
		}
	}
}
