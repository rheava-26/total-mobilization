# Motions Register (Round 3)
*Compiled by the Clerk from Section 4 of every Round 2 paper. Find every motion that names YOUR council and answer it.*

---

## Motions filed by `air`

### MOTION A — "THE ARGUS NET" (Air + Space-Fleet)
Merge AIR-2 and SPACE-2 into one cueing network. **Mechanic:** a single node `datalink` (branch `energy`, pre `directedenergy`, tier 2, not dangerous; combo `orbitalcmd` for the satellite relay). The net has two node types sharing one code path: **Argus aircraft** (hp 190, STRATO ceiling, ic 380) and **Relay Sat "Argus"** (SPACE-2, 340 IC). Any friendly aircraft, AA gun or space unit within 1,100 px of a node is in the net. In-net units (a) share one designated target; (b) gain launch-on-remote, +20% effective missile range; (c) may join **SYNC FIRE**: a 6 s hold, then one same-tick volley whose travel times are corrected to land within 0.2 s, which saturates the enemy `pdBudget` (L8554/L8620). A net that loses all nodes falls back to solo targeting.
**Clerk's Annex.** *Numbers:* net radius 1,100 px; hold 6 s; range +20%; volley bonus +4% per member (no cap other than range). *Hooks:* `acquireIncoming()` L4465, `canIntercept()` L4464, `pdBudget`/`PD_SAT` L8554/L8620, aura pattern `helicmd` L3239 and `radar` L3406, air brain `updUnits()` L7809, `jammed()` L4970. *Size:* M-L. *Tag:* TM1. *Tenet:* a buff, and the cost is a real vulnerability (a visible node, a 6 s hold, jammable). Passes. *Science:* Cooperative Engagement Capability, Link-16, launch-on-remote (AIM-120).

### MOTION B — "THE FLEET HUNTS THE WING" (Air + Autofleet)
One coalition bill from AUTOFLEET-1, AIR-3 and AIR-5. **Mechanic:** AUTOFLEET-1's `airfieldRaid`, `hkwing` and chaffer stay as written, with my amendments, plus the `weasel` anti-radiation flight (AIR-3: it homes on emitters, and a 12 s EMCON order drops the lock at the cost of the aura). The player half ships in the same bill: **Hardened Shelter** (S building: +3 hangar slots, docked aircraft take 85% less damage and launch through a crater) and a **runway-repair** ability on Overwatch (`helicmd`), which cuts `runwayT` decay ×4 within 200 px. I withdraw my own Durandal bomber and my own Reaver spawn rules (see Section 5).
**Clerk's Annex.** *Hooks:* `scanEnemyIntel` L5737, `counterBias` L5513, `waveAirKind` L5524, `spawnEnemy` L5581, `rearmAircraft()` L4522 (base-pick loop checks `runwayT`), `BUILD_DEFS` airbase L3392, `AIR_SLOTS` L3818, `tickFleetStance` L5999. *Size:* M-L (about 350 lines). *Tag:* TM1. *Tenet:* the enemy gets scarier and the player's counters are built, never denied. Passes. *Science:* Durandal/JP233 runway denial, Operation Focus, AGM-88 HARM, hardened aircraft shelters, EMCON.

### MOTION C — "SORTIE SUPPLY" (Air + Industry)
Merge AIR-1 (tankers, forward arming strips) with INDUSTRY-1 (ammunition trains). **Mechanic:** Fuel is a separate stock from ammo. `tanker` "Pelican" refuels 12 fuel/s, ≤4 aircraft at a time, `fuelStock` 400; the `farp` strip and any depot-linked base rearm ammo 30% faster when `G.comp.munitions > 0`. Munition Works feeds both ground guns and air sorties, so IC flows into the air war, which gives the 212k unspent IC a real sink. Node: `logistics` (INDUSTRY-1, `pre: ['automation']`) with `airmobility` as its child (branch `age`, `pre: ['fusion','logistics']`).
**Clerk's Annex.** *Hooks:* `rearmAircraft()` L4522, `AIR_FUEL_BURN` L4511, `COMP` L3180 (cap change for `munitions`), `makes` loop L6306. *Size:* M. *Tag:* TM1. *Tenet:* supply is a bonus; nothing is starved or capped. Passes.

---

## Motions filed by `autofleet`

### Motion A — "THE COUNTER-AIR DOCTRINE" (addressed to: Air, Naval)

Merge AUTOFLEET-1, AIR-3 and AIR-5 into one doctrine. The scan gains `air` and `skyNavy` axes plus a pair read. The hostile roster is Reaver (hunts fighters), Weasel (anti-radiation) and Durandal (cratering bomber). Runway state is AIR-5's `runwayT`. Naval adds a sky-ship counter: the Flak Minelayer.

#### Clerk's Annex
- **Numbers.** `air = fighter bodies / 60`; `skyNavy = capital weight / 20`; pair read when both ≥0.8 and sum ≥2.0. Reaver wings of 6–10 (AIR-3 stats), Weasel flights of 4, Durandal pairs of 2 per airbase every 40s from phase 3. Minelayer: 4 mines, 120 dmg, radius 90 per drop.
- **Hooks.** `scanEnemyIntel` L5737 (axes), `counterBias` L5513 (`kindWeights`), `waveAirKind` L5524, `spawnEnemy` L5581, `rearmAircraft` L4522 (`runwayT` check).
- **Size.** M (~300 lines). **TM1.**
- **Tenet.** Pass: enemy-side only, no unit cap, mitigation is unlocked by building (shelters).

### Motion B — "THE RESOLVE ENGINE" (addressed to: Lore, Pacing)

One gauge, two inputs. **Fear** rises with the player's industrial output (AUTOFLEET-2's Fear Index; PACING-HOT's thermal alarm becomes an input). **Resolve** falls with fleet losses (LORE-2). Fear drives enemy strength and gambit weight. Resolve drives the bands, the planet-cracker and the "WE STOP IF YOU STOP" exit. Prep length is not touched.

#### Clerk's Annex
- **Numbers.** `F = clamp((icRate/300 + bldgTier/120 + armyWeight/200)/3, 0, 4)`, ratchet with 1%/min bleed. `t` multiplier `(1 + 0.08F)`; `OPEN_BOMBARDERS + floor(F)` (cap 9). Resolve bands as LORE-2 (100–60, 60–30, <30). Buried industry counts half (PACING-HOT).
- **Hooks.** `spawnEnemy` L5582 (`t`), `ESC` L5160, `G.supersPlan` L6493, prep loop L6492, `end()` L10345 (truce).
- **Size.** M (~350 lines). **TM1** (truce epilogue is TM2).
- **Tenet.** Pass: a real cost of success, enemy-side, ignoring it is the player's right.

### Motion C — "THE FLEET HAS A PLAN" (addressed to: Pacing, Space-Fleet)

Merge AUTOFLEET-4, PACING-1, PACING-3 and PACING-5. The fleet draws a gambit at match start. On breaking, the Loss Ledger chooses RETHINK or DESPAIR. If the flagship survives, it leaves and an Interbellum begins; the returning fleet runs a counter-doctrine against the top-killer family. Three doors end the war (rout, orbital denial, attrition).

#### Clerk's Annex
- **Numbers.** Gambits per PACING-3 (six). RETHINK if one family ≥50% of kills in the last 120s and losses ≥45%; DESPAIR otherwise or at ≥70%. Interbellum 45% of `prepTimer`, `it.cycles` invasion = 3.
- **Hooks.** `startWithdraw` L5990, trigger L6505, `INTENT` L5119, `phase3AllOut` L5937, `G.stats.killsByType`, `end()` L10345.
- **Size.** XL-lean L (~900 lines; mostly data tables). **TM1** (grudge flag is TM2).
- **Tenet.** Pass: the player gets more fortress time and a scarier fleet, never fewer toys.

---

---

## Motions filed by `bio`

**MOTION A — "THE LIVING BATTALION" (to: ground).** Merges BIO-2 (Blastema Protocol) into GROUND-1.
*Merged mechanic.* A Battalion of bio-family squads regrows bodies for MP (1 per 6 s, unhit 8 s); IC drafts remain for non-bio battalions. Gene Facilities shorten the interval: `6 ÷ (1 + 0.15 × facilities)`.
*Clerk's Annex.* Code hooks: `healSquad` L5029, `hitSquadMember` L5000, `spawnSquad` L4091, heal tick L7866, `UNIT_FAM` L3375. Numbers: 1 MP per regrown body, 6 s interval, 8 s no-hit window, 0.15 per facility. Size: M added to GROUND-1's L (~150 lines extra). TM1. Tenet: pure buff; MP cost is logistics, nothing capped. Interactions: Stability (ledger refund), Command (drafts).

**MOTION B — "HOT GROUND" (to: industry, science).** Merges BIO-3 (Radiotroph Gardens) with INDUSTRY-2 (Uranium tiers) and SCIENCE-2 (doctrine).
*Merged mechanic.* A Uranium Extractor at tier 3+ grants the Radiotroph Garden its yield multiplier ×(1 + 0.25 × tier), and completing both `radiotroph` and the Uranium cascade fires the doctrine **FALLOUT HUSBANDRY**: fallout chunks within 500 px of a garden yield IC and influence and heal bio units; taboo strikes still cost their full stability.
*Clerk's Annex.* Hooks: `SR` L3173, `srMult` L3810, `igniteFirestorm` (called L6567), STRAT L6878 (nuke/MIRV `taboo`), `finishResearch` L4703 for the doctrine. Numbers: +0.6 IC/s and +0.15 inf/s per active fallout chunk, ×(1 + 0.25 × tier). Size: M (~220 lines) on top of INDUSTRY-2 and SCIENCE-2 frameworks. TM1. Tenet: adds a reason to use a taboo strike without discounting it. Interactions: Strikes (stability ledger unchanged), Planet (haze offset).

**MOTION C — "THE BROOD PIT" (to: megaprojects).** Turns BIO-X (Immortal Line) into a MEGA-1 megaproject.
*Merged mechanic.* A multi-stage Brood Pit (4 sections: Vat Hall, Biomass Silo, Cooling Spine, Seed Chamber) plants Titan larvae with the BIO-X growth rules. Reaching Ancient stage on any Titan fires the tier-1 doctrine "THE IMMORTAL LINE". MEGA-3 Interdiction applies: the fleet reads the Pit and comes to kill it.
*Clerk's Annex.* Hooks: `PROJECTS`/`projState` from `megaprojects-wip`, `spawnUnit` L4253, `spawnSuper` L5269 (focus-fire), UNIT_DEFS titan L3294. Numbers: Pit 4 sections, ~900 IC each; larva 60 IC; growth rules unchanged from BIO-X. Size: L (~500 lines incl. visuals). TM1. Tenet: unlimited growth with MP upkeep, an armor-per-stage heat penalty, and a hunting enemy. Interactions: Autofleet (Fear Index biomass term), Energy (cooling spine reads heat).

---

## Motions filed by `command`

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

---

## Motions filed by `energy`

### Motion 1 — "THE THERMAL FIELD" (addressed to: physics, psi)

**Merged mechanic.** ENERGY-1, ENERGY-4, PHYSICS-1, PHYSICS-2 and PSI-2 become one system, owned jointly.

**Clerk's Annex**
- **One field.** `e.heat` 0-100 on every enemy and every `shifter`. One module, `heatApply(t, amount, src)`. All deposits (laser, plasma, particle, re-entry, nuke thermal, Lumen) call it.
- **Coefficients.** Deposit: beam `dmg*.9*fade`, plasma `dmg*1.4`, particle `dmg*.4`, antimatter flat 60, re-entry spawn 90 (Physics' formula replaces the flat value). Effect: kinetic x(1 + .5*heat/100), armour cut up to 40% (50% for AP and sabot rounds).
- **Cooling by medium.** Troposphere -10/s, stratosphere -6, mesosphere -3, space -1.2 (radiation, T^4). Physics' density function supplies the number. Land -5/s.
- **Blackout.** Physics' plasma-sheath rule at heat > 60 during re-entry. Lasers are damped by the sheath, so rods and rails are the answer.
- **Shapeshifters.** Psi's rule applies to the shared field: heat from enemy sources only. They keep their resistance, and enemy beams make them take extra damage (a scarier enemy, not a nerf).
- **Code hooks.** `hurt` L4971 (multiplier and armour term at L4985), `beamHit` L4964, `particleHit` L4965, `fire()` L4854-4881, `spawnEnemy` L5581, enemy update near L8759, `UNIT_DEFS.shifter` L3297, `jammed` L4970 for the IR bonus.
- **Size.** L (~450 lines, mostly per-weapon tagging).
- **Tag.** TM1.
- **Tenet.** Pure buff. No player cap, nerf or denial.
- **Research.** One node `thermalload` (branch energy, tier 2, pre `directedenergy`, cost ~150, not `dang`) plus Physics' `reentrytrack`. Combo with `plasma` and `particlebeam` raises deposit +35%.

### Motion 2 — "THE GRID" (addressed to: railgun, space-fleet)

**Merged mechanic.** One **Capacitor Bank** (RAILGUN-1 and ENERGY-2) and one **power pool** fed by Power Plants and Helios Arrays (SPACE-3).

**Clerk's Annex**
- **Pool.** `G.power` stores MJ, filled by Power Plants (+10 MW each) and Helios Arrays (+6 MW each). Capacitor Bank: 220 IC, 60 MP, stores 2000 MJ. A rail overdrive shot spends 1 charge (Railgun), and an energy emitter spends its `pwr` per shot (Energy).
- **No floor, no throttle.** Dropped from ENERGY-2. If the pool is empty an emitter fires at normal rate and bills the shortfall as IC debt at 0.6 IC/s per MW. Fire is never throttled. The bill is the cost (Article III permits upkeep).
- **Cooling.** The Radiator Array (110 IC) removes 25 waste-MW. Uncooled waste is billed at 0.4 IC/s per MW.
- **Code hooks.** Upkeep sum L6313-6323, `fire()` L4854, rate hook near L8244, `BUILD_DEFS` L3381, HUD upkeep L9937 and L9977, `UNIT_DEFS` L3217 for Helios.
- **Size.** M (~320 lines).
- **Tag.** TM1.
- **Tenet.** IC debt can never lock a weapon out, so nothing is capped.
- **Research.** `directedenergy` unlocks Capacitor Bank. `superconductor` halves draw and doubles pool (Railgun's own rule). `ssps` (Space) unlocks Helios.

### Motion 3 — "PHASED ARRAYS, METERED" (addressed to: science)

**Merged mechanic.** SCIENCE-2 Doctrine 1 (+3% damage per other emitter within 350 px, uncapped) plus the Grid and the Thermal Field.

**Clerk's Annex**
- **Rule.** The +3% stays uncapped. Each emitter's waste-heat upkeep is the ENERGY-2 bill, not a flat 0.4 IC/s. A 100-laser block pays 100 draws and is a single obvious power-plant project.
- **Heat synergy.** Phased emitters also deposit +15% heat when 10 or more are in range, so a laser wall paints a target faster. A reason to mass.
- **Code hooks.** `finishResearch` L4703, `fireDoctrine` (new), `beamHit` L4964, power pool as Motion 2.
- **Size.** S on top of the framework (~90 lines).
- **Tag.** TM1.
- **Tenet.** Uncapped synergy, a real IC cost. Passes.
- **Research.** The node is Science's `PHASED ARRAYS` (pre `directedenergy` + `automation`).

---

## Motions filed by `game-feel`

**Motion A — "The Long Shot" (to MEGA and RAILGUN councils).** Merge MEGA-2, RAILGUN-2 and GAME-FEEL-H: the superheavy gun fires with a built-in Warhead Cam, the Hush and a map-wide muzzle flash.
*Clerk's Annex.* On each shot (every 20 s), `timeScale` 0.2 for 1.2 s, trauma 0.9 (attenuated by distance, so players across the map feel a rumble), a 1.0 s bus-mute at impact, and a lagged bang (delay = dist_px / 2200 s, capped 0.6 s). Hooks: `updateCamera` L6649, `shake` L6675, `STRAT` L6878, `musDuck` L1298. Build size M, on top of GAME-FEEL-1/2/3. TM1. Tenet: the gun's rate and damage are untouched; cinematics skippable and off with `reducedMotion`. Interactions: Sim (single `G.timeScale`), Accessibility.

**Motion B — "Gun Voice" (to kinetic-arms).** Merge KINETIC-3 with GAME-FEEL-1 and GAME-FEEL-4: spool drives audio pitch, recoil and the Roar.
*Clerk's Annex.* Per-gun `spool` float (one float per gun, no new draw). Pitch = base × (1 + 0.5·spool) on `sfxAt`. Voice budget 24; guns beyond the budget contribute to the battle bed intensity instead of voices. Hooks: `fire()` L4855, `SFX.shoot` L1123, `updUnits`. Build size S–M. TM1. Tenet: strict buff; nothing capped.

**Motion C — "Show Your Cause" (to new-player).** Merge NEW-PLAYER-4 (Because Chip) with GAME-FEEL-2 (trauma): the chip and the shake carry one scale.
*Clerk's Annex.* A global `power` 0–12 scale on `feel.big(power)`; the chip reads the last event's power and cause ("Reaper-P hit: 6"), and the camera trauma uses the same number. Hooks: `hurt` L4971, `destroyEnemy` L6834, `shake` L6675. Build size S. TM1. Tenet: information and feel only.

---

## Motions filed by `ground`

### Motion 1 — "FIRE DIRECTION NET" (to: kinetic-arms, space-fleet)
Merges GROUND-3, KINETIC-4 and SPACE-2 into one time-on-target engine with two front-ends.

**Clerk's Annex**
- **Mechanic.** One delay solver, `solveToT(guns, point)`: each gun's flight time `t_i` is computed from its arc and the barrage fires gun `i` at `T - t_i` so impacts land within 0.25 s. Front-end A (kinetic-arms): *Fire Direction Centre* building (180 IC, 60 MP, hp 200), links guns within 900 px, cooldown 8 s, shell blast r 38 / 60% dmg. Front-end B (space-fleet): the *Relay Sat* net uses the same solver for orbital shot travel. Front-end C (ground): **Counter-battery radar** (240 IC, range 1,400 px) back-traces enemy `shell`/`rocket` arcs (`PS` L4892) and fires linked guns at the origin ±40 px, 2 s reaction. Salvo size unbounded; ammo from INDUSTRY-1 depots when present, base stats when not.
- **Code hooks.** `UNIT_DEFS` arty/mlrs/hydra L3189-3194, `acquireEnemyMulti` L4398, `PS` L4892, projectile fire in `fire()` L4865, strike-aim UI near `STRAT` L6878, `PD_SAT` L8620 for the orbital front-end, dead `G.designated` Fire Control plumbing.
- **Size.** L (about 600 lines, the solver is shared).
- **Tag.** TM1.
- **Tenet.** Pure power; the only limit is guns built and shells supplied.

### Motion 2 — "THE LIVING BATTALION" (to: bio, command)
Merges GROUND-1, BIO-2 and COMMAND-1's standing orders.

**Clerk's Annex**
- **Mechanic.** Squads of the same type muster into a battalion with pooled HP and body count (GROUND-1). Bio-family battalions regrow one body per 6 s ÷ (1 + 0.15 × Gene Facilities) at 1 MP when the pool has MP, else at IC cost via the draft depot (no new MP, Art. IV.1 respected). Non-bio battalions regrow only within 400 px of a Barracks/Veterans' Hall at IC cost, slower (1 squad per 6 s). COMMAND-1 gains the standing order `RAISE bn <type> N AT <line>`. A battalion below 40% strength is Shaken (−25% rate) until relieved.
- **Code hooks.** `spawnSquad` L4091, `SQUAD_LOADOUT` L3953, `healSquad` L5029, `hitSquadMember` L5000, `UNIT_FAM` L3375, `armyCap` L3803, heal tick L7866, `updUnits` L7809.
- **Size.** L (about 700 lines).
- **Tag.** TM1.
- **Tenet.** Adds regrowth and structure; costs are IC and cap weight, no hard caps. Uncapped number of battalions.

### Motion 3 — "PREPARED GROUND" (to: planet, naval)
Merges GROUND-4 (Engineer Corps), PLANET-2 (Delta Works) and NAVAL-X (Inland Sea).

**Clerk's Annex**
- **Mechanic.** Combat Engineers ("Sapper", 90 IC / 8 MP, hp 80) are the single builder for terrain-works: they lay mines (1 cell per 6 px, 140 dmg r 24, triple vs tracked/legged, zero vs air), dig tank ditches (existing SINK mode, enemy `gtank` stall 6 s), raise seawalls and polders (PLANET-2), and build Sluice Gate chains (NAVAL-X) at 60% of the listed IC when a Sapper battalion is present. Flood waves spare `amph` units (L8202). Enemy answers stay the fleet's job: `breacher` mine-rollers and `earthbreaker` ditch-crackers.
- **Code hooks.** `TERRAIN_MODES`, `fills[]`/`craterAt` L1492, `passable` L1536, flood rule L6561, `deployEmplacement` L4564, `hurt` L4971, `spawnEnemy` L5597-5620.
- **Size.** L (about 500 lines).
- **Tag.** TM1.
- **Tenet.** Adds toys and an IC sink; the player's own flooding is the only cost.

---

## Motions filed by `industry`

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

---

## Motions filed by `kinetic-arms`

### Motion 1 — to the Railgun Council (and Energy): "THE ONE LOCKER" (shared Smart Munitions)
**Merges** KINETIC-1, KINETIC-2, RAILGUN-3.
- **Mechanic.** One research node **Smart Munitions** (`def`, pre `pointdefense` + `automation`, cost 150 base ×1.6, not `dang`). It adds a per-group round selector to every kinetic gun (tank, SPG, AA, CIWS, Lance, Battlecruiser): **HE-frag** (default, r 30 splash 50%), **AP-sabot** (armour term ×0.35, +40% dmg on rail), **Airburst** (fuse at target range, `splashDamage` r 46, 80% dmg), **Guided HVP** (rail only, 25°/s turn, ×0.8 dmg). Non-default rounds draw `munitions` (INDUSTRY-1) at 0.04×dmg/50 per shot; empty stock reverts to HE-frag at full stats. Hailstorm and Sentry airburst gain +30 IC at upgrade.
- **Code.** `fire()` L4855 (add `rnd` argument), rotary branch L4883, `hurt()` L4981-4985 (single `pen` param), `updProj` L8899 (burst point), `splashDamage` L7546, `UPGRADES` L3321, `BUILD_DEFS.ammodepot` L3452, `RESEARCH` L3502.
- **Build size.** M-L (~400 lines). **TM1.**
- **Tenet.** Additive choices, default unchanged, shortage costs IC not stats.

### Motion 2 — to the Ground and Industry Councils: "FIRE FOR EFFECT, SUPPLIED"
**Merges** KINETIC-4, GROUND-3, INDUSTRY-1.
- **Mechanic.** GROUND-3's `firefinder` radar and Time-on-Target box are the fire-direction layer (node `firecontrol_net`); my Fire Direction Centre becomes its linking building (900 px link, 8 s barrage cooldown). Shells finally get a blast (r 38, 60% dmg). A barrage that lands from supplied guns (INDUSTRY-1) fires at +35% damage. No stability cost, so no strike drain.
- **Code.** L8447 dead `fireControl` branch, `UNIT_DEFS.arty/mlrs/hydra` L3189-3194, `fire()` shell arc L4865 and gravity L9137 (delay solver), `splashDamage` L7546, `COMP` L3180.
- **Build size.** L (~550 lines). **TM1.**
- **Tenet.** Mass artillery is rewarded; supply is the cost, empty is baseline.

### Motion 3 — to the Megaprojects and Railgun Councils: "ONE LONG GUN"
**Merges** KINETIC-X into MEGA-2/RAILGUN-2.
- **Mechanic.** KINETIC-X is withdrawn. The Long Gun keeps its section build and gains: (a) the shared round selector (Motion 1) with a sabot slot, and (b) the Annihilator rule, a Long Gun hit interrupts the 6 s beam charge, flagged in `spawnSuper`. Upkeep stays an IC cost. If you want the powder-and-light-gas science (HARP, SHARP), it becomes the *flavour text of the section build*, not a second gun.
- **Code.** `BUILD_DEFS` L3381, `spawnSuper` L5269, `weaponCeil` L4488, `megaprojects-wip`.
- **Build size.** M on top of MEGA-2. **TM1** (needs the framework).
- **Tenet.** Pure toy plus a scarier target, no cap.

---

## Motions filed by `lore`

**MOTION A — THE FLEET'S MIND. Addressed to: autofleet, pacing.**
Merge LORE-2, AUTOFLEET-2, PACING-HOT and PACING-5 into one gauge with two arrows and four doors.
- **Mechanic.** `G.fear` (ratchet, from AUTOFLEET-2's `F`) raises fleet scale and gambit weight. `G.resolve` (100 to 0, from LORE-2) falls with losses and changes doctrine. Bands: Resolve 60-30 speeds the phase-3 cadence by 25%; <30 permits DESPAIR (AUTOFLEET-4). Doors: Attrition (existing), Rout (flagship dead and Resolve <60), Orbital Denial (PACING-5), Truce (Resolve <20, invTime >=120s, `G.dangCap >=20`): one transmission, "WE STOP IF YOU STOP"; accept ends the match as `G.over='truce'`, refuse snaps Resolve to Desperate. PACING-HOT stays as information and scale, never as a clock drain.
- **Annex.** Hooks: `spawnEnemy` L5582 and `doWave` L5533 (scale), `startWithdraw` L5990, withdraw trigger L6505, `end()` L10345, `banner` L10335, `tickFleetStance` L5999. Size M (about 350 lines). TM1. Tenet: every effect makes the enemy scarier or adds a win route; Truce is voluntary. Interacts with MARKET-2 and PACING-2 (forecast), STABILITY-2 (the lie shifts Fear).

**MOTION B — THE DOSSIER LINE. Addressed to: science, industry.**
Merge LORE-3, SCIENCE-1 and INDUSTRY-H into one pipeline: wreck, sample, dossier, discount, salvage.
- **Mechanic.** Each enemy kind's wreck, collected within 500 px of a Research Lab or a Salvage Yard, yields a Sample (SCIENCE-1). A Sample unlocks (a) a research discount on studied nodes (-12%, floor 40%, SCIENCE-1), (b) a Codex dossier with a 3-sentence entry, +10% damage vs that kind (LORE-3), and (c) salvage value (INDUSTRY-H: hp/40 alloy, Xenoalloy 5% from heavy hulls). After 6 salvaged wrecks, Sanitiser drones start detonating wrecks and Yards. Boss wrecks always yield the dossier; only the carrier yields the `warp` sample (SCIENCE-X key).
- **Annex.** Hooks: `destroyEnemy` L6834, `resCostOf` L4663, `RESEARCH` L3502, `openAchievements` L1005 pattern for the Codex panel. Size L (about 450 lines, with the designer's text). TM1. Tenet: only adds power; the cost is Sanitiser risk. Interacts with Enemy (Sanitiser, Mirror echoes leave no wreck) and Achievements (analyst medals).

**MOTION C — THE EXERCISE. Addressed to: new-player.**
Merge LORE-4 Red Flag into NEW-PLAYER-6: the training sim is the lore reveal and the doctrine ledger.
- **Mechanic.** NEW-PLAYER-6's training framing for Guided mode, plus LORE-4's single drill wave pre-T-0, scored; its `killsByType` seeds the fleet's `threatWeight`. At T-0, "THIS IS NOT AN EXERCISE". Free +10% starting IC for a good score; the real fleet over-represents what you drilled against.
- **Annex.** Hooks: `babyFleet` L5189, `startTutorial` L11988, `threatWeight`, `banner` L10335. Size M (about 250 lines). TM1. Tenet: player gains IC; the enemy counters, which is a scarier enemy. Interacts with Enemy (counter-doctrine) and Onboarding.

---

## Motions filed by `market`

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

---

## Motions filed by `megaprojects`

### Motion 1 (to `railgun`, `kinetic-arms`): THE LONG GUN, ONE GUN

*Merged mechanic.* One structure, three tiers of the same barrel.
- **Early barrel (kinetic-arms).** Sections 1-3 are a light-gas stage (Bull gun), usable at once.
- **Long barrel (railgun + megaprojects).** Sections 4 and beyond are rail sections. Damage `100 x N`, range `+400 px x N`, both linear and uncapped. A Recoil Cradle every 4 sections (cheap IC) avoids +30% wear on section hp.
- **Meridian doctrine (railgun-X).** When 8 buried Rail Hub segments link to the barrel, barrel length includes tube length (one tier-1 doctrine).
- **Capstone.** At N >= 40 (my MEGA-H) the gun gains an exosphere ceiling and the `anchor` boss target.

**Clerk's Annex.**
- Numbers: section 380 IC, 40 MP, 16s; idle upkeep 6 IC/s per section; shot costs 40 IC per section; slew 1.5s per section across three arcs; scaffolding 25% hp until `done`.
- Code hooks: `BUILD_DEFS` L3381 (model `siegegun` L3414, `massrail` L3455); `canPlace` L3866 / `placeBuilding` L3884 (adjacency); `p.pierce` projectile path; `spawnSuper` L5269 (`anchor` kind); lifted `PROJECTS` stages from `origin/megaprojects-wip`.
- Build size: L (about 800 lines).
- Tag: TM1.
- Tenet check: passes; there is no section cap. The cost is upkeep and a target on the map.

### Motion 2 (to `space-fleet`): ORION, THE BUILD AND THE HULL

*Merged mechanic.* Orion is commissioned as a three-stage megaproject (Pusher Plate, Pulse Magazine of 10 uranium, Ascent 40s exposed in every band). The finished product is their Orion Battleship "Pusher" (4,000 hp, 4 turrets, hangar of 10 Strike Fighters). One node, `nuclearpulse` / `orion`, pre `orbitalcmd`, `dang: true` sc 8-10, `srReq: uranium`.

**Clerk's Annex.**
- Numbers: 1,400 IC, 80 MP; uranium 10; 1.4% stability per pulse.
- Hooks: `UNIT_DEFS` L3184 (model `dreadnought` L3298), carrier code L3260, `projReqProgress` new `{type:'uranium'}`, `STRAT` L6878 cost pattern.
- Build size: L (about 600 lines).
- Tag: TM1.
- Tenet check: passes; uranium is a real cost, never a cap.

### Motion 3 (to `autofleet`, `lore`): SITE UNDER SIEGE

*Merged mechanic.* Each active megaproject stage adds `heat` = IC drawn / 500. Heat adds to the Fear Index F (AUTOFLEET-2), and above heat 60 the planner sends an Interdiction wave plus a boss at the site via `spawnSuper(atX)`. Surviving a stage drops Resolve (LORE-2), and a finished megaproject drops it by a large step. A lost site refunds nothing.

**Clerk's Annex.**
- Numbers: wave strength `heat x diff x (1 + invTime/70)` every 10s; Resolve -8 per finished megaproject, -2 per defended stage.
- Hooks: `spawnSuper` L5269 (`atX`), `doSkyfall` L5206, `INTENT` L5119, `updateGameInner` L6288, `damageDistrict` L3702.
- Build size: M (about 300 lines).
- Tag: TM1.
- Tenet check: passes; a scarier enemy, never a nerf.

---

## Motions filed by `missiles`

### Motion 1 — "DARK SKY": High-Altitude Burst and Retrograde Debris
**Addressed to: physics, space-fleet.** Merges PHYSICS-3 (burst altitude), MISSILES-X (Starfish), SPACE-4 (Scattergun) and the Kessler overlap.
**Clerk's Annex.**
- **Mechanic.** One strike, `starfish` (IC 120, stab 20 taboo, cd 30 s), aimable only above `SPACE_Y`. 25 s EMP zone, radius 1,400 px: enemy electronics lose PD/weapons; your own `isMechanical` units and unhardened satellites in the zone also go dark. Zone leaves a debris record (PHYSICS-4 `G.debris`) that the Scattergun then sweeps, so the sequence is blind, then shoot, then clear.
- **Hooks.** `STRAT` L6878; `launchStrike` L6943; `strikeAimPoint` L6908; `detonateNuke` L7565; `G.blackout` (`detonateEmpBurst` L5864); `PD_SAT` L8529 `pdBudget`; `isMechanical` L4541.
- **Research.** One node `hemp` (energy, tier 2, `pre: directedenergy`, `bReq: uranenrich`, `dang`, sc 8). Combos: `shieldtech` (hardening; your own cost vanishes), `retrograde` (Scattergun).
- **Build size.** M (≈260 lines). **TM1.**
- **Tenet check.** New toy; the cost is self-inflicted attrition and stability, not a cap.

### Motion 2 — "THE DISPERSAL": Launchers under the sea and on the road
**Addressed to: naval.** Merges MISSILES-2 (TEL, Arsenal Ship, Boomer) with NAVAL-1 (Trident, Tomahawk boat) and NAVAL-5 (Ekranoplan).
**Clerk's Annex.**
- **Mechanic.** Naval owns the hulls; Missiles owns the magazine. Each launcher (`TEL`, `Trident`, `Tomahawk`, `Arsenal Ship`) has `strat: true` and a magazine of N rounds (TEL 4, Trident 8, Arsenal Ship 16). Reload from a Munition Works/Depot in range. Firing from a hidden hull reveals it for 3 s (NAVAL-1 rule). Strikes drawn from a launcher carry the same STRAT costs.
- **Hooks.** `UNIT_DEFS` L3184; `armSilo` L7660; `tickAutoStrikes` L7708; `spawnBuster` L7485 (enemy target priority); `acquireEnemy` L4374 hidden flag.
- **Research.** `sonar` -> `deepsea` (their nodes); TEL under `fusion`.
- **Build size.** L (≈600), split: naval 400, missiles 200. **TM1.**
- **Tenet check.** More launchers, no cap. Cost is magazine logistics.

### Motion 3 — "TIME ON TARGET": The salvo net
**Addressed to: space-fleet, kinetic-arms.** Merges SPACE-2 (CEC SYNC FIRE), KINETIC-4 (Fire Direction Centre) and MISSILES-1 (cruise ripple).
**Clerk's Annex.**
- **Mechanic.** One Fire Direction building/Relay Sat gives a single "TOT" command that computes launch delays so every linked gun, space unit, silo and cruise round impacts in the same 0.3 s window. The enemy's `pdBudget` is spent on the first arrivals and the rest hit raw. Cooldown 8 s; no stability (conventional rounds only).
- **Hooks.** `PD_SAT` L8529; `strikeEta` L6902 (already gives flight time per type; the delay is `maxETA - ETA`); `launchTargeting` stagger queue; `acquireEnemyMulti` L4398; `STRAT` L6878.
- **Research.** `fdc` (kinetic) + `cec` (space); partner `terrainnav` for the cruise leg.
- **Build size.** M (≈300). **TM1.**
- **Tenet check.** Pure power, with the 6 s prep window as its real exposure.

---

## Motions filed by `naval`

**Motion A. To the Missiles Council and the Air Council. "THE SILENT BOOMER"**
Merge MISSILES-2's Boomer and Arsenal Ship with NAVAL-1's depth band.

*Annex.* Mechanic: `u.depth` 0/1/2 on naval units; `hidden` units skipped for hostile `acquireEnemy` unless `u.revealT > 0` (set 3 s on firing, on surfacing, or by a `sonar` aura radius 600). Boomer: IC 600, 8 SLBM rounds, `strat:true`, reload at a Shipyard; Pike hunter-killer IC 230 as the escort; Tomahawk cruise-missile boat launches MISSILES-1 ripples. Research: `sonar` (def, tier 2, cost ~130, pre `pointdefense`), Boomer gated by `spaceage` + `uranenrich` as MISSILES-2 proposes. Code hooks: `UNIT_DEFS` L3184, `acquireEnemy` L4374, `armSilo` L7660, `bldSurfaceY` L3671, naval move L8123, render L9348. Size: L. TM1. Tenet check: pure addition; the cost is the enemy's ASW answer (NAVAL-4 Trench Hunter) and reload IC. Interactions: Missiles (strike dispersal), Autofleet (hunters), Air (subs are immune to bombers).

**Motion B. To the Planet Council. "THE NAVIGABLE MOAT"**
Merge PLANET-X's CANAL brush, NAVAL-3's Canal Lock and NAVAL-X's Inland Sea.

*Annex.* Mechanic: CANAL brush as PLANET-X (90 IC per 105 px, no cap). When a canal reaches an ocean, `waterBounds` merges the bodies so ships sail it. *Canal Lock* (IC 150) spans up to 240 px of land. *Sluice Chain* (megaproject, NAVAL-X) floods a valley for 60-90 s with a 10 s evacuation warning. Enemy answer: Amphibious Lander from PLANET-X plus Trench Landers. Code hooks: `doTerrain` L3911, `pushLevel`/`capLevels` L1504-1505 (exempt), `waterBounds` L3760 and cache `TERR_EDIT` L1470, drown rule L8217, flood rule L6561, `doSkyfall` L5206. Research: `geoeng` (PLANET-2) plus `harbourworks`; combos `automation` (dredger fleet) and `nanotech` (self-clearing). Size: L. TM1. Tenet check: uncapped terrain power for the player; enemy gets amphibious reply; the cost is the flood plain you drown. Interactions: Planet, Ground (non-amphibious tracks drown), Enemy.

**Motion C. To the Industry Council and the Planet Council. "THE DEEP DEPOSIT"**
Merge NAVAL-2's Blue Economy, INDUSTRY-2's cascades and PLANET-3's Deep Survey.

*Annex.* Mechanic: Seawater Adsorber (uranium), Nodule Dredger (steel and rare earth), OTEC (IC) each get the 5 development tiers (cost x3 per tier). Deep Survey reveals a seabed grade per water body, scaled by `waterBounds` width. Dredger gives rare earth meeting Antimatter's `srReq` without a land node. Code hooks: `SR` L3173, `BUILD_DEFS` L3381, `srMult` L3810 (fix boolean-vs-count read flagged by Industry), `G.owned`, `resourcesCaptured` L3608. Size: M. TM1. Tenet check: adds income and options, no caps; the cost is vulnerability at sea. Interactions: Industry, Planet, Enemy (Trench Landers), Strategic (cheaper strikes).

---

## Motions filed by `new-player`

### Motion 1 — ONE DIAL, FIVE FACES (addressed to: market, pacing)
**Merges** NEW-PLAYER-1, MARKET-2, PACING-2 and SCIENCE-2 Deep Survey (LORE-5 feeds it).

- **Mechanic.** One shared object, `G.forecast`, holds for each enemy class and each phase a mean and a band width `sigma`. Tier 0 is free and shows every class name, the four phase names and bands of ±40%. Radar and Data Center steps and Market's Forecast Desk each add an independent observer, so `sigma` shrinks as 1/sqrt(n) (Market's own statistic). A new `signals` node (branch `def`, pre `pointdefense`, ~90 base, not dangerous) adds a further observer. Combo `signals` + `automation` is Science's Deep Survey. The object has five renderers: the manifest rows (NEW-PLAYER-1), the phase strip, the Market positions panel, the PACING-2 prep beats, and the LORE-5 Wire headlines (their `conf` is read from `sigma`).
- **Annex.**
  - Code hooks: `tutReadyTick` L11796, `renderReadyPanel` L11720, `tutIntentBlurb` L11691, `scanEnemyIntel` L5737, `curIntent` L5187, `banner` L10335.
  - Build size: M (~320 lines of shared model plus rendering; each existing bill's own UI shrinks).
  - TM1 (Article IV.4 slice 2).
  - Tenet check: pure addition of information, no toy touched. Tier 0 hides nothing that is shown today.
  - Real science: ensemble forecasting variance of a mean, the NHC cone of uncertainty.

### Motion 2 — THE EXERCISE, ON THE BOARD (addressed to: lore)
**Merges** NEW-PLAYER-6 into LORE-4 Red Flag. I withdraw mine.

- **Mechanic.** LORE-4's three PREP drill waves (0.4x, 0.7x, 1.0x) replace the baby-fleet opening in Guided and Heavily Guided modes. A drill result banner teaches each concept: drill 1 shows the Reach Curtain on the bomber, drill 2 the re-entry window, drill 3 the landing flank. At T-0 the EXERCISE tag changes to THIS IS NOT AN EXERCISE and the full manifest from Motion 1 replaces the exercise one. In those two modes only, LORE-4's rehearsal-reading adaptation (`threatWeight`) applies at half strength, so new players are not punished twice. Veterans can opt out of the exercise and keep everything else.
- **Annex.**
  - Code hooks: `INTENT` L5119, `babyFleet` L5189, `TUT_MODES` L1011, `doWave` L5533, `banner` L10335.
  - Build size: L (LORE-4's ~600 lines; the removed baby-fleet code offsets ~60).
  - TM1.
  - Tenet check: the player is stronger at T-0 (LORE-4's bonuses) and the enemy escalates. Nothing denied.
  - Lore: consistent with Art. V (a scaled forward model from sparse telemetry).

### Motion 3 — THE PLAN NEVER STALLS SILENTLY (addressed to: command, industry)
**Merges** COMMAND-1 and INDUSTRY-3 Autofoundry with NEW-PLAYER-4 The Because Chip.

- **Mechanic.** One Plan panel, not two. Each ledger line shows a live status chip: "OK", "Blocked: needs 5 lift, Space Center +5", "Blocked: hangar full 18/18, Airbase +6", "Waiting: IC". A blocked line offers a "Build the fix" button, which adds the missing prerequisite line at the same priority (the Bureau fills it if IC allows). The chips reuse `unitReqUnmet`'s strings, so there is one source of truth for the refusal.
- **Annex.**
  - Code hooks: `unitReqUnmet` L3829, `airCap`/`airUsed` L3815–3819, `queueUnit` L4619, `placeBuilding` L3884, Command's planned panel near the building info HTML (~L10086).
  - Build size: S on top of COMMAND-1 (~100 lines).
  - TM1.
  - Tenet check: adds convenience and information, caps nothing.

---

## Motions filed by `pacing`

### Motion 1 — THE NERVE (to AUTOFLEET and LORE)

**Merges** AUTOFLEET-2 (Fear Index), LORE-2 (Resolve) and PACING-HOT (withdrawn as a clock drain).

**Clerk's Annex.**
- **Mechanic.** One panel, two needles. `G.fear` (0-4) ratchets up from IC/s, standing tier and army weight (AUTOFLEET-2's formula, unchanged) and scales the fleet **upward only**: `fleetMax += round(F·12·diff)`, `+floor(F)` bombarders, `t ×(1+0.08F)`. `G.resolve` (100→0) falls with kills (1 per line unit, 8 per boss, ÷fleetMax) and rises with cities lost. Bands from LORE-2 (Rattled: phase cadence +25%; Desperate: planet-cracker). **The prep clock is never touched.** Fear is computed during prep and shown as a rising ALARM bar (the dread), applied at arrival. Dispersal Doctrine (DEF node, cost ~200, not `dang`) cuts Fear growth by 20% and buried buildings count at half.
- **Code.** `fleetMax` at the prep→invasion flip L6492; `spawnEnemy` t-scale L5581-5582; `ESC.OPEN_BOMBARDERS` (L5160 area); `buildSupersPlan` L5301; `syncHUD` L11121; `startWithdraw` L5990; `end()` L10345 for the truce string.
- **Size.** M (about 330 lines combined). **TM1.**
- **Tenet.** Passes: difficulty is a scarier enemy; no toy is capped or denied. The clock is not shortened, which answers every Article III challenge.
- **Interactions.** Economy (unspent IC now has a cost to *hoard*, not to *build*), Stability (Maskirovka lowers Fear), Space (orbital kills move Resolve).

### Motion 2 — THE ONE FORECAST (to MARKET and NEW-PLAYER)

**Merges** MARKET-2 (Forecast Exchange), NEW-PLAYER-1 (Order of Battle), PACING-2 (Long Dark) and LORE-5 (Wire).

**Clerk's Annex.**
- **Mechanic.** A single forecast object `G.forecast` with one uncertainty `sigma/sqrt(1+n_obs)` (MARKET-2), counted per Radar, Data Center, Tracking or Signals node. It feeds four faces: the NEW-PLAYER manifest and plan strip, the MARKET desk bands, the PACING prep beats (five beats at 70/45/25/8/4% of prep: a rendered deep-contact point of light with brightness ∝ 1/d², a burn plume, the manifest, a split into flank tracks, the re-entry warning) and the LORE Wire headlines. Information only. The true `INTENT` and gambit are shown when `sigma<0.1`. One node, `signals` (def, pre `pointdefense`, ~90 base). Betting positions ship second.
- **Code.** `renderReadyPanel` L11720, `tutPrepFrac` L11419, `scanEnemyIntel` L5737, `curIntent` L5187, prep loop L6492, `banner` L10335.
- **Size.** M (about 450 lines combined). **TM1** (Article IV.4 slice 2).
- **Tenet.** Passes: strictly adds information.
- **Interactions.** Science (survey data as `n_obs`), Game-Feel (beat stingers), Enemy (the fleet's own `G.enemyIntel` mirror).

### Motion 3 — THE FLEET WILL RETURN (to AUTOFLEET and GAME-FEEL)

**Merges** AUTOFLEET-4's Calculated Retreat, PACING-1 (Second Siege) and GAME-FEEL-3's withdrawal Director.

**Clerk's Annex.**
- **Mechanic.** When the break fires with the flagship alive (Calculated Retreat), `startWithdraw(reason)` sets `G.cycle++` and begins a 45%-prep **Interbellum**. Survivors climb to a Molniya-type return arc (`T=2π√(a³/μ)`, shown on the HUD, ETA tightened by the One Forecast). The second fleet uses AUTOFLEET-4's Loss Ledger as its counter-doctrine and returns with `fleetMax×(1+0.6·cycle)`, `5+2·cycle` bombarders and phases compressed to 60%. `it.cycles = {invasion:3, others:1}`. Killing the flagship during any cycle makes the next withdrawal final. Letterbox and caption play on the final withdrawal only.
- **Code.** `startWithdraw` L5990, trigger L6505, end timer L6544-6546, `INTENT` L5119, `updateInvPhases` L6019, `tickCarrierArrival` L5401, `end()` L10345.
- **Size.** L (about 500 lines combined). **TM1.**
- **Tenet.** Passes: the enemy returns scarier, the player gets a gift of prep time, nothing is capped.
- **Interactions.** Space (survivors on the arc are targets), Economy (spend sink between cycles), Game-Feel.

---

## Motions filed by `physics`

### Motion 1 — "THE THERMAL LEDGER" — addressed to **Energy** and **Psi**
Merge ENERGY-1, ENERGY-4, PSI-2, PHYSICS-1 and PHYSICS-2 into one bill: one `t.heat` field (0–100) for every unit, enemy and friendly.

**Clerk's Annex**
- **Mechanic.**
  - Deposit and band table from ENERGY-1 (laser 0.9x, plasma 1.4x, particle 0.4x, antimatter +60). Cooling by medium as ENERGY-1.
  - Re-entry: ENERGY-4's heat 90 on `reentry`. Add the PHYSICS-1 **blackout**: while `heat > 60` and `reentry`, a pod cannot be retargeted, cannot be shielded or healed by outside units, and its `vx` is frozen. Interceptors and AA get +25% lead accuracy on a sheathed pod.
  - **Ballistic coefficient.** Per-kind `bc` (bombpod high, drones low) sets the terminal `vy` cap at L8631, so waves stratify by altitude.
  - Shapeshifters use the same field. Above 60 regeneration is off, above 90 speed is -30%. The Nanite Foundry vents heat.
- **Code hooks.** `hurt` L4971, `beamHit` L4964, `particleHit` L4965, `spawnEnemy` L5581, `updEnemies` L8631–8650, `jammed` L4970, `UNIT_DEFS.shifter` L3297.
- **Size.** M–L (about 350 lines). **TM1.**
- **Tenet check.** Every term is an addition to the player. The only downside is for Shapeshifters, and it is a speed penalty, not a damage nerf. Passes.
- **Research.** `directedenergy` hosts the deposit. New combo node `thermaldoctrine` (pre `directedenergy` + `pointdefense`, ~200, not `dang`) gives the readout and the thermal-vision toggle. PHYSICS-1's overlay rides `orbitalcmd`.

### Motion 2 — "DARK SKY, KNIFE BELT" — addressed to **Missiles** and **Space-Fleet**
Merge MISSILES-X, PHYSICS-3, PHYSICS-4 and the debris half of SPACE-4, SPACE-X and MARKET-6 into one high-altitude package.

**Clerk's Annex**
- **Mechanic.**
  - `starfish` strike as written (IC 120, stab 20, taboo), with `detonateNuke` L7565 rebuilt to split a burst by density: blast near the ground, thermal at mid-altitude, EMP above the thermosphere. The *same* nuke is therefore a three-way choice for the player, and Starfish is the exosphere option.
  - **One debris record**, `G.debris[]` of `{x, y, r, t}`, created by dead satellites, by the Shotgun lane and by Starfish. Use a flat damage aura (`+8%` taken by hostiles, per MARKET-6, plus `18 + 0.6t` per second for SPACE-X). Half-life by band: 12 s in the stratosphere, 40 s in the thermosphere, 120 s at SPACE_Y.
  - Hardening: `radhard` (MISSILES-X) also makes satellites immune to the debris record's friendly term.
- **Code hooks.** `detonateNuke` L7565, `STRAT` L6878, `launchStrike` L6943, `G.blackout` plus `empMul` L5902, `updEnemies` L8631, `resolveStyleAt` L6689.
- **Size.** M–L (about 400 lines). **TM1** (psychology events stay TM2).
- **Tenet check.** All player-facing costs are voluntary (taboo stability, hardening decisions). Nothing is capped. Passes.
- **Research.** `highaltburst` node as MISSILES-X, plus `retrograde` for the Shotgun. Combo `orbitalflak` (pre `orbitalcmd` + `pointdefense`) halves debris timers.

### Motion 3 — "THIN AIR" — addressed to **Science**
Merge PHYSICS-5 and SCIENCE-5 node 3 into one `ionoheat` megaproject.

**Clerk's Annex**
- **Mechanic.** Heater Array built in sections (megaproject tier). Each section raises `G.skyInflate`. While it runs, hostile anchored and loitering units above THERMO_Y sink at 6 px/s per section, uncapped. Reentry pods feel `density x (1 + 3 x skyInflate)`, so they glow earlier, which feeds the Thermal Ledger. Costs: radar range -25% (Science) and 2 IC/s per section (Science), plus our satellite decay. Satellites never move, so decay is HP only, restored by a cheap Reboost pulse. Aurora curtain is the HUD.
- **Code hooks.** `SPACE_Y`/`THERMO_Y` L1541, `updEnemies` L8631, `BUILD_DEFS` radar L3406, `society()` for the aurora tint, the `megaprojects-wip` branch.
- **Size.** L. **TM1.**
- **Tenet check.** No cap. The costs are radar range and IC upkeep. Passes.
- **Research.** `ionoheat` (space, tier 3, pre `spaceage` + `directedenergy` + `geosurvey`, cost ~430, `dang`, sc 8–10).

---

---

## Motions filed by `planet`

### MOTION P-1 — THE GREAT CANALS AND THE INLAND SEA (to: naval)

**Merged mechanic.** One water system: a CANAL terrain mode (PLANET-X) cuts channels, Canal Locks (NAVAL-3) let ships pass narrow land gaps, and Sluice Chains (NAVAL-X) flood a valley as a megaproject. All three share one `terrainVersion` bump, and the fleet answers with Trench Landers (NAVAL-4).

**Clerk's Annex**
- **Numbers.** CANAL brush: 40 px wide, 90 IC per 105 px, no per-edit cap. Lock: 150 IC / 40 MP, spans up to 240 px. Sluice Gate: 400 IC each, chain of 3 to 6 for a 600 to 1,200 px valley, flood runs 60 to 90 s.
- **Code hooks.** `doTerrain` L3911, `pushLevel` L1504, `capLevels` L1505 (exempt canal and flood edits), `waterBounds` L3760, non-amphibious drown L8200/L8217, `passable` L1536, `doSkyfall` L5206, `spawnEnemy` L5581.
- **Build size.** L.
- **TM1.**
- **Tenet check.** Passes. All new player power, no cap. The cost is that your own ground units cannot cross either (a real logistics cost). The enemy answer is scarier units, as the tenet asks.

### MOTION P-2 — THE CONTINENTAL RAIL GRAPH (to: industry, command)

**Merged mechanic.** One Rail Hub graph: hubs within 1,000 to 1,400 px link into a net. The net carries (a) shared component stock (INDUSTRY-4), (b) trunk bonus +40% to buildings within 120 px and the Molodets Train (COMMAND-2), (c) freight upkeep at 1.5 IC/s per 1,000 px (PLANET-1), (d) buried-tube immunity to surface barrage, and (e) the same tube segments as the Meridian barrel later (RAILGUN-X). One graph pass, one overlay, one set of weak points.

**Clerk's Annex**
- **Numbers.** Link range 1,200 px. Net adds +10% per hub pair (uncapped). Reconstruction ghost rebuild at 70% IC after 20 s. Node `railnet`, branch `auto`, tier 1, pre `automation`, cost ~100.
- **Code hooks.** `railhub` L3416, `railHubs` L7812, rail draw L9313, `recalcBuildings` L3762, `digLevel` L3863, `makes` loop L6306.
- **Build size.** L.
- **TM1.**
- **Tenet check.** Passes. Pure upside, with upkeep and a vulnerable network as the cost.

### MOTION P-3 — DEPOSIT GRADE, DEPTH AND CASCADE (to: science, industry)

**Merged mechanic.** Each node has a hidden `grade` 0.6 to 2.5 (SCIENCE-5 `geosurvey`), revealed by survey. Extractor tiers (INDUSTRY-2) multiply that grade. A deep dig layer on the Extractor (PLANET-3) adds ×2 yield and ×2 resource bonus. EGS Geothermal (SCIENCE-5) adds buried power plants.

**Clerk's Annex**
- **Numbers.** Yield = base × grade × (1 + 0.4 × tier) × (2 if dig level 2). One jackpot node per game with grade 2.5. Seismic Survey Station: 90 IC, reveals grades during prep.
- **Code hooks.** `SR` L3173, node spawn L3633 to L3636, `nodeNear` L3876, `digLevel` L3863, node draw L9290, minimap L9776.
- **Build size.** M.
- **TM1.**
- **Tenet check.** Passes. Strategic resources become bonuses, not gates, as the designer asked. A jackpot node is a landing target (the enemy's reason to go there).

---

---

## Motions filed by `psi`

### MOTION A — "THE HEAT BOOK" (addressed to: energy, physics)
**Merged mechanic.** One heat vocabulary for both sides. ENERGY-1's `t.heat` on enemies; PHYSICS-2's medium-based cooling; PSI-2's `u.heat` on friendly nanite-bodied units (Shapeshifters). Same bands (Warm, Glowing, Molten), same renderer tint, same `heatBonus()` function.
#### Clerk's Annex
- **Numbers.** Enemy bands per ENERGY-1. Friendly Shapeshifter: kinetic adds `0.02`/HP, explosive `0.05`/HP, energy `0.35`/HP. Regen off above 60. Slump above 90 (speed and damage x0.5, +40% damage taken). Vent `-6/s` idle, `-2/s` in combat, +50% when dispersed.
- **Code hooks.** `hurt(t,dmg,ix,iy)` L4971 and the armour term L4985. `beamHit` L4964, `particleHit` L4965. Regen at L7866 (replace global `allRegen` with a per-unit gate). `UNIT_DEFS.shifter` L3297. Art tint in the cached-art overlay pass.
- **Build size.** M (~300 lines, shared).
- **TM1.**
- **Tenet check.** Passes. Pure addition of a vulnerability the *enemy* can exploit. No player toy capped.

### MOTION B — "THE ANOMALY ENTRY" (addressed to: autofleet, lore)
**Merged mechanic.** Add a *psi axis* to AUTOFLEET-3's weakest-axis list and a psi term to AUTOFLEET-2's Fear Index. When `G.psiN` crosses 8, 24, 64, the fleet spends a psychology event on a counter: Hush-Hound pickets, then a Quiet-class dampener boss. LORE-2's Resolve ladder gets a "the fleet is afraid of what it cannot count" rung. Mind-Forge gets counterplay (orbital laser lattice absorbs 25% per node in the column).
#### Clerk's Annex
- **Numbers.** Hush field radius 240, halves psi fire rate and range. Hush-Hound 60 HP. Quiet 1,100 HP, radius 600, blanks Relay auras. Boss kill +10 stability as usual.
- **Code hooks.** `spawnEnemy` L5581, `spawnSuper` L5269, `INTENT` L5119, `EVT1`/`EVENTS_LIVE` L1078-1099, `STRAT.psilance` L6886, `firePsiLance` via L6969.
- **Build size.** L (~500 lines), shared across three councils.
- **TM1** with TM2 hooks into the psychology layer.
- **Tenet check.** Passes. The answer to a strong toy is a scarier enemy.

### MOTION C — "THE LISTENING" (addressed to: market, science)
**Merged mechanic.** One forecast surface. MARKET-2's Forecast Exchange sets the *price* of precision; SCIENCE-2/Deep Survey supplies the *ground truth*; PSI-X's Listening Chamber is the *active, costly* peek: 90 s of full wave and boss reveal for 18 stability cap, with a `listenDebt` the fleet reads back. Forecasts sharpen with all three.
#### Clerk's Annex
- **Numbers.** Chamber 600 IC / 80 MP. Listen: -18 cap for 90 s. Debt +1 per use; at 3 the Anomaly escalates one tier. Exchange reduces cost by 1 point per 2 positions held.
- **Code hooks.** `INTENT` L5119, HUD forecast panel, `psionicrelay` def pattern L3444, `announcePhase` L5683.
- **Build size.** M (~400 lines, shared).
- **TM1** (intel), TM2 (the "we stop if you stop" ending).
- **Tenet check.** Passes. Information and a stability price, no cap.

---

## Motions filed by `railgun`

### Motion 1 — "THE LONG GUN COALITION" (to: Megaprojects, Kinetic-Arms)
**Merged mechanic.** MEGA-2's chassis (section chain, curved track slew, upkeep) with my muzzle scaling and KINETIC-X's light-gas bore as a cheaper early ammo.
- Per section: +120 damage and +400 px range (linear, from MEGA-2). Light-gas bore ammo works from section 3 at 60% damage. Rail slug unlocks at section 6. Particle slug is the `particlebeam` era upgrade.
- Slew 1.5 s per section. Upkeep 6 IC/s per section idle and 40 IC per section per shot.
- The Recoil Cradle penalty from my RAILGUN-2 is struck. Nothing penalises a missing cradle.
- At N >= 40, MEGA-H's anchor boss gate applies.

**Clerk's Annex.**
- **Code hooks.** `BUILD_DEFS` L3381 (models: `siegegun` L3414, `massrail` L3455), `canPlace` L3866 and `placeBuilding` L3884, `fire()` L4855 rail branch L4879, `weaponCeil` L4490, `PD_UNINTERCEPTABLE` L8519, `RESEARCH` L3502.
- **Research.** One node `megastructures` (branch `space`, tier 4, ~450 x 1.6). Prerequisites `spaceage` and `colossus`. Combo `particlebeam` for the particle slug, and `supergun` ammo is a sub-unlock.
- **Size.** L (~650 lines). **TM1.** Needs the `megaprojects-wip` framework lifted, not merged.
- **Tenet check.** Passes. No section cap. The cost is upkeep and slew, which are logistics.
- **Interactions.** MEGA-3 interdiction, SPACE-2 (shot timing), ENERGY-1 (heated target takes more).

### Motion 2 — "THE POWER BANK" (to: Energy, Physics)
**Merged mechanic.** One **Capacitor Bank** building serving both beams and rails. Energy emitters draw from the pool as ENERGY-2 describes. Rail weapons within 420 px **overdrive** from the pool (x1.5 damage, x1.25 rate), with +1 bore wear per overdriven shot.
- Pool 2000 MJ, refilled at idle. A rail overdrive shot spends 20 MJ. A shortfall for beams **bills IC** (see section 6) and rails simply fire at baseline.
- **Rail Foundry** relines worn bores (180 IC, 1 alloy per reline, 6 s). A burned-out bore falls back to baseline, never below.
- Superconductor Facility halves draw and wear.

**Clerk's Annex.**
- **Code hooks.** Upkeep accumulator L6313-6323, building weapon loop L5764 and the aura hook near L8244, `BUILD_DEFS` L3381 and `lasertower` L3439, `superconductor` L3459.
- **Research.** Existing `directedenergy` (Bank) and a new `pulsedpower` node (branch `energy`, ~150 x 1.6) for overdrive and the Foundry.
- **Size.** M (~350 lines). **TM1.**
- **Tenet check.** Passes. Overdrive is an addition, wear never lowers baseline stats, and a shortfall costs IC rather than throttling.
- **Interactions.** Industry (alloy for relining), Physics (heat and power vocabulary).

### Motion 3 — "THE MERIDIAN SPINE" (to: Planet, Industry)
**Merged mechanic.** PLANET-1's Spine (Rail Hubs auto-linked underground) is also RAILGUN-X's barrel. A Spine chain with at least 8 Mass-Driver segments connected becomes the Meridian Accelerator: damage = 3 x tube length px / 10, one shot per 60 s, `pierce:true`, `ceil:0`. Cutting a tube section shortens the barrel. Freight and components flow along the same tube, and Spine upkeep is PLANET-1's 1.5 IC/s per 1,000 px.

**Clerk's Annex.**
- **Code hooks.** `railhub` L3416, `railHubs` L7812, `massrail` stack scan L8441, `UG_DEPTH` L3843, `fire()` pierce L4879.
- **Research.** PLANET-1's `deeprail` node (branch `auto`, ~220) plus my `Evacuated Tube Transit` as the combo (`spaceage`, `directedenergy`, `deeprail`, `dang` sc 6).
- **Size.** L (~700 lines on a shared tube graph). **TM1.**
- **Tenet check.** Passes. The cost is a vulnerable tunnel network, which is a logistics cost and an enemy opportunity.
- **Interactions.** COMMAND-2 (Molodets), INDUSTRY-4 (freight net), MEGA-3 (interdiction).

---

## Motions filed by `science`

### Motion 1 — THE WRECK CODEX (to: **lore**, **industry**)
*Merges SCIENCE-1, LORE-3, INDUSTRY-H.*
- **Mechanic.** One wreck object per kill (`hp/40` alloy, per Industry), one record per kind (`G.codex[kind]`). A wreck within 700 px of a Salvage Yard *or* 500 px of a Research Lab is "recovered": it pays alloy (Industry), unlocks the dossier (+10% damage vs kind, Lore; +8% family bonus at 4 dossiers) and grants one Sample (Science: −12% cost on nodes whose `studies` includes the kind, floor 40%). Xenotech nodes appear when the class sample exists. After 6 recoveries, Sanitiser drones hunt wrecks, so salvage is a front-line job.
- **Code hooks.** `destroyEnemy` L6834 (spawn wreck), `resCostOf` L4663, `RESEARCH` `studies` field, `buildResearch` L10926, enemy kind in `spawnEnemy` L5581.
- **Build size.** L (~450). **TM1.**
- **Tenet check.** Pure additions to the player; the cost is geography and a smarter enemy.

### Motion 2 — THE BREATHING SKY (to: **physics**, **space-fleet**)
*Merges SCIENCE-5 (`ionoheat`), PHYSICS-5, SPACE-4/SPACE-X (debris).*
- **Mechanic.** Node `ionoheat` (branch `space`, tier 3, pre `spaceage`+`directedenergy`, cost 380, `dang`, sc 8). Ionospheric Heater building: while running, every enemy in the space band descends 6 px/s per heater (stacking, uncapped) until it reaches the mesosphere (`MESO_Y`), where SAMs and railguns reach. Debris from kills in the heated band decays out too, answering Kessler without a cap. Cost: radar range −25%, 2 IC/s per heater. Friendly satellites are exempt (Article IV.2).
- **Code hooks.** `updEnemies` L8596, `SPACE_Y`/`MESO_Y` L1541, `BUILD_DEFS` radar aura L3406, `finishResearch` L4703.
- **Build size.** L (~500). **TM1.**
- **Tenet check.** Adds power. The costs are radar attenuation and power draw, both logistics. The fleet is invited to boost back up (Autofleet).

### Motion 3 — THE UNIFIED FORECAST (to: **market**, **lore**)
*Merges SCIENCE-2 doctrine 2, MARKET-2, LORE-5.*
- **Mechanic.** One HUD forecast. Research key `futures` (Market). Wire lines (Lore) are its text channel. Sigma starts at 1.0; each Radar, Data Center and Forecast Desk is an independent observer (σ/√n). Our capstone doctrine halves σ again and extends the horizon from the next wave to the next phase. At low confidence, 25% of lines are wrong, so forecasts matter without being gospel.
- **Code hooks.** `syncHUD` ~L11133, readiness pill L11713, `INTENT` L5119, `tickFleetStance` L5999.
- **Build size.** M (~250). **TM1** (TM2 society intel later).
- **Tenet check.** Pure information; nothing hidden today is hidden after.

---

## Motions filed by `space-fleet`

### Motion 1 — "DEAD SKY PROTOCOL" (to: Missiles, Physics)
**Merged mechanic.** Merge SPACE-4 (Scattergun lane), SPACE-X (Kessler Doctrine), MISSILES-X (Starfish) and PHYSICS-3/4 into one three-rung ladder: **Burst** (Starfish: EMP zone, hardened units survive), **Lane** (Scattergun: an un-interceptable pellet band), **Burn** (the Kessler Doctrine: 180s of debris belt, and your own lift offline). One shared **debris-belt object**, a stationary area (no moving satellites, Art. IV.2), created by any rung and decaying 5% per 20s. Dead sats of either side seed it.
**Clerk's Annex.** Numbers: belt damage `18 + 0.6×t`/s to hostiles, 25% friendly fire, Starfish 120 IC / 20% stab, Kessler +12% stab cost. Hooks: `STRAT` L6878, `launchStrike` L6943, `PD_SAT` L8620, `orbCap` L3812, `hurt` L4971. Size: L (~700 lines, shared object saves ~250). **TM1** (fleet reaction events TM2). Tenet: all player *options* with visible costs; hardening node `radhard` lets the player protect their fleet, and the fleet answers with +40% PD budget and faster grid re-seeding (a scarier enemy).

### Motion 2 — "ORION: THE SHIP AND THE PROJECT" (to: Megaprojects)
**Merged mechanic.** MEGA-4's three-stage construction (pusher plate, 10 uranium magazine, 40s exposed ascent) produces SPACE-5's Orion Battleship: 4,000 HP, armour 24, 4 turrets, hangar of 10 Strike Fighters, 2s pulse dash. The ship is a unit, can be lost, and can be built again at full price.
**Clerk's Annex.** Numbers: 1,400 IC, 6 rocket parts, uranium per pulse as MEGA-4 (1 per pulse, 1.4% stability). Hooks: `UNIT_DEFS` L3298 (`dreadnought` template), carrier code L3260, megaproject framework on `megaprojects-wip`, `STRAT` L6878. Size: L (~550). **TM1.** Tenet: toy with a resource cost, no cap; interdiction (MEGA-3) is the scarier enemy.

### Motion 3 — "ONE NET" (to: Air, Kinetic-Arms)
**Merged mechanic.** AIR-2 Argus, SPACE-2 Argus Net and KINETIC-4 Fire for Effect share one **Engagement Net** object: any unit with a link (AEW jet, Relay Sat, Fire Direction Centre) joins, and **SYNC FIRE** releases every linked shot with times-of-flight corrected to land within 0.2s. Saturation drains enemy `pdBudget` (L8620), and the volley bonus is +4% per linked unit.
**Clerk's Annex.** Hooks: projectile spawn, `acquireEnemyMulti` L4398, `PD_SAT` L8620. Size: M (~300 shared). **TM1.** Tenet: pure power with a 6s exposed charge as the cost.

---

## Motions filed by `stability`

### Motion 1 — To the Command and Lore Councils: "THE DECREES" (stability as a tempo budget)
Merges our Decrees (Hot Take), COMMAND-3 Shock Brigades, COMMAND-5 Urals Gambit's evacuation clause and LORE-1's Charter dial under one **Decrees** panel.
**Clerk's Annex.** Mechanic: three timed decrees, **Haste** (build/research/production time ×0.5, 60 s, −12% stability on issue and −0.15%/s), **Reprisal** (+25% fire rate, 45 s, −15%), **Evacuation** (re-place destroyed buildings at 50% IC with no build time, 90 s, −10% plus −3% per building). Each use inside 120 s multiplies the next cost by 1.25. Shock Brigades become the per-factory form of Haste inside the Commissariat aura. The Charter dial (Restrain/Standing/Unleash) sets the *unrest multiplier* each decree adds to `stabOffset` (Unleash ×1.8, Restrain ×0.5). Decrees are allowed at stability 0 (matching `STRIKE_STAB_FLOOR`, L6892). Hooks: `BUILD_DEFS` L3381 build timers, research tick L4692, `G.stab` writes (L6950 pattern), `dangCap` L4708, crisis L6635, strike-menu UI L7010. Size: M (~300 lines). TM1. Tenet: passes; the player gains power, paid in a recoverable resource, never capped.

### Motion 2 — To the Autofleet and Pacing Councils: "THE FLEET READS THE HOME FRONT"
Merges AUTOFLEET-2 Fear Index, AUTOFLEET-3 Weakest Axis, our Maskirovka Bureau and a rewritten PACING-HOT.
**Clerk's Annex.** Mechanic: the Fear Index `F` (AUTOFLEET-2) is computed from the same `G.enemyIntel` snapshot the fleet already reads (L5773). Maskirovka campaigns (Dummy Grid, Order of Battle) shave up to 25% off the *reported* inputs for 45 s; a second identical campaign inside 240 s gets half effect, and a third flags "propaganda detected" and the fleet swaps to blind carpet fire (a scarier-enemy answer, not a nerf). Dispersal Doctrine and Maskirovka share one node, `maskirovka` (def, pre `pointdefense`, cost 170 before ×1.6, not dangerous). PACING-HOT's prep drain is dropped; the alarm is information only. Hooks: `enemyIntel` L5773, `counterBias` L5509, `buildSupersPlan` L6493, `spawnEnemy` L5582, HUD L11133. Size: M (~350 lines). TM1. Tenet: passes; the enemy scales upward, the player's prep clock and toys are untouched.

### Motion 3 — To the Lore and New-Player Councils: "THE WIRE AS HOME FRONT"
Merges LORE-5 The Wire, NEW-PLAYER-3 The Ledger and our Information Policy Dial and news ticker.
**Clerk's Annex.** Mechanic: one feed of labelled systems headlines ("Casualty index 14 · Influence 1.8× · Flank 2 contested"), each carrying a hover receipt that lists every contribution to stability and influence (strike costs, ledger penalty, cadre/psion upkeep, city losses). The Information Policy dial (Free Press / Managed / Censored) alters the receipt: Free Press doubles swing sizes and regen and adds a 15 s earlier forecast; Censored halves hits but defers strike costs as a −2%/min bleed. Hooks: `stabRegen` L6332, influence L6360, `stratStab` L6890, `announcePhase` L5683, HUD cell L811/L11133. Size: M (~250 lines). TM1 (forecast slice of Art. IV.4); the full free-versus-command axis is TM2. Tenet: passes.
