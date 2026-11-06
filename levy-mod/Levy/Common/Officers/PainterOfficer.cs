using System;
using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE COLORIST. The Painter is a FRONTLINE officer that marches with the line and looses a colorful, hard-hitting paint barrage at any enemy in reach.
	/// His paint gun is his weapon, and what it paints is his kit: a paint-soaked enemy has less defense and deals less damage.
	/// </summary>
	public static class PainterOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Painter,
			Key = "Painter",
			CostMult = PainterCostMult,
			Aggro = PainterAggro,
			Posture = OfficerPosture.Frontline,          // marches with the army and fights what comes in reach
			// His paintball gun: the item's real paintballs at the gun's real cadence.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.PainterPaintballGun },
			// The fallback paint barrage (vivid paintballs on a short cooldown, thrown hard), and the reach and damage floor the paintball gun fights with.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.PainterPaintball,
				Speed = PainterPaintballSpeed, GravityCorrection = PainterPaintballGravityCorrection,
				Damage = PainterPaintballDamage, Knockback = PainterPaintballKnockback,
				Delay = 1, Cooldown = PainterPaintballCooldown, RandomCooldown = PainterPaintballRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Painter]),
				RangeTiles = PainterPaintballRangeTiles,
			},
			Kit = new PainterKit(),                      // his paint: a hit from the gun soaks the enemy (PaintGlobalNPC)
		};
	}
}

namespace Levy.Common.Officers
{
	/// <summary>The Painter's ability: his paintball gun's hits cover enemies in paint (<see cref="Levy.Content.Buffs.PaintedBuff"/>). The painting happens on the hit (<see cref="PaintProjectile"/>); nothing runs per tick.</summary>
	public sealed class PainterKit : OfficerKit
	{
		// {0} defense lost, {1} damage dealt less (percent)
		public override object[] DescriptionArgs => new object[] { (int)LevyConstants.Officer.PaintDefenseLoss, (int)System.Math.Round(LevyConstants.Officer.PaintDamageCut * 100f) };
		public override void Tick(OfficerContext ctx) { }
	}
}
