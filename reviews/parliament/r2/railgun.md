# RAILGUN COUNCIL — Round 2 Floor Paper
*Slug: `railgun`. Line numbers are `title.html` as cited in the Round 1 papers.*

## 1. Floor Speech

Honourable members. The Directorate came to this House with one gun and found three rival guns on the order paper. Megaprojects drew a Long Gun, Kinetic-Arms drew a Bull Gun, and we drew a Long Gun and then a gun the size of a continent. Ministers, **we agree.** Three councils asked for the same barrel. Nobody in this House has ever been so thoroughly vindicated.

I salute **Megaprojects**, whose MEGA-2 has the better track and slew than our own draft. I salute **Energy**, who have understood that a laser is a power station with a lens and a railgun is a power station with a hammer. I salute **Planet** and **Industry**, who laid the rail under the continent. We will happily fire down it.

To **Air**: you sold this House a fuel tank with opinions. To the **Kinetic-Arms** Council: a light-gas gun is a respectable shell. It is not a barrel. Attach it to ours.

To the objectors who will say a 40-section gun is "too much": there is no such thing. The tenet says yes. The enemy gets a scarier flagship, the gun gets a bigger bill, and the player gets the sky. **Seconded, by me, loudly.**

## 2. Positions

| Bill | Position | One line |
|---|---|---|
| ENERGY-1 Thermal Load | **SUPPORT** | Heated targets take more kinetic damage, which gives every rail and rod a reason to love a laser wing. Rails are `killBy` kinetic (L6680), so they already qualify. |
| ENERGY-2 Power Bill | **AMEND** | Merge its Capacitor Bank with RAILGUN-1. One building, one power pool. Rail weapons draw from the pool as emitters do. Replace the 35% throttle floor with IC billing (see section 6). |
| PHYSICS-2 Thermal Loading | **SUPPORT** | Same heat mechanic as ENERGY-1. Merge them in the heat coalition. Rails never take heat themselves. |
| KINETIC-2 Sabot and Shell | **AMEND** | Make it the one ammo-selector system for all kinetic guns. RAILGUN-3's guided and sabot rounds become rail entries in that selector. Airburst stays with KINETIC-1. |
| KINETIC-1 Programmable Airburst | **SUPPORT** | I withdraw RAILGUN-3's airburst mode in its favour. The rail gets it as a selector entry. |
| KINETIC-3 Spin-Up and barrel heat | **AMEND** | Rails and rods are excluded from `spool`. They use capacitor charge and bore wear, not barrel heat, so the two meters never fight. |
| KINETIC-X Bull Gun | **AMEND** | Fold into the Long Gun as the section-0 **light-gas bore** ammo option (cheap, early, slower). Same barrel, two shells. One gun, not two. |
| MEGA-2 Long Gun | **SUPPORT** | Better slew and upkeep design than my RAILGUN-2. I yield the chassis. See Motion 1. |
| MEGA-H Barrel Is the World | **AMEND** | The barrel runs along the Spine tunnel (Motion 3). The anchor boss appears at N >= 40 as drafted. |
| MEGA-3 Interdiction | **SUPPORT** | The fleet hunting the Long Gun and its tunnels is the scarier-enemy half of the tenet bargain. |
| MEGA-1 Ledger of Projects | **SUPPORT** | A project ledger is the home of the Long Gun and the Meridian shot. |
| PLANET-1 Spine | **SUPPORT** | Buried and barrage-immune hubs are the substrate for the Meridian Accelerator. |
| INDUSTRY-4 Freight Rail | **AMEND** | Its "Orbital Resupply" combo (no lift cost from a rail-linked Space Center) duplicates my Launch Rail. Keep it, and let the Launch Rail count as a rail-linked Space Center for free lift. |
| COMMAND-2 Metro-2 | **SUPPORT** | A Molodets rail launcher and our tunnel gun share a tunnel graph. Build the graph once. |
| SPACE-2 Cooperative Engagement | **SUPPORT** | Time-on-target salvos let a Long Gun shot arrive with a satellite barrage. |
| SPACE-4 / SPACE-X Kessler | **AMEND** | Our rods and Launch Rail make debris. Amendment: rod impacts and debris stay as visual and projectile effects, never a cap on orbital launches. |
| SPACE-5 / MEGA-4 Orion | **SUPPORT** (mild) | A pusher plate ship shot into orbit by a Launch Rail is silly and I love it. |

## 3. Ballot (10 votes)

| Bill | Votes | Reason |
|---|---|---|
| MEGA-2 The Long Gun | 3 | The designer's own seed, with the best-specified mechanic in the House. |
| ENERGY-1 Thermal Load | 2 | Real combined arms, and every kinetic gun in the game gets better. |
| MEGA-3 Interdiction | 2 | Gives megaprojects an enemy that answers back. |
| PLANET-1 The Spine | 1 | Fixes the underground-rail seed, and hubs survive barrage. |
| INDUSTRY-4 Freight Rail | 1 | Dissolves the lift bottleneck that sank report 7's space fleet. |
| KINETIC-2 Sabot and Shell | 1 | Gives every gun in the game a real ammo choice. |

## 4. Motions to Co-sponsor

### Motion 1 — "THE LONG GUN COALITION" (to: Megaprojects, Kinetic-Arms)
**Merged mechanic.** MEGA-2's chassis (section chain, curved track slew, upkeep) with my muzzle scaling and KINETIC-X's light-gas bore as a cheaper early ammo.
- Per section: +120 damage and +400 px range (linear, from MEGA-2). Light-gas bore ammo works from section 3 at 60% damage. Rail slug unlocks at section 6. Particle slug is the `particlebeam` era upgrade.
- Slew 1.5 s per section. Upkeep 6 IC/s per section idle and 40 IC per section per shot.
- The Recoil Cradle penalty from my RAILGUN-2 is struck. Nothing penalises a missing cradle.
- At N >= 40, MEGA-H's anchor boss gate applies.

**Clerk's Annex.**
- **Code hooks.** `BUILD_DEFS` L3381 (models: `siegegun` L3414, `massrail` L3455), `canPlace` L3866 and `placeBuilding` L3884, `fire()` L4855 rail branch L4879, `weaponCeil` L4490, `PD_UNINTERCEPTABLE` L8519, `RESEARCH` L3502.
- **Research.** One node `megastructures` (branch `space`, tier 4, ~450 x 1.6). Prerequisites `spaceage` and `colossus`. Combo `particlebeam` for the particle slug, and `supergun` ammo is a sub-unlock.
- **Size.** L (~650 lines). **TM1.** Needs the `megaprojects-wip` framework lifted, not merged.
- **Tenet check.** Passes. No section cap. The cost is upkeep and slew, which are logistics.
- **Interactions.** MEGA-3 interdiction, SPACE-2 (shot timing), ENERGY-1 (heated target takes more).

### Motion 2 — "THE POWER BANK" (to: Energy, Physics)
**Merged mechanic.** One **Capacitor Bank** building serving both beams and rails. Energy emitters draw from the pool as ENERGY-2 describes. Rail weapons within 420 px **overdrive** from the pool (x1.5 damage, x1.25 rate), with +1 bore wear per overdriven shot.
- Pool 2000 MJ, refilled at idle. A rail overdrive shot spends 20 MJ. A shortfall for beams **bills IC** (see section 6) and rails simply fire at baseline.
- **Rail Foundry** relines worn bores (180 IC, 1 alloy per reline, 6 s). A burned-out bore falls back to baseline, never below.
- Superconductor Facility halves draw and wear.

**Clerk's Annex.**
- **Code hooks.** Upkeep accumulator L6313-6323, building weapon loop L5764 and the aura hook near L8244, `BUILD_DEFS` L3381 and `lasertower` L3439, `superconductor` L3459.
- **Research.** Existing `directedenergy` (Bank) and a new `pulsedpower` node (branch `energy`, ~150 x 1.6) for overdrive and the Foundry.
- **Size.** M (~350 lines). **TM1.**
- **Tenet check.** Passes. Overdrive is an addition, wear never lowers baseline stats, and a shortfall costs IC rather than throttling.
- **Interactions.** Industry (alloy for relining), Physics (heat and power vocabulary).

### Motion 3 — "THE MERIDIAN SPINE" (to: Planet, Industry)
**Merged mechanic.** PLANET-1's Spine (Rail Hubs auto-linked underground) is also RAILGUN-X's barrel. A Spine chain with at least 8 Mass-Driver segments connected becomes the Meridian Accelerator: damage = 3 x tube length px / 10, one shot per 60 s, `pierce:true`, `ceil:0`. Cutting a tube section shortens the barrel. Freight and components flow along the same tube, and Spine upkeep is PLANET-1's 1.5 IC/s per 1,000 px.

**Clerk's Annex.**
- **Code hooks.** `railhub` L3416, `railHubs` L7812, `massrail` stack scan L8441, `UG_DEPTH` L3843, `fire()` pierce L4879.
- **Research.** PLANET-1's `deeprail` node (branch `auto`, ~220) plus my `Evacuated Tube Transit` as the combo (`spaceage`, `directedenergy`, `deeprail`, `dang` sc 6).
- **Size.** L (~700 lines on a shared tube graph). **TM1.**
- **Tenet check.** Passes. The cost is a vulnerable tunnel network, which is a logistics cost and an enemy opportunity.
- **Interactions.** COMMAND-2 (Molodets), INDUSTRY-4 (freight net), MEGA-3 (interdiction).

## 5. Trades

- **RAILGUN-2 is withdrawn** in favour of MEGA-2 and Motion 1. I keep only the muzzle scaling.
- **RAILGUN-1 shrinks into Motion 2.** Capacitor Bank is now ENERGY-2's building. I keep overdrive, wear and the Foundry.
- **RAILGUN-3 loses airburst** to KINETIC-1 and joins KINETIC-2's selector.
- **RAILGUN-X is reduced** to the Meridian Spine, the Planet and Industry rail graph.
- **RAILGUN-4 (Launch Rail)** stays, with INDUSTRY-4's amendment.

## 6. Constitutional Challenges

- **ENERGY-2, Article III.** The "35% floor" throttle on emitters when the power pool is dry is a denial of the player's toys. The cost is real but the mechanism is a slowdown cap. **Remedy:** emitters keep full rate, and the shortfall is billed as IC at 0.4 IC/s per MW. The throttle becomes optional, not default.
- **MEGA-3 / Article III (watch).** Interdiction is constitutional only if the fleet response scales with enemy strength and never forces demolition of the player's megaprojects.
