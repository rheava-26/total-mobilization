# COMMAND COUNCIL — Round 2 Floor Paper

## 1. Floor speech

Comrades, the Commission is moved. Three councils filed bills that are our bills in different coats.

Industry wrote an **Autofoundry**, which is our Planning Bureau with a nicer hat. Planet and Industry both laid a freight rail under the continent. They discovered the Rail Hub two rounds after we did. We forgive them, since forgiveness costs nothing under a planned economy.

The Market Council wants a **Clearing House** in which prices spend your surplus. Fine. We have no quarrel with a price, only with a price pretending to be the only road. Let the President build either. Let a player who builds both find the two overlap in a tangle. That is the point of an emergent society and not a slider.

We praise **Autofleet**, which noticed that a fleet afraid of us will cut the telephone line. We praise **Stability**, which gave the crisis a real decision. We praise Missiles for the missing TEL, which we have been smuggling into the tunnels since Round 1.

To the Pacing Council: do not shorten the preparation because we built a power plant. That is a tax on the fantasy, and the Commission pays taxes only to itself.

To those who will say our shock brigades "punish" the player: the factory jam is a **cost chosen by the player**. A planner who storms the month accepts the breakdown. Nothing is capped, and nothing is denied.

The plan will be fulfilled. Pass the bills.

## 2. Positions

| Bill | Stance | Reason / exact amendment |
|---|---|---|
| INDUSTRY-3 Autofoundry | **AMEND** | Merge with COMMAND-1 (Motion A). Orders must be able to target *buildings* too, not only units, and 'reserve slider' must be a building-driven number, not a UI slider (Art. IV.4: no slider). |
| INDUSTRY-4 Freight Rail / Reconstruction Corps | **SUPPORT** | The Reconstruction Corps is the right half of our evacuation bill. Amend: Corps rebuild ghosts must be allowed to be *moved* buildings (COMMAND-5 manifest) at 0 IC. |
| PLANET-1 The Spine | **AMEND** | Merge into one rail net with INDUSTRY-4 (Motion B). Keep 1.5 IC/s upkeep per 1,000 px as the real cost. |
| MARKET-1 Clearing House | **AMEND** | Reserve R = 800(1 - mkt) + 150 must not apply as a penalty to a player who built a Planning Bureau. Amendment: a command reading `G.cmd` (0 to 1) raises from Planning Bureau, Commissariat, Staff Bunker; `R` uses `max(0, 1 - mkt - cmd)`. Neither road is a nerf. |
| MARKET-2 Forecast Exchange | **SUPPORT** | Intel buildings that sharpen the forecast are Art. IV.4 slice 2. |
| AUTOFLEET-3 Weakest Axis | **AMEND** | Add a sixth axis: **Command Layer** (share of Planning Bureau / Comms / Radar alive). Its event is "Cutting the Lines" (Motion C). |
| AUTOFLEET-2 Fear Index | **SUPPORT** | A fleet scaled to industry is a scarier enemy, which the tenet wants. |
| STABILITY-2 Maskirovka Bureau | **SUPPORT** | Command societies run propaganda. Amend: Bureau counts toward `G.cmd`. |
| STABILITY-3 State of Emergency | **SUPPORT** | Martial Law is the command road in the crisis. Amend: add **Continuity** as a fourth option if a Staff Bunker stands (Motion C). |
| LORE-1 Charter | **SUPPORT** | A dial per dangerous tech is how Shock Labor and Dead Hand should read. |
| MISSILES-2 TEL/Arsenal/Boomer | **SUPPORT** | The TEL exists as a unit now. Amend: TEL may ride the rail net, surfacing at a hub (our Molodets, same unit). |
| MISSILES-5 Warhead Stockpile | **SUPPORT** | Magazine depth is a quota in disguise; the Plan can order stockpiles. |
| RAILGUN-X Meridian / MEGA-2 Long Gun | **AMEND** | Both need the rail net as their circle. They must read the same rail graph (Motion B), not each invent one. |
| PACING-HOT Fleet Watches You Build | **AMEND** | Take the sponsor's own fallback: the alarm changes the fleet's *gambit*, never the clock (see §6). |
| PACING-4 War Charters | **SUPPORT** | Posture choices are the command/market fork in plain sight. |
| SCIENCE-4 Crash Programs | **SUPPORT** | Crash programs are shock brigades for research; share the strain curve with COMMAND-3. |
| PLANET-5 Mobilized Ground | **SUPPORT** | Zoning and salvage is the civilian-sector conversion the ROADMAP calls the designer's best idea. |
| INDUSTRY-H Salvage the Fleet | **SUPPORT** | Free ore is free ore. |
| BIO-1 Hyphal Net | **OPPOSE** | A second, living infrastructure net competes with the rail net for the same Spine job. Fold its repair into rail. |

## 3. Ballot (10 votes, max 3 per bill, none to COMMAND)

| Bill | Votes | Why |
|---|---|---|
| INDUSTRY-4 Freight Rail and Reconstruction Corps | 3 | Gives the rail a job. Dissolves the lift bottleneck that sank Report 7's space fleet. |
| AUTOFLEET-3 The Weakest Axis | 2 | The fleet reading the player is the heart of the society layer. |
| INDUSTRY-3 The Autofoundry | 2 | Fixes the 212k anti-pattern with a building, not a button. |
| MISSILES-2 TEL / Arsenal / Boomer | 1 | The dispersal promise CLAUDE.md makes and v3.25.0 broke. |
| STABILITY-3 State of Emergency | 1 | Turns the worst minute into a decision. |
| LORE-1 The Charter | 1 | Standing orders for dangerous research, a real society texture. |

## 4. Motions to co-sponsor

### Motion A — "The Standing Plan" (to: **industry**, **market**)
**Title.** One ledger engine, three faces.

**Merged mechanic.** One shared `tickStandingOrders` loop (1 Hz, modelled on `tickAutoStrikes` L6300). Three buildings are skins of it: Planning Bureau (command), Autofoundry (neutral), Clearing House (market). Each holds ledger lines `{key, target}`. A line may name a unit *or* a building. Reserve `R = 800 * (1 - max(mkt, cmd)) + 150`, where `G.mkt` rises from Exchanges and `G.cmd` rises from Bureaus, Commissariats and Bunkers. The market face spends surplus by kills-per-IC. The command face spends by ledger priority. The neutral face is first-come. A player with all three gets an average, which is the 50/50 emergent default.

**Clerk's Annex.**
- **Numbers.** 8 ledger lines per Autofoundry, 12 per Bureau, none for an Exchange (it chooses). Overhead +6% (Bureau), 0% (Autofoundry), −2% dividend (Exchange). Scan every 2 s.
- **Code hooks.** `queueUnit` L4619, `placeBuilding` L3884, `G.sites` L3908, `produceGlobal` L9863, income tick L6318–6328, `BUILD_DEFS` L3381.
- **Build size.** L (one engine, three skins).
- **TM1/TM2.** TM1; `G.cmd`/`G.mkt` are the TM2 society reading.
- **Tenet check.** Passes. Pure additions with a real overhead cost. The 'reserve' is a cost of choosing neither road.

### Motion B — "The Trunk" (to: **industry**, **planet**)
**Title.** One rail net under the continent.

**Merged mechanic.** INDUSTRY-4 Freight Rail + PLANET-1 Spine + COMMAND-2 Molodets. Hubs within 1,000 px link into one graph. Linked components pool (cap +60 per hub, a buff). Linked buildings gain +25% output within 120 px of track. The Reconstruction Corps rebuilds linked losses. Rail upkeep 1.5 IC/s per 1,000 px. TEL/Molodets unit rides the graph, untargetable underground, surfaces at any hub.

**Clerk's Annex.**
- **Numbers.** Throughput 1 building or unit per 2 s per hub. Molodets speed 140 px/s. Hub immune to orbital barrage (`ug`), cut only by seismic kinds.
- **Code hooks.** `railhub` L3416, `railHubs` L7812, rail hop L8232, `digLevel` L3863, draw L9313, `makes` loop L6306, `armSilo` L7660.
- **Build size.** L.
- **TM1/TM2.** TM1.
- **Tenet check.** Passes. Gives pooling, mobility and a launcher. The cost is upkeep and a cuttable network.

### Motion C — "Cutting the Lines" (to: **autofleet**, **stability**)
**Title.** The command-layer wave and the continuity option.

**Merged mechanic.** AUTOFLEET-3's sixth axis and event, plus STABILITY-3's crisis menu. Axis `Command` = share of Planning Bureau, Comms and Radar standing. When it is the weakest axis, the fleet sends a wave at those buildings. For 45 s the Plan runs at half speed. Answer: Staff Bunkers (`autoBury: 2`, hp 700) keep the ledger alive. In a capitulation crisis, a standing Bunker adds a fourth option, **Continuity**: strategic launchers fire once at the densest cluster at no stability cost.

**Clerk's Annex.**
- **Numbers.** Event window 45 s, cooldown 90 s, bunker 380 IC. Continuity one-shot per crisis.
- **Code hooks.** `busterScore` L7446, `busterTarget` L7462, crisis start L6636, crisis ramp L6608–6648, `armSilo` L7660, `EVENTS` L1081.
- **Build size.** M on top of the two parents.
- **TM1/TM2.** TM1 (slice 3).
- **Tenet check.** Passes. A scarier enemy plan with a real answer, and an additive crisis option.

## 5. Trades

- **Shrink COMMAND-1.** Cede the unit-queue half to Motion A. We keep the building-ledger and the Bureau as the command skin.
- **Withdraw the freight bonus in COMMAND-2** in favour of Motion B. We keep only the Molodets unit and the hub upkeep.
- **Keep COMMAND-3 (Shock Brigades) and COMMAND-5 (Urals Gambit) whole.** They are the unique command bills; nobody else filed a strain curve or an evacuation. COMMAND-4's Dead Hand shrinks into Motion C.

## 6. Constitutional challenges

- **PACING-HOT, Art. III.** A shorter prep removes build time, which reads as denying the player's toys. It stands only if the sponsor's fallback applies (the alarm changes the fleet's gambit, never the clock); we ask the President to rule for the fallback.
- **MARKET-1, Art. IV.4.** As drafted the reserve `R` charges a command-leaning player a penalty. Our amendment (`max(mkt, cmd)`) removes it, and without it the bill pushes the player toward one road, which breaks the "several roads into each side" instruction.
- We raise **no** challenge against `capLevels` or `dangCap`. `dangCap` is a real cost, but we hold that the Charter (LORE-1) is the right answer to it.
