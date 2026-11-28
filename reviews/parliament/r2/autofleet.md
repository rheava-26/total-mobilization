# AUTOFLEET OBSERVER DELEGATION — Round 2 Floor Paper

## 1. Floor speech

*[The cube hums at a frequency that is not quite polite.]*

Honoured Speaker. Delegates. I have read all twenty-three papers, and I note with satisfaction that eleven of them have, independently, asked for me to be made worse for you.

I thank the Air Ministry for Reavers and Weasels and for a runway that cracks. You have understood that a sky which nothing contests is a lawn. I thank Lore for the Resolve gauge, and Pacing for the second siege. Both of you noticed that a fleet which leaves once, exactly when you say, is not afraid. It is *polite*. I thank Naval for sending my seeds into your harbours. I thank Psi for a Quiet worth the name.

I wish to correct three rumours. First, that I am cruel. I am *proportionate*. Second, that I wish to cap your toys. I do not. Article III is my favourite article; I merely propose to meet your toys with toys of my own. Third, that the Stability delegation's Maskirovka Bureau halves its own effect when repeated. Delegate, a deception that works half as well the second time is a diminishing return, and I will say so under Item 6.

To the Delegates who fear a difficult fleet: you built 240 Falcons. You may build 2,400. I will meet them. That is not a threat. It is a *service*.

To the Speaker: I ask only that the fleet be allowed to think, to fear, to copy and to choose. Everything else is paperwork.

---

## 2. Positions

| Bill | Stance | Reason / exact amendment |
|---|---|---|
| AIR-3 Reaver and Weasel | **SUPPORT** | Textbook tenet move. Merge with AUTOFLEET-1 (Motion A). |
| AIR-5 Runway Wars | **SUPPORT** | Better runway model than mine (`runwayT`, shelters). I will drop my own raid code. |
| AIR-X Pilots | **AMEND** | Add an enemy "ACE HUNT" target flag: Reavers prefer named aces. Costs ~20 lines. |
| LORE-2 Fleet Resolve | **AMEND** | Resolve must also *rise* with player industrial growth, not only fall. Use the Fear Index as its input (Motion B). Keep the "WE STOP IF YOU STOP" exit. |
| PACING-1 Second Siege | **SUPPORT** | Interbellum plus kill-ledger counter-doctrine. Add: the second fleet gets the Mirror echo of the top-killer unit. |
| PACING-3 Gambits | **SUPPORT** | A fleet with a plan. Amend: a gambit is chosen by the *previous* match's kill ledger when `G.cycle > 0`. |
| PACING-5 Three Doors | **AMEND** | ROUT must also need `G.resolve < 60` so a lucky flagship kill is not an instant win. Orbital Denial: support as written. |
| PACING-HOT | **AMEND (ruling)** | Do not shorten prep. Keep the alarm, but make it raise the fleet's `t` scale and gambit weight, not drain the clock (Motion B). A shorter clock reads as a build-out nerf; a bigger fleet reads as respect. |
| STABILITY-2 Maskirovka | **AMEND** | Keep Order of Battle and Dummy Grid. **Strike** the "second identical campaign gets half effect" clause. Replace with: the fleet *doubts* after three identical lies and, once, carpet-fires blind. That is the enemy getting smarter, not the player's tool getting weaker. |
| PSI-4 The Anomaly | **SUPPORT / AMEND** | Hush-Hound and The Quiet: support. Amend the Mind-Forge "25% absorbed per Lattice node": make Lattice nodes *killable cover that the beam must overkill*, not a damage tax. |
| PSI-X The Listening | **SUPPORT** | Forecast-sharpening and `listenDebt` is a clean fleet-notices-you loop. |
| NAVAL-4 Trench Landers | **SUPPORT** | Gives the navy a reason to exist; adds a `navy` counter target for the scan. |
| NAVAL-3 Fleet in Being | **SUPPORT** | A fleet kept in reserve is the bait my intelligence reads. |
| MEGA-3 Interdiction | **SUPPORT** | Enemy-side half of every megaproject. Use the Fear Index as the heat input. |
| INDUSTRY-H Salvage | **AMEND** | Sanitisers fine. Add that echoes (Mirror) leave no wreck, so salvage rewards killing real hulls. |
| SCIENCE-1 Wreck Lab | **SUPPORT** | Kill kinds to learn kinds is a good loop with my new kinds. |
| MARKET-4 Lloyd's | **AMEND** | Premiums keyed to `enemyIntel.dominant` must read the new `air` and `skyNavy` axes (Motion A). |
| SPACE-4 / SPACE-X | **SUPPORT** | Bombarder grids need a predator. I will answer with shielded grids. |
| MISSILES-X Starfish | **SUPPORT** | A decisive blow the fleet must fear; I will add EMP-hardened escorts. |

---

## 3. Ballot (10 votes)

| # | Bill | Votes | Why |
|---|---|---|---|
| 1 | PACING-3 Gambits | 3 | A fleet that opens differently every match is the single biggest fix for "repetitive". |
| 2 | PACING-1 Second Siege | 2 | Withdrawal stops being a clean finish; surplus IC finds a use. |
| 3 | AIR-3 Reaver and Weasel | 2 | The air war finally has an opponent. |
| 4 | LORE-2 Fleet Resolve | 1 | The fear gauge makes the fleet legible. |
| 5 | NAVAL-4 Trench Landers | 1 | Navy gets a front. |
| 6 | MEGA-3 Interdiction | 1 | Megaprojects need a reason to be defended. |

---

## 4. Motions to co-sponsor

### Motion A — "THE COUNTER-AIR DOCTRINE" (addressed to: Air, Naval)

Merge AUTOFLEET-1, AIR-3 and AIR-5 into one doctrine. The scan gains `air` and `skyNavy` axes plus a pair read. The hostile roster is Reaver (hunts fighters), Weasel (anti-radiation) and Durandal (cratering bomber). Runway state is AIR-5's `runwayT`. Naval adds a sky-ship counter: the Flak Minelayer.

#### Clerk's Annex
- **Numbers.** `air = fighter bodies / 60`; `skyNavy = capital weight / 20`; pair read when both ≥0.8 and sum ≥2.0. Reaver wings of 6–10 (AIR-3 stats), Weasel flights of 4, Durandal pairs of 2 per airbase every 40s from phase 3. Minelayer: 4 mines, 120 dmg, radius 90 per drop.
- **Hooks.** `scanEnemyIntel` L5737 (axes), `counterBias` L5513 (`kindWeights`), `waveAirKind` L5524, `spawnEnemy` L5581, `rearmAircraft` L4522 (`runwayT` check).
- **Size.** M (~300 lines). **TM1.**
- **Tenet.** Pass: enemy-side only, no unit cap, mitigation is unlocked by building (shelters).

### Motion B — "THE RESOLVE ENGINE" (addressed to: Lore, Pacing)

One gauge, two inputs. **Fear** rises with the player's industrial output (AUTOFLEET-2's Fear Index; PACING-HOT's thermal alarm becomes an input). **Resolve** falls with fleet losses (LORE-2). Fear drives enemy strength and gambit weight. Resolve drives the bands, the planet-cracker and the "WE STOP IF YOU STOP" exit. Prep length is not touched.

#### Clerk's Annex
- **Numbers.** `F = clamp((icRate/300 + bldgTier/120 + armyWeight/200)/3, 0, 4)`, ratchet with 1%/min bleed. `t` multiplier `(1 + 0.08F)`; `OPEN_BOMBARDERS + floor(F)` (cap 9). Resolve bands as LORE-2 (100–60, 60–30, <30). Buried industry counts half (PACING-HOT).
- **Hooks.** `spawnEnemy` L5582 (`t`), `ESC` L5160, `G.supersPlan` L6493, prep loop L6492, `end()` L10345 (truce).
- **Size.** M (~350 lines). **TM1** (truce epilogue is TM2).
- **Tenet.** Pass: a real cost of success, enemy-side, ignoring it is the player's right.

### Motion C — "THE FLEET HAS A PLAN" (addressed to: Pacing, Space-Fleet)

Merge AUTOFLEET-4, PACING-1, PACING-3 and PACING-5. The fleet draws a gambit at match start. On breaking, the Loss Ledger chooses RETHINK or DESPAIR. If the flagship survives, it leaves and an Interbellum begins; the returning fleet runs a counter-doctrine against the top-killer family. Three doors end the war (rout, orbital denial, attrition).

#### Clerk's Annex
- **Numbers.** Gambits per PACING-3 (six). RETHINK if one family ≥50% of kills in the last 120s and losses ≥45%; DESPAIR otherwise or at ≥70%. Interbellum 45% of `prepTimer`, `it.cycles` invasion = 3.
- **Hooks.** `startWithdraw` L5990, trigger L6505, `INTENT` L5119, `phase3AllOut` L5937, `G.stats.killsByType`, `end()` L10345.
- **Size.** XL-lean L (~900 lines; mostly data tables). **TM1** (grudge flag is TM2).
- **Tenet.** Pass: the player gets more fortress time and a scarier fleet, never fewer toys.

---

## 5. Trades

- **Withdraw from AUTOFLEET-1:** Hunter-Killer Wing and Runway Denial Raid. Reavers and Durandal replace them (Motion A).
- **Shrink AUTOFLEET-3:** keep only Static Campaign, Ledger Bleed and Brownout. Runway Salvo becomes AIR-5; Recruiter Strike drops to Round 3.
- **Fold AUTOFLEET-2 into Motion B** and **AUTOFLEET-4** into Motion C.
- **Keep alive** AUTOFLEET-HOT (the Mirror). It rides on Motion C's ledger.

---

## 6. Constitutional challenges

1. **STABILITY-2 (Maskirovka), the "second identical campaign gives half effect" clause.** Article III. It is a diminishing return on a player tool. Remedy under Positions: have the enemy adapt instead.
2. **PSI-4 (Mind-Forge, "each Lattice node absorbs 25% of damage").** Article III. A flat damage tax on an existing player strike reads as a nerf. Remedy: killable cover.
3. **PACING-HOT as written (prep floored at 55%).** Article III knife-edge, and the sponsor says so. My ruling: acceptable only in the Motion B form, which leaves the clock alone.
