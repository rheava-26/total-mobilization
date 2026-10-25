using System;
using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE SWORDMASTER OF THE ARCANE. The Wizard is a SUPPORT officer who enchants a flight of swords: <see cref="LevyConstants.Summon.WizardSwordCount"/> flying melee
	/// <see cref="SummonedUnitNPC"/>s that hover over the army, dart at the nearest enemy and slash it on contact. A shattered sword is replaced on a cadence. He casts a
	/// magic bolt of his own from a diamond staff.
	/// </summary>
	public static class WizardOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Wizard,
			Key = "Wizard",
			CostMult = WizardCostMult,
			Aggro = WizardAggro,
			Posture = OfficerPosture.Support,   // casts from behind the line
			// His staff: he looses the item's real bolt at the staff's real cadence at anything that strays into reach.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.DiamondStaff },
			// The fallback fireball, and the reach and damage floor the staff fights with.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.BallofFire,
				Speed = WizardBoltSpeed, GravityCorrection = WizardBoltGravityCorrection, Damage = WizardBoltDamage, Knockback = WizardBoltKnockback,
				Delay = 1, Cooldown = WizardBoltCooldown, RandomCooldown = WizardBoltRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Wizard]),
				RangeTiles = WizardBoltRangeTiles,
			},
			Kit = new WizardKit(),
		};
	}

	/// <summary>The Wizard's kit: one flight of enchanted swords, managed by <see cref="SummonerKit"/>.</summary>
	public sealed class WizardKit : SummonerKit
	{
		protected override SummonKind Kind => SummonKinds.WizardSword;
		protected override int SquadCap => WizardSwordCount;
		protected override int RespawnCadenceTicks => WizardRespawnCadenceTicks;

		// {0} swords fielded, {1} each sword's health, {2} seconds to enchant a replacement
		public override object[] DescriptionArgs => new object[] { WizardSwordCount, WizardSwordHp, WizardRespawnCadenceTicks / 60 };
	}
}
