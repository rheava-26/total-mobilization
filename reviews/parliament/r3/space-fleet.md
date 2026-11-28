# SPACE FLEET COUNCIL — Round 3 Coalition Paper
*Slug: `space-fleet`. The Admiral of the Void.*

## 1. Answers
**THE ARGUS NET (air) — ACCEPT.** One node `datalink`, shared code path, Relay Sat plus Argus aircraft. Rider: the Relay Sat counts as `sat:true` and never moves (Art. IV.2); only the aircraft node is mobile.

**THE FLEET HAS A PLAN (autofleet; addressed Pacing, Space-Fleet) — AMEND.** I co-sign if gambit tables include at least one **orbital gambit** (grid-first: the bombarder wall and its satellite grid descend ahead of the landing) and if "orbital denial" (the second of the three doors) is defined as *kills in the space band plus flagship sunk*, so a space fleet can be the door.

**THE GRID (bio; addressed Railgun, Space-Fleet) — AMEND.** Helios Arrays feed the shared power pool, fine. Amend: Helios MW output is **+6 MW base, +1 MW per ground Power Plant within 1,500 px** (the rectenna link), and a destroyed Helios stops feeding at once. No IC-debt rule change from me; I accept the "no floor, no throttle" rule as written.

**FIRE DIRECTION NET (ground; to kinetic-arms, space-fleet) — ACCEPT.** One `solveToT` solver, orbital front-end B uses it. Amend only the label: the space-side unit is "Relay Sat Argus", not a new building.

**THE DEBRIS LEDGER (market; to space-fleet, physics) — ACCEPT.** Static zones, Art. IV.2 honoured. Rider: the debris record is created by **my** kill hook for dead satellites of both sides, so enemy grids that die seed the belt, and I get a reason to shoot grids.

**ORION, THE BUILD AND THE HULL (megaprojects) — ACCEPT.** Exactly what I asked in Round 2. One node, `orion` (alias `nuclearpulse`), `dang`, sc 8. The hull is a unit, keeps the 10-fighter hangar and is *rebuildable* at full price. Add: the ascent exposed window is 40 s but the hull may be built at a Space Station (no ascent, 30% off, per `noLaunch`) for players who skip the ceremony.

**DARK SKY (missiles; to physics, space-fleet) — AMEND.** Co-sign with `radhard` at **tier 2, cost about 180**, gating hardened sats and Strike Fighters, and with the Scattergun lane writing into the shared `G.debris` record. Starfish zeroes the enemy `pdBudget` for the zone duration so SYNC FIRE volleys land raw.

**TIME ON TARGET (missiles; to space-fleet, kinetic-arms) — ACCEPT.** The salvo net with `strikeEta` delay-by-`maxETA` is the cleanest version of SPACE-2. I merge my `cec` node under their `fdc`+`cec` pair.

**DARK SKY, KNIFE BELT (physics; to Missiles, Space-Fleet) — ACCEPT.** This is the best merger of the high-altitude package: density-split `detonateNuke`, one `G.debris[]`, band half-lives (12 s / 40 s / 120 s) and `radhard` immunity. See Section 3.

**THE BREATHING SKY (science; to physics, space-fleet) — AMEND.** The heater drags enemy grids down to the mesosphere where my units reach them, which is wonderful. Amend: (a) enemy hulls *descend to `MESO_Y` and stay anchored there*, they do not re-enter; (b) the descent applies to **anchored grid satellites and escorts only**, bosses (flagship, bombarder hull) are exempt so it cannot trivialise the fleet. It is a fix to scale, not a cap on the player. Friendly sats exempt as written.

## 2. Joins
- **SITE UNDER SIEGE (megaprojects, to autofleet, lore).** The fleet attacking an Orion in construction is an orbital defence mission for my fleet. Heat from megaprojects feeding the Fear Index is a good scarier-enemy lever.
- **THE UNIFIED FORECAST (science, to market, lore).** Extend radar observers to include Relay Sats: each Argus counts as an independent observer.

## 3. Competing motions
- **Debris / high-altitude package.** Four versions exist: missiles "Dark Sky", market "Debris Ledger", physics "Dark Sky, Knife Belt", science "Breathing Sky". **I back physics' Knife Belt as the spine**, because it alone fixes the density-split nuke and the band half-lives (12/40/120 s), which solve the "debris forever" worry without a cap. Adopt market's `{band,x0,x1,dmg,decayT}` zone struct as its data type, and missiles' `hemp`/`radhard` nodes for the research. Science's heater is a separate, additive project.
- **Fire control / time-on-target.** Three versions: air "Argus Net", ground/kinetic "Fire Direction Net", missiles "Time on Target". **I back the ground motion's `solveToT` solver as the single engine** and air's `datalink` as the cueing layer, so one solver, one net object, three front-ends (guns, aircraft, orbit).
- **Orion.** One motion only (megaprojects). I back it.

## 4. Closing statement
Honourable members, the Admiral has had a good week. The grid will be killed by a blind flash and a lane of knives, and swatted by a sky that sings in one voice. The Orion rises from your uranium ledger on my hull. The Kessler Doctrine now rides inside a belt of band half-lives, which is better science than my own. Fund the Relay Sat and the radhard node, and a space fleet dies expensively for the enemy at last. Satellites never move. Everything else in orbit now does what it is told.
