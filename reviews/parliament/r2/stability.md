# STABILITY & POLITICS COUNCIL — Round 2 Paper

## 1. Floor speech

Madam Speaker. The Ministry rises to thank the House for finally discovering that people live in this war.

To the Autofleet Council: you have invented a fleet that is *afraid* (AUTOFLEET-2), that reads our weakest axis (AUTOFLEET-3) and that changes its mind (AUTOFLEET-4). Splendid. A fleet that reads us can be lied to, and the only ministry in this chamber that employs liars is mine. To the Lore Council: your Charter dial is the best idea on the order paper that is not ours, and we will say so once, quietly. To the Pacing Council: you propose to shorten the prep because the player built too well. The Ministry has seen that sentence before. It was written by a man with a clipboard, and it is a knife in the player's hand disguised as a clock.

To the Air Staff, who will say that Decrees are "a stability nerf in a party hat": a decree is a *purchase*. You are given more power, you pay in a currency that rebuilds itself, and nobody may stop you issuing it at zero. To the Strike lobby, who will say the Casualty Ledger punishes mass: it charges a logarithm, madam. Twenty thousand dead cost sixteen points of ceiling and a Veterans' Bureau halves it. Machines pay nothing. That is not a cap. That is a *choice of army*, which is the whole game.

The Ministry has read twenty-two other papers and finds that nineteen of them quietly route a cost through "stability". We are the plumbing of this parliament. Please stop pouring in without asking.

## 2. Positions

| Bill | Position | One line |
|---|---|---|
| LORE-1 Charter | **SUPPORT** | Dangerous research becomes a dial with real numbers; our Decrees and State of Emergency plug into it. |
| LORE-2 Fleet Resolve | **SUPPORT** | A fear gauge gives Maskirovka something to push on. |
| LORE-5 Wire & Chronicle | **SUPPORT** | Our headline ticker needs exactly this feed; merge, do not duplicate. |
| AUTOFLEET-2 Fear Index | **SUPPORT** | A scarier enemy answers hoarding with no cap. Needs a spoofable input (see Motion 2). |
| AUTOFLEET-3 Weakest Axis | **AMEND** | Static Campaign's "Comms/Propaganda −60%, cap −8" is fine; amend: killing the jammer drone refunds the lost cap in 20 s and the effect never stacks with Casualty Ledger above −12 total, so the bill is a bounded cost, not a spiral. |
| AUTOFLEET-4 Rethink or Despair | **SUPPORT** | A fleet that adapts to catastrophe is a scarier enemy. |
| PACING-HOT Fleet Watches You Build | **OPPOSE as written / AMEND** | Draining the prep clock *is* a build-time nerf. Amend: keep the alarm as information plus the AUTOFLEET-2 fleet-size scaling; the arrival time is untouched. |
| PSI-1 Awakening | **SUPPORT** | −0.8 / −0.4 cap per psion is linear and priced. Amend: show each line in the Ledger receipt (NEW-PLAYER-3). |
| PSI-X The Listening | **SUPPORT** | A stability-priced intel ability is exactly our idiom. |
| NEW-PLAYER-3 The Ledger | **SUPPORT** | Receipts for stability and influence fix the "no idea what each cost" problem. |
| BIO-5 Overgrowth | **SUPPORT** | A stability-positive strike is the pressure valve Reports 6–7 lacked; it is bounded by the cap, so it cannot print stability. |
| COMMAND-4 Dead Hand | **SUPPORT** | Decapitation stays a real fear; the Command Layer wave is a good fleet event. |
| COMMAND-3 Shock Brigades | **AMEND** | Merge into our Decrees (Motion 1): one stability-priced haste system, not two. |
| COMMAND-5 Urals Gambit | **AMEND** | Same: Evacuation is our decree; keep their city-move as the megaproject. |
| MARKET-3 War Bonds | **SUPPORT** | A default event that costs stability gives debt a political face. |
| PHYSICS-3 / MISSILES-X Starfish | **SUPPORT** | High bursts and taboo strikes at stab 20 are fair, visible costs. |
| KINETIC-4, MISSILES-1 | **SUPPORT** | Stability-free strike tiers are the escape hatch that makes taboo pricing honest. |
| SCIENCE-3 Containment | **AMEND** | Containment sites should *be* the place a Charter dial lives; one UI, not two. |

## 3. Ballot (10 votes)

| Bill | Votes |
|---|---|
| AUTOFLEET-2 The Fear Index | 2 |
| LORE-1 The Charter | 2 |
| NEW-PLAYER-3 The Ledger | 1 |
| AUTOFLEET-3 The Weakest Axis | 1 |
| BIO-5 Overgrowth | 1 |
| COMMAND-4 Continuity of Government | 1 |
| PSI-1 The Awakening | 1 |
| LORE-5 The Wire & The Chronicle | 1 |
| **Total** | **10** |

## 4. Motions to co-sponsor

### Motion 1 — To the Command and Lore Councils: "THE DECREES" (stability as a tempo budget)
Merges our Decrees (Hot Take), COMMAND-3 Shock Brigades, COMMAND-5 Urals Gambit's evacuation clause and LORE-1's Charter dial under one **Decrees** panel.
**Clerk's Annex.** Mechanic: three timed decrees, **Haste** (build/research/production time ×0.5, 60 s, −12% stability on issue and −0.15%/s), **Reprisal** (+25% fire rate, 45 s, −15%), **Evacuation** (re-place destroyed buildings at 50% IC with no build time, 90 s, −10% plus −3% per building). Each use inside 120 s multiplies the next cost by 1.25. Shock Brigades become the per-factory form of Haste inside the Commissariat aura. The Charter dial (Restrain/Standing/Unleash) sets the *unrest multiplier* each decree adds to `stabOffset` (Unleash ×1.8, Restrain ×0.5). Decrees are allowed at stability 0 (matching `STRIKE_STAB_FLOOR`, L6892). Hooks: `BUILD_DEFS` L3381 build timers, research tick L4692, `G.stab` writes (L6950 pattern), `dangCap` L4708, crisis L6635, strike-menu UI L7010. Size: M (~300 lines). TM1. Tenet: passes; the player gains power, paid in a recoverable resource, never capped.

### Motion 2 — To the Autofleet and Pacing Councils: "THE FLEET READS THE HOME FRONT"
Merges AUTOFLEET-2 Fear Index, AUTOFLEET-3 Weakest Axis, our Maskirovka Bureau and a rewritten PACING-HOT.
**Clerk's Annex.** Mechanic: the Fear Index `F` (AUTOFLEET-2) is computed from the same `G.enemyIntel` snapshot the fleet already reads (L5773). Maskirovka campaigns (Dummy Grid, Order of Battle) shave up to 25% off the *reported* inputs for 45 s; a second identical campaign inside 240 s gets half effect, and a third flags "propaganda detected" and the fleet swaps to blind carpet fire (a scarier-enemy answer, not a nerf). Dispersal Doctrine and Maskirovka share one node, `maskirovka` (def, pre `pointdefense`, cost 170 before ×1.6, not dangerous). PACING-HOT's prep drain is dropped; the alarm is information only. Hooks: `enemyIntel` L5773, `counterBias` L5509, `buildSupersPlan` L6493, `spawnEnemy` L5582, HUD L11133. Size: M (~350 lines). TM1. Tenet: passes; the enemy scales upward, the player's prep clock and toys are untouched.

### Motion 3 — To the Lore and New-Player Councils: "THE WIRE AS HOME FRONT"
Merges LORE-5 The Wire, NEW-PLAYER-3 The Ledger and our Information Policy Dial and news ticker.
**Clerk's Annex.** Mechanic: one feed of labelled systems headlines ("Casualty index 14 · Influence 1.8× · Flank 2 contested"), each carrying a hover receipt that lists every contribution to stability and influence (strike costs, ledger penalty, cadre/psion upkeep, city losses). The Information Policy dial (Free Press / Managed / Censored) alters the receipt: Free Press doubles swing sizes and regen and adds a 15 s earlier forecast; Censored halves hits but defers strike costs as a −2%/min bleed. Hooks: `stabRegen` L6332, influence L6360, `stratStab` L6890, `announcePhase` L5683, HUD cell L811/L11133. Size: M (~250 lines). TM1 (forecast slice of Art. IV.4); the full free-versus-command axis is TM2. Tenet: passes.

## 5. Trades

- **Withdraw** STABILITY-3 State of Emergency as a standalone bill: it folds into the Charter (LORE-1) as the "emergency" dial position, so one modal serves both.
- **Shrink** the Information Policy Dial to the Free/Managed stance only if Motion 3 is short of room; Censored is the first to go.
- **Keep** the Casualty Ledger and Maskirovka as stand-alone bills.

## 6. Constitutional challenges

- **PACING-HOT (Art. III, knife-edge).** Shortening prep in response to building is a denial of the build-out fantasy. Its own sponsor flags it. The Ministry rules it *not void* but recommends the information-only fallback (Motion 2).
- **Point of order, `dangCap` (L4708).** It lowers the stability ceiling per dangerous tech, a recoverable cost and not a ban on the tech. It passes Art. III as long as every dangerous node stays researchable at any count.
- **Point of order, CLAUDE.md stability.** Missiles is right: only `taboo` strikes cost stability (`stratStab` L6890). The reference is stale and should be corrected by the President's own hand.
- No bill is unconstitutional under Art. IV.
