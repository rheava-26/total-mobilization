// FIRST-RUN INTRO SEQUENCE (CONCEPT.md's settled "Onboarding / tutorial
// concept (settled — the 'goal-driven, self-aware' tutorial)" paragraph,
// beats 1-2 specifically: "the only scripted stretch is the intro framing
// ... which should be light and skippable on replay"). This module is the
// FRONT HALF of the onboarding concept — the back half (goal selection, the
// reverse-planned checklist, the reactive director) already exists in
// game/tutorialplan.js and game/director.js and is untouched by this file.
// This module owns state SHAPES + pure transition logic only (same split as
// game/tutorialplan.js/game/director.js's own header comments describe) —
// it never touches the DOM or canvas 2D context state itself, EXCEPT for
// drawDiscardFlourish below, which is a pure "draw given a ctx + numbers"
// function in the same spirit as game/fog.js's drawFogOverlay and
// game/ambient.js's drawAmbientGround/drawAmbientSmoke — those own drawing
// logic without owning the canvas element itself, and this follows the same
// shape. main.js is the only place that creates the canvas/DOM, decides
// which real game systems (the tech tree view, the guidance panel) each beat
// touches, and drives the per-frame flourish loop.
//
// THE FOUR BEATS (CONCEPT.md's numbered list, condensed to a 4-state
// machine — beats 3-5 of the settled paragraph are "loose, goal-driven
// guidance" i.e. the ALREADY-EXISTING tutorialplan.js/tech-tree-click goal
// system, so this module's own state machine only needs to cover the NEW
// front half, beats 1-2 of the task's framing):
//   'welcome' — controls first (pan/zoom), nothing else demanded.
//   'graph'   — the full tech/production tree is shown (reusing the exact
//               same techTreeView the G key opens — see main.js), openly
//               acknowledging it's overwhelming.
//   'discard' — TRANSIENT, no player action inside it: the "burn away" (or
//               "dissolve"/"crumple" — same effect three ways to describe
//               it) flourish plays for DISCARD_FLOURISH_MS on the SAME
//               tech-tree canvas, then main.js auto-advances to 'handoff'.
//               This is the one deliberately memorable beat — see
//               drawDiscardFlourish below.
//   'handoff' — points the player at the REAL goal-selection system
//               (game/tutorialplan.js's goalState + game/techtreeview.js's
//               click-to-set-goal) and then, on the player's own click,
//               ends the intro into fully open play — nothing after this is
//               gated, per CONCEPT.md's "First-run / tutorial is OPEN, not
//               dictated" paragraph.
// A persistent Skip (skipIntro below) can end the sequence from ANY of the
// four states at any time — main.js keeps one Skip button visible for the
// whole time introState.active is true, regardless of beat.

// ---------------------------------------------------------------------------
// PLACEHOLDER COPY (designer to rewrite) — same convention as main.js's own
// COPY block, game/tutorialplan.js's TUTORIAL_COPY, and game/director.js's
// DIRECTOR_COPY: every player-facing string below is a plain, functional
// stand-in marked [PLACEHOLDER]. The self-aware "...is that overwhelming?
// Yeah — exactly why we're not using it, yet" beat CONCEPT.md calls for is
// folded into GRAPH_BODY below as flavor text (the designer's actual voice
// belongs there) rather than crammed onto a button label. No canon place
// names anywhere in here, per the task constraint.
export const INTRO_COPY = {
  WELCOME_TITLE: '[PLACEHOLDER] Welcome, Commander',
  WELCOME_BODY: '[PLACEHOLDER] First things first: this is your map. Drag to pan, scroll/wheel to zoom. Get a feel for it — nothing else matters yet.',
  WELCOME_CONTINUE: '[PLACEHOLDER] Got it',
  GRAPH_TITLE: '[PLACEHOLDER] The Whole Picture',
  GRAPH_BODY: '[PLACEHOLDER] This is everything the nation could ever build or research — every resource, every building, every unit, every research program, and how they all depend on each other. ...Is that overwhelming? Yeah — exactly why we\'re not using it. Not like this, anyway.',
  GRAPH_CONTINUE: '[PLACEHOLDER] Get rid of it',
  HANDOFF_TITLE: '[PLACEHOLDER] Pick Your Own Goals',
  HANDOFF_BODY: '[PLACEHOLDER] Instead: open that same tree any time with G, and click a finished product near its edge — a tank, a fighter, whatever you want rolling off the line. We\'ll work out the steps between here and there and keep a running checklist for you. Ignore it whenever you want — it\'s a suggestion, not a script.',
  HANDOFF_CONTINUE: '[PLACEHOLDER] Start the war effort',
  SKIP: '[PLACEHOLDER] Skip intro',
};

// ---------------------------------------------------------------------------
// FIRST-RUN DETECTION. Deep links (?seed=/?map=) are the dev/share path and
// already skip the main menu (see main.js's MAP SOURCE section) — the intro
// skips right alongside them, same reasoning: someone pasting a seed link
// wants to land in the game, not rewatch an intro. `?intro` force-shows it
// (dev/QA convenience, e.g. verifying a copy edit without clearing storage).
// Otherwise: a `localStorage` flag remembers "this browser has already seen
// it" so a normal player doesn't get re-nagged on every load, only the
// actual first run (or after clearing site data) — per the task's "don't
// make it re-nag every load if that's easy to avoid" ask. Wrapped in
// try/catch because localStorage can throw in some sandboxed/private-mode
// contexts; failing open (show it) is the safe default there — worst case a
// dismissable intro reappears, never a hard failure.
const INTRO_SEEN_KEY = 'tm.introSeen';

export function shouldShowIntro(urlParams, hasDeepLink) {
  if (urlParams.has('intro')) return true;
  if (hasDeepLink) return false;
  try {
    return localStorage.getItem(INTRO_SEEN_KEY) !== '1';
  } catch {
    return true;
  }
}

function markIntroSeen() {
  try { localStorage.setItem(INTRO_SEEN_KEY, '1'); } catch { /* best-effort only */ }
}

// ---------------------------------------------------------------------------
// INTRO STATE — create*State() shape, same convention as
// game/research.js's createResearchState / game/tutorialplan.js's
// createGoalState / game/director.js's createDirectorState. Owned once by
// main.js, mutated only through the transition functions below so beat
// order can never be driven out of sequence by a stray direct write.
//   active — true for the whole run of the sequence (every beat); false
//            once skipped or completed. main.js's persistent Skip button is
//            shown/hidden off this single flag.
//   beat   — one of 'welcome' | 'graph' | 'discard' | 'handoff' | null
//            (null only when !active).
//   dissolve.running/start — the 'discard' beat's flourish timer; see
//            beginDiscard/discardProgress/finishDiscard below.
export function createIntroState() {
  return { active: false, beat: null, dissolve: { running: false, start: 0 } };
}

export function startIntro(introState) {
  introState.active = true;
  introState.beat = 'welcome';
}

// Ends the sequence from ANY beat — the task's hard "skippable at any point"
// requirement. Also called internally by completeIntro (the handoff beat's
// own natural end) so there is exactly ONE way the sequence ever actually
// stops, whether the player clicked through every beat or bailed on beat 1.
export function skipIntro(introState) {
  introState.active = false;
  introState.beat = null;
  introState.dissolve.running = false;
  markIntroSeen();
}

export function beginGraphBeat(introState) { introState.beat = 'graph'; }

export const DISCARD_FLOURISH_MS = 1100;

export function beginDiscard(introState) {
  introState.beat = 'discard';
  introState.dissolve.running = true;
  introState.dissolve.start = performance.now();
}

// 0..1 over DISCARD_FLOURISH_MS; 0 if the flourish isn't currently running
// (main.js only calls this while it is, but a defensive default keeps this
// safe to call unconditionally too).
export function discardProgress(introState, now = performance.now()) {
  if (!introState.dissolve.running) return 0;
  return Math.min(1, (now - introState.dissolve.start) / DISCARD_FLOURISH_MS);
}

export function finishDiscard(introState) {
  introState.dissolve.running = false;
}

export function beginHandoff(introState) { introState.beat = 'handoff'; }

// The handoff beat's own natural end — same terminal effect as skipIntro
// (active=false, marks seen) but kept as a separate name at the call site in
// main.js so "the player finished it" and "the player skipped it" read as
// distinct intents in the caller, even though they converge here.
export function completeIntro(introState) { skipIntro(introState); }

// ---------------------------------------------------------------------------
// THE "DISCARD THE GRAPH" FLOURISH — a cheap, canvas-native "burn away" wipe,
// NOT a particle engine (per the task's explicit cost constraint): one
// jagged erase path (destination-out — permanently punches transparent
// holes in whatever's already drawn on the canvas) sweeping left-to-right,
// with a soft ember-colored gradient (source-atop — only tints pixels that
// are still opaque) riding just ahead of the erased edge. Two fills per
// frame, no per-pixel manipulation, no offscreen buffers.
//
// A deliberate side effect this module leans on rather than fights: main.js
// stops calling the real techTreeView.render() while this plays (see
// main.js's loop()), so this draws on top of whatever the tree's LAST real
// frame left on the canvas, and erasing never redraws a fresh backdrop first
// — meaning as the graph burns away, the canvas turns progressively
// TRANSPARENT, and the real game map (rendered every frame on the canvas
// BELOW this one, unconditionally, per main.js's loop()) shows through in
// its place. That reads as exactly the beat's point made visually literal —
// "we don't need to hold this whole graph in view; the real game underneath
// it is still running" — for free, with no extra drawing of our own.
//
// Jaggedness is a small deterministic sine mix (not real per-pixel noise) —
// cheap, and stable frame-to-frame modulo the `progress` term already fed
// in, so the burn edge visibly flickers/crawls rather than reading as a
// perfectly straight wipe, without any random-number bookkeeping to manage.
export function drawDiscardFlourish(ctx, w, h, progress) {
  const teeth = 26;
  const frontX = -60 + progress * (w + 120);

  ctx.save();
  ctx.globalCompositeOperation = 'destination-out';
  ctx.fillStyle = '#000';
  ctx.beginPath();
  ctx.moveTo(0, 0);
  ctx.lineTo(frontX, 0);
  for (let i = 0; i <= teeth; i++) {
    const t = i / teeth;
    const jag = Math.sin(t * 37.1 + progress * 9.7) * 22 + Math.sin(t * 11.3 - progress * 4.1) * 10;
    ctx.lineTo(frontX + jag, t * h);
  }
  ctx.lineTo(0, h);
  ctx.closePath();
  ctx.fill();
  ctx.restore();

  ctx.save();
  ctx.globalCompositeOperation = 'source-atop'; // only tints pixels still standing — never paints into already-erased (transparent) area
  const grad = ctx.createLinearGradient(frontX - 55, 0, frontX + 55, 0);
  grad.addColorStop(0, 'rgba(255,140,40,0)');
  grad.addColorStop(0.55, 'rgba(255,150,60,0.85)');
  grad.addColorStop(1, 'rgba(255,225,150,0)');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, w, h);
  ctx.restore();
}
