# Lab kit proof and demo (levylab, build on branch labkit)

## Proof: deathladder cell C best re-run with `lab/specs/deathladder_c.yaml` (Master+Death EoW, 16 levies, 400 HP, 6 seeds)
Original (deathladder/RESULTS.md): 6/6 wins, kill 24-36 s, 1-2 levy deaths per fight, first death 11-25 s.
| cell | levies | pts | wins | kill s (med) | levy deaths | 1st death s | taken head/body/proj % | dealt R/M/E/S % |
|---|---|---|---|---|---|---|---|---|
| ctl (data preset postskel.best) | 16 x 400hp | 336 | 6/6 | 27 (22-29) | 1.2/16 | 18 | 77/21/0 | 31/49/10/10 |
| legacy_c (original C# roll, dl=C) | 16 x 400hp | 339 | 6/6 | 26 (20-31) | 1.7/16 | 19 | 77/22/0 | 27/49/15/10 |
Matches within noise. The data preset is a Python port of the C# roll; pierce weighting is approximated by hand marks (`*`) in `presets/gear.yaml`.

## Demo A/B: `lab/specs/demo_ab.yaml` (10 levies, post-Skeletron, 6 seeds per arm)
| cell | levies | pts | wins | kill s | levy deaths | 1st death s | boss left |
|---|---|---|---|---|---|---|---|
| ctl best | 10 x 400hp | 225 | 6/6 | 34 (30-50) | 2.7/10 | 17 | - |
| hp250 | 10 x 250hp | 197 | 1/6 (p=0.02) | 34 | 8.8/10 | 13 | 8.5k (30%) |
| okish | 10 x 340hp | 189 | 0/6 (p=0.00) | - | 10.0/10 | 9 | 24.2k (84%) |
Reading: at 10 levies the best kit wins 6/6; trimming 150 HP saves 28 points and drops to 1/6; a normal player's kit loses every fight. Points are provisional weights.

## Speed (18 fights, demo spec)
old-style (1 server process, recordings on, 20 ticks/turn): 186 s, 5.8 fights/min, 74 MB of recordings. kit (2 processes, no recordings): 129 s, 8.4 fights/min, 0.3 MB. A server process boots in ~55 s (mod JIT) and a fight takes ~10 s, so the gain grows with batch size; ticks per turn (`--steps`) and .NET JIT flags made no difference; boot self-checks cost ~3 s of the boot, not worth a flag.
CSVs: `proof_deathladder_c.csv`, `demo_hp_gear.csv`.
