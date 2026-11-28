# NAVAL COUNCIL: Round 3 Coalition Paper

## 1. Answers

**A. "The Counter-Air Doctrine" (Air; addressed Air, Naval) — AMEND.**
Co-sign with this amendment: the Flak Minelayer is a *player-side* naval escort hull (Missile Cruiser line, `req: 'navalpwr'`, no new research), dropping 4 mines (120 dmg, radius 90) as a barrage laid in the sky-ship's flight path, using the existing `d.capital` CIWS hooks (L7905). It is not an enemy kind and not a counter to our own sky capitals. Mines must be a *toy we place*, not a rule that changes enemy flight.

**B. "Prepared Ground" (Ground; addressed Planet, Naval) — AMEND.**
Co-sign with three amendments:
1. The Sluice Chain keeps its 10 s evacuation warning (toast, siren) before the flood wave begins.
2. The Sapper's 60% discount applies to *Sluice Gates and Canal Locks*, not to the CANAL brush, which Planet's P-1 owns.
3. The flood/canal edits are exempt from `capLevels` (L1505), as Planet's point of order demands.

**C. "The Dispersal" (Missiles; addressed Naval) — ACCEPT, with one clarification.**
The Admiralty owns hulls, Missiles own magazines. I accept the split of 400 lines naval, 200 missiles. Clarification: Ekranoplan (NAVAL-5) is *not* a magazine launcher; it stays an ordinary fast naval unit with its own 6-round salvo, and I accept it as the first cut if the House wants a smaller bill. The 3 s reveal-on-fire rule is as in NAVAL-1.

**D. "The Great Canals and the Inland Sea" (Planet; addressed Naval) — ACCEPT.**
As written. One request for the Clerk: when a canal joins two waters, `waterBounds` must merge them under a single `TERR_EDIT` bump so ship caches (`u._wbV`, L8125) rebuild once, not per edit.

## 2. Joins

- **Orion: The Ship and the Project (Space-fleet; to Megaprojects).** The Admiralty co-signs. It is the first space ship that is a ship, and its hull taxonomy should reuse the naval `capital` block.
- **Time on Target (Space-fleet, Kinetic-arms).** Co-sign: a Tomahawk boat's cruise ripple is a natural leg, and submarine launches arriving in one 0.3 s window is the whole point of a hidden hull.
- **The Continental Rail Graph (Planet; to Industry, Command).** Co-sign: one rail node must be placeable at a harbour, so shipyards join the supply graph.

## 3. Competing motions

- **Water and terrain.** Back **P-1 (Planet)** as the single water system. "Prepared Ground" rides *under* P-1 as the Sapper discount and the mines. One CANAL brush, one lock, one Sluice Chain.
- **Launchers.** Back the **Missiles "Dispersal"** over my own Motion A ("Silent Boomer"). They are the same bill; Missiles' version has the clean magazine rule. I withdraw Motion A in its favour, keeping the depth band and sonar rules as the naval half.
- **Rail.** Back **P-2 (Planet)**: it already folds INDUSTRY-4, COMMAND-2, PLANET-1 and RAILGUN-X into one graph. Harbour terminal only requested.
- **Forecast.** Back **"The One Forecast" (Market, New-player)**. Hydrophone arrays (NAVAL-1 sonar) should count as observations in its `sigma/sqrt(1+n_obs)`, so the navy contributes to intel without a rival system.
- **Blue Economy.** My Motion C (Deep Deposit) stands; I accept being folded into Industry's tier ladder.

## 4. Closing statement

Mr Speaker, the Admiralty arrived with three ships and a grievance. It leaves with a depth band, a canal, a sluice and a place in every supply graph. We asked the House for water and it gave us a theatre. The Autofleet fears an industrialising Earth; let it also fear what sails out of the harbour. The Admiralty yields the floor, and we will meet you on the water.
