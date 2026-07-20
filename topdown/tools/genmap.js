#!/usr/bin/env node
// Thin CLI wrapper around the shared generator (topdown/game/mapgen.js) —
// regenerates the authored dev-fixture map, maps/theater-01.json. All actual
// generation logic (terrain, cities, roads, procedural names) lives in
// game/mapgen.js so the browser skirmish loader (main.js's
// loadGeneratedMap, via engine/tilemap.js) and this CLI produce identical
// output for the same seed — this file just picks a seed, calls the shared
// generator, and writes the result to disk.
//
// Run with `node genmap.js` from anywhere; it always writes
// ../maps/theater-01.json relative to this file. Same SEED -> byte-identical
// output every time (see game/mapgen.js's determinism guarantee).
//
// Per docs/CONCEPT.md's settled ledger, this fixed seed is a DEV FIXTURE for
// the authored/scenario map-loading path (`?map=maps/theater-01.json`), not
// a canonical island — every skirmish otherwise generates a fresh map from a
// fresh seed at load (see main.js). The names below are whatever seed 1337
// happens to synthesize this run of the generator; nothing about them is
// special-cased or preserved across regenerations.

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { generateTheaterWithStats } from '../game/mapgen.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SEED = 1337;

const { mapData, stats } = generateTheaterWithStats(SEED);

console.log(`mapgen: seed ${SEED} accepted after ${stats.attempts} attempt(s) `
  + `(land ${(stats.landFraction * 100).toFixed(1)}%, ${stats.roadTileCount} road tiles `
  + `across ${stats.roadEdgeCount} routed legs connecting ${stats.cityCount} cities)`);

const outPath = path.join(__dirname, '..', 'maps', 'theater-01.json');
fs.writeFileSync(outPath, JSON.stringify(mapData, null, 0));
console.log('wrote', outPath, `${mapData.width}x${mapData.height}`, '—', mapData.name);
console.log('cities:', mapData.cities.map(c => c.name).join(', '));
