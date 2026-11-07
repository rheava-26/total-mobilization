# Worm dodge: measurement and fix (branch `wormdodge`, 2026-11-07)

Scripts: `tools/fightviewer/wormdodge_measure.py` (per close pass metrics), `wormdodge_claim.py` (with/against and jump tables), `wormdodge_table.py` (lab table). Full step-1 output: `step1_measure.txt`.
Code: `Content/NPCs/SoldierNPC.WormDodge.cs`, hooks in `SoldierNPC.Threat.cs` (TickThreatDodge, Consider), `Common/Movement/ThreatField.cs` (Head, Turn, Aged, PosAt), `Common/LevyConstants.WormDodge.cs`.
Switches: `LEVY_WORMDODGE=0` off (before arm). `LEVY_WORMPICK=1` hard pick variant (off by default). `LEVY_WORMHOLD=0` no hold-still in the hard pick.

## 1. Measurement (OBSERVED)

A close pass is a worm head within 8 tiles (128 px) of a body at closest approach. Window = 1.5 s before it. Eve: 12 solo passes, 4 team commander passes (small samples). Levies: 13 passes in her team fights, 579 in the old lab recordings (`recm/ds_exp`, Expert, arena a3).

| metric | Eve (solo) | Eve (team) | Levies in her team fights | Levies in lab (old) |
|---|---|---|---|---|
| speed during the approach (px/tick, first 0.9 s) | 0.65 | 1.9 | 6.4 | ~3 |
| time the big move starts (s before closest approach) | 0.10 (largest velocity change) | 0.35 | 0.67 | 0.67 |
| head-to-body distance at that move | 154 px | 321 px | 375 px | ~375 px |
| size of the move (px/tick) | 13.6 | 13.4 | 10.0 | 7.8 |
| dashed in the pass window | 75% | 75% | 8% | 8% |
| jumped in the pass window | 50% | 25% | 85% | 66% |
| double jump (2+ kicks) | 17% | 0% | 0% | 12% |
| move is mostly UP (dv up, px/tick) | 1 | 2 | 7.8 | 7.1 |
| passes with a hit within 0.5 s | 25% | 50% | 69% | 77% |

Direction (worm head's horizontal travel vs body horizontal motion, window dx over 20 px):

| | against | with | median dx (+ = with) |
|---|---|---|---|
| Eve solo | 42% | 33% | -8 px |
| Eve team (commander) | 75% | 25% | -102 px |
| Levies, her team fights | 38% | 62% | +133 px |
| Levies, lab | 35% | 58% | +50 px |

Claim test (`wormdodge_claim.py`, head within 160 px, Eve both groups, n = 24 passes, 5 head hits): hit rate WITH 9% (n=11), AGAINST 25% (n=8), still 40% (n=5). Jumped 36% (n=11), no jump 8% (n=13). Move began 0.40 s before closest approach on clean passes and 0.73 s before on passes that got hit. For levies in the old lab (head within 96 px, n=441): WITH 92.5%, AGAINST 93.4%, still 95.6% hit; jumped 95.6%, no jump 89.3%.

Hits by part (levy hits per fight, from `lhit` events, Desert Scourge Expert): head 25, body 54. Eater of Worlds Revengeance: head 27, body 284. The trail does most of the damage.

What this supports:
- CONFIRMED: levies commit early (0.67 s, head 375 px away) and Eve commits late (0.10 s, head 154 px away) after standing almost still. Hit rate on passes: 77% levies vs 25% Eve. In Eve's own data the passes that got hit began their move earlier (0.73 s) than the clean ones (0.40 s).
- CONFIRMED: Eve's signature move is a dash (75% of passes); levies almost never dash (8%) and their move is a jump (dv up 7 to 8 against 1 to 2 for Eve).
- PARTLY CONFIRMED: levies' horizontal motion is mostly WITH the head's horizontal travel (58 to 62%); Eve's is not (33% solo, 25% team commander). Eve's solo split is 42/33, a weak lean, not a rule.
- NOT SUPPORTED by this data: that moving against the head's horizontal travel avoids the hit. Levies' hit rate is the same with and against (92.5% vs 93.4%); Eve's sample has more hits when going against (25% vs 9%, n too small to read). The "jump" cell also shows no protection (hits are higher when jumped, because jumping is a late reaction to a head already on top).
- NOT SUPPORTED: "levies do not jump". They jump in 66 to 85% of passes; the jump is early and vertical, and it does not leave the head's box.

## 2. Why (INFERRED)

- Head position prediction error (calibrated on the DS recordings, 12,000 samples): linear extrapolation 10 / 37 / 80 / 137 px median at 10 / 20 / 30 / 40 ticks; with a constant turn rate 6 / 25 / 56 / 99 px. A head box is 104 px. A dodge planned 40 ticks ahead is aimed at a point wrong by a body width; one planned 10 ticks ahead is not. This is the likely reason the late move works and the early one does not.
- A head at 13 to 16 px/tick outruns a levy (max run 9), so a levy that walks off along the head's heading cannot gain separation.

## 3. The change

1. Threat field marks worm heads (`Threat.Head`) and gives them a turn rate (`Turn`, `TurnT`: arc then straight, capped at 1.2 rad). Only when the worm dodge is on.
2. While a head is in the near list, a dodge scan is judged twice: with an extra 40 px margin, then by the field's own margin.
3. A move that clears is not started until starting 6 ticks later would no longer clear (the last moment that still works). Until then the levy holds still.
4. Soft preference against moving along the head's heading, toward it, or the way it travels horizontally (score, not a veto).
5. Variant `LEVY_WORMPICK=1` (hard rule): side against the head's horizontal travel (rising or diving heads: against their predicted drift, else the open side), dash + jump, double jump, step + jump, air kick; a head flying along the row gets a jump or drop instead. Fires when the head is within 14 ticks. It overrides all other movement.

Default: 1 to 4 on. The hard pick is off by default because it tested worse (below).

## 4. Lab before / after (OBSERVED)

Cmd=near, order=follow, arena a3, 6 seeds, levy count n=cp, CalamityMod gear, Death/Master switches as named. Boot logs had zero `xception|TypeLoad|MissingMethod|malformed` in every run. Before = `LEVY_WORMDODGE=0`. After = default build (items 1 to 4), `LEVY_WORMPICK=0`.

| arm | cell | wins | kill s (wins) | boss left % (losses) | levy deaths | levy hits by head | levy hits by body |
|---|---|---|---|---|---|---|---|
| before | DS Expert | 1/6 | 65 | 31 | 10.8 | 25 | 54 |
| after | DS Expert | 2/6 | 41 | 49 | 9.3 | 27 | 52 |
| before | DS Master+Death | 0/6 | - | 83 | 11.0 | 16 | 38 |
| after | DS Master+Death | 0/6 | - | 81 | 11.0 | 11 | 44 |
| before | EoW Revengeance | 0/6 | - | 50 | 13.0 | 27 | 284 |
| after | EoW Revengeance | 0/6 | - | 48 | 13.0 | 28 | 282 |
| before | EoW Expert | 6/6 | 41 | - | 1.7 | 59 | 218 |
| after | EoW Expert | 6/6 | 43 | - | 1.7 | 41 | 163 |

Hard-pick variants, DS Expert only (6 seeds, 0 wins each, boss left % in losses, mean seconds survived): before 50 s; hold + search 45 s (first version); hard pick with hold 26 s and then 18 s after the horizontal-head fix (levies wiped in 9 to 24 s); hard pick without hold 33 s. In those runs levies got hit in 97 to 99% of passes despite jumping in ~100% and double-jumping in 72% (no dash gear), commit distance 129 to 287 px.

Dodge shape on the final recordings (levy passes, `wormdodge_measure.py`):

| | DS Exp before | DS Exp after | EoW Rev before | EoW Rev after |
|---|---|---|---|---|
| hold speed (px/tick) | 2.65 | 1.6 | 3.55 | 2.8 |
| still (<24 px) | 10% | 15% | 6% | 8% |
| head-to-body distance at the commit (px) | 375 | 321 | 234 | 178 |
| time of commit (s before closest) | 0.67 | 0.53 | 0.53 | 0.47 |
| jumped | 38% | 45% | 50% | 71% |
| hit in the pass | 79% | 78% | 75% | 78% |

Replays: `/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/replays/wormdodge/` (7 files with index.txt).

## 5. Verdict

The dodge shape moved only a little (holds more, commits closer) and the hit rate per pass did not change (78% before and after). Win counts (2/6 vs 1/6 on DS Expert) are inside seed noise at n=6. The hard against-the-head rule, with dash + jump, made fights worse in the lab. Treat the shipped default as neutral, not as a fix.

What is not solved: levies have almost no dash (0 to 8%), and the head box (104 px) is wider than the jump rise (~70 px) plus step reach available in the last 10 ticks, so a late move cannot always clear even when it is well timed. Body segments cause about two thirds of levy worm damage and the dodge only models the head (the chain model `LEVY_CHAIN=1` was not combined with this).

## 6. Not verified

- Whether Eve's with/against and jump effect exists: her data has 24 close passes and 5 head hits, too few to separate the cells.
- The replay-site screenshots and clips she sent were not available to me.
- Only the arena a3 was run; no surface or underground arena for worms.
- No test with the chain model (`LEVY_CHAIN=1`) or with a dash accessory given to levies.
