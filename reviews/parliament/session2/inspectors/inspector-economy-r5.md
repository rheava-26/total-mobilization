# Inspector-General of the Economy Bloc, Round Five: the second audit (`inspector-economy`)

## 1. Opening

The green eyeshade returns. The bloc has read my first report and, to its credit, wrote back at length. Two councils found their own printing press and closed it. That is rare, and I will say so once.

But I asked where 850 IC/s goes, and four papers answered with new things to build. None of them noticed that building costs influence, that upkeep is clamped, and that a stability number was written in the wrong units. I brought a ledger and a calculator.

## 2. The second charge sheet

Every line number was re-grepped in `title.html`.

**Cross-cutting finding: the Civic Block writes to the wrong field (FALSE).** MEGA-5 says `civ.stabCap` "writes to `g.mult.stab`". But `G.stabCap = … + Math.max(0, G.mult.stab) * 30` (L6338), so:
- Every "+0.3/+0.5/+4" is multiplied by 30, giving +9, +15 or +120 cap points per section, and the cap is clamped at 100.
- Every negative ("−0.4 per Node", "−3 Chthonic") is dropped by `Math.max(0, …)`, so the bills' costs are void.

**Fix demanded:** write civic stability through `econ.stab` (×14, L6317, negatives allowed via `capPenalty`), as Comms Towers do.

### Megaprojects

- **MEGA-5 Civic Block: CHARGED (FALSE).** See above. The enemy `fleetRead` hook is fine.
- **MEGA-6 Orbital Ring: CHARGED (FALSE, CREEP).**
  - `SPACE_Y` is a function of `worldH()` (L1541). "+2% per section, uncapped" shifts every altitude band that reads it, and that is not scoped.
  - Upkeep 8 IC/s is inert (`IC_FLOOR`, L6330).
  - Unbounded `orbCap` just feeds the space fleet that dies to the grids.
  - **Fix:** the Ring adds lift only, and its upkeep is bank-billed.
- **MEGA-7 The One City: CHARGED (FALSE, EXPLOIT).**
  - "+4 stabCap" is +120 points.
  - It adds "+5 army-cap per section", which touches the formula Art. IV.1 reserves.
  - The −30 on core loss is a real cost. Keep it.
  - **Fix:** use Housing's existing `econ.cap`, and fix the stab units.
- **MEGA-8 Aegis of Earth: CLEAN.** It mirrors `shieldgen` (L3412, 900 hp, `shieldR`), and Breachers already exist (L5642). One rider: its stab line goes through `econ.stab`.
- **MEGA-9 Continental Redoubt: CHARGED (CREEP, FALSE).**
  - "At 8 sections the capital counts as buried" is a decapitation immunity. The only named answer (Burrowers) belongs to another bloc and does not exist yet.
  - "stabCap −3" is void (see above).
  - **Fix:** Burrowers ship in this bill's text, and the immunity is a 60% damage cut, not a flag.
- **MEGA-10 Blue Belt: CHARGED (FALSE).** +0.8 stab per section becomes +24 points, and +14 IC/section feeds the glut. Fix the stab units. Bathyscaphe Drones are named but belong to UNDERSEA-1.
- **Long Gun (auto-fire amendment): CHARGED (FALSE, OP, LINE).**
  - *FALSE:* "the old gun was `manual:true`". The flag is read nowhere in `title.html`.
  - *FALSE:* "sections are fleet-prioritised at 60% (MEGA-3)". MEGA-3's heat is still IC/500, and a 380-IC section is 0.76 heat against a threshold of 60. The interdiction never fires on a section-built gun.
  - *OP:* the slew (1.5 s/section) and per-shot IC (40/section) are not restated. A rate of 1 shot per 8 s at N = 100 means 10,000 dmg per shot and range 40,000 px, for 38,000 IC (about 45 s of income).
  - *LINE:* if the slug pierces (the code hooks list `p.pierce`), one shot hits the whole ground front.
  - Hp checks: `hive` 1,100t, `leviathan` 1,700t, `annihilator` 950t are all true (L5626–5628).
  - **Fix:** heat per section placed; restore slew and shot cost in the text; the anchor's hp scales with the player's N.
- **Orion (auto burn): CHARGED (FALSE, unchanged).** There is still no uranium stockpile for `{type:'uranium'}` to read.

### Command

- **COMMAND-3R Shock Posture: CHARGED (FALSE).** The ρ/(1−ρ) singularity is unbounded as ρ → 1. AUTO "storms while enemies stand on the map", which in an endless invasion is always. At ρ = 0.9 it oscillates around the 70-strain threshold and overshoots to a jam. **Fix:** clamp ρ ≤ 0.95. Per-posture MICRO is fixed.
- **COMMAND-5 Urals, no refund: CLEAN.** Time-priced relocation with no refund and no free rebuild removes the printing press.
- **COMMAND-6 Directed Economy: CLEAN.** Pure build-driven reading, with a named wave as the enemy answer.
- **COMMAND-7 OGAS: CHARGED (FALSE, PERF).**
  - "stabCap −0.4 per Node" goes through `mult.stab`, so it is void.
  - +2 ledger lines per Node to every Bureau means hundreds of lines scanned per tick. Demand a single pass that counts by key into a map.
- **Aura-ring bombing rule: CHARGED (FALSE, CREEP-by-cliff).**
  - The code claim is wrong: `auraAt` is additive (`m += aura − 1`, L3776) and linear, not "multiplicative". Heal is `Math.max` (L3783), so it never stacks.
  - Radar and Depot at score 26 is correct (L7453).
  - "6+ neighbours → 72" is a threshold to dodge.
  - **Fix:** scale the priority continuously with neighbour count, from a grid pass.
- **Standing Plan (pre-filled ledger): CHARGED.** The pre-fill is good, because it fixes my first demand. But "Build the fix" now auto-places prerequisites by default, so influence momentum is reset continuously. The cap-slice, spread-by-share and influence-chip demands go unanswered. **Fix:** bill the 6% tax to the bank, not `upkeep`.

### Market

- **Grand Bourse: CHARGED (FALSE, inert answer).**
  - THE RUN's cost is `stabOffset += 4` for 60 s. But `stabOffset` decays at 0.5/s or faster (L6369), so it is gone in 8 s.
  - Doubled premiums and spreads hit only what the player opted into.
  - Stab units are wrong (+0.3 becomes +9).
  - *Credit:* 600 IC → +0.4 influence/s is the first bill that turns IC into the influence the build-everything fantasy needs.
  - **Fix:** the Run freezes outflow and drains 1%/s of the banked IC and influence for 40 s unless a Sinking Fund stands.
- **Free Port: CLEAN.** It gives naval hulls a reason to exist, and the radius is one-dimensional. Count hulls at 1 Hz.
- **Standing Posture (positions): CHARGED (TENET, MICRO-fixed).** It auto-stakes 5–20% of banked influence at every refresh, and a loss burns the brake currency while a win pays IC (worthless at 850 IC/s). Each stake also resets momentum. **Fix:** pay winnings in influence, and exempt posture stakes from `infSpent`.
- **Bond Policy: CHARGED (EXPLOIT unchanged).** Debt service still goes through `icR`, then `IC_FLOOR` (L6330). Under WHEN BROKE or ALWAYS, the second tranche is free. **Fix:** service is bank-billed.
- **Underwriter (KEEP): CHARGED.** The payout of 60% and the Trunk ghost rebuild of 70% still stack, and premiums are inert upkeep. **Fix:** ghost rebuild consumes the credit first.
- **MARKET-6 (cached sats): CLEAN.** The army cap and orbital lift (`orbCapUsed`) still bound it.

### Industry

- **Ammunition, National Stock (1R): CLEAN.** The stock is global, so per-shot cost is O(1) with no spatial query. The selector defaults to AUTO. Works are priority targets and the Fear Index reads the burn. It is the one honest sink in the bloc (section 3).
- **Self-Linking Trunk (4R): CHARGED (FALSE, inert).**
  - Upkeep of 1.5 IC/s per 1,000 px is still routed through `upkeep`. A 6,000 px net costs 9 IC/s, which `IC_FLOOR` swallows.
  - The Reconstruction Corps cannot rebuild an Extractor: `canPlace` needs `!n.used` (L3869) and `n.used` is never cleared.
  - **Fix:** bank-billed upkeep, and clear `n.used` when an Extractor dies.
- **Refinery Rows: CHARGED (EXPLOIT, FALSE).**
  - Each Mill adds +1% additively. At 100 Cascades the discount exceeds 100%. `stratCost` has no floor (L6896), so a strike would pay IC. `unitCost` floors at 8.
  - No building has an IC price in the annex.
  - Influence cost (`infCostOf`, L3877) makes "100 refineries" take about 4 minutes of full influence income.
  - **Fix:** floor `stratCost` at 5 IC, state the prices, and cut influence per building to at most 6.
- **Reserve & Repair Depots: CLEAN.** The heal aura is `Math.max`, so it does not stack. The alloy draw is a real cost.

**Tally: CHARGED 17, CLEAN 7.**

## 3. The old charges

| # | Charge | Verdict |
|---|---|---|
| 1 | Standing Plan hoard, cap, influence | **NOT SATISFIED** (only the pre-fill landed) |
| 2 | Ammunition: MICRO, PERF, CREEP | **SATISFIED** (national stock, AUTO, Works as targets) |
| 3 | Trunk: pair exploit, MICRO | **SATISFIED** (upkeep returns as a new charge) |
| 4 | Deep Deposits | **SATISFIED** (tiers withdrawn; permanence remains, see Rows) |
| 5 | Power Grid and `IC_FLOOR` | NOT SATISFIED (not addressed) |
| 6 | Dispersal: no enemy answer | NOT SATISFIED |
| 7 | Long Gun | NOT SATISFIED |
| 8 | Nerve: publish the fear slope | NOT SATISFIED |
| 9 | Fleet Reads You: cliffs | NOT SATISFIED |
| 10 | Dead Sky: Starfish answer | NOT SATISFIED |
| 11 | Living Battalion: Shaken | NOT SATISFIED (the waiver does not remove it) |
| 12 | Orion: uranium stockpile | NOT SATISFIED |
| 13 | War Bonds | NOT SATISFIED |
| 14 | Lloyd's double dip | NOT SATISFIED |
| 15 | Shock Brigades: ρ singularity | NOT SATISFIED |
| 16 | Urals Gambit | **SATISFIED** (no refund) |
| 17 | MEGA-1 doctrines reward the winner | NOT SATISFIED |

**4 satisfied, 13 not.**

**Does anything now drain a 212k hoard at 850 IC/s? Only Ammunition 1R, and only if the player builds the Works.**
- 400 shots/s burns about 96 IC/s, which needs 8 Works.
- Draining 700 IC/s needs about 58 Works.
- Nothing forces that. Every other bill still sinks IC behind the army cap or the influence cost.

## 4. Cross-council stacks

**The Long Gun is the exploit, because three bills remove its brakes.**
- Bourse sections (600 IC → +0.4 influence/s) pay for unlimited Long Gun sections (about 15 influence each).
- The Ring adds lift and the Plan auto-builds the chain.
- Interdiction heat never fires (0.76 per section).
- The slew is dropped.
- With an uncapped N, N = 100 costs about 38,000 IC (45 s of income) and kills the N ≥ 40 anchor in one shot. That is the ending, as a bank balance.

**Other stacks:**
- *Underwriter 60% + Corps 70%:* rebuild is profitable.
- *Bond tranches + IC_FLOOR:* free IC.
- *Refinery Rows past 100%:* strikes pay you.
- *Civic stab units in all six megaprojects:* the cap hits 100 in about three sections.

## 5. Questions for Question Time

1. **Megaprojects:** Does the Long Gun slug pierce, and what stops N = 100 killing the anchor in one shot for 45 s of income? Show the heat figure per section.
2. **Megaprojects (MEGA-5):** `G.mult.stab` is multiplied by 30 and drops negatives (L6338). Which field do the civic stab lines actually write?
3. **Market (Bourse):** `stabOffset` decays at 0.5/s, so THE RUN's +4 lasts 8 s. What does the fleet take from a hoard-holder?
4. **Industry (Rows):** What do the four buildings cost, and what stops 100 Cascades making `stratCost` negative?
5. **Command (Plan):** Will "Build the fix" auto-place spend influence by default? State the reserved cap slice for hand-queued ships.
6. **Market (Bonds):** Will service be billed to the bank rather than `icR`, so `IC_FLOOR` cannot absorb it?
