# Levy weapon audit

How a soldier reproduces a weapon's special effect, checked against the decompiled vanilla source (`Player.ItemCheck_Shoot`, `Player.ProcessHitAgainstNPC`, `Player.ApplyNPCOnHitEffects`, `Player.StatusToNPC`, `Player.PickAmmo`, `Projectile.StatusNPC`).
Verified by reading code paths and compiling (`COMPILE OK`); nothing here was playtested in game.

Categories
- **A** already works: the item's projectile goes through `TryRealShoot` (real Shoot hooks) or `ResolveProjectile` (ammo swaps), or a Levy projectile family handles it.
- **B** fixed by routing broadsword hits through the real hook chain (`MeleeEffects.StrikeNpc`: ModifyHitNPC, item statuses, OnHitNPC on ModItem / GlobalItem / ModPlayer).
- **C** vanilla hard-coded special, ported by hand (`MeleeEffects.ApplyVanillaMeleeHit` for melee hits, `ItemShots` / `ResolveProjectile` for shots).
- **D** still broken or out of scope, with the reason.

Root causes found
1. Broadsword swings called `SimpleStrikeNPC` directly: no on-hit hooks, no item debuffs, no per-item melee specials (B, C below).
2. After the Shoot hooks, vanilla runs a long per-item `else if (sItem.type == ...)` chain in `ItemCheck_Shoot` that sets extra `ai[]` values, spawn points and counts. Only Daedalus Stormbow and Blood Rain Bow were ported; every other chain item fired a plain projectile with `ai[] = 0` (C below).
3. Night's Edge family (no-melee swings whose projectile is a slash drawn around the owner's body, `aiStyle` NightsEdge / TrueNightsEdge) were classified as plain shooters and spawned a broken slash bound to the commander. They are now broadswords.
4. Tethered "retract to owner" projectiles (Harpoon, aiStyle 13) hijack the commander's slot: rejected by the classifier.

## Audit table

| Weapon | Special effect | Cat | Evidence / note |
|---|---|---|---|
| Volcano (Fiery Greatsword) | 50% On Fire on hit; one explosion per swing on the first enemy hit | C | `StatusToNPC` case 121 runs in `StrikeNpc`; blast = projectile 978 spawned in `ApplyVanillaMeleeHit`, once per swing (`SwingSpecialSpent`); 978 inflicts On Fire itself (`Projectile.StatusNPC`) |
| Bee Keeper | 1-3 bees per hit (+1 sometimes with Hive Pack), 90% Confused | C | bees use `Dummy.beeType/beeDamage/beeKB`, damage = source damage / 3, spawned at the swing box |
| Muramasa | one arc slash (projectile 977) per swing | C | port of the `_spawnMuramasaCut` block |
| Blade of Grass | 25% Poisoned on hit; vine shot (projectile 976) each swing | C | status via `StatusToNPC` 190; vine = `ItemShots.BladeOfGrass` (ai0 = turn, ai1 = steps; vanilla chain item 190) |
| Light's Bane | slash (projectile 974) appears on the enemy, doubled on crit | C | `ItemShots.LightsBane`; before, it spawned a scale-0 projectile (its ai[0] is the scale) |
| Starfury | stars fall from the sky onto the enemy, 1.5x damage, no knockback | C | `ItemShots.Starfury` (vanilla chain item 65 + `projToShoot == 9`); ai[1] = landing height, before it spawned at the body with ai[1] = 0 |
| Blood Butcherer | barb sticks into the enemy, max 5 per enemy, once per swing | C | `SpawnBarb` (projectile 975, `Projectile.KillOldestJavelin`) |
| Tentacle Spike | same barb mechanic (projectile 971) | C | as above |
| Bat Bat | heals the wielder 1 HP once per swing | C | heals the soldier body |
| Enchanted Sword | sword beam | A | `item.shoot` 173 via `StartSwing` -> `TryRealShoot` (no chain branch in vanilla) |
| Ice Blade | ice bolt, counts as melee | A | `StartSwing` -> `TryRealShoot`; shot is tagged with the item's damage class (melee), as vanilla's `.melee = true` |
| Beam Sword, Frostbrand (hardmode) | beam | A | same path; no chain branch |
| Night's Edge (+ True Night's Edge, Excalibur, True Excalibur, Terra Blade, The Horseman's Blade) | slash projectile drawn around the wielder | D | classified as broadswords: the swing hitbox does the damage. Every held slash (972, 982, 983, 984, 997) and True Night's Edge's lunge (973) re-centre on `Main.player[owner]` each tick, so none are spawned (Night's Edge, Excalibur, True Excalibur, True Night's Edge have nothing else that flies). Terra Blade's beam (985, `aiStyle` 191) flies on its own velocity and is launched from the soldier on each swing (`SwordBeams`, speed 60 decaying x0.94/tick, damage x0.75 per hit); a `PostAI` clears its first-6-tick commander line-of-sight gate. The Horseman's Blade spawns a homing Flaming Jack (321) per enemy hit (`MeleeEffects.ApplyVanillaMeleeHit`). Known quirk: aiStyle 190/191 hits take their knockback direction from the commander's side of the enemy (Projectile.Damage) |
| Bladetongue (hardmode) | ichor splash per hit, Ichor debuff | C | `SpawnIchor` + `StatusToNPC` 3211 |
| Breaker Blade, Keybrand (hardmode) | damage bonus against full-health / nearly dead enemies | C | `ItemDamageModifiers` applies the source-damage multiplier before the strike |
| Modded sword with `OnHitNPC` / `ModifyHitNPC` / ModPlayer `OnHitNPCWithItem` | spark projectiles, life steal, debuffs | B | `StrikeNpc` runs `CombinedHooks.ModifyPlayerHitNPCWithItem` before and `OnPlayerHitNPCWithItem` after one strike; spawned projectiles are re-owned and tagged |
| Magma Stone / Fire Gauntlet / Frostburn-style accessories on the soldier | burn or frostburn on melee hits | B | `Dummy.StatusToNPC` reads the dummy's accessory flags set by `UpdateEquips` |
| Flask melee imbues | element debuffs | D | `meleeEnchant` comes from buffs; the dummy has none. (Soldier projectiles flagged melee still read the commander's own imbue through `Projectile.StatusNPC`) |
| Ham Bat | damage scales with Well Fed | D | reads the dummy's food buffs, which it never has |
| Spear, Trident, Swordfish, Obsidian Swordfish, Rotted Fork, shortswords (Gladius ...) | thrust | A | `SoldierThrust` projectile; vanilla has no extra on-hit special for these |
| Leather Whip, Snapthorn, Spinal Tap | lash + tag | A | `SoldierWhip` with Levy's own tag system; vanilla whip tag debuffs are replaced by it, not reproduced |
| Wooden / Enchanted Boomerang, Bananarang, Light Disc | thrown, returns | A | `SoldierBoomerang` |
| Flamarang, Ice Boomerang, Thorn Chakram | burn / frostburn / poison on hit | C | these debuffs are keyed on the vanilla projectile type in `Projectile.StatusNPC`; `ApplyBoomerangStatus` reapplies them from `SoldierBoomerang.OnHitNPC` |
| Ball O' Hurt, Blue Moon, Sunfury, The Meatball, Mace, Flaming Mace | flail, thrown out and hauled back | A | `SoldierFlail` (Levy-owned, anchored to the soldier, item's ball sprite); the vanilla held flail is never spawned |
| Wooden Yoyo ... Terrarian, modded yoyos | yoyo orbits the target for its flight time, then returns | A | `SoldierYoyo` (Levy-owned, own AI, reach from the shot's `YoyosMaximumRange`, flight time from `YoyosLifeTimeMultiplier`); the vanilla held yoyo is never spawned |
| Harpoon, Chain Knife, Golem Fist, Boxing Glove | tethered line that retracts to the owner | D | projectile `aiStyle` 13 reads `Main.player[owner]` and allows one out per owner; a soldier's shot is owned by the commander, so it tethered to the commander and blocked the commander's own harpoon. The classifier now rejects `aiStyle` 13 and grappling hooks (7), and `ItemID.Harpoon` explicitly |
| Hellwing Bow | bats curve toward the aim vector | C | arrow -> bat swap was already in `ResolveProjectile`; `ItemShots.Hellwing` sets ai[0..1] (vanilla chain item 3019). Without them the bat's velocity decays to zero |
| Molten Fury | wooden arrows become fire arrows | C | `ResolveProjectile` (vanilla: `projToShoot == 1` -> 2 before the hooks) |
| Bee's Knees | arrows become bee arrows | A | already in `ResolveProjectile` |
| Hellwing / Shadowflame / Skeleton / Fairy Queen / Blood Rain bows, Molten Quiver | arrow swaps | A | `ResolveProjectile` |
| Daedalus Stormbow, Blood Rain Bow | arrows rain from the sky | A | `RainShot` in `DefaultShoot` |
| Sniper Rifle, Venus Magnum, Uzi (hardmode) | bullets become high-velocity bullets | C | `ResolveProjectile` (vanilla: `projToShoot == 14` -> 242) |
| Shotgun, Boomstick | several pellets with spread | A | `LevyConstants.Multishot` (counts fixed, vanilla randomizes 4-5 / 3-4) |
| Minishark, Megashark | random inaccuracy | A | cosmetic; soldier aim error plays that role |
| Musket, Phoenix Blaster, bows, Star Cannon, Flare Gun | plain shot | A | ammo path through `TryRealShoot` |
| Bee Gun | 1-4 bees per cast (more with Hive Pack), magic damage | C | `ItemShots.BeeGun` (vanilla chain item 1121) |
| Crimson Rod (Nimbus Rod, hardmode) | rain cloud flies to a point and rains | C | `ItemShots`: ai[0..1] = the target position (vanilla: the cursor). Before, the cloud flew to the world origin |
| Ice Rod | ice block flies to a tile | C | `ItemShots`: ai[0..1] = target tile. It places Ice Block tiles where it ends, as in vanilla |
| Blizzard Staff | two hail shards fall from the sky onto the enemy | C | `ItemShots.BlizzardStaff` (vanilla chain item 1931, projectile 337 `aiStyle` 1). Spawn 600 px above the soldier, X = halfway between (target + 0..200 toward the back) and the soldier +-200, the second shard 100 px higher, speed 10 toward the target (always downward, min 20 px fall) +-0.8 scatter, ai[1] = frame 0..4. The shard only starts colliding with terrain below 300 px above the commander (it reads `Main.player[owner]`), so a soldier with no open sky above fires the plain shot instead. Before, it left the staff like a bolt |
| Meteor Staff | a meteor falls from the sky onto the enemy | C | `ItemShots.MeteorStaff` (vanilla chain item 2750, projectile 424..426 `aiStyle` 1, picked at random). Same spawn as the Blizzard Staff with one projectile, speed x0.75, ai[1] = scale 0.5..0.8 (scale 0 without it); same open-sky fallback. Before, it left the staff with scale 0 |
| Lunar Flare | three flares fall from the sky onto the enemy | C | `ItemShots.LunarFlare` (vanilla chain item 3570, projectile 645). Same spawn as the Blizzard Staff, three flares at half speed, ai[1] = target height (they ignore terrain above it) |
| Vilethorn, Space Gun, Water Bolt, Demon Scythe, gem staves, Wand of Sparking, Flower of Fire, Book of Skulls, Aqua Scepter | spawn their own projectile | A | none of them has a branch in the `ItemCheck_Shoot` chain; fired through `FireMagic` -> `TryRealShoot` |
| Magic Missile, Flamelash, Rainbow Rod | cursor-guided projectile | D | channeled (`item.channel`); the classifier rejects them because the guiding reads the commander's cursor |
| Throwing Knife, Poisoned Knife, Shuriken, Javelins, Bone Javelin | thrown consumables | D | consumable items are rejected by the classifier |
| Sky Fracture, Star Wrath, Phantasm, Zenith, other hardmode chain items | per-item spawn patterns | D | out of the prehardmode scope; they fire a plain projectile |
| Summon staves (Slime Staff, Finch Staff ...) | minions | A | `SoldierMinion`, not part of this path |

## Sword-beam sanity check

`StartSwing` runs `TryRealShoot` whenever `item.shoot > 0` and the projectile is not a held slash. Checked against vanilla item definitions: Enchanted Sword (shoot 173), Ice Blade (118), Beam Sword (116), Frostbrand (119) fire normally. Starfury, Light's Bane and Blade of Grass fired a wrong shot (fixed in `ItemShots`). Night's Edge, Excalibur, True Excalibur, True Night's Edge, Terra Blade and The Horseman's Blade have `noMelee` and a held-slash projectile: they were classified as plain shooters (broken) and are now broadswords.
