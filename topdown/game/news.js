// IN-GAME NEWS SYSTEM — "a nation at war should have a VOICE reporting on
// itself" (playtest feedback synthesized in docs/reference-city-and-
// mobilization.md §5: prep shouldn't be silent, and the invasion shouldn't
// arrive unannounced). This is the ALIVENESS + TELEGRAPHING layer: a rolling
// wartime-bulletin feed generated from live game state, styled after real
// WWII-era radio/press bulletins per that reference doc.
//
// PURELY NARRATIVE, READ-ONLY, like game/ambient.js and game/supply.js
// before it: this file reads world/economy/map and never writes to any of
// them. Delete this file and every import of it and the game plays
// byte-identical — the war just goes quiet. No balance impact, no gameplay-
// affecting randomness (template SELECTION rotates off a small local seeded
// hash, same shape as ambient.js's own hash2, purely for headline variety).
//
// Two kinds of item, both landing in the same rolling `news.items` buffer
// (newest first):
//   EVENT headlines — fired the instant a real state transition is detected
//     by diffing this frame's state against module-local scratch kept ON the
//     news state object (mobilization band crossed, combat begins/"enemy
//     landing", a city captured/recaptured, a battalion raised, a production
//     milestone crossed). Bypass the ambient pacing timer — these post
//     immediately, same as the director's own event-driven bubble.
//   AMBIENT headlines — periodic "the home front carries on" bulletins,
//     picked from docs/reference-city-and-mobilization.md §5's phase-bucketed
//     template pool (adapted/expanded here, ~60 templates total across every
//     bucket) weighted by the CURRENT mobilization band + threat level, so
//     early-game reads peacetime-ish ("rationing begins", "training camp
//     opened") and late-game reads total-war ("final offensive", "enemy
//     homeland bombed"). The ambient CADENCE itself shortens as mobilization/
//     threat climbs (see AMBIENT_INTERVAL_* below) — the war gets LOUDER, not
//     just grimmer, as it escalates.
//
// Every template is filled from REAL state (game/mapgen.js city/island names,
// economy pools, building names, a couple of small cosmetic word pools for
// flavor nouns a mobilization sim has no state for — a training camp's name,
// a propaganda claim's exact wording — same "flavor constant" spirit
// game/battalions.js's BATTALION_TEMPLATES names already use).

import { MOBILIZATION_BANDS } from './economy.js';
import { RESOURCE_LIST } from './resources.js';

const MAX_ITEMS = 40;        // rolling buffer cap — plenty for a scrolling ticker + an expanded recent-list
const VISIBLE_RECENT = 12;   // how many the UI accessor hands back for the expanded panel

// ---------------------------------------------------------------------------
// SEEDED VARIETY — a tiny local hash (mirrors game/ambient.js's own hash2
// exactly, kept as a local copy for the same "zero cross-module dependency"
// reason that file gives) driving template pick/number-flavor rolls off a
// monotonically-advancing internal counter. Deterministic given the same
// sequence of calls (same spirit as the rest of the codebase's "seeded/
// rotating, not chaotic" RNG usage) without needing a map seed threaded
// through initNews()'s zero-arg signature.
function hash2(x, y, seed) {
  let h = (x * 374761393 + y * 668265263 + seed * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h = (h ^ (h >>> 16)) >>> 0;
  return h / 4294967296;
}
function nextRoll(news) {
  news._rollCounter = (news._rollCounter | 0) + 1;
  return hash2(news._rollCounter, news._rollCounter * 7 + 3, 0x9e3779b1);
}
function pick(news, arr) {
  if (!arr || !arr.length) return undefined;
  return arr[Math.floor(nextRoll(news) * arr.length) % arr.length];
}
function rollInt(news, min, max) {
  return Math.round(min + nextRoll(news) * (max - min));
}

// ---------------------------------------------------------------------------
// COSMETIC FLAVOR POOLS — small word lists for nouns the sim has no state
// for at all (a training camp's proper name, the exact wording of a
// propaganda claim). Picking among these is exactly the "template SELECTION
// variety" the task calls out as fine to randomize; nothing here reads back
// into gameplay.
const CAMP_NAMES = ['Camp Reynolds', 'Fort Arden', 'Camp Liberty', 'Fort Halden', 'Camp Ashworth', 'Fort Mercer'];
const VEHICLE_WORDS = ['tanks', 'trucks', 'field guns', 'rifles', 'landing craft', 'aircraft', 'artillery pieces', 'armored cars'];
const SPECIALTY_WORDS = ['pilots', 'engineers', 'signalmen', 'medics', 'gunners', 'radio operators'];
const BONUS_WORDS = ['a signing bonus', 'travel pay', 'priority housing', 'an extra ration book'];
const SACRIFICE_WORDS = ['scrap metal', 'kitchen fat', 'their war bonds', 'a day\'s wages', 'spare rubber'];
const FALSE_CLAIM_WORDS = ['a decisive enemy rout', 'the enemy fleet scattered and burning', 'total collapse of enemy resistance', 'the enemy suing for terms'];
const SUBSTITUTE_WORDS = ['synthetic rubber', 'low-grade alloy steel', 'reclaimed aluminum', 'wood-fiber composite'];
const INNOVATION_WORDS = ['an improved gun sight', 'a lighter radio set', 'a faster-curing armor weld', 'an upgraded field telephone'];
const PROFITEER_COUNTS = [3, 5, 7, 11, 14];

// Wartime datelines — which "desk" a bulletin reads as filed from. Rotated
// per item so the ticker doesn't read as one monotone voice.
const SOURCES_HOME = ['NATIONAL BULLETIN', 'HOME FRONT WIRE', 'MINISTRY OF SUPPLY', 'WAR PRODUCTION BOARD'];
const SOURCES_GOV = ['WAR DEPARTMENT', 'OFFICE OF MOBILIZATION', 'NATIONAL BULLETIN'];
const SOURCES_FIELD = ['FIELD DISPATCH', 'FRONT WIRE', 'ADMIRALTY BULLETIN'];
const SOURCES_RADIO = ['RADIO BULLETIN', 'NATIONAL BULLETIN'];

// ---------------------------------------------------------------------------
// CONTEXT — one fresh fill-in-the-blanks bag built per headline, off REAL
// state (map/world/economy) plus the small cosmetic pools above. Every
// template function below takes this and returns a plain string.
function regionName(map, city) {
  if (!city) return map && map.name ? map.name : 'the coast';
  const w = map.width || 1, h = map.height || 1;
  const nx = city.x / w, ny = city.y / h; // 0..1 across the island
  const ns = ny < 0.4 ? 'north' : ny > 0.6 ? 'south' : '';
  const ew = nx < 0.4 ? 'west' : nx > 0.6 ? 'east' : '';
  const dir = ns && ew ? `${ns}${ew}ern` : ns ? `${ns}ern` : ew ? `${ew}ern` : 'central';
  return `the ${dir} coast`;
}

function pickCity(news, map, ownerFilter) {
  const cities = (map && map.cities) || [];
  const pool = ownerFilter ? cities.filter(c => c.owner === ownerFilter) : cities;
  return pick(news, pool.length ? pool : cities) || null;
}

function pickPlantName(news, world) {
  const facs = (world.buildings || []).filter(b => b.side === 'player' && b.status === 'complete' && b.def && b.def.name);
  const b = pick(news, facs);
  return b ? b.def.name : pick(news, ['the Municipal Arsenal', 'the Assembly Works', 'the shipyard', 'the ordnance works']);
}

function buildCtx(news, world, economy, map) {
  const capital = (map.cities || [])[0];
  const playerCity = pickCity(news, map, 'player') || capital;
  const enemyCity = pickCity(news, map, 'enemy'); // may be null if none held yet
  const frontCity = enemyCity || playerCity;
  const mobPct = Math.round(economy.mobilizationLevel || 0);
  const weekNum = 1 + Math.floor((news.elapsed || 0) / 45); // ~45s of playtime per "week" of bulletin dateline flavor
  return {
    city: (playerCity && playerCity.name) || 'the capital',
    capital: (capital && capital.name) || 'the capital',
    frontCity: (frontCity && frontCity.name) || 'the front',
    region: regionName(map, frontCity || playerCity),
    island: map.name || 'the homeland',
    plant: pickPlantName(news, world),
    vehicleType: pick(news, VEHICLE_WORDS),
    specialty: pick(news, SPECIALTY_WORDS),
    bonus: pick(news, BONUS_WORDS),
    sacrifice: pick(news, SACRIFICE_WORDS),
    falseClaim: pick(news, FALSE_CLAIM_WORDS),
    substitute: pick(news, SUBSTITUTE_WORDS),
    innovation: pick(news, INNOVATION_WORDS),
    camp: pick(news, CAMP_NAMES),
    commodity: pick(news, RESOURCE_LIST).name || 'rubber',
    date: `Week ${weekNum}`,
    jobs: rollInt(news, 6, 24) * 100,
    recruits: Math.max(200, Math.round((playerCity && playerCity.r || 6) * 90 * (0.7 + nextRoll(news) * 0.8))),
    percent: Math.min(95, Math.max(4, Math.round(8 + mobPct * 0.55 + rollInt(news, -8, 12)))),
    number: Math.max(50, Math.round((economy.militaryOutput || 0) * (0.4 + nextRoll(news) * 0.6)) || rollInt(news, 200, 2000)),
    profiteers: pick(news, PROFITEER_COUNTS),
    age: Math.max(16, 45 - Math.round(mobPct * 0.22)),
    mobPct,
    icRate: Math.max(1, Math.round(economy.icRate || 1)),
  };
}

// ---------------------------------------------------------------------------
// AMBIENT TEMPLATE POOL — adapted from docs/reference-city-and-mobilization.md
// §5's ~60 numbered beats, bucketed by the mobilization PHASE they read as
// (Phase 1 rearmament .. Phase 4 late-war exhaustion) plus a GENERIC bucket
// usable at any phase. Each entry is {text(ctx), source} — `source` picks
// which dateline pool the item is filed under.
const PHASE1 = [ // Rearmament / Peacetime -> Limited Mobilization
  c => `Defense Department announces ${c.plant} awarded contract for ${c.vehicleType}; ${c.jobs} new jobs created.`,
  c => `Conscription order: ${c.recruits} men called for military induction from ${c.city}; report to ${c.camp} within the fortnight.`,
  c => `Government establishes War Production Board to coordinate industry — ${c.island}'s factories placed on standby footing.`,
  c => `Rationing begins: ${c.commodity} limited to a fixed weekly ration per household.`,
  c => `Civil defense drill: ${c.city} conducts a blackout and air-raid shelter test; all residents urged to participate.`,
  c => `Defense bond drive launched in ${c.city}; local leaders visit factories to encourage purchases.`,
  c => `${c.plant} shifts to military production: civilian output ceases, all-military schedule begins Monday.`,
  c => `Labor shortage feared: industry warns conscription could cost ${c.percent}% of the skilled workforce.`,
  c => `Training camp opened: ${c.camp} begins inducting recruits monthly.`,
  c => `Scrap metal collection drive in ${c.city}: ${c.percent}% of target already turned in.`,
];
const PHASE2 = [ // Full Mobilization, early conflict
  c => `Breaking: enemy aircraft sighted off ${c.region}. Air-raid sirens sound in ${c.city}.`,
  c => `Factory retooling complete: ${c.plant} delivers its first ${c.vehicleType} off the line — output climbing.`,
  c => `Mandatory conscription expanded: minimum age lowered to ${c.age}; more men called each month.`,
  c => `Rationing tightened: ${c.commodity} ration cut ${c.percent}%. The public is urged to accept the sacrifice.`,
  c => `War production milestone: output at ${c.plant} exceeds target by ${c.percent}%.`,
  c => `Workplace strike narrowly averted at ${c.plant}; workers agree to terms, production continues uninterrupted.`,
  c => `New munitions works dispersed near ${c.city}, sited to keep production clear of enemy air routes.`,
  c => `Women in the factories: recruitment drives expand as more of ${c.city}'s workforce turns to war production.`,
  c => `Enemy advance checked: fresh troops mustered from ${c.city} hold the line near ${c.region}.`,
  c => `Supply shortage: ${c.commodity} rationing to continue indefinitely; reserves running lean.`,
  c => `Night shift declared at ${c.plant}; morale reported high despite the long hours.`,
];
const PHASE3 = [ // Peak War Economy
  c => `Production records shattered: military output at ${c.plant} now double its pre-war pace.`,
  c => `Civilian consumption rationed to the minimum: only a fraction of pre-war goods now on shelves.`,
  c => `Black market crackdown: authorities arrest ${c.profiteers} profiteers in ${c.city}; penalties severe.`,
  c => `Morale boost: enemy force turned back near ${c.region} — word spreads fast through the factories.`,
  c => `Worker exhaustion reported: absenteeism climbing at ${c.plant}; a mandatory rest day ordered.`,
  c => `Wounded from the fighting near ${c.frontCity} fill the wards; civilian patients moved to shelters.`,
  c => `Honor roll posted at ${c.city}'s town square, names of the fallen read aloud.`,
  c => `Factory sabotage suspected after an explosion at ${c.plant}; an investigation is underway.`,
  c => `New matériel unveiled: government displays the latest ${c.vehicleType}; crowds cheer the display.`,
  c => `Supply convoy reaches ${c.city} with critical cargo; a tense week on the ration line finally eases.`,
  c => `Evacuation order issued for parts of ${c.frontCity} as the fighting draws nearer.`,
  c => `Scorched-earth order: military directs select plants near ${c.frontCity} destroyed rather than let them fall intact.`,
  c => `Radio bulletin claims ${c.falseClaim}; the crowd outside ${c.city}'s town hall cheers.`,
];
const PHASE4 = [ // Late War / Exhaustion
  c => `Final reserves mobilized: conscription now reaches men up to age ${45 + Math.round(c.mobPct * 0.1)} previously exempt.`,
  c => `Breakthrough reported near ${c.region}: enemy forces pushed back, homeland threatened in turn.`,
  c => `Civilian casualties reported after fighting near ${c.frontCity}; clearing crews work through the night.`,
  c => `Industry dispersal order: select works near ${c.city} to relocate inland, out of range of enemy raids.`,
  c => `Field reports: ${c.substitute} substitutes failing under strain; engineers scramble for a fix.`,
  c => `Labor unrest spreads: several plants report short strikes; authorities warn of consequences.`,
  c => `Rationing board reports supplies critically thin across ${c.island}; further cuts expected.`,
  c => `Official broadcasts claim victory may come within the season; skepticism spreads quietly all the same.`,
  c => `Counter-offensive launched from ${c.city}; enemy raids ${c.island}'s industrial centers in reply.`,
  c => `Total sacrifice decreed: government calls for one final push from every factory and field.`,
];
const GENERIC = [ // any phase
  c => `Patriotic appeal: citizens of ${c.city} asked to contribute ${c.sacrifice} to the war effort.`,
  c => `Sabotage alert issued for ${c.city}: residents urged to report suspicious activity to authorities.`,
  c => `Technology bulletin: field engineers report ${c.innovation}, expected to improve ${c.vehicleType} output.`,
  c => `Rail network moves matériel toward ${c.frontCity}; convoy discipline credited for the pace.`,
  c => `Recruitment drive: the service seeks ${c.specialty}; ${c.bonus} offered to volunteers.`,
  c => `Morale building: officials tour ${c.plant}, workers photographed for the evening papers.`,
  c => `Bond drive in ${c.city} reaches ${c.percent}% of its target; organizers press for a final push.`,
  c => `Conscription boards report record turnout in ${c.city} this week.`,
  c => `Factories retool for war production across ${c.island} — the change is visible from the rail line in.`,
  c => `Supply update: ${c.commodity} stocks steady this week; no change to the ration.`,
];

const AMBIENT_BUCKETS = { PHASE1, PHASE2, PHASE3, PHASE4, GENERIC };

// Bucket weights by mobilization band index (0..3, see game/economy.js's
// MOBILIZATION_BANDS) and current threat — "early-game reads peacetime-ish,
// late-game reads total-war" per the task brief. Weight tables, not hard
// gates, so a bit of every era's voice can still surface (a peacetime bulletin
// about a bond drive can still run mid-war; a rare total-war beat can surface
// a touch early if the invasion has already forced the threat level up).
function bucketWeights(band, threatLevel) {
  const w = { PHASE1: 1, PHASE2: 0, PHASE3: 0, PHASE4: 0, GENERIC: 3 };
  if (band >= 0) w.PHASE1 = 6;
  if (band >= 1) { w.PHASE1 = 3; w.PHASE2 = 5; }
  if (band >= 2) { w.PHASE2 = 5; w.PHASE3 = 4; w.PHASE1 = 1; }
  if (band >= 3) { w.PHASE3 = 5; w.PHASE4 = 4; w.PHASE2 = 2; w.PHASE1 = 0; }
  // combat pushes weight toward the more urgent buckets regardless of how far
  // mobilization itself has climbed yet (the invasion can land before the
  // ramp has caught all the way up) — a direct read of economy.threatLevel,
  // same threat hook game/economy.js's own ramp already keys off.
  if (threatLevel > 0.3) { w.PHASE2 += 3; w.PHASE3 += 1; }
  if (threatLevel > 0.7) { w.PHASE3 += 2; w.PHASE4 += 1; }
  return w;
}
function weightedBucketPick(news, weights) {
  const entries = Object.entries(weights).filter(([, w]) => w > 0);
  const total = entries.reduce((s, [, w]) => s + w, 0);
  if (!total) return 'GENERIC';
  let r = nextRoll(news) * total;
  for (const [k, w] of entries) { r -= w; if (r <= 0) return k; }
  return entries[entries.length - 1][0];
}

// ---------------------------------------------------------------------------
// EVENT TEMPLATES — triggered headlines, not ambient-pool draws. Pulled from
// §5's phase-transition beats (Priority 3's "News system triggers on phase
// transitions") plus the task's explicit event list (combat begins, city
// capture, battalion raised, production milestone).
const BAND_TRANSITION_LINES = [
  null, // band 0 (Peacetime) is the starting state, not a transition anyone announces
  c => `Government announces a rearmament program: partial mobilization begins nationwide — ${c.mobPct}% and climbing.`,
  c => `War footing declared: conscription boards open nationwide, full mobilization ordered.`,
  c => `TOTAL WAR DECREE: every factory, every hand — ${c.island} declares total mobilization.`,
];
const COMBAT_BEGIN_LINES = [
  c => `BREAKING: enemy landing reported near ${c.region}. Coastal defense engaged.`,
  c => `Enemy forces sighted off ${c.region} — air-raid sirens sound across ${c.city}.`,
  c => `The invasion has begun: enemy troops ashore near ${c.frontCity}. All units to readiness.`,
];
const CITY_LOST_LINES = [
  name => `${name} has fallen: enemy forces overrun the city; evacuation underway.`,
  name => `Enemy breakthrough at ${name} — defenders withdraw to regroup.`,
  name => `${name} occupied: reports of enemy columns moving through the streets.`,
];
const CITY_RETAKEN_LINES = [
  name => `${name} retaken: defenders drive the enemy out; church bells ring across the city.`,
  name => `Victory at ${name}: the flag is raised again over the town hall.`,
  name => `${name} liberated — enemy garrison broken, the city is ours once more.`,
];
const BATTALION_RAISED_LINES = [
  c => `Home Guard mustering: a new battalion raised at ${c.city}; ${c.recruits} recruits sworn in.`,
  c => `New formation stood up near ${c.city} — the line grows by one more battalion.`,
];
const MILESTONE_LINES = [
  c => `War production milestone: military output surpasses ${c.number} — factories running at capacity.`,
  c => `Output report: cumulative war matériel now past ${c.number} units, ${c.island}'s industry at full stride.`,
];

function pushItem(news, text, source) {
  news._id = (news._id | 0) + 1;
  news.items.unshift({ id: news._id, text, source, t: news.elapsed || 0 });
  if (news.items.length > MAX_ITEMS) news.items.length = MAX_ITEMS;
}

// ---------------------------------------------------------------------------
// PUBLIC API

// initNews() — zero-arg (per the task's exported signature): all per-run
// scratch (event-detection last-seen state, the seeded roll counter, the
// ambient pacing clock) lives on the returned object, same "caller-owned
// state, not a module singleton" shape game/ambient.js/game/supply.js use, so
// a Play-Again reload gets a clean feed.
export function initNews() {
  return {
    items: [],
    _id: 0,
    _rollCounter: 0,
    elapsed: 0,
    _ambientTimer: 4, // small head start so the very first bulletin doesn't wait the full interval
    // event-detection scratch — undefined/null until the first tick fills it,
    // exactly like game/economy.js's own _connCacheVersion=-1 sentinel
    // pattern ("not computed yet" is a real, checked state, not skipped).
    _lastBandIdx: -1,
    _lastThreatOn: false,
    _lastOwners: null, // Map<cityIndex, owner>, filled on first tick
    _lastBattalionCount: -1,
    _lastMilestone: 0,
  };
}

// Ambient cadence — shortens as mobilization + threat climb, so the war gets
// louder as it escalates (not just grimmer). DESIGNER'S TO TUNE, same
// first-pass-placeholder spirit as game/economy.js's own tunables: sized so a
// short playtest actually sees the cadence visibly tighten, not tuned against
// a real campaign clock.
const AMBIENT_INTERVAL_MAX = 26; // seconds between bulletins at peacetime
const AMBIENT_INTERVAL_MIN = 7;  // seconds between bulletins at full total-war + active combat

// updateNews(news, world, economy, map, dt) — called once per frame (see
// main.js's loop(), right alongside updateEconomy/updateHomeDefense). Purely
// additive/observational: never mutates world/economy/map, only its own
// `news` state and the returned buffer.
export function updateNews(news, world, economy, map, dt) {
  news.elapsed = (news.elapsed || 0) + dt;

  const bandIdx = Math.max(0, MOBILIZATION_BANDS.findIndex(b => b === economy.band));
  const threatOn = (economy.threatTarget || 0) > 0;

  // ---- EVENT: mobilization band crossed ----
  if (news._lastBandIdx === -1) {
    news._lastBandIdx = bandIdx; // first tick: just record, don't announce the starting band
  } else if (bandIdx > news._lastBandIdx) {
    const line = BAND_TRANSITION_LINES[bandIdx];
    if (line) pushItem(news, line(buildCtx(news, world, economy, map)), pick(news, SOURCES_GOV));
    news._lastBandIdx = bandIdx;
  } else if (bandIdx < news._lastBandIdx) {
    news._lastBandIdx = bandIdx; // defensive (band never actually falls today, but stay consistent if it ever does)
  }

  // ---- EVENT: combat begins ("the invasion lands") ----
  if (threatOn && !news._lastThreatOn) {
    const line = pick(news, COMBAT_BEGIN_LINES);
    pushItem(news, line(buildCtx(news, world, economy, map)), pick(news, SOURCES_FIELD));
  }
  news._lastThreatOn = threatOn;

  // ---- EVENT: city captured / recaptured (diff map.cities[].owner against
  // last-seen scratch, exactly the "remember last-seen state between ticks"
  // pattern the task calls for — no dependency on objectives.js's own
  // capture-event array, so this works from world/economy/map alone) ----
  const cities = map.cities || [];
  if (!news._lastOwners) {
    news._lastOwners = cities.map(c => c.owner);
  } else {
    for (let i = 0; i < cities.length; i++) {
      const c = cities[i];
      const prevOwner = news._lastOwners[i];
      if (prevOwner !== undefined && c.owner !== prevOwner) {
        const lines = c.owner === 'enemy' ? CITY_LOST_LINES : CITY_RETAKEN_LINES;
        pushItem(news, pick(news, lines)(c.name), pick(news, SOURCES_FIELD));
      }
      news._lastOwners[i] = c.owner;
    }
    // map grew/shrunk (shouldn't happen mid-run, but stay in sync defensively)
    if (news._lastOwners.length !== cities.length) news._lastOwners = cities.map(c => c.owner);
  }

  // ---- EVENT: battalion raised (Home Guard / reinforcement milestone) —
  // PLAYER battalions only (world.battalions also fills with the enemy
  // landing force the instant combat begins — see game/enemyai.js/main.js's
  // triggerBeginCombat groupUnitsIntoBattalions call — and that is emphatically
  // NOT a "new formation stood up" moment for the home front). ----
  const bnCount = (world.battalions || []).filter(bn => bn.side === 'player').length;
  if (news._lastBattalionCount === -1) {
    news._lastBattalionCount = bnCount;
  } else if (bnCount > news._lastBattalionCount) {
    pushItem(news, pick(news, BATTALION_RAISED_LINES)(buildCtx(news, world, economy, map)), pick(news, SOURCES_HOME));
    news._lastBattalionCount = bnCount;
  } else if (bnCount < news._lastBattalionCount) {
    news._lastBattalionCount = bnCount; // battalions can be wiped out; don't re-fire on the way back up past an old high
  }

  // ---- EVENT: production milestone (military output crossing round steps —
  // step size widens as the war grows, so this doesn't fire every few
  // seconds once output is large) ----
  const output = economy.militaryOutput || 0;
  const step = Math.max(150, Math.round(news._lastMilestone / 3) || 150);
  if (output >= news._lastMilestone + step) {
    news._lastMilestone = Math.floor(output / step) * step;
    pushItem(news, pick(news, MILESTONE_LINES)(buildCtx(news, world, economy, map)), pick(news, SOURCES_HOME));
  }

  // ---- AMBIENT: periodic bulletin, cadence scaling with mobilization +
  // threat (louder as the war intensifies) ----
  news._ambientTimer -= dt;
  if (news._ambientTimer <= 0) {
    const urgency = Math.min(1, (economy.mobilizationLevel || 0) / 100 * 0.65 + (economy.threatLevel || 0) * 0.35);
    news._ambientTimer = AMBIENT_INTERVAL_MAX + (AMBIENT_INTERVAL_MIN - AMBIENT_INTERVAL_MAX) * urgency;
    const bucketKey = weightedBucketPick(news, bucketWeights(bandIdx, economy.threatLevel || 0));
    const pool = AMBIENT_BUCKETS[bucketKey];
    const line = pick(news, pool);
    if (line) {
      const source = bucketKey === 'PHASE1' ? pick(news, SOURCES_GOV)
        : (bucketKey === 'PHASE3' || bucketKey === 'PHASE4') ? pick(news, SOURCES_RADIO)
        : pick(news, SOURCES_HOME);
      pushItem(news, line(buildCtx(news, world, economy, map)), source);
    }
  }

  return news.items;
}

// Read accessor for the UI (and headless verification) — newest-first slice,
// never the live array itself, so a caller can't accidentally mutate the
// feed by holding onto what this returns.
export function newsFeed(news, count = VISIBLE_RECENT) {
  return news.items.slice(0, count);
}

// Debug/test convenience — inject an arbitrary headline (used by
// window.__debug.news.push and any headless test that wants to poke the UI
// without waiting out a real event/ambient tick).
export function pushNews(news, text, source = 'NATIONAL BULLETIN') {
  pushItem(news, text, source);
}
