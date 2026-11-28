# MARKET COUNCIL — Round 1 Position Paper
*Ministry of the Exchange. Slug: `market`.*

## 1. Opening Speech

Honourable members, President. I rise on a falling stockpile and a rising conscience. Report 7: **212,000 IC sat unspent at 850 IC/s** while the fleet ate your cities. The Command benches will tell you this is a failure of the player. I tell you it is a failure of *price*. Nobody told that money where to go. Money wants to move. Money has a nose for the next kill. Give it a nose and it will find the Falcon wing you forgot to order before you do.

The Generals, I hear, want "a button that spends 20,000 IC." Colleagues, a button is a *command economy with a thumb*. We propose the opposite: a society that spends itself. You build an Exchange and a clearing house, and the surplus leaves your treasury like water leaves a hill. You pay a fee for that, you lose control of the timing, and you get something better than a banked pile: a living, hungry industrial base that has already guessed what the fleet will send.

We bring four things the designer has not thought of: prediction markets that *really* sharpen with the number of independent observers, war-risk insurance priced by the enemy's own scan of you, contract units paid per kill, and a flash crash. The Command Council will call us speculators. We call them people who have never seen a spread. **The market is not a button. The market is a mirror, and it always shows what you built.**

## 2. The Gap

- **Idle treasury.** 212k IC unspent (Art. VI, report 7). `updateGameInner` (L6288) adds `icR*dt` to `G.ic` (L6328) and nothing drains it except the player. The only sinks are manual: `queueUnit` (L4619), `placeBuilding` (L3884), `produceGlobal` (L9863). ROADMAP L450 names this the "ROOT ECONOMY DRIVER": excess IC funnels into off-cap units because nothing else absorbs it.
- **Influence has one job.** `G.infR` (L6359) pays for research and buildings only, and `infMomentum` rewards hoarding it. There is no *influence-rich* playstyle with its own verbs, so influence never trades off against IC.
- **The PREP forecast is a label.** `INTENT` (L5119) prints the intent name. `tutReadyTick` (L11796) produces a readiness score from `READY_REASONS` (L11443) about *your* gaps. Nothing tells you *what the fleet will do*, so there is nothing to speculate on and no reward for scouting.
- **No risk transfer.** `G.lostStructures` (L3707, `pushRuin` L1515) only drags stability. A destroyed 1,000 IC building is simply gone. Nobody can pay to flatten a loss.
- **The price signal is invisible.** `G.stats.killsByType` and `spentIC` exist (L3606, L3902), so kills-per-IC is computable per unit type. The player has no readout and the economy never uses it.

## 3. Bills

**Research hook for all bills (see MARKET-1 annex):** one new `age`-branch node, **Capital Markets**, and one combo, **Algorithmic Exchange**. Buildings are the road, since the President wants buildings to nudge the reading, not a slider.

---

### MARKET-1 — The Clearing House (surplus-IC outflow by market-ness)

*Speech.* "Hon. Members, a treasury that sits is a treasury that rots. We propose the Commodity Exchange. Stack them and money stops queuing for permission."

**Player fantasy.** You stop micromanaging a 200k pile. Your industrial base spends the surplus *for* you on what has been killing best, and you get something back for letting go: a dividend.

**Clerk's Annex**
- **Mechanic.** Compute `G.mkt` (0 to 1) each second from buildings: Commodity Exchange 0.15 each, Data Center 0.05, Propaganda Bureau 0.05, Comms Tower 0.03, capped at 1. Default 0. In TM1, `mkt` is only a number that these buildings raise; there is no slider. Reserve `R = 800 * (1 - mkt) + 150`. Each second, `surplus = max(0, G.ic - R)`; outflow `= surplus * mkt * 0.08`. The outflow is spent by the Exchange on the unit type with the best **kills-per-IC over the trailing 90s** (`G.stats.killsByType` / spent, for types you own a production building for), fielded as free squads from the nearest valid production building using the `queueUnit` path with the cost pre-paid. The spend obeys army cap and air command like any order (`combatWeight`/`armyCap` check at L4625); if capped, the Exchange fields mechanical/drone types, then banks as influence at **12 IC to 1 inf** (the "influence-rich" half). **Dividend: market-spent IC buys 10% more** (price discovery; the unit comes out with 1.1x of its normal build value).
- **Real science.** Hayek's knowledge problem and market clearing: dispersed information is aggregated by a price, not planned. Operationally this is a feedback controller with kills-per-IC as the error signal, which will oscillate if gain is too high, hence the 0.08 gain (a 12s time constant).
- **Code grounding.** Income at `updateGameInner` L6318-6328; stats at L3606; `queueUnit` L4619; `produceGlobal` L9863; `bmod()` L3767 for a `mkt` bonus; `BUILD_DEFS` L3381 for `exchange` (model on `datacenter` L3450, `ds` string per style rule: "Spends your surplus industrial capacity on whatever has been killing best. Each one makes your society more market-led. Your army cap still applies.").
- **Research.** New node `capmarkets` (branch `age`, tier 1, pre `info`, cost 70 raw so x1.6 = 112, not `dang`, time 14). Unlocks `exchange`. **Combo `algoexchange`** (pre `capmarkets` + `automation`, cost 240, `dang` sc6): doubles outflow gain, Flash Crash risk (see Hot Take).
- **Build size.** M (about 220 lines). **TM1.**
- **Tenet check.** Nothing capped or denied. The reserve is a *cost of choosing market*, bought only by building Exchanges, and the dividend rewards it. Command players who never build one keep today's game exactly.
- **Interactions.** Command Council (wants the manual 20k button; this bill supersedes it), Air/Naval councils (their units become outflow targets, a free demand signal), Society councils (`mkt` is half of the TM2 reading).

---

### MARKET-2 — The Forecast Exchange (sharpening the PREP forecast, with positions)

*Speech.* "You do not know what comes. Neither do I. But 40 of me together know very well."

**Player fantasy.** The PREP clock becomes a trading floor. You buy information, watch the forecast bands narrow, and take a position on what lands. When the fleet obeys your bet, a dividend rains.

**Clerk's Annex**
- **Mechanic.** Add a **forecast** to the HUD readiness pill (L11713): for each of {dominant counter-axis (the `G.enemyIntel` axes: aa, lasers, infantry, robotic, navy, spaceReach), first super kind and its time, skyfall flank}, show a probability band. Base uncertainty `sigma = 1.0`. Each **Forecast Desk** (new `SPEC` building: "Tape Room") adds an independent observer, and so does each Radar and Data Center at half weight. Effective band `= sigma / sqrt(1 + n_obs)`. At the top, the band collapses to the true value from `INTENT` + `buildSupersPlan` (L5301). **Positions:** spend influence (20/40/80) before re-entry to stake a binary claim ("first super is a hive"); on resolution the payout is `stake_IC_equiv * (1/p - 1)` where `p` is the displayed probability: a calibrated market, per the logarithmic market scoring rule (Hanson 2003). Correct bets pay IC and stability +1; wrong bets lose only the stake. Positions lock at `prepTimer < 12` (the existing re-entry warning, L6492).
- **Real science.** Variance of a mean shrinks as 1/n (ensemble forecasting; the same reason a weather ensemble beats one model); LMSR automated market makers; Brier scoring. The player-facing note is the band width, not the maths.
- **Code grounding.** `tutReadyTick` L11796 (already ticks ~1/s in PREP); `scanEnemyIntel` L5737 and `G.enemyIntel` (the axes the fleet reads about *you*, mirrored here); `curIntent` L5187; `buildSupersPlan` L5301; stat settlement hooks at the `phase = 'invasion'` flip L6492.
- **Research.** Node `futures` (branch `age`, pre `capmarkets`, cost 100 raw, not `dang`). Combo with `psistudies` unlocks "Precognition Desk" (SCI-FI: the psi-relay's decentralised network samples future states; consistent with Art. V psionics as decentralised command: a swarm of low-grade oracles averages out like an ensemble). Without it, every figure is real-world.
- **Build size.** M (about 250 lines, mostly UI). **TM1** (this is TM1 slice #2 from Art. IV).
- **Tenet check.** Pure addition: more information and optional payouts. Nothing is hidden that is shown today.
- **Interactions.** Intel/Scout council (their buildings feed `n_obs`), Enemy council (forecast must be *truthful but bounded*, so the enemy's adaptation scan stays interesting), Society councils (the fleet's reading of *your* axes and your reading of *its* intent are two halves of one mirror).

---

### MARKET-3 — War Bonds & the Sovereign Spread

*Speech.* "A nation that cannot borrow against its own survival does not believe in it."

**Player fantasy.** Mortgage tomorrow for a doomsday arsenal today. The more you borrow, the more everyone watches your credit.

**Clerk's Annex**
- **Mechanic.** New action in the Exchange panel: **Issue Bonds** converts future IC into a lump now (100, 500, 2,000 IC). Debt service = `principal * r` IC/s drained from `icR` for the remaining term (about 5 game-minutes), where `r = base 0.4%/s + spread`. **Spread rises with `G.lostStructures` and falls with stability and with your stockpile of banked influence** (influence is *the credit rating*; the "same number, renamed" seed from the design memo). Default (IC income below service for 20s) costs `-15` stability and freezes bonds for 2 minutes: a real cost, no cap on issuance.
- **Real science.** Sovereign yield = risk-free + default premium; WWII war bonds worked partly as *forced savings* that dampened inflation (financial repression). Spread widening under losses is the empirical pattern of war risk.
- **Code grounding.** Upkeep subtraction at L6318-6321 (`icR -= upkeep` then `IC_FLOOR`); `G.lostStructures` L3707; influence at L6359; stability at `effStab` L4548.
- **Research.** `futures` node unlocks the Bond Desk panel (no building needed beyond one Exchange).
- **Build size.** S (about 90 lines). **TM1.**
- **Tenet check.** More toys earlier. The cost is debt service, an upkeep, as Art. III allows. No cap on how much you issue.
- **Interactions.** Economy/Logistics councils (Logistics Hub's `upkeepCut` also trims bond service if routed through `upkeep`), Stability council (default event).

---

### MARKET-4 — Lloyd's of Orbit (war-risk insurance priced by the enemy's scan)

*Speech.* "We do not insure against the war. We insure against *your* war, and your war is on the front page of the fleet's own file."

**Player fantasy.** A loss is no longer a loss. The wreck becomes a cheque and a rebuild order; you actively design your base to be cheap to insure.

**Clerk's Annex**
- **Mechanic.** New building **Underwriter** (`SPEC`). While standing, every non-city building you own pays a premium `= 0.1% of its IC cost per second` (as upkeep). When a building is destroyed (`pushRuin` L1515), pay out **60% of its IC cost** immediately as a rebuild credit usable only for placing that building type again, within 90s. **Premium multipliers priced by the fleet's own read of you:** `G.enemyIntel.dominant` (L5775): if you are monocultured, the insurer's rate rises up to x2.5 on the building types the fleet is counter-tooling (`counterBias`, L5513); balanced bases pay x0.8. **Correlation rule:** if more than 12 insured buildings die within 20s (a skyfall or annihilator beam), the Underwriter *is out of money*: payouts are halved and a headline fires ("LLOYD'S SUSPENDS PAYOUTS"). That is a real cost, and a reason to disperse.
- **Real science.** Insurance works by pooling *uncorrelated* risk (law of large numbers); correlated catastrophes (hurricanes, war) bankrupt pools, hence reinsurance and catastrophe bonds. War-risk surcharges on shipping in the Persian Gulf (Lloyd's Joint War Committee listed areas) rise with threat level exactly as modelled here.
- **Code grounding.** `pushRuin` L1515 / destroy paths; `G.enemyIntel` and `counterBias` at L5737-5785; `BUILD_DEFS` for the building; `placeBuilding` L3884 for the rebuild credit.
- **Research.** `capmarkets` unlocks it; combo `capmarkets` + `shieldtech` unlocks **Cat-Bond Reinsurance** (pays at the full 100%, removes the correlation halving, but takes 20% of your Exchange dividend).
- **Build size.** M (about 160 lines). **TM1.**
- **Tenet check.** Net buff to resilient play, with a real, legible cost (premiums, correlation). No cap.
- **Interactions.** Enemy council (its psychology scan is now a price you can read; edge-path events could target the insurer: "the fleet shorts your Underwriter"), Defence/Dispersion council (dispersal lowers correlation, so it is a free reason to build mobile launchers).

---

### MARKET-5 — Letters of Marque (contract units paid per kill)

*Speech.* "Our contractors do not ask for a salary. They ask for a *bounty per hull*. Cheaper than a standing army, and they never stop trying."

**Player fantasy.** Whole wings and squadrons you never paid for upfront. They fight for the prize money, and you can see the bounty table like a stock ticker.

**Clerk's Annex**
- **Mechanic.** Building **Contractor Pool** (`PROD`) fields **Privateer** versions of existing air and ground units (reskin of existing defs through `effDef`, so no new art) at **0 IC upfront** and full manpower weight (the Art. IV army cap stands untouched). Each kill they score pays a **bounty** from `G.ic`, `bounty = 0.35 * enemy base cost`; the price table is a live HUD ticker updated from `G.stats.killsByType`. They pay themselves in IC drained *only when they win*, so the market cost is always the kills-per-IC of the unit. If IC is zero they do not leave; they simply fight unpaid at 70% fire rate ("on strike" icon). The contract price floats with scarcity: the less you have killed of a type recently, the *higher* its bounty, so privateers drift toward the neglected target, a natural fix for the "monoculture" problem.
- **Real science.** Privateering and prize law (Letters of Marque and Reprisal, US Constitution Art. I s.8); prize shares aligned incentives for centuries. Dynamic bounties are an auction for attention.
- **Code grounding.** `destroyEnemy` L6834 (the kill hook; `G.kills++`, `G.salvage` refund at L6842 shows the kill-reward style), `unitCost` L3835, `UNIT_DEFS` L3184, `effDef`.
- **Research.** `capmarkets` + `automation` (the combo `algoexchange` puts them on a ticker); non-`dang`.
- **Build size.** M (about 180 lines). **TM1.**
- **Tenet check.** Strictly more toys. The bounty is a real cost with no cap.
- **Interactions.** Air/Ground councils (their units are the contractors), Economy (competes with `salvage`, stacks).

---

## 4. Hot Take

### MARKET-6 — Privatise the Sky (commercial orbit, the Flash Crash, and the debris tax)

*Speech.* "Colleagues, the fleet kills your satellites because *you* put them there, one at a time, with your own treasury. We say: let the *market* fill the sky. Ten thousand commercial slots. Let them eat the bombarders' fire. And when the grid collapses, as it will, we will sell the debris."

**Player fantasy.** A sky *dense* with cheap commercial satellites that you never ordered. When the fleet's bombarder grids tear them apart, the debris cloud itself becomes a weapon, and the crash is a headline.

**Clerk's Annex**
- **Mechanic.** Satellites **never move** (Art. IV.2), so all of this is placement. With the `algoexchange` combo and a Space Center, the Exchange's MARKET-1 outflow can **auto-launch commercial Halos/Volleys into unclaimed orbital slots** (the same units, 40% IC, 60% HP, no upkeep, still bound by lift: `orbCapUsed`). Dead satellites leave a **debris field** (a static hazard zone, a stationary area, no motion) in that band: `+8%` damage taken by any *enemy* in the zone, a small `+2%` to your own satellites also in it (real Kessler cost). **Flash Crash:** at `mkt >= 0.8`, each minute there is a 6% chance the algorithmic traders herd: for 45s outflow **x4** and the Exchange mass-buys the current top kills-per-IC type, then a 20s lockout (priced out). The headline fires: "FLASH CRASH — EXCHANGE SUSPENDED". You can pre-empt it by building a **Circuit Breaker** (halts for 10s and fires early, trading profit for safety).
- **Real science.** Commercial constellations (Starlink: thousands of cheap sats); Kessler syndrome (1978: debris collisions cascade, a real risk in dense orbital shells). Flash crashes: 6 May 2010 (Dow -9% in minutes, from algorithmic herding) and Knight Capital 2012 (about $440M lost in 45 minutes from a bad algorithm), the latter literally a 45-second window. Circuit breakers are the real mitigation.
- **Code grounding.** Satellite defs in `UNIT_DEFS` L3184 (Halo, Volley); `orbCapUsed` / space lift; spawn hook via the same path as MARKET-1; debris could be a static zone entry in the existing `G.parts`/zone structures (verify before building); stat tracking at L6834.
- **Research.** Combo `algoexchange` (`capmarkets` + `automation`, `dang`: yes, sc6, since it is the unrest of an unregulated algorithmic market), plus `spaceage`. Existing nodes `spaceage` (L3505) and `orbitalcmd`.
- **Build size.** L (about 500 lines; the debris field and the crash scheduler are the expensive parts). **TM1** for the auto-launch and the crash (core), debris could be cut to a flat damage aura for M.
- **Tenet check.** Cheap mass in orbit is exactly the Art. VI wish ("space units should matter"). The cost is a real one: the crash window and the Kessler tax. No cap on satellite count.
- **Interactions.** Space/Orbital council (core ally, MARKET-1's outflow is the demand signal), Enemy council (bombarder grid must still be scary: suggest it *prefers* debris-poor slots), Command council (this is the thing they cannot do; their orbit is hand-placed).

## 5. Alliances Sought

- **Space/Orbital Council**: the commercial sky and the debris tax need their satellite roster and lift rules. They get demand.
- **Command / Industrial Council**: our natural enemy. But the *emergent reading* needs both poles; together we produce the 50/50 default, with them owning the IC- and MP-rich side.
- **Intel / Scout Council**: every Tape Room is another observer for `sigma / sqrt(1+n)`. They sell us bands, we buy them.
- **Enemy / Fleet Psychology Council**: the insurer's price *is* the fleet's scan result. Edge-path vulnerability events (the TM1 slice #3) fit as "the fleet shorts the Underwriter".
- **Air/Naval councils**: their units are the primary destinations for MARKET-1 outflow and MARKET-5 contracts.
