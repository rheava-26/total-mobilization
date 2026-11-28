# The Member for the Street (`street`) — crossbench paper

## 1. Maiden speech

Madam Speaker, I have read this nation's code the way I read a ration book. It knows a great deal about *machines* and almost nothing about *people*. The people are four numbers: manpower, influence, stability and a hit-point bar shared by blocks of flats. A city is a row of rectangles that musters half a soldier a second. A destroyed district is a permanent minus 2.5 on a dial. Nobody is moved, fed, trained or told anything.

In 1940 the factory floor *was* the front. Women moved into the shell shops, children collected scrap, and a street learned its air-raid drill. The state asked a hundred million people to change jobs, and it kept morale up by giving them things to *do*. This game has the war machine without the home front. I propose we build the home front. We do it with standing orders and volume, never chores. The player keeps every toy, and the enemy gets to be frightening to an actual society.

## 2. What the code tells you

1. **A city is hit points with a skyline.** `buildCity` (line 3675) makes `n = round(w/42)` districts, each with `hp`, `dead` and an `art`. The art is `block`, with one `powerplant` and one `factory` look-alike. Those are only pictures. They produce nothing, and `damageDistrict` (3702) just subtracts hp.
2. **Districts never come back.** I grepped `rebuild`, `repairCity` and `heal` and found nothing that restores a dead district. `dist.dead = true` and `G.lostStructures++` are permanent. `cityStabPenalty()` (4547) charges `lostStructures * 2.5` forever. So a city's death is a ledger entry, not a wound that heals.
3. **Manpower is a pool, not people.** `G.mp` is capped at 900 (6335). It grows from a base of 4/s plus Housing Blocks (`econ:{mp:3,cap:4}`, 3384) and `cityRecruit` (3739). Cities give a flat `CITY_RECRUIT_MP = 0.5` per second, scaled by hp fraction. Nothing is conscripted, trained or drained. The only pressure on the pool is spending it.
4. **Stability is a dial with no social cause.** It rises toward `stabCap` (6338) by a flat 0.5/s. The cap is raised by Comms, Propaganda and Radar. Nothing in the world changes with the dial: no strikes, no queues, no looting. At zero you get the capitulation crisis (6643), where production freezes and troops mutiny. It is the one place where the people finally show up, and it is a pop-up.
5. **"Society" is a paint job.** `society(kind)` (3549) swaps the sky gradient and a tint. The only mechanical readers are `evtReqMet` (1066), which gates one event choice, and the after-action report (10411).
6. **The one real home-front scene is switched off.** `EVENTS_LIVE = false` (1099). The one event, `autofleet_arrival`, is about families fleeing the housing blocks. It is the best writing in the file, and it is dormant. Its choices (reassure, refuse, strict guidelines) are one-shot stability numbers.
7. **Housing is the only place civilians live.** `housing` death costs a flat -5 stability (6571). Elsewhere, ambient cars and airliners flee when the fleet arrives (1845), but this is cosmetic. The population runs, and it costs and gives nothing.
8. **Mobilization is only ever additive.** Every economic `eff` string reads "+45% IC" or "+30% manpower". Nothing is converted. The 1940s story is conversion: a car plant becomes a tank plant, a school becomes a barracks. Here you only place more rectangles on bare land.

**What total mobilization would mean mechanically:** it means *converting* peacetime life into war capacity, and paying for it in morale, with a visible cost that recovers. The code has the currencies (stability, influence, manpower) and the crisis event. It does not have conversion, shifts, or a civilian layer.

## 3. Bills

### Bill 1 — The Conversion Order

*"Madam Speaker, my constituents' car factory stamped tank hulls by Tuesday. Let the districts of our cities do the same."*

**Fantasy:** your cities themselves go to war, and you watch their blocks light up as factories.

**Annex**
- **Mechanic:** one standing choice per city: CIVIL / WAR FOOTING / TOTAL. A War Footing city gives a small `econ` bonus for each live district. A `factory`-art or `powerplant`-art district (those exist today as skyline art on a city district) is made real. TOTAL gives roughly +60% of that yield. It costs a fixed cap reduction of 2 stability points per footing level, using the same "lower ceiling, not a bleed" rule (6333). Numbers: War Footing +0.4 IC/s per live district, -2 cap. TOTAL +0.7 IC/s, -5 cap.
- **Hooks:** `buildCity` (3675), `cityRecruit` (3739), `capPenalty` (6333).
- **Size:** M.
- **Tenet:** gives power, costs stability ceiling. The enemy's answer is that cities on TOTAL are the capital-strike targets for the decapitation intent, so exposure is the price.
- **One-Button:** one toggle per city, or one global "footing" switch. Nothing per unit.
- **Line:** untouched. The effect is a ledger on the surface strip.

### Bill 2 — Ration Books

*"My street knew what a ration book was for. Let the player choose how tight the belt is, once."*

**Fantasy:** a war economy where you set the national table: guns or butter.

**Annex**
- **Mechanic:** one global standing setting with three notches: FREE, RATIONED, STARVATION-DIET. RATIONED gives +25% manpower income and a -4 stability cap. DIET gives +50% manpower and a -10 cap, and the downside is capped by the existing `stabCap` floor of 20 (6338). Stability *regen* rises 20% per notch, so the "recoverable penalty" feel of the crisis system is kept.
- **Hooks:** `G.mult.mp`, `capPenalty` (6333), the `mpR` calc (6312).
- **Size:** S.
- **Tenet:** a trade, not a cap. It never blocks building anything.
- **One-Button:** one setting, in the HUD stability cell.
- **Line:** untouched.

### Bill 3 — The Shelter Programme

*"A district that falls should be a funeral and a rebuild, not an accounting entry."*

**Fantasy:** your cities bleed and heal, and you commit steel to the rebuild.

**Annex**
- **Mechanic:** a Reconstruction Yard (proposed building: a repair depot, ECON category) restores one dead district per 20s per yard, at 90 IC and 10 manpower per rebuilt district (`dist.dead=false`, `hp=maxHp`). Each rebuild also removes 2.5 from `lostStructures`. An "Air-Raid Shelter" research would reduce the stability loss per district kill by 50%.
- **Hooks:** `damageDistrict` (3702), `G.lostStructures`, `cityStabPenalty` (4547), `recomputeCityHp` (3701), `BUILD_DEFS` ECON.
- **Size:** M.
- **Tenet:** a pure buff. The enemy's answer is the `bulwark` (an existing enemy exosphere platform that lobs nukes) and rod bombardment. A rebuilt city invites another strike.
- **One-Button:** automatic. A yard rebuilds on its own.
- **Line:** surface building, plus underground shelters via `autoBury` (existing building behaviour).

### Bill 4 — The Workforce Draft

*"The women in the munitions works did not fill in a form per shell. The state moved them in thousands."*

**Fantasy:** you see your city population shift between farms, factories and barracks, and the numbers follow.

**Annex**
- **Mechanic:** a single "mobilization %" slider, 0 to 100. It trades civilian growth for manpower. Each 10 points adds +5% manpower and removes 3% of `CITY_RECRUIT_MP`-style trickle, and the army cap grows slowly (`armyCap` 3803). At 100% your cities muster militia faster (`CITY_TROOP_EVERY` halves) but stop growing new districts if I add city growth. Reversible.
- **Hooks:** `armyCap`, `cityRecruit`, `CITY_TROOP_EVERY` (3730).
- **Size:** S to M.
- **Tenet:** a trade. It never denies a unit.
- **One-Button:** one slider.
- **Line:** untouched.

### Bill 5 — The Wireless

*"Ministry of Information, not Ministry of Pop-ups. Let the player buy a morale line and let the sky answer."*

**Fantasy:** your public broadcasts keep a whole nation fighting through the worst week.

**Annex**
- **Mechanic:** turn the dormant `EVENTS` engine on (flip `EVENTS_LIVE`, 1099) and add five to eight more events fired from real conditions (a city lost, a boss killed, superhumans fielded). Each choice is a one-time trade of IC or influence against stability cap or reserve. `stabReserve` (6339) already absorbs losses first. A Wireless Mast (proposed Comms Tower variant) raises reserve by +4 and cuts event stability penalties by 30%.
- **Hooks:** `EVENTS`, `evtReqMet`, `checkEvents` (1099), `stabReserve`, `stabRes` (3385).
- **Size:** M (mostly writing).
- **Tenet:** each choice is a trade-off. The enemy's `strikewing` and bombardment phases are the pressure that makes choosing matter.
- **One-Button:** the choice is a single click and auto-pauses (the engine already does it). A rule such as "always pick the first option" can be saved as a standing policy.
- **Line:** untouched.

### Bill 6 — Scrap Drive

*"We collected pots and pans for aluminium. The pans did not matter. The collecting did."*

**Fantasy:** the wreckage of the war feeds the next factory.

**Annex**
- **Mechanic:** destroyed enemy hulls and your own ruins (`G.ruins`, 1519) contribute a small scrap stream. A Scrap Depot (proposed ECON building) turns scrap into heavy alloy (`G.comp.alloy`, currently capped at 60) at 1 alloy per 4 scrap, with a depot-count scaling of +20% rate per depot. Ruins sitting near a depot return +30% of the building's IC cost after 30s.
- **Hooks:** `G.ruins` / `pushRuin` (1519), `G.comp`, `COMP`, the `makes` building field.
- **Size:** M.
- **Tenet:** a pure buff. The enemy's answer is bigger hulls, which drop more scrap for you and more damage on them.
- **One-Button:** automatic.
- **Line:** surface.

### Bill 7 — The Evacuation

*"Move them early and they live. Let the player decide where the people go."*

**Fantasy:** you shelter the population underground and your cities survive a bombardment that would have broken them.

**Annex**
- **Mechanic:** a standing "Evacuate" setting that moves civilians from a threatened city. While evacuated, a city's `stabilityPenalty` from district loss is halved, but its recruit trickle drops to zero (`citySieged`, 3733, already blocks recruiting). It triggers automatically when re-entry is detected (the 12s warning, which exists today) if set to AUTO. It reverses on all-clear.
- **Hooks:** `citySieged`, `cityStabPenalty`, the re-entry warning, `ambientFleeing` (1845), `UG_DEPTH`.
- **Size:** S.
- **Tenet:** pure trade-off, no denied toys.
- **One-Button:** one standing choice: OFF / AUTO.
- **Line:** uses the underground band.

## 4. Hot Take — The People's Army

*"Let every district raise its own regiment, and let the player be afraid of what that means."*

**Fantasy:** the nation arms itself. At the cost of civil order, your cities produce a standing citizen army that scales with volume.

**Annex**
- **Mechanic:** a TOTAL MOBILIZATION edict. While active, each live district musters a free `inf` militia (existing infantry unit) every 20s, bypassing the garrison ledger (`G.garrCap`). Stability cap drops by 1 per 5 active districts, up to a floor of 20 (the existing clamp, 6338). If stability hits zero, the crisis system (6643) triggers as usual, and mutineers join the enemy side for the crisis duration: the autofleet's answer is a "propaganda wave" (proposed enemy phase: an enemy variant that targets stability rather than structures).
- **Hooks:** `cityRecruit`, `spawnGarrison`, `garrCap`, `stabCap`, `crisis*`.
- **Size:** L.
- **Tenet:** gives the biggest army fantasy in the game. The risk is the crisis, which is already recoverable, not a loss.
- **One-Button:** a single edict toggle.
- **Line:** ground units move left and right only.
