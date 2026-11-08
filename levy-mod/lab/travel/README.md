# Travel lab (A to B)

Measures how well levies get from one point to another: a map-point order (Barracks "Send to map point", Command Radio shift-click) or Follow behind a commander who walks the same route.
It is a harness scenario (`LEVY_HARNESS_SCENARIO=travel`, `Common/Testing/CombatHarness.Travel.cs`) plus small scripts here. It is separate from the boss lab (`levylab`) because it needs long courses, not arenas.

## What it records (per levy, `out/levies.csv`)
arrived / arrival time, straight line vs tiles actually walked (`ratio`), time stuck (no 4-tile exit from a spot for 3 s, only while still travelling), stall (best progress toward the goal not improved for 10 s), where (course feature / world x),
teleports (a jump of more than 6 tiles in one tick), falls of 8+ tiles, seconds wet, body lost, and the order/state it ended in. `events.txt` has every STUCK / TELEPORT / FALL / "order ended" with the movement reason, an ASCII map around the first stuck levy of each gear, the route-planner log and cost counters.
`trace.csv` is a position every half second (x, elevation, state); `surface.csv` the standing surface of the world (world courses); `plot_svg.py` draws them.

## Courses (`LEVY_TRAVEL_COURSE`)
- `walk` (default): carved 960-tile surface course. Hills with slopes, a 3-tile ledge, a 14-wide 12-deep dip with a stair out, a forest, a pond, a house with two doors, a mountain with a tunnel, a 62-wide 7-deep lake, a 20-wide chasm with a platform bridge, rough hills. Camps (flat) at offsets 20, 170, 320 ... 920 so a 150-tile leg starts and ends on flat ground.
- `zoo`: one obstacle per 150-tile leg, mostly impassable on foot on purpose: 10-high cliff, 6-wide crack, 14-wide and 20-wide chasms, 12-deep lake, 20-high cliff. Shows what each gear can do.
- `world`: the REAL generated terrain of a world file (`LEVY_TRAVEL_X0` = start column, `FROM`/`TO` are offsets from it, negative goes left). This is the one that reproduced the report; use generated worlds (`gen.sh`), not only carved courses.

## Run
```
export TRAVEL_HOME=~/travel-lab                       # scratch: save/Worlds (copy LevyTest.* and generated M1/M2 here), save/ModSources, tmod/levy.tmod, runs/
./gen.sh 2 11111 M1 7901 ; ./gen.sh 2 22222 M2 7902  # medium worlds, seeds used for the numbers in docs/travel_lab
COURSE=world WORLD=M1 X0=700 ./run.sh tmod/levy.tmod name map 0 800 mixed 15 9000     # tmod name mode from to gear n cap(ticks)   mode: map | follow
LEGACY=1 ./run.sh ...                                 # the same build with the long-trip fixes off (the "before")
./batch.sh $TRAVEL_HOME/tmod/levy.tmod TAG 3          # walk + zoo, long and 150-tile legs, map and follow
./batchW.sh $TRAVEL_HOME/tmod/levy.tmod TAG 3         # two real worlds x five starts: 800 long, follow, three 150 legs
python3 analyze.py TAG [--gear]                       # one table
python3 plot_svg.py out.svg title run_dir [run_dir ...]
```
Gear (`mixed`, 15 levies): 5 bare, 4 Hermes Boots, 3 Angel Wings, 3 Grappling Hook. `LEVY_TRAVEL_FOES=1` adds day-time monsters ahead of the army and gives them handguns; `LEVY_TRAVEL_PLAN=1` also prints what the route planner finds for each ability class.
The headless server runs the sim far faster than real time (3000 ticks in about 4 s); one run costs the 20 s boot plus a few seconds.
