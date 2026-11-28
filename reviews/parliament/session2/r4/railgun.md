# RAILGUN — Second Reading (Round 4)
*Arsenal Bloc. Line numbers are `title.html`.*

## 1. Speech

*(The Director enters carrying a barrel section. Nobody asked. He sets it down gently.)*

Mr Speaker. The Directorate has been accused of the greatest crime in this House: striking its own penalty. Guilty. We struck a wear rule and the President's Inspector-General has demanded to know whether a gun with no section limit is a win button. We have done the sums, and we bring you the answer in IC, in orbit and in a hull that learns.

We do not ask for a cap. We ask for an enemy worth firing at. The Institute of Directed Energy has built one: a capital hull that hardens against whatever you keep feeding it. We say *good*. A Lance that learns to share a battlefield with a laser is a better Lance, and the 282-Lance doctrine is about to meet a wall that tells it how to win. We withdraw what must be withdrawn. We keep what is one button. And we fire at the sky.

## 2. The Re-reading

**RAILGUN-1 Pulsed Power (folded into THE POWER GRID) — AMEND (shrink).** Per-gun bore wear and the Rail Foundry are bookkeeping: 282 Lances each carrying a wear meter is a chore and a perf cost. Strike both. Keep overdrive only: a rail in range of a Capacitor Bank fires +50% damage and +25% rate while the pool lasts, billed as 20 MJ plus 0.4 IC per shot into the global upkeep line. *Amendment I:* nothing nerfed, baseline is the floor. *One-Button:* PASS (automatic while the pool holds, no per-gun state). *Line:* PASS.

**RAILGUN-2 Long Gun (folded into THE LONG GUN) — WITHDRAW** as a separate bill. The coalition bill is the chassis.

**RAILGUN-3 Hypervelocity Munitions (folded into THE ONE LOCKER) — WITHDRAW** as a separate bill. I accept the Institute's amendment: one standing choice per gun family, default `AUTO` (AP vs medium-or-heavier, HE-frag otherwise), and AP is defined as +40% damage against medium-or-heavy targets, since armour is only ×0.55 of a number that is already small (`ARMOR_NERF` L4987).

**RAILGUN-4 Launch Rail (The Space Gun) — AMEND.** Original: a button to reseed. *One-Button* demands the default be automatic. Amend: a standing toggle `RESEED` (default on) queues a replacement satellite whenever a Launch-Rail-class unit dies, paid at the usual IC and −35%. *Amendment I / power creep:* −35% IC and no rocket part is a buff, so it names its enemy answer: the Launch Rail's plume is a priority bombarder target (`busterScore` L7446), and the bombarder grid (report 7: 42 of 44 Halos lost) is untouched. *Line:* PASS (vertical).

**RAILGUN-X Meridian Accelerator (via the Trunk graph) — AMEND.** Original: fixed azimuth chosen by the player once per 60 s. Amend: it **auto-fires** at the highest-HP hostile in reach (boss priority) with a single standing toggle; manual aim stays optional. *Line:* PASS (the tube runs along the line, the slug leaves upward). *Amendment I:* damage `3 × tube px / 10` (up to ~1,800) faces the Adaptive Hull below, and its enemy answer is a cut tube.

**THE LONG GUN (docket 10) — AMEND.** See Section 5 and Bill 6. *One-Button:* PASS (sections are place-and-forget, the gun fires itself at boss priority; three firing arcs are an optional toggle). *Line:* PASS (west/zenith/east arcs fit a 1-D strip).

**THE TRUNK (docket 8) — KEEP.** My share is the Launch Rail rider (counts as a rail-linked Space Center) and the Meridian tube, both automatic. *One-Button:* PASS if hubs auto-link, as the bill says. *Line:* PASS (horizontal rail, vertical depth underground).

**Tally:** KEEP 1, AMEND 4 (Pulsed Power, Launch Rail, Meridian, Long Gun), WITHDRAW 2 (RAILGUN-2, RAILGUN-3).

## 3. The Order Paper

### Item 2 — RAILGUN-5: THE HYPERVELOCITY FAMILY (the railgun's half of a mixed army)

**Pitch.** *"The Lance is the anvil, the laser is the furnace. A hull that has learned to shrug off shells has not learned to shrug off a slug that arrives before the lesson."* Under the Institute's Adaptive Hull, `rail`, `rod`, `krod` and `amslug` are filed under generic kinetic. Our 282-Lance doctrine would then meet a wall that is built against exactly one thing. We ask for our own family.

**Clerk's Annex**
- **Mechanic.**
  - Split the Adaptive Hull's `a_kin` meter into `a_bal` (shell, tracer, rotary) and `a_hyp` (rail, rod, krod, amslug, and the Long Gun). A hull now adapts to a Hailstorm line and a Lance line *separately*. A mix of both is already variety.
  - `a_hyp` gains at **0.5×** the Institute's rate, `da = 6 × dmg / maxHp`. Real basis: above roughly 2.5–3 km/s impact velocities, penetration is **hydrodynamic**. The projectile and armour flow like fluids and strength matters little (penetration depth scales with rod length and √(ρ_p/ρ_t)). A hull cannot harden against a hit that arrives faster than it can react.
  - The Institute's decay (−0.08·a/s), factor `1 − 0.75·a²` and the Thermal Stress multiplier (`1 + B × heat/100`, capped x3.0 on capitals) apply unchanged. **Per-hit gain is capped at 0.6**, so one big slug cannot saturate a hull.
  - Worked example: capital, 9,000 HP, 60 Lances (70 dmg, one hit per 4.5 s each). Gain per hit is 6×70/9000 = 0.047, so ~13 hits per second raise `a_hyp` to ~0.55 and the factor settles near 0.77 (against ~0.48 for a one-family blob). Add 20 Laser Towers (heat ~80): Stress x(1+2.0×0.8) = 2.6, so effective ~2.0 against bare 0.77 — a 2.6× jump from adding one laser block.
  - Enemy answer (Amendment I): the **Quench Tender** and shield soak from Energy's bill remain; the Lance's buff is only that its family adapts more slowly. No existing hostile gets weaker.
- **Code grounding.** `hurt()` L4971 and the armour block L4987-4992 (apply the family multiplier after L4992), the kill-class mapping at L6680 (`case 'rail': case 'rod': case 'krod': case 'amslug': return 'kinetic'` — split into `hyp`), the `rail` branch of `fire()` L4879, `spawnEnemy` L5581 (init meters), enemy tick near L8759 (decay).
- **Size.** S on top of the Institute's L bill (~60 lines).
- **Tag.** TM1.
- **Research.** None of its own. It rides `thermalload` (Energy). Combo `particlebeam`: the particle slug joins `a_hyp` rather than the ballistic family.
- **Tenet (Amendment I).** No nerf. It protects an existing doctrine from becoming worthless under the new enemy, and the answer to the buff is the enemy's own Tender and shield.
- **One-Button.** PASS. The player builds Lances and lasers, and the meters run themselves.
- **Line.** PASS. Nothing depends on depth.

### Item 10 (answer): see Section 4. There is no nerf to propose.

## 5. Struck Self-Limits (the Long Gun)

**What I struck.** In Round 3 I struck my Recoil Cradle's +30% section-hp penalty ("a nerf in a costume"). I stand by it. It punished a missing cosmetic part and stopped no exploit, so under Amendment I it stays struck.

**The harder question: is a Long Gun with no section limit a win button?** I did the arithmetic rather than speculating.
- At N = 40 sections: damage `100 × 40 = 4,000` per 20 s shot, build cost `40 × 380 = 15,200 IC`, upkeep `40 × 6 = 240 IC/s` plus 1,600 IC per shot.
- Report 7 banked 212k IC at 850 IC/s, so **IC is not a brake**: the whole gun is 18 s of income and the upkeep is 28% of it. The tenet allows that.
- MEGA-H lets an N ≥ 40 gun take 60% of the *anchor* per hit, so two shots end the invasion (G.withdraw). That is one standing button that wins. It is gated (`fleet/fleetMax < 0.6` and `heat > 100`), so it is a finisher, not an opener, but a finisher that only costs IC is exactly the one-button win Amendment I worries about.

**Ruling.** No hard section cap (Article III, and no evidence of a blatant exploit yet). Instead the **enemy answers**, in three layers, so the limit is the fleet's:
1. **Slew is the gun's natural limit.** 1.5 s per section means a 40-section gun takes 60 s to swing across its three arcs. It is a pre-aimed siege gun, not a point-defence, and it cannot answer a swarm.
2. **The hull learns the gun** (RAILGUN-5). A first shot into a capital sets `a_hyp` to the 0.6 cap. With decay −0.08·a/s the hull takes ~17 s to forget, so the 20 s second shot meets ~0.26 resistance. The gun keeps hitting but a lone gun slows against the same hull. Lasers and other families keep it shredding.
3. **The fleet hunts the gun** (Bill 6).

### Item 4 (Long Gun's limit) — RAILGUN-6: THE SILENCER WING (the enemy answer to the Long Gun)

**Pitch.** *"The fleet is afraid of an industrialising Earth. It is more afraid of this."* Every completed section is a section the fleet counts. When the barrel grows past what it can ignore, it sends something to silence it.

**Clerk's Annex**
- **Mechanic.**
  - Each completed Long Gun section adds **3** to the fleet's Fear weight (a normal building adds 1; docket item 9 shows the player why).
  - Every **10 completed sections**, the next wave brings one **Silencer** to the gun: an Annihilator-class hull (hp 700×t, armour 8, holds at `SPACE_Y`) with a 6 s beam charge aimed at the largest section. Beam damage 60×t per second to that section. A Long Gun hit on a Silencer **interrupts the charge** (KINETIC-X rule). Escort: 4 fighters per Silencer.
  - The gun can shoot Silencers in orbit (`ceil:0`), so a small gun races a single Silencer and a big gun cannot ignore a pack. The player answers with AA and bunker rings (Fortification Corps) or with more guns.
  - Tripwire for the Speaker: if a headless run shows an N ≥ 40 gun ending the anchor fight before `fleet/fleetMax` reaches 0.6, raise the anchor's hp scaling in `spawnSuper` rather than capping sections.
- **Code grounding.** `spawnSuper` L5269 and the `annihilator` branch L5621 (add `silencer`), `INTENT` L5119 wave fields, `busterScore` L7446 (target priority), the Fear weight hook from AUTOFLEET-2, `weaponCeil` L4488.
- **Size.** M (~220 lines) on top of the Long Gun.
- **Tag.** TM1.
- **Research.** Rides `megastructures` (space, tier 4); no extra node.
- **Tenet (Amendment I).** No nerf and no cap on the player. The buff (the gun) names its enemy answer (the Silencer and the hull).
- **One-Button.** PASS. The player builds sections. The Silencer arrives on its own. Fortifying is optional.
- **Line.** PASS. The Silencer attacks from above, the vertical axis.

## 4. Blatantly Overpowered Today

**Nothing in the Railgun portfolio meets the Amendment I bar, and I will not invent one.**
- **The Lance (282 built, report 6).** Uninterceptable (`PD_UNINTERCEPTABLE` L8526), `ceil:0`, unlimited ammo (L4495). But reports 6–10 were wins won by air and the sky navy (report 8: 240 Falcons, 113 Kites, 138 capital sky ships), and report 7 was a pyrrhic win with 3/7 cities and 226 buildings lost. No match shows the Lance making loss impossible or other options pointless.
- **Watch item, not a charge: the Mass-Driver Railway stack** (L8441). `dmgMul = 1 + .6 × segs` counts neighbours within 90 px. Segments are 26 px wide, so ~6 neighbours fit, giving ×4.6, or 59 dps against a Lance's 15.6 (`64×4.6/5` against `70/4.5`) for 360 IC against 290. That is 3.8× the DPS per IC. The reports show no mass-driver rows, so I propose no change. If the Inspector shows a run where a packed row is the only thing winning, the answer is an enemy that hunts dense rows, not a rule. The Adaptive Hull (a_hyp) already bites such a row.
- **Real monoculture in the reports: air.** Not my portfolio. The Institute's Adaptive Hull is the answer there too.

## Summary
KEEP 1, AMEND 4, WITHDRAW 2. New bills: RAILGUN-5 Hypervelocity Family, RAILGUN-6 Silencer Wing.
