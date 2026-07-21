// INTERACTIVE TECH/PRODUCTION TREE — GRAPH MODEL (CONCEPT.md's settled
// "Interactive in-game tech/production tree (direction)" paragraph). This
// module owns the DATA SIDE only: it walks the game's real definition
// tables (game/resources.js, game/buildings.js, game/production.js,
// game/research.js, game/units.js) and derives a node/edge graph plus a
// deterministic layered layout. It knows nothing about canvas, DOM, or
// drawing — game/techtreeview.js owns the icons/camera/hover/rendering, and
// reads this module's output.
//
// THE POINT OF DERIVING RATHER THAN HAND-LISTING: every node and every edge
// below is read off a *_DEFS table or a recipe/gate field that already
// exists for gameplay reasons. Add a new resource/building/tech/category/
// unit to those tables and it shows up here automatically, in the right
// tier, wired to the right neighbors, with zero changes to this file. That
// is the whole reason the task calls for "derive it ALL from the defs."
//
// ---------------------------------------------------------------------------
// NODE KINDS & TIERS (left-to-right layered layout, per the task's explicit
// ordering: Resources -> Facilities -> Research gates -> Categories -> Units).
//   resource  (TIER.RESOURCE) — game/resources.js RESOURCE_DEFS.
//   building  (TIER.BUILDING) — game/buildings.js BUILDING_DEFS, EVERY entry
//     (including ones with no downstream edges, like supplyDepot/
//     gunEmplacement/factory/barracks/mine — they're real buildings, they
//     just don't happen to gate any category or tech in the current data;
//     see the honest note in the PR report about these reading as leaves).
//   tech      (TIER.TECH) — game/research.js TECH_DEFS, one per gated
//     production category. A baseline category (tanks/aircraft/warships/
//     artillery) has NO tech node at all, matching research.js's own
//     "baseline-by-omission" comment — those categories are fed directly by
//     their facility building, no research gate in between.
//   category  (TIER.CATEGORY) — game/production.js PRODUCTION_DEFS.
//   unit      (TIER.UNIT) — game/units.js UNIT_DEFS, but ONLY the units that
//     actually appear in some PRODUCTION_DEFS[...].outputs list. Units that
//     exist in UNIT_DEFS but are never produced by a facility (infantry,
//     militia, scout, the baseline sam/aa batteries — all pre-placed/
//     scripted-spawn only) are outside the production/research economy this
//     view maps, so they correctly don't appear.
//
// ---------------------------------------------------------------------------
// EDGE KINDS (exactly the rules the task brief names):
//   'recipe'         resource        -> building (facility)   recipe inputs
//   'buildingPrereq' building        -> building (facility)   recipe.prerequisiteBuildings
//   'enables'        building (facility) -> category          facility complete -> category can run
//   'gate'           building/resource -> tech                TECH_DEFS prerequisiteBuildings / resourceCost
//   'techPrereq'     tech            -> tech                  TECH_DEFS prerequisiteTechs
//   'unlocks'        tech            -> category               TECH_DEFS unlocksCategory
//   'produces'       category        -> unit                  PRODUCTION_DEFS outputs
import { RESOURCE_LIST } from './resources.js';
import { BUILDING_DEFS } from './buildings.js';
import { PRODUCTION_DEFS } from './production.js';
import { TECH_DEFS, techStatus, isCategoryUnlocked } from './research.js';
import { UNIT_DEFS } from './units.js';

export const TIER = { RESOURCE: 0, BUILDING: 1, TECH: 2, CATEGORY: 3, UNIT: 4 };
export const TIER_LABELS = ['RESOURCES', 'FACILITIES', 'RESEARCH', 'PRODUCTION', 'UNITS'];

// ---------------------------------------------------------------------------
// LAYOUT TUNABLES — purely cosmetic spacing, DESIGNER'S TO TUNE like every
// other placeholder-numbers block in this codebase. Sized so a node's icon +
// label (see techtreeview.js NODE_SIZE) never touches its row neighbors at
// the default zoom.
export const LAYOUT = {
  COL_WIDTH: 340,
  ROW_HEIGHT: 74,
};

// Reverse lookup built once per graph build: production category id ->
// TECH_DEFS id that gates it (mirrors research.js's own private
// CATEGORY_TO_TECH, rebuilt here because research.js doesn't export it).
function buildCategoryToTech() {
  const map = {};
  for (const [techId, def] of Object.entries(TECH_DEFS)) map[def.unlocksCategory] = techId;
  return map;
}

// Reverse lookup: unit key -> the ONE category that produces it. A unit
// def could in principle be listed under more than one category's outputs
// in future data; if that ever happens the last category found wins (same
// "last write wins" shape as production.js's own FACILITY_TO_CATEGORY).
function buildUnitToCategory() {
  const map = {};
  for (const [catId, def] of Object.entries(PRODUCTION_DEFS)) {
    for (const uKey of def.outputs) map[uKey] = catId;
  }
  return map;
}

// ---------------------------------------------------------------------------
// GRAPH BUILD — pure function of the *_DEFS tables (which are static module-
// level data, never mutated at runtime), so the result is cached after the
// first call. Nothing here reads world/economy/researchState — that's
// computeNodeState() below, called fresh every time the view wants to know
// what's LIVE right now.
let cachedGraph = null;
export function getTechTreeGraph() {
  if (cachedGraph) return cachedGraph;
  cachedGraph = buildGraph();
  return cachedGraph;
}

function buildGraph() {
  const nodesById = new Map();
  const edges = [];

  function addNode(id, kind, key, tier, extra) {
    const node = { id, kind, key, tier, ...extra };
    nodesById.set(id, node);
    return node;
  }
  function addEdge(fromId, toId, kind) {
    // defensive: only wire an edge if both ends actually exist as nodes —
    // guards against a future data typo (e.g. a TECH_DEFS prerequisiteBuildings
    // entry that misspells a BUILDING_DEFS key) silently drawing a dangling
    // line instead of surfacing as a console warning.
    if (!nodesById.has(fromId) || !nodesById.has(toId)) {
      console.warn(`[techtree] dropped edge ${fromId} -> ${toId}: missing endpoint`);
      return;
    }
    edges.push({ from: fromId, to: toId, kind });
  }

  for (const r of RESOURCE_LIST) {
    addNode(`resource:${r.id}`, 'resource', r.id, TIER.RESOURCE, { def: r, name: r.name });
  }
  for (const [key, def] of Object.entries(BUILDING_DEFS)) {
    addNode(`building:${key}`, 'building', key, TIER.BUILDING, { def, name: def.name });
  }
  for (const [techId, def] of Object.entries(TECH_DEFS)) {
    addNode(`tech:${techId}`, 'tech', techId, TIER.TECH, { def, name: def.name });
  }
  for (const [catId, def] of Object.entries(PRODUCTION_DEFS)) {
    addNode(`category:${catId}`, 'category', catId, TIER.CATEGORY, { def, name: def.name });
  }
  const unitToCategory = buildUnitToCategory();
  for (const [uKey, catId] of Object.entries(unitToCategory)) {
    const uDef = UNIT_DEFS[uKey];
    if (!uDef) continue; // defensive: a typo'd output key in PRODUCTION_DEFS shouldn't crash the tree
    addNode(`unit:${uKey}`, 'unit', uKey, TIER.UNIT, { def: uDef, name: uDef.name, category: catId });
  }

  // --- edges driven off PRODUCTION_DEFS: recipe inputs, building prereqs,
  // facility->category, category->units ---
  for (const [catId, prod] of Object.entries(PRODUCTION_DEFS)) {
    const facilityId = `building:${prod.facility}`;
    for (const rid of Object.keys(prod.recipe.resourceCostPerUnit || {})) {
      addEdge(`resource:${rid}`, facilityId, 'recipe');
    }
    for (const bKey of prod.recipe.prerequisiteBuildings || []) {
      addEdge(`building:${bKey}`, facilityId, 'buildingPrereq');
    }
    addEdge(facilityId, `category:${catId}`, 'enables');
    for (const uKey of prod.outputs) addEdge(`category:${catId}`, `unit:${uKey}`, 'produces');
  }

  // --- edges driven off TECH_DEFS: the research gate (buildings + techs +
  // resources all feed the tech node), tech prereqs, and unlocksCategory ---
  for (const [techId, def] of Object.entries(TECH_DEFS)) {
    const techNodeId = `tech:${techId}`;
    for (const bKey of def.prerequisiteBuildings || []) addEdge(`building:${bKey}`, techNodeId, 'gate');
    for (const tId of def.prerequisiteTechs || []) addEdge(`tech:${tId}`, techNodeId, 'techPrereq');
    for (const rid of Object.keys(def.resourceCost || {})) addEdge(`resource:${rid}`, techNodeId, 'gate');
    addEdge(techNodeId, `category:${def.unlocksCategory}`, 'unlocks');
  }

  layoutGraph(nodesById, edges);

  const nodes = [...nodesById.values()];
  // adjacency, built once for hover/edge-highlighting use (techtreeview.js
  // wants "edges brighten along satisfied paths" — cheapest way to answer
  // "what touches this node" is a precomputed index rather than filtering
  // the flat edges array every draw call).
  const outgoing = new Map(), incoming = new Map();
  for (const n of nodes) { outgoing.set(n.id, []); incoming.set(n.id, []); }
  for (const e of edges) { outgoing.get(e.from).push(e); incoming.get(e.to).push(e); }

  return { nodes, edges, nodesById, outgoing, incoming, categoryToTech: buildCategoryToTech(), unitToCategory };
}

// Deterministic layered layout: nodes bucket by tier (their column), then
// sort within the column and stack them evenly spaced, centered vertically
// on the tallest column so tiers line up around a shared horizontal axis
// instead of all hugging the top. Row spacing/count per column never
// changes past this point, which is what guarantees zero node-on-node
// OVERLAP (the hard requirement) regardless of what order barycenterPasses
// below puts the rows in — it only ever PERMUTES a column's existing rows,
// never adds/removes/resizes them.
function layoutGraph(nodesById, edges) {
  const byTier = [[], [], [], [], []];
  for (const n of nodesById.values()) byTier[n.tier].push(n);

  // Sort key per tier: units group by their category (so a category's whole
  // output cluster sits together rather than interleaved alphabetically),
  // everything else sorts by display name — cheap, stable, fully derived
  // from data already on the node (no hardcoded ordering table). This is
  // the deterministic STARTING order the barycenter passes below refine —
  // starting from a stable, name-derived order (rather than Map iteration
  // order) means two runs always converge on the identical final layout.
  for (let tier = 0; tier < byTier.length; tier++) {
    byTier[tier].sort((a, b) => {
      if (tier === TIER.UNIT) {
        const c = (a.category || '').localeCompare(b.category || '');
        if (c) return c;
      }
      return (a.name || a.key).localeCompare(b.name || b.key);
    });
  }

  barycenterPasses(byTier, edges);

  const tallest = Math.max(...byTier.map(col => col.length));
  const totalHeight = (tallest - 1) * LAYOUT.ROW_HEIGHT;
  for (let tier = 0; tier < byTier.length; tier++) {
    const col = byTier[tier];
    const colHeight = (col.length - 1) * LAYOUT.ROW_HEIGHT;
    const yOffset = (totalHeight - colHeight) / 2; // center this column within the tallest one
    col.forEach((n, i) => {
      n.x = tier * LAYOUT.COL_WIDTH;
      n.y = i * LAYOUT.ROW_HEIGHT + yOffset;
    });
  }
}

// CROSSING-REDUCTION PASS (polish fix — the Resources→Facilities fan-out is
// the worst offender: 4 resource nodes wiring into ~15 facility nodes with
// no ordering discipline produces a dense hairball). This is the classic
// Sugiyama-style layered-graph heuristic: repeatedly re-sort each column by
// the average row-position ("barycenter") of its neighbors in the column
// the sweep is currently reading FROM, alternating left-to-right and
// right-to-left so information about far-column structure propagates back
// through the middle tiers too. It is NOT a true crossing-minimization
// solver (that's NP-hard) — it's a cheap, fully deterministic approximation
// that measurably untangles the common case (mostly-local, mostly-adjacent-
// tier edges) without any new dependency. A fixed, small pass count keeps
// it deterministic and bounded-cost regardless of graph size.
const BARYCENTER_PASSES = 3;
function barycenterPasses(byTier, edges) {
  // nodeTier: node id -> which column it lives in (fixed for the whole
  // pass — a node never changes tier here, only its row within one).
  const nodeTier = new Map();
  for (let tier = 0; tier < byTier.length; tier++) {
    for (const n of byTier[tier]) nodeTier.set(n.id, tier);
  }
  // neighborIds: nodeId -> every OTHER node id it has an edge to/from,
  // regardless of direction — a barycenter sort filters this down to the
  // subset that happens to sit in the tier it's currently reading from.
  const neighborIds = new Map();
  for (const id of nodeTier.keys()) neighborIds.set(id, []);
  for (const e of edges) {
    neighborIds.get(e.from).push(e.to);
    neighborIds.get(e.to).push(e.from);
  }
  const ordOf = new Map(); // node id -> current row index within its own column
  function reindex(col) { col.forEach((n, i) => ordOf.set(n.id, i)); }
  for (const col of byTier) reindex(col);

  // Re-sort `col` by each node's average row index among neighbors that
  // live in `fromTier`. A node with no neighbors there keeps its current
  // row (falls back to its own ordinal) so isolated nodes don't all
  // collapse to the top, and ties break on that same fallback — keeping
  // the sort stable and deterministic run-to-run.
  function sortColumnByBarycenter(col, fromTier) {
    const scored = col.map(n => {
      const neighborOrds = [];
      for (const nbId of neighborIds.get(n.id)) {
        if (nodeTier.get(nbId) === fromTier) neighborOrds.push(ordOf.get(nbId));
      }
      const bary = neighborOrds.length
        ? neighborOrds.reduce((s, v) => s + v, 0) / neighborOrds.length
        : ordOf.get(n.id); // no neighbors in that tier: hold current position
      return { n, bary, fallback: ordOf.get(n.id) };
    });
    scored.sort((a, b) => a.bary - b.bary || a.fallback - b.fallback);
    col.length = 0;
    for (const s of scored) col.push(s.n);
  }

  for (let pass = 0; pass < BARYCENTER_PASSES; pass++) {
    // left-to-right sweep: each tier (after the first) re-sorts by its
    // neighbors' rows in the tier immediately to its LEFT.
    for (let tier = 1; tier < byTier.length; tier++) {
      sortColumnByBarycenter(byTier[tier], tier - 1);
      reindex(byTier[tier]);
    }
    // right-to-left sweep: mirror image, reading from the tier to the RIGHT
    // — this is what lets a resource node (tier 0) settle near the
    // vertical center of the facilities it actually feeds, not just the
    // reverse.
    for (let tier = byTier.length - 2; tier >= 0; tier--) {
      sortColumnByBarycenter(byTier[tier], tier + 1);
      reindex(byTier[tier]);
    }
  }
}

// ---------------------------------------------------------------------------
// LIVE STATE — the "fills out as you build/research" payoff. Called fresh
// every time the view draws (cheap: ~50-60 nodes, a handful of linear scans
// over world.buildings each), never cached, so it always reflects the
// CURRENT world/economy/researchState — exactly the live-read requirement
// the task calls out explicitly.
//
// Returns { state, badge, detail } where:
//   state  — one of 'locked' | 'researchable' | 'active' | 'unlocked' | 'built'
//            (a shared vocabulary across all five node kinds, described
//            per-kind in the comments below; techtreeview.js maps this
//            directly to a palette tint).
//   badge  — a short glyph-overlay hint ('lock' | 'check' | 'clock' | null).
//   detail — kind-specific extra data for the hover tooltip (recipe text,
//            gate progress, stockpile amount, unit stats, ...).
export function computeNodeState(node, ctx) {
  switch (node.kind) {
    case 'resource': return resourceState(node, ctx);
    case 'building': return buildingState(node, ctx);
    case 'tech': return techState(node, ctx);
    case 'category': return categoryState(node, ctx);
    case 'unit': return unitState(node, ctx);
    default: return { state: 'locked', badge: null, detail: {} };
  }
}

// Resources have no lock/unlock concept of their own (CONCEPT.md's supply
// chain: a resource is always "real," it's just empty until mined) — always
// reads 'available' (mapped to the neutral/cyan tint), with the live
// stockpile amount + accrual rate as the payoff detail.
function resourceState(node, ctx) {
  const amt = ctx.economy.resources[node.key] || 0;
  const rate = (ctx.economy.resourceRates && ctx.economy.resourceRates[node.key]) || 0;
  return { state: 'available', badge: null, detail: { amount: amt, rate } };
}

function playerBuildingsOf(ctx, key) {
  return ctx.world.buildings.filter(b => b.side === 'player' && b.key === key);
}

// Buildings never LOCK (any building can be sited/afforded any time — the
// task's four-state vocabulary collapses to three for this kind): 'built'
// (>=1 complete), 'active' (>=1 under construction — reuses the 'active'
// bucket so techtreeview.js doesn't need a sixth palette entry), or
// 'available' (none placed yet, buildable any time).
function buildingState(node, ctx) {
  const instances = playerBuildingsOf(ctx, node.key);
  const complete = instances.filter(b => b.status === 'complete');
  const constructing = instances.filter(b => b.status === 'constructing');
  if (complete.length) return { state: 'built', badge: 'check', detail: { count: complete.length, def: node.def } };
  if (constructing.length) {
    const avgProgress = constructing.reduce((s, b) => s + b.buildProgress, 0) / constructing.length;
    return { state: 'active', badge: 'clock', detail: { count: constructing.length, progress: avgProgress, def: node.def } };
  }
  return { state: 'available', badge: null, detail: { def: node.def } };
}

// Techs read straight off research.js's own techStatus() — that function is
// already the single source of truth main.js's #techPanel uses, so the tree
// view and the tech panel can never disagree about a tech's state.
function techState(node, ctx) {
  const st = techStatus(ctx.world, ctx.researchState, node.key);
  const def = node.def;
  if (st.state === 'unlocked') return { state: 'unlocked', badge: 'check', detail: { def } };
  if (st.state === 'active') return { state: 'active', badge: 'clock', detail: { def, progress: st.progress } };
  if (st.state === 'researchable') return { state: 'researchable', badge: null, detail: { def } };
  // 'locked' and 'busy' both read as 'locked' in the tree's palette (busy
  // means "gate satisfied, but the one research slot is occupied elsewhere"
  // — still not something the player can act on for THIS tech right now,
  // same visual bucket as a genuinely unmet gate) but the tooltip keeps the
  // distinction via st.state/st.missingBuildings/st.missingTechs.
  return { state: 'locked', badge: 'lock', detail: { def, raw: st } };
}

// Categories: unlocked (research gate satisfied — baseline categories with
// no TECH_DEFS entry are always unlocked, per isCategoryUnlocked's own
// "no gate, always on" contract) is required before 'built' can apply;
// 'built' additionally requires the facility to actually be standing and
// complete (that's when the category is genuinely live-producing).
function categoryState(node, ctx) {
  const unlocked = isCategoryUnlocked(ctx.researchState, node.key);
  const facilityKey = node.def.facility;
  const facilities = playerBuildingsOf(ctx, facilityKey);
  const complete = facilities.filter(b => b.status === 'complete');
  if (!unlocked) return { state: 'locked', badge: 'lock', detail: { def: node.def } };
  if (complete.length) {
    const producing = complete.some(b => b.prodState === 'producing');
    return { state: 'built', badge: producing ? 'clock' : 'check', detail: { def: node.def, producing, facilities: complete } };
  }
  return { state: 'unlocked', badge: null, detail: { def: node.def } };
}

// Units: mirror the category's state one tier further out — a unit reads
// 'built' once its category's facility has actually produced at least one
// (world state, not just "the facility exists"), 'unlocked' once its
// category is unlocked but nothing's rolled off the line yet, else 'locked'.
function unitState(node, ctx) {
  const catId = node.category;
  const prod = PRODUCTION_DEFS[catId];
  const unlocked = isCategoryUnlocked(ctx.researchState, catId);
  if (!unlocked) return { state: 'locked', badge: 'lock', detail: { def: node.def, category: catId } };
  const facilities = playerBuildingsOf(ctx, prod.facility).filter(b => b.status === 'complete');
  const produced = facilities.some(b => (b.producedCount || 0) > 0);
  if (produced) return { state: 'built', badge: 'check', detail: { def: node.def, category: catId } };
  return { state: 'unlocked', badge: null, detail: { def: node.def, category: catId } };
}
