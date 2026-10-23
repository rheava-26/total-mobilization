using System;
using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE COLORIST. The Painter is a FRONTLINE officer that marches with the line and looses a colorful, hard-hitting paint barrage at any enemy in
	/// reach, and whose kit is an <see cref="AuraKit"/> that inspires every friendly unit nearby to strike a little harder.
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
			// The paint barrage: vivid paintballs on a short cooldown, thrown hard.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.PainterPaintball,
				Speed = PainterPaintballSpeed, GravityCorrection = PainterPaintballGravityCorrection,
				Damage = PainterPaintballDamage, Knockback = PainterPaintballKnockback,
				Delay = 1, Cooldown = PainterPaintballCooldown, RandomCooldown = PainterPaintballRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Painter]),
				RangeTiles = PainterPaintballRangeTiles,
			},
			Kit = new PainterKit(),
		};
	}

	/// <summary>
	/// The Painter's ability: a buff aura that keeps soldiers AND fellow officers within <see cref="LevyConstants.Officer.PainterAuraRadiusTiles"/>
	/// tiles inspired (Wrath), refreshed every <see cref="LevyConstants.Officer.PainterAuraCadenceTicks"/> ticks (held steady, never stacked).
	/// </summary>
	public sealed class PainterKit : AuraKit
	{
		private static readonly int[] _buffs = { BuffID.Wrath };

		protected override int[] BuffTypes => _buffs;
		protected override int RadiusTiles => PainterAuraRadiusTiles;
		protected override int DurationTicks => PainterAuraDurationTicks;
		protected override int CadenceTicks => PainterAuraCadenceTicks;
		protected override bool IncludeOfficers => true;

		// {0} the aura reach in tiles
		public override object[] DescriptionArgs => new object[] { PainterAuraRadiusTiles };
	}
}
