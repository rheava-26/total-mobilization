# Army-scale gear gaps: independent second opinion

Scope: where getting 10-30 copies of class gear gets disproportionately painful, per stage, for (a) vanilla, (b) vanilla + Calamity, (c) vanilla + Calamity + Thorium. No existing Levy docs were read. No code was changed.

## 0. Method, sources, and honesty notes

**Sources**
- Vanilla: terraria.wiki.gg. Recipes and drops come from its Cargo tables (`api.php?action=cargoquery`, tables `Recipes` and `Drops`), plus `?action=raw` pages. Pages cited as `[V:Page]`.
- Calamity: read straight from `/home/user/tml-save-2026.10/ModSources/CalamityMod`.
  - `Items/Armor/*` and `Items/Weapons/*` give the recipes.
  - NPC `ModifyNPCLoot`, `Items/TreasureBags/*` and `Utilities/DropHelper.cs` give the drops.
  - Cited as `[C:path:line]`.
  - Source tag meaning, following the code layout: `normalOnly.Add(...)` lines are Classic drops. `TreasureBags/*.cs` lines are the Expert+ bag amount.
- Thorium: thoriummod.wiki.gg. Cargo gives `Recipes` only, not drops. Drop numbers come from `?action=raw` item infoboxes. Cited as `[T:Page]`.
  - Thorium boss drop rates for most weapons and accessories were NOT gathered. Thorium coverage is therefore armor, bars, essences and fragments, not full weapon and accessory drop tables.

**Conventions**
- `A-B` means a uniform quantity range. The average is (A+B)/2.
- "Per set" means head + body + legs. Many Calamity heads are class-specific but cost the same across classes, so a set costs the same for every class.
- N = 20 soldiers is the working example. The middle of the player's 10-30 range scales linearly.
- Kill counts assume 100% drop unless stated.

**Things I could not fully verify (treat as estimates)**
- Lunar pillar fragment average: the wiki says "12-20 stacks, each 1-3 in Classic, 12-60 total; Expert 24-100" [V:Celestial_Pillars]. I use the derived averages 32 (Classic) and 56 (Expert). The Expert stack size is inferred as 2-5.
- Pumpkin Moon Mourning Wood count per run: the wiki gives points per kill (MW = 75 Classic) and 3,410 points to reach wave 15 [V:Pumpkin_Moon]. "About 10-15 MW per wave-15 run" is my estimate.
- Spectre Bar recipe: Cargo is ambiguous. Item-page alternates show a Spectre set equals 1 Chlorophyte Bar + 1 Ectoplasm per bar. I use that.
- Calamity "NO RECIPE" counts were found by scanning each item file for `CreateRecipe`. They can undercount recipes registered centrally in `Systems/Mechanic/RecipeSystem.cs`. Read them as "no per-item recipe", approximately "drop or shop only".
- NPC soldiers may count differently from players for `PerPlayer` loot. I did not test this.

---

## 1. Ranked list of top issues

Rank is by disproportion: how much worse it is at 20 copies than at 1, and how few substitutes exist.

| # | Issue | Setup | Key numbers |
|---|---|---|---|
| 1 | **Auric Ore is non-renewable.** It is placed once at first Yharon kill and has no drop source. | b, c | 450 ore per Auric set (45 bars at 10 ore each). Demonshade needs the same 45 Auric Bars. N=20 is 9,000 ore. |
| 2 | **Lunar tier: Luminite + 4 fragment types, with the Celestial Sigil taxing every Moon Lord kill.** | a, b, c | 36 bars (144 ore) + 45 fragments per set. N=20 needs about 36 Classic Moon Lord kills. Sigils cost 48 fragments per kill. Thorium adds three more fragment types. |
| 3 | **Hallowed Bar** is not craftable, buyable or shimmerable. Only the 3 mech bosses drop it. | a, b, c | 54 per vanilla set. N=20 is 1,080 bars, or about 16 Classic (13 Expert) full mech cycles. Thorium adds 43 recipes that use it. |
| 4 | **Summoner mid-game wall: Spooky Wood.** | a, b, c | 750 wood per set. N=20 is 15,000 wood, about 667 Classic Mourning Wood kills. Calamity's Fearmonger needs a full Spooky set per copy. |
| 5 | **Ranger wall: Shroomite.** Every bar needs 15 Glowing Mushrooms. | a, b, c | 54 bars = 810 mushrooms + 54 Chlorophyte Bars per set. N=20 is 16,200 mushrooms and 5,400 Chlorophyte Ore. |
| 6 | **Melee wall: Beetle armor** needs Golem husks plus a Turtle set. | a, b, c | 18 husks + 3 Turtle Shells per set. Shells drop at 8.33%, so 36 Giant Tortoise kills per set. N=20 needs 720 tortoise kills. |
| 7 | **Class emblems come only from the Wall of Flesh**, with no craft or shop route. | a, b, c | Vanilla gives exactly 1 per kill. Calamity gives about 1.49 per kill. Thorium's 3 emblems each drop at 12.5% (Classic) or 25% (Expert bag), so 0.375 or 0.75 per kill, on a separate shimmer cycle. |
| 8 | **Drop-only Calamity weapons and accessories.** Many have no recipe, and class coverage is uneven. | b, c | Summon has 50 of 102 weapons drop-only (49%). Rogue 61/148 (41%), magic 51/145, melee 56/169, ranged 51/164. Accessories 109/227. Per-copy drop rate is 1/4 (Classic) or 1/3 (bag). |
| 9 | **Ascendant Spirit Essence** sits behind three separate night events, and it is the key to post-DoG sets and accessories. | b, c | 1 AE = 5 Nightmare Fuel + 5 Endothermic Energy + 2 Darksun + 2 Necroplasm. GodSlayer and Silva need 9 AE per set. Fearmonger needs 7 AE plus a 750-wood Spooky set. |
| 10 | **Finite or low-renewal Calamity ores** at the tiers between Plantera and Providence. | b, c | Aerialite 135 ore per set (cloud islands only). Cryonic 216 ore and Perennial 180 ore per set, supplied only by one-time world gen plus rare slimes. Scoria 180 ore per set, Abyss-only. |
| 11 | **Ectoplasm + Chlorophyte** for Spectre, and Thorium makes it worse. | a, b, c | 54 Ecto + 54 Chlo Bars per set. Thorium's Terrarium Core uses 0.5 Hallowed, 0.5 Chlo and 0.5 Ecto per core. Armor takes 36 cores. |
| 12 | **Healer, bard, thrower and rogue have no vanilla gear. Their emblems are separate drops.** | b, c | Thorium fills bard and healer armor, but the endgame sets compete for Luminite and the shared pillar fragments. |

---

## 2. Issue write-ups with arithmetic, citations and general fixes

### Issue 1. Auric Ore is a one-time, non-renewable pool (Calamity)

**Evidence**
- `Yharon.OnKill` calls `SpawnOre(AuricOre, 2E-05, 0.75f, 0.9f, 10, 20)` only `if (!DownedBossSystem.downedYharon)` [C:NPCs/Yharon/Yharon.cs:3033-3035].
- No NPC drops Auric Ore. The only other path is the Greed Pot conversion `Uelibloom -> Auric` once Calamitas or the Exo Mechs are down [C:Tiles/GreedPotTile.cs:228].
- Shimmer runs the other way only: Auric -> Uelibloom -> Exodium -> Luminite -> Chlorophyte Ore [C:Items/CalamityGlobalItemTweaks.cs:1206-1211; V:Shimmer].
- Auric Bar: 50 ore + 1 Yharon Soul Fragment makes 5 bars [C:Items/Materials/AuricBar via calrec].
- Auric armor: head 10 + body 20 + legs 15 = 45 bars. Demonshade: 12 + 18 + 15 = 45 Shadowspec Bars, each needing 1 Auric Bar + 1 Exo Prism + 1 Ashes of Annihilation [C:Items/Armor/Demonshade].
- Veins: 20,160,000 tiles (Large) x 2E-05 = 403 vein attempts, 230 (Medium), 101 (Small). Tiles per vein (OreRunner strength 10-20) is not quantified here.
- Demand breadth: 17 Calamity weapons use Auric Bars, plus Rampart of Deities, Seraph Tracers and the Auric armor [C:calwep scan].

**Army arithmetic**
- Auric set = 45 bars = 450 ore + 9 Yharon fragments. N=20 is 9,000 ore.
- The Auric set also nests GodSlayer or Silva, Bloodflare and Tarragon pieces, so it needs all four. See the Calamity table.

**General fixes**
- Make the late ore farmable. Examples: a post-first-kill source such as ore-bearing enemies or biome growth, as already exists for Perennial/Cryonic/Uelibloom slimes (10-26 ore at 100% [C:NPCs/NormalNPCs/BloomSlime.cs:82]).
- Or add a "refine upward" path. Shimmer only goes downward today. A lossy upward conversion (for example 4 Uelibloom -> 1 Auric) would give a renewable ladder without touching the one-off boss loot.

### Issue 2. The Lunar tier and the Sigil tax (vanilla, stacked by Calamity and Thorium)

**Evidence**
- Lunar set: 36 Luminite Bars + 45 fragments of one type. Vortex: 8+16+12 bars, 10+20+15 fragments [V:Recipes Cargo]. Solar is identical.
- Luminite Bar = 4 Luminite Ore [V:Luminite_Bar]. A set is 144 ore.
- Moon Lord drops 70-90 Luminite (Classic) or 90-110 (bag) [V:Luminite Drops].
- The Celestial Sigil costs 12 of each fragment type, 48 total [V:Cargo Celestial Sigil].
- Pillars drop 12-60 (Classic) or 24-100 (Expert) fragments each. The shield kill requirement falls from 100 to 50 after Moon Lord [V:Celestial_Pillars].
- The Cultists respawn when all four pillars are destroyed, so the event can be repeated [V:Cultists].
- Thorium adds three more fragment types from the same pillars: Shooting Star (bard), Celestial (healer), White Dwarf (thrower), 3-15 (Classic) or 6-25 (Expert) per tower [T:Celestial_Fragment, Shooting_Star_Fragment, White_Dwarf_Fragment]. Each set is 36 Luminite Bars + 45 fragments.

**Army arithmetic (N = 20 lunar sets)**
- Luminite: 20 x 144 = 2,880 ore. Classic avg 80 per kill gives 36 kills. Expert bag avg 100 gives 28.8 kills.
- Sigil tax: 36 kills x 48 = 1,728 fragments. Expert: 29 x 48 = 1,392.
- Set fragments: 20 x 45 = 900.
- Total about 2,628 fragments. At 128 per Classic pillar cycle (4 x 32) that is about 20.5 cycles. At 224 per Expert cycle it is about 10.2 cycles.
- Calamity adds more Lunar Bar and fragment sinks. 30 weapons use Lunar Bar, and GemTech takes 14 of each vanilla fragment per set [C:calwep, Items/Armor/GemTech].

**General fixes**
- A conversion station: fragment type A to B at a lossy ratio, or fragments to Luminite.
- Tie loot to army size or deployment, so more soldiers engaged increases the yield of the shared drops (pillar fragments, Luminite).
- Lower the Sigil's fragment cost when it is crafted in bulk, or let one Sigil summon scale with party count.

### Issue 3. Hallowed Bar (the hard universal gate)

**Evidence**
- Skeletron Prime / Destroyer / Twins each drop 15-30 Hallowed Bars (Classic) and 20-35 in the Expert bag [V:Drops cargo Hallowed Bar].
- There is no craft recipe, no shop and no shimmer route. The wiki trivia notes Hallowed Bars have no matching ore [V:Hallowed_Bar].
- Set: Helmet/Mask/Headgear 12 + Plate Mail 24 + Greaves 18 = 54 [V:Recipes Cargo].
- Thorium: Cyber Punk 24 + Biotech 36 Hallowed Bars [T:Bard_Armor, Healer_Armor]. 43 Thorium recipes use Hallowed Bar [T:Cargo count].
- Calamity: Umbraphile uses 8 + 15 + 11 = 34. The Early Hardmode rework config makes the bars drop only after all three mechs are down [C:Utilities/DropHelper.cs:774-780].

**Army arithmetic**
- Full cycle (3 bosses) yields 3 x 22.5 = 67.5 (Classic) or 3 x 27.5 = 82.5 (Expert) bars.
- That is 1.25 or 1.53 sets per cycle.
- N=20 = 1,080 bars = 16.0 Classic or 13.1 Expert cycles.
- Add Thorium's 60 per bard+healer pair of sets, and the total reaches about 18 Classic cycles for 20 of each.
- Summon cost per cycle is cheap: Mechanical Eye 5 iron + 3 lens + 6 Soul of Light, Worm 5 iron + 6 chunk + 6 Soul of Night, Skull 5 iron + 30 bone + 3 + 3 souls [V:Recipes Cargo].
- Soul drops from the mechs are generous: 25-40 each [V:Souls drops].

**General fixes**
- A Hallowed Bar smelter that converts lesser bars at a lossy ratio.
- Mech-boss loot that scales with the number of soldiers engaged.
- A rare repeatable source, for example an enemy or crate-based supplement.

### Issue 4. Summoner mid-game: Spooky Wood

**Evidence**
- Spooky set: Helmet 200 + Breastplate 300 + Leggings 250 = 750 Spooky Wood at a Work Bench [V:Recipes Cargo].
- Mourning Wood drops 15-30 (Classic), 25-40 (Expert), 30-50 (Master). Splinterling drops 1-3 / 1-4 / 2-4 [V:Drops Spooky Wood].
- Only the Pumpkin Moon (post-Plantera) produces it. The medallion costs 5 Ecto + 10 Hallowed Bars + 30 Pumpkin [V:Cargo].
- Calamity's Fearmonger needs the Spooky set plus 30 Cosmilite, 7 Ascendant Spirit Essence and 30 Soul of Fright [C:Items/Armor/Fearmonger].

**Army arithmetic**
- 750 / 22.5 = 33.3 Classic kills per set, or 23.1 Expert (avg 32.5).
- N=20 = 15,000 wood = 667 Classic or 462 Expert Mourning Wood kills.
- Estimated at about 10-15 MW per wave-15 run, which is on the order of 30-60 full runs.

**Substitutes**
- Tiki armor is sold by the Witch Doctor [V:Tiki_armor]. It is the one fully buyable class set. It also shows the pattern a fix could copy.
- Hercules Beetle is also sold post-Plantera [V:Hercules_Beetle].

**General fixes**
- Add a shop route or a bulk wood recipe, for example log conversion at an event station.
- Add more per-run yield for repeats of the same event.

### Issue 5. Shroomite (ranger)

**Evidence**
- Each Shroomite Bar = 1 Chlorophyte Bar + 15 Glowing Mushrooms at an Autohammer [V:Cargo].
- Set: 12 + 24 + 18 = 54 bars [V:Cargo].
- The Chlorophyte Bar itself costs 5 ore [V:Cargo]. Chlorophyte regrows slowly: the ore converts mud near jungle grass at 1/300 per selected tile, with density limits [V:Chlorophyte_Ore].

**Army arithmetic**
- 54 bars = 810 mushrooms + 54 Chlo Bars (270 ore) per set.
- N=20 = 16,200 mushrooms + 1,080 Chlorophyte Bars (5,400 ore).

**Substitute**
- Frost armor (Adamantite 46 plus 3 Frost Cores, or Hallowed). The core drops from Ice Golem at 100%, but that enemy is rare [V:Frost_Core drops, Cargo].

**General fixes**
- A bulk mushroom source: a farmable tile or planter that stacks with the number of soldiers in range.
- A cheaper per-bar recipe when crafting in volume.

### Issue 6. Beetle armor (melee)

**Evidence**
- Beetle Husk drops only from Golem: 4-8 (Classic), 18-23 (Expert bag) [V:Drops Beetle Husk].
- Set: Helmet 4 + Scale Mail 8 + Leggings 6 = 18 husks, plus the matching Turtle piece for each slot [V:Cargo].
- Turtle set: 12 + 24 + 18 = 54 Chlorophyte Bars + 3 Turtle Shells [V:Cargo].
- Turtle Shell drops from Giant Tortoise at 8.33% [V:Drops Turtle Shell].
- The Lihzahrd Power Cell is craftable: 10 Adamantite + 25 Fallen Stars + 10 Glass [V:Cargo], so Golem fights are repeatable.

**Army arithmetic**
- 3 shells / 0.0833 = 36 tortoise kills per set. N=20 = 60 shells = 720 kills.
- Husks: N=20 needs 360. Classic Golem avg 6 gives 60 kills. Expert bag avg 20.5 gives 17.6 kills.
- 20 Turtle sets also need 1,080 Chlorophyte Bars.

**General fixes**
- Raise the shell rate for repeat farming, or add a conversion from tortoise or turtle-related drops.
- Scale Golem drops with soldiers engaged.

### Issue 7. Class emblems (Wall of Flesh only)

**Evidence**
- Vanilla: exactly one of four emblems per kill, 25% each [V:Wall_of_Flesh raw].
- Shimmer cycles the four emblems into each other: Warrior -> Ranger -> Sorcerer -> Summoner -> Warrior [V:Shimmer list].
- Calamity: five emblems (adds Rogue), each rolled independently at 1/4 with pity if nothing drops [C:NPCs/CalamityGlobalNPCLoot.cs:1316-1335; DropHelper.AllOptionsAtOnceWithPityDropRule]. That is 5 x 0.25 + 0.75^5 x 1 = 1.49 emblems per kill. Rogue Emblem joins the shimmer cycle [C:Systems/Mechanic/RecipeSystem.cs:921].
- Thorium: Bard Emblem 12.5% (Classic) or 25% (Expert bag) per kill [T:Bard_Emblem]. Bard, Cleric and Ninja emblems cycle among themselves only [T:Cargo shimmer rows].
- Avenger Emblem = any class emblem + 5 each of Might/Sight/Fright. Souls are plentiful at 25-40 per mech boss, so they are not the limit [V:Cargo, Souls].

**Army arithmetic (N=20 emblems for one army)**

| Setup | Emblems per WoF kill | Kills for 20 |
|---|---|---|
| Vanilla | 1.00 | 20 |
| Calamity | 1.49 | 13.4 |
| Thorium (3 emblems) | 0.375 Classic / 0.75 Expert | 53.3 Classic / 26.7 Expert |

- The Guide Voodoo Doll is a guaranteed drop from Voodoo Demons [V:Guide_Voodoo_Doll], so the fights are repeatable but each needs a doll.

**General fixes**
- Add a cross-mod shimmer link between the vanilla/Calamity and Thorium emblem cycles.
- Let a high-tier crafting station make emblems from souls plus a lower accessory.
- Scale WoF emblem rolls with the number of soldiers present.

### Issue 8. Drop-only weapons and accessories (Calamity), class unevenness

**Evidence (scan of `Items/Weapons` and `Items/Accessories`, no `CreateRecipe`)**

| Class | Weapons | Drop-only | Share |
|---|---|---|---|
| Summon | 102 | 50 | 49% |
| Rogue | 148 | 61 | 41% |
| Magic | 145 | 51 | 35% |
| Melee | 169 | 56 | 33% |
| Ranged | 164 | 51 | 31% |
| Accessories (all) | 227 | 109 | 48% |

- Boss weapon rate per item is 1/4 in Classic and 1/3 from the bag, rolled independently with pity [C:Utilities/DropHelper.cs:84-109].
- Cosmilite Bar feeds 40 weapons but only 3 summon ones. Auric Bar feeds 17 weapons but only 1 summon one. Uelibloom feeds 15, 2 summon. Perennial feeds 6, 1 summon [C:calwep scan].

**Army arithmetic**
- 20 copies of one drop-only item at 1/4 = 80 kills. At 1/3 it is 60 kills.
- Example chain: Electrician's Glove = Filthy Glove (Hive Mind drop, 1/4 or 1/3) + 100 Wire [C:Items/Accessories/ElectriciansGlove, HiveMind.cs:1347]. Nanotech then needs Rogue Emblem + Vampiric Talisman + Electrician's Glove + 8 Lunar Bar + 4 AE [C:Items/Accessories/Nanotech].

**General fixes**
- A "duplication" workbench that recreates a drop-only item from the original plus materials, so one lucky drop seeds the whole army.
- Bring summon weapon coverage closer to the other classes at each bar tier.
- Shared drop rolls that multiply with soldiers on the field.

### Issue 9. Ascendant Spirit Essence and the night-event triple

**Evidence**
- AE = 2 Necroplasm + 5 Nightmare Fuel + 5 Endothermic Energy + 2 Darksun Fragment at a Mythril Anvil [C:Items/Materials/AscendantSpiritEssence].
- Sources are gated to events after Devourer of Gods:
  - Nightmare Fuel: Pumpkin Moon enemies. Mourning Wood 5-10, Pumpking 10-20, Headless Horseman 3-5 [C:NPCs/CalamityGlobalNPCLoot.cs:834-864].
  - Endothermic Energy: Frost Moon enemies, with the Ice Queen at 10-20 [C:NPCs/CalamityGlobalNPCLoot.cs:880-916].
  - Darksun Fragment: Solar Eclipse enemies. Mothron 20-30 [C:NPCs/CalamityGlobalNPCLoot.cs:761-818].
  - Necroplasm: Polterghast 30-40 (Classic), 40-50 (bag) [C:NPCs/Polterghast/Polterghast.cs:999].
- Consumers: GodSlayer 9 per set, Silva 9, Fearmonger 7, and 4 each on Auric accessories (Rampart of Deities, Elemental Gauntlet, Ethereal Talisman, Chalice of the Blood God) [C:Items/Armor, Accessories].
- Medallions are not free: Pumpkin Moon needs Hallowed Bars (above).

**Army arithmetic**
- One GodSlayer set = 9 AE = 45 NF + 45 EE + 18 DS + 18 NP.
- N=20 = 180 AE = 900 NF + 900 EE + 360 DS + 360 NP.
- Polterghast alone: 360 / 35 = 10.3 Classic kills.

**General fixes**
- Add a refining recipe that mixes the three event materials (for example NF + EE + DS to AE at a better ratio).
- Let the events scale reward with soldier count.
- Add a non-event fallback source at a worse rate.

### Issue 10. Finite or low-renewal Calamity ore tiers

| Ore / bar | Per set | Where it comes from | Source |
|---|---|---|---|
| Aerialite (3 ore per bar) | 45 bars = 135 ore | Cloud islands at world gen; "enchanted" on Hive Mind or Perforator kill | [C:World/AerialiteOreGen.cs; HiveMind.cs:1313] |
| Cryonic (4 per bar) | 54 bars = 216 ore | World spawn at first Cryogen kill; Cryo Slime 10-26 at 100% | [C:Cryogen.cs:1426; CryoSlime.cs:88] |
| Perennial (4 per bar) | 45 bars = 180 ore | World spawn at first Plantera kill; Perennial Slime 10-26 | [C:CalamityGlobalNPCLoot.cs:2025; PerennialSlime.cs:82] |
| Uelibloom (4 per bar) | 54 bars = 216 ore | World spawn at first Providence kill; Bloom Slime 10-26 | [C:Providence.cs:1963; BloomSlime.cs:82] |
| Scoria (4 per bar) | 45 bars = 180 ore | Abyss world gen | [C:World/Abyss.cs:922] |
| Auric | see Issue 1 | Single Yharon spawn | |

- The slimes spawn at `Cavern.Chance x 0.08` with progression gates [C:BloomSlime.cs:57-65].
- Slime arithmetic: Tarragon needs 216 ore / 18 avg = 12 slimes per set. N=20 is 240 slime kills at a reduced spawn weight.

**General fixes**
- Add a slow regrowth or a post-clear respawn for the world-gen ores.
- Add a "prospecting" bulk recipe, for example converting adjacent-tier ore.

### Issue 11. Ectoplasm and Terrarium Core

- Spectre set: 54 bars (12 + 24 + 18), each 1 Chlorophyte Bar + 1 Ectoplasm [V:Spectre set Cargo]. That is 54 Ecto per set and 1,080 for N=20.
- Dungeon Spirits drop 1-2 Ecto at 100%, but the wiki warns that "it may take multiple trips to acquire enough for all items" [V:Ectoplasm].
- Thorium Terrarium Core: 2 cores need 1 each of 13 bars (including Hallowed, Chlorophyte, Ectoplasm) [T:Cargo]. Armor is 16 + 12 + 8 = 36 cores = 18 of each bar. The wiki says "205 cores for one of everything" [T:Terrarium_Core].

### Issue 12. Class coverage

- Vanilla has no bard, healer, rogue or thrower sets. Calamity adds rogue heads to most sets. Thorium adds bard, healer and thrower.
- Calamity head gaps: GodSlayer has no Magic or Summon head. Silva has only Magic and Summon heads. Auric needs a matching head from the earlier sets, so each Auric head requires a full chain of that class [C:Items/Armor/GodSlayer, Silva, Auric].
- Thorium endgame: Shooting Star (bard), Celestial (healer), White Dwarf (thrower) each 36 Luminite Bars + 45 class fragments. These share pillar output and Luminite with the vanilla sets.

---

## 3. Per-stage tables

Stages: **S0** pre-boss, **S1** pre-Hardmode bosses, **S2** Wall of Flesh to mechs, **S3** Plantera/Golem, **S4** Lunar/Moon Lord, **S5** post-Moon Lord (Calamity Providence onward, Thorium Primordials).
"20 sets" means N=20 of that one tier for one class.

### (a) Vanilla

| Stage | Melee | Ranged | Magic | Summon | Bottleneck and note |
|---|---|---|---|---|---|
| S0 | Copper 48 / Iron 60 / Gold 75 bars | same | same | same | Plain mining. Trivial. No class gating. |
| S1 | Molten: 45 Hellstone Bars (135 hellstone + 45 obsidian) | Necro: 110 Bone + 95 Cobweb (bone 97% at 1-3, Expert 2-6 at 100%) | Jungle: 32 Spores + 10 Stinger + 2 Vine. Meteor: 45 bars | Bee: 30 Bee Wax (Queen Bee 16-26, bag 17-30). Spider: 36 Fangs (Recluse 50% at 1-3) | 20 Bee sets = 600 wax = about 28 Queen Bee kills. Spider is the worst: 36 fangs per set, about 1 per kill Classic. |
| S1 alt | Shadow: 60 Demonite Bars + 45 Shadow Scale. Crimson: 35 Crimtane + 25 Tissue Sample | | | | Scales and samples are from EoW/BoC. Fine at small N. |
| S2 | Cobalt 30-45 / Mythril 45 / Adamantite 54 / Titanium 59 bars | same | same | same | Ore tiers are class-agnostic and bulk-minable. Easy (also crates, Extractinator lateral swaps). |
| S2 | **Hallowed 54 bars** | Hallowed or Frost (46 Adamantite + 3 Frost Cores) | Hallowed | Hallowed | **Issue 3.** 16 Classic cycles for 20 sets. |
| S2 acc. | Warrior / Avenger / Fire Gauntlet chain | Ranger Emblem, Sniper/Recon Scope chain | Sorcerer Emblem, Celestial Cuffs | Summoner Emblem, Necromantic Scroll, Hercules Beetle | **Issue 7.** Emblem is 1 per WoF kill. Scroll is 2.86-20% by wave. |
| S3 | Chlorophyte 54 bars (270 ore). Turtle 54 + 3 shells. Beetle +18 husks | Chlorophyte. **Shroomite 54 bars = 810 mushrooms** | Spectre: 54 Spectre Bars (54 Chlo + 54 Ecto) | **Spooky 750 wood**, or buy Tiki | **Issues 4, 5, 6, 11.** Summoner has a shop escape (Tiki). Others do not. |
| S3 alt | Forbidden: 46 Adamantite + 3 Forbidden Fragment (Sand Elemental 100%) | | Forbidden (mage) | | 3 elementals per set; rare spawn. |
| S4 | Solar 36 bars + 45 | Vortex 36 + 45 | Nebula 36 + 45 | Stardust 36 + 45 | **Issue 2.** Same 36-bar cost for each, so all four types are needed. |
| S5 | n/a | n/a | n/a | n/a | Vanilla ends at Moon Lord. |

**Vanilla easy spots**
- S2 ore armor, S0 bars, Hellstone and Meteor: bulk-minable.
- Chlorophyte is renewable, though slow.
- Tiki armor and Hercules Beetle are buyable.
- Nanites are buyable from the Cyborg [V:Nanites].
- Souls of Might/Sight/Fright at 25-40 per mech kill are plentiful.

### (b) Vanilla + Calamity

Calamity gives every set five head variants for one cost, so class parity is much better than vanilla. The pain moves to materials.

| Stage | Set (per set) | N=20 and note |
|---|---|---|
| S1 | Victide: 12 Sea Remains. Wulfrum: 45 Scrap + 3 Energy Cores (Scrap 1-3 per drone). Desert Prowler, Snow Ruffian: cheap | Trivial. |
| S1 | Aerialite: 45 bars = 135 ore + 5 feathers | **Issue 10.** Finite cloud ore. |
| S1 | Statigel: 30 Purified Gel + 30 Blighted Gel (SG 30-45 / bag 40-52; Blight slimes 15-21) | About 0.8 SG kills per set. Easy. |
| S2 | Sulphurous: 45 Acidwood + 45 Scale. Mollusk: 31 husk + 60 Sea Prism | Mining / sea farming. |
| S2 | Daedalus: 54 Cryonic Bars = 216 ore. Hydrothermic: 45 Scoria Bars = 180 ore | **Issue 10.** |
| S2-S3 | Reaver: 45 Perennial Bars = 180 ore (10 slimes per set) | Slime spawn 0.08 x Cavern. |
| S3 | Astral: 54 bars (2 Astral Ore + 3 soot per bar) | Biome ore. |
| S3 | Plaguebringer: vanilla Bee set + 15 plating + 15 canisters | Event-ish. |
| S4-S5 | Bloodflare: 110 Bloodstone + 45 Blood Orb + 9 Ruinous Soul | Ruinous: Polterghast 20-25 Classic, so 0.4 kill per set. Easy. |
| S5 | Tarragon: 54 Uelibloom Bars (216 ore) + 36 Divine Geode | 12 slimes per set; Providence geode 50-60. |
| S5 | Silva (magic/summon only): 180 Planty Mush + 30 Feather + 9 AE | **Issue 9.** |
| S5 | GodSlayer (melee/ranged/rogue only): 45 Cosmilite + 9 AE | DoG Cosmilite 65-80 Classic / 75-90 bag, so about 0.6 kill per set. AE is the real cost. |
| S5 | **Auric**: 45 Auric Bars (450 ore) + one GodSlayer/Silva set + Bloodflare + Tarragon + Flying Carpet | **Issue 1.** Per copy: 45 cosmilite, 9 AE, 110 bloodstone, 45 orbs, 9 souls, 54 Uelibloom bars, 36 geode. |
| S5 | **Demonshade**: 45 Shadowspec Bars (each 1 Auric Bar + 1 Exo Prism + 1 Ashes of Annihilation) | Another 450 Auric ore per set. Exo Prism: bag 30-40 / Ares 25-30. Ashes: Supreme Calamitas 25-30 Classic. |
| S5 | GemTech: 38 Exo Prism + 14 of each vanilla fragment + 14 Meld + 6 Core of Calamity | Collides with Issue 2. |
| S5 | Prismatic: 900 Nanites + 18 geode + 15 Exodium + 9 Armored Shell | Nanites are Cyborg-buyable (gold). The easy outlier. |
| S4/S5 weapons | 31-49% drop-only by class | **Issue 8.** Summon is worst. |

**Calamity easy spots**
- Large boss quantities: Cosmilite 65-80, Ruinous Soul 20-25, Divine Geode 50-60, Yharon fragment 35-40.
- Emblems at 1.49 per kill with pity.
- Nanites are buyable.

### (c) Vanilla + Calamity + Thorium

Everything above stays. Thorium adds the following.

| Stage | Thorium content (per set) | Note |
|---|---|---|
| S0-S1 | Bard: Crier 36 Cloth (Cloth = 4 Cobweb). Healer: Coral 24, Ebon 32 Unholy Shards | Cheap. |
| S1 | Thorium Bar sets: 45 bars = 180 ore (whole tier is 157 bars / 628 ore) [T:Thorium_Bar] | Plain mining. Easy. |
| S1 | Jester (bard): 15 Silk + 45 Demonite/Crimtane + 30 Scale/Sample. Noble: 36 Cloth + 30 Hellstone. Templar (healer): 36 Darksteel + 12 Life Quartz | Competes with Shadow/Crimson. |
| S2 | Cyber Punk (bard): 24 Hallowed + 12 Strange Plating. Biotech (healer): 36 Hallowed + 6 Energy Cell | **Issue 3.** Strange Plating 12-16 per mech boss. |
| S2 | Marching Band: 24 Silk + 24 Soul of Light. Sacred: 30 Hallowed Charm + 24 Purified Shards | Charm is craftable (3 Pixie Dust + 1 Soul of Light yields 3). Easy. |
| S3 | Life Binder: 36 Chlorophyte + 6 Bio Matter (10% from jungle mobs) | Competes with Chlorophyte sets. |
| S3 | Maestro: 18 Dark Matter + 26 Silk | Dungeon mobs, 50% at 1-3. |
| S4 | Shooting Star / Celestial / White Dwarf: 36 Luminite + 45 class fragments each | **Issue 2.** Three more fragment types from the same pillars. |
| S4 | Terrarium (class-agnostic): 36 Terrarium Cores | **Issue 11.** |
| S5 | Rhapsodist (bard) / Dream Weaver (healer) / Inspirator, Soloist etc.: 3 each of Death + Inferno + Ocean Essence | The Primordials drop 16 of each Classic, 20 per bag. That is 5.3 sets per kill. Easy. |

**Thorium shared-resource pressure**
- 43 recipes use Hallowed Bar, 60 use Soul of Night, 43 use Soul of Light, 29 use Luminite, 19 use Chlorophyte Bar [T:Cargo count].
- Emblems: **Issue 7**. Separate 3-emblem cycle at 12.5% each.

---

## 4. Things that are trivially easy (no nerf suggestions)

- Ore-tier armor from Cobalt through Titanium: class-agnostic and bulk-minable. Chlorophyte is renewable.
- Calamity boss material quantities are large. Cosmilite 65-80, Ruinous Soul 20-25, Divine Geode 50-60, Armored Shell 10-12, Yharon fragments 35-40. One kill often funds a set.
- Calamity emblems: 1.49 per kill with pity.
- Nanites are buyable. Tiki armor and Hercules Beetle are buyable.
- Thorium Primordial Essences: 16 per type (Classic), 20 (bag), and a set needs only 3 each.
- Strange Plating 12-16 per mech boss. Souls of Might/Sight/Fright 25-40 per mech boss.
- Hallowed Charm and Pixie Dust conversion: craftable in bulk.
- Calamity Perennial/Cryonic/Uelibloom Slimes drop 10-26 ore at 100% (but see spawn rate).

---

## 5. Summary of general fixes by pattern

- **Bulk crafting stations / duplicate recipes:** a workbench that recreates a drop-only item from the original plus materials (Issue 8); smelter for lesser bars into Hallowed Bar (Issue 3).
- **Conversions:** fragment type A to B, fragments to Luminite (Issue 2); a lossy upward ore ladder to complement the downward Shimmer (Issues 1, 10); cross-mod emblem shimmer link (Issue 7).
- **Drop scaling with army size:** shared drops (pillar fragments, Luminite, mech Hallowed Bars, Golem husks, WoF emblems) scale with soldiers engaged (Issues 2, 3, 6, 7).
- **Progression unlocks / shops:** copy the Witch Doctor and Cyborg pattern for bulk wood, mushrooms, shells (Issues 4, 5, 6).
- **Renewable sources:** post-first-kill ore regrowth and event-material refining (Issues 1, 9, 10).
