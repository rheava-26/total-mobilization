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

  return off;
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
