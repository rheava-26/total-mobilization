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
	/// The band spawner. Server / single player only. Natural bands: once a minute, for each player standing in the open with no boss out and no town under them, it rolls the config chance (once the world has reached
	/// FirstStage and fewer than MaxActiveBands are out) and forms a band of the player's biome and world stage on the ground 40 to 70 tiles to one side. Summoned bands (the crafted items) form 24 to 34 tiles to one side on demand.
	/// A band runs Levy's soldier brain (BandLevyNPC) unless the config asks for the older Fighter-AI soldiers (BandSoldier).
	/// Band size is the config range plus one per ten active levies (cap 8), so a big army does not trivialise a band.
	/// Env WARBANDS_FORCE=1 forms one band at the first opportunity (headless tests only).
	/// </summary>
	public class BandSpawner : ModSystem
	{
		public const int RollTicks = 60 * 60;
		private int _timer; private bool _forced;
		public static int BandsSpawned;
		public const int MaxBand = 8;

		public static int ActiveBands()
		{
			var ids = new HashSet<int>();
			for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (!n.active) continue; if (n.ModNPC is BandSoldier b) ids.Add(b.BandId); else if (n.ModNPC is BandLevyNPC l) ids.Add(l.BandId); }
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

		/// <summary>Bonus members for the player's army: one per ten active levies.</summary>
		public static int ArmyBonus() => BandBrain.CountLevies() / 10;

		/// <summary>Natural band near <paramref name="p"/>: the named type for the player's biome at the world stage (the legacy biome roster when none fits). Returns false when no footing was found.</summary>
		public static bool SpawnBand(Player p, int stage, int? size)
		{
			var cfg = WarbandsConfig.Get;
			var biome = BandData.BiomeOf(p);
			bool legacy = !cfg.LevyBrain;
			var type = legacy ? BandData.For(biome, stage) : BandTypes.Pick(biome, stage, Main.rand) ?? BandData.For(biome, stage);
			if (type == null) return false;
			int n = size ?? Main.rand.Next(Math.Min(cfg.BandSizeMin, cfg.BandSizeMax), Math.Max(cfg.BandSizeMin, cfg.BandSizeMax) + 1);
			bool neutral = !legacy && Main.rand.NextFloat() * 100f < cfg.NeutralBandPercent;
			return SpawnBand(p, type, n, 40, 70, legacy, true, neutral);
		}

		/// <summary>Forms one band of <paramref name="type"/> near <paramref name="p"/>, <paramref name="minTiles"/> to <paramref name="maxTiles"/> to one side. <paramref name="size"/> is the base count; the army bonus is added and the total capped at 8.</summary>
		public static bool SpawnBand(Player p, BandRoster type, int size, int minTiles, int maxTiles, bool legacy = false, bool armyBonus = true, bool neutral = false)
		{
			int side = Main.rand.NextBool() ? 1 : -1;
			Point? foot = FindFooting(p, side, minTiles, maxTiles);
			if (foot == null) { foot = FindFooting(p, -side, minTiles, maxTiles); side = -side; }
			if (foot == null) return false;
			int n = Math.Min(MaxBand, Math.Max(1, size) + (armyBonus ? ArmyBonus() : 0));
			int stage = type.Stage;
			BandBrain brain = null; int bandId = BandBrain.NextId();
			if (!legacy) brain = BandBrain.Create(type, stage, n, new Vector2(foot.Value.X * 16, foot.Value.Y * 16), bandId, neutral);
			int made = 0;
			for (int k = 0; k < n; k++)
			{
				int idx = k % type.Members.Length; // every role is fielded before any repeats
				int x = foot.Value.X * 16 + k * 22 * side, y = foot.Value.Y * 16;
				if (legacy)
				{
					int lid = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), x, y, ModContent.NPCType<BandSoldier>());
					if (lid < 0 || lid >= Main.maxNPCs) continue;
					(Main.npc[lid].ModNPC as BandSoldier)?.Setup(type, idx, stage, bandId);
					if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, lid);
					made++;
					continue;
				}
				int id = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), x, y, ModContent.NPCType<BandLevyNPC>());
				if (id < 0 || id >= Main.maxNPCs) continue;
				(Main.npc[id].ModNPC as BandLevyNPC)?.Setup(type, k, stage, bandId, n);
				Main.npc[id].direction = -side;
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, id);
				made++;
			}
			if (made == 0) { if (brain != null) BandBrain.All.Remove(bandId); return false; }
			if (brain != null) brain.StartSize = made;
			BandsSpawned++;
			string line = Language.GetTextValue("Mods.Warbands.Chat.Arrive", type.DisplayName);
			if (Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(line), new Color(210, 150, 90));
			else Main.NewText(line, 210, 150, 90);
			Warbands.Instance.Logger.Info($"[Warbands] band #{bandId} '{type.Name}' stage {stage} size {made}{(legacy ? " (fighter AI)" : "")} at tile {foot.Value.X},{foot.Value.Y}");
			return true;
		}

		/// <summary>The summoning item was used: a band of that type forms 24 to 34 tiles from the player.</summary>
		public static bool Summon(Player p, int typeIdx)
		{
			var type = BandTypes.FromIndex(typeIdx); if (type == null || p == null || !p.active) return false;
			var cfg = WarbandsConfig.Get; if (cfg == null) return false;
			return SpawnBand(p, type, cfg.SummonedBandSize, 24, 34, false);
		}

		/// <summary>A standing spot <paramref name="minTiles"/> to <paramref name="maxTiles"/> tiles to one side of the player: solid floor, three clear tiles above, nothing liquid.</summary>
		private static Point? FindFooting(Player p, int side, int minTiles, int maxTiles)
		{
			int px = (int)(p.Center.X / 16f), py = (int)(p.Center.Y / 16f);
			for (int attempt = 0; attempt < 14; attempt++)
			{
				int x = Math.Clamp(px + side * (minTiles + Main.rand.Next(Math.Max(1, maxTiles - minTiles + 1))), 10, Main.maxTilesX - 10);
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
