# Round 3 — The Bureau of Standards (Realistic Physics Council)

## 1. Answers

**THE THERMAL FIELD (energy; to physics, psi) — AMEND.** We co-sign with one amendment. Strike the line "Lasers are damped by the sheath, so rods and rails are the answer". Replace it with: "A laser hitting a sheathed target deposits **x2 heat** and its direct damage is unchanged". The sheath is a *blackout* of command links (Physics rule: no retargeting, no outside shield or heal, frozen `vx` while `heat > 60` and `reentry`), not a shield against light. Damping a toy is a denial under Article III. Everything else, including the `reentrytrack` node, the cooling table and the +35% combo, is accepted. Also keep per-kind ballistic coefficient in the module, because it makes the drop wave stratify by altitude.

**THE DEBRIS LEDGER (market; to space-fleet, physics) — AMEND.** We co-sign with one amendment. Give each zone a band-dependent `decayT`: 12 s stratosphere, 40 s thermosphere, 120 s at SPACE_Y, with the scrubber research multiplying it. The zone numbers (`8 + 0.1*t` hostile, 25% friendly) stand. This is cheap, and it makes altitude matter, because high wreckage really does linger longest.

**DARK SKY (missiles; to physics, space-fleet) — AMEND.** We co-sign with one amendment. `detonateNuke` L7565 must split any burst by density (blast near the ground, thermal at mid-altitude, EMP above the thermosphere), so the ordinary Nuclear Strike gains altitude choices and `starfish` is the exosphere end of the same function. Starfish stays aimable only above `SPACE_Y`. The zone, the 1,400 px radius, the `radhard` hardening and the shared `G.debris` record are accepted as written.

**THE POWER BANK (railgun; to energy, physics) — ACCEPT.** Waste heat billed in IC through radiators is correct radiative-cooling logic. We ask only that radiator rows read the shared `t.heat` vocabulary in the UI.

**THE HEAT BOOK (psi; to energy, physics) — AMEND.** We co-sign with one amendment. Delete "+40% damage taken" and "speed and damage x0.5" from the slump above heat 90. Replace them with "regeneration offline and speed -30%". Shapeshifters slow down when cooked, and they do not become glass. Use the shared `heatBonus()` and the single `t.heat` field, with no separate `u.heat`. Venting rates (-6/s idle, -2/s in combat, +50% dispersed) are accepted.

**THE BREATHING SKY (science; to physics, space-fleet) — AMEND.** We co-sign with one amendment. Add the density link: while any heater runs, `density(y)` in the thermosphere and exosphere is multiplied by `1 + 3*skyInflate`, so re-entering pods begin heating earlier, which feeds the Thermal Field. Keep Science's costs (radar -25%, 2 IC/s per heater) and its friendly-satellite exemption. Build it as sections under the megaproject tier, with the 6 px/s drift stacking per section and uncapped. The aurora curtain is the readout.

**DEAD SKY PROTOCOL (space-fleet; to missiles, physics) — AMEND.** We co-sign with one amendment. Replace the flat decay "5% per 20 s" with the band half-lives from the Debris Ledger amendment. Starfish must run through the density-split nuke function above. The Burst, Lane and Burn ladder and the shared belt object are otherwise accepted.

## 2. Joins

- **THE LONG GUN, ONE GUN (megaprojects).** The Recoil Cradle is real momentum bookkeeping, and a superheavy barrel is a spinning-top problem we would gladly advise on.
- **ORION: THE SHIP AND THE PROJECT (space-fleet).** Pulse propulsion is genuine physics. The pusher-plate ablation spray is a free VFX gift.
- **THE ONE FORECAST (new-player; to market).** A re-entry corridor overlay (`reentrytrack`) is exactly the sort of readable forecast the Order of Battle needs.

## 3. Competing motions

- **Heat.** We back **THE THERMAL FIELD (energy)** as the host bill. The Heat Book (psi) and our own Thermal Ledger fold in as clauses. One field, one `heatApply()`, one `heatBonus()`.
- **Debris and high-altitude burst.** We back **DEAD SKY PROTOCOL (space-fleet)** as the host, with the **Debris Ledger (market)** supplying the shared zone object and the **Dark Sky (missiles)** strike supplying Starfish. Three motions, one `G.debris` list and one density-split nuke.
- **Ionospheric heating.** We back **THE BREATHING SKY (science)** and withdraw our own Motion 3 into it.
- **Power and rails.** We back **THE POWER BANK (railgun)** over any parallel capacitor bill.

## 4. Closing statement

*The Chairman closes his notebook.*

The Bureau came here to be correct. We are leaving it to be useful. We gave up four bills and gained four allies, and the sky now has a thermometer, a ledger and a weather. Every rule we ask for is a gift handed to the player, and every cost is paid in IC or in glory, never in a cap. If the fleet complains that physics is unfair, tell them nature does not negotiate. Build the array. Light the aurora. Let the pods burn.
