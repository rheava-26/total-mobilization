import { generateTheaterWithStats, DEPOSIT_TUNABLES } from '../game/mapgen.js';

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
// run-length packed — a flat grid is trivial to diff/regenerate/hand-edit,
// which matters more at this scale than a few KB of savings. `roads` is
// optional; a map with no roads field gets an all-zero infra layer.
//
// Two map SOURCES share one construction path (buildMap below): an authored
// JSON file fetched from disk (loadMap, the scenario path — e.g.
// maps/theater-01.json) and a freshly procedurally-generated map (
// loadGeneratedMap, the default skirmish path — see game/mapgen.js). Both
// end up with byte-for-byte the same shaped `data` object; buildMap doesn't
// care which produced it.

// Builds the live map object from already-parsed map JSON `data` and
// already-parsed terrain-defs JSON `defsData`. Pure/synchronous — no
// fetching here, so it works identically whether `data` came off the wire
// (loadMap) or straight out of game/mapgen.js's generateTheater (
// loadGeneratedMap).
export function buildMap(data, defsData) {
  const { tileSize, width, height, spawns, cities, regions, deposits, grid: rows, roads: roadRows } = data;
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

  // OBSTACLE LAYER (P2 city layer): 0 = clear, 1 = occupied by a player/enemy
  // BUILDING footprint (game/buildings.js). Independent of `grid`/`infra` for
  // the same reason roads are — a factory sitting on what was grass doesn't
  // rewrite the terrain, it just adds a fact on top that ground movement
  // (terrainSample, game/units.js) and the road planner (roadTileCost,
  // game/pathfind.js) both consult. Buildings are never placed on roads (the
  // construction planner's siting rules reject that), but the check is
  // unconditional here regardless of what's under a blocked tile.
  const blocked = new Uint8Array(width * height);

  // Permanent decals baked into the prerendered terrain bitmap — currently
  // just building-destruction scorch marks. Plain world-px circles; the
  // renderer reads this array on every re-prerender (triggered by dirty()),
  // so a decal survives any later full repaint instead of being a one-off
  // draw that a subsequent version bump would erase.
  const scorches = [];

  const map = {
    name: data.name || 'untitled',
    // Present only for procedurally-generated maps (game/mapgen.js stamps
    // `seed` onto its output) — undefined for an authored map file that
    // never had one. main.js's HUD shows it when present so a player can
    // share/reproduce a good island via ?seed=.
    seed: data.seed,
    tileSize, width, height,
    typeDefs, legend,
    grid,
    spawns: spawns || [],
    cities: cities || [],
    regions: regions || [],
    // RESOURCE DEPOSITS (P3 follow-up — see game/resources.js/game/mapgen.js):
    // optional per map JSON — a procedurally generated map always carries
    // them (game/mapgen.js's placeDeposits), an authored map file may omit
    // `deposits` entirely and just gets none.
    deposits: deposits || [],
    // Nearest deposit whose anchor is within DEPOSIT_TUNABLES.RADIUS tiles of
    // (gx,gy), or null. This is the single rule both the mine-siting check
    // (game/buildings.js isValidPlacement/spawnBuilding) and the map-quality
    // headless checks read — "on/adjacent to a deposit" means within this
    // shared radius, nothing bespoke per caller.
    depositAt(gx, gy) {
      let best = null, bestD = Infinity;
      for (const d of (deposits || [])) {
        const dd = (d.gx - gx) ** 2 + (d.gy - gy) ** 2;
        if (dd <= DEPOSIT_TUNABLES.RADIUS * DEPOSIT_TUNABLES.RADIUS && dd < bestD) { bestD = dd; best = d; }
      }
      return best;
    },
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
    // 0 = clear, 1 = a building footprint tile. Ground move classes treat
    // this as a hard wall (terrainSample, game/units.js); air ignores it
    // (buildings don't block the sky) same as it ignores terrain entirely.
    blockAt(gx, gy) {
      if (gx < 0 || gy < 0 || gx >= width || gy >= height) return 1; // off-map: treat as blocked, not free
      return blocked[gy * width + gx];
    },
    setBlock(gx, gy, v = 1) {
      if (gx < 0 || gy < 0 || gx >= width || gy >= height) return;
      blocked[gy * width + gx] = v;
    },
    // Building destruction leaves a permanent scorch decal at (wx,wy) with
    // world-px radius r. Read by the renderer's prerender pass; caller must
    // still call dirty() to actually trigger a repaint.
    addScorch(wx, wy, r) {
      scorches.push({ x: wx, y: wy, r });
    },
    get scorches() { return scorches; },
    worldW() { return width * tileSize; },
    worldH() { return height * tileSize; },
    get version() { return version; },
    // Later phases (fortification, craters) and road/building construction
    // call this after mutating `grid`/`infra`/`blocked`/`scorches` so the
    // renderer knows its cached prerender is stale, and so pathfind.js's
    // path cache (keyed off map.version) invalidates stale routes.
    dirty() { version++; },
  };
  return map;
}

// Fetches terrain-defs.json (the shared {legend, types} library) relative to
// `defsUrl`. Shared by both loaders below.
async function fetchDefs(defsUrl) {
  const defsRes = await fetch(defsUrl);
  if (!defsRes.ok) throw new Error(`failed to load terrain defs: ${defsUrl} (${defsRes.status})`);
  return defsRes.json();
}

// AUTHORED MAP PATH (scenario/dev-fixture): fetch a map JSON file + its
// terrain-defs, then hand off to buildMap. This is what `?map=file.json`
// loads (see main.js) — e.g. maps/theater-01.json, the regenerable dev
// fixture (tools/genmap.js).
export async function loadMap(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`failed to load map: ${url} (${res.status})`);
  const data = await res.json();
  const defsUrl = new URL(data.terrainDefs || 'terrain-defs.json', new URL(url, location.href)).href;
  const defsData = await fetchDefs(defsUrl);
  return buildMap(data, defsData);
}

// GENERATED MAP PATH (the default skirmish path per docs/CONCEPT.md's
// settled ledger: "maps and all place names are procedurally generated per
// skirmish run"): run the shared generator (game/mapgen.js) for `seed`, then
// hand its output to the exact same buildMap() an authored file goes
// through. terrain-defs.json is still a shared static file (there's nothing
// map-specific about terrain type effect lists), fetched relative to this
// module rather than to any map URL since a generated map has none.
export async function loadGeneratedMap(seed) {
  const t0 = performance.now();
  const { mapData, stats } = generateTheaterWithStats(seed);
  const genMs = performance.now() - t0;
  const defsData = await fetchDefs(new URL('../maps/terrain-defs.json', import.meta.url).href);
  const map = buildMap(mapData, defsData);
  map.genMs = genMs;
  map.genStats = stats;
  return map;
}
