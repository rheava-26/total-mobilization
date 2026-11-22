using System;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Harness-only switches for before/after runs of the one-class-per-levy fixes (Eve's 11-22 report: a ranged primary, a melee sidearm, ranged armor, a rogue and a summoner accessory). Every one is on in play;
	/// env LEVY_LOADOUTFIX_OFF lists the letters to turn off, so a lab run can show each cause on its own (see LoadoutFitWorldCheck).
	///  s  the sidearm's class is the primary's, and a class balance is no "rule asked for it" (SidearmClass)
	///  o  no sidearm of another class after the Moon Lord (SidearmClass.OffClassAllowed)
	///  m  a classed primary decides whether a levy is a summoner (ArmoryTE.IsSummonerSoldier)
	///  p  the army plan reads a levy by his primary when his armor is another class's (ArmoryTE.PlanClass)
	///  f  a levy whose primary the plan moved to another class has his armor, accessories and sidearm follow at once (ArmoryTE.FollowPrimary)
	///  a  class accessories: left for their class at issue, given back by another class (ArmoryTE.GiveBackClassAccessories)
	///  x  class armor: kept for its class at issue, traded between levies (ArmoryTE.ExchangeClassArmor)
	///  d  a class floor only takes levies from classes above their own floor (WeaponAllocator.AssignClasses; also env LEVY_ALLOC_LEGACYFLOOR=1)
	/// </summary>
	internal static class LoadoutFixes
	{
		private static readonly string Off = Environment.GetEnvironmentVariable("LEVY_LOADOUTFIX_OFF") ?? "";
		internal static bool Sidearm = !Off.Contains('s'), PostMoonLord = !Off.Contains('o'), Summoner = !Off.Contains('m'), PlanClass = !Off.Contains('p'),
			Follow = !Off.Contains('f'), Accessories = !Off.Contains('a'), Armor = !Off.Contains('x'), FloorDonors = !Off.Contains('d');
	}
}
