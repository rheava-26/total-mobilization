# AIR COUNCIL — Round 2 Paper (slug: `air`)

## 1. Floor Speech

Honourable members, the Air Staff rises. Yes, again. Yes, I heard the Ground Staff mutter "parked in a hangar." Field Marshal, my Falcons are parked because they are *rearming*, which is more than your infantry can say for themselves, 232 of 251 of whom died as confetti.

I want to thank the **Autofleet Council**, who read my AIR-3 and AIR-5 and independently proposed runway cratering and a hunter-killer wing. Great minds. I accept the compliment and the plagiarism. I thank **Missiles** for proportional navigation, because a Falcon missile that cannot hit a jinking target is a firework. I thank **Kinetic Arms**, who say I will call flak a nerf. I will not. Flak punishes the *enemy's* swarm, and my Falcons carry IFF and a sense of humour. I thank **Space** for stealing my Argus. It is a fine name, and I shall be merging our claims.

I shall be honest with the chamber: the Pacing Council wants the fleet to arrive earlier when I build more. Fine. Tell them to bring it. But if anyone proposes that my airbases hold fewer aircraft, I will be waiting by the Article III door with a very large wrench.

Make the enemy hunt my wing, and I will fly it brilliantly. Air Staff, out.

## 2. Positions

| Bill | Position | Reason |
|---|---|---|
| AUTOFLEET-1 Uncounted Columns | **AMEND** | Right idea; it duplicates my AIR-3/AIR-5. Amendment: (a) `hkwing` target priority is aircraft in flight, then tankers and AEW aircraft, then airbases; (b) the cratering raid only targets bases with at least 8 parked aircraft; (c) a Hardened Shelter and Rapid Runway Repair (my AIR-5 player half) ship in the same bill, so every raid has a counter; (d) a raid's `launch disable` is 12 s, ramping to 25 s only after phase 4. |
| AUTOFLEET-HOT The Mirror | **SUPPORT** | Falcon echoes: my own wing as the enemy. Echoes need `fuel` so they bingo and retreat, which makes the dogfight readable. |
| MISSILES-4 Real Guidance | **AMEND** | Support PN and seekers. Add: player aircraft get a **countermeasure dispenser** (4 flare/chaff charges, refilled on any pad), the mirror of the enemy flares it adds. |
| MISSILES-1 Cruise Wing | **SUPPORT** | Air-launched cruise missiles from Lancers give bombers a standoff role. |
| SPACE-2 Cooperative Engagement Net | **AMEND** | Aircraft in the Argus net join the SYNC FIRE volley. Merge with my AIR-2 (Motion 1). |
| SPACE-1 Chaff/Decoy Clouds | **SUPPORT** | Same physics as the player flare dispenser; share the decoy-weight code. |
| KINETIC-1 Programmable Flak | **SUPPORT** | The enemy's drone swarms bleed, my wing is untouched. Amend: friendly-fire filter (`friendly:true`) is mandatory. |
| INDUSTRY-1 Ammunition Trains | **AMEND** | Supplied aircraft rearm 30% faster at a depot-linked base. The unsupplied still rearm at the current rate. A bonus, never a starvation lock. |
| INDUSTRY-3 Autofoundry / COMMAND-1 Five-Year Plan | **SUPPORT** | A standing order "keep 240 Falcons" is how a wing replaces losses. Amend: orders respect `airCap()` and name a home base. |
| INDUSTRY-H Salvage the Fleet | **SUPPORT** | Sanitiser air drones are a fine new enemy; they hunt wrecks, so air gets a patrol job. |
| NAVAL-5 Ekranoplan Wing | **SUPPORT** | No airbase slot, a real new role. Air does not own the low band over water. |
| PSI-4 The Hush / SPACE-4 Retrograde Shotgun | **AMEND** (one clause) | Any debris cloud or jamming field must state its effect on aircraft in the band; otherwise the Air Council will be surprised. |
| PACING-HOT | **Ruling: tolerate** | See Section 6. |

## 3. Ballot (10 votes)

| Bill | Votes |
|---|---|
| AUTOFLEET-1 (as amended) | 3 |
| MISSILES-4 Real Guidance (as amended) | 2 |
| SPACE-2 Cooperative Engagement Net | 1 |
| KINETIC-1 Programmable Flak | 1 |
| INDUSTRY-1 Ammunition Trains | 1 |
| AUTOFLEET-HOT The Mirror | 1 |
| INDUSTRY-3 Autofoundry | 1 |
| **Total** | **10** |

## 4. Motions to Co-Sponsor

### MOTION A — "THE ARGUS NET" (Air + Space-Fleet)
Merge AIR-2 and SPACE-2 into one cueing network. **Mechanic:** a single node `datalink` (branch `energy`, pre `directedenergy`, tier 2, not dangerous; combo `orbitalcmd` for the satellite relay). The net has two node types sharing one code path: **Argus aircraft** (hp 190, STRATO ceiling, ic 380) and **Relay Sat "Argus"** (SPACE-2, 340 IC). Any friendly aircraft, AA gun or space unit within 1,100 px of a node is in the net. In-net units (a) share one designated target; (b) gain launch-on-remote, +20% effective missile range; (c) may join **SYNC FIRE**: a 6 s hold, then one same-tick volley whose travel times are corrected to land within 0.2 s, which saturates the enemy `pdBudget` (L8554/L8620). A net that loses all nodes falls back to solo targeting.
**Clerk's Annex.** *Numbers:* net radius 1,100 px; hold 6 s; range +20%; volley bonus +4% per member (no cap other than range). *Hooks:* `acquireIncoming()` L4465, `canIntercept()` L4464, `pdBudget`/`PD_SAT` L8554/L8620, aura pattern `helicmd` L3239 and `radar` L3406, air brain `updUnits()` L7809, `jammed()` L4970. *Size:* M-L. *Tag:* TM1. *Tenet:* a buff, and the cost is a real vulnerability (a visible node, a 6 s hold, jammable). Passes. *Science:* Cooperative Engagement Capability, Link-16, launch-on-remote (AIM-120).

### MOTION B — "THE FLEET HUNTS THE WING" (Air + Autofleet)
One coalition bill from AUTOFLEET-1, AIR-3 and AIR-5. **Mechanic:** AUTOFLEET-1's `airfieldRaid`, `hkwing` and chaffer stay as written, with my amendments, plus the `weasel` anti-radiation flight (AIR-3: it homes on emitters, and a 12 s EMCON order drops the lock at the cost of the aura). The player half ships in the same bill: **Hardened Shelter** (S building: +3 hangar slots, docked aircraft take 85% less damage and launch through a crater) and a **runway-repair** ability on Overwatch (`helicmd`), which cuts `runwayT` decay ×4 within 200 px. I withdraw my own Durandal bomber and my own Reaver spawn rules (see Section 5).
**Clerk's Annex.** *Hooks:* `scanEnemyIntel` L5737, `counterBias` L5513, `waveAirKind` L5524, `spawnEnemy` L5581, `rearmAircraft()` L4522 (base-pick loop checks `runwayT`), `BUILD_DEFS` airbase L3392, `AIR_SLOTS` L3818, `tickFleetStance` L5999. *Size:* M-L (about 350 lines). *Tag:* TM1. *Tenet:* the enemy gets scarier and the player's counters are built, never denied. Passes. *Science:* Durandal/JP233 runway denial, Operation Focus, AGM-88 HARM, hardened aircraft shelters, EMCON.

### MOTION C — "SORTIE SUPPLY" (Air + Industry)
Merge AIR-1 (tankers, forward arming strips) with INDUSTRY-1 (ammunition trains). **Mechanic:** Fuel is a separate stock from ammo. `tanker` "Pelican" refuels 12 fuel/s, ≤4 aircraft at a time, `fuelStock` 400; the `farp` strip and any depot-linked base rearm ammo 30% faster when `G.comp.munitions > 0`. Munition Works feeds both ground guns and air sorties, so IC flows into the air war, which gives the 212k unspent IC a real sink. Node: `logistics` (INDUSTRY-1, `pre: ['automation']`) with `airmobility` as its child (branch `age`, `pre: ['fusion','logistics']`).
**Clerk's Annex.** *Hooks:* `rearmAircraft()` L4522, `AIR_FUEL_BURN` L4511, `COMP` L3180 (cap change for `munitions`), `makes` loop L6306. *Size:* M. *Tag:* TM1. *Tenet:* supply is a bonus; nothing is starved or capped. Passes.

## 5. Trades

- **Withdraw** AIR-3's Reaver spawn rules and AIR-5's Durandal bomber in favour of Autofleet's `hkwing` and raid (Motion B). I keep the Weasel flight and the player-side Hardened Shelter, which Autofleet lacks.
- **Shrink** AIR-4 (Shrike EW jet) to **S**: reuse the Argus net's `pdBudget` drain and drop the separate noise cone, if Missiles' `arm`/home-on-jam seekers pass.
- **Shrink** AIR-2 to a stand-alone Argus aircraft model only if Motion A passes; its satellite variant belongs to Space.
- **Hold** AIR-X (Pilots Are People) as a Round 3 stretch goal; it is the one bill I will not trade for.

## 6. Constitutional Challenges

- **None filed.** I examined every bill that touches air and found no cap, nerf or denial of the player's toys.
- **Ruling requested on PACING-HOT (knife-edge, as its sponsor says).** A shorter prep is a **cost** within Article III's clause "a real cost (upkeep, logistics)", not a cap, because the player keeps every unit and building. I would tolerate it **only** if the arrival clamp (55% of nominal prep) stays and buried industry counts at half. If the clock is ever tied to **unit count**, Article III is violated, since it would punish the air power fantasy directly. The information-only fallback is the safe harbour.
- **Caution on SPACE-4 and MISSILES-X (Starfish):** a high-altitude burst or debris cloud that kills the player's aircraft with no counter would amount to denial. Each needs a stated counter (shelters, Shrike hardening, a node in the net). I do not file the challenge yet; I reserve it for Round 3.
