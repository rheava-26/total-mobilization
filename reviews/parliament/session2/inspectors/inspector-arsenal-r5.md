# Inspector-General of the Arsenal Bloc: Second Audit (Round Five)
*Checked against `title.html` (12,243 lines). Read: r4 papers of energy, kinetic-arms, railgun, physics, plus armour, artillery and autofleet.*

## 1. Opening

Honourable members. I asked for a wall and you have built a labyrinth. Four councils now hold a multiplier each, and every one swears the others make it safe. I have taken my stopwatch to the product. The Lance is no longer 441 per hit. On a fresh capital it is 2,778. I do not object to big numbers. I object to big numbers that arrive from five directions and call themselves one design.

## 2. The second charge sheet

**Bloc counts: CLEAN 7, CHARGED 5.** Interlocks are ruled separately.

**Energy: Adaptive Hull + Thermal Stress. CHARGED (FALSE, CREEP-by-enemy).**
- **FALSE.** The meters and the Stress multiplier sit in `hurt()`. `splashDamage` and `areaDamage` write `e.hp -= d2` directly (L7553, L7544). Prox-fused fighter missiles take that path (L9146-9149).
  - The claim that "a 240-Falcon blob meets a hardened hull" is false. Falcon missiles never fill the explosive meter and never see Stress.
  - The same blind spot covers ARTILLERY-1's splash and Kinetic's spall.
  - The doctrine the President most wants walled is the one the wall cannot see.
- **Unspecified.** Gain is `12*dmg/maxHp` and decay is `0.08*a`, so steady state `a = g/0.08` clips at 1 whenever a family deals more than `maxHp/150` per second. That is factor 0.25, not the "0.48" in the worked example, and below Four Walls' floor of 0.35.
- **Fix.**
  1. One `dealDamage(t, dmg, family)` entry that `hurt`, `splashDamage` and `areaDamage` all call.
  2. State the saturating curve.
  3. Share a single 0.35 floor with Four Walls.

**Energy: ENERGY-H Furnace, auto-fire. CLEAN.** Auto-fire fixes the micro. Division by thermal mass fixes the saturation.

**Kinetic: Finisher (Spall). CLEAN.** It is one `kinMul` computed once, which is the ledger I asked for. Spall is a fixed 25% to neighbours only, gated at heat 70 on medium-or-heavier hulls, with a 0.4 s per-hull limit.
- Rider: spall goes through the shared `dealDamage` so meters see it.

**Kinetic: Rod Watch. CLEAN.** It is instrumentation with a pre-agreed enemy response. A nerf is allowed only if the enemy answer fails twice. That is the Amendment I shape.

**Kinetic: Long Gun Coalition (amended). CLEAN.** The gun auto-targets, and that satisfies my micro charge. Rider: the +4% per linked unit volley bonus stays struck.

**Kinetic: Fire Direction Net (amended). CLEAN.** Auto-fire on the densest medium-or-heavier cluster, with an `AUTO`/`HOLD` toggle. Micro is satisfied.

**Railgun: Hypervelocity Family. CHARGED (MONOCULTURE, CREEP).**
- `a_hyp` gains at 0.5x. A Lance-only army settles near factor 0.77, while a Hailstorm-only army settles near 0.48.
- That rewards the most-built weapon (282 in Report 6). Rails are already `ceil:0`, uninterceptable (L8526) and unlimited-ammo.
- The per-hit cap of 0.6 is decoration.
- **Fix.** Equal gain across families, or a matching cost on the family it favours. Better: drop the separate meter and add `hyp` as one more family in the shared table.

**Railgun: Silencer Wing. CLEAN.** The enemy answer scales with sections (3 Fear per section, one Silencer per 10), so N stays uncapped. The anchor-HP tripwire is the right lever.

**Railgun: Launch Rail RESEED. CLEAN.** Standing toggle, named enemy target.

**Railgun and Energy: Power Grid. CHARGED (EXPLOIT).**
- Overdrive (x1.5, +25% rate) still applies to the Mass-Driver Railway, whose adjacency is `1 + .6 x segs` (L8448). It packs about 8 neighbours at `OVERLAP_FRAC` .32, so x5.8.
- Both Railgun and Physics now call this a "watch item". Physics proposes a ledger but does not commit to one.
- The O(n^2) scan is unaddressed.
- **Fix.** Put the railway term in `kinMul`. Raise the `busterScore` (now 72) of any cluster of four or more massrails.

**Physics: Thermal Mass. CHARGED (EXPLOIT, FALSE guard).**
- Energy's guard says "bosses cannot be pinned at 100 by a handful of lasers". Arithmetic: a capital has mass 30. One Solaris deposits 69 heat/hit, divided by 30^0.6 = 7.7, so 9 per hit. At 1.75 hits/s that is 15.7 heat/s.
- Cooling at heat 100 is `(2+8) x 30^-0.33`, about 3.2/s. One plane pins a capital, with 5x margin.
- `mass = baseHP/300` ignores the `t` scaling, so late-game bosses are no harder to heat.
- The ablation plateau turns excess laser fire into free HP damage.
- **Fix.** Deposit divides by the live scaled `maxHp`, so laser volume must track boss HP. Drop the plateau, or make it a real sink.

**Physics: The Column (PHYSICS-7). CHARGED (CREEP, PERF).**
- Overkill carries on at 0.7 to 3 more bodies. Overkill is huge once research x2.55 is in: one round can clear a 4-deep column of landed `gtank`.
- Its own enemy answer ("chaff screens the heavy") is the same rule, which also helps the player.
- Each kill triggers a `GRID_E.firstHit` lookup, in the hot path.
- **Fix.** A target with armour 8 or more absorbs the carry. Meters see the carry hits. Cap lookups per frame.

**Interlocks.**
- **ARMOUR-2 The Anvil. CHARGED.**
  - It re-states the superseded x1.5 heat bonus on top of Thermal Stress, adds sabot x1.3 and cold x0.55.
  - It duplicates Four Walls' Basalt (guns x0.35 below heat 50).
  - Its Falcon question: the Anvil gates `weapon:'shell'`, so it cannot touch Falcons. And Falcon missiles bypass `hurt()` anyway.
  - **Fix.** Keep one idea: plasma tanks as armour's own heater. Delete the multipliers.
- **ARTILLERY-1 HE. CLEAN.** It is a real fix and a volume scaler. Only the splash-path blindness applies.

## 3. Old charges

| Charge | Verdict |
|---|---|
| D1 monoculture wall | **NOT SATISFIED.** It exists on paper (hull plus Four Walls) and is blind to splash in code. |
| D1 stack 441 to 1,389 | **NOT SATISFIED.** It is worse. Fresh capital: 441 x1.4 AP x3.0 Stress x1.5 overdrive = **2,778**, and the hull only bites after warm-up. |
| D1 `hurt()` misses splash | **NOT SATISFIED.** Nobody mentions it. |
| D1 armour cut inert | **SATISFIED.** Struck by three councils. |
| D1 drop-window pile-up | **NOT SATISFIED.** No `decoy` scaling was adopted. |
| D7 per-group selector (MICRO) | **SATISFIED.** Standing choice plus `AUTO`. |
| D7 sabot +20% (FALSE) | **SATISFIED.** AP is now +40% vs medium-or-heavier. |
| D7 airburst vs single targets | **NOT SATISFIED.** KINETIC-1 is KEEP and unchanged. |
| D10 manual targeting | **SATISFIED.** Auto-targets. |
| D10 uncapped stack | **SATISFIED, partly.** Silencers answer the gun. Superlinear upkeep was declined; I accept that if the Silencer ships. |
| D11 click barrage | **SATISFIED.** |
| D15 massrail x5.8 | **NOT SATISFIED.** |
| D15 debt without bite | **SATISFIED.** Shortfall eats income down to `IC_FLOOR` 8 (L6254, L6330), a real if bounded cost. |
| D8 buried tubes untouchable | **NOT SATISFIED.** No enemy reaches `ug`. |
| D6 sweeper chore, Furnace micro | **SATISFIED.** Both dropped or automated. |
| Breathing Sky creep | **SATISFIED, conditional** on the autofleet reboost. |
| Aura stacking (x15 to x34) | **NOT SATISFIED.** No bloc paper mentions it. |
| Shapeshifter heat answer hollow | **NOT SATISFIED.** |

**Tally: 8 SATISFIED, 9 NOT SATISFIED.** The heat design walls monoculture only where damage goes through `hurt()`. The Lance chain stayed bounded only by enemy terms that start cold.

## 4. The variety verdict

The pieces: Energy's Adaptive Hull and Stress, Autofleet's Four Walls, Physics' Thermal Mass, Kinetic's Finisher, Railgun's adaptation meter and Armour's Anvil.

**The one coherent system I would sign:**
1. **One damage entry** (`dealDamage` with a family tag), so every weapon sees every wall. Without it, nothing else matters.
2. **Thermal Stress** by mass class, as Energy wrote it, as the single heat multiplier. Physics' Thermal Mass is kept only with deposit scaled by live `maxHp`.
3. **Adaptive Hull**, one program for medium and heavy hulls, with `hyp` as a plain family at equal gain.
4. **Four Walls as the doctrine content**, with Basalt exempt from hull meters so the two walls do not multiply.
5. **Finisher's `kinMul` ledger**, with the railway term inside it.

**Redundant:**
- Armour's Anvil multipliers: Stress and Basalt already cover them. Keep only plasma tanks as heaters.
- Railgun's separate meter and its half-gain.
- Energy's Quench Tender and Four Walls' Prism overlap (cooling vs mirror) and need one owner.

## 5. Questions for Question Time

1. **Energy:** which entry point carries the family tag for `splashDamage`, `areaDamage` and spall? Show the Falcon missile's path.
2. **Energy:** give the saturating form of the adaptation meter, and say whether the floor is 0.25 or 0.35.
3. **Physics:** will you divide heat deposit by live `maxHp` and drop the ablation plateau, given that one Solaris pins a capital by a 5x margin?
4. **Railgun:** why should rails adapt at half the rate of every other kinetic family?
5. **Armour, Autofleet:** is Basalt exempt from the hull meters, and does the Anvil's cold hull survive as a separate rule at all?
6. **Kinetic-Arms:** will you commit the Mass-Driver adjacency into `kinMul`, and keep KINETIC-1's airburst off any unit it would weaken against single targets?
