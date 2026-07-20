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
// DESIGNER'S TO TUNE: this exact roster (4 resources), their colors/glyphs,
// and their terrain affinities are a first-pass set sized only to cover
// CONCEPT.md's illustrative example recipes (steel + chromium + tungsten +
// oil for a tank/aircraft line) — none of it is canon. Swap affinities,
// rename, add a fifth resource, whatever the production-recipe design ends
// up wanting; the shape (id/name/color/glyph/terrainAffinity) is what has to
// stay stable for the rest of the pipeline to keep working.
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
