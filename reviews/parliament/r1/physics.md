# Round 1 — The Bureau of Standards (Realistic Physics Council)

## 1. Opening speech

*The Chairman taps a slide rule on the lectern.*

Honourable members, generals, and the Council of Loud Things. The Bureau of Standards has audited your war. We are pleased to report that most of it obeys the laws of nature by accident.

We came to praise two things. First, your re-entering pods start slow and accelerate as the air thickens, which is correct. Second, your explosions go quiet above the thermosphere (`resolveStyleAt`, L6689), which is also correct. The Bureau wept.

Then we found the rest. The orbital pod that falls through the mesosphere at several kilometres a second is a **glowing fire emoji**. It is not a plasma sheath. It is not hot. It is not blind. Your Nuclear Strike has a radius of 230 whether it goes off in a cornfield or in the exosphere. A nuclear weapon detonated in vacuum produces no blast wave, as the Bureau will explain with diagrams, at length, to anyone who does not leave the room.

Physics is not here to say no. Physics is the reason a Mach-20 fireball is the best special effect you will ever own. Every real phenomenon we bring you is a **toy handed to the player**. It is never a cap and never a nerf. The atmosphere is the most spectacular weapon in your game, and you have not yet picked it up.

The Bureau moves that the war be made *true*. It will be much louder.

---

## 2. The Gap

**Evidence from the code and from the war:**

- **Re-entry is a cosmetic fall.** `updEnemies` (L8631) accelerates `e.vy` with a flat +30 (or +130 for pods) per second to a cap of 440 (or 760). The only "heating" is `Math.random() < .2 + e.vy/600` spawning a `fire` particle. Nothing is hot, and nothing is blind. A pod's speed does not depend on its mass or shape.
- **Altitude is binary.** `ceil` on each weapon says "can or cannot reach". `SPACE_Y`, `THERMO_Y` and the other bands (L1541) change which style of explosion draws. They never change what a weapon *does*.
- **Explosions ignore the atmosphere.** `areaDamage` (L7536) and `splashDamage` use one core-plus-linear falloff at any altitude. `detonateNuke` (L7565) has `R = 230` and `areaDamage(x, y, R, 720)` whatever the burst height. The `high` flag changes only the particles.
- **The heat seed is unplanted.** Article VII asks for lasers and plasma that heat a target so that kinetic weapons hit harder. Nothing in `title.html` carries a `heat` field. Lasers do plain `dmg`, and the combined-arms payoff is empty.
- **Space is empty, and it dies.** Report 7 lost 42 of 44 Halos, 37 of 37 Strike Fighters and 18 of 18 Volleys. A dead satellite currently leaves nothing behind. In real orbit a dead satellite is the most dangerous object in the neighbourhood.
- **The fleet's blackouts are one-way.** `G.blackout` and `empMul` (L5902) exist and are good, but only the enemy has an EMP. The player has no electromagnetic weapon at all.
- **The sky has no weather and no density.** Real satellites lose altitude when the thermosphere inflates. Geomagnetic storm drag killed 38 Starlink satellites in February 2022. This is a gift to a game about orbital war, and the game does not use it.

---

## 3. Bills

### PHYSICS-1 — "PEAK HEATING": the Re-entry Corridor and the Plasma Blackout

**Pitch (in character).** *"Members, a body falling into air is slowed by drag, and heated in proportion to the cube of its speed and the square root of the density. Heating therefore peaks at one altitude (Allen and Eggers, 1953) and falls off below it, because the body has slowed. Your pods already fall through that band. They do nothing there. We propose they do what physics says: they become blind, hot and predictable."*

**Player fantasy.** You watch the whole drop wave light up like a meteor shower. At the peak-heating band the fleet's pods go dark to their own command net, and you shoot them like ducks. Anyone with infrared seekers sees them from orbit.

**Clerk's Annex**
- **Mechanic.**
  - Add `e.heat` (0–1) to enemies with `reentry`, computed as `heat = clamp((e.vy/vmax)^3 * density(y)^.5 / peak)`. Use `density(y) = exp(-(worldH()*.82 - y)/H)`, with H tuned so the peak sits between `THERMO_Y` and `MESO_Y`.
  - **Blackout.** When `heat > .6`, the pod is inside a plasma sheath. It cannot receive retargeting, so its `vx` freezes. It also stops being a valid lock for enemy jammers and shield coverage, so it cannot be shielded, healed or buffed from outside. Its path is a deterministic line, so player interceptors and AA get a "lead prediction" bonus: +25% hit chance on `GRID_E` acquisition (hit-probability, not damage).
  - **IR bonus.** Heated pods are always visible to homing missiles regardless of `jammed()` (L4970). A hot target cannot hide, and the jammer excuse goes away for the 3–5 s of the corridor.
  - **Drag by mass.** Replace the flat accel with a ballistic coefficient `bc = mass / (Cd*A)`. Heavy pods (`bombpod`, antimatter bomb) fall faster and arrive hotter. Light drones decelerate early. Mixed waves therefore stratify by altitude, which gives the player layered kill zones.
  - **FX.** Draw a violet-white shock cone and a plasma streak, and add a brief radio-silence tick sound.
- **Code grounding.** `spawnEnemy` L5581–5585 (sets `reentry:true`, `vy`); `updEnemies` L8631–8650 (accel, cap, `fire` particles, `e.hold` arrival); `jammed` L4970; `acquireEnemy` for the prediction bonus; `empburst` carrier path (L8650–8653) should use the same heat so EMP pods glow most.
- **Research.** New node `reentrytrack` "Re-entry Tracking", branch `def`, tier 2, pre `['pointdefense','orbitalcmd']` (combo), cost ~200, not `dang`. It unlocks the corridor overlay and the prediction bonus. The base physics (blackout, heat) runs without research, since it is the fleet's physics.
- **Real basis.** Allen–Eggers ballistic entry, with peak deceleration at a fixed density, and ionised-sheath comms blackout (Apollo and Shuttle lost radio for about 3 minutes). Not sci-fi.
- **Size.** M. **TM1.**
- **Tenet check.** Passes. It only gives the player advantage, and the enemy's pods become scarier for the unprepared as well, because heavier pods now arrive faster.
- **Interactions.** Air Defence council (prediction, interceptor doctrine). Enemy/Fleet council (per-kind ballistic coefficient is a free way to make new drop pods feel different). Space council (corridor overlay).

---

### PHYSICS-2 — "THERMAL LOADING": the Heat Mechanic, Done Properly

**Pitch.** *"The President asks that lasers heat things so that kinetics hit harder. The Bureau agrees and demands rigour. Real steel loses roughly half its strength by 600 °C. A hot target is a soft target. So: lasers and plasma accumulate heat in an enemy, and a hot enemy takes more damage from every other weapon. Combined arms, by thermodynamics."*

**Player fantasy.** Your laser grid paints the hostile heavy, which glows orange. Then your railguns and rods slice through it like warm butter. You *want* a mixed army now, and you watch the heat bar climb.

**Clerk's Annex**
- **Mechanic.**
  - Add `e.heatT` (0–100). Beam and plasma weapons (`beam`, `laser`, `plasma`, `plasball`, `plasarc`, `pbeam`) add `dmg * k` to `heatT` in `hurt()` (L4971) in addition to their normal damage.
  - **Strength penalty.** Kinetic and explosive damage vs `heatT` multiplies by `1 + .5 * heatT/100`. Armor is reduced by up to 40% at full heat (hot metal yields).
  - **Cooling follows physics.** Cooling is convective in air and proportional to density, so ground and low-air targets shed heat in about 3 s. In vacuum there is only radiative loss ∝ T⁴, which is slow, so space targets hold heat for ~10+ s. High-altitude hostiles are therefore the best laser prey.
  - **Thermal shock.** If `heatT > 80` and a `water`/rain/cryo hit lands, the target takes a one-off bonus. This is an easter egg for naval units.
  - **Feedback.** Draw heated targets with a rising orange tint. A glowing silhouette needs no HUD.
  - **Enemy side (cost, not cap).** The fleet's own beam weapons heat YOUR units and buildings with the same rule, so a bunker under plasma fire can be broken by the follow-up shells. This is scarier enemy, not weaker player.
- **Code grounding.** `hurt` L4971; weapon dispatch near L4493–4503 (`case 'rail'... return 0` ceilings); `areaDamage` L7536 for the hot multiplier; `UNIT_DEFS` L3184 (`weapon:` field) to tag heat contributors; drawing via cached art stamp (v4.9.0), so use a tint overlay, not a new sprite.
- **Research.** New node `thermalload` "Thermal Loading", branch `energy`, tier 2, pre `['directedenergy']`, cost ~140. Combo with `plasma` and `particlebeam` raises the heat coefficient (`+35%`). It is not `dang`, because it is only a doctrine. The combo with `railgun` (kinetics) unlocks a "Pre-heat Doctrine" tooltip.
- **Real basis.** Temperature-dependent yield strength (steel: ~50% at 600 °C), convective vs radiative cooling (Stefan–Boltzmann), standard in thermal-protection engineering. Not sci-fi.
- **Size.** L (touches many weapon types; the multiplier hook is small, the per-weapon tagging is the bulk).
- **TM1.**
- **Tenet check.** Passes. It adds a multiplier for the player and makes mixed armies better, and it caps nothing.
- **Interactions.** Weapons/Laser council (this is their toy). Navy council (water quench). Air Defence (hot targets are easier for IR seekers, shared with PHYSICS-1).

---

### PHYSICS-3 — "BURST ALTITUDE": Where You Detonate Matters

**Pitch.** *"At sea level a nuclear weapon is a blast. At 40 km it is mostly a flash. At 400 km it is almost entirely an electromagnetic event. The Bureau has watched your Nuclear Strike go off at one radius no matter where you point it. The cursor already knows the altitude. Let it matter."*

**Player fantasy.** Two buttons in one. Low burst flattens the front. Mid-air burst scorches a whole squadron. High burst is a **Starfish Prime**: it dumps an EMP on the fleet, and their jammers, shields, drones and missiles go dead, and your own hardened sites do not.

**Clerk's Annex**
- **Mechanic.**
  - `detonateNuke(x, y)` computes `alt = groundY(x) - y` and splits the same 720 damage into three channels by `density(y)`:
    - **Blast (areaDamage):** damage scales with ambient density, so ground to stratosphere keeps the current feel. Above the mesosphere blast drops toward 0.
    - **Thermal (heat via PHYSICS-2 `heatT`):** peaks mid-altitude, with a larger radius where the air is thin. By Sachs scaling, radius at a given overpressure rises as (p0)^(-1/3), so a thin-air burst has a bigger fireball with a gentler wave.
    - **EMP (`G.blackout`):** above the thermosphere the burst pushes `G.blackout` for the enemy, not just for you. Enemy jammers, shield domes (`shieldgen`), `empburst` pods and drones in the zone are switched off for the duration.
  - Total effect budget is *increased*, not split. A high burst is the fleet's nightmare, not a nerf of anything.
  - **Cost.** High bursts cost extra stability (the world sees the sky go dark), plus an EMP backscatter that blacks out YOUR unhardened electronics too. Hardened `Radar` and `Bunker` are fine. That is the real cost, not a cap.
- **Code grounding.** `detonateNuke` L7565 (`R = 230`, `high` flag, `areaDamage` call); `STRAT` L6878 and `launchStrike` L6943 (strike aim snaps to altitude); `G.blackout` push in `detonateEmpBurst` L5864 and `empMul` L5902; `BOOM_STYLES` L5058 (`nuke`, `emp`, `vacuum`) for the matching visuals.
- **Research.** New node `hemp` "High-Altitude Burst", branch `energy`, tier 2, pre `['directedenergy']`, cost ~180, **`dang: true`, sc 6**. It is gated behind `bReq: 'uranenrich'` like the nuke itself. The combo with `shieldtech` (hardened electronics) makes your own blackout cost vanish.
- **Real basis.** Starfish Prime (1962 high-altitude test; Hawaii streetlights failed 1,400 km away), HEMP E1/E2/E3 pulses, Sachs scaling. Not sci-fi.
- **Size.** M. **TM1.**
- **Tenet check.** Passes. It adds a new weapon mode for the player and a real cost (blackout backscatter, stability).
- **Interactions.** Strategic Strikes / Strategy council. Society council (stability cost and headlines such as "THE SKY WENT DARK"). Fleet council (EMP-hardened enemy variants is their answer).

---

### PHYSICS-4 — "KESSLER SHIELD": Dead Satellites Fight Back

**Pitch.** *"The Bureau notes with professional delight that in real orbit the most dangerous thing is a **corpse**. A destroyed satellite becomes a cloud of fragments moving at 7 km/s relative to everything crossing its path. Your dead Halos currently leave nothing. We propose they leave an *ambush*."*

**Player fantasy.** The space fleet "does not die in vain". When the fleet's bombarders carve up your grid, the wreckage forms a debris belt, and the next drop wave has to fly through it. You build space units knowing that a loss is also a landmine. Space *matters*, even in death.

**Clerk's Annex**
- **Mechanic.**
  - When a satellite-class unit (`sat: true`) dies, push a `G.debris` record `{x, y, r: 180, t: 40, n}` into the band at its altitude. Debris persists, with `t` decaying only slowly at the thermosphere and fast near the stratosphere (drag cleanup).
  - Anything with `reentry` or `y < THERMO_Y()` passing through takes continuous hull damage `n * invT()` and has a 15% chance per tick to lose a weapon or shield layer. Hostile pods get the corridor treatment of PHYSICS-1.
  - **Symmetric cost.** The fleet's bombarders and pickets create their own debris, which hits your satellites. Space is a shared hazard. The cost is real attrition and a need for **reboost / sweeper** units.
  - **Sweepers (cost).** A new cheap support unit, `Debris Sweeper`, clears debris in a radius, which is the logistics cost for keeping your band clean.
  - A huge cloud (more than ~12 pieces overlapping) triggers a **cascade**: debris spawns more debris. This is the real Kessler syndrome, and it is rare, spectacular and mutual.
- **Code grounding.** Satellite death handling in the unit-reap loop (grep `u.sat`); `G.blackout` is the model for a zone list on `G`; `updEnemies` L8631 reentry loop for the damage check; `SPACE_STATION_Y` L1551 and `STATION_Y` for band placement; `UNIT_DEFS` L3217–3219 for the `sat:true` tag; `hurt` L4971.
- **Research.** New node `orbitalflak` "Orbital Debris Doctrine", branch `space`, tier 2, pre `['orbitalcmd','pointdefense']` (combo), cost ~260, not `dang`. It unlocks the Sweeper and makes your own satellites hardened vs debris.
- **Real basis.** Kessler syndrome, the 2007 Chinese ASAT test (3,000+ pieces), the 2009 Iridium–Cosmos collision, and debris velocities in orbit. Not sci-fi. Satellites never move (standing decision 2), and nothing here moves them. Only the debris and the damage are new.
- **Size.** M. **TM1.**
- **Tenet check.** Passes. The player loses nothing they own to a cap, and gains from the death of their own units. The cost is attrition, which is a real cost and not a cap.
- **Interactions.** Space/Orbital council (this is their problem exactly). Enemy/Fleet (their bombarders become hazard sources). Air Defence (shares the corridor).

---

## 4. HOT TAKE — PHYSICS-5 "THE BREATHING SKY": Ionospheric Inflation (Megaproject)

**Pitch.** *"On 3 February 2022 a modest geomagnetic storm warmed the thermosphere and thickened the air at 200 km. Thirty-eight freshly launched Starlink satellites lost altitude and burned up. The Bureau proposes the player do this to the fleet on purpose. A heater array pumps the ionosphere, the upper atmosphere swells, and everything in the exosphere and thermosphere, **including every pod in its re-entry fall**, begins to feel real drag a hundred kilometres early."*

**Player fantasy.** You build a wall of towers across the map. Their power lights the northern sky green (aurora). The fleet's orbital approach goes from a fall to a **braking** descent. Pods heat early (PHYSICS-1), slow and burn, and the sky above your land becomes a kill zone for every fleet unit stationed in it. Your own satellites bleed altitude and need reboosting. That is the price, and you pay it gladly.

**Clerk's Annex**
- **Mechanic.**
  - A **megaproject** (two-tier, per standing decision 3: the player builds it on purpose, in sections) `Ionospheric Heater Array`. Each section adds `G.skyInflate` (0–1).
  - `density(y)` from PHYSICS-1 is multiplied by `1 + 3 * skyInflate` in the thermosphere and exosphere bands. Hostile pods and anchored space units (`bombarder`, `picket`, orbital annihilator) take drag damage per second proportional to density times speed squared. Anchored orbital units must burn fuel (`hold` drift) or sink toward `THERMO_Y`.
  - **Cost, never a cap.** Your own `sat:true` units lose `maxHp` slowly while `skyInflate > 0` (a decay tax) and need a `Reboost` pulse (cheap, IC plus rocket part). Power upkeep: IC/s scales with inflation. You can turn the array off.
  - **Aurora FX.** A green/violet curtain in the upper sky bands. It doubles as a legible readout of the inflation level.
  - Satellites never move: decay is HP and upkeep only.
- **Code grounding.** `SPACE_Y`/`THERMO_Y` L1541 for the bands; `updEnemies` anchored-orbital branch (grep `e.anchored`, `e.orbital`) and `reentry` L8631; `spawnSuper` L5269 for bombarder/annihilator anchoring; the `megaprojects-wip` branch framework; `BUILD_DEFS` L3381 (new `SPEC` building); sky gradient in `society()` for the aurora tint.
- **Research.** New node `ionoheat` "Ionospheric Heating", branch `space`, tier 3, pre `['spaceage','directedenergy','orbitalflak']`, cost ~480, **`dang: true`, sc 10**. The unrest is physical: the market sees the sky change. It is a combo that **changes the world** (the sky is different), which the Parliament wants.
- **Real basis.** Thermospheric heating and drag (Starlink, Feb 2022); real ionospheric heaters (HAARP, EISCAT) warm the F-layer by tens of percent in a small patch. **SCI-FI** is the scale: heating a patch the width of a continent. Internal logic (Article V): Earth's industry scales up, and the fleet fears exactly this kind of whole-planet response.
- **Size.** L (new megaproject plus sky-density field). **TM1.**
- **Tenet check.** Passes. It is an optional toy of staggering power, and its costs are upkeep and decay, not caps.
- **Interactions.** Megaprojects/Doctrine council (it is the poster project). Space council (satellite reboost). Society council (aurora headlines). Fleet council (how does the fleet adapt? It could hide in the lower, dense corridor or dive faster).

---

## 5. Alliances sought

- **Air Defence / Interceptor council.** PHYSICS-1 and 2 give them predictable, hot, IR-visible targets and a heat-for-damage combo.
- **Space / Orbital council.** PHYSICS-4 and 5 are their rescue plan, because space units must matter. We will happily share the debris ledger.
- **Strategic Strikes and Society councils.** PHYSICS-3 gives them a strike with a *choice*. Their stability cost and headlines make the dark sky a story.
- **Lasers / Energy council.** PHYSICS-2 is their toy. We will not bring it to the floor without them.
- **Fleet / Enemy council.** The enemy is the real difficulty lever, so every physics rule above hands them a reply: hardened electronics, heavier pods, ablative shields. We want those proposals.
- **Enemies of the Bureau.** The Council of Loud Things, if it demands a missile that turns in vacuum without a motor. We will bring a slide rule.
