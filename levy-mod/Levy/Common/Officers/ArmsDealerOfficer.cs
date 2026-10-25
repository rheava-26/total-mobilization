using System;
using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE AMMO MAN. The Arms Dealer is a FRONTLINE officer that marches with the line and lays down a rapid stream of rifle fire. His kit is what a gun dealer
	/// brings to an army: the soldiers near him shoot straighter and never run dry. Both effects are passive and read shot by shot
	/// (<see cref="OfficerSupport"/>, called from <c>WeaponUse.AimErrorMultiplier</c> and <c>WeaponUse.ConsumesAmmo</c>), so there is no buff to track.
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
			// His Minishark and the bullets he sells: the item's real rapid stream, at the Minishark's real cadence.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.Minishark, Ammo = ItemID.MusketBall },
			// The fallback rifle (fast, light bullets on a short cooldown), and the reach and damage floor the Minishark fights with.
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
	/// The Arms Dealer's ability: every soldier within <see cref="ArmsDealerSupplyRadiusTiles"/> tiles has his aim error multiplied by <see cref="ArmsDealerAimErrorMult"/>
	/// (tighter spread, truer lead) and spends no ammo. There is nothing to run on a tick: the weapon code asks <see cref="AimSupport"/> and <see cref="BottomlessAmmo"/>
	/// whenever a soldier fires.
	/// </summary>
	public sealed class ArmsDealerKit : OfficerKit
	{
		// {0} reach in tiles, {1} how much less aim error a soldier in reach has (percent)
		public override object[] DescriptionArgs => new object[] { ArmsDealerSupplyRadiusTiles, (int)Math.Round((1f - ArmsDealerAimErrorMult) * 100f) };

		public override bool AimSupport(out int radiusTiles, out float errorMult) { radiusTiles = ArmsDealerSupplyRadiusTiles; errorMult = ArmsDealerAimErrorMult; return true; }

		public override bool BottomlessAmmo(out int radiusTiles) { radiusTiles = ArmsDealerSupplyRadiusTiles; return true; }

		public override void Tick(OfficerContext ctx) { }
	}
}
