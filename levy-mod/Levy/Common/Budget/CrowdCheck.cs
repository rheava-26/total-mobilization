using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Audio;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;

namespace Levy.Common.Budget
{
	/// <summary>Boot self-check of the crowd layer, logged as "[Crowd] self-check ok fail=0 :: ..." (or FAILED fail=N): the budget plan on a modelled 36-levy army, the one-summon-item rule, the sound caps, the use-gate and swing-rate rules.</summary>
	public class CrowdCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			var fails = new List<string>(); var nums = new List<string>();
			void Check(bool ok, string what) { if (!ok) fails.Add(what); }
			try
			{
				// 1. the budget plan: 36 levies, each with 3 minions and 3 towers (216 entities), free slots 100 under a reserve of 150 -> 60 must go
				var cands = new List<CrowdBudget.Cand>();
				uint born = 100; int who = 0;
				for (int soldier = 0; soldier < 36; soldier++)
					for (int k = 0; k < 3; k++) { cands.Add(new CrowdBudget.Cand { Who = who++, Soldier = soldier, Sentry = true, Born = born++ }); cands.Add(new CrowdBudget.Cand { Who = who++, Soldier = soldier, Sentry = false, Born = born++ }); }
				var retire = new List<int>();
				int free = 100, reserve = 150, need = reserve - free + 10;
				CrowdBudget.Plan(cands, need, retire);
				Check(retire.Count == need, $"plan retires {need}, got {retire.Count}");
				var left = cands.Where(c => !retire.Contains(c.Who)).ToList();
				Check(left.Count(c => c.Sentry) >= 36 && left.Count(c => !c.Sentry) >= 36, "plan leaves every levy at least one tower and one minion");
				Check(retire.All(w => { var c = cands[w]; return c.Sentry; }), "surplus towers go before any minion");
				var retiredSent = retire.Select(w => cands[w]).OrderBy(c => c.Born).ToList();
				Check(retire.Select(w => cands[w].Born).Distinct().Count() == retire.Count, "plan picks distinct entities");
				Check(retiredSent.First().Born == cands.Where(c => c.Sentry).Min(c => c.Born), "the oldest surplus tower is retired first");
				CrowdBudget.Plan(cands, 500, retire);
				Check(retire.Count == CrowdBudget.MaxRetiresPerPass, "one pass retires at most " + CrowdBudget.MaxRetiresPerPass);
				CrowdBudget.Plan(new List<CrowdBudget.Cand>(), 10, retire); Check(retire.Count == 0, "nothing to retire is fine");
				CrowdBudget.Plan(cands, 0, retire); Check(retire.Count == 0, "no need retires nothing");
				// the gate: leaves the reserve, with a margin once retiring
				Check(CrowdBudget.Allows(200, 1, 150, false) && !CrowdBudget.Allows(150, 1, 150, false) && !CrowdBudget.Allows(170, 1, 150, true) && CrowdBudget.Allows(190, 1, 150, true), "the cast gate keeps the reserve and the margin");
				// before/after on the model: 216 persistent + 330 other army + 200 enemy/player on a 1000 board
				int board = 216 + 330 + 200;
				int freeBefore = 1000 - board; int armyAfter = 216 - retire.Count;
				nums.Add($"model: 36 levies x (3 minions + 3 towers) on a board of {board}/1000: free {freeBefore}; budget retires {need} -> free {freeBefore + need} (reserve {reserve})");

				// 2. one summon item for a non-summoner
				{
					Item Make(int t) { var it = new Item(); it.SetDefaults(t); return it; }
					var g = new SoldierGear();
					g.Weapon = Make(ItemID.TerraBlade); g.ExtraSidearms[0] = Make(ItemID.QueenSpiderStaff); g.ExtraSidearms[1] = Make(ItemID.HornetStaff);
					var bad = SidearmPolicy.Misfits(g, false);
					Check(bad.Count == 1, "a non-summoner with a sentry and a staff gives one back, got " + bad.Count);
					Check(SidearmPolicy.Misfits(g, true).Count == 0 && SidearmPolicy.Misfits(g).Count == 0, "a summoner (or no class given) keeps both");
					SidearmPolicy.NonSummonersOverride = false;
					try { Check(SidearmPolicy.Misfits(g, false).Count == 2, "with the config off a non-summoner keeps none"); } finally { SidearmPolicy.NonSummonersOverride = null; }
					nums.Add("non-summoner kit: sentry + staff -> " + (2 - bad.Count) + " summon item");
				}

				// 3. sound caps
				{
					Check(ArmySoundThrottle.Decide(0, ArmySoundThrottle.TotalMax + ArmySoundThrottle.HardExtra, 0f) == 0f, "the hard ceiling holds against the one-per-style guarantee");
					Check(ArmySoundThrottle.Decide(0, 0, 0f, 0f, ArmySoundThrottle.KindMax(ArmySoundThrottle.KMinion), ArmySoundThrottle.KindMax(ArmySoundThrottle.KMinion)) == 0f, "summoned minions share a small window budget");
					Check(ArmySoundThrottle.KindGain(ArmySoundThrottle.KMinion) < 1f, "summoned sounds play quieter");
					int styles = 60, windows = 300; int admitted = 0, raw = 0; int w0 = 0, tot = 0; var per = new int[styles]; var pk = new int[ArmySoundThrottle.KindCount];
					var rnd = new Random(7);
					for (int t = 0; t < windows * ArmySoundThrottle.WindowTicks; t++)
					{
						if (t % ArmySoundThrottle.WindowTicks == 0) { w0 = t; tot = 0; Array.Clear(per, 0, styles); Array.Clear(pk, 0, pk.Length); }
						for (int s = 0; s < 150; s++) // 150 sound makers: weapons, 3 summons each...
						{
							if (rnd.Next(60) != 0 && rnd.Next(60) != 0) continue; // each about every half second
							int st = s % styles, kind = s < 50 ? ArmySoundThrottle.KShot : s < 100 ? ArmySoundThrottle.KMinion : ArmySoundThrottle.KSentry; raw++;
							if (ArmySoundThrottle.Decide(per[st], tot, (s % 7) * 250f, 9999f, pk[kind], ArmySoundThrottle.KindMax(kind)) > 0f) { admitted++; per[st]++; tot++; pk[kind]++; }
						}
					}
					float perSec = admitted / (windows * ArmySoundThrottle.WindowTicks / 60f), rawSec = raw / (windows * ArmySoundThrottle.WindowTicks / 60f);
					Check(perSec <= (ArmySoundThrottle.TotalMax + ArmySoundThrottle.HardExtra) * 60f / ArmySoundThrottle.WindowTicks, "modelled 150 sound makers stay under the window ceiling");
					Check(perSec > 3f, "modelled army still plays sounds");
					nums.Add($"sound model: 150 makers {rawSec:0}/s -> {perSec:0}/s played (cap {(ArmySoundThrottle.TotalMax + ArmySoundThrottle.HardExtra) * 60f / ArmySoundThrottle.WindowTicks:0}/s, rules {(ArmySoundThrottle.OldRules ? "old" : "new")})");
				}

				// 4. a weapon's own use gate, and the swing floor
				{
					var fast = new Item(); fast.SetDefaults(ItemID.Muramasa); fast.useTime = 5; fast.useAnimation = 5; fast.damage = 100;
					Check(WeaponUse.SwingFloor(fast) == LevyConstants.MeleeSwingFloorTicks, "a melee weapon under the floor is raised to it");
					Check(Math.Abs(WeaponUse.RateComp(fast) - Math.Min(LevyConstants.SwingRateCompMax, LevyConstants.MeleeSwingFloorTicks / 5f)) < 0.01f, "its damage is scaled to keep its output");
					var slow = new Item(); slow.SetDefaults(ItemID.TerraBlade); Check(WeaponUse.SwingFloor(slow) == 0 && WeaponUse.RateComp(slow) == 1f, "an ordinary melee weapon is untouched");
					string gate = "calamity absent";
					if (ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.TryFind("Murasama", out ModItem mi))
					{
						var mur = new Item(); mur.SetDefaults(mi.Type);
						bool down = false; try { down = (bool)cal.Code.GetType("CalamityMod.DownedBossSystem").GetProperty("downedDoG").GetValue(null); } catch (Exception) { }
						Check(down || ModWeaponGates.Locked(mur), "Murasama is locked while the Devourer of Gods is not downed");
						gate = $"Murasama useTime={mur.useTime} useAnimation={mur.useAnimation} channel={mur.channel} noMelee={mur.noMelee} shoot={mur.shoot} dmg={mur.damage} doG downed={down} locked={ModWeaponGates.Locked(mur)} floor={WeaponUse.SwingFloor(mur)} comp={WeaponUse.RateComp(mur):0.0}";
					}
					nums.Add(gate);
				}
			}
			catch (Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (fails.Count == 0) Mod.Logger.Info("[Crowd] self-check ok fail=0 :: " + string.Join("; ", nums));
			else Mod.Logger.Error($"[Crowd] self-check FAILED fail={fails.Count}: " + string.Join(" | ", fails) + " :: " + string.Join("; ", nums));
		}
	}
}
