using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// The floating Crystal Leaf of a soldier in the full Chlorophyte set: ai[0] = the soldier's whoAmI. It hangs above its soldier and fires vanilla's leaf shot
	/// (<see cref="ProjectileID.CrystalLeafShot"/>) at his enemies, dealing the soldier's own damage. Vanilla's leaf (type 226, aiStyle 42) cannot be used: it reads the
	/// flag, position and shot cooldown straight off Main.player[owner], which for a soldier's projectile is the commander, and the commander's own Chlorophyte buff handler
	/// (Player.UpdateBuffs) counts and kills every type-226 projectile that player owns. So this is a copy of that AI with the soldier in the commander's place, drawn with vanilla's sprite.
	/// Owned by the commander and tagged like every soldier shot (see WeaponUse.Spawn); SoldierNPC.MaintainCrystalLeaf keeps exactly one up while the set is worn and removes it when it is not.
	/// </summary>
	public class SoldierCrystalLeaf : ModProjectile
	{
		/// <summary>No asset of its own: the vanilla Crystal Leaf sprite.</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalLeaf;

		public override void SetDefaults()
		{
			Projectile.width = 22; // vanilla's leaf hitbox
			Projectile.height = 42;
			Projectile.aiStyle = -1;
			Projectile.friendly = false; // it never hurts anything itself (WeaponUse.Spawn tags it friendly: CanDamage below keeps it harmless)
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.penetrate = -1;
			Projectile.ignoreWater = true;
			Projectile.light = 0.4f;
			Projectile.timeLeft = 18000;
			Projectile.netImportant = true;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
		}

		public override bool? CanDamage() => false;
		public override bool? CanCutTiles() => false;
		public override bool MinionContactDamage() => false;

		/// <summary>Shot cooldown (authority only; a remote peer never fires) and the retry gap between empty scans.</summary>
		private int _cooldown;

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
			if (s == null || (authority && (!s.IsAlive || s.Record == null || !s.WearsChlorophyteSet)))
			{
				// A client may see the leaf before the body arrives: let the server decide when it is really gone.
				if (authority) Projectile.Kill();
				return;
			}

			Projectile.timeLeft = 18000; // refreshed while the soldier lives and wears the set
			// Vanilla's placement: centred on the wearer, 60 px up, whole pixels; the same slow glow pulse as the player's leaf.
			NPC body = s.NPC;
			Projectile.position.X = (int)(body.Center.X - Projectile.width / 2f);
			Projectile.position.Y = (int)(body.Center.Y - Projectile.height / 2f + body.gfxOffY - LevyConstants.LeafLiftPx);
			Projectile.rotation = 0f;
			Projectile.velocity = Vector2.Zero;
			float pulse = ((int)Main.mouseTextColor / 200f - 0.35f) * 0.2f;
			Projectile.scale = pulse + 0.95f;

			if (!authority) return;
			if (_cooldown > 0) { _cooldown--; return; }
			if (s.Record.HoldFire) return;
			try { Fire(s); }
			catch (Exception) { _cooldown = LevyConstants.LeafCooldownTicks; } // never retry a failing shot every tick
		}

		/// <summary>Picks the target and launches the shot, vanilla's way (intercept lead, speed 12, 180 extra-updates). Sets the cooldown on a shot, a short retry gap on an empty scan.</summary>
		private void Fire(SoldierNPC s)
		{
			NPC target = PickTarget(s);
			if (target == null) { _cooldown = LevyConstants.LeafScanRetryTicks; return; }

			Vector2 from = Projectile.Center;
			Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitX) * LevyConstants.LeafShotSpeed;
			Utils.ChaseResults chase = Utils.GetChaseResults(from, LevyConstants.LeafShotSpeed * LevyConstants.LeafShotLeadUpdates, target.Center, target.velocity);
			if (chase.InterceptionHappens && chase.InterceptionTime <= LevyConstants.LeafShotLeadUpdates)
				vel = chase.ChaserVelocity / LevyConstants.LeafShotLeadUpdates;

			Projectile shot = WeaponUse.Spawn(s, new Vector2(from.X - 4f, from.Y), vel, ProjectileID.CrystalLeafShot, s.CrystalLeafDamage(), Player.crystalLeafKB, DamageClass.Generic);
			_cooldown = LevyConstants.LeafCooldownTicks;
			if (shot != null) shot.netUpdate = true;
		}

		/// <summary>
		/// The soldier's own engaged enemy when it is within the leaf's reach and in line of sight, else the nearest hostile that is (vanilla's metric: |dx|+|dy| under 700 px).
		/// Critters and never-target NPCs are skipped, as for every soldier shot.
		/// </summary>
		private NPC PickTarget(SoldierNPC s)
		{
			Vector2 me = Projectile.Center;
			int pref = s.TargetIndex;
			if (pref >= 0 && pref < Main.maxNPCs && Reachable(Main.npc[pref], me, LevyConstants.LeafRangePx, out _)) return Main.npc[pref];
			NPC best = null; float bestDist = LevyConstants.LeafRangePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				if (Reachable(Main.npc[i], me, bestDist, out float d)) { bestDist = d; best = Main.npc[i]; } // the line-of-sight test only runs for a candidate that would win
			}
			return best;
		}

		/// <summary>A valid hostile closer than <paramref name="limit"/> (|dx|+|dy|) with a clear line from the leaf.</summary>
		private bool Reachable(NPC n, Vector2 me, float limit, out float dist)
		{
			dist = float.MaxValue;
			if (!n.active || n.life <= 0 || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) return false;
			dist = Math.Abs(me.X - n.Center.X) + Math.Abs(me.Y - n.Center.Y);
			if (dist >= limit) return false;
			return Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, n.position, n.width, n.height);
		}
	}
}
