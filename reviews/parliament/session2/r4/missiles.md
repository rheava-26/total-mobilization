# ROCKET FORCES: Second Reading (Round Four)

## 1. Speech

Mr Speaker. I thank the President for the Address and the Inspector-General for the clipboard. He found three things in my bills that were wrong and one that was a crater. I will fix all four.

Amendment I says a nerf needs proof. I have no nerfs to propose against my own toys, but I will correct my own arithmetic. Amendment II says nothing may need babysitting. The Rocket Forces were accused of "the countdown nobody presses". Fine: from today the silo presses itself, on a standing posture chosen once, and keeps a stability reserve by default. Amendment III says the ground is a line. My missiles have never cared. They go up.

The Inspector asks whether the Kinetic Lance is overpowered. I have the numbers and I will give them, not a speech. On the other matter, I confess I gave away my Burn rung's only real cost last time. I am taking it back.

## 2. The Re-reading

**MISSILES-1 Cruise ripple. KEEP.** Clean per the Inspector. *One-Button:* launched through `tickAutoStrikes` (L7708) by standing posture (see MISSILES-8); no per-round steering. *Line:* a cruise round follows the terrain heightfield (`groundY`), which is a 1-D function, so it is the Line made literal.

**MISSILES-2 Dispersal (docket 14). KEEP.** TEL relocation, magazine reload from a Depot and sea launchers are automatic. *One-Button:* the player builds and the launchers fill themselves. *Line:* Boomer and Arsenal Ship live in the sea/depth bands (vertical), the TEL on the surface.

**MISSILES-3 HGV. AMEND.** The Inspector is right: 160 damage at 70 IC loses to `ballistic` (360 at R150, 35 IC). New job: the **shield and armour breaker**. IC 45; ignores armour entirely and does 220 against shield domes and `shieldHp`, with a 1.3 s PD window. Nothing else in the strike menu can crack a Shield Generator dome (900 hp) cheaply, and the heat bill rewards a mix. *One-Button:* auto-picked by `tickAutoStrikes` when the target cluster has a shield or armour above 15, no player choice. *Line:* no change.

**MISSILES-4 PN and seekers. AMEND.** The creep charge stands. Fix: proportional navigation is a straight buff; every existing missile defaults to a flare-proof `dual` seeker. The `ir` package is the cheap alternative (30% cheaper, diverted by flares) and the `arm` package is the Weasel counter. *One-Button:* default dual, one standing dropdown, nothing per unit. Enemy answer: flares scale with `t` and only gunships and bombers carry them.

**MISSILES-5 Warhead Stockpile (docket 7). AMEND.** Delete the "consecutive use costs 25% less stability" clause, as the Inspector demands: the stock and upkeep are the brake. Keep the 0.2 IC/s upkeep per stored nuke, the fallout risk and the fragile Depot. *One-Button:* fed by the standing ledger; auto-strike spends it. *Line:* n/a.

**MISSILES-X Starfish (docket 6 Dead Sky). AMEND.** `e.elec` has no hits, so I replace it with an explicit **EMP list** written into the bill: any unit or enemy with `drone`, `mech`, `elec`, `clone`, `sat`, plus capital sky ships and shield generators; aircraft carry a `hardened` flag only after `radhard`. The cost then lands on both fleets, not on space alone. **Restored self-limit:** the Burn rung takes your own lift offline while the belt holds above 50% (about 90 s). It is opt-in, voluntary and time-boxed, so it is a cost, not a cap. Lane damage scales with the hull `t` (35 + 1.2t becomes `20t`-class) as the Inspector's arithmetic requires. *One-Button:* Burst and Burn are one-time confirmed doctrines; the Lane is automatic.

**Docket 7 Ammunition and the One Locker. AMEND.** I co-sponsor only the stockpile half. The per-group round selector fails Amendment II. Ammunition auto-selects by target armour, with one standing "ammo doctrine" choice. *Line:* n/a.

**Docket 11 One Net. AMEND.** The `+4%` per linked unit is quadratic, as the Inspector shows. Delete the per-unit bonus: N units already deliver N shots. SYNC FIRE auto-triggers on boss-class targets and is otherwise optional. Strategic rounds join the net. *Line:* passes.

## 3. The Order Paper

### #1. Re-read. Done above. #8 and #10 follow.

### Item #8: Missile-defence stations

**MISSILES-6: The Interceptor Grid.**
*Pitch.* An AMI battery has four rounds. Ten of them have forty, but ten that fire at the same buster waste thirty-six. Make them a net.
**Clerk's Annex**
- **Mechanic.** AMI batteries within 500 px form a **grid** automatically. They share a claim ledger so no two shoot the same threat (the `under` claim logic in `acquireIncoming`, L4465); magazines pool, and rearm speed is `AMI_REARM / sqrt(N)` for N linked batteries. Grid size is unlimited; ten batteries rearm about 3x faster, and the enemy answers with more saturating barrages (bombarder `PD_SAT` budgets grow with `t`).
- **Science.** Real. THAAD and Aegis BMD use cooperative engagement and shoot-look-shoot to avoid double-tapping; reload logistics, not launchers, limit a battery.
- **Code grounding.** AMI branch L8422 (`b.ammo`, `b.rearm`); `acquireAmiTarget` L8391; `AMI_REARM`.
- **Size.** S (about 90 lines). **TM1.**
- **Research.** Existing: AMI is unlocked from the start. Attach to `pointdefense` (L3533) as a passive upgrade, cost 90 base.
- **Tenet (Amendment I).** A buff; the enemy answer is the bombarder budget scaling with `t`. No nerf.
- **One-Button.** Automatic membership. Nothing to click.
- **Line.** Interceptors fly vertically from the surface line; passes.

**MISSILES-7: The Early-Warning Radar ("the cue").**
*Pitch.* You cannot hit what you see late. A radar that sees the fleet's ordnance leave orbit gives the grid time.
**Clerk's Annex**
- **Mechanic.** New building `Early-Warning Radar` (DEF, 260 IC, 60 MP, hp 220). Within 1,200 px of it, AMI, Aegis and SAM batteries gain +25% range and may engage re-entering ordnance (busters, rods) during its slow crawl phase instead of waiting for the acceleration. It also shows the player a **threat-axis arrow** on the HUD (the explainer for the Fear Index, order 9). Stacks additively per radar, so ten radars cover ten stretches.
- **Science.** Real. Early-warning radars (Pave Paws, Don-2N) cue interceptors minutes ahead; cueing, not interceptor speed, decides midcourse defence.
- **Code grounding.** `radarBoost` (L8433 usage), `BUILD_DEFS` `radar` L3407, `acquireAmiTarget` L8391, `REENTRY_V0_FRAC` crawl in `spawnBuster` L7485.
- **Size.** S-M (about 140 lines). **TM1.**
- **Research.** `pointdefense` + `directedenergy`, cost 130 base, not `dang`.
- **Tenet (Amendment I).** Buff plus enemy answer: busters gain more decoys as `t` grows.
- **One-Button.** Passive aura.
- **Line.** Range is horizontal reach along the line; the arrow points left or right. Passes.

### Item #10: Are strategic strikes overpowered, useless or mis-costed?

**Verdict: mis-costed, not overpowered, and partly useless late.** Amendment I's proof bar is that the thing makes losing impossible or other options pointless.
- **Kinetic Lance, the Ground Inspector's watch item.** `detonateKinetic` (L7586) does `areaDamage(x, y, 118, 620, 'rod')`; 40 IC, cd 5 s, no stability, needs a Tungsten Rods satellite. It does not meet the bar. Report 7 used 44-46 strikes and still ended pyrrhic (3 of 7 cities, 81% of units lost). Report 8 used zero and won ten to one. A 620-damage hit with a 118 px radius does not scale with `t`: bomber hp is `52t` (about 680 at ten minutes), so it decays into chaff clearing. Enemy PD already shreds it: `STRAT_MSL_HP` 90 against `PD_LANCE_DMG` 30 means three lances kill a round, and domes absorb it at `+90` (L8937).
- **The real defect is the brake.** Docket 7's stockpile replaces the 5 s silo cooldown with IC-bought stock. A lance at 40 IC then has no brake except IC, and 850 IC/s buys 21 lances per second across enough silos. That is the exploit, and it does not exist today.
- **Stability.** Only taboo strikes cost it (`stratStab` L6897). The 44-46 strike spiral came from `tickAutoStrikes` firing taboo rounds down to the floor (`G.stab <= sc + 5`). That is a One-Button footgun, not a flaw of the strikes.

**MISSILES-8: Strike Posture (one standing choice).**
**Clerk's Annex**
- **Mechanic.** Each silo, TEL or boat carries one standing **posture**: *Conventional* (default; auto-fires only non-taboo strikes), *Reserve* (taboo allowed while stability stays above 50%), or *Free* (current behaviour). Auto-strike prefers a cheaper strike that kills the target (a HE does not need a nuke). Strike yield reads `G.mult.dmg` (a buff, so strikes keep up with `t`). **Enemy answer:** `scanEnemyIntel` gains a `strikes` axis from the last 60 s of launches: when it is high the fleet widens its column spacing 1.6x and escorts gain +30% PD budget.
- **Code grounding.** `tickAutoStrikes` L7708, `stratStab` L6897, `launchStrike` L6943, `areaDamage`, `scanEnemyIntel` L5737, `PD_SAT` L8529.
- **Size.** M (about 150 lines). **TM1.**
- **Research.** None; part of the silo. Taboo strikes keep their existing nodes.
- **Tenet (Amendment I).** A default and a buff, not a nerf; `Free` remains for the player who wants it. The enemy responds. No cap.
- **One-Button.** One choice, made once.
- **Line.** n/a.

**MISSILES-9: The Rod Magazine (Lance brake that scales with volume).**
**Clerk's Annex**
- **Mechanic.** Each Tungsten Rods "Zeus" platform carries a magazine of 6 rods and recovers one every 20 s from the Space Center lift. A Kinetic Lance spends one rod from any Zeus in orbit, not a silo cooldown (silo cd stays 1.5 s so ripples still work). N platforms hold 6N rods and refill N times faster: ten Zeus, ten times the barrage. Lance IC stays 40.
- **Science.** Real. The Project Thor concept: rod mass per platform is the constraint, not the trigger; a tungsten rod of 100 kg hits at about 3 km/s with roughly a ton of TNT.
- **Code grounding.** `STRAT.kinetic` L6882 (`unitReq: 'rods'`), `launchStrike` L6977, `UNIT_DEFS.rods` L3219 (`lift: 3`, `comp`).
- **Size.** S (about 70 lines). **TM1.**
- **Research.** Existing `kinetics`; no new node.
- **Tenet (Amendment I).** Not a nerf: it replaces a flat cooldown with a brake that rises with how much the player builds, and docket 7 would otherwise delete the only brake. If the Speaker rules it a nerf, I note the proof is the stockpile's 21 lances per second.
- **One-Button.** Automatic draw from the nearest platform.
- **Line.** The rod falls vertically; passes.

## 4. Blatantly overpowered today

**Nothing in my portfolio meets the bar.** Strategic strikes have no report where they made losing impossible. The one future hazard is the Lance after docket 7 lands, which MISSILES-9 fixes before it ships. I agree with the Inspector that **the sky ship discount bug (`facsFor`) is the one blatant item in the bloc**, and it is not mine to fix.

## 5. Struck self-limits

I struck two in the First Session: (1) the Burn rung's forced lift outage, restored above as a time-boxed cost; (2) the "consecutive-use stability discount" is not a limit but a loophole, and I withdraw it. I also restore the **Warhead Stock upkeep and fragile Depot**, which the Inspector rightly says are the only brakes on the stockpile.
