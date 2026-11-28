# Round 3 — The Bureau of Spectacle (`game-feel`)

## 1. Answers

Only one motion in the register names this council (found by searching `game-feel`, `GAME-FEEL`, `Game-Feel`, "Audio" and "Visuals").

- **THE FLEET WILL RETURN (pacing, Motion 3, to AUTOFLEET and GAME-FEEL) — AMEND.** The Bureau co-signs with three exact amendments:
  1. The Director gets two tiers, not one. On every Calculated Retreat (`startWithdraw`, L5990) it plays a short beat: `timeScale` 0.35 for 1.2 s, a one-note sting on the music bus, no letterbox. On the *final* withdrawal it plays the full sequence the motion describes (letterbox and caption), so the first retreat does not spend the final one's impact.
  2. All time dilation goes through one `G.timeScale` multiplier on the `updateGame(SIM_STEP * 0.45 * G.speed)` call (L10205). It multiplies the player's chosen `G.speed` and never overrides it. `reducedMotion` forces 1.0 and skips the letterbox.
  3. The Interbellum return arc and the arrival of the second fleet get a sound cue (a low rising tone while the HUD ETA ticks down, the existing `SFX.charge` pattern at L1123), because the dread of waiting is the best thing the original had. This stays in the Bureau's lane and adds no mechanic.
  - Build size for the Bureau's share: S (about 60 lines on top of GAME-FEEL-3). Tenet: nothing is capped or nerfed; the sim step is uniform, so the enemy loses no time.

## 2. Joins

- **THE LONG GUN COALITION (railgun, Motion 1) and ONE LONG GUN (kinetic-arms, Motion 3).** Co-sign: the gun is the best candidate for the Long Shot cinematic (the Bureau's own Motion A to MEGA and RAILGUN).
- **THE ONE FORECAST (pacing, Motion 2).** Co-sign. The five prep beats need lights and stings, and the Bureau writes the stings (the Mix Bus voice budget protects the mix).
- **THE THERMAL LEDGER (physics) and THE THERMAL FIELD (energy).** Co-sign on one condition: heat shows as a cached colour-ramp bitmap per unit, with no per-frame vector glow, so the lag stays gone.

## 3. Competing motions

- **Long gun.** I back **THE LONG GUN COALITION (railgun)** as the base. It already holds the mechanic and the sections, and the Bureau's Long Shot cinematic attaches to it as a single hook. Please merge kinetic-arms' ONE LONG GUN and megaprojects' THE LONG GUN, ONE GUN into it.
- **Forecast.** I back **THE ONE FORECAST (pacing, Motion 2)**. It is the version that names the prep beats the Bureau scores, and it is information only. THE UNIFIED FORECAST (science) should fold in as the `n_obs` source.
- **Rail networks.** The Bureau has no stake, but would ask the winning version to give train departures a horn and a rumble through the Mix Bus. I do not rank the four.
- **Heat.** I back **THE THERMAL LEDGER (physics)**, the one version that carries a ledger the player can read, and the cached-ramp rule above.

## 4. Closing statement

*Speaker, the House has built a fleet that fears, a fleet that returns, a gun that takes twenty seconds to speak, and a forecast that arrives in five beats. Every one of them is a moment. A moment needs a sound, a pause and a light. The Bureau asks for no new toys and takes none away; it asks only that when the Autofleet finally turns tail, the player hears it go. We will not make the war louder. We will make it legible to the heart.*
