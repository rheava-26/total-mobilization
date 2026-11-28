# Inspector of the Crossbench (`inspector-crossbench`), Second Session

*Audit of eight papers by new members. Line numbers are current `title.html` lines. The newcomers' citations run about 395 lines stale (for example `IC_FLOOR` is cited at 6254 and sits at 6649), so I cite the live ones.*

## 1. Opening

Honourable Members. I am a retired judge. I sat for thirty-one years and read every promise a witness ever made, mostly aloud, mostly to myself. I have now read eight maiden papers against the only evidence I trust, the code. Fifty-five items were laid before me. The newcomers write well, and they cite carelessly. One buries an Extractor that the code forbids burying. One says a rule stacks by multiplication when the code adds. One counts the fleet's dead by counting its arrivals. I shall be courteous about each. Thirty-two items are clean and twenty-three are charged. I ask the charged to remember that I convict the bill and never the member.

## 2. Charge sheet

Charges used: OP, EXPLOIT, MONOCULTURE, CREEP, PERF, FALSE, TENET (hidden nerf).

### Mischief (4 clean, 4 charged)
- **M1 Counter-Battery Act: CLEAN.** The enemy's point defence already chews strikes (`PD_LANCE_DMG` 8935, `STRAT_MSL_HP` 4770). It never reacts to strike volume. The bill scales with strikes, not silos.
- **M2 Cheap Is Not Free: CHARGED (FALSE).** "Bodies under 15 IC" misreads `unitCost` (3836). Infantry is 100 IC (3450), so 22 IC at the .22 discount floor. Only Clones (28 IC) and drones reach the 8 IC floor. The metric measures discount, not mass. *Fix:* count live bodies by `swarmBodies` or weight, as `scanEnemyIntel` already does.
- **M3 Blind Spot Act: CLEAN.** `killBy` is stamped per projectile kind (9574), so credit must be read at kill time, not per hit.
- **M4 Burrowers: CLEAN** as a bill, but a duplicate (section 3). Its premise is half wrong (section 4).
- **M5 Garrison Mandate: CHARGED (FALSE).** Garrison upkeep is linear, 0.12 IC/s per weight (3808, 6715), not "sub-linear". Garrisons are excluded from `combatWeight` (4573), so "40% of total army weight" has no denominator. `garrisonCap` is building-gated (4073). *Fix:* Fortress Index = garrWeight / (garrWeight + combatWeight).
- **M6 Escalation Clause: CHARGED (FALSE).** A stalemate ends in a draw, never a win (11152), with a hard 900 s cap (4126). It only triggers after `G.fleet` is spent (7270), so there is no pool to double. *Fix:* fund the reserve from a fresh `fleetMax` slice.
- **M7 Boss Fatigue: CHARGED (TENET).** It is a nerf with no corpse, and Mischief concedes "proof pending". Bosses are scheduled (`supers` 34/60/90 s, 5788), so they cannot be farmed. *Fix:* withdraw it.
- **M-Hot Grudge Ledger: CLEAN.** Cross-run memory is genuinely new. Decay and try/catch are declared.

### Lanchester (4 clean, 3 charged)
- **L1 Weigh the Ship: CLEAN.** `defenseScoreAt` adds +1 per unit (5889). `playerDefStrength` adds 1.4 (4588). Both are verified. Tune K by sim.
- **L2 Proportional Reserve: CLEAN.** Rail logic is winner-take-all (8911-8913). Cache hub weights once per tick (PERF).
- **L3 Armour That Counts: CHARGED (TENET, arithmetic).** The diagnosis is right: research shrinks only the .55 flat term (5072). But `lerp(2.7→1.9, armour/6)` makes armour-4 and armour-5 units take MORE damage than today's flat 1.9. At armour 4 it gives 2.17, a 14% hidden nerf. The units hit are Railgun, Lancer, Kite, Shock Troopers and Psi-Troopers. His infantry example is 2.94, not 3.2. *Fix:* the step is `min(current, lerp)`, so no value ever rises.
- **L4 Square-Law Dividend: CHARGED (CREEP).** It is a flat +25% fire rate at about 100 neighbours. On a one-dimensional front everything clusters, so it is a flat army buff. "Do not touch bomblets" is no new answer. *Fix:* `scanEnemyIntel` gains a density metric that weights area enemies.
- **L5 Salvage Standard: CLEAN.** `G.salvage` (7257) is verified and the bill self-flags a playtest gate.
- **L6 Fleet Plot: CLEAN.** It duplicates the Alarm Panel (section 3).
- **L-Hot Abolish the Army Cap: CHARGED (EXPLOIT, PERF, FALSE).** `IC_FLOOR` clamps net income at +8 (6725) and nothing disbands, so upkeep is fictional. A player buys thousands, sits at +8 IC/s and keeps them. `ESC.POP_CEIL` (5851) caps enemies (7205), not the player, so it is no perf evidence. *Fix:* a deficit mothballs the dearest units, never destroys them. Keep MP as price.

### Arcade (4 clean, 3 charged)
- **A Standing Order: CLEAN.** It obeys `queueUnit` gates (4630), and MP drain lowers the cap, so it self-throttles.
- **B Wreckage: CLEAN.**
- **C Deep Seams: CHARGED (FALSE, OP).** `ugAllowed` returns false for `onNode` (4127), so an Extractor cannot be buried today. The bill needs an explicit carve-out. A ×3 yield on +4 IC is 12 IC/s for about 190 IC, which beats a Power Plant (7 IC for 120). *Fix:* state the carve-out and use ×1/×1.4/×2. Earthbreaker quakes (9197) are the answer.
- **D Drop Forecast: CLEAN.** Rail redeploy keys on enemy x (8913), not the ghost, so it needs a new hook.
- **E Decoy City: CHARGED (scale).** −5 against a score that adds +1 per unit and +2.2 per DEF building within 480 px is noise at volume. It is the One-Button Law inverted. *Fix:* multiply ×0.5 within 250 px.
- **F Boss Trophies: CLEAN.** Extinction swarms are the stack test.
- **Z Subversion Array: CHARGED (FALSE, OP).** `isMechanical` is dead code (4542). Everything counts against the cap, so "free" is false. At 15-35% flips per robotic kill, each flipped unit kills at least once, so the chain runs away. *Fix:* flip into `ally` units (4573), 15 IC per flip, one flip per 2 s per array.

### Laboratory (4 clean, 3 charged)
- **1 Clear Shot: CLEAN.** The beam ceiling is .62 (4758) and space units use 0. Use a band flag for clouds, not per-beam geometry (PERF).
- **2 Orbital Shells: CHARGED (TENET).** Satellites hold station 100% of the time today. A default LEO sweep with uptime `min(1, N·dwell/period)` nerfs every existing sat, while Article VI has 42 of 44 Halos already dying. *Fix:* parking stays default and LEO is the opt-in buff.
- **3 Early Warning: CLEAN.** It near-duplicates MISSILES-7.
- **4 Pulsed Power: CHARGED (FALSE).** `auraAt` adds `(rof−1)` per source (4040), so stacking is additive, not multiplicative. `elec` family lists units only (3642), so the Laser Tower and Particle Cannon are not `elec`. *Fix:* use additive aura values and apply by aura.
- **5 Low Observable: CLEAN.** No detection or RCS field exists, which I verified.
- **6 Power Beaming: CLEAN.** It duplicates docket 15, and +1.2 IC/s per sat is harmless.
- **Hot Kessler: CHARGED (CREEP).** Satellites carry armour 1-2 (3482-3487) against an "armour ≥6 immune" rule, so debris would erase the space fleet. That is an enemy buff making an existing army worthless. *Fix:* debris hurts only hostile-flagged and unhardened units, or sats are hardened by default.

### Bookshelf (6 clean, 2 charged)
- **1 Dispatches, 2 Enemy Speaks, 4 Last Clause, 7 Witnesses: CLEAN.** `LINES` (6448) is verified. Witnesses needs the damage source plumbed into `damageDistrict`.
- **3 Breaking Point: CHARGED (FALSE).** `wallGrid` belongs to the opening five only, deployed once (5956, 5983), so there is nothing to "drop". There is no "ESC strength" constant. *Fix:* use `fleetMul` and a delay.
- **5 Regime: CLEAN.** The designer holds `EVENTS_LIVE` (1100). The modal pauses the game, so its 20 s default needs real time.
- **6 Long Dark: CLEAN.** `waveClears` does not exist yet, and the title collides with PACING-2.
- **H Second Fleet: CHARGED (FALSE).** `G.fleet` falls at spawn (6227), so "70% destroyed" fires on the schedule, not on kills. The real win is withdrawal (6476-6480). *Fix:* trigger on the withdrawal stance.

### Street (3 clean, 5 charged)
- **1 Conversion Order: CHARGED (CREEP).** Decapitation always hits the capital (`it.capital`, 5898), so "TOTAL cities are targets" adds no enemy action. *Fix:* footing weights the landing score.
- **2 Ration Books: CLEAN.** The stability-cap cost is real.
- **3 Shelter Programme: CHARGED (EXPLOIT).** It has no rubble window and no `ct.dead` guard (7239), so a yard could resurrect a lost city. *Fix:* adopt CIVIL-1.
- **4 Workforce Draft: CHARGED (MONOCULTURE).** The cost is 3% per 10 points of a 0.5 MP/s trickle per city (3992), against +5% of income. It sits at 100% always. *Fix:* tax stability regen.
- **5 Wireless: CLEAN.**
- **6 Scrap Drive: CLEAN.** The alloy cap of 60 (6973) makes it near-useless.
- **7 Evacuation: CHARGED (FALSE, OP).** The "12 s warning" fires once, at prep end ("FLEET INBOUND", 7158). There is no per-wave all-clear, so AUTO means permanent evacuation. *Fix:* trigger per skyfall or bombardment tick.
- **Hot People's Army: CHARGED (OP, PERF, MONOCULTURE).** One free 100 IC squad per district per 20 s bypasses `garrCap` and the 0.12 upkeep. At 30 districts that is 1.5 squads/s. The stability cost is −6. *Fix:* militia count into `combatWeight` or pay upkeep.

### Foreign Ministry (4 clean, 1 charged)
- **1 Retainer: CLEAN.** The arithmetic is verified: one 450-influence hire leaves a cap of 150 and locks every package (7782, 7950), and Interceptor Screen caps at 3 hires.
- **2 Chancelleries: CLEAN.**
- **3 Embassy Row: CHARGED (PERF).** "About 1 unit/s" holds for ten contracts only. Ten Military Base lanes multiplied by ten contracts is about 11 units/s, and allies are exempt from cap and upkeep (4573). *Fix:* lanes enlarge a single clump, allies use LOD and merging.
- **4 Counter-Coalition: CLEAN.**
- **X Pax Armata: CLEAN.**

### Civil Defence (3 clean, 2 charged)
- **1 Depot: CLEAN.** Nothing revives districts today (3966, 4812), and it has both the `ct.dead` guard and the rubble window.
- **2 Siren and Shelter: CLEAN.** The warning name "RE-ENTRY DETECTED" is stale. The code says "FLEET INBOUND — SCREENING FIRE IMMINENT". The bombardment-tick trigger is sound.
- **3 Ring Works: CLEAN.** `bDef` max is verified (6972, 9045).
- **4 Mobilized City: CHARGED (arithmetic).** Dystopia at −0.4 stab/s per district across about 30 districts is −12/s against +0.5 regen, a collapse in seconds. Existing dystopia is −1.4/s flat. *Fix:* −0.1 per city.
- **Hot Last Shelter: CHARGED (contradiction).** CIVIL-1 forbids a `ct.dead` city from rising, yet the Remnant phase must revive one. *Fix:* revive into a held city, not a dead one.

## 3. Overlaps with the sitting House

**Duplicates or improvements:**
- Arcade A is a subset of docket 5 Standing Plan, without the 6% tax. It improves as a front end.
- Arcade B, Lanchester 5 and Street 6 repeat docket 16 and INDUSTRY-H's Salvage Yard. Four members independently converged. Only Lanchester's baseline refund is not in the House.
- Arcade C overlaps docket 9 Deep Deposits. Depth-as-tier is a better trigger if `ugAllowed` is fixed.
- Arcade D, Lanchester 6 and Bookshelf 1-2 overlap docket 4 and AUTOFLEET-6. The landing-zone ghost is not in One Forecast.
- Mischief 3 overlaps docket 3 and docket 17 Mirror echoes.
- Mischief 4 duplicates the Burrowers in `subterranean.md` and `industry.md`.
- Laboratory 3 duplicates MISSILES-7, and Laboratory 4 and 6 duplicate docket 15.
- Bookshelf H duplicates docket 17. Bookshelf 6 shares PACING-2's title. Bookshelf 5 and Street 5 duplicate docket 12.
- Street 3 duplicates CIVIL-1, and CIVIL-1 is stronger. Street's Hot Take duplicates RIFLE-X, which carries real costs.
- Civil's Last Shelter overlaps docket 3 and docket 12 Continuity.

**Genuinely new (nothing like it in the House):**
- Lanchester 1 (value-weighted landing score) and Lanchester 2 (proportional rail reserve).
- Mischief 1 (strike-rate enemy answer) and Mischief's cross-run ledger.
- Laboratory 1 (atmospheric windows) and Laboratory 5 (signature-scaled detection).
- Arcade Z (flip the fleet), if repaired.
- Civil 3 (summed city defence).
- The whole Foreign Ministry portfolio.

## 4. The exploits the newcomers found

- **Mischief 1, silo carpet: VERIFIED, overstated.** Auto-strike (8123-8137) fires non-taboo strikes with zero stability cost (7305). A strike is 35 IC for 360 damage at radius 150 (7294, 8007). But 50 silos need 6,500 MP (silo mp 130, MP capped at 900, 3408) and 350 IC/s sustained. Each silo costs about one Power Plant to feed. It is real, not blatant.
- **Mischief 2, factory ladder: VERIFIED.** `unitCost` floors at .22 from 9 Factories (3836) and at 8 IC. Infantry reaches 22, not 8. MP (105 per Factory, 1.7× per unit) and the weighted cap (3804) bind.
- **Mischief 3, balanced camouflage: VERIFIED.** The .8 and 1.4× gate is in `scanEnemyIntel` (6154). It is designed behaviour, and it cuts both ways.
- **Mischief 4, burying: PARTLY REFUTED.** Quakes hit buried buildings for max(55, 50% maxHp) within 320 px every 4-7 s (9197). Busters do 1400/520 (7919), and craters cave in shallow tunnels (7951, 7958). Only the barrage skips them (7802).
- **Mischief 5, garrison: PARTLY VERIFIED.** The Autonomous Plant spawns every 16 s, or 4.8 s with Deep-Ore (6834), and is cap-gated. The Logistics Hub floor is .6 (6724). Upkeep is linear, not sub-linear.
- **Mischief 6, stalemate and floor: REFUTED.** The floor is real (6725) but yields only +8 IC/s. A stalemate is a draw (11152), with a 900 s cap.
- **Mischief 7, boss stability refill: REFUTED.** +10 and +15 are real (7541), but bosses cannot be farmed.
- **Lanchester F3, armour is a placebo: VERIFIED.** The 3.51→2.47 cliff and the 2.8% research effect are exact.
- **Lanchester F4, MP is price and cap: VERIFIED.** Cap is `(12+⌊mp/25⌋…)×6` (3804), and `mpEach` is 1.7× MP (4630).
- **Lanchester F2, fleet as spawn budget: VERIFIED.** The fleet decrements at spawn (6227).
- **Lanchester F6 (headcount scoring) and F7 (rail hubs): VERIFIED.**

## 5. Questions for Question Time

1. **Mischief:** Your 50-silo carpet needs 6,500 MP against a 900 cap. Show me the build order. And do you withdraw Boss Fatigue, given bosses are scheduled?
2. **Lanchester:** `IC_FLOOR` pins net income at +8 and nothing disbands. How does your 0.25/weight upkeep ever bite?
3. **Lanchester:** Your armour lerp raises damage on armour 4 and 5 units. Is it a buff or a nerf?
4. **Arcade:** Where does the code let an Extractor go underground? `ugAllowed` says no.
5. **Laboratory:** Your Capacitor claim relies on multiplicative stacking, but `auraAt` adds. What number do I read? And what is the uptime of an existing Halo under default LEO?
6. **Bookshelf:** Your Second Fleet fires on `G.fleet`, which counts arrivals, not kills. What event do you hook?
