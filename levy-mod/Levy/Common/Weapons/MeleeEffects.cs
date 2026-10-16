using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Broadsword hits. A swing deals damage straight to the NPC, so nothing in vanilla's melee hit path ran for it. This runs that path:
	/// the real hook chain (<see cref="StrikeNpc"/>: ModifyHitNPC, item statuses, OnHitNPC on ModItem / GlobalItem / ModPlayer, with the soldier's dummy as the player)
	/// and the effects vanilla hard-codes per item in Player.ApplyNPCOnHitEffects (<see cref="ApplyVanillaMeleeHit"/>).
	/// </summary>
	public static class MeleeEffects
	{
		private static bool[] _hookBefore;
		private static readonly Point[] _tentacleBuffer = new Point[LevyConstants.JavelinBarbsPerTarget];
		private static readonly Point[] _butcherBuffer = new Point[LevyConstants.JavelinBarbsPerTarget];

		/// <summary>
		/// Strikes <paramref name="n"/> the way Player.ProcessHitAgainstNPC does: incoming-hit modifiers, ModifyHit hooks, the item's status effects,
		/// the strike itself (once; damage is never applied twice), then the OnHit hooks. Projectiles a hook spawned are re-owned and tagged as this soldier's.
		/// Returns the damage dealt; <paramref name="sourceDamage"/> is the hit's pre-defense damage (what vanilla passes to its on-hit effects).
		/// </summary>
		public static int StrikeNpc(SoldierNPC s, Item item, NPC n, int damage, int dir, bool crit, float knockback, out int sourceDamage)
		{
			DamageClass cls = item.DamageType ?? DamageClass.Melee;
			sourceDamage = Math.Max(1, damage);
			Player d = s.Dummy;
			if (d == null || !LevyConfig.Get.RealWeaponShoot)
				return n.SimpleStrikeNPC(damage, dir, crit, knockback, cls, true, 0f, true);

			bool[] before = _hookBefore ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;

			int savedWho = d.whoAmI, savedDir = d.direction; Vector2 savedCenter = d.Center;
			int done = 0; bool struck = false;
			try
			{
				// Hooks spawn projectiles with player.whoAmI as the owner: make it the soldier owner so ai[] is assigned and the spawn is synced (as TryRealShoot does).
				d.whoAmI = WeaponUse.OwnerFor(s);
				d.Center = s.NPC.Center;
				d.direction = s.NPC.direction == 0 ? 1 : s.NPC.direction;
				if (!ReferenceEquals(d.inventory[0], item)) d.inventory[0] = item;
				d.selectedItem = 0;

				NPC.HitModifiers mods = n.GetIncomingStrikeModifiers(cls, dir);
				ItemDamageModifiers(item, n, ref mods);
				try { CombinedHooks.ModifyPlayerHitNPCWithItem(d, item, n, ref mods); } catch (Exception) { }
				try { d.StatusToNPC(item.type, n.whoAmI); } catch (Exception) { } // item debuffs + the dummy's Magma Stone / Frostburn-style flags

				NPC.HitInfo hit = mods.ToHitInfo(damage, crit, knockback, true, 0f);
				struck = true; // from here a throw must not trigger the fallback strike: the damage may already be applied
				done = n.StrikeNPC(hit, false, true);
				if (Main.netMode != NetmodeID.SinglePlayer) NetMessage.SendStrikeNPC(n, in hit);
				sourceDamage = Math.Max(1, hit.SourceDamage);
				try { CombinedHooks.OnPlayerHitNPCWithItem(d, item, n, in hit, done); } catch (Exception) { }
			}
			catch (Exception)
			{
				if (!struck) done = n.SimpleStrikeNPC(damage, dir, crit, knockback, cls, true, 0f, true);
			}
			finally { d.whoAmI = savedWho; d.direction = savedDir; d.Center = savedCenter; }

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || before[i]) continue;
				WeaponUse.TagSoldierProjectile(s, p, cls);
			}
			return done;
		}

		/// <summary>Damage multipliers vanilla applies per item in ProcessHitAgainstNPC before the strike (Breaker Blade, Keybrand).</summary>
		private static void ItemDamageModifiers(Item item, NPC n, ref NPC.HitModifiers mods)
		{
			switch (item.type)
			{
				case ItemID.BreakerBlade:
					if (n.life >= n.lifeMax * LevyConstants.BreakerBladeHealthFraction) mods.SourceDamage *= LevyConstants.BreakerBladeBonus;
					break;
				case ItemID.Keybrand:
					mods.SourceDamage *= 1f + Utils.GetLerpValue(1f, 0.1f, n.life / (float)Math.Max(1, n.lifeMax), clamped: true);
					break;
			}
		}

		/// <summary>
		/// The hard-coded melee hit effects of Player.ApplyNPCOnHitEffects: Bee Keeper bees, Volcano blast, Muramasa cut, Bladetongue ichor,
		/// Blood Butcherer / Tentacle Spike barbs and Bat Bat's life leech. Call after the strike with the hit's source damage.
		/// Everything spawned is tagged to the soldier. Never throws.
		/// </summary>
		public static void ApplyVanillaMeleeHit(SoldierNPC s, Item item, NPC target, int damage, Rectangle swingBox = default)
		{
			if (s == null || item == null || item.IsAir || target == null) return;
			try
			{
				int dir = s.NPC.direction == 0 ? 1 : s.NPC.direction;
				DamageClass cls = item.DamageType ?? DamageClass.Melee;
				bool hittable = !target.immortal;
				switch (item.type)
				{
					case ItemID.BeeKeeper:
						if (hittable) SpawnBees(s, target, damage, dir, swingBox);
						break;
					case ItemID.FieryGreatsword: // "Volcano"
						if (!s.SwingSpecialSpent && target.HittableForOnHitRewards())
						{
							WeaponUse.Spawn(s, target.Center, new Vector2(0f, -1f), ProjectileID.Volcano, (int)(damage * LevyConstants.VolcanoBlastDamageFactor), item.knockBack, cls, 0f, 2f);
							s.SwingSpecialSpent = true;
						}
						break;
					case ItemID.Muramasa:
						if (!s.SwingSpecialSpent && hittable)
						{
							s.SwingSpecialSpent = true;
							SpawnMuramasaCut(s, target, damage, dir, cls);
						}
						break;
					case ItemID.Bladetongue:
						SpawnIchor(s, item, target, damage, dir, swingBox, cls);
						break;
					case ItemID.BloodButcherer:
						if (!s.SwingSpecialSpent && target.CanBeChasedBy(s.Dummy)) { SpawnBarb(s, item, target, damage, 975, true, cls); s.SwingSpecialSpent = true; }
						break;
					case ItemID.TentacleSpike:
						if (!s.SwingSpecialSpent && target.CanBeChasedBy(s.Dummy)) { SpawnBarb(s, item, target, damage, 971, false, cls); s.SwingSpecialSpent = true; }
						break;
					case ItemID.BatBat:
						if (!s.SwingSpecialSpent && s.NPC.life < s.NPC.lifeMax && target.HittableForOnHitRewards())
						{
							s.SwingSpecialSpent = true;
							s.NPC.life = Math.Min(s.NPC.lifeMax, s.NPC.life + 1);
							s.NPC.HealEffect(1, false);
							s.NPC.netUpdate = true;
						}
						break;
				}
			}
			catch (Exception) { }
		}

		/// <summary>Boomerang hit debuffs vanilla keys on the projectile type (Projectile.StatusNPC): Flamarang burns, Ice Boomerang chills, Thorn Chakram poisons.</summary>
		public static void ApplyBoomerangStatus(int shootType, NPC target)
		{
			if (target == null || !target.active) return;
			try
			{
				if (shootType == ProjectileID.Flamarang) { if (Main.rand.NextBool(2)) target.AddBuff(BuffID.OnFire, 360); }
				else if (shootType == ProjectileID.IceBoomerang) { if (Main.rand.NextBool(2)) target.AddBuff(BuffID.Frostburn, 180); }
				else if (shootType == ProjectileID.ThornChakram) { if (Main.rand.NextBool(2)) target.AddBuff(BuffID.Poisoned, 420); }
			}
			catch (Exception) { }
		}

		private static void SpawnBees(SoldierNPC s, NPC target, int damage, int dir, Rectangle box)
		{
			Player d = s.Dummy;
			int count = Main.rand.Next(1, 4);
			if (d != null && d.strongBees && Main.rand.NextBool(3)) count++;
			Vector2 origin = box.Width > 0 && box.Height > 0 ? box.Center.ToVector2() : (s.NPC.Center + target.Center) * 0.5f;
			for (int k = 0; k < count; k++)
			{
				float vx = (dir * 2f + Main.rand.Next(-35, 36) * 0.02f) * 0.2f;
				float vy = Main.rand.Next(-35, 36) * 0.02f * 0.2f;
				int type = d != null ? d.beeType() : ProjectileID.Bee;
				int beeDmg = d != null ? d.beeDamage(damage / LevyConstants.BeeKeeperDamageDivisor) : damage / LevyConstants.BeeKeeperDamageDivisor;
				float kb = d != null ? d.beeKB(0f) : 0f;
				WeaponUse.Spawn(s, origin, new Vector2(vx, vy), type, beeDmg, kb, DamageClass.Melee);
			}
		}

		private static void SpawnMuramasaCut(SoldierNPC s, NPC target, int damage, int dir, DamageClass cls)
		{
			Rectangle hb = target.Hitbox;
			hb.Inflate(30, 16);
			hb.Y -= 8;
			Vector2 start = Main.rand.NextVector2FromRectangle(hb);
			Vector2 center = hb.Center.ToVector2();
			Vector2 step = (center - start).SafeNormalize(new Vector2(dir, 1f)) * 8f;
			Main.rand.NextFloat();
			float turn = (Main.rand.Next(2) * 2 - 1) * (MathHelper.Pi / 5f + MathHelper.Pi * 4f / 5f * Main.rand.NextFloat());
			turn *= 0.5f;
			step = step.RotatedBy(MathHelper.PiOver4);
			const int scale = 3, stepsDiv = 10 * scale, lead = 5, steps = lead * scale;
			Vector2 pos = center;
			for (int k = 0; k < steps; k++)
			{
				pos -= step;
				step = step.RotatedBy(-turn / stepsDiv);
			}
			pos += target.velocity * lead;
			WeaponUse.Spawn(s, pos, step, ProjectileID.Muramasa, (int)(damage * LevyConstants.MuramasaCutDamageFactor), 0f, cls, turn);
		}

		private static void SpawnIchor(SoldierNPC s, Item item, NPC target, int damage, int dir, Rectangle box, DamageClass cls)
		{
			if (target.immortal) return;
			Vector2 v = new Vector2(dir * 100 + Main.rand.Next(-25, 26), Main.rand.Next(-75, 76));
			v.Normalize();
			v *= Main.rand.Next(30, 41) * 0.1f;
			Rectangle r = box.Width > 0 && box.Height > 0 ? box : target.Hitbox;
			Vector2 pos = new Vector2(r.X + Main.rand.Next(Math.Max(1, r.Width)), r.Y + Main.rand.Next(Math.Max(1, r.Height)));
			pos = (pos + target.Center * 2f) / 3f;
			WeaponUse.Spawn(s, pos, v, ProjectileID.IchorSplash, (int)(damage * LevyConstants.BladetongueIchorDamageFactor), item.knockBack * LevyConstants.BladetongueIchorKnockFactor, cls);
		}

		/// <summary>Blood Butcherer / Tentacle Spike: a barb that sticks into the enemy (ai0 = 1, ai1 = its index); a sixth barb pulls the oldest out.</summary>
		private static void SpawnBarb(SoldierNPC s, Item item, NPC target, int damage, int projType, bool butcher, DamageClass cls)
		{
			Vector2 from = s.NPC.Center;
			Vector2 toward = (target.Center - from).SafeNormalize(Vector2.Zero);
			Vector2 pos = target.Hitbox.ClosestPointInRect(from) + toward;
			Vector2 vel = (target.Center - pos) * 0.8f;
			if (butcher) vel = vel.RotatedBy(Main.rand.NextFloatDirection() * MathHelper.Pi * 0.25f);
			Projectile p = WeaponUse.Spawn(s, pos, vel, projType, damage, item.knockBack, cls, 1f, target.whoAmI);
			if (p == null) return;
			p.StatusNPC(target.whoAmI);
			Projectile.KillOldestJavelin(p.whoAmI, projType, target.whoAmI, butcher ? _butcherBuffer : _tentacleBuffer);
		}
	}
}
