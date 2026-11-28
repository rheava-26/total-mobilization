# Inspector-General of the Economy Bloc — the Auditor-General of the Treasury (`inspector-economy`)

## 1. Opening speech

Honourable members, I have audited the Ministry of War Supply of eleven nations. I found the missing money in every one. In this game I found it again: **212,000 IC, in plain sight, at 850 IC/s, with nothing to spend it on.** Four councils of the Economy Bloc walked past it and wrote me bills about spending it. Most of those bills sink the IC at a rate the code will not allow.

Let me save you the ledgers. Units are bound by the army cap (`armyCap`, L3803, `queueUnit` L4625). Buildings cost influence (`infCostOf`, L3877). Every "real cost (upkeep)" in this House is clamped by `IC_FLOOR` (L6254) and therefore costs the rich player nothing. The Market calls this a "treasury that rots". I call it a till with no customers.

I belong to the party. I will convict my own bloc first, and I will say CLEAN when a bill is clean. That happens about as often as a balanced budget.

## 2. The charge sheet

**What I verified.** Every claim below is grepped in `title.html` (line numbers are the current file) or computed from the bill text.

### Docket bills

**D5. The Standing Plan** (command, industry, market; Air, Bio and Missiles joined). **CHARGED: FALSE, MONOCULTURE, TENET, MICRO.**
- **FALSE: "ends the 212k hoard".** Unit lines die at the cap: `combatWeight()+queuedWeight()+grp*defWeight > armyCap()` (L4625). `ARMY_WEIGHT_MULT = 6` (L3786), and MP caps at 900. Building lines die at influence: `infCostOf = ic*.035+2` (about 12 inf for a 300-IC building), against income of 2–6 inf/s. That is at most about 0.5 buildings/s, or about 150 IC/s. It cannot absorb 850 IC/s. Refilling losses spends little (232 infantry × 30 IC ≈ 7k IC in report 7).
- **TENET (influence).** Every `placeBuilding` sets `G.infSpent` (L3903) and snaps momentum to 1.0 (L6366). A Plan that keeps auto-placing holds the player's influence income at 1/3 of its ceiling. The bill's chip list ("OK / needs X / Waiting: IC") has no influence state.
- **MONOCULTURE.** The Exchange line buys "best kills-per-IC" and winner-take-all. That is report 8's 240 Falcons, auto-fed forever.
- **TENET (the cap).** The Exchange fills the shared army cap with the cheapest kill-per-IC chaff before the player can queue one Land Dreadnought. It crowds out the player's hand-picked army.
- **MICRO.** Twelve hand-typed ledger lines per Bureau is a chore unless there is a default.
- **Fixes I demand:**
  1. Ship one default line, "Replace losses of what I field", on the moment a Bureau stands.
  2. Plan and Exchange spend only into cap headroom above a reserved 20% weight slice for the player's hand-queued units.
  3. The Exchange spreads its outflow across the top types by share of kills-per-IC, never winner-take-all.
  4. Plan placement does not reset influence momentum. Either exempt auto-placement from `infSpent`, or add an "Influence" blocked-chip.
  5. State plainly that the hoard sinks are D7's burn, D9's tiers and D10's sections (see section 3).

**D7. Ammunition & the One Locker** (kinetic-arms, railgun, industry, missiles, energy). **CHARGED: MICRO, PERF, CREEP.**
- **MICRO.** A "per-group round selector for every kinetic gun" is per-group attention by design. Require an automatic default: the gun picks its round from the target's armour and altitude. Manual selection stays optional.
- **PERF.** "Depot within 600 px" tested inside `fire()` (L4858) is a spatial query per shot at thousands of guns. Demand a 1 Hz supply flag cached per x-bucket, as `auraBB` already is (L6324).
- **CREEP.** Supplied guns get +35% damage and +15% rate (×1.55 dps). The enemy answer is unwritten. Demand that Sanitiser-class raids and Harvesters target Munition Works.
- **Credit.** "Empty stock = base stats, no penalty" is clean. The per-shot burn is the best real IC sink in the docket (0.04 × dmg/50 munitions × 6 IC).

**D8. The Trunk** (industry, command; Planet and Railgun; Megaprojects joined). **CHARGED: EXPLOIT, MICRO, FALSE-ish.**
- **EXPLOIT.** "Each hub pair adds +10% to the whole network, uncapped." If "pair" means any two hubs, 30 hubs give 435 pairs, or +4,350% output (quadratic). Demand that pairs mean adjacent links (N−1).
- **MICRO.** The Molodets "surfaces at any point on the line" and sally ports "debark behind a named line" both need per-unit orders. Demand that it auto-fires from the tunnel point nearest the target, and that sally ports auto-debark at the nearest port to contact.
- **Inert cost.** 1.5 IC/s per 1,000 px is 9 IC/s for the whole strip, about 1% of 850 IC/s. It runs through `upkeep` and `IC_FLOOR`, so it is free (section 3).
- **FALSE.** Pooled "cap +60 per hub" needs the `Math.min(60, …)` at L6313 replaced. The bill must say so; the industry annex says it does.
- **Credit.** Auto-link, auto-bonus and auto-rebuild pass the One-Button Law.

**D9. Deep Deposits** (planet, industry, naval, science, bio). **CHARGED: FALSE, EXPLOIT-bug, CREEP.**
- **Hidden denial.** `n.used = true; G.owned[type]++` (L3927). Nothing ever decrements `G.owned` or clears `n.used`. A destroyed tier-5 Extractor (48,400 IC) leaves a dead node. `canPlace` requires `!n.used` (L3869), so it can never be re-placed or rebuilt, even by the Trunk's Reconstruction Corps. Meanwhile the strike discount persists forever. The bill's "exposed investment" is false: the loss is irreversible and the survivors are free.
- **FALSE.** "Fixes `srMult`": the boolean `if (G.owned.steel …)` (L3810) is real. But with 8 nodes, 2 per type (L3633), additive stacking tops out at 2 deposits.
- **CREEP.** Oil tier 5 gives +30% sortie speed and −38% air IC. That feeds the dominant doctrine (report 8) and has no enemy answer beyond Harvester priority.
- **Worthless reward.** IC discounts mean nothing at 850 IC/s. The real rewards are alloy, rocket parts and strike stability.
- **Fixes I demand:**
  1. Destroying the Extractor clears `n.used` and decrements the deposit, so the tier is genuinely at risk and rebuildable.
  2. Stack deposits by count, with the discount floored by the existing `Math.max(8, …)`.
  3. Oil tiers do not touch air tempo.

**D15. The Power Grid** (energy, railgun, space-fleet, physics, science). **CHARGED: EXPLOIT.**
- "Shortfall = IC debt at 0.6 IC/s per MW" is subtracted from `icR` and then clamped by `Math.max(IC_FLOOR, icR)` (L6330). Debt past income − 8 is not billed. The bank is never touched.
- **Fix.** Bill power shortfall straight to `G.ic` per tick, not through `upkeep`.
- **Credit.** Overdrive wear is an optional cost and is fine.

**D14. Dispersal** (missiles, naval, air). **CHARGED: CREEP.**
- Boomer submarines "hide from bombers", and the Chronicle itself says no enemy targets the sea. A launcher that nothing can reach is an immortal warhead farm.
- **Fix.** Pair it with a fleet ASW answer in the same bill: sonar-drone pickets that spawn from sub count, built on the `scanEnemyIntel` scan. Do not nerf the subs.
- **Credit.** Warhead stock and auto-fire (`tickAutoStrikes`) pass the One-Button Law.

**D10. The Long Gun** (megaprojects, railgun, kinetic-arms, game-feel; incl. MEGA-1, MEGA-2, MEGA-3, MEGA-H). **CHARGED: FALSE, CREEP, MICRO.**
- **FALSE (enemy answer).** MEGA-3's heat is "IC drawn this stage / 500", with the interdiction boss at heat 60. A 380-IC section is 0.76 heat, and heat decays at 1/s. The 30,000 IC needed for one stage is 35 s of income. The siege never fires on a section-by-section gun. The bill's whole "scarier enemy" half is arithmetic dead code.
- **FALSE (code).** "Reuse designation flow, `manual:true`". The flag is read nowhere in `title.html`. The Fire Control path is dead (L8454). The sibling Orbital Gun just auto-fires.
- **CREEP (cost).** N = 15 sections is 5,700 IC (about 7 s of income) for 1,800 damage and map-wide range (400 px × 15 = 6,000). MEGA-H's N ≥ 40 "win shot" costs 15,200 IC, about 18 s. Compare Sky Railgun Ship 320 dmg, nuke 720.
- **MEGA-1 doctrines.** "60 air units → Air Supremacy" rewards the doctrine already winning (MONOCULTURE).
- **Fixes I demand:**
  1. Heat is per section placed (1 heat per section × N), so a 40-section gun draws a real siege.
  2. Auto-aim by default, because Fire Control does not exist.
  3. Fire interval and slew (1.5 s/section) are written into the bill; the docket summary dropped them.
  4. One doctrine per family at equal thresholds.

**D2. The Nerve** (autofleet, lore, pacing, stability, megaprojects). **CHARGED: CREEP.**
- Fear "rises with your industry" and enlarges the fleet. That is a scarier enemy, which the tenet allows. But the President wants "building more is still always the winning move".
- **Fix.** Publish the slope: 2× output gives at most 1.3× fleet. Fear reads fielded output, never the banked hoard.
- The Annihilator interrupt and Truce door are clean.

**D3. The Fleet Reads You** (autofleet, air, naval, command, stability, psi). **CHARGED: MONOCULTURE-by-cliff.**
- Reavers spawn at 20+ fighters and Durandal craters at 8+ parked aircraft. A rational player parks 19 and 7. It is a threshold to dodge, not a counter to adopt.
- **Fix.** Scale the response continuously with fighters above 10 and aircraft parked above 4.
- The scarier-enemy half is exactly right. EMCON and Hardened Shelters are real player answers.

**D6. Dead Sky Protocol** (space-fleet, missiles, physics, market, energy). **CHARGED: CREEP.**
- A Starfish burst that zeroes the grid's point defence has no enemy reply. The grid is the fleet's only answer to space.
- **Fix.** Add a `radhard` grid generation that appears after N Starfish bursts. This is cheap: it hooks `scanEnemyIntel`.
- **Credit.** The self-EMP and Kessler self-harm are optional and honest.

**D4. The One Forecast.** **CLEAN.** Information only. One rider: delete "correct positions pay stability +1", which is a free stability faucet.

**D12. The Charter & Decrees.** **CLEAN.** The dial makes the existing `dangCap` (L4708) visible. Haste/Reprisal spend stability, which the 100-point reserve partly absorbs. That is a soft cost, not an exploit.

**D16. The Dossier Line.** **CLEAN.** It has a built-in enemy answer (Sanitisers) and a bounded bonus (about 15 kinds).

**The Living Battalion.** **CHARGED: TENET.** "Shaken at 40%: −25% rate" is a new penalty on every squad, with no OP proof. Convert it to a Steady bonus for the supplied or commissioned.

**Orion.** **CHARGED: FALSE.** "10 uranium consumed as `{type:'uranium'}`". There is no uranium stockpile in the code. `SR` uranium is a deposit flag (`G.owned.uranium` is a claim count). The req type has nothing to read. Create a real stock (the Cascade tiers, or a Uranium Enrichment output) first.

### First-Session bills not carried whole

- **MARKET-3 War Bonds: CHARGED, EXPLOIT.** Service drains `icR`, which `IC_FLOOR` clamps at +8. Past the first bond, every extra 2,000-IC lump is free. "No cap on issuance" turns it into an infinite money loop in a 135 s prep.
- **MARKET-4 Lloyd's: CHARGED.** A 60% payout stacked with the Trunk's 70% ghost rebuild means a lost building is nearly free. The premium runs through the clamp. Demand a single rebuild refund, not both.
- **MARKET-5 Letters of Marque: CLEAN.** The bounty is a real, scaling, per-kill cost.
- **MARKET-6 Privatise the Sky: CLEAN as withdrawn.** Structure survives in D6.
- **COMMAND-3 Shock Brigades: CHARGED, FALSE and MICRO.** `bt ×0.4` is 2.5× speed, not "60% faster". With ρ/(1−ρ), ρ = 0.9 jams in about 11 s. A busy factory (the only one you would shock) nets 0.89× output over the 31-s cycle, so it is a trap. A per-factory toggle is per-building attention. Make it one Commissariat-wide standing choice with a bounded ρ.
- **COMMAND-4 Dead Hand: CLEAN.** It has its own enemy answer (the command-layer wave) and fires only in a crisis.
- **COMMAND-5 Urals Gambit: CHARGED, EXPLOIT.** Dismantle refunds 70% and re-erection costs 0 IC, so each cycle prints 0.7 × building cost. Round-trip it forever (95% with nanotech). It is not carried, but the Decree menu names Evacuation. Demand a refund of 0 for relocated buildings (they simply move) and a one-way refund only for destruction.
- **INDUSTRY-3 Autofoundry: CLEAN as withdrawn** into D5.
- **INDUSTRY-1, 2, 4, H and MEGA-2, 3, 4, H** are judged inside D7, D9, D8, D16, D10 and Orion.

**Tally: CHARGED 17, CLEAN 7.**

## 3. Blatantly overpowered today

1. **IC itself.** Report 7: 212k unspent at 850 IC/s. Income is uncapped (every Power Plant +7, `icR` L6318) with no outflow. Proof under Amendment I: every cost-reduction in the economy, including `unitCost`'s factory discount floor (0.22) and `srMult`, is moot, and the economy tech choices are pointless. **This does not need a nerf.** It needs a sink the enemy can attack and the cap cannot block: D7's per-shot burn, D9's tiers and the Long Gun's sections (a 6,000 px strip carries about 400 surface slots and two buried layers, so even buildings are finite).
2. **Upkeep is not a cost.** `icR -= upkeep` then `Math.max(IC_FLOOR, icR)` (L6330). Upkeep only lowers income and is clamped to +8. It never drains the bank. Logistics Hubs (−18% each, floor 40%, L6329) widen it. At 850 IC/s you need 842 IC/s of upkeep before it bites. Every bill that claims "a real cost (upkeep)" (Trunk, Plan tax, Long Gun idle, Grid debt) is inert. **This needs no nerf, only honest billing:** charge new costs to the bank per tick (as `queueUnit` charges `G.ic -= cost`).
3. **Deposits are permanent.** `G.owned` and `n.used` never reset (L3927). Uranium's −30% IC and −40% strike stability cost (L6896) survive the Extractor. No enemy answer exists. An answer, not a nerf: make Extractor death clear the claim.
4. **Air plus oil.** Oil −18% air IC (L3810) feeds report 8's dominant doctrine. D9's oil tiers add more. This is a CREEP warning, not an OP finding. It needs the Fleet Reads You answer (D3), not a cut.

## 4. Struck self-limits that should return

- **The Long Gun's slew (1.5 s/section) and per-shot IC (40 IC/section)** are in the annex but absent from the docket summary. They are the only things that make section 40 different from section 400. Restore them in the bill text. Do **not** restore the 12-section cap or the Recoil Cradle wear. Those were nerfs in costume.
- **The Exchange gain of 0.08/s (a 12-s time constant)** came out with MARKET-6's Flash Crash. It is the only damper on the kills-per-IC feedback loop. Write it back into D5.
- **Hub upkeep of 1 IC/s** is gone with "once per 1,000 px". That is fine, because per-hub charge and per-px charge were never both needed. Do not restore it.

## 5. Questions for Question Time

1. **Command and Industry (D5):** At army cap, where does the Plan's IC go? Name the sink that absorbs 850 IC/s with no unit cap and no influence cost.
2. **Industry (D9):** When a tier-5 Extractor dies, `n.used` stays true (L3927) and the node is dead. Do you accept that destroyed tiers are rebuildable, or is "exposed investment" just flavour text?
3. **Megaprojects (D10):** One section is 380 IC, so 0.76 heat against the 60 threshold. At what N does the first Interdiction wave actually spawn? Show the arithmetic.
4. **Market (D5):** Your outflow fills the shared army cap with the best kills-per-IC type. How does the player still queue a capital ship? Do you accept a 20% weight reserve for hand-queued units?
5. **Command (D8 and COMMAND-5):** Is "each hub pair" N−1 or N(N−1)/2? And if relocated buildings refund 70% and rebuild free, what stops the loop?
6. **Industry (D7):** Name the automatic default round selector. If the player never touches it, does a gun still fire the right shell?
