using System;
using Terraria;
using Terraria.ModLoader;

namespace Warbands.Common
{
	/// <summary>
	/// Reads the Calamity Sunken Sea zone without referencing Calamity: the ModBiome is found by name among the loaded content the first time it is asked for. With Calamity absent every answer is false.
	/// A player counts as inside through tModLoader's own mod-biome flags (the same flags Calamity's music and spawn rules read, kept for every player by the server).
	/// </summary>
	public static class CalamityZones
	{
		private static ModBiome _sunken; private static bool _looked;
		/// <summary>Self-check hook: pretend every player is in the Sunken Sea.</summary>
		public static bool TestForce;
		/// <summary>Headless sampler: stand-in players carry no mod-biome flags, so ask the biome directly.</summary>
		public static bool ScannedProbe;

		/// <summary>The Sunken Sea ModBiome, or null when Calamity is not loaded.</summary>
		public static ModBiome SunkenSea
		{
			get
			{
				if (!_looked)
				{
					_looked = true;
					try
					{
						foreach (var b in ModContent.GetContent<ModBiome>())
							if (b.Mod != null && b.Mod.Name == "CalamityMod" && b.Name.IndexOf("SunkenSea", StringComparison.OrdinalIgnoreCase) >= 0) { _sunken = b; break; }
					}
					catch { }
				}
				return _sunken;
			}
		}

		/// <summary>True when <paramref name="p"/> is in the Sunken Sea. <paramref name="scanned"/>: a headless stand-in player whose scene was just scanned, so the biome is asked directly instead of through the player's flags.</summary>
		public static bool InSunkenSea(Player p, bool scanned = false)
		{
			if (TestForce) return true;
			var b = SunkenSea; if (b == null || p == null) return false;
			try
			{
				// a player whose biome flags were sized before every mod's biomes were counted (seen with many biome mods loaded) has no flag for this biome: ask the biome itself instead of reading past the end
				if (!HasFlag(p, b)) return b.IsBiomeActive(p);
				return p.InModBiome(b) || (scanned || ScannedProbe) && b.IsBiomeActive(p);
			}
			catch { return false; }
		}

		private static System.Reflection.FieldInfo _flags;
		/// <summary>True when the player's mod-biome flag array reaches this biome's index (tModLoader keeps the array internal: found once through reflection).</summary>
		internal static bool HasFlag(Player p, ModBiome b)
		{
			_flags ??= typeof(Player).GetField("modBiomeFlags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (_flags == null) return true;
			return _flags.GetValue(p) is System.Collections.BitArray a && b.Type < a.Length;
		}

		public static void Reset() { _looked = false; _sunken = null; }
	}
}
