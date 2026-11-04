using System;
using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE NECROMANCER'S TAILOR. The Clothier is a SUPPORT officer who raises a squad of skeleton warriors: <see cref="LevyConstants.Summon.ClothierSkeletonCount"/> melee
	/// <see cref="SummonedUnitNPC"/>s that march with the army, run down the nearest enemy beside the line and cut it on contact. A fallen skeleton is raised again on a cadence,
	/// so the squad is rebuilt on the fly mid-fight, not only on death. He throws his cursed skull at what strays into reach.
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

	/// <summary>The Clothier's kit: one squad of skeleton warriors, managed by <see cref="SummonerKit"/>.</summary>
	public sealed class ClothierKit : SummonerKit
	{
		protected override SummonKind Kind => SummonKinds.ClothierSkeleton;
		protected override int SquadCap => ClothierSkeletonCount;
		protected override int RespawnCadenceTicks => ClothierRespawnCadenceTicks;

		// {0} squad size, {1} each skeleton's health, {2} seconds to raise a replacement
		public override object[] DescriptionArgs => new object[] { ClothierSkeletonCount, ClothierSkeletonHp, ClothierRespawnCadenceTicks / 60 };
	}
}
