# NAVAL DOMINANCE — Framework Build Plan

**Status:** v2 — the blueprint we approve *before* the batch. Companion to `DESIGN.md` (v0.6).
**v2 changes:** folded in four research scouts (naval combat feel/sound · fleet-AI & crew
illusion · Three.js/WebGL perf · TBS↔RTS integration & auto-resolve). Research-derived notes
are tagged **[R]** inline; full digest + sources in §F.
**Goal:** build the FRAMEWORK once, cleanly, so all CONTENT (ships, scenarios, domains,
eras) is just data slapped on. Converge on the **vertical slice** early — *fun before finished.*

---

## 0. How to read this

- **Frameworks** = the ~13 "build once" systems below. Each has a **job** and a **boundary**
  (interface others use). Boundaries are the point: systems talk through interfaces, never
  reach into each other's guts. That's what makes content cheap and refactors safe.
- **Build order** (§B) sequences them into phases; each phase ends runnable.
- **Non-negotiable:** a *unit, weapon, building, scenario, or era* is **data**, not code.

---

## A. The frameworks (build once)

**Job** — what it owns · **Boundary** — how others use it.

1. **App Shell & Loop**
   - Job: single-file HTML + Three.js scene, fixed-timestep loop, `GameState` root, save/load
     (JSON), **streamed music player** (port from Total Mobilization).
   - Boundary: owns canvas + `GameState`; others register as systems on the loop.
   - → **[R] Single-file gotcha:** Three.js is **ESM-only since ~r150** (no more `<script>`
     global build). Inline `three.module.min.js` as a string → `Blob` → `createObjectURL` →
     dynamic `import()`. CSP-safe (`script-src 'self' blob:`), zero network, no build step.
     Embed textures/audio as base64 `data:` URIs; keep normal maps 256–512px tileable.

2. **Camera & Zoom**
   - Job: one-world camera; strategic (top-down, far) ↔ tactical (close) as continuous zoom;
     input routed by zoom level; exposes `focusedEntity` + `zoomLevel`.
   - Boundary: read-only over world; emits focus/zoom events; nothing else moves the camera.
   - → **[R]** Enable `logarithmicDepthBuffer:true` (mandatory across map-km→deck-m range);
     keep `far` sane (huge far + flat ocean = horizon artifacts). Drive detail by
     **distance-gated LOD fade**, never a hard cut, so the zoom never pops. Floating origin
     likely unneeded (small world) — add only if deck-zoom jitter appears.

3. **Unit = Data**
   - Job: schema (DESIGN §11), loader, registry. Units/weapons/crew/secondaries/capabilities
     all in data files.
   - Boundary: single source of truth; everything reads units *through* the registry.

4. **World & Map (Campaign Graph)**
   - Job: flat top-down world, panelized zones as an **adjacency graph**, islands, buildings,
     ownership, garrison-to-hold state.
   - Boundary: owns territory state; exposes `zones/adjacency/ownerOf/buildingsOn`.
   - → **[R] Zone of Control:** occupying/adjacency should let fleets **cut supply without
     direct combat** (Unity of Command's signature "aha") — telegraphed in advance so prepared
     players see the cut-off coming and it feels earned, not random.

5. **Turn System**
   - Job: loop **Production → Orders → Resolution → end**; order queue; end-turn sequencing.
   - Boundary: orchestrates other systems in phase order; holds no combat/production math.
   - → **[R]** Deliberately **decouple time scales** (campaign = long, battle = minutes) — the
     mismatch is *why* the hybrid works. Prep lives in the Orders phase as active decisions.

6. **Production & Garrisons**
   - Job: per-turn garrison floor (free), **Industrial-Capacity** accrual from territory,
     IC-spending build queues at capable buildings.
   - Boundary: reads World/buildings + IC; writes units via the Unit registry. Never renders.

7. **Battle Engine (real-time, tactical)**
   - Job: given two fleets + a location, spawn the engagement into the one world and run it
     real-time (movement, orders, firing). Hosts **dual-fidelity** combat.
   - Boundary: input `{fleetA,fleetB,location,playerControlled?}` → `Outcome`. Uses Gunnery +
     Combat Math.

8. **Gunnery & Take-Over**
   - Job: turret aiming (auto-aim solution + manual), **click-a-turret possession**,
     difficulty assist levels.
   - Boundary: operates on `unit.weapons`; driven by Camera focus + input; asks Combat Math.
   - → **[R] Possession moment** (War Thunder / Space Engineers pattern): on take-over, snap
     camera to the gunsight, **swap HUD to a red→green lead-indicator reticle** (red = no
     firing solution, green = lead+travel solved), and play a mechanical "clunk". Realism mode
     strips the assist (manual lead). Barrel recoil animation synced to the sound transient.

9. **Combat Math (shared) — load-bearing**
   - Job: the ONE hit/damage model used by BOTH the live ballistic path and the statistical
     path, so playing vs. auto-resolving stay consistent.
   - Boundary: pure functions `hitChance(shooter,target,weapon,conditions)`, `applyDamage(...)`.
   - → **[R]** Hit chance = **cone-of-error tied to crew-skill stat** (not binary). Auto-resolve
     MUST derive from these *same per-unit terms* — **fortification/terrain/supply weigh
     equally in both paths** (TW's #1 failure: defenses barely count in auto-resolve, so
     defenders never trust it).

10. **Auto-Resolve**
    - Job: run a battle to `Outcome` via the statistical path only (no render), using Combat Math.
    - Boundary: same input/output contract as Battle Engine (§7) — interchangeable.
    - → **[R] Never a black box.** Show a **pre-battle odds readout** listing the same visible
      modifiers (terrain, fortification tier, flanking, supply) the manual fight uses, then a
      **fast legible result animation** (HP tick-down, death markers) so it reads as *computed,
      not RNG*. Distrust is the default when players can't reason about inputs.

11. **Juice, Sound & FX**
    - Job: muzzle flash, smoke, tracers, splashes, screen shake, sound bus, music ducking.
      Event-driven.
    - Boundary: systems emit events ("shotFired"/"hit"/"sunk"); this renders/plays. No gameplay
      logic.
    - → **[R] Sound (the #1 priority):** 3-layer gunshot = **transient crack + sub-bass body +
      mechanical tail**; add a felt **sub-bass "kick" (50–200Hz)** under big guns; blend
      non-gun material per caliber so guns are ID-able by ear; **distance shifts timbre** (far
      = only low rumble), not just volume; separate cues for fire/travel/impact/reload; stagger
      multi-barrel timing. **Exaggerate over realism** (WoWS abandoned pure realism on purpose).
    - → **[R] Visual:** camera kick *opposite* the shot; **hit-stop 60–80ms freeze** on
      confirmed hits/kills; tracers (juice *and* manual-aim feedback); **exaggerated near-miss
      shell splashes** (bracket-and-correct read); persistent smoke/scorch = cumulative damage
      at a glance; **screen shake scaled to caliber** (weapon-class identity via feel).

12. **Crew Reactions**
    - Job: zoom-gated, instanced, stylized crew on the **focused** ship only; react to signals
      (incoming fire / kill / fire-on-deck) via canned anim + barks.
    - Boundary: active only when a ship is focused & close; reads battle events; never affects
      sim outcome (crew *skill* lives in data).
    - → **[R]** "**Reactions > cognition**" — players read a panicked flinch as intelligence far
      more than actual smart pathing, so spend budget on reaction juice. Render via **billboard
      impostors / animation-texture flipbooks** (no skeletons); 3–4 anim variants + bark library
      is enough perceived variety. Crew improve via **FTL-style task-tied XP** (gunners gain
      skill by firing).

13. **UI / HUD Shell + Content Pipeline**
    - Job (HUD): click-anything inspection (ship/gun/crew/building), the info panel (DESIGN §6),
      drag-arrow order previews, weapon-type icons, strategic map UI.
    - Job (Pipeline): data formats + loaders to add a unit/building/scenario/campaign.
    - Boundary: HUD reads state, issues orders through Turn/Battle. Pipeline feeds Unit registry
      + World + scenario loader.

---

## B. Build order (each phase ends runnable)

**Phase 0 — Foundation.** App Shell & Loop · Camera & Zoom · Unit = Data. → Load a
data-defined ship into a 3D world, zoom map↔deck. Nothing fights yet.

**Phase 1 — VERTICAL SLICE (the fun test) ← the whole bet.** Battle Engine (ballistic/close) ·
Gunnery & Take-Over · Combat Math · Juice/Sound + streamed Music · minimal HUD. Content: 1
player ship (2–3 take-overable turrets) + a couple of enemy aircraft. → **DESIGN §14's 60
seconds. If it isn't fun, we stop and fix it before building anything else.**

**Phase 2 — Tactical depth.** Statistical path + Auto-Resolve (shared Combat Math, with the
odds readout + result animation) · full click-anything HUD · Crew Reactions. → Play OR
auto-resolve, fully inspectable, with the crew-illusion soul.

**Phase 3 — Campaign.** World & Map graph (+ ZOC supply) · Turn System · Production & Garrisons
+ Industrial Capacity + garrison-to-hold. → A playable small campaign feeding Phases 1–2.

**Phase 4 — Content pipeline + first content.** Content Pipeline · starter roster (patrol
boats, small helis, tanks, mortars) · a ~12–16 zone first campaign + a couple of single
battles. → A real, if small, game.

**Everything after Phase 4 is CONTENT.** No new framework required — the win condition.

---

## C. Decisions this plan bakes in

One 3D world, Three.js/WebGL, camera-as-UI, flat top-down map (From the Depths) · seamless
zoom but real-time physics only for the active engagement · Industrial Capacity + free
garrison floor · holding territory requires a garrison · **dual-fidelity combat via one shared
Combat-Math module** · **auto-resolve transparent & consistent with manual** · difficulty =
realism spectrum · ~12–16 zones; turn loop Production→Orders→Resolution · single-player (sim
kept clean so MP stays possible).

## D. Explicitly NOT in this batch

Content beyond starter roster · multiplayer · rotating globe · fantasy/ISOT/ground-air beyond
proving the data path · deep component-damage sim · full simulated-soul crew. Parked in
DESIGN §18. None cut.

---

## E. Process — how this plan gets hardened

- Orchestrator (Opus 4.8) holds design context, writes/edits. Delegation approved.
- Research scouts (Haiku for web search going forward) gather references → fold into the plan.
- **Critique pass:** a fresh reviewer red-teams this plan against `DESIGN.md`; orchestrator
  integrates real findings; bounded rounds (harden, don't rubber-stamp).
- Orchestrator stays 4.8 (never Opus 5.0). Worker model otherwise doesn't matter.

---

## F. Research digest (v2) — key findings & sources

**Naval combat feel & sound** — 3-layer gunshot (transient/sub-bass body/tail); felt sub-bass
kick for big guns; per-caliber sonic signatures (ID by ear); exaggerate over realism (WoWS);
distance timbre shift; separate fire/travel/impact/reload cues; staggered multi-barrel timing.
Visual: opposite-direction camera kick, 60–80ms hit-stop, tracers, exaggerated near-miss
splashes, persistent smoke, caliber-scaled shake. Possession: camera-to-gunsight + red→green
lead reticle + "clunk". · WoWS sound (Making Games), Splice/Game Audio Co./tbirdsound weapon
sound, Vlambeer "Art of Screenshake", War Thunder & Space Engineers manual-turret leads.

**Fleet AI & crew illusion** — damage-reservation targeting (no overkill); threat-priority
chooser (not nearest); formation-as-flocking; utility-scores-picks + small-BT/FSM-executes;
kite only with speed+range edge. Imperfect-but-human AI via "11 Ways to Be Stupid" + cone-of-
error accuracy stat; FTL task-tied crew XP. Crew illusion: canned event-triggered anims,
"reactions > cognition", impostor/flipbook rendering, InstancedMesh on focused ship only. ·
slashskill RTS combat guide, GameAIPro, gamedeveloper.com AI articles, FTL wiki.

**Three.js / WebGL perf** — `THREE.Water` baseline; Gerstner vertex waves only near focus
(scrolling normal map far); `logarithmicDepthBuffer` for the zoom range; `InstancedMesh` +
object pooling for ships/shells; sprite/billboard tracers at range; **ESM-only three.js →
blob-import for single-file**; base64 data-URI assets. · three.js docs/examples, Franky Hung
water write-up, threejsroadmap draw-calls, impostor crowd-rendering paper.

**TBS↔RTS integration & auto-resolve** — decouple campaign/battle time scales; pre-battle
build phase with spendable defense resource (Warhammer sieges); **auto-resolve never a black
box, derive from same combat terms, weight fortification equally**; fast legible result
animation; Zone-of-Control supply cutting (Unity of Command); fortification changes *what you
bring*, not just a multiplier; terrain affects engagement width (HOI4); named visible spoils;
avoid cliff-edge war-weariness. · Total War (GDC "Designing Grand Strategy"), Sins of a Solar
Empire, TWCenter auto-resolve threads, Unity of Command case study, gamedeveloper.com economy.

> Method note: this session's web egress is allowlisted, so scouts worked from WebSearch result
> snippets (not the AI-overview blurbs, and not full-page fetches). The GDC "Designing Grand
> Strategy" talk and the Unity of Command case study are worth a direct read when reachable.

```
This file is the thing to approve. Once solid, the batch builds Phase 0 → 1 first.
```
