# Warbands: fight comparison (phase 2 lab)

Source: `Warbands/Common/WarbandsLab.cs`, run with `WARBANDS_LIVE=fight WARBANDS_SEEDS=6 WARBANDS_OUT=f.tsv` on a headless server (Expert). Raw rows: `fight_lab.tsv`. The real game update runs in a sealed sky arena; the stand-in player (slot 0) wears the stage kit through the real equip pass (pre-boss Gold armor 200 HP, post-EoW Shadow 300 HP +2 def, early Hardmode Mythril 400 HP +5, post-Plantera Turtle 500 HP +10) and a puppet weapon (the stage's gun or bow: real item plus ammo damage, 70% hit chance, one shot per use time, nearest enemy within 32 tiles with a clear line, struck through the real NPC defense rule). A dedicated server runs no player-side NPC collision, so contact damage is applied by the lab through the player's own `Hurt` (defense and immunity). Hostile projectiles hit through vanilla. Each fight is a band of 4 (or 4 ordinary enemies of the stage) 28 to 34 tiles away, 75 seconds at most, 6 seeds. The player is held up in the arena; "dead" means the damage taken reached the kit's life.

Arms: levy-ai band (the same roster on Levy's real brain), fighter-ai band (phase 1: vanilla Fighter AI plus a shooter layer), normal enemies (Zombie, Demon Eye, Skeleton; Fire Imp, Hornet, Demon; Chaos Elemental, Pixie, Corruptor; Blue Armored Bones, Necromancer, Skeleton Sniper).

Stat rules in force (from `TTK_EXPERT.md`): landed hit capped near 12% of the stage's typical life (24 / 36 / 42 / 48 / 54 / 60 / 66; twice that for a slow heavy weapon); sustained landed damage 15 / 40 / 45 / 50 / 50 / 50 / 55 per member on the player's own 1.4 landed hits per second.

| stage | arm | player damage taken (mean / HP) | player dead | clear s (median, cap=75) | enemies killed | fled | panics | rotations |
|---|---|---|---|---|---|---|---|---|
| pre-boss | levy-ai band | 32 / 200 | 0/6 | 75 | 1.8 of 4 | 1.3 | 4.7 | 15 |
| pre-boss | fighter-ai band | 0 / 200 | 0/6 | 24 | 4.0 of 4 | 0.0 | 0.0 | 0 |
| pre-boss | normal enemies | 0 / 200 | 0/6 | 9 | 3.8 of 4 | 0.0 | 0.0 | 0 |
| post-EoW | levy-ai band | 26 / 300 | 0/6 | 75 | 1.3 of 4 | 1.3 | 4.0 | 22 |
| post-EoW | fighter-ai band | 0 / 300 | 0/6 | 16 | 4.0 of 4 | 0.0 | 0.0 | 0 |
| post-EoW | normal enemies | 0 / 300 | 0/6 | 75 | 2.3 of 4 | 0.0 | 0.0 | 0 |
| early Hardmode | levy-ai band | 427 / 400 | 6/6 | 75 | 0.0 of 4 | 0.0 | 0.0 | 3 |
| early Hardmode | fighter-ai band | 0 / 400 | 0/6 | 53 | 4.0 of 4 | 0.0 | 0.0 | 0 |
| early Hardmode | normal enemies | 0 / 400 | 0/6 | 25 | 4.0 of 4 | 0.0 | 0.0 | 0 |
| post-Plantera | levy-ai band | 0 / 500 | 0/6 | 75 | 0.7 of 4 | 2.2 | 3.2 | 8 |
| post-Plantera | fighter-ai band | 0 / 500 | 0/6 | 75 | 2.0 of 4 | 0.0 | 0.0 | 0 |
| post-Plantera | normal enemies | 0 / 500 | 0/6 | 75 | 1.5 of 4 | 0.0 | 0.0 | 0 |


## Reading it

1. The Levy-AI band is the only arm that touches the player. The Fighter-AI band and the ordinary enemies never landed a hit on a player who shoots from 32 tiles: they die walking in (4 of 4 in 9 to 55 s). The Levy-AI band keeps its distance (ranged members kite to the standoff), dodges the puppet's slow shots, rotates its aggro and breaks off: members panic 3 to 5 times per fight and one to three drink a Recall potion. A fight against it ends with survivors, not a clean kill, so its clear time reads 75 s (the cap).
2. At post-Plantera (Turtle armor, 65+ defense) the band deals 0 damage in all six seeds: its landed hit after defense is 1, its members lose morale to chip damage and run. At early Hardmode (Mythril, 42 defense) the Levy-AI band killed the 400 HP player in 6 of 6 seeds within about ten seconds without losing a member: the stage's hit cap lands on every Excalibur or Crystal Storm contact and the puppet (static, no healing, no dodging) kills nothing in that time. That is the stat rule working as written; the early Hardmode roster deserves Eve's look before the numbers ship.
3. Before the stage caps (phase 1 rules) the same lab gave: early Hardmode 382 of 400 with 5 of 6 deaths, pre-boss 22, post-EoW 9, post-Plantera 0.
4. Limits: a static puppet that never dodges, never heals and never closes in; hostile projectiles and swings on the stand-in are the real code paths, vanilla contact damage is replayed; no multiplayer client.
