# STABILITY & POLITICS COUNCIL — Second Reading (Round Four), Society Bloc

## 1. Speech

Mr President. The Ministry has been told to be less complicated and it weeps with relief. We were drafting a button for the crisis, a button for each decree and a button for every lie. You have taught us that the people do not push buttons; they *elect a mood*, once, and the war does the rest.

So we have gone through our own drawers with a lamp. The decrees now fire themselves under one standing order. The lies are told by a building, not a click. The crisis asks you a question exactly once, in peacetime.

And we found something in the drawer: a free refill. A government that collapses recovers forty points of morale in five seconds. We had been calling that a mercy. It is a *coupon*. We shall fix it by lengthening the sickness, not by shrinking a single toy.

## 2. The Re-reading

**STABILITY-1, Casualty Ledger — KEEP.** Fully automatic: `D` counts organic deaths, a Veterans' Bureau is built once, machines pay nothing. Amendment I: it is a cost on mass, not a cap, and it is small (232 dead = 5.1 points; 20,000 = 16.2). Honest caution: at cap 65, 16 points cost about 25% of influence income, so Veterans' Bureaus must be cheap (110 IC) and visible in the Alarm Panel receipt. One-Button: pass. Line: pass (counts bodies, not positions).

**STABILITY-2, Maskirovka Bureau — AMEND.** As written the three campaigns were per-use clicks and fail Article IX. Amendment: the Bureau runs a *standing declaration* chosen once ("Declare: Air / Ground / Sea / Space doctrine"), spoofs the reported `enemyIntel.dominant` continuously, and auto-replenishes up to 12 decoy emplacements (5 IC each) from its own slot every 45 s. Cost is influence upkeep (0.4/s), not a click. The enemy's answer stays: a third identical lie flags "propaganda detected" and the fleet falls back to carpet fire. Line: pass; decoys sit on the ground line and are targeted by band.

**STABILITY-3, State of Emergency — WITHDRAWN (already folded into LORE-1 and "Cutting the Lines").**

**STABILITY-4, Information Policy Dial — AMEND.** One standing choice (Free / Managed / Censored), default Managed, so One-Button passes. Amendment: drop the 10-influence switch fee and 120 s lockout (friction that punishes a choice) and keep the dial as a free change with a 60 s settle. Censored's deferred bleed stays: it is a cost, not a nerf. Line: pass.

**STABILITY Hot Take, Decrees — AMEND.** Three timed buttons fail Article IX. Amendment: **Standing Decree Orders**, one panel set once: tick which decrees may fire and a stability floor (default 30%). Haste fires itself when 4 or more construction sites or queue items are waiting; Reprisal fires at invasion phase starts and when a city district is hit; Evacuation fires itself after a building is destroyed. Costs unchanged (12/15/10 points, ×1.25 inside 120 s). Amendment I (no power creep): each decree issued adds Fear +0.1 that decays over 120 s, so the fleet notices a war economy. Merged with Shock Brigades so the two do not stack. Line: pass.

**Docket "The Fleet Reads the Home Front" — AMEND.** Recruiter Strike is withdrawn (Autofleet), Maskirovka follows the amendment above. **Docket "The Wire" — KEEP** (display only, no input). **"Cutting the Lines" — AMEND (Round 3 terms stand)**, plus one-button: the crisis modal never waits; a standing **Crisis Stance** (Ask / Coalition / Martial Law / Continuity) is set once, default Coalition, and Ask auto-picks after 10 s.

Counts: KEEP 2, AMEND 5, WITHDRAW 1.

## 3. The Order Paper

### #5 — STABILITY-5: THE CIVIC CONTRACT (how megaprojects shape a people)

*Pitch.* "A ring makes a nation hopeful. A cave makes it grim. The Ministry simply writes it down."

**Clerk's Annex**
- **Code finding first.** MEGA-5 writes `civ.stabCap` into `g.mult.stab`. The cap formula is `65 + capBonus − capPenalty + max(0, G.mult.stab) × 30 − 3·superN` (L6338), so a "+4" would be +120. Amendment: write to `capBonus` through a new `G.civCap` term.
- **Mechanic.** Each society tag from a megaproject carries one political number set, applied automatically: **Spacefaring** +0.5 cap per section and +0.1 influence/s per section (a people investing in the future); **Arcology** +4 cap, Casualty Ledger penalty ×0.5 (a sheltered, concentrated population); **Fortress** +2 cap, city-loss penalties ×0.75; **Chthonic** −3 cap but crisis mutiny fraction ×0.5 (shelter morale); **Abyssal** +0.8 cap per section. Each is a gain paired with MEGA-5's named enemy adaptation. Headline label on completion, no prose.
- **Real science.** WWII shelter studies (the London Underground raids; the UK Strategic Bombing Survey) found morale resilience tracked shelter and routine, not bombardment totals.
- **Code.** `society()` L3549, cap formula L6338, `cityStabPenalty` L4547, crisis mutiny L6641. **Size:** S. **TM1.** **Research:** none new, it rides each project's node.
- **Tenet (Amendment I):** player gains only, each with an enemy answer. **One-Button:** automatic at completion. **Line:** vertical structures, no depth.

### #9 — STABILITY-6: THE RALLY AND THE SHOCK (what a frightened enemy does to your people)

*Pitch.* "A fleet that grows is a fleet the people can *see*. Fear in orbit is a rally on the ground, until the first city falls."

**Clerk's Annex**
- **Mechanic.** When the Fear Index first crosses each band (Uneasy, Alarmed, Desperate) a **Rally** adds a temporary cap bonus of +3, +5, +7 that decays linearly over 180 s. Re-crossing the same band later pays half (habituation). At **Desperate**, city and housing losses hit ×1.25 (the rally has turned to shock). The Alarm Panel hover gains one line: *"The people see the fleet grow. They rally for 3 minutes; a lost city at Desperate hurts a quarter more."* Maskirovka lies to the fleet, not to the people: the Rally uses real Fear.
- **Real science.** Rally-round-the-flag (Mueller 1970): presidential approval jumped about 39 points after 9/11; it faded within months. Habituation is the standard decay.
- **Code.** `G.fear` from AUTOFLEET-6; cap block L6325; city-loss paths L6564 (housing) and `hurtCity`; `syncHUD` L11183. **Size:** S–M (~110 lines). **TM1.** **Research:** none.
- **Tenet (Amendment I):** a bounded buff (+7 for 180 s) beside a named cost (×1.25 at Desperate); the enemy's escalation is the reason. **One-Button:** fully automatic. **Line:** map-wide scalar.

### #10 — STABILITY-7: THE TABOO BOOK (is the strike economy balanced?)

*Finding, with arithmetic.* Taboo costs are 10–24 points (nuke 14, MIRV 11, firestorm 10, antimatter 18, Mind-Forge 24; ×0.6 with uranium). Regeneration is at most 0.5/s, so the sustainable rate is one nuke per 28 s. Forty-four strikes in a long match is exactly that rate, so the *flow* is balanced. The flaw is the **crisis refill**: at 0% the recovery ramp restores 40 points (k=1) in 5 s at 8/s, 30 points in 3.75 s (k=2) and 20 points in 2.5 s (k=3), before mutiny stands down. A crisis therefore *licenses* one to three more nukes for a few seconds of frozen production. That is how 44–46 strikes ended at 0% in Reports 6–7 without losing. But Reports 8 and 10 won flawlessly with zero strikes, so strikes are **not** blatantly overpowered; I propose no nerf to any strike.

**Clerk's Annex**
- **Mechanic.** A crisis lasts at least `10 × k` seconds (10, 20, 30 s): mutineers stay inert and production stays frozen for that long even if stability is already at the floor. Recovery rate unchanged. The panel shows "Next crisis: refills 40, costs 10 s of production". The Ledger receipt (NEW-PLAYER-3) lists the crisis refill as a line.
- **Code.** `CRISIS_RECOVER_RATE` and `CRISIS_SAFE_FLOOR` (L3845–3849), crisis block L6608–6648 (add `G.crisisMinT`), `launchStrike` L6943.
- **Size:** S (~30 lines). **TM1.** **Research:** none.
- **Tenet (Amendment I):** touches no toy; it prices a failure state the player entered by choice, and the setback stays recoverable and never ends the run. **One-Button:** automatic. **Line:** n/a.

## 4. Blatantly overpowered today

The **crisis refill loop** (strikes at 0%, crisis, refill, strike again), by the arithmetic and reports above. It is cheap rather than game-breaking, so the answer is STABILITY-7's minimum crisis duration, a real cost. No strike, building or unit in my portfolio meets the "could not lose" bar.

## 5. Struck self-limits

Maskirovka's "half effect on repeat" was struck as a nerf. It should **not** return: nothing proves an exploit, and the enemy's "propaganda detected" fallback is the better answer.
