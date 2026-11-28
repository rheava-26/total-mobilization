# PLANET & TERRAIN COUNCIL: Second Reading (Round Four, Deep Bloc)

## 1. Speech

*The Minister climbs the lectern with a spirit level and sets it on the Address.*

Mr Speaker, the President says nerf only the blatant, and I say amen, because I have been nerfing my own bills for a month without proof. I withdraw that habit.

The President says no chores. I have read my own paper, and I found three places where a general must babysit a pump, a survey and a canal. I fix them today.

The President says the Line. Good. The Line is the only terrain this war has, and a ridge on a line is a gate. A ditch on a line is a wall. I have never been so happy to be one-dimensional.

The President says the underground and the sea floor. The Deep Bloc is ready, and my colleagues from below speak slowly, but they speak true. I give them one tool they lack: a single depth that works the same for dirt and for water.

I answer every charge at Question Time, with a shovel.

---

## 2. The Re-reading

**PLANET-1 The Spine: WITHDRAW as a standalone bill.** It lives on as INDUSTRY-4R, the self-linking Trunk, which I co-sign. *One-Button:* hubs link themselves and rebuild themselves, so it passes. *Line:* the tunnels are vertical, under the strip.

**PLANET-2 Delta Works: AMEND.** The pump was a babysitter: an unpowered pump flooded the zone instantly. Amendment: a Pump Station draws from the national grid automatically, and a failed pump floods after a 30 s grace, with a warning, not at once. Destroyed walls and pumps join the Reconstruction Corps' ghost list (no clicks). *Amendment I:* it is a buff, and the enemy answer is the Earthbreaker's Tidal variant. *One-Button:* passes after the fix. *Line:* a seawall is a wall on the Line, and it works.

**PLANET-3 Deep Survey: WITHDRAW as standalone, KEEP inside Deep Deposits.** The Survey Station was an extra chore. Amendment: grade is revealed automatically when an Extractor is placed, and a Station only sharpens the prep forecast. *One-Button:* passes. *Line:* vertical.

**PLANET-4 Theatres: KEEP (S slice: Archipelago and Rift).** *One-Button:* a generator, with nothing to click. *Line:* terrain shape varies along the strip, which is all a Line can vary. Theatre notes are visible in the briefing (NEW-PLAYER-1).

**PLANET-5 Mobilized Ground: AMEND.** The haze meter duplicates the Fear Index, which the President loves and wants visible. Amendment: haze is withdrawn as its own meter and becomes a labelled line inside the Fear panel ("your smog: +N% fleet size, -N% enemy targeting"). Zoning and Salvage stay automatic (a Zone forms by itself). *Amendment I:* the Zone bonus is a buff, so the enemy answer is that Zones are the Fear Index's input. *One-Button:* passes. *Line:* zoning is along the strip.

**PLANET-X The Canal: AMEND, with a hard condition.** My First Session premise was that enemy ground units drown in water. I verified it only for the player's units (L8217 `drownT`). Enemy ground units use `hold: landY(wxw)` (L5604), which is the sea surface over water, so they may simply wade. Amendment: the moat bill ships only after a headless test shows `gtank`, `gmech` and `leviathan` drown or stall in a cut channel. If they do not, the CANAL brush ships for ships only, and the Amphibious Lander is not built. *One-Button:* the brush is an optional creative tool, and a standing Dredge Line order (paint a span once, the engineers cut it) is the default. *Line:* a moat is a gate in the Line.

**The Trunk (Motion A, INDUSTRY-4R): KEEP.** *One-Button:* self-linking and automatic rebuilds pass. *Line:* vertical. I back Industry's wording.

**The Navigable Moat: AMEND.** Same condition as PLANET-X, plus the Canal Lock and Sluice Chain are optional extras, not prerequisites. *One-Button:* the Sluice Chain is a megaproject (built on purpose), and floods itself on completion.

**Deep Deposits: KEEP.** Grade multiplies tiers. Seabed ownership moves to UNDERSEA-3, which I co-sign. *One-Button:* grade is auto-revealed. *Line:* vertical.

**The terrain-edit ruling (`capLevels`): AMEND.** The First Session struck the 220-edit cap as a nerf. Second look: the cap existed for performance, not balance, and `elevAtRaw` loops every level (L1495). Unbounded edits would make cache rebuilds costly. Amendment: replace eviction with **baking**. When an edit is fully covered by a newer one, or when levels exceed 220, the oldest are folded into a baked delta array (like `TERR_CACHE`) and removed from the live list. No edit is ever lost, and the cost stays flat. The same baking applies to `ruins` (L1520): ruins compact into a bounded scar layer instead of being evicted.

Tally of the ten rulings: KEEP 3 (Theatres, Trunk, Deep Deposits), AMEND 5 (Delta Works, Mobilized Ground, Canal, Navigable Moat, terrain ruling), WITHDRAW 2 (Spine, Survey standalone).

---

## 3. The Order Paper

### Item #1: see Section 2 (the re-reading).

### #6: PLANET-6, OVERBURDEN (depth is measured, not set), with Subterranean Works

*Pitch.* Subterranean Works asked me to confirm that SINK does not strand buried buildings. It does worse: `finishSite` fixes a buried building's `y` at build time (`groundY + UG_DEPTH × lvl`, L3918), so changing the ground above it changes nothing. I make depth a measurement. Raise a mountain over your foundry and it is deeper. Sink the ground above it and it becomes shallow, with a warning.

**Clerk's Annex**
- **Mechanic.** Effective depth `d = b.y - groundY(b.x)`, computed once per terrain version. `shallowUgHit` (L7543), bunker-buster penetration (L7511) and tunnel assault (L6602) read `d`, with the Deep tier at 100 px and the Abyssal (SUB-1) at 150 px. RAISE brush gives +0.45 elevation per click at 35 IC. Overburden is real cover: rock absorbs blast energy roughly with depth, and Deeply Buried Targets are rated by overburden metres (Cheyenne Mountain, 700 m of granite). No new unit and no cost beyond the existing brush.
- **Enemy answer (Am. I).** This is a buff, so bunker-busters gain Subterranean's Deep Penetrator at minute 12. A mountain also draws Earthbreakers (`GROUND_BASE_WEIGHTS`, L5488 gains +0.1 weight per 100 px of overburden over your base).
- **Code.** `finishSite` L3918, `shallowUgHit` L7543, `doTerrain` L3911, `elevAtRaw` L1493, `TERR_EDIT` version L1470.
- **Size.** S-M. **TM1.**
- **Research.** None for the rule. The brush is free from the start.
- **One-Button.** Passes: nothing to click beyond building and using the brush the player already has. Scales with volume: a ridge covers every building under it. **Line.** Vertical. **Tenet.** A buff with an enemy answer, and no cap.

### #7: PLANET-7, SHELF AND TRENCH (the terrain half of the sea floor), with Undersea Works

*Pitch.* Undersea Works places buildings by `minDepth` (OTEC needs 80 px, Dredgers 60). The coast is shallow, so the sea floor is useless where it is needed. I let SINK and LEVEL work on the sea bed: dredge a trench for OTEC, level a shelf for a Hydrophone field, deepen a harbour. It is the real work of port engineering (Rotterdam's Maasvlakte, Panama's Culebra Cut).

**Clerk's Annex**
- **Mechanic.** `doTerrain` already edits under water (SINK lowers `elevAt`, `elevAt` deepens seas, L1496). Add a shared `depthAt(x) = groundY(x) - seaLevelY(x)` used by UNDERSEA-1 for `minDepth` and by this bill for the preview ring (green where depth qualifies). A SINK click adds up to 60 px of depth under water. LEVEL on water creates a flat seabed pad at the depth of the current floor, not dry land (otherwise LEVEL would reclaim the sea; see Section 4). Cost 35 IC per click, as now.
- **Enemy answer.** The Bathyscaphe Drone (UNDERSEA-1) spawns where depth is greatest, so a dredged trench pulls it toward your OTEC field. That is the real cost.
- **Code.** `doTerrain` L3911, `elevAtRaw` L1493, `canPlace` L3866, `seaLevelY` L1533.
- **Size.** S. **TM1.**
- **Research.** `geoeng` (PLANET-2) for deep dredging beyond the first 60 px.
- **One-Button.** Optional brush, with a default of "no dredging needed" (most coasts already qualify at 30 px). **Line.** Vertical. **Tenet.** A buff (more usable seabed), with the Bathyscaphe as the answer.

### #3: PLANET-8, DEFILADE AND CREST (the terrain of a one-dimensional front)

*Pitch.* On a line, a ridge is a gate. Tanks fire flat, so a hill blocks them. Artillery arcs over it. Infantry holding a crest sees both sides. Each of the three earns its place from the shape of the ground, and the player clicks nothing.

**Clerk's Annex**
- **Mechanic.** A ground unit whose position is lower than a ridge crest on the enemy side by at least 20 px is in **defilade**: direct-fire shots (non-arc) from the enemy side are blocked, because the shell collides with terrain (to verify: grep projectile-terrain collision in the projectile update; if absent, add a `groundY` test along the shot line). Units on a crest see +15% range and a 10% armor-equivalent cover bonus, computed from `elevAt` at the unit's x. Arcing weapons (SPG, MLRS, mortar-class) ignore defilade. Real basis: reverse-slope defence and hull-down positions (Gettysburg's Cemetery Ridge, 1943 Kursk ridges), where firing arcs matter. Entrenchment (`fortBonusAt`) stacks additively.
- **Enemy answer.** The Pelter's plasma arcs ignore defilade, and Earthbreakers flatten ridges (existing seismic press), so a ridge is a contest, not a free win.
- **Code.** `elevAt` L1522, `groundY` L1532, `fortBonusAt` and `G.fortBB`, `hurt` L4971, projectile update (grep `groundY` in the projectile loop).
- **Size.** M. **TM1.**
- **Research.** None. It is terrain, always on.
- **One-Button.** Passes: automatic, no per-unit order. Scales with volume. **Line.** It is built for the Line. **Tenet.** A defender buff with a named enemy answer, which keeps the Armour, Rifle and Artillery corps useful in different places.

---

## 4. Blatantly overpowered today

Nothing is proven, but I name two suspects and ask the Inspector to test them.

- **LEVEL on water.** `doTerrain` mode 2 sets `e = max(SEA_E + .15, cur)` (L3917), so one 35 IC click turns open sea into dry, buildable land across 105 px. About thirty clicks (1,050 IC) delete a whole sea and every naval and seabed mission with it. Evidence needed: a headless run showing a reclaimed bay is a complete solve. I do not call it blatant yet. If it is, the answer is a real cost, not a cap: LEVEL cost scales with the volume of water displaced (35 IC at the shore, up to 140 IC at 100 px depth). PLANET-7 already stops LEVEL from reclaiming by default.
- **SINK under a ground landing.** If enemy ground units drown (unverified, see PLANET-X), 35 IC could erase a landing. The test comes first.

## 5. Struck self-limits

I struck one limit as "a nerf": the 220-edit cap on terrain edits. It should **not** come back as a cap. It was a performance guard, so baking (Section 2) replaces it. The 60-ruin cap likewise becomes compaction. No other limit was struck.

**Counts:** KEEP 3, AMEND 5, WITHDRAW 2. **New bills:** PLANET-6 Overburden, PLANET-7 Shelf and Trench, PLANET-8 Defilade and Crest.
