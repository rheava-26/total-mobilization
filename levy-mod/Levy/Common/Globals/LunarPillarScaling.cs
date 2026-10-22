using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Globals
{
	/// <summary>
	/// LUNAR PILLAR ARMY SCALING. The more command points (CP) of soldiers stand near a Celestial Pillar, the harder it gets:
	///  * SHIELD. Vanilla keeps one global counter per pillar (NPC.ShieldStrengthTowerSolar/Vortex/Nebula/Stardust, full at NPC.ShieldStrengthTowerMax = 100, 50 after Moon Lord).
	///    Every kill of one of its enemies spawns projectile 629, whose Kill takes 1 off that counter on the server (Projectile.cs, `type == 629`), and the pillar is
	///    invulnerable (dontTakeDamage, set each tick in NPC.AI) while the counter is not 0. We cannot change that one-per-kill step, so the pillar's PreAI watches the counter
	///    and gives part of each kill back: with E = LunarShieldKillsPerCp x CP extra kills wanted, a kill counts for Max / (Max + E) of a kill, so Max + E kills break the shield.
	///    The refund is a fractional accumulator (the counter is an int). The kill that takes the counter to 0 is never refunded, so the shield never flickers back on.
	///  * SPAWNS. Pillar enemies come from vanilla's natural spawner (NPC.SpawnNPC, `Main.rand.Next(spawnRate) == 0` per player in a Player.ZoneTower*); GlobalNPC.EditSpawnRate
	///    runs after vanilla's own clamps, so the spawn rate is multiplied by (1 + LunarSpawnRatePerCp x CP) there, and maxSpawns (the nearby-enemy cap, which would otherwise
	///    stop a faster spawner) by the same factor up to a cap.
	/// Server / single player only (shield, kills and spawns all run there; the counter is re-broadcast with UpdateTowerShieldStrengths). The CP near each pillar is re-measured
	/// every <see cref="LevyConstants.LunarCpRefreshTicks"/> ticks and clamped to <see cref="LevyConstants.LunarCpCap"/>.
	/// </summary>
	public class LunarPillarScaling : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => PillarIndex(entity.type) >= 0;

		/// <summary>Order of the cache arrays below.</summary>
		private static readonly int[] PillarTypes = { NPCID.LunarTowerSolar, NPCID.LunarTowerVortex, NPCID.LunarTowerNebula, NPCID.LunarTowerStardust };

		// ---- cached army measure (one slot per pillar) ----
		private static readonly int[] _cp = new int[4];
		private static readonly Vector2[] _center = new Vector2[4];
		private static readonly bool[] _present = new bool[4];
		private static uint _stamp = uint.MaxValue;

		// ---- per pillar-instance shield tracking ----
		/// <summary>The counter value seen last tick (-1 = not seen yet).</summary>
		private int _lastShield = -1;
		/// <summary>Fractional kills handed back but not yet returned as whole counter points.</summary>
		private float _owed;

		public static int PillarIndex(int npcType)
		{
			for (int i = 0; i < PillarTypes.Length; i++) if (PillarTypes[i] == npcType) return i;
			return -1;
		}

		private static int GetShield(int i) => i switch
		{
			0 => NPC.ShieldStrengthTowerSolar,
			1 => NPC.ShieldStrengthTowerVortex,
			2 => NPC.ShieldStrengthTowerNebula,
			_ => NPC.ShieldStrengthTowerStardust,
		};

		private static void SetShield(int i, int v)
		{
			switch (i)
			{
				case 0: NPC.ShieldStrengthTowerSolar = v; break;
				case 1: NPC.ShieldStrengthTowerVortex = v; break;
				case 2: NPC.ShieldStrengthTowerNebula = v; break;
				default: NPC.ShieldStrengthTowerStardust = v; break;
			}
		}

		private static bool PlayerInZone(Player p, int i) => i switch
		{
			0 => p.ZoneTowerSolar,
			1 => p.ZoneTowerVortex,
			2 => p.ZoneTowerNebula,
			_ => p.ZoneTowerStardust,
		};

		/// <summary>Re-measures (at most every LunarCpRefreshTicks): finds each live pillar, then adds up the CP of every living soldier body within the arena radius of it.</summary>
		private static void Refresh()
		{
			uint now = Main.GameUpdateCount;
			if (_stamp != uint.MaxValue && now - _stamp < (uint)LevyConstants.LunarCpRefreshTicks) return;
			_stamp = now;
			for (int i = 0; i < 4; i++) { _present[i] = false; _cp[i] = 0; }
			for (int n = 0; n < Main.maxNPCs; n++)
			{
				NPC npc = Main.npc[n];
				if (!npc.active) continue;
				int i = PillarIndex(npc.type);
				if (i < 0) continue;
				_present[i] = true; _center[i] = npc.Center;
			}
			bool any = false;
			for (int i = 0; i < 4; i++) any |= _present[i];
			if (!any) return;
			float r2 = LevyConstants.LunarArenaRadiusPx * LevyConstants.LunarArenaRadiusPx;
			for (int n = 0; n < Main.maxNPCs; n++)
			{
				NPC npc = Main.npc[n];
				if (!npc.active || npc.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive) continue;
				int cost = s.Record.CommandPointCost;
				for (int i = 0; i < 4; i++)
					if (_present[i] && Vector2.DistanceSquared(npc.Center, _center[i]) <= r2) _cp[i] += cost;
			}
			for (int i = 0; i < 4; i++) _cp[i] = Math.Clamp(_cp[i], 0, LevyConstants.LunarCpCap);
		}

		/// <summary>CP of soldiers near pillar <paramref name="i"/> (0 Solar, 1 Vortex, 2 Nebula, 3 Stardust), cached, clamped to LunarCpCap.</summary>
		public static int ArmyCp(int i)
		{
			if (i < 0 || i > 3) return 0;
			Refresh();
			return _cp[i];
		}

		/// <summary>Spawn-rate multiplier for an army of <paramref name="cp"/> CP: 1 + LunarSpawnRatePerCp x CP, capped.</summary>
		public static float SpawnMult(int cp) => Math.Clamp(1f + LevyConstants.LunarSpawnRatePerCp * Math.Max(0, cp), 1f, LevyConstants.LunarSpawnRateMultCap);

		public override bool PreAI(NPC npc)
		{
			if (!LevyUtil.IsServerOrSingle) return true;
			int i = PillarIndex(npc.type);
			if (i < 0) return true;
			int shield = GetShield(i);
			// first sight, or the counter went UP (event restart / world load): just resync
			if (_lastShield < 0 || shield > _lastShield) { _lastShield = shield; _owed = 0f; return true; }
			if (shield == _lastShield) return true;
			int dropped = _lastShield - shield;
			int max = Math.Max(1, NPC.ShieldStrengthTowerMax);
			int extra = LevyConstants.LunarShieldKillsPerCp * ArmyCp(i);
			if (shield > 0 && extra > 0)
			{
				// a kill counts for max / (max + extra) of a kill: the rest is given back, whole points at a time
				_owed += dropped * (1f - max / (float)(max + extra));
				int refund = Math.Min((int)_owed, dropped);
				if (refund > 0)
				{
					_owed -= refund;
					shield += refund; // <= _lastShield, so never above what it was
					SetShield(i, shield);
					if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.UpdateTowerShieldStrengths);
				}
			}
			else _owed = 0f; // broken (or no army near): nothing more to give back
			_lastShield = shield;
			return true;
		}

		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			float best = 1f;
			for (int i = 0; i < 4; i++)
				if (PlayerInZone(player, i)) best = Math.Max(best, SpawnMult(ArmyCp(i)));
			if (best <= 1f) return;
			spawnRate = Math.Max(1, (int)(spawnRate / best));
			maxSpawns = Math.Min(Math.Max(maxSpawns, (int)(maxSpawns * best)), Math.Max(maxSpawns, LevyConstants.LunarMaxSpawnsCap));
		}
	}
}
