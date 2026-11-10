using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// The surface spawner. Server / single player only. Once a minute, for each player standing in the open with no boss out and no town under them, it rolls the config chance (once the world has reached
	/// FirstStage and fewer than MaxActiveBands are out) and forms a band from the roster of the player's biome at the world's stage, on the ground 40 to 70 tiles to one side.
	/// Env WARBANDS_FORCE=1 forms one band at the first opportunity (headless tests only).
	/// </summary>
	public class BandSpawner : ModSystem
	{
		public const int RollTicks = 60 * 60;
		private static int _nextBandId = 1;
		private int _timer; private bool _forced;
		public static int BandsSpawned;

		public static int ActiveBands()
		{
			var ids = new HashSet<int>();
			for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is BandSoldier b) ids.Add(b.BandId); }
			return ids.Count;
		}

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			var cfg = WarbandsConfig.Get; if (cfg == null || !cfg.Enabled) return;
			bool force = !_forced && Environment.GetEnvironmentVariable("WARBANDS_FORCE") == "1" && Main.GameUpdateCount > 600;
			if (!force && ++_timer < RollTicks) return;
			_timer = 0;
			int stage = Stage.Current;
			if (!force && stage < cfg.FirstStage) return;
			if (ActiveBands() >= cfg.MaxActiveBands) return;
			if (!force && (Main.eclipse || Main.bloodMoon || Main.pumpkinMoon || Main.snowMoon || Terraria.GameContent.Events.DD2Event.Ongoing || Main.invasionType > 0)) return;
			if (force) // headless test: no player is online, so a stand-in at the world spawn takes the roll
			{
				bool any = false; for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i].active) any = true;
				if (!any) { var fake = new Player(); fake.position = new Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16); fake.width = 20; fake.height = 42; _forced = true; SpawnBand(fake, stage, 4); return; }
			}
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				var p = Main.player[i];
				if (!p.active || p.dead || p.townNPCs > 0 && !force) continue;
				if (!force && Main.rand.NextFloat() * 100f >= cfg.SpawnChancePercentPerMinute) continue;
				if (SpawnBand(p, stage, null)) { _forced = true; break; }
			}
		}

		/// <summary>Forms one band near <paramref name="p"/>. Returns false when no footing was found. <paramref name="size"/> null = the config range.</summary>
		public static bool SpawnBand(Player p, int stage, int? size)
		{
			var cfg = WarbandsConfig.Get;
			var roster = BandData.For(BandData.BiomeOf(p), stage);
			if (roster == null) return false;
			int n = size ?? Main.rand.Next(Math.Min(cfg.BandSizeMin, cfg.BandSizeMax), Math.Max(cfg.BandSizeMin, cfg.BandSizeMax) + 1);
			int side = Main.rand.NextBool() ? 1 : -1;
			Point? foot = FindFooting(p, side);
			if (foot == null) { foot = FindFooting(p, -side); }
			if (foot == null) return false;
			int bandId = _nextBandId++;
			for (int k = 0; k < n; k++)
			{
				int idx = k % roster.Members.Length; // every role is fielded before any repeats
				int x = foot.Value.X * 16 + k * 22 * side, y = foot.Value.Y * 16;
				int id = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), x, y, ModContent.NPCType<BandSoldier>());
				if (id < 0 || id >= Main.maxNPCs) continue;
				(Main.npc[id].ModNPC as BandSoldier)?.Setup(roster, idx, stage, bandId);
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, id);
			}
			BandsSpawned++;
			string line = Language.GetTextValue("Mods.Warbands.Chat.Arrive", roster.Name);
			if (Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(line), new Color(210, 150, 90));
			else Main.NewText(line, 210, 150, 90);
			Warbands.Instance.Logger.Info($"[Warbands] band #{bandId} '{roster.Name}' stage {stage} ({Stage.Names[stage]}) size {n} at tile {foot.Value.X},{foot.Value.Y}");
			return true;
		}

		/// <summary>A standing spot 40 to 70 tiles to one side of the player: solid floor, three clear tiles above, nothing liquid.</summary>
		private static Point? FindFooting(Player p, int side)
		{
			int px = (int)(p.Center.X / 16f), py = (int)(p.Center.Y / 16f);
			for (int attempt = 0; attempt < 12; attempt++)
			{
				int x = Math.Clamp(px + side * (40 + Main.rand.Next(31)), 10, Main.maxTilesX - 10);
				for (int y = Math.Max(10, py - 25); y < Math.Min(Main.maxTilesY - 10, py + 40); y++)
				{
					Tile floor = Main.tile[x, y];
					if (!floor.HasUnactuatedTile || !Main.tileSolid[floor.TileType] || Main.tileSolidTop[floor.TileType]) continue;
					bool clear = true;
					for (int a = 1; a <= 3 && clear; a++) { Tile t = Main.tile[x, y - a]; if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] || t.LiquidAmount > 0) clear = false; }
					if (clear) return new Point(x, y - 3);
				}
			}
			return null;
		}
	}
}
