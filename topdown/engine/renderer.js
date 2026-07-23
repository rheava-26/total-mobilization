import { RESOURCE_DEFS } from '../game/resources.js';
// WEAPON_DEFS is a pure DATA table (kind/targeting flags) — importing it here
// is the same one-directional "renderer reads game/*.js data" relationship
// RESOURCE_DEFS above already has, not a mechanics dependency: everything
// below only ever READS .kind/.canTargetAir/.canTargetGround off it to pick
// a look, never touches unit/projectile update logic (that stays entirely in
// game/units.js).
import { WEAPON_DEFS } from '../game/units.js';
// B6 (operational-map battalion presentation, CONCEPT.md's "BATTALIONS, not
// singular units" -> §3.3 hybrid representation) — same one-directional
// "renderer reads game/*.js data" relationship as RESOURCE_DEFS/WEAPON_DEFS
// above: drawBattalionMarker below only ever READS these two pure
// summarizers (battalionStrength: alive/total/hpFrac off bn.units;
// moraleState: the fresh/steady/shaken/broken bucket off bn.morale), never
// mutates a battalion or unit. No circular import risk — game/battalions.js
// only imports game/units.js, game/battalionMorale.js only imports
// game/battalions.js + game/objectives.js (which only imports
// game/units.js); neither imports this file.
import { battalionStrength } from '../game/battalions.js';
import { moraleState } from '../game/battalionMorale.js';

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
  // stepping in hard rings.
  //
  // BUG FIX (inland water rendering as flat, hard-edged rectangles): the
  // original version here drew exactly ONE blob, dead-centered on the tile,
  // whose peak alpha was a pure step function of an INTEGER ring-distance
  // (1/2/3/4 tiles from the nearest land) — no per-tile jitter, no offset,
  // same tile-aligned center every time. That's precisely the "one value
  // held constant across the whole tile, jumping to a new value at the next
  // tile" checkerboard bug pass 1's continuous noise lattice above exists to
  // fix for LAND; water's own painterly pass 2 (the jittered/offset land
  // blobs a few dozen lines up) explicitly skips water tiles entirely
  // ("water skips the blob pass, it should read calm"), so this single-blob
  // shallow band was the ONLY texture water ever got beyond pass 1's very
  // subtle base noise (water/river's two color-def endpoints are close
  // hexes, so that base noise barely shows). A big ocean's coastline dilutes
  // the artifact — most of the ocean is plain deep water far outside the
  // 3-tile band, and the coastline itself is long/irregular so a bit of
  // per-tile banding along it reads as "texture." A small INLAND pond,
  // though, is small enough that EVERY tile in it (or nearly every tile)
  // falls inside the same 3-tile shallow band, so the whole pond is nothing
  // but this per-tile step function — which is exactly the "flat rectangles
  // with hard edges sitting on the grass" the bug report describes.
  //
  // Fix: give water the same "several small jittered, offset, overlapping
  // blobs" treatment pass 2 already uses for land, instead of one centered
  // disc. Jittering the blob CENTER off the tile's exact center (not just
  // its alpha) is what actually breaks the tile-grid alignment that read as
  // hard seams — neighboring tiles' blobs now overlap unevenly and blend,
  // the same way overlapping offset land blobs melt into their neighbors
  // instead of reading as polka dots.
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
      const basePeak = (4 - dist) / 4 * 0.30;
      const blobs = 3;
      for (let b = 0; b < blobs; b++) {
        const h1 = hash2(gx, gy, 61 + b), h2 = hash2(gx, gy, 71 + b), h3 = hash2(gx, gy, 81 + b);
        const ox = (h1 - 0.5) * ts * 1.3, oy = (h2 - 0.5) * ts * 1.3;
        const peak = basePeak * (0.5 + h3 * 0.7) + hash2(gx, gy, 90 + b) * 0.05;
        const rad = ts * (0.85 + h3 * 0.7);
        softBlob(octx, cx + ox, cy + oy, rad, rad * (0.75 + h3 * 0.4), h1 * Math.PI, shallow, Math.max(0, peak));
      }
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
// MODERNIZATION (designer: "buildings look like they're from 1196 ... make
// them look more modern"). Two material families instead of one all-brown
// medieval set, picked per-building by HEIGHT TIER below (see the
// building-placement loop): CONCRETE_PALETTES for low/mid-rise residential —
// concrete grey, off-white precast/stucco, a muted sage-grey, and ONE warm
// stone/brick accent kept sparse so the city has a little warmth without
// reading as a field of brown — and GLASS_PALETTES for taller commercial
// towers — cool steel/glass blue-green/blue-violet tones that pair with the
// curtain-wall sheen drawn on tall buildings below. Muted/desaturated on
// purpose (not neon) to stay harmonious with the parchment map.
const CONCRETE_PALETTES = [
  ['#84898c', '#aeb4b7'], // concrete grey
  ['#c7c1b0', '#e7e1cf'], // off-white precast/stucco
  ['#6f6a5d', '#948d7a'], // warm stone/brick accent (sparse, not dominant)
  ['#5b6660', '#7c8981'], // muted sage-grey low-rise
];
const GLASS_PALETTES = [
  ['#2d3d46', '#5c8ea0'], // steel/glass blue-teal curtain wall
  ['#334349', '#6c9298'], // cooler slate glass
  ['#3a3f4a', '#6d7690'], // blue-violet glass tower
];
const ROOF_PALETTES = CONCRETE_PALETTES; // kept name: default low/mid-rise family
// Larger office/industrial complexes (mid ring AND core now — see the civic
// condition below) — steel-toned, distinct from ordinary residential
// concrete, reads as a modern business park / office block rather than a
// medieval guildhall.
const CIVIC_PALETTE = ['#546670', '#83a0ab'];

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
// One settlement's own local street-grid parameters — rotation, lane
// spacing, lane width — factored out to a single source of truth (used to
// live duplicated in drawSettlementLanes AND onSettlementLane). B5b adds a
// THIRD consumer: the building-placement pass below reads `.rot` directly so
// every building can sit SQUARE to its settlement's own rotated grid instead
// of getting an independent random spin — see the building-placement loop's
// comment for why that's the actual fix for "random blocks tossed in".
function settlementGrid(c, ts) {
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const rot = (hash2(seedX, seedY, 9001) - 0.5) * Math.PI * 0.9;
  const spacing = ts * (2.2 + hash2(seedX, seedY, 9002) * 1.3);
  const width = ts * (0.14 + hash2(seedX, seedY, 9003) * 0.08);
  return { rot, spacing, width, seedX, seedY };
}

// Draws the internal street/lane grid for one settlement as real ROTATED
// VECTOR STROKES (clipped to the settlement's core), not per-tile fills.
// This is the fix for an early version of this pass that painted lane tiles
// with fillRect — at 32px tile granularity that just recreated a NEW,
// bigger checkerboard (clean tile-aligned rectangles), the exact "reads as
// squares" complaint this whole feature exists to fix. A stroked line has
// no such quantization: its edges are smooth at any angle, so the lane
// genuinely reads as a diagonal street cutting across the block instead of
// a grid cell toggling on/off.
//
// B5b: two-layer (soft warm wash + slightly lighter worn core) instead of
// the old single near-invisible thin stroke — designer feedback called the
// grey space between buildings "the most problematic" part of the city, and
// a lane you can barely see is exactly what let flat ground dominate the
// read instead of it looking like a paved street network. Same halo+core
// language prerenderRoads/drawArterials already use, just lighter-weight
// since a settlement has many dense in-city lanes rather than a handful of
// avenues (no separate blur pass — these are numerous, short, straight
// strokes, so a second unblurred wash layer gets the same "worn in" softness
// far more cheaply than a filter pass would per lane).
function drawSettlementLanes(octx, c, ts) {
  const { rot, spacing, width } = settlementGrid(c, ts);
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
  const n = Math.ceil(R / spacing) + 1;
  for (let i = -n; i <= n; i++) {
    const off = i * spacing;
    octx.strokeStyle = 'rgba(150,134,100,0.16)';
    octx.lineWidth = width * 2.1;
    octx.beginPath(); octx.moveTo(-R, off); octx.lineTo(R, off); octx.stroke();
    octx.beginPath(); octx.moveTo(off, -R); octx.lineTo(off, R); octx.stroke();
    octx.strokeStyle = 'rgba(196,178,138,0.30)';
    octx.lineWidth = width;
    octx.beginPath(); octx.moveTo(-R, off); octx.lineTo(R, off); octx.stroke();
    octx.beginPath(); octx.moveTo(off, -R); octx.lineTo(off, R); octx.stroke();
  }
  octx.restore();
}

// Same rotated-grid test as drawSettlementLanes above, evaluated at one
// world-px point — used only to decide "does a building belong here," so a
// building never straddles a lane the stroke pass just painted. Hit-test
// width intentionally stays the narrow COLLISION width even though the
// stroke drawn above is visually wider — same convention roads already use
// (a soft halo wider than what actually blocks placement).
function onSettlementLane(c, wx, wy, ts) {
  const { rot, spacing, width } = settlementGrid(c, ts);
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
      // track both the farthest-out sample (the actual entry point) AND how
      // many road tiles fed this direction — `count` is what lets the cap
      // below keep only genuinely-established roads, not every lone stub
      if (cur) { cur.count++; if (dist > cur.dist) { cur.dist = dist; cur.wx = wx; cur.wy = wy; } }
      else bins.set(bin, { dist, wx, wy, count: 1 });
    }
  }
  // Cap to a handful of the STRONGEST entries (designer feedback: smaller
  // cities "starburst" — every compass direction with even one road tile
  // near the boundary got its own full avenue, radiating like sun-rays
  // instead of reading as "a couple of main roads into town"). Ranking by
  // tile count (a real thoroughfare feeds many road tiles into the same
  // direction bin; a stray stub feeds one) and taking the top few, with
  // distance as a tiebreak, keeps only the avenues an actual road justifies.
  const MAX_ARTERIALS = 3;
  const ranked = [...bins.values()].sort((a, b) => b.count - a.count || b.dist - a.dist);
  const chosen = ranked.slice(0, MAX_ARTERIALS);
  const segs = [];
  // narrower than before (was 0.36..0.58 half-width) — "less starburst" also
  // means each individual avenue reads as a street, not a wide bright band
  const halfWidth = ts * (0.20 + Math.min(1, (c.r || 6) / 10) * 0.13);
  for (const { wx, wy } of chosen) {
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
    // toned down across the board (was 0.22/0.55/0.30 alpha) — narrower AND
    // dimmer, so the capped ~3 avenues read as a couple of main roads
    // instead of bright rays even on a small city where they dominate more
    // of the visible footprint
    octx.strokeStyle = 'rgba(120,108,86,0.14)';
    octx.lineWidth = s.halfWidth * 2.1;
    octx.beginPath(); octx.moveTo(s.x1, s.y1); octx.lineTo(s.x2, s.y2); octx.stroke();
    octx.strokeStyle = 'rgba(198,184,152,0.40)';
    octx.lineWidth = s.halfWidth * 1.4;
    octx.beginPath(); octx.moveTo(s.x1, s.y1); octx.lineTo(s.x2, s.y2); octx.stroke();
    octx.strokeStyle = 'rgba(92,80,62,0.22)';
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

// Modern flat-roof detailing — HVAC/AC condenser units, a rooftop water
// tank, reflective skylight strips, a solar-panel grid, or (rare, tallest
// towers only) a rooftop helipad. Drawn in the building's own LOCAL space
// (caller must already be translated+rotated to the building/roof center),
// so it rotates for free with the building. `rolls` is a handful of
// deterministic 0..1 values (cellHash(...) with distinct salts, computed by
// the caller) passed in as plain numbers rather than threading the hash
// closure through, so this stays a pure drawing function.
function drawRoofDetail(octx, w, h, kind, rolls) {
  const pad = Math.min(w, h) * 0.16;
  const rw = Math.max(0, w - pad * 2), rh = Math.max(0, h - pad * 2);
  if (rw < 2 || rh < 2) return;
  switch (kind) {
    case 0: { // HVAC / AC condenser units — small grey boxes with a dark vent stroke
      const count = 2 + Math.floor(rolls[0] * 3);
      for (let k = 0; k < count; k++) {
        const ux = (rolls[(k * 2 + 1) % rolls.length] - 0.5) * rw * 0.8;
        const uy = (rolls[(k * 2 + 2) % rolls.length] - 0.5) * rh * 0.8;
        const us = Math.min(w, h) * (0.09 + rolls[(k + 3) % rolls.length] * 0.05);
        octx.fillStyle = 'rgba(58,62,64,0.85)';
        roundedRectPath(octx, ux - us / 2, uy - us / 2, us, us, us * 0.15);
        octx.fill();
        octx.strokeStyle = 'rgba(18,18,18,0.4)';
        octx.lineWidth = Math.max(0.5, us * 0.06);
        octx.stroke();
      }
      break;
    }
    case 1: { // rooftop water tank — squat cylinder, off-center
      const tr = Math.min(w, h) * 0.16;
      const tx = (rolls[0] - 0.5) * rw * 0.5, ty = (rolls[1] - 0.5) * rh * 0.5;
      octx.fillStyle = 'rgba(72,68,60,0.85)';
      octx.beginPath(); octx.ellipse(tx, ty, tr, tr * 0.85, 0, 0, Math.PI * 2); octx.fill();
      octx.strokeStyle = 'rgba(20,18,14,0.5)';
      octx.lineWidth = Math.max(0.5, tr * 0.08);
      octx.stroke();
      break;
    }
    case 2: { // reflective skylight strips
      octx.fillStyle = 'rgba(190,215,222,0.5)';
      for (let k = 0; k < 2; k++) {
        const sy = -rh * 0.22 + k * rh * 0.44;
        roundedRectPath(octx, -rw * 0.32, sy - rh * 0.05, rw * 0.64, rh * 0.1, rh * 0.03);
        octx.fill();
      }
      break;
    }
    case 3: { // solar panel grid — dark blue rows, thin cool grid lines
      const cols = 3, rows = 2;
      const pw = rw / cols * 0.82, ph = rh / rows * 0.7;
      octx.fillStyle = 'rgba(28,40,56,0.8)';
      for (let ry = 0; ry < rows; ry++) for (let rx = 0; rx < cols; rx++) {
        const px = -rw / 2 + (rx + 0.5) * (rw / cols), py = -rh / 2 + (ry + 0.5) * (rh / rows);
        roundedRectPath(octx, px - pw / 2, py - ph / 2, pw, ph, 1);
        octx.fill();
      }
      octx.strokeStyle = 'rgba(120,150,180,0.35)';
      octx.lineWidth = 0.6;
      for (let ry = 0; ry < rows; ry++) for (let rx = 0; rx < cols; rx++) {
        const px = -rw / 2 + (rx + 0.5) * (rw / cols), py = -rh / 2 + (ry + 0.5) * (rh / rows);
        roundedRectPath(octx, px - pw / 2, py - ph / 2, pw, ph, 1);
        octx.stroke();
      }
      break;
    }
    case 4: { // rooftop helipad — tallest towers only
      const hr = Math.min(rw, rh) * 0.3;
      octx.beginPath(); octx.arc(0, 0, hr, 0, Math.PI * 2);
      octx.fillStyle = 'rgba(28,30,32,0.75)'; octx.fill();
      octx.strokeStyle = 'rgba(228,228,228,0.75)'; octx.lineWidth = Math.max(0.6, hr * 0.12); octx.stroke();
      octx.strokeStyle = 'rgba(228,228,228,0.8)';
      octx.lineWidth = Math.max(0.8, hr * 0.18);
      octx.beginPath(); octx.moveTo(-hr * 0.32, -hr * 0.45); octx.lineTo(-hr * 0.32, hr * 0.45); octx.stroke();
      octx.beginPath(); octx.moveTo(hr * 0.32, -hr * 0.45); octx.lineTo(hr * 0.32, hr * 0.45); octx.stroke();
      octx.beginPath(); octx.moveTo(-hr * 0.32, 0); octx.lineTo(hr * 0.32, 0); octx.stroke();
      break;
    }
  }
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

  // Deterministic hash for a LOT (a settlement-local block-grid cell, not a
  // raw world tile — see the placement loop below) — same hash2 primitive,
  // just fed the lot's own integer (i,j) address plus the settlement's own
  // seed so every lot gets a stable, settlement-unique roll.
  const cellHash = (seedX, seedY, i, j, salt) => hash2(seedX + i * 92821, seedY + j * 68909, salt);

  // Building placement runs in each settlement's own LOCAL, ROTATED
  // block-grid space (u = local "east", v = local "north", both along
  // grid.rot) — NOT a raw world-tile scan. This is the actual fix for
  // "random blocks tossed into the mix": an earlier pass kept placing
  // buildings at ordinary WORLD-tile centers and only changed their
  // rotation to match the settlement grid — uniformly rotating a bunch of
  // shapes that still sit on an unrotated world position grid just turns a
  // checkerboard into a diamond/argyle lattice (still reads as "shapes
  // tossed down," just now all tossed at the same angle). Working in local
  // (u,v) space — the exact same rotated coordinate frame
  // drawSettlementLanes strokes its lane lines in — makes a building's LOT
  // one cell of a grid that is ITSELF rotated with the streets, so
  // buildings land in genuine straight rows fronting the lanes on both
  // axes, the way an actual city block is laid out.
  for (const c of settlements) {
    const { rot, spacing, seedX, seedY } = settlementGrid(c, ts);
    const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;
    const R = (c.r || 6) * ts * 0.88;
    const isCity = (c.r || 0) >= 6;
    const isCapital = isCity && c.r === maxCityR;
    const clearR = landmarkClearRadius(c, ts, isCity, isCapital);
    const arterialSegs = arterialsBySettlement.get(c);
    const cosR = Math.cos(rot), sinR = Math.sin(rot);

    // Lot size: a fraction of the lane spacing, so 2-3 building lots fit
    // between one lane and the next on each axis — enough for a genuine
    // ROW of buildings fronting the street, not so many that lots shrink
    // back into confetti-sized slivers.
    const lots = spacing > ts * 3.1 ? 3 : 2;
    const cell = spacing / lots;
    const n = Math.ceil(R / cell) + 1;
    const claimed = new Set(); // lots already consumed as the "far half" of a merged row block

    for (let i = -n; i <= n; i++) {
      for (let j = -n; j <= n; j++) {
        const key = i + ',' + j;
        if (claimed.has(key)) continue;
        const u = (i + 0.5) * cell, v = (j + 0.5) * cell;
        if (u * u + v * v > R * R) continue; // stay inside the settlement's own footprint

        const wx = cx + u * cosR - v * sinR, wy = cy + u * sinR + v * cosR;
        const gx = Math.floor(wx / ts), gy = Math.floor(wy / ts);
        const t = map.terrainAt(gx, gy);
        if (!t || t.name !== 'urban') continue;
        if (map.roadAt(gx, gy) > 0) continue; // leave real infrastructure road tiles clear

        const settle = nearestSettlementAt(settlements, wx, wy, ts);
        if (!settle || settle.c !== c) continue; // a neighboring settlement's turf claims this point instead
        const density = settle.density; // 1 at dead center, fading to 0 at the settlement's nominal radius

        if (onSettlementLane(c, wx, wy, ts)) continue; // no building on a lane — the street stays clear
        if (arterialSegs && arterialSegs.length && onArterial(arterialSegs, wx, wy)) continue;
        // reserve clear ground around the settlement's own landmark so it
        // reads as a standalone centerpiece rather than getting swallowed
        // by ordinary blocks pressed right up against it
        if (Math.hypot(wx - cx, wy - cy) < clearR) continue;

        const roadDir = nearestRoadDir(map, gx, gy);
        // smooth (not stepped) density curve, deliberately continuous
        // rather than three discrete ring bands — a stepped placeChance
        // would paint a visible seam where the band boundary falls; this
        // instead reads as a genuine gradual thinning from packed core to
        // sparse edge. B5b raises the whole curve (was 0.22 base / 0.72
        // density mult / 0.13 road bonus / 0.12 floor) — designer feedback
        // ("more buildings???") was that the old pass was too sparse; a
        // city should read as built-up, with only small gaps for
        // yards/alleys, not a scatter with lots of open ground showing
        // through (which is also part of what read as "grey").
        const placeChance = Math.max(0.22, Math.min(0.97, 0.34 + density * 0.78 + (roadDir ? 0.15 : 0)));
        if (cellHash(seedX, seedY, i, j, 201) > placeChance) continue; // gap: courtyard/alley/paddock

        // small open plazas right at a city core instead of a building, for
        // variety and so the very center doesn't read as maximally packed
        if (density > 0.78 && cellHash(seedX, seedY, i, j, 202) < 0.22) { drawPlazaOrPark(octx, wx, wy, i, j, ts, false); continue; }
        // pocket parks scattered through the rest of the city — rarer than
        // the core plazas above, green instead of paved
        if (density <= 0.78 && cellHash(seedX, seedY, i, j, 250) < 0.045) { drawPlazaOrPark(octx, wx, wy, i, j, ts, true); continue; }

        // HEIGHT TIER (designer: "make them look more modern" — real
        // high-rises in the core, stepping down to mid-rise then low-rise at
        // the edge, is the single biggest lever for "modern city" vs.
        // "medieval village": right now everything was the same low height).
        // Continuous with `density`, same smooth-not-stepped philosophy as
        // placeChance above, so the skyline thins gradually instead of a
        // hard ring where towers stop. 1 = low-rise, 4 = high-rise/tower.
        const heightRoll = cellHash(seedX, seedY, i, j, 230);
        let tier = 1;
        if (density > 0.6) tier = heightRoll < 0.30 ? 4 : heightRoll < 0.62 ? 3 : 2;
        else if (density > 0.32) tier = heightRoll < 0.22 ? 3 : heightRoll < 0.55 ? 2 : 1;
        else tier = heightRoll < 0.10 ? 2 : 1;

        // occasional larger office/industrial complex — no longer capped to
        // the mid-density band (designer: "large flat commercial/industrial
        // roofs in the core") so downtown itself can roll one of these too,
        // reading as a modern business-park block rather than a medieval
        // guildhall.
        const civic = density > 0.28 && cellHash(seedX, seedY, i, j, 260) < 0.05;
        if (civic) tier = Math.max(tier, 2); // a civic/office complex always reads at least mid-rise
        // occasional larger ROW BLOCK — a real merge with the adjacent lot
        // along whichever local axis the hash picks (u or v), safe/exact
        // here (unlike a world-tile-grid merge) because both lots share
        // the EXACT same settlement rotation and sit precisely `cell` apart
        // along that axis. This is the "spanning 2+ tiles" ask: two lots
        // fuse into one longer footprint, subdivided below into a
        // terraced row rather than one shapeless mega-blob.
        let mergeAxis = null;
        if (!civic && density > 0.2 && cellHash(seedX, seedY, i, j, 261) < (0.16 + density * 0.18)) {
          const axis = cellHash(seedX, seedY, i, j, 262) < 0.5 ? 'u' : 'v';
          const ni = axis === 'u' ? i + 1 : i, nj = axis === 'v' ? j + 1 : j;
          const nu = (ni + 0.5) * cell, nv = (nj + 0.5) * cell;
          if (nu * nu + nv * nv <= R * R && !claimed.has(ni + ',' + nj)) {
            const nwx = cx + nu * cosR - nv * sinR, nwy = cy + nu * sinR + nv * cosR;
            const ngx = Math.floor(nwx / ts), ngy = Math.floor(nwy / ts);
            const nt = map.terrainAt(ngx, ngy);
            const nSettle = nearestSettlementAt(settlements, nwx, nwy, ts);
            const neighborOk = nt && nt.name === 'urban' && map.roadAt(ngx, ngy) <= 0 &&
              nSettle && nSettle.c === c && !onSettlementLane(c, nwx, nwy, ts) &&
              !(arterialSegs && arterialSegs.length && onArterial(arterialSegs, nwx, nwy)) &&
              Math.hypot(nwx - cx, nwy - cy) >= clearR;
            if (neighborOk) { claimed.add(ni + ',' + nj); mergeAxis = axis; }
          }
        }
        if (mergeAxis) tier = Math.max(tier, 2); // a merged row/office block reads at least mid-rise too

        // small in-LOT jitter, applied in LOCAL (u,v) space rather than
        // world x/y — nudging along the settlement's own axes keeps the
        // building inside its lot while still respecting the grid's
        // rotation; a world-space offset would drag it sideways relative
        // to that rotated grid and reintroduce the misalignment this whole
        // restructure exists to fix. Kept small (was 0.16 of a lot) so
        // neighboring lots' buildings sit close enough to read as a
        // continuous facade along the row, not separated boxes.
        const ju = (cellHash(seedX, seedY, i, j, 210) - 0.5) * cell * 0.05;
        const jv = (cellHash(seedX, seedY, i, j, 211) - 0.5) * cell * 0.05;
        const bu = u + ju + (mergeAxis === 'u' ? cell / 2 : 0);
        const bv = v + jv + (mergeAxis === 'v' ? cell / 2 : 0);
        const bx = cx + bu * cosR - bv * sinR, by = cy + bu * sinR + bv * cosR;

        // FORM FIX (designer: "the city looks weird because it looks like
        // random blocks tossed into the mix" / "larger and straighter").
        // Every building shares its settlement's `rot` (the exact angle
        // drawSettlementLanes/onSettlementLane already carve the street
        // network at) with only a TINY few-degree jitter left for life —
        // deliberately NOT world-axis-aligned (that's the pre-B5a
        // "checkerboard of squares" complaint) and deliberately NOT
        // independently-random per building (that's "tossed in") — square
        // to the LOCAL, already-rotated street grid, like a real block.
        const bRot = rot + (cellHash(seedX, seedY, i, j, 214) - 0.5) * 0.14; // +-4 degrees, not +-20

        // BIGGER, straighter footprints (designer: "larger and straighter"),
        // sized off the LOT itself (was off a fixed ts fraction) so
        // buildings scale with however many lots actually fit between the
        // lanes, and pulled tight to the lot (0.90-0.98, was 0.58-0.80 of a
        // whole tile) so adjacent lots' buildings sit close enough to read
        // as a continuous facade/row instead of scattered islands with
        // visible gaps between every single structure.
        const sizeScale = 0.62 + density * 0.85;
        const fitScale = Math.min(1.15, sizeScale);
        let w = cell * (0.90 + cellHash(seedX, seedY, i, j, 212) * 0.08) * fitScale;
        let h = cell * (0.90 + cellHash(seedX, seedY, i, j, 213) * 0.08) * fitScale;
        // aspect-ratio variety — a real building footprint is rarely a
        // perfect square; skew one axis narrower on roughly two-thirds of
        // ordinary lots so the block reads as a mix of wide-frontage and
        // deep-footprint buildings rather than a field of same-shaped tiles
        // (the "diamond quilt" a uniform near-square footprint produces no
        // matter how well-aligned its rotation is).
        if (!mergeAxis && !civic) {
          const skew = cellHash(seedX, seedY, i, j, 219);
          if (skew < 0.38) w *= 0.58 + cellHash(seedX, seedY, i, j, 220) * 0.2;
          else if (skew > 0.62) h *= 0.58 + cellHash(seedX, seedY, i, j, 221) * 0.2;
        }
        if (mergeAxis === 'u') w = cell * 2 * (0.92 + cellHash(seedX, seedY, i, j, 212) * 0.05) * fitScale;
        if (mergeAxis === 'v') h = cell * 2 * (0.92 + cellHash(seedX, seedY, i, j, 213) * 0.05) * fitScale;
        if (civic) { w *= density > 0.6 ? 1.9 : 1.55; h *= density > 0.6 ? 1.7 : 1.4; }

        // Palette family follows HEIGHT TIER, not just civic/ordinary — tall
        // (tier >= 3) non-civic buildings pull from the cool glass/steel
        // family (curtain-wall towers), everything else from the concrete/
        // off-white/stone family. This is what makes the skyline read as
        // coherent glass-and-steel downtown vs. concrete/stucco mid-and-low
        // rise, instead of one flat brown palette everywhere.
        const isGlass = !civic && tier >= 3;
        const palette = civic ? CIVIC_PALETTE
          : isGlass ? GLASS_PALETTES[Math.floor(cellHash(seedX, seedY, i, j, 215) * GLASS_PALETTES.length) % GLASS_PALETTES.length]
          : CONCRETE_PALETTES[Math.floor(cellHash(seedX, seedY, i, j, 215) * CONCRETE_PALETTES.length) % CONCRETE_PALETTES.length];
        const roofRgb = lerpRgb(palette[0], palette[1], cellHash(seedX, seedY, i, j, 216));

        octx.save();
        octx.translate(bx, by);
        octx.rotate(bRot);

        // corner radius kept small — a real building has near-sharp
        // corners; the old pass's heavier rounding (0.12-0.14 of the short
        // side) is what made every block read as a soft rounded pebble/die
        // rather than a rectilinear structure, undercutting "straighter"
        // no matter how well the rotation/packing lined up.
        const cr = Math.min(w, h) * 0.05;

        // subtle drop shadow, offset toward lower-right (a fixed light
        // direction reads as more coherent than per-tile-random shadow
        // angles) — HEIGHT TIER stretches the offset further for taller
        // buildings, the classic "long cast shadow implies a tall building"
        // top-down cue.
        const heightBoost = ts * (tier - 1) * 0.09;
        octx.fillStyle = 'rgba(6,8,10,0.35)';
        roundedRectPath(octx, -w / 2 + w * 0.06 + heightBoost * 0.5, -h / 2 + h * 0.09 + heightBoost, w, h, cr);
        octx.fill();

        // WALL bands — thin darker strips along the bottom and right edges
        // of the footprint, reusing the same fixed lower-right light
        // direction as the drop shadow. This is the actual fix for "flat
        // blob, not a building": a single flat-fill rect has no sense of
        // mass no matter how nice its color is; a lighter ROOF top-face
        // plus a darker WALL catching shadow on two sides is the cheapest
        // fake-extrusion cue that reads as "this has height" at top-down
        // angle (same 2.5-tier language drawTowerBlock already uses for
        // landmarks, simplified to one band). Deeper for taller tiers — a
        // taller building shows more of its own side face.
        const wallRgb = [roofRgb[0] * 0.5, roofRgb[1] * 0.5, roofRgb[2] * 0.5];
        const wallDepth = Math.min(w, h) * (0.14 + (tier - 1) * 0.035);
        octx.fillStyle = rgba(wallRgb, 0.9);
        roundedRectPath(octx, -w / 2, h / 2 - wallDepth, w, wallDepth, cr * 0.6);
        octx.fill();
        roundedRectPath(octx, w / 2 - wallDepth, -h / 2, wallDepth, h, cr * 0.6);
        octx.fill();

        // ROOF top-face — inset slightly off the wall bands so both remain
        // visible, giving the roof a crisp edge rather than bleeding into
        // the wall color. Flat modern roof, not a medieval pitched/gabled
        // one — the ridge-line treatment further down stays a thin division
        // stroke, never a peaked/gabled silhouette.
        octx.fillStyle = rgba(roofRgb, 0.96);
        const roofW = w - wallDepth * 0.55, roofH = h - wallDepth * 0.55;
        roundedRectPath(octx, -w / 2, -h / 2, roofW, roofH, cr);
        octx.fill();

        // glass curtain-wall sheen — a soft diagonal reflection streak,
        // clipped to the roof, on tall glass-family towers only. This is
        // the "cool sheen/glint that sells modern skyscraper" cue; concrete/
        // off-white low-and-mid-rise buildings skip it (they get a plain
        // warm corner highlight instead, drawn after restore() below).
        if (isGlass) {
          octx.save();
          roundedRectPath(octx, -w / 2, -h / 2, roofW, roofH, cr);
          octx.clip();
          const gx0 = -w / 2, gy0 = -h / 2, gx1 = w / 2, gy1 = h / 2;
          const sheen = octx.createLinearGradient(gx0, gy0, gx1, gy1);
          sheen.addColorStop(0, 'rgba(255,255,255,0)');
          sheen.addColorStop(0.4, 'rgba(255,255,255,0)');
          sheen.addColorStop(0.5, 'rgba(226,242,246,0.4)');
          sheen.addColorStop(0.6, 'rgba(255,255,255,0)');
          sheen.addColorStop(1, 'rgba(255,255,255,0)');
          octx.fillStyle = sheen;
          octx.fillRect(gx0, gy0, w, h);
          octx.restore();
        }

        // crisp dark building outline around the FULL footprint — the other
        // half of the "reads as built, not painted" fix; the old stroke was
        // thin/low-alpha enough to nearly disappear at anything but max zoom
        octx.strokeStyle = 'rgba(14,11,8,0.6)';
        octx.lineWidth = Math.max(0.9, ts * 0.032);
        roundedRectPath(octx, -w / 2, -h / 2, w, h, cr);
        octx.stroke();

        // parapet trim — a thin light inset line just inside the roof edge,
        // the modern flat-roof equivalent of the old pitched-roof ridge:
        // sells "poured/capped roof edge" rather than a bare flat fill.
        const parapetPad = Math.min(w, h) * 0.07;
        octx.strokeStyle = 'rgba(255,255,255,0.16)';
        octx.lineWidth = Math.max(0.5, ts * 0.014);
        roundedRectPath(octx, -w / 2 + parapetPad, -h / 2 + parapetPad,
          Math.max(1, roofW - parapetPad * 2), Math.max(1, roofH - parapetPad * 2), Math.max(0, cr - parapetPad * 0.3));
        octx.stroke();

        // roof ridge / row-house division lines — a merged row block or
        // civic structure (or anything that simply rolled big) gets an
        // internal line along its long axis so a big footprint reads as a
        // TERRACE OF ATTACHED BUILDINGS/offices, not one shapeless
        // mega-blob. Skipped on tier >= 3 towers — the stepped upper massing
        // drawn below already carries that job for high-rises.
        if (tier < 3 && (mergeAxis || civic || Math.max(w, h) > ts * 1.05)) {
          const longIsW = w >= h;
          const span = longIsW ? w : h;
          octx.strokeStyle = 'rgba(14,11,8,0.28)';
          octx.lineWidth = Math.max(0.6, ts * 0.02);
          const at = -span / 2 + span / 2;
          octx.beginPath();
          if (longIsW) { octx.moveTo(at, -h / 2); octx.lineTo(at, h / 2); }
          else { octx.moveTo(-w / 2, at); octx.lineTo(w / 2, at); }
          octx.stroke();
        }

        // deterministic rooftop-detail rolls, shared by both the plain roof
        // and (for tier >= 3) the stepped upper-massing block below
        const rolls = [
          cellHash(seedX, seedY, i, j, 241), cellHash(seedX, seedY, i, j, 242),
          cellHash(seedX, seedY, i, j, 243), cellHash(seedX, seedY, i, j, 244),
          cellHash(seedX, seedY, i, j, 245), cellHash(seedX, seedY, i, j, 246),
        ];
        const detailRoll = cellHash(seedX, seedY, i, j, 240);
        let detailKind = -1;
        if (civic || tier >= 3) {
          if (tier === 4 && detailRoll < 0.12) detailKind = 4; // helipad — tallest towers only
          else if (detailRoll < 0.85) detailKind = detailRoll < 0.32 ? 0 : detailRoll < 0.5 ? 1 : detailRoll < 0.7 ? 2 : 3;
        } else if (tier === 2) {
          if (detailRoll < 0.55) detailKind = detailRoll < 0.3 ? 0 : detailRoll < 0.45 ? 1 : 2;
        } else if (detailRoll < 0.12) {
          detailKind = 0; // rare single AC unit on an ordinary low-rise house
        }

        // STEPPED UPPER MASSING for high-rise towers (tier >= 3) — a second,
        // smaller block set toward the light-opposite (upper-left) corner
        // with its own mini drop-shadow, same fake-extrusion language
        // drawTowerBlock uses for the central landmark. This is what turns
        // "tall drop shadow + deep wall band" into an actual stepped-tower
        // silhouette instead of just a bigger flat rectangle — the real
        // "skyline" read. Rooftop detail (HVAC/tank/skylights/solar/
        // helipad) is drawn on TOP of this upper block for towers, since
        // that's the roof an aerial view would actually see.
        if (tier >= 3) {
          const insetScale = tier === 4 ? 0.6 : 0.78;
          const iw = roofW * insetScale, ih = roofH * insetScale;
          const iox = -w * 0.07, ioy = -h * 0.09;
          octx.fillStyle = 'rgba(4,6,8,0.3)';
          roundedRectPath(octx, iox - iw / 2 + iw * 0.05, ioy - ih / 2 + ih * 0.07, iw, ih, cr * 0.8);
          octx.fill();
          const upperRgb = lerpRgb(palette[0], palette[1], Math.min(1, cellHash(seedX, seedY, i, j, 231) * 0.5 + 0.45));
          octx.fillStyle = rgba(upperRgb, 0.98);
          roundedRectPath(octx, iox - iw / 2, ioy - ih / 2, iw, ih, cr * 0.8);
          octx.fill();
          octx.strokeStyle = 'rgba(14,11,8,0.55)';
          octx.lineWidth = Math.max(0.7, ts * 0.026);
          roundedRectPath(octx, iox - iw / 2, ioy - ih / 2, iw, ih, cr * 0.8);
          octx.stroke();
          if (detailKind >= 0) {
            octx.save();
            octx.translate(iox, ioy);
            drawRoofDetail(octx, iw, ih, detailKind, rolls);
            octx.restore();
          }
        } else if (detailKind >= 0) {
          drawRoofDetail(octx, roofW, roofH, detailKind, rolls);
        }

        octx.restore();

        // corner highlight lick for non-glass buildings only (glass towers
        // already got their diagonal sheen above) — cool neutral tint, not
        // the old warm sunlit-stone tone, so concrete/off-white low-rise
        // reads modern rather than sun-baked medieval.
        if (!isGlass) {
          softBlob(octx, bx - w * 0.28, by - h * 0.3, Math.min(w, h) * 0.34, Math.min(w, h) * 0.24, bRot,
            [232, 236, 236], 0.05 + cellHash(seedX, seedY, i, j, 217) * 0.045);
        }

        // outer-density "yard" hint: a faint fence outline around a
        // detached structure, selling "edge of town" instead of "block ran
        // out of room"
        if (density < 0.3) drawYardFence(octx, wx, wy, w, h, ts, i, j);
      }
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
    // SPECIAL/CIVIC BUILDINGS (designer: "special buildings" — landmarks
    // beyond just the exact center). Cities only (towns keep their single
    // small plaza+monument landmark from drawLandmark above); drawn last, on
    // top of ordinary blocks, same "centerpiece always reads clearly" reason
    // the main landmark is drawn last.
    if (isCity) drawSpecialBuildings(octx, map, c, ts, isCapital);
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
// smaller set-back tier offset up-and-left, both with a glass sheen — a
// cheap top-down fake extrusion that reads as "this is taller than
// everything around it" without needing real 3D. `crown` adds a lit
// rooftop penthouse block plus a thin antenna mast with a beacon light on
// the tallest landmark (the central skyscraper), giving it a genuinely
// distinct, MODERN silhouette — a glass tower with a lit crown, not a
// medieval spired keep.
function drawTowerBlock(octx, x, y, size, dark, light, crown) {
  octx.save();
  octx.fillStyle = 'rgba(4,6,8,0.45)';
  roundedRectPath(octx, x - size / 2 + size * 0.22, y - size / 2 + size * 0.3, size, size, size * 0.1);
  octx.fill();

  octx.fillStyle = dark;
  roundedRectPath(octx, x - size / 2, y - size / 2, size, size, size * 0.1);
  octx.fill();
  octx.strokeStyle = 'rgba(14,16,18,0.5)';
  octx.lineWidth = Math.max(0.8, size * 0.045);
  octx.stroke();

  // glass curtain-wall sheen across the base tier
  octx.save();
  roundedRectPath(octx, x - size / 2, y - size / 2, size, size, size * 0.1);
  octx.clip();
  const sheen = octx.createLinearGradient(x - size / 2, y - size / 2, x + size / 2, y + size / 2);
  sheen.addColorStop(0, 'rgba(255,255,255,0)');
  sheen.addColorStop(0.42, 'rgba(255,255,255,0)');
  sheen.addColorStop(0.5, 'rgba(226,242,246,0.3)');
  sheen.addColorStop(0.58, 'rgba(255,255,255,0)');
  sheen.addColorStop(1, 'rgba(255,255,255,0)');
  octx.fillStyle = sheen;
  octx.fillRect(x - size / 2, y - size / 2, size, size);
  octx.restore();

  const off = size * 0.3;
  octx.fillStyle = light;
  roundedRectPath(octx, x - size * 0.34 - off * 0.25, y - size * 0.34 - off, size * 0.68, size * 0.68, size * 0.09);
  octx.fill();
  octx.strokeStyle = 'rgba(14,16,18,0.45)';
  octx.lineWidth = Math.max(0.6, size * 0.035);
  octx.stroke();

  if (crown) {
    // lit rooftop penthouse block + antenna mast with a beacon — the modern
    // "capital skyscraper" silhouette, replacing the old pitched spire
    const cw = size * 0.34, ch = size * 0.28;
    const ccx = x - off * 0.25, ccy = y - size * 0.34 - off - ch * 0.62;
    octx.fillStyle = light;
    roundedRectPath(octx, ccx - cw / 2, ccy - ch / 2, cw, ch, cw * 0.18);
    octx.fill();
    octx.strokeStyle = 'rgba(232,246,248,0.85)';
    octx.lineWidth = Math.max(0.7, size * 0.028);
    octx.stroke();
    octx.strokeStyle = 'rgba(40,44,46,0.85)';
    octx.lineWidth = Math.max(0.6, size * 0.022);
    octx.beginPath();
    octx.moveTo(ccx, ccy - ch / 2);
    octx.lineTo(ccx, ccy - ch / 2 - size * 0.36);
    octx.stroke();
    octx.beginPath();
    octx.arc(ccx, ccy - ch / 2 - size * 0.36, size * 0.045, 0, Math.PI * 2);
    octx.fillStyle = 'rgba(255,92,70,0.92)';
    octx.fill();
  }
  octx.restore();
  softBlob(octx, x - size * 0.15 - off * 0.25, y - size * 0.15 - off, size * 0.75, size * 0.55, 0, [222, 236, 238], 0.11);
}

// THE central landmark — the single biggest "this is a real, MODERN city
// with a heart" lever. A city (r >= 6) gets a civic plaza around a tall
// central glass skyscraper (crowned with a lit penthouse + antenna beacon,
// distinctly colored from every ordinary roof palette); the capital
// additionally gets a small cluster of secondary glass towers around it —
// a downtown skyline cluster, not defensive citadel bastions — and a
// grander crowned centerpiece (scaled further by its own `r`, which
// mapgen.js already guarantees is the largest — no new "is capital" field
// needed, same trick drawLabels already uses for the bold/gold city name).
// A town (r < 6) gets a much smaller modern civic-plaza version — present,
// but never competing with a real city's landmark for visual weight.
function drawLandmark(octx, c, ts, isCity, isCapital) {
  const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const h = (salt) => hash2(seedX, seedY, salt);

  if (!isCity) {
    const R = ts * 0.62;
    octx.beginPath();
    octx.ellipse(cx, cy, R, R * 0.82, h(720) * Math.PI, 0, Math.PI * 2);
    octx.fillStyle = 'rgba(148,150,146,0.30)';
    octx.fill();
    drawTowerBlock(octx, cx + ts * 0.02, cy - ts * 0.02, ts * 0.5, '#6f7478', '#a7b1b4', false);
    return;
  }

  const scale = isCapital ? 1.5 : 1.0;
  const compoundR = ts * (1.3 + (c.r / 10) * 1.1) * scale;

  // civic plaza fronting the tower — cool paved-concrete tone (was a warm
  // tan "walled compound" wash)
  octx.beginPath();
  octx.ellipse(cx, cy, compoundR * 1.2, compoundR * 1.05, h(721) * 0.3, 0, Math.PI * 2);
  octx.fillStyle = 'rgba(146,148,144,0.26)';
  octx.fill();

  // plaza edge — a dashed landscaped ring (paths/planters around the
  // square), NOT a fortified perimeter wall: a modern civic square, not a
  // medieval citadel curtain wall
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
  octx.strokeStyle = 'rgba(108,124,116,0.42)';
  octx.lineWidth = Math.max(1, ts * 0.028);
  octx.setLineDash([ts * 0.1, ts * 0.08]);
  octx.stroke();
  octx.setLineDash([]);
  octx.restore();

  // capital only: a small cluster of secondary glass towers around the
  // centerpiece — reads as a downtown skyline cluster
  if (isCapital) {
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2 + h(723) * Math.PI * 0.2;
      const tx = cx + Math.cos(a) * compoundR * 0.82, ty = cy + Math.sin(a) * compoundR * 0.82;
      drawTowerBlock(octx, tx, ty, ts * 0.46, '#33434b', '#6f97a1', false);
    }
  }

  // central tower — the tallest, most distinctly colored silhouette in the
  // whole city: a glass skyscraper with a lit crown for the capital, a
  // smaller modern civic/office tower for a lesser city
  const bodyDark = isCapital ? '#233039' : '#3a454c';
  const bodyLight = isCapital ? '#6fb4c4' : '#82949a';
  drawTowerBlock(octx, cx, cy, ts * (isCapital ? 1.05 : 0.75), bodyDark, bodyLight, true);
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

// ---------------------------------------------------------------------------
// B5b SPECIAL/CIVIC BUILDINGS — designer feedback: "them being larger and
// straighter with special buildings". drawLandmark above already gives every
// city its central citadel/town-hall; this adds a SPARSE handful of
// additional, visually distinct structures scattered through the rest of a
// larger city, so it has recognizable landmarks throughout rather than just
// one at dead center. Hash-placed off the settlement's own seed exactly like
// everything else in this file — deterministic, no Math.random.
// ---------------------------------------------------------------------------

// Slim glass communications/observation tower — the tallest, narrowest
// silhouette of the three special kinds (replaces the old church + steeple),
// meant to read as a tower poking up out of the roofline from across the
// city: a glass shaft with a stepped setback and an antenna + beacon light.
function drawCommsTower(octx, x, y, ts, rot) {
  octx.save();
  octx.translate(x, y);
  octx.rotate(rot);
  const s = ts * 0.85;
  octx.fillStyle = 'rgba(6,8,10,0.4)';
  roundedRectPath(octx, -s * 0.28 + s * 0.1, -s * 0.28 + s * 0.14, s * 0.56, s * 0.56, s * 0.08);
  octx.fill();

  octx.fillStyle = '#33434b';
  roundedRectPath(octx, -s * 0.28, -s * 0.28, s * 0.56, s * 0.56, s * 0.08);
  octx.fill();
  octx.strokeStyle = 'rgba(14,16,18,0.55)';
  octx.lineWidth = Math.max(0.8, ts * 0.03);
  octx.stroke();

  // glass curtain-wall sheen
  octx.save();
  roundedRectPath(octx, -s * 0.28, -s * 0.28, s * 0.56, s * 0.56, s * 0.08);
  octx.clip();
  const sheen = octx.createLinearGradient(-s * 0.28, -s * 0.28, s * 0.28, s * 0.28);
  sheen.addColorStop(0, 'rgba(255,255,255,0)');
  sheen.addColorStop(0.45, 'rgba(255,255,255,0)');
  sheen.addColorStop(0.55, 'rgba(224,242,246,0.35)');
  sheen.addColorStop(0.65, 'rgba(255,255,255,0)');
  sheen.addColorStop(1, 'rgba(255,255,255,0)');
  octx.fillStyle = sheen;
  octx.fillRect(-s * 0.28, -s * 0.28, s * 0.56, s * 0.56);
  octx.restore();

  // stepped setback upper shaft
  const uw = s * 0.34;
  octx.fillStyle = '#6f97a1';
  roundedRectPath(octx, -uw / 2, -s * 0.5, uw, s * 0.34, uw * 0.15);
  octx.fill();
  octx.strokeStyle = 'rgba(14,16,18,0.5)';
  octx.lineWidth = Math.max(0.6, ts * 0.022);
  octx.stroke();

  // antenna mast + beacon
  octx.strokeStyle = 'rgba(40,44,46,0.85)';
  octx.lineWidth = Math.max(0.6, ts * 0.02);
  octx.beginPath(); octx.moveTo(0, -s * 0.5); octx.lineTo(0, -s * 0.98); octx.stroke();
  octx.beginPath();
  octx.arc(0, -s * 0.98, s * 0.04, 0, Math.PI * 2);
  octx.fillStyle = 'rgba(255,92,70,0.9)';
  octx.fill();
  octx.restore();
  softBlob(octx, x, y - s * 0.15, s * 0.85, s * 0.6, rot, [212, 232, 236], 0.09);
}

// Civic/commercial complex — a wide flat-roofed modern building (mall,
// convention center, or office block): a glass entrance canopy along the
// front edge, rooftop skylight strips, and a small rooftop sign pylon —
// replaces the old colonnade + clock-tower town hall for a contemporary
// anchor building. The widest, most low-slung of the three special kinds,
// meant to anchor a plaza the way a real civic/commercial complex would.
function drawCivicComplex(octx, x, y, ts, rot) {
  octx.save();
  octx.translate(x, y);
  octx.rotate(rot);
  const w = ts * 1.85, h = ts * 1.2;
  octx.fillStyle = 'rgba(6,8,10,0.35)';
  roundedRectPath(octx, -w / 2 + w * 0.05, -h / 2 + h * 0.1, w, h, Math.min(w, h) * 0.08);
  octx.fill();

  const roofRgb = lerpRgb(CIVIC_PALETTE[0], CIVIC_PALETTE[1], 0.4);
  octx.fillStyle = rgba(roofRgb, 0.96);
  roundedRectPath(octx, -w / 2, -h / 2, w, h, Math.min(w, h) * 0.08);
  octx.fill();
  octx.strokeStyle = 'rgba(14,11,8,0.55)';
  octx.lineWidth = Math.max(0.9, ts * 0.03);
  octx.stroke();

  // parapet trim
  octx.strokeStyle = 'rgba(255,255,255,0.16)';
  octx.lineWidth = Math.max(0.5, ts * 0.012);
  roundedRectPath(octx, -w / 2 + w * 0.05, -h / 2 + h * 0.06, w * 0.9, h * 0.88, Math.min(w, h) * 0.06);
  octx.stroke();

  // glass entrance canopy along the front edge
  octx.fillStyle = 'rgba(206,228,232,0.5)';
  roundedRectPath(octx, -w * 0.42, h / 2 - h * 0.16, w * 0.84, h * 0.1, h * 0.04);
  octx.fill();

  // rooftop skylight strips
  octx.fillStyle = 'rgba(190,215,222,0.42)';
  for (let k = 0; k < 2; k++) {
    const sy = -h * 0.2 + k * h * 0.32;
    roundedRectPath(octx, -w * 0.34, sy, w * 0.68, h * 0.08, h * 0.02);
    octx.fill();
  }

  // small rooftop sign pylon
  const bs = Math.min(w, h) * 0.3;
  const towerRgb = lerpRgb(CIVIC_PALETTE[0], CIVIC_PALETTE[1], 0.75);
  octx.fillStyle = rgba(towerRgb, 0.95);
  roundedRectPath(octx, -bs * 0.18, -h / 2 - bs * 0.5, bs * 0.36, bs * 0.5, bs * 0.06);
  octx.fill();
  octx.strokeStyle = 'rgba(14,11,8,0.5)';
  octx.lineWidth = Math.max(0.6, ts * 0.02);
  octx.stroke();
  octx.restore();
  softBlob(octx, x, y - h * 0.1, Math.max(w, h) * 0.5, Math.max(w, h) * 0.34, rot, [216, 232, 234], 0.07);
}

// Modern logistics depot/warehouse — a long low flat-roofed shed with
// loading-dock door notches along the front edge and rooftop solar panels +
// vent stacks (replaces the old sawtooth-roof + smokestack factory look) —
// a distinct steel-grey working-building silhouette, reading as depot/
// logistics rather than housing or civic grandeur.
function drawLogisticsDepot(octx, x, y, ts, rot) {
  octx.save();
  octx.translate(x, y);
  octx.rotate(rot);
  const w = ts * 1.7, h = ts * 0.82;
  octx.fillStyle = 'rgba(6,8,10,0.35)';
  roundedRectPath(octx, -w / 2 + w * 0.05, -h / 2 + h * 0.14, w, h, Math.min(w, h) * 0.06);
  octx.fill();

  octx.fillStyle = '#61666a';
  roundedRectPath(octx, -w / 2, -h / 2, w, h, Math.min(w, h) * 0.06);
  octx.fill();
  octx.strokeStyle = 'rgba(14,11,8,0.55)';
  octx.lineWidth = Math.max(0.8, ts * 0.03);
  octx.stroke();

  // loading-dock door notches along the front edge
  octx.fillStyle = 'rgba(30,32,34,0.6)';
  const doors = 4;
  for (let i = 0; i < doors; i++) {
    const dx = -w / 2 + (w / doors) * (i + 0.5);
    roundedRectPath(octx, dx - w * 0.06, h / 2 - h * 0.12, w * 0.12, h * 0.12, h * 0.02);
    octx.fill();
  }

  // rooftop solar panel grid
  const cols = 4, rows = 2;
  const pw = w / cols * 0.78, ph = h / rows * 0.62;
  octx.fillStyle = 'rgba(28,40,56,0.75)';
  for (let ry = 0; ry < rows; ry++) for (let rx = 0; rx < cols; rx++) {
    const px = -w / 2 + (rx + 0.5) * (w / cols), py = -h / 2 + (ry + 0.5) * (h / rows) - h * 0.06;
    roundedRectPath(octx, px - pw / 2, py - ph / 2, pw, ph, 1);
    octx.fill();
  }

  // two small rooftop vent stacks (replaces the single factory smokestack)
  octx.fillStyle = '#3f4440';
  roundedRectPath(octx, -w * 0.36, -h / 2 - h * 0.22, w * 0.045, h * 0.22, w * 0.01);
  octx.fill();
  roundedRectPath(octx, -w * 0.28, -h / 2 - h * 0.16, w * 0.045, h * 0.16, w * 0.01);
  octx.fill();
  octx.restore();
}

const SPECIAL_BUILDING_KINDS = [drawCommsTower, drawCivicComplex, drawLogisticsDepot];

// Scatters a few special/civic buildings through a city (r >= 6), clear of
// the reserved landmark footprint at its exact center. Count scales gently
// with the city's own `r` (bigger city, more landmarks) and stays sparse —
// "a few per city", not a second layer of ordinary blocks. Each candidate
// spot is tried a handful of times against the real terrain/road data so a
// special building never lands on water or a road; a spot that never finds
// valid ground after a few tries is just skipped (rather than forced),
// keeping the whole pass a pure read of existing map data.
function drawSpecialBuildings(octx, map, c, ts, isCapital) {
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const h = (salt) => hash2(seedX, seedY, salt);
  const cx = (c.x + 0.5) * ts, cy = (c.y + 0.5) * ts;
  const R = (c.r || 6) * ts * 0.88;
  const clearR = landmarkClearRadius(c, ts, true, isCapital);
  const count = Math.min(4, Math.max(1, Math.round(((c.r || 6) - 4) * 0.8)));
  const rot = settlementGrid(c, ts).rot;

  for (let i = 0; i < count; i++) {
    let spot = null;
    for (let attempt = 0; attempt < 5 && !spot; attempt++) {
      const salt = 840 + i * 17 + attempt * 3;
      const ang = h(salt + 1) * Math.PI * 2;
      const dist = clearR * 1.35 + h(salt + 2) * Math.max(ts, R * 0.8 - clearR * 1.35);
      const x = cx + Math.cos(ang) * dist, y = cy + Math.sin(ang) * dist;
      const gx = Math.floor(x / ts), gy = Math.floor(y / ts);
      const t = map.terrainAt(gx, gy);
      if (t && t.name === 'urban' && map.roadAt(gx, gy) <= 0) spot = { x, y };
    }
    if (!spot) continue;
    const kind = SPECIAL_BUILDING_KINDS[Math.floor(h(900 + i) * SPECIAL_BUILDING_KINDS.length) % SPECIAL_BUILDING_KINDS.length];
    const jitterRot = rot + (h(910 + i) - 0.5) * 0.12;
    kind(octx, spot.x, spot.y, ts, jitterRot);
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
  ctx.restore(); // pops back to whatever globalAlpha the caller (drawUnit) had set — see that function's B6 alpha param
}

// Exported: main.js's render loop calls this once per LIVE, currently-
// visible unit (fog-filtering happens at the call site, same as before this
// pass). `isHovered` replaces the old `u === hovered` check done inline —
// main.js still owns what "hovered" means, this file just draws the result.
// `alpha` (B6, operational-map battalion presentation) is an OPTIONAL 0..1
// fade, defaulting to 1 — every pre-existing call site that doesn't pass it
// draws exactly as before this phase. main.js's crossfade wiring passes a
// fractional value ONLY for a player battalion's sub-units mid-crossfade
// between the sub-unit view and the B6 formation marker (drawBattalionMarker
// below); loose units and enemy units are never called with anything but
// the default.
export function drawUnit(ctx, worldToScreen, cam, u, isHovered, alpha = 1) {
  if (alpha <= 0.002) return;
  const [sx, sy] = worldToScreen(u.x, u.y);
  const r = u.def.radius * cam.zoom * devicePixelRatio;
  ctx.save();
  if (alpha < 1) ctx.globalAlpha = alpha;

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
  ctx.restore();
}

// ---------------------------------------------------------------------------
// BATTALION MARKERS — B6 (operational-map battalion presentation; CONCEPT.md
// "BATTALIONS, not singular units" -> docs/reference-battalion-design.md
// §3.3's "Hybrid (Recommended)": single formation icon at strategic zoom,
// expand to sub-units at tactical zoom). At strategic zoom a player
// battalion draws as ONE compact marker at its live-sub-unit centroid
// instead of N separate drawUnit() calls; main.js suppresses those sub-units
// while a marker is visible (see that file's render-loop crossfade wiring)
// and owns the zoom thresholds (Z_MARKER_ONLY/Z_UNITS_ONLY) — this function
// only draws at whatever `alpha` it's handed, it never reads cam.zoom to
// decide WHETHER to draw, only to size/scale what it draws once told to.
//
// Deliberately assetless/procedural + fully deterministic: every visual
// choice below is a pure function of the battalion record itself
// (type/side/morale/strength/garrisoned), never Math.random() — redrawing
// the same battalion state twice always produces pixel-identical output.
const BATTALION_MARKER_MIN_PX = 20; // floor so a marker never shrinks to an unreadable speck fully zoomed out
const BATTALION_MARKER_MAX_PX = 34; // ceiling so it doesn't balloon partway through the crossfade band
// Same fresh/steady/shaken/broken semantics as #battalionPanel's
// .battalionMorale.* CSS (index.html) — different literal hex values because
// the panel is paper-themed (light background, --green/--amber/--red) and
// the canvas is this game's dark neon vocabulary (fresh reuses drawUnit's
// own HP-bar green just above; broken/shaken reuse the existing enemy-red/
// warm-amber already in use elsewhere on this map) — same four-way mapping,
// so "green/grey/amber/red" reads identically in both places.
const MORALE_MARKER_COLOR = { fresh: '#6dffb0', steady: '#cfd8e3', shaken: '#ffb84d', broken: '#ff5a5a' };

function liveBattalionCentroid(bn) {
  let sx = 0, sy = 0, n = 0;
  for (const u of bn.units) {
    if (u.hp <= 0) continue;
    sx += u.x; sy += u.y; n++;
  }
  return n ? { x: sx / n, y: sy / n } : null;
}
// Exported so main.js's render loop can build its per-frame marker/
// suppression bookkeeping off the EXACT same alive-unit centroid this
// function itself draws at, rather than duplicating the alive-filter loop
// and risking the two drifting apart.
export function battalionMarkerCentroid(bn) { return liveBattalionCentroid(bn); }

function roundRectPath(ctx, x, y, w, h, r) {
  ctx.moveTo(x + r, y);
  ctx.lineTo(x + w - r, y);
  ctx.arcTo(x + w, y, x + w, y + r, r);
  ctx.lineTo(x + w, y + h - r);
  ctx.arcTo(x + w, y + h, x + w - r, y + h, r);
  ctx.lineTo(x + r, y + h);
  ctx.arcTo(x, y + h, x, y + h - r, r);
  ctx.lineTo(x, y + r);
  ctx.arcTo(x, y, x + r, y, r);
  ctx.closePath();
}

// main.js calls this once per player battalion with >=1 live sub-unit, in a
// second pass AFTER the main unit-draw loop (so markers sit on top of any
// still-fading-in sub-units during the crossfade band rather than under
// them). `alpha` is the crossfade weight (0..1) main.js computed off
// cam.zoom; `isHovered` is a plain boolean, same contract as drawUnit's own
// isHovered param.
export function drawBattalionMarker(ctx, worldToScreen, cam, bn, alpha, isHovered) {
  if (alpha <= 0.002) return;
  const centroid = liveBattalionCentroid(bn);
  if (!centroid) return;
  const [sx, sy] = worldToScreen(centroid.x, centroid.y);
  const dpr = devicePixelRatio;
  const w = Math.max(BATTALION_MARKER_MIN_PX, Math.min(BATTALION_MARKER_MAX_PX, 26 * cam.zoom)) * dpr;
  const h = w * 0.66;
  const side = bn.side === 'player' ? OWNERSHIP_COLORS.player : bn.side === 'enemy' ? OWNERSHIP_COLORS.enemy : OWNERSHIP_COLORS.neutral;
  const state = moraleState(bn);
  const frameColor = MORALE_MARKER_COLOR[state] || MORALE_MARKER_COLOR.steady;
  const { hpFrac } = battalionStrength(bn);

  ctx.save();
  ctx.translate(sx, sy);

  // FRAME — rounded rect filled with faction color, bordered by the morale-
  // state color above (mirrors #battalionPanel's morale-state left-border
  // rule with the same fresh/steady/shaken/broken vocabulary).
  const rx = Math.max(2, w * 0.14);
  ctx.beginPath();
  roundRectPath(ctx, -w / 2, -h / 2, w, h, rx);
  ctx.globalAlpha = alpha * 0.92;
  ctx.fillStyle = side;
  ctx.fill();
  ctx.globalAlpha = alpha;
  ctx.lineWidth = Math.max(1.4, w * 0.07);
  ctx.strokeStyle = frameColor;
  ctx.stroke();

  // TYPE GLYPH — elite: filled diamond (armor-heavy, the "ace" formation);
  // standard: bold X (the classic NATO infantry glyph, matching this
  // template's infantry-heavy roster); militia: a single hollow ring
  // (nothing filled — the "lighter/hollow mark" the brief calls for, reading
  // as irregular/light next to the other two's solid glyphs). Dark ink tone
  // so it reads against the bright faction fill regardless of side color.
  ctx.fillStyle = 'rgba(6,10,18,0.85)';
  ctx.strokeStyle = 'rgba(6,10,18,0.85)';
  const gr = h * 0.32; // glyph half-size, off marker height so it always fits inside the frame
  if (bn.type === 'elite') {
    ctx.beginPath();
    ctx.moveTo(0, -gr); ctx.lineTo(gr, 0); ctx.lineTo(0, gr); ctx.lineTo(-gr, 0);
    ctx.closePath();
    ctx.fill();
  } else if (bn.type === 'militia') {
    ctx.lineWidth = Math.max(1.1, w * 0.05);
    ctx.beginPath();
    ctx.arc(0, 0, gr * 0.72, 0, 6.28);
    ctx.stroke();
  } else { // 'standard', and any future/unknown type falls through to this default
    ctx.lineWidth = Math.max(1.3, w * 0.06);
    ctx.beginPath();
    ctx.moveTo(-gr, -gr); ctx.lineTo(gr, gr);
    ctx.moveTo(gr, -gr); ctx.lineTo(-gr, gr);
    ctx.stroke();
  }

  // STRENGTH BAR — same visual language as drawUnit's own HP bar just above
  // (black backing + green fill scaled to a live fraction) so a chewed-up
  // battalion reads exactly the way a chewed-up single unit already does
  // elsewhere on this map, just anchored under the frame instead of over a
  // unit and scaled off battalionStrength()'s hpFrac instead of one unit's
  // own hp/maxHp.
  const barH = Math.max(2, w * 0.09);
  const barY = h / 2 + Math.max(3, w * 0.16);
  ctx.globalAlpha = alpha * 0.9;
  ctx.fillStyle = 'rgba(0,0,0,.55)';
  ctx.fillRect(-w / 2, barY, w, barH);
  ctx.fillStyle = '#6dffb0';
  ctx.fillRect(-w / 2, barY, w * Math.max(0, Math.min(1, hpFrac)), barH);
  ctx.globalAlpha = alpha;

  // GARRISON BADGE — small house glyph pinned to the frame's top-right
  // corner when bn.garrisoned (game/battalionMorale.js's field — same
  // condition #battalionPanel's "⛨ garrisoned" tag reads, just a shape
  // instead of text since this is a strategic-glance icon, not a panel row).
  if (bn.garrisoned) {
    const bx = w / 2, by = -h / 2;
    const bs = w * 0.36;
    ctx.fillStyle = '#0d1420';
    ctx.beginPath();
    ctx.arc(bx, by, bs * 0.62, 0, 6.28);
    ctx.fill();
    ctx.fillStyle = MORALE_MARKER_COLOR.fresh;
    ctx.beginPath(); // house glyph: a triangular roof over a small square body
    ctx.moveTo(bx, by - bs * 0.34); ctx.lineTo(bx + bs * 0.3, by); ctx.lineTo(bx - bs * 0.3, by);
    ctx.closePath();
    ctx.fill();
    ctx.fillRect(bx - bs * 0.22, by, bs * 0.44, bs * 0.28);
  }

  ctx.restore();

  if (isHovered) {
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = '#fff';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(sx, sy, Math.max(w, h) * 0.72, 0, 6.28);
    ctx.stroke();
    ctx.restore();
  }

  // NAME — only once zoomed past the marker range's own more-zoomed-in end
  // (fades in over a small band so it doesn't pop), same "earn your place on
  // screen" rule the city/deposit labels below already follow in this file —
  // skips entirely at wide zoom-out where N adjacent battalion names would
  // collide into label soup.
  const nameT = Math.max(0, Math.min(1, (cam.zoom - 0.5) / 0.15));
  if (nameT > 0) {
    ctx.save();
    ctx.globalAlpha = alpha * nameT;
    ctx.textAlign = 'center';
    ctx.font = `${Math.max(9, Math.min(12, w * 0.34))}px Consolas, monospace`;
    ctx.lineWidth = 2.5;
    ctx.strokeStyle = 'rgba(4,8,14,0.75)';
    const ny = sy + barY + barH + 11;
    ctx.strokeText(bn.name, sx, ny);
    ctx.fillStyle = 'rgba(255,255,255,0.85)';
    ctx.fillText(bn.name, sx, ny);
    ctx.restore();
  }
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
      const size = 40 * dpr;
      ctx.font = `${size}px Georgia, serif`;
      ctx.fillStyle = 'rgba(255,255,255,0.09)';
      // BUG FIX (region/ocean labels overlapping illegibly): the zigzag
      // below only ever separates labels within the same 3-row bucket by a
      // fixed 30px vertical nudge — it says nothing about horizontal
      // overlap, or about two regions in DIFFERENT row buckets that still
      // land close together on screen once panned/zoomed, so two names
      // routinely rendered on top of each other (this was the actual
      // playtest complaint, e.g. "DOAMKRIVSTEIR SOUND" over "KRIRNOTHVAV
      // BAY"). Simple greedy collision avoidance on top of the existing
      // zigzag (kept as-is — it's still a cheap first pass that helps the
      // common same-row case): measure each label's actual screen-space
      // bounding box and skip drawing it if that box overlaps any label
      // already placed this frame. `map.regions`' own list order is the
      // priority (same "earlier wins" convention the capital-city bold/gold
      // check above uses via list index 0) — deterministic per map, no
      // per-frame sort needed. Dropping the loser is a deliberate,
      // acknowledged simplification (task: "don't over-engineer") — a
      // dropped region's name just doesn't show at this zoom; panning or
      // zooming in enough moves it out of `REGION_ZOOM_THRESHOLD` order and
      // it can win a spot once its neighbor's label leaves screen space.
      const placedBoxes = [];
      regionList.forEach((r, i) => {
        const cx = (r.x0 + r.x1) / 2 * map.tileSize, cy = (r.y0 + r.y1) / 2 * map.tileSize;
        const [sx, sy0] = worldToScreen(cx, cy);
        const zigzag = ((i % 3) - 1) * 30 * dpr;
        const sy = sy0 + zigzag;
        if (sx < -400 || sx > canvas.width + 400 || sy < -200 || sy > canvas.height + 200) return;
        const label = r.name.toUpperCase();
        const halfW = ctx.measureText(label).width / 2;
        // approximate text bounding box: ctx.textBaseline is 'alphabetic'
        // (set once at the top of drawLabels), so most of the glyph height
        // sits ABOVE sy with a small descender allowance below it.
        const box = { x0: sx - halfW, x1: sx + halfW, y0: sy - size * 0.8, y1: sy + size * 0.25 };
        for (const p of placedBoxes) {
          if (box.x0 < p.x1 && box.x1 > p.x0 && box.y0 < p.y1 && box.y1 > p.y0) return; // collides with an already-placed label — drop this one
        }
        placedBoxes.push(box);
        ctx.fillText(label, sx, sy);
      });
    }
    ctx.restore();
  }

  // BUG FIX (flat black void at max zoom-out): this used to be a single flat
  // fillRect — fine while the map fills the screen, but at/near the min-zoom
  // camera limit (attachCameraControls' minZoom) the map's own drawImage
  // only covers a small island in the middle of the canvas, leaving a huge
  // perfectly-flat black rectangle around it that reads as unfinished/
  // missing content rather than "open space." A cheap radial gradient
  // (viewport-space, not world-space — it's a fixed vignette around the
  // canvas center regardless of where the camera is looking, not a real
  // depth/distance-to-map effect) gives that space a soft dark-to-darker
  // falloff instead of one dead-flat tone. One gradient fill per frame is a
  // trivial cost next to everything else frame() already does.
  let clearGrad = null, clearGradW = -1, clearGradH = -1;
  function clear() {
    const w = canvas.width, h = canvas.height;
    if (clearGrad === null || clearGradW !== w || clearGradH !== h) {
      const cx = w / 2, cy = h / 2, r = Math.max(w, h) * 0.75;
      clearGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
      clearGrad.addColorStop(0, '#0b1520');
      clearGrad.addColorStop(0.55, '#070f18');
      clearGrad.addColorStop(1, '#02050a');
      clearGradW = w; clearGradH = h;
    }
    ctx.fillStyle = clearGrad;
    ctx.fillRect(0, 0, w, h);
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
