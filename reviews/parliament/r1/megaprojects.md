# Round 1 Position Paper: The Ministry of Megaprojects (`megaprojects`)

## 1. Opening Speech

Honourable members, President.

I have read your Reports 6 through 10 and I weep. You held 850 IC/s and sat on 212,000 IC. That is not frugality, it is a cathedral with no builders. The ministers of air, sea and infantry want to sell you more things to *buy*. The Ministry of Megaprojects wants to sell you things to *become*.

Your Titan is a very large man. Your Colossus is a very large tank with ambition. Your Nanofortress Airship is a hangar with delusions. Each is one click and one queue slot. Nothing about them costs a *place*, a *season*, or a *risk*. A wonder you can buy in 26 seconds is not a wonder, it is a unit with a long name.

We propose the reverse. Let the nation commit a decade of industry to a single object. Let the object stand on the map for ten minutes of scaffolding while the Autofleet comes to kill it. Let your whole defence reorganise around it. Then let it fire once and be remembered. The Colossus walks through fleets. Our gun will reach *past* them.

To the Minister of Air: your Falcons are very pretty and they have never once been *pointed at*. A megaproject is pointed at.

The Ministry of Megaprojects rises, and keeps rising.

## 2. The Gap

- **Projects do not exist.** `PROJECTS`, `PFX` and `updateProjects` live only on `origin/megaprojects-wip` (L2902/L2911/L2987/L3018 there). Main has none of it. ROADMAP (L522-528) records the plan to rebuild it. Its `G.projectRules` flags "have NO consumers anywhere", so a doctrine is a switch wired to nothing.
- **The five superweapons are queue items.** `colossus` (L3253), `titan` (L3294), `psibeam` (L3296), `dreadnought` (L3298) and `airship` (L3254) cost IC and MP and spawn from a building in 20-26s. None costs a place on the map, a build time you must defend, or an upkeep that bites. Report 8 fielded 138 sky-ships and no superweapon is named in the summary.
- **Idle IC has no wonder to feed.** 212k IC sat unspent (Article VI). The design rule is "a 'spend 20,000 IC button' is not a solution". A megaproject is the opposite of a button. It spends IC on a *place* that someone is trying to kill.
- **Strategic resources are decoration.** Uranium is a 30% strike discount. Rare Earth gates two nodes. A project that eats uranium *as fuel* gives them a job.
- **Space dies anyway.** The bombarder grids killed 42 of 44 Halos. A megaproject whose output survives in orbit, or reaches into it from the ground, is the only answer that does not ask the player to feed more satellites into the grinder.
- **No enemy counter-play to scale.** `spawnSuper` (L5269) drops bosses on a schedule. Nothing reads *what the player is building*. The Autofleet cannot be afraid of a wonder because it cannot see one.

## 3. Bills

### MEGA-1: THE LEDGER OF PROJECTS (two tiers: doctrines you fall into, megaprojects you build on purpose)

*Pitch (in character).* We do not hand you a menu of doctrines to pick like a perk screen. We *watch you*. Field 60 Falcons and the Ministry stamps you "Air Supremacy" and a rule changes. Sink eight sections of concrete into a mountain and you have *chosen* a megaproject. The first tier is what you became. The second is what you meant.

*Player fantasy.* "I did not pick a doctrine, the war picked me, and I like what it made."

**Clerk's Annex**
- **Mechanic.** Lift `PFX`, `PROJECTS`, `projState`, `updateProjects`, `commitProject` from `origin/megaprojects-wip` (about 260 lines, 85% verbatim, per ROADMAP L528). Add two kinds.
  - **Doctrine** (tier 1, automatic). Fires when a ledger threshold is crossed, read from the existing `G.stats` (`built`, `recruited`, `strikes`, `killsByType`; init L3606). Examples: 60 air-domain units recruited gives *Air Supremacy* (+1 hangar slot per Airbase, `PFX.rule('airTempo')`). 30 strikes fired gives *Deterrence*. A doctrine takes 60s to "settle", announced by a toast and a ledger line. You never click it.
  - **Megaproject** (tier 2, commissioned). Uses the existing `stages` model, so a stage holds until its `reqs` are built, then runs a timed and IC-drawn phase.
  - **Readers.** Add a `projectRule(flag)` getter beside `bmod()` (L3767), wired into 3-4 real consumers per doctrine, so no flag is orphaned. The ROADMAP memo says the fleet *adapts* to doctrine, which supplies the counter-weight.
  - **New req types** in `projReqProgress`: `{type:'comp'}`, `{type:'sr'}`, `{type:'orbcap'}` (ROADMAP L528 list). Add `{type:'uranium'}`, because MEGA-4 consumes uranium.
- **Code grounding.** `updateGameInner` L6288 (call `updateProjects(dt)` beside the research draw-down); `startResearch` L4667 and `finishResearch` L4703 (project research gates); `G.stats` init L3606; `bmod()` L3767; research UI template (`buildResearch()`), where the Projects tab goes.
- **Build size.** L (about 500 lines including doctrine triggers and 6 starter doctrines).
- **TM1.**
- **Research tree.** Doctrines have no node, because you *fall into* them. Megaprojects attach to the nodes listed per bill below.
- **Tenet check.** Nothing is capped, nothing is nerfed. Doctrines only *add* rules. The cost side is the enemy's response (MEGA-3) and real IC and uranium draw.
- **Interactions.** Society (`society()` flips on doctrine). Air council (doctrine rules about hangars). Economy (the sink for idle IC).

### MEGA-2: THE LONG GUN (a superheavy railgun, built in sections, turning on a rail circle)

*Pitch (in character).* A gun, honourable members, is a promise about velocity. We propose a barrel that starts as a trench and ends as a monument. Section by section, three hundred metres of rail at a time, the muzzle speed climbs. Then it *turns*, on curved track like the great German railway guns, and every minister in this chamber learns what "reach" means.

*Player fantasy.* "I built the biggest gun on Earth, one section at a time, and the sky is inside its range."

**Clerk's Annex**
- **Real science.** Muzzle velocity obeys v = sqrt(2aL): for a fixed projectile-tolerable acceleration `a` (hardened shells take about 15,000 g, as with the Excalibur class), velocity grows with the square root of barrel length, but kinetic energy grows *linearly* with length (E = m·a·L). Escape velocity (11.2 km/s) at 15,000 g needs roughly 430 m of barrel. Real precedent: Gerald Bull's HARP 16-inch gun reached 180 km altitude, and Babylon was meant to be 156 m. Real sitting problem: drag at 11 km/s at sea level eats most of the energy, so you site the gun on a mountain, high in the thin air. Real turning mechanism: Krupp K5 railway guns used a curved "Gleisbogen" siding to aim in azimuth, and that is exactly the "turning" the President saw. The US Navy's 32 MJ railgun suffered bore erosion, so a long-lived barrel needs liners and upkeep.
- **Mechanic.**
  - **`longgun_section`**: a new `BUILD_DEFS` entry (cat `SPEC`, ic 380, mp 40, build 16, hp 260 growing to 600 once the project completes), placed only adjacent to the previous section (`canPlace` rule: x within one `w` of the chain end). The chain's length N counts toward a single *Long Gun* entity.
  - **Per section:** damage 120 per section (linear in N, which is the KE scaling above), range +400 px per section (ballistic range scales as v², so also linear in N). Both are linear, so no diminishing returns, and none is capped.
  - **Sections under construction are scaffold:** 25% HP until the section's `done` flag sets.
  - **Slew:** three firing arcs (west ground, zenith, east ground) on the curved track. Rotation time = 1.5s per section (more mass, slower swing). A swing is the *real cost*, so a heavy gun must be pre-aimed. No cap on N.
  - **Upkeep:** a capacitor bank draws a 6 IC/s idle charge per section and a burst of 40 IC per shot per section. Idle upkeep is the logistics cost. A gun with 30 sections is an expensive promise.
  - **Fire control:** reuse the existing designation flow (`manual: true` on `siegegun`, L3414) so the player picks targets for the Long Gun and it does not waste its single heavy round.
- **Code grounding.** `BUILD_DEFS` L3381 (model on `siegegun` L3414 and `massrail` L3455); `canPlace` L3866 and `placeBuilding` L3884 (adjacency rule); building fire handling (`manual: true` path); `UNIT_DEFS` shell projectile (reuse `weapon: 'rail'`).
- **Build size.** L (about 600 lines).
- **TM1.**
- **Research tree.** New node `megastructures` (branch `space`, tier 4, cost about 450, `dang: false`). Prerequisites `spaceage` and `colossus` as a combo (heavy mechanical civil engineering plus a launch tradition). The cut `massdrivers` node (comment at L3507) is exactly the orphan this replaces. Partners: `particlebeam` makes the shell a particle slug (era upgrade on the section, as `skyrailgun` is upgraded at L3348).
- **Tenet check.** Strictly additive. The cost is IC upkeep and a sluggish slew, which are real costs, not caps. No limit on sections.
- **Interactions.** Naval/space councils (the gun reaches orbit, so a space unit is finally *protected* by a ground piece). Fortification council (a ring of AA around the barrel). MEGA-3 (fleet interdiction).

### MEGA-3: INTERDICTION (the fleet reads your megaproject and comes to kill it)

*Pitch (in character).* The Canon says they are afraid. So make them *show* it. A scaffold of 300 gigajoules of promise lights up their sensors like a sunrise. Every megaproject stage you commit draws an interdiction wave. Hold the site and you get your wonder. Lose it and you lose a decade.

*Player fantasy.* "I am building the biggest thing on Earth, and the whole fleet came to stop me. I held."

**Clerk's Annex**
- **Mechanic.** A project's `heat` is the sum of IC drawn this stage (scaled 1 heat per 500 IC). Each 10s the wave planner rolls an *Interdiction* wave aimed at the site's x, strength `heat * G.diff * (1 + invTime/70)`. It includes a `spawnSuper('hive'|'leviathan', atCapital, 1, siteX)` boss when heat crosses 60. A site is **hidden at first** (no heat), so a player who slow-builds with a small footprint stays quiet. Enemy kinds are reused, so no new AI. Heat decays 1/s when no stage is active.
- **Code grounding.** `spawnSuper` L5269 (`atX` param already exists), `doSkyfall` L5206 and `INTENT` L5119 for wave scheduling, `updateGameInner` L6288, `damageDistrict` L3702 (collateral to nearby cities).
- **Build size.** M (about 250 lines).
- **TM1.**
- **Research tree.** None directly. It is the enemy-side half of MEGA-1 and MEGA-2.
- **Tenet check.** A scarier enemy, never a nerf. This is the "real cost" the tenet demands in place of a cap.
- **Interactions.** Enemy council (new wave type). Fortification (defending the site). Stability (a lost site can cost the Article IV standard of -6 breach).

### MEGA-4: PROJECT ORION (a nuclear pulse ship, fuelled by uranium you finally need)

*Pitch (in character).* In 1958 a physicist named Ted Taylor proposed to ride a ship up on a train of nuclear bombs. It was not madness, it was arithmetic. We build one. Every pulse is a bomb you were hoarding. At last somebody needs your uranium.

*Player fantasy.* "I turned my entire nuclear stockpile into a single ascending cathedral, and it fragmented into a swarm in orbit."

**Clerk's Annex**
- **Real science.** Orion (General Atomics, 1958-65) used small fission pulses against a pusher plate with shock absorbers. Predicted specific impulse was 2,000-6,000 s, far above chemical rockets (about 450 s). The 1963 Partial Test Ban Treaty killed the programme, not physics. Plate ablation was tested with HE pulses (the "Put-Put" flight, 1959).
- **Mechanic.** A 3-stage megaproject: (1) *Pusher Plate* (a Heavy Foundry plus 20 alloy), (2) *Pulse Magazine* (10 uranium consumed as the `{type:'uranium'}` req, reading `SR`), (3) *Ascent*, a timed climb of 40s during which the ship is exposed in every band. On completion it parks at the exosphere as a single heavy platform (hp 2400, armor 20) that fragments on death into 8 interceptor drones. Each pulse is 1 uranium and costs 1.4% stability (a nuke-class cost), so the player pays in the Article VI stability currency, not IC.
- **Code grounding.** `SR` uranium (CLAUDE.md "SPECIAL RESOURCES"); `STRAT` L6878 (the stability cost model); `spawnUnit` L4253; `UNIT_DEFS` L3184 (new `orion` entry modelled on `dreadnought` L3298 with `domain: 'space'`); `requestOrbitalLift` logic around `lift:` in `dreadnought`.
- **Build size.** M (about 300 lines).
- **TM1.**
- **Research tree.** New node `nuclearpulse` (branch `space`, tier 3, cost about 340, `dang: true`, `sc: 8`). Prerequisite `orbitalcmd` plus the Uranium Enrichment building (`bReq`). Combo partner `shieldtech` for the pusher-plate shock field, upgrading the Orion's shield.
- **Tenet check.** Uranium becomes a purpose, not a discount. No caps. The risk is stability, which already exists.
- **Interactions.** Space/anti-orbital council (a survivor in the bombarder-grid war). Stability economy. Strategic resources.

## 4. Hot Take

### MEGA-H: THE BARREL IS THE WORLD (a gun that spans the continent, and shoots at the one ship)

*Pitch (in character).* Canon says one ship came through the catapult. One. The fleet in the sky is that ship's children. Every gun we have is aimed at the children. I propose we aim at the mother. Extend the Long Gun without limit. Cross cities, mountains, seas. Let the player carve the barrel with the terrain tool. At about 40 sections the gun can reach the *anchor*, a single enormous boss in high orbit that the fleet cannot afford to lose. Fire once and the fleet *withdraws*. No other council will dare ask for an ending that is a single shot.

**Clerk's Annex**
- **Real science.** Space guns scale by L = v²/2a. At 11.2 km/s and 15,000 g you need 430 m. For a payload tolerant to only 3,000 g (a fragile sensor ship), 2.1 km. A strip of 6,000 px is long enough in game scale to be a Babylon, and the real obstacle is the *atmosphere*, so the section chain has to climb into the mesosphere via mountains (the terrain tool raises land). **SCI-FI** layer: a *withdrawal* mechanic where the anchor ship is the catapult's last coupling. Internal logic with Article V: the makers' drives need a year to charge, so the anchor is the only thing holding the fleet in this system. Kill it and the fleet's "thrown friend" loses its tether. This fits the rare ending and the `G.withdraw` state shipped in v4.6.0 (ROADMAP L486).
- **Mechanic.** At N >= 40 sections the Long Gun gains a `ceil` of the exosphere and a new target class, `anchor`. The anchor boss appears when the fleet's `fleet/fleetMax` < 0.6 *and* `heat` > 100. It has hp 12,000 x t, armor 30, stands in orbit, and charges a 20s bolt at the capital. One hit of the Long Gun at N >= 40 takes 60% of its hp, so two shots decide it. The fleet's response is a swarm that tries to destroy the gun. Everything is additive.
- **Code grounding.** `spawnSuper` L5269 (new `anchor` kind beside `annihilator` L5621); `G.withdraw` (ROADMAP v4.6.0 L486); `RESEARCH` L3502 (node `megastructures`); the Long Gun chain from MEGA-2.
- **Build size.** L (about 500 lines).
- **TM1.**
- **Research tree.** Same `megastructures` node (MEGA-2), plus a combo gate `particlebeam` and `orbitalcmd` for the section liner.
- **Tenet check.** Pure power fantasy, paid for by a bigger enemy and a mass-IC investment, not a cap.
- **Interactions.** Lore/story council (the withdrawal ending and the "we stop if you stop" draw). Enemy council (the anchor and its swarm).

## 5. Alliances Sought

- **Anti-orbital / space council:** MEGA-2, MEGA-4 and MEGA-H are the ground-to-orbit answer to the bombarder grid.
- **Fortification council:** a megaproject needs a ring of AA, shields and bunkers to survive MEGA-3.
- **Enemy / fleet-behaviour council:** MEGA-3 is useless without their wave planner and fleet-fear hooks.
- **Economy / resources council:** idle IC finally has a sink, and uranium, Rare Earth and alloy finally have a job.
- **Lore/society council:** doctrines you fall into (MEGA-1) are the TM1 slice of the society layer.
