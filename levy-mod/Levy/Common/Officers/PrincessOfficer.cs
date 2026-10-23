using Terraria.ID;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE CAPSTONE. The Princess is a SUPPORT officer who never fights: she hangs behind the line and lifts the whole army at once. Her kit is an
	/// <see cref="AuraKit"/> that keeps every soldier AND fellow officer in reach under four morale buffs together — Ironskin (defense), Wrath (damage),
	/// Rage (crit) and Endurance (damage reduction). The strongest force-multiplier in the roster, which is why she is the priciest commission.
	/// </summary>
	public static class PrincessOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Princess,
			Key = "Princess",
			CostMult = PrincessCostMult,
			Aggro = PrincessAggro,
			Posture = OfficerPosture.Support,   // never fights; hangs back and buffs
			Weapon = null,                       // the army protects her
			Kit = new PrincessKit(),
		};
	}

	/// <summary>
	/// The Princess's ability: an all-army morale aura. Soldiers and officers within <see cref="LevyConstants.Officer.PrincessAuraRadiusTiles"/> tiles are
	/// kept Ironskin, Wrath, Rage and Endurance together, refreshed every pulse (held steady, never stacked). Every buff here is already folded onto the
	/// soldier dummy by <see cref="Levy.Content.NPCs.SoldierNPC"/>, so the aura changes real stats.
	/// </summary>
	public sealed class PrincessKit : AuraKit
	{
		private static readonly int[] _buffs = { BuffID.Ironskin, BuffID.Wrath, BuffID.Rage, BuffID.Endurance };

		protected override int[] BuffTypes => _buffs;
		protected override int RadiusTiles => PrincessAuraRadiusTiles;
		protected override int DurationTicks => PrincessAuraDurationTicks;
		protected override int CadenceTicks => PrincessAuraCadenceTicks;
		protected override bool IncludeOfficers => true;

		// {0} the aura reach in tiles
		public override object[] DescriptionArgs => new object[] { PrincessAuraRadiusTiles };
	}
}
