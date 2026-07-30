// AFTER-ACTION REPORT — records a run as it happens and renders a readable
// post-battle account of it ("i love looking at my player stats after a
// mission hehe" — the designer). This is an OBSERVATION LAYER ONLY: every
// function here READS world/economy/map/battalion/research state that other
// systems already own and mutate, and appends to a private log object. It
// never writes to world/economy/map/battalions/units, never issues an
// order, never changes a number that affects simulation or balance — the
// exact same "additive, never gates behavior" contract game/news.js and
// game/ambient.js already follow for their own per-frame observation passes
// (see those files' headers).
//
// SHAPE: initAfterAction() creates a log; recordEvent()/recordCombatBegin()/
// recordCityCapture() are called from the exact spots in main.js those
// events already happen (city capture already flows through
// game/objectives.js's updateObjectives() return value, combat-begin
// already flows through main.js's triggerBeginCombat, etc. — this module
// doesn't reimplement any of that detection, it just gets called from
// there); updateAfterAction() is the one per-frame pass that DIFFS state
// this module has no other hook into (buildings finishing construction,
// research completing, mobilization band shifts, battalions being raised/
// broken/destroyed, units dying) since none of those already hand back a
// discrete "this just happened" event the way city capture does.
// buildReport() turns the finished log into a structured, display-ready
// object once (at outcome time); reportToText() serializes that same object
// into the plain-text paste format for the COPY REPORT button.
//
// game/conversion.js DOES NOT EXIST in this codebase (checked at the time
// this module was written — see docs/ the designer's task brief, "industry
// conversions if game/conversion.js exists"). Nothing here imports or
// references it; if a future pass adds one, its events slot into the same
// recordEvent()/timeline machinery everything else here already uses.

import { moraleState } from './battalionMorale.js';
import { cityCenterWorld } from './objectives.js';
import { homeGuardTarget } from './homedefense.js';
import { TECH_DEFS } from './research.js';
import { REINFORCEMENT_DEFS } from './reinforcements.js';

// ---------------------------------------------------------------------------
export const AFTER_ACTION_TUNABLES = {
  SAMPLE_INTERVAL_S: 2,          // economy-curve sampling cadence — "every few seconds" per the task brief
  MAX_CURVE_SAMPLES: 600,        // ~20 minutes of curve at the cadence above; caps memory on a very long run
  MAX_TIMELINE_ENTRIES: 400,     // generous for a normal run, still a hard cap so a pathological run can't balloon
  MAX_LOSS_DETAIL: 300,          // per-side detailed loss records kept for the worst-engagement scan (totals are NEVER capped, only the detail list)
  WORST_ENGAGEMENT_WINDOW_S: 12, // sliding window a cluster of losses must fall inside to count as "one engagement"
  WORST_ENGAGEMENT_MIN_LOSSES: 3, // below this, "worst engagement" isn't worth reporting — too thin to mean anything
};

// Human labels for each battalion SOURCE (elite/standard/militia come from
// REINFORCEMENT_DEFS' own display names — game/reinforcements.js — so this
// never drifts out of sync with what the Reinforcements panel itself calls
// them; homeGuard has no reinforcements-panel equivalent, named directly).
function sourceLabel(source) {
  if (source === 'homeGuard') return 'Home Guard';
  const def = REINFORCEMENT_DEFS[source];
  return def ? def.name : source;
}

function fmtT(t) {
  const s = Math.max(0, Math.round(t));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${String(m).padStart(2, '0')}:${String(r).padStart(2, '0')}`;
}

function snapshotEconomy(economy, t) {
  return {
    t,
    ic: economy.ic, manpower: economy.manpower, influence: economy.influence,
    mobilizationLevel: economy.mobilizationLevel, band: economy.band ? economy.band.label : '',
  };
}

function nearestCityName(map, x, y) {
  let best = null, bestD = Infinity;
  for (const c of (map && map.cities) || []) {
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const d = (cx - x) ** 2 + (cy - y) ** 2;
    if (d < bestD) { bestD = d; best = c; }
  }
  return best ? best.name : null;
}

function currentPhase(log) { return log.combatBeganAt != null ? 'COMBAT' : 'PREP'; }

// ---------------------------------------------------------------------------
// initAfterAction — creates the empty log. Takes no arguments deliberately:
// the run's actual state (world/economy/map) doesn't exist as "the log's
// baseline" yet at the point this is normally called (right after world is
// created, per main.js's wiring) — updateAfterAction's own first call does
// the one-time bootstrap snapshot (see `_bootstrapped` below) once the
// starting industry/garrison for THIS run actually exists, so those don't
// misread as "events that happened at t=0."
export function initAfterAction() {
  return {
    t: 0,
    _bootstrapped: false,
    _truncated: false,
    combatBeganAt: null,
    landingSize: 0,
    timeline: [],
    // curve: throttled samples for charting; peaks: tracked every call
    // (cheap comparisons) regardless of the sampling throttle, so a spike
    // between two samples is never missed.
    curve: [],
    sampleAccum: 0,
    peaks: { ic: 0, manpower: 0, influence: 0, mobilizationLevel: 0, fielded: 0 },
    atLanding: null,
    atFirstCityLoss: null,
    lastBandLabel: null,
    seenBuildings: new Set(),
    seenTechs: new Set(),
    seenBattalions: new Set(),
    // Map<battalionId, {...bookkeeping}> — kept even after a battalion is
    // pruned from world.battalions (that's how "destroyed" is detected).
    battalionInfo: new Map(),
    homeGuardMustered: 0,
    // Map<unitId, {side,key,name,x,y}> for currently-alive units this module
    // has seen — an id that drops out of world.units between two ticks is a
    // death; pruned as each death is recorded so this only ever holds
    // CURRENTLY-alive units, never growing past world.units.length.
    lastUnitIds: new Map(),
    lossCounts: { player: 0, enemy: 0 }, // never capped — totals are always exact
    unitLosses: { player: [], enemy: [] }, // capped detail (MAX_LOSS_DETAIL) for the worst-engagement scan
  };
}

// ---------------------------------------------------------------------------
// recordEvent — generic timeline append. `type` is a short tag (see the
// describeEvent() table below for every type this module itself emits);
// `data` is a plain object describeEvent()/reportToText() read to phrase the
// line. Capped at MAX_TIMELINE_ENTRIES — once hit, silently stops accepting
// new entries (one closing "timeline truncated" marker is appended the
// first time the cap is reached) rather than dropping/rotating old ones, so
// the early campaign (build order, first contact) — the part most worth
// reading back — is never the part that gets evicted.
export function recordEvent(log, type, data = {}) {
  if (log.timeline.length >= AFTER_ACTION_TUNABLES.MAX_TIMELINE_ENTRIES) {
    if (!log._truncated) {
      log._truncated = true;
      log.timeline.push({ t: log.t, phase: currentPhase(log), type: 'truncated', data: {} });
    }
    return;
  }
  log.timeline.push({ t: log.t, phase: currentPhase(log), type, data });
}

// recordCombatBegin — call once, right where main.js's triggerBeginCombat
// spawns the landing force (AFTER the spawn, same "measure against the real
// landing force" discipline game/objectives.js's initInvasion already
// follows). One-shot (a stray second call is a defensive no-op) — snapshots
// the economy at the literal instant of contact, which is what the "banked
// capacity at the landing" report stat reads back later.
export function recordCombatBegin(log, world, economy) {
  if (log.combatBeganAt != null) return;
  log.combatBeganAt = log.t;
  log.landingSize = world.units.reduce((n, u) => n + (u.side === 'enemy' && u.hp > 0 ? 1 : 0), 0);
  log.atLanding = snapshotEconomy(economy, log.t);
  recordEvent(log, 'combatBegin', { landingSize: log.landingSize });
}

// recordCityCapture — call once per event game/objectives.js's
// updateObjectives() already returns each frame (main.js's captureEvents
// loop — the exact same array that already drives the reactive director's
// city-event bubble). Snapshots the economy the first time a city is lost
// TO the enemy (never overwritten after — "the first city loss" is a fixed
// moment, not whichever fell most recently).
export function recordCityCapture(log, economy, ev) {
  recordEvent(log, 'cityCapture', { city: ev.city.name, from: ev.from, to: ev.to });
  if (ev.to === 'enemy' && !log.atFirstCityLoss) {
    log.atFirstCityLoss = { ...snapshotEconomy(economy, log.t), city: ev.city.name };
  }
}

// ---------------------------------------------------------------------------
// updateAfterAction — the one per-frame diffing pass, called from main.js's
// loop() alongside every other per-frame update. `ctx.researchState` is
// optional (a defensive degrade: no research module wired in still just
// means no research-completion timeline entries, everything else in this
// pass still runs).
export function updateAfterAction(log, world, economy, map, dt, ctx = {}) {
  log.t += dt;

  if (!log._bootstrapped) {
    log._bootstrapped = true;
    for (const b of world.buildings) if (b.status === 'complete') log.seenBuildings.add(b.id);
    for (const bn of world.battalions) log.seenBattalions.add(bn.id);
    for (const u of world.units) {
      log.lastUnitIds.set(u.id, { side: u.side, key: u.key, name: (u.def && u.def.name) || u.key, x: u.x, y: u.y });
    }
    if (ctx.researchState) {
      for (const techId of Object.keys(ctx.researchState.completed || {})) log.seenTechs.add(techId);
    }
    log.lastBandLabel = economy.band ? economy.band.label : null;
  }

  // ---- BUILDINGS: player-owned constructions completing since last tick --
  for (const b of world.buildings) {
    if (b.side !== 'player' || b.status !== 'complete' || log.seenBuildings.has(b.id)) continue;
    log.seenBuildings.add(b.id);
    recordEvent(log, 'buildingComplete', { name: (b.def && b.def.name) || b.key, key: b.key });
  }

  // ---- RESEARCH: techs completing since last tick ----
  if (ctx.researchState) {
    for (const techId of Object.keys(ctx.researchState.completed || {})) {
      if (log.seenTechs.has(techId)) continue;
      log.seenTechs.add(techId);
      const def = TECH_DEFS[techId];
      recordEvent(log, 'researchComplete', { name: (def && def.name) || techId, techId });
    }
  }

  // ---- MOBILIZATION BAND transitions (economy.band — game/economy.js) ----
  const bandLabel = economy.band ? economy.band.label : null;
  if (bandLabel && bandLabel !== log.lastBandLabel) {
    recordEvent(log, 'mobilizationBand', { label: bandLabel, level: Math.round(economy.mobilizationLevel) });
    log.lastBandLabel = bandLabel;
  }

  // ---- BATTALIONS: raised (player only) / broken / reformed / destroyed --
  const stillPresent = new Set();
  for (const bn of world.battalions) {
    stillPresent.add(bn.id);
    let info = log.battalionInfo.get(bn.id);
    if (!info) {
      info = {
        id: bn.id, name: bn.name, type: bn.type, side: bn.side,
        homeGuard: !!bn.homeGuard, homeSector: bn.homeSector,
        raisedAt: log.t, peakUnits: 0, brokenAt: null, everBroken: false,
        currentlyBroken: false, reformed: false, destroyedAt: null, _lastRaw: 0,
      };
      log.battalionInfo.set(bn.id, info);
      if (bn.side === 'player' && !log.seenBattalions.has(bn.id)) {
        const source = bn.homeGuard ? 'homeGuard' : bn.type; // elite | standard | militia | homeGuard
        recordEvent(log, 'battalionRaised', { name: bn.name, source, label: sourceLabel(source) });
      }
      log.seenBattalions.add(bn.id);
    }

    const aliveCount = bn.units.reduce((n, u) => n + (u.hp > 0 ? 1 : 0), 0);
    if (aliveCount > info.peakUnits) info.peakUnits = aliveCount;

    // Home Guard mustering is INCREMENTAL (game/homedefense.js adds one
    // militia at a time to a growing battalion) — count only upward deltas
    // in raw roster size as "mustered," so a militia dying later doesn't
    // retroactively un-count the muster that already happened.
    if (bn.side === 'player' && info.homeGuard) {
      const raw = bn.units.length;
      if (raw > info._lastRaw) log.homeGuardMustered += raw - info._lastRaw;
      info._lastRaw = raw;
    }

    if (bn.side === 'player') {
      const broken = moraleState(bn) === 'broken';
      if (broken && !info.currentlyBroken) {
        info.currentlyBroken = true;
        info.everBroken = true;
        if (info.brokenAt == null) info.brokenAt = log.t;
        recordEvent(log, 'battalionBroken', { name: bn.name, bnId: bn.id });
      } else if (!broken && info.currentlyBroken) {
        info.currentlyBroken = false;
        info.reformed = true;
        recordEvent(log, 'battalionReformed', { name: bn.name, bnId: bn.id });
      }
    }
  }
  // Battalions PRUNED this frame (game/battalions.js's updateBattalions drops
  // a battalion once every sub-unit is dead) — id present in our own
  // bookkeeping but no longer in world.battalions == destroyed.
  for (const info of log.battalionInfo.values()) {
    if (info.destroyedAt == null && !stillPresent.has(info.id) && info.raisedAt != null) {
      info.destroyedAt = log.t;
      if (info.side === 'player') recordEvent(log, 'battalionDestroyed', { name: info.name, bnId: info.id });
    }
  }

  // ---- PEAK FIELDED — concurrent player battalions with >=1 living unit --
  let fieldedNow = 0;
  for (const bn of world.battalions) {
    if (bn.side === 'player' && bn.units.some(u => u.hp > 0)) fieldedNow++;
  }
  if (fieldedNow > log.peaks.fielded) log.peaks.fielded = fieldedNow;

  // ---- ECONOMY CURVE — peaks tracked every call; the curve ARRAY itself is
  // throttled to SAMPLE_INTERVAL_S (charting doesn't need per-frame density,
  // and this is what keeps a long run cheap per the task's guardrail). ----
  if (economy.ic > log.peaks.ic) log.peaks.ic = economy.ic;
  if (economy.manpower > log.peaks.manpower) log.peaks.manpower = economy.manpower;
  if (economy.influence > log.peaks.influence) log.peaks.influence = economy.influence;
  if (economy.mobilizationLevel > log.peaks.mobilizationLevel) log.peaks.mobilizationLevel = economy.mobilizationLevel;
  log.sampleAccum += dt;
  if (log.sampleAccum >= AFTER_ACTION_TUNABLES.SAMPLE_INTERVAL_S) {
    log.sampleAccum = 0;
    if (log.curve.length < AFTER_ACTION_TUNABLES.MAX_CURVE_SAMPLES) {
      log.curve.push(snapshotEconomy(economy, log.t));
    }
  }

  // ---- UNIT LOSSES — diff world.units ids against last tick's known-alive
  // set. An id that drops out is a death (units.js's updateUnits already
  // filters dead units out of world.units every frame — see that file's
  // `world.units = world.units.filter(u => u.hp > 0)` — so "id vanished"
  // IS "unit died," no separate hook needed). Position is kept current
  // every tick a unit is still seen, so the loss location reads as "where it
  // actually died," not "where it spawned." ----
  const currentIds = new Set();
  for (const u of world.units) {
    currentIds.add(u.id);
    const rec = log.lastUnitIds.get(u.id);
    if (!rec) {
      log.lastUnitIds.set(u.id, { side: u.side, key: u.key, name: (u.def && u.def.name) || u.key, x: u.x, y: u.y });
    } else {
      rec.x = u.x; rec.y = u.y;
    }
  }
  const deadIds = [];
  for (const id of log.lastUnitIds.keys()) if (!currentIds.has(id)) deadIds.push(id);
  for (const id of deadIds) {
    const rec = log.lastUnitIds.get(id);
    log.lastUnitIds.delete(id); // bound the map to currently-alive units only
    if (rec.side !== 'player' && rec.side !== 'enemy') continue; // defensive: no third side in this v1
    log.lossCounts[rec.side]++;
    const bucket = log.unitLosses[rec.side];
    if (bucket.length < AFTER_ACTION_TUNABLES.MAX_LOSS_DETAIL) {
      bucket.push({ t: log.t, key: rec.key, name: rec.name, x: rec.x, y: rec.y, city: nearestCityName(map, rec.x, rec.y) });
    }
  }
}

// ---------------------------------------------------------------------------
// WORST ENGAGEMENT — cheapest useful version of "largest cluster of losses
// in a short window near one city": merge both sides' capped detail loss
// lists, sort by time, slide a WORST_ENGAGEMENT_WINDOW_S window from every
// loss as a candidate start, keep the window with the most total losses,
// and label it with whichever city most of THAT window's losses were
// nearest to. O(n^2) on the capped detail lists (n <= 2*MAX_LOSS_DETAIL) —
// trivial at this game's scale. Returns null if there's nothing worth
// calling out (too few losses total, or the busiest window is still under
// WORST_ENGAGEMENT_MIN_LOSSES).
function computeWorstEngagement(log) {
  const all = [
    ...log.unitLosses.player.map(e => ({ ...e, side: 'player' })),
    ...log.unitLosses.enemy.map(e => ({ ...e, side: 'enemy' })),
  ].sort((a, b) => a.t - b.t);
  if (all.length < AFTER_ACTION_TUNABLES.WORST_ENGAGEMENT_MIN_LOSSES) return null;

  const W = AFTER_ACTION_TUNABLES.WORST_ENGAGEMENT_WINDOW_S;
  let best = null;
  for (let i = 0; i < all.length; i++) {
    const t0 = all[i].t;
    let j = i, playerLosses = 0, enemyLosses = 0;
    const cityCounts = {};
    while (j < all.length && all[j].t - t0 <= W) {
      const e = all[j];
      if (e.side === 'player') playerLosses++; else enemyLosses++;
      if (e.city) cityCounts[e.city] = (cityCounts[e.city] || 0) + 1;
      j++;
    }
    const total = playerLosses + enemyLosses;
    if (!best || total > best.total) {
      let city = null, cityCount = 0;
      for (const [c, n] of Object.entries(cityCounts)) if (n > cityCount) { city = c; cityCount = n; }
      best = { tStart: t0, tEnd: all[j - 1].t, total, playerLosses, enemyLosses, city };
    }
  }
  return (best && best.total >= AFTER_ACTION_TUNABLES.WORST_ENGAGEMENT_MIN_LOSSES) ? best : null;
}

// ---------------------------------------------------------------------------
// VERDICT LINES — 1-3 short, DATA-DRIVEN observations. Every generator below
// is threshold-gated against REAL recorded numbers (never flattery, never a
// fixed "nice job" string) and returns null when its own condition isn't
// met; buildReport takes the first 3 non-null generators in priority order
// (most narratively pointed first) so "nothing notable, say less" falls out
// naturally rather than needing a separate "is this a boring run" check.
function buildVerdicts(log, economy, map, orderOfBattle, butchersBill) {
  const candidates = [];

  // 1) Banked capacity at the moment a city was actually lost — the
  // "idle capacity during a crisis" stat the task brief calls out by name.
  if (log.atFirstCityLoss) {
    const s = log.atFirstCityLoss;
    const bits = [];
    if (s.ic >= 30) bits.push(`${Math.round(s.ic)} IC`);
    if (s.manpower >= 30) bits.push(`${Math.round(s.manpower)} manpower`);
    if (bits.length) candidates.push(`${bits.join(' and ')} banked when ${s.city} fell.`);
  } else if (log.atLanding && log.atLanding.ic >= 40) {
    candidates.push(`${Math.round(log.atLanding.ic)} IC sat banked the instant the landing force made contact.`);
  }

  // 2) Home Guard undershoot at whichever owned city missed its mobilization
  // target by the widest margin (game/homedefense.js's homeGuardTarget —
  // the exact same cap/formula the Home Defense panel itself reads).
  let worstShortfall = null;
  for (const info of log.battalionInfo.values()) {
    if (!info.homeGuard || info.side !== 'player') continue;
    const city = map.cities && map.cities[info.homeSector];
    if (!city) continue;
    const target = homeGuardTarget(economy, city);
    if (target < 2) continue; // too small a target to be a meaningful shortfall
    if (info.peakUnits < target * 0.6) {
      const shortfall = target - info.peakUnits;
      if (!worstShortfall || shortfall > worstShortfall.shortfall) {
        worstShortfall = { city: city.name, peak: info.peakUnits, target, shortfall };
      }
    }
  }
  if (worstShortfall) {
    candidates.push(`Home Guard at ${worstShortfall.city} never exceeded ${worstShortfall.peak} of ${worstShortfall.target} mustered.`);
  }

  // 3) A battalion that broke and was STILL broken (or destroyed while
  // broken) at the moment this report was built — "broke and never
  // reformed," picking the earliest such case (the one that had the most
  // time to recover and didn't).
  let neverReformed = null;
  for (const info of log.battalionInfo.values()) {
    if (info.side !== 'player' || !info.everBroken || !info.currentlyBroken) continue;
    if (!neverReformed || info.brokenAt < neverReformed.brokenAt) neverReformed = info;
  }
  if (neverReformed) {
    candidates.push(`The ${neverReformed.name} broke at ${fmtT(neverReformed.brokenAt)} and never reformed.`);
  }

  // 4) Invasion attrition — how thoroughly the landing force was broken as
  // a fighting force (only interesting on a real win, and only if the
  // remainder is genuinely small relative to what landed).
  if (log.landingSize > 0) {
    const remaining = butchersBill.enemyRemaining;
    const frac = remaining / log.landingSize;
    if (frac <= 0.34) {
      candidates.push(`Landing force reduced to ${remaining} of ${log.landingSize} — broken as a fighting force.`);
    }
  }

  // 5) Battalion attrition rate, generic fallback so a run with nothing
  // sharper to say about the economy/home-guard/morale still gets ONE real
  // number about how the campaign actually went, rather than nothing.
  if (orderOfBattle.raised > 0) {
    candidates.push(`${orderOfBattle.survived}/${orderOfBattle.raised} battalions raised survived the campaign${orderOfBattle.routed ? `; ${orderOfBattle.routed} broke under fire at some point` : ''}.`);
  }

  return candidates.slice(0, 3);
}

// ---------------------------------------------------------------------------
// describeEvent — the single source of phrasing for a timeline entry, read
// by both buildReport's HTML-ready timeline and reportToText's plain-text
// serializer, so the two can never drift out of sync with each other.
function describeEvent(e) {
  const d = e.data;
  switch (e.type) {
    case 'buildingComplete': return { text: `${d.name} completed`, tone: 'ok' };
    case 'researchComplete': return { text: `Research complete: ${d.name}`, tone: 'ok' };
    case 'mobilizationBand': return { text: `Mobilization shifted to ${d.label} (${d.level}%)`, tone: 'warn' };
    case 'combatBegin': return { text: `Enemy landing force makes contact — combat begins (${d.landingSize} units)`, tone: 'bad' };
    case 'cityCapture': return d.to === 'player'
      ? { text: `${d.city} recaptured`, tone: 'ok' }
      : { text: `${d.city} captured by the enemy`, tone: 'bad' };
    case 'battalionRaised': return { text: `${d.name} raised (${d.label})`, tone: 'ok' };
    case 'battalionBroken': return { text: `${d.name} broke and routed`, tone: 'bad' };
    case 'battalionReformed': return { text: `${d.name} reformed`, tone: 'ok' };
    case 'battalionDestroyed': return { text: `${d.name} destroyed`, tone: 'bad' };
    case 'outcome': return d.outcome === 'victory'
      ? { text: 'VICTORY — the invasion is spent', tone: 'ok' }
      : { text: 'DEFEAT — the capital has fallen', tone: 'bad' };
    case 'truncated': return { text: '(timeline truncated — run exceeded the reportable event cap)', tone: 'warn' };
    default: return { text: e.type, tone: 'warn' };
  }
}

// ---------------------------------------------------------------------------
// buildReport — call once, at outcome time (main.js's showOutcomeScreen).
// Appends the final 'outcome' timeline entry itself (so callers don't need
// a separate recordOutcome step) and returns a fully structured, display-
// and-serialize-ready report object. Never mutates world/economy/map.
export function buildReport(log, world, economy, map, outcome) {
  recordEvent(log, 'outcome', { outcome });

  const cities = map.cities || [];
  const held = cities.filter(c => c.owner === 'player').length;
  const lost = cities.filter(c => c.owner === 'enemy').length;
  const capital = cities[0] || null;
  const capitalStatus = !capital ? 'N/A' : (capital.owner === 'player' ? 'HELD' : 'LOST');

  let enemyRemaining = 0, playerRemaining = 0;
  for (const u of world.units) {
    if (u.hp <= 0) continue;
    if (u.side === 'enemy') enemyRemaining++;
    else if (u.side === 'player') playerRemaining++;
  }

  // ORDER OF BATTLE — grouped by source (elite/standard/militia/homeGuard),
  // player battalions only.
  const bySource = {};
  let raised = 0, routed = 0, destroyed = 0;
  for (const info of log.battalionInfo.values()) {
    if (info.side !== 'player') continue;
    const src = info.homeGuard ? 'homeGuard' : info.type;
    if (!bySource[src]) bySource[src] = { source: src, label: sourceLabel(src), raised: 0, subUnits: 0 };
    bySource[src].raised++;
    bySource[src].subUnits += info.peakUnits;
    raised++;
    if (info.everBroken) routed++;
    if (info.destroyedAt != null) destroyed++;
  }
  const orderOfBattle = {
    bySource: Object.values(bySource).sort((a, b) => b.raised - a.raised),
    raised, peakFielded: log.peaks.fielded, routed, destroyed,
    survived: Math.max(0, raised - destroyed),
    homeGuardMustered: log.homeGuardMustered,
  };

  const butchersBill = {
    playerLosses: log.lossCounts.player,
    enemyKills: log.lossCounts.enemy,
    enemyRemaining, playerRemaining,
    worstEngagement: computeWorstEngagement(log),
  };

  const timeline = log.timeline.map(e => {
    const { text, tone } = describeEvent(e);
    return { t: e.t, tLabel: fmtT(e.t), phase: e.phase, type: e.type, text, tone };
  });

  return {
    outcome,
    durationS: log.t, durationLabel: fmtT(log.t),
    cities: { held, lost, total: cities.length, capitalStatus, capitalName: capital ? capital.name : null },
    economy: {
      peak: {
        ic: Math.round(log.peaks.ic), manpower: Math.round(log.peaks.manpower),
        influence: Math.round(log.peaks.influence), mobilizationLevel: Math.round(log.peaks.mobilizationLevel),
      },
      final: {
        ic: Math.round(economy.ic), manpower: Math.round(economy.manpower),
        influence: Math.round(economy.influence), mobilizationLevel: Math.round(economy.mobilizationLevel),
        band: economy.band ? economy.band.label : '',
      },
      atLanding: log.atLanding ? {
        t: log.atLanding.t, tLabel: fmtT(log.atLanding.t),
        ic: Math.round(log.atLanding.ic), manpower: Math.round(log.atLanding.manpower),
        influence: Math.round(log.atLanding.influence), mobilizationLevel: Math.round(log.atLanding.mobilizationLevel),
      } : null,
      atFirstCityLoss: log.atFirstCityLoss ? {
        t: log.atFirstCityLoss.t, tLabel: fmtT(log.atFirstCityLoss.t), city: log.atFirstCityLoss.city,
        ic: Math.round(log.atFirstCityLoss.ic), manpower: Math.round(log.atFirstCityLoss.manpower),
        influence: Math.round(log.atFirstCityLoss.influence), mobilizationLevel: Math.round(log.atFirstCityLoss.mobilizationLevel),
      } : null,
    },
    orderOfBattle,
    butchersBill: {
      ...butchersBill,
      worstEngagement: butchersBill.worstEngagement && {
        ...butchersBill.worstEngagement,
        tStartLabel: fmtT(butchersBill.worstEngagement.tStart),
        tEndLabel: fmtT(butchersBill.worstEngagement.tEnd),
      },
    },
    landingSize: log.landingSize,
    timeline,
    verdicts: buildVerdicts(log, economy, map, orderOfBattle, butchersBill),
  };
}

// ---------------------------------------------------------------------------
// reportToText — plain-text serializer for the COPY REPORT button. Terse,
// headed, indented sections; a data readout, not narrative prose, per the
// task brief ("write REAL functional labels... this is a data readout, not
// narrative voice"). Pure function of `report` (buildReport's return
// value) — never touches log/world/economy directly, so it can't drift from
// what's actually on screen.
export function reportToText(report) {
  const lines = [];
  const rule = '='.repeat(64);
  lines.push(rule);
  lines.push(`AFTER-ACTION REPORT — ${report.outcome === 'victory' ? 'VICTORY' : 'DEFEAT'}`);
  lines.push(rule);
  lines.push(`Duration: ${report.durationLabel}`);
  lines.push(`Cities Held: ${report.cities.held}/${report.cities.total}   Capital (${report.cities.capitalName || 'n/a'}): ${report.cities.capitalStatus}`);
  lines.push(`Player Units Remaining: ${report.butchersBill.playerRemaining}   Enemy Remaining: ${report.butchersBill.enemyRemaining}`);
  lines.push('');

  if (report.verdicts.length) {
    lines.push('VERDICT');
    for (const v of report.verdicts) lines.push(`  - ${v}`);
    lines.push('');
  }

  lines.push('TIMELINE');
  if (report.timeline.length) {
    for (const e of report.timeline) {
      lines.push(`  [${e.tLabel}] ${e.phase.padEnd(6)} ${e.text}`);
    }
  } else {
    lines.push('  (no recorded events)');
  }
  lines.push('');

  const eco = report.economy;
  lines.push('ECONOMY');
  lines.push(`  Peak IC: ${eco.peak.ic}    Final IC: ${eco.final.ic}`);
  lines.push(`  Peak Manpower: ${eco.peak.manpower}    Final Manpower: ${eco.final.manpower}`);
  lines.push(`  Peak Influence: ${eco.peak.influence}    Final Influence: ${eco.final.influence}`);
  lines.push(`  Peak Mobilization: ${eco.peak.mobilizationLevel}%    Final: ${eco.final.mobilizationLevel}% (${eco.final.band})`);
  if (eco.atLanding) {
    lines.push(`  At Contact [${eco.atLanding.tLabel}]: IC ${eco.atLanding.ic}, Manpower ${eco.atLanding.manpower}, Influence ${eco.atLanding.influence}, Mobilization ${eco.atLanding.mobilizationLevel}%`);
  }
  if (eco.atFirstCityLoss) {
    lines.push(`  At First City Loss (${eco.atFirstCityLoss.city}) [${eco.atFirstCityLoss.tLabel}]: IC ${eco.atFirstCityLoss.ic}, Manpower ${eco.atFirstCityLoss.manpower}, Influence ${eco.atFirstCityLoss.influence}, Mobilization ${eco.atFirstCityLoss.mobilizationLevel}%`);
  }
  lines.push('');

  const oob = report.orderOfBattle;
  lines.push('ORDER OF BATTLE');
  if (oob.bySource.length) {
    for (const s of oob.bySource) {
      lines.push(`  ${s.label.padEnd(20)} raised ${s.raised}   sub-units ${s.subUnits}`);
    }
  } else {
    lines.push('  (no battalions raised)');
  }
  if (oob.homeGuardMustered) lines.push(`  Home Guard mustered (cumulative): ${oob.homeGuardMustered}`);
  lines.push(`  Peak Fielded: ${oob.peakFielded} battalion(s)`);
  lines.push(`  Routed: ${oob.routed}    Destroyed: ${oob.destroyed}    Survived: ${oob.survived}/${oob.raised}`);
  lines.push('');

  const bb = report.butchersBill;
  lines.push("BUTCHER'S BILL");
  lines.push(`  Player Units Lost: ${bb.playerLosses}`);
  lines.push(`  Enemy Units Killed: ${bb.enemyKills}`);
  lines.push(`  Enemy Remaining: ${bb.enemyRemaining}   Player Remaining: ${bb.playerRemaining}`);
  if (bb.worstEngagement) {
    const w = bb.worstEngagement;
    lines.push(`  Worst Engagement: ${w.city ? `near ${w.city}, ` : ''}${w.tStartLabel}-${w.tEndLabel}, ${w.total} losses (${w.playerLosses} player / ${w.enemyLosses} enemy)`);
  }
  lines.push(rule);
  return lines.join('\n');
}
