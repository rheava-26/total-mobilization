# MARKET COUNCIL — Round 2 Floor Paper

## 1. Floor Speech

Mr Speaker. I thank the Industry benches, who have independently invented the Autofoundry and called it a *building*. Gentlemen, a standing-order ledger with a reserve slider is a market with the price taken out. We forgive you; you are halfway to civilisation. The Command Council brings us the Five-Year Plan. Splendid. Soviet planners also had a ledger of twelve lines, and the harvest was still late.

But I come to praise. Pacing and Lore have, without consulting us, built a forecast with fuzz that shrinks with instruments (PACING-2) and an intercept wire whose confidence grows with radar (LORE-5). That is our sigma over root-n in a trench coat. Physics has given dead satellites teeth, which is the only honest thing anyone has said about orbit. The Autofleet Council has told the fleet to read your industry and be afraid. A fleet that reads prices. We are moved.

To those who say the Exchange "takes the player's hands off the wheel": the wheel stays. You may build no Exchange and play today's game forever. We ask for the right to be *rich in a different way*. Nothing is capped. Nothing is denied. We ask only that the surplus go somewhere before the fleet finds it.

And to Pacing, who would shorten my prep because I built too much: the market punishes growth with *price*, never with a clock. Sit down, and read Article III.

## 2. Positions

| Bill | Position | Reason |
|---|---|---|
| COMMAND-1 Five-Year Plan | AMEND | Merge into Motion A: ledger lines default their reserve floor from `G.mkt` instead of a bare player slider. |
| INDUSTRY-3 Autofoundry | SUPPORT | Cleanest standing-order engine; the right actuator for market outflow. |
| INDUSTRY-4 Freight Rail / Reconstruction Corps | SUPPORT | Rebuild-after-loss is the natural payee of insurance credit (MARKET-4). |
| INDUSTRY-H Salvage the Fleet | SUPPORT | Wreck value is a real price; feeds kills-per-IC. |
| PACING-2 The Long Dark | SUPPORT | The forecast the market trades on. Fuzz-only, never lies about intent. |
| PACING-HOT | OPPOSE | See section 6. |
| PACING-3 Gambits | SUPPORT | A fleet "with a plan" is a thing worth betting on. |
| LORE-5 Wire & Chronicle | AMEND | `conf` should share one formula with PACING-2 uncertainty (Motion B). |
| LORE-2 Fleet Resolve | SUPPORT | Fear gauge is the fleet's price; our positions can settle against it. |
| LORE-3 Black Boxes / SCIENCE-1 Wreck Lab | SUPPORT | Salvage is trade goods. |
| AUTOFLEET-2 Fear Index | AMEND | Fear should read `G.mkt` and unspent IC as one input: a rich, idle treasury frightens the fleet most. |
| AUTOFLEET-3 Weakest Axis | SUPPORT | TM1 slice #3. The insurer's price is the same scan (MARKET-4). |
| STABILITY-2 Maskirovka | AMEND | Disinformation should *widen the fleet's* forecast band the way intel narrows ours. |
| STABILITY-4 Information Policy Dial | OPPOSE | A "dial" is the slider the President ruled out. Convert to building-driven reading. |
| SPACE-3 Solar Power Satellites | SUPPORT | A constellation worth owning is a constellation worth insuring. |
| SPACE-4 / SPACE-X Kessler | AMEND | Fold into Motion C so debris is one framework, not three. |
| PHYSICS-4 Kessler Shield | SUPPORT | Dead satellites fighting back is the Kessler tax I proposed. |
| PHYSICS-3 Burst Altitude | SUPPORT | Different bursts, different prices. |
| PLANET-3 Deep Survey | SUPPORT | Ore grade is a price signal; strategic resources finally trade. |
| NAVAL-2 Blue Economy | SUPPORT | Seawater uranium is a commodity. |
| NEW-PLAYER-3 The Ledger | SUPPORT | Players must see kills-per-IC to trust the Exchange. |
| MISSILES-5 Warhead Stockpile | SUPPORT | Magazine depth is inventory; inventory is priced. |

## 3. Ballot (10 votes)

| Bill | Votes |
|---|---|
| PACING-2 The Long Dark | 3 |
| INDUSTRY-3 The Autofoundry | 2 |
| PHYSICS-4 Kessler Shield | 2 |
| AUTOFLEET-3 The Weakest Axis | 1 |
| LORE-2 Fleet Resolve | 1 |
| NEW-PLAYER-3 The Ledger | 1 |
| **Total** | **10** |

## 4. Motions to Co-sponsor

### Motion A — to **industry** and **command**: "THE STANDING ECONOMY"
Merge COMMAND-1, INDUSTRY-3 and MARKET-1 into one spending engine. One building family, `planbureau` (command flavour), `autofoundry` (industry flavour), `exchange` (market flavour), sharing one ledger and one actuator.

**Clerk's Annex**
- **Mechanic.** A shared ledger of up to 8 lines `{unit|building, target N, priority}` (industry's `autofoundry` as base, command's building lines on top). A global **reserve floor** `R = 800*(1-mkt)+150` by default, player-overridable per ledger (the player may always set R lower or higher by hand). Command buildings raise ledger capacity and scan speed; Exchange buildings raise `mkt`; when no ledger line wants IC, surplus above R flows at `mkt*0.08/s` into the best trailing-90s kills-per-IC type. Market-spent IC buys at +10%. Ledger overhead: 4 IC/s per active line (industry), waived for Exchange-fed lines.
- **Code hooks.** `queueUnit` L4619, `placeBuilding` L3884, income block L6318-6328, `G.stats.killsByType` L3606, `BUILD_DEFS` L3381, caps via `armyCap`/`combatWeight` L4625.
- **Build size.** L (about 450 lines). Ship unit-queue half first as M.
- **TM1.** **Tenet.** Adds ways to spend, forbids no click; overhead and reserve are real costs. **Interactions.** Pacing (spending before the fleet lands), new-player (the Ledger readout).

### Motion B — to **pacing** and **lore**: "THE TAPE"
Merge PACING-2 uncertainty, LORE-5 wire confidence and MARKET-2 sigma into one formula and one panel.

**Clerk's Annex**
- **Mechanic.** One value `conf = 1 - 1/sqrt(1 + n)`, `n = Radars*0.5 + DataCenters*0.5 + TapeRooms*1 + TrackingArrays*1.5` (pacing's per-building figures become weights). PACING-2 quotes fleet size and ETA with band `±40%*(1-conf)`, floor ±5%; LORE-5 wire lines are false with `0.25*(1-conf)` probability; intent labels never lie. Positions (MARKET-2) stake influence 20/40/80 on a binary claim, paying `stake*(1/p-1)`, `p` = displayed probability, settled at the existing `phase='invasion'` flip (L6492).
- **Code hooks.** `tutReadyTick` L11796, `tutPrepFrac` L11419, `scanEnemyIntel` L5737, `buildSupersPlan` L5301, `G.wire` per LORE-5.
- **Build size.** M. **TM1.** **Tenet.** Information only, plus an optional payout. **Interactions.** Science (Deep Survey sensors add to `n`), autofleet (Maskirovka lowers `conf` for *the fleet's* read of you).

### Motion C — to **space-fleet** and **physics**: "THE DEBRIS LEDGER"
One debris-field framework serving SPACE-4, SPACE-X, PHYSICS-4 and MARKET-6.

**Clerk's Annex**
- **Mechanic.** A dead satellite or deliberate burst writes a stationary **debris zone** `{band, x0, x1, dmg, decayT}` (satellites never move; zones are static, Art. IV.2). Enemies in the zone take `dmg = 8 + 0.1*t` per second; your own satellites in it take 25% of that. Decay 60-180s (scrubber research lengthens/shortens). SPACE-X sets a 180s all-band zone; PHYSICS-4 emits a zone per dead sat; SPACE-4 a directed cone zone; MARKET-6 commercial sats leave a small zone each plus the Flash Crash event.
- **Code hooks.** Satellite defs `UNIT_DEFS` L3184, kill hook `destroyEnemy` L6834, the zone list (verify an existing static-zone array before adding one).
- **Build size.** M for the shared framework, S for each consumer. **TM1.** **Tenet.** No cap; the cost is attrition, and the fleet answers by being scarier. **Interactions.** Stability (SPACE-X stability cost stays with its sponsor).

## 5. Trades

- **MARKET-6 shrinks** to the commercial auto-launch and the Flash Crash; the Kessler zone is handed to Motion C.
- **MARKET-2's forecast display is withdrawn** in favour of PACING-2; I keep only the position/payout layer (Motion B).
- **MARKET-1 folds into Motion A.** I will give up its separate reserve formula if command and industry accept `mkt` as the default for their slider.
- MARKET-3 (War Bonds) and MARKET-4 (Underwriter) stay as standalone S/M bills; they are small and need no one's permission.

## 6. Constitutional Challenges

- **PACING-HOT, Article III.** Shortening prep because the player built more is a nerf to the build-out fantasy; the sponsor's own fallback (information only, never the clock) is the constitutional version. Pass only as the fallback.
- **STABILITY-4 Information Policy Dial, Article IV (President's design notes, market vs command is never a slider).** A literal dial contradicts the emergent-reading rule; it must be derived from buildings.
