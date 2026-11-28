# GROUND FORCES COUNCIL — Round 2 Paper (`ground`)

## 1. Floor Speech

Mr Speaker. I have sat through twenty-two papers and counted the words "sky", "orbit" and "satellite" more often than the word "ground", which is the thing the enemy is *landing on*.

I praise the Artillery Directorate (kinetic-arms), whose KINETIC-4 is my GROUND-3 in a better hat. I praise Industry for INDUSTRY-1: a gun is a customer, and at last somebody has sent it a bill and a train. I praise Bio, whose axolotls will refill my line at the price of one manpower each. And I praise the Autofleet Council for AUTOFLEET-HOT, because nothing flatters a field marshal like an enemy forced to copy his rifleman.

I will not praise Naval for proposing to drown my front. Admirals will flood anything that isn't floating. (My tanks swim from the Fusion Age, so the joke is on them.)

I expect two attacks. First: "Battalions are a perf project." Correct, and perf is solved. Second: "Infantry are chaff and the Treasury should pay for the dead" (STABILITY-1). Gentlemen, I will pay the Ledger gladly, once the Army has a draft depot to answer it. Mass is the fantasy. Give mass a *name*, a *line* and a *replacement pipeline*, and I will bury the Air Council's ten-to-one under mud.

## 2. Positions

| Bill | Position | One line |
|---|---|---|
| KINETIC-4 Fire for Effect | **SUPPORT** (merge, see Motion 1) | Same TOT/MRSI idea as GROUND-3; their building and blast radius are good, my counter-battery radar completes it. |
| BIO-2 Blastema Protocol | **AMEND** | Regrowth must hand off to battalions: bodies regrow into a battalion pool (GROUND-1) and IC, not MP, pays when the army cap is full. Otherwise it stays a bio-only perk. |
| BIO-X Immortal Line | **SUPPORT** | A Titan that grows is the right ground capital; it needs a Line marker (GROUND-2) to be stationed. |
| COMMAND-1 Five-Year Plan / INDUSTRY-3 Autofoundry | **AMEND** | Standing orders must include "raise battalion of X to strength N and station on Line Y". Ground reinforcement is the first consumer. |
| COMMAND-3 Shock Brigades | **SUPPORT** | Strain is a real cost and storming a Barracks before a landing is exactly the gambit a general wants. |
| INDUSTRY-1 Ammunition Trains | **SUPPORT** | Artillery logistics is the oldest truth in war; "supplied are blessed, unsupplied unchanged" is tenet-clean. |
| INDUSTRY-4 / COMMAND-2 / PLANET-1 rail under the continent | **AMEND** | Add a **Sally Port** node: rail stations let battalions debark behind a Line on the surface (extends the existing `rail:true` Rail Hub). |
| PLANET-5 Mobilized Ground (Zones) | **SUPPORT** | Paved belts want garrisons; Zones plus Lines (GROUND-2) make a belt a fortress. |
| NAVAL-X Inland Sea | **AMEND** | Flooding must spare `amph` units and let Sappers (GROUND-4) pre-build Sluice Gates cheaply; otherwise the player drowns their own army. |
| PLANET-2 Delta Works | **SUPPORT** | Seawalls and polders are engineering; fits the Sapper Corps. |
| AUTOFLEET-1 Uncounted Columns | **AMEND** | Add a *ground* axis: a column that tests the Line the player hasn't drawn. |
| AUTOFLEET-HOT The Mirror | **SUPPORT** | Echo copies of ground units make the player's battalion mass face its own face. |
| STABILITY-1 Casualty Ledger | **AMEND** | Count *squads or battalions* lost, not bodies, and a standing Battalion with a draft depot in range counts casualties as "relieved". Otherwise it taxes mass specifically. |
| SPACE-2 Cooperative Engagement Net | **SUPPORT** | Same ToT concept for orbit; share the delay solver with KINETIC-4. |
| PACING-1 Second Siege | **SUPPORT** | The withdrawal window is when the Reserve counter-attacks (GROUND-2). |
| MEGA-1 Ledger of Projects | **SUPPORT** | The Crawler Capital (GROUND-X) wants the section framework. |

## 3. Ballot (10 votes; max 3 per bill; none to ground)

| Bill | Votes |
|---|---|
| KINETIC-4 Fire for Effect | 3 |
| INDUSTRY-1 Ammunition Trains | 2 |
| AUTOFLEET-HOT The Mirror | 2 |
| BIO-2 Blastema Protocol | 1 |
| COMMAND-3 Shock Brigades | 1 |
| PLANET-5 Mobilized Ground | 1 |
| **Total** | **10** |

## 4. Motions to Co-sponsor

### Motion 1 — "FIRE DIRECTION NET" (to: kinetic-arms, space-fleet)
Merges GROUND-3, KINETIC-4 and SPACE-2 into one time-on-target engine with two front-ends.

**Clerk's Annex**
- **Mechanic.** One delay solver, `solveToT(guns, point)`: each gun's flight time `t_i` is computed from its arc and the barrage fires gun `i` at `T - t_i` so impacts land within 0.25 s. Front-end A (kinetic-arms): *Fire Direction Centre* building (180 IC, 60 MP, hp 200), links guns within 900 px, cooldown 8 s, shell blast r 38 / 60% dmg. Front-end B (space-fleet): the *Relay Sat* net uses the same solver for orbital shot travel. Front-end C (ground): **Counter-battery radar** (240 IC, range 1,400 px) back-traces enemy `shell`/`rocket` arcs (`PS` L4892) and fires linked guns at the origin ±40 px, 2 s reaction. Salvo size unbounded; ammo from INDUSTRY-1 depots when present, base stats when not.
- **Code hooks.** `UNIT_DEFS` arty/mlrs/hydra L3189-3194, `acquireEnemyMulti` L4398, `PS` L4892, projectile fire in `fire()` L4865, strike-aim UI near `STRAT` L6878, `PD_SAT` L8620 for the orbital front-end, dead `G.designated` Fire Control plumbing.
- **Size.** L (about 600 lines, the solver is shared).
- **Tag.** TM1.
- **Tenet.** Pure power; the only limit is guns built and shells supplied.

### Motion 2 — "THE LIVING BATTALION" (to: bio, command)
Merges GROUND-1, BIO-2 and COMMAND-1's standing orders.

**Clerk's Annex**
- **Mechanic.** Squads of the same type muster into a battalion with pooled HP and body count (GROUND-1). Bio-family battalions regrow one body per 6 s ÷ (1 + 0.15 × Gene Facilities) at 1 MP when the pool has MP, else at IC cost via the draft depot (no new MP, Art. IV.1 respected). Non-bio battalions regrow only within 400 px of a Barracks/Veterans' Hall at IC cost, slower (1 squad per 6 s). COMMAND-1 gains the standing order `RAISE bn <type> N AT <line>`. A battalion below 40% strength is Shaken (−25% rate) until relieved.
- **Code hooks.** `spawnSquad` L4091, `SQUAD_LOADOUT` L3953, `healSquad` L5029, `hitSquadMember` L5000, `UNIT_FAM` L3375, `armyCap` L3803, heal tick L7866, `updUnits` L7809.
- **Size.** L (about 700 lines).
- **Tag.** TM1.
- **Tenet.** Adds regrowth and structure; costs are IC and cap weight, no hard caps. Uncapped number of battalions.

### Motion 3 — "PREPARED GROUND" (to: planet, naval)
Merges GROUND-4 (Engineer Corps), PLANET-2 (Delta Works) and NAVAL-X (Inland Sea).

**Clerk's Annex**
- **Mechanic.** Combat Engineers ("Sapper", 90 IC / 8 MP, hp 80) are the single builder for terrain-works: they lay mines (1 cell per 6 px, 140 dmg r 24, triple vs tracked/legged, zero vs air), dig tank ditches (existing SINK mode, enemy `gtank` stall 6 s), raise seawalls and polders (PLANET-2), and build Sluice Gate chains (NAVAL-X) at 60% of the listed IC when a Sapper battalion is present. Flood waves spare `amph` units (L8202). Enemy answers stay the fleet's job: `breacher` mine-rollers and `earthbreaker` ditch-crackers.
- **Code hooks.** `TERRAIN_MODES`, `fills[]`/`craterAt` L1492, `passable` L1536, flood rule L6561, `deployEmplacement` L4564, `hurt` L4971, `spawnEnemy` L5597-5620.
- **Size.** L (about 500 lines).
- **Tag.** TM1.
- **Tenet.** Adds toys and an IC sink; the player's own flooding is the only cost.

## 5. Trades

- I **shrink GROUND-3** to its counter-battery radar only. The ToT barrage becomes KINETIC-4's, via Motion 1.
- I **shrink GROUND-4** to mines and ditches only if Planet and Naval take the seawall and sluice work under Motion 3.
- I will **withdraw the Veteran/Guards honours in GROUND-1** if Stability takes them as a Veterans' Bureau perk (STABILITY-1).
- I keep GROUND-2 (Elastic Line) and GROUND-X (Crawler Capital) whole; nobody else is bidding for either.

## 6. Constitutional Challenges

- **STABILITY-1 (Casualty Ledger), Art. III.** The log penalty on organic dead falls hardest on chaff infantry, and it reduces the stability *ceiling* automatically. It stays constitutional only if the amendment above is accepted (count units lost, not bodies; relieved battalions excluded), since otherwise it nerfs a mass-infantry fantasy rather than pricing it.
- **PACING-HOT (the more you build, the sooner the fleet arrives), Art. III.** It denies the "cover the world in factories" fantasy by punishing it with the clock. Its sponsor flags it as a knife-edge; my ruling is that it is unconstitutional unless the earlier arrival comes with a scarier *enemy for that arrival* and not a shorter prep. As written, it is a tax on growth, which is the fantasy.
- **`dangCap` (L4708, lore) and `capLevels` (planet), Art. III.** Silent caps on dangerous research and terrain edits are exactly what the tenet forbids; replace them with upkeep or a scarier fleet response.
