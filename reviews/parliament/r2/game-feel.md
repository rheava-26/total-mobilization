# Round 2 — The Bureau of Spectacle (`game-feel`)

## 1. Floor Speech

*Speaker, honourable members. The Bureau thanks the House for its many mechanics. We note, with tenderness, that not one of them has a sound.*

Kinetic-arms, I kiss your forehead: KINETIC-3 spools a Hailstorm and promises a pitch rise. Pitch rise through *what*? Through a single blip and an unbuffered noise call. Without my Mix Bus your spool is a number on a spreadsheet. New-player, your Because Chip and Order of Battle are fine instruments, but the player *feels* before they read. PHYSICS-1 gives us a plasma blackout and a "radio-silence tick". I will take that tick and build a world around it.

To the Ministry of Ledgers, which will say "cosmetics": a thousand cached sprites cost nothing, and they are noise without consequence. Thirty-one new mechanics tabled this session, and the only thing a player will remember is the one second the world went quiet. Pacing, you promise five prep beats and cede the sound. I accept. Market, do not price my silence.

I expect the attack that hit-stop is a nerf. It is not. It is time dilation applied to the *camera*; the sim step stays uniform and the enemy loses no time. Shake that signals is not shake that decorates. Cinematics are a toggle, and a toggle is not a cap.

*The Bureau yields, and turns it up.*

## 2. Positions

| Bill | Position | Reason / amendment |
|---|---|---|
| KINETIC-3 Spin-Up | AMEND | Route its audio pitch ramp and recoil through GAME-FEEL-1 `sfxAt`; spool pitch must be capped at one voice per 8 guns, as the Roar replaces individual shots at density. |
| PHYSICS-1 Peak Heating / blackout | SUPPORT | The blackout is a sound and picture moment; give its tick a bus-mute (the Hush) rather than a one-off blip. |
| PHYSICS-3 Burst Altitude | SUPPORT | Burst height changes the sound: thin air means a thin bang, which matches the Mix Bus altitude filter. |
| PHYSICS-4 Kessler Shield | AMEND | Debris-shield hits must emit a distinct crackle on the shared `power` scale, and not add per-fragment audio voices. |
| NEW-PLAYER-4 Because Chip | SUPPORT | Reading a cause is feel. Pair every chip with the trauma shake level. |
| NEW-PLAYER-5 Post-Mortem | SUPPORT | Hand it the Director's recorded moments as replay markers. |
| LORE-5 Wire and Chronicle | SUPPORT | The chronicle is the natural home for the Money Shot moments. |
| PACING-2 Long Dark | SUPPORT | Its beats need a sky light and a sting; the Bureau writes the stings. |
| PACING-1 Second Siege | SUPPORT | A withdrawal and return is exactly the Director's best two beats. |
| MISSILES-3 Hypersonic Glide | AMEND | Add a Warhead Cam variant: a skip-glide trail with a visible sonic-boom wake line. |
| MISSILES-5 Warhead Stockpile | SUPPORT | A magazine count gives each launch a weight that the cam can use. |
| ENERGY-4 Reentry Sheath | SUPPORT | Plasma sheath is the cam's best visual and the cleanest radio-blackout hush. |
| ENERGY-1 / PHYSICS-2 / PSI-2 heat | AMEND | Heat must show on the *cached sprite* as a colour-ramp overlay (one extra cached bitmap per unit), never as per-frame vector glow. |
| STABILITY-1 Casualty Ledger | SUPPORT | A running ledger is a score the Roar can read for intensity. |
| MEGA-2 / RAILGUN-2 Long Gun | SUPPORT | A 20-second shot interval is a ritual: trauma 1.0, the Hush, and a cam. Best cinematic in the session. |
| PSI-4 The Hush | AMEND | Rename it. The Bureau's Warhead Cam silence is also called the Hush; call the psi bill "The Quiet". |
| AUTOFLEET-4 Rethink or Despair | SUPPORT | A fleet changing its mind is the best withdrawal theatre available. |
| MARKET-6 Privatise the Sky | OPPOSE | A "flash crash" that reorders prices every second spams the toast channel; only support it if prices surface in the ledger, not the banner. |

## 3. Ballot (10 votes)

| # | Bill | Votes | Why |
|---|---|---|---|
| 1 | MEGA-2: The Long Gun | 2 | A gun the whole map waits on; pure spectacle with real mechanics. |
| 2 | NEW-PLAYER-4: The Because Chip | 2 | Readability is half of feel; cheap. |
| 3 | PACING-1: The Second Siege | 2 | A fleet that leaves and returns creates the beats the whole battle needs. |
| 4 | KINETIC-3: Spin-Up | 1 | Gun feel at the source; the player touches it constantly. |
| 5 | PHYSICS-1: Peak Heating | 1 | Blackout is a ready-made sound and sight moment. |
| 6 | MISSILES-5: Warhead Stockpile | 1 | Gives launches weight. |
| 7 | AUTOFLEET-4: Rethink or Despair | 1 | Theatre for the fleet's fear. |

Total: 10.

## 4. Motions to Co-sponsor

**Motion A — "The Long Shot" (to MEGA and RAILGUN councils).** Merge MEGA-2, RAILGUN-2 and GAME-FEEL-H: the superheavy gun fires with a built-in Warhead Cam, the Hush and a map-wide muzzle flash.
*Clerk's Annex.* On each shot (every 20 s), `timeScale` 0.2 for 1.2 s, trauma 0.9 (attenuated by distance, so players across the map feel a rumble), a 1.0 s bus-mute at impact, and a lagged bang (delay = dist_px / 2200 s, capped 0.6 s). Hooks: `updateCamera` L6649, `shake` L6675, `STRAT` L6878, `musDuck` L1298. Build size M, on top of GAME-FEEL-1/2/3. TM1. Tenet: the gun's rate and damage are untouched; cinematics skippable and off with `reducedMotion`. Interactions: Sim (single `G.timeScale`), Accessibility.

**Motion B — "Gun Voice" (to kinetic-arms).** Merge KINETIC-3 with GAME-FEEL-1 and GAME-FEEL-4: spool drives audio pitch, recoil and the Roar.
*Clerk's Annex.* Per-gun `spool` float (one float per gun, no new draw). Pitch = base × (1 + 0.5·spool) on `sfxAt`. Voice budget 24; guns beyond the budget contribute to the battle bed intensity instead of voices. Hooks: `fire()` L4855, `SFX.shoot` L1123, `updUnits`. Build size S–M. TM1. Tenet: strict buff; nothing capped.

**Motion C — "Show Your Cause" (to new-player).** Merge NEW-PLAYER-4 (Because Chip) with GAME-FEEL-2 (trauma): the chip and the shake carry one scale.
*Clerk's Annex.* A global `power` 0–12 scale on `feel.big(power)`; the chip reads the last event's power and cause ("Reaper-P hit: 6"), and the camera trauma uses the same number. Hooks: `hurt` L4971, `destroyEnemy` L6834, `shake` L6675. Build size S. TM1. Tenet: information and feel only.

## 5. Trades

- I shrink GAME-FEEL-5 (Legible Mass) to the zoomed-out density glyph and team-coloured sparks only. The sprite-cache rim variants wait until the Render council confirms zero per-frame cost; this keeps the bill out of the L range.
- I will bind GAME-FEEL-3 (the Director) so it only fires on events a coalition bill already names (Second Siege, Long Gun), rather than inventing its own triggers.

## 6. Constitutional Challenges

- **PACING-HOT (the more you build, the sooner the fleet arrives).** Article III. It ties the player's industry to a worse outcome; it denies the player the toy of a long prep for building. The sponsor flagged it. The Bureau rules it **unconstitutional unless reframed** as the fleet being *scarier* on arrival, not earlier.
- No Article IV challenges.
