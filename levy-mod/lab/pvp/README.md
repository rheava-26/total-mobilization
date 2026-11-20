# PvP lab: Eve's levies against the optimal levies, fighting each other

Lab only. Two levy armies face each other in arena a3 (flat floor, five platform rows), Master + Death, no boss. Results: `RESULTS.md`.

## How it is fair
- Both armies are ordinary levies of the same stand-in commander: the same brain, movement, weapon use, gear rules (lab-kit `lv=` loadouts, same accessory slots), stations, buffs, potion rule and healing. Nothing is a band, nothing runs the hostile seam.
- A lab-only team flag (`Common/Testing/PvpLab.cs`) makes each army the other's enemy. The game's own enemy tests (vanilla projectile hits, homing, minion and whip targeting, Levy's targeting and dodging) read `NPC.friendly`, so the lab flips that flag by perspective: while a levy or a levy's shot of army 1 updates, army 2's levies are non-friendly, and the other way round. Between updates every levy is friendly again, as in a game.
- Hits land through the game's normal strike (the shooter's own stats, crits, the target's player-mode defense and damage reduction), and a levy hit by the other army gets the levy's own player-style hit window and after-hit bookkeeping (immune ticks, fear, barks, hurt-driven gear), as from an enemy's hit. `LEVY_PVP_IFRAMES=0` turns the window off.
- Mirror: every matchup runs with each army on the left (team 1, spawned first, first in the update order) and on the right. Seeds 1-5 on each side.
- Order Search & Destroy for both (both armies hunt). Fronts start 70 tiles apart. The commander stands at the arena centre, invulnerable under the Spectator buff, and takes no part. The whole arena counts as watched, so no levy takes the off-screen hop.
- Fight ends when one army is wiped (cap 180 s: more levies standing wins). Dead levies do not respawn.

## Run
```
python3 levy-mod/lab/pvp/mkarmies.py <prov_1014.levyrec> <calnames.json>   # eve_prov_army.json + the two current-tier optimal armies (battery/calnames.py makes calnames.json)
python3 levy-mod/lab/pvp/mkspec.py levy-mod/lab/pvp/specs/pvp_all.tsv --seeds 1-5 --cap 180
LEVYLAB_HOME=... levy-mod/lab/levylab run levy-mod/lab/pvp/specs/pvp_all.tsv --rec 99 --jobs 1
python3 levy-mod/lab/pvp/collect.py <run dir> --dest <recordings dir>     # tables + <A>_vs_<B>_seed<N>.levyrec
```
Harness line: `fight pvp MODE SEED CAP lv=A.json lvb=B.json ta=LABEL tb=LABEL order=sd arena=a3`. Rows: `out.tsv.pvp.tsv` (winner, time, survivors, damage by class, top weapons, death times, per levy). Recordings (`LEVY_REC_DIR`) hold both armies as levies with names (`<army> <class><n>`) and gear; the header's meta names the armies, their sides and their levy ids.
