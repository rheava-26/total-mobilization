using System.IO;
using Terraria.ModLoader;
using Levy.Common.Net;

namespace Levy
{
	/// <summary>The mod entry point. Subsystems register their own hooks in their own ModSystems.</summary>
	public class Levy : Mod
	{
		public static Levy Instance => ModContent.GetInstance<Levy>();

		public override void HandlePacket(BinaryReader reader, int whoAmI) => LevyPackets.Handle(this, reader, whoAmI);
	}
}
