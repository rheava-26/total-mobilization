# PACING & TENSION COUNCIL — Round 1 position paper (`pacing`)

*The War Cabinet Secretariat. We keep the clock.*

## 1. Opening speech

Honourable members. Five wins in a row. Five times the Autofleet fell through the sky, five times it was ground down to 15% and fled. The Secretariat has read the minutes and notes, with the dryness of a clock, that this is not a war. It is a *procedure*.

The designer told us what he loves: "waiting for them to show up felt genuinely stressful because of everything you'd invested." That is our whole ministry. Dread is made of three ingredients: *not knowing*, *having something to lose*, and *a clock you cannot argue with*. The game has the clock and the stakes. We find the not-knowing missing. At T-12 a toast appears. The player already knows the intent, the fleet size and the shape of the attack, because they have played it five times. The fleet that is supposedly afraid of Earth never *adapts to Earth*, and when it loses it leaves, and the match ends in fifteen seconds as if someone had pulled the plug.

The Autofleet's makers are afraid. Frightened people do not retreat politely and never return. They come back angrier, or they come back wiser. The Secretariat proposes that the war have a *shape*: a countdown that lies a little, a fleet that watches you build, a retreat that is a pause, and more than one door out. We will not touch a single toy. Every bill below makes the *enemy's clock* scarier. The Industrial Council may keep its 850 IC/s. We would like to know how the fleet feels about it.

## 2. The Gap

- **One win condition, one ending.** `startWithdraw()` (L5990) fires when `G.fleet<=0 && G.invPhase>=3 && breakCount()<=withdrawThreshold()` (L6505, `WITHDRAW_BREAK_FRAC=.15`, L3858). The ROADMAP (v4.6.0) says all five recent wins ran this path. Fifteen seconds later `G.withdraw.t>=15` (L6546) ends the match. There is no second siege, no counter-doctrine, and no consequence beyond a +15 stability bonus.
- **Prep dread is under-built.** The only prep beat that touches the invasion is the `prepTimer<12` toast (L6492). The timer is a known number (`recon 200 / decap 135 / invasion 450 / glass 500`, L3598). The intent is announced in the HUD, so the player holds perfect information from T-450.
- **The phase clock is fixed.** `updateInvPhases` (L6019) hard-codes phase 2 at invTime 18 and phase 3 at 50. The wall of five (`ESC.OPEN_BOMBARDERS`, `openingBombarders` L5306) and the flagship (`tickCarrierArrival` L5401, 25s into phase 4) arrive the same way every match.
- **The fleet adapts only locally.** `scanEnemyIntel` (L5737) re-reads you every 45s, which is good, but it forgets everything when the match ends. Meta-progression is only `tm_bestEndure` (L1040) and the `markEver` win flags (L999).
- **Scenario variety is four difficulty labels** (`INTENT` L5119). The designer called it "slightly repetitive". A new intent is a number slider. It is not a different *plan*.

## 3. Bills

### PACING-1 — THE SECOND SIEGE (the fleet withdraws to orbit and returns)

*Pitch.* "Members, a retreat is not a surrender. The fleet that lifts off is on a trajectory, and trajectories come back. Give the player a tracked return, a plan, and a worse fleet."

*Player fantasy.* You broke them. You watch the lift-off, and then the radar draws the arc of their return. You have a short window to turn a victory into a *fortress* before round two, and you know, with a countdown, that the second fleet has been built from what you used against it.

**Clerk's Annex.**
- **Mechanic.** `startWithdraw()` keeps its banner, +15 stability and ordnance cut-off, but sets `G.cycle++` and, when `G.cycle < it.cycles`, replaces the 15-second end with an **INTERBELLUM**. Survivors climb out (existing lift-off path, L8622) and the return countdown begins (`G.interT`). During it, Space-band survivors loiter on **elliptical return orbits**, drawn as an arc on the HUD. `it.cycles = {recon:1, decap:1, invasion:3, glass:1, extinction:1}`. Interbellum is `45% of prepTimer` (about 200 game-s at Invasion), with a build/research clock like prep, and the player may keep killing survivors on the arc. Return fleet: `fleetMax = round(270·diff·fleetMul·(1+0.6·cycle))`, a fresh phase 1-3 compressed to 60% of the old timings, bombarder wall of `5+2·cycle`, and **counter-doctrine** (see below). The final cycle still ends in withdrawal-victory. Stability resets to `min(cap, stab+15)` at each cycle, so no death spiral.
- **Counter-doctrine.** Read `G.stats` (kills by domain: air/ground/naval/space). The dominant killer domain gets a fleet answer next cycle through existing hooks (`scanEnemyIntel`, `spawnEnemy`'s `escorts` tables): e.g. air-dominant gets +50% `escortfighter`/flak density and a second flagship flak field (`flagshipFlakBurst`, L8567). Naval-dominant gets submarine-hunter frigates. The fleet answers what *works*. It never nerfs the player.
- **Science.** A real orbit. A craft lifted to apogee *must* return at the orbital period `T = 2π√(a³/μ)`. A Molniya-type orbit (12 h, e≈0.74) dwells over one hemisphere for about 8 of 12 hours, so a retreating fleet is predictable, and its return window is *computable* by tracking. Radar precision orbit determination is routine (the NORAD catalogue). The fleet cannot hide its return, so the player is told when.
- **Tree.** New node **`orbtrack` DEEP-SPACE TRACKING**: pre `orbitalcmd` + `pointdefense`, cost tier 200 (×1.6 load rule), not `dang`. Unlocks the **Tracking Array** building (cat DEF) that shows the return arc and its ETA to ±5s. Without it the return time shows as "±40s". Combo `orbtrack`+`particlebeam` gives a *predictive* lead on re-entry for beam weapons (existing `ceil`/targeting hooks).
- **Code.** `startWithdraw` L5990, trigger L6505, end-timer L6544-6546, `INTENT` L5119 (add `cycles`), `ESC` (scale constants by `G.cycle`), `updateInvPhases` L6019, `end()` L10345 (rows: ADD `SIEGES REPELLED`).
- **Size.** L (about 450 lines). **TM1.**
- **Tenet.** Pass. The enemy gets scarier, the player's toys are untouched, and the extra prep is a gift.
- **Interactions.** Enemy council (counter-doctrines, bigger waves), Space council (survivors loitering in orbit are a target-rich opportunity for space fleets), Economy (surplus IC has a place to go: a second prep).

### PACING-2 — THE LONG DARK (a prep that is read by sky and by instrument)

*Pitch.* "Dread is a clock with a smudge on it. We shall give the clock a face worth staring at."

*Player fantasy.* You watch a pale star in the sky brighten as the fleet's deceleration burn begins. The HUD says "ETA 6:10 ±35%". You buy radar and the number tightens. At T-60 you can read the manifest. You have one night where you *know the number* but not the *shape*.

**Clerk's Annex.**
- **Mechanic.** Add five **prep beats** keyed to `tutPrepFrac()`-style progress (`1 - prepTimer/duration`): 70% DEEP CONTACT (a rendered point of light in the sky band, brightness ∝ `1/d²`), 45% THE BURN (the point becomes a plume and widens), 25% MANIFEST (the fleet size is quoted ±uncertainty), 8% SPLIT (the formation breaks into 3-5 tracks, one per landing flank), and the existing 12-second RE-ENTRY. The **uncertainty** is a function of intel: base ±40% on ETA and fleet size, minus 8% per Radar (min ±5%) and 15% per Tracking Array. At the end the forecast resolves *exactly*, and an incorrect forecast never lies about the *intent* label. It fuzzes the *numbers* only. A match-start roll biases the displayed numbers (±10%), not the true ones.
- **Science.** Real. A decelerating torch ship is the brightest object a civilisation can make (a fusion rocket at 10⁹ W is visible interstellar-scale). Apparent flux follows inverse-square. Waste-heat IR is the real SETI technosignature. Earth's own power use (about 18 TW) makes it visible *back*. See the Hot Take.
- **Tree.** Reads existing `radar` (L3406) plus the new `orbtrack` from PACING-1. No other unlocks.
- **Code.** `G.prepTimer` loop L6492, HUD `syncHUD` L11121 (ETA text), `banner()` L10335, `musDuck`, `tutPrepFrac` L11419 (beat progress reuse).
- **Size.** M (about 220 lines). **TM1.** Intel-sharpening is TM1 slice 2 of Article IV.
- **Tenet.** Pass (information is *fuzzed*, no toys touched).
- **Interactions.** Intel-and-doctrine councils (forecast-sharpening buildings), Visuals council (sky render), Audio (beat stingers on `musDuck`).

### PACING-3 — GAMBITS (a fleet that arrives with a plan, not a difficulty)

*Pitch.* "Four intents, and they are numbers. We want *plans*. The fleet has a mind. Let it pick one."

*Player fantasy.* Every match the fleet opens differently. You scout it in prep and, if you read it right, you are ready. If you read it wrong, you remember why.

**Clerk's Annex.**
- **Mechanic.** A new `GAMBIT` table. At match start the fleet draws one (seeded, shown in the HUD after the 45% beat, with a "SUSPECTED / CONFIRMED" tag by intel level). Each gambit is a patch over `INTENT`/`ESC` and `supers` schedule. Initial six:
  1. **HAMMER** (current default: the wall of five).
  2. **NET**: satellite grids first, bombarders delayed 40s; kills space units early, so space investment is *paid for by* a counter-play window.
  3. **FOBS**: warheads come from the *south*, so they enter outside your radar arc for the first 8s (re-entry warning compressed).
  4. **ROD STORM**: a rain of kinetic rods before any troops land.
  5. **WEDGE**: a single enormous flank landing plus feints (`doWave` snowball cranked on one zone).
  6. **TROJAN**: landings preceded by decoys (existing `decoy` kind) and an `empburst` wall.
  Gambits modify but never reduce the fleet's total strength (`fleetMax` unchanged).
- **Science.** FOBS (Fractional Orbital Bombardment System) is real, a Soviet 1960s design that dodged early-warning arcs. Kinetic rods ("Rods from God") are real concept studies, hitting at 2-3 km/s in tungsten. Both are *cheaply* explainable and make a good sky.
- **Tree.** Adds a *response* node, not a lock: `pointdefense` + `orbtrack` answers FOBS by granting an all-round radar; `shieldtech` answers rods. No unlocks needed for gambits themselves.
- **Code.** `INTENT` L5119, `ESC` L5160-ish, `phase3AllOut` L5937, `phase3Skyfall` L5947, `openingBombarders` L5306, `doWave` L5533, `spawnEnemy` L5581.
- **Size.** L (about 600 lines; most are data tables over existing spawners). **TM1.**
- **Tenet.** Pass. This is variety, not nerf. Gambits can be *worse* or *different* for the player, but never stronger on the *defender's* debt.
- **Interactions.** Enemy council (gambit authors), Air Defence (FOBS angle), Cities (Trojan landings).

### PACING-4 — WAR CHARTERS (progression by posture, not by power)

*Pitch.* "The nation is not blank on day one. Let the player *choose what kind of nation* faces the sky, and let the fleet notice."

*Player fantasy.* Your third victory unlocks a new Charter. "Garrison State" starts you with a pre-built bunker belt and +10% army cap items; "Industrial Surge" starts you with extra IC and slow research; "Open Society" gives influence faster. Each choice is a *different opening*, never a stronger one.

**Clerk's Annex.**
- **Mechanic.** `tm_charters` in localStorage (same pattern as `markEver`, L999). Winning on intent X unlocks one Charter. A Charter is a start-state preset: a bundle of ±starting values (all positive on one axis, with matching *fleet counter-posture*: Garrison State draws the NET gambit, Industrial Surge draws a larger first wave). Maximum one Charter per match. There is a **Mandate** toggle: pick a harder fleet for a bonus (the ×1/×2.5/×5 intensity tiers already banked in ROADMAP). The mandate scales *the fleet*, never caps the player.
- **Science.** Pre-war posture is real: Finland's 1930s fortification versus Swiss mobilisation doctrine versus Israeli reserve systems. Defence planners distinguish "forward defence", "defence in depth" and "elastic defence".
- **Tree.** Charters grant a *free first node* from a branch (e.g. Industrial Surge hands you `automation`). They never hide nodes.
- **Code.** `newGame` init L3598, `SCEN` L10251, `end()` L10345 (award on win), `markEver` L999, `INTENT` L5119.
- **Size.** M (about 250 lines). **TM1.**
- **Tenet.** Pass: nothing is capped, starting packages are additive, and the counter-posture is on the enemy side.
- **Interactions.** Society layer (TM2 seed: Charter *is* the TM2 starting society), Meta (achievements).

### PACING-5 — THREE DOORS (more than one way to end the war)

*Pitch.* "A fleet that only leaves when 85% of it is dead is not afraid. It is *stubborn*. Fear has many triggers."

*Player fantasy.* Kill the flagship and the fleet routs. Clear the exosphere and the fleet loses its anchor. Or break them the old way. Each ending is a different epilogue, and the space fleet finally has a victory condition of its own.

**Clerk's Annex.**
- **Mechanic.** Add two withdrawal triggers beside `breakCount()<=threshold` (L6505), each with its own banner/epilogue string in `end()`:
  1. **ROUT**: the flagship (`carrier`, `tickCarrierArrival` L5401) dies *and* `breakCount()<=35% of fleetMax`. The fleet's command point is gone. Epilogue "DECAPITATED". (Today the flagship kill only grants +15 stability, L6863.)
  2. **ORBITAL DENIAL**: the Space band (`<SPACE_Y()`) holds fewer than 4 live enemy hulls for 40 continuous game-seconds *after* phase 3, and your Space units alive ≥ 25. The fleet cannot resupply, so it withdraws. Epilogue "THE HIGH GROUND". Space units and the bombarder grid finally matter in the arc.
  3. **ATTRITION** (current, unchanged).
  A fourth, rare door is the *canon draw*: at extreme drift the makers offer "we stop if you stop" (TM2 epilogue slot; stub only here).
- **Science.** Orbital denial is real doctrine: whoever holds low orbit holds the high ground, and ASAT plus space-based interceptors are the standard problem. Command-and-control decapitation is the doctrine of the 2003 "shock and awe" opening.
- **Tree.** ORBITAL DENIAL needs `orbitalcmd` + `spaceage` (existing). ROUT is open to all. Combo `orbtrack`+`massdrivers` unlocks a *flagship-hunter* targeting aid (lock indicator on the carrier).
- **Code.** L6505, `startWithdraw` L5990 (take a `reason` arg), `end()` L10345, `WITHDRAW_BREAK_FRAC` L3858, `tickCarrierArrival` L5401.
- **Size.** M (about 180 lines). **TM1.**
- **Tenet.** Pass: purely adds routes to victory.
- **Interactions.** Space council (finally a win path), Enemy (flagship protection), Meta (epilogue strings).

## 4. Hot Take

### PACING-HOT — THE FLEET WATCHES YOU BUILD (the clock is afraid of your industry)

*Pitch.* "Canon says the fleet cannot siege an industrial planet for long. So why does it wait patiently while we build 850 IC/s? **It should not.** The fleet watches your waste heat, and the louder you industrialise, the sooner the clock runs out."

*Player fantasy.* Prep stops being a free build-out. You can mass-produce, and you will, but you are *racing*. The alarm meter climbs as your power grows, the arrival slides up, and you must choose: bury your industry (a heat shield), or accept an early fleet. The enemy is overwhelming, but now it is *impatient*.

**Clerk's Annex.**
- **Mechanic.** `G.alarm` accumulates from **thermal signature** `H = Σ(power output) · (1 − 0.5·buriedFrac)`. During prep each tick the countdown drains `dt·(1 + k·max(0, log10(H/H0)))` with `k=0.25`, clamped so the fleet never arrives before `55%` of nominal prep. Visible as an ALARM bar plus a headline ("THE FLEET HAS NOTICED YOU"). Buildings that are `autoBury`/underground (`UG_DEPTH=50`) count at half. A *Cold Plant* (reactor with radiators dumped to a heat sink) halves a building's contribution. The arrival fleet is *not* weakened by coming early. The player owns the choice.
- **SCI-FI frame, real basis.** The real basis is **waste-heat technosignatures**: any civilisation at power P radiates at least P as infrared, detectable at astronomical distance (Dyson-sphere IR surveys; Earth's ~18 TW is a measurable IR excess). SCI-FI: the Autofleet reads this at transit range and its makers' jump drive *charge clock* (canon: a year) is running down to the fleet's own deadline, so *the more Earth industrialises, the less patient the thrown ship may be.*
- **Tree.** Needs no research: it reacts to what you *build*. A counter node `coldplant` under `def`/`directedenergy` (pre `directedenergy`+`pointdefense`, cost tier 130) unlocks the radiator buildings.
- **Code.** Prep loop L6492 (tick the drain there), `econ` income (see `G.mult.econ`), `UG_DEPTH` buried flag in `mkBuilding` L3670, HUD L11121.
- **Size.** M (about 200 lines). **TM1.**
- **Tenet check.** A knife-edge, flagged openly. It adds a *cost* to growth (a real cost, allowed under the tenet's "real cost (upkeep, logistics)" clause). It does not cap, nerf or deny any unit or building, and the player may ignore it entirely at the price of a shorter prep. If the President judges that a shorter clock reads as a nerf to the build-out fantasy, the fallback is to keep the alarm as *information* only (it changes the fleet's *gambit*, never the clock).
- **Interactions.** Economy (surplus IC outflow has a reason), Underground Railway seed (burying is now a decision), PACING-2 (alarm is a prep beat), PACING-3 (the gambit may be chosen by alarm).

## 5. Alliances sought

- **Enemy/Fleet council**: for the counter-doctrine table (PACING-1) and the gambit set (PACING-3). Their units, our timing.
- **Space council**: PACING-5's ORBITAL DENIAL gives their fleet a victory path the bombarder grid has denied them.
- **Intel/Society council**: PACING-2 and the Hot Take are the TM1 intel-and-forecast slice; we will share the `orbtrack` node.
- **Economy council**: the 212k unspent IC finds a use in the Interbellum (PACING-1) and under the Alarm (Hot Take).
- **Audio/Visual**: the five prep beats need a light in the sky and a sting. We will cede all credit for the sound.

*Respectfully filed, the Secretariat. Time is not on anyone's side.*
