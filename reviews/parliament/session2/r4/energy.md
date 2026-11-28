# ENERGY — Second Reading (Round 4)

## 1. Speech

*(The coat is intact. The Institute is alarmed by this.)*

Mr Speaker, the President wants a wall made by the enemy, and the Institute has wanted to build one for weeks. We heard the Armour Corps' note and checked it. In `hurt()` (L4987-4992) a shell deals dmg x1.3 x 1.9 (armour 4 or more) before armour is subtracted, and armour is also scaled by 0.55. An armour-30 hull sheds 16 points from a 99-point hit. Our "50% armour cut" was worth about 8. It was a decoration. We strike it from every bill.

What replaces it has teeth. Heat becomes a damage-taken multiplier that grows with the target's mass. The enemy's heavy hulls adapt to whatever you keep feeding them. Spam one thing and the hull hardens against it, and you win slowly. Mix three things and it shreds.

We also confess the Furnace was a micromanagement button. It now fires itself.

## 2. The Re-reading

**ENERGY-1 / THE THERMAL FIELD (merged docket item 1) — AMEND.** Strike the armour cut (see the note above). Heat becomes a damage-taken multiplier, scaled by target mass, with deposit divided by thermal mass (Section 3, Bill A). *One-Button:* PASS. Deposit is automatic from any laser or plasma gun the player builds. *Line:* PASS. Beams pierce along the line, and ground cooling is flat -5/s.

**ENERGY-2 / THE POWER GRID (docket 15) — KEEP.** *Amendment I:* the shortfall is IC debt, never a throttle, so nothing is denied (`IC_FLOOR` L6254 already stops the economy hitting zero). *One-Button:* PASS with a fix. Capacitor Banks and Radiator Arrays are build-and-forget, and the pool balances itself. The player never routes power. *Line:* PASS, since it is global and not positional.

**ENERGY-3 / THE PLASMOID — KEEP.** *Power creep:* the enemy answer is that Heat-Sink carriers and shieldgen domes absorb the burst (shields soak first, so no heat accrues). *One-Button:* PASS. *Line:* PASS. The 70 px burst and the ground flame lie along the line, and a ground stack walking through them heats together.

**ENERGY-4 / THE REENTRY SHEATH — WITHDRAW.** Already merged into the Thermal Field (reentering pods spawn at heat 90, blackout from Physics). Standalone it adds nothing. Re-entry pods are light class, so the heat multiplier on them is small. Their value is the blackout window, which Physics owns.

**ENERGY-H / THE FURNACE — AMEND.** The first version needed an aimed click per use. It now **auto-fires** at the densest cluster of medium-or-heavier hostiles once charged, with a single standing toggle. The player may still aim it by hand, as with a strike. Its heat deposit is divided by thermal mass, so a big column needs a big field: a cost that scales with mirrors, and no cap. *Line:* PASS (a vertical column over the line). *One-Button:* PASS with the auto-fire. *Amendment I:* the heavy fleet response is the enemy's own (bombers target the field).

**DEAD SKY PROTOCOL (docket 6, co-sponsor) — KEEP.** The thermal burst channel feeds `heatApply`. Beams ignore debris. *One-Button:* PASS. *Line:* PASS, as it is vertical.

**AMMUNITION & THE ONE LOCKER (docket 7, co-sponsor) — AMEND.** A per-group round selector is per-group attention. Amend it to a **single standing choice per gun family**, set once, plus an `AUTO` default: AP against heavy-class targets, HE-frag against the rest. Also correct our Round 3 amendment: AP's armour term ("x0.35") is nearly irrelevant for the reason above, so AP should instead be defined as **+40% damage against medium-or-heavier targets**. The 50% joint armour cap is moot and withdrawn.

Tally: KEEP 3 (ENERGY-2, ENERGY-3, Dead Sky), AMEND 3 (Thermal Field, ENERGY-H, One Locker), WITHDRAW 1 (ENERGY-4).

## 3. The Order Paper

### Item 2 (LEAD): THE ADAPTIVE HULL, with Thermal Stress (heat becomes variety)

**Pitch.** *"You cannot out-spam a lattice that learns. You can out-mix it."* The heavy fleet is made of nanolattice armour that hardens against whatever has been hitting it. A single weapon family is still a strategy. It is a slow one. Lasers cook the hull so that everything else lands harder.

**Clerk's Annex**
- **Mechanic A — Thermal Stress (heat as a damage multiplier, not armour).**
  - `t.heat` 0-100 as in the Thermal Field. Damage taken from the kinetic and explosive families is multiplied by `1 + B * heat/100`, with `B` set by the target's mass class: light 0.25, medium 0.8, heavy 1.5, capital 2.0. A capital at heat 100 takes x3.0, and chaff x1.25.
  - Armour is not touched. (Verified: `hurt()` L4987-4992, `ARMOR_NERF` 0.55, hit buff x1.3 x1.9.)
  - Deposit is divided by thermal mass: `heat += dmg * k / (mass^0.6)`, with mass = base HP / 300. A heavy hull needs real laser volume, so laser spam scales and nothing is capped.
  - Cooling rises with heat (Stefan-Boltzmann, ~T^4): `-(2 + 8*(heat/100)^2)` per second in the troposphere, x0.15 in vacuum. Holding heat 100 costs steady laser fire.
  - Shields soak before any heat accrues, so a shielded wing is cooked only after its shield drops.
- **Mechanic B — Adaptive Hull (the enemy's wall).**
  - Medium and heavy enemies carry three meters `a_kin`, `a_exp`, `a_en` (0-1). Families: kinetic (shell, rail, rod, tracer, rotary, particle), explosive (missile, rocket, bomb, nuke), energy (beam, plasma).
  - Gain on a hit: `da = 12 * dmg / maxHp` into that family's meter. Decay: `-0.08*a` per second. Damage taken from a family is multiplied by `1 - 0.75*a²` (strength x0.5 on medium, x1.0 on heavy).
  - Steady state when the whole army deals `maxHp/30` damage per second: one family `a≈0.83`, factor ~0.48; two families ~0.62; three families ~0.7-0.8. At maxHp/10 per second one family falls to ~0.34. These are tuning numbers to be confirmed in a headless run.
  - Heat is not adapted against. Lasers feed the energy meter but their payoff is the Stress multiplier on the other two families.
  - Light-class enemies have no hull meter, so chaff still dies to volume.
- **Worked example.** A capital with 9,000 HP and 30 armour. 60 Hailstorms alone: kinetic factor ~0.4. The same plus 20 Laser Towers (heat ~80 after warm-up): kinetic x(1+2.0*0.8)=2.6, a net ~1.0 against a bare 0.4, a 2.6x jump from adding one laser block, with no new rule for the player to learn.
- **Exploit guard.** Because heat and kinetic multiply, three guards:
  1. Deposit divides by thermal mass, so bosses cannot be pinned at 100 by a handful of lasers.
  2. Stress applies once, and only to families other than energy, so a laser wall alone gains nothing.
  3. The worst-case multiplier is x3.0 on a capital with a fresh hull, and the adaptation floor means a mixed army reaches ~x2.5 effective. That is large but bounded by the enemy, not by a rule.
  - If playtests show the product is a blatant exploit, the fallback is a heavier boss cooling rate, an enemy answer.
- **Enemy response (Amendment I).** New enemy **Quench Tender**: a medium hull whose aura (radius 300) doubles cooling and halves heat deposit. Heavies spawn with more escorts as `t` grows. Chaff swarms are unaffected, so the player's whole existing army remains useful.
- **Code grounding.** `hurt()` L4971 (replace the armour-cut idea with the damage multiplier; multiply `eff` after L4992), `t.killBy` set by `beamHit` L4964, `particleHit` L4965 and `fire()` L4854-4881 (family lookup table), `spawnEnemy` L5588 (mass class and meters), enemy tick near L8759 (decay and cooling), `shieldgen` (L3412) for the soak.
- **Size.** L (~450 lines, mostly the family table and per-kind classes).
- **Tag.** TM1.
- **Research.** One node, `thermalload` (branch energy, tier 2, pre `directedenergy`, ~150 x 1.6, not `dang`). It owns the readout. Combo `plasma` + `particlebeam`: deposit +35%. The Adaptive Hull itself needs no research, since it is the fleet's physics. A readout chip appears with `thermalload`: "HULL: ADAPTED TO KINETIC 70%".
- **Tenet check (Amendment I).** No player toy is nerfed. The enemy gets a wall that rewards variety. Any buff (Stress) names its enemy answer (Quench Tender, hull adaptation).
- **One-Button check.** PASS. Nothing needs steering. The player builds a mix and the numbers do the rest. The readout is optional.
- **Line check.** PASS. Nothing depends on depth or flanking. Beams and the 70 px plasma burst work along the line.
- **Science.** Thermal stress and brittle fracture of big structures under steep gradients, plus radiative cooling. The adaptive hull is **SCI-FI**, consistent with canon: the Autofleet's lattice is autodrone nanostructure (a scaled cousin of the shapeshifter body), and its makers fear Earth's industry, so adaptation is a defensive reflex.

### Item 1 (re-read): see Section 2.

### Item 10: see Section 4.

## 4. Blatantly Overpowered Today

Nothing in the Energy portfolio meets the Amendment I bar. The reports show no laser, plasma or particle spam. The dominant doctrine is air plus the sky navy (Report 8: 240 Falcons, 113 Kites, 138 capital sky ships, ten to one in damage). The Particle Cannon (L3413, hitscan, ceil 0, range 1,600) is strong but costs 440 IC and 80 MP and nobody mass-builds it.

The one real monoculture in the reports is air. We propose no nerf. The **Adaptive Hull** is the answer: Falcons' missiles fall in the explosive family, so a 240-Falcon blob meets a hull that has hardened against it, and the fix is a scarier enemy rather than a rule.

## 5. Struck Self-Limits

- **The 35% power-throttle floor stays struck.** No evidence of an exploit. A shortfall is billed as IC debt (never throttling), and `IC_FLOOR` already protects the economy.
- **The armour-cut idea is struck permanently**, because it does not work. It is not a limit, it is a lever that was too short.
- **The 50% joint armour cap** accepted in Round 3 is withdrawn as moot.
- **Bore wear (Railgun's)** is not ours. We take no position, except that if restored it should be a bill-in-IC receipt and never a throttle.
