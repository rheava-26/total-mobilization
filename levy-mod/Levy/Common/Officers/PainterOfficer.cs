using System;
using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE COLORIST. The Painter is a FRONTLINE officer that marches with the line and looses a colorful, hard-hitting paint barrage at any enemy in reach.
	/// That is the whole officer: his paint gun is his <see cref="OfficerWeapon"/>, and he has no kit.
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
			Kit = null,                                  // a fighter with a gun and nothing else: the officer runtime and the menu both treat a null kit as "fights only"
		};
	}
}
