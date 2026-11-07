using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Common.Movement
{
	/// <summary>
	/// The world's door custodian (server / single player). Every door a levy or an officer opens (regular, modded, tall gate) is registered here, keyed by its top tile so the doors of a stack are separate
	/// entries, and the custodian shuts each one again as soon as nobody stands in the doorway. It does not depend on the opener: a stream of levies, a levy that died, was knocked or hooked far away,
	/// or opened the next door before the last one shut, all leave the same registered door, and it is closed on the first quiet moment within DoorCustodianTicks. Doors a levy did not open are never touched.
	/// The shut itself is vanilla's (WorldGen.CloseDoor refuses while any body is in the doorway), so a levy is never walled in.
	/// </summary>
	public sealed class DoorCustodian : ModSystem
	{
		private struct Entry { public int Left; public int Height; public bool Gate; }
		private static readonly Dictionary<long, Entry> Doors = new Dictionary<long, Entry>();
		private static readonly List<long> Scratch = new List<long>();
		private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

		/// <summary>Doors currently waiting to be shut (harness and debugging).</summary>
		public static int Pending => Doors.Count;

		public override void OnWorldLoad() => Doors.Clear();
		public override void OnWorldUnload() => Doors.Clear();

		/// <summary>
		/// Opens the closed door (or tall gate) at the tile (x, y) for a body walking in direction dir, tells clients, and registers it for closing. Server / single player only.
		/// False when the door would not open (locked, blocked leaf side, a client).
		/// </summary>
		public static bool TryOpen(int x, int y, int dir)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return false;
			Tile t = Framing.GetTileSafely(x, y);
			if (t.TileType == TileID.TallGateClosed)
			{
				int top = y; while (Framing.GetTileSafely(x, top - 1).TileType == TileID.TallGateClosed) top--;
				if (!WorldGen.ShiftTallGate(x, y, closing: false)) return false;
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 4, x, y);
				Register(x, top, 5, true);
				return true;
			}
			int doorTop = y - (t.TileFrameY % 54) / 18; // a door is three tiles tall; frameY runs 0, 18, 36 within a style
			if (WorldGen.OpenDoor(x, y, dir))
			{
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 0, x, y, dir);
				Register(x, doorTop, 3, false);
				return true;
			}
			if (WorldGen.OpenDoor(x, y, -dir))
			{
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 0, x, y, -dir);
				Register(x, doorTop, 3, false);
				return true;
			}
			return false;
		}

		private static void Register(int x, int top, int height, bool gate)
			=> Doors[Key(x, top)] = new Entry { Left = DoorCustodianTicks, Height = height, Gate = gate }; // opening it again refreshes the clock

		public override void PostUpdateNPCs()
		{
			if (Doors.Count == 0 || Main.netMode == NetmodeID.MultiplayerClient) return;
			if (Main.GameUpdateCount % DoorCustodianIntervalTicks != 0) return;
			Scratch.Clear(); Scratch.AddRange(Doors.Keys);
			foreach (long k in Scratch)
			{
				Entry e = Doors[k];
				int x = (int)(k >> 32), y = (int)(uint)k;
				Tile t = Framing.GetTileSafely(x, y);
				bool openDoor = !e.Gate && TileLoader.CloseDoorID(t) >= 0;
				bool openGate = e.Gate && t.TileType == TileID.TallGateOpen;
				if (!openDoor && !openGate) { Doors.Remove(k); continue; } // someone else shut it (or the tile is gone): not ours any more
				e.Left -= DoorCustodianIntervalTicks;
				if (e.Left <= 0) { Doors.Remove(k); continue; }
				// a margin of a tile each side keeps the door open for a body about to step into it; in the last seconds only vanilla's own test (a body IN the doorway) holds it
				bool strict = e.Left <= DoorCustodianStrictTicks;
				if (!strict && Occupied(x, y, e.Height)) { Doors[k] = e; continue; }
				if (TryClose(x, y, e.Gate)) { Doors.Remove(k); continue; }
				Doors[k] = e;
			}
		}

		private static bool TryClose(int x, int top, bool gate)
		{
			if (gate)
			{
				if (!WorldGen.ShiftTallGate(x, top, closing: true)) return false;
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 5, x, top);
				return true;
			}
			if (!WorldGen.CloseDoor(x, top)) return false;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 1, x, top, 1);
			return true;
		}

		/// <summary>True when a player, a town NPC (officers included) or a levy is in, or within DoorCustodianMarginTiles of, the doorway column.</summary>
		private static bool Occupied(int x, int top, int height)
		{
			int m = DoorCustodianMarginTiles * 16;
			var r = new Rectangle(x * 16 - m, top * 16 - m, 16 + 2 * m, height * 16 + 2 * m);
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && !p.dead && p.Hitbox.Intersects(r)) return true;
			}
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && (n.townNPC || n.ModNPC is SoldierNPC) && n.Hitbox.Intersects(r)) return true;
			}
			return false;
		}
	}
}
