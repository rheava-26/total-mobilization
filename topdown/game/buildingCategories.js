// BUILDBAR CATEGORY MAP — playtest feedback ("group buildings... the build
// menu is really ugly, you can't scroll down on it"), CONCEPT.md's settled
// "Presentation & UI overhaul" section, UX fix #2. Pure DATA, same
// attribute-table philosophy as every other *_DEFS table in this codebase
// (game/units.js UNIT_DEFS, game/buildings.js BUILDING_DEFS, ...): a
// building's category is looked up here, never branched on by name inside
// behavior code. main.js reads BUILD_CATEGORIES once to build the grouped,
// scrollable "construction ledger" (see its BUILDBAR section) and reads
// CATEGORY_OF_BUILDING for the reverse lookup the grey-out/highlight logic
// needs. Add a building to game/buildings.js BUILDING_DEFS and list its key
// in exactly one category below — nothing else has to change for it to show
// up correctly grouped in the ledger.
//
// Four groups per the task's own suggested shape (Economy/Infrastructure,
// Industry & Production, Air & Ground Defense, Research & Support) — a
// flavor grouping, not a strict taxonomy (radar is a sensor, not a weapon,
// but it lives with the other "watch the skies/coast" buildings; the two
// intermediate-component facilities — electronicsPlant/alloyForge — are
// structurally ordinary production facilities per buildings.js's own
// comment, so they sit with the rest of Industry & Production).
export const BUILD_CATEGORIES = [
  {
    id: 'economy',
    label: 'Economy & Infrastructure',
    keys: ['factory', 'barracks', 'supplyDepot', 'mine'],
  },
  {
    id: 'industry',
    label: 'Industry & Production',
    keys: [
      'ammunitionPlant', 'tankFactory', 'aircraftPlant', 'shipyard', 'artilleryWorks',
      'droneWorks', 'missileWorks', 'hypersonicWorks', 'electronicsPlant', 'alloyForge',
    ],
  },
  {
    id: 'defense',
    label: 'Air & Ground Defense',
    keys: ['radar', 'gunEmplacement', 'airDefenseWorks', 'guidedWeaponsWorks'],
  },
  {
    id: 'research',
    label: 'Research & Support',
    keys: ['researchBureau', 'signalsWorks'],
  },
];

// Reverse lookup — building key -> category id. Built once from the table
// above so nothing else has to scan BUILD_CATEGORIES itself.
export const CATEGORY_OF_BUILDING = Object.fromEntries(
  BUILD_CATEGORIES.flatMap(c => c.keys.map(k => [k, c.id]))
);
