# Round 2 Paper: Ministry of Megaprojects (`megaprojects`)

## 1. Floor Speech

Madam Speaker, I have counted the guns on this floor. Four councils have proposed the same supergun. Railgun brought a barrel with twelve sections. Kinetic-arms brought a barrel with ten, powered by hydrogen. Energy brought ten thousand mirrors. I brought a gun and *then* a gun the length of a continent. This is not a quarrel, it is a standing ovation, and I thank the House for stealing my idea so enthusiastically.

I praise the Honourable Autofleet and Lore. A wonder with no enemy is a lawn ornament, and their Fear Index and Fleet Resolve are the missing half of my Interdiction bill. I praise Bio for the Immortal Line, the only Titan that earns its name. Space-fleet knocked on my door with the same pulse ship and I will not make them wait in the corridor.

I savage the Council of Pacing, who would shorten my prep clock because the fleet "watches you build". A wonder takes time. Do not hurry a cathedral. I savage Railgun for *capping* its Long Gun at twelve sections. The Ministry has a word for a gun with a limit: a rifle.

Now, to the objections I expect. "Megaprojects are win buttons." They are not: they are sieges, paid for in IC upkeep, uranium, and a fleet that comes to the site. "Projects are too big for one game." The framework is 260 lines. Lift it. Build the wonders.

## 2. Positions

| Bill | Position | Reason / amendment |
|---|---|---|
| RAILGUN-2 Long Gun | **AMEND** | Delete "up to 12 sections": sections uncapped. Damage `100 x N` linear (energy is linear in L), range linear in N as well (ballistic range goes as v squared, i.e. as L). Keep their Recoil Cradle every 4 sections as a real cost. Merge into Motion 1. |
| RAILGUN-X Meridian | **AMEND** | Make it the *doctrine* tier of the same gun: buried tube networks extend barrel length of the Long Gun. Tube destruction shortens the shot, as they propose. |
| KINETIC-X Bull Gun | **AMEND** | Keep as the *early sections* (light-gas stage, useful from section 3). Their fixed ten sections must not cap. The Annihilator charge interrupt is excellent and stays. |
| SPACE-5 Orion Battleship | **AMEND** | Hull and hangar are theirs, the commissioning is a three-stage megaproject (Motion 2). Uranium is the fuel. |
| SPACE-X Kessler Doctrine | **SUPPORT** | A model tier-1 doctrine, and the first live user of the ledger. |
| ENERGY-H The Furnace | **SUPPORT** | Mass-spammed mirrors scale uncapped, a textbook site worth interdicting. |
| GROUND-X Crawler Capital | **SUPPORT** | A megaproject built in sections under a city. Needs a scoped M first slice (one mobile capital). |
| BIO-X Immortal Line | **SUPPORT** | Unlimited growth, enemy focus-fire as the cost. A superweapon with an arc. |
| AUTOFLEET-2 Fear Index | **SUPPORT** | It is the heat model for megaprojects (Motion 3). |
| LORE-2 Fleet Resolve | **SUPPORT** | The reward for defending a site: Resolve falls. |
| PACING-HOT | **AMEND** | Remove the shortened prep clock. Keep the alarm as information that feeds the Fear Index and the gambit. |
| LORE-1 Charter | **SUPPORT** | Dangerous superweapon nodes (Titan, Pylon, Dreadnought) get a dial. |
| SCIENCE-3 Containment | **AMEND** | A megaproject site is a containment site: the dang-research distance rule should reuse the same site and heat. |
| PLANET-1 / INDUSTRY-4 / COMMAND-2 rail spine | **SUPPORT** | A tube network is the Meridian's barrel, and a rail is a project requirement. |
| MARKET-6 Privatise the Sky | **OPPOSE** | A commercial orbit that only exists to be crashed sells the player's own toys for a tax. |

## 3. Ballot (10 votes)

| Bill | Votes |
|---|---|
| AUTOFLEET-2 The Fear Index | 2 |
| LORE-2 Fleet Resolve | 2 |
| ENERGY-H The Furnace | 2 |
| BIO-X The Immortal Line | 1 |
| SPACE-X The Kessler Doctrine | 1 |
| LORE-1 The Charter | 1 |
| GROUND-X The Crawler Capital | 1 |
| **Total** | **10** |

None to MEGA bills.

## 4. Motions to Co-Sponsor

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

## 5. Trades

- **MEGA-2 and MEGA-H are withdrawn as separate bills** and fold into Motion 1.
- **MEGA-4 folds into Motion 2.** The Ministry takes the build stages and leaves the hull to Space-fleet.
- **MEGA-3 folds into Motion 3.** I will shrink my wave planner to reuse their Fear Index.
- **MEGA-1 (the Ledger) stays alone**: it is the framework every other bill needs.

## 6. Constitutional Challenges

- **Article III against RAILGUN-2:** "up to 12 Barrel Sections" is a hard cap on the player's toy. Strike it.
- **Article III against PACING-HOT:** a shortened prep clock, floored at 55%, reduces the player's build-out time as a penalty for building. Its sponsor concedes a knife-edge. I rule it unconstitutional unless amended to information only.
- **Article IV.3 against MARKET-6:** a commercial orbit the player is taxed for deletes toys the standing decision protects. Needs amendment.
