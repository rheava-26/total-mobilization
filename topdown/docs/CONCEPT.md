# TOTAL MOBILIZATION — Top-Down Concept & Roadmap
*Synthesized from the designer's vision brain-dump (2026-07-19). Intentionally at concept level — specifics get locked per-phase.*

---

## The Thesis

**You are the coordinating intelligence of a society at war — not its hands.**

Every system serves one feeling: *a unified national effort is happening, and you are directing it, not performing it.* Militias raise themselves when invasion looms. Infantry garrisons defensible ground on its own. Supply flows along the roads you built without you routing each truck. The conventional army dispatches reinforcements to threatened regions. Civilians move underground when you sound the alarm. You decide **what the nation prepares for and where its weight goes** — the nation does the rest.

This also fixes the arcadey feel at the root: the old game felt like a toy because *you* were the only actor. When the world acts, it feels like a war.

---

## The Target Experience (canonical playthrough)

This narrative is the north star — every phase of the roadmap exists to make a beat of it real:

> Start the game on a big island. Build a few factories; connect your cities with roads so industry actually flows. The threat is announced — an alien invasion, mostly air-based — so you research **micromissiles** and start converting civilian industry to military production. Build carriers (island nation), harvest the region's three oil deposits with offshore rigs (unlocking heavier ground vehicles), and put up an **ultralight-metal works** — which is what actually completes the microrocket program and lets you mass-produce them. Eighty bajillion fighter jets later, the invasion is inbound: militias muster themselves, the army pushes a mixed reinforcement battalion into the key regions, units dig into natural terrain, the fleet goes to high alert, spare stockpiles get shipped to the threatened coast, and the population moves underground while the military waits. The landing comes down through your SAM umbrella — 40% intercepted on descent. The survivors land on the road network outside the city; your micromissile fighters fly constant close air support and shred the unescorted drones. The **Battle of Syrograd** begins as their ground forces enter the city — militias, garrisons, and regulars break them decisively. The orbital bombardment ship overhead is continuously screened off by your air cover until someone nukes it. You won. Roll the after-action report: casualty counts, the named battles, interception rates, your favorite units.

Named regions and cities everywhere. The war produces a **story**, and the game tells it back to you.

---

## Design Pillars

### 1. The Nation Acts (autonomy — the signature)
Carried over and expanded from v1's best instinct. Self-organizing behaviors, not orders:
- **Militias** raise themselves in threatened population centers (a posture you enable, not units you click out).
- **Garrisons**: infantry/militia automatically occupy defensible positions — cities, forts, chokepoints, rough terrain.
- **Reinforcement**: the standing army dispatches battalions toward threatened regions on its own doctrine.
- **Supply** self-routes along the infrastructure network.
- **Civil defense**: evacuation/underground sheltering as a national directive, not per-person micro.
- **Construction is delegated too.** You order *what* gets built (and optionally roughly where — "a fighter plant in the east"); the nation's planners pick the actual site, accounting for road access, power, safety from the coast, and land use. Kills v1's "intensely maximize every pixel of space" chore at the root — even building is the nation acting on your intent. (Pressure valve: you can pin an exact spot for the rare thing you genuinely want *right there* — a fort on a specific pass.)
- Player verbs are **directives and priorities** (postures, alert levels, "hold this region," "ship stockpiles east"), plus **build orders** and **strategic strikes** — the things a war cabinet actually decides.

### 2. Mobilization IS the Economy (fixes the wonky start)
No more starting with a huge IC faucet and going crazy in minute one. You start with a **civilian economy**:
- Base IC is small and comes from cities + their connectivity.
- **Convert AND build — both, always.** New military industry is constructed alongside the old economy, not instead of it. Conversion splits by weight class:
  - **Automatic (light industry):** as the mobilization level rises, generic civilian industry re-tools *itself* into light war production — workshops become small-arms and ammunition plants without the player touching anything. The nation converts; you watch your cities' output shift character.
  - **Directed (heavy/specialized):** tank works, missile plants, shipyards — deliberate projects, built new or converted by explicit order. This is where the player's industrial strategy actually lives.
- **Manpower is population**, drawn from cities at mobilization levels (volunteers → conscription → total mobilization), with consequences at each step.
- The opening minutes are groundwork — roads, connections, conversion — and the curve *ramps*. Keep v1's influence-momentum and research-drawdown lessons; they fit this naturally.

### 3. The Map Is a Machine (infrastructure, terrain, world-shaping)
- **Scale: a theater, not an islet.** Roughly a 100×100 km region — you're defending a country-sized piece of land, not Nauru. Baseline density: **~5 cities/urban areas** (v1 accidentally landed on the right number). Big enough for real geography — coasts, interiors, road networks between cities — small enough that every place can have a name you remember.
- **Plots & territorial control.** The map divides into named **plots/districts**. Holding plots yields rewards (resources, manpower, IC, strategic positions); taking more is how you grow — and **difficulty scales with how much you own**, because everything you claim, you must defend. Expansion vs. consolidation becomes a real strategic tension, plots give the enemy something concrete to seize, and control shifts are what frontlines are *made of*.
- **Buildable networks**: roads, railroads, tunnels (tunnels = the underground layer's edges). Connected cities produce more; units move faster and stay supplied on the network; rail moves battalions and stockpiles in bulk.
- **Terrain matters**: forest conceals, mountains channel and defend, rivers block, urban grinds. Terrain types carry effect lists (data-driven, same attribute philosophy as units).
- **Fortification**: entrenchment over time, buildable forts/lines — ground forces become *impactful* because position matters.
- **Fog of war**: you see your territory, your sensor ranges, your patrols; beyond that, intelligence *estimates* (which is also the autonomy fantasy — your intel service reports, it doesn't scry).
- **You reshape the world visibly**: what you build is on the map, changes the map, and the enemy interacts with it (they landed *on your road network* — that was a mistake they paid for).

### 4. Two Grains of Units (the recommendation on the units-vs-battalions conflict)
**Hybrid.** Ground forces are **battalions**; air, naval, and special units are **individual physical craft**.
- **Ground = battalions**: a marker representing a formation with composition, strength, entrenchment, and supply state. Solves the scale weirdness (a small stretch of world can't honestly hold 800 individual riflemen), gives frontlines/garrisons a natural grain, and is the unit the autonomy layer naturally dispatches. Battalions still *render* as a cluster of tiny figures/vehicles — you keep seeing mass, not NATO rectangles.
- **Air/naval = individual units**: the 80-bajillion-jets spectacle, physical missiles, carrier sorties, fuel — the charm we refused to lose, kept fully. Swarms of fighters exist and *function*, because the infrastructure/supply game supports basing them.
- Unit *behavior* stays *data-driven attribute lists* (v1's layer system, generalized): domain, movement class, terrain affinities, weapon behaviors, AI dispositions — all composable flags, so new unit types are data, not code.

### 5. Research = Programs + Industry (fixes "tech level is weird here")
Not civilizational eras — **R&D programs** scaled to one region's war:
- A research node has a **lab phase** (influence/time, from Pillar 2's economy) and an **industrialization phase** — completed by *building or converting the specific industry* (the ultralight-metal works is what finishes the microrocket program, and then it IS the factory that mass-produces them).
- Prototypes exist after the lab phase; *mass production* is what industry unlocks. Research and economy become one system instead of two parallel menus.
- Doctrine choices live here too — and different doctrines genuinely perform differently against different threats.

### 6. Supply & Resources as Gameplay (not extractor-spam)
- **Named regional deposits**, few and meaningful, requiring specialized structures (offshore oil rigs, deep mines) — extraction is a project, not a click.
- Resources **gate unit classes** (3× oil → heavy ground vehicles), not just costs.
- Output **flows over the network** into stockpiles; theaters draw from stockpiles; you can ship reserves toward a threatened front *ahead of time* (a real preparation decision).
- **Out-of-supply forces degrade.** Cutting a line — yours or theirs — is a war-winning act.

### 7. Wars Are Stories (names, battles, after-action)
- Generated/authored maps carry **named regions and cities**.
- Sustained combat in a place becomes a **named battle** ("Battle of Syrograd") tracked with casualties, duration, and outcome.
- An **event timeline** logs the war's key moments (interception percentages, battles, firsts, losses).
- **After-action report / replay** at game end: casualty counts, the map of the war, biggest battles, your most effective units, your favorite moments. The demobilization *scenario* begins where this screen ends.
- **Scenario framework**: threat types × doctrines × maps, all data-driven so scenarios are content, not code. The invader doesn't have to be alien — a **conventional human invasion** uses the exact same systems (descent interception becomes coastal/air interdiction), alongside aliens, monsters, zombies, and demobilization/civil collapse. Different doctrines genuinely perform differently per threat — that's the replayability engine, and (as far as we know) this coordinator-of-a-nation defense fantasy hasn't been properly executed anywhere; the scenario breadth is how we prove the concept generalizes.

---

## Roadmap (revised — each phase ends playable)

**Done — P1 slice:** top-down renderer, pan/zoom camera, data-driven tile maps, physical units with steering + real projectiles (shells arc, missiles home), fire discipline (no dogpiling), unit stat cards.

- **P1.5 — Foundations refit.** Generalize unit defs into the attribute/layer system (behaviors as composable data). Terrain types with effect lists in the map format. Named regions/cities in map data at theater scale (~100×100 km, ~5 urban areas). Basic fog of war.
- **P2 — The map is a machine.** Buildable roads/rail (network graph), city connectivity → IC, on-network movement bonuses, entrenchment/fortification, terrain combat effects. **Plot/district control**: named plots with hold rewards, ownership tracking (the substrate frontlines and difficulty-by-extent are built on).
- **P3 — Mobilization economy.** Civilian baseline economy, automatic light-industry conversion by mobilization level + directed heavy industry, manpower-from-population with mobilization levels, named deposits + specialized extractors, stockpiles. **Delegated construction** ships here: build orders are designated, planner AI sites them (with the pin-a-spot override).
- **P4 — Two-grain forces.** Ground battalion system (composition/strength/entrenchment/supply, rendered as visual clusters) + individual air/naval (port sortie/fuel/carrier design from v1). Production via converted industry. Research-as-programs (lab + industrialization phases).
- **P5 — The nation acts.** The autonomy layer: auto-garrison, militia muster, reinforcement dispatch, supply self-routing, civil defense/evacuation directives, alert postures. *The signature phase.*
- **P6 — Supply & fronts.** Supply flow + interdiction + out-of-supply degradation; emergent frontline detection (which also feeds battle naming).
- **P7 — The Invasion (vertical-slice scenario).** The canonical playthrough above, playable end-to-end on an authored island map: descent interception, landings, CAS, city battle, orbital screen. Underground layer toggle ships here (tunnels + sheltering).
- **P8 — Stories.** Event timeline, named battles, after-action report/replay screen.
- **P9+ — Breadth.** More scenarios (monsters/zombies/demobilization), doctrines, custom maps/editor, art & sound passes (designer-directed).

**Carried from v1:** unit roster & flavor text, research-tree content, strike systems, the balance lessons (fire discipline, influence momentum, manpower caps, air sorties/fuel), art motifs. The engine is new; the ideas are not.

---

## Open Questions (deliberately deferred)
- Battalion composition depth: fixed templates vs. player-designed (HoI-style designer is a rabbit hole — recommend fixed templates per doctrine first).
- Real-time pacing: continuous with speed controls (recommended, matches v1 feel) vs. wego/pause-heavy.
- How much of the *enemy* also runs on the autonomy layer (ideally: all of it — same systems, different doctrine).
- How smart the construction-planner AI needs to be at v1 (simple scoring — near roads/power, away from coast — is probably enough to start; it can get taste later).

*Settled since first draft:* map scale (one ~100×100 km theater, ~5 urban areas — not whole-planet); build placement (delegated by default, pin override); conversion model (automatic light / directed heavy); invader need not be alien.
