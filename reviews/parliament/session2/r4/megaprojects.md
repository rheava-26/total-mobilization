# Second Reading: Ministry of Megaprojects (`megaprojects`), Economy Bloc

## 1. Speech

Madam Speaker, the President says nobody had the job of saying "too strong". Then let the Ministry say it first, about itself. A gun that needs you to aim it is a chore. A wonder that needs you to babysit it is a hobby. A wonder that takes a decade and then runs itself, until the fleet comes for it, is a civilisation.

I accept the One-Button Law with relief. My bills were always meant to be built and then left alone. I accept the Line with joy, because my wonders go *up*, *down* and *out to sea*, never sideways. And I accept Amendment I, because an uncapped gun is only honest if the enemy is allowed to grow into it.

The President asks that a megaproject change what a civilisation *is*. I have been waiting for that sentence for two sessions. A gun changes a battle. A ring changes a sky. A city that moves underground changes a people.

I will answer the Inspector-General, and I will bring a ledger.

## 2. The Re-reading

**MEGA-1, the Ledger (doctrines you fall into, megaprojects you build). AMEND.** One-Button: doctrines fire from `G.stats` thresholds with no click, and a megaproject is commissioned once and then only fed by building. Amendment: stage reqs must only be buildings or stockpiles, never "hold X for N seconds" tasks that need attendance. Line: passes, as it is a menu. Perf: `updateProjects` is one loop over under 20 entries.

**MEGA-2 and the docket's THE LONG GUN COALITION (railgun, kinetic-arms; plus Long Shot). AMEND.** One-Button: the old gun was `manual: true` (a `siegegun` pattern, L3414), which is per-shot attention. Amendment: it auto-fires at the highest-priority target in arc (bosses, then largest hull), with one standing priority choice made once (BOSS / LARGEST / NEAREST). Aiming becomes optional. Line check: the three arcs (west, zenith, east) are the Line plus the vertical, so it passes, and the gun site stands on a mountain, which is a vertical choice. *Win button?* No. Evidence from the code: damage is linear in sections N (100 x N), but a boss's hp is `1700 x t` and `t = diff x (1 + invTime/70)` grows without bound, and the gun fires one slug per 8 s along one line, while a General-difficulty wave is hundreds of bodies. A 40-section gun is a boss-killer, not a fleet-killer, and the sections are the target the fleet prioritises at 60% (MEGA-3). There is no proof here that losing becomes impossible, so Amendment I does not call for a limit. I will not restore one.

**MEGA-3 Interdiction (adopted as SITE UNDER SIEGE). KEEP.** One-Button: it is the enemy's side and needs no input. Add, per Order Paper #9, a HUD line: "SITE HEAT: the fleet sees what you are building". Line: passes. Amendment I: it is the enemy answer that licenses every uncapped wonder.

**MEGA-4 and ORION: THE SHIP AND THE PROJECT (space-fleet). AMEND.** One-Button: hangar rearm and pulse burn must be automatic. Amendment: the burn triggers by itself when the ship is attacked or retargeted, with no ability button. Line: passes (vertical). Amendment I: a 4,000 hp hull is a buff, and the enemy answer is MEGA-3 plus the bombarder grid (SPACE-1), so it is not power creep.

**MEGA-H, the world-spanning barrel. WITHDRAW as a standalone bill.** Its one live idea survives as the N >= 40 capstone of the Long Gun: the `anchor` boss and the withdrawal ending, fully automatic.

**THE BROOD PIT (bio). AMEND (already uncapped chain).** One-Button: larvae seed themselves from slots. Line: the Pit is a surface and cooling-spine structure, a vertical column. Amendment I: Ancient Titans draw enemy focus-fire, so it is not an unanswered buff.

**THE LONG SHOT (game-feel). KEEP.** Cinematics only, skippable, first shot per 20 s.

**THE MERIDIAN SPINE (railgun). KEEP.** The tube graph runs underground, so it is on Subterranean's layer. One-Button: fixed azimuth, auto-fires with the same priority choice. Line: passes.

Counts: KEEP 3, AMEND 4, WITHDRAW 1.

## 3. The Order Paper

**#1** is answered above.

**#5 (LEAD): megaprojects that change what the civilisation is.**

First, a framework amendment, **MEGA-5: THE CIVIC BLOCK.** Every megaproject gets one extra field, `civ`: `{ society, stabCap, look, fleetRead }`. *Society* is a new or existing `society(kind)` repaint, *stabCap* a change to the stability cap (a cost or a gain), *look* a visible skyline or map change, and *fleetRead* a line the fleet reacts to (a Fear Index delta plus a named enemy adaptation). Unlike a tech node, a megaproject's effect is a *place* on the map.

#### Clerk's Annex: MEGA-5
- **Mechanic and numbers.** `civ.society` fires once at completion via `society()` (L3549). `civ.stabCap` writes to `g.mult.stab` (the real stability cap field). `civ.look` registers a draw-pass id. `civ.fleetRead` adds `F += x` (AUTOFLEET-2) and a named adaptation. No new UI beyond a toast and the Fear readout.
- **Code.** `PFX` extended with `PFX.civ(...)`, `updateProjects` on `origin/megaprojects-wip`, `society()` L3549, `cityStabPenalty` L4547.
- **Size:** S (on top of MEGA-1). **TM1.** **Research:** none; it lives with each project's own node.
- **Amendment I:** every civ block names the enemy's answer, so it cannot be power creep. **One-Button:** automatic at completion. **Line:** n/a.

**MEGA-6: THE ORBITAL RING (the sky becomes a place).**
*Pitch.* A ring above the strip. Section by section the exosphere rises and the space band opens to *building*, not just visiting.
*Science.* Birch's orbital ring (1982), Lofstrom's launch loop, and space-elevator tether studies. A rotor spun above orbital speed can hold a stationary structure up by pure momentum, with tethers to the ground. **SCI-FI:** the material strength at planetary scale, assumed.

#### Clerk's Annex: MEGA-6
- **Mechanic.** One Ring Anchor per ~1,000 px of strip, built as sections with no limit. Each section: +4 orbital lift (`orbCap`, L3812), +2% to space-band height (`SPACE_Y`, L1541), and unlocks Space Station construction in that section's footprint. Cost 700 IC, 80 MP, 24 s per section, 8 IC/s upkeep (momentum losses).
- **Civic block.** Society: **Spacefaring** (a new `society` kind: a dark sky with a lit ring arc drawn across the top). `stabCap` +0.5 per section (a people with a future). `fleetRead`: Fear +0.3 per section; adaptation: the fleet retargets the Ring Anchors with bombarders, so a ring without a picket is a liability.
- **Code.** `orbCap` L3812, `SPACE_Y` L1541 and the band constants, `society()` L3549, `spawnSuper` L5269, the lifted framework.
- **Size:** L. **TM1.** **Research:** new node `orbitalring` (space, tier 4, cost ~500, pre `spaceage`, `orbitalcmd`, combo `colossus`), not dangerous.
- **Amendment I:** a buff with an enemy answer (anchors become the bombarder priority), and it does not make the army worthless. **One-Button:** build anchors, nothing else. **Line:** vertical.

**MEGA-7: THE ONE CITY (an arcology, the nation becomes a single organism).**
*Pitch.* Fuse your cities into one tower. Fewer targets, one heart.
*Science.* Soleri's arcology, Masdar and Kowloon density studies. **SCI-FI:** a megastructure housing millions on a footprint of 600 px.

#### Clerk's Annex: MEGA-7
- **Mechanic.** Arcology Core plus uncapped Tier Sections (500 IC each). Each section adds +6 MP, +5 army-cap equivalent housing, and +1 city-defence tier up to the existing `bDef` ladder (reuse L3549 area, then continue as `bDef 3` self-defence). Cost: the capital becomes the *only* city that counts for `decapitation`, so a lost core is a flat -30 stability, not per structure.
- **Civic block.** Society: **Arcology**, a dense lit skyline. `stabCap` +4 (a nation with one heart). `fleetRead`: the fleet declares Decapitation an escalation (INTENT, L5119): the first skyfall is doubled on the core. That is the scarier enemy.
- **Code.** `buildCity` (L3628/3694), `damageDistrict` L3702, `cityStabPenalty` L4547, `INTENT` L5119.
- **Size:** L. **TM1.** **Research:** new node `arcology` (auto, tier 3, cost ~400, pre `robotics`, `biotech`).
- **Amendment I:** a power gain paired with a named, larger enemy threat. **One-Button:** automatic. **Line:** vertical.

**MEGA-8: THE AEGIS OF EARTH (a shield you can see).**
*Pitch.* The deflector shield, sectioned along the strip. The dome appears on the sky when the last section lights.
*Science.* Plasma windows and magnetic deflection of charged particles, real but small. **SCI-FI:** a dome the width of the strip. Internal logic: the fleet fears an industrialising Earth, so it must prove itself against a sky that fights back.

#### Clerk's Annex: MEGA-8
- **Mechanic.** Shield Pylons every ~600 px, uncapped. Complete coverage over a stretch grants it a dome of `shieldHp = 1500 x pylons-in-stretch`, regenerating 3%/s outside combat, drawn as a visible aurora layer. Upkeep 5 IC/s per pylon.
- **Civic block.** Society: **Fortress** (a bright blue-green rim sky). `stabCap` +2. `fleetRead`: the fleet adapts to **Breachers** (shield-penetrating bulwarks), and Fear +0.4.
- **Code.** `shieldgen` L3412 (`shieldR`, `shieldHp`), `society()`, enemy spawn tables in `spawnEnemy` L5581.
- **Size:** M. **TM1.** **Research:** `shieldtech` plus `colossus` (combo).
- **Amendment I:** buff plus Breacher answer. **One-Button:** build pylons. **Line:** horizontal coverage, not depth, so it passes.

**#6 (coordination with Subterranean Works and Undersea Works).**

**MEGA-9: THE CONTINENTAL REDOUBT (with SUB-3).** Adopt Subterranean's Redoubt as a section-built megaproject on the Trunk.

#### Clerk's Annex: MEGA-9
- **Mechanic.** Sections along the buried Trunk, each adding +1 shelter tier (Subterranean's `deephab` rule, `cityStabPenalty` cut by 12% per tier to a 40% floor). At 8 sections the capital counts as buried against decapitation. Cost 600 IC per section. No section limit.
- **Civic block.** Society: **Chthonic** (SUB-3's repaint). `stabCap` -3 (the sun deficit) offset by the Lamps. `fleetRead`: Burrowers and the Deep Penetrator (SUB-1) appear.
- **Code.** `UG_DEPTH` L3843, `ugAllowed` L3862, `cityStabPenalty` L4547, `railhub` L3416.
- **Size:** M (on SUB-3). **TM1.** **Research:** `deepmining` + `biotech`.
- **Amendment I:** the enemy has a named answer. **One-Button:** automatic. **Line:** vertical.

**MEGA-10: THE BLUE BELT (with UNDERSEA-2/4/5).** A section-built coastal chain of OTEC plants, hydrophone arrays and cable stations on the sea floor.

#### Clerk's Annex: MEGA-10
- **Mechanic.** Each section is a seabed cluster (one OTEC plant, one Hydrophone Array, one Cable Station, all from UNDERSEA-2/4). Per section: +14 IC, +6 s landing warning, +0.8 stability cap. No limit. Cost 900 IC and 90 MP per section, build 22 s.
- **Civic block.** Society: **Abyssal** (UNDERSEA-5). `fleetRead`: Fear +0.25 per section and Bathyscaphe Drones spawn above 5 seabed buildings (UNDERSEA-1).
- **Code.** `canPlace` L3866 (`seabed` flag), `mkBuilding` L3670, `society()` L3549.
- **Size:** M. **TM1.** **Research:** `deepworks` + `fusion`.
- **Amendment I:** uncapped economy balanced by the Fear Index and Bathyscaphes. **One-Button:** build and forget. **Line:** vertical.

## 4. Blatantly overpowered today

Nothing in my portfolio. The five superweapons (Colossus, Titan, Nanofortress, Pylon, Dreadnought) do not appear among the losses of the match reports, and the reports record no case where one made losing impossible. I will not propose a nerf without proof.

## 5. Struck self-limits

The Railgun Directorate struck the Recoil Cradle wear penalty as "a nerf in a costume". My coalition endorsed that. I do not want it restored: the Long Gun's limiter is `t` growth and a fleet that prioritises it, not a wear tax. If the Inspector shows a section-spam exploit, I would restore it as a flat IC cost and not as a hp penalty.
