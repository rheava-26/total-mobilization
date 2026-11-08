# Travel lab results (A to B)

Lab: `levy-mod/lab/travel/` (README there). 15 levies per run: 5 bare, 4 Hermes Boots, 3 Angel Wings, 3 Grappling Hook. The player is 400 tiles behind the start for the after runs of the corruption course (20 tiles in the earlier runs; no run showed a body lost, so distance from the player did not matter here).
Before = the original code (the "W"/"B" runs, and `LEVY_TRAVEL_LEGACY=1` in the same build). After = this branch.

## OBSERVED (before)
Two generated medium worlds (seeds 11111, 22222), five start points each, map order. Arrival = within 12 tiles of the point.
- 800 tiles: 6 of 150 levies arrive (4%). 150 tiles: 154 of 435 (35%).
- A carved benign course of 920 tiles (hills, pond, house with doors, tunnel, lake, bridge): 15 of 15 arrive in 91 s. Length is not the problem; obstacles are.
- Stuck reasons (movement log, all runs): wall taller than a jump 97, gap with no floor within 20 rows 81, tight overhang 50, deep drop 25, goal above 18. Winged levies stood at every gap (the gap rule had no wing case) and at waypoints on slopes. After 60 s without progress the order silently became Hold.
- Follow never sticks only because it teleports (954 teleports for 150 levies over 800 tiles, about 6 each); map orders correctly never teleport.
- Not seen: out-of-range despawn or a lost body (0 in every run), leash teleports on map orders (0).

## Root causes (each confirmed by a run before it was changed)
1. No route. A levy aims 30 tiles ahead and only searches (160x100 window) after a hard block. A detour, a climb by wing or a pit-with-stair gets no plan.
2. Executor and planner disagreed: bottomless gap hard-blocked winged bodies; a waypoint counted as reached inside 12 px but the body stopped inside the 24 px arrival box on slopes (stood "arrived" forever); a drop waypoint under a body balanced on a ledge gave no sideways error; the look-ahead refused a jump the plan asked for; gap jumps ignored run speed (3 tiles for boots too); closely spaced waypoints made them walk, not run.
3. Silent give-up.

## Fix
`RoutePlanner` (shared sliced A* over the whole trip, 700 nodes/tick, cached per goal + ability class + 12-tile start bucket, closest-reachable-point fallback, hops for trips over 1100 tiles), `PathSearch` (the old search made resumable; adds a running-jump gap edge, a wing flight budget, and the water rule the body already obeys), `SoldierNPC.Route.cs` (follow the route; replan when off it or stalled), plus the executor fixes above, no hard-blocking of winged bodies at gaps, and a refusal to step into a hole deeper than a jump unless a plan asks. A levy whose route runs out tells the commander ("could not find a way ... stopped as close as they could") after 24 s instead of 60 s of silence.

## After (final build, same worlds and seeds)
| set | before | after |
|---|---|---|
| real worlds, 800 tiles | 6/150 (4%) | 24/150 (16%); wings 10% to 43% |
| real worlds, 150 tiles | 154/435 (35%) | 222/450 (49%); wings 43% to 84%, bare 34% to 41%, hook 30% to 42% |
| carved 920 tiles / 150 legs | 15/15, 90/90 | 15/15, 90/90 (same time, no stuck) |
| obstacle zoo (cliffs, cracks, chasms) | 15/90 | 34/90 (the rest cannot be crossed on foot) |
| real world 800 with monsters and handguns (M2 x2700) | 6/15 | 14/15 |
| corruption course: 8-deep and 4-wide holes | 4 of 15 (boots) | 7 of 15 (boots, wings); nobody falls in |
| corruption course: cave mouth under a rock lid | 15/15 | 15/15 |
| corruption course: 60-deep chasm across the route | 0/15 | 3/15 (wings); the rest stop at the lip, no falls |
| Follow, 800 real tiles | 49% | 49% (teleports unchanged) |
Cost: planner 33 ms per whole run on average (about 1.4 ms per slice, one 25 ms outlier under load); local searches unchanged.

## Still open (OBSERVED)
Ground levies (bare, boots, hook) cannot pass faces taller than 6 tiles or chasms: the hook is not part of routing. Levies that wade into a deep lake along a slope float mid-water and do not get out (a swim-up change did not fix it and was dropped). 12 of 14 failing zoo legs and most remaining real-world failures are these.
