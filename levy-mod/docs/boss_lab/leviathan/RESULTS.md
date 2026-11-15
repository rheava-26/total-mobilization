# Leviathan and Anahita: do the levies dodge the charge? (2026-11-15, branch `leviathan`)

Eve: "levies are comedically bad at fighting leviathan and anahita. the dash from leviathan murders them all instantly."

Calamity 2.2.2 on tML 2026.10, Master+Death and Expert+Revengeance, ocean arena `a3@ocean` (platform arena at tile x 200, inside the ocean strip so Calamity reads the target as "in the ocean"; no water in it), 30 levies (10 ranged, 10 magic, 5 melee, 5 summoner, 400 HP, 8 healing potions, full buffs, postwof stations, every levy with wings and a Shield of Cthulhu dash), commander passive, order follow, cap 240 s, 3 seeds per cell. `on` = every weapon obtainable before the fight (Boss Checklist 12.8, just after Plantera), `mid` = one tier below. Same seed = same army in every arm. Boss stats are untouched (Levy's own boss-HP scaling applies as always). Scripts: `scripts/mklevi.py` (army + spec), `scripts/analyze.py` (per-fight tables below), `scripts/summ.py` (summary rows). Raw probe rows: `data/`. The new harness boss is `levi` (Anahita spawned through `NPC.SpawnOnPlayer`; the Leviathan joins her the way the game spawns it) with a probe in `CombatHarness.Leviathan.cs`.

"Before" = this build with `LEVY_CHARGELANE=0` (the new code off, everything else identical). "After" = default. Same .tmod, 12 fights each. The sim is chaotic: an identical build on the same seeds gave 3 and 4 wins of 6 on Expert+Rev in two runs, so read single fights as noise and the pattern across fights as the result.

## 1. What happens (OBSERVED unless marked)

**The charge, from Calamity's source** (`NPCs/Leviathan/Leviathan.cs`, state `ai[0] == 2`). `ai[1]` even is the line-up: damage is 0, the boss flies to about 900 px (less late in the fight) beside its target and within 20 px of the target's height. The moment both hold it sets its velocity once, straight at the target (20 px/tick on Expert+Rev, 22 to 29 on Death as its life drops), and `ai[1]` goes odd (damage on). The dash is a straight line with no steering. When it is about 900 px past the target it brakes (damage 0 again), `ai[1]` goes even, and if it is already level it launches back along the same line on the next tick. There is a roar at launch only, no telegraph before it. Its body is 900 x 450 px, so the swept band is 450 px (28 tiles) tall and about 3,600 px long. Anahita's dash (`ai[0] == 3` setup for 45 to 60 ticks, then 26+ px/tick for 35 to 50 ticks, body 100 x 100) is a narrow diagonal and is not what kills the army (section 4).

**Warning time.** 85 first-of-a-series dashes in the before runs: 52 of them (61%) launched within 5 ticks of the boss entering its charge state (it was already 900 px out and level while hovering); 27 had 30 ticks or more. Dashes after the first in a series have no line-up at all (it brakes and relaunches). So on most dashes there is no wind-up to read: the lane has to be known from the hover.

**What the levies did** (581 in-lane observations over 191 dashes): 65% were hit, 39% died. Leviathan contact on Master+Death is about 340 per hit against 400 HP; a hit gives 40 ticks of immunity while a 900 px body at 20 px/tick overlaps a levy for about 47 ticks, so a levy that stays in the lane takes two hits and dies. The threat field first listed the Leviathan 1 tick after launch (its damage is 0 in the line-up and the hover and the field skips bodies under its damage filter): it saw the dash only once it was under way. In the 45 ticks after launch an in-lane levy made 0.6 dodge manoeuvres on average and moved 4.8 tiles along the lane but only 3.2 tiles across it; 15% had left the band vertically after 45 ticks. They dodged along the charge, as suspected, and nothing they own (a hop of a few tiles, a sidestep, a dash of 76 px) gets out of a 31-tile band in the 22 to 45 ticks a dash gives.
Deaths with a recorded source over the 12 before fights: 248 to a Leviathan dash, 10 to Anahita, 6 to bubbles and aberrations, 20 unattributed. First dash of a series: 219 deaths over 85 dashes; one dash killed 19 of 30.

**INFERRED (not tested separately):** the ocean arena has no water, so the swim-out-of-the-lane option could not be exercised. Levies take the Leviathan's whole 900 x 450 rectangle (`NPC.GetMeleeCollisionData`); a player takes Calamity's mouth, body and tail circles (`CanHitPlayer`), so the rectangle's corners hurt a levy but not a player. I did not change that (it is a parity question about the boss's hitbox, not about movement).

## 2. The fix (movement only, boss untouched)

`Common/Movement/ChargeLanes.cs`, `Content/NPCs/SoldierNPC.ChargeLane.cs`, a term in `SoldierNPC.Rows.cs`. `LEVY_CHARGELANE=0` turns all of it off.

A **charge lane** is the band a big charger will sweep, built once a tick for the whole army. For the Leviathan: its own height (plus an 18 px margin) at its own height (it tracks its target's), along its heading, from the start of the line-up (`ai[0] == 2`) and, while its AI may charge (death mode, under 20% life, or Anahita no longer fighting), already during the hover before it (`ai[0]` 0 or 1), because the line-up can be one tick long. Any other very large body (half height at least 80 px) already moving fast (14 px/tick) and nearly level is a lane from its velocity, so a modded charger of that size gets it without a name; small fast bodies stay with the threat field.

Two layers read it. **Positioning** (the row spread that already exists): a platform row inside the band scores worse in proportion to how deep in it, so ranged levies climb to rows beyond the band's edge and chain upward, spread by the existing crowd term, before the dash. **Escape** (line-up and dash only): a levy inside the band goes across it, not along it, at once: up on its wings at full thrust (1.25 x fly speed, the whole tank but 6 ticks) or down by falling (9 px/tick, no fuel; from a platform by dropping through it) to just past the edge, never into lava, and it holds beside the band while the boss stays in its charge state. When both ways are open its parity picks (half go each way) and a slot-dependent extra clearance keeps the escaped levies from stacking where the next line-up will look. Dash i-frames are unchanged (12 ticks, already read by the dodge search): they cannot cover a 900 px body that overlaps for 40+ ticks, so a dash is not a way through a Leviathan charge.

Self-check at boot: `[ChargeLane] self-check pass=22 fail=0` (line-up, dash, hover with and without charge permission, fading-in boss, a state outside 0 to 2, band edges, behind the tail, the way out with a deep floor, a shallow floor, split by parity, no fuel, no flight but a drop, and the fast-body rule).

## 3. Before and after, every fight

| difficulty | arm | wins | median kill s (wins) | levy deaths /30 (mean) | deaths to a Leviathan dash | deaths to Anahita |
|---|---|---|---|---|---|---|
| Expert+Rev | before | 4/6 | 103 | 17.3 | 94 | 5 |
| Expert+Rev | after | 6/6 | 80 | 1.7 | 8 | 2 |
| Master+Death | before | 0/6 | - | 30.0 | 154 | 5 |
| Master+Death | after | 1/6 | 139 | 27.7 | 110 | 41 |

Over all 12 fights: levies in the lane at launch that were hit 65% before, 52% after; that died 39% before, 18% after; out of the band vertically 45 ticks after launch 15% before, 54% after; moved across the lane 3.2 tiles before, 13.4 after. First-of-series dashes killed 219 before, 45 after.

Reading it: on Expert+Rev the charge stopped killing the army (every after fight a win, 5 of 6 with no deaths). On Master+Death the first dashes now land on empty bands (one fight's 12 dashes: one hit) but the army still does not win: it dies later, to dashes that arrive while the boss hovers level with the floor (the band then covers every row the army can reach, and floor-bound levies cannot climb 20+ tiles in time), and to Anahita (41 deaths against 5: the same levies now live long enough to meet her). Not solved: a Master+Death win rate (1/6 is inside the noise).

### Before (`LEVY_CHARGELANE=0`)

| boss | difficulty | kit | seed | result | kill s | levy deaths /30 | to a dash | other Leviathan contact | Anahita | bubbles+aberrations | unattributed | dashes | deaths per dash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| levi | Expert+Rev | mid | 1 | WIN | 117.9 | 14 | 13 | 0 | 1 | 0 | 0 | 3 | 4.3 |
| levi | Expert+Rev | mid | 2 | LOSS | - | 30 | 27 | 0 | 0 | 3 | 0 | 2 | 13.5 |
| levi | Expert+Rev | mid | 3 | WIN | 125.5 | 8 | 8 | 0 | 0 | 0 | 0 | 2 | 4.0 |
| levi | Expert+Rev | on | 1 | LOSS | - | 30 | 28 | 0 | 2 | 0 | 0 | 5 | 5.6 |
| levi | Expert+Rev | on | 2 | WIN | 87.4 | 21 | 17 | 0 | 2 | 2 | 0 | 3 | 5.7 |
| levi | Expert+Rev | on | 3 | WIN | 62.1 | 1 | 1 | 0 | 0 | 0 | 0 | 1 | 1.0 |
| levi | Master+Death | mid | 1 | LOSS | - | 30 | 24 | 0 | 0 | 1 | 5 | 20 | 1.2 |
| levi | Master+Death | mid | 2 | LOSS | - | 30 | 14 | 0 | 3 | 0 | 13 | 23 | 0.6 |
| levi | Master+Death | mid | 3 | LOSS | - | 30 | 28 | 0 | 0 | 0 | 2 | 37 | 0.8 |
| levi | Master+Death | on | 1 | LOSS | - | 30 | 29 | 0 | 1 | 0 | 0 | 37 | 0.8 |
| levi | Master+Death | on | 2 | LOSS | - | 30 | 29 | 0 | 1 | 0 | 0 | 49 | 0.6 |
| levi | Master+Death | on | 3 | LOSS | - | 30 | 30 | 0 | 0 | 0 | 0 | 9 | 3.3 |

### After

| boss | difficulty | kit | seed | result | kill s | levy deaths /30 | to a dash | other Leviathan contact | Anahita | bubbles+aberrations | unattributed | dashes | deaths per dash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| levi | Expert+Rev | mid | 1 | WIN | 98.1 | 0 | 0 | 0 | 0 | 0 | 0 | 3 | 0.0 |
| levi | Expert+Rev | mid | 2 | WIN | 88.4 | 10 | 8 | 0 | 2 | 0 | 0 | 3 | 2.7 |
| levi | Expert+Rev | mid | 3 | WIN | 105.5 | 0 | 0 | 0 | 0 | 0 | 0 | 2 | 0.0 |
| levi | Expert+Rev | on | 1 | WIN | 71.5 | 0 | 0 | 0 | 0 | 0 | 0 | 2 | 0.0 |
| levi | Expert+Rev | on | 2 | WIN | 68.5 | 0 | 0 | 0 | 0 | 0 | 0 | 3 | 0.0 |
| levi | Expert+Rev | on | 3 | WIN | 68.2 | 0 | 0 | 0 | 0 | 0 | 0 | 2 | 0.0 |
| levi | Master+Death | mid | 1 | LOSS | - | 30 | 0 | 0 | 22 | 8 | 0 | 12 | 0.0 |
| levi | Master+Death | mid | 2 | LOSS | - | 30 | 22 | 0 | 5 | 3 | 0 | 8 | 2.8 |
| levi | Master+Death | mid | 3 | WIN | 139.3 | 16 | 6 | 0 | 7 | 3 | 0 | 5 | 1.2 |
| levi | Master+Death | on | 1 | LOSS | - | 30 | 28 | 0 | 1 | 1 | 0 | 8 | 3.5 |
| levi | Master+Death | on | 2 | LOSS | - | 30 | 27 | 0 | 3 | 0 | 0 | 3 | 9.0 |
| levi | Master+Death | on | 3 | LOSS | - | 30 | 27 | 0 | 3 | 0 | 0 | 8 | 3.4 |

## 4. Anahita

Deaths to Anahita's contact attacks were 10 before and 43 after across the 12 fights. Her dash lane is 100 px wide with a 45 to 60 tick setup, so the existing dodge can already beat it; the cases that kill are her volleys and her dash arriving while levies are airborne from a lane escape. I did not change anything for her.

## 5. Regression (on-tier kit, 3 seeds, cap 180 s, same code, `LEVY_CHARGELANE=0` vs default)

Duke Fishron (`a3@ocean`), The Destroyer and Desert Scourge (worm, arena `a3`). Fishron's wipe is the battery's result (his body, 100 x 100, is under the lane rule so he is untouched); Desert Scourge and the Destroyer's heads are small bodies, so no lane applies. Differences are the sim's chaos.

| boss | difficulty | before wins | after wins | before deaths mean | after deaths mean |
|---|---|---|---|---|---|
| Duke Fishron | Expert+Rev | 0/3 | 0/3 | 30 | 30 |
| Duke Fishron | Master+Death | 0/3 | 0/3 | 30 | 30 |
| The Destroyer | Expert+Rev | 3/3 (median 40 s) | 3/3 (36 s) | 4.3 | 6.0 |
| The Destroyer | Master+Death | 0/3 | 1/3 | 30 | 28 |
| Desert Scourge | Expert+Rev | 3/3 (13 s) | 3/3 (14 s) | 0 | 0 |
| Desert Scourge | Master+Death | 3/3 (16 s) | 3/3 (14 s) | 0 | 0 |

### Regression, before

| boss | difficulty | kit | seed | result | kill s | levy deaths /30 | to a dash | other Leviathan contact | Anahita | bubbles+aberrations | unattributed | dashes | deaths per dash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| destroyer | Expert+Rev | on | 1 | WIN | 42.5 | 6 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Expert+Rev | on | 2 | WIN | 30.9 | 3 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Expert+Rev | on | 3 | WIN | 39.5 | 4 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Master+Death | on | 1 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Master+Death | on | 2 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Master+Death | on | 3 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Expert+Rev | on | 1 | WIN | 14.6 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Expert+Rev | on | 2 | WIN | 13.1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Expert+Rev | on | 3 | WIN | 12.9 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Master+Death | on | 1 | WIN | 13.9 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Master+Death | on | 2 | WIN | 18.6 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Master+Death | on | 3 | WIN | 15.6 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Expert+Rev | on | 1 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Expert+Rev | on | 2 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Expert+Rev | on | 3 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Master+Death | on | 1 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Master+Death | on | 2 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Master+Death | on | 3 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |

### Regression, after

| boss | difficulty | kit | seed | result | kill s | levy deaths /30 | to a dash | other Leviathan contact | Anahita | bubbles+aberrations | unattributed | dashes | deaths per dash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| destroyer | Expert+Rev | on | 1 | WIN | 35.5 | 3 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Expert+Rev | on | 2 | WIN | 33.5 | 6 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Expert+Rev | on | 3 | WIN | 46.4 | 9 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Master+Death | on | 1 | WIN | 68.3 | 24 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Master+Death | on | 2 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| destroyer | Master+Death | on | 3 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Expert+Rev | on | 1 | WIN | 14.5 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Expert+Rev | on | 2 | WIN | 17.1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Expert+Rev | on | 3 | WIN | 12.5 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Master+Death | on | 1 | WIN | 16.0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Master+Death | on | 2 | WIN | 12.6 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| ds | Master+Death | on | 3 | WIN | 13.7 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Expert+Rev | on | 1 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Expert+Rev | on | 2 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Expert+Rev | on | 3 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Master+Death | on | 1 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Master+Death | on | 2 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |
| fish | Master+Death | on | 3 | LOSS | - | 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0.0 |

## 6. Not verified

- No real client, no water in the arena, stand-in commander (invulnerable, pinned): the commander's own dodging against the Leviathan was not tested.
- 3 seeds per cell. The Master+Death win rate moved 0/6 to 1/6 (noise); the deaths per dash are the stronger signal.
- Whether Calamity's hitbox circles (what a player takes) should apply to levies was not changed.
- Levies with no flight kit were not in the test army: the drop-through half of the escape is covered by the self-check only.

## 7. Ship check (private install, tools/ship/shipboth.sh with private paths and ports)

Vanilla boot exc=0 (known summon-staff pick failure only); Calamity boot exc=0 failN=0 compat=1; `[ChargeLane] self-check pass=22 fail=0`. shipworld, second run: vanilla MechWorld 74/0, SummonWorld 9/0, IdentityWorld 3/0, OfficerWorld 39/0, WhipReal 110/0; Calamity 84/0, 32/0, 3/0, 39/0, WhipReal 108/0; 0 exceptions. In the first run OfficerWorld (vanilla) was 38/1 (a Skeleton Archer sweep check) and passed 39/0 on the repeat. The Calamity half prints `[ClientProf] self-check pass=28 fail=1` in both runs (`clientprof world: FAIL and it still ends cleanly with prof events`, the profile recorder timing check); I did not run the base commit to see whether it predates this change.
