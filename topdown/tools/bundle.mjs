#!/usr/bin/env node
// tools/bundle.mjs — packages the whole topdown game (index.html + main.js +
// every game/*.js + engine/*.js module it transitively imports, plus the
// runtime-fetched map JSON under maps/) into ONE self-contained HTML file
// with ZERO external requests, so it can be hosted as a single artifact and
// played by clicking a link.
//
// STRATEGY (see topdown's bundling task writeup for the two options
// considered): a hand-rolled CommonJS-style module registry, not a flat
// concatenation. Flat concatenation was rejected because the real source
// has genuine top-level name collisions across files (e.g. `hash2` is a
// distinct local helper in engine/renderer.js, game/ambient.js,
// game/mapgen.js AND game/news.js; `doctrine.js`/`battalionDoctrine.js`
// share a dozen more) — stripping `import`/`export` and concatenating would
// silently let later files clobber earlier ones' same-named locals. Blob-URL
// module workers were rejected because they don't work from `file://` (the
// task explicitly wants a file:// smoke test to pass).
//
// Instead every module is wrapped in its own function scope:
//   __modules['game/units.js'] = async function(exports, require) { ... };
// and a tiny require()/exports registry (below, emitted into the bundle)
// resolves each module on first use, memoizing its `exports` object. Real
// scoping per file means the name-collision problem above cannot occur
// (each file keeps its own closure), while everything still runs as plain
// synchronous-looking JS that works identically under http:// and file://.
//
// This source graph has NO default exports, NO namespace (`import * as`)
// imports, and NO `export … from` re-exports anywhere (verified against the
// real 34-module import graph before writing this) — every import/export is
// a plain named one, `export { X }` re-export lists appear exactly once, and
// no file exports a mutable `let`/`var` binding relied on across modules.
// That means CommonJS-style "destructure the exports object at require()
// time" is behaviourally equivalent to real ESM live bindings here — EXCEPT
// for the two genuine import cycles in this graph:
//   game/units.js -> game/fog.js -> game/buildings.js -> game/units.js
//   game/units.js -> game/pathfind.js -> game/units.js
// Both cycles only cross FUNCTION exports (never a const), which matters
// because real ES modules hoist function-declaration bindings (value and
// all) to the very top of a module's evaluation, before any of that
// module's own top-level statements — including its own imports — run. This
// bundler reproduces that: every module's `export function`/`export async
// function` names are assigned onto `exports` as the FIRST statements in its
// factory (safe because JS itself hoists `function foo(){}` declared
// anywhere in a function body to the top of that body), strictly BEFORE the
// factory's own require() calls run. So a circular require() always sees
// the other module's hoisted functions already published, exactly like a
// real ESM circular import of functions would. `export const` names are
// instead published at the END of the factory (after its whole body has
// run) since nothing in this graph's cycles ever needs a const early, and
// this matches real ESM (a const has no value at all until its declaration
// line executes; reading it earlier is a TDZ error in real modules too).
//
// MAP JSON: engine/tilemap.js `fetch()`es maps/terrain-defs.json always
// (loadGeneratedMap, the default skirmish path — game/mapgen.js generates
// the theater itself, no JSON for it) and, only on the `?map=` deep-link
// path, an authored map file (maps/theater-01.json / maps/slice-01.json).
// Rather than special-case tilemap.js, the bundle installs one global
// `window.fetch` shim (self-contained-fetch.js below) that serves those
// three JSON files from data embedded in the bundle by filename, and falls
// through to the real fetch for everything else (audio — see game/audio.js,
// already tolerant of missing/blocked audio and left untouched).
//
// Regenerate with:  node topdown/tools/bundle.mjs
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '..'); // topdown/
const OUT = join(ROOT, 'dist', 'total-mobilization.html');

// ---------------------------------------------------------------------------
// 1. Resolve the real import graph starting from main.js (mirrors the audit
//    used to write the strategy comment above — kept in the script so a
//    future added/removed import is picked up automatically, no hand
//    maintenance of a file list).
// ---------------------------------------------------------------------------
const IMPORT_RE = /import\s*\{([^}]*)\}\s*from\s*['"](\.[^'"]+)['"]\s*;?/g;

function toId(absPath) {
  return relative(ROOT, absPath).split(require_sep()).join('/');
}
function require_sep() {
  return process.platform === 'win32' ? '\\' : '/';
}

function findImports(src) {
  const out = [];
  let m;
  IMPORT_RE.lastIndex = 0;
  while ((m = IMPORT_RE.exec(src))) {
    out.push({ full: m[0], names: m[1], specifier: m[2] });
  }
  return out;
}

const moduleOrder = []; // ids, dependency-agnostic order (registry doesn't care, but keep it deterministic)
const visited = new Set();
function walk(absPath) {
  const id = toId(absPath);
  if (visited.has(id)) return;
  visited.add(id);
  const src = readFileSync(absPath, 'utf8');
  for (const { specifier } of findImports(src)) {
    const depAbs = join(dirname(absPath), specifier);
    walk(depAbs);
  }
  moduleOrder.push(id);
}
walk(join(ROOT, 'main.js'));

console.log(`[bundle] resolved ${moduleOrder.length} modules (main.js + ${moduleOrder.length - 1} game/engine modules)`);

// ---------------------------------------------------------------------------
// 2. Per-module transform: strip import/export syntax, translate `import`
//    into `require()` calls hoisted to the top of the factory (ahead of the
//    rest of the module's own code, matching real ESM import hoisting —
//    see game/economy.js which has a real, legal mid-file `import` that
//    this must handle identically to one at the top), and collect exported
//    names into the "hoisted" bucket (function/async function — published
//    before requires run) or the "end" bucket (const / `export { … }` —
//    published after the whole body has run).
// ---------------------------------------------------------------------------
const FN_EXPORT_RE = /^export\s+(async\s+function\*?|function\*?)\s+([A-Za-z_$][\w$]*)/gm;
const CLASS_EXPORT_RE = /^export\s+class\s+([A-Za-z_$][\w$]*)/gm;
const CONST_EXPORT_RE = /^export\s+(const|let|var)\s+([A-Za-z_$][\w$]*)\b/gm;
const BARE_EXPORT_RE = /^export\s*\{([^}]*)\}\s*;?\s*$/gm;

function parseNamedList(blob) {
  return blob
    .split(',')
    .map(s => s.trim())
    .filter(Boolean)
    .map(spec => {
      const asMatch = spec.match(/^([A-Za-z_$][\w$]*)\s+as\s+([A-Za-z_$][\w$]*)$/);
      if (asMatch) return { imported: asMatch[1], local: asMatch[2] };
      return { imported: spec, local: spec };
    });
}

function transformModule(id, absPath) {
  let src = readFileSync(absPath, 'utf8');

  // --- imports -> require() calls, hoisted to the top of the factory ---
  const requireLines = [];
  src = src.replace(IMPORT_RE, (full, names, specifier) => {
    const depAbs = join(dirname(absPath), specifier);
    const depId = toId(depAbs);
    const bindings = parseNamedList(names)
      .map(({ imported, local }) => (imported === local ? imported : `${imported}: ${local}`))
      .join(', ');
    requireLines.push(`const { ${bindings} } = require(${JSON.stringify(depId)});`);
    return ''; // remove from original position — hoisted requires are emitted separately below
  });

  // --- exported function/async function declarations: hoisted bucket ---
  const hoistedNames = [];
  FN_EXPORT_RE.lastIndex = 0;
  let m;
  while ((m = FN_EXPORT_RE.exec(src))) hoistedNames.push(m[2]);
  src = src.replace(FN_EXPORT_RE, (full, kind, name) => full.replace(/^export\s+/, ''));

  // --- exported classes: real ESM classes are NOT hoisted-with-value (TDZ,
  //     same as let/const), so these go in the "end" bucket, not "hoisted".
  //     (None exist in the current graph, but handled for robustness.) ---
  const endNames = [];
  CLASS_EXPORT_RE.lastIndex = 0;
  while ((m = CLASS_EXPORT_RE.exec(src))) endNames.push(m[1]);
  src = src.replace(CLASS_EXPORT_RE, (full, name) => full.replace(/^export\s+/, ''));

  // --- exported const/let/var: end bucket ---
  CONST_EXPORT_RE.lastIndex = 0;
  while ((m = CONST_EXPORT_RE.exec(src))) endNames.push(m[2]);
  src = src.replace(CONST_EXPORT_RE, (full, kind, name) => full.replace(/^export\s+/, ''));

  // --- bare `export { a, b as c };` re-export lists (e.g.
  //     game/battalionDoctrine.js's `export { STANCES };`) ---
  const bareExportPairs = [];
  BARE_EXPORT_RE.lastIndex = 0;
  while ((m = BARE_EXPORT_RE.exec(src))) {
    for (const { imported: local, local: exported } of parseNamedList(m[1])) {
      // parseNamedList's {imported, local} is import-shaped (imported as
      // local); for `export { local as exported }` the roles are the
      // opposite way round, so pass the raw spec through the same "as"
      // splitter but read it as {local, exported} instead.
      bareExportPairs.push({ local, exported });
    }
  }
  src = src.replace(BARE_EXPORT_RE, '');

  const hoistBlock = hoistedNames.map(n => `  exports.${n} = ${n};`).join('\n');
  const requireBlock = requireLines.map(l => `  ${l}`).join('\n');
  const endBlock = [
    ...endNames.map(n => `  exports.${n} = ${n};`),
    ...bareExportPairs.map(({ local, exported }) => `  exports.${exported} = ${local};`),
  ].join('\n');

  const bodyIndented = src
    .split('\n')
    .map(line => (line.length ? '  ' + line : line))
    .join('\n');

  return `__modules[${JSON.stringify(id)}] = async function (exports, require) {\n`
    + (hoistBlock ? hoistBlock + '\n' : '')
    + (requireBlock ? requireBlock + '\n' : '')
    + bodyIndented + '\n'
    + (endBlock ? endBlock + '\n' : '')
    + `};\n`;
}

const moduleSources = moduleOrder.map(id => transformModule(id, join(ROOT, id)));

// ---------------------------------------------------------------------------
// 3. Embed the map JSON the game fetches at runtime (engine/tilemap.js).
//    terrain-defs.json is fetched on EVERY boot (loadGeneratedMap, the
//    default procedural-skirmish path — game/mapgen.js generates the
//    theater itself, no map JSON needed for that part). theater-01.json /
//    slice-01.json are only fetched via the `?map=` deep-link path (no
//    SCENARIO_DEFS menu row uses one today — see game/menu.js) but are
//    embedded too so that deep link keeps working self-contained in the
//    bundle rather than silently breaking.
// ---------------------------------------------------------------------------
const MAP_FILES = ['terrain-defs.json', 'theater-01.json', 'slice-01.json'];
const inlineAssets = {};
for (const f of MAP_FILES) {
  inlineAssets[f] = JSON.parse(readFileSync(join(ROOT, 'maps', f), 'utf8'));
}

const fetchShim = `<script>
// SELF-CONTAINED FETCH SHIM — installed before any game module runs.
// engine/tilemap.js fetch()es maps/terrain-defs.json (always) and, only on
// the ?map= deep-link path, an authored map file (theater-01.json /
// slice-01.json). This shim answers those three by filename from data
// embedded in this file instead of hitting the network, so the bundle makes
// ZERO map-related requests. Anything else (game/audio.js's best-effort
// *.mp3 probes) falls through to the real fetch unchanged — audio.js
// already treats a failed/missing/blocked fetch as "use the synth
// fallback," so a 404 or a CSP rejection there is expected and harmless
// (see that file's initAudio()).
(function () {
  var REAL_FETCH = window.fetch ? window.fetch.bind(window) : null;
  var INLINE_JSON = ${JSON.stringify(inlineAssets)};
  window.fetch = function (input, init) {
    try {
      var urlStr = typeof input === 'string' ? input : (input && input.url) || String(input);
      var base = urlStr.split('/').pop().split('?')[0].split('#')[0];
      if (Object.prototype.hasOwnProperty.call(INLINE_JSON, base)) {
        var body = JSON.stringify(INLINE_JSON[base]);
        return Promise.resolve(new Response(body, {
          status: 200, statusText: 'OK', headers: { 'Content-Type': 'application/json' },
        }));
      }
    } catch (e) { /* fall through to the real fetch below */ }
    if (!REAL_FETCH) return Promise.reject(new TypeError('fetch unavailable'));
    try {
      return REAL_FETCH(input, init);
    } catch (e) {
      // Defensive: some strict-CSP environments have been observed to throw
      // synchronously from a blocked fetch() rather than reject the
      // returned promise. game/audio.js's callers only ever chain
      // .then()/.catch() off the RETURN VALUE of fetch(), so a synchronous
      // throw here would otherwise be an uncaught exception; normalize it
      // into the same rejected-promise shape audio.js already handles.
      return Promise.reject(e);
    }
  };
})();
</script>`;

// ---------------------------------------------------------------------------
// 4. The module registry runtime + the concatenated modules + the entry
//    point call, as one <script type="module"> block.
// ---------------------------------------------------------------------------
const registryRuntime = `// --- module registry (CommonJS-style; see tools/bundle.mjs for why) ---
const __modules = Object.create(null);
const __cache = Object.create(null);
function require(id) {
  if (id in __cache) return __cache[id];
  const exports = {};
  __cache[id] = exports; // cache BEFORE running the factory so a circular
                          // require() sees this module's (possibly partial)
                          // exports object instead of recursing forever.
  __modules[id].call(undefined, exports, require);
  return exports;
}
`;

const gameScript = `<script type="module">\n${registryRuntime}\n${moduleSources.join('\n')}\nawait require('main.js');\n</script>`;

// ---------------------------------------------------------------------------
// 5. Splice into index.html in place of <script type="module" src="main.js">.
// ---------------------------------------------------------------------------
let html = readFileSync(join(ROOT, 'index.html'), 'utf8');
const scriptTagRe = /<script\s+type="module"\s+src="main\.js"\s*><\/script>/;
if (!scriptTagRe.test(html)) {
  throw new Error('index.html: could not find <script type="module" src="main.js"></script> to replace');
}
html = html.replace(scriptTagRe, `${fetchShim}\n${gameScript}`);

mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, html);

const sizeKb = (Buffer.byteLength(html, 'utf8') / 1024).toFixed(1);
console.log(`[bundle] wrote ${relative(ROOT, OUT)} (${sizeKb} KiB, ${moduleOrder.length} modules + index.html shell, ${MAP_FILES.length} map JSON files inlined)`);
