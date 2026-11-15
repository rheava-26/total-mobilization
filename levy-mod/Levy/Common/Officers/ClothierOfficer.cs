using System;
using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE NECROMANCER'S TAILOR. The Clothier is a SUPPORT officer who raises a squad of skeletons: <see cref="LevyConstants.Summon.ClothierSkeletonCount"/> REAL skeleton enemies (a Skeleton, a Skeleton Archer, Angry Bones,
	/// Armored Skeletons, the Rusty / Blue / Hell Armored Bones, the Skeleton Commando and Sniper: <see cref="ConjureRoster.SkeletonPool"/> by how far the world has got), each running its own vanilla AI against the army's
	/// enemies (<see cref="ConjuredGlobalNPC"/>). In a fight he never stops raising them: a lost one is replaced every second (the squad size is the cap), and a skeleton that deals no damage for ten seconds of a fight crumbles, so idle ones fade and fighting ones stay. He throws his cursed skull at what strays into reach.
	/// </summary>
	public static class ClothierOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Clothier,
			Key = "Clothier",
			CostMult = ClothierCostMult,
			Aggro = ClothierAggro,
			Posture = OfficerPosture.Support,   // stays behind his skeletons
			// No real item to mount: the cursed skull he throws IS his weapon.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.ClothiersCurse,
				Speed = ClothierCurseSpeed, GravityCorrection = ClothierCurseGravityCorrection, Damage = ClothierCurseDamage, Knockback = ClothierCurseKnockback,
				Delay = 1, Cooldown = ClothierCurseCooldown, RandomCooldown = ClothierCurseRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Clothier]),
				RangeTiles = ClothierCurseRangeTiles,
			},
			Kit = new ClothierKit(),
		};
	}

	/// <summary>The Clothier's kit: one squad of tier-appropriate skeletons, managed by <see cref="SummonerKit"/>.</summary>
	public sealed class ClothierKit : SummonerKit
	{
		private static readonly SummonSquad[] _squads = { new SummonSquad(ConjureRoster.PickSkeleton, ClothierSkeletonCount, ClothierRespawnCadenceTicks, ClothierSkeletonMinLife, ClothierSkeletonMinDamage, ClothierCombatRespawnCadenceTicks, ClothierSkeletonIdleLifeTicks) };
		protected override SummonSquad[] Squads => _squads;

		// {0} squad size, {1} seconds to raise a replacement
		public override object[] DescriptionArgs => new object[] { ClothierSkeletonCount, ClothierCombatRespawnCadenceTicks / 60, ClothierSkeletonIdleLifeTicks / 60 };
	}
}
