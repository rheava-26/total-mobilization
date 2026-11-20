# Movement 2: grounded by default, hover only on hover gear, Mind Link parity

Eve (11-19, Calamity, post-Providence): levies hover all the time, even on wings that cannot hover, and burn their wing time; they do not seem to dodge; under the Mind Link hooks and ropes do not work, the body is forced to fly, and many weapons do not fire.

## Causes found

Hovering had three sources. The boss dance lifted every levy with wings or rocket boots to a standing hover 5 to 9 tiles over the floor against any ground boss (`DanceLift`). The altitude intent climbed to the target's level whenever a target was 5 tiles above and in reach, even when the gun could already hit it from the floor (`UpdateAltitudeIntent`). And the aerial controller held any altitude with a proportional hover at 0.4 of a tick of flight per tick, whatever the wings: nothing in the code knew that only some wings hover (vanilla's down + jump list, and Calamity's Hadarian Wings and Tired Tail through `WingStats.HasDownHoverStats`). A slowed descent after a climb was also free.

Mind Link: the drive called `Player.WingMovement` every tick in the air whether jump was held or not, so every jump on wings turned into a flight; it also added gravity during flight, gave every wing down + jump hover, and never ran rocket boots, the glide or the hover freeze the way `Player.Update` does. The grapple key asked the AI hook planner for a plan toward the cursor, which refuses any cast it cannot land from. There was no up key and no rope code at all. On the attack side the levy's own alt-fire policy chose the right-click on her left button, and her right button fired nothing.

## Fixes (targeted, inside the existing systems; `LEVY_GROUNDED=0` restores the old air rules for A/B)

- `MovementAbilities.CanHover`: vanilla's hover wing list or `HasDownHoverStats`. Without it the aerial controller climbs while below the altitude (a full tick of flight a tick) and lets go above it: the body climbs, falls through the height and climbs again, as a player's wings do. Hover wings hold the height at half a tick a tick (vanilla's cost). A slowed descent after a climb costs half a tick a tick.
- No boss-dance lift. A gun that can hit its target from the floor (in reach, firing line) does not climb; one that must climb goes only to the lowest height its shot reaches from.
- Mind Link: wings fly only on a held jump in the air after the jump itself (vanilla's flag), no gravity while flying, glide and featherfall, rocket boots after the wings, the hover freeze only on hover wings. The grapple key throws the real hook at the cursor (`QuickGrapple`), latched it runs `Player.GrappleMovement` with her keys, a jump lets go. Up on a rope takes hold and climbs, down slides, nothing held hangs, jump or a sideways key leaves. Her left button fires the primary only, her right button the right-click only.
- Lab: airborne share, wing time spent, hover ticks (with and without hover gear), airborne time and wing time by cause, dodge counters, in the lab kit's results.

## Measured (Eve's army, 34 levies, Master + Death, arena a3, 6 seeds a cell; `specs/movement2_eve.tsv`, run r3)

| | Ravager old | Ravager new | Plaguebringer old | Plaguebringer new |
|---|---|---|---|---|
| wins | 6/6 | 6/6 | 6/6 | 6/6 |
| kill time s (median) | 36.3 | 33.3 | 70.9 | 65.3 |
| levy deaths (median) | 14.5 | 3.5 | 1.0 | 1.5 |
| army DPS (median) | 10,319 | 11,639 | 4,946 | 5,285 |
| airborne % of fight time | 55 | 40 | 70 | 62 |
| wing s spent per levy-minute | 6.4 | 5.0 | 7.7 | 8.7 |
| hovering %, all / without hover gear | 5.6 / 5.2 | 0.1 / 0.1 | 3.3 / 3.2 | 0.3 / 0.3 |
| climbing to a target overhead, % of time | 22 | 4 | 24 | 9 |
| hits taken per levy-minute | 5.1 | 5.7 | 40 | 48 |
| damage taken per levy-minute | 539 | 262 | 150 | 188 |
| dodge taken when a hit was predicted | 0.64 | 0.82 | 0.91 | 0.93 |
| predicted hit with no better move | 0.36 | 0.18 | 0.09 | 0.07 |

Two earlier runs agree on the Ravager (deaths 16 to 4.5 and 12.5 to 2.5). Hits taken at the Plaguebringer move 40 to 48 between runs of the same arm, so its rise is inside the noise; the remaining airborne time there is the boss flying out of reach (a real reason) plus dodge hops. The Plaguebringer's wing spend rises because a climb on non-hover wings now costs what it costs a player.

Dodging: on the floor the field finds a dodge for a predicted hit 82% of the time against 64% in the air, which is most of the Ravager death drop. Homing missiles are the hard case: predicting them on their turning arc (`LEVY_PROJTURN=1`, lab only) gave 47.7 hits per levy-minute against 43.2 without, no gain, so it stays off.

## Mind Link checks (IdentityWorld, before = the drive at dec2255)

Walking with wings or rocket boots keeps the feet down (both). A jump tap on Angel Wings: player 59 px, before 98, now 59. Jump held 40 ticks: player 428, before 189, now 428. Down + jump on Angel Wings: before a hover (4.7 px moved), now a climb as for the player. Vortex Booster and Hadarian Wings hover in both. The grapple key at a ceiling 14 rows up: now 1 cast, pulled up 165 px, a jump lets go. A rope: before nothing, now 262 px climbed in 80 ticks and the body hangs still. Fire audit on her 166-weapon endgame list: the Mind Link fires 165 of 166 the way the levy fires them alone; the one exception is Phalanx Surge aimed at an enemy (3 shots, 0 damage in 300 ticks, against 51 shots and 7,890 when the levy fires it alone; at an empty cursor it fires normally). The button change above did not fix it; the cause is not isolated yet. The rest of the weapons that "don't work" fail for the levy's own weapon layer too (unsupported by the classifier, holdouts fired as plain shots, boomerang and spear stand-ins: see ENDGAME_SWEEP.tsv).

Ship: serial ShipWorld, vanilla and Calamity halves, 0 FAIL (IdentityWorld 177 / 350, MobWorld 42 / 62 with the new hover trials, Bodyguard 7 / 8); Calamity boot exc=0.
