# NAVAL DOMINANCE — OPERATION (Build Execution Plan)

**Status:** v1 — the pre-flight for the batch build. Sits on top of `DESIGN.md` (v0.6) and
`FRAMEWORK_PLAN.md` (v3). This is the "actual order" — what gets built, by whom, in what
sequence, and how we know it's good. **No game code is written until this is approved.**

**Intent:** this is the load-bearing wall. Maximum rigor. Every phase ends in something
*runnable* and *judged*, never a pile of unproven code.

---

## 1. Final locked requirements (added this round)

1. **Wave-rock by mass** — ocean waves rock ships; **bigger ships rock less** (roll amplitude
   ∝ 1/mass). Owned by Movement & Locomotion + Juice.
2. **Domain-specific collision** — **ground units use ground/terrain collision**, distinct
   from naval buoyancy and air flight. One `domains` flag → three physics paths.
3. **Box-select + target designation** — RTS-style drag-box selection and target assignment
   from the tactical view. Owned by Input & Mode Stack + HUD.
4. **Audio = asset packs, never synthesized.** Source licensed/CC0 SFX packs; do not synth.
   (Synth "always feels wrong.")
5. **Projectiles are LONG STRETCHED TRAILS, never boxes.** Rendered as elongated tracer
   streaks (stretched billboards / trail geometry). This is non-negotiable feel.

Plus: **highly-detailed unit models** (≥ Navy Simulator tier), refined via the recursive
quality gate (§4), referenced against real photos & blueprints. Starter lineup: `ROSTER.md`.

---

## 2. Asset & fidelity policy (the honest core)

The rule, learned from the audio decision: **source, don't synthesize.** AI cannot reliably
author quality meshes or SFX from scratch — both come out "wrong." So:

- **Sound:** licensed / CC0 SFX packs, layered per the research (3-layer gunshots, sub-bass
  kick, per-caliber signatures). We *mix and layer* real samples; we do not generate them.
- **Models:** **asset-pack / CC0 base hulls + procedural detailing** (turrets, railings, guns,
  masts as parametric parts driven by `Unit = data`). NOT AI-generated geometry. This is how
  we actually reach Navy-Simulator tier while staying **sellable** (clean licensing).
- **Projectiles:** stretched streak/trail geometry (see req #5), never box+trail.
- **Reference-driven:** each ship is built against gathered **real photos + blueprints** of its
  type (patrol boat, destroyer, etc.). Research scouts (Haiku) collect references first.
- **Budget & fallback (from FRAMEWORK_PLAN v3):** single-file has an asset-size ceiling; if
  exceeded, split assets/data into lazy-loaded `data:`/external files rather than bloating one
  uneditable HTML. Licensing for every asset recorded in `ASSETS.md` (to be created) so a
  future sale is clean.

> **This is the one decision needing your confirmation.** Your recursive model loop is kept in
> full — it just evaluates *renders of real-asset-based ships against references*, not
> from-scratch AI geometry.

---

## 3. Build operation — phases as an execution checklist

Each phase: **Goal · Tasks · Definition of Done (DoD) · Gate.** Phases are sequential; tasks
*within* a phase that touch different frameworks can be parallel builder batches.

**Phase 0 — Foundation**
- Goal: a single-file Three.js page you can fly a camera through.
- Tasks: App Shell & Loop (ESM blob-import skeleton, music player, save/load) · Input & Mode
  Stack · Camera & Zoom (log depth, possession API) · Unit = Data (schema + loader).
- DoD: load a data-defined ship into a 3D ocean, zoom map↔deck, input routed cleanly.
- Gate: no visual jank on zoom; frame budget healthy.

**Phase 1a — TOY fun-check (earliest signal)**
- Goal: answer "does firing a gun feel good?" before building anything on top.
- Tasks: one take-overable turret · one static target · one layered gunshot (asset-pack) +
  hit-stop + **stretched-trail tracer** · barrel recoil synced to sound.
- DoD: you can possess the turret and fire in-browser.
- Gate: **YOU say the shot feels good.** If not, iterate here only.

**Phase 1 — VERTICAL SLICE (the bet)**
- Goal: DESIGN §14's 60 seconds — fun, playable.
- Tasks: Battle Engine (minimal single encounter) · Gunnery & Take-Over · Combat Math (both
  signatures stubbed, ballistic live) · Targeting (minimal) · Movement (player **Destroyer**
  with mass-scaled wave-rock + a couple enemy **Scout Helis**) · Juice/Sound + music · minimal
  HUD (info panel, take-over prompt).
- DoD: play a real fight; win/lose; the Destroyer's turrets are take-overable; planes to down.
- Gate: **fun.** If not fun, stop and fix before Phase 2.

**Phase 2 — Tactical depth**
- Tasks: statistical Combat-Math path + Auto-Resolve (odds readout + result animation) ·
  Unit/Fleet AI (damage-reservation targeting, threat priority, formations) · Targeting/fog ·
  full click-anything HUD · **box-select + target designation** · Crew Reactions (cosmetic,
  impostor/flipbook) · ground collision for land units.
- DoD: play OR auto-resolve; command multiple units via box-select; crews visibly react.

**Phase 3 — Campaign**
- Tasks: World & Map (panelized graph, islands, buildings, + Zone-of-Control supply) · Turn
  System (Production→Orders→Resolution) · Production & Garrisons + Industrial Capacity +
  garrison-to-hold.
- DoD: a small campaign that spawns battles into Phases 1–2.

**Phase 4 — Content pipeline + first content**
- Tasks: Content Pipeline (data formats/loaders) · full starter roster (`ROSTER.md`) · a
  ~12–16 zone first campaign + a couple of single-battle scenarios · `ASSETS.md` licensing log.
- DoD: a real, if small, game. Everything after this is CONTENT.

---

## 4. The recursive quality gate ("is it good? → if no, retry")

Applied to **both code/feel and models**. Bounded, and it escalates to you when stuck.

- **What "good" means** is defined *per task* before work starts (a checklist / reference set).
- **Who judges:**
  - *Feel* (combat, sound, juice): playtest + your verdict at the Phase 1a and Phase 1 gates.
  - *Models:* build → render → **vision review against real reference photos/blueprints** →
    refine → repeat. Fails escalate to you with side-by-side renders.
  - *Code:* a red-team review pass per phase (like the one that produced FRAMEWORK_PLAN v3).
- **Bounded loop:** up to a set number of iterations; if it can't pass, it stops and asks you
  rather than spinning. No infinite mutual-admiration loops.
- **Orchestrator (me, Opus 4.8) integrates**; workers do the passes.

---

## 5. Agent organization

- **Orchestrator:** me — Opus 4.8, never 5.0. Holds design context, writes/integrates, judges.
- **Research scouts:** Haiku (web search) — gather references (ship photos/blueprints, SFX
  pack sources, technique refs) *before* each build batch that needs them.
- **Builder batches:** worker agents implement one framework/task against its boundary
  (model doesn't matter per your call, as long as I stay 4.8). Parallel only across
  *non-adjacent* frameworks; sequential where one depends on another's interface.
- **Reviewer:** a fresh agent red-teams each phase's output.
- I supervise, integrate, and bring every gate decision to you.

---

## 6. Tech setup (Phase 0 concretes)

- Single-file `index`/`game.html`: inline `three.module.min.js` → `Blob` → `import()` (ESM,
  CSP-safe, no build step). Streamed music player ported from `title.html`.
- Repo layout under `naval-dominance/`: the game HTML, a `data/` set of unit/scenario JSON, an
  `assets/` policy (embedded or lazy per budget), and these planning docs.
- `GameState` root; systems register on a fixed-timestep loop; save/load = JSON of `GameState`
  (campaign + between-battles only; no mid-battle save in v1).

---

## 7. Honest scope

This is a **large, multi-phase build**, not a one-shot. That's *why* the phase gates and the
Phase 1a toy exist — we prove fun and quality early and often, and you can stop, redirect, or
bank a playable build at every gate. The framework-first approach means the slow part is
front-loaded once; content after Phase 4 is fast. Biggest live risks: model fidelity vs.
single-file budget (mitigated by §2 policy + fallback), and solo maintainability (mitigated by
strict boundaries).

---

## 8. Go / no-go — what I need from you to launch builders

1. **Confirm the asset/model policy (§2)** — source-don't-synthesize, asset-pack + procedural
   models, recursive loop on renders. (Or correct it.)
2. **Confirm the roster** (`ROSTER.md`) or edit it.
3. **Say "build"** — and I start at **Phase 0**, reporting at every gate.

Until then: still zero game code.
