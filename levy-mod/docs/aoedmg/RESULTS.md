# Area attacks and the Chalice of the Blood God (aoedmg)

Question: why did Sgt. Alice stand inside the Avatar of Emptiness's collapsing star and show only "5" and "(-435)"?

## Cause (read from the decompiled mods, then reproduced)
- The "(-435)" text is Calamity's Chalice of the Blood God (`CalamityPlayerHitHurt.cs`, `$"({-bleedoutToApply})"`, colour 230,40,100). Not Wrath of the Gods, not Levy.
- The Chalice cuts every hit above 5 damage down to 5 and puts the rest into a bleedout buffer. `ChaliceOfTheBloodGod.HandleBleedout` then takes about 0.83 percent of the buffer from the wearer's life every tick (about 40 percent a second). A player is hurt for the full amount, a few seconds late.
- A levy took the cut (5) and filled the buffer on its gear dummy, but nothing ever moved the buffer's life onto the levy's body: the only place the drain ran was the regeneration pass, which discards any life the dummy loses. The buffer drained into nothing, so a levy in the Chalice was immune to hits of any size.
- Wrath of the Gods itself does nothing special to damage. Its projectiles are ordinary hostile shots with big numbers (Avatar `AvatarOfEmptinessAIValues.json`, Nameless Deity `NamelessDeityAIValues.json`); the only hit hooks are cosmetic. The designer's "tiny `Projectile.damage`" idea is not it.
- Unrelated finding: the Nameless Deity super cosmic beam gives a hit player 6 ticks of immunity (`ImmuneTimeOverride`) instead of 40. A levy keeps the normal window, so it is hit about 5 times less often than a player (lab: player 47.8k in 5 s, levy 10.1k). Not fixed here (narrow).
- Player-only punishments (Absolute Zero outburst leaves a player 5 to 50 life, `Player.Hurt` on the boss's target) never touch a levy. Design difference, not changed.

## Lab (Master + Death, defense 40, endurance 25 percent, a levy and a real Player both in the Chalice, 10 s standing in the shot; 27 attacks, `[AoeDmg]` world check)
Life taken in 10 s (hits plus drain), a few rows (all 27 are in the check's OBSERVED lines):

| attack (damage in Master+Death) | player | levy BEFORE | levy NOW |
|---|---|---|---|
| Avatar dying star burst, the collapsing sun (550) | 39,894 | 100 | 40,130 |
| Avatar stolen planetoid (750) | 55,319 | 100 | 55,506 |
| Avatar blood torrent (720) | 53,091 | 100 | 53,418 |
| ND quasar (700) | 51,602 | 100 | 50,622 |
| ND super cosmic beam (336) | 24,749 | 100 | 24,300 |
| ND sword constellation (104; the mod's own SwordConstellationDamage property reads a slash-timing value, so this number may not be the real shot damage) | 7,247 | 100 | 7,263 |

Per hit: the body took 5 every hit before and still does; the deferred part is about 2,400 per hit at these numbers. Levy over player is 0.98 to 1.03 in all 27 attacks (worst gap 3 percent). Per second: 1 s, 3 s and 10 s columns are in the log (the dying star: player 1,553 / 8,019 / 39,894; levy now 1,507 / 8,093 / 40,130; before 15 / 35 / 100).

Video numbers: a levy sized to take about 400 per hit (the deferred part measured 392 to 399, the video shows 429 and 435), 400 life, standing in the shot: before, alive after 30 s with a buffer that grew to 6,608; now, dies at 1.3 s.

Army: 12 levies in the Chalice, 400 life each, defense 40, inside the dying star's shot (550) for 30 s: before, 12 alive after 30 s, 302 hits taken; now, all 12 dead by the first one-second sample (12 hits).

Drain rate: buffer 1,908 after a hit, 699.2 after 120 ticks, Calamity's constant gives 699.2; the body lost 1,209 for a buffer drop of 1,209.3 (regeneration pass running beside it).

## Fix
`SoldierNPC.Bleedout_Update` runs Calamity's own `HandleBleedout` on the dummy once a tick and takes what it took from the body (a body that bleeds out dies of it; `ChaliceBleed.cs`). The regeneration pass parks the buffer so it is drained once. Setting `SoldiersBleedOut` (default on, a parity fix like the projectile and contact rules); `LEVY_BLEEDOUT=0` for lab A/B.
The drain runs from the body's per-tick damage step, so a levy that step skips (dormant, far from every player) does not bleed while it is skipped. Not measured.
