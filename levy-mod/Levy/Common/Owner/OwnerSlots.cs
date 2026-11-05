using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Owner
{
	/// <summary>
	/// Per-soldier owner slots. A soldier whose real items spawn projectiles needs a player slot of its own: on a shared slot QuickGrapple's per-owner checks (hooks in flight, hooks held) make all but one cast fail.
	/// <para/>Slots are taken from <see cref="Top"/> downward and only ever above everything a real client could hold: the server's configured player limit, every connected client and every active real player.
	/// Slots 254, 253 and 252 are not handed out: <see cref="Levy.Common.Weapons.SoldierMinions"/> parks and aliases owners there. A slot is held by one soldier body until it despawns or dies
	/// (<see cref="Release"/>, and a sweep once a second for bodies that vanished without a death). Outside a frame a pooled slot holds a blank inactive player, as every other client-less slot does.
	/// </summary>
	public sealed class OwnerSlots : ModSystem
	{
		/// <summary>The highest slot a soldier is given: below the three SoldierMinions parks (254, 253, 252).</summary>
		public const int Top = 251;
		private static readonly Dictionary<SoldierNPC, int> _byBody = new();
		private static readonly bool[] _used = new bool[256];
		private static readonly List<SoldierNPC> _scratch = new();
		public static int InUse { get; private set; }
		public static long Allocations, Refusals;
		private static bool _warned;

		/// <summary>The lowest slot index that is NOT available to soldiers: at or below it a real client could be (or is). Single player: slot 0 and any active player.</summary>
		public static int RealFloor()
		{
			int f = Main.netMode == NetmodeID.Server ? Math.Max(1, Main.maxNetPlayers) : 1;
			for (int i = 0; i < Top; i++)
			{
				Player p = Main.player[i];
				if (p != null && p.active && !_used[i] && !SoldierOwner.IsDummy(p)) f = Math.Max(f, i + 1);
			}
			if (Main.netMode == NetmodeID.Server)
				for (int i = 0; i < Top && i < Netplay.Clients.Length; i++)
					if (Netplay.Clients[i] != null && Netplay.Clients[i].IsActive) f = Math.Max(f, i + 1);
			return f;
		}

		/// <summary>True when <paramref name="slot"/> is currently held by a soldier (never a real player's).</summary>
		public static bool IsPooled(int slot) => slot >= 0 && slot < _used.Length && _used[slot];

		/// <summary>The soldier's slot (allocated on first use), or -1 when none is free above the real-player slots.</summary>
		public static int For(SoldierNPC body)
		{
			if (body == null) return -1;
			int floor = RealFloor();
			if (_byBody.TryGetValue(body, out int s))
			{
				if (s > floor) return s;
				Release(body); // a real client now holds (or could hold) it
			}
			for (int c = Top; c > floor; c--)
			{
				if (_used[c]) continue;
				_used[c] = true; _byBody[body] = c; InUse++; Allocations++;
				return c;
			}
			Refusals++;
			if (!_warned) { _warned = true; Levy.Instance?.Logger.Warn("Levy: no free owner slot for a soldier above the real-player slots (floor " + floor + "); hooks stay off for it. Lower the server's player limit to free slots."); }
			return -1;
		}

		public static void Release(SoldierNPC body)
		{
			if (body == null || !_byBody.TryGetValue(body, out int s)) return;
			_byBody.Remove(body);
			if (s >= 0 && s < _used.Length && _used[s]) { _used[s] = false; InUse--; }
			Player p = Main.player[s];
			if (p != null && SoldierOwner.IsDummy(p)) Main.player[s] = new Player(); // a dummy left standing here by an exception: blank it
		}

		/// <summary>Frees the slots of bodies that are gone (despawned, killed, replaced by a new NPC in the same index).</summary>
		public static void Sweep()
		{
			if (_byBody.Count == 0) return;
			_scratch.Clear();
			foreach (var kv in _byBody)
			{
				SoldierNPC b = kv.Key;
				if (b?.NPC == null || !b.NPC.active || !ReferenceEquals(b.NPC.ModNPC, b) || b.NPC.life <= 0) _scratch.Add(b);
			}
			for (int i = 0; i < _scratch.Count; i++) Release(_scratch[i]);
			_scratch.Clear();
		}

		public static void Reset()
		{
			Array.Clear(_used, 0, _used.Length); _byBody.Clear(); InUse = 0; _warned = false;
		}

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();
		public override void PostUpdateEverything()
		{
			if (Main.GameUpdateCount % 60 == 7) Sweep();
		}
	}
}
