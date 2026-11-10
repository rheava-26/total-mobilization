using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Util;

namespace Warbands
{
	/// <summary>
	/// Warbands: hostile levy bands, a sub-mod of Levy (build.txt modReferences = Levy). Every Levy touch point is listed in warbands-mod/SEAMS.md.
	/// </summary>
	public class Warbands : Mod
	{
		public static Warbands Instance => ModContent.GetInstance<Warbands>();

		public override void Load()
		{
			// Same proof Mechanization gives: a Levy constant (build time) and a Levy public static (load time).
			Logger.Info($"Warbands: Levy reference RESOLVED. LevyConstants.TilePx={LevyConstants.TilePx}, MaxSoldierHp={LevyConstants.MaxSoldierHp}, LevyUtil.IsServerOrSingle={LevyUtil.IsServerOrSingle}.");
		}
	}
}
