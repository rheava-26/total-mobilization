# NAVAL COUNCIL: Second Reading (Deep Bloc)

## 1. Speech

Mr Speaker. The President has asked the House to stop making everything stronger and start making everything *work*. The Admiralty has never been accused of making anything stronger, so we feel unusually safe.

We have also read the Commissioner of Undersea Works, who has been breathing helium for nineteen days. He is right. We have been fighting over the top three metres of an ocean. The Admiralty keeps the waterline and the lanes just beneath it; the Commissioner keeps the floor. It is a clean treaty and I intend to sign it before the Subterranean Director digs under it.

The One-Button Law suits a navy: a ship is a button you press once and a harbour you forget. The Line suits a navy too, because a coast is a line with a second, wetter line beside it.

We shall be honest about our bills. Some of them were babysitting. Those are cut.

## 2. The Re-reading

**NAVAL-1, The Silent Service: AMEND.** *One-Button:* the three depths (0/1/2) were a micromanagement trap. Cut to two states, **surfaced** and **submerged**, chosen automatically: a sub dives when an enemy air unit or bomber is within 900 px, surfaces to fire, and returns to a Pen or Shipyard to reload. The player never sets depth. *Line:* depth is vertical; subs need no flank. *Amendment I (buff):* the enemy answer is the Trench Hunter (NAVAL-4). I also strike "immune to air attack": it is replaced by "enemy air cannot lock a submerged hull; torpedoes and depth bombs from Hunters can". Folds into docket #14 Dispersal.

**NAVAL-2, The Blue Economy: WITHDRAW.** Docket #9 (Deep Deposits) carries the economy, and the Commissioner's UNDERSEA-2 and UNDERSEA-3 place the Adsorber, Dredger and OTEC on the sea *floor*, which is where they belong. Naval keeps no economy building. We keep only the surface tender hull in NAVAL-6.

**NAVAL-3, The Great Canals: WITHDRAW.** Already merged into the Navigable Moat. One brush, one lock, one sluice. No second copy.

**NAVAL-4, Trench Landers: AMEND.** Merge with UNDERSEA-1's Bathyscaphe Drone so the enemy has one sea family, not two. *Trench Lander* (seeder, 900 HP x `t`, anchors at sea level) releases *Trench Hunters* (swim; 70 HP; torpedo). Hunters get a `bed` flag: they may crawl the sea floor to attack seabed buildings (the Bathyscaphe behaviour). Spawn trigger: any of {5+ seabed buildings, 3+ ships, a Pen} but never before invasion minute 4. Enemy-side only; the player does nothing. *Line:* a sea lane is one dimension along the front. *Amendment I:* a scarier enemy, which is the preferred answer.

**NAVAL-5, Ekranoplan Wing: WITHDRAW.** It overlaps MISSILES-1 (sea-skimming cruise missile) and the Air Staff, and I claimed it ignored the sky ceilings, which was nonsense. It is the first cut. If a cheap fast surface skirmisher is wanted later, it returns as an Arsenal Ship upgrade, not a unit.

**NAVAL-X, The Inland Sea: AMEND.** *One-Button:* the 10 s evacuation warning was attention per event. Replace it with a **standing order** set once on the Sluice Chain: FLOOD ON LANDING (default) or HOLD. When a landing touches the span, the chain fires automatically; player ground units in the span get an automatic 10 s retreat (they simply path out), and flood-plain buildings drown as written. *Line:* the valley is a horizontal span on the one line, which is the best geometry a 1-D front offers: everything inside the span is wet. *Amendment I:* enemy answer is the Amphibious Lander and Earthbreaker from the Moat.

**Docket #13, The Navigable Moat (co-sponsor): AMEND.** Same standing order as above. Add a *canal auto-lock*: ships pass a lock without a command, and a canal that reaches an ocean merges `waterBounds` once per `TERR_EDIT`. The Sapper discount applies to gates and locks only. Trench Lander replaced by the unified NAVAL-4 above.

**Docket #14, Dispersal (co-sponsor): AMEND.** *One-Button:* a magazine needs a reload; reload must be **automatic** when the launcher is within 400 px of a Depot, Pen or Shipyard, and it queues on its own. Without that, 4 TELs + 8 subs is a chore. *Line:* vertical for subs; TELs relocate along the line.

**Docket #9, Deep Deposits (co-sponsor): KEEP.** Sea-floor deposits move to Undersea Works. Passive, scales by volume.

**Docket #3, The Fleet Reads You (co-sponsor): KEEP.** The Flak Minelayer lays its mines automatically along the capital's path. Enemy-side change, no attention cost.

Tally: KEEP 2 (Deep Deposits, Fleet Reads You), AMEND 5 (NAVAL-1, NAVAL-4, NAVAL-X, Moat, Dispersal), WITHDRAW 3 (NAVAL-2, NAVAL-3, NAVAL-5).

## 3. The Order Paper

### #7: NAVAL-6, THE FLOATING WORKS (the surface layer; the treaty with Undersea)

**The division of the sea.** Naval owns **the waterline and the first submerged lane** (ships, subs, floating platforms, Shipyard, Heavy Port, Pens' surface stations). Undersea Works owns **the sea floor** (anything with `seabed:true`). The boundary is `seaLevelY(x) + 24 px`. A submarine may transit above the floor but never occupies it. Neither party builds on the other's layer.

**Pitch.** The Admiralty proposes the one thing a harbour has always needed: space. A **Floating Works** is a pontoon platform on water that accepts land buildings on its deck. A Mulberry harbour made industrial.

**Player fantasy.** You pave a bay with rafts and put factories on it. The coastline becomes a city.

**Clerk's Annex**
- **Mechanic.** *Raft* (IC 40 / MP 10, 24 px wide, HP 220, `needWater`, no depth minimum). A building whose footprint is wholly on adjoining Rafts counts as on land for `canPlace`; it sits at `seaLevelY(x)`. Rafts chain to any length. *Tender* (IC 260, naval, speed 6): repairs Rafts and ships within 300 px automatically, rearms subs, and is the only surface tender needed. No cap. Real basis: Mulberry artificial harbours (1944) and floating production vessels (FPSOs) are built from linked pontoons.
- **Enemy answer.** Rafts are surface targets, so orbital beams and bombers hit them at full strength (unlike the seabed). A raft district is the exposed, cheap, expendable layer; that is the deliberate counterweight to the Commissioner's sheltered floor.
- **Code grounding.** `canPlace` L3866 (new `onRaft` branch beside `needWater` L3870-71), `mkBuilding` L3670, `bldSurfaceY` L3671, `waterBounds` L3760, flood-kill rule L6561 (rafts exempt as `b.raft`), naval repair via the nanite aura pattern. 
- **Size.** M.
- **TM1.**
- **Research.** New node `harbourworks` (branch `def`, tier 1, pre `info`, cost ~90, not dangerous). Combo `nanotech`: self-repairing rafts.
- **Tenet (Am. I).** Adds a place to build; enemy answer is exposure. No cap.
- **One-Button.** Place a raft, drag it long, place buildings. Rafts are volume: ten rafts, ten times the shore.
- **Line.** The raft strip is vertical-adjacent: it extends the surface row horizontally across water, adding no depth.

### #3: NAVAL-7, THE GUNLINE (the sea beside a one-dimensional front)

**Pitch.** On a Line there is no flank, so a coast is the only place a fleet can stand at an angle to the enemy. The sea is a second lane running beside the front. Make the navy fight there by default.

**Player fantasy.** You build ships and they simply appear along the shore nearest the fighting, shelling the landing zone.

**Clerk's Annex**
- **Mechanic.** A **standing fleet order**, set once: SUPPORT THE FRONT (default on). Idle naval units reposition inside their `waterBounds` to the water tile nearest the active landing (the FRONT marker, v4.1.0). Ships with `range >= 1000` (Arsenal Ship, Battlecruiser, grand battleship) fire at ground targets inside range at **x1.0** (no buff). The enemy gets a coastal counter: **Shore Raiders**, ground units that target ships at the waterline, and the Trench Hunters of NAVAL-4. An inland front gives ships nothing to shoot, which is honest: a navy earns its place on a coastal front.
- **Code grounding.** naval move block L8123-8137 (ranged-hold already anchors the ship on a fraction of its range; add the nearest-water-to-front target), FRONT marker state, `acquireEnemy` L4374, `scanEnemyIntel` `navy` weight L5742.
- **Size.** S (about 80 lines).
- **TM1.**
- **Research.** None (a standing order). 
- **Tenet (Am. I).** No stat change. The enemy answer is coastal raiders.
- **One-Button.** One standing choice, on by default.
- **Line.** This bill *is* the Line answer: the sea is a parallel lane on the strip.

### #10: NAVAL-8, THE CAPITAL-HUNTER (a scarier enemy for the dominant doctrine)

**Pitch.** Report 8 shows 138 sky capitals, 240 Falcons and 113 Kites, ten to one in damage. The Counter-Air Doctrine hunts fighters and runways; nothing hunts the capitals. Give the fleet a capital-killer.

**Clerk's Annex**
- **Mechanic.** Enemy kind *Cataphract* (heavy anti-ship missile frigate, 1,200 HP x `t`, armor 20, range 1,500, 4 salvos of 3 heavy missiles, 90 dmg each, strong vs `capital`/shielded hulls). It spawns only when the scan's `skyNavy` axis reads >= 0.8 (the same read as Motion A), in packs of 2-4, and holds in the stratosphere. Real basis: anti-ship ballistic and supersonic missiles such as the P-800 Oniks exist to kill big ships.
- **Code grounding.** `scanEnemyIntel` L5737 (the `skyNavy` axis), `counterBias` L5513, `spawnEnemy` L5581, `acquireEnemy` L4374.
- **Size.** S-M.
- **TM1.**
- **Research.** None (enemy).
- **Tenet (Am. I).** A scarier enemy, the preferred answer. No nerf to the player's capitals.
- **One-Button.** The player does nothing.
- **Line.** Vertical (stratosphere versus surface and sky).

## 4. Blatantly overpowered today

**Nothing naval.** Three ships in report 8 against 240 Falcons is not an overpowered navy. The evidence of blatant dominance is the *sky doctrine*: 240 Falcons, 113 Kites and 138 sky capitals, ten to one in damage, and report 10 flawless. That is Air's portfolio; the Admiralty asks for the answer as an enemy (NAVAL-8) and for the Counter-Air Doctrine (#3), not a nerf, because no report shows the doctrine makes losing *impossible*, only certain.

**Why was the navy barely fielded?** My honest findings:
1. **A ship only fights inside its own puddle.** `waterBounds` clamps ships to one body of water (L8125). A fleet on the far coast never sees a shot.
2. **The enemy never touches the sea.** `spawnEnemy` and `doSkyfall` land on ground and sky. A ship that never earns a kill is never rebuilt.
3. **Ships need a Shipyard on water,** a map-dependent precondition (`needWater`, L3391). Aircraft need only an Airbase.
4. **Air does the job better.** Naval guns reach the low-air `ceil` .56-.62, so ships cannot touch what the Falcons kill.
5. **There is no research.** `navalpwr` and `kinetics` start unlocked (L3588). A player has no reason to *visit* the ocean tree.
6. **Ships cost army cap** (they are not `isMechanical`) at 18-30 MP each, so a ship is a Falcon you cannot afford to lose.

NAVAL-4, -6 and -7 plus the Moat attack points 1, 2 and 3; the Silent Service and the Gunline attack point 5.

## 5. Struck self-limits

In the First Session I struck no limit as "a nerf." I did concede that the Flood Sluice evacuation and the Canal Lock's 240 px span were costs, not caps, and the House did not touch them. Both stay. I do **restore** one thing: NAVAL-1's old line "hidden units are revealed 3 s after firing" must remain. If the House ever calls that a nerf, it is the only thing stopping an invisible launcher exploit, and Amendment I's restoration clause applies.
