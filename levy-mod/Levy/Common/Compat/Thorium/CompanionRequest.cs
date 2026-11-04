using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Content.NPCs;
using Levy.Common.Util;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// The PLAYER -> soldier direction of the compat. A Thorium heal or bard cast belongs to the casting player's own client (Thorium only runs it there), but a soldier's life and buffs
	/// belong to the server, so in multiplayer the client sends a <see cref="LevyPacket.CompanionRequest"/> and the server re-validates and applies it; in single player the same
	/// application runs directly. Touches no ThoriumMod type (the empowerment kind is decoded by the gated <see cref="EmpowerCompat"/>), so it is not JIT-gated.
	/// </summary>
	public static class CompanionRequest
	{
		public const byte KindHeal = 1, KindEmpower = 2;
		/// <summary>Ticks a soldier is left alone by other healers after a heal lands on it.</summary>
		public const int ReserveTicks = 40;
		/// <summary>Furthest a soldier may be from the casting player for the server to accept a request about him.</summary>
		public const float MaxReachPx = 3200f;
		/// <summary>Most life one request may restore, and most targets one packet carries (sanity bounds on client-supplied numbers).</summary>
		public const int MaxHeal = 2000, MaxTargets = 8;

		private static readonly uint[] LastEmpower = new uint[Main.maxPlayers + 1];

		/// <summary>True when <paramref name="s"/> serves <paramref name="commander"/> (a soldier or an officer avatar of that player).</summary>
		public static bool Serves(SoldierNPC s, Player commander) =>
			s?.Record != null && commander != null && string.Equals(s.Record.CommanderName ?? "", commander.name ?? "", StringComparison.Ordinal);

		/// <summary>
		/// Server / single player: heal soldier <paramref name="t"/> by <paramref name="amount"/> and mark him as just healed so a soldier healer does not pile on.
		/// Returns the life gained (0 when he is gone, full or reserved).
		/// </summary>
		public static int HealSoldier(NPC t, int amount)
		{
			if (t == null || !t.active || t.ModNPC is not SoldierNPC) return 0;
			int gained = SoldierHeal.Restore(t, Math.Clamp(amount, 0, MaxHeal));
			if (gained > 0 && t.TryGetGlobalNPC(out SoldierThoriumGlobalNPC g)) g.HealReservedUntil = Main.GameUpdateCount + ReserveTicks;
			return gained;
		}

		/// <summary>Client (multiplayer): ask the server to heal these soldiers (npc index, amount) on behalf of the local player.</summary>
		public static void SendHeal(List<(int npc, int amount)> targets)
		{
			if (targets == null || targets.Count == 0) return;
			ModPacket pkt = LevyPackets.New(LevyPacket.CompanionRequest);
			int n = Math.Min(targets.Count, MaxTargets);
			pkt.Write(KindHeal); pkt.Write((byte)n);
			for (int i = 0; i < n; i++) { pkt.Write((short)targets[i].npc); pkt.Write((short)Math.Clamp(targets[i].amount, 0, MaxHeal)); }
			pkt.Send();
		}

		/// <summary>Server: decode a heal request. The sender is the commander; only his own living, wounded soldiers within reach are healed.</summary>
		public static void OnHeal(BinaryReader r, int fromWhoAmI)
		{
			int n = Math.Min((int)r.ReadByte(), MaxTargets);
			var idx = new short[n]; var amt = new short[n];
			for (int i = 0; i < n; i++) { idx[i] = r.ReadInt16(); amt[i] = r.ReadInt16(); }
			if (!LevyUtil.IsServerOrSingle || fromWhoAmI < 0 || fromWhoAmI >= Main.maxPlayers) return;
			Player p = Main.player[fromWhoAmI];
			if (p == null || !p.active || p.dead) return;
			uint now = Main.GameUpdateCount;
			for (int i = 0; i < n; i++)
			{
				if (idx[i] < 0 || idx[i] >= Main.maxNPCs) continue;
				NPC t = Main.npc[idx[i]];
				if (!t.active || t.life <= 0 || t.life >= t.lifeMax || t.ModNPC is not SoldierNPC s || !Serves(s, p)) continue;
				if (Microsoft.Xna.Framework.Vector2.DistanceSquared(p.Center, t.Center) > MaxReachPx * MaxReachPx) continue;
				if (t.TryGetGlobalNPC(out SoldierThoriumGlobalNPC g) && g.HealReservedUntil > now) continue;
				HealSoldier(t, amt[i]);
			}
		}

		/// <summary>Server: accept at most one empowerment request per sender per few ticks (an instrument fires many times a second; the buff lasts seconds).</summary>
		public static bool AllowEmpower(int fromWhoAmI)
		{
			if (fromWhoAmI < 0 || fromWhoAmI >= Main.maxPlayers) return false;
			uint stamp = unchecked(Main.GameUpdateCount + 1u); // never 0, so 0 can mean "no request yet"
			if (LastEmpower[fromWhoAmI] != 0u && unchecked(stamp - LastEmpower[fromWhoAmI]) < 8u) return false;
			LastEmpower[fromWhoAmI] = stamp;
			return true;
		}
	}
}
