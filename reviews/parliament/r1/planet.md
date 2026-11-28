# PLANET & TERRAIN COUNCIL — Round 1 Position Paper (`planet`)

## 1. Opening Speech

*The Minister of the Interior and Geography taps a core sample against the lectern.*

Honourable members. You have spent this war arguing about what flies over the ground. I speak for the ground.

Look at your own battle reports. Five wins, and in every one the planet was a painted backdrop. The Air Council fights over the stratosphere and the Space Council over the exosphere, while the Navy sulks on a coastline we never gave it a reason to defend. Beneath all of it sits a lovely seeded heightfield, seven cities, eight ore nodes, a terrain brush that costs 35 IC, and a rail tunnel nobody talks about. We have one planet and we have been treating it as a floor.

The designer said "cover the entire ground in factories and turn the world into a fortress". I say the world should fight back. Raise it, drown it, hollow it and pave it. When the fleet glasses a valley it should leave a lake. When we win, the map should be unrecognisable from the one we started on.

We are a civilisation under a sky full of knives, and the only place the knives cannot reach is below. Give us the ground.

*(To the Air Council, aside:)* Yes, I noticed your 240 Falcons. They need somewhere to land.

---

## 2. The Gap

Evidence from `title.html` and the reports:

- **The map is the same every game.** `newGame` (L3580) hardcodes `nCity = 7, capIdx = 3` (L3626) and a fixed `types` list of 8 nodes (L3633), spaced evenly with ±160 px jitter. Only the noise seed changes. `MIN_LAND_FRAC = .35` (L1467) actively flattens variety. There are no theatres, small towns or distinct flanks. The ROADMAP's "small towns for map variety" (L1007) was never built.
- **The terrain brush is a sandbox toy with a cap.** `doTerrain` (L3911) is one 105 px brush at a flat 35 IC. `capLevels` (L1505) silently evicts the oldest edit after 220. That is a hard cap on a player toy, which is a tenet smell. A player who pours a mountain range into the map has it quietly un-poured.
- **The sea is passive.** Water is just `elevAt < SEA_E`. Buildings below sea level drown (L6561). Non-amphibious ground units take 60% max-HP per second in water (L8217) and halt at the shoreline (L8200). The player never uses this against the enemy. The fleet's `earthbreaker` and orbital barrage sink land, but we have no counter and no weapon of our own.
- **Resource nodes are a checkbox.** `SR` (L3173) gives flat discounts, and the 8 nodes are identical in richness. Report 7 shows Steel, Oil, Rare Earth and Uranium barely mattered.
- **Underground is a hiding place, not a world.** `UG_DEPTH = 50` and two layers (L3843, L3863). The Rail Hub (L3416, teleport logic around L7812 and L9313) moves idle troops. It carries no freight and no strategic meaning.
- **Ruins are capped at 60** (`pushRuin`, L1515). After a big war the "destruction of the world" fantasy silently forgets itself.
- **Mobilization leaves no landscape.** Spamming 400 buildings changes nothing about the ground. The ROADMAP's own "planetary conduct signal" (L526, L656) is unbuilt.

---

## 3. Bills

### PLANET-1 — THE SPINE: A FREIGHT RAILWAY UNDER THE CONTINENT

**Pitch (in character).** Honourable members, you have a rail hub the way a man has one boot. Build the Spine. A true underground trunk, vactrain freight under the whole 6,000 px, so the capital's factories feed the far flank and the far flank's ore feeds the capital. The fleet can glass the surface, but the surface is not where the nation lives.

**Player fantasy.** You lay a continent-spanning steel artery, watch glowing freight capsules run end to end, and the fleet's orbital bombardment finds an empty crust.

**Science.** Evacuated-tube maglev and vactrains are real engineering (the Swiss Metro concept, Hyperloop studies). Tunnelling below the weather avoids drag and bombing. Freight logistics scales with trunk density, as in the real Soviet and Chinese rail-first mobilizations.

**Clerk's Annex**
- **Mechanic.** Rail Hubs within 900 px auto-link into a Spine segment, drawn as a glowing line under the ground. Each linked segment gives:
  - (a) Linked buildings share component stock: `G.comp.alloy` and `G.comp.rocketpart` produced anywhere count toward the cap for units assembled anywhere on the Spine.
  - (b) An Extractor on a node touching the Spine yields +25% IC.
  - (c) Troop teleport (existing) gets 2x speed per extra link.
  - (d) A new building, Spine Depot (180 IC, 40 MP, rail, auto-buried at depth 2), lets any ground unit class be produced from a buried queue and surfaced at any hub.
  - Real cost, not a cap: each segment costs 1.5 IC/s upkeep per 1,000 px of chain, via the existing upkeep system. Hubs are 20x12 px, so cost per hub is 220 IC.
  - Strategic value: Spine hubs are immune to orbital barrage damage, since they sit at depth ≥ 1 and the barrage hits surface structures. Only Earthbreaker-class seismic hits (below) can cut a segment, which forces the fleet to spend a scarce unit on it.
- **Code grounding.** `BUILD_DEFS.railhub` L3416. `railHubs` array L7812. `hubs.sort` L9313. `digLevel` L3863. Component spend and cap code near `COMP` L3181. `nodeNear` L3876 for Extractor bonus. Orbital damage targeting would need an `!b.ug` check at the barrage damage site (grep `ug` with the barrage code, which already skips buried buildings elsewhere at L6561).
- **Build size.** M. Linking is a graph over an existing sorted hub list. Share-stock and the bonus are a few lines in the econ tick.
- **TM1.**
- **Tenet check.** Passes. This is a new toy with a logistics cost, with no cap and no nerf.
- **Interactions.** Logistics Council (supply, conveyors). Economy (IC upkeep and surplus outflow, Art. IV(4)). Ground Council (surfacing troops). Underground-adjacent Enemy Council (seismic cuts).
- **Research tree.** New node `deeprail`, branch `auto`, tier 2, cost ~220 after the 1.6x multiplier, prerequisite `automation` and `spaceage`. Not dangerous. Combo partner `nanotech`: the combo "Self-Boring Mole" auto-extends the Spine by 600 px per 60 s. SCI-FI only for the self-boring part; Elon-style TBM automation is plausible.

---

### PLANET-2 — DELTA WORKS: SEAWALLS, POLDERS AND THE FLEET'S TIDAL STRIKE

**Pitch.** The Dutch have held back the North Sea since 1953 with concrete and spite. We will do the same. A seawall and a pump station let you win land below the waterline, and a fleet that sinks your coast discovers you made a harbour.

**Player fantasy.** You reclaim a bay into a factory plain behind a levee, and the enemy's seismic press fails to drown it.

**Science.** Polders and pumps (Netherlands Delta Works, Zuiderzee), sea barriers (Thames Barrier, Maeslantkering) are real. Reclaimed land sits below sea level and survives only while the pumps run. That is the cost. Not a cap.

**Clerk's Annex**
- **Mechanic.** Two buildings:
  - Seawall (60 IC, wide, 160 HP, walkable). Any building within 140 px behind a standing Seawall is exempt from the flood kill-check at L6561.
  - Pump Station (140 IC, 3 IC/s power draw). Raises the flood threshold from `SEA_E - .04` to `SEA_E - .20` within 220 px.
  
  If the pump is unpowered or the wall dies, the zone floods instantly and buildings die. That is the real risk, and it makes the enemy's seismic and orbital strikes on walls a genuine strategic target. The terrain brush also gains a fourth mode, RECLAIM: a SINK-then-wall pour that costs 90 IC but can hold land at e = .35 (below the sea). `capLevels` L1505 is removed for edits inside a polder (see tenet).
  
  New enemy option for the fleet: Tidal Strike, a rare Earthbreaker variant that sinks a 300 px swath (`pushLevel` negative). The player's answer is the wall.
- **Code grounding.** `doTerrain` L3911, `pushLevel` L1504, `capLevels` L1505, flood check L6561, `isWater` L1534, `SEA_E` L1465. New building defs near `BUILD_DEFS` L3381. The earthbreaker spawn is near `spawnEnemy` L5581.
- **Build size.** M (two buildings, one terrain mode, one flood-rule branch, one enemy ability).
- **TM1.**
- **Tenet check.** Passes and fixes a violation: it removes the silent 220-edit cap on a player toy for polders. The pump upkeep is a real cost, not a cap.
- **Interactions.** Naval Council (new harbours, Shipyard sites). Enemy Council (Tidal Strike, wall sieges). Economy (power draw).
- **Research tree.** New node `geoeng` (Geoengineering), branch `auto`, tier 1-2, cost ~140, prerequisite `automation`. Not dangerous. Combos: `nanotech` yields Nanite Grout, where walls self-repair at 4 HP/s (extends the existing `g.regen`). `directedenergy` yields Laser-Sintered Glass Seawall, +100% HP.

---

### PLANET-3 — DEEP SURVEY: ORE VEINS, DEPTH AND THE GRADE OF A NATION

**Pitch.** Your eight ore nodes are identical because nobody ever surveyed them. A real geologist reads gravity, magnetism and seismic echo and finds that every deposit has a grade and a depth. Rich ore is deep. Poor ore is on the surface. Dig and you get paid.

**Player fantasy.** Your second dig layer stops being a hiding place and becomes a mine. A Deep-Ore Excavator descends 100 px below a node and the node roars to life.

**Science.** Geophysical survey (gravimetry, magnetometry, seismic reflection) is how real ore bodies are found. Grade increases with depth in many porphyry and uranium deposits. Geothermal gradient (about 25 to 30 C per km) is real, so deep rock is hot, which allows geothermal power.

**Clerk's Annex**
- **Mechanic.** Each node gets a grade `g` from 1 to 3 (random at `newGame`, with one jackpot node per game), shown as a ring colour. Extractor yield is scaled by `g`. An Extractor placed with dig level 2 doubles a node's IC and doubles its `SR` bonus magnitude (steel/oil discounts go from 18% to 36%, rare from 15% to 30%, and so on). Strategic resources then become bonuses, not requirements, as the designer wanted. A Seismic Survey Station building (90 IC, radar-like) reveals grades map-wide during prep. Without it, grades are shown only after capture. New building Geothermal Tap (200 IC, deep only): +12 IC, free power. The geothermal output scales with depth (+50% at UG level 2).
- **Code grounding.** `SR` L3173. Node spawn L3633-3636. `nodeNear` L3876. `digLevel` L3863 and `ugAllowed` L3862. Node draw L9290 and minimap L9776. `G.stats.resourcesCaptured` exists for the after-action.
- **Build size.** M.
- **TM1.**
- **Tenet check.** Passes. This adds bonuses and buildings. The "jackpot node" creates a contested flank, a reason for the fleet to land there. Nothing is denied.
- **Interactions.** Economy (surplus IC outflow). Enemy Council (jackpot nodes as landing targets). Logistics (Spine bonus from PLANET-1).
- **Research tree.** Existing node `directedenergy` unlocks Seismic Survey Station (lidar and radar tie-in). New node `deepcore` (Deep-Core Power), branch `energy`, tier 3, cost ~380, prerequisites `fusion` and `deeprail`. Dangerous (`dang`, sc 8: tapping the mantle risks quakes, see Hot Take). Combo with `nanotech`: Nanite Drill Bits double the dig speed.

---

### PLANET-4 — THEATRES: A DIFFERENT PLANET EVERY WAR

**Pitch.** Seven cities in a line at even spacing is a conga line. Give me archipelagos, rift valleys, river deltas and highland plateaus. Give each a different answer to the same fleet.

**Player fantasy.** Each match opens with a new country on the strategic map and a new puzzle: where do the enemies land, where do I dig, where do I build my navy?

**Science.** Real geography: continental rifts (East Africa), deltas (Mekong), archipelagos (Indonesia), altiplanos (Tibet). Each changes logistics and defence for real nations.

**Clerk's Annex**
- **Mechanic.** Roll one of 6 theatre templates at `newGame`, replacing the fixed `nCity = 7`:
  - ARCHIPELAGO: 5 islands, MIN_LAND_FRAC lowered to .25, naval and air-lift heavy.
  - RIFT VALLEY: a central trench with cliffs, so tunnelling is shallow and cheap.
  - DELTA: 60% flat low land, polders matter (PLANET-2).
  - HIGHLAND: tall plateau, ground units get +high-ground bonus.
  - ISTHMUS: two landmasses joined by a narrow land bridge, a chokepoint and a bridge target.
  - CLASSIC: today's map.
  
  City count varies 4 to 9. Add small towns (30% of a city's size, 1 district, a cheap stability bonus, a landing target for the fleet). Flanks in `doSkyfall` already target neglected sides; theatres give them identity. The title screen shows the next theatre's name and a one-line briefing. The player may re-roll once for free.
- **Code grounding.** `newGame` L3580, city loop L3626-3629, `flatLandSpread` L3652, `nodeSpot` L3662, `buildCity` L3674, `mergeCities`, `seedTerrain` L1476 and `MIN_LAND_FRAC` L1467, `avoidCityCenter`/`doSkyfall` L5205-5206.
- **Build size.** L (templates plus the small-town type plus tuning). A first S slice: add ARCHIPELAGO and RIFT only.
- **TM1.**
- **Tenet check.** Passes. Variety is not a nerf. The player keeps all toys, with new reasons to use naval and underground.
- **Interactions.** Naval Council (archipelago gives ships a job). Air Council (airlift across water). Enemy Council (landing logic per theatre). Economy (node counts).
- **Research tree.** None needed, since this is a world generator. Optional: research `geoeng` (PLANET-2) lets the player re-sculpt a theatre toward another template.

---

### PLANET-5 — MOBILIZED GROUND: ZONING, SALVAGE AND THE SCARRED EARTH

**Pitch.** The designer's phrase is "cover the entire ground in factories". Right now covering the ground does nothing. Industrial belts should be more than the sum of their parts, and the dead should pay for the living.

**Player fantasy.** You build a contiguous industrial belt 2,000 px long and the ground itself turns to a paved fortress, with a visible overlay and a real bonus. Every ruin in the war becomes scrap you salvage.

**Science.** Agglomeration economies are real (Marshall, Krugman): clustered industry is more productive. Urban salvage and brownfield remediation are real. The "urban heat island" and industrial haze are real.

**Clerk's Annex**
- **Mechanic.**
  - (a) Zoning: 5 or more buildings of the same `cat` within 300 px form a Zone. Each Zone gives +4% output per building in it, to a +60% max for that Zone, no cap on the number of Zones. The ground under a Zone is drawn paved.
  - (b) Salvage: ruins from `pushRuin` stay forever (the 60 limit is dropped for ruins that are not on screen; store as a compact array) and a Salvage Yard (80 IC) near a ruin collects 5 IC per ruin per minute and removes the ruin.
  - (c) Pollution: each Zone adds "haze" to a `G.conduct` meter, a TM1 slice that tints the sky and reduces stability cap by 0.1 per Zone, but also hides your structures from the enemy's targeting radius by 10% each (smog as cover). That cost and benefit are real, and the Zone count has no hard limit.
- **Code grounding.** `pushRuin` L1515 and `G.ruins`. `charAdd` L1510. `recalcBuildings` for `G.bmod`. Stability cap formula (CLAUDE.md, Stability section). Ground drawing in `drawWorld`.
- **Build size.** M.
- **TM1** (the haze meter is the seed for a TM2 planetary-conduct signal, ROADMAP L526 and L656).
- **Tenet check.** Passes. Rewards sprawl, which is exactly the fantasy. Haze is a cost.
- **Interactions.** Economy (stability and surplus outflow). Fleet Psychology Council (haze as an input to fleet events). Enemy Council (reduced visibility).
- **Research tree.** Existing nodes only: Zones need `automation`, Salvage Yard needs `nanotech` (salvage flag already exists at `g.salvage`).

---

## 4. HOT TAKE

### PLANET-X — THE GREAT CANAL: MAKE THE CONTINENT AN ISLAND

**Pitch.** Honourable members, stop defending the land. Delete it. The fleet's ground wave is non-amphibious: the code drowns it at 60% max-HP per second. The terrain brush already sinks land. So dig a canal, flood a valley, cut the isthmus. Turn three flanks into moats and your enemy's landings into swimming lessons.

**Science.** Moats are as old as Jericho. The Suez and Panama canals show a few thousand workers can cut a continent. Flood defences have been used offensively (the Dutch Water Line, 1672; the 1938 Yellow River dike break). SCI-FI note: none needed for the mechanism. The only liberty is speed, which nanites and autoteam bulldozers justify.

**Player fantasy.** In prep you sculpt a moat around your industrial heartland, and on landing day a thousand Autodrone tanks march into the sea.

**Clerk's Annex**
- **Mechanic.** Add a fifth terrain mode, CANAL: a line brush that sinks a linear 40 px wide channel (cost 90 IC per 105 px, no cap). Any `gtank`, `gmech`, `harvester` and other non-amphibious ground enemy entering water takes the existing drown damage (L8217) and, new, the fleet re-routes: `doSkyfall` and flank landings (L5206) prefer shores outside the moat, and Bulwarks and Earthbreakers become the fleet's answer (this is the "scarier enemy" the tenet demands). The enemy gets new unit Amphibious Lander (fleet, pod-delivered, a wave-1 reply at General and above), so the moat is never a permanent win. The player's navy finally has a purpose: moats and harbours demand ships.
- **Code grounding.** `doTerrain` L3911, `pushLevel` L1504, `capLevels` L1505 (must be exempted), drown logic L8200 and L8217, `isWater` L1534, `doSkyfall` L5206, `avoidCityCenter` L5205, `findLand` use at L4836 and L7974.
- **Build size.** L.
- **TM1.**
- **Tenet check.** Passes. It gives the player more terrain power, uncapped, and answers it with a scarier enemy.
- **Interactions.** Naval Council (ships). Enemy Council (Lander). Air Council (the one threat that crosses water). Planet-2 (walls keep canals from flooding your base).
- **Research tree.** Prerequisite `geoeng` (PLANET-2). Not dangerous. Combo `automation`: Autonomous Dredger Fleet, ×3 canal speed.

---

## 5. Alliances Sought

- **Naval Council.** Moats, polders and archipelagos give ships a reason to exist. Report 8 fielded 3 naval units.
- **Enemy / Fleet Council.** Every defensive toy I propose needs a scarier answer (Tidal Strike, Lander, seismic cuts). We trade bills.
- **Economy and Logistics Councils.** The Spine, Zones and Deep Survey all feed the surplus-IC outflow problem (212k IC unspent in report 7) with sinks that are interesting.
- **Air Council.** Their Falcons need airfields on reclaimed land, and the fleet's air arm is the one thing a moat cannot stop.

*We hold the ground. Hold the line.*
