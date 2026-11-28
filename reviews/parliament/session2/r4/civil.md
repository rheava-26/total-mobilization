# Directorate of Civil Defence: Maiden Paper (Society Bloc, new seat)

## 1. Opening speech

*(blows whistle, twice, at the Speaker)*

Mr Speaker. For three rounds this House has argued about tanks, rods, hulls and heat. Not one council counted the people. I count them. I am the Director of Civil Defence, I started on a bicycle with a bucket of sand, and I am told I am now a minister. Fine.

Here is my census. There are seven cities, and each is a row of districts that dies one block at a time. A dead district stays dead for the whole war. It also charges the nation 2.5 points of stability for ever, because that counter only goes up. Nobody here has proposed a spade, a siren or a shelter. You gave the cities a rifle and a prayer.

The Armour Corps will tell you the city is "just a victory point". Wrong. A city is where the factories are, where the manpower comes from, and where the capital sits. The enemy lands next to my people on purpose. I want sirens, shelters, rebuilding crews and a ring of guns that gets stronger with every one you build. Attention, please. Fall in.

## 2. The Gap (what the code says today)

All line numbers are in `title.html`.

- **A city is a row of districts.** `buildCity` (L3688) makes a city of `n = round(w/42)` districts, min 3, sharing 700 HP. The capital (w 150) has 4 districts at 175 HP each. Two cities that overlap fuse into a megacity (`mergeCities` L3686, w up to 640), with HP summed and 15 districts.
- **Damage and loss are one-way.** `damageDistrict` (L3702) kills a district and adds one to `G.lostStructures`. Nothing ever decrements it, and no code revives a district. `cityStabPenalty` (L4547) charges 2.5 stability per lost district, for ever. A fallen city costs another −12 stability (L6580) and its defenders take 45% damage. Three lost 4-district cities is about −66 stability of drain, and no player action can reduce it.
- **Capture is cheap to stall.** The capture meter (L6586-6593) climbs only while enemy ground troops stand in the city and none of yours do. A single friendly rifleman stops it completely, and holds it frozen even with 30 enemy units present. An occupied city costs a flat −6 stability, hands your factories to the enemy (`setCityCaptured` L3718), and sprouts enemy siege guns. The `held` state is a heavy penalty, but the rule for entering it is thin.
- **Defence does not scale.** `BDEF` (L8373) has three tiers. `bDef` takes the maximum, not the sum (L6313). One Rotary Cannon Works arms every city on the map, and ten change nothing. Gun count is `1 + round(w/150)` (L8383), which is 2 for an ordinary city. The Garrison Stockpile (L3420) raises a global cap and ignores where it sits.
- **The capital is a label.** It is the decapitation drop target (L5238) and gets a yellow flag. Losing it is no worse than losing any other city. The only loss condition is all cities gone or held (L6604).
- **Cities help you only slightly.** `cityRecruit` (L3737) pays 0.5 MP/s per full city against a 4/s base, plus one free militia per 28 s.
- **Evidence from the reports.** Report 7 ended with 3 of 7 cities and the stability crisis. Report 10 kept 4 of 4 with stability never moving. The difference was the sky, not the city, which tells me the city has almost no levers of its own.

## 3. Bills

### CIVIL-1: The Reconstruction Depot

*Pitch.* "A dead district is a hole where my people used to be. Give me a work gang and 60 IC and I will fill it by Thursday. Bring a hard hat."

*Player fantasy.* You lose a block, and by the end of the wave it is a block again. You grind the war economy at the front and rebuild behind it.

**Clerk's Annex**
- **Mechanic.** New building **Reconstruction Depot** (PROPOSED): a depot that repairs wrecked city districts and sends work crews to them. 180 IC, 50 MP. Within 400 px it revives one dead district every 20 s at 60% HP, for 60 IC each. Each revival lowers `G.lostStructures` by one, which returns the 2.5 stability. A revived district has a 20 s "rubble" window in which it is a fixed 1 HP with no stability return, so it can't be rebuilt mid-barrage for profit. Depots stack: ten in range revive ten at a time. A city never rises from the dead (`ct.dead`); only districts do.
- **Enemy answer (Amendment I).** Reviving costs IC and ground crews; the enemy's `moraleBombing` x1.3 (L3703) and siege guns punish a city that keeps getting rebuilt. The Autofleet's ground drops walk the crews down.
- **Code grounding.** `damageDistrict` L3702, `G.lostStructures` L3707, `cityStabPenalty` L4547, and a new loop beside `cityRecruit` L3737.
- **Size:** M. **TM1.**
- **Research.** None needed; it rides the Nanite Foundry (existing: a building that self-repairs nearby structures) for a 2x rate.
- **Tenet check.** It is a pure buff, and it gives IC a sink and a reason to build. No nerf.
- **One-Button check.** Passes: build it, and districts rise on their own. Volume scales: 10 depots rebuild 10 districts per cycle, and a megacity is 15 districts.
- **Line check.** Passes: it works on a 1-D city strip, because the radius is horizontal only.
- **Interactions.** Stability (the penalty counter), Industry (IC sink), Fortifications (they own the guns, I own the plaster), Subterranean (deep districts are cheaper).

### CIVIL-2: The Siren and the Shelter

*Pitch.* "The siren goes off at 12 seconds, as the code says. I want it to *mean* something. Everyone below ground in 30 seconds, and keep the whistle out of your mouth."

*Player fantasy.* You hear the siren, and then the bombardment hits a city that has already gone to cover.

**Clerk's Annex**
- **Mechanic.** Two new buildings (PROPOSED). **Air-Raid Siren Net** is a tower that warns the districts near it when an orbital drop or bombardment is coming; 90 IC. **Civil Shelter** is a hardened basement under the city blocks; 120 IC, 30 MP. The warning goes up at the existing `RE-ENTRY DETECTED` event and on every bombardment tick. Each district within 300 px of a Shelter takes 30% less damage from bombs and bombardment while a siren has sounded in the last 20 s. Shelters stack up to 4 effective layers per district, then flatten at 70% reduction, since a city that is entirely underground is safe, and that is the design goal. A shelter-covered district that falls costs only 1.25 stability (half).
- **Enemy answer.** The Autofleet's earthbreaker and pelter (existing: heavy ground siege units) and bunker-busters ignore sirens. The fleet's attrition phase prefers districts with the most shelters, so shelters draw a heavier hammer.
- **Code grounding.** `doSkyfall` L5213 and the `RE-ENTRY DETECTED` warning, `hurtCity` L3711, `damageDistrict`, and the stability hit on loss at L3707.
- **Size:** M. **TM1.**
- **Research.** Attach to Point Defense (existing node).
- **Tenet check.** A buff with a stated cost. No nerf.
- **One-Button check.** Passes: build it and it fires itself.
- **Line check.** Passes, because it's a horizontal radius around a city.
- **Interactions.** Subterranean (shared digging, `UG_DEPTH` L3380), Stability, Autofleet.

### CIVIL-3: The Ring Works

*Pitch.* "Ten rotary cannon works should be ten rotary cannon works. One for 240 IC arms the nation. I want a ring, Mr Speaker. A ring."

*Player fantasy.* You keep building around a city until nothing dares to enter its airspace.

**Clerk's Annex**
- **Mechanic.** `cityDefend` takes the sum of works within the city's footprint (+400 px), not the max. Each additional Rotary Cannon Works or Plasma Bastion Works adds +1 emplacement to that city's `guns` count (L8383). A new tier 3, the **Aegis Ring** (PROPOSED: a ring of anti-missile interceptors around a city, built by a new Plasma Bastion upgrade), adds a flak shell at `rng 760, dmg 40, ceil .50`. It needs Deflector Shields (existing). Baseline remains global: one works arms all cities, as now, and extra works only add.
- **Enemy answer.** Fleet pelters and bunker-busters target the largest gun cluster first; the Fear Index (from the Stability council's bills) grows with city guns.
- **Code grounding.** `BDEF` L8373, `cityDefend` L8382, `G.bDefTier` L6313-6315, `rotaryworks`/`plasmabastion` L3457-3458.
- **Size:** S.
- **TM1.**
- **Research.** Plasma Weapons and Deflector Shields (both existing).
- **Tenet check.** Pure yes. No cap. No nerf.
- **One-Button check.** Passes and is exactly "scales with volume".
- **Line check.** Passes: cities are fixed points on the line, so the ring is the natural defence of a fixed point.
- **Interactions.** Fortifications (they ask for a Star Fort at 2,400 IC; this is the cheap volume path).

### CIVIL-4: The Mobilized City (Order Paper #5)

*Pitch.* "In 1943 the factories had no doors. The city *was* the war effort. Make mine look it."

*Player fantasy.* The skyline changes as the nation does. Every building you place in a city makes that city something else.

**Clerk's Annex**
- **Mechanic.** Each district reads the buildings within 60 px and the society state (`soc`, L3549) and picks a *civic form*. Factory-block (industry nearby): +3 IC/s per district. Barracks-block (Barracks or Garrison Stockpile): free militia every 18 s. Garden-block (Verdant): +0.2 stability cap. Server-block (Machine): +1 influence/s. Dystopia: all districts are Barracks-blocks with a −0.4 stability/s drain per district and +25% HP. No new buttons; the form is automatic.
- **Enemy answer.** Fleet targeting weights districts by civic form, so a factory-block gets hit first and the fleet reads your mobilization from the skyline.
- **Code grounding.** `buildCity` art field L3692, `society()` L3549, `cityRecruit` L3737, draw loop L9317.
- **Size:** L. **TM2** for the full society fork, with a **TM1 slice**: factory, barracks and garden forms only.
- **Research.** Biotech, Automation, and the society nodes.
- **Tenet check.** Yes. Pure upside.
- **One-Button check.** Passes; all automatic.
- **Line check.** Passes.
- **Interactions.** Lore (canon), Stability, Market, Command, Bio, Psi.

## 4. Order Paper answers

- **#3, the Line.** A city is a fixed point on a line, which is the cleanest thing the game has. It is *the* thing ground units defend. Today a single rifleman freezes capture; I propose capture should need your ground units to *outnumber* the invaders (CIVIL-1's depot gets a garrison link). Infantry earns its place by holding districts, tanks by clearing the approach, and artillery by covering both flanks of a point it can't reach by walking. A city is a ground-based anchor and the siege's goal.
- **#8, defensive buildings.** CIVIL-2 and CIVIL-3 above, plus CIVIL-1. I want sirens, shelters and rings, not more turrets.
- **#10, blatant overpowering or uselessness.** I found **nothing blatantly overpowered in the cities**, so I propose no nerf. The nearest candidate, one Rotary Works arming all cities for 240 IC, is cheap and strong, but reports 7 and 10 show cities still fell in a bad run, so it fails the proof test. **Useless today:** the capture meter (one unit freezes it), `CITY_ROTOR` (3 rotor pads per city, L3825), `cityRecruit` at 0.5 MP/s, and the rubble that never clears.

## 5. HOT TAKE: THE LAST SHELTER

*Pitch.* "When the last city falls, you lose in an instant. The designer hated that for stability. I hate it for people."

- **Mechanic.** A **Continuity Bunker** (PROPOSED: a deep underground shelter for the government and a seed population; 1,200 IC, sits at `UG_DEPTH` L3380). If every city is lost or held while at least one Continuity Bunker stands, the game does not end. A 120 s **Remnant** phase starts. The Bunker holds the capital's functions, pays a 40% stability floor, and rebuilds one district (CIVIL-1) every 20 s anywhere on the map. Retake or rebuild one city before the timer ends and the war resumes. Otherwise the loss fires as today. The Autofleet's tunnel assault (L6600) is how the enemy answers: ground units reaching the Bunker's surface point breach it.
- **Code grounding.** `end(false, 'ALL CITIES LOST OR OCCUPIED')` L6604, tunnel assault L6598, `UG_DEPTH`.
- **Size:** M. **TM1.** **Research:** Fortress Earth doctrine node (existing doctrine idea, ROADMAP L545).
- **Tenet check.** It denies nothing and rewards investment.
- **One-Button check.** Passes: it triggers itself.
- **Line check.** The vertical dimension is used, as the Address asks.
- **Interactions.** Subterranean Works (owns the deep layer), Stability.

## 6. Alliances sought

- **Stability** (Society Bloc, fellow whistle-blowers): the permanent 2.5 counter is theirs and mine.
- **Subterranean Works:** shelters and the Last Shelter live in their layer.
- **Fortifications Corps:** Ring Works vs Star Fort; I will take the volume path, they take the megaproject.
- **Industry and Command:** the IC sink.
- **Rifle Corps:** capture needs bodies.

To the **Armour Corps**: your tank is a metal box without a city to defend. Do try to remember what it is *for*.
