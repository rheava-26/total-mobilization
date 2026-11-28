# PSIONIC CONCLAVE (`psi`) — Second Reading

## 1. Speech

*(The Conclave stands. The Inspector-General, seated two rows behind, finds their notes wet.)*

The President says: no power creep, one button, the Line. We agree with all three, and we are the party the President nerfed. We remember. "You literally could not lose with them" is the sentence we carry.

So we ask the House for something unusual: *evidence against ourselves.* Report 6 is ours. It is the proof. Where it holds, we will hand over the nerf with our own fingers. Where it does not, we will not be bled by rumour.

To the Inspector-General: you will say our bills assume depth, or a click per cadre, or a hunting unit nobody can steer. We have read ourselves harder than you can. Three of our bills changed shape below. One is withdrawn.

To the President's question about the swarm and the tank: we have an answer, and it is not "more damage". It is dust in the intake.

*(The microphone is warm. It was warm before we arrived.)*

## 2. The Re-reading

**PSI-1 THE AWAKENING — AMEND.** *Amendment I:* it contains a real nerf (the strip of `ceil:0` from Psi-Troopers and Pylons) and a buff (stats). The nerf stands on report 6 (see section 4). The buff goes too far: drop Psi-Trooper `hp 220 / dmg 120 / ic 480` to `hp 140 / dmg 70 / ic 300 / mp 16`, so rarity comes from throughput and cost, not from a statline the enemy cannot answer. *One-Button:* originally "one Awakening per Relay at a time" read as a queue to tend. Fix: each Relay awakens its standing choice (Psi-Trooper by default, one setting made once) whenever IC and MP are free, like any autospawn building. Ten Relays are ten times the throughput. *Line:* pass, nothing is positional. The linear `-0.8` stability cap per trooper stays: a cost, not a cap, and the floor is a degraded state (v3.26), not defeat.

**PSI-2 THE THERMAL BUDGET — WITHDRAW as a standalone bill.** It is wholly absorbed into docket bill 1 (Thermal Field). The "dispersed swarms vent +50% faster" line is dead: on a one-dimensional front units overlap and cannot disperse. That clause assumed depth. Heat vents come from the Nanite Foundry aura only.

**PSI-3 THE LUMEN — AMEND.** *One-Button:* the perch, the escort, the fuel cell and "ride an Airship" all asked for per-unit steering. Rebuilt: the Lumen is an **air-domain** unit with existing fuel and return-to-base logic. It auto-climbs to firing altitude when it holds a fuel cell and a valid target, fires, and returns to cover (a Shield dome or a friendly stack of 3+) because of its paranoia. Fuel cells arrive automatically from each Uranium Enrichment building, so ten enrichers give ten times the cells. The Thunderhead Perch is deleted. *Amendment I enemy answer:* a Lumen is a buff, so the fleet answers: Reaver air hunters (docket bill 3) prioritise any lone high-value airborne target, and a Lumen has 35 HP, no armour and no shield. It is one hit from dead and the enemy is built to find it. *Line:* altitude is vertical. Pass.

**PSI-4 THE HUSH — AMEND (trim).** The Quiet boss is yielded to the Autofleet roster. The 25% lattice absorption is struck (docket ruling 9). What remains is Hush-Hound pickets, which spawn automatically at `G.psiN` thresholds 8, 24, 64 and halve psi fire rate and range inside 240 px. *Amendment I:* an enemy buff, and it touches only psi units, so the player's other armies are not made worthless. *One-Button:* the Relay aura restores range without any click. *Line:* a horizontal radius. Pass.

**PSI-X THE LISTENING — AMEND.** *One-Button:* "LISTEN costs 18 cap for 90 s" is an active click each wave. Fix: a single standing choice, the **Listening Watch** toggle, made once. While it is on, each Chamber listens automatically through the last 60 s of prep and costs cap only while listening. Each extra Chamber counts as one more forecast observer (sigma over root n), so ten Chambers are ten times the sight, with `listenDebt` rising per Chamber-hour. The TM2 "we stop if you stop" ending stays TM2.

**Docket 1, THE THERMAL FIELD (co-sponsor) — KEEP.** Heat is deposited by enemy sources only on friendly Shapeshifters; regen off above heat 60; speed -30% above 90 (ruling 6). Amendment I, power creep: it is a pure player buff against enemies, and the enemy's answer is that heat-armed fleet units now matter.

**Docket 2, THE NERVE (joined) — KEEP.** Add `+0.25 Fear per 8 psi units`: a swarm of individuals the fleet cannot count scares it. Visible on the Fear panel (Order Paper item 9).

**Docket 3, THE FLEET READS YOU (co-sponsor) — KEEP.** Our share is only the Hush-Hound clause.

**Docket 4, THE ONE FORECAST (co-sponsor) — AMEND.** Replace "Listening Chamber, an active 90-s peek" with the Listening Watch toggle above.

**Counts:** KEEP 3, AMEND 5, WITHDRAW 1.

## 3. The Order Paper

**#1.** Done in section 2: every sponsor decision is stated with its One-Button and Line check.

### #4 PSI-5 — THE INGRESS (Shapeshifters on the Line; Rifle Corps co-sponsors RIFLE-4)

**The President's question.** A swarm is strong against infantry and weak against a tank, because plasma at 75 damage meets armour 14 and loses. The answer is to stop trying to beat the armour.

**Pitch.** *"A tank does not die to a thing that enters it. It dies to a thing that gets into the tracks."*

**Real science.** Armoured vehicles are famously killed by fine dust. The M1 Abrams' AGT-1500 gas turbine ingests sand through its air filtration and fails if the filters clog (documented in desert operations); tracks and bearings seize on grit. A swarm of nanoscale machines is mechanically a *directed* version of the same ingress: it does not penetrate the hull, it enters the intake and the running gear. SCI-FI (internal logic): the makers' nanotech can sort itself into abrasive form and re-aggregate afterwards, consistent with canon that Shapeshifters are nanite-bodied and "turn into" what they need.

**Player fantasy.** You see a Mammoth stagger, slow, stop. A tide of your nanoswarm pours off it and moves to the next.

#### Clerk's Annex
- **Mechanic (one front, no depth needed).** A Shapeshifter in contact (within 40 px, melee) with a ground enemy of `armor >= 12` **latches** automatically. Latch slots per target equal its hull size `s`, about `s` shifters.
  - Each latched shifter adds `-12%` target speed (max `-85%`) and `-8%` fire rate (max `-60%`).
  - The latched stack deals `0.9% of target max HP/s` per shifter, ignoring armour, `0.2%` on bosses. At full slots a Goliath (`s` 7) dies in about 15 s, a Leviathan in about 3 minutes.
  - Latched shifters take no regen and are exposed: **while latched, heat from enemy splash hits every shifter on the stack.** Stacks sit on one point of the line, so a single Pelter plasma splash cooks the whole stack (heat deposit x1.4, radius 70). The Line becomes the enemy's gift.
  - **Wall (a mix, not a cap):** Shapeshifters are *useless against air and space*, and the Thermal Field's enemy lasers and Pelters punish clumps. One unit type still works; it just works slowly against anything that is not on the ground.
- **Enemy answer (Amendment I).** `counterBias('shifter')` raises Pelter and gunship weight x2 when the scan sees 30+ shifters. An enemy buff only against the unit that buffed.
- **Code.** `UNIT_DEFS.shifter` L3297, `shapeshifter` research L3531, `allRegen` L7873, `infFast` L7885, `UNIT_FAM` L3378, `GROUND_BASE_WEIGHTS` L5488, `counterBias` L5526, plus the Nanite Foundry smart-round latch for precedent.
- **Size.** M. **TM1.**
- **Tree.** `shapeshifter` (triple combo: psionics + biotech + nanotech), cost 360. Optional node `mimicry` (pre `shapeshifter`, cost 240, not `dang`) widens latch range to 80 px.
- **Tenet / Amendment I.** Buff, no nerf; the costs are enemy-delivered (splash heat) and structural (no regen while latched), not a rule cap.
- **One-Button.** Pass. Latch and release are automatic; ten times the shifters is ten times the stack.
- **Line.** Pass. Contact on one front is enough. Depth is not needed.
- **Interactions.** Rifle Corps (co-signs RIFLE-4 and takes the form-switch; the Ingress is the tank half), Armour Corps (a predator for the tank), Bioengineering (nanite body), Energy and Physics (heat is the counter).

### #10 PSI-6 — THE LOW CEILING (the explicit nerf, with proof)

See section 4. Bill text: strip `ceil:0` from `psitrooper` and `psibeam`; set `ceil .60` (atmosphere) and `.66`. The Lumen (PSI-3) is the only psi thing that reaches higher, and it is a one-hit glass cannon.

#### Clerk's Annex
- **Mechanic.** `psitrooper.ceil 0 -> .60`, `psibeam.ceil 0 -> .66`. No stat change. Interception of busters and rods continues (`canIntercept`, v3.88.0) within the new band.
- **Code.** L3295, L3296, `acquireEnemy` L4374, `acquireIncoming` L4465.
- **Size.** S. **TM1.** **Tree.** `psionics`, `telekinesis`. No new node.
- **Tenet / Amendment I.** An explicit nerf, justified by section 4's evidence.
- **One-Button.** Pass (no player action). **Line.** Altitude is vertical; pass.

## 4. Blatantly Overpowered Today

**Yes, exactly one thing: any-altitude reach on Psi-Troopers and Pylons (`ceil:0`, L3295 and L3296).**

Evidence (ROADMAP L585, report 6):
- 73% of kills were ground kills because psi units reach orbit for 150 IC. The altitude puzzle (the game's core air-defence puzzle) was switched off.
- Damage ratio 213:1, flawless, 1.28M IC unspent, stability at its floor of 65% untouched, no brake engaged.
- Psi-Trooper is `ic 150, hp 90`, spammable with no scarcity. It still holds `ceil: 0` in the code today (L3295).
- The v3.90.0 patch shipped only the Mind-Forge antimatter cost and the additive aura. The ROADMAP records the deeper dominance as "STILL OPEN".

That meets the bar: it makes every other altitude option pointless. The remedy is PSI-6, a numbers-free nerf of reach, and the scarcity of PSI-1.

**Not proven:** the global `allRegen` flag (L7873) from a 360-cost node. It is strong, but one match report that predates v3.91.1 (Shapeshifters on the army cap) and the heat gate does not meet the bar. I recommend leaving it and re-auditing after one match with the Thermal Field. If it still wins flawlessly, the answer is a scarier enemy: heat-armed fleet units, not a rules cap.

## 5. Struck Self-Limits

The Speaker struck the Shapeshifter damage slump (x0.5 damage and +40% damage taken above heat 90). Amendment I allows restoring a struck self-limit "only if it is the only thing stopping a blatant exploit." It is not. Regeneration is off above 60 and speed is -30% above 90, all driven by enemy-sourced heat, and PSI-5 adds stacked splash vulnerability. The exploit it was drafted against (immortal 77% army) is already closed by three other mechanisms. **Our position: do not restore it.** I withdraw the request. If a post-Thermal-Field match shows Shapeshifters still winning flawlessly, return with that report and I will argue the slump back in as an enemy-side effect (an enemy "Cinder" plasma weapon applying it), not a rule.
