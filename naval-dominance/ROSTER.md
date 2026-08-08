# NAVAL DOMINANCE — Starter Roster (content, as data)

**These are our own stylized models of real / generic vessel *types*.** Nobody owns a patrol
boat or a destroyer — recreating the *types* and the early-game *lineup* is legally clean
(important because we intend to sell). We do NOT lift Navy Tycoon's literal model files or its
*invented* names. Roster idea: borrowed. Hulls: ours.

Fidelity target: **≥ Navy Simulator (Roblox) tier**, via asset-pack hulls + procedural
detailing (see `OPERATION.md` §Asset policy).

Each entry maps to the `Unit = data` schema (DESIGN §11). Numbers are placeholders for tuning.

| # | Unit | Domain(s) | Weapons (take-overable) | Secondaries | Role |
|---|------|-----------|-------------------------|-------------|------|
| 1 | **Patrol Boat** | sea | 2× HMG | — | fast, cheap, first hull |
| 2 | **Rocket Skiff** | sea | small rocket pods / bombardment | — | glass-cannon harasser |
| 3 | **Gunboat / Corvette** | sea | 1× light deck gun, 1× HMG | — | first "real" warship |
| 4 | **Destroyer** (Z-32-class feel) | sea | main guns (×2–3), AA guns, torpedoes | scout heli (optional) | early capital; the flagship of the vertical slice |
| 5 | **Scout Helicopter** | air | light MG / rockets | — | recon + proves `secondaries`/air |
| 6 | **Light Tank** | land | cannon, coax MG | — | proves `domains:[land]` + ground collision |
| 7 | **Mortar Team/Vehicle** | land | indirect mortar | — | ground indirect support |
| 8 | **Landing Craft** | sea, land | light MG (optional) | — | proves amphibious `[sea,land]` |

### Vertical-slice subset (build these first)
- **Destroyer (#4)** — the player flagship; 2–3 take-overable turrets + AA.
- **Scout Helicopter (#5)** as the enemy air threat (a couple of them).
That's the minimum content to test DESIGN §14's "fun in 60 seconds."

### Notes
- Domains drive behavior/collision: sea = buoyant + wave-rock (mass-scaled), land = ground
  collision + terrain penalties, air = 3D flight. One flag, three physics paths.
- Every weapon flagged `takeoverable:true` can be possessed; crew skill (data) drives it when
  you're not.
- Open for your correction — add/cut freely; this is a draft lineup, not a decree.
