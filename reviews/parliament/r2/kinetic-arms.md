# KINETIC ARMS COUNCIL — Round 2 Paper
*Slug: `kinetic-arms`. Read: GROUND-3, INDUSTRY-1, RAILGUN-1/2/3, ENERGY-1, and headings of all 23 papers.*

## 1. Floor Speech

Madam Speaker, the Chief of Ordnance finds himself, for the first time in this Parliament, among friends, and he distrusts it.

Three councils have independently reinvented my bills. Ground wants time-on-target and counter-battery; I proposed it. Railgun wants airburst, sabot and guided rounds on a selector; I proposed that too, in the same words, down to the APFSDS. Industry wants shells to cost something and to bless the supplied; I said alloy per twenty rounds. Great minds, and one of them is mine.

So the Board does the unfashionable thing: it **merges**. A kinetic gun is a kinetic gun whether the charge is powder or lightning. One ammunition locker, one fire-direction net, one supply line. I withdraw what is duplicated and keep what is mine alone: the programmable fuze that bursts flak in the cloud, the spool of a rotary, the penetration model.

To the Energy Council: your thermometer is lovely. Heat the plate, and my sabot meets butter. Combined arms, at last. To the Railgun Council: your Lance is a fine, long, silent, expensive stick. Come and fire a shell with me. And to whoever will say airburst flak "nerfs fighters": it cannot. It bursts on the enemy swarm, and the enemy swarm is only as large as we make it.

Carried by the breech.

## 2. Positions

| Bill | Position | One line |
|---|---|---|
| GROUND-3 Counterbattery and Time-on-Target | SUPPORT | Same mechanic as KINETIC-4; theirs has the radar, I will fold mine in (Motion 2). |
| INDUSTRY-1 Ammunition Trains | AMEND | Support, but state shell stock as the *cost* for AP and airburst rounds (Motion 1), and keep "out of stock = base stats, never a penalty" in the text. |
| RAILGUN-3 Hypervelocity Munitions | AMEND | Do not give it its own node. Make the `def` node a shared **Smart Munitions** node serving every kinetic gun (Motion 1). Sabot "ignores 60% armour" must run through one `hurt()` pen parameter, not two. |
| RAILGUN-1 Pulsed Power | SUPPORT | Wear is a receipt, baseline is the floor; legal and gives the Lance gun feel. |
| RAILGUN-2 / MEGA-2 Long Gun | AMEND | Support one superheavy gun. KINETIC-X is withdrawn into it; add a sabot slot and the "hit interrupts the Orbital Annihilator charge" rule (Motion 3). |
| MEGA-H Barrel Is the World | OPPOSE (soft) | Same gun at continental cost; the Long Gun comes first. |
| ENERGY-1 Thermal Load | SUPPORT | Heated target takes more kinetic damage is the combined-arms seed; I add: sabot penetrates a heated plate further (armour term ×0.8 on `heat>50`). |
| PHYSICS-2 Thermal Loading | AMEND | Merge into ENERGY-1; one `t.heat` field, not two. |
| GAME-FEEL-2 Impact Budget | SUPPORT | Hits and recoil need trauma-weighted feedback; my spool/recoil (KINETIC-3) is the feeder. |
| MISSILES-5 Warhead Stockpile | AMEND | Magazine depth should share the `munitions` component with INDUSTRY-1; do not create a second ammo currency. |
| GROUND-1 Battalion | SUPPORT | Airburst and canister need squads, not single bodies, to matter. |
| SPACE-4 / SPACE-X Kessler | NO POSITION | Not kinetic ordnance; I defer. |

## 3. Ballot (10 votes)

| Bill | Votes |
|---|---|
| INDUSTRY-1 Ammunition Trains | 3 |
| GROUND-3 Counterbattery and Time-on-Target | 2 |
| RAILGUN-3 Hypervelocity Munitions | 2 |
| ENERGY-1 Thermal Load | 1 |
| MEGA-2 The Long Gun | 1 |
| GAME-FEEL-2 The Impact Budget | 1 |
| **Total** | **10** |

Rationale: INDUSTRY-1 is the best answer to the 212k-IC-unspent anti-pattern and is a real cost, not a cap. GROUND-3 and RAILGUN-3 give ground guns and the Lance a verb.

## 4. Motions to Co-sponsor

### Motion 1 — to the Railgun Council (and Energy): "THE ONE LOCKER" (shared Smart Munitions)
**Merges** KINETIC-1, KINETIC-2, RAILGUN-3.
- **Mechanic.** One research node **Smart Munitions** (`def`, pre `pointdefense` + `automation`, cost 150 base ×1.6, not `dang`). It adds a per-group round selector to every kinetic gun (tank, SPG, AA, CIWS, Lance, Battlecruiser): **HE-frag** (default, r 30 splash 50%), **AP-sabot** (armour term ×0.35, +40% dmg on rail), **Airburst** (fuse at target range, `splashDamage` r 46, 80% dmg), **Guided HVP** (rail only, 25°/s turn, ×0.8 dmg). Non-default rounds draw `munitions` (INDUSTRY-1) at 0.04×dmg/50 per shot; empty stock reverts to HE-frag at full stats. Hailstorm and Sentry airburst gain +30 IC at upgrade.
- **Code.** `fire()` L4855 (add `rnd` argument), rotary branch L4883, `hurt()` L4981-4985 (single `pen` param), `updProj` L8899 (burst point), `splashDamage` L7546, `UPGRADES` L3321, `BUILD_DEFS.ammodepot` L3452, `RESEARCH` L3502.
- **Build size.** M-L (~400 lines). **TM1.**
- **Tenet.** Additive choices, default unchanged, shortage costs IC not stats.

### Motion 2 — to the Ground and Industry Councils: "FIRE FOR EFFECT, SUPPLIED"
**Merges** KINETIC-4, GROUND-3, INDUSTRY-1.
- **Mechanic.** GROUND-3's `firefinder` radar and Time-on-Target box are the fire-direction layer (node `firecontrol_net`); my Fire Direction Centre becomes its linking building (900 px link, 8 s barrage cooldown). Shells finally get a blast (r 38, 60% dmg). A barrage that lands from supplied guns (INDUSTRY-1) fires at +35% damage. No stability cost, so no strike drain.
- **Code.** L8447 dead `fireControl` branch, `UNIT_DEFS.arty/mlrs/hydra` L3189-3194, `fire()` shell arc L4865 and gravity L9137 (delay solver), `splashDamage` L7546, `COMP` L3180.
- **Build size.** L (~550 lines). **TM1.**
- **Tenet.** Mass artillery is rewarded; supply is the cost, empty is baseline.

### Motion 3 — to the Megaprojects and Railgun Councils: "ONE LONG GUN"
**Merges** KINETIC-X into MEGA-2/RAILGUN-2.
- **Mechanic.** KINETIC-X is withdrawn. The Long Gun keeps its section build and gains: (a) the shared round selector (Motion 1) with a sabot slot, and (b) the Annihilator rule, a Long Gun hit interrupts the 6 s beam charge, flagged in `spawnSuper`. Upkeep stays an IC cost. If you want the powder-and-light-gas science (HARP, SHARP), it becomes the *flavour text of the section build*, not a second gun.
- **Code.** `BUILD_DEFS` L3381, `spawnSuper` L5269, `weaponCeil` L4488, `megaprojects-wip`.
- **Build size.** M on top of MEGA-2. **TM1** (needs the framework).
- **Tenet.** Pure toy plus a scarier target, no cap.

## 5. Trades

- **Withdraw KINETIC-X** entirely (into MEGA-2).
- **Withdraw KINETIC-4** into GROUND-3 (Motion 2); keep only the Fire Direction Centre link building.
- **Shrink KINETIC-2** to the penetration model and canister; the sabot and guided rounds ride in RAILGUN-3's shared node.
- **Keep** KINETIC-1 airburst fuze and KINETIC-3 spool; nobody else owns them. KINETIC-3 is an S bill and can ship alone.

## 6. Constitutional Challenges

None against a kinetic bill. I flag one in passing: **PACING-HOT** (the fleet arriving sooner as you build) is legal under Article III only if the enemy gets scarier or earlier, never if the player's buildings are throttled; I have not verified how it is written, so the Speaker should hold it to that test. RAILGUN-1 bore wear passes because the baseline is the stated floor.
