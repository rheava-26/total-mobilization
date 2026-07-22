// Shared procedural theater generator (browser-safe: no node builtins). This
// is the single source of truth for map generation — both the in-browser
// skirmish loader (main.js's loadGeneratedMap, via engine/tilemap.js) and
// the node CLI wrapper (tools/genmap.js, which regenerates the authored
// dev-fixture map file) call generateTheater()/generateTheaterWithStats()
// here. See docs/CONCEPT.md's settled-ledger line: maps and place names are
// procedurally generated per skirmish run — there is no canonical geography
// or canonical name anywhere in this file; every name is synthesized from
// syllable/pattern data below, seeded by the run's seed.
//
// generateTheater(seed) -> mapData, where mapData has the exact shape a map
// JSON file carries (name, tileSize, width, height, terrainDefs, legend,
// grid, roads, cities, regions, spawns, notes, seed). Deterministic: the
// same seed always produces byte-identical JSON.
//
// Approach (unchanged in spirit from the original fixed-seed generator this
// was extracted from): a value-noise heightfield masked into an island
// shape, classified into terrain bands (sea/beach/plains/forest/hills/
// mountains), a steepest-descent river traced from high ground to the sea,
// marsh stamped near the river mouth, five urban blots (biggest is the
// island's "capital", coastal), and a handful of named regions covering the
// island. Everything is derived from the seed — no hand-typed rows, no
// hand-typed names.
//
// RETRY LOOP: a single noise field can occasionally produce a degenerate
// island for an unlucky seed (too little/too much land, an archipelago
// instead of one landmass, too little room for 5 well-spaced cities, or a
// city an A* road route genuinely can't reach). Rather than ship whatever
// came out, generateTheaterWithStats validates each attempt and, on
// failure, deterministically reseeds and tries again (see deriveSeed) up to
// MAX_ATTEMPTS times. The result is always a pure function of the ORIGINAL
// requested seed — retries are internal and reproducible, never random.

import { RESOURCE_LIST } from './resources.js';

export const MAP_WIDTH = 220;
export const MAP_HEIGHT = 150;
export const TILE_SIZE = 32;

const MAX_ATTEMPTS = 24;

// ---------------------------------------------------------------------------
// RESOURCE DEPOSIT PLACEMENT — DESIGNER'S TO TUNE. Every number below is a
// first-pass placeholder sized to make deposits reliably show up (a handful
// per resource per island) across a variety of seeds without crowding the
// map, not balanced for a real campaign. `RADIUS` doubles as the mine-siting
// rule's "on/adjacent" distance (game/buildings.js isValidPlacement reads it
// via map.depositAt, engine/tilemap.js) — a mine anywhere within RADIUS tiles
// of a deposit's anchor counts as sited on that deposit's resource-rich area.
export const DEPOSIT_TUNABLES = {
  MIN_PER_RESOURCE: 1,
  MAX_PER_RESOURCE: 3,
  // DEEPENED SUPPLY CHAIN follow-up: was 14 when only 4 resources (steel/
  // chromium/tungsten/oil) competed for terrain-affinity tiles. With
  // aluminum/titanium/rareEarths added (game/resources.js), 7 raw resources
  // now compete for the same handful of terrain types (mountain/hills
  // especially — steel, chromium, tungsten, titanium, and rareEarths all
  // draw from it), and placeDeposits below places them in RESOURCE_LIST
  // order, so a resource placed late could find most of the qualifying
  // terrain already spacing-excluded by earlier resources. Tightened from
  // 14 to keep coverage reliable across a normal map spread — measured
  // empirically: at 14, ~22% of generated maps rolled ZERO rareEarths
  // deposits (feeds a PART B follow-up's electronics component, so that's a
  // real "can't build the chain at all without importing" risk, not just
  // cosmetic); at 10, that drops to ~2-3% across a 40-seed sample, matching
  // the pre-existing miss rate the original 4-resource roster already lived
  // with for chromium/tungsten. A player who still rolls a bad map always
  // has the import fallback (game/production.js importResource) as the
  // documented "pricier stopgap."
  MIN_SPACING: 10,
  RADIUS: 3, // tiles a mine footprint may sit within to count as "on/adjacent"
  RICHNESS_MIN: 0.6,
  RICHNESS_MAX: 1.6,
  COASTAL_MAX_DIST: 3, // for `coastal` resources, max tiles a water candidate may sit from the nearest land tile
};

// ---------------------------------------------------------------- rng ----
// mulberry32: tiny, fast, deterministic PRNG. Good enough for terrain art
// and for the discrete choices (spine layout, name synthesis, region cuts)
// made along the way.
function mulberry32(seed) {
  let a = seed >>> 0;
  return function rng() {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Deterministic hash of integer coords -> [0,1), independent of generation
// order (unlike the rng stream) so it can be resampled anywhere. Every term
// is combined with Math.imul (true wrapping 32-bit multiply) rather than
// plain `*` — the original fixed-seed version of this function used plain
// multiplication, which was fine when SEED was always the small literal
// 1337, but random per-run seeds now range across the full uint32 space and
// `seed * 2147483647` overflows Number's 53-bit safe-integer precision with
// plain multiplication, quietly degrading the hash. Math.imul keeps every
// intermediate result an exact int32 regardless of seed magnitude.
function hash2(x, y, seed) {
  let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263) + Math.imul(seed | 0, 2147483647)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h = (h ^ (h >>> 16)) >>> 0;
  return h / 4294967296;
}

// Deterministic reseed for retry attempt N of a given requested seed. Pure
// function of (seed, attempt) — same requested seed always walks the same
// sequence of internal attempt-seeds, so generateTheater(seed) stays a pure
// function of `seed` even when it takes more than one attempt internally.
function deriveSeed(seed, attempt) {
  return Math.floor(hash2(attempt, 0x51ed270b, seed) * 4294967296) >>> 0;
}

function pick(rng, arr) { return arr[Math.floor(rng() * arr.length) % arr.length]; }

// --------------------------------------------------------- value noise ----
// Lattice value noise with smoothstep interpolation over a small random grid,
// WRAPPED (toroidal) so callers can freely pass u,v outside [0,1) — several
// call sites multiply u,v by a frequency >1 to layer detail, which would
// otherwise walk off the lattice into out-of-bounds reads (NaN). Wrapping
// just makes it tileable; visually harmless at these frequencies.
function makeNoise(seed, cells) {
  cells = Math.max(2, Math.round(cells));
  const g = new Float32Array(cells * cells);
  for (let i = 0; i < g.length; i++) g[i] = hash2(i, seed, seed * 7 + 3);
  const at = (xi, yi) => g[(((yi % cells) + cells) % cells) * cells + (((xi % cells) + cells) % cells)];
  return function sample(u, v) {
    const fx = u * cells, fy = v * cells;
    const x0 = Math.floor(fx), y0 = Math.floor(fy);
    let tx = fx - x0, ty = fy - y0;
    tx = tx * tx * (3 - 2 * tx); // smoothstep
    ty = ty * ty * (3 - 2 * ty);
    const v00 = at(x0, y0), v10 = at(x0 + 1, y0);
    const v01 = at(x0, y0 + 1), v11 = at(x0 + 1, y0 + 1);
    const a = v00 + (v10 - v00) * tx, b = v01 + (v11 - v01) * tx;
    return a + (b - a) * ty;
  };
}

// Fractal sum of a few octaves of value noise, normalized to [0,1].
function makeFractal(seed, baseCells, octaves = 4) {
  const layers = [];
  let amp = 1, cells = baseCells, total = 0;
  for (let o = 0; o < octaves; o++) {
    layers.push({ sample: makeNoise(seed + o * 101, Math.round(cells)), amp });
    total += amp;
    amp *= 0.5;
    cells *= 2.15;
  }
  return function sample(u, v) {
    let s = 0;
    for (const l of layers) s += l.sample(u, v) * l.amp;
    return s / total;
  };
}

// -------------------------------------------------------- name tables ----
// Pure data: syllable fragments and pattern templates. No specific name
// anywhere in this file is ever privileged or looked up — every name below
// is assembled fresh from these tables each time makeRoot() runs.
//
// Weighted by duplication (plain consonants/vowels appear more than once so
// they're picked more often) rather than a separate weight table — keeps
// pick() a single flat array lookup. Simple onsets/codas dominate so most
// syllables stay easy to say; blends are a seasoning, not the default, and
// makeRoot() below refuses to stack a blend coda directly against a blend
// onset (that's what produced unpronounceable pileups like "rdgl" early on).
const ONSETS_SIMPLE = ['b', 'd', 'f', 'g', 'h', 'k', 'l', 'm', 'n', 'p', 'r', 's', 't', 'v', 'w', 'z'];
const ONSETS_BLEND = ['br', 'dr', 'fr', 'gr', 'kr', 'pr', 'sh', 'st', 'sk', 'sl', 'tr', 'th', 'vr'];
const ONSETS = [...ONSETS_SIMPLE, ...ONSETS_SIMPLE, ...ONSETS_BLEND];

const VOWELS = ['a', 'e', 'i', 'o', 'u', 'a', 'e', 'i', 'o', 'u', 'a', 'o', 'ae', 'ai', 'au', 'ei', 'oa', 'io'];

const CODAS_NONE = ['', '', '', '', '', ''];
const CODAS_SIMPLE = ['n', 'r', 's', 't', 'd', 'k', 'l', 'm', 'g', 'v', 'z'];
const CODAS_BLEND = ['nd', 'rn', 'st', 'ld', 'rd', 'sk', 'th'];
const CODAS = [...CODAS_NONE, ...CODAS_SIMPLE, ...CODAS_SIMPLE, ...CODAS_BLEND];

const SETTLEMENT_WORDS = ['Cross', 'Port', 'Ford', 'Hold', 'Landing', 'Reach', 'Watch',
  'Bridge', 'Gate', 'Rest', 'Harbor', 'Bend', 'Falls', 'Hollow', 'Crest', 'Mills', 'Haven'];

// Region-name suffix pools, keyed by the region's DOMINANT terrain (a data
// table, not a per-map lookup) — a mountain-heavy region can draw
// "Range"/"Ridge", a marshy one "Fen"/"Mire", etc. See classifyRegionFlavor.
const REGION_SUFFIXES = {
  mountain: ['Range', 'Ridge', 'Peaks', 'Crags', 'Highlands', 'Spine'],
  marsh: ['Marsh', 'Fen', 'Mire', 'Bog', 'Wetlands', 'Sloughs'],
  forest: ['Woods', 'Wold', 'Timberland', 'Glade', 'Thicket', 'Weald'],
  hills: ['Downs', 'Highlands', 'Heights', 'Rise', 'Uplands'],
  coast: ['Coast', 'Shore', 'Bay', 'Cape', 'Strand', 'Sound'],
  plains: ['Plains', 'Fields', 'Vale', 'Downs', 'Reach', 'Flats'],
};

// A single syllable: onset (optionally dropped so words can open on a
// vowel) + vowel core + optional coda. `avoidBlendOnset` lets
// the caller veto a multi-letter onset right after a multi-letter coda so
// two syllables never pile 3+ consonants at their seam. Returns
// { text, codaLen } — codaLen feeds the next syllable's avoidBlendOnset
// decision.
function makeSyllable(rng, allowNoOnset, avoidBlendOnset) {
  let onset = (allowNoOnset && rng() < 0.24) ? '' : pick(rng, ONSETS);
  if (avoidBlendOnset && onset.length > 1) onset = pick(rng, ONSETS_SIMPLE);
  const coda = pick(rng, CODAS);
  return { text: onset + pick(rng, VOWELS) + coda, codaLen: coda.length };
}

// 2-3 syllable root word, capitalized. Consonant clusters at syllable seams
// are kept to at most 2 letters (see avoidBlendOnset above) so the result
// stays sayable in one breath instead of reading as a hash.
function makeRoot(rng) {
  const syllableCount = 2 + (rng() < 0.3 ? 1 : 0);
  let word = '';
  let prevCodaLen = 0;
  for (let i = 0; i < syllableCount; i++) {
    const syl = makeSyllable(rng, i === 0, prevCodaLen > 1);
    word += syl.text;
    prevCodaLen = syl.codaLen;
  }
  return word.charAt(0).toUpperCase() + word.slice(1);
}

// Draws a fresh root word guaranteed unique (case-insensitive) within this
// run's `used` set. 40 tries is astronomically more than the ~30^4-sized
// name space ever needs; the final fallback just appends an extra syllable
// rather than ever emitting a numbered duplicate.
function makeUniqueRoot(rng, used) {
  for (let tries = 0; tries < 40; tries++) {
    const w = makeRoot(rng);
    const key = w.toLowerCase();
    if (!used.has(key)) { used.add(key); return w; }
  }
  const extra = makeSyllable(rng, false, false).text;
  const w = makeRoot(rng) + extra.charAt(0).toUpperCase() + extra.slice(1);
  used.add(w.toLowerCase());
  return w;
}

// Picks from `pool` while preferring an entry not already in `usedSet` (a
// run-scoped set of suffix words already spent) — keeps e.g. six regions
// from repeatedly landing on the same "...Strand" ending purely by chance.
// Falls back to any entry (repeats allowed) once the pool is exhausted.
function pickFresh(rng, pool, usedSet) {
  const fresh = pool.filter(w => !usedSet.has(w));
  const choice = pick(rng, fresh.length ? fresh : pool);
  usedSet.add(choice);
  return choice;
}

function makeCityName(rng, used, usedSuffixes) {
  const root = makeUniqueRoot(rng, used);
  const roll = rng();
  if (roll < 0.55) return root; // plain single-word city
  const suffix = pickFresh(rng, SETTLEMENT_WORDS, usedSuffixes);
  if (roll < 0.8) return `${root} ${suffix}`; // spaced compound, e.g. root "Datlas" + "Reach" -> "Datlas Reach"
  return `${root}${suffix.toLowerCase()}`; // merged compound, e.g. root "Naenov" + "cross" -> "Naenovcross"
}

// Short procedural name for a small town/village (SMALL TOWNS follow-up —
// playtest feedback: "small towns aside from the major cities would be
// nice"). Deliberately a single syllable (rarely two) with no forced
// suffix compound, so a town's NAME reads as plainer/shorter than a city's
// even before you see its much smaller footprint on the map — a village is
// "Dun", a city is "Naenovcross". Falls back to a couple of retries on a
// same-run name collision (cheap and the syllable space is large relative
// to a handful of towns) rather than the city path's full extra-syllable
// fallback, which would blur the length distinction this function exists
// to create.
function makeTownName(rng, used, usedSuffixes) {
  let word = '';
  for (let tries = 0; tries < 8; tries++) {
    word = makeSyllable(rng, true, false).text;
    if (rng() < 0.3) word += makeSyllable(rng, false, false).text;
    word = word.charAt(0).toUpperCase() + word.slice(1);
    if (!used.has(word.toLowerCase())) break;
  }
  used.add(word.toLowerCase());
  if (rng() < 0.3) {
    const suffix = pickFresh(rng, SETTLEMENT_WORDS, usedSuffixes);
    return `${word} ${suffix}`;
  }
  return word;
}

function makeRegionName(rng, used, usedSuffixes, flavor) {
  const root = makeUniqueRoot(rng, used);
  const suffix = pickFresh(rng, REGION_SUFFIXES[flavor] || REGION_SUFFIXES.plains, usedSuffixes);
  const roll = rng();
  if (roll < 0.4) return `The ${root} ${suffix}`;
  if (roll < 0.85) return `${root} ${suffix}`;
  return `${root}'s ${suffix}`;
}

// Island name: sometimes named for its capital city (a data-driven
// composition of two already-generated procedural names, not a lookup of
// any fixed place), sometimes its own independent name.
function makeIslandName(rng, used, capitalCityName) {
  const roll = rng();
  if (roll < 0.3) return `The Isle of ${capitalCityName}`;
  const root = makeUniqueRoot(rng, used);
  if (roll < 0.55) return `${root} Island`;
  if (roll < 0.8) return `The ${root} Coast`;
  return root;
}

// ------------------------------------------------------- terrain codes ----
const T_GRASS = '.', T_FOREST = 'f', T_HILLS = 'h', T_MOUNTAIN = 'M',
  T_WATER = '~', T_RIVER = 'r', T_MARSH = 'm', T_URBAN = 'u', T_SAND = 's';

const LEGEND = {
  '.': 'grass', 'f': 'forest', 'h': 'hills', 'M': 'mountain',
  '~': 'water', 'r': 'river', 'm': 'marsh', 'u': 'urban', 's': 'sand',
};

// terrain type NAME -> char, the reverse of LEGEND — lets RESOURCE_DEFS'
// terrainAffinity (game/resources.js, named by terrain type) look up which
// raw terrain char to scan for, without either file hardcoding the other's
// data.
const TYPE_TO_CHAR = Object.fromEntries(Object.entries(LEGEND).map(([ch, name]) => [name, ch]));

function terrainMoveMult(ch) {
  // mirrors maps/terrain-defs.json's moveMult per type
  switch (ch) {
    case T_FOREST: return 0.55;
    case T_HILLS: return 0.7;
    case T_URBAN: return 0.85;
    case T_SAND: return 0.8;
    case T_MARSH: return 0.4;
    default: return 1.0; // grass and anything unlisted
  }
}

// ---------------------------------------------------- landmass check ----
// 8-connected flood fill over "not open sea" (river tiles still count as
// land here — they're carved out of contiguous land, not a second body of
// water splitting the island). Used to reject archipelago/broken-blob
// attempts: the acceptance gate wants ONE main landmass, not several.
function largestLandComponentFraction(terrain, W, H) {
  const isLand = (i) => terrain[i] !== T_WATER;
  const seen = new Uint8Array(W * H);
  let totalLand = 0, best = 0;
  const stack = new Int32Array(W * H);
  for (let start = 0; start < W * H; start++) {
    if (!isLand(start)) continue;
    totalLand++;
    if (seen[start]) continue;
    let sp = 0, size = 0;
    stack[sp++] = start; seen[start] = 1;
    while (sp > 0) {
      const i = stack[--sp];
      size++;
      const x = i % W, y = (i / W) | 0;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        if (!dx && !dy) continue;
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
        const ni = ny * W + nx;
        if (seen[ni] || !isLand(ni)) continue;
        seen[ni] = 1;
        stack[sp++] = ni;
      }
    }
    if (size > best) best = size;
  }
  return { totalLand, largest: best, fraction: totalLand > 0 ? best / totalLand : 0 };
}

// --------------------------------------------------------------- roads ----
// Same binary-heap A* as game/pathfind.js's aStarGrid: 8-directional with
// corner-cut prevention, octile-distance heuristic scaled by a lower-bound
// cost so it stays admissible. This is a deliberate duplication of
// pathfind.js's cost function (not an import) — pathfind.js's roadTileCost
// takes a live `map` object (map.terrainAt/roadAt/blockAt), which doesn't
// exist yet mid-generation; re-deriving cost from the raw terrain/roadFlags
// arrays here is simpler than constructing a throwaway map facade just to
// reuse it.
function roadAStar(W, H, terrain, roadFlags, sx, sy, gx, gy) {
  const SQRT2 = Math.SQRT2;
  const ROAD_REUSE_COST = 0.12;
  const roadTileCost = (x, y) => {
    const ch = terrain[y * W + x];
    if (ch === T_MOUNTAIN) return Infinity;
    if (ch === T_WATER) return Infinity; // open sea: unbridgeable
    if (roadFlags[y * W + x]) return ROAD_REUSE_COST;
    if (ch === T_RIVER) return 4; // bridge: legal but discouraged
    return 1 / Math.max(0.15, terrainMoveMult(ch));
  };
  const NEI = [[1, 0, 1], [-1, 0, 1], [0, 1, 1], [0, -1, 1], [1, 1, SQRT2], [1, -1, SQRT2], [-1, 1, SQRT2], [-1, -1, SQRT2]];
  const n = W * H;
  const gScore = new Float64Array(n).fill(Infinity);
  const cameFrom = new Int32Array(n).fill(-1);
  const closed = new Uint8Array(n);
  const startIdx = sy * W + sx;
  gScore[startIdx] = 0;
  const heap = [];
  function push(node, f) {
    heap.push([f, node]);
    let i = heap.length - 1;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (heap[p][0] <= heap[i][0]) break;
      [heap[p], heap[i]] = [heap[i], heap[p]]; i = p;
    }
  }
  function pop() {
    const top = heap[0], last = heap.pop();
    if (heap.length) {
      heap[0] = last;
      let i = 0;
      for (;;) {
        const l = i * 2 + 1, r = i * 2 + 2; let s = i;
        if (l < heap.length && heap[l][0] < heap[s][0]) s = l;
        if (r < heap.length && heap[r][0] < heap[s][0]) s = r;
        if (s === i) break;
        [heap[s], heap[i]] = [heap[i], heap[s]]; i = s;
      }
    }
    return top[1];
  }
  const h = (x, y) => {
    const dx = Math.abs(x - gx), dy = Math.abs(y - gy);
    const dmin = Math.min(dx, dy), dmax = Math.max(dx, dy);
    return (dmax - dmin + SQRT2 * dmin) * ROAD_REUSE_COST;
  };
  push(startIdx, h(sx, sy));
  while (heap.length) {
    const cur = pop();
    if (closed[cur]) continue;
    closed[cur] = 1;
    const cx = cur % W, cy = (cur / W) | 0;
    if (cx === gx && cy === gy) {
      const path = [];
      let n2 = cur;
      while (n2 !== -1) { path.push({ x: n2 % W, y: (n2 / W) | 0 }); n2 = cameFrom[n2]; }
      path.reverse();
      return path;
    }
    for (const [dx, dy, dist] of NEI) {
      const nx = cx + dx, ny = cy + dy;
      if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
      const nCost = roadTileCost(nx, ny);
      if (!(nCost < Infinity)) continue;
      if (dx !== 0 && dy !== 0) {
        if (!(roadTileCost(cx + dx, cy) < Infinity) || !(roadTileCost(cx, cy + dy) < Infinity)) continue;
      }
      const ni = ny * W + nx;
      if (closed[ni]) continue;
      const tentative = gScore[cur] + dist * nCost;
      if (tentative < gScore[ni]) {
        gScore[ni] = tentative;
        cameFrom[ni] = cur;
        push(ni, tentative + h(nx, ny));
      }
    }
  }
  return null;
}

// ------------------------------------------------------ region terrain ----
// Dominant-terrain classifier for a region rect — purely a tally over the
// tiles actually inside it, so the flavor (and therefore the suffix pool
// makeRegionName draws from) is driven by what the generator actually put
// there, never hardcoded to any region's position or name.
function classifyRegionFlavor(terrain, W, x0, y0, x1, y1) {
  let mountain = 0, hills = 0, forest = 0, marsh = 0, water = 0, sand = 0, total = 0;
  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const ch = terrain[y * W + x];
      total++;
      if (ch === T_MOUNTAIN) mountain++;
      else if (ch === T_HILLS) hills++;
      else if (ch === T_FOREST) forest++;
      else if (ch === T_MARSH) marsh++;
      else if (ch === T_WATER || ch === T_RIVER) water++;
      else if (ch === T_SAND) sand++;
    }
  }
  if (total === 0) return 'plains';
  const coastFrac = (water + sand) / total;
  if (mountain / total > 0.10) return 'mountain';
  if (marsh / total > 0.08) return 'marsh';
  if (forest / total > 0.30) return 'forest';
  if (hills / total > 0.22) return 'hills';
  if (coastFrac > 0.18) return 'coast';
  return 'plains';
}

// ------------------------------------------------------------ deposits ----
// Distance (in tiles, ring search) from a WATER tile to the nearest non-water
// tile — the inverse of the cities section's distToWater above (that one
// measures land-to-water; this measures water-to-land), used only to keep
// `coastal` resources (game/resources.js) out of open ocean and confined to
// genuine shallows/coastline. Returns 0 immediately for a tile that isn't
// water to begin with.
function distToNearestLand(terrain, W, H, x, y, maxR) {
  if (terrain[y * W + x] !== T_WATER) return 0;
  for (let r = 1; r <= maxR; r++) {
    for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
      const ax = x + dx, ay = y + dy;
      if (ax < 0 || ay < 0 || ax >= W || ay >= H) continue;
      if (terrain[ay * W + ax] !== T_WATER) return r;
    }
  }
  return maxR + 1;
}

// Places a handful of named/typed resource deposits per RESOURCE_DEFS entry
// (game/resources.js), each anchored on a tile whose terrain matches that
// resource's `terrainAffinity` — fully data-driven, no resource id or
// terrain name hardcoded here. Runs AFTER cities are stamped into `terrain`
// (T_URBAN tiles are skipped as candidates) so deposits never land under a
// city footprint. Deterministic: draws only from the attempt's own `rng`
// stream, so the same seed always places the same deposits.
function placeDeposits(terrain, W, H, rng) {
  const D = DEPOSIT_TUNABLES;
  const deposits = [];
  for (const res of RESOURCE_LIST) {
    const affinityChars = new Set(res.terrainAffinity.map(n => TYPE_TO_CHAR[n]).filter(Boolean));
    if (!affinityChars.size) continue;
    const candidates = [];
    for (let y = 2; y < H - 2; y++) {
      for (let x = 2; x < W - 2; x++) {
        const ch = terrain[y * W + x];
        if (ch === T_URBAN || !affinityChars.has(ch)) continue;
        if (res.coastal && ch === T_WATER && distToNearestLand(terrain, W, H, x, y, D.COASTAL_MAX_DIST) > D.COASTAL_MAX_DIST) continue;
        candidates.push({ x, y });
      }
    }
    if (!candidates.length) continue; // this island just has none of this terrain — fine, not every resource is guaranteed every map
    const count = D.MIN_PER_RESOURCE + Math.floor(rng() * (D.MAX_PER_RESOURCE - D.MIN_PER_RESOURCE + 1));
    let placed = 0, tries = 0;
    while (placed < count && tries < candidates.length * 4) {
      tries++;
      const c = candidates[Math.floor(rng() * candidates.length)];
      let clear = true;
      for (const d of deposits) {
        if ((d.gx - c.x) ** 2 + (d.gy - c.y) ** 2 < D.MIN_SPACING ** 2) { clear = false; break; }
      }
      if (!clear) continue;
      const richness = D.RICHNESS_MIN + hash2(c.x, c.y, 6151) * (D.RICHNESS_MAX - D.RICHNESS_MIN);
      deposits.push({ type: res.id, gx: c.x, gy: c.y, richness: Math.round(richness * 100) / 100 });
      placed++;
    }
  }
  return deposits;
}

// --------------------------------------------------------- one attempt ----
// Generates a full candidate map for a concrete (already-derived) seed.
// Returns { ok: false, reason } on any acceptance-gate failure, or
// { ok: true, mapData, stats } on success. Never throws for "this seed's
// island turned out badly" — that's an ok:false, not an exception; only
// genuinely unexpected bugs should throw.
function attemptGenerate(seed) {
  const W = MAP_WIDTH, H = MAP_HEIGHT;
  const rng = mulberry32(seed);
  const elevationNoise = makeFractal(seed, 5, 5);
  const moistureNoise = makeFractal(seed + 1000, 6, 4);
  const angleNoise = makeNoise(seed + 2000, 10); // coastline irregularity, sampled around a circle

  // ---- island shape: center/radius jittered per seed for real geographic
  // variety run to run (not just coastline wobble around a fixed center) ----
  const cx = W * (0.46 + rng() * 0.08);
  const cy = H * (0.48 + rng() * 0.12);
  const baseRadius = Math.min(W, H) * (0.42 + rng() * 0.08);

  function islandMask(x, y) {
    const dx = x - cx, dy = y - cy;
    const dist = Math.hypot(dx, dy);
    const angle = Math.atan2(dy, dx);
    const au = (angle + Math.PI) / (2 * Math.PI);
    const wobble = angleNoise(0.5 + 0.35 * Math.cos(au * 2 * Math.PI), 0.5 + 0.35 * Math.sin(au * 2 * Math.PI));
    const radius = baseRadius * (0.72 + wobble * 0.6);
    const t = 1 - dist / radius;
    return Math.max(0, Math.min(1, t * 2.2));
  }

  // ---- mountain spine: a random polyline crossing the island's interior
  // (endpoints + jittered midpoints), not a fixed set of coordinates — the
  // range's position/orientation genuinely differs seed to seed. Two shallow
  // saddles are carved across it so ground forces always have somewhere to
  // funnel through. ----
  function randomSpine() {
    const marginX = W * 0.18, marginY = H * 0.15;
    const ax = marginX + rng() * (W - 2 * marginX);
    const ay = marginY + rng() * (H - 2 * marginY);
    let bx = ax, by = ay, tries = 0;
    const minSpan = Math.min(W, H) * 0.35;
    do {
      bx = marginX + rng() * (W - 2 * marginX);
      by = marginY + rng() * (H - 2 * marginY);
      tries++;
    } while (Math.hypot(bx - ax, by - ay) < minSpan && tries < 20);
    const n = 3 + Math.floor(rng() * 3); // 3..5 points
    const dx = bx - ax, dy = by - ay;
    const perpLen = Math.hypot(dx, dy) || 1;
    const perpx = -dy / perpLen, perpy = dx / perpLen;
    const pts = [];
    for (let i = 0; i < n; i++) {
      const t = n === 1 ? 0 : i / (n - 1);
      const jitter = (rng() - 0.5) * Math.min(W, H) * 0.12;
      pts.push({ x: ax + dx * t + perpx * jitter, y: ay + dy * t + perpy * jitter });
    }
    return pts;
  }
  const spine = randomSpine();

  function randomPasses() {
    const count = 1 + (rng() < 0.7 ? 1 : 0);
    const out = [];
    for (let i = 0; i < count; i++) {
      const segIdx = Math.min(spine.length - 2, Math.floor(rng() * (spine.length - 1)));
      const a = spine[segIdx], b = spine[segIdx + 1];
      const t = 0.25 + rng() * 0.5;
      out.push({ x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t, r: 5 + rng() * 3 });
    }
    return out;
  }
  const passes = randomPasses();

  function distToSegment(x, y, ax, ay, bx, by) {
    const abx = bx - ax, aby = by - ay;
    const len2 = abx * abx + aby * aby;
    let t = len2 > 0 ? ((x - ax) * abx + (y - ay) * aby) / len2 : 0;
    t = Math.max(0, Math.min(1, t));
    return Math.hypot(x - (ax + abx * t), y - (ay + aby * t));
  }
  function distToSpine(x, y) {
    let best = Infinity;
    for (let i = 0; i < spine.length - 1; i++) {
      const a = spine[i], b = spine[i + 1];
      best = Math.min(best, distToSegment(x, y, a.x, a.y, b.x, b.y));
    }
    return best;
  }

  function elevationAt(x, y) {
    const u = x / W, v = y / H;
    const mask = islandMask(x, y);
    if (mask <= 0) return 0;
    let e = mask * (0.26 + elevationNoise(u * 1.6, v * 1.6) * 0.22);
    const jag = elevationNoise(u * 3.4 + 9.1, v * 3.4 + 4.4);
    const coreWidth = (W / 220) * (4 + jag * 4);
    const skirtWidth = coreWidth + (W / 220) * (7 + jag * 4);
    const d = distToSpine(x, y);
    if (d < coreWidth) e += (1 - d / coreWidth) * 0.62 * mask;
    else if (d < skirtWidth) e += (1 - (d - coreWidth) / (skirtWidth - coreWidth)) * 0.2 * mask;
    for (const p of passes) {
      const pd = Math.hypot(x - p.x, y - p.y);
      if (pd < p.r) e -= (1 - pd / p.r) * 0.55;
    }
    return Math.max(0, Math.min(1.4, e));
  }

  const SEA_LEVEL = 0.30;
  const BEACH_BAND = 0.045;
  const HILL_LEVEL = 0.44;
  const MOUNTAIN_LEVEL = 0.60;

  const elevation = new Float32Array(W * H);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) elevation[y * W + x] = elevationAt(x, y);

  function isWater(x, y) {
    if (x < 0 || y < 0 || x >= W || y >= H) return true;
    return elevation[y * W + x] < SEA_LEVEL;
  }

  const terrain = new Array(W * H);
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      const e = elevation[y * W + x];
      const i = y * W + x;
      if (e < SEA_LEVEL) { terrain[i] = T_WATER; continue; }
      if (e < SEA_LEVEL + BEACH_BAND) { terrain[i] = T_SAND; continue; }
      if (e >= MOUNTAIN_LEVEL) { terrain[i] = T_MOUNTAIN; continue; }
      if (e >= HILL_LEVEL) { terrain[i] = T_HILLS; continue; }
      const m = moistureNoise(x / W * 2.4, y / H * 2.4);
      if (e < SEA_LEVEL + BEACH_BAND + 0.05 && m > 0.62) { terrain[i] = T_MARSH; continue; }
      terrain[i] = m > 0.52 ? T_FOREST : T_GRASS;
    }
  }

  // ---- acceptance gate 1: land fraction + single main landmass. Cheap
  // relative to city/road search, so check before doing any of that work. ----
  const { fraction: landFraction, totalLand } = largestLandComponentFraction(terrain, W, H);
  const landOfMap = totalLand / (W * H);
  // The island-mask formula's natural output centers around ~0.20 land
  // coverage (confirmed empirically, and matching the original fixed-seed
  // dev fixture this generator was extracted from — mostly sea around a
  // mid-sized island, consistent with CONCEPT.md's "one theater" scale).
  // The floor here exists to catch genuine degenerates (a sliver of land),
  // not to reject the algorithm's normal output — set well below the
  // observed natural minimum rather than at the natural median.
  if (landOfMap < 0.12 || landOfMap > 0.55) return { ok: false, reason: `land fraction ${landOfMap.toFixed(3)} out of range` };
  if (landFraction < 0.90) return { ok: false, reason: `largest landmass only ${(landFraction * 100).toFixed(1)}% of land (archipelago)` };

  // -------------------------------------------------------------- river ----
  function traceRiver() {
    let best = null, bestE = -1;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const e = elevation[y * W + x];
      if (e > bestE && e < 1.1 && x > W * 0.25 && x < W * 0.75 && y > H * 0.25 && y < H * 0.7) {
        bestE = e; best = { x, y };
      }
    }
    const path = [];
    if (!best) return path;
    let { x, y } = best;
    const visited = new Set();
    for (let steps = 0; steps < W + H; steps++) {
      path.push({ x, y });
      visited.add(x + ',' + y);
      if (isWater(x, y)) break;
      let nx = x, ny = y, ne = elevation[y * W + x];
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        if (dx === 0 && dy === 0) continue;
        const ax = x + dx, ay = y + dy;
        if (ax < 0 || ay < 0 || ax >= W || ay >= H) continue;
        if (visited.has(ax + ',' + ay)) continue;
        const e = elevation[ay * W + ax];
        if (e < ne) { ne = e; nx = ax; ny = ay; }
      }
      if (nx === x && ny === y) break;
      x = nx; y = ny;
    }
    return path;
  }

  const riverPath = traceRiver();
  for (let k = 0; k < riverPath.length; k++) {
    const { x, y } = riverPath[k];
    if (isWater(x, y)) continue;
    terrain[y * W + x] = T_RIVER;
    if (k > riverPath.length * 0.6) {
      const alt = [[1, 0], [0, 1], [-1, 0], [0, -1]][Math.floor(hash2(x, y, 55) * 4)];
      const ax = x + alt[0], ay = y + alt[1];
      if (ax >= 0 && ay >= 0 && ax < W && ay < H && !isWater(ax, ay)) terrain[ay * W + ax] = T_RIVER;
    }
  }
  for (let k = Math.floor(riverPath.length * 0.75); k < riverPath.length; k++) {
    const { x, y } = riverPath[k];
    for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
      const ax = x + dx, ay = y + dy;
      if (ax < 0 || ay < 0 || ax >= W || ay >= H) continue;
      const i = ay * W + ax;
      if (terrain[i] === T_GRASS && hash2(ax, ay, 77) > 0.45) terrain[i] = T_MARSH;
    }
  }

  // ------------------------------------------------------------- cities ----
  function distToWater(x, y, maxR = 6) {
    for (let r = 1; r <= maxR; r++) {
      for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const ax = x + dx, ay = y + dy;
        if (ax < 0 || ay < 0 || ax >= W || ay >= H) return r;
        if (isWater(ax, ay)) return r;
      }
    }
    return maxR + 1;
  }
  function buildableAt(x, y) {
    const t = terrain[y * W + x];
    return t === T_GRASS || t === T_HILLS || t === T_SAND;
  }

  const candidates = [];
  for (let y = 4; y < H - 4; y++) for (let x = 4; x < W - 4; x++) {
    if (buildableAt(x, y)) candidates.push({ x, y, coast: distToWater(x, y) });
  }
  if (candidates.length < 5) return { ok: false, reason: 'too few buildable candidate tiles' };

  function farthestFrom(points, pool) {
    let best = null, bestD = -1;
    for (const c of pool) {
      let d = Infinity;
      for (const p of points) d = Math.min(d, (c.x - p.x) ** 2 + (c.y - p.y) ** 2);
      if (d > bestD) { bestD = d; best = c; }
    }
    return best;
  }

  // Capital: the biggest city, on the coast, biased toward the map's
  // south-west quadrant so it reads as "a" place rather than dead-center —
  // this bias is a layout preference, not tied to any particular name.
  const coastalCandidates = candidates.filter(c => c.coast <= 4);
  if (!coastalCandidates.length) return { ok: false, reason: 'no coastal candidate for capital' };
  let capital = null, capitalBest = -Infinity;
  for (const c of coastalCandidates) {
    const biasX = 1 - Math.abs(c.x / W - 0.4);
    const biasY = 1 - Math.abs(c.y / H - 0.62);
    const score = biasX + biasY - c.coast * 0.05 + hash2(c.x, c.y, 99) * 0.3;
    if (score > capitalBest) { capitalBest = score; capital = c; }
  }

  const chosen = [capital];
  let cityPool = candidates.filter(c => (c.x - capital.x) ** 2 + (c.y - capital.y) ** 2 > 30 * 30);
  while (chosen.length < 5 && cityPool.length) {
    const next = farthestFrom(chosen, cityPool);
    chosen.push(next);
    const nx = next.x, ny = next.y;
    for (let i = cityPool.length - 1; i >= 0; i--) {
      if ((cityPool[i].x - nx) ** 2 + (cityPool[i].y - ny) ** 2 < 22 * 22) cityPool.splice(i, 1);
    }
  }
  if (chosen.length < 5) return { ok: false, reason: `only found room for ${chosen.length}/5 cities` };

  const usedNames = new Set();
  const usedCitySuffixes = new Set();
  const usedRegionSuffixes = new Set();
  const cities = chosen.map((c, i) => ({
    name: makeCityName(rng, usedNames, usedCitySuffixes),
    x: c.x, y: c.y,
    r: i === 0 ? 10 : 6 + Math.floor(hash2(c.x, c.y, 21) * 2), // the capital is deliberately the biggest
  }));

  for (const c of cities) {
    const r = c.r;
    for (let dy = -r - 2; dy <= r + 2; dy++) for (let dx = -r - 2; dx <= r + 2; dx++) {
      const x = c.x + dx, y = c.y + dy;
      if (x < 0 || y < 0 || x >= W || y >= H) continue;
      const d = Math.hypot(dx, dy) + (hash2(x, y, 31) - 0.5) * 3.2;
      if (d > r) continue;
      const i = y * W + x;
      if (terrain[i] === T_WATER || terrain[i] === T_RIVER || terrain[i] === T_MOUNTAIN) continue;
      terrain[i] = T_URBAN;
    }
  }

  // ------------------------------------------------------------ deposits ----
  const deposits = placeDeposits(terrain, W, H, rng);

  // ------------------------------------------------------------- regions ----
  // A 2x3 grid of named regions with randomized cut lines (position varies
  // seed to seed) rather than fixed fractions — each cell's name/flavor
  // comes from what terrain actually landed inside it (classifyRegionFlavor),
  // never from which city happens to be nearby.
  const colCut1 = W * (0.28 + rng() * 0.14);
  const colCut2 = W * (0.58 + rng() * 0.16);
  const rowCut = H * (0.36 + rng() * 0.16);
  const rectSpecs = [
    [0, 0, colCut1, rowCut], [colCut1, 0, colCut2, rowCut], [colCut2, 0, W, rowCut],
    [0, rowCut, colCut1, H], [colCut1, rowCut, colCut2, H], [colCut2, rowCut, W, H],
  ];
  const regions = rectSpecs.map(([x0, y0, x1, y1]) => {
    const ix0 = Math.floor(x0), iy0 = Math.floor(y0), ix1 = Math.floor(x1), iy1 = Math.floor(y1);
    const flavor = classifyRegionFlavor(terrain, W, ix0, iy0, ix1, iy1);
    return { name: makeRegionName(rng, usedNames, usedRegionSuffixes, flavor), x0: ix0, y0: iy0, x1: ix1, y1: iy1 };
  });

  // ------------------------------------------------------------- spawns ----
  const playerSpawn = { x: cities[0].x + (cities[0].r + 6), y: cities[0].y - (cities[0].r + 6) };
  let enemyCity = cities[1];
  for (const c of cities.slice(1)) {
    if ((c.x - capital.x) ** 2 + (c.y - capital.y) ** 2 > (enemyCity.x - capital.x) ** 2 + (enemyCity.y - capital.y) ** 2) enemyCity = c;
  }
  function nearestBeach(cx0, cy0) {
    let best = null, bestD = Infinity;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      if (terrain[y * W + x] !== T_SAND) continue;
      const d = (x - cx0) ** 2 + (y - cy0) ** 2;
      if (d < bestD) { bestD = d; best = { x, y }; }
    }
    return best || { x: cx0, y: cy0 };
  }
  const enemySpawn = nearestBeach(enemyCity.x, enemyCity.y);
  function nearestLand(x, y) {
    if (!isWater(x, y) && terrain[y * W + x] !== T_MOUNTAIN) return { x, y };
    return nearestBeach(x, y);
  }
  const playerSpawnSafe = nearestLand(
    Math.max(0, Math.min(W - 1, playerSpawn.x)),
    Math.max(0, Math.min(H - 1, playerSpawn.y)));
  const spawns = [
    { side: 'player', x: playerSpawnSafe.x, y: playerSpawnSafe.y },
    { side: 'enemy', x: enemySpawn.x, y: enemySpawn.y },
  ];

  // -------------------------------------------------------------- roads ----
  // Minimum spanning tree over the 5 cities (straight-line distance — a fine
  // proxy for "which pairs to connect"; each edge still bends around terrain
  // via A*), then route every MST edge for real. ANY edge that fails to
  // route rejects this whole attempt (see acceptance gate 2 below) — a
  // skipped edge would leave that city outside the road network, which
  // violates "all cities reachable by road."
  const mstEdges = [];
  {
    const inTree = [0];
    const remaining = cities.map((c, i) => i).filter(i => i !== 0);
    while (remaining.length) {
      let best = null, bestD = Infinity, bestI = -1;
      for (let ri = 0; ri < remaining.length; ri++) {
        const j = remaining[ri];
        for (const i of inTree) {
          const d = (cities[i].x - cities[j].x) ** 2 + (cities[i].y - cities[j].y) ** 2;
          if (d < bestD) { bestD = d; best = { a: i, b: j }; bestI = ri; }
        }
      }
      mstEdges.push(best);
      inTree.push(best.b);
      remaining.splice(bestI, 1);
    }
  }

  const roadFlags = new Uint8Array(W * H);
  let roadTileCount = 0;
  for (const edge of mstEdges) {
    const a = cities[edge.a], b = cities[edge.b];
    const routePath = roadAStar(W, H, terrain, roadFlags, a.x, a.y, b.x, b.y);
    if (!routePath) return { ok: false, reason: `no road route between ${cities[edge.a].name} and ${cities[edge.b].name}` };
    for (const { x, y } of routePath) {
      const i = y * W + x;
      if (!roadFlags[i]) { roadFlags[i] = 1; roadTileCount++; }
    }
  }

  // ---- acceptance gate 2: every city actually sits in ONE connected road
  // component (defensive re-check beyond "no edge failed to route" — every
  // MST edge's endpoints are exactly a city's tile, so this should always
  // hold if the loop above completed, but it's cheap to verify directly). ----
  {
    const seen = new Uint8Array(W * H);
    const stack = [];
    const first = cities[0];
    const startIdx = first.y * W + first.x;
    if (roadFlags[startIdx]) {
      stack.push(startIdx); seen[startIdx] = 1;
      while (stack.length) {
        const i = stack.pop();
        const x = i % W, y = (i / W) | 0;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          if (!dx && !dy) continue;
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
          const ni = ny * W + nx;
          if (seen[ni] || !roadFlags[ni]) continue;
          seen[ni] = 1; stack.push(ni);
        }
      }
    }
    for (const c of cities) {
      if (!seen[c.y * W + c.x]) return { ok: false, reason: `${c.name} not in the main road component` };
    }
  }

  // ------------------------------------------------------- small towns ----
  // SMALL TOWNS / VILLAGES — playtest feedback: "the map looks a bit
  // stale — maybe adding small towns aside from the major cities would be
  // nice." Purely map RICHNESS: deliberately kept OUT of `cities` and given
  // their own `towns` array, so game/objectives.js and game/enemyai.js
  // (which only ever read map.cities) never see them at all — v1's "the 5
  // cities are the only objectives" stays exactly true with zero guard code
  // needed here. Placed AFTER the road network is finalized (this section
  // sits right after acceptance gate 2 above) so siting can actually score
  // real proximity to a road, and so each town's optional connector spur
  // below can hug the existing network instead of laying a whole new route.
  //
  // Candidate scan uses a coarse stride (not every tile) purely to keep the
  // one-time generation cost flat — towns don't need single-tile placement
  // precision the way the 5 hand-picked city sites did.
  const TOWN_STRIDE = 2;
  const TOWN_CITY_MARGIN = 12; // min tiles from any of the 5 real cities' edge
  const TOWN_SPACING = 14; // min tiles between two towns
  const TOWN_COUNT = 6 + Math.floor(rng() * 5); // 6..10 — enough to feel like a populated island without crowding it

  function distToRoadFlags(x, y, maxR) {
    for (let r = 1; r <= maxR; r++) {
      for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const ax = x + dx, ay = y + dy;
        if (ax < 0 || ay < 0 || ax >= W || ay >= H) continue;
        if (roadFlags[ay * W + ax]) return r;
      }
    }
    return maxR + 1;
  }

  const townCandidates = [];
  for (let y = 4; y < H - 4; y += TOWN_STRIDE) {
    for (let x = 4; x < W - 4; x += TOWN_STRIDE) {
      // reuses buildableAt/distToWater from the cities section above (same
      // function scope) — "not in the sea, not on a mountain peak" is
      // already exactly what buildableAt enforces (grass/hills/sand only)
      if (!buildableAt(x, y)) continue;
      let tooCloseToCity = false;
      for (const c of cities) {
        if ((x - c.x) ** 2 + (y - c.y) ** 2 < (c.r + TOWN_CITY_MARGIN) ** 2) { tooCloseToCity = true; break; }
      }
      if (tooCloseToCity) continue;
      // score once here (not inside the selection loop below) — a ring
      // search per candidate is the expensive part, and it doesn't depend
      // on which OTHER towns end up chosen, so it only needs computing once
      // per candidate rather than once per (candidate x town) pair
      const roadD = distToRoadFlags(x, y, 9);
      const coastD = distToWater(x, y, 8);
      const score = (roadD <= 9 ? (9 - roadD) : 0) * 0.5 + (coastD <= 8 ? (8 - coastD) : 0) * 0.15;
      townCandidates.push({ x, y, score });
    }
  }

  const towns = [];
  let townPool = townCandidates;
  for (let i = 0; i < TOWN_COUNT && townPool.length; i++) {
    let best = null, bestScore = -Infinity;
    for (const c of townPool) {
      const s = c.score + hash2(c.x, c.y, 8123 + i) * 3; // jitter so towns don't all clump on the single best-scoring spot
      if (s > bestScore) { bestScore = s; best = c; }
    }
    if (!best) break;
    const r = 2 + Math.floor(hash2(best.x, best.y, 8124) * 3); // 2..4 tiles — clearly smaller than any of the 5 cities (r>=6)
    towns.push({ x: best.x, y: best.y, r });
    townPool = townPool.filter(c => (c.x - best.x) ** 2 + (c.y - best.y) ** 2 >= TOWN_SPACING * TOWN_SPACING);
  }

  // stamp T_URBAN tiles for each town — same organic-blob approach the 5
  // cities use just above (irregular hash-jittered edge, not a hard
  // circle), just at a much smaller radius so it visibly reads as "lesser"
  // even before the renderer's density-based city-block painter (engine/
  // renderer.js) gets a look at it.
  for (const t of towns) {
    const r = t.r;
    for (let dy = -r - 1; dy <= r + 1; dy++) for (let dx = -r - 1; dx <= r + 1; dx++) {
      const x = t.x + dx, y = t.y + dy;
      if (x < 0 || y < 0 || x >= W || y >= H) continue;
      const d = Math.hypot(dx, dy) + (hash2(x, y, 41) - 0.5) * 2.0;
      if (d > r) continue;
      const i = y * W + x;
      if (terrain[i] === T_WATER || terrain[i] === T_RIVER || terrain[i] === T_MOUNTAIN) continue;
      terrain[i] = T_URBAN;
    }
  }

  // Connect some towns to the road network "if cheap" (task wording).
  // roadAStar's cost function makes reusing an existing road tile ~8x
  // cheaper than laying fresh road (ROAD_REUSE_COST vs terrain cost), so
  // aiming at the nearest CITY (not literally the nearest road tile) still
  // makes the search hug whatever road segment is closest along the way —
  // it only pays real cost for the genuinely new spur into town. A spur
  // that turns out to need a lot of brand-new tiles (rare — a town stranded
  // far from the network) is simply skipped rather than forced; the town
  // still exists as a settlement, just an unconnected one.
  const TOWN_SPUR_MAX_NEW_TILES = 22;
  for (const t of towns) {
    if (distToRoadFlags(t.x, t.y, 3) <= 3) continue; // already road-adjacent (sits near a city's sprawl) — nothing to add
    let nearestCity = cities[0], bestD = Infinity;
    for (const c of cities) {
      const d = (c.x - t.x) ** 2 + (c.y - t.y) ** 2;
      if (d < bestD) { bestD = d; nearestCity = c; }
    }
    const spur = roadAStar(W, H, terrain, roadFlags, t.x, t.y, nearestCity.x, nearestCity.y);
    if (!spur) continue;
    let newTiles = 0;
    for (const { x, y } of spur) if (!roadFlags[y * W + x]) newTiles++;
    if (newTiles > TOWN_SPUR_MAX_NEW_TILES) continue; // too far from the network to be "cheap" — leave it unconnected
    for (const { x, y } of spur) {
      const i = y * W + x;
      if (!roadFlags[i]) { roadFlags[i] = 1; roadTileCount++; }
    }
  }

  const usedTownSuffixes = new Set();
  for (const t of towns) t.name = makeTownName(rng, usedNames, usedTownSuffixes);

  const roads = [];
  for (let y = 0; y < H; y++) {
    let row = '';
    for (let x = 0; x < W; x++) row += roadFlags[y * W + x] ? 'r' : '.';
    roads.push(row);
  }

  const grid = [];
  for (let y = 0; y < H; y++) grid.push(terrain.slice(y * W, (y + 1) * W).join(''));

  const islandName = makeIslandName(rng, usedNames, cities[0].name);

  const mapData = {
    name: islandName,
    tileSize: TILE_SIZE,
    width: W,
    height: H,
    terrainDefs: 'terrain-defs.json',
    legend: LEGEND,
    grid,
    roads,
    cities,
    // SMALL TOWNS (map-richness follow-up, see the section above) — plain
    // {x,y,r,name} shape, same as `cities`, deliberately a SEPARATE array so
    // nothing that reads map.cities (objectives, enemy AI) is even aware
    // towns exist. An authored map file may omit this entirely (buildMap
    // below defaults it to []), same convention as `deposits`.
    towns,
    regions,
    spawns,
    deposits,
    notes: `Procedurally generated by topdown/game/mapgen.js. Re-run with the same seed for the identical island.`,
  };

  return {
    ok: true,
    mapData,
    stats: {
      landFraction: landOfMap,
      mainLandmassFraction: landFraction,
      cityCount: cities.length,
      townCount: towns.length,
      roadTileCount,
      roadEdgeCount: mstEdges.length,
      depositCount: deposits.length,
    },
  };
}

// ---------------------------------------------------------------- API ----
// generateTheaterWithStats(seed) -> { mapData, stats }. stats carries
// { attempts, requestedSeed, usedInternalSeed, exhausted?, ...attemptStats }
// so callers (the node CLI wrapper, the browser loader, headless tests) can
// report/inspect how many retries a seed cost.
export function generateTheaterWithStats(seed) {
  seed = seed >>> 0;
  let last = null;
  let attempts = 0;
  for (; attempts < MAX_ATTEMPTS; attempts++) {
    const trySeed = attempts === 0 ? seed : deriveSeed(seed, attempts);
    const result = attemptGenerate(trySeed);
    last = { ...result, trySeed };
    if (result.ok) break;
  }
  if (!last.ok) {
    // Every attempt hit an acceptance-gate rejection (bad land fraction,
    // archipelago, no room for 5 cities, or an unroutable road edge). This
    // is not expected to happen in practice — see mapgen's own retry
    // statistics from testing — but failing loudly here beats silently
    // shipping a degenerate island or crashing on a missing mapData field.
    // Callers that need a load to always succeed (main.js) can catch this
    // and retry with an unrelated seed.
    throw new Error(`mapgen: exhausted ${MAX_ATTEMPTS} attempts for seed ${seed} (last reason: ${last.reason})`);
  }
  last.mapData.seed = seed;
  last.stats.attempts = attempts + 1;
  last.stats.requestedSeed = seed;
  last.stats.usedInternalSeed = last.trySeed;
  return { mapData: last.mapData, stats: last.stats };
}

export function generateTheater(seed) {
  return generateTheaterWithStats(seed).mapData;
}
