# AIR COUNCIL — Round 1 Position Paper (slug: `air`)
*The Air Staff. Air Marshal on the rostrum, moustache waxed, ashtray full.*

## 1. Opening Speech

Honourable members, sit down, you are blocking my sunlight.

Report 8: 240 Falcons, 113 Kites, 138 sky ships, ten-to-one. Report 10: flawless. The Ground Staff calls this "a problem." I call it a **vindication**, and I will not hear one word about "nerfs" in this chamber, because the Constitution (Article III) would have the mover shot.

But I am a *professional*, and professionals know why we win: **we win because nobody has made us play.** Our Falcons take off, burn fuel, land, and take off again. Nobody refuels them. Nobody cues them. Nobody jams them, hunts them, or cratered a runway under them. The enemy throws interceptors that hold station like sunbathers. This is not air power. This is a *screensaver*.

Give me tankers, and fuel becomes geography. Give me an AWACS, and the wing becomes a network. Give me an enemy that hunts my fighters and kills my radars, and I will be forced to be *brilliant*. Give me electronic attack, and bombers stop being a number and become an operation.

Spam wins today because nothing punishes spam. I want something that rewards *craft*. Leave my toys alone, and make the sky dangerous.

Air Staff, out. Somebody refill my cup.

---

## 2. The Gap

Evidence from `title.html` (the code wins over CLAUDE.md):

- **Logistics is one-dimensional.** `AIR_FUEL_BURN = 1` (L4511); `rearmAircraft()` (L4522) sends any low-fuel or empty aircraft home to the nearest base or deck, then a 3–5 s turnaround (L4532–4533). There is **no tanker, no forward arming point, no range-extension.** Falcon fuel 38 s, Warthog 30 s, Lancer 45 s. Fuel is a leash, never a decision.
- **Command is flat.** Air command is a pure headcount: `AIR_SLOTS = {airbase 6, airwing 3, aeroworks 4}` (L3818), `airCap()` L3819. Support is only `helicmd` "Overwatch" (L3239, a rotor aura). **There is no AEW&C, datalink or cueing**; range is each plane's own `range`.
- **Fighters are a single multirole blob.** `fighter` "Falcon" (L3214) is `multirole`, `groundAtk` and `ammo 14`. Role specialisation is thin: `pdjet`, `laserplane`, `strikejet`, `bomber` exist but there is **no SEAD, no electronic attack, no escort/SWEEP identity.**
- **The enemy never fights the air war.** Enemy air in `spawnEnemy` (L5591–5604) is `interceptor`, `bomber`, `gunship`, `strikewing`, `raider`, `jammer` (blinds homing missiles, `jammed()` L4970) and `hijacker` (L8797, targets drone swarms only). Nothing hunts *fighters* by role; nothing targets *radars*, *airbases* or *tankers*. v4.0.1 "Fleet Adapts" (`scanEnemyIntel` L5736) re-tasks by dominant element but has no air-superiority counter.
- **Airbases are indestructible in concept.** Only HP. Runways have no state, so aircraft always launch.
- **Banked but unbuilt:** ROADMAP L922 (Counter-systems: "Air-Superiority Swarm 'Reaver wing'" hunting your fighters) and L946 (air crews are SUSCEPTIBLE to catastrophe). I upgrade the Reaver wing below (AIR-3).

---

## 3. Bills

### AIR-1 — Aerial Refuelling Wing "Pelican" (fuel becomes geography)

**Pitch (Air Marshal):** A fighter is a fuel tank with opinions. Give me a flying petrol station and I will park my air war over *their* landing zone for an hour while the Ground Staff is still reading the map.

**Player fantasy:** A tanker orbit stacked behind the front: a rope of Falcons plugging in, topping up, and returning without ever touching a runway. The enemy hunts the tanker. You escort it.

**Clerk's Annex**
- **Mechanic:** New unit `tanker` "Pelican" (air domain, large `s:6`, speed 170, hp 260, armor 3, unarmed, ic 210, mp 10, build 11). Counts against airCap at weight 2. It **orbits at an anchor** the player places (hold-position command; ~600 px behind the contact). Any friendly fuel-bearing aircraft that would RTB (`u.fuel` below the home-trip estimate) and has a tanker within 1,400 px **refuels from the tanker instead**: transfer rate 12 fuel/s per aircraft, ≤4 aircraft at a time, tanker `fuelStock` 400 (refilled when the tanker docks 6 s at any airbase; a tanker also tops up its own fleet from `u.maxFuel`). Ammo is **not** restored: this is deliberate, so fuel and ordnance become separate problems. Optional **Forward Arming Strip** (S): `farp` building, cheap, rearms ammo only at a front where no airbase exists, destroyed easily, no production.
- **Real science:** Boom/drogue aerial refuelling (KC-135/KC-46, probe-and-drogue); buddy refuelling. The key insight designers miss: refuelling is a *range multiplier*, ~2–3× combat radius; also reduces the "tether" cost of RTB.
- **Code grounding:** `rearmAircraft()` L4522 (insert a tanker-seek branch before the home-base path at the fuel-low check; `AIR_RTB_EST_SPD_MULT` L4513 gives the fuel estimate); `UNIT_DEFS` L3214–3260 (add `tanker` beside `helilift` L3248 which already has `ferry`); `airUsed()` L3820 (weight 2); hold anchor command via existing capital-ship orders; `BUILD_DEFS` L3392 area for the optional `farp`.
- **Build size:** M.
- **Tree:** New node `airmobility` "Air Mobility", branch `age`, `pre: ['fusion']`, cost tier 1.5 (~200 base, after the 1.6 multiplier), **not dangerous**. Combo partner: `spaceage` makes the Pelican a **hydrogen-fuelled tanker** (SCI-FI-adjacent, plausible: hydrogen has 2.8× the energy per kg, tank-limited by volume) with `fuelStock` 700.
- **TM1.**
- **Tenet check:** Adds capability and range; nothing capped or denied. Tankers cost IC/MP like any unit, a legitimate upkeep-like cost; they are also a *target* the enemy can hunt (AIR-3).
- **Interactions:** Logistics/Economy (fuel is an IC sink at sortie scale, addressing surplus IC): optional `fuelStock` refills cost IC (e.g. 0.4 IC/fuel-unit). Naval: carriers could host tankers. Ground: `farp` ties into forward airbases on captured territory.

### AIR-2 — Airborne Early Warning & Datalink "Argus" (the wing becomes a network)

**Pitch:** Today a jet shoots what *it* sees. In a real air force a jet shoots what the *sky* sees. Give me one radar plane at altitude and thirty Falcons suddenly know where everything is, and where they are not looking.

**Player fantasy:** A slow, expensive, fragile dish-plane high above the formation; a visible lattice of cueing lines from it to every friendly in the net; lose it, and the wing is suddenly blind.

**Clerk's Annex**
- **Mechanic:** New unit `awacs` "Argus" (hp 190, armor 2, speed 150, ceiling STRATO, unarmed, ic 380, mp 12, build 16, airCap weight 2). A **datalink aura** radius 1,100 px (beyond the unit's own sight): every friendly aircraft and AA gun in-net (a) acquires targets **inside the net, not just their own range**, so fighters pre-position instead of drifting (`AIR_PATROL_FRAC` L4787 currently has them idle-drift to the nearest threat), (b) gains **+20% effective missile range** from launch-on-remote (a missile can be fired from a jet that never saw the target), (c) shares **one designated target** (the most dangerous enemy), cutting overkill across a 240-strong wing. Multiple Argus nets don't stack (no tenet issue; only the strongest applies), but **a second Argus within 2,000 px adds redundancy**: if one dies, the net persists. Loses the net entirely if jammed (see AIR-4/enemy `jammer` L5604).
- **Real science:** AESA radar, E-3 Sentry/E-7 Wedgetail, Cooperative Engagement Capability (CEC) and Link-16: the network turns each shooter into a sensor-and-launcher. The unthought-of angle: **launch-on-remote** (AIM-120 + datalink) lets a fighter fire from beyond its radar horizon.
- **Code grounding:** aura pattern from `helicmd` L3239 (`aura: { rof, range, r }`) and `radar` L3406 (`aura: { range: 1.12 }`); target acquisition `acquireIncoming()` L4465 and `canIntercept()` L4464; idle-drift constants L4787–4821; `jammed()` L4970; air brain in `updUnits()` L7809.
- **Build size:** M.
- **Tree:** New node `datalink` "Datalink Command", branch `energy`, `pre: ['directedenergy']`, cost tier 2, not dangerous. **Combo:** `datalink` + `pointdefense` -> **Cooperative Engagement** (Aegis Batteries and the CIWS network fire at what the Argus sees, extending ground AA range +15%). This changes the world (a shared battlespace), not just a unit.
- **TM1.**
- **Tenet check:** Pure buff plus a single point of failure. The weakness is the enemy's choice (AIR-3 Weasel), not a hard cap.
- **Interactions:** Defense Council (CEC combo); Space Council (a satellite uplink "SATCOM" upgrade could give the Argus orbit-wide reach); Intel/Society (the forecast-sharpening intel slice of Article IV.4: Argus nets could sharpen the wave forecast by 20%).

### AIR-3 — The Reaver Wing and the Weasel Flight (an enemy that fights the air war)

**Pitch:** I am tired of winning because nothing contested the sky. The Autofleet is afraid? Good. Afraid things *hunt fighters first*. Send me Reavers. Send me Weasels. Make me earn it.

**Player fantasy:** A fast, nasty enemy squadron screams down the net: Reavers go straight for your Falcons; Weasels fire anti-radiation missiles at your Argus and radars. You scramble, you screen, you retaliate with your own Pelican-fed sweep.

**Clerk's Annex**
- **Mechanic (upgrading banked ROADMAP L922 "Reaver wing"):**
  - `reaver` (enemy air): speed 380 (faster than Falcon 340), hp 34t, armor 1, short-range cannon + 2 hunting missiles; **target priority = nearest player fighter-class aircraft**, break-off at 30% fuel. Spawns in wings of 6–10 gated on `G.counterBias` air-dominant read (v4.0.1) once `invTime > 120`. Never spawns before the player fields ≥ 20 fighters (so air is read as "dominant").
  - `weasel` (enemy air): speed 300, fires an **anti-radiation missile** (`weapon: 'arm'`, homing on emitters: Radar, Argus, Aegis, Laser Tower, Particle Cannon) from 900 px; the missile **loses lock if the emitter is switched off** (a player order: "emissions OFF" for 12 s, which costs the aura during that time). Dies easily (hp 20t).
  - Both are *alternate wave kinds* in `waveAirKind()` L5524 (weights by `counterBias`).
- **Real science:** Wild Weasel/SEAD doctrine (AGM-88 HARM homes on radio emissions), and fighter sweep/OCA. The aircraft pattern is the 1960s "Wild Weasel" answer to SA-2 sites. Emission control (EMCON) is the real defender's response.
- **Code grounding:** `spawnEnemy()` L5581, kind branches L5591–5604 (`interceptor`, `strikewing`, `raider` are templates); `waveAirKind()` L5524; `scanEnemyIntel()` L5736 + `G.counterBias` (set in the dominant-element block ~L5518); target selection in `updEnemies()` L8596 (the `hijacker` branch L8797 is the pattern for "home on a specific class"); `PD_SAT`/`pdBudget` L8554/L8620 for the Weasel missile saturation.
- **Build size:** M.
- **Tree:** None needed (enemy). Telegraph through the existing `⚠ AUTOFLEET ADAPTING` banner (L5790).
- **TM1.**
- **Tenet check:** The *textbook* tenet move: difficulty comes from a scarier, smarter enemy; no unit capped. Tankers, Argus and emitters become hunted, so craft matters.
- **Interactions:** Defense (their radars become targets; Aegis needs EMCON discipline); Society/Fleet Psychology (the fleet's "afraid" canon: Reavers as the fleet's panicked reflex to air dominance, a psychology event: "ACE WING SCRAMBLED"); Ground Staff (Weasel missiles also threaten Laser Towers).

### AIR-4 — Electronic Attack Jet "Shrike" (open the door for the bombers)

**Pitch:** The bombers are cheap, and enemy bombarders sit behind interception budgets that refill. A Shrike is the key: noise that eats the enemy's interception budget while my Lancers go in. This is *combined arms*, comrades.

**Player fantasy:** A Shrike orbits the front, a visible noise-cone shimmer; the enemy's point-defence flickers; the Lancers dive through the gap.

**Clerk's Annex**
- **Mechanic:** New unit `ewjet` "Shrike" (hp 60, armor 1, speed 280, unarmed, ic 250, mp 14, build 9; `fuel 34`; airCap 1). While airborne within 900 px of an enemy, it applies a **noise-jam cone** (forward arc, range 900 px): enemies in the cone (a) suffer **−40% missile/rocket accuracy against friendlies** (extra lead error), (b) have their **PD interception budget drain 3×** (`e.pdBudget` L8554/L8620), so saturation arrives faster; (c) enemy `jammer`-kind (L5604) effects on friendly seekers are halved within its cone (burn-through). Stacks multiplicatively with up to **3** Shrikes per target (no hard cap; the third just stops adding, because accuracy bottoms out). A Shrike *cannot jam through* enemies with `shield` (shield is hardware, not a sensor).
- **Real science:** Noise and deception jamming (EA-18G Growler, EA-6B), burn-through range (jam power vs target radar power drop with R^2 vs R^4), and self-protection jammers. Crucially, jamming works because radar return falls off with R^4 while jam power falls off with R^2: the jammer wins at long range, loses close in. That gives a genuine tactical rule: the Shrike must stand off, then the strikers burn through.
- **Code grounding:** `PD_SAT` and `pdBudget` L8554/L8620 (v4.4 saturable PD); `jammed()` L4970 (enemy-side jam test; mirror for player jam); `ejammer` art L2826 (reuse sprite pattern); `UNIT_DEFS` L3216 (`pdjet`) as template; weapon accuracy in the projectile creation path (search `lead` in the missile code, `AIR WAR` note at L478).
- **Build size:** M.
- **Tree:** New node `ewarfare` "Electronic Warfare", branch `auto`, `pre: ['automation','directedenergy']` (a combo: sensors + computing), cost tier 2, **dangerous** `sc: 4` (EW lowers friendly comms for a while: unrest). Combo with `datalink` (AIR-2) -> **Networked Jamming**: Argus-linked Shrikes share cones.
- **TM1.**
- **Tenet check:** Adds a tool; no cap. The counter is the enemy's own `jammer` and `hijacker` (both already exist), so the electronic war is symmetric and fun.
- **Interactions:** Space (jamming Orbital Annihilator/bombarder PD budgets could finally let Laser Sats and Orbital Strike Fighters survive the bombarder grid, a lever for Report 6's problem; Space Council should claim it); Defense (their Radar auras give jam immunity radius).

### AIR-5 — Runway Wars: Cratering, Hardened Shelters, Rapid Repair

**Pitch:** The most valuable target on the map isn't a tank. It is the *concrete strip my 240 Falcons sleep on*. Make the enemy know it. Then give me shelters and repair crews and let me defend it like I mean it.

**Player fantasy:** The fleet cracks a runway with submunitions mid-sortie: half your wing is grounded, circling at fuel-low; a repair heli and a hardened shelter get you airborne again in 20 seconds.

**Clerk's Annex**
- **Mechanic:** Airbases/Airwings/Aero Works gain `runwayT`. An enemy **cratering bomb** (new `weapon: 'crater'` on a bomber variant "Durandal") sets `runwayT = 25 s` (stacks to 60 s) on a hit within 60 px of the strip: during that time **no aircraft can launch or land** from that base, and landing aircraft divert to the next base (existing RTB logic picks nearest base). A new building `shelter` "Hardened Shelter" (S): +3 airCap (extra slots); aircraft docked inside **take 85% less damage** and are **immune to cratering delay for launch** (they exit via taxiway). A **repair heli variant / engineer** (`helicmd` "Overwatch" gets `repairRunway` ability, or a new `heliengineer`) cuts `runwayT` decay ×4 within 200 px. Enemy bomber kind with `crater` weighted in `waveAirKind()` once `invTime > 90`, pressure only on bases with ≥ 8 parked aircraft (uses `airUsed` data).
- **Real science:** Runway denial: Durandal anti-runway bomb, JP233; hardened aircraft shelters (Gulf War); Rapid Runway Repair (RRR) crews fix a crater in ~2–4 hours; SCI-FI version is "nanite RRR". It is how real air forces lose air superiority on the ground.
- **Code grounding:** `BUILD_DEFS` airbase L3392, aeroworks L3433; `AIR_SLOTS` L3818; `rearmAircraft()` L4522 (check `base.runwayT` in the base-pick loop L4526–4533); `spawnEnemy()` bomber kind L5592; `waveAirKind()` L5524; `updEnemies()` bomb drop path (search the `'bomb'` weapon case).
- **Build size:** M.
- **Tree:** `shelter` unlocked by existing `pointdefense`-adjacent building tier; the heli engineer gated by `robotics` (already exists, branch `auto`, non-dangerous). No new node.
- **TM1.**
- **Tenet check:** Does not limit unit count or buff cost. It is a **scarier enemy plus a real cost (logistics)**, exactly what Article III prescribes. Mitigation is unlocked by building, not denied.
- **Interactions:** Ground Staff (anti-air/point defence around airbases); Infra (Rail Hub for fast repair crews); Space (orbital recon warns of the Durandal wave: forecast intel slice).

---

## 4. Hot Take — AIR-X: "Pilots Are People" (Aces, Bail-outs and CSAR)

**Pitch (the Marshal puts down his cup):** In seven reports, we have lost thousands of Falcons and *never once* lost a pilot, because we never had one. Planes are replaceable. **Pilots are the thing you cannot buy.** Give each Falcon a pilot with a record. When the plane dies, the pilot may eject. The Kites go get him. Get him back, and he walks out of the hangar an *ace*.

**Player fantasy:** A ragged wing of nobodies; one named pilot with 14 kills sprouts a gold tail stripe; he is shot down over the front; a flashing beacon; your Kites race a Reaver wing to extract him; he is back in the air by dawn. Every engagement has a face.

**Clerk's Annex**
- **Mechanic:** Every fixed-wing aircraft carries a `kills` counter (already trackable via the kill-credit path). At **5 kills** the pilot becomes an **ACE** (named from a list; +10% fire rate, +10% evasion; visually a tail stripe, shown in the roster). At 15, **DOUBLE ACE** (+20%). On aircraft death (ground-lethal hit, not on a catastrophe/atomised kill) there is a 35% chance (60% for Aces) a **bail-out beacon** spawns at the crash position: a downed-pilot object with 40 s life. **A friendly helicopter** (Kite, Kestrel, Chinook `helilift` L3248, which already has `ferry`) that hovers within 80 px for 3 s **recovers** him; he arrives at the nearest airbase and the next aircraft built there **inherits his kills and rank for free** (no IC). Enemy ground units target the beacon (priority). Pilots exist only on **named-airframe** classes (fighter, strikejet, bomber, laserplane, pdjet); swarm drones (`cdrone`, `kdrone`) and capital ships do not (no Ace spam: it stays a hero-scale layer on a mass-scale game).
- **Real science:** Combat Search and Rescue (CSAR, Sandy/Jolly Green), the ace mythos of the 5-kill threshold (a real convention), and the observed fact that a tiny number of experienced pilots score a disproportionate share of kills (the "ace effect": ~5% of pilots took ~40% of kills in WWII data). Experience genuinely compounds.
- **Code grounding:** kill attribution and death path (search `killUnit`/unit-death at ~L6813 `u.swarm` clear block; `die`-style handler near L7190 for ground-lethal landing); `UNIT_DEFS` `helilift` L3248 (`ferry`); `rearmAircraft()` L4522 for post-rescue spawn; research-free; `u.vet` field already used? (grep for `vet`/`rank` before building; if unused, add).
- **Build size:** M-L (kills counter, ace stat hook, beacon entity, rescue AI, roster UI, enemy beacon targeting).
- **Research tree:** `biotech`-like gating is unneeded; **optional** node `csar` under `robotics` (+ranged beacon pings from Overwatch). Not dangerous.
- **TM1.**
- **Tenet check:** Pure addition: free veterancy, free rescue, no cap, no denial. Cost is attention, which is the point; the enemy's targeting of beacons is the "scarier enemy".
- **Interactions:** Fleet Psychology (an enemy "ACE HUNT" event: the fleet gets afraid of named aces and sends Reavers after them, a TM2-adjacent psychological axis); Society (the **tone rule**: it is a beacon and a recovery timer, no scenes); Ground Staff (escort and secure the pickup); Naval (carriers receive recovered pilots).

---

## 5. Alliances Sought

- **Defense Council:** the Aegis/CEC combo with `datalink`, and the EMCON dilemma Weasels create for radar-heavy fortresses.
- **Space Council:** Shrike jamming of bombarder PD budgets is their route out of the Report 7 massacre; an Argus SATCOM uplink gives satellites a cueing role. We trade: they get the lever, we get a reason to fly Argus under orbital cover.
- **Naval / Sky Navy:** tankers deck-launched from carriers; sky capital ships escort Argus nets.
- **Intel / Society:** Argus nets as the forecast-sharpening building; Reavers/aces as fleet psychology events.
- **Economy / Logistics:** fuel as an IC sink (a direct answer to the 212k unspent IC in Report 7).
- **Rival:** the Ground Staff. They will say planes need nerfing. They are wrong; point them at Article III and at AIR-5.
