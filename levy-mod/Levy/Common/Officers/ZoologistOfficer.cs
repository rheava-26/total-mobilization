using System;
using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE BEASTMASTER. The Zoologist is a FRONTLINE officer who marches with a pack: <see cref="LevyConstants.Summon.ZoologistWolfCount"/> fast wolves and
	/// <see cref="LevyConstants.Summon.ZoologistWerewolfCount"/> sturdy werewolf, all melee <see cref="SummonedUnitNPC"/>s that run down the nearest enemy beside the line and maul it.
	/// The werewolf draws the enemy's attention. A lost beast is replaced on a cadence (the werewolf slowly). She also looses a green strike of her own.
	/// </summary>
	public static class ZoologistOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.BestiaryGirl,
			Key = "Zoologist",
			CostMult = ZoologistCostMult,
			Aggro = ZoologistAggro,
			Posture = OfficerPosture.Frontline,   // marches with her pack
			// No real item to mount: the strike she calls IS her weapon.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.ZoologistStrikeGreen,
				Speed = ZoologistStrikeSpeed, GravityCorrection = ZoologistStrikeGravityCorrection, Damage = ZoologistStrikeDamage, Knockback = ZoologistStrikeKnockback,
				Delay = 1, Cooldown = ZoologistStrikeCooldown, RandomCooldown = ZoologistStrikeRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.BestiaryGirl]),
				RangeTiles = ZoologistStrikeRangeTiles,
			},
			Kit = new ZoologistKit(),
		};
	}

	/// <summary>The Zoologist's kit: a wolf pack and a werewolf, two squads managed by <see cref="SummonerKit"/>.</summary>
	public sealed class ZoologistKit : SummonerKit
	{
		// Wolves first, then the werewolf: the pack runs out ahead of the brute.
		private static readonly SummonSquad[] _squads =
		{
			new SummonSquad(SummonKinds.ZoologistWolf, ZoologistWolfCount, ZoologistWolfRespawnCadenceTicks),
			new SummonSquad(SummonKinds.ZoologistWerewolf, ZoologistWerewolfCount, ZoologistWerewolfRespawnCadenceTicks),
		};

		protected override SummonSquad[] Squads => _squads;

		// {0} wolves, {1} each wolf's health, {2} seconds to replace a wolf, {3} werewolves, {4} the werewolf's health, {5} seconds to replace a werewolf
		public override object[] DescriptionArgs => new object[]
		{
			ZoologistWolfCount, ZoologistWolfHp, ZoologistWolfRespawnCadenceTicks / 60,
			ZoologistWerewolfCount, ZoologistWerewolfHp, ZoologistWerewolfRespawnCadenceTicks / 60,
		};
	}
}
