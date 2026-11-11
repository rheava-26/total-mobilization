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


# perf2 (IL pair cull before any hook; branch perf2, build a62f3a0a8812; same spec, same seeds in each arm; profiled CPU ms per tick; the box carried other builders' load, compare arms inside one run)

## Destroyer, 36 levies, Caustic Staffs, Death mode, 12 seeds: cull on (ctl) against `LEVY_PAIRCULL=0` (nocull), `lab/perf_pairs.py RUN nocull ctl`
| seed | CPU ms/tick nocull / ctl | hit loop ms/tick nocull / ctl | projectiles ms/tick nocull / ctl | ticks > 22 ms nocull / ctl | hits/tick nocull / ctl | pairs asked/tick nocull / ctl | culled/tick nocull / ctl |
|---|---|---|---|---|---|---|---|
| 1 | 14.88 / 11.26 | 5.95 / 1.11 | 7.95 / 4.14 | 241 / 50 | 8.70 / 9.30 | 32896 / 517 | 0 / 36842 |
| 2 | 16.75 / 11.99 | 5.96 / 0.99 | 9.26 / 4.38 | 470 / 34 | 9.40 / 8.50 | 37256 / 425 | 0 / 35843 |
| 3 | 19.08 / 12.21 | 7.14 / 1.17 | 11.40 / 5.25 | 572 / 68 | 10.70 / 8.40 | 47139 / 217 | 0 / 52551 |
| 4 | 15.40 / 11.27 | 4.88 / 0.98 | 7.92 / 4.06 | 307 / 63 | 10.00 / 8.60 | 30466 / 1083 | 0 / 30648 |
| 5 | 15.59 / 11.40 | 4.78 / 1.11 | 7.79 / 4.26 | 283 / 34 | 7.50 / 9.90 | 29063 / 1373 | 0 / 34418 |
| 6 | 17.50 / 10.91 | 6.05 / 1.08 | 9.50 / 3.97 | 639 / 34 | 8.70 / 8.80 | 34541 / 1123 | 0 / 33534 |
| 7 | 14.69 / 11.85 | 4.20 / 1.04 | 7.04 / 4.03 | 259 / 37 | 7.10 / 9.20 | 25080 / 1559 | 0 / 26671 |
| 8 | 18.93 / 13.07 | 6.95 / 1.27 | 11.14 / 5.16 | 707 / 137 | 10.40 / 9.80 | 42934 / 1145 | 0 / 38849 |
| 9 | 19.89 / 11.13 | 6.80 / 1.02 | 11.06 / 3.92 | 884 / 56 | 10.40 / 9.20 | 37828 / 494 | 0 / 29896 |
| 10 | 17.87 / 11.27 | 5.63 / 0.86 | 9.50 / 3.81 | 503 / 37 | 9.50 / 9.50 | 33184 / 320 | 0 / 30511 |
| 11 | 17.90 / 11.83 | 6.00 / 0.96 | 10.11 / 4.27 | 531 / 39 | 9.70 / 8.10 | 36991 / 425 | 0 / 34551 |
| 12 | 18.19 / 12.25 | 6.37 / 1.21 | 10.41 / 4.46 | 647 / 60 | 8.30 / 10.80 | 39043 / 1384 | 0 / 35871 |

| metric (12 paired seeds) | nocull median | ctl median | ctl - nocull mean | ctl better on |
|---|---|---|---|---|
| CPU ms/tick | 17.68 | 11.62 | -5.52 (-32%) | 12/12 |
| hit loop ms/tick | 5.98 | 1.06 | -4.83 (-82%) | 12/12 |
| projectiles ms/tick | 9.50 | 4.20 | -5.11 (-54%) | 12/12 |
| ticks > 22 ms | 517 | 44.5 | -449.5 (-89%) | 12/12 |
| hits/tick | 9.45 | 9.20 | -0.02 | 6/12 |
| pairs asked/tick | 35766 | 800 | -34697 (-98%) | 12/12 |

Previous branch head e1e6532 (same spec, 12 seeds, run a few minutes later on the same loaded box): 17.26 ms CPU per tick (7.25 + 9.28), 478 ticks over 22 ms. Bucket comparison nocull / ctl (ms per tick): npc.levy 5.85 / 5.72, ai 4.94 / 4.80, stats 3.27 / 3.20, npc.boss 1.21 / 1.19, proj.army 9.15 / 4.02, damage 5.98 / 1.06.

An earlier run of the same comparison with the first version of the edit (margin 160 px plus the shot's speed, whole-struct call per pair; build aade616be568): CPU 16.00 -> 12.66 (-24%), hit loop 5.58 -> 1.82, pairs asked 32 800 -> 4 070.

## Audit (`LEVY_PAIRCHK=1`: nothing culled, every classified pair asked vanilla's Colliding): `lab/specs/perf_destroyer_audit.yaml`, 12 seeds
checked 976 936 071 pairs, 968 809 712 of them would be skipped, violations 0, maxHitExcessPx 0.0 (no hit ever landed outside the plain rectangle). Left alone as not plain (about 3% of army projectile ticks): CalamityMod/ShadeFire, StormSurgeTornado, SandstreamScepterExplosion (all override Colliding), vanilla 974. Vanilla Skeletron, 20 seeds: 46 779 485 checked, 45 735 005 would be skipped, 0 violations.

## Vanilla boss (Skeletron, vanilla world, Master+Death, 8 levies after the harness drops the Calamity items), 100 seeds (41-140) per arm
| | cull on | cull off |
|---|---|---|
| wins | 57 / 100 | 60 / 100 (p 0.77) |
| mean damage dealt to the boss | 10 152 | 10 527 |
| mean damage per second | 232.2 | 233.7 |
| CPU ms per tick (median) | 1.45 | 1.53 |
| hit loop ms per tick (median) | 0.10 | 0.15 |
| hits per tick (median) | 0.35 | 0.40 |
| pairs asked per tick | 14 | 724 |

The fights are chaotic (identical arms differ): seeds 1-10 and 1-40 earlier showed 17/30 and 20/40 wins against 24/30 and 28/40 for the cull-off arm; the 100 fresh seeds above and the audit (0 violations, the same hit test on every skipped pair) say it was noise.

## Boots
Both headless boots (Calamity Destroyer world, vanilla Skeletron world) of every run above: 0 exceptions from Levy. Calamity's `DeathstareBeam.AI` NullReferenceException (tML "Silently Caught", Calamity code, also in the previous builder's logs) appears twice per Calamity process. All self-checks pass (Provisions, Crowd, OOA/tavern, bard renown, mobility, summon-staff pick in the Calamity world); the vanilla world shows the known summon-staff pick failure (2 failures, unchanged by this work). Levy, Mechanization and Warbands compile with 0 errors with the real build (`-build`).
