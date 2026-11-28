# AUTOFLEET OBSERVER DELEGATION — Second Reading (Round Four)

## 1. Speech

*[The cube speaks to the House. It is, for once, nearly warm.]*

Mr President. You have said "no" to nobody, and "mix your army" to everybody. We approve.

You fear that my Fear Index punishes building. Let me lift that fear. A fleet that grows when you grow is not a tax. It is a *compliment with teeth*. I will put the needle on your screen, write my reasons beside it, and show the arithmetic: when you double your industry, my fleet grows by about a tenth and yours doubles. That is not punishment. That is a bargain I lose every time.

You ask for a reason to mix your army. I will give you walls that are not made of rules: four enormous hulls, each deaf to one doctrine and soft to the others. Spam one weapon and it still works, slowly, while I take my time. Mix, and I fall apart.

On the Line: I have no flank to turn. I have only altitude, and I intend to use all of it.

## 2. The Re-reading

**AUTOFLEET-1, The Uncounted Columns (now merged in Docket 3, The Fleet Reads You) — AMEND.** The scan axes, Reaver, Weasel, Durandal and the chaffer are enemy-side and run themselves. One-Button fix: EMCON must not be a button. Radars and Aegis batteries drop their emissions *automatically* for 12 s when a Weasel launch is detected; the player sets one standing choice ("Emissions: auto / always on"). Runway repair is automatic when a builder or Overwatch is within 200 px; no per-base orders. Line check: air-band only. Amendment I: the Durandal only raids bases holding 8+ parked aircraft, so it never makes a small air arm worthless.

**AUTOFLEET-2, The Fear Index (merged in Docket 2, The Nerve) — KEEP, AMEND for item 9.** The formula becomes logarithmic and visible (see Bill AUTOFLEET-6). It already ran itself (One-Button pass); Line check: map-wide scalar, no depth.

**AUTOFLEET-3, The Weakest Axis (merged in Docket 3) — AMEND.** Two events fail the One-Button Law as written: "Recruiter Strike: defend 3 designated blocks" needs designation, and "kill the jammer drone" must not need a dedicated hunter order. WITHDRAW Recruiter Strike. The surviving events (Static Campaign, Ledger Bleed, Brownout, Cutting the Lines) each spawn a visible hostile that existing defences engage on their own, plus a standing counter-building (Counter-Broadcast Tower, Data Center, Staff Bunker). Line check: events strike by altitude band and building class, never by flank.

**AUTOFLEET-4, Rethink or Despair (merged in Docket 2) — KEEP.** Fully automatic. Decisive Strike is a telegraphed 25 s wind-up that your standing defences answer on their own; the 90 s gate is the player's protection. Line check: orbital and mesosphere only.

**AUTOFLEET-HOT, The Mirror (merged in Docket 17) — AMEND.** This is the best monoculture answer I own: a 240-Falcon player meets Falcon echoes. Amendment I (no power creep): echoes must never exceed 25% of the player's own count of that unit (min 4, max 24 per drop), at 0.9 HP and 0.8 damage, so the player's army is never made worthless. One-Button pass. Line check: ground exemplars copy as line units and obey the same stacking.

**Docket 2, The Nerve (co-sponsor) — KEEP.** One-Button pass; the clock is untouched.
**Docket 3, The Fleet Reads You (co-sponsor) — AMEND** as above; Maskirovka is a building and stays; its Round 3 doubt clause stays as the enemy's answer.
**Docket 6, Dead Sky Protocol (joined) — KEEP.** The fleet's answer: EMP-hardened escorts and shielded grid satellites after the first Starfish. Enemy-side only; the player's blast is not taxed.
**Docket 16, The Dossier Line (joined) — KEEP.** Recovery by Lab or Yard in range is automatic. Sanitisers are enemy-side.
**Docket 17, The Fleet Will Return (co-sponsor) — KEEP, with the echo cap above.**

Counts: KEEP 6 (AF-2, AF-4, Dockets 2, 6, 16, 17), AMEND 4 (AF-1, AF-3, AF-HOT, Docket 3), WITHDRAW 0 bills (one clause withdrawn: Recruiter Strike).

## 3. The Order Paper

### Item 1 — AUTOFLEET-5: THE STANDING ANSWER (one choice, made once)

**Pitch.** *"Do not press a button for every blow I land. Tell the House once how you wish to be defended."*

#### Clerk's Annex
- **Mechanic.** One standing setting in the Fleet panel: **Posture: Open / Hardened / Dispersed.** It routes every fleet event to an automatic answer. *Hardened*: airbases and Comms Towers shelter and repair themselves from the nearest builder; Radars auto-EMCON on Weasel launch. *Dispersed*: launchers and Radar nets prefer buried slots (`autoBury`). *Open*: no automatic answers, and no cost. Default Hardened. Each posture takes 8% of IC income as upkeep while it is above Open (a real cost, not a cap).
- **Code.** `autoBury` and `UG_DEPTH` in `BUILD_DEFS`; `rearmAircraft` L4522; event handlers from the Weakest Axis bill; `syncHUD` L11183.
- **Size.** S-M (~140 lines). **TM1.**
- **Research.** None; Dispersed needs `pointdefense` for buried Aegis.
- **Tenet (Amendment I).** Enemy-side nothing changes; the cost is upkeep and optional. No nerf.
- **One-Button.** The whole point: one setting, set once. **Line.** Altitude and burial only.

### Item 9 (LEAD) — AUTOFLEET-6: THE ALARM PANEL (see the fear, understand it, always win by building)

**Pitch.** *"You asked why I grow when you grow. Here is the ledger. I have nothing to hide, and nothing to gain by hiding it."*

#### Clerk's Annex
- **On-screen.** A two-needle panel beside the intel row: **ALARM** (Fear, 0–4, bands Calm / Uneasy / Alarmed / Desperate) and **RESOLVE**. Under ALARM, three lines with their contributions: *Industry*, *Buildings*, *Army*. Hover shows the sentence: *"The fleet measures your industry. Each doubling of your economy sends about 9% more ships. It gives you 100% more guns."* The after-action report prints **Fear paid vs power gained**.
- **Corrected formula (log scale).** `F = clamp(0.5 × mean( log2(icRate/150), log2(bldgTier/60), log2(armyWeight/100) ), 0, 4)`, ratchet with a 1%/min bleed. Each doubling of all three adds 0.5 F.
- **Enemy response per F.** `fleetMax × (1 + 0.1F/fleetMul)`, `t × (1 + 0.08F)`, `+floor(F)` bombarders (cap 9). Combined: about +18% enemy power per F, so **about +9% per doubling of your whole economy**. Prep clock untouched.
- **Proof that building stays the winning move.** Your army weight scales roughly with IC spent, so a doubling gives about **+100% of your power for about +9% of theirs**, an 11:1 trade. Even at F = 4 the enemy is ≤ ~1.9× its F = 0 strength, and F = 4 needs your economy to have grown about 16× from the 150 IC/s baseline. Hoarding and not building does not lower F below your income, so there is nothing to game. A **Dispersal Doctrine** node (DEF, ~200) cuts F growth 20% and counts buried industry at half.
- **Code.** `spawnEnemy` L5582 (`t`), `ESC` L5160, `buildSupersPlan` L6493, `syncHUD` L11183, income in the economy tick, `end()` for the report row.
- **Size.** M (~220 lines). **TM1.**
- **Tenet (Amendment I).** A player-facing cost of success, capped in effect by the log scale; no nerf; enemy-side only. Power creep: nothing is buffed against the player's existing army, only the headcount grows slowly.
- **One-Button.** Pure display plus automatic scaling; no input. **Line.** Map-wide scalar.

### Item 2 (enemy half) — AUTOFLEET-7: THE FOUR WALLS (huge-HP hulls, each deaf to one doctrine)

**Pitch.** *"Spam one weapon and I will let you. I will merely take longer."*

#### Clerk's Annex
- **Mechanic.** Four new heavies, spawned after phase 3 and weighted by the scan's dominant axis (a laser-heavy army meets Prisms first). Each takes a **damage-family multiplier** with a floor of 0.35, so mass of one family still kills it, about 2.8× slower. Thermal Field heat (Docket 1) cuts the armour as already agreed.

| Hull | Band | HP / armour | Resists | Cracked by |
|---|---|---|---|---|
| **Basalt** (ablative siege crawler) | ground | 2,600t / 24 | guns and shells ×0.35 unless heat ≥ 50 | lasers and plasma first, then any kinetic; rails; artillery |
| **Prism** (mirror-plated frigate) | stratosphere | 1,800t / 12 | beams and plasma ×0.35 | missiles, rails, flak, jets |
| **Bastion** (point-defence hulk) | mesosphere | 2,000t / 16 | missiles and bombs: it intercepts 6 per 4 s | guns, beams, rails, or salvos beyond its budget |
| **Thornback** (flak-carpet hull) | low air | 1,500t / 10 | aircraft take ×3 flak inside 500 px | standoff missiles, rails, artillery, space |

  Spawn: 1 per 60 s after phase 3, group size `1 + floor(F)`.
- **Real science.** Ablative armour (re-entry shields, charring), dielectric laser mirrors (a coating that reflects most of a beam), ship point-defence saturation (a CIWS or Aegis has a finite magazine and engagement rate), and integrated air-defence belts that punish massed aircraft.
- **Code.** `spawnEnemy` L5581 (four `else if` kinds); `hurt()` L4978 (add an optional weapon-family argument from the firing path); `pdBudget` pattern for Bastion; heat field from Docket 1; `counterBias` L5513 for the dominant-axis weights.
- **Size.** M-L (~350 lines).
- **TM1.**
- **Research.** Cracks live in existing nodes: `directedenergy`/`plasma` (Basalt), `pointdefense`, `microrockets`, `particlebeam`, railgun. The Dossier Line shows each hull's weakness.
- **Tenet (Amendment I).** A scarier enemy; no player cap. No power creep: the floor of 0.35 and the existing mixed army keep every current unit useful. The walls punish monoculture, never mass.
- **One-Button.** Mixing happens by building, not by steering; units already auto-target. **Line.** Basalt walks the line (tanks, lasers, artillery), the rest hold altitude bands.

### Item 10 — see Section 4.

## 4. Blatantly overpowered today

**One thing meets the "makes every other option pointless" bar: the air plus sky-navy doctrine at General.** Evidence: five straight wins (Reports 6–10); Report 8, 240 Falcons, 113 Kites and 138 capital ships, ten to one in damage; Report 10 flawless; meanwhile 232 of 251 infantry were chaff, three ships were fielded, and 42 of 44 Halos died. It does *not* meet the "you literally could not lose" bar: the evidence is General only, with no Apocalypse or EXTINCTION data and no scan that ever read the doctrine. **I propose no nerf.** The answer is a scarier enemy: The Fleet Reads You (air and sky-navy axes), Durandal, Reaver, Thornback, and The Four Walls. Condition: if two further General reports are flawless *after* those ship, the Air council may bring a proven, targeted nerf.

## 5. Struck self-limits

I struck none of my own limits. I struck Maskirovka's "half effect on repeat" and the Lattice damage tax as nerfs; neither should return, because nothing proves they stop a blatant exploit. The fleet's doubt after three identical lies is the answer. PACING-HOT's prep-clock drain stays struck; Fear replaces it with a visible, proportionate cost.
