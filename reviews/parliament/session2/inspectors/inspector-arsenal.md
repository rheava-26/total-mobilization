# Inspector-General of the Arsenal Bloc (`inspector-arsenal`)
*Lines are `title.html` as it stands (12,243 lines); council citations have drifted, so I re-checked each.*

## 1. Opening speech

Honourable members, the Inspector-General rises with a stopwatch and a bad feeling.

I have watched a thousand wonder-weapons fail acceptance trials. The brochure always says "multiplier". The range report always says "it jammed". My bloc has four councils and one thermometer, and each swore to me that its bill is "a pure buff, nothing is capped".

That is the confession. A pure buff with no enemy reply is not a design. It is a bonus on a bonus.

I did the arithmetic. A Lance hit does 441 today. Under the bloc's bills the same pair of rails does 1,389. I love this game more than my party does, so I went looking for the multiplier nobody has stacked yet. *(sets stopwatch down; it is still ticking)*

## 2. The charge sheet

**Counts: CHARGED 9, CLEAN 3.** Bills merged into a docket bill are ruled under their parent.

### Docket 1. THE THERMAL FIELD (energy, physics, psi; joined by railgun). **CHARGED**
Merges ENERGY-1, ENERGY-4, PHYSICS-1, PHYSICS-2 and PSI-2. This is the President's bill, so I answer his three questions first.

**Does it punish monoculture through the enemy? No. It is a carrot, not a wall.** Heat only adds. A one-type army loses a x1.5 it never had.
- Heat saturates almost at once. A Solaris does 30 x 2.55 (research) x 0.9 = 69 heat per hit, every 0.57 s. A Laser Tower deposits about 22 heat/s against 10/s cooling. Either one holds a boss at heat 100.
- "Forced to get new things" therefore costs 150-240 IC, once. Then the player goes back to spamming.

**Is heat plus kinetic a multiplier on a multiplier? Yes.** The live stack today is research dmg x2.55 (L3511-3536) times `HIT_BUFF` 1.3 x 1.9 = x2.47 (`hurt`, L4978).
- Thermal x1.5, sabot x1.4 and Power Bank overdrive x1.5 are all new and all multiply.
- A Lance hit goes from 70 x 2.55 x 2.47 = 441 to 70 x 2.55 x 1.5 x 1.4 x 1.5 x 2.47 = 1,389.

**Does it need micro? No.** It is automatic; PERF is fine if the tint is bucketed.

**Further charges:**
- **FALSE (hook).** The bill hooks `hurt()`. `splashDamage` (L7553) and `areaDamage` (L7544) do `e.hp -= d2` directly and bypass `hurt()`. The Falcon missile, the dominant doctrine's weapon, lands through `splashDamage` (L9149).
  - As drafted, "every kinetic gets stronger" is false.
  - If someone patches the splash path, the dominant air doctrine gets x1.5 for free (CREEP).
- **FALSE (armour).** "Heat strips the Leviathan's armour" is nearly inert. Enemy armour never scales (`spawnEnemy`, L5588+); only HP does, as `t`. A Lance hit is 441 against armour 22 x 0.55 = 12.1, so the armour is 2.7% of the hit. The cut matters only for small bore (see D7).
- **CREEP (the drop).** Heat 90, blackout, debris belts (D6) and the Breathing Sky all hit one window, and the fleet answers none.

**Fix I demand.**
1. Add an **enemy wall**. Boss and capital kinds (leviathan, hive, bombarder, carrier, earthbreaker; base HP 620 and up) get *cold plate*: kinetic x0.5 at heat 0, rising linearly to x1.25 at heat 100. Chaff is untouched.
   - A mixed army gets a 2.5x swing; a monoculture still works, only at half rate. That is the President's sentence.
   - The cure is one 240-IC plane, so the existing army is not worthless.
   - Net at full heat is only +25% over today, so the bonus is no longer a free x1.5.
2. Drop sabot's armour cut from the heat stack.
3. When `spaceReach` is the dominant intel axis (`scanEnemyIntel`, L5744), triple the share of existing `decoy` pods in the drop. That is the enemy's answer to the white-hot window.
4. State in the bill which paths take the bonus.

### Docket 7. AMMUNITION & THE ONE LOCKER (kinetic-arms, railgun, industry, missiles, energy). **CHARGED**
Absorbs KINETIC-1, KINETIC-2, RAILGUN-3 and INDUSTRY-1.
- **MICRO.** "A per-group round selector for every kinetic gun" needs groups, which means per-group attention. It also has a per-shot `munitions` bill.
- **FALSE (sabot).** KINETIC-2 claims "+20% against Leviathan, +60% against armour 36". The real numbers:
  - A 70-dmg shell is 70 x 2.47 = 173. Minus 12.1 is 160.8; minus 4.2 is 168.7. The gain is **+4.9%**.
  - Armour matters only for small bore. A 14-dmg AA pellet (x2.47 = 34.6) against the carrier's armour 30 (16.5) is 18.1, and 26.4 with a 50% cut.
- **FALSE and TENET (airburst).** The splash path has no `HIT_BUFF`. A Hailstorm pellet is 11 x 2.55 x 0.8 = 22 per target at the centre. The four direct pellets today do about 138 on one target.
  - Break-even is **6.3 enemies inside the 46 px burst**.
  - Swapping `aa` and `ciws` to airburst through `UPGRADES` would silently nerf them against bombers and interceptors.

**Fix.**
1. One standing doctrine per class (Rails, Guns, AA), chosen once, defaulting to **AUTO**: AP against armour 14 and up, airburst against four or more fliers in range, HE otherwise.
2. Route splash through the same x2.47 buff.
3. One burst per shot, not per pellet.
4. Keep "empty stock means base stats".

### Docket 10. THE LONG GUN (megaprojects, railgun, kinetic-arms, game-feel). **CHARGED**
Merges RAILGUN-2 and KINETIC-X (both withdrawn).

**Is unlimited sections a win button? Not by itself. The stack makes it one.** The arithmetic at N = 40 (Railgun R3: 100 x N damage, 20 s fire interval, 6 IC/s idle plus 40 IC per section per shot, 1.5 s slew per section):
- Base damage is 4,000. Research x2.55 gives 10,200, and the hit buff x2.47 gives about 25,200 on every target on the pierce line.
- Cost is 16,000 IC, 320 alloy, 240 IC/s idle and 1,600 IC per shot. A full swing takes 60 s.
- That is 1,260 dps per target in the column, worth about 8.6 Lances (2,500 IC). On raw economics it is worse than Lances.

The stack: heat x1.5, sabot slot x1.4, overdrive x1.5 (it is `weapon:'rail'`) is 79,000 per target per shot.
- Add Railgun's One Net volley rider of +4% per linked unit, "no ceiling". With 200 linked guns that is x9, or 714k.
- At `t` about 29 (General, invTime 1,400 s) the carrier is 9,000 x 29 = 261k. It dies to one shot, and so does every boss, every 20 s, with pierce.

**Other charges:**
- **FALSE.** MEGA-2 says "reuse the existing designation flow (`manual: true`)". Grep: nothing reads `d.manual`. `G.fireControl` is never set (L8454). "The player picks targets" is a new system, and it is MICRO.

**Fix.**
1. Strike the +4%-per-linked-unit volley bonus. The docket text already omits it.
2. Make upkeep superlinear: 6 x N x (1 + N/20). At N = 40 that is 720 IC/s, which is Report 7's whole income. It is a real cost, not a cap.
3. The gun auto-fires at the highest-HP target in its arc; no picking.
4. Keep the N >= 40 anchor boss and interdiction.

### Docket 11. ONE NET (ground, kinetic-arms, space-fleet, air, missiles). **CHARGED**
Absorbs KINETIC-4 (Fire for Effect).
- **MICRO.** "Click anywhere to place a barrage marker, 8 s cooldown" is per-shot attention. SYNC FIRE is optional in the docket, but KINETIC-4's click is the main verb.
- **CREEP (conditional).** The volley bonus of +4% per linked unit is x11 at 250 guns with no enemy answer.
**Fix.** Auto-barrage on the heaviest cluster in net range, with no click. A click is an optional override. Strike the volley bonus.

### Docket 15. THE POWER GRID (energy, railgun, space-fleet, physics, science). **CHARGED**
Merges ENERGY-2 and RAILGUN-1.
- **EXPLOIT.** Overdrive x1.5 damage and x1.25 rate applies to every `weapon:'rail'`, including the Mass-Driver Railway.
  - Its adjacency stack (L8448) is +60% per neighbour within 90 px, uncapped.
  - `OVERLAP_FRAC` .32 packs about 8 neighbours, so x5.8.
  - One packed massrail shot is 64 x 2.55 x 5.8 x 1.5 (overdrive) x 1.5 (heat) x 1.4 (sabot) x 2.47 = **7,365**, every 4 s. That is 1,840 dps for a 360-IC building. One hundred of them (36,000 IC) do 184k dps and delete the 261k carrier in 1.4 s.
- **Cost is unspecified.** "Shortfall becomes IC debt, never a throttle". What does debt block? If nothing, the cost is a fiction.
- **PERF.** The massrail scan (L8448) loops every building, every frame, for every massrail: O(n^2).

**Fix.**
1. Debt freezes new construction and research draw, not fire.
2. Wear auto-bills an IC reline.
3. A cluster of four or more massrails inside Bank range raises its `busterScore` (L7453, now 72) to 100. That is the enemy answer.
4. Cache the adjacency count at build and destroy time.

### Docket 8. THE TRUNK (industry, planet, command, railgun). **CHARGED**
Absorbs RAILGUN-X (the Meridian Accelerator).
- **FALSE (the cost).** "The cost is a vulnerable tunnel network." Blasts only gut `!b.ug` (L7394), and no enemy touches buried structures. The Earthbreaker has `weapon:'none'` and `quake:true`, and nothing reads `quake` against `ug`.
- The Meridian is mild on arithmetic (6,000 px gives 1,800 x 2.55, once a minute), so it is not OP. But the stated cost does not exist.

**Fix.** Give `quake` kinds a radius against `b.ug` (a scarier enemy), and say so in the bill.

### Docket 6. DEAD SKY PROTOCOL (space-fleet, missiles, physics, market, energy, science). **CHARGED**
Absorbs PHYSICS-3 and PHYSICS-4.
- **CREEP.** It is the third bill on the drop window: debris belts, Starfish blindness and zeroed PD. The enemy answers none of them.
- **MICRO.** Debris Sweepers are a chore unless they patrol themselves.
- PHYSICS-3's burst split is **CLEAN** on its own. It has real costs (taboo stability, friendly EMP backscatter), and its enemy answer (hardened variants) must ship in the same bill.

**Fix.** Sweepers auto-patrol. Scale existing `decoy` pods with debris density.

### Smaller bill. THE BREATHING SKY / THIN AIR (physics, science). **CHARGED**
Absorbs PHYSICS-5. This is CREEP.
- Hostile anchored units sink 6 px/s per section, uncapped. At 20 sections that is 120 px/s, which puts the grid inside rail reach in about a minute. The "space fleet dies to the grid" problem is solved by deleting the grid, and the fleet gets no answer.

**Fix.** Anchored units reboost at a rate that scales with the heater count, so N stays uncapped. Hardened pickets resist.

### ENERGY-H. THE FURNACE (energy). **CHARGED**
- **MICRO.** It is armed by click like `armSilo`, per shot.
- **Saturation.** 500 heliostats give 300 heat/s on one column, but heat caps at 100. Past about 20 heliostats, volume adds nothing, which breaks "scales with volume".

**Fix.** It auto-fires at the densest column on a 20 s cycle. Each 100 heliostats widens the column by 40 px, not its intensity.

### Spin-Up (KINETIC-3, game-feel). **CLEAN**
It is one float per gun and the +35% rate is the same order as a single Ammunition Depot (+25%), which already stacks uncapped. Rails and rods are excluded, so the meters do not fight.

### ENERGY-3. THE PLASMOID. **CLEAN**
A feel pass on an existing weapon. Enemy-only heat, and energy damage never takes the heat bonus, so it adds no multiplier.

### RAILGUN-4. THE LAUNCH RAIL. **CLEAN**
It answers Report 7 (18 of 18 Volleys lost), costs real IC, and the grid keeps its teeth. Rider: the Bombarder escort must target it.

*Withdrawn or merged, ruled under their docket parent: RAILGUN-1/2/3/X, KINETIC-1/2/4/X, ENERGY-1/2/4, PHYSICS-1/2/3/4/5.*

## 3. Blatantly overpowered today

Verdict first. **Nothing in this bloc is proven OP by Article VI.** The five wins were air and sky navy (240 Falcons, 113 Kites, 138 capital-class sky ships). Report 8 does not break down the 138, so the Sky Railgun Ship (320 dmg, pierce, 2,600 hp and a 600 shield, 8 IC/s upkeep) is a suspect, not a conviction.

What the **code** proves is below.

**Realistic late-game multiplier.**
- **The brief and CLAUDE.md are stale.** `massdrivers` +35% and `voidengine` +20% do not exist (cut, L3509).
- **Research damage** is `directedenergy` .10, `plasma` .18, `antimatter` .40, `robotics` .15, `colossus` .12, `transhuman` .20, `shapeshifter` .15 and `particlebeam` .25. That sums to 1.55, so **x2.55** (L3511-3536).
- **Research rate** is `automation` .12, `psistudies` .08, `telekinesis` .15 and `psionics` .15, so **x1.5** (units only; buildings read no rate research).
- Units get **x3.83 DPS** from research. Buildings get x2.55. A plasma-armed bunker is x4.08.
- Add the constant `HIT_BUFF` x2.47 against anything with armour 4 or more.

**1. Uncapped additive auras.** (`auraAt`, L3774-3778: `m += aura - 1` with no ceiling.) Ammunition Depot +25% rate (160 IC), War Academy +15% damage (260 IC), Radar +12% range. All three descriptions say "stacks".
- Five depots and five academies: units reach rate 1.5 x 2.25 = 3.4 and damage 2.55 x 1.75 = 4.5. That is **x15 DPS**, or about **x33** with the plasma AA re-arm (11 to 26 dmg).
- Ten of each (4,200 IC) gives rate x5.3 and damage x6.4, which is **x34 before the re-arm**.
- That makes the research rate branch pointless (Amendment I's "every other option pointless", by code). No match shows it, so it is latent.
- **Answer: an enemy answer plus a real cost, not a nerf.**
  - Three or more auras in one cluster raise `busterScore` (ammodepot is 26 today; waracademy counts as plain DEF at 52) to 72.
  - Existing `jammer` kinds suppress auras inside `jamR`.
  - A per-building upkeep of 0.5 IC/s, in the Logistics Hub's discount pool, if the enemy answer proves too weak.

**2. Mass-Driver adjacency, x5.8.** It is invisible in the manual, uninterceptable, `ceil: 0` and range 1,500. Per IC it is 0.52 dps against the Lance's 0.21: **2.5x**.
- **Answer: an enemy answer.** Packed clusters become priority busts, which is the existing `spaceReach` counter extended. Fix the O(n^2) scan.

**3. Superconductor cost stacking** (`costElec` .3 per facility, `unitCost` floor .1, L3835): three facilities and nine factories take a Lance to 8 IC. IC is not binding (212k idle); manpower and the cap are. **No action.**

## 4. Struck self-limits

1. **ENERGY-2's 35% throttle floor.** It was struck correctly as a throttle. But its replacement, "IC debt", has no stated bite, and a cost with no bite is a free toy.
   - **Restore as a real cost, not a throttle:** debt freezes new construction and research draw. It does not freeze fire.
2. **Psi's damage slump (ruling 6).** My own bloc struck it and replaced it with heat.
   - The replacement is hollow. Only the leviathan, pelter and bulwark (`plasarc`) and `lasersat` emit heat in the enemy roster, so Shapeshifters are rarely heated.
   - The President nerfed psionics because "you literally could not lose with them". Either ship an enemy thermal wing in the same bill, or restore the slump.
3. **The Recoil Cradle's +30% section-HP penalty. Do NOT restore it.** It was never the brake. The real brakes are slew, the 20 s cycle, upkeep and interdiction.
4. **RAILGUN-1's bore wear.** "Never below baseline" gave it no teeth. Restore it as an auto-billed IC reline, not a lockout.

## 5. Questions for Question Time (Round Five)

1. **Energy:** a single Solaris holds any boss at heat 100. What stops a player buying exactly one per boss and returning to monoculture? Will you accept *cold plate* (kinetic x0.5 at heat 0 on boss kinds, x1.25 at heat 100)?
2. **Physics, Energy:** `hurt()` is bypassed by `splashDamage` and `areaDamage`. Do the Falcon's missiles (L9149) get the heat bonus? If yes, name the Falcon doctrine's enemy answer. If no, say so in the text.
3. **Railgun:** overdrive reaches the Mass-Driver Railway, whose adjacency is about x5.8 (L8448). Will you exclude massrail from the Bank, or defend 7,365 per hit?
4. **Railgun, Kinetic-Arms:** what exactly does IC debt block? If nothing, why is it a cost?
5. **Kinetic-Arms:** the per-group selector violates Amendment II. Do you accept an AUTO default, and routing splash through the x2.47 hit buff, given airburst is a 6.3-target break-even?
6. **Railgun, Megaprojects:** `manual: true` is read by no code. Who picks the Long Gun's target, and will you strike the +4%-per-linked-unit volley bonus?
