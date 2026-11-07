# Weapon rank audit (Balanced mode: price vs damage)

Trigger: "my summoners only use the Slime Staff". In Balanced mode a weapon's rank key is its shop price (value^0.7, stage discount, found boost), not its damage. Commit f56bad3 moved minion staffs onto `Score x 800`. This audit checks every other family, vanilla and Calamity, and fixes the clear cases.

Method. `Common/Testing/RankAuditDump.cs` (test only, `LEVY_RANKAUDIT=<path>`) writes one row per issuable weapon (422 vanilla, 708 Calamity): family, damage, rarity, value, Score, progression tier, found flag, and the Balanced rank at world progress 0 (pre-boss), 2 (post-EoC), 5 (post-Skeletron), 7 (early Hardmode), 12 (post-Plantera), 18 (post-Moon Lord). Data: `rank_audit_dump_before_fix.tsv` and `rank_audit_dump_after_fix.tsv` in this folder. Armory rules and deposit recency are out of the picture (fresh items, no rules).

## OBSERVED

All numbers below are from the dump (Levy 2.0.26 for "before").

### Why it happens: price is not Score in any family
Median shop copper per point of Score, crafted items only (found = no recipe, no shop): Gun 873, Magic 831, Bow 352, Broadsword 233, Spear 634, Boomerang 1320, Yoyo 1401, Whip 1611, Summon staff 1291, Sentry 2314. Within a family the spread is huge: the same Score sells for 100 copper (Pearlwood Bow, Monolith Bow/Sword: value 100 at tier 7) or 1 000 000 (Hydra, Vortexpopper), and Katana (14 Score) is priced at 100 000.

### Sentries (fixed)
- DD2 towers are flat-priced by tier whatever they do. Tier 3, all 750 000 copper: Ballistra 156 dmg (Score 401), Flameburst 88 (226), Explosive Trap 126 (324), Lightning Aura 34 (88). Tier 2, all 250 000: Ballistra 74, Explosive Trap 59, Flameburst 42, Lightning Aura 11. They tied on rank, so the pick was arbitrary.
- Priced drops beat stronger towers. Post-Plantera (stage 12): Cryogenic Staff (30 dmg, Score 71, 350 000) rank 7598 outranked DD2 Ballistra T2 (74 dmg, Score 168, 250 000) rank 5738, 3.1x out of line. Staff of the Frost Hydra (100 dmg, 1 000 000) rank 15849 over Rainbow Crystal Staff (130 dmg, 500 000) rank 9756 at stage 18.
- Post-Skeletron (stage 5): the pick was Flameburst T1 (17 dmg, Score 35), the best was Ballistra T1 (30 dmg, Score 62): pick/best Score 0.57.
- Pre-boss/post-EoC: Caustic Croaker Staff (8 dmg) outranked Polyp Launcher (12 dmg).
- Before the fix the sentry picked was not the best by Score at any of the five stages with enough sentries (2, 5, 7, 12, 18).

### Whips (fixed)
Vanilla whips sit in fixed price bands. Leather Whip (14 dmg, Score 28) ranks 3131, over Bone Whip (27 dmg, Score 57) at 2585 and Thorn Whip (18 dmg, 1946): 2.4x out of line, at the stage a summoner is actually choosing (post-Skeletron the pick was the Leather Whip, best 0.50 of Score). Later stages (Fire, Sword, Maces) happened to line up. Calamity registers no Whip-family weapons.

### Not fixed: broad price/Score disagreement in the other families
Worst per family, with numbers (rank vs Score at the stated stage, among weapons the world has reached):

| Family | Stage | Outranking item | Outranked (stronger) item | Out of line |
|---|---|---|---|---|
| Broadsword | post-EoC | Enchanted Sword 23 dmg, Score 81, 150 000 -> rank 4199 | Tax Collector's Stick of Doom 80 dmg, Score 622, rank 201 | 160x |
| Broadsword | post-Plantera | Seedler 45 dmg, Score 359, 500 000 -> 10927 | Excalibur 170 dmg, Score 772, 230 000 -> 812 | 29x |
| Broadsword | post-ML | Swordsplosion 48 dmg, Score 365, 1 200 000 -> 18006 | DD2 Betsy's Sword 150 dmg, Score 1526 -> 6571 | 11x |
| Broadsword | pre-boss | Sickle 9 dmg priced 6000 -> 66 | Ash Wood Sword 13 dmg priced 100 -> 4 | 34x |
| Gun | post-ML | Onyx Chain Blaster Score 518, 1 200 000 -> 18006 | SDMG Score 2092, 750 000 -> 14513 | 5x |
| Gun | post-ML | Conference Call 53 dmg, Score 166, 1 000 000 | Astral Blaster 150 dmg, Score 1137, 800 000 | 8x |
| Gun | post-EoC | Snowball Cannon 10 dmg (100 000, found) -> 3507 | Sandgun 30 dmg, Score 115, 10 000 -> 631 | 16x |
| Bow | post-Plantera | Daedalus Stormbow 30 dmg, Score 200 -> 9343 | Marrow 60 dmg, Score 327 -> 1208 | 13x |
| Bow | early HM | Calamity Monolith Bow 19 dmg, value 100 -> 25 | any 100 000 bow | 100-375x |
| Magic | post-ML | Celestial Reaper Score 520, 3 000 000 -> 34197 | Augur of the Void Score 4986, 1 200 000 -> 18006 | 18x |
| Magic | post-Plantera | Crystal Vile Shard Score 97, 400 000 -> 8931 | Book Staff Score 514, 50 000 -> 2174 | 22x |
| Magic | post-EoC | Vilethorn Score 27, 75 000 -> 2560 | Icicle Staff Score 105, 10 000 -> 625 | 16x |
| Spear | early HM | Dark Lance 34 dmg, Score 128 -> 3956 | Cobalt Naginata 60 dmg, Score 570, 45 000 -> 1808 | 10x |
| Boomerang | post-Skeletron | Trimarang Score 70, 100 000 -> 3161 | Anchor 107 dmg, Score 351, 50 000 -> 2179 | 7x |
| Held | post-EoC | Terragrim 13 dmg, 250 000 -> 6003 | Basher 65 dmg -> 1025 | 14x |
| Flail | post-EoC | Mace 9 dmg, 100 000 -> 3507 | Ball O' Hurt 15 dmg, 75 000 -> 2560 | 2.3x |
| Yoyo | early HM | Jungle Yoyo Score 47 -> 2235 | Air Spinner Score 87 -> 1860 | 2.2x |

Pattern: priced drops and boss/biome items (Enchanted Sword, Katana, Terragrim, Mace, Snowball Cannon, Seedler, Stormbow, Celestial Reaper) outrank stronger crafted weapons; cheap-priced wood/Calamity items (value 100) rank at the floor. Offenders per stage per family run from 0 (Yoyo, Shortsword) to 43 (Magic post-Plantera).

Score itself is not trustworthy in every case, so some of these rows may be Score errors rather than price errors: P90 (6 dmg, Score 702), Roxcalibur (held, Score 124 at 180 dmg), Murasama (dmg 2200, Score 52062), dynamite family (damage -1), Tax Collector's Stick of Doom (its availability tier is unreliable). That is why these families are proposal only.

## INFERRED (not measured)
- Summoners that hold both a staff and a sentry or whip pick each by rank; with the staff on Score and the others on price the staff was effectively being compared to a different scale. Not measured with armory scenarios.
- Dropping sentries from ~2314 to 800 copper per Score lowers their absolute rank about 2.9x. Sentries compete only against other Summon-class kit, and staffs now use the same 800, so the relative order within a summoner's kit improves; I did not run a full armory scenario for a staff-plus-sentry kit.
- The price modes are the player's choice; nothing in this audit touches them.

## FIXED
`WeaponRank.ScoreRanked` now maps three families to Score x copper-per-score in Balanced mode only:

| Family | Constant | Before | After |
|---|---|---|---|
| Minion staff | `SummonRankCopperPerScore` 800 (unchanged) | n/a | n/a |
| Sentry | `SentryRankCopperPerScore` 800 | key = shop price | key = Score x 800 |
| Whip | `WhipRankCopperPerScore` 800 | key = shop price | key = Score x 800 |

Calibration: 800 is the median shop price per Score point of crafted guns (873) and magic weapons (831), and is the staff constant, so a summoner's staff, sentry and whip sit on one footing with the main classes. Sentries and whips would sit at their own medians (2314, 1611) if we matched their price spread, but that would put every sentry far above a gun of equal Score, so I used the shared value. Stage discount, found boost and recency still apply on top.

After the fix (Balanced, dump after_fix): the sentry pick is the best by Score at stages 2, 5, 7 and 12 (before: wrong at all five) and 0.89 of best at stage 18 (Rainbow Crystal Staff, held back only by the 1.12 found boost, under the 1.15 swap margin). Whip pick is the best by Score at every stage (before: Leather Whip over Bone Whip post-Skeletron). Staff picks are still the best by Score at every stage.

Self-check: five new assertions in `WeaponRankCheck.CheckBalancedRanks`: equal-priced DD2 tier-3 towers order by damage (Ballistra, Flameburst over Lightning Aura, each > 1.15x); Rainbow Crystal Staff over Frost Hydra; Ballistra T3 over Queen Spider Staff; Bone Whip over Leather Whip and Thorn Whip; and the SellPrice mode still ranks Leather Whip and Lightning Aura on price alone. Boot: "325 checks OK", zero `xception|TypeLoad|MissingMethod|malformed` lines.

## PROPOSED (not applied)
Broad. A fix like the staff's for Gun, Bow, Magic, Broadsword, Spear, Boomerang, Yoyo, Held and Flail would change every armory pick in the game, and Score has its own gaps (above). Options, in order of cost:

1. Score-rank only items whose price is meaningless: the found/drop set (`ProgressionTier.IsFound`) plus value <= 100 outliers, at the family's crafted median copper per Score (Gun 870, Magic 830, Bow 350, Broadsword 230, Spear 630, Boomerang 1300, Yoyo 1400, Flail 2400, Held 2200). That targets Enchanted Sword, Katana, Terragrim, Mace, Snowball Cannon, Seedler, Stormbow, and lifts Monolith/Pearlwood items off the floor, while crafted weapons keep their price rank.
2. Cap the price term: key = min(price, K x Score) with K = 3 x the family median, so a priced drop cannot beat a crafted weapon of 1/3 its Score. Cheap items are untouched.
3. Fix Score first for P90, Roxcalibur, Murasama and the dynamite family, then re-run the dump and measure offenders per stage (current counts are in the tables above).

I recommend 1 then 2, behind the same `ScoreRanked`-style switch, each validated by re-running `RankAuditDump` and the gear progression audit before shipping.

## Reproduce
Private toolchain, Calamity enabled: build, then `LEVY_RANKAUDIT=/path/dump.tsv dotnet tModLoader.dll -server -nosteam -tmlsavedirectory <save> -world <world>`; the dump is written about 10 seconds after the world loads. `LEVY_RANKAUDIT_MODS=CalamityMod,ThoriumMod` adds mods.
