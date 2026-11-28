# NAVAL COUNCIL: Round 2 Floor Paper

## 1. Floor speech

Mr Speaker, the Admiralty thanks the House. For the first time in this war, other ministries have discovered water.

To the Rocket Forces (MISSILES-2): you have promised a Boomer, an Arsenal Ship and a Ballistic Submarine, and you put "naval submerge" in a line of your annex as if depth were a footnote. Depth is a dimension, gentlemen. We will lend you ours, with the sonar, the thermocline and the detection rules attached. To the Planet Council (PLANET-2, PLANET-X): you dig canals and the Admiralty weeps with joy. You also drown the enemy's tanks. Bless you. But a moat with no ship in it is a puddle, and your Great Canal needs a fleet to patrol it.

To the Air Council: 138 sky capitals. A fine zeppelin museum. The Admiralty notes that none of them can hide under a thermocline.

I defend my own bills in advance. They say the Blue Economy is "free IC". It is free the way a harbour is free: an enemy Trench Lander will come for it, which is exactly the real cost the Tenet asks for. They say submarines are "invisible, therefore a cap on the enemy's fun". No. The enemy gets sonar hunters. And they say Canal Locks duplicate PLANET-X. Then let us merge them. The Admiralty does not own the shovel. We merely insist someone sail the result.

## 2. Positions

| Bill | Position | Reason / amendment |
|---|---|---|
| MISSILES-2 Launcher Dispersion (TEL, Arsenal Ship, Boomer) | **AMEND** | Merge the Boomer with NAVAL-1: Boomer uses `u.depth`/`u.hidden`, is revealed 3 s after launch or inside a sonar aura, and the hidden-target skip in `acquireEnemy` (L4374) is built once for both bills. Arsenal Ship gets 16 cells, as written. |
| MISSILES-1 Cruise Missile Wing | **SUPPORT** | Sea-skimming is real and the wing launches naturally from a sub. Add: launchable from a `strat:true` naval unit. |
| PLANET-X The Great Canal | **AMEND** | Merge with NAVAL-3 and NAVAL-X: a canal that joins to the ocean via `waterBounds` becomes navigable, so ships may sail it; a Canal Lock building allows a land gap of 240 px. Moats then have a patrol. |
| PLANET-2 Delta Works | **SUPPORT** | Seawalls and pumps give harbours a defence, and the `capLevels` exemption is a tenet fix. |
| PLANET-3 Deep Survey (ore veins) | **SUPPORT** | Pairs with seabed nodules. Add seabed grade as a survey result. |
| PLANET-4 Theatres (archipelago) | **SUPPORT** | An archipelago map gives ships a job. Without NAVAL-3 canals, however, an archipelago strands a fleet; require merged `waterBounds`. |
| INDUSTRY-2 Deposit Cascades | **AMEND** | Add ocean deposits: Adsorber and Dredger (NAVAL-2) get the same 5 development tiers, so the sea is one more deposit family, not a rival system. |
| INDUSTRY-H Salvage the Fleet | **SUPPORT** | Wrecks of Trench Hunters sink and are recoverable by Dredgers. |
| AUTOFLEET-1 Uncounted Columns | **AMEND** | Add a sea column: Trench Landers (NAVAL-4) are the "uncounted" axis for coastlines. |
| AUTOFLEET-3 Weakest Axis | **SUPPORT** | A coast-heavy build exposes the maritime axis. |
| PHYSICS-2 Thermal Loading | **SUPPORT** | Its "thermal shock" easter egg for water hits is a naval gift. Ships should *cool* heated targets and not just shrug. |
| COMMAND-1 / INDUSTRY-3 auto-build | **AMEND** | Standing orders must target a named shipyard, since `needWater` placement limits where hulls come from. |
| AIR-1 Pelican refuelling | **SUPPORT** | Carriers as tanker hosts, as AIR notes. |
| SPACE-5 Orion Battleship | **SUPPORT** | It is the first space ship that is a ship. The Admiralty claims the name "battleship". |
| MARKET-5 Letters of Marque | **SUPPORT** | Privateers are literally naval. Contract ships paid per kill. |
| PLANET-1 / INDUSTRY-4 / COMMAND-2 rail under continent | **AMEND** | Rail terminals at ports: a freight rail node at a harbour feeds shipyards one supply graph. |
| GROUND-4 Engineer Corps (bridges) | **AMEND** | Bridges must not block ship lanes; bridge decks need clearance of 40 px above `seaLevelY` or a lift span. |

## 3. Ballot (10 votes)

| Bill | Votes | Why |
|---|---|---|
| MISSILES-2 Launcher Dispersion | 3 | Fills three promised-but-empty slots and makes the strike system survivable. Best bill for the game. |
| PLANET-X The Great Canal | 3 | Turns terrain, ground enemy and navy into one system, and rewards the existing toy. |
| PLANET-2 Delta Works | 1 | Fixes `capLevels` and defends coasts. |
| INDUSTRY-2 Deposit Cascades | 1 | Gives the idle strategic resources a ladder. |
| AUTOFLEET-3 The Weakest Axis | 1 | Makes enemy reaction legible and fair. |
| GAME-FEEL-5 Legible Mass | 1 | Mass is back on the table. It must read. |

Total: 3+3+1+1+1+1 = 10. None for NAVAL bills.

## 4. Motions to co-sponsor

**Motion A. To the Missiles Council and the Air Council. "THE SILENT BOOMER"**
Merge MISSILES-2's Boomer and Arsenal Ship with NAVAL-1's depth band.

*Annex.* Mechanic: `u.depth` 0/1/2 on naval units; `hidden` units skipped for hostile `acquireEnemy` unless `u.revealT > 0` (set 3 s on firing, on surfacing, or by a `sonar` aura radius 600). Boomer: IC 600, 8 SLBM rounds, `strat:true`, reload at a Shipyard; Pike hunter-killer IC 230 as the escort; Tomahawk cruise-missile boat launches MISSILES-1 ripples. Research: `sonar` (def, tier 2, cost ~130, pre `pointdefense`), Boomer gated by `spaceage` + `uranenrich` as MISSILES-2 proposes. Code hooks: `UNIT_DEFS` L3184, `acquireEnemy` L4374, `armSilo` L7660, `bldSurfaceY` L3671, naval move L8123, render L9348. Size: L. TM1. Tenet check: pure addition; the cost is the enemy's ASW answer (NAVAL-4 Trench Hunter) and reload IC. Interactions: Missiles (strike dispersal), Autofleet (hunters), Air (subs are immune to bombers).

**Motion B. To the Planet Council. "THE NAVIGABLE MOAT"**
Merge PLANET-X's CANAL brush, NAVAL-3's Canal Lock and NAVAL-X's Inland Sea.

*Annex.* Mechanic: CANAL brush as PLANET-X (90 IC per 105 px, no cap). When a canal reaches an ocean, `waterBounds` merges the bodies so ships sail it. *Canal Lock* (IC 150) spans up to 240 px of land. *Sluice Chain* (megaproject, NAVAL-X) floods a valley for 60-90 s with a 10 s evacuation warning. Enemy answer: Amphibious Lander from PLANET-X plus Trench Landers. Code hooks: `doTerrain` L3911, `pushLevel`/`capLevels` L1504-1505 (exempt), `waterBounds` L3760 and cache `TERR_EDIT` L1470, drown rule L8217, flood rule L6561, `doSkyfall` L5206. Research: `geoeng` (PLANET-2) plus `harbourworks`; combos `automation` (dredger fleet) and `nanotech` (self-clearing). Size: L. TM1. Tenet check: uncapped terrain power for the player; enemy gets amphibious reply; the cost is the flood plain you drown. Interactions: Planet, Ground (non-amphibious tracks drown), Enemy.

**Motion C. To the Industry Council and the Planet Council. "THE DEEP DEPOSIT"**
Merge NAVAL-2's Blue Economy, INDUSTRY-2's cascades and PLANET-3's Deep Survey.

*Annex.* Mechanic: Seawater Adsorber (uranium), Nodule Dredger (steel and rare earth), OTEC (IC) each get the 5 development tiers (cost x3 per tier). Deep Survey reveals a seabed grade per water body, scaled by `waterBounds` width. Dredger gives rare earth meeting Antimatter's `srReq` without a land node. Code hooks: `SR` L3173, `BUILD_DEFS` L3381, `srMult` L3810 (fix boolean-vs-count read flagged by Industry), `G.owned`, `resourcesCaptured` L3608. Size: M. TM1. Tenet check: adds income and options, no caps; the cost is vulnerability at sea. Interactions: Industry, Planet, Enemy (Trench Landers), Strategic (cheaper strikes).

## 5. Trades

- **NAVAL-3 (Great Canals)**: I withdraw the standalone canal and merge it into Motion B. PLANET owns the brush.
- **NAVAL-X (Inland Sea)**: I shrink it to the *Sluice Chain* option inside Motion B; the flood wave stays, the 600-line animation goes if the House is tired.
- **NAVAL-5 (Ekranoplan)**: first to be dropped if the sea bills crowd the calendar. It is S-M and expendable.
- **NAVAL-2 (Blue Economy)**: I accept being folded into INDUSTRY-2's tier ladder.

## 6. Constitutional challenges

- **PACING-HOT (the more you build, the sooner the fleet arrives): OPPOSE as an Article III violation.** It punishes the player for building toys. Its sponsor concedes it is a knife-edge. The tenet's lever is a scarier enemy; a shorter clock *per toy built* is denial by another name. It would be legal as AUTOFLEET-2's fear scaling the *enemy's strength*, not the player's time.
- **`capLevels` (L1505) at 220 edits** is a silent cap on a player toy (terrain). Planet's point of order stands; PLANET-2 and PLANET-X should exempt it outright, and the Admiralty so moves.
