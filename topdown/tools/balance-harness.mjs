#!/usr/bin/env node
// ---------------------------------------------------------------------------
// HEADLESS SELF-PLAY BALANCE HARNESS (Playability v1 tuning pass — see
// docs/CONCEPT.md's "Playability v1" section: "the drama lives in
// PREPARATION"). A DEV TOOL, not shipped game code — it never runs inside
// the actual game, has no build step, and adds no runtime dependency (it
// drives the real browser build through window.__debug, the same headless
// hooks every other pass in this codebase already exposes for testing).
//
// WHAT THIS PROVES: it drives full simulated playthroughs against the real
// game code (not a re-implementation of it) with a small set of REFERENCE
// STRATEGIES, across every SCENARIO_DEFS difficulty and several map seeds
// each, and records the outcome — so a change to CAPTURE_TIME_S, the enemy
// AI's group tunables, ENEMY_LANDING_BASE, or the production/economy numbers
// can be checked against real playthroughs instead of guesswork, and so the
// balance doesn't get overfit to one lucky seed.
//
// HOW IT MOVES FASTER THAN REAL PLAY: two different techniques for two
// different phases, both driving the SAME code path the real per-frame loop
// (main.js's loop()) uses —
//   PREP   — window.__debug.fastForward(seconds, stepDt) calls
//            updateBuildings/updateEconomy/updateResearch/updateProduction
//            directly in fixed dt steps with NO rendering and NO per-frame
//            unit/AI simulation (there's nothing for units to do in PREP
//            anyway — no enemy exists yet). This is not an approximation:
//            main.js's OWN window.__debug.fastForward is the literal
//            function; a real prep phase produces byte-identical building/
//            economy state for the same sequence of build orders. Effectively
//            instant (tested: 600 simulated seconds in ~25ms).
//   COMBAT — needs the real per-frame loop (units/AI/objectives all only
//            update inside main.js's loop(), which only runs off
//            requestAnimationFrame). Playwright's page.clock (fake timers +
//            a faked requestAnimationFrame) lets us advance that loop through
//            many simulated frames far faster than real wall-clock 60fps —
//            still real simulation, just not real-time-paced.
//
// TWO REFERENCE STRATEGIES (per the task spec) plus a third calibration rung:
//   unprepared — begins combat immediately with only the starting garrison.
//   lightPrep  — a short, partial prep window (see LIGHT_PREP_S below) —
//                probes "how much prep is enough" at each difficulty rather
//                than just "prepared vs not."
//   prepared   — a full prep window (PREP_S) driving the ACTUAL production
//                chain (build mines + factories through the same
//                sitePlacement/canAffordBuilding/spendForBuilding/
//                spawnBuilding path main.js's B-mode click handler uses —
//                see orderBuilding below) rather than a magic unit spawn, so
//                this exercises the real economy/production/mobilization
//                systems end to end, not just the objectives/enemyai combat
//                layer.
//
// USAGE:
//   /opt/node22/bin/node topdown/tools/balance-harness.mjs [--quick]
// Env overrides: PLAYWRIGHT_MODULE, CHROMIUM_PATH, PORT, CONCURRENCY.
// --quick runs one seed per difficulty (fast smoke check, not a balance
// verdict) instead of the full multi-seed matrix — see SEED sets below.

import http from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const TOPDOWN_ROOT = path.join(__dirname, '..');

const PLAYWRIGHT_MODULE = process.env.PLAYWRIGHT_MODULE || '/opt/node22/lib/node_modules/playwright/index.mjs';
const CHROMIUM_PATH = process.env.CHROMIUM_PATH || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const PORT = Number(process.env.PORT) || 8791;
const CONCURRENCY = Number(process.env.CONCURRENCY) || 4;
const QUICK = process.argv.includes('--quick');

// ---------------------------------------------------------------------------
// TUNABLES OF THE HARNESS ITSELF (not the game) — designer/balance-agent's to
// adjust if the target "reasonable prep window" changes.
const LIGHT_PREP_S = 150; // ~2.5 min — a player who did SOMETHING but not much
const PREP_S = 420; // 7 min — a diligent, but not maximal/power-gaming, prep window
const CHAIN_TEST_PREP_S = 1500; // 25 min — generous ceiling for the hypersonics-reachability check specifically (Mode B), not the standard balance matrix
const COMBAT_CAP_S = 300; // 5 min simulated combat — if neither side has resolved it by then, call it 'undecided' rather than burn more wall-clock chasing a grind
const COMBAT_CHUNK_S = 30; // check outcome every this-many simulated seconds, so a fast resolution doesn't pay for the full cap

// SEEDS: several per difficulty so tuning isn't fit to one lucky island.
// Reuses each SCENARIO_DEFS row's own seed (game/menu.js) as the primary
// seed (so results are directly comparable to what a player picking that
// exact menu row would get) plus two extra arbitrary seeds per difficulty.
const DIFFICULTIES = [
  { label: 'easy', difficulty: 0.5, seeds: [101, 5101, 9101] },
  { label: 'normal', difficulty: 1.0, seeds: [202, 5202, 9202] },
  { label: 'hard', difficulty: 1.75, seeds: [303, 5303, 9303] },
  { label: 'brutal', difficulty: 2.5, seeds: [404, 5404, 9404] },
];
const ACTIVE_DIFFICULTIES = QUICK
  ? DIFFICULTIES.map(d => ({ ...d, seeds: d.seeds.slice(0, 1) }))
  : DIFFICULTIES;

// ---------------------------------------------------------------------------
// STATIC FILE SERVER — serves topdown/ so the game's real ES modules/JSON
// load exactly like a normal dev server would (file:// can't satisfy the
// module graph's relative fetches). No new dependency: Node's built-in http.
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json', '.css': 'text/css' };
function startServer() {
  return new Promise((resolve) => {
    const server = http.createServer((req, res) => {
      let p = decodeURIComponent(req.url.split('?')[0]);
      if (p === '/') p = '/index.html';
      const full = path.join(TOPDOWN_ROOT, p);
      if (!full.startsWith(TOPDOWN_ROOT)) { res.writeHead(403); res.end(); return; }
      fs.readFile(full, (err, data) => {
        if (err) { res.writeHead(404); res.end('not found'); return; }
        res.writeHead(200, { 'Content-Type': MIME[path.extname(full)] || 'application/octet-stream' });
        res.end(data);
      });
    });
    server.listen(PORT, '127.0.0.1', () => resolve(server));
  });
}

// ---------------------------------------------------------------------------
// PREP DRIVER (runs IN-PAGE via page.evaluate — see PREPARED STRATEGY above).
// A fixed, generic build-PRIORITY list, front-loaded with cheap
// economy-enabling buildings (supply depot/barracks/mines) before the
// pricier production facilities, so IC isn't wasted overflowing a low cap
// and doesn't stall on affording a big-ticket item before laying groundwork
// — modeling roughly what a competent player's own build order would look
// like, not a special-cased script (it's driven purely by
// canAffordBuilding/sitePlacement, the same generic checks every other
// caller uses; no per-difficulty/per-seed branching here). Runs the SAME
// build+production systems main.js's real B-mode + game/production.js run —
// this is not a re-implementation, it's the actual game code driven
// headlessly through window.__debug.
function prepDriverSource() {
  return async function runPrepDriver(prepBudgetS) {
    const dbg = window.__debug;
    const { map, world, economy } = dbg;
    const capital = map.cities[0];
    const capWorld = { x: (capital.x + 0.5) * map.tileSize, y: (capital.y + 0.5) * map.tileSize };
    function depositWorldPos(type) {
      const dep = (map.deposits || []).find(d => d.type === type);
      if (!dep) return null;
      return { x: (dep.gx + 0.5) * map.tileSize, y: (dep.gy + 0.5) * map.tileSize };
    }
    // Mirrors main.js's canvas 'click' B-mode handler exactly: sitePlacement
    // -> canAffordBuilding -> spendForBuilding -> spawnBuilding. Same afford
    // check, same siting planner, same construction/econ pipeline a real
    // click goes through — nothing here bypasses the economy.
    function tryBuild(key, anchor) {
      const site = dbg.sitePlacement(map, key, anchor.x, anchor.y);
      if (!site) return false;
      if (!dbg.canAffordBuilding(economy, key)) return false;
      dbg.spendForBuilding(economy, key);
      dbg.spawnBuilding(world, map, key, site.x, site.y, 'player');
      return true;
    }
    const orders = [
      { id: 'supplyDepot1', key: 'supplyDepot', anchor: () => capWorld },
      { id: 'barracks1', key: 'barracks', anchor: () => capWorld },
      { id: 'mineSteel1', key: 'mine', anchor: () => depositWorldPos('steel') },
      { id: 'mineChromium1', key: 'mine', anchor: () => depositWorldPos('chromium') },
      { id: 'mineTungsten1', key: 'mine', anchor: () => depositWorldPos('tungsten') },
      { id: 'mineOil1', key: 'mine', anchor: () => depositWorldPos('oil') },
      { id: 'ammoPlant1', key: 'ammunitionPlant', anchor: () => capWorld },
      { id: 'tankFactory1', key: 'tankFactory', anchor: () => capWorld },
      { id: 'supplyDepot2', key: 'supplyDepot', anchor: () => capWorld },
      { id: 'artilleryWorks1', key: 'artilleryWorks', anchor: () => capWorld },
      { id: 'researchBureau1', key: 'researchBureau', anchor: () => capWorld },
      { id: 'mineSteel2', key: 'mine', anchor: () => depositWorldPos('steel') },
      { id: 'mineChromium2', key: 'mine', anchor: () => depositWorldPos('chromium') },
      { id: 'aircraftPlant1', key: 'aircraftPlant', anchor: () => capWorld },
      { id: 'mineAluminum1', key: 'mine', anchor: () => depositWorldPos('aluminum') },
      { id: 'mineTitanium1', key: 'mine', anchor: () => depositWorldPos('titanium') },
      { id: 'mineOil2', key: 'mine', anchor: () => depositWorldPos('oil') },
    ];
    const done = new Set();
    let t = 0;
    const STEP = 5;
    while (t < prepBudgetS) {
      for (const o of orders) {
        if (done.has(o.id)) continue;
        const anchor = o.anchor();
        if (!anchor) continue; // this map/seed rolled no deposit of that type — skip permanently (a real player would import instead; not modeled here)
        if (tryBuild(o.key, anchor)) done.add(o.id);
        else break; // couldn't afford this priority item yet — stop for this tick rather than letting a cheaper later item cut the queue, so IC accumulates toward the front of the priority list like a real build-order-following player would
      }
      dbg.fastForward(STEP, 1);
      t += STEP;
    }
    const counts = {};
    for (const u of world.units) if (u.side === 'player') counts[u.key] = (counts[u.key] || 0) + 1;
    return {
      builtCount: done.size, ordersTotal: orders.length,
      unitCounts: counts,
      ic: economy.ic, manpower: economy.manpower, mobilization: economy.mobilizationLevel,
    };
  };
}

// ---------------------------------------------------------------------------
// CHAIN DRIVER (Mode B — hypersonics reachability). Same shape as the prep
// driver above, but its wishlist is specifically every building the
// hypersonics chain needs (game/production.js PRODUCTION_DEFS.hypersonics +
// game/research.js TECH_DEFS.missileTech/hypersonicTech), plus starts
// research the instant each tech is 'researchable' per game/research.js's
// own techStatus() query (never hand-computed here). Returns WHEN (if ever)
// a hypersonicLauncher unit actually gets produced.
function chainDriverSource() {
  return async function runChainDriver(prepBudgetS) {
    const dbg = window.__debug;
    const { map, world, economy, researchState } = dbg;
    const capital = map.cities[0];
    const capWorld = { x: (capital.x + 0.5) * map.tileSize, y: (capital.y + 0.5) * map.tileSize };
    function depositWorldPos(type) {
      const dep = (map.deposits || []).find(d => d.type === type);
      if (!dep) return null;
      return { x: (dep.gx + 0.5) * map.tileSize, y: (dep.gy + 0.5) * map.tileSize };
    }
    function tryBuild(key, anchor) {
      const site = dbg.sitePlacement(map, key, anchor.x, anchor.y);
      if (!site) return false;
      if (!dbg.canAffordBuilding(economy, key)) return false;
      dbg.spendForBuilding(economy, key);
      dbg.spawnBuilding(world, map, key, site.x, site.y, 'player');
      return true;
    }
    const orders = [
      { id: 'supplyDepot1', key: 'supplyDepot', anchor: () => capWorld },
      { id: 'supplyDepot2', key: 'supplyDepot', anchor: () => capWorld },
      { id: 'supplyDepot3', key: 'supplyDepot', anchor: () => capWorld },
      { id: 'barracks1', key: 'barracks', anchor: () => capWorld },
      { id: 'mineSteel1', key: 'mine', anchor: () => depositWorldPos('steel') },
      { id: 'mineChromium1', key: 'mine', anchor: () => depositWorldPos('chromium') },
      { id: 'mineTitanium1', key: 'mine', anchor: () => depositWorldPos('titanium') },
      { id: 'mineTitanium2', key: 'mine', anchor: () => depositWorldPos('titanium') },
      { id: 'mineRareEarths1', key: 'mine', anchor: () => depositWorldPos('rareEarths') },
      { id: 'mineRareEarths2', key: 'mine', anchor: () => depositWorldPos('rareEarths') },
      { id: 'mineOil1', key: 'mine', anchor: () => depositWorldPos('oil') },
      { id: 'researchBureau1', key: 'researchBureau', anchor: () => capWorld },
      { id: 'ammoPlant1', key: 'ammunitionPlant', anchor: () => capWorld },
      { id: 'electronicsPlant1', key: 'electronicsPlant', anchor: () => capWorld },
      { id: 'alloyForge1', key: 'alloyForge', anchor: () => capWorld },
      { id: 'missileWorks1', key: 'missileWorks', anchor: () => capWorld },
      { id: 'hypersonicWorks1', key: 'hypersonicWorks', anchor: () => capWorld },
      { id: 'mineSteel2', key: 'mine', anchor: () => depositWorldPos('steel') },
      { id: 'mineRareEarths3', key: 'mine', anchor: () => depositWorldPos('rareEarths') },
    ];
    const done = new Set();
    const researchStarted = new Set();
    const log = [];
    let t = 0;
    const STEP = 5;
    let hypersonicAtS = null;
    while (t < prepBudgetS) {
      for (const o of orders) {
        if (done.has(o.id)) continue;
        const anchor = o.anchor();
        if (!anchor) continue;
        if (tryBuild(o.key, anchor)) { done.add(o.id); log.push(`t=${t}s built ${o.key}`); }
        else break;
      }
      for (const techId of ['missileTech', 'hypersonicTech']) {
        if (researchStarted.has(techId)) continue;
        const status = dbg.techStatus(world, researchState, techId);
        if (status.state === 'researchable' && dbg.canAffordResearch(economy, techId)) {
          if (dbg.startResearch(world, economy, researchState, techId)) {
            researchStarted.add(techId);
            log.push(`t=${t}s started research ${techId}`);
          }
        }
      }
      dbg.fastForward(STEP, 1);
      t += STEP;
      const hasLauncher = world.units.some(u => u.side === 'player' && u.key === 'hypersonicLauncher');
      if (hasLauncher && hypersonicAtS === null) hypersonicAtS = t;
      if (hasLauncher) break; // goal reached — stop early, no need to keep simulating
    }
    return {
      hypersonicAtS, log,
      techsCompleted: Object.keys(researchState.completed),
      builtCount: done.size, ordersTotal: orders.length,
      ic: economy.ic, elapsedS: t,
    };
  };
}

// ---------------------------------------------------------------------------
// ONE RUN — navigate a fresh page at {seed, difficulty}, run the chosen
// strategy's PREP (skipped for 'unprepared'), begin combat, and drive the
// real per-frame loop forward via page.clock until an outcome fires or
// COMBAT_CAP_S is reached.
async function runOne(chromium, { label, difficulty, seed, strategy }) {
  const browser = await chromium; // shared browser instance, one page per run
  const page = await browser.newPage();
  const pageErrors = [];
  page.on('pageerror', e => pageErrors.push(e.message));
  try {
    await page.goto(`http://127.0.0.1:${PORT}/index.html?seed=${seed}&difficulty=${difficulty}`);
    await page.waitForFunction(() => !!window.__debug, { timeout: 20000 });

    let prepInfo = null;
    if (strategy !== 'unprepared') {
      const budget = strategy === 'lightPrep' ? LIGHT_PREP_S : PREP_S;
      prepInfo = await page.evaluate(`(${prepDriverSource().toString()})(${budget})`);
    }

    // Combat needs the real per-frame loop -> install the fake clock only
    // NOW (after navigation/prep, which both run in real time but are fast —
    // installing earlier would make page.waitForFunction's default
    // rAF-based polling hang, since the fake clock only advances when WE
    // call runFor/fastForward, never on its own).
    await page.clock.install({ time: 0 });
    await page.evaluate(() => window.__debug.phase.beginCombat());

    let outcome = null;
    let resolvedAtS = null;
    let t = 0;
    while (t < COMBAT_CAP_S && !outcome) {
      await page.clock.runFor(COMBAT_CHUNK_S * 1000);
      t += COMBAT_CHUNK_S;
      outcome = await page.evaluate(() => window.__debug.objectives.outcome);
      if (outcome) resolvedAtS = t;
    }
    const finalStats = await page.evaluate(() => {
      const dbg = window.__debug;
      const cities = dbg.map.cities || [];
      const held = cities.filter(c => c.owner === 'player').length;
      const capital = cities[0];
      let enemyAlive = 0, playerAlive = 0;
      for (const u of dbg.world.units) {
        if (u.hp <= 0) continue;
        if (u.side === 'enemy') enemyAlive++; else playerAlive++;
      }
      return {
        citiesHeld: held, citiesTotal: cities.length,
        capitalHeld: capital ? capital.owner === 'player' : null,
        enemyAlive, playerAlive,
      };
    });

    return {
      label, difficulty, seed, strategy,
      outcome: outcome || 'undecided',
      resolvedAtS,
      prepInfo, finalStats,
      pageErrors,
    };
  } finally {
    await page.close();
  }
}

async function runChainRun(chromium, { seed = 202, difficulty = 1.0, prepBudgetS = CHAIN_TEST_PREP_S } = {}) {
  const browser = await chromium;
  const page = await browser.newPage();
  const pageErrors = [];
  page.on('pageerror', e => pageErrors.push(e.message));
  try {
    await page.goto(`http://127.0.0.1:${PORT}/index.html?seed=${seed}&difficulty=${difficulty}`);
    await page.waitForFunction(() => !!window.__debug, { timeout: 20000 });
    const result = await page.evaluate(`(${chainDriverSource().toString()})(${prepBudgetS})`);
    return { seed, difficulty, prepBudgetS, ...result, pageErrors };
  } finally {
    await page.close();
  }
}

// ---------------------------------------------------------------------------
// A tiny concurrency-limited map so runs overlap (several chromium pages at
// once) without unbounded parallelism blowing up memory/CPU.
async function mapLimit(items, limit, fn) {
  const results = new Array(items.length);
  let i = 0;
  async function worker() {
    while (i < items.length) {
      const idx = i++;
      results[idx] = await fn(items[idx], idx);
    }
  }
  await Promise.all(Array.from({ length: Math.min(limit, items.length) }, worker));
  return results;
}

async function main() {
  const { chromium } = await import(PLAYWRIGHT_MODULE);
  console.log(`[harness] starting static server on 127.0.0.1:${PORT} (serving ${TOPDOWN_ROOT})`);
  const server = await startServer();
  const browser = await chromium.launch({ executablePath: CHROMIUM_PATH });
  try {
    const jobs = [];
    for (const d of ACTIVE_DIFFICULTIES) {
      for (const seed of d.seeds) {
        for (const strategy of ['unprepared', 'lightPrep', 'prepared']) {
          jobs.push({ label: d.label, difficulty: d.difficulty, seed, strategy });
        }
      }
    }
    console.log(`[harness] running ${jobs.length} playthroughs (concurrency ${CONCURRENCY})...`);
    let done = 0;
    const results = await mapLimit(jobs, CONCURRENCY, async (job) => {
      const r = await runOne(browser, job);
      done++;
      console.log(`[harness] (${done}/${jobs.length}) ${job.label} seed=${job.seed} ${job.strategy} -> ${r.outcome}`
        + (r.resolvedAtS ? ` @${r.resolvedAtS}s` : '') + (r.pageErrors.length ? ` [${r.pageErrors.length} pageerror(s)]` : ''));
      return r;
    });

    console.log('\n[harness] running hypersonics-reachability chain test (Mode B)...');
    const chain = await runChainRun(browser, {});
    console.log(`[harness] hypersonics reachable at t=${chain.hypersonicAtS}s`
      + ` (techs completed: ${chain.techsCompleted.join(', ') || 'none'}, elapsed=${chain.elapsedS}s)`
      + (chain.pageErrors.length ? ` [${chain.pageErrors.length} pageerror(s)]` : ''));

    // -----------------------------------------------------------------
    // RESULTS TABLE — strategy x difficulty -> win rate, grouped for the
    // report. Printed as both a compact matrix and the raw per-seed rows.
    console.log('\n=== RESULTS MATRIX (win rate = victory OR capital held at cap) ===');
    const header = ['difficulty', 'strategy', 'runs', 'victory', 'defeat', 'undecided', 'capitalHeldRate'];
    console.log(header.join('\t'));
    for (const d of ACTIVE_DIFFICULTIES) {
      for (const strategy of ['unprepared', 'lightPrep', 'prepared']) {
        const rows = results.filter(r => r.label === d.label && r.strategy === strategy);
        const victory = rows.filter(r => r.outcome === 'victory').length;
        const defeat = rows.filter(r => r.outcome === 'defeat').length;
        const undecided = rows.filter(r => r.outcome === 'undecided').length;
        const capHeld = rows.filter(r => r.finalStats.capitalHeld).length;
        console.log([d.label, strategy, rows.length, victory, defeat, undecided, `${capHeld}/${rows.length}`].join('\t'));
      }
    }

    console.log('\n=== RAW PER-RUN RESULTS ===');
    for (const r of results) {
      console.log(`${r.label}\tseed=${r.seed}\t${r.strategy}\t${r.outcome}\tresolvedAt=${r.resolvedAtS}s\t`
        + `capitalHeld=${r.finalStats.capitalHeld}\tcitiesHeld=${r.finalStats.citiesHeld}/${r.finalStats.citiesTotal}\t`
        + `enemyAlive=${r.finalStats.enemyAlive}\tplayerAlive=${r.finalStats.playerAlive}\t`
        + (r.prepInfo ? `builtCount=${r.prepInfo.builtCount}/${r.prepInfo.ordersTotal}\tunits=${JSON.stringify(r.prepInfo.unitCounts)}` : '(no prep)'));
    }

    const anyErrors = results.some(r => r.pageErrors.length) || chain.pageErrors.length;
    if (anyErrors) {
      console.log('\n[harness] WARNING: page console errors observed during at least one run — see per-run pageErrors above.');
    }

    // Machine-readable dump alongside the human log, for anyone re-analyzing
    // a past run without re-simulating.
    const outPath = path.join(TOPDOWN_ROOT, 'tools', 'balance-harness-results.json');
    fs.writeFileSync(outPath, JSON.stringify({ results, chain }, null, 2));
    console.log(`\n[harness] wrote ${outPath}`);
  } finally {
    await browser.close();
    server.close();
  }
}

main().catch(e => { console.error(e); process.exit(1); });
