// Map format v2: hand-authorable STRING-GRID terrain + data-driven terrain
// types (effect lists) + named cities/regions. No sparse overrides, no
// backward compat with the v1 sparse-override format — the map format is
// young and every map in the repo already speaks v2.
//
// A map JSON looks like:
//   { name, tileSize, width, height, terrainDefs?: "terrain-defs.json",
//     legend?: {char: typeName}, grid: ["....", ...], spawns, cities, regions }
// `legend` is optional per-map; if omitted, the shared terrain-defs.json's
// own `legend` is used. `terrainDefs` is a URL relative to the map file
// (defaults to "terrain-defs.json" next to it) pointing at the shared
// {legend, types} library — new terrain = a new entry there, no code change.
export async function loadMap(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`failed to load map: ${url} (${res.status})`);
  const data = await res.json();
  const { tileSize, width, height, spawns, cities, regions, grid: rows } = data;

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

  // Bumped whenever terrain is mutated (future phases: roads, entrenchment,
  // craters). The renderer's pre-render cache checks this to know it needs
  // to redo the offscreen paint instead of trusting a stale bitmap.
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
    worldW() { return width * tileSize; },
    worldH() { return height * tileSize; },
    get version() { return version; },
    // Later phases (roads, fortification, craters) call this after mutating
    // `grid` so the renderer knows its cached prerender is stale.
    dirty() { version++; },
  };
  return map;
}
