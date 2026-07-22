import { RESOURCE_DEFS } from '../game/resources.js';
// WEAPON_DEFS is a pure DATA table (kind/targeting flags) — importing it here
// is the same one-directional "renderer reads game/*.js data" relationship
// RESOURCE_DEFS above already has, not a mechanics dependency: everything
// below only ever READS .kind/.canTargetAir/.canTargetGround off it to pick
// a look, never touches unit/projectile update logic (that stays entirely in
// game/units.js).
import { WEAPON_DEFS } from '../game/units.js';

// City-ownership ring colors — pulled out to a named export so main.js's
// map-legend panel (Operations Map chunk C) can swap in the exact same
// swatches instead of hardcoding a second copy of these hex values; the
// ownership-ring draw code below (drawLabels) reads this same table.
export const OWNERSHIP_COLORS = { player: '#5fd0ff', enemy: '#ff5a5a', neutral: '#8fa0ac' };

// Canvas2D top-down renderer: camera (pan/zoom), a pre-rendered terrain
// layer, and a draw-call hook the game layer feeds world-space draw
// functions into every frame.
//
// Terrain used to be redrawn tile-by-tile with fillRect every frame, which
// is fine at slice-01 scale (60x40) but cliffs on the theater map (220x150 =
// 33k tiles). Instead we paint the whole map ONCE onto an offscreen canvas
// (in map-pixel space) and every frame is just one drawImage of the visible
// window through the camera transform. Re-painted only if the map's
// `.version` changes (bumped by map.dirty(), for future terrain edits).

// deterministic per-tile hash, independent of paint order — used for color
// jitter and blob placement so the look is stable across reloads/frames
function hash2(x, y, seed) {
  let h = (x * 374761393 + y * 668265263 + seed * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h = (h ^ (h >>> 16)) >>> 0;
  return h / 4294967296;
}

function hexToRgb(hex) {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}
function lerpRgb(hexA, hexB, t) {
  const a = hexToRgb(hexA), b = hexToRgb(hexB);
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}
function lerpColor(hexA, hexB, t) {
  const [r, g, b] = lerpRgb(hexA, hexB, t);
  return `rgb(${r | 0},${g | 0},${b | 0})`;
}
function rgba(rgb, a) { return `rgba(${rgb[0] | 0},${rgb[1] | 0},${rgb[2] | 0},${a})`; }

// Continuous (non-tile-aligned) value-noise lattice, used to replace the old
// "one random jitter value per whole tile" base-color pass — see
// prerenderTerrain's pass 1 below for why that read as a checkerboard.
// Instead of hashing per-tile, we hash a grid of points spaced `cellPx`
// world-pixels apart (deliberately NOT a multiple of tileSize, so the noise
// grid never lines up with the tile grid) and bilinearly interpolate between
// the four surrounding points with a smoothstep ease. The result is a value
// that varies continuously across the whole map, including across tile
// boundaries — two adjacent same-type tiles blend into each other instead of
// jumping between two independently-rolled constants. Precomputing the
// lattice once (instead of hashing on every pixel sample) keeps the whole
// map-wide per-pixel pass cheap.
function buildNoiseLattice(worldW, worldH, cellPx, salt) {
  const cols = Math.ceil(worldW / cellPx) + 2;
  const rows = Math.ceil(worldH / cellPx) + 2;
  const g = new Float32Array(cols * rows);
  for (let y = 0; y < rows; y++) {
    for (let x = 0; x < cols; x++) g[y * cols + x] = hash2(x, y, salt);
  }
  return { g, cols, cellPx };
}
function sampleLattice(lat, wx, wy) {
  const gx = wx / lat.cellPx, gy = wy / lat.cellPx;
  // gx/gy are always >= 0 here (world-pixel coords), so a bitwise truncate
  // is a cheap equivalent to Math.floor — this runs once per octave per
  // pixel of the whole map (millions of times), so the constant-factor win
  // is worth the (safe, in-range) micro-optimization.
  const x0 = gx | 0, y0 = gy | 0;
  const fx = gx - x0, fy = gy - y0;
  const ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy); // smoothstep — avoids visible creases at lattice cell edges
  const i00 = y0 * lat.cols + x0, i10 = i00 + 1, i01 = i00 + lat.cols, i11 = i01 + 1;
  const n00 = lat.g[i00], n10 = lat.g[i10], n01 = lat.g[i01], n11 = lat.g[i11];
  const nx0 = n00 + (n10 - n00) * ux, nx1 = n01 + (n11 - n01) * ux;
  return nx0 + (nx1 - nx0) * uy;
}

// A feathered blob (radial gradient, full alpha at center fading smoothly to
// 0 at the edge) — the actual fix for "reads as hard tiles": solid
// ellipses/rects have a crisp boundary no matter how low their opacity is,
// so overlapping them still reads as polka dots. A gradient has none.
function softBlob(octx, cx, cy, rx, ry, rot, rgb, peakAlpha) {
  octx.save();
  octx.translate(cx, cy);
  octx.rotate(rot);
  octx.scale(1, ry / rx);
  const grad = octx.createRadialGradient(0, 0, 0, 0, 0, rx);
  grad.addColorStop(0, rgba(rgb, peakAlpha));
  grad.addColorStop(0.6, rgba(rgb, peakAlpha * 0.55));
  grad.addColorStop(1, rgba(rgb, 0));
  octx.fillStyle = grad;
  octx.beginPath();
  octx.arc(0, 0, rx, 0, Math.PI * 2);
  octx.fill();
  octx.restore();
}

// Paint the whole map once, in map-pixel space, onto an offscreen canvas.
// Soft/painterly look: a solid full-coverage base pass (continuous, sub-tile
// noise-driven color jitter — see pass 1 below) followed by scattered
// semi-transparent blobs that bleed across tile boundaries — including
// toward a differing neighbor's color, which is what dithers a hard edge
// into an organic transition. Water skips the blob pass (it should read
// calm) and instead gets a lightened band near the coast, but still gets the
// continuous noise base — it's the only thing standing between open ocean
// and a flat/checkered fill. All of this runs once at load; per-frame cost
// is a single drawImage.
function prerenderTerrain(map) {
  const ts = map.tileSize;
  const off = document.createElement('canvas');
  off.width = map.width * ts;
  off.height = map.height * ts;
  const octx = off.getContext('2d');

  // pass 1: solid, full-coverage base fill with color jitter. Used to be one
  // hash2() draw per tile — a single random value held constant across the
  // whole tile, then jumping to a new random value at the next tile — which
  // is exactly a checkerboard: invisible at strategic zoom where a tile is a
  // couple of screen pixels, but at max zoom each tile is a big flat square
  // with a hard-edged neighbor, and water (which skips the painterly blob
  // pass below entirely) had nothing else to hide it. Fixed by sampling two
  // octaves of the continuous noise lattice above (a coarse layer for the
  // broad color drift, a finer half-amplitude layer so close zoom still has
  // grain to look at instead of a flat gradient) at every pixel instead of
  // every tile. Neither lattice's cell size is a multiple of tileSize, so
  // the noise seams never land on a tile edge — adjacent tiles of the same
  // terrain blend into each other with no visible boundary at any zoom.
  // Written via ImageData/putImageData rather than per-pixel fillRect calls,
  // since this pass now touches every pixel of the map (millions, not the
  // ~33k tile-fills it used to be) and per-call canvas draw overhead would
  // dominate at that count.
  const noiseLat = buildNoiseLattice(off.width, off.height, ts * 0.6, 1);
  const detailLat = buildNoiseLattice(off.width, off.height, ts * 0.22, 501);
  const img = octx.createImageData(off.width, off.height);
  const data = img.data;
  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      const t = map.terrainAt(gx, gy);
      const [r0, g0, b0] = hexToRgb(t.color[0]);
      const [r1, g1, b1] = hexToRgb(t.color[1]);
      const dr = r1 - r0, dg = g1 - g0, db = b1 - b0;
      for (let ly = 0; ly < ts; ly++) {
        const wy = gy * ts + ly;
        const rowBase = wy * off.width;
        for (let lx = 0; lx < ts; lx++) {
          const wx = gx * ts + lx;
          const n = sampleLattice(noiseLat, wx, wy) * 0.72 + sampleLattice(detailLat, wx, wy) * 0.28;
          const idx = (rowBase + wx) * 4;
          data[idx] = r0 + dr * n;
          data[idx + 1] = g0 + dg * n;
          data[idx + 2] = b0 + db * n;
          data[idx + 3] = 255;
        }
      }
    }
  }
  octx.putImageData(img, 0, 0);

  // pass 2: painterly texture — feathered blobs per land tile (radial
  // gradients, not solid shapes, so they melt into their neighbors instead
  // of reading as polka dots), some bled toward a differing neighbor's color
  // to dither the boundary into an organic transition instead of a seam
  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      const t = map.terrainAt(gx, gy);
      if (t.water) continue; // sea stays calm/flat; treated separately below
      const cx = gx * ts + ts / 2, cy = gy * ts + ts / 2;
      const blobs = 4;
      for (let b = 0; b < blobs; b++) {
        const h1 = hash2(gx, gy, 10 + b), h2 = hash2(gx, gy, 20 + b);
        const h3 = hash2(gx, gy, 30 + b), h4 = hash2(gx, gy, 40 + b);
        const ox = (h2 - 0.5) * ts * 1.6, oy = (h3 - 0.5) * ts * 1.6;
        let rgb = lerpRgb(t.color[0], t.color[1], h1);
        // if this blob has drifted toward an edge, bleed it toward that
        // neighbor's color — the actual boundary-softening trick
        const ndx = ox > ts * 0.22 ? 1 : ox < -ts * 0.22 ? -1 : 0;
        const ndy = oy > ts * 0.22 ? 1 : oy < -ts * 0.22 ? -1 : 0;
        if (ndx || ndy) {
          const nt = map.terrainAt(gx + ndx, gy + ndy);
          if (nt && nt.name !== t.name) {
            const nrgb = lerpRgb(nt.color[0], nt.color[1], 0.5);
            rgb = [rgb[0] + (nrgb[0] - rgb[0]) * 0.6, rgb[1] + (nrgb[1] - rgb[1]) * 0.6, rgb[2] + (nrgb[2] - rgb[2]) * 0.6];
          }
        }
        const rad = ts * (0.65 + h4 * 0.55);
        softBlob(octx, cx + ox, cy + oy, rad, rad * (0.6 + h4 * 0.5), h1 * Math.PI, rgb, 0.3 + hash2(gx, gy, 50 + b) * 0.22);
      }
    }
  }

  // water treatment: lighten a shallow band near the coast (cheap distance
  // check over a small ring, not a real depth field) using the same
  // feathered blobs so the band fades smoothly out to sea instead of
  // stepping in hard rings
  const shallow = [191, 230, 242];
  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      const t = map.terrainAt(gx, gy);
      if (!t.water) continue;
      let dist = 4;
      outer:
      for (let r = 1; r <= 3; r++) {
        for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
          const nt = map.terrainAt(gx + dx, gy + dy);
          if (nt && !nt.water) { dist = r; break outer; }
        }
      }
      if (dist > 3) continue;
      const cx = gx * ts + ts / 2, cy = gy * ts + ts / 2;
      const peak = (4 - dist) / 4 * 0.30 + hash2(gx, gy, 60) * 0.05;
      softBlob(octx, cx, cy, ts * 1.1, ts * 1.1, 0, shallow, peak);
    }
  }

  prerenderRoads(octx, map);
  prerenderCityBlocks(octx, map);
  prerenderScorches(octx, map);
  prerenderDeposits(octx, map);

  return off;
}

// RESOURCE DEPOSIT MARKERS (P3 follow-up — game/resources.js/game/mapgen.js
// map.deposits). Baked into the terrain prerender same as roads/city blocks
// above: deposits never move or change mid-game, so there's no reason to pay
// a per-frame draw cost for them. A faint tinted glow marks the deposit's
// whole "resource-rich area" (matches game/mapgen.js DEPOSIT_TUNABLES.RADIUS
// loosely, just for legibility — not read back for gameplay), and a small
// diamond glyph pins the exact anchor tile. Colors/labels are pulled
// entirely from RESOURCE_DEFS (game/resources.js) so a new resource needs no
// renderer change. The readable NAME label is drawn separately, in screen
// space, by drawLabels below — zoom-reactive text doesn't belong baked into
// a static bitmap.
function prerenderDeposits(octx, map) {
  const ts = map.tileSize;
  for (const d of (map.deposits || [])) {
    const res = RESOURCE_DEFS[d.type];
    if (!res) continue;
    const cx = (d.gx + 0.5) * ts, cy = (d.gy + 0.5) * ts;
    const rgb = hexToRgb(res.color);
    softBlob(octx, cx, cy, ts * 2.4, ts * 2.4, 0, rgb, 0.07 + Math.min(1.6, d.richness) * 0.04);
    octx.save();
    octx.translate(cx, cy);
    octx.beginPath();
    octx.moveTo(0, -ts * 0.46);
    octx.lineTo(ts * 0.36, 0);
    octx.lineTo(0, ts * 0.46);
    octx.lineTo(-ts * 0.36, 0);
    octx.closePath();
    octx.fillStyle = rgba(rgb, 0.88);
    octx.fill();
    octx.strokeStyle = 'rgba(8,8,8,0.6)';
    octx.lineWidth = Math.max(1, ts * 0.045);
    octx.stroke();
    octx.restore();
  }
}

// DECORATIVE CITY BLOCKS (P2 city layer, CONCEPT.md Pillar 3: "what you
// build is on the map"). Purely a render-layer treatment of tiles the map
// already classifies as `urban` — never touches map.grid, so re-running this
// against byte-identical map JSON produces byte-identical terrain data,
// just a richer painted city on top. Scenery only: not entities, not
// destructible (that's the plots/battle-phase districts, later).
//
// Deterministic from hash2(gx,gy,seed) exactly like the terrain texture pass
// above, so the same map always looks the same city. Density scales with
// proximity to the nearest SETTLEMENT center — map.cities (the 5 objective
// cities) AND map.towns (small, non-objective villages — game/mapgen.js)
// both carry {x,y,r} in TILE coords, and both feed the same density/lane
// logic below with zero special-casing by name — so the capital (r=10, the
// biggest by construction — see mapgen.js) reads as visibly denser/bigger
// than a lesser city, which in turn dwarfs any small town (r=2..4). A tile
// within a couple rings of a road gets a placement-probability bump and is
// nudged away from the road tile it's closest to, so blocks loosely "front
// the street" instead of a uniform scatter ignoring the road network
// entirely.
const ROOF_PALETTES = [
  ['#6b4a34', '#8a6244'], // warm brick/tile
  ['#585049', '#726a5f'], // grey slate
  ['#75563a', '#93704c'], // sun-baked tan
];
// Distinct from the residential palettes above — used only for the rare
// larger civic/industrial structure the middle ring occasionally rolls (a
// warehouse/depot/guildhall silhouette breaking up a street of houses).
const CIVIC_PALETTE = ['#4f5c54', '#6f8177'];

// Nearest SETTLEMENT (a city or a small town — see below) to a world-px
// point, plus a normalized 0..1 "how deep into its core" density. Operating
// in world-px (not tile coords) is what lets the per-city rotated
// street-lane grid below use ordinary trig against the settlement's exact
// center instead of fighting tile-grid quantization.
function nearestSettlementAt(settlements, wx, wy, ts) {
  let best = null, bestNorm = Infinity;
  for (const c of settlements) {
    const dx = wx - (c.x + 0.5) * ts, dy = wy - (c.y + 0.5) * ts;
    const norm = Math.hypot(dx, dy) / Math.max(1, (c.r || 6) * ts);
    if (norm < bestNorm) { bestNorm = norm; best = c; }
  }
  return best ? { c: best, density: Math.max(0, 1 - bestNorm) } : null;
}

// nearest road tile within a small ring, or null — used both to bias
// placement probability and to nudge a block's offset away from the street
// so it reads as "fronting" it rather than sitting on top of it.
function nearestRoadDir(map, gx, gy, r = 2) {
  for (let ring = 1; ring <= r; ring++) {
    for (let dy = -ring; dy <= ring; dy++) {
      for (let dx = -ring; dx <= ring; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== ring) continue;
        if (map.roadAt(gx + dx, gy + dy) > 0) return { dx, dy };
      }
    }
  }
  return null;
}

// RICHER CITIES (playtest feedback: "the city-block rendering ... reads as
// a checkerboard of squares" — CONCEPT.md's Art-Direction Notes flagged the
// same thing: "Cities should be prettier"). The single biggest reason the
// old pass read as a grid was that every tile placed its block independent
// of its neighbors, so the WORLD's own axis-aligned tile grid always showed
// through no matter how much per-block jitter got added. The fix: each
// settlement (city OR small town — see nearestSettlementAt above) gets its
// own randomly-ROTATED internal street/lane grid, carved BEFORE
// buildings are placed. A tile that lands on a lane gets a thin worn-path
// treatment instead of a building; a tile between lanes gets one. Because
// the rotation angle differs settlement to settlement, no two cities' block
// structure lines up with the map's world axes (or with each other), which
// is what actually breaks the "checkerboard" read — the lanes ARE the
// "internal street/lane structure" the task asked for, not a cosmetic
// add-on drawn on top.
// Draws the internal street/lane grid for one settlement as real ROTATED
// VECTOR STROKES (clipped to the settlement's core), not per-tile fills.
// This is the fix for an early version of this pass that painted lane tiles
// with fillRect — at 32px tile granularity that just recreated a NEW,
// bigger checkerboard (clean tile-aligned rectangles), the exact "reads as
// squares" complaint this whole feature exists to fix. A stroked line has
// no such quantization: its edges are smooth at any angle, so the lane
// genuinely reads as a diagonal street cutting across the block instead of
// a grid cell toggling on/off.
function drawSettlementLanes(octx, c, ts) {
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const rot = (hash2(seedX, seedY, 9001) - 0.5) * Math.PI * 0.9;
  const spacing = ts * (2.2 + hash2(seedX, seedY, 9002) * 1.3);
  const width = ts * (0.14 + hash2(seedX, seedY, 9003) * 0.08);
  // slightly SMALLER than the settlement's nominal radius (not larger) —
  // the stamped urban footprint (game/mapgen.js) is an organic blob that
  // roughly fills `r` but never perfectly circular, so clipping at exactly
  // `r` (let alone beyond it) let faint lines bleed out over open
  // water/terrain past the actual city edge. Staying a bit inside `r`
  // trades a few uncovered corner tiles (already the sparsest, lowest-
  // density part of the city) for never painting a street over the sea.
  const R = (c.r || 6) * ts * 0.88;
  const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;

  octx.save();
  octx.beginPath();
  octx.arc(cx, cy, R, 0, Math.PI * 2);
  octx.clip(); // keep lanes inside this settlement's own core — never bleeds onto open terrain
  octx.translate(cx, cy);
  octx.rotate(rot);
  octx.strokeStyle = 'rgba(150,138,114,0.18)';
  octx.lineWidth = width;
  const n = Math.ceil(R / spacing) + 1;
  for (let i = -n; i <= n; i++) {
    const off = i * spacing;
    octx.beginPath(); octx.moveTo(-R, off); octx.lineTo(R, off); octx.stroke();
    octx.beginPath(); octx.moveTo(off, -R); octx.lineTo(off, R); octx.stroke();
  }
  octx.restore();
}

// Same rotated-grid test as drawSettlementLanes above, evaluated at one
// world-px point — used only to decide "does a building belong here," so a
// building never straddles a lane the stroke pass just painted.
function onSettlementLane(c, wx, wy, ts) {
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const rot = (hash2(seedX, seedY, 9001) - 0.5) * Math.PI * 0.9;
  const spacing = ts * (2.2 + hash2(seedX, seedY, 9002) * 1.3);
  const width = ts * (0.14 + hash2(seedX, seedY, 9003) * 0.08);
  const relx = wx - (c.x + 0.5) * ts, rely = wy - (c.y + 0.5) * ts;
  const lx = relx * Math.cos(rot) + rely * Math.sin(rot);
  const ly = -relx * Math.sin(rot) + rely * Math.cos(rot);
  const halfW = width / 2;
  const laneX = ((lx + halfW) % spacing + spacing) % spacing;
  const laneY = ((ly + halfW) % spacing + spacing) % spacing;
  return laneX < width || laneY < width;
}

// ---------------------------------------------------------------------------
// B5a CITY OVERHAUL (districts/rings + landmark + arterials + texture).
// Everything below still bakes into the same one-time terrain prerender as
// the organic block/lane pass above — it EXTENDS that pass rather than
// replacing it: drawSettlementLanes/onSettlementLane/nearestSettlementAt/
// ROOF_PALETTES/nearestRoadDir all stay exactly as they were, doing exactly
// the job they did before. What's new is layered on top:
//   - a smooth (not stepped) density->placement/size curve so the city
//     visibly THINS toward its edge instead of stopping at a hard ring,
//   - a reserved, building-free footprint at every settlement's center that
//     a dedicated LANDMARK gets painted into (a citadel for the capital, a
//     town-hall-scaled compound for lesser cities, a small plaza+monument
//     for towns),
//   - a handful of wide ARTERIAL avenues per city, computed from where the
//     map's own road network actually enters the settlement (no new mapgen
//     data needed — map.roadAt is read directly),
//   - an OLD-TOWN WALL ring around the core of the larger cities, and
//   - WATERFRONT quay/pier dressing for any city whose footprint borders a
//     water tile.
// All of it is still pure hash2(...)-driven off (c.x, c.y) and per-tile
// (gx, gy) coordinates, so it stays exactly as deterministic as the pass it
// extends — same seed always paints the same city, no Math.random anywhere.
// ---------------------------------------------------------------------------

// Closest point on segment (x1,y1)-(x2,y2) to (px,py) — used to test
// "is this tile sitting on an arterial avenue" the same way onSettlementLane
// tests against the fine lane grid.
function distToSegment(px, py, x1, y1, x2, y2) {
  const dx = x2 - x1, dy = y2 - y1;
  const len2 = dx * dx + dy * dy;
  let t = len2 > 0 ? ((px - x1) * dx + (py - y1) * dy) / len2 : 0;
  t = Math.max(0, Math.min(1, t));
  const ex = x1 + dx * t, ey = y1 + dy * t;
  return Math.hypot(px - ex, py - ey);
}
function onArterial(segs, px, py) {
  for (const s of segs) if (distToSegment(px, py, s.x1, s.y1, s.x2, s.y2) < s.halfWidth) return true;
  return false;
}

// Finds where the REAL road network (game/mapgen.js's MST + A* routing,
// already baked as map.roadAt) crosses each settlement's rough boundary,
// buckets those crossings into 16 compass bins so a cluster of adjacent road
// tiles collapses to one direction, and returns one avenue segment (center
// -> near-edge point) per surviving direction. This is why the city reads as
// "organized around its road connections" rather than an arbitrary radial
// spoke pattern — an avenue only exists where a real road actually meets the
// city.
function computeArterials(map, c, ts) {
  const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;
  const R = (c.r || 6) * ts;
  const BIN_COUNT = 16;
  const bins = new Map();
  const pad = Math.ceil(R / ts) + 2;
  const gx0 = Math.max(0, c.x - pad), gy0 = Math.max(0, c.y - pad);
  const gx1 = Math.min(map.width - 1, c.x + pad), gy1 = Math.min(map.height - 1, c.y + pad);
  for (let gy = gy0; gy <= gy1; gy++) {
    for (let gx = gx0; gx <= gx1; gx++) {
      if (map.roadAt(gx, gy) <= 0) continue;
      const wx = gx * ts + ts / 2, wy = gy * ts + ts / 2;
      const dx = wx - cx, dy = wy - cy;
      const dist = Math.hypot(dx, dy);
      const norm = dist / R;
      if (norm < 0.5 || norm > 1.3) continue; // only near-boundary road tiles count as an "entry"
      const ang = Math.atan2(dy, dx);
      const bin = Math.round(((ang + Math.PI) / (Math.PI * 2)) * BIN_COUNT) % BIN_COUNT;
      const cur = bins.get(bin);
      if (!cur || dist > cur.dist) bins.set(bin, { dist, wx, wy }); // farthest-out tile per bin = the actual entry point
    }
  }
  const segs = [];
  const halfWidth = ts * (0.36 + Math.min(1, (c.r || 6) / 10) * 0.22);
  for (const { wx, wy } of bins.values()) {
    const dx = wx - cx, dy = wy - cy;
    const dist = Math.hypot(dx, dy) || 1;
    const clamped = Math.min(dist, R * 0.98); // stop just inside the settlement edge; the real road stroke carries on from there
    segs.push({ x1: cx, y1: cy, x2: cx + (dx / dist) * clamped, y2: cy + (dy / dist) * clamped, halfWidth });
  }
  return segs;
}

// Three-layer stroke (soft halo / paved core / thin centerline) — same
// "worn into the ground" language as prerenderRoads' halo+core trick, just
// without the blur pass (these are short, few-per-city strokes; blurring
// each individually isn't worth a filter pass). Deliberately a lighter,
// cooler stone tone than the dirt-road color so an avenue reads as paved
// city street, distinct from the open-country road it connects to.
function drawArterials(octx, segs) {
  for (const s of segs) {
    octx.lineCap = 'round';
    octx.strokeStyle = 'rgba(120,108,86,0.22)';
    octx.lineWidth = s.halfWidth * 2.1;
    octx.beginPath(); octx.moveTo(s.x1, s.y1); octx.lineTo(s.x2, s.y2); octx.stroke();
    octx.strokeStyle = 'rgba(198,184,152,0.55)';
    octx.lineWidth = s.halfWidth * 1.4;
    octx.beginPath(); octx.moveTo(s.x1, s.y1); octx.lineTo(s.x2, s.y2); octx.stroke();
    octx.strokeStyle = 'rgba(92,80,62,0.30)';
    octx.lineWidth = Math.max(0.6, s.halfWidth * 0.16);
    octx.beginPath(); octx.moveTo(s.x1, s.y1); octx.lineTo(s.x2, s.y2); octx.stroke();
  }
}

// Old-town fortification ring around the CORE of the larger cities only
// (r >= 7 — about half the non-capital cities plus the capital, using the
// existing r spread from game/mapgen.js rather than adding a new flag).
// A hand-jittered polyline (not a perfect circle) with small corner-tower
// dabs, reading as a defensive perimeter separating the packed core from
// the looser middle ring around it.
function drawOldTownWall(octx, c, ts) {
  const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;
  const R = (c.r || 6) * ts;
  const wallR = R * 0.36;
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const segs = 28;
  octx.save();
  octx.strokeStyle = 'rgba(58,50,40,0.5)';
  octx.lineWidth = Math.max(1.4, ts * 0.09);
  octx.beginPath();
  for (let i = 0; i <= segs; i++) {
    const a = (i / segs) * Math.PI * 2;
    const jitter = 1 + (hash2(seedX + i, seedY, 8801) - 0.5) * 0.1;
    const px = cx + Math.cos(a) * wallR * jitter, py = cy + Math.sin(a) * wallR * jitter;
    if (i === 0) octx.moveTo(px, py); else octx.lineTo(px, py);
  }
  octx.closePath();
  octx.stroke();
  const towers = 8;
  for (let i = 0; i < towers; i++) {
    const a = (i / towers) * Math.PI * 2 + 0.2;
    const px = cx + Math.cos(a) * wallR, py = cy + Math.sin(a) * wallR;
    octx.beginPath();
    octx.arc(px, py, ts * 0.15, 0, Math.PI * 2);
    octx.fillStyle = 'rgba(58,50,40,0.55)';
    octx.fill();
  }
  octx.restore();
}

// Radius of the building-free footprint reserved at a settlement's exact
// center for its landmark — sized to comfortably fit drawLandmark's own
// compound below (see drawLandmark), scaled by `r` and grandest for the
// capital, so the landmark always stands alone instead of getting crowded
// by ordinary blocks.
function landmarkClearRadius(c, ts, isCity, isCapital) {
  if (!isCity) return ts * 0.7;
  return ts * (1.5 + (c.r || 6) / 10 * 1.1) * (isCapital ? 1.35 : 1.0);
}

// Paved plaza (existing look, unchanged) or a green pocket park (new — a few
// tree dabs around a grass-toned ellipse) for mid/outer-ring variety.
function drawPlazaOrPark(octx, cx0, cy0, gx, gy, ts, isPark) {
  octx.beginPath();
  octx.ellipse(cx0, cy0, ts * (isPark ? 0.5 : 0.55), ts * (isPark ? 0.4 : 0.42), hash2(gx, gy, 203) * Math.PI, 0, Math.PI * 2);
  octx.fillStyle = isPark ? 'rgba(90,132,74,0.32)' : 'rgba(140,128,104,0.28)';
  octx.fill();
  if (isPark) {
    for (let i = 0; i < 3; i++) {
      const a = hash2(gx, gy, 270 + i) * Math.PI * 2, d = ts * (0.12 + hash2(gx, gy, 280 + i) * 0.22);
      octx.beginPath();
      octx.arc(cx0 + Math.cos(a) * d, cy0 + Math.sin(a) * d, ts * 0.11, 0, Math.PI * 2);
      octx.fillStyle = 'rgba(58,96,48,0.55)';
      octx.fill();
    }
  }
}

// Faint dashed fence outline around a detached outer-ring structure —
// "edge of town" texture (a yard/paddock around the building) rather than
// the block just running out of room, sold with a light touch (not every
// outer-ring building gets one).
function drawYardFence(octx, cx0, cy0, w, h, ts, gx, gy) {
  if (hash2(gx, gy, 290) > 0.6) return;
  const rx = Math.max(w, h) * 0.62, ry = Math.max(w, h) * 0.5;
  octx.save();
  octx.strokeStyle = 'rgba(150,138,110,0.28)';
  octx.lineWidth = Math.max(0.6, ts * 0.025);
  octx.setLineDash([ts * 0.06, ts * 0.08]);
  octx.beginPath();
  octx.ellipse(cx0, cy0, rx, ry, hash2(gx, gy, 291) * Math.PI, 0, Math.PI * 2);
  octx.stroke();
  octx.setLineDash([]);
  octx.restore();
}

function prerenderCityBlocks(octx, map) {
  // towns (game/mapgen.js's small-settlements pass) join cities for this
  // render-only pass so a village gets the exact same organic
  // block-and-lane treatment, just scaled down by its much smaller `r` —
  // no separate code path, no special-casing by name. Only settlements with
  // r >= 6 are ever "cities" (game/mapgen.js: the 5 objective cities are the
  // only ones that size; towns are r 2..4) — that existing size gap is used
  // below, read-only, to decide which settlements get the full
  // landmark/wall/arterial treatment vs. the small town version, with no new
  // mapgen field required.
  const settlements = (map.cities || []).concat(map.towns || []);
  if (!settlements.length) return;
  const ts = map.tileSize;
  const maxCityR = (map.cities || []).reduce((m, c) => Math.max(m, c.r || 0), 0);

  // Per-settlement street prep — fine lane grid (unchanged), plus the new
  // wide arterial avenues and old-town wall — all drawn BEFORE ordinary
  // buildings so the block-placement loop below can skip "on an avenue"
  // tiles exactly like it already skips "on a lane" tiles, and so the wall
  // sits under the buildings that crowd around it like real streetscape.
  const arterialsBySettlement = new Map();
  for (const c of settlements) {
    drawSettlementLanes(octx, c, ts);
    const isCity = (c.r || 0) >= 6;
    const segs = isCity ? computeArterials(map, c, ts) : [];
    if (segs.length) drawArterials(octx, segs);
    arterialsBySettlement.set(c, segs);
    if (isCity && c.r >= 7) drawOldTownWall(octx, c, ts);
  }

  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      const t = map.terrainAt(gx, gy);
      if (!t || t.name !== 'urban') continue;
      if (map.roadAt(gx, gy) > 0) continue; // leave road tiles clear for the road stroke

      const cx0 = gx * ts + ts / 2, cy0 = gy * ts + ts / 2;
      const settle = nearestSettlementAt(settlements, cx0, cy0, ts);
      if (!settle) continue; // shouldn't happen (terrain is only 'urban' inside a stamped settlement), but stay defensive
      const { c, density } = settle; // density: 1 at dead center, fading to 0 at the settlement's nominal radius

      if (onSettlementLane(c, cx0, cy0, ts)) continue; // no building on a lane tile — the street stays clear
      const arterialSegs = arterialsBySettlement.get(c);
      if (arterialSegs && arterialSegs.length && onArterial(arterialSegs, cx0, cy0)) continue;

      // reserve clear ground around the settlement's own landmark so it
      // reads as a standalone centerpiece rather than getting swallowed by
      // ordinary blocks pressed right up against it
      const isCity = (c.r || 0) >= 6;
      const isCapital = isCity && c.r === maxCityR;
      const clearR = landmarkClearRadius(c, ts, isCity, isCapital);
      if (Math.hypot(cx0 - (c.x + 0.5) * ts, cy0 - (c.y + 0.5) * ts) < clearR) continue;

      const roadDir = nearestRoadDir(map, gx, gy);
      const placeRoll = hash2(gx, gy, 201);
      // smooth (not stepped) density curve, deliberately continuous rather
      // than three discrete ring bands — a stepped placeChance would paint
      // a visible seam where the band boundary falls; this instead reads as
      // a genuine gradual thinning from packed core to sparse edge, exactly
      // the "fading into countryside, not a hard circle" ask.
      const placeChance = Math.max(0.12, Math.min(0.97, 0.22 + density * 0.72 + (roadDir ? 0.13 : 0)));
      if (placeRoll > placeChance) continue; // gap: courtyard/alley/paddock — keeps it from reading as a solid carpet

      // small open plazas right at a city core instead of a building, for
      // variety and so the very center doesn't read as maximally packed
      if (density > 0.78 && hash2(gx, gy, 202) < 0.22) { drawPlazaOrPark(octx, cx0, cy0, gx, gy, ts, false); continue; }
      // pocket parks scattered through the rest of the city — rarer than
      // the core plazas above, green instead of paved
      if (density <= 0.78 && hash2(gx, gy, 250) < 0.045) { drawPlazaOrPark(octx, cx0, cy0, gx, gy, ts, true); continue; }

      // nudge away from the nearest road so the block sits back off the
      // street with a small gap, rather than centered on the tile. Wider
      // than a first pass (was +-0.15 tile) — at close zoom that read as a
      // grid of near-identical boxes each pinned to its own tile center;
      // letting a block drift up to ~45% of a tile breaks the per-tile
      // quantization into something closer to an actual uneven streetscape.
      let ox = (hash2(gx, gy, 210) - 0.5) * ts * 0.44;
      let oy = (hash2(gx, gy, 211) - 0.5) * ts * 0.44;
      if (roadDir) {
        ox -= roadDir.dx * ts * 0.16;
        oy -= roadDir.dy * ts * 0.16;
      }
      const cx = cx0 + ox, cy = cy0 + oy;

      // size/rotation variance also widened for the same reason — the
      // original ranges (0.26 size spread, 0.3 rad rotation) were subtle
      // enough that same-size, near-axis-aligned boxes still dominated the
      // read at max zoom. A wider spread (independent w/h draws, so aspect
      // ratio genuinely varies block to block) and a full ~40-degree
      // rotation swing reads as an organic block cluster instead of a grid.
      // sizeScale is also now a smooth density curve (0.55 at the edge up
      // to 1.4 at dead center) for the same "no seam" reason as placeChance.
      const sizeScale = 0.55 + density * 0.85;
      let w = ts * (0.36 + hash2(gx, gy, 212) * 0.42) * sizeScale;
      let h = ts * (0.36 + hash2(gx, gy, 213) * 0.42) * sizeScale;
      const rot = (hash2(gx, gy, 214) - 0.5) * 0.7;

      // occasional larger civic/industrial structure in the mid-density
      // band — a little "not every block is a house" variety
      const civic = density > 0.3 && density <= 0.7 && hash2(gx, gy, 260) < 0.045;
      if (civic) { w *= 1.7; h *= 1.6; }

      const palette = civic ? CIVIC_PALETTE : ROOF_PALETTES[Math.floor(hash2(gx, gy, 215) * ROOF_PALETTES.length) % ROOF_PALETTES.length];
      const roofRgb = lerpRgb(palette[0], palette[1], hash2(gx, gy, 216));

      octx.save();
      octx.translate(cx, cy);
      octx.rotate(rot);

      // subtle drop shadow, offset toward lower-right (a fixed light
      // direction reads as more coherent than per-tile-random shadow angles)
      octx.fillStyle = 'rgba(6,8,10,0.35)';
      roundedRectPath(octx, -w / 2 + w * 0.1, -h / 2 + h * 0.14, w, h, Math.min(w, h) * 0.14);
      octx.fill();

      // body
      octx.fillStyle = rgba(roofRgb, 0.94);
      roundedRectPath(octx, -w / 2, -h / 2, w, h, Math.min(w, h) * 0.14);
      octx.fill();
      octx.strokeStyle = 'rgba(20,16,12,0.4)';
      octx.lineWidth = Math.max(0.6, ts * 0.02);
      octx.stroke();

      // soft highlight lick, painterly-consistent with the terrain blob
      // language rather than a hard specular — reuses softBlob at low alpha
      octx.restore();
      softBlob(octx, cx - w * 0.18, cy - h * 0.22, Math.max(w, h) * 0.5, Math.max(w, h) * 0.36, rot,
        [255, 240, 210], 0.10 + hash2(gx, gy, 217) * 0.08);

      // outer-density "yard" hint: a faint fence outline around a detached
      // structure, selling "edge of town" instead of "block ran out of room"
      if (density < 0.3) drawYardFence(octx, cx0, cy0, w, h, ts, gx, gy);
    }
  }

  // Landmark + waterfront overlays go LAST, on top of every ordinary block,
  // so a city's centerpiece and any quay/pier structures always read
  // clearly no matter how densely the surrounding blocks painted.
  for (const c of settlements) {
    const isCity = (c.r || 0) >= 6;
    const isCapital = isCity && c.r === maxCityR;
    drawLandmark(octx, c, ts, isCity, isCapital);
    if (isCity) drawWaterfront(octx, map, c, ts);
  }
}

function roundedRectPath(ctx, x, y, w, h, r) {
  r = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

// A single "tall" structure: a base tier (with an extra-long drop shadow,
// the same height cue ordinary blocks use but exaggerated) topped by a
// smaller tier offset up-and-left — a cheap top-down fake extrusion that
// reads as "this is taller than everything around it" without needing real
// 3D. `spire` adds a peaked roof on the upper tier for the tallest landmark
// (the central keep/tower), giving it a genuinely distinct silhouette
// against the flat-roofed residential blocks.
function drawTowerBlock(octx, x, y, size, dark, light, spire) {
  octx.save();
  octx.fillStyle = 'rgba(4,6,8,0.45)';
  roundedRectPath(octx, x - size / 2 + size * 0.22, y - size / 2 + size * 0.3, size, size, size * 0.12);
  octx.fill();

  octx.fillStyle = dark;
  roundedRectPath(octx, x - size / 2, y - size / 2, size, size, size * 0.12);
  octx.fill();
  octx.strokeStyle = 'rgba(20,16,12,0.5)';
  octx.lineWidth = Math.max(0.8, size * 0.05);
  octx.stroke();

  const off = size * 0.3;
  octx.fillStyle = light;
  roundedRectPath(octx, x - size * 0.34 - off * 0.25, y - size * 0.34 - off, size * 0.68, size * 0.68, size * 0.1);
  octx.fill();
  octx.strokeStyle = 'rgba(20,16,12,0.4)';
  octx.lineWidth = Math.max(0.6, size * 0.04);
  octx.stroke();

  if (spire) {
    octx.beginPath();
    octx.moveTo(x - off * 0.25, y - size * 0.34 - off);
    octx.lineTo(x - off * 0.25 + size * 0.34, y - size * 1.05 - off);
    octx.lineTo(x - off * 0.25 + size * 0.68, y - size * 0.34 - off);
    octx.closePath();
    octx.fillStyle = light;
    octx.fill();
    octx.strokeStyle = 'rgba(20,16,12,0.4)';
    octx.stroke();
  }
  octx.restore();
  softBlob(octx, x - size * 0.15 - off * 0.25, y - size * 0.15 - off, size * 0.75, size * 0.55, 0, [255, 240, 210], 0.12);
}

// THE central landmark — B5a's single biggest "these are real cities with a
// heart" lever. A city (r >= 6) gets a walled compound with a tall central
// keep (spired, distinctly colored from every ordinary roof palette); the
// capital additionally gets four corner towers and a grander gold-toned keep
// (scaled further by its own `r`, which mapgen.js already guarantees is the
// largest — no new "is capital" field needed, same trick drawLabels already
// uses for the bold/gold city name). A town (r < 6) gets a much smaller
// plaza-and-monument version — present, but never competing with a real
// city's landmark for visual weight.
function drawLandmark(octx, c, ts, isCity, isCapital) {
  const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const h = (salt) => hash2(seedX, seedY, salt);

  if (!isCity) {
    const R = ts * 0.62;
    octx.beginPath();
    octx.ellipse(cx, cy, R, R * 0.82, h(720) * Math.PI, 0, Math.PI * 2);
    octx.fillStyle = 'rgba(150,138,110,0.30)';
    octx.fill();
    drawTowerBlock(octx, cx + ts * 0.02, cy - ts * 0.02, ts * 0.5, '#6b5a48', '#8a7460', false);
    return;
  }

  const scale = isCapital ? 1.5 : 1.0;
  const compoundR = ts * (1.3 + (c.r / 10) * 1.1) * scale;

  // plaza fronting the compound
  octx.beginPath();
  octx.ellipse(cx, cy, compoundR * 1.2, compoundR * 1.05, h(721) * 0.3, 0, Math.PI * 2);
  octx.fillStyle = 'rgba(150,138,110,0.28)';
  octx.fill();

  // compound wall — an irregular octagon rather than a perfect circle, so
  // it doesn't read as a UI-drawn ring sitting on top of the city
  octx.save();
  octx.translate(cx, cy);
  octx.rotate(h(722) * Math.PI * 0.4);
  const sides = 8;
  octx.beginPath();
  for (let i = 0; i < sides; i++) {
    const a = (i / sides) * Math.PI * 2;
    const rr = compoundR * (0.92 + (i % 2 === 0 ? 0.08 : 0));
    const px = Math.cos(a) * rr, py = Math.sin(a) * rr;
    if (i === 0) octx.moveTo(px, py); else octx.lineTo(px, py);
  }
  octx.closePath();
  octx.fillStyle = 'rgba(96,88,74,0.28)';
  octx.fill();
  octx.strokeStyle = 'rgba(60,52,40,0.55)';
  octx.lineWidth = Math.max(1, ts * 0.05);
  octx.stroke();
  octx.restore();

  // capital only: four corner towers, turning the compound into a citadel
  if (isCapital) {
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2 + h(723) * Math.PI * 0.2;
      const tx = cx + Math.cos(a) * compoundR * 0.82, ty = cy + Math.sin(a) * compoundR * 0.82;
      drawTowerBlock(octx, tx, ty, ts * 0.42, '#5c5346', '#847b68', false);
    }
  }

  // central keep — the tallest, most distinctly colored silhouette in the
  // whole city (gold-toned for the capital's citadel, plain stone for a
  // lesser city's town hall)
  const bodyDark = isCapital ? '#9c7a3c' : '#6b5a48';
  const bodyLight = isCapital ? '#d8bd7c' : '#8a7460';
  drawTowerBlock(octx, cx, cy, ts * (isCapital ? 1.0 : 0.7), bodyDark, bodyLight, true);
}

// Circular run-finder over a boolean ring array — starts the linear scan
// right after a guaranteed `false` sample so a run that wraps past index 0
// is never split in two, without any special-case wraparound math.
function circularRuns(arr) {
  const N = arr.length;
  let startIdx = arr.indexOf(false);
  if (startIdx < 0) return [[0, N]]; // entire ring is coastal (a tiny islet city) — one big run
  const runs = [];
  let curStart = -1, curLen = 0;
  for (let k = 0; k < N; k++) {
    const idx = (startIdx + k) % N;
    if (arr[idx]) {
      if (curStart < 0) curStart = idx;
      curLen++;
    } else if (curStart >= 0) {
      runs.push([curStart, curLen]);
      curStart = -1; curLen = 0;
    }
  }
  if (curStart >= 0) runs.push([curStart, curLen]);
  return runs;
}

// Waterfront dressing for any city whose footprint borders a water tile
// (checked directly against map.terrainAt around the settlement's own
// radius — no new mapgen "coastal" flag needed, same read-the-existing-grid
// approach as everything else in this file). A quay/embankment stroke runs
// along each contiguous coastal arc, and the one or two longest arcs each
// get a small pier/dock jutting out into the water.
function drawWaterfront(octx, map, c, ts) {
  const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;
  const R = (c.r || 6) * ts;
  const N = 32;
  const coastal = new Array(N);
  for (let i = 0; i < N; i++) {
    const a = (i / N) * Math.PI * 2;
    const wx = cx + Math.cos(a) * R * 0.97, wy = cy + Math.sin(a) * R * 0.97;
    const t = map.terrainAt(Math.floor(wx / ts), Math.floor(wy / ts));
    coastal[i] = !!(t && t.water);
  }
  if (!coastal.some(Boolean)) return;

  octx.save();
  octx.strokeStyle = 'rgba(120,116,108,0.55)';
  octx.lineWidth = Math.max(1.6, ts * 0.14);
  octx.lineCap = 'round';
  for (let i = 0; i < N; i++) {
    if (!coastal[i] || !coastal[(i + 1) % N]) continue;
    const a0 = (i / N) * Math.PI * 2, a1 = ((i + 1) / N) * Math.PI * 2;
    octx.beginPath();
    octx.moveTo(cx + Math.cos(a0) * R * 0.9, cy + Math.sin(a0) * R * 0.9);
    octx.lineTo(cx + Math.cos(a1) * R * 0.9, cy + Math.sin(a1) * R * 0.9);
    octx.stroke();
  }
  octx.restore();

  const runs = circularRuns(coastal).sort((a, b) => b[1] - a[1]);
  const pierCount = Math.min(2, runs.length);
  for (let p = 0; p < pierCount; p++) {
    const [start, len] = runs[p];
    if (len < 2) continue;
    const mid = start + len / 2;
    const a = (mid / N) * Math.PI * 2;
    const bx = cx + Math.cos(a) * R * 0.92, by = cy + Math.sin(a) * R * 0.92;
    const pierLen = ts * 2.2, w = ts * 0.5;
    octx.save();
    octx.translate(bx, by);
    octx.rotate(a); // pier points outward, away from the city center, into the water
    octx.fillStyle = 'rgba(96,70,46,0.85)';
    roundedRectPath(octx, 0, -w / 2, pierLen, w, w * 0.15);
    octx.fill();
    octx.strokeStyle = 'rgba(50,36,22,0.6)';
    octx.lineWidth = Math.max(0.8, ts * 0.03);
    octx.stroke();
    for (let k = 0; k < 4; k++) {
      const kx = (k / 3) * pierLen;
      octx.beginPath();
      octx.moveTo(kx, -w / 2); octx.lineTo(kx, -w / 2 - w * 0.35);
      octx.moveTo(kx, w / 2); octx.lineTo(kx, w / 2 + w * 0.35);
      octx.strokeStyle = 'rgba(40,28,18,0.5)';
      octx.lineWidth = Math.max(0.6, ts * 0.025);
      octx.stroke();
    }
    octx.restore();
  }
}

// Building-destruction scorch decals (game/buildings.js removeBuilding),
// baked permanently into the terrain prerender rather than drawn per-frame —
// map.scorches persists across re-prerenders (triggered by any later
// map.dirty(), e.g. a different building going up or down elsewhere), so an
// old scorch mark doesn't vanish just because the bitmap got repainted.
function prerenderScorches(octx, map) {
  for (const s of (map.scorches || [])) {
    const seed = (s.x * 131 + s.y * 977) | 0;
    softBlob(octx, s.x, s.y, s.r * 1.35, s.r * 1.15, 0, [22, 19, 16], 0.55);
    softBlob(octx, s.x, s.y, s.r * 0.7, s.r * 0.6, 0.5, [10, 9, 8], 0.6);
    for (let i = 0; i < 6; i++) {
      const h1 = hash2(seed, i, 301), h2 = hash2(seed, i, 302), h3 = hash2(seed, i, 303);
      const ang = h1 * Math.PI * 2, dist = s.r * (0.3 + h2 * 0.7);
      const rx = s.r * (0.12 + h3 * 0.14);
      octx.save();
      octx.translate(s.x + Math.cos(ang) * dist, s.y + Math.sin(ang) * dist);
      octx.rotate(h1 * Math.PI);
      octx.fillStyle = `rgba(15,13,11,${0.35 + h2 * 0.25})`;
      octx.fillRect(-rx / 2, -rx * 0.3, rx, rx * 0.6);
      octx.restore();
    }
  }
}

// Roads (P2, engine/tilemap.js's infra overlay): painted as continuous
// STROKES along road runs, not per-tile squares — a tile-center-to-
// tile-center line segment per adjacent road pair reads as a connected
// polyline once a whole run is drawn, because consecutive segments share
// endpoints. Only the four "forward" neighbor directions are walked (right,
// down, and both diagonals going down) so each adjacent pair is drawn
// exactly once. Each segment is split at its midpoint into two halves so a
// bridge (a road tile sitting on a water tile — see tilemap.js roadAt vs
// terrainAt) gets its own tone without needing a separate bridge encoding.
//
// "Worn into the ground, not a decal on top of it" (CONCEPT.md art-direction
// notes) means the road can't have a crisp, uniform-width, hard-edged
// outline the way the old single-stroke version did — that reads as a
// sticker no matter how good the fill color is. Two changes get it there:
//
//  1. Two-layer paint, same trick a real gouache/watercolor road does: a
//     wide, soft, low-alpha HALO laid down first (tinted toward the local
//     terrain color, so the margin looks like dirt-worn ground rather than a
//     paved edge) and blurred as a single image after all halo strokes are
//     drawn — a canvas filter blur over the whole halo layer in one pass
//     feathers every stroke's edge AND lets neighboring strokes melt into
//     each other, at a fraction of the cost of hand-computing per-pixel
//     gradients along every run. A crisp, narrow CORE stroke then goes on
//     top, unblurred, so the road stays legible as a network at any zoom —
//     "worn in" softens the edges, it doesn't remove the road.
//  2. Wear/color variation along the run is driven by the same continuous
//     sub-tile noise lattice prerenderTerrain's base fill uses (see
//     buildNoiseLattice/sampleLattice above), sampled in WORLD space rather
//     than once per tile — so a road's lightness drifts smoothly along its
//     length (packed-earth patches vs. rutted/darker stretches) instead of
//     jumping at every tile boundary, and it never re-introduces a tile
//     grid. Two thin, low-alpha "wheel rut" lines paired either side of the
//     centerline sell the worn-in read further — traffic pressing a real
//     dirt road over time leaves exactly this double-groove.
//
// The halo layer (and the noise lattices driving it) is sized to the road
// network's own bounding box, not the whole map — most maps are mostly NOT
// road, so this keeps the extra cost roughly proportional to road coverage
// instead of map area. Bridges skip the terrain-color bleed (there's no
// "ground" under a bridge to bleed from) and keep a tighter, plainer core so
// a crossing still reads as a distinct structure rather than more worn dirt.
function prerenderRoads(octx, map) {
  const ts = map.tileSize;
  const roadColor = ['#8a744f', '#9c8760']; // dirt-tan
  const bridgeColor = ['#6f7681', '#828a94']; // cooler grey-plank tone, reads as "over water"
  const FORWARD = [[1, 0], [0, 1], [1, 1], [1, -1]];
  const BACK = [[-1, 0], [0, -1], [-1, -1], [-1, 1]];

  const isRoad = (gx, gy) => map.roadAt(gx, gy) > 0;
  const isBridge = (gx, gy) => { const t = map.terrainAt(gx, gy); return !!(t && t.water); };
  const tileCenter = (gx, gy) => [gx * ts + ts / 2, gy * ts + ts / 2];

  // Bounding box (in tiles, padded so the blur/feather has room to fall off)
  // of every road tile — lets the halo canvas and its noise lattices scale
  // with the size of the road network instead of the whole map.
  let minGX = Infinity, minGY = Infinity, maxGX = -Infinity, maxGY = -Infinity, any = false;
  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      if (!isRoad(gx, gy)) continue;
      any = true;
      if (gx < minGX) minGX = gx; if (gx > maxGX) maxGX = gx;
      if (gy < minGY) minGY = gy; if (gy > maxGY) maxGY = gy;
    }
  }
  if (!any) return; // no infra layer on this map — nothing to paint

  const pad = 3;
  minGX = Math.max(0, minGX - pad); minGY = Math.max(0, minGY - pad);
  maxGX = Math.min(map.width - 1, maxGX + pad); maxGY = Math.min(map.height - 1, maxGY + pad);
  const originX = minGX * ts, originY = minGY * ts;
  const haloW = (maxGX - minGX + 1) * ts, haloH = (maxGY - minGY + 1) * ts;

  const halo = document.createElement('canvas');
  halo.width = haloW; halo.height = haloH;
  const hctx = halo.getContext('2d');

  // Wear lattice drives lightness drift along a run (packed-earth vs. rutted
  // patches); edge lattice is finer and drives the irregular width jitter —
  // both continuous, both offset into the halo's local pixel space.
  const wearLat = buildNoiseLattice(haloW, haloH, ts * 1.8, 900);
  const edgeLat = buildNoiseLattice(haloW, haloH, ts * 0.55, 950);
  const sampleWear = (wx, wy) => sampleLattice(wearLat, wx - originX, wy - originY);
  const sampleEdge = (wx, wy) => sampleLattice(edgeLat, wx - originX, wy - originY);

  // Wide, soft, low-alpha layer — drawn onto the small `halo` canvas (plain
  // strokes, no blur yet; blurring is done once, below, over the whole
  // layer). Tinted toward the local terrain color so the feathered margin
  // reads as ground bleeding in rather than a second, differently-colored
  // decal sitting next to the first.
  function drawHalfHalo(cx, cy, mx, my, gx, gy) {
    const bridge = isBridge(gx, gy);
    const base = bridge ? bridgeColor : roadColor;
    const wear = sampleWear(cx, cy), jitter = sampleEdge(cx, cy);
    const width = ts * (bridge ? 0.52 : 0.36 + jitter * 0.22 + wear * 0.10);
    let rgb = lerpRgb(base[0], base[1], wear);
    if (!bridge) {
      const t = map.terrainAt(gx, gy);
      const trgb = lerpRgb(t.color[0], t.color[1], 0.5);
      rgb = [rgb[0] + (trgb[0] - rgb[0]) * 0.4, rgb[1] + (trgb[1] - rgb[1]) * 0.4, rgb[2] + (trgb[2] - rgb[2]) * 0.4];
    }
    hctx.lineCap = 'round';
    hctx.strokeStyle = rgba(rgb, bridge ? 0.55 : 0.48);
    hctx.lineWidth = width;
    hctx.beginPath(); hctx.moveTo(cx - originX, cy - originY); hctx.lineTo(mx - originX, my - originY); hctx.stroke();
  }

  // Narrow, crisp layer drawn straight onto the real terrain canvas, on top
  // of the (already-blurred, by the time this runs) halo — keeps the road
  // legible as a connective network at any zoom no matter how soft the
  // margin got. Wear-lattice-driven color variation runs down the CORE too
  // (blended with a little per-tile grain, hash2, so it isn't perfectly
  // smooth), plus a pair of thin darker "wheel rut" strokes either side of
  // centerline — skipped on bridges, where a plain plank deck reads better.
  function drawHalfCore(cx, cy, mx, my, gx, gy) {
    const bridge = isBridge(gx, gy);
    const base = bridge ? bridgeColor : roadColor;
    const wear = sampleWear(cx, cy), jitter = sampleEdge(cx, cy);
    const width = ts * (bridge ? 0.42 : 0.20 + jitter * 0.10);
    const col = lerpColor(base[0], base[1], wear * 0.7 + hash2(gx, gy, 72) * 0.3);
    octx.lineCap = 'round';
    octx.strokeStyle = col;
    octx.lineWidth = width;
    octx.beginPath(); octx.moveTo(cx, cy); octx.lineTo(mx, my); octx.stroke();

    if (!bridge) {
      const dx = mx - cx, dy = my - cy, len = Math.hypot(dx, dy) || 1;
      const px = -dy / len, py = dx / len; // unit vector perpendicular to the run
      const rutOff = width * 0.34;
      octx.strokeStyle = `rgba(38,28,16,${(0.14 + wear * 0.14).toFixed(3)})`;
      octx.lineWidth = Math.max(1, width * 0.14);
      octx.beginPath(); octx.moveTo(cx + px * rutOff, cy + py * rutOff); octx.lineTo(mx + px * rutOff, my + py * rutOff); octx.stroke();
      octx.beginPath(); octx.moveTo(cx - px * rutOff, cy - py * rutOff); octx.lineTo(mx - px * rutOff, my - py * rutOff); octx.stroke();
    }
  }

  // Walk the grid ONCE to build the segment list (same forward/back
  // adjacency scheme as before), then paint it in two passes — halo, blur,
  // core — so the blur step only ever runs once over the whole layer.
  const segments = [];
  for (let gy = minGY; gy <= maxGY; gy++) {
    for (let gx = minGX; gx <= maxGX; gx++) {
      if (!isRoad(gx, gy)) continue;
      const [cx, cy] = tileCenter(gx, gy);
      let connected = false;
      for (const [dx, dy] of FORWARD) {
        const nx = gx + dx, ny = gy + dy;
        if (nx < 0 || ny < 0 || nx >= map.width || ny >= map.height || !isRoad(nx, ny)) continue;
        connected = true;
        const [ncx, ncy] = tileCenter(nx, ny);
        const mx = (cx + ncx) / 2, my = (cy + ncy) / 2;
        segments.push([cx, cy, mx, my, gx, gy]);
        segments.push([ncx, ncy, mx, my, nx, ny]);
      }
      if (!connected) {
        for (const [dx, dy] of BACK) {
          const nx = gx + dx, ny = gy + dy;
          if (nx >= 0 && ny >= 0 && nx < map.width && ny < map.height && isRoad(nx, ny)) { connected = true; break; }
        }
      }
      if (!connected) segments.push([cx, cy, cx + 0.01, cy + 0.01, gx, gy]); // isolated tile: still visible as a dab
    }
  }

  for (const [cx, cy, mx, my, gx, gy] of segments) drawHalfHalo(cx, cy, mx, my, gx, gy);

  // Single blur pass over the whole halo layer, composited onto the real
  // terrain canvas in one drawImage — this is what actually feathers every
  // stroke's edge and melts overlapping strokes together, for the cost of
  // one filtered blit instead of per-segment gradient math.
  octx.save();
  octx.filter = `blur(${(ts * 0.26).toFixed(2)}px)`;
  octx.drawImage(halo, originX, originY);
  octx.restore();

  for (const [cx, cy, mx, my, gx, gy] of segments) drawHalfCore(cx, cy, mx, my, gx, gy);
}

// ---------------------------------------------------------------------------
// UNITS, PROJECTILES, MUZZLE FLASH/RECOIL, IMPACT FX (playtest feedback:
// "combat feels arcadey" — the VISUAL half of that fix; a sibling pass fixed
// the underlying MECHANICS in game/units.js and left three hooks for this
// file to read: u.aim (hull facing), u.turretAim (gun facing, decoupled from
// the hull so a stopped tank still tracks its target), and u.muzzleFlash
// ({t, angle} — set the instant a unit fires, aged out automatically). This
// whole section used to live in main.js as a plain triangle marker + a round
// projectile dot ("weird blob projectiles"); it moved here so all real
// battlefield DRAWING sits with the terrain/label draw code above, same
// data-doesn't-live-with-drawing split this file already keeps with
// game/*.js. main.js just calls drawUnit/drawProjectile/drawImpacts per frame.
// ---------------------------------------------------------------------------

// SILHOUETTE CLASS — a small fixed vocabulary of code-drawn shapes, chosen
// ENTIRELY from a unit def's attributes (domain / moveClass / weapon
// targeting flags / dispositions) — never its name or key. Exactly the same
// discipline game/techtreeview.js's glyphForBuilding already applies to
// building icons (see that file's comment); this is the map-unit equivalent
// of that same attribute chain, and the shapes below are stylistically
// matched to techtreeview.js's ICON_GLYPHS vocabulary (tank/aircraft/ship/
// drone/missile) so the game reads as one visual system, tank-turret-through-
// tech-tree-icon. Cached per UNIT DEF (a WeakMap keyed on the def object
// itself, which every unit of one UNIT_DEFS entry shares by reference) since
// the classification can never change and there are only ~25 defs total —
// this is a one-time lookup per unit type, not a per-unit-per-frame cost.
const silhouetteCache = new WeakMap();
function classifyUnit(def) {
  let cls = silhouetteCache.get(def);
  if (!cls) { cls = computeSilhouetteClass(def); silhouetteCache.set(def, cls); }
  return cls;
}
function computeSilhouetteClass(def) {
  const wdef = WEAPON_DEFS[def.weapon];
  if (def.domain === 'air') {
    // Manned airframes (fighter/strikejet: hp 60-70) vs small UAVs (recon
    // drone/UCAV/loitering munition/swarm: hp 12-45) — hp is the one number
    // in the roster that cleanly splits "aircraft" from "drone" with no name
    // check (see game/units.js UNIT_DEFS for the actual figures).
    return def.hp >= 50 ? 'aircraft' : 'drone';
  }
  if (def.domain === 'naval') return 'ship'; // only shape in the naval domain today; scales with def.radius already
  if (!wdef) return 'support'; // unweaponed ground vehicle: EW/recon support (e.g. the jammer)
  if (def.moveClass === 'foot') return 'infantry'; // dismounted team — ATGM/MANPADS teams included, armed or not
  const holdGround = (def.dispositions || []).includes('holdGround');
  // air-only weapon (SAM/MANPADS/laser lineage): reads as an AA/SAM mount,
  // never a tank, regardless of how it moves
  if (wdef.canTargetAir && !wdef.canTargetGround) return 'airdefense';
  // a dual-purpose gun fired from a parked TRACKED mount (the AA Gun) reads
  // the same way even though its weapon can also hit ground targets
  if (wdef.kind === 'gun' && holdGround && def.moveClass === 'tracked') return 'airdefense';
  if (wdef.kind === 'ballistic' || wdef.kind === 'homing') {
    // truck-mounted (wheeled) + never chases = a rocket/missile launcher TEL
    if (holdGround && def.moveClass === 'wheeled') return 'launcher';
    if (holdGround) return 'artillery'; // tracked tube/rocket battery that holds ground
    return 'tank'; // direct-fire, closes to a standoff — the roster's tank shape
  }
  return 'scout'; // remaining case: wheeled dual-purpose gun that doesn't hold ground — light recon car
}

// TURRETED shapes get the hull(aim) + gun(turretAim) two-part treatment (the
// task's "bonus that really sells tanks" — extended to every other turreted
// ground vehicle and to ships' deck guns, since the mechanics pass computes
// turretAim generically for any unit with a target, not just the tank).
// Everything else (infantry/aircraft/drone/support) draws as one shape
// oriented on u.aim alone — a rifle squad or a strike jet doesn't have an
// independently-slewing turret in this game.
const TURRETED_SHAPES = new Set(['tank', 'artillery', 'launcher', 'airdefense', 'scout', 'ship']);
// Muzzle length (in units of the unit's own def.radius) the flash/recoil
// hooks anchor to, per shape — a tank's gun reaches further out front than a
// squat AA turret's twin barrels, etc.
const BARREL_REACH = { tank: 1.5, artillery: 2.0, launcher: 1.1, airdefense: 1.0, scout: 1.0, ship: 1.3 };
const MUZZLE_REACH = { infantry: 0.8, aircraft: 1.15, drone: 0.55 }; // non-turreted shapes' equivalent reach

// --- turreted hulls (local space: forward = +x, caller has already
// translated to the unit's screen position and rotated by u.aim; fillStyle/
// strokeStyle/lineWidth are already set by the caller, same convention
// game/techtreeview.js's ICON_GLYPHS use) ---
function hullTank(ctx, s) {
  // tracked hull: tapered nose, flat stern, tread-tick hint along both edges
  ctx.beginPath();
  ctx.moveTo(s * 1.05, 0);
  ctx.lineTo(s * 0.7, s * 0.62); ctx.lineTo(-s * 0.9, s * 0.62); ctx.lineTo(-s * 1.0, 0);
  ctx.lineTo(-s * 0.9, -s * 0.62); ctx.lineTo(s * 0.7, -s * 0.62);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  if (s > 5) { // tread ticks only worth the extra draw calls once the hull is actually visible
    ctx.beginPath();
    for (let i = -2; i <= 2; i++) {
      ctx.moveTo(i * s * 0.32, s * 0.62); ctx.lineTo(i * s * 0.32, s * 0.8);
      ctx.moveTo(i * s * 0.32, -s * 0.62); ctx.lineTo(i * s * 0.32, -s * 0.8);
    }
    ctx.stroke();
  }
}
function hullArtillery(ctx, s) {
  // low flatbed hull on two big road wheels — the long barrel is the
  // separately-rotated "turret" half, drawn by drawTurret below
  ctx.beginPath();
  ctx.moveTo(s * 0.7, -s * 0.35); ctx.lineTo(s * 0.7, s * 0.35); ctx.lineTo(-s * 0.85, s * 0.35); ctx.lineTo(-s * 0.85, -s * 0.35);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  ctx.beginPath();
  ctx.arc(-s * 0.5, s * 0.45, s * 0.3, 0, 6.28);
  ctx.moveTo((s * 0.35) + s * 0.3, s * 0.45); ctx.arc(s * 0.35, s * 0.45, s * 0.3, 0, 6.28);
  ctx.fill(); ctx.stroke();
}
function hullLauncher(ctx, s) {
  // wheeled TEL: long narrow truck bed, small road wheels along the bottom edge
  ctx.beginPath();
  ctx.moveTo(s * 0.95, -s * 0.3); ctx.lineTo(s * 0.95, s * 0.3); ctx.lineTo(-s * 0.95, s * 0.3); ctx.lineTo(-s * 0.95, -s * 0.3);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  if (s > 5) {
    ctx.beginPath();
    for (const wx of [-0.7, -0.25, 0.25, 0.7]) { ctx.moveTo((wx + 0.16) * s, s * 0.42); ctx.arc(wx * s, s * 0.42, s * 0.16, 0, 6.28); }
    ctx.fill();
  }
}
function hullAirdefense(ctx, s) {
  // squat box base — mount for the twin-gun or missile-canister turret
  ctx.beginPath();
  ctx.moveTo(s * 0.6, -s * 0.5); ctx.lineTo(s * 0.6, s * 0.5); ctx.lineTo(-s * 0.6, s * 0.5); ctx.lineTo(-s * 0.6, -s * 0.5);
  ctx.closePath(); ctx.fill(); ctx.stroke();
}
function hullScout(ctx, s) {
  // small, light wheeled hull — same tapered-nose language as the tank, just narrower/shorter
  ctx.beginPath();
  ctx.moveTo(s * 0.95, 0); ctx.lineTo(s * 0.55, s * 0.45); ctx.lineTo(-s * 0.8, s * 0.45);
  ctx.lineTo(-s * 0.8, -s * 0.45); ctx.lineTo(s * 0.55, -s * 0.45);
  ctx.closePath(); ctx.fill(); ctx.stroke();
}
function hullShip(ctx, s) {
  // pointed bow, curved flanks, flat transom stern, small bridge block amidships
  ctx.beginPath();
  ctx.moveTo(s * 1.3, 0); ctx.quadraticCurveTo(s * 0.4, -s * 0.5, -s * 1.1, -s * 0.42);
  ctx.lineTo(-s * 1.1, s * 0.42); ctx.quadraticCurveTo(s * 0.4, s * 0.5, s * 1.3, 0);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  if (s > 5) {
    ctx.fillRect(-s * 0.25, -s * 0.3, s * 0.55, s * 0.6);
    ctx.strokeRect(-s * 0.25, -s * 0.3, s * 0.55, s * 0.6);
  }
}
const SILHOUETTE_HULLS = { tank: hullTank, artillery: hullArtillery, launcher: hullLauncher, airdefense: hullAirdefense, scout: hullScout, ship: hullShip };

function drawTurret(ctx, s, cls, wdef) {
  const reach = BARREL_REACH[cls] || 1.2;
  // Missile-canister style for a launcher TEL or a non-gun (homing) air-
  // defense mount — echoes the diamond missile BODY drawProjectile draws in
  // flight below, so a launcher visibly carries the same ordnance it fires.
  if (cls === 'launcher' || (cls === 'airdefense' && wdef && wdef.kind !== 'gun')) {
    ctx.beginPath(); ctx.arc(0, 0, s * 0.32, 0, 6.28); ctx.fill(); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(s * reach, 0); ctx.lineTo(s * (reach - 0.45), s * 0.16); ctx.lineTo(s * 0.15, s * 0.16);
    ctx.lineTo(s * 0.15, -s * 0.16); ctx.lineTo(s * (reach - 0.45), -s * 0.16);
    ctx.closePath(); ctx.fill(); ctx.stroke();
    return;
  }
  if (cls === 'airdefense') {
    // twin-barrel AA gun mount
    ctx.beginPath(); ctx.arc(0, 0, s * 0.3, 0, 6.28); ctx.fill(); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(0, -s * 0.14); ctx.lineTo(s * reach, -s * 0.14);
    ctx.moveTo(0, s * 0.14); ctx.lineTo(s * reach, s * 0.14);
    ctx.stroke();
    return;
  }
  // default single-barrel turret — tank/artillery/scout/ship deck gun
  ctx.beginPath(); ctx.arc(0, 0, s * 0.42, 0, 6.28); ctx.fill(); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(s * reach, 0); ctx.stroke();
}

// --- non-turreted whole-body shapes (also local space, forward = +x) ---
function drawInfantry(ctx, s) {
  // a small fireteam wedge — three troopers, point toward facing. Reads as
  // "personnel" at any zoom without needing per-limb detail at close range.
  const dot = Math.max(1.1, s * 0.34);
  ctx.beginPath();
  ctx.arc(s * 0.55, 0, dot, 0, 6.28);
  ctx.moveTo(-s * 0.35 + dot, s * 0.5); ctx.arc(-s * 0.35, s * 0.5, dot, 0, 6.28);
  ctx.moveTo(-s * 0.35 + dot, -s * 0.5); ctx.arc(-s * 0.35, -s * 0.5, dot, 0, 6.28);
  ctx.fill(); ctx.stroke();
}
function drawAircraft(ctx, s) {
  // swept-wing dart silhouette
  ctx.beginPath();
  ctx.moveTo(s * 1.3, 0);
  ctx.lineTo(s * 0.15, s * 0.22); ctx.lineTo(-s * 0.2, s * 0.95); ctx.lineTo(-s * 0.5, s * 0.7);
  ctx.lineTo(-s * 0.35, s * 0.18); ctx.lineTo(-s * 1.15, s * 0.28); ctx.lineTo(-s * 1.15, -s * 0.28);
  ctx.lineTo(-s * 0.35, -s * 0.18); ctx.lineTo(-s * 0.5, -s * 0.7); ctx.lineTo(-s * 0.2, -s * 0.95);
  ctx.lineTo(s * 0.15, -s * 0.22);
  ctx.closePath(); ctx.fill(); ctx.stroke();
}
function drawDrone(ctx, s) {
  // small quad-rotor body — central hub + 4 splayed arms with rotor rings
  ctx.beginPath();
  ctx.moveTo(s * 0.4, 0); ctx.lineTo(0, s * 0.4); ctx.lineTo(-s * 0.4, 0); ctx.lineTo(0, -s * 0.4);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  const arms = [[0.75, 0.75], [0.75, -0.75], [-0.75, 0.75], [-0.75, -0.75]];
  if (s > 3) {
    ctx.beginPath();
    for (const [ax, ay] of arms) { ctx.moveTo(0, 0); ctx.lineTo(ax * s, ay * s); }
    ctx.stroke();
    for (const [ax, ay] of arms) { ctx.beginPath(); ctx.arc(ax * s, ay * s, s * 0.22, 0, 6.28); ctx.stroke(); }
  }
}
function drawSupport(ctx, s) {
  // boxy support hull + a small jammer/antenna mast with two signal arcs
  ctx.beginPath();
  ctx.moveTo(s * 0.7, -s * 0.45); ctx.lineTo(s * 0.7, s * 0.45); ctx.lineTo(-s * 0.7, s * 0.45); ctx.lineTo(-s * 0.7, -s * 0.45);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  if (s > 4) {
    ctx.beginPath(); ctx.moveTo(0, -s * 0.45); ctx.lineTo(0, -s * 1.1); ctx.stroke();
    ctx.beginPath(); ctx.arc(0, -s * 1.1, s * 0.35, Math.PI * 1.15, Math.PI * 1.85); ctx.stroke();
  }
}
function drawFallback(ctx, s) {
  // defensive catch-all (mirrors glyphForBuilding's own default branch) —
  // never hit by the current roster, keeps a future UNIT_DEFS entry with an
  // unusual attribute combo visible instead of invisible
  ctx.beginPath();
  ctx.moveTo(s * 1.3, 0); ctx.lineTo(-s, s * 0.8); ctx.lineTo(-s, -s * 0.8);
  ctx.closePath(); ctx.fill(); ctx.stroke();
}
const SILHOUETTE_FULL = { infantry: drawInfantry, aircraft: drawAircraft, drone: drawDrone, support: drawSupport };

// Mirrors game/units.js's own MUZZLE_FLASH_LIFE (0.3s) — that file owns the
// fire-event's actual timing (ages u.muzzleFlash.t up, clears it at this
// duration); this constant only needs to match it so the flash's fade-out
// finishes exactly as the event itself expires, not drive any timing itself.
const MUZZLE_FLASH_LIFE_MIRROR = 0.3;
const RECOIL_DURATION = 0.12; // seconds — the kick itself, well inside the flash's fade window
const RECOIL_KICK = 3.2; // world px at peak recoil, scaled by zoom/DPR like every other on-screen size here

function drawMuzzleFlash(ctx, sx, sy, r, cls, flash) {
  const fade = Math.max(0, 1 - flash.t / MUZZLE_FLASH_LIFE_MIRROR);
  if (fade <= 0) return;
  const reach = (BARREL_REACH[cls] ?? MUZZLE_REACH[cls] ?? 1) * r;
  const tipX = sx + Math.cos(flash.angle) * reach, tipY = sy + Math.sin(flash.angle) * reach;
  ctx.save();
  ctx.translate(tipX, tipY);
  ctx.rotate(flash.angle);
  ctx.globalAlpha = fade;
  const flen = r * (0.5 + fade * 0.4); // sized to the muzzle tip, not the whole hull — a flash, not a fireball
  const grad = ctx.createRadialGradient(0, 0, 0, 0, 0, flen);
  grad.addColorStop(0, 'rgba(255,250,220,0.95)');
  grad.addColorStop(0.4, 'rgba(255,190,90,0.75)');
  grad.addColorStop(1, 'rgba(255,120,40,0)');
  ctx.fillStyle = grad;
  ctx.beginPath(); ctx.ellipse(flen * 0.3, 0, flen, flen * 0.55, 0, 0, 6.28); ctx.fill();
  ctx.strokeStyle = 'rgba(255,235,180,0.9)';
  ctx.lineWidth = Math.max(0.6, r * 0.1);
  for (const a of [-0.5, 0, 0.5]) { ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(Math.cos(a) * flen * 1.1, Math.sin(a) * flen * 1.1); ctx.stroke(); }
  ctx.restore();
  ctx.globalAlpha = 1;
}

// Exported: main.js's render loop calls this once per LIVE, currently-
// visible unit (fog-filtering happens at the call site, same as before this
// pass). `isHovered` replaces the old `u === hovered` check done inline —
// main.js still owns what "hovered" means, this file just draws the result.
export function drawUnit(ctx, worldToScreen, cam, u, isHovered) {
  const [sx, sy] = worldToScreen(u.x, u.y);
  const r = u.def.radius * cam.zoom * devicePixelRatio;

  // domain read-at-a-glance: air units cast a small drop shadow (hints
  // altitude), naval units get a wake ring (hints hull-in-water) — unchanged
  // from the pre-silhouette version, purely cosmetic, off the same `domain`
  // attribute as everything else.
  if (u.def.domain === 'air') {
    ctx.beginPath(); ctx.ellipse(sx, sy + r * 0.7, r * 0.75, r * 0.32, 0, 0, 6.28);
    ctx.fillStyle = 'rgba(0,0,0,.35)'; ctx.fill();
  } else if (u.def.domain === 'naval') {
    ctx.beginPath(); ctx.arc(sx, sy, r * 1.7, 0, 6.28);
    ctx.strokeStyle = 'rgba(190,225,255,.4)'; ctx.lineWidth = 1; ctx.stroke();
  }

  const cls = classifyUnit(u.def);
  const wdef = WEAPON_DEFS[u.def.weapon];
  const side = u.side === 'player' ? '#5fd0ff' : '#ff5a5a'; // matches OWNERSHIP_COLORS above — one side-color vocabulary for the whole map
  const sideLight = u.side === 'player' ? '#bfeeff' : '#ffc3b0';
  const flash = u.muzzleFlash;
  const kick = flash && flash.t < RECOIL_DURATION
    ? (1 - flash.t / RECOIL_DURATION) * RECOIL_KICK * cam.zoom * devicePixelRatio : 0;
  const kx = flash ? -Math.cos(flash.angle) * kick : 0, ky = flash ? -Math.sin(flash.angle) * kick : 0;

  ctx.strokeStyle = 'rgba(8,10,14,0.55)';
  ctx.lineWidth = Math.max(0.6, r * 0.06);

  if (TURRETED_SHAPES.has(cls)) {
    // HULL — oriented to movement facing (u.aim), independent of where the
    // gun is currently pointed
    ctx.save();
    ctx.translate(sx, sy);
    ctx.rotate(u.aim || 0);
    ctx.fillStyle = side;
    (SILHOUETTE_HULLS[cls] || drawFallback)(ctx, r);
    ctx.restore();

    // TURRET — oriented to u.turretAim (tracks the live target independent
    // of hull facing — see game/units.js's TURRET AIM comment), kicked back
    // a couple px along the fire angle while a muzzle flash is fresh
    ctx.save();
    ctx.translate(sx + kx, sy + ky);
    ctx.rotate(u.turretAim ?? u.aim ?? 0);
    ctx.fillStyle = sideLight;
    drawTurret(ctx, r, cls, wdef);
    ctx.restore();
  } else {
    ctx.save();
    ctx.translate(sx + kx, sy + ky);
    ctx.rotate(u.aim || 0);
    ctx.fillStyle = side;
    (SILHOUETTE_FULL[cls] || drawFallback)(ctx, r);
    ctx.restore();
  }

  if (flash) drawMuzzleFlash(ctx, sx, sy, r, cls, flash);

  if (u.hp < u.def.hp) {
    const w = r * 2.4;
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(sx - w / 2, sy - r - 8, w, 3);
    ctx.fillStyle = '#6dffb0'; ctx.fillRect(sx - w / 2, sy - r - 8, w * (u.hp / u.def.hp), 3);
  }
  if (isHovered) { ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(sx, sy, r + 5, 0, 6.28); ctx.stroke(); }
}

// ---------------------------------------------------------------------------
// PROJECTILES — real ordnance in flight, keyed off WEAPON_DEFS[...].kind
// (never a weapon or unit NAME), replacing the old flat "round blob" marker:
//   ballistic (shells — tank/artillery/MLRS/SRBM/hypersonic/…): an elongated
//     shell body with a short fading motion-trail, oriented along its flight
//     line (constant for the whole flight — see game/units.js's fireProjectile).
//   gun (autocannon/directed-energy point-defense): a fast, thin, bright
//     tracer streak — a glow pass plus a hot core line plus a small flare
//     at the leading tip.
//   homing (missiles — AAM/SAM/ATGM/cruise/anti-ship/rocket/loitering): a
//     small finned missile body with a warm exhaust trail, oriented on its
//     live velocity vector (it steers, so this direction changes in flight).
// `p.physics` on the projectile object already mirrors `kind` 1:1 (see
// fireProjectile) so it's used as the fallback if a weapon somehow has no
// WEAPON_DEFS entry — this never actually diverges from wdef.kind in
// practice, it's just defensive.
export function drawProjectile(ctx, worldToScreen, cam, p) {
  const wdef = WEAPON_DEFS[p.weapon];
  const kind = wdef ? wdef.kind : p.physics;
  const y = p.physics === 'ballistic' ? p.y - (p.lofted || 0) : p.y; // lofted arc height stays a pure visual offset, unchanged from before
  const [sx, sy] = worldToScreen(p.x, y);
  const scale = cam.zoom * devicePixelRatio;
  const bodyColor = p.side === 'player' ? '#eaf6ff' : '#ffe3d0';
  const rimColor = p.side === 'player' ? '#5fd0ff' : '#ff5a5a';
  // Ballistic flight is a straight line for its whole duration (x0,y0 -> tx,
  // ty — see fireProjectile), so its heading is fixed rather than read off a
  // per-frame velocity the way homing/gun projectiles are.
  const angle = p.physics === 'ballistic' ? Math.atan2(p.ty - p.y0, p.tx - p.x0) : Math.atan2(p.vy, p.vx);

  if (kind === 'gun') drawTracer(ctx, sx, sy, angle, scale, bodyColor, rimColor);
  else if (kind === 'homing') drawMissile(ctx, sx, sy, angle, scale, bodyColor, rimColor);
  else drawShell(ctx, sx, sy, angle, scale, bodyColor, rimColor);
}

function drawShell(ctx, sx, sy, angle, scale, bodyColor, rimColor) {
  const len = Math.max(6, 9 * scale), wid = Math.max(2, 2.6 * scale);
  ctx.save();
  ctx.translate(sx, sy);
  ctx.rotate(angle);
  const trail = len * 2.4;
  const g = ctx.createLinearGradient(-trail, 0, -len * 0.4, 0);
  g.addColorStop(0, 'rgba(255,200,120,0)');
  g.addColorStop(1, 'rgba(255,220,150,0.55)');
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.moveTo(-len * 0.4, -wid * 0.35); ctx.lineTo(-trail, 0); ctx.lineTo(-len * 0.4, wid * 0.35);
  ctx.closePath(); ctx.fill();
  ctx.fillStyle = bodyColor;
  ctx.beginPath();
  ctx.moveTo(len * 0.5, 0); ctx.lineTo(len * 0.1, -wid * 0.5); ctx.lineTo(-len * 0.4, -wid * 0.5);
  ctx.lineTo(-len * 0.4, wid * 0.5); ctx.lineTo(len * 0.1, wid * 0.5);
  ctx.closePath(); ctx.fill();
  ctx.strokeStyle = rimColor; ctx.lineWidth = Math.max(0.5, scale * 0.5); ctx.stroke();
  ctx.restore();
}

function drawTracer(ctx, sx, sy, angle, scale, bodyColor, rimColor) {
  const len = Math.max(8, 14 * scale);
  ctx.save();
  ctx.translate(sx, sy);
  ctx.rotate(angle);
  ctx.globalAlpha = 0.35;
  ctx.strokeStyle = rimColor;
  ctx.lineWidth = Math.max(1.5, scale * 2.2);
  ctx.beginPath(); ctx.moveTo(-len, 0); ctx.lineTo(len * 0.3, 0); ctx.stroke();
  ctx.globalAlpha = 1;
  ctx.strokeStyle = bodyColor;
  ctx.lineWidth = Math.max(0.8, scale * 0.9);
  ctx.beginPath(); ctx.moveTo(-len * 0.7, 0); ctx.lineTo(len * 0.3, 0); ctx.stroke();
  ctx.fillStyle = '#fff';
  ctx.beginPath(); ctx.arc(len * 0.3, 0, Math.max(0.8, scale * 0.7), 0, 6.28); ctx.fill();
  ctx.restore();
}

function drawMissile(ctx, sx, sy, angle, scale, bodyColor, rimColor) {
  const len = Math.max(6, 8 * scale), wid = Math.max(1.6, 2.2 * scale);
  ctx.save();
  ctx.translate(sx, sy);
  ctx.rotate(angle);
  const trail = len * 3;
  const g = ctx.createLinearGradient(-trail, 0, -len * 0.3, 0);
  g.addColorStop(0, 'rgba(255,140,40,0)');
  g.addColorStop(0.6, 'rgba(255,170,60,0.45)');
  g.addColorStop(1, 'rgba(255,235,160,0.85)');
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.moveTo(-len * 0.3, -wid * 0.28); ctx.lineTo(-trail, 0); ctx.lineTo(-len * 0.3, wid * 0.28);
  ctx.closePath(); ctx.fill();
  ctx.strokeStyle = rimColor; ctx.lineWidth = Math.max(0.6, scale * 0.6);
  ctx.beginPath();
  ctx.moveTo(-len * 0.25, 0); ctx.lineTo(-len * 0.55, -wid * 0.9);
  ctx.moveTo(-len * 0.25, 0); ctx.lineTo(-len * 0.55, wid * 0.9);
  ctx.stroke();
  ctx.fillStyle = bodyColor;
  ctx.beginPath();
  ctx.moveTo(len * 0.55, 0); ctx.lineTo(len * 0.05, -wid * 0.45); ctx.lineTo(-len * 0.3, -wid * 0.45);
  ctx.lineTo(-len * 0.3, wid * 0.45); ctx.lineTo(len * 0.05, wid * 0.45);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  ctx.restore();
}

// ---------------------------------------------------------------------------
// IMPACT FX — replaces the old plain fading-ring "hit marker" with a proper
// flash/spark/debris burst, sized off whether the hit came from a shell/
// missile or a gun-kind tracer (game/units.js's resolveHit now carries the
// firing weapon's name through onto the hit record purely for this — see the
// comment there). `0.25` below mirrors that same file's fixed hit life.
const HIT_LIFE_MIRROR = 0.25;
export function drawImpacts(ctx, worldToScreen, cam, hits) {
  const scale = cam.zoom * devicePixelRatio;
  for (const h of hits) {
    const [sx, sy] = worldToScreen(h.x, h.y);
    const wdef = WEAPON_DEFS[h.weapon];
    const big = !wdef || wdef.kind !== 'gun'; // shells/missiles hit harder than a fast tracer round
    const t = 1 - Math.max(0, Math.min(1, h.life / HIT_LIFE_MIRROR)); // 0 at impact -> 1 as it fades
    const maxR = (big ? 24 : 11) * scale * (0.4 + t * 0.9);
    ctx.save();
    ctx.globalAlpha = Math.max(0, 1 - t) * (big ? 1 : 0.85);
    const g = ctx.createRadialGradient(sx, sy, 0, sx, sy, maxR);
    g.addColorStop(0, 'rgba(255,250,225,0.95)');
    g.addColorStop(0.35, big ? 'rgba(255,190,90,0.75)' : 'rgba(255,225,150,0.6)');
    g.addColorStop(1, 'rgba(255,120,40,0)');
    ctx.fillStyle = g;
    ctx.beginPath(); ctx.arc(sx, sy, maxR, 0, 6.28); ctx.fill();
    if (big) {
      // expanding ring sells "shockwave"; a few deterministic debris specks
      // (hashed off the impact point, not Math.random — stable if redrawn)
      ctx.globalAlpha = Math.max(0, 1 - t) * 0.8;
      ctx.strokeStyle = 'rgba(255,210,150,0.9)';
      ctx.lineWidth = Math.max(1, 1.6 * scale);
      ctx.beginPath(); ctx.arc(sx, sy, maxR * 0.75, 0, 6.28); ctx.stroke();
      const seed = (h.x * 131 + h.y * 977) | 0;
      ctx.fillStyle = 'rgba(60,50,40,0.6)';
      for (let i = 0; i < 4; i++) {
        const a = ((seed >> (i * 5)) & 0x3ff) / 1024 * 6.28;
        const d = maxR * (0.5 + 0.4 * (((seed >> (i * 3 + 2)) & 0xff) / 255));
        ctx.beginPath(); ctx.arc(sx + Math.cos(a) * d, sy + Math.sin(a) * d, Math.max(0.6, scale * 0.9), 0, 6.28); ctx.fill();
      }
    }
    ctx.restore();
  }
}

export function createRenderer(canvas) {
  const ctx = canvas.getContext('2d');
  // shakeX/shakeY: RENDER-ONLY screen-shake offset (main.js's trauma model
  // writes these every frame — see COMBAT-FEEL/AUDIO pass). Deliberately NOT
  // folded into cam.x/cam.y: worldToScreen/screenToWorld below must stay
  // byte-identical to before shake existed, or every mouse-driven system
  // (unit selection, move orders, build placement) would jitter along with
  // the screen. frame() applies the offset as a whole-canvas ctx.translate
  // wrapped in its own save/restore instead, so it visually shakes
  // everything drawn without perturbing a single world<->screen coordinate.
  const cam = { x: 0, y: 0, zoom: 1, shakeX: 0, shakeY: 0 };
  const terrainCache = { map: null, version: -1, canvas: null };

  function resize() {
    canvas.width = canvas.clientWidth * devicePixelRatio;
    canvas.height = canvas.clientHeight * devicePixelRatio;
  }
  addEventListener('resize', resize);
  resize();

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

  function ensureTerrain(map) {
    if (terrainCache.map === map && terrainCache.version === map.version) return terrainCache.canvas;
    const t0 = performance.now();
    terrainCache.canvas = prerenderTerrain(map);
    terrainCache.map = map;
    terrainCache.version = map.version;
    console.log(`[renderer] pre-rendered terrain for "${map.name}" (${map.width}x${map.height}) in ${(performance.now() - t0).toFixed(1)}ms`);
    return terrainCache.canvas;
  }

  function drawTilemap(map) {
    const off = ensureTerrain(map);
    const ts = map.tileSize;
    const [tlx, tly] = screenToWorld(0, 0);
    const [brx, bry] = screenToWorld(canvas.width, canvas.height);
    const sx0 = Math.max(0, tlx), sy0 = Math.max(0, tly);
    const sx1 = Math.min(off.width, brx), sy1 = Math.min(off.height, bry);
    if (sx1 <= sx0 || sy1 <= sy0) return;
    const [dx0, dy0] = worldToScreen(sx0, sy0);
    const scale = cam.zoom * devicePixelRatio;
    ctx.drawImage(off, sx0, sy0, sx1 - sx0, sy1 - sy0, dx0, dy0, (sx1 - sx0) * scale, (sy1 - sy0) * scale);
  }

  // Labels draw in screen space every frame (not baked into the terrain
  // bitmap) so they can react to zoom/pan without a repaint. Cities: always
  // on, sized/faded by zoom. Regions: only once zoomed out past a
  // threshold, large + very transparent, map-painter style.
  const REGION_ZOOM_THRESHOLD = 0.62;
  function drawLabels(map) {
    const dpr = devicePixelRatio;
    ctx.save();
    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';
    // The "capital" reads bold/gold — driven by which city actually has the
    // largest radius (mapgen.js always makes the capital biggest by
    // construction), never by matching a specific name. Ties all read as
    // capital-style, which never happens in practice since mapgen.js only
    // ever gives city index 0 r=10.
    const maxCityR = (map.cities || []).reduce((m, c) => Math.max(m, c.r || 0), 0);
    for (const c of map.cities || []) {
      const [sx, sy] = worldToScreen(c.x * map.tileSize, c.y * map.tileSize);
      if (sx < -150 || sx > canvas.width + 150 || sy < -150 || sy > canvas.height + 150) continue;

      // OWNERSHIP RING (Playability v1 — game/objectives.js sets
      // c.owner/.captureProgress/.contested on the exact same city objects
      // this loop already reads; drawn dynamically here every frame rather
      // than baked into the static terrain prerender above, since ownership
      // changes mid-game). Reads sensibly even for a map that never called
      // initCityOwnership (c.owner undefined falls through to a neutral
      // grey) so this never breaks a bare/authored map that skips
      // objectives entirely. A CONTESTED city (both sides holding ground
      // units inside it this frame) gets a dashed ring instead of solid;
      // an in-progress capture draws a partial arc in the CHALLENGING
      // side's color so a capture-in-progress reads at a glance without
      // opening the HUD.
      {
        const ownerColor = c.owner === 'player' ? OWNERSHIP_COLORS.player : c.owner === 'enemy' ? OWNERSHIP_COLORS.enemy : OWNERSHIP_COLORS.neutral;
        const ringR = (c.r || 6) * map.tileSize * cam.zoom * dpr;
        ctx.save();
        ctx.beginPath();
        ctx.arc(sx, sy, ringR, 0, Math.PI * 2);
        ctx.lineWidth = (c.contested ? 2.6 : 2) * dpr;
        ctx.strokeStyle = ownerColor;
        ctx.globalAlpha = 0.85;
        if (c.contested) ctx.setLineDash([6 * dpr, 5 * dpr]);
        ctx.stroke();
        ctx.setLineDash([]);
        if (c.captureProgress > 0.001) {
          const challengerColor = c.owner === 'player' ? '#ff5a5a' : '#5fd0ff';
          ctx.beginPath();
          ctx.arc(sx, sy, ringR, -Math.PI / 2, -Math.PI / 2 + c.captureProgress * Math.PI * 2);
          ctx.lineWidth = 4 * dpr;
          ctx.strokeStyle = challengerColor;
          ctx.globalAlpha = 1;
          ctx.stroke();
        }
        ctx.restore();
      }

      const size = Math.max(11, Math.min(24, 14 * cam.zoom)) * dpr;
      const alpha = Math.max(0.45, Math.min(1, cam.zoom * 1.3));
      const big = (c.r || 0) === maxCityR;
      ctx.font = `${big ? 'bold ' : ''}${size}px Consolas, monospace`;
      const above = (c.r || 4) * map.tileSize * cam.zoom * dpr + 8 * dpr;
      ctx.lineWidth = 3 * dpr;
      ctx.strokeStyle = `rgba(4,8,14,${alpha * 0.8})`;
      ctx.strokeText(c.name, sx, sy - above);
      ctx.fillStyle = big ? `rgba(255,230,150,${alpha})` : `rgba(255,255,255,${alpha})`;
      ctx.fillText(c.name, sx, sy - above);
    }
    // Deposit labels (opposite sense from region labels below: readable only
    // once zoomed IN — a deposit is a small, precise spot, not a sweeping
    // area, so its name is clutter at strategic zoom and only earns its
    // place on screen once the player is close enough to plausibly be
    // siting a mine).
    const DEPOSIT_LABEL_ZOOM_THRESHOLD = 0.45;
    if (cam.zoom > DEPOSIT_LABEL_ZOOM_THRESHOLD) {
      for (const d of map.deposits || []) {
        const res = RESOURCE_DEFS[d.type];
        if (!res) continue;
        const [sx, sy] = worldToScreen((d.gx + 0.5) * map.tileSize, (d.gy + 0.5) * map.tileSize);
        if (sx < -100 || sx > canvas.width + 100 || sy < -100 || sy > canvas.height + 100) continue;
        const size = Math.max(9, Math.min(13, 11 * cam.zoom)) * dpr;
        ctx.font = `${size}px Consolas, monospace`;
        const ly = sy + map.tileSize * 0.6 * cam.zoom * dpr + 12 * dpr;
        ctx.lineWidth = 2.5 * dpr;
        ctx.strokeStyle = 'rgba(4,8,14,0.75)';
        ctx.strokeText(res.name, sx, ly);
        ctx.fillStyle = 'rgba(255,255,255,0.62)';
        ctx.fillText(res.name, sx, ly);
      }
    }
    if (cam.zoom < REGION_ZOOM_THRESHOLD) {
      const regionList = map.regions || [];
      // game/mapgen.js lays regions out as a uniform row/column grid, so
      // regions sharing a row land at nearly the same screen Y at this zoom
      // — a small deterministic zigzag (by position within the list, not by
      // name/content) keeps same-row labels from stacking directly on top
      // of each other without needing any real collision layout.
      regionList.forEach((r, i) => {
        const cx = (r.x0 + r.x1) / 2 * map.tileSize, cy = (r.y0 + r.y1) / 2 * map.tileSize;
        const [sx, sy0] = worldToScreen(cx, cy);
        const zigzag = ((i % 3) - 1) * 30 * dpr;
        const sy = sy0 + zigzag;
        if (sx < -400 || sx > canvas.width + 400 || sy < -200 || sy > canvas.height + 200) return;
        const size = 40 * dpr;
        ctx.font = `${size}px Georgia, serif`;
        ctx.fillStyle = 'rgba(255,255,255,0.09)';
        ctx.fillText(r.name.toUpperCase(), sx, sy);
      });
    }
    ctx.restore();
  }

  function clear() {
    ctx.fillStyle = '#060a12';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
  }

  function frame(map, drawWorld) {
    clear();
    // whole-canvas render-only translate for screen shake — see the cam
    // comment above. worldToScreen/screenToWorld are untouched by this, so
    // anything computing where the mouse is over the world (build/move
    // targeting) reads the un-shaken coordinate space, exactly as required.
    ctx.save();
    ctx.translate(cam.shakeX || 0, cam.shakeY || 0);
    drawTilemap(map);
    drawWorld(ctx, worldToScreen, cam);
    drawLabels(map);
    ctx.restore();
  }

  return { ctx, cam, canvas, resize, worldToScreen, screenToWorld, frame };
}

// Mouse drag-to-pan + wheel-to-zoom, matching the feel of the original game's
// research-tree pan control (drag empty space; clicks on entities still work).
export function attachCameraControls(canvas, cam, opts = {}) {
  const minZoom = opts.minZoom ?? 0.08, maxZoom = opts.maxZoom ?? 3;
  let drag = null;
  canvas.addEventListener('pointerdown', e => {
    if (opts.isEntityHit && opts.isEntityHit(e)) return;
    drag = { x: e.clientX, y: e.clientY, camX: cam.x, camY: cam.y };
    canvas.classList.add('grabbing');
  });
  canvas.addEventListener('pointermove', e => {
    if (!drag) return;
    cam.x = drag.camX - (e.clientX - drag.x) / cam.zoom;
    cam.y = drag.camY - (e.clientY - drag.y) / cam.zoom;
  });
  const end = () => { drag = null; canvas.classList.remove('grabbing'); };
  canvas.addEventListener('pointerup', end);
  canvas.addEventListener('pointercancel', end);
  addEventListener('pointerup', end);
  canvas.addEventListener('wheel', e => {
    e.preventDefault();
    const f = Math.exp(-e.deltaY * 0.001);
    cam.zoom = Math.max(minZoom, Math.min(maxZoom, cam.zoom * f));
  }, { passive: false });
}
