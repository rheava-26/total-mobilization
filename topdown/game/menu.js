// ---------------------------------------------------------------------------
// MAIN-MENU SCENARIO PICKER (docs/CONCEPT.md, "Design directions raised" —
// the "Main-menu map/scenario picker" bullet: "a menu row is a scenario
// descriptor: label + gen params + difficulty ... a curated front door onto
// the generator, not a return to hand-authored-only maps"). This module is
// ONLY that front door: it shows a full-screen DOM overlay (markup + CSS
// live in index.html's #mainMenu block, same split as #guidancePanel), lets
// the player pick one SCENARIO_DEFS row, and resolves a promise with the
// chosen {seed, mapUrl, difficulty, scenarioId}. It does NOT load maps or
// touch game state itself — main.js takes that result and feeds it into the
// EXACT SAME loadGeneratedMap/loadMap calls the ?seed=/?map= deep-link path
// already uses, so there is exactly one map-boot code path, not two.
//
// Terrain variety today comes ONLY from each entry's distinct seed (a fixed
// seed per non-random entry for reproducibility, "Random" rolls a fresh one
// every pick via randomSeed() — the same generator every other boot path
// uses). FUTURE STRETCH (not built here, per task scope): per-scenario
// terrain-bias generation params (e.g. "more mountainous", "archipelago")
// once game/mapgen.js grows a bias input; today mapgen.js takes a seed only,
// so a menu row cannot yet request specific terrain character beyond what
// its seed happens to roll.

// ---------------------------------------------------------------------------
// PLACEHOLDER COPY (designer to rewrite) — parallel to main.js's own COPY
// block (guidance panel / phase-announcement text). Every string below is a
// plain, functional stand-in marked [PLACEHOLDER]; nothing here is read for
// gameplay logic — only for menu display. Find-and-replace this block (and
// the label/blurb/difficultyLabel fields in SCENARIO_DEFS just below it)
// when writing the real menu copy; the numeric fields on SCENARIO_DEFS
// (seed/difficulty/mapUrl) are NOT placeholders and drive real behavior.
export const MENU_COPY = {
  TITLE: '[PLACEHOLDER] TOTAL MOBILIZATION',
  SUBTITLE: '[PLACEHOLDER] Choose a scenario to begin',
  DIFFICULTY_PREFIX: '[PLACEHOLDER] Difficulty: ',
  START_HINT: '[PLACEHOLDER] Click a scenario to start. Deep links (?seed=/?map=) skip this menu.',
};

// ---------------------------------------------------------------------------
// SCENARIO_DEFS — data-driven menu rows. Each entry:
//   id               stable key (not shown to the player)
//   label            [PLACEHOLDER] display name
//   blurb            [PLACEHOLDER] one-line flavor text
//   difficultyLabel  [PLACEHOLDER] display string for the difficulty badge
//   seed             fixed seed (int) for a reproducible run, or `null` for
//                    "roll a fresh seed every pick" (the Random entry)
//   difficulty       numeric multiplier, 1.0 = baseline. Consumed by
//                     main.js's spawnEnemyLandingForce() to scale enemy
//                     landing-force unit counts (the PRIMARY difficulty
//                     knob per the task spec) and, as a smaller secondary
//                     nudge, the player's starting IC/manpower (see
//                     main.js's startingEcoMult). Pure data — no code
//                     change needed to add/retune a row.
//   mapUrl           optional — set to an authored map path (like the
//                     ?map= deep-link) instead of a seed, for a future
//                     entry that wants specific hand-built geography. Every
//                     entry below uses procedural generation (seed) per the
//                     concept doc's "curated front door onto the generator"
//                     framing; mapUrl is here as a documented extension
//                     point, not because any row currently needs it.
//
// Five rows spanning easy -> brutal, plus one "Random" entry, per the task's
// "~4-6 entries" guidance. Difficulty numbers are first-pass placeholders
// (same status as game/economy.js's ECONOMY_TUNABLES) — the designer's to
// retune once there's a real feel for what "3 tanks x1.75" plays like.
export const SCENARIO_DEFS = [
  { id: 'skirmish-easy', label: '[PLACEHOLDER] Coastal Skirmish', blurb: '[PLACEHOLDER] A small, forgiving landing — learn the systems.', difficultyLabel: '[PLACEHOLDER] Easy', seed: 101, difficulty: 0.5 },
  { id: 'skirmish-normal', label: '[PLACEHOLDER] Standard Theater', blurb: '[PLACEHOLDER] The baseline invasion.', difficultyLabel: '[PLACEHOLDER] Normal', seed: 202, difficulty: 1.0 },
  { id: 'skirmish-hard', label: '[PLACEHOLDER] Contested Theater', blurb: '[PLACEHOLDER] A larger, faster landing force.', difficultyLabel: '[PLACEHOLDER] Hard', seed: 303, difficulty: 1.75 },
  { id: 'skirmish-brutal', label: '[PLACEHOLDER] Total War', blurb: '[PLACEHOLDER] Everything, at once. Good luck.', difficultyLabel: '[PLACEHOLDER] Brutal', seed: 404, difficulty: 2.5 },
  { id: 'random-normal', label: '[PLACEHOLDER] Random Theater', blurb: '[PLACEHOLDER] A fresh island every time, normal difficulty.', difficultyLabel: '[PLACEHOLDER] Normal', seed: null, difficulty: 1.0 },
];

// Same generator every other boot path uses (main.js's own copy of this
// function was moved here so there is exactly one definition; main.js
// imports it back for its deep-link "no ?seed= given" fallback).
export function randomSeed() {
  return Math.floor(Math.random() * 0xFFFFFFFF) >>> 0;
}

// showMainMenu(): fills in the static #mainMenu overlay (markup lives in
// index.html, same split as #guidancePanel — CSS/structure in the HTML,
// content + behavior wired from JS) and returns a Promise that resolves
// once the player clicks a scenario row. One-shot: the overlay is removed
// from the DOM on resolve (matching the task's "menu is a front door, not a
// rewrite" — there's no in-game "back to menu" control today, a page
// reload is the way back).
export function showMainMenu() {
  return new Promise(resolve => {
    const overlay = document.getElementById('mainMenu');
    const titleEl = document.getElementById('menuTitle');
    const subtitleEl = document.getElementById('menuSubtitle');
    const listEl = document.getElementById('menuScenarioList');
    const hintEl = document.getElementById('menuHint');

    titleEl.textContent = MENU_COPY.TITLE;
    subtitleEl.textContent = MENU_COPY.SUBTITLE;
    hintEl.textContent = MENU_COPY.START_HINT;
    listEl.innerHTML = '';

    for (const scenario of SCENARIO_DEFS) {
      const row = document.createElement('button');
      row.type = 'button';
      row.className = 'menuScenarioRow';
      row.dataset.scenarioId = scenario.id;
      const label = document.createElement('span');
      label.className = 'msLabel';
      label.textContent = scenario.label;
      const diff = document.createElement('span');
      diff.className = 'msDiff';
      diff.textContent = `${MENU_COPY.DIFFICULTY_PREFIX}${scenario.difficultyLabel}`;
      const blurb = document.createElement('span');
      blurb.className = 'msBlurb';
      blurb.textContent = scenario.blurb;
      row.append(label, diff, blurb);
      row.addEventListener('click', () => {
        const seed = scenario.mapUrl ? undefined : (scenario.seed === null ? randomSeed() : scenario.seed);
        overlay.classList.remove('open');
        overlay.remove(); // one-shot — see function comment above
        resolve({
          scenarioId: scenario.id,
          seed,
          mapUrl: scenario.mapUrl || null,
          difficulty: scenario.difficulty,
        });
      });
      listEl.appendChild(row);
    }

    overlay.classList.add('open');
  });
}
