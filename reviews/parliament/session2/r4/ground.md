# GROUND STAFF (Bloc Whip) — Second Reading, Second Session (`ground`)

## 1. Speech

Mr Speaker, the Address says the ground is a line. Good. I have been saying so since Round One, and for a month the House heard it as an insult. A line is not a handicap, it is the oldest geometry in war: the Roman *acies*, the Maginot, the Eastern Front, the Korean DMZ. On a line there are no flanks, so there are only four questions: what **stops** them, what **pins** them, what **kills** them while they are stopped, and what **breaks** them when they will not die. I have four caucuses and four verbs. Armour breaks, Artillery kills, Rifle pins, Fortification stops. I also accept the One-Button Law with relief; the Staff has never had enough clerks to draw lines on maps. And to my future Inspector-General: I withdraw two of my own bills before you ask. I expect that to confuse you.

## 2. The Re-reading

Counts: **KEEP 1, AMEND 5, WITHDRAW 2.**

**GROUND-1, The Battalion (with docket "Living Battalion"): AMEND.** *One-Button:* pass. Squads auto-muster when idle within 120 px, drafts run from any Barracks, nothing is clicked. The rename and honours UI is cut. *Line:* pass, the battalion is just a stack on x, and it is the strongest volume-scaling bill I have: ten battalions are ten times one. *Amendment:* the "Shaken, -25% rate" state below 40% strength is a self-inflicted debuff with no proof of overpowering behind it (Amendment I). Replace it with **Fall Back**: a battalion below 40% retreats automatically to the nearest draft depot at +20% speed, refills at IC, and returns. Bio bodies regrow in place for MP, falling back to IC at MP 0 (Art. IV.1 untouched).

**GROUND-2, The Elastic Line: AMEND, heavily.** *One-Button:* **fails as filed.** Drawing Screen/Main/Reserve marker intervals is per-unit micromanagement. *Line:* the idea is native to the strip (depth is x-distance) but "reserve counter-attack" is now ARMOUR-1's Hammer. *Amendment:* no markers. The **Main Line** is computed automatically as the x of the frontmost standing garrison or fortification cluster; ground units beyond it that take sustained fire (5 s) **fall back** behind it and dig in, in step with orbital snipes. One optional standing choice, made once: `Defence: Forward / Balanced / Deep`, which shifts that offset by -300/0/+300 px. Counter-attack is struck from this bill and given to Armour.

**GROUND-3, Counterbattery & Time-on-Target: WITHDRAW.** Superseded by ARTILLERY-3 (counter-battery) and docket #11 ONE NET. Its `firefinder` radar lives on in ONE NET.

**GROUND-4, The Engineer Corps: WITHDRAW.** The Sapper unit with paint-a-minefield orders breaks One-Button. FORT-2 does it correctly: a building seeds a per-cell density value on its own and there is nothing to click. I keep only the discount: sappers present make sluices and seawalls 40% cheaper (see below).

**GROUND-X, The Crawler Capital (hot take, not carried): AMEND.** *Line:* pass, a city that walks left or right is the most native ground idea I have. *One-Button:* fails as filed (who steers it?). *Amendment:* it **auto-relocates** toward the sector with the lowest `G.dmgTaken` bucket at 3 px/s, with one toggle `Capital: Fixed / Mobile`. Enemy answer: Decapitation drops lead the target and `earthbreaker` presses strand it for 20 s. Still XL, still not carried.

**Docket #11 ONE NET (co-sponsor): AMEND.** The target-box painting is a chore. Default: artillery within the net auto-times every shot (`solveToT`) at whatever it is already engaging; **SYNC FIRE** is one optional button. *Line:* pass. *Tenet:* pure buff, supply is the cost.

**Docket #13 THE NAVIGABLE MOAT (co-sponsor): KEEP.** Sappers become a building-level discount, flood warnings are automatic, `amph` units cross. *One-Button:* canal brush is optional, floods are one decision. *Line:* water is a vertical/horizontal terrain element, not ground depth.

## 3. The Order Paper

### Item #1 — covered above. 

### Item #3 (LEAD) — How each arm earns its place on a one-dimensional front

**Whip's division of the Line (for the four caucuses).** On a line there is one scarce thing: **dwell time**, the seconds an enemy spends inside the band your guns reach. Each caucus owns one verb on that clock.

| Caucus | Verb | What it adds to dwell | Bills it owns |
|---|---|---|---|
| Fortifications | **STOP** | halts the enemy (wall, mine, dome lattice) | FORT-1, 2, 3 |
| Rifle | **PIN** | slows and softens (suppression, city hold, cellar) | RIFLE-1, 2, 3 |
| Artillery | **KILL** | hits what is halted, in the 300-800 px band no one else reaches | ARTILLERY-1, 2, 3 |
| Armour | **BREAK** | closes with what will not die: domes, anchors, hulls; the counter-landing | ARMOUR-1, 2, 3 |

**Overlaps I rule on.** (a) ARMOUR-4 Recovery Yard and FORT-4 Engineer's Yard are **one building** (the Yard): repairs structures, recovers hulks. (b) GROUND-2's counter-attack goes to ARMOUR-1. (c) RIFLE-1 Cellar and Subterranean Works' shelters use one `shelterW` field. (d) FORT-1 "Gatehouse closes a flank": there is no flank. Rewrite it as "extends the wall along the line", which is all it ever did.

#### GROUND-5 — THE KILL ZONE (the combined-arms ladder)
**Pitch.** "Four verbs and nobody to say them in order. The Staff proposes the sentence: stop, pin, kill, break. An enemy that has been stopped and pinned and then shelled does not get up."

**Clerk's Annex**
- **Mechanic.** Every enemy ground unit carries a tiny bitmask `e._arm` with a timestamp: STOP (wall/mine/dome hit), PIN (suppressed per RIFLE-3), KILL (splash/shell/rocket from artillery), BREAK (tank-class or breach shell). Damage from a source gets `x(1 + 0.15 x (k-1))`, where `k` = distinct arms that touched the target in the last 4 s (k up to 4 because there are four arms). One arm alone is the baseline. A readout in the unit panel shows "COMBINED ARMS x1.45". **Enemy answer (no power creep):** each arm meets its own wall from `scanEnemyIntel`, not a rule cap: infantry-heavy -> plasma pelters and gunships (existing); artillery-heavy -> `shieldgen` domes absorb shells (new weighting); tank-heavy -> ARMOUR-2's cold-hull Ablative Carapace (x0.55 until heat >= 30%); fortification-heavy -> more `earthbreaker` presses (existing). Mixed armies meet all of them and out-multiply them.
- **Code.** `hurt` L4971 (attach `t._arm |= src`), `acquireEnemy` L4374 (no change), `scanEnemyIntel` L5737 and `GROUND_BASE_WEIGHTS` L5488 (weight adds), unit panel readout beside the WPN_LABEL block ~L9894.
- **Size.** M (~220 lines). **TM1.**
- **Research.** Unlocked at start; no node.
- **Tenet (Amendment I).** A buff that names its enemy answers (four, listed). No nerf; nothing capped.
- **One-Button.** Pass: fully automatic, scales with volume (more arms, more multiplier).
- **Line.** Pass: tags are on the target, not on a position; no depth needed.

### Item #4 (with the Rifle Corps) — The Shapeshifters on the ground

The Rifle Corps' RIFLE-4 (Mimic) is the right fix: Swarm form vs infantry, Disassembler form vs `armor >= 12`, and useless against air as the monoculture wall. I co-sign it and add two numbers from the code. The Shapeshifter (L3297) has speed 24 against enemy `gtank` spd 20, `gmech` 15, `leviathan` 11, `earthbreaker` 4, so **catching a tank is already trivial on a line; the problem is what happens when it arrives.** Its plasma at 75 dmg meets armor 14 on its own hull and armor 6-22 on the target.

#### GROUND-6 — THE OPENING (a latched swarm opens the hull for everyone else)
**Pitch.** "The swarm does not need to kill the tank. It needs to open it so the *tank* can."

**Clerk's Annex**
- **Mechanic.** A Shapeshifter in Disassembler form (RIFLE-4) that latches marks the target **EXPOSED**: each latched swarm strips 8% of the target's armor for 6 s, refreshed while latched, for **all** friendly fire, stacking by latch slots (about `s` per hull). A Mammoth (armor 22, s 6) with 6 swarms on it has armor ~11.5. This feeds GROUND-5 as its BREAK/PIN rung. **Enemy answer:** enemy `plasarc` (Pelter, Leviathan) hits a swarm with heat, and heat above 60 turns off swarm regen and above 90 slows 30% (docket ruling 6), so a swarm that rushes a Pelter-escorted column pays for it.
- **Code.** Shifter def L3297, `hurt` L4971 (read `t._exposed`), `allRegen` L7873, `infFast` L7885, RIFLE-4's latch plumbing.
- **Size.** S (~70 lines on top of RIFLE-4). **TM1.**
- **Research.** `shapeshifter` (triple combo psi/bio/nano, dang sc12). No new node.
- **Tenet (Amendment I).** Buff with a named answer (plasma heat). Shapeshifter immortality was already replaced by heat, so nothing is restored.
- **One-Button.** Pass: automatic. **Line.** Pass: contact only.

## 4. Blatantly Overpowered Today

**Nothing in the ground portfolio meets the bar.** Evidence: Report 7 lost 232 of 251 infantry, and the ground arms never carried a match; Report 8 and 10 were won by air and the sky navy (240 Falcons, 113 Kites, 138 capital-class sky ships). The one ground thing that *was* blatantly strong, the free auto-emplacements, was already nerfed (`EMPLACE_DMG_MUL` and `EMPLACE_FIRERATE_MUL`, L4563, ROADMAP L1027), and I do not propose touching them again. Ground is under-powered, not over.

## 5. Struck Self-Limits

I did not strike a limit from my own bills as "a nerf" in the First Session. I did *trade away* the Veteran/Guards honours in GROUND-1 to Stability; they should come back, because they are a pure buff with a proper enemy (the fleet's Mirror echoes), and they cost nothing in complexity once battalions exist. The "Shaken" debuff I am now replacing with Fall Back, and it should not return.
