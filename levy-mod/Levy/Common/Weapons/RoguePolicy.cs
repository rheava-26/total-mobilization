using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>How a Calamity rogue levy uses one weapon's stealth.</summary>
	internal enum RogueMode
	{
		/// <summary>Hold the throw until the stealth bar is full and throw the strike (its damage per second beats the rate of plain throws, which each empty the bar).</summary>
		Wait,
		/// <summary>Throw at the weapon's own rate; the strike is not worth the two seconds of waiting.</summary>
		Spam,
		/// <summary>Strike only when it pays: a boss in reach, or a crowd (the strike pierces, explodes or splits); plain throws otherwise.</summary>
		Situational,
		/// <summary>Strike only against targets that are not bosses (a lone small target or a crowd): the strike wins there and loses against a boss-sized hitbox.</summary>
		SmallTargets,
	}

	/// <summary>
	/// PER-WEAPON ROGUE POLICY (data). Calamity empties the stealth bar on every throw that is not a stealth strike, so a rogue either throws plain at the weapon's use rate (no strikes) or waits out the bar
	/// (standing still about two seconds) and throws one strike. Which is better depends on the weapon: its use time, and what its strike is worth. Measured on a target dummy with a Statigel rogue levy
	/// (docs/fizzrogue/ROGUE.md): the strike cycle gives <c>strikeDamage / (fillTime + useTime)</c> against <c>damage / useTime</c> for spam. A weapon not in <see cref="Table"/> gets the hedge of <see cref="Estimate"/>
	/// (strike for a boss or a crowd, plain throws otherwise).
	/// </summary>
	internal static class RoguePolicy
	{
		/// <summary>Weapons measured or decided by hand, by item full name (Mod/Item). The reason is the measured spam / strike damage per second on a dummy, or the strike's utility.</summary>
		internal static readonly Dictionary<string, RogueMode> Table = new(StringComparer.Ordinal)
		{
			["CalamityMod/WulfrumKnife"] = RogueMode.Wait, // spam 15, strike 24 dps (x1.58) boss x3.04
			["CalamityMod/Kylie"] = RogueMode.Wait, // spam 6, strike 33 dps (x5.72) boss x4.82
			["CalamityMod/UrchinStinger"] = RogueMode.Situational, // spam 25, strike 20 dps (x0.77) boss x1.06
			["CalamityMod/GildedDagger"] = RogueMode.Wait, // spam 15, strike 18 dps (x1.22) boss x0.95
			["CalamityMod/ContaminatedBile"] = RogueMode.Wait, // spam 41, strike 46 dps (x1.13) boss x1.07
			["CalamityMod/Mycoroot"] = RogueMode.Wait, // spam 0, strike 110 dps (plain throws do nothing)
			["CalamityMod/SnapClam"] = RogueMode.Wait, // spam 0, strike 10 dps (plain throws do nothing) boss x1.53
			["CalamityMod/Pumpkaboom"] = RogueMode.Spam, // spam 64, strike 52 dps (x0.81) boss x0.92
			["CalamityMod/AntlionSkewer"] = RogueMode.Spam, // spam 35, strike 28 dps (x0.80) boss x0.91
			["CalamityMod/SludgeSplotch"] = RogueMode.SmallTargets, // spam 7, strike 34 dps (x5.22) boss x0.88
			["CalamityMod/MeteorFist"] = RogueMode.Wait, // spam 87, strike 109 dps (x1.26) boss x0.95
			["CalamityMod/BouncingEyeball"] = RogueMode.Spam, // spam 23, strike 18 dps (x0.78) boss x0.83
			["CalamityMod/RotBall"] = RogueMode.Wait, // spam 56, strike 106 dps (x1.91) boss x1.66
			["CalamityMod/FeatherKnife"] = RogueMode.Situational, // spam 53, strike 53 dps (x1.00) boss x0.90
			["CalamityMod/ShinobiBlade"] = RogueMode.Wait, // spam 56, strike 86 dps (x1.53) boss x0.98
			["CalamityMod/MetalMonstrosity"] = RogueMode.Situational, // spam 10, strike 11 dps (x1.03) boss x1.17
			["CalamityMod/Lionfish"] = RogueMode.Wait, // spam 162, strike 281 dps (x1.73) boss x1.06
			["CalamityMod/Cinquedea"] = RogueMode.SmallTargets, // spam 44, strike 51 dps (x1.15) boss x0.93
			["CalamityMod/TitaniumShuriken"] = RogueMode.Spam, // spam 95, strike 66 dps (x0.69) boss x0.91
			["CalamityMod/CobaltKunai"] = RogueMode.Spam, // spam 154, strike 134 dps (x0.87) boss x0.86
			["CalamityMod/AdamantiteThrowingAxe"] = RogueMode.SmallTargets, // spam 101, strike 114 dps (x1.13) boss x0.85
			["CalamityMod/MythrilKnife"] = RogueMode.Wait, // spam 25, strike 54 dps (x2.16) boss x1.10
			["CalamityMod/PalladiumJavelin"] = RogueMode.SmallTargets, // spam 100, strike 113 dps (x1.13) boss x0.91
			["CalamityMod/IchorSpear"] = RogueMode.Wait, // spam 144, strike 236 dps (x1.64) boss x1.02
			["CalamityMod/GacruxianMollusk"] = RogueMode.Spam, // spam 212, strike 114 dps (x0.54) boss x0.84
			["CalamityMod/StormfrontRazor"] = RogueMode.Spam, // spam 139, strike 131 dps (x0.95) boss x0.94
			["CalamityMod/SkyfinBombers"] = RogueMode.Spam, // spam 930, strike 734 dps (x0.79) boss x0.94
			["CalamityMod/KelvinCatalyst"] = RogueMode.Wait, // spam 367, strike 557 dps (x1.52) boss x1.08
			["CalamityMod/ScourgeoftheSeas"] = RogueMode.SmallTargets, // spam 343, strike 410 dps (x1.19) boss x0.88
			["CalamityMod/SpearofPaleolith"] = RogueMode.Spam, // spam 91, strike 41 dps (x0.45) boss x0.69
			["CalamityMod/BlazingStar"] = RogueMode.Situational, // spam 358, strike 361 dps (x1.01) boss x0.99
			["CalamityMod/SpentFuelContainer"] = RogueMode.SmallTargets, // spam 296, strike 317 dps (x1.07) boss x0.94
			["CalamityMod/GraveGrimreaver"] = RogueMode.Situational, // spam 323, strike 331 dps (x1.02) boss x1.00
			["CalamityMod/CrystalPiercer"] = RogueMode.Wait, // spam 161, strike 266 dps (x1.66) boss x1.03
			["CalamityMod/SpearofDestiny"] = RogueMode.Spam, // spam 322, strike 222 dps (x0.69) boss x0.93
			["CalamityMod/Exorcism"] = RogueMode.Spam, // spam 892, strike 509 dps (x0.57) boss x0.61
			["CalamityMod/LeviathanTeeth"] = RogueMode.Wait, // spam 362, strike 796 dps (x2.20) boss x1.01
			["CalamityMod/TerrorTalons"] = RogueMode.Spam, // spam 147, strike 89 dps (x0.61) boss x0.84
			["CalamityMod/AuroradicalThrow"] = RogueMode.Spam, // spam 683, strike 479 dps (x0.70) boss x0.86
			["CalamityMod/BallisticPoisonBomb"] = RogueMode.Situational, // spam 650, strike 627 dps (x0.96) boss x0.97
			["CalamityMod/StellarKnife"] = RogueMode.Situational, // spam 490, strike 494 dps (x1.01) boss x0.83
			["CalamityMod/Whitewater"] = RogueMode.Spam, // spam 351, strike 250 dps (x0.71) boss x0.96
			["CalamityMod/DeepWounder"] = RogueMode.Wait, // spam 328, strike 443 dps (x1.35) boss x1.01
			["CalamityMod/Apoctolith"] = RogueMode.Wait, // spam 7, strike 270 dps (x36.22) boss x0.99
			["CalamityMod/EpidemicShredder"] = RogueMode.SmallTargets, // spam 105, strike 286 dps (x2.71) boss x0.79
			["CalamityMod/DukesDecapitator"] = RogueMode.Wait, // spam 1987, strike 2360 dps (x1.19) boss x1.00
			["CalamityMod/CraniumSmasher"] = RogueMode.Spam, // spam 598, strike 491 dps (x0.82) boss x0.91
			["CalamityMod/HeavenfallenStardisk"] = RogueMode.Wait, // spam 835, strike 912 dps (x1.09) boss x0.96
			["CalamityMod/ShardofAntumbra"] = RogueMode.Situational, // spam 598, strike 575 dps (x0.96) boss x0.91
			["CalamityMod/UtensilPoker"] = RogueMode.Spam, // spam 624, strike 409 dps (x0.65) boss x0.96
			["CalamityMod/StarofDestruction"] = RogueMode.Spam, // spam 1367, strike 1184 dps (x0.87) boss x0.90
			["CalamityMod/ProfanedPartisan"] = RogueMode.Situational, // spam 833, strike 814 dps (x0.98) boss x1.31
			["CalamityMod/EclipsesFall"] = RogueMode.Spam, // spam 4603, strike 1459 dps (x0.32) boss x0.79
			["CalamityMod/DynamicPursuer"] = RogueMode.Wait, // spam 4950, strike 8066 dps (x1.63) boss x1.72
			["CalamityMod/TimeBolt"] = RogueMode.Spam, // spam 3215, strike 2261 dps (x0.70) boss x0.89
			["CalamityMod/Penumbra"] = RogueMode.Spam, // spam 3137, strike 2846 dps (x0.91) boss x0.90
			["CalamityMod/PlasmaGrenade"] = RogueMode.Spam, // spam 3338, strike 2828 dps (x0.85) boss x0.91
			["CalamityMod/ScarletDevil"] = RogueMode.Spam, // spam 52314, strike 38639 dps (x0.74) boss x0.90
			["CalamityMod/GhoulishGouger"] = RogueMode.SmallTargets, // spam 2078, strike 2237 dps (x1.08) boss x0.88
			["CalamityMod/Valediction"] = RogueMode.SmallTargets, // spam 2063, strike 2538 dps (x1.23) boss x0.95
		};

		internal static RogueMode For(Item item)
		{
			if (item == null || item.IsAir) return RogueMode.Situational;
			string key = item.ModItem != null ? item.ModItem.Mod.Name + "/" + item.ModItem.Name : null;
			if (key != null && Table.TryGetValue(key, out RogueMode m)) return m;
			return Estimate(item.useTime > 0 ? item.useTime : item.useAnimation);
		}

		/// <summary>
		/// The default for a weapon with no table row (a Calamity update's new rogue weapon, another mod's): strike only when it pays. Use time does not predict the winner (the 60 measured weapons: 22 wait, 20 tie, 18 spam,
		/// with fast and slow weapons on every side), so the default hedges: a boss or a crowd gets the strike, a lone small target gets plain throws.
		/// </summary>
		internal static RogueMode Estimate(int useTime) => RogueMode.Situational;
	}
}
