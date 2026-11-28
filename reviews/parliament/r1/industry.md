# INDUSTRY & LOGISTICS COUNCIL — Round 1 Position Paper
*Ministry of Industry and Supply. Slug: `industry`.*

## 1. Opening speech

Honourable members, quartermasters, and the gentlemen of the Air Council who have never once asked where the fuel comes from.

I bring you a number. **212,000.** That is how much Industrial Capacity sat in the Treasury of report 7 while the Halo grids burned and the cities fell. Two thirds of everything this nation earned went into a vault and did nothing. My factory directors wept. Our grandmothers' great-grandchildren will ask why the Ministry of Industry let a Space Age economy turn into a savings account.

The amateurs will tell you the answer is a bigger gun. It is not. The answer is that **a nation at 850 IC/s has nothing to eat with that income.** Yes, the 60-unit component caps are generous; yes, we have Steel and Oil and Uranium lying about in the dirt granting a discount of eighteen percent, which the General Staff has never once noticed. We have an underground railway that moves idle riflemen and, I am told, *nothing else*. We have a Logistics Hub that cuts upkeep and, so help me, *nobody has built one on purpose*.

The tenet forbids us from nerfing the toys. Excellent. We will not. We will give the money a *place to go that looks like a war economy*: depots that must be fed, deposits that can be developed in tiers, foundries that rebuild what the fleet breaks, and rails that carry the lot. And in the Hot Take I will propose that the enemy's own corpse become our ore.

Logistics wins wars. Let the others shoot. We will make sure they are *still shooting* at hour three.

## 2. The Gap

| Evidence | Reading |
|---|---|
| Report 7: 212k IC unspent of 318k earned, 850 IC/s, 42 Space Centers, 20 Airbases | IC income has no sink once the unit queue is saturated. Army cap, lift and air command, not money, gate spending. |
| `COMP` stocks cap at 60 each (`Math.min(60, ...)`, L6306) | Alloy and Rocket Parts are cheap, tiny intermediate goods; they never become a strategic concern. |
| `SR` (L3173): Steel −18% ground IC, Oil −18% air/naval, Rare −15% space, Uranium −30% strikes. One claim is enough (`G.owned[n.type]` is a count, but `srMult` L3810 reads it as boolean) | A deposit is a one-time toggle. A second Steel deposit does nothing. The designer's intended fix, "grant bonuses rather than act as requirements", is half done; the bonuses are flat and static. |
| Rail Hub (L3416, hook L8231) only redeploys *idle ground troops* | The underground railway is a minor convenience. It carries no goods, links no industry. |
| Ammunition Depot (L3452) is just a +25% rate aura | No unit or building has any logistics. `ammo` exists only on aircraft and interceptors. |
| `upkeep` (L6313ff) and `Logistics Hub` (L3472, `upkeepCut`) | Upkeep exists, is capped at 40%-floor, and the counter-building is a dull −18%. No player is *choosing* logistics. |
| Destroyed buildings stay destroyed (226 lost in report 7) | Rebuilding is manual. A nation at 850 IC/s rebuilds by clicking. |

The player has tremendous income and *zero standing commitments for it to service.* That is a design hole, not a balance problem.

## 3. Bills

### INDUSTRY-1 — THE AMMUNITION TRAINS (munition stock and supplied-fire bonus)

**Pitch (in character):** "Gentlemen, a shell is not free. A Thunder battery that fires 400 rounds an hour is a *customer*. Give it a depot, a rail spur and a line to the factory and it will fire like it means it. Let it starve and it fires like a rifleman. Nobody is nerfed; the supplied are *blessed*."

**Player fantasy:** You are running the logistics of a continent. Whole belts of guns light up when the ammunition trains arrive, and you watch the freight cars roll underground at 850 IC/s.

**Clerk's Annex**
- **Mechanic.** New component `munitions` in `COMP` (cap 600, not 60). Produced by new `Munition Works` (INFRA, 240 IC, +2.0/s, converts 6 IC → 1 munition; draws IC *only while the stock is below the cap*) and Ammunition Depot (now +0.4/s). Every friendly ground/naval/AA/arty/turret shot has a "supplied" check: if a depot or rail-linked Munition Works is within 600 px *and* `G.comp.munitions > 0`, the shot fires at **+35% damage and +15% rate** and removes `0.04 × dmg/50` munitions. Out of stock = base stats (no penalty). Spend scales with fire, so a 9,000-unit war burns real IC. Aircraft keep their own `ammo`; munition depots near Airbases rearm 40% faster.
- **Numbers.** Peak war fire of ~400 shots/s × 0.04 = ~16 munitions/s = ~96 IC/s sink per 400 supplied guns. At 3,000 guns, ~700 IC/s. That *is* the 850 IC/s economy. Industry scales with the player's fantasy.
- **Real science.** Real armies are limited by shell tonnage per day: WWI artillery logistics, the 1915 Shell Crisis, WWII Red Ball Express. Ammunition, not guns, defines sustainable rate of fire. Consumption scales roughly linearly with rounds fired; stockpiles *forward-deployed* matter more than central stock.
- **Research.** New node `logistics` ("Military Logistics", branch `auto`, tier 1, pre `automation`, cost 90, not `dang`, combo partner `spaceage` for the rail-fed variant: "Orbital Resupply", see INDUSTRY-4). Gate the Munition Works and the +35% bonus behind it. Before research, depots give the old +25% rate aura only.
- **Code.** `COMP` L3180 (add entry), `BUILD_DEFS` L3381 (add `munitionworks`), per-shot hook in `fire()` L4855 and the unit cooldown at L8240 (`u.cd`), production tick in the `makes` loop at L6306 (cap currently hard-coded `Math.min(60, ...)`; change to `COMP[k].cap`), HUD cell next to `alloy · parts` at L9908.
- **Build size:** M.
- **Tag:** TM1.
- **Tenet check.** Bonus only; no penalty when empty. A real cost (IC burn) scales with the fantasy.
- **Interactions.** Fire Control & Ground (they are the consumers), Air Council (rearm speed), Intel (a Radar cell can display supply-level overlay), Society layer (munitions = the burn that makes "emergent market vs command" legible).

### INDUSTRY-2 — DEPOSIT CASCADES (strategic resources scale with investment)

**Pitch:** "Gentlemen, a mine is not a toggle. The Soviets did not claim Magnitogorsk once and go home. They built it for twenty years. Your Uranium is a hole in the ground; let me build it into a *cascade*."

**Player fantasy:** Every deposit you claim becomes a fortress-industrial district you pour IC into, and the map's four resources become four visible economies instead of four icons.

**Clerk's Annex**
- **Mechanic.** Every `Extractor` on a node gets 5 development tiers, bought by IC (cost ×3 per tier: 400, 1,200, 3,600, 10,800, 32,400). Each tier lifts that resource's bonus: Steel tier N = −(18 + 4N)% ground IC and +N·0.3 alloy/s; Oil tier N = −(18 + 4N)% air/naval IC and **+6% sortie speed** per tier; Rare Earth tier N = −(15 + 4N)% orbital IC and **+N·0.4 rocket parts/s**; Uranium tier N = −(30 + 5N)% strike IC and strike stability cost −3N%, **and tier 3+ adds a free Nuclear Reactor aura (+N IC/s)**. Multiple deposits of the same type stack additively. Developed Extractors are targets for enemy ground landings (Harvesters will prioritise them), so the investment is *exposed*.
- **Real science.** Uranium enrichment is a *cascade* of centrifuge stages; separative work scales with stage count. Rare earths need multi-stage solvent extraction (hundreds of mixer-settlers in series, which is why China dominates). Steel: Magnitogorsk-style integrated works. Oil: Fischer–Tropsch and cracking tiers.
- **Research.** No new tech for tiers 1–2. Tier 3+ requires `fusion` (Steel/Oil), `orbitalcmd` (Rare), `directedenergy` (Uranium).
- **Code.** `SR` L3173, `srMult` L3810, claim at L3927 (`G.owned[n.type]` already a count; add `b.tier`), `extractor` def L3387, economy loop L6306, UI under `srBar` at L11135.
- **Build size:** M.
- **Tag:** TM1.
- **Tenet check.** Pure upside; IC outflow without a button, because the money is *physically on the map* and a target.
- **Interactions.** Space (rocket-part rate), Air (Oil), Strategic Strikes (Uranium), Enemy Council (Harvester target priority).

### INDUSTRY-3 — THE AUTOFOUNDRY (standing orders, reinforcement pipeline)

**Pitch:** "Victoria did not ask the Prime Minister to click each factory. She *ordered* it, and it was done. Give the nation a standing order: *keep 200 Falcons alive.* Let the foundry replace the dead."

**Player fantasy:** You stop babysitting the queue. You write policy ("keep 300 Kites, 40 Sky Destroyers, 1,000 Lances") and the nation replaces losses without being asked; the surplus drains into an army that quietly refills while you play the war.

**Clerk's Annex**
- **Mechanic.** New building **Autofoundry** (SPEC, 520 IC, req new node `autofoundry`). Holds up to 8 **standing orders** `{unit, target count}`. Each second it checks live count of that unit (including queued); if below target and the matching factory is idle, it queues one batch automatically at normal cost with 5% bonus build speed. **A reserve slider** (0–100% of current IC) sets what it may never spend. Orders respect army cap, orbital lift and air command *exactly as manual queues do*. Autofoundry upkeep 4 IC/s per active order. Upgrade **Nanite Seeding** (SCI-FI, see below): +30% build speed and rebuilds *buildings* too (INDUSTRY-4).
- **Why it fixes the 212k:** the existing outflow gaps are *attention* and *lift*; this fixes attention and leaves lift as the design lever, the Space Council's job.
- **Real science.** Reorder-point replenishment, kanban and "supermarket" pull-systems (Toyota); military "replacement depots" (WWII "repple-depples"). A pull-based factory is plausible today.
- **Research.** New node `autofoundry`, branch `auto`, tier 2, pre `automation` + `robotics`, cost 220, **dang: true, sc 4** (automation-run industry unnerves people), combo partner `nanotech` for Nanite Seeding.
- **Code.** `queueUnit` L4619, `produceGlobal` L9863, `facsFor/facsForUnit` L9860–9862, pattern for auto-replacement at Underground Airwing garrison (`airwing` L3461), `tickAutoStrikes` called at L6300 is the precedent for a once-a-second policy loop; add `tickAutoFoundry`.
- **Build size:** M.
- **Tag:** TM1; the society layer in TM2 can read the reserve slider as a *market vs command* signal (**TM2 hook**).
- **Tenet check.** More convenience, no cap. Requires nothing the player does not already have.
- **Interactions.** Battlegroups (orders can target a group), Air Council (replacement of fighters), Society (the slider *is* a political axis in TM2).

### INDUSTRY-4 — THE FREIGHT RAIL AND THE RECONSTRUCTION CORPS

**Pitch:** "In 1941 the Soviets put 1,500 factories on trains and moved them behind the Urals. That is the single greatest industrial feat in history. You have a Rail Hub and use it to move *riflemen*."

**Player fantasy:** The railway becomes a nervous system. You can lose a city and watch rails carry cranes, rebuild it in the background, and send alloy to the Space Center at the far end.

**Clerk's Annex**
- **Mechanic.** Rail Hubs within 1,400 px link into a **rail net**. (a) Freight: any building with `makes` adds its output to the *net's* shared stock, effectively lifting component cap to 60 per hub linked (a buff, not a nerf); Space Centers draw alloy and rocket parts from the net. (b) **Reconstruction Corps**: when a *rail-linked* building is destroyed, a ghost is queued; if `Autofoundry` is present it rebuilds at 70% IC after 20 s; otherwise one click rebuilds the whole ghost list. (c) **Mobile command**: rail hubs let the Mass-Driver Railway segment *relocate along the track* (moving the Mass Driver to the active front over 12 s; the railway-gun fantasy, Schwerer Gustav, Dora). (d) Hubs can hold 6 reserve **Rail Guns** (new "Rail Battery", 90 IC, ground, range 1,000, dmg 55, only fires when deployed from a hub).
- **Real science.** Strategic railway evacuation (USSR 1941), railway artillery (Dora 800 mm, Paris Gun), underground rail depots (Cold War strategic rail missiles, the Soviet BZhRK "Molodets"). A rail-mobile launcher is real.
- **Research.** New node `railnet` ("Strategic Rail", branch `auto`, tier 1, pre `automation`, cost 100, not dangerous). Combo with `spaceage`: **Orbital Resupply**: rocket parts need no lift budget when launched from a railnet-linked Space Center (a *combo that changes the world*: it dissolves the lift bottleneck that sank report 7's space fleet).
- **Code.** `railhub` L3416, rail hook L7812/L8231 (`railHubs` filter), `makes` loop L6306, `massrail` L3455, `recalcBuildings` L3762 (net graph), building death in `destroyBuilding` (find by `captured`/`hp<=0` checks near L8398 `updBuildings`).
- **Build size:** L.
- **Tag:** TM1.
- **Tenet check.** Gives, does not take. A lost hub breaks the net and its freight, a real cost the enemy can exploit.
- **Interactions.** Space (lift bypass, the President's wish for relevant space), Ground (mobile guns), Enemy (rail hubs become priority bombardment targets: a scarier enemy), Intel (the net graph is a nice overlay).

## 4. Hot Take

### INDUSTRY-H — SALVAGE THE FLEET (the enemy's wrecks are our ore)

**Pitch:** "You have been burying your dead. Dig up *theirs.* The Autofleet is eleven thousand tonnes of metamaterial per hull, built by a civilisation that can fold space. Every Bulwark you shoot down is a lode. Why are we importing steel from the ground when it falls from the sky?"

**Player fantasy:** The more completely you break the invasion, the richer you get; war becomes a harvest. Your Salvage Yards pull in Heavy Alloy, a new **Xenoalloy**, and, rarely, a *warp coil*. The fleet realises it is feeding you and **changes tactics**.

**Clerk's Annex**
- **Mechanic.** On `destroyEnemy` (L6834), non-ground-hit-by-nothing kills leave a **wreck** (a persistent prop, 90 s decay) worth `hp/40` alloy-equivalents. New **Salvage Yard** (INFRA, 180 IC) and new unit **Salvage Crawler** (ground, 30 IC, weight .5, speed 14) collect wrecks within 700 px of a Yard, converting to Heavy Alloy at 1:1 and to **Xenoalloy** (new `COMP`, cap 300) at 5% chance from heavy hulls and boss wrecks. Xenoalloy lets Factories build **"Xeno-plated" variants** (+25% armor, immune to the first hit of any plasma burst) and unlocks the Salvage Tower (dmg 90). **Boss wrecks are worth 10×.**
- **Enemy reaction (scarier, tenet-compliant).** After 6 wrecks salvaged, `INTENT` adds **Sanitiser** drones (new enemy, 60 HP, air) that *fly to wrecks and detonate them*, denying the ore and damaging Yards, plus the fleet's `reentry` wave lands on Yards first. Escalation, not patience (Article V: the fleet is afraid and learns).
- **Real science.** Battlefield recovery was decisive in WWII (Germans lost ~70% of Panzers *knocked out but recoverable*; the Soviets recovered and repaired thousands). Urban mining; asteroid-metal logic. **SCI-FI (internal logic):** the Autofleet hull is a single-use "thrown friend" ship built of **warp-tolerant metamaterial** to survive the catapult; that material is why it is worth salvaging. The enemy's makers expected no recovery because their ship was meant to *die* where it landed.
- **Research.** New node `salvage` ("Reclamation Engineering", branch `auto`, tier 2, pre `robotics`, cost 180, **dang: true, sc 3**: handling alien material), combo `nanotech` for the Nanite Reclaimer (Yards collect without Crawlers).
- **Code.** `destroyEnemy` L6834, `spawnEnemy` L5581, `INTENT` L5119 (add Sanitiser wave), `COMP` L3180, `makes` loop L6306, `unitCost/comp` consumption L3832/L4635.
- **Build size:** L.
- **Tag:** TM1.
- **Tenet check.** The player gets stronger the harder they break the fleet. The cost is risk, not a cap; the enemy gets scarier in response.
- **Interactions.** Enemy Council (Sanitiser kind; they may want a "carrion drone" family), Ground (Crawlers), Society TM2 (Xenoalloy as the fleet's moral hazard: do you build with *their* metal?).

## 5. Alliances sought

- **Air & Sky Council:** Ammunition Trains and Autofoundry orders feed their 240 Falcons; rearm-speed depots are their ask.
- **Space & Orbital Council:** INDUSTRY-4's Orbital Resupply combo breaks the lift bottleneck they need, in exchange for them designing the rocket-part sinks.
- **Intel & Surveillance Council:** supply-level overlays and the forecast-sharpening slice; the rail net graph is theirs to read.
- **Enemy / Fleet Council:** I hand them my Harvester-target priorities and Sanitisers; they hand me a scarier enemy to justify every IC I make the player spend.
- **Society layer (TM2):** the reserve slider and Xenoalloy dilemma are my gifts to it.
- **Rivals:** the Fire Control and Strategic Strike lobbies, whose silos burn 46 strikes of stability and 0 IC of logistics; I propose munitions-tagged strikes, but they will have to argue for it.
