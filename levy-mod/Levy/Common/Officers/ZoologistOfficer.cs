using System;
using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE BEASTMASTER. The Zoologist is a FRONTLINE officer who marches with a creature from her Bestiary: one of the strongest the world's progress and her unlocked entries allow
	/// (<see cref="ConjureRoster.BeastCandidates"/>: no boss, miniboss, town NPC or critter, and nothing past the stage's life and damage caps). It is the real creature running its own AI against the army's
	/// enemies (<see cref="ConjuredGlobalNPC"/>). A fallen creature is replaced on a cadence. She also looses a green strike of her own.
	/// </summary>
	public static class ZoologistOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.BestiaryGirl,
			Key = "Zoologist",
			Female = true,
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

	/// <summary>The Zoologist's kit: one strong creature from her Bestiary, managed by <see cref="SummonerKit"/>.</summary>
	public sealed class ZoologistKit : SummonerKit
	{
		private static readonly SummonSquad[] _squads = { new SummonSquad(ConjureRoster.PickBeast, ZoologistBeastCount, ZoologistBeastRespawnCadenceTicks, ZoologistBeastMinLife, ZoologistBeastMinDamage) };
		protected override SummonSquad[] Squads => _squads;

		// {0} seconds to call a replacement
		public override object[] DescriptionArgs => new object[] { ZoologistBeastRespawnCadenceTicks / 60 };
	}
}
