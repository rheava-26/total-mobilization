// REACTIVE / ADAPTIVE DIRECTOR (Part B — CONCEPT.md's settled "Reactive /
// adaptive guidance" paragraph: "the game rolls with you"; the "Serious /
// Silly tone mode" paragraph; the "Corner commentator — the 'little guy'"
// direction, part (1), the MECHANIC). This module is the placeholder
// stand-in the corner-commentator direction explicitly asks for: an event-
// driven speech-bubble mechanic with NO character art (deferred, per that
// same paragraph — "must NOT be faked with janky auto-generated art"). Two
// jobs, both data-driven off the real game tables, no hardcoded action or
// building names:
//   1. OFF-TRACK ACKNOWLEDGEMENT — classify a player action against the
//      CURRENT plan (game/tutorialplan.js computePlan output) and surface a
//      brief line that acknowledges + re-orients, never blocks. Runs in
//      BOTH tone modes (acknowledging deviation is the open-principle
//      behavior itself, not a joke).
//   2. THE SILLY-MODE-ONLY SELF-SABOTAGE GAG CHAIN — a tiny explicit state
//      machine, entirely inert outside toneMode==='silly' AND outside the
//      exact escalation sequence (sell a key economic building, THEN sell
//      the pity-replacement the game quietly placed) — never a punishment
//      for ordinary selling.
//
// This file owns state SHAPES and pure classification/transition logic; it
// takes world/map/economy as explicit arguments (same "operate on state
// objects passed in" shape as game/economy.js's updateEconomy or
// game/production.js's updateProduction) rather than closing over them, so
// it stays a plain, testable module — main.js is the only place that wires
// it to real DOM (the bubble text, the "are you five" dialog, the takeover
// screen).
import { BUILDING_DEFS, spawnBuilding, sitePlacement } from './buildings.js';
import { PRODUCTION_DEFS } from './production.js';
import { RESOURCE_DEFS } from './resources.js';

// ---------------------------------------------------------------------------
// TONE MODE — CONCEPT.md's settled "Serious / Silly tone mode" paragraph:
// "a minimal toneMode lever ... just the flag" (the full two-variant copy
// system is explicitly a LATER piece, not built here). A single flag that
// gates every line of sass below.
//
// TEST DEFAULT, CLEARLY MARKED: 'silly' so the gag chain is visible while
// testing/reviewing this pass, per the task's explicit instruction. FLIP TO
// 'serious' (or wire a real settings-menu control) once the designer wants
// the quiet default for normal play — either edit this constant, or call
// setToneMode(directorState, 'serious') at boot (main.js also exposes a key
// binding and a window.__debug hook, see main.js's director section).
export const DEFAULT_TONE_MODE = 'silly'; // <-- FLIP HERE for the quiet default; TEST-ONLY value for now

// ---------------------------------------------------------------------------
// PLACEHOLDER COPY (designer to rewrite) — same convention as main.js's own
// COPY block, game/research.js's RESEARCH_COPY, and game/tutorialplan.js's
// TUTORIAL_COPY: every player-facing string below is a plain, functional
// stand-in marked [PLACEHOLDER]. The funny voice in the gag-chain lines is
// the designer's to write — these are deliberately flat/functional so
// nothing here reads as "the" voice of the game.
export const DIRECTOR_COPY = {
  ON_TRACK: name => `[PLACEHOLDER] ${name} — right on plan.`,
  OFF_TRACK: (name, nextStepLabel) => `[PLACEHOLDER] Huh, ${name}? Wasn't the plan — but sure, we can still get there. Next up: ${nextStepLabel}`,
  OFF_TRACK_NO_NEXT: name => `[PLACEHOLDER] ${name}, noted. Not on the current plan, but we'll roll with it.`,
  SELL_NEUTRAL: name => `[PLACEHOLDER] ${name} sold.`,
  // Gag chain, stage 1: confused auto-replacement.
  CONFUSED_REPLACED: name => `[PLACEHOLDER] Wait, you sold the ${name}? ...the nation got confused and quietly built another one. Carry on.`,
  // Gag chain, stage 2: the confirm dialog.
  ARE_YOU_FIVE_TITLE: '[PLACEHOLDER] Are you five years old?',
  ARE_YOU_FIVE_YES: '[PLACEHOLDER] Yes',
  ARE_YOU_FIVE_NO: '[PLACEHOLDER] No',
  // Gag chain, stage 3a: "Yes" -> instant black takeover. Deliberately
  // nonchalant/deadpan — one flat line, no drama (designer-authored, not a
  // placeholder). Approximates "the game instantly closes": a web page can't
  // force-close its own tab, so we hard-cut to black instantly (no fade) and
  // attempt window.close() as a bonus (see main.js).
  TAKEOVER_LINES: [
    "you're too young to play this game",
  ],
  // Gag chain, stage 3b: "No" -> fired -> restart.
  FIRED_LINES: [
    '[PLACEHOLDER] You have been fired for misappropriating funds.',
    '[PLACEHOLDER] Restarting the war effort...',
  ],
};

// ---------------------------------------------------------------------------
// DIRECTOR STATE — create*State() shape, same convention as
// game/research.js's createResearchState / game/tutorialplan.js's
// createGoalState. Owned once by main.js, threaded through every function
// below.
//   toneMode — see DEFAULT_TONE_MODE above.
//   bubble   — { text, until } transient corner-bubble content; main.js
//              reads this every frame the same way it already reads the
//              phase-transition `announcement` object.
//   gag      — the self-sabotage state machine: 'idle' (nothing has
//              happened yet) -> 'awaitingPityDelete' (a key economic
//              building was sold and quietly replaced; watching for THAT
//              specific replacement to also be sold) -> 'confirmDialog'
//              (main.js is showing the "are you five" dialog) ->
//              'takeover' | 'fired' (terminal — main.js drives the actual
//              screen/reload from here).
export function createDirectorState() {
  return {
    toneMode: DEFAULT_TONE_MODE,
    bubble: { text: null, until: 0 },
    gag: { stage: 'idle', pityBuildingId: null },
  };
}

export function setToneMode(directorState, mode) {
  if (mode === 'silly' || mode === 'serious') directorState.toneMode = mode;
}

function announce(directorState, text, ms = 4200) {
  directorState.bubble.text = text;
  directorState.bubble.until = performance.now() + ms;
}

// ---------------------------------------------------------------------------
// KEY ECONOMIC BUILDING — generalized by ROLE (data), NOT by name (the task
// is explicit: "do NOT hardcode a building literally named 'port'; pick it
// by role/importance from the data"). game/economy.js's updateEconomy reads
// exactly four econ fields to run the CORE ic/manpower economy: icRate,
// manpowerRate, icCapBonus, manpowerCapBonus. A building carrying any of
// those (today: factory/barracks/supplyDepot) is "key economic" — a
// production facility, mine, radar station, or research bureau carries none
// of them (they feed a different accounting path entirely: resources,
// vision, or the research gate), so this naturally excludes them without
// ever naming a building. A future building the designer adds with, say,
// icRate on it (an "embassy," a "trade port," whatever it's called) becomes
// "key economic" automatically, with zero changes here.
function isKeyEconomicBuilding(key) {
  const econ = BUILDING_DEFS[key] && BUILDING_DEFS[key].econ;
  if (!econ) return false;
  return !!(econ.icRate || econ.manpowerRate || econ.icCapBonus || econ.manpowerCapBonus);
}

// ---------------------------------------------------------------------------
// OFF-TRACK ACKNOWLEDGEMENT (task point 4). `action` is
// { kind: 'building'|'category'|'import'|'sell', key } — main.js raises one
// at each real action point (a building placed, a category's facility comes
// online, a resource imported, a building sold). `plan` is
// game/tutorialplan.js's computePlan(...) output, passed in fresh by the
// caller (main.js already recomputes it every frame for the checklist, so
// there's no reason to duplicate that here).
//
// DATA-DRIVEN classification: an action is "on track" iff its node id
// literally appears in the CURRENT plan's step list — no hardcoded action
// names, just membership in a Set built off the same *_DEFS-derived node
// ids the checklist itself uses. "Off track" gets pointed at whatever the
// plan's current next actionable step is (also read live, never hardcoded)
// — this is the "fine, we can still get there" re-orientation the task
// describes, generated from data rather than scripted per situation.
function actionNodeId(action) {
  if (action.kind === 'building' || action.kind === 'sell') return `building:${action.key}`;
  if (action.kind === 'category') return `category:${action.key}`;
  if (action.kind === 'import') return `resource:${action.key}`;
  return null;
}
function actionName(action) {
  if (action.kind === 'building' || action.kind === 'sell') return (BUILDING_DEFS[action.key] || {}).name || action.key;
  if (action.kind === 'category') return (PRODUCTION_DEFS[action.key] || {}).name || action.key;
  if (action.kind === 'import') return (RESOURCE_DEFS[action.key] || {}).name || action.key;
  return action.key;
}

export function reactToAction(directorState, action, plan) {
  if (!plan || !plan.steps.length) return; // no active goals: nothing to be "on/off" a plan from yet
  const name = actionName(action);
  const nodeId = actionNodeId(action);
  const stepIds = new Set(plan.steps.map(s => s.nodeId));
  if (nodeId && stepIds.has(nodeId)) {
    announce(directorState, DIRECTOR_COPY.ON_TRACK(name));
    return;
  }
  const nextStep = plan.steps.find(s => !s.satisfied);
  announce(directorState, nextStep
    ? DIRECTOR_COPY.OFF_TRACK(name, nextStep.label)
    : DIRECTOR_COPY.OFF_TRACK_NO_NEXT(name));
}

// ---------------------------------------------------------------------------
// SILLY-MODE SELF-SABOTAGE GAG CHAIN (task point 6). handleBuildingSold is
// the SINGLE entry point main.js calls on every sell/delete — it returns a
// tri-state result so main.js knows exactly what to do next without needing
// to re-derive the gag stage itself:
//   { type: 'handled' } — this module already surfaced a bubble line for
//     this sell (a serious-mode neutral ack, or the silly-mode "confused,
//     replaced" line); main.js does nothing further.
//   { type: 'dialog' }  — this sell was THE pity-building, sold a second
//     time; main.js must show the "are you five" confirm.
//   { type: 'pass' }    — nothing gag-related happened (idle stage and not
//     a key building, OR mid-chain but this sale isn't the pity-building);
//     main.js should treat it like any other action via reactToAction.
export function handleBuildingSold(directorState, world, map, economy, soldBuilding) {
  const { gag } = directorState;

  if (directorState.toneMode !== 'silly') {
    // SERIOUS MODE: the whole chain is inert (CONCEPT.md: "neutral
    // acknowledgement or silence") — a plain neutral line, never the
    // confused-replacement/dialog escalation.
    announce(directorState, DIRECTOR_COPY.SELL_NEUTRAL(soldBuilding.def.name));
    return { type: 'handled' };
  }

  if (gag.stage === 'idle' && isKeyEconomicBuilding(soldBuilding.key)) {
    // STAGE 1 -> 2: sold a key economic building. The game gets "confused"
    // and quietly replaces it for free (no IC/manpower spend, no
    // construction timer — it's already standing, same shortcut main.js's
    // own placeEstablishedBuilding uses for the game's OTHER pre-placed
    // starting industry) — sited via the same delegated-placement planner
    // every other building uses, so it degrades gracefully (a slightly
    // different nearby spot) if the exact old footprint is no longer clear.
    const site = sitePlacement(map, soldBuilding.key, soldBuilding.x, soldBuilding.y)
      || { x: soldBuilding.gx, y: soldBuilding.gy };
    const pity = spawnBuilding(world, map, soldBuilding.key, site.x, site.y, 'player');
    pity.status = 'complete';
    pity.buildProgress = 1;
    pity.hp = pity.maxHp;
    gag.stage = 'awaitingPityDelete';
    gag.pityBuildingId = pity.id;
    announce(directorState, DIRECTOR_COPY.CONFUSED_REPLACED(soldBuilding.def.name), 6000);
    return { type: 'handled' };
  }

  if (gag.stage === 'awaitingPityDelete' && soldBuilding.id === gag.pityBuildingId) {
    // STAGE 2 -> 3: they sold the pity-building too — this IS the
    // deliberate self-sabotage sequence the task describes (a single sell
    // never reaches this branch; only selling the REPLACEMENT does).
    gag.stage = 'confirmDialog';
    return { type: 'dialog' };
  }

  // Idle-and-not-key, or mid-chain but this sale isn't the pity-building
  // (a legitimate, unrelated sell while the chain happens to be waiting) —
  // doesn't advance or reset the chain; let the caller give it the normal
  // plan-relevance treatment every other action gets.
  return { type: 'pass' };
}

// Called by main.js when the player answers the "are you five" dialog.
// Returns the new terminal stage so main.js knows which screen to show.
export function resolveAreYouFive(directorState, answeredYes) {
  directorState.gag.stage = answeredYes ? 'takeover' : 'fired';
  return directorState.gag.stage;
}

// Full state reset (used by main.js's "fired -> restart" path before it
// reloads, and available for a test harness that wants to re-arm the chain
// without reloading the page).
export function resetGag(directorState) {
  directorState.gag = { stage: 'idle', pityBuildingId: null };
}
