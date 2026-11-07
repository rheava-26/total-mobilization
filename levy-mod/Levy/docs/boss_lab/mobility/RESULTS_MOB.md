# Mobility lab (branch `moblab`, 2026-11-07): how much do a hook and a jump accessory matter, per boss

Base: `worktree-levy-stardust` @ 48fb7f1 (the vert1 harness). **No gameplay code changed, no harness edits were needed** (tokens `hook=`, `acc=`, `mdeath`, `a3`, `a3r5` all existed; the two tokens can be combined in one spec row and both applied).

## 0. Setup
- 7 bosses/modes x 4 arms x 6 seeds on arena `a3` (rows 7 tiles apart) = 168 fights, plus `a3r5` (5-tile rows) for King Slime Master+Death and Queen Bee (none / hook / hook+cloud, 36 fights). 204 fights total.
- Spec row: `fight <boss> <mode> <seed 1-6> 18000 cmd=near n=cp gear=mod grade=lv l3=1 order=follow [arena=a3|a3r5] [hook=Grappling_Hook] [acc=Cloud_in_a_Bottle]`. `n=cp` = the real command-point levy count (KS 10, DS 11, EoW/Brain 13, QB 15, Skeletron 16). Env: `LEVY_FEARMODEL=events`, `LEVY_SURV=formation` (the defaults), `LEVY_CR_SP=1`, `LEVY_CR_EXCLUDE=CalamityMod/UrchinMace,CalamityMod/SaharaSlicers` (same as vert1). Boss tokens as vert1: `kslime` (modes `mdeath`, `exp`), `ds` (Calamity Desert Scourge), `qb`, `brain` (mode `rev`), `eow` (`rev`), `skel`.
- Arms: `none` (no forced mobility), `hook` = every levy gets a Grappling Hook, `cloud` = every levy gets a Cloud in a Bottle, `both`.
- Machine: 4 cores, 3 concurrent tML servers (private installs `tmlm`, `tmlm2`, `tmlm3`), load average 4-7, no failures or retries needed. A 6-fight spec takes 1.5-5 min.
- Raw rows: `/home/user/lab/outm/*.tsv` (+ `.l3.tsv`), tables made by `/home/user/lab/tabm.py`, `extram.py`. Replays: see section 6.
- Tier check: Grappling Hook is pre-boss (craftable/obtainable from the start; every levy getting one is generous but legal). Cloud in a Bottle is a sky-island / desert-recipe item that a real player has before most of these bosses but it is not a given; the harness forces it regardless of whether a levy would own it (that is the point of the lab: an upper bound). Better hooks (Amethyst/Topaz/Ruby, Ivy Whip, later Calamity hooks) exist; I did not test them, so the hook numbers here are for the weakest realistic hook. Cloud is also the weakest jump bottle; vert1 saw sandstorm/blizzard bottles give more platform use (21-39%).

## 1. Headline table (a3, 6 seeds per cell)
Columns: wins/6 (seeds 1..6, W = win), mean levy deaths (levies spawned = the `n` above, so 10 for KS means the whole army), mean kill time of the wins (s), mean boss HP left over all 6 fights (%), mean hits taken.

| boss / mode | none | hook | cloud | hook+cloud |
|---|---|---|---|---|
| **King Slime Master+Death** (n=10) | 0/6, deaths 10.0, boss 79% left, hits 2 | 0/6, 9.8, 91% left, 2 | 0/6, 10.0, 76% left, 3 | 0/6, 10.0, **63% left**, 3 |
| King Slime Expert | 4/6 (WlWWWl), 4.8, 33 s, 9% left | 4/6 (WWWWll), 7.0, 51 s, 16% | **6/6**, 3.5, 57 s, 0% | 5/6, 4.3, 49 s, 0% |
| **Desert Scourge Expert** (n=11) | 1/6 (lWllll), 10.2, 41 s, 30% left | 0/6, 11.0, -, 33% | **3/6** (lWWllW), 9.0, 54 s, 7% | 2/6, 9.2, 40 s, 24% |
| **Queen Bee Expert** (n=15) | 1/6 (lWllll), 12.7, 27 s, 18% left, hits 91 | 4/6 (lWWWWl), 5.5, 33 s, 3%, hits 74 | **6/6**, 1.8, 33 s, 0%, hits 48 | **6/6**, 1.3, 33 s, 0%, hits 47 |
| **Brain of Cthulhu Revengeance** (n=13) | 3/6 (llWlWW), 8.7, 70 s | 2/6, 8.5, 68 s | 4/6 (WlWWWl), **4.0**, 58 s | 3/6, 6.2, 72 s |
| Eater of Worlds Revengeance (n=13) | 0/6, 13.0, 47% left, hits 262 | 0/6, 13.0, 41%, 287 | 0/6, 13.0, 42%, 290 | 0/6, 13.0, 39%, 267 |
| Skeletron Expert (n=16) | 6/6, 1.2, 35 s, hits 37 | 6/6, 1.3, 33 s, 32 | 6/6, 1.2, 36 s, 26 | 6/6, 0.8, 36 s, 26 |

`a3r5` (5-tile rows):

| boss / mode | none | hook | hook+cloud |
|---|---|---|---|
| King Slime Master+Death | 0/6, deaths 10.0, boss 74% left | 0/6, 10.0, **61% left** (best single fight 27% left, 70 s) | 0/6, 10.0, 60% left |
| Queen Bee Expert | 2/6, 11.5, 18-21% left, hits 98 | **6/6**, 2.7, 0% left, hits 59 | **6/6**, 2.0, 0%, hits 54 |

## 2. Movement telemetry (a3; mean over the 6 seeds; floor / platform-row / air % of levy-time)

| cell | none | hook | cloud | hook+cloud |
|---|---|---|---|---|
| KS mdeath | 62/3/35 | 48/14/38 | 30/37/33 | 26/42/33 |
| KS exp | 68/2/30 | 29/32/39 | 20/49/31 | 16/51/33 |
| Desert Scourge | 79/1/19 | 48/29/23 | 56/23/21 | 54/24/23 |
| Queen Bee | 62/4/34 | 42/17/40 | 33/32/35 | 36/31/33 |
| Brain Rev | 83/1/16 | 60/17/24 | 63/20/16 | 62/20/17 |
| EoW Rev | 79/3/18 | 36/42/22 | 37/38/26 | 33/42/24 |
| Skeletron | 64/3/32 | 26/26/49 | 31/35/34 | 29/37/34 |

Distance to commander (mean / max over seeds of the per-fight max, tiles), a3:

| cell | none | hook | cloud | hook+cloud |
|---|---|---|---|---|
| KS mdeath | 14.2 / 45 | 16.3 / 58 | 18.9 / 53 | 20.5 / 55 |
| DS | 15.8 / 47 | 15.6 / 58 | 14.7 / 41 | 15.3 / 46 |
| QB | 15.4 / 49 | 16.7 / 69 | 17.1 / 44 | 16.9 / 47 |
| Brain | 13.0 / 64 | 14.3 / 70 | 13.1 / 53 | 13.8 / 44 |
| EoW | 21.4 / 76 | 20.9 / 69 | 21.5 / 60 | 20.6 / 58 |
| Skeletron | 20.9 / 116 | 19.6 / 109 | 20.6 / 115 | 20.7 / 111 |

Survival (mean seconds a levy lived, first levy death) from `extram_final.txt`: QB none 26 s / first death 16 s, QB cloud 33 s / 24 s; KS mdeath none 19.5 s / 8 s, hook+cloud 28 s / 12 s; DS none 28.5 s / 7 s, cloud 30 s / 5 s.

## 3. OBSERVED, per hard boss
(6 seeds. A win-count difference of 1 is noise; 2 is borderline; 3+ in a clean ladder is a real effect. Per-fight boss HP left and survival are less noisy than win counts.)

- **Queen Bee Expert: mobility gear is decisive.** none 1/6 -> hook 4/6 -> cloud 6/6 -> hook+cloud 6/6. Deaths 12.7 -> 5.5 -> 1.8 -> 1.3 of 15; hits taken 91 -> 74 -> 48 -> 47; boss never survives in the cloud arms. On `a3r5`: none 2/6 -> hook 6/6. This reproduces and strengthens vert1 (hook 5-6/6). Kill time of the wins does not change (27-35 s), so the gain is survival, not DPS. The cloud arm beats the hook arm by 2 wins (borderline), but hits taken (48 vs 74) is a clear gap.
- **Desert Scourge Expert: cloud helps, hook alone does not.** none 1/6 -> cloud 3/6 -> hook+cloud 2/6 -> hook 0/6. Boss HP left averages 30% (none), 33% (hook), 7% (cloud), 24% (both). The 0/6 for hook vs 1/6 for none is noise; the cloud arm having 3 wins with 7% boss HP left is probably real but only a ~2-win difference. Army still dies fast (deaths 9-11 of 11 in every arm, first death ~5-7 s in all arms). This is still the hardest non-wipe cell: no arm clears it more than half the time.
- **King Slime Master+Death: no arm wins (0/24 on a3, 0/18 on a3r5).** The army is wiped in 20-45 s in every arm (mean levy lifetime 19.5 s none, 21 s hook, 21 s cloud, 28 s both; first death 5-12 s). Gear moves the needle on damage dealt: boss HP left 79% (none) -> 76% (cloud) -> 63% (hook+cloud) on `a3`, 74% -> 61% / 60% on `a3r5`; the best single fight was 27% left (hook, `a3r5`, seed 5, 70 s) and one hook-arm fight (seed 6) ran to the 300 s timeout with the boss at 98% (a levy survived without damaging it; not a win). Hook alone on `a3` is the worst arm (91% left), within noise of none. Observed: mobility prolongs the fight by ~10-15 s and takes ~15-20 points off the boss; it does not come close to a win. Hits taken is 2-4 in every arm, i.e. levies die to a very small number of very large hits (not to attrition).
- **Brain of Cthulhu Revengeance: no clear effect.** none 3/6, hook 2/6, cloud 4/6, both 3/6: all within noise. The one number that moves is mean deaths: cloud 4.0 and both 6.2 vs none 8.7 / hook 8.5, and mean levy lifetime 84-93 s vs 46-50 s. So the cloud arms keep levies alive about twice as long without changing the kill rate in 6 seeds; I would not claim a win-rate effect.
- **Eater of Worlds Revengeance: nothing.** 0/6 in every arm (24 fights), whole army dead (13.0) every time, boss HP left 39-47% in all arms. Mobility is irrelevant to this cell; the arm differences (47/41/42/39%) are noise. Hits taken 262-290 in every arm.
- **King Slime Expert:** none 4/6, hook 4/6, cloud 6/6, both 5/6; noise-level (2 wins). Win kill time lengthens (33 s none vs 49-57 s with gear: the levies spend time on platforms rather than on the floor under the slime).
- **Skeletron Expert:** 6/6 in all arms; hits taken 37 -> 32 / 26 / 26; deaths 1.2 -> 0.8-1.3. No effect on the outcome, a mild reduction in hits (noise-adjacent).
- **Row / air use.** Both hook and cloud move levies off the floor: floor 62-83% (none) -> 16-60%; platform rows 1-4% -> 14-51%. Cloud alone puts levies on rows more than the hook alone in most cells (e.g. KS exp 49% vs 32%, DS 23% vs 29% is the reverse). A `a3r5` arena with closer rows raises none-arm row use (KS mdeath 3% -> 16%) and the benefit of hook on QB (2/6 -> 6/6) is larger than on `a3`.
- **Skeletron 115-tile max distance:** reproduced. Max distance to the commander / centre reached 100-116 tiles in 5 of 6 seeds of the `none` arm (seeds 2-6; seeds 2-5 had zero levy deaths), in 2 of 6 hook seeds (4, 5), 1 of 6 cloud seeds (2) and 3 of 6 hook+cloud seeds (2, 4, 5). The mean distance in the same fights is only ~20 tiles. Mobility gear reduces how often it happens (5/6 -> 2/6, 1/6, 3/6; small numbers). Inferred (not traced): one levy strays to the far end of the 220-tile arena (half-width 110) while the army fights near the commander. A per-levy trace at the moment of the max is the cheap next step; no gameplay or harness change was made.

## 4. INFERRED
- Hook vs cloud: the cloud (a single extra jump) does at least as well as the hook in every cell where mobility mattered (QB, DS, Brain, KS exp), and beat it on QB (6/6 vs 4/6) and DS (3/6 vs 0/6). Inferred: levies use the extra jump more often and more cheaply than the hook cycle (a hook needs a ceiling/row to attach to and wastes time on re-grapple) while still dodging; the telemetry (cloud has the same row % but fewer hits taken on QB, 48 vs 74) is consistent with that, but I did not trace jump vs grapple events per levy.
- The mobility effect is large where the threat is a *roaming, fast, melee/projectile attacker that the floor-bound army cannot dodge or reach* (Queen Bee dashes, Desert Scourge). It is nil where the threat is raw damage density or a wall of segments (Eater of Worlds), and small where the whole army dies to a few large hits (King Slime Master+Death).
- KS Master+Death needs something other than mobility (more damage on the first 20 s, shielding, or fewer levies standing under the slime); gear alone gives roughly 15-20 points of boss HP. I did not test combined with damage gear.

## 5. Caveats
- 6 seeds per cell; wins/6 binomial noise is about +/-2 wins around 3/6. Single-win differences (KS exp, Brain, Skeletron, hook vs none on DS) are not findings.
- The harness forces the item onto every levy, regardless of the Armory's real stock; the numbers are an upper bound on what the Armory could deliver. Only Grappling Hook and Cloud in a Bottle were tested.
- The `none` arm already has a real fight-zone and row-use feature from vert1 (default). Row use 1-4% in `none` is the baseline.
- `a3r5` has only three arms (none / hook / hook+cloud) and only two bosses.
- `hitsTaken` for KS Master+Death (2-4) is not comparable to other bosses: levies die to a handful of large hits.

## 6. Replays
`/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/replays/moblab/` (12 files + `index.txt`): none vs best arm for King Slime Master+Death (a3 and a3r5), Desert Scourge, Queen Bee, Brain, an Eater of Worlds hook loss, the Skeletron 115-tile case, and the a3r5 Queen Bee hook win. File suffix `_NN` is the spec row index: 01-06 none, 07-12 hook, 13-18 cloud, 19-24 both (a3r5 specs: 01-06 none, 07-12 hook, 13-18 both).

## 7. Recommendation
Have the Armory prefer issuing a jump accessory (Cloud in a Bottle or better) to every levy, and a hook second: on Queen Bee Expert it takes the army from 1/6 to 6/6 and on Desert Scourge from 1/6 to 3/6 at no cost to any other cell. Do not expect it to fix King Slime Master+Death or Eater of Worlds Revengeance; those need damage or defense changes, not mobility.
