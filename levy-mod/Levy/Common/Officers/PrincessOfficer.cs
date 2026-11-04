using System;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.Buffs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE CAPSTONE. The Princess is a SUPPORT officer who hangs behind the line and lifts the whole army's morale at once, striking with her wand only what strays into reach. Her kit is an
	/// <see cref="AuraKit"/> that keeps every soldier AND fellow officer in reach under the single <see cref="MoraleBuff"/> (damage, defense and crit under one icon).
	/// The widest morale reach in the roster, which is why she is the priciest commission.
	/// </summary>
	public static class PrincessOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Princess,
			Key = "Princess",
			CostMult = PrincessCostMult,
			Aggro = PrincessAggro,
			Posture = OfficerPosture.Support,   // hangs back and buffs; she only strikes what strays into reach
			// Her wand: she looses the item's real spark at the wand's real cadence at anything that strays into reach.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.WandofSparking },
			// The fallback spark, and the reach and damage floor the wand fights with.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.WandOfSparkingSpark,
				Speed = PrincessWandSpeed, GravityCorrection = PrincessWandGravityCorrection, Damage = PrincessWandDamage, Knockback = PrincessWandKnockback,
				Delay = 1, Cooldown = PrincessWandCooldown, RandomCooldown = PrincessWandRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Princess]),
				RangeTiles = PrincessWandRangeTiles,
			},
			Kit = new PrincessKit(),
		};
	}

	/// <summary>
	/// The Princess's ability: an all-army morale aura. Soldiers and officers within <see cref="LevyConstants.Officer.PrincessAuraRadiusTiles"/> tiles are kept under
	/// Morale, refreshed every pulse (held steady, never stacked). Morale is folded onto the soldier dummy by <see cref="Levy.Content.NPCs.SoldierNPC"/>, so the aura
	/// changes real stats.
	/// </summary>
	public sealed class PrincessKit : AuraKit
	{
		private int[] _buffs;

		protected override int[] BuffTypes => _buffs ??= new[] { ModContent.BuffType<MoraleBuff>() };
		protected override int RadiusTiles => PrincessAuraRadiusTiles;
		protected override int DurationTicks => PrincessAuraDurationTicks;
		protected override int CadenceTicks => PrincessAuraCadenceTicks;
		protected override bool IncludeOfficers => true;

		// {0} the aura reach in tiles, {1} damage added (percent), {2} defense added, {3} crit chance added (points)
		public override object[] DescriptionArgs => new object[] { PrincessAuraRadiusTiles, (int)Math.Round(MoraleDamage * 100f), MoraleDefense, (int)Math.Round(MoraleCrit) };
	}
}
