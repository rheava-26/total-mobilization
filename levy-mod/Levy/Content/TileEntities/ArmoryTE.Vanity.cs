using Terraria;
using Terraria.ID;
using Levy.Common.Vanity;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// The armory's vanity sets (see <see cref="ArmoryVanity"/>): the levies this armory serves wear them in their social armor slots. Only the server (or single player) ever writes <see cref="Vanity"/>;
	/// a multiplayer client sends its edited list to the server (ArmoryVanitySet) and receives the result with the tile entity.
	/// </summary>
	public partial class ArmoryTE
	{
		public ArmoryVanity Vanity = new ArmoryVanity();

		/// <summary>Set by the vanity screen on a multiplayer client while an edit waits to be sent: a tile entity sync arriving meanwhile must not overwrite it.</summary>
		public bool VanityHeldBack;

		/// <summary>Server / single player: replaces the vanity list with <paramref name="incoming"/> (already read and clamped by <see cref="ArmoryVanity.FromBytes"/>), bumps the revision and queues a sync.</summary>
		public void SetVanity(ArmoryVanity incoming)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || incoming == null) return;
			int rev = Vanity.Rev + 1;
			Vanity.CopyFrom(incoming);
			Vanity.Rev = rev;
			MarkDirty();
		}
	}
}
