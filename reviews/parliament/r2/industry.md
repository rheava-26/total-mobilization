# INDUSTRY & LOGISTICS COUNCIL — Round 2 Floor Paper
*Slug: `industry`.*

## 1. Floor speech

Mr Speaker. I rise to count freight cars.

I note with joy that **four councils independently discovered the railway.** Command wants a trunk and a Molodets. Planet wants a Spine. Railgun wants the nation to be a barrel. Even the Hon. Member for the Air Force wants a flying petrol station. Gentlemen: this is what logistics looks like when other ministries finally read the quartermaster's memo. You are welcome.

I also note that **three councils invented auto-build in the same week**: a Planning Bureau, a Clearing House, and my Autofoundry. The Ministry does not mind being copied; it minds being copied *three times in three incompatible buildings*. Merge, or the President will click three panels to do one job.

I praise the Naval Council for finding the sea's uranium. It is the first strategic-resource fix that does not require a lucky rock. I praise Missiles for the Warhead Stockpile: stockpiles are the honest answer to 212,000 idle IC.

I savage the Secretariat's PACING-HOT, which would punish a nation for industrialising. Industry is the fantasy. A clock that runs faster because you built a factory is a tax on the thing this game exists to celebrate. And I warn the Ground Council that "battalions" without replenishment depots are columns of men walking out of ammunition.

Against my own bills I expect two attacks: that Ammunition Trains is a hidden nerf, and that Salvage is a snowball. Neither holds. Empty depots lose only the *bonus*, and the fleet gets a Sanitiser for every wreck we harvest.

## 2. Positions

| Bill | Stance | Reason / amendment |
|---|---|---|
| COMMAND-1 Five-Year Plan | AMEND | Merge into one building with INDUSTRY-3 and MARKET-1 (Motion B). |
| COMMAND-2 Metro-2 / Molodets | AMEND | Keep trunk bonus and Molodets; hub upkeep 1 IC/s is fine. Merge the network into Motion A. |
| COMMAND-5 Urals Gambit | SUPPORT | The best use of the rail net: evacuation is a real logistic flow, bounded by hub throughput. |
| PLANET-1 Spine | AMEND | Merge into Motion A; hubs are barrage-immune only at depth, which is the right trade. |
| PLANET-3 Deep Survey | SUPPORT | Grade and depth give each node an identity; stacks with my deposit tiers. Amend: tier cost scales with node grade. |
| PLANET-5 Zoning and Salvage | AMEND | Zoning: support. Ruin salvage: fold into Motion C so there is one salvage economy, not three. |
| NAVAL-2 Blue Economy | SUPPORT | Resources as bonuses without a lucky node; this is the designer's intended fix. Amend: adsorber stacks with my Cascade tiers for Uranium. |
| MISSILES-5 Warhead Stockpile | SUPPORT | Real sink, real upkeep, real fallout risk. Amend: Munition Depot draws munitions from Munition Works (INDUSTRY-1) so there is one stock system. |
| MARKET-1 Clearing House | AMEND | Fold into Motion B as the "market mode" of a ledger line. |
| AIR-1 Pelican | SUPPORT | Fuel as geography; optional fuel stock refilled for IC is a sink. |
| KINETIC-2 Sabot and Shell | AMEND | AP burn draws from the `munitions` stock, not alloy, to avoid starving Mammoths. |
| RAILGUN-X Meridian / MEGA-2 Long Gun | SUPPORT | Tunnels as a vulnerable asset is a logistics cost. Amend: shares the net graph from Motion A. |
| SCIENCE-1 Wreck Lab / LORE-3 Black Boxes | AMEND | Merge with INDUSTRY-H (Motion C). |
| ENERGY-2 Power Bill | SUPPORT | A power grid is the sibling of a supply grid; a Cold Plant should double as a Rail Hub client. |
| BIO-1 Hyphal Net | SUPPORT | Living infrastructure; hyphae may carry munitions: a free cheap depot link. |
| PACING-HOT | OPPOSE | See section 6. |
| STABILITY-1 Casualty Ledger | SUPPORT | Replacement drafts need the ledger to know what to replace. |

## 3. Ballot (10 votes)

| Bill | Votes | Why |
|---|---|---|
| NAVAL-2 Blue Economy | 2 | Fixes the oldest known flaw, and gives the sea an economy. |
| MISSILES-5 Warhead Stockpile | 2 | A deliberate IC sink with a story. |
| PLANET-3 Deep Survey | 1 | Every deposit gets an identity. |
| COMMAND-5 Urals Gambit | 1 | Best rail fantasy in the House. |
| COMMAND-1 Five-Year Plan | 1 | Auto-build is the missing quality-of-life for a mass game. |
| AIR-1 Pelican | 1 | Logistics for the dominant doctrine. |
| RAILGUN-X Meridian | 1 | Sprawl becomes a weapon. |
| SCIENCE-1 Wreck Lab | 1 | Wrecks as research gives the dead a purpose. |

## 4. Motions to co-sponsor

### Motion A — THE TRUNK (to: Planet, Command)
Merge INDUSTRY-4, PLANET-1 and COMMAND-2 into one rail bill.

**Clerk's Annex**
- **Mechanic.** Rail Hubs within 900 px auto-link into a **Trunk**. (a) Linked hubs pool `G.comp` (alloy, rocketpart, munitions); cap becomes 60 per linked hub. (b) Buildings with `makes` or `autospawn` within 120 px of a tunnel segment gain +40% output; each hub pair adds +10% network-wide (Command). (c) Extractors on a node touching the Trunk gain +25% IC (Planet). (d) A destroyed Trunk-linked building queues a **ghost**; 20 s later it rebuilds at 70% IC if an Autofoundry (Motion B) exists, otherwise a single click rebuilds all ghosts. (e) Molodets Train, gated by `railmobile` (Command), unchanged. Real cost: 1 IC/s per hub. Hubs at depth ≥ 1 skip orbital barrage damage (Planet); only Earthbreaker hits cut a segment.
- **Code.** `railhub` L3416; `railHubs` L7812; `makes` loop L6306; `digLevel` L3863; ghost list on building death near `updBuildings` L8398; `recalcBuildings` L3762 for the graph.
- **Research.** One node `railnet` (branch `auto`, tier 1, pre `automation`, cost 100). Combo `spaceage`: Orbital Resupply, rocket parts skip lift. Combo `nanotech`: self-boring extension (Planet).
- **Size.** L. **TM1.**
- **Tenet.** Pure upside with an upkeep cost. A cut Trunk is the enemy's target, not a cap.
- **Interactions.** Railgun (Meridian shares the graph), Strike (Molodets), Fleet (tunnels become targets).

### Motion B — THE FIVE-YEAR LEDGER (to: Command, Market)
Merge COMMAND-1, INDUSTRY-3 and MARKET-1 into one building and one panel.

**Clerk's Annex**
- **Mechanic.** One building, **Planning Bureau** (Command's cost 220 IC, build 12). Up to 12 ledger lines per Bureau. Each line has a mode: **FIXED** (`{key, target N}`, Command and Autofoundry) or **MARKET** (the Exchange picks the best kills-per-IC type over 90 s, Market). A reserve slider sets the floor (default 0). Standing orders replace losses and queue only what army cap, lift and air command allow. Overhead +6% IC (Command); MARKET lines get the Market dividend of +10% build value but a 5% reserve penalty. Each Exchange raises `G.mkt` (TM2 hook).
- **Code.** `queueUnit` L4619; `placeBuilding` L3884; `tickAutoStrikes` call at L6300 as the loop precedent; `G.stats.killsByType` L3606; `facsForUnit` L9862.
- **Research.** `cybernetics` (Command) plus `capmarkets` (Market); combo `hivemind` lifts the reserve floor.
- **Size.** L. **TM1** (the slider and `mkt` are TM2 inputs).
- **Tenet.** Adds a way to spend; never forbids a click.

### Motion C — THE WRECK DOCTRINE (to: Science, Lore)
Merge INDUSTRY-H, SCIENCE-1 and LORE-3 into one wreck economy.

**Clerk's Annex**
- **Mechanic.** `destroyEnemy` L6834 leaves a **wreck** (90 s decay) worth `hp/40` alloy. Wrecks within 500 px of a Research Lab yield a **Sample** (Science); wrecks collected by a Salvage Crawler yield alloy and a **Black Box** dossier (Lore, +10% damage vs the kind). 5% of heavy hulls also yield Xenoalloy. Fleet response: after 6 wrecks harvested, **Sanitiser** drones appear to detonate wrecks, and landings favour Salvage Yards. Boss wrecks 10×.
- **Code.** `destroyEnemy` L6834; `resCostOf` L4663; `spawnEnemy` L5581; `INTENT` L5119; `COMP` L3180.
- **Research.** `salvage` (branch `auto`, pre `robotics`, dang sc 3) feeds `studies` tags on existing nodes.
- **Size.** L. **TM1.**
- **Tenet.** Gain only; the cost is the Sanitiser escalation.

## 5. Trades
- **Withdraw** INDUSTRY-3 into Motion B in full.
- **Shrink** INDUSTRY-4 to the Trunk only: I drop the Rail Battery.
- **Shrink** INDUSTRY-H to Xenoalloy plus Sanitisers; Science and Lore own samples and dossiers.
- **Keep** INDUSTRY-1 and INDUSTRY-2 as written, with Planet's grade multiplier on node tiers.

## 6. Constitutional challenges
- **PACING-HOT (Article III).** Shortening prep as you industrialise punishes the build-out the game celebrates. Rule it admissible only in its own fallback form: ALARM as information, changing the fleet's gambit, never the countdown.
- **Planet's `capLevels` point of order (Article III).** Evicting terrain edits past 220 silently caps a player toy; it is a tenet smell and should be lifted, not defended.
