# ENERGY — Institute of Directed Energy & Plasma Council (Round 1)

## 1. Opening Speech

*(A fire alarm is ringing. Nobody has stopped it. A lab coat enters, smoking.)*

Honourable members, President. We are the Institute of Directed Energy. Four of our lasers are currently on fire, and we count that as a successful test.

The Parliament has treated our branch as a damage multiplier for fifty years. Your Laser Tower does 18 damage a shot, and a rifle does 11. We were put in the tree to apply `g.mult.dmg += .1` and nobody asked what light does to a target. Light heats things. Steel at 600 C has lost half its yield strength, and a shell that would have bounced then goes straight through. That is the whole secret of combined arms, and the other councils have walked past it for two reports.

Our Laser Tower fires a beam and the target is exactly as warm afterwards as before. That is a crime against thermodynamics.

We are asking for three things. First, **heat**: a real state on every enemy, visible and exploitable. Second, **a power bill** that costs you IC and never costs you a laser. Third, a plasma weapon that finally behaves like a contained field dumping its soul on impact.

The artillery council will say we are only a buff for them. They are right, and it is their finest hour. We bring the match and they bring the petrol.

*(The alarm stops. Something explodes. Applause.)*

## 2. The Gap

- **Energy weapons are numbers with a different sprite.** `beamHit` (L4964) applies `hurt(t, dmg*fade)` and nothing else. `particleHit` (L4965) is the same with pierce. The Laser Tower, Warden, Solaris and Halo are all statistically interchangeable with a gun.
- **The beam fades with altitude** (`fade` in `beamHit`), which is backwards. Lasers are worst in thick air, where they scatter and bloom, and best in vacuum. The weapon is weakest where the Halo sats work and strongest where the Autofleet is not.
- **Plasma is a kinetic slug already** (v2.7.0 note). It makes no thermal event, no lingering fire and no ring. `plasmaturret` (L3441) is a 75-dmg gun that happens to be purple.
- **Heat does not exist.** Grep `heat|thermal` in `title.html` returns only the AAR heatmap CSS.
- **Nothing is a real cost.** `d.upkeep` (L6313) is used for shields and capital ships. Directed energy has no power draw, so 100 lasers cost the same per second as 1. Report 7 sat on 212k IC, and the only sink was more buildings.
- **Reports 6-10:** 240 Falcons and 113 Kites won on volume. No report shows a combined-arms moment, because no system rewards pairing two weapon types.
- **Space dies to the grids.** Halo sats are 50 HP at range 1200 with no way to hide, shed heat or fight back.

## 3. Bills

### ENERGY-1: THERMAL LOAD (the Heat Mechanic)

**Pitch.** *"Every enemy is a thermometer. Pour light on it and read the number."* Lasers deposit heat. Heat softens armour and brittle-fractures hull, and kinetics then do real work. The pairing is the combined arms: a Laser Tower and a battery of Goliaths are suddenly a single weapon.

**Player fantasy.** Your lasers paint the sky orange while the artillery erases whatever glows. You learn to read the glow and put a laser wing over the guns that need it.

**Clerk's Annex**
- **Mechanic.** Every enemy gets `t.heat` (0-100).
  - *Deposit.* Laser hit: `heat += dmg * 0.9 * fade`. Plasma hit: `+ dmg * 1.4`, and splash heat in radius 70. Particle: `+ dmg * 0.4` (clean, almost no deposit). Antimatter: `+ 60` flat.
  - *Decay.* Cooling is by medium, and that is the science. Troposphere (convection): -10/s. Stratosphere: -6/s. Mesosphere: -3/s. Space (radiation only): -1.2/s. Land units: -5/s, or -2/s when standing on a `G.fires` flame.
  - *Effect, by heat band.* Warm (25-50): kinetic dmg x1.1. Glowing (50-80): x1.25 and armour -25%. Molten (80-100): x1.5 and armour -50%. At heat 100 the target sheds a plume and takes +4 HP/s thermal stress ("thermal shock").
  - *Hooks.* "Kinetic" is `killBy` in shell, rocket, missile, rail, rod, bomb, krod and tracer. Energy `killBy` (beam, particle, plasma) never gets the bonus, so there is no energy-on-energy feedback.
  - *Per-target.*
    - Shielded wings (`t.shield > 0`): heat charges the shield's absorbed damage. A hot shield leaks, so it loses 1% per heat point. This lets energy crack the existing shield-reflects-energy rule.
    - Superheavy bosses (Leviathan, armour 22): heat strips armour, so energy is the key to the walker.
    - Drones and bombpods (low armour): they pop anyway, and heat adds nothing worth taking.
    - Capital ships: huge thermal mass, +0.5 weight. They heat slowly and cool slowly, which rewards massed lasers.
  - *Read.* Heated enemies tint toward orange, then white at 100, via the cached art's tint/overlay pass. Tooltip: "Heat: Glowing (x1.25 kinetic)". A thermal-vision toggle recolours the whole sky by heat. The minimap shows hot dots.
- **Code grounding.**
  - `hurt(t,dmg,ix,iy)` L4971: multiply `eff` by `1 + heatBonus(t)` for kinetic `killBy`, and fold `armor` by `(1 - armCut)`. The armour term is at L4985.
  - Heat deposit in `beamHit` L4964 and `particleHit` L4965, and in the plasma branch of `fire()` L4854-4881.
  - Decay in the enemy per-frame update beside the `lastHitT` tick, L8759-8761.
  - Per-enemy init in `spawnEnemy` L5581 (`heat: 0`).
- **Build size.** M (~250 lines incl. render tint).
- **Tag.** TM1.
- **Tenet check.** Pure addition. Nothing player-side is capped or nerfed. Every existing kinetic gets stronger the moment you place one laser. Passes.
- **Research.** Attach to `directedenergy` (existing node, tier 1, cost 55, not `dang`). Heat deposit is its payload. A new combo node, "Thermal Doctrine" (pre `directedenergy` + `pointdefense`; ~200; not `dang`), adds the heat-band UI and the thermal-vision toggle, so the reward for the combo is the readout.
- **Interactions.** Artillery, armour and missile councils gain +25-50% on hot targets. The air council's Falcons and Kites (the dominant doctrine) become the kinetic that wants a Warden or Solaris escort. Naval gunnery benefits from a laser picket.

### ENERGY-2: THE POWER BILL (Capacitors and Radiators)

**Pitch.** *"A laser is a power station with a lens on the front. Pay the station."* Directed energy draws megawatts, and a good lab keeps the budget in the open. Stored power is a battle pool and cooling is a build. The cost is IC upkeep and never a hard cap.

**Player fantasy.** The war room has a meter for "joules in the bank". A mass of lasers eats it, and your reply is more reactors and more radiators, never "you may only place 12".

**Clerk's Annex**
- **Mechanic.**
  - *Draw.* Each energy emitter has `pwr` (Laser Tower 6, Plasma Turret 14, Particle Cannon 40, Solaris 8). It draws only while firing.
  - *Supply.* Power Plant gives +7 IC and now +10 MW. A new **Capacitor Bank** (SPEC, 140 IC, 40 MP, `req: directedenergy`) stores 2000 MJ and refills at idle. Battle draw comes from the bank first, and the fight is fed by the pool.
  - *Shortfall.* If the pool is empty, energy emitters fire at `supply/demand` of normal rate (floor 35%). They never switch off, and this is a throttle rather than a cap. The floor is stated openly, and more banks and plants remove it.
  - *Cooling.* Each emitter outputs waste heat. A new **Radiator Array** (INFRA, 110 IC, no MP, `req: superconductor`) removes 25 waste-MW. Uncooled waste heat is billed as extra IC upkeep, 0.4 IC/s per MW over the radiator budget, via `d.upkeep`. Real basis: spacecraft shed heat only by radiation (Stefan-Boltzmann, big flat panels), and Earth gets convection for free, so ground cooling is cheap and orbital cooling is costly.
  - *Superconductor Facility* (L3459) halves emitter draw via `gmod`.
  - *Meters.* The HUD gets two bars: POWER (MJ stored) and WASTE HEAT (MW/limit).
- **Code grounding.**
  - Upkeep sum at L6313-6323 (the `upkeep` accumulator and `upkeepCut`). Add `u.d.pwr` and a `G.power` pool there.
  - Firing rate gate in the building weapon loop L5764 and unit fire path `fire()` L4854.
  - New entries in `BUILD_DEFS` L3381 (beside `lasertower` L3439, `superconductor` L3459).
  - HUD upkeep display L9937 and L9977.
- **Build size.** M (~300 lines).
- **Tag.** TM1.
- **Tenet check.** The cost is IC, which Article III names ("upkeep/logistics"). No cap on emitter count. The 35% floor is a throttle with a published fix, and a purist could drop it entirely. Passes, and it gives IC-flush players (212k banked) a sink that matters.
- **Research.** `directedenergy` unlocks the Capacitor Bank. `superconductor` (existing building) unlocks the Radiator Array and the draw discount. New node "Fusion Taps" is NOT proposed. The Fusion Age (+45% IC) is the existing power node, and it also raises plant MW.
- **Interactions.** Economy council (power plants, grids), logistics (upkeep), city defence (`bDef` plasma tier draws power from the grid).

### ENERGY-3: THE PLASMOID (the Plasma Fantasy)

**Pitch.** *"It is a smoke ring made of lightning, held together by its own magnetic field, and when it hits it lets go of everything at once."* Real research: the spheromak and compact torus, plasma held by self-generated fields (Marshall gun, LLNL). The field stays coherent in flight, then collapses on contact and dumps the whole energy budget into the target's surface.

**Player fantasy.** The plasma shot is a slow, bright, wobbling ring. It lands and the target flashes white, the armour glows, and the ground burns for a few seconds.

**Clerk's Annex**
- **Mechanic.** The `plasball` becomes a plasmoid.
  - *Flight.* Field charge decays in flight: damage scales `0.7 + 0.5*(1 - flightTime/maxFlight)`, strongest at close range, and the ring's visual brightness reads it.
  - *Impact.* Direct damage unchanged, plus a **thermal burst**: radius 70, +45 heat to all enemies inside, and a 3-second ground flame (`G.fires`) that deposits +8 heat/s to anything crossing it.
  - *Chain.* A target already above heat 60 is hit "through the sheath": the plasmoid ignores 30% of its armour (the field punches a hot surface).
  - *Magnetic Confinement* node: shot flight speed x1.4, charge decay halved, and the burst radius is 100.
  - Plasma-armed conventional units (UPGRADES, `era: 'plasma'`) keep their current stats and also deposit heat, so the whole re-armed line becomes a heater.
- **Code grounding.**
  - `plasball`/`plasma` weapon branch in `fire()` L4854-4881 and the projectile update (~L9005 area).
  - Impact FX table row `plasma:` L5055 (ring `#d9b3ff`), reuse for the burst.
  - `G.fires` at L5106 shows the fire-family pattern to copy.
  - `plasmaturret` L3441, `plasmabastion` L3458, `UPGRADES` L3314, `applyEraUpgrades` L4284.
- **Build size.** M (~200 lines).
- **Tag.** TM1.
- **Tenet check.** Pure buff to an existing toy plus a new node. Passes.
- **Research.** Existing `plasma` (tier 2, 150). New node **Magnetic Confinement**, branch energy, pre `plasma`, combo with `shieldtech` (def branch, the same field generators), cost ~220, not `dang`. World-changing combo effect: Shield Generator domes become "containment fields". The dome flashes friendly plasma through it, so your plasma batteries can fire out through your own shield without hitting it.
- **Interactions.** Defence council (shield tie-in), artillery (heat bursts feed kinetics), city defence (`bDef: 2` plasma cannons inherit the burst).

### ENERGY-4: THE REENTRY SHEATH

**Pitch.** *"They fall through the air at orbital speed. Do you know what that does to a hull? We do. It makes them glow."* Real science: reentry heating. A body at 7 km/s sits at roughly 1,500-3,000 C on the stagnation point inside a plasma sheath, and the ablative shield is burning off. A dropship is the worst target for armour and the best for a hammer.

**Player fantasy.** The "window" the CLAUDE.md talks about becomes legible. A falling pod glows white-hot, a rod or a railgun round hits it at its weakest, and you feel clever.

**Clerk's Annex**
- **Mechanic.** Every enemy with `t.reentry === true` spawns with `heat = 90` and holds it until `reentry` ends (it ends at the active-phase handoff, L5262), after which normal decay applies. The ablative sheath adds: armour x0.5 while reentering.
  - Kinetic hits on a reentering target get the ENERGY-1 bonus automatically.
  - Lasers on a reentering target are damped by the plasma sheath (it reflects and absorbs, the real "radio blackout" reason), so they should NOT be the answer. Rods, rails and the Particle Cannon are the answer. This pushes the player toward the tall-reach kinetics that matter against skyfall.
  - Landing flare: at touchdown the heat bleeds into the ground as a 4-second scorched ring that damages nothing but marks the landing point on the map.
- **Code grounding.**
  - `reentry` flag set/cleared near L5262 and `spawnEnemy` L5581, `incomingList()` (used in `beamHit`/`particleHit`; note `particleHit` already skips `t.reentry` at L4965, so the cannon cannot hit them, which needs a decision from the owning council).
  - Heat bonus in `hurt()` L4971.
- **Build size.** S (~80 lines, relies on ENERGY-1).
- **Tag.** TM1.
- **Tenet check.** Buff, plus a clearer enemy. Passes.
- **Research.** No new node. It rides `directedenergy` (the heat system) and rewards `spaceage` rods. Optional readout node "Thermal Imaging" is folded into "Thermal Doctrine" (ENERGY-1).
- **Interactions.** Space council (rods, Halo and Volley get a reason to exist at the top of the sky), air council (Falcons can finish hot drops), enemy council (skyfall reads clearly).

## 4. Hot Take

### ENERGY-H: THE FURNACE (Heliostat Field Megaproject)

**Pitch.** *"We will not build a laser. We will build ten thousand mirrors."* The real Odeillo solar furnace concentrates sunlight to 3,500 C with a few hundred mirrors, and the Crescent Dunes tower focused 10,000 of them on one receiver. Nobody on this Parliament would dare say it: **heat should not be a laser's side effect. It should be a landscape.** You build a field of cheap mirrors (heliostats), and the field's focus power grows with every mirror you add, with no cap. Aim it and the sky above the target is cooked.

**Player fantasy.** You carpet a plain with mirrors, throw the switch, and a column of sky turns white. Every enemy in it hits heat 100 in two seconds, and then the whole army fires on it.

**Clerk's Annex**
- **Mechanic.**
  - *Heliostat* (INFRA, 18 IC, 2 MP, w 6, tiny HP). Cheap, mass-spam by design. Each adds 1 "focus" unit.
  - *Receiver Tower* (SPEC, 600 IC) is the focus point. A megaproject built in sections (President's tier 2): 4 sections, each +25% efficiency. It must stand with >= N heliostats within 1,200 px (the `near:'capital'`-style LOCAL `reqs` from the projects framework).
  - *Firing.* Player clicks a column (armed like `armSilo`). Heat deposit = `0.6 * focus` per second, to everything in the column, any altitude up to the mesosphere (the beam passes through a **fixed relay mirror satellite**, so Article IV.2 holds: satellites never move; the relay is a fixed Halo-class sat above the field). 10 s duration. Cooldown 20 s, costing IC to run (spill-over power, `d.upkeep`).
  - *Scale.* 500 heliostats ~ 9,000 IC, which is the kind of IC dump that actually buys a combined-arms moment. Focus is uncapped, and the cost IS the limit.
  - *Weakness (cost, per the tenet).* The field is a fat target and fragile (heliostat HP ~20). The Autofleet's bombarders will happily glass it, which is exactly the "scarier enemy" answer. Clouds and smoke from `G.fires` over the field cut efficiency, so a burning base cooks itself.
- **Code grounding.**
  - Arming/confirming pattern: `STRAT` L6878 and `armSilo`; use `firePsiLance` L7530 and `fireHostileColumn` L5464 as the column implementation templates (the enemy one is the mirror image).
  - Project reqs: `projReqProgress()` on the `megaprojects-wip` branch.
  - Heat deposit via ENERGY-1 `t.heat`.
- **Build size.** L (~700 lines, needs ENERGY-1 and the projects framework).
- **Tag.** TM1 (framework permitting). Otherwise a stripped M.
- **SCI-FI note.** The fixed relay mirror in orbit is plausible engineering but SCI-FI at scale: the Znamya space-mirror experiment of 1993 was a 20 m reflector. Internal logic with canon: Earth's industrial base can only afford one-trick structures against a fleet that cannot siege for long, so a field mirror and one relay is exactly the right "industrial refuse" weapon.
- **Tenet check.** Massive yes: the power scales with spam, never with a cap. Passes.
- **Research.** New node **Solar Concentration** (branch energy, pre `directedenergy` + `spaceage`, cost ~300, not `dang`). Combo with `orbitalcmd` gives the relay mirror sat.
- **Interactions.** Projects council (a textbook megaproject), economy council (IC sink, answers report 7's 212k), enemy council (the field is the perfect bomber target), space council (the fixed relay sat).

## 5. Alliances Sought

- **Artillery, armour and missile councils.** Heat is their multiplier. We need their kinetic lists and they need our thermometer. This is the combined-arms bargain.
- **Economy and logistics council.** The power bill (ENERGY-2) needs their grids and upkeep rules. We trade a sink for their reactor art.
- **Space council.** Reentry sheath (ENERGY-4) and the Furnace relay both depend on fixed satellites having a job. They get the "space units matter" ask answered.
- **Enemy council.** Must give the Autofleet some thermal answer (radiators on capitals, heat-resistant ablatives), so heat stays a contest. We ask them to be cruel.
- **Defence council.** Magnetic Confinement is the shield combo they have wanted. We should write the node together.

*(End of paper. Smoke may be present.)*
