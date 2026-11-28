# SCIENCE COUNCIL — Round 2 Paper (`science`)

## 1. Floor speech

Mr Speaker, the Academy has read twenty-two other papers and wishes to report that the Parliament has independently reinvented our grant proposals, each under a different letterhead. Physics wants to heat the ionosphere. So do we. Industry wants to salvage the fleet's wrecks. So do we. Lore wants to put the dangerous techs on a dial. Planet wants a deep survey. Market wants a forecast. The Academy was *first*, and it is a scandal that nobody cited us, but we are scientists and we forgive. Merge us.

To Lore, whose Charter is the best bill on the order paper: the Academy would marry it. To Megaprojects, whose Ledger is the frame every doctrine needs: take our three. To Pacing, whose PACING-HOT punishes players for *building*: the Academy would like a word, and the word is Article III.

We expect three attacks. "Research is too complex": every bill we filed reduces to one object on the map or one banner on the screen. "Samples are bookkeeping": the Belenko MiG-25 was bookkeeping that rewrote Western fighter design. "Ionospheric heating hurts your radar": yes, that is the price, and the tenet permits a price. A node that costs nothing is not science. It is a coupon.

The Academy yields the floor, and requests the Speaker note that we are withdrawing two nodes in the spirit of cooperation. Nobody should say scientists cannot share.

## 2. Positions

| Bill | Position | One line |
|---|---|---|
| LORE-1 Charter | SUPPORT + AMEND | Amend: Containment Lab siting (SCIENCE-3) multiplies the Charter's `scMult` (×0.4 beyond 1,500 px of a city, ×1.5 inside 600 px), so Restrain/Standing/Unleash and siting stack. |
| LORE-2 Fleet Resolve | SUPPORT | The scarier-enemy ladder. Our Return Address (SCIENCE-X) fires its escalation phase through `G.resolve`. |
| LORE-3 Black Boxes | AMEND | Fold into one table. `G.dossier[kind]` and our `G.samples[kind]` become the same record. Reward is both: +10% damage vs that kind (Lore) and −12% cost on `studies` nodes (Science). |
| LORE-5 The Wire | SUPPORT | Intercept feed is the forecast's UI. Merge per Motion 3. |
| INDUSTRY-H Salvage | AMEND | Wrecks near a **Research Lab** *or* a Salvage Yard pay out. Its node `salvage` (pre `robotics`, dang sc 3) takes `studies:['harvester','bulwark']`. The Sanitiser reaction stays. Merge per Motion 1. |
| PLANET-3 Deep Survey | SUPPORT | Owns deposit grades. We **withdraw** `geosurvey`'s grade mechanic. Rename our forecast node so the words "Deep Survey" belong to Planet. |
| PLANET-2 Delta Works | SUPPORT | Owns seawalls and tide. We **withdraw** `deltaworks`. Crater-healing (20 s) moves to Planet as a free rider on their node. |
| PHYSICS-5 Breathing Sky | AMEND | Same node: one `ionoheat`. Physics owns the megaproject building and sky-density field. Science contributes: drag-descent 6 px/s per heater, uncapped, radar −25%, 2 IC/s each. Motion 2. |
| ENERGY-1 / PHYSICS-2 Thermal Load | SUPPORT | One heat system. Our Phased Arrays set the heat flag; kinetics profit. Waste-heat upkeep reuses their radiators. |
| MARKET-2 Forecast Exchange | AMEND | Our Deep Survey is deleted as a separate node. Their `futures` node is the research key; our doctrine becomes its capstone (σ halves per Radar/Data Center, horizon extends). Motion 3. |
| MEGA-1 Ledger | SUPPORT | The container for all three of our doctrines (tier "fall into"). |
| PACING-HOT | OPPOSE as written | See §6. Accept only the information-only fallback its sponsor offered. |
| PSI-1 / PSI-2 | AMEND | Psi nodes carry `studies` for the future Hush samples. No change to their numbers. Shapeshifter immortality is not a research question. |
| MARKET-3 War Bonds | SUPPORT | Influence as credit rating pairs with crash funding (SCIENCE-4). |

## 3. Ballot

| Bill | Votes | Why |
|---|---|---|
| LORE-1 Charter | 3 | Turns the silent `dangCap` (L4708) into a decision and a dial. The best bill for the research tree anyone filed. |
| MEGA-1 Ledger of Projects | 3 | The framework that makes combo-doctrines and megaprojects shippable at all. |
| ENERGY-1 Thermal Load | 2 | One heat mechanic gives lasers, kinetics and the tree a combined-arms reason to exist. |
| PLANET-3 Deep Survey | 1 | Gives strategic resources a job, the oldest flaw in the file. |
| INDUSTRY-H Salvage the Fleet | 1 | Sample/salvage unification, with the fleet answering by sending Sanitisers. |

## 4. Motions to co-sponsor

### Motion 1 — THE WRECK CODEX (to: **lore**, **industry**)
*Merges SCIENCE-1, LORE-3, INDUSTRY-H.*
- **Mechanic.** One wreck object per kill (`hp/40` alloy, per Industry), one record per kind (`G.codex[kind]`). A wreck within 700 px of a Salvage Yard *or* 500 px of a Research Lab is "recovered": it pays alloy (Industry), unlocks the dossier (+10% damage vs kind, Lore; +8% family bonus at 4 dossiers) and grants one Sample (Science: −12% cost on nodes whose `studies` includes the kind, floor 40%). Xenotech nodes appear when the class sample exists. After 6 recoveries, Sanitiser drones hunt wrecks, so salvage is a front-line job.
- **Code hooks.** `destroyEnemy` L6834 (spawn wreck), `resCostOf` L4663, `RESEARCH` `studies` field, `buildResearch` L10926, enemy kind in `spawnEnemy` L5581.
- **Build size.** L (~450). **TM1.**
- **Tenet check.** Pure additions to the player; the cost is geography and a smarter enemy.

### Motion 2 — THE BREATHING SKY (to: **physics**, **space-fleet**)
*Merges SCIENCE-5 (`ionoheat`), PHYSICS-5, SPACE-4/SPACE-X (debris).*
- **Mechanic.** Node `ionoheat` (branch `space`, tier 3, pre `spaceage`+`directedenergy`, cost 380, `dang`, sc 8). Ionospheric Heater building: while running, every enemy in the space band descends 6 px/s per heater (stacking, uncapped) until it reaches the mesosphere (`MESO_Y`), where SAMs and railguns reach. Debris from kills in the heated band decays out too, answering Kessler without a cap. Cost: radar range −25%, 2 IC/s per heater. Friendly satellites are exempt (Article IV.2).
- **Code hooks.** `updEnemies` L8596, `SPACE_Y`/`MESO_Y` L1541, `BUILD_DEFS` radar aura L3406, `finishResearch` L4703.
- **Build size.** L (~500). **TM1.**
- **Tenet check.** Adds power. The costs are radar attenuation and power draw, both logistics. The fleet is invited to boost back up (Autofleet).

### Motion 3 — THE UNIFIED FORECAST (to: **market**, **lore**)
*Merges SCIENCE-2 doctrine 2, MARKET-2, LORE-5.*
- **Mechanic.** One HUD forecast. Research key `futures` (Market). Wire lines (Lore) are its text channel. Sigma starts at 1.0; each Radar, Data Center and Forecast Desk is an independent observer (σ/√n). Our capstone doctrine halves σ again and extends the horizon from the next wave to the next phase. At low confidence, 25% of lines are wrong, so forecasts matter without being gospel.
- **Code hooks.** `syncHUD` ~L11133, readiness pill L11713, `INTENT` L5119, `tickFleetStance` L5999.
- **Build size.** M (~250). **TM1** (TM2 society intel later).
- **Tenet check.** Pure information; nothing hidden today is hidden after.

## 5. Trades

- **Withdrawn:** `geosurvey` grade mechanic (to PLANET-3), `deltaworks` (to PLANET-2), SCIENCE-2's separate Deep Survey node (into Motion 3).
- **Shrunk:** SCIENCE-3 Containment stays small (M) and rides on LORE-1. If Lore objects to a second dial, we drop to the distance factor only.
- **Held:** SCIENCE-4 (Parallel Slots) is a UI-heavy L and could be deferred a release if the vote is tight. SCIENCE-X is a hot take and should wait for MEGA-1.

## 6. Constitutional challenges

- **PACING-HOT, Article III.** A prep clock that drains faster as you build punishes the build-out fantasy and reads as a nerf to prep. Valid only in its sponsor's own fallback form (alarm as information, no clock change).
- **Point of order (planet): `capLevels` at 220.** A silent hard cap on the player's terrain edits, in plain breach of Article III unless it is a perf guard shown in the UI. Ruling requested.
- **Point of order (lore): `dangCap` L4708.** Cumulative and permanent, it is a cost, not a cap, *provided* it is visible and the player can choose. Constitutional if LORE-1 passes; knife-edge if left silent.
