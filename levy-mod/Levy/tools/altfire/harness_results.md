# Alt-fire harness results (Calamity, 16 weapons)

Question: for a levy holding a weapon with a right-click, is it better to use only the left click, to use the alt 80% of the time as today, or to follow a per-weapon policy?

## Setup

* New toolchain (tModLoader 2026.10.2.3) with Calamity 2.2.2, **private** copies: `/home/user/tml-bin/tml-alt3` + `/home/user/tml-save-alt3` (boss runs) and `tml-alt4` + `tml-save-alt4` (pack runs). Nothing under `/root/.local/share/Terraria/tModLoader/Mods` was written.
* `tools/run_harness.sh -N -m CalamityMod -c -r 15 -t 420`, `LEVY_HARNESS_NOBASE=1`. Runner scripts: `harness/run_altfire_trial.sh`, `harness/run_altfire_sample.sh`.
* The soldier fires through the real `WeaponUse.TryAltAttack`. `Common/Testing/AltPolicyLab.cs` (a `GlobalItem`, active only with `LEVY_HARNESS` and `LEVY_ALT_MODE`) answers the alt's `CanUseItem`:
  * `left`: every alt vetoed, so the soldier fires only the left-click attack.
  * `random`: every alt let through, so today's behaviour (0.8 dice, 75-105 tick gap).
  * `policy`: the item's row in `calamity_altfire_items.tsv` decides.
* Metric: `damageToTarget` of the **walk-in trial** (1300 ticks, soldier walks into range and fights; the larger sample). The 15-tile trial (420 ticks) is in the raw files and agrees in direction. Damage counts every pinned target's life drop (the target is reset each tick).
* Scenarios: **boss** = one pinned target with a million life, `LEVY_ALT_TOUGH=1`; **pack** = six pinned slimes 2.5 tiles apart, `LEVY_ALT_TOUGH=0`.
* Weapons (16, one per interesting pattern): Animosity, Blossom Flux, Corinth Prime, Face Melter, Frigidflash Bolt, Hydra, Leviatitan, Nullification Pistol, Short Circuit, Plasma Caster, Plasma Rifle, Seas Searing, Septic Skewer, Speed Blaster, Veering Wind, Vesuvius.

## Results (walk-in trial, damage dealt)

#### Boss: one tough target at 15 tiles (`LEVY_ALT_TOUGH=1`)

| Weapon | left only | 80% random alt | proposed policy | random / left | policy / left |
|---|---:|---:|---:|---:|---:|
| Animosity | 5885 | 7568 | 9471 | 1.29 | 1.61 |
| Blossom Flux | 5380 | 5457 | 5343 | 1.01 | 0.99 |
| Corinth Prime | 32541 | 6968 | 33415 | 0.21 | 1.03 |
| Face Melter | 29316 | 22285 | 28192 | 0.76 | 0.96 |
| Frigidflash Bolt | 0 | 0 | 0 | n/a | n/a |
| Hydra | 2469 | 2322 | 2408 | 0.94 | 0.98 |
| Leviatitan | 6631 | 8144 | 7928 | 1.23 | 1.20 |
| Nullification Pistol | 9996 | 9223 | 9124 | 0.92 | 0.91 |
| P-TSL: Short Circuit | 1384 | 1258 | 1366 | 0.91 | 0.99 |
| Plasma Caster | 30262 | 23222 | 30348 | 0.77 | 1.00 |
| Plasma Rifle | 9043 | 6208 | 8856 | 0.69 | 0.98 |
| Sea's Searing | 6311 | 6649 | 6991 | 1.05 | 1.11 |
| Septic Skewer | 40637 | 56187 | 60016 | 1.38 | 1.48 |
| Speed Blaster | 3276 | 3908 | 4131 | 1.19 | 1.26 |
| Veering Wind | 263 | 301 | 316 | 1.14 | 1.20 |
| Vesuvius | 0 | 2428 | 2430 | n/a | n/a |

#### Pack: six non-tough targets 2.5 tiles apart (`LEVY_HARNESS_CROWD=6`, `LEVY_ALT_TOUGH=0`)

| Weapon | left only | 80% random alt | proposed policy | random / left | policy / left |
|---|---:|---:|---:|---:|---:|
| Animosity | 5658 | 9698 | 9351 | 1.71 | 1.65 |
| Blossom Flux | 5418 | 5858 | 5408 | 1.08 | 1.00 |
| Corinth Prime | 32847 | 8301 | 33235 | 0.25 | 1.01 |
| Face Melter | 35448 | 29244 | 35020 | 0.82 | 0.99 |
| Frigidflash Bolt | 0 | 0 | 0 | n/a | n/a |
| Hydra | 2258 | 2256 | 2139 | 1.00 | 0.95 |
| Leviatitan | 6900 | 9228 | 9007 | 1.34 | 1.31 |
| Nullification Pistol | 44681 | 28272 | 31219 | 0.63 | 0.70 |
| P-TSL: Short Circuit | 1845 | 1699 | 1722 | 0.92 | 0.93 |
| Plasma Caster | 39172 | 27210 | 36806 | 0.69 | 0.94 |
| Plasma Rifle | 23534 | 16189 | 24010 | 0.69 | 1.02 |
| Sea's Searing | 6414 | 7368 | 7234 | 1.15 | 1.13 |
| Septic Skewer | 45140 | 61935 | 62621 | 1.37 | 1.39 |
| Speed Blaster | 3433 | 3669 | 4212 | 1.07 | 1.23 |
| Veering Wind | 179 | 221 | 208 | 1.23 | 1.16 |
| Vesuvius | 0 | 2425 | 2965 | n/a | n/a |

### Summary

Mean of the per-weapon ratio to left-only (13 weapons with a non-zero left-only baseline, Hydra and Frigidflash excluded):

| Scenario | 80% random alt / left | policy / left | policy / best of (left, random) |
|---|---:|---:|---:|
| Boss, walk-in | 0.97 | 1.13 | 1.02 |
| Boss, 15 tiles | 1.05 | 1.19 | 1.00 |
| Pack, walk-in | 1.00 | 1.11 | 0.96 |
| Pack, 15 tiles | 1.08 | 1.24 | 0.97 |

What it says:

1. **The 80% dice are neutral on average and wrong per weapon.** Across the sample the random alt is exactly as good as no alt (mean ratio 0.97-1.08), but individual weapons swing from 0.21x to 1.71x. A policy needs per-weapon data; there is no situation-only rule that fixes this.
2. **Alt wins** (use it often or always): Septic Skewer (+37-38%, rip out harpoons), Animosity (+29% boss, +71% pack), Leviatitan (+23% / +34%), Speed Blaster (+19% / +7%, already self-gated by its cooldown), Seas Searing (+5% / +15%), Veering Wind (+14% / +23%, tiny absolute damage), Vesuvius (left-click did 0 against the pinned target; the alt did ~2.4k).
3. **Alt loses** (turn it off): Corinth Prime grenade (-79% boss, -75% pack; six bullets per 30 ticks beat a 90-tick grenade even against a pack), Plasma Caster turbo (-23% / -31%), Plasma Rifle rapid mode (-31% / -31%), Face Melter amplifier (-24% / -17%), Short Circuit hook (-9% / -8%), Nullification Pistol polarity swap (-8% / -37%: a toggle costs a shot).
4. **The situational labels were partly wrong.** I expected "AOE alt only vs crowds" and "turbo alt always inside range" to help. They did not: the grenade lost in a six-enemy pack, the turbo modes lost everywhere. The policy rows were rewritten from the numbers (`evidence` column in `calamity_altfire_items.tsv`). The situation gates (`Tough`, `Crowd`) are still right for weapons where the alt is a good move in only one situation; the sample did not find one that needed them, because targets were pinned and there was no incoming fire.
5. **Policy tracks the best of left/random** (0.96-1.02 of the oracle) and is above left-only by 11-24% on average with no weapon more than 10% under left-only except Nullification Pistol (-9% boss / -30% pack under `TOGGLE_N afterShots=12`; now set to `NEVER`).

### Caveats

* Single runs per cell. Run-to-run noise is large (Animosity under `ALT_MIX p=0.8` read 9471 in the boss run against 7568 for the 80% random run, which is the same configuration), so differences under about 25% are not significant. The big effects (Corinth, plasma, Septic Skewer, Animosity, Vesuvius) are.
* The policy rows were tuned on these same scenarios, so the policy columns are in-sample. They are a per-weapon choice between two measured behaviours, which is robust, but they are not a held-out test.
* Hydra: the alt is performed inside `AltFunctionUse` itself (it launches every head and returns false), before the harness can veto it. Its three columns are the same behaviour; the veto is not testable without moving the gate before the probe.
* Frigidflash Bolt: 0 damage in all modes. Its ball steers to `CalamityPlayer.mouseWorld`, which the dummy never has. It is a finding, not a measurement.
* One stationary or walking target, no incoming fire, no armour. `GUARD`, `METER_READY`, `HOLD_VARIANT`, `MANA_REFILL`, `ALLY_HEAL`, `PORTAL_USE` and `DASH_CLOSE` need either a hook or a scenario with threats and allies and were not run. `HOLD_VARIANT` (setting `mouseRight`) and `TOGGLE_N` are implemented in the lab but not in this sample.
* Nullification Pistol's final row is `NEVER`; the policy column above is from the run with `TOGGLE_N afterShots=12`.

### Reproduce

```
# build the mod into a private save dir, then:
tools/altfire/harness/run_altfire_sample.sh 3 boss left random policy
tools/altfire/harness/run_altfire_sample.sh 4 pack left random policy
```
Raw rows: `harness/{boss,pack}_{left,random,policy}.tsv` (the `notes` column ends with `ALT[mode]: item asked= pass= veto= prim=` counters).
