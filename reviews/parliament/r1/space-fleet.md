# SPACE FLEET COUNCIL — Round 1 Position Paper
*The Orbital General Staff. Slug: `space-fleet`. Speaker: the Admiral of the Void.*

## 1. Opening Speech
Honourable members. I have buried 42 of 44 Halos, 37 of 37 Strike Fighters and 18 of 18 Volleys. I buried them in one engagement, Report 7. Each cost 300 IC and sat in the dark doing arithmetic while a railsat with 20 hit points and a clock multiplier deleted it from 1,900 pixels away.

The Air Council will tell you the sky belongs to Falcons. Fine. Then the Falcons can park in the exosphere and learn about vacuum. The enemy flagship sits where nothing of yours reaches it but a few big guns. Their bombarders carry a ten-sat grid each and the grid outranges what you launch to kill it.

You call space "empty". It is not empty. It is a shooting gallery where only one side stands behind the targets. I do not ask for immortal ships, because the tenet forbids it. I ask for a war with a **middle**: decoys, screens, saturation, an economy worth defending, and a flagship you can sink and be proud of. Give me a space fleet that dies *expensively* for the enemy. Today it dies for free.

Satellites never move. Fine. Then **the war moves around them**, through reach, volume and what each satellite *is for*. Orbital supremacy, honourable members. Seconded by nobody yet.

## 2. The Gap
Evidence, all grounded in `title.html`:
- **Our units are glass.** Halo 50 HP, Volley 45, Zeus 40, Warden-S 70, Strike Fighter 55, Frigate 60 (`UNIT_DEFS` L3217-3223). The enemy railsat does `dmg 43 × t` every 5.28s at `RAILSAT_RANGE` 1900 (L5637, L8517). Our best sat range is 1,300.
- **Range inversion.** Their grid out-ranges our sats (1,900 vs 1,100–1,300), and bombarder lances reach 1,400 with 220×clock damage. We lose the opening shot every time.
- **The enemy's point defence saturates but nothing exploits it.** `PD_SAT` (L8620) gives a bombarder about 5 swats a second and the flagship 14. Nobody on our side fires coordinated salvos.
- **Space units earn nothing.** A sat has no economy, no aura and no role beyond shooting. If it is shot first, nothing is lost but IC. Enemy `threatWeight` (L4330) already prioritises `THREAT_W_GRID` for space shooters, so our sats are the first targets.
- **Lift is a flat queue.** `orbCap()` (L3812) is a pure slot count. The only capital space hull is the Dreadnought (600 HP, antimatter-gated). There is no battleship, no carrier and no tender.
- **No space-specific research combo.** The space branch (L3508 onward) is stat nodes and unit unlocks. Nothing changes the world.

## 3. Bills

### SPACE-1 — "Chaff, Flares and Whipple Bumpers" (Decoy Clouds)
**Pitch.** Admiral: "Every missile in history chases the brightest thing. Build the brightest thing and make it cost nothing."
**Fantasy.** You launch a thousand cheap glittering lures and the bombarder fleet burns its lances on tinfoil while your Halos sit untouched.
**Clerk's Annex.**
- **Mechanic.** New unit **Decoy Cloud "Tinsel"**, `domain:'space'`, `sat:true`, 8 IC, 0 MP, lift 0.25, 12 HP, no weapon. It draws the same radar signature as a Halo. Enemy target selection treats a Tinsel as a grid target (`THREAT_W_GRID`) with a +30% weight bonus. Each carries a **Whipple bumper**: first hit on any friendly sat within 120px is absorbed by a plate with 40×t HP that regenerates between waves. Lift is fractional, so the existing `orbCap()` must allow `lift: .25` (a float, not a rewrite).
- **Science.** Real. Inflatable and chaff decoys are standard on ICBM penetration aids (Mylar balloons in the same radar cross-section as warheads). Whipple shields (a thin sacrificial outer plate spaced from the hull) are on the ISS and defeat hypervelocity micrometeoroids.
- **Tree.** New node `debrisshield` (space branch, pre: `orbitalcmd`, cost ~200 after the 1.6× multiplier, not dangerous). No combo.
- **Code grounding.** `UNIT_DEFS` L3217-3223 for the entry. `acquireEnemy` L4374 and `threatWeight` L4330 for the decoy weighting. `orbCap` L3812 and the lift check near L4265 for fractional lift. Bumper absorb hooks into `hurt()` L4971.
- **Size.** M (~180 lines).
- **TM1.**
- **Tenet.** Adds toys, no cap. The enemy still wins attrition by volume, which is the legitimate lever.
- **Interactions.** Enemy-AI council: they will want enemies that learn to ignore decoys after N wasted shots (fair, scarier). Economy council: IC sink that is not "spend 20,000 IC".

### SPACE-2 — "Cooperative Engagement Net" (Time-on-Target Saturation)
**Pitch.** Admiral: "Their point defence swats five rounds a second. Fine. Give me six hundred rounds in the *same* second."
**Fantasy.** Your constellation holds fire for ten seconds while the net counts down, then every sat, picket and gunboat in range fires at once, and the bombarder's PD budget goes red and flickers.
**Clerk's Annex.**
- **Mechanic.** New unit **Relay Sat "Argus"** (sat, 340 IC, lift 3, hp 90, no weapon). Space units within 900px of an Argus form a **net**. The player presses a hotkey or a button, **SYNC FIRE**. The net holds fire for 6s while charging (pure targeting; units remain targetable), then all units in the net fire their next shot at the designated target on the *same tick*, with the shot's travel times corrected so they impact within 0.2s of each other. Net size N grants +4%×N damage to that volley, capped by what is physically in range, and no more than the units' own firing.
- **Hook.** Saturation exploits the existing `PD_SAT` budget (L8620) so a target with `pdBudget < 1` takes the volley raw. A bombarder holding 5 and refilling 5/s is overwhelmed by 8+ simultaneous rounds.
- **Science.** Real. Aegis **Cooperative Engagement Capability** (CEC) shares fire-control quality tracks across ships so any ship's missile can be guided by any other's radar. Artillery **Time-on-Target** fires rounds from different ranges so they land together, defeating counter-fire reaction.
- **Tree.** New node `cec` (combo: `orbitalcmd` + `directedenergy`, cost ~260, not dangerous). Combo effect beyond the unit: enemy PD saturation is displayed on enemy hull bars as "PD EMPTY", which the player can read.
- **Code grounding.** Projectile spawn path in `fireUnit`/projectile code (grep `weapon === 'rail'`), `PD_SAT` L8620, `acquireEnemyMulti` L4398 for the designation, `updUnits` space branch L8083-8117 for hold-fire.
- **Size.** M (~250 lines; the hold-and-release state machine is the cost).
- **TM1.**
- **Tenet.** More power for the player, with a real cost: the 6s charge window is a vulnerability. No nerf.
- **Interactions.** Air council: Falcons could join the net (they will ask). Missiles council: NET applies to rocket salvos with the same code.

### SPACE-3 — "Solar Power Satellites" (a Constellation Worth Defending)
**Pitch.** Admiral: "Stationary satellites are not a flaw. They are *infrastructure*. Put the economy up there and suddenly the fleet has something to protect."
**Fantasy.** Your sky glitters with golden panels beaming gigawatts to rectennas on the ground. The enemy's first target becomes your income, and your space fleet becomes the shield of the nation.
**Clerk's Annex.**
- **Mechanic.** New unit **Helios Array**: sat, 420 IC, lift 4, hp 110, armour 4, no weapon. +2.5 IC/s each (boosted by Power Plants on the ground: +1 IC/s per Power Plant within 1,500 px, a rectenna link). Within 800px, Halo/Volley/Warden-S fire rate +20% (beamed power). Enemy `threatWeight` for an Helios is `THREAT_W_BOSS` (grid targets are weighed under the economy's own priority), so it draws fire, and the Decoy cloud of SPACE-1 now has a job.
- **Science.** Real. **Space-based solar power** was studied by NASA and JAXA; microwave beaming at 2.45 or 5.8 GHz with rectennas achieved over 80% efficiency in lab and kilometre-scale demonstrations (JAXA 2015, Caltech SSPD-1 2023). Orbit gives about 8× the ground insolation, with no night.
- **Tree.** Attach to existing `spaceage` (L3506) plus a new node `ssps` (space branch, pre: `spaceage`, cost ~240, not dangerous). Combo with `directedenergy`: **laser power relay**, Halo beam +20% range.
- **Code grounding.** BUILD_DEFS `econ` field (see `powerplant`), `UNIT_DEFS` L3217; sat hold L8092 so it stays; income tick (grep `G.mult.econ`).
- **Size.** M (~150 lines).
- **TM1.**
- **Tenet.** More toys and more IC. The enemy's answer is a scarier raid on the grid, which is the intended lever, plus the IC sink it creates by drawing a real fight.
- **Interactions.** Economy council: IC firehose that is attackable, not free. Society slice (TM2): SSPS is the "outflow by what you built" seed.

### SPACE-4 — "Retrograde Shotgun" (Debris-Cloud Weapon)
**Pitch.** Admiral: "A grain of sand at fourteen kilometres a second is a cannon shell. I have a ton of sand and no sense of mercy."
**Fantasy.** One quiet sat opens like a flower and the whole thermosphere lane in front of it goes white. Railsats pop in rows, and no point-defence on earth can swat ten thousand grains.
**Clerk's Annex.**
- **Mechanic.** New unit **Shotgun Platform "Scattergun"**: sat, 380 IC, lift 3, hp 70, one-shot per 22s reload. Fires a **pellet cloud**: a 1,600×90px lane band in the mesosphere/thermosphere at its own altitude ±120px. Everything hostile in the band takes `35 + 1.2×t` damage (flat) and armour is halved. It **cannot be intercepted** (no projectile objects: an area `hurt()` sweep, cheap). It also stings friendly sats in the lane (friendly fire at 25%), which is the real cost: you must place sats outside the lane or accept losses. After firing, the lane lingers 4s as a debris screen that damages re-entering hostiles.
- **Science.** Real. Retrograde-orbit encounters carry relative velocities near 15 km/s, and a 1 mm aluminium sphere at that speed has the kinetic energy of a rifle round. Shotgun and "flak" kinetic ASAT concepts were studied under Project Defender and SDI "Brilliant Pebbles" literature, and the 2007 Chinese ASAT test left debris still in orbit.
- **Tree.** New node `retrograde` (space branch, pre: `orbitalcmd` + `massdrivers`, cost ~380, **dangerous** `sc:6`: your own debris is a political problem).
- **Code grounding.** Area sweep modelled on the flak field (grep `flakring`), `BEAMK` L4340 for non-interceptable families, `hurt()` L4971, enemy hold bands `MESO_TOP()`/`THERMO_Y()` L1547.
- **Size.** M (~220 lines).
- **TM1.**
- **Tenet.** Pure power, with a real cost (friendly-fire on your own sats) rather than a cap.
- **Interactions.** Enemy council: the bombarder grid's whole point (low, numerous, cheap sats) is that this weapon is their natural predator, so they may want grids to spread out or deploy shielded variants as an answer. Air council: lane band clips aircraft above the stratosphere too; air gets the same 25% friendly-fire rule.

### SPACE-5 — "Orion Battleship" (the Capital Ship Space Never Had)
**Pitch.** Admiral: "A navy needs a ship that is *the* ship. Nuclear pulse propulsion. The pusher plate is the armour. I do not apologise."
**Fantasy.** One enormous hull, 4,000 HP, with four turret hardpoints and a hangar, climbs through the exosphere on a staccato of nuclear pulses, and when the flagship's escort fighters run into it they go through a very bad afternoon.
**Clerk's Annex.**
- **Mechanic.** New unit **Orion Battleship "Pusher"**: `domain:'space'`, not a sat, 1,400 IC, 80 MP, 40 build, hp 4,000, armour 24 (pusher plate), lift 12, `comp: rocketpart 6`, speed 14, 4 turrets (rail 90 dmg / rate 3, ceil 0; 2× point-defence), carries 10 Orbital Strike Fighters that rearm on deck, with no Space Station required. Built only at a Space Station (`bReq`). A pulse-unit "burn" ability: 2s dash (+300% speed) that detonates a small nuke behind it: 120 dmg in 250px, friendly-safe at the pusher plate (the ship is the only unit it protects).
- **Science.** Real design. **Project Orion** (1958-65, Dyson, Taylor) was a funded study of nuclear pulse propulsion with a massive steel pusher plate and shock-absorber springs. It was shown feasible, with high Isp (2,000-6,000s) and absurdly high thrust. Banned only by the 1963 Partial Test Ban Treaty. Planetary-defence studies cite it as the heavy-lift option.
- **Tree.** New node `orion` (space branch, pre: `orbitalcmd` + `massdrivers`, cost ~450, **dangerous** `sc:10`, `srReq: 'uranium'`; combo: **`antimatter`** upgrade swaps pulse charges for antimatter-catalysed pulses, +50% burn damage).
- **Code grounding.** Carrier logic exists (`carrier`, `carrierN`, L3260 Nanoforge), reuse for the hangar. `turrets` array format L3260. `bReq` L3223 for station gating. Space cruise AI L8083+.
- **Size.** L (~500 lines, mostly the art and the deck-rearm code).
- **TM1.**
- **Tenet.** A new toy at a real IC and uranium cost, and it fixes "our only capital is the Dreadnought". No nerf.
- **Interactions.** Naval council: the "sky navy" is the evidence they win; this is our answer to a capital ship. Air council: they get a carrier peer.

## 4. Hot Take

### SPACE-X — "The Kessler Doctrine" (a Doctrine You Fall Into, Orbit You Burn)
**Pitch.** Admiral: "If we cannot win the sky above them, we will *burn the sky*. All of it. Theirs, ours, the Moon's, anyone's. They are afraid of an industrialising Earth? Good. Let them watch us light the lane."
**Fantasy.** You press the button you were told never to press. Every satellite in the thermosphere, yours and theirs, fractures into a glittering shell. The bombarder grids collapse. The flagship's escorts melt. Re-entering skyfall now burns through a belt of knives. And you lose your space fleet and your Helios income, knowing the orbit is a minefield for minutes, and that you pulled the trigger yourself.
**Clerk's Annex.**
- **Mechanic.** A one-shot **Doctrine**, in the President's "doctrines you fall into" tier. It unlocks when you have lost more than 20 satellites in one match and you hold the `retrograde` node. The player pays IC and stability (+12% stability cost, a visible "world will not forgive" label). On activation, for **180 seconds**: (a) every sat (friend and foe) in the mesosphere/thermosphere band is destroyed, (b) a **debris belt** fills the band, dealing `18 + 0.6×t` damage per second to any hostile that enters or loiters (re-entry crossing deals the most), (c) your own space-domain units and lift are **offline** (`orbCap()` returns 0) until the belt decays, with decay time inversely tied to your thermosphere "scrubber" research, and (d) the fleet reacts: at least **one fleet psychology event** fires, "ORBITAL RECALCULATION", and enemy bombarders hold off descent until the belt thins, which lengthens their approach. It gives a real cost, not a nerf, and an enemy that gets *scarier* afterwards (the fleet adapts, re-seeds the grid faster, and gains PD budget +40% for the rest of the match).
- **Science.** The **Kessler syndrome** (1978, Donald Kessler and Burton Cour-Palais) is a real cascade: collisions between orbital objects generate fragments that cause further collisions. Low Earth Orbit shells decay on timescales from months (below 400 km) to centuries (above 800 km), so a one-shot in the thermosphere clears in minutes-to-hours, which is the right shape for a 180s mechanic. **SCI-FI:** the *deliberate*, bounded cascade (a tuned detonation pattern) is invention. Internal logic per Article V: the Autofleet is *afraid*; Earth proves it will wreck its own high ground rather than lose, which is exactly the escalation the fleet cannot siege through.
- **Tree.** Doctrine is gated by node `retrograde` (SPACE-4), and by a new combo with `pointdefense`: **"Orbital Scrubber"** halves the debris decay timer, a world-changing combo node (def branch, pre: `pointdefense` + `orbitalcmd`, cost ~300, not dangerous).
- **Code grounding.** `megaprojects-wip` framework (CLAUDE.md note). `PFX.rule`/`PFX.mod` (ROADMAP L709), `orbCap()` L3812, `updEnemies` L8596 for the belt damage, `G.projectRules`.
- **Size.** L (~450 lines).
- **TM1** for the doctrine, with the fleet psychology event as a **TM2** slice.
- **Tenet.** Passes. It is the *player's* choice to burn their toys, and the cost is real and visible, not forced. It does not cap or nerf any unit; the fleet answers by becoming scarier, as the tenet requires.
- **Interactions.** Enemy council: the fleet's post-Kessler adaptation is theirs to design. Society (TM2): "Orbital Recalculation" is the first fleet psychology event.

## 5. Alliances Sought
- **Air Council.** They own the sky below mine. They will join the Cooperative Engagement Net (SPACE-2), and I want their Falcons to *escort* my Orion into the mesosphere instead of fighting alone.
- **Enemy / Autofleet Council.** SPACE-1 and SPACE-4 only matter if the grid is dangerous but fair. I want them on my side to design what the fleet does when we out-range it.
- **Economy Council.** SPACE-3 is an IC sink that is also a *target*. They will like it, and it solves "212k IC unspent" without a button.
- **Missiles Council.** The Argus net improves any salvo weapon. I will share the code.
- **Projects / Doctrines Council.** SPACE-X is a doctrine on their framework. I lend the first live example.
