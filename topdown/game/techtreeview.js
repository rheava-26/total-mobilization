// INTERACTIVE TECH/PRODUCTION TREE — VIEW (CONCEPT.md's settled "Interactive
// in-game tech/production tree (direction)" paragraph, "ICONIZED (settled)"
// sentence: "nodes are ICONS, not text boxes — simple monochrome vector
// GLYPHS drawn in code ... tinted by the game palette and by node STATE ...
// with hover -> a tooltip/card revealing the specifics"). This module owns
// EVERYTHING visual: the dedicated pan/zoom canvas, the icon glyph library,
// palette tinting by live state, hit-testing, and the hover tooltip. It reads
// game/techtree.js for the graph (data) and per-node live state — it never
// derives graph structure itself, so the two concerns (what the graph IS vs.
// how it's DRAWN) stay cleanly split, same spirit as engine/renderer.js
// (drawing) vs. game/*.js (data) throughout this codebase.
//
// Camera math is lifted directly from engine/renderer.js's own
// attachCameraControls (drag-to-pan, wheel-to-zoom) — this view gets its own
// canvas + its own independent {x,y,zoom} camera object, not a mode bolted
// onto the main game camera, so panning the tree never fights panning the
// battlefield.
import { attachCameraControls } from '../engine/renderer.js';
import { getTechTreeGraph, computeNodeState, TIER_LABELS, LAYOUT } from './techtree.js';
import { BUILDING_DEFS } from './buildings.js';
import { TECH_DEFS } from './research.js';
import { RESOURCE_DEFS } from './resources.js';
import { PRODUCTION_DEFS } from './production.js';

// ---------------------------------------------------------------------------
// PALETTE — the same five-state vocabulary computeNodeState() returns for
// every node kind, mapped onto the game's existing HUD colors (cyan/green/
// amber/muted — see index.html's #econHud/#techPanel rules) so this view
// reads as part of the same game, not a bolted-on tool.
//
// POLISH FIX (unlocked vs built were near-indistinguishable at a glance —
// both landed in the same teal/green hue band): the four player-facing
// states are now pulled apart on TWO axes at once, not just hue, so the
// distinction survives both a quick glance and color-blindness:
//   locked        — muted grey, thin ring, low-alpha fill (gate not met).
//   researchable  — cyan, filled solid (gate satisfiable now — draws the
//                   eye the way index.html's cyan accents do elsewhere).
//   available      — same cyan bucket as researchable (see the
//                   'available' comment on game/techtree.js's buildingState);
//                   kept as a distinct KEY only so a future palette split is
//                   a one-line change, not a re-plumb.
//   active         — amber, filled (a research project or construction in
//                   progress — matches #techPanel's .techStatus amber).
//   unlocked       — a distinct PERIWINKLE BLUE (hue pulled well away from
//                   built's green, not just a lighter shade of it), drawn
//                   mostly HOLLOW (low fill alpha, thicker ring) — an
//                   "outline" reads as *pending* on sight, independent of
//                   color, so this doesn't rely on hue alone (colorblind-
//                   friendly form cue, per the task's suggestion).
//   built          — the game's established success-green (#6dffb0, same
//                   as #econHud b / #card b / .techHeader b everywhere
//                   else in the HUD), drawn SOLID/filled at high alpha —
//                   filled-in reads as *done*, the opposite form cue from
//                   unlocked's outline.
const PALETTE = {
  locked:       { icon: '#5b7086', ring: '#3a4d61', fill: 'rgba(30,42,56,0.45)',  edge: 'rgba(90,110,130,0.14)' },
  researchable: { icon: '#5fd0ff', ring: '#3a8fd0', fill: 'rgba(20,45,66,0.60)',  edge: 'rgba(95,208,255,0.30)' },
  available:    { icon: '#5fd0ff', ring: '#3a8fd0', fill: 'rgba(20,45,66,0.60)',  edge: 'rgba(95,208,255,0.30)' },
  active:       { icon: '#ffcf5c', ring: '#c99a3a', fill: 'rgba(66,52,20,0.60)',  edge: 'rgba(255,207,92,0.40)' },
  unlocked:     { icon: '#8fb0ff', ring: '#5b7fe6', fill: 'rgba(24,30,58,0.20)',  edge: 'rgba(143,176,255,0.32)', ringWidth: 2.4 },
  built:        { icon: '#6dffb0', ring: '#3fae76', fill: 'rgba(16,55,38,0.80)', edge: 'rgba(109,255,176,0.52)' },
};

// Logical (pre-camera-scale) sizes — same coordinate space as
// game/techtree.js's LAYOUT.COL_WIDTH/ROW_HEIGHT (260x74), so a node's icon
// footprint is comfortably smaller than its row/column pitch at zoom 1x and
// stays proportionally clear of its neighbors at any zoom (this is the "no
// overlaps" guarantee — spacing and icon size share one coordinate system).
const NODE_RADIUS = 20;
const ICON_SIZE = 12; // half-extent glyph paths are drawn within

// ---------------------------------------------------------------------------
// ICON GLYPHS — simple monochrome vector silhouettes, one draw function per
// glyph name. Each function assumes ctx is already translated to the node's
// SCREEN center and that fillStyle/strokeStyle/lineWidth are already set by
// the caller (drawNode below) — a glyph function only builds paths, it never
// touches color state, so palette tinting is entirely the caller's job and
// every glyph automatically respects live-state tinting for free.
const ICON_GLYPHS = {
  // --- resources ---
  oreChunk(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(-s * 0.7, -s * 0.1); ctx.lineTo(-s * 0.25, -s * 0.85); ctx.lineTo(s * 0.4, -s * 0.6);
    ctx.lineTo(s * 0.75, s * 0.15); ctx.lineTo(s * 0.3, s * 0.8); ctx.lineTo(-s * 0.55, s * 0.7);
    ctx.closePath(); ctx.fill(); ctx.stroke();
  },
  oilDrop(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(0, -s * 0.95);
    ctx.bezierCurveTo(s * 0.7, -s * 0.1, s * 0.62, s * 0.85, 0, s * 0.85);
    ctx.bezierCurveTo(-s * 0.62, s * 0.85, -s * 0.7, -s * 0.1, 0, -s * 0.95);
    ctx.closePath(); ctx.fill(); ctx.stroke();
  },
  // --- buildings ---
  factory(ctx, s) {
    ctx.beginPath();
    ctx.rect(-s * 0.85, -s * 0.05, s * 1.7, s * 0.95);
    ctx.moveTo(-s * 0.85, -s * 0.05); ctx.lineTo(-s * 0.4, -s * 0.65); ctx.lineTo(0, -s * 0.05);
    ctx.moveTo(0, -s * 0.05); ctx.lineTo(s * 0.4, -s * 0.65); ctx.lineTo(s * 0.85, -s * 0.05);
    ctx.stroke();
    ctx.strokeRect(-s * 0.85, -s * 0.05, s * 1.7, s * 0.95);
    // chimney
    ctx.strokeRect(s * 0.4, -s * 1.05, s * 0.22, s * 0.55);
  },
  flask(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(-s * 0.22, -s * 0.95); ctx.lineTo(-s * 0.22, -s * 0.25);
    ctx.lineTo(-s * 0.75, s * 0.7); ctx.quadraticCurveTo(-s * 0.75, s * 0.95, -s * 0.4, s * 0.95);
    ctx.lineTo(s * 0.4, s * 0.95); ctx.quadraticCurveTo(s * 0.75, s * 0.95, s * 0.75, s * 0.7);
    ctx.lineTo(s * 0.22, -s * 0.25); ctx.lineTo(s * 0.22, -s * 0.95);
    ctx.stroke();
    ctx.moveTo(-s * 0.35, -s * 0.95); ctx.lineTo(s * 0.35, -s * 0.95);
    ctx.beginPath(); ctx.moveTo(-s * 0.35, -s * 0.95); ctx.lineTo(s * 0.35, -s * 0.95); ctx.stroke();
    // liquid fill line
    ctx.beginPath(); ctx.moveTo(-s * 0.55, s * 0.25); ctx.lineTo(s * 0.55, s * 0.25); ctx.stroke();
  },
  radar(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(0, s * 0.9); ctx.lineTo(0, s * 0.1);
    ctx.moveTo(-s * 0.35, s * 0.9); ctx.lineTo(s * 0.35, s * 0.9);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(0, s * 0.15, s * 0.75, Math.PI * 1.05, Math.PI * 1.85);
    ctx.stroke();
    ctx.beginPath(); ctx.arc(-s * 0.1, -s * 0.55, s * 0.1, 0, 6.28); ctx.fill();
  },
  turret(ctx, s) {
    ctx.beginPath(); ctx.arc(0, s * 0.3, s * 0.55, Math.PI, 0); ctx.fill(); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(0, -s * 0.1); ctx.lineTo(s * 0.85, -s * 0.45); ctx.stroke();
    ctx.beginPath(); ctx.rect(-s * 0.8, s * 0.3, s * 1.6, s * 0.22); ctx.stroke();
  },
  mine(ctx, s) {
    // crossed pickaxe
    ctx.beginPath();
    ctx.moveTo(-s * 0.75, -s * 0.75); ctx.lineTo(s * 0.75, s * 0.75);
    ctx.moveTo(-s * 0.75, s * 0.75); ctx.lineTo(s * 0.75, -s * 0.75);
    ctx.stroke();
    ctx.beginPath(); ctx.moveTo(-s * 0.75, -s * 0.75); ctx.lineTo(-s * 0.45, -s * 0.9); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(s * 0.75, -s * 0.75); ctx.lineTo(s * 0.45, -s * 0.9); ctx.stroke();
  },
  barracks(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(-s * 0.85, s * 0.85); ctx.lineTo(0, -s * 0.85); ctx.lineTo(s * 0.85, s * 0.85);
    ctx.closePath(); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(0, -s * 0.85); ctx.lineTo(0, s * 0.85); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(-s * 0.3, s * 0.85); ctx.lineTo(-s * 0.05, s * 0.15); ctx.lineTo(s * 0.2, s * 0.85); ctx.stroke();
  },
  depot(ctx, s) {
    ctx.strokeRect(-s * 0.8, -s * 0.65, s * 1.6, s * 1.3);
    ctx.beginPath();
    ctx.moveTo(-s * 0.8, -s * 0.65); ctx.lineTo(s * 0.8, s * 0.65);
    ctx.moveTo(s * 0.8, -s * 0.65); ctx.lineTo(-s * 0.8, s * 0.65);
    ctx.stroke();
  },
  // --- production line (categories) ---
  gear(ctx, s) {
    const teeth = 8, rOuter = s * 0.9, rInner = s * 0.6, rHole = s * 0.28;
    ctx.beginPath();
    for (let i = 0; i < teeth * 2; i++) {
      const a = (i / (teeth * 2)) * Math.PI * 2;
      const r = i % 2 === 0 ? rOuter : rInner;
      const x = Math.cos(a) * r, y = Math.sin(a) * r;
      if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    }
    ctx.closePath(); ctx.stroke();
    ctx.beginPath(); ctx.arc(0, 0, rHole, 0, 6.28); ctx.stroke();
  },
  // --- units, by production category ---
  tank(ctx, s) {
    ctx.strokeRect(-s * 0.85, -s * 0.15, s * 1.7, s * 0.75);
    ctx.beginPath(); ctx.arc(0, -s * 0.15, s * 0.45, Math.PI, 0); ctx.fill(); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(0, -s * 0.35); ctx.lineTo(s * 0.9, -s * 0.55); ctx.stroke();
    ctx.beginPath();
    for (let i = -3; i <= 3; i++) { ctx.moveTo(i * s * 0.24, s * 0.6); ctx.lineTo(i * s * 0.24, s * 0.75); }
    ctx.stroke();
  },
  aircraft(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(0, -s * 0.95); ctx.lineTo(s * 0.18, -s * 0.1); ctx.lineTo(s * 0.95, s * 0.55);
    ctx.lineTo(s * 0.14, s * 0.3); ctx.lineTo(s * 0.1, s * 0.9); ctx.lineTo(0, s * 0.68);
    ctx.lineTo(-s * 0.1, s * 0.9); ctx.lineTo(-s * 0.14, s * 0.3); ctx.lineTo(-s * 0.95, s * 0.55);
    ctx.lineTo(-s * 0.18, -s * 0.1); ctx.closePath();
    ctx.fill(); ctx.stroke();
  },
  ship(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(-s * 0.85, s * 0.25); ctx.quadraticCurveTo(0, s * 0.75, s * 0.85, s * 0.25);
    ctx.lineTo(s * 0.6, s * 0.55); ctx.lineTo(-s * 0.6, s * 0.55); ctx.closePath();
    ctx.fill(); ctx.stroke();
    ctx.strokeRect(-s * 0.28, -s * 0.35, s * 0.56, s * 0.6);
    ctx.beginPath(); ctx.moveTo(0, -s * 0.35); ctx.lineTo(0, -s * 0.85); ctx.stroke();
  },
  artillery(ctx, s) {
    ctx.beginPath(); ctx.arc(-s * 0.45, s * 0.55, s * 0.28, 0, 6.28); ctx.stroke();
    ctx.beginPath(); ctx.arc(s * 0.45, s * 0.55, s * 0.28, 0, 6.28); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(-s * 0.5, s * 0.3); ctx.lineTo(s * 0.5, s * 0.3); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(-s * 0.1, s * 0.1); ctx.lineTo(s * 0.9, -s * 0.75); ctx.stroke();
  },
  drone(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(0, -s * 0.35); ctx.lineTo(s * 0.35, 0); ctx.lineTo(0, s * 0.35); ctx.lineTo(-s * 0.35, 0);
    ctx.closePath(); ctx.fill(); ctx.stroke();
    const arms = [[-1, -1], [1, -1], [-1, 1], [1, 1]];
    for (const [dx, dy] of arms) {
      ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(dx * s * 0.8, dy * s * 0.8); ctx.stroke();
      ctx.beginPath(); ctx.arc(dx * s * 0.8, dy * s * 0.8, s * 0.2, 0, 6.28); ctx.stroke();
    }
  },
  missile(ctx, s) {
    ctx.beginPath();
    ctx.moveTo(0, -s * 0.95); ctx.lineTo(s * 0.24, -s * 0.35); ctx.lineTo(s * 0.24, s * 0.65);
    ctx.lineTo(s * 0.5, s * 0.95); ctx.lineTo(-s * 0.5, s * 0.95); ctx.lineTo(-s * 0.24, s * 0.65);
    ctx.lineTo(-s * 0.24, -s * 0.35); ctx.closePath();
    ctx.fill(); ctx.stroke();
  },
  interceptor(ctx, s) {
    ICON_GLYPHS.missile(ctx, s * 0.75);
    ctx.beginPath(); ctx.arc(0, 0, s * 1.05, 0, 6.28); ctx.stroke();
  },
  guided(ctx, s) {
    ICON_GLYPHS.missile(ctx, s * 0.85);
    ctx.beginPath(); ctx.arc(0, -s * 0.95, s * 0.32, 0, 6.28); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(-s * 0.32, -s * 0.95); ctx.lineTo(s * 0.32, -s * 0.95); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(0, -s * 1.27); ctx.lineTo(0, -s * 0.63); ctx.stroke();
  },
  jammer(ctx, s) {
    ctx.beginPath(); ctx.moveTo(0, s * 0.9); ctx.lineTo(0, -s * 0.3); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(-s * 0.4, s * 0.9); ctx.lineTo(s * 0.4, s * 0.9); ctx.stroke();
    for (const r of [0.4, 0.7, 1.0]) {
      ctx.beginPath(); ctx.arc(0, -s * 0.3, s * r, Math.PI * 1.15, Math.PI * 1.85); ctx.stroke();
    }
  },
};

// Structural glyph selection for a BUILDING node — derived from attributes
// already on the def (weapon/vision/econ/requiresDeposit), NOT from a
// hand-listed per-building-name table, so a new BUILDING_DEFS entry gets a
// sensible icon automatically. researchBureau is the one deliberate name
// exception (see the comment on it below) because it is structurally
// identical to every other unarmed low-vision building — nothing about its
// DATA distinguishes "the lab" from a generic depot, only its role does.
function glyphForBuilding(key, def) {
  if (key === 'researchBureau') return 'flask'; // the one semantic exception — see comment above
  if (def.weapon) return 'turret';
  if (def.requiresDeposit) return 'mine';
  if (def.vision >= 400) return 'radar'; // radar station's vision (780) dwarfs every other building's
  if (def.econ && def.econ.manpowerRate !== undefined) return 'barracks';
  if (def.econ && (def.econ.icCapBonus !== undefined || def.econ.manpowerCapBonus !== undefined)) return 'depot';
  return 'factory'; // default: every production-style facility (factory itself, and every *Works/*Plant/shipyard/ammunitionPlant)
}

// Structural glyph selection for a RESOURCE node — RESOURCE_DEFS' own
// `coastal` flag (currently true only for oil) is the one bit of data that
// distinguishes "a liquid pumped from marsh/shallows" from "a solid chunk
// dug from hills/mountains" — reusing it means a future coastal resource
// automatically draws as a drop with no icon-selection code change.
function glyphForResource(def) { return def.coastal ? 'oilDrop' : 'oreChunk'; }

// Glyph selection for a UNIT node is keyed off which PRODUCTION_DEFS
// CATEGORY produced it (node.category, set by techtree.js's buildGraph) —
// this is a curated icon-per-category palette (CONCEPT.md itself names the
// intended glyph set: "factory/tank/drone/flask/…"), with a generic
// fallback so a brand-new category some future data change adds still
// renders something legible instead of breaking.
const CATEGORY_UNIT_ICON = {
  tanks: 'tank', aircraft: 'aircraft', warships: 'ship', artillery: 'artillery',
  drones: 'drone', missiles: 'missile', hypersonics: 'missile',
  airDefense: 'interceptor', guidedWeapons: 'guided', support: 'jammer',
};
function glyphForUnit(node) { return CATEGORY_UNIT_ICON[node.category] || 'gear'; }

function glyphFor(node) {
  if (node.kind === 'resource') return glyphForResource(node.def);
  if (node.kind === 'building') return glyphForBuilding(node.key, node.def);
  if (node.kind === 'tech') return 'flask';
  if (node.kind === 'category') return 'gear';
  if (node.kind === 'unit') return glyphForUnit(node);
  return 'gear';
}

// ---------------------------------------------------------------------------
// FRAME SHAPES — one distinct outer silhouette per NODE KIND (independent of
// the glyph inside), so the five kinds are tellable apart at a glance before
// even reading the icon: hexagon (resource), rounded square (building),
// diamond (tech — "a research program," visually distinct from a building
// even though both use the flask glyph), circle (category — "a process"),
// octagon (unit). All strokes use the caller's already-set palette colors.
function frameHexagon(ctx, r) {
  ctx.beginPath();
  for (let i = 0; i < 6; i++) {
    const a = Math.PI / 6 + i * (Math.PI / 3);
    const x = Math.cos(a) * r, y = Math.sin(a) * r;
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.closePath();
}
function frameRoundedSquare(ctx, r) {
  const k = r * 0.9, rad = r * 0.28;
  ctx.beginPath();
  ctx.moveTo(-k + rad, -k);
  ctx.arcTo(k, -k, k, k, rad);
  ctx.arcTo(k, k, -k, k, rad);
  ctx.arcTo(-k, k, -k, -k, rad);
  ctx.arcTo(-k, -k, k, -k, rad);
  ctx.closePath();
}
function frameDiamond(ctx, r) {
  ctx.beginPath();
  ctx.moveTo(0, -r); ctx.lineTo(r, 0); ctx.lineTo(0, r); ctx.lineTo(-r, 0);
  ctx.closePath();
}
function frameCircle(ctx, r) { ctx.beginPath(); ctx.arc(0, 0, r, 0, 6.28); }

// Truncate a label to fit maxWidth (assumes ctx.font is already set) so a
// long placeholder tech/building name (the [PLACEHOLDER] program names are
// the worst offenders — see game/research.js TECH_DEFS) never bleeds into
// the neighboring column's node/label at the view's default fit-to-graph
// zoom. Cheap linear trim from the end, plus an ellipsis, rather than a
// binary search — label strings here are short enough (a few dozen chars)
// that the loop is never more than a handful of iterations.
function truncateToWidth(ctx, text, maxWidth) {
  if (ctx.measureText(text).width <= maxWidth) return text;
  let lo = 0, hi = text.length;
  while (lo < hi) {
    const mid = (lo + hi + 1) >> 1;
    const candidate = text.slice(0, mid) + '…';
    if (ctx.measureText(candidate).width <= maxWidth) lo = mid; else hi = mid - 1;
  }
  return lo > 0 ? text.slice(0, lo) + '…' : '…';
}
function frameOctagon(ctx, r) {
  ctx.beginPath();
  for (let i = 0; i < 8; i++) {
    const a = Math.PI / 8 + i * (Math.PI / 4);
    const x = Math.cos(a) * r, y = Math.sin(a) * r;
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.closePath();
}
const FRAME_BY_KIND = {
  resource: frameHexagon, building: frameRoundedSquare, tech: frameDiamond,
  category: frameCircle, unit: frameOctagon,
};

// ---------------------------------------------------------------------------
export function createTechTreeView(canvas, getLiveCtx) {
  const ctx = canvas.getContext('2d');
  const cam = { x: 0, y: 0, zoom: 1 };
  let open = false;
  let fitted = false;
  let hovered = null;
  let lastPointer = { x: 0, y: 0 };

  function resize() {
    canvas.width = Math.max(1, canvas.clientWidth) * devicePixelRatio;
    canvas.height = Math.max(1, canvas.clientHeight) * devicePixelRatio;
  }
  addEventListener('resize', () => { if (open) resize(); });

  function worldToScreen(wx, wy) {
    return [
      (wx - cam.x) * cam.zoom * devicePixelRatio + canvas.width / 2,
      (wy - cam.y) * cam.zoom * devicePixelRatio + canvas.height / 2,
    ];
  }
  function screenToWorld(sx, sy) {
    return [
      (sx * devicePixelRatio - canvas.width / 2) / (cam.zoom * devicePixelRatio) + cam.x,
      (sy * devicePixelRatio - canvas.height / 2) / (cam.zoom * devicePixelRatio) + cam.y,
    ];
  }

  // hit-test: nearest node whose screen-space radius contains the point.
  // Exposed publicly (both as a plain function and via window.__debug in
  // main.js) so a headless test can verify hover WITHOUT synthetic mouse
  // events — call nodeAtPoint(x,y) with the same client coordinates a real
  // pointer would report and read back the node it resolves to.
  function nodeAtPoint(sx, sy) {
    const { nodes } = getTechTreeGraph();
    const screenR = NODE_RADIUS * cam.zoom * devicePixelRatio;
    let best = null, bestD = (screenR * 1.3) ** 2;
    for (const n of nodes) {
      const [nx, ny] = worldToScreen(n.x, n.y);
      // convert candidate node's screen pos from device px (worldToScreen's
      // units) to the CSS px the sx/sy hit-test args arrive in
      const csx = nx / devicePixelRatio, csy = ny / devicePixelRatio;
      const d = (csx - sx) ** 2 + (csy - sy) ** 2;
      if (d < bestD) { bestD = d; best = n; }
    }
    return best;
  }

  canvas.addEventListener('mousemove', e => {
    lastPointer = { x: e.clientX, y: e.clientY };
    hovered = nodeAtPoint(e.clientX, e.clientY);
  });
  attachCameraControls(canvas, cam, { isEntityHit: () => !!hovered, minZoom: 0.15, maxZoom: 3.5 });

  function fitToGraph() {
    const { nodes } = getTechTreeGraph();
    if (!nodes.length) return;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of nodes) {
      minX = Math.min(minX, n.x); maxX = Math.max(maxX, n.x);
      minY = Math.min(minY, n.y); maxY = Math.max(maxY, n.y);
    }
    const pad = 140;
    const w = (maxX - minX) + pad * 2, h = (maxY - minY) + pad * 2;
    cam.x = (minX + maxX) / 2;
    cam.y = (minY + maxY) / 2;
    cam.zoom = Math.max(0.15, Math.min(1.1, Math.min(canvas.clientWidth / w, canvas.clientHeight / h)));
  }

  function show() {
    open = true;
    canvas.style.display = 'block';
    resize();
    if (!fitted) { fitToGraph(); fitted = true; }
  }
  function hide() { open = false; canvas.style.display = 'none'; hovered = null; }

  // -------------------------------------------------------------------------
  function drawBadge(kind, pal, r) {
    if (!kind) return;
    ctx.save();
    ctx.translate(r * 0.72, -r * 0.72);
    ctx.fillStyle = '#0a1420';
    ctx.beginPath(); ctx.arc(0, 0, r * 0.36, 0, 6.28); ctx.fill();
    ctx.strokeStyle = pal.icon; ctx.lineWidth = Math.max(1, r * 0.06);
    ctx.beginPath(); ctx.arc(0, 0, r * 0.36, 0, 6.28); ctx.stroke();
    ctx.strokeStyle = pal.icon; ctx.fillStyle = pal.icon;
    const k = r * 0.36;
    if (kind === 'check') {
      ctx.beginPath(); ctx.moveTo(-k * 0.45, 0); ctx.lineTo(-k * 0.1, k * 0.4); ctx.lineTo(k * 0.5, -k * 0.4); ctx.stroke();
    } else if (kind === 'lock') {
      ctx.strokeRect(-k * 0.35, -k * 0.05, k * 0.7, k * 0.55);
      ctx.beginPath(); ctx.arc(0, -k * 0.15, k * 0.32, Math.PI, 0); ctx.stroke();
    } else if (kind === 'clock') {
      ctx.beginPath(); ctx.arc(0, 0, k * 0.5, 0, 6.28); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(0, -k * 0.4); ctx.moveTo(0, 0); ctx.lineTo(k * 0.3, k * 0.1); ctx.stroke();
    }
    ctx.restore();
  }

  function drawNode(n, stateInfo, isHovered) {
    const [sx, sy] = worldToScreen(n.x, n.y);
    const r = NODE_RADIUS * cam.zoom * devicePixelRatio;
    if (sx < -r * 2 || sx > canvas.width + r * 2 || sy < -r * 2 || sy > canvas.height + r * 2) return false;
    const pal = PALETTE[stateInfo.state] || PALETTE.locked;
    ctx.save();
    ctx.translate(sx, sy);
    const frameFn = FRAME_BY_KIND[n.kind] || frameCircle;
    frameFn(ctx, r);
    ctx.fillStyle = pal.fill;
    ctx.fill();
    ctx.strokeStyle = isHovered ? '#ffffff' : pal.ring;
    // unlocked's PALETTE entry sets a wider ringWidth (see the PALETTE
    // comment above) so its near-hollow fill still reads as a deliberate
    // "outline" state rather than a thin, faint circle.
    ctx.lineWidth = isHovered ? 2.4 : (pal.ringWidth || 1.6);
    ctx.stroke();

    const iconScale = r / NODE_RADIUS; // convert the fixed ICON_SIZE coordinate space to this node's on-screen radius
    ctx.strokeStyle = pal.icon;
    ctx.fillStyle = pal.icon;
    ctx.lineWidth = Math.max(1, 1.6 * (cam.zoom * devicePixelRatio) ** 0.35);
    const glyphName = glyphFor(n);
    (ICON_GLYPHS[glyphName] || ICON_GLYPHS.gear)(ctx, ICON_SIZE * iconScale);

    drawBadge(stateInfo.badge, pal, r);
    ctx.restore();

    // label — screen space, clamped size, only drawn once icons are legibly
    // large (mirrors engine/renderer.js's own zoom-gated label convention).
    if (r > 8) {
      const fontSize = Math.max(9, Math.min(13, r * 0.5));
      ctx.font = `${fontSize}px Consolas, monospace`;
      ctx.textAlign = 'center';
      // Cap label width to ~90% of the column pitch (converted to this
      // frame's screen scale) so a long placeholder name (the [PLACEHOLDER]
      // research program strings run 30+ chars) truncates with an ellipsis
      // instead of visually bleeding into the next tier's column — see
      // truncateToWidth above.
      const colPitchPx = LAYOUT.COL_WIDTH * cam.zoom * devicePixelRatio;
      const label = truncateToWidth(ctx, n.name, colPitchPx * 0.9);
      ctx.lineWidth = 2.5;
      ctx.strokeStyle = 'rgba(4,8,14,0.85)';
      ctx.strokeText(label, sx, sy + r + fontSize + 2);
      ctx.fillStyle = isHovered ? '#ffffff' : '#cfe6f5';
      ctx.fillText(label, sx, sy + r + fontSize + 2);
      ctx.textAlign = 'left';
    }
    return true;
  }

  // POLISH FIX (the Resources->Facilities hairball): with 4 resource nodes
  // each fanning edges out to every facility that consumes them, drawing
  // every edge at equal, always-on weight produces a dense tangle on the
  // left no amount of curving alone fixes — the player only ever needs to
  // read ONE node's connections at a time anyway. So edges default to
  // faint/thin (a big cut from the old always-on alphas baked into
  // PALETTE[...].edge above), and hovering a node brightens just its own
  // incident edges while dimming everything else near-invisible. This is
  // the highest-value declutter per the task brief; the barycenter reorder
  // pass in techtree.js's layoutGraph is the complementary structural fix
  // (fewer crossings to begin with), and the bezier curve below (already
  // in place from the prior pass) keeps even the emphasized edges legible.
  const EDGE_DIM_ALPHA = 0.10; // near-invisible but still technically present, not a hard cut (helps "where does this even go" scans)
  const EDGE_EMPHASIS_ALPHA = 1;
  const EDGE_DEFAULT_ALPHA = 0.62; // faint-but-present baseline when nothing is hovered

  function drawEdges(stateById, hoveredNode) {
    const { edges, nodesById, outgoing, incoming } = getTechTreeGraph();
    // Precompute the hovered node's own incident edges (both directions) —
    // techtree.js already builds outgoing/incoming adjacency indices for
    // exactly this "what touches this node" question, so no per-frame scan
    // of the flat edge list is needed to answer it.
    let hoveredEdges = null;
    if (hoveredNode) {
      hoveredEdges = new Set();
      for (const e of outgoing.get(hoveredNode.id) || []) hoveredEdges.add(e);
      for (const e of incoming.get(hoveredNode.id) || []) hoveredEdges.add(e);
    }
    for (const e of edges) {
      const from = nodesById.get(e.from), to = nodesById.get(e.to);
      const toState = stateById.get(e.to);
      const pal = PALETTE[toState.state] || PALETTE.locked;
      const [sx1, sy1] = worldToScreen(from.x, from.y);
      const [sx2, sy2] = worldToScreen(to.x, to.y);
      if (Math.max(sx1, sx2) < 0 || Math.min(sx1, sx2) > canvas.width) continue;
      const emphasized = hoveredEdges && hoveredEdges.has(e);
      const dimmed = hoveredEdges && !emphasized;
      ctx.strokeStyle = pal.edge;
      ctx.globalAlpha = dimmed ? EDGE_DIM_ALPHA : (emphasized ? EDGE_EMPHASIS_ALPHA : EDGE_DEFAULT_ALPHA);
      ctx.lineWidth = (toState.state === 'locked' ? 1 : 1.7) * (emphasized ? 1.7 : 1) * Math.max(0.5, Math.min(1.6, cam.zoom));
      ctx.beginPath();
      // gentle S-curve (single control-point bezier) rather than a straight
      // line — with several edges converging on one node from a scattered
      // column this keeps near-parallel edges visually separable instead of
      // bundling into one thick line.
      const mx = (sx1 + sx2) / 2;
      ctx.moveTo(sx1, sy1);
      ctx.bezierCurveTo(mx, sy1, mx, sy2, sx2, sy2);
      ctx.stroke();
    }
    ctx.globalAlpha = 1; // reset — drawNode/drawTierHeaders/drawTooltip below all assume opaque default
  }

  function drawTierHeaders() {
    ctx.save();
    ctx.font = 'bold 12px Consolas, monospace';
    ctx.fillStyle = 'rgba(191,233,255,0.55)';
    ctx.textAlign = 'center';
    for (let tier = 0; tier < TIER_LABELS.length; tier++) {
      const [sx] = worldToScreen(tier * LAYOUT.COL_WIDTH, 0);
      if (sx < -50 || sx > canvas.width + 50) continue;
      ctx.fillText(TIER_LABELS[tier], sx, 26 * devicePixelRatio);
    }
    ctx.textAlign = 'left';
    ctx.restore();
  }

  // -------------------------------------------------------------------------
  // TOOLTIP CONTENT — kind-specific, reusing the detail payload
  // computeNodeState() already computed this frame. Mirrors the statcard's
  // "name + live stats" shape (game/statcard.js) but as plain lines (drawn
  // on canvas, not a DOM element, to stay visually cohesive with the rest of
  // this canvas-drawn overlay per the task's stated canvas preference).
  function resourceCostLines(costObj) {
    return Object.entries(costObj || {}).map(([rid, amt]) => `  ${RESOURCE_DEFS[rid] ? RESOURCE_DEFS[rid].name : rid}: ${amt}`);
  }
  function tooltipLines(n, st) {
    const lines = [n.name];
    if (n.kind === 'resource') {
      lines.push(`Kind: Resource`);
      lines.push(`Stockpile: ${st.detail.amount.toFixed(1)}  (+${st.detail.rate.toFixed(2)}/s)`);
      lines.push(`Terrain: ${(n.def.terrainAffinity || []).join(', ')}${n.def.coastal ? ' (coastal)' : ''}`);
    } else if (n.kind === 'building') {
      const d = n.def;
      lines.push(`Kind: Building`);
      lines.push(`State: ${st.state}${st.detail.count ? ` (x${st.detail.count})` : ''}${st.state === 'active' ? ` — ${Math.round(st.detail.progress * 100)}%` : ''}`);
      const cost = d.cost || {};
      lines.push(`Cost: ${cost.ic || 0} IC${cost.manpower ? ` + ${cost.manpower} manpower` : ''}   Build: ${d.buildTime}s`);
      lines.push(`Footprint: ${d.footprint.w}x${d.footprint.h}   Vision: ${d.vision}`);
      if (d.weapon) lines.push(`Weapon: ${d.weapon}  dmg ${d.dmg}  range ${d.range}`);
      if (d.econ) lines.push(`Econ: ${Object.entries(d.econ).map(([k, v]) => `${k} ${v}`).join(', ')}`);
      if (d.requiresDeposit) lines.push(`Requires a resource deposit to site.`);
    } else if (n.kind === 'tech') {
      const d = n.def;
      lines.push(`Kind: Research Program`);
      lines.push(d.blurb);
      lines.push(`State: ${st.state}${st.state === 'active' ? ` — ${Math.round(st.detail.progress * 100)}%` : ''}`);
      lines.push(`Buildings: ${(d.prerequisiteBuildings || []).map(k => BUILDING_DEFS[k] ? BUILDING_DEFS[k].name : k).join(', ') || 'none'}`);
      if ((d.prerequisiteTechs || []).length) lines.push(`Techs: ${d.prerequisiteTechs.map(t => TECH_DEFS[t] ? TECH_DEFS[t].name : t).join(', ')}`);
      lines.push(`Cost: ${d.icCost} IC`, ...resourceCostLines(d.resourceCost));
      lines.push(`Research time: ${d.researchTime}s`);
      if (st.state === 'locked' && st.detail.raw) {
        const missing = [...(st.detail.raw.missingBuildings || []).map(k => BUILDING_DEFS[k].name),
          ...(st.detail.raw.missingTechs || []).map(t => TECH_DEFS[t].name)];
        if (missing.length) lines.push(`Missing: ${missing.join(', ')}`);
        else if (st.detail.raw.state === 'busy') lines.push(`Gate met — waiting on the research slot.`);
      }
    } else if (n.kind === 'category') {
      const d = n.def;
      lines.push(`Kind: Production Category`);
      lines.push(`State: ${st.state}${st.detail.producing ? ' — producing' : ''}`);
      lines.push(`Facility: ${BUILDING_DEFS[d.facility] ? BUILDING_DEFS[d.facility].name : d.facility}`);
      lines.push(`Recipe/unit:`, ...resourceCostLines(d.recipe.resourceCostPerUnit));
      lines.push(`IC/unit: ${d.recipe.icCostPerUnit}   Manpower/unit: ${d.recipe.manpowerCostPerUnit}   Time/unit: ${d.recipe.buildTimePerUnit}s`);
      if ((d.recipe.prerequisiteBuildings || []).length) lines.push(`Also needs: ${d.recipe.prerequisiteBuildings.map(k => BUILDING_DEFS[k].name).join(', ')}`);
      lines.push(`Produces: ${d.outputs.map(k => k).join(', ')}`);
    } else if (n.kind === 'unit') {
      const d = n.def;
      lines.push(`Kind: Unit — ${PRODUCTION_DEFS[n.category] ? PRODUCTION_DEFS[n.category].name : n.category}`);
      lines.push(`State: ${st.state}`);
      lines.push(`HP ${d.hp}   Armor ${d.armor}   Speed ${d.speed}`);
      lines.push(`Domain: ${d.domain}   Move class: ${d.moveClass}`);
      if (d.weapon) lines.push(`Weapon: ${d.weapon}  dmg ${d.dmg}  range ${d.range}`);
    }
    return lines;
  }

  function drawTooltip(n, st) {
    const lines = tooltipLines(n, st);
    ctx.font = '12px Consolas, monospace';
    let w = 0;
    for (const l of lines) w = Math.max(w, ctx.measureText(l).width);
    w += 24;
    const lineH = 16;
    const h = lines.length * lineH + 16;
    let x = lastPointer.x * devicePixelRatio + 22, y = lastPointer.y * devicePixelRatio - h / 2;
    x = Math.min(x, canvas.width - w - 8);
    y = Math.max(8, Math.min(y, canvas.height - h - 8));
    ctx.save();
    ctx.fillStyle = 'rgba(8,14,26,0.96)';
    ctx.strokeStyle = '#2f5f9c';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.rect(x, y, w, h);
    ctx.fill(); ctx.stroke();
    ctx.fillStyle = '#6dffb0';
    ctx.font = 'bold 12px Consolas, monospace';
    ctx.fillText(lines[0], x + 12, y + 18);
    ctx.font = '11px Consolas, monospace';
    ctx.fillStyle = '#bfe9ff';
    for (let i = 1; i < lines.length; i++) ctx.fillText(lines[i], x + 12, y + 18 + i * lineH);
    ctx.restore();
  }

  // -------------------------------------------------------------------------
  function render() {
    if (!open) return;
    const { nodes } = getTechTreeGraph();
    const liveCtx = getLiveCtx();
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = 'rgba(4,8,14,0.97)';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    const stateById = new Map();
    for (const n of nodes) stateById.set(n.id, computeNodeState(n, liveCtx));

    drawTierHeaders();
    drawEdges(stateById, hovered);
    for (const n of nodes) drawNode(n, stateById.get(n.id), n === hovered);
    if (hovered) drawTooltip(hovered, stateById.get(hovered.id));

    ctx.save();
    ctx.font = '11px Consolas, monospace';
    ctx.fillStyle = 'rgba(159,182,196,0.85)';
    ctx.fillText('G: close tree view  —  drag: pan  —  wheel: zoom  —  hover a node for specifics', 14 * devicePixelRatio, canvas.height - 14 * devicePixelRatio);
    ctx.restore();
  }

  return {
    show, hide, render, nodeAtPoint, resize,
    get isOpen() { return open; },
    get hovered() { return hovered; },
    cam,
  };
}
