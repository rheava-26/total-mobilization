# NAVAL DOMINANCE — ROADMAP & IDEA LOG

**Purpose:** the living catch-all for ideas, direction, and open work — so nothing gets lost
in chat scrollback and a clean sendoff is always one file away. New ideas the dev mentions get
**appended here (dated)** as they come up. Companion to `DESIGN.md` / `FRAMEWORK_PLAN.md` /
`OPERATION.md` (the settled design) — this file is the *raw stream* that feeds them.

Maintained by: Claude (auto-appends any new idea the dev raises). Last updated: 2026-08-17.

---

## 💡 Idea log (newest first)

### 2026-08-17 (later — the parallel fleet batch)
- **Synth-generated sounds APPROVED** (dev reversed the earlier packs-only rule): "fuck it, we
  can have synth generated sounds." `sfx.js` is now a layered procedural WebAudio bank.
- **Parallel agent batch delivered:** 18 new real warship classes across `fleet-heavy.js`
  (Fletcher, Gearing, Iowa, Ticonderoga, Perry, Atlanta, Essex carrier, oiler) and
  `fleet-light.js` (PT boat, LCS, minesweeper, missile corvette, landing craft, submarine,
  Seahawk, Cobra, seaplane, patrol bomber) → 25 spawnable unit types total.
- **24-zone theater map** (enemy northern reach → contested archipelago → player home waters,
  fortress zones + rich harbors).
- **`NavalDominance.html` standalone build** (`node build.mjs`): one double-clickable file that
  works from file:// via blob-URL module embedding. Rebuild it after any game.html/module edit.

### 2026-08-17
- **"This plays like Risk."** The campaign layer is genuinely Risk-shaped: a graph of
  territories, each zone garrisons/produces units every turn, you move + attack across a big
  map. **Our twist:** Risk resolves fights with dice; we resolve them with *either* an
  auto-roll *or* dropping into a real-time 3D battle you actually play.
  - *Directions to explore:* unit **stacks** that move zone-to-zone; **reinforcement/supply
    lines** (ties to the chokepoint/Zone-of-Control idea in DESIGN); fronts that shift.
  - *Design tension to watch:* lean into the Risk-like strategic map **without** getting so
    abstract that the naval combat — the soul — becomes an afterthought. The battles must stay
    the star; the map is the reason to fight them.

---

## 🔧 Open polish / debt (known, none structural)

1. **Ship graphics — TOP PRIORITY.** Dev is (rightly) unhappy with current procedural ships.
   Path: a dedicated art sprint — deeper procedural detailing against real reference photos
   (OPERATION §4 render→compare→refine loop), or drop in real/CC0 modeled hulls. Lock one ship
   to a quality bar, then match all others to it.
2. **Audio.** Still a WebAudio *placeholder* synth. Swap in a layered CC0 gunfire pack (drop
   files into `assets/`, wire via `SND.load`). Blocked in-sandbox by the egress proxy; do it
   locally or point me at an npm SFX package.
3. **Crew reactions.** The "living crew" flavor (deck figures reacting to fire/hits) — deferred
   on purpose until the ship art bar is set, so we don't pile rough 3D on rough 3D.
4. **Balance tuning.** Wave timing, damage curves, heli toughness — quick knobs, not rebuilds.
5. **Music-for-sale.** YouTube streaming is fine for a free web build; swap to licensed/
   royalty-free before any *paid* release.

---

## 🅿️ Parked / future content (deliberately deferred — nothing cut)

From DESIGN §18 + session notes. All are *content on the existing framework*, not new engines:
- Multiplayer (sim kept clean so it stays possible)
- Roblox port (dev is unsure; standalone-first)
- More domains as first-class: full **ground** ("War Economy") + **air** campaigns
- **Airships**, and a **fantasy setting** the dev may port in
- **ISOT** scenarios; wilder what-if battles
- Semi-open-world quadrant movement (From-the-Depths style) — vs. the current abstract graph
- Deeper War-Thunder-ish component detail (kept shallow for now, on purpose)
- Much larger maps (design already scales — more nodes in the same graph)
- Content pipeline formalization (adding a ship/scenario as pure data)

---

## ✅ Built so far (as of 2026-08-17)

Single self-contained `game.html` (Three.js, no build step), on branch
`claude/naval-strategy-game-design-wvrwiv`:
- **Campaign:** 11-zone panelized theater, turns, garrisons + Industrial Capacity, capture by
  attack, auto-resolve or launch a battle, theater win/lose.
- **Battle:** corvette + wingman (fleet grows with territory) vs zone-scaled enemy waves;
  take over any turret; stretched-tracer combat, hit-stop, muzzle flash, recoil.
- **Systems:** JSON-clean `G` + parallel render registry; event bus; input arbiter; ported
  YouTube music; difficulty sliders (weapon damage + aim-assist↔manual); box-select.
- **Roster:** patrol boat, corvette, destroyer, scout heli, gunship, gunboat, missileboat
  (all data-driven).
