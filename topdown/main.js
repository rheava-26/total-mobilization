import { loadMap, loadGeneratedMap } from './engine/tilemap.js';
import { createRenderer, attachCameraControls, OWNERSHIP_COLORS, drawUnit, drawProjectile, drawImpacts } from './engine/renderer.js';
import { spawnUnit, updateUnits, updateProjectiles, clearClaims, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS, terrainSample } from './game/units.js';
import { BUILDING_DEFS, spawnBuilding, updateBuildings, isValidPlacement, sitePlacement, footprintHasDeposit, sellBuilding } from './game/buildings.js';
// BUILDBAR CATEGORY MAP (playtest: "group buildings... you can't scroll
// down on it") — pure data, see that file's header for the full rationale.
import { BUILD_CATEGORIES } from './game/buildingCategories.js';
import {
  createEconomy, updateEconomy, canAffordBuilding, spendForBuilding, buildingCost,
  ECONOMY_TUNABLES, MOBILIZATION_BANDS,
} from './game/economy.js';
import { RESOURCE_DEFS, RESOURCE_LIST, MINE_TUNABLES } from './game/resources.js';
import {
  PRODUCTION_DEFS, updateProduction, categoryForFacility,
  IMPORT_TUNABLES, importCost, canAffordImport, importResource,
} from './game/production.js';
import {
  TECH_DEFS, RESEARCH_COPY, createResearchState, updateResearch, techStatus,
  canAffordResearch, startResearch, isCategoryUnlocked,
} from './game/research.js';
import { createStatCard } from './game/statcard.js';
import { computeFog, detects, fogState, drawFogOverlay } from './game/fog.js';
import { findRoadRoute, findPath, findPathCached, pathfindStats } from './game/pathfind.js';
import { PHASES, createPhaseState, applyPhaseFog, beginCombat } from './game/phase.js';
import { showMainMenu, randomSeed, MENU_COPY, SCENARIO_DEFS } from './game/menu.js';
import { getTechTreeGraph, computeNodeState } from './game/techtree.js';
import { createTechTreeView } from './game/techtreeview.js';
// GOAL-DRIVEN TUTORIAL (Part A — CONCEPT.md's settled "Onboarding / tutorial
// concept" paragraph): game/tutorialplan.js owns goal-selection state and the
// reverse-planner; this is the ONLY place the two live states it needs
// (goalState, the plan derived from it) are created/threaded through, same
// pattern as researchState/economy above.
import { createGoalState, toggleGoal, isGoalNode, computePlan, TUTORIAL_COPY } from './game/tutorialplan.js';
// REACTIVE DIRECTOR (Part B — CONCEPT.md's settled "Reactive / adaptive
// guidance" + "Serious / Silly tone mode" + "Corner commentator" paragraphs):
// game/director.js owns off-track classification, the tone-mode flag, and
// the silly-mode self-sabotage gag state machine. main.js's job is only to
// call it at the real action points and reflect its state into the DOM (the
// corner bubble, the "are you five" dialog, the takeover/fired screens).
import {
  createDirectorState, setToneMode, reactToAction, handleBuildingSold,
  resolveAreYouFive, resetGag, DIRECTOR_COPY, announceCityEvent,
} from './game/director.js';
// FIRST-RUN INTRO SEQUENCE (the FRONT HALF of the onboarding concept —
// CONCEPT.md's settled "Onboarding / tutorial concept" paragraph, beats 1-2:
// "the only scripted stretch is the intro framing"). game/intro.js owns the
// beat state machine + the discard-flourish's pure draw function; this file
// wires it to the DOM and to the two ALREADY-BUILT systems it reuses
// end-to-end — the tech tree view (toggleTechTree, defined below) and the
// goal-driven checklist (#guidancePanel/game/tutorialplan.js) — see the
// INTRO SEQUENCE section further down for the actual wiring.
import {
  INTRO_COPY, createIntroState, shouldShowIntro, startIntro, skipIntro,
  beginGraphBeat, beginDiscard, discardProgress, finishDiscard, beginHandoff,
  completeIntro, drawDiscardFlourish,
} from './game/intro.js';
// PLAYABILITY v1 (CONCEPT.md's settled "Playability v1 — closing the game
// loop" section): game/objectives.js owns city ownership/capture and the
// win/lose evaluation (Part A); game/enemyai.js owns the functional-
// aggressor objective-seeking that REPLACES the old scripted "advance
// toward the player base" order (Part B — see spawnEnemyLandingForce
// below). Both are COMBAT-phase-only per the hard constraint that PREP
// stays peaceful/open — see the `combatActive` gate in loop().
import { initCityOwnership, updateObjectives, evaluateOutcome, initInvasion, CAPTURE_TIME_S, cityCenterWorld } from './game/objectives.js';
import { updateEnemyAI, debugEnemyGroups, debugAiStateSize } from './game/enemyai.js';
// PLAYER-SIDE DEFENSIVE DOCTRINE (CONCEPT.md Pillar 1, "The Nation Acts") —
// the defensive mirror of updateEnemyAI above: garrisons/disperses idle
// player units onto held cities (weighted toward the capital and whichever
// city is most threatened) instead of leaving them stacked wherever they
// spawned/were placed. See game/doctrine.js's header for the full contract.
import { updatePlayerDoctrine, debugDoctrineState, debugForceAssignTick } from './game/doctrine.js';
// AMBIENT LIFE (playtest feedback: "the game is too still" — game/ambient.js
// owns road traffic, factory smoke, coastal shimmer, city light flicker, and
// the capital's flag). Purely visual, read-only over map/world — see that
// file's header for the full "never touches gameplay" contract.
import { initAmbient, updateAmbient, drawAmbientGround, drawAmbientSmoke } from './game/ambient.js';
// COMBAT FEEL & AUDIO PASS — game/audio.js plays combat sound off world.sfx
// events (file assets under assets/audio/ when present, procedural synth
// fallback otherwise — see that file's header for the swap contract);
// game/impactfx.js owns the lingering smoke/scorch decal pools. Both are
// purely reactive to events main.js drains every frame — see the loop()
// section further down for the actual event-queue drain + screen-shake
// trauma model, which lives here in main.js rather than in either of those
// two files since it touches renderer.cam directly.
import { initAudio, resumeAudio, playSfx } from './game/audio.js';
import { createImpactFx, spawnImpactFx, updateImpactFx, drawScorch, drawSmoke } from './game/impactfx.js';
// BATTALION ENTITY + GROUPING LAYER (B1 of the 6-phase battalion rework —
// CONCEPT.md's "Ground combat model: BATTALIONS, not singular units").
// game/battalions.js owns the composition templates + the battalion record
// shape; this phase is purely additive (see that file's header) — the only
// wiring main.js needs is creating `world.battalions`, running the light
// per-frame prune pass, and exposing headless test hooks below.
import { spawnBattalion, updateBattalions, BATTALION_TEMPLATES, battalionStrength } from './game/battalions.js';
// BATTALION-LEVEL DOCTRINE (B3 of the 6-phase battalion rework — "Self-
// deploy + sectors + stance"). game/battalionDoctrine.js owns the formation-
// level analogue of game/doctrine.js's per-unit garrison AI — see that
// module's header for the full sector-pick/stance/arbitration contract.
// doctrine.js itself now skips any unit tagged `u.battalion` (see its one-
// line handoff comment), so this is the ONLY thing issuing those units
// orders; main.js's job is just calling it once per frame alongside
// updatePlayerDoctrine and wiring the Battalions panel + debug hooks below.
import {
  updateBattalionDoctrine, assignSector, setStance, cycleStance,
  debugBattalionDoctrineState, debugForceTick as debugForceBattalionDoctrineTick,
} from './game/battalionDoctrine.js';
// BATTALION MORALE / COHESION (B4 of the 6-phase battalion rework — "Morale
// / cohesion"). game/battalionMorale.js owns bn.morale/bn.cohesion and the
// per-unit defenseMult it feeds units.js's resolveHit; battalionDoctrine.js
// reads isRouting()/bn.morale itself for the rout branch (see that file) —
// this file's job is just calling updateBattalionMorale once per frame,
// coloring the Battalions panel rows by state, and exposing debug hooks.
import { updateBattalionMorale, moraleState, setMorale as setBattalionMorale } from './game/battalionMorale.js';
// REINFORCEMENTS (B2 of the 6-phase battalion rework — CONCEPT.md's "three
// sources of battalions": Factory Elite / Army-Sent Standard / Local
// Militia). game/reinforcements.js is a NEW, purely ADDITIVE module — see
// its header for the full "does not touch production.js" contract; this
// file's job is just creating `world.reinforcements`, ticking the queue once
// per frame near updateBattalions, wiring the Reinforcements panel (below),
// and exposing headless test hooks.
import {
  REINFORCEMENT_DEFS, requestElite, requestStandard, raiseMilitia,
  updateReinforcements, canAfford, playerCapital,
} from './game/reinforcements.js';

// ---------------------------------------------------------------------------
// PLACEHOLDER COPY (designer to rewrite) — CONCEPT.md's settled "First-run /
// tutorial is OPEN, not dictated" paragraph: the framework below ships with
// the STRUCTURE of the guidance layer and the phase-transition announcement,
// but every word a player actually reads is a stand-in. Every string here is
// functional/plain and marked [PLACEHOLDER] on purpose — find-and-replace
// this one block when writing the real tutorial/lore prose; nothing else in
// the file reads these values for gameplay logic, they're pure display text.
// NOTE: the guidance panel's copy (title/steps/dismiss hint/payoff) now
// lives in game/tutorialplan.js's TUTORIAL_COPY — its content is DYNAMIC
// (goal-driven, see the GUIDANCE PANEL section below), so it moved next to
// the planner that generates it rather than staying a static list here. The
// reactive director's lines (off-track acknowledgement, the gag chain, the
// "are you five" dialog) live in game/director.js's DIRECTOR_COPY for the
// same reason.
const COPY = {
  // Shown in the center-screen announcement banner the instant the enemy
  // landing force actually spawns (see triggerBeginCombat below).
  COMBAT_BEGIN_ANNOUNCEMENT: '[PLACEHOLDER] COMBAT PHASE BEGUN — THE ENEMY HAS LANDED',
  // PLAYABILITY v1 (CONCEPT.md's settled "Playability v1" section) — the
  // director's corner bubble on a capture event (game/objectives.js's
  // updateObjectives return value; see loop() below), and the victory/
  // defeat end-screen copy (#outcomeScreen in index.html, wired further
  // down in this file). Every string here is a plain functional stand-in,
  // same convention as COMBAT_BEGIN_ANNOUNCEMENT above — designer's to
  // rewrite the actual voice.
  CITY_CAPTURED_BY_ENEMY: name => `[PLACEHOLDER] ${name} has fallen to the enemy.`,
  CITY_RECAPTURED_BY_PLAYER: name => `[PLACEHOLDER] ${name} recaptured!`,
  VICTORY_TITLE: '[PLACEHOLDER] VICTORY',
  VICTORY_SUBTITLE: '[PLACEHOLDER] The landing force has been wiped out. The invasion is over.',
  DEFEAT_TITLE: '[PLACEHOLDER] DEFEAT',
  DEFEAT_SUBTITLE: '[PLACEHOLDER] The capital has fallen. The war is lost.',
  OUTCOME_RESTART_BUTTON: '[PLACEHOLDER] Play Again',
  OUTCOME_MENU_BUTTON: '[PLACEHOLDER] Main Menu',
};

const canvas = document.getElementById('view');
const hud = document.getElementById('hud');
const econHud = document.getElementById('econHud');
const buildbar = document.getElementById('buildbar');
const card = createStatCard(document.getElementById('card'));

// ECON/RESOURCE LEDGER — de-occlusion fix (playtest: "the econ HUD blocks
// the screen and menus"). #econHud is now a COLLAPSIBLE panel: a slim
// always-visible summary line (IC/manpower/influence/mobilization) plus a
// click-to-expand detail sheet (resources + per-facility production, the
// part that used to grow tall and eat map real estate). Collapsed by
// default on boot so the very first frame the player sees is a compact tab,
// never a wall of stats — see loop()'s HUD refresh below for what fills
// each half every frame.
const econHudToggle = document.getElementById('econHudToggle');
const econHudToggleIcon = document.getElementById('econHudToggleIcon');
const econHudSummaryEl = document.getElementById('econHudSummary');
const econHudDetailInnerEl = document.getElementById('econHudDetailInner');
let econHudExpanded = false;
function setEconHudExpanded(v) {
  econHudExpanded = v;
  econHud.classList.toggle('expanded', v);
  econHudToggleIcon.textContent = v ? '▾' : '▸';
  if (v) econHud.classList.remove('nudge'); // opened once → stop the discoverability pulse
}
econHudToggle.addEventListener('click', () => setEconHudExpanded(!econHudExpanded));
setEconHudExpanded(false);
econHud.classList.add('nudge'); // pulse the collapsed tab until the player first opens it (see setEconHudExpanded)

// MAP SOURCE (docs/CONCEPT.md settled ledger: "maps and all place names are
// procedurally generated per skirmish run" — a fresh seed every game by
// default). Two optional URL params, either usable independently:
//   ?map=maps/theater-01.json  — load a specific AUTHORED file (the
//     scenario path; the map's own JSON is the source of truth, seed or no
//     seed inside it).
//   ?seed=1337                 — reproduce a specific GENERATED run.
// With neither param, a fresh random seed is drawn every load so each
// skirmish gets its own geography and its own names.
//   ?difficulty=1.75            — OPTIONAL, only meaningful alongside a deep
//     link (?seed=/?map=): sets scenarioDifficulty directly instead of
//     leaving it at the 1.0 no-op default, so a deep link can reproduce a
//     SPECIFIC {seed, difficulty} pair without going through the menu
//     click UI. Exists for the headless balance harness (topdown/tools/
//     balance-harness.mjs) — driving hundreds of runs across seeds x
//     difficulties by clicking a menu row per run would work but is slower
//     and more brittle than a URL param; this is the exact same knob
//     game/menu.js's SCENARIO_DEFS rows already set, just reachable without
//     the menu. Omitted (the common case): scenarioDifficulty stays 1.0,
//     byte-identical to pre-existing deep-link behavior.
//
// MAIN MENU (docs/CONCEPT.md "Design directions raised" — "Main-menu
// map/scenario picker": game/menu.js owns the scenario data + DOM overlay).
// The picker is the DEFAULT front door onto the exact map-boot code below —
// it is shown whenever there's no deep link, resolves a scenario's
// {seed|mapUrl, difficulty}, and this block then loads that map through the
// SAME loadGeneratedMap/loadMap calls the deep-link branch below already
// uses (no duplicated map-loading logic). ?seed=/?map= SKIP the menu
// entirely and boot exactly as before this menu existed — deep links are
// the dev/share path and must not regress. `scenarioDifficulty` defaults to
// 1.0 (a no-op multiplier) on that skip branch, so spawnEnemyLandingForce()
// below and the starting-economy nudge produce byte-identical numbers to
// pre-menu behavior. ?menu forces the picker open even over a deep link (a
// convenience for previewing the menu without editing the URL) — picking a
// scenario there overrides the deep-link params for that load.
const urlParams = new URLSearchParams(location.search);
const hasDeepLink = urlParams.has('seed') || urlParams.has('map');
let scenarioDifficulty = 1.0; // baseline enemy-force multiplier; see spawnEnemyLandingForce()
let scenarioId = null;
let map;
if (urlParams.has('menu') || !hasDeepLink) {
  const picked = await showMainMenu();
  scenarioDifficulty = picked.difficulty;
  scenarioId = picked.scenarioId;
  if (picked.mapUrl) {
    map = await loadMap(picked.mapUrl);
  } else {
    map = await loadGeneratedMap(picked.seed);
    console.log(`[mapgen] "${map.name}" (seed ${map.seed}) — ${map.genMs.toFixed(1)}ms, `
      + `${map.genStats.attempts} attempt(s), ${map.genStats.roadTileCount} road tiles — `
      + `scenario "${scenarioId}" (difficulty x${scenarioDifficulty})`);
  }
} else if (urlParams.has('map')) {
  map = await loadMap(urlParams.get('map'));
  if (urlParams.has('difficulty')) scenarioDifficulty = Number(urlParams.get('difficulty')) || 1.0;
} else {
  const seedParam = urlParams.get('seed');
  const seed = seedParam !== null && seedParam !== '' && !Number.isNaN(Number(seedParam))
    ? (Number(seedParam) >>> 0)
    : randomSeed();
  if (urlParams.has('difficulty')) scenarioDifficulty = Number(urlParams.get('difficulty')) || 1.0;
  map = await loadGeneratedMap(seed);
  console.log(`[mapgen] "${map.name}" (seed ${map.seed}) — ${map.genMs.toFixed(1)}ms, `
    + `${map.genStats.attempts} attempt(s), ${map.genStats.roadTileCount} road tiles`);
}
// PLAYABILITY v1 — every city starts player-owned regardless of map source
// (generated or ?map= authored), set once right after the map object
// exists and before anything else reads map.cities. See game/objectives.js.
initCityOwnership(map);

// FIRST-RUN INTRO (game/intro.js) — decided from the exact same
// urlParams/hasDeepLink the MAP SOURCE block above already computed: a deep
// link skips the intro for the same reason it skips the main menu (that's
// the dev/share path — land straight in the game), `?intro` forces it back
// on for testing/QA regardless of storage state. shouldShowIntro also
// checks a localStorage flag so a normal player only ever sees this once
// per browser, not on every load (see that function's own comment for the
// full "don't re-nag" rationale). Created here (state only, "should we run
// at all") so the decision is made right alongside the deep-link check it
// depends on; the actual sequence (opening the tech tree, the flourish, the
// handoff) is wired in the INTRO SEQUENCE section further down, once the
// systems it reuses exist.
const introState = createIntroState();
if (shouldShowIntro(urlParams, hasDeepLink)) startIntro(introState);

const renderer = createRenderer(canvas);
renderer.cam.x = map.worldW() / 2;
renderer.cam.y = map.worldH() / 2;
// frame the whole map on load (fit width AND height, with a little breathing
// room) so the whole island is visible instead of an arbitrary fixed zoom
renderer.cam.zoom = Math.max(0.08, Math.min(2,
  Math.min(canvas.clientWidth / map.worldW(), canvas.clientHeight / map.worldH()) * 0.9));

// world.sfx: COMBAT-FEEL/AUDIO EVENT QUEUE — plain-data records pushed by
// game/units.js at its fire and resolveHit sites ({kind:'fire'|'impact',
// weapon, x, y}), same shape convention as world.hits. Deliberately NOT
// consumed inside units.js itself (that file has zero import of game/audio.js
// or game/impactfx.js) — loop() below drains this array once per frame into
// sound (game/audio.js), lingering smoke/scorch (game/impactfx.js), and
// screen-shake trauma, then clears it. Keeping the queue on `world` (rather
// than a separate module-level array) is what lets game/buildings.js's gun
// emplacements — which call the exact same fireProjectile/resolveHit code
// path a unit does — get sound/shake/vfx for free with no separate wiring.
// world.battalions: B1 grouping layer (game/battalions.js) — formation
// records that own refs into world.units. Additive only this phase: nothing
// yet spawns through it (that's B2), see that file's header for the full
// contract.
// world.reinforcements: B2 queue (game/reinforcements.js) — in-flight
// {route, battalionType, timeLeft, totalTime, target} jobs from a
// requestElite/requestStandard/raiseMilitia call, ticked down by
// updateReinforcements() in loop() below until they spawn a real battalion.
const world = { units: [], projectiles: [], hits: [], buildings: [], sfx: [], battalions: [], reinforcements: [] };

// AMBIENT LIFE (game/ambient.js) — created once right alongside `world`,
// since it's precomputed off the same `map` (road network, cities/towns)
// this whole boot sequence already has in hand. See loop() below for the
// per-frame update/draw hooks; see that file's header for why this can
// never affect gameplay.
const ambient = initAmbient(map);

// LINGERING BATTLE VFX (game/impactfx.js) — smoke/scorch pools, spawned off
// world.sfx impact events in loop() below. Purely visual, see that file's
// header for the "never touches gameplay" contract.
const impactFx = createImpactFx();

// COMBAT AUDIO (game/audio.js) — kicks off the (best-effort, may 404 until
// the designer drops real files into assets/audio/) buffer loads now so
// they're ready well before the player's first click; actually hearing
// anything still waits on the browser's user-gesture unlock wired just below.
initAudio();
addEventListener('pointerdown', resumeAudio, { once: true });
addEventListener('keydown', resumeAudio, { once: true });

// SCREEN SHAKE — a small "trauma" model (Vlambeer's own term for this
// pattern): trauma accumulates on significant nearby impacts (see loop()'s
// world.sfx drain) and decays every frame; the camera offset is trauma
// SQUARED so small trauma reads as barely-there and only a real pile-up of
// nearby impacts pushes it toward the hard pixel cap below. Render-only —
// see engine/renderer.js's cam.shakeX/shakeY comment for why this can never
// perturb worldToScreen/screenToWorld (mouse/build placement math).
const shake = { trauma: 0 };
const SHAKE_MAX_PX = 6; // hard cap in CSS px before dpr scaling — "single-digit pixels," per the task brief, never more
const SHAKE_DECAY_PER_SEC = 8; // exponential decay rate; ~0.87 per 1/60s frame, close to the reference doc's "0.85/frame" figure
function addTrauma(amount) {
  shake.trauma = Math.min(1, shake.trauma + amount);
}
// Returns 1 at screen center, fading to 0 a bit past the corner — used to
// scale both shake trauma and (optionally) hit-stop triggering by how
// visible an event actually is, without duplicating game/audio.js's own
// world-space falloff math (screen space already bakes in cam.zoom for us).
function screenProximity(x, y) {
  const [sx, sy] = renderer.worldToScreen(x, y);
  const cx = renderer.canvas.width / 2, cy = renderer.canvas.height / 2;
  const maxR = Math.hypot(cx, cy) * 1.15; // a little past the corner so edge-of-screen impacts still register faintly
  if (maxR <= 0) return 0;
  const d = Math.hypot(sx - cx, sy - cy);
  return Math.max(0, 1 - d / maxR);
}
// Called every frame from loop() with the REAL (unscaled) frame delta —
// shake must keep moving at real cadence even if hit-stop is ever enabled
// and slows the sim's own dt (see HITSTOP_ENABLED below).
function updateShake(rawDt) {
  shake.trauma *= Math.exp(-SHAKE_DECAY_PER_SEC * rawDt);
  if (shake.trauma < 0.002) shake.trauma = 0;
  const shaped = shake.trauma * shake.trauma; // squared response — small trauma barely visible, per the "never arcadey" brief
  const mag = shaped * SHAKE_MAX_PX * devicePixelRatio;
  const ang = Math.random() * Math.PI * 2;
  renderer.cam.shakeX = Math.cos(ang) * mag;
  renderer.cam.shakeY = Math.sin(ang) * mag;
}

// HIT-STOP — brief real-time dt scale-down on a big, close explosion, per
// docs/reference-combat-feel-audio.md's #1 highest-ROI game-feel item. LEFT
// OFF BY DEFAULT: the mechanism scales the SAME `dt` every other system in
// loop() reads (economy/production/research/AI — not just combat draw code),
// and in a busy multi-front battle with several simultaneous big impacts
// that's a real risk of reading as a stutter bug rather than a weighty pause
// — exactly what the designer has repeatedly rejected as "arcadey." The
// mechanism below is fully implemented and rate-limited, so flipping this
// flag to true is a one-line, already-working change if the designer wants
// to try it; screen shake + audio + smoke/scorch already carry the
// "impact feels heavy" load on their own without it.
const HITSTOP_ENABLED = false;
const HITSTOP_SCALE = 0.15;    // sim runs at 15% speed while active — slowed, never fully frozen
const HITSTOP_DURATION = 0.055; // ~55ms of real time
const HITSTOP_COOLDOWN = 0.28;  // never re-trigger faster than this, however many impacts land in one frame
const hitstop = { timer: 0, cooldown: 0 };

// MOBILIZATION ECONOMY CORE (P3 — game/economy.js). Created once at load,
// ticked every frame in loop() below with ZERO player input required — a
// fresh game's mobilizationLevel/IC/manpower/militaryOutput all advance on
// their own from here. See game/economy.js's ECONOMY_TUNABLES for every
// tunable number (designer's to set).
const economy = createEconomy();
// SCENARIO DIFFICULTY -> STARTING ECONOMY (secondary knob per CONCEPT.md's
// menu bullet: "Difficulty scales concrete knobs (enemy landing size,
// starting stockpile, etc.)"). The PRIMARY knob is the enemy landing force
// (spawnEnemyLandingForce below); this is a small, clamped nudge so "Easy"
// also opens with a slightly fatter civilian cushion and "Brutal" a
// slightly leaner one. scenarioDifficulty defaults to 1.0 on every deep-
// link boot (no menu shown) -> mult is exactly 1 -> Math.round(x*1) === x,
// so ?seed=/?map= starting IC/manpower stay byte-identical to before this
// menu existed.
const startingEcoMult = Math.max(0.6, Math.min(1.3, 1 + (1 - scenarioDifficulty) * 0.4));
economy.ic = Math.round(economy.ic * startingEcoMult);
economy.manpower = Math.round(economy.manpower * startingEcoMult);

// RESEARCH STATE (P4 follow-up — game/research.js, CONCEPT.md's settled
// "Tech / research model" paragraph). Created once at load, ticked every
// frame in loop() below alongside the economy/production ticks — see the
// RESEARCH PANEL section further down for the UI that lets the player start
// a project, and game/production.js's updateProduction for the consuming
// side of the gate (a locked category's facility stalls with 'tech not
// researched' until this state says otherwise).
const researchState = createResearchState();

// GOAL STATE (Part A — game/tutorialplan.js, CONCEPT.md's settled
// "Onboarding / tutorial concept" paragraph beat 3: "the player ... selects
// what they want to end up making"). Created once at load; mutated from
// exactly one place — the tech tree view's click handler just below — and
// read by the checklist UI (GUIDANCE PANEL section further down) and Part
// B's off-track classifier (DIRECTOR section further down).
const goalState = createGoalState();

// DIRECTOR STATE (Part B — game/director.js). Created once at load; its
// toneMode/bubble/gag fields are all threaded through the DIRECTOR section
// further down (sell action, off-track hooks, the corner bubble render,
// the "are you five" dialog).
const directorState = createDirectorState();

// ---------------------------------------------------------------------------
// TECH/PRODUCTION TREE VIEW (CONCEPT.md's settled "Interactive in-game tech/
// production tree" direction — game/techtree.js builds the graph, game/
// techtreeview.js draws it). Created once at load, toggled by the G key
// (see the keydown handler below) — a dedicated full-screen canvas with its
// own pan/zoom camera, entirely independent of the main renderer's camera.
// getLiveCtx returns the SAME live world/economy/researchState/goalState
// objects the real game loop mutates every frame, so every time the view
// draws it reads current state — that's what makes the tree "fill out" as
// the player builds/researches, per the task's explicit payoff requirement,
// and what lets it draw goal markers on whatever's currently selected.
//
// onNodeClick is the GOAL-SELECTION half of the task ("click a finished-
// product node to toggle it as an active GOAL"): the view itself only ever
// reports "this node was clicked" (see techtreeview.js's own header comment
// on staying graphics-only) — this callback is where that turns into an
// actual goalState mutation, gated on isGoalNode so clicking a resource/
// building/tech node (a PREREQUISITE, never a goal itself) is silently a
// no-op rather than an error.
const techTreeCanvas = document.getElementById('techTreeCanvas');
const techTreeView = createTechTreeView(techTreeCanvas, () => ({ world, economy, researchState, goalState }), {
  onNodeClick(node) {
    if (isGoalNode(node)) toggleGoal(goalState, node.id);
  },
});
function toggleTechTree() {
  if (techTreeView.isOpen) { techTreeView.hide(); return; }
  // Full-screen exclusive overlay — close every other panel/mode first so
  // G never leaves a build/road/prod/import/tech panel fighting underneath
  // it, same "enter one mode, exit the others" convention R/B already use.
  if (roadMode.active) exitRoadMode();
  if (buildMode.active) exitBuildMode();
  if (sellMode.active) exitSellMode();
  if (prodPanel.classList.contains('open')) exitProdPanel();
  if (importPanel.classList.contains('open')) exitImportPanel();
  if (techPanel.classList.contains('open')) exitTechPanel();
  techTreeView.show();
}

// GAME PHASES (P4 — game/phase.js; CONCEPT.md's settled "Phases" line). A
// fresh game defaults to PREP, which the reconciliation call below drives
// straight into fogState.revealAll = true — the whole theater visible while
// building up, per Pillar 3's fog bullet. beginCombat() (wired to the C key
// and the on-screen button below) is the only other writer of phaseState.
const phaseState = createPhaseState();
applyPhaseFog(phaseState.phase, fogState);
// Transient center-screen banner for phase-transition announcements (and
// nothing else) — set text + expiry, drawn in the HUD block down in loop().
const announcement = { text: null, until: 0 };
function showAnnouncement(text, ms = 3200) {
  announcement.text = text;
  announcement.until = performance.now() + ms;
}
const phaseHud = document.getElementById('phaseHud');
const phaseLabel = document.getElementById('phaseLabel');
const phaseHint = document.getElementById('phaseHint');
const beginCombatBtn = document.getElementById('beginCombatBtn');
const announcementDiv = document.getElementById('announcement');
function updatePhaseHud() {
  const inCombat = phaseState.phase === PHASES.COMBAT;
  phaseHud.classList.toggle('combat', inCombat);
  phaseLabel.textContent = inCombat ? 'COMBAT' : 'PREPARATION';
  const fogTxt = fogState.revealAll ? 'fog OFF' : 'fog ON';
  phaseHint.textContent = `${fogTxt} — F: toggle fog  |  H: toggle guide${inCombat ? '' : '  |  C / button: begin combat'}`;
  beginCombatBtn.style.display = inCombat ? 'none' : '';
}
function triggerBeginCombat() {
  // The intro's beats assume PREP the whole way through (the tech tree/
  // flourish/handoff all play out over a peaceful map) — a no-op guard here
  // means an eager click on the always-visible #beginCombatBtn during
  // 'welcome'/'handoff' (the two beats where it's actually reachable — it's
  // physically covered by the full-screen tech tree canvas during
  // 'graph'/'discard') can't launch the invasion out from under the
  // sequence. Once introState.active goes false (Skip or the handoff's own
  // Continue), this is back to a normal no-op guard doing nothing.
  if (introState.active) return;
  // beginCombat() (game/phase.js) is a one-way PREP->COMBAT transition that
  // returns true ONLY on the actual transition (false every time after,
  // since it's a no-op once already in combat) — gating the enemy landing
  // spawn on that same return value is what makes "the invasion force
  // appears exactly once, the moment combat begins" fall out for free,
  // with no separate "already spawned" flag needed here.
  if (beginCombat(phaseState, fogState)) {
    spawnEnemyLandingForce();
    // Snapshot the invasion the instant it lands (AFTER the spawn) so the
    // "spent/routed" win condition (game/objectives.js) measures against the
    // real landing-force size for this run/difficulty.
    invasionState = initInvasion(world);
    showAnnouncement(COPY.COMBAT_BEGIN_ANNOUNCEMENT);
    updatePhaseHud();
  }
}
beginCombatBtn.addEventListener('click', triggerBeginCombat);
updatePhaseHud();

// ---------------------------------------------------------------------------
// PLAYABILITY v1 — OBJECTIVE HUD + VICTORY/DEFEAT END SCREEN (CONCEPT.md's
// settled "Playability v1 — closing the game loop" section). `gameOutcome`
// is null until game/objectives.js's evaluateOutcome fires once (see
// loop()'s `combatActive` block below), at which point it's 'victory' or
// 'defeat' PERMANENTLY for the rest of this run — the one-shot "the run
// ENDS" the task calls for, not a condition that could un-fire.
let gameOutcome = null;
// Per-run landing-force snapshot for the "invasion spent" win condition,
// created by triggerBeginCombat's initInvasion(world) and ticked each combat
// frame below. null until combat begins.
let invasionState = null;

const objectiveHud = document.getElementById('objectiveHud');
// Compact readout matching #econHud's own style/format (see index.html) —
// "am I winning / what's threatened" at a glance: which cities are held vs
// lost, the capital called out by name with its own status, and win
// progress as the enemy's remaining unit count. Hidden outside COMBAT
// (nothing to report before the invasion exists) — reused by loop() every
// frame same as updatePhaseHud/updateTechPanelUI already are.
function updateObjectiveHud() {
  const inCombat = phaseState.phase === PHASES.COMBAT;
  objectiveHud.classList.toggle('show', inCombat);
  if (!inCombat) return;
  const cities = map.cities || [];
  const held = cities.filter(c => c.owner === 'player').length;
  const lost = cities.filter(c => c.owner === 'enemy').length;
  const capital = cities[0];
  const capitalStatus = !capital ? 'n/a'
    : capital.owner === 'enemy' ? 'LOST'
    : capital.contested ? 'CONTESTED'
    : 'held';
  let enemyAlive = 0;
  for (const u of world.units) if (u.side === 'enemy' && u.hp > 0) enemyAlive++;
  objectiveHud.innerHTML =
    `<b>Objectives</b>\n`
    + `Capital (${capital ? capital.name : 'n/a'}): ${capitalStatus}\n`
    + `Cities held: ${held}/${cities.length}  (lost: ${lost})\n`
    + `Enemy remaining: ${enemyAlive}`;
}

const outcomeScreenEl = document.getElementById('outcomeScreen');
const outcomeTitleEl = document.getElementById('outcomeTitle');
const outcomeSubtitleEl = document.getElementById('outcomeSubtitle');
const outcomeStatsEl = document.getElementById('outcomeStats');
const outcomeRestartBtn = document.getElementById('outcomeRestartBtn');
const outcomeMenuBtn = document.getElementById('outcomeMenuBtn');
outcomeRestartBtn.textContent = COPY.OUTCOME_RESTART_BUTTON;
outcomeMenuBtn.textContent = COPY.OUTCOME_MENU_BUTTON;
// PLAY AGAIN: reload exactly as-is (same seed/map/difficulty deep link, or
// the same menu-picked scenario re-rolled through a fresh boot) — the
// simplest robust "reset the run" available, same shortcut the director's
// own "fired -> restart" gag-chain path already uses (location.reload()).
outcomeRestartBtn.addEventListener('click', () => location.reload());
// MAIN MENU: force the scenario picker open on the next load regardless of
// whether this run was a deep link — `?menu` is the exact override
// main.js's own MAP SOURCE section already honors ("forces the picker open
// even over a deep link"), so this is just navigating to that same URL
// shape rather than a second menu-opening code path.
outcomeMenuBtn.addEventListener('click', () => { location.href = `${location.pathname}?menu`; });

// Shown exactly once (gameOutcome flips from null to a value exactly once —
// see loop()) — fills in the title/subtitle for whichever outcome fired and
// a small stats readout (cities held, capital fate) so the end screen
// summarizes the war, not just its verdict.
function showOutcomeScreen(outcome) {
  outcomeScreenEl.classList.remove('victory', 'defeat');
  outcomeScreenEl.classList.add(outcome); // 'victory' | 'defeat' — drives title color via CSS
  outcomeTitleEl.textContent = outcome === 'victory' ? COPY.VICTORY_TITLE : COPY.DEFEAT_TITLE;
  outcomeSubtitleEl.textContent = outcome === 'victory' ? COPY.VICTORY_SUBTITLE : COPY.DEFEAT_SUBTITLE;
  const cities = map.cities || [];
  const held = cities.filter(c => c.owner === 'player').length;
  const capital = cities[0];
  let enemyAlive = 0;
  for (const u of world.units) if (u.side === 'enemy' && u.hp > 0) enemyAlive++;
  outcomeStatsEl.innerHTML =
    `Cities held: ${held}/${cities.length}<br>`
    + `Capital: ${capital ? (capital.owner === 'player' ? 'held' : 'lost') : 'n/a'}<br>`
    + `Enemy units remaining: ${enemyAlive}`;
  outcomeScreenEl.classList.add('open');
}

// find the nearest water tile to a world point (spiral ring search over the
// grid) — used both to place the demo gunboat offshore and to snap naval
// move orders onto water, since a naval unit can't be ordered onto land.
function nearestWaterPoint(wx, wy, maxTiles = 40) {
  const ts = map.tileSize;
  const gx0 = Math.floor(wx / ts), gy0 = Math.floor(wy / ts);
  for (let r = 0; r <= maxTiles; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const t = map.terrainAt(gx0 + dx, gy0 + dy);
        if (t && t.water) return { x: (gx0 + dx + 0.5) * ts, y: (gy0 + dy + 0.5) * ts };
      }
    }
  }
  return { x: wx, y: wy }; // no water found within range: fall back to the raw point
}

// Inverse of nearestWaterPoint: nearest tile a GROUND unit could actually
// stand on (not water, not impassable terrain like mountain — read off
// t.moveMult generically rather than hardcoding a terrain-type name, since
// the map's terrain and layout are procedural per run). The demo spawn
// offsets below are chosen to land on land for a "typical" generated
// coastline, but a run's actual geometry is never known ahead of time, so
// every ground-unit demo spawn point is snapped through this — a no-op
// when the raw point is already fine, a rescue when a tight beach/city
// shape happens to put it a tile into the sea or up a mountainside.
function nearestLandPoint(wx, wy, maxTiles = 40) {
  const ts = map.tileSize;
  const gx0 = Math.floor(wx / ts), gy0 = Math.floor(wy / ts);
  for (let r = 0; r <= maxTiles; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const t = map.terrainAt(gx0 + dx, gy0 + dy);
        if (t && !t.water && (t.moveMult ?? 1) > 0) return { x: (gx0 + dx + 0.5) * ts, y: (gy0 + dy + 0.5) * ts };
      }
    }
  }
  return { x: wx, y: wy };
}
// Convenience: spawn a ground unit at (wx,wy) rescued onto the nearest legal
// land tile if needed. Air units don't go through this (moveClass ignores
// terrain entirely, per units.js terrainSample), so callers below only wrap
// the ground-class spawns.
function spawnGroundUnit(key, wx, wy, side) {
  const p = nearestLandPoint(wx, wy);
  return spawnUnit(world, key, p.x, p.y, side);
}

// pBase/eBase come straight from whatever map.spawns the generator produced
// THIS run (game/mapgen.js) — nothing below assumes a specific island's
// geometry, every spawn point is either land-snapped (spawnGroundUnit) or
// water-snapped (nearestWaterPoint) before use.
const [pSpawn, eSpawn] = map.spawns;
const pBase = { x: pSpawn.x * map.tileSize, y: pSpawn.y * map.tileSize };
const eBase = { x: eSpawn.x * map.tileSize, y: eSpawn.y * map.tileSize };

// ---------------------------------------------------------------------------
// STARTING INDUSTRY (CONCEPT.md Pillar 2: "New military industry is
// constructed alongside the old economy, not instead of it" — mobilization
// converts AND builds onto an economy that already exists, it doesn't start
// from a bare field). A fresh game opens with a SMALL slice of that
// pre-existing civilian base already standing near the player's spawn: one
// factory, one barracks. Sited through the exact same delegated-placement
// planner the B-mode buildbar uses (sitePlacement — see game/buildings.js),
// clicked (in effect) at map.spawns[0] instead of a real mouse click, so
// this lands on legal, buildable ground on ANY generated map regardless of
// that run's actual coastline/terrain. Force-completed right after spawning
// (skip the constructing/HP-floor state a player-placed building goes
// through) because this industry was already standing when the game opened
// — it never went through a construction timer the player watched.
function placeEstablishedBuilding(key, wx, wy) {
  const site = sitePlacement(map, key, wx, wy);
  if (!site) {
    // Extremely unlikely (spawn tiles are chosen buildable by the
    // generator) but not impossible on a pathological seed — degrade to "no
    // starting building there" rather than throwing and blanking the game.
    console.warn(`[startingIndustry] no legal site found for "${key}" near the player spawn — skipping`);
    return null;
  }
  const b = spawnBuilding(world, map, key, site.x, site.y, 'player');
  b.status = 'complete';
  b.buildProgress = 1;
  b.hp = b.maxHp;
  return b;
}
placeEstablishedBuilding('factory', pBase.x, pBase.y);
placeEstablishedBuilding('barracks', pBase.x, pBase.y);

// A modest starting GARRISON — CONCEPT.md's "first-run is open, not
// dictated" paragraph: the player already commands a small defensive force
// from minute one, not the former full combined-arms roster (that was a
// system-exerciser, not an opening). Heavy industry, production, and the
// enemy itself are all left for the run to develop, per the guidance panel
// below.
for (let i = 0; i < 2; i++) spawnGroundUnit('infantry', pBase.x - 50 + i * 22, pBase.y + 60, 'player');
for (let i = 0; i < 2; i++) spawnGroundUnit('militia', pBase.x - 50 + i * 22, pBase.y + 84, 'player');
for (let i = 0; i < 2; i++) spawnGroundUnit('tank', pBase.x + 20 + i * 26, pBase.y + 60, 'player');

// ---------------------------------------------------------------------------
// ENEMY LANDING FORCE — phase-gated (CONCEPT.md's settled "Phases" line: "a
// run has a PREPARATION phase (build-up, fog off) and a COMBAT phase (fog
// on, the enemy arrives)"). Deliberately NOT spawned here at load — the
// whole point of the phase split is that PREP is a peaceful build-up with no
// enemy on the map. This function is only ever invoked from
// triggerBeginCombat above, itself gated on beginCombat()'s one-shot return
// value, so the invasion force appears exactly once, the instant COMBAT
// actually begins (C key / BEGIN COMBAT button / window.__debug.phase.beginCombat
// — all three routes go through the same triggerBeginCombat).
//
// Composition mirrors the former combined-arms demo's enemy roster (mixed
// ground + air, so SAM/AA/fighter targeting is exercised the moment the
// player has anything built to exercise it against) — it just no longer
// camps on the map for the whole prep phase first. eBase sits on a coastal
// beach tile near the map's farthest-from-capital city (game/mapgen.js's
// enemySpawn/nearestBeach); every ground spawn goes through spawnGroundUnit
// so it lands on legal terrain regardless of that run's exact beach/city
// shape.
//
// DIFFICULTY -> LANDING FORCE SIZE (the PRIMARY knob per the menu-picker
// task spec). ENEMY_LANDING_BASE holds the baseline (difficulty x1.0, i.e.
// exactly the counts this function used before scenarioDifficulty existed)
// per-unit-type count; scaledCount() multiplies by the chosen scenario's
// difficulty and rounds, floored at 1 so even "Easy" still fields one of
// every type (SAM/AA/fighter targeting stays exercised on every difficulty,
// per the comment above) while "Brutal" fields visibly more of each. A
// deep-link boot (?seed=/?map=, no menu shown) leaves scenarioDifficulty at
// its 1.0 default, so scaledCount(base, 1) === base for every entry here —
// identical spawn counts to before this menu existed.
// BALANCE-TUNED (Playability v1 balance pass): the ground assault was raised
// (tank 3->6, infantry/militia 2->3) because the balance harness showed the
// bare starting garrison out-fought the old landing force and held the capital
// untouched — so an UNPREPARED player could "win" by turtling, which kills the
// whole "prep decides the outcome" bet. These counts are the difficulty-1.0
// baseline; scaledCount() multiplies by the scenario's difficulty. Still the
// designer's to fine-tune by feel against real play across seeds.
const ENEMY_LANDING_BASE = { tank: 6, infantry: 3, militia: 3, aa: 1, fighter: 1, strikejet: 1 };
function scaledCount(key) {
  return Math.max(1, Math.round(ENEMY_LANDING_BASE[key] * scenarioDifficulty));
}
function spawnEnemyLandingForce() {
  const nTank = scaledCount('tank'), nInf = scaledCount('infantry'), nMil = scaledCount('militia');
  const nAa = scaledCount('aa'), nFighter = scaledCount('fighter'), nStrike = scaledCount('strikejet');
  for (let i = 0; i < nTank; i++) spawnGroundUnit('tank', eBase.x - i * 26, eBase.y + (i % 2) * 26, 'enemy');
  for (let i = 0; i < nInf; i++) spawnGroundUnit('infantry', eBase.x - 60 - i * 22, eBase.y + 10, 'enemy');
  for (let i = 0; i < nMil; i++) spawnGroundUnit('militia', eBase.x - 60 - i * 22, eBase.y + 34, 'enemy');
  for (let i = 0; i < nAa; i++) spawnGroundUnit('aa', eBase.x + 20 + i * 24, eBase.y - 30, 'enemy');
  for (let i = 0; i < nFighter; i++) spawnUnit(world, 'fighter', eBase.x - 20 - i * 30, eBase.y - 100, 'enemy');
  for (let i = 0; i < nStrike; i++) spawnUnit(world, 'strikejet', eBase.x + 35 + i * 30, eBase.y - 115, 'enemy');

  // PLAYABILITY v1 PART B — the landing force is now a FUNCTIONAL AGGRESSOR
  // (CONCEPT.md's settled "Playability v1" section), not a one-shot
  // scripted order toward the player's spawn point. game/enemyai.js's
  // updateEnemyAI runs every frame during combat (see loop()'s
  // `combatActive` block below) and assigns each landed unit a real
  // objective — the nearest uncaptured city, biased toward the capital —
  // using the exact same order/pathfinding/targeting machinery a player
  // order already goes through. No per-frame scripted order is issued here
  // at spawn time; the AI takes over from the unit's landing position on
  // its very first update tick.
}

let hovered = null, lastMouse = { x: 0, y: 0 };
canvas.addEventListener('mousemove', e => {
  lastMouse = { x: e.clientX, y: e.clientY };
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  let best = null, bd = 20 * 20;
  for (const u of world.units) {
    // fog: an undetected enemy can't be hovered/stat-carded — the player
    // shouldn't be able to mouse-probe the fog for free intel.
    if (u.side !== 'player' && !detects('player', u)) continue;
    const dd = (u.x - wx) ** 2 + (u.y - wy) ** 2;
    if (dd < bd) { bd = dd; best = u; }
  }
  // buildings are big — hover-test their whole footprint rect, not just a
  // point-radius around their center, so a factory is hoverable across its
  // whole body the way a unit is within its (much smaller) radius.
  for (const b of world.buildings) {
    if (b.side !== 'player' && !detects('player', b)) continue;
    const ts = map.tileSize;
    const x0 = b.gx * ts, y0 = b.gy * ts, x1 = x0 + b.def.footprint.w * ts, y1 = y0 + b.def.footprint.h * ts;
    if (wx >= x0 && wx <= x1 && wy >= y0 && wy <= y1) best = b;
  }
  hovered = best;

  // ROAD MODE: after point A is placed, re-route the preview live under the
  // cursor so the player sees what the planner would actually build before
  // committing with the second click. Only recomputes when the cursor moves
  // to a new tile (not every pixel) — cheap enough not to matter at this
  // grid size, but no reason to solve A* every mousemove either.
  if (roadMode.active && roadMode.a) {
    const ts = map.tileSize;
    const gx = Math.floor(wx / ts), gy = Math.floor(wy / ts);
    if (!roadMode.previewGoalTile || roadMode.previewGoalTile.x !== gx || roadMode.previewGoalTile.y !== gy) {
      roadMode.previewGoalTile = { x: gx, y: gy };
      const route = findRoadRoute(map, roadMode.a.x, roadMode.a.y, wx, wy);
      roadMode.previewPath = route ? route.waypoints : null;
    }
  }

  // B MODE: track the footprint tile under the cursor so the frame draw can
  // paint a green/red preview — validity is the literal same isValidPlacement
  // rule the pin-override (shift-click) enforces, so the preview never lies
  // about what a pin would do (a planner-sited normal click may still move
  // the building off this exact spot if it can find a better-scored one).
  if (buildMode.active && buildMode.selectedType) {
    const ts = map.tileSize;
    const def = BUILDING_DEFS[buildMode.selectedType];
    const gx = Math.round(wx / ts - def.footprint.w / 2);
    const gy = Math.round(wy / ts - def.footprint.h / 2);
    buildMode.previewGx = gx;
    buildMode.previewGy = gy;
    buildMode.previewValid = isValidPlacement(map, gx, gy, def);
  }
});
attachCameraControls(canvas, renderer.cam, { isEntityHit: () => !!hovered });

// right-click issues a move order to the player's units (basic input, proves
// the `order` field). Routed by move-class attribute, not per-unit code:
// ground and air units take the raw click point (ground wall-slides off
// impassable terrain on its own; air ignores terrain entirely); naval units
// only ever accept a water point, so their order gets snapped to the
// nearest water tile — ordering a gunboat onto a beach just parks it
// offshore of where you clicked instead of doing nothing.
//
// While ROAD MODE is active, right-click means "cancel" instead (per spec:
// Esc/right-click exits road mode) — it does not also issue a move order.
// Extracted from the contextmenu handler so a headless test can drive the
// exact same coalescing path without needing to fake mouse/camera math
// (see window.__debug.issueMoveOrder below).
function issueMoveOrder(wx, wy) {
  // COALESCE: a mass order (say, 15 units at once) shouldn't trigger 15
  // separate A* solves next frame just because per-unit jitter happens to
  // spill a few of them into a neighboring goal tile. Pre-warm the path
  // cache ONCE per ground move class actually present in this order,
  // solved from that class's own centroid to the un-jittered click point —
  // every unit of that class then hits the same cache entry in its own
  // lazy ensurePath() call next frame (game/units.js), each picking its own
  // nearest-waypoint entry point onto the shared route (the "per-unit
  // offset" half of coalescing).
  const groups = new Map(); // moveClassName -> {sumX, sumY, n, moveClass}
  for (const u of world.units) {
    if (u.side !== 'player') continue;
    const mc = MOVE_CLASSES[u.def.moveClass];
    if (!mc.ground) continue;
    let g = groups.get(u.def.moveClass);
    if (!g) { g = { sumX: 0, sumY: 0, n: 0, moveClass: mc }; groups.set(u.def.moveClass, g); }
    g.sumX += u.x; g.sumY += u.y; g.n++;
  }
  for (const [moveClassName, g] of groups) {
    findPathCached(map, moveClassName, g.moveClass, g.sumX / g.n, g.sumY / g.n, wx, wy);
  }

  for (const u of world.units) {
    if (u.side !== 'player') continue;
    const jx = wx + (Math.random() - 0.5) * 12, jy = wy + (Math.random() - 0.5) * 12;
    u.order = MOVE_CLASSES[u.def.moveClass].requiresWater ? nearestWaterPoint(jx, jy) : { x: jx, y: jy };
  }
}

canvas.addEventListener('contextmenu', e => {
  e.preventDefault();
  if (roadMode.active) { exitRoadMode(); return; }
  if (buildMode.active) { exitBuildMode(); return; }
  if (battalionAssignMode.active) { exitBattalionAssignMode(); return; }
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  issueMoveOrder(wx, wy);
});

// ---------------------------------------------------------------------------
// ROAD MODE (P2 — "The Map Is a Machine" / Pillar 1's delegated construction:
// the player states intent — point A to point B — and the nation's planners
// route the actual road via the same A* cost logic pathfinding uses, not a
// straight line). Press R to enter/exit; while active, click point A, then
// point B; Esc or right-click cancels a placement or exits the mode.
const roadMode = { active: false, a: null, previewPath: null, previewGoalTile: null };

function exitRoadMode() {
  roadMode.active = false;
  roadMode.a = null;
  roadMode.previewPath = null;
  roadMode.previewGoalTile = null;
}
function enterRoadMode() {
  if (sellMode.active) exitSellMode();
  if (battalionAssignMode.active) exitBattalionAssignMode();
  roadMode.active = true;
  roadMode.a = null;
  roadMode.previewPath = null;
  roadMode.previewGoalTile = null;
}

// ---------------------------------------------------------------------------
// BUILD MODE (P2 city layer — CONCEPT.md Pillar 1's signature verb,
// "delegated construction"): press B to open a minimal DOM strip of building
// types, click a type, then click a rough location — the NATION'S PLANNERS
// site the actual placement (game/buildings.js sitePlacement, spiral search
// scored by buildable terrain + no overlap + a mild near-road/near-city
// preference). Shift-click is the pressure-valve pin override: place exactly
// at the clicked tile if valid, else reject with a brief on-screen message.
// Construction is instant/free (no economy until P3, per CONCEPT.md).
//
// R and B are made mutually exclusive at the input level (entering one exits
// the other first) so they never fight over clicks/Esc.
const buildMode = {
  active: false, selectedType: null,
  previewGx: undefined, previewGy: undefined, previewValid: false,
  message: null, messageUntil: 0,
};

// CONSTRUCTION LEDGER — playtest feedback ("the build menu is really ugly,
// you can't scroll down on it... group buildings"). Buildings are grouped
// into game/buildingCategories.js's BUILD_CATEGORIES (pure data — see that
// file's header) into one labeled "ledger section" per category inside a
// single scrollable body, instead of one flat unscrollable strip of ~20
// buttons. buildBtns maps key -> button so selection/grey-out/next-needed
// state can be refreshed without re-scanning the DOM by className.
const buildbarBody = document.getElementById('buildbarBody');
const buildBtns = {};
for (const cat of BUILD_CATEGORIES) {
  const group = document.createElement('div');
  group.className = 'buildCatGroup';
  const label = document.createElement('span');
  label.className = 'buildCatLabel';
  label.textContent = cat.label;
  const row = document.createElement('div');
  row.className = 'buildCatRow';
  for (const key of cat.keys) {
    const def = BUILDING_DEFS[key];
    if (!def) continue; // defensive: a stale category entry shouldn't crash the ledger
    const btn = document.createElement('button');
    btn.className = 'opsBtn buildBtn';
    btn.textContent = def.name;
    btn.dataset.key = key;
    btn.addEventListener('click', () => selectBuildType(key));
    row.appendChild(btn);
    buildBtns[key] = btn;
  }
  group.append(label, row);
  buildbarBody.appendChild(group);
}
// Defensive dev-time check (console only, never blocks play): every
// BUILDING_DEFS entry should appear in exactly one category, so a future
// building added to buildings.js without a matching buildingCategories.js
// entry doesn't just silently vanish from the ledger.
for (const key of Object.keys(BUILDING_DEFS)) {
  if (!buildBtns[key]) console.warn(`[buildbar] "${key}" has no BUILD_CATEGORIES entry — it won't appear in the construction ledger.`);
}

function updateBuildBarUI() {
  for (const key of Object.keys(buildBtns)) buildBtns[key].classList.toggle('selected', key === buildMode.selectedType);
}

// ---------------------------------------------------------------------------
// PLAN-DRIVEN GREY-OUT + NEXT-NEEDED HIGHLIGHT (playtest feedback: "grey out
// things you aren't building" / "highlight what you currently need"). Reads
// the SAME live plan game/tutorialplan.js's computePlan() already produces
// for the guidance checklist — no separate notion of "relevant" is invented
// here, so the ledger and the checklist can never disagree about what the
// plan wants. A step's nodeId is `${kind}:${key}` (see game/techtree.js) so
// the key is recovered generically by stripping the kind prefix — no
// per-building-name branching anywhere in this logic.
function stepKey(step) { return step.nodeId.slice(step.kind.length + 1); }
// Which building KEYS the current plan touches, directly or indirectly:
//   - a 'building' step names its key directly.
//   - a 'category' step (e.g. "get Missiles production online") is
//     satisfied by building that category's FACILITY (PRODUCTION_DEFS'
//     data), so the facility reads as plan-relevant too.
//   - a 'resource' step (e.g. "secure titanium") is satisfied by mining a
//     deposit, which always goes through the one generic `mine` building
//     (game/buildings.js) — so `mine` reads as relevant whenever ANY
//     resource step is outstanding.
function planBuildingKeys(plan) {
  const relevant = new Set();
  for (const s of plan.steps) {
    if (s.kind === 'building') relevant.add(stepKey(s));
    else if (s.kind === 'category') {
      const facility = PRODUCTION_DEFS[stepKey(s)]?.facility;
      if (facility) relevant.add(facility);
    } else if (s.kind === 'resource') {
      relevant.add('mine');
    }
  }
  return relevant;
}
// The single building key the FIRST unsatisfied step in the plan's order
// implies — the "highlight what the goal currently needs" requirement, tied
// (per the task) to the plan's first unsatisfied step specifically, not just
// "anything relevant." Returns null if the plan is empty/fully satisfied, or
// if the next step is a 'tech' (no single building represents "research
// this" — the research panel already surfaces that).
function nextNeededBuildingKey(plan) {
  const next = plan.steps.find(s => !s.satisfied);
  if (!next) return null;
  if (next.kind === 'building') return stepKey(next);
  if (next.kind === 'category') return PRODUCTION_DEFS[stepKey(next)]?.facility || null;
  if (next.kind === 'resource') return 'mine';
  return null; // 'tech' — no single building stands in for "go research this"
}
// Refreshed every frame from the SAME plan object updateGuidancePanel just
// computed (see loop() below) — cheap (20 class toggles), and guarantees the
// ledger and the checklist are always looking at identical plan state. With
// NO active goal (plan.goals.length === 0) every button reads normal, per
// the task's explicit "with no active goal, everything reads normal."
function updateBuildBarPlanUI(plan) {
  const hasGoals = plan.goals.length > 0;
  const relevant = hasGoals ? planBuildingKeys(plan) : null;
  const nextKey = hasGoals ? nextNeededBuildingKey(plan) : null;
  for (const key of Object.keys(buildBtns)) {
    const btn = buildBtns[key];
    btn.classList.toggle('dim', hasGoals && !relevant.has(key));
    btn.classList.toggle('next', key === nextKey);
  }
}
// The RESOURCE id the plan's first unsatisfied step needs mined, or null —
// feeds drawDepositFocus's "NEEDED" ring on the map (the on-map half of
// "highlight what you currently need"; nextNeededBuildingKey above is the
// menu half). Only meaningful when the next step is actually a 'resource'
// step; a building/category/tech next-step has no single deposit to ring.
function nextNeededResourceType(plan) {
  const next = plan.steps.find(s => !s.satisfied);
  return next && next.kind === 'resource' ? stepKey(next) : null;
}
function selectBuildType(key) {
  buildMode.selectedType = key;
  buildMode.previewGx = undefined;
  updateBuildBarUI();
}
function enterBuildMode() {
  if (sellMode.active) exitSellMode();
  if (battalionAssignMode.active) exitBattalionAssignMode();
  buildMode.active = true;
  buildbar.classList.add('open');
  updateBuildBarUI();
}
function exitBuildMode() {
  buildMode.active = false;
  buildMode.selectedType = null;
  buildMode.previewGx = undefined;
  buildbar.classList.remove('open');
}
function flashMessage(text) {
  buildMode.message = text;
  buildMode.messageUntil = performance.now() + 2400;
}

// Left-click while in B mode: DELEGATED placement (CONCEPT.md Pillar 1) —
// the planner's spiral search (game/buildings.js sitePlacement) picks the
// actual site near the click. Shift-click is the pin override: place
// exactly at the hovered footprint tile (buildMode.previewGx/Gy, the same
// tile the green/red preview is drawn from) or reject with a brief message
// if that exact spot is invalid — no silent fallback to the planner.
//
// P3: construction now costs IC (+manpower for one building type) and takes
// build time — checked/spent here via game/economy.js BEFORE spawnBuilding
// is called; an unaffordable placement is rejected with a brief message and
// nothing is spent or spawned, same shape as the existing "invalid site"
// rejection above it.
function affordabilityMessage(key, def) {
  const cost = buildingCost(key);
  const parts = [`${cost.ic || 0} IC`];
  if (cost.manpower) parts.push(`${cost.manpower} manpower`);
  return `Can't afford ${def.name} — needs ${parts.join(' + ')} `
    + `(have ${Math.floor(economy.ic)} IC, ${Math.floor(economy.manpower)} manpower).`;
}
canvas.addEventListener('click', e => {
  if (!buildMode.active || !buildMode.selectedType) return;
  const key = buildMode.selectedType;
  const def = BUILDING_DEFS[key];
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  if (e.shiftKey) {
    const gx = buildMode.previewGx, gy = buildMode.previewGy;
    if (gx === undefined || !isValidPlacement(map, gx, gy, def)) {
      // Mines carry an extra siting rule (def.requiresDeposit — see
      // game/buildings.js) on top of the generic buildable/no-overlap check:
      // call it out specifically so "why was this rejected" is legible
      // rather than folding into the same generic message every other
      // building type gets.
      const depositIssue = def.requiresDeposit && gx !== undefined && !footprintHasDeposit(map, gx, gy, def);
      flashMessage(depositIssue
        ? `Can't pin ${def.name} there — no resource deposit on/adjacent to that site.`
        : `Can't pin ${def.name} there — invalid site.`);
      return;
    }
    if (!canAffordBuilding(economy, key)) {
      flashMessage(affordabilityMessage(key, def));
      return;
    }
    spendForBuilding(economy, key);
    spawnBuilding(world, map, key, gx, gy, 'player');
    flashMessage(`${def.name} pinned — under construction.`);
    // DIRECTOR ACTION POINT (Part B point 1 of 4: "a building placed") —
    // classify against the current plan and surface an acknowledgement.
    reactToAction(directorState, { kind: 'building', key }, getCurrentPlan());
  } else {
    const site = sitePlacement(map, key, wx, wy);
    if (!site) {
      flashMessage(def.requiresDeposit
        ? `No resource deposit found near there for ${def.name} — mines must site on/adjacent to a deposit.`
        : `No buildable site found near there for ${def.name}.`);
      return;
    }
    if (!canAffordBuilding(economy, key)) {
      flashMessage(affordabilityMessage(key, def));
      return;
    }
    spendForBuilding(economy, key);
    spawnBuilding(world, map, key, site.x, site.y, 'player');
    flashMessage(`${def.name} sited by the planners — under construction.`);
    reactToAction(directorState, { kind: 'building', key }, getCurrentPlan());
  }
});

// ---------------------------------------------------------------------------
// SELL / DEMOLISH MODE (Part B follow-up — game/buildings.js sellBuilding).
// Press X to toggle; while active, click a PLAYER building to sell it for a
// partial IC refund. Minimal on purpose (a mode + a click, same shape as
// B/R above) — its main job is being a real action point Part B's reactive
// director (and its silly-mode gag chain) can hook, but it's also just a
// generically useful "reclaim a mis-sited building" tool on its own.
const sellMode = { active: false };
function enterSellMode() {
  if (roadMode.active) exitRoadMode();
  if (buildMode.active) exitBuildMode();
  if (battalionAssignMode.active) exitBattalionAssignMode();
  sellMode.active = true;
}
function exitSellMode() { sellMode.active = false; }

// Same footprint hit-test shape as the mousemove hover handler above (world-
// space rect, not a point-radius) so "click the building" feels consistent
// with "hover the building" elsewhere on this canvas.
function playerBuildingAt(wx, wy) {
  const ts = map.tileSize;
  for (const b of world.buildings) {
    if (b.side !== 'player') continue;
    const x0 = b.gx * ts, y0 = b.gy * ts, x1 = x0 + b.def.footprint.w * ts, y1 = y0 + b.def.footprint.h * ts;
    if (wx >= x0 && wx <= x1 && wy >= y0 && wy <= y1) return b;
  }
  return null;
}

// Sells `b` and routes the result through game/director.js's gag-chain
// state machine (Part B point 6) — see handleBuildingSold's own header
// comment for the tri-state contract this switches on. Reads b.key/def/x/y/
// gx/gy/id BEFORE they'd matter for anything else: sellBuilding removes `b`
// from world.buildings, but the JS object itself (and every field read
// below) stays alive, so reading them AFTER the sell is safe — kept in this
// order only because it reads naturally as "sell it, then react to having
// sold it."
function performSell(b) {
  const name = b.def.name, key = b.key;
  const refund = sellBuilding(world, map, b, economy);
  const gagResult = handleBuildingSold(directorState, world, map, economy, b);
  if (gagResult.type === 'dialog') {
    showAreYouFiveDialog();
  } else if (gagResult.type === 'pass') {
    // DIRECTOR ACTION POINT (Part B point 4 of 4: "a building sold/deleted")
    // — no gag-chain line already covered this sale, so give it the same
    // plan-relevance treatment every other action gets.
    reactToAction(directorState, { kind: 'sell', key }, getCurrentPlan());
  }
  // 'handled' -> handleBuildingSold already set the bubble line (the
  // serious-mode neutral ack, or the silly-mode "confused, replaced" line).
  flashMessage(`Sold ${name} (+${refund} IC).`);
}

canvas.addEventListener('click', e => {
  if (!sellMode.active) return;
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  const b = playerBuildingAt(wx, wy);
  if (!b) { flashMessage('No building there to sell.'); return; }
  performSell(b);
});

// ---------------------------------------------------------------------------
// "CHOOSE WHAT TO PRODUCE" PANEL (P4 — CONCEPT.md's settled "Production &
// supply chain" ledger: "you pick a production category ... see its recipe
// ... build the factory"). Press P to open; click a category button and (a)
// its recipe/requirements are shown in #recipeInfo BEFORE anything is spent
// or placed, so the player learns the supply chain they need to build, and
// (b) B-mode is armed with that category's facility preselected — placing
// it then goes through the EXACT SAME costed+timed construction pipeline
// (isValidPlacement/sitePlacement/spawnBuilding) every other building uses;
// this panel is purely a friendlier front door onto that same buildbar
// entry, not a separate placement path.
const prodPanel = document.getElementById('prodPanel');
const prodCatRow = document.getElementById('prodCatRow');
const recipeInfo = document.getElementById('recipeInfo');
let selectedCategory = null;

function resourceCostText(resourceCostPerUnit, unitNoun = 'unit') {
  return Object.entries(resourceCostPerUnit || {})
    .map(([rid, amt]) => `${RESOURCE_DEFS[rid] ? RESOURCE_DEFS[rid].name : rid} ${amt}/${unitNoun}`)
    .join(', ') || 'none';
}
function recipeHtml(category) {
  const prod = PRODUCTION_DEFS[category];
  const facilityDef = BUILDING_DEFS[prod.facility];
  const prereq = prod.recipe.prerequisiteBuildings.length
    ? prod.recipe.prerequisiteBuildings.map(k => BUILDING_DEFS[k].name).join(', ')
    : 'none';
  // Part B: a component category (electronics/advancedAlloy) produces a
  // RESOURCE into the stockpile rather than a unit — read that generically
  // off producesResource so this panel doesn't need a per-category branch,
  // same "outputs" concept, different destination.
  const outputs = prod.producesResource
    ? `${RESOURCE_DEFS[prod.producesResource.resourceId].name} (+${prod.producesResource.amountPerCycle}/cycle)`
    : prod.outputs.map(k => UNIT_DEFS[k].name).join(', ');
  const unitNoun = prod.producesResource ? 'cycle' : 'unit';
  const facCost = facilityDef.cost || {};
  return `<b>${prod.name}</b> — produces: ${outputs}\n`
    + `Facility: ${facilityDef.name} (${facCost.ic || 0} IC${facCost.manpower ? ` + ${facCost.manpower} manpower` : ''}, ${facilityDef.buildTime}s to build)\n`
    + `Resources/${unitNoun}: ${resourceCostText(prod.recipe.resourceCostPerUnit, unitNoun)}\n`
    + `Prerequisite buildings: ${prereq}\n`
    + `IC/${unitNoun}: ${prod.recipe.icCostPerUnit}   Manpower/${unitNoun}: ${prod.recipe.manpowerCostPerUnit}\n`
    + `Rate at full supply: 1 ${unitNoun} / ${prod.recipe.buildTimePerUnit}s`;
}
// Shared by the econ HUD's Production block and the on-map facility label
// (game/production.js's per-building state) — "N built" reads fine for a
// unit-producing facility, but a Part B component facility's b.producedCount
// counts CYCLES, not finished units, so it needs its own wording; this is
// the one place both call sites branch on producesResource so they can't
// drift out of sync with each other.
function productionCountLabel(prod, b) {
  if (!prod.producesResource) return `${b.producedCount} built`;
  const { resourceId, amountPerCycle } = prod.producesResource;
  return `${b.producedCount} cycles (${(b.producedCount * amountPerCycle).toFixed(0)} ${RESOURCE_DEFS[resourceId].name})`;
}
for (const category of Object.keys(PRODUCTION_DEFS)) {
  const btn = document.createElement('button');
  btn.className = 'opsBtn'; // Operations Map shared button skin — see index.html .opsBtn
  btn.textContent = PRODUCTION_DEFS[category].name;
  btn.dataset.category = category;
  btn.addEventListener('click', () => selectCategory(category));
  prodCatRow.appendChild(btn);
}
function updateProdPanelUI() {
  for (const btn of prodCatRow.children) btn.classList.toggle('selected', btn.dataset.category === selectedCategory);
}
function selectCategory(category) {
  selectedCategory = category;
  updateProdPanelUI();
  recipeInfo.innerHTML = recipeHtml(category);
  // Arm B-mode with this category's facility preselected — same pipeline
  // the buildbar's own per-building buttons use (selectBuildType), just
  // reached from the recipe-first front door instead of a raw building list.
  if (roadMode.active) exitRoadMode();
  enterBuildMode();
  selectBuildType(PRODUCTION_DEFS[category].facility);
}
function enterProdPanel() { prodPanel.classList.add('open'); }
function exitProdPanel() { prodPanel.classList.remove('open'); }

// ---------------------------------------------------------------------------
// IMPORT PANEL (P4 — CONCEPT.md: missing resources are importable "in
// exchange for INFLUENCE + a bit of IC — a pricier stopgap, not a
// replacement for owning the deposit"). Press I to open; each button
// imports one BATCH_SIZE of that resource immediately if affordable
// (game/production.js importResource) — a single explicit spend per click,
// no queue.
const importPanel = document.getElementById('importPanel');
const importResRow = document.getElementById('importResRow');
function importButtonLabel(rid) {
  const cost = importCost();
  return `${RESOURCE_DEFS[rid].name} +${IMPORT_TUNABLES.BATCH_SIZE} (${cost.influence} infl + ${cost.ic} IC)`;
}
for (const r of RESOURCE_LIST) {
  const btn = document.createElement('button');
  btn.className = 'opsBtn';
  btn.textContent = importButtonLabel(r.id);
  btn.dataset.resource = r.id;
  btn.addEventListener('click', () => {
    const ok = importResource(economy, r.id);
    flashMessage(ok
      ? `Imported ${IMPORT_TUNABLES.BATCH_SIZE} ${RESOURCE_DEFS[r.id].name}.`
      : `Can't afford to import ${RESOURCE_DEFS[r.id].name} — needs ${importCost().influence} influence + ${importCost().ic} IC.`);
    // DIRECTOR ACTION POINT (Part B point 3 of 4: "a resource ... imported").
    if (ok) reactToAction(directorState, { kind: 'import', key: r.id }, getCurrentPlan());
  });
  importResRow.appendChild(btn);
}
function enterImportPanel() { importPanel.classList.add('open'); }
function exitImportPanel() { importPanel.classList.remove('open'); }

// ---------------------------------------------------------------------------
// RESEARCH PANEL (P4 follow-up — game/research.js, CONCEPT.md's settled
// "Tech / research model" paragraph). Press T to open; lists every gated
// tech (game/research.js TECH_DEFS) with its full gate (prerequisite
// buildings/techs + IC + resource cost + time), what production category it
// unlocks, and its current state (locked / researchable / in-progress /
// unlocked). Rows are built ONCE below (static structure — name/blurb/gate
// text never change at runtime); updateTechPanelUI() then refreshes only the
// dynamic bits (status text + Start button visibility/afford state) every
// frame, same split main.js already uses for the econ HUD vs. the one-time
// buildbar/prodCatRow/importResRow button construction above.
const techPanel = document.getElementById('techPanel');
const techPanelTitleEl = document.getElementById('techPanelTitle');
const techListEl = document.getElementById('techList');
techPanelTitleEl.textContent = RESEARCH_COPY.PANEL_TITLE;

function resourceAmountsText(costObj) {
  return Object.entries(costObj || {})
    .map(([rid, amt]) => `${RESOURCE_DEFS[rid] ? RESOURCE_DEFS[rid].name : rid} ${amt}`)
    .join(', ') || 'none';
}
function techGateText(techId) {
  const def = TECH_DEFS[techId];
  const buildings = (def.prerequisiteBuildings || []).map(k => BUILDING_DEFS[k].name).join(', ') || 'none';
  const techs = (def.prerequisiteTechs || []).length
    ? ` + tech: ${def.prerequisiteTechs.map(t => TECH_DEFS[t].name).join(', ')}`
    : '';
  return `Requires: ${buildings}${techs}\n`
    + `Cost: ${def.icCost} IC + ${resourceAmountsText(def.resourceCost)}   Time: ${def.researchTime}s`;
}
const techRows = {};
for (const techId of Object.keys(TECH_DEFS)) {
  const def = TECH_DEFS[techId];
  const row = document.createElement('div');
  row.className = 'techRow';
  const catName = PRODUCTION_DEFS[def.unlocksCategory] ? PRODUCTION_DEFS[def.unlocksCategory].name : def.unlocksCategory;
  const header = document.createElement('div');
  header.className = 'techHeader';
  header.innerHTML = `<b>${def.name}</b> — unlocks: ${catName}`;
  const blurb = document.createElement('div');
  blurb.className = 'techBlurb';
  blurb.textContent = def.blurb;
  const gate = document.createElement('div');
  gate.className = 'techGate';
  gate.textContent = techGateText(techId);
  const status = document.createElement('div');
  status.className = 'techStatus';
  const btn = document.createElement('button');
  btn.className = 'opsBtn';
  btn.textContent = RESEARCH_COPY.START_BUTTON;
  btn.addEventListener('click', () => {
    const ok = startResearch(world, economy, researchState, techId);
    flashMessage(ok ? `Research started: ${def.name}.` : `Can't start ${def.name} yet.`);
  });
  row.append(header, blurb, gate, status, btn);
  techListEl.appendChild(row);
  techRows[techId] = { status, btn, row };
}
// Live status refresh — cheap enough (six rows) to call unconditionally
// every frame from loop() below, same spirit as updateGuidancePanel's own
// "cheap enough to run unconditionally" comment. A locked row shows
// exactly what's still missing (buildings AND/OR techs); a researchable row
// shows the Start button, disabled (not hidden) when the gate is met but
// the player can't currently afford it — so the player can see the tech is
// within reach without it vanishing from the list.
function updateTechPanelUI() {
  for (const techId of Object.keys(TECH_DEFS)) {
    const st = techStatus(world, researchState, techId);
    const { status, btn, row } = techRows[techId];
    // Operations Map cue: the row's left-edge "tab" colors by state (green
    // once researchable/active, ink-blue once unlocked) so the ledger of
    // techs reads at a glance without needing to read every status line —
    // purely a border-color toggle, no gating logic here.
    row.classList.toggle('researchable', st.state === 'researchable' || st.state === 'active');
    row.classList.toggle('done', st.state === 'unlocked');
    if (st.state === 'unlocked') {
      status.textContent = RESEARCH_COPY.UNLOCKED_LABEL;
      btn.style.display = 'none';
    } else if (st.state === 'active') {
      status.textContent = `${RESEARCH_COPY.ACTIVE_PREFIX}${Math.round(st.progress * 100)}%`;
      btn.style.display = 'none';
    } else if (st.state === 'busy') {
      status.textContent = RESEARCH_COPY.BUSY_HINT;
      btn.style.display = 'none';
    } else if (st.state === 'locked') {
      const missing = [
        ...st.missingBuildings.map(k => BUILDING_DEFS[k].name),
        ...st.missingTechs.map(t => TECH_DEFS[t].name),
      ].join(', ');
      status.textContent = `${RESEARCH_COPY.LOCKED_PREFIX}${missing}`;
      btn.style.display = 'none';
    } else { // 'researchable'
      const afford = canAffordResearch(economy, techId);
      status.textContent = afford ? '' : RESEARCH_COPY.CANT_AFFORD_HINT;
      btn.style.display = '';
      btn.disabled = !afford;
    }
  }
}
function enterTechPanel() { techPanel.classList.add('open'); }
function exitTechPanel() { techPanel.classList.remove('open'); }

// ---------------------------------------------------------------------------
// MAP LEGEND & RESOURCE FILTER (playtest: "filter for resources", "clearer
// ... where things are") — a collapsible key (deposit glyphs, ownership ring
// colors, a compact keybind cheat-sheet) plus one FILTER button per
// RESOURCE_LIST entry. Clicking a filter button lights up every deposit of
// that type on the map (drawDepositFocus, defined above near the other
// draw* helpers) — click the active one again to clear it. Pure finder, never
// gates anything; content is built generically off RESOURCE_LIST/
// OWNERSHIP_COLORS so a new resource or a renderer color change shows up
// here with no template rewrite.
const legendPanel = document.getElementById('legendPanel');
const legendToggle = document.getElementById('legendToggle');
const legendToggleIcon = document.getElementById('legendToggleIcon');
const legendBodyInner = document.getElementById('legendBodyInner');
let legendExpanded = false;
function setLegendExpanded(v) {
  legendExpanded = v;
  legendPanel.classList.toggle('expanded', v);
  legendToggleIcon.textContent = v ? '▾' : '▸';
  if (v) legendPanel.classList.remove('nudge'); // opened once → stop the discoverability pulse
}
legendToggle.addEventListener('click', () => setLegendExpanded(!legendExpanded));
setLegendExpanded(false);
legendPanel.classList.add('nudge'); // pulse the collapsed tab until the player first opens it (see setLegendExpanded)

let resourceFilterType = null;
const filterBtns = {};
function updateFilterBtnsUI() {
  for (const id of Object.keys(filterBtns)) filterBtns[id].classList.toggle('active', id === resourceFilterType);
}
function setResourceFilter(id) {
  resourceFilterType = (resourceFilterType === id) ? null : id; // click active -> clear
  updateFilterBtnsUI();
}

{
  const ownerRows = [
    ['player', 'Your cities/units'],
    ['enemy', 'Enemy cities/units'],
    ['neutral', 'Uncontested city'],
  ].map(([id, label]) =>
    `<div class="legendRow"><span class="legendSwatch" style="background:${OWNERSHIP_COLORS[id]}"></span>${label}</div>`
  ).join('');

  const filterRowsHtml = RESOURCE_LIST.map(r =>
    `<button type="button" class="filterBtn" data-res="${r.id}">`
    + `<span class="legendGlyph" style="color:${r.color}">${r.glyph}</span>${r.name}</button>`
  ).join('');

  legendBodyInner.innerHTML =
    `<div class="legendSection"><span class="legendSectionLabel">Ownership rings</span>${ownerRows}</div>`
    + `<div class="legendSection"><span class="legendSectionLabel">Resource finder — click to highlight</span><div id="legendFilterRow"></div></div>`
    + `<div class="legendSection"><span class="legendSectionLabel">Controls</span>`
    + `<div class="legendKeyRow"><b>B</b> build &nbsp; <b>P</b> produce &nbsp; <b>I</b> import</div>`
    + `<div class="legendKeyRow"><b>T</b> research &nbsp; <b>R</b> road &nbsp; <b>X</b> sell</div>`
    + `<div class="legendKeyRow"><b>G</b> tree &nbsp; <b>H</b> guide &nbsp; <b>F</b> fog</div>`
    + `<div class="legendKeyRow"><b>C</b> begin combat &nbsp; <b>M</b> tone &nbsp; <b>Esc</b> cancel</div>`
    + `</div>`;
  const filterRowEl = document.getElementById('legendFilterRow');
  filterRowEl.innerHTML = filterRowsHtml;
  for (const btn of filterRowEl.children) {
    filterBtns[btn.dataset.res] = btn;
    btn.addEventListener('click', () => setResourceFilter(btn.dataset.res));
  }
}

// ---------------------------------------------------------------------------
// REINFORCEMENTS PANEL — B2 (game/reinforcements.js, CONCEPT.md's "three
// sources of battalions"). Same collapsible-tab shape + static-shell/
// JS-fills-content split as #legendPanel just above: three route rows built
// ONCE below (name/cost/button never change at runtime), refreshed every
// frame by updateReinforcePanelUI() for the parts that DO change — affordable
// vs. not (button disabled, same convention #techPanel's Start button uses),
// the elite gate met/unmet, and the in-flight queue's live countdown.
const reinforcePanel = document.getElementById('reinforcePanel');
const reinforceToggle = document.getElementById('reinforceToggle');
const reinforceToggleIcon = document.getElementById('reinforceToggleIcon');
const reinforceBodyInner = document.getElementById('reinforceBodyInner');
let reinforceExpanded = false;
function setReinforceExpanded(v) {
  reinforceExpanded = v;
  reinforcePanel.classList.toggle('expanded', v);
  reinforceToggleIcon.textContent = v ? '▾' : '▸';
  if (v) reinforcePanel.classList.remove('nudge');
}
reinforceToggle.addEventListener('click', () => setReinforceExpanded(!reinforceExpanded));
setReinforceExpanded(false);
reinforcePanel.classList.add('nudge'); // pulse the collapsed tab until first opened, same as econHud/legendPanel

function reinforceCostText(cost) {
  const parts = [];
  if (cost.ic) parts.push(`${cost.ic} IC`);
  if (cost.manpower) parts.push(`${cost.manpower} manpower`);
  for (const [rid, amt] of Object.entries(cost.resources || {})) {
    parts.push(`${amt} ${RESOURCE_DEFS[rid] ? RESOURCE_DEFS[rid].name : rid}`);
  }
  return parts.join(', ');
}

const REINFORCE_ROUTES = [
  { id: 'elite', request: () => requestElite(world, economy, map) },
  { id: 'standard', request: () => requestStandard(world, economy, map) },
  { id: 'militia', request: () => raiseMilitia(world, economy, map) },
];
const reinforceRouteEls = {};
{
  const routesHtml = REINFORCE_ROUTES.map(({ id }) => {
    const def = REINFORCEMENT_DEFS[id];
    return `<div class="reinforceRoute" data-route="${id}">`
      + `<span class="reinforceRouteName">${def.name}</span>`
      + `<span class="reinforceRouteCost">${reinforceCostText(def.cost)} — ${def.leadTime}s</span>`
      + `<span class="reinforceRouteGate" data-gate></span>`
      + `<button type="button" class="opsBtn" data-req>Requisition</button>`
      + `</div>`;
  }).join('');
  const queueHtml = `<div id="reinforceQueue"></div>`;
  reinforceBodyInner.innerHTML = `<div id="reinforceRoutes">${routesHtml}</div>${queueHtml}`;
  for (const el of reinforceBodyInner.querySelectorAll('.reinforceRoute')) {
    reinforceRouteEls[el.dataset.route] = { row: el, gate: el.querySelector('[data-gate]'), btn: el.querySelector('[data-req]') };
  }
  for (const { id, request } of REINFORCE_ROUTES) {
    reinforceRouteEls[id].btn.addEventListener('click', () => {
      const res = request();
      flashMessage(res.ok
        ? `Requisitioned: ${REINFORCEMENT_DEFS[id].name}.`
        : `Can't requisition ${REINFORCEMENT_DEFS[id].name} — ${res.reason}.`);
      if (res.ok) reactToAction(directorState, { kind: 'reinforce', key: id }, getCurrentPlan());
    });
  }
}
const reinforceQueueEl = document.getElementById('reinforceQueue');

// Live refresh — cheap (three static rows + a short queue list), so called
// unconditionally every frame from loop() near updateTechPanelUI(), same
// spirit as every other Ops panel's own "cheap enough to run unconditionally"
// comment.
function updateReinforcePanelUI() {
  for (const { id } of REINFORCE_ROUTES) {
    const def = REINFORCEMENT_DEFS[id];
    const { row, gate, btn } = reinforceRouteEls[id];
    let gateOk = true, gateText = '';
    if (def.requiresFactory) {
      gateOk = world.buildings.some(b => b.side === 'player' && b.key === def.requiresFactory && b.status === 'complete');
      if (!gateOk) gateText = `Requires: ${BUILDING_DEFS[def.requiresFactory].name}`;
    }
    const affordable = canAfford(economy, def.cost);
    row.classList.toggle('ready', gateOk && affordable);
    gate.textContent = gateText;
    btn.disabled = !gateOk || !affordable;
    btn.title = !gateOk ? gateText : (!affordable ? 'Cannot afford yet' : '');
  }
  if (!world.reinforcements.length) {
    reinforceQueueEl.innerHTML = `<span class="rqEmpty">No reinforcements in transit.</span>`;
  } else {
    reinforceQueueEl.innerHTML = world.reinforcements.map(job => {
      const name = REINFORCEMENT_DEFS[job.route].name;
      return `<div class="rqRow"><b>${name}</b> — arrives in ${Math.max(0, job.timeLeft).toFixed(1)}s</div>`;
    }).join('');
  }
}

// ---------------------------------------------------------------------------
// BATTALIONS PANEL — B3 (game/battalionDoctrine.js, "self-deploy + sectors +
// stance"), the player's window onto/control surface over every battalion
// world.battalions currently holds. Same collapsible-tab shape as
// #reinforcePanel just above (parked directly below it on the right edge,
// same column) — a slim always-visible tab plus a click-to-expand body. Row
// list is fully REBUILT every refresh (not diffed) rather than the static-
// shell-plus-live-fields split #reinforcePanel uses for its three fixed
// routes: battalions come and go at runtime (spawned by reinforcements,
// wiped out in combat), so there's no fixed row count to build once — same
// "just re-render the whole dynamic list" shape #reinforceQueue already uses
// for its own runtime-length job queue, one level up.
const battalionPanel = document.getElementById('battalionPanel');
const battalionToggle = document.getElementById('battalionToggle');
const battalionToggleIcon = document.getElementById('battalionToggleIcon');
const battalionBodyInner = document.getElementById('battalionBodyInner');
let battalionExpanded = false;
function setBattalionExpanded(v) {
  battalionExpanded = v;
  battalionPanel.classList.toggle('expanded', v);
  battalionToggleIcon.textContent = v ? '▾' : '▸';
  if (v) battalionPanel.classList.remove('nudge');
}
battalionToggle.addEventListener('click', () => setBattalionExpanded(!battalionExpanded));
setBattalionExpanded(false);

// ASSIGN-SECTOR MODE — "click a city to rally this battalion," same one-shot
// input-mode shape as roadMode/buildMode (see those sections above): press
// the row's "Assign sector" button to arm it, then the next canvas click
// that lands on a city owned by the player sets that battalion's
// bn.homeSector (via game/battalionDoctrine.js's assignSector — the SAME
// function the debug hook wraps, not a reimplementation). Esc/right-click
// cancels without assigning anything, exactly like R/B mode.
const battalionAssignMode = { active: false, bnId: null };
function enterBattalionAssignMode(bnId) {
  if (roadMode.active) exitRoadMode();
  if (buildMode.active) exitBuildMode();
  if (sellMode.active) exitSellMode();
  battalionAssignMode.active = true;
  battalionAssignMode.bnId = bnId;
}
function exitBattalionAssignMode() {
  battalionAssignMode.active = false;
  battalionAssignMode.bnId = null;
}

// City hit-test for assign-sector clicks — a generous click radius (1.6x the
// city's own capture radius) rather than an exact-pixel target, since a city
// icon on the rendered map is small and this is a deliberate one-shot
// "point at roughly the right place" action, not precision placement (same
// forgiving-target spirit as sitePlacement's own spiral search for B-mode).
function cityAtWorldPoint(wx, wy) {
  const ts = map.tileSize;
  let bestIdx = null, bestD2 = Infinity;
  for (let i = 0; i < (map.cities || []).length; i++) {
    const c = map.cities[i];
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const tol = (c.r || 6) * ts * 1.6;
    const dd = (wx - cx) ** 2 + (wy - cy) ** 2;
    if (dd <= tol * tol && dd < bestD2) { bestD2 = dd; bestIdx = i; }
  }
  return bestIdx;
}

canvas.addEventListener('click', e => {
  if (!battalionAssignMode.active) return;
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  const cityIdx = cityAtWorldPoint(wx, wy);
  const bnId = battalionAssignMode.bnId;
  const bn = world.battalions.find(b => b.id === bnId);
  if (cityIdx === null) { flashMessage('Click a city to assign that sector — Esc/right-click to cancel.'); return; }
  const city = map.cities[cityIdx];
  if (city.owner !== 'player') { flashMessage(`Can't rally to ${city.name} — not under player control.`); return; }
  assignSector(world, bnId, cityIdx);
  flashMessage(bn ? `${bn.name} rallied to ${city.name}.` : `Battalion rallied to ${city.name}.`);
  exitBattalionAssignMode();
});

// Effective sector NAME for a battalion right now — mirrors
// game/battalionDoctrine.js's own sector-pick logic (explicit homeSector if
// still owned, else its last AUTO-pick) read-only, purely for display; never
// duplicates the scoring itself, just the two-branch "which one's active"
// check the module already resolves each assignment tick.
function battalionSectorLabel(bn) {
  if (bn.homeSector != null) {
    const c = map.cities[bn.homeSector];
    if (c && c.owner === 'player') return c.name;
  }
  const auto = debugBattalionDoctrineState().find(s => s.id === bn.id);
  if (auto) {
    const c = map.cities[auto.mapIdx];
    if (c) return c.name;
  }
  return '—';
}

// Live refresh — rebuilds the row list every call (see this section's header
// for why), called unconditionally every frame from loop() near
// updateReinforcePanelUI(), same "cheap enough to run unconditionally" spirit
// every other Ops panel already follows.
function updateBattalionPanelUI() {
  const battalions = world.battalions.filter(bn => bn.side === 'player');
  if (!battalions.length) {
    battalionBodyInner.innerHTML = `<span id="battalionEmpty">No battalions in the field yet.</span>`;
    return;
  }
  battalionBodyInner.innerHTML = battalions.map(bn => {
    const { alive, total } = battalionStrength(bn);
    const sector = battalionSectorLabel(bn);
    const stance = bn.stance || 'balanced';
    const assigning = battalionAssignMode.active && battalionAssignMode.bnId === bn.id;
    // B4: morale state label + color (fresh/steady/shaken/broken — see
    // game/battalionMorale.js's moraleState/STATE_THRESHOLDS). A `broken`
    // battalion is routing — battalionDoctrine.js's rout branch is what's
    // actually moving it; this is purely the read-only display of that.
    const mState = moraleState(bn);
    const routTag = mState === 'broken' ? ' (routing)' : '';
    const rowStateClass = mState === 'broken' ? ' morale-broken' : mState === 'shaken' ? ' morale-shaken' : '';
    return `<div class="battalionRow${assigning ? ' assigning' : rowStateClass}" data-bn="${bn.id}">`
      + `<div class="battalionRowHead"><b>${bn.name}</b><span class="battalionType">${bn.type}</span></div>`
      + `<div class="battalionRowStats">Strength: <b>${alive}/${total}</b> — Sector: <b>${sector}</b></div>`
      + `<div class="battalionRowStats">Morale: <span class="battalionMorale ${mState}">${mState}${routTag}</span> `
      + `(${Math.round(bn.morale)}) — Cohesion: <b>${Math.round(bn.cohesion)}</b></div>`
      + `<div class="battalionRowBtns">`
      + `<button type="button" class="opsBtn" data-stance-cycle>Stance: ${stance} ▸</button>`
      + `<button type="button" class="opsBtn" data-assign>${assigning ? 'Click a city…' : 'Assign sector'}</button>`
      + `</div></div>`;
  }).join('');
  for (const row of battalionBodyInner.querySelectorAll('.battalionRow')) {
    const bnId = Number(row.dataset.bn);
    row.querySelector('[data-stance-cycle]').addEventListener('click', () => cycleStance(world, bnId));
    row.querySelector('[data-assign]').addEventListener('click', () => {
      if (battalionAssignMode.active && battalionAssignMode.bnId === bnId) exitBattalionAssignMode();
      else enterBattalionAssignMode(bnId);
    });
  }
}

// ---------------------------------------------------------------------------
// GUIDANCE PANEL — now the DYNAMIC, goal-driven checklist (Part A — CONCEPT.
// md's settled "Onboarding / tutorial concept" paragraph, beats 3-5: the
// player picks goals in the tree, "the game reverse-plans the how", "the
// payoff is legible"). This EVOLVES the panel rather than replacing it —
// same DOM ids, same dismiss/non-gating contract as before ("First-run /
// tutorial is OPEN, not dictated": never disables a button, never requires a
// step before some other system unlocks, every control keeps working
// identically whether this is open or closed) — only its CONTENT changed,
// from a fixed COPY.GUIDANCE_STEPS list to game/tutorialplan.js's
// computePlan(goalState, ...) output, recomputed live every frame so it
// always reflects the player's CURRENT goal selection and CURRENT progress.
// Repositioned to the TOP of the screen per the task's explicit placement
// (see index.html's #guidancePanel CSS) — previously bottom-left.
const guidancePanel = document.getElementById('guidancePanel');
const guidanceTitleEl = document.getElementById('guidanceTitle');
const guidanceGoalsEl = document.getElementById('guidanceGoals');
const guidanceStepsEl = document.getElementById('guidanceSteps');
const guidanceFootnoteEl = document.getElementById('guidanceFootnote');
const guidanceCloseBtn = document.getElementById('guidanceCloseBtn');

guidanceTitleEl.textContent = TUTORIAL_COPY.CHECKLIST_TITLE;
guidanceFootnoteEl.textContent = TUTORIAL_COPY.CHECKLIST_DISMISS_HINT;
function setGuidanceVisible(visible) { guidancePanel.classList.toggle('hidden', !visible); }
function toggleGuidance() { setGuidanceVisible(guidancePanel.classList.contains('hidden')); }
guidanceCloseBtn.addEventListener('click', () => setGuidanceVisible(false));

// Recomputed on demand (cheap: the whole techtree graph is ~50-60 nodes, see
// game/techtree.js's own "cheap enough to run unconditionally" comments) —
// this is the SAME plan object both the checklist render below and (once
// wired) the reactive director's off-track classifier read, so the two
// systems can never disagree about "what's the current plan."
function getCurrentPlan() {
  return computePlan(goalState, { world, economy, researchState });
}

// One-shot PAYOFF tracking ("we're making jets now" — CONCEPT.md: "gives
// feedback"): fires exactly once per goal completion, not every frame while
// it stays true. Keyed on goalId so toggling a goal off and back on later
// re-arms its payoff line (a goal that's ALREADY satisfied the moment it's
// re-selected reads as freshly "done," not silently skipped).
const payoffAnnounced = new Set();
function updateGuidancePanel(plan = getCurrentPlan()) {
  // Buildbar grey-out/next-highlight reads this SAME plan object (see
  // updateBuildBarPlanUI's own comment) — refreshed here, once per frame,
  // right alongside the checklist it must never disagree with, rather than
  // recomputing the plan a second time in loop().
  updateBuildBarPlanUI(plan);
  if (!plan.goals.length) {
    guidanceGoalsEl.innerHTML = '';
    guidanceStepsEl.innerHTML = `<li class="hint">${TUTORIAL_COPY.NO_GOALS_HINT}</li>`;
    return plan;
  }
  guidanceGoalsEl.innerHTML = plan.goals.map(g =>
    `<span class="goalTag${g.satisfied ? ' done' : ''}">${g.satisfied ? '✓ ' : ''}${g.node.name}</span>`
  ).join('');
  // "current/next actionable step" — the FIRST unsatisfied step in
  // topological order, per the task's explicit highlight requirement.
  const nextIdx = plan.steps.findIndex(s => !s.satisfied);
  guidanceStepsEl.innerHTML = plan.steps.map((s, i) =>
    `<li class="${s.satisfied ? 'done' : ''}${i === nextIdx ? ' next' : ''}">${s.label}</li>`
  ).join('') || `<li class="hint">${TUTORIAL_COPY.NO_GOALS_HINT}</li>`;
  for (const g of plan.goals) {
    if (g.satisfied) {
      if (!payoffAnnounced.has(g.goalId)) {
        payoffAnnounced.add(g.goalId);
        flashMessage(TUTORIAL_COPY.PAYOFF(g.node.name));
      }
    } else {
      payoffAnnounced.delete(g.goalId);
    }
  }
  return plan;
}

// ---------------------------------------------------------------------------
// DIRECTOR — the reactive/adaptive layer (Part B — game/director.js owns the
// state machine + classification logic; this section is purely the DOM
// wiring: the corner bubble, the "are you five" confirm dialog, and the two
// terminal screens the silly-mode gag chain can reach). CONCEPT.md's
// "Corner commentator" direction, part (1): "an event-driven corner speech-
// bubble ... quips as designer-written placeholder text" — no character art
// (deferred, per that same paragraph), just the bubble + mechanic.
const directorBubbleEl = document.getElementById('directorBubble');
const areYouFiveDialogEl = document.getElementById('areYouFiveDialog');
const areYouFiveTextEl = document.getElementById('areYouFiveText');
const areYouFiveYesBtn = document.getElementById('areYouFiveYes');
const areYouFiveNoBtn = document.getElementById('areYouFiveNo');
const takeoverScreenEl = document.getElementById('takeoverScreen');
const takeoverTextEl = document.getElementById('takeoverText');

areYouFiveTextEl.textContent = DIRECTOR_COPY.ARE_YOU_FIVE_TITLE;
areYouFiveYesBtn.textContent = DIRECTOR_COPY.ARE_YOU_FIVE_YES;
areYouFiveNoBtn.textContent = DIRECTOR_COPY.ARE_YOU_FIVE_NO;
function showAreYouFiveDialog() { areYouFiveDialogEl.classList.add('open'); }
function hideAreYouFiveDialog() { areYouFiveDialogEl.classList.remove('open'); }

// "Yes" -> mock takeover. A web page cannot force-close its own tab (only a
// script-opened window can, and only in some browsers) — window.close() is
// attempted as a harmless bonus (a no-op everywhere it isn't permitted), and
// the REAL effect the task asks for is this full-screen black takeover,
// which is achievable everywhere.
areYouFiveYesBtn.addEventListener('click', () => {
  hideAreYouFiveDialog();
  resolveAreYouFive(directorState, true);
  takeoverTextEl.textContent = DIRECTOR_COPY.TAKEOVER_LINES.join('\n');
  takeoverScreenEl.classList.add('open');
  window.close(); // harmless bonus attempt — see comment above
});
// "No" -> "fired for misappropriating funds" -> restart. Reload is the
// simplest robust "reset the game state" (re-runs the whole boot sequence,
// including the main-menu picker on a non-deep-link load) — a brief beat on
// the same takeover screen first so "fired" actually reads before the page
// goes away.
areYouFiveNoBtn.addEventListener('click', () => {
  hideAreYouFiveDialog();
  resolveAreYouFive(directorState, false);
  takeoverTextEl.textContent = DIRECTOR_COPY.FIRED_LINES.join('\n');
  takeoverScreenEl.classList.add('open');
  setTimeout(() => location.reload(), 1800);
});

// DIRECTOR ACTION POINT (Part B point 2 of 4: "a unit/category produced").
// Production is automatic once a facility is fed (game/production.js), so
// there's no single player CLICK to hook here the way building/import/sell
// have — the real, non-spammy action point is the moment a facility
// actually rolls its FIRST unit off the line (producedCount 0->1), read
// straight off game/production.js's own per-building bookkeeping every
// frame in loop() below. announcedProducing tracks which building ids have
// already fired this once, so a facility producing its 50th unit doesn't
// re-trigger it.
const announcedProducing = new Set();
function checkProductionMilestones() {
  for (const b of world.buildings) {
    if (b.side !== 'player' || !b.prodCategory || !b.producedCount) continue;
    if (announcedProducing.has(b.id)) continue;
    announcedProducing.add(b.id);
    reactToAction(directorState, { kind: 'category', key: b.prodCategory }, getCurrentPlan());
  }
}

// ---------------------------------------------------------------------------
// INTRO SEQUENCE — DOM wiring for game/intro.js's front-half onboarding
// beats. Deliberately reuses two systems end to end rather than building new
// ones (per the task's "don't rebuild the tree or the plan" instruction):
// the 'graph'/'discard' beats drive the SAME techTreeView the G key opens
// (toggleTechTree, defined above in the TECH/PRODUCTION TREE VIEW section),
// and 'handoff' just points the player at it plus the goal-driven checklist
// (#guidancePanel/game/tutorialplan.js, already live and updating every
// frame regardless of the intro) — nothing here creates a second tree or a
// second plan.
const introPanelEl = document.getElementById('introPanel');
const introTitleEl = document.getElementById('introTitle');
const introBodyEl = document.getElementById('introBody');
const introContinueBtn = document.getElementById('introContinueBtn');
const introSkipBtn = document.getElementById('introSkipBtn');
introSkipBtn.textContent = INTRO_COPY.SKIP;

// Renders whatever the CURRENT beat needs into #introPanel, plus the
// persistent Skip button's visibility, off introState alone. Called once on
// every transition below (so the very next frame already reflects the new
// beat) and once more per frame from loop() for the same "just refresh it,
// don't diff" reason updatePhaseHud/updateGuidancePanel already do.
function updateIntroUI() {
  introSkipBtn.classList.toggle('show', introState.active);
  // Focus the intro: hide the game's corner HUD while it runs so the welcome/
  // handoff beats read as "just you and the map, nothing else matters yet"
  // (the CSS hides the ledger/legend/plan/objective/phase panels; the #hud
  // controls ticker stays, since the welcome beat is teaching pan/zoom). The
  // instant the intro ends this class drops and every panel reverts to its
  // own state. See body.intro-active in index.html.
  document.body.classList.toggle('intro-active', introState.active);
  if (!introState.active) { introPanelEl.classList.add('hidden'); return; }
  // 'discard' is the flourish playing on the tech-tree canvas itself (see
  // loop() below) — hiding the panel for its ~1s keeps the burn the sole
  // focus (per the task's "a bit of flourish"); there's no player action to
  // offer mid-flourish anyway, it auto-advances to 'handoff' on its own.
  if (introState.beat === 'discard') { introPanelEl.classList.add('hidden'); return; }
  introPanelEl.classList.remove('hidden');
  if (introState.beat === 'welcome') {
    introTitleEl.textContent = INTRO_COPY.WELCOME_TITLE;
    introBodyEl.textContent = INTRO_COPY.WELCOME_BODY;
    introContinueBtn.textContent = INTRO_COPY.WELCOME_CONTINUE;
  } else if (introState.beat === 'graph') {
    introTitleEl.textContent = INTRO_COPY.GRAPH_TITLE;
    introBodyEl.textContent = INTRO_COPY.GRAPH_BODY;
    introContinueBtn.textContent = INTRO_COPY.GRAPH_CONTINUE;
  } else if (introState.beat === 'handoff') {
    introTitleEl.textContent = INTRO_COPY.HANDOFF_TITLE;
    introBodyEl.textContent = INTRO_COPY.HANDOFF_BODY;
    introContinueBtn.textContent = INTRO_COPY.HANDOFF_CONTINUE;
  }
}

// One Continue button serves all three beats that have a player action
// (welcome/graph/handoff — 'discard' has none, see above) — dispatches on
// the CURRENT beat rather than needing three separate buttons, since
// #introPanel only ever shows one beat's content at a time anyway.
introContinueBtn.addEventListener('click', () => {
  if (introState.beat === 'welcome') {
    beginGraphBeat(introState);
    if (!techTreeView.isOpen) toggleTechTree(); // the REAL G-key tree — see this block's header comment
  } else if (introState.beat === 'graph') {
    // This click IS "discard it" (CONCEPT.md's memorable beat, folded into
    // GRAPH_BODY's copy) — starts the flourish immediately; see loop()
    // below for where it actually plays and auto-advances to 'handoff'.
    beginDiscard(introState);
  } else if (introState.beat === 'handoff') {
    completeIntro(introState);
    if (techTreeView.isOpen) techTreeView.hide(); // defensive — the flourish's own completion should have already closed it
  }
  updateIntroUI();
});

// SKIP — the task's hard "skippable at any point" requirement. Tears down
// whatever the sequence happened to be doing (closes the tree if it's open,
// stops a mid-flight flourish so loop() falls back to its normal
// techTreeView.render() call next frame) and lands in the exact same open,
// non-gated state completeIntro would have, just without waiting out the
// remaining beats.
introSkipBtn.addEventListener('click', () => {
  skipIntro(introState);
  if (techTreeView.isOpen) techTreeView.hide();
  updateIntroUI();
});

updateIntroUI(); // reflect the boot-time decision (shown or not) on the very first frame, before loop() ever runs

addEventListener('keydown', e => {
  if (e.repeat) return;
  // The intro owns input exclusively while it's running (see game/intro.js's
  // header comment) — every mode toggle below (B/R/X/P/I/T/G/...) stays
  // inert until the sequence ends. Skip is a deliberate on-screen BUTTON,
  // not a key, per the task's "persistent Skip affordance" framing — a
  // first-time player shouldn't need to already know a shortcut to get out.
  if (introState.active) return;
  if (e.key === 'g' || e.key === 'G') {
    // TECH/PRODUCTION TREE VIEW toggle (see the block above researchState's
    // GAME PHASES comment). Checked first and returns early — while the
    // tree is open it's a full-screen exclusive overlay (toggleTechTree
    // already closed every other panel on the way in), so no other
    // shortcut below should also fire on the same keypress. Escape still
    // closes it too (see the Escape branch further down).
    toggleTechTree();
    return;
  }
  if (techTreeView.isOpen) {
    // Escape closes the tree like every other overlay (the G-handler comment
    // above promises this). It has to be handled HERE rather than in the
    // Escape branch below, because that branch sits past this exclusive-input
    // return and would otherwise never run while the tree is open.
    if (e.key === 'Escape') techTreeView.hide();
    return; // otherwise the tree owns input exclusively (drag/wheel handled by its own camera controls)
  }
  if (e.key === 'r' || e.key === 'R') {
    if (buildMode.active) exitBuildMode();
    if (sellMode.active) exitSellMode();
    if (prodPanel.classList.contains('open')) exitProdPanel();
    if (roadMode.active) exitRoadMode(); else enterRoadMode();
  } else if (e.key === 'b' || e.key === 'B') {
    if (roadMode.active) exitRoadMode();
    if (sellMode.active) exitSellMode();
    if (prodPanel.classList.contains('open')) exitProdPanel();
    if (buildMode.active) exitBuildMode(); else enterBuildMode();
  } else if (e.key === 'x' || e.key === 'X') {
    // SELL/DEMOLISH MODE TOGGLE (Part B follow-up — see the SELL/DEMOLISH
    // MODE section above) — same "enter one mode, exit the others" shape as
    // R/B.
    if (roadMode.active) exitRoadMode();
    if (buildMode.active) exitBuildMode();
    if (sellMode.active) exitSellMode(); else enterSellMode();
  } else if (e.key === 'p' || e.key === 'P') {
    if (prodPanel.classList.contains('open')) exitProdPanel(); else enterProdPanel();
  } else if (e.key === 'i' || e.key === 'I') {
    if (importPanel.classList.contains('open')) exitImportPanel(); else enterImportPanel();
  } else if (e.key === 't' || e.key === 'T') {
    // RESEARCH PANEL TOGGLE (P4 follow-up) — same independent-toggle shape
    // as the I key above (importPanel): doesn't close/exclude any other
    // panel, just shows/hides itself.
    if (techPanel.classList.contains('open')) exitTechPanel(); else enterTechPanel();
  } else if (e.key === 'h' || e.key === 'H') {
    // GUIDANCE PANEL TOGGLE — dismiss/restore only, never gates anything
    // (see the GUIDANCE PANEL section above for the full rationale).
    toggleGuidance();
  } else if (e.key === 'f' || e.key === 'F') {
    // MANUAL FOG TOGGLE (player override, CONCEPT.md Pillar 3: "player-
    // toggleable on/off regardless of phase"). Writes fogState.revealAll
    // directly; per game/phase.js's contract that's the ONLY other writer,
    // so this choice sticks until the next actual phase change (which calls
    // applyPhaseFog and overwrites it) — no separate override flag needed.
    fogState.revealAll = !fogState.revealAll;
    updatePhaseHud();
  } else if (e.key === 'c' || e.key === 'C') {
    // PHASE-TRANSITION CONTROL (key half; see also #beginCombatBtn below).
    // One-way prep -> combat for now, per task scope; a scripted trigger
    // (the later tutorial agent) can call the same triggerBeginCombat path.
    triggerBeginCombat();
  } else if (e.key === 'm' || e.key === 'M') {
    // TONE MODE FLIP (Part B — game/director.js's DEFAULT_TONE_MODE is a
    // TEST-ONLY 'silly' default; this key is the quickest way to flip it
    // during a session without touching code/a debug console). Purely a
    // flavor toggle — never affects gating, checklist logic, or the off-
    // track acknowledgement itself, only whether the silly-mode gag chain
    // can trigger (see game/director.js's handleBuildingSold).
    setToneMode(directorState, directorState.toneMode === 'silly' ? 'serious' : 'silly');
    flashMessage(`Tone mode: ${directorState.toneMode}.`);
  } else if (e.key === 'Escape') {
    if (roadMode.active) exitRoadMode();
    if (buildMode.active) exitBuildMode();
    if (sellMode.active) exitSellMode();
    if (battalionAssignMode.active) exitBattalionAssignMode();
    if (prodPanel.classList.contains('open')) exitProdPanel();
    if (importPanel.classList.contains('open')) exitImportPanel();
    if (techPanel.classList.contains('open')) exitTechPanel();
  }
});

// Left-click places point A, then point B. Construction is instant (timed
// construction is a P3/economy-phase concern per CONCEPT.md) — the planner
// routes with findRoadRoute (game/pathfind.js) and lays every tile along the
// dense route, then bumps map.dirty() ONCE so the renderer re-prerenders the
// new road as a single repaint instead of once per tile.
canvas.addEventListener('click', e => {
  if (!roadMode.active) return;
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  if (!roadMode.a) {
    roadMode.a = { x: wx, y: wy };
    roadMode.previewPath = null;
    roadMode.previewGoalTile = null;
    return;
  }
  const route = findRoadRoute(map, roadMode.a.x, roadMode.a.y, wx, wy);
  if (route) {
    for (const t of route.tiles) map.setRoad(t.x, t.y, 1);
    map.dirty();
  }
  roadMode.a = null;
  roadMode.previewPath = null;
  roadMode.previewGoalTile = null;
});

function drawRoadPreview(ctx, worldToScreen) {
  if (!roadMode.active || !roadMode.a) return;
  ctx.save();
  const [ax, ay] = worldToScreen(roadMode.a.x, roadMode.a.y);
  ctx.fillStyle = 'rgba(255,230,150,0.9)';
  ctx.beginPath(); ctx.arc(ax, ay, 5, 0, 6.28); ctx.fill();
  if (roadMode.previewPath && roadMode.previewPath.length) {
    ctx.strokeStyle = 'rgba(255,230,150,0.85)';
    ctx.lineWidth = 2;
    ctx.setLineDash([7, 5]);
    ctx.beginPath();
    ctx.moveTo(ax, ay);
    for (const p of roadMode.previewPath) {
      const [sx, sy] = worldToScreen(p.x, p.y);
      ctx.lineTo(sx, sy);
    }
    ctx.stroke();
    ctx.setLineDash([]);
  }
  ctx.restore();
}

// B-mode footprint preview: green if isValidPlacement (what shift-click's
// exact pin would do right now), red otherwise. Purely a preview — the
// planner's normal (non-shift) click can still choose a different, better-
// scored spot nearby; this just tells the player the literal rule at the
// exact tile under the cursor.
function drawBuildPreview(ctx, worldToScreen) {
  if (!buildMode.active || !buildMode.selectedType || buildMode.previewGx === undefined) return;
  const def = BUILDING_DEFS[buildMode.selectedType];
  const ts = map.tileSize;
  const [x0, y0] = worldToScreen(buildMode.previewGx * ts, buildMode.previewGy * ts);
  const [x1, y1] = worldToScreen((buildMode.previewGx + def.footprint.w) * ts, (buildMode.previewGy + def.footprint.h) * ts);
  ctx.save();
  ctx.fillStyle = buildMode.previewValid ? 'rgba(90,255,140,0.28)' : 'rgba(255,90,90,0.28)';
  ctx.strokeStyle = buildMode.previewValid ? 'rgba(120,255,160,0.9)' : 'rgba(255,120,120,0.9)';
  ctx.lineWidth = 2;
  ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
  ctx.strokeRect(x0, y0, x1 - x0, y1 - y0);
  ctx.restore();
}

// ---------------------------------------------------------------------------
// DEPOSIT FOCUS — the RESOURCE FILTER (playtest: "filter for resources") +
// the goal-driven NEXT-NEEDED highlight (playtest: "highlight what you
// currently need", "ON THE MAP") drawn together, since they're the same
// draw pass over map.deposits and can legitimately both be lit at once (a
// player filtering for tungsten while the plan separately needs titanium
// should see BOTH rings — they're visually distinct so they don't read as
// one confused thing). `focus.filterType` comes from the map-legend panel's
// filter buttons (resourceFilterType below); `focus.needType` comes from the
// live plan's first unsatisfied 'resource' step (nextNeededResourceType) —
// same "read the SAME plan the checklist/ledger already computed" contract
// as updateBuildBarPlanUI. Grease-pencil-styled rings (dashed/hand-marked)
// to stay in the Operations Map idiom rather than a generic glow.
function drawDepositFocus(ctx, worldToScreen, cam, focus) {
  if (!focus || (!focus.filterType && !focus.needType)) return;
  const dpr = devicePixelRatio;
  const t = performance.now() / 1000;
  const pulse = 0.5 + 0.5 * Math.sin(t * 3);
  for (const d of map.deposits || []) {
    const isFiltered = focus.filterType && d.type === focus.filterType;
    const isNeeded = focus.needType && d.type === focus.needType;
    if (!isFiltered && !isNeeded) continue;
    const [sx, sy] = worldToScreen((d.gx + 0.5) * map.tileSize, (d.gy + 0.5) * map.tileSize);
    if (sx < -80 || sx > ctx.canvas.width + 80 || sy < -80 || sy > ctx.canvas.height + 80) continue;
    const baseR = map.tileSize * cam.zoom * dpr * 1.7;
    ctx.save();
    if (isFiltered) {
      // the manual filter: a bold pulsing amber ring, "you asked for this."
      ctx.beginPath();
      ctx.arc(sx, sy, baseR * (1 + pulse * 0.22), 0, Math.PI * 2);
      ctx.setLineDash([baseR * 0.35, baseR * 0.18]);
      ctx.strokeStyle = `rgba(184, 65, 42, ${0.55 + 0.35 * pulse})`;
      ctx.lineWidth = 3 * dpr;
      ctx.stroke();
      ctx.setLineDash([]);
    }
    if (isNeeded) {
      // the plan's own ask: a tighter dashed ring + a small stamped tag,
      // same red/"NEXT" vocabulary the buildbar's next-needed tag uses.
      ctx.beginPath();
      ctx.arc(sx, sy, baseR * 0.82, 0, Math.PI * 2);
      ctx.setLineDash([5 * dpr, 4 * dpr]);
      ctx.strokeStyle = 'rgba(255, 207, 92, 0.92)';
      ctx.lineWidth = 2.4 * dpr;
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.font = `bold ${10 * dpr}px Consolas, monospace`;
      ctx.textAlign = 'center';
      ctx.lineWidth = 2.5 * dpr;
      ctx.strokeStyle = 'rgba(4,8,14,0.75)';
      ctx.strokeText('NEEDED', sx, sy - baseR - 6 * dpr);
      ctx.fillStyle = 'rgba(255, 207, 92, 0.95)';
      ctx.fillText('NEEDED', sx, sy - baseR - 6 * dpr);
    }
    ctx.restore();
  }
}

function drawBuilding(ctx, worldToScreen, cam, b) {
  const ts = map.tileSize;
  const [x0, y0] = worldToScreen(b.gx * ts, b.gy * ts);
  const [x1, y1] = worldToScreen((b.gx + b.def.footprint.w) * ts, (b.gy + b.def.footprint.h) * ts);
  const constructing = b.status === 'constructing';
  ctx.save();
  // P3: an in-progress footprint reads visually distinct — dashed outline,
  // lower fill opacity — so "this isn't a real building yet" is legible at
  // a glance, same information the buildbar/HUD progress readout repeats.
  ctx.fillStyle = b.side === 'player'
    ? (constructing ? 'rgba(70,120,170,0.42)' : 'rgba(70,120,170,0.92)')
    : (constructing ? 'rgba(150,60,60,0.42)' : 'rgba(150,60,60,0.92)');
  ctx.strokeStyle = b.side === 'player' ? '#5fd0ff' : '#ff5a5a';
  ctx.lineWidth = 1.5;
  if (constructing) ctx.setLineDash([5, 4]);
  ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
  ctx.strokeRect(x0, y0, x1 - x0, y1 - y0);
  ctx.setLineDash([]);
  ctx.restore();
  if (b.hp < b.maxHp) {
    const w = x1 - x0;
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(x0, y0 - 8, w, 3);
    ctx.fillStyle = '#6dffb0'; ctx.fillRect(x0, y0 - 8, w * (b.hp / b.maxHp), 3);
  }
  if (constructing) {
    // build-progress bar, below the footprint (the HP bar above it already
    // owns the top edge) — amber to read distinctly from the green HP bar.
    const w = x1 - x0;
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(x0, y1 + 3, w, 4);
    ctx.fillStyle = '#ffcf5c'; ctx.fillRect(x0, y1 + 3, w * b.buildProgress, 4);
    if (cam.zoom > 0.25) {
      ctx.fillStyle = '#ffe9b0';
      ctx.font = '10px Consolas, monospace';
      ctx.textAlign = 'center';
      ctx.fillText(`${Math.round(b.buildProgress * 100)}%`, (x0 + x1) / 2, y1 + 17);
      ctx.textAlign = 'left';
    }
  }
  // P4: production facilities show their live state right on the map —
  // "producing" (green) or "STALLED — needs X" (amber) — same info the econ
  // HUD's Production block repeats in text form, so it's readable at a
  // glance without hunting the HUD for which specific building is stuck.
  if (!constructing && b.prodCategory && cam.zoom > 0.2) {
    const label = b.prodState === 'producing'
      ? `▶ producing (${productionCountLabel(PRODUCTION_DEFS[b.prodCategory], b)})`
      : `⏸ STALLED — ${b.prodStallReason}`;
    ctx.fillStyle = b.prodState === 'producing' ? '#8dffb0' : '#ffcf5c';
    ctx.font = '10px Consolas, monospace';
    ctx.textAlign = 'center';
    ctx.fillText(label, (x0 + x1) / 2, y1 + 15);
    ctx.textAlign = 'left';
  }
  if (b === hovered) { ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5; ctx.strokeRect(x0 - 3, y0 - 3, (x1 - x0) + 6, (y1 - y0) + 6); }
}

// drawUnit/drawProjectile/drawImpacts (per-type silhouettes, real ordnance
// in flight, muzzle flash/recoil, impact FX) now live in engine/renderer.js
// alongside the rest of the battlefield draw code — see that file's "UNITS,
// PROJECTILES, MUZZLE FLASH/RECOIL, IMPACT FX" section header for the full
// rationale. Imported at the top of this file; this used to be two plain
// local functions (a rotated triangle marker + a round projectile dot).

// headless test hook — safe to leave, no gameplay effect. Exposes spawnUnit
// and the attribute tables too, so a test harness can stage its own
// scenarios (e.g. an isolated SAM-vs-tank standoff) beyond the shipped demo.
// fog: exposes detects() directly and the revealAll flag so a test can flip
// fog off (to compare drawn-vs-hidden positions) or query detection without
// having to reverse-engineer it from rendered pixels.
window.__debug = {
  world, renderer, map, spawnUnit, updateUnits, updateProjectiles, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS, nearestWaterPoint,
  fog: Object.assign(fogState, { detects }),
  // pathfinding + road-building hooks (P2), for headless verification:
  // pathfindStats.solves carries {kind, ms, nodes, t} for every A* solve
  // actually run (cache hits don't add entries), roadMode exposes the R-mode
  // UI state, findPath/findRoadRoute let a test harness solve/time routes
  // directly without needing real DOM input, enterRoadMode/exitRoadMode let
  // a test drive the mode without depending on exact key event shape.
  pathfindStats, roadMode, findPath, findRoadRoute, enterRoadMode, exitRoadMode, terrainSample, issueMoveOrder,
  // buildings / B-mode hooks (P2 city layer), for headless verification:
  // BUILDING_DEFS/spawnBuilding/isValidPlacement/sitePlacement let a test
  // stage or query construction directly; buildMode exposes the UI state
  // (selectedType/previewGx/previewGy/previewValid/message) the same way
  // roadMode already does above; enter/exit/select drive the mode without
  // needing real key/click events.
  BUILDING_DEFS, spawnBuilding, isValidPlacement, sitePlacement, buildMode,
  enterBuildMode, exitBuildMode, selectBuildType,
  // economy hooks (P3 — game/economy.js), for headless verification:
  // `economy` is the live state object the real loop mutates every frame
  // (read it directly to observe the auto-ramp with real wall-clock time);
  // ECONOMY_TUNABLES/MOBILIZATION_BANDS expose the tunable data block;
  // canAffordBuilding/buildingCost mirror the B-mode cost check so a test
  // can query affordability without staging a click; spendForBuilding is the
  // matching spend step (same function the B-mode click handler calls right
  // before spawnBuilding — see the canvas 'click' listener above) so a test
  // can place a costed building exactly the way a real click does
  // (afford-check -> spend -> spawn) instead of spawning one for free via
  // spawnBuilding alone, which would silently skip the economy. Exists
  // primarily for topdown/tools/balance-harness.mjs, which drives whole
  // build orders headlessly. simulateEconomy fast-
  // forwards the economy tick in fixed dt steps WITHOUT touching
  // units/projectiles/rendering — a test-only convenience for comparing
  // curves (e.g. "with vs without the accelerator lever") faster than
  // waiting real wall-clock seconds; the real per-frame loop above is what
  // actually proves "ramps on its own during normal play."
  economy, ECONOMY_TUNABLES, MOBILIZATION_BANDS, canAffordBuilding, buildingCost, spendForBuilding,
  // resource/deposit hooks (P3 follow-up — game/resources.js, map.deposits/
  // depositAt from game/mapgen.js via engine/tilemap.js), for headless
  // verification: RESOURCE_DEFS/RESOURCE_LIST/MINE_TUNABLES expose the
  // tunable data block; footprintHasDeposit mirrors the siting check a mine
  // placement goes through, so a test can query it without staging a click.
  // map.deposits/map.depositAt are already reachable off `map` above.
  RESOURCE_DEFS, RESOURCE_LIST, MINE_TUNABLES, footprintHasDeposit,
  simulateEconomy(seconds, stepDt = 0.5) {
    let t = 0;
    while (t < seconds) {
      const step = Math.min(stepDt, seconds - t);
      updateEconomy(world, economy, map, step);
      t += step;
    }
  },
  // production-chain hooks (P4 — game/production.js), for headless
  // verification: PRODUCTION_DEFS is the recipe table itself;
  // categoryForFacility mirrors the reverse lookup updateProduction uses;
  // IMPORT_TUNABLES/importCost/canAffordImport/importResource let a test
  // drive the import fallback directly without staging an #importPanel
  // click; prodPanel/importPanel expose the UI open/selected state the same
  // way buildMode/roadMode already do; enter/exit/select* drive the panels
  // without needing real key/click events. fastForward advances
  // construction + economy + production together in fixed dt steps WITHOUT
  // touching units/projectiles/rendering — lets a test observe "stalled ->
  // mines built -> producing -> N units at t" over simulated minutes in
  // milliseconds of real time, using the EXACT SAME updateBuildings/
  // updateEconomy/updateProduction calls the real per-frame loop below
  // makes, so behavior never diverges between the fast-forwarded test path
  // and normal play.
  PRODUCTION_DEFS, categoryForFacility, IMPORT_TUNABLES, importCost, canAffordImport, importResource,
  prodPanel, importPanel, selectCategory, enterProdPanel, exitProdPanel, enterImportPanel, exitImportPanel,
  get selectedCategory() { return selectedCategory; },
  // RESEARCH hooks (P4 follow-up — game/research.js), for headless
  // verification: TECH_DEFS/RESEARCH_COPY expose the data tables;
  // researchState is the live object the real loop mutates every frame
  // (read .completed/.active directly, or mutate it for a test shortcut);
  // techStatus/canAffordResearch/startResearch/isCategoryUnlocked let a test
  // drive/query the gate directly without staging a click; updateResearch
  // lets a test advance JUST the research timer (fastForward above already
  // includes it, this is for isolated timing tests); techPanel/enter/exit
  // expose the panel's open state the same way prodPanel/importPanel do.
  TECH_DEFS, RESEARCH_COPY, researchState, techStatus, canAffordResearch,
  startResearch, isCategoryUnlocked, updateResearch,
  techPanel, enterTechPanel, exitTechPanel,
  // AMBIENT LIFE hooks (game/ambient.js), for headless verification: `ambient`
  // is the live state object updateAmbient mutates every frame — a test can
  // read vehicle/smoke/shimmer/flicker counts and positions directly (e.g.
  // to confirm motion between two snapshots) without parsing rendered pixels.
  ambient,
  // GAME PHASES (P4 — game/phase.js), for headless verification: phaseState
  // is the live object (read .phase directly); PHASES is the enum;
  // beginCombat drives the exact same one-way transition the C key/button
  // use (fog reconciliation included); toggleFog mirrors the F key's manual
  // override; fogState is already reachable via the `fog` entry above but
  // re-exposed here too under a name that reads clearly next to phase state.
  phaseState, PHASES, phase: {
    get current() { return phaseState.phase; },
    beginCombat: triggerBeginCombat,
    toggleFog() { fogState.revealAll = !fogState.revealAll; updatePhaseHud(); },
    get fogOn() { return !fogState.revealAll; },
    get announcement() { return announcement.text && performance.now() < announcement.until ? announcement.text : null; },
  },
  // PLAYABILITY v1 hooks (game/objectives.js + game/enemyai.js), for
  // headless verification: updateObjectives/evaluateOutcome/CAPTURE_TIME_S
  // let a test fast-forward capture (e.g. `for (let i=0;i<40;i++)
  // objectives.updateObjectives(world, map, 1)` beats waiting out
  // CAPTURE_TIME_S real seconds) and query win/lose directly; updateEnemyAI
  // lets a test drive Part B's objective-seeking in isolation; `outcome`
  // reads the live one-shot gameOutcome value ('victory'|'defeat'|null);
  // showOutcomeScreen/outcomeScreen expose the end-screen DOM/trigger the
  // same way director.areYouFiveDialog/takeoverScreen already do above.
  objectives: {
    updateObjectives, evaluateOutcome, CAPTURE_TIME_S,
    updateEnemyAI,
    // Assault-group coordination hooks (game/enemyai.js), for headless
    // verification: aiGroups() snapshots every live group's phase/rally
    // point/target/membership so a test can watch a group mass then commit
    // together without reverse-engineering it from raw unit positions;
    // aiStateSize() exposes the per-unit scratch Map's size so a test can
    // confirm it's actually pruned for dead units rather than growing
    // unbounded (the v1 report's flagged cleanup gap).
    aiGroups: debugEnemyGroups,
    aiStateSize: debugAiStateSize,
    get outcome() { return gameOutcome; },
    showOutcomeScreen,
    // PLAYER DOCTRINE hooks (game/doctrine.js), for headless verification:
    // updatePlayerDoctrine drives the assignment pass directly (in case a
    // test wants to call it outside the main loop); state() snapshots every
    // live player unit's current post {mapIdx,x,y}; forceAssignTick() skips
    // the real ASSIGN_INTERVAL_S throttle so a test doesn't have to simulate
    // several real seconds of frames just to observe one assignment.
    doctrine: { update: updatePlayerDoctrine, state: debugDoctrineState, forceAssignTick: debugForceAssignTick },
    outcomeScreen: outcomeScreenEl,
    get outcomeOpen() { return outcomeScreenEl.classList.contains('open'); },
    objectiveHud,
  },
  fastForward(seconds, stepDt = 0.5) {
    let t = 0;
    while (t < seconds) {
      const step = Math.min(stepDt, seconds - t);
      updateBuildings(world, step, map);
      updateEconomy(world, economy, map, step);
      updateResearch(researchState, step);
      updateProduction(world, economy, map, step, researchState);
      t += step;
    }
  },
  // FIRST-RUN FRAMEWORK hooks, for headless verification: spawnEnemyLandingForce
  // is exposed directly too (in ADDITION to going through phase.beginCombat
  // above) so a test can check it's idempotent — call it a second time
  // in isolation and diff world.units.length, without needing to also flip
  // phase state to do so. guidance exposes the panel's DOM + visibility +
  // toggle so a test can confirm dismiss/restore without real key events, and
  // COPY so a test (or the designer) can find every placeholder string from
  // one place without grepping the file.
  spawnEnemyLandingForce, COPY,
  // MAIN MENU / SCENARIO hooks (game/menu.js), for headless verification: a
  // test can read which scenario was picked (or confirm the deep-link
  // default of null/1.0 when no menu was shown), and reach SCENARIO_DEFS/
  // MENU_COPY without re-importing the module. ENEMY_LANDING_BASE +
  // scaledCount let a test compute the expected spawn count for the active
  // scenarioDifficulty without recounting world.units by hand.
  scenario: { id: scenarioId, difficulty: scenarioDifficulty, startingEcoMult },
  SCENARIO_DEFS, MENU_COPY, ENEMY_LANDING_BASE, scaledCount,
  guidance: {
    panel: guidancePanel,
    get visible() { return !guidancePanel.classList.contains('hidden'); },
    toggle: toggleGuidance,
    show: () => setGuidanceVisible(true),
    hide: () => setGuidanceVisible(false),
  },
  // TECH/PRODUCTION TREE VIEW hooks (game/techtree.js + game/techtreeview.js),
  // for headless verification per the task's explicit requirement — a canvas
  // overlay has no DOM nodes per graph node, so a test can't just querySelect
  // its way to "is this node built." getTechTreeGraph() exposes the derived
  // node/edge list (including each node's .x/.y in the view's own coordinate
  // space); computeNodeState(node, {world, economy, researchState}) answers
  // "what does this node's live state/detail read as RIGHT NOW" without
  // needing the view open at all. techTreeView itself exposes show/hide/
  // isOpen/cam (drive pan/zoom or read the camera directly) and, critically,
  // nodeAtPoint(clientX, clientY) — the exact hit-test the real mousemove
  // listener uses internally — so a test can confirm hover resolves to the
  // right node using the SAME screen coordinates a real cursor would report,
  // with no synthetic mouse events required.
  techTree: {
    getGraph: getTechTreeGraph,
    computeNodeState: (node) => computeNodeState(node, { world, economy, researchState }),
    view: techTreeView,
    toggle: toggleTechTree,
  },
  // GOAL-DRIVEN TUTORIAL hooks (Part A — game/tutorialplan.js), for headless
  // verification: goalState is the live Set-backed state (read .goals
  // directly, or mutate via toggleGoal below without needing a real click on
  // the tree canvas); getPlan() returns the SAME computePlan(...) output the
  // checklist UI renders every frame, so a test can assert on step order/
  // satisfaction directly; TUTORIAL_COPY exposes every placeholder string
  // from one place, same convention as COPY/RESEARCH_COPY above.
  tutorial: {
    goalState,
    toggleGoal: (nodeId) => toggleGoal(goalState, nodeId),
    isGoalNode,
    getPlan: getCurrentPlan,
    TUTORIAL_COPY,
  },
  // REACTIVE DIRECTOR hooks (Part B — game/director.js + this file's SELL
  // MODE/DIRECTOR sections), for headless verification: `state` is the live
  // directorState object (read .toneMode/.bubble/.gag directly);
  // setToneMode/DIRECTOR_COPY mirror the module's own exports so a test
  // doesn't need a separate import; sellBuilding/sellMode/enter/exitSellMode/
  // performSell let a test drive the sell action (and, in silly mode, the
  // whole self-sabotage gag chain) without staging real clicks;
  // resolveAreYouFive/resetGag drive the dialog's two outcomes directly;
  // the dialog/takeover DOM elements are exposed too so a test can assert on
  // visibility (.open class) the same way it already does for
  // #guidancePanel above.
  director: {
    state: directorState,
    setToneMode: (mode) => setToneMode(directorState, mode),
    get toneMode() { return directorState.toneMode; },
    get gagStage() { return directorState.gag.stage; },
    get bubbleText() {
      return directorState.bubble.text && performance.now() < directorState.bubble.until ? directorState.bubble.text : null;
    },
    DIRECTOR_COPY,
    reactToAction: (action) => reactToAction(directorState, action, getCurrentPlan()),
    resolveAreYouFive: (answeredYes) => resolveAreYouFive(directorState, answeredYes),
    resetGag: () => resetGag(directorState),
    sellBuilding, sellMode, enterSellMode, exitSellMode, performSell, playerBuildingAt,
    areYouFiveDialog: areYouFiveDialogEl,
    takeoverScreen: takeoverScreenEl,
    get areYouFiveOpen() { return areYouFiveDialogEl.classList.contains('open'); },
    get takeoverOpen() { return takeoverScreenEl.classList.contains('open'); },
    clickYes: () => areYouFiveYesBtn.click(),
    clickNo: () => areYouFiveNoBtn.click(),
  },
  // FIRST-RUN INTRO SEQUENCE hooks (game/intro.js), for headless
  // verification: `state` is the live introState object (read
  // .active/.beat/.dissolve directly); `panel`/`skipBtn` are the DOM
  // elements so a test can assert visibility the same way it already does
  // for #guidancePanel/#areYouFiveDialog above; `continueClick`/`skipClick`
  // drive the two real buttons (not the underlying transition functions
  // directly) so a test exercises the exact same path a real player click
  // does, including the updateIntroUI() refresh; INTRO_COPY exposes every
  // placeholder string from one place, same convention as
  // COPY/TUTORIAL_COPY/DIRECTOR_COPY above.
  intro: {
    state: introState,
    panel: introPanelEl,
    skipBtn: introSkipBtn,
    get visible() { return !introPanelEl.classList.contains('hidden'); },
    get skipVisible() { return introSkipBtn.classList.contains('show'); },
    continueClick: () => introContinueBtn.click(),
    skipClick: () => introSkipBtn.click(),
    INTRO_COPY,
  },
  // BATTALION ENTITY + GROUPING LAYER hooks (B1 — game/battalions.js), for
  // headless verification: spawnBattalion/updateBattalions are the real
  // functions the loop wires in above (mirrored here, not reimplemented);
  // BATTALION_TEMPLATES/battalionStrength expose the data + read-only helper;
  // `list` reads the live world.battalions array the same way other __debug
  // sub-objects expose their own live state.
  battalions: {
    spawnBattalion: (opts) => spawnBattalion(world, map, opts),
    updateBattalions, BATTALION_TEMPLATES, battalionStrength,
    list: () => world.battalions,
  },
  // BATTALION DOCTRINE hooks (B3 — game/battalionDoctrine.js), for headless
  // verification: updateBattalionDoctrine drives the assignment pass
  // directly; assignSector/setStance/cycleStance are the real functions the
  // Battalions panel wires in below (mirrored here, not reimplemented) —
  // world is bound internally so a test calls e.g.
  // battalionDoctrine.assignSector(bnId, cityIdx) with only the battalion's
  // own args, same binding convention every other __debug sub-object here
  // uses; debugForceTick bypasses ASSIGN_INTERVAL_S's real-time throttle for
  // a test, same escape hatch as doctrine.debugForceAssignTick above.
  battalionDoctrine: {
    updateBattalionDoctrine: (dt) => updateBattalionDoctrine(world, map, dt),
    assignSector: (bnId, cityIdx) => assignSector(world, bnId, cityIdx),
    setStance: (bnId, stance) => setStance(world, bnId, stance),
    cycleStance: (bnId) => cycleStance(world, bnId),
    state: debugBattalionDoctrineState,
    debugForceTick: debugForceBattalionDoctrineTick,
  },
  // BATTALION MORALE hooks (B4 — game/battalionMorale.js), for headless
  // verification: updateBattalionMorale drives the real per-frame morale/
  // cohesion pass directly (world/map bound internally, same convention as
  // battalionDoctrine above — call with just `dt`); moraleState reads a
  // battalion record's current fresh/steady/shaken/broken label;
  // setMorale(bnId, v) is the test-only escape hatch to force a battalion
  // straight to a given morale value (e.g. into `broken` to exercise the
  // rout path without waiting out the real decay rates).
  battalionMorale: {
    updateBattalionMorale: (dt) => updateBattalionMorale(world, map, dt),
    moraleState,
    setMorale: (bnId, v) => setBattalionMorale(world, bnId, v),
  },
  // REINFORCEMENTS hooks (B2 — game/reinforcements.js), for headless
  // verification: request* are the real request functions the loop/UI wire
  // in above (mirrored here, not reimplemented); REINFORCEMENT_DEFS exposes
  // the tunable cost/leadtime data block; queue() reads the live
  // world.reinforcements array the same way battalions.list() above reads
  // world.battalions; updateReinforcements/playerCapital let a test drive
  // completion and capital-resolution directly without waiting on real
  // wall-clock frames.
  reinforcements: {
    requestElite: () => requestElite(world, economy, map),
    requestStandard: () => requestStandard(world, economy, map),
    raiseMilitia: (city) => raiseMilitia(world, economy, map, city),
    updateReinforcements: (dt) => updateReinforcements(world, economy, map, dt),
    REINFORCEMENT_DEFS,
    queue: () => world.reinforcements,
    playerCapital: () => playerCapital(map),
  },
};

let last = performance.now();
function loop(now) {
  const rawDt = Math.min(0.05, (now - last) / 1000);
  last = now;
  // HIT-STOP (see HITSTOP_ENABLED above, default OFF): while active, `dt` —
  // which every gameplay system below (units/economy/production/AI) reads —
  // runs at a fraction of real time. rawDt is preserved for the render-only
  // effects (shake, ambient, impact vfx) further down so those never
  // themselves appear to freeze.
  let dt = rawDt;
  if (HITSTOP_ENABLED) {
    if (hitstop.timer > 0) { hitstop.timer -= rawDt; dt = rawDt * HITSTOP_SCALE; }
    hitstop.cooldown = Math.max(0, hitstop.cooldown - rawDt);
  }

  try {  // CRASH RESILIENCE — a throwing frame must not kill the rAF chain; see catch/finally at the end of loop()

  // fog must be current BEFORE targeting runs (pickTarget/nearestEnemy read
  // it) and before rendering reads it for draw-filtering/hover/overlay.
  computeFog(world, map);

  clearClaims();

  // PLAYABILITY v1 (CONCEPT.md's settled "Playability v1" section) —
  // enemy-AI objective assignment (Part B), city capture, and win/lose
  // (Part A). COMBAT-phase only, and frozen the instant an outcome fires:
  // PREP stays peaceful/open per the hard constraint (no invasion, no
  // win/lose, no forced actions during build-up), and once gameOutcome is
  // set the war state freezes at the moment of victory/defeat instead of
  // continuing to capture/re-capture cities behind the end screen.
  const combatActive = phaseState.phase === PHASES.COMBAT && !gameOutcome;
  if (combatActive) updateEnemyAI(world, map, dt); // sets u.order toward each unit's objective BEFORE updateUnits consumes it this frame
  // PLAYER DOCTRINE runs every frame in BOTH phases (unlike updateEnemyAI,
  // which only matters once the invasion lands) — a freshly built/placed
  // unit should start dispersing to a sensible garrison post during PREP
  // already, not just once combat begins. Also sets u.order (or leaves it
  // alone — see doctrine.js's arbitration rules) BEFORE updateUnits
  // consumes it this frame, same ordering contract as updateEnemyAI.
  updatePlayerDoctrine(world, map, dt);
  // B3: the battalion-level analogue of the doctrine call just above — sets
  // u.order for battalion-tagged sub-units toward their formation's shared
  // sector/stance hold-point (see game/battalionDoctrine.js's header). Also
  // runs in both phases, same reasoning, and also BEFORE updateUnits.
  updateBattalionDoctrine(world, map, dt);

  updateUnits(world, dt, map);
  // B1 battalion bookkeeping: prune dead/removed sub-units from each
  // battalion's roster against the unit state updateUnits just resolved
  // this frame. Pure maintenance — never gates or alters combat; see
  // game/battalions.js's header for the additive-only contract.
  updateBattalions(world, dt);
  // B4: morale/cohesion update (game/battalionMorale.js) — reads this
  // frame's freshly-pruned battalion rosters (hence AFTER updateBattalions
  // above) and writes bn.morale/bn.cohesion + each sub-unit's defenseMult.
  // Runs in both phases like the other battalion passes above: a battalion
  // sitting in friendly territory during PREP should already be trending
  // toward `fresh`, not starting its clock only once combat begins.
  updateBattalionMorale(world, map, dt);
  // B2: tick the reinforcement queue (game/reinforcements.js) — resolves
  // completed elite/standard/militia jobs into real spawnBattalion() calls.
  // Runs in BOTH phases (like updatePlayerDoctrine above), not gated on
  // combatActive: a player should be able to requisition/queue battalions
  // during PREP too, same as any other build/production action.
  updateReinforcements(world, economy, map, dt);

  if (combatActive) {
    const captureEvents = updateObjectives(world, map, dt);
    for (const ev of captureEvents) {
      // Optional low-priority director hook (CONCEPT.md: "may announce a
      // city captured/lost via its existing bubble — only if trivial").
      announceCityEvent(directorState, ev.to === 'player'
        ? COPY.CITY_RECAPTURED_BY_PLAYER(ev.city.name)
        : COPY.CITY_CAPTURED_BY_ENEMY(ev.city.name));
    }
    if (invasionState) invasionState.elapsed += dt; // drives the hold-out backstop in evaluateOutcome
    const outcome = evaluateOutcome(world, map, invasionState);
    if (outcome) { gameOutcome = outcome; showOutcomeScreen(outcome); }
  }

  updateBuildings(world, dt, map);
  // P3: the mobilization economy ticks every frame, unconditionally — this
  // is the "zero player input" ramp (game/economy.js). Runs after
  // updateBuildings so a building that JUST completed construction this
  // frame already counts toward econ effects this same tick.
  updateEconomy(world, economy, map, dt);
  // P4 follow-up: research (game/research.js) ticks right after the economy
  // (a project's IC/resource cost was already charged up front at
  // startResearch time, so this doesn't need to read this frame's pools —
  // it only advances the active project's timer) and BEFORE production, so
  // a tech that completes THIS frame already reads as unlocked when
  // updateProduction checks it a few lines down — same "just-finished-this-
  // frame already counts" ordering rule updateBuildings/updateEconomy/
  // updateProduction already follow relative to each other.
  updateResearch(researchState, dt);
  // P4: production facilities (game/production.js) run AFTER the economy
  // tick so they read this frame's freshly-accrued resource/IC/manpower
  // pools, same ordering rationale as updateEconomy running after
  // updateBuildings (a facility that just finished construction this frame
  // already counts).
  updateProduction(world, economy, map, dt, researchState);
  // DIRECTOR: first-production milestone check (see checkProductionMilestones
  // above) — cheap (linear scan over world.buildings, same cost class as the
  // HUD's own per-facility production readout below), so unconditional here.
  checkProductionMilestones();
  updateProjectiles(world, dt);
  for (const h of world.hits) h.life -= dt;
  world.hits = world.hits.filter(h => h.life > 0);

  // COMBAT-FEEL/AUDIO EVENT QUEUE DRAIN (see world.sfx's own comment up near
  // its creation) — updateUnits/updateProjectiles above just pushed this
  // frame's fire/impact events; turn each into sound (game/audio.js),
  // screen-shake trauma, and lingering smoke/scorch (game/impactfx.js), then
  // clear the queue so next frame starts empty. Wrapped defensively per-event
  // (never let a bad weapon key or an audio hiccup break the sim loop) even
  // though the CRASH RESILIENCE try/catch around all of loop() already backs
  // this up.
  for (const evt of world.sfx) {
    try {
      playSfx(evt, renderer.cam);
      const wdef = WEAPON_DEFS[evt.weapon];
      const kind = wdef && wdef.kind;
      if (evt.kind === 'fire') {
        // only heavy tube/rail-launched fire shakes the camera at all — an
        // MG/autocannon burst or a missile launch is comparatively gentle,
        // matching the reference doc's own "MG light / cannon heavy" split
        // and the task brief's "significant sfx events" scoping
        if (kind === 'ballistic') addTrauma(0.10 * screenProximity(evt.x, evt.y));
      } else if (evt.kind === 'impact') {
        spawnImpactFx(impactFx, evt.x, evt.y, evt.weapon);
        if (kind !== 'gun') {
          const prox = screenProximity(evt.x, evt.y);
          addTrauma(0.24 * prox);
          if (HITSTOP_ENABLED && prox > 0.5 && hitstop.cooldown <= 0) {
            hitstop.timer = HITSTOP_DURATION;
            hitstop.cooldown = HITSTOP_COOLDOWN;
          }
        }
      }
    } catch (e) { /* one bad sfx event must not break the frame */ }
  }
  world.sfx.length = 0;
  updateImpactFx(impactFx, rawDt); // real cadence, not the (possibly hit-stopped) sim dt — see updateShake's comment
  updateShake(rawDt);

  // AMBIENT LIFE (game/ambient.js) — updated every frame, unconditional on
  // phase (PREP or COMBAT): the whole point is the world doesn't go still
  // just because you're still in the build-up. Purely visual — see that
  // file's header for the "never touches gameplay" contract.
  updateAmbient(ambient, world, map, dt);

  // Computed once per frame and threaded into BOTH the map's deposit-focus
  // overlay (right below) and updateGuidancePanel further down, so the
  // ledger/checklist/map "what does the plan need right now" answer can
  // never disagree with itself within a single frame.
  const framePlan = getCurrentPlan();

  renderer.frame(map, (ctx, worldToScreen, cam) => {
    drawFogOverlay(ctx, worldToScreen, 'player', world, map);
    // RESOURCE FILTER + NEXT-NEEDED (playtest: "filter for resources",
    // "highlight what you currently need"). Drawn early, right on top of
    // fog/terrain and under buildings/units, so a highlighted deposit still
    // reads clearly even under a cluster of unit markers.
    drawDepositFocus(ctx, worldToScreen, cam, { filterType: resourceFilterType, needType: nextNeededResourceType(framePlan) });
    // ground-level ambient life (traffic/shimmer/flicker/flag) BEFORE
    // buildings/units so it reads as part of the ground, not floating on
    // top of the things that actually matter
    drawAmbientGround(ambient, ctx, worldToScreen, cam);
    // scorch decals: ground layer, same ordering rationale as ambient life
    // above — a battle-worn patch of ground reads as part of the terrain,
    // not floating on top of the units standing near it
    drawScorch(impactFx, ctx, worldToScreen, cam);
    for (const b of world.buildings) {
      // fog: same rule as units — an undetected enemy building doesn't draw.
      if (b.side !== 'player' && !detects('player', b)) continue;
      drawBuilding(ctx, worldToScreen, cam, b);
    }
    // factory smoke drawn AFTER buildings so plumes visibly rise above
    // rooftops instead of sitting under them
    drawAmbientSmoke(ambient, ctx, worldToScreen, cam);
    for (const p of world.projectiles) drawProjectile(ctx, worldToScreen, cam, p);
    for (const u of world.units) {
      // fog: only draw enemy units the player side currently detects. Own
      // units always draw regardless (you always see yourself).
      if (u.side !== 'player' && !detects('player', u)) continue;
      drawUnit(ctx, worldToScreen, cam, u, u === hovered);
    }
    // impact smoke wisps drawn ABOVE units — same ordering as ambient.js's
    // own factory smoke above, so drifting wisps read as sitting over the
    // battlefield instead of tucked under a unit marker
    drawSmoke(impactFx, ctx, worldToScreen, cam);
    drawImpacts(ctx, worldToScreen, cam, world.hits);
    drawRoadPreview(ctx, worldToScreen);
    drawBuildPreview(ctx, worldToScreen);
  });

  // TECH/PRODUCTION TREE VIEW — drawn on its own dedicated canvas, on top of
  // (not instead of) the main game canvas above: the game keeps simulating
  // underneath (economy/production/research all tick every frame regardless
  // of which panels are open, per the loop's own ordering), so the view
  // always reflects live state the instant it's opened, and closing it
  // drops straight back into an unpaused game. render() itself is a no-op
  // when the view isn't open (checked first thing inside it), so this call
  // is cheap enough to leave unconditional here.
  //
  // INTRO SEQUENCE's 'discard' beat takes over this exact canvas for its
  // short flourish instead of the tree's own render() — see
  // game/intro.js's drawDiscardFlourish header comment for why drawing on
  // top of the tree's last real frame (rather than calling render() again,
  // which would just redraw the full graph underneath the burn every frame)
  // is what makes the burnt area reveal the live map below it. Every other
  // beat/state falls through to the unchanged render() call.
  if (introState.dissolve.running) {
    const introProgress = discardProgress(introState);
    const introCtx = techTreeCanvas.getContext('2d');
    drawDiscardFlourish(introCtx, techTreeCanvas.width, techTreeCanvas.height, introProgress);
    if (introProgress >= 1) {
      finishDiscard(introState);
      techTreeView.hide();
      beginHandoff(introState);
      updateIntroUI();
    }
  } else {
    techTreeView.render();
  }

  card.show(techTreeView.isOpen ? null : hovered, lastMouse.x, lastMouse.y);
  const alive = { player: 0, enemy: 0 };
  for (const u of world.units) alive[u.side]++;
  const roadHint = roadMode.active
    ? (roadMode.a ? 'road mode: click point B (Esc/right-click to cancel)' : 'road mode: click point A (Esc/right-click to exit)')
    : 'R: build road';
  const buildHint = buildMode.active
    ? (buildMode.selectedType ? 'build mode: click a rough spot (shift-click to pin exact) — Esc to exit' : 'build mode: pick a building below — Esc to exit')
    : 'B: build';
  const battalionHint = battalionAssignMode.active
    ? '  |  assign sector: click a city to rally this battalion (Esc/right-click to cancel)'
    : '';
  const flash = (buildMode.message && performance.now() < buildMode.messageUntil) ? `  |  ${buildMode.message}` : '';
  // Island name + seed shown subtly up front (map.seed is only set for a
  // procedurally-generated map — see game/mapgen.js/loadGeneratedMap; an
  // authored file loaded via ?map= just shows its name) so a player can
  // share/reproduce a good island via ?seed=N.
  const seedTag = map.seed !== undefined ? ` (seed ${map.seed})` : '';
  hud.textContent = `${map.name}${seedTag} — player ${alive.player} vs enemy ${alive.enemy} — right-click: move order — drag: pan — wheel: zoom — ${roadHint} — ${buildHint}${battalionHint}${flash}`;

  // PHASE HUD + announcement banner (game/phase.js). Refreshed every frame
  // (cheap DOM writes) rather than only on the triggering events, so a
  // headless test flipping fogState.revealAll directly via window.__debug
  // still sees the HUD reflect it next frame.
  updatePhaseHud();
  // INTRO SEQUENCE — same "cheap, unconditional, just refresh it" refresh as
  // updatePhaseHud just above (every transition already calls this too, so
  // this is a defensive no-op most frames, not the only place it's called).
  updateIntroUI();
  const announcing = announcement.text && performance.now() < announcement.until;
  announcementDiv.classList.toggle('show', !!announcing);
  if (announcing) announcementDiv.textContent = announcement.text;

  // PLAYABILITY v1 — objective HUD refresh (see updateObjectiveHud above),
  // same "cheap, unconditional every frame" treatment as updatePhaseHud.
  updateObjectiveHud();

  // GUIDANCE PANEL — dynamic checklist refresh (see updateGuidancePanel
  // above) — recomputes the reverse-planned step list from LIVE state every
  // frame; cheap enough to run unconditionally (small graph, see
  // game/tutorialplan.js). A no-op visual update when the panel is dismissed
  // (it's just hidden via CSS, not torn down), so re-showing it later still
  // reflects current progress.
  updateGuidancePanel(framePlan);

  // DIRECTOR corner bubble (Part B) — same transient-text pattern as the
  // phase `announcement` object just above, own DOM element/corner though
  // (this one never occludes anything else and is meant to feel like a
  // running commentary, not a big banner event).
  const bubbleActive = directorState.bubble.text && performance.now() < directorState.bubble.until;
  directorBubbleEl.classList.toggle('show', !!bubbleActive);
  if (bubbleActive) directorBubbleEl.textContent = directorState.bubble.text;

  // RESEARCH PANEL live refresh (see the RESEARCH PANEL section above) —
  // cheap (six static rows, text/visibility updates only), so unconditional
  // every frame same as updateGuidancePanel just above.
  updateTechPanelUI();

  // REINFORCEMENTS PANEL live refresh (B2 — see the REINFORCEMENTS PANEL
  // section above) — cheap (three static rows + a short queue list), same
  // "unconditional every frame" spirit as updateTechPanelUI just above.
  updateReinforcePanelUI();
  updateBattalionPanelUI();

  // Resource stockpile line (P3 follow-up — game/resources.js): compact,
  // icons (colored glyph spans) + amount + current rate, one resource per
  // line-item, joined with a couple of spaces so it wraps as a paragraph
  // instead of forcing #econHud wider. Iterates RESOURCE_LIST generically —
  // a new resource entry there shows up here with no HUD change.
  const resourceLine = RESOURCE_LIST.map(r => {
    const amt = economy.resources[r.id] || 0;
    const rate = economy.resourceRates[r.id] || 0;
    return `<span style="color:${r.color}">${r.glyph}</span> ${amt.toFixed(0)} (+${rate.toFixed(2)}/s)`;
  }).join('&nbsp;&nbsp;');

  // P3 economy HUD — compact readout: pool/cap + current rate for IC and
  // manpower, mobilization % + its band label, military output + rate, and
  // (below) the resource stockpiles mines feed.
  // P4: one status line per complete player production facility — "state
  // (producing/stalled-needs-X) + rate + units made so far", generic over
  // whatever's in world.buildings (no per-category branching here; a
  // facility's own b.prodState/b.prodStallReason/b.producedCount, set by
  // game/production.js's updateProduction, carry everything this needs).
  const prodLines = [];
  for (const b of world.buildings) {
    if (b.side !== 'player' || !b.prodCategory) continue;
    const prod = PRODUCTION_DEFS[b.prodCategory];
    const stateTxt = b.prodState === 'producing'
      ? `producing (${(1 / prod.recipe.buildTimePerUnit).toFixed(2)}/s)`
      : `STALLED — ${b.prodStallReason}`;
    prodLines.push(`${prod.name}: ${stateTxt} — ${productionCountLabel(prod, b)}`);
  }
  const prodBlock = prodLines.length ? `<b>Production</b>\n${prodLines.join('\n')}` : '';

  // SUMMARY (always visible, collapsed-tab default) — the four top-line
  // totals a player needs at a glance, one compact line each. Everything
  // that used to make this panel tall (per-resource stockpile line +
  // per-facility production status) moved to the DETAIL sheet below, which
  // only renders visibly once the player expands the ledger — see
  // setEconHudExpanded above for the de-occlusion fix this implements.
  econHudSummaryEl.innerHTML =
    `<b>IC</b> ${economy.ic.toFixed(0)}/${economy.icCap.toFixed(0)} (+${economy.icRate.toFixed(2)}/s)  `
    + `<b>MP</b> ${economy.manpower.toFixed(0)}/${economy.manpowerCap.toFixed(0)} (+${economy.manpowerRate.toFixed(2)}/s)\n`
    + `<b>Infl</b> ${economy.influence.toFixed(0)}/${economy.influenceCap.toFixed(0)} (+${economy.influenceRate.toFixed(2)}/s)\n`
    + `<b>Mobilization</b> ${economy.mobilizationLevel.toFixed(1)}% (+${economy.mobilizationRate.toFixed(3)}/s) — ${economy.band.label}`;

  econHudDetailInnerEl.innerHTML =
    `<b>Military Output</b> ${economy.militaryOutput.toFixed(1)} (+${economy.militaryOutputRate.toFixed(2)}/s)\n`
    + `Arming quality x${economy.armingQuality.toFixed(2)}\n`
    + `<b>Resources</b>\n${resourceLine}`
    + (prodBlock ? `\n${prodBlock}` : '');

  } catch (err) {
    // A single throwing frame must not brick the session: an uncaught throw
    // here would stop the requestAnimationFrame chain and freeze the whole
    // game (what "the game crashed" looks like to a player). Log it — throttled
    // so a persistent per-frame error doesn't flood the console — and keep the
    // loop alive so the game degrades to a recoverable glitch, not a dead
    // canvas. (This is a safety net, NOT a substitute for fixing a real bug the
    // log surfaces.)
    if (!loop._lastErrAt || performance.now() - loop._lastErrAt > 2000) {
      console.error('[loop] recovered from a frame error:', err);
      loop._lastErrAt = performance.now();
    }
  } finally {
    requestAnimationFrame(loop);
  }
}
requestAnimationFrame(loop);
