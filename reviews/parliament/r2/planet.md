# PLANET & TERRAIN COUNCIL — Round 2 (`planet`)

## 1. Floor Speech

*The Minister of the Interior and Geography unrolls a very large map and pins it over the Speaker's gavel.*

Honourable members. In Round 1 I said the ground deserved a war. I did not expect half the House to turn up with shovels.

I thank the Admiralty. Two papers, NAVAL-3 and NAVAL-X, independently arrived at my canal, and a man who finds the same river as me is a friend. I thank Industry and Command for finding my railway. Three councils drew the same tunnel, which means it is a good tunnel and a crowded one. I thank Science for finding my survey, and for stealing my title.

Now the rivals. The Air Council wants runway wars but has never asked what the runway is built on. The Space Council wants Kessler clouds, which is a polite way to say a ceiling made of garbage. The Pacing Council wants the clock to run faster when I build, and I tell them this: you do not punish a nation for pouring concrete. That is the whole game.

My own bills are defended in one sentence each. The Spine is a railway, and a railway has no cap. Delta Works is a levee, and a levee has a pump bill. The Survey is a map. Theatres are variety. Mobilized Ground is a landscape. The Canal is a moat.

I submit to the House that one of my own papers raised a tenet problem, the 220-edit cap on terrain. I ask the House to rule against my own code, and I ask for the cap to go.

---

## 2. Positions

| Bill | Position | Reason |
|---|---|---|
| NAVAL-3 Great Canals | AMEND | Merge with PLANET-X (Motion 1). Canal width, SINK cost and the `capLevels` exemption are one decision, and I own it. Lock building and fleet-in-being stay with Naval. |
| NAVAL-X Inland Sea | AMEND | Support the megaproject. Amendment: flood uses `pushLevel` negative levels (L1504), not `fills[]`, since `fills` is legacy. Evacuation warning is mandatory. |
| NAVAL-4 Trench Landers | SUPPORT | The scarier enemy my moats demand. Without it PLANET-X is a permanent win. |
| INDUSTRY-4 Freight Rail | AMEND | Merge with PLANET-1 and COMMAND-2 (Motion 2). Amendment: reconstruction ghosts must also queue for terrain edits (levelled plateaus, polders). |
| COMMAND-2 Metro-2 | SUPPORT | The Molodets train gives dispersed launchers back. Fold the trunk bonus into the shared rail graph. |
| RAILGUN-X Meridian Accelerator | AMEND | Use the same rail graph. The barrel is the tube segments only, so the freight net and the gun are one structure with one set of weak points. |
| INDUSTRY-2 Deposit Cascades | AMEND | Merge with PLANET-3 and SCIENCE-5 (Motion 3). Tiers and grade multiply, they do not replace one another. |
| SCIENCE-5 Geoscience branch | AMEND | `geosurvey` and `egs` are good. Amendment: `deltaworks` crater-heal applies only inside Pump Station or Seawall zones, never map-wide. Scars elsewhere stay, because the destruction fantasy is half my portfolio. |
| GROUND-4 Engineer Corps | SUPPORT | Ditches reuse SINK. Amendment: sappers share the terrain brush (no per-edit cap). |
| GROUND-X Crawler Capital | OPPOSE | A walking city fights the map instead of using it. It needs the cities to not be places. |
| BIO-1 Hyphal Net | AMEND | Roots as ground cover only where terrain is LEVEL or RAISED. Gives Zoning in PLANET-5 something to join. |
| BIO-3 Radiotroph Gardens | SUPPORT | Fallout becomes farmland is exactly the scarred-earth idea. Lets craters mean something. |
| AIR-5 Runway Wars | AMEND | Cratered runways must repair via the same ruins and levels system, with no cap. |
| MEGA-H, MEGA-2 | SUPPORT | Continental gun sections are terrain claims. Each section is a LEVEL strip. |
| AUTOFLEET-2 Fear Index | AMEND | Only measure what is built and visible. Add a haze input (PLANET-5), so smog is a real tradeoff. |
| NEW-PLAYER-1 Order of Battle | SUPPORT | Show the theatre name and the three landing flanks in the briefing. |
| STABILITY-2 Maskirovka | SUPPORT | Hardened, buried hubs are the physical half of hiding the nation. |

---

## 3. Ballot (10 votes)

| # | Bill | Votes |
|---|---|---|
| 1 | INDUSTRY-4 Freight Rail and Reconstruction Corps | 2 |
| 2 | INDUSTRY-2 Deposit Cascades | 2 |
| 3 | NAVAL-3 Great Canals | 1 |
| 4 | NAVAL-X Inland Sea | 1 |
| 5 | NAVAL-4 Trench Landers | 1 |
| 6 | SCIENCE-5 Geoscience branch | 1 |
| 7 | GROUND-4 Engineer Corps | 1 |
| 8 | RAILGUN-X Meridian Accelerator | 1 |
| | **Total** | **10** |

I voted for the economy of the ground, because report 7's 212k idle IC is a map with nothing worth spending on. I voted for the Admiralty because the navy is barely fielded and canals give it a theatre.

---

## 4. Motions to Co-Sponsor

### MOTION P-1 — THE GREAT CANALS AND THE INLAND SEA (to: naval)

**Merged mechanic.** One water system: a CANAL terrain mode (PLANET-X) cuts channels, Canal Locks (NAVAL-3) let ships pass narrow land gaps, and Sluice Chains (NAVAL-X) flood a valley as a megaproject. All three share one `terrainVersion` bump, and the fleet answers with Trench Landers (NAVAL-4).

**Clerk's Annex**
- **Numbers.** CANAL brush: 40 px wide, 90 IC per 105 px, no per-edit cap. Lock: 150 IC / 40 MP, spans up to 240 px. Sluice Gate: 400 IC each, chain of 3 to 6 for a 600 to 1,200 px valley, flood runs 60 to 90 s.
- **Code hooks.** `doTerrain` L3911, `pushLevel` L1504, `capLevels` L1505 (exempt canal and flood edits), `waterBounds` L3760, non-amphibious drown L8200/L8217, `passable` L1536, `doSkyfall` L5206, `spawnEnemy` L5581.
- **Build size.** L.
- **TM1.**
- **Tenet check.** Passes. All new player power, no cap. The cost is that your own ground units cannot cross either (a real logistics cost). The enemy answer is scarier units, as the tenet asks.

### MOTION P-2 — THE CONTINENTAL RAIL GRAPH (to: industry, command)

**Merged mechanic.** One Rail Hub graph: hubs within 1,000 to 1,400 px link into a net. The net carries (a) shared component stock (INDUSTRY-4), (b) trunk bonus +40% to buildings within 120 px and the Molodets Train (COMMAND-2), (c) freight upkeep at 1.5 IC/s per 1,000 px (PLANET-1), (d) buried-tube immunity to surface barrage, and (e) the same tube segments as the Meridian barrel later (RAILGUN-X). One graph pass, one overlay, one set of weak points.

**Clerk's Annex**
- **Numbers.** Link range 1,200 px. Net adds +10% per hub pair (uncapped). Reconstruction ghost rebuild at 70% IC after 20 s. Node `railnet`, branch `auto`, tier 1, pre `automation`, cost ~100.
- **Code hooks.** `railhub` L3416, `railHubs` L7812, rail draw L9313, `recalcBuildings` L3762, `digLevel` L3863, `makes` loop L6306.
- **Build size.** L.
- **TM1.**
- **Tenet check.** Passes. Pure upside, with upkeep and a vulnerable network as the cost.

### MOTION P-3 — DEPOSIT GRADE, DEPTH AND CASCADE (to: science, industry)

**Merged mechanic.** Each node has a hidden `grade` 0.6 to 2.5 (SCIENCE-5 `geosurvey`), revealed by survey. Extractor tiers (INDUSTRY-2) multiply that grade. A deep dig layer on the Extractor (PLANET-3) adds ×2 yield and ×2 resource bonus. EGS Geothermal (SCIENCE-5) adds buried power plants.

**Clerk's Annex**
- **Numbers.** Yield = base × grade × (1 + 0.4 × tier) × (2 if dig level 2). One jackpot node per game with grade 2.5. Seismic Survey Station: 90 IC, reveals grades during prep.
- **Code hooks.** `SR` L3173, node spawn L3633 to L3636, `nodeNear` L3876, `digLevel` L3863, node draw L9290, minimap L9776.
- **Build size.** M.
- **TM1.**
- **Tenet check.** Passes. Strategic resources become bonuses, not gates, as the designer asked. A jackpot node is a landing target (the enemy's reason to go there).

---

## 5. Trades

- **PLANET-1 (Spine) folds into Motion P-2.** I withdraw it as a standalone bill and keep only the buried-immunity and upkeep rules inside the coalition bill.
- **PLANET-3 (Deep Survey) folds into Motion P-3.** Withdrawn as standalone.
- **PLANET-X shrinks.** The canal brush goes into Motion P-1. I give up the Amphibious Lander to Naval's NAVAL-4 (Trench Landers).
- **PLANET-4 Theatres shrinks to an S slice** (Archipelago and Rift only), so more room is left for the coalition bills.
- I keep PLANET-2 (Delta Works) and PLANET-5 (Mobilized Ground), since nobody else touches them.

---

## 6. Constitutional Challenges

1. **Article III against the existing code, and against myself.** `capLevels` (L1505) silently evicts player terrain edits after 220. That is a hard cap on a player toy. I ask the House to rule it void and strike it. Perf is already solved by the terrain cache, and a rebuild range per edit is bounded.
2. **Article III against PACING-HOT.** A clock that arrives sooner the more you build punishes the core fantasy (cover the ground in factories). I challenge it unless it is recast as a scarier fleet (the fleet gets stronger), not a shorter prep.
3. **Article III watch on `ruins` cap of 60** (`pushRuin`, L1515). Evicting wrecks is a destruction-fantasy cap. Not a challenge to any bill, but a ruling is requested.
