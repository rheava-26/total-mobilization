namespace Levy.Common
{
	/// <summary>Numbers for the flavor module: Nurse healing, the event books and the Guide hint.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Soldiers this close to the Nurse (tiles) are healed.</summary>
		public const int NurseHealRadiusTiles = 4;
		/// <summary>The Nurse checks for wounded soldiers this often (ticks).</summary>
		public const int NurseHealIntervalTicks = 120;
		/// <summary>The Nurse is paid by the nearest barracks within this many tiles.</summary>
		public const int NurseTreasuryReachTiles = 120;
		/// <summary>Minimum ticks between two "Already studied." notices on the same client.</summary>
		public const int BookNoticeCooldownTicks = 90;
	}
}
