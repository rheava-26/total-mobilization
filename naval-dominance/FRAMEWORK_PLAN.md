# NAVAL DOMINANCE — Framework Build Plan

**Status:** v1 — the blueprint we approve *before* the batch. Companion to `DESIGN.md` (v0.6).
**Goal:** build the FRAMEWORK once, cleanly, so all CONTENT (ships, scenarios, domains,
eras) is just data slapped on afterward. Converge on the **vertical slice** as early as
possible so the game is *fun before it is finished.*

---

## 0. How to read this

- **Frameworks** = the ~13 "build once" systems below. Each has a **job** and a **boundary**
  (what it may touch, and the interface others use). Boundaries are the whole point: systems
  talk through interfaces, never reach into each other's guts. That's what makes content
  cheap and refactors safe.
- **Build order** (§B) sequences them into phases; each phase ends at something runnable.
- **Non-negotiable rule:** if a thing is a *unit, weapon, building, scenario, or era*, it is
  **data**, not code. Code never hard-codes a ship.

---

## A. The frameworks (build once)

Each: **Job** — what it owns · **Boundary** — how others use it.

1. **App Shell & Loop**
   - Job: single-file HTML + Three.js scene, fixed-timestep update/render loop, global game
     state root, save/load (JSON ↔ localStorage/file), **streamed music player** (port the
     working one from Total Mobilization).
   - Boundary: owns the canvas + `GameState`. Everything else is a system that reads/writes
     `GameState` and registers into the loop.

2. **Camera & Zoom**
   - Job: the one-world camera; strategic (top-down, far) ↔ tactical (close, angled) as a
     continuous zoom; routes input differently by zoom level; exposes `focusedEntity` and
     `zoomLevel`.
   - Boundary: read-only over world state; emits focus/zoom events. No other system moves
     the camera directly.

3. **Unit = Data**
   - Job: the schema (DESIGN §11), loader, and registry. Units, weapons, crew, secondaries,
     capabilities all defined in data files.
   - Boundary: the single source of truth for unit definitions. Everything reads units
     *through* this registry; nothing else parses unit files.

4. **World & Map (Campaign Graph)**
   - Job: flat top-down world, panelized zones as an **adjacency graph**, islands, buildings,
     ownership, garrison-to-hold state.
   - Boundary: owns territory state. Exposes `zones`, `adjacency`, `ownerOf`, `buildingsOn`.
     Turn system drives it; battle system reads it for location/context.

5. **Turn System**
   - Job: the loop **Production → Orders → Resolution → end**; order queue; end-turn
     resolution sequencing (advance builds, move fleets, trigger battles, resolve capture).
   - Boundary: orchestrates other systems in phase order; holds no combat/production math
     itself — it *calls* Production, Movement, Battle, Auto-resolve.

6. **Production & Garrisons**
   - Job: per-turn garrison output (free floor), **Industrial-Capacity** accrual from
     territory, IC-spending build queues at capable buildings.
   - Boundary: reads World/buildings + IC; writes new units into fleets via the Unit
     registry. Never renders.

7. **Battle Engine (real-time, tactical)**
   - Job: given two fleets + a world location, spawn the engagement into the one world and
     run it real-time: ship movement, order execution, firing. Hosts **dual-fidelity**
     combat (§ Battle detail).
   - Boundary: input = `{fleetA, fleetB, location, playerControlled?}`; output = an
     `Outcome` (survivors, damage, victor). Uses Gunnery + shared hit-math.

8. **Gunnery & Take-Over**
   - Job: turret aiming (auto-aim solution + manual control), **click-a-turret possession**,
     difficulty-as-realism assist levels, firing solutions feeding the hit-math.
   - Boundary: operates on `unit.weapons`; driven by Camera focus + input; asks Combat-Math
     for hit results.

9. **Combat Math (shared)**
   - Job: the ONE hit/damage model used by both the live ballistic path (close) and the
     statistical path (far / auto-resolve), so playing vs. auto-resolving stay consistent.
   - Boundary: pure functions. `hitChance(shooter,target,weapon,conditions)`,
     `applyDamage(...)`. No rendering, no state ownership.

10. **Auto-Resolve**
    - Job: run a battle to an `Outcome` with the statistical path only (no render), using
      Combat Math. Rewards preparation on the numbers.
    - Boundary: same input/output contract as Battle Engine (§7) so they're interchangeable.

11. **Juice, Sound & FX**
    - Job: muzzle flash, smoke, tracers, hit sparks, water spray, screen shake, sound bus,
      music ducking. Event-driven.
    - Boundary: systems *emit events* ("shotFired", "hit", "sunk"); this listens and
      renders/plays. No gameplay logic here.

12. **Crew Reactions**
    - Job: zoom-gated, instanced, stylized crew on the **focused** ship only; react to a few
      signals (incoming fire / kill / fire-on-deck) via canned anim + barks.
    - Boundary: active only when a ship is focused & close; reads battle events; renders
      cheap figures. Pure illusion, never affects sim outcome (crew *skill* lives in data).

13. **UI / HUD Shell + Content Pipeline**
    - Job (HUD): click-anything inspection (ship / gun / crew / building), the info panel
      (DESIGN §6), drag-arrow order previews, weapon-type icons, strategic map UI.
    - Job (Pipeline): the data formats + loaders for adding a unit / building / scenario /
      campaign — the "slap on forever" enabler.
    - Boundary: HUD reads state, issues orders through Turn/Battle. Pipeline feeds the Unit
      registry + World + scenario loader.

---

## B. Build order (each phase ends runnable)

**Phase 0 — Foundation (skeleton you can fly a camera through)**
App Shell & Loop · Camera & Zoom · Unit = Data. → You can load a data-defined ship into a
3D world and zoom from map-height to deck-height. Nothing fights yet.

**Phase 1 — VERTICAL SLICE (the fun test) ← the whole bet**
Battle Engine (ballistic/close path) · Gunnery & Take-Over · Combat Math · Juice/Sound +
streamed Music · minimal HUD. Content: 1 player ship (2–3 take-overable turrets) + a couple
of enemy aircraft. → **The 60 seconds from DESIGN §14. If this isn't fun, we stop and fix
it before building anything else.**

**Phase 2 — Tactical depth**
Statistical path + Auto-Resolve (shared Combat Math) · full click-anything HUD · Crew
Reactions. → A battle you can play hands-on *or* auto-resolve, fully inspectable, with the
crew-illusion soul.

**Phase 3 — Campaign**
World & Map graph · Turn System (Production→Orders→Resolution) · Production & Garrisons +
Industrial Capacity + garrison-to-hold. → A playable small campaign feeding battles into
Phases 1–2.

**Phase 4 — Content pipeline + first content**
Content Pipeline · starter roster (patrol boats, small helis, tanks, mortars) · a ~12–16
zone first campaign + a couple of single-battle scenarios. → A real, if small, game.

**Everything after Phase 4 is CONTENT** (more ships, domains, eras, airships, fantasy,
ISOT). No new framework required — that's the win condition of this plan.

---

## C. Decisions this plan bakes in (from the design convo)

- One 3D world, Three.js/WebGL, camera-as-UI, flat top-down map (From the Depths).
- **Seamless zoom, but real-time physics only for the engagement you're in** (rest = turn state).
- **Industrial Capacity** (territory-driven, per-turn) + free garrison floor for big vs. small builds.
- **Holding territory requires a garrison.**
- **Dual-fidelity combat** via one shared Combat-Math module (ballistic close / statistical far).
- Difficulty = realism spectrum (auto-aim assist → manual).
- ~12–16 zones first campaign; turn loop Production → Orders → Resolution.
- Single-player; multiplayer parked but the sim is kept clean so it stays possible.

## D. Explicitly NOT in this batch

Content beyond the starter roster · multiplayer · rotating globe · fantasy/ISOT/ground-air
domains beyond proving the data path · deep component-damage sim · full simulated-soul crew.
All parked in DESIGN §18. None cut.

---

## E. Process — how this plan gets hardened

- Orchestrator (Opus 4.8) holds the design context and writes/edits.
- **Research agents** ("the front") gather concrete references on naval combat feel, TBS↔RTS
  integration/auto-resolve, Three.js one-world zoom & WebGL perf, and cheap living-crew +
  smart-AI patterns — findings fold back into this plan.
- **Critique pass:** a fresh reviewer red-teams the plan against DESIGN.md; the orchestrator
  integrates; repeat for a *bounded* couple of rounds (enough to harden, not to rubber-stamp).
- **Any agent that writes changes runs on 4.8, not 5.0.**
```
This file is the thing to approve. Once it's solid, the batch builds Phase 0 → 1 first.
```
