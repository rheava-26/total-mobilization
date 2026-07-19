// Canvas2D top-down renderer: camera (pan/zoom), tile grid, and a draw-call
// hook the game layer feeds world-space draw functions into every frame.
const TERRAIN_COLOR = { 0: '#2a3f2c', 1: '#1a3a52', 2: '#3a3630', 3: '#33402a' };

export function createRenderer(canvas) {
  const ctx = canvas.getContext('2d');
  const cam = { x: 0, y: 0, zoom: 1 };

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

  function drawTilemap(map) {
    const ts = map.tileSize;
    const [tlx, tly] = screenToWorld(0, 0);
    const [brx, bry] = screenToWorld(canvas.width, canvas.height);
    const gx0 = Math.max(0, Math.floor(tlx / ts)), gy0 = Math.max(0, Math.floor(tly / ts));
    const gx1 = Math.min(map.width, Math.ceil(brx / ts)), gy1 = Math.min(map.height, Math.ceil(bry / ts));
    for (let gy = gy0; gy < gy1; gy++) {
      for (let gx = gx0; gx < gx1; gx++) {
        const t = map.tileAt(gx, gy);
        ctx.fillStyle = TERRAIN_COLOR[t] || '#2a3f2c';
        const [sx, sy] = worldToScreen(gx * ts, gy * ts);
        const size = ts * cam.zoom * devicePixelRatio;
        ctx.fillRect(sx, sy, size + 1, size + 1); // +1 so seams don't show at odd zooms
      }
    }
  }

  function clear() {
    ctx.fillStyle = '#060a12';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
  }

  function frame(map, drawWorld) {
    clear();
    drawTilemap(map);
    ctx.save();
    ctx.translate(...worldToScreen(0, 0));
    ctx.scale(cam.zoom * devicePixelRatio, cam.zoom * devicePixelRatio);
    ctx.translate(-0, -0);
    ctx.restore();
    drawWorld(ctx, worldToScreen, cam);
  }

  return { ctx, cam, canvas, resize, worldToScreen, screenToWorld, frame };
}

// Mouse drag-to-pan + wheel-to-zoom, matching the feel of the original game's
// research-tree pan control (drag empty space; clicks on entities still work).
export function attachCameraControls(canvas, cam, opts = {}) {
  const minZoom = opts.minZoom ?? 0.35, maxZoom = opts.maxZoom ?? 3;
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
