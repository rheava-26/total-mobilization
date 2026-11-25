using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Wotg
{
	/// <summary>
	/// The army follows its commander through the Avatar of Emptiness's moves (Common/Compat/Wotg/AvatarFight.cs: the boss sets a player's Center to the middle of the world, with no teleport call and no packet, and a portal
	/// moves one with Player.Teleport). Once a tick, while the fight is on, it notes every player who was moved more than <see cref="MoveTiles"/> since the last tick and brings his followers to him
	/// (<see cref="SoldierNPC.AvatarRejoin"/>: into the air beside him in the dimension or for a body with wings, onto the ground near him for the rest when the arena is back). Followers that the commander's own Player.Teleport
	/// already brought are not far any more and are left alone. Server and single player.
	/// </summary>
	public sealed class AvatarFightSystem : ModSystem
	{
		/// <summary>A player who moves this far in one tick (tiles) was moved by the fight: nothing a player flies does it.</summary>
		public const float MoveTiles = 30f;
		private static readonly Vector2[] _last = new Vector2[Main.maxPlayers];
		private static readonly bool[] _have = new bool[Main.maxPlayers];
		private static readonly uint[] _movedAt = new uint[Main.maxPlayers];
		/// <summary>Harness telemetry: moves seen.</summary>
		internal static int Moves;

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();
		private static void Reset() { Array.Clear(_have); for (int i = 0; i < _movedAt.Length; i++) _movedAt[i] = uint.MaxValue - 100000; Moves = 0; }

		/// <summary>True when this player was moved by the fight within the last <paramref name="ticks"/> ticks.</summary>
		public static bool MovedRecently(int player, int ticks) => (uint)player < (uint)_movedAt.Length && Main.GameUpdateCount - _movedAt[player] <= (uint)ticks;

		public override void PostUpdateEverything()
		{
			if (LevyUtil.IsClient || RosterSystem.Instance == null) return;
			if (!AvatarFight.Applies) { Array.Clear(_have); return; }
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active || p.dead) { _have[i] = false; continue; }
				if (_have[i] && Vector2.Distance(p.Center, _last[i]) > MoveTiles * 16f)
				{
					_movedAt[i] = Main.GameUpdateCount; Moves++;
					try { BringArmy(p, 40f); } catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: Avatar fight follow failed: " + e); }
				}
				_last[i] = p.Center; _have[i] = true;
			}
		}

		/// <summary>Brings a commander's followers (regulars and officers on Follow, alive, not routed or on their way home) that are more than <paramref name="minTiles"/> from him to his side. In the dimension, or for a body with wings, in the air.</summary>
		public static void BringArmy(Player cmd, float minTiles)
		{
			if (!AvatarFight.Applies || cmd == null || !cmd.active) return;
			bool air = AvatarFight.Void || AvatarFight.IsUniversePoint(cmd.Center);
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || !s.IsAlive || s.Record == null) continue;
				if (s.Record.Order != SoldierOrder.Follow || s.Record.StoodDown || !string.Equals(s.Record.CommanderName, cmd.name, StringComparison.Ordinal)) continue;
				if (s.State == SoldierState.Routed || s.State == SoldierState.ReturnHome || s.State == SoldierState.Desert || s.State == SoldierState.Dead) continue;
				if (Vector2.Distance(n.Center, cmd.Center) <= minTiles * 16f) continue;
				s.AvatarRejoin(cmd, air);
			}
		}
	}
}
