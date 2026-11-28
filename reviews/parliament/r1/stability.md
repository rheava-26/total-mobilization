# STABILITY & POLITICS COUNCIL — Round 1 Position Paper
*Ministry of Public Order and Information. Slug: `stability`.*

## 1. Opening speech

Honourable members, citizens, and the gentlemen of the Air Staff who have not looked down from their Falcons since Report 6.

The Ministry has examined the home front across five consecutive victories and has the honour to report that **it did not notice them.** In Reports 8 and 10 the people of Earth watched two hundred and forty Falcons and a hundred and thirteen Kites destroy the invader and their confidence did not move *one point*. That is not stability. That is a sleeping patient. The Ministry has a word for a population that feels nothing during the end of the world, and the word is "unmanaged".

Meanwhile, in Reports 6 and 7, a different general fired forty-four nuclear and thermobaric strikes and watched the national mood hit zero. We responded with a polite little ramp of eight points a second and a grace period. The public called it "a bad afternoon".

The Ministry's complaint is simple. Politics in this game is a **tax on one button**. You pay it or you don't. It has no weather. It has no news. Nobody lies to anybody. The fleet, who we are told is *afraid of us*, has never once been given something to be afraid of that was not a missile.

We propose four instruments and one outrage. We will make the home front a *front*: it will keep a ledger, it will hold a press, it will run a deception, it will take emergency powers, and it will, if asked, burn itself for speed. The Air Staff may keep every Falcon. They will merely discover that the people watching them have opinions.

*(Distributes a leaflet. The leaflet is also a budget.)*

## 2. The Gap

Evidence from the code (`title.html`) and the President's reports:

1. **Stability is a one-input system.** The only routine drain is the `taboo` flag on STRAT strikes (`STRAT` L6878; `stratStab` L6890 returns 0 for every non-taboo strike). Reports 6–7 (44–46 strikes) hit 0%; Reports 8 and 10 (0 strikes) never moved. Stability is not a home front; it is a *strike meter*.
2. **Losses are free.** Reports: 232 of 251 infantry died in Report 7, yet `destroyUnit` (L6812) only increments `G.unitsLost` for the after-action report. Casualties never touch stability. The most visible cost of a war, the dead, has no political price at all.
3. **Crisis is a flat ramp.** The v4.5 softening was right (never the end of a run). But the crisis block (L6608–6648) is one script: freeze, mutiny a random 30/45/60%, ramp +8/s to a floor, grace. The player makes **no decision** at the most dramatic moment the system has.
4. **The fleet reads you but you cannot lie to it.** `G.enemyIntel` (L5773: `aa, lasers, infantry, robotic, navy, spaceReach, dominant`) drives `counterBias` (L5509), so the fleet already adapts to your army composition. The President's seed, "propaganda that shifts the Autofleet", has a ready-made hook that nothing uses.
5. **The event engine is dormant.** `EVENTS_LIVE = false` (L1099); `EVT1` (L1072) exists with a stability-cap penalty, but news and headlines are toasts. Stability's feedback to the player is a number in the HUD (`hStab`, L11133).
6. **Influence momentum is a pure saving reward.** `MOM_CAP 3.0 / MOM_TAU 25` (L6358). Nothing about politics makes you *want* to spend it on the people.
7. **Superhuman upkeep is a hidden tax.** `-3 * G.superN` on the cap (L6331) is visible only as a tiny ⚡ badge. A player with 15 cadres gives up 45 points of ceiling and is never told that this is a *political* act.

**Tenet guardrail.** Nothing below caps unit counts or nerfs a weapon. Every cost is upkeep-class (a ceiling that can be rebuilt toward, the existing "no death spiral" doctrine) and every cost has a purchasable or researchable answer.

---

## 3. Bills

### STABILITY-1 — The Casualty Ledger

**Pitch (in character).** "The Ministry has noticed that the dead do not vote, but their mothers do. We propose to count them. Not with sorrow. With a *logarithm*."

**Player fantasy.** You can still throw ten thousand riflemen at the sky. But the home front feels the *weight* of choosing mass over machines, and a Veterans' Bureau lets a mass-army player pay the bill with a building instead of a nerf.

**Clerk's Annex**
- **Mechanic.** Track `G.dead` = organic friendly units destroyed (excludes `d.drone`, `d.elec`, `d.mech`, `d.clone`, non-crewed capitals, per the `isMechanical` test at L4541; builders excluded as in L6816). Stability ceiling penalty `ledgerCap = 6 * log10(1 + D / 40)`, added to `capPenalty` beside the dystopia/`evt.capPenalty` lines (L6325–6326). Benchmarks: 232 dead = 5.1 points; 2,000 = 10.3; 20,000 = 16.2. It is slow and sub-linear, so a 20,000-death army costs under a fifth of the ceiling. A **Veterans' Bureau** (ECON, 110 IC, 40 MP) halves the penalty per building up to 3 buildings (−50%, −75%, −87%: diminishing returns on the *answer*, not on the player's toys). A **crew-saving doctrine** (armour, medevac, Biotech `engineered composure`) lowers organic deaths. Machine armies pay nothing.
- **Real science.** Mueller (1970, 1973): US public support for Korea and Vietnam fell with the *logarithm* of cumulative casualties, not linearly. Later work (Gartner & Segura) refines this to local and marginal-casualty framing. The `log10(1 + D/40)` shape reproduces the "first deaths hurt most" curve.
- **Code grounding.** Hook in `destroyUnit` L6812–6816 (increment `G.dead` beside `G.unitsLost`); apply in `updateGame` capPenalty block L6318–6331; show in the HUD cap readout `hStab` L11133 and in `G.stats` after-action (L6139 area, `lostByType`); new building in `BUILD_DEFS` L3381 modelled on `propaganda` L3386 (`econ`-style; add `ledgerCut`).
- **Research attach.** No new research. The Veterans' Bureau unlocks with the existing `biotech` node (cost 60 × 1.6 load multiplier); it reuses an existing branch so no tree changes.
- **Build size.** S–M (~90 lines).
- **TM1.**
- **Tenet check.** Passes: it is a *real cost* (upkeep class). It does not cap or deny a unit; it makes chaff infantry *visible* as a political choice. Floor stays at `max(20, …)`.
- **Interactions.** Ground/Infantry councils get a reason to design armour and medevac; Air/Naval benefit from crew survivability (ejection, damage control); Robotics/Automation gain a hidden advantage (machine immunity) the Research council can sell.

---

### STABILITY-2 — The Maskirovka Bureau (propaganda that shifts the Autofleet)

**Pitch.** "A nation that cannot lie to its enemy has only half a defense ministry. Let the fleet read our order of battle; let us write it for them."

**Player fantasy.** You are a deceiver. You build a rubber army, a fake foundry district and a wireless ghost, and the fleet's bunker-busters fall on cardboard while the real arsenal sleeps.

**Clerk's Annex**
- **Mechanic.** New building **Maskirovka Bureau** (SPEC, 160 IC, 55 MP, 20 influence per campaign) offering three campaigns (influence-priced, 45 s duration each, cooldown 90 s, which resets momentum like any influence spend):
  1. **Order of Battle.** Overwrites `G.enemyIntel.dominant` for the fleet's `counterBias` (L5509) with the axis you pick (e.g. claim "infantry" while you are mostly Falcons) for the next intel scan, so the fleet counter-prepares the wrong thing.
  2. **Dummy Grid.** Spawns up to 12 inflatable **decoy emplacements** (own 5 IC each, 8 HP, no weapons) that the bunker-buster targeting list (`decoyHitX`, L3601; bunker-busters chase value) treats as high-value for 45 s.
  3. **Quiet Sky.** Hides the player's `strat` silos from the phase-2 scan for 30 s (`G.blackout` is already the enemy's own tool; this is the mirror).
  Counterplay and cost: **high-intel enemies** (the fleet's scans every ~60 invasion-seconds at phase 2) can see through a campaign they have already seen once: the *second* identical campaign inside 4 minutes gives only half effect. That is a cost on mechanical repetition, not a cap.
- **Real science.** Operation Bodyguard/Fortitude (1944): inflatable tanks, dummy landing craft and a fictitious First US Army Group kept two panzer divisions in the Pas-de-Calais. Kosovo 1999: Serb decoy tanks and bridges absorbed a large share of NATO sorties. Radar cross-section decoys (ADM-160 MALD) are the airborne version. The fleet's *enemyIntel* is analogous to a classifier; spoofing a classifier is a standard adversarial technique.
- **Code grounding.** `G.enemyIntel` assigned at L5773 (`dominant` selection nearby); `counterBias` L5509; `decoyHitX` init L3601; phase scan in `updateInvPhases` L6019; building def in `BUILD_DEFS` L3381; influence spend uses `G.infSpent = true` so the momentum snap-back (L6350) is automatic.
- **Research attach.** New node **`maskirovka` "Perception Management"**: branch `def`, `pre: ['pointdefense']`, cost 170 (before the ×1.6 load multiplier), `time: 14`, **not dangerous**, unlocks the Bureau. **Combo**: `maskirovka` + `psionics` (dangerous, sc 8) unlocks **Mirror Earth**, where a phantom of your whole force appears on enemy scans for 60 s. SCI-FI: psionic "collective broadcast" fits the canon of decentralised command; its internal logic is that the fleet *classifies* by pattern and psionic signal noise forges the pattern. Combo costs 8 stability unrest through the existing `stabOffset` path.
- **Build size.** M (~260 lines).
- **TM1** (the intel-spoof half is also a TM2 society-slice seed: "forecast-sharpening intel").
- **Tenet check.** Passes: pure additions; the player's toys are untouched; the enemy gets *more* interesting without being nerfed.
- **Interactions.** Enemy-design council: a fleet that must learn to doubt its scans (every 3rd identical campaign flags "propaganda detected" and swaps to blind carpet fire, a scarier enemy answer). Defense council: Aegis and Radar sit alongside decoys. Air council: decoys protect airbases.

---

### STABILITY-3 — The State of Emergency (the crisis becomes a decision)

**Pitch.** "When the government collapses, we should have the courtesy to ask the people in the room what they want."

**Player fantasy.** The worst minute of your war becomes the most *authored* one. You are not rescued by a script; you choose your poison.

**Clerk's Annex**
- **Mechanic.** At crisis trigger (L6635) the game pauses the crisis ramp for up to 10 s and presents three emergency options (auto-picks **Coalition** on timeout):
  - **Martial Law.** Recovery ramp ×2 (16/s), mutiny fraction halved; sets `G.dystopia` for 180 s (cap −20, already in the cap block L6325) and the society tint turns red.
  - **Coalition.** Normal ramp; mutiny as today; gains +6 influence per second for the crisis duration (a negotiated cabinet that funds research).
  - **National Unity Appeal.** Recovery ×0.6 but the production freeze is lifted for **non-strike** units (the people are willing to work through the mess). Mutiny ×1.3.
  The `crisisK` stacking (L6628, 6645) multiplies each option's cost, so a third crisis is genuinely worse, never terminal.
- **Real science.** Constitutional emergency powers: the Roman *dictator* (six-month limit), Weimar Article 48, the UK Defence of the Realm Acts. Political science (Gross & Ní Aoláin) shows emergency powers *work* short term and corrode norms long term, which is exactly the cap-vs-tempo trade here.
- **Code grounding.** Crisis state machine L6608–6648 (`CRISIS_RECOVER_RATE`, `CRISIS_MUTINY_FRAC`, `CRISIS_SAFE_FLOOR` L3845–3849); `G.dystopia` cap effect L6325; the Super Events engine `EVENTS` L1081 / `fireEvent` L1101 can host the modal (it is held by `EVENTS_LIVE` L1099; this bill only needs the modal helper, not the full events layer); HUD banner L10226.
- **Research attach.** Gated options: Martial Law needs nothing; Coalition needs `automation` (negotiated with the machine unions); Unity Appeal needs `shieldtech` (a believable shelter promise, per the ROADMAP tech-credibility axis at L973).
- **Build size.** M (~170 lines).
- **TM1.**
- **Tenet check.** Passes: it adds agency to a setback; nothing is denied.
- **Interactions.** Society/Doctrine councils (dystopia tint); Events council (shared modal); Enemy council (the fleet can read "Martial Law" as a weakness axis for TM2).

---

### STABILITY-4 — The Information Policy Dial

**Pitch.** "A free press tells the truth quickly. A managed press tells you what to feel. A censored press tells you nothing, and you pay for the silence in influence."

**Player fantasy.** You set the tone of your whole civilisation with one lever, and the war report you read back at the end shows exactly what that lever did.

**Clerk's Annex**
- **Mechanic.** A three-position policy (HUD gear next to stability, changeable once per 120 s, switching costs 10 influence):
  - **Free Press.** Influence income ×1.15; stability *swings* are doubled (strike costs and city losses hit ×2, but regen toward cap is ×2); **Intel Forecast**: the HUD shows an *incoming-phase* countdown 15 s earlier and an arrow for the next flank.
  - **Managed.** Today's behaviour (default).
  - **Censored.** Stability hits ×0.5, regen ×0.5, influence ×0.8, no forecast; each strike's taboo cost is replaced by a **−2%/min** slow bleed (the cost is *deferred*, not forgiven). Dystopia flag is **not** set but **Superhuman cap upkeep doubles** (the state needs more cadres to enforce it).
  A **news ticker** (one line, bottom of the HUD) emits labelled *systems headlines*: "Casualty index 14 · Influence index 1.8× · Tarantula Valley contested". No scenes; numbers and labels only.
- **Real science.** Amartya Sen's finding that famine does not occur in functioning democracies with a free press (information feedback). The CNN effect and Soviet Chernobyl response (1986) show opposite failure modes. Kahneman's "peak-end" effect justifies the end-of-wave headline.
- **Code grounding.** `stabRegen` L6332; influence income L6360; `stratStab` L6890; HUD at L11133 and `hStab`; Phase announcements `announcePhase` L5683; superhuman term at L6331. Policy state on `G.policy`.
- **Research attach.** Unlocked by a new building, **Press Bureau** (ECON), gated by `info` (root). The "Forecast" tier requires `directedenergy` (a sensor-fusion fiction: radar *and* press).
- **Build size.** M (~200 lines).
- **TM1** (the "forecast-sharpening intel building" slice from Standing Decision 4); full free-vs-command society axes are **TM2**.
- **Tenet check.** Passes: three valid play-styles; every stance has benefits and costs; none denies a toy.
- **Interactions.** TM2 society layer (this is the TM1 seed); Strategic-strike council (the Censored deferral changes the stab cost of 45 strikes).

---

## 4. Hot Take — DECREES: Burn the Home Front for Tempo

**Pitch.** "In 1941 the Soviet state moved fifteen hundred factories east in five months, and the workers slept in the snow beside the machines. They did not call it a policy. They called it *the war*. The Ministry proposes to let the player do the same. For a price. In public."

Stability is currently something you *avoid spending*. We flip it: it is a **resource you can burn for time**, because IC cannot buy time (212k IC sat unspent in Report 7). Time is the one thing the player never has enough of.

**Clerk's Annex**
- **Mechanic.** Three **Decrees**, issued from a new **Decrees** tab (needs a Press Bureau, bill 4, or the existing Propaganda Bureau):
  - **Decree of Haste.** 60 s: construction, production and research times ×0.5. Cost: −12% stability on issue and −0.15%/s while active (21 total).
  - **Decree of Reprisal.** 45 s: all organic units +25% fire rate, +15% move speed. Cost: −15% stability.
  - **Decree of Evacuation.** 90 s: destroyed buildings can be re-placed at 50% IC with no build time at the original site. Cost: −10% stability plus −3% per building re-placed.
  Each use inside a 120 s window multiplies the next decree's stability cost by 1.25. **At stability 0 the decree is still allowed** (desperation, as strikes are, `STRIKE_STAB_FLOOR` L6892) but the crisis banner shows a "decree-driven" tag.
- **Real science.** Operation of the Soviet evacuation of industry 1941 (Gosplan; roughly 1,500 plants moved); Albert Speer's 1942 "rationalisation" doubled German armament output with the same labour via standardisation and speed; Stakhanovism (1935) as an incentive-fueled burst.
- **Code grounding.** `build:` timers (`BUILD_DEFS` L3381), `resolveResearch` timing (L4692), unit `rate` modifiers (`G.mult`), `G.stab` write sites (L6950, L8462 pattern); crisis interplay L6635; HUD button via the strike-menu pattern L7010.
- **Research attach.** Needs `automation` (the logistics control system) for Haste; `psistudies` (dangerous, sc 5) for Reprisal as "synchronised crews"; `nanotech` (dangerous sc 5) for Evacuation (self-assembling site). Each use of a dangerous-tech decree adds 3 to `stabOffset`.
- **Build size.** M (~250 lines).
- **TM1.**
- **Tenet check.** Strongly yes: the player is *given* more power, paid for in a recoverable, rebuilt-toward resource. No cap, no nerf.
- **Interactions.** Economy (answers the 212k IC glut by letting stability buy what IC cannot); Strategic-strike council (Decrees compete with strikes for the same stability pool, creating the first real *political budget*); Enemy council (a decree-driven crisis raises the TM2 "fleet sees weakness" flag).

---

## 5. Alliances sought

- **Enemy / Fleet Psychology council.** Maskirovka (STABILITY-2) and the Decrees both feed `enemyIntel`. We offer them a fleet that can be lied to; they offer us a fleet that learns it has been.
- **Ground / Infantry council.** The Ledger gives them a reason to build armour and medevac; chaff infantry become a *choice*.
- **Economy / Logistics council.** Decrees convert the unspent-IC glut into tempo.
- **Research / Doctrine council.** The state-of-emergency options and decree gates make every dangerous tech a political posture.
- **Strategic Strikes council.** Stability is now a shared budget; they can price strikes against it knowing the player has other ways to spend and earn it.
