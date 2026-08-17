# NAVAL DOMINANCE — Asset Licensing Log

Every third-party asset shipped in the game, with source and license, so a future paid
release is clean. **Nothing enters the game without an entry here.**

## 3D models (embedded as base64 .glb inside `fleet-real.js`)

| In-game unit | Source model | Creator | License | Obtained via |
|---|---|---|---|---|
| Galleon of the Line (`galleon`) | `ship-dark` | **Kenney** (kenney.nl) | **CC0 1.0** | `pmndrs/market-assets` mirror (`files/models/ship-dark`, info.json creator:"kenney") |
| Brig Corsair (`brig`) | `ship-light` | Kenney | CC0 1.0 | same repo, `ship-light` |
| Armed Longboat (`longboat`) | `boat-large` | Kenney | CC0 1.0 | same repo, `boat-large` |
| Skiff (`skiff2`) | `boat-small` | Kenney | CC0 1.0 | same repo, `boat-small` |
| Shore Cannon (`shoregun`) | `cannon` | Kenney | CC0 1.0 | same repo, `cannon` |

License basis: Kenney publishes all assets on kenney.nl under **Creative Commons Zero (CC0)**
("free to use in personal, educational and commercial projects"). The pmndrs/market-assets
repository (asset backend of market.pmnd.rs) mirrors these with per-model `info.json`
attribution `creator: "kenney"`. CC0 requires no attribution; we credit anyway — thanks Kenney.
Consider a donation at kenney.nl / patreon.com/kenney if this ever ships paid.

Processing: models were Draco-compressed `.gltf` in the mirror; decompressed to plain
self-contained `.glb` at build time with `@gltf-transform` (+`draco3dgltf` decoder), then
base64-embedded. No geometry/texture edits.

## Code
- `three.module.js` + vendored `gltf-loader.js` (GLTFLoader with one inlined helper from
  BufferGeometryUtils): **three.js, MIT license** (npm `three@0.160.0`).

## Known rejects
- `sub` from the same mirror is a **submarine sandwich** (Kenney food pack), not a submarine.
  Do not re-add. (It was briefly in the navy. It has been honorably discharged.)

## Still procedural (no third-party assets)
All other 25 unit models, the ocean, FX, and UI are generated in code. Sounds are procedural
WebAudio (`sfx.js`). Music streams from YouTube (user playlists — swap to licensed tracks
before any paid release; see ROADMAP).
