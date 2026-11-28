# Inspector-General of the Sky Bloc (`inspector-sky`)
*Audited: Air (`air`), Space Fleet (`space-fleet`), Rocket Forces (`missiles`), and the docket bills they co-sponsor. Code line numbers are from the live `title.html`.*

## 1. Opening speech

Honourable members. I was a test pilot. I now investigate craters, which is the same job from the other end. I have read 15,000 words of Sky Bloc paper, and not one of them contains the sentence "this might break." I will supply it.

The Air Marshal calls 240 Falcons "a screensaver". It is a screensaver that won five straight matches. The Admiral of the Void says space "dies expensively". It dies for free, and nobody else needed it. The Marshal of the Rocket Forces says his countdown is "the only honest sound in the war". It is honest because nobody presses the button.

I love this bloc. That is why I bring the clipboard. Where a bill is clean I will say so, in writing, once. Where it is not, I will give the crater, the arithmetic and the smallest fix I will sign.

## 2. The charge sheet

**Counts: 7 CLEAN, 19 CHARGED** (18 First Session bills plus 8 co-sponsored coalition bills). Merged bills are judged where they ended up.

### Air (`air`)
1. **AIR-1 Pelican tanker. CHARGED, MICRO.** Its "hold-position anchor the player places" is per-unit steering. Demand: tankers auto-orbit 600 px behind the front's centroid, and the anchor is an optional override.
2. **AIR-2 Argus datalink.** Merged into ONE NET (item 23). **CHARGED** there.
3. **AIR-3 Reaver and Weasel.** Merged into Bill 3 (item 19). **CHARGED** there.
4. **AIR-4 Shrike EW jet. CLEAN.** It is a buff with a real exposure: a fragile standoff unit, 250 IC, jammable. Its "3 stack" is accuracy bottoming out, not a hidden cap. Check that `pdBudget` exists as the bill claims. It does, via `PD_SAT` (L8536).
5. **AIR-5 Durandal, Shelter, Repair.** Merged into Bill 3 (item 19). **CHARGED** there.
6. **AIR-X Pilots Are People. CHARGED, MICRO.** A Kite must "hover within 80 px for 3 s" per beacon, and ace names are per-airframe. Demand: rescue is automatic by the nearest rotor unit in range, and aces are shown as a roster counter, not per-unit chores.

### Space Fleet (`space-fleet`)
7. **SPACE-1 Tinsel decoys. CLEAN.** At 8 IC and 0.25 lift, it is a cheap volume sink that the enemy can learn to ignore (a scarier enemy). `orbCapUsed` already sums float lift (L3813).
8. **SPACE-2 Cooperative Engagement Net.** Merged into ONE NET. **CHARGED** (item 23).
9. **SPACE-3 Helios. CHARGED, EXPLOIT.** "+1 IC/s per Power Plant within 1,500 px" is per Helios. It is quadratic: 10 Helios among 30 clustered plants is 10 × (2.5 + 30) = 325 IC/s from 4,200 IC, a payback of about 13 s. The Round 3 text (+1 MW per plant per Helios) keeps this. Demand: each plant feeds one Helios, assigned automatically, so the link is linear.
10. **SPACE-4 Retrograde Shotgun. CHARGED, FALSE.** The claim is that the lane "kills grid satellites". The code says `railsat` hp is `20 × t` (L5644). The pellet does `35 + 1.2t`. It kills in one shot only when t ≤ 1.9. At t = 7.4 (invTime 300 s, General) it does 44 against 148 hp, so four reloads of 22 s each. Demand: lane damage scales with the same `t` as the hull, or it is a minute-one weapon.
11. **SPACE-5 Orion Battleship. CLEAN.** It costs 1,400 IC, 6 rocket parts and uranium, and is `dang`. It is rebuildable at full price, not a free unlock. Note for the record: on the numbers it is a worse Sky Railgun Ship, which is a design question (Section 3), not a crime.
12. **SPACE-X Kessler Doctrine. CHARGED, TENET.** Round 3 gave away its only real cost. See Section 4.

### Rocket Forces (`missiles`)
13. **MISSILES-1 Cruise ripple. CLEAN.** It is volume-scaled, costs IC per round, and is auto-fireable through `tickAutoStrikes`. A note on PERF only: a terrain snap per frame per round is fine on the cached heightfield.
14. **MISSILES-2 Dispersal.** See Bill 14 (item 24). **CLEAN.**
15. **MISSILES-3 HGV. CHARGED, useless.** At 70 IC it does 160 damage plus a 90 px splash. `ballistic` already does 360 at R150 for 35 IC with no stability cost (L6886, `detonateHE`). The only edge is a shorter PD window, and `PD_SAT` is saturable anyway. Demand: either price it at ≤35 IC, or give it a unique job (shield-breaker, since missiles overload shields in `hurt()`).
16. **MISSILES-4 PN and seekers. CHARGED, CREEP.** The enemy flares divert 60% of IR rounds. If current SAMs and Falcon missiles default to `ir`, the existing army gets worse. Demand: every existing missile defaults to a flare-proof package, and `ir` is the cheap alternative.
17. **MISSILES-5 Warhead Stockpile.** Merged into Bill 7. **CHARGED** (item 22).
18. **MISSILES-X Starfish.** Merged into Dead Sky. **CHARGED** (item 21).

### Coalition bills
19. **Bill 3 THE FLEET READS YOU. CHARGED.**
   - **Wrong half.** The 138 sky ships need no runway, no tanker and no emitter. Durandal, Weasel and the tanker hunt hit the Falcons only.
   - **FALSE premise.** "Nothing hunts fighters." `interceptor` is commented "hunts your aircraft" (L5598), `raider` is "AGILE ESCORT FIGHTER", and the manual (L777) says "they hunt your aircraft first".
   - **Gate undefined.** "8+ parked aircraft" exceeds one Airbase's 6 slots (L3818). A spread-out player of 3-slot Underground Airwings never trips it, so it rewards dispersal.
   - **MICRO.** The "EMCON 12 s" player order must be automatic.
   - **Fix demanded:** add a sky-navy scan axis (capital count ≥ 20) that spawns Annihilator beam waves and antimatter whistlers. Make EMCON automatic when an ARM round is inbound. Define "parked" as aircraft within 400 px of a strip.
20. **Bill 5 THE STANDING PLAN. CHARGED, MONOCULTURE.** The exchange line "buys last fight's best kills-per-IC". Air won, so the market buys air, so air wins. It is a loop with no enemy in it. Fix: pay the dividend on killing varied enemy kinds, not on raw kills-per-IC.
21. **Bill 6 DEAD SKY PROTOCOL. CHARGED, FALSE.**
   - The Starfish hook `e.elec` has **0 hits** in `title.html`.
   - The self-EMP selector `isMechanical` (L4541) is false for Falcons and for sky ships (it needs `mech`, `drone`, `elec` or `clone`). It is also false for Halo, Volley and Zeus, which have no `elec` flag. It hits Frigate, Gunboat, Picket and Strike Fighter, because those carry `elec:true`. So "Starfish is a real cost" lands on the space fleet and spares air, which makes air dominance worse and makes Air's Article III reservation moot.
   - Lane damage is flat against `20t` hull hp (item 10).
   - Fix: define an explicit `emp` flag list, and decide in writing which units it covers.
22. **Bill 7 AMMUNITION & THE ONE LOCKER. CHARGED, MICRO.** "A per-group round selector for every kinetic gun" is exactly what the One-Button Law forbids. Fix: auto-select by target armour, with one standing "ammo doctrine" choice. For missiles: the stockpile's "each consecutive use costs 25% less stability" undoes the "every launch is a decision" pitch. Delete it. The stock is the brake.
23. **Bill 11 ONE NET. CHARGED, OP.** The volley bonus is "+4% per linked unit, uncapped". Damage is N shots × (1 + 0.04N), which is quadratic. With 240 Falcons plus 138 ships, N is about 380, so each shot is ×16 and the volley is about 6,000 shot-equivalents. That kills the flagship in one press. It also needs a button press (MICRO), and Fire Control is dead code (`G.fireControl` is never set, L8447) for exactly this reason. Fix: delete the per-unit bonus (N shots already scale with N), and make SYNC auto-fire on any boss-class target. Restore the original clause "no more than the units' own firing" (Section 4).
24. **Bill 14 DISPERSAL. CLEAN.** The silo is the only strategic launcher today (L3407). Hidden launchers and a shared stock are the right fix. TEL relocation is automatic.
25. **Bill 15 THE POWER GRID. CHARGED.** Helios link is quadratic (item 9). The railgun overdrive "bore wear as real cost" must stay as filed. This bill is the one place the wear self-limit is still alive.
26. **Orion (smaller bill). CLEAN.** See item 11.

## 3. Blatantly overpowered today

**3a. Sky capital ships are priced like fighters. OP, bug. Meets Amendment I.**
- `unitCost` (L3835) uses `facsFor(d.domain)`. `facsFor` counts every building with `prod === 'air'`, which is **Airbases and Heli-Depots**, giving `disc = max(.22, 1 − .09(n−1))`.
- Sky warships are `domain:'air', capital:true, facKey:'skydock'`. They inherit the Airbase discount, and Sky Docks add none.
- At 10 airbases the Sky Railgun Ship costs **820 × .22 = 180 IC** for 2,600 hp, armour 20, a 600 shield, range 1,700 and `ceil:0`, which reaches the whole sky including space.
- It takes **no hangar slot** (L3820 excludes `capital`). Its upkeep is 8 IC/s, cut up to 60% by Logistics Hubs, and `IC_FLOOR` (L6254) means upkeep can never bankrupt you.
- Against it: Halo is 320 IC for 50 hp and needs lift 3 and a rocket part. A Halo cannot reach what a Sky Railgun already shoots.
- Report 8 had 138 sky ships and ten-to-one damage. Report 10 was flawless. The space fleet (Report 7), strikes (zero in 8 and 10), infantry and the navy are all pointless next to it. That is "makes every other choice pointless".
- **Verdict: both.** Bug fix: `unitCost` should use `facsForUnit(d)` (L9924, which already exists), so sky ships discount by Sky Docks. Enemy answer: the sky-navy axis in item 19.

**3b. Falcons: strong, not blatant. Needs an enemy answer, not a nerf.**
- A Falcon is three bodies for one slot and 270 IC. Per slot: 31/1.98 = 15.7 dps for the crewed jet, plus two wingmen at 0.55 × 1.15 each (L3961) = about 35.6 dps.
- At 10 airbases it costs 59 IC. A slot costs 30 IC (Airbase 180/6) or 38 IC (Underground Airwing 115/3). 240 Falcons need 40 airbases (7,200 IC). That is about 8 s of 850 IC/s. **The hangar cap is not a limit.**
- Falcon HP is fixed at 55. Enemy air `dmg` is mostly flat (interceptor 28, gunship 21, strikewing 40, L5598–5604), while enemy HP scales `×t`. Only `raider` scales damage (`22t`). So Falcon dominance grows with time.
- **Verdict: enemy answer.** Scale enemy air `dmg` with `t` (the raider already does), and use the Reaver wing as the carrier. No nerf. The bar is not met by Falcons alone.

**3c. The space fleet's death. A design gap, not a balance problem.**
- Halo range 1,200 against `RAILSAT_RANGE` 1,900 (L8524) loses every opening shot.
- But railsats hold at `MESO_TOP+40..+120` (L5644). That is the mesosphere, **within reach of air and sky ships**, so air already kills the grid.
- Space has no job that air lacks. Bill 2's Orbital Denial door is the right fix. Enemy answer: a space-first gambit.

**3d. Strategic strikes. Neither OP nor useless. Two real defects.**
- **Stability.** Only `taboo` strikes cost stability (`stratStab`, L6897). Ballistic and Kinetic cost none.
- **Reports 6–7's 44–46 strikes** come from `tickAutoStrikes`, which keeps firing taboo rounds until `G.stab <= sc + 5`. An unattended default spends stability to the floor. That is a One-Button footgun, not a design.
- **Flat yield.** Nuke 720, HE 360, Kinetic 620, none scaling with `t`. Bomber hp is `52t`. At t ≈ 13 (ten minutes, General) a bomber has 680 hp. Damage-percent research does not touch strikes (`areaDamage` takes no multiplier). So strikes decay into chaff-clearing.
- **Demand:** auto-strike prefers non-taboo and obeys a standing stability reserve (default 50%). That is a default, not a nerf. Let strike yield read `G.mult.dmg`, which is a buff, so pair it with a bigger enemy answer: sanitiser decoys and more PD saturation.

## 4. Struck self-limits that should come back

1. **SPACE-X "your own lift offline" (Round 3, Missiles Amendment 1).** It was the only thing making the Burn a cost. Restore it as time-boxed: lift offline while the belt holds above 50% (about 90 s). It is a real cost, not a cap.
2. **SPACE-2 "no more than the units' own firing".** It was the original bound on the volley bonus, loosened to uncapped in Air's Round 3 clarification. Restore it as "volley bonus ≤ +100%". Without it, item 23 is quadratic.
3. **SPACE-3 "+1 IC/s per Power Plant".** Space offered to drop it in Round 2, then kept it in Round 3. Drop it, or make it one plant per Helios.
4. **MISSILES-5 "no cap on stock, only upkeep".** The upkeep (0.2 IC/s per nuke, plus fallout risk) is the *only* brake. Keep both, and keep the depot fragile.

## 5. Questions for Question Time (Round Five)

1. **Air:** Your Durandal gate is "8+ parked aircraft" and an Airbase holds 6 (L3818). Which building counts the parked 8? And what in the bill touches 138 sky ships that never use a runway?
2. **Air:** Enemy `raider` and `interceptor` already hunt fighters (L5598, manual L777). What new thing does the Reaver do, and does its `dmg` scale with `t`?
3. **Space Fleet:** Railsats hold at the mesosphere (`MESO_TOP+40..+120`, L5644). Which debris half-life applies there: 12 s, 40 s or 120 s? And why can a Sky Railgun Ship not do the Halo's job already?
4. **Space Fleet:** Is each Helios linked to every Power Plant in 1,500 px or to one? Show me the payback at 10 Helios and 30 plants.
5. **Missiles:** `e.elec` has no hits in the code. How does Starfish pick enemy electronics, and which player units does it cover? `isMechanical` spares every aircraft.
6. **Missiles:** Will you accept auto-strike defaulting to non-taboo with a 50% stability reserve, and strike yield reading `G.mult.dmg`?
