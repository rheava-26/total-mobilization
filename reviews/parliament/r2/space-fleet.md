# SPACE FLEET COUNCIL — Round 2 Floor Paper
*Slug: `space-fleet`. Speaker: the Admiral of the Void.*

## 1. Floor Speech
Honourable members. For one round I stood alone in the dark. Now I count friends.

To **Missiles**: Starfish is the first honest answer to the bombarder grid anyone has tabled. You also cheerfully fry my sats, so it needs a hardening node. I will help you write one.
To **Physics**: Kessler Shield is the other half of my Kessler Doctrine, and it is better thought through. Take my debris lane and your dead-sat hazard, and we are one bill.
To **Megaprojects**: your Orion is a *ledger entry*. Mine is a *ship*. Marry them: you pour the uranium, I fly the thing.
To **Market**: a commercial constellation is the cheap mass I could never afford. Your crash window is a fine cost.

To **Air**: your Argus is a radar plane. Mine is a net in orbit. Do not make me explain the difference with a dreadnought.
To **Ground** and **Naval**: I never heard from you about the sky above your heads. That is how the bombarder grids got there.

Now the expected attacks. "Decoys are cheap, so the fleet will ignore them." Then it burns its lances on the real units, and the fleet's *learning* is a scarier enemy, which the tenet wants. "Kessler is self-harm." Yes. The *player* pulls it, and the price is the player's own fleet. It is a choice, not a cap. "Sync fire has a 6-second window of risk." That is the cost the tenet permits. I vote for the war worth fighting. Seconded by the vacuum.

## 2. Positions (bills from other councils touching space)
| Bill | Position | Reason / amendment |
|---|---|---|
| MISSILES-X Starfish | **AMEND** | Add node `radhard`: hardened sats and Orbital Strike Fighters survive the zone; unhardened lose 60% HP, not 100%. Starfish must also zero the enemy `pdBudget` so SPACE-2 volleys land. |
| PHYSICS-4 Kessler Shield | **SUPPORT** | Gives dead sats a role. Merge with my SPACE-4 lane (Motion 1). |
| PHYSICS-3 Burst Altitude | **SUPPORT** | Makes exosphere the premium altitude for warheads, which is a reason to own space. |
| ENERGY-4 Re-entry Sheath | **SUPPORT** | Hot drops at the top of the sky are exactly where Halo, Volley and Zeus finish them. |
| MEGA-4 Project Orion | **AMEND** | One Orion, not two. Megaprojects builds it in stages (pusher plate, magazine, ascent). The finished hull is my SPACE-5 unit with the 10-fighter hangar and lift 12, not a fixed 2,400 HP platform that dies into drones. It must *fly* (it is a ship, not a satellite, so Art. IV.2 holds). |
| MARKET-6 Privatise the Sky | **SUPPORT** | Cheap commercial sats are mass in orbit. The debris tax and crash window are real costs. Amend: commercial sats count against `orbCap` at half a slot, so the cap formula (Art. IV.1) is untouched. |
| RAILGUN-4 Space Gun | **SUPPORT** | Gun-launch lowers the cost of the lift my fleet is short of, with no nerf to rockets. |
| AIR-2 Argus datalink | **AMEND** | Same net as SPACE-2. Rename one, share one code path (Motion 3). |
| KINETIC-4 Fire for Effect | **SUPPORT** | Time-on-target is exactly SPACE-2's saturation trick on the ground. |
| AUTOFLEET-1/3 columns and weakest axis | **SUPPORT** | A fleet that targets a *neglected* axis makes an ignored orbit a real vulnerability. |
| AUTOFLEET-HOT (the Mirror) | **AMEND** | If the fleet fields a copy of my best unit, mirror my *space* unit too, once per match, so space has a duel. |
| PACING-HOT (the fleet watches you build) | **AMEND** | Tenet knife-edge. Rule: it may only *add* enemy pressure, never reduce player output. |
| INDUSTRY-H / SCIENCE-1 (wrecks and salvage) | **SUPPORT** | Enemy hulls killed in orbit leave wreckage in the band: SPACE-4 debris and salvage are the same tile. |
| MEGA-3 Interdiction | **SUPPORT** | A fleet that comes to kill a megaproject is an orbital target set for my fleet to defend. |

## 3. Ballot (10 votes)
| Bill | Votes |
|---|---|
| MISSILES-X Starfish | 3 |
| PHYSICS-4 Kessler Shield | 2 |
| MEGA-1 Ledger of Projects | 1 |
| RAILGUN-4 Space Gun | 1 |
| ENERGY-4 Re-entry Sheath | 1 |
| AUTOFLEET-3 The Weakest Axis | 1 |
| INDUSTRY-3 The Autofoundry | 1 |
| **Total** | **10** |

Starfish gets the three because it is the only bill that kills the grid in the wild, and *every* other council's space ambition depends on that.

## 4. Motions to co-sponsor

### Motion 1 — "DEAD SKY PROTOCOL" (to: Missiles, Physics)
**Merged mechanic.** Merge SPACE-4 (Scattergun lane), SPACE-X (Kessler Doctrine), MISSILES-X (Starfish) and PHYSICS-3/4 into one three-rung ladder: **Burst** (Starfish: EMP zone, hardened units survive), **Lane** (Scattergun: an un-interceptable pellet band), **Burn** (the Kessler Doctrine: 180s of debris belt, and your own lift offline). One shared **debris-belt object**, a stationary area (no moving satellites, Art. IV.2), created by any rung and decaying 5% per 20s. Dead sats of either side seed it.
**Clerk's Annex.** Numbers: belt damage `18 + 0.6×t`/s to hostiles, 25% friendly fire, Starfish 120 IC / 20% stab, Kessler +12% stab cost. Hooks: `STRAT` L6878, `launchStrike` L6943, `PD_SAT` L8620, `orbCap` L3812, `hurt` L4971. Size: L (~700 lines, shared object saves ~250). **TM1** (fleet reaction events TM2). Tenet: all player *options* with visible costs; hardening node `radhard` lets the player protect their fleet, and the fleet answers with +40% PD budget and faster grid re-seeding (a scarier enemy).

### Motion 2 — "ORION: THE SHIP AND THE PROJECT" (to: Megaprojects)
**Merged mechanic.** MEGA-4's three-stage construction (pusher plate, 10 uranium magazine, 40s exposed ascent) produces SPACE-5's Orion Battleship: 4,000 HP, armour 24, 4 turrets, hangar of 10 Strike Fighters, 2s pulse dash. The ship is a unit, can be lost, and can be built again at full price.
**Clerk's Annex.** Numbers: 1,400 IC, 6 rocket parts, uranium per pulse as MEGA-4 (1 per pulse, 1.4% stability). Hooks: `UNIT_DEFS` L3298 (`dreadnought` template), carrier code L3260, megaproject framework on `megaprojects-wip`, `STRAT` L6878. Size: L (~550). **TM1.** Tenet: toy with a resource cost, no cap; interdiction (MEGA-3) is the scarier enemy.

### Motion 3 — "ONE NET" (to: Air, Kinetic-Arms)
**Merged mechanic.** AIR-2 Argus, SPACE-2 Argus Net and KINETIC-4 Fire for Effect share one **Engagement Net** object: any unit with a link (AEW jet, Relay Sat, Fire Direction Centre) joins, and **SYNC FIRE** releases every linked shot with times-of-flight corrected to land within 0.2s. Saturation drains enemy `pdBudget` (L8620), and the volley bonus is +4% per linked unit.
**Clerk's Annex.** Hooks: projectile spawn, `acquireEnemyMulti` L4398, `PD_SAT` L8620. Size: M (~300 shared). **TM1.** Tenet: pure power with a 6s exposed charge as the cost.

## 5. Trades
- **Withdraw SPACE-X as a standalone bill** into Motion 1 (the Burn rung). Keep it only if Motion 1 fails.
- **Shrink SPACE-5**: drop the pulse-dash ability if MEGA-4 supplies the ascent drama. The hull and hangar stay.
- **Hold SPACE-3 (Helios) lean**: drop the +1 IC/s per Power Plant rectenna link if Market's outflow already pays for the constellation.

## 6. Constitutional challenges
- **None filed.** One warning: MISSILES-X as written kills every unhardened friendly electronic with no recourse. That drifts toward *denying* toys unless `radhard` ships with it. I make hardening a condition of my AMEND, not a challenge.
- **PACING-HOT** (Art. III): lawful only if it adds pressure and never lowers player income or output.
