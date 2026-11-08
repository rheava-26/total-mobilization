# Boss spawn audit (Eve: "Rev EoW is 4 worms")

## OBSERVED (Calamity 2026.10 decompiled: EaterOfWorldsAI.cs)
- `flag = CalamityWorld.death || BossRushActive`. The 4-worm split is gated on `flag`: head spawn code makes 3 extra heads (each with its own body chain) only when `flag`. Segment count `GetEaterOfWorldsSegmentsCountRevDeath()` = 62 (Rev, one worm) or 57 (Death: 4 worms of 14 bodies + tail).
- So 4 worms is **Death mode only**. Revengeance alone is 1 head, 62 bodies, 1 tail. The lab's EoW "rev" arm was therefore a correct Rev fight; it was the wrong fight only for Death (Eve plays Rev/Death; if she is on Death, the "death" arm below is hers).
- All worm segments, extra heads, Skeletron hands, Creepers, Nuisances, Paladins, Hive blobs, Perforator cysts, King Slime jewels are spawned **inside the boss AI** (GetSource_FromAI), not by the summon item. The only summon-path difference is `CalamityUtils.SpawnBossUsingItem` -> `NPC.SpawnOnPlayer` (position/target). CalamityGlobalNPC.OnSpawn does not look at the entity source for bosses. So a direct `NPC.NewNPC` of the head runs the same hooks.
- Census after the fix (log line `HARNESS-CR: worm census`): Rev heads=1 bodies=62 tails=1; Death heads=4 bodies=56 tails=4. (Death census prints `death=False` because the `CrDeathOn()` reflection label is stale; the 4 heads prove Death was on.)

## Audit of lab bosses (spawn vs real summon)
King Slime, Desert Scourge (+Nuisances, spawned by DS AI via SpawnOnPlayer), Brain, EoW, Queen Bee, Skeletron (hands), Hive Mind, Perforators, Slime God, Crabulon: all head/core spawned directly; all adds come from AI. None spawned wrong by hook. Only difference was spawn placement/target.

## Fix
`CombatHarness.CalRev.cs`: boss now spawned with `NPC.SpawnOnPlayer(0, type)` (the summon item path), then moved to the lab's fixed spot (LEVY_CR_SOPPOS=1 keeps game position, LEVY_CR_SOP=0 = old NewNPC). Added worm census log.

## Rerun (arena a3, cmd=near, order=follow, n=13, LEVY_CR_SP=1, 4 seeds)
| mode | wins | kill time | levy deaths | boss left at end |
|---|---|---|---|---|
| rev (1 worm) | 0/4 | none (loss at 32-49s) | 13/13 each | 6.8-11.9k of ~22k |
| death (4 worms) | 0/4 | none (loss at 26-34s) | 13/13 each | 16.6-18.2k of ~23.5k |

Spawn path did not change the outcome; Death is harsher (first levy death 6-9s vs 16-22s). Recordings: scratchpad `replays/eowrev/` (index.txt).
