using System;
using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE VENOMANCER. The Witch Doctor is a SUPPORT officer whose signature is VENOM: while he is on duty, a share of every hit the army lands within
	/// <see cref="LevyConstants.Summon.WitchDoctorVenomRadiusTiles"/> tiles of him leaves its target envenomed, a damage-over-time that ignores defense (<see cref="Levy.Common.Venom.VenomSystem"/>).
	/// The venom needs no kit work: it is read off the officer's presence at the moment of each hit. His kit only fields <see cref="LevyConstants.Summon.WitchDoctorSpiritCount"/> voodoo spirits,
	/// flying <see cref="SummonedUnitNPC"/>s that spit poison darts, replaced on a cadence. He fires a blowgun of his own.
	/// </summary>
	public static class WitchDoctorOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.WitchDoctor,
			Key = "WitchDoctor",
			CostMult = WitchDoctorCostMult,
			Aggro = WitchDoctorAggro,
			Posture = OfficerPosture.Support,   // works his venom from behind the line
			// His blowgun and the poison darts he carries: the item's real shot at the blowgun's real cadence.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.Blowgun, Ammo = ItemID.PoisonDart },
			// The fallback poison dart, and the reach and damage floor the blowgun fights with.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.PoisonDartBlowgun,
				Speed = WitchDoctorDartSpeed, GravityCorrection = WitchDoctorDartGravityCorrection, Damage = WitchDoctorDartDamage, Knockback = WitchDoctorDartKnockback,
				Delay = 1, Cooldown = WitchDoctorDartCooldown, RandomCooldown = WitchDoctorDartRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.WitchDoctor]),
				RangeTiles = WitchDoctorDartRangeTiles,
			},
			Kit = new WitchDoctorKit(),
		};
	}

	/// <summary>The Witch Doctor's kit: a pair of voodoo spirits, managed by <see cref="SummonerKit"/>. The venom itself lives in <see cref="Levy.Common.Venom.VenomSystem"/>.</summary>
	public sealed class WitchDoctorKit : SummonerKit
	{
		protected override SummonKind Kind => SummonKinds.WitchDoctorSpirit;
		protected override int SquadCap => WitchDoctorSpiritCount;
		protected override int RespawnCadenceTicks => WitchDoctorRespawnCadenceTicks;

		// {0} reach of the venom (tiles), {1} chance a hit envenoms (percent), {2} seconds it lasts, {3} damage per second at the start,
		// {4} spirits fielded, {5} each spirit's health, {6} seconds to call a replacement
		public override object[] DescriptionArgs => new object[]
		{
			WitchDoctorVenomRadiusTiles, (int)Math.Round(WitchDoctorVenomChance * 100f), WitchDoctorVenomDurationTicks / 60, WitchDoctorVenomDps,
			WitchDoctorSpiritCount, WitchDoctorSpiritHp, WitchDoctorRespawnCadenceTicks / 60,
		};
	}
}
