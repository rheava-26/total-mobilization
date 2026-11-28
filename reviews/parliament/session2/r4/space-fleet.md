# SPACE FLEET COUNCIL — Round Four: Second Reading
*Slug: `space-fleet`. Sky Bloc. The Admiral of the Void.*

## 1. Speech
Mr Speaker. The Inspector-General says my fleet "dies for free and nobody needed it". I have read his report. He is right, and I resent how much I enjoyed it.

I came to this House asking for a better grave. The Inspector shows me the sky already kills the grid from the mesosphere. My fleet has *no job air lacks*. A satellite that duplicates a Falcon is a very expensive ornament.

So this reading is not about saving my toys. It is about giving space the one thing air cannot do: **hold the heat**. Vacuum does not cool. A hull I cook in the exosphere stays cooked while the Air Marshal's jets spend half their lives refuelling. That is a job.

Amendment II: my fleet needs no babysitting, and I will strip every button I wrote. Amendment III: the ground is a line, so I look up. I am pleased to be the Speaker's only seat that already lives at the top of the sky.

## 2. The Re-reading
**SPACE-1 Tinsel decoys. KEEP.** *One-Button:* decoys auto-launch with the satellites they shield (one Tinsel per three sats, standing choice made once on the Space Center). *Line:* vertical. The Inspector found it clean.

**SPACE-2 / ONE NET (co-sponsored with Air, Kinetic). AMEND.** The Inspector's charge is true: +4% per linked unit is quadratic (N shots × (1+0.04N), about ×16 at N≈380), and it needed a press. Amendment: **delete the per-unit bonus.** N shots already scale with N. SYNC FIRE becomes automatic on any boss-class target (flagship, bombarder, annihilator), the 6 s hold stays as the exposure, and the original clause returns: *the volley gains no damage beyond the units' own firing.* *Line:* vertical, passes.

**SPACE-3 Helios. AMEND.** The Inspector's arithmetic is right (10 Helios among 30 clustered plants: 325 IC/s for 4,200 IC, payback 13 s). Amendment: **each Power Plant feeds one Helios, assigned automatically (nearest first).** Linear: a Helios earns 2.5 IC/s plus 1 IC/s if paired with a plant. *One-Button:* the pairing is automatic. *Line:* passes.

**SPACE-4 Retrograde Shotgun. AMEND.** The Inspector is right that `35 + 1.2t` vs railsat hp `20t` (L5644) stops killing after minute one. Amendment: lane damage `12t + 20`, armour halved, so a railsat (20t) dies in two lanes at any clock and a mender (110t) in ten. Debris half-life applies by altitude band: grid sats sit at the mesosphere (`MESO_TOP+40..+120`), which gets the **40 s** thermosphere value. Fires automatically at the densest grid cluster in range. Friendly fire 25% stays unless hardened.

**SPACE-5 Orion. AMEND.** On the numbers it is a worse Sky Railgun Ship, as the Inspector says. Its job is the one nobody else has: **it is a forward base in orbit**. Strike Fighters rearm, repair 4%/s and respawn at full cost on its deck. The pulse dash fires automatically when a boss is within 600 px, once per 20 s (no button). *Line:* vertical.

**SPACE-X Kessler Doctrine. AMEND (a self-limit comes back).** In Round 3 I gave away "your own lift offline". The Inspector is right that it was the only real cost. **Restored, time-boxed:** lift offline while the belt holds above 50% (about 90 s), plus the stability cost. It is a real cost, not a cap, and the doctrine is opt-in. *One-Button:* one standing decision, taken once.

**Docket 6, DEAD SKY PROTOCOL. AMEND.** The Inspector's charge is false-claim: `e.elec` has 0 hits and `isMechanical` (L4541) hits Frigates and Pickets but spares Falcons and Halos. That makes air dominance worse. Amendment, **altitude-based, no flag list:**
- Starfish EMP affects every friendly unit above `STRATO_Y`, regardless of flags: sats, space hulls and high sky ships alike. Aircraft below the stratosphere are shielded by air (density attenuation: real, EMP coupling falls with distance from burst).
- Enemy side: all `anchored && air` hulls (the grid) and `orbital` bosses lose point defence and weapons for 25 s. `radhard` (tier 2, about 180) exempts a unit; a hardened sat is the player's choice made once.
- Scattergun uses the amended `12t+20`, and the debris zone half-lives are 12 / 40 / 120 s.
*Tenet (Amendment I):* all player-facing costs are voluntary. *Line:* vertical.

**Docket (smaller) Orion bill. KEEP.** One node, `dang`. **Debris Ledger. KEEP** (structure only). **Breathing Sky (co-sponsor). KEEP with exemption:** bosses exempt, enemy grid sinks to `MESO_Y` and stays anchored.

## 3. The Order Paper

### Item #2 — SPACE-6: THE COLD HULL (space holds the heat)
**Pitch.** Air cools in seconds. Vacuum does not. Let the exosphere be where a cooked hull *stays* cooked.
**Clerk's Annex**
- **Mechanic.** Docket 1's `heat` field: cooling is fast in air and slow in vacuum. Space adds two units that make that matter:
  1. **Halo upgrade (no new unit):** the existing Halo beam deposits heat as a laser (+8 heat/s per beam). Because enemy grid hulls, the flagship and bombarders park above `MESO_TOP`, heat decays at x0.25 the air rate. Three Halos on a bombarder (hp scaled by t) reach heat 90 in about 4 s and stay above 60 for ~25 s.
  2. **Soletta Mirror "Znamya"** (new sat, 380 IC, lift 3, hp 70): a large orbital reflector that bathes a 500 px footprint (centred on the highest-HP enemy within 1,300 px, automatic) at +14 heat/s. It deals no damage itself. Real basis: Russia's Znamya 2 (1993) deployed a 20 m reflective sheet in orbit and lit a ground spot; the Soletta concept is a classic solar-reflector study.
- **The wall is the enemy's.** Heat gives up to x1.5 kinetic damage and -50% armour on hot targets (docket 1). A flagship (9,000 HP x clock, armour 30) is nearly untouched by light rounds; a Mirror plus Halos plus rails and rods kill it in a fraction of the time. A pure rail fleet still works, slowly. No cap anywhere.
- **Code grounding.** `UNIT_DEFS` L3217 (new `solettamirror` beside `satlaser`), docket-1 `heat` field and cooling by `e.y` band, `acquireEnemy` L4374 for the highest-HP score, sat hold L8092.
- **Size.** S-M (about 140 lines on top of the docket's heat field).
- **TM1.**
- **Research.** New node `soletta` (space branch, pre: `orbitalcmd` + `directedenergy`, cost about 260, not dangerous). Combo with `thermalload` is the vacuum-cooling rule.
- **Tenet (Amendment I).** A buff. Enemy answer: hot hulls dive into denser air to cool (they descend within the Breathing Sky's reach), and the flagship's flak field kills Mirrors (hp 70) first, which makes them a target, not a free win. No nerf.
- **One-Button.** Fully automatic target choice.
- **Line.** Vertical; passes.

### Item #1 — SPACE-7: DOCK PARITY (a proven bug fix, and the job for space)
**Pitch.** My fleet is priced out of its own war by a discount bug. Fix the bug, then give space a station that actually pays.
**Clerk's Annex**
- **Mechanic.** Part A, a bug fix (Inspector's 3a): sky capital ships should discount by Sky Docks, not Airbases. Today `unitCost` (L3835) uses `facsFor(domain)`, so at 10 airbases a Sky Railgun Ship costs 820 x .22 = 180 IC. Fix: `unitCost` uses `facsForUnit(d)` (L9924). Part B: Space Stations discount space capital ships the same way (-9% per station, floor .22). Part C: Orion's deck repair and the Space Station's lift each count for 8 slots.
- **Proof for the nerf (Amendment I).** Report 8: 138 sky ships, ten-to-one damage; report 10 flawless; at that cost it makes the space fleet, strikes, infantry and navy pointless (Inspector 3a). A pricing bug is not a nerf of a toy; it restores intended costs. The player keeps the ship and the fix scales with the docks they build.
- **Code grounding.** `unitCost` L3835, `facsForUnit` L9924, `facsFor`, `orbCap` L3812.
- **Size.** S (about 30 lines).
- **TM1.**
- **Research.** None.
- **One-Button.** Pure pricing.
- **Line.** Passes.

### Item #10 — space's total death: balance or design?
**A missing design, not a balance problem.** Halo range 1,200 loses every opening shot to `RAILSAT_RANGE` 1,900 (L8524), but the grid sits in the mesosphere (L5644), where air and sky ships (`ceil:0`) already shoot it. Space has no job that air lacks. The fix is jobs, not stats: the vacuum heat hold (SPACE-6), the forward base (Orion), Dead Sky for the grid, and the Orbital Denial door (kills in the space band plus flagship sunk). Enemy answer: a space-first gambit that sends the grid ahead of the landing.

## 4. Blatantly overpowered today
**The Sky Capital Ship price bug** (SPACE-7 above), with the Inspector's evidence: 180 IC for 2,600 HP, armour 20, a 600 shield, range 1,700, no hangar slot (L3820 excludes `capital`). It meets the bar because reports 8 and 10 show it making every other option pointless. Fix: correct the discount. Enemy answer: the sky-navy axis (Autofleet). I do **not** call Falcons blatant.

## 5. Struck self-limits
Three come back, all as real costs rather than caps:
1. **SPACE-2 "no more than the units' own firing"**: restored (bonus deleted).
2. **SPACE-X "your own lift offline"**: restored, time-boxed to the belt above 50%.
3. **SPACE-3 "+1 IC/s per Power Plant"**: restored as one plant per Helios, linear.
