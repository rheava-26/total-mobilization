using System;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.Buffs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE GUIDE. A FRONTLINE officer that marches with the line and steadies the soldiers around him: they shoot straighter, and they fight with Morale. He also
	/// pokes with a guiding bolt at anything that strays into reach. The accuracy is passive and read shot by shot (<see cref="OfficerSupport"/>); the morale is an
	/// <see cref="AuraKit"/> keeping the single <see cref="MoraleBuff"/> on everyone in reach.
	/// </summary>
	/// <remarks>DEFERRED (net-new, not built): the synchronized volley, where the Guide calls a moment and the soldiers around him fire together.</remarks>
	public static class GuideOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Guide,
			Key = "Guide",
			CostMult = GuideCostMult,
			Aggro = GuideAggro,
			Posture = OfficerPosture.Frontline,          // marches with the army and fights what comes in reach
			// The guiding bolt: a bright, straight shard thrown at whatever strays into reach.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.SapphireBolt,
				Speed = GuideBoltSpeed, GravityCorrection = GuideBoltGravityCorrection, Damage = GuideBoltDamage, Knockback = GuideBoltKnockback,
				Delay = 1, Cooldown = GuideBoltCooldown, RandomCooldown = GuideBoltRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Guide]),
				RangeTiles = GuideBoltRangeTiles,
			},
			Kit = new GuideKit(),
		};
	}

	/// <summary>
	/// The Guide's ability: every soldier within <see cref="GuideAuraRadiusTiles"/> tiles has his aim error multiplied by <see cref="GuideAimErrorMult"/> (the accuracy,
	/// via <see cref="AimSupport"/>) and is kept under Morale (the aura, refreshed every pulse and never stacked). Soldiers and fellow officers both feel the morale.
	/// </summary>
	public sealed class GuideKit : AuraKit
	{
		private int[] _buffs;

		protected override int[] BuffTypes => _buffs ??= new[] { ModContent.BuffType<MoraleBuff>() };
		protected override int RadiusTiles => GuideAuraRadiusTiles;
		protected override int DurationTicks => GuideAuraDurationTicks;
		protected override int CadenceTicks => GuideAuraCadenceTicks;
		protected override bool IncludeOfficers => true;

		public override bool AimSupport(out int radiusTiles, out float errorMult) { radiusTiles = GuideAuraRadiusTiles; errorMult = GuideAimErrorMult; return true; }

		// {0} reach in tiles, {1} how much less aim error a soldier in reach has (percent), {2} damage added (percent), {3} defense added, {4} crit chance added (points)
		public override object[] DescriptionArgs => new object[]
		{
			GuideAuraRadiusTiles, (int)Math.Round((1f - GuideAimErrorMult) * 100f), (int)Math.Round(MoraleDamage * 100f), MoraleDefense, (int)Math.Round(MoraleCrit),
		};
	}
}
