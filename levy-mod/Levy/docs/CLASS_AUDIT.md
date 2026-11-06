> **CORRECTION (verified on terraria.wiki.gg "Spider Fang"/"Black Recluse", 2026-11-06):** Black Recluse drops 1–3 Spider Fangs at 50% (Classic) / 90% (Expert), about 1.0 / 1.8 fangs per kill. Any "~2,080 kills" figure below is ~2× too high. 20 Spider sets (36 fangs each = 720 fangs) take ~720 kills Classic / ~400 Expert; with Spider Staffs (52 per levy) ~1,040 / ~580.

# Levy class audit: where is a class hard to equip in bulk?

Scope: 9 progression stages, analysed CUMULATIVELY: (a) vanilla only, (b) vanilla + Calamity, (c) vanilla + Calamity + Thorium. Each cell says what the added mod contributes, so you can see where a mod FILLS a vanilla hole.

**Evidence tags.** [Page] = a terraria.wiki.gg / calamitymod.wiki.gg / thoriummod.wiki.gg page fetched this session (page name in brackets). [CalSrc] = read from `/home/user/tml-save-2026.10/ModSources/CalamityMod` recipes/drops. [Levy:file] = Levy code read this session. (R) = recalled from game knowledge, NOT verified (treat as a lead). "calc" = my arithmetic. Thorium: only wiki pages were available (the extracted dll is binary only), so Thorium numbers are wiki numbers; its progression ORDER is (R).
Raw fetches: `.../scratchpad/classaudit/{tw,th,cal}/`. Sibling doc: `levy-mod/Levy/docs/BOTTLENECKS.md` (material-level detail; not repeated here).

**Grading rule (designer):** anything craftable from ORE/bars = EASY (mine it; Levy already has crate-ore and Chlorophyte options). Anything gated on a rare drop or a specific enemy/event with NO ore substitute = REAL PROBLEM. Armor bulk grades: **E** ore-craftable or vendor-buyable; **M** common farmable mob drop or single-boss drop in the hundreds; **H** rare drop / specific enemy / event-only, no ore path.

---

## 1. What Levy supports TODAY (from code)

| Topic | Fact | Source |
|---|---|---|
| Accessory slots by grade | Levy 1, Man-at-Arms 3, Champion 5 (config defaults). Command-point cost 1 / 3 / 7, so slots per CP is 1.0 / 1.0 / 0.71: most of an army at 10-60 CP is 1-slot Levies. | LevyConfig.cs:61-63; LevyConstants.cs:22-23 |
| Extra slots | Guide to Expertise (+1, drops from Lunatic Cultist, i.e. endgame). Boss milestones add slots army-wide but **all default to disabled** (WallOfFlesh +1 slot, Plantera +4 levies, MoonLord +1 slot). Config field `ExpertiseGuideSlots`. | GuideToExpertise.cs; LevyConfig.cs:64,110-130 |
| Hard cap | A soldier carries at most **6** accessories: `Clamp(grade + expertise + milestone, 0, 6)`. So a Champion (5) with Expertise (6) gains NOTHING from a milestone slot; milestone slots only help Levy and Man-at-Arms grades. Weapons/armor/ammo/hook are separate slots, so hooks and ammo never cost an accessory slot. | LevySettings.cs:51; SoldierGear.cs:13 |
| Stat pipeline | Real `Player.UpdateEquips` runs on a dummy (`UseVanillaStatPipeline`), so any accessory that only changes stats works: emblems, defense, minion/sentry slots, wings, boots, max mana. Only accessories on a short "useless" list are refused (info/building/fishing). | SoldierNPC.cs:249-281; WeaponClassifier.cs:73-86,543 |
| Accessory choice | Armory ranks by rarity, defense, value, prefix, damage-class fit (+ aggro). It has no notion of "role-critical" accessory; with 1 slot a summoner may spend it on the highest-scoring generic item rather than a minion-slot item. (Doctrine code counts extra minion slots for ARMOR choice; accessory score uses class damage fit.) | ArmoryTE.cs:353-359; ArmorDoctrine.cs:113,301-307 |
| Summoner: whip AND staff | **Yes.** Primary + sidearm may be staff and whip in either order. `TakeSummonerComplement` gives whip first as off-hand, then a staff, then a sentry, then a blade. A staff carried as sidearm still maintains its flock (`SummonWeapon()`); a staff/sentry is never the hand weapon. ROADMAP v0.9.52 fixed the earlier two-whips bug. | ArmoryTE.cs:829-842; SoldierNPC.Summon.cs:36-44; SoldierNPC.Combat.cs:73-80 |
| Minions | Flock = dummy `maxMinions` / weapon slot cost, clamped 1..6 per soldier; base is 1 slot. **So minion-slot armor and accessories ARE the summoner's damage.** One sentry per soldier. Bewitching Table / Summoning potion add a slot. | SoldierNPC.Summon.cs:59-70; LevyConstants.Summon.cs:7,73; StationBuffSystem.cs:138 |
| Yoyo | Yoyo reach/loiter come from static `ProjectileID.Sets.Yoyos*`; `YoyoMaxOut = 1`. **No code reads yoyoGlove / yoyoString / counterweight** (grep empty). A yoyo levy wearing a Yoyo Bag gets no second yoyo, no counterweight, no range. Yoyo Bag is therefore NOT required, and is currently worth nothing. | LevyConstants.Yoyo.cs:7-20; WeaponClassifier.cs:446-461 |
| Mana | `FireMagic` pays raw `item.mana`; no mana-cost reduction read (grep `manaCost/GetManaCost` only hits a station buff). Levies sip mana potions below 30% mana from the heal slot. So **Mana Flower is functionally dead for levies** (its -8% cost is ignored; its auto-potion is redundant). Max-mana items (crystals, cuffs/armor) are what help; hard cap 200. | WeaponUse.cs:408; LevyConstants.cs:64; SoldierNPC.Damage.cs:311 |
| Melee autoswing | Not needed (designer). Feral Claws still gives +12% melee speed through the pipeline [Page Feral_Claws]. | |
| Shield of Cthulhu | Levies never use the dash (only the Solar Flare dash is wired), so it is a **2-defense accessory** for them [Page Shield_of_Cthulhu]. Not required. | `dashType` grep: ArmorSets.cs:278 only |
| Wings / boots / hooks | Supported and used for pathing (`HasWings`, rocket boots, hooks). Wings matter for terrain, one pair per levy. | MovementAbilities.cs:78-81 |
| Thorium bard / healer | Supported. Bard Inspiration pool = 10 + absorbed Inspiration Fragments/Shards/Crystals from the home Armory (cap 40) + army renown. Healers heal allies. **Fragments are a per-bard bulk item** (below). | BardCompat.cs; LevyConstants.BardRenown.cs |
| Calamity rogue | Stealth is regenerated by running Calamity's own player hooks on the dummy. | SoldierNPC.GearUpkeep.cs:22-23,145 |
| Progression config (LevyProgressionConfig) | Recipes: Life Crystal/Fruit, Fallen Star, Chlorophyte Ore recipes; Chlorophyte ore per bar; ammo yield multiplier (1-10). Drops: `MobDropMultiplier`, `SoulDropMultiplier` (Light/Night/Flight), `EctoplasmDropMultiplier` (each 1-20x). Chlorophyte growth multiplier and chunk scale. Early-hardmode crate ore multiplier. Boss-weapon repeat %. Armor cost sliders (Chlorophyte, Hallowed, Spider, "Levy" = all armor incl. modded, min 10%; Spectre/Shroomite/Spooky protected). Arms Dealer bulk ammo. | ProgressionConfig.cs; ProgMath.cs:76; ProgressionRecipes.cs |
| What the drop multiplier does NOT cover | **Boss drops** are untouched except Ectoplasm and the three Souls. So Bee Wax, Hallowed Bars (mechs), Luminite (Moon Lord), Providence geodes, Calamity bars from bosses cannot be multiplied. Mob drops (Spider Fang, Turtle Shell, Lunar fragments from event mobs, mod mob materials) ARE covered by `MobDropMultiplier`. | ProgressionLoot.cs:30-38,50-52 |
| Guides | Firepower (WoF drop), Healthcare (craft: Life Fruit + Chlorophyte), Arcane Arts (craft: mech souls), Expertise (Cultist drop), Engineering, Autonomy, plus event books. None currently touch weapons, accessories-by-class, or bulk crafting. | Content/Items/Books |
| ROADMAP "factory / ammo bench" | ROADMAP greps show bulk reforge, armory filters, merchant officer and the Arms Dealer bulk-ammo idea; I found no shipped batch-crafting station for gear. Treat bulk-craft as open design space. | ROADMAP.md:156 etc. |

---

## 2. Class-by-class vanilla facts used below (verified)

* **Summoner needs a staff, a whip, and minion slots.** Leather Whip: buyable from the Zoologist, "virtually always available" [Leather_Whip] = unlimited. Snapthorn: 15 Stinger + 3 Vine + 12 Jungle Spores, craftable [Snapthorn]. Cool Whip: 8 Soul of Light + 8 Soul of Night + Frost Core [Cool_Whip]. Durendal: 12 Hallowed Bar [Durendal]. Firecracker: Wall of Flesh 1/4 [Firecracker]. Kaleidoscope: Empress of Light 1/4. Morning Star: 1/200 Hardmode-dungeon skeletons. Dark Harvest: Pumpkin Moon 2.5-12.5%.
* **Staffs.** Hornet Staff: 14 Bee Wax [Hornet_Staff]. Imp Staff: 17 Hellstone Bar [Imp_Staff]. Flinx Staff: 6 Flinx Fur + 10 Gold [Flinx_Staff]. Spider Staff: 16 Spider Fang [Spider_Staff]. Optic Staff: 12 Hallowed Bar + 20 Soul of Sight + Black Lens [Optic_Staff]. Blade Staff: Queen Slime. Sanguine Staff: Dreadnautilus. Pirate Staff: Pirate Invasion. Pygmy Staff: Plantera 1/4. Raven Staff: Pumpkin Moon. Tempest Staff: Duke Fishron 1/7. Xeno Staff: Martian Saucer 1/6. Slime Staff: rare drop from most slimes [Slime_Staff]; Finch Staff 1/3 of Living Wood chests (finite) [Finch_Staff].
* **Summoner armor.** Bee: 44 Bee Wax for set + Hornet Staff = 2-3 Queen Bee kills each [Bee_Wax]. Spider: 36 Spider Fang [Spider_armor]; fang 1/2 (Expert 9/10) from Black Recluse in Spider Nests [Spider_Fang]. Forbidden: 3 Forbidden Fragments + 46 Adamantite/Titanium Bars [Forbidden_armor]; fragments always dropped by Sand Elementals (sandstorm) [Forbidden_Fragment]. Tiki: purchasable from Witch Doctor for 1 platinum 50 gold per set, post-Plantera [Tiki_armor]. Spooky: 750 Spooky Wood [Spooky_armor], dropped only by Splinterlings/Mourning Wood in the Pumpkin Moon [Spooky_Wood].
* **Summoner accessories.** Pygmy Necklace (+1 slot): Witch Doctor, at night, pre-HM [Pygmy_Necklace]. Hercules Beetle (+15% summon dmg): Witch Doctor post-Plantera [Hercules_Beetle]. Necromantic Scroll (+1 slot, +10%): Pumpkin Moon only, 2.86-20% [Necromantic_Scroll]. Papyrus Scarab combines both (post-Plantera) [Papyrus_Scarab]. Summoner Emblem: Wall of Flesh [Summoner_Emblem].
* **Emblems (all four classes):** Wall of Flesh always drops ONE of the four, 25% each [Wall_of_Flesh]; Shimmer transmutes between them [Warrior_Emblem].
* **Mages:** Mana Flower 8% cost + auto-potion [Mana_Flower] (dead for levies, see s1). Celestial Cuffs pre-HM mana pickup.
* **Yoyo Bag** = Yoyo Glove + Strung Counterweight at Tinkerer's [Yoyo_Bag]; Yoyo Glove is bought from the Skeleton Merchant [Yoyo_Glove]. Craftable from purchasable parts; moot while Levy ignores it.
* **Wings:** the only pre-HM wings are Fledgling Wings [Fledgling_Wings]; others are mech-soul/Soul of Flight crafts (R). Soul of Flight drops only from Wyverns [BOTTLENECKS, Soul_of_Flight].

---

## 3. Stage tables (cumulative)

Row = class. Cells give: **armor bulk grade + key sets**, then weapons/accessory notes. "+Cal" and "+Tho" show what that mod ADDS to the previous column. Weapon counts: Calamity file counts in source (Melee 168, Ranged 161, Magic 140, Summon 97, Rogue 144 [CalSrc dir counts, includes some helpers]); Thorium: Bard ~116 items/9 armor sets, Thrower 132 items/9 sets, Healer ~158 items [Page Bard, Thrower, Healer]. Per-stage weapon counts below are descriptive, not exhaustive.

### Stage 1: pre-boss (before Eye of Cthulhu)

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee | **E**: Copper..Platinum 48-75 bars, ore only [BOTTLENECKS]. Many craftable swords. | Wulfrum (45 scrap + 3 Energy Core per set, Wulfrum mobs; Core is a supercharged-mob drop, H-ish) [CalSrc]. Victide (12 Sea Remains, **E**: Sea Remains crafts from shore items) [CalSrc, Sea_Remains]. | Thorium Armor (36 Thorium Bar = 144 ore, **E**) [Page Armor]; Steel armor bought from Blacksmith 3 gold, **E** (very easy) [Page Armor]. | None. Thorium Steel armor is the one TOO-EASY item (3 gold/set, no cap), left alone. |
| Ranged | **E** same ores; weapons craftable; Necro needs Skeletron. | Snow Ruffian (boreal wood + silk + flinx fur, **E**) [CalSrc]; Victide ranged head. | Yew Wood (105 Yew Wood, **E**), Tide Hunter (30 Aquaite + 18 Depth Scales: scales drop, **M**) [Page Armor]. | None. Ammo supply only (Levy has bulk ammo yield + Arms Dealer). |
| Magic | **E**: Meteor (45 bars) needs a meteor; Silk/Gem staffs craftable. Mana Flower is a chest item but dead for levies. | Aerospec (45 Aerialite bars + 5 feathers, **E**, sky island ore) magic head [CalSrc, Aerialite_Bar: 3 ore/bar]. | Silk armor (15 silk + 15 Fallen Star, **E** if stars; Levy has a Fallen Star recipe), Icy (18 Icy Shard) [Page Armor]. | None. |
| Summoner | **No armor** (Bee needs Queen Bee). Whip: Leather Whip **E** (unlimited, Zoologist). Staffs: Flinx **E** (fur), Slime Staff rare, Finch finite. Base 1 minion slot, so flock = 1. | Wulfrum jacket +1 slot, Aerospec summon head +1 slot, Victide summon head [Armor page, CalSrc]. All ore/scrap craftable = fills the vanilla hole. | Living Wood armor (24 Living Leaf + 45 Wood, +2 summon dmg, **E**), Astro (45 Meteorite, +1 minion +1 sentry, **E**), Spirit Trapper (36 Darksteel + 15 Spirit Droplet, +1 minion/sentry, **M**: droplets from Dark Casters/Cursed Skulls) [Page Armor]. | **Vanilla hole, filled by both mods.** Without a mod, a pre-QB summoner levy has 1 minion + Leather Whip. |
| Rogue (Cal) / Thrower (Tho) | n/a | Desert Prowler (mandibles + silk, **E/M**), Sulphurous (45 Acidwood + 45 Scale: Acid Rain only, **H-ish**: scales are an event drop after Eye) [CalSrc]. Rogue weapons plentiful. | Thrower: Sandstone (24 ingot), Flight (20 Feather), Bronze (48 Alloy Fragment) all **E** [Page Thrower_Armor]. | Calamity rogue needs the Rogue Emblem later (WoF). |
| Bard (Tho) | n/a | n/a | Crier's (36 Cloth, **E**), Jester's post-evil. Inspiration Fragment = 1 Fallen Star + 2 Thorium Bars [Page Inspiration_Fragment]: 10 per bard to hit 20 max (20 bards = 200 stars + 400 bars, calc), **E-M**. | Fallen Stars are the throttle; Levy's FallenStarRecipe covers it (meteorite bars). |
| Healer (Tho) | n/a | n/a | Coral (24 Coral), Ebon/Novice Cleric (12 Silk + shards), Iridescent (34 Gold/Plat + 3 White Pearl + 18 Purified Shards) [Page Healer_Armor]: mostly **E**; pearls/shards **M**. | None. |

### Stage 2: post-Eye of Cthulhu

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee | Same ores; Ninja armor (King Slime, 33% per piece) **M**. Shield of Cthulhu: bag only, 1 per kill, non-craftable; but it is just +2 defense for levies. | Daedalus etc. are later. Acid Rain scales now available. | none new. | None; SoC is not functionally required (dash unused). |
| Ranged | same | same | same | None. |
| Magic | same; Hermes/Cloud boots mimic-pool (**H**) but minor. | Blood Orbs (Blood Moon mobs) open later sets [Blood_Orb]. | none | None. |
| Summoner | Same hole. Hornet/Imp staffs need Queen Bee / Hellstone. | Same fill as stage 1. | Same fill. | Covered by mods only. |
| Others | -- | Rogue: same. | Healer/Bard/Thrower same. | -- |

### Stage 3: post-Eater of Worlds / Brain of Cthulhu

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee | Shadow/Crimson armor: 60 Demonite/Crimtane bars (180 ore) **E** but 45 Shadow Scales / Tissue Samples (boss/enemy drop) **M-H** [BOTTLENECKS]. Molten (hellstone) **E** best. | Hive Mind/Perforator materials (Rotten Matter etc., R). | Jester's (bard), Darksteel etc. (R). | **Scales/Samples: no ore substitute**, covered only by MobDropMultiplier if they are mob drops; EoW/BoC scale drop is boss-ish. Hellstone bypasses it for melee/ranged/mage. |
| Ranged / Magic | Molten/Jungle(spores) **E-M**; Necro later. | Statigel etc. need Slime God (late pre-HM). | -- | None. |
| Summoner | Imp Staff = 17 Hellstone **E** (first ore-craftable vanilla staff). Bee armor available if Queen Bee dead. | Statigel summon head (Purified + Blighted Gel, 30+30 per set; Purified Gel is a Slime God drop [Purified_Gel], so **H/boss-gated**). | Spirit Trapper, Astro, Living Wood remain. | Fine. |

### Stage 4: post-Skeletron (dungeon open)

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee | Molten still best; dungeon weapons (Muramasa, Cobalt Shield) are chest finds, single copies **H** for bulk but class has many substitutes. | Calamity pre-HM melee/rogue craftables; Aerospec/Victide as alt armor. | Granite/Darksteel (48 Darksteel Ingot, **E**) [Page Armor]. | None. |
| Ranged | **Necro armor (150 Bones + 135 Cobwebs)** **M** [BOTTLENECKS]; Bone Glove etc. | Snow Ruffian, Victide. | Yew Wood. | None (bone farm). |
| Magic | Jungle (spores/stingers) **M**; Necro; Water Bolt/Magic dungeon books are single finds. | Brimflame later. | Silk; Darksteel. | Weapons are chest singles (**H**) but magic has many craftable gem/ore staffs. |
| Summoner | **Bee armor + Hornet Staff: 44 Bee Wax per levy** = 2-3 Queen Bee kills each [Bee_Wax] (calc: 20 levies = 40-60 kills). Pygmy Necklace (+1 slot) from Witch Doctor at night **E** once QB is dead [Pygmy_Necklace]. Snapthorn **E** (stinger/vine/spore). | Bee armor is also the base of Calamity's Plaguebringer later. | -- | **Queen Bee wax is boss-drop-only, not multipliable by Levy (boss rule). Ranked real problem, but the mods supply alternative armor.** |

### Stage 5: early Hardmode (before any mech boss)

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee | Cobalt/Mythril/Adamantite/Titanium 135-236 ore per set **E**. Frost 3 Frost Core + 46 bars **M** (core guaranteed from Ice Golem). | Mollusk (31 Husk + 60 Sea Prism), Reaver (45 Perennial Bar = 180 ore; also from Geodes), Hydrothermic (45 Scoria), Daedalus (54 Cryonic) [CalSrc, Perennial_Bar etc.]. **E** (ore; each gated by a boss unlocking the ore (R)). | Thorium Hardmode set list (Dragon's armor 45 Green Dragon Scale, Granite 48 Energy Core, Lodestone 36, Valadium 36) [Page Armor]: scales/cores **M**. | None: ore sets plentiful. |
| Ranged | ore sets **E**. | Same plus Lunic Corps later. | Valadium (36 ingots), Tide Hunter. | None. |
| Magic | ore sets **E**; Forbidden **E+** (3 fragments + 184 ore) hybrid with summon. | Brimflame (30 Unholy Core: crafted from Infernal Suevite + Hellstone, ore-ish, **E-M**) [Unholy_Core]. | Permafrost (66), Titan helms later. | None. |
| **Summoner (known hole)** | **Spider armor: 36 fangs/set, +Spider Staff 16 = 52 fangs per levy; 20 levies = 1,040 fangs = ~2,080 Black Recluse kills Classic or ~1,160 Expert (calc from 1/2 and 9/10)** [Spider_armor, Spider_Staff, Spider_Fang]. Spider Nest only. **Forbidden armor is the escape:** 3 fragments + 184 ore per set; Sand Elementals always drop it (needs Sandstorm). Forbidden gives 25% damage but **no minion slots**, so flock stays small. Whips: Firecracker (WoF 1/4, single), Cool Whip (Frost Core + 16 souls). Staffs: Spider, Pirate (event), Blade (Queen Slime), Sanguine (Dreadnautilus event). | **Fills it:** Fathom Swarmer (27 Sea Remains + 150 Planty Mush + 45 Depth Cells; mush is an Abyss block you mine, cells from Abyss mobs) gives +2-3 slots, **M**; Plaguebringer later. Reaver/Hydrothermic/Daedalus have Summon-variant heads (damage only). | **Fills partially:** Life Bloom armor (+3 minions, 30 Chlorophyte + 6 Bio Matter) is post-Plantera (R). Nothing early. | **GAP A (top).** Only Spider gives slots in vanilla early HM and it is H (specific enemy, no ore path). Calamity's Fathom Swarmer is the only slot-giving mid-tier ore-adjacent set. |
| Rogue (Cal) | n/a | Mollusk (all), Umbraphile (Solar Veil: mob drop after Calamitas Clone), Titan Heart (Atlas drop) **H** [Solar_Veil, Titan_Heart]. Reaver/Daedalus/Hydrothermic rogue heads **E**. | Thrower: Fungus/Lich's/Shade Master (36 Dark Matter) [Page Thrower_Armor]. | None; ore heads exist. |
| Bard / Healer (Tho) | n/a | n/a | Bard: Marching Band (24 Silk + 24 Soul of Light, or 54 Hallowed Bar variants) [Page Bard_Armor]; Inspiration Shard (Light+Night+Fragment) for 11-20 -> 20-30. Healer: Warlock (Spiritualist, Blood Moon vendor), Sacred (30 Hallowed Charm + 24 Purified Shards) [Page Healer_Armor]. | Souls of Light/Night: Levy `SoulDropMultiplier` helps; fine. |

### Stage 6: post-mechs

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee/Ranged/Magic | Hallowed armor 54 bars/set; bars **only** from the three mech bosses, no ore path (BOTTLENECKS: 20 sets = ~1,080 bars = ~48 boss kills) **H**. Chlorophyte armor **E-M** (54 bars). Adamantite/Titanium remain the ore alternative. | Alternatives that avoid Hallowed Bars: Reaver, Hydrothermic, Daedalus, Mollusk (all **E**, ore). So Calamity fills the vanilla Hallowed hole. | Thorium: Hallowed-adjacent sets (Cyber Punk, Biotech: 36 Hallowed Bar) add MORE bar demand [Page Armor]. | **GAP B.** Vanilla-only: Hallowed Bar boss-only. Calamity mitigates for melee/ranged/magic. |
| Summoner | Optic Staff (12 Hallowed + 20 Soul of Sight + lens) and Durendal (12 Hallowed) **H** bars again. Spider still the only slot armor. Hallowed armor gives summoners nothing. | Fathom Swarmer, Plaguebringer? (post-Golem). | Same. | **GAP A persists** in vanilla. |
| Ranged (shroomite, later) | -- | Lunic Corps (Astral Bar 25 + Chlorophyte, needs Lunar Crafting Station) | -- | -- |
| Thorium classes | -- | -- | Healer: Biotech (36 Hallowed Bar + 6 Life-Powered Cell), Life Binder (36 Chlorophyte + 6 Bio Matter), Fallen Paladin (15 Holy Knight's Alloy). Bard: Cyber Punk (12 Strange Plating + 24 Hallowed Bar), Ornate (30 Bloom Weave), Maestro (18 Dark Matter + 26 Silk). Thrower: Lich's (36 Cursed Cloth). | Mix of ore-ish (Chlorophyte) and Hallowed-gated; fine. |

### Stage 7: post-Plantera

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee | Turtle: 54 Chlorophyte Bar + 3 Turtle Shell, shell 1/12 from Giant Tortoise (~36 kills/set) **H** [BOTTLENECKS]. Chlorophyte armor **E-M**. | Tarragon needs Uelibloom (post-ML). Mid options: Astral (54 bars, Lunar station). | Dread (40 Dread Soul), Demon Blood (40 shards) [Page Armor]; Terrarium (36 Terrarium Core, any class) later. | Turtle shell covered by MobDropMultiplier (mob drop). Chlorophyte alt. |
| Ranged | Shroomite 54 bars = 810 glowing mushrooms + 270 chlorophyte ore **M** (farmable; BOTTLENECKS). | Plague Reaper (Necro set + 65 Plague Canisters + 47 Nanites) [CalSrc], canisters are jungle-mob drops after Golem. | Valadium. | None critical. |
| Magic | Spectre: 27 Ectoplasm + 270 chlorophyte ore, Ectoplasm only from Dungeon Spirits **H** (~234 kills/set; Levy `EctoplasmDropMultiplier` exists). | Prismatic is post-ML. | Permafrost, Titan. | Covered by config. |
| **Summoner** | **Tiki armor: buy for 1p50g/set, Witch Doctor [Tiki_armor]: EASY AND TOO EASY** (20 sets = 30 platinum, 4 slots, +30% summon dmg, whip range). Hercules Beetle (buy). Pygmy Staff (Plantera 1/4, single), Desert Tiger/Raven/Dark Harvest (Pumpkin Moon, rare). Necromantic Scroll / Papyrus Scarab: Pumpkin Moon **H**. Spooky 750 Spooky Wood **H** (Levy protects it from cost sliders). | Plaguebringer (Bee set + 16 Infected Armor Plating from a BOSS + 16 canisters): needs the Bee set again. | Life Bloom (+3 minions) **M** Chlorophyte. | Armor solved by Tiki. **GAP C: summoner WEAPONS.** Most good post-Plantera staffs are single boss/event drops; Hercules/Pygmy purchasable. Tiki flagged as trivially mass-produced (no change recommended). |
| Rogue / Thrower / Bard / Healer | -- | Brimflame, Umbraphile, Plague gear: drop-material gated after Calamitas Clone. | Healer/Bard Chlorophyte-based: **E-M**. | -- |

### Stage 8: post-Golem

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee | Beetle: 18 Beetle Husk (Golem 4-8, bag 18-23) + a full Turtle set **H** (boss-only husk, 360 for 20 sets). | -- | -- | Husk is a BOSS drop (not multipliable) plus Turtle shells. **Real problem** but Turtle/Chlorophyte substitute exists. |
| Ranged/Magic | Shroomite/Spectre continue; Xeno Staff (Martian Saucer 1/6, event). | Plaguebringer Goliath drops Infected Plating; Plague armor sets. | -- | -- |
| Summoner | Xeno Staff only new; Tiki still best. | Plaguebringer: +3 slots but chain-gated (Bee wax + boss plating). | -- | Fine due to Tiki. |

### Stage 9: endgame (Cultist / Moon Lord / Calamity post-ML)

| Class | (a) Vanilla | (b) +Calamity | (c) +Thorium | Gap? |
|---|---|---|---|---|
| Melee/Ranged/Magic/Summon | Solar / Vortex / Nebula / Stardust: 36 Luminite Bar + 45 matching fragments each [BOTTLENECKS]; Luminite only from Moon Lord (2,880/20 sets = ~36 kills), fragments only from the Lunar Event (mob drops). **H** across all four, boss-only part not multipliable. | Huge fill: Tarragon (54 Uelibloom + 36 Divine Geode; Providence drops ~50-60 geodes per kill, calc 20 sets ~13 kills) [CalSrc Providence], Bloodflare (110 Bloodstone + 45 Blood Orb + 9 Ruinous Soul), Silva (180 Planty Mush + 30 Feather + 9 Essence), God Slayer (45 Cosmilite, DoG drops 65-80 bars [CalSrc]), Astral (54 Astral Bar), Omega Blue, Prismatic (mage), Demonshade, Empyrean/Auric (rogue/all). Mostly farmable-per-boss with scalable mob drops. | Terrarium (36 Terrarium Core), Titan/Illumite (48 Illumite Ingot) [Page Armor]; Bard Shooting Star / Rhapsodist (36 Luminite + 45 fragment, or 3 each essence), Healer Celestial, Thrower White Dwarf: mirror vanilla lunar gate. Essences from Thorium bosses. | **GAP D.** Vanilla-only: lunar tier is the hardest to bulk. Calamity gives ~10 alternative sets so the problem is largely FILLED, but each late Calamity summon set is stacked: Fearmonger needs a Spooky set + Cosmilite; Auric Tesla needs three other sets + Auric Bars (1 Yharon fragment per 5 bars) [CalSrc, Auric_Bar]. |
| Summoner extras | Stardust Dragon/Cell Staffs, Terraprisma (Empress, daylight-only), Kaleidoscope 1/4. | Silva (+5 slots), Demonshade (+2), many minion staffs (97 summon files). | Slag Fury armor (+6 minions, 10 Inferno Essence) [Page Armor]. | Slots plentiful here. |

---

## 4. Required items by class (summary)

| Class | Functionally required | Levy fit | Gap |
|---|---|---|---|
| Summoner | Whip + staff (both supported), minion slots (armor + Pygmy Necklace/Necromantic Scroll/Papyrus Scarab), sentry slot item, Summoner Emblem | Whip+staff: yes. Slots: pipeline works. 1-slot Levies cannot hold Necklace AND Emblem AND wings. | Slot-accessory priority in Armory scoring unverified; Necromantic Scroll/Scarab are Pumpkin-Moon-only. |
| Yoyo | Yoyo Bag in vanilla | **Unsupported**: Bag/Glove/counterweight/string have no effect (YoyoMaxOut=1). | Either wire Glove (second yoyo) or accept; not required today. |
| Mage | Mana Flower (vanilla), max-mana gear | Flower dead; potions automatic. | None; mana cost reduction items are ignored (design note). |
| Melee | Feral Claws (autoswing), SoC | Not needed / +2 defense only. | None. |
| Everyone | Wings, boots, hook | Supported; wings = Soul of Flight (Wyvern only) in vanilla. | Wing supply (BOTTLENECKS). |
| Class emblem (all) | Emblem, 25% each from WoF; Calamity adds Rogue Emblem to same 25% pool (5 emblems) [CalSrc CalamityGlobalItemLoot.cs:119-127] | Works through pipeline. | 1 WoF kill = ~1 emblem total across ALL classes; 20 levies need 20 emblems. Shimmer transmutes them; no recipe. |
| Bard (Tho) | Inspiration Fragments (Star + 2 Thorium Bars), Shards (HM), Crystals (Solar Pebbles) | Levy absorbs from Armory. | Crystals need Solar Pebbles (event, R). |
| Rogue (Cal) | Stealth gear, Rogue Emblem | Stealth works via hooks. | Emblem as above. |

---

## 5. Prioritised gaps (ranked by the designer's rule: rare drop / specific enemy, no ore substitute)

1. **GAP A: Early-hardmode summoner (minion-slot armor).** Spider = 52 fangs/levy, Spider Nest only, no ore path; Forbidden is ore-ish but gives no slots. Calamity's Fathom Swarmer is the first slot-bearing set with a mineable path; vanilla-only has none. Also Hornet Staff (44 wax per levy incl. armor) is boss-wax-only.
2. **GAP B: Hallowed Bar (mech bosses only)** blocks Hallowed armor, Optic Staff, Durendal, Excalibur-line for melee/ranged/mage/summoner. Calamity and Adamantite/Titanium cover armor; summoner weapons and Thorium Hallowed-based sets stay blocked.
3. **GAP D: Lunar tier (Luminite + fragments)** for all four vanilla classes, plus Thorium essences/Luminite sets. Boss-only Luminite is not multipliable.
4. **Pumpkin-Moon-only summoner kit:** Spooky armor (750 wood), Necromantic Scroll, Papyrus Scarab, Raven Staff, Dark Harvest. Event-gated, no ore path. (Tiki masks the armor part.)
5. **Boss-bound unscalable drops generally:** Bee Wax, Beetle Husk, Divine Geode, Hallowed/Luminite. The drop multiplier explicitly skips bosses.
6. **Class emblem supply:** 1 emblem per WoF kill total; no recipe. Matters because Levy-grade has 1 slot, so emblem competes with everything.
7. **Accessory slot starvation:** with 1 slot at Levy grade, every "required" accessory (emblem, Pygmy Necklace, wings, boots) competes. Expertise is endgame; milestone slots default off and wasted on Champions at cap 6.
8. **Yoyo Bag / dash / Mana Flower are non-functional for levies** (no impact today, but they will mislead players who believe they are required).
9. Too-easy items (no action): Tiki armor 1p50g, Thorium Steel armor 3 gold, Leather Whip unlimited, Pygmy Necklace.

---

## 6. Proposed GENERAL fixes (create incentives, no item rebalances)

| Fix | Addresses | What Levy already has | Notes |
|---|---|---|---|
| **A. Enable and re-tier accessory-slot milestones** (e.g. +1 slot at Queen Bee, +1 at Wall of Flesh, +1 at Plantera, +1 at Moon Lord; raise the cap from 6 to 7-8 so Champions benefit). | Slot starvation (7), makes required accessories fit. | `MilestoneRewards` list exists with WoF/MoonLord slots, default off. | Cheapest: change defaults to ON and add Queen Bee entry. The cap in `SoldierGear.AccessorySlots` is the only structural change. |
| **B. Bulk batch crafting ("Quartermaster" station / recipe set).** Convert plentiful materials into scarce ones at a fixed army-scale ratio: e.g. N Adamantite/Titanium Bars + souls -> Hallowed Bar; N Gel/ore -> Purified Gel; mushrooms -> Shroomite. Batch of 20+ gives a bonus yield (ProgMath already has a `BatchYield` helper with bonus %). | B, D partially, A (fang bundles), Spectre/Shroomite. | `ProgMath.BatchYield`, ChlorophyteOrePerBar, FallenStar recipe, armor cost sliders, ammo yield multiplier, Arms Dealer bulk ammo. | General: one station with an "ore tier -> equivalent bar" rule rather than per-item rules. Fits "factories/ammo bench" roadmap ideas. |
| **C. Extend the drop multiplier to bosses and to named bottleneck materials (opt-in).** `BossDropMultiplier` for bar/material drops (Hallowed Bar, Luminite, Bee Wax, Beetle Husk, Calamity bars). Works for Calamity/Thorium automatically since it is by item category not name. | 2, 3, 5. | `MobDropMultiplier`, `SoulDropMultiplier`, `EctoplasmDropMultiplier`; `ProgressionLoot.Factor` is the single place to extend. | Config only; keeps defaults vanilla. |
| **D. "Armory Quartermaster" guide book** unlocking cheaper class-kit recipes (e.g. "Guide to Logistics": 1 emblem per 5 souls, Spider/Bee set recipe using any bar tier). Guides already do army-wide once-per-world unlocks. | 1, 4, 6. | Guide system (`GuideSystem`), crafted guides (Healthcare, Arcane Arts). | A crafted guide gated by mid-tier materials fits existing pattern. |
| **E. Role-critical accessory preference in the Armory** (score minion-slot/sentry-slot accessories for summoners, max-mana for casters, so the single slot is spent well). | 7. | `ArmorDoctrine` already measures minion slots for armor. | Code-side; not an item buff. |
| **F. Optional: wire the Yoyo Glove (second yoyo) or drop it from player expectations.** | 8. | `SoldierYoyo`, `YoyoMaxOut`. | Low priority. |

---

## 7. Top 8 gaps (for the report)

1. Early-hardmode summoner minion slots (Spider fang-only) -> **B** (any-bar -> set recipe), **A** (earlier slot milestones); Calamity Fathom Swarmer fills partly.
2. Hallowed Bar boss-only -> **B** and **C**.
3. Lunar tier (Luminite/fragments) -> **C**, **B**; Calamity post-ML sets fill.
4. Pumpkin-Moon-only summoner kit -> **D** (guide-unlocked alternatives), **B**.
5. Unscalable boss drops (Wax, Husk, Geode) -> **C**.
6. Class emblem supply -> **D** / **B** (emblem recipe from souls).
7. 1-slot Levies cannot hold the "required" accessories; milestone slots disabled and wasted at cap -> **A** and **E**.
8. Non-functional required items (Yoyo Bag, dash, Mana Flower) -> document or wire **F**.
