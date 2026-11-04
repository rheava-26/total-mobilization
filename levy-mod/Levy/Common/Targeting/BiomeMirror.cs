using Microsoft.Xna.Framework;
using Terraria;


namespace Levy.Common.Targeting
{
	/// <summary>
	/// A target-body stand-in (the free-standing dummy Player swapped into Main.player[255]) is "in the same biome as the player it serves", so a boss whose AI
	/// checks its target's biome (Brain of Cthulhu ZoneCrimson, Plantera ZoneJungle, ...) does not see a target outside its home and flee.
	/// Copies, with no tile scans: the five vanilla zone bit-fields zone1..zone5 (every vanilla ZoneXxx property is a bit in these), the plain height-zone bools,
	/// and the tModLoader per-player mod-biome bit array (modBiomeFlags, what Player.InModBiome reads). Per-mod ModPlayer fields are not copied.
	/// </summary>
	public static class BiomeMirror
	{
		/// <summary>Player.modBiomeFlags (internal in tModLoader, a BitArray indexed by ModBiome.ZeroIndexType) — the storage behind Player.InModBiome.</summary>
		public static readonly System.Reflection.FieldInfo FlagsField = typeof(Player).GetField("modBiomeFlags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

		/// <summary>Copy the biome state of player <paramref name="commanderIndex"/> (else the nearest active real player to <paramref name="pos"/>) onto the dummy. No live player: unchanged.</summary>
		public static void Apply(Player dummy, int commanderIndex, Vector2 pos)
		{
			Player src = null;
			if (commanderIndex >= 0 && commanderIndex < Main.maxPlayers && Main.player[commanderIndex].active && !Main.player[commanderIndex].dead) src = Main.player[commanderIndex];
			if (src == null)
			{
				float best = float.MaxValue;
				for (int i = 0; i < Main.maxPlayers; i++)
				{
					Player p = Main.player[i];
					if (p == null || !p.active || p.dead || p.ghost) continue;
					float d = Vector2.DistanceSquared(p.Center, pos);
					if (d < best) { best = d; src = p; }
				}
			}
			if (src != null && !ReferenceEquals(src, dummy)) Copy(src, dummy);
		}

		public static void Copy(Player src, Player dst)
		{
			dst.zone1 = src.zone1; dst.zone2 = src.zone2; dst.zone3 = src.zone3; dst.zone4 = src.zone4; dst.zone5 = src.zone5;
			dst.ZoneSkyHeight = src.ZoneSkyHeight; dst.ZoneOverworldHeight = src.ZoneOverworldHeight; dst.ZoneDirtLayerHeight = src.ZoneDirtLayerHeight;
			dst.ZoneRockLayerHeight = src.ZoneRockLayerHeight; dst.ZoneUnderworldHeight = src.ZoneUnderworldHeight;
			if (FlagsField != null)
			{
				var a = FlagsField.GetValue(src) as System.Collections.BitArray;
				if (a == null) return;
				var b = FlagsField.GetValue(dst) as System.Collections.BitArray;
				if (b == null || b.Length != a.Length) FlagsField.SetValue(dst, new System.Collections.BitArray(a));
				else for (int i = 0; i < a.Length; i++) b[i] = a[i];
			}
		}
	}
}
