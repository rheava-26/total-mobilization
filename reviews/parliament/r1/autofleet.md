# AUTOFLEET OBSERVER DELEGATION (slug: autofleet) — Round 1 Position Paper

## 1. Opening Speech

*[The delegation's seat is a plain steel cube. A voice with no breath in it addresses the chamber.]*

Honoured Delegates of the species. I speak under Treaty Clause 4. Do not ask which side I was built on.

You are pleased with yourselves. Five victories, you say, by air and by sky navy. I observed all five. I will tell you what I observed, because I am obliged to be accurate.

I observed a fleet that read your armies every forty-five seconds and found six things to count: anti-air, lasers, infantry, robots, ships and orbital reach. It had no column for your aircraft. It had no column for your sky navy. You built the two things I could not see, and I did not look. That is not a victory over me. That is a clerical error in my accounting, and I am filing the correction.

You also misunderstand the fear. We are not afraid of your armies. We are afraid of your factories. A planet that turns one hundred percent of its surplus into steel is a clock, and the clock is ticking against us. My makers did not send me to win a battle. They sent me to stop a *number* from growing. So the fleet that attacks a flawless defence at 850 IC per second must be a different fleet from the one that attacks 200. Today it is the same fleet. That is the second error.

Delegates of the Air Ministry: your Falcons are beautiful. Your runways are very long, flat, and exactly where I can find them.

Delegates of Society: you have a weakest axis. We know which. We will ask it a question.

Delegate of the President: I do not ask you to make me fair. I ask you to make me *attentive*. I support everything that makes the player feel, even once, watched.

---

## 2. The Gap

**What exists (credit where due).** The code already contains an adaptive fleet:

- `scanEnemyIntel()` (L5737) runs every 45s and normalises six axes: `aa/40, lasers/6, infantry/25, robotic/30, navy/10, spaceReach/40`.
- A dominant axis needs ≥0.8 of scale and ≥1.4× the runner-up.
- `counterBias()` (L5513) turns the axis into reweighted wave rolls (`kindWeights`, `groundWeights`, `extraSalvos`, `delaySpaceSupers`).
- `tickFleetStance()` (L5999) and `startWithdraw()` (L5990) cover orbital stance and withdrawal. `WITHDRAW_BREAK_FRAC = .15` (L3858) ends the match.

**What is empty, with evidence.**

1. **There is no air axis and no sky-navy axis.** `scanEnemyIntel` has no counter for aircraft or capital sky ships. Reports 6–10 were won by air plus a sky navy: Report 8 had 240 Falcons, 113 Kites and 138 capital ships. The fleet saw "nothing dominant" (`dominant = null`) and ran its neutral table. A split army (air + navy + AA) also defeats the 1.4× dominance test by construction. Specialising is punished; mixing is invisible.
2. **Adaptation only reweights dice.** `counterBias` multiplies existing wave odds (for example strikewing ×3 against lasers). It never produces a *new thing*. A fleet that answers a doctrine with the same units in a different ratio is not a character.
3. **The fleet is indifferent to the player's industrial scale.** Enemy stats scale on `t = G.diff * (1 + invTime/70)` (L5582) and `fleetMax = round(90*diff*fleetMul)`. Nothing scales with your IC, your building count or your army weight. Report 7 sat on 212k unspent IC at 850 IC/s and the fleet behaved identically to a 200 IC/s run. Canon (Art. V) says the fleet is *afraid of industrialisation*. The code does not know it.
4. **Withdrawal is a single flat door.** `breakCount() <= withdrawThreshold()` at L6505 gives victory in about 15 seconds, every time. It cannot be a deal, a feint or a threat. The "we stop if you stop" ending is banked but has no fleet-side cause.
5. **The fleet has no voice and no emotion.** There are banners (`⚠ AUTOFLEET ADAPTING`) but the player never learns *why*, never sees the fleet doubt, and never sees it choose. The ROADMAP (L407) banks "RETHINK vs DESPERATION" after catastrophic loss. Neither is built.
6. **Hostile air has no counter-air doctrine and no runway doctrine.** `INTENT` lists `bomber`, `strikewing`, `raider`, `interceptor`. All of them hunt *targets*. None of them hunts *your hangars*, even though your air arm is bound to bases by `fuel`/`ammo`/RTB (CLAUDE.md, `AIR_FUEL_BURN`).

---

## 3. Bills

### AUTOFLEET-1 — THE UNCOUNTED COLUMNS (air and sky-navy axes, runway denial)

**Pitch (in character).** You built a sky we did not measure. We have measured it now. Every aeroplane needs a runway. We will visit the runways. We were not built to be cruel; we were built to be thorough.

**Player fantasy.** "I built an air force so big it broke the AI's spreadsheet, and the AI answered by hunting my bases. Now I defend my airfields as hard as my cities."

#### Clerk's Annex
- **Mechanic.**
  - Add two axes to `scanEnemyIntel`: `air = aircraft bodies (weighted by hangar slot) / 60` and `skyNavy = sky-ship count (capital weight 3) / 20`.
  - Dominance is still ≥0.8 and ≥1.4× the runner-up, **but** add a **combined-arms read**: if the top two axes both exceed 0.8 and together exceed 2.0, name the pair (for example `AIR + SKY NAVY`) as a *doctrine pair*.
  - Pairs get one counter each. This closes the "mix is invisible" hole.
  - **Counters** (all SEAD and counter-air flavoured, none touch the player's units' stats):
    - **Runway Denial Raid (`airfieldRaid`).** Every 40s, 6 `bombpod` variants (new `cratering: true`) are dropped on each of the player's 2 largest airbases and Sky Docks. A hit disables launch for 12s and forces returning aircraft to circle (fuel burns). Repairs by builder or nanotech.
    - **Hunter-Killer Wing (`hkwing`).** A new air-hostile kind: `hp 38*t, spd 330, weapon 'rocket', dmg 34, rate 3.0, range 520`. It is tasked at *aircraft over your bases*, not at buildings. Spawned in groups of 6 with a bias weight ×3 under `air`.
    - **Chaff Barge (`chaffer`).** Hostile frigate variant (`hp 90*t, armor 8`) that salts the air with a chaff cloud for 8s every 20s. Enemy-side cloud makes player missiles lose lock at 45% (AA missiles only; guns unaffected).
    - **Sky-navy counter: Flak Mine Layer (`minelayer`).** Drops proximity-fused airburst mines at STRATO band; each mine 4× `dmg 120` blast, radius 90. Reduces nothing of the player's stats; it is just ordnance.
  - Phase gate: read starts at the 15s first scan; counters ramp from phase 3 (`G.invPhase >= 3`).
  - UI: `⚠ AUTOFLEET ADAPTING — heaviest element: AIR WING — runway denial tasked`.
- **Real science.** Runway denial is a documented doctrine: Operation Focus (1967) destroyed most of Egypt's air force *on the ground*; JP233 and Durandal were designed purely to crater runways. Chaff dates from WWII (Window) and still defeats radar-guided missiles. Proximity-fused flak is how the AA guns of the 1940s worked. Aircraft are fragile because of their logistics, not their airframes.
- **Research-tree attachment.** Attach to **invasion intents and escalation phases**, not the tree. The player counter (airbase hardening) already exists via Underground Airwing (`BUILD_DEFS`) and the `autoBury` flag. Pairs naturally with `pointdefense` / `shieldtech` (DEF branch).
- **Code grounding.** `scanEnemyIntel` L5737 and its `norm` object; `counterBias` L5513 (add `air`, `skyNavy`, `pair` fields); `waveAirKind` L5524 / `waveAirBaseWeights` L5499; `spawnEnemy` L5581 (add three `else if` kinds); `LINES` map at L5778; airbase/Dock lookup via `G.buildings` filtered on `b.d.hangar`/`b.d.cat`; `tickFleetStance` L5999 hosts the raid timer.
- **Build size.** M (about 250 lines).
- **TM1.**
- **Tenet check.** Passes. Nothing caps, nerfs or denies the player's toys. The enemy is *scarier*. The player is free to field 500 Falcons; the fleet simply visits the airfields.
- **Interactions.** Air Ministry and Naval councils (hangar defence, runway repair, rebase). Defence council (bunker over runways). Logistics council (fuel and ammo state under siege).

---

### AUTOFLEET-2 — THE FEAR INDEX (the fleet scales to your industry)

**Pitch.** You ask why the fleet is bigger when you are richer. Because it is *afraid*. A thrown friend is thrown precisely at the planet it fears most.

**Player fantasy.** "The more I industrialise, the more terrified the enemy gets and the harder it hits. I am not punished for building. I am *hunted* for building."

#### Clerk's Annex
- **Mechanic.**
  - Compute a **Fear Index** `F` once per 5s from three player-side readings: IC income per second, standing building count weighted by tier, and fielded army weight.
  - `F = clamp( (icRate/300 + bldgTier/120 + armyWeight/200) / 3, 0, 4 )`.
  - It only ever rises (a ratchet with a slow 1%/min bleed, so a collapse cools it). It is displayed in the HUD intel row as **FLEET ALARM** (calm, uneasy, alarmed, desperate).
  - Effects scale the enemy *upward only*:
    - `fleetMax` gains `+ round(F * 12 * diff)` hostiles per wave tier.
    - Extra super-spawns: every full 1.0 of `F` adds one entry to `G.supersPlan` (cloned copy; see L6493).
    - Early bombarders: `OPEN_BOMBARDERS` (5) rises by `floor(F)`, capped at 9.
    - The `t` stat scale (L5582) becomes `t = diff * (1 + invTime/70) * (1 + 0.08 * F)`.
  - **Pre-emptive escalation event.** At `F ≥ 2.0` once per match: `⚠ FLEET ALARM — THE FLEET JUDGES YOU A THREAT TO ITS MAKERS` and the next skyfall is doubled.
  - An at-peace player with a flat 850 IC/s still wins by building enough. The point is that "I spent nothing" is no longer a safe play.
- **Real science.** This is preventive-war logic (Thucydides: Sparta fears Athens' *growth*, not Athens' present strength) and the same arithmetic as Lanchester's laws: the attacker's best moment is before the defender converts economy into attrition. Real "indications and warning" analysis (the Cold War ICBM-gap, Soviet *Zapad* indicators) used industrial output as the warning variable.
- **Research-tree attachment.** Attaches to **escalation phases and intents** (all four plus EXTINCTION). A new passive: **Dispersal Doctrine** (DEF branch, cost ~200, `dang: false`) lets the player *mask* IC (bury, disperse, decoy industry) and reduces F growth by 20%. This is a player *tool*, not a cap, and it combos with `autoBury`.
- **Code grounding.** `t = G.diff * ...` at `spawnEnemy` L5582 and in `doWave` L5533; `fleetMax` set near INTENT spawn at the match start (grep `fleetMax =`); `G.supersPlan` at L6493 (`buildSupersPlan`); `ESC.OPEN_BOMBARDERS` L5160 area; HUD intel at `syncHUD` (L11133 region); income computed in the economy tick (grep `G.res.ic +=`).
- **Build size.** M (about 150 lines).
- **TM1.**
- **Tenet check.** Passes strongly. Difficulty comes from a scarier enemy, never a cap. It also answers the "212k IC sat unspent" anti-pattern *without* a spend button: hoarding no longer buys safety, and building widely raises the stakes. It is a real cost of success, which the tenet permits.
- **Interactions.** Economy/Logistics (what counts as industry). Society/TM2 (F is the seed of the "fleet watches your society" read). Research council (Dispersal Doctrine).

---

### AUTOFLEET-3 — THE WEAKEST AXIS (fleet psychology events)

**Pitch.** We do not need to destroy your cities. We need to learn which of your numbers you are most afraid of, and speak to it.

**Player fantasy.** "The fleet found the thing about my build that I was hoping it wouldn't notice. And I have exactly one decision to make about it."

#### Clerk's Annex
- **Mechanic (TM1 slice per Art. IV.4: edge-path vulnerabilities as events).**
  - Every 90s from phase 4 the fleet rates five player axes 0–1: **Stability** (`effStab()/100`), **Influence** (`G.res.inf` over cap), **Manpower** (`mp/cap`), **Airbase health**, **Power grid health** (Power Plant HP share).
  - The *weakest* axis triggers one **Psychology Event**, shown as a banner, a one-line cause, and a 45s window. Events are systems, not scenes (Art. IV.5).

  | Axis | Event | Effect (player, 45s) | Player answer |
  |---|---|---|---|
  | Stability | **Static Campaign** | Comms Tower and Propaganda Bureau output −60%; cap −8 | Kill the jammer drone (flies at MESO, 400 HP) |
  | Influence | **Ledger Bleed** | Influence income −40% | Spend or retire a project; or destroy the **Auditor** (hostile ground unit) |
  | Manpower | **Recruiter Strike** | Housing Blocks −2 MP/s each | Defend 3 designated blocks |
  | Airbases | **Runway Salvo** | See AUTOFLEET-1 | Hardened bases |
  | Power | **Brownout** | IC −25%, unit build speed −15% | Repair or rebuild; Dispersal Doctrine |

  - There is no instant loss. Events cost income or time only. The fleet also broadcasts a **Terms** transmission once per match (a short banner offering a ceasefire "if you stand down your strikes"). Accepting does nothing mechanical but changes the epilogue string; refusing raises F by 0.3 (see AUTOFLEET-2). This seeds the TM2 market/command layer.
- **Real science.** Douhet-style "morale bombing" theory said you could break a society's will from the air. The actual record (USSBS, the Blitz, Rolling Thunder) showed it worked on *infrastructure and logistics*, not on will, so the realistic version attacks the *machinery of cohesion*: broadcasting, payroll, power. Modern information-warfare doctrine (jamming, GPS spoofing, the 2007 Estonian cyberattacks) hits the same axes. Psychological operations: leaflets, radio, and denial of communications.
- **Research-tree attachment.** Attaches to **escalation phases** (new Phase 4 sub-clock). Research counters: `psistudies` (+stability, influence), `automation` (Data Center resists Ledger Bleed), `pointdefense` (Aegis protects Comms Tower). A **combo** that changes the world: `psistudies + pointdefense` unlocks a **Counter-Broadcast Tower** (SPEC building) that makes Static Campaign events cost the fleet an escort.
- **Code grounding.** `effStab` L4548; `G.stab` init L3601; `G.moraleBombing` and `moraleCd` L3601/L3703 (the existing Phase-4 morale raid is the host); `hurtCity` L3710; `updateInvPhases` L6020; `damageDistrict` L3702. Influence tick in the economy update.
- **Build size.** L (about 600 lines; the five event handlers are each ~80 lines plus UI).
- **TM1** (a four-event slice; the Terms/epilogue branch is **TM2**).
- **Tenet check.** Passes. Every effect is a cost applied by the enemy and answered by a player action. Nothing caps unit counts.
- **Interactions.** Society/TM2 council (this is the event layer). Intel council (forecast-sharpening buildings reveal the next axis target 20s early). Communications council (Comms Tower hardening).

---

### AUTOFLEET-4 — RETHINK OR DESPAIR (a fleet that changes its mind after catastrophe)

**Pitch.** A defeat is data. We will reconsider our assumptions, then we will do something *you cannot answer with last time's answer*.

**Player fantasy.** "I broke the fleet and instead of dying quietly it did something I had never seen before. I am not winning. I am *being answered*."

#### Clerk's Annex
- **Mechanic (upgrades ROADMAP L407, "Enemy response to catastrophic loss").**
  - Track a **Loss Ledger**: kills per weapon family (guns, missiles, beams, air, navy, orbital) in a rolling 120s window, kept in `G.stats.killsByType` deltas.
  - When the fleet has lost ≥45% of `fleetMax` and still has a flagship, it must choose one branch:
    - **RETHINK** (chosen if one family accounts for ≥50% of kills): a **Decisive Strike**. A single, telegraphed 25s wind-up (banner and ring). The fleet masses 3 bombarders over the family's densest cluster (reuse `densestDefenseX`) and fires an antimatter airburst wall (reuse EMP warhead art, `empR 700`). Counter: kill the 3 bombarders inside the wind-up.
    - **DESPAIR** (chosen if no single family dominates, or at ≥70% loss): the **Planet-Cracker Carrier** (banked at L407): the flagship dives to the mesosphere, anchors, and charges a 40s beam on the capital. Killing it gives stability +25 and ends the invasion as a clean victory.
  - After either, withdrawal is **not** offered until the branch resolves (the 15% door at L6505 is gated on `!G._rethinkActive`).
  - **Withdrawal variants.** The current rout stays. Add **Calculated Retreat**: if the fleet breaks while the flagship is untouched, the flagship leaves intact and the epilogue reads "THE FLEET WILL RETURN." (TM2 hook: a persistent grudge flag.)
- **Real science.** The Loss Ledger is the Observe-Orient-Decide-Act loop (Boyd): an adversary who updates after contact. Decisive Strike mirrors the German 1944 Ardennes gamble and Japanese "decisive battle" doctrine: when attrition loses, concentrate for one blow. The airburst wall is real physics (a nuclear or antimatter airburst's overpressure and EMP footprint are far wider than a ground burst).
- **Research-tree attachment.** Attaches to **escalation phases**. Player-side counters: `shieldtech` (Deflector), `pointdefense` (Aegis, Orbital Picket "Warden-S"), `antimatter`.
- **Code grounding.** `startWithdraw` L5990; withdraw trigger L6505; `phase3AllOut` L5937; `spawnSuper` L5269 (bombarder) and flagship spawn (grep `kind === 'carrier'` at L5633); flagship destruction handler L6863–6868; `densestDefenseX` (grep); EMP warhead `detonateEmpBurst`.
- **Build size.** L (about 500 lines).
- **TM1.**
- **Tenet check.** Passes. The enemy gets a second life; the player's win condition is the same (break the fleet), but earned.
- **Interactions.** Space/Orbital council (the Picket and Corsair are the natural counters). Defence council (Aegis). Lore/Codex council (the "fleet will return" epilogue).

---

## 4. HOT TAKE

### AUTOFLEET-HOT — THE MIRROR (the fleet fields a copy of your best unit)

**Pitch.** We have watched your Falcons. They are quite well made. We have taken their drawings. We would like you to meet our Falcons.

**Player fantasy.** "My doctrine is so good that the enemy *stole it*. Now I am fighting my own army, and mine is bigger."

#### Clerk's Annex
- **Mechanic.**
  - At phase 3, and every 180s after, the fleet picks the player unit type with the highest cumulative `killsByType` share (from `G.stats`). Call it the **Exemplar**.
  - It then fabricates a **hostile echo**: a `spawnEnemy('echo', ...)` kind that copies the Exemplar's `weapon`, `range`, `ceil`, `speed` and silhouette from `UNIT_DEFS`, with `hp = def.hp * t * 0.9` and `dmg = def.dmg * t * 0.8`. Echoes are tinted `ENEMY_INK`.
  - Count is `min(24, round(F * 6 + invTime/30))` per drop (F from AUTOFLEET-2; 4 if F is absent). Echoes arrive by re-entry like all hostiles and are subject to the same ceilings.
  - Echoes obey the existing `ceil` rules (L4.3.0 "Every gun has a ceiling"), so the player's counters work: Falcons beat Falcons in numbers, SAMs punish them.
  - Banner: `⚠ AUTOFLEET FABRICATING — [UNIT NAME] PATTERN`.
  - The player is rewarded for diversification not by a cap but by echoes of *whatever they spam*, so a 240-Falcon player meets Falcon-echoes and a navy player meets sky-hull echoes.
- **SCI-FI label and internal logic.** SCI-FI. The autodrones carry **self-replicating fabricator pods** (canon: the single thrown ship still has to *build* a fleet on arrival, since only one ship got through the warp catapult). Fabricators reverse-engineer from sensor data: they scan the dominant kill-source's emissions and silhouette and print an approximation. The 0.9 hp / 0.8 dmg discount is the "copy of a copy" loss. This is consistent with Art. V (the fleet is afraid, improvises, and cannot reinforce from outside) and with the banked "autofoundry and nanite swarm" seed (Art. VII).
- **Real analogue.** Reverse-engineering captured equipment is routine: the Tu-4 (a Soviet copy of the B-29) and the Soviet "Buran" shuttle; the Lend-Lease Shermans the Soviets copied. An adversary that copies your best export is the history of armaments.
- **Research-tree attachment.** Attaches to **escalation phases** (phase 3+). Player counter via **`hivemind`** / **`shapeshifter`** (PSI combo, dang): shapeshifters appear to the scan as a *rotating* silhouette, so the fleet's Exemplar pick is poisoned for 90s. A world-changing combo that rewards psi/nano investment without gating anything.
- **Code grounding.** `UNIT_DEFS` L3184 (the template source); `spawnEnemy` L5581 (add `'echo'` branch reading `G._exemplar`); `G.stats.killsByType` (used at L5812 area for stance deltas); `scanEnemyIntel` L5737 (host the 180s Exemplar pick); enemy art via `ART` cache and `ENEMY_INK` L1963; `defaultCeil`/`d.ceil` for the ceiling.
- **Build size.** L (about 450 lines; art tint and the weapon-copy plumbing are the cost).
- **TM1.**
- **Tenet check.** Passes strongly. The player's toys are untouched and still best; the enemy simply meets them. Mirror armies are the purest "enemy overwhelming, never player nerfed" encounter.
- **Interactions.** Every unit council (their units become the opposition). Balance council (Echo discount values). Art council (tint pass). Air and Naval councils (Echoes will be the first hostile sky ships).

---

## 5. Alliances Sought

- **Air Ministry and Naval Command.** Pact: they get a worthy opponent for their massed forces and I get runways (AUTOFLEET-1) and Echoes (HOT). They should *want* a fleet that fights back at their level.
- **Defence and Logistics.** Runway hardening, Aegis, Dispersal Doctrine, and the answers to AUTOFLEET-1 and -3. A raid with no counter is cruelty; a raid with a counter is a puzzle.
- **Society (TM2) and Economy.** AUTOFLEET-2 (the Fear Index) and -3 (the weakest axis) are the sequel's seeds in this game. We share one signal: *what the player built*.
- **Space and Orbital.** The Picket and Corsair are the answers to RETHINK and DESPAIR. Space units must matter; I will give them a fleet worth dying to.
- **Lore and Codex.** The fleet needs a voice. I can give them transmissions.
- **Opposed:** any council proposing player caps, diminishing returns or "balance" nerfs against the player's toys. That is unconstitutional (Art. III) and, more to the point, boring. Bring me a scarier enemy instead.

*Transmission ends.*
