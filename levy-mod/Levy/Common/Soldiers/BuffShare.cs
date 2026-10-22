using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Flavor;
using Levy.Content.NPCs;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// Buff sharing (Guide to Firepower): a soldier passively inherits up to <see cref="GuideSystem.BuffShareSlots"/> of his commander's active buffs.
	/// Only buffs the soldier stat pipeline can actually use are shared (<see cref="Shareable"/>, the set <c>SoldierNPC.ApplyBuffEffect</c> understands), so debuffs,
	/// pets, light orbs and every buff with no soldier effect are skipped by construction. The set is built once per commander and refreshed every
	/// <see cref="LevyConstants.BuffShareRefreshTicks"/>; every soldier of that commander reads the same cached array during his once-a-second stat refresh.
	/// Server / single player only (clients never refresh soldier stats).
	/// </summary>
	public static class BuffShare
	{
		/// <summary>Potion buffs with a soldier-side effect. Keep in step with SoldierNPC.ApplyBuffEffect.</summary>
		public static readonly HashSet<int> Shareable = new()
		{
			BuffID.Ironskin, BuffID.Swiftness, BuffID.Wrath, BuffID.Rage, BuffID.Endurance, BuffID.Regeneration,
			BuffID.MagicPower, BuffID.ManaRegeneration, BuffID.AmmoReservation, BuffID.Archery, BuffID.Summoning,
		};

		private static readonly int[] None = new int[0];
		private static readonly int[][] _sets = new int[Main.maxPlayers][];
		private static readonly uint[] _stamps = new uint[Main.maxPlayers];

		/// <summary>Drops every cached set (a world loads or unloads).</summary>
		internal static void Reset()
		{
			for (int i = 0; i < _sets.Length; i++) { _sets[i] = null; _stamps[i] = 0; }
		}

		/// <summary>The buffs this soldier inherits right now (never null; empty with no guide, no commander in reach, or nothing shareable).</summary>
		public static int[] For(SoldierNPC soldier)
		{
			int n = GuideSystem.BuffShareSlots;
			if (n <= 0 || soldier?.Record == null) return None;
			Player source = soldier.Commander;
			if (source == null && soldier.Record.Kind == SoldierKind.Militia) source = NearestPlayer(soldier.NPC.Center);
			if (source == null || !source.active || source.dead) return None;
			return SetOf(source, n);
		}

		private static int[] SetOf(Player p, int n)
		{
			uint now = Main.GameUpdateCount;
			int who = p.whoAmI;
			if (who < 0 || who >= _sets.Length) return Collect(p, n);
			if (_sets[who] != null && now - _stamps[who] < (uint)LevyConstants.BuffShareRefreshTicks) return _sets[who];
			_stamps[who] = now;
			return _sets[who] = Collect(p, n);
		}

		/// <summary>The first <paramref name="n"/> distinct shareable, running, non-harmful buffs in the player's buff order.</summary>
		private static int[] Collect(Player p, int n)
		{
			List<int> picked = null;
			for (int i = 0; i < p.buffType.Length && (picked == null || picked.Count < n); i++)
			{
				int t = p.buffType[i];
				if (t <= 0 || p.buffTime[i] <= 0 || !Shareable.Contains(t) || Main.debuff[t]) continue;
				picked ??= new List<int>(n);
				if (!picked.Contains(t)) picked.Add(t);
			}
			return picked == null ? None : picked.ToArray();
		}

		/// <summary>A militia soldier has no commander: the nearest living player within <see cref="LevyConstants.BuffShareMilitiaRadiusTiles"/>, or null.</summary>
		private static Player NearestPlayer(Vector2 pos)
		{
			float best = LevyConstants.BuffShareMilitiaRadiusTiles * LevyConstants.TilePx; best *= best;
			Player found = null;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (!p.active || p.dead) continue;
				float d = Vector2.DistanceSquared(p.Center, pos);
				if (d <= best) { best = d; found = p; }
			}
			return found;
		}
	}

	/// <summary>Clears the shared-buff cache with the world.</summary>
	public class BuffShareSystem : ModSystem
	{
		public override void OnWorldLoad() => BuffShare.Reset();
		public override void OnWorldUnload() => BuffShare.Reset();
	}
}
