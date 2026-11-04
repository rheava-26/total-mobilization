using System;
using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE PROOF of the summon infrastructure. The Pirate Captain is a FRONTLINE officer whose kit conscripts a boarding crew and a cannon: up to
	/// <see cref="LevyConstants.Summon.PirateCrewSize"/> <see cref="SummonedUnitNPC"/> flintlock gunners that march with the army and
	/// <see cref="LevyConstants.Summon.PirateCannonCount"/> cannon emplacement that is redeployed beside him as the army advances (all real NPCs with healthbars),
	/// which fire on enemies in reach, scale their damage with boss progress, and are replaced on a cadence when lost. The captain also fires his own flintlock pistol.
	/// To add a summon officer: subclass <see cref="SummonerKit"/> with a <see cref="SummonKind"/> (or several <see cref="SummonSquad"/>s) and register a
	/// <c>Create()</c> like this in <see cref="OfficerRegistry.RegisterAll"/>.
	/// </summary>
	public static class PirateOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Pirate,
			Key = "Pirate",
			CostMult = PirateCostMult,
			Aggro = PirateOfficerAggro,
			Posture = OfficerPosture.Frontline,   // the captain marches at the front with his crew
			MarchTrigger = DialogueTrigger.OFFICER_MARCH,
			// His flintlock pistol and the musket balls he carries: the item's real shot at the pistol's real cadence, in support of the crew.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.FlintlockPistol, Ammo = ItemID.MusketBall },
			// The fallback ball, and the reach and damage floor the pistol fights with.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.Bullet,
				Speed = PirateCaptainShotSpeed, GravityCorrection = PirateCaptainShotGravityCorrection, Damage = PirateCaptainShotDamage, Knockback = PirateCaptainShotKnockback,
				Delay = 1, Cooldown = PirateCaptainShotCooldown, RandomCooldown = PirateCaptainShotRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Pirate]),
				RangeTiles = PirateCaptainRangeTiles,
			},
			Kit = new PirateKit(),
		};
	}

	/// <summary>The Pirate Captain's kit: a flintlock boarding crew and a cannon, two squads managed by <see cref="SummonerKit"/>.</summary>
	public sealed class PirateKit : SummonerKit
	{
		// Crew first, then the cannon: the gunners are stood up before the emplacement is deployed.
		private static readonly SummonSquad[] _squads =
		{
			new SummonSquad(SummonKinds.PirateCrew, PirateCrewSize, PirateRespawnCadenceTicks),
			new SummonSquad(SummonKinds.PirateCannon, PirateCannonCount, PirateCannonRespawnCadenceTicks),
		};

		protected override SummonSquad[] Squads => _squads;

		// {0} crew size, {1} each gunner's health, {2} seconds to replace a lost gunner, {3} cannon, {4} the cannon's health, {5} seconds to replace a lost cannon
		public override object[] DescriptionArgs => new object[]
		{
			PirateCrewSize, PirateUnitHp, PirateRespawnCadenceTicks / 60,
			PirateCannonCount, PirateCannonHp, PirateCannonRespawnCadenceTicks / 60,
		};
	}
}
