# Inspector-General of the Society Bloc: Second Audit (`inspector-society`, Round Five)

## 1. Opening

*(The Censor opens the ledger. A page has been torn out and replaced with a gift card.)*

Members, I asked for prices and you brought me coupons. Eight of my twenty-eight charges are satisfied. That is a 29% pass rate, which in my profession is called "a trend." The Ministry of Stability found a bug in the Megaprojects Council's bill with a lamp, and I wish to kiss its forehead. Everyone else defended their toys with adjectives. I grep.

## 2. The second charge sheet (new or amended bills)

**Stability**
- **S-2 Maskirovka (standing declaration): CHARGED (EXPLOIT).** The click is gone, but Air/Ground/Sea/Space rotation still beats "third identical lie". *Fix:* doubt counts any declaration change inside 180 s.
- **S-4 Info Dial: CHARGED (EXPLOIT).** Dropping the 10-inf fee and lockout makes Free Press freer. A no-strike, no-city-loss player never pays. *Fix:* Free Press also speeds the fleet's adaptation scan.
- **Decrees standing orders, Crisis Stance: CLEAN.** Floor, Fear +0.1 and ×1.25 are automatic, priced costs.
- **S-5 Civic Contract: CLEAN, and it caught a real bug.** Verified: L6338 is `Math.max(0, G.mult.stab) * 30`, so MEGA-5's "+4" is +120 and clamps the cap to 100. Stability's `G.civCap` fix is right. **`megaprojects.md` still says "`civ.stabCap` writes to `g.mult.stab`"** and must adopt it.
- **S-6 Rally, S-7 Taboo Book: CLEAN.** I verified the crisis constants (8/s ramp, floors 40/30/20) and that the strike flow is 14 ÷ 0.5 = 28 s per nuke.

**Science**
- **SCI-2 Phased Arrays/Stigmergy: CHARGED (FALSE, PERF).** The geometry claim "Laser Tower w 19, so about 36 per window, x2 at saturation" is wrong: `lasertower` is `w: 14` (L3439). That is about 50 per layer, +147%, ×2.5. Other emitters and a buried layer stack on top. Stigmergy's 48→96 bodies has no perf test. *Fix:* correct the number and add emitter-cluster priority to `busterScore`.
- **SCI-3 Containment by depth: CLEAN.** It is vertical, and a buried lab is a Deep Penetrator target.
- **SCI-4 Crash by default: CLEAN.**
- **SCI-6 The Fork: CHARGED (EXPLOIT).** `UNIT_FAM` (L3378) puts `shifter` in `['bio','mech']`, so it takes Ascension's +25% HP and healing and Singularity's build speed: the fork is no fork for shifters. Ascension also buffs Cadres (bio). *Fix:* each family tag's bonus applies to the unit's *primary* family only.
- **SCI-7 Open Doors: CHARGED (FALSE).** It rests on Energy's Adaptive Hull, which is not in the code, and on a `tutquick` audit I cannot find in `title.html`. *Fix:* file it as a dependency of that bill.

**Lore**
- **LORE-6 Masthead, LORE-7 Assay: CLEAN.** Readouts with enemy answers. *Note:* Psi's `+0.25 Fear per 8 psi` is unbounded (400 psi = +12.5), against "F max 4" in the Assay.
- **LORE-8 The Unaccounted: CHARGED (CREEP).** Buried mass counts 50% in Fear, yet the Plumb answer only arrives below Resolve 60. At 0.04/s that is 1,000 s away. *Fix:* the Plumb spawns on a 120-s timer once Unaccounted ≥50%.

**Psi**
- **PSI-1: CHARGED (EXPLOIT).** The −0.8 cap per trooper still floors at 56 troopers, then is free. The new stats (hp 140, 70 dmg, 300 IC) double value per IC (12 to 23). *Fix:* IC upkeep of 0.3/s per trooper.
- **PSI-3 Lumen (air-domain, auto), PSI-4 Hush, PSI-X Watch: CLEAN.**
- **PSI-5 Ingress: CHARGED (FALSE, TENET).** See §4.
- **PSI-6 Low Ceiling: CHARGED (FALSE).** It cites "ROADMAP L585"; the report 6 facts are at L605, and they predate v3.90.0 and v3.91.1. The `.66` Pylon ceiling is *lower* than the default plasma `.62` (L4494), so a 560 IC superweapon would reach less than a Shapeshifter. *Fix:* `.62` for both, and cite the post-fix match.

**Bio**
- **BIO-1 Reservoir: CLEAN.** The per-second pool answers PERF.
- **BIO-4 Drift: CHARGED (OP).** Drift strips 40% of one kind every 240 s, but at 3.5 kills/s (reports 8 and 10 scale) adaptation settles near 3,000, which is 120 steps, so −3%/step still passes 100%. *Fix:* damage taken is ×0.97^n, never a linear subtraction.
- **BIO-5 Thicket Works, BIO-7 Living Continent: CLEAN.** A chokepoint along the front is the Line done right. The continent names a Fear term and a harvester answer.
- **BIO-X Kleiber upkeep (and Brood Pit): CHARGED (EXPLOIT).** Upkeep is paid in MP, which was "pinned 900" in report 6 (ROADMAP L605), so it is inert. Weight stays 6. *Fix:* weight scales with stage.
- **BIO-6 Undercut: CHARGED (FALSE).** See §4.

**Counts: CLEAN 15, CHARGED 12 (27 entries).**

## 3. Old charges

SATISFIED (8):
- Decrees (standing orders and Fear tax).
- LORE-1 Charter (the deterministic Fear +0.1 per Unleash).
- LORE-H Invoice (withdrawn).
- PSI-2 (withdrawn into the Thermal Field; the dispersal and melee clauses are dead).
- PSI-3 Lumen (no Nanofortress mount).
- BIO-1 (reservoir).
- Docket 2, The Nerve (LORE-7 supplies the positive-trade arithmetic).
- Docket 12, Charter (Crisis Stance).

NOT SATISFIED (20):
- S-1 Ledger: "machines pay nothing" stays, and `isMechanical` is still the cited predicate.
- S-2, S-4: see above.
- SCI-2, Synthesis, Power Grid (Phased Arrays): the uncapped stack stays.
- SCI-5 and Breathing Sky: there is still no enemy re-boost.
- SCI-X: the Rout-versus-flagship-sample conflict is unanswered.
- LORE-2: the Truce gate is still `dangCap ≥ 20`.
- LORE-3 and Dossier Line: no codex record or TTL.
- PSI-1: the floor.
- BIO-3 and Deep Deposits: the self-nuke influence loop is unanswered.
- BIO-4 (see above).
- BIO-X.
- Thermal Field: the armour claim (`ARMOR_NERF .55`) and the cold plate are untouched.
- Fleet Reads You.
- Fleet Will Return: the stability reset is unanswered.

**`allRegen`: not answered.** Psi says "leave it and re-audit" and Science says "watch". Nobody gated it behind enemy heat. **Cadres (OP #2): not answered**, and Ascension now buffs them.

## 4. The Shapeshifter verdict

**Check the premise first.** `hurt` (L4987–4991): armour is ×0.55, and hits are ×1.3 × 1.9. Against a `gtank` (armor 6, L5604) plasma 75 loses 3.3 of 185. Against a Leviathan (armor 22) it loses 12 of 185. A shifter's ranged shot does about 87 dps to a Leviathan, so one shifter kills a base-strength one in about 20 s. The live code has no tank problem.

**Mimic, Ingress and Opening latch, and the latch is worse than shooting.**
- At 1.2% per second, a latched shifter beats its own 38 dps only above 3,170 HP.
- Bosses at 0.3% give 5 hp/s per shifter against 87 ranged.
- Mimic and Ingress both trigger at `armor ≥ 12`. The only enemy tank (`gtank`, armor 6) and `gmech` (8) never qualify. They fix a tank problem for units that are not tanks.
- Both add "useless vs air", which removes the shifter's `ceil .62` reach on a fleet that is mostly air. That is an unproven nerf (TENET).
- Ingress's own "Leviathan in about 3 minutes" is wrong: 12 slots at 0.2% is 2.4%/s, about 42 s.
- Opening's "strip 8% armour" saves about 6 damage on a 173 hit, about 3%. Make it +15% damage taken.

**Ranking** (President's question, then Line, then One-Button; all five pass One-Button):
1. **Undercut (Bio), amended.** It is the only one that keeps ranged fire and the air reach, uses the vertical axis the Address told us to use, and gives an actual tank answer (an untouchable approach, then belly attack). Fix its threshold to `armor ≥ 5` so the `gtank` triggers it, and drop "armour ×0.35" (worthless).
2. **Mimic (Rifle)**, minus the air clause and with the threshold fixed.
3. **Inspectors' answer:** change nothing. Add the heat gate for `allRegen`, and raise Pelter weight when the scan sees 30+ shifters. This is the minimum.
4. **Ingress (Psi).** Steal its `counterBias` ×2 and the clump-splash heat.
5. **Opening (Ground).** An add-on to a latch that does not work.

**I would sign the Undercut, amended.**

## 5. Questions for Question Time

1. **Rifle Corps:** your Disassembler needs `armor ≥ 12`, but the `gtank` is armor 6. Which enemy tank does it fire against, and what is its dps next to a shifter's 38?
2. **Psi:** why is the Pylon `.66` when a Shapeshifter is `.62`? Which post-v3.91.1 match proves a reach cut?
3. **Bio:** BIO-4's adaptation reaches 3,000. What is the damage-taken formula that does not go negative? And what does Kleiber cost when MP is pinned at 900?
4. **Science:** a Laser Tower is `w: 14`, not 19. Redo your saturation. Where is `tutquick`?
5. **Stability:** has Megaprojects adopted `civCap`? Who owns the fix if not?
6. **Lore:** why does the Plumb wait for Resolve 60 (1,000 s) while buried mass already counts half in Fear?
