# Rogue ambush cycle: results (2026-11-19)

Eve's direction: rogue levies play as ambushers. Fall back, charge stealth standing still, run in, throw the strike and a couple of plain throws, fall back again; time Rage and Adrenaline to the strike.

## The cycle (SoldierNPC.Ambush.cs, Common/Weapons/Ambush.cs)

A Calamity rogue levy with a stealth bar runs the cycle when its weapon wants the strike on this target: a `Wait` weapon always, a `Situational` one against a boss or a crowd, a `SmallTargets` one against anything but a boss, and the Doomsday Device. `Spam` weapons (their plain throws beat the strike) keep throwing at their own rate. Ballistic Poison Bomb is on the measured exception list.

- **Fall back** to a charge spot 14 tiles from a boss's hitbox (9 from a lesser enemy, plus a tenth of the body's size, at most 20), on the levy's own side unless that side is full of hostile shots, on standable ground, inside the order's chase clamp and any Calamity arena box. Already standing in that band with no shot coming: charge at once.
- **Charge** standing still (the dodge still runs). The target closing to 70% of the danger radius, drifting 6 tiles past the spot, or a hit sends the levy to a new spot.
- **Strike** when the bar is full (a short run in starts early enough that the bar is full on arrival). In reach with a line: stand and throw the strike, then two plain throws (`LEVY_AMBUSH_PLAIN`), then fall back. Out of reach: two seconds walking straight in, then the usual approach search. With Rage or Adrenaline burning the levy stays in the window and the stealth policy decides.
- **Fire gate**: falling back and charging hold every throw. The Doomsday Device's plain full-charge grenades keep the bar, so its own rule throws in every phase and the cycle only moves the body.
- **Skipped** (the levy just throws): target within four plain throws of death, or the commander below half life with a boss after her (below 30% with any hostile within 15 tiles).
- Switches: `LEVY_AMBUSH=0` off, `1` policy (default), `2` every rogue weapon (lab). `LEVY_AMBUSH_LOG=1` logs phase telemetry every 10 s.

## Rage and Adrenaline

Existing: `SoldierNPC.Rippers.cs` latches Rage (any fight) and Adrenaline (a boss within 70 tiles) when the meter is full; a rogue holds a full meter up to 10 s for a full stealth bar. An older path (`TickCalamityMeters`, SetBonusKey.cs) also adds the mode buff to the levy's player; the latch overrides it each tick, so it changes nothing. Added: an ambusher's activation waits until it is in the strike window, in reach, with the bar full (the same 10 s cap).

## Rogue army sweep (Statigel, 20 s, levy vs stand-in player, final code)

dps / strikes per minute; "laser" = the dummy fires an aimed laser every 1.5 s (struck per minute in brackets).

| weapon | scene | off | on | on/off | off, laser | on, laser | on/off | wait player (laser: struck/min) |
|---|---|---|---|---|---|---|---|---|
| Shinobi Blade | solo | 156 / 0 | 296 / 24 | 1.90 | 160 / 0 (3) | 221 / 18 (0) | 1.38 | 359 / 27 (39) |
| Shinobi Blade | crowd | 150 / 0 | 294 / 24 | 1.96 | 172 / 6 (0) | 221 / 18 (0) | 1.28 | 353 / 27 |
| Doomsday Device | solo | 938 / 18 | 1100 / 18 | 1.17 | 947 / 18 (0) | 1081 / 18 (0) | 1.14 | 1108 / 18 |
| Doomsday Device | crowd | 2408 / 18 | 3012 / 18 | 1.25 | 2479 / 18 (0) | 2837 / 18 (0) | 1.14 | 3556 / 18 |
| Ballistic Poison Bomb (excepted) | solo / crowd | 753 / 751 | 735 / 737 | 0.98 | 700 / 700 | 656 / 725 | 0.94 / 1.04 | 1244 / 1202 |
| Cobalt Kunai (Spam) | solo / crowd | 295 / 454 | 295 / 437 | 1.00 / 0.96 | 297 / 449 | 296 / 461 | 1.00 / 1.03 | 467 / 417 |
| Stellar Knife (Spam) | solo / crowd | 493 / 484 | 485 / 490 | 0.98 / 1.01 | 480 / 482 | 487 / 485 | 1.01 | 468 / 386 |
| The Syringe (Spam) | solo / crowd | 1304 / 1433 | 1079 / 1398 | 0.83 / 0.98 | 1135 / 1535 | 1189 / 1263 | 1.05 / 0.82 | 723 / 676 |
| Regulus Riot (Spam) | solo / crowd | 976 / 1003 | 981 / 1003 | 1.01 / 1.00 | 988 / 1011 | 988 / 1001 | 1.00 | 828 / 791 |

The Spam rows run identical code on and off: their spread (0.82-1.05) is the sweep's run-to-run noise. With the cycle forced on every weapon (`LEVY_AMBUSH=2`), Cobalt Kunai (0.67-1.16), Stellar Knife (0.43-0.70), The Syringe (0.52-0.62) and Regulus Riot (0.65-0.87) lose: they keep throwing. Ballistic Poison Bomb won the sweep (x1.25-1.40) but lost against Ravager (below). Levies are almost never hit by the laser on or off (they dodge); the standing stand-in takes 39 hits a minute.

## Ravager (Master+Death, arena a3, Eve's 11-16 army with 8 rogues in Umbraphile, 8 seeds)

Rogue weapon dps (damage / fight seconds, mean ± s.e.):

| arm | wins | levy deaths (mean / median) | Doomsday x2 | BPB x2 | Leviathan Teeth | Deep Wounder |
|---|---|---|---|---|---|---|
| off | 8/8 | 7.6 / 6.5 | 2423 ± 90 | 176 ± 28 | 109 ± 21 | 85 ± 14 |
| on, first cut (charge 20-30 tiles out) | 7/8 | 13.0 / 6.5 | 1931 ± 178 | 102 ± 23 | 111 ± 25 | 99 ± 25 |
| on, charge at 14 tiles | 8/8 | 8.6 / 6.5 | 2449 ± 106 | 116 ± 9 | 144 ± 23 | 111 ± 26 |
| on, final (BPB excepted) | 8/8 | 9.2 / 9.0 | 2302 ± 99 | 184 ± 34 | 115 ± 12 | 76 ± 17 |

Final arm telemetry: 151 strikes thrown in the window over 8 fights, 55 full cycles, mean charge distance 15.2 tiles, 1,082 spot re-picks (Ravager jumps; the levy follows it). In the real fight the cycle is neutral within the noise for the ambushing weapons. Army deaths are dominated by Ravager's mass slams; ranged-class deaths (gunners and rogues together) were 9 off against 11 and 19 on, not resolved without per-levy attribution.

Files: `ravager_rogue.tsv` (spec, raw fight lines), `eve_rogue_army.json` (army).
