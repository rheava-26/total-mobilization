# TOTAL MOBILIZATION — Top-Down Concept & Roadmap
*Synthesized from the designer's vision brain-dump (2026-07-19). Intentionally at concept level — specifics get locked per-phase.*

---

## The Thesis

**You are the coordinating intelligence of a society at war — not its hands.**

Every system serves one feeling: *a unified national effort is happening, and you are directing it, not performing it.* Militias raise themselves when invasion looms. Infantry garrisons defensible ground on its own. Supply flows along the roads you built without you routing each truck. The conventional army dispatches reinforcements to threatened regions. Civilians move underground when you sound the alarm. You decide **what the nation prepares for and where its weight goes** — the nation does the rest.

This also fixes the arcadey feel at the root: the old game felt like a toy because *you* were the only actor. When the world acts, it feels like a war.

---

## The Target Experience (an ARCHETYPE, not a script)

This narrative conveys the *feel* the game must produce — it is one example of the kind of war the systems should generate, not a story to implement beat-for-beat. Every specific in it (the island, the city names, micromissiles, oil counts) is illustrative; the systems must produce *equivalents* of these beats from procedurally generated maps, names, threats, and resources, so each run tells its own version:

> Start the game on a big island. Build a few factories; connect your cities with roads so industry actually flows. The threat is announced — an alien invasion, mostly air-based — so you research **micromissiles** and start converting civilian industry to military production. Build carriers (island nation), harvest the region's three oil deposits with offshore rigs (unlocking heavier ground vehicles), and put up an **ultralight-metal works** — which is what actually completes the microrocket program and lets you mass-produce them. Eighty bajillion fighter jets later, the invasion is inbound: militias muster themselves, the army pushes a mixed reinforcement battalion into the key regions, units dig into natural terrain, the fleet goes to high alert, spare stockpiles get shipped to the threatened coast, and the population moves underground while the military waits. The landing comes down through your SAM umbrella — 40% intercepted on descent. The survivors land on the road network outside the city; your micromissile fighters fly constant close air support and shred the unescorted drones. The **Battle of Syrograd** begins as their ground forces enter the city — militias, garrisons, and regulars break them decisively. The orbital bombardment ship overhead is continuously screened off by your air cover until someone nukes it. You won. Roll the after-action report: casualty counts, the named battles, interception rates, your favorite units.

Named regions and cities everywhere — **procedurally generated per run** (the names above were invented for this example; nothing about them is canon). The war produces a **story**, and the game tells it back to you, with names that belong to *that* run.

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
- **Mobilization happens automatically, over time — you don't micromanage it.** The mobilization level rises on its own as the war develops (time + threat); industry re-tools and manpower is raised by the nation itself, not by the player pulling levers each minute. This IS the "the nation acts" thesis applied to the economy. (Designer directive — the *baseline* is autonomous ramp; see below for how the player influences it.)
- **The player's economic levers are UPGRADES, not management.** Specific constructions / programs let you (a) **accelerate** the automatic ramp (mobilize faster than the default clock) and/or (b) **arm better** (higher-quality output, better-equipped militias/industry). You invest in *changing the curve*, then the nation runs on the new curve — you don't hand-crank the conversion. Which buildings/programs do this, and the exact rates, are tunable data (designer's to set); the *pattern* is: automatic baseline + upgrade accelerators/improvers.
- The opening minutes are groundwork — roads, connections, early investment — and the curve *ramps on its own*. Keep v1's influence-momentum and research-drawdown lessons; they fit this naturally.

### 3. The Map Is a Machine (infrastructure, terrain, world-shaping)
- **Scale: a theater, not an islet.** Roughly a 100×100 km region — you're defending a country-sized piece of land, not Nauru. Baseline density: **~5 cities/urban areas** (v1 accidentally landed on the right number). Big enough for real geography — coasts, interiors, road networks between cities — small enough that every place can have a name you remember.
- **Plots & territorial control.** The map divides into named **plots/districts**. Holding plots yields rewards (resources, manpower, IC, strategic positions); taking more is how you grow — and **difficulty scales with how much you own**, because everything you claim, you must defend. Expansion vs. consolidation becomes a real strategic tension, plots give the enemy something concrete to seize, and control shifts are what frontlines are *made of*.
- **Buildable networks**: roads, railroads, tunnels (tunnels = the underground layer's edges). Connected cities produce more; units move faster and stay supplied on the network; rail moves battalions and stockpiles in bulk.
- **Terrain matters**: forest conceals, mountains channel and defend, rivers block, urban grinds. Terrain types carry effect lists (data-driven, same attribute philosophy as units).
- **Fortification**: entrenchment over time, buildable forts/lines — ground forces become *impactful* because position matters.
- **Fog of war is a COMBAT-phase thing.** During the PREPARATION phase (building up, before the enemy arrives) fog is OFF — you can see your own theater freely while you organize it. When the COMBAT phase begins, fog turns ON (sensor ranges, patrols; beyond that, intelligence *estimates* — the autonomy fantasy: your intel reports, it doesn't scry). Also player-toggleable on/off regardless of phase.
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
- Sustained combat in a place becomes a **named battle** ("Battle of ⟨that run's city⟩") tracked with casualties, duration, and outcome.
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

*Settled since first draft:* map scale (one ~100×100 km theater, ~5 urban areas — not whole-planet); build placement (delegated by default, pin override); conversion model (automatic light / directed heavy); invader need not be alien; **maps and all place names are procedurally generated per skirmish run** (fresh seed every game so each war has its own geography and its own names — the current fixed-seed island is a dev fixture, not canon; authored maps are reserved for scenarios that need specific geography).

**Production & supply chain (settled — the concrete P4 model):**
- **You direct WHAT, the nation makes it automatically.** You pick a production *category* (tanks / warships / aircraft / artillery / … — categories are data, "tanks" is just an example) and build the matching production facility; it then produces that unit class on its own, no click-to-queue micro. Directed-what, automatic-how — same shape as mobilization and construction.
- **Every production category has a RECIPE (data): input RESOURCES + prerequisite BUILDINGS.** (Illustrative only, not canon: a tank line might need chromium + tungsten + steel + an ammunition plant; a shipyard would need something else.) A facility only produces while its recipe is satisfied.
- **Resources come from a SUPPLY CHAIN.** Named resource DEPOSITS sit on the map in resource-rich areas (procedural). You build MINES/extractors on them to feed per-resource stockpiles, which flow (over the road/rail network) to your factories. Missing a resource you can also **import it in exchange for INFLUENCE + a bit of IC** — a pricier stopgap, not a replacement for owning the deposit.
- **The loop the player actually runs:** war effort demands heavy industry → pick a category → see its recipe → build the factory (idle, unfed) → build the mines to supply it → production comes online and mass-produces → those forces win the engagement. This chain is also the game's **basic tutorial/demonstration**.

**Phases (settled):** a run has a **PREPARATION** phase (build-up, fog off) and a **COMBAT** phase (fog on, the enemy arrives). The tutorial/demonstration walks the player from prep through the production chain into a first combat encounter.

**First-run / tutorial is OPEN, not dictated (settled):** the first playthrough is *not* a linear scripted walkthrough that tells the player exactly what to click next. It's a normal run with the full set of options available from the start — every building, every production category, import, phases — and a **dismissable, optional** guidance layer that *offers* the intended path (heavy industry → pick a category → build the factory → supply it with mines → win the first encounter) without gating anything. The player is free to ignore it and explore. Games that dictate every step are annoying; this one surfaces the path and lets you off the rails. (Prompt/lore text for that guidance layer is authored later by the designer — the framework ships with clearly-marked placeholders.)

**Plays like Total Mobilization; IC is a brute-force escape valve, NOT a "direct the nation" budget (settled — corrects an earlier mis-record):** an earlier draft here overreached and described IC as a currency you spend to puppet the nation's every behavior. That's wrong. The game should feel broadly like the original **Total Mobilization** — you direct the war through priorities and delegated construction/production, and the nation acts largely on its own. IC's specific role is narrower: it's what lets you **brute-force around shortfalls** — e.g. sell surplus resources for IC, or spend extra IC to import a resource you can't mine — a pricey stopgap when you're stuck, not the primary way you play. Owning the supply chain is still the real game; IC just buys you out of a corner.

**Design directions raised (not yet built — keep the principle, specifics are disposable):**
- **You are not isolated (except when a scenario says so).** By default the player nation is part of a wider world it can reach out to: build **trade convoys** (sea) to import/export, **connect railroads** to neighbors (passively, over time), etc. Being cut off — a besieged island with no outside contact — is a *specific scenario condition* (and a dramatic one), not the baseline. This is also where IC-for-import above physically happens: the imports arrive on convoys/rail, so trade routes are the fiction behind the escape valve.
- **Main-menu map/scenario picker.** Rather than always dropping into one random seed, offer a **selectable list of maps** at the main menu — varying terrain character and **difficulty options** — that the player chooses from before starting. Procedural generation still backs each entry (a menu row is a scenario descriptor: label + gen params + difficulty), so this is a curated front door onto the generator, not a return to hand-authored-only maps. Difficulty scales concrete knobs (enemy landing size, starting stockpile, etc.), kept as data.

**Arsenal scope & the tech ceiling (settled):** the player's roster spans conventional → **near-future / present-day** and stops there *for now* — a deliberately *defined grounded baseline*. That baseline includes the modern staples the old roster lacked: **recon + armed drones (UAV/UCAV), loitering munitions, rocket artillery/MLRS, precision cruise missiles (long-range strike), ballistic missiles (short/medium range), ATGM/MANPADS teams, modern layered air-defense, anti-ship missiles, EW/jamming**, up through the bleeding edge of real-world lineage — **hypersonics, drone swarms, early directed-energy point-defense**. The human arsenal stays grounded even though the book's *enemy* is not. **SCI-FI is a separate, later "go crazy" phase built ON TOP of this baseline** (see the research model below) — not part of the grounded-roster work.

**Tech / research model (settled): big bonuses behind big gates.** Advanced capability isn't just "spend some points." A heavy tech unlocks only when you satisfy a COMBINATION of **research effort (IC/effort over time) + prerequisite BUILDINGS + RESOURCES** — the bigger the payoff, the more infrastructure and material it demands. Buildings do double duty: the same advanced-industry building both *gates/enables* a line of research and later *produces* the unit. Baseline conventional gear is **pre-unlocked**; production is gated on a unit's tech being unlocked. *Illustrative future (sci-fi tier, NOT built yet)* — usable human cloning armies would require **big data centers** (to give clones real intelligence) + **cloning facilities** + **massive farms** (to feed them) + the research itself + resources: a massive payoff sitting behind a massive, multi-building, multi-resource gate. That's the template every big tech follows.

---

## Art-Direction Notes (queued for the graphics-overhaul pass — designer-flagged, do not lose)
- **Roads must read as part of the landscape, not an overlay decal.** They already render inside the same prerendered painterly canvas as terrain, but the strokes still sit visually ON the land rather than being OF it. Wanted: feathered/irregular edges, terrain color bleeding into road margins, wear/color variation along the run — a road that looks worn into the ground. (Data-wise roads stay a separate infra layer; this is purely about the paint.)
- **Extreme close-zoom checkering**: the per-tile jitter (especially on water) reads as a faint checkerboard at max zoom. Fine at strategic zoom; needs a noise-scale rework up close.
- Unit graphics: markers are intentional for now; per-type silhouettes deferred until the designer directs the pass.
- **Cities should be prettier.** The city-block rendering is functional but plain; the designer wants urban areas to read as richer, more characterful places in the graphics pass (deferred with the rest of the overhaul — flagged so it isn't lost).
