// GOAL-DRIVEN TUTORIAL — REVERSE PLANNER (Part A of the onboarding concept —
// CONCEPT.md's settled "Onboarding / tutorial concept" paragraph, beats 3-5:
// "the player browses the tips of the production trees ... and selects what
// they want to end up making. The game reverse-plans the how ... an ordered
// step list shown at the top of the screen.") This module owns the DATA side
// only — goal selection state + the reverse-planning algorithm — same split
// as game/techtree.js (graph data) vs game/techtreeview.js (drawing): this
// file knows nothing about canvas/DOM, main.js wires it into the checklist
// UI and game/techtreeview.js's click handling.
//
// THE REVERSE-PLANNER IS NOT A NEW DEPENDENCY GRAPH: game/techtree.js already
// encodes every prerequisite edge (resource->facility recipe inputs,
// facility->category enablement, building/tech gates, tech->tech prereqs)
// for real gameplay-gating reasons. "Reverse-plan from a goal" is just a
// backward graph walk over `incoming` starting at the goal's production
// category node — see computePlan below. Add a new resource/building/tech/
// category/unit to the *_DEFS tables (as every other system in this codebase
// already does) and the planner picks it up automatically, in the right
// order, with zero changes here.
import { getTechTreeGraph } from './techtree.js';
import { PRODUCTION_DEFS } from './production.js';
import { isCategoryUnlocked } from './research.js';

// ---------------------------------------------------------------------------
// PLACEHOLDER COPY (designer to rewrite) — same convention as main.js's own
// COPY block and game/research.js's RESEARCH_COPY: every player-facing
// string below is a plain, functional stand-in marked [PLACEHOLDER]. This is
// the checklist's copy; game/director.js DIRECTOR_COPY holds the reactive-
// line/gag-chain copy for Part B. Nothing here is read for gameplay logic.
export const TUTORIAL_COPY = {
  CHECKLIST_TITLE: '[PLACEHOLDER] Your plan (goal-driven)',
  CHECKLIST_DISMISS_HINT: '[PLACEHOLDER] Pick goals in the tree (G — click a unit or category). A suggestion, not a script — dismiss any time (× or H). Nothing here is required or gated.',
  NO_GOALS_HINT: '[PLACEHOLDER] Open the tree (G) and click a unit or production category to set it as a goal.',
  // Step label templates, one per node kind that can appear as a
  // prerequisite (see stepFor below) — keyed by the SAME four kinds
  // game/techtree.js's TIER enum names (resource/building/tech/category).
  STEP_RESOURCE: name => `[PLACEHOLDER] Secure a supply of ${name} (mine a deposit, or import it)`,
  STEP_BUILDING: name => `[PLACEHOLDER] Build a ${name}`,
  STEP_TECH: name => `[PLACEHOLDER] Research ${name}`,
  STEP_CATEGORY: name => `[PLACEHOLDER] Get ${name} production online`,
  // Payoff acknowledgement (CONCEPT.md: "the payoff is legible ... gives
  // feedback ('we're making jets now')") — shown once a goal's whole chain
  // is satisfied.
  PAYOFF: name => `[PLACEHOLDER] We're making ${name} now.`,
};

// ---------------------------------------------------------------------------
// GOAL STATE — which finished-product nodes (unit or category, per the task's
// "browse the tips" framing) the player has selected. Plain data, same
// create*State() shape as game/research.js's createResearchState /
// game/phase.js's createPhaseState — main.js owns the one live instance and
// passes it wherever it's needed (the tree view for click-toggling + goal
// markers, this module for planning, game/director.js for off-track
// classification).
export function createGoalState() { return { goals: new Set() }; }

// A node is "goal-able" iff it's a genuine finished product per CONCEPT.md's
// framing — a producible UNIT, or the PRODUCTION CATEGORY itself (picking
// the category directly is equivalent to "I want this whole line running"
// without caring which specific output rolls off it first). Resources,
// buildings, and techs are prerequisites, never goals themselves.
export function isGoalNode(node) { return !!node && (node.kind === 'unit' || node.kind === 'category'); }

export function toggleGoal(goalState, nodeId) {
  if (goalState.goals.has(nodeId)) goalState.goals.delete(nodeId);
  else goalState.goals.add(nodeId);
}

// A goal node always ultimately depends on exactly one PRODUCTION_DEFS
// category — a unit goal resolves through techtree.js's unitToCategory
// (node.category, set when the graph was built); a category goal already IS
// one.
function categoryIdForGoal(node) {
  if (node.kind === 'category') return node.key;
  if (node.kind === 'unit') return node.category;
  return null;
}

// ---------------------------------------------------------------------------
// SATISFACTION CHECKS — one per node kind that can appear as a plan step,
// each reading LIVE state (world/economy/researchState), same "never cached,
// always current" contract as game/techtree.js's computeNodeState. Kept
// separate from computeNodeState rather than reusing its output directly
// because the plan's notion of "done" is deliberately simpler/coarser than
// the tree view's five-state vocabulary (a step is binary: satisfied or not)
// and because a RESOURCE step needs a planning-specific definition the tree
// view doesn't need at all (see resourceSatisfied below).
function resourceSatisfied(resourceId, ctx) {
  // A resource node in the tree view always reads 'available' (it's always
  // "real," per techtree.js's own comment) — that's not useful for planning
  // ("have I actually secured this input" needs a stronger signal). Treat it
  // as secured once it's either actively flowing in (a mine bound to it) or
  // already stocked (e.g. from an import) — either one means the player
  // doesn't need to take further action on this specific input.
  const rate = (ctx.economy.resourceRates && ctx.economy.resourceRates[resourceId]) || 0;
  const amt = (ctx.economy.resources && ctx.economy.resources[resourceId]) || 0;
  return rate > 0 || amt > 0;
}

function buildingSatisfied(key, ctx) {
  return ctx.world.buildings.some(b => b.side === 'player' && b.key === key && b.status === 'complete');
}

function techSatisfied(techId, ctx) {
  return !!ctx.researchState.completed[techId];
}

// Mirrors game/techtree.js's categoryState 'built' bucket: unlocked (no gate,
// or its tech completed) AND the facility actually standing complete. Used
// both for the category's own step AND as a goal's overall "done" flag
// (once the category is genuinely live, every goal riding on it is met).
function categorySatisfied(catId, ctx) {
  if (!isCategoryUnlocked(ctx.researchState, catId)) return false;
  const facilityKey = PRODUCTION_DEFS[catId].facility;
  return ctx.world.buildings.some(b => b.side === 'player' && b.key === facilityKey && b.status === 'complete');
}

// Single dispatch point: is THIS node (any of the four prerequisite kinds)
// currently satisfied? Shared by stepFor (the checklist's per-step check)
// AND computePlan's per-goal completeness check below, so "is this goal
// fully done" and "is this individual step checked off" can never disagree
// about what "done" means for a given node.
function nodeSatisfied(node, ctx) {
  if (node.kind === 'resource') return resourceSatisfied(node.key, ctx);
  if (node.kind === 'building') return buildingSatisfied(node.key, ctx);
  if (node.kind === 'tech') return techSatisfied(node.key, ctx);
  return categorySatisfied(node.key, ctx); // 'category'
}

function stepFor(nodeId, ctx, graph) {
  const node = graph.nodesById.get(nodeId);
  const satisfied = nodeSatisfied(node, ctx);
  if (node.kind === 'resource') return { nodeId, kind: node.kind, name: node.name, label: TUTORIAL_COPY.STEP_RESOURCE(node.name), satisfied };
  if (node.kind === 'building') return { nodeId, kind: node.kind, name: node.name, label: TUTORIAL_COPY.STEP_BUILDING(node.name), satisfied };
  if (node.kind === 'tech') return { nodeId, kind: node.kind, name: node.name, label: TUTORIAL_COPY.STEP_TECH(node.name), satisfied };
  return { nodeId, kind: node.kind, name: node.name, label: TUTORIAL_COPY.STEP_CATEGORY(node.name), satisfied }; // 'category'
}

// Deterministic topological sort (Kahn's algorithm, layer-at-a-time) over the
// SUBGRAPH induced by `stepNodeIds` — a real topo sort rather than a plain
// tier-bucket sort because same-tier edges exist in the data (buildingPrereq
// building->building, techPrereq tech->tech: e.g. ammunitionPlant must
// precede hypersonicWorks, missileTech must precede hypersonicTech), which a
// tier sort alone would not order correctly. Each pass collects every
// currently-ready node (no remaining incoming edge from another node still
// in the set) so the result also reads as loosely "layered" — resources
// before the buildings that consume them, etc. — same spirit as
// techtree.js's own layered layout. Ties broken by (tier, name) so the
// order is stable run-to-run for the same plan.
function topoOrder(stepNodeIds, graph) {
  const { nodesById, incoming } = graph;
  const remaining = new Set(stepNodeIds);
  const order = [];
  const sortKey = id => { const n = nodesById.get(id); return `${n.tier}:${n.name || n.key}`; };
  while (remaining.size) {
    const ready = [...remaining]
      .filter(id => !(incoming.get(id) || []).some(e => remaining.has(e.from)))
      .sort((a, b) => sortKey(a).localeCompare(sortKey(b)));
    if (!ready.length) {
      // Defensive only: game/techtree.js's edge set is a DAG by construction
      // (see that file's own header comment enumerating edge kinds, all of
      // which point strictly tier-forward or within a tier with no back-
      // edges in the shipped data) — this should never trigger. If a future
      // data change ever produced a cycle, dump what's left in a stable
      // order rather than looping forever, so the checklist degrades to
      // "slightly mis-ordered" instead of hanging the game.
      order.push(...[...remaining].sort((a, b) => sortKey(a).localeCompare(sortKey(b))));
      break;
    }
    for (const id of ready) { order.push(id); remaining.delete(id); }
  }
  return order;
}

// ---------------------------------------------------------------------------
// THE REVERSE-PLANNER. From the active goal set, produces ONE merged,
// ordered, deduplicated step list plus per-goal satisfaction — CONCEPT.md:
// "generates an ordered step list ... a topological walk over the recipe +
// research-gate dependency graph."
//
// Algorithm: for every active goal, resolve it to a production category,
// then BFS BACKWARD over the techtree graph's `incoming` adjacency starting
// at that category's node — this collects exactly "the production category,
// its facility building, the research tech (if gated) + ITS prerequisite
// buildings/techs, and the resource inputs" in one pass, because
// game/techtree.js already wired every one of those edges for real gameplay-
// gating reasons (see this file's header comment). Every goal's collected
// node-id set is UNIONED into one `stepNodeIds` Set — Set union is exactly
// the dedup the task asks for: a building/tech/resource shared by two goals
// (e.g. two aircraft-category units, or a shared Research Bureau) only ever
// occupies one slot in the final list.
export function computePlan(goalState, ctx) {
  const graph = getTechTreeGraph();
  const { nodesById, incoming } = graph;
  const stepNodeIds = new Set();
  const goals = [];

  for (const goalId of goalState.goals) {
    const goalNode = nodesById.get(goalId);
    if (!goalNode) continue; // stale id (e.g. a future data change removed the node) — skip defensively
    const catId = categoryIdForGoal(goalNode);
    const catNodeId = catId ? `category:${catId}` : null;
    if (!catId || !nodesById.has(catNodeId)) continue;

    // Per-goal ancestor set — kept SEPARATE from the merged stepNodeIds
    // below so a goal's own "am I fully done" check (satisfied, just
    // below) reflects EVERY prerequisite THIS goal actually depends on
    // (including its resource inputs), not just whether its category
    // happens to be live — a goal sharing its facility with an unfinished
    // sibling goal must not read as satisfied on the sibling's back.
    const ownAncestors = new Set([catNodeId]);
    const queue = [catNodeId];
    while (queue.length) {
      const id = queue.shift();
      for (const e of incoming.get(id) || []) {
        if (ownAncestors.has(e.from)) continue;
        ownAncestors.add(e.from);
        queue.push(e.from);
      }
    }
    for (const id of ownAncestors) stepNodeIds.add(id); // union into the merged/deduped step list

    // A goal is fully satisfied (the "we're making X now" payoff) only once
    // EVERY node it depends on reads satisfied — resources included, not
    // just "the facility is standing" — matching the checklist's own
    // per-step ticks one-for-one, per nodeSatisfied above.
    const satisfied = [...ownAncestors].every(id => nodeSatisfied(nodesById.get(id), ctx));
    goals.push({ goalId, node: goalNode, catId, catNodeId, satisfied });
  }

  const order = topoOrder(stepNodeIds, graph);
  const steps = order.map(id => stepFor(id, ctx, graph));
  return { steps, goals };
}
