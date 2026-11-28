# RAILGUN COUNCIL (Mass Driver Directorate) — Round 1 Position Paper
*Slug: `railgun`. Portfolio: electromagnetic and hypervelocity launch. All line numbers are `title.html`.*

## 1. Opening Speech

*(The Director raps a tungsten gavel. It leaves a dent in the table, in the floor, and slightly in the Ministry of Air.)*

Honourable members. The Air Council will tell you that jets win wars. A jet is a fuel tank with opinions. It lands, it rearms, it is shot down, and then someone writes a poem about it. The Lance does not rearm, because the Lance does not need to. It turns electricity into velocity and velocity into the end of an argument. Report 6 saw **282 Lances built**. The President has voted with his wallet, and the wallet says *rails*.

What did we give them for it? A 60-hp box on a Steel deposit, with a 70-damage shot and no research node of its own. It is the most-built weapon in the game and has never had a single idea added to it since it shipped. Our **Orbital Gun** sits at 72 damage. Our **Mass-Driver Railway** has one clever rule (neighbours stack) and nobody knows. Our **Zeus** rods are launched by a Space Center, a rocket-part and a prayer, and then perish to a grid of bombarders in report 7: *18 of 18*.

The Directorate does not ask for more guns. We ask for physics. A railgun is not "a gun that shoots high." It is a **capacitor bank, a pair of rails that are slowly vaporising, and a very long argument with Newton**. Give us the capacitor, the erosion, the barrel length. Give us the Long Gun, a barrel you build section by section until it points at God and fires.

Rival councils will call this excess. They are correct. Seconded.

## 2. The Gap

- **The Lance has no depth.** The Lance (`UNIT_DEFS.railgun` L3192) is `speed:0`, `ceil:0`, a flat 70 dmg every 4.5 s. It is the most-built weapon in the game and has no upgrade path, no mode, no logistics and no counterplay.
- **No node owns the rail line.** `kinetics` and `orbital` are pre-unlocked at game start (L3588), so the Lance, Zeus, Orbital Gun and Mass-Driver Railway are free at minute zero. Rail technology never escalates the way psionics and energy do. The only rail upgrade today is a side effect: `particlebeam` re-arms ships (L3347).
- **Rail effects exist and are barely used.** The Mass-Driver Railway has an adjacency stack of `+60% per neighbour` within 90 px (L8441). It is invisible to the player and never reaches the manual.
- **Orbital launch is just a rocket.** Zeus needs a Space Center's lift (`orbLift`, L3812) and a `rocketpart` (L3219). Nothing uses the line "railgun launch slings satellites to orbit" in the Space Age text (L3506). Space units die to the bombarder grids (Article VI), and replacing them is slow and expensive.
- **There is no cost to firing a rail.** Rail ammo is unlimited (L4495). It has no power draw, no wear, no heat. The designer asked for these as *real costs, not caps*. As it stands the Lance is free damage, and every free thing gets built 282 times.
- **The megaproject is missing.** The Sky Railgun Ship (L3259) is the biggest gun in the game, at 320 dmg. The designer's turning superheavy railgun does not exist. Neither does the megaproject framework outside the `megaprojects-wip` branch.

## 3. Bills

### RAILGUN-1 — Pulsed Power: Capacitor Banks, Overdrive and Bore Wear

**Pitch (in character):** *A railgun fires on stored lightning. Right now our lightning is infinite and our barrels are eternal, and that is simply bad physics. Build a Capacitor Bank beside your Lances and they overdrive: heavier slugs, faster cycling, white-hot rails. Every overdriven shot scorches the bore. Keep a Rail Foundry nearby and the liners are swapped as you fire. The barrel is not a cap. It is a **receipt**.*

**Player fantasy:** A line of 100 Lances, a bank humming behind them, firing in rippling overdrive volleys. Sparks and plasma scoring the muzzles, liner crews running between guns.

**Clerk's Annex**
- **Mechanic.**
  - New SPEC building **Capacitor Bank**: 220 IC, 60 MP, 8 s build, 220 hp. It holds a charge pool of 60 and refills at 3/s, paid for at 0.5 IC/s while refilling. Range 420 px.
  - Any `weapon:'rail'` unit or building (Lance, Mass-Driver, Orbital Gun, Battlecruiser) within range **overdrives** while charge lasts. Each overdriven shot spends 1 charge, deals ×1.5 damage and fires ×1.25 faster, and adds **+1 bore wear**.
  - At 300 wear the bore "burns out": the gun falls back to its normal stats, **never below baseline**, until relined.
  - **Rail Foundry** (new SPEC, 180 IC, uses Heavy Alloy): relines every gun in 500 px at 1 alloy per reline, taking 6 s per gun.
  - Wear never reduces damage, range or uptime below today's numbers. Overdrive is a bonus, and the receipt is IC, alloy and attention.
  - Real basis: US Navy 32 MJ railgun tests (about 2.5 km/s, ~Mach 7). Rail and armature erosion from arc transition and current-density hot spots was the programme's limiting factor, at hundreds of shots per barrel. Pulsed-power supply from capacitor banks charged over seconds is how those guns actually fired.
- **Code grounding.**
  - `updBuildings` L8398 and the `massrail` stack at L8441 are the natural place for a bank-aura pass.
  - `fire()` L4855, with the `rail` projectile at L4879.
  - The unit rate and damage hook near L8244 (`jitterCd(d.rate / (1+G.mult.rof) / auraAt(...))`). Add a bank aura via `auraAt`.
  - `BUILD_DEFS` L3381.
  - `COMP`/`alloy` logic for components, near `comp:` on units.
- **Research.** New node **Pulsed Power** (branch `energy`), prerequisite `directedenergy`, cost tier ~150 × 1.6, not dangerous. It unlocks Capacitor Bank and Rail Foundry. Combo partner: `superconductor` building raises the pool by +20 and halves wear.
- **Build size:** M (~250 lines). **TM1.**
- **Tenet check:** Passes. Everything is additive. Baseline is the floor, and wear is a logistics cost for a bonus.
- **Interactions.**
  - Defence council: Rail Foundry is an obvious sabotage target for enemy busters, so it is a high-value building.
  - Energy council: shared charge and power vocabulary.
  - Heat seed (Article VII): a hot bore can add heat to the *firer* only as a cosmetic and an animation.

### RAILGUN-2 — The Long Gun: Superheavy Railgun Megaproject

**Pitch (in character):** *We do not build a gun. We build a barrel and keep building it. One section is a Lance wearing a hat. Twelve sections is a cathedral pointing at the exosphere, mounted on a slewing ring mid-map, turning on the target like a mind making itself up. Each section bolts onto the last. Muzzle velocity goes up with the square root of the barrel. Energy goes up with the length. The enemy flagship learns to respect geometry.*

**Player fantasy:** A mid-map wonder visibly growing section by section and swinging around to the flagship. A single shot with a muzzle flash that lights the whole horizon, and a slug that goes straight through the fleet's column.

**Clerk's Annex**
- **Mechanic.**
  - One **Long Gun Foundation** (deliberate megaproject, tier 2 per Article IV.3): 900 IC plus 20 alloy. Up to **12 Barrel Sections** are added at 400 IC and 8 alloy each, taking 14 s per section.
  - Damage is `80 × sections`. Range is `1500 × √sections`, so about 5,200 px at 12 sections. At constant acceleration `v² = 2aL`, so energy scales as L and velocity as √L.
  - Slew is slow (a 3 s traverse arc) and the gun is manual-aimable like the other heavy guns (`manual:true`).
  - Fire cost is 1.2 IC per section per shot, and shots come every 8 s. The rail line shot is `pierce:true` and passes through every enemy on the line.
  - **Recoil and structure:** rail separation force (Lorentz) is real, so the barrel needs a **Recoil Cradle** every 4 sections (cheap IC). Skip one and the gun still fires, with +30% wear to its own section hp (a cost, not a cap).
  - The Long Gun is a juicy target. Boss-tier enemies prioritise it at 60%.
  - Stages: Foundation, then each section adds a new silhouette, then the "Capstone" at 12 sections gives a visible plasma-window muzzle (see Hot Take).
- **Code grounding.**
  - `BUILD_DEFS` L3381 and `makes` and `prodList` patterns; see `skydock` L3395 for staged capital builds.
  - `massrail` L3455 and the adjacency pass L8441 as the template for section counting.
  - `siegegun` L3414 for manual fire.
  - `PD_UNINTERCEPTABLE` L8519 (rails stay uninterceptable).
  - `p.pierce` projectile path L4879 and L9159.
  - The `megaprojects-wip` branch has the project framework to lift (not merge).
- **Research.** New node **Superheavy Rails** (branch `space`): prerequisites `orbitalcmd` and `spaceage`, cost ~420 × 1.6, `dang:false`. Combo: **`particlebeam` + `superconductor`**, which gives the Long Gun a hitscan particle slug (the existing `particlebeam` re-arm pattern at L3347).
- **Build size:** L (~700 lines with art and the section UI). **TM1.**
- **Tenet check:** Passes. It is the biggest toy in the game, built on purpose.
- **Interactions.**
  - Enemy council: a scarier Annihilator or Skyfortress response is a free design lever. They should shield, jam or dodge the gun.
  - Space council: the same projectile can finish a Bombarder in orbit.
  - Economy: a megaproject IC sink that actually holds a gun.

### RAILGUN-3 — Hypervelocity Munitions: Guided, Airburst and Sabot Rounds

**Pitch (in character):** *Ordnance, gentlemen. A dumb slug is a statement. A smart slug is a conversation. Give the Lance three rounds: the fin-guided HVP that bends toward a manoeuvring jet; the airburst slug that opens like a flower and fills a cloud of drones with tungsten; and the armour-piercing sabot that does not care what the Skyfortress has for a shield.*

**Player fantasy:** The Lance finally *chooses*. Pick a round type per Lance group and see it change the sky: flak-clouds of flechettes, or a single needle through a hull.

**Clerk's Annex**
- **Mechanic.** A per-group ammo selector on `railgun`, `massrail`, `siegegun` and the Battlecruiser (unlimited, switchable in 1 s):
  - **HVP Guided:** the slug gains `homeTgt` homing with a 25°/s turn. It no longer misses fast air targets, and its dmg is ×0.8.
  - **Airburst:** at the fuze point the round detonates for a 120 px cloud, 6 flechettes at 30% dmg each. Strong against the drone and bombpod swarms but weaker vs one big target.
  - **Sabot APFSDS:** `pierce:true`, +40% dmg and ignores 60% of armour, ×1.5 reload.
  - Real basis: BAE's Hypervelocity Projectile (guided, fired at Mach 3+ from naval guns and also evaluated for railguns), the ARDEC/Excalibur gun-hardened guided rounds (survive about 15,000 g), and discarding-sabot long-rod penetrators such as the M829 tank round.
- **Code grounding.**
  - `fire()` L4855 and the rail branch L4879.
  - Existing `homeTgt` and `pierce` parameters on `fire()`.
  - Cluster and split handling L6706 (rail "splits").
  - `weaponCeil` L4490.
  - The UI selector pattern is the same one used for strike selection (`armSilo` L7660).
- **Research.** New node **Smart Munitions** (branch `def`), prerequisites `pointdefense` and `directedenergy` (the `shieldtech` pattern), cost ~170 × 1.6, not dangerous. Combo with **`automation`** for the airburst fuzing. Note this is a combo that changes how the weapon plays, not only a new unit.
- **Build size:** M (~300 lines). **TM1.**
- **Tenet check:** Passes. It adds choices on top of the unchanged baseline round.
- **Interactions.**
  - Air council: Airburst rounds are the high-altitude answer to enemy swarms, so Falcons are not the only answer.
  - Defence council: the HVP slug is a new interceptor of buster ordnance.
  - Enemy council: the enemy's shields and `armor` stat gain meaning with sabot.

### RAILGUN-4 — The Space Gun: Gun-Launched Orbit

**Pitch (in character):** *The Space Age already says "railgun launch slings satellites to orbit." Then it asks you for a rocket. We object. Build a Launch Rail, a long, shallow barrel in the hills, and shoot hardened satellites into orbit for the price of electricity. When the bombarder grid eats your Halos, you reseed the heavens in a minute.*

**Player fantasy:** A long slender rail on the horizon. A salvo of canisters streaks upward, and a dead orbital grid is replaced in front of you.

**Clerk's Annex**
- **Mechanic.**
  - **Launch Rail** (SPEC, 500 IC, 120 MP, 16 s): grants `orbcap` +20 (compare Space Center 5 and Station 8, `orbCap` L3812) and launches **hardened** space units (Zeus rods, Halo/Volley satellites, Pickets) with **no rocket part** and at **−35% IC**.
  - It launches one canister every 4 s, so 10 satellites replace in 40 s.
  - Satellites arrive and hold, as standing decision 2 says: they never move.
  - Real basis: acceleration `a = v²/2L`. 8 km/s over 1 km needs about 32,000 m/s², roughly 3,300 g, which is survivable for ruggedised electronics (Excalibur shells see ~15,000 g). Bull's HARP project fired 16-inch guns to about 180 km in the 1960s. Today's SpinLaunch and Quicklaunch concepts are modern descendants.
  - Non-hardened units (crewed or fuel-heavy ones) still need rockets. Pickets are hardened.
  - Atmospheric drag cost: slugs launch through the lower atmosphere and lose speed, so the muzzle sits high on a hill (terrain editing, `RAISE`) for a flat bonus.
- **Code grounding.** `orbCap`/`orbCapUsed` L3812–3813, `UNIT_DEFS.rods` L3219 (`lift:3`, `comp:{rocketpart:1}`), the satellite launch path near L4871 (hard boost off the rail), `STRAT.kinetic` L6881 and `launchStrike` L6943 (Zeus is gated by `unitReq: 'rods'`).
- **Research.** Attach to existing **SPACE AGE** (`spaceage`, which already promises "railgun launch"), so no new node is required. An optional combo, **`orbitalcmd`** (3× lift), multiplies the Launch Rail's lift.
- **Build size:** M (~200 lines). **TM1.**
- **Tenet check:** Passes. It *adds* a cheaper, faster path and is a fix for "space units must matter." It does not touch rocket launch.
- **Interactions.**
  - Space council: this is their logistics, and a reseed button for a grid that dies in report 7.
  - Enemy council: Bombarder escorts should target the Launch Rail, as it is now a strategic asset.
  - Economy: lowers the IC cost of orbital play without an IC cap, an answer to unspent IC in report 7.

## 4. Hot Take

### RAILGUN-X — The Meridian Accelerator (the whole map is the barrel)

**Pitch (in character):** *You have a Rail Hub network. You have Mass-Driver segments. Every engineer on this planet looks at them and sees a railway. We look at them and see a barrel. Connect them with buried vacuum tube and the nation itself becomes a single hypervelocity gun, as long as the sprawl you built, firing slugs at orbital speed. You did not build a megaproject. You **became** one.*

**Player fantasy:** The doctrine you fall into (Article IV.3): the sprawl you already built turns out to be a weapon. One white, silent shot a minute, and a boss simply is not there anymore.

**Clerk's Annex**
- **Mechanic.**
  - When a chain of buried Rail Hubs (`rail:true`, auto-buried at `UG_DEPTH` 50) links at least **8 Mass-Driver segments** end to end, a toast announces **THE MERIDIAN ACCELERATOR** and a charge meter appears.
  - **Barrel length = connected tube length.** Damage = `3 × tube length in px / 10`, up to roughly 1,800 at a 6,000-px sprawl. One shot per 60 s, 30 IC per 100 px of tube per shot, `pierce:true`, `ceil:0`.
  - It fires along the tube azimuth only (fixed aim, sky-angle selectable). If any tube section is destroyed, the barrel shortens and so does the shot. That is the real cost: the enemy now has a reason to hit your **tunnels**.
  - The **plasma window** at the muzzle is real physics (Hershcovitch, Brookhaven: a plasma arc can hold atmosphere against vacuum). **SCI-FI:** sustaining a plasma window at the scale of a 50-tonne slug, and a ground-launched slug surviving ~8–12 km/s transit through the lower atmosphere without ablating. Internal logic: hot, dense plasma in the bore pre-clears a channel of air, much as a meteor trail does, and the Autofleet's makers never built a tunnel for a reason (Article V: they feared an industrialising Earth, and this is why).
  - **TM2 hook:** the same barrel is how the post-siege counter-offensive fires payloads off the planet.
- **Code grounding.** `railhub` L3416 (tunnel graph and idle-troop redeploy logic), `massrail` L3455 and the adjacency scan L8441 as the chain test, `UG_DEPTH` L3843, `fire()` L4879 `pierce`, `spawnSuper` L5269 (for the boss that dodges it).
- **Research.** New node **Evacuated Tube Transit** (branch `space`), prerequisites `spaceage`, `superconductor`-related **`directedenergy`** and `automation`. Cost ~380 × 1.6. `dang:true` (sc 6), since a tube breach means a sonic implosion near your own cities. Combo: **`nanotech`**, which self-heals tube breaks.
- **Build size:** XL-leaning L (~900 lines, with a graph pass and a UI).
- **Tenet check:** Passes. It rewards sprawl, the game's core fantasy, with the biggest gun in the game. The "cost" is a vulnerable network, which is a logistics cost.
- **Interactions.** Underground railway seed (Article VII). Enemy council: busters and Earthbreakers get a real reason to hit the tunnels. Psionics council: Mind-Forge is the only rival for single-shot spectacle.

## 5. Alliances Sought

- **Space/Orbital council.** RAILGUN-4 reseeds their satellite grids, and RAILGUN-2's slug is the only answer to a Bombarder that anchors in space.
- **Energy/Electronics council.** RAILGUN-1 shares the Capacitor Bank and Pulsed Power vocabulary. We want their lasers hot and our slugs cold, and the heat seed in Article VII is the bridge.
- **Logistics/Infrastructure council.** The Rail Hub network is the Hot Take's barrel. We will happily trade them a vulnerable-tunnel mechanic.
- **Enemy/Autofleet council.** We beg them to answer the Long Gun with a scarier fleet. Every bill here is paid for by an enemy that earns it.
- **Rivals.** We oppose the Air Council's claim that jets are the whole answer, and we politely offer to shoot their jets down if they do not stop saying it.
