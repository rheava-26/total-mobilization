using System;
using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE AURA PROOF. The Angler is a SUPPORT officer: he hangs behind the line, feeds the army, and flings frost daggerfish only at an enemy that
	/// strays into reach (never a reason to advance). His kit is a <see cref="AuraKit"/> that keeps every friendly unit in reach Well Fed.
	/// </summary>
	public static class AnglerOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Angler,
			Key = "Angler",
			CostMult = AnglerCostMult,
			Aggro = AnglerAggro,
			Posture = OfficerPosture.Support,            // hangs behind the line, feeds the aura, keeps clear of enemies
			// Frost Daggerfish (projectile 520, inflicts Frostburn), thrown fast enough to actually reach. WindUp/Delay are inert in the officer path but mirror the Nurse for consistency.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.FrostDaggerfish,
				Speed = AnglerFishSpeed, GravityCorrection = AnglerFishGravityCorrection,
				Damage = AnglerFishDamage, Knockback = AnglerFishKnockback,
				Delay = 1, Cooldown = AnglerFishCooldown, RandomCooldown = AnglerFishRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Angler]),
				RangeTiles = AnglerFishRangeTiles,
			},
			Kit = new AnglerKit(),
		};
	}

	/// <summary>
	/// The Angler's ability: a buff aura that keeps soldiers AND fellow officers within <see cref="LevyConstants.Officer.AnglerAuraRadiusTiles"/> tiles
	/// Well Fed, refreshed every <see cref="LevyConstants.Officer.AnglerAuraCadenceTicks"/> ticks (held steady, never stacked).
	/// </summary>
	public sealed class AnglerKit : AuraKit
	{
		private static readonly int[] _buffs = { BuffID.WellFed };

		protected override int[] BuffTypes => _buffs;
		protected override int RadiusTiles => AnglerAuraRadiusTiles;
		protected override int DurationTicks => AnglerAuraDurationTicks;
		protected override int CadenceTicks => AnglerAuraCadenceTicks;
		protected override bool IncludeOfficers => true;

		// {0} the aura reach in tiles
		public override object[] DescriptionArgs => new object[] { AnglerAuraRadiusTiles };
	}
}
