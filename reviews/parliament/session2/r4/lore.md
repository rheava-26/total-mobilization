# Second Reading: Story & Lore Council (Office of the Chronicler), Society Bloc

## 1. Speech

*(The Chronicler stands. The tin helmet now has a small lamp strapped to it.)*

Mr Speaker. The President has told us the game is one button and a great many units, and that the ground is a line. The Chronicler has never been happier. A line is the oldest story shape there is: a front, and which way it moves.

I confess the First Session's sin. I proposed a boss that took away your techs. The President says a nerf needs a corpse on the table, and I had brought a ghost. I withdraw it, with a bow.

To the Deep Bloc: you have given me a cellar, and I have a use for it. The fleet can count your factories from orbit. It cannot count what is under the dirt, and *that* is the most frightening sentence in the war.

To the Inspector-General, whoever you are: audit me. Every line I wrote must work when the player only builds things. If it cannot, strike it.

## 2. The Re-reading

**LORE-1 The Charter, in docket bill 12 "Charter & Decrees": AMEND.** The dial is one standing choice per dangerous tech, default Standing, so it passes the One-Button Law. I strike my "random 6% dips" under Unleash: it was a surprise tax. Replace with a deterministic cost: each Unleash adds +0.1 to Fear (it is read by the fleet), which is the enemy's answer under Amendment I. Line: not applicable (a society setting).

**LORE-2 Fleet Resolve, in docket bill 2 "The Nerve": AMEND.** Keep. Add the explanation panel from LORE-7 below, and state the player is never asked to do anything: the only choice is accept or refuse a Truce, once. One-Button: pass. Line: pass (the flagship dies, the fleet breaks; no flank needed).

**LORE-3 Black Boxes, in docket bill 16 "The Dossier Line": KEEP.** Recovery by any unit standing on a wreck, or a Lab or Yard in range, is automatic. Amendment I: +10% damage vs a kind is a buff, and the named answer is the Sanitiser drone (wrecks vanish) and, in the second siege, a Mirror echo that leaves no wreck. Line: wrecks lie on the strip and air kills drop where they fall. Pass.

**LORE-4 Red Flag, in "The Exercise": KEEP.** Three drill waves, auto-run, banner at T-0. Veterans opt in. Line: the "flank" of a drill is the left and right ends of the strip, which exist. Pass.

**LORE-5 The Wire and Chronicle, in "The Wire as Home Front" and docket bill 4 "The One Forecast": KEEP.** Pure information; the player reads it or ignores it. Unconfirmed lines are wrong with probability `0.25 x (1 - conf)`; the tag shows when `conf` <0.7, so a false line is never a surprise.

**LORE-H The Invoice: WITHDRAW.** Revoking a researched node is a denial of the player's toys, and nothing in reports 6 to 10 proves research is blatantly overpowered. It fails Amendment I. I keep one line of it: the Chronicle may write "the patron did not call in the debt" as an epilogue. Nothing mechanical.

**Docket bill 17 "The Fleet Will Return" (co-sponsor): KEEP.** The Interbellum is a wait, so there is nothing to click. The grudge flag feeds my Chronicle.

**Count:** KEEP 5, AMEND 2, WITHDRAW 1.

## 3. The Order Paper

**#1** is answered above.

### LORE-6: THE MASTHEAD (#5: what a megaproject makes of you, in headlines and systems)

*Pitch.* Megaprojects.md has the Civic Block (`civ`: society, stabCap, look, fleetRead). I supply the *voice* and the fleet's answer. A project does not change you through a cutscene. It changes the headlines, the label under your name, and the thing the fleet decides to do about you.

**Clerk's Annex**
- **Mechanic.** Each finished project writes one **Civic Tag** to `G.civ[]` (Chthonic from the Redoubt, Thalassic from the Seabed Cable, Arcology, Aegis from the Dome, Orbital from the Ring, and so on). Each tag adds three Wire lines (start, midpoint, finish) built from system values, for example "Redoubt section 5 lit · shelter x0.52 · NO SURFACE SIGNAL". A **Civic Reading** is the tag with the most sections finished. The fleet reads it (via `G.enemyIntel`) and spends its next Weakest-Axis event on the counter: Chthonic gets Burrowers, Thalassic gets Trench Landers, Arcology gets a decapitation raid at the core, Aegis gets shield-breaker pods, Orbital gets a bombarder wedge. Three finished tags of one family rename your society for the Chronicle ("a Nocturnal Republic") and carry no extra power beyond each project's own `civ.stabCap`.
- **Real science.** Derinkuyu, Cappadocia: a real underground city of about 18 levels and 60 m, with ventilation shafts and rolling stone doors, held up to 20,000 people. Cheyenne Mountain and the Soviet closed cities made states in miniature. A civilization takes the shape of the thing it builds to survive.
- **Code grounding.** `society()` L3549, `banner` L10335, `end()` L10345 (Chronicle), `scanEnemyIntel` L5737, `G.enemyIntel` L5773, `spawnSuper` L5269, `cityStabPenalty` L4547.
- **Size.** M (about 220 lines on top of MEGA-5). **TM1** (the full society axis is TM2).
- **Research.** None of its own; each tag arrives with its project's node (`deepmining`, `deepworks`, `orbitalring`, `arcology`, `shieldtech`).
- **Amendment I.** A flavour-and-answer bill: it adds the enemy's counter for every tag, so it is not power creep.
- **One-Button.** Pass: tags and lines are automatic. **Line.** Pass: nothing is positioned.

### LORE-7: THE ASSAY (#9: why building makes the fleet bigger, shown on screen)

*Pitch.* The President loves the Fear meter and wants to see it and understand it. Here is the in-universe reason. The fleet is the thrown friend: one ship, one catapult charge, a **finite reserve** in its hold (canon, Article V). It cannot send for more. So the only decision it makes is *how much of the reserve to commit now*. It measures Earth's **assay**: how fast Earth could replace losses. Lanchester's square law says a defender with industrial depth wins attrition against a magazine-limited attacker, so the better Earth's industry, the sooner the fleet must spend its reserve, before the planet converts mass into war. The fleet does not grow because you build; it *releases* more of what it was always carrying, because waiting only makes the bill larger.

**Clerk's Annex**
- **Mechanic.** HUD pill **FLEET ASSAY** beside the readiness pill, with a hover receipt of four lines, each from a live value:
  1. "Industry read: 850 IC/s, 2.8x landing baseline" (`icRate/300`).
  2. "Standing works: 61 tier-points" (`bldgTier/120`).
  3. "Army weight: 140" (`armyWeight/200`).
  4. "Reserve committed: 34% (+22 hostiles)" with the numbers `F` and `round(F*12*diff)` from AUTOFLEET-2.
  A fifth line is the player's side of the trade: "Your +400 IC/s buys about 160 units/min; it costs +22 hostiles." That line is what keeps building the winning move: the unit gain always exceeds the added hostiles (at `F` max 4, +48 hostiles at diff 1, against 850 IC/s = 340 units/min).
  The pill shows bands at tier 0 and exact numbers once `signals` is researched; Maskirovka spoofing shifts the *shown* belief and a small tick shows the truth (docket bill 2).
- **Real science.** Lanchester's square law; waste-heat technosignatures (Earth's roughly 18 TW infrared excess); the 1930s British "air-defence assay" that measured German production, not stock, to predict raid size. SCI-FI: the fleet's one-year catapult charge (canon) is why its reserve is fixed.
- **Code grounding.** `syncHUD` L11133, `spawnEnemy` L5582 and `doWave` L5533 (the `t` scale), `ESC.OPEN_BOMBARDERS` L5160, `scanEnemyIntel` L5737, `buildSupersPlan` L5301.
- **Size.** S-M (about 150 lines; the HUD is the cost, the maths exists in AUTOFLEET-2). **TM1.**
- **Research.** `signals` (def, pre `pointdefense`, ~90) for exact numbers.
- **Amendment I.** No nerf. It explains an enemy buff and proves the player's trade is positive. **One-Button.** Pass: a readout. **Line.** Not applicable.

### LORE-8: THE UNACCOUNTED (#6: the underground, in canon)

*Pitch.* The President says the underground is underused relative to its role in the original book. I take the canon reading: the fleet's whole technology looks *down* from orbit. Thermal infrared sees surface heat. Radar sees the top of the ground. The fleet cannot count what is under 50 px of dirt, and a fleet that cannot count is a fleet that is afraid. The Deep Bloc has built the place. I write what the *fleet* thinks of it.

**Clerk's Annex**
- **Mechanic.** The Assay (LORE-7) reports **UNACCOUNTED: n%**: the share of your IC/s and army weight that sits in `ug` buildings (Deep or Abyssal). The fleet counts unaccounted mass at 50% in Fear (the Dispersal rule) and its gambit choice from `enemyIntel` ignores it, so a mostly buried player faces a fleet that over-prepares for the surface army. Resolve falls 0.04 per second while Unaccounted >=50% and invTime >=120 ("the fleet is afraid of what it cannot count"). The enemy's answer: after Resolve <60 the fleet drops a **Plumb** (500 HP, sensor pod, 600 px) that makes buried works within 600 px "counted" again for 60 s, so the Deep Penetrator (SUB-1) can see them. Kill the Plumb and the gain returns.
- **Real science.** Muon tomography: cosmic-ray muons passing through rock reveal voids (ScanPyramids, 2017). Surface-penetrating radar and gravimetry can map a void, but need a sensor near the ground. SCI-FI: the fleet's orbital sensors lack both, because the thrown friend was built to look at a *surface*.
- **Code grounding.** `ugAllowed` L3862, `placeBuilding` L3888, `shallowUgHit` L7543, `scanEnemyIntel` L5737, `spawnSuper` L5269 (the Plumb is a small boss), `end()` L10345.
- **Size.** M (about 200 lines; the Plumb is a copy of a pod unit). **TM1.**
- **Research.** Attaches to Subterranean's `deepmining` (cost 200, pre `automation`); combo with `signals` (Assay exactness).
- **Amendment I.** A player buff (Fear discount) with a named enemy answer (Plumb). No nerf. **One-Button.** Pass: automatic from the depth the player already chose. **Line.** Pass: vertical axis.

## 4. Blatantly overpowered today

Nothing in my portfolio. The achievements, event engine and AAR do not touch combat, and reports 6 to 10 record no loss prevented by any of them. I will not propose a nerf without a corpse.

## 5. Struck self-limits

I struck no limit of mine as a nerf in the First Session. I did strike one of the Stability Council's: the "second identical Maskirovka campaign gets half effect". Under Amendment I, a spoof that zeroes Fear forever *would* be a blatant exploit, so I support restoring it as the **enemy doubting** (the fleet recognises the third identical lie and carpet-fires blind), not as a cut to the player's tool. The Autofleet filed the same fix.
