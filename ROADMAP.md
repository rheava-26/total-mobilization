# TOTAL MOBILIZATION — Living Roadmap & Model Handoff

**This file is the single source of truth for the ongoing work. Update it EVERY turn and commit it, so nothing is ever forgotten.** It also serves as the send-off / onboarding doc for the next model taking over (the designer is moving the expensive framework builds to **Fable 5**).

**Bank DESIGN DISCUSSIONS and concepts here, not just task lists.** Conversation memory gets compacted; ideas that were discussed but not written here have been lost before. If the designer talks through a concept — even a rejected or someday one — it gets a line in this file.

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

- **Live version: v3.34.0** (on `main`). Enemy shields now block player fire (incl. splash); PD laser moved off the Fighter to a new "Laser Interceptor Warden".
- **In-flight agents:** none.
- **AWAITING DESIGNER:** (a) **CIWS-perf + auto-emplacement nerf** — its agent was interrupt-killed twice with nothing saved; still wanted (live lag + unstoppable free wall) — re-fire on the designer's go. (b) **MORALE/ORGANIZATION** design pass (global vs local shock? how harsh?). (c) Enemy-escalation v2 (snowballing drops, groundfall warnings for all supers, 5 opening bombarders). Lethality = REVERSED, do not build.
- **RESOLVED (emplacement question):** nerf the FREE auto-emplacements only (not built guns, no whiffing); CIWS's per-round explosion spam is the real perf killer. Side-vignette fixed (v3.31.1). Hidden `nightMode` flag awaiting the designer's verdict.
- **Fable 5 has the organizer seat.**

---

## 🚀 THE BIG VISION — "SKY / SEA / SPACE BATTLESHIPS" (the next major build; likely Fable's)

The unifying insight: **the war is VERTICAL. The enemy is UP (orbital siege), not to the side.** So the fantasy isn't ground invasion — it's a **fortress world of capital ships firing upward.** This single reframe fixes the old "ground units feel wonky on one axis" complaint.

- **KEYSTONE — Deflector Shields → auto-shields on every unit bigger than a jet.** Researching Deflector Shields gives all large units (sea battleships, airships, spacecraft, big ground) a shield that **wears down instead of pops.** This *revives the battleship* ("one missile wears the shield" not "one missile kills it"), justifies the airship, and delivers durability all at once. Shields should carry **upkeep / power cost** (a capital investment you field a few of — the ONE place a soft cost is right, and it's a cost, not a cap).
- **Multi-weapon capital ships:** sea battleships, airships (flying AA dreadnoughts — power is compact in the air), and city-sized spacecraft all mount **mixed weapons firing at once** — railguns + lasers + missile cells. Apartment-block gunships with long-range plasma.
- **Weapon-swap upgrades:** planes carry autocannons AND missiles; research swaps autocannon→plasma and missiles→micromissiles per-slot. Reuses the existing `UPGRADES`/`effDef` plasma-re-arm machinery.
- Sea battleships explicitly wanted alongside sky/space — capital ships in all three theaters.

---

## 👾 THE ENEMY OVERHAUL — full design bank (build AFTER 4.0, but the design lives HERE so it's never lost)

**The fantasy:** an invader with orbital supremacy massing a VAST armada you can WATCH assemble in deep orbit during PREP — stationed ships, lit hangars, a countdown — before it descends. Build DREAD. **Phased doctrine:** First Strike (orbital saturation degrades your exposed force) → Suppression (bombardment + jammers ground your air) → Overwhelming simultaneous multi-front landings (burst waves, not a trickle).

- **THE CAPITAL FABRICATOR (the true boss & WIN TARGET):** a dreadnought the WIDTH OF THE SCREEN anchored deep in the exosphere, with **destructible subsystems** — dozens of hangars (endless fighters), AA batteries (PD that downs your aircraft AND rounds), missile launchers. You DISMANTLE it subsystem by subsystem, not chip an HP bar. Deploys **~40 passive laser satellites** the moment the invasion starts. *(Designer is drawing the hull art themselves — transcribe to canvas when it exists.)*
- **Counter-systems** (each hard-counters one dominant player strat; each has a counter-counter): **AA-Missile Fairing "Flak Barge"** (mass-dropped pods that pop into homing-AA batteries — punishes cheap-fighter spam) · **Orbital Laser-Interceptor Grid "Aegis-Net"** (sat grid that auto-lasers your missiles/strikes — punishes missile-only play) · **Orbital Rail-Lance "Skyhook"** (hypervelocity ground strikes, near-uninterceptable — answered by anti-orbital fire/shields/dispersal) · **Air-Superiority Swarm "Reaver wing"** (dogfighters that hunt YOUR fighters) · massed antimatter-slug whistlers + EW jammers.
- **More concepted weapons:** Antimatter Flash Cannon (telegraphed zone-vaporizing ray / kiloton airburst bomblets) · Scalable Plasma (drone-mounted up to building-sized capital plasma mortars) · Antimatter Flak Curtain (airburst WALL across flight lanes) · Smart Rod-Storm (rod saturation aimed at your densest cluster — punishes turtling) · Mag-Tether Grappler (yanks your sats/aircraft UP into a kill zone) · **Adaptive Fabricator** (scouts your composition, builds the counter) · Shutdown Pulse EMP (briefly disables a fraction of electronic units).
- **Flavor concepts (Claude's):** The Eclipse (a ship so vast it casts a moving SHADOW debuff) · Harvesters eat wreckage to fabricate on-site (punishes attrition) · Mimics (late-game copies of YOUR best units) · The Choir (synchronized antimatter emitters, "kill them before the note lands") · The Peeling Sky (temporarily strips your air ceiling).
- **Opening-bombardment arsenal still unbuilt:** nuclear atmospheric EMP · antimatter airbursts (shooting them down still detonates them) · hostile fighter swarms · microrocket drop pods · asteroids (~2500 HP — break them up or catastrophe) · **sun blocker** (deploys if the siege stalls; sits atop the exosphere; tanks stability; space-weapons-only) · centrifugal drone-flinger · constant light bursts · tungsten **cluster-munition buster** variant · flare decoys that actually BREAK missile lock · **relativistic planet-cracker** hard-loss (top intensity tiers only).
- **Bosses → fortresses, not sponges:** lower raw HP, real countermeasures (PD, spawning, hard-to-reach), screen-scale.
- **Scale & perf strategy:** fleet reads as 1000+, but simulate BATTLEGROUP entities (HOI4-style — one entity = a brigade with a count/HP pool, rendered as a cluster). Designer verdict: ~equal numbers + SMART enemies, not literal thousands (phones melt). The **mobilization preview / order-of-battle manifest** ("127 antimatter-slug drones · 339 air-dominance fighters…") sells the true scale in text — it also fixes "the invasion's scale and attack plan was never explained to the player." **Planetary approach view** (cinematic fleet-closing-on-Earth shot) = later dread-polish.
- **Enemy roster consolidation audit** (several ships too similar). **Escalation:** Invasion & Annihilation escalate if you keep surviving with the mothership alive.

## 😰 MORALE / ORGANIZATION — "shock" on the battlefield (designer concept, needs a design pass)
Make stability VISCERAL on the ground. A unit-level **morale/organization** state that mirrors the STABILITY score: when the nation is stable, units fight crisp and organized; when stability craters — because a city block just got leveled and a chunk of the defensive network crumbled — units on the ground **panic and enter SHOCK**, struggling to focus. Designer's framing: *"I'd be freaking the hell out and struggling to focus if I just watched all of Prague get erased off the map."*
- **Driven by stability + shock events:** baseline morale follows `effStab`; a catastrophe (city DISTRICT destroyed, a big defensive structure lost, a nuke nearby) fires a temporary SHOCK SPIKE — a jolt of panic that fades over seconds. Possibly localized (units near the catastrophe shaken hardest) + a global baseline.
- **What shock does:** shaken units fire slower, aim worse (lead/accuracy degrade — they were just given leading; panic undoes it), move hesitantly/scatter, maybe visually tremble. Recovers as stability climbs back and no new shocks land.
- **This is the GRADIENT the designer always wanted:** stability loss should "degrade your units," not instant-loss. The Capitulation Crisis (production freeze + ~30% mutiny at 0 stab) is the rock-bottom extreme; morale is the smooth ramp above it.
- **NO death spiral (critical):** because stability is now cap-based and RECOVERABLE (not a drain), morale recovers with it — a shaken army claws back, it doesn't spiral to zero with no counterplay (the exact thing the designer hated about the old drain). Keep a floor so low morale never = helpless.
- **IMPACT VARIES HEAVILY by unit + situation** (v2 detail):
  - **MECHANICAL units are immune** (drones, androids, mechs, autonomous, machine-family). **Organic/human units falter.** This is the strategic core: a robot army ignores morale entirely (the "machine" society answer).
  - **Faltering behaviors (a spectrum):** hesitate / can't pick a target for a moment · hunker down & get SUPPRESSED · full mental breakdown / lose composure · and at minimum become WASTEFUL — dump their whole magazine in a panic, accidentally over-fire missiles (burns ammo/upkeep, reads as desperation, not just "weaker").
  - **⭐ EVERY RESEARCH PATH is secretly a morale answer** (the insight that makes the tree cohere): Automation → immune robots · Superhumans → fearless demigods · Psionics → mental fortitude/conditioning · Biotech → engineered composure · Propaganda/defensive structures → SUPPRESS panic in a radius. This finally makes the research tree about the game's real question: *how does your civilization keep functioning while watching itself be annihilated?* — the "what does total mobilization turn you into" theme, made mechanical. (Answers the design doc's "societal shift was never represented.")
  - **Shield-watching stress (counter-intuitive shield "nerf"):** units sheltering under a shield that's visibly being HIT and WAVERING get scared as it weakens — even though they're objectively safer. Keep this MILD (flavor, not a reason to avoid shields).
  - **PRODUCTION shock:** factory/economy workers panic under catastrophe too (9/11 framing — "let me check if my family's okay"), slowing production. The gradient above the Capitulation Crisis's hard production-freeze.
  - **Ties into the DECISION/SOCIETY layer:** dystopian / "mean" doctrine choices interact with morale (a harsher society may suppress panic at a human cost, etc.).
- **BUILD IN LAYERS** (like garrison/stability were): L1 spine = per-organic-unit morale from `effStab` + shock spikes on catastrophe → combat faltering (mechanical immune) + clear feedback (shaken visual + HUD). L2 = research suppression counters. L3 = production shock + shield-stress + society ties.
- **Legibility is make-or-break:** the player MUST see why units falter — shaken-unit visual (tremble/hunker/scatter), a morale HUD readout, a "⚠ SHOCK — [district] LOST" jolt notification.
- OPEN Qs: global-baseline + local-spikes (Fable's vote) vs pure-local? how harsh at the bottom (Fable: sloppy/slow, never frozen)?

## 🗞 THE DECISION & SOCIETY LAYER (the "decision tab" concept — needs its own design pass with the designer)

The original game's biggest missed opportunity, per the designer: **research combos only ever unlocked a unit — they should trigger DECISION CHAINS and NEWS POPUPS with genuinely large impacts on the world and policy.** A decisions/events layer:
- Combo research (Transhumanism, Shapeshifters, Hive Mind territory) presents **choices** with world-scale consequences, announced via news popups — policy shifts, society transformation, not "+1 unit type".
- **Society states must become REAL:** dystopia/verdant/machine are currently just a sky tint + one stability modifier. The societal transformation implied by "total mobilization" should be visible and mechanical (what your civilization becomes).
- **Bioengineering is the designer's chosen axis** for society-level shifts (flagged repeatedly as the interesting tree for this).
- Ties into: Superhuman political costs, dangerous-research unrest, the coup/Capitulation-Crisis framing, and the stability-cap model.

---

## 📋 BACKLOG (prioritized; move to Shipped as done)

### 🔥 COMBAT REWORK — from the latest playtest (a big coherent vision; sequence carefully — shared `hurt()`/projectile hot path)
The designer's combat philosophy: **glass cannons everywhere.** Most units (even large ones) die in 1–2 hits; combined defensive fire instantly deletes enemies (satisfying); **SHIELDS are the ONE survivability layer.** Pieces:
- ~~**Lethality: most units die in 1–2 hits.**~~ **❌ REVERSED by the designer (do NOT build).** Reason (their insight): fast deaths favor the side with MORE guns = the PLAYER, so it makes the game EASIER, not harder — and it kills the prolonged sieges that are the whole point. The real problem was never durability, it was the free auto-emplacement wall (see CIWS/emplacement item). Leave `hurt()` durability AS-IS.
- **PLAYER shields work fine — it's ENEMY shields that are broken.** (Fable misread this.) The player's Shield Generator dome blocks correctly. The fix: make ENEMY shields (beachhead domes / shielded enemy units) BLOCK incoming player fire the SAME WAY the player's shield does. Do NOT rework player shields; do NOT add threshold immunity; do NOT auto-shield big units (that's a later Battleships piece, deferred).
- **The FREE auto-emplacements are the real problem** (RESOLVED framing): the MG nests / flak / field guns that spawn AUTOMATICALLY when infantry dig in are an unstoppable "wall of bullets" that shreds everything — and they're free + absurdly scalable, so they trivialize the game. NERF THESE SPECIFICALLY (fire rate + damage) so a wall is strong-but-beatable and the tankier/saturating enemy can push through. This is NOT a tenet violation — a free auto-win-wall isn't a "toy," it's the game not being a game. **Do NOT make built guns miss/whiff** (whiffing feels broken, not hard) — Fable's counter-proposal accepted by designer's clarification.
- **CIWS perf — the explosion spam is the killer (designer confirmed):** every connecting CIWS round spawns an impact explosion → thousands/sec of pure-cosmetic FX = the lag. Fix: **invisible bullets + a few visible tracers, and KILL the per-round explosion** (sparse spark at most). Also make CIWS read as the anti-ordnance/anti-air SPECIALIST, not the one-true-answer (the lethality change normalizes much of this on its own).
- **Airship → capital ship:** shield + 6+ machine guns + anti-tank guns + MLRS (multi-weapon; Battleships vision).
- **Remove PD lasers from the Fighter** (a fighter carrying anti-missile lasers is a stretch) → make anti-missile lasers a **dedicated aircraft**.

### 🌊 ENEMY ESCALATION v2 — from the lethality-reversal playtest (NEXT enemy build; the *right* way to make it hard)
Instead of nerfing the player, make each landing GROW and blanket the map:
- **Drops ESCALATE:** a drop that starts as ~3 airships can balloon into ~9 airships + a ground landing drop + leviathans, etc. — each landing has the potential to snowball if not answered.
- **Groundfall WARNING banner for ALL super-unit types** (currently only the Land Leviathan gets the "⚠ HAS MADE GROUNDFALL" alert) — give the same warning to bombarders, hives, harvesters, etc.
- **Open the invasion with ~5 bombarders spread across the whole map** (covering every stretch of ground) right AFTER the first missile barrage — so orbital-plasma pressure blankets the ground from the start.
- This is the "difficulty from an overwhelming, escalating enemy" path — the correct alternative to the reversed lethality change.

### 🛰 ENEMY LANDINGS & SUPER-UNIT OVERHAUL — ✅ SHIPPED v3.33.0 (kept here for reference)
- **Every landing includes a super unit; super units come in GROUPS.** Each reinforces if not killed in ~30s (extends existing `BOSS_REINFORCE_T`).
- **Each super unit acts like a CARRIER** — spawns a small group of units.
- **Bombardier kit:** 2× PD fire rate · on arrival deploys **10 railgun sats + 10 laser-defense sats** · arrives with a **support ship that repairs in a radius** · gains a **plasma cannon that snipes GROUND units from space** (oppressive to ground; canNOT snipe air).
- **Bombardment missiles get DURABLE decoys** (take a few hits before breaking apart; current buster decoys are hp:1 — bump them).
- **Ships: fewer drops, ~2× units per drop, targeting the LEAST-defended areas.** Generally units land more in undefended areas and are stronger.

### 🧩 SQUADS — collision rework (designer's revised model; re-touches recent squad work)
Current model = one entity + footprint collision (reads as a blob). Designer wants: **separate collision + separate hitboxes per body, but a shared "brain" (targeting/AI) + a cohesion "pull force"** keeping the bodies clustered. Tightly linked, mostly independent. NOTE: separate collision reintroduces per-body cost — mitigate by keeping the AI/targeting shared (the expensive part stays single) + the existing render LOD.

### 🧵 MULTICORE / WORKERS — Fable's honest verdict: probably NOT worth it
Designer asked to offload targeting math to a worker so the main thread just renders. Reality: Workers can't touch the live object graph — you'd `postMessage`-copy all entity positions out and results back every frame; for thousands of entities that serialization usually costs ~what it saves and adds a frame of targeting latency. Profiling says **RENDERING is the bottleneck, not targeting math** — so the high-value fix is render-side (invisible bullets/tracers, LOD, glow cheapening), not multicore. Revisit only if targeting math ever becomes the profiled hot spot.

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
15. ~~**Achievements + coup screen**~~ ✅ SHIPPED v3.32.0 — coup defeat reframe + 5 medals (`couped` "Regime Change", `nukehappy`, `oops`, `forgotresearch`, `pacifist` "Who Needs Science?" = win with zero research) + unlock toasts.

### SMALL / QoL (new from latest playtest)
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
- Enemy-competence overhaul — **after 4.0** (make the enemy far smarter before touching the player further). Full design banked above in THE ENEMY OVERHAUL.
- Scenario menu rework — make the 4 intents feel like **equal choices**, not a difficulty ladder. Every cell of scenario×tier should be genuinely threatening (even Probe/Decap slam every capital hard in their short window).
- Difficulty as **×1 / ×2.5 / ×5 intensity tiers** decoupled from the 4 scenarios (the home for the brutal-vs-humane split). Escalation + planet-cracker only at top tiers of Invasion/Annihilation.
- Perf: it's **RENDERING**, not unit math, that's the bottleneck — optimize rendering (LOD/batching); web-workers/multicore can't touch Canvas2D (must stay on the main thread), so they're NOT the fix. Squad LOD shipped; general projectile/glow render cheapening still open (the old "9 FPS at 800 units" issue).
- **Strategic resources should grant BONUSES, not act as hard REQUIREMENTS** (the original design doc's intended fix — SRs ended up barely used vs. their intended role).
- **Graphical direction:** shape/outline reworks over tonal detail; the designer dislikes "jets become spaceships" at Space Age.
- **Redundancy consolidation audit (old list):** ground bombardment ×3, heavy tanks ×2, 1-item TECH tab; Comms-vs-Propaganda now RESOLVED (split roles); Scout CUT; Tractor Beam / Rail Hub on trial.
- **Musings banked:** optional ads + ~30 trivia pieces; a custom scenario editor already exists (`SCEN`).

---

## ✅ SHIPPED THIS SESSION (on `main`)
Emplacement-crumble fix · intro cinematic (plays every load, skip after first watch) · minimap (true world aspect, legible units) · camera auto-pull removed · **General** default · adaptive decapitation busters · persistent (killable) beachhead Shield Generator · super-units made proactive & multi-weapon (no HP inflation) · **Garrison economy v1** (weight budget, upkeep, HUD cell) · **infantry squads** · distinct **drone & warship textures** · **drone & fighter (with wingmen) squads** + grp:6 exploit fix · **Bombardment counterplay** (slow orbital buster descent, predictive interception, pinpoint damage) · **Stability overhaul** (drains→cap reductions, 0-stab = recoverable Capitulation Crisis not instant loss, stability reserve, demolition) · dispersed launcher buildings removed (TEL/Arsenal Ship/Ballistic Sub) · garrison orbital-lift exemption · mobile strike menu · demolition costs stability · buildings can slightly overlap · **Comms = reserve / Propaganda = cap** split + radar-stacking surfaced · **Particle Cannon → true hitscan** + **every projectile gun now LEADS** its shots (corrected intercept solve, 0%→100% hit rate on fast movers) · **squad perf** (4→3 bodies + render LOD, no phone-melt at scale) + **cleaner squad art** (staggered fireteam) + **water/drowning fix** (units never shoved into the sea; drowned units sink quietly, no explosion). Versions v3.20.1 → v3.31.0, each with a changelog entry.
