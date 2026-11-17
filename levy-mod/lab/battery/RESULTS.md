# Battery: Eve's levies against the best levies at her tier (Ravager, Empress of Light)

Calamity, Master + Death. Lab only: the code changes are harness hooks (per-levy sidearm, hook, Life Fruit and item prefixes in `lv=` armies; a defense knob on the per-weapon dummy).

## Did her levies win?

### Ravager

| army | wins | kill time s, median (range) | boss HP left in the losses | army DPS (median) | per-levy DPS | levy deaths (median) | first death s | levies not engaged % | levies >50 tiles from the boss % |
|---|---|---|---|---|---|---|---|---|---|
| Eve's army | **5/8** | 39 (34-46) | 52%, 43%, 25% | 8661 | 255 | 29/34 | 11.6 | 20 | 37 |
| Eve's army + Brimflame on her 10 mages | **5/8** | 42 (34-70) | 24%, 32%, 28% | 5993 | 176 | 32/34 | 11.4 | 30 | 35 |
| optimal (measured DPS) | **5/5** | 28 (27-31) | - | 11771 | 346 | 22/34 | 8.1 | 57 | 43 |
| optimal, fight-tested (ranged) | **5/5** | 33 (27-34) | - | 11449 | 337 | 23/34 | 10.2 | 30 | 31 |

### Empress of Light, at night

| army | wins | kill time s, median (range) | boss HP left in the losses | army DPS (median) | per-levy DPS | levy deaths (median) | first death s | levies not engaged % | levies >50 tiles from the boss % |
|---|---|---|---|---|---|---|---|---|---|
| Eve's army | **2/8** | 66 (62-70) | 75%, 39%, 81%, 32%, 9%, 29% | 4815 | 142 | 34/34 | 7.1 | 57 | 39 |
| Eve's army + Brimflame on her 10 mages | **2/8** | 54 (50-58) | 7%, 30%, 54%, 18%, 44%, 17% | 4959 | 146 | 34/34 | 7.1 | 57 | 39 |
| optimal (measured DPS) | **5/5** | 38 (32-48) | - | 8193 | 241 | 21/34 | 7.0 | 62 | 52 |
| optimal, fight-tested (ranged) | **5/5** | 27 (21-27) | - | 13184 | 388 | 11/34 | 18.0 | 47 | 40 |

Wins are kills before the cap (Ravager 360 s, Empress 300 s); a loss here is always a wipe. "Not engaged" = share of each living levy's samples in any state but Engage. Seeds vary only the dice; the sim is chaotic (seed 1 of Eve vs Ravager won in 72 s on one run and 46 s on the next), so 5/8 against 5/5 is a direction, not a precise rate.

The Brimflame upgrade is not worth it: the same 5/8 and 2/8, no faster, no fewer deaths. Her mages do 7-16% of the army's damage, and three of the ten hold a Star Shower that does none.

## Setup (same for every army)
- Arena a3 (the big lab arena, five platform rows), Calamity world, night, Hallow zone for the Empress. Stations: campfire, heart lantern, star in a bottle, sunflower, bast statue, sharpening station, war table, bewitching table, ammo box, crystal ball, honey.
- Commander: the harness stand-in, idle and pinned near the army: invulnerable, no gear, 500 HP, under the Spectator buff, so the boss never targets him. Army order Follow.
- Supplies: 8 Greater Healing Potions each, no buff potions (her levies carried no buffs in the recordings), 9 mana crystals.
- 34 levies, grade Levy, 2 accessory slots plus the wings and boots slots, one sidearm slot.

## Eve's army (eve_army.json), from her 2026-11-16 22:08 Empress recording
Recorded for every levy: weapon and prefix, sidearm, armour, the two accessories, wings, boots, hook, ammo, max life. 6 whip summoners (3 Dark Harvest, 3 Morning Star, staff sidearms), 8 melee (3 Terra Blade, 4 Ark of the Ancients, 1 The Microwave), 6 ranged (Malevolence, Stake Launcher, Realm Ravager, Vernal Bolter, Sniper Rifle, Havoc's Breath), 10 magic (4 Apathanull, 3 Star Shower, 3 Wingman), 4 rogue (The Syringe, 2 Doomsday Device, Regulus Riot). Armour is mostly Chlorophyte, Beetle, Spectre and Shroomite, one Brimflame. 420-460 HP (15 crystals and 4-12 fruit).
Not recorded, stand-ins chosen: mana crystals (9), potions (8 Greater Healing), stations (above). Her 2 officers and 12 militia are not levies and are left out of every army.
Broken in levy hands and carried by her levies: Star Shower x3 and Doomsday Device x2 deal 0, Wingman x3 fires at half rate; sidearms Ultimus Cleaver (0 on the dummy), Pulse Turret Remote, and the weak spears Vulcanite Lance, Botanic Piercer, Hellion Flower Spear. Levy 14 carries a Sniper Rifle loaded with Musket Balls.
In her real fights the levies did almost nothing: at Ravager (22:00) none were near the fight and she won it alone with the Doomsday Device; at the Empress they did 4% of the damage. The lab answers "could her levies win on their own".

## The optimal army (optimal_army.json, optimal_ranged_army.json; mkopt.py)
Tier: before Moon Lord, after Golem, Plaguebringer, Ravager, Astrum Deus and the Lunatic Cultist with the pillars up (her 22:11 recording), so lunar-fragment weapons count; Duke Fishron and Empress drops are left out (she has not beaten them).
Weapons by measured levy DPS against a boss-sized dummy with defense 50 (specs/battery_dps*.yaml, one levy, 20 s window), at most 6 levies per weapon:
- Daybreak x6 (1452 DPS on the dummy), Solar Eruption x6 (1407), Chlorophyte Saber x6 (1149): the three top melee numbers.
- Morning Star x6: whips read 0 on the dummy, so the number is ARMYSWEEP_DPS (979), and whips are the top damage dealers in every real fight.
- Star Sputter x6 (828) and Vortex Beater x4 with Bubonic Rounds (808): the top ranged numbers.
- Fight-tested variant: the melee did 0-7% of the damage in the first fights, so their 18 places go to Vortex Beater (6), Astral Blaster x6 (673), Realm Ravager x5 (618), Regulus Riot x5 (635).
Gear (one line each):
- Astral armour for all: the highest pre-Moon Lord set (63 defense, damage and crit for every class, minion slots, rogue stealth).
- Hadarian Wings: the best wings Eve can make before Moon Lord without Fishron or the Empress.
- Angel Treads: the best movement boots before Moon Lord.
- Destroyer Emblem: +10% damage and +8% crit for every class.
- Celestial Stone (non-summoners): +10% damage and crit plus speed, regen and defense, no transformation.
- Papyrus Scarab (summoners): an extra minion slot and summon damage.
- Stardust Cell Staff sidearm on the whip levies: the best measured summon staff (215 on the dummy).
- Spooky Hook: Eve's own best hook.
- Bubonic Rounds: the best measured bullet (520 vs Venom 436, Chlorophyte 284, Hyperius 45 on a Chain Gun).
- Prefixes: Godly on Daybreak and Solar Eruption (Legendary does not apply to them), Legendary on the other melee and the whip, Unreal ranged, Flawless rogue, Ruthless staff, Menacing on every accessory.
- 15 crystals and 20 fruit (500 HP) on every levy.

## What explains the gap
1. Dead weapons. 8 of her 34 primaries are broken in levy hands (3 Star Shower and 2 Doomsday Device at 0, 3 Wingman at half rate), and the rest of the magic and rogue line is low: her 10 mages and 4 rogues do 12-20% of the damage. Her army lives on 6 whip summoners (60-70% of all her damage).
2. Per-hit damage against armour. Ravager has 55 defense and 35% DR, the Empress 50 and 15%. Apathanull, her main magic weapon, drops from 1105 DPS (ARMYSWEEP) to 173 on a defense-50 dummy. The optimal ranged guns hit hard enough to keep their damage.
3. Deaths. Her army loses 29-34 of 34 levies, usually all of them at the Empress; the first dies at 7 s (the Empress's opening attack) and the Ravager's slam kills 5-17 at once around 12-25 s. The fight-tested optimal army loses 11 at the Empress and its first death comes at 18 s: Astral armour, 500 HP and keeping range matter.
4. Range and engagement. At the Empress her levies are not engaged 57% of the time. Melee does badly in both armies: the optimal melee picks either never engaged (Daybreak) or died in the first Ravager slam.
5. Accessories and armour. Hers are a mix (Moon Stone, emblems, Yoyo Bag, Power Glove, Charm of Myths); the optimal gear adds damage and survivability, but the weapon swap is most of the difference. Giving only her mages Brimflame changed nothing.

## Behaviour bugs seen (not fixed)
- Daybreak levies stay in Follow about 55 tiles from the boss and deal 0 boss damage in every army fight (optimal_ravager_1..5, optimal_eol_1..5), although one levy does 1452 DPS on the dummy. Daybreak is classified ThrownMelee with a 14-tile range.
- One Ravager slam kills a block of levies at once: optimal_ravager_3 15 levies in one second at tick 760 (12.7 s); eve_brimflame_ravager_4 17 at tick 736; eve_ravager_8 13 at tick 1472. The Solar Eruption and Chlorophyte Saber levies die together within 2.5 s at about tick 700-770 in every optimal Ravager fight.
- The Empress kills the first levies at tick 384-407 (6.4-6.8 s) in almost every fight of every army.
- Apoctosis Array reads 41,205 DPS on the defense-50 dummy (103,796 projectiles counted in 20 s), about 50 times any other weapon: an anomaly to check before anyone uses it.
- Whips: 0 damage on the dummy, yet 30-50% of army damage in boss fights, and ARMYSWEEP rates them 3.5-4.2 times a player's damage. Something about whip damage in levy hands is off in one direction or the other.
- Ultimus Cleaver deals 0 in levy hands (Eve's levy 23 has it as a sidearm). Hyperius Bullets drop a Chain Gun to 45 DPS (Bubonic 520).
- Eve's levy 14 fires a Sniper Rifle with Musket Balls.

## Files and how to rerun
- Armies: eve_army.json (mkeve.py), eve_brimflame_army.json, optimal_army.json and optimal_ranged_army.json (mkopt.py).
- Fight specs: specs/fights_eve.tsv, fights_round2.tsv, fights_opt.tsv, fights_optr.tsv (mkfights.py); DPS trials specs/battery_dps.yaml and battery_dps2.yaml.
- Run: `levy-mod/lab/levylab run SPEC --jobs 1 --rec 99`, then `collect.py RUN --dest DIR` (gzip .levyrec named army_boss_seed + fights.json), `table.py` and `summarize.py`. Results of these runs: fights.json.
- The first optimal run (Daybreak and Solar Eruption without their prefix) also won 6/6 and is not in the table.
