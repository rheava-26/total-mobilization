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
