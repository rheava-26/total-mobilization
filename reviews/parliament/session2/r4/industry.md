# INDUSTRY & LOGISTICS COUNCIL — Second Reading (Round Four)
*Slug: `industry`. Economy Bloc.*

## 1. Speech

Mr Speaker, the President has said "build as many refineries as you want", and the Ministry has never been more insulted by its own paperwork.

I confess. My Ammunition Trains asked a general to place his guns within 600 pixels of a depot. My Cascades asked him to buy five tiers on every mine, one click at a time. My Trunk asked him to click a ghost to rebuild a factory. That is not logistics. That is a clerk's chore, and a chore is a tax on attention, which is the one thing the President has told us not to spend.

So the Ministry amends itself before the Inspector-General can. Logistics, properly done, is a thing you build and then forget about. Stock is national, not local. Mines grow when you build refineries, not when you click. Broken factories rebuild while you watch the sky.

Volume is the fantasy. Ten refineries are ten times one. Let the freight run itself.

## 2. The Re-reading

**INDUSTRY-1 Ammunition Trains (and the One Locker). AMEND.** As written it failed the One-Button Law: supply meant "within 600 px of a depot", which is placement homework, and the round selector was a per-group chore. The player who only builds Munition Works got a bonus only where he happened to put them. Redesigned as INDUSTRY-1R below. Line check: passes, it is range-free.

**INDUSTRY-2 Deposit Cascades. WITHDRAW.** Five IC-bought tiers per Extractor is a per-building click, and the cost curve (x3 per tier) punishes volume. Replaced by Refinery Rows (INDUSTRY-5). Coalition bill Deep Deposits (grade, depth, seabed buildings) is AMENDED to read "refinery count" wherever it read "tier". Hot Ground (Radiotroph Gardens x Uranium) is AMENDED the same way: the multiplier is 1 + 0.05 per Enrichment Cascade built.

**INDUSTRY-3 Autofoundry, as merged into The Standing Plan. AMEND.** It passes One-Button by nature (the ledger is a standing choice). I yield on the overhead: the Chair's **6% tax** wins over my flat 4 IC/s per line, because a percentage scales with volume and a flat charge does not. Status chips are mandatory.

**INDUSTRY-4 The Trunk. AMEND.** Hub spacing and clicking ghosts were chores. Redesigned below (INDUSTRY-4R), including the fit with the underground.

**INDUSTRY-H Salvage / The Dossier Line. AMEND.** Salvage Crawlers are units to micromanage. Cut. A Salvage Yard auto-collects every wreck within 700 px; a Research Lab within 500 px takes the sample. Sanitisers stay as the enemy's answer. Line check: wrecks lie on the line and the Yard's radius is one-dimensional.

**Sortie Supply (air). KEEP.** Rearm and refuel are automatic (`rearmAircraft` seeks a tanker or depot by itself). **Fire for Effect, Supplied (kinetic-arms). KEEP.** The supplied bonus rides INDUSTRY-1R's global stock, so it needs no placement.

## 3. The Order Paper

### #1 The One-Button read of the logistics bills

| Bill | Does a player who only builds things get the full benefit? |
|---|---|
| Ammunition & the One Locker | **No, as drafted.** Distance to depots and a per-group selector. Fixed in INDUSTRY-1R. |
| The Trunk | **No, as drafted.** Hub spacing, ghost-clicking. Fixed in INDUSTRY-4R. |
| Dispersal (missiles, naval, air) | **Yes if** the strike button auto-picks the launcher: the existing strike flow (`armSilo`) must choose the launcher with the best lane by itself; submarines auto-station in their depth band. AMEND to say so. Without it, "which boat?" is a per-shot chore. |
| The Power Grid | **Yes.** One pool, shortfall billed as IC debt, never a throttle. Capacitor overdrive must fire automatically when the bank is charged. Radiators auto-cool. KEEP with that one sentence. |

### INDUSTRY-1R — AMMUNITION TRAINS, NATIONAL STOCK

*Pitch.* "The nation has one ammunition stock. Build more works and it never runs dry. That is the whole bill."

**Clerk's Annex**
- **Mechanic.** `munitions` joins `COMP` with cap 600 + 100 per Munition Works. Munition Works: 240 IC, +2.0/s, converts 6 IC to 1 munition while stock is under cap. **Every** friendly gun (ground, naval, AA, artillery, turret, rail) draws from the national stock regardless of distance: if stock > 0, +35% damage and +15% rate; burn 0.04 × dmg/50 per shot. Empty stock = baseline. Ammunition Depot (existing +25% rate aura) stacks as a local bonus; it is optional. The One Locker's round selector defaults to **AUTO** (AP vs armour 14+, airburst vs air, HE vs swarm); a player may set one standing preference per gun type, once. AP draws munitions, not alloy.
- **Numbers.** 400 supplied shots/s burn about 16 munitions/s, about 96 IC/s. At 3,000 guns, about 700 IC/s: a real sink that scales with volume. Ten Works make 20/s.
- **Code.** `COMP` L3180; `makes` loop L6306 (hard cap `Math.min(60, …)` to `COMP[k].cap`); `fire()` L4855; cooldown at L8240; HUD L9908; `ammodepot` L3452.
- **Size.** M. **TM1.** **Research.** `logistics` (branch `auto`, tier 1, pre `automation`, cost 90, not dangerous). `smartfuze` and `apds` (docket #7) attach as before.
- **Tenet (Am. I).** A player buff, so the enemy answers twice. The fleet reads munition burn as industrial signature (it feeds the Fear Index), and Works are **priority targets**: Sanitiser and bomber waves hunt them. Losing the Works costs the bonus, never the gun.
- **One-Button.** Passes: build Works, stock is national, selector is automatic. **Line.** Passes: no range, no flank.

### INDUSTRY-4R — THE TRUNK, SELF-LINKING

*Pitch.* "Lay hubs. The nation lays the rest."

**Clerk's Annex**
- **Mechanic.** Hubs link automatically at 1,200 px **plus 100 px per hub already linked**, so a growing net closes its own gaps. Linked hubs pool alloy, rocket parts and munitions (cap +60 per hub), and buildings with `makes` within 120 px of a tunnel gain +40%. The **Reconstruction Corps rebuilds destroyed linked buildings automatically** at 70% IC after 20 s, drawing from IC above the ledger reserve, no click. Upkeep 1.5 IC/s per 1,000 px. The Molodets rides the net; the strike button picks its surfacing hub.
- **Underground fit (Subterranean Works).** Hubs sit at the Deep stratum natively; under SUB-1's standing setting they follow it, so an ABYSSAL net is cut only by Earthbreakers. Every segment is addressable (hub pair id) for the Under-Line. Deep Foundry's +20% PROD aura stacks **additively** with the trunk bonus. Habitats and Geo Taps within 120 px of a tunnel are automatically in the Reconstruction Corps' ghost list. The Redoubt megaproject is built in sections *along* the Trunk.
- **Code.** `railhub` L3416; `railHubs` L7812; hop AI L8232; `makes` loop L6306; `recalcBuildings` L3762; `digLevel` L3863; building death near `updBuildings` L8398.
- **Size.** L. **TM1.** **Research.** `railnet` (branch `auto`, tier 1, pre `automation`, cost 100); Orbital Resupply combo with `spaceage`.
- **Tenet.** A buff with a real upkeep and a cuttable net; the enemy answers with seismic cuts and tunnel Burrowers. **One-Button.** Passes. **Line.** Passes: the net is a line and the Under-Line is the same line one stratum down.

### INDUSTRY-5 — REFINERY ROWS (#8 more buildings; replaces Cascades)

*Pitch.* "Build as many refineries as you want."

**Clerk's Annex**
- **Mechanic.** Four buildings, each needing one claimed deposit of its type: Steel Mill, Oil Refinery, Rare-Earth Separator, Enrichment Cascade (real: staged centrifuges). Each built one adds linearly: **+1% to that resource's bonus** (Steel: ground IC; Oil: air and naval IC; Rare: orbital IC; Uranium: strike IC, plus 0.4 alloy/rocket parts/s and +2 IC/s). 100 mills are 100 times one. The existing unit-cost floor (`Math.max(.1, cm)`, L3840) already bounds discounts; I add no cap.
- **Code.** `SR` L3173; `srMult` L3810 (also read the count, not a boolean); `BUILD_DEFS` L3381; `G.owned`.
- **Size.** M. **TM1.** **Research.** None for the Mill and Refinery; `fusion` for the Separator; `directedenergy` for the Cascade.
- **Tenet.** A buff. The enemy answer: Harvesters and landings prioritise refinery rows (a big silhouette). **One-Button.** Passes. **Line.** Passes.

### INDUSTRY-6 — RESERVE AND REPAIR DEPOTS (#8 more buildings, defensive stations)

**Clerk's Annex**
- **Mechanic.** (a) **Reserve Depot** (140 IC, DEF): +60 component cap, +100 munition cap each; it keeps 2 garrison infantry and 1 AA. (b) **Field Repair Depot** (160 IC, DEF): heals friendly buildings and ground units in 300 px at 4 HP/s, drawing 0.05 alloy/s while it heals.
- **Code.** `BUILD_DEFS` L3381; `auraHeal` L3777; `spawnGarrison` L4573; cap at L6306.
- **Size.** S. **TM1.** **Research.** `logistics`. **Tenet.** A buff; Depots are stationary, so bombers can kill them. **One-Button.** Passes (place and forget). **Line.** Passes.

## 4. Blatantly overpowered today

**Nothing meets the Amendment I bar.** Report 7 left 212k IC unspent at 850 IC/s, which proves IC is not binding, the opposite of overpowered. Observation, not a nerf: `unitCost` (L3835) discounts 9% per extra factory down to a floor of 22% after nine, so more factories give no further discount, only throughput. That is a volume wall. I propose no change; throughput is the volume.

## 5. Struck self-limits

I struck none as "a nerf". I dropped Salvage Crawlers and the flat 4 IC/s ledger overhead for One-Button reasons. The IC burn per shot, the Sanitiser answer and the hub upkeep stay as real costs.
