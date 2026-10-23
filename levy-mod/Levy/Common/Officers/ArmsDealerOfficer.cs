using System;
using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE GUNSLINGER. The Arms Dealer is a FRONTLINE officer that marches with the line and lays down a rapid stream of rifle fire, and whose kit
	/// is an <see cref="AuraKit"/> keeping every friendly unit in reach under Archery — their ranged attacks hit harder and fire faster.
	/// </summary>
	public static class ArmsDealerOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.ArmsDealer,
			Key = "ArmsDealer",
			CostMult = ArmsDealerCostMult,
			Aggro = ArmsDealerAggro,
			Posture = OfficerPosture.Frontline,          // marches with the army and fights what comes in reach
			// The rifle: fast, light bullets on a short cooldown, so the stream reads as rapid fire.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.Bullet,
				Speed = ArmsDealerBulletSpeed, GravityCorrection = ArmsDealerBulletGravityCorrection,
				Damage = ArmsDealerBulletDamage, Knockback = ArmsDealerBulletKnockback,
				Delay = 1, Cooldown = ArmsDealerBulletCooldown, RandomCooldown = ArmsDealerBulletRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.ArmsDealer]),
				RangeTiles = ArmsDealerBulletRangeTiles,
			},
			Kit = new ArmsDealerKit(),
		};
	}

	/// <summary>
	/// The Arms Dealer's ability: a buff aura that keeps soldiers AND fellow officers within <see cref="LevyConstants.Officer.ArmsDealerAuraRadiusTiles"/>
	/// tiles under Archery, refreshed every <see cref="LevyConstants.Officer.ArmsDealerAuraCadenceTicks"/> ticks (held steady, never stacked).
	/// </summary>
	public sealed class ArmsDealerKit : AuraKit
	{
		private static readonly int[] _buffs = { BuffID.Archery };

		protected override int[] BuffTypes => _buffs;
		protected override int RadiusTiles => ArmsDealerAuraRadiusTiles;
		protected override int DurationTicks => ArmsDealerAuraDurationTicks;
		protected override int CadenceTicks => ArmsDealerAuraCadenceTicks;
		protected override bool IncludeOfficers => true;

		// {0} the aura reach in tiles
		public override object[] DescriptionArgs => new object[] { ArmsDealerAuraRadiusTiles };
	}
}
