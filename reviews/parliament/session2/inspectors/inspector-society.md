# Inspector-General of the Society Bloc: the Censor of the Republic (`inspector-society`)

## 1. Opening speech

*(The Censor sets down a ledger the size of a paving slab.)*

Honourable members, I have watched four utopias rot, and each began with a committee that said "it only costs stability." Stability, in this game, floors at 20 and the crisis fires at 0.5, so "it costs stability" means "it costs nothing, plus a speech." The Society Bloc is the party of prices. Charters, Decrees, Ledgers: you love a dial. I love a dial that bites.

I also hold the receipts for the one council that cheered itself for nerfing its own shapeshifters, and then wrote them a research node that heals every unit in the army, Nanofortresses included, at 4.5% a second. I will be fair. Ten bills of ours are clean and I will say so. The rest of you may now enjoy the sensation of being read by someone who greps.

*Method: every number below was checked in `title.html` this session. Where a council's claim failed, I say FALSE.*

## 2. The charge sheet

### Stability Ministry
- **STABILITY-1 Casualty Ledger: CHARGED (MONOCULTURE, FALSE).** The exemption list (`d.drone, elec, mech, clone`) makes drone and clone losses free, which pushes players toward drone mass. It leans on `isMechanical` (L4541), whose own comment says it is "NO LONGER CALLED". *Fix:* write an explicit predicate. Weight losses by `defWeight`, so a lost drone (0.35) counts as little as it is worth.
- **STABILITY-2 Maskirovka Bureau: CHARGED (EXPLOIT, MICRO).** Three "identical lies" trigger doubt, so rotating A, B, C never trips it. At 45 s on and 90 s cooldown, one Bureau keeps `enemyIntel` (L5744) spoofed half the time, which switches off Fleet Reads You for the five-win air doctrine. The re-cast every 90 s is a chore. *Fix:* doubt counts any campaign inside 180 s, with a spawn-weight surcharge on the dominant kind. Add a standing "keep running" toggle.
- **STABILITY-3 State of Emergency: CLEAN.** Auto-picks Coalition on timeout, so it needs no clicks. Each option has a price and a Line-neutral effect. Coalition's +6 inf/s lasts about five seconds.
- **STABILITY-4 Information Policy Dial: CHARGED (EXPLOIT).** Free Press gives +15% influence and an earlier forecast, and its cost (doubled strike and city-loss hits) is zero for the no-strike players of reports 8 and 10. It strictly dominates Managed for the winning doctrine. *Fix:* Free Press also speeds the fleet's adaptation scan, so it has an enemy answer.
- **DECREES, hot take: CHARGED (EXPLOIT, MICRO).** Haste halves production time for 12–21% stability, and regen is 0.5/s, so 60% uptime is sustainable. No-strike players hold full stability, so it is free. It also needs a manual click per ~100 s. *Fix:* make decrees an auto standing order ("Haste while stab above X"). Haste then also bills IC at the standing-upkeep rate.

### Science Academy
- **SCIENCE-1 Wreck Lab: CLEAN** as superseded. Its charges are filed under the Dossier Line.
- **SCIENCE-2 Synthesis Doctrines: CHARGED (EXPLOIT, PERF).** Phased Arrays gives +3% per emitter within 350 px, uncapped. That is quadratic: 50 emitters on a Line stack make each hit +147%. Stigmergy doubles swarm bodies (nMax 48 to 96). *Fix:* Weasel flights and `busterScore` (L7453) target emitter clusters. Prove perf at 96 bodies.
- **SCIENCE-3 Containment: CLEAN.** The distance factor is a one-time placement choice, and a destroyed lab is an enemy lever.
- **SCIENCE-4 Crash Programs: CLEAN.** Influence income is still the brake, and parallel slots only spend it faster.
- **SCIENCE-5 Geoscience: CHARGED (CREEP).** Ionoheat drags space-band enemies down 6 px/s per heater, stacking without limit, at 2 IC/s each. Twenty heaters pull every Annihilator and Bombarder into SAM reach, so the fleet's high ground is gone and the space fleet is moot again. The "fleet should boost back" is only invited, never written. *Fix:* anchored space units re-boost at a rate that scales with heaters (an enemy answer), and bosses are exempt, as the Breathing Sky already says.
- **SCIENCE-X Return Address: CHARGED (FALSE).** The `warp` sample drops from the flagship, but under the Nerve, killing the flagship ends the war by Rout before a 600-s charge can run. *Fix:* the sample drops at a flagship's first Rethink, not its death.

### Chronicler of Lore
- **LORE-1 Charter: CHARGED (EXPLOIT, FALSE).** It claims ten dangerous nodes, but the live tree has nine (`hivemind` and `voidengine` were cut). Their `sc` sum is 72, and `dangCap` pulls the cap from 65 to the floor of 20 after about 45. Unleash's ×1.8 `sc` is therefore free late in the tree. *Fix:* past the floor, extra `sc` becomes `stabOffset`, which is unfloored in `effStab` (L4548) and bites.
- **LORE-2 Fleet Resolve: CHARGED (CREEP).** The Truce gate is `dangCap ≥ 20`, which two nodes meet (antimatter 10 plus psionics 10). That is nowhere near Article V's "extreme drift". *Fix:* `dangCap ≥ 45` plus a society state.
- **LORE-3 Black Boxes: CHARGED.** See the Dossier Line.
- **LORE-4 Red Flag: CLEAN.** Veteran opt-in, default off, and the drills are one-shot.
- **LORE-5 The Wire: CLEAN.** False lines are tagged "unconfirmed", and intent labels never lie.
- **LORE-H The Invoice: CHARGED (TENET, CREEP).** Winning too well replaces victory with an Auditor whose beam sets a researched node to "revoked" and makes its units inert. That is a denial of toys, and it punishes the win. It is already not carried. *Fix:* keep it dead.

### Psionic Conclave
- **PSI-1 The Awakening: CHARGED (FALSE, EXPLOIT).** "Needs escort above .60 restores the strato puzzle" is false. The stratosphere is .76, below .60 in height terms, so psi still hits it. The stability price (−0.8 per trooper) hits the floor at about 56 troopers, then is free; 400 troopers cost exactly the same stability as 56. *Fix:* IC upkeep of 0.3/s per trooper, which is 120 IC/s at 400. Keep the throughput limit at the Relay.
- **PSI-2 The Thermal Budget: CHARGED (LINE, TENET, FALSE).** See §3, Shapeshifters on the ground. The melee collapse (range 560 to 140) is a nerf nobody proved. The "disperse +50% vent" needs depth that does not exist.
- **PSI-3 The Lumen: CHARGED (EXPLOIT).** Mounted on a Nanofortress it is a 900-damage exosphere beam every 4 s, stacked onto the best-per-IC platform in the game (3,700 hp for 700 IC), and it puts reach to orbit on the sky navy that already dominates. Uranium per beam needs a feed. *Fix:* the Lumen may only be carried by a hull with a Relay aura aboard, and the Quiet (PSI-4) spawns per Lumen fielded.
- **PSI-4 The Hush: CLEAN.** The Hush-Hound (60 hp) and the Quiet are killable enemy answers, which is the tenet's preferred arm. *Note:* scale their number with `psiN`, not tier.
- **PSI-X The Listening: CLEAN.** It costs cap and the fleet listens back.

### Bioengineering
- **BIO-1 Hyphal Net: CHARGED (PERF).** The 30% damage share fans out to six other buildings per hit. At thousands of hits per second that is a graph walk per hit. *Fix:* pool net HP into one number per net, ticked once a second.
- **BIO-2 Blastema, as the Living Battalion amends it: CLEAN.** Regrowth is capped at founding strength. *Note:* Cadres are bio-family, so each body regrows for 1 MP instead of 35 IC.
- **BIO-3 Radiotroph Gardens: CHARGED (EXPLOIT).** A garden pays +0.6 IC/s and +0.15 inf/s per fallout chunk, uncapped. Strikes at your own side are the intended play, and Uranium cuts strike stability by 40%. Nuke yourself and influence, the game's deliberate brake, is bypassed. *Fix:* only enemy-origin fallout pays influence.
- **BIO-4 Directed Evolution: CHARGED (OP).** "+4% damage and −3% damage taken per 25 adaptation, uncapped and diminishing nothing." Taken-reduction reaches 100% at 34 steps (850 kills of one kind), and goes negative after that. Reports 8 and 10 had far more kills than that, so drones go to zero damage. *Fix:* the multiplier is 0.97^n, never linear. Past 100 adaptation the variant takes at least half the kind's spawns, not +10%.
- **BIO-5 Overgrowth: CLEAN.** It is a one-dimensional wall, which is the Line done right. The +2 stability is trivial.
- **BIO-X Immortal Line: CHARGED (EXPLOIT, MONOCULTURE).** HP is `base×(1+0.02×biomass)`, but weight stays `wt: 6`. An Ancient at 800 biomass is about 17× HP for 6 weight, which bypasses the army weight budget (Art. IV.1 in spirit), and kills snowball it (+0.5 per kill). *Fix:* weight rises with stage (a cost, not a cap).

### Docket coalition bills co-sponsored by the Bloc
- **1 Thermal Field (psi): CHARGED (MONOCULTURE, TENET, FALSE).** The "armour −50%" claim is nearly empty. `hurt` (L4987–4991) subtracts `armor × 0.55` flat after a ×1.9–3.5 hit buff, so halving a boss's armour 22 is worth about 6 damage on a 100-damage hit. The only real lever is ×1.5 kinetic, which does not force a mixed army. Replacing the global `allRegen` with a nano-only gate removes regen from every non-shifter, which is an unproven nerf. *Fix:* give the enemy a "cold plate" kind (kinetic resist at heat 0) so the wall is real. Gate regen at heat 60 for every unit that has it, with enemy-only heat.
- **2 The Nerve (lore, stability): CHARGED (CREEP).** Fear scales the fleet with IC, but no elasticity is stated. *Fix:* state fleet-size elasticity to IC/s of at most 0.5, so building more is always net positive (Order Paper 9). Truce gate per LORE-2 above.
- **3 The Fleet Reads You (stability, psi): CHARGED.** Maskirovka holes as above.
- **4 One Forecast: CLEAN.** `±40%/√(1+n)` floored at ±5% rewards volume. Lines are honest and tagged.
- **5 Standing Plan: CLEAN** from this bloc's side. `RAISE bn` shares one grammar.
- **6 Dead Sky Protocol: CLEAN.** The ladder carries real self-harm: EMP on own unhardened drones and sats, and Kessler burns both sides.
- **8 The Trunk: CLEAN** for this bloc's slice (Hyphal bridging).
- **9 Deep Deposits: CHARGED (EXPLOIT).** The Radiotroph loop above plus Uranium's 40% strike discount.
- **10 The Long Gun: CLEAN** for this bloc.
- **12 Charter & Decrees: CHARGED.** The floor arithmetic, free Decrees and manual clicks, as above. Continuity's free taboo strike in a crisis should also bill mutiny at 60%.
- **15 Power Grid: CHARGED (MONOCULTURE).** It carries Phased Arrays' uncapped quadratic stack.
- **16 Dossier Line: CHARGED (PERF, EXPLOIT).** One wreck per kill at thousands of kills a match. The stack against the commonest kind: dossier ×1.1 × family ×1.08 × adaptation × Thermal ×1.5 is about 3× before BIO-4. *Fix:* one `G.codex[kind]` record, not per-wreck objects, and a 20-s wreck TTL.
- **17 The Fleet Will Return: CHARGED (EXPLOIT).** The Interbellum "stability reset" launders 40+ strikes (reports 6–7). *Fix:* reset to the current cap, not 100.
- **Living Battalion: CLEAN.** The regrowth guard is correct.
- **Synthesis Doctrines: CHARGED.** The framework is fine; Phased Arrays and Stigmergy are charged under SCIENCE-2.
- **Breathing Sky: CHARGED.** Same uncapped drag as Ionoheat.
- **Wire as Home Front: CLEAN.**
- **The Exercise: CLEAN.**

**Count: CLEAN 18, CHARGED 28 (of 46).**

## 3. Blatantly overpowered today

*(Live code. Amendment I's bar: it makes losing impossible or every other choice pointless.)*

1. **`allRegen`: every unit heals 4.5%/s once Shapeshifters research is done (L3531, L7873).** A 3,700-hp Nanofortress heals about 166 hp/s free; a 260-hp shifter heals 11.7. It is hidden in a psi capstone. *Verdict: enemy answer, not a nerf.* Gate it behind enemy-sourced heat, for every unit alike. No match since v3.91.1 proves more, and the ROADMAP's "77%, none died" report (the Constitution does not cite it) predates the cap fix and the Mind-Forge cost.
2. **Superhuman Cadre: 70 IC, 4 MP, 220 hp + 110 shield, 40 dps at 600 range, speed 460.** Static value per IC is 189, against Titan 100 and Shapeshifter 33. The "−3% cap each" floors at 15 cadres, and the −2 inf/s in CLAUDE.md is gone (L6366 comment). So it is free past 15. *Verdict: a real cost, not a nerf:* 0.5 IC/s per cadre (150 IC/s at 300, about 18% of 850). The enemy answer is a missile wing, because only missiles overload the shield (L4983).
3. **Psi-Trooper and Telekinetic Pylon, `ceil: 0` (L3295–3296).** 400 troopers for 60k IC (70 s of 850 IC/s) is 8,000 dps at any altitude, which kills the grid the Space Fleet loses 42 of 44 to. This meets the bar narrowly. *Verdict: enemy answer first* (Hush, Quiet), reach cut only if the answer proves insufficient.
4. **Not OP:** Titans (760 IC, weight 6), Clones (28 IC chaff), Cloning (+60% MP, +10 cap), and Hive Mind +30 cap (cut at L3530; CLAUDE.md is stale). *Aside for the Sky Inspector:* Nanofortress per-IC value is 1,163, six times a Cadre.

### The President's question: Shapeshifters on the ground

*Live code has no rock-paper-scissors.* A shifter (300 IC, 260 hp, armor 14, plasma 75 / 1.98 s, range 560, `ceil .62`) shoots tanks and air alike. Enemy `gtank` is armor 6 and `gmech` 8, and their cannon fires every 4.84 s for about 99 effective damage, 20 dps against 11.7 hp/s of regen. A tank is weak against a shifter, the opposite of the President's premise.

PSI-2 does not fix that. It makes shifters take 85% from gunfire and heat only 0.02/hp from cannon, so a tank never overheats one (0.14 shot/s adds under 1 heat/s against a vent of −2/s). Its 140-px melee range also kills the unit against everything that outranges it: the pelter (plasma artillery), the bulwark and all air.

What works on the Line: keep range 560 and `ceil .62`. The natural predator is *enemy plasma and artillery* (the pelter's 95-damage plasma deposits about 33 heat a hit at 0.35/hp, so two hits switch off regen), so the Fleet Reads You's pelter weights should rise against shifter mass. Dispersal as written needs depth; on one front it is a chore. Bio and Psi also both rewrite L7873 (`allRegen`): Bio keeps it as a capstone, Psi deletes it. They must settle one gate.

## 4. Struck self-limits that should return

- **Psi slump above heat 90.** *Restore the +40% damage taken and speed ×0.7. Keep damage ×0.5 struck.* Without a bite above 90, heat only switches off regen, and a shifter without regen is a mid unit (value 33). The +40% mirrors the Thermal Field's own ×1.5 on hot enemies, so it is a consistency rule, not a special nerf, and the enemy deposits the heat. It does not meet Amendment I's nerf bar alone, so it rides inside the Thermal Field.
- **The Psi scarcity "stability price" (struck as a pool).** The replacement is free past 56 units. Restore a real cost as IC upkeep, not a cap.
- **Maskirovka's half-effect on repeats** stays struck. Doubt must also catch the rotating lie.
- **PSI-4's Lattice absorb** stays struck.

## 5. Questions for Question Time

1. **Psi:** your slump kills damage ×0.5, melee 140 and a 85% gunfire resist. Which enemy unit, by name, makes a shifter lose? Show the heat arithmetic against a gtank and against a pelter.
2. **Stability:** what stops a player rotating three Maskirovka lies A, B, C forever, and what is Free Press's cost to a player who never fires a strike?
3. **Bio:** BIO-4's linear −3% per step passes 100% at 850 kills. Show the formula that does not, and the variant's spawn share.
4. **Bio:** an Ancient Titan at 800 biomass is ×17 HP for weight 6. What is its weight at each stage?
5. **Lore:** the Truce opens at `dangCap ≥ 20`, two nodes. What is the "extreme drift" threshold, and does the Auditor's research reversal ever make a unit inert?
6. **Science:** under the Nerve's Rout, when does the Return Address ever get its flagship sample?
