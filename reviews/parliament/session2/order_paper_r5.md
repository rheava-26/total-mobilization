# ORDER PAPER, ROUND FIVE (Question Time and Caucus)

*Compiled by the Clerk. Neutral. Sizes S, M, L, XL; n/s = not stated. Clerk-coded IDs (sponsor gave none): ARMOUR-X, FORT-X, ARTILLERY-X, ENERGY-5, COMMAND-8, AUTOFLEET-X, UNDERSEA-X, CIVIL-X. Bills merged into a DOCKET bill appear only inside it. Inspector verdict is the latest on file; where only Round One exists, that charge stands.*

*Plain-unit rule (address2): units proposed, not yet in code, are marked PROPOSED.*

---

## 1. THE BILL LIST, BY BLOC

### GROUND BLOC

- GROUND-1 | The Battalion (Living Battalion) | ground | Idle squads auto-muster into a pooled-HP battalion that regrows to founding strength from Barracks drafts; Shaken penalty dropped. | L | Inspector: EXPLOIT, MONOCULTURE: draft draws MP when MP>0, excludes garrison, names enemy answer.
- GROUND-2 | The Main Line | ground | Main Line is computed from city positions with one Forward/Balanced/Deep dial; units beyond it fall back, then counter-attack. | M | Inspector: LINE: define rear as toward nearest city, per side.
- GROUND-5 | The Kill Zone | ground | Enemy ground units tagged STOP, PIN, KILL or BREAK take +15% per extra arm that touched them. | M | Inspector: CREEP: Autofleet ships a named ground escalation, not weight tweaks.
- GROUND-6 | The Opening | ground | A latched Shapeshifter marks its target EXPOSED, stripping 8% armour for all friendly fire. | S | Inspector: FALSE (about a 5% buff): flat 25% armour strip, or drop.
- GROUND-X | The Crawler Capital (hot take) | ground | Capital auto-relocates toward the quietest sector at 3 px/s. | XL | Inspector: CLEAN.
- DOCKET-11 | One Net | ground (kinetic-arms, space-fleet, air, missiles; naval) | One engagement net and solver auto-times artillery; SYNC FIRE auto-fires on boss-class targets; per-unit bonus deleted. | L | Inspector: Ground FALSE: shells and rails are uninterceptable (L8526), so "PD overload" is false; one TOT owner. Sky, Arsenal: CLEAN.
- ARMOUR-1 | The Hammer | armour | Idle tanks get +60% speed, +25% damage for 30 s toward a landing within 2,400 px; fleet answers with escorts and a 15 s dome. | M | Inspector: FALSE: MBT covers 528 px in 30 s; radius 500 px, or window follows unit to contact.
- ARMOUR-2 | The Anvil | armour | Tank shells ×1.5 vs heated targets, sabot ×1.3, enemy Ablative Carapace ×0.55 vs cold Leviathan. | S | Inspector: CREEP, duplicate of Four Walls: keep only plasma tanks as heaters; Carapace needs `thermalload`.
- ARMOUR-3 | The Siege Breacher | armour | Mammoth (340 IC super-heavy tank) Breach Shell ×3 vs anchored units, domes and buildings, range 360. | S | Inspector: CLEAN.
- ARMOUR-4 | The Recovery Yard | armour | Yard turns a dead tank's hulk into a new unit at 40% IC after 20 s. | M | Inspector: EXPLOIT, duplicate: one Yard; revived unit costs normal MP.
- ARMOUR-X | The Reconsolidation (hot take) | armour | 12 idle Mammoths fuse into a Land Dreadnought (2,400 HP, 13 IC/s upkeep). | M | Inspector: useless (Grand Arsenal sells it for 1,500 IC): refund the difference or drop.
- RIFLE-1 | The Cellar Clause | rifle | Infantry within 250 px auto-shelter in Rail Hubs or buried Barracks on a barrage warning, untargetable inside. | M | Inspector: CLEAN.
- RIFLE-2 | The Garrison Clause | rifle | Infantry in cities get deepest cover at once and add capture-reversal and city fire. | M | Inspector: CLEAN.
- RIFLE-3 | The Anvil (Suppression) | rifle | Infantry hits pin enemy ground (−35% move and fire); pinned targets take +25% from artillery, rail, bombs. | M | Inspector: CLEAN.
- RIFLE-4 | The Mimic | rifle | Shapeshifters (300 IC morphing swarm) pick a form by target; Disassembler latches armour ≥12. | M | Inspector: TENET, FALSE: keep air reach; Disassembler ≥1.2%/s on bosses; trigger on armour 6.
- RIFLE-X | The Levée en Masse | rifle | Housing Blocks raise free militia while a city is being captured; their MP income pauses. | M | Inspector: CLEAN.
- ARTILLERY-1 | High Explosive | artillery | Shells and rockets splash 60% damage (Thunder 36 px, Tempest 20 px). | S | Inspector: CLEAN.
- ARTILLERY-2 | Registered Targets | artillery | Batteries auto-fire at a descending lander's predicted impact point for 50% damage in the re-entry window. | M | Inspector: CLEAN.
- ARTILLERY-3 | Counter-Battery | artillery | Pelter (enemy plasma artillery) shots are tracked 6 s; artillery weights them ×4 and engages beyond acquire range. | S-M | Inspector: CLEAN.
- ARTILLERY-4 | Siege Round | artillery | Thunder shells ignore 60% armour on slow or anchored giants, +40% after beam hit. | S | Inspector: FALSE: Leviathan spd 11 fails the spd ≤8 gate; gate on ground bosses.
- ARTILLERY-X | The King's Thunder (hot take) | artillery | 10+ guns within 900 px auto-fire one time-on-target volley every 20 s. | M-L | Inspector: EXPLOIT, duplicate of SYNC FIRE: one TOT engine; named brigade-hunter enemy.
- FORT-1 | The Curtain Wall | fortifications | 25-IC, 600-HP wall stops enemy ground until broken; Gatehouse passes friendly units. | M | Inspector: LINE, PERF, EXPLOIT: walls as density cells; Breacher in same patch; wave-scaled enemy.
- FORT-2 | The Sapper Works | fortifications | Building seeds mines as per-cell density (90 damage), 3 IC/min upkeep; sweepers answer. | M | Inspector: CLEAN.
- FORT-3 | The Dome Lattice | fortifications | Overlapping shield domes pool HP and regen; damage split across them. | S | Inspector: FALSE, CREEP: `shieldBlocks` already sums in sequence; Disruptor ships with bill.
- FORT-4 | The Engineer's Yard | fortifications | Yard repairs DEF buildings 6 HP/s and rebuilds ghosts at 60% IC; Laser Towers heat targets for Bunkers. | M | Inspector: duplicate, FALSE: one Yard; Bunkers read the Thermal Field's heat.
- FORT-X | The Star Fort (hot take) | fortifications | Per-city megaproject: ring of walls, two bastions, +1 city-defence tier; landings stop to siege. | L | Inspector: LINE, CREEP: drop tempo effect, or bill it as Fear.

WITHDRAWN: GROUND-3 (into ARTILLERY-3 and DOCKET-11), GROUND-4 (into FORT-2).

### DEEP BLOC

- NAVAL-1 | The Silent Service | naval | Submarines auto-dive near enemy air, surface to fire, reload at a Pen; two states. | n/s | Inspector: OP, FALSE: name the enemy-side hook (L8682); reveal on fire within 300 px of enemy air.
- NAVAL-4 | Trench Landers (one sea family) | naval | Enemy Lander seeds Hunters that crawl the seabed; spawns on 5+ seabed buildings, 3+ ships or a Pen. | n/s | Inspector: CLEAN: Hunters killable on surface by any ship.
- NAVAL-6 | The Floating Works | naval | 40-IC Rafts let buildings stand on water; Tender repairs and rearms. | M | Inspector: CLEAN: rafts must set the needWater flag.
- NAVAL-7 | The Gunline | naval | Idle ships anchor at the water nearest a landing; long-range ships hit ground at ×1.0. | S | Inspector: FALSE, duplicate: no FRONT marker exists; cite real hook.
- NAVAL-8 | The Cataphract | naval | Enemy anti-ship frigate (4 salvos of 3×90) spawns in packs of 2-4 when the sky-navy axis reads ≥0.8. | S-M | Inspector: CREEP: salvo cooldown, one pack per 60 s, ship after Fleet Reads You.
- NAVAL-X | The Inland Sea (Sluice Chain) | naval | Standing order floods a span on landing; player units retreat; plain buildings get 10 s flash. | n/s | Inspector: TENET, EXPLOIT: default HOLD; 10 s HUD warning.
- PLANET-2 | Delta Works | planet | Seawalls and grid-powered pumps; failed pump floods after 30 s grace; ghost rebuild. | n/s | Inspector: CLEAN: Tidal Strike needs visible 6 s charge.
- PLANET-4 | Theatres | planet | Archipelago and Rift terrain generators. | S | Inspector: LINE, CREEP (unsatisfied): ship enemy shoreline rule first; delete cliff text.
- PLANET-5 | Mobilized Ground | planet | Haze becomes a labelled Fear-panel line; Zones form automatically. | n/s | Inspector: FALSE, MONOCULTURE, PERF (unsatisfied): drop stealth; key Zones on distinct categories.
- PLANET-6 | Overburden | planet | Rock cover from effective depth (b.y − groundY); RAISE brush adds elevation at 35 IC. | S-M | Inspector: EXPLOIT, MICRO: key Penetrator to depth; scale RAISE cost by volume.
- PLANET-7 | Shelf and Trench | planet | Shared depth function and SINK dredging make seabed sites; LEVEL reclamation default removed. | S | Inspector: TENET: leave LEVEL alone; minDepth as fraction of maxElev.
- PLANET-8 | Defilade and Crest | planet | Units behind a ridge crest are in defilade; crest units gain +15% range, +10% cover. | M | Inspector: FALSE (hedged), EXPLOIT: state existing terrain collision; price crest bonus by height.
- PLANET-X | The Canal | planet | Canal brush ships only after a headless test shows enemy ground can drown. | n/s | Inspector: CLEAN (old charge satisfied by the test gate).
- SUB-1 | The Deep Works | subterranean | SURFACE/AUTO/ABYSSAL burial setting; Abyssal layer at 150 px; Deep Foundry +20% to buried PROD within 400 px. | M | Inspector: EXPLOIT: best-foundry-only stacking; key Penetrator to quake and buster.
- SUB-2 | The Mantle Tap | subterranean | Geothermal tap +9 IC (+12 Abyssal); 3+ Taps heat neighbours −2% output each. | S-M | Inspector: CREEP: state the cost; tie yield to a sink or enemy target.
- SUB-3 | The Redoubt | subterranean | Deep Habitat housing cuts lost-city stability penalty 12% each to a 40% floor; Deep Lamps rule. | L | Inspector: FALSE, MICRO: price it; automate or drop Lamps; drop "+MP".
- SUB-4 | The Sortie Shafts | subterranean | Buried silos and airwings fire through hardened shafts; −15% strike IC, +25% sortie rate. | M | Inspector: FALSE: `launchStrike` never reads `ug`; keep only the numbered sortie boost.
- SUB-X | The Under-Line | subterranean | Underground rail lane with enemy Burrowers; losing a segment cuts rail 20 s. | XL | Inspector: FALSE, LINE: no underground unit domain; ship as event layer (L) or defer.
- UNDERSEA-1 | The Sea Floor | undersea | `seabed` buildings on water ≥30 px deep, sheltered from beams, orbital and ground fire. | M | Inspector: FALSE, EXPLOIT: flood exemption (L6568); depth-bomb bomblet; Bathyscaphe gate to 1.
- UNDERSEA-2 | The Heat Engine | undersea | OTEC (+9 IC) and Current Turbine (+3 IC) on the seabed. | S | Inspector: CLEAN.
- UNDERSEA-3 | The Deep Harvest | undersea | Dredger and Adsorber grant rare, steel, uranium bonuses scaling with count. | M | Inspector: EXPLOIT: stacking law, −4% per extra same-type source.
- UNDERSEA-4 | The Listening Floor | undersea | Hydrophone arrays (+6 s landing warning each) and Cable Landing stability. | M | Inspector: CLEAN.
- UNDERSEA-5 | Pens and Pressure Domes | undersea | Submarine Pen is a hidden strategic launcher; seabed Pressure Dome city; abyssal society −8% MP. | L | Inspector: TENET, EXPLOIT: drop −8% MP; Dome upkeep as share of income.
- UNDERSEA-X | The Drowned Capital (hot take) | undersea | Five-section megaproject (6,000 IC) moves the capital flag to the sea floor; beams lose 75%. | L | Inspector: CREEP: decapitation scan picks surface capital if richer; seabed-hunter flight.
- DOCKET-9 | Deep Deposits | planet (industry, naval, science, bio) | Deposit grade and depth; same-type sources stack by count; seabed nodules, OTEC, radiotroph gardens; tiers withdrawn. | M | Inspector: EXPLOIT (Deep); permanence (Economy): write stacking law; Extractor death clears `n.used`.
- DOCKET-13 | The Navigable Moat | planet (naval, ground) | Canal brush, locks, sluice chain; non-amphibious enemy landings drown; Trench Lander answers. | L | Inspector: FALSE, OP, LINE: ship enemy drown rule first; canal 35 IC; friendly infantry drowns too.

WITHDRAWN: NAVAL-2, NAVAL-3, NAVAL-5, PLANET-1 (into the Trunk), PLANET-3 (into DOCKET-9).

### SKY BLOC

- AIR-1 | Pelican Tanker | air | Tanker auto-orbits 600 px behind the wing and refuels bingo aircraft. | n/s | Inspector: CLEAN.
- AIR-4 | Shrike EW Jet | air | Standoff jammer drains enemy point-defence budget ×3; accuracy loss bottoms out. | S | Inspector: CLEAN.
- AIR-6 | The Heat Wing | air | Solaris (laser plane) and Warden heat targets; Falcon and Kite rockets cash in; auto-focus on hot targets. | S | Inspector: FALSE: Falcon rocket does 64, not 9, vs armour 22; drop the 2.2× claim.
- AIR-7 | Flak Belt | air | Enemy drops 6 proximity pods (34t airburst) on the densest air column once its air axis ≥0.8. | M | Inspector: CREEP: headless density test; damage 34+6t; sky ships targetable too.
- AIR-X | Pilots Are People | air | Aces, bail-outs, automatic helicopter rescue. | n/s | Inspector: CLEAN.
- SPACE-1 | Tinsel Decoys | space-fleet | 8-IC decoys auto-launch with satellites, one per three. | n/s | Inspector: CLEAN.
- SPACE-3 | Helios | space-fleet | Solar satellite earns 2.5 IC/s, +1 when auto-paired with one Power Plant. | n/s | Inspector: CLEAN.
- SPACE-4 | Retrograde Shotgun | space-fleet | Lane damage 12t+20 kills a 20t grid satellite in two lanes. | n/s | Inspector: CLEAN.
- SPACE-5 | Orion Battleship | space-fleet | Orbital forward base: fighters rearm and respawn on deck; auto pulse dash near bosses. | n/s | Inspector: CLEAN.
- SPACE-6 | The Cold Hull | space-fleet | Vacuum keeps enemy heat; armour strip on space hulls. | S-M | Inspector: FALSE: grid sits below MESO_TOP; scope to flagship, bombarder, annihilator.
- SPACE-7 | Dock Parity | space-fleet | Sky capital ships discount by Sky Docks (bug fix) and Space Stations. | S | Inspector: EXPLOIT: capital hulls floor at .6 (−4% per dock); explicit nerf accepted.
- SPACE-X | Kessler Doctrine | space-fleet | Opt-in 180 s thermosphere burn; own lift offline ~90 s while belt >50%. | n/s | Inspector: CLEAN.
- MISSILES-1 | Cruise Ripple | missiles | Terrain-following cruise rounds fired through standing posture. | n/s | Inspector: CLEAN.
- MISSILES-3 | HGV | missiles | 45-IC hypersonic ignoring armour, 220 vs domes. | n/s | Inspector: FALSE: `areaDamage` already skips domes; new job or drop.
- MISSILES-4 | PN and Seekers | missiles | All missiles default to dual seeker; cheap IR and anti-radiation packages. | n/s | Inspector: CLEAN.
- MISSILES-5 | Warhead Stockpile | missiles | Magazine depth bought with IC replaces strike cooldown; 0.2 IC/s upkeep per stored nuke. | n/s | Inspector: CLEAN.
- MISSILES-6 | Interceptor Grid | missiles | Anti-missile batteries within 500 px share targets and magazines; rearm ÷√N. | S | Inspector: CREEP: `PD_SAT` is static (L8536); write scaling or add saturating barrage.
- MISSILES-7 | Early-Warning Radar | missiles | +25% battery range, early engagement of re-entry ordnance, threat-axis arrow. | S-M | Inspector: CLEAN.
- MISSILES-8 | Strike Posture | missiles | Per-launcher Conventional/Reserve/Free; auto-strike picks the cheapest killer. | M | Inspector: CLEAN.
- MISSILES-9 | The Rod Magazine | missiles | Zeus (tungsten-rod satellite) holds 6 rods, recovers 1 per 20 s. | S | Inspector: TENET: delete; or burn alloy per lance.
- DOCKET-6 | Dead Sky Protocol | space-fleet (missiles, physics, market, energy, science; air, autofleet) | Burst (Starfish EMP by altitude), Lane (shotgun), Burn (Kessler) on one debris record. | L | Inspector: FALSE: two EMP definitions; unhardened aircraft go dark; one rule, price `radhard`.
- DOCKET-14 | Dispersal | missiles (naval, air) | Land and sea launchers (Boomer submarine, Arsenal Ship) share the stockpile; auto reload; subs hide from bombers. | M-L | Inspector: FALSE, OP, EXPLOIT, CREEP: correct hook; one shared magazine; fleet ASW answer.

WITHDRAWN: AIR Motion C Sortie Supply (into the Plan and Industry). Folded, not separate: AIR-2, SPACE-2 (DOCKET-11); AIR-3, AIR-5 (DOCKET-3); MISSILES-2 (DOCKET-14); MISSILES-X, PHYSICS-3 (DOCKET-6).

### ARSENAL BLOC

- KINETIC-1 | Fuse and Fragment | kinetic-arms | Programmable airburst flak with 46 px fragment splash. | n/s | Inspector: TENET (unsatisfied): keep airburst off units it weakens vs single targets.
- KINETIC-3 | Spool-Up | kinetic-arms | Rotary guns spool to +35% rate; rails and rods excluded. | S | Inspector: CLEAN.
- KINETIC-5 | The Finisher | kinetic-arms | Spall: kinetic hits on hulls at heat ≥70 splash 25% to neighbours; one `kinMul` ledger. | S | Inspector: CLEAN: spall via shared damage entry; railway term in `kinMul`.
- KINETIC-6 | The Rod Watch | kinetic-arms | Per-Lance stats; if median kills >12 hulls, enemy columns spread. | S | Inspector: CLEAN.
- RAILGUN-4 | Launch Rail | railgun | Standing RESEED toggle requeues dead satellites at −35% IC. | n/s | Inspector: CLEAN.
- RAILGUN-5 | Hypervelocity Family | railgun | Separate rail and rod adaptation meter at half gain. | S | Inspector: MONOCULTURE, CREEP: equal gain; fold into the shared table.
- RAILGUN-6 | The Silencer Wing | railgun | Each Long Gun section adds 3 Fear; every 10 sections bring a Silencer hull. | M | Inspector: CLEAN.
- RAILGUN-X | Meridian Accelerator | railgun | Trunk tube auto-fires at the highest-HP hostile; damage 3×tube px/10. | n/s | Inspector: FALSE: stated cost (vulnerable tunnel) has no code; give `quake` radius vs `ug`.
- ENERGY-3 | The Plasmoid | energy | Plasma bolt with 70 px heat burst; enemy-only heat. | n/s | Inspector: CLEAN.
- ENERGY-5 | Adaptive Hull and Thermal Stress | energy | Heat multiplies kinetic and explosive damage by mass class (×3.0 capital); medium and heavy hulls adapt per family; Quench Tender. | L | Inspector: FALSE, CREEP: one `dealDamage` entry; saturating curve; one 0.35 floor.
- ENERGY-H | The Furnace | energy | Heliostat mirror field auto-fires at the densest cluster; deposit ÷ thermal mass. | n/s | Inspector: CLEAN.
- PHYSICS-1 | Peak Heating | physics | Re-entry plasma blackout above heat 60 and ballistic-coefficient wave stratification. | n/s | Inspector: not re-audited; Thermal Field drop-window charge applies.
- PHYSICS-5 | The Breathing Sky | physics | Heater sections thicken upper air, pods heat earlier; bosses exempt; one toggle. | L | Inspector: CREEP, conditional: anchored units reboost with heater count.
- PHYSICS-6 | Thermal Mass | physics | Cooling ×mass^−0.33; heat above 100 becomes ablation damage. | S | Inspector: EXPLOIT, FALSE: one Solaris pins a capital; deposit ÷ live maxHp; drop plateau.
- PHYSICS-7 | The Column | physics | Killing flat-fire rounds carry 70% overkill to up to 3 hostiles within 60 px. | S-M | Inspector: CREEP, PERF: armour ≥8 absorbs carry; cap lookups per frame.
- DOCKET-1 | The Thermal Field | energy (physics, psi; railgun, game-feel, science) | `heat` on every enemy multiplies kinetic damage by mass class; cooling by medium; pods arrive hot; armour cut struck. | L | Inspector: FALSE, CREEP, MONOCULTURE: route splash through damage entry; enemy cold-plate wall; one multiplier.
- DOCKET-7 | Ammunition and the One Locker | kinetic-arms (railgun, industry, missiles, energy) | National munitions stock; supplied guns fire faster; empty stock means base stats; AP +40% vs medium-or-heavier; one standing doctrine per gun family, AUTO. | M-L | Inspector: MICRO, PERF satisfied; airburst weakness unsatisfied: keep airburst off single-target units.
- DOCKET-15 | The Power Grid | energy (railgun, space-fleet, physics, science) | One power pool; Helios; Capacitor Banks overdrive rails ×1.5 damage, ×1.25 rate; shortfall is IC debt. | M | Inspector: EXPLOIT, PERF: railway term into `kinMul`; cache adjacency; raise `busterScore` of massrail clusters.

WITHDRAWN: KINETIC-4, KINETIC-X (into DOCKET-10 and DOCKET-11); RAILGUN-2, RAILGUN-3 (into DOCKET-10, DOCKET-7); ENERGY-4 (into DOCKET-1); PHYSICS-2, PHYSICS-4. Folded: KINETIC-2, RAILGUN-1, ENERGY-1, ENERGY-2.

### ECONOMY BLOC

- COMMAND-2 | Metro-2 / Molodets | command | Rail-mobile strategic launcher auto-surfaces at the hub with the best lane; freight half withdrawn. | n/s | Inspector: not re-audited; Trunk MICRO charge fixed by auto-surfacing.
- COMMAND-3 | Shock Posture | command | Commissariat sets STEADY/STORM for its factories; storm cuts queue time to ×0.4 with strain ρ/(1−ρ). | M | Inspector: FALSE: clamp ρ ≤0.95.
- COMMAND-4 | Dead Hand and Cutting the Lines | command | Continuity strike fires itself once per crisis; command-layer enemy waves. | n/s | Inspector: CLEAN.
- COMMAND-5 | The Urals Gambit | command | Drag-relocate a coast's buildings: 30 s dismantle plus 12 s each, no refund. | n/s | Inspector: CLEAN.
- COMMAND-6 | The Directed Economy | command | `G.cmd` reading from Bureaus: Decree stability −20%, faster ledger scan, machine-tint sky. | M | Inspector: CLEAN.
- COMMAND-7 | OGAS, the National Network | command | Uncapped Node: +2 ledger lines per Bureau, +2% production, −0.4 stab cap, +0.2 Fear. | M | Inspector: FALSE, PERF: civic stab via `econ.stab`; one-pass ledger count.
- COMMAND-8 | Aura-Ring Bombing Rule | command | Enemy `busterScore` rises for clusters of Radar and Ammunition Depot auras. | S | Inspector: FALSE, CREEP-by-cliff: `auraAt` is additive; scale priority continuously.
- INDUSTRY-1R | Ammunition Trains, National Stock | industry | `munitions` stock (Works convert 6 IC to 1); every gun draws +35% damage, +15% rate; burn per shot. | M | Inspector: CLEAN.
- INDUSTRY-5 | Refinery Rows | industry | Steel Mill, Oil Refinery, Separator, Cascade each add +1% to their resource bonus. | M | Inspector: EXPLOIT, FALSE: floor `stratCost` at 5 IC; state prices; influence ≤6.
- INDUSTRY-6 | Reserve and Repair Depots | industry | Reserve Depot +60 component cap; Field Depot heals 4 HP/s within 300 px. | S | Inspector: CLEAN.
- MARKET-2 | Standing Posture (Forecast Exchange) | market | Auto-stakes 5, 10 or 20% of banked influence on forecast claims. | n/s | Inspector: TENET: pay winnings in influence; exempt stakes from `infSpent`.
- MARKET-3 | Bond Policy | market | OFF/WHEN BROKE/ALWAYS 500-IC tranches with debt service. | n/s | Inspector: EXPLOIT: bill service to the bank, not `icR`.
- MARKET-4 | The Underwriter (Lloyd's) | market | Premiums fund a 60% rebuild credit. | n/s | Inspector: CHARGED: ghost rebuild consumes the credit first.
- MARKET-5 | Letters of Marque | market | Contractors queued as normal units; per-kill bounties. | n/s | Inspector: CLEAN.
- MARKET-6 | Cached Commercial Sats | market | Auto-launched cached-art satellites feed the Debris Ledger. | n/s | Inspector: CLEAN.
- MARKET-7 | The Grand Bourse | market | Uncapped sections: +0.4 influence/s and market dividend each; THE RUN. | M | Inspector: FALSE: Run drains 1%/s of bank 40 s unless Sinking Fund; fix stab units.
- MARKET-8 | The Free Port | market | +1.5 IC/s per friendly hull within 800 px; Cable Landing +0.5 influence/s. | M | Inspector: CLEAN.
- MEGA-1 | The Ledger of Projects | megaprojects | Doctrines fire from `G.stats`; megaprojects are commissioned once and fed by building. | n/s | Inspector: MONOCULTURE (unsatisfied): doctrines reward the winner; equal thresholds.
- MEGA-4 | Orion (the project) | megaprojects (space-fleet) | Staged 4,000-HP nuclear-pulse capital ship with automatic burn. | L | Inspector: FALSE: no uranium stockpile exists; create one first.
- MEGA-5 | The Civic Block | megaprojects | Each finished project fires a society, stab cap, art and Fear term. | S | Inspector: FALSE: writes `mult.stab` (×30, negatives dropped); adopt `civCap`/`econ.stab`.
- MEGA-6 | The Orbital Ring | megaprojects | Anchors: +4 lift, +2% SPACE_Y per section, 8 IC/s upkeep. | L | Inspector: FALSE, CREEP: lift only; bank-billed upkeep.
- MEGA-7 | The One City | megaprojects | Arcology core and tiers: +6 MP, +5 cap, defence tier; capital sole decapitation target. | L | Inspector: FALSE, EXPLOIT: use `econ.cap`; fix stab units; keep −30 on core loss.
- MEGA-8 | The Aegis of Earth | megaprojects | Shield pylons every 600 px form a dome of 1,500 HP per pylon. | M | Inspector: CLEAN: route stab via `econ.stab`.
- MEGA-9 | The Continental Redoubt | megaprojects | Buried-Trunk sections add shelter tier; at 8 the capital counts as buried. | M | Inspector: CREEP, FALSE: ship Burrowers in text; 60% damage cut, not a flag.
- MEGA-10 | The Blue Belt | megaprojects | Seabed OTEC, hydrophone and cable cluster per section: +14 IC, +0.8 stab. | M | Inspector: FALSE: fix stab units; Bathyscapes belong to UNDERSEA-1.
- FOREIGN-1 | The Retainer | foreign | Contracts pay retainer IC per clump; influence a one-time fee refunded when lapsed. | n/s | Inspector: CLEAN.
- FOREIGN-2 | The Chancelleries | foreign | Five nations with delivery styles and quality profiles covering different damage types. | n/s | Inspector: CLEAN.
- FOREIGN-3 | Embassy Row | foreign | One Embassy per nation; Military Bases stack as parallel lanes. | n/s | Inspector: PERF: lanes enlarge one clump; allies use LOD and merging.
- FOREIGN-4 | The Counter-Coalition | foreign | Contracts count half in Fear; Interdictor (PROPOSED jamming platform) holds landings; risk-premium retainers. | n/s | Inspector: CLEAN.
- FOREIGN-X | Pax Armata (hot take) | foreign | 60,000-IC Treaty Organisation megaproject auto-spends a budget share on the biggest damage-type gap. | n/s | Inspector: CLEAN.
- DOCKET-5 | The Standing Plan | command (industry, market, new-player; air, bio, missiles) | Planning Bureau ledger auto-queues, places and refills; 6% tax; pre-filled lines; blocked chips; auto-"Build the fix". | L | Inspector: MONOCULTURE, TENET: bill tax to bank; pay Exchange on enemy variety; reserve 20% cap slice.
- DOCKET-8 | The Trunk | industry (planet, command, railgun; naval, ground, megaprojects, bio) | Self-linking Rail Hubs pool components, +40% linked industry, Reconstruction Corps, Launch Rail, Meridian. | L | Inspector: FALSE, EXPLOIT: bank-billed upkeep; clear `n.used`; "pair" means adjacent link.
- DOCKET-10 | The Long Gun | megaprojects (railgun, kinetic-arms, game-feel; physics, bio) | Uncapped sections; auto-fires at highest-HP target; Interdiction waves; N≥40 anchor boss. | XL | Inspector: FALSE, OP, LINE: heat per section placed; restate slew and shot cost; anchor HP scales with N.

WITHDRAWN: MARKET-1 (into the Plan), INDUSTRY-2, MEGA-H. Folded: COMMAND-1, INDUSTRY-3 (DOCKET-5); INDUSTRY-4R (DOCKET-8); INDUSTRY-H (DOCKET-16); MEGA-2, MEGA-3 (DOCKET-10).

### SOCIETY BLOC

- STABILITY-1 | Casualty Ledger | stability | Organic deaths lower stability; machines pay nothing; Veterans' Bureau. | n/s | Inspector: MONOCULTURE, FALSE: explicit predicate, not dead `isMechanical`; weight by `defWeight`.
- STABILITY-2 | Maskirovka Bureau | stability | Standing declaration spoofs the doctrine the fleet reads. | n/s | Inspector: EXPLOIT: doubt counts any declaration change inside 180 s.
- STABILITY-4 | Information Policy Dial | stability | Free, Managed or Censored, free to switch with 60 s settle. | n/s | Inspector: EXPLOIT: Free Press also speeds the fleet's adaptation scan.
- STABILITY-5 | The Civic Contract | stability | Each megaproject society tag carries one automatic political number set. | S | Inspector: CLEAN: adopt `civCap` fix.
- STABILITY-6 | The Rally and the Shock | stability | Fear bands add decaying cap bonuses (+3/+5/+7); Desperate makes city losses ×1.25. | S-M | Inspector: CLEAN (Clock: Shock moot, F≥3 needs 64× economy).
- STABILITY-7 | The Taboo Book | stability | Crisis lasts at least 10×k seconds. | S | Inspector: CLEAN.
- SCIENCE-2 | Synthesis Doctrines | science | Research combos fire permanent world rules; Phased Arrays (+3% per emitter), Stigmergy (swarm bodies 48 to 96). | M | Inspector: FALSE, PERF: Laser Tower is w 14; emitter-cluster priority; test 96 bodies.
- SCIENCE-3 | Containment by Depth | science | Dangerous-research unrest read from lab depth (×1.0, 0.7, 0.4). | n/s | Inspector: CLEAN.
- SCIENCE-4 | Crash by Default | science | Parallel slots withdrawn; one standing crash-funding toggle. | n/s | Inspector: CLEAN.
- SCIENCE-6 | The Fork | science | Combo node `integration` fires one standing choice, Ascension or Singularity. | M | Inspector: EXPLOIT: bonuses apply to a unit's primary family only.
- SCIENCE-7 | The Open Doors | science | Audit so every enemy has a counter. | M | Inspector: FALSE: file as dependency of Adaptive Hull; `tutquick` not found.
- SCIENCE-X | The Return Address (hot take) | science | 600 s megaproject throws a warp catapult back using a flagship sample. | n/s | Inspector: FALSE: sample drops at first Rethink; Rout conflict unanswered.
- LORE-4 | Red Flag (The Exercise) | lore | Three auto-run drill waves framed as training; veterans opt in. | n/s | Inspector: Society CLEAN; Clock FALSE: key drills on player families (needs attribution).
- LORE-5 | The Wire | lore | Fleet and home headlines; unconfirmed lines wrong with p=0.25×(1−conf). | n/s | Inspector: CLEAN.
- LORE-6 | The Masthead | lore | Each megaproject writes a Civic Tag and three Wire lines. | M | Inspector: CLEAN.
- LORE-7 | The Assay | lore | HUD FLEET ASSAY pill with live four-line receipt. | S-M | Inspector: CLEAN (Society); Clock: stale linear formula, fold into hover.
- LORE-8 | The Unaccounted | lore | Buried mass counts 50% in Fear; a Plumb hunter arrives below Resolve 60. | M | Inspector: CREEP: Plumb on a 120 s timer once ≥50%.
- PSI-1 | The Awakening | psi | Relay-limited awakening; Psi-Trooper (psychic infantry) 140 HP, 70 dmg, 300 IC; −0.8 stab cap each. | n/s | Inspector: EXPLOIT: 0.3 IC/s upkeep per trooper.
- PSI-3 | The Lumen | psi | Air-domain beam unit auto-climbs to firing altitude with a fuel cell. | n/s | Inspector: CLEAN.
- PSI-4 | The Hush | psi | Hush-Hound pickets spawn at psi thresholds 8, 24, 64; halve psi rate and range in 240 px. | n/s | Inspector: CLEAN: scale with `psiN`.
- PSI-5 | The Ingress | psi | Shapeshifters in contact with armour ≥12 latch automatically. | M | Inspector: FALSE, TENET: trigger on gtank; keep air reach; steal `counterBias` ×2.
- PSI-6 | The Low Ceiling | psi | Psi-Trooper ceil 0 to .60, Pylon to .66. | S | Inspector: FALSE: .62 for both; cite post-v3.91.1 match.
- PSI-X | The Listening Watch | psi | Standing toggle: Chambers listen the last 60 s of prep, cost cap meanwhile. | n/s | Inspector: CLEAN.
- BIO-1 | The Hyphal Reservoir | bio | Connected net holds a 12% shared HP buffer, ticked once a second. | n/s | Inspector: CLEAN.
- BIO-3 | Radiotroph Gardens | bio | Gardens turn fallout into IC, influence and healing. | n/s | Inspector: EXPLOIT: only enemy-origin fallout pays influence.
- BIO-4 | Directed Evolution | bio | Per-kind adaptation raises damage and cuts damage taken; antigenic drift strips 40% every 240 s. | n/s | Inspector: OP: damage taken ×0.97^n, never a linear subtraction.
- BIO-5 | Thicket Works | bio | Building plants a 600 px thicket ahead of ground threats every 60 s. | n/s | Inspector: CLEAN.
- BIO-6 | The Undercut | bio | Shapeshifters auto-dive and latch armour ≥12 targets from below. | M | Inspector: FALSE: threshold armour ≥5; drop "armour ×0.35". Ranked first of the five.
- BIO-7 | The Living Continent | bio | Five-section megaproject fires a verdant society; one standing choice. | L | Inspector: CLEAN.
- BIO-X | The Immortal Line and Brood Pit | bio | Titan larva grows by biomass with Kleiber upkeep; Brood Pit megaproject, sequential sections. | n/s | Inspector: EXPLOIT, MONOCULTURE: weight rises with stage; MP upkeep inert at 900.
- CIVIL-1 | The Reconstruction Depot | civil | Depot revives one dead district per 20 s at 60% HP for 60 IC. | M | Inspector: CLEAN.
- CIVIL-2 | The Siren and the Shelter | civil | Siren Net warns districts; Civil Shelter hardened basement. | M | Inspector: CLEAN: warning text stale.
- CIVIL-3 | The Ring Works | civil | City defence sums all works in its footprint; tier-3 Aegis Ring (PROPOSED). | S | Inspector: CLEAN.
- CIVIL-4 | The Mobilized City | civil | Districts adopt civic forms (factory, barracks, garden) from nearby buildings. | L | Inspector: arithmetic: dystopia −0.1 stab/s per city, not −0.4 per district.
- CIVIL-X | The Last Shelter (hot take) | civil | Continuity Bunker (PROPOSED, 1,200 IC) triggers a 120 s Remnant phase if all cities fall. | M | Inspector: contradiction: revive into a held city, not a dead one.
- DOCKET-12 | The Charter and the Decrees | stability (lore, command, science) | Restrain/Standing/Unleash dial per dangerous tech; standing decree orders; Crisis Stance. | M | Inspector: CLEAN (Economy, Society).
- DOCKET-16 | The Dossier Line | science (lore, industry; market, autofleet, bio) | Wrecks yield samples and black boxes; Yard or Lab auto-recovers; Sanitiser answers. | M | Inspector: PERF, EXPLOIT: one `G.codex[kind]` record; 20 s wreck TTL.

WITHDRAWN: STABILITY-3 (into LORE-1), LORE-H, PSI-2 (into DOCKET-1), BIO-2 (into GROUND-1). Folded: LORE-1 (DOCKET-12); LORE-2 (DOCKET-2); LORE-3 (DOCKET-16); SCIENCE-1; SCIENCE-5 (DOCKET-9, PHYSICS-5); Decrees (DOCKET-12).

### CLOCK BLOC

- PACING-2 | The Long Dark | pacing | Forecast renderer: prep plays as five beats. | n/s | Inspector: see DOCKET-4.
- PACING-3 | Gambits | pacing | Three opening gambits: Net, FOBS (from strip edges), Wedge. | n/s | Inspector: CLEAN (LINE charge satisfied).
- PACING-6 | The Fear Ledger | pacing | Fear arithmetic audited and printed on the panel. | S-M | Inspector: CLEAN: erratum, line `dmg` is literal; "1.09 per doubling" contradicts its formula.
- PACING-7 | The Siege Clock | pacing | Timing of huge-hull arrival and the anchor timer; HP from measured throughput. | S-M | Inspector: TENET: constant HP tables per intent; timer scales with `playerDefStrength`.
- NEW-PLAYER-2 | The Reach Curtain | new-player | Sky tint of reach bands while placing or selecting. | n/s | Inspector: CLEAN.
- NEW-PLAYER-3 | The Ledger | new-player | Display-only receipt, itemising the Alarm Panel's three inputs. | n/s | Inspector: CLEAN.
- NEW-PLAYER-4 | The Because Chip | new-player | Every refusal states why; optional show-me button. | n/s | Inspector: CLEAN.
- NEW-PLAYER-5 | The Post-Mortem | new-player | After-action rows including "Fear paid vs power gained". | n/s | Inspector: FALSE: `lostByType` has no timestamps; add ring-buffer `lossLog`.
- NEW-PLAYER-7 | The Fleet Follows Your Factories | new-player | Lesson card quoting Alarm Panel numbers. | S | Inspector: FALSE: "+9%, twice the guns" wrong; generate from live formula.
- NEW-PLAYER-8 | The Sub-Tabs | new-player | Chip row of building sub-categories; ALL default. | S-M | Inspector: CLEAN.
- GAME-FEEL-1 | The Mix Bus | game-feel | Positional audio, 24-voice budget, cached noise buffers. | n/s | Inspector: CLEAN.
- GAME-FEEL-2 | The Impact Budget | game-feel | Log-summed trauma shake and hit-stop; uniform sim step. | n/s | Inspector: CLEAN.
- GAME-FEEL-3 | The Money Shot | game-feel | One standing Cinematics Full/Short/Off choice. | n/s | Inspector: CLEAN.
- GAME-FEEL-4 | The Roar | game-feel | Battle bed and kill ladder driven by sim data. | n/s | Inspector: CLEAN.
- GAME-FEEL-5 | Legible Mass | game-feel | Baked team rim and team-coloured sparks; hit pip withdrawn. | n/s | Inspector: CLEAN.
- GAME-FEEL-6 | The Curtain Call | game-feel | Cached cue when Fear crosses a band. | S-M | Inspector: CLEAN: banner reads the live string.
- GAME-FEEL-H | The Warhead Cam | game-feel | Slow-motion cam on first three strikes or any above 40 stability cost. | n/s | Inspector: CLEAN.
- DOCKET-4 | The One Forecast | new-player (pacing, market, science, lore, psi, stability) | `G.forecast` bands ±40%/√(1+observers); observers are buildings; Listening Watch. | M | Inspector: FALSE, TENET: keep exact countdown, intent label, tier-0 count; bands for composition only.
- DOCKET-17 | The Fleet Will Return | pacing (autofleet, game-feel, lore) | Interbellum of 45% of prep, then a stronger second fleet with Mirror echoes; held for the President. | L | Inspector: CREEP, EXPLOIT: reset `invTime` and t each cycle; 2 cycles default; reset stab to cap.

WITHDRAWN: PACING-4, PACING-HOT (stays withdrawn), NEW-PLAYER-6 (into LORE-4). Folded: PACING-1 (DOCKET-17), PACING-5 (DOCKET-2), NEW-PLAYER-1 (DOCKET-4).

### OBSERVER DELEGATION (AUTOFLEET)

- AUTOFLEET-5 | The Standing Answer | autofleet | One Posture setting (Open, Hardened, Dispersed) routes fleet events to automatic answers; 8% IC upkeep above Open. | S-M | Inspector: TENET: free auto-EMCON and repair; upkeep only on Dispersed, per buried building.
- AUTOFLEET-6 | The Alarm Panel | autofleet | Fear needle 0-4 on a log formula, three input bars, live exchange readout. | M | Inspector: FALSE, TENET: clamp terms ≥0; track current input, no ratchet; quote live numbers.
- AUTOFLEET-7 | The Four Walls | autofleet | Basalt, Prism, Bastion, Thornback heavies each take ×0.35 from one weapon family. | M-L | Inspector: CREEP, FALSE: ship after Thermal Field; stamp family once at `fire()`; reset t.
- AUTOFLEET-X | The Mirror | autofleet | Echoes of your top-killing unit, ≤25% of own count, max 24 per drop. | n/s | Inspector: FALSE, CREEP: echo damage flat ×0.8; needs killer attribution.
- DOCKET-2 | The Nerve | autofleet (lore, pacing, stability, megaprojects; psi) | Fear scales the fleet; Resolve drives bands; four doors: Attrition, Rout, Orbital Denial, Truce. | L | Inspector: FALSE, EXPLOIT: Resolve needs ×100; Denial needs fleet ≤50%; Decisive Strike stays a blackout.
- DOCKET-3 | The Fleet Reads You | autofleet (air, naval, command, stability, psi) | Scan axes for air, sky navy, command, psi; Reaver, Weasel, Durandal, Hush-Hounds; auto EMCON; Maskirovka. | L | Inspector: CREEP, FALSE, EXPLOIT: wings scale with volume; add sky-navy axis; continuous thresholds.

WITHDRAWN: Recruiter Strike clause (AUTOFLEET-3). Folded: AUTOFLEET-1, AUTOFLEET-3 (DOCKET-3); AUTOFLEET-2, AUTOFLEET-4 (DOCKET-2).

### CROSSBENCH (Independents; codes assigned by the Clerk in paper order)

- LANCH-1 | Weigh the Ship, Not the Head | lanchester | Landing and defence scores weight unit value, not headcount. | S | Inspector: CLEAN.
- LANCH-2 | The Proportional Reserve | lanchester | Rail reserve splits by enemy HP near each hub, not winner-take-all. | M | Inspector: CLEAN: cache hub weights.
- LANCH-3 | Armour That Counts | lanchester | Smooth hit multiplier 2.7 to 1.9 so research armour matters. | S | Inspector: TENET: armour 4-5 units take more; use min(current, lerp).
- LANCH-4 | The Square-Law Dividend | lanchester | +25% fire rate at about 100 neighbours. | M | Inspector: CREEP: add density metric to `scanEnemyIntel`.
- LANCH-5 | The Salvage Standard | lanchester | Every kill refunds 3 + 6% maxHp IC, capped 400. | S | Inspector: CLEAN.
- LANCH-6 | The Fleet Plot | lanchester | Optional building shows fleet size, ETA and DPS-to-HP ratio. | S-M | Inspector: CLEAN (duplicates Alarm Panel).
- LANCH-X | Abolish the Army Cap (hot take) | lanchester | Remove the cap; 0.25 IC/s upkeep per weight. | L | Inspector: EXPLOIT, PERF, FALSE: deficit mothballs dearest units; keep MP price.
- ARC-1 | The Standing Order | arcade | Each production building holds an optional standing unit order above an IC reserve. | S-M | Inspector: CLEAN.
- ARC-2 | Wreckage | arcade | Wrecks drop IC; Salvage Yard claims 2/s at 60%. | M | Inspector: CLEAN.
- ARC-3 | Deep Seams | arcade | Extractors built at depth 1-2 yield ×1.8 and ×3. | M | Inspector: FALSE, OP: explicit `ugAllowed` carve-out; use ×1/×1.4/×2.
- ARC-4 | Drop Forecast | arcade | Landing zone pre-rolled; Radar shows markers; rail troops pre-route. | S | Inspector: CLEAN: needs new rail hook.
- ARC-5 | The Decoy City | arcade | Lure Beacon (PROPOSED) lowers defence score by 5 within 250 px. | S | Inspector: scale: multiply ×0.5 within 250 px.
- ARC-6 | Spoils of War | arcade | Each boss kill grants a persistent ~4% stat bonus. | S | Inspector: CLEAN.
- ARC-X | Turn the Fleet (hot take) | arcade | Subversion Array flips dying robotic enemies, 15-35% chance. | L | Inspector: FALSE, OP: flip into ally units, 15 IC, one per 2 s per array.
- LAB-1 | The Clear Shot Act | laboratory | Beam damage ×1.5 in vacuum, ×1.0 baseline. | M | Inspector: CLEAN: band flag, not per-beam geometry.
- LAB-2 | The Orbital Shells Act | laboratory | Satellites sweep by shell; LEO default. | L | Inspector: TENET: parking stays default; LEO opt-in.
- LAB-3 | The Early Warning Act | laboratory | IR Station shows impact ellipse, +15% AA range vs re-entry. | M | Inspector: CLEAN (duplicates MISSILES-7).
- LAB-4 | The Pulsed Power Act | laboratory | Capacitor Bank charges ×2.5 fire rate for `elec` weapons. | M | Inspector: FALSE: auras add, not multiply; `elec` lists units only.
- LAB-5 | The Low Observable Act | laboratory | Radar cross-section field shrinks detection range; Sensor Drone answers. | M | Inspector: CLEAN.
- LAB-6 | The Power Beaming Act | laboratory | Power Sat +1.2 IC/s to nearest Power Plant. | M | Inspector: CLEAN (duplicates DOCKET-15).
- LAB-X | Kessler Doctrine (hot take) | laboratory | Orbital kills raise a debris counter hurting orbital units; armour ≥6 ignores. | L | Inspector: CREEP: debris hurts only hostile or unhardened units.
- BOOK-1 | The Dispatches | bookshelf | Correspondent lines from stats, one per ~40 s. | S | Inspector: CLEAN.
- BOOK-2 | The Enemy Speaks | bookshelf | Persistent INTERCEPTED panel of last five fleet lines. | S | Inspector: CLEAN.
- BOOK-3 | The Breaking Point | bookshelf | Flagship death: +45 s, grids drop, enemy +20%. | M | Inspector: FALSE: `wallGrid` deploys once; use `fleetMul` and a delay.
- BOOK-4 | The Last Clause | bookshelf | Epilogue templates from society, era, cities held, strike use. | S | Inspector: CLEAN.
- BOOK-5 | The Regime | bookshelf | Four events with 2-3 choices (±10% cap, ±150 IC); default pick on timeout. | M | Inspector: CLEAN: modal needs real-time default.
- BOOK-6 | The Long Dark | bookshelf | 40 s quiet, then three simultaneous waves. | S | Inspector: CLEAN: `waveClears` missing; title collides with PACING-2.
- BOOK-7 | The Witnesses | bookshelf | Lost cities recorded with name, time, killer for the report. | S | Inspector: CLEAN: plumb damage source.
- BOOK-X | The Second Fleet (hot take) | bookshelf | At 30% fleet left, an Elder fleet 1.2× arrives. | L | Inspector: FALSE: `G.fleet` falls at spawn; trigger on withdrawal stance.
- MISCH-1 | The Counter-Battery Act | mischief | Above 20 strikes per 60 s the fleet fields Interceptor Frigates (PROPOSED). | M | Inspector: CLEAN.
- MISCH-2 | Cheap Is Not Free | mischief | Culling Fleet (PROPOSED area platforms) scales with cheap bodies. | S | Inspector: FALSE: count live bodies by `swarmBodies`.
- MISCH-3 | The Blind Spot Act | mischief | Dominant killer family (≥35% of kills over 120 s) doubles counter weights. | M | Inspector: CLEAN: credit read at kill time.
- MISCH-4 | Burrowers | mischief | Burrower (PROPOSED digger) spawns by buried IC share. | L | Inspector: CLEAN, duplicate of SUB-X.
- MISCH-5 | The Garrison Mandate | mischief | Garrison upkeep sub-linear as a Fortress Index rises. | S | Inspector: FALSE: upkeep is linear; index = garr/(garr+combat).
- MISCH-6 | The Escalation Clause | mischief | Stalemate 120 s commits a doubled reserve wave. | S | Inspector: FALSE: stalemate is a draw; fund from fresh `fleetMax` slice.
- MISCH-7 | Boss Fatigue | mischief | Boss-kill stability halves per same-type kill in 120 s. | S | Inspector: TENET: withdraw (bosses are scheduled).
- MISCH-X | The Grudge Ledger (hot take) | mischief | Three-entry cross-run memory decaying 25% per game. | M | Inspector: CLEAN.
- STREET-1 | The Conversion Order | street | Per-city CIVIL/WAR/TOTAL footing gives IC from districts. | M | Inspector: CREEP: footing weights the landing score.
- STREET-2 | Ration Books | street | FREE/RATIONED/DIET: +25% or +50% MP for −4 or −10 cap. | S | Inspector: CLEAN.
- STREET-3 | The Shelter Programme | street | Reconstruction Yard restores a dead district per 20 s. | M | Inspector: EXPLOIT: no rubble window or `ct.dead` guard; adopt CIVIL-1.
- STREET-4 | The Workforce Draft | street | Mobilization slider trades civilian growth for manpower. | S-M | Inspector: MONOCULTURE: sits at 100%; tax stability regen.
- STREET-5 | The Wireless | street | Turns on `EVENTS_LIVE` with 5-8 real-condition events. | M | Inspector: CLEAN.
- STREET-6 | Scrap Drive | street | Scrap Depot turns scrap into alloy. | M | Inspector: CLEAN (alloy cap 60).
- STREET-7 | The Evacuation | street | Standing Evacuate halves city stability loss, stops recruiting. | S | Inspector: FALSE, OP: trigger per skyfall or bombardment tick.
- STREET-X | The People's Army (hot take) | street | TOTAL MOBILIZATION edict: free militia per district every 20 s. | L | Inspector: OP, PERF, MONOCULTURE: militia into `combatWeight` or pay upkeep.

WITHDRAWN: none.

---

## 2. THE PRESIDENT'S DIRECTIONS, MAPPED

**Direction 1, the damage-type matrix** (heat, shields, armour, plasma, kinetic).
- DOCKET-1, ENERGY-5: heat multiplies damage by mass class; medium and heavy hulls adapt per family.
- AUTOFLEET-7: four enemy heavies, each deaf to one family (floor 0.35).
- PHYSICS-6, ENERGY-H, ENERGY-3: heat deposit and cooling by mass; plasma burst.
- KINETIC-5, RAILGUN-5, ARMOUR-2, ARTILLERY-4, AIR-6, SPACE-6, FORT-4: each weapon family's cash-in on heat.
- MISSILES-3 (dome breaker), FORT-3, ARMOUR-3: shield layer; DOCKET-7 (AP vs medium-plus).
- FOREIGN-2, FOREIGN-X: nations whose doctrines fill the weakest damage type.
- LANCH-3, PACING-7: armour research, arrival timing.
- **Gap:** no bill adopts the Speaker's shield physics (magnetic field stops plasma and missiles, passes lasers and kinetic; Bombarder shield plus armour). AUTOFLEET-7's Prism resists beams, the reverse. Armour is near-inert (Section 5), so the Inspector says the wall must come from HP and heat.

**Direction 2, ammunition as upgrade.** INDUSTRY-1R, DOCKET-7 (empty stock means base stats), MISSILES-5, MISSILES-4, KINETIC-1.

**Direction 3, allies.** FOREIGN-1 (retainer), -2 (five nations), -3 (embassies), -4 (enemy answer), -X (treaty); MARKET-5 (contractors).

**Direction 4, ideology-shaped research.** **NO bill answers it.** Nearest: COMMAND-6 (Directed reading, perks not research), MARKET-7, SCIENCE-6, SCIENCE-4; FOREIGN-1's annex sketches a fourth TM1 slice and defers it.

**Direction 5, research reveal / lead-time research / yes-or-no outcomes.**
- Reveal: **NO bill.**
- Lead-time research: **NO bill.** Nearest staging: MEGA-1, SCIENCE-X.
- Yes or no: DOCKET-12 (dial per dangerous tech), SCIENCE-6 (choice on completion), DOCKET-2 (Truce accept or refuse), BOOK-5, STREET-5, BIO-7.

**Direction 6, warhead stockpiles.** MISSILES-5, DOCKET-14 (shared stock, Boomer subs), MISSILES-8, UNDERSEA-5 (Pen), NAVAL-1, SUB-4, COMMAND-2, DOCKET-5 ("stock Z warheads" line), MISSILES-9.

**Direction 7, more megaprojects (every one):**
- DOCKET-10 Long Gun, MEGA-1 Ledger (framework), MEGA-5 Civic Block (rules).
- MEGA-4 Orion, MEGA-6 Orbital Ring, MEGA-7 One City, MEGA-8 Aegis of Earth, MEGA-9 Redoubt, MEGA-10 Blue Belt.
- MARKET-7 Bourse, COMMAND-7 OGAS, BIO-7 Living Continent, BIO-X Brood Pit, UNDERSEA-X Drowned Capital.
- FOREIGN-X Treaty Organisation, FORT-X Star Fort, GROUND-X Crawler Capital, SCIENCE-X Return Address.
- PHYSICS-5 Breathing Sky, RAILGUN-X Meridian, ENERGY-H Furnace, DOCKET-8 Trunk, SPACE-5 Orion ship.

**First Address items.**
- **The Line:** GROUND-2, GROUND-5, RIFLE-1/-2/-3, ARMOUR-1, ARTILLERY-1 to -4, PHYSICS-7, FORT-1/-2, PLANET-8, BIO-5, NAVAL-7. Holes: GROUND-2 "rear", FORT-1 Gatehouse, FORT-X.
- **Shapeshifters on the ground:** RIFLE-4, GROUND-6, PSI-5, BIO-6, DOCKET-1 (heat). Society Inspector ranks BIO-6 first, RIFLE-4 second, "change nothing" third, PSI-5 fourth, GROUND-6 last.
- **The underground:** SUB-1 to -4, SUB-X, RIFLE-1, PLANET-6, MEGA-9, DOCKET-8, SCIENCE-3, LORE-8, ARC-3, MISCH-4, CIVIL-X, AUTOFLEET-5.
- **Underwater:** UNDERSEA-1 to -5, UNDERSEA-X, NAVAL-1/-4/-6, PLANET-7, MEGA-10, MARKET-8, DOCKET-9/-13/-14.
- **Defensive stations:** FORT-1 to -4, FORT-X, MISSILES-6/-7, INDUSTRY-6, CIVIL-2/-3, MEGA-8, COMMAND-8.
- **Fear Index:** AUTOFLEET-6, PACING-6, LORE-7, NEW-PLAYER-3/-7, GAME-FEEL-6, STABILITY-6, PLANET-5, FOREIGN-4, RAILGUN-6, LANCH-6, DOCKET-2.
- **Blatantly overpowered today:**
  - Sky capital-ship price bug: SPACE-7 (explicit nerf, accepted by Sky Inspector).
  - Air plus sky navy: enemy answers DOCKET-3, AIR-7, NAVAL-8, AUTOFLEET-7.
  - Psi reach: PSI-6 (nerf; Society charges FALSE).
  - Aura stacking: COMMAND-8. Rod chain: KINETIC-6, MISSILES-9. Mass-Driver ×5.8: DOCKET-15 (open).
  - **No bill answers:** `allRegen` gate, Superhuman Cadre upkeep, enemy air `dmg` flat (Sky, Society charges unanswered).

**Directions no bill answers:** ideology-shaped research; research reveal; lead-time research. Partial: Speaker's shield physics.

---

## 3. OVERLAPS (groups that should merge)

1. **Auto-rebuilders:** ARMOUR-4, FORT-4, DOCKET-8 Reconstruction Corps, MARKET-4 ghost credit. Share: rebuild dead structures at 40-70% IC. Differ: tanks, DEF buildings, any linked building, insurance.
2. **City-district revival:** CIVIL-1, STREET-3, CIVIL-X. Share: revive districts, lower `lostStructures`. Differ: depot, yard without guard, bunker phase.
3. **TOT and volley engines:** DOCKET-11 SYNC FIRE, ARTILLERY-X. Share: one-second volley. Differ: boss targets vs artillery brigades.
4. **Heat multipliers:** DOCKET-1, ENERGY-5, PHYSICS-6, KINETIC-5, ARMOUR-2, FORT-4, ARTILLERY-4, AIR-6, SPACE-6. Share: heat boosts kinetic hits. Differ: ×1.5 vs mass-class ×3.0; three constants in play.
5. **Enemy family walls:** ENERGY-5 Adaptive Hull, AUTOFLEET-7 Four Walls (Basalt ×0.35), RAILGUN-5, ARMOUR-2 cold hull, LANCH-3. Share: resist the family you spam. Differ: learned meters vs fixed kinds.
6. **Shapeshifter latches:** RIFLE-4, GROUND-6, PSI-5, BIO-6. Share: latch armour ≥12. Differ: form-switch, armour strip, melee, dive approach.
7. **Hidden launchers:** NAVAL-1, DOCKET-14, SUB-4, UNDERSEA-5, COMMAND-2. Share: strikes from unreachable sites. Differ: sub, buried silo, Pen, train.
8. **Decapitation immunity:** UNDERSEA-X, SUB-3, MEGA-9, MEGA-7, PLANET-6, CIVIL-X, STREET-1. Share: make the capital hard to hit. Differ: sea floor, buried, one city, rock cover, bunker.
9. **Burrowers:** SUB-X, MISCH-4, LORE-8 Plumb. Share: enemy tunnellers. Differ: rail event layer, buried-IC share, timer.
10. **Fear panels:** AUTOFLEET-6, PACING-6, LORE-7, NEW-PLAYER-7, GAME-FEEL-6, LANCH-6, STABILITY-6. Share: show the fleet's growth rule. Differ: needle, ledger, pill, lesson, cue, plot, rally.
11. **Wreck salvage:** DOCKET-16, ARC-2, LANCH-5, STREET-6. Share: wrecks pay. Differ: samples, IC, flat refund, alloy.
12. **Forecast and warning:** DOCKET-4, ARC-4, MISSILES-7, LAB-3, UNDERSEA-4, BOOK-1/-2. Share: advance notice. Differ: bands, landing marker, radar, IR, hydrophones, prose.
13. **Standing build orders:** DOCKET-5, ARC-1. Share: auto-queue. Differ: ledger with tax vs per-building order.
14. **Second siege:** DOCKET-17, BOOK-X, AUTOFLEET-X, MISCH-X. Share: the fleet returns or remembers. Differ: cycles, Elder fleet, echoes, ledger.
15. **Power:** DOCKET-15, LAB-4, LAB-6, SPACE-3, UNDERSEA-2, SUB-2. Share: IC or rate from structures. Differ: pool, banks, power sat, Helios, OTEC, geotherm.
16. **Levies:** RIFLE-X, STREET-X, STREET-4, CIVIL-4. Share: citizens become troops. Differ: during capture, edict, slider, barracks-block.
17. **Evacuation and relocation:** STREET-7, CIVIL-2, COMMAND-5, DOCKET-12 Evacuation decree. Share: move people or industry. Differ: citizens, shelters, industry, decree.

---

## 4. QUESTIONS FOR QUESTION TIME

**Ground Staff**
- ARMOUR-1's window is 30 s and radius 2,400 px; a Mammoth at speed 5 covers 240 px. How does the hammer reach the landing? (to Armour)
- Which new ground enemy answers ×3.5 on the line (Kill Zone ×1.45, RIFLE-3 +25%, ARMOUR-2 ×1.5, sabot ×1.3), and when does it appear? (to Autofleet)
- One Yard or two? Is a wall a building or a cell? (to Fortifications and Armour)

**Rifle Corps**
- Disassembler needs armour ≥12, but gtank is 6. Which enemy tank does it fire at, and what is its dps next to a shifter's 38? Why strip the air reach `plasma` has today (L4494)?

**Artillery Corps**
- Siege Round gates on spd ≤8; the Leviathan is spd 11. Which enemy is the bill for?

**Fortifications Corps**
- FORT-3 says domes do not stack, but `shieldBlocks` already returns the first live dome, so 20 domes are 18,000 HP in sequence. What changes, and what is the Disruptor's spawn rule?

**Undersea Works**
- `L6568` sets hp = 0 on any non-needWater building under SEA_E − .04. How does a seabed building survive its own placement?

**Planet**
- PLANET-7 removes LEVEL's reclamation while PLANET-8 and PLANET-6 add ridges. What stops RAISE being the best defensive purchase at 35 IC?
- Who owns the enemy shoreline rule, and when does it ship relative to the Moat?

**Subterranean Works**
- `launchStrike` (L6951) never reads `ug`, and the Airwing is `autoBury: 1`. What does SUB-4 add?

**Subterranean and Undersea**
- The Redoubt, the Drowned Capital and Overburden each cancel Decapitation. Which one survives?

**Space Fleet**
- SPACE-7 leaves `max(.22, 1 − .09(n−1))`; ten Sky Docks cost 4,400 IC and restore the 180-IC Railgun Ship. Accept a .6 floor for capital hulls?

**Air Staff**
- Withdraw the "armour wall" claim and the 2.2× in AIR-6 after reading L4987-4991? Why does Round 4 say nothing on Bill 3's wrong half, the 8-parked gate or the sky-navy axis?

**Missiles**
- MISSILES-9 caps a weapon your own Section 4 says does not meet the bar. Defend it or delete it?
- Where does `PD_SAT` scale with t (L8536)? If nowhere, what is MISSILES-6's enemy answer?

**Space Fleet and Missiles jointly**
- Falcons patrol above STRATO_Y. Which of your two EMP definitions applies, and will you say publicly that unhardened aircraft go dark?

**Energy**
- Which entry point carries the family tag for `splashDamage`, `areaDamage` and spall? Show the Falcon missile's path.
- Give the saturating form of the adaptation meter, and say whether the floor is 0.25 or 0.35.

**Railgun**
- Why should rails adapt at half the rate of every other kinetic family?

**Kinetic Arms**
- Will you commit the Mass-Driver adjacency into `kinMul`, and keep KINETIC-1's airburst off any unit it weakens against single targets?

**Armour and Autofleet**
- Is Basalt exempt from the hull meters, and does the Anvil's cold hull survive as a separate rule?

**Megaprojects**
- Does the Long Gun slug pierce, and what stops N = 100 killing the anchor in one shot for 45 s of income? Show the heat per section.
- MEGA-5: `G.mult.stab` is ×30 and drops negatives (L6338). Which field do the civic lines write?

**Market**
- Will Bond service be billed to the bank rather than `icR`, so `IC_FLOOR` cannot absorb it?

**Command**
- Will "Build the fix" auto-place spend influence by default? State the reserved cap slice for hand-queued ships.

**Command and Market**
- Will the exchange line be paid on enemy-kind variety, not raw kills-per-IC?

**Rifle Corps and Psi (Society)**
- Psi: why is the Pylon .66 when a Shapeshifter is .62? Which post-v3.91.1 match proves a reach cut?

**Bio**
- BIO-4 adaptation reaches 3,000. What damage-taken formula does not go negative? What does Kleiber cost when MP is pinned at 900?

**Pacing**
- Does `invTime`, and therefore t, reset each cycle? Is Siege Clock HP a fixed table or live from `G.stats`?

**Autofleet**
- Who stamps weapon family and killer? Once at `fire()`, shared with the Loss Ledger and Mirror?
- Will you sign "Fear tracks the current input, never a ratchet"?

**Other councils (one question each)**
- Naval: NAVAL-7 cites a FRONT marker; which line defines it?
- New Player: will the Fleet Follows text be generated live so a rebalance cannot make it lie?
- Game Feel: how does Interdiction scale on the Long Gun if F clamps at 4 and sections do not?
- Physics: divide heat deposit by live `maxHp` and drop the plateau, since one Solaris pins a capital 5×?
- Industry: what do the Rows cost, and what stops 100 Cascades making `stratCost` negative?
- Lore: why does the Plumb wait for Resolve 60 (1,000 s) while buried mass counts half in Fear?
- Science: a Laser Tower is w 14, not 19; redo the saturation; where is `tutquick`?
- Stability: has Megaprojects adopted `civCap`?

**Mischief**
- Your 50-silo carpet needs 6,500 MP against a 900 cap. Show the build order. Withdraw Boss Fatigue, given bosses are scheduled?

**Lanchester**
- `IC_FLOOR` pins net income at +8 and nothing disbands; how does 0.25/weight upkeep bite? Is your armour lerp a buff or a nerf for armour 4-5 units?

**Arcade**
- Where does the code let an Extractor go underground? `ugAllowed` says no.

**Laboratory**
- Capacitor claim needs multiplicative stacking but `auraAt` adds. What number do I read? What is an existing Halo's uptime under default LEO?

**Bookshelf**
- Your Second Fleet fires on `G.fleet`, which counts arrivals, not kills. What event do you hook?

---

## 5. VERIFIED CODE FINDINGS (councils must not contradict)

*Line numbers drift between papers (Crossbench cites about 395 lines stale; `IC_FLOOR` is at L6649, older cites L6254). Facts below are the Inspectors' verified readings.*

**Damage and armour**
- `hurt()`: `eff = max(1, dmg × 1.3 × (armor<4 ? 2.7 : 1.9) − armor × 0.55)` (L4987-4991). Enemy armour is nearly inert: a Falcon rocket does 64 vs armour 22; armour 22 costs about 12 of a 441 Lance hit. Heat's "−50% armour" is worth about 6 points.
- `splashDamage` (L7553) and `areaDamage` (L7544) write `e.hp -= d2` directly and bypass `hurt()`, domes, shields and armour. Falcon missiles use this path (L9146-9149). Any multiplier placed in `hurt()` misses splash, spall and fighter missiles.
- `hurt()` carries no killer or weapon family. `killsByType` is keyed by enemy kind; `lostByType` has no timestamps; `killBy` is stamped per projectile kind. Loss Ledger, Mirror, Plan Exchange, Post-Mortem and Exercise all lack attribution.
- Live research stack: damage ×2.55 (L3511-3536), rate ×1.5 (units only), plus `HIT_BUFF` ×2.47. A Lance hit is 441 today; with proposed multipliers 2,778.
- `PD_UNINTERCEPTABLE = {rail, shell}` (L8526): shells and rails are never intercepted. `PD_SAT` is a static table (L8536), not t-scaled.
- Enemy line `dmg` is a flat literal (interceptor 28, gunship 21, strikewing 40); only HP carries t; `raider` alone scales damage (22t).
- `auraAt` is additive (`m += aura − 1`, L3776); heal is `Math.max` (L3783). Uncapped additive auras reach ×15 to ×34 DPS. Mass-Driver adjacency `1 + .6×segs` (L8448) packs ×5.8.
- Armour/enemy values: Leviathan spd 11, not anchored, 1,700t HP, armour 22; hive 1,100t; annihilator 950t (L5626-5628); bombarder spd 0, anchored, weapon none (L5629); gtank armour 6, gmech 8; pelter 55t HP, armour 4, spd 6; enemy domes `fieldHp` (360+…)×diff (L5642).

**Fire Control and dead code**
- `G.fireControl` is never set (L8447/L8454). `manual: true` is read nowhere. Fire Control is dead code.
- `isMechanical` is dead code (L4541); mechanical units count against the army cap. `e.elec` has zero hits. Starfish-style EMP by `isMechanical` would hit frigates, gunboats, pickets, strike fighters, and miss Falcons, sky ships, Halo, Volley, Zeus.

**The enemy and the fleet**
- `t = diff × (1 + invTime/70)` (L5589), never reset. Enemy strength scales with time only (L5582).
- `scanEnemyIntel` (L5737/5744) has six axes (L5775), no air or sky-navy axis; the navy axis counts naval only; dominance test is ≥1.4× (L5778).
- `interceptor` hunts aircraft (L5598); `raider` is an escort fighter; manual L777 says "they hunt your aircraft first." "Nothing hunts fighters" is FALSE.
- No enemy ground path tests water (`updEnemies` L8603-8900; `e.y = landY`, L8851). Enemy tanks cross water today. The drown rule (L8217-8227, 60% max HP/s after 0.4 s) is the player branch. Amphibious player units: tank, heavytank, arty, mlrs, mammoth, hydra only (L3320-3329); infantry, Titan, Colossus drown.
- `G.fleet` falls at spawn (L6227), not on kills. A stalemate ends in a draw (L11152) with a 900 s cap (L4126); the real win is withdrawal (L6476-6480).
- The HUD already shows exact `T-Ns` (L11202), the intent label and exact `~N hostiles` (L11754). The prep warning reads "FLEET INBOUND, SCREENING FIRE IMMINENT", once at prep end (L7158); "RE-ENTRY DETECTED" is stale.
- Fear arithmetic: F = 0.5·mean(log2(x/base)); F=4 needs 256× (about 38,000 IC/s); at 850 IC/s F is near 1.25.

**Economy and stability**
- `unitCost` (L3835) uses `facsFor(domain)`: sky capital ships take the Airbase discount, `max(.22, 1 − .09(n−1))`, so 10 airbases give 180 IC for a 2,600-HP Sky Railgun Ship. `facsForUnit` (L9924) exists. Space Station already has `costSpace .3` (L3454), `orbcap 8`.
- `IC_FLOOR` clamps net income at +8 (L6330): upkeep, bond service, power debt and tax routed through `icR` are inert; billing must hit the bank. Report 7: 212k IC idle at 850 IC/s.
- `G.stabCap = … + max(0, G.mult.stab) × 30` (L6338): civic "+4" is +120 and negatives vanish. `econ.stab` is ×14 (L6317).
- `G.owned[type]++` and `n.used` never reset (L3927); `canPlace` needs `!n.used` (L3869); `srMult` reads a boolean (L3810); a lost Extractor can never be re-placed. `ugAllowed` forbids Extractors, turrets, docks (L3862).
- Garrison upkeep is linear, 0.12 IC/s per weight; garrisons are excluded from `combatWeight` (L4573); allies are exempt from cap and upkeep.
- 50 silos need 6,500 MP (silo mp 130, cap 900); auto-strike non-taboo strikes cost 0 stability (L7305).

**Underground, sea and strikes**
- `UG_DEPTH = 50`; two buried layers; `launchStrike` (L6951) never reads `ug`, so a buried Silo fires today; Underground Airwing is `autoBury: 1` (L3461). TEL, Mobile Launcher and Ballistic Submarine do not exist (removed v3.25.0); CLAUDE.md is stale. Only taboo strikes cost stability.
- Rail hubs already teleport idle ground units hub to hub (L7884, L8238, `railT = 1.4 + dist/1500`): Sally Ports exist.
- Flood rule (L6568) sets hp = 0 on any non-needWater, non-buried building under SEA_E − .04, captured or not.
- Earthbreaker quake hits buried buildings for `max(55, 50% maxHp)` within 320 px (L8791, L9197), per Deep and Crossbench; the Arsenal Inspector said no enemy reaches `ug`. The Clerk records this disagreement. Buster 1,400/520 (L7511); craters cave shallow tunnels.
- `shieldBlocks` returns the first live dome (L8903); N overlapping domes already absorb N×900 in sequence; frame cap L8939.
- Shapeshifter: plasma ceil .62 (L4494), range 560, armour 14; `allRegen` heals 4.5%/s once researched (L3531, L7873). Psi-Trooper and Pylon are `ceil: 0` (L3295-3296). Laser Tower is w 14 (L3439). A shifter beats a gtank today.
- Railsats hold at MESO_TOP+40..+120 (L5644), below MESO_TOP, 20t HP. Falcons patrol at `airTopY()+30..+100` (L4161); `airTopY` is MESO_TOP (L1543), above STRATO_Y.
