# PvP: Eve's levies against the optimal levies

64 fights. Each matchup ran 8 seeds with Eve's army on the left and 8 with it on the right. Master + Death, arena a3, Search & Destroy, 180 s cap (no fight reached it). Setup and fairness rules: `README.md`. Per-fight rows: `fights.json`.

## Who won

| matchup | Eve's wins | optimal's wins | Eve wins left / right | median fight s | median survivors (winner) | Eve dmg (median) | optimal dmg (median) |
|---|---|---|---|---|---|---|---|
| Eve (Nov 16, 34 levies) vs optimal (measured DPS, pre-Moon Lord) | **8** | **8** | 5/8, 3/8 | 12 | 9 | 26080 | 24290 |
| Eve (Nov 16) vs optimal, fight-tested ranged | 0 | **16** | 0/8, 0/8 | 7 | 17.5 | 18728 | 24574 |
| Eve now (post-Providence, 40 levies) vs optimal at that tier | 1 | **15** | 1/8, 0/8 | 6 | 26.5 | 19586 | 33016 |
| Eve now vs optimal at that tier, fight-tested ranged | 1 | **15** | 0/8, 1/8 | 5 | 30 | 15138 | 37248 |

Deaths over time (median share of the army dead at 3 / 6 / 10 s; the first death comes at about 2 s in every matchup):

| matchup | Eve | optimal |
|---|---|---|
| Nov 16 vs optimal | 7% / 53% / 78% | 49% / 79% / 88% |
| Nov 16 vs optimal ranged | 21% / 85% / 100% | 21% / 46% / 49% |
| now vs optimal | 68% / 99% / 100% | 18% / 32% / 34% |
| now vs optimal ranged | 72% / 100% / 100% | 12% / 25% / 25% |

Damage by weapon class (share of the army's damage, all its fights): Eve Nov 16 summon 70-77%; Eve now ranged 31-36%, summon 31-34%, melee 26-27%; optimal summon 35-45% plus ranged (pre-Moon Lord) or rogue and melee (current tier).

Top weapons (damage per levy per fight): Eve Nov 16: Dark Harvest 2642, Morning Star 2564, Sniper Rifle 1091, then nothing above 450 (Apathanull 97, Terra Blade 173, True Ark 215). Eve now: Dark Harvest 1530, Stellar Contempt 1134, Spyker 793 (x7), Ark of the Elements 672, Morning Star 665. Optimal now: Morning Star 3583, Molten Amputator 1555, Ark of the Elements 556. Optimal ranged now: Morning Star 2371, Spyker 1853, Molten Amputator 1361. Pre-Moon Lord optimal: Morning Star 1712, Star Sputter 1145.

## Why
1. **Whips decide a levy-on-levy fight.** A lash hits every body in a clump, and both armies clump. Whips are 35-77% of every army's damage. Her Nov 16 army had six whips, the same as the measured optimal, and it tied 8-8. Her current army has five whips in 40 levies, and her two Morning Stars do 665 each against 2371-3583 for the optimal Morning Stars in the same fights.
2. **Many of her slots do almost nothing, and her current army fights at range without the right guns.** Apathanull, Terra Blade, True Ark, Tarragon Throwing Dart, Seeking Scorcher, Planetary Annihilation and Doomsday Device each do under 450 a fight. The optimal fills those slots with Molten Amputator (1361-1555) and Star Sputter or Spyker. Her seven Spykers do 793 each against 1853 for the six in the optimal ranged army. The optimal Spykers live (17% die, against 99% of hers), so they keep firing.
3. **The first seconds snowball.** Whoever loses bodies early loses the fight. Her current army is 68-72% dead at 3 s and the optimal only 12-18%. The winner keeps 26-30 of 40. The optimal armies also wear one coherent set (Tarragon, Elysian Wings, 500 HP) against her mix, and they drink fewer potions because fewer of them get hurt.

## Lab notes and bugs found (not fixed: gameplay)
- **The commander's facing changes her levies' damage.** Every levy shot is owned by the commander's player slot. In identical mirrors with the commander facing right, the army on the right (shooting left) won 10 of 12 and its lashes did 2-3 times the damage; facing left, the left army won 6 of 6. In a game, whichever side of the commander the enemy stands on changes what her levies do, whips most of all. The lab cancels it: the commander faces the way each levy faces while that levy and its shots update, and she turns every tick. After that, mirrors came out 10 of 16 for the right side (noise).
- Whips are the strongest levy weapon against bodies that clump. This fits the battery's open question: whips read 0 on the dummy but 30-50% of boss damage.
- Fights are short (5-12 s; the first death comes at about 2 s). Levies have about 500 HP and the other army's whole damage output lands on them. Hits use the game's strike with player-mode defense, and every levy gets its own player-style hit window.
- "Moon Boots" (item 5819 in her world) comes from a mod the lab does not load. It sat in the wings slot of 21 of her current levies and is replaced by Solar Wings.
