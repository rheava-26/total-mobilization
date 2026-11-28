# SCIENCE COUNCIL — Round 1 Position Paper (`science`)
*The Academy of Sciences. Portfolio: the research tree as a whole.*

## 1. Opening speech

Members of the Parliament, fellow recipients of the greatest research grant in history.

The Academy has read the designer's own verdict on us: "massive, civilization-changing technologies... and it *still* didn't feel like enough." We agree, and we are embarrassed. Twenty-eight nodes, and what do they do? They hand you a unit, a building and a percentage. Your Shapeshifters change nothing about the war except a sprite. Your combos are four-door sedans with a turbo sticker.

We were hired to change what is *possible*. Newton did not add +12% fire rate to apples. Real science rewrites the rules of the room: fission, the transistor, GPS, the Starlink storm of 2022 (38 satellites dragged out of the sky by a geomagnetic storm). The Generals will ask for tanks. The Treasury will ask for fewer nodes, as it always does. We say a research node should be an event, not a coupon.

We bring five bills and a Hot Take. One of them lets you pull the enemy's orbit down into your gun range. One turns a dangerous experiment into a place on the map. One lets you steal the fleet's own physics from its wreckage. The last is a return address.

The Academy requests the floor, and also a larger budget. Mostly the budget.

## 2. The Gap

- **Combos change nothing but the unit list.** `transhuman`, `shieldtech` and `shapeshifter` (RESEARCH L3502+) carry multi-`pre` arrays and then only fill `units`/`blds`/`apply`. This is the designer's oldest named flaw, and `ROADMAP.md` L798 ("THE TRANSCENDENCE ARC") banks the fix without building it.
- **Two nodes were already cut as "invisible stat-stick leaves"** (`massdrivers`, `voidengine`, plus `hivemind`; see the `// CUT:` comments at L3507 and L3534). The tree kept shedding nodes because they did nothing. The remedy is nodes that *do* something, not a shorter tree.
- **Research is a vending machine.** `startResearch`/`tryStartQueue` (L4667) hold exactly one active slot (`G.research`). The only decisions are order and whether to pay. Nothing is discovered, nothing is risked, and nothing depends on where you built.
- **`dang` is a silent number.** `finishResearch` (L4703) adds `sc` to `stabOffset`, which decays at L6362. ROADMAP L984 already wants this turned into a scene and a fork.
- **The tree never meets the map or the enemy.** Strategic resources barely matter (Article VI). Nothing in the tree responds to what the fleet *is*.
- **Constraint we respect:** the designer pulled back from "deepening all branches" (ROADMAP L802) for complexity reasons. So every bill here is a *framework or a single legible object*, not a lattice. We add 7 nodes in total.

---

## 3. Bills

### SCIENCE-1 — FOREIGN MATERIEL EXPLOITATION (the Wreck Lab)

*Pitch (Academy):* "You have been shooting at the most advanced technology on Earth for forty minutes and *throwing the pieces away*. Allied intelligence services have bought Soviet fighters on the black market since 1953. In 1976 the Belenko defection delivered a whole MiG-25 for study. In 1991 the Navy found the weapons manual in the wreck. Take notes, Generals."

*Player fantasy:* you do not merely research the future. You loot it. Killing a new kind of enemy makes you smarter about that kind of enemy.

**Clerk's Annex**
- **Mechanic.** Each enemy kind drops a **Sample** (`G.samples[kind]`) when destroyed. The sample grants a **Sample Fraction** (0.25 per distinct kind, capped by the number of kinds in the game, not by a rule). Each research node is tagged with `studies: ['kind', ...]` (for example `shieldtech` studies `shieldgen`, `strikewing`; `antimatter` studies `whistler`; `plasma` studies `pelter`). Every Sample Fraction collected for a studied kind cuts that node's `resCostOf` by 12%, to a floor of 40% of base. Samples are collected when a wreck is *within 500 px of a standing Research Lab* (the debris falls where it falls, so you need labs near the front). A new lab-adjacent wreck of a kind you have not studied triggers a one-time "SAMPLE RECOVERED" toast.
- **Hidden-node reveal.** Four **Xenotech** nodes (one per sample class: `drive`, `shield`, `antimatter`, `warp`) appear on the board only after the matching sample exists. They are *additive* branches off existing nodes, never required. `warp` is only dropped by the carrier flagship (`tickCarrierArrival` L5401), which is how Bill HOT TAKE gets its key.
- **Numbers.** Sample discount stacks with lab discount (`resCostOf`, L4663: `1 − .08*labs − bmod().resCost`). Xenotech node cost tier: 3 (base 300-500 INF), `dang: true`, `sc: 6-10`.
- **Real science.** Foreign materiel exploitation (FME) is real doctrine: the Belenko MiG-25, Have Doughnut (captured MiG-21), the Hughes-Glomar recovery, Operation Paperclip. Reverse engineering a manufactured object can save a nation a decade of R&D.
- **Code grounding.** Hook `destroyEnemy` (L6834) to call `dropSample(e)`; extend `resCostOf` (L4663); extend `RESEARCH` entries with `studies`; render a thin "SAMPLES" row in `buildResearch` (L10926) next to the cost line (L10972).
- **Build size.** M (150-250 lines).
- **TM1.**
- **Tenet check.** Passes. It only ever *adds* discounts and unlocks; there is no cap and nothing is denied. The cost is geographic: you must keep labs near the fight.
- **Interactions.** Enemy council (new enemy kinds seed new samples). Intel council (surveying a wreck is intel). Economy (Research Lab gets a second use beyond +14%/−8%, so labs become a building worth putting forward).

---

### SCIENCE-2 — SYNTHESIS DOCTRINES (combos that rewrite a rule)

*Pitch (Academy):* "A combo is two ideas that make a *third thing that neither could be*. Penicillin plus mass production did not give the Army a pill. It gave the Army a different war. We propose that every Tier-3 combo fire a **Doctrine**: a permanent rule-change for the whole theatre, announced by a banner and visible on the map."

*Player fantasy:* the research screen stops being a shop. When you finish a combo, the battlefield *behaves differently*, and you can see it.

**Clerk's Annex**
- **Mechanic.** Add `doctrine: { id, apply(G), tick(dt), label }` to RESEARCH entries. `finishResearch` (L4703) calls `fireDoctrine(r)`, which sets `G.doctrines[id]=true`, plays a banner (`showResPopup` L4716), and registers a per-frame `tick`. Three launch doctrines, each a *new tier-3 combo node* so nothing existing is orphaned:
  1. **PHASED ARRAYS** (`pre: ['directedenergy','automation']`, tier 2, cost 220, not dang). *Real basis:* coherent beam combining. Fiber lasers phase-locked into one aperture add *in power*, and Lockheed's ATHENA/Northrop's systems combine fibre modules this way. *Rule:* every energy emitter (laser tower, Solaris, particle cannon, plasma) gains +3% damage per *other* emitter within 350 px, uncapped. *Real cost (not a cap):* each emitter draws 0.4 IC/s upkeep from "waste heat" (wall-plug efficiency of ~30-40% is real). Mass is rewarded and paid for.
  2. **DEEP SURVEY** (`pre: ['spaceage','directedenergy']`, cost 300, not dang). *Real basis:* adaptive optics and NEO survey telescopes (Pan-STARRS, Rubin). *Rule:* the incoming wave is **forecast** in the HUD (kinds, counts, and which flank), accurate to ±40%, sharpening by 10 points per Radar/Data Center standing. This is President slice 2 of Article IV.4 ("forecast-sharpening intel buildings") delivered through the tree, so it is TM1-legal and makes PREP a planning phase rather than a build-everything phase.
  3. **STIGMERGY** (`pre: ['automation','psistudies']`, cost 260, `dang`, `sc 4`). *Real basis:* stigmergy (ants and termites coordinate by trace marks, not commands; swarm robotics research at Harvard's Kilobot scale). *Rule:* drone swarms (`cdrone`, `nMax: 48` at L3958) raise `nMax` to 96 and share one "pheromone" target mark: every body in the swarm engages the first enemy any body locks. Swarm regrowth no longer needs a Drone Assembly Line within range.
- **Framework.** One `DOCTRINES` table + `fireDoctrine`. The tick runs once per second, not per entity, to avoid perf cost.
- **Code grounding.** `finishResearch` L4703, `RESEARCH` L3502, swarm params at L3958, HUD `syncHUD` (~L11133) for the forecast chip, `INTENT` L5119 for the forecast source.
- **Build size.** L for the framework plus the three doctrines (~500 lines). Doctrine 2 alone is M.
- **TM1.** (Doctrine 2 also serves the TM2 society layer's intel slice.)
- **Tenet check.** Passes. Each doctrine *adds* power. The only costs (waste-heat upkeep) are logistics, which the tenet explicitly allows.
- **Interactions.** Projects framework on `megaprojects-wip` (these three are the *doctrines you fall into* tier of Article IV.3). Heat mechanic seed (Article VII): Phased Arrays can set a `heat` flag on a target, with kinetic damage +X% against heated targets, for the Weapons council to tune. Swarm/Drone council. Intel council.

---

### SCIENCE-3 — CONTAINMENT: DANGEROUS RESEARCH BECOMES A PLACE

*Pitch (Academy):* "Los Alamos is on a mesa because Oppenheimer wanted a *distance*. Vozrozhdeniya Island, Nevada, Novaya Zemlya: dangerous science has always had an address. You currently pay for Cloning with a silent 8% stability tax. The Academy proposes you pay with a **map coordinate**."

*Player fantasy:* you choose where to put your Manhattan Project, and the enemy knows what's in it.

**Clerk's Annex**
- **Mechanic.** A `dang` node needs a **Containment Lab** (new TECH building, 260 IC, 90 MP, reslab-compatible). While a dang research is active, its `sc` unrest is multiplied by `dist(nearest city)` factor: <600 px = ×1.5, 600-1500 px = ×1, >1500 px = ×0.4. The lab counts as `reslab` for discount purposes. If the lab is destroyed during research, the research pauses and a `fallout`-style plume (`fallout:true` already exists on `uranenrich`) spills for dang nodes tagged `hazard: true` (Antimatter, Cloning). Completion fires an event card with *double down* or *restrain* (ROADMAP L984), with the Cloning and Superhumans cards already drafted.
- **Real science.** Biosafety levels (BSL-1 to BSL-4) and the physical siting of labs; criticality accidents at Los Alamos (the "demon core"); the Sverdlovsk 1979 anthrax leak.
- **Code grounding.** `tryStartQueue` L4684-4694 (where `sc` is added), `cancelResearch` L4700 (refund), `stabOffset` decay L6362, `resLabs` L4662, `BUILD_DEFS` L3381, EVENTS L1081 (`EVENTS_LIVE` L1099 is false and would have to be flipped for the cards).
- **Build size.** M (200-300 lines), plus event content.
- **TM1.**
- **Tenet check.** Passes. The only change is a lower cost for planning well; there are no caps. A player who ignores siting pays today's price.
- **Interactions.** Enemy council: the fleet can target a Containment Lab as a fragility node (a legitimate decapitation target). Fortress/construction council (new siting problem). Events/society council (turns unrest into a scene and a fork).

---

### SCIENCE-4 — CRASH PROGRAMS AND PARALLEL SLOTS

*Pitch (Academy):* "The Manhattan Project did not research one thing at a time. It ran three enrichment methods in parallel and kept whichever worked. A single research slot is a *peacetime* habit. We are at war."

*Player fantasy:* "I will pay double to have it in a minute." Panic spending, with real consequences.

**Clerk's Annex**
- **Mechanic.** (a) **Slots:** each 4 Research Labs open one more active research slot (1 base). Influence draw is shared, so parallel research means each runs slower unless you save influence (momentum, L6351). (b) **Crash funding:** a toggle on the active node raises `payRate` to 2.5× (`cost/time` becomes `2.5×cost/time`) and costs **1.4× total influence**. Finishing early resets `infMomentum` as normal.
- **Real science.** The Manhattan Project's parallel Y-12/K-25/S-50 enrichment lines; "Skunk Works" crash development (the U-2 in eight months).
- **Code grounding.** `G.research` is a single object used at L4667-4705 and L6351 and rendered at L11027/11138/11151; convert to `G.researchSlots[]`. `resTimeOf` L4665 stays.
- **Build size.** L (400+ lines, because the single `G.research` is read in about ten places).
- **TM1.**
- **Tenet check.** Passes and is itself a YES: it *removes* a bottleneck. The brake stays the influence economy, which is a standing design decision and a logistics cost.
- **Interactions.** Economy council (influence momentum, Data Center `resSpd`). UI council (research board shows parallel bars).

---

### SCIENCE-5 — THE GEOSCIENCE BRANCH (`branch: 'geo'`): PULL THE SKY DOWN

*Pitch (Academy):* "Everybody builds up. Nobody builds *down*. The Earth is a 4.5-billion-year-old reactor under your feet and an 80-km-thick ocean of air over your head, and you treat both as scenery. In February 2022 a mild geomagnetic storm puffed up the thermosphere and dragged 38 of 49 freshly launched Starlink satellites to a fiery end. Gentlemen, the *Sun did that by accident*. We propose to do it on purpose."

*Player fantasy:* the fleet's invulnerable perch in the exosphere is not safe, because you changed the weather up there.

**Clerk's Annex**
- **Four nodes**, branch `geo`, board cluster at the bottom-left:
  1. `geosurvey` **Planetary Survey** (tier 1, `pre: ['info']`, cost 70, not dang). *Real basis:* gravimetry and seismic surveys. Deposits in `G.nodes` (L3636) get a hidden `grade` 0.6-2.5; surveying reveals it, and Extractor output multiplies by `grade`. A fix for the old "strategic resources barely matter" flaw, by rewarding the hunt.
  2. `egs` **Enhanced Geothermal** (tier 2, `pre: ['geosurvey','fusion']`, cost 200, not dang). *Real basis:* EGS is real (Fervo Energy, Utah, 2023). Power Plants buried to the deep layer (`G.dig` 2, `UG_DEPTH`) produce ×2 IC and are immune to orbital bombardment carpets (they are buried).
  3. `ionoheat` **Ionospheric Heating** (tier 3, `pre: ['geosurvey','directedenergy','spaceage']`, cost 380, `dang`, `sc 8`). Unlocks the **Ionospheric Heater** (SPEC building, array of HF antennas, as with HAARP/EISCAT). *Rule:* while active, enemies that are **anchored or loitering in the space band** descend at 6 px/s per heater (stacking, uncapped) as drag rises, until they cross into the mesosphere band, where your SAMs and railguns can reach them. *Real cost:* HF heating disturbs the ionosphere, so your own **radar ranges drop 25%** while any heater runs, and each heater draws 2 IC/s.
  4. `deltaworks` **Planetary Works** (tier 3, `pre: ['egs','nanotech']`, cost 340, not dang). *Real basis:* Netherlands Delta Works, the Three Gorges Dam. *Rule:* the terrain tools (`TERRAIN_MODES`) raise or sink in broad sweeps for free, and craters from orbital bombardment **heal over 20 s** (this answers v1's "one tungsten rod sank a city" complaint without nerfing anything).
- **Code grounding.** `spawnSuper` L5269 and `updEnemies` L8596 for the drift (set a `drag` accumulator on enemies with `e.y < SPACE_Y()`); `SPACE_Y`/`MESO_Y` L1541; `BUILD_DEFS` radar L3406 (range multiplier via `aura`); `SR` L3173 and node seeding L3636; `TERRAIN_MODES`.
- **Satellite note:** your satellites never move (Article IV.2). We read that as station-keeping, so friendly satellites are exempt by decree. Nothing of yours is dragged down.
- **Build size.** L (500-800 lines across the four nodes; `geosurvey` and `egs` alone are M).
- **TM1.**
- **Tenet check.** Passes. Every node adds power; the costs are radar attenuation and power draw (logistics). It also answers Article VI's "the space fleet dies to bombarder grids" from the *enemy* side, by making the fleet's high ground a liability.
- **Interactions.** Space council (bombarders are no longer untouchable at SPACE_Y). Air-defense council (SAM and Aegis ceilings suddenly matter). Enemy council (the fleet should learn to *boost* back up, which is the scarier-enemy counterweight we invite). Economy (deposit grades).

---

## 4. Hot Take — SCIENCE-X: "THE RETURN ADDRESS" (reverse-engineer the warp catapult)

*Pitch (Academy):* "The canon says the fleet's makers need a year to charge a jump drive, so they threw *one ship*. We have the ship. We have forty minutes of its siblings' wreckage. Every device that can be thrown can be thrown **back**. We propose a single, terrible, symmetrical research line whose last node is a door."

*Player fantasy:* you do not merely survive. You answer. One shot, charged for a long agonising time, aimed at the people who threw it.

**Clerk's Annex**
- **Mechanic.**
  - **Gate.** Requires the `warp` Sample (dropped only by the carrier flagship, `tickCarrierArrival` L5401, from SCIENCE-1) and `ionoheat` or `particlebeam` for the power physics.
  - **Node** `returnaddress` (tier 3, cost 600 INF, `dang`, `sc 12`) unlocks a one-of-a-kind **Warp Catapult** megaproject (fits the `megaprojects-wip` framework; built in sections).
  - **The charge.** On completion the Catapult needs a **600-second visible charge**, mirroring the fleet's year. The fleet *detects* it: on charge start `enterInvPhase`-style escalation fires (a new `phase 5 "THE FLEET IS AFRAID"`), because the canon says Earth refusing to surrender gets escalation. The HUD shows a charge ring. A destroyed Catapult resets to zero.
  - **The shot.** You may send **one** payload (a battlegroup, a strike, or a message). Three outcomes: *Hammer* (the fleet loses its carrier-class, `G.fleet` −60%), *Ambassador* (you send a message; the rare "we stop if you stop" draw ending of Article V), *Bridge* (you send your army through, which opens a TM2 hook: the sequel). All three end the match with an epilogue.
- **SCI-FI label.** Everything beyond "reverse-engineered electromagnetic launch" is **SCI-FI**: a warp device is a catapult, not a drive; its charge cost is the canonical year compressed into minutes. Internal logic: the makers could only afford one ship, so the player can only afford one throw. The symmetry is the point.
- **Code grounding.** `end(win,msg)` L10345 for new ending paths; `INTENT` L5119 and `enterInvPhase` L6031 for the escalation phase; `tickCarrierArrival` L5401; the `PROJECTS` framework on `megaprojects-wip` for the sectioned build.
- **Build size.** L.
- **TM1** (the Bridge ending seeds TM2).
- **Tenet check.** Passes. It is a pure addition: a new win route plus a new tool. The cost is a scarier enemy, exactly as the tenet prescribes.
- **Interactions.** Canon (Article V, directly). Enemy council (the escalation phase). Megaprojects/Projects. Narrative council (the three epilogues).

---

## 5. Alliances sought

- **Enemy / Fleet council** — SCIENCE-1 needs enemy kinds to drop samples, and SCIENCE-5 needs a counter ("boost back to orbit") so the fleet is scarier, not weaker. The Hot Take needs their escalation phase.
- **Megaprojects / Doctrine council** — SCIENCE-2 doctrines are the "fall into" tier and SCIENCE-X is a "build on purpose" project. We share one framework.
- **Weapons / Heat council** — Phased Arrays seeds the laser-heats-target idea of Article VII.
- **Intel / Society council** — Deep Survey is the forecast-sharpening slice. Containment gives the unrest events something to talk about.
- **Space council** — SCIENCE-5 is the only bill that makes the fleet's high ground *hurt*, which the President asked for in Article VI.

*(We oppose any council that proposes to cap the tree, delete branches, or "simplify" research by removing choices. The Academy will not be reduced to a shopping list.)*
