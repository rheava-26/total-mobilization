# ENERGY — Round 2 (Directed Energy & Plasma Council)

## 1. Floor Speech

*(The lab coat is now only partly on fire. This is an improvement.)*

Mr Speaker. The Bureau of Physics has tabled a heat mechanic, the Shapeshifters have tabled a heat mechanic, and the Psi council has tabled a heat meter. Three separate thermometers for one sky. We are touched. We are also the people who invented the thermometer, and we would like it back, in one piece, with one set of coefficients.

To the Physics Bureau: your Allen-Eggers corridor is better than our Reentry Sheath, and we say so on the record. Withdraw our bill, adopt your blackout, and give us the orange glow. To the Railgun council: your Capacitor Bank is our Capacitor Bank. Same name, same joules, stolen on separate days. To Space-Fleet: Helios Arrays are a power station in the sky, and a laser needs one. We love you.

To the Kinetic council, who will say that heat makes their shells a sidekick: sidekicks do not get a pay rise from a laser. You do.

To those who say a power bill is a nerf: it is an IC bill, Article III permits it, and we concede that our 35% throttle floor was a knife-edge. We withdraw it. Pay the bill and fire every beam you own.

*(The alarm sounds. The coat leaves. The coat returns, extinguished, with a clipboard.)*

## 2. Positions

| Bill | Position | One line |
|---|---|---|
| PHYSICS-1 Peak Heating | SUPPORT | Allen-Eggers is the right physics and blackout gives interceptors a reason to lead. Replaces ENERGY-4. |
| PHYSICS-2 Thermal Loading | AMEND | Merge into the unified field below: one `e.heat` 0-100, one set of coefficients (see Motion 1). Keep Physics' cooling by medium and "enemy beams heat your buildings". |
| PHYSICS-3 Burst Altitude | SUPPORT | Thermal channel should deposit into the same `e.heat`. Gains its high burst as a mass heater. |
| PHYSICS-5 Breathing Sky | AMEND | Ionospheric heating is a legitimate heat source, so it may feed `e.heat` in the column. Keep it as a heater, not a damage source. |
| PSI-2 Thermal Budget | AMEND | Reuse the shared `heat` field and its coefficients. Strike the "slump" at heat > 90 as player-side punishment unless the heat came from an enemy source (see Challenges). |
| PSI-3 The Lumen | SUPPORT | The laser-filament channel is real science and should deposit +40 heat along its column. |
| PSI-4 The Hush | SUPPORT | A scarier enemy answer to a strong toy. Exactly the tenet. |
| SCIENCE-2 Synthesis Doctrines | SUPPORT | Phased Arrays is our best combo idea and it belongs in a doctrine framework. Amend: its waste-heat upkeep should use the ENERGY-2 power grid, not a second ledger. |
| RAILGUN-1 Pulsed Power | AMEND | Same Capacitor Bank as ENERGY-2. Share one building and one pool (see Motion 2). Bore wear is fine since baseline is the floor. |
| RAILGUN-2 / MEGA-2 Long Gun | SUPPORT | A heated target is the Long Gun's best customer. |
| SPACE-3 Helios Arrays | SUPPORT | A defendable power source in orbit is the missing half of our power bill. Amend: Helios output also feeds the grid MW, not only IC. |
| SPACE-4 / SPACE-X Kessler | OPPOSE (soft) | Debris fields should be a laser-friendly vacuum, not a laser-blocking haze. Amend: beams ignore debris. Otherwise support. |
| KINETIC-3 Spin-Up | SUPPORT | Barrel heat is cosmetic here, which keeps the word "heat" honest. Add a rule: spool does not read `e.heat`. |
| KINETIC-2 Sabot and Shell | AMEND | AP and sabot rounds get a larger armour-cut from heat than HE, for example x1.5. A real reason to pick AP vs a hot target. |
| MISSILES-X Starfish | SUPPORT | Fits PHYSICS-3. A high EMP burst should also blind enemy thermal sensors for 10 s. |
| MEGA-1 Ledger | SUPPORT | The Furnace (ENERGY-H) is a clean test case for the two tiers. |

## 3. Ballot (10 votes)

| Bill | Votes | Why |
|---|---|---|
| SCIENCE-2 Synthesis Doctrines | 3 | The combo-that-changes-the-world fix is the best thing in the paper stack. It is a framework, so every other council's combo gets a home. |
| PHYSICS-1 Peak Heating | 2 | Makes the drop window legible. A clearer skyfall is better for new and old players. |
| SPACE-3 Helios Arrays | 2 | Gives space units a reason to be defended, which is the President's ask from report 7. |
| PSI-4 The Hush | 1 | Every strong toy needs a scarier enemy. |
| RAILGUN-1 Pulsed Power | 1 | Charge pools are a fun, readable resource. |
| KINETIC-3 Spin-Up | 1 | Cheap, S-size, felt in every fight. |
| **Total** | **10** | None for ENERGY-1/2/3/4/H. |

## 4. Motions to Co-Sponsor

### Motion 1 — "THE THERMAL FIELD" (addressed to: physics, psi)

**Merged mechanic.** ENERGY-1, ENERGY-4, PHYSICS-1, PHYSICS-2 and PSI-2 become one system, owned jointly.

**Clerk's Annex**
- **One field.** `e.heat` 0-100 on every enemy and every `shifter`. One module, `heatApply(t, amount, src)`. All deposits (laser, plasma, particle, re-entry, nuke thermal, Lumen) call it.
- **Coefficients.** Deposit: beam `dmg*.9*fade`, plasma `dmg*1.4`, particle `dmg*.4`, antimatter flat 60, re-entry spawn 90 (Physics' formula replaces the flat value). Effect: kinetic x(1 + .5*heat/100), armour cut up to 40% (50% for AP and sabot rounds).
- **Cooling by medium.** Troposphere -10/s, stratosphere -6, mesosphere -3, space -1.2 (radiation, T^4). Physics' density function supplies the number. Land -5/s.
- **Blackout.** Physics' plasma-sheath rule at heat > 60 during re-entry. Lasers are damped by the sheath, so rods and rails are the answer.
- **Shapeshifters.** Psi's rule applies to the shared field: heat from enemy sources only. They keep their resistance, and enemy beams make them take extra damage (a scarier enemy, not a nerf).
- **Code hooks.** `hurt` L4971 (multiplier and armour term at L4985), `beamHit` L4964, `particleHit` L4965, `fire()` L4854-4881, `spawnEnemy` L5581, enemy update near L8759, `UNIT_DEFS.shifter` L3297, `jammed` L4970 for the IR bonus.
- **Size.** L (~450 lines, mostly per-weapon tagging).
- **Tag.** TM1.
- **Tenet.** Pure buff. No player cap, nerf or denial.
- **Research.** One node `thermalload` (branch energy, tier 2, pre `directedenergy`, cost ~150, not `dang`) plus Physics' `reentrytrack`. Combo with `plasma` and `particlebeam` raises deposit +35%.

### Motion 2 — "THE GRID" (addressed to: railgun, space-fleet)

**Merged mechanic.** One **Capacitor Bank** (RAILGUN-1 and ENERGY-2) and one **power pool** fed by Power Plants and Helios Arrays (SPACE-3).

**Clerk's Annex**
- **Pool.** `G.power` stores MJ, filled by Power Plants (+10 MW each) and Helios Arrays (+6 MW each). Capacitor Bank: 220 IC, 60 MP, stores 2000 MJ. A rail overdrive shot spends 1 charge (Railgun), and an energy emitter spends its `pwr` per shot (Energy).
- **No floor, no throttle.** Dropped from ENERGY-2. If the pool is empty an emitter fires at normal rate and bills the shortfall as IC debt at 0.6 IC/s per MW. Fire is never throttled. The bill is the cost (Article III permits upkeep).
- **Cooling.** The Radiator Array (110 IC) removes 25 waste-MW. Uncooled waste is billed at 0.4 IC/s per MW.
- **Code hooks.** Upkeep sum L6313-6323, `fire()` L4854, rate hook near L8244, `BUILD_DEFS` L3381, HUD upkeep L9937 and L9977, `UNIT_DEFS` L3217 for Helios.
- **Size.** M (~320 lines).
- **Tag.** TM1.
- **Tenet.** IC debt can never lock a weapon out, so nothing is capped.
- **Research.** `directedenergy` unlocks Capacitor Bank. `superconductor` halves draw and doubles pool (Railgun's own rule). `ssps` (Space) unlocks Helios.

### Motion 3 — "PHASED ARRAYS, METERED" (addressed to: science)

**Merged mechanic.** SCIENCE-2 Doctrine 1 (+3% damage per other emitter within 350 px, uncapped) plus the Grid and the Thermal Field.

**Clerk's Annex**
- **Rule.** The +3% stays uncapped. Each emitter's waste-heat upkeep is the ENERGY-2 bill, not a flat 0.4 IC/s. A 100-laser block pays 100 draws and is a single obvious power-plant project.
- **Heat synergy.** Phased emitters also deposit +15% heat when 10 or more are in range, so a laser wall paints a target faster. A reason to mass.
- **Code hooks.** `finishResearch` L4703, `fireDoctrine` (new), `beamHit` L4964, power pool as Motion 2.
- **Size.** S on top of the framework (~90 lines).
- **Tag.** TM1.
- **Tenet.** Uncapped synergy, a real IC cost. Passes.
- **Research.** The node is Science's `PHASED ARRAYS` (pre `directedenergy` + `automation`).

## 5. Trades

- **Withdraw ENERGY-4 (Reentry Sheath).** Physics did it better. Keep only our "reentering targets spawn at heat 90" line as a sentence in Motion 1.
- **Withdraw ENERGY-1 and ENERGY-2 as standalone bills.** They become Motions 1 and 2 and I accept the co-owners.
- **Drop the 35% throttle floor** (see Motion 2).
- **Keep ENERGY-3 (Plasmoid) and ENERGY-H (Furnace) as standalone bills.** They are the part nobody else has, and they only need the shared heat field to exist.

## 6. Constitutional Challenges

- **PSI-2 (Article III).** A Shapeshifter that slumps at heat > 90 (half speed, half damage, +40% damage taken) and loses regeneration from heat is a punishment on the player's own toy if self-heating from its own kinetic hits counts. Amend so heat comes only from enemy sources, which makes it the scarier-enemy arm. As written the self-heat is a nerf.
- **Our own ENERGY-2 throttle floor (Article III).** Self-reported and withdrawn above.
- **PHYSICS-2 enemy beams heating your units.** Passes. It is an enemy escalation, and nothing is capped.
