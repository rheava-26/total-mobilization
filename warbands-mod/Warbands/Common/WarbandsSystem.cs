using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// Per tick, server and single player: rebuilds every band's member list (BandBrain.Update) and keeps one invisible stand-in NPC on each player while any hostile soldier is out (the Levy brain targets NPC indices,
	/// so a player is attacked through the NPC that sits on them). The stand-ins go away a few seconds after the last band member.
	/// </summary>
	public class WarbandsSystem : ModSystem
	{
		private int _idle;
		public static int StandIns;

		public override void OnWorldLoad() { BandBrain.Reset(); _idle = 0; }
		public override void OnWorldUnload() { BandBrain.Reset(); }

		public override void PreUpdateNPCs()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			BandBrain.Update();
			bool any = false;
			int standType = ModContent.NPCType<PlayerStandIn>();
			for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is BandLevyNPC) { any = true; break; } }
			if (any)
			{
				_idle = 0;
				var have = new bool[Main.maxPlayers];
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					var n = Main.npc[i];
					if (!n.active || n.type != standType) continue;
					int pi = (int)n.ai[0];
					if (pi < 0 || pi >= Main.maxPlayers || !Main.player[pi].active || have[pi]) { n.active = false; continue; }
					have[pi] = true;
				}
				for (int pi = 0; pi < Main.maxPlayers; pi++)
				{
					var p = Main.player[pi];
					if (!p.active || have[pi]) continue;
					int id = NPC.NewNPC(new EntitySource_Misc("Warbands:StandIn"), (int)p.Center.X, (int)p.Bottom.Y, standType, 0, pi);
					if (id >= 0 && id < Main.maxNPCs) { Main.npc[id].ai[0] = pi; Main.npc[id].Center = p.Center; StandIns++; }
				}
			}
			else if (++_idle > 300)
			{
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].type == standType) Main.npc[i].active = false;
				_idle = 300;
			}
		}
	}
}
