# Levy lab kit (levylab)

A lab is a short spec file plus one command. Do not read the C# harness, copy a tML install or write run/table scripts: this kit already does it.

```
levy-mod/lab/levylab list                          # every spec field, token, env toggle and preset, one line each
levy-mod/lab/levylab run my_lab.yaml               # build once per git revision, run every cell x seed, print one table
levy-mod/lab/levylab table /home/user/lab/kit/runs/<run>   # re-print a finished run (--cols potionsDrunk,rageAct  --wide)
```
Defaults give a result from three lines: `boss: eow` + `gear: postskel.best` runs Master+Death, arena a3, cmd near, order follow, 6 seeds, 180 s cap.

- **Spec** (yaml): `boss mode arena gear potions stations levies hp cmd order cap seeds tokens env vs grid cells` (see `list`). `vs:` is A/B: each arm = the base plus its overrides (`env:` toggles such as `LEVY_WORMDODGE: 0` merge). `grid:` makes a cross product. A `.tsv` spec is a header row of fields with one cell per row, or raw harness `fight` lines.
- **Presets** are data in `lab/presets/*.yaml`: gear = tier x quality (`preevil|posteow|postskel|postwof` . `best|okish`), plus variants and `legacy.dl*` (the original C# roll). Add a preset by editing yaml; the army is rolled per seed in Python and loaded by the harness through the `lv=` hook. Missing item names are dropped and listed under the table.
- **Same seed = same army** in every arm, so A/B arms are paired. The table prints a Fisher p-value of each arm's wins against the first cell; with 6 seeds treat p > 0.1 as noise (the sim is chaotic).
- **Table**: wins/N, kill time median (range), levy deaths per fight, first death, boss HP left (losses), damage taken by head/body/projectile, damage dealt by class, and `pts`.
- **pts = army points** (point-buy auto-battler seed): levies x (per_levy + potions) + HP/100 x per_100hp + summed WeaponRank/1000 x per_rank_1000 + stations. Weights are the `cost:` blocks in the presets (or a spec-level `cost:`). Read the cheapest cell that still wins.
- **Speed**: no recordings unless `--rec K` (K seeds per cell, own processes), early stop on a win or a wipe, the .tmod is cached per commit under `/home/user/lab/kit/cache` (uncommitted edits get their own hash; `--rev` builds a git revision), one persistent private install, up to `--jobs` (default CPUs-1) server processes with >= `--per-proc` (8) fights each. A server process costs ~55 s to boot (mod JIT), a fight ~10 s, so batch many cells into one run. One run = one machine slot set: do not run two labs at full `--jobs` at once.
- **Output** in the run dir: `results.csv` (one row per fight), `cells.json`, `jobs/*/try*/` (harness log, `server.log.gz`, raw sidecars), `rec/`, `armies/`. Never touch /home/user/tml-bin or /home/user/tml-save-*.
- **New C# knobs**: tokens are listed in `list`; a new token needs a line in `TOKENS` in `levylab` and nothing else.

## Aim / drink lab (`tipsy:` spec)
`specs/tipsy_main.yaml` is the pattern: a `tipsy:` section (weapons with class and ammo, `dist`, `motion` static|strafe, `size` small|boss, `arms` such as `{sober: {}, tipsy: {b: ale}, x2.5: {x: 2.5}}`) expands to one cell per combination, seeds are the rows. One levy fires at a pinned or strafing dummy; the table is damage per levy-second per arm with the hit rate. `b:` is a real drink buff (ale, stout, rum), `x:` multiplies aim error through the aim-accuracy seam. `tipsy_summary.py main|sweep|drinks RUN_DIR` pools the results by weapon group. See `docs/tipsy/RESULTS.md`.

## Lab brief template (paste into a builder brief, fill the blanks)
```
Lab: <question in one sentence>.  Use levy-mod/lab/README.md; do NOT read the harness C# or write run scripts.
1. Write levy-mod/lab/specs/<name>.yaml: base = <boss, mode, gear preset, levies, hp>; vs: arms = <the one thing that differs>. seeds: 6 (12 if close).
2. levy-mod/lab/levylab run levy-mod/lab/specs/<name>.yaml ; if a preset or token is missing, edit presets/*.yaml or TOKENS (data only).
3. Paste the table, then say in 3 lines: result, whether it is more than noise (p), and what Eve should decide. Keep notable fights with --rec 1.
4. Commit the spec and a 5-line result note under levy-mod/docs/boss_lab/<name>/. Report in under 10 lines.
```
