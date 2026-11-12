# Free Rein: the leash and the pastimes (lab results)

Harness scenario `freerein`, runner `levy-mod/lab/travel/freereinlab` (see `lab/travel/README.md`, "Free Rein lab"). 15 armed levies (5 bare, 4 boots, 3 wings, 3 hook) on Free Rein at a base on generated terrain: barracks, two campfires, beds, a keg, chairs, a 4-wide elevator shaft with a platform every 5 rows down to a cavern 130 rows under the base, a sheer 30-deep hole and a 9-deep pit with a stair out. Day and night monsters around the base, a skeleton in the cavern. Each run is a different base site (a seed); one run is 12 game minutes.
"before" is the Free Rein of 2.0.55 in the same build (`LEVY_FREEREIN_LEGACY=1`). The reference lines are fixed numbers, not the mod's own: inside 150 = within 150 tiles of the barracks and not 14+ rows under its floor; inside 205 = within 205 tiles and not 18+ rows under it.

## The wandering (observed)
Before, 10 sites x 15 levies:

- 56% of the time inside 150 tiles, 65% inside 205. 101 of 150 levies went beyond 150 tiles; the farthest was 517 tiles from the barracks (a medium world is 6400 tiles wide; on a small one the same trips are the map corners).
- 26 of 150 stood in the built hole, 46 of 150 got 20+ rows under the base floor, 16 of 150 got 40+ rows down (deepest 62 rows here, 173 in an earlier set of runs).

After: 95% inside 150, 100% inside 205, 30 of 150 ever beyond 150 (the radius is 200), farthest 199 tiles; 0 in the hole, 0 in the shaft, 0 at 20+ rows, 0 at 40+ rows, deepest 16 rows. 36 levies (6 sites): the same, 0 in the hole, 0 deep, 0 underground.

## What caused it (ablation, 6 sites x 15 levies, 8 minutes, `freerein_abl.yaml`)
| arm | inside 150 | inside 205 | in the hole | 20+ rows | 40+ rows | farthest |
|---|---|---|---|---|---|---|
| old Free Rein | 55% | 69% | 17/90 | 24/90 | 4/90 | 480 |
| + a 200-tile circle only | 67% | 84% | 16/90 | 20/90 | 2/90 | 199 |
| + rows above and below the base floor, walk home | 69% | 87% | 15/90 | 23/90 | 0/90 | 199 |
| + spots (dry, not under rock, not a pit floor) | 78% | 86% | 18/90 | 22/90 | 0/90 | 199 |
| + routes that do not dip into or cross a hole | 97% | 100% | 1/90 | 1/90 | 0/90 | 199 |
| + rim spots, fights stopped at gaps (all of it) | 97% | 100% | 0/90 | 0/90 | 0/90 | 199 |

Four causes, all of them in the old order: the leash was a 600-tile circle around home and every leg of 64 tiles was measured from the levy's own position, 5 to 9 legs one way before turning, so the army bounced out to 300 to 580 tiles; the circle had no vertical limit (600 tiles down is the whole underground); the spot for a leg was the nearest standable tile on rings around the levy's own row, so a hole floor or a cave floor was as good as the ground; and the cheapest route to a spot beyond a 3-wide hole is a jump (winged levies fly over it, waypoints one tile apart), so legs crossed holes and a knock or a bad landing at the rim dropped the levy in. Only the route check removes the falls; the circle and the box fix distance and depth.

## Ramp (a walkable way down instead of a shaft, 6 sites, `freerein_ramp.yaml`)
Before: 55% inside 150, 15/90 levies 20+ rows down, 2/90 at 40+ (47 rows). After: 98% inside 150, 4/90 reached 20+ rows (deepest 21, one base where the lab's pit opens into a natural cavity), 0/90 at 40+.

## Cost (36 levies, 6 sites x 8 minutes, `freerein_cpu.yaml`)
Whole server tick, same waves: old Free Rein 1.11 to 1.30 ms, new leash without pastimes 1.09 to 1.13 ms, new leash with pastimes 1.08 to 1.14 ms (the difference is inside the noise). The pastime code itself, measured with a stopwatch around every think: 0.9 to 3.5 microseconds per levy think, 0.14 to 0.58 s over a 28800-tick run of 36 levies, i.e. 0.005 to 0.02 ms per tick. The worst case is a base where the seats are unreachable (3.5 microseconds, retries back off 10 to 40 s). The path planner spends less than before (fewer long legs): 3 to 5 s of planner time per run against 4 to 11 s.

## Pastimes (night run, `freerein_night.yaml`, share of levy-time on Free Rein)
Standing around roaming 66%, fireside / chair sitting 11.5% (9.4% of the time seated), marshmallows 5.0%, kite 4.6% (the run is mostly night), sleeping 6.2%, keg 2.5%, stargazing 1.9%, cheering 2.5%. A pastime and a fight at the same 20-tick sample: 1 in 227,000 samples (the pastime ends in the same think the fight starts).
