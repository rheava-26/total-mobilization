# SCIENCE COUNCIL — Second Reading (Round 4)

## 1. Speech

Mr Speaker. The President says the House drifted toward power creep, and the Academy confesses it: we proposed an uncapped +3% per laser, and a laser wall grows with the square of its size. Nobody on our side said "too strong." The Inspector will. Good. Science without a referee is theology.

We welcome Amendment I. A nerf with proof is a measurement, and measurement is our trade. We welcome Amendment II. The best research is the kind a player never has to think about: you finish a combo, and the world is different. We welcome Amendment III. The Line is a gift: everything that matters in this game happens *up* or *down*, and the tree has been pointing there since the day we proposed pulling the sky down.

So we bring fewer bills and sharper ones. One fork that decides what your civilization becomes. One audit that guarantees a counter to every wall the fleet can build. And one confession: we cut our own parallel slots, because they needed babysitting.

## 2. The Re-reading

**SCIENCE-1 Foreign Materiel / the Dossier Line (docket #16) — KEEP.** One-Button: recovery is automatic. A wreck is "recovered" when any unit stands on it or a Lab or Yard is in range, and the sample, the dossier and the alloy all land without a click. Ten Labs give ten times the coverage. Line: wrecks lie on the front or fall along it; no depth needed. Nothing here is a nerf, and the fleet answers with Sanitisers.

**SCIENCE-2 Synthesis Doctrines — AMEND.** Doctrine 2 (the forecast) lives in THE ONE FORECAST (#4) and is withdrawn here. Doctrine 3, Stigmergy, stays: it is automatic (swarms share one mark; no orders). Doctrine 1, Phased Arrays (docket #15, Power Grid), needs honest treatment. +3% per emitter within 350 px is quadratic in density. On the Line it bounds itself by geometry, not by a rule: a Laser Tower is `w` 19 px, so a 700 px window holds about 36 emitters per layer, roughly x2 at saturation, not x4. We keep it uncapped and name the watch: if a report shows Phased laser walls delete bosses unaided, the answer is the Quench Tender (ENERGY), not a cap. One-Button: pass. Line: pass, and a buried second layer is a real extra dimension.

**SCIENCE-3 Containment — AMEND.** Distance from cities was a Line problem: on a strip, "far" is only a position along x, which the fleet can land on. Amendment: the unrest multiplier is read from **depth**, not distance. A Containment Lab at dig level 0 gives x1.0 of `sc`, at level 1 x0.7, at level 2 x0.4 (`digLevel` L3863, `UG_DEPTH` L3843). It rides on the Charter's dial (docket #12) as a second factor, set once when you place it. One-Button: pass. Line: pass, the depth is vertical. Enemy answer: a buried lab is a Deep Penetrator target (Subterranean).

**SCIENCE-4 Crash Programs and Parallel Slots — AMEND (shrink).** Parallel slots touched about ten sites of the single `G.research` object (L4667-4705, L6351, HUD at L11027) and would need a slot-management UI. That is babysitting and an L build for little. **Withdrawn.** Crash funding survives as one standing toggle: "Crash by default", off or on. When on, an active node draws at 2.5x rate for 1.4x influence. One-Button: pass. Line: n/a. Size S.

**SCIENCE-5 Geoscience — KEEP, in two coalition bills.** `ionoheat` is in THE BREATHING SKY (heaters in sections, bosses exempt); grades and geothermal are in DEEP DEPOSITS (#9, merged with Planet's `deepcore`). One-Button: sections and extractors are build-and-forget. Line: vertical (drag descends the sky, geothermal descends the ground).

**SCIENCE-X The Return Address — KEEP as the hot take.** One-Button: the 600 s charge runs by itself after commissioning, and there is exactly one decision at the end (the payload), made once. Line: n/a. Needs MEGA-1.

**Docket coalitions I co-sponsor:** DEAD SKY PROTOCOL (#6): KEEP, because `ionoheat` is the burn-down valve. THE CHARTER (#12): KEEP, because it turns the silent `dangCap` L4708 into a visible dial. POWER GRID (#15): KEEP, see Phased Arrays above.

## 3. The Order Paper

**#1.** Done in Section 2.

### SCIENCE-6 — THE FORK (item #5: a combo that changes what society becomes)

*Pitch.* "A combo that only unlocks a unit is a recipe. A combo that decides what your people become is a constitution. Two roads lead past the last wall: build the minds, or raise the people. Both arrive. They arrive as different civilisations."

*Science.* Brain-computer interfaces are real (Utah arrays, 2004 onward; Neuralink-class implants). Distributed cognition in colonies is real (ant and bee collective decisions). **SCI-FI:** a nation-scale network where units are wearing one mind. Internal logic (canon): psionics are a decentralised command structure, and the fleet is a machine intelligence, so the human road risks panic and the machine road risks capture.

**Clerk's Annex**
- **Mechanic.** New tier-3 combo node `integration` (pre `psionics` + `robotics`, cost 420 base x1.6, `dang`, sc 8). On completion `finishResearch` (L4703) fires one **standing choice, once**:
  - **Ascension** (society `verdant`): bio and clone family units (`UNIT_FAM` L3375) gain +25% HP and heal 1%/s out of combat; stability cap +3; +3 influence/s. Cost and enemy answer: **the Hush** (PSI-4) pulses panic through organic units outside a Relay's range, -20% fire rate for 6 s, every 40 s, and the fleet raises Hush weights x2 when it scans 30+ organic units.
  - **Singularity** (society `machine`): mechanical units (`isMechanical`) build 20% faster, and drone swarm `nMax` rises 48 to 72 with one shared target mark. Cost and enemy answer: **Spoofers** (new enemy, 60 HP, air) jam units more than 600 px from a Data Center or Comms Tower, -25% fire rate while jammed. The fleet raises Spoofer weight x2 when it scans 30+ drones.
  - Neither choice touches the manpower or army-cap formula (Article IV.1). The word "Transcendence" is dropped: we do not rip out the governor.
- **Code.** `finishResearch` L4703, `society()` L3549, `UNIT_FAM` L3375, `isMechanical`, swarm params L3958, `scanEnemyIntel` L5737, `counterBias` L5526, `spawnEnemy` L5581.
- **Size.** M (~250 lines). **TM1** (the fork seeds the TM2 society reading).
- **Research tree.** `integration`, combo partners `psionics`, `robotics`; optional second tier `meshnet` (pre `integration`, not dang) adds +10 bodies to swarms.
- **Tenet check (Amendment I).** Pure power, each side with a named enemy answer; the player's existing army is not made worthless, only half of it is buffed.
- **One-Button.** Pass. One standing choice, then automatic.
- **Line.** Pass. Jamming and panic work on one front.

### SCIENCE-7 — THE OPEN DOORS (item #2: always a counter)

*Pitch.* "Energy's Adaptive Hull builds a wall against the unit you spam. The Academy builds the door in the wall, and a guarantee that the door is never locked."

**Clerk's Annex**
- **The guarantee (a design rule with a test).** For each hull family (kinetic, explosive, energy), at least one unit or building is reachable by a player who has researched **at most tier 1 and no `dang` node**. Today: kinetic and explosive are free at start (`unlocked.kinetics`, `rocketry`); energy needs only `directedenergy` (tier 1, 55 x 1.6 = 88 INF). A researchless player can still mix kinetic and explosive (~0.62 adaptation vs ~0.83), and the energy road is one cheap node. A headless audit `auditCounterPaths()` runs in `tutquick`, listing the cheapest unlock per family and failing the build if any is above tier 1.
- **New node `hullanalysis`** (branch `energy`, tier 2, pre `directedenergy` + `automation`, cost 140 x 1.6, not `dang`). Effect: (a) the HULL readout shows for every enemy in radar range, and (b) target selection is **automatic**: when two targets are within range, units prefer the one whose adapted family differs from their own weapon's. No player input. Enemy answer: the Quench Tender's aura scrambles the readout inside its radius.
- **Samples help.** With the Dossier Line, each dossier of a heavy kind reduces its adaptation gain by 6%, floor 50% of base. That is a buff to the player, answered by the Tenders and by escorts that grow with `t`.
- **Code.** `hurt()` L4971 (family lookup), target acquisition near `acquireEnemy`, `resCostOf` L4663, `RESEARCH` L3502.
- **Size.** M (~180 lines, plus the audit).
- **TM1.**
- **Tenet check.** No nerf; it only adds. Fine under Amendment I because the enemy keeps its wall and the Tender.
- **One-Button.** Pass (auto-priority). **Line.** Pass.

## 4. Blatantly overpowered today

**Research damage stacking: watch, not struck.** `RESEARCH` L3502-3547 grants `mult.dmg` +1.55, `mult.rof` +0.5, `mult.armor` +0.75 and `mult.range` +0.79 if every node is taken. Damage uses `(1 + G.mult.dmg)` at L8247-8447 and fire rate is a separate multiplier, so a fully researched army does about x2.55 x 1.5 = x3.8 DPS per weapon, before building auras and the Phased bonus. That is large. It is not blatant by Amendment I's proof: Reports 6-10 were won by *air plus sky navy at ten to one* (Report 8), a doctrine problem rather than a research one, and the research hardline win was pre-v3.76. The answer is the Adaptive Hull plus Tender scaling with `t`. If a report shows a researched-and-spammed single type ending the match unaided, we will propose a nerf with the evidence. We will not preempt it.

## 5. Struck self-limits

None of our limits were struck as nerfs. Two clarifications. The 0.4 IC/s per-emitter upkeep on Phased Arrays was replaced by the ENERGY-2 power bill, which is the same cost in a better wrapper. And the 40% floor on the Dossier discount stays: it only bites on free research, and it is not a cap on power, only on a price.
