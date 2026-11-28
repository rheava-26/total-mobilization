# PSIONIC CONCLAVE (`psi`) — Round 3: Coalitions

## 1. Answers

Two motions name the Conclave. They are the same bill filed twice.

**ENERGY, Motion 1 — "THE THERMAL FIELD" (to physics, psi) — AMEND.**
I co-sign with three amendments.
1. **Regeneration gate is mandatory.** Add to the Shapeshifter clause: while `heat > 60`, per-unit regeneration is off, and the global `G.allRegen` flag (L7866) is replaced by a per-unit gate on nanite-bodied units. Energy's text keeps the resistance but never ends the immortality. Immortality is the thing report 6 broke, so this is the point of the bill.
2. **Slump band.** Above 90, Shapeshifter speed and damage are x0.5 and damage taken is +40%. Energy's "extra damage from enemy beams" is correct but too soft to matter on its own.
3. **Venting.** Dispersed swarms vent +50% faster and the Nanite Foundry vents heat in radius. This keeps counterplay on the player side.

I **accept** Energy's rule that only enemy sources deposit heat on friendly Shapeshifters. Friendly lasers never cook our own swarm. It is cleaner than my original text and I withdraw mine.

**PHYSICS, Motion 1 — "THE THERMAL LEDGER" (to energy, psi) — AMEND.**
Co-sign with one amendment. Physics already carries the regen-off rule at heat above 60, which is right. Amend the slump band to match amendment 2 above (speed and damage x0.5, not speed -30% alone). Physics' tenet note calls the penalty "a speed penalty, not a damage nerf". I agree it is not a nerf: it is a vulnerability the enemy exploits. I add that the heat is deposited by the enemy, and that is the Article III defence for both motions.

**The merge I back.** One bill, one module `heatApply(t, amount, src)` (Energy's), with Physics' re-entry blackout and ballistic-coefficient terms, and the psi amendments above. One node `thermalload` (Energy's) with Physics' `thermaldoctrine` as the readout combo. Size L, TM1.

## 2. Joins

- **Motion B "THE RESOLVE ENGINE" (autofleet, to Lore and Pacing) — JOIN.** Add the psi term: `G.psiN` feeds the Fear gauge as `+0.25F` per 8 psi units, so a swarm of individuals the fleet cannot count scares it. It is the cleanest home for my Anomaly entry.
- **"THE ONE FORECAST" (new-player, to Market and New-Player), "THE UNIFIED FORECAST" (science, to Market and Lore) — JOIN as the base.** My Listening Chamber (Motion C) plugs in as the active, costly 90-second peek that drops sigma to under 0.1 at a stability-cap price, with `listenDebt` feeding Fear.

## 3. Competing Motions

- **Heat:** Energy's THE THERMAL FIELD and Physics' THE THERMAL LEDGER are one bill. Back the Energy module with the Physics additions and my amendments, as above.
- **Forecast (three motions plus mine):** I back **the new-player "ONE FORECAST"**. It already names `G.forecast` with one sigma and wires four faces (manifest, desk, prep beats, Wire). Science's version adds the doctrine capstone and the "25% of lines wrong at low confidence" honesty rule, which I like and want merged in. Market's desk is the price, the Chamber is the active peek. One sigma, many observers.
- **My own Motion B, "THE ANOMALY ENTRY":** I back folding it into the Resolve Engine above. I drop the standalone version. Keep Hush-Hound pickets and Mind-Forge counterplay as the psi content of the Fear gauge's counter-spawns. I yield the Quiet boss to the Autofleet roster design.

## 4. Closing Statement

*(The chamber is colder than when we arrived. The thermometer at the Speaker's elbow has stopped reading.)*

We came asking to be rare. We leave asking to be warm. A swarm that cannot burn is a swarm that cannot be afraid, and nothing that cannot be afraid is worth writing a book about. Give us a field the enemy can read. Give the fleet a number for what it cannot count. And let one chamber listen to the gun being loaded.

We will be here. We were already here.
