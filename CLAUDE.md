# TOTAL MOBILIZATION (v1) — complete reference

## WORKING STYLE (standing instruction from the designer)

**Delegate the coding. Always implement game changes by dispatching a Sonnet subagent (in an
isolated git worktree), not by hand-editing `title.html` yourself.** Because the whole game is
one file, run parallel subagents only on non-overlapping regions, then merge each worktree back.
Reserve your own turns for the part the designer actually wants from you: **talk about the DESIGN
— the ideas, the mechanics, the feasibility and trade-offs, and the plan** — while the subagent
builds. Give real opinions, not "big change, nice." Don't narrate coding logistics (merges,
git, harness) unless asked. Keep the honest verification bar (headless boot, zero pageerrors)
on whatever the subagent produces before it ships.

---

Everything known about the **original** Total Mobilization, the single-file game in this
branch. Written 2026-08-05 by reading `title.html` end to end. This is a reference document,
not instructions — it exists so another session (or another tool) can understand the game
without re-deriving it.

**The game is one file: `title.html`, ~3,750 lines, ~537 KB.** `index.html` is a small stub
that redirects to it. No build step, no dependencies, no asset files. Serve the directory over
static HTTP and open `title.html`.

There is a separate, much larger **top-down 2D rebuild** on the `claude/topdown-rebuild`
branch (~20,000 lines under `topdown/`). The designer considers **this** version — the small,
crude one — more fun. That judgement is the most important fact in this document.

---

## THE PREMISE

- A hostile **autofleet** arrives from orbit to take or destroy the planet.
- You control one nation on a 6,000-px-wide side-on strip of world (`WORLD_W = 6000`).
- You have a **PREP** phase on a countdown, then the invasion lands and never stops.
- The fantasy is *total mobilization* — transforming your entire society, past any recognisable
  form, in the hope of surviving.

---

## CORE LOOP

- **PREP phase** — a real countdown (`G.prepTimer`), length set by scenario intent: recon 200s,
  decap 135s, invasion 450s, glass 500s. Displayed as `T-45s` in the HUD.
- At **12 seconds** remaining the game fires `⚠ RE-ENTRY DETECTED — [INTENT]`, ducks the music,
  and tells you what kind of attack is coming.
- **INVASION phase** — the fleet arrives. `G.fleet` / `G.fleetMax` track how much of it is left;
  destroying the fleet is how you win. Waves spawn continuously, scaling with elapsed time
  (`t = G.diff * (1 + G.invTime / 70)`), so every enemy gets stronger the longer you survive.
- **Pause exists** — `Space` toggles `G.paused`, with a `❚❚ PAUSED` overlay. The research menu
  also pauses the battle while open. Strike aiming is explicitly built to work while paused.
- **Speed control** — `G.speed`, set by on-screen buttons. The game's internal second is
  `0.45 × G.speed` of real time, so all displayed timers are divided by that.
- **Loss conditions** — all cities dead, or `G.stab <= 0` → `STABILITY COLLAPSED`.

---

## THE ECONOMY — four currencies plus extras

- **IC (Industrial Capacity)** — the main build/spend currency. Base income 6/s, multiplied by
  `1 + G.mult.econ`. Starts at 320.
- **Manpower (MP)** — capped at 900. Base income 4/s, scaled by `(1 + G.mult.mp) × econ`.
  Starts at 180. **Manpower drives your army cap.**
- **Influence** — the research currency, capped at 600, starts at 50. This is the deliberate
  brake on the game: it stops you researching everything and spamming buildings.
  - Income: `(2.0 + infBonus) × (effectiveStability / 100) × infMomentum − 2 × superhumanCount`
  - **INFLUENCE MOMENTUM** — patiently *not* spending ramps income up to **3×** over ~75s
    (exponential, τ=25s). Any spend — a research draw-down tick, a building placement — snaps it
    straight back to 1.0. Explicitly rewards saving, punishes influence-spam.
  - **Ground units cost no influence.** That is the point: influence pushes you toward mass.
- **Stability** — 0–100, starts at 65.
  - `effStab() = stab − stabOffset (research unrest) − cityStabPenalty()`
  - **Stability cap**: `65 + capBonus + max(0, mult.stab) × 30 − superhumanCount`, clamped 20–100.
  - Regen is slow and only trends toward the cap (max +0.5/s); anything above the cap decays.
  - Buildings don't grant stability directly — **Comms Tower, Propaganda Bureau and Radar raise
    the CAP.**
  - Losses: a city destroyed −12, a fallout breach −6, city damage bleeds it continuously,
    dystopia state −1.4/s. Killing a boss superweapon gives **+10**.
- **Antimatter** — capped at 300, accrued only by Antimatter Extractors. Spent on the Antimatter
  Strike.
- **Components** — `alloy` (Heavy Alloy) and `rocketpart` (Rocket Parts), capped at 60 each.
  Intermediate goods produced over time by certain buildings and spent to assemble bigger units.

### Caps

- **Army cap** — `12 + floor(manpower / 25) + mult.capb + building bonuses`. ~300 MP ≈ 24,
  ~900 MP ≈ 48. **Mechanical units don't count against it** (`isMechanical`).
- **Orbital lift** — space units need lift capacity from Space Centers (5) and Space Stations (8).
- **Air command** — hangar slots from Airbase (6), Aero Works (4), Underground Airwing (3).

---

## RESEARCH — the heart of the game

**Design statement in the source:** *"Conventional arms (rifles→railguns→nukes→orbital) are
ALREADY unlocked at game start; research is purely the exotic, escalating doctrines."*

- **28 nodes across 7 branches**, laid out on a 1000×600 pannable board.
- Every non-root cost is multiplied by **1.6** on load — *"influence is precious — research is a
  real strategic commitment, not a trickle of cheap upgrades."*
- **Draw-down funding**: research siphons influence gradually (up to `cost/time` per second)
  and completes when fully **paid**, not on a timer. Underfunding stretches it rather than
  stalling it. There is a research **queue** that auto-starts the next eligible item.
- **`dang: true`** marks dangerous research, which adds `sc` **unrest** to `stabOffset` — a
  *recoverable* penalty that decays over time, not a permanent loss. Cancelling refunds it.
- Gates beyond prerequisites: **`srReq`** (needs a special resource), **`bReq`** (needs a
  specific building standing).

### The tree

**SPINE — THE AGES** (safe backbone, `branch: 'age'`)

| Node | Cost | Effect |
|---|---|---|
| `info` Information Age | 0 (root) | Starting era; full conventional arsenal already mobilized |
| `fusion` **FUSION AGE** | 110 | +45% IC, +5 influence/s. Upgrades Combat Drones → "Hornet" |
| `spaceage` **SPACE AGE** | 280 | +70% IC, +10 army cap. Railgun launch — satellites slung to orbit |

**SPACE** (`branch: 'space'`)
- `orbitalcmd` Orbital Command (240) — +12 army cap, +12% range, unlocks Orbital Gun
- `massdrivers` Mass Drivers (360, needs orbitalcmd + spaceage) — +35% damage
- `voidengine` Void Engine (420, **dangerous** sc10, needs Rare Earth) — +80% IC, +20% damage

**ENERGY** (`branch: 'energy'`)
- `directedenergy` Directed Energy (55) — +10% damage; Laser Tower + Superconductor Facility
- `plasma` Plasma Weapons (150) — +18% damage, +10% range; **re-arms your whole conventional
  line with plasma**
- `antimatter` Antimatter (300, **dangerous** sc10, needs Rare Earth) — Antimatter Dreadnought
  + Antimatter Extractor
- `particlebeam` Particle Beams (340) — Particle Cannons, single-shot hypervelocity bolts

**AUTOMATION** (`branch: 'auto'`)
- `automation` Automation (60) — +12% fire rate; Combat + Kamikaze Drones, Drone Assembly Line,
  Data Center, Mantle Forge
- `robotics` Robotics (150) — +15% damage, +12% armor; Combat Androids, Battle-Droid Foundry
- `nanotech` Nanotechnology (160, **dangerous** sc5) — +50% construction speed, buildings
  self-repair
- `colossus` War Walkers (300) — **COLOSSUS WALKER** superweapon, +20% armor, +12% damage
- `airfortress` Sky Fortresses (320, **dangerous** sc6) — **NANOFORTRESS AIRSHIP**, +30% IC

**BIOENGINEERING** (`branch: 'bio'`)
- `biotech` Biotechnology (60) — +30% manpower, +stability; Shock Troopers, Gene Facility
- `transhuman` Transhumanism (180, **combo: biotech + robotics**) — +20% armor & damage;
  Cybernetic Legion
- `cloning` Cloning Vats (150, **dangerous** sc8) — +60% manpower, +10 cap; Clone Battalions
- `superhumans` Superhumans (175, **dangerous** sc8) — Superhuman Cadre; **society → Verdant**
- `titans` Living Titans (320) — **TITAN** superweapon, +40% manpower, +8 army cap

**PSIONICS** (`branch: 'psi'` — the entire branch is dangerous)
- `psistudies` Psionic Studies (70, sc5) — +stability, +4 influence/s, +8% fire rate;
  Psionic Relay
- `psionics` Applied Psionics (170, sc8, **requires the Psionic Relay building**) — +15% fire
  rate, +12% range; Psi-Troopers
- `telekinesis` Telekinesis (300, sc10) — **TELEKINETIC PYLON** superweapon, +60% construction
- `hivemind` Hive Mind (300, sc10, **combo: psionics + nanotech**) — +30 army cap, all units
  12% cheaper
- `shapeshifter` Shapeshifters (360, sc12, **TRIPLE COMBO: psionics + biotech + nanotech**)

**DEFENSE** (`branch: 'def'` — the answer to orbital bombardment)
- `pointdefense` Point Defense (110) — +10% range; **Aegis Battery**
- `microrockets` Microrockets (200) — rockets that split into independently-homing submunitions
- `shieldtech` Deflector Shields (240, **combo: pointdefense + directedenergy**) — Shield
  Generator

### Combos already existed in v1

`transhuman` (bio+robotics), `shieldtech` (def+energy), `hivemind` (psi+nano), `shapeshifter`
(psi+bio+nano). **But a combo only ever unlocked a unit** — it never changed the world, the
policy layer, or the society. The designer has identified this as the biggest missed
opportunity in the original.

### Society states

`society(kind)` repaints the entire sky gradient and adds a screen tint:
- **dystopia** — blacks into deep reds; `rgba(120,20,20,.12)`; also adds −1.4 stability/s
- **verdant** — teals and greens; `rgba(40,160,90,.10)` (triggered by Superhumans)
- **machine** — cold blues and steel; `rgba(90,120,160,.10)`

This is the only expression of "what your civilization became," and it is **purely visual plus
one stability modifier**.

---

## ERA UPGRADES — technology visibly re-arms your existing army

`UPGRADES` overrides fields on a base unit def once an era is unlocked. `effDef(key)` returns
the upgraded version transparently. This is how tech shift becomes *visible* on units you
already own:

- **Fusion Age** — Combat Drones → Heavy Drone "Hornet"
- **Plasma Weapons** re-arms *gun/kinetic* units only: Infantry → Plasma Infantry, MBT → Plasma
  Pelter "Aegis-P", Heavy → Plasma Goliath, AA → Plasma Rotator, SPG → Plasma Pelter "Thunder-P",
  Gunship → Plasma Gunship "Reaper-P"
- **Microrockets** re-arms strike/CAS aircraft: Warthog, Lancer, Heli → microrocket variants
- **Explicit rule:** missile-armed units (interceptors, stealth, helis, SAMs) keep their missiles.
  Plasma only re-arms gun/kinetic platforms.

---

## SPECIAL RESOURCES & COMPONENTS

- **`SR`** — map nodes you capture by building an Extractor: **Steel**, **Oil**, **Rare Earth**,
  **Uranium**.
  - Uranium makes strategic strikes **30% cheaper in IC and 40% cheaper in stability**.
  - Rare Earth is required by Void Engine and Antimatter.
- **`COMP`** — **Heavy Alloy** and **Rocket Parts**, produced over time by Factories, Heavy
  Foundries, Space Centers and Space Stations, then spent to assemble bigger units.
- **`UNIT_FAM`** — units belong to families (`drone`, `mech`, `elec`, `bio`, `clone`) and
  special buildings discount or boost by family.

**Known flaw:** the designer says strategic resources ended up barely used relative to the role
originally intended for them.

---

## BUILDINGS (~55, in categories)

- **ECON** — Power Plant (+7 IC), Housing Block (+3 MP, +4 cap), Comms Tower (+0.5 stab cap),
  Propaganda Bureau (+1.1 stab cap), Extractor (on a resource node, +4 IC)
- **PROD** — Factory (ground, makes alloy), Barracks, Shipyard (naval, needs water), Airbase
  (air), Space Center (space, +5 orbital lift, makes rocket parts)
- **PRODUCTION LINES** (add unit types with *no research* — an alternative to the tech tree) —
  Missile Works, Heavy Foundry, Aero Works
- **DEF** — Bunker, CIWS "Sentry", SAM Battery, Radar, Missile Silo, Aegis Battery, Microrocket
  Battery, Shield Generator, Particle Cannon, Orbital Gun
- **DISPERSED LAUNCHERS** (all enable the strike button from their own position, so one hit
  can't take out your whole arsenal) — Mobile Launcher (TEL), Arsenal Ship, Ballistic Submarine
- **INFRA** — Rail Hub (underground)
- **TECH** — Research Lab
- **SPEC** — the largest group, and the source of most of your power. *"Most of your power now
  comes from BUILDING these, not from raw research."* Psionic Relay, Cloning Facility, Deep-Ore
  Excavator, Gene Facility, Uranium Enrichment, Drone Assembly Line, Data Center, Antimatter
  Extractor, Ammunition Depot, Battle-Droid Foundry, Space Station, Mass-Driver Railway,
  Munition Factory, Rotary Cannon Works, Plasma Bastion Works, Superconductor Facility, Mantle
  Forge, Underground Airwing, Tractor Beam, Nanite Foundry
- Mechanisms buildings carry: `econ` (income), `aura` (radius buffs to nearby units), `gmod`
  (global modifiers), `garrison` (free defending units), `makes` (component output),
  `autospawn` (free units on a timer), `autoBury` (builds underground at `UG_DEPTH = 50`),
  `bDef` (upgrades **city** self-defense tier: 0 rifles → 1 rotary/CIWS → 2 plasma),
  `shieldR`/`shieldHp` (protective dome), `fallout` (explodes catastrophically)

---

## UNITS

**Ground** — Infantry, Scout "Lynx", MBT "Aegis", Heavy "Goliath", AA "Hailstorm", SPG "Thunder",
MLRS "Tempest", SAM "Aegis-AA", Railgun "Lance"

**Naval** — Destroyer "Squall", Missile Cruiser, Arsenal Ship "Leviathan", Railgun Battlecruiser
"Tempest", Carrier "Sentinel"

**Air** — Interceptor "Falcon", Stealth "Wraith", Laser Plane "Solaris", Strike Jet "Warthog",
Gunship "Reaper", Attack Heli "Kite"

**Space** — Laser Sat "Halo", Missile Sat "Volley", Tungsten Rods "Zeus", Orbital Frigate,
Orbital Strike Fighter

**Production-line (no research)** — Hydra Rocket Battery, Mammoth Super-Heavy, Lancer Heavy Bomber

**Exotic (research-locked)** — Combat Drones, Kamikaze Drones, Combat Androids, **Colossus
Walker**, **Nanofortress Airship**, Shock Troopers, Cybernetic Legion, Clone Battalion,
Superhuman Cadre, **Titan**, Psi-Troopers, **Telekinetic Pylon**, Shapeshifters, **Antimatter
Dreadnought**

Notable numbers: Infantry 46 HP / 30 IC. Titan **1,300 HP / 760 IC / 26s build**. Colossus 900
HP / 620 IC. Antimatter Dreadnought 150 damage at 1,400 range.

- **Air units carry `fuel` and `ammo`** and must return to base to rearm (`AIR_FUEL_BURN = 1`
  per second, so fuel reads directly as flight-seconds). RTB logic uses a deliberately
  pessimistic speed estimate so aircraft don't crash short of the runway.
- **Superhuman Cadres carry a standing upkeep** — each one lowers the stability cap by 1 and
  costs 2 influence/s. Near-immortal flying laser demigods have political consequences.

---

## ☢ STRATEGIC STRIKES

Launched from any `strat: true` building (Silo, TEL, Arsenal Ship, Ballistic Sub).

**Flow: `armSilo(type)` → aim (click the map) → `☢ CONFIRM STRIKE` → launch.** There is an
explicit confirm bar with an ABORT button — *"no fat-finger nukes."* Aiming snaps to an airborne
enemy near the cursor, else bursts at the cursor's altitude if it's well off the ground, else
hits the ground.

| Strike | IC | Stability | Cooldown | Gate |
|---|---|---|---|---|
| Ballistic Missile | 35 | 5 | 5 | — |
| Nuclear Strike | 60 | 14 | 7 | Uranium Enrichment |
| Kinetic Lance | 40 | 6 | 5 | Tungsten Rods unit |
| Cluster MIRV | 55 | 11 | 6 | Uranium Enrichment |
| Firestorm | 50 | 10 | 6 | — |
| Antimatter Strike | 80 | 18 | 14 | Antimatter research + Extractor, 30 antimatter |
| **Mind-Forge** | 90 | **24** | 12 | Antimatter + Telekinesis + Uranium |
| Combat Drop | 55 | 5 | 7 | — (drops *your own* troops, era-scaled: paratroopers → drop pods → orbital landing) |

- **Every strike costs stability**, and you are blocked if `stab < cost + 3`:
  `TOO UNSTABLE — [NAME] COSTS N% STABILITY`.
- Uranium reduces both the IC and the stability cost.
- This is the closest thing v1 has to a "permission to do the unthinkable" mechanic.

---

## ⊕ FIRE CONTROL

Heavy emplacements (Bunker, Laser Tower, Orbital Gun, Mass Driver, Microrocket Battery, Particle
Cannon) can be hand-directed:

1. Select the gun → `⊕ ADD TO FIRE CONTROL`
2. The group projects a shared **radar** over its combined range, with range rings and a sweeping
   scope
3. **Click enemies inside the radar to designate them.** Designated targets get a spinning
   reticle
4. Every gun in the group engages only the marked targets within its own reach and ceiling
5. Right-click / `Esc` releases the battery

Source comment: *"FIRE CONTROL — autonomy suspended; this gun engages ONLY the enemies the
player has designated."* A gun in fire control **holds fire** until you designate something.

**The designer's verdict: this is a nothingburger and it didn't work properly in practice.**
Do not treat it as a proven mechanic.

---

## THE ENEMY

Every hostile **drops from orbit** — a long, slow, vulnerable high-altitude re-entry that
gradually accelerates, giving your high-reaching weapons a window to kill it before it goes
active. All stats scale by `G.diff × (1 + invTime/70)`.

**Line units** — drone, bombpod, interceptor, bomber, gunship, strikewing (shielded), frigate,
whistler (finite antimatter ammo), gtank, gmech, shieldgen (drops a shield dome with its wave),
pelter (ground plasma artillery), earthbreaker (heavy seismic press), bulwark (exosphere heavy
platform lobbing nukes), harvester (lands, anchors, keeps producing)

**Boss-tier AUTODRONE SUPERWEAPONS**
- **SKYFORTRESS "HIVE"** — 1,100 HP, stratosphere, *produces more enemies*
- **LAND LEVIATHAN** — 1,700 HP, armor 22, ground siege walker
- **ORBITAL ANNIHILATOR** — 950 HP, sits in space, charges a 6-second beam
- **AUTODRONE BOMBARDER** — 1,500 HP, anchored in space, produces and bombards

Killing a boss gives **+10 stability**.

### Altitude bands

`SPACE_Y` 12% of world height · `MESO_Y` 40% · `STRATO_Y` 58% · `TROPO_Y` 74%. World height is
**1.9× the viewport** (`SKY_MULT`). Weapons have a `ceil` value limiting how high they reach —
this is the core of the air-defense puzzle: a bomber sitting in the stratosphere can only be
touched by aircraft and railguns.

### Invasion intents (difficulty is the fleet's *intent*, not a stat slider)

| Difficulty | Intent | Behaviour |
|---|---|---|
| Recruit (×0.7) | **PROBING ATTACK** | A feeler. No orbital bombardment |
| Officer (×1.0) | **DECAPITATION STRIKE** | Concentrated skyfall drop on your CAPITAL |
| General (×1.4) | **FULL INVASION** | Mass skyfall + ground landings on every flank |
| Apocalypse (×2.0) | **ORBITAL ANNIHILATION** | No landing at all — they glass the planet. Just survive |

There is also a **custom scenario editor** (`SCEN`) exposing prep time and difficulty.

`doSkyfall()` drops a mass wave right after the opening bombardment, plus ground landings on the
neglected flanks. `avoidCityCenter()` nudges landings to city outskirts — the autodrones assault
from the edges rather than dropping dead-centre.

---

## TERRAIN, INFRASTRUCTURE, MISC

- **Terrain editing** — `TERRAIN_MODES = ['', 'RAISE', 'LEVEL', 'SINK']`. You reshape the ground:
  raise land, flatten a buildable plateau, or sink it toward water.
- **Underground construction** — `UG_DEPTH = 50`. Some buildings auto-bury; rail tunnels run below.
- **Cities** have districts that can be individually damaged (`damageDistrict`), a capital flag,
  and their own self-defense tier (`BDEF`: tracer → rotary → plasma ball) upgraded by Rotary
  Cannon Works / Plasma Bastion Works.
- **Entrenchment** — dug-in, fortified ground units soak up to 60% of incoming damage.
- **Achievements** — the `ACHIEVEMENTS` array exists and is evaluated once per second, but is
  **empty**. The system is scaffolding only.
- **Audio** — fully procedural Web Audio SFX (`SFX.shoot/launch/boom/click/build/research/split/
  rip`) plus a **YouTube-backed music player** (`MUSIC`, `MUS`) with prep/combat playlists,
  crossfade, and ducking on major events.
- **Touch support** — drag to scroll, pinch to zoom, tap to select/place/designate, long-press to
  cancel. Strategic strikes take a first tap to aim then a `☢ CONFIRM` tap.
- **In-game manual** — a 9-section `<details>` reference covering controls, economy, production
  and air command, research, strikes, special resources, superweapons, fire control, and building
  costs.

---

## VERSION HISTORY (36 releases, v1.9.7 → v3.19.0)

`v1.9.7` Combat Overhaul · `v1.9.8` Fleet & Forge · `v1.9.9` Airburst & Arsenal ·
`v2.0.0` Special Economy · `v2.1.0` Ground & Fleet · `v2.2.0` Arsenal Expansion ·
`v2.3.0` Cities & Ordnance · `v2.4.0` Upgrades & Units · `v2.5.0` Production Lines ·
`v2.6.0` Immersion Pass · `v2.7.0` Combat & Invasion Overhaul · `v3.0.0` The 3.0 Update pt.2 ·
`v3.1.0` Doctrine Pass · `v3.1.1` Orbit & Re-entry · `v3.1.2` The Fleet Roster ·
`v3.2.0` Doctrine Balance · `v3.2.1` Fortify · `v3.3.0` Hostile Takeover ·
`v3.3.1` Bombardment Rules · `v3.4.0` The Tall Sky · `v3.5.0` Total War ·
`v3.6.0` The Living Sky · `v3.7.0` Take the Trigger · `v3.8.0` Production Chains ·
`v3.9.0` Fire Control · `v3.10.0` Defense & Destruction · `v3.11.0` Camera & UI ·
`v3.12.0` Deflector Tech Path · `v3.13.0` After-Action Report · `v3.13.1` Bug Fixes ·
`v3.14.0` Global Projectiles & Clearer Guns · `v3.15.0` Stability Economy Rework ·
`v3.16.0` Graphics Rework · `v3.17.0` Unit Graphics Rework ·
`v3.18.0` Air Command · Mobile · Accessibility · `v3.19.0` Confirmed Launch & A Readable Manual

**Note the shape of this list.** Getting the escalation ladder to feel good took 30+ iterative
balance passes on an engine a fifth the size of the rebuild. That is the real cost of "eras of
research with a sci-fi ceiling."

---

## WHAT IT DID RIGHT (the designer's own list)

1. **Sense of scale and exponential growth.** You cover the entire ground in factories and ports
   and turn the world into a fortress.
2. **Interesting research trees and units.** Massive, civilization-changing technologies — and it
   *still* didn't feel like enough. One of the best parts, because you rarely see games with
   crazy sci-fi of your own choosing.
3. **Satisfaction watching your army obliterate the invaders through sheer firepower, without
   micromanaging anything.** 9,000 units fighting off air attacks while your ground guns shoot
   everything down.
4. **Atmosphere and building tension.** Waiting for them to show up felt genuinely stressful
   because of everything you'd invested.

---

## WHAT IT DID WRONG (the designer's own list)

1. **Air units overpowered** — too strong relative to everything else, and dirt cheap once you
   had many airbases. Partially addressed in later versions.
2. **Ground units felt wonky** because they could only move along one axis. Hard to justify them
   when a single tungsten rod annihilated everything on the ground and sank a city.
3. **Too arcadey**, weapons behaved unrealistically, and the art style wasn't what was wanted.
4. **Slightly repetitive** because some balance was off.
5. **Limited-influence and stability felt stale** — you didn't feel the impact as much as you'd
   want.
6. **Mechanics didn't chain together properly.** Strategic resources especially ended up barely
   used compared to the role originally designed for them. Intended fix: make them grant bonuses
   rather than act as requirements.
7. **Stability loss as an instant defeat felt jarring** — "wow this is a great defense, oops, you
   lose." It should be a major setback that knocks you out for a while or degrades your units,
   not a sudden loss.

### Additional gaps identified later

- **Research combos only ever unlocked a unit.** With decision chains and news popups they could
  have produced genuinely large impacts on the world and on policy.
- **The scale and attack plan of the invasion was never properly explained** to the player.
- **The societal shift implied by "total mobilization" was never really represented** — society
  states are a sky-colour change and one stability modifier.
- **Fire Control didn't work well enough to matter.**

---

## RELATIONSHIP TO THE TOP-DOWN REBUILD

The rebuild (`claude/topdown-rebuild`) has the better *world* — a genuine 2D plane, real terrain,
projectile physics with ballistic arcs and homing missiles that have real turn rates and thrust,
procedurally generated cities. It structurally fixes v1's bad-list items 2 and 3.

**But it dropped almost everything on the good list.** It has six grounded near-future techs and
no sci-fi at all; a four-building prerequisite chain in front of a single missile launcher; no
prep countdown; no pause; no strategic strikes; no stability; and a deep raw→refined→component
supply chain that makes growth slow and conditional instead of exponential.

The intended direction is **this game, on that plane** — see `topdown/docs/ROADMAP.md` on the
rebuild branch.
