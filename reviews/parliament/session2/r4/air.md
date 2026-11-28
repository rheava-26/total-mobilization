# AIR COUNCIL — Round Four: Second Reading

## 1. Speech

Mr Speaker, the Air Staff has read the President's Address twice, and the second time with a cigar.

Amendment I says a nerf needs proof. Good. I have been proved against by nobody, and I intend to keep it that way. Amendment II says nothing may need babysitting per unit. Mr Speaker, I fly two hundred and forty Falcons and I have not touched one since the PREP timer. My air force is the original One-Button army. Amendment III says the ground is a line. I have *always* said so. The ground is a line and the sky is a cathedral with six floors, and the Air Staff lives on all of them.

My honest confession: my wing wins because nothing hunts it, and nothing hunts it because the fleet cannot count aeroplanes. Docket item three fixes that. I will fund my own opposition. Then I will beat it with a net and a tanker. That is not a nerf; that is a war.

## 2. The Re-reading

**AIR-1, Pelican tanker wing. AMEND.** *One-Button:* the original had a hand-placed orbit anchor. That is a chore. Amendment: the tanker **auto-orbits** 600 px behind the wing's current staging point (the existing `AIR_RALLY` logic, L4818) and auto-refuels whoever is bingo. Nothing to place, nothing to click. *Line:* air bands are vertical; it passes. Farp strip stays an optional building.

**AIR-2, Argus AEW and datalink. KEEP.** Already folded into the engagement net (docket, `firecontrol_net`). *One-Button:* the net is automatic membership, SYNC FIRE is optional, and an idle player gets the passive +20% launch-on-remote. *Line:* passes; aircraft live above the line.

**AIR-3 and AIR-5, Reavers, Weasels, Durandal, shelters (docket item 3). AMEND.** I co-sponsor the coalition. Fix: **EMCON must be automatic**. When a Weasel is detected within 900 px, emitters in its cone go silent by themselves for 12 s; the player may only choose a standing policy once ("auto / never"). Repair is automatic when an Overwatch or builder is in range. Shelters are build-and-forget. Without this, "switch the radar off" is a per-building chore, so it fails Amendment II.

**AIR-4, Shrike EW jet. AMEND (shrink to S).** One-Button: it flies its sortie by itself with the wing. The separate noise cone is dropped; it reuses the net's `pdBudget` drain (L8554/L8620) at 3x. The "3 Shrikes per target" wording becomes a physics fact, not a cap: accuracy loss bottoms out. *Line:* passes.

**AIR-X, Pilots Are People. KEEP.** *One-Button:* ace promotion, bail-out and rescue are all automatic: the nearest idle heli answers a beacon, and the replacement inherits rank at no order. I add the guard that rescue never preempts a combat heli in contact. *Line:* passes. Perf: aces are named only on crewed airframes, so tens, not thousands.

**Motion C, Sortie Supply (Air + Industry). WITHDRAW as a separate bill.** Fuel and rearm bonuses fold into the Planning Bureau's ledger and Industry's ammunition-train bonus (`logistics` node). I do not want a second logistics screen. The tanker stays as AIR-1.

**Docket: the engagement net (`firecontrol_net`). KEEP.** Already automatic by default. **Docket: Charter and Decrees, dipping-sonar Kite and sonobuoys. KEEP.** The Kite auto-dips when a depth-1 contact is within 500 px; the sonobuoy drop is an automatic Lancer sortie option, a single standing choice.

## 3. The Order Paper

### Item #10: Is air blatantly overpowered?

**Honest answer: no, under Amendment I's bar, and a nerf is the wrong answer.** The bar is "losing becomes impossible or every other option is pointless", and the proof has to be in the reports and the code.
- Reports 6-10 were all wins, **including report 7**, which was a strike-and-space doctrine that went through 44-46 strikes and a stability crash. Air is not the only road to a win. Reports 8 and 10 were flawless, but that is the signature of a game with no answer to one doctrine, not of one unit that cannot be beaten.
- The cause is in the code. `scanEnemyIntel` (L5737) has no air or sky-navy axis, so the fleet never counters a wing. The enemy air roster (`interceptor`, `bomber`, `gunship`, `strikewing`, `raider`, `jammer`, `hijacker`, L5591-5604) never hunts fighters.
- The unit table shows an armour wall already exists. `hurt()` (L4991) subtracts armour per hit: `eff = max(1, dmg - armor)`. A Falcon rocket (dmg 31) against the Land Leviathan (armor 22) does 9 per hit; the Kite does 22. Light-rocket spam is slow against heavy armour. The "10:1" figure is mostly against the fleet's soft air and light ground.
- The one watch-list item is **Kite cost-efficiency**: 120 IC, 95 hp, armor 4, dps of about 15 (44 dmg / 2.86 s) on its own rotor cap with city baseline slots (`CITY_ROTOR`, L3826), against a Falcon at 270 IC for about 15.7 dps. Report 8 fielded 113 Kites against 240 Falcons, so it is not dominant. I recommend no action until a report shows Kites alone carrying a win.

**The right answer is an enemy answer plus a real cost**, not a nerf. The enemy answer is docket item three. I add one more bill, AIR-7, below, and the real cost is fuel and runways (AIR-1, AIR-5), which are costs of *operating* air, not caps on buying it.

### AIR-7: Flak Belt (the Skyhook pods, upgraded from banked ROADMAP L922)

**Pitch.** The enemy cannot count aeroplanes today. When it can, it should not merely send fighters at them. It should seed the sky itself.

**Clerk's Annex**
- **Mechanic.** When the fleet's `air` axis reads at least 0.8 (docket item three) and `invPhase >= 3`, it drops a **Flak Barge**: 6 proximity-fuse pods over the densest column of your air (the same bucket-and-rank logic as `densestDefenseX`). Pods arm in 4 s, last 45 s, hp 30t, range 620, ceil STRATO, airburst r 46 for dmg 34t, rate 1.4 s. One barge wave costs a 240-strong wing roughly 5-8% of the aircraft inside the radius if left alone.
- **Counters (all player-friendly, automatic).** Pods are cheap, so anything that shoots at pods kills them: Falcons' wingman cannons, Warden lasers, Argus-linked fighters take the pod as designated first priority when the net is up. Shrikes drain a pod's interception budget 3x. A shelter or a tanker-escorted standoff lets the wing wait out the 45 s. The pods *cost the player nothing to answer* beyond ordinary fire.
- **Code grounding.** `spawnEnemy` L5581 (add `flakpod` beside `shieldgen` and `jammer` branches); `waveAirKind` L5524 and `counterBias` L5513 for the weight; `scanEnemyIntel` L5737 (the same `air` axis as docket item three); airburst code is KINETIC-1's `splashDamage` L7546, shared.
- **Size.** M (about 180 lines, reusing KINETIC-1 and docket three).
- **TM1.**
- **Research attachment.** None (enemy-side). The player counter-technology is the existing `datalink`, `ewarfare` and `directedenergy` nodes.
- **Tenet check, Amendment I.** Enemy-side only. It buffs the enemy and the Amendment's second rule says that must not make the player's army worthless: the pods die in 45 s and are priority targets, so no existing unit becomes useless. No player nerf.
- **One-Button check.** The player does nothing. The wing auto-targets pods, the net designates them, EMCON and shelters are automatic.
- **Line check.** Pods hang in the vertical air bands. It passes.

### Item #2: What air brings to a mixed army against huge-HP enemies

**AIR-6: The Heat Wing (air as the delivery and cash-in arm of the Thermal Field).** Docket item one makes heat strip up to 50% of armour and add up to x1.5 kinetic damage. Armour is subtractive (`hurt` L4991), so for light rounds the reward is enormous: a Falcon's 31 against armour 22 does 9; against heat-stripped armour 11 it does 20, **2.2x**. Air should be the *fast* half of that combo.
- **Mechanic.**
  1. **Deposit side.** `laserplane` "Solaris" (range 720) and `pdjet` "Warden" (range 640), both beam planes that already exist, deposit heat at their normal beam rate. They are the airborne heaters, which gives the laser branch a seat in an air army.
  2. **Cash-in side.** Falcon rockets, Kite rockets and Lancer bombs deal heat-multiplied damage on hot targets by the docket-one rules. No change to those units.
  3. **Automatic focus.** Ordinary target selection gets a small score bonus for hot, high-armour targets (`+0.25 x heat/100` in the score), so a mixed wing stacks onto the cooked target without orders.
  4. **The monoculture wall is the enemy's.** A pure Falcon swarm is already slow against armour 22; a pure Solaris swarm deposits heat but its 30 dmg beams do little to a 1,700-hp walker. Falcons and Solaris together shred it. One type still works, slowly.
- **Real science.** Thermal softening: metals lose strength as they heat (steel yield drops by about half near 600 C), which is why lasers and then kinetics is a real combined-arms idea, and the thermal-load logic of docket item one.
- **Code grounding.** Docket item one's `heat` field (new); `UNIT_DEFS` laserplane L3226, pdjet L3216; `hurt()` L4991; target scoring in the air brain `updUnits()` L7809 and `acquireIncoming()` L4465.
- **Size.** S (about 60 lines on top of the docket's heat field).
- **TM1.**
- **Research attachment.** `thermalload` (docket item one) plus the existing `directedenergy` gate on Solaris/Warden. No new node.
- **Tenet check.** A buff, and the enemy does something about it: a heated enemy cools fast in air (docket), so the window is short and rewards a coordinated wing, not a bigger one. No nerf anywhere.
- **One-Button check.** Entirely automatic. Build both types and they cooperate.
- **Line check.** Beams and rockets fire along the line of sight; the bill is vertical and passes.

## 4. Blatantly overpowered today

**Nothing in my portfolio meets the bar.** Air is overperforming against an enemy that cannot read it, which is a gap in the enemy and not a flaw in the unit. I recommend the Kite stay on a watch-list only. If a future report shows a Kite-only army winning, the first answer is AIR-7, and the nerf is last.

## 5. Struck self-limits

I struck **none** of my own limits in the First Session. Three costs remain in my bills and I want them kept: the tanker's 4-aircraft transfer limit and `fuelStock` (a logistics cost, not a unit cap), the Shrike's stand-off burn-through (physics), and the 20-fighter gate on Reavers (it protects early game). The only item I would *restore* is the 6 s SYNC FIRE hold on units that choose to join, which is the net's real cost, and which the Sky Bloc has already kept.
