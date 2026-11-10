using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Util;
using Warbands.Common;
using Warbands.Content.Items;

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
			BandChatter.Load(this);
			for (int i = 0; i < BandTypes.All.Length; i++) AddContent(new BandSummonItem(BandTypes.All[i], i));
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			var kind = (WarbandsPacket)reader.ReadByte();
			if (kind == WarbandsPacket.SummonBand && Main.netMode == NetmodeID.Server)
			{
				int idx = reader.ReadByte();
				if (whoAmI >= 0 && whoAmI < Main.maxPlayers) BandSpawner.Summon(Main.player[whoAmI], idx);
			}
		}
	}
}
