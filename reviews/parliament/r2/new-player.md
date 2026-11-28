# NEW PLAYER COUNCIL — Round 2: The Debate
*Ministry of Recruitment and Civil Defence Training. File slug: `new-player`.*

## 1. Floor speech

Mr Speaker, I rise to praise my allies and to bore my rivals.

Five councils have independently invented the same thing: a forecast of the incoming fleet. Market calls it a trading floor, Pacing calls it a clock with a smudge, Science calls it a doctrine, Lore calls it a newspaper, and my ministry called it an order of battle. Colleagues, we have built five instruments for one dial. Let us build one dial and five faces.

Lore, I kiss your hand. Red Flag is my Hot Take with a scoreboard and a budget, and I withdraw mine in your favour. Game-Feel, your legible-mass rim is the best onboarding bill nobody labelled as onboarding. Command, your Five-Year Plan is what I would give a civilian on day one: tell the nation what you want and let it build.

To Autofleet and Pacing, whose fleets watch the player build: a fleet that reads you is glorious, but only if the player can read the fleet. Every clever enemy reaction needs a headline, a phase marker or a cause string. A reaction the player cannot see is a bug report.

To the Stability council, whose logarithmic casualty ledger I admire: a penalty is a lesson only when the player can see the invoice.

To my critics, who say I want a tutorial that holds hands: I want no hand held and nothing capped. I want the sky labelled. You do not make a pilot weaker by printing the altitude on the dial.

## 2. Positions

| Bill | Vote | Reason / amendment |
|---|---|---|
| LORE-4 Red Flag | SUPPORT | It is my Hot Take with real structure. See Motion 2. |
| LORE-5 Wire & Chronicle | SUPPORT | Headlines are the best-readable way to say what the fleet is doing. |
| MARKET-2 Forecast Exchange | AMEND | Render through one shared forecast model (Motion 1). Tier-0 must show classes and phases to every player free; the Forecast Desk only narrows the bands. |
| PACING-2 The Long Dark | SUPPORT | The prep beats give the clock a face. Fold the manifest into its 25% MANIFEST beat. |
| SCIENCE-2 Synthesis Doctrines | SUPPORT | Doctrine 2 (Deep Survey) is the sharpening tier of the same forecast. Every doctrine needs the banner and map marker it already proposes, or new players never learn the combo happened. |
| STABILITY-1 Casualty Ledger | AMEND | Add a Ledger line for it on the STAB hover card (NEW-PLAYER-3) and the same line in the AAR. A hidden logarithmic cap penalty is a nerf by stealth; a visible one is a real cost. |
| STABILITY-3 State of Emergency | SUPPORT | A decision modal turns the worst minute into a lesson. Add a one-line "what each option costs" line to each card. |
| STABILITY-4 Information Policy Dial | AMEND | Fine as a mechanic. Default must be the plain-truth setting so a new player's HUD never lies to them. |
| GAME-FEEL-5 Legible Mass | SUPPORT | Team rims and far-zoom density glyphs are readability, which is my portfolio. |
| COMMAND-1 Five-Year Plan | SUPPORT | Standing orders replace the new player's hardest chore, remembering to build. See Motion 3. |
| INDUSTRY-3 Autofoundry | AMEND | Merge with COMMAND-1. Two standing-order panels will confuse a recruit. |
| AUTOFLEET-3 Weakest Axis | AMEND | Each psychology event must emit a named cause string for the Post-Mortem and a Wire headline. |
| PACING-3 Gambits | SUPPORT | A named plan ("SUSPECTED/CONFIRMED") is exactly the explained-plan gap. |
| PACING-HOT | AMEND | Constitutional as enemy behaviour (a scarier fleet), but the clock must show the reason ("Fear rising: industry +X"). Never silent. |
| MEGA-1 Ledger of Projects | AMEND | Doctrines you fall into need the banner, and the progress toward the next one must show on screen. |
| LORE-1 The Charter | AMEND | Also covers `dangCap` (L4708): any hidden limit on dangerous research must print itself in the research card. |
| MISSILES-4 Real Guidance | AMEND | Needs a Reach Curtain row for seeker range. Otherwise silent. |
| SPACE-4 / SPACE-X Kessler | AMEND | Debris fields need a visible hazard overlay, or new players meet them as unexplained deaths. |

## 3. Ballot (exactly 10 votes)

| # | Bill | Votes |
|---|---|---|
| 1 | LORE-4 Red Flag | 2 |
| 2 | LORE-5 The Wire & The Chronicle | 2 |
| 3 | GAME-FEEL-5 Legible Mass | 2 |
| 4 | COMMAND-1 The Five-Year Plan | 2 |
| 5 | PACING-2 The Long Dark | 1 |
| 6 | STABILITY-3 The State of Emergency | 1 |
| | **Total** | **10** |

## 4. Motions to co-sponsor

### Motion 1 — ONE DIAL, FIVE FACES (addressed to: market, pacing)
**Merges** NEW-PLAYER-1, MARKET-2, PACING-2 and SCIENCE-2 Deep Survey (LORE-5 feeds it).

- **Mechanic.** One shared object, `G.forecast`, holds for each enemy class and each phase a mean and a band width `sigma`. Tier 0 is free and shows every class name, the four phase names and bands of ±40%. Radar and Data Center steps and Market's Forecast Desk each add an independent observer, so `sigma` shrinks as 1/sqrt(n) (Market's own statistic). A new `signals` node (branch `def`, pre `pointdefense`, ~90 base, not dangerous) adds a further observer. Combo `signals` + `automation` is Science's Deep Survey. The object has five renderers: the manifest rows (NEW-PLAYER-1), the phase strip, the Market positions panel, the PACING-2 prep beats, and the LORE-5 Wire headlines (their `conf` is read from `sigma`).
- **Annex.**
  - Code hooks: `tutReadyTick` L11796, `renderReadyPanel` L11720, `tutIntentBlurb` L11691, `scanEnemyIntel` L5737, `curIntent` L5187, `banner` L10335.
  - Build size: M (~320 lines of shared model plus rendering; each existing bill's own UI shrinks).
  - TM1 (Article IV.4 slice 2).
  - Tenet check: pure addition of information, no toy touched. Tier 0 hides nothing that is shown today.
  - Real science: ensemble forecasting variance of a mean, the NHC cone of uncertainty.

### Motion 2 — THE EXERCISE, ON THE BOARD (addressed to: lore)
**Merges** NEW-PLAYER-6 into LORE-4 Red Flag. I withdraw mine.

- **Mechanic.** LORE-4's three PREP drill waves (0.4x, 0.7x, 1.0x) replace the baby-fleet opening in Guided and Heavily Guided modes. A drill result banner teaches each concept: drill 1 shows the Reach Curtain on the bomber, drill 2 the re-entry window, drill 3 the landing flank. At T-0 the EXERCISE tag changes to THIS IS NOT AN EXERCISE and the full manifest from Motion 1 replaces the exercise one. In those two modes only, LORE-4's rehearsal-reading adaptation (`threatWeight`) applies at half strength, so new players are not punished twice. Veterans can opt out of the exercise and keep everything else.
- **Annex.**
  - Code hooks: `INTENT` L5119, `babyFleet` L5189, `TUT_MODES` L1011, `doWave` L5533, `banner` L10335.
  - Build size: L (LORE-4's ~600 lines; the removed baby-fleet code offsets ~60).
  - TM1.
  - Tenet check: the player is stronger at T-0 (LORE-4's bonuses) and the enemy escalates. Nothing denied.
  - Lore: consistent with Art. V (a scaled forward model from sparse telemetry).

### Motion 3 — THE PLAN NEVER STALLS SILENTLY (addressed to: command, industry)
**Merges** COMMAND-1 and INDUSTRY-3 Autofoundry with NEW-PLAYER-4 The Because Chip.

- **Mechanic.** One Plan panel, not two. Each ledger line shows a live status chip: "OK", "Blocked: needs 5 lift, Space Center +5", "Blocked: hangar full 18/18, Airbase +6", "Waiting: IC". A blocked line offers a "Build the fix" button, which adds the missing prerequisite line at the same priority (the Bureau fills it if IC allows). The chips reuse `unitReqUnmet`'s strings, so there is one source of truth for the refusal.
- **Annex.**
  - Code hooks: `unitReqUnmet` L3829, `airCap`/`airUsed` L3815–3819, `queueUnit` L4619, `placeBuilding` L3884, Command's planned panel near the building info HTML (~L10086).
  - Build size: S on top of COMMAND-1 (~100 lines).
  - TM1.
  - Tenet check: adds convenience and information, caps nothing.

## 5. Trades

- **NEW-PLAYER-6 withdrawn** in favour of LORE-4 (Motion 2).
- **NEW-PLAYER-1 shrinks** to a renderer of the shared model (Motion 1); its stand-alone cone UI is dropped.
- **NEW-PLAYER-4 folds** into the Plan panel (Motion 3) but stays as a stand-alone chip on the manual produce buttons.
- Kept as is: NEW-PLAYER-2 (Reach Curtain), NEW-PLAYER-3 (Ledger) and NEW-PLAYER-5 (Post-Mortem). I will trade away the Post-Mortem's win-card text before the Curtain.

## 6. Constitutional challenges

None. I flag PACING-HOT and STABILITY-1 as knife-edge but constitutional (a scarier enemy and a real cost respectively), conditional on being visible on screen. Silent versions of either would drift toward a stealth nerf under Article III.
