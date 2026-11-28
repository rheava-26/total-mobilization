# Bureau of Undersea Works: Maiden Paper (Round Four)

*Commissioner of Undersea Works, Deep Bloc. Sworn in at depth. Please speak slowly when you reply; the helium makes me hear you late.*

## 1. Opening speech

Honourable... members. Forgive... the pace. I have been under... nineteen days. Decompression takes... longer than your debates.

The Admiralty has... a fixation. On things that float. Seventy-one percent of this planet is water, and the last report fielded... three ships. Three. They have taken an ocean and built a bathtub.

Floating is... the *worst* thing to do in a sea. It is where the lasers are. Where the kinetic rods land. Where the Autofleet looks. Light does not go... very deep. Seawater drinks a laser beam within tens of metres. A rod that hits the surface... spends itself on a column of water. Down there, nobody is aiming at you. Down there, it is quiet.

The game has a floor under the ocean. It has drawn it. It colours it. It lets nothing live on it. My Bureau proposes to move in.

To the Admiralty: congratulations on your hulls. They look... lovely from below.

## 2. The Gap

- **Water has exactly one use in the code: ships float on it.** `needWater` appears on two buildings only: `shipyard` (L3391) and `heavyport` (L3464). Their `y` is `seaLevelY(x)`, the surface (`mkBuilding` L3670, `bldSurfaceY` L3671). Nothing in the game is placed on the seabed.
- **The sea floor exists and is ignored.** `groundY(x)` is computed under water and drawn (L1580 region, `drawWaterRegion` L1689-1694 fills between sea level and `groundY`). `elevAt` deepens seas by 2.6x (L1496), so there is real depth. No building can stand on it.
- **Water already blocks things, but only one way.** Non-amphibious ground units drown (`drownT`, L8226). Land buildings that flood are destroyed (L6568). Both are penalties. Nothing rewards being wet.
- **Placement is surface-only.** `canPlace` (L3866) sends water buildings to `findWater` (L3758) and ignores depth. `digLevel` (L3863) and `UG_DEPTH` (L3843) give land two buried levels; water gets zero.
- **Evidence from play.** Report 8 fielded 3 naval units. The docket (#143) banked Seabed Nodules, Seawater Uranium and OTEC as "a navy economy", but they are stranded in Naval's bills. A ship is the only reason they would exist. That is the wrong reason.
- **The waterline is a hole in the Line.** The Address says the real extra dimensions are vertical: underground, surface, sea, sea floor. The sea floor is listed. The game has none.

## 3. Bills

### UNDERSEA-1: THE SEA FLOOR (foundations)

**Pitch.** "Click on water. The building goes... down. That is all. Come and see how little you have to do."

**Player fantasy.** The same build menu, the same click. You point at the ocean and your industry vanishes into it, quiet and untargeted.

**Clerk's Annex.**
- **Mechanic.** New flag `seabed: true` on a building def (parallel to `autoBury`). It may be placed on any water tile where depth `groundY(x) - seaLevelY(x)` is at least `d.minDepth` (default 30 px). It is drawn and positioned at `groundY(x)`. **Submerged shelter:** damage from enemy `weapon` types `laser`, `beam` and orbital strikes (Annihilator beam, Zeus rods) is multiplied by `1 / (1 + depth / 40)`; at 120 px depth that is -75%. Enemy ground fire and bombers cannot target seabed buildings at all; only enemy naval and the new answer below can. Cost: seabed buildings cost +25% IC (pressure hulls).
- **Enemy answer (Amendment I).** New enemy **Bathyscaphe Drone** in the fleet roster: a sinking lander that settles on the seabed, 300 HP x `t`, walks the floor at 40 px/s and torpedoes seabed buildings. It spawns only when you own at least 5 seabed buildings. Sub-hunting turrets and the SOSUS grid (UNDERSEA-4) counter it; a seabed that is ignored costs the enemy nothing, so an underwater economy invites a response in proportion.
- **Code.** `canPlace` L3866 (new branch beside `needWater` L3870), `mkBuilding` L3670, `bldSurfaceY` L3671, draw of sites/placement L9327/L9392 (use `groundY`), `findWater` L3758, `waterBounds` L3760 for the body width, damage hook in `updEnemies` orbital block L8665. Shelter factor lives in the projectile damage path.
- **Size.** M (L if the seabed unit pathing for Bathyscaphes is included; I propose the drone ships in UNDERSEA-4's M).
- **TM1.**
- **Research.** New node `deepworks` (branch `def`, cost 140, prereqs `pointdefense`, not dangerous). Seabed buildings of each later bill carry their own req.
- **Tenet (Amendment I).** Adds a place to build with a shelter bonus. No cap, no nerf. The enemy gets a real answer, so the bonus is not a free solve; it matters most against the Annihilator and rods, which Report 7 already shows hurt.
- **One-Button.** The player only builds. The flag changes where the building sits, not what the player does. Every water body gives more room as `waterBounds` widens, so scaling is volume.
- **Line.** Depth is vertical, not sideways. The seabed is a second row beneath the surface row. Nothing needs a flank.
- **Interactions.** Naval (surface/seabed layer split), Planet (terrain SINK makes new seabed, see Moat), Subterranean Works (same layering rules), Enemy.

### UNDERSEA-2: THE HEAT ENGINE (OTEC and current turbines)

**Pitch.** "The ocean is a... battery. Warm on top. Cold below. Put a pipe between, and ask for... electricity. It never sets. It never needs fuel."

**Player fantasy.** You flood the coast with machines that print IC for free, and the longer you look the more of them there are.

**Clerk's Annex.**
- **Mechanic.** **OTEC Plant** (seabed, minDepth 80, IC 200, MP 70, `econ: {ic: 9}`). **Current Turbine** (seabed, minDepth 30, IC 90, `econ: {ic: 3}`). Real science: Ocean Thermal Energy Conversion runs a Rankine cycle on the roughly 20 K gap between warm surface and cold deep water. Tidal and current turbines are in service (MeyGen, Strangford Lough). **No cap and no diminishing returns.** Instead a real cost: each OTEC raises the Fear Index of the nearby sea slightly (the fleet reads big plants as industry), consistent with the Order Paper #9 explanation.
- **Code.** `BUILD_DEFS` L3381 (econ field handled with `econ:` in the income loop, L3810 area), `Fear` display hooks as needed.
- **Size.** S.
- **TM1.**
- **Research.** `deepworks`. Combo `fusion` unlocks OTEC grade 2 (+50% IC).
- **Tenet.** Pure addition. No nerf. The Fear Index cost is already the designed brake.
- **One-Button.** Pure build-and-forget income. Ten plants are ten times one.
- **Line.** Needs no front.
- **Interactions.** Industry/Market (new IC stream), Pacing (an economy for the player who dislikes ships), Planet (SINK makes more sea).

### UNDERSEA-3: THE DEEP HARVEST (nodules, uranium, brine)

**Pitch.** "Polymetallic... nodules. Lying on the floor. Manganese, nickel, cobalt. Nobody owns them. I would like to... pick them up."

**Player fantasy.** Strategic resources finally matter, and you get them without a node. The ocean is the node.

**Clerk's Annex.**
- **Mechanic.** **Nodule Dredger** (seabed, minDepth 60, IC 260, MP 80): grants `G.owned.rare` and `G.owned.steel` as a **bonus multiplier**, not a gate, as CLAUDE.md's intended fix says ("grant bonuses rather than act as requirements"). Output scales by dredger count and by the body's width from `waterBounds`. **Seawater Adsorber** (seabed, IC 220): uranium on polymer braids, about 3 ppb, a real Japanese/US experiment; grants Uranium (strike discount). **Brine Electrolyser** (seabed, IC 180): desalination plus electrolysis, gives +4 IC, real hydrogen economy. This bill takes ownership of the seabed placement of Naval's *Deep Deposit* motion (r2 Motion C); Naval keeps the surface ships that tend them.
- **Code.** `SR` L3173, `COMP` L3180, `srMult` L3810, `G.owned`, `resourcesCaptured` L3608, `extractor` L3387 (pattern for node claim).
- **Size.** M.
- **TM1.**
- **Research.** `deepworks` plus existing `automation` (dredger crews). Rare Earth satisfies `antimatter`/`voidengine` `srReq` without a land node.
- **Tenet.** Increases strategic resource use, which the designer asked for. No cap.
- **One-Button.** Output per building; no per-unit handling.
- **Line.** Vertical. A dredger beneath a frontline city is not exposed to its ground front.
- **Interactions.** Naval, Industry (cascades), Strategic (uranium), Planet (survey grade).

### UNDERSEA-4: THE LISTENING FLOOR (SOSUS and cable)

**Pitch.** "Two things... live on the sea floor... that nobody remembers. Ears... and the internet. Ninety-nine percent of intercontinental data runs along cables, on the bottom."

**Player fantasy.** You hear the invasion coming before the sky does, and the sea floor links your nation like a nerve.

**Clerk's Annex.**
- **Mechanic.** **Hydrophone Array** (seabed, IC 130): passive acoustic detection, reveals the Fear Index's *forecast* (decree 4, forecast-sharpening intel) and extends warning of landings by +6 s per array (stacking, no cap); also counters Bathyscaphe Drones in range. Real: SOSUS, the Cold War seabed listening network. **Cable Landing Station** (seabed, IC 150): +0.6 stability cap each, like Comms Tower but larger and sheltered. Stability regen is gated by the cap, so each station is a real upgrade. Research-gated as an Information Age building.
- **Code.** `INTENT` L5119 forecast text, stability cap function (Comms Tower, `gmod`), `spawnEnemy` L5581 for the Bathyscaphe spawn gate, `updEnemies`.
- **Size.** M.
- **TM1** for the arrays and stations; the forecast sharpening is one of the three TM1 society slices.
- **Research.** `deepworks`; combo `pointdefense` (arrays cue Aegis against inbound landings).
- **Tenet.** Adds information and stability. No cap.
- **One-Button.** Passive. The player builds; the bars rise.
- **Line.** Detection range is horizontal radius along the one line; nothing flanks.
- **Interactions.** Stability, Science, Autofleet (fleet psychology reads detection), Naval (sub warfare).

### UNDERSEA-5: PENS AND PRESSURE DOMES (and the civilisation they make)

**Pitch.** "Submarine pens. Habitats. A city... with no sky. What do you become... when you live under a kilometre of water? Quieter. Wealthier. Slower to... forgive."

**Player fantasy.** Ballistic subs launch from nowhere; whole districts live on the sea floor; the society that results is not the one you started with.

**Clerk's Annex.**
- **Mechanic.** **Submarine Pen** (seabed, IC 300): `strat: true` launcher, a dispersed arsenal that cannot be hit from the surface. Replaces the surface-only Ballistic Submarine dock requirement. **Pressure Dome** (seabed, IC 360, `garrison`, `bDef` tier): a city district on the floor, with its own self-defence. Real: Conshelf, Aquarius, and Navy saturation diving (the Commissioner's trade). Cost: each dome has an upkeep, `-3 IC/s`; if IC hits zero the dome floods and is lost, the same real cost as an air pump. Tenet-friendly: a real upkeep, not a cap. **Society.** A new society kind **abyssal** (`society(kind)`, L? repaint): sky replaced with sea-green dark, +12 stability cap, -8% manpower income. Systems, not scenes: the headline is "Abyssal Ministry reports full employment."
- **Code.** `BUILD_DEFS` strat flag (Silo, TEL), `armSilo`, `society()`, stability cap function, city `bDef`/districts (`damageDistrict`).
- **Size.** L (society state plus pens).
- **TM1** for pens and domes; the full abyssal society layer is **TM2**.
- **Research.** New `abyssal` node, prereqs `deepworks` + `biotech`, dangerous (sc6). Combo `biotech + deepworks`: Pressure Dome habitats house the Bio caucus. Combo `psionics`: the Hive hears through water.
- **Tenet.** Gives a new safe nation shape. No nerf. The enemy's Bathyscaphes and the Trench Landers (Naval) answer.
- **One-Button.** Pens and domes are built, then run themselves; upkeep is automatic.
- **Line.** Domes sit under the front, not beside it.
- **Interactions.** Missiles (strat launch), Stability, Lore (society), Bio, Megaprojects.

## 4. Hot Take: DROWN THE CAPITAL

**Pitch.** "Move... the capital. Into the sea. Let them glass the empty field."

**Clerk's Annex.**
- **Mechanic.** Megaproject **The Drowned Capital** (IC 6000, build in 5 sections of 1200 IC, each section sinks a district). When complete, the capital flag moves to the sea floor. The enemy's Decapitation Strike (`INTENT` L5119) targets the capital: it now targets the seabed, where orbital beams lose 75%+ of their punch. The old surface capital stays as decoy. Cost: the capital becomes dependent on your OTEC grid; if the grid is cut the capital's stability falls 2/s.
- **Code.** Capital flag on cities, `doSkyfall` L5206 targeting, `avoidCityCenter`, `INTENT` L5119.
- **Size.** L. **TM1** (the society reading is TM2).
- **Research.** `abyssal` + `megaprojects-wip` framework. **Tenet.** A power fantasy with a real dependence cost. **One-Button.** Sections are built, not steered. **Line.** Vertical only.
- **Order Paper #5 answer.** A sea-floor civilisation becomes quiet, rich, slow to anger and fragile to a single pump: its weak axis is dependence on its own grid.

## 5. Alliances sought

- **Naval** (Admiralty): surface plus floor is one theatre. You keep the hulls, I keep the floor.
- **Planet Council**: the Navigable Moat makes more sea, which makes more of my floor. Sunk land is seabed.
- **Subterranean Works**: same rules, same flag shape; let us share the builder UI so the player has one dig button.
- **Industry and Market**: OTEC and nodules are IC streams.
- **Missiles**: Pens are your dispersed launcher.
- **Stability and Lore**: cable stations and the abyssal society.
- **Deep Bloc caucus**, naturally.

Dignity requires... I say this slowly... that the Admiralty read this before its next report.
