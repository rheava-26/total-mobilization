// Map format v2: hand-authorable STRING-GRID terrain + data-driven terrain
// types (effect lists) + named cities/regions. No sparse overrides, no
// backward compat with the v1 sparse-override format — the map format is
// young and every map in the repo already speaks v2.
//
// A map JSON looks like:
//   { name, tileSize, width, height, terrainDefs?: "terrain-defs.json",
//     legend?: {char: typeName}, grid: ["....", ...], spawns, cities, regions,
//     roads?: ["....", "..r.", ...] }
// `legend` is optional per-map; if omitted, the shared terrain-defs.json's
// own `legend` is used. `terrainDefs` is a URL relative to the map file
// (defaults to "terrain-defs.json" next to it) pointing at the shared
// {legend, types} library — new terrain = a new entry there, no code change.
//
// ROADS (P2) are a parallel per-tile OVERLAY layer, not a terrain type: a
// tile keeps its underlying terrain (grass/forest/river/...) and separately
// carries infrastructure on top. Encoded in JSON as its own string grid,
// same shape as `grid`: '.' = no infrastructure, 'r' = road, 'R' = rail
// (reserved, unused until rail ships). Kept as a plain char grid rather than
// run-length packed — theater-01.json is ~1.3MB either way and a flat grid
// is trivial to diff/regenerate/hand-edit, which matters more at this scale
// than a few KB of savings. `roads` is optional; a map with no roads field
// gets an all-zero infra layer.
export async function loadMap(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`failed to load map: ${url} (${res.status})`);
  const data = await res.json();
  const { tileSize, width, height, spawns, cities, regions, grid: rows, roads: roadRows } = data;

  const defsUrl = new URL(data.terrainDefs || 'terrain-defs.json', new URL(url, location.href)).href;
  const defsRes = await fetch(defsUrl);
  if (!defsRes.ok) throw new Error(`failed to load terrain defs: ${defsUrl} (${defsRes.status})`);
  const defsData = await defsRes.json();
  const legend = data.legend || defsData.legend;

  // Ordered type table: index -> effect list (the def), used both for the
  // packed grid (Uint8Array of indices) and for terrainAt() lookups.
  const typeNames = Object.keys(defsData.types);
  const typeIndexByName = new Map(typeNames.map((n, i) => [n, i]));
  const typeDefs = typeNames.map(n => ({ name: n, ...defsData.types[n] }));

  const charToIndex = new Map();
  for (const [ch, typeName] of Object.entries(legend)) {
    if (!typeIndexByName.has(typeName)) {
      throw new Error(`terrain-defs has no type "${typeName}" (referenced by legend char "${ch}")`);
    }
    charToIndex.set(ch, typeIndexByName.get(typeName));
  }

  if (!Array.isArray(rows) || rows.length !== height) {
    throw new Error(`map "${data.name}" grid has ${rows?.length ?? 0} rows, expected height ${height}`);
  }
  const grid = new Uint8Array(width * height);
  for (let y = 0; y < height; y++) {
    const row = rows[y];
    if (row.length !== width) {
      throw new Error(`map "${data.name}" row ${y} has length ${row.length}, expected width ${width}`);
    }
    for (let x = 0; x < width; x++) {
      const idx = charToIndex.get(row[x]);
      if (idx === undefined) throw new Error(`unknown terrain char "${row[x]}" at (${x},${y}) in "${data.name}"`);
      grid[y * width + x] = idx;
    }
  }

  // Road/rail overlay: 0 = none, 1 = road, 2 = rail (reserved). Independent
  // of `grid` — mutating this never touches underlying terrain, so a road
  // dropped on a river tile is a bridge rather than a terrain rewrite (see
  // game/units.js terrainSample for how that gets consumed by movement).
  const infra = new Uint8Array(width * height);
  if (Array.isArray(roadRows)) {
    if (roadRows.length !== height) {
      throw new Error(`map "${data.name}" roads has ${roadRows.length} rows, expected height ${height}`);
    }
    for (let y = 0; y < height; y++) {
      const row = roadRows[y];
      if (row.length !== width) {
        throw new Error(`map "${data.name}" roads row ${y} has length ${row.length}, expected width ${width}`);
      }
      for (let x = 0; x < width; x++) {
        const ch = row[x];
        infra[y * width + x] = ch === 'r' ? 1 : ch === 'R' ? 2 : 0;
      }
    }
  }

  // Bumped whenever terrain OR the road overlay is mutated. The renderer's
  // pre-render cache checks this to know it needs to redo the offscreen
  // paint instead of trusting a stale bitmap. Pathfinding also keys its
  // path cache off this so a freshly-built road invalidates stale routes.
  let version = 0;

  const map = {
    name: data.name || 'untitled',
    tileSize, width, height,
    typeDefs, legend,
    grid,
    spawns: spawns || [],
    cities: cities || [],
    regions: regions || [],
    tileAt(gx, gy) {
      if (gx < 0 || gy < 0 || gx >= width || gy >= height) return -1;
      return grid[gy * width + gx];
    },
    // Full terrain effect list for a tile, or null if out of bounds.
    terrainAt(gx, gy) {
      const idx = map.tileAt(gx, gy);
      return idx >= 0 ? typeDefs[idx] : null;
    },
    terrainAtWorld(wx, wy) {
      return map.terrainAt(Math.floor(wx / tileSize), Math.floor(wy / tileSize));
    },
    // 0 = none, 1 = road, 2 = rail (reserved). Out-of-bounds reads as 0.
    roadAt(gx, gy) {
      if (gx < 0 || gy < 0 || gx >= width || gy >= height) return 0;
      return infra[gy * width + gx];
    },
    roadAtWorld(wx, wy) {
      return map.roadAt(Math.floor(wx / tileSize), Math.floor(wy / tileSize));
    },
    // Mutates the overlay only — does NOT call dirty() itself, so callers
    // laying many tiles in one action (road construction, genmap) can batch
    // a whole route and bump the version once instead of re-prerendering
    // per tile.
    setRoad(gx, gy, v = 1) {
      if (gx < 0 || gy < 0 || gx >= width || gy >= height) return;
      infra[gy * width + gx] = v;
    },
    worldW() { return width * tileSize; },
    worldH() { return height * tileSize; },
    get version() { return version; },
    // Later phases (fortification, craters) and road construction call this
    // after mutating `grid`/`infra` so the renderer knows its cached
    // prerender is stale.
    dirty() { version++; },
  };
  return map;
}
