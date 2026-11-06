# Gear progression audit: who gets new gear?

Designer's question: when new gear arrives, who gets it? The intent is (1) the best weapon still goes to the best-suited class, never taking a great weapon from its best user and leaving them a pin, and (2) new armor, accessories and weapons raise the FLOOR: the worst-equipped levies get them first. Example: 10 levies, some in copper, wood or cactus, 3 new Shadow sets go to the 3 worst-armored levies and the middle stays where it was.

Everything below is what the audit measured on the real armory code (`ArmoryTE.UpgradeWeapons` / `IssueGear`), not a model of it. "Observed" is read off the logs; "inferred" says so.

## The audit

`Common/Testing/GearProgressionAudit.cs` (test-only, does nothing unless `LEVY_GEARAUDIT=<output path>` is set; `LEVY_GEARAUDIT_MODE=before|after|both`, `LEVY_GEARAUDIT_TRACE=1` traces the weapon plan; `LEVY_GEARAUDIT_DEBUG=1` dumps set-bonus detection). Runs once at PostSetupRecipes on throwaway armories and soldiers, touches no world state.

One army of 10 levies, mixed class, mixed grade (4 Levy, 4 Man-at-Arms, 2 Champion; 1, 3 or 5 accessory slots): wood x3, copper x2, cactus x3, one mixed kit, one tin; copper/tin/wooden weapons (4 melee, 3 ranged, 2 magic, 1 summon); a few starter accessories (Aglet, Shackle, Shiny Red Balloon, Band of Regeneration). Arrows and bullets are in storage.

Gear arrives in the order a player gets it. Each step deposits the armor sets (plus every head variant, so melee, ranged and magic helmets exist), per-class weapons and accessories:

| Step | Gear |
|---|---|
| 1 | Iron, Tungsten, Gold; ore weapons; Hermes Boots, Cloud in a Bottle, Lucky Horseshoe |
| 2 | Ninja (King Slime); Katana, Slime Gun/Staff; Slime Crown |
| 3 | Shadow and Crimson; Light's Bane, Blood Butcherer, Musket, Vilethorn, Crimson Rod...; Worm Scarf, Brain of Confusion, Band of Starpower |
| 4 | Meteor; Space Gun, Phoenix Blaster; Obsidian Shield |
| 5 | Jungle; Blade of Grass, Boomstick, Hornet Staff; Anklet of the Wind, Feral Claws |
| 6 | Necro; Muramasa, Water Bolt, Imp Staff; Cobalt Shield |
| 7 | Molten; Fiery Greatsword, Molten Fury, Hellwing Bow, Flower of Fire; Lava Waders, Magic Quiver |
| 8 | Cobalt, Palladium (all heads); matching swords, repeaters, staves; Putrid Scent |
| 9 | Mythril, Orichalcum; Paladin's Shield, Charm of Myths |
| 10 | Adamantite, Titanium; Ankh Shield, Frozen Turtle Shell |
| 11 | Hallowed (all heads); Excalibur, Hallowed Repeater; Angel Wings |
| 12 | Chlorophyte, Spectre, Shroomite (all heads); Saber, Shotbow, Megashark, Spectre Staff; Celestial Shell, Shark Tooth Necklace, Ankh Charm |

After each step every levy's upgrade timer is run (ascending ids, repeated until nothing changes) and the log records, per levy: armor with the step it entered the game, set bonus, defense, weapon, weapon class vs armor class, weapon score, accessories; per army: defense, armor score and weapon score floor / median / max; the swaps made; what is left in storage; and a FISHY list:

`UNUSED-ARMOR/ACC/WEAPON` (a clearly better item sits in storage), `WEAPON-MISMATCH` / `ARMOR-MISMATCH` (a top item with a worse-suited class), `CLASS-INCOHERENT`, `NOT-WORST-FIRST` (a stronger levy upgraded while a junk one got nothing), `NO-SET-BONUS` / `BARE-SLOT` (split sets), `CHURN` (A -> B -> A or more than two changes in a slot in one step). It also repeats the whole playthrough with the timers firing in descending ids and two shuffled orders (ORDER SENSITIVITY).

Full logs, before and after, every step and every levy: `docs/gear_audit/AUDIT_LOG_BEFORE_AFTER.md`.

Limits: the weapon column is `WeaponClassifier.Score` (a rough damage proxy), not true DPS, and the allocator ranks by sell price, so a Katana reads 14 while ranking high; no world, so no stage discount or recency boost; every levy is idle (no mid-fight gating, no 30 s swap cooldown), so the audit measures where gear settles, not how fast.

## Current behaviour, as observed (before)

1. Armor is first-come, not worst-first. `UpgradeArmor` runs per levy on his own timer and takes the best set for HIMSELF, which beats his kit by 15%. Whoever's timer fires first (the audit: lowest id) takes the new set and his old set goes back to storage, to be picked up by the next timer down the line. Nobody compares levies. 45 NOT-WORST-FIRST flags over the playthrough, at least one in each of steps 1-9. Examples: step 3 (Shadow, Crimson): six strong levies upgrade (kits 759 to 2406) while levy 10, in cactus at kit 587, gets nothing; step 5 (Jungle): nine flags, levies 9 and 10 still in copper while levy 1 goes from 3501 to 5394. The army floor stays at wood/cactus/copper for steps 1-6: armor score floor 200 (step 1), 587 (steps 2-3), 759 (steps 4-6), against a max of 5394.
2. Accessories are fill-only. A slot holding an Aglet is never swapped, however good the stock. A 1-slot levy keeps his Aglet / Shackle / Red Balloon for the whole game. New accessories reach only levies that still have an empty slot and whose `IssueGear` happens to run, in id order.
3. Weapons are already right. `WeaponAllocator` deals each class's stock worst-equipped first inside the class, class-matched levies before the class-less, and a levy can only take a weapon from a levy with less claim to it. No WEAPON-MISMATCH was ever flagged. The three UNUSED-WEAPON flags in the BEFORE log are transient states inside the churn below.
4. Weapon churn (a bug, found by the audit). A class-less levy (generic armor) who already held a weapon was counted into his bucket and then dealt a bucket again by the class-less spread, so on every plan he could flip between melee and magic. In the audit that is steps 6-9 never settling (14 rounds, the cap), with 285, 824, 561 and 298 weapon swaps in a step, levies cycling Crimson Rod -> Mythril Sword -> Vilethorn -> Light's Bane -> Palladium Sword. In the real game that is a weapon swap every 30 s forever for those levies (inferred; the audit has no cooldown). The trace confirms the cause: the same levy picked cls=Melee in one plan and cls=Magic in the next with an unchanged army.
5. Set-bonus detection never worked (a bug). `ArmorDoctrine.FormsSetBonus` ran the vanilla set check on a scratch player whose worn-slot ids (`head`/`body`/`legs`) were never set, so it returned false for every set (observed: every row of the first run said "set bonus: no"; a debug dump showed the bonus string only appears once the slot ids are set). The consequences, inferred from the code: the +300 set-bonus score in armor choice and the "irregular names" cross-family search never fired; matching worked only by name stem, which covers vanilla.
6. Set splitting: not observed. Apart from the deliberately mixed starting kit (levy 7), no levy was ever left in a mixed kit; the pieces are drawn per set.
7. Churn in armor: none beyond the above. The 15% margin holds.

Order sensitivity (before): the final floor depends on whose timer fires first (6872 vs 7072 in the order test).

## The fix: raise the floor

`ArmoryTE.Floor.cs` (new), small hooks in `ArmoryTE.cs`.

- `FloorSweep()`, run at the top of every `UpgradeWeapons`, visits the served levies from the worst-equipped up (armor score, bare first, then id) and gives each the best set he is entitled to using the existing `TakeArmorSetCore` upgrade path, so the usual 15% upgrade margin applies: every swap strictly improves the kit, nothing can cycle. Whoever asks, the same deal is made, so timer order no longer matters. Up to four passes, so hand-me-downs settle in one call.
- Class guard: a set that favours class C is not taken by a levy whose weapon is another class while a levy firing C would gain from it (by the same margin). The best set stays with its best-suited user, even when a worse-armored levy of another class asks first.
- Accessories: `AccessoryStep` (one slot per levy per visit, lowest average accessory strength first, empty slots counting as zero) fills an empty slot, else swaps the weakest slot for something at least 15% better. An accessory with a bonus for another class is left for a levy of that class who would use it. The old one goes back to storage. The requester also runs `UpgradeAccessories` after armor.
- Throttle: the sweep re-runs only when armor or an accessory was deposited since the last one, the served count changed, or 20 s passed, so a 100-levy army does not rescan on every timer.
- Weapons are not changed by this; they were already class-first and worst-first. The reported churn is fixed separately (below).
- Server / single player only; multiplayer clients never run it (same as the weapon upgrade).
- `ArmoryTE.FloorFirst` (default true) is an internal switch the audit uses to measure the old behaviour.
- Spread-flip fix in `WeaponAllocator.SpreadClassless`: holders are no longer re-dealt a bucket. Test seam `LegacySpreadRedeal` (never set in game) reproduces the old behaviour for the audit.
- Set-bonus fix in `ArmorDoctrine.FormsSetBonus`: the scratch player's slot ids are set before the set check. Note this changes armor choice slightly: real set bonuses now count for the +300 score and the irregular-name search works.

## Result (after)

Armor / defense floor, median, max after each step; swaps are armor pieces / weapons / accessories in the step; rounds is how many passes of every timer it took to settle (14 = never settled); flags are FISHY lines.

| Step | Defense before | Defense after | Armor score before | Armor score after | Swaps before | Swaps after | Flags b/a |
|---|---|---|---|---|---|---|---|
| 0 Start | 2/3/5 | 2/3/5 | 200/587/880 | 200/587/880 | 0/0/0 | 0/0/0 | 1 / 1 |
| 1 Ore armor | 2/4/13 | 3/4/13 | 200/759/1815 | 511/759/1815 | 14/6/3 | 9/6/3 | 3 / 1 |
| 2 Ninja | 3/4/14 | 3/5/13 | 587/819/2406 | 587/819/2406 | 15/1/0 | 3/1/0 | 6 / 0 |
| 3 Shadow, Crimson | 3/8/19 | 3/8/19 | 587/1416/3501 | 587/1416/3501 | 24/6/3 | 6/6/3 | 6 / 0 |
| 4 Meteor | 4/10/20 | 5/10/19 | 759/1758/3501 | 759/1758/3501 | 18/2/1 | 6/4/1 | 6 / 0 |
| 5 Jungle | 4/14/20 | 5/12/19 | 759/2110/5394 | 759/2110/5394 | 15/5/2 | 6/4/2 | 11 / 0 |
| 6 Necro | 4/15/20 | 6/14/20 | 759/2800/5394 | 759/2800/5394 | 15/285/1 (14 rounds) | 3/4/1 | 12 / 1 |
| 7 Molten | 7/16/25 | 9/16/26 | 1131/3340/6194 | 1131/3340/6194 | 18/824/2 (14 rounds) | 6/3/2 | 13 / 0 |
| 8 Cobalt, Palladium | 7/20/32 | 11/19/32 | 1131/4023/7972 | 1815/4023/7972 | 24/561/1 (14 rounds) | 9/2/1 | 17 / 1 |
| 9 Mythril, Orichalcum | 16/22/32 | 17/22/42 | 3194/5794/7972 | 3194/5794/8993 | 21/298/2 (14 rounds) | 9/2/2 | 6 / 0 |
| 10 Adamantite, Titanium | 16/32/50 | 17/25/42 | 3194/7493/9808 | 3501/7033/8993 | 21/23/2 | 6/3/2 | 3 / 0 |
| 11 Hallowed | 16/32/51 | 17/29/42 | 3194/7883/10885 | 4544/7583/9385 | 6/4/1 | 3/3/1 | 1 / 0 |
| 12 Post-Plantera | 21/46/51 | 21/39/52 | 6872/9758/14156 | 6872/8601/14156 | 24/5/3 | 9/3/4 | 1 / 0 |

Reading it:

- The floor rises earlier and the junk is cleared first: defense floor at step 8 is 11 vs 7, at step 11 is 17 vs 16, armor floor 4544 vs 3194 at step 11. Flags fall from 45 NOT-WORST-FIRST and 31 CHURN to one NOT-WORST-FIRST; steps 6-9 settle in one round instead of never; armor swaps per step fall by about two thirds (15-24 to 3-9) because a new set goes straight to the worst instead of rippling down the line.
- The middle and the top lose a little during the middle of the game and at the end. Median armor is lower in steps 10-12 (8601 vs 9758 at the end) and the top is a little lower at steps 9-11 (armor max 9385 vs 10885 at step 11); the final max is the same (14156). Inferred cause, not isolated: the same stock is spread differently, and the weapon classes the levies end up with differ (after: 3 melee, 3 ranged, 4 magic; before: 5 melee, 2 ranged, 3 magic). Magic helmets and sets are lower tier in vanilla, so a more magic-heavy army has a lower median defense from the same stock. `UNUSED-ARMOR` is clear in the last six steps, so the better sets are worn, not left in storage.
- Designer's example, verified at boot (`GearFloorCheck`): 10 levies (3 wood, 3 copper, 2 cactus, 2 iron), 3 Shadow sets arrive: the 3 wood levies wear Shadow, everyone else is unchanged, and a second round of every timer changes nothing. Same in reverse timer order.
- Best item to best class: a Shroomite set goes to the gunner in iron over the swordsman in wood (`GearFloorCheck`). In the playthrough there is no ARMOR-MISMATCH and no WEAPON-MISMATCH.
- New accessories: the one new Obsidian Shield goes to the levy with none, not to the ones already holding two (`GearFloorCheck`, both timer orders). In the playthrough accessories change 1-4 times per step, and storage holds no accessory at the end of any step (all are worn): the 1-slot levies end with the weakest ones (Aglet, Shackle, Red Balloon) only because the stock is smaller than the army's slot count. An empty slot counts as zero strength, so the multi-slot levies' empty slots are filled before a 1-slot levy swaps up; that ordering is a tuning point.
- Order sensitivity (after): final armor floor / median / max are identical in all four timer orders (6872 / 8601 / 14156); before they varied (floor 6872 vs 7072).

Residuals in the after log, all minor: step 8, a levy holding a magic staff when the sweep ran was passed over for a melee set that went to a higher-kit melee levy (his weapon class changed to melee in the same step, after the armor was dealt); steps 1 and 6 flag a levy whose Copper/Cactus kit is within the 15% margin of a stored Tin/Cactus set (below the upgrade margin by design).

## What is not verified

- Multiplayer: the sweep is server / single player only; not tested with real clients.
- Live play: the audit has no mid-fight gating (a busy levy is skipped by the sweep and by the armory, as before) and no swap cooldown; real timers settle the same state more slowly.
- The sweep adds one more full pass over the stock per new armor or accessory deposit; cost not profiled at 100+ levies (inferred small: throttled to deposits and 20 s).
- Weapon floor is not touched. The weapon score proxy shows a floor of 14 for several steps in both runs (a levy holding a Katana, which ranks high by sell price); that is the ranking's choice, not the allocator's.
- Magic Storage and pylon-networked armories are scanned by the existing set code, not separately exercised here.

## How to re-run

Build Levy into a private save dir, then boot with `LEVY_GEARAUDIT=/path/out.md` set (any enabled list containing Levy; the report is written at PostSetupRecipes, and the server then waits at world select). Boot checks: `Levy gear floor self-check: 12 checks OK` and the existing allocator, intake, rules, ranking and reforge checks.
