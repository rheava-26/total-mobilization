# Calamity and Thorium armor-set audit with ModPlayer fields (branch setaudit, 2026-11-25)

Redo of the 11-06 set-bonus audit. That audit compared Player fields and class stats only; the Redemption sweep found the harness never read a ModPlayer field, and that a set whose flag lives on a mod's ModPlayer can look "matching" while a levy gets nothing. This pass measured every Calamity (109 head/body/leg combinations, 37 sets) and Thorium (about 72 sets, 210 combinations from a loose IsArmorSet) set three ways, then fixed the hook that was dead.

## Method (all lab, tML 2026.10, Levy only plus the mod; ports 9230-9239)

1. **WRITES (static diff).** `LEVY_HARNESS=1 LEVY_HARNESS_SCENARIO=setbonus LEVY_HARNESS_SBMODE=static LEVY_HARNESS_SBFILTER=CalamityMod: LEVY_HARNESS_SBMOD=CalamityMod` (or `ThoriumMod`). A reference Player wears the full set, then the same pieces without the legs; every public Player field, every ModPlayer field and every class stat that changes is compared with the same two states on a levy's dummy (`CombatHarness.SetBonus.Rede.cs`, now building sets for any mod, pairing a head with body and legs by namespace first and by every piece second).
2. **READS (hook census).** New lab ModPlayer `SbHookCensus` (loads only with LEVY_HARNESS=1) counts every player hook, for a levy's dummy and for any other player, and whether `Player.whoAmI == Main.myPlayer` held for the dummy (the gate in front of most spawns and heals).
3. **EFFECT (observe fight).** New event `observe` (`CombatHarness.SetBonus.Observe.cs`): a levy in the full set, a weapon of the set's class, a pinned target and a poker that hits the levy, 600 ticks; one line per set with projectiles spawned (GearUpkeep spawn counts), buffs on the levy, the target and the poker, the levy's wounds, and the census. `LEVY_HARNESS_SBFIELDS=a,b` also prints named ModPlayer fields. The source side: `scratchpad/sa/calreaders.py` finds, for every field a head's UpdateArmorSet writes, every method that reads it (Calamity source; Thorium decompiled with ilspycmd).

Numbers: static diff is the same ACTIVE verdict for all 109 Calamity combinations once the levy's 1.5x run-speed policy is set aside (every set with a moveSpeed bonus shows levy = 1.5 x reference: `P.moveSpeed`, a Levy design, not a set difference).

## The one structural finding

Vanilla runs a player's mod hooks for a projectile hit (`PlayerLoader.OnHitNPCWithProj`, through `CombinedHooks.OnHitNPCWithProj`) only when `Projectile.TryGetOwner` returns a Player, and it returns nobody for an `npcProj` shot. A levy's shot is an npcProj shot owned by the commander.

* OBSERVED: the census shows `OnHitNPCWithProj` and `ModifyHitNPCWithProj` 0 calls for the levy's dummy and 0 for the commander over 600 ticks of ranged, magic, rogue and summon fire; melee swings run `OnHitNPCWithItem` for the dummy (8 to 33 calls). Everything a set reads in its projectile on-hit hook was therefore dead for the four projectile classes, for Calamity and Thorium alike. Calamity wraps the whole body in `!proj.npcProj && !proj.trap && proj.friendly`, so even a call is not enough: the shot has to look like a player's shot for the length of it.
* Parity of the flags was never the problem (writes are ACTIVE), the reader never ran.

**Fix (built):** `SoldierNPC.GearOnHitProj` (`SoldierNPC.GearOnHit.cs`), called from `SoldierProjectileTag.OnHitNPC`. For a levy with modded gear (the soldiers that already pay for the per-tick gear frame) it enters the frame the hurt reaction uses, makes the shot owned by the dummy's slot and not npcProj for the length of the call, runs `PlayerLoader.OnHitNPCWithProj` on the levy's own dummy, writes a life steal back to the body (Bloodflare, Ataxia) and adopts what the hook spawned. At most 24 calls per levy per tick. Skipped for a shot a held chain already owns (not npcProj) and for a shot `PlayerCounters` counts (shark guns: it would count the hit twice). Nothing the commander wears is involved (the shot is the dummy's, so a hook that homes a heal at "the owner" finds the levy). `LEVY_GEARONHIT=0` is the A/B switch.

OBSERVED before and after (LEVY_GEARONHIT=0 against default, same lab, new spawns on the levy's shots): Tarragon ranged and Auric ranged: Leaf and Tarra energy; Silva and Auric magic: Silva burst; Hydrothermic magic: Hydrothermic sphere and heal orb; GemTech: green flechette; Thorium Pyromancer: Pyro burst and explosion plus burn on the target; Cryomancer: Nitrogen reaction plus Freezing; Master Arbalest's Hood and Master Marksman's Scouter: their armor-set projectiles and debuffs on the target; Yew Wood: its visual. `[SetAbility]` trial `onhit` (Tarragon ranged levy, Handgun): 31 hook calls, 12 leaves and 12 Tarra energy, commander whole, 6/0; with the switch off the same trial gives the old nothing (0 calls, 2 FAIL).

**Second fix:** set-bonus max mana was not in a levy's pool (`ArmorDoctrine.GearManaBonus` read pieces only, "set bonuses are not counted"). `ArmorDoctrine.SetManaBonus` reads it from a scratch player's equip pass plus set pass; Thorium Pyromancer and Magma Seer (+20), Ebon Hood and Novice Cleric (+5) now match the reference (static diff MISSING -> OK).

## Verdict per set

Verdicts: WORKS (writes match, every reader hook runs for the levy), PARTIAL (some reader dead), NOTHING (no effect for a levy). BEFORE is before this branch; AFTER is with it. "Effect seen" is OBSERVED in the observe fight; "reader runs" without it means the hook is reachable and the effect is chance, cooldown, crit or boss gated (INFERRED to work).

### Calamity

| Set (heads) | Reader hooks (from source) | BEFORE | AFTER | Evidence |
|---|---|---|---|---|
| Aerospec (5) | PostHurt feathers, MiscEffects; Valkyrie AI | WORKS | WORKS | StickyFeatherAero x12-16 on all five; summon: ValkyrieBuff and Valkyrie |
| Astral | ItemOnHit and ProjOnHit star rain on crit | PARTIAL (melee only) | WORKS | melee: AstralStar and AstralInfection seen; projectile classes had 0 hook calls, now run |
| Auric Tesla (5) | tarra + bloodflare + silva + god slayer parts | PARTIAL | WORKS | melee: TarraLifeRegen, TarragonCloak, BloodFrenzy; ranged: GodSlayerShrapnel, BloodBomb, and now Leaf and TarraEnergy; magic: SilvaBurst now; summon: SilvaCrystal, TarragonAura. Rogue crit counting reachable, not seen. Dash is the key path below |
| Bloodflare (5) | MagicOnHit, ProjLifesteal, ItemLifesteal, MiscEffects, Shoot | PARTIAL | WORKS | melee BloodFrenzy; ranged BloodBomb and BloodflareSoul; magic GhostlyBolt (Shoot) and, now, the on-hit bloodsplosion and life steal path (hook runs, write-back built; crit and cooldown gated, not seen); summon GhostlyMine |
| Brimflame | key, MiscEffects | WORKS | WORKS | BrimflameFrenzyBuff (existing key path) |
| Daedalus (5) | OnHurt absorb, ModifyHitByProjectile reflect, PostHurt shards, GlobalProjectile.AI split, minion AI | PARTIAL | PARTIAL | ranged: CrystalShard x60 on 6 hurts; summon: crystal and its shots; absorb and reflect: hooks run, chance gated, not seen; **rogue split NOTHING**: read in `CalamityGlobalProjectile.AI` from `Main.player[projectile.owner]` (the commander); INFERRED from source, the lab levy did not throw (see Not fixed) |
| Demonshade | key, MeleeEffects, PostHurt | WORKS | WORKS | Enraged on levy and enemies, DemonScythe x155, ShadowBeam x62 |
| Desert Prowler | key smoke, rogue stealth | WORKS | WORKS | existing `[SetAbility]` / harness desertprowler |
| Empyrean | OnHurt, RogueOnHit | PARTIAL | WORKS | EmpyreanWrath seen; RogueOnHit now runs (not seen, ember cap and chance) |
| Fathom Swarmer, Wulfrum, Snow Ruffian, Titan Heart, Umbraphile, Sulphurous | stats; Sulphurous bubble | WORKS | WORKS | static ACTIVE; Sulphurous: SulphuricAcidBubbleFriendly seen |
| Fearmonger | GeneralLifeRegen, SummonOnHit | PARTIAL | WORKS | regen runs (census UpdateLifeRegen 37/37); SummonOnHit now runs |
| Forbidden Circlet | key tornado, OnHitNPCWithProj stealth tag | PARTIAL | WORKS | key path existing; the on-hit tag now reachable (rogue shots) |
| GemTech | Melee/Ranged/PlayerOnHit | PARTIAL | WORKS | GemTechGreenFlechette appears now on ranged hits |
| God Slayer (3 heads + 4 helms) | Shoot shrapnel, PostHurt darts (hurt over a threshold), dash | WORKS | WORKS | ranged GodSlayerShrapnel x145; dash is the existing God Slayer dash |
| Hydrothermic (5) | PostHurt blaze, MagicOnHit/ProjLifesteal, ItemOnHit geyser, Shoot bolts, vent AI | PARTIAL | WORKS | DeepseaBlaze and the 116 aura on all five, ChaoticGeyser (melee), HydrothermicFlare (ranged), HydrothermicVent (summon); magic: Sphere and HealOrb new |
| Lunic Corps | energy shields (MiscEffects, ModifyHurtInfo) | WORKS (inferred) | WORKS | static ACTIVE 7/7; hooks run; shield not visible in the fight (INFERRED) |
| Marnite Architect | IsArmorSet true for 25 body/leg pairs in the lab (the pieces are the same item) | WORKS | WORKS | only the run-speed scaling differs |
| Mollusk | ShellfishBuff, claps | WORKS | WORKS | buff and ShellfishClaps on enemies; endurance 0.091 vs 0.1 is vanilla's own conversion |
| Omega Blue | key madness | WORKS | WORKS | AbyssalMadness, OmegaBlueTentacle x876, HadopelagicPressure |
| Plaguebringer | patron set bees (friendly bee projectile hit) | WORKS | WORKS | Plague on enemies, Bee and PlagueBeeSmall |
| Plague Reaper | key blackout | WORKS | WORKS | existing key path |
| Prismatic | key lasers | WORKS | WORKS | DeathhailBeam x104, GodSlayerInferno |
| Reaver (3) | stats, ReaverRage | WORKS | WORKS | ReaverRage on the tank head |
| Silva (7 pieces) | FreeDodge, PreKill, regen, crystal AI, MagicOnHit | PARTIAL | WORKS | summon SilvaCrystal and explosions; magic SilvaBurst appears now; dodge and death-save run (census FreeDodge, PreKill) |
| Statigel (5) | stats, summon | WORKS | WORKS | BabyPaladin; stats ACTIVE |
| Tarragon (5) | melee cloak and regen, RangedOnHit leaves, MagicOnHit, RogueOnHit crit count, summon aura | PARTIAL | WORKS | melee TarraLifeRegen and TarragonCloak; ranged Leaf x14 and TarraEnergy x14 (new); summon TarragonAura; magic and rogue crit paths run |
| Victide (5) | stats, Seashell, urchins, snail | WORKS | WORKS | UrchinSpike, SeaSnail, Seashell seen |

### Thorium (72 distinct heads; names as the mod's)

Stat-only or reader on a hook that runs (PostUpdateEquips, ProcessTriggers aside, hurt hooks, Shoot, melee on-hit): WORKS, static ACTIVE: Flight Mask, Darksteel, Dread Skull (run-speed fields, see below), Granite (same), Depth Diver (DepthDiverAura seen), Demon Blood, Geode, Sandstone, Steel, Durasteel, Thorium, Titan (3), Terrarium (see below), Illumite (GlobalItem.Shoot), Icy (IcyArmorEffect1-3 and Chill seen), Whispering Hood (tentacles seen), Marching Band (set projectiles and Stunned seen), Fallen Paladin (OnHurt), Shade Master (ModifyHurt, OnHurt), Naga Skin (OnConsumeMana), Silk Hat, Conduit, Lodestone, Fungus, and the vanity or plain heads (Ace, Prospector, Feral Fur, Blue Knight, Chunky, Caustic, Legendary, Light Automaton, Space Dwarf, Sunhat, Glittering).

| Set | Reader | BEFORE | AFTER | Evidence |
|---|---|---|---|---|
| Pyromancer, Cryomancer, Dragon, Flesh, Lich, Life Bloom, Astro, Bronze, Magma Seer's, Spirit Trapper (cowl and mask), Tide Hunter, Tide Turner's Gaze, White Dwarf | `ThoriumPlayer.OnHitNPCWithProj` (and `OnHitNPCWithItem` for melee) | PARTIAL (swing only) or NOTHING (projectile classes) | WORKS (hook runs) | Pyromancer: PyroBurst, PyroExplosion2, burn; Cryomancer: ReactionNitrogen, Freezing; Dragon: DragonPulse x10 (melee path, was already working); the rest: hook runs 2-13 times in the fight, effect chance gated (Lich souls on kill, Life Bloom heal chance, Astro burst chance) |
| Master Arbalest's Hood, Master Marksman's Scouter, Yew Wood | `OnHitNPCWithProj`; `ModifyHitNPCWithProj` for the Marksman's defense ignore and Yew's guaranteed crit | NOTHING | PARTIAL | AssassinArmorSetProx, Scouter projectile and debuffs, YewVisual now appear; the Modify half (defense ignore, forced crit) is not replayed (below) |
| Magma Seer's (summon burn), Pyromancer mana | mana +20, +5 (Ebon Hood, Novice Cleric) | PARTIAL (pool) | WORKS | static MISSING -> OK after `SetManaBonus` |
| Terrarium | set focus chosen from the wearer's class (`GetTerrariumFocusValue`), TerrariumSetProx, Backlash | WORKS (effects seen) | WORKS | TerrariumSetProx x14 and TerrariumBacklash seen; static max life differs (ref +15, levy -35): OPEN, INFERRED to be the class-dependent focus, not investigated |
| Bard sets (Crier's Cap, Jester's Mask, Noble's Hat, Ornate Hat, Shooting Star, Soloist, Maestro, Inspirators, Cyberpunk, Hallowed Chapeau) | bard hooks read `Main.player[proj.owner]` (BardProjectile/BardItem OnHitNPC, EmpowermentLoader) | UNVERIFIED | UNVERIFIED | the levy fires Bongos (BongoDamage x2-7) and the on-hit hook runs; whether the set flag is read from the levy or from the commander cannot be told from the lab (INFERRED: the commander's, so empowerment lengthening and note bonuses are NOTHING for a levy; BardRenown is the separate Levy mechanism) |
| Healer sets (Biotech, Blooming, Celestial, Coral, Ebon, Iridescent, Life Binder, Novice Cleric, Sacred, Templar, Warlock, Hallowed Cowl) | `ProjectileHelper.ThoriumHealTarget`, `HealerOnHitNPC` read the owner's ThoriumPlayer | UNVERIFIED | UNVERIFIED | the lab levy did not use the healer weapon (0 hits, 0 hook calls); Biotech's probe spawned (BiotechProbe x1). INFERRED NOTHING for the owner-read parts |
| Thrower sets (Plague Doctor's Mask, Fungus Hat, Shade Master, White Dwarf) | `ReactionX.ModifyHitNPC`, `ThoriumGlobalNPC.ModifyHit` read the owner | UNVERIFIED | UNVERIFIED | a vanilla knife stood in for a Thorium thrower, so only the ModPlayer part ran; INFERRED NOTHING for the owner-read parts |

Static diff leftovers that are not set differences (INFERRED, each a timer or a state at the snapshot): Soloist and the 123 Malignant Cap combinations `inspirationRegenTimer +1` (a counter the reference's one tick advanced), Cryomancer `setCryomancerRotation`, Conduit `circuitTimer`, Silk Hat (25% magic damage only above 90% mana: the snapshot mana differs), White Knight (set text carries the stored mana), Dread Skull and Granite `maxRunSpeed` / `accRunSpeed` (a levy's run speed is read from the gear by `AccessoryMobility` on its own scratch player, not from the dummy's fields).

### Key-press set bonuses (known gap, listed, not built)

Calamity: none left. Every `ArmorSetBonusActivated` or `ProcessTriggers` set reader (Brimflame, Tarragon melee cloak, Bloodflare ranged souls, Omega Blue, Demonshade, Plague Reaper, Forbidden Circlet, Prismatic, Desert Prowler, God Slayer and Auric Tesla dash) is already in `SetAbilities.tsv`.

Thorium, from the `ProcessTriggers` readers and the "Pressing the ..." set texts: Celestial Crown, Cyberpunk Headset, Dream Weaver's Helmet and Hood, Inspirator's Helmet, Maestro's Wig, Soloist's Hat, Tide Turner's Helmet, Warlock Hood, White Knight Mask; also Valadium Helmet (UP reverses gravity), Hallowed Guise (technique keys) and Darksteel's double-tap dash (the dash itself is the run-speed fields of the above).

### Not fixed (with the reason)

* **Modify-side projectile hooks** (`ModifyHitNPCWithProj`): Thorium Master Marksman, Yew Wood and Magma Seer's. Replaying it would double what `LevyRippers` already does for Calamity's Rage and Adrenaline damage. Calamity has no set reader there.
* **Set flags read by a projectile's or NPC's own hook from `Main.player[owner]`**: Calamity Daedalus rogue split (`CalamityGlobalProjectile.AI`), the Thorium bard, healer and thrower flags above. The generic fix is a `C` row in `PlayerCounters.tsv` per flag (the field is held as the levy's for every pass of his shot). I added the Daedalus row, then removed it: the lab levy never threw a Daedalus-rogue knife, so it could not be shown to work.
* Terrarium's max-life difference and the bard, healer and thrower sets' real effects (class gear the lab levy does not use well).
