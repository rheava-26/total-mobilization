# GROUND FORCES COUNCIL — Round 1 Position Paper (`ground`)

*The Army General Staff. Seat: the mud.*

## 1. Opening Speech

Honourable members of the air, the sea and the sky-that-is-mostly-vacuum.

I am the Field Marshal of the Army General Staff, and I have been reading Report 7. Two hundred and thirty-two of two hundred and fifty-one infantry dead. Ninety-two percent. The Air Council would like you to call that a "casualty rate." I call it a *tithe*. We pay it every war, and every war we are handed a bill for the tank, the rifle and the boot, and then somebody in a clean flight suit gets the medal for Report 8: 240 Falcons, 113 Kites, ten to one.

Let me be fair. The Falcons are lovely. They are also parked in a hangar between sorties while my lads hold a strip of dirt that is, I remind you, the *only thing the enemy is actually here to take*. The fleet lands on ground. It builds a beachhead on ground. It fortifies *our cities* on ground. A planet is a thing you stand on.

And what do my men do? They march to wherever the idle-march code points them, stop, dig a foxhole, and wait. They have no line to hold, no reserve to feed, no order to obey, no battalion to belong to. A squad is three bodies and a prayer. Nobody asks them to do anything except stand where the plasma lands.

Give the Army a *job*, a *shape* and a *name on the flag*. We hold the line. We have always held the line. Now let us *be* a line.

---

## 2. The Gap

**Evidence from the code and the record:**

1. **Chaff by design.** Infantry is `inf` (UNIT_DEFS L3185): 100 IC, 46 HP, armor 2, squad of 3 bodies (`SQUAD_LOADOUT` L3953). Report 7 lost 232 of 251. The fleet *hunts* them: the Bombarder snipes ground units with orbital plasma, infantry-heavy builds draw gunships and plasma `pelter`s (`scanEnemyIntel`, `GROUND_BASE_WEIGHTS` L5481). A unit that cannot shelter, retreat or be replaced is a consumable, not an army.
2. **No command layer.** ROADMAP "BATTLEGROUPS" (L441-452) banks the designer's own wish: *"squads upon squads will form battalions of the same type."* It was deferred as a perf project. Perf is now solved (v4.9.0 stamped art), so the blocker is gone and the idea is still sitting there. There is also a "battlegroups stopgap" in v3.100.0 that did not become a command structure.
3. **Entrenchment is the only ground mechanic.** `updUnits` L8205-8212: stand still 9 s, get `u.entr`, up to 60% soak, and a free auto-emplacement (`mgnest`/`flaknest`/`artnest`, `EMPLACE` L4558). The roadmap (L1027) already called the free emplacements "a wall of bullets" and they were nerfed. Dig-in is a passive tax, not a decision.
4. **One-axis movement is read as a flaw.** The designer called ground units "wonky" (CLAUDE.md). I say the strip *is a front line*, exactly like the Eastern Front or the Western one, and a front line has depth, reserves, withdrawals and counterattacks. Nothing in the game uses it.
5. **The enemy ground game is richer than ours.** `gtank`, `gmech`, `pelter`, `earthbreaker`, `leviathan`, the `ebreacher` beachhead anchor (L2773), shield domes, three landings placed by *least damage taken*. The player's answer is "stand there."
6. **Mass is back, and nothing spends it.** 212k IC unspent in Report 7. Ground has no IC sink that is not a body: no engineering, no mines, no logistics, no mass artillery doctrine.

---

## 3. Bills

### GROUND-1 — THE BATTALION (body → squad → battalion, with replacement drafts)

**Pitch (in character):** "A squad is a funeral waiting for a flag. A *battalion* is a thing with a name, a commander and a replacement pipeline. Raise 40 squads of Infantry and they become the 3rd Rifles: one thing on the field, a hundred and twenty rifles in loose order, a casualty list that thins the line instead of deleting it, and a draft office in the rear that sends fresh boys forward at no cost in manpower, because the Treasury has already paid for the uniforms. When the 3rd Rifles loses its colour sergeant, it fights harder. Come on. You can see it."

**Player fantasy:** Seeing your army become *units with identity*. A thousand-man block of infantry advances behind a line of tanks, bleeds down to a core, refills from the barracks, and returns with a battle honour.

**Clerk's Annex**
- **Mechanic.** Same-type ground squads (inf, shock, veteran, android, cyborg, psitrooper, clone) within 120 px and idle for 3 s *auto-muster* into a **Battalion**: one entity (`u.bn`) with `n` squads folded into a pooled `bnHp` and `bnN` body count. Combat uses Lanchester's square law for aimed fire: battalion damage output scales with `bnN`, but it renders and simulates at most 12 representative bodies (reuse `swarm:true`/`nMax` growth from v3.103 cdrone, L3958). Each 10% of strength lost removes a body and drops a 2-second *cohesion* check: below 40% strength a battalion with no reserve is *Shaken* (−25% rate) until it is relieved or rejoins. **Replacement drafts:** a battalion within 400 px of a Barracks/Veterans' Hall/Fortress HQ regrows lost bodies at 1 squad per 6 s for **IC only** (`d.ic`, no new MP, because the MP/army-cap formula is untouched per Art. IV.1). **Honours:** a battalion that survives 3 engagements earns *Veteran* (+10% dmg, +15% HP), 6 earns *Guards* (+20%/+30%, own sprite flag). No cap on battalion count or size.
- **Army cap interaction.** Cap accounting uses existing weights (`ARMY_WEIGHT_MULT` L3786). A battalion's weight is the sum of its squads. A replacement draft respects cap, and while the cap is full a depleted battalion shows `AWAITING DRAFT`. This is a real cost (logistics), not a nerf.
- **Code grounding.** `spawnSquad` L4091, `SQUAD_LOADOUT` L3953, `stepSquadBodies`/`SQUAD_SEP_MAX_N` L3990, `updUnits` L7809 (entry point; insert a muster pass every ~1 s on the same cadence as the garrison fill), `drawSquadUnit` L9421, `spawnGarrison` L4573 (template for a refill source), `hurt` L4971 (route damage into `bnHp` and pop bodies).
- **Build size.** L (about 500-800 lines): muster/dissolve, pooled HP, render LOD, select/rename UI, draft loop.
- **Research.** Attaches to the age spine: no new node, unlocked at game start (it is the conventional army), with a **Hive Mind** combo (`hivemind`) making a battalion share target locks. Clone Battalion (`cloning`) musters at 2x speed.
- **Real science.** Lanchester's equations (aimed-fire square law: concentration beats dispersion), cohesion/morale thresholds (Ardant du Picq, Marshall's studies on fire discipline), and the replacement-depot (*Ersatz*) system used by every army since 1914.
- **TM tag.** TM1.
- **Tenet check.** Passes. It adds toys, uncaps mass, and gives the fleet a reason to bring harder counters. It only prices replacement in IC the player already hoards.
- **Interactions.** Perf (Engineering): this is the banked battlegroup architecture and halves per-body AI cost. Air: `helilift` (Chinook, `ferry:true`) can lift a whole battalion. Logistics/Economy: draft IC is a new, useful sink for the 212k idle IC.

---

### GROUND-2 — THE ELASTIC LINE (named fire lines, fighting withdrawal, counter-attack)

**Pitch:** "The strip is a *line*. That is the whole genius of this map and nobody has noticed. Let me draw you three rulings on it with a grease pencil: the **Outpost Screen**, the **Main Line**, the **Reserve Line**. When the plasma starts falling on the Screen, my boys *withdraw to the Main Line without being told*, the way Ludendorff taught every army that survived 1917, and the enemy walks into the Main Line's guns having bombed an empty field. When their tanks stall, the Reserve counter-attacks. Defence in depth. Not a foxhole. A *plan*."

**Player fantasy:** Drawing a line on the map and watching an army carry out a doctrine with discipline and without micromanagement. Fighting retreats that bait the enemy into a kill zone.

**Clerk's Annex**
- **Mechanic.** Player drops up to any number of **Line Markers** (a draggable x-interval strip on the ground band, free to place) tagged `SCREEN`, `MAIN`, or `RESERVE`. Battalions (GROUND-1) or loose squads with orders `Assign Line` hold their line. **Elastic rule:** a `SCREEN` unit that takes sustained incoming fire above a threshold (5 s of >X dmg, or a counter-battery/orbital lance tracking its cluster) executes *fighting withdrawal* to the nearest `MAIN` line at +25% speed, firing while moving (reverse-slope behaviour). On reaching the `MAIN` line it digs in with `fort` boosted +15% (cover from the line). **Counter-attack trigger:** when the enemy ground wave stalls inside a `MAIN` line's kill zone and enemy density in the interval drops below 40% of its peak, `RESERVE` battalions auto-advance up to 600 px, kill, then fall back. Each line is a one-axis interval, so it fits the world natively. The idle-march-to-front code (`updUnits` idle path, v3.48.0) is replaced by "go to your assigned line".
- **Code grounding.** `updUnits` L7809 (movement target + idle march), entrenchment block L8205-8212 (`u.entr`, `u.fort`), `fortBonusAt` L4542, `G.dmgTaken` x-buckets used by `phase3AllOut` (the fleet's landing chooser reads *least damage taken*; lines let the player bait it), `acquireEnemy` L4374, HUD/minimap overlay (v3.48.0 minimap, remove the old front marker v4.1.3 and replace it with line bands).
- **Build size.** M-L (about 400-600 lines): marker placement UI, state machine, withdrawal pathing (1-D, so cheap), minimap rendering.
- **Research.** Unlocked at start. A new node **`doctrine_depth` ("Defence in Depth", tier 2, def branch, cost ~120, not dangerous)** gives +1 withdrawal speed and unlocks Reserve counter-attacks. Combo with `pointdefense` unlocks auto-link to Aegis coverage, and with `robotics` (mechs withdraw without morale checks).
- **Real science.** Elastic defence in depth (German Hindenburg Line, 1917), reverse-slope defence, and fire-and-manoeuvre. It beats orbital snipers (Bombarder plasma) because the target has moved.
- **TM tag.** TM1.
- **Tenet check.** Passes. No cap, no nerf: a player-driven doctrine. It gives the enemy a reason to evolve: the fleet's `scanEnemyIntel` can detect "elastic" and send plasma `pelter`s to hit the Main Line (a scarier enemy, not a weaker player).
- **Interactions.** Fleet psychology (TM2): a line that never breaks feeds the "afraid" canon (the fleet withdraws earlier). Space/Air: the withdrawal buys time for orbital reach.

---

### GROUND-3 — COUNTERBATTERY & THE TIME-ON-TARGET (artillery as a ground arm)

**Pitch:** "You gave me the SPG 'Thunder' and said *Slow.* Slow is a feature, you philistines; artillery is not about speed, it is about *arithmetic*. Give me radar that watches their shells in flight, back-computes where the gun sits, and has my guns kill it before the second round leaves the tube. Give me 300 guns that each time their shot so *every shell lands in the same second*. That is not firepower. That is a **verdict**."

**Player fantasy:** Painting a box on the map and watching hundreds of guns land one thunderclap that erases the landing. Silencing the enemy's `pelter`/Bombarder battery within seconds of its first salvo.

**Clerk's Annex**
- **Mechanic.** (a) **Counter-battery radar** (new building `firefinder`, INFRA, ic 240 / mp 50, radar range 1,400 px, ceiling 0): detects any enemy ground shell/rocket projectile in flight (`PS` table L4892: `shell`, `rocket`) and back-projects the origin along the ballistic arc. Linked SPG/MLRS/Hydra within range auto-fire at the origin ±40 px as priority 1 (`acquireEnemy` filter), 2-second reaction. (b) **Time-on-Target fire missions:** player selects artillery group → paints a target box (reuse the Fire Control UI) → each gun computes its own flight time and delays its trigger so all shells impact within 0.25 s (the MRSI/TOT technique). Salvo size is unbounded; it is limited by ammo logistics (gun `ammo` field, resupplied from the Ammunition Depot, an existing building), a real cost. (c) **Shell variants** by research: smoke (blocks `lock` for 6 s), DPICM cluster, and **extended-range base-bleed** (+25% range, real, ~5-8% in practice).
- **Code grounding.** `UNIT_DEFS` arty L3189, mlrs L3190, hydra L3194; projectile speed table `PS` L4892; `acquireEnemyMulti` L4398; Fire Control code (`v3.9.0`, reuse the box designation and group radar, which the designer called a "nothingburger" because it never *did* anything meaningful: TOT fire missions give it a purpose); Ammunition Depot in `BUILD_DEFS`; enemy `pelter` spawn L5656 (`kind==='pelter'`).
- **Build size.** M (about 250-350 lines).
- **Research.** New node **`firecontrol_net` ("Fire Direction Net", tier 2, energy/def border, cost ~150, not dangerous), pre: `directedenergy` or `pointdefense`**, combo with `automation` (autonomous guns need no spotters, +20% rate) and `particlebeam` (particle-battery counterfire out of the orbital ceiling).
- **Real science.** The AN/TPQ-37 Firefinder (counter-battery radar, ~50 km), MRSI (Multiple Rounds Simultaneous Impact, PzH 2000, Archer, K9), time-on-target (WWII). All real.
- **TM tag.** TM1.
- **Tenet check.** Passes. It unlocks toys. The enemy answer: Bombarder shifts battery after firing (a real counter: shoot-and-scoot), so the player has to out-think it.
- **Interactions.** Salvages the "Fire Control" nothingburger by giving it a real use; Radar/Intel council (forecast-sharpening). Air: smoke shells shield Falcon strike runs.

---

### GROUND-4 — THE ENGINEER CORPS (mines, obstacles, breach and bridge)

**Pitch:** "The enemy walks along a single axis. A single axis! You could not design a better minefield if you were paid by the metre. Give me combat engineers, and I will carpet the approach with scatterable mines, tank ditches and dragon's teeth, repair the Main Line under fire, and seal their `ebreacher` anchor behind a wall. They will send breaching rollers? *Good.* Now it is a war of engineering. My sappers have not lost one of those yet."

**Player fantasy:** Engineering the battlefield: laying 5,000 mines in an afternoon, then watching a landing grind to a halt in the carpet while artillery works the stalled column.

**Clerk's Annex**
- **Mechanic.** New unit **Combat Engineers "Sapper"** (ground, `grp` 2, 90 IC / 8 MP, hp 80). Orders: *Lay Minefield* (paint an x-interval; engineers lay a field of mine cells, 1 per 6 px, each 140 dmg / 24 px blast, `armor`-piercing vs the underside, so triple damage on tracked/legged hulls, and zero vs air); *Fortify* (construct/repair `EMPLACE`-class structures at 3x the auto-emplacement HP, using IC, not free); *Breach* (clear an enemy dome generator or `ebreacher` footings so friendly artillery can hit it); *Tank Ditch* (terrain `SINK` mode: reuse `TERRAIN_MODES`, a one-cell ditch that stalls enemy `gtank` by 6 s). **Enemy answer** (scarier enemy, not a nerf): the fleet's `breacher`/`gmech` get a *mine-roller* variant and `earthbreaker` seismic presses crack ditches; sappers' mines are visible to enemy drones within 300 px, so they must be hidden (research *Stealth Mines*).
- **Code grounding.** `UNIT_DEFS` (add near L3289), `TERRAIN_MODES` (RAISE/LEVEL/SINK) for ditches, `deployEmplacement` L4564 for fortify, `spawnEnemy` L5597-5620 gtank/gmech/leviathan (apply `hurt()` L4971 on mine contact), `G.parts`/`boom()` for effect, `hurt` armour handling.
- **Build size.** M (about 300 lines): unit, mine entity list (cheap: one array of x-sorted cells), 1-D collision check.
- **Research.** New node **`sappers` ("Combat Engineering", tier 1, auto branch, cost ~70, not dangerous), pre: `info`.** Combo with `nanotech` → **Nanite Mines** (self-replicating, regrow 1 cell / 20 s; SCI-FI with internal logic: Autofleet's own ordnance is made of a swarm medium, so Earth's swarm medium repurposes it) and with `robotics` → *Mine-laying drones*.
- **Real science.** FASCAM/Volcano scatterable mine systems, combat engineering vehicles (AVRE, Terrier), obstacle belts (Kursk, 1943: ~2,500 mines/km), and anti-tank ditches.
- **TM tag.** TM1.
- **Tenet check.** Passes. Mines are another toy and an IC sink; the enemy answers are scarier fleet units, not a cap.
- **Interactions.** Fleet (enemy designer) must supply a mine-clearer; Terrain council (ditches); Economy (IC sink).

---

## 4. Hot Take

### GROUND-X — THE CRAWLER CAPITAL (a city that walks)

**Pitch:** "You keep trying to defend a city. I am proposing you *stop letting it stand still.* The enemy's Decapitation Strike aims at the capital; a capital that is already 3 kilometres down the road when the lance lands is a capital that did not die. Real engineers moved the Swedish town of Kiruna. We will do it with tracks."

**Fantasy:** Your capital grinds along the strip on gigantic crawler-treads, its districts bolted to a moving fortress hull, shields and flak racked on the roof, escorted by battalions. The fleet has to *lead* the target.

**Clerk's Annex**
- **Mechanic.** A megaproject (tied to the "built out in sections" idea, Art. IV.3, tier 2: *megaproject built on purpose*). **Crawler Pads** (a new `INFRA` building, 8 sections, ic 600 each) are built beneath a chosen city. When complete, `ct.mobile = true`: the city's districts translate along x at **2-4 px/s** (crawler-transporter scale: NASA's crawler moves 3.2 km/h at 8 million lb; Kiruna's 2025 relocation moved large buildings at similar speeds). Cities have `siegeT`, `districts`, and `capital` flags already; add a `ct.vx` and shift district x, city sprites, and attached buildings (`b.parentCity`). Moving costs IC upkeep scaled by city HP and prevents recruitment during motion (`CITY_RECRUIT_MP`, `CITY_SIEGE_COOLDOWN` rules apply). **Enemy answer:** the Decapitation Strike (`INTENT` L5119, `spawnSuper`) leads the target with an interception window; the fleet adds *track-breakers* (`earthbreaker` seismic press) that strand a crawler for 20 s. SCI-FI note: the fleet's intel is stale because its warp catapult only sent one ship (Art. V), so it plans against a static map; a moving city makes the "thrown friend" misjudge. Fits canon.
- **Code grounding.** `buildCity` (called L3628, L3694), `damageDistrict` L3702, `hurtCity` L3710, `CITY_*` constants L3727+, `phase3AllOut`/`INTENT` L5119 (target selection), `avoidCityCenter()`.
- **Build size.** XL (>1,500 lines: moving districts, every building/unit attached to a city, terrain gradient limits, collision with other cities and water).
- **Research.** New node **`crawler` ("Crawler Engineering", tier 3, auto branch, cost ~320, `dang` sc6), pre: `colossus` + `nanotech`.** Combo with `shieldtech` (a shield dome moves with the city).
- **TM tag.** TM1 for a single mobile capital (S/M scoped), TM2 for a fleet of crawlers.
- **Tenet check.** Passes. The player gets a moving city; the enemy gets scarier.
- **Interactions.** Fleet psychology (stale intel), Terrain (flat ground required), Cities council.

---

## 5. Alliances Sought

- **Logistics / Economy council:** GROUND-1 drafts and GROUND-3 ammo are real IC sinks; I will back their upkeep systems if they back my battalions.
- **Intel / Radar council:** GROUND-3 (counter-battery radar) and GROUND-2 (line overlay and fleet reaction) feed their forecast intel.
- **Fleet / Enemy designers:** every ground bill demands a scarier answer (mine rollers, shoot-and-scoot, track-breakers). I ask them to author those.
- **Space council:** the elastic line buys time for orbital reach, and the Bombarder's ground snipe is the common enemy.
- **Air council:** I will lend them smoke and lift (Chinook) in exchange for medals. I sort of mean it.
