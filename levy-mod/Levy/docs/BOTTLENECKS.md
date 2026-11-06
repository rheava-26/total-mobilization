# Levy: army-scale progression bottlenecks (Terraria 1.4.4)

Sources: terraria.wiki.gg pages (fetched 2026-11-06 via curl; rendered pages saved in
`bottlenecks-raw/`), plus a few calamitymod.wiki.gg / thoriummod.wiki.gg pages. Wiki page name is in
[brackets] next to each key number. "Calc" = my arithmetic from cited numbers, not a wiki figure.
Unless stated, drop numbers are the wiki's Classic table; Expert/Master numbers are noted where the page shows them.
Unknown to me: how Levy applies defense to NPC soldiers. Everything about "does defense matter" assumes
the vanilla formula applies to them.

"Set" = full armor (all pieces, one headpiece where the set has variants).

## Pre-hardmode

| Item | Gates | Cost / source (cited) | Pain at 20 copies | Candidate fix |
|---|---|---|---|---|
| Copper/Tin | Copper (6 def) / Tin (7 def) sets | 48 bars = 144 ore per set [Copper_armor, Tin_armor] | Trivial: 2,880 ore for 20 sets | none needed |
| Iron/Lead | Iron (9) / Lead (11) | 60 bars = 180 ore [Iron_armor, Lead_armor] | Trivial | none |
| Silver/Tungsten | Silver (13) / Tungsten (15) | 60 bars = 240 ore [Silver_armor, Tungsten_armor] | Low: 4,800 ore for 20 sets | none |
| Gold/Platinum | Gold (16) / Platinum (20) | 75 bars = 300 ore [Gold_armor, Platinum_armor]; Platinum "can be obtained from Crates or the Extractinator" in Gold worlds | Medium: 6,000 ore for 20 sets, and only one of Gold/Platinum generates per world | Bulk recipe; Platinum<->Gold shimmer conversion (Shimmer page lists Copper/Tin/Iron/Lead ore transmutations, so ore swap is an existing mechanic) |
| Demonite / Crimtane | Shadow (19) / Crimson (19) | 60 bars (180 ore) + 45 Shadow Scales or 45 Tissue Samples per set [Shadow_armor, Crimson_armor]. Scales/Samples are boss/enemy drops | Medium-high: scales/samples are the gate, 900 for 20 sets (calc) | Scale/Sample bulk recipe (e.g. 3 Bars -> 1 Scale); drop multiplier |
| Meteorite | Meteor armor (16), mage only | 45 bars = 135 ore [Meteor_armor] | Low | none |
| Hellstone | Molten (25 def, +10% melee) | 45 bars = 135 Hellstone + 45 Obsidian [Molten_armor] | Low-medium: hazardous Underworld mining but cheap in bars | none. Best pre-HM armor and cheapest of the top tier |
| Bee Wax | Bee armor (13 def, +10% summon) | 30 wax per set [Bee_armor]; Queen Bee drops 16-26 (bag 17-30); Beenades can be shimmer-decrafted for more wax [Bee_Wax] | Medium: ~1.4 Queen Bee kills per set (calc). Beenade shimmer already a built-in loop | Wax drop multiplier (config) |
| Jungle Spores / Stingers / Vines | Jungle armor (17 def, -16% mana), magic only | 32 Spores + 2 Vines + 10 Stingers per set [Jungle_armor]; Hornet drops Stinger 66%, Moss Hornet 16.67%, Spiked Jungle Slime 50% [Stinger] | Medium: 640 Spores for 20 sets (calc); Spores are an herb-style pickup | Spore bulk recipe |
| Bones + Cobwebs | Necro armor (19 def, +10% ranged crit), post-Skeletron | 150 Bones + 135 Cobwebs per set [Necro_armor] | Medium: 3,000 Bones (calc) | Bone/web bulk recipe |
| Ninja armor | Ninja (9 def, +20% move speed); not a real power gate | King Slime drops each piece separately, 1/3 (33.33%) each per kill; bag gives two pieces in Expert [Ninja_armor] | Medium: ~9 kills per set on average (calc), ~180 kills for 20 sets | Guaranteed-piece recipe; drop multiplier |
| Mimic-pool weapons/accessories | Hermes Boots, Cloud in a Bottle, Band of Regeneration, Magic Mirror, Dual Hook, Philosopher's Stone, Titan Glove, Star Cloak, Cross Necklace, Frostbrand, Flower of Frost, etc. | Mimic variants roll from a pool: 16.67% per item in the Dungeon/Hardmode pools shown, 23.75% for the ice pool items [Mimic] | High for accessories: one item per mimic kill, and mimics are rare spawns | Mimic drop multiplier (config); craft recipes for the common ones |

## Early hardmode (first mech boss onward)

| Item | Gates | Cost / source | Pain at 20 copies | Candidate fix |
|---|---|---|---|---|
| Cobalt/Palladium | set, 45 bars = 135 ore [Cobalt_armor] | Mining | Low | none |
| Mythril/Orichalcum | 45 bars = 180 ore [Mythril_armor] | Mining | Low | none |
| Adamantite/Titanium | Adamantite 54 bars = 216 ore; Titanium 59 bars = 236 ore [Adamantite_armor, Titanium_armor] | Mining | Low-medium (4,320+ ore for 20 sets, calc) | Bulk recipe |
| **Hallowed Bar** | Hallowed armor, 54 bars per set (all six pieces: 90) [Hallowed_armor]. Also Excalibur-line and many weapons | Dropped ONLY by the three mech bosses; each drops 15-30 per kill, 20-35 in the Expert/Master table [The_Twins, The_Destroyer, Skeletron_Prime]. No crafting recipe, no ore [Hallowed_Bar] | **Worst early-HM bottleneck.** 20 sets = 1,080 bars (calc) = ~48 boss kills at 22.5 average (Classic), ~39 at 27.5 (Expert) | Bulk recipe (e.g. Adamantite/Titanium Bars + Souls -> Hallowed Bars), config toggle; boss drop multiplier (config) |
| Souls of Might / Sight / Fright | Mech-boss weapons, wings, accessories | 25-40 per boss kill, from the same three bosses [Soul_of_Might, The_Twins]. All Might uses need 63 total [Soul_of_Might] | Medium: tied to same boss kills as Hallowed Bars | Same multiplier |
| Soul of Light | Frost Core, Forbidden Fragment, others | 20% (1/5) from most enemies in Underground Hallow [Soul_of_Light] | Low-medium: farmable | none |
| Soul of Flight | Wings, Soul of Flight in a Bottle | Wyverns only: 5-10 normally, 10-20 in the second table [Soul_of_Flight]; Wyverns spawn in Space with special conditions [Wyvern] | High for wing-equipped levies: Space farm | Flight-soul recipe or drop multiplier |
| Frost armor | Frost (43 def, +10% melee/ranged dmg) | 3 Frost Cores + 46 Adamantite/Titanium Bars (184 ore) [Frost_armor] | Medium: Frost Cores come from Souls of Light-type material | Core recipe |
| Cursed Flames / Ichor | Flamethrower/Ichor weapons and ammo | Clinger/World Feeder 100% (2-5); Ichor Sticker 100% (2-5); Ghouls 33.3% (1-3) [Cursed_Flame, Ichor] | Low-medium: biome-specific | none |
| Pixie Dust / Unicorn Horn | Crystal and Hallowed items | Pixie 1-3 at 100%; Unicorn 1 at 100% [Pixie_Dust, Unicorn_Horn] | Low | none |

## Post-mechs (Chlorophyte tier)

| Item | Gates | Cost / source | Pain at 20 copies | Candidate fix |
|---|---|---|---|---|
| **Chlorophyte Ore/Bar** | Chlorophyte armor (54 bars = 270 ore per set; 90 bars = 450 ore for all six pieces) [Chlorophyte_armor]; Turtle, Beetle, Spectre, Shroomite all consume it | Bar = 5 ore [Chlorophyte_Bar]. Growth: each random tile tick on Jungle grass has 1/300 (0.33%) chance to try spreading within a 21x21 area; spread onto adjacent mud 2/3 per attempt, capped by nearby-ore limits (e.g. 40 ore in a 71x71 area, 130 in a 171x171 area, cavern layer and below) [Chlorophyte_Ore]. Can be farmed; Chlorophyte Bricks spread ore and do not count toward the cap | 20 sets = 5,400 ore (calc); the cap on density, not the spread chance, is the real limit | Raise or remove density caps (config); faster spread chance (config); mud/brick farm recipe |
| Turtle armor | 65 def melee set | 54 Chlorophyte Bars + 3 Turtle Shells; shell is 1/12 (8.33%) per Giant Tortoise kill, ~65 kills for 90% to get 3 [Turtle_armor, Turtle_Shell] | High: ~36 kills per set average (calc), 720 for 20 sets | Shell drop multiplier (config) |
| Autohammer / Shroomite | Shroomite armor (51 def, ranged) | Autohammer sold by Truffle after Plantera; recipe 1 Chlorophyte Bar + 15 Glowing Mushrooms -> 1 Shroomite Bar [Autohammer, Shroomite_Bar]; set = 54 bars = 810 Glowing Mushrooms + 270 Chlorophyte Ore [Shroomite_armor] | **High**: 20 sets = 16,200 mushrooms and 5,400 ore (calc) | Mushroom per bar down to 5 (config); mushroom farm recipe |

## Post-Plantera / Golem

| Item | Gates | Cost / source | Pain at 20 copies | Candidate fix |
|---|---|---|---|---|
| **Ectoplasm -> Spectre Bar** | Spectre armor (magic) and Spectre tools | Ratio is 1 Chlorophyte Bar + 0.5 Ectoplasm per Spectre Bar (the rendered recipe table loses quantities; ratio taken from the armor page totals) [Spectre_Bar, Spectre_armor]. Set = 54 Spectre Bars (27 Ectoplasm and 270 Chlorophyte Ore) [Spectre_armor]. Dungeon Spirit spawns from a dying Dungeon enemy at 1/13 (7.69%), 1/9 (11.11%) in Expert; drops 1-2 Ectoplasm at 100% [Dungeon_Spirit, Ectoplasm] | Very high: 27 Ectoplasm = ~18 spirits = ~234 Dungeon kills per set in Classic (calc); 20 sets ~4,700 kills | Spirit spawn chance multiplier (config); Ectoplasm-per-bar reduction; Water-Candle/Piranha Gun farm already in wiki |
| Beetle Husk | Beetle armor, post-Golem | Golem drops 4-8, bag 18-23 [Beetle_Husk]; set = 18 husks + a full Turtle armor set (54 bars, 3 shells) [Beetle_armor] | Medium-high: 360 husks for 20 sets (calc) and the Turtle half is the real cost | Husk multiplier; drop Turtle-set requirement |

## Endgame (Moon Lord)

| Item | Gates | Cost / source | Pain at 20 copies | Candidate fix |
|---|---|---|---|---|
| Lunar Fragments | Solar/Vortex/Nebula/Stardust armor: 36 Luminite Bars (144 Luminite) + 45 fragments of one type per set [Solar_Flare_armor, Vortex_armor, Nebula_armor, Stardust_armor] | Each pillar drops 12-20 stacks of fragments: 12-60 per pillar Classic, 24-100 Expert [Solar_Pillar]. Pillars appear only in the Lunar Event | High: 900 fragments of one type for 20 sets (calc); ~25 full Lunar Events at 36 average (calc) | Fragment multiplier (config); recipe from Luminite |
| Luminite | all lunar gear | Moon Lord drops 70-90 (90-110 Expert) [Moon_Lord]; 4 Luminite = 1 bar (144 ore for 36 bars) | Very high: 20 sets = 2,880 Luminite = ~36 Moon Lord kills at 80 average (calc) | Luminite multiplier (config); Luminite Bar bulk recipe |
| Calamity / Thorium | Not a full audit | Calamity: Auric Bar needs 50 Auric Ore + 1 Yharon Soul Fragment for 5 bars [calamity Auric_Bar]; Perennial/Cryonic bars: 4 ore per bar, also from Geodes [calamity Perennial_Bar, Cryonic_Bar]. Thorium Bars drop from crates at low rates (Strange Crate 25%, Wondrous Crate 12.5% / 8%) [thorium Thorium_Bar] | Calamity endgame is boss-fragment gated: Yharon fragment per 5 bars | Same pattern: per-boss material multiplier |

## Pre-hardmode ore armor, honest assessment

Set defense includes set bonus, from each page's infobox:

| Set | Defense (base + bonus) | Bars | Source |
|---|---|---|---|
| Copper | 6 (4+2) | 48 | Copper_armor |
| Tin | 7 (5+2) | 48 | Tin_armor |
| Iron | 9 (7+2) | 60 | Iron_armor |
| Lead | 11 (8+3) | 60 | Lead_armor |
| Silver | 13 (10+3) | 60 | Silver_armor |
| Tungsten | 15 (12+3) | 60 | Tungsten_armor |
| Gold | 16 (13+3) | 75 | Gold_armor |
| Platinum | 20 (16+4) | 75 | Platinum_armor |
| (Molten, for comparison) | 25, +10% melee, fire immunity | 45 hellstone bars | Molten_armor |

No ore set has any non-defense bonus; the set bonus is plain defense only.
Defense formula: flat reduction of defense x 0.5 per hit in Classic, x 0.75 in Expert, x 1 in Master/Legendary [Defense].
- Copper (6) vs Platinum (20): 14 defense apart = 7 damage per hit in Classic, 10.5 in Expert, 14 in Master.
- For a 100-200 HP levy, 7 damage per hit is a real difference only against weak hits: a 15 damage hit becomes 8 (about 47% less) at Platinum vs 12 at Copper in Classic.
  Against boss-level hits (40+), the gap is small percentage-wise, and Copper's 6 defense shaves very little.
- Honest verdict: the ore ladder is a smooth, small curve. Platinum is genuinely useful for a pre-Hardmode
  army; Copper/Tin/Iron/Lead are not worth 48-60 bars per soldier. Hellstone (Molten, 25 defense in 45 bars) beats every ore set and is cheaper than Gold/Platinum (75 bars),
  so the whole upper ore tier is dominated by it.
What would make lower ores worth using in quantity (all config toggles):
1. A "Militia Kit" recipe: a cheap bulk recipe (e.g. a handful of bars of any ore tier) for a basic uniform that gives a levy a small flat defense, so 60 soldiers are issued for hundreds of ore rather than thousands.
2. Ore -> ammo conversion (cheap arrows/bullets by ore tier) so low ores feed the army's ranged output.
3. Treat any bar tier as an interchangeable input to a single bulk "issue gear" recipe, scaling defense by tier.
4. Shimmer-style ore swap: the Shimmer page lists ore-to-ore transmutations, so Gold<->Platinum and Copper<->Tin already have a precedent to extend.

## Best candidate fixes, ranked
1. Hallowed Bar bulk recipe from Adamantite/Titanium plus Souls, plus a boss-drop multiplier (config).
2. Spectre: Ectoplasm-per-bar reduction and a Dungeon Spirit spawn multiplier (config).
3. Chlorophyte: raise or remove the nearby-ore density cap (config), plus a faster spread chance.
4. Shroomite: Glowing Mushrooms per bar 15 -> fewer (config).
5. Turtle Shell drop multiplier (config), since each Beetle/Turtle set needs 3 shells at 1/12.
6. Lunar Fragment and Luminite multipliers, or a Luminite->fragment recipe (config).
7. Militia Kit and ore->ammo conversion for the ore ladder.

Raw pages: /tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/bottlenecks-raw/
