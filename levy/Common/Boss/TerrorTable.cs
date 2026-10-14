using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using Levy.Common.Systems;
using Levy.Content.TileEntities;

namespace Levy.Common.Boss
{
	/// <summary>
	/// Terror lookups incl. trophy reductions. OWNED BY THE BOSS AGENT (see LevyConstants.Terror).
	/// A trophy (TileID.Painting3X3) or relic (TileID.MasterTrophyBase) of a boss within TrophyRadius tiles of a barracks
	/// lowers that boss's terror by TrophyTerrorReduction for soldiers based there. Scans are cached for TrophyCacheTicks.
	/// </summary>
	public static class TerrorTable
	{
		public static int BaseTerror(int npcType) => LevyConstants.Terror.TryGetValue(npcType, out var t) ? t : 5;

		/// <summary>Boss root type → (tile type, place style) of its trophy and relic, built from ContentSamples on first use.</summary>
		private static Dictionary<int, HashSet<(int tile, int style)>> _trophyStyles;
		private static readonly Dictionary<(int boss, int barracksId), (int tick, int value)> _byBarracks = new();
		private static readonly Dictionary<(int boss, int tx, int ty), (int tick, int value)> _byPoint = new();
		private static readonly Dictionary<int, (int tick, int value)> _anyBarracks = new();

		private static void EnsureBuilt()
		{
			if (_trophyStyles != null) return;
			var map = new Dictionary<int, HashSet<(int, int)>>();
			foreach (var kv in LevyConstants.BossTrophyItems)
			{
				var set = new HashSet<(int, int)>();
				foreach (int itemType in kv.Value)
				{
					if (!ContentSamples.ItemsByType.TryGetValue(itemType, out var item) || item == null || item.createTile < 0) continue;
					set.Add((item.createTile, item.placeStyle));
				}
				if (set.Count > 0) map[kv.Key] = set;
			}
			_trophyStyles = map;
		}

		private static bool Fresh((int tick, int value) entry, int tick) => tick >= entry.tick && tick - entry.tick < LevyConstants.TrophyCacheTicks;

		/// <summary>Reduction for a soldier whose home is this barracks (cached per boss and barracks id).</summary>
		public static int TrophyReduction(int bossRootType, BarracksTE barracks)
		{
			if (barracks == null) return 0;
			int tick = (int)Main.GameUpdateCount;
			var key = (bossRootType, barracks.ID);
			if (_byBarracks.TryGetValue(key, out var c) && Fresh(c, tick)) return c.value;
			int v = HasTrophyNear(bossRootType, barracks.Center) ? LevyConstants.TrophyTerrorReduction : 0;
			_byBarracks[key] = (tick, v);
			return v;
		}

		/// <summary>Reduction around an arbitrary world point (cached per boss and tile coordinate).</summary>
		public static int TrophyReduction(int bossRootType, Vector2 barracksCenter)
		{
			int tick = (int)Main.GameUpdateCount;
			var key = (bossRootType, (int)(barracksCenter.X / LevyConstants.TilePx), (int)(barracksCenter.Y / LevyConstants.TilePx));
			if (_byPoint.TryGetValue(key, out var c) && Fresh(c, tick)) return c.value;
			int v = HasTrophyNear(bossRootType, barracksCenter) ? LevyConstants.TrophyTerrorReduction : 0;
			_byPoint[key] = (tick, v);
			return v;
		}

		/// <summary>Reduction if ANY barracks in the world has the boss's trophy nearby (used when no soldier is known).</summary>
		public static int AnyBarracksTrophyReduction(int bossRootType)
		{
			int tick = (int)Main.GameUpdateCount;
			if (_anyBarracks.TryGetValue(bossRootType, out var c) && Fresh(c, tick)) return c.value;
			int v = 0;
			var settlements = SettlementSystem.Instance;
			if (settlements != null)
			{
				foreach (var b in settlements.AllBarracks())
				{
					if (TrophyReduction(bossRootType, b) > 0) { v = LevyConstants.TrophyTerrorReduction; break; }
				}
			}
			_anyBarracks[bossRootType] = (tick, v);
			return v;
		}

		/// <summary>True when a trophy or relic tile of the boss stands within TrophyRadius tiles of the point. Uncached.</summary>
		public static bool HasTrophyNear(int bossRootType, Vector2 center)
		{
			EnsureBuilt();
			if (!_trophyStyles.TryGetValue(bossRootType, out var styles)) return false;
			int cx = (int)(center.X / LevyConstants.TilePx), cy = (int)(center.Y / LevyConstants.TilePx), r = LevyConstants.TrophyRadius;
			int x0 = Math.Max(0, cx - r), x1 = Math.Min(Main.maxTilesX - 1, cx + r);
			int y0 = Math.Max(0, cy - r), y1 = Math.Min(Main.maxTilesY - 1, cy + r);
			for (int x = x0; x <= x1; x++)
			{
				for (int y = y0; y <= y1; y++)
				{
					Tile t = Main.tile[x, y];
					if (!t.HasTile) continue;
					int type = t.TileType;
					if (type != TileID.Painting3X3 && type != TileID.MasterTrophyBase) continue;
					// GetTileStyle divides frameX/frameY by the full style size, so any cell of the multitile yields its style;
					// existence is all we need, so the first matching cell answers the scan.
					int style;
					try { style = TileObjectData.GetTileStyle(t); }
					catch (ArgumentOutOfRangeException) { continue; }
					if (style >= 0 && styles.Contains((type, style))) return true;
				}
			}
			return false;
		}

		public static void ClearCaches()
		{
			_byBarracks.Clear(); _byPoint.Clear(); _anyBarracks.Clear();
		}
	}
}
