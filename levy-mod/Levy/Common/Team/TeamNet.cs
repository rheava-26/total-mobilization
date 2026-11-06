using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Compat.Thorium;
using Levy.Common.Net;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Team
{
	/// <summary>
	/// How a team effect reaches the commander PLAYER. A player's buffs, life and hurt belong to that player's own machine, so in single player the effect is applied right here, on a dedicated server it is sent to that one
	/// client as <see cref="LevyPacket.TeamEffect"/>, and a reaction only the player's client can start (the commander took a hit under a levy's Paladin's Shield, picked up a Nebula booster) comes back as
	/// <see cref="LevyPacket.TeamRequest"/> for the server to apply to the levies. Touches no Thorium or Calamity type.
	/// <para/>TeamEffect kinds: 1 buff (byte player, ushort buff, int ticks), 2 hurt (byte player, int damage, short npc), 3 nebula (byte player, ushort buff), 4 heal (byte player, int amount).
	/// TeamRequest kinds: 1 paladin share (short npc, int damage, short attacker), 2 nebula (ushort buff).
	/// </summary>
	public static class TeamNet
	{
		public const byte EffBuff = 1, EffHurt = 2, EffNebula = 3, EffHeal = 4;
		public const byte ReqPaladin = 1, ReqNebula = 2;

		public static void Register()
		{
			LevyPackets.Register(LevyPacket.TeamEffect, OnEffect);
			LevyPackets.Register(LevyPacket.TeamRequest, OnRequest);
		}

		// ---------------- server / single player -> the player ----------------
		public static void Buff(Player p, int buff, int ticks)
		{
			if (p == null || !p.active || buff <= 0) return;
			if (!Direct)
			{
				ModPacket k = LevyPackets.New(LevyPacket.TeamEffect);
				k.Write(EffBuff); k.Write((byte)p.whoAmI); k.Write((ushort)buff); k.Write(ticks);
				CompanionFx.SendTo(k, p.whoAmI);
				return;
			}
			AsLocal(p, () => p.AddBuff(buff, ticks, true));
		}

		/// <summary>The commander takes <paramref name="damage"/> as a Paladin's Shield wearer's share (vanilla: Hurt with the legacy empty reason, no knockback), through his own defense.</summary>
		public static void Hurt(Player p, int damage, int attackerNpc)
		{
			if (p == null || !p.active || p.dead || damage <= 0) return;
			if (!Direct)
			{
				ModPacket k = LevyPackets.New(LevyPacket.TeamEffect);
				k.Write(EffHurt); k.Write((byte)p.whoAmI); k.Write(damage); k.Write((short)attackerNpc);
				CompanionFx.SendTo(k, p.whoAmI);
				return;
			}
			AsLocal(p, () => ApplyHurt(p, damage, attackerNpc));
		}

		public static void Nebula(Player p, int buffType)
		{
			if (p == null || !p.active || p.dead) return;
			if (!Direct)
			{
				ModPacket k = LevyPackets.New(LevyPacket.TeamEffect);
				k.Write(EffNebula); k.Write((byte)p.whoAmI); k.Write((ushort)buffType);
				CompanionFx.SendTo(k, p.whoAmI);
				return;
			}
			AsLocal(p, () => TeamNebula.ApplyFromServer(p, buffType));
		}

		public static void Heal(Player p, int amount)
		{
			if (p == null || !p.active || p.dead || amount <= 0) return;
			if (!Direct)
			{
				ModPacket k = LevyPackets.New(LevyPacket.TeamEffect);
				k.Write(EffHeal); k.Write((byte)p.whoAmI); k.Write(amount);
				CompanionFx.SendTo(k, p.whoAmI);
				return;
			}
			AsLocal(p, () => CompanionFx.ApplyHealNow(p, amount));
		}

		/// <summary>True when an effect on the commander is applied on THIS machine: single player, or a harness run whose stand-in player lives on the headless server. A real dedicated server sends it to the player's client.</summary>
		public static bool Direct => Main.netMode != NetmodeID.Server || TeamItems.HarnessLocal;

		/// <summary>Runs <paramref name="a"/> as the player's own machine: on the headless harness server that means Main.myPlayer is the stand-in for the length of the call (vanilla's local-player checks read it).</summary>
		private static void AsLocal(Player p, Action a)
		{
			if (Main.netMode != NetmodeID.Server) { a(); return; }
			int save = Main.myPlayer;
			Main.myPlayer = p.whoAmI;
			try { a(); } finally { Main.myPlayer = save; }
		}

		private static bool _inHurt;
		/// <summary>True while a Paladin's Shield share is being taken (a shared hit is never shared again).</summary>
		public static bool InShare => _inHurt;

		private static void ApplyHurt(Player p, int damage, int attackerNpc)
		{
			_inHurt = true;
			try
			{
				p.Hurt(PlayerDeathReason.LegacyEmpty(), damage, 0); // vanilla's own share: the empty legacy reason, no knockback
			}
			finally { _inHurt = false; }
		}

		/// <summary>Single-player / server entry for a share the commander owes: used by a levy's hit (levy under the commander's shield) and by the harness.</summary>
		public static void ShareToPlayer(Player p, int damage, int attackerNpc)
		{
			Hurt(p, damage, attackerNpc);
		}

		// ---------------- the player's client -> server ----------------
		/// <summary>Client (or single player): the commander was hit while a levy's Paladin's Shield covered him; the server makes that levy take the third.</summary>
		public static void RequestPaladinShare(Player commander, int levyNpc, int damage, int attackerNpc)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket k = LevyPackets.New(LevyPacket.TeamRequest);
				k.Write(ReqPaladin); k.Write((short)levyNpc); k.Write(damage); k.Write((short)attackerNpc);
				k.Send();
				return;
			}
			ApplyPaladinShare(commander, levyNpc, damage, attackerNpc);
		}

		public static void RequestNebula(Player commander, int buffType)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket k = LevyPackets.New(LevyPacket.TeamRequest);
				k.Write(ReqNebula); k.Write((ushort)buffType);
				k.Send();
				return;
			}
			TeamNebula.ShareFromPlayer(commander, buffType);
		}

		// ---------------- handlers ----------------
		private static void OnEffect(BinaryReader r, int from)
		{
			byte kind = r.ReadByte();
			int who = r.ReadByte();
			if (who != Main.myPlayer || who >= Main.maxPlayers) return;
			Player p = Main.player[who];
			switch (kind)
			{
				case EffBuff: { int b = r.ReadUInt16(); int t = r.ReadInt32(); p.AddBuff(b, t, true); break; }
				case EffHurt: { int d = r.ReadInt32(); int n = r.ReadInt16(); ApplyHurt(p, d, n); break; }
				case EffNebula: { int b = r.ReadUInt16(); TeamNebula.ApplyFromServer(p, b); break; }
				case EffHeal: { int a = r.ReadInt32(); CompanionFx.ApplyHealNow(p, a); break; }
			}
		}

		private static void OnRequest(BinaryReader r, int from)
		{
			byte kind = r.ReadByte();
			if (from < 0 || from >= Main.maxPlayers) return;
			Player sender = Main.player[from];
			if (sender == null || !sender.active || sender.dead) { DrainRequest(kind, r); return; }
			switch (kind)
			{
				case ReqPaladin: { int n = r.ReadInt16(); int d = r.ReadInt32(); int a = r.ReadInt16(); ApplyPaladinShare(sender, n, d, a); break; }
				case ReqNebula: { int b = r.ReadUInt16(); TeamNebula.ShareFromPlayer(sender, b); break; }
			}
		}

		private static void DrainRequest(byte kind, BinaryReader r) { /* the payload is simply dropped with the packet */ }

		/// <summary>Server / single player: the levy paladin takes a third of what the commander was just hit for. The server re-validates: it must be this commander's living levy in reach, wearing a shield.</summary>
		private static void ApplyPaladinShare(Player commander, int levyNpc, int damage, int attackerNpc)
		{
			if (LevyUtil.IsClient || levyNpc < 0 || levyNpc >= Main.maxNPCs || damage <= 0 || damage > 100000) return;
			NPC n = Main.npc[levyNpc];
			if (!n.active || n.ModNPC is not SoldierNPC s || !s.IsAlive || !TeamItems.Serves(s, commander)) return;
			if (Microsoft.Xna.Framework.Vector2.Distance(s.NPC.Center, commander.Center) > LevyConstants.TeamPaladinRangePx + 200f) return;
			s.SoakForPlayer(damage, attackerNpc, commander);
		}
	}
}
