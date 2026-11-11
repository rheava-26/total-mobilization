using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Budget
{
	/// <summary>Who owns an entity, for the census and the budget.</summary>
	public enum CrowdKind { ArmyShot, ArmyMinion, ArmySentry, Drone, Enemy, Player, PlayerMinion, Other }

	/// <summary>
	/// THE ARMY ENTITY BUDGET. Terraria has 1000 projectile slots and 200 NPC slots for everything. An army of dozens of levies, each keeping minions and towers out, plus militia and drones, can take them all: the army's own
	/// shots fail to spawn, enemies cannot spawn, and the player's weapons fire nothing. The budget keeps the game's free slots at least <see cref="ReservedProj"/> projectiles and <see cref="ReservedNpc"/> NPCs:
	///  * <see cref="MayAddProjectiles"/> / <see cref="MayAddNpc"/> are asked by the upkeep before it casts a minion, plants a tower or launches a drone (with a margin, so it does not thrash);
	///  * when the free projectile slots fall under the reserve, <see cref="Plan"/> picks the army's persistent entities (minions and towers) to retire: the lowest-value first (a soldier's surplus towers, then surplus minions,
	///    then his only tower, then his only minion), oldest first inside a value. The player's and the enemy's entities are never candidates, and lingering shots are bounded by <see cref="PersistentShotBudget"/> against
	///    the same ceiling.
	/// Server / single player only. The census (<see cref="Snapshot"/>) runs every 15 ticks and logs one [Crowd] line every 2 s under env LEVY_CROWD=1.
	/// </summary>
	public static class CrowdBudget
	{
		/// <summary>Extra free slots required before the upkeep may add again once the budget has had to retire something (anti-thrash).</summary>
		public const int Hysteresis = 30;
		public const int CensusTicks = 15;
		public const int MaxRetiresPerPass = 60;

		/// <summary>Lab switch: LEVY_CROWD_BUDGET=0 runs the army without the budget (before/after numbers).</summary>
		internal static readonly bool LabOff = Environment.GetEnvironmentVariable("LEVY_CROWD_BUDGET") == "0";

		public static bool On { get { if (LabOff) return false; try { return LevyConfig.Get?.ArmyEntityBudget ?? true; } catch (Exception) { return true; } } }
		/// <summary>LEVY_CROWD_RESERVE=n (lab): overrides the projectile reserve so the budget can be exercised at a modest army size.</summary>
		private static readonly int LabReserve = int.TryParse(Environment.GetEnvironmentVariable("LEVY_CROWD_RESERVE"), out int lr) ? lr : -1;
		public static int ReservedProj { get { if (LabReserve >= 0) return LabReserve; try { return LevyConfig.Get?.ReservedProjectileSlots ?? 150; } catch (Exception) { return 150; } } }
		public static int ReservedNpc { get { try { return LevyConfig.Get?.ReservedNpcSlots ?? 40; } catch (Exception) { return 40; } } }

		/// <summary>Counts from the last census.</summary>
		public static int FreeProj = Main.maxProjectiles, FreeNpc = Main.maxNPCs;
		/// <summary>Lab counters.</summary>
		public static int RetiredMinions, RetiredSentries, RetiredDrones, GatedCasts, GatedDrones, Passes;

		/// <summary>True when the army may add <paramref name="n"/> persistent projectiles (a minion cast, a tower) without taking the game under its reserve. The free count is read live, so a burst inside one census cannot overshoot.</summary>
		public static bool MayAddProjectiles(int n = 1)
		{
			if (!On) return true;
			bool ok = Allows(LiveFreeProjectiles(), n, ReservedProj, Retiring);
			if (!ok) GatedCasts++;
			return ok;
		}

		/// <summary>Pure rule behind <see cref="MayAddProjectiles"/>: adding <paramref name="n"/> must leave at least the reserve free (plus the hysteresis while the budget has been retiring).</summary>
		public static bool Allows(int freeNow, int n, int reserve, bool retiring) => freeNow - n >= reserve + (retiring ? Hysteresis : 0);

		/// <summary>True when a drone may take another NPC slot: the free NPC slots stay at least the reserve.</summary>
		public static bool MayAddNpc()
		{
			if (!On) return true;
			bool ok = LiveFreeNpcs() - 1 >= ReservedNpc;
			if (!ok) GatedDrones++;
			return ok;
		}

		/// <summary>NPC slots over the reserve to spare is negative when the army's drones should stand down.</summary>
		public static int NpcOver => On ? ReservedNpc - LiveFreeNpcs() : 0;

		/// <summary>True after a retire pass until the free slots recover past reserve + hysteresis.</summary>
		internal static bool Retiring;

		private static uint _freeStamp = uint.MaxValue; private static int _freeP, _freeN;
		private static void Live() { uint now = Main.GameUpdateCount; if (_freeStamp == now) return; _freeStamp = now; int p = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) p++; int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) n++; _freeP = Main.maxProjectiles - p; _freeN = Main.maxNPCs - n; }
		/// <summary>Free slots counted once per game tick (a scan of both arrays), then adjusted by every add in the tick.</summary>
		public static int LiveFreeProjectiles() { Live(); return _freeP; }
		public static int LiveFreeNpcs() { Live(); return _freeN; }
		/// <summary>Tells the tick cache that the army just took a slot (a cast made this tick).</summary>
		public static void NoteAdded(int projectiles = 1) { Live(); _freeP -= projectiles; }

		// ------------------------------------------------------------------------------------------------ the plan (pure)
		public struct Cand { public int Who; public int Soldier; public bool Sentry; public uint Born; }

		/// <summary>The value of one candidate: lower retires first. 0 a soldier's surplus tower, 1 his surplus minion, 2 his only tower, 3 his only minion.</summary>
		private static int Level(bool sentry, int rankInKind) => sentry ? (rankInKind > 0 ? 0 : 2) : (rankInKind > 0 ? 1 : 3);

		/// <summary>Chooses up to <paramref name="need"/> candidates to retire into <paramref name="retire"/> (whoAmI values). Within a soldier the OLDEST of a kind is rank 0 (kept longest as his "only" one is the newest, see below),
		/// so surplus = every one past the first; candidates are then taken lowest value first, oldest first.</summary>
		public static void Plan(List<Cand> cands, int need, List<int> retire)
		{
			retire.Clear();
			if (need <= 0 || cands.Count == 0) return;
			// rank inside (soldier, kind): newest = 0 (the one that is kept longest is the newest cast), older ones are the surplus
			var byKey = new Dictionary<long, List<int>>();
			for (int i = 0; i < cands.Count; i++)
			{
				long k = ((long)cands[i].Soldier << 1) | (cands[i].Sentry ? 1L : 0L);
				if (!byKey.TryGetValue(k, out var l)) byKey[k] = l = new List<int>();
				l.Add(i);
			}
			var order = new List<(int level, uint born, int who)>(cands.Count);
			foreach (var kv in byKey)
			{
				var l = kv.Value;
				l.Sort((a, b) => cands[b].Born.CompareTo(cands[a].Born)); // newest first
				for (int r = 0; r < l.Count; r++) { var c = cands[l[r]]; order.Add((Level(c.Sentry, r), c.Born, c.Who)); }
			}
			order.Sort((a, b) => { int d = a.level.CompareTo(b.level); if (d != 0) return d; d = a.born.CompareTo(b.born); return d != 0 ? d : a.who.CompareTo(b.who); });
			for (int i = 0; i < order.Count && retire.Count < need && retire.Count < MaxRetiresPerPass; i++) retire.Add(order[i].who);
		}

		// ------------------------------------------------------------------------------------------------ the census
		public sealed class Counts
		{
			public int Npc, Levy, Militia, DroneNpc, Enemy, Town, OtherNpc;
			public int Proj, ArmyShot, Lingering, ArmyMinion, ArmySentry, DroneProj, Enemies, Player, PlayerMinion, OtherProj;
			public int Dust, Gore;
			public readonly Dictionary<string, int> Top = new();
			public void Clear() { Npc = Levy = Militia = DroneNpc = Enemy = Town = OtherNpc = Proj = ArmyShot = Lingering = ArmyMinion = ArmySentry = DroneProj = Enemies = Player = PlayerMinion = OtherProj = Dust = Gore = 0; Top.Clear(); }
		}
		public static readonly Counts Last = new();
		/// <summary>Peak values seen since the last reset (lab report).</summary>
		public static readonly Counts Peak = new();
		private static readonly List<Cand> _cands = new(256);
		private static readonly List<int> _retire = new(64);

		/// <summary>Where a projectile counts. Pure over the projectile's fields.</summary>
		public static CrowdKind KindOf(Projectile p)
		{
			bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex >= 0;
			if (tagged) return p.sentry ? CrowdKind.ArmySentry : p.minion ? CrowdKind.ArmyMinion : CrowdKind.ArmyShot;
			if (p.hostile) return CrowdKind.Enemy;
			if (p.ModProjectile?.Mod?.Name == "Mechanization" || (t != null && t.DroneShot)) return CrowdKind.Drone;
			if (p.owner >= 0 && p.owner < Main.maxPlayers - 1 && Main.player[p.owner].active) return p.minion || p.sentry ? CrowdKind.PlayerMinion : CrowdKind.Player;
			return CrowdKind.Other;
		}

		/// <summary>One pass over NPCs and projectiles: fills <see cref="Last"/>, the retire candidates, and (when asked) the top projectile types.</summary>
		public static void Snapshot(bool withTop)
		{
			var c = Last; c.Clear(); _cands.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) continue;
				c.Npc++;
				if (n.ModNPC is SoldierNPC sn) { if (sn.Hostile) c.Enemy++; else if (sn.IsMilitia) c.Militia++; else c.Levy++; }
				else if (n.ModNPC != null && n.ModNPC.Mod.Name == "Mechanization") c.DroneNpc++;
				else if (n.townNPC) c.Town++;
				else if (!n.friendly) c.Enemy++;
				else c.OtherNpc++;
			}
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active) continue;
				c.Proj++;
				CrowdKind k = KindOf(p);
				switch (k)
				{
					case CrowdKind.ArmyShot: c.ArmyShot++; if (p.TryGetGlobalProjectile(out PersistentShot ps) && ps.Registered && Main.GameUpdateCount - ps.Born >= PersistentShot.LingerTicks) c.Lingering++; break;
					case CrowdKind.ArmyMinion:
					case CrowdKind.ArmySentry:
						if (k == CrowdKind.ArmyMinion) c.ArmyMinion++; else c.ArmySentry++;
						var tag = p.GetGlobalProjectile<SoldierProjectileTag>();
						if (!tag.Dismissed) _cands.Add(new Cand { Who = i, Soldier = tag.SoldierIndex, Sentry = k == CrowdKind.ArmySentry, Born = tag.SpawnTick });
						break;
					case CrowdKind.Drone: c.DroneProj++; break;
					case CrowdKind.Enemy: c.Enemies++; break;
					case CrowdKind.Player: c.Player++; break;
					case CrowdKind.PlayerMinion: c.PlayerMinion++; break;
					default: c.OtherProj++; break;
				}
				if (withTop && k != CrowdKind.ArmyShot)
				{
					string key = k + ":" + (p.ModProjectile?.FullName ?? ("vanilla" + p.type));
					c.Top.TryGetValue(key, out int n0); c.Top[key] = n0 + 1;
				}
			}
			FreeProj = Main.maxProjectiles - c.Proj; FreeNpc = Main.maxNPCs - c.Npc;
			if (withTop)
			{
				for (int i = 0; i < Main.maxDust; i++) if (Main.dust[i].active) c.Dust++;
				for (int i = 0; i < Main.maxGore; i++) if (Main.gore[i].active) c.Gore++;
			}
			var pk = Peak;
			pk.Npc = Math.Max(pk.Npc, c.Npc); pk.Levy = Math.Max(pk.Levy, c.Levy); pk.Militia = Math.Max(pk.Militia, c.Militia); pk.DroneNpc = Math.Max(pk.DroneNpc, c.DroneNpc); pk.Enemy = Math.Max(pk.Enemy, c.Enemy);
			pk.Proj = Math.Max(pk.Proj, c.Proj); pk.ArmyShot = Math.Max(pk.ArmyShot, c.ArmyShot); pk.Lingering = Math.Max(pk.Lingering, c.Lingering); pk.ArmyMinion = Math.Max(pk.ArmyMinion, c.ArmyMinion); pk.ArmySentry = Math.Max(pk.ArmySentry, c.ArmySentry);
			pk.DroneProj = Math.Max(pk.DroneProj, c.DroneProj); pk.Enemies = Math.Max(pk.Enemies, c.Enemies); pk.Player = Math.Max(pk.Player, c.Player); pk.PlayerMinion = Math.Max(pk.PlayerMinion, c.PlayerMinion); pk.OtherProj = Math.Max(pk.OtherProj, c.OtherProj);
			pk.Dust = Math.Max(pk.Dust, c.Dust); pk.Gore = Math.Max(pk.Gore, c.Gore);
		}

		/// <summary>Retires army minions and towers until the free projectile slots are back over the reserve. Returns how many went.</summary>
		public static int Enforce()
		{
			Passes++;
			int reserve = ReservedProj;
			int free = FreeProj;
			if (free >= reserve + Hysteresis) { Retiring = false; return 0; }
			if (free >= reserve) return 0; // inside the margin: nothing new is cast (MayAddProjectiles), nothing is retired
			Retiring = true;
			Plan(_cands, reserve - free + 10, _retire);
			int n = 0;
			foreach (int who in _retire)
			{
				Projectile p = Main.projectile[who];
				if (!p.active) continue;
				if (p.sentry) RetiredSentries++; else RetiredMinions++;
				if (p.minion) SoldierMinions.Dismiss(p); else p.Kill();
				n++;
			}
			return n;
		}

		/// <summary>Drones past the NPC reserve: how many should stand down now (the caller recalls its oldest).</summary>
		public static void NoteRetiredDrone() => RetiredDrones++;
		/// <summary>A drone launch was refused for want of free NPC slots (counted for the census line).</summary>
		public static void NoteDroneGate() => GatedDrones++;

		internal static string Describe(Counts c)
		{
			var sb = new StringBuilder();
			sb.Append($"npc {c.Npc}/{Main.maxNPCs} (levy {c.Levy} militia {c.Militia} drone {c.DroneNpc} enemy {c.Enemy} town {c.Town} other {c.OtherNpc}) ");
			sb.Append($"proj {c.Proj}/{Main.maxProjectiles} (armyShot {c.ArmyShot} lingering {c.Lingering} minion {c.ArmyMinion} sentry {c.ArmySentry} drone {c.DroneProj} enemy {c.Enemies} player {c.Player} playerMinion {c.PlayerMinion} other {c.OtherProj}) ");
			sb.Append($"free proj {Main.maxProjectiles - c.Proj} npc {Main.maxNPCs - c.Npc} dust {c.Dust} gore {c.Gore}");
			return sb.ToString();
		}
	}

	/// <summary>Runs the census and the budget once every <see cref="CrowdBudget.CensusTicks"/> ticks, and the [Crowd] log and timing under env LEVY_CROWD=1.</summary>
	public class CrowdSystem : ModSystem
	{
		/// <summary>LEVY_CROWD=1: log a [Crowd] census every 2 s (lab / diagnosis only).</summary>
		internal static readonly bool Log = Environment.GetEnvironmentVariable("LEVY_CROWD") == "1";
		private static readonly Stopwatch _tick = new(), _npc = new(), _proj = new();
		private static double _tickMs, _npcMs, _projMs, _tickMax; private static int _tickN;
		private static uint _lastLog;

		public override void OnWorldLoad() { ResetCounters(); }
		public override void OnWorldUnload() { ResetCounters(); }
		internal static void ResetCounters() { CrowdBudget.Peak.Clear(); CrowdBudget.RetiredMinions = CrowdBudget.RetiredSentries = CrowdBudget.RetiredDrones = CrowdBudget.GatedCasts = CrowdBudget.GatedDrones = CrowdBudget.Passes = 0; CrowdBudget.Retiring = false; _tickMs = _npcMs = _projMs = _tickMax = 0; _tickN = 0; }

		public override void PreUpdateEntities() { if (Log) _tick.Restart(); }
		public override void PreUpdateNPCs() { if (Log) _npc.Restart(); }
		public override void PostUpdateNPCs() { if (Log) _npc.Stop(); }
		public override void PreUpdateProjectiles() { if (Log) _proj.Restart(); }
		public override void PostUpdateProjectiles() { if (Log) _proj.Stop(); }

		public override void PostUpdateEverything()
		{
			if (!LevyUtil.IsServerOrSingle || Main.gameMenu) return;
			if (Log)
			{
				_tick.Stop(); double t = _tick.Elapsed.TotalMilliseconds; _tickMs += t; _npcMs += _npc.Elapsed.TotalMilliseconds; _projMs += _proj.Elapsed.TotalMilliseconds; _tickN++; if (t > _tickMax) _tickMax = t;
			}
			uint now = Main.GameUpdateCount;
			bool logNow = Log && now - _lastLog >= 120;
			if (now % (uint)CrowdBudget.CensusTicks != 0 && !logNow) return;
			CrowdBudget.Snapshot(logNow);
			if (CrowdBudget.On) CrowdBudget.Enforce();
			if (logNow)
			{
				_lastLog = now;
				var c = CrowdBudget.Last;
				var top = new List<KeyValuePair<string, int>>(c.Top); top.Sort((a, b) => b.Value.CompareTo(a.Value));
				var sb = new StringBuilder(); for (int i = 0; i < top.Count && i < 8; i++) sb.Append(i > 0 ? ", " : "").Append(top[i].Key).Append('=').Append(top[i].Value);
				double n = Math.Max(1, _tickN);
				Levy.Instance?.Logger.Info($"[Crowd] census tick={now} {CrowdBudget.Describe(c)} | budget on={CrowdBudget.On} retired minions={CrowdBudget.RetiredMinions} sentries={CrowdBudget.RetiredSentries} drones={CrowdBudget.RetiredDrones} gatedCasts={CrowdBudget.GatedCasts} gatedDrones={CrowdBudget.GatedDrones} | ms/tick entities avg={_tickMs / n:0.00} max={_tickMax:0.0} npcs={_npcMs / n:0.00} projectiles={_projMs / n:0.00} | top {sb}");
				_tickMs = _npcMs = _projMs = _tickMax = 0; _tickN = 0;
			}
		}
	}
}
