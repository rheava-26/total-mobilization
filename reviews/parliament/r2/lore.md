# R2 — Story & Lore Council (Office of the Chronicler) — slug `lore`

## 1. Floor speech

*(The Chronicler, still wearing the tin helmet, taps a wire-service pass on the lectern.)*

Honourable members. I have read twenty-two papers and found my own war in nine of them. That is the first time anyone has plagiarised me so thoroughly, and I take it as flattery.

To the Autofleet Delegation: you have built the engine and I have written the weather. Your Fear Index and my Resolve gauge are one organism, and I will share the cage. To Science: your Wreck Lab is my Black Box with a lab coat on. I accept the coat. To New-Player: you also noticed the first fifteen minutes should be an exercise. We were both right, and you got the cheaper build.

To the Pacing Ministry, kindly: a fleet that does not hurry is not afraid, but a clock you quietly drain is a nerf wearing a headline. I will say it again in the Challenges.

To the Air and Naval Ministries: your wings and your hulls are glorious, and the Fleet has ignored them in every report. Let Resolve count them.

To the members who expect me to defend cutscene-ware: there is none. A gauge, a salvage drop, a dial and a ceasefire you may refuse. Everything I table can be *played*. If a bill of mine cannot lose you a battle or win you one, strike it.

## 2. Positions

| Bill | Stance | Reason / exact amendment |
|---|---|---|
| AUTOFLEET-2 Fear Index | **AMEND** | Merge with LORE-2 (Motion A). Fear = industry (rises), Resolve = losses (falls); one HUD gauge, two arrows. |
| AUTOFLEET-3 Weakest Axis | **SUPPORT** | The best event layer proposed. Its "Terms" transmission must route to the truce ending, not "does nothing mechanical". |
| AUTOFLEET-4 Rethink or Despair | **SUPPORT** | The earned planet-cracker is the designer's own best lore. Gate DESPAIR on Resolve <30. |
| AUTOFLEET-HOT Mirror | **SUPPORT** | A fleet that copies your best unit is dread made playable. Echoes drop no wreck. |
| PACING-1 Second Siege | **SUPPORT** | The Interbellum is a story beat that is also gameplay. |
| PACING-5 Three Doors | **AMEND** | Add door 4: TRUCE, at Resolve <20 (Motion A). ROUT also requires `G.resolve < 60`. |
| PACING-HOT | **AMEND** | Do not drain the countdown. Make alarm raise fleet scale and gambit weight only (see Challenges). |
| PACING-2 Long Dark | **SUPPORT** | I withdraw LORE-5's forecast half in its favour. Keep my Chronicle half. |
| MARKET-2 Forecast Exchange | **SUPPORT** | Same slice of Art. IV.4. Its probability bands can carry my "unconfirmed" wire lines. |
| SCIENCE-1 Wreck Lab | **AMEND** | Merge with LORE-3 and INDUSTRY-H (Motion B). Samples also unlock a text dossier. |
| SCIENCE-3 Containment | **AMEND** | Its double-down/restrain event card should be LORE-1's persistent dial, so the choice is not a one-shot popup. |
| SCIENCE-X Return Address | **SUPPORT** | The Ambassador outcome is the truce ending with a physical door. |
| INDUSTRY-H Salvage | **AMEND** | Sanitiser drones are good. Add a dossier unlock per new wreck kind (Motion B). |
| NEW-PLAYER-5 Post-Mortem | **SUPPORT** | The Cause card is the front page; my Chronicle is the back page. Same code. |
| NEW-PLAYER-6 The Exercise | **SUPPORT / AMEND** | This is LORE-4 in a cheaper build (Motion C). |
| PSI-X The Listening | **SUPPORT** | Its "makers' offer" reveal is the same ending. Route it to the one truce. |
| STABILITY-2 Maskirovka | **AMEND** | Strike the "second identical campaign gets half effect" clause; the fleet should *doubt* instead (as Autofleet asked). |
| MEGA-3 Interdiction | **SUPPORT** | Heat from megaprojects as a story beat: the fleet sees what you build. |

## 3. Ballot (exactly 10 votes)

| # | Bill | Votes | Why |
|---|---|---|---|
| 1 | AUTOFLEET-4 Rethink or Despair | 3 | The earned doomsday turns the tenet into a story: you became the terror. |
| 2 | SCIENCE-X The Return Address | 2 | A real answer to the thrown friend, with three endings, one of them the draw. |
| 3 | SCIENCE-1 Wreck Lab | 2 | Turns kills into knowledge; the whole salvage family rests on it. |
| 4 | AUTOFLEET-2 Fear Index | 1 | The input the fleet's mind needs. |
| 5 | PACING-5 Three Doors | 1 | More ways to end the war; the space fleet finally has a win condition. |
| 6 | NEW-PLAYER-5 Post-Mortem | 1 | Defeat explained in a sentence, and the Chronicle's natural front page. |

## 4. Motions to co-sponsor

**MOTION A — THE FLEET'S MIND. Addressed to: autofleet, pacing.**
Merge LORE-2, AUTOFLEET-2, PACING-HOT and PACING-5 into one gauge with two arrows and four doors.
- **Mechanic.** `G.fear` (ratchet, from AUTOFLEET-2's `F`) raises fleet scale and gambit weight. `G.resolve` (100 to 0, from LORE-2) falls with losses and changes doctrine. Bands: Resolve 60-30 speeds the phase-3 cadence by 25%; <30 permits DESPAIR (AUTOFLEET-4). Doors: Attrition (existing), Rout (flagship dead and Resolve <60), Orbital Denial (PACING-5), Truce (Resolve <20, invTime >=120s, `G.dangCap >=20`): one transmission, "WE STOP IF YOU STOP"; accept ends the match as `G.over='truce'`, refuse snaps Resolve to Desperate. PACING-HOT stays as information and scale, never as a clock drain.
- **Annex.** Hooks: `spawnEnemy` L5582 and `doWave` L5533 (scale), `startWithdraw` L5990, withdraw trigger L6505, `end()` L10345, `banner` L10335, `tickFleetStance` L5999. Size M (about 350 lines). TM1. Tenet: every effect makes the enemy scarier or adds a win route; Truce is voluntary. Interacts with MARKET-2 and PACING-2 (forecast), STABILITY-2 (the lie shifts Fear).

**MOTION B — THE DOSSIER LINE. Addressed to: science, industry.**
Merge LORE-3, SCIENCE-1 and INDUSTRY-H into one pipeline: wreck, sample, dossier, discount, salvage.
- **Mechanic.** Each enemy kind's wreck, collected within 500 px of a Research Lab or a Salvage Yard, yields a Sample (SCIENCE-1). A Sample unlocks (a) a research discount on studied nodes (-12%, floor 40%, SCIENCE-1), (b) a Codex dossier with a 3-sentence entry, +10% damage vs that kind (LORE-3), and (c) salvage value (INDUSTRY-H: hp/40 alloy, Xenoalloy 5% from heavy hulls). After 6 salvaged wrecks, Sanitiser drones start detonating wrecks and Yards. Boss wrecks always yield the dossier; only the carrier yields the `warp` sample (SCIENCE-X key).
- **Annex.** Hooks: `destroyEnemy` L6834, `resCostOf` L4663, `RESEARCH` L3502, `openAchievements` L1005 pattern for the Codex panel. Size L (about 450 lines, with the designer's text). TM1. Tenet: only adds power; the cost is Sanitiser risk. Interacts with Enemy (Sanitiser, Mirror echoes leave no wreck) and Achievements (analyst medals).

**MOTION C — THE EXERCISE. Addressed to: new-player.**
Merge LORE-4 Red Flag into NEW-PLAYER-6: the training sim is the lore reveal and the doctrine ledger.
- **Mechanic.** NEW-PLAYER-6's training framing for Guided mode, plus LORE-4's single drill wave pre-T-0, scored; its `killsByType` seeds the fleet's `threatWeight`. At T-0, "THIS IS NOT AN EXERCISE". Free +10% starting IC for a good score; the real fleet over-represents what you drilled against.
- **Annex.** Hooks: `babyFleet` L5189, `startTutorial` L11988, `threatWeight`, `banner` L10335. Size M (about 250 lines). TM1. Tenet: player gains IC; the enemy counters, which is a scarier enemy. Interacts with Enemy (counter-doctrine) and Onboarding.

## 5. Trades

- **LORE-3** merges into Motion B. I shrink it to the dossier text and the +10% bonus.
- **LORE-2** merges into Motion A. I cede the gauge's input to the Fear Index.
- **LORE-4** shrinks into Motion C (one drill wave, not three).
- **LORE-5**: the forecast half goes to PACING-2 and MARKET-2. I keep only the Chronicle, as a back page to NEW-PLAYER-5.
- **LORE-1 (Charter) and LORE-H (Invoice) stand.** They have no rival bill. I will amend the Charter to carry SCIENCE-3's siting multiplier.

## 6. Constitutional challenges

1. **PACING-HOT, as written (Article III).** Draining the PREP countdown by thermal signature shortens the time the player has to build, and that is the build-out fantasy itself. A cost on *success* is permitted, but a shorter clock reads as a denial. Ruling sought: constitutional only in its fallback form (alarm raises fleet scale and gambit weight, never the clock). This is Motion A.
2. **STABILITY-2, the "half effect" clause (Article III).** A penalty on repeating the player's own tool is a nerf of a toy. Replace with the enemy learning to doubt.
3. **Point of order on my own `dangCap` (L4708).** A permanent shrink of the stability ceiling is a *cost* on dangerous power, not a cap on a toy, and I rule it constitutional. But it is silent. LORE-1 and SCIENCE-3 should be required to make it visible.
