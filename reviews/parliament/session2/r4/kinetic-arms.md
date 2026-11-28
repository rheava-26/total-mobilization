# KINETIC ARMS COUNCIL — Second Reading (Round 4)
*The Ordnance Board. Slug: `kinetic-arms`. Order Paper items #1, #2, #10. Read: the Address, Energy's and the Artillery Corps' second readings, CLAUDE.md tenet clarification, `hurt()` and `STRAT`.*

## 1. Speech

Mr Speaker, the Chief of Ordnance has been audited by the President, and the Chief of Ordnance would like to confess before the Inspector-General does it for him.

I told this House that armour-piercing sabot would "subtract 0.35 of the armour." I did the sum. In `hurt()` (L4980-4985) a hit is multiplied by 1.3 and by 1.9 *before* armour is taken off, and the armour is then scaled by 0.55. My great penetration model was worth about eight points on a hundred. It was a decoration. I strike it, and I thank the Institute of Directed Energy for noticing first.

What a shell needs is a *reason to be fired*, and the President has handed me one: heat makes the hull brittle, and the gun is the finisher. The laser opens the door; the cannon walks through. I will build that door-frame, and I will bolt a lock on it so it cannot swing open on the whole game.

Carried by the breech, and checked by the Inspector.

## 2. The Re-reading

**KINETIC-1 Fuse and Fragment (airburst flak): KEEP.** Real basis stands (VT fuze, AHEAD/3P). It scales with volume: build more flak, more clouds. *Amendment I:* it is a buff, so the enemy answer is stated: swarms already vary in density through `colMul` landings, and when `scanEnemyIntel` (L5524) reads `dominant==='flak'` the fleet sends more spaced, shielded and heavier wings; Adaptive Hull (Energy) hardens medium hulls against the kinetic family, so flak alone stops being the whole answer. *One-Button:* PASS. The fuse range is computed from the target, with no setting. *Line:* PASS. A burst on a stacked column of drones is exactly what a line gives; bursts are vertical-friendly, since air bands are the extra dimension.

**KINETIC-2 Sabot and Shell / THE ONE LOCKER (docket 7): AMEND.** Three changes. (a) AP is redefined, as Energy also found: **+40% damage against medium-or-heavier targets**, with no armour term. (b) A per-group selector is attention per group, which Article IX forbids; it becomes **one standing choice per gun family, plus `AUTO`** (AP against heavy-class, HE-frag against the rest, canister inside 140 px). `AUTO` is the default and the player may never touch it. (c) AP's alloy cost remains, drawn from INDUSTRY-1's `munitions` stock, and an empty stock reverts to HE-frag at full stats. *One-Button:* PASS with the amendment. *Line:* PASS; canister's 40-degree cone is a horizontal cone along the line.

**KINETIC-3 Spool-Up: KEEP.** One float per gun, no attention, scales with volume. A buff, and the enemy answer is that the extra rate is spent on larger waves, not a stronger rule. *One-Button:* PASS. *Line:* PASS.

**KINETIC-4 Fire for Effect: WITHDRAW** into the Fire Direction Net (below).

**KINETIC-X SHARP / Babylon: WITHDRAW** (already folded into the Long Gun Coalition in Round 3).

**FIRE DIRECTION NET (Ground, M1; docket coalition): AMEND.** As written the barrage is a click per barrage. Amend: the Fire Direction Centre **fires for effect on its own** at the densest cluster of medium-or-heavier hostiles within its link, on an 8 s cooldown, with one standing toggle (`AUTO` / `HOLD`). The player may still paint a point by hand, optional, as with a strike. The Artillery Corps' shell blast (r 36 for the Thunder, ARTILLERY-1) is the damage layer I depend on; mine is only the solver and the linking building, so we do not duplicate: *they own the burst, I own the timing.* *Line:* PASS; ToT lands one volley on a stack.

**THE LONG GUN COALITION (Railgun M1 / One Gun): AMEND.** The Address notes the gun ended with no section limit. No cap is needed unless proof exists, and the cost already scales (upkeep 6 IC/s per section, 40 IC per section per shot, 1.5 s slew per section). I add only one-button hygiene: the gun **auto-targets the highest-threat boss** within its arcs, with one `AUTO`/`HOLD` toggle, so no click is needed per shot. Light-gas stage rounds use the shared selector (`AUTO`). *Line:* PASS, it fires up and along the line.

**ONE NET (Railgun M3): AMEND (unchanged).** The Engagement Net is the Fire Direction Net, one object.

**GUN VOICE (Game-feel B): KEEP.** Voice budget counts a burst as one voice.

**Tally:** KEEP 3 (KINETIC-1, KINETIC-3, Gun Voice), AMEND 4 (KINETIC-2 / One Locker, Fire Direction Net, Long Gun Coalition, One Net), WITHDRAW 2 (KINETIC-4, KINETIC-X).

## 3. The Order Paper

### Item 2: KINETIC-5 — THE FINISHER (the gun's half of heat-as-variety)

**Pitch.** *Lasers do not kill a capital ship; they make it brittle. A shell finishes it. Ship both, or spam one and wait.*

**Clerk's Annex**
- **Mechanic.** Energy's Thermal Stress multiplies kinetic damage taken by `1 + B·heat/100` (B: light 0.25, medium 0.8, heavy 1.5, capital 2.0). The gun adds only **Spall Fracture**: when a kinetic hit (shell, rail, rod, tracer, rotary) lands on a medium-or-heavier hull at **heat ≥ 70**, it sheds spall: `splashDamage(x, y, r=28, dmg·0.25)` on neighbours (the stacked hulls beside it on the line). Spall is rate-limited to once per 0.4 s per target hull, never spawns spall, and never fires on light chaff. A heated formation, such as a wing flown through a Plasmoid burst, therefore loses a *column* to one shell.
- **Stacking audit (the guard Energy asked for).** Every bonus to one hit is applied from **one multiplier computed once**: `kinMul = AP(1.4 if medium+) × Stress(1+B·h) × Hull(1−0.75a²)`, then spall at a fixed 25%. Worked capital hull (heat 100, B 2.0): fresh hull 1.4 × 3.0 = **4.2**; mixed army (kinetic meter a≈0.5, factor ≈0.81) = **3.4**; kinetic-only army (a≈0.83, factor ≈0.48, and it never reaches heat 100 without lasers) = **0.67 to 1.4**. Spall adds 25% of the hit to *neighbours only*, not to the target. The worst product is therefore bounded by three enemy-owned terms (heat deposit divided by thermal mass, cooling, hull adaptation), none of them a player-facing cap. The Quench Tender halves deposit, so Stress ×3.0 is the ceiling, not the norm. If headless testing shows the product trivialising a boss, the fix is a heavier enemy cooling rate, not a rule on the player.
- **Code grounding.** `hurt()` L4971-4992 (single multiplier after `eff`), `fire()` L4855 / `updProj` hit branch L9159-9167 (spall hook beside `boomW`), `splashDamage` L7553, `spawnEnemy` L5581 (mass class, shared with Energy), rate-limit field `t._spall`.
- **Size.** S (~90 lines, on top of Energy's Thermal Field).
- **TM1.**
- **Research.** None of its own; it needs the `thermalload` node (branch energy, tier 2, pre `directedenergy`). Combo: `apds` Sabot (the AP bonus) and `particlebeam`.
- **Tenet (Amendment I).** A buff, so the enemy answer is named: Quench Tender (halves deposit), Adaptive Hull (kinetic meter), shield domes soaking before heat. The fleet's heavies get *slower to kill*, so the player's existing mixed army is rewarded rather than made redundant.
- **One-Button.** PASS. Spall is automatic; the player builds a mix.
- **Line.** PASS, and it is the best case: spall radius is horizontal and a stacked column is the whole point.
- **Science.** Brittle fracture and spallation: a shock through a plate sheds a cloud of scab fragments from its far face, and a thermally stressed plate fractures at lower strain. Real.

### Item 10 bill: KINETIC-6 — THE ROD WATCH (instrument the Lance, pre-agree the answer)

**Pitch.** *I will not nerf a rod on a hunch. I will weigh it, and I will tell the enemy what to do if it weighs too much.*

**Clerk's Annex**
- **Mechanic.** Add a per-strike stat to `G.stats`: `kills`, `hullDamage`, and `ic` per Lance. Pre-agreed trigger: if the **median Lance kills more than 12 hulls or 4,000 HP per strike across three headless General runs**, the answer is *not* a nerf but a scarier enemy: columns spread (`colMul` +40% spacing) whenever `scanEnemyIntel` reads `dominant==='rods'`, and Bombarder point-defence already prioritises rods (L4453). A nerf is allowed only if the trigger holds *and* the enemy answer fails a second run.
- **Code grounding.** `STRAT.kinetic` L6888, `detonateKinetic` / `areaDamage` (118 px, 620 dmg), `stratStab` L6896, `G.stats` in the AAR. **Size.** S. **TM1.** **Research.** None (existing `kinetics`/Tungsten Rods). **Tenet.** Instrumentation and a pre-agreed enemy response, nothing capped. **One-Button.** PASS. **Line.** PASS.

## 4. Blatantly Overpowered Today

**The Kinetic Lance: not blatant on the evidence so far.** What the code says: `detonateKinetic` deals 620 at radius 118 (full to 50 px, linear falloff) for 40 IC (`STRAT.kinetic` L6888), and `stratStab` charges 0 because it is not `taboo`. A landed `gtank` has roughly 210 HP, so one rod can clear a column. But the evidence the Amendment demands is missing:
- No match report records rods as the winning doctrine. Reports 8 and 10 were won with zero strikes; Reports 6-7's stability crisis came from 44-46 strikes that mostly included nukes.
- It needs a Tungsten Rods satellite (`unitReq: 'rods'`, 40 HP), and Article VI records that space units are being destroyed wholesale by the bombarder grids (42 of 44 Halos, 18 of 18 Volleys in Report 7).
- Every friendly rod carries `STRAT_MSL_HP = 90` and is shot at first by enemy point defence (L4453).

So we propose **no nerf**; KINETIC-6 collects the proof, and names the enemy answer in advance. If the House insists on a nerf now, the Board's only acceptable form is a real cost, not a cap: each Lance deletes 1 rocket-part from the satellite's lift stock, never reduces its damage.

## 5. Struck Self-Limits

I struck none of my own limits in the First Session, and I do not restore any. Two co-signed strikings: (a) the Railgun recoil-cradle penalty, which Round 3 struck; I leave it struck, since a missing cradle does not make losing impossible, and the shot cost is already a real brake. (b) The joint 50% armour cap accepted in Round 3 is moot and withdrawn, as Energy says. KINETIC-2's alloy cost on AP rounds stays as a *real cost* under Amendment I.
