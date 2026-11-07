using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Potions
{
	/// <summary>
	/// Which world tiles "are" a given item for the Seeker: the tile the item places (<c>createTile</c>: ores, Life Crystal, blocks, modded ores), the gem tiles whose drop is a gem (Sapphire ore, the exposed gem stones), and for modded
	/// tiles the item their drop table registered (<c>TileLoader.GetItemDropFromTypeAndStyle</c>, reliable for modded tiles only).
	/// Pure data, built from item and tile definitions only, so the boot self-check can run it with no world loaded.
	/// </summary>
	public sealed class SeekerMatcher
	{
		/// <summary>Gem item to (ore tile, exposed-gem-stone style on tile 178).</summary>
		public static readonly Dictionary<int, (int tile, int style)> Gems = new()
		{
			{ ItemID.Sapphire, (TileID.Sapphire, 2) }, { ItemID.Ruby, (TileID.Ruby, 4) }, { ItemID.Emerald, (TileID.Emerald, 3) },
			{ ItemID.Topaz, (TileID.Topaz, 1) }, { ItemID.Amethyst, (TileID.Amethyst, 0) }, { ItemID.Diamond, (TileID.Diamond, 5) },
			{ ItemID.Amber, (-1, 6) },
		};
		private const int ExposedGemTile = TileID.ExposedGems;

		public readonly int Item;
		private readonly bool[] _types;
		private readonly int _gemStyle = -1;

		private SeekerMatcher(int item)
		{
			Item = item;
			_types = new bool[TileLoader.TileCount];
			if (item <= 0 || item >= ItemLoader.ItemCount) return;
			var it = new Item(); it.SetDefaults(item);
			if (it.createTile >= 0 && it.createTile < _types.Length) _types[it.createTile] = true;
			if (Gems.TryGetValue(item, out var g)) { if (g.tile >= 0) _types[g.tile] = true; _gemStyle = g.style; }
			if (item == ItemID.LifeCrystal) _types[TileID.Heart] = true;
			for (int t = TileID.Count; t < _types.Length; t++) // modded tiles: the item their drop table registered
				if (TileLoader.GetItemDropFromTypeAndStyle(t) == item) _types[t] = true;
			_types[ExposedGemTile] = false; // gem stones are matched by style below
		}

		private static SeekerMatcher _cache;
		public static SeekerMatcher For(int item) => _cache != null && _cache.Item == item ? _cache : (_cache = new SeekerMatcher(item));
		public static void ClearCache() => _cache = null;

		public bool Matches(Tile t)
		{
			if (!t.HasTile) return false;
			int ty = t.TileType;
			if (ty == ExposedGemTile) return _gemStyle >= 0 && t.TileFrameX / 18 == _gemStyle;
			return ty < _types.Length && _types[ty];
		}

		/// <summary>True when this item matches at least one tile type (so a Seeker can ever light something for it).</summary>
		public bool MatchesTile(int tileType) => tileType == ExposedGemTile ? _gemStyle >= 0 : tileType >= 0 && tileType < _types.Length && _types[tileType];

		private static List<int> _fallback;

		/// <summary>Every item whose tile Spelunker highlights (ores, gems, Life Crystal, treasure), minus chests. Used when no armory can be reached.</summary>
		public static List<int> FallbackCandidates()
		{
			if (_fallback != null) return _fallback;
			var set = new SortedSet<int>(Gems.Keys) { ItemID.LifeCrystal };
			var it = new Item();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				it.SetDefaults(i);
				int ct = it.createTile;
				if (ct >= 0 && ct < Main.tileSpelunker.Length && Main.tileSpelunker[ct] && !TileID.Sets.BasicChest[ct]) set.Add(i);
			}
			return _fallback = set.ToList();
		}

		private static HashSet<int> _treasure;
		/// <summary>True for the ores, gems and Spelunker treasure an empty Seeker search lists.</summary>
		public static bool IsTreasure(int type) => (_treasure ??= new HashSet<int>(FallbackCandidates())).Contains(type);

		private static List<int> _all;

		/// <summary>Every real item type (deprecated ones left out): the Seeker can look for anything, in tiles and in chests.</summary>
		public static List<int> AllCandidates()
		{
			if (_all != null) return _all;
			var list = new List<int>();
			for (int i = 1; i < ItemLoader.ItemCount; i++) if (!ItemID.Sets.Deprecated[i]) list.Add(i);
			return _all = list;
		}

		public static void Unload() { _cache = null; _fallback = null; _all = null; _treasure = null; }
	}
}
