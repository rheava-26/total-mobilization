# Levy design options: fear models and survivability (branch `options-lab`, 2026-11-07)

Everything here is OFF by default and chosen by an env switch. Nothing ships until Eve decides. Default behaviour is untouched: every hook starts with a static check (`LevyOptions.Fear != Default`, `LevyOptions.SurvOn`) and the default arm below is the same build with no switch set.

Build: Levy 2.0.18 + options code (`Common/Options/LevyOptions.cs`, `Common/LevyConstants.Options.cs`, small hooks in `SoldierNPC.Fear.cs`, `FearSystem.cs`, `SoldierRecord.MaxHp`, `SoldierNPC.RefreshStats`, `SoldierNPC.ModifyIncomingHit`). Harness: `CombatHarness.CalRev*.cs`, scenario `calrev`, `LEVY_CR_SP=1` (single-player emulation), `cmd=near`, real counts (KS 10, DS 11, Eye 11, EoW/Brain 13, QB 15, Skeletron 16), `gear=mod`, `grade=lv`, worms spawn at 80 tiles, Queen Bee `jungle=1`, cap 300 s, seeds 1-6 paired (the same seed is the same army in every arm). `UrchinMace` and `SaharaSlicers` excluded as in lab 2.

## Switches

| Switch | Values | Default |
|---|---|---|
| `LEVY_FEARMODEL` | `default`, `events`, `proximity_slow`, `rally` | `default` (today's model) |
| `LEVY_SURV` | `off`, `parity`, `veteran`, `formation` (comma list to combine, e.g. `parity,formation`) | off |
| Harness spec tokens | `fm=<model>`, `surv=<flags>`, `vet=<boss fights survived>` override the env for one fight (key suffixes `fm`, `sv`, `vt`) | none |

They are env-only on purpose (a new ModConfig field would need localization and the boot field check). If Eve picks one, the ship step is a config enum plus a label.

## The options

### Fear (Shaken/Routed behaviour itself is unchanged; only what moves the fear value changes)

1. **events** (`LEVY_FEARMODEL=events`): no fear from a boss merely being near. Fear comes only from a hit that takes 30% or more of a levy's max HP (its own, or an ally's within 40 tiles), an ally dying within 40 tiles (+25, as today), and a boss phase change (+15, as today); the commander dying still routs everyone. The meter drains 4 per second once a levy has gone 3 seconds without being hit, and a Routed levy whose meter falls under 25 recovers.
2. **proximity_slow** (`proximity_slow`): today's model with the per-tick boss-in-view gain at 20%, plus a drain of 2.5 per second while the levy is above 70% HP. Hit, death and phase events are as today. A Routed levy that drains under 25 recovers.
3. **rally** (`rally`): today's model, but every 20 ticks a levy with a living player (the commander) within 40 tiles is steadied: fear drops to at most 25 and Shaken/Routed end (the existing `Fear_Rally`). No banner or rally order feeds this: the `Rally` command exists (30-tile radius, cooldown) but is manual, and the Sentry Banner only calls up Old Ones' Army units, so the model uses commander proximity. In the lab the commander stands inside the army (`cmd=near`), so this is its best case.

### Survivability

1. **parity** (`LEVY_SURV=parity`): a levy's max HP is floored at the commander's max life and its defense at the commander's defense (Regulars only, still capped at 500 HP). In the lab the commander is a pinned dummy, so a table stands in for "what a player has" (inferred typical values): KS 160 HP / 6 def, DS 200/8, Eye 240/10, EoW and Brain 300/12, QB 300/16, Skeletron 400/20. Levies that were fed more crystals keep theirs.
2. **veteran** (`veteran`): +6% max HP per boss fight survived (up to 5 fights), +10% for Man-at-Arms and +20% for Champion grade, +1 defense per 2 fights. The lab gives every levy the fights a campaign army would have behind it (KS 0, DS 1, Eye 2, EoW/Brain 3, QB 4, Skeletron 5).
3. **formation** (`formation`): 1% damage reduction for each other living, un-routed levy within 15 tiles, capped at 10%.

## Question B: is a levy under the player-equivalent at that tier?

Observed (code read plus lab columns `avgHp`, `avgDef`):
- **No formula bug.** Max HP is `100 + 20 x Life Crystals + 5 x Life Fruit`, cap 500 (`SoldierRecord.RawHp/MaxHp`): the player's own formula, and crystals are counted. Defense is `Dummy.statDefense` from the vanilla stat pipeline (armor, set bonuses, accessories) and incoming hits use the player's rule (`DefenseEffectiveness` 0.5 scaled to 0.75 in Expert, endurance, i-frames at parity since 2.0.18). Lab levies measure avg defense 13.0 (Eye, DS) and 17-18 (EoW, Brain, QB, Skeletron), at or above the inferred player table except Skeletron (17.8 vs 20).
- **There is a progression gap in HP, not in the formula.** Nothing gives a levy HP by progression: a levy has 100 HP until Eve donates Life Crystals to the home armory, and the armory feeds ONE crystal per levy per check. Lab levies have 137-141 HP (0-5 rolled crystals) before the Eye and 200 HP (5 crystals) from Eater/Brain on (observed `avgHp`). A player at those points has about 200-300 HP, about 400 at Skeletron (inferred, typical Calamity progression; the brief's 200-300 range). So levies are 30-50% below player HP at every tier, while their defense is at parity.
- **Structural difference (by design, not a bug):** a Levy-grade soldier has 1 accessory slot (player 5-7); Man-at-Arms 3, Champion 5 (CP 3 and 7).
- Verdict: the HP gap is a fix candidate if the intent is "a levy is a player-equivalent soldier at that tier". The `parity` option is that fix, written so it can never lower a levy. Whether crystals should instead stay the way levies grow is Eve's call.

## Results

Cells: Eye Rev, Brain Rev, Queen Bee Expert, Eater of Worlds Expert, Desert Scourge Expert, Skeletron Expert. 6 paired seeds per arm. Columns: win/despawn/timeout/loss counts, mean levy deaths, mean kill time of the wins, mean time of the first levy death, share of levy-ticks spent Shaken or Routed, average max HP and defense, damage the levies deal to the boss per second. Skeletron TIMEOUT rows with the boss moved over 100000 px are counted as DESPAWN (none occurred). Arms: `base` = today; `fm_*` = fear models; `sv_*` = survivability; `combo_ep` = events + parity; `combo_epf` = events + parity + formation.

| cell | arm | win/desp/timeout/loss (n) | deaths | kill time s | first death s | Shaken+Routed % | avg HP | avg def | boss dmg/s |
|---|---|---|---|---|---|---|---|---|---|
| Eye of Cthulhu Rev | base | 0/0/0/6 (6) | 11.0 | - | 52.6 | 90 | 137 | 13.0 | 22 |
| Eye of Cthulhu Rev | fm_events | 0/0/0/6 (6) | 11.0 | - | 30.8 | 8 | 137 | 13.2 | 46 |
| Eye of Cthulhu Rev | fm_slow | 1/0/0/5 (6) | 10.3 | 170 | 26.9 | 71 | 137 | 13.0 | 26 |
| Eye of Cthulhu Rev | fm_rally | 2/0/0/4 (6) | 10.5 | 156 | 46.0 | 27 | 137 | 13.3 | 41 |
| Eye of Cthulhu Rev | sv_parity | 1/0/0/5 (6) | 9.5 | 167 | 97.3 | 88 | 240 | 14.2 | 32 |
| Eye of Cthulhu Rev | sv_veteran | 0/0/0/6 (6) | 11.0 | - | 70.4 | 92 | 153 | 14.1 | 26 |
| Eye of Cthulhu Rev | sv_formation | 0/0/0/6 (6) | 11.0 | - | 46.8 | 89 | 137 | 13.2 | 23 |
| Eye of Cthulhu Rev | combo_ep | 4/0/0/2 (6) | 6.0 | 105 | 80.6 | 2 | 240 | 14.1 | 58 |
| Eye of Cthulhu Rev | combo_epf | 5/0/0/1 (6) | 6.2 | 104 | 76.9 | 1 | 240 | 14.1 | 59 |
| Brain of Cthulhu Rev | base | 0/0/0/6 (6) | 13.0 | - | 18.8 | 52 | 200 | 16.9 | 1 |
| Brain of Cthulhu Rev | fm_events | 3/0/0/3 (6) | 9.8 | 149 | 18.9 | 2 | 200 | 17.0 | 26 |
| Brain of Cthulhu Rev | fm_slow | 1/0/1/4 (6) | 12.3 | 160 | 17.6 | 43 | 200 | 16.9 | 4 |
| Brain of Cthulhu Rev | fm_rally | 1/0/0/5 (6) | 11.8 | 107 | 17.8 | 23 | 200 | 17.0 | 11 |
| Brain of Cthulhu Rev | sv_parity | 1/0/2/3 (6) | 9.7 | 100 | 24.8 | 69 | 300 | 17.4 | 8 |
| Brain of Cthulhu Rev | sv_veteran | 0/0/0/6 (6) | 13.0 | - | 19.8 | 59 | 236 | 17.7 | 4 |
| Brain of Cthulhu Rev | sv_formation | 0/0/1/5 (6) | 12.8 | - | 18.7 | 52 | 200 | 17.0 | 0 |
| Brain of Cthulhu Rev | combo_ep | 2/0/1/3 (6) | 10.8 | 111 | 21.6 | 2 | 300 | 16.9 | 20 |
| Brain of Cthulhu Rev | combo_epf | 5/0/0/1 (6) | 4.5 | 111 | 26.6 | 1 | 300 | 17.1 | 39 |
| Queen Bee Expert | base | 0/0/0/6 (6) | 15.0 | - | 31.5 | 96 | 200 | 18.4 | 32 |
| Queen Bee Expert | fm_events | 4/0/0/2 (6) | 9.8 | 79 | 30.6 | 10 | 200 | 18.4 | 76 |
| Queen Bee Expert | fm_slow | 0/0/0/6 (6) | 15.0 | - | 40.9 | 94 | 200 | 18.3 | 31 |
| Queen Bee Expert | fm_rally | 3/0/0/3 (6) | 11.2 | 66 | 20.2 | 24 | 200 | 18.6 | 66 |
| Queen Bee Expert | sv_parity | 1/0/0/5 (6) | 14.0 | 60 | 60.3 | 96 | 300 | 19.2 | 37 |
| Queen Bee Expert | sv_veteran | 0/0/0/6 (6) | 15.0 | - | 51.7 | 96 | 248 | 20.4 | 33 |
| Queen Bee Expert | sv_formation | 1/0/0/5 (6) | 12.7 | 69 | 57.6 | 96 | 200 | 18.3 | 33 |
| Queen Bee Expert | combo_ep | 4/0/0/2 (6) | 7.2 | 58 | 34.4 | 7 | 300 | 19.3 | 73 |
| Queen Bee Expert | combo_epf | 5/0/0/1 (6) | 4.5 | 74 | 46.3 | 4 | 300 | 19.0 | 92 |
| Eater of Worlds Expert | base | 0/0/0/6 (6) | 13.0 | - | 19.9 | 78 | 200 | 17.1 | 243 |
| Eater of Worlds Expert | fm_events | 5/0/0/1 (6) | 7.7 | 55 | 18.1 | 4 | 200 | 16.8 | 375 |
| Eater of Worlds Expert | fm_slow | 0/0/0/6 (6) | 13.0 | - | 15.8 | 60 | 200 | 17.0 | 258 |
| Eater of Worlds Expert | fm_rally | 3/0/0/3 (6) | 10.5 | 62 | 12.8 | 33 | 200 | 17.2 | 281 |
| Eater of Worlds Expert | sv_parity | 2/0/0/4 (6) | 10.7 | 98 | 24.0 | 86 | 300 | 17.7 | 187 |
| Eater of Worlds Expert | sv_veteran | 2/0/0/4 (6) | 11.0 | 88 | 20.7 | 86 | 236 | 18.3 | 192 |
| Eater of Worlds Expert | sv_formation | 0/0/0/6 (6) | 13.0 | - | 17.5 | 83 | 200 | 17.3 | 189 |
| Eater of Worlds Expert | combo_ep | 6/0/0/0 (6) | 4.8 | 55 | 20.1 | 3 | 300 | 17.0 | 398 |
| Eater of Worlds Expert | combo_epf | 6/0/0/0 (6) | 4.0 | 55 | 18.4 | 2 | 300 | 17.1 | 373 |
| Desert Scourge Expert | base | 0/0/0/6 (6) | 11.0 | - | 9.6 | 0 | 141 | 13.0 | 70 |
| Desert Scourge Expert | fm_events | 0/0/0/6 (6) | 11.0 | - | 5.3 | 0 | 141 | 12.8 | 63 |
| Desert Scourge Expert | fm_slow | 0/0/0/6 (6) | 11.0 | - | 4.8 | 0 | 141 | 13.2 | 68 |
| Desert Scourge Expert | fm_rally | 0/0/0/6 (6) | 11.0 | - | 3.5 | 0 | 141 | 13.1 | 78 |
| Desert Scourge Expert | sv_parity | 4/0/0/2 (6) | 7.8 | 87 | 9.8 | 0 | 200 | 13.7 | 64 |
| Desert Scourge Expert | sv_veteran | 0/0/0/6 (6) | 11.0 | - | 5.3 | 0 | 150 | 13.4 | 84 |
| Desert Scourge Expert | sv_formation | 0/0/0/6 (6) | 11.0 | - | 3.6 | 0 | 141 | 13.2 | 54 |
| Desert Scourge Expert | combo_ep | 2/0/0/4 (6) | 8.8 | 69 | 12.2 | 0 | 200 | 13.5 | 64 |
| Desert Scourge Expert | combo_epf | 1/0/0/5 (6) | 10.7 | 85 | 10.8 | 0 | 200 | 14.6 | 53 |
| Skeletron Expert | base | 5/0/0/1 (6) | 12.2 | 150 | 20.6 | 93 | 200 | 17.8 | 64 |
| Skeletron Expert | fm_events | 6/0/0/0 (6) | 5.0 | 70 | 16.5 | 16 | 200 | 18.0 | 168 |
| Skeletron Expert | fm_slow | 5/0/0/1 (6) | 10.2 | 124 | 15.3 | 86 | 200 | 17.6 | 74 |
| Skeletron Expert | fm_rally | 6/0/0/0 (6) | 8.0 | 97 | 15.2 | 28 | 200 | 17.5 | 123 |
| Skeletron Expert | sv_parity | 6/0/0/0 (6) | 6.3 | 119 | 28.9 | 92 | 400 | 20.6 | 90 |
| Skeletron Expert | sv_veteran | 5/0/0/1 (6) | 9.8 | 137 | 17.6 | 93 | 260 | 19.9 | 67 |
| Skeletron Expert | sv_formation | 3/0/0/3 (6) | 13.3 | 164 | 15.0 | 91 | 200 | 17.9 | 46 |
| Skeletron Expert | combo_ep | 6/0/0/0 (6) | 1.0 | 49 | 33.0 | 0 | 400 | 20.7 | 223 |
| Skeletron Expert | combo_epf | 6/0/0/0 (6) | 2.3 | 52 | 21.8 | 0 | 400 | 20.7 | 220 |

Wins per arm (paired seeds; W = kills):
| arm | Eye of Cthulhu Rev | Brain of Cthulhu Rev | Queen Bee Expert | Eater of Worlds Expert | Desert Scourge Expert | Skeletron Expert | total |
|---|---|---|---|---|---|---|---|
| base | 0/6 | 0/6 | 0/6 | 0/6 | 0/6 | 5/6 | 5/36 |
| fm_events | 0/6 | 3/6 | 4/6 | 5/6 | 0/6 | 6/6 | 18/36 |
| fm_slow | 1/6 | 1/6 | 0/6 | 0/6 | 0/6 | 5/6 | 7/36 |
| fm_rally | 2/6 | 1/6 | 3/6 | 3/6 | 0/6 | 6/6 | 15/36 |
| sv_parity | 1/6 | 1/6 | 1/6 | 2/6 | 4/6 | 6/6 | 15/36 |
| sv_veteran | 0/6 | 0/6 | 0/6 | 2/6 | 0/6 | 5/6 | 7/36 |
| sv_formation | 0/6 | 0/6 | 1/6 | 0/6 | 0/6 | 3/6 | 4/36 |
| combo_ep | 4/6 | 2/6 | 4/6 | 6/6 | 2/6 | 6/6 | 24/36 |
| combo_epf | 5/6 | 5/6 | 5/6 | 6/6 | 1/6 | 6/6 | 28/36 |

Per-seed results (W/D/T/L letters, seeds 1..6):
- Eye of Cthulhu Rev base: LLLLLL
- Eye of Cthulhu Rev fm_events: LLLLLL
- Eye of Cthulhu Rev fm_slow: LLLWLL
- Eye of Cthulhu Rev fm_rally: LWLWLL
- Eye of Cthulhu Rev sv_parity: LLLWLL
- Eye of Cthulhu Rev sv_veteran: LLLLLL
- Eye of Cthulhu Rev sv_formation: LLLLLL
- Eye of Cthulhu Rev combo_ep: WWLWLW
- Eye of Cthulhu Rev combo_epf: WWLWWW
- Brain of Cthulhu Rev base: LLLLLL
- Brain of Cthulhu Rev fm_events: LWLLWW
- Brain of Cthulhu Rev fm_slow: LTLLLW
- Brain of Cthulhu Rev fm_rally: LLWLLL
- Brain of Cthulhu Rev sv_parity: TLLLTW
- Brain of Cthulhu Rev sv_veteran: LLLLLL
- Brain of Cthulhu Rev sv_formation: LLLLLT
- Brain of Cthulhu Rev combo_ep: WLLTLW
- Brain of Cthulhu Rev combo_epf: WWWLWW
- Queen Bee Expert base: LLLLLL
- Queen Bee Expert fm_events: LWLWWW
- Queen Bee Expert fm_slow: LLLLLL
- Queen Bee Expert fm_rally: LWLWWL
- Queen Bee Expert sv_parity: LWLLLL
- Queen Bee Expert sv_veteran: LLLLLL
- Queen Bee Expert sv_formation: LWLLLL
- Queen Bee Expert combo_ep: WWLWWL
- Queen Bee Expert combo_epf: LWWWWW
- Eater of Worlds Expert base: LLLLLL
- Eater of Worlds Expert fm_events: WWWLWW
- Eater of Worlds Expert fm_slow: LLLLLL
- Eater of Worlds Expert fm_rally: WLWLLW
- Eater of Worlds Expert sv_parity: LLWLLW
- Eater of Worlds Expert sv_veteran: LLWLLW
- Eater of Worlds Expert sv_formation: LLLLLL
- Eater of Worlds Expert combo_ep: WWWWWW
- Eater of Worlds Expert combo_epf: WWWWWW
- Desert Scourge Expert base: LLLLLL
- Desert Scourge Expert fm_events: LLLLLL
- Desert Scourge Expert fm_slow: LLLLLL
- Desert Scourge Expert fm_rally: LLLLLL
- Desert Scourge Expert sv_parity: WWWLWL
- Desert Scourge Expert sv_veteran: LLLLLL
- Desert Scourge Expert sv_formation: LLLLLL
- Desert Scourge Expert combo_ep: LWLLLW
- Desert Scourge Expert combo_epf: LWLLLL
- Skeletron Expert base: WWWWWL
- Skeletron Expert fm_events: WWWWWW
- Skeletron Expert fm_slow: WWWWLW
- Skeletron Expert fm_rally: WWWWWW
- Skeletron Expert sv_parity: WWWWWW
- Skeletron Expert sv_veteran: WWWWWL
- Skeletron Expert sv_formation: LLWWWL
- Skeletron Expert combo_ep: WWWWWW
- Skeletron Expert combo_epf: WWWWWW


## Reading the tables

Observed:
- **Today's model:** 78-96% of levy-ticks are Shaken or Routed in every cell except Desert Scourge (0% in all arms: the Calamity worm has no entry in the terror table, so proximity fear is 0 there). Base wins 5/36, all of them Skeletron.
- **events:** 18/36 wins (EoW 5/6, QB 4/6, Brain 3/6, Skeletron 6/6), Shaken+Routed down to 2-16%, damage to the boss per second 1.5-26x higher (Brain 1 to 26, EoW 243 to 375, QB 32 to 76, Skeletron 64 to 168). It does nothing for Desert Scourge (0/6) and Eye Rev (0/6), and the first levy death comes earlier (Eye 52.6 s to 30.8 s): fear was also what kept levies alive by pulling them back.
- **proximity_slow:** 7/36. At 20% gain the meter still reaches Shaken (43-94% of ticks) because hits add to it and the drain only runs while above 70% HP. Not enough.
- **rally:** 15/36 with the commander inside the army; 23-33% Shaken/Routed. Only works while the commander stays within 40 tiles (not tested with `cmd=far`).
- **parity:** 15/36, the only option that moves Desert Scourge (4/6 at 200 HP vs 0/6 at 141) and it lifts Skeletron deaths 12.2 to 6.3. It does not touch fear (86-96% Shaken/Routed).
- **veteran:** 7/36. By construction it adds little where the problem is (0-2 fights behind the army at KS/DS/Eye, +6% to +18% HP).
- **formation:** 4/36 alone, indistinguishable from zero (Skeletron 3/6 vs 5/6 base is within noise): 10% is too small against 3-hit kills.
- **Best combined, events + parity:** 24/36 (Eye Rev 4/6, Brain 2/6, QB 4/6, EoW 6/6, DS 2/6, Skeletron 6/6). Adding formation gives 28/36 but formation alone does nothing, so treat 24 vs 28 as noise (binomial SD about 3).

Inferred, not measured:
- Fear and HP act on different cells: events fixes the army that stops fighting (EoW, Brain, QB), parity fixes the army that is one-shot by a worm head (DS). That is why they combine. Desert Scourge stays hard in every arm (best 4/6): its head hits about 124 and no HP floor below 500 survives three hits at spacing.
- Desert Scourge (2/6 vs 4/6) and Brain (2/6 vs 1-3/6) differences between arms are inside the noise of 6 seeds; fights are not bit-reproducible.
- The parity table is my estimate of typical player stats; the production option reads the real player.

## Recommendation

1. **Ship `events` as the fear model** (candidate). It is the largest single change (5 to 18 wins of 36; levy damage to the boss 1.5-3x, far more for Brain) and it fits the tenet: the enemy stays frightening through events (a levy dying beside you, a hit that nearly kills you, a phase roar), and the player's army is not capped or denied. Trade-off: boss proximity no longer frightens by itself, so fear no longer scales with boss terror (the terror table is unused; a follow-up could scale event sizes by terror), and levies fight on instead of falling back, so first deaths come earlier (Eye Rev) while total deaths fall in most cells. If Eve wants a softer change, `rally` is the second choice (15/36) but depends on the commander standing near.
2. **Ship `parity` as the HP fix** (candidate, and a gap report as much as a design call). It never lowers a levy, uses the real player's life and defense, and is the only option that moves the worms. Trade-off: the army's HP follows the commander's, so crystals fed to levies matter only above the commander's own, and it is a large early buff (137 to 240 HP at the Eye). Do not rely on it for Desert Scourge alone.
3. **Drop `proximity_slow` and `veteran` as built; keep `formation` only as a tuning knob.** To be useful formation needs a number far above 10%, which is a design call.
4. Desert Scourge and Eye Rev stay hard with both options (best 4/6, 5/6 only with formation stacked); under the tenet the answer there is a boss-side or worm-hit design change, not a cap on the player.

## Replay

Typical fight of the best combined arm (events + parity): `scratchpad/replays/options/` (`calrev_Eater_of_Worlds_surf_s2_mexp_mod_n13_01.jsonl`, a 57.5 s win with 7 deaths; `calrev_Eye_of_Cthulhu_surf_s2_mrev_mod_n11_02.jsonl`, a 121.1 s win with 5 deaths). Raw rows: `raw_options/`; specs and scripts: `scripts/options/`.
