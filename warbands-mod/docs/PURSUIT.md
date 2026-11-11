# Pursuit lab: before and after

Bands that cannot hit their foe from where they stand used to wait there. They now go to a firing position over, under or around the terrain. This file is the measurement. `README.md` describes the behaviour, `SEAMS.md` the one Levy seam it needed.

## What was wrong

Levy's long-route planner (walking, drops, grappling-hook casts and chains up a face, wall climbs, wing arcs) was reachable only from the map-march order. A band member whose foe was out of reach of the brain's local search had no way to ask for a route: it pressed against the nearest wall and stood. Three causes, all reproduced by the lab:

- A foe under an overhang, across a chasm or up a tower was never acquired (no clear line), so the member had no goal at all.
- The first-contact posture picked a spot around the foe, found none when the foe was high or walled in, and stopped for good.
- Members that did walk went straight at the foe or at a spot they shared, so a band arrived as one stack.

## What changed

- `BandLevyNPC.Pursue`: a member with no shot at its foe for 1.25 s (or no target at all) picks a firing position and routes to it through Levy's planner. A shooter wants a standable spot in view of the foe, inside its weapon's reach, on higher ground when there is some, 6 tiles or more from the other members' spots, on the side of the foe its place in the band suggests. A blade wants the ground beside the foe, odd members on the far side. A spot is kept while it still gives a shot, given up (and banned for 12 s) when the planner has no route to it. Four failures in 20 seconds mean the member is walled in; it stops asking for 10 s and spreads out along the ground it has.
- Last resorts for a member confined to one place for 12 s with no shot: out of every player's sight it takes the hop a levy takes (same landing rules); after 36 s in sight with no way out it drinks its Recall.
- Every member carries a grappling hook from the first boss on (one in two before it). It was one in three from the first boss.
- Bands form three tiles apart (two before).
- Natural spawns: `NaturalChancePercentPerMinute` 12 (was `SpawnChancePercentPerMinute` 4; renamed so a saved config does not keep the old figure). `NoSpawnsDuringBosses` true: no new natural band while any `NPC.boss` is alive. Summoned and event bands are not held back, bands already out stay and fight.

## The lab

`WARBANDS_LIVE=move WARBANDS_SEEDS=18`. Terrain is built in the sealed arena, the stand-in player cannot hurt the band, three band types take turns (Forest Deserters, Meteor Marauders, Mythril Mercenaries), 40 seconds per trial, 18 trials per scenario. Raw rows: `pursuit_lab_before.tsv` (the code before this branch, bands forming two tiles apart as the spawner did) and `pursuit_lab_after.tsv`.

- Hit: a hostile shot reaches the player's hitbox, or a blade lands (a dedicated server runs no player-side projectile hits, so shots are counted in the lab).
- Firing position: a clear line to the player inside the member's own weapon reach, a blade within 3.2 tiles.
- Stuck: stayed within 3 tiles sideways and 7 tiles up or down of one spot for 10 s with no firing position. A member waiting its turn at the band's attack token counts too, so the figure is an upper bound.
- Distance between members: mean over the trial from second 5, in tiles.

| Scenario | Bands hit the player (before / after) | Median time to first hit | Bands with a firing position in 30 s | Members stuck over 10 s | Mean distance between members (tiles) |
|---|---|---|---|---|---|
| Open ground (control) | 18/18 / 18/18 | 1.0 s / 0.8 s | 100% / 100% | 20 of 81 (25%) / 0 of 81 (0%) | 19.2 / 19.9 |
| Open ground, player running | 18/18 / 18/18 | 1.1 s / 1.4 s | 100% / 100% | 0 of 81 (0%) / 0 of 81 (0%) | 16.1 / 17.5 |
| Player under an overhang | 0/18 / 18/18 | never / 2.0 s | 0% / 100% | 27 of 81 (33%) / 7 of 81 (9%) | 4.4 / 12.4 |
| Player across and above a chasm | 0/18 / 18/18 | never / 5.5 s | 0% / 100% | 81 of 81 (100%) / 6 of 81 (7%) | 3.9 / 20.7 |
| Band spawned in a pit | 12/18 / 18/18 | 7.6 s / 3.0 s | 67% / 100% | 58 of 81 (72%) / 7 of 81 (9%) | 20.6 / 25.2 |
| Band in a pit, out of every player's sight | 12/18 / 18/18 | 7.1 s / 7.9 s | 67% / 100% | 53 of 81 (65%) / 1 of 81 (1%) | 33.2 / 36.8 |
| Player on a 34-tile tower | 0/18 / 18/18 | never / 4.3 s | 0% / 100% | 81 of 81 (100%) / 33 of 81 (41%) | 3.6 / 21.2 |

Medians are over the bands that were hit. The control rows show the open-ground fight is unchanged: the first hit comes at the same time and no member idles. The player-running row is a player moving 25 tiles each way in 15 seconds.

Fight lab (`WARBANDS_LIVE=fight`, 4 seeds, Expert) and event lab (`WARBANDS_LIVE=events`, 4 seeds): damage to the stand-in rose or held in every stage (mean over stages 74 to 117) and all 16 event runs pass, so the pursuit did not soften a band.

## What is still weak

- A band of members with only a hook (Meteor Marauders, stage 1) climbing a sheer 34-tile face: about half the members arrive. Levy's route planner plans the chain of casts and its executor sometimes fails a cast at a given start position; Warbands gives the spot up and tries the next, then walls the member in for 10 s. A fix belongs in Levy's hook executor.
- A member with no hook and no wings (a pre-boss member in a pit, across a chasm or at a tower's foot) cannot leave by itself. In sight it stays until its Recall (60% carry one); out of sight it hops.
- Pit and tower rows count members that wait at the attack token as stuck.
