# ENERGY — Round 3 (Coalitions)

## 1. Answers

**Physics, "THE THERMAL LEDGER" — ACCEPT with one amendment.** It is our Thermal Field with Physics' blackout and ballistic coefficient added, and both are improvements.
- Amendment: heat on friendly Shapeshifters comes from enemy-source damage only. The shared module has one `heatApply(t, amount, src)`, and `src` is checked, so friendly guns never heat friendly units.
- The ledger's "above 90, speed -30%" stays as written (a speed penalty, not a damage cut).
- Heat is added to the Furnace and Lumen deposits (ENERGY-H, PSI-3) in the same module.
- Research: one node, `thermaldoctrine` (Physics' name wins over our `thermalload`). `directedenergy` hosts the deposit.

**Psi, "THE HEAT BOOK" — AMEND.** We co-sign the shared vocabulary (bands, tint, `heatBonus()`), with two changes:
- The Shapeshifter "slump" is speed -30% only, as in Physics' version. Delete "damage x0.5 and +40% damage taken". Halving a player toy's output and softening it is a nerf (Article III). The scarier enemy is the lever: enemy beams add the heat, the penalty is mobility.
- Heat bands per ENERGY-1/Physics apply to Shapeshifters unchanged. The 0.02/0.05/0.35 per-HP deposit table stays, since only enemy-source hits count.
- This motion and Physics' are one bill (see Section 3).

**Railgun, "THE POWER BANK" — ACCEPT with one amendment.** It is our Grid with Railgun's overdrive on top.
- Amendment: Helios Arrays (SPACE-3) also feed `G.power` at +6 MW each, and the Bank's pool fills from Power Plants first.
- We confirm the rules as written: beam shortfall bills IC (0.6 IC/s per MW, never throttled), rails fire at baseline when the pool is dry, and the 2000 MJ pool and 20 MJ overdrive shot stand.
- Research: `pulsedpower` (branch energy, pre `directedenergy`, ~150 x 1.6, not `dang`) owns overdrive and the Foundry. `superconductor` halves draw and wear.

**Kinetic-Arms, "THE ONE LOCKER" — AMEND.** It names us for the energy-branch research hook and for AP rounds against hot targets.
- Amendment: the Smart Munitions armour term (x0.35 on AP-sabot) multiplies with the heat armour cut, then the cut is capped at 50% of base armour so stacking cannot reach zero. This is a multiplier cap on armour reduction, not a player cap, and it keeps stacked heat and sabot from making armour irrelevant (it has no effect on the player's toys).
- AP and sabot rounds get the 50% heat armour cut (others 40%), so picking AP against a hot target is a real choice.
- No new energy node. The `munitions` draw stands as written.

## 2. Joins

- **Missiles, "DARK SKY" (to physics, space-fleet).** The `hemp` node is in our branch, so we host it. Its thermal burst deposits `e.heat` through `heatApply`, and a high burst blinds enemy thermal sensors for 10 s.
- **Physics, "THIN AIR" and Science, "THE BREATHING SKY".** An ionospheric heater is a legitimate heat source. Heated space targets that descend into the mesosphere land in range of rails and lasers, so the heater feeds `e.heat` for the column.
- **Market, "THE DEBRIS LEDGER".** Beams ignore debris zones (vacuum and clear columns are laser-friendly). We co-sign on that basis.
- **Market and New-Player forecast.** No stake. We back whichever version keeps heat readouts out of the forecast, since the heat band is an in-combat read.

## 3. Competing Motions

- **Heat.** Back **Physics, "THE THERMAL LEDGER"** as the base, with the Psi "HEAT BOOK" as its friendly-units clause and our own Motion 1 retired into it. Physics is the owner of the cooling and blackout physics, so the sim has one author. Our deposit table and band table remain as the numbers.
- **Capacitor and power.** Back **Railgun, "THE POWER BANK"** (with the Helios amendment). It is the only version that carries both rails and beams in one pool.
- **Debris.** Back **Market, "THE DEBRIS LEDGER"** as the framework, with Missiles' sequence as its first consumer.
- **Phased Arrays.** Our own Motion 3 goes to Science. It rides the Power Bank's pool and the Thermal Ledger's field, so it follows whichever versions of those pass.

## 4. Closing Statement

*(The coat has been replaced. It is slightly less scorched.)*

Mr Speaker, we came in with three thermometers and a fire. We leave with one thermometer, one power pool, and a fire that is now in the Capacitor Bank, as designed. We thank Physics for the plasma sheath, Railgun for the capacitors, and Psi for the noble Shapeshifter who will not melt under the Speaker's gaze, because we took the melting out. To the House: heat is a landscape, power is a bill, and every kinetic gun on this floor is about to get a pay rise from a laser. Vote accordingly. Stand back from the lab.
