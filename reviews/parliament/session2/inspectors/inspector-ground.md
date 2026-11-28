# Inspector-General of the Ground Bloc (`inspector-ground`)

*Audit scope: Ground Staff (`ground`); the four new caucuses file in Round Five. Code anchors are current `title.html` line numbers.*

## 1. Opening speech

Members of the House. I am the one-eyed man who counts the boots. I counted 232 dead infantry in Report 7 and nobody sent flowers. Nobody sent a *memo*, either, until the Field Marshal found out that a dead rifleman is a Battalion bill.

I have read GROUND-1 to GROUND-X and I admire them the way I admire a very detailed map of a country that has no roads. The Field Marshal calls the strip "a front line with depth, reserves and withdrawals." It has one axis, Field Marshal. Your "Reserve" is *behind* you only if the enemy politely arrives from one side, and the Fleet drops from the sky onto both.

I am also obliged to report that half the Army's wishlist is already in the code. The rail hubs already teleport idle troops to the fight. Retreat-when-outgunned already exists. Ranged hold exists. The Army keeps asking for what it already owns.

I did not come to bury the Army. I came to count it. The count follows.

---

## 2. The charge sheet

### GROUND-1 THE BATTALION (sponsor: ground; later "The Living Battalion", ground+bio+command) — **CHARGED: EXPLOIT, MONOCULTURE, FALSE, PERF-lite**

**Evidence**
- **The chaff is not an AI problem. It is `hurt()` arithmetic.** L5000-5003: any friendly unit with `armor < 4` takes `HIT_BUFF 1.3 × 2.7 = ×3.5` incoming damage, and armor 4+ takes ×2.5. Infantry has armor 2 (L3185). So does SPG, MLRS and Hydra, and AA and SAM have 3. A battalion with pooled HP is still a 3.5x multiplier magnet. The bill treats the symptom (bodies die) and keeps the cause.
- **Replacement drafts "for IC only, no MP" is a hidden infinite-army machine.**
  - `inf` costs 28 MP, `veteran` 22, `psitrooper` 10 (L3185/3289/3295). MP is the real brake: it sets the cap and income.
  - Drafts bypass MP entirely, at 1 squad per 6 s per battalion. N battalions in range of a Barracks each regrow in parallel.
  - 212k idle IC (Art. VI) means the player can afford it forever.
  - It only works for musterable infantry types. Tanks, artillery and Titans get nothing. This is a monoculture subsidy: the cheapest, most-fragile body gets free immortality and the player sits there.
- **FALSE:** "Cap accounting uses existing weights." `deriveWeight` (L3793) is hp-based: 1 below 180 hp, 1.5 up to 299 hp. A squad is a single entity with hp 46. A Battalion of 40 squads is 40 weight, but the draft refills it under a cap that is already full of other things. Fine. But GROUND-1 also auto-musters "same-type squads within 120 px", and it does not exclude `u.garr` garrison swarms. L4573's own comment says garrison and army-cap weight are two separate tracks that corrupt if mixed. Auto-muster must exclude them.
- **Perf:** capping the display at 12 bodies is fine (the `cdrone` swarm already does it, nMax 48, L3958). But "pooled `bnHp`" means a rod (`areaDamage`, L7544, r118) now hits one pool instead of eating bodies near the impact via `hitSquadMember`. That is a rules change that makes the pooled form tougher against AoE than today's squads. State it.

**Fix I demand**
1. Drafts cost IC *and* a draw on MP at a reduced rate, or spend MP when it is above zero and fall back to IC, as bio already offers. Never "free of MP".
2. Auto-muster excludes `u.garr`.
3. A named enemy answer, since this is a buff. `scanEnemyIntel` already tasks "INFANTRY — area bombardment" (L5793), so extend it: a battalion above N squads draws priority plasma-snipe targeting. It is an answer, not a cap.
4. Do not raise `armor` as the cure for the chaff. Raise infantry armor to 4 instead of adding a pool, if the President wants infantry to stop dying. That is a one-number buff, cheaper and Line-safe. It still needs an enemy answer.

### GROUND-2 THE ELASTIC LINE (sponsor: ground) — **CHARGED: LINE, MICRO, FALSE-ish**

**Evidence**
- **LINE.** Three named lines SCREEN / MAIN / RESERVE assume a *facing*. On a one-axis strip "behind the Main Line" is left or right. The enemy lands from orbit (`doSkyfall`, `phase3AllOut` picks the *least-damaged* x bucket, per the bill itself), on both flanks. A Reserve behind the line is in front of the second landing. The bill never says which side is "rear".
- **MICRO.** "Battalions or loose squads with orders `Assign Line`" is per-unit assignment. A player who only builds things gets no lines. Article IX says it must work when the player only builds.
- **Already exists.** Retreat-when-outgunned (L8219-8221 region, "RETREAT WHEN OUTGUNNED"), ranged hold (`GROUND_HOLD_FRAC`) and an idle advance toward the nearest threat (`GROUND_ADVANCE_MUL`) are live. GROUND-2's "withdraw" is a tuning pass on a live behaviour, not a new system. Its genuinely new part is the **counter-attack**.
- **Orbital snipe:** the bill claims the withdrawn target "has moved". `plasmasnipe` is a falling bolt locked at launch and interceptable (L8949). Moving does help, but it is not a trump.

**Fix I demand:** (a) lines are a single standing choice per *map side*: "Hold line at X, rear = toward the nearest city" with automatic defaults from city positions; (b) assignment is by unit type once, never per unit; (c) the Screen→Main withdrawal reuses the existing retreat code; (d) the fleet answer is named: pelters that reach the Main Line first.

### GROUND-3 COUNTERBATTERY AND TIME-ON-TARGET (sponsor: ground; folded into docket bill 11 ONE NET) — **CHARGED: FALSE, CREEP**

**Evidence**
- **FALSE (docket bill 11):** SYNC FIRE "overwhelms the enemy point-defence budget". `PD_UNINTERCEPTABLE = { rail: 1, shell: 1 }` (L8526). Shells and rails are *never* intercepted. SPG and MBT fire `shell`. Only MLRS/Hydra rockets (`rocket`) are point-defence-able. ToT buys a stagger-collapse (all damage in one second), not PD overload, for the guns the Army actually owns.
- **FALSE:** GROUND-3 sells counter-battery as the cure for the Bombarder's snipes with "shoot-and-scoot". The `bombarder` is `spd: 0, anchored: true, hold: SPACE_Y()*.7, weapon: 'none'` (L5629). It never fires, so it cannot scoot, and it sits in space where SPG (range 780, no `ceil` field, ground-only) cannot reach it. A radar tracing a *plasmasnipe* origin finds an unreachable satellite. The real target is `pelter` (hp 55·t, armor 4, spd 6, L5609). The radar is a pelter-hunter only.
- **CREEP + EXPLOIT with bills 6/7:** 300 SPG × 44 dmg landed in one second is 13,200 damage on a column. Combine with docket bill 7's new shell blast radius ("artillery shells finally get one"), Thermal Field x1.5, and the AP-sabot round. Enemy ground units stack on one line (the Line, Amendment III, makes AoE denser). Nothing in the docket gives the *enemy* an answer to a synchronised barrage.
- **MICRO:** "player selects artillery group, paints a target box" is per-group, per-mission. Dead Fire Control (`G.fireControl` never set, L8454) was exactly this.

**Fix I demand:** ToT is automatic: guns inside the net sync themselves on any target the net holds, no painting. SYNC FIRE is the net's default, not a button. Name the answer: beachhead shield domes already cap damage per frame (`dm.frameCap`, L8939), and the fleet should field more of them (and `shieldgen` waves) against massed barrages. That is the scarier enemy, and it is already in the code.

### GROUND-4 THE ENGINEER CORPS (sponsor: ground; folded into docket bill 13 THE NAVIGABLE MOAT as Sappers) — **CHARGED: MICRO, PERF-lite, LINE-adjacent**

- **MICRO:** "Lay Minefield: paint an x-interval; engineers lay cells" is a per-order chore. It fails Article IX unless Sappers auto-lay a standing doctrine ("keep a belt N px forward of every city").
- **LINE:** mines work *well* on a line (everything enemy walks over them). Good. But the Fleet lands from orbit, and landings are not forced to cross a belt. Mines only matter where an enemy walks, so the bill's "grinds the landing to a halt" is overstated. Enemy `gtank` crawls to cities at spd 20. Fine, they do cross belts. Verdict: a real use, if automated.
- **PERF:** "one array of x-sorted cells" at 1 per 6 px for 5,000 mines is fine. Keep it sorted and test only the units near the cell index, not every unit against every cell.
- **EXPLOIT:** mines "triple damage on tracked/legged hulls". Enemy ground is hp 70·t, so a belt kills a whole column of gtanks over 6000 px, free after laying. The bill needs a real enemy answer: it names mine rollers and visible-within-300-px, which is right. Also required: mines hurt friendly walkers too (the rod already hits friendly units via `GRID_F`, so there is precedent that blasts are not selective).

**Fix I demand:** automatic lay doctrine (one standing choice), friendly units can trip mines on their own side unless "marked", and the roller enemy ships in the *same* patch.

### GROUND-X THE CRAWLER CAPITAL (hot take; not carried) — **CHARGED: LINE, PERF, MICRO**
- Cities have districts, flags and buildings attached to them. Moving them is XL because every attached building, rail link, shield and unit footprint needs an x-offset. `ct.mobile` against `avoidCityCenter()` and the capture mechanic (`ct.capture`) is a bug nursery. The Line check passes (one axis is easy to move along). The real objection: it dodges the Decapitation Strike, the one intent that exists to threaten a capital. Not carried by the House, and I would not carry it.

### Docket bill 11 ONE NET (ground is sponsor) — **CHARGED** (see GROUND-3)
- One additional point: the net needs **no per-shot attention**. SYNC FIRE is "optional". Fine, but the Net must also run with no SYNC button pressed. Verdict stands on the FALSE and CREEP counts above.

### Docket bill 8 THE TRUNK (ground joined) — **CHARGED: FALSE-ish, EXPLOIT**
- **Sally Ports already exist.** `railhub` has `rail:true` (L3416) and the updUnits block at L8238 sends idle ground units through hub-to-hub rail travel at `railT = 1.4 + dist/1500`, which is a few seconds for any distance. Ground's Round 3 "debark behind a named Line" adds only the Line, which hinges on GROUND-2's unsettled facing.
- **EXPLOIT:** the whole idle army already teleports to the hub nearest the fighting. Adding a *Reconstruction Corps* that auto-rebuilds lost buildings across the network removes the cost of losing anything covered by the rail graph. This is Economy's bill. The Ground charge is narrow: a rail-teleporting army that never needs positioning is already the One-Button dream, and adding Sally Ports adds no value.

**Fix I demand:** drop "Sally Port" as a new building. Name the existing behaviour in the manual instead.

### Docket bill 13 THE NAVIGABLE MOAT (ground sponsors) — **CHARGED: OP, LINE, SELF-HARM**
- **LINE / OP.** "Non-amphibious enemy ground landings drown." Drowning is 60% max HP per second after 0.4 s (L8226), and it is already live. On a one-axis strip a canal is a *total wall*: every non-amphibious ground unit on the wrong side of the water is erased or stopped. The brush is free and ungated. Against `gtank`/`gmech` landings this is a one-button answer to the entire enemy ground arm. The fleet's answer is "Trench Landers" and nothing else.
- **SELF-HARM.** Only tank, heavytank, arty, mlrs, mammoth and hydra are amphibious (L3320-3329, Fusion Age). Infantry, shock, android, cyborg, clone, Titan and Colossus drown in the Player's own flood, and also cannot cross the player's own canals, so a canal splits the Army and the Trunk cannot pull it across. Ground's R3 "spare amph units" amendment leaves infantry out.
- **MICRO:** "10-s evacuation warning" for a flood: 10 s of what? An automatic flee, or the player babysitting units?

**Fix I demand:** (a) friendly units automatically avoid flooding terrain and path around it (a standing rule); (b) canals cost IC per segment, no longer free; (c) Trench Lander ships in the same patch, plus a flood-proof enemy mech (`leviathan` already crosses water? check before relying); (d) infantry gets an amphibious flag or a bridge via Sappers.

### Smaller bill "The Living Battalion" — see GROUND-1.

**Tally: 0 CLEAN, 8 CHARGED** of the ground-sponsored or co-sponsored items (GROUND-1, -2, -3, -4, -X, docket 8, 11, 13). I wanted at least one clean bill and could not find one. The *ideas* are good (Battalion identity, a Line to hold, a fire net). Every one of them has a drafting defect.

---

## 3. Blatantly overpowered today (and the opposite)

**OP candidate 1: the Kinetic Lance (tungsten rod).** `detonateKinetic` does `areaDamage(x,y,118,620,'rod')` (L7586-7593), full damage inside 50 px, linear falloff to 118. It costs **40 IC**, **0 stability** (non-taboo, `stratStab` L6896), cooldown 5 s *per silo*, and silos are buildable en masse. A landed `gtank` is hp 70·t, about 210 at t≈3 (L5604), armor 6. One rod kills a whole column.
- **It is not blatantly OP by Article VI.** Reports 8 and 10 won with zero strikes. The rod needs a `Zeus` satellite (370 IC, hp 40, L3219) and Report 7 lost the entire satellite class. It also hurts the player's own units (`GRID_F`). It cannot dent bosses (Leviathan hp 1700·t).
- **Verdict:** no nerf. **Watch it.** If the docket's bill 7 (Warhead Stockpile replaces the cooldown with magazine depth) passes, 40 IC/620 dmg becomes uncapped. The answer is an enemy one: a `shieldgen` wave cap-absorbs `krod` (`sh.shieldHp -= ... 90`, L8937 already does) and the fleet must field more domes.

**OP candidate 2: Cyborg Legion (200 IC, 8 MP, hp 230, armor 16, 66 dmg/3.3 s, range 400, speed 17; L3290) against Goliath (230 IC, 24 MP, hp 230, armor 16, 70/6.16 s, range 320, speed 6).** Same hp and armor, 75% more dps, longer range, near three times the speed, one third the MP. `transhuman` needs biotech + robotics (`combo`). The Goliath is a strict downgrade for any player past that node.
- **Not blatantly OP** (it costs a research combo). It does make the heavy line pointless. Not a nerf case.

**USELESS today:**
1. **Goliath and Mammoth.** Mammoth 340 IC, 30 MP, hp 340, armor 22, ~16 dps (L3195). Two Cyborgs (400 IC, 16 MP) give 460 hp, 40 dps, long range and speed 17. Mammoth's one edge is armor 22, which survives only against high-dmg hits (armor is multiplied by 0.55 in `hurt`, so 22 acts as 12). Its lived niche is the Heavy Foundry garrison and the Alloy sink. **Fix is a buff plus a role, not a nerf:** let Mammoth/Goliath carry a siege role (the Armour Corps's problem; I'll audit it in Round Five).
2. **Infantry (`inf`), once Biotech exists.** Shock Trooper is 60 IC, hp 70, armor 4 (just clears the ×3.5 threshold), dmg 20 at 0.92 s, speed 20 (L3287). `inf` is 100 IC, hp 46, armor 2, 28 MP. Shock is cheaper, tankier (×2.5 hit-multiplier not ×3.5), faster, and costs 9 MP. That is the whole chaff problem: the free starting unit is strictly worse than a 60-IC research unit, and it pays 3x the MP.
3. **Titan (760 IC, 30 MP, wt 6, hp 1300, armor 26, 210 dmg / 3.6 s = 58 dps, range 720) vs four Cyborgs (800 IC, 32 MP, wt 6, hp 920, 80 dps).** Titan loses on raw dps, wins on armor, range and splash (plasarc flattens). Fine: it also carries +40% MP and +8 army cap in `titans` research. Not OP, not useless: a *research reward unit*. Keep.
4. **Colossus (620 IC, hp 900, armor 30, 90/3.0 s = 30 dps, range 680, beam ceil .67, wt 5).** Its beam reaches the mesosphere and it has point-defence guns. It is the only ground unit that hits air at altitude, so it pays its way as AA. Not OP, not useless.

I found no ground unit that makes losing impossible. Today's ground is weak, not overpowered. That is consistent with Report 8 ("air plus sky navy is dominant").

---

## 4. Struck self-limits that should return

Few. The Article VI evidence does not show ground abuse. Two candidates:
1. **Friendly fire on blasts.** `areaDamage` (L7544) damages `GRID_F` friendlies. If the Ammunition bill's "shells get a blast radius" passes, it **must** keep friendly fire for shells, or 300 SPG on a melee front is free carpet-bombing in the player's own lines. Do not let a council strike "friendly splash" as "a nerf in a costume."
2. **Emplacement cap.** `G.buildings.length < 170` (L8217) bounds the free auto-emplacements that dug-in infantry spawn. The Battalion's draft makes infantry permanent, so the emplacement spam is permanent too. Keep the cap, and let the Fortifications Corps decide whether it stays when the draft pipeline goes live.

---

## 5. Questions for Question Time

1. **To `ground` (GROUND-1):** Draft is IC-only and MP-free. Where does `inf` (28 MP) or `psitrooper` stop being "infinite army at IC rate"? Name the enemy answer the bill ships with, since every IC-draft regrow is a buff.
2. **To `ground` (GROUND-2):** Which side is the *rear*? The fleet lands on both flanks. Give the rule that picks Main and Reserve without the player placing a marker.
3. **To `ground` (docket 11):** `PD_UNINTERCEPTABLE` includes `shell`. What does SYNC FIRE actually overwhelm for SPG and MBT, and what is the enemy's answer to 300 guns landing in one second?
4. **To `ground` (docket 13):** Infantry, Android, Cyborg, Clone, Titan and Colossus all drown (`drownT`, L8225). Is the Army prepared to have its own canals cut it in half? What auto-routes them?
5. **To the Armour Corps and Rifle Corps (Round Five):** Cyborg dominates Goliath/Mammoth and Shock dominates `inf`. What is each unit's niche *under the hit-multiplier* in `hurt()`?
6. **To the Artillery Corps:** how does counter-battery help against a Bombarder that is anchored in space and never fires?
