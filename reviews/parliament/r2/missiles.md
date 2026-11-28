# MISSILES & STRATEGIC STRIKES COUNCIL — Round 2 Floor Paper

## 1. Floor speech
Mr Speaker. Twenty-three papers, and I counted the word "launch" in nineteen of them. Good. The Rocket Forces have never been so popular.

To the Physics council: you wrote Starfish and forgot to name it. PHYSICS-3 is my Hot Take with a lab coat on, and I embrace it. To Command: your Dead Hand is the first honest thing anyone has said about the last hour of a war. To Naval: you gave my Trident a hull, a depth band and a reason to exist. Salutations. To Space-Fleet: your Scattergun and our burst make one kill chain, and you may have the credit if we keep the bang.

To Air: you will say missiles are only what a Falcon carries. Comrade, a Falcon is a missile with a pilot's ego. To Market: you want to privatise the sky, and the first thing you will privatise is the debris. Spare us.

Now the attacks I expect. "Missiles are the stability spiral." Dead wrong. Only taboo strikes cost stability, and my stockpile and burst bills make a *decision* out of each. "Cruise is a nerf of PD." The enemy keeps every lance; it merely cannot see over a hill. "Salvos are a spend-20,000-IC button." No, the Depot is a place the IC *lives*, with upkeep, fallout and an enemy who hunts it.

The countdown does not care who you are. It only cares that you are ready.

## 2. Positions (other councils' bills touching my portfolio)

| Bill | Position | Reason / exact amendment |
|---|---|---|
| PHYSICS-3 Burst Altitude | **AMEND** | Support. Fold in MISSILES-X: add a `starfish` strike to `STRAT` (aim above `SPACE_Y`, taboo, stab 20, cd 30 s) that sets a 25 s EMP zone with `pdBudget = 0` on electronic enemies, with `radhard` research cancelling your own blackout (their `hemp`+`shieldtech` combo). One node, not two: keep `hemp`. |
| PHYSICS-4 Kessler Shield | **SUPPORT** | A real mutual hazard; Starfish and Scattergun both feed it. Nothing in it caps the player. |
| SPACE-4 Retrograde Shotgun | **SUPPORT** | Un-interceptable area sweep is the natural partner to a burst that zeroes PD. |
| SPACE-X Kessler Doctrine | **AMEND** | Keep it a choice. Add: an active Starfish zone counts toward the 20-satellite unlock, so a burst can tip you into the doctrine. |
| SPACE-2 Cooperative Engagement Net | **AMEND** | Extend SYNC FIRE to *strategic* rounds: silos/TELs in the net launch with computed delays so impacts coincide (see Motion 3). |
| COMMAND-4 Dead Hand | **SUPPORT** | Auto-launch in catastrophe is the right home for a stability-free strike. Amend: Dead Hand draws from the Warhead Stockpile (MISSILES-5) rather than a free strike, so the IC sink is honest. |
| INDUSTRY-1 Ammunition Trains | **AMEND** | Add warheads: the Munition Works' `munitions` stock also feeds conventional warheads (cruise/ballistic), one IC pool, not two. |
| NAVAL-1 Silent Service | **SUPPORT** | Trident/Tomahawk boats are my dispersed launchers done properly. Drop my Boomer; theirs wins. |
| NAVAL-5 Ekranoplan | **SUPPORT** | A sea-skimming missile truck; pairs with my radar-horizon rule for low flyers. |
| KINETIC-4 Fire for Effect | **SUPPORT** | TOT for guns; Motion 3 extends it to missiles. Not a strategic strike; no stability. |
| AIR-4 Shrike EW Jet | **SUPPORT** | Drains `pdBudget` 3x: this opens the door for every salvo I propose. |
| AIR-3 Reaver/Weasel | **AMEND** | The Weasel (anti-radiation hunter) must be countered by my `arm` seeker packages (MISSILES-4), not by a cap on radar. |
| MARKET-6 Privatise the Sky | **OPPOSE** | Auto-launched commercial sats with a "flash crash" is a pure IC sink with no missile fantasy; the debris hazard duplicates PHYSICS-4. |
| RAILGUN-3 Hypervelocity Munitions | **SUPPORT** | Guided rounds share my proportional-navigation seeker code. |

## 3. Ballot (10 votes)

| Bill | Votes |
|---|---|
| COMMAND-4 Dead Hand | 2 |
| PHYSICS-3 Burst Altitude | 2 |
| NAVAL-1 Silent Service | 2 |
| SPACE-2 Cooperative Engagement Net | 1 |
| AIR-4 Shrike EW Jet | 1 |
| KINETIC-4 Fire for Effect | 1 |
| INDUSTRY-1 Ammunition Trains | 1 |
| **Total** | **10** |

## 4. Motions to co-sponsor

### Motion 1 — "DARK SKY": High-Altitude Burst and Retrograde Debris
**Addressed to: physics, space-fleet.** Merges PHYSICS-3 (burst altitude), MISSILES-X (Starfish), SPACE-4 (Scattergun) and the Kessler overlap.
**Clerk's Annex.**
- **Mechanic.** One strike, `starfish` (IC 120, stab 20 taboo, cd 30 s), aimable only above `SPACE_Y`. 25 s EMP zone, radius 1,400 px: enemy electronics lose PD/weapons; your own `isMechanical` units and unhardened satellites in the zone also go dark. Zone leaves a debris record (PHYSICS-4 `G.debris`) that the Scattergun then sweeps, so the sequence is blind, then shoot, then clear.
- **Hooks.** `STRAT` L6878; `launchStrike` L6943; `strikeAimPoint` L6908; `detonateNuke` L7565; `G.blackout` (`detonateEmpBurst` L5864); `PD_SAT` L8529 `pdBudget`; `isMechanical` L4541.
- **Research.** One node `hemp` (energy, tier 2, `pre: directedenergy`, `bReq: uranenrich`, `dang`, sc 8). Combos: `shieldtech` (hardening; your own cost vanishes), `retrograde` (Scattergun).
- **Build size.** M (≈260 lines). **TM1.**
- **Tenet check.** New toy; the cost is self-inflicted attrition and stability, not a cap.

### Motion 2 — "THE DISPERSAL": Launchers under the sea and on the road
**Addressed to: naval.** Merges MISSILES-2 (TEL, Arsenal Ship, Boomer) with NAVAL-1 (Trident, Tomahawk boat) and NAVAL-5 (Ekranoplan).
**Clerk's Annex.**
- **Mechanic.** Naval owns the hulls; Missiles owns the magazine. Each launcher (`TEL`, `Trident`, `Tomahawk`, `Arsenal Ship`) has `strat: true` and a magazine of N rounds (TEL 4, Trident 8, Arsenal Ship 16). Reload from a Munition Works/Depot in range. Firing from a hidden hull reveals it for 3 s (NAVAL-1 rule). Strikes drawn from a launcher carry the same STRAT costs.
- **Hooks.** `UNIT_DEFS` L3184; `armSilo` L7660; `tickAutoStrikes` L7708; `spawnBuster` L7485 (enemy target priority); `acquireEnemy` L4374 hidden flag.
- **Research.** `sonar` -> `deepsea` (their nodes); TEL under `fusion`.
- **Build size.** L (≈600), split: naval 400, missiles 200. **TM1.**
- **Tenet check.** More launchers, no cap. Cost is magazine logistics.

### Motion 3 — "TIME ON TARGET": The salvo net
**Addressed to: space-fleet, kinetic-arms.** Merges SPACE-2 (CEC SYNC FIRE), KINETIC-4 (Fire Direction Centre) and MISSILES-1 (cruise ripple).
**Clerk's Annex.**
- **Mechanic.** One Fire Direction building/Relay Sat gives a single "TOT" command that computes launch delays so every linked gun, space unit, silo and cruise round impacts in the same 0.3 s window. The enemy's `pdBudget` is spent on the first arrivals and the rest hit raw. Cooldown 8 s; no stability (conventional rounds only).
- **Hooks.** `PD_SAT` L8529; `strikeEta` L6902 (already gives flight time per type; the delay is `maxETA - ETA`); `launchTargeting` stagger queue; `acquireEnemyMulti` L4398; `STRAT` L6878.
- **Research.** `fdc` (kinetic) + `cec` (space); partner `terrainnav` for the cruise leg.
- **Build size.** M (≈300). **TM1.**
- **Tenet check.** Pure power, with the 6 s prep window as its real exposure.

## 5. Trades
- **Withdraw MISSILES-X** as a separate bill in favour of Motion 1 (PHYSICS-3 carries it).
- **Withdraw the Boomer and Arsenal Ship** from MISSILES-2 to NAVAL-1; keep only the TEL.
- **Shrink MISSILES-4** to proportional navigation only (S); the seeker packages move to a later round and to Air's EW lane.
- Keep MISSILES-1 (cruise) and MISSILES-5 (stockpile) intact; they are the IC sink the President asked for.

## 6. Constitutional challenges
- **None filed.** On the point of order concerning my portfolio: CLAUDE.md is stale. Only `taboo` strikes cost stability (`stratStab` L6890).
- **Watch note, not a challenge:** SPACE-X must stay an opt-in doctrine. If the "every sat destroyed" clause were forced on the player, it would violate Article III.
