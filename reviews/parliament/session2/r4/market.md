# MINISTRY OF THE EXCHANGE — Second Reading (Round Four)
*Slug: `market`. Economy Bloc.*

## 1. Speech

Madam Speaker. The President says: click a button, build units, scale with volume. Honourable members, that is a *market* sentence. Nobody in a market attends to a single trade; the price does the attending. I have never wanted the player to touch the Exchange. I wanted them to build it and look away.

So I confess where I forgot. I asked the player to stake influence on claims, one at a time. I asked them to press Issue Bonds like a cashier. That is a chore dressed as a finance. The Ministry amends itself before the Inspector-General can: positions become standing posture, bonds become a policy, and surplus simply leaves.

And to Amendment I, the Ministry says: a market is never "too strong". It is only ever *too exposed*. Give the fleet a bank run to start and I will build the Bourse anyway.

## 2. The Re-reading

**MARKET-1 The Clearing House. WITHDRAW as a standalone bill.** Its whole mechanic lives in the Standing Plan. Nothing is lost; one engine is better than two.

**MARKET-2 The Forecast Exchange (positions). AMEND.** One-Button: staking influence per claim is per-decision attention, so the manual stake is struck. Amendment: **Standing Posture.** Once, the player picks one of three postures (CAUTIOUS / BALANCED / BOLD); the Exchange then auto-stakes a fixed fraction of banked influence (5 / 10 / 20%) on the top three claims at each forecast refresh, at the displayed `p`, settling at the invasion flip. The forecast display itself is already the One Forecast's. Line: passes (no space).

**MARKET-3 War Bonds & the Sovereign Spread. AMEND.** One-Button: pressing Issue three times a match is cheap, but "a button" is not a standing choice. Amendment: **Bond Policy**, a single dropdown (OFF / WHEN BROKE / ALWAYS). Under WHEN BROKE the Exchange issues a 500 IC tranche whenever banked IC is under the reserve and a ledger line is starved; debt service stays `principal*r` with `r` rising with `lostStructures`. Default OFF. Line: passes.

**MARKET-4 Lloyd's of Orbit (Underwriter). KEEP.** One-Button: fully automatic; premiums are upkeep and payouts are a rebuild credit that rides the Standing Plan's Reconstruction ghost list (no click). The correlation halving is a real cost, not a nerf, and stays. Line: passes (a building cost, no flank).

**MARKET-5 Letters of Marque. KEEP.** One-Button: contractors come out of a Pool like any queued unit; bounties settle automatically; the ticker is a display. The "on strike at 0 IC" 70% fire rate is a real cost that scales with volume. Power-creep check: contractors count against `armyCap` as ordinary units (Art. IV.1 untouched), so they do not make the existing army worthless; the enemy answer is the Fear Index reading the Pool as industry.

**MARKET-6 Privatise the Sky. AMEND.** Debris is handed to the Debris Ledger (coalition, `G.debris[]`). One-Button: auto-launch is the Exchange's outflow, not a click; the Circuit Breaker is a building, not an action. Perf risk: ten thousand sats is a render and AI cost, so commercial sats are **cached-art, no-AI, fire-only** (they never move, Art. IV.2) and the Flash Crash batch-spawns at most 40 per second. Power-creep check: bombarder grids already murder 42/44 Halos (report 7), so the enemy's answer exists today; the new part is that a market player loses cheap sats, not dear ones. Line: vertical, passes.

**THE STANDING PLAN (coalition, 11 votes). AMEND.** The docket asks "tax or per-line". Market votes **tax**: Industry has already yielded to the Chair's 6%. A percentage scales with volume; a flat per-line charge does not. Amendment: the Exchange face pays **no** tax (its price is the +10% dividend minus a 3% clearing fee on market-spent IC), so the market road costs about 7 percentage points less than the command road: that is the point of being market. The reserve `R = 800*(1-max(mkt,cmd))+150` stays a default only. One-Button: a player who builds and never opens the panel still gets surplus outflow. Line: passes.

**THE ONE FORECAST (coalition, 8 votes). KEEP.** `G.forecast` with `sigma/sqrt(1+n_obs)`. One-Button: it is information, and the player who ignores it loses nothing they have today. Observers are buildings, so ten Radars sharpen it ten times as hard: scales with volume. Line: passes.

**Counts.** KEEP 3, AMEND 4, WITHDRAW 1.

## 3. The Order Paper

### #1 The One-Button read
Done above. The one structural fix is that **nothing in the Market portfolio needs a click**: Standing Posture, Bond Policy, auto-outflow, auto-insurance, auto-launch. Every choice is a standing choice made once.

### #5 What a market society becomes (lead)

### MARKET-7 — THE GRAND BOURSE (a megaproject that makes the nation a market)

*Pitch.* "Section by section a tower of glass and ticker-tape climbs over the capital. When it is done, every building in the country has a price on it, and everyone can read it. The nation stops being a state with an economy and becomes an economy with a state."

*Science.* Diamond and Dybvig (1983): a bank is stable only while depositors *believe* it is; a run is a self-fulfilling prophecy. Minsky (1986): stability breeds the leverage that breaks it. Both give the enemy a real weapon against a market society: **confidence.**

**Clerk's Annex**
- **Mechanic and numbers.** Built in unlimited sections (framework of MEGA-1). Each section: 600 IC, 60 MP, 16 s, upkeep 3 IC/s. Per section: +0.02 to `G.mkt`, +0.5% to the market dividend, +0.4 influence/s, and +0.25 influence cap (to a global cap that already moves via `INF_CAP`). `civ.society` = `mercantile` (a new `society()` repaint: gilded dusk, scrolling ticker line along the horizon). `civ.stabCap` +0.3 per section. `civ.fleetRead` = Fear +0.2 per section (AUTOFLEET-2): a rich nation scares the fleet. **The enemy's answer is a named psychological event, THE RUN.** At `mkt >= 0.6` and at least 3 sections, the fleet (reading `G.enemyIntel` and idle IC) fires THE RUN once per phase: for 40 s the Underwriter's premiums double, the bond spread doubles, and Exchange outflow reverses into an **automatic reserve draw** (the Sinking Fund, a building, auto-spends banked influence to halt it at 2 influence per second). The run ends early if the Sinking Fund can pay; if not, `stabOffset += 4` for 60 s. Headline: "CONFIDENCE FALTERS — THE EXCHANGE SUSPENDS TRADING". Systems, not scenes (Art. IV.5).
- **Code grounding.** `society()` L3549 (new kind); `scanEnemyIntel` L5737 and `G.enemyIntel` for the trigger; stability cap write at the `G.stabCap` line L6331; `INF_CAP` L7103; `cityStabPenalty` L4547; `BUILD_DEFS` L3381 (Sinking Fund); `updateProjects` and `PFX` on `megaprojects-wip`; headline via `banner` L10335.
- **Size.** M on the framework (MEGA-5's `civ` block), L without it. **TM1.**
- **Research.** New node `bourse` (branch `age`, tier 3, pre `capmarkets` + `spaceage`, cost ~300 raw, not dangerous). Combos: `algoexchange` (more sections per project), `shieldtech` (hardening against THE RUN: a Reserve Depot also halves run duration).
- **Tenet (Am. I).** A buff; the enemy answer is named (THE RUN, plus bombarder priority on the tower) so this is not power creep.
- **One-Button.** Sections are built like any building; THE RUN is answered by an automatic Sinking Fund; no click.
- **Line.** The Bourse is a vertical tower on the surface; the Run is a time event, not a location.

### MARKET-8 — THE FREE PORT (the undersea branch of the market, #7 adjacency)

*Pitch.* "A port that the sea cannot close: undersea cables, a container exchange, and ships that trade between wars."

**Clerk's Annex**
- **Mechanic.** Building **Free Port** (water-adjacent, 420 IC, SPEC). Each standing one: +1.5 IC/s per friendly naval hull within 800 px (a one-dimensional radius), minimum 0, and +0.5 influence/s while a **Cable Landing** (small seabed building, 180 IC) stands within 1,500 px. Cables are cut by enemy trench landers (NAVAL-4); a cut is a headline and a rebuild credit via the Underwriter.
- **Code.** `BUILD_DEFS` L3381, naval unit list `UNIT_DEFS` L3184, `srMult` style aura L3810, `pushRuin` L1515 for the cut.
- **Size.** M. **TM1.** **Research.** `capmarkets` + Shipyard access (no new node).
- **Tenet.** A buff; the enemy answer is the trench lander. **One-Button.** Passes (naval units park and earn). **Line.** One-dimensional radius; the cable is a vertical seabed layer.

### #10 (answered in section 4)

## 4. Blatantly overpowered today

**Nothing in the Market portfolio is blatantly overpowered, and I will not invent a nerf to look rigorous.**

The honest candidate is **idle IC**, and it is a symptom, not a toy. Report 7: 212k IC unspent at 850 IC/s, and ROADMAP (OPTIMUM-BUILD STUDY, report 3 to 5) records the player funnelling surplus into off-cap units (Clone Battalions x2748, Shapeshifters). That proves IC has **no marginal value** late, not that any unit makes losing impossible: reports 6-10 are Full Invasion wins, yes, but report 7 was PYRRHIC (3/7 cities, 81% units lost). So the amendment's proof bar is not met for a nerf. The right answer is the one already on the docket: **sinks that scale with volume** (Standing Plan, Munition burn, Reserve Depots) and **a scarier enemy**, which is AUTOFLEET-2's Fear Index (the fleet grows with your industry). Influence momentum (3x over about 75 s, `MOM_CAP` L6358) is also not blatant: it is a reward for patience with a snap-back.

## 5. Struck self-limits

None of my First Session limits were struck as "a nerf in a costume". I kept three costs deliberately and I recommend they stay: the **correlation halving** on insurance payouts (it is the reason dispersal matters), the **Flash Crash lockout** (a real cost of a market), and the **debt service on bonds**. They are costs with counterplay (Circuit Breaker, Sinking Fund, Reserve Depot), not caps. If the House strikes any as "nerfs", a market economy becomes strictly free money and the Inspector-General will rightly say so.
