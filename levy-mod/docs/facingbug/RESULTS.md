# A levy's lash, swing and shot read the levy, not the commander (facing fix, 2026-11-20)

The PvP lab (`lab/pvp/RESULTS.md`, branch `pvp`) found that the commander's facing changed what her levies' shots and lashes did: with her facing right the army on the right out-damaged the army on the left in mirrored fights and won 10 of 12; facing left, the left army won 6 of 6. The lab cancelled it in its harness (the commander faced each levy's way during its update, and turned every tick). In play nothing did.

This is what was measured, what was changed, and what was not shown.

## OBSERVED

**Every levy shot is owned by the commander's player slot** (`WeaponUse.OwnerFor`), or by slot 255 for a held weapon's chain. Code that reads "the owner" (`Main.player[projectile.owner]`: its `direction`, `Center`, `velocity`) therefore reads whoever stands in that slot at that moment. Levy already lends the levy's transform to the owner for a shot's `Update`, `Damage` and `Kill` (`OwnerPuppet`), a lash lends more for its hit (`RealWhips.DamageScope`), and a lash's AI and the vanilla body of a held weapon run with the levy's dummy in slot 255. The new world check (`FacingWorldCheck`, `[FacingWorld]` lines, chained after the whip check in the ship world) puts one levy in a flat arena with a pinned enemy in front of it and the commander 32 tiles behind, and runs the same weapon with the commander facing right and then left, for a levy facing right and a levy facing left (same random seed per pair). At every point the game hands a levy's shot or strike to code that reads its owner (spawn, AI before and after, hit test, hit modifiers, hit, death, strike) it records whether the owner it finds faces the levy's way and stands at the levy.

With the fix off (`LEVY_FACINGFIX=0`), same build, same check:

| hook | vanilla half: owner facing the wrong way | Calamity half |
|---|---|---|
| spawn | 187 of 1751 | 608 of 6528 |
| AI (before and after) | 9809 of 48393 | 9809 of 454919 |
| hit test (canHit) | 6450 of 23319 | 3780 of 44062 |
| hit modifiers and on-hit | 835 of 3606 | 327 of 6073 |
| kill | 0 of 1697 | 100 of 6231 |
| strike on the enemy | 891 of 3716 | 463 of 6345 |
| check result | 355 pass, **33 FAIL** | 729 pass, **73 FAIL** |

With the fix on: 0 wrong in every row, in both halves (388 pass, 0 fail; 832 pass, 0 fail).

Where the commander (or the empty player of slot 255) was found, by weapon (vanilla half, fix off; wrong / seen, both commander facings summed):

- **Spawn** (everything the levy makes in its own AI pass): Enchanted Sword's beam 26/52, Spear 38/74, Blue Moon 24/48, Cascade 4/8, Enchanted Boomerang 16/32, Musket 36/72, Amethyst Staff 31/63. Calamity: Brimblade, Celestus, Molten Amputator, Stormfront Razor, Terratomere, Brimstone Sword, Aquamarine Staff, Skytide Dragoon, Devil's Devastation: 12 to 104 each.
- **Melee swing strike**: Iron Broadsword 56/112, Brimstone Sword 40/229, Tainted Blade 40/80. The commander faced the other way at 1 in 2 swings.
- **Held weapon, hit pass** (Last Prism's beams, Ark of the Elements and Cosmos blades, Grand Dad): canHit, modifyHit, onHit and strike all read the commander (Last Prism 6450/12900, 835/1673, 835/1673, 835/1671).
- **Held weapon, AI hooks**: the PreAI and PostAI of every global projectile hook and a modded child's own AI ran with slot 255 EMPTY (the null player: facing right, standing at the world's corner): Last Prism 7581/15134, Laser Machinegun 1113/7086, Phantasm 1115/5986. That one is not about the commander at all: it made every left-facing levy's held weapon read "facing right" while a right-facing levy's read correctly. Its death (kill) read it too (the Arks of the Elements and the Cosmos: 52 of 304 and 48 of 393).

**Already right (0 wrong, fix off):** a non-chain shot's AI, hit and death (`OwnerPuppet`), a lash's AI (`AiScope`) and its hit (`DamageScope`), the Zenith swords, the Lunar sentries, and Levy's own projectile types (`SoldierWhip`, `SoldierThrust`, `SoldierFlail`, `SoldierYoyo`, `SoldierBoomerang`: none reads a player).

## What did not show: an outcome

In the check's arena, no weapon's damage, hit count, hit position or knockback direction depended on the commander's facing with the fix off, apart from noise. A sweep of every damaging Calamity weapon the army can use (`LEVY_FACING_SWEEP=CalamityMod`, 614 candidates, 564 hit the pinned enemy, fix off): 521 read an owner facing the wrong way at some hook, and 4 showed a damage ratio under 0.6 between the two commander facings. None of the four follows the facing. Each follows the ORDER the two commander facings were run in (the check runs them one after the other, and a weapon's burst counter lives on the commander's Calamity player, which carries from one scene into the next): Pwnagehammer 907 against 2053 (commander right first), then 1766 against 1762 with the order reversed and 1757 against 1731 forward again (both with the fix on); Galaxy Smasher, the first scene of the pair is the low one (10924 against 35330 with her facing right first, 10226 against 34903 with her facing left first: no big burst fired in it, 28 hits against 36), the same with the fix on; The Dance of Light, the first scene of the pair is the high one (98526 with her facing right first, 88322 with her facing left first); Nebulous Cataclysm 0.60 and 0.66, then 0.88 and 0.96 in a second run. The levy's own 2400-tick army sweep of the ship world (`[IdentityWorld] army sweep`) shows the same kind of run-to-run spread on Infested Clawmerang with the fix on and off (idle ratio 0.59 to 0.98 on, 0.70 to 0.91 off; in the facing arena over 1500 ticks 1312, 1313, 1319, 1308 on against 1312, 1316, 1308, 1315 off).

So the mechanism the lab found is real and wide, and it is closed at every point the check could reach, but **the 2 to 3 times damage split of the lab was not reproduced by any single weapon in this arena**. INFERRED, not tested: it comes from something the arena does not contain (two armies hitting each other: the levy as the target of a commander-owned shot; a weapon of a mod outside vanilla and Calamity; a ModPlayer of the commander that reads her facing). A mirrored army-against-army run on the fixed build with the lab's own facing correction switched off (`LEVY_PVP_OWNERDIR=0`) is the test that would show it, and it needs the `pvp` branch's harness.

## What changed

All of it is one idea: wherever a levy's own code runs a weapon, the owner is lent the levy's facing and place for the length of the call, with the lend Levy already has (`OwnerPuppet` frames, which put the commander's transform back exactly) or the dummy swap Levy already uses (`EnemyTargeting.Swap`, slot 255).

1. `OwnerPuppet.LendTransform(body)`: the shot puppet's transform lend (`Center`, velocity, direction), for a body's use instead of a projectile's pass. Used around
   - every `WeaponUse.Spawn` (thrust, flail, yoyo, boomerang, whip fallback, sword beams, everything the levy spawns itself);
   - the whole of `WeaponUse.TryRealShoot` (the weapon's `ModifyShootStats` and `Shoot` hooks: guns, bows, staves, rogue weapons, modded weapons);
   - a melee swing's hooks and strike (`SoldierNPC.ProcessSwing`, `SwingStartTick`);
   - a held weapon's beam's hit pass (`HeldWeapons.RunDamage`; a lash keeps its own `DamageScope`).
2. `HeldWeapons.OpenOuterSwap`: the held-weapon wrapper stood on the vanilla AI body (and on a lash's or a modded holdout's whole call). It now stands on the whole `ProjectileLoader.ProjectileAI` call of every chain projectile, so every mod's PreAI and PostAI and a modded child's AI find the levy's dummy in slot 255; `RunAi` sees it already there and leaves it.
3. `HeldWeaponSystem.KillDetour`: a chain projectile's death runs with the dummy in slot 255.
4. `HeldWeapons.TryFire`: a vanilla held weapon's holder is spawned with the dummy in slot 255 (its spawn hooks read the slot).

`LEVY_FACINGFIX=0` turns 1 to 4 off (lab A/B).

## Not changed (known)

- A shot still flying after its levy died or was dismissed is not puppeted (`OwnerPuppet.Puppetable` needs a live body): it reads the commander's facing. A shot remembers no facing of its own.
- On a dedicated server the army's shots are owned by slot 255, which is not a real player (`OwnerPuppet` needs one); not testable here (the world checks run the single-player owner).
- Officers, summoned units, Mechanization's machines: their own owner reads are not weapon use (a unit's rows read the commander's facing on purpose: `SummonedUnitNPC`, `SoldierNPC.Brain` follow slots).
- Reads of the commander's ModPlayer state (`Main.player[owner].Calamity().X`) are hers by design; they are not facing.

## Run it

- In the ship: `[FacingWorld]` in `tools/ship/shipworld.sh` (both halves). Alone: `LEVY_SELFCHECK_WORLD=1 LEVY_FACING_ONLY=1`; `LEVY_FACING_FILTER=name/name` narrows, `LEVY_FACING_TICKS=n` sets the window, `LEVY_FACING_DEBUG=1` prints the first wrong reads with their stack, `LEVY_FACING_REVERSE=1` runs the commander facings in the other order.
- The lab sweep (not in the ship): `LEVY_FACING_SWEEP=CalamityMod` (or `Terraria`, or any mod name), `LEVY_FACING_SWEEPTICKS=n`.
- The before: the same commands with `LEVY_FACINGFIX=0`.
