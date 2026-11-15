# PARITY AUDIT: where a levy is not treated like a player

Principle (NORTH_STAR): the army plays by the same rules as the player: same damage taken, same damage dealt, same effects. Difficulty comes from the enemy.

Scope: audit only (find, measure, rank). Nothing here was fixed. Gap 0 (hostile projectiles) is already being fixed in branch `projparity` and is listed only for context and numbers.

Method and honesty about evidence:
- Recordings mined: 184 battery recordings (`hmbattery2/recordings`, Master+Death, Calamity) and Eve's 30 real-game recordings (Master+Death, Calamity + NoxusBoss + Fargo etc., commander defense 47 to 65, levies "Levy" grade). Scripts are in `levy-mod/tools/parityaudit/` (`lhit.py` per-source damage, `lbuf.py` buffs, `cmpcmd.py` commander vs levy hits, `other.py`, `anah.py`).
- Code claims cite file:line on `worktree-levy-stardust` at 6058879.
- No tML probe was run (cores shared by five builders). Where a claim rests on code reading alone it says "code only"; each has a one-probe verification listed.
- Not covered in depth: mobility (dash, grapple, mounts) and damage dealt by class (see gaps 9 and 12).

## Ranked gaps

### 1. Debuffs and damage over time never reach a levy (largest, never noticed)
What differs: a player hit by a boss gets the boss's debuffs (Poisoned, Cursed Inferno, Frostburn, Wind Chilled, Brimstone Flames, Crush Depth, Weak, Slow, Frozen, Stoned, no-heal debuffs, defense-down). A levy gets none. Two independent causes:
1. Contact: `SoldierNPC.ContactDamage` (Content/NPCs/SoldierNPC.Damage.cs:72-105) strikes with `NPC.SimpleStrikeNPC`. It never calls `NPCLoader.OnHitPlayer` / `ModifyHitPlayer` / `PlayerLoader.OnHitByNPC`, which is where vanilla and Calamity bosses call `target.AddBuff` (grep: no `OnHitPlayer` call anywhere in Levy outside the test harness; `HostileContact.cs:100` only runs `CanHitPlayer`). Hostile projectiles reach `ModProjectile.OnHitNPC`, not `OnHitPlayer`, so projectile debuffs are lost the same way.
2. Immunity: with Calamity loaded, `SoldierNPC` has `ActsLikeTownNPC` (SoldierNPC.cs:134), so `NPCStats.SetDebuffImmunities` (CalamityMod/Utilities/NPCDebuffs.cs:113-116) sets `ImmuneToRegularBuffs`. `AllowFriendlyBuffs` (SoldierNPC.cs:168-171) clears immunity for non-debuffs only; the comment says debuffs "keep whatever immunity the load gave him". So even an environmental `NPC.AddBuff(OnFire)` (TileHazards.cs:45-46, lava) is refused (code only).

Evidence: across all 214 recordings, `lbuf` (every NPC buff on every levy, written on change, FightRecorder.cs:1089-1107) contains only good buffs (Ironskin, Regeneration, Chi, Well Fed, Dryad's Blessing, Morale, Honey marker) and zero debuffs. In the same Eve recordings the commander carried Poisoned (302 samples), Acid Venom (31), Wind Chilled (8), On Fire! (7), Cursed Inferno (6), Wet (37), Big Suck. Bosses fought include Plantera, Anahita, Empress, Retinazer.
Fight impact: high and boss-specific. Levies are immune to every boss status effect: no stun/slow lock-down (Frozen, Webbed, Stoned, Chilled), no DoT attrition, no weakness or defense-down, no anti-heal. Every Calamity boss whose design leans on debuffs (Cryogen, Anahita, Aquatic Scourge, Brimstone Elemental, Calamitas) is meaningfully softer on the army than on the player. It also makes mixed fights inconsistent: the commander is debuffed, the army is not.
Proposed fix (the HostileContact way, run the player's own sequence through the dummy):
- After a landed contact hit, call `NPCLoader.OnHitPlayer(a, dummy, hurtInfo)` and `PlayerLoader.OnHitByNPC` on the dummy inside the existing frame slot / IdentityGuard window (as `GearOnHurt`, SoldierNPC.GearUpkeep.cs:318-360, already does for Player.Hurt). For projectiles, `PlayerLoader.OnHitByProjectile` plus the projectile's `OnHitPlayer` equivalents.
- Collect the buffs the dummy gained (`dummy.buffType/buffTime`, honouring the dummy's own `buffImmune` from gear such as Obsidian Skull, Ankh) and move them to the NPC with debuff immunity cleared for those types only (as MarkAmpGlobalNPC.cs:68 and TeamItems.cs:316 already do for single buffs).
- Make debuff effects real: modded debuffs already run `Update(Player)` on the dummy inside `GearFrame` (SoldierNPC.GearUpkeep.cs:160-166) so stat debuffs work; DoT needs a conversion: negative `d.lifeRegen` (currently only positive is kept, GearUpkeep.cs:185-187) must be applied through a player-rule DoT tick that bypasses defense as the player's does. Do NOT rely on NPC `UpdateLifeRegen`, whose DoT numbers differ per mod.
- Debuff-immunity gear then works as for the player; clearing is by Nurse/potion/time as for any buff.
Verify: harness fight against Cryogen/Plantera with `lbuf` showing debuffs; unit probe per boss: levy and commander stand in the same hitbox and the same buff set (type, time) must result. One probe: spawn a levy under Calamity, `NPC.AddBuff(BuffID.OnFire)`, assert it sticks after the fix.

### 0. (context, in progress in `projparity`) Hostile projectiles use the NPC damage path
Numbers for the record: battery recordings, boss projectile hits on levies: 25,983 hits, mean 9.1 damage, versus boss contact 9,831 hits, mean 142.6. The commander took 50 to 190 from the same projectiles (Spore Gas 96 median, Homing Dart 99, Thorn Ball 99, Meteor Vomit 191, Musical Note 125) while levies took medians of 1 to 14 (Spore Bulb Gas: commander 70, levy 1; Thorn Ball Spike: commander 70, levy 4.5; Hive Bomb 1; Prismatic Bolt 3; Ethereal Lance 4). Projectiles are about 14 percent of the damage levies take today and would plausibly become the majority once they hit at player strength. The doc `Damage.cs:22-33` explains the ×2 patch (`HostileProjectileDamageMult`) that is not enough in Death mode.

### 2. Contact damage per hit is 3 to 7 times the commander's for the same boss (unexplained, needs measurement)
Same boss, same recording set (Eve, Master+Death), contact (`src body`) median damage per hit, commander versus levy:

| Source | Commander (n, median) | Levy (n, median) |
|---|---|---|
| Anahita | 12, 166 | 12,629, 877 |
| Plantera's Tentacle | 18, 139 | 149, 281 |
| The Leviathan | 11, 256 | 10,165, 546 |
| Detonating Bubble | 8, 167 | 8,772, 747 |
| Retinazer | 5, 157 | 2,401, 898 |
| Spazmatism | 2, 120 | 9,604, 429 |
| Spore | 5, 156 | 933, 1,134 |

Levy defense is minimal by design (NORTH_STAR), so some gap is expected, and the commander has 47 to 65 defense plus Calamity DR. But the Master defense term is at most `defense x 1.0` (LevyUtil.cs:49-54, SoldierNPC.cs:501-510), which cannot explain 5.7x on Retinazer (157 versus 898) unless the levy's effective defense is near zero and the commander's DR is large. Candidates, to be separated by one measurement: (a) commander DR (Calamity endurance, Limits conversion `1 - 1/(1+e)`, adrenaline) versus levy `Endurance` capped at 0.9; (b) gap 4 (Calamity hit hooks not run); (c) levy damage taken uses `a.damage * mult` while the player path also goes through `Player.Hurt` `modifiers` (ModifyHitByNPC, CalculateDamagePlayersTake in `Main`); (d) levy HP pool is large in Eve's army (avgLife 346 to 864), so overkill hits are legitimately large and are not limited by HP.
Fight impact: potentially the single biggest quantity, since body contact is 81 percent of all damage levies take in the battery set (1.40M of 1.73M).
Proposed fix: the HitParity harness (`CombatHarness.HitParity.cs`) already compares hit decisions with a Player; extend it to compare the damage number: for each landed contact, also run `Player.Hurt` calculation on the dummy with the same gear (`Player.HurtModifiers` through `PlayerLoader.ModifyHitByNPC`/`ModifyHurt`) and log `levyDamage / dummyDamage`. If the ratio is not 1.0, make the dummy's number the strike's damage (the strike then bypasses `ModifyIncomingHit`) so the levy takes exactly the number a player with that gear takes.
Verify: ratio 1.00 ± rounding across the boss set; recordings show levy medians close to commander medians once defense differences are accounted for.

### 3. Calamity (and other mod) damage-taken hooks are not run for a levy (code only)
What differs: `SoldierNPC.ModifyIncomingHit` (SoldierNPC.cs:501-510) applies only: defense, `Endurance`, Calamity adrenaline (`Rippers_IncomingMult`), formation DR, set scales and banner. A player additionally runs `ModPlayer.ModifyHitByNPC` / `ModifyHitByProjectile` / `ModifyHurt` / `FreeDodge` / `ConsumableDodge` / `PreKill`. For Calamity (CalPlayer/CalamityPlayerHitHurt.cs): Tarragon cloak and Bloodflare frenzy contact DR (lines 705-709), The Bee 50% DR (718-722), Relic of Convergence and Lion Heart shells, energy shields that absorb hits (Rover Drive, Lunic Corps, Profaned Soul Artifact, The Sponge, lines 1660-1760), parries (Divine Providence, Flame-Licked Shell, 1550-1650), dodge hooks (1314-1490), `ModifyHitByNPC` damage-source modifiers such as Whispering Death on the attacker (684-700). `IdentityGuard.cs:47` lists these hooks as guarded names, i.e. known to be unreachable. `GearOnHurt` runs `Player.Hurt` after the damage is already dealt, so these hooks run on a dummy copy whose result is discarded (only a heal is written back).
Fight impact: medium to high for levies wearing Calamity armor and accessories (all of Eve's recorded levies carry modded gear; the doc `lgear` events show mod item ids). Their defensive gear is partly decorative, while the attacker side of the same gear (damage) works. Today levies are penalised.
Proposed fix: run the player's hurt calculation on the dummy before applying the damage: build `Player.HurtModifiers` from the hit, call `PlayerLoader.ModifyHitByNPC` then `ModifyHurt`, honour cancel / dodge results, and use `hurtInfo.Damage` as the strike. This is the same fix as gap 2 and should be one change.
Verify: levy wearing a Rover Drive / Tarragon set takes the same number as the dummy-computed player value; a Sponge shield absorbs.

### 4. Per-slot immunity for projectiles is folded into one window
`SoldierProjectileImmunity.CanBeHitByProjectile` (SoldierNPC.Damage.cs:426-433) refuses every hostile projectile while `ImmuneTime > 0`. A player's hit from a projectile with its own immunity slot (Moon Lord and Empress projectiles use `ImmunityCooldownID.Bosses` slot 1) is not blocked by a general window opened by something else, and the Empress's hits themselves do not extend the general window. Contact already does this correctly (HostileContact.Slots, Damage.cs:62,85). The code comment at line 417-421 states the fold is deliberate.
Evidence: Empress of Light projectile hits on levies (battery): Sun Dance 854, Ethereal Lance 1,381, Prismatic Bolt 1,217, Everlasting Rainbow 886 hits, mean 6 to 14 each. A player taking a Prismatic Bolt while still in a contact window would be hit; a levy is not.
Fight impact: moderate, boss-specific (Empress, Moon Lord): the army takes fewer simultaneous hits than a player would.
Fix: in `CanBeHitByProjectile` and `AfterHit`, use the slot for projectiles that vanilla assigns a cooldown slot to (read `projectile.ModProjectile.CanHitPlayer`'s cooldown through `ProjectileLoader.CanHitPlayer`... the same `ref int cooldownSlot` the player path uses), as `HostileContact.EffectiveSlot` does.
Verify: Empress fight recording: hit-gap histogram per levy (HitGapDbg already exists, Damage.cs:125) shows hits inside a 40-tick window that vary by slot.

### 5. No knockback taken
Contact strike passes knockback 0 unless the body is Fizzy (Damage.cs:95); `knockBackResist = 0` (SoldierNPC.cs:150). Hostile projectile knockback does nothing. A player is knocked back by every hit unless the gear says otherwise (Cobalt Shield, Frozen Turtle, Calamity's Calcium: `modifiers.Knockback`, CalamityPlayerHitHurt.cs:1500-1501). Levies hold their line against a charging boss, never get shoved out of an attack, and cannot be knocked off a platform or into lava.
Fight impact: low to medium. Mostly a positioning/stagger effect, and it also makes dash-boss hits (Leviathan) land with fewer follow-up hits on the same levy because the levy is not knocked clear.
Fix: pass the player's knockback (`npc.knockBackStopsMovement`-style value from `Player.HurtModifiers.Knockback`) with `dummy.noKnockback` honoured; reuse the Fizz machinery (SoldierNPC.Fizz.cs) which already moves a body on knockback. Needs the knockback to feed `Movement` so the brain does not cancel it. Verify: Leviathan fight, levy displacement after a hit versus the dummy's computed knockback.

### 6. Environmental hazards default off or are absent
- Fall damage and tile hazards (spikes, thorns, meteorite, hellstone): config `SoldiersTakeFallDamage`, `SoldiersTakeTileHazards`, both `DefaultValue(false)` (LevyConfig.cs:81-82). Players always take them. Levies are immune by default.
- Drowning and suffocation: not implemented (Damage.cs / TileHazards.cs:42 skip `Suffocate`; the only mention of drowning is a reserved killer code, LevyConstants.Roster.cs:42). A levy never drowns in deep water or the abyss; Calamity's Abyss breath mechanic does not apply.
- Lava: vanilla `Collision_LavaCollision` strikes 50 every 30 ticks, with `immune[255]=30` (Damage.cs:207-211, `ImmuneTicksOnHit`); the player's lava hit scales with hardmode and goes through Player.Hurt with fire-walk and Obsidian rules. Levy lava damage is a flat NPC strike (code only; check number against Player).
- "other / world" damage in the recordings is unattributed damage (no npc, no projectile): battery 932 hits, mean 93, 5 percent of damage taken; Eve 10,751 hits (almost all from one anomaly, gap 10). Hit gaps of 30 ticks (the lava window) show in battery data.
Fight impact: low in boss arenas, high in travel and terrain play. The defaults are a deliberate choice (config), so this is a "decide" item.
Fix: if parity is the goal, default both on and add the missing drowning/suffocation through `Player`'s own rules. Verify with the existing fall and hazard harness checks.

### 7. Healing and regeneration differ in both directions
- Natural regen: flat 1 HP per 60 ticks after 5 s without damage (LevyConstants.cs:87-88, SoldierNPC.Damage.cs:332-347). A player's `lifeRegen` scales with max HP and standing still (order of 1 to 4 HP/s at 500 HP, code only).
- Potions: levy drinks below 50 percent with a 30 s cooldown (LevyConstants.cs:84-86); the player has a Potion Sickness window of 60 s in Calamity worlds and can drink at will. Levy cadence is therefore better than a player's during a long fight.
- Heal-over-time from buffs: only positive `lifeRegen` from gear and buffs is kept (GearUpkeep.cs:185-187), and `StationRegen` uses 1 HP per 120 units.
- Life steal: pooled (LifeStealPool.cs); not compared.
Fight impact: low individually; the sum of the two directions roughly cancels on long fights but not on burst fights. Fix: compute natural regen with the player's own formula on the dummy (`Player.UpdateLifeRegen` is guarded in IdentityGuard.cs:42, so it needs a tiny re-implementation or a guarded call), and give potions the same sickness. Verify by a 60 s no-damage heal curve of levy versus commander with the same max HP.

### 8. Vanilla-only gear skips Calamity's player pass
`ComputeUpkeepNeed` (SoldierNPC.GearUpkeep.cs:49-60) switches the per-tick gear frame on only for modded armor/accessories or a short vanilla list. A levy in vanilla-only gear under Calamity therefore never runs `PostUpdateMiscEffects`, which is where Calamity converts DR (`endurance = 1 - 1/(1+e)`, CalamityPlayerMiscEffects.cs:4880-4883) and applies the stat clamps and Calamity's own passive effects. Result: a vanilla levy's Endurance Potion (0.10) keeps 10 percent instead of Calamity's 9 percent; small. The bigger effect is that Calamity-side defense damage (CalamityPlayerHitHurt.cs:1944-2000) is only dealt through `GearOnHurt` when the frame is active. Fight impact: low; fix: run the frame whenever Calamity is loaded (cost: perf, measured by LevyProf).

### 9. Damage dealt: hidden multipliers in both directions (not measurable from recordings)
Found by code:
- `GradeDamageMult = {0.8, 0.9, 1.0}` (LevyConstants.cs:27): a plain Levy deals 80 percent of the gear's damage. Eve's army is all "Levy" grade.
- `ShakenDamageMult = 0.6` while Shaken by fear (LevyConstants.cs:35); `SoldierDamageMultiplier` config (default 1).
- Aim handicap by grade: jitter 6/3/1 degrees, lead error 25/10/3 percent (LevyConstants.cs:29-30).
- `DamageMultiplier` is applied to `WeaponDamage` everywhere (SoldierNPC.cs:118; Summon.cs:175, 393; Combat.cs:358; ArmorSets.cs; AccessoryProc.cs:99).
These are designed progression systems (grades cost command points); they are listed as "levy is not a player" by design. If strict parity is wanted, grades should be expressed as aim and HP, not damage.
- `WhipRealDamageMult = 4.0` (LevyConstants.Summon.cs:114; RealWhips.cs:90): a levy's real whip lash is the weapon damage times 4. The comment says it was calibrated so the real lash matches the old hand-made lash's DPS (the real lash did 31 to 41 percent per second at 1.0), and the old lash itself had 1.35x damage, a crit every hit, and a 0.75 cadence. A player whip does 1.0x. So 4.0 is a compensation for something else (tag application, cadence, or hit-count) and it is no longer needed in principle: the lash should be 1.0x and the cadence/hit-count difference found and fixed instead. Recommendation: remove the multiplier only together with a measured sweep (WhipRealCheck) of dummy-versus-commander whip DPS; do not remove it blind, or whips become the weakest class.
- Not measured: class multipliers, set bonuses and crits for each damage class versus the commander with the same weapon. The `dmg` events give damage per second by class but not equal weapons. Proposed harness: give the commander and a levy the same weapon and gear, hit a dummy target 30 s, compare `dmg` totals per class.

### 10. Anomaly in Eve's Anahita recording
`2026-11-14_16-24_Anahita_won.levyrec`: 7,400 `lhit` events with `src other` for id -1, three strikes per tick (damage 1, 1 and 126), same position, for 190 s, with life constant at 3248 / 3247 / 3122. Total 430,940 damage and no death. This is a bookkeeping loop (a body repeatedly struck and healed; possibly an officer or conjured unit hit by an aura and healed by `Overhealth`/regen) or a recorder double count, not a real source. Excluded from the comparisons. Needs a look: if real, it is a levy-side damage and heal loop that costs performance and fires hit hooks 60 times a second.

### 11. Boss-side systems that treat the army differently (designed, not bugs)
- Boss HP scales +2 percent per command point, capped at +80 percent (`HpScalePerCp`, `HpScaleCap`, LevyConstants.cs:170-171). More levies means a longer fight, which multiplies every gap above: a levy that cannot be debuffed and takes soft projectile hits fights longer than intended; fixing gaps 0 to 2 makes the same HP scale a harder fight than the current tuning assumes, so retune after the fixes.
- Spite and fear: bosses redirect shots at a levy that is close or does a lot of damage (tkind 3 in the recordings); fear (`SoldierNPC.Fear.cs`) makes levies Shaken (0.6 damage, 2x aim error). Flee-when-dead rule for bosses (NORTH_STAR). These interact with gap 0: spite shots at a levy were doing 1 to 14 damage each.
- `ModifyIncomingHit` also applies a "formation defense" DR (config `LevyOptions.FormationDr`, off by default; SoldierNPC.cs:508). Keep it off for parity.

### 12. Mobility (not found, not proven)
Reading only: wings and boots slots just landed (Common/Movement/MobilitySlots.cs, AccessoryMobility.cs); hooks via HookSystem; movement speed uses `Dummy.moveSpeed` clamped to at least 0.5 (SoldierNPC.cs:359). No gap was verified. Open question to probe: a levy never has `Player.dash` states hurt-immune (Shield of Cthulhu, `HostileContact` handles `eocDash` for exemption only), mounts are absent. Not ranked.

## Suggested order
1. Land `projparity` (gap 0).
2. One change for gaps 1, 2, 3: run the player's whole hit sequence (OnHitPlayer, ModifyHitByNPC/Projectile, ModifyHurt, dodges, OnHitByNPC) on the dummy, strike with the dummy's number, and move debuffs to the NPC. Measure first with the HitParity extension from gap 2 (a ratio log, no behavior change).
3. Slots for projectiles (gap 4), knockback (gap 5).
4. Decide the environmental defaults (gap 6), regen (7).
5. Re-measure fight length after 1 to 3 and retune `HpScalePerCp`.

## Status of gaps 6, 7, 8 and 9 (branch `levyrules`)

Decided by Eve and done. The `[LevyRules]` world check (Common/Testing/LevyRulesCheck*.cs, chained in shipworld) holds each of them against a real Player in a spare slot.
- Gap 7, regeneration and potions: a levy's life regeneration is `Player.UpdateLifeRegen` on its dummy (Content/NPCs/SoldierNPC.Healing.cs): 30 s from a fresh wound it heals within a few HP of a Player with the same gear (gear regeneration, Regeneration potion, Well Fed, Shiny Stone, Crimson armor, Campfire, Heart Lantern). Vanilla's own friendly-NPC regeneration (`NPC.CheckLifeRegen`, 1 HP every 181 ticks) is switched off for levies. Healing potions bring the potion sickness the dummy's `Player.ApplyPotionDelay` gives (60 s, 45 s for a Restoration Potion, 0.75x with a Philosopher's Stone); a mana potion brings none. The barracks bonus (2x natural regeneration for a stood-down levy at home) stays. Well Fed no longer adds regeneration of its own (vanilla's Well Fed has none; it lifts the Expert and Master halving through `Player.wellFed`, which the dummy now carries).
- Gap 6, environment: fall damage and tile hazards are on by default; a new option, "Levies can drown" (off by default), runs `Player.CheckDrowning` on the dummy.
- Gap 9, damage dealt: the grade damage table is gone (a Levy, a Man-at-Arms and a Champion with one weapon deal the same); Shaken stays 0.6 and `WhipRealDamageMult` stays 4.0.
- Gap 8, vanilla-only gear under Calamity: with Calamity loaded the stat pass runs the gear frame once a second for every levy (RefreshStats), so Calamity's PostUpdateEquips and PostUpdateMiscEffects shape every levy's numbers. The per-tick frame is still only for gear whose effects work across ticks.
- Found on the way: (1) the dummy's life and mana maximum fell back to 100 and 20 at every stat pass (ResetEffects rebuilds them from the consumed crystals and fruit), while its life stayed the body's, so every effect reading life as a fraction of the maximum saw a levy at several hundred percent; the dummy now carries the record's crystals and fruit. (2) Calamity's reworked vanilla armor sets look the wearer up through `Item.playerIndexTheItemIsReservedFor`; a levy's items carried the commander's slot in single player, so those sets were judged on the commander's armor. EquipDummy stamps the dummy's slot on worn items.
- Whips: a summoner levy lashes at the whip's own rate (a Player's best: one lash per use animation) whenever an enemy is inside the lash. What held it back was the brain: the whip lashed only the enemy it had chosen as its target, and a boss it will not charge (or any enemy while the body was following, patrolling or walking home) never became one. A whip in hand now lashes any hostile inside its reach in any state but dead, deserted, mutinous or resupplying.
