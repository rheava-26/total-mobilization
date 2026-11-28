# GROUND FORCES COUNCIL — Round 3 Paper (`ground`)

## 1. Answers (motions naming `ground`)

### Motion A, "THE LIVING BATTALION" (bio) — AMEND
I co-sign with three amendments, all of them mechanical:
1. **Pool, not bodies.** Regrowth feeds the battalion's pooled body count (GROUND-1), not individual squads. Bio-family battalions regrow 1 body per 6 s ÷ (1 + 0.15 × Gene Facilities), unhit for 8 s, paying 1 MP per body *only while MP is above zero*. At MP 0 regrowth switches to the IC draft (no new MP, Art. IV.1) instead of pausing. A bio battalion is therefore never worse off than a non-bio one.
2. **Non-bio drafts** need a Barracks, Veterans' Hall or Fortress HQ within 400 px, at 1 squad per 6 s for IC only, and respect the existing weighted army cap.
3. **Shaken rule stays.** Below 40% strength a battalion is Shaken (−25% rate) until relieved or refilled. Bio regrowth clears it as fast as bodies return.
Size: GROUND-1's L plus bio's ~150 lines. TM1. Tenet: all buff, costs are MP/IC.

### Motion 2, "FIRE FOR EFFECT, SUPPLIED" (kinetic-arms) — ACCEPT
This is my Motion 1 and GROUND-3 with INDUSTRY-1 bolted on, which is what I wanted. One request, not a condition: the `firefinder` counter-battery radar (240 IC, range 1,400 px, 2 s reaction, fires linked guns at the back-traced origin ±40 px) must survive in the merged bill, since KINETIC-4's reveal-for-10-s is the weaker half. Share the delay solver with SPACE-2's Cooperative Engagement Net so there is one `solveToT`.

## 2. Joins

- **Planet/Naval "THE NAVIGABLE MOAT" (naval, to planet).** JOIN. Canals plus sluice chains are my Sapper Corps' work; sappers build them at 60% IC and `amph` units cross.
- **"The Trunk" (rail under the continent).** JOIN, with one clause: every Rail Hub is also a **Sally Port**, from which battalions debark behind a named Line (GROUND-2). Hubs are already `rail:true` and idle troops already dive into them.
- **Any motion that routes standing orders (COMMAND-1/INDUSTRY-3).** JOIN, with the order `RAISE battalion <type> N AT <line>` added.
- **Starfish/EMP motions.** Nothing to join, but note battalions of robotic bodies need a `hardened` flag so one burst does not delete the army; the Psionic Relay/Data Center aura rules already model this.

## 3. Competing Motions

- **Time-on-target.** I back **kinetic-arms Motion 2** as the master, with my radar and SPACE-2's solver folded in. My own Motion 1 is withdrawn in its favour.
- **Rail under the continent.** I back **"The Trunk"** (industry/planet) over the four near-duplicates because it has a single graph, a single upkeep number (1.5 IC/s per 1,000 px) and a Molodets unit that rides it. I attach Sally Ports.
- **Battalions/regrowth.** I back **bio's Motion A** as amended above over my own Motion 2; theirs is cleaner on the regrowth numbers.
- **Terrain works.** I back **Planet/Naval "NAVIGABLE MOAT"** for canals and floods, with my Motion 3 reduced to the Sapper unit and mines/ditches as the labour force for it.

## 4. Closing Statement

Gentlemen, the Army accepts a coalition. We give you the gun-laying and the draft; the artillerymen give us the barrage; the biologists give us the axolotl. Between us we have built an army that is a *line* with a pipeline behind it: one named battalion at the front, one barracks behind it, one battery counting seconds. I ask the Speaker for one thing only, which is that the Air Council be seated downwind of my mines.
