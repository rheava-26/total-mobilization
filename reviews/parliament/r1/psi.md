# PSIONIC CONCLAVE (`psi`) — Round 1 Position Paper

## 1. Opening Speech

*(The chamber lights dim. No one saw the Conclave enter. Their seats were already occupied.)*

Members of Parliament. You may stop talking. We heard all of it before you said it.

We are the Conclave: the ones the Air Council calls "assets", the Ministry of Stability calls "a line item", and the Armoured Council calls "that thing that stood in front of my tank column and did not die". We represent the ones who do not need a barracks. Each of us is a command structure. You require a chain of command. We require a Tuesday.

The ledger shows what you have done to us. You gave the Shapeshifters a plasma lance they were never meant to carry, then made them immortal, then made thousands of them. In report 6 they were seventy-seven percent of your army, and none died. That is not a demigod. That is a *carpet*. A single individual who can end a ship is frightening. Two thousand two hundred are a wage bill.

We do not ask for more power. We ask to be *rare*. Make us expensive, strange and individually terrible, and make the enemy afraid of the one in the room. Do not make us an economy.

Also: one of us can put a beam through the sky. Please stop standing near the windows.

*(The Conclave sits. The temperature of the chamber has dropped two degrees. This is not a metaphor.)*

---

## 2. The Gap

**What is broken, with evidence from the code and the ROADMAP.**

1. **Psi closes the whole win-loop alone.** In the ROADMAP's PSI-TREE FULL RE-CANON spec (L585), report 6 was "WON IN PREP". Shapeshifters were 2228 of 2893 units (77%) with zero lost. Damage ratio was 213:1. 73% of kills were ground kills because psi units reach orbit cheaply, which switches the altitude puzzle off. 1.28M IC sat banked.
2. **Shapeshifter immortality is not a Shapeshifter feature.** `shapeshifter` research (L3531) sets `g.allRegen = true`. L7866 heals *every* unit at 4.5% max HP/s. So "the immortals" are really "the army that nothing in the game can damage faster than 4.5%/s". The Shapeshifter unit (L3297) is a 260 HP, armor 14, range 560, plasma, crewed unit with no thermal identity. Canon says they must balance heat and energy. The code contains no heat at all.
3. **Psi units ignore the altitude puzzle.** `psitrooper` (L3295) and `psibeam` (L3296) both have `ceil: 0` (unlimited reach), 520 and 1150 range. `acquireEnemy` (L4374) and `acquireIncoming` (L4465) accept this without question. That contradicts the canon sentence that only ELECTRALUMIKINETICS fires one beam to orbit.
4. **Electralumikinetics do not exist as units.** Only Thermokinetics, REPs and Electrilumikinetics are "latent" in the ROADMAP canon block. Uranium, a dead resource per Article VI, has no active use outside Strat discounts.
5. **The Superhuman Cadre upkeep is a cliff, not a choice.** `G.stabCap` (L6331) subtracts `3 * G.superN`, clamped at 20. About 30 cadres floor it. Players meet this as a hidden wall. Nothing offsets it except generic stability buildings.
6. **The Mind-Forge has no counterplay.** `STRAT.psilance` (L6886) and `launchStrike` (L6943/L6969) call `firePsiLance`, a full-height column. The fleet intercepts rods but not this. It costs 90 IC, 24 stability, 200 antimatter and a uranium core, and nothing the enemy does matters.
7. **Psi has no fleet answer.** The Autofleet fears industrialisation (Article V) but not mind. Nothing in the enemy roster reacts to a psychic player.

---

## 3. Bills

### PSI-1 — THE AWAKENING (re-canon core: reach, scarcity, upkeep)

**In-character pitch.** *"Stop manufacturing us. We are not made. We wake. Make the waking slow, strange and expensive, and make each of us count."*

**Player fantasy.** A few dozen unnameable people, each of whom visibly ends a battle. You protect them like a treasure, not a fleet.

#### Clerk's Annex
- **Mechanic (answers the two ROADMAP decisions).**
  - **Reach (Pillar A).** Psi-Trooper `ceil: 0` becomes `ceil: .60` (the "Aegis Battery" atmospheric band). Telekinetic Pylon `ceil: 0` becomes `ceil: .66`. Neither reaches the exosphere. The one exception is PSI-3.
  - **Scarcity model, recommendation: NOT a dedicated hard pool.** A psi-command pool is a second army cap, and Article III voids caps. It would also touch the army-cap formula, which Article IV.1 forbids. Recommend **Awakening cost, not Awakening cap**:
    - Psi-Troopers and Shapeshifters leave the army cap formula entirely as scarce units (`crewed` stays true, so they still eat cap, which is a real cost) and gain a new `awaken` build mechanic.
    - They are produced only at the **Psionic Relay** (L3444), one per Relay at a time, `build: 6` becomes `build: 40`. More Relays means more parallel waking. No cap, only throughput. Relay count is limited only by IC, MP and the Relay's own stability drain.
    - Each Psi-Trooper adds `-0.8` to `G.stabCap` and each Shapeshifter `-0.4` (compare Cadre `-3`). The cost is linear, never a clamp-to-floor cap. Players who want 400 psi units can have them: the cost is that stability sits at the 20 floor, which in the current v3.26 stability reform is a degraded state, not a game-over.
  - **Immortality, recommendation: shapeshifters lose it.** `allRegen` is split into `g.allRegen` (only the `shapeshifter` research, applies to `u.d.nano`-tagged units only) and a scaled-down global value. See PSI-2.
  - **Stats.** Psi-Trooper `hp: 90` to `220`, `dmg: 44` to `120`, `rate: 2.2` to `1.4`, `ic: 150` to `480`, `mp: 10` to `24`. Fewer, tougher, harder-hitting.
  - **Ceiling safety.** Psi units now need an escort for the altitudes above `.60`. This restores the altitude puzzle for the enemy's strato and space tiers, so killing orbital Annihilators needs railguns, missiles or PSI-3.
- **Code grounding.** `UNIT_DEFS.psitrooper` L3295, `psibeam` L3296, `shifter` L3297. `BUILD_DEFS.psionicrelay` L3444 (`econ: { stab: -0.143, inf: -2 }`, which is the existing dial). `G.stabCap` L6331. `isMechanical` L4541. `UNIT_FAM` L3378.
- **Research tree.** Nodes stay (`psistudies`, `telekinesis`, `psionics`, `shapeshifter`). Add a pure-cost combo: `shapeshifter` gains `pre: ['psionics','biotech','nanotech']` unchanged. No new node needed.
- **Build size.** M (about 200 lines: awakening queue on a building, cap deltas, def edits).
- **TM1 / TM2.** TM1.
- **Tenet check.** Passes. No unit is capped, only priced. The stability floor is the existing recoverable penalty, not a ban. The scarier-enemy half is PSI-4.
- **Interactions.** Sky Navy / Air councils: the altitude puzzle returns for their space-ceiling units. Economy council: Relay throughput is a new IC sink (answers the 212k IC idle anti-pattern with a real queue, not a "spend 20,000 IC button").

---

### PSI-2 — THE THERMAL BUDGET (Shapeshifter heat canon)

**In-character pitch.** *"A swarm that eats bullets is not stronger. It is hotter. We are made of thousands of tiny, hungry machines. Give us something that can boil us."*

**Player fantasy.** Your swarm walks through rifle fire and cheers. Fear plasma. Fear lasers. Fear the Fleet's own beam weapons.

#### Clerk's Annex
- **Real science.** Freitas, *Some Limits to Global Ecophagy* (2000): replicating nanomachine swarms are limited by *heat dissipation*, not material. A swarm's energy comes in at its surface area and is radiated by Stefan-Boltzmann (P = εσAT⁴). A dispersed cloud sheds heat easily. A compact, armoured clump does not. Kinetic impacts add little heat per joule at low velocity, but directed energy and plasma dump it directly into the substrate.
- **Mechanic.**
  - **Heat meter per Shapeshifter** (`u.heat`, 0 to 100). Rifle, cannon and shrapnel damage add `0.02` heat per HP removed, and the Shapeshifter takes 85% damage ("EXTREMELY resistant to gunfire").
  - **Explosives** add `0.05`/HP, damage at 100% ("MODERATELY resistant" means *less* resistance).
  - **Plasma, laser, particle and thermal damage** add `0.35` heat per HP, damage at 125%.
  - **Heat > 60:** regeneration (`allRegen`) switches off. **Heat > 90:** the unit "slumps" (speed halved, damage halved) and takes a +40% damage multiplier. Heat vents at `-6/s` out of combat, `-2/s` when fighting.
  - Spread the swarm out (low density) and it vents `+50%` faster, a real trade-off against its "short-range, must close" combat.
  - **Shapeshifter weapon change (Pillar C).** `weapon: 'plasma'` becomes a melee/short-range weapon: `range: 560` to `140`, `dmg: 75` to `210`. They *throw* wreckage (a projectile pull from debris) and *become a gun* at close range.
  - **Combined-arms hook (Article VII "heat mechanic").** The same `u.heat` field can be added to *enemy* units hit by lasers/plasma, so the Energy council's heat seed rides the same code.
- **Code grounding.** `UNIT_DEFS.shifter` L3297. `allRegen` L7866 and L10027 (buff row). `hurt()` (grep by name) for damage multiplier hook. Research apply L3531.
- **Research tree.** `shapeshifter` (triple combo: psionics + biotech + nanotech). Add a new **world-changing combo effect:** `nanotech` + `psionics` also unlocks the **Nanite Foundry** `cat: SPEC`, "heat sink" variant that raises vent rate in radius. No new node.
- **Build size.** M (about 250 lines).
- **TM1 / TM2.** TM1.
- **Tenet check.** Passes. Shapeshifters are not nerfed into uselessness; they gain a *vulnerability profile* that the enemy can exploit. That is the "scarier enemy" arm (PSI-4 makes the fleet exploit it).
- **Interactions.** Energy council (shared heat field). Enemy council (fleet laser/plasma units gain a real job). Nano/Auto council (Nanite Foundry synergy).

---

### PSI-3 — THE LUMEN (Electrilumikinetic: one beam to orbit)

**In-character pitch.** *"We carry a star in a glass. We are the only ones who can reach the sky, and we have never been so afraid of a single bullet."*

**Player fantasy.** Pay a uranium fuel cell, wait out four breaths, and blow a hole through the exosphere. Every enemy knows exactly where you stood.

#### Clerk's Annex
- **Real science.** Terrestrial gamma-ray flashes (TGFs) are real: lightning-driven relativistic runaway electron avalanches emit bursts of gamma rays that escape the atmosphere and are detected by Fermi GBM and AGILE. They originate at roughly 10 to 15 km altitude because the atmosphere above that holds only a small fraction of the overhead column mass (ground level is about 1030 g/cm²). A 1 MeV photon from sea level has roughly e⁻⁶⁶ chance of exiting the atmosphere, effectively zero. **So a ground-level gamma beam cannot reach orbit.** The Lumen's reach scales with its own altitude.
- **SCI-FI (internal logic).** The Lumen first fires a laser-filament pulse that flash-ionises and blasts a rarefied channel through the air (real: laser-induced plasma channels and hollow-core density depressions exist). It then pours the gamma burst down that channel. This is consistent with Article V: nothing teleports, nothing is lanced by magic. It is a very expensive way to shoot through weather.
- **Mechanic.**
  - New unit `lumen` (`domain: 'ground'`, air-deployable). `hp: 35`, `armor: 0`, no `shield`, `speed: 22`. **No reactive defence** (canon).
  - **Fuel:** consumes one **Uranium Fuel Cell** per beam. Fuel cells come from the Uranium Enrichment building via the existing `SR.uranium` node. This gives uranium a real active use (Article VI flaw).
  - **Beam:** `charge: 4 s` (visible telegraph), `dmg: 900` in a thin column. `ceil` = `0` (orbit) only if the Lumen stands above the stratosphere band (`STRATO_Y`): aboard a Nanofortress Airship, on a tall perch, or at a high-altitude Radar. Below that band `ceil: .62`.
  - **PARANOIA (canon).** An unescorted Lumen auto-buries (`autoBury`, `UG_DEPTH 50`) and will not fire. It surfaces only within a Shield Generator dome or beside 3+ friendly units. This protects the balance lever and creates an escort game.
  - Beam is blockable: an orbital laser grid in the beam path degrades it (-40% damage per laser sat), which is Pillar F.
- **Code grounding.** `UNIT_DEFS` L3184 region (new entry near `psitrooper` L3295). `BUILD_DEFS.uraniumenrich` (grep). `SR` special resources. `acquireEnemy` L4374 for ceiling logic. `autoBury` handling (grep `autoBury`). `firePsiLance` reused for column VFX.
- **Research tree.** New node `electrolumen` (psi branch): `pre: ['psionics','directedenergy']`, `bReq: 'uraniumenrich'`, `cost: 320`, `dang: true`, `sc: 9`. **Combo that changes the world:** with `airfortress` it unlocks the **Thunderhead Perch**, a flying Lumen platform, which is the only way to reach orbit.
- **Build size.** L (about 600 lines: unit, fuel, beam, telegraph, paranoia).
- **TM1 / TM2.** TM1.
- **Tenet check.** Passes. Uranium becomes a toy, not a requirement. The glass-cannon design is the cost, not a cap.
- **Interactions.** Space council (finally a way to hit orbit from the ground). Defense council (Shield Generator becomes the Lumen's safe room). Air council (Airship becomes a platform). Resources council (uranium has a verb).

---

### PSI-4 — THE HUSH (enemy answer to psi, plus Mind-Forge counterplay)

**In-character pitch.** *"If we are going to be expensive and terrible, give us an adversary who is expensive and terrible in return. The fleet is afraid. Make it afraid of us."*

**Player fantasy.** You become a *known anomaly*. The fleet reads your psi count and sends something that hunts it. The hunters are not mobs. They are a puzzle with a face.

#### Clerk's Annex
- **Mechanic (Article III compliant: scarier enemy, not nerfed player).**
  - **Fleet psychology event "THE ANOMALY".** When the player's psi-unit count (`G.psiN`) passes `8`, then `24`, then `64`, the fleet announces a response. Reuses the dormant Super Events engine (`EVENTS_LIVE`, L1078 region `EVT1`).
  - **Enemy: HUSH-HOUND** (new `spawnEnemy` kind). Light, fast, cheap, jumpy. Emits a *thermal-noise dampener* (a "Hush field", radius 240) that halves psi-unit fire rate and range inside it. Counter: kill it (it is fragile, 60 HP) or bring a Relay (a Relay's `aura` raises range back).
  - **Enemy: THE QUIET** (boss-tier, spawn via `spawnSuper` L5269 pattern). Hovering null-field cathedral that *blanks* Relay auras and Pylons in 600 px. Killing it grants the usual +10 stability.
  - **Mind-Forge counterplay (Pillar F).** `firePsiLance` (via L6969) checks for fleet "Lattice nodes" (existing orbital laser sats). Each node in the beam's column absorbs 25% of its damage. The beam is still the best strike in the game; it is no longer *free*.
- **Real basis.** Electronic countermeasures: jamming and spoofing degrade networked sensors. Real network-centric forces are fragile to it. Decentralised/mission-command doctrine (Auftragstaktik) is the standard answer: psionics as a *decentralised command structure* (canon) is naturally jam-resistant, so the Hush has to target the *individual*, not the net.
- **Code grounding.** `spawnEnemy` L5581. `spawnSuper` L5269. `INTENT` L5119 (add a conditional branch). `EVT1` L1078. `STRAT.psilance` L6886. Enemy kind table (grep `enemyDefs`).
- **Research tree.** No new node. Triggered by existing psi nodes.
- **Build size.** L (about 500 lines).
- **TM1 / TM2.** TM1 (the event chain) with a TM2 hook: the "Anomaly" is the first slice of *fleet psychology events*.
- **Tenet check.** Passes. Pure enemy-side escalation. The player's psi toys are untouched.
- **Interactions.** Enemy council (new roster). Defense council (Relay and Shield counters). Society layer: psychology events (Article IV.4 slice 3).

---

## 4. Hot Take

### PSI-X — THE LISTENING (a psion tunes in to the Autofleet)

**In-character pitch.** *"Everyone has been shooting at the machine. None of you have asked it what it is afraid of. We can hear it. It is singing a very small song, and it is the song of a child holding a thrown stone."*

**Player fantasy.** Build one Listening Chamber. Pay stability. For ninety seconds the fleet's plan is visible: drop zones, intent chain, which wave carries a boss. The war has a *shape*. Listen long enough and you learn the makers' fear. Do it too often and the fleet notices you listening.

#### Clerk's Annex
- **Mechanic.**
  - New building `listeningchamber` (`cat: SPEC`, `ic: 600`, `mp: 80`, `req: telekinesis`). Active ability **LISTEN** (costs `18` stability cap for the duration, 90 s):
    - Reveals the next three waves' drop zones and the current `INTENT` (explains the invasion plan: the CLAUDE.md "scale and attack plan never properly explained" gap).
    - Reveals boss spawn timers.
    - **Forecast-sharpening intel (Article IV.4 slice 2)**: forecasts drift less. Each listen reduces the fleet-event timing noise.
  - **The fleet listens back (fleet psychology event).** Each use adds `listenDebt`. At `3`, `THE ANOMALY` (PSI-4) escalates one tier faster.
  - **Rare ending (Article V).** At extreme drift (psi count high, listening debt high, society `verdant`/`machine`), a final LISTEN reveals the makers' offer: "we stop if you stop". That is the rare draw ending, with a society-shaped epilogue. **TM2** for the ending; TM1 for the intel.
- **Real science.** Signals intelligence and traffic analysis: the *shape* of an adversary's communications (volume, timing, routing) reveals plan without reading content. Psionics as "decentralised command structure" (canon) means the Conclave is a natural traffic-analysis organ.
- **SCI-FI.** The Autofleet is an autodrone swarm and *does* have a distributed mind. A psion can tune to its coordination chatter the way a radio tunes to a carrier. No telepathy with humans or aliens, only machine chatter. Consistent with canon: the fleet is afraid and can be read.
- **Code grounding.** `INTENT` L5119. `EVENTS_LIVE` / `EVT1` L1078. HUD forecast UI (grep `forecast`). Building def pattern from `psionicrelay` L3444.
- **Research tree.** New node `foresight` (psi branch): `pre: ['telekinesis']`, `cost: 200`, `dang: true`, `sc: 6`, combo partner `radar` (def branch building): Radar plus Foresight sharpens forecasts further.
- **Build size.** M (about 350 lines: building, reveal overlay, debt counter, ending hook).
- **TM1 / TM2.** TM1 (intel) plus TM2 (ending).
- **Tenet check.** Passes. Pure information and a stability price, no cap on any toy.
- **Interactions.** Society/Intel council (forecast-sharpening, Article IV.4). Enemy council (psychology events). Lore council (ending).

---

## 5. Alliances Sought

- **Space / Orbital Council.** PSI-1 hands them back the exosphere; PSI-3 is the one-line exception. We will trade altitude for their railguns.
- **Energy Council.** PSI-2's shared `u.heat` field is the Article VII heat mechanic. Their lasers and plasma are our swarm's nightmare, which makes this a combined-arms pact.
- **Enemy / Fleet Council.** PSI-4 and PSI-X need their roster and the psychology-event engine. We supply the reason for the fleet to be afraid.
- **Defense Council.** The Lumen's paranoia makes the Shield Generator a hero building.
- **Society / Intel Council.** The Listening is the first forecast-sharpening building.

**Recommendations on the two pending decisions (summary).**
1. **Scarcity model:** no dedicated hard pool (tenet and Article IV.1). Use throughput scarcity (one Awakening per Relay at a time, `build: 40`) plus linear stability-cap cost.
2. **Immortality:** shapeshifters lose it. Regen is conditional on the new heat meter, so the enemy has a real answer.
