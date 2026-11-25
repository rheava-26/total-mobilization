using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;
using Levy.Common.Systems;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The set-bonus audit's OBSERVE fight (event "observe", LEVY_HARNESS_SBMODE=fight|both with LEVY_HARNESS_SBMOD): a levy in a mod's full set holds a weapon of the set's class, fights one pinned target while a poker hits it,
	/// and everything the set could show is recorded as ONE line: every projectile type seen (by name), the buffs the levy wore, the buffs the target and the poker caught, the levy's life range, its first hit taken, damage dealt,
	/// the stealth low, the commander's life range. It judges nothing (always PASS): the line is read against the set's own text. Measurement only.
	/// </summary>
	public partial class CombatHarness
	{
		private readonly Dictionary<string, int> _sbObsProj = new();
		private readonly bool[] _sbObsPrevActive = new bool[Main.maxProjectiles + 1]; private readonly int[] _sbObsPrevType = new int[Main.maxProjectiles + 1];
		private readonly HashSet<string> _sbObsTargetBuffs = new(), _sbObsPokerBuffs = new();
		private int _sbObsLife0, _sbObsLifeMax, _sbObsFirstHit, _sbObsHits, _sbObsPrevLife, _sbObsHitSum;

		private void SbObserveStart()
		{
			_sbObsProj.Clear(); Array.Clear(_sbObsPrevActive); Array.Clear(_sbObsPrevType); _sbObsTargetBuffs.Clear(); _sbObsPokerBuffs.Clear();
			_sbObsLife0 = _sbObsPrevLife = _sbA.NPC.life; _sbObsLifeMax = 0; _sbObsFirstHit = 0; _sbObsHits = 0; _sbObsHitSum = 0;
			SbHookCensus.Clear(); SoldierNPC.GearHitCalls = SoldierNPC.GearHitCapped = SoldierNPC.GearHitErrors = 0;
		}

		private static string SbProjName(Projectile p) => p.ModProjectile != null ? p.ModProjectile.Mod.Name + "/" + p.ModProjectile.Name : (ProjectileID.Search.TryGetName(p.type, out string n) ? n : p.type.ToString());

		private void SbObserveTick()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				bool fresh = p.active && (!_sbObsPrevActive[i] || _sbObsPrevType[i] != p.type);
				_sbObsPrevActive[i] = p.active; _sbObsPrevType[i] = p.active ? p.type : 0;
				if (!fresh) continue;
				string n = SbProjName(p);
				_sbObsProj[n] = _sbObsProj.GetValueOrDefault(n) + 1;
			}
			foreach (int ti in new[] { _sbTarget, _sbPoker })
			{
				if (ti < 0 || !Main.npc[ti].active) continue;
				for (int b = 0; b < NPC.maxBuffs; b++) if (Main.npc[ti].buffType[b] > 0) (ti == _sbTarget ? _sbObsTargetBuffs : _sbObsPokerBuffs).Add(BuffLoader.GetBuff(Main.npc[ti].buffType[b])?.Name ?? Main.npc[ti].buffType[b].ToString());
			}
			int life = _sbA.NPC.life;
			if (life < _sbObsPrevLife) { int d = _sbObsPrevLife - life; if (_sbObsHits == 0) _sbObsFirstHit = d; _sbObsHits++; _sbObsHitSum += d; }
			_sbObsPrevLife = life; if (life > _sbObsLifeMax) _sbObsLifeMax = life;
		}

		private string SbObserveVerify(SbSpec s, out string info)
		{
			NPC tg = _sbTarget >= 0 ? Main.npc[_sbTarget] : null;
			int dealt = tg != null ? _sbTargetL0 - tg.life : 0;
			Player d = _sbA.Dummy;
			string projs = string.Join(",", _sbObsProj.OrderByDescending(kv => kv.Value).Take(14).Select(kv => kv.Key + "x" + kv.Value));
			string spawned = string.Join(",", GearUpkeep.SpawnCounts.OrderByDescending(kv => kv.Value).Take(14).Select(kv => (kv.Key < ProjectileID.Count ? (ProjectileID.Search.TryGetName(kv.Key, out string vn) ? vn : kv.Key.ToString()) : (ProjectileLoader.GetProjectile(kv.Key)?.FullName ?? kv.Key.ToString())) + "x" + kv.Value));
			string dropped = string.Join(",", GearUpkeep.Dropped.Select(kv => (kv.Key < ProjectileID.Count ? kv.Key.ToString() : (ProjectileLoader.GetProjectile(kv.Key)?.FullName ?? kv.Key.ToString())) + "x" + kv.Value));
			info = $"class {s.Cls}; dealt {dealt}; levy life {_sbObsLife0}->min {_sbLevyLifeMin} max {_sbObsLifeMax}, wounds {_sbObsHits} (first {_sbObsFirstHit}, sum {_sbObsHitSum}), defense {d.statDefense}, endurance {d.endurance:0.###}; commander life {_sbMinCmd}..{_sbMaxCmd}; stealth min {_sbMinStealth:0.##}; "
				+ $"levy buffs [{string.Join(",", _sbBuffsSeen)}]; target buffs [{string.Join(",", _sbObsTargetBuffs)}]; poker buffs [{string.Join(",", _sbObsPokerBuffs)}]; projectiles [{projs}]; spawnCounts [{spawned}]; dropped [{dropped}]; gearOnHit calls {SoldierNPC.GearHitCalls} capped {SoldierNPC.GearHitCapped} errors {SoldierNPC.GearHitErrors}; hooks dummy calls/myPlayerOk [{SbHookCensus.Table()}]";
			string flds = Environment.GetEnvironmentVariable("LEVY_HARNESS_SBFIELDS");
			if (!string.IsNullOrEmpty(flds))
			{
				var vals = new List<string>();
				foreach (object mp in SbModPlayersOf(d))
					foreach (string f in flds.Split(',', StringSplitOptions.RemoveEmptyEntries))
					{
						var fi = mp.GetType().GetField(f.Trim(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
						if (fi != null) vals.Add(mp.GetType().Name + "." + f.Trim() + "=" + fi.GetValue(mp));
					}
				info += "; fields [" + string.Join(" ", vals) + "]";
			}
			return null;
		}
	}
}
