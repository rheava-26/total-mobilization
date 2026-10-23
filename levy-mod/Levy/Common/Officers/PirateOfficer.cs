using Terraria.ID;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE PROOF of the summon infrastructure. The Pirate Captain is a FRONTLINE officer whose kit conscripts a boarding crew: up to
	/// <see cref="LevyConstants.Summon.PirateCrewSize"/> <see cref="SummonedUnitNPC"/> flintlock gunners (real NPCs with healthbars)
	/// that march with the army, fire on enemies in reach, scale their damage with boss progress, and are replaced on a cadence when
	/// the crew thins. To add a summon officer: subclass <see cref="SummonerKit"/> with a <see cref="SummonKind"/> and register a
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
			Weapon = null,                          // the crew does the fighting; the captain directs it
			Kit = new PirateKit(),
		};
	}

	/// <summary>The Pirate Captain's kit: a flintlock boarding crew managed by <see cref="SummonerKit"/>.</summary>
	public sealed class PirateKit : SummonerKit
	{
		protected override SummonKind Kind => SummonKinds.PirateCrew;
		protected override int SquadCap => PirateCrewSize;
		protected override int RespawnCadenceTicks => PirateRespawnCadenceTicks;

		// {0} crew size, {1} each unit's health, {2} seconds to replace a lost crewman
		public override object[] DescriptionArgs => new object[] { PirateCrewSize, PirateUnitHp, PirateRespawnCadenceTicks / 60 };
	}
}
