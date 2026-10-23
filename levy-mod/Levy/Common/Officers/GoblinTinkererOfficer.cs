using System;
using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE FIELD ARMORER. The Goblin Tinkerer is a FRONTLINE officer who marches with the line: he tunes the gear of every soldier in reach — an
	/// <see cref="AuraKit"/> granting Wrath (damage), Rage (crit) and Ironskin (defense) — and flings spiky balls at anything that strays into reach.
	/// </summary>
	public static class GoblinTinkererOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.GoblinTinkerer,
			Key = "GoblinTinkerer",
			CostMult = GoblinCostMult,
			Aggro = GoblinAggro,
			Posture = OfficerPosture.Frontline,   // marches at the front and fights what comes in reach
			// A thrown spiky ball (affected by gravity, so a far shot is arced above the target); scales with world progress like a town NPC's shot.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.SpikyBall,
				Speed = GoblinBallSpeed, GravityCorrection = GoblinBallGravityCorrection,
				Damage = GoblinBallDamage, Knockback = GoblinBallKnockback,
				Delay = 1, Cooldown = GoblinBallCooldown, RandomCooldown = GoblinBallRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.GoblinTinkerer]),
				RangeTiles = GoblinBallRangeTiles,
			},
			Kit = new GoblinTinkererKit(),
		};
	}

	/// <summary>
	/// The Goblin Tinkerer's ability: a gear-tune aura. Soldiers within <see cref="LevyConstants.Officer.GoblinAuraRadiusTiles"/> tiles are kept under Wrath,
	/// Rage and Ironskin together, refreshed every pulse (held steady, never stacked). All three buffs are folded onto the soldier dummy, so the lift is real.
	/// </summary>
	public sealed class GoblinTinkererKit : AuraKit
	{
		private static readonly int[] _buffs = { BuffID.Wrath, BuffID.Rage, BuffID.Ironskin };

		protected override int[] BuffTypes => _buffs;
		protected override int RadiusTiles => GoblinAuraRadiusTiles;
		protected override int DurationTicks => GoblinAuraDurationTicks;
		protected override int CadenceTicks => GoblinAuraCadenceTicks;
		protected override bool IncludeOfficers => false;

		// {0} the aura reach in tiles
		public override object[] DescriptionArgs => new object[] { GoblinAuraRadiusTiles };
	}
}
