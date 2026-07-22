// STRATEGIC RESOURCE TYPES (P3 follow-up — CONCEPT.md "Supply & Resources as
// Gameplay": named regional deposits, few and meaningful, requiring
// specialized structures to extract). This file is pure data: adding a
// resource is ONE entry in RESOURCE_DEFS below — nothing else in the engine
// (game/mapgen.js's deposit placement, game/buildings.js's mine,
// game/economy.js's stockpiles, engine/renderer.js's markers, main.js's HUD)
// hardcodes a resource id anywhere; they all iterate this table.
//
// Each resource carries a TERRAIN AFFINITY: the terrain type NAMES (matching
// engine/tilemap.js typeDefs `.name`, which matches maps/terrain-defs.json's
// keys) a deposit of this resource tends to spawn in. game/mapgen.js reads
// this generically to place deposits in terrain-appropriate spots — no
// per-map or per-resource special-casing. `coastal: true` is a second,
// orthogonal data flag (not specific to oil) meaning "a water-affinity tile
// only counts if it's near land" — implemented once, generically, in
// mapgen.js's placeDeposits.
//
// ---------------------------------------------------------------------------
// DESIGNER'S TO TUNE: colors/glyphs/terrain affinities below are first-pass
// placeholders — none of it is canon. Swap affinities, rename, add another
// resource, whatever the production-recipe design ends up wanting; the shape
// (id/name/color/glyph/terrainAffinity) is what has to stay stable for the
// rest of the pipeline to keep working.
//
// ---------------------------------------------------------------------------
// DEEPENED SUPPLY CHAIN, PART A (follow-up to the original 4-resource first
// pass — see docs/reference-military-production.md and
// docs/reference-electronics-and-chains.md): the original roster (steel/
// chromium/tungsten/oil) covered CONCEPT.md's illustrative tank example but
// left every advanced unit (aircraft/drones/missiles/hypersonics/air
// defense/guided weapons) approximated with the SAME four basic ores —
// production.js's recipes each carried a `// TODO: richer material
// (titanium/electronics) when the supply chain expands` marker. This block
// cashes those TODOs in with three new MINEABLE raw resources (still plain
// RESOURCE_DEFS entries, still placed as deposits by mapgen.js's
// placeDeposits, still mined by the one generic `mine` building — nothing
// about the pipeline had to change to add them):
//   aluminum   (bauxite) — airframes/light alloys; aircraft & drones lean on
//     this per the reference's "2xxx/7xxx aerospace alloy" chain.
//   titanium   (rutile/ilmenite ore) — high-strength/high-heat structure
//     (engines, hypersonic airframes); aircraft/drones/hypersonics all want
//     it per the reference's titanium supply-chain section.
//   rareEarths (bastnasite-type ore) — guidance/sensor/magnet feedstock
//     (NdFeB magnets, GaN radar T/R modules per the reference's section 3.5);
//     everything with a seeker or a fire-control brain wants it. (A PART B
//     follow-up wires this into an intermediate `electronics` component —
//     see that pass's own commit for the raw->component->system chain.)
// See game/production.js's PRODUCTION_DEFS for exactly which recipe now
// consumes what — this file only defines that the material EXISTS and where
// it's found; production.js is the "why."
export const RESOURCE_DEFS = {
  steel: {
    id: 'steel', name: 'Steel', color: '#a7b3bd', glyph: 'Fe',
    terrainAffinity: ['hills', 'mountain'],
  },
  chromium: {
    id: 'chromium', name: 'Chromium', color: '#7fe0cf', glyph: 'Cr',
    terrainAffinity: ['mountain'],
  },
  tungsten: {
    id: 'tungsten', name: 'Tungsten', color: '#c99ee8', glyph: 'W',
    terrainAffinity: ['hills', 'mountain'],
  },
  oil: {
    id: 'oil', name: 'Oil', color: '#caa24a', glyph: 'Oil',
    // "marsh/coastal/sea shallows" per CONCEPT.md's example — implemented
    // generically as "marsh, beach, or near-shore water", not hardcoded to
    // oil specifically (see `coastal` above).
    terrainAffinity: ['marsh', 'sand', 'water'],
    coastal: true,
  },
  aluminum: {
    id: 'aluminum', name: 'Aluminum', color: '#b9c4d9', glyph: 'Al',
    // bauxite is a lateritic weathering product, not a hard-rock mineral —
    // grassland/forest lowlands rather than mountain/hills, deliberately the
    // opposite terrain lean from steel/chromium/tungsten so the new raw tier
    // doesn't just crowd the same hills.
    terrainAffinity: ['grass', 'forest'],
  },
  titanium: {
    id: 'titanium', name: 'Titanium', color: '#5c7a99', glyph: 'Ti',
    // rutile/ilmenite occurs both as hard-rock igneous deposits (mountain)
    // and as beach placer sands (sand) per the reference doc — the one
    // resource in this roster with a two-terrain-family spread.
    terrainAffinity: ['mountain', 'sand'],
  },
  rareEarths: {
    id: 'rareEarths', name: 'Rare Earths', color: '#e05fc4', glyph: 'REE',
    // bastnasite/monazite associate with carbonatite/alkaline igneous
    // complexes — rugged terrain, same family as chromium/tungsten but kept
    // its own resource so it doesn't silently piggyback on an existing ore.
    terrainAffinity: ['hills', 'mountain'],
  },
};

export const RESOURCE_LIST = Object.values(RESOURCE_DEFS);

// ---------------------------------------------------------------------------
// MINE EXTRACTION TUNABLES — DESIGNER'S TO TUNE. A mine's output scales
// linearly with its deposit's `richness` (see game/mapgen.js DEPOSIT_TUNABLES
// for the richness range deposits are generated with). Picked only to make
// stockpile accrual clearly observable within a short playtest, same spirit
// as game/economy.js's ECONOMY_TUNABLES and game/buildings.js's cost/
// buildTime placeholders — not balanced for a real campaign.
export const MINE_TUNABLES = {
  BASE_RATE_PER_RICHNESS: 0.6, // resource units/sec at deposit richness 1.0
};
