# Boss lab: running results (branch `boss-lab`)

Recovery file. If the agent dies, the next one reads this, `LOADOUTS.md`, and `git log` on `boss-lab`.
Harness: `Common/Testing/CombatHarness.CalRev.cs` + `CombatHarness.CalRevLab.cs` (scenario `calrev`, Calamity 2.2.2 on tML 2026.10).
Raw rows: `docs/boss_lab/raw/*.tsv` (one row per fight). Table generator: `docs/boss_lab/tab.py`. Run scripts: `docs/boss_lab/scripts/`.

## Conventions
- Result: `WIN` = the boss (all worm segments) died with levies alive or not = KILL; `DESPAWN` = the boss left without dying; `TIMEOUT` = cap reached; `LOSS` = every levy dead.
- Cell = boss / mode / gear / arena / levy count, 3 seeds minimum. Seed = same army across modes.
- Modes: exp = Expert, rev = Expert+Revengeance (levy Rage/Adrenaline on), revoff, death = Rev + Death.
- Boss target % is sampled every tick over every head (boss) / add NPC: L = a levy (slot 255 or 300+), P = a player slot (the Spectator commander), N = none.
- Levy count `n=cp` = the real command-point count for that fight (Levy's own tier math, defaults 10 at the start, 20 at Wall of Flesh): KS 10, DS 11, Eye 11, EoW/Brain 13, QB 14, Skeletron 14.

## Status log
(newest last)

- 04:50 setup done. Private installs `/home/user/lab/{tml1,tml2,tmlb}`, saves `/home/user/lab/save{1,2,b}`, scripts `/home/user/lab/{build,run}.sh`.
- Smoke (1 seed, not results): Eye Rev boss target is the PLAYER for ~50% of ticks even with the Calamity targeting fix (vanilla boss trials: 0%). Under investigation, see anomalies.
