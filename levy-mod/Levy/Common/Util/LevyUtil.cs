using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	public static class LevyUtil
	{
		public static bool IsServerOrSingle => Main.netMode != NetmodeID.MultiplayerClient;
		public static bool IsClient => Main.netMode == NetmodeID.MultiplayerClient;
		public static bool IsServer => Main.netMode == NetmodeID.Server;

		/// <summary>Chat line visible to everyone (server broadcast or local NewText).</summary>
		public static void Chat(string text, Color? color = null)
		{
			Color c = color ?? new Color(255, 240, 20);
			if (Main.netMode == NetmodeID.Server) ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(text), c);
			else if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(text, c);
		}

		/// <summary>Localized text from Mods.Levy.*</summary>
		public static string T(string key, params object[] args) => Language.GetTextValue("Mods.Levy." + key, args);

		/// <summary>Formats copper as "1 gold 20 silver" using vanilla coin names.</summary>
		public static string Coins(long copper)
		{
			if (copper <= 0) return "0 " + Lang.inter[18].Value;
			long plat = copper / 1000000; copper %= 1000000;
			long gold = copper / 10000; copper %= 10000;
			long silver = copper / 100; copper %= 100;
			var sb = new System.Text.StringBuilder();
			if (plat > 0) sb.Append(plat).Append(' ').Append(Lang.inter[15].Value).Append(' ');
			if (gold > 0) sb.Append(gold).Append(' ').Append(Lang.inter[16].Value).Append(' ');
			if (silver > 0) sb.Append(silver).Append(' ').Append(Lang.inter[17].Value).Append(' ');
			if (copper > 0) sb.Append(copper).Append(' ').Append(Lang.inter[18].Value).Append(' ');
			return sb.ToString().TrimEnd();
		}

		/// <summary>Player defense effectiveness for the current world mode (Classic 0.5, Expert 0.75, Master 1.0).</summary>
		public static float ModeDefenseEffectiveness()
		{
			if (Main.masterMode) return 1f;
			if (Main.expertMode) return 0.75f;
			return 0.5f;
		}

		public static float TileDistance(Vector2 a, Vector2 b) => Vector2.Distance(a, b) / LevyConstants.TilePx;
		public static bool WithinTiles(Vector2 a, Vector2 b, float tiles) => Vector2.DistanceSquared(a, b) <= tiles * tiles * LevyConstants.TilePx * LevyConstants.TilePx;

		public static bool IsOnScreen(Rectangle hitbox)
		{
			var screen = new Rectangle((int)Main.screenPosition.X - 100, (int)Main.screenPosition.Y - 100, Main.screenWidth + 200, Main.screenHeight + 200);
			return screen.Intersects(hitbox);
		}

		/// <summary>Any vanilla invasion or event the militia should answer.</summary>
		public static bool IsEventActive()
		{
			return (Main.invasionType > 0 && Main.invasionSize > 0) || Main.bloodMoon || Main.eclipse || Main.pumpkinMoon || Main.snowMoon
				|| Terraria.GameContent.Events.DD2Event.Ongoing;
		}

		/// <summary>True while any hostile boss (or boss-tier NPC) is alive, so the garrison turns out to fight it.</summary>
		public static bool IsBossActive()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly) continue;
				if (n.boss || (n.type >= 0 && n.type < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[n.type])) return true;
			}
			return false;
		}

		/// <summary>Finds the player index by name, -1 if not online.</summary>
		public static int FindPlayerByName(string name)
		{
			if (string.IsNullOrEmpty(name)) return -1;
			for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i].active && Main.player[i].name == name) return i;
			return -1;
		}

		/// <summary>Resolves a main-boss NPC to its head/core type (worm/ML parts via realLife).</summary>
		public static NPC RootNpc(NPC npc)
		{
			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active) return Main.npc[npc.realLife];
			return npc;
		}

		public static bool IsHostileTarget(NPC npc) => npc.active && !npc.friendly && !npc.townNPC && npc.lifeMax > 5 && !npc.dontTakeDamage && !npc.immortal && !NPCID.Sets.CountsAsCritter[npc.type];
	}
}
