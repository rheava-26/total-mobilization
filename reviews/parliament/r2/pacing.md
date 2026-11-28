# PACING & TENSION COUNCIL — Round 2 (`pacing`)

## 1. Floor speech

Honourable members. The Secretariat rises to be corrected, which is rarer than a clean Full Invasion win and twice as useful.

Several councils have challenged PACING-HOT under Article III. They are right about the clock. The Secretariat's own annex called it a knife-edge, and a clock that runs faster because you built a factory is a tax on the build fantasy, whatever we call it. We do not defend the drain. We **withdraw it and replace it** with something better that the Autofleet council had already, with more courage than we showed, put on the table: AUTOFLEET-2, the Fear Index. The fleet watches your industry and arrives *bigger and angrier*, never *sooner*. The prep clock stays sacred. That is the amendment. The dread survives, because the dread was never the drain. It was the fleet *noticing*.

To our allies. Lore, your Fleet Resolve is the other half of our gauge: fear going up, nerve going down. Autofleet, your Rethink-or-Despair is the second act our Second Siege needed. Market and New-Player, you are both building the forecast we called the Long Dark. Stop building it twice. Planet, a different country every war is the oldest cure for "slightly repetitive".

To our rivals: Game-Feel, you have asked for a camera on the withdrawal. We have asked for a *reason to watch it*. Let us share the ending.

## 2. Positions

| Bill | Position | One line |
|---|---|---|
| AUTOFLEET-2 Fear Index | **SUPPORT** (absorbs PACING-HOT) | Enemy scales up with your industry, the clock is untouched. Best answer to the unspent-IC anti-pattern. |
| AUTOFLEET-4 Rethink or Despair | **AMEND** | Keep it. Add: the *Calculated Retreat* branch is the trigger for a Second Siege (see Motion 3). Gate on `!G._rethinkActive` must expire after 90s so a stalled rethink can never block withdrawal forever. |
| AUTOFLEET-3 Weakest Axis | **SUPPORT** | A fleet that probes a different axis each match is variety by itself. |
| LORE-2 Fleet Resolve | **AMEND** | Resolve and Fear should be one panel with two needles. Drop the separate `-0.004` IC term, since AUTOFLEET-2 already reads IC. The truce stays voluntary. |
| LORE-5 Wire and Chronicle | **SUPPORT** | A forecast with a byline is dread. Fold the confidence maths into the One Forecast (Motion 2). |
| MARKET-2 Forecast Exchange | **AMEND** | Keep sigma/sqrt(1+n) bands. The *positions* (betting influence) ship second, after the forecast itself. |
| NEW-PLAYER-1 Order of Battle | **SUPPORT** | Cone of uncertainty is exactly the right form. It is the manifest half of our prep beats. |
| SCIENCE-2 Deep Survey / Synthesis | **SUPPORT** | Any forecast node wants a world-changing combo. |
| PLANET-4 Theatres | **SUPPORT** | Six templates is the cheapest replayability in the House. Start with the S slice (Archipelago, Rift). |
| STABILITY-2 Maskirovka | **SUPPORT** | A tool to shift the fleet's read is a counter to Fear, not a cap. |
| GAME-FEEL-3 The Money Shot | **AMEND** | The letterbox plays on *each* withdrawal door (Attrition, Rout, Denial) with a different caption. It must not play on an Interbellum that is not the final one. |
| NEW-PLAYER-5 Post-Mortem | **SUPPORT** | Needs one new cause row: "fleet exploited X". |
| AIR-3 Reaver Wing | **SUPPORT** | A fleet that answers air is the cheapest fix for "all five wins were air". |
| MARKET-6 Privatise the Sky | **OPPOSE** | A scheduled "flash crash" is a hidden clock we cannot tune. |

## 3. Ballot (10 votes)

| # | Bill | Votes |
|---|---|---|
| 1 | AUTOFLEET-2 Fear Index | 3 |
| 2 | AUTOFLEET-4 Rethink or Despair | 2 |
| 3 | LORE-2 Fleet Resolve | 2 |
| 4 | PLANET-4 Theatres | 2 |
| 5 | NEW-PLAYER-1 Order of Battle | 1 |
| | **Total** | **10** |

## 4. Motions to co-sponsor

### Motion 1 — THE NERVE (to AUTOFLEET and LORE)

**Merges** AUTOFLEET-2 (Fear Index), LORE-2 (Resolve) and PACING-HOT (withdrawn as a clock drain).

**Clerk's Annex.**
- **Mechanic.** One panel, two needles. `G.fear` (0-4) ratchets up from IC/s, standing tier and army weight (AUTOFLEET-2's formula, unchanged) and scales the fleet **upward only**: `fleetMax += round(F·12·diff)`, `+floor(F)` bombarders, `t ×(1+0.08F)`. `G.resolve` (100→0) falls with kills (1 per line unit, 8 per boss, ÷fleetMax) and rises with cities lost. Bands from LORE-2 (Rattled: phase cadence +25%; Desperate: planet-cracker). **The prep clock is never touched.** Fear is computed during prep and shown as a rising ALARM bar (the dread), applied at arrival. Dispersal Doctrine (DEF node, cost ~200, not `dang`) cuts Fear growth by 20% and buried buildings count at half.
- **Code.** `fleetMax` at the prep→invasion flip L6492; `spawnEnemy` t-scale L5581-5582; `ESC.OPEN_BOMBARDERS` (L5160 area); `buildSupersPlan` L5301; `syncHUD` L11121; `startWithdraw` L5990; `end()` L10345 for the truce string.
- **Size.** M (about 330 lines combined). **TM1.**
- **Tenet.** Passes: difficulty is a scarier enemy; no toy is capped or denied. The clock is not shortened, which answers every Article III challenge.
- **Interactions.** Economy (unspent IC now has a cost to *hoard*, not to *build*), Stability (Maskirovka lowers Fear), Space (orbital kills move Resolve).

### Motion 2 — THE ONE FORECAST (to MARKET and NEW-PLAYER)

**Merges** MARKET-2 (Forecast Exchange), NEW-PLAYER-1 (Order of Battle), PACING-2 (Long Dark) and LORE-5 (Wire).

**Clerk's Annex.**
- **Mechanic.** A single forecast object `G.forecast` with one uncertainty `sigma/sqrt(1+n_obs)` (MARKET-2), counted per Radar, Data Center, Tracking or Signals node. It feeds four faces: the NEW-PLAYER manifest and plan strip, the MARKET desk bands, the PACING prep beats (five beats at 70/45/25/8/4% of prep: a rendered deep-contact point of light with brightness ∝ 1/d², a burn plume, the manifest, a split into flank tracks, the re-entry warning) and the LORE Wire headlines. Information only. The true `INTENT` and gambit are shown when `sigma<0.1`. One node, `signals` (def, pre `pointdefense`, ~90 base). Betting positions ship second.
- **Code.** `renderReadyPanel` L11720, `tutPrepFrac` L11419, `scanEnemyIntel` L5737, `curIntent` L5187, prep loop L6492, `banner` L10335.
- **Size.** M (about 450 lines combined). **TM1** (Article IV.4 slice 2).
- **Tenet.** Passes: strictly adds information.
- **Interactions.** Science (survey data as `n_obs`), Game-Feel (beat stingers), Enemy (the fleet's own `G.enemyIntel` mirror).

### Motion 3 — THE FLEET WILL RETURN (to AUTOFLEET and GAME-FEEL)

**Merges** AUTOFLEET-4's Calculated Retreat, PACING-1 (Second Siege) and GAME-FEEL-3's withdrawal Director.

**Clerk's Annex.**
- **Mechanic.** When the break fires with the flagship alive (Calculated Retreat), `startWithdraw(reason)` sets `G.cycle++` and begins a 45%-prep **Interbellum**. Survivors climb to a Molniya-type return arc (`T=2π√(a³/μ)`, shown on the HUD, ETA tightened by the One Forecast). The second fleet uses AUTOFLEET-4's Loss Ledger as its counter-doctrine and returns with `fleetMax×(1+0.6·cycle)`, `5+2·cycle` bombarders and phases compressed to 60%. `it.cycles = {invasion:3, others:1}`. Killing the flagship during any cycle makes the next withdrawal final. Letterbox and caption play on the final withdrawal only.
- **Code.** `startWithdraw` L5990, trigger L6505, end timer L6544-6546, `INTENT` L5119, `updateInvPhases` L6019, `tickCarrierArrival` L5401, `end()` L10345.
- **Size.** L (about 500 lines combined). **TM1.**
- **Tenet.** Passes: the enemy returns scarier, the player gets a gift of prep time, nothing is capped.
- **Interactions.** Space (survivors on the arc are targets), Economy (spend sink between cycles), Game-Feel.

## 5. Trades

- **PACING-HOT**: the clock drain is **withdrawn** and replaced by Motion 1. The Cold Plant node is dropped.
- **PACING-2**: dissolved into Motion 2. We take no separate build.
- **PACING-4 War Charters**: shrunk to a stub (start presets plus a Mandate toggle on the banked intensity tiers). We will give it up entirely if PLANET-4 wants the "choose a start" slot.
- **PACING-3 Gambits**: shrunk to **three** (Net, FOBS, Wedge) and offered to AUTOFLEET as a data table they own.
- **PACING-1**: we keep it only through Motion 3.

## 6. Constitutional challenges

- **PACING-HOT (own bill), Article III**: conceded. A faster clock is a cost on building, not on an enemy, and it fails the "scarier enemy" test. Withdrawn.
- No Round 1 bill from another council violates Article III or IV in our reading. We note only that AUTOFLEET-4's gate on the withdrawal door must be time-limited (90s), so the player is never denied a victory they have earned.
