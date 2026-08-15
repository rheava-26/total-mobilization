# TOTAL MOBILIZATION — Living Roadmap & Model Handoff

**This file is the single source of truth for the ongoing work. Update it EVERY turn and commit it, so nothing is ever forgotten.** It also serves as the send-off / onboarding doc for the next model taking over (the designer is moving the expensive framework builds to **Fable 5**).

---

## ⭐ THE DESIGN TENET (read this first, it governs every decision)

> **The game says YES to the player's power fantasy. Difficulty comes from the enemy being overwhelming — NEVER from capping, nerfing, or denying the player their toys.**

The designer's own words: *"you see this thing and say 'oooh I could place 100 of those and it'd be OP?' and then the game says 'errr no.' We say yes."* When something is too strong, the answer is a scarier enemy or a real cost (upkeep/logistics), **not** a hard cap or diminishing returns. Multiplicative aura stacking, 90 airships, a planet-covering army, a wall-of-light particle cannon — all intentional. Let them happen. Fix *perf* and *bugs*, never the fantasy.

---

## 🛠 WORKFLOW & INFRASTRUCTURE (how work actually ships here)

- **The game is ONE file:** `title.html` (~4,700 lines, vanilla JS + Canvas2D, no build step, all art procedural via `ART`/`BART` draw-functions). `index.html` redirects to it. Serve statically, open `title.html`.
- **Delegate the coding.** Implement game changes by dispatching **Sonnet subagents in isolated git worktrees**, not by hand-editing `title.html`. Run parallel agents ONLY on **non-overlapping code regions** (partition by function/region; give each an explicit "do NOT touch" list), then `git merge` each worktree branch back. When two tasks touch the same code, SEQUENCE them. Tell every agent to **commit incrementally** (a session-limit kill with zero commits loses everything — this has happened).
- **The designer's turns are for DESIGN** — ideas, mechanics, feasibility, real opinions (not "nice, big change"). Have opinions. Be a partner. Don't narrate git/merge logistics unless asked.
- **Branch:** develop on `claude/game-balance-tweaks-x4odw9`.
- **Deploy flow (the designer plays `main`):** for each shippable change → bump the footer version + prepend an in-game `<details>` changelog entry (house style: emoji + `<b>` headers) → `git push -u origin claude/game-balance-tweaks-x4odw9` → **deploy:** `git push origin claude/game-balance-tweaks-x4odw9:main`. The designer authorized auto-deploy to main. (`main` may have a stale LOCAL ref — always `git fetch origin main` before reasoning about it.) It's a PWA; a hard refresh busts the cache after deploy.
- **A changelog entry rides with EVERY version.** Standing designer instruction.
- **Verification bar (mandatory, zero pageerrors):** headless boot check before every deploy.
  - Serve: `python3 -m http.server <PORT>` from repo dir.
  - Chromium: `/opt/pw-browsers/chromium-1194/chrome-linux/chrome` args `--no-sandbox --use-gl=swiftshader`.
  - Playwright ESM: import from `/opt/node22/lib/node_modules/playwright/index.mjs`; run via `NODE_PATH=/opt/node22/lib/node_modules /opt/node22/bin/node script.mjs`.
  - In-page: set `localStorage.tm_seenTut='1'` AND `localStorage.tm_seenIntro='1'`. **The intro overlay plays on EVERY load** → after reload, click `.tm-skip` if present, then `[data-act="play"]`, then `waitForFunction(()=>typeof G!=='undefined' && G && mode==='game')`. Assert **zero `pageerror`** (a syntax error stops boot — this is the make-or-break check). Put test scripts in scratchpad, never the repo.

---

## 📍 CURRENT STATE

- **Live version: v3.31.0** (on `main`).
- **In-flight Sonnet agents:** none — both landed and shipped (particle hitscan + universal leading; squad perf/art/water).
- **Next:** the designer is handing the big framework builds (see The Big Vision / Backlog) to **Fable 5**. This file is the send-off.

---

## 🚀 THE BIG VISION — "SKY / SEA / SPACE BATTLESHIPS" (the next major build; likely Fable's)

The unifying insight: **the war is VERTICAL. The enemy is UP (orbital siege), not to the side.** So the fantasy isn't ground invasion — it's a **fortress world of capital ships firing upward.** This single reframe fixes the old "ground units feel wonky on one axis" complaint.

- **KEYSTONE — Deflector Shields → auto-shields on every unit bigger than a jet.** Researching Deflector Shields gives all large units (sea battleships, airships, spacecraft, big ground) a shield that **wears down instead of pops.** This *revives the battleship* ("one missile wears the shield" not "one missile kills it"), justifies the airship, and delivers durability all at once. Shields should carry **upkeep / power cost** (a capital investment you field a few of — the ONE place a soft cost is right, and it's a cost, not a cap).
- **Multi-weapon capital ships:** sea battleships, airships (flying AA dreadnoughts — power is compact in the air), and city-sized spacecraft all mount **mixed weapons firing at once** — railguns + lasers + missile cells. Apartment-block gunships with long-range plasma.
- **Weapon-swap upgrades:** planes carry autocannons AND missiles; research swaps autocannon→plasma and missiles→micromissiles per-slot. Reuses the existing `UPGRADES`/`effDef` plasma-re-arm machinery.
- Sea battleships explicitly wanted alongside sky/space — capital ships in all three theaters.

---

## 📋 BACKLOG (prioritized; move to Shipped as done)

### NEXT BIG BUILDS
1. **Sky/Sea/Space Battleships** (above) — shields-on-big-units keystone first, then multi-weapon capital ships + weapon-swap upgrades.
2. **Garrison v2 — supply chain (emergent cap, no hard wall):** upkeep-heavy **Factories** → nearest **Depots** → nearest **buildings** (proximity logistics). Remove per-unit upkeep (upkeep lives on factories). Limits are economic + logistical only. (Current garrison is v1: passive per-building auto-fill + per-unit upkeep + a hard global Stockpile weight cap.)
3. **Real helicopters** (designed long ago, NEVER built — biggest miss): naval-launched + city-launched, slow/cheap/close-range, **low ceiling** (tropo-only, can't reach high enemies — keeps them from feeding air-spam), land in cities to rearm.
4. **Aerowork-style garrison buildings:** buildings that grant garrison capacity / unit-upgrades / new unit-types to *neighboring* buildings; "+1 missile capacity" buildings; **helicopter-depot** buildings that give city tiles a helicopter garrison → turn a city into a helicopter swarm.
5. **Units enter buildings → become turrets** (huge perf + cinematic win; ties to garrison): a stack cap, click a building to see who's inside, building-type buffs.
6. **Apocalypse as honest ENDURANCE mode:** replace the demoralizing "survived 9%" with a clear survival objective (e.g. HOLD FOR X) + a personal best to beat. General & below stay winnable; Apocalypse is a last-stand you chase a record on.

### MEDIUM / FEATURES
7. **Anti-missile interceptor building** — laser-tower-sized, ~1500 IC / 100 MP / 30 influence, 4 missiles, reaches space, slow reload, buriable in a silo. The dedicated bombardment counter. (Bombardment code now merged — UNBLOCKED.)
8. **Aircraft reach one band up (thermosphere)** — partial walk-back of the hard mesosphere cap; exosphere stays space-only. (UNBLOCKED now.)
9. **Multi-weapon big units** (titans, land leviathans, warships get secondaries) — largely folded into Battleships.
10. **Lasers → damage-over-time** (a beam melts, it doesn't "hit"). Shifts lasers to the "melt the big slow thing" role; flak handles swarms.
11. **Conventional reinforcement — "you're not alone":** if the game detects the player is woefully underprepared, allied conventional forces surge in (≈4 carrier strike groups, 4 armored battalions, city defenses, underground barracks); some aid even if prepared. Cities also self-improve (bunkers, heli bases).
12. **Inherited-nation start:** cities begin with a few garrison stockpiles/factories + 2–3 pre-placed missile silos, so you inherit a country instead of an empty map.
13. **Influence "hire allies" sink:** buy a Carrier Strike Group for ~200 influence + IC + MP, like a strike, especially pre-invasion. Late-game influence sink.
14. **Naval identity:** differentiate Missile Cruiser (AA/point-missile escort) from Arsenal Ship "Leviathan" (VLS saturation); untangle the name clash with the (now-removed) Arsenal Ship building. Distinct warship hulls already shipped.
15. **Achievements + coup screen** (specced, agent died twice, NEVER shipped): pre-invasion stability collapse reframed as a **COUP/OVERTHROWN** defeat; 4 achievements — `couped` "Regime Change", `nukehappy` "This Will Have No Long-Term Consequences" (10 nukes/game), `oops` "Oops" (nuke in peacetime), `forgotresearch` "I Forgot to Research" (20 ballistic strikes/game); unlock notifications. System already exists (`ACHIEVEMENTS` array, `G.ach`).

### SMALL / QoL (new from latest playtest)
16. **Unit buff panel:** click a unit → see all active buffs affecting it (nearby-building auras, research mults, resource discounts). Also surfaces stacking to the player.
17. **Fortified/dug-in units should NOT block building placement** (currently you can't build where units have fortified).
18. **Click-through occluded buildings:** if a unit is in front of a building, clicking should let you reach/see the building behind it (selection cycling / building priority in `pickAt`).
19. **End-screen post-game graphs** ("the 9000 graphs") — rich after-action charts.
20. **Special-building effects listed in BOTH the SPEC menu AND the Infrastructure menu** (currently only one).
21. **Factory production readout:** show what garrison factories are producing, how much, how fast.
22. **Descriptions audit (partly done):** stale unit/research text. CONFIRMED BUG: the **Nanotech** research node only advertises the Autonomous Plant — it does NOT mention the **Nanite Foundry** and **Nanite Turret** it also unlocks (both `req: 'nanotech'`). Fix the node's blds list/description.

### AUDIT — DECIDED CUTS / FIXES
- **CUT:** Mass Drivers (pure +35% dmg stat), Scout "Lynx" (recons nothing — no fog of war), Hive Mind (never integrates cleanly). **CUT or fold power into something real:** Void Engine (pure +80% IC/+20% dmg). Re-point any prereqs when removing nodes.
- **FIX:** Titan fires `weapon: 'tracer'` (a machine gun) — give the 1,300-HP colossus a real signature weapon.
- **PERF philosophy for the death-gun:** aura stacking is currently MULTIPLICATIVE (30 ammo depots ≈ 800× fire rate). Designer wants to *try the "super mega death gun"* — **DO NOT NERF YET.** When (if) it stops being fun, tame to ADDITIVE (`+25% +25%`, not `×125% ×125%`), never a hard cap. Radar-stacking already surfaced in-description.
- **AUDIT STILL TO DO:** strikes (`STRAT` table), enemy roster, achievements, raw UI strings — sweep for stale/redundant/weird, categorize KEEP/CUT/FIX/MERGE.
- **ON TRIAL** (suspected nothingburgers, watch in a real game before defending): Fire Control, Tractor Beam, Rail Hub.

---

## 🎨 BLOCKED ON THE DESIGNER
- **Capital Fabricator** super-ship art (designer is drawing the hull; transcribe to canvas once it exists).
- **Lore / story layer** (parents flagged the story as thin).
- **Music** — artist permissions for the default soundtrack.
- Possible spin-off: an **FPS in the same setting** (soldier's-eye view of the orbital siege; Helldivers-style co-op or extraction-under-bombardment).

## 🔒 DEFERRED DECISIONS (locked, apply later)
- Enemy-competence overhaul — **after 4.0** (make the enemy far smarter before touching the player further).
- Scenario menu rework — make the 4 intents feel like **equal choices**, not a difficulty ladder.
- Difficulty as **×1 / ×2.5 / ×5 intensity tiers** decoupled from the 4 scenarios (the home for the brutal-vs-humane split).
- Perf: it's **RENDERING**, not unit math, that's the bottleneck — optimize rendering (LOD/batching); web-workers/multicore can't touch Canvas2D (must stay on the main thread), so they're NOT the fix.

---

## ✅ SHIPPED THIS SESSION (on `main`)
Emplacement-crumble fix · intro cinematic (plays every load, skip after first watch) · minimap (true world aspect, legible units) · camera auto-pull removed · **General** default · adaptive decapitation busters · persistent (killable) beachhead Shield Generator · super-units made proactive & multi-weapon (no HP inflation) · **Garrison economy v1** (weight budget, upkeep, HUD cell) · **infantry squads** · distinct **drone & warship textures** · **drone & fighter (with wingmen) squads** + grp:6 exploit fix · **Bombardment counterplay** (slow orbital buster descent, predictive interception, pinpoint damage) · **Stability overhaul** (drains→cap reductions, 0-stab = recoverable Capitulation Crisis not instant loss, stability reserve, demolition) · dispersed launcher buildings removed (TEL/Arsenal Ship/Ballistic Sub) · garrison orbital-lift exemption · mobile strike menu · demolition costs stability · buildings can slightly overlap · **Comms = reserve / Propaganda = cap** split + radar-stacking surfaced · **Particle Cannon → true hitscan** + **every projectile gun now LEADS** its shots (corrected intercept solve, 0%→100% hit rate on fast movers) · **squad perf** (4→3 bodies + render LOD, no phone-melt at scale) + **cleaner squad art** (staggered fireteam) + **water/drowning fix** (units never shoved into the sea; drowned units sink quietly, no explosion). Versions v3.20.1 → v3.31.0, each with a changelog entry.
