# PHYSICS — Second Reading (Round 4)

## 1. Speech

*The Chairman has brought a second slide rule, in case the first is audited.*

Mr Speaker. The President asks for an Inspector-General to say "this is too strong". The Bureau welcomes the post. It is the only office in this House that has ever been paid in Joules.

Our confession: last session we agreed to strike every limit as a nerf, and then spent two rounds adding multipliers. Energy's table, Kinetic Arms' audit and Railgun's meters are good. Together they are one equation, and an equation has to balance. We checked it. It nearly does. We have one correction (heavy hulls cool slowly, as heavy things do), and one new thing: the Line. A ground front is a row of bodies, and a bullet is a conserved quantity. The shell that kills the first body in the row **keeps going**. That one sentence gives every ground unit a job.

---

## 2. The Re-reading

**PHYSICS-1 Peak Heating — AMEND (shrink).** Heat has moved into the Thermal Field. What stays is the **plasma blackout** (while `reentry` and `heat > 60`: no retargeting, no outside shield or heal, frozen `vx`, +25% lead accuracy for AA) and the **ballistic coefficient** (heavy pods fall faster, so waves stratify by altitude). *One-Button:* PASS, since it is automatic and the player builds AA. *Line:* PASS, as it is vertical. *Amendment I:* the enemy answer is built in: heavy pods arrive faster and hotter, so a thin AA belt gets less time.

**PHYSICS-2 Thermal Loading — WITHDRAW.** Fully merged into the Thermal Field. Its one surviving idea (cooling follows the medium) is already in Energy's table.

**PHYSICS-3 Burst Altitude — KEEP (inside Dead Sky).** `detonateNuke` L7565 splits a burst into blast, thermal and EMP by density. *One-Button:* PASS. The existing aim already carries the altitude, and no new input exists. *Line:* PASS, vertical. *Amendment I:* it gives the player options. The enemy answer is the fleet's EMP-hardened shield carriers and the +40% grid PD budget Space-Fleet already filed, and the cost is Starfish's own-unit blackout.

**PHYSICS-4 Kessler Shield — WITHDRAW.** The shared debris record carries it. The **Debris Sweeper** unit is dropped, because it would be a chore. Debris decays by band half-life on its own (12 s, 40 s, 120 s).

**PHYSICS-5 Breathing Sky — AMEND.** Heaters run as sections under the megaproject tier, with **one standing toggle** (on or off) and no steering. Bosses are exempt, per the docket. Pods meet `density x (1 + 3*skyInflate)` and heat earlier. Costs stay: radar -25%, 2 IC/s per section. *Line:* PASS, vertical. *Amendment I:* a heated, sinking grid is a buff, so the fleet's answer is to boost back up (autofleet's call) and to keep its bosses above the effect.

**THE THERMAL FIELD (docket 1) — AMEND.** Adopt Energy's rewrite. The docket's "lose up to 50% armour" is **struck**, because armour is scaled by 0.55 in `hurt()` and the cut is worth about 8 points. Heat is a damage-taken multiplier by mass class. One more physics fix is Bill PHYSICS-6 below. *One-Button:* PASS. *Line:* PASS.

**DEAD SKY PROTOCOL (docket 6) — AMEND.** The Scattergun fires **on its own** at grid clusters once armed, with one standing toggle. Starfish stays an aimed strike like any strike. The Burn rung is a single, opt-in, confirmed button, which the One-Button Law permits. *Line:* PASS, vertical. *Amendment I:* the fleet's counter (+40% PD, faster re-seeding) is already filed.

**THE POWER GRID (docket 15) — KEEP.** Pool, radiators, IC-debt shortfall. We follow Railgun: bore wear is struck as bookkeeping.

**THE LONG GUN (docket 10, joined) — KEEP.** Physics note, no new rule: slew time grows with barrel length (moment of inertia), which is a delay and not a cap. It auto-targets. *Line:* PASS.

Tally: **KEEP 3** (Burst Altitude, Power Grid, Long Gun), **AMEND 4** (Peak Heating, Breathing Sky, Thermal Field, Dead Sky), **WITHDRAW 2** (Thermal Loading, Kessler Shield).

---

## 3. The Order Paper

### Item 2 — Is the bloc's heat-and-kinetic design physically coherent? Is stacking bounded?

**Audit verdict.** Coherent, with one fix. Bounded, with a table.

*What holds:*
- Heat is stored energy, so it divides by mass. Energy's `heat += dmg*k/(mass^0.6)` is correct in form.
- Radiative cooling rises with temperature (~T^4). Energy's `-(2 + 8h^2)` matches.
- Vacuum cooling x0.15 is right: only radiation works.
- Thermal stress breaks big, brittle structures first, so B rising with mass class (0.25 to 2.0) is physical.
- Spall (Kinetic) is real spallation off a stressed plate.

*What does not hold:* cooling is not scaled by mass. Heat capacity goes as mass, but surface goes as mass^(2/3), so a big hull sheds heat *per unit of heat held* more slowly (about mass^-1/3). In Energy's table a capital cools as fast as a drone. It should hold its heat. The fix is a gift to lasers (below).

**The stacking table (one hit, fresh capital, worst case).**

| Term | Source | Max |
|---|---|---|
| Existing hit buff | `hurt()` L4987 | x1.3 x 1.9 (today, unchanged) |
| AP vs medium+ | Ammunition | x1.4 |
| Thermal Stress | Energy | x3.0 (B 2.0 at heat 100) |
| Hull meters | Energy / Railgun | x1.0 fresh, down to x0.25 |
| Rail overdrive | Power Grid | x1.5 (rails only) |
| Spall | Kinetic | +25% to neighbours, not to the target |

Worst product on a fresh capital: 1.4 x 3.0 x 1.5 = **6.3**, rails only, and only while heat is pinned at 100 by sustained laser fire against a thermal mass that resists it. A mixed army also feeds the meters and the product falls to ~3.4. **Bounded: yes.** One flag for the Inspector: the Mass-Driver Railway stack (x4.6, L8441) is *outside* this product. Multiplied in, a fresh capital sees ~29. No report shows a mass-driver row. The proposal is a **single-multiplier ledger** (Kinetic's `kinMul`) that includes the railway term, so any future stack is visible in one line of code.

#### PHYSICS-6 — THERMAL MASS (and the Ablation Plateau)

**Pitch.** *"Big things are slow to warm and slow to cool. This is why the soup is still hot."*

**Clerk's Annex**
- **Mechanic.**
  - Cooling per second is multiplied by `mass^-0.33` (mass = baseHP/300, floor 1). A heavy hull (mass 10) cools at 0.47x, a capital (mass 30) at 0.33x.
  - Deposit stays Energy's `/mass^0.6`. Net: the equilibrium heat under steady fire is only about `mass^-0.27`, so a laser wall still reaches high heat on capitals, and then **holds it**.
  - **Ablation plateau.** Heat clamps at 100. Excess deposit becomes ablation damage `0.3 HP per excess heat unit`. Laser spam on a saturated target is never wasted.
- **Code grounding.** Enemy tick near L8759 (cooling), `heatApply` (Energy's module), `hurt` L4971, `spawnEnemy` L5581 (mass class already shared).
- **Size.** S (~50 lines on top of the Thermal Field).
- **TM1.**
- **Research.** No new node. It rides `thermalload` (energy, pre `directedenergy`).
- **Tenet (Amendment I).** No nerf. It is a buff to laser volume against heavies, so the enemy answer is already named: the Quench Tender (doubles cooling) and shield soak.
- **One-Button.** PASS. Nothing to steer.
- **Line.** PASS.
- **Science.** Heat capacity ~ mass; surface cooling ~ area. Real. The plateau is latent heat and ablative loss, as on a re-entry shield.

### Item 3 — The physics of a one-dimensional front

**The problem.** On a line, every unit overlaps with every other. A shell that hits one body has nothing to say about the bodies behind it. That makes tanks, infantry and artillery interchangeable damage dealers.

**The physics.** A projectile is a conserved quantity. A round that kills a target with energy to spare **perforates**. The leftover kinetic energy carries on, as in real armour penetration and real overpenetration of soft targets. On a line this is a free, automatic, one-axis rule that gives each arm a different job.

#### PHYSICS-7 — THE COLUMN (residual energy along the line)

**Pitch.** *"On a front with no flanks, the only geometry left is the row. Shoot down the row."*

**Clerk's Annex**
- **Mechanic.**
  - Flat-fire ground rounds (`shell` from tanks, `rail`, `tracer` with `heavy`, AP and sabot) that **kill** their target carry `overkill = dmg - target.hp` forward. The remainder, times 0.7, is applied as a second hit to the next hostile within 60 px along the direction of travel (maximum 3 bodies per round, so it decays and cannot chain forever).
  - **Plunging fire** (artillery, SPG, MLRS shells, rods) does not perforate. It lands from above and uses its splash radius instead.
  - Result by arm:
    - **Infantry** are the *screen*. A 46 HP body absorbs 46 points of a 70-point round. A line of rifles soaks penetrators that would have reached the tanks behind them. This is the infantry's job, and it scales with volume.
    - **Tanks and rails** are the *column-breakers*. Their high-energy round passes through cheap chaff and still reaches the heavy behind it.
    - **Artillery** is the *area* arm. It ignores screens and punishes dense stacks by splash.
  - The same rule holds for the fleet. Enemy chaff screens their heavies from your shells. That is the enemy's answer to a mass of tank fire.
- **Code grounding.** `updProj` L8906, hit branch L9159–9167; the `pierce` argument of `fire()` L4858 already exists as a hook; `GRID_E.firstHit` for the second lookup; `hurt` L4971.
- **Size.** S–M (~120 lines).
- **TM1.**
- **Research.** No new node. It is baseline physics, tied to `kinetics` for the AP rounds that carry the most.
- **Tenet (Amendment I).** No nerf. It is a buff to flat-fire arms and a use for infantry. The enemy answer is stated: chaff screens on their side. Existing artillery and air are untouched.
- **One-Button.** PASS. Fully automatic. The player builds infantry, tanks and guns, and the line sorts itself.
- **Line.** PASS. The rule exists *because* of the line.
- **Science.** Residual energy after perforation, overpenetration of soft targets, and the standard combined-arms logic of screening forces. Real.

### Item 1 — see Section 2.

---

## 4. Blatantly overpowered today

**Nothing in the Physics portfolio meets the Amendment I bar.** The nuke (`R 230`, 720 damage) is the only strong thing, and the reports show it is self-limiting: Reports 6 and 7 fired 44–46 strikes and drove stability to 0%, while Reports 8 and 10 fired none and won. A weapon that makes you lose is not making losing impossible.

**One watch item, not a charge:** the Mass-Driver Railway stack (x4.6, L8441) from Railgun's own audit. No report shows it, so we propose no nerf. Include it in the single-multiplier ledger.

## 5. Struck self-limits

- **"Lasers are damped by the sheath."** Struck in Round 3. Stays struck: it denied a toy.
- **Satellite decay under the Breathing Sky.** Struck when Science exempted friendly satellites. It stays struck. The costs that remain (radar -25%, 2 IC/s per section) are real and cover the power.
- **The Debris Sweeper.** Dropped as a chore. It does not return.
- **Debris damage to your own satellites (25%).** Kept. It is the only thing stopping a free, riskless Kessler.
