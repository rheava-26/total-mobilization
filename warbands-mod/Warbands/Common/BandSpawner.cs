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
	/// FirstStage, fewer than MaxWorldBands are out and fewer than MaxActiveBands are within 200 tiles of that player) and forms a band of the player's biome and world stage on the ground 40 to 70 tiles to one side. Summoned bands (the crafted items) form 24 to 34 tiles to one side on demand.
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

		/// <summary>Distinct bands with a living member within <paramref name="tiles"/> of a point.</summary>
		public static int BandsNear(Vector2 pos, float tiles)
		{
			var ids = new HashSet<int>(); float r2 = tiles * 16f * tiles * 16f;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i]; if (!n.active || Vector2.DistanceSquared(n.Center, pos) > r2) continue;
				if (n.ModNPC is BandSoldier b) ids.Add(b.BandId); else if (n.ModNPC is BandLevyNPC l) ids.Add(l.BandId);
			}
			return ids.Count;
		}
		/// <summary>Radius of the "a band is already about" rule for a natural roll.</summary>
		public const float NearBandTiles = 200f;
		/// <summary>Natural bands that could not form because the NPC array was full (self-check and sampler read it).</summary>
		public static int SlotFailures;

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			var cfg = WarbandsConfig.Get; if (cfg == null || !cfg.Enabled) return;
			bool force = !_forced && Environment.GetEnvironmentVariable("WARBANDS_FORCE") == "1" && Main.GameUpdateCount > 600;
			if (!force && ++_timer < RollTicks) return;
			_timer = 0;
			int stage = Stage.Current;
			if (!force && stage < cfg.FirstStage) return;
			if (ActiveBands() >= cfg.MaxWorldBands) return;
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
				if (!force && BandsNear(p.Center, NearBandTiles) >= cfg.MaxActiveBands) continue; // a band already out near this player: it is the local rare enemy, no second one on top
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
			bool neutral = !legacy && RollNeutral(p, type);
			return SpawnBand(p, type, n, 40, 70, legacy, true, neutral);
		}

		/// <summary>Whether a natural band forms neutral: always in the Calamity Sunken Sea (and for a type flagged AlwaysNeutral), else the config percent.</summary>
		public static bool RollNeutral(Player p, BandRoster type)
		{
			var cfg = WarbandsConfig.Get;
			if (type != null && type.AlwaysNeutral) return true;
			if (CalamityZones.InSunkenSea(p)) return true;
			return Main.rand.NextFloat() * 100f < (cfg?.NeutralBandPercent ?? 90f);
		}

		/// <summary>Forms one band of <paramref name="type"/> near <paramref name="p"/>, <paramref name="minTiles"/> to <paramref name="maxTiles"/> to one side. <paramref name="size"/> is the base count; the army bonus is added and the total capped at 8.</summary>
		public static bool SpawnBand(Player p, BandRoster type, int size, int minTiles, int maxTiles, bool legacy = false, bool armyBonus = true, bool neutral = false, int forceSide = 0, bool eventFoot = false)
		{
			// an event band is its captain plus the members asked for (the captain does not count against the config size)
			bool isEvent = type.Event != EventKind.None;
			int n = Math.Min(MaxBand, Math.Max(1, size) + (armyBonus ? ArmyBonus() : 0) + (isEvent ? 1 : 0));
			// find room for the whole band before anything spawns: up to six tries (alternating sides) at a footing where at least three soldiers (or all of a smaller band) have a place to stand
			int side = forceSide != 0 ? forceSide : Main.rand.NextBool() ? 1 : -1; Point? foot = null; List<(int k, Point spot)> spots = null;
			for (int attempt = 0; attempt < 6 && spots == null; attempt++)
			{
				int sd = attempt % 2 == 0 ? side : -side;
				Point? f = FindFooting(p, sd, minTiles, maxTiles, eventFoot ? EventBands.OpenFooting : null); if (f == null) continue;
				var taken = new List<Point>(); var got = new List<(int, Point)>();
				for (int k = 0; k < n; k++) if (PlaceMember(f.Value, sd, k, taken, out Point sp)) { taken.Add(sp); got.Add((k, sp)); }
				if (got.Count >= Math.Min(n, 3)) { spots = got; foot = f; side = sd; }
			}
			if (spots == null) return false;
			int stage = type.Stage;
			BandBrain brain = null; int bandId = BandBrain.NextId();
			Vector2 anchor = new Vector2(foot.Value.X * 16f + 8f, foot.Value.Y * 16f - BandLevyNPC.BodyHeight * 0.5f);
			if (!legacy) brain = BandBrain.Create(type, stage, n, anchor, bandId, neutral);
			int made = 0, slotFull = 0, noSpot = n - spots.Count;
			foreach (var (k, spot) in spots)
			{
				int idx = k % type.Members.Length; // every role is fielded before any repeats
				int x = spot.X * 16 + 8, y = spot.Y * 16 - 1; // bottom centre of the body: standing on the floor tile
				if (legacy)
				{
					int lid = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), x, y, ModContent.NPCType<BandSoldier>());
					if (lid < 0 || lid >= Main.maxNPCs) { slotFull++; continue; }
					if (Collision.SolidCollision(Main.npc[lid].position, Main.npc[lid].width, Main.npc[lid].height)) { Main.npc[lid].active = false; continue; }
					(Main.npc[lid].ModNPC as BandSoldier)?.Setup(type, idx, stage, bandId);
					if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, lid);
					made++;
					continue;
				}
				int id = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), x, y, ModContent.NPCType<BandLevyNPC>());
				if (id < 0 || id >= Main.maxNPCs) { slotFull++; continue; }
				if (Collision.SolidCollision(Main.npc[id].position, Main.npc[id].width, Main.npc[id].height)) { Main.npc[id].active = false; continue; } // never leave a soldier inside a tile
				(Main.npc[id].ModNPC as BandLevyNPC)?.Setup(type, k, stage, bandId, n);
				Main.npc[id].direction = -side;
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, id);
				made++;
			}
			if (slotFull > 0) SlotFailures++;
			if (made == 0) { if (brain != null) BandBrain.All.Remove(bandId); return false; }
			if (brain != null) brain.StartSize = made;
			LastBrain = brain;
			BandsSpawned++;
			string line = Language.GetTextValue(isEvent ? "Mods.Warbands.Chat.EventArrive" : "Mods.Warbands.Chat.Arrive", type.DisplayName);
			if (Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(line), new Color(210, 150, 90));
			else Main.NewText(line, 210, 150, 90);
			Warbands.Instance.Logger.Info($"[Warbands] band #{bandId} '{type.Name}' stage {stage} size {made}{(legacy ? " (fighter AI)" : "")} at tile {foot.Value.X},{foot.Value.Y}{(neutral ? " (neutral)" : "")}{(noSpot > 0 ? $" ({noSpot} members found no standing room)" : "")}{(slotFull > 0 ? $" ({slotFull} members left out: NPC array full)" : "")}");
			return true;
		}

		/// <summary>The brain of the band the last successful SpawnBand formed (null for a legacy Fighter-AI band).</summary>
		public static BandBrain LastBrain;

		/// <summary>The summoning item was used: a band of that type forms 24 to 34 tiles from the player.</summary>
		public static bool Summon(Player p, int typeIdx)
		{
			var type = BandTypes.FromIndex(typeIdx); if (type == null || p == null || !p.active) return false;
			var cfg = WarbandsConfig.Get; if (cfg == null) return false;
			return SpawnBand(p, type, cfg.SummonedBandSize, 24, 34, false);
		}

		/// <summary>True when a soldier's whole body (20 x 42 px) can stand on floor tile (<paramref name="tx"/>, <paramref name="fy"/>): a solid, non-platform floor, no solid tile or liquid anywhere in the body box.</summary>
		public static bool BodyFits(int tx, int fy)
		{
			if (tx < 5 || tx >= Main.maxTilesX - 5 || fy < 8 || fy >= Main.maxTilesY - 8) return false;
			Tile floor = Main.tile[tx, fy];
			if (!floor.HasUnactuatedTile || !Main.tileSolid[floor.TileType] || Main.tileSolidTop[floor.TileType]) return false;
			var pos = new Vector2(tx * 16f + 8f - BandLevyNPC.BodyWidth * 0.5f, fy * 16f - BandLevyNPC.BodyHeight - 1f);
			return !Collision.SolidCollision(pos, BandLevyNPC.BodyWidth, BandLevyNPC.BodyHeight) && !Collision.WetCollision(pos, BandLevyNPC.BodyWidth, BandLevyNPC.BodyHeight);
		}

		/// <summary>The floor row in column <paramref name="x"/>, within <paramref name="up"/> rows above and <paramref name="down"/> below <paramref name="near"/>, where a body fits and that is closest to <paramref name="near"/>; -1 when none.</summary>
		public static int FloorAt(int x, int near, int up, int down)
		{
			int best = -1, bestD = int.MaxValue;
			for (int y = Math.Max(10, near - up); y <= Math.Min(Main.maxTilesY - 10, near + down); y++)
				if (BodyFits(x, y)) { int d = Math.Abs(y - near); if (d < bestD) { bestD = d; best = y; } }
			return best;
		}

		/// <summary>A standing spot <paramref name="minTiles"/> to <paramref name="maxTiles"/> tiles to one side of the player, as (column, floor row): a body fits there, and of the floors in the column the one nearest the player's own height is taken (so the band stands where the player can reach it, not on the topmost ledge).</summary>
		public static Point? FindFooting(Player p, int side, int minTiles, int maxTiles, Func<Point, bool> accept = null)
		{
			int px = (int)(p.Center.X / 16f), py = (int)(p.Bottom.Y / 16f);
			for (int attempt = 0; attempt < 18; attempt++)
			{
				int x = Math.Clamp(px + side * (minTiles + Main.rand.Next(Math.Max(1, maxTiles - minTiles + 1))), 10, Main.maxTilesX - 10);
				int fy = FloorAt(x, py, 25, 40);
				if (fy >= 0 && (accept == null || accept(new Point(x, fy)))) return new Point(x, fy);
			}
			return null;
		}

		/// <summary>The standing spot of member <paramref name="k"/>: two tiles further along the band's ground per member, on the floor nearest the band's own height; when the body does not fit there the nearest column that does is taken (outward, both ways), never within a tile of a member already placed. False when nothing near fits.</summary>
		public static bool PlaceMember(Point foot, int side, int k, List<Point> taken, out Point spot)
		{
			int want = foot.X + side * k * 2;
			for (int d = 0; d <= 8 + k; d++)
				for (int s = 0; s < (d == 0 ? 1 : 2); s++)
				{
					int x = want + (s == 0 ? d : -d);
					bool near = false; foreach (var t in taken) if (Math.Abs(t.X - x) < 2) { near = true; break; }
					if (near) continue;
					int fy = FloorAt(x, foot.Y, 8, 8);
					if (fy >= 0) { spot = new Point(x, fy); return true; }
				}
			spot = default; return false;
		}
	}
}
