# perf-destroyer raw results (lab/perf_summary.py, lab/perf_buckets.py); build = this branch unless noted

## final A/B (spec perf_destroyer_ab, 3 seeds; before = LEVY_OPT=0)
| cell | seeds | CPU ms/tick (npc + proj) | wall ms/tick | worst wall tick | levies | drones | enemy NPCs | projectiles (army/hostile) | alloc KB/tick | GC pause ms per 2 s | ticks over 22 ms after 10 s (max ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ctl | 3 | 14.18 (6.08 + 7.52) | 14.29 | 67.4 | 36 | 0 | 88 | 212 (211/0) | 402 | 3.7 | 135 (67) |
| alone | 3 | 2.67 (1.38 + 0.76) | 2.69 | 17.1 | 1 | 0 | 90 | 16 (3/13) | 48 | 0.3 | 0 (0) |
| noxs | 2 | 11.80 (6.13 + 5.03) | 11.97 | 66.5 | 36 | 0 | 91 | 125 (124/0) | 393 | 3.3 | 48 (66) |
| before | 3 | 15.81 (6.33 + 8.73) | 16.00 | 69.2 | 36 | 0 | 89 | 224 (223/0) | 403 | 3.9 | 187 (69) |


| bucket (ms per tick) | alone | ctl | before | noxs |
|---|---|---|---|---|
| tick(wall) | 2.69 | 14.29 | 16.00 | 11.97 |
| npc.levy | 0.14 | 5.14 | 5.19 | 4.97 |
| ai | 0.11 | 4.33 | 4.42 | 4.16 |
| stats | 0.01 | 2.91 | 2.79 | 2.76 |
| g.reset | 0.00 | 0.71 | 0.71 | 0.66 |
| g.equips | 0.00 | 0.54 | 0.52 | 0.52 |
| g.sets | 0.00 | 0.28 | 0.29 | 0.28 |
| g.misc | 0.00 | 1.00 | 0.97 | 1.00 |
| move | 0.04 | 0.53 | 0.81 | 0.55 |
| dodge | 0.03 | 0.39 | 0.66 | 0.40 |
| hits | 0.01 | 0.20 | 0.47 | 0.21 |
| combat | 0.01 | 0.41 | 0.42 | 0.40 |
| brain | 0.03 | 0.19 | 0.18 | 0.20 |
| npc.boss | 1.05 | 1.10 | 1.05 | 1.08 |
| npc.add | 0.06 | 0.06 | 0.06 | 0.07 |
| npc.other | 0.07 | 0.08 | 0.08 | 0.08 |
| proj.army | 0.25 | 7.39 | 8.18 | 4.65 |
| damage ms/tick | 0.18 | 4.78 | 5.34 | 3.24 |
| pairs/tick | 235.40 | 33336.40 | 38982.00 | 22789.90 |
| hits/tick | 0.20 | 8.80 | 9.20 | 5.20 |

## original code (rev ecc99bc, wall time only)
| cell | seeds | CPU ms/tick (npc + proj) | wall ms/tick | worst wall tick | levies | drones | enemy NPCs | projectiles (army/hostile) | alloc KB/tick | GC pause ms per 2 s | ticks over 22 ms after 10 s (max ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ctl | 3 | 0.00 (0.00 + 0.00) | 15.28 | 65.7 | 36 | 0 | 89 | 208 (208/0) | 0 | 0.0 | 0 (0) |
| alone | 3 | 0.00 (0.00 + 0.00) | 2.19 | 42.5 | 1 | 0 | 91 | 11 (3/9) | 0 | 0.0 | 0 (0) |
| noxs | 2 | 0.00 (0.00 + 0.00) | 12.48 | 67.8 | 36 | 0 | 89 | 137 (136/0) | 0 | 0.0 | 0 (0) |

## diagnostic runs on earlier builds: noupkeep (gear frames off), noxs, sea, cull / pairchk (audit), cutchk (dodge cut-off audit)
```
| cell | seeds | CPU ms/tick (npc + proj) | wall ms/tick | worst wall tick | levies | drones | enemy NPCs | projectiles (army/hostile) | alloc KB/tick | GC pause ms per 2 s | ticks over 22 ms after 10 s (max ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ctl | 3 | 15.33 (5.80 + 8.59) | 15.52 | 66.1 | 36 | 0 | 89 | 237 (237/0) | 402 | 3.8 | 165 (66) |
| alone | 3 | 2.29 (1.13 + 0.72) | 2.31 | 45.6 | 1 | 0 | 85 | 12 (3/10) | 48 | 2.5 | 1 (46) |
| noxs | 2 | 11.53 (6.15 + 4.73) | 11.66 | 59.0 | 36 | 0 | 88 | 121 (120/1) | 390 | 3.2 | 16 (65) |
| sea | 2 | 13.61 (5.72 + 7.24) | 13.73 | 64.0 | 36 | 0 | 89 | 204 (203/0) | 403 | 3.4 | 74 (64) |
| noupkeep | 2 | 12.38 (3.30 + 8.38) | 12.51 | 74.5 | 36 | 0 | 89 | 214 (214/0) | 144 | 3.6 | 44 (74) |
| cutchk | 2 | 15.47 (6.69 + 8.07) | 15.58 | 70.3 | 36 | 0 | 86 | 218 (217/0) | 400 | 3.9 | 210 (49) |
| before | 3 | 15.77 (6.56 + 8.51) | 15.90 | 66.4 | 36 | 0 | 88 | 222 (221/0) | 403 | 3.5 | 191 (66) |

### ctl: LEVY_PROF buckets (ms per tick, whole fight, per seed)

| cell | seeds | CPU ms/tick (npc + proj) | wall ms/tick | worst wall tick | levies | drones | enemy NPCs | projectiles (army/hostile) | alloc KB/tick | GC pause ms per 2 s | ticks over 22 ms after 10 s (max ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ctl | 3 | 14.42 (5.67 + 8.11) | 14.53 | 64.1 | 36 | 0 | 87 | 210 (209/0) | 400 | 3.6 | 89 (72) |
| alone | 3 | 2.50 (1.17 + 0.84) | 2.51 | 40.4 | 1 | 0 | 93 | 13 (3/9) | 49 | 2.2 | 1 (40) |
| sea | 2 | 14.25 (5.57 + 8.06) | 14.40 | 63.2 | 36 | 0 | 88 | 211 (211/0) | 402 | 3.2 | 48 (63) |
| noxs | 2 | 12.77 (5.81 + 6.36) | 12.89 | 65.5 | 36 | 0 | 88 | 137 (136/0) | 396 | 3.4 | 24 (66) |
| nocull | 3 | 16.39 (6.11 + 9.57) | 16.51 | 65.5 | 36 | 0 | 88 | 218 (218/0) | 400 | 3.5 | 302 (66) |
| pairchk | 2 | 19.30 (6.45 + 12.10) | 19.50 | 84.0 | 36 | 0 | 89 | 220 (220/0) | 399 | 5.2 | 438 (67) |
| noupkeep | 2 | 12.72 (3.21 + 8.84) | 13.17 | 126.7 | 36 | 0 | 88 | 221 (220/0) | 146 | 5.8 | 58 (58) |
| before | 3 | 15.13 (5.92 + 8.54) | 15.32 | 65.4 | 36 | 0 | 87 | 239 (239/0) | 402 | 3.9 | 149 (65) |

paircull[eligible=0 ineligible=10325 refused=0 audited=0 violations=0 maxHitExcessPx=0.0]
paircull[eligible=0 ineligible=17763 refused=0 audited=0 violations=0 maxHitExcessPx=0.0]
paircull[eligible=0 ineligible=19391 refused=0 audited=0 violations=0 maxHitExcessPx=0.0]
paircull[eligible=0 ineligible=25705 refused=0 audited=0 violations=0 maxHitExcessPx=0.0]
cutchk[checked=65790 cut=57707 bad=0]
cutchk[checked=141133 cut=122851 bad=0]
```
