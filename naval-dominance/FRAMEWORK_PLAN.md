# NAVAL DOMINANCE — Framework Build Plan

**Status:** v3 — hardened against a red-team pass. Companion to `DESIGN.md` (v0.6).
**Goal:** build the FRAMEWORK once, cleanly, so all CONTENT (ships, scenarios, domains, eras)
is just data slapped on. Converge on the **vertical slice** early — *fun before finished.*

**v3 changes (from critique):** specified the shared-Combat-Math contract; fixed the
Camera/Gunnery boundary; added four missing frameworks (Input, Targeting/Sensors, Movement,
Unit/Fleet AI); moved crew skill/XP out of the cosmetic Crew system; added an earlier **Phase
1a toy** fun-check; scoped out mid-battle save; added a single-file asset budget + fallback.
Research notes from the four scouts are tagged **[R]**; digest + sources in §F.

---

## 0. How to read this

- **Frameworks** = the "build once" systems in §A. Each has a **job** and a **boundary**
  (interface others use). Boundaries are the point — systems talk through interfaces, never
  reach into each other's guts. That's what makes content cheap and refactors safe.
- **Build order** (§B) sequences them into phases; each phase ends runnable.
- **Non-negotiable:** a *unit, weapon, building, scenario, or era* is **data**, not code.

---

## A. The frameworks (build once)

**Job** — what it owns · **Boundary** — how others use it.

1. **App Shell & Loop** — Job: single-file HTML + Three.js scene, fixed-timestep loop,
   `GameState` root, save/load, **streamed music player** (port from Total Mobilization).
   Boundary: owns canvas + `GameState`; others register as loop systems.
   - **[R]** Three.js is **ESM-only (~r150+)** → inline `three.module.min.js` as string → `Blob`
     → `import()` (CSP-safe, no build). Assets as base64 `data:` URIs.
   - **Save scope (v3):** campaign snapshot + between-battles only. **No mid-battle save in
     v1** (live projectiles/AI/anim state aren't worth serializing yet) — stated, not implied.
   - **[R] Asset budget:** hard ceiling (target ≤ a few MB single file; `title.html` is already
     ~538KB). **Fallback if exceeded:** split data/assets into lazy-loaded `data:`/external
     files rather than bloating one uneditable HTML.

2. **Input & Mode Stack** *(new — was homeless)* — Job: the single arbiter for mouse/keyboard;
   a **mode stack** (strategic / tactical / turret-possessed / inspecting) that decides what a
   click/drag means right now. Boundary: raw input in → intent events out; every other system
   subscribes to intents, none reads raw input. Kills the drag-arrow / possession-look /
   click-inspect / zoom-drag collisions.

3. **Camera & Zoom** — Job: one-world camera; strategic↔tactical as continuous zoom; exposes
   `focusedEntity`, `zoomLevel`, and **`requestPossessionView(entity)`** (fix: Gunnery asks
   the camera, never moves it directly). Boundary: read-only over world; emits focus/zoom.
   - **[R]** `logarithmicDepthBuffer:true`; keep `far` sane; **distance-gated LOD fade** (no
     hard cut); floating origin only if deck-zoom jitter appears.

4. **Unit = Data** — Job: schema (DESIGN §11), loader, registry; **owns crew skill & XP**
   (fix: progression is data, not cosmetic). Boundary: single source of truth; everything reads
   units through the registry.

5. **World & Map (Campaign Graph)** — Job: flat top-down world, panelized zones as adjacency
   graph, islands, buildings, ownership, garrison-to-hold. Boundary: owns territory; exposes
   `zones/adjacency/ownerOf/buildingsOn`.
   - **[R] Zone of Control:** adjacency lets fleets **cut supply without combat** (Unity of
     Command), telegraphed so being cut off feels earned.

6. **Turn System** — Job: loop **Production → Orders → Resolution → end**; order queue.
   Boundary: orchestrates phases; holds no combat/production math.
   - **[R]** Decouple time scales (campaign long, battle minutes); prep lives in Orders.

7. **Production & Garrisons** — Job: free per-turn garrison floor, **Industrial-Capacity**
   accrual from territory, IC build queues at capable buildings. Boundary: reads World/IC,
   writes units via registry; never renders.

8. **Movement & Locomotion** *(new)* — Job: how things move — **ships** (graph/heading, swell
   bob) vs **aircraft** (3D dive/strafe/evade) are different problems, owned here with a common
   interface. Boundary: consumes orders + AI intents, writes transforms; the slice's enemy
   planes live here.

9. **Targeting & Sensors** *(new)* — Job: what each weapon/ship can **see and legally shoot**
   (range, LOS, radar, later fog-of-war); the valid-target provider both combat fidelities
   depend on. Boundary: `visibleTargets(unit)`, `canEngage(weapon,target)`; no damage logic.

10. **Battle Engine (real-time)** — Job: given an encounter (fleets or a single ship + attackers)
    + location, spawn it into the one world and run real-time (orders, movement, firing). Hosts
    dual-fidelity combat. Boundary: `{sideA,sideB,location,playerControlled?}` → `Outcome`.
    - **Scope (v3):** Phase 1 builds the **minimal single-encounter** path; the fleet-vs-fleet
      generalization comes only after fun is proven.

11. **Gunnery & Take-Over** — Job: turret aiming (auto-aim + manual), **click-a-turret
    possession**, difficulty assist levels. Boundary: operates on `unit.weapons`; driven by
    Input intents; calls `Camera.requestPossessionView`; asks Combat Math for results.
    - **[R] Possession moment:** camera to gunsight + **red→green lead reticle** (red = no
      solution, green = lead+travel solved) + mechanical "clunk"; realism mode strips assist;
      recoil synced to the sound transient.

12. **Combat Math (shared) — load-bearing, now specified**
    - Job: the shared **damage + modifier** model, plus **two hit-determination modes** that
      feed it:
      - **Ballistic (manual/close):** real projectile vs. `Targeting` hitboxes; *player aim*
        replaces the crew-skill term.
      - **Statistical (auto / crew-run):** `hitChance(shooter,target,weapon,conditions)` =
        cone-of-error from **crew skill + range + weapon + target vulnerability + fortification/
        terrain/supply**.
    - **The shared part** is `applyDamage(...)` and the modifier terms — identical in both
      paths. **[R] Auto-resolve must weight fortification/terrain/supply equally** to manual
      (Total War's #1 trust failure was defenses barely counting).
    - Boundary: pure functions. **Stub BOTH signatures in Phase 1** even though only ballistic
      runs, so the module is shaped for both callers from day one (fix: no Phase-2 retrofit).

13. **Auto-Resolve** — Job: run an encounter to `Outcome` via the statistical path only (no
    render), using Combat Math. Boundary: same contract as Battle Engine (interchangeable).
    - **[R] Never a black box:** pre-battle **odds readout** showing the same visible modifiers,
      then a **fast legible result animation** (HP tick-down, death markers) so it reads as
      computed, not RNG.

14. **Unit/Fleet AI** *(new — was only in the digest)* — Job: how AI ships/planes decide —
    target selection, formations, evasion, when to commit/retreat. Boundary: reads
    World/Targeting/Unit state, emits **orders/intents** to Movement & Gunnery; owns no
    transforms or damage.
    - **[R]** **Damage-reservation targeting** (no overkill); **threat-priority chooser** (not
      nearest); **formation-as-flocking**; **utility-scores-pick + small BT/FSM-executes**;
      "**11 Ways to Be Stupid**" + cone-of-error for human-feeling imperfect AI.

15. **Juice, Sound & FX** — Job: flash, smoke, tracers, splashes, shake, sound bus, music
    ducking. Event-driven. Boundary: systems emit events; this renders/plays; no gameplay logic.
    - **[R] Sound (#1):** 3-layer gunshot (transient crack + sub-bass body + mechanical tail);
      felt **sub-bass kick** for big guns; per-caliber signatures (ID by ear); **distance shifts
      timbre**, not just volume; separate fire/travel/impact/reload cues; stagger multi-barrel
      timing; **exaggerate over realism** (WoWS).
    - **[R] Visual:** camera kick opposite the shot; **60–80ms hit-stop** on hits/kills; tracers
      (juice + aim feedback); exaggerated near-miss splashes; persistent smoke/scorch;
      **shake scaled to caliber**.

16. **Crew Reactions** *(cosmetic only)* — Job: zoom-gated, instanced, stylized crew on the
    **focused** ship; react to signals (incoming fire/kill/fire) via canned anim + barks.
    Boundary: focused-ship-only; reads battle events; **never affects sim outcome** (skill/XP
    lives in Unit=Data, §4).
    - **[R]** "**Reactions > cognition**"; **billboard impostors / animation-texture flipbooks**
      (no skeletons); 3–4 anim variants + bark library.

17. **UI / HUD Shell + Content Pipeline** — Job (HUD): click-anything inspection
    (ship/gun/crew/building), info panel (DESIGN §6), drag-arrow order previews, weapon-type
    icons, strategic map UI. Job (Pipeline): data formats + loaders to add
    unit/building/scenario/campaign. Boundary: HUD reads state, issues orders via
    Input→Turn/Battle; Pipeline feeds Unit registry + World + scenario loader.

---

## B. Build order (each phase ends runnable)

**Phase 0 — Foundation.** App Shell & Loop · Input & Mode Stack · Camera & Zoom · Unit = Data.
→ Load a data-defined ship into a 3D world, zoom map↔deck, input routed cleanly.

**Phase 1a — TOY FUN-CHECK (new, earliest possible signal).** One take-overable turret, one
static target, one juicy 3-layer gunshot + hit-stop + tracer. No zoom, no fleet, no AI. →
**Does firing a gun feel good?** If not, fix feel here before building anything on top of it.

**Phase 1 — VERTICAL SLICE (the bet).** Battle Engine (minimal single-encounter) · Gunnery &
Take-Over (via Camera API) · Combat Math (both signatures stubbed, ballistic live) · Targeting
(minimal) · Movement (player ship + simple enemy plane) · Juice/Sound + streamed Music ·
minimal HUD. Content: 1 ship (2–3 turrets) + a couple of enemy aircraft. → **DESIGN §14's 60
seconds.** If it isn't fun, stop and fix before proceeding.

**Phase 2 — Tactical depth.** Statistical Combat-Math path + Auto-Resolve (odds readout +
result animation) · Unit/Fleet AI · Sensors/fog · full click-anything HUD · Crew Reactions.

**Phase 3 — Campaign.** World & Map (+ ZOC) · Turn System · Production & Garrisons + Industrial
Capacity + garrison-to-hold.

**Phase 4 — Content pipeline + first content.** Content Pipeline · starter roster (patrol
boats, small helis, tanks, mortars) · ~12–16 zone first campaign + a couple of single battles.

**Everything after Phase 4 is CONTENT** — no new framework required. The win condition.

---

## C. Decisions this plan bakes in

One 3D world, Three.js/WebGL, camera-as-UI, flat top-down map (From the Depths) · seamless zoom
but real-time physics only for the active encounter · Industrial Capacity + free garrison floor
· holding territory requires a garrison · **dual-fidelity combat: shared damage/modifiers, two
hit modes; auto-resolve transparent & equally-weighted** · difficulty = realism spectrum ·
~12–16 zones; Production→Orders→Resolution · single-player (sim kept clean so MP stays possible)
· **no mid-battle save in v1** · single-file with an asset budget + external-data fallback.

## D. Explicitly NOT in this batch

Content beyond starter roster · multiplayer · rotating globe · fantasy/ISOT/ground-air beyond
proving the data path · deep component-damage sim · full simulated-soul crew · mid-battle save.
Parked in DESIGN §18. None cut.

---

## E. Process

Orchestrator (Opus 4.8, never 5.0) holds design context & writes. Research scouts (Haiku for
web search) gather → fold in. Critique pass red-teams vs `DESIGN.md`; orchestrator integrates
real findings (v3 did one round). Worker model otherwise doesn't matter.

---

## F. Research digest (scouts) — key findings & sources

**Combat feel & sound** — 3-layer gunshots, felt sub-bass kick, per-caliber signatures,
exaggerate-over-realism (WoWS), distance timbre shift, separate cue layers, staggered barrels;
opposite-kick camera, 60–80ms hit-stop, tracers, exaggerated splashes, caliber-scaled shake;
possession = gunsight + red→green lead reticle + clunk. · WoWS (Making Games), Splice/Game
Audio Co./tbirdsound, Vlambeer "Art of Screenshake", War Thunder & Space Engineers manual turrets.

**Fleet AI & crew illusion** — damage-reservation targeting, threat-priority chooser,
formation-flocking, utility+BT, kite-with-edge; "11 Ways to Be Stupid" + cone-of-error; FTL
task-tied crew XP; reactions>cognition; impostor/flipbook crew, InstancedMesh on focused ship. ·
slashskill RTS guide, GameAIPro, gamedeveloper.com, FTL wiki.

**Three.js / WebGL perf** — `THREE.Water` baseline, Gerstner near-focus only, `logarithmic
DepthBuffer`, InstancedMesh + pooling, billboard tracers at range, **ESM-only → blob-import**,
base64 assets. · three.js docs/examples, Franky Hung water, threejsroadmap, impostor paper.

**TBS↔RTS & auto-resolve** — decouple time scales; pre-battle build phase w/ spendable defense
resource; **auto-resolve transparent, shared terms, fortification weighted equally**; fast
result animation; **Zone-of-Control supply cutting**; fortification changes composition; terrain
sets engagement width (HOI4); named spoils; taper war-weariness. · Total War (GDC "Designing
Grand Strategy"), Sins of a Solar Empire, TWCenter, Unity of Command, gamedeveloper.com economy.

> Egress was allowlisted; scouts used WebSearch snippets (not AI-overview blurbs, not full
> fetches). The GDC "Designing Grand Strategy" talk and Unity of Command case study are worth a
> direct read when reachable.

```
This is the version worth approving. On approval, the batch builds Phase 0 → 1a → 1.
```
