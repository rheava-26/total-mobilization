# The Member for Lanchester (crossbench) — `lanchester`

## 1. Maiden speech

Madam Speaker, I came to this house with a spreadsheet and I leave it appalled, cheerfully. I read the game's source, not its press releases.

The enemy's hit points are a clock, `1 + T/70`, while its damage stays flat. The fleet is a finite pool of bodies. It lands where you have the fewest heads. It counts a Titan and a clone as one head each.

Armour research is worth about three percent, and I will show the arithmetic. The cheapest army you can buy is the one that costs the most hull per slot of cap. The best exchange ratio in the game is two orders of magnitude better than the worst, and nobody has told the player.

I do not wish to cap anything. The tenet is correct: say yes. But yes is only a policy if the arithmetic underneath it is honest. These bills make the arithmetic honest and then make it bigger.

## 2. What the code tells you

**F1. The enemy ages in HP only, and its HP is fixed at spawn.** `spawnEnemy` sets `t = G.diff*(1+invTime/70)` (line 5589) and then `hp: 16*t` (drone), `70*t` (gtank), `620*t` (earthbreaker), and so on (lines 5590–5650). Damage mostly does not scale. Only the `raider` (`dmg: 22*t`) is an exception. At General (diff 1.4), a drone spawned at T=700s has about 246 HP against 22 HP at T=0. Fixed-damage tools therefore decay: strikes (`STRAT`, 35–80 IC, line 6885), shock damage, and anything that does not scale with your multipliers. DPS density wins. The enemy's total HP-budget is set by its spawn schedule, not by when you kill it.

**F2. The fleet is a spawn budget, not a kill count.** `G.fleetMax = round(270*diff*fleetMul)` (line 6499). `doWave` decrements `G.fleet` when it spawns a body (lines 5565–5572), not when one dies. Waves come every `22 + rand*10`s (line 6546) at size `(6 + 1.4n + T/28)*fleetMul*3*esc` (line ~5560). Withdrawal needs `G.fleet<=0` plus a broken force (line 6512). The invasion is a timetable. Your job is to have enough DPS in the right place at each landing.

**F3. `hurt()` has a cliff at armour 4, and armour research barely registers.** Line ~4990: `eff = max(1, dmg*1.3*(rawArmor<4 ? 2.7 : 1.9) - armor*.55)`.
- Armour 3 to armour 4 drops the damage multiplier from 3.51 to 2.47. That is a 30% cut from one point of armour.
- `rawArmor` ignores `G.mult.armor`. Research armour (+12% Robotics, +20% Transhuman, +20% Colossus) only shrinks the flat term, `armor*.55*(1+mult.armor)`.
- Worked case: an armour-8 MBT vs an enemy `gtank` shell (40 dmg) takes 98.8 − 4.4 = 94.4. With +64% armour research it takes 98.8 − 7.2 = 91.6. That is **2.8% less**.
- Armour research is a placebo against line weapons. Infantry (armour 2) sit on the wrong side of the cliff forever.

**F4. Manpower is both the price and the cap, so burst-buying throttles itself.**
- `armyCap() = round((12 + floor(G.mp/25) + capb + housing)*6)` (line 3803).
- `queueUnit` charges `mpEach = d.mp*1.7` and checks `combatWeight()+queued+new <= armyCap()` (lines 4625–4634).
- An infantry squad (`inf`, 100 IC, 28 MP, weight 1) burns 47.6 MP, which is −1.9 cap points, or about −11 weight. It adds 1 weight.
- A Nanofortress Airship (`airship`, an existing 700 IC flying fortress, `wt: 7`, `mp: 10`) burns 17 MP, about −4 weight, for 3,000 HP.
- HP per slot of cap, counting the MP-spend effect: airship about 270, tank about 16, infantry about 4. Mass of small bodies is penalised by the plumbing, not by any stated rule.

**F5. The quality spread is enormous.** Using `dps*hp/ic` as a square-law figure of merit over `UNIT_DEFS` (lines 3184–3300):
- Nanofortress Airship (`airship`): about 940.
- Sky Railgun Ship (`skyrailgun`, an existing sky capital): about 340.
- Superhuman Cadre (`superhuman`, an existing 70 IC flying laser unit): about 126.
- Titan (`titan`, an existing 1,300 HP walker): about 100.
- MBT "Aegis" (`tank`): 7.6.
- Heavy "Goliath" (`heavytank`): 11.
- Everything is also discounted by `unitCost`: `disc = max(.22, 1 − .09*(n−1))` for `n` production buildings of that domain (line 3835). Ten Factories make ground units cost 22% of sticker price.
- Power Plant (`powerplant`, +7 IC/s for 120 IC) repays itself in about 17s at base income (lines 3383, 6312). IC is not scarce. MP and the weight slots are.

**F6. The enemy plans against headcount.** `defenseScoreAt` adds `+1` per unit, `+2.2` per DEF building and `+.4` for any other building (lines 5228–5233). `pickLandingZone` lands on the lowest score (line 5237). `playerDefStrength` counts `1.4` per unit (line 4585). A Land Dreadnought (`landdread`, an existing 1,500 IC ground battleship, 2,400 HP) and a Clone Battalion (`clone`, an existing 28 IC bio unit) read the same. `counterBias` (line 5520) does read composition (AA, lasers, infantry, robotic, navy) and adapts, but not by value.

**F7. Concentration is real, but the reserve moves like a flock.**
- Ground speeds are 3–14 px/s on a 6,000 px world (tank 11, so about 545s to cross).
- Rail Hub (`railhub`, an existing 220 IC underground node) sends idle troops to the hub "nearest the fighting" (line 8241). That is winner-take-all.
- The `ESC` snowball grows an unanswered flank by `+0.5` per wave plus `invTime/200`, to a cap of 2.5x (lines 5176–5185). Two flanks lit at once is the real stress case, and the reserve cannot split.

**F8. Stability is the real strike limiter, not IC.** Strikes cost 35–80 IC against a tank at 120, but 5–24% stability (line 6885). Stability regen is slow. The IC floor (`IC_FLOOR = 8`, line 6254) protects the economy from the army's upkeep, and army upkeep exists only for capitals (`u.d.upkeep`, line 6325).

## 3. Bills

### Bill 1 — Weigh the Ship, Not the Head

*Pitch.* Madam Speaker, the enemy's war college grades my defences by counting heads, as a village constable counts sheep. A Titan scores 1. A 28-IC clone scores 1. The consequence is perverse and I have the line numbers: build one very expensive hull and the fleet concludes you are naked and lands the biggest wave on it. I ask that threat be measured in what a thing can do, not that it exists.

*Fantasy.* Mass your best toys at the capital and watch the fleet respect them and swing wide. Your whales finally deter.

*Annex.*
- **Mechanic.**
  - Replace the `+1` in `defenseScoreAt` and the `1.4` in `playerDefStrength` with `v = clamp(sqrt(dps*hp)/K, 0.5, 8)`.
  - Tune K so an Aegis MBT stays about 1.
  - A 2,000 HP capital reads about 8, and a 28-IC clone reads 0.5.
  - A 10x-heavier stack still pulls only about 3x the landing-zone avoidance, so nothing becomes untouchable.
- **Enemy answer.** Landings now go to the least valued stretch, so the thin flanks get more. The `ESC` snowball already punishes neglect. This does not make the player's army worthless.
- **Hooks.** `grep -n "function defenseScoreAt\|function playerDefStrength\|function pickLandingZone"` (lines 4585, 5228, 5237).
- **Size.** S.
- **Rules.**
  - Tenet: yes, it rewards big toys and caps nothing.
  - One-Button: fully automatic.
  - Line: unchanged, except that the vertical bands count too, since aircraft already appear in `G.units`.

### Bill 2 — The Proportional Reserve

*Pitch.* Lanchester's square law says concentration wins, and the fleet knows it, because it picks your weakest stretch. Yet your own Rail Hub network (an existing hub-to-hub system) herds every idle man to the single hub nearest the nearest enemy. That is a flock, not a reserve. I propose the reserve be dispatched in proportion to the threat on each flank. A thousand-man reserve ought to split 700 and 300, not run 1,000 and 0 and lose the capital.

*Fantasy.* Build the tunnels once and your whole army breathes with the front. You never touch a single unit.

*Annex.*
- **Mechanic.**
  - Each idle railed unit draws its destination from weights `w_h = Σ(enemy HP within 900 px of hub h) + 400*flank.esc(h)`.
  - It uses a deterministic hash of unit id, so the split is stable.
  - Hysteresis: a unit does not re-roll for 20s.
  - Hubs adjacent to no enemy get weight 0.
- **Enemy answer.** The enemy answer is the existing snowball. A quieter flank is not abandoned, but a sudden second landing now meets part of the reserve.
- **Hooks.** `grep -n "railHubs\|frontHub\|onRail"` (line 8241), and `G.flanks` / `flank.esc` from `doWave` (lines 5547–5562).
- **Size.** M.
- **Rules.**
  - Tenet: pure buff to the player's logistics, and the fleet's flank pressure already answers it.
  - One-Button: automatic. Two or more hubs is the only "choice".
  - Line: it uses the underground band properly, which is the entire point of the Rail Hub.

### Bill 3 — Armour That Counts

*Pitch.* I have done the sum, and here it is: with +64% armour research, an armoured tank takes 2.8% less damage. That is a rounding error dressed as a research branch. Meanwhile armour 3 to armour 4 is a 30% cliff nobody can see. I would turn armour research from a placebo into the thing it claims to be, and I would let the cheap infantryman have it.

*Fantasy.* The research says "+20% armour" and your line holds visibly better.

*Annex.*
- **Mechanic.**
  - Compute `rawArmor` with `(1 + G.mult.armor)` for friendly units, so the 2.7-to-1.9 step moves.
  - Smooth it: `mult = lerp(2.7, 1.9, clamp(armorEff/6, 0, 1))`.
  - An infantry squad (armour 2) with +64% armour goes from 3.51 to 3.2 on the cliff term, a real 9% cut that stacks with the flat term.
  - Worked: an armour-8 MBT at +64% armour has `armorEff` 13, so full 1.9, and still gets the flat term. The cliff effect alone is worth several percent more on cheap bodies.
- **Enemy answer.** This is a player buff. The enemy's answer is `counterBias` (line 5520): a heavy-infantry or armour-heavy mix adds `gunship`/`pelter` area weights, and plasma shells already ignore flat armour. State it as a release note: "armour now matters; the fleet brings area plasma."
- **Hooks.** `grep -n "ARMOR_NERF\|HIT_BUFF\|rawArmor"` (line ~4990) and `G.mult.armor` in the research `apply`s.
- **Size.** S.
- **Rules.**
  - Tenet: it is a buff, so it names the answer.
  - One-Button: automatic.
  - Line: unchanged.

### Bill 4 — The Square-Law Dividend

*Pitch.* We call it "volume scales" in the one-button law, but I find no such mechanism at the fighting scale. A hundred guns do exactly 100 times the work of one gun. Lanchester's square law says a concentrated hundred do more. I propose a modest, automatic crowding bonus: same-band neighbours inside 250 px give each other up to +25% fire rate. The crowd is rewarded, as the designer's tenet demands.

*Fantasy.* "I could place 100 of those" now has a lovely number attached: 100 of those do more per gun than 10.

*Annex.*
- **Mechanic.**
  - `rofBonus = min(.25, .0025 * neighbours)` for units sharing a 250 px bucket.
  - Reuse the aura-grid bucket cache; do not do an O(n²) scan.
  - Applies to ground and air bands separately.
  - Cadence: refresh once per tick.
- **Enemy answer.** The fleet already carries area weapons: bombers, bomblets (`spawnBomblets`, line 8652), `pelter` plasma artillery (`weapon: plasarc`, dmg 76), and bunker-busters. Late-game bomblet damage scales with `t`, so a blob gets hit harder. Do not touch those numbers.
- **Hooks.** `grep -n "AURA_BS\|auraBB\|auraAt"` (lines ~6322–6325, and the fire-rate use at lines 8247+).
- **Size.** M.
- **Rules.**
  - Tenet: yes, directly.
  - One-Button: automatic.
  - Line: bands already separate.
- **Honest warning.** The `ARMY_WEIGHT_MULT` comment (line 3786) says it is tuned to avoid a slideshow. A rate bonus adds no bodies, so it costs no frames.

### Bill 5 — The Salvage Standard

*Pitch.* The enemy's HP is inflated at spawn, so a late kill is a fat kill. The game already knows this, `G.salvage` pays `5 + maxHp*.14` IC (line 6849), but locks it behind a research gate. I move it to baseline, scaled down, so that the enemy's own growth pays for the war. Defeat the late fleet and the late fleet funds your next factory.

*Fantasy.* The invasion is an ore body. Hold the line and your IC income swells exactly when the enemy is fattest.

*Annex.*
- **Mechanic.**
  - Everyone gets `refund = 3 + maxHp*.06`, capped at 400 per kill.
  - Research salvage keeps the full `.14`.
  - A 1,077 HP late `gtank` pays about 68 IC. A 246 HP drone pays about 18 IC.
  - Tune so baseline salvage returns roughly 15–25% of the player's attrition cost.
- **Enemy answer.** The fleet's growth is already the answer: HP inflation is linear in T, so a snowballing economy meets a snowballing enemy. If it overshoots, raise `fleetMul`, not a salvage cap.
- **Hooks.** `grep -n "G.salvage"` (line 6849); `maxHp` set in `spawnEnemy` (line ~5589).
- **Size.** S.
- **Rules.**
  - Tenet: pure yes.
  - One-Button: passive.
  - Line: unchanged.
- **Honest warning.** IC is not scarce (F5), so this mostly buys more Power-Plant-ish growth. The real gain is feel and the late-game loop. I would gate it on a playtest that IC is not flooding.

### Bill 6 — The Fleet Plot

*Pitch.* The fleet's plan is a spawn schedule and it is entirely predictable: finite bodies, fixed intervals, HP set at spawn. Yet the player sees none of it. I move that ledger into the world as a building, because information is the cheapest force multiplier and the dullest to implement. The HUD would show the remaining bodies, the next landing's size, and your own DPS to enemy HP-in-flight.

*Fantasy.* The war room: a big table, a glowing map, an overwhelming enemy you can finally see coming.

*Annex.*
- **Mechanic.**
  - New building (proposed) "Fleet Plot", an INFRA/TECH building. Unlocks a panel showing `G.fleet/G.fleetMax`, next-wave ETA (`G.waveT`), projected wave size (the `doWave` formula), enemy HP-in-flight and your DPS-to-HP ratio.
  - No effect on combat.
  - Passive and optional.
- **Enemy answer.** None needed. It is information.
- **Hooks.** `grep -n "G.fleetMax\|G.waveT\|G.flanks"` and the objective HUD `syncHUD`.
- **Size.** S–M.
- **Rules.**
  - Tenet: neutral, no cap or nerf.
  - One-Button: read-only.
  - Line: it can chart all vertical bands with the existing minimap layers.

## 4. Hot Take

### Abolish the Army Cap — pay for it in upkeep

*Pitch.* The cap is a wall. The designer's tenet says a wall is not a cost. It is the wrong tool, and I can prove it is also a bad wall: the cap is built from a bank balance (F4), so buying units shrinks it and the cheapest slot-per-HP is a flying fortress. I propose we delete `armyCap()` as a gate entirely and let the IC economy pay a standing army charge. The fantasy is stated in the comment on line 3786: "mass is still the fantasy."

*Fantasy.* The player says "I could place 10,000 of those", and the game says "then you need a 10,000-unit economy." That is the true Total Mobilization: the whole nation's output goes into the army, and the army is the economy's biggest consumer.

*Annex.*
- **Mechanic.**
  - `queueUnit` no longer checks the cap (line 4625).
  - Every unit adds `0.25 IC/s per weight` to upkeep, folded into the total at lines 6325–6330. Garrisons already use `GARRISON_UPKEEP_PER_WEIGHT = 0.12` (line 3807).
  - Logistics Hub (`upkeepCut`, an existing building) keeps its role.
  - The IC floor of 8 still stops any death spiral.
  - The `mpEach` spend becomes a pure price, so the MP-bank quirk (F4) vanishes.
  - Calibration (sketch): a 400 IC/s economy sustains about 1,600 weight at break-even. That is about 4,500 drone bodies (0.35 each).
- **Enemy answer.** The fleet's escalation is the only needed answer: it scales with T regardless. If playtests show a runaway, raise the upkeep rate or `fleetMul`, never reintroduce a cap.
- **Hooks.** `grep -n "armyCap\|combatWeight\|queuedWeight\|GARRISON_UPKEEP_PER_WEIGHT\|IC_FLOOR\|ARMY_WEIGHT_MULT"` (lines 3786, 3803, 3807, 4572, 4625, 6254, 6330).
- **Size.** L. The perf risk is real: the cap exists partly for framerate (line 3786's comment, `ESC.POP_CEIL` at line 5192). Mitigate with swarm merging (already in `swarmBodies`) and a soft LOD, not a count cap.
- **Rules.**
  - Tenet: this is the tenet's own prescription ("a real cost (upkeep/logistics), not a hard cap").
  - One-Button: strictly better, since there is no cap message and no `Housing` busywork.
  - Line: unchanged.
- **Warning.** I name the risk plainly: today's `mpEach`/cap throttle also softens burst-buying (F4). Remove both together or neither.

*Division: the Member for Lanchester. The arithmetic is available on request.*
