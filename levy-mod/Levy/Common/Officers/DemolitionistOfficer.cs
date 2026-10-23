using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE SAPPER. The Demolitionist is a FRONTLINE officer that marches with the line and lobs arcing grenades at any enemy in reach, and on a
	/// cadence hurls a stick of dynamite at the nearest hostile for a heavy blast. The grenade is his <see cref="OfficerWeapon"/>; the dynamite is
	/// the kit's doing. Both blasts scale with the world's progress like a vanilla town NPC's shot.
	/// </summary>
	public static class DemolitionistOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Demolitionist,
			Key = "Demolitionist",
			CostMult = DemolitionistCostMult,
			Aggro = DemolitionistAggro,
			Posture = OfficerPosture.Frontline,          // marches with the army and fights what comes in reach
			// The lobbed grenade: a slow arcing throw with a heavy gravity lead, so it falls onto a target in range.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.Grenade,
				Speed = DemoGrenadeSpeed, GravityCorrection = DemoGrenadeGravityCorrection,
				Damage = DemoGrenadeDamage, Knockback = DemoGrenadeKnockback,
				Delay = 1, Cooldown = DemoGrenadeCooldown, RandomCooldown = DemoGrenadeRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Demolitionist]),
				RangeTiles = DemoGrenadeRangeTiles,
			},
			Kit = new DemolitionistKit(),
		};
	}

	/// <summary>
	/// The Demolitionist's ability: every <see cref="LevyConstants.Officer.DemoDynamiteIntervalTicks"/> he lobs a stick of dynamite at the nearest
	/// hostile within <see cref="LevyConstants.Officer.DemoDynamiteRangeTiles"/> tiles for a heavy blast. The throw is gravity-led like his grenade
	/// and the damage scales with boss progress. Nothing within reach, no throw — the timer simply waits.
	/// </summary>
	public sealed class DemolitionistKit : OfficerKit
	{
		private sealed class State { public int Timer; }

		public override object CreateState() => new State();

		// {0} seconds between dynamite throws
		public override object[] DescriptionArgs => new object[] { DemoDynamiteIntervalTicks / 60 };

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < DemoDynamiteIntervalTicks) return;
			st.Timer = 0;

			NPC demo = ctx.Npc;
			float reach = DemoDynamiteRangeTiles * LevyConstants.TilePx;
			NPC target = NearestInReach(demo.Center, reach);
			if (target == null) return;                        // nobody in blast range: hold the charge

			int dir = demo.spriteDirection == 0 ? 1 : demo.spriteDirection;
			float frac = MathHelper.Clamp(Vector2.Distance(demo.Center, target.Center) / reach, 0f, 1f);
			Vector2 aim = demo.DirectionTo(target.Center + new Vector2(0f, -DemoDynamiteGravityCorrection * frac));
			if (aim.HasNaNs()) aim = new Vector2(dir, -1f);
			aim *= DemoDynamiteSpeed;

			int dmg = (int)(DemoDynamiteDamage * OfficerBrain.TownDamageScale() * AttackDamageMult);
			if (Main.expertMode) dmg = (int)(dmg * Main.GameModeInfo.TownNPCDamageMultiplier);
			int idx = Projectile.NewProjectile(demo.GetSource_FromAI(), demo.Center.X + dir * 16f, demo.Center.Y - 2f,
				aim.X, aim.Y, ProjectileID.Dynamite, Math.Max(1, dmg), DemoDynamiteKnockback, Main.myPlayer);
			if (idx >= 0 && idx < Main.maxProjectiles) { Main.projectile[idx].npcProj = true; Main.projectile[idx].noDropItem = true; }

			demo.direction = target.Center.X < demo.Center.X ? -1 : 1; demo.spriteDirection = demo.direction;
		}

		/// <summary>The nearest hostile within <paramref name="reach"/> of <paramref name="from"/> (line of sight not required: a lobbed charge arcs over cover). Null when none is in range.</summary>
		private static NPC NearestInReach(Vector2 from, float reach)
		{
			NPC best = null; float bestD = reach;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !LevyUtil.IsHostileTarget(n)) continue;
				float d = Vector2.Distance(from, n.Center);
				if (d < bestD) { bestD = d; best = n; }
			}
			return best;
		}
	}
}
