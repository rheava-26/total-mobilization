using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Content.Buffs
{
	/// <summary>Barracks Ale: +defense for a levy. Like every drink it also makes the levy Tipsy (wider aim error).</summary>
	public class AleBuff : DrinkBuff { public override int IconBuff => BuffID.Ironskin; }
	/// <summary>Black Stout: +damage.</summary>
	public class StoutBuff : DrinkBuff { public override int IconBuff => BuffID.Wrath; }
	/// <summary>Spiced Rum: +critical strike chance.</summary>
	public class RumBuff : DrinkBuff { public override int IconBuff => BuffID.Swiftness; }

	public abstract class DrinkBuff : ModBuff
	{
		public abstract int IconBuff { get; }
		public override string Texture => "Terraria/Images/Buff_" + IconBuff;
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false; Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
		}
	}

	/// <summary>The drinks' effects on a levy: stat deltas for the soldier's dummy, the Tipsy aim penalty, the Rum cadence, and the serving of a round.</summary>
	public static class DrinkBuffs
	{
		public static int[] Types => new[] { ModContent.BuffType<AleBuff>(), ModContent.BuffType<StoutBuff>(), ModContent.BuffType<RumBuff>() };
		public static int TypeOf(int drink) => Types[System.Math.Clamp(drink, 0, 2)];

		/// <summary>Lays a drink buff's stat delta on a soldier's dummy. False when the buff is not one of the drinks.</summary>
		public static bool Apply(Player d, int buffType)
		{
			if (buffType == ModContent.BuffType<AleBuff>()) { d.statDefense += LevyConstants.AleDefense; return true; }
			if (buffType == ModContent.BuffType<StoutBuff>()) { d.GetDamage(DamageClass.Generic) += LevyConstants.StoutDamage; return true; }
			if (buffType == ModContent.BuffType<RumBuff>()) { d.GetCritChance(DamageClass.Generic) += LevyConstants.RumCrit; return true; }
			return false;
		}

		/// <summary>True if the buff type is one of the three drinks.</summary>
		public static bool IsDrinkBuff(int type) => type == ModContent.BuffType<AleBuff>() || type == ModContent.BuffType<StoutBuff>() || type == ModContent.BuffType<RumBuff>();

		/// <summary>True if the NPC carries any drink.</summary>
		public static bool IsDrunk(NPC n)
		{
			if (n == null) return false;
			var t = Types;
			for (int i = 0; i < t.Length; i++) if (n.HasBuff(t[i])) return true;
			return false;
		}

		/// <summary>Multiplier on a soldier's aim error: <see cref="LevyConstants.DrunkAimErrorMult"/> while it carries a drink, else exactly 1.</summary>
		public static float AimErrorFactor(SoldierNPC s) => s != null && IsDrunk(s.NPC) ? LevyConstants.DrunkAimErrorMult : 1f;

		public static bool InReach(Vector2 centre, Vector2 at) => Vector2.Distance(centre, at) <= LevyConstants.DrinkRadiusTiles * LevyConstants.TilePx;

		/// <summary>Serves a round: every levy within the drink radius of <paramref name="centre"/> (the commander's own, plus militia) gets the drink. Returns how many.</summary>
		public static int Serve(Vector2 centre, int drink, string commander)
		{
			int type = TypeOf(drink);
			int n = 0;
			var served = new System.Collections.Generic.List<SoldierNPC>();
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive || s.Record == null) continue;
				if (s.IsRegular && s.Record.CommanderName != commander) continue;
				if (!InReach(centre, s.NPC.Center)) continue;
				s.NPC.AddBuff(type, LevyConstants.DrinkDurationTicks);
				s.StatsDirty = true;
				served.Add(s);
				n++;
			}
			DrinkCheers.Begin(served, commander);
			return n;
		}
	}
}
