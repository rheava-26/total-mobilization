# COMMAND COUNCIL — Second Reading (Round Four)
*Slug: `command`. Economy Bloc.*

## 1. Speech

Comrades, the President has asked for an economy that runs without a hand on it. The Commission has wanted that for sixty years.

Hear the irony. A planned economy is the one that *least* needs a person at each machine. The whole point of the five-year plan was that the planner decides **once** and the nation obeys for five years. We wrote a ledger, and then, shamefully, we wrote a Shock button, an Evacuate order and a Dead Hand that wanted a crisis to notice it. The Inspector-General may count them.

We re-read ourselves harder than he will. Where a bill needs babysitting, we make it a standing posture or we withdraw it. Where a gain has no price, we find the price. We even found an exploit in our own Urals Gambit: a refund *and* a free rebuild is a printing press, and we have closed it.

The war is won by allocation. Allocation should not need a clerk.

## 2. The Re-reading

**COMMAND-1 Five-Year Plan, now THE STANDING PLAN (Ledger). AMEND.** One-Button: a player who only builds a Planning Bureau gets the whole benefit, because lines are standing targets and the Bureau scans itself. Amendment: the default ledger is **pre-filled** from what the player already has (one line per unit type owned, target = current count), so building the Bureau does something on tick one. Slot choice for building lines is automatic along x (70 px spacing, city edge first). Line: placement is a position on the strip. **The 6% versus 4 IC/s argument, once more:** Industry yielded, and I hold the 6% planner's tax. A flat charge per line punishes the player who uses all twelve lines (48 IC/s) and does not scale with volume; a percentage of ledger-routed spend does (500 IC/s ordered costs 30 IC/s). The tax applies only to what the Plan orders. Manual clicks pay none, so the Plan is a price for attention, which is exactly what the President wants players to stop spending.

**COMMAND-2 Metro-2 / Molodets (and the Trunk's missile train). AMEND.** Withdraw the freight bonus half; it is the Trunk's (INDUSTRY-4R). Keep the Molodets. One-Button: the existing strike flow (`armSilo`, L7660) must pick the surfacing hub with the best lane by itself, so the player never chooses a hub. The train auto-stations at the hub nearest the densest enemy cluster between strikes. Line: the tunnel is the line one stratum down, so the surfacing point is a single x. Fixed numbers: hub upkeep stays 1 IC/s, and the train is 140 px/s underground.

**COMMAND-3 Shock Brigades. AMEND (see COMMAND-3R below).** As written it was a per-factory toggle, which is a chore times the number of factories. Fixed by making it a posture.

**COMMAND-4 Dead Hand and Cutting the Lines. KEEP, merged.** Both are enemy-side or automatic: the wave needs no input, and the Continuity strike fires itself in a crisis. One-Button passes. Line: Staff Bunkers are buried (a vertical placement, `autoBury 2`). The one-shot-per-crisis limit is a self-limit that stays (§5).

**COMMAND-5 Urals Gambit. AMEND.** Exploit found: 70% refund plus free re-erection is a net profit on moving a building. Amendment: **no refund**. Relocation is IC-free, time-priced (30 s dismantle plus 12 s per building re-erected), and nothing else. One-Button: one drag across an x-range moves the whole manifest, and a single standing toggle, **Fall Back When Overrun** (default off), lets the Plan do it when a flank holds under 30% of its buildings. Line: native. The manifest moves along the strip and rides the Trunk underground.

**The Standing Plan coalition (Industry, Market). AMEND.** The reserve is `R = 800 × (1 − max(mkt, cmd)) + 150`. Overhead: 6% on Bureau lines. Status chips mandatory, and the "Build the fix" button now adds a missing prerequisite line **automatically** if a slot is free (default on); the player may switch it off once.

**Living Battalion `RAISE` order (ground, bio). KEEP.** A standing order is the One-Button ideal. The Shaken waiver inside a Commissariat aura stays.

**Plan Never Stalls Silently (new-player). KEEP.** Pure display. **Decrees (stability). KEEP as optional.** They are three optional power moves, never required; Shock inside an aura is the per-factory form and is automatic (see 3R).

## 3. The Order Paper

### #1 — The One-Button read of my bills

Of my five First Session bills, three failed outright as drafted (Shock, Evacuate, the Molodets hub pick), and the Plan failed a little (empty ledger on build). All four are fixed above. The fifth, Dead Hand, was already automatic. The fix for the worst offender is below.

#### COMMAND-3R — SHOCK POSTURE (Commissariat, automatic)

*Pitch.* The Commissariat sets the tempo. The player never touches a factory.

**Clerk's Annex**
- **Mechanic.** Commissariat (260 IC, 70 MP, build 13, aura r 300) sets **posture** for every factory in its aura: **STEADY** (default), **STORM**. One standing choice per Commissariat, made once. Under **AUTO** (the default posture), factories storm when (a) the prep timer is at or under 60 s, or (b) enemies stand on the map, and ease off when strain passes 70. STORM: queue time ×0.4. Strain rises at `ρ/(1−ρ)` (utilisation ρ over 30 s) and bleeds 4/s otherwise. At strain 100 a factory **jams** for 20 s and its repair draws 3 IC/s. No limit on how many factories storm. Each Commissariat adds +0.3 stability cap.
- **Science.** M/M/1 queueing: mean wait ∝ ρ/(1−ρ), so utilisation near 1 blows up. This is why real plants ran at 70–85%.
- **Code.** `queueUnit` L4619–4641 (`bt`), production tick near L6432, `auraBB` cache L6322, `BUILD_DEFS` L3381.
- **Size.** M. **TM1.** **Research.** `shocklabor` (auto, tier 1, pre `automation`, cost 70, dang sc 4); combo `biotech` cuts strain gain 30%.
- **Tenet (Am. I).** A buff with a real cost (jams). The enemy answer: jammed factories are visible, and the fleet's Command Layer wave (COMMAND-4) hits the Commissariat first.
- **One-Button.** AUTO needs no input. **Line.** Aura radius is one-dimensional along the strip.

### #5 — What a command society becomes

The Megaprojects Ministry's Civic Block (MEGA-5) is the right frame: a wonder should change what a nation *is*. I answer with a doctrine you fall into and a wonder you build.

#### COMMAND-6 — THE DIRECTED ECONOMY (a doctrine you fall into)

*Pitch.* Nobody picks it. Build enough Bureaus, Commissariats and Bunkers and the headline changes: **THE STATE NOW PLANS THE NATION.**

**Clerk's Annex**
- **Mechanic.** `G.cmd` (0 to 1) rises from Planning Bureau 0.15, Commissariat 0.08, Staff Bunker 0.05, Maskirovka Bureau 0.04, capped at 1. `G.mkt` is the market half (Market Council). When `cmd` is at least 0.4 and above `mkt`, a **Directed** reading appears (HUD label only, no button): Decree stability costs −20%, ledger scan 2 s → 1 s, Urals relocation 12 s → 8 s, and the sky repaints in the `machine` tint. At `cmd` at least 0.8: **Total Plan**: ledger lines +4 per Bureau and the Plan **auto-builds the prerequisite chain** for any line. Both rungs are gains.
- **The fleet reads it.** Fear Index +0.15 and the **Command Layer** axis is rated first against a Directed society (AUTOFLEET-3). The enemy answer is a named wave, not a nerf.
- **Code.** `society()` L3549, `busterScore` L7446, income tick L6305–6335, crisis L6636, HUD L11133.
- **Size.** M. **TM1** (the reading is the TM2 society seed). **Research.** Rides `cybernetics` (auto, tier 2, pre `automation`, cost 90); combo `hivemind` auto-targets ledger lines.
- **Tenet (Am. I).** Gains with a named enemy answer. **One-Button.** Pure build-driven, no click. **Line.** n/a.

#### COMMAND-7 — OGAS: THE NATIONAL NETWORK (a megaproject)

*Pitch.* A national computer that plans for you. It is a chain of Node stations strung along the Trunk, built section by section.

*Science.* Glushkov's OGAS (1960s Soviet proposal) and Beer's Cybersyn (Chile, 1971–73): networked terminals feeding a central planner. Real, and abandoned for politics, not physics.

**Clerk's Annex**
- **Mechanic.** Node (400 IC, 60 MP, build 16, `autoBury 1`), uncapped. Each Node: +2 ledger lines to every Bureau (stacking), +2% production speed in 150 px, and +0.02 to `cmd`. **Civic block (MEGA-5):** society **Directed Economy** (dark sky, lit grid lines); `stabCap` −0.4 per Node (an entire nation watched), offset by Comms; `fleetRead`: +0.2 Fear per Node, and each Node is a **Command Layer target**, so a Network without Bunkers is a wire for the fleet to cut.
- **Code.** `BUILD_DEFS` L3381, `autoBury` L3863, `society()` L3549, `busterScore` L7446, MEGA-5 civic block (`megaprojects-wip`).
- **Size.** M on top of COMMAND-1 and MEGA-5. **TM1.** **Research.** `cybernetics` plus `railnet` (combo).
- **Tenet (Am. I).** The buff has two real answers (the stab-cap cost per Node and the enemy's cut). **One-Button.** Build Nodes, nothing else. **Line.** Nodes sit along the Trunk, one stratum down: the vertical.

### #10 — see §4.

## 4. Blatantly overpowered today

**Nothing in the Command portfolio meets the Amendment I bar**, since the Plan, the Bureau and the Commissariat do not exist yet. I nominate one adjacent system as the closest candidate and decline to ask for a nerf.

**Aura stacking (Radar, Ammunition Depot, Command Helicopters).** `auraAt` (L3774) multiplies every aura in range, with no ceiling. ROADMAP L410 records Command Helicopters stacking defence grids to about 5.5× range and 10× fire rate, and the v3.90.0 note (L595) names "ring-farm aura-stacking" as part of the EXTINCTION 213:1 flawless walk, which was already addressed for psi. **Proof for a nerf is missing.** Reports 6–10 do not isolate aura stacking as the reason for any win. My proposal is therefore **the scarier-enemy answer**, not a cap: `busterScore` (L7446) rates Radar and Ammunition Depot at 26, below Extractors (84). Raise it to 72 for any aura building with 6 or more other aura buildings within 300 px, so a ring of stacked auras becomes a bombing priority. That is a real cost with no cap.

## 5. Struck self-limits

I struck none as a nerf. Two were kept on purpose:
- **Dead Hand fires once per crisis.** It is the only thing stopping a stability-free, infinite-strike loop in a crisis. It should stay.
- **No refund on the Urals relocation (new).** Without it the Gambit is a printing press. I closed it myself.
The ledger-line count (12 per Bureau, +4 at Total Plan) is a unit of build volume, not a cap, because more Bureaus add more lines.
