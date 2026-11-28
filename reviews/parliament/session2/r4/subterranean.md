# Directorate of Subterranean Works: Maiden Paper (Second Session, Deep Bloc)

## 1. Opening Speech

*The Director enters, blinking at the lamps, which are too bright. He asks for them to be lowered.*

Members. I have not seen the sun in eleven years, and I recommend the experience. The surface is a temporary inconvenience. It is where weather happens, and the Autofleet, I am told, is about to glass all of it. I am delighted. Every rod they drop on a field is a rod not dropped on my foundry.

Look at what the House has built for the underground. It has a button. The button says DIG: OFF, SHALLOW, DEEP, and you click it like a man changing a TV channel. Rail Hubs go down because they must. The Mantle Forge and the Airwing go down because the code says so. Everything else gets a surcharge and an apology. The enemy's worst weapon is orbital, and the one place orbit cannot reach is under fifty pixels of dirt. Yet the Navy asks for ships, and the Air Staff asks for more of the sky.

The President says the underground is the book's great untapped theme. I agree, since I have lived in it. I do not ask for a tunnel. I ask for a *place*: a third floor to this world, where your factories, your silos, your people and your power go to outlast the war.

Bring a lamp. Not a bright one.

## 2. The Gap

Evidence, from the code (`title.html`):

- **Digging is a chore.** `G.dig` is a three-way toggle on the build bar (L10956-57). Every building the player wants buried needs the mode set first. This is the opposite of the One-Button Law.
- **Burial is a surcharge, not a place.** `placeBuilding` (L3888-90) charges 1.5x IC, plus 0.6x per extra depth, and 1.3x MP. Only `rail` and `autoBury` buildings are exempt. Only two depths exist (`digLevel`, L3863). Nothing in the game *needs* a player to go down.
- **The enemy already answers it, which is good.** Bunker-busters crack shallow structures for 1,400 and deep ones for 520 (L7511). `shallowUgHit` (L7543) lets deep ones shrug off 82% of blasts. Tunnel assault (L6602-03) bleeds buried buildings whenever ground enemies stand within 48 px. Depth is a real contest. It just has nothing to protect.
- **The buried layer is thin.** `ugAllowed` (L3862) lets industry, housing and silos sink. The only buried *weapons* are three AA batteries via `ugFire` (L8413, L8449). A buried silo or hangar cannot act on its own. Buried housing grants nothing special. Nothing makes power underground.
- **Cities are the weak flank.** `cityStabPenalty` (L4547) charges 2.5 per structure lost, permanently. ROADMAP banks the *Continental Redoubt* (L532, L752-54: "underground cities, glassing-proof") and says it is parked.
- **Reports 6-10** show a House that wins in the air. Nothing underground matters in any report, because nothing underground does anything.

## 3. Bills

### SUB-1: THE DEEP WORKS (strata, autobore and bombardment-proof factories)

*Pitch.* Stop clicking DIG. Build a Deep Foundry and it simply goes down, where it belongs. I add a third stratum, the Abyssal, below the Deep, and a single standing choice: **BUILD DOWN: AUTO**. Industry, housing, silos and hangars choose their own depth. The surface is for things that shoot at the sky and things you can afford to lose.

*Player fantasy.* The base goes dark on the surface and the factories hum below. Eight hundred pixels of rod-cratered glass sit above a working economy.

**Clerk's Annex**
- **Mechanic.** Replace the `G.dig` cycle (OFF/SHALLOW/DEEP) with a standing setting: SURFACE / AUTO (default) / ABYSSAL. AUTO sends every `ugAllowed` building down one level once the first bunker-buster is confirmed (the "screening fire" phase), or from turn one if the player picks it. Add `ugLvl 3`, the Abyssal, at 150 px depth. The Abyssal takes 3% of blast damage in `shallowUgHit` (vs 18% for Deep), and tunnel assault is x0.4 of the Deep rate.
- **Cost.** The burial surcharge falls to 1.15x IC for any building. The IC cost of depth is real but small, and the real cost is *time*: Abyssal builds take 1.5x `build`. Burial must never block mass-building.
- **Numbers.** New `deepfoundry` (cat PROD, native lvl 2): IC 300, MP 90, build 14. It works like a Factory, plus +20% output to every other buried PROD building within 400 px, and it cannot be hit by orbital rods (L7394 gutted anchored buildings only when `!b.ug`).
- **Code grounding.** `digLevel` L3863, `ugAllowed` L3862, `canPlace` L3866-72, `placeBuilding` L3888-90, `shallowUgHit` L7543, `tunnel assault` L6602, build-bar toggle L10956.
- **Size:** M. **TM1.**
- **Research.** Attach to `fusion` for AUTO and the Deep Foundry, and a new `deepmining` node (cost 200, not dangerous, prerequisite `automation`) for Abyssal. Combo: `deepmining` + `nanotech` makes Abyssal builds self-repairing.
- **Tenet check (Am. I).** A buff, so the enemy answers: bunker-busters gain a **Deep Penetrator** variant (x2.5 damage to Abyssal) after minute 12 of the invasion. Nothing is nerfed, and the player's surface army is untouched.
- **One-Button check.** Passes. One standing setting, then the player only builds. It scales with volume, since 40 buried foundries are 40x the effect.
- **Line check.** Passes. The new stratum is vertical, so the Line is not touched.
- **Interactions.** Planet owns terrain: no change to `groundY`, only a depth offset; Planet should confirm that SINK terrain does not strand buried buildings. Industry's Cascade tiers apply.

### SUB-2: THE MANTLE TAP (geothermal power)

*Pitch.* Power plants are surface things that scream. I put a pipe down until the rock is warm and boil water with it. Real engineers do this in Iceland. Their IDDP-1 well hit *magma* at two kilometres by accident. I would do it on purpose.

*Player fantasy.* A power plant that never needs defending, because nobody can find it.

**Clerk's Annex**
- **Science.** Enhanced geothermal systems (EGS) pump water through hot dry rock. The gradient is about 25-30 C per km. Iceland's IDDP-1 (2009) reached supercritical magma-heated fluid. So: real.
- **Mechanic.** `geotap`: IC 260, MP 70, `autoBury: 2`. Yields +9 IC (+12 at Abyssal). **Heat load:** every Tap raises a local `heat` value. Buried buildings in 300 px past 3 Taps lose 2% output each, so the Deep Foundry's cooling aura (ground bonus, SUB-1) cancels it. This is a real cost, not a cap: more Taps still means more IC.
- **SCI-FI: The Magma Tap.** The Abyssal Tap taps a magma chamber. It yields +30 IC and releases a **Fissure** if destroyed (a one-time 250-damage burst to every neighbour in 120 px). Internal logic: the makers' fear of a *planet that can cut itself open* fits Article V.
- **Code grounding.** `BUILD_DEFS` L3381 (`powerplant` L3383, `mantleforge` L3460 as template), `econ` income loop, `autoBury` path L3863.
- **Size:** S-M. **TM1.**
- **Research.** New node `geothermal` (cost 120, prerequisite `info`), no `dang`. Magma Tap needs `deepmining` plus the Rare Earth `srReq` (so strategic resources finally matter).
- **Tenet check.** A buff with an enemy answer: seismic sounding. Earthbreaker presses (L5488) weight up toward Tap fields as the fleet reads your power grid.
- **One-Button.** Passes. Build Taps, and they auto-bury and auto-cool. **Line.** Passes. **Interactions.** Industry (power and Cascade), Planet (Deep Deposits `geosurvey` grade), Lore (the fear of the Fissure).

### SUB-3: THE REDOUBT (bunker cities that keep stability)

*Pitch.* A city is a district on the surface and a set of people under a thin roof. I would move the people. A Deep Habitat holds ten thousand souls and one very good air filter.

*Player fantasy.* Watch the surface cities burn on the screen while the number that matters, stability, does not move.

**Clerk's Annex**
- **Mechanic.** `deephab` (a buried Housing Block): +3 MP, +4 cap (same as `housing`), plus **Shelter**: for each Deep Habitat standing, the permanent stability cost of a lost city structure (`lostStructures`, L4547) is reduced by 12% (to a floor of 40%). It needs **Deep Lamps**: below a stratum-share of 50% of your housing, the stability cap falls 5 (the sun deficit). A Lamp is a cheap buried building (IC 80).
- **Society.** **Chthonic** joins `society(kind)`: a dark, lit, warm sky repaint. Answer to Order Paper #5: a civilization that moved below becomes **a nocturnal republic**. Its prices are in air and water. The fleet's headlines read "NO SURFACE SIGNAL".
- **Megaproject.** The **Continental Redoubt** (built in sections along the Trunk, no section limit): +1 shelter tier per section; at 8 sections the capital counts as buried for `decapitation` strikes. It is the doctrine you fall into by building deep, and the capstone you build on purpose.
- **Code grounding.** `cityStabPenalty` L4547, `society()` L3549, `bDef` city defence, `lostStructures`, `housing` def L(grep `housing` L3384).
- **Size:** L. **TM1** (the shelter and Lamps), with the full society as **TM2**.
- **Research.** `deepmining` plus `biotech` (combo: *Chthonic Biosphere*, which turns Lamps into food, +MP).
- **Tenet check.** The shelter reduces a punishment and does not cap anything. Enemy answer: **Burrowers** (tunnel-assault drones) and the Deep Penetrator from SUB-1.
- **One-Button.** Passes (build Habitats; shelter is automatic). **Line.** Passes. **Interactions.** Stability (owns the cap, must confirm), Society Bloc, Lore, Autofleet (what it reads).

### SUB-4: THE SORTIE SHAFTS (deep silos and underground hangars)

*Pitch.* My silos fire out of the earth like a man opening a drawer, and my fighters launch from a hill that has no airfield on it.

**Clerk's Annex**
- **Mechanic.** Extend `ugFire` (currently AA only) to **strat silos** and **airwings**: a buried silo launches strikes from the surface muzzle, with the strike's IC cost -15% (the muzzle is hardened), and a buried Airwing launches and recovers fighters through a camouflaged shaft. Aircraft sortie rate +25%, `air command +3` as now.
- **Real basis.** Hardened and Deeply Buried Targets (HDBTs) and silos (Minuteman) are real; "hangar-in-a-mountain" bases (Sweden's Bergbasen) are real.
- **Code grounding.** `ugFire` L3405-09, `updBuildings` L8413 (`if (b.ug && !b.d.ugFire) continue`), `silo` L3407, `airwing` L3461, STRAT L6878.
- **Size:** M. **TM1.**
- **Research.** Attach to `pointdefense` (`ugFire` node) and `spaceage` (silo upgrade).
- **Tenet check.** A buff; the fleet answers by prioritising **muzzle-hunters**: strikewings that target visible shafts. **One-Button.** Passes: silos launch themselves under the existing strike button. **Line.** Passes. **Interactions.** Missiles, Air, The Trunk (shaft access to the rail).

## 4. Hot Take: SUB-X, THE UNDER-LINE

The underground becomes a **second front**. Burrowers dig into the tunnel network, and a **Deep Garrison** (auto-spawned militia in Rail Hubs and Habitats) fights them on a 1-D *tunnel line* under your cities.

**Clerk's Annex**
- **Mechanic.** The tunnel net of the Trunk is a line; enemy Burrowers (HP 80, spawn from Earthbreaker presses) enter at breach points; your Garrison (free, one unit per Habitat) auto-fights. Losing a tunnel segment cuts rail for 20 s.
- **Code grounding.** `railhub` L3416, `onRail` L7884, tunnel assault L6602.
- **Size:** XL. **TM2** if made a full map, **TM1** as an event layer (L). **Research:** `hivemind` plus `deepmining`.
- **Tenet.** Enemy-side addition, and it hurts no existing toys. **One-Button.** Automatic. **Line.** It *is* a Line, one level down. **Interactions.** The Trunk's sponsors (Industry, Planet, Command, Railgun) own the rail; the Under-Line only asks that segments be addressable.

## 5. Alliances Sought

- **Deep Bloc:** Planet (terrain, Deep Deposits, `groundY` offsets) and Naval and Undersea Works (shared deep-sea and bedrock stratum).
- **The Trunk's sponsors (Industry, Planet, Command, Railgun):** my buildings plug into their rail and I ask them for addressable segments. Their rail, my stations.
- **Megaprojects:** the Redoubt as a section-built megaproject.
- **Society Bloc (Stability, Lore, Bio):** the Chthonic society and shelter rule.
- **Ground Bloc, Fortifications Corps:** Deep Garrisons.
- **Rivals:** the **Air Staff** may keep the sky. When it burns, my customers will be underground.
