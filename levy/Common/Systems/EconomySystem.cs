using Terraria;
using Levy.Common.Config;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>Prices in copper. Pure functions of tier + grade; treasury ops on a barracks. OWNED BY THE ROSTER AGENT.</summary>
	public static class EconomySystem
	{
		public static long LevyWage => (long)(RosterSystem.Instance.LevyWage * LevyConfig.Get.WageMultiplier);
		public static long WagePerDay(Grade grade, bool stoodDown) => (long)(LevyWage * LevyConstants.GradeWageMult[(int)grade] * (stoodDown ? LevyConstants.StoodDownWageMult : 1f));
		public static long MusterFee(Grade grade) => WagePerDay(grade, false) * LevyConstants.MusterFeeDays;
		public static long PromotionCost(Grade from, Grade to) => System.Math.Max(0, MusterFee(to) - MusterFee(from));
		public static long MilitiaMusterFeePerHead => LevyWage / LevyConstants.MilitiaMusterDivisor;
		public static long RecruitingDrivePerPoint => LevyWage * LevyConstants.RecruitingDriveWageMult;

		public static bool CanAfford(BarracksTE b, long cost) => b != null && b.Treasury >= cost;
		public static bool TryPay(BarracksTE b, long cost) { if (!CanAfford(b, cost)) return false; b.Treasury -= cost; return true; }
		public static void Deposit(BarracksTE b, long amount) { if (b != null && amount > 0) b.Treasury += amount; }
		/// <summary>Coin value of an item stack (coins only), 0 otherwise.</summary>
		public static long CoinValue(Item item)
		{
			if (item == null || item.IsAir) return 0;
			return item.type switch { Terraria.ID.ItemID.CopperCoin => 1L * item.stack, Terraria.ID.ItemID.SilverCoin => 100L * item.stack, Terraria.ID.ItemID.GoldCoin => 10000L * item.stack, Terraria.ID.ItemID.PlatinumCoin => 1000000L * item.stack, _ => 0 };
		}
	}
}
