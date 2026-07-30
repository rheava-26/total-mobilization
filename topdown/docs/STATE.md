# STATE — read this first

**Purpose:** a fresh session (or a fresh Claude) reads this file and can continue immediately
without re-reading the whole codebase or re-deriving decisions. Update it whenever something
lands or a preference changes.

**Last updated:** 2026-07-29
**Branch:** `claude/topdown-rebuild` (pushed to origin — this is the live branch, NOT the
default branch and NOT the branch named in any task config)
**Game lives in:** `topdown/` — the repo ROOT `index.html` is an OLD abandoned side-view
prototype. Never serve from the repo root; always serve from inside `topdown/`.

---

## RESUME HERE

Nothing is in flight. Working tree clean. The game is playable and shipped as a single file.

The designer's stated next preference: **smaller-scale implementations.** One change at a
time, ~15–20 minutes, show the result, get a reaction, then continue. Do NOT chain multiple
large agent builds back-to-back — that produced multi-hour black boxes and burned session
limits three times.

**Open items, roughly in priority order:**

1. **Civilian → military industry conversion (visible).** The map already spawns civilian
   infrastructure (`map.infrastructure` — airports, refineries, steel mills, ports, rail
   yards, power stations) and each site carries a `converted: false` hook waiting for this.
   As `economy.mobilizationLevel` rises, flip sites to converted, push a news headline, and
   draw a military overlay (camo/markings/parked assets). Design it VISUAL + NARRATIVE only
   so it stays balance-neutral. Two prior attempts died on session limits before writing any
   code — nothing exists yet, clean slate.
2. **Finish the battalion pivot.** Lone ground-unit production (`tanks` category →
   `outputs:['tank']` in `game/production.js`) still coexists with battalions, so there are
   two competing ways to get ground troops. Demote/remove it so battalions are THE ground
   force (air/naval/strike stay individual). BALANCE-SENSITIVE — re-run the balance harness
   after.
3. **Adaptive tense music** — procedural Web Audio that intensifies with
   `economy.threatLevel` / mobilization. (Real audio FILES can't be fetched here; the network
   blocks non-registry hosts. `game/audio.js` already plays a file if present and falls back
   to synth, so dropping real `.mp3`s into `assets/audio/` later Just Works.)
4. **Designer's own copy pass.** Everything player-facing is `[PLACEHOLDER]` BY DESIGN — the
   designer writes all copy. `docs/LORE.md` now provides the world, vocabulary, and
   per-system story hooks to draw from. Do NOT write final narrative copy unless asked.

---

## WHAT'S BUILT (all committed + pushed)

**Battalion system (the ground-combat model).** Ground forces are formations, not lone units.
- `game/battalions.js` — battalion entities; sub-units are normal units tagged `u.battalion`
- `game/reinforcements.js` — three sources: Factory Elite / Army-Sent Standard / Local Militia
- `game/battalionDoctrine.js` — self-deploy to a sector, garrison, stance (defensive/balanced/aggressive)
- `game/battalionMorale.js` — morale/cohesion, fresh→steady→shaken→broken, rout, the single
  `defenseMult` combat hook (the ONLY damage hook — `resolveHit` in `units.js`)
- `game/homedefense.js` — cities auto-muster "Home Guard" militia scaled by mobilization,
  drawing manpower. This is the "nation mobilizes alongside you" signature mechanic.
- Enemy fields battalions too, with a balance guard: enemy `defenseMult` is pinned flat 1.0
  and enemy rout is gated on STRENGTH (<20%), not morale — because an attacker owns no
  territory so its morale only ever falls.

**World & presentation.**
- Modern city rendering (glass/concrete towers, height tiers, rooftop detail) + district
  structure, landmarks, arterials, walls, waterfront
- `map.infrastructure` — naturally-spawning civilian facilities placed with real spatial logic
- `game/supply.js` — cargo convoys driving real roads (cosmetic)
- Per-type textured building art (`BUILDING_VISUALS` in `game/buildings.js`)
- Real-vehicle unit silhouettes + NATO/APP-6 battalion symbology (echelon `II`, branch icons)
- Player color is military grey `#8a9199` (was cyan `#5fd0ff`)
- Naval rework: standoff ranges (destroyer 980 vs tank 220), momentum + wide turns, wakes,
  gunboat/frigate/destroyer classes
- Combat feel: screen shake, smoke/scorch, muzzle flash; `game/audio.js` file-else-synth

**Systems & meta.**
- `game/news.js` — DISPATCHES ticker, wartime bulletins generated from live game state
- `game/afteraction.js` — After-Action Report on the outcome screen with a COPY REPORT button
- Modern Military C2 UI (dark tactical console) — chosen from 4 mockups in `docs/ui-options.html`
- `tools/bundle.mjs` → `dist/total-mobilization.html` — the whole game as ONE self-contained
  file, zero external requests, playable from `file://`

**Docs worth reading before designing anything:**
`docs/CONCEPT.md` (design bible), `docs/LORE.md` (world/vocabulary/story hooks),
`docs/reference-*.md` (research: battalions, naval, city/mobilization, UI x3),
`docs/playtest-agents/` (local commentary-agent kit for the designer's own PC)

---

## HOW TO RUN / TEST

```
# serve (ALWAYS from inside topdown/)
python3 -m http.server 8000 --directory /home/user/total-mobilization/topdown
# then open http://localhost:8000/?seed=42   (?seed= skips the menu)

# regenerate the single-file build
node /home/user/total-mobilization/topdown/tools/bundle.mjs

# balance harness — 36 self-play games, ~15-20 min, run in BACKGROUND
PORT=8791 CONCURRENCY=2 node /home/user/total-mobilization/topdown/tools/balance-harness.mjs
```

**Healthy balance looks like:** unprepared LOSES on normal+; prepared WINS; no "undecided".
If unprepared starts winning everywhere, something made the invasion too weak — that exact
regression happened once (enemy battalions routing at full strength).

---

## ENVIRONMENT GOTCHAS (these cost real time to rediscover)

- **cwd resets between Bash calls** — use absolute paths.
- **Foreground `sleep` is blocked** (exit 144). Use Playwright waits, or run long things in
  the background. A `pkill` at the start of a compound command will abort the whole thing.
- Chromium: `/opt/pw-browsers/chromium-1194/chrome-linux/chrome`
  Playwright ESM: `/opt/node22/lib/node_modules/playwright/index.mjs`
  (run node with `NODE_PATH=/opt/node22/lib/node_modules`, args `--no-sandbox --use-gl=swiftshader`)
- `assets/audio/*.mp3` **404s are expected and harmless** — the synth fallback handles it.
- Network blocks non-registry hosts, so no webfonts, no CDNs, no downloading assets.
  System font stacks and procedural drawing only.
- `window.__debug` exposes hooks for nearly every system — sub-objects BIND world/map/economy
  internally, so call them with only their own args (e.g. `__debug.reinforcements.raiseMilitia()`,
  `__debug.homeDefense.updateHomeDefense(dt)`).
- **Run ONE build agent at a time.** Two agents in the same working tree stashed/reverted each
  other's uncommitted work. Read-only research agents can run in parallel safely.

---

## DESIGNER PREFERENCES (learned — honor these)

- **They are a game designer, not a programmer.** Explain in design terms, not plumbing.
  Don't hand them tooling critiques; just handle it.
- **Small chunks.** ~15–20 min of work, then show a result. No multi-hour black boxes.
- **Show, don't tell** — screenshots for anything visual. They decide on look.
- **Honesty over positivity.** They explicitly asked for harsh outside critique twice and
  valued it. Verify subagent claims rather than relaying them; agents have been wrong.
- **They write all player-facing copy.** `[PLACEHOLDER]` is deliberate, not an oversight.
- **Options over decisions** for aesthetic calls — mock up alternatives and let them pick
  (this is how the UI direction was chosen).
- Dislikes: synth audio ("wonky" — placeholder only), "AI-looking" UI, anything that reads
  as arcadey, units that don't behave like their real counterparts.
