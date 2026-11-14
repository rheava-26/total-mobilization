# Boss battery, early Hardmode with Calamity (2026-11)

Measurement only, no product code changed. 30 mixed levies (10 ranged, 10 magic, 5 melee, 5 summoner), 400 HP each, 8 healing potions and the full buff set, postwof stations, arena a3, commander passive (Spectator stand-in), order follow, cap 300 s. Calamity 2.2.2 on tML 2026.10. 3 seeds per cell (same seeds = same army across arms). Raw rows: `results.csv` (231 fights). Scripts: `scripts/`. Gear route table: `item_tiers.tsv`.

## Headline

1. **Shared owner immunity is not the problem.** Across all 126 base fights, zero army shots were blocked by the commander-slot `npc.immune[owner]` gate. Vanilla and Calamity army projectiles that pierce already use per-shot (local) or per-type (static) immunity; single-hit shots are never gated. Melee swings bypass boss immunity completely (9,146 melee hits vs 850,412 projectile hit contacts landed).
2. **The sharing that does exist is static per-type immunity**, mostly on magic (Bee Gun bees, Aqua Scepter stream, Blood Beam, Space Gun laser and similar). Every levy holding the same weapon shares one cooldown on each boss part. Magic: 30-75% of contacts were blocked by it, and 15-50% of those blocks were set by a different levy's hit. Switching every shot to local immunity raised mean army DPS 2150 to 2369 (+10%) over the 33 on-tier Master+Death fights, but with 3 seeds that is within noise per boss (only Aquatic Scourge 161 to 99 s and Calamitas Clone 172 to 126 s are large). Per-levy immunity (each levy its own immunity, N-player style) did not help (2113): a levy's own shots still block each other.
3. **Hardest for the army:** Duke Fishron (wipe in about 17 s, every cell), Plantera (never killed, boss enraged), The Destroyer (wipe in Master+Death, 5-13 deaths in Expert+Rev). **Easiest:** Queen Slime, Empress of Light (she only targets the commander here), Brimstone Elemental, Prime.

## Per boss tables (shared immunity = the product)
mid = gear route tier boss-1.5, on = gear route tier just below the boss. Time is game seconds. HP left over 100% means the boss gained HP (Levy boss HP scaling) over its starting max.

### Queen Slime

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 6.50) | 3/3 | 72 (69-87) | 0 (0-0) | - | 1119 (880-1177) | 51840 (51840-51840) | - | 0 (0-0) |
| Expert+Rev | on (T 7.99) | 3/3 | 46 (36-47) | 0 (0-0) | - | 1756 (1723-2299) | 51840 (51840-51840) | - | 3 (0-4) |
| Master+Death | mid (T 6.50) | 3/3 | 95 (92-119) | 0 (0-1) | 42 | 1060 (842-1090) | 66096 (66096-66096) | - | 6 (3-6) |
| Master+Death | on (T 7.99) | 3/3 | 58 (41-65) | 0 (0-2) | 20 | 1818 (1610-2526) | 66096 (66096-66096) | - | 7 (2-12) |

### Cryogen

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 7.00) | 3/3 | 52 (43-55) | 0 (0-1) | 51 | 2359 (2168-2818) | 76800 (76800-76800) | - | 4 (3-7) |
| Expert+Rev | on (T 8.49) | 3/3 | 44 (42-46) | 0 (0-1) | 34 | 2712 (2584-2821) | 76800 (76800-76800) | - | 6 (4-6) |
| Master+Death | mid (T 7.00) | 3/3 | 83 (74-96) | 7 (5-10) | 39 (23-58) | 1814 (1616-2115) | 97920 (97920-97920) | - | 25 (22-39) |
| Master+Death | on (T 8.49) | 3/3 | 72 (51-95) | 5 (2-6) | 44 (38-59) | 2082 (1577-3009) | 97920 (97920-97920) | - | 19 (11-23) |

### The Twins

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 7.50) | 3/3 | 60 (46-64) | 2 (0-2) | 18 (14-22) | 1898 (1872-2502) | 118758 (114069-125062) | - | 7 (0-17) |
| Expert+Rev | on (T 8.99) | 3/3 | 58 (52-61) | 2 (1-3) | 41 (28-43) | 1982 (1960-2229) | 122794 (122039-122794) | - | 15 (8-15) |
| Master+Death | mid (T 7.50) | 3/3 | 104 (62-133) | 22 (6-25) | 18 (10-19) | 1470 (1059-2411) | 158491 (156433-159454) | - | 44 (20-46) |
| Master+Death | on (T 8.99) | 3/3 | 124 (85-127) | 15 (13-21) | 10 (8-15) | 1243 (1142-1756) | 159454 (158491-159454) | - | 43 (32-44) |

### Aquatic Scourge

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 8.00) | 3/3 | 104 (96-119) | 0 (0-0) | - | 1602 (1342-1698) | 245759 (236543-245759) | - | 0 (0-23) |
| Expert+Rev | on (T 9.49) | 3/3 | 100 (75-102) | 0 (0-0) | - | 1724 (1702-2476) | 245759 (245759-245759) | - | 0 (0-19) |
| Master+Death | mid (T 8.00) | 3/3 | 187 (96-217) | 0 (0-13) | 11 | 1125 (869-1364) | 313343 (313343-313343) | - | 0 (0-48) |
| Master+Death | on (T 9.49) | 3/3 | 161 (20-171) | 0 (0-6) | 10 | 1321 (1245-1407) | 313343 (301593-313343) | - | 14 (0-42) |

### The Destroyer

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 8.50) | 3/3 | 53 (43-56) | 13 (10-13) | 8 (8-9) | 2811 (2462-3464) | 215999 (215999-215999) | - | 22 (19-25) |
| Expert+Rev | on (T 9.99) | 3/3 | 38 (34-39) | 6 (5-10) | 8 (8-16) | 3840 (3729-4460) | 215999 (215999-215999) | - | 12 (11-19) |
| Master+Death | mid (T 8.50) | 0/3 | - | 30 (30-30) | 3 (2-3) | 1548 (1425-1612) | 275399 (271957-275399) | 20 (19-53) | 24 (20-26) |
| Master+Death | on (T 9.99) | 0/3 | - | 30 (30-30) | 3 (2-7) | 1666 (1398-1884) | 275399 (271957-275399) | 18 (10-24) | 18 (15-23) |

### Brimstone Elemental

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 9.00) | 3/3 | 35 (34-42) | 5 (3-5) | 17 (13-21) | 3602 (2928-3638) | 78720 (78720-78720) | - | 7 (6-7) |
| Expert+Rev | on (T 10.49) | 3/3 | 42 (32-43) | 0 (0-4) | 26 | 2911 (2744-3814) | 78720 (78720-78720) | - | 4 (1-5) |
| Master+Death | mid (T 9.00) | 3/3 | 66 (54-73) | 5 (2-10) | 24 (17-28) | 2393 (2179-2936) | 100368 (100368-100368) | - | 11 (5-14) |
| Master+Death | on (T 10.49) | 3/3 | 52 (49-56) | 3 (3-3) | 26 (14-36) | 2976 (2777-3144) | 100368 (100368-100368) | - | 11 (7-13) |

### Skeletron Prime

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 9.50) | 3/3 | 48 (34-59) | 5 (5-9) | 21 (19-22) | 1459 (1202-2110) | 45360 (45360-45360) | - | 11 (9-15) |
| Expert+Rev | on (T 10.99) | 3/3 | 42 (39-42) | 3 (2-3) | 22 (20-37) | 1693 (1666-1735) | 45360 (45360-45360) | - | 7 (6-10) |
| Master+Death | mid (T 9.50) | 3/3 | 60 (53-70) | 14 (10-16) | 29 (9-33) | 1503 (1296-1702) | 57834 (57834-57834) | - | 23 (21-25) |
| Master+Death | on (T 10.99) | 3/3 | 68 (65-69) | 8 (6-11) | 34 (8-35) | 1302 (1298-1315) | 57834 (57834-57834) | - | 16 (15-16) |

### Calamitas Clone

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 10.20) | 3/3 | 76 (68-148) | 1 (0-1) | 69 (42-96) | 1527 (808-1726) | 75000 (75000-75000) | - | 4 (0-20) |
| Expert+Rev | on (T 11.69) | 3/3 | 132 (105-149) | 0 (0-0) | - | 878 (798-1099) | 75000 (75000-75000) | - | 10 (2-12) |
| Master+Death | mid (T 10.20) | 3/3 | 118 (109-136) | 9 (7-10) | 64 (51-66) | 1243 (1110-1356) | 95625 (95625-95625) | - | 43 (42-43) |
| Master+Death | on (T 11.69) | 3/3 | 172 (122-201) | 7 (0-8) | 103 (84-121) | 880 (724-1223) | 95625 (95625-95625) | - | 37 (16-47) |

### Plantera

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 10.50) | 0/3 | - | 10 (9-13) | 10 (8-11) | 1608 (1148-1639) | 119699 (119699-119699) | 160 (160-160) | 18 (18-21) |
| Expert+Rev | on (T 11.99) | 0/3 | - | 11 (9-14) | 12 (7-28) | 1889 (1380-1894) | 119699 (119699-119699) | 136 (135-160) | 19 (18-20) |
| Master+Death | mid (T 10.50) | 0/3 | - | 10 (6-11) | 12 (7-24) | 1616 (1403-1821) | 152617 (152617-152617) | 160 (156-160) | 19 (12-21) |
| Master+Death | on (T 11.99) | 0/3 | - | 6 (6-16) | 20 (12-25) | 1715 (1354-1904) | 152617 (152617-152617) | 160 (160-160) | 13 (13-24) |

### Duke Fishron

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 12.50) | 0/3 | - | 30 (30-30) | 3 (3-4) | 379 (267-518) | 179400 (179400-179400) | 154 (154-157) | 24 (20-25) |
| Expert+Rev | on (T 13.99) | 0/3 | - | 30 (30-30) | 3 (2-4) | 425 (315-711) | 179400 (179400-179400) | 150 (146-152) | 22 (22-26) |
| Master+Death | mid (T 12.50) | 0/3 | - | 30 (30-30) | 2 (2-3) | 349 (158-611) | 228735 (228735-228735) | 155 (153-158) | 8 (5-9) |
| Master+Death | on (T 13.99) | 0/3 | - | 30 (30-30) | 2 (2-2) | 581 (362-634) | 228735 (228735-228735) | 151 (151-152) | 6 (4-15) |

### Empress of Light

| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |
|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid (T 13.50) | 3/3 | 42 (40-45) | 0 (0-13) | 7 | 6138 (5724-6552) | 166600 (166600-166600) | - | 0 (0-3) |
| Expert+Rev | on (T 14.99) | 3/3 | 36 (34-37) | 0 (0-6) | 7 | 7080 (6957-7406) | 166600 (166600-166600) | - | 2 (0-3) |
| Master+Death | mid (T 13.50) | 3/3 | 47 (45-72) | 0 (0-18) | 7 | 7019 (4500-7304) | 212415 (212415-212415) | - | 1 (1-10) |
| Master+Death | on (T 14.99) | 3/3 | 43 (39-45) | 0 (0-10) | 7 | 7430 (7247-8407) | 212415 (212415-212415) | - | 0 (0-5) |


Wall of Flesh is not in the tables: the lab arena is on the surface and WoF stays at Underworld depth (one smoke fight, 100 s, 0 damage dealt). Not measured.

## Ranking (Master+Death, on-tier kit, 3 seeds)
| rank (hardest first) | boss | intended order | wins | kill s | levy deaths /30 |
|---|---|---|---|---|---|
| 1 | Duke Fishron | 11 | 0/3 | wipe at ~17 s | 30 |
| 2 | Plantera | 10 | 0/3 | 300 s timeout | 6-16 |
| 3 | The Destroyer | 6 | 0/3 | wipe at ~30 s | 30 |
| 4 | The Twins | 3 | 3/3 | 124 | 15 |
| 5 | Calamitas Clone | 9 | 3/3 | 172 | 7 |
| 6 | Aquatic Scourge | 4 | 3/3 | 161 | 0 |
| 7 | Skeletron Prime | 8 | 3/3 | 68 | 8 |
| 8 | Cryogen | 2 | 3/3 | 72 | 5 |
| 9 | Brimstone Elemental | 7 | 3/3 | 52 | 3 |
| 10 | Queen Slime | 1 | 3/3 | 58 | 0 |
| 11 | Empress of Light | 12 | 3/3 | 43 | 0 |

## Much easier or harder than their slot
- **The Destroyer** is far harder than its slot (6th of 11): first deaths at 2-3 s in Master+Death, 13 deaths even in Expert+Rev with the mid kit. Worm contact damage, not army DPS, decides it.
- **Duke Fishron and Plantera** are far harder, but both were enraged for the whole fight in the lab (see caveats), so their numbers overstate the real difficulty until the enrage is explained.
- **Empress of Light** is far easier than her slot: she targets the commander slot 100% of the time (with `cmd=far` she chases the unreachable commander and a 60 s fight ends 91% unkilled), so the army kills her at 6-7k DPS. It means bosses of this AI ignore levies, which is itself worth a look.
- **Brimstone Elemental, Prime, Cryogen, Queen Slime** are easier than the Twins / Destroyer / Aquatic Scourge in the same early-Hardmode window, consistent with Eve's "some bosses are way easier".
- **Gear tier effect is small** inside this window: on-tier beat mid by 0-40% kill time on most bosses, but Calamitas Clone on-tier was slower than mid (172 vs 118 s), because the kit takes the top-6 by score and that pool differs. Take mid vs on as "mild gear gradient", not precise.

## Hit immunity
Method: a detour on `Projectile.Damage` classifies each (army projectile, boss part) contact at the moment vanilla runs its gate: landed, blocked by shared `npc.immune[owner]`, blocked by the shot's own local immunity, blocked by static per-type immunity, other. Arms (spec token `imm=`): `shared` (product), `local` (every shared or static shot made local, same cooldown), `static`, `perlevy` (per-levy immunity columns for both the owner and the static gates). Prediction of the shared gate was checked against reality: 0 violations.

On-tier Master+Death, 3 seeds per arm (landed and blocked are summed over the seeds):

| boss | arm | wins | kill s (median) | army DPS (median) | deaths (median) | landed hits | static-blocked | cross-levy blocked | detour us/tick | alt us/tick |
|---|---|---|---|---|---|---|---|---|---|---|
| qslime | shared | 3/3 | 58 | 1818 | 0 | 15990 | 6585 | 1662 | 518 | 2 |
| qslime | local | 3/3 | 50 | 2043 | 0 | 13394 | 0 | 0 | 323 | 0 |
| qslime | static | 3/3 | 43 | 2381 | 0 | 15005 | 6288 | 1345 | 497 | 1 |
| qslime | perlevy | 3/3 | 48 | 2206 | 1 | 14494 | 6303 | 1127 | 357 | 1 |
| cryo | shared | 3/3 | 72 | 2082 | 5 | 17883 | 25713 | 12745 | 869 | 0 |
| cryo | local | 3/3 | 68 | 2201 | 6 | 15749 | 0 | 0 | 876 | 1 |
| cryo | static | 3/3 | 89 | 1682 | 5 | 17175 | 23271 | 10304 | 640 | 1 |
| cryo | perlevy | 3/3 | 74 | 2020 | 4 | 15536 | 17054 | 7023 | 523 | 1 |
| twins | shared | 3/3 | 124 | 1243 | 15 | 24798 | 13721 | 1949 | 484 | 0 |
| twins | local | 3/3 | 122 | 1208 | 9 | 23286 | 0 | 0 | 300 | 0 |
| twins | static | 2/3 | 119 | 1242 | 13 | 21233 | 14087 | 1757 | 342 | 0 |
| twins | perlevy | 3/3 | 103 | 1491 | 14 | 25659 | 16623 | 2592 | 457 | 1 |
| aqua | shared | 3/3 | 161 | 1321 | 0 | 22624 | 6666 | 1948 | 890 | 1 |
| aqua | local | 3/3 | 99 | 1511 | 0 | 25929 | 0 | 0 | 823 | 0 |
| aqua | static | 3/3 | 155 | 1319 | 0 | 35724 | 10876 | 4026 | 622 | 0 |
| aqua | perlevy | 3/3 | 160 | 1258 | 0 | 27598 | 11564 | 4243 | 511 | 1 |
| destroyer | shared | 0/3 | - | 1666 | 30 | 23965 | 12846 | 3135 | 441 | 1 |
| destroyer | local | 1/3 | 54 | 2509 | 30 | 29831 | 0 | 0 | 303 | 1 |
| destroyer | static | 1/3 | 85 | 2037 | 30 | 25054 | 16180 | 3098 | 324 | 1 |
| destroyer | perlevy | 2/3 | 57 | 2788 | 25 | 31096 | 6760 | 631 | 372 | 2 |
| brim | shared | 3/3 | 52 | 2976 | 3 | 23897 | 13916 | 3041 | 352 | 1 |
| brim | local | 3/3 | 65 | 2343 | 5 | 23089 | 0 | 0 | 299 | 1 |
| brim | static | 3/3 | 54 | 2892 | 5 | 22656 | 17564 | 5078 | 326 | 1 |
| brim | perlevy | 3/3 | 59 | 2594 | 6 | 22228 | 13519 | 1817 | 432 | 3 |
| prime | shared | 3/3 | 68 | 1302 | 8 | 11016 | 7962 | 2491 | 413 | 2 |
| prime | local | 3/3 | 68 | 1319 | 7 | 11611 | 0 | 0 | 485 | 0 |
| prime | static | 3/3 | 69 | 1302 | 9 | 10789 | 6894 | 2342 | 298 | 0 |
| prime | perlevy | 3/3 | 62 | 1309 | 9 | 11147 | 5787 | 1424 | 215 | 1 |
| cal | shared | 3/3 | 172 | 880 | 7 | 10487 | 5891 | 1275 | 298 | 0 |
| cal | local | 3/3 | 126 | 1181 | 1 | 9875 | 0 | 0 | 203 | 0 |
| cal | static | 3/3 | 121 | 1226 | 3 | 11898 | 10126 | 2032 | 147 | 0 |
| cal | perlevy | 3/3 | 160 | 924 | 6 | 10886 | 7664 | 971 | 177 | 0 |
| plant | shared | 0/3 | - | 1715 | 6 | 45767 | 27035 | 7417 | 288 | 1 |
| plant | local | 0/3 | - | 1471 | 14 | 37736 | 0 | 0 | 139 | 0 |
| plant | static | 0/3 | - | 1427 | 16 | 41804 | 34542 | 6146 | 152 | 0 |
| plant | perlevy | 0/3 | - | 1523 | 15 | 41373 | 30996 | 1233 | 155 | 1 |
| fish | shared | 0/3 | - | 581 | 30 | 1366 | 14301 | 5691 | 549 | 0 |
| fish | local | 0/3 | - | 524 | 30 | 3146 | 0 | 0 | 371 | 0 |
| fish | static | 0/3 | - | 332 | 30 | 1067 | 8551 | 2134 | 277 | 0 |
| fish | perlevy | 0/3 | - | 512 | 30 | 1511 | 13626 | 5587 | 244 | 1 |
| eol | shared | 3/3 | 43 | 7430 | 0 | 15087 | 68815 | 5718 | 517 | 1 |
| eol | local | 3/3 | 35 | 9186 | 11 | 15884 | 0 | 0 | 600 | 1 |
| eol | static | 3/3 | 47 | 6758 | 15 | 14693 | 70141 | 4472 | 495 | 1 |
| eol | perlevy | 3/3 | 52 | 6156 | 13 | 15617 | 38415 | 4769 | 432 | 2 |

shared mean army DPS over 33 fights: 2150
local mean army DPS over 33 fights: 2369
static mean army DPS over 33 fights: 2031
perlevy mean army DPS over 33 fights: 2113

- Contact counts overstate "wasted shots": a shot touching a worm for 10 ticks counts each tick. The DPS and kill-time columns are the real cost estimate.
- Shared owner gate blocked: 0 in every arm and fight. avgImmValue never recorded because no shared-type shot landed.
- CPU: the classification itself costs about 0.15-0.9 ms per game tick (median 0.5 ms with 30 levies) and is lab-only. The alternatives cost almost nothing: `local` flips two fields per shot (0 us/tick measured), `perlevy` adds 0-3 us/tick (a dictionary read and write per contact), `static` the same.
- Estimate of damage lost to static sharing: +10% mean army DPS when the sharing is removed (noisy; per boss -20% to +60%). Not conclusive at 3 seeds.

## Caveats and unverified
- Headless server, stand-in commander (invulnerable, pinned, Spectator). Commander damage taken is 0 by construction; no dodging commander and no real client were tested.
- Enrage: Brimstone Elemental, Plantera and Duke Fishron were enraged (Calamity `CurrentlyEnraged`) for 100% of every fight even with the commander flagged in the right biome; Aquatic Scourge varied. I did not find why (decompilation was not done). Plantera also fled in about 11 s with the default Spectator commander, so Plantera runs use `spec=0` (visible commander) and Plantera and Fishron results are not comparable.
- Gear is data-built (route tier from the game's drop and recipe tables, ranked by Levy's weapon score, top 6 per class at random). Armor and accessory ladders are hand-set vanilla. Calamity armor in Hardmode is not used. Levies stay at 400 HP.
- Boss HP is Levy-scaled (up to about 1.3x per fight); sim is chaotic and 3 seeds is thin: read differences below about 25% as noise.
- Tooling: `levylab` got env overrides for its install paths (`LEVYLAB_SRC_TML` etc.). The lab world is a fresh vanilla world (ShipWorld), not LevyTest.


---

# FOLLOW-UP (2026-11-14, branch bossbiome): why Brimstone, Plantera, Fishron and Queen Bee were enraged, and Empress of Light targeting

Lab only for the biome part (no product change); the Empress change is product code. Same army, kits, arena size, 3 seeds, cap 300 s as above. Stand-in commander caveat unchanged: the commander is an invulnerable pinned Spectator-style stand-in (Plantera runs use `spec=0`), so commander damage is 0 by construction and no real client was tested. Rows of every fight below are also appended to `results.csv` with `imm` = `FOLLOWUP_<before|after>_<night|day>_<arena>` (columns not measured are blank). Helper scripts: `scripts/enr.py`, `scripts/followup.py`, `scripts/summ.py`.

## 1. The enrage cause (OBSERVED)

The lead (levy dummies never get biome flags) is disproved. Levy already copies the commander's zone bits and modded-biome bit array onto the dummy (`BiomeMirror.Apply`, every tick), and a probe inside each boss's own AI call (`HARNESS-ENR`, once a second, dummy still in slot 255) shows the dummy carrying the right flags. What Calamity's checks read is different for each boss, and the lab arena failed them:

| boss | Calamity check (target = Main.player[NPC.target]) | what the probe saw in the old lab arena (floor tile row ~203, world 4200x1200, surface row 337, arena in the sky above the surface, x = world centre) | proper placement | enraged ticks, old arena / proper |
|---|---|---|---|---|
| Brimstone Elemental | `!target.ZoneUnderworldHeight \|\| !target.Calamity().ZoneCalamity` (BrimstoneElemental.cs:197) | dummy `crag=1` (mirrored), `uwh=0`: the harness set the Crags biome but never Underworld height, and the arena was not at that depth | Underworld depth, `a3@crag` (row 1140) | 60/60 vs 0/43 samples |
| Plantera | target above `worldSurface` (or below the Underworld) (PlanteraAI.cs:77,146) | target row 202, surface 337: above the surface | below the surface, `a3e@jungle` (row 544) | 90/90 vs 0/90 |
| Duke Fishron | target Y < 800 px, or below the surface, or x between 400 tiles of either world edge (DukeFishronAI.cs:~146). No biome flag is read | `notOceanX=1`: arena at the world centre | x within 400 tiles of an edge, `a3@ocean` (tile x 166) | 13/13 vs 0/36 samples |
| Queen Bee | boss itself above `worldSurface`, or `!target.ZoneJungle` (QueenBeeAI.cs:43-48) | `bossAboveSurface=1 noJungle=1` | underground jungle, `a3e@jungle` | 32/32 vs 0/39 samples |

The dummies stood at the soldiers' true positions (`SyncDummyTransform`), so the position-based checks were evaluated correctly; the arena was the cause. A real fight in the right place is not enraged. Other Calamity bosses found by grep that read the target's position or zones: Aquatic Scourge, Leviathan, Anahita (ocean x/y band; Sulphur zone for Aquatic Scourge), Old Duke (Underworld-depth band), Hive Mind (ZoneCorrupt), Perforator (ZoneCrimson), Desert Scourge/Nuisance (ZoneDesert), Dragonfolly and Plaguebringer Goliath (ZoneJungle), Providence (ZoneHallow/Underworld), Polterghast (ZoneDungeon or below the surface), Deerclops (ZoneSnow). They go through the same mirrored dummy, so the arena placement is what to check for any of them; not re-run here except where listed.

Harness fix found on the way: the commander's modded-biome bit array was never cleared between fights in one server process, so a Brimstone fight leaked the Crags flag into later fights (the Plantera and Fishron dummies in the first probe showed `crag=1`). It is cleared every tick now; it only affected Calamity biome flags, none of which those bosses read.

Not done, on purpose: no per-position biome scan for dummies. The mirror is proven sufficient for the enrage; a positional scan (the `ViewBiome` private-SceneMetrics technique) swaps Main.SceneMetrics and re-hands tile counts to mod systems, which risks the real player's biome music and visuals in single player. The one case it would change is a levy far from the commander in a different biome from the commander.

## 2. Empress of Light targets levies (product change)

Cause (OBSERVED): `LevyConstants.Targeting.BossSwapIncompatibleAiStyles` listed AI style 120. Her AI (and Calamity's override) reads its target only through `NPC.GetTargetData()`, which treats slot 255 as no target, so `IsAllowlisted` was false and `Compete` always kept the commander. Fix: she takes the last real player slot (254, the existing flyer slot) for one AI call, like Flying-AI enemies; `PostAI` hands back target 255. Boss-aggro unlock (first kill solo) unchanged: with the first-kill flag cleared she targeted the commander 100% and the fight went as before (d4 check, 1 fight).

Share of her attacks on levies (the harness samples the boss's target every tick): before 0% in all 24 fights, after 100% in all but a few cells (see per-fight tables). Checks: zero exceptions in the server logs of the after runs.

What this did to the fights (night, 12 fights each): before 12/12 wins, median kill 41 s; after 10/12 wins, median kill 77 s, the two losses are Master+Death seed 1 (timeout at 300 s, she switched to the commander for most of the fight in the on-tier one: 6% on levies). Daytime (LEVY_CR_DAY=1, midday, she is enraged for the whole fight): before 12/12 (median 45 s, she fought only the stand-in commander), after 0/12: all 300 s timeouts with her at 70-140% of max HP; in those fights she spent 30-50 s in phase 1 (the idle phase-switch, damage 0) per cycle against about 6 s before, and the army's damage output dropped to a third or less (e.g. 90k, 44k, 174k vs 257k-330k). I did not find why she lingers in phase 1 (inferred only: her phase-switch despawn check reads `GetTargetData().Invalid`); the daytime numbers should be read as "she is currently unbeatable by the army in daylight", not tuned. Her projectiles carry `NPC.target` (254) as ai[0]; vanilla rainbow/lance projectiles that read `Main.player[ai[0]]` would see the empty slot after the AI call. The lab shows damage landing on levies but nobody has watched her patterns in game.

## 3. Re-run, every fight

"before" = harness build 9c6bab6 (no Empress change; old surface arena). "after" = 99fd58c (Empress change; proper arena for the first four, the same surface arena for Empress). Queen Bee ran with the Calamity world's Queen Bee kit (`posteow.best`, 13 levies), 3 seeds, both modes (kit column "on" is a placeholder: one kit). Empress daytime runs: build 2a29a0c, `LEVY_EOLFLY=0` (before) vs default (after). Attacks-on-levies share is the share of ticks the boss's target was a levy.

Summary by boss (wins / fights; Brimstone, Plantera, Fishron: 12 fights each per set; Queen Bee 6; Empress night 12, day 12):

| boss | before (old arena) | after (proper arena / fixed) |
|---|---|---|
| Brimstone Elemental | 12/12, median kill 50 s, enraged 53 s mean | 12/12, median 46 s, enraged 0 s |
| Plantera | 0/12 (11 timeouts, 1 despawn), enraged 276 s mean | 7/12 (all 6 Expert+Rev and 1 Master+Death win; 5 Master+Death wipes) |
| Duke Fishron | 0/12 (wipe in 2-5 s), enraged 18 s mean | 0/12 (all 30 levies dead in every fight, first death at 5-34 s; boss HP left 54-129%) |
| Queen Bee | 6/6, median 40 s | 6/6, median 31 s |
| Empress of Light, night | 12/12, median 41 s, 0% on levies | 10/12, median 77 s, 88% on levies |
| Empress of Light, day | 12/12, median 45 s, 0% on levies | 0/12, 300 s timeouts, 15% on levies |

Reading it: the old Plantera and Brimstone numbers were rage-inflated. Plantera in her real arena is beatable on Expert+Rev and about 1 in 6 on Master+Death with this kit; Fishron stays a wipe (first death moves from 2-5 s to 5-34 s but the army loses in all 12), so his difficulty is not an enrage artifact. 

### Per-fight tables

#### Brimstone Elemental

| difficulty | kit | seed | set | arena | result | kill s | levy deaths /30 | first death s | boss HP left % (loss) | attacks on levies % | on commander % | enraged s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid | 1 | before | surface (a3) | WIN | 33.8 | 5 | 22.9 | - | 100 | 0 | 33.7 |
| Expert+Rev | mid | 2 | before | surface (a3) | WIN | 49.4 | 6 | 12.8 | - | 100 | 0 | 49.3 |
| Expert+Rev | mid | 3 | before | surface (a3) | WIN | 43.8 | 1 | 19.8 | - | 100 | 0 | 43.8 |
| Expert+Rev | mid | 1 | after | Brimstone Crags depth (a3@crag) | WIN | 35.9 | 1 | 34.2 | - | 100 | 0 | 0.0 |
| Expert+Rev | mid | 2 | after | Brimstone Crags depth (a3@crag) | WIN | 33.7 | 4 | 25.9 | - | 100 | 0 | 0.0 |
| Expert+Rev | mid | 3 | after | Brimstone Crags depth (a3@crag) | WIN | 42.9 | 1 | 31.0 | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 1 | before | surface (a3) | WIN | 31.8 | 1 | 29.8 | - | 100 | 0 | 31.8 |
| Expert+Rev | on | 2 | before | surface (a3) | WIN | 32.0 | 0 | - | - | 100 | 0 | 31.9 |
| Expert+Rev | on | 3 | before | surface (a3) | WIN | 46.2 | 1 | 13.6 | - | 100 | 0 | 46.1 |
| Expert+Rev | on | 1 | after | Brimstone Crags depth (a3@crag) | WIN | 43.7 | 2 | 12.1 | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 2 | after | Brimstone Crags depth (a3@crag) | WIN | 48.2 | 2 | 10.9 | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 3 | after | Brimstone Crags depth (a3@crag) | WIN | 32.1 | 4 | 7.0 | - | 100 | 0 | 0.0 |
| Master+Death | mid | 1 | before | surface (a3) | WIN | 79.6 | 5 | 4.3 | - | 100 | 0 | 79.6 |
| Master+Death | mid | 2 | before | surface (a3) | WIN | 73.6 | 6 | 12.0 | - | 100 | 0 | 73.6 |
| Master+Death | mid | 3 | before | surface (a3) | WIN | 67.0 | 4 | 21.0 | - | 100 | 0 | 67.0 |
| Master+Death | mid | 1 | after | Brimstone Crags depth (a3@crag) | WIN | 59.6 | 1 | 40.7 | - | 100 | 0 | 0.0 |
| Master+Death | mid | 2 | after | Brimstone Crags depth (a3@crag) | WIN | 78.2 | 3 | 25.8 | - | 100 | 0 | 0.0 |
| Master+Death | mid | 3 | after | Brimstone Crags depth (a3@crag) | WIN | 61.6 | 1 | 17.2 | - | 100 | 0 | 0.0 |
| Master+Death | on | 1 | before | surface (a3) | WIN | 72.4 | 4 | 11.0 | - | 100 | 0 | 72.4 |
| Master+Death | on | 2 | before | surface (a3) | WIN | 58.2 | 6 | 18.2 | - | 100 | 0 | 58.2 |
| Master+Death | on | 3 | before | surface (a3) | WIN | 50.8 | 5 | 8.4 | - | 100 | 0 | 50.8 |
| Master+Death | on | 1 | after | Brimstone Crags depth (a3@crag) | WIN | 62.7 | 8 | 8.5 | - | 100 | 0 | 0.0 |
| Master+Death | on | 2 | after | Brimstone Crags depth (a3@crag) | WIN | 53.9 | 7 | 8.6 | - | 100 | 0 | 0.0 |
| Master+Death | on | 3 | after | Brimstone Crags depth (a3@crag) | WIN | 43.7 | 3 | 7.5 | - | 100 | 0 | 0.0 |

Summary (wins / fights, enraged seconds mean, median kill s of wins):

| set | wins | mean enraged s | mean attacks on levies % | median kill s |
|---|---|---|---|---|
| before | 12/12 | 53 | 100 | 50 |
| after | 12/12 | 0 | 100 | 46 |

#### Plantera

| difficulty | kit | seed | set | arena | result | kill s | levy deaths /30 | first death s | boss HP left % (loss) | attacks on levies % | on commander % | enraged s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid | 1 | before | surface (a3) | TIMEOUT | - | 9 | 13.5 | 143.2 | 100 | 0 | 300.0 |
| Expert+Rev | mid | 2 | before | surface (a3) | TIMEOUT | - | 11 | 12.0 | 160.0 | 100 | 0 | 300.0 |
| Expert+Rev | mid | 3 | before | surface (a3) | TIMEOUT | - | 13 | 7.2 | 158.4 | 100 | 0 | 300.0 |
| Expert+Rev | mid | 1 | after | underground jungle box (a3e@jungle) | WIN | 64.8 | 10 | 7.8 | - | 99 | 1 | 0.0 |
| Expert+Rev | mid | 2 | after | underground jungle box (a3e@jungle) | WIN | 67.5 | 10 | 8.8 | - | 99 | 1 | 0.0 |
| Expert+Rev | mid | 3 | after | underground jungle box (a3e@jungle) | WIN | 53.6 | 7 | 8.5 | - | 99 | 1 | 0.0 |
| Expert+Rev | on | 1 | before | surface (a3) | TIMEOUT | - | 9 | 13.6 | 158.8 | 100 | 0 | 300.0 |
| Expert+Rev | on | 2 | before | surface (a3) | TIMEOUT | - | 16 | 13.0 | 160.0 | 100 | 0 | 300.0 |
| Expert+Rev | on | 3 | before | surface (a3) | DESPAWN | - | 0 | - | - | 100 | 0 | 11.4 |
| Expert+Rev | on | 1 | after | underground jungle box (a3e@jungle) | WIN | 59.7 | 7 | 16.1 | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 2 | after | underground jungle box (a3e@jungle) | WIN | 44.1 | 5 | 11.7 | - | 99 | 1 | 0.0 |
| Expert+Rev | on | 3 | after | underground jungle box (a3e@jungle) | WIN | 81.0 | 14 | 10.5 | - | 99 | 1 | 0.0 |
| Master+Death | mid | 1 | before | surface (a3) | TIMEOUT | - | 13 | 11.6 | 159.0 | 100 | 0 | 300.0 |
| Master+Death | mid | 2 | before | surface (a3) | TIMEOUT | - | 11 | 10.6 | 160.0 | 100 | 0 | 300.0 |
| Master+Death | mid | 3 | before | surface (a3) | TIMEOUT | - | 9 | 12.8 | 158.0 | 100 | 0 | 300.0 |
| Master+Death | mid | 1 | after | underground jungle box (a3e@jungle) | LOSS | - | 30 | 8.5 | 20.7 | 93 | 7 | 0.0 |
| Master+Death | mid | 2 | after | underground jungle box (a3e@jungle) | LOSS | - | 30 | 10.6 | 14.6 | 91 | 9 | 0.0 |
| Master+Death | mid | 3 | after | underground jungle box (a3e@jungle) | WIN | 89.1 | 12 | 11.5 | - | 100 | 0 | 0.0 |
| Master+Death | on | 1 | before | surface (a3) | TIMEOUT | - | 19 | 9.0 | 160.0 | 100 | 0 | 300.0 |
| Master+Death | on | 2 | before | surface (a3) | TIMEOUT | - | 7 | 16.4 | 160.0 | 100 | 0 | 300.0 |
| Master+Death | on | 3 | before | surface (a3) | TIMEOUT | - | 8 | 15.9 | 160.0 | 100 | 0 | 300.0 |
| Master+Death | on | 1 | after | underground jungle box (a3e@jungle) | LOSS | - | 30 | 8.7 | 44.6 | 89 | 11 | 0.0 |
| Master+Death | on | 2 | after | underground jungle box (a3e@jungle) | LOSS | - | 30 | 13.7 | 19.6 | 80 | 20 | 0.0 |
| Master+Death | on | 3 | after | underground jungle box (a3e@jungle) | LOSS | - | 30 | 19.4 | 9.1 | 76 | 24 | 0.0 |

Summary (wins / fights, enraged seconds mean, median kill s of wins):

| set | wins | mean enraged s | mean attacks on levies % | median kill s |
|---|---|---|---|---|
| before | 0/12 | 276 | 100 | - |
| after | 7/12 | 0 | 94 | 65 |

#### Duke Fishron

| difficulty | kit | seed | set | arena | result | kill s | levy deaths /30 | first death s | boss HP left % (loss) | attacks on levies % | on commander % | enraged s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid | 1 | before | surface (a3) | LOSS | - | 30 | 4.0 | 156.2 | 100 | 0 | 20.5 |
| Expert+Rev | mid | 2 | before | surface (a3) | LOSS | - | 30 | 4.9 | 152.3 | 100 | 0 | 28.2 |
| Expert+Rev | mid | 3 | before | surface (a3) | LOSS | - | 30 | 2.5 | 154.7 | 100 | 0 | 22.8 |
| Expert+Rev | mid | 1 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 24.9 | 68.6 | 100 | 0 | 0.0 |
| Expert+Rev | mid | 2 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 34.4 | 75.5 | 100 | 0 | 0.0 |
| Expert+Rev | mid | 3 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 20.1 | 92.4 | 100 | 0 | 0.0 |
| Expert+Rev | on | 1 | before | surface (a3) | LOSS | - | 30 | 2.5 | 151.4 | 100 | 0 | 17.3 |
| Expert+Rev | on | 2 | before | surface (a3) | LOSS | - | 30 | 3.9 | 151.2 | 100 | 0 | 15.8 |
| Expert+Rev | on | 3 | before | surface (a3) | LOSS | - | 30 | 2.5 | 147.1 | 100 | 0 | 26.0 |
| Expert+Rev | on | 1 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 25.1 | 63.4 | 100 | 0 | 0.0 |
| Expert+Rev | on | 2 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 28.8 | 53.9 | 100 | 0 | 0.0 |
| Expert+Rev | on | 3 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 22.7 | - | 100 | 0 | 0.0 |
| Master+Death | mid | 1 | before | surface (a3) | LOSS | - | 30 | 2.5 | 147.7 | 100 | 0 | 17.4 |
| Master+Death | mid | 2 | before | surface (a3) | LOSS | - | 30 | 1.9 | 153.3 | 100 | 0 | 14.1 |
| Master+Death | mid | 3 | before | surface (a3) | LOSS | - | 30 | 2.5 | 156.7 | 100 | 0 | 19.5 |
| Master+Death | mid | 1 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 14.6 | 124.4 | 100 | 0 | 0.0 |
| Master+Death | mid | 2 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 8.9 | 122.5 | 100 | 0 | 0.0 |
| Master+Death | mid | 3 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 5.4 | 126.5 | 100 | 0 | 0.0 |
| Master+Death | on | 1 | before | surface (a3) | LOSS | - | 30 | 2.5 | 151.2 | 100 | 0 | 13.5 |
| Master+Death | on | 2 | before | surface (a3) | LOSS | - | 30 | 2.5 | 151.6 | 100 | 0 | 11.5 |
| Master+Death | on | 3 | before | surface (a3) | LOSS | - | 30 | 2.5 | 151.3 | 100 | 0 | 10.8 |
| Master+Death | on | 1 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 7.1 | 105.8 | 100 | 0 | 0.0 |
| Master+Death | on | 2 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 9.7 | 128.6 | 100 | 0 | 0.0 |
| Master+Death | on | 3 | after | ocean strip (a3@ocean) | LOSS | - | 30 | 8.7 | 102.8 | 100 | 0 | 0.0 |

Summary (wins / fights, enraged seconds mean, median kill s of wins):

| set | wins | mean enraged s | mean attacks on levies % | median kill s |
|---|---|---|---|---|
| before | 0/12 | 18 | 100 | - |
| after | 0/12 | 0 | 100 | - |

#### Queen Bee

| difficulty | kit | seed | set | arena | result | kill s | levy deaths /30 | first death s | boss HP left % (loss) | attacks on levies % | on commander % | enraged s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | on | 1 | before | surface (a3) | WIN | 32.6 | 0 | - | - | 100 | 0 | 32.6 |
| Expert+Rev | on | 2 | before | surface (a3) | WIN | 32.2 | 0 | - | - | 100 | 0 | 32.2 |
| Expert+Rev | on | 3 | before | surface (a3) | WIN | 26.6 | 0 | - | - | 100 | 0 | 26.6 |
| Expert+Rev | on | 1 | after | underground jungle box (a3e@jungle) | WIN | 30.5 | 0 | - | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 2 | after | underground jungle box (a3e@jungle) | WIN | 28.6 | 0 | - | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 3 | after | underground jungle box (a3e@jungle) | WIN | 26.6 | 0 | - | - | 100 | 0 | 0.0 |
| Master+Death | on | 1 | before | surface (a3) | WIN | 53.4 | 0 | - | - | 100 | 0 | 53.4 |
| Master+Death | on | 2 | before | surface (a3) | WIN | 62.1 | 0 | - | - | 100 | 0 | 62.1 |
| Master+Death | on | 3 | before | surface (a3) | WIN | 47.5 | 0 | - | - | 100 | 0 | 47.5 |
| Master+Death | on | 1 | after | underground jungle box (a3e@jungle) | WIN | 36.0 | 0 | - | - | 100 | 0 | 0.0 |
| Master+Death | on | 2 | after | underground jungle box (a3e@jungle) | WIN | 43.2 | 0 | - | - | 100 | 0 | 0.0 |
| Master+Death | on | 3 | after | underground jungle box (a3e@jungle) | WIN | 30.7 | 0 | - | - | 100 | 0 | 0.0 |

Summary (wins / fights, enraged seconds mean, median kill s of wins):

| set | wins | mean enraged s | mean attacks on levies % | median kill s |
|---|---|---|---|---|
| before | 6/6 | 42 | 100 | 40 |
| after | 6/6 | 0 | 100 | 31 |

#### Empress of Light

| difficulty | kit | seed | set | arena | result | kill s | levy deaths /30 | first death s | boss HP left % (loss) | attacks on levies % | on commander % | enraged s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid | 1 | before | surface (a3) | WIN | 41.0 | 0 | - | - | 0 | 100 | 0.0 |
| Expert+Rev | mid | 2 | before | surface (a3) | WIN | 40.6 | 0 | - | - | 0 | 100 | 0.0 |
| Expert+Rev | mid | 3 | before | surface (a3) | WIN | 42.0 | 12 | 7.4 | - | 0 | 100 | 0.0 |
| Expert+Rev | mid | 1 | after | surface (a3) | WIN | 85.1 | 0 | - | - | 100 | 0 | 0.0 |
| Expert+Rev | mid | 2 | after | surface (a3) | WIN | 80.2 | 0 | - | - | 100 | 0 | 0.0 |
| Expert+Rev | mid | 3 | after | surface (a3) | WIN | 95.2 | 12 | 7.5 | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 1 | before | surface (a3) | WIN | 36.2 | 0 | - | - | 0 | 100 | 0.0 |
| Expert+Rev | on | 2 | before | surface (a3) | WIN | 36.3 | 0 | - | - | 0 | 100 | 0.0 |
| Expert+Rev | on | 3 | before | surface (a3) | WIN | 34.5 | 8 | 7.4 | - | 0 | 100 | 0.0 |
| Expert+Rev | on | 1 | after | surface (a3) | WIN | 71.7 | 0 | - | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 2 | after | surface (a3) | WIN | 47.0 | 0 | - | - | 100 | 0 | 0.0 |
| Expert+Rev | on | 3 | after | surface (a3) | WIN | 67.7 | 6 | 7.4 | - | 100 | 0 | 0.0 |
| Master+Death | mid | 1 | before | surface (a3) | WIN | 55.9 | 12 | 7.1 | - | 0 | 100 | 0.0 |
| Master+Death | mid | 2 | before | surface (a3) | WIN | 47.7 | 0 | - | - | 0 | 100 | 0.0 |
| Master+Death | mid | 3 | before | surface (a3) | WIN | 44.9 | 0 | - | - | 0 | 100 | 0.0 |
| Master+Death | mid | 1 | after | surface (a3) | TIMEOUT | - | 22 | 16.1 | 30.8 | 56 | 44 | 0.0 |
| Master+Death | mid | 2 | after | surface (a3) | WIN | 106.6 | 0 | - | - | 100 | 0 | 0.0 |
| Master+Death | mid | 3 | after | surface (a3) | WIN | 82.0 | 0 | - | - | 100 | 0 | 0.0 |
| Master+Death | on | 1 | before | surface (a3) | WIN | 52.8 | 14 | 7.0 | - | 0 | 100 | 0.0 |
| Master+Death | on | 2 | before | surface (a3) | WIN | 41.5 | 0 | - | - | 0 | 100 | 0.0 |
| Master+Death | on | 3 | before | surface (a3) | WIN | 40.6 | 0 | - | - | 0 | 100 | 0.0 |
| Master+Death | on | 1 | after | surface (a3) | TIMEOUT | - | 8 | 7.0 | 132.4 | 6 | 94 | 0.0 |
| Master+Death | on | 2 | after | surface (a3) | WIN | 66.6 | 0 | - | - | 100 | 0 | 0.0 |
| Master+Death | on | 3 | after | surface (a3) | WIN | 74.1 | 0 | - | - | 100 | 0 | 0.0 |

Summary (wins / fights, enraged seconds mean, median kill s of wins):

| set | wins | mean enraged s | mean attacks on levies % | median kill s |
|---|---|---|---|---|
| before | 12/12 | 0 | 0 | 41 |
| after | 10/12 | 0 | 88 | 77 |

#### Empress of Light (daytime fights)

| difficulty | kit | seed | set | arena | result | kill s | levy deaths /30 | first death s | boss HP left % (loss) | attacks on levies % | on commander % | enraged s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Expert+Rev | mid | 1 | before | surface (a3) | WIN | 45.9 | 7 | 6.8 | - | 0 | 100 | 45.9 |
| Expert+Rev | mid | 2 | before | surface (a3) | WIN | 57.1 | 16 | 6.8 | - | 0 | 100 | 57.1 |
| Expert+Rev | mid | 3 | before | surface (a3) | WIN | 45.0 | 7 | 6.8 | - | 0 | 100 | 44.9 |
| Expert+Rev | mid | 1 | after | surface (a3) | TIMEOUT | - | 27 | 6.8 | 97.6 | 38 | 62 | 300.0 |
| Expert+Rev | mid | 2 | after | surface (a3) | TIMEOUT | - | 16 | 7.1 | 139.6 | 5 | 95 | 300.0 |
| Expert+Rev | mid | 3 | after | surface (a3) | TIMEOUT | - | 28 | 7.0 | 70.8 | 52 | 48 | 300.0 |
| Expert+Rev | on | 1 | before | surface (a3) | WIN | 34.7 | 2 | 14.8 | - | 0 | 100 | 34.6 |
| Expert+Rev | on | 2 | before | surface (a3) | WIN | 37.5 | 5 | 7.2 | - | 0 | 100 | 37.5 |
| Expert+Rev | on | 3 | before | surface (a3) | WIN | 34.4 | 3 | 14.8 | - | 0 | 100 | 34.4 |
| Expert+Rev | on | 1 | after | surface (a3) | TIMEOUT | - | 21 | 7.1 | 39.2 | 44 | 56 | 300.0 |
| Expert+Rev | on | 2 | after | surface (a3) | TIMEOUT | - | 15 | 7.1 | 128.2 | 5 | 95 | 300.0 |
| Expert+Rev | on | 3 | after | surface (a3) | TIMEOUT | - | 11 | 7.2 | 136.7 | 5 | 95 | 300.0 |
| Master+Death | mid | 1 | before | surface (a3) | WIN | 75.6 | 20 | 6.6 | - | 0 | 100 | 75.6 |
| Master+Death | mid | 2 | before | surface (a3) | WIN | 65.8 | 21 | 6.7 | - | 0 | 100 | 65.8 |
| Master+Death | mid | 3 | before | surface (a3) | WIN | 64.6 | 25 | 6.6 | - | 0 | 100 | 64.5 |
| Master+Death | mid | 1 | after | surface (a3) | TIMEOUT | - | 9 | 6.3 | 140.7 | 5 | 95 | 300.0 |
| Master+Death | mid | 2 | after | surface (a3) | TIMEOUT | - | 16 | 6.5 | 134.4 | 9 | 91 | 300.0 |
| Master+Death | mid | 3 | after | surface (a3) | TIMEOUT | - | 1 | 6.5 | 140.5 | 2 | 98 | 300.0 |
| Master+Death | on | 1 | before | surface (a3) | WIN | 40.7 | 3 | 31.5 | - | 0 | 100 | 40.7 |
| Master+Death | on | 2 | before | surface (a3) | WIN | 56.7 | 22 | 6.6 | - | 0 | 100 | 56.7 |
| Master+Death | on | 3 | before | surface (a3) | WIN | 41.0 | 4 | 6.6 | - | 0 | 100 | 41.0 |
| Master+Death | on | 1 | after | surface (a3) | TIMEOUT | - | 8 | 6.3 | 136.1 | 5 | 95 | 300.0 |
| Master+Death | on | 2 | after | surface (a3) | TIMEOUT | - | 9 | 13.5 | 128.3 | 5 | 95 | 300.0 |
| Master+Death | on | 3 | after | surface (a3) | TIMEOUT | - | 9 | 6.5 | 140.3 | 5 | 95 | 300.0 |

Summary (wins / fights, enraged seconds mean, median kill s of wins):

| set | wins | mean enraged s | mean attacks on levies % | median kill s |
|---|---|---|---|---|
| before | 12/12 | 50 | 0 | 45 |
| after | 0/12 | 300 | 15 | - |

