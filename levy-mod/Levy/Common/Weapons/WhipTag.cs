using System;
using Terraria;
using Terraria.ID;
using Levy.Common.Globals;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// The whip tag, as the game has it. A whip puts a tag debuff on the enemy it hits (BuffID.Sets.IsATagBuff: vanilla StatusNPC for the vanilla whips, the modded whip's own hit for a modded one), and every minion,
	/// sentry and minion shot that then hits the enemy deals the debuff's bonus damage. That bonus is applied in <c>Projectile.Damage</c>, behind the same gate as every term that comes from the owner
	/// (<c>!npcProj</c>), so a levy's minion, whose shots are NPC-style projectiles, never gets it from the game. <see cref="Apply"/> gives it the game's own numbers from the same debuffs (read out of
	/// Projectile.Damage): a flat bonus per debuff times the shot's <c>SummonTagDamageMultiplier</c>, the Morning Star's and Kaleidoscope's crit chance, the Firecracker's scaling bonus (and its debuff ending on the hit).
	/// The commander's own minions need none of this: they are ordinary projectiles and the game applies the tag to them.
	/// <para/>With the real whip path off (LEVY_WHIPREAL=0) the old rule stands: an enemy a levy's hand-made lash marked takes +<see cref="LevyConstants.TagMinionDamageBonus"/> from a levy's minion.
	/// </summary>
	public static class WhipTag
	{
		/// <summary>Flat bonus damage per tag debuff (Projectile.Damage's own table).</summary>
		private static readonly (int buff, int flat)[] Table =
		{
			(BuffID.BlandWhipEnemyDebuff, 4), (BuffID.SwordWhipNPCDebuff, 9), (BuffID.ScytheWhipEnemyDebuff, 10), (BuffID.ThornWhipNPCDebuff, 6),
			(BuffID.RainbowWhipNPCDebuff, 20), (BuffID.MaceWhipNPCDebuff, 8), (BuffID.BoneWhipNPCDebuff, 7), (BuffID.CoolWhipNPCDebuff, 6),
		};

		/// <summary>True when the enemy carries a tag debuff of a whip (any in BuffID.Sets.IsATagBuff, vanilla or modded).</summary>
		public static bool HasTag(NPC n)
		{
			if (n == null || !n.active) return false;
			for (int i = 0; i < n.buffType.Length; i++)
			{
				int t = n.buffType[i];
				if (t > 0 && n.buffTime[i] > 0 && t < BuffID.Sets.IsATagBuff.Length && BuffID.Sets.IsATagBuff[t]) return true;
			}
			return false;
		}

		/// <summary>True when the enemy carries a tag debuff that is not one of the vanilla whips' (a modded whip's).</summary>
		public static bool ModdedTag(NPC n)
		{
			for (int i = 0; i < n.buffType.Length; i++)
			{
				int t = n.buffType[i];
				if (t >= BuffID.Count && n.buffTime[i] > 0 && t < BuffID.Sets.IsATagBuff.Length && BuffID.Sets.IsATagBuff[t]) return true;
			}
			return false;
		}

		/// <summary>The shot's tag damage scale (Projectile.Damage reads the same set; 1 for most).</summary>
		private static float Scale(int projType) => projType > 0 && projType < ProjectileID.Sets.SummonTagDamageMultiplier.Length ? ProjectileID.Sets.SummonTagDamageMultiplier[projType] : 1f;

		/// <summary>The flat tag damage an enemy's vanilla tag debuffs add to a minion shot of this type (0 when it has none).</summary>
		public static int Flat(NPC n, int projType)
		{
			int flat = 0;
			for (int i = 0; i < n.buffType.Length; i++)
			{
				if (n.buffTime[i] < 1) continue;
				int t = n.buffType[i];
				for (int k = 0; k < Table.Length; k++) if (Table[k].buff == t) { flat += Table[k].flat; break; }
			}
			return (int)(flat * Scale(projType));
		}

		/// <summary>
		/// A levy's minion (or its shot, or a stand-in minion) hits <paramref name="target"/>: the tag's bonus, by the game's numbers when the whip was a real one, by the old flat rule otherwise.
		/// </summary>
		public static void Apply(Projectile p, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (RealWhips.Disabled)
			{
				if (LevyNpcData.IsNpcTagged(target)) modifiers.SourceDamage += LevyConstants.TagMinionDamageBonus;
				return;
			}
			bool authority = Util.LevyUtil.IsServerOrSingle;
			bool mace = false, rainbow = false, flame = false;
			for (int i = 0; i < target.buffType.Length; i++)
			{
				if (target.buffTime[i] < 1) continue;
				int t = target.buffType[i];
				if (t == BuffID.MaceWhipNPCDebuff) mace = true; else if (t == BuffID.RainbowWhipNPCDebuff) rainbow = true; else if (t == BuffID.FlameWhipEnemyDebuff) flame = true;
			}
			int flat = Flat(target, p.type);
			if (flat > 0) modifiers.FlatBonusDamage += flat;
			else if (ModdedTag(target)) modifiers.SourceDamage += LevyConstants.TagMinionDamageBonus; // a modded whip's tag: its numbers live in the mod's own hook, which does not reach an NPC-style shot (measured on Thrombosis)
			if ((mace && Main.rand.Next(100) < 12) || (rainbow && Main.rand.Next(10) == 0)) modifiers.SetCrit();
			if (flame)
			{
				modifiers.ScalingBonusDamage += 1.75f * Scale(p.type);
				if (authority) target.RequestBuffRemoval(BuffID.FlameWhipEnemyDebuff); // the Firecracker's tag is spent by the hit that cashes it
			}
		}

		/// <summary>Damage of a stand-in minion's own shot after the tag (the shot is spawned with its damage fixed, so the bonus is added here).</summary>
		public static int ShotDamage(NPC target, int damage, int projType)
		{
			if (RealWhips.Disabled) return LevyNpcData.IsNpcTagged(target) ? Math.Max(1, (int)Math.Round(damage * (1f + LevyConstants.TagMinionDamageBonus))) : damage;
			return damage + Flat(target, projType);
		}
	}
}
