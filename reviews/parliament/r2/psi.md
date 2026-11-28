# PSIONIC CONCLAVE (`psi`) — Round 2 Paper

## 1. Floor Speech

*(No one saw the Conclave stand. The Speaker notes that the microphone was already warm.)*

We thank Energy. You wrote a thermometer and did not know you were describing us. Your Thermal Load is the first honest thing anyone has said about a Shapeshifter: a swarm is a heat problem wearing a coat. We thank Autofleet for the Fear Index and the Weakest Axis, because a fleet that is afraid should notice the one thing in the room it cannot count. We thank Bio for Blastema. It proves regeneration can be a squad's trait and not a planetary cheat code.

Now the rivals. Stability, you wish to double the Cadre upkeep under "Censored". You would hand the Conclave a bill and call it policy. We will pay a bill. We will not pay it in the dark. Science, you want to put our research in a lab and multiply our unrest by distance. We have always been a long way from the city. Market, you have noticed that we can see tomorrow. Yes. We were hoping you would not.

To those who say we are weakening ourselves: no. We are making ourselves *scarce*. A king is not weaker for being one. Scarce, terrible, and hunted by a fleet that finally has a reason to hunt. That is a better game, and it is still a yes.

*(The temperature drops. The Speaker adjusts his collar.)*

## 2. Positions

| Bill | Stance | One line |
|---|---|---|
| ENERGY-1 Thermal Load | **SUPPORT** | One heat field for enemies. Our PSI-2 becomes the *player-side* half, sharing the same `heat` field and band vocabulary. |
| PHYSICS-2 Thermal Loading | **SUPPORT** | Same overlap. Merge into one heat spec with medium-based cooling. |
| PHYSICS-1 / ENERGY-4 (re-entry heating) | **SUPPORT** | Kills psi-trooper "orbital reach" argument further: a hot re-entry is a lit target. No conflict. |
| BIO-2 Blastema Protocol | **AMEND** | Add: Shapeshifters (nanite-bodied) regrow only while `u.heat < 60`. Blastema supersedes the global `G.allRegen` (L7866) *as the bio Council already proposes*, and Conclave takes the heat gate. |
| AUTOFLEET-2 Fear Index | **SUPPORT** | Add a psi term: `G.psiN` raises the index, because a swarm of individuals is the thing the fleet cannot count. |
| AUTOFLEET-3 Weakest Axis | **SUPPORT** | The Anomaly (PSI-4) is the psi axis event. Make it one of the axes, not a parallel system. |
| AUTOFLEET-4 Rethink or Despair | **SUPPORT** | Its shapeshifter "rotating silhouette poisons the Exemplar pick" (autofleet L173) is exactly the right counter-fiction. |
| LORE-1 The Charter | **SUPPORT** | Dangerous research as a standing dial. Superhumans and Telekinesis become Charters with visible terms. Replaces the silent `dangCap`. |
| LORE-2 Fleet Resolve | **SUPPORT** | The ladder is where the PSI-X rare ending lives. |
| LORE-5 The Wire | **AMEND** | Merge with MARKET-2 and PSI-X into one forecast surface. See Motion 3. |
| MARKET-2 Forecast Exchange | **AMEND** | Its "Precognition Desk" unlocks via `psistudies`. Move the desk to Foresight (PSI-X node) and keep Exchange for money. Avoid two oracles. |
| STABILITY-1 Casualty Ledger / NEW-PLAYER-3 Ledger | **SUPPORT** | Superhuman upkeep is currently a tiny badge. Itemise it as a signed line. |
| STABILITY-2 Maskirovka (Mirror Earth with psionics) | **AMEND** | Mirror Earth should require a standing Relay lattice (3+ Relays), so it is built, not researched. Otherwise support. |
| STABILITY-4 Information Policy Dial | **AMEND** | "Censored doubles Superhuman upkeep" must be a visible line item and must scale with *Cadres you own*, not apply as a flat doubling to the floor. See challenge. |
| SCIENCE-3 Containment | **AMEND** | Cost multiplier is fine, but the Lab must *also* grant a reward: dang research inside containment gives its `sc` back at 50% on completion. Cost with a payoff, not a bare tax. |
| SCIENCE-2 Synthesis Doctrines | **SUPPORT** | World-changing combos. `shapeshifter` and `psionics + nanotech` become doctrines. |
| COMMAND-4 Continuity of Government (Dead Hand) | **SUPPORT** | Psi societies resist decapitation (nobody to decapitate). Gives PSI-1 a second reason to exist. |
| COMMAND-1 / INDUSTRY-3 Autofoundry | **SUPPORT** | PSI-1's Awakening queue should sit inside the same standing-orders system. |
| MEGA-1 Ledger of Projects | **SUPPORT** | A Lumen Perch fits as a built-on-purpose project. |
| SPACE-5 / MEGA-4 Orion | **SUPPORT** | Uranium needs verbs. Lumen and Orion share the fuel cell. Merge the fuel economy. |

## 3. Ballot (exactly 10 votes)

| # | Bill | Votes | Why |
|---|---|---|---|
| 1 | ENERGY-1 Thermal Load | 2 | Combined arms from one field. Fixes the Shapeshifter problem for free. |
| 2 | AUTOFLEET-3 The Weakest Axis | 2 | The fleet reacting to the player is the missing half of every doctrine. |
| 3 | LORE-1 The Charter | 1 | Replaces the silent `dangCap` with a decision. |
| 4 | BIO-2 Blastema Protocol | 1 | Regeneration as a squad trait, not a global flag. |
| 5 | NEW-PLAYER-3 The Ledger | 1 | Hidden upkeep is the oldest complaint. |
| 6 | COMMAND-1 The Five-Year Plan | 1 | Answers the 212k idle IC anti-pattern with standing orders. |
| 7 | PACING-3 Gambits | 1 | A fleet that arrives with a plan is a scarier enemy. |
| 8 | MEGA-1 Ledger of Projects | 1 | Framework for everything built on purpose. |
| | **Total** | **10** | None to PSI. Max 2 to any bill. |

## 4. Motions to Co-sponsor

### MOTION A — "THE HEAT BOOK" (addressed to: energy, physics)
**Merged mechanic.** One heat vocabulary for both sides. ENERGY-1's `t.heat` on enemies; PHYSICS-2's medium-based cooling; PSI-2's `u.heat` on friendly nanite-bodied units (Shapeshifters). Same bands (Warm, Glowing, Molten), same renderer tint, same `heatBonus()` function.
#### Clerk's Annex
- **Numbers.** Enemy bands per ENERGY-1. Friendly Shapeshifter: kinetic adds `0.02`/HP, explosive `0.05`/HP, energy `0.35`/HP. Regen off above 60. Slump above 90 (speed and damage x0.5, +40% damage taken). Vent `-6/s` idle, `-2/s` in combat, +50% when dispersed.
- **Code hooks.** `hurt(t,dmg,ix,iy)` L4971 and the armour term L4985. `beamHit` L4964, `particleHit` L4965. Regen at L7866 (replace global `allRegen` with a per-unit gate). `UNIT_DEFS.shifter` L3297. Art tint in the cached-art overlay pass.
- **Build size.** M (~300 lines, shared).
- **TM1.**
- **Tenet check.** Passes. Pure addition of a vulnerability the *enemy* can exploit. No player toy capped.

### MOTION B — "THE ANOMALY ENTRY" (addressed to: autofleet, lore)
**Merged mechanic.** Add a *psi axis* to AUTOFLEET-3's weakest-axis list and a psi term to AUTOFLEET-2's Fear Index. When `G.psiN` crosses 8, 24, 64, the fleet spends a psychology event on a counter: Hush-Hound pickets, then a Quiet-class dampener boss. LORE-2's Resolve ladder gets a "the fleet is afraid of what it cannot count" rung. Mind-Forge gets counterplay (orbital laser lattice absorbs 25% per node in the column).
#### Clerk's Annex
- **Numbers.** Hush field radius 240, halves psi fire rate and range. Hush-Hound 60 HP. Quiet 1,100 HP, radius 600, blanks Relay auras. Boss kill +10 stability as usual.
- **Code hooks.** `spawnEnemy` L5581, `spawnSuper` L5269, `INTENT` L5119, `EVT1`/`EVENTS_LIVE` L1078-1099, `STRAT.psilance` L6886, `firePsiLance` via L6969.
- **Build size.** L (~500 lines), shared across three councils.
- **TM1** with TM2 hooks into the psychology layer.
- **Tenet check.** Passes. The answer to a strong toy is a scarier enemy.

### MOTION C — "THE LISTENING" (addressed to: market, science)
**Merged mechanic.** One forecast surface. MARKET-2's Forecast Exchange sets the *price* of precision; SCIENCE-2/Deep Survey supplies the *ground truth*; PSI-X's Listening Chamber is the *active, costly* peek: 90 s of full wave and boss reveal for 18 stability cap, with a `listenDebt` the fleet reads back. Forecasts sharpen with all three.
#### Clerk's Annex
- **Numbers.** Chamber 600 IC / 80 MP. Listen: -18 cap for 90 s. Debt +1 per use; at 3 the Anomaly escalates one tier. Exchange reduces cost by 1 point per 2 positions held.
- **Code hooks.** `INTENT` L5119, HUD forecast panel, `psionicrelay` def pattern L3444, `announcePhase` L5683.
- **Build size.** M (~400 lines, shared).
- **TM1** (intel), TM2 (the "we stop if you stop" ending).
- **Tenet check.** Passes. Information and a stability price, no cap.

## 5. Trades

- **Shrink PSI-2** to a *tag* on Motion A. Conclave drops its own heat code and uses the Energy field.
- **Withdraw The Quiet boss** from PSI-4 if Autofleet wants to own roster design. Keep Hush-Hound and the psi term.
- **Shrink PSI-X** to the chamber and debt counter if Market and Science own the forecast UI.
- **Keep PSI-1 and PSI-3 intact.** They are the re-canon spine and the one beam to orbit.
- **Ruling requested on the pool question.** We stand by throughput scarcity over a dedicated pool.

## 6. Constitutional Challenges

- **STABILITY-4 "Censored" (Article III).** Doubling the Superhuman cap upkeep stacks onto the existing `-3 * G.superN` (L6331) and floors the cap at 20 with no counterplay beyond not owning cadres. That is a denial by arithmetic. It must be a visible, offsettable line, not a doubling.
- **Self-disclosure.** PSI-1's linear `-0.8` per Psi-Trooper cap cost is also a clamp risk at hundreds of units. We argue it is a *cost*, not a cap, since the floor is a degraded state under v3.26, but invite the Speaker to rule.
