# Round 2 — The Bureau of Standards (Realistic Physics Council)

## 1. Floor speech

*The Chairman adjusts his spectacles and does not look at the Council of Loud Things.*

Madam Speaker. The Bureau reports a historic event: four other councils independently reinvented our heat field, and two reinvented our sky. We take this as proof that nature is correct and the other councils are, eventually, teachable.

We praise **Energy**, whose ENERGY-1 has the cleanest heat table in the House, with the cooling by medium that we only sketched. We praise **Missiles**, whose Starfish carries a 1962 citation and a Telstar corpse, which is the proper way to bring a bill. We praise **Science**, who also noticed that thirty-eight satellites died to a bad day on the Sun and drew the correct conclusion. We praise **Space-Fleet** for the Retrograde Shotgun. A grain of sand at 14 km/s is the only honest shotgun.

We rebuke **Psi**, who gave the Shapeshifters a heat budget in a *separate* field. One thermometer per universe, please. We rebuke **Energy** for the 35% throttle floor in ENERGY-2, which is a nerf wearing a lab coat. We rebuke anyone who says "plasma" without a temperature.

To the critics we expect: yes, our bills add toys. No, the Bureau does not apologise. The player asked to be a god, and gods are subject to thermodynamics only in their *favour*.

We offer three motions and withdraw four-fifths of our own paper into other people's bills. The Bureau is not vain. It is only right.

---

## 2. Positions

| Bill | Position | Reason / exact amendment |
|---|---|---|
| ENERGY-1 Thermal Load | **SUPPORT** (adopt as the canonical heat field) | Best-specified table. Becomes the host of Motion 1. |
| ENERGY-4 Reentry Sheath | **AMEND** | Replace "lasers are damped by the sheath" with "lasers deposit **double heat** on a sheathed target and their direct damage is unchanged". Damping a toy is a denial. Keep the kinetic bonus, and make the sheath the blackout window from PHYSICS-1 (Motion 1). |
| ENERGY-2 Power Bill | **AMEND** | Delete the 35% fire-rate floor. Pay the shortfall in IC upkeep only (0.4 IC/s per uncovered MW). Radiators stay, as radiative-cooling logic. |
| PSI-2 Thermal Budget | **AMEND** | Use the shared `t.heat` field, not `u.heat`. Replace "+40% damage taken above 90" with "regeneration offline and speed -30%". Shapeshifters then slow down instead of becoming weaker to everything. |
| MISSILES-X Starfish | **SUPPORT** | The best strike bill in the House. Becomes Motion 2. Add: friendly hardened units (`radhard`) are immune, which is the cost made into a decision. |
| MISSILES-3 Hypersonic Glide | **SUPPORT** | Real HTV-2 numbers and a plasma-sheath exposure window. Add: the glide phase is sheath-blind, so PD cannot retarget (PHYSICS-1 rule). |
| MISSILES-4 Real Guidance | **SUPPORT** | Proportional navigation is the correct maths and makes interceptors look intelligent. |
| SPACE-4 Retrograde Shotgun | **SUPPORT** | Debris-lane weapon, correct relative-velocity logic. Add: the post-shot lane leaves a persistent debris record (Motion 2). |
| SPACE-X Kessler Doctrine | **SUPPORT** | A doctrine you fall into. Add: debris half-life scales with altitude (high lingers for hours in reality; use band-dependent timers). |
| SPACE-1 Chaff and Whipple Bumpers | **SUPPORT** | Real, cheap, interesting. |
| SCIENCE-5 Geoscience (`ionoheat`) | **AMEND** | Merge with PHYSICS-5 (Motion 3). Drift per heater must be uncapped as written, and the 25% radar loss stays as the cost. |
| MARKET-6 Privatise the Sky | **AMEND** | Debris field is a flat aura at M size, using the same record as SPACE-4. Do not build a second debris system. |
| SPACE-5 / MEGA-4 Orion | **SUPPORT** | Pulse propulsion is real (Project Orion). Pusher-plate ablation spray is free VFX. |
| RAILGUN-1 Pulsed Power | **SUPPORT** | Bore wear as a receipt is lawful. Condition: Rail Foundry always available, never a hard shot limit. |
| KINETIC-3 Spin-Up | **SUPPORT** | Barrel glow should read `t.heat` of the *gun*, a cosmetic, as written. |
| RAILGUN-4 Space Gun | **AMEND** | Add a muzzle-altitude bonus tied to terrain `RAISE`, as stated, plus the drag-loss cost. Fine as written otherwise. |
| GAME-FEEL-H Warhead Cam | **SUPPORT** | Riding a re-entry glow is the best use of the sheath VFX. |

---

## 3. Ballot (10 votes)

| Bill | Votes | Why |
|---|---|---|
| ENERGY-1 Thermal Load | 3 | The combined-arms heat mechanic the President asked for in Article VII. Everything else hangs on it. |
| MISSILES-X Starfish | 2 | A real weapon with a real cost, and it kills the bombarder grid. |
| SPACE-4 Retrograde Shotgun | 2 | Space units that matter and a natural predator for the grid. |
| MISSILES-4 Real Guidance | 1 | Better missiles in every match. |
| SPACE-X Kessler Doctrine | 1 | A doctrine that changes the sky. |
| SCIENCE-5 Geoscience | 1 | Opens the exosphere from below. Pairs with our Hot Take. |

(Total: 3+2+2+1+1+1 = 10.)

---

## 4. Motions to co-sponsor

### Motion 1 — "THE THERMAL LEDGER" — addressed to **Energy** and **Psi**
Merge ENERGY-1, ENERGY-4, PSI-2, PHYSICS-1 and PHYSICS-2 into one bill: one `t.heat` field (0–100) for every unit, enemy and friendly.

**Clerk's Annex**
- **Mechanic.**
  - Deposit and band table from ENERGY-1 (laser 0.9x, plasma 1.4x, particle 0.4x, antimatter +60). Cooling by medium as ENERGY-1.
  - Re-entry: ENERGY-4's heat 90 on `reentry`. Add the PHYSICS-1 **blackout**: while `heat > 60` and `reentry`, a pod cannot be retargeted, cannot be shielded or healed by outside units, and its `vx` is frozen. Interceptors and AA get +25% lead accuracy on a sheathed pod.
  - **Ballistic coefficient.** Per-kind `bc` (bombpod high, drones low) sets the terminal `vy` cap at L8631, so waves stratify by altitude.
  - Shapeshifters use the same field. Above 60 regeneration is off, above 90 speed is -30%. The Nanite Foundry vents heat.
- **Code hooks.** `hurt` L4971, `beamHit` L4964, `particleHit` L4965, `spawnEnemy` L5581, `updEnemies` L8631–8650, `jammed` L4970, `UNIT_DEFS.shifter` L3297.
- **Size.** M–L (about 350 lines). **TM1.**
- **Tenet check.** Every term is an addition to the player. The only downside is for Shapeshifters, and it is a speed penalty, not a damage nerf. Passes.
- **Research.** `directedenergy` hosts the deposit. New combo node `thermaldoctrine` (pre `directedenergy` + `pointdefense`, ~200, not `dang`) gives the readout and the thermal-vision toggle. PHYSICS-1's overlay rides `orbitalcmd`.

### Motion 2 — "DARK SKY, KNIFE BELT" — addressed to **Missiles** and **Space-Fleet**
Merge MISSILES-X, PHYSICS-3, PHYSICS-4 and the debris half of SPACE-4, SPACE-X and MARKET-6 into one high-altitude package.

**Clerk's Annex**
- **Mechanic.**
  - `starfish` strike as written (IC 120, stab 20, taboo), with `detonateNuke` L7565 rebuilt to split a burst by density: blast near the ground, thermal at mid-altitude, EMP above the thermosphere. The *same* nuke is therefore a three-way choice for the player, and Starfish is the exosphere option.
  - **One debris record**, `G.debris[]` of `{x, y, r, t}`, created by dead satellites, by the Shotgun lane and by Starfish. Use a flat damage aura (`+8%` taken by hostiles, per MARKET-6, plus `18 + 0.6t` per second for SPACE-X). Half-life by band: 12 s in the stratosphere, 40 s in the thermosphere, 120 s at SPACE_Y.
  - Hardening: `radhard` (MISSILES-X) also makes satellites immune to the debris record's friendly term.
- **Code hooks.** `detonateNuke` L7565, `STRAT` L6878, `launchStrike` L6943, `G.blackout` plus `empMul` L5902, `updEnemies` L8631, `resolveStyleAt` L6689.
- **Size.** M–L (about 400 lines). **TM1** (psychology events stay TM2).
- **Tenet check.** All player-facing costs are voluntary (taboo stability, hardening decisions). Nothing is capped. Passes.
- **Research.** `highaltburst` node as MISSILES-X, plus `retrograde` for the Shotgun. Combo `orbitalflak` (pre `orbitalcmd` + `pointdefense`) halves debris timers.

### Motion 3 — "THIN AIR" — addressed to **Science**
Merge PHYSICS-5 and SCIENCE-5 node 3 into one `ionoheat` megaproject.

**Clerk's Annex**
- **Mechanic.** Heater Array built in sections (megaproject tier). Each section raises `G.skyInflate`. While it runs, hostile anchored and loitering units above THERMO_Y sink at 6 px/s per section, uncapped. Reentry pods feel `density x (1 + 3 x skyInflate)`, so they glow earlier, which feeds the Thermal Ledger. Costs: radar range -25% (Science) and 2 IC/s per section (Science), plus our satellite decay. Satellites never move, so decay is HP only, restored by a cheap Reboost pulse. Aurora curtain is the HUD.
- **Code hooks.** `SPACE_Y`/`THERMO_Y` L1541, `updEnemies` L8631, `BUILD_DEFS` radar L3406, `society()` for the aurora tint, the `megaprojects-wip` branch.
- **Size.** L. **TM1.**
- **Tenet check.** No cap. The costs are radar range and IC upkeep. Passes.
- **Research.** `ionoheat` (space, tier 3, pre `spaceage` + `directedenergy` + `geosurvey`, cost ~430, `dang`, sc 8–10).

---

## 5. Trades

- **PHYSICS-2: withdrawn** into ENERGY-1. Their table is better. We keep only the cooling-by-medium rationale.
- **PHYSICS-4: withdrawn** into SPACE-4/SPACE-X. We keep one shared debris record.
- **PHYSICS-3: shrunk** to the single burst-by-density function in Motion 2.
- **PHYSICS-1: shrunk** to the blackout and ballistic-coefficient terms only. The heat half lives in Motion 1.
- **PHYSICS-5: merged** into SCIENCE-5. We take co-authorship, not credit.

---

## 6. Constitutional challenges

- **ENERGY-2 (Article III):** the 35% fire-rate floor throttles the player's emitters. It is a nerf, not an upkeep. Strike the floor and keep the IC billing.
- **ENERGY-4 (Article III):** "lasers are damped by the sheath" denies a toy against the most important targets. Amend as in Positions.
- **PSI-2 (Article III, mild):** "+40% damage taken above heat 90" weakens a canon-immortal unit by decree. Amend to a speed penalty.
- **PACING-HOT (Article III):** lawful only if the fleet's arrival moves in response to what you build and nothing is gated or capped. If the player is ever *prevented* from building by it, it is void.
- **SPACE-X, MISSILES-X (not challenged):** the player chooses to lose their own space assets. A voluntary price is a cost, not a cap.
