# Loot audit (Calamity world, Underworld and Hallow)

Run: `LEVY_LOOTAUDIT=1` on a dedicated-server boot with a world; log lines start `[LootAudit]` and end `self-check pass=N fail=M`. The kill procedure is `Common/Testing/LootAuditCore.cs` and `LootAuditScenarios.cs`, which have no Levy dependency; the same two files compiled into a stand-alone probe mod give the "Levy not loaded" arm.

Observed (150 kills per cell, 44 cells per killer, classic and expert+Revengeance/Death, pre-hardmode and hardmode, Demon, Voodoo Demon, Fire Imp, Lava Slime, Hellbat, Lavabat, Bone Serpent, Red Devil, Pixie, Unicorn, Gastropod, Chaos Elemental, Enchanted Sword, Illuminant Slime and Bat):

- NPCLoot and OnKill calls per kill: exactly 1.00 in every cell, player-credited and army-credited.
- Weapons per kill: Levy not loaded 0.0259, Levy loaded player kills 0.0208, 20-levy army kills 0.0247 (same within Poisson noise). Coins per kill 1583 / 1588 / 1586 copper.
- Calamity's Bladecrest Oathsword drops from a plain Underworld Demon at about 2.5 percent with Levy not loaded. It is Calamity's own drop.
- Army loot bonus (off by default): 5 percent per command point at 20 command points doubles boss material drops (Soul of Fright per Skeletron Prime kill 33.1 to 65.6, x1.98); it never touches a non-boss NPC.
- Spawner (Levy's hook, input 1 in 600 / cap 5): no event 1 in 600 / 5; any event with 20 command points 1 in 272 / 11 (x2.2 rate, x2.2 cap); with Events spawn more with an army off, 1 in 600 / 5 in every case.
