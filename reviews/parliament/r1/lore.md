# R1 — Story & Lore Council (the Office of the Chronicler) — slug `lore`

## 1. Opening speech

*(The Chronicler rises, ink to the elbows, a wire-service pass pinned to a tin helmet.)*

Honourable members. I have been to your war. I have read your after-action reports, and I will tell you what is in them: forty-four strikes, stability zero, 212,000 IC unspent. Numbers. Not one of you has asked what the numbers *meant*.

The Autofleet did not come for sport. One ship got through a catapult that takes a year to recharge. It is a thrown friend, and it is afraid. We built the whole game on that premise and then wrote the premise on a wiki and left it there. The engine for telling it exists. `EVENTS_LIVE = false`, L1099, a loaded gun in a locked drawer. We have forty-odd medals, and thirty of them are jokes about failing to build things. We have one event and it is dormant. We have an ending screen that says "A COSTLY STAND".

I propose that the war speak. It will speak through systems, not cutscenes: a gauge you can read, a transmission you can act on, a dossier that makes your guns hit harder, a ceasefire you may refuse. To the Ministers of Air and Space: your sky is magnificent and it has no history. Give me five bills and I will give it one.

## 2. The Gap

- **The event engine is finished and switched off.** `EVENTS` L1081 holds exactly one event (`autofleet_arrival`). `EVENTS_LIVE=false` L1099. The dangerous-tech vein (Cloning, Superhumans) sits in ROADMAP L983-995 as drafts. Meanwhile `dang:true` research only does `G.dangCap += r.sc` (L4708): a silent number where a scene belongs.
- **Achievements are no longer empty, but they are inert.** ~40 medals at L952-995, mostly gags and scenario outcomes. Nothing a medal does changes play, and none of them point at the story.
- **No codex in code.** `grep -i codex title.html` returns nothing. The lore (warp catapult, benefactor, fleet's fear, four superhuman types) lives only in ROADMAP L401, L639, L1097.
- **The fleet's fear is canon and invisible.** v4.6.0 withdrawal (`startWithdraw` L5990, threshold `WITHDRAW_BREAK_FRAC` L3858) is the only trace. No gauge, no signal, no transmissions, no draw ending beyond the watchdog `STALEMATE` (L6606), which is a bug-catcher, not a story.
- **The after-action report counts everything and narrates nothing** (`end()` L10345, `downloadAARReport` L10725). Reports 6-10 were all wins, and all read the same.
- **Hidden warfare** (the designer's gamer-trained-by-a-game idea) has no home. PREP is a 450s countdown (CLAUDE.md), the least story-bearing stretch of the game.

## 3. Bills

### LORE-1 — THE CHARTER: dangerous techs become standing orders, not pop-ups

**Pitch.** *"Every dangerous doctrine is a promise you made to your own people. Make them hold you to it."* When you finish Cloning, Superhumans, Telekinesis or Nanotech, the Chronicler does not pop a modal. A **Charter** appears on the Research panel: a three-position dial (Restrain / Standing / Unleash) you can turn at any time, with a cooldown. The dial feeds real numbers and the fleet's reading of you.

**Player fantasy.** You are the regime deciding what your society becomes, and you can change your mind and pay for it.

**Clerk's Annex**
- **Mechanic.** Each `dang:true` node (10 nodes, L3513-3531) gets `charter:{restrain,standing,unleash}` giving `{effMult, scMult, extra}`. Cloning example, tied to the draft Event #2: Restrain = clone manpower bonus x0.6, `sc` x0.5, no upkeep. Standing = as today. Unleash = bonus x1.4, clone build time -30%, `sc` x1.8, and the wartime clone-batch spawns +20% unit count (tenet: more power, a real cost). Changing a dial costs 15 influence and has a 90s lockout.
- **Real science (the cost side).** The "unrest" is Kuznets-style wartime-mobilisation fatigue. Real WWI/WWII planners tracked "industrial morale" as a hard variable (the UK's Mass-Observation surveys). Unleash raises the *variance* of stability, not only its mean: add a rare random 6% dip each 120s.
- **Code grounding.** Dang applied at L4708 (`G.dangCap += r.sc`); replace with `dangCap = Σ charterSc(k)`. Event delivery by `fireEvent` L1101 and `evtReqMet` L1063 (reuse the `req` gate to show Charter options by tech). Flip `EVENTS_LIVE` L1099 once three Charter-events exist. `EVT1` L1072 shows the constant-table pattern to follow.
- **Build.** M (about 250 lines): dial UI on the research card, `G.charter{}` state, one hook at L4708, three event objects.
- **TM1.**
- **Tenet check.** Unleash buys *more* toy for more risk. No cap on the player. Restrain is voluntary, never forced.
- **Interactions.** Society (dystopia/verdant/machine tint at `society()` L3550 becomes a result of dials, not research order). Bio-council (clones), Psi-council (Superhumans).

### LORE-2 — FLEET RESOLVE: the fear gauge and the "we stop if you stop" ladder

**Pitch.** *"The one ship watches you through a telescope the width of a mountain. It is counting your factories. Count with it."* A visible 0-100 **Resolve** gauge in the fleet panel. It falls when the fleet loses high-value assets and when your industrial output (IC/s) outgrows what it modelled at landing. It rises when you lose cities. Three bands and one rare exit.

**Player fantasy.** You can *see* the enemy flinch, and the more terrifying you are, the more the fleet does something about it.

**Clerk's Annex**
- **Mechanic.** `G.resolve` starts at 100. Per second: `-0.004 x (IC/s / landingIC/s - 1)` clamped >=0. Per kill: `-v/fleetMax`, where v is 1 for a line unit and 8 for a boss (`G.stats.bossKills`). Per city lost: `+6`. Bands:
  1. 100-60 Steady: nothing.
  2. 60-30 Rattled: the fleet's `G._phase3T` escalation cadence (L5970) speeds up 25% (a scarier enemy, never a nerf to you).
  3. <30 Desperate: it commits a carrier planet-cracker (ROADMAP L403) as a boss event via `spawnSuper` (L5269), a charged 90s beam you must kill.
  - **The rare exit (canon).** If Resolve <20 at invTime>=120s AND `society` is not `null` AND `G.dangCap>=20`, the makers transmit once: **"WE STOP IF YOU STOP."** Accept: an epilogue-draw (a new `G.over='truce'`), with a text assembled from `stats.society`, `stats.eraReached` and Charter dials (LORE-1). Refuse: Resolve snaps to Desperate and the planet-cracker comes early. Accepting is voluntary, never forced.
- **Real science.** Lanchester's square law (a defender with industrial depth wins attrition against a magazine-limited expeditionary force). The *tail-to-tooth* ratio: a one-ship fleet has no tail, and its fear is rational. The one-year charge is a real constraint on a coilgun-launched, energy-limited payload: stored energy E=1/2 m v^2; a catapult thrower limited to one jump per year is exactly why the first ship has no escorts that can come home. Label: **SCI-FI** is the "warp" in the catapult only. Its internal logic: the thrower is mass-limited, not aim-limited, so one ship is the *whole* budget.
- **Code grounding.** `end()` L10345 and the `isDraw` branch (add `'truce'`); `startWithdraw` L5990, `breakCount` L5989, `withdrawThreshold` L5988; `tickFleetStance` L5999; `STALEMATE` handling L6606; `banner()` L10335 for the transmission.
- **Build.** M (about 300 lines): gauge widget, three band hooks, one transmission modal, one ending branch.
- **TM1.** The full psychological-event stack is TM2.
- **Tenet check.** The fleet gets scarier as it fears more, which is exactly the tenet's recipe. The truce ending is a choice and never a cap.
- **Interactions.** Enemy council (planet-cracker, desperation doctrine), Space council (orbital fleet moves Resolve), Society council (the society label shapes the epilogue).

### LORE-3 — BLACK BOXES: the codex is salvage, and salvage is damage

**Pitch.** *"On 18 July 1942 a Japanese Zero landed upside-down in a bog on Akutan Island. Eleven months later the Hellcat was built to kill it. Intelligence is a weapon. Pick it up."* The first time a given enemy kind dies, it may leave a **Black Box** (a salvage pickup). Collect it with any unit and a **Dossier** entry unlocks in a new Codex panel. Each dossier grants a permanent bonus against that kind.

**Player fantasy.** Every new horror you survive makes you better at killing it, and the codex is the proof.

**Clerk's Annex**
- **Mechanic.** `G.dossier[kind]` unlocks on salvage. Reward: +10% damage vs that kind and +10% range on first-strike. Chain: analysing 4 dossiers of a family (bomber/pod/ground/boss) gives a Combined Dossier (+8% to the whole family). Boss kills (4 superweapons, CLAUDE.md) always drop; the Harvester and Bulwark need a nearby ground unit to retrieve it. Lore text is the designer's own prose (the designer WRITES per ROADMAP L733), each entry 3 sentences, the style rule applied. Text slots also include the four superhuman canon entries (ROADMAP L639) as "recovered at a human site" entries.
- **Real science.** Foreign Materiel Exploitation (the Akutan Zero, 1942; the MiG-25 Belenko defection, 1976, which showed it was a high-speed interceptor and not the dogfighter feared). Reverse-engineering collapses the knowledge gap in months.
- **Code grounding.** Enemy kinds are spawned in `spawnEnemy` L5581 and killed in the kill path (`G.stats.killsByType` L3606); add a drop event there. Damage multiplier belongs with `G.mult.dmg` application. A Codex button beside `btnAchievements` (L855), reuse `openAchievements` L1005's pattern. Achievements: add `'analyst'` medals via the L952 array.
- **Build.** M (about 220 lines) plus the designer's writing.
- **TM1.**
- **Tenet check.** Pure buff to the player, gated by playing, and no nerf anywhere.
- **Interactions.** Enemy council (every new enemy kind carries a dossier slot), Intel/Radar (a Data Center speeds analysis by 25%), Achievements (the medals start pointing at lore).

### LORE-4 — RED FLAG: the war is a simulation until it isn't (hidden warfare)

**Pitch.** *"Ladies and gentlemen, welcome to the exercise. Please do not mistake the sky for the sky."* An opt-in start mode. PREP is dressed as a **training exercise**: scripted OPFOR waves ("EXERCISE") of increasing size arrive during the countdown, with a scoreboard, and you build and fight them normally. Your rehearsed doctrine is recorded. At T-0 the banner changes from EXERCISE to **THIS IS NOT AN EXERCISE** and the real fleet, which has read your drills, arrives.

**Player fantasy.** The designer's book: a gamer trained by a video game, the war only comes at the end. You are *already* good when it begins, because you practised for it.

**Clerk's Annex**
- **Mechanic.** Three drill waves in PREP (drone, bomber, ground) at 0.4x, 0.7x, 1.0x of the doctrine's phase-1 mass. They die easily and are *real units* (your buildings and units are real). Score S = kills/enemies + survival time. A high S gives a bonus: +1 research tick per drill (a free node-fraction) and +10% starting IC. The twist: `G.stats.killsByType` and `G.stats.built` from the drills seed the fleet's `threatWeight` so the real fleet over-represents what you drilled against (mirror counter-doctrine). Brutal and fair: a scarier enemy, not a nerf.
- **Real science.** Red Flag exercises (the USAF, from 1975, found that a pilot's first ten sorties carried most of the combat losses, so drill them out), and the Millennium Challenge 2002 lesson: the OPFOR commander (Van Riper) won with motorcycle couriers and swarming small boats because the other side's doctrine was legible. A drilled, predictable defender becomes the target. That is your doctrine: be unpredictable, or be countered.
- **Code grounding.** `INTENT` L5119 (add `exercise:true` intent preset), `G.prepTimer` and the PREP block, `doWave` L5533 for the drills, `banner` L10335 for the switch, `threatWeight` in `acquireEnemy` (ROADMAP v4.5.0). The `SCEN` scenario editor exposes the toggle.
- **Build.** L (about 600 lines): drill scripting, a scoreboard, a doctrine ledger and the weighting hook.
- **TM1.**
- **Tenet check.** The player is stronger at T-0 (free IC, free research); the cost is the enemy reading the rehearsal.
- **Interactions.** Enemy council (the adaptive counter), Onboarding (it is the tutorial that is also lore), Air/Ground (drills teach doctrine).

### LORE-5 — THE WIRE & THE CHRONICLE: intercepts that predict, an after-action that remembers

**Pitch.** *"A headline is a forecast with a byline."* A live **Wire**: one-line intercepted fleet transmissions and war-correspondent headlines. Each is derived from state, and the first ones are *accurate forecasts* of the next phase (bunker-busters inbound, flank landing in 40s). Better intel buildings sharpen it. At the end, the **Chronicle** turns the war into five dated paragraphs generated from the real log.

**Player fantasy.** You read the war like a newspaper that is always half a minute ahead of the sky.

**Clerk's Annex**
- **Mechanic.** A ring buffer `G.wire[]` of beat-objects `{t, tag, text, conf}`. Sources: fleet stance changes (`tickFleetStance` L5999 and `enterInvPhase` L6033, 20-45s warning), the flagship, `doSkyfall` L5206, boss spawns (`spawnSuper` L5269). `conf` (0-1) = f(radars, Comms Towers, Data Centers); at low conf the line says "unconfirmed" and may be wrong 25% of the time (the player must weigh it). At `end()` L10345, a Chronicle composes five paragraphs from `G.stats` (opening, first loss, turning point = max single-minute `G.kills` jump, price paid, epilogue from `stats.society`/LORE-2 resolve). Exportable via the existing share/download path.
- **Real science.** SIGINT and indications-and-warning (the Cold War "Watch Condition" system): warnings are probabilistic, and the *cost of a false alarm* is what makes intelligence a decision, not a readout.
- **Code grounding.** `banner` L10335, `toast` L10266 as render; `end()` L10345 and `downloadAARReport` L10725 for the Chronicle; `aarSum` L10473. This is society slice 2 (forecast-sharpening intel buildings, Article IV.4) delivered in TM1 form.
- **Build.** M (about 280 lines).
- **TM1** (the full society-intel layer is TM2).
- **Tenet check.** Information is a power-up, never a restriction.
- **Interactions.** Society council (intel buildings), Enemy council (every phase transition should emit a wire), UI council.

## 4. Hot Take — LORE-H — THE INVOICE: victory is the trigger

**Pitch.** *"You broke the fleet. Congratulations. Now check who gave you the gun."* Today the fleet withdraws and you win (v4.6.0). I propose that if you win *too well* (Resolve <15 at the break, LORE-2, fleet 95% destroyed) you do not win. The benefactor (ROADMAP L405, the political pawn twist) collects. The same match continues into a five-minute **Act II**: the patron's single **Auditor** descends, a boss that cannot be fought with your new toys, because your toys were *licensed*.

**Player fantasy.** You become the thing you fought. The game's thesis is that you turned into a monster, and the monster is now a threat to its maker.

**Clerk's Annex**
- **Mechanic.** At `startWithdraw` (L5990), if `G.resolve < 15` and `G.kills/fleetMax > 0.95`, replace the victory with `G.act2 = {t:0}`. An **Auditor** (a boss, `spawnSuper` L5269) arrives with the Benefactor's own exotic loadout: 2,600 HP x `G.diff`, armor 30, a **research-reversal beam** that sets ONE researched node to "revoked" for 60s per hit (its units go inert, never deleted; they return at once if the Auditor dies). Ending: kill the Auditor and the true win fires. No Auditor kill in 300s and the patron "accepts your compliance": a second draw ending. Revocation is temporary and answerable by killing the Auditor, so it is a boss mechanic and not a denial of toys.
- **Real science.** Patron-client wars (US-Mujahideen 1979-89: arms supplied against a common enemy become a regional problem the supplier then fights). SCI-FI: remote licence-revocation is possible only because the gifted tech is *dependent on the patron's signal*. Internal logic with Article V: a thrown friend implies a thrower; a gift implies a ledger.
- **Code grounding.** `startWithdraw` L5990, `end()` L10345 (add `'compliance'`), `spawnSuper` L5269, `RESEARCH` L3502 (a `revoked` flag read in unit-spawn gating). `G.dangCap` ties in as the "price of the licence".
- **Build.** L (about 700 lines for the Auditor, the revocation layer and the Act II timer). A TM2 extension makes the patron a full second faction.
- **TM1** (Act II slice); **TM2** (patron faction).
- **Tenet check.** Difficulty is *an enemy being overwhelming*. The revocation is bounded and reversible, and no hard cap is introduced.
- **Interactions.** Enemy council (boss design), Research/Tree council (the revoked flag), every council with exotic units.

## 5. Alliances sought

- **Enemy / Fleet council**: Resolve (LORE-2), the Auditor (LORE-H) and dossier slots (LORE-3) all need new enemy behaviours. I supply the reason, they supply the teeth.
- **Space council**: the President wants space to matter. Orbital kills move Resolve hardest, so a space fleet earns *story weight*.
- **Society council**: Charter dials (LORE-1) and the Wire's intel buildings (LORE-5) are their slices 1 and 2.
- **UI / Onboarding**: Red Flag is the tutorial that carries a plot.
- **Research/Tree council**: Charter dials and Revoked flags both live on `RESEARCH`.
