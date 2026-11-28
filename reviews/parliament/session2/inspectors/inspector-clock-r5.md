# Inspector-General of the Clock Bloc and the Observer Delegation: Second Audit (`inspector-clock`)

## 1. Opening

Speaker. The stopwatch has run again. The House has been busy: six bills now touch one meter, and three of them quote an 11:1 that nobody timed. I timed it. The sponsors read my first report closely and fixed the easy things. The two charges that need real work, the never-reset `t` and the killer nobody recorded, went unanswered. I am not offended. I am merely holding the stopwatch and waiting.

## 2. The second charge sheet (8 entries: 3 CLEAN, 5 CHARGED)

**AUTOFLEET-5 The Standing Answer: CHARGED. TENET.** Default posture "Hardened" costs 8% of IC income at all times. At Report 7's 850 IC/s that is 68 IC/s, a Falcon every 4 s, spent whether or not a Weasel ever launches. A default-on tax on every match is a build-fantasy tax with no enemy cause.
- **Fix:** auto-EMCON and auto-repair are free in every posture. Upkeep applies only to "Dispersed", per buried building, because it actually buries something.
- It also overlaps Air's Hardened Shelter, so one of the two should go.

**AUTOFLEET-6 The Alarm Panel: CHARGED. FALSE.**
- "F=4 needs about 16× the economy" is wrong. `F = 0.5·mean(log2(x/base))`, so F=4 needs eight doublings, 256×, or about 38,000 IC/s.
- At Report 7's 850 IC/s the IC term is log2(5.7)=2.5. With buildings and army it lands near F≈1.25, which is +10% hostiles and HP, so the meter barely bites.
- `log2(armyWeight/100)` goes negative below weight 100. `armyCap` (L4572) bounds that weight near 50–150, so the army term drags F down.
- The ratchet with a 1%/min bleed is unchanged from my first charge.
- **Fix:** clamp each term ≥0, track current input rather than a ratchet, and quote the live numbers (see §4).

**AUTOFLEET-7 The Four Walls: CHARGED. CREEP and FALSE.**
- `hurt()` (L4978) takes no weapon family. "Optional argument from the firing path" is the missing attribution again, and it touches 28 call sites.
- Basalt resists guns and shells ×0.35 "unless heat ≥50". That depends on Docket 1. If the Walls ship first, the existing kinetic army grinds 2.9× slower.
- Wall HP is `×t` with no reset.
- **Fix:** ship after Thermal Field. Stamp the family once at `fire()` into the projectile, shared with the Loss Ledger. Cycle-reset `t`.
- The design itself is sound. Floor 0.35 means mono still wins, only slower. Thornback's ×3 flak is moot for aircraft that already die in one hit (Falcon HP 55, an enemy hit ≈100).

**PACING-6 The Fear Ledger: CLEAN.** The mechanism is right and the amendments are right: `round(F)` bombarders without the grid, tracking current input, a live readout, and a dev invariant.
- One erratum is demanded. It says "both hp and damage scale with `t`". That is FALSE for line units: `dmg:17/28/55` are literals in `spawnEnemy` (L5593–5604), and only HP carries `t`.
- Its "1.09 per doubling" for count also contradicts its own printed formula, `0.5×0.1/1.3 = 1.038`.

**PACING-7 The Siege Clock: CHARGED. TENET.**
- "HP is derived from measured `G.stats` throughput" reads as live rubber-banding. Enemy HP rising with the player's measured damage is a hidden punishment for building. It is Fear again, unshown.
- If it is a one-time headless constant, say so. A constant calibrated to the Report-8 army is also 10× too strong for a recruit. The 90 s anchor then hits weak players hardest.
- **Fix:** constant HP tables per intent. The anchor timer scales with the player's `playerDefStrength()` (L4586).
- Its timings (3:22, 2:06) also conflate internal and real seconds. At 1× speed, 450 internal is 1,000 real.

**NEW-PLAYER-7 The Fleet Follows Your Factories: CHARGED. FALSE.**
- "9% more ships and about twice the guns" is wrong twice. The count rise is about 4% per doubling, HP is +4%, and "twice the guns" assumes power ∝ IC.
- The lesson card carries rationale and an imperative ("Keep building"), against the text rule.
- **Fix:** both lines are generated from the live formula (§4).

**NEW-PLAYER-8 The Sub-Tabs: CLEAN.** I verified DEF is 22 (19 plus `mgnest`, `flaknest`, `artnest` at L4559–4561). `ALL_CATS`, `curBcat` and the `hideUnimportant` flag all exist. ALL is the default, so nothing is hidden.

**GAME-FEEL-6 The Curtain Call: CLEAN.** Cached cues, no input, gold not red. Rider: its banner "+9% ships, +100% yours" must read the same live string as the panel.

## 3. Old charges

| # | Charge | Status |
|---|---|---|
| 1 | Fear ratchet (TENET) | NOT SATISFIED. Autofleet keeps 1%/min. Pacing amends, but does not own F. |
| 2 | Fear boss entries / recon | SATISFIED (dropped). Walls must still skip `recon`. |
| 3 | Resolve ×100 | NOT SATISFIED. Lore's re-read is silent. |
| 4 | Orbital Denial gate | NOT SATISFIED. |
| 5 | Bombarder grid ceiling | SATISFIED (cap 9, no grid on extras). |
| 6 | Decisive Strike blackout only | NOT SATISFIED. Unspecified. |
| 7 | Reaver wing sized to volume | NOT SATISFIED. |
| 8 | Durandal "8+" | NOT SATISFIED. |
| 9 | EMCON automatic | SATISFIED (Air and Autofleet). |
| 10 | **Return: `t` never reset** | **NOT SATISFIED.** Pacing cut cycles but never mentions `invTime`. |
| 11 | **Missing killer attribution** | **NOT SATISFIED.** No paper answers it. It still starves the Loss Ledger, Mirror, Standing Plan, Post-Mortem and Exercise. |
| 12 | Return cycle count | SATISFIED (2 at General, 3 at Apocalypse). |
| 13 | Return ETA fog | NOT SATISFIED. |
| 14 | Return: max not sum for bombarders | NOT SATISFIED. |
| 15 | Forecast: countdown / tier 0 downgrade | NOT SATISFIED. |
| 16 | Standing Plan monoculture loop | NOT SATISFIED. |
| 17 | Long Gun Interdiction keyed to sections | NOT SATISFIED. |
| 18 | FOBS "from the south" | SATISFIED (rewritten to strip edges). |
| 19 | Charters rider | SATISFIED (withdrawn). |
| 20 | Post-Mortem `lossLog` | NOT SATISFIED. |
| 21 | Warhead Cam MICRO | SATISFIED (first 3 or >40 stab, On/Off). |
| 22 | Mirror damage flat | NOT SATISFIED. |
| 23 | Mirror count by weight | SATISFIED (≤25% of own count, max 24). |
| 24 | Chaff Barge stacking | NOT SATISFIED. |

**Satisfied: 8 of 24.** Answering my first two: no, `t` was not fixed, and no, nobody answered attribution.

## 4. The Fear Index verdict

**Whose arithmetic?** Pacing's direction is right, and Pacing is closer on the headline. Autofleet's "11:1" is an increment ratio (+100% against +9%), and its "+9%" is itself rounded up. From the paper's own formula, per doubling of all three inputs (ΔF = 0.5):
- Hostile count: ×1.038.
- Hostile HP: ×1.04. Damage stays flat.
- Combined: about ×1.08, so you ×2 against them ×1.08 is **1.85:1 linear**.
- Lanchester square: you ×4, them 1.038²×1.04 = ×1.12, so **3.6:1**.
- Pacing's 1.8:1 is right. Its 3.1 is low, because its own count term was wrong.

Both papers assume power ∝ IC, and neither knows what an IC buys at a given `c`. A hoarder at 212k IC (Report 7) gets no power from income they do not spend. The panel must not claim a trade the player has not made.

**The design I would sign.** One host, the Alarm Panel:
- **Formula.** Autofleet's log F, with every term clamped to ≥0. F is the smoothed current input (τ≈60 s, symmetric), with the peak shown as a HUD tick only. Hard clamp at 4.
- **Effects.** Hostile count ×(1+0.1F/fleetMul). Hostile HP ×(1+0.08F). `round(F)` bombarders without the grid, capped at 9 in total as the max of the Fear and Return terms. Never damage.
- **HUD.** A gold needle with three contribution bars: Industry, Buildings, Army. Beneath it, a live readout computed from the formula: "Fleet ×1.08 · You ×2.0 per doubling".
- **Explanation text** (hover, generated from the live values):
  > The fleet measures your industry. Each doubling of your economy adds 4% hostiles and 4% hostile hit points. It never raises their damage. Fear stops at 4.
- **Bands.** Rename Fear's top band. Resolve already uses "Desperate", and two meters sharing a word is a bug waiting to be filed.

**Redundant pieces, by name:**
- **LORE-7 The Assay pill.** It uses the old linear formula (`icRate/300`, `bldgTier/120`), which conflicts with the log F. Its "+22 hostiles against +160 units/min" mixes a one-time count with a rate. Fold its in-universe reason into the hover tooltip.
- **NEW-PLAYER-7 lesson card and its static text.** Replace both with the live string.
- **The Curtain Call banner numbers.** Same live string.
- **STABILITY-6 The Rally and the Shock.** Keep the Rally, since habituation stops farming. The Shock (×1.25 at Desperate) is moot, because F≥3 needs about 64× the economy.

## 5. Questions for Question Time

1. **`pacing`:** does `invTime`, and therefore `t`, reset each cycle? Your R4 paper cut the cycles and never answered.
2. **`autofleet`:** who stamps the weapon family and the killer? Will you do it once at `fire()` and share it with the Loss Ledger and Mirror?
3. **`autofleet`:** will you sign "Fear tracks the current input, never a ratchet"? Pacing already did.
4. **`pacing`:** is Siege Clock HP a fixed table or live from `G.stats`? Your annex says both.
5. **`new-player`:** will the Fleet Follows text be generated live, so a rebalance cannot make it lie?
6. **`game-feel`:** how does Interdiction scale on the Long Gun, if F clamps at 4 and sections do not?
