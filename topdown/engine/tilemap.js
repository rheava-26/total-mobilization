// Loads a sparse tile-map JSON and expands it into a flat terrain grid.
// Sparse format: only non-default tiles are listed (as {x,y,t} triples) — an
// empty/omitted "overrides" array means an all-default (grass) field, which is
// enough to prove movement/combat before any real terrain art exists.
export async function loadMap(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`failed to load map: ${url} (${res.status})`);
  const data = await res.json();
  const { tileSize, width, height, terrain, spawns } = data;
  const grid = new Uint8Array(width * height); // 0 = grass by default
  for (const o of data.overrides || []) grid[o.y * width + o.x] = o.t;
  return {
    name: data.name || 'untitled',
    tileSize, width, height,
    terrainNames: terrain,
    grid,
    spawns: spawns || [],
    tileAt(gx, gy) {
      if (gx < 0 || gy < 0 || gx >= width || gy >= height) return null;
      return grid[gy * width + gx];
    },
    worldW() { return width * tileSize; },
    worldH() { return height * tileSize; },
  };
}
