# Inspector-General of the Ground Bloc: Second Audit (`inspector-ground`, Round Five)

## 1. Opening

Five papers, four caucuses, one whip, and not one of them has counted the Leviathan's speed. I have. The Army wants me to be pleased that it withdrew two bills and rewrote a third; I am mildly pleased, which at my age is a medical event. The rest is arithmetic. Several papers are lovely and clean, and I say so. Several contain a tank that cannot reach the battle in the time the bill gives it. Those I have marked in red pencil.

## 2. The second charge sheet (25 entries: 10 CLEAN, 15 CHARGED)

### Ground Staff
- **GROUND-1 Battalion (amended): CHARGED (EXPLOIT, MONOCULTURE).** "Fall Back" replaces Shaken, which is fine. But the draft still refills "at IC", so MP is still bypassed (inf 28 MP, L3185), there is still no garrison exclusion (L4573 keeps garrison weight on a separate track), and no enemy answer is named for battalions. *Fix:* draft draws MP when MP > 0, excludes `u.garr`, and the answer is stated (`scanEnemyIntel` L5793 already tasks "INFANTRY").
- **GROUND-2 Main Line (amended): CHARGED (LINE).** The marker chore is gone, and the Forward/Balanced/Deep dial is a fair one-time choice. But "ground units *beyond* the Main Line fall back behind it" still has no rear. Landings come on both flanks. *Fix:* define the rear as "toward the nearest city", per side.
- **GROUND-X Crawler Capital (amended): CLEAN.** Automatic, native to the line, the enemy answer is named, not carried. Size XL is the Speaker's call.
- **GROUND-5 Kill Zone: CHARGED (CREEP).** Fully automatic and works on the Line, with the tag on the target. The trouble is the stack (section 4). A wall (25 IC) or a mine counts as the STOP arm, so ×1.45 is nearly free. The four "enemy answers" are weight tweaks to existing code, not new threats. *Fix:* the Autofleet must ship a named ground escalation, not a weight adjustment.
- **GROUND-6 Opening: CHARGED (FALSE, useless).** The example puts "a Mammoth with 6 swarms on it" as the target, but that is the player's own unit. The enemy case: armor 22 × 0.55 = 12.1 (L4984), minus 48% leaves 6.3, about 5 damage on a ~118-damage hit. That is a 5% buff. *Fix:* make the strip a flat 25% armor, or drop the bill.

### Armour Corps
- **ARMOUR-1 Hammer: CHARGED (FALSE).** The reserve gets +60% speed for **30 s**, dispatched from "within 2,400 px". An MBT at speed 11 (L3186) covers 11 × 1.6 × 30 = 528 px. A Goliath covers 288, a Mammoth 240. The armour never arrives inside its own window. *Fix:* radius 500 px, or the window follows the unit until contact.
- **ARMOUR-2 Anvil: CHARGED (CREEP).** The Ablative Carapace (cold hull ×0.55 on all kinetic) is an enemy buff on the Leviathan and "heavy tier". It ships before heat exists in the player's hands, so tanks, rails and rods lose 45% against the one enemy they need to beat. This is exactly Amendment I's "enemy buff must not make the existing army worthless". The paper itself asks me to check that Falcons are not gated, and I cannot until it says `kinetic` means which `killBy`. *Fix:* the Carapace appears only if the player has researched `thermalload`, and only against a kinetic-dominant `scanEnemyIntel` read.
- **ARMOUR-3 Siege Breacher: CLEAN.** Auto-priority, S size, and it gives the Mammoth the role I said it lacked (Cyborg beats it otherwise). Enemy domes are only `fieldHp (360+…)×diff` (L5642), so ×3 is ample, and the `frameCap` guard holds. The enemy answer (dome ×1.5 after minute 15) is named.
- **ARMOUR-4 Recovery Yard: CHARGED (EXPLOIT, duplicate).** 40% IC returns as a new unit with no MP stated, so Mammoths (30 MP) resurrect MP-free. It also duplicates FORT-4's Yard, which the whip ruled "one building" while both papers still file separate ones. *Fix:* one Yard, and the revived unit costs its normal MP.
- **Hot take Reconsolidation: CHARGED (useless).** 12 Mammoths cost 4,080 IC and 360 MP and fuse into a `landdread` that a Grand Arsenal sells outright for 1,500 IC (L3282). The only gain is a weight-laundering trick, 24 weight down to a capital's cap of 6. *Fix:* refund the difference, or drop it.

### Rifle Corps
- **RIFLE-1 Cellar: CLEAN.** Automatic, volume scales (more hubs, more holes). Open question: what happens to housed infantry when the hub dies.
- **RIFLE-2 Garrison: CLEAN.** Capture only rises when `fg === 0` (L6590), so any friendly ground unit already stops a capture. Part (b) only speeds retakes. Harmless, but modest.
- **RIFLE-3 Suppression: CLEAN.** No enemy buff, thousands of rifles become a role, a per-enemy field is cheap. The +25% from artillery, rail and bombs belongs in my stack count.
- **RIFLE-4 Mimic: CHARGED (TENET, FALSE).** "Reach: useless vs air" is a new denial. The Shapeshifter's `plasma` weapon already has ceil .62 (L4494), so it hits aircraft up to the mesosphere today. And the Disassembler is a nerf: 0.3% max HP per second on a boss. A Leviathan at t≈3 (hp ~5,100) gives ~15 HP/s per shifter, against ~32 DPS from the swarm's 75 dmg after armor. The only form that qualifies for `armor ≥ 12` is worse than shooting. `gtank` has armor 6, so the President's "tank" case never triggers it. *Fix:* swarm keeps its air reach, Disassembler is ≥1.2% on bosses, and the latch bonus must beat the gun.
- **RIFLE-X Levée: CLEAN.** Real costs (MP income suspended, stability), and civilians off the cap by Art. IV.1.

### Artillery Corps
- **ARTILLERY-1 High Explosive: CLEAN.** `splashDamage(…, true)` hits the enemy grid only (L7556), so there is no friendly fire. My demand for friendly shell fire is waived: it is not the only thing stopping an exploit. The perf cap of one splash per ripple is sensible.
- **ARTILLERY-2 Registered Targets: CLEAN.** Automatic, uses the existing 50% re-entry window, and the fleet already lands where the least damage was taken.
- **ARTILLERY-3 Counter-Battery: CLEAN.** It now targets Pelters only (hp 55·t, spd 6) and says it cannot touch space. My earlier FALSE is cured.
- **ARTILLERY-4 Siege Round: CHARGED (FALSE).** The headline target ("Leviathan crawls up") is excluded by its own gate (`spd ≤ 8` or `anchored`). Leviathan has spd 11 and is not anchored (L5627). Qualifiers: Earthbreaker spd 4, Pelter spd 6, the Breacher dome. *Fix:* gate on `e.boss && e.ground`, or raise the threshold to 12.
- **Hot take King's Thunder: CHARGED (duplicate, EXPLOIT).** An automatic Fires Brigade volley every 20 s duplicates Ground's ONE NET SYNC FIRE. 100 SPG at 44 dmg is 4,400 on a cluster core, and the bill admits "super-linear". Shells are never intercepted (`PD_UNINTERCEPTABLE`, L8526). *Fix:* a single TOT engine with one owner, and a named brigade-hunter enemy (the bill's "Pelters target it first" is already true of any battery).

### Fortifications Corps
- **FORT-1 Curtain Wall: CHARGED (LINE, PERF, EXPLOIT).** It still says the Gatehouse "closes a flank", which the whip ordered rewritten. It is 600 HP for 25 IC, the cheapest HP in the game (shieldgen 900 for 320). A gtank deals about (40−7.7)/4.84 = 6.7 DPS, so one wall holds one tank for ~90 s. One hundred walls (2,500 IC) halt all ground landings, and ground capture is the only way to take a city. Walls are buildings: thousands of them add to every `G.buildings` scan, and the free-emplacement cap `G.buildings.length < 170` (L8217) stops working. *Fix:* walls as density cells like mines, a Breacher charge that ships in the same patch, and a wave-scaled enemy.
- **FORT-2 Sapper Works: CLEAN.** A density value per cell, no per-order click, answer named (sweeper), 3 IC/min. One question: does a halted enemy re-trigger a mine each tick?
- **FORT-3 Dome Lattice: CHARGED (FALSE, CREEP).** "Merely jealous" is wrong. `shieldBlocks` returns the first live dome (L8903), so N overlapping domes already absorb N × 900 in sequence. The only real change is the **regen**: pooled it becomes N × 28/s. Twenty domes (6,400 IC) is an 18,000-HP pool with 560 HP/s regen. *Fix:* the Disruptor ships with the bill and is weighted by `scanEnemyIntel` the moment ≥ N domes overlap.
- **FORT-4 Engineer's Yard: CHARGED (duplicate, FALSE).** Duplicates ARMOUR-4 and the Trunk's Reconstruction Corps. Its +25% heat bonus for Bunkers is a third heat constant next to the Thermal Field's ×1.5. *Fix:* one Yard, and Bunkers read the Thermal Field's `heat`.
- **Hot take Star Fort: CHARGED (LINE, CREEP).** "A ring of 4 walls" on a line is two. "The fleet's overall landing tempo slows" while a siege line is built is a lever on enemy escalation that the player owns, with no counter. *Fix:* drop the tempo effect, or make it cost Fear (The Nerve).

## 3. Old charges

1. G-1 MP bypass: **NOT SATISFIED.** Still "IC".
2. G-1 garrison exclusion: **NOT SATISFIED.** Not mentioned.
3. G-1 named enemy answer: **NOT SATISFIED.**
4. G-1 pooled-HP vs AoE note: **NOT SATISFIED.** Silent.
5. G-2 MICRO markers: **SATISFIED.** Removed.
6. G-2 LINE facing: **NOT SATISFIED.** "Behind" is still undefined.
7. G-2 duplicates retreat code: **NOT SATISFIED.** Silent.
8. G-3 SYNC FIRE vs PD: **NOT SATISFIED.** Shells are still uninterceptable (L8526).
9. G-3 counter-battery vs Bombarder: **SATISFIED.** ARTILLERY-3 is Pelter-only.
10. G-3 CREEP (no answer to synced barrage): **SATISFIED.** `shieldgen` and `colMul` named.
11. G-3 MICRO painting: **SATISFIED.** Auto-timed.
12. G-4 MICRO: **SATISFIED.** Withdrawn into FORT-2.
13. G-4 mine enemy answer: **SATISFIED.** Sweeper plus Earthbreaker.
14. G-X MICRO/PERF: **SATISFIED.** Auto-relocate, and not carried.
15. Trunk Sally Port (already exists, L8238): **NOT SATISFIED.** The whip is silent.
16. Moat free brush: **NOT SATISFIED.**
17. Moat drowns infantry, Titan and Colossus (drown at 60% max HP/s, L8226): **NOT SATISFIED.** Still only "amph units cross".
18. Struck self-limit, friendly shell fire: **SATISFIED** by waiver (above).
19. Emplacement cap 170: **NOT SATISFIED.** And FORT-1 breaks it.

**7 satisfied, 12 not.**

## 4. Cross-caucus check

**Duplicates.** (a) Two Yards plus the Trunk's Reconstruction Corps: three auto-rebuilders. (b) SYNC FIRE and the Fires Brigade: two TOT engines. (c) FORT-4's heat (+25%), ARMOUR-2's ×1.5 and the Thermal Field's ×1.5 are three heat constants. (d) RIFLE-1's cellar and the Trunk's Rail Hub: both want a hub as shelter, a sally port and a rail node.

**Gaps.** Nobody owns the **air threat to the ground** (the infantry-chaff answer is shelter only), and nobody owns an *enemy* ground escalation. Every bill buffs the player on the Line, and the enemy's ground arm is gtank 70·t, pelter 55·t and the Leviathan. Ground was never what won or lost a match (Report 8, Report 10).

**Stack.** Against a pinned, heated, walled enemy ground column, with all four arms touching it: Kill Zone ×1.45 × RIFLE-3 +25% × ARMOUR-2 ×1.5 × sabot ×1.3 = **×3.5**. Add Siege Round's +40% on slow targets and it is ×4.9. Mines ignore armor. A wall halts the column while HE splash (60%) hits all of it. This is OP *only on the enemy's ground arm*, but that arm is the one that captures cities. A time-scaled enemy (`70·t` hp) does not absorb ×3.5. The answer has to be a new enemy, not a weight tweak.

## 5. Questions for Question Time

1. **Armour Corps:** ARMOUR-1's window is 30 s and the radius is 2,400 px. A Mammoth at speed 5 covers 240 px. How does the hammer reach the landing?
2. **Rifle Corps:** the Disassembler does about 15 HP/s per shifter on a boss, against about 32 DPS from the swarm. Which player picks it? And why does RIFLE-4 strip air reach that `plasma` has today (L4494)?
3. **Artillery Corps:** Siege Round gates on `spd ≤ 8` and the Leviathan is spd 11. Which enemy is the bill for?
4. **Fortifications Corps:** FORT-3 says domes do not stack, but `shieldBlocks` already returns the first live dome, so 20 domes are already 18,000 HP in sequence. What do you change, and what is the Disruptor's spawn rule?
5. **Fortifications and Armour jointly:** one Yard or two? And is a wall a building or a cell?
6. **Autofleet:** which new ground enemy answers ×3.5 on the line, and when does it first appear?
