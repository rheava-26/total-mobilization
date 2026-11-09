using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Common.Globals
{
	/// <summary>
	/// PERSISTENT-SHOT BUDGET. Terraria has 1000 projectile slots for everything (the commander's, every enemy's, every levy's). A weapon whose shots linger instead of flying off and dying (Calamity's Firestorm Cannon
	/// puts out ~11 flares a second that each live five seconds, stick, and keep hitting) is harmless for one shooter and eats the slots with twenty: the army's own arrows, bolts and rockets then fail to spawn.
	/// A soldier's shot that is still alive after <see cref="LingerTicks"/> is a lingering one. It is registered, and when the board is crowded the OLDEST lingering shots are retired (their own Kill, so they end as they
	/// would at the end of their life) until it is not: oldest first means the ones closest to expiring, so the weapon keeps its full output. Two limits, both measured on the live board:
	///  * a single soldier never keeps more than <see cref="PerSoldierCap"/> lingering shots (what one shooter holding the trigger naturally has);
	///  * lingering soldier shots together never pass <see cref="ArmyCap"/>, and the whole board is never taken past <see cref="BoardCeiling"/>.
	/// Minions, sentries, held-weapon chains and driven minions are not shots and are never touched. Server / single player only.
	/// </summary>
	public class PersistentShot : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		internal const int LingerTicks = 150, PerSoldierCap = 40, ArmyCap = 330, BoardCeiling = 600;
		internal uint Born, Serial; internal int Soldier = -1; internal bool Registered;

		public override void PostAI(Projectile p)
		{
			if (Registered || !CombatFix.On(CombatFix.Budget) || !LevyUtil.IsServerOrSingle || !p.friendly || p.minion || p.sentry || p.hostile) return;
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.Cast || tag.Dismissed) return;
			if (p.TryGetGlobalProjectile(out HeldWeaponProjectile hw) && hw.Chain) return;
			Registered = true; Born = Main.GameUpdateCount; Soldier = tag.SoldierIndex; Serial = ++PersistentShotBudget.NextSerial;
			PersistentShotBudget.Add(p.whoAmI, Serial);
		}

		public override void OnKill(Projectile p, int timeLeft) { Serial = 0; }
	}

	public class PersistentShotBudget : ModSystem
	{
		internal static uint NextSerial;
		private struct Entry { public int Who; public uint Serial; }
		private static readonly List<Entry> _live = new(1024);
		private static readonly int[] _perSoldier = new int[Main.maxNPCs];
		/// <summary>Lab counters: retired by the army limit, retired by a single soldier's limit; highest lingering count seen.</summary>
		internal static int RetiredArmy, RetiredSoldier, PeakLingering;

		internal static void Add(int who, uint serial) => _live.Add(new Entry { Who = who, Serial = serial });

		public override void OnWorldUnload() { _live.Clear(); NextSerial = 0; RetiredArmy = RetiredSoldier = PeakLingering = 0; }

		public override void PostUpdateEverything()
		{
			if (!CombatFix.On(CombatFix.Budget) || _live.Count == 0 || !LevyUtil.IsServerOrSingle || (Main.GameUpdateCount & 7u) != 0u) return;
			uint now = Main.GameUpdateCount;
			// compact: keep what is still the same live projectile
			int w = 0;
			for (int i = 0; i < _live.Count; i++)
			{
				Entry e = _live[i];
				Projectile p = Main.projectile[e.Who];
				if (p.active && p.TryGetGlobalProjectile(out PersistentShot g) && g.Serial == e.Serial && g.Serial != 0) _live[w++] = e;
			}
			_live.RemoveRange(w, _live.Count - w);

			// the lingering ones, oldest first (the list is in the order they were born)
			int lingering = 0; int board = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) board++;
			Array.Clear(_perSoldier, 0, _perSoldier.Length);
			// pass 1 (newest to oldest): one soldier keeps at most PerSoldierCap lingering shots
			for (int i = _live.Count - 1; i >= 0; i--)
			{
				Projectile p = Main.projectile[_live[i].Who];
				PersistentShot g = p.GetGlobalProjectile<PersistentShot>();
				if (now - g.Born < PersistentShot.LingerTicks) continue;
				lingering++;
				int s = g.Soldier;
				if (s < 0 || s >= _perSoldier.Length) continue;
				if (++_perSoldier[s] > PersistentShot.PerSoldierCap) { Retire(p, true); lingering--; board--; }
			}
			if (lingering > PeakLingering) PeakLingering = lingering;
			// pass 2 (oldest first): the army as a whole, and the board as a whole
			int over = Math.Max(lingering - PersistentShot.ArmyCap, board - PersistentShot.BoardCeiling);
			for (int i = 0; i < _live.Count && over > 0; i++)
			{
				Projectile p = Main.projectile[_live[i].Who];
				if (!p.active) continue;
				PersistentShot g = p.GetGlobalProjectile<PersistentShot>();
				if (g.Serial == 0 || now - g.Born < PersistentShot.LingerTicks) continue;
				Retire(p, false); over--;
			}
		}

		private static void Retire(Projectile p, bool perSoldier)
		{
			if (perSoldier) RetiredSoldier++; else RetiredArmy++;
			CombatFix.BudgetKills++;
			p.Kill();
		}
	}
}
