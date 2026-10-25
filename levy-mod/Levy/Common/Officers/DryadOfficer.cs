using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Mark;
using Levy.Common.Util;
using Levy.Content.Buffs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE AOE MARKER. The Dryad is a SUPPORT officer who works two effects on one cadence. DRYAD'S BANE hangs the army's SHRED mark on EVERY enemy in
	/// reach, so the army deals extra damage to all of them; and DRYAD'S BLESSING keeps the soldiers around her under one nature buff (defense, regeneration
	/// and thorns that turn a contact hit back on the attacker). The mark and its amp live in <see cref="Levy.Common.Mark.MarkSystem"/> / MarkAmpGlobalNPC; the
	/// Blessing is <see cref="DryadsBlessingBuff"/>.
	/// </summary>
	public static class DryadOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Dryad,
			Key = "Dryad",
			CostMult = DryadCostMult,
			Aggro = DryadAggro,
			Posture = OfficerPosture.Support,   // casts from behind the line, out of melee
			// Her blowpipe and the seeds she carries: she spits the item's real seed at the blowpipe's real cadence at anything that strays into reach.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.Blowpipe, Ammo = ItemID.Seed },
			// The fallback seed, and the reach and damage floor the blowpipe fights with.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.Seed,
				Speed = DryadSeedSpeed, GravityCorrection = DryadSeedGravityCorrection, Damage = DryadSeedDamage, Knockback = DryadSeedKnockback,
				Delay = 1, Cooldown = DryadSeedCooldown, RandomCooldown = DryadSeedRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Dryad]),
				RangeTiles = DryadSeedRangeTiles,
			},
			Kit = new DryadKit(),
		};
	}

	/// <summary>
	/// The Dryad's ability: every <see cref="LevyConstants.Officer.DryadCadenceTicks"/> she hangs the shred on every hostile within
	/// <see cref="LevyConstants.Officer.DryadMarkRadiusTiles"/> (AoE) and refreshes Dryad's Blessing on every soldier within
	/// <see cref="LevyConstants.Officer.DryadAuraRadiusTiles"/>. The marks are real debuffs that tick down on their own and vanish with the enemy, so there
	/// is no state to leak; the Blessing is refreshed each pulse, so it holds steady and never stacks.
	/// </summary>
	public sealed class DryadKit : OfficerKit
	{
		private int[] _blessing;
		private sealed class State { public int Timer; }

		public override object CreateState() => new State();

		// {0} mark reach (tiles), {1} army damage added to each marked enemy (percent), {2} Blessing reach (tiles), {3} defense it adds, {4} contact damage it reflects (percent)
		public override object[] DescriptionArgs => new object[]
		{
			DryadMarkRadiusTiles, (int)MarkDamagePct, DryadAuraRadiusTiles, DryadBlessingDefense, (int)(DryadThornsReflectFrac * 100f),
		};

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < DryadCadenceTicks) return;
			st.Timer = 0;
			Vector2 from = ctx.Npc.Center;

			// DRYAD'S BANE — hang the shred on every hostile in reach (refreshes those already marked, so it reads as continuous).
			float markReach = DryadMarkRadiusTiles * LevyConstants.TilePx;
			float markReachSq = markReach * markReach;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !LevyUtil.IsHostileTarget(n)) continue;
				if (Vector2.DistanceSquared(from, n.Center) > markReachSq) continue;
				MarkSystem.Mark(n, DryadMarkDurationTicks);
			}

			// DRYAD'S BLESSING — keep nearby soldiers under the one nature buff (the pulse also flags their stats for a recompute so the effect lands).
			AuraKit.Pulse(from, _blessing ??= new[] { ModContent.BuffType<DryadsBlessingBuff>() }, DryadAuraRadiusTiles, DryadAuraDurationTicks, includeOfficers: false);
		}
	}
}
