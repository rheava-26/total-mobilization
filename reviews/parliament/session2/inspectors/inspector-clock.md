# Inspector-General of the Clock Bloc and the Observer Delegation (`inspector-clock`)

## 1. Opening speech

Speaker. I am the Inspector of Rhythm. I carry a stopwatch and a metronome, and I have timed every war since the first one. Both are in a bad mood.

My bloc wants the fleet to fear, to think, to return, and to be heard from a postcode away. I love all of it. I also read `destroyEnemy` (L6840) and found that it records what died, never what killed it. Four bills tell the fleet to "answer what killed it last". They are asking a witness who was never in the room.

The Nerve's Resolve gauge falls by `v/fleetMax` per kill. By that arithmetic you could destroy the entire fleet and lose one point of a hundred. The Second Siege runs a clock that never resets, so by minute twenty-five a Falcon needs 1,300 hits to kill a leviathan. The Mirror hands the enemy our best unit at 14 times its hit points.

I stopped the clock on 34 entries. Sixteen are clean, and I say so with a sour face. Eighteen are charged. Please do not applaud while I am timing.

## 2. The charge sheet

Shorthand: *no attribution* means `hurt(t, dmg, ix, iy)` (L4978) and `destroyEnemy` carry no killer. `killsByType` (L6844) is keyed by the enemy's kind, so it counts what the player killed, never what killed it.

### The President's question: does the Fear Index punish building?

**The Fear Index (AUTOFLEET-2, inside The Nerve).** I did the arithmetic at General (`fleetMax = 270 × 1.4 × 1.3 = 491`, L6499).

`F = (icRate/300 + bldgTier/120 + armyWeight/200)/3`, clamped to 4. A point of input is 300 IC/s, or 120 tier-points of buildings, or 200 army weight. Report 7's 850 IC/s is 2.83 points, so F is about 0.94 from income alone.

| F | Input points | Hostiles | Hostile HP | Bombarders | Extra bosses | Other |
|---|---|---|---|---|---|---|
| 0 | 0 | 491 | ×1.00 | 5 | 0 | |
| 1 | 3 | 508 | ×1.08 | 6 | +1 | |
| 2 | 6 | 525 | ×1.16 | 7 | +2 | skyfall doubled once |
| 4 | 12 | 558 | ×1.32 | 9 | +4 | |

- **Verdict: no crime in aggregate.** F=4 costs the player at least 12 input points and gives the enemy about ×1.5 on line HP and ×2.3 on bosses. Building out-grows the fear by at least 2:1 in linear terms and 4:1 under Lanchester's square law. That holds only if all of these stay true:
  1. F touches count and HP, never per-unit damage. Line-unit `dmg` is a flat literal in `spawnEnemy` (`dmg: 17`, `28`, `55`). Only HP carries `t`.
  2. F stays clamped at 4.
  3. F is not a ratchet.
- **Charge: TENET.** The ratchet decays only 1% a minute. A player whose industry is bombed flat stays at F=4 while rebuilding. The game has an `IC_FLOOR` (L6254) so a bombed nation can always come back, and a ghost-economy tax contradicts it.
  - **Fix:** F is the smoothed current input (τ≈60 s). Keep the peak for the HUD tick only.
- **Charge: CREEP.** "Each full 1.0 of F adds one `supersPlan` entry" names no kind and no time. On RECON (`supers: []`) it would bolt leviathans onto a "light probe".
  - **Fix:** extra entries cycle through the intent's own plan kinds, only from t≥50, and never on `recon`.
- **Why it feels like punishment.** The live game already punishes building, and invisibly. Every placement snaps `infMomentum` back to 1.0 (L6366). `scanEnemyIntel` reads your buildings and counters them. The fix for both is visibility.

**What the player must be shown.** The Fear panel holds one needle and three input bars (income, buildings, army). Each bar shows its share of F. Under the needle sit the exchange rate and a true-versus-believed tick.

> *Fear: 1.8.* **Your output +210% · Fleet +14% hit points, +6% hostiles, +1 bomber wall, +1 boss.** *The fleet scales to what you build. It raises hostile numbers and toughness, never their damage. Fear stops at 4. Your output has no stop.*

The panel must always print both sides. A one-sided "Fleet +14%" reads as a tax. Both sides read as a trade the player wins.

**The Nerve (Fear, Resolve, four doors; 23 votes). CHARGED.**
- **FALSE:** Resolve falls `v/fleetMax` per kill, where v=1 for a line unit. Killing all 491 units drops it by 1 point out of 100. Bands at 60 and 30, Rout at <60 and Truce at <20 never trigger. It needs ×100.
- **EXPLOIT (Orbital Denial):** "<4 enemy hulls in the space band for 40 s after phase 3" is satisfiable while 85% of the fleet is still undropped. Re-entering pods also pass through that band, so the check is also noisy.
  - **Fix:** Denial additionally needs `G.fleet ≤ 50%·fleetMax` and Resolve<60.
- **CREEP (bomber wall):** Pacing's R3 asked for the 9-bombarder ceiling to go. See §4.
- **Rethink:** Decisive Strike must stay a blackout (`detonateEmpBurst`, L5871, a 14 s zone, no damage). A damaging "antimatter airburst" would be CREEP.

**The Fleet Reads You (13 votes). CHARGED.**
- **CREEP, inverted:** Reaver wings of 6–10 answer 240 Falcons, about 4%. An answer that does not scale with volume is decoration.
  - **Fix:** wing size is `round(6 × norm.air)`, up to `POP_CEIL`.
- **EXPLOIT:** Durandal hits only bases with 8+ parked aircraft, so the meta is "park 7".
  - **Fix:** hit the top two bases by parked count, with strength scaling by count.
- **MICRO:** EMCON as a per-emitter 12 s order.
  - **Fix:** automatic. Radars blink when an anti-radiation missile is inbound, one standing doctrine.
- **FALSE:** the pair-read and the Mirror both assume attribution. See below.

**The Fleet Will Return (11 votes, held for the President). CHARGED. My most serious charge.**
- **CREEP:** `t = diff×(1+invTime/70)` (L5589). The paper never says `invTime` resets.
  - If it does not, 3 cycles push t past 30 and 49 by cycle 3. A Falcon hit does about 109 against a fragile target (`HIT_BUFF 1.3 × 2.7`, L4987). A leviathan then has 83,000 HP and armor 22, so about 1,300 hits. That is exactly "blows up your unbuffed units".
  - **Fix:** `invTime` and `t` reset each cycle, restarting at `diff×(1+0.25·cycle)`.
- **FALSE:** the counter-doctrine and the Loss Ledger read "kills by domain". That data does not exist.
- **TENET:** "ETA ±40 s without a Tracking Array" fogs a clock the player has exactly today.
- **PACING:** `it.cycles.invasion = 3` triples every General match. Ship 2 cycles as default and 3 as a Charter Mandate.
- **Stack:** the Return wall is `5+2c` and Fear adds `floor(F)`. Take the max, not the sum.

**The One Forecast (8 votes). CHARGED.**
- **FALSE / TENET:** the HUD already shows an exact `T-Ns` from the first second (L11202), the named intent, and an exact `~N hostiles` in `tutIntentBlurb` (L11754). "ETA ±40%", "exact only from T-60" and a ±40% tier 0 all downgrade information the player holds today.
  - **Fix:** keep the exact countdown, intent label and tier-0 fleet figure. Bands apply to composition and gambit only.
- **PERF:** the "n observers" band reaches the ±5% floor at 63 observers. Radar spam is a mild monoculture; the rider is that each Radar is a Weasel target.

**The Standing Plan (11 votes). CHARGED.**
- **MONOCULTURE / FALSE:** Exchange lines buy "last fight's best kills-per-IC", which needs attribution.
  - Once built, it feeds the Mirror: the Plan mass-produces the top killer, and the Mirror copies it. A loop.
  - **Fix:** the default auto-split spreads across the top three. The manual path is untouched.

**The Long Gun (game-feel co-sponsor). CHARGED.**
- **CREEP:** damage scales with sections, unbounded. The enemy answer (Interdiction) must scale with the same count.
  - **Fix:** wave size and boss tier are keyed to section count, not to F, which clamps at 4.
- The cinematic itself is CLEAN: 1.2 s of dilation every 20 s is 6% of play.

### Pacing Secretariat (`pacing`)
- **PACING-1 Second Siege: CHARGED.** See Return.
- **PACING-2 Long Dark: CHARGED.** See Forecast.
- **PACING-3 Gambits: CHARGED. LINE.** "FOBS from the south" means nothing on a side-on strip.
  - **Fix:** FOBS arrives from the map edges, east or west, past the radar ring.
- **PACING-4 War Charters: CLEAN.** The Mandate scales the fleet only. One rider: "+10% army cap" touches the cap formula (Art. IV.1), so grant a starting building, not a cap change.
- **PACING-5 Three Doors: CHARGED.** See Denial.
- **PACING-HOT: CLEAN, withdrawn.**

### New Player (`new-player`)
- **NEW-PLAYER-1 Order of Battle: CHARGED.** See Forecast.
- **NEW-PLAYER-2 Reach Curtain: CLEAN.** The sky's vertical bands are the Line's real dimension. Draw cost is one wash.
- **NEW-PLAYER-3 Ledger: CLEAN.** Information only.
- **NEW-PLAYER-4 Because Chip: CLEAN.** Refresh at ≤4 Hz, not per frame.
- **NEW-PLAYER-5 Post-Mortem: CHARGED. FALSE.**
  - `lostByType` (L6823) is cumulative, with no timestamps. "42 of 44 Halos died inside 90 s" cannot be computed.
  - The "heat strip scrub" cannot work, because `heat` (L1466) is a spatial grid.
  - **Fix:** add a ring-buffer `lossLog` of `{t, key}`, and scrub the `hist` time series instead.
- **The Exercise (NEW-PLAYER-6 into LORE-4): CHARGED. FALSE.** `killsByType` seeding `threatWeight` seeds enemy kinds, and so punishes the player for killing them.
  - **Fix:** key the drill's reading on the player's drilled unit families. That needs attribution.

### Bureau of Spectacle (`game-feel`)
- **GAME-FEEL-1 Mix Bus: CLEAN.** I verified `noise()` allocates a buffer per call (L1122). The 24-voice budget protects the new nodes.
- **GAME-FEEL-2 Impact Budget: CLEAN.** I verified 56 `shake(` sites and a max-latch (L6682).
- **GAME-FEEL-3 Director: CLEAN.** Sim `dt` is smaller, never skipped.
- **GAME-FEEL-4 Roar: CLEAN.**
- **GAME-FEEL-5 Legible Mass: CLEAN.** Trimmed to the density glyph and team sparks.
- **GAME-FEEL-H Warhead Cam: CHARGED. MICRO.** Reports 6–7 fired 44–46 strikes. At `timeScale 0.2` for every flight, each needs a click or Esc to skip. A default of "Always" turns the nuke into a chore.
  - **Fix:** default "First 3", and the cam never runs while any other strike is airborne.
- **Thermal Field (co-sponsor): CLEAN** on my terms. Heat is a cached bitmap, never per-frame glow.
- **Game-feel bundle (Spin-Up, Gun Voice, Impact Budget, Legible Mass, Because Chip): CLEAN.**

### Autofleet Observer Delegation (`autofleet`)
- **AUTOFLEET-1 Uncounted Columns: CHARGED.** See Reads You. The Chaff Barge's 45% lock-loss on player missiles needs a cap on cloud overlap, or two barges stack to total denial.
- **AUTOFLEET-2 Fear Index: CHARGED.** See Fear above.
- **AUTOFLEET-3 Weakest Axis: CLEAN.** Each event is 45 s, answerable, and proportional.
  - One rider: the Command event never fires first (accepted in R3).
- **AUTOFLEET-4 Rethink or Despair: CHARGED. FALSE.** The Loss Ledger "kills per weapon family" cannot be computed.
- **AUTOFLEET-HOT The Mirror: CHARGED. FALSE and CREEP.**
  - **FALSE:** "highest `killsByType` share" selects enemy kinds.
  - **CREEP:** `dmg = def.dmg × t × 0.8` is the first enemy line unit whose damage scales with `t`. At invTime 700, t=15.4, so an echo Falcon has 14× the player's HP and 12× its damage. 24 per drop is about 4,000 Falcon-equivalents of product strength against 240.
  - **Fix:** echo damage is flat ×0.8. HP scales with t. The count is budgeted in weight (`defWeight`, L3801), not bodies, so a Titan echo (weight 2) counts double and 24 Titans cannot appear.

**Dead Sky Protocol (autofleet joined): CLEAN.** The fleet's EMP-hardened escorts are an enemy answer.
**Dossier Line (autofleet joined): CLEAN.**
**The Wire as Home Front: CLEAN.** The Managed default and true receipts are right.

## 3. Blatantly overpowered today

1. **Air plus sky navy.** Report 8 fielded 240 Falcons, 113 Kites and 138 capital ships, with a 10:1 damage ratio. Reports 6–10 were all wins. `scanEnemyIntel`'s `norm` has six axes (L5775) and no air or sky-navy axis. A split army also loses to the ≥1.4× dominance test by construction (L5778). So the fleet is blind to the doctrine that beats it.
   - **Verdict:** enemy answer, not a nerf. That is The Fleet Reads You, sized to volume. Amendment I's bar for a nerf is not met.
2. **Perfect, deterministic information.** The intent label, an exact `T-Ns`, an exact fleet count, phase 2 at invTime 18, phase 3 at 50 and the wall of five at phase 3. A day-one player can plan to the second.
   - **Verdict:** enemy answer. Gambits, with FOBS fixed, are the right tool. It is not OP enough for a nerf.
3. **The 85% door.** `breakCount ≤ 15%` ends the match in 15 s with +15 stability (L5997, L6512, L6552). Five straight wins went this way.
   - **Verdict:** enemy answer, the Return, but at 2 cycles by default.

## 4. Struck self-limits that should return

- **The 9-bombarder ceiling.** Pacing R3 asked to drop it. One wall bombarder is `1 + 4 escorts + 8 + 8 + mender = 22` hulls (L8728). 5 gives 110, 9 gives 198, and 13 gives 286, which is above `POP_CEIL 280`.
  - At that count `doWave` stops firing (L6546), so the fleet pool never drains and the war cannot be won until the grid dies. That is also the grid that killed 42 of 44 Halos in Report 7.
  - It is the only thing between The Nerve plus The Fleet Will Return and an unwinnable sky. **Restore it as a hard ceiling of 9 on the opening wall**, taking the max of the Fear and Return terms.
- **F clamp at 4.** It must stay. It is an enemy-side cap, which is allowed.

## 5. Questions for Question Time

1. **`autofleet`:** `destroyEnemy` records no killer. Which bill pays for attribution, and where is it stamped? I suggest `FIRE_CTX` and one integer per hit.
2. **`pacing`:** does `invTime`, and so `t`, reset each cycle? If not, what is a Falcon's hit count against a leviathan at minute 25?
3. **`pacing` with `lore`:** Resolve falls `v/fleetMax` per kill. Show me the kill count that reaches Rattled. Is there a ×100 missing?
4. **`pacing`:** the HUD shows exact `T-Ns` today. Will the One Forecast hide it, and if so, why is that not a denial?
5. **`autofleet`:** will you sign that Fear never touches per-unit damage, and that the Mirror's damage is flat?
6. **`game-feel`:** is Warhead Cam default "Always" or "First 3"? How does a player skip it across 46 strikes?
