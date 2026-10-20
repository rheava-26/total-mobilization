using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Per-item shots vanilla hard-codes in Player.ItemCheck_Shoot after the Shoot hooks return true, for weapons whose projectile needs more than
	/// "spawn it and aim it" (extra ai[] values, a spawn point away from the player, several projectiles, a damage multiplier). Cursor positions are
	/// replaced by the target. Called from WeaponUse.DefaultShoot; every method spawns tagged soldier projectiles.
	/// </summary>
	public static class ItemShots
	{
		/// <summary>
		/// Vanilla's pre-shoot cleanup (Player.ItemCheck_Shoot, before the Shoot hooks): firing the Rainbow Gun kills the owner's existing rainbow (front and back pieces) first, so a player
		/// only ever has one. Soldier shots skip ItemCheck_Shoot, so each shot would stack another rainbow for 40 s and the pile exhausts the global projectile cap. This does the same
		/// kill scoped to the FIRING soldier (matched by <see cref="SoldierProjectileTag.SoldierIndex"/>): the commander's own rainbow and other soldiers' rainbows are untouched.
		/// Call it before the shot is spawned, whichever path spawns it.
		/// </summary>
		public static void PreShoot(SoldierNPC s, int type)
		{
			if (s == null || type != ProjectileID.RainbowFront) return;
			int soldier = s.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || (p.type != ProjectileID.RainbowFront && p.type != ProjectileID.RainbowBack)) continue;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == soldier) p.Kill();
			}
		}

		/// <summary>Fires the item's special shot with the hook-modified stats. Returns false when the item has none (the caller then fires the plain shot).</summary>
		public static bool TryShoot(SoldierNPC s, Item item, WeaponProfile profile, NPC target, Vector2 pos, Vector2 vel, int type, int dmg, float kb)
		{
			if (s == null || item == null || target == null) return false;
			DamageClass cls = item.DamageType ?? profile.DamageType;
			switch (item.type)
			{
				case ItemID.Starfury: return Starfury(s, target, vel, type, dmg, cls);
				case ItemID.LightsBane: return LightsBane(s, item, target, type, dmg, kb, cls);
				case ItemID.BladeofGrass: return BladeOfGrass(s, target, type, dmg, kb, cls);
				case ItemID.BeeGun: return BeeGun(s, pos, vel, dmg, kb);
				case ItemID.HellwingBow: return Hellwing(s, pos, vel, type, dmg, kb, cls);
				case ItemID.CrimsonRod:
				case ItemID.NimbusRod:
					// The cloud flies to the point in ai[0..1] and rains there: vanilla stores the cursor, the soldier's target stands in.
					return WeaponUse.Spawn(s, pos, vel, type, dmg, kb, cls, target.Center.X, target.Center.Y) != null;
				case ItemID.ClingerStaff: return ClingerStaff(s, item, target, pos, vel, type, dmg, kb, cls);
				case ItemID.Zenith: return Zenith(s, item, target, type, dmg, kb, cls);
				case ItemID.IceRod:
					// The ice block flies to the tile in ai[0..1] (vanilla: the cursor tile).
					return WeaponUse.Spawn(s, pos, vel, type, dmg, kb, cls, (int)(target.Center.X / 16f), (int)(target.Center.Y / 16f)) != null;
			}
			return false;
		}

		/// <summary>Starfury: stars fall from the sky onto the target for 1.5x damage and no knockback; they pass through terrain until they reach the target's height.</summary>
		private static bool Starfury(SoldierNPC s, NPC target, Vector2 vel, int type, int dmg, DamageClass cls)
		{
			int dir = s.NPC.direction == 0 ? 1 : s.NPC.direction;
			float speed = Math.Max(1f, vel.Length());
			Vector2 origin = new Vector2(target.Center.X + Main.rand.Next(LevyConstants.StarfuryDropSpreadPx + 1) * -dir, s.NPC.Center.Y - LevyConstants.StarfuryDropHeightPx);
			Vector2 aim = target.Center - origin;
			Vector2 v = aim.SafeNormalize(Vector2.UnitY) * speed;
			Vector2 landing = target.Center;
			Vector2 back = (origin - target.Center).SafeNormalize(new Vector2(0f, -1f));
			for (int guard = 0; guard < 200 && landing.Y > origin.Y && WorldGen.SolidTile(landing.ToTileCoordinates()); guard++) landing += back * 16f;
			return WeaponUse.Spawn(s, origin, v, type, (int)(dmg * LevyConstants.StarfuryDamageFactor), 0f, cls, 0f, landing.Y) != null;
		}

		/// <summary>Light's Bane: a slash that appears on the enemy in reach (a crit makes it bigger and doubles it before the halving).</summary>
		private static bool LightsBane(SoldierNPC s, Item item, NPC target, int type, int dmg, float kb, DamageClass cls)
		{
			int dir = s.NPC.direction == 0 ? 1 : s.NPC.direction;
			Vector2 heading = new Vector2(dir, 4f).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.TwoPi * Main.rand.NextFloatDirection() * 0.05f);
			Vector2 search = s.NPC.Center + new Vector2(70f * dir, -40f) + heading * -10f;
			NPC near = NearestChaseable(search, 50f);
			if (near != null) search = near.Center + Main.rand.NextVector2Circular(near.width / 2, near.height / 2);
			else search += Main.rand.NextVector2Circular(20f, 20f);
			float scale = 1f;
			int crit = s.Dummy != null ? s.Dummy.GetWeaponCrit(item) : LevyConstants.BaseCritChance;
			if (Main.rand.Next(100) < crit) { scale = 2f; dmg *= 2; }
			return WeaponUse.Spawn(s, search, heading * 0.001f, type, (int)(dmg * LevyConstants.LightsBaneDamageFactor), kb, cls, scale) != null;
		}

		/// <summary>Blade of Grass: a vine that curves out of the body toward the nearest enemy (ai0 = turn per step, ai1 = steps).</summary>
		private static bool BladeOfGrass(SoldierNPC s, NPC target, int type, int dmg, float kb, DamageClass cls)
		{
			int dir = s.NPC.direction == 0 ? 1 : s.NPC.direction;
			Vector2 aimPoint = s.NPC.Center + new Vector2(70f * dir, -40f);
			NPC near = NearestChaseable(aimPoint, LevyConstants.BladeOfGrassTargetRadiusPx);
			bool locked = near != null;
			if (locked) aimPoint = Main.rand.NextVector2FromRectangle(near.Hitbox);
			else aimPoint += Main.rand.NextVector2Circular(20f, 20f);
			Vector2 origin = s.NPC.Center + new Vector2(Main.rand.NextFloatDirection() * s.NPC.width / 2f * dir, s.NPC.height / 2f);
			Vector2 toAim = aimPoint - origin;
			float arc = (MathHelper.Pi + MathHelper.TwoPi * Main.rand.NextFloat() * 1.5f) * -dir;
			const int maxSteps = 60;
			float turn = arc / maxSteps;
			const float stepLen = 16f;
			float dist = toAim.Length();
			if (Math.Abs(turn) >= 0.17f) turn *= 0.7f;
			Vector2 step = Vector2.UnitX * stepLen, reach = step;
			int steps = 0;
			while (reach.Length() < dist && steps < maxSteps)
			{
				steps++;
				reach += step;
				step = step.RotatedBy(turn);
			}
			Vector2 launch = toAim.SafeNormalize(Vector2.UnitY).RotatedBy(-reach.ToRotation() - turn) * stepLen;
			if (steps == maxSteps) launch = new Vector2(dir, 0f) * stepLen;
			if (!locked)
			{
				origin.Y -= 24f;
				launch = launch.RotatedBy(dir * MathHelper.TwoPi * 0.14f);
			}
			return WeaponUse.Spawn(s, origin, launch, type, (int)(dmg * LevyConstants.BladeOfGrassDamageFactor), kb, cls, turn, steps) != null;
		}

		/// <summary>Bee Gun: one to four bees (strong bees with the Hive Pack) instead of a single bee.</summary>
		private static bool BeeGun(SoldierNPC s, Vector2 pos, Vector2 vel, int dmg, float kb)
		{
			Player d = s.Dummy;
			int count = Main.rand.Next(1, 4);
			if (Main.rand.NextBool(6)) count++;
			if (Main.rand.NextBool(6)) count++;
			if (d != null && d.strongBees && Main.rand.NextBool(3)) count++;
			int spawned = 0;
			for (int k = 0; k < count; k++)
			{
				Vector2 v = vel + new Vector2(Main.rand.Next(-35, 36), Main.rand.Next(-35, 36)) * 0.02f;
				int type = d != null ? d.beeType() : ProjectileID.Bee;
				int beeDmg = d != null ? d.beeDamage(dmg) : dmg;
				float beeKb = d != null ? d.beeKB(kb) : kb;
				if (WeaponUse.Spawn(s, pos, v, type, beeDmg, beeKb, DamageClass.Magic) != null) spawned++;
			}
			return spawned > 0;
		}

		/// <summary>Hellwing Bow: each bat gets a scattered velocity and a target velocity in ai[0..1] that it eases toward, so it curves.</summary>
		private static bool Hellwing(SoldierNPC s, Vector2 pos, Vector2 vel, int type, int dmg, float kb, DamageClass cls)
		{
			float len = vel.Length();
			if (len < 0.01f) return false;
			Vector2 pull = vel;
			pull.X += Main.rand.Next(-100, 101) * 0.01f * len * LevyConstants.HellwingAimScatter;
			pull.Y += Main.rand.Next(-100, 101) * 0.01f * len * LevyConstants.HellwingAimScatter;
			Vector2 v = new Vector2(vel.X + Main.rand.Next(-40, 41) * 0.03f, vel.Y + Main.rand.Next(-40, 41) * 0.03f);
			pull.Normalize();
			pull *= len;
			v.X *= Main.rand.Next(50, 150) * 0.01f;
			v.Y *= Main.rand.Next(50, 150) * 0.01f;
			v.X += Main.rand.Next(-100, 101) * 0.025f;
			v.Y += Main.rand.Next(-100, 101) * 0.025f;
			if (v == Vector2.Zero) v = pull;
			v.Normalize();
			v *= len;
			return WeaponUse.Spawn(s, pos, v, type, dmg, kb, cls, pull.X, pull.Y) != null;
		}

		/// <summary>
		/// Clinger Staff (Player.ItemCheck_Shoot item 3014): a Cursed Flame wall. The shot marches from the muzzle toward the target until the line is blocked or the target is reached, then
		/// scans the tile column there: ai[0] = Y of the wall's top, ai[1] = its height (at most 15 tiles), on the tile column. No ground within 30 tiles below means no wall (vanilla spawns nothing).
		/// </summary>
		private static bool ClingerStaff(SoldierNPC s, Item item, NPC target, Vector2 pos, Vector2 vel, int type, int dmg, float kb, DamageClass cls)
		{
			Vector2 aim = target.Center;
			Vector2 step = vel.LengthSquared() > 0.0001f ? vel : (aim - pos).SafeNormalize(Vector2.UnitX) * Math.Max(1f, item.shootSpeed);
			NPC body = s.NPC;
			for (int guard = 0; guard < LevyConstants.ClingerMarchMaxSteps && Collision.CanHitLine(body.position, body.width, body.height, pos, 1, 1); guard++)
			{
				pos += step;
				if ((pos - aim).Length() < LevyConstants.ClingerArrivePx + Math.Abs(step.X) + Math.Abs(step.Y)) { pos = aim; break; }
			}
			int col = Math.Clamp((int)(pos.X / 16f), 1, Main.maxTilesX - 2);
			int row0 = Math.Clamp((int)(pos.Y / 16f), 11, Main.maxTilesY - 12);
			int row = row0;
			while (row < Main.maxTilesY - 10 && row - row0 < LevyConstants.ClingerScanTiles && !SolidOrPlatform(col, row)) row++;
			if (!SolidOrPlatform(col, row)) return true; // nothing to stand a wall on: vanilla spawns nothing and still spends the cast
			float floorY = row * 16;
			row = row0;
			while (row > 10 && row0 - row < LevyConstants.ClingerScanTiles && !WorldGen.SolidTile(col, row)) row--;
			float topY = row * 16 + 16;
			float height = Math.Min(floorY - topY, 16 * LevyConstants.ClingerMaxWallTiles);
			topY = floorY - height;
			Vector2 at = new Vector2((int)(pos.X / 16f) * 16, pos.Y);
			return WeaponUse.Spawn(s, at, Vector2.Zero, type, dmg, kb, cls, topY, height) != null;
		}

		private static bool SolidOrPlatform(int x, int y) => WorldGen.SolidTile(x, y) || TileID.Sets.Platforms[Main.tile[x, y].TileType];

		/// <summary>
		/// Zenith (Player.ItemCheck_Shoot item 4956): per swing useAnimation / useTime swords (three). Each launch velocity is half the offset from the body to the target (the swirl's radius), ai[0] a
		/// random arc width, ai[1] the sword-profile index (the first is the Zenith itself, the rest random; Graphics.FinalFractalHelper). The last sword is scattered. The swords' AI is anchored to the
		/// soldier by <see cref="MiscProjSwap"/>.
		/// </summary>
		private static bool Zenith(SoldierNPC s, Item item, NPC target, int type, int dmg, float kb, DamageClass cls)
		{
			int count = Math.Clamp(Math.Max(1, item.useAnimation) / Math.Max(1, item.useTime), 1, LevyConstants.ZenithMaxShotsPerSwing);
			Vector2 origin = s.NPC.Center;
			int spawned = 0;
			for (int k = 0; k < count; k++)
			{
				Vector2 offset = target.Center - origin;
				if (k == count - 1 && count > 1) offset += Main.rand.NextVector2Circular(LevyConstants.ZenithScatterPx, LevyConstants.ZenithScatterPx);
				if (offset.LengthSquared() < 1f) offset = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
				if (offset.Length() > LevyConstants.ZenithMaxAimPx) offset = Vector2.Normalize(offset) * LevyConstants.ZenithMaxAimPx;
				int profile = k == 0 ? ItemID.Zenith : FinalFractalHelper.GetRandomProfileIndex();
				float arc = Main.rand.Next(-LevyConstants.ZenithArcRoll, LevyConstants.ZenithArcRoll + 1);
				if (WeaponUse.Spawn(s, origin, offset / 2f, type, dmg, kb, cls, arc, profile) != null) spawned++;
			}
			return spawned > 0;
		}

		/// <summary>The nearest enemy within <paramref name="maxDist"/> of <paramref name="from"/> that a player's homing weapons could chase (Player.GetZenithTarget).</summary>
		private static NPC NearestChaseable(Vector2 from, float maxDist)
		{
			NPC best = null; float bestDist = maxDist;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !n.CanBeChasedBy()) continue;
				float d = from.Distance(n.Center);
				if (d < bestDist) { best = n; bestDist = d; }
			}
			return best;
		}
	}
}
