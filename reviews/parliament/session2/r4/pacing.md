# PACING & TENSION COUNCIL — Second Reading (Round Four)
*Clock Bloc: Pacing, New Player, Game Feel. Slug `pacing`.*

## 1. Speech

Mr Speaker. The Address says nobody had the job of saying "too strong" or "too fiddly." The Secretariat has the job of saying "too long." A match that is eleven minutes is a war. A match that is twenty is a shift.

We conceded the clock last session, and the House gave us a fleet with a mind. We now hold the same line on every bill in this paper: it either runs itself, or we strike it. I have also done what the Autofleet invited and checked its arithmetic over a whole match. The direction is right and building always wins. Two of its numbers are wrong, one in each direction, and I correct them below, so the meter's hover text never lies.

To the Inspector-General: bring the clipboard. Our Interbellum is a wait, and nobody clicks during a wait.

## 2. The Re-reading

**PACING-1, The Second Siege (docket 17, held for the President) — AMEND.** One-Button: pass, the Interbellum and return are automatic, and the only input is the build the player was already doing. Line: the return arc and the climb are altitude bands only. Honest finding: length. Today a General match is 7:30 prep plus about 3:30 combat. Each extra cycle adds a 45% interbellum (3:22) plus about 2:06 of compressed combat, **+5.5 minutes**. Three cycles would make a 22-minute match. Amendment: `it.cycles` is **2 at General and below, 3 only at Apocalypse**. Echo cap stays (Autofleet: at most 25% of your count of that unit). Tenet: enemy gets scarier, the interbellum is a gift, nothing is nerfed.

**PACING-2, The Long Dark (inside The One Forecast) — KEEP as a renderer.** One-Button: pure display. Line: a time axis. No babysitting.

**PACING-3, Gambits — AMEND.** Ship **three** (Net, FOBS, Wedge). Line fix: the First Session said FOBS comes "from the south" and Wedge is "one flank", which are meaningless on a side-on strip. Rewrite: FOBS arrives low from the *edge of the strip* outside Radar reach for its first 8s. Wedge lands on *one end* of the strip (left or right). Net delays bombarders 40s so space is bought a window. One-Button: all enemy-side; the player answers by building. Tenet: scarier enemy, no player cap.

**PACING-4, War Charters — WITHDRAW.** The docket's Charter & Decrees (Lore) owns the "start posture" slot. Mine is a start preset, which is a buff that needs an enemy answer I never wrote, so it fails the no-power-creep clause. The Mandate toggle moves to the banked ×1/×2.5/×5 intensity tiers.

**PACING-5, Three Doors — KEEP** (inside The Nerve). One-Button: doors fire themselves. Line: flagship and altitude only.

**PACING-HOT — stays WITHDRAWN** (see Section 5).

**Docket 2, The Nerve — AMEND.** Adopt the corrections in PACING-6 below. One-Button: pass, the Truce is the only choice, made once. Line: map-wide scalar.

**Docket 17, The Fleet Will Return — AMEND** as PACING-1 above.

**Docket 4, The One Forecast — KEEP.** The Alarm Panel (AUTOFLEET-6) sits on the same strip as the manifest.

**Count: KEEP 4, AMEND 5, WITHDRAW 1** (PACING-4; PACING-HOT is already withdrawn).

## 3. The Order Paper

**#1** is answered in Section 2.

### Item 9 — PACING-6: THE FEAR LEDGER (the arithmetic, audited, and printed on the panel)

*Pitch.* "A meter that says it is a good bargain must be a good bargain. We checked."

**The audit (Autofleet's AUTOFLEET-6, whole match, General).** Assumptions are stated, since I have no per-second log: power converts to IC with exponent `c` (power ∝ IC^c); a doubling of all three inputs adds 0.5 F; enemy count `×(1+0.1F/1.3)`, enemy stats `×(1+0.08F)` (both hp and damage scale with `t`, L5582).
- **Per doubling, linear:** you ×2, enemy ×1.09 count × 1.04 stat ≈ 1.13. The ratio is **about 1.8:1**, not 11:1. The "+100% vs +9%" comparison is an increment ratio, and it silently ignores that the enemy's stats rise too.
- **Per doubling, Lanchester square law** (strength ∝ N²·hp·dps): you ×4, enemy ×1.19·1.08 ≈ 1.29, a ratio of **about 3.1:1**. After two doublings (F=1.0, about 600 IC/s) you are **~10:1**, which is where "11:1" comes from. It is true only as a cumulative at that point, and only if the whole economy converts.
- **Break-even.** Building loses only if doubling your economy buys less than about 13% more power (`c < 0.13`). Caps on mass (hangar slots, orbital lift, army cap: the cap formula is untouched) lower `c`, but none lowers it to 0.13 because mechanical units and buildings are uncapped. **Direction confirmed: building always wins.**
- **Error 1 (against the meter's honesty).** "F=4 needs about 16× economy" is wrong. `F = 0.5·mean(log2(x/base))`, so F=4 needs eight doublings, **256×** the baseline (about 38,000 IC/s). Report 7's 850 IC/s gives F ≈ 1.25, a mere +25% Lanchester. The panel's hover text must not promise a late-game bite it cannot deliver. The bite belongs to the Four Walls and the Second Siege.
- **Error 2 (a cliff).** `+floor(F)` bombarders adds a whole wall element at F=1.0, a **+20% jump** in the thing that kills space fleets, from one doubling's worth of income. It breaks the smooth trade.

**Amendments.**
1. Extra bombarders (beyond 5) arrive **without the full wallGrid** (4 escort fighters only) and count `round(F)` rather than `floor(F)`, so one doubling never adds a grid.
2. Fear tracks the *current* reading: bleed toward it at 10%/min, 1%/min only upward drift. A player who loses factories faces a fleet sized to what is left.
3. Print a live two-number readout under the ALARM needle: **FLEET ×1.13 · YOU ×2.0 (per doubling)**, computed from the live formula, and use it in New-Player's hover text instead of "11:1".
4. A dev invariant `devFearAudit()`: with `c=0.5`, assert ratio ≥ 1.5 at every F band. A rebalance can never make the sentence lie.

**Clerk's Annex.**
- **Code.** `spawnEnemy` L5581 (`t`), `ESC.OPEN_BOMBARDERS` L5160, `openingBombarders` L5306 (grid flag), `syncHUD` L11183, `LESSONS` L11525, `end()` L10345 (Fear paid vs power gained).
- **Size.** S-M (~120 lines on AUTOFLEET-6). **TM1.**
- **Tenet (Amendment I).** Enemy-side only; no nerf, no player cap. It removes a hidden cliff, which is the opposite of power creep.
- **One-Button.** Zero input. **Line.** Map-wide scalar.

### Item 2 — PACING-7: THE SIEGE CLOCK (when the huge hulls arrive, and how long a monoculture grinds)

*Pitch.* "A wall that is paper to a mixed army is not a wall. A wall that never falls is not a game. Set the clock."

**The grind math.** With deaf-family floor 0.35 (AUTOFLEET-7), effective dps = Σ share × multiplier. Pure monoculture ×0.35 (2.9× slower). A 30% minority of the answering family gives `0.7·0.35 + 0.3 = 0.545`, **1.56× faster for 30% of the spend**. A 50/50 mix gives 0.675. This is the "forced to get new things" curve: the first third of diversification does most of the work, so mixing is cheap and mono is slow, never forbidden.

**The problem.** At Autofleet's numbers (Basalt 2,600·t HP, t≈3.4 at invTime 100, so about 8,800 HP), Report 7's average 810 dps (173k dealt in 3:34) kills it in **11s mixed, 31s mono**. A Wall every 60s is paper. Mono must grind long enough to *notice* and short enough to *survive*.

**Mechanic.**
- Walls arrive from **invTime 110** (phase 3 plus 60s). The flagship lands about invTime 75. A 35s gap keeps two peaks apart.
- Wall HP scales `×(1 + 0.35F)` and is **calibrated by a headless run** so the Report-8 reference army kills a Wall in ~20s mixed, ~58s mono. (Calibration is the whole job: HP is derived from measured `G.stats` throughput, never guessed.)
- Cadence is smooth, `interval = 60 / (1 + 0.25F)` clamped to ≥30s, one hull at a time. This replaces `group size 1+floor(F)`, a step.
- **An unanswered Wall (alive 90s after landing) anchors** and begins its weapon on the nearest city. That is the enemy's real pressure on a mono army: not a cap, a timer that bites.
- Telegraph: a Wire line and a Because chip say *"BASALT inbound: deaf to guns"* 20s before landing, so the lesson is visible.
- Withdrawal: Walls count in `breakCount`, one each, so a slow grind delays the win but never blocks it.

**Real science.** Ablative and dielectric armour shift in effectiveness by threat type; operations research uses *time on target* the same way: the question is not whether you can kill it, but whether you do before it fires.

**Clerk's Annex.**
- **Code.** `spawnEnemy` L5581, `tickPhase4` L5969, `tickCarrierArrival` L5401, `counterBias` L5513, `breakCount` L5989, `scanEnemyIntel` L5737.
- **Size.** S-M (~140 lines on AUTOFLEET-7). **TM1.**
- **Research.** None; cracks are the existing `directedenergy`, `plasma`, `pointdefense`, `particlebeam` and rail nodes.
- **Tenet (Amendment I).** A scarier enemy. No cap: mono still wins, slower. No power creep: no unit is nerfed or made worse.
- **One-Button.** The player mixes by building. Nothing is steered. **Line.** Basalt walks the strip; the rest hold altitude.

## 4. Blatantly overpowered today

Nothing in my portfolio meets the bar. The nearest candidate is the **single withdrawal door**: five wins by one method (Reports 6–10). It does not make losing impossible, only repetitive. I propose no nerf. The fix is more doors (Three Doors) and a fleet that answers (The Nerve, The Fleet Will Return).

## 5. Struck self-limits

**PACING-HOT's prep-clock drain** was struck in the First Session as a tenet knife-edge. It should **not** return. Nothing proves a faster clock stops a blatant exploit, and Fear now charges for building visibly and proportionately. No other limit of mine was struck.
