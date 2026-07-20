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
// Soft/painterly look: a solid per-tile base pass (full coverage, subtle
// color jitter) followed by scattered semi-transparent blobs that bleed
// across tile boundaries — including toward a differing neighbor's color,
// which is what dithers a hard edge into an organic transition. Water skips
// the texture pass (it should read calm) and instead gets a lightened band
// near the coast. All of this runs once at load; per-frame cost is a single
// drawImage.
function prerenderTerrain(map) {
  const ts = map.tileSize;
  const off = document.createElement('canvas');
  off.width = map.width * ts;
  off.height = map.height * ts;
  const octx = off.getContext('2d');

  // pass 1: solid, full-coverage base fill with subtle per-tile color jitter
  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      const t = map.terrainAt(gx, gy);
      octx.fillStyle = lerpColor(t.color[0], t.color[1], hash2(gx, gy, 1));
      octx.fillRect(gx * ts, gy * ts, ts, ts);
    }
  }

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

  return off;
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
// proximity to the nearest named city center (map.cities carries {x,y,r} in
// TILE coords already, same as genmap.js authored them) so Syrograd (r=10,
// the biggest by construction) reads as visibly denser/bigger than the
// smaller towns without any special-casing by name. A tile within a couple
// rings of a road gets a placement-probability bump and is nudged away from
// the road tile it's closest to, so blocks loosely "front the street"
// instead of a uniform scatter ignoring the road network entirely.
const ROOF_PALETTES = [
  ['#6b4a34', '#8a6244'], // warm brick/tile
  ['#585049', '#726a5f'], // grey slate
  ['#75563a', '#93704c'], // sun-baked tan
];

function cityDensityAt(map, cities, gx, gy) {
  let best = Infinity;
  for (const c of cities) {
    const d = Math.hypot(gx - c.x, gy - c.y) / Math.max(1, c.r || 6);
    if (d < best) best = d;
  }
  return Math.max(0, 1 - best); // 1 at the very center, 0 at/beyond the city's nominal radius
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

function prerenderCityBlocks(octx, map) {
  const cities = map.cities || [];
  if (!cities.length) return;
  const ts = map.tileSize;
  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      const t = map.terrainAt(gx, gy);
      if (!t || t.name !== 'urban') continue;
      if (map.roadAt(gx, gy) > 0) continue; // leave road tiles clear for the road stroke
      const density = cityDensityAt(map, cities, gx, gy);
      const roadDir = nearestRoadDir(map, gx, gy);
      const placeRoll = hash2(gx, gy, 201);
      const placeChance = 0.5 + density * 0.35 + (roadDir ? 0.13 : 0);
      if (placeRoll > placeChance) continue; // gap: courtyard/alley — keeps it from reading as a solid carpet

      const cx0 = gx * ts + ts / 2, cy0 = gy * ts + ts / 2;

      // small open plazas right at a city core instead of a building, for
      // variety and so the very center doesn't read as maximally packed
      if (density > 0.78 && hash2(gx, gy, 202) < 0.22) {
        octx.fillStyle = 'rgba(140,128,104,0.28)';
        octx.beginPath();
        octx.ellipse(cx0, cy0, ts * 0.55, ts * 0.42, hash2(gx, gy, 203) * Math.PI, 0, Math.PI * 2);
        octx.fill();
        continue;
      }

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
      const sizeScale = 0.8 + density * 0.4; // bigger structures near city centers
      const w = ts * (0.36 + hash2(gx, gy, 212) * 0.42) * sizeScale;
      const h = ts * (0.36 + hash2(gx, gy, 213) * 0.42) * sizeScale;
      const rot = (hash2(gx, gy, 214) - 0.5) * 0.7;

      const palette = ROOF_PALETTES[Math.floor(hash2(gx, gy, 215) * ROOF_PALETTES.length) % ROOF_PALETTES.length];
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
    }
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
function prerenderRoads(octx, map) {
  const ts = map.tileSize;
  const roadColor = ['#8a744f', '#9c8760']; // dirt-tan
  const bridgeColor = ['#6f7681', '#828a94']; // cooler grey-plank tone, reads as "over water"
  const FORWARD = [[1, 0], [0, 1], [1, 1], [1, -1]];
  const BACK = [[-1, 0], [0, -1], [-1, -1], [-1, 1]];

  const isRoad = (gx, gy) => map.roadAt(gx, gy) > 0;
  const isBridge = (gx, gy) => { const t = map.terrainAt(gx, gy); return !!(t && t.water); };
  const tileCenter = (gx, gy) => [gx * ts + ts / 2, gy * ts + ts / 2];

  // Half-segment from (cx,cy) to (mx,my), toned/widened by the tile it's
  // attributed to (gx,gy) — irregular width via the same deterministic hash
  // jitter the terrain texture pass uses, plus a darker, slightly wider
  // stroke drawn first as edging so the road reads at both zoom levels.
  function drawHalf(cx, cy, mx, my, gx, gy) {
    const bridge = isBridge(gx, gy);
    const base = bridge ? bridgeColor : roadColor;
    const width = ts * (bridge ? 0.46 : 0.30 + hash2(gx, gy, 71) * 0.16);
    const col = lerpColor(base[0], base[1], hash2(gx, gy, 72));
    octx.lineCap = 'round';
    octx.strokeStyle = bridge ? 'rgba(10,16,22,0.45)' : 'rgba(24,16,8,0.4)';
    octx.lineWidth = width * 1.5;
    octx.beginPath(); octx.moveTo(cx, cy); octx.lineTo(mx, my); octx.stroke();
    octx.strokeStyle = col;
    octx.lineWidth = width;
    octx.beginPath(); octx.moveTo(cx, cy); octx.lineTo(mx, my); octx.stroke();
  }

  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      if (!isRoad(gx, gy)) continue;
      const [cx, cy] = tileCenter(gx, gy);
      let connected = false;
      for (const [dx, dy] of FORWARD) {
        const nx = gx + dx, ny = gy + dy;
        if (nx < 0 || ny < 0 || nx >= map.width || ny >= map.height || !isRoad(nx, ny)) continue;
        connected = true;
        const [ncx, ncy] = tileCenter(nx, ny);
        const mx = (cx + ncx) / 2, my = (cy + ncy) / 2;
        drawHalf(cx, cy, mx, my, gx, gy);
        drawHalf(ncx, ncy, mx, my, nx, ny);
      }
      if (!connected) {
        for (const [dx, dy] of BACK) {
          const nx = gx + dx, ny = gy + dy;
          if (nx >= 0 && ny >= 0 && nx < map.width && ny < map.height && isRoad(nx, ny)) { connected = true; break; }
        }
      }
      if (!connected) drawHalf(cx, cy, cx + 0.01, cy + 0.01, gx, gy); // isolated tile: still visible as a dab
    }
  }
}

export function createRenderer(canvas) {
  const ctx = canvas.getContext('2d');
  const cam = { x: 0, y: 0, zoom: 1 };
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
    for (const c of map.cities || []) {
      const [sx, sy] = worldToScreen(c.x * map.tileSize, c.y * map.tileSize);
      if (sx < -150 || sx > canvas.width + 150 || sy < -150 || sy > canvas.height + 150) continue;
      const size = Math.max(11, Math.min(24, 14 * cam.zoom)) * dpr;
      const alpha = Math.max(0.45, Math.min(1, cam.zoom * 1.3));
      const big = c.name === 'Syrograd';
      ctx.font = `${big ? 'bold ' : ''}${size}px Consolas, monospace`;
      const above = (c.r || 4) * map.tileSize * cam.zoom * dpr + 8 * dpr;
      ctx.lineWidth = 3 * dpr;
      ctx.strokeStyle = `rgba(4,8,14,${alpha * 0.8})`;
      ctx.strokeText(c.name, sx, sy - above);
      ctx.fillStyle = big ? `rgba(255,230,150,${alpha})` : `rgba(255,255,255,${alpha})`;
      ctx.fillText(c.name, sx, sy - above);
    }
    if (cam.zoom < REGION_ZOOM_THRESHOLD) {
      for (const r of map.regions || []) {
        const cx = (r.x0 + r.x1) / 2 * map.tileSize, cy = (r.y0 + r.y1) / 2 * map.tileSize;
        const [sx, sy] = worldToScreen(cx, cy);
        if (sx < -400 || sx > canvas.width + 400 || sy < -200 || sy > canvas.height + 200) continue;
        const size = 44 * dpr;
        ctx.font = `${size}px Georgia, serif`;
        ctx.fillStyle = 'rgba(255,255,255,0.09)';
        ctx.fillText(r.name.toUpperCase(), sx, sy);
      }
    }
    ctx.restore();
  }

  function clear() {
    ctx.fillStyle = '#060a12';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
  }

  function frame(map, drawWorld) {
    clear();
    drawTilemap(map);
    drawWorld(ctx, worldToScreen, cam);
    drawLabels(map);
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
