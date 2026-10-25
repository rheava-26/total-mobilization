using Terraria.ModLoader;
using Levy.Common;          // Levy.Common.LevyConstants  (cross-mod reference, resolved from the installed Levy.tmod)
using Levy.Common.Util;     // Levy.Common.Util.LevyUtil

namespace Mechanization
{
	/// <summary>
	/// The mod entry point. Mechanization is the machine arm of the levy: it DEPENDS ON the Levy mod (see build.txt
	/// <c>modReferences = Levy</c>) and reuses Levy's proven public helpers rather than re-deriving them.
	///
	/// <see cref="Load"/> is where the cross-mod reference is PROVEN at runtime: it reads a Levy compile-time constant
	/// (resolved at BUILD time — Mechanization will not compile without Levy.dll) AND calls a Levy public static
	/// (<see cref="LevyUtil.IsServerOrSingle"/>, a real cross-assembly call that resolves at LOAD time — a missing or
	/// renamed symbol would throw here the instant Mechanization loads). If this line is in the log with no exception
	/// around it, the assembly reference both compiled and linked.
	/// </summary>
	public class Mechanization : Mod
	{
		public static Mechanization Instance => ModContent.GetInstance<Mechanization>();

		public override void HandlePacket(System.IO.BinaryReader reader, int whoAmI) => global::Mechanization.Common.MechPackets.Handle(reader, whoAmI);

		public override void Load()
		{
			// Build-time proof: a Levy const only compiles when Levy.dll is referenced.
			int tilePx = LevyConstants.TilePx;
			int levyHpCeiling = LevyConstants.MaxSoldierHp;
			// Runtime proof: a live call into Levy's assembly. If the reference did not link, this throws on load.
			bool serverOrSingle = LevyUtil.IsServerOrSingle;

			Logger.Info($"Mechanization: cross-mod reference to Levy RESOLVED. " +
				$"Levy.Common.LevyConstants.TilePx={tilePx}, MaxSoldierHp={levyHpCeiling}; " +
				$"Levy.Common.Util.LevyUtil.IsServerOrSingle={serverOrSingle}. Companion arm online.");
		}
	}
}
