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

## Open after round 1 (OBSERVED, closed or narrowed in round 2 below)
Ground levies (bare, boots, hook) cannot pass faces taller than 6 tiles or chasms: the hook is not part of routing. Levies that wade into a deep lake along a slope float mid-water and do not get out (a swim-up change did not fix it and was dropped). 12 of 14 failing zoo legs and most remaining real-world failures are these.

# Round 2: hooks in routes, pits, water

Lab, worlds and seeds as above (M1 11111, M2 22222, `lab/travel/round2/` has the scripts). Before = branch head after round 1 (`worktree-levy-stardust`), after = `travel2`. Commander 20 tiles behind the start (`LEVY_TRAVEL_CMDFAR=20`; the default 400 pulls a levy that spawns in Follow back to the commander, so a leg starting past offset 420 started 400 tiles earlier than its name). Arrival = within 12 tiles in x and 10 in y of the point. One run per row, 15 levies (5 bare, 4 boots, 3 wings, 3 hook).

## OBSERVED (before this round)
- Ground levies: a face above their jump or a chasm has no route (Partial route, they stop at the lip, which is right), and the hook never entered a route: the local triggers need a blocked walk and "no path", and a levy following a route always has a path.
- A levy that wades into a lake **sinks to the floor and stays**. An NPC with `aiStyle -1` has no buoyancy: in water vanilla gives it gravity 0.2, max fall 7 and halves its movement (`NPC.UpdateNPC_UpdateGravity`, `Collision_MoveWhileWet`), and the movement code never swam. A jump from the floor is half height, so a steep shore (the lake of M1 at x 1700: 11 deep, a narrow pocket at the bottom, steep far wall) is not climbable. Bare/boots/hook in that lake: 0 of 12 arrive in 61 s, 144 s wet in the 800-tile run.
- The "float mid-water" state was NOT seen with the head code. It was produced by the first fix tried here (swim up whenever the head is under water and the goal is not clearly below): the goal 2 to 3 tiles below pulled the body down and the swim pushed it up, a balance. The final rule swims only when the goal is above the feet.
- Falls: the route search refused a drop deeper than jump + 6 (11 tiles for boots), the executor refused it too (`BlockWhy 8`), so a pit with steps up the far side was never entered.

## What changed
1. **Hook edges in `PathSearch`** (`ExpandHooks`). Only for a levy with a usable hook (`MovementAbilities.HookReachPx`), not for wings. A walking route is searched first; only when it does not reach the goal does the search run again with hook edges (so a levy still walks where it can). From a standing cell with a wall or an open edge beside it: to the highest standable cell of each column within 0.88 of the reach (3 nearest the goal per side) that has a fan anchor (24 rays across the upper half plane, first latchable tile, as `HookPlanner.FanAnchors`) within 3 columns; up a wall face (one cast when it is within 0.75 of the reach, a chain of casts up to 3.2 times the reach). Not from water. Cost 7 + 0.5 per tile.
2. **Executor** (`SoldierNPC.Route.cs`): the route leg ends at the cast point; there `RouteHookTick` calls the real `HookStart(HookReason.Route, target)` (the usual planner: fan, reel simulation, release, step-back off the wall, chain climbing), up to 3 tries 45 ticks apart. An edge that comes to nothing in 420 ticks or 3 tries bans its target (kept per soldier, up to 6) and asks for a new route; the ban list is part of the route cache key. After the release the body holds the direction key toward the target (`HookParams.AirSteer`, the same rule `TickBallistic` already used for a chain), in the planner's simulation and in the flight; without it the wall zeroes the sideways speed and a single cast cannot climb a cliff.
3. **Pits**: the route search lets a ground levy fall up to `FallScanTiles` (20, inside the 25 free tiles) and climb out by the usual step/jump edges; a cell reached by a fall deeper than a jump is never the best-effort end of a Partial route until the body is back at the row it fell from (so a goal beyond a pit that cannot be left stops at the lip, as before). The executor lets a route's descent happen (`ScanAhead`).
4. **Water** (`SoldierNPC.SwimAssist`): in water, with the goal more than 1 tile above the feet, upward acceleration 0.35 to a top of 3 px/tick (half in honey), as a player holds jump. A goal level with or below the feet is left to sink to; the planner's rule (floor walk, enter deep water only from a wet cell) is unchanged.
5. Not done: **wall climbing gear** (Shoe Spikes, Climbing Claws, Tiger Climbing Gear). `MovementAbilities` does not read `spikedBoots`, no lab levy carries them, and I did not measure what a levy with them does, so there is no wall edge for them. Without that gear the lip stays the end (no wall jump, no teleport).

## After (final build `n7`, same worlds and seeds, before = head)
| set (gears: bare / boots / wings / hook) | before | after |
|---|---|---|
| real worlds, 150 tiles (30 runs) | 215/450 (47%): 58/150, 46/120, 76/90, 35/90 | 274/450 (61%): 82/150, 66/120, 73/90, 53/90; wings 76 to 73 is noise |
| real worlds, 800 tiles (10 runs) | 24/150 (16%): 5/50, 3/40, 13/30, 3/30 | 24/150 (16%): 5/50, 3/40, 13/30, 3/30 |
| real worlds, Follow 800 (10 runs) | 75/150: 34/50, 21/40, 10/30, 10/30 | 73/150: 35/50, 24/40, 4/30, 10/30 (noise, see below) |
| 150-tile starts that are on a lake (M1 x1700 med1 and med3, M1 x3700 med1) | 3/15 each (wings only) | 15/15 each |
| obstacle zoo, map (6 legs) | 34/90: 5/30, 8/24, 18/18, 3/18 | 40/90: 5/30, 8/24, 18/18, 9/18 (cliff10 and chasm14: hook 0/3 to 3/3) |
| obstacle zoo, Follow | 51/90 | 50/90 |
| corruption C0: 4-wide holes 3, 5, 8 deep | 7/15: 0/5, 4/4, 3/3, 0/3 | 10/15: 0/5, 4/4, 3/3, 3/3 |
| corruption C1: cave mouth under a rock lid | 15/15 | 15/15 |
| corruption C2: 8-wide 60-deep chasm | 3/15: 0/5, 0/4, 3/3, 0/3 | 6/15: 0/5, 0/4, 3/3, 3/3 |
| corruption C3: 4-deep hole | 15/15 | 15/15 |
| corruption C4: 6-wide 14-deep pit, steps up the far wall (new) | 3/15: 0/5, 0/4, 3/3, 0/3 | 15/15: 5/5, 4/4, 3/3, 3/3 |
| carved 920, six 150 legs, full, map | 90/90, 15/15 (same time) | 90/90, 15/15 |
| carved 920, Follow | 10/15 loaded; 7/15 and 7/15 on an idle machine | 9/15 loaded; 9/15 and 9/15 idle (all 15 reach the last 20 tiles in every run) |

Cost, idle machine, one run at a time (planner ms over the whole run; slices are 700 nodes or 1.6 ms, whichever ends first):

| run | before: ms, routes, nodes, max slice | after: ms, routes, nodes, slices, max slice |
|---|---|---|
| M1 800 tiles | 42.8, 20, 30.8k, 4.45 ms | 135.2, 36, 40.1k, 127, 2.73 ms |
| M2 150 tiles | 11.0, 13, 2.0k, 3.78 ms | 9.4, 13, 2.1k, 14, 1.88 ms |
| corruption C0 | 48.7, 99, 15.1k, 4.52 ms | 94.0, 105, 18.8k, 128, 5.52 ms |
| corruption C2 | 40.3, 97, 13.6k, 3.17 ms | 56.7, 108, 17.8k, 131, 3.13 ms |
| carved 920 | 225.6, 100, 96.0k, 3.29 ms | 337.8, 105, 99.4k, 268, 2.91 ms |

Per slice the planner averages 0.4 to 1.3 ms and tops out at 5.5 ms (before: 3.2 to 4.5 ms). A node costs 3.4 us on the carved course (2.35 us before: the pit bookkeeping); a node at a wall or an open edge costs about 100 us with a hook (the anchor fan: 24 rays, plus the columns), 5.7k such scans in the 800-tile run. The 800-tile run uses 3 times the planner time: 20 routes became 36 and the hook levies search twice (walking first, then with hooks) (INFERRED: the extra routes are replans after a banned edge). Averaged over the full set with three runs at once: 2.09 before and 3.15 after ms per 700 nodes; the worst slice 71.5 ms before (a loaded machine) and 20.8 ms after. The swim rule is one tile read and a compare per tick in water.

## Reading it
- 150-tile real-world trips: 47% to 61%; bare 39% to 55%, boots 38% to 55%, hook 39% to 59%. Most of the gain is water (the lake starts 3/15 to 15/15; 8 of 30 runs had a levy swimming 15 s or more without arriving, now 3) and pits and shores a hook can climb.
- 800-tile real-world trips do not move (24/150 both). OBSERVED, bare levies' stuck lines in those runs: wall 16, overhang 10, void gap 5, deep drop 5, goal above 4, all with a Partial route. INFERRED: the far ends are walls and cliffs beyond a hook's reach, and several starts have the goal 140 to 170 rows below (a fall of that depth is damage, so the lip is the correct end). Wings are unchanged (13 of 30), hook levies too (3 of 30).
- Follow is noisy at the 12-tile arrival line. Carved course: before 10/15 once (loaded), 7/15 twice (idle); after 9/15 three times; all 15 levies reach the last 20 tiles in every run. Real worlds, Follow 800: 75/150 before, then 75, 74, 80, 73, 73 over five builds of this branch (wings between 4 and 10 of 30 with no code path in common); teleports 1079 before, 978 after.
- Z5 (20-high cliff, 40-wide mesa, 20-deep drop): hooks climb the face (chain) but the chain is flaky and they stop on the mesa; open.
- Z1, Z3 (crack, 20-wide chasm): hooks stop at the lip: the far lip has no anchor above it within reach, so no hook edge exists; correct, not a miss.
- Remaining failures with a hook route (real worlds, OBSERVED in the stuck lines): `hookWant` at the cast point with no line found by the planner or the retries not yet spent, a cast point in a wet cell on a replan, and routes whose terrain gives many short hook edges in a row where one of them fails. Verifying every edge on `HookPlanner.Plan` at route time (tried, `D`/`E` builds) gave no more arrivals (55 of 90 vs 54 of 90 for hooks) and cost up to 150 ms in one tick under load, so it was dropped.

# Round 3: wall-climb gear and off-screen catch-up

Lab: `lab/travel/round3/` (`jobs.py` lists the runs, `run_all.sh TMOD TAG sets`, `tabr.py`, `costs.py`, `summary.csv` = every levy of the numbers below). Before = branch head `worktree-levy-stardust` plus the harness changes only (`bf`), after = `travel3` (`ag`). Same worlds and seeds (M1 11111, M2 22222), 15 levies per run. "far" = commander 600 tiles behind the start and the levies waiting where they stand (`LEVY_TRAVEL_HOLD=1`; a Follow levy 400+ tiles from the commander is pulled back to them before the order lands); "near" = the commander walks after the army (at most 14 px per tick, at the middle levy) so the army is on screen. Hops are counted by the harness itself: any move of more than 6 tiles in one tick whose start or end is inside 2400x1400 px around a player.
Gear arms: mixed = 5 bare / 4 Hermes Boots / 3 Angel Wings / 3 hook; climb = 5 Shoe Spikes / 5 Climbing Claws / 5 Tiger Climbing Gear.

## OBSERVED (before)
- No levy with `spikedBoots` climbs: `MovementAbilities` did not read it, so spiked levies are bare levies (zoo with the climb arm: 0/15 on the 10-high and the 20-high cliff).
- Real 800-tile trips: 24/150 arrive (16%) with the commander far or leaving. Stuck lines: walls, overhangs, drops deeper than the search, sealed pockets.
- A zoo or corruption obstacle that cannot be crossed on foot ends the order as Hold at the lip.

## What changed
1. **Wall climb gear.** `MovementAbilities.SpikedBoots` is the dummy's `Player.spikedBoots` (1 Shoe Spikes or Climbing Claws, 2 both, Tiger Climbing Gear, Master Ninja Gear, any mod that sets it). `PathSearch.ExpandClimb`: from a standing cell with a wall beside it, up the whole face (any height up to 380 tiles; a face a jump clears gets no edge) to the cell on its top, when the column the body climbs in is open; cost 3 + 1.1 per tile; route kind 3; part of the route cache key. `SoldierNPC.Climb.cs` performs it as `Player.WallslideMovement` does: caught on the wall while falling (strong spikes hold the body still, weak ones slide at 0.5 px/tick), 3 ticks later a jump with a 3 px/tick kick off the wall, then held toward the wall again; at the lip it steps onto the top. A climb that gains no tile in 150 ticks bans its lip like a failed hook and the route is asked for again. `AccessoryMobility` values the gear (1.0, 1.3 for spikedBoots 2, plus the dash bonus on Master Ninja Gear), so the Armory hands it out as a levy's mobility accessory (wings 4 and bundles 3.9 stay above).
2. **Off-screen catch-up** (`SoldierNPC.CatchUp.cs`, `OffscreenHop.cs`, `FollowerTravel.HopTo` = the placement Follow's catch-up uses: no velocity, no fall damage for 240 ticks, `netUpdate`). A map-point levy that no active player can see (outside 2400x1400 px around every one) and has stayed within 5 tiles of one spot for 6 s hops: along its route to the furthest waypoint that has a standing spot (floor, three clear rows, no liquid, line of sight) out of everyone's sight; with a route that ran out short, or none, to a standing spot within 5 tiles of the goal; if a player can see that, to the nearest unseen standing spot on the straight way (at least 10 tiles on, in a region of 240 walkable cells or 40 columns, so not a sealed pocket). The "unreachable" notice only fires for a levy that did not hop. Constants: `LevyConstants.Movement.CatchUp*`, `Climb*`.

## After (arrivals, same sets)
| set | before | after |
|---|---|---|
| real 800, commander far (600), mixed | 24/150 (16%): 5/50, 3/40, 13/30, 3/30 | 134/150 (89%): 46/50, 39/40, 22/30, 27/30; 145 hops, 0 seen, mean 43 s (before 83 s) |
| real 800, commander leaving (20 tiles), mixed | 24/150 (16%) | 117/150 (78%), 129 hops, 0 seen |
| real 800, commander with the army (near), mixed | 21/150 (14%) | 59/150 (39%), 43 hops, 0 seen |
| real 800, far, climb gear | 14/150 (9%) | 142/150 (94%): 47/50, 47/50, 48/50 |
| real 800, near, climb gear | 3/150 (2%) | 11/150 (7%), 10 hops, 0 seen, 4 wall climbs |
| real 150 (30 runs), mixed | 272/450 (60%) | 358/450 (79%) |
| real 150, climb gear | 245/450 (54%) | 348/450 (77%) |
| zoo, 6 legs, mixed | 40/90 | 90/90 |
| corruption C0-C4, mixed | 61/75 | 75/75 |
| carved 920 and six 150 legs, mixed | 105/105, 86 s | 105/105, 86 s |
| zoo + corruption, climb gear, commander on screen (no hops possible) | Z0 0/15, Z5 0/15, C0 7/15 | Z0 15/15, Z5 7/15, C0 15/15 (15 climbs each) |
| Follow 800 (4 runs) and carved Follow | 23% / 53% and 9/15 | 26% / 46% and 10/15 (noise, Follow code untouched) |

Cost (planner ms over the whole run; slices are 700 nodes or 1.6 ms): real 800 1.15 ms/slice before, 1.24 after (1740 vs 1382 slices, 2007 vs 1719 ms in all); far runs 1.03 vs 1.11. Slowest slice 10.3 ms in both (a loaded machine; 4.9 vs 4.4 ms on the far set). A hop costs 0.05 to 0.08 ms (choosing the spot, once per hop; a levy that cannot hop retries every 2 s). The climb edge adds one column scan per standing cell beside a wall, only for levies with spiked gear.

## Reading it
- OBSERVED: the arrivals come from the hop. With the commander far, wings and hook go from 13/30 and 3/30 to 22/30 and 27/30; the misses left are levies still walking at the cap or wings with no standing spot on the way.
- OBSERVED: with the commander on screen nobody hops and the army does not get there (39%). Eve's case (the player elsewhere) is the far row; the near row is the price of "never on screen" and the baseline for the next fix.
- OBSERVED: climbing is used where a wall blocks the route (zoo Z0 and Z5, C0 holes, 15 climbs each) and rarely in the real worlds (0 to 15 climbs in 10 runs); the real-world failures are mostly drops and pits, which the hop covers.
- OBSERVED, not fixed: the route search accepts a fall of exactly `FallScanTiles` (20) but `TileNav.DropDepth` in the executor sees 19 below the lip, so a plan that drops 20 is refused at the edge (zoo Z5 mesa, `void-gap`). Climbers stop on the mesa until a hop.
- INFERRED: Shoe Spikes and Climbing Claws climb like Tiger Climbing Gear here because the levy jumps off the wall 3 ticks after it catches whatever the gear.
- Harness note: a headless server resets `Player.active` every tick for a slot nobody is connected on, so the lab lists its stand-in commander in `OffscreenHop.LabViewers`; on a real server connected players are active.

# Round 4: hook levies climb like a player

Lab: `lab/travel/round4/`. Before = branch head `worktree-levy-stardust` plus the harness only (`h1`), after = `travel4`. Commander on screen means `LEVY_TRAVEL_NOHOP=1` (no catch-up hop exists); "far" is 600 tiles behind with the levies holding (hops on). 13 hook levies per run, one per vanilla hook (Grappling, Amethyst, Sapphire, Emerald, Diamond, Ivy Whip, Web Slinger, Skeletron Hand, Slime Hook, Fish Hook, Dual Hook, Lunar Hook, Static Hook). New course `wall`: flat ground, a sheer face H tiles tall with a plateau on top (goal 150 tiles on), or a chasm W wide and 60 deep.

## OBSERVED (before), per cast logs (`HK` lines in events.txt)
- A face above 20 tiles was never tried by a route: the chain edge stopped at 3.2 x the hook's reach (58 tiles for the Grappling Hook), and the executor's `HookStart` only cast when ONE cast "beats" the walk, so a 40-tile face gave `plan Route ... gain 0.2T beats=False` three times and a ban (zoo and wall40: 0/13).
- A chain that did start broke at the top: `TryChain` asked for the highest anchor, but a body hanging against a face latches only about half way up the fan's steepest rays (the hook's 10 px box touches the face half way), the fan has 24 rays 7.5 degrees apart, and the lip tile itself is never hit. "No higher anchor" ended the chain with a release that fell the whole way (wall20: 11 falls of 20 tiles, 9/13 arrive). The chain was also capped at 8 links, and its progress test was straight-line distance to a goal that is 150 tiles down the road, so a link that gains 2 tiles up looks like nothing.
- Two-hook items (Dual Hook, Skeletron Hand) cast a pair whose second hook missed and stood stranded (zoo Z1: 0/13).
- No edge crosses a gap wider than a cast (chasm 14/20/30: 0 casts, 0/13). The far lip has no anchor above it, but the far wall below the lip has plenty.
- The route search accepted a fall of 19 tiles and the executor 19 (a 20-tile drop is inside the 25 free tiles): a mesa with a 20-tile drop was a dead end (Z5).
- Wall climbing is slow in clock time only because the casts are: a link is about 40 ticks (cast flight 10 to 30, reel 10), and gains about half the reach.

## What changed
1. `HookPlanner.PlanClimb` (one link of a climb) and `DirectedAnchors`: rays aimed at the exposed latchable tiles around the body that lie nearest the goal (up to 48), the way a player aims at the lip, plus the fan. A hanging link needs no safe drop (a body 80 tiles up a face has none). Progress is `ClimbDist` (height still to make up plus 0.3 x the way across, capped), so a far goal does not hide a gain in height. A climb never casts at the wall behind it, and casts one hook at a time (a pair that does not both latch strands the body).
2. Chain climbing (`SoldierNPC.Hook.cs`): `HookStart` falls back to a climb when no single plan beats; each arrival casts the next link at once (up to 160 links or 150 s, ended early after 8 links without getting nearer); within reach of the goal the link is a landing plan. HOOK-JUMP: when no anchor gains ground the body jumps off the hook (the full jump this body makes from the ground, extra jumps refreshed as vanilla does on release), and casts again in the air once it falls short of the goal. An arrived release is also a full jump. An extra jump (cloud in a bottle) is spent at the top of the arc when the ledge is still above. A cast that came down where it started waits longer each time.
3. Planner (`PathSearch`): the chain edge up a face has no height limit (cost 8 + 1.3 per tile), `GapChain` crosses a gap by casting at the far wall below its lip and climbing it (a far wall up to 4 tiles beyond the cast's reach is allowed: the executor jumps out over the gap first, `TryJumpCast`), and a route may take a fall of exactly 20 tiles (executor too).
4. Lab: `wall` course, `hooks` gear, `LEVY_TRAVEL_NOHOP`, per-levy hook counters, `HK` event lines, `hookstats`.

## After (arrivals; time = mean seconds of those that arrived)
| set (13 hook levies per run) | before | after |
|---|---|---|
| wall 10 / 20 / 40 / 80 / 150 tiles | 12 / 9 / 0 / 0 / 0 of 13 (28 s, 25 s) | 13 / 13 / 13 / 13 / 13 (20, 19, 22, 24, 27 s), falls 0 to 1 |
| chasm 14 / 20 / 30 wide, 60 deep | 0 / 0 / 0 of 13 | 13 / 13 / 13 (22, 22, 19 s) |
| zoo Z0 / Z1 / Z2 / Z3 / Z5 (cliff 10, crack 6, chasm 14, chasm 20, cliff 20 and drop 20) | 12 / 0 / 12 / 0 / 0 | 13 / 12 / 13 / 13 / 13 |
| corruption C0 / C1 / C2 / C3 / C4 | 12 / 13 / 0 / 13 / 13 | 12 / 13 / 11 / 13 / 13 |
| real worlds 800, commander on screen (10 runs) | 11/130 (8%) | 20/130 (15%) |
| real worlds 150, commander on screen (30 runs) | 251/520 (48%) | 281/520 (54%) |
| real worlds 800, commander far (hops) | 117/130 (90%) | 111/130 (85%) |
| mixed gear, commander on screen, worlds + courses (58 runs): bare / boots / wings / Grappling Hook | 142/290, 121/232, 139/174, 100/174 | 142/290, 123/232, 139/174, 120/174 |

Cost (whole runs, route planner slices of 700 nodes or 1.6 ms): 800-tile on-screen set 1.55 to 1.67 ms per slice (slowest 9.7 to 12.9 ms, loaded machine); 150-tile set 1.43 to 1.54 ms (13.0 to 18.5 ms); the mixed set 0.87 to 0.94 ms (13.1 to 24.6 ms). A hook plan costs 0.87 ms before and 0.55 ms after on average (more plans, 5.3k to 7.9k, because every climb link plans; INFERRED: a link simulates only the hang, not the ballistic releases). Hook tick and projectile time over the 800-tile set: 1.2 s to 2.6 s for ten runs of 13 levies (about 0.02 ms per hook levy-tick).

## Reading it
- OBSERVED: any wall height and chasms up to 30 wide are crossed by every vanilla hook, with the commander on screen and no hops, in 19 to 27 s and with no falls. Z5 (cliff 20 then a 20-tile drop) is 13/13.
- OBSERVED: real worlds with the commander on screen improve modestly (8% to 15% at 800, 48% to 54% at 150). The levies now reach the next obstacle and climb it, then meet the next one; a failed edge in a long chain of edges still ends the trip (route bans, a pocket the climb cannot top). Stuck lines of the 800-tile runs: a 70-tile shaft where the local wall trigger repeats a 21-tile cast that lands lower than it started (waits longer each time since the last fix, not solved), and pits whose only way out is a ledge no anchor tops.
- OBSERVED, a regression: with the commander far, hook levies arrive 111/130 where hops alone gave 117/130. The three worst runs are levies that keep casting inside a pit (they move more than 5 tiles each time, so the catch-up hop never counts them as stuck). INFERRED: letting the hop see a levy that has gone 20 s without getting nearer would give the arrivals back; not done.
- Static Hook (steered, 6 px/tick) climbs and crosses like the others after the planner stopped refusing it, and pairs are not cast in a climb.

## Round 4 addendum: catch-up hop on progress, fall limit, loop backoff, lava
- Catch-up hop: stuck now also means "the nearest the body has been to the goal has not improved by 8 tiles in 15 s" (`CatchUpNoProgressTicks`), and a hook levy in Cooldown can hop; a body looping casts in a pit used to move too far from one spot to count. Loop backoff (a cast that comes down within 4 tiles of where it started waits longer each time) stays at 4 tiles: at 8 tiles it also caught climbs out of an 8-deep hole (corruption C0 12 to 8 of 13).
- Fall limit: the route search and the executor both take a drop of exactly `FallScanTiles` (20 tiles, inside the 25 free ones); `SoldierNPC.ScanAhead` extends the scan one row for a body on a planned route, so no planned drop is refused. Wings keep their own rule.
- Final numbers (before = branch head plus harness; after = this branch; hooks arm = 13 levies): real 800 commander far, hooks 117/130 to 122/130, mixed 133/150 to 141/150 (hook 26/30 to 29/30); real 800 on screen, hooks 11/130 to 18/130, mixed 46/150 to 56/150; real 150 on screen, hooks 251/520 to 277/520; mixed worlds + courses on screen (58 runs) bare 142 to 141 of 290, boots 121 to 121 of 232, wings 139 to 140 of 174, Grappling Hook 100 to 120 of 174.
- Lava (course `lava`, `lavatab.py`): V0 lava lake, V1 8-wide gap, V2 pit, V3 3-wide pool, V4 40-deep shaft, V5 island over a lava sea. OBSERVED: following a route or a map order, nobody ever entered lava (0 entries in V0 to V5, all gear): the planner and `ScanAhead` already refuse lava cells and drops onto lava. Levies end up in lava when they are moved: dropped into the shaft (9 of 13 hook levies, 5 of 9 bare and boots died) and knocked (7 px/tick shove toward a pool: all 15 died in the 3-wide pool, 3 winged died over the lava sea).
- Changed: a falling hook levy whose fall ends in lava or void casts at the nearest anchor that stops the fall when no landing is reachable (`TryFallHang`, then the chain climbs out): shaft drop, hook arm 9/13 died to 0/13 (4 to 13 arrive), mixed hook 2/3 to 0/3. A body with no hook steers in the air toward the side with less lava and spends an extra jump (`AirLavaGuard`); a body in lava swims up and toward the nearer bank (`LavaEscape`); route search takes no fall, jump, hook or climb landing beside lava. Winged bodies keep their own flight logic (a flap added from the guard made them enter lava in the gap run, so it is off for them).
- Not fixed (OBSERVED): bare and boots levies dropped into the shaft, any levy shoved into a 3-wide pool, and winged levies shoved over a lava sea still die; there is no action that saves them in the time they have.
