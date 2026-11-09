using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Common.Events
{
	/// <summary>
	/// Events push back against the army, the enemy-side answer to a big levy count. While a vanilla event runs (an invasion, Blood Moon, Eclipse, Pumpkin Moon, Frost Moon, the Old One's Army) enemies
	/// spawn faster and more of them stand at once, and an invasion begins larger, so its reinforcement waves keep coming. The numbers are <see cref="LevyConstants"/> and grow with the army's command points.
	/// </summary>
	public static class EventPressure
	{
		/// <summary>Spawn rate multiplier (enemies spawn this many times as often) for an army of <paramref name="cp"/> command points: base plus per-CP, capped. Vanilla is 1.</summary>
		public static float SpawnRateMult(int cp) => Math.Min(LevyConstants.EventSpawnRateCap, LevyConstants.EventSpawnRateBase + LevyConstants.EventSpawnRatePerCp * Math.Max(0, cp));

		/// <summary>How many enemies may stand near a player at once: the vanilla cap times the spawn multiplier's share, held under <see cref="LevyConstants.EventMaxSpawnsCap"/> (and never below the vanilla cap).</summary>
		public static int MaxSpawns(int vanillaMax, float rateMult) => Math.Max(vanillaMax, Math.Min((int)(vanillaMax * rateMult * LevyConstants.EventMaxSpawnsShare), Math.Max(vanillaMax, LevyConstants.EventMaxSpawnsCap)));

		/// <summary>The spawn rate value (one in N chance per tick) after the multiplier; at least 1.</summary>
		public static int SpawnRate(int vanillaRate, float rateMult) => Math.Max(1, (int)(vanillaRate / Math.Max(1f, rateMult)));

		/// <summary>An invasion's size multiplier (its reinforcement waves) for an army of <paramref name="cp"/> command points: base plus per-CP, capped. Vanilla is 1.</summary>
		public static float InvasionSizeMult(int cp) => Math.Min(LevyConstants.InvasionSizeCap, LevyConstants.InvasionSizeBase + LevyConstants.InvasionSizePerCp * Math.Max(0, cp));

		/// <summary>The event-active rule: the militia's own list of events (<see cref="LevyUtil.IsEventActive"/>).</summary>
		public static bool Active => LevyUtil.IsEventActive();

		private static uint _cpStamp = uint.MaxValue; private static int _cp;
		/// <summary>The army's command points in the field, re-counted at most every <see cref="LevyConstants.EventCpRefreshTicks"/> ticks.</summary>
		public static int ArmyCp
		{
			get
			{
				uint now = Main.GameUpdateCount;
				if (_cpStamp != uint.MaxValue && now - _cpStamp < (uint)LevyConstants.EventCpRefreshTicks) return _cp;
				_cpStamp = now;
				_cp = RosterSystem.Instance?.CommandPointsUsed ?? 0;
				return _cp;
			}
		}
	}

	/// <summary>Raises the spawn rate and the nearby-enemy cap of every player while a vanilla event is running.</summary>
	public class EventSpawnPressure : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (!LevyUtil.IsServerOrSingle || !EventPressure.Active) return;
			float mult = EventPressure.SpawnRateMult(EventPressure.ArmyCp);
			spawnRate = EventPressure.SpawnRate(spawnRate, mult);
			maxSpawns = EventPressure.MaxSpawns(maxSpawns, mult);
		}
	}

	/// <summary>An invasion that begins gets bigger: its size (and the size its progress bar counts from) is multiplied once, at the start, so the reinforcement waves behind the first one are many times the vanilla count.</summary>
	public class InvasionReinforcements : ModSystem
	{
		private int _lastType;
		private bool _primed;

		public override void OnWorldLoad() => _primed = false;
		public override void OnWorldUnload() => _primed = false;

		public override void PostUpdateInvasions()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			int type = Main.invasionType > 0 && Main.invasionSize > 0 ? Main.invasionType : 0;
			if (!_primed) { _lastType = type; _primed = true; return; } // an invasion already under way when the world loads keeps its size
			if (type != 0 && type != _lastType && Main.invasionSizeStart > 0 && Main.invasionSize >= Main.invasionSizeStart)
			{
				float mult = EventPressure.InvasionSizeMult(EventPressure.ArmyCp);
				Main.invasionSize = (int)(Main.invasionSize * mult);
				Main.invasionSizeStart = Main.invasionSize;
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
			}
			_lastType = type;
		}
	}
}
