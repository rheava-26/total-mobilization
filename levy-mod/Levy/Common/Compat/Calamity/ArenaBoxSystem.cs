using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// The end of the tick for a body held by a Calamity arena box. <see cref="ArenaBoxes.Contain"/> holds a levy after its AI (SoldierNPC.PostAI), but the engine then moves it by the speed it still carries: a body standing on tiles that lie
	/// beyond the box's bottom wall (the world's ground under the box, a platform row across the wall) was put back inside by the wall and dropped back onto the tiles by the engine on the same tick, every tick, and ended each tick
	/// below the wall at a full tile's fall speed. Holding it once more after the engine has moved every NPC leaves it AT the wall, at rest, and the next tick starts from a body that is standing (SoldierNPC.ArenaBox.cs).
	/// With no box up this is one static check per tick.
	/// </summary>
	public sealed class ArenaBoxSystem : ModSystem
	{
		public override void PostUpdateNPCs()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaBoxes.Active) return;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s) continue;
				bool floor = ArenaBoxes.Contain(n);
				s.NoteBoxHeld(floor);
				if (floor && n.velocity.Y >= 0f) n.velocity.Y = 0f;
			}
		}
	}
}
