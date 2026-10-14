namespace Levy.Common.Boss
{
	/// <summary>Terror lookups incl. trophy reductions. OWNED BY THE BOSS AGENT (see LevyConstants.Terror).</summary>
	public static class TerrorTable
	{
		public static int BaseTerror(int npcType) => LevyConstants.Terror.TryGetValue(npcType, out var t) ? t : 5;
	}
}
