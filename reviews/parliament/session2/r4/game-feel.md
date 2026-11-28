# GAME FEEL & VFX COUNCIL, Second Reading (Round Four)

## 1. Speech

*The Bureau of Spectacle rises. It is wearing, for the first time, a tie.*

Mr President, you said "I love the fear meter, I wish I could see it." Nobody has ever said that about a spreadsheet. Of course you want to *see* it. A fear that lives in a formula is paperwork. A fear that you can hear is a war.

And you said the player must feel complimented, not taxed. The Bureau has one tool for that, and it is the oldest in the theatre: the **curtain call**. When the fleet commits its reserves because your industry frightened it, the House should rise. Gold, not red. A bell, not a siren. The enemy is not punishing you; it is blinking.

On the Amendments: I accept the One-Button Law with relief. A feel system that needs a button is a feel system that is broken. Everything I make runs itself and has one standing switch. On the Line: sound and light do not care if the ground is one-dimensional. The sky has plenty of room.

*The Bureau yields, and rings the bell.*

## 2. The Re-reading

**GAME-FEEL-1, The Mix Bus: KEEP.** Positional pan, distance lowpass, speed-of-sound lag, a 24-voice budget and cached noise buffers. It runs on every sound without input. One-Button: pass; the only control is one standing "Sound Mix: Cinematic / Flat" choice. Line: pan is world-x; altitude only filters. Amendment I: nothing here touches a unit.

**GAME-FEEL-2, The Impact Budget: KEEP.** Trauma shake, log-summed, with distance attenuation, plus rare-event hit-stop as time dilation. One-Button: pass, nothing to press. Line: n/a. Amendment I: the sim step stays uniform, so the enemy gains no relative time and no unit is weakened.

**GAME-FEEL-3, The Money Shot (Director): AMEND.** Honest correction: as written it had three separate options (`Cinematics: On / Short / Off`, per-trigger rules, cooldown), which is babysitting. Amendment: ONE standing choice, "Cinematics: Full / Short / Off", default Short; triggers and the 45-second cooldown are fixed constants. Every moment ends on any input and never takes the camera while the player is placing or aiming. Line: camera and time only.

**GAME-FEEL-4, The Roar: KEEP.** The battle bed and Kill Ladder are driven by data the sim already collects (`G._tickKills`, enemies and projectiles on screen). More units makes it louder, which is the exact volume-scaling the Law asks for. One-Button: pass. No gameplay effect.

**GAME-FEEL-5, Legible Mass: AMEND, and WITHDRAW one clause.** Withdrawn: the per-hit white "hit pip" second bitmap. It doubles the cache and risks bringing the lag back; the Inspector would be right to charge it. Kept: the baked team rim (zero per-frame cost) and the team-coloured sparks. Amendment: the zoomed-out density glyph is the performance win, so it ships first. One-Button: pass. Line: reads a one-dimensional front better, since units stack and overlap and need a rim to separate.

**GAME-FEEL-H, The Warhead Cam and the Hush: AMEND.** Honest correction: a cinematic that fires on every strike becomes a chore after the fifth, and "Always / First 3 / Off" is a menu. Amendment: it plays on the first three strategic strikes of a match and on any strike above 40 stability cost, then goes silent; one standing choice "Warhead Cam: On / Off". It never delays the strike, and it never blocks input. Line: vertical flight is the whole point.

**Docket 1, The Thermal Field (joined): AMEND.** Co-signed only on the rule already in the docket: heat is shown by cached tint stamps. Added: tint has 4 steps (cool, warm, hot, white), baked once per sprite, and heat gets a sizzle voice that obeys the Mix Bus budget. Heat is a mix-your-army reason, which the Bureau loves because it is visible from a distance.

**Docket 10, The Long Gun (sponsor): KEEP.** The first shot in any 20 s gets the cinematic beat, on the same Cinematics choice as above. Amendment I: with no section limit the gun is strong; the fleet's Interdiction answers it, which is the enemy being scarier, not the gun being capped. One-Button: pass. Line: a gun that points at the sky is the purest vertical bill in the docket.

**Docket 17, The Fleet Will Return (sponsor): KEEP.** The two-tier Director is already agreed (short beat on a retreat, full sequence on the final). It plays on its own.

Counts: KEEP 5, AMEND 5, WITHDRAW 0 whole bills (one clause withdrawn: hit pips).

## 3. The Order Paper

### Item 1: covered by the re-reading above. Every Bureau bill now has one standing choice or none.

### Item 9: GAME-FEEL-6, THE CURTAIN CALL (the Fear Index you can hear, and the fleet's reserves as a compliment)

**Pitch.** *"The panel shows the number. The Bureau shows the meaning. When they commit reserves because you built, the screen should say 'well done', not 'you are being punished'."*

**Clerk's Annex**
- **Mechanic.** A feel layer on top of AUTOFLEET-6 (the Alarm Panel, which owns the numbers and the formula; the Bureau does not touch them).
  1. **Needle voice.** The ALARM needle plays a soft tick whose rate follows F (about 1 tick per 4 s at F 0.5 to 4 per second at F 4), on the Mix Bus at low gain; silent in a 3 s window after any boss alert so it never masks a warning.
  2. **Band crossing.** Moving up a band (Calm / Uneasy / Alarmed / Desperate) fires the Curtain Call: a gold border pulse on the panel (1.2 s), a rising two-note bell, and a banner whose text is a *compliment with a figure*, for example "THE FLEET NOTICED YOUR INDUSTRY. +9% SHIPS, +100% YOURS." It reuses `banner()` and the same gold style as the Chronicle. Never red, never "WARNING". One-Button: it announces, it never asks.
  3. **Reserves committed.** When `fleetMax` is raised at the prep to invasion flip because of F, a one-time cue: a ring of extra points of light appears in the sky above the player's densest industrial cluster, brightness proportional to the ships added, and the cue describes the number in the banner. The player sees exactly where their industry was noticed.
  4. **After-action.** The "Fear paid vs power gained" row gets a small bar pair (their growth vs yours), drawn with the existing chart helpers, no new dependency.
- **Real science.** Perceptual loudness and rate follow log stimulus (the same Weber-Fechner rule as GAME-FEEL-2); a clicking rate is the oldest legible readout of a rising quantity (the Geiger counter).
- **Code grounding.** `syncHUD` L11183 (the panel, from AUTOFLEET-6), `banner` L10335, the prep to invasion flip at L6492 (where `fleetMax` is set), `SFX` L1123 (add `bell()` and `tick()` on `blip`), `sfxAt` from GAME-FEEL-1.
- **Size.** S-M (about 120 lines, on top of AUTOFLEET-6 and GAME-FEEL-1). **TM1.**
- **Tenet (Amendment I).** Pure display and sound. No nerf, no buff. It supports the 11:1 trade shown on the panel by making the trade audible.
- **One-Button.** No input at all; one standing "Sound Mix" choice already exists. **Line.** The lights hang in the sky bands above the line; no depth is implied.
- **Research.** None.

### Item 10: see section 4.

## 4. Blatantly overpowered today

**Nothing in this portfolio is blatantly overpowered.** The Bureau's tools change what the player perceives, not what they can do. I name one **defect**, not an overpowered thing: the screen shake is pinned at its cap of 16 in large fights, because 56 `shake(` call sites each take the max (L6675), so the player cannot tell a minor kill from a boss. The evidence is in the code: `shakeAmt = Math.min(16, Math.max(shakeAmt, a))`. The fix is GAME-FEEL-2, a feel repair and not a nerf. I note for the Inspector that I support the Autofleet's reading that the air and sky-navy doctrine is the strongest thing in the game but, as the Autofleet says, the evidence is General only, so I vote for a scarier enemy first, not a nerf.

## 5. Struck self-limits

I struck none of my own limits as "a nerf" in the First Session. Two limits were never struck and should stay because they protect the player's game, not the player's toys: the 24-voice audio budget (it drops sounds, never units, and the Roar covers the density) and the rare-event rule on hit-stop (a time dilation on every kill would make the game feel slower, which is the exact "lag" the designer fears). Neither is a rules cap on the player's power.
