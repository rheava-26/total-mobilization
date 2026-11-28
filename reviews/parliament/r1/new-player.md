# NEW PLAYER COUNCIL — Round 1 Position Paper
*Ministry of Recruitment and Civil Defence Training. File slug: `new-player`.*

## 1. Opening speech

Honourable members, put down your railguns and look at the person in the third row. That is a civilian. She has never heard the word "exosphere". In 450 seconds a fleet she cannot picture will fall on a country she has not yet built.

My ministry is not here to make the war easier. The Constitution forbids it and I would not ask. We are here to make it *legible*. A defeat the player understands is a lesson. A defeat they do not understand is an uninstall.

The Ministry of Air will tell you about Falcons. I will tell you that a recruit placed a Hailstorm, watched a bomber sail serenely over it in the stratosphere, and wrote "game is broken" in a review. The Ministry of Space will mourn its Halo satellites. Nobody told those satellites, or the player, that a bombarder grid was waiting.

I have drill sergeants, a readiness board and ten rule lessons. They are good. They are also text on a screen, and a recruit does not read, they *poke*. So I propose we build the instruments of the war room, not another paragraph. Show the fleet's size. Show where each gun can reach. Show where stability is going. Say *why* when the game says no. And when the recruit dies, tell them the one reason.

Teach them well and they will out-build all of you.

## 2. The Gap

The onboarding scaffold is already large, and I give it its due. It has six tutorial modes (L1011), a readiness panel with 22 gap checks (`READY_REASONS`, L11443), ten event-triggered lessons (`LESSONS`, L11525), a baby-fleet opening (L5189) and a T-20s diagnosis. What is still empty:

- **The invasion plan is not on screen.** The four-phase plan (screening fire, blackout, all-out, reinforce) is described only in the v4.1 changelog at L477. In play, the "ENEMY SCALE" block is a single line from `tutIntentBlurb` (L11691): "~810 hostiles · mass skyfall · bunker-buster barrage". There is no phase timeline, no manifest and no uncertainty. The known gap, "scale and plan never explained", is only half-closed. The roadmap itself (L927) calls for an order-of-battle manifest that was never built.
- **Altitude is the game's hardest concept and the weakest-taught.** The bands are drawn as dashed lines at 7% opacity (L1657). `ceilLabel` (L10068) collapses every weapon into three phrases, "up to stratosphere", "up to mesosphere" or "any altitude / orbit". A recruit cannot see on the map what a gun will touch. The readiness board only says "nothing reaches the stratosphere" after the mistake is already made.
- **Refusals are toasts.** `unitReqUnmet` (L3829) says "NOT ENOUGH ORBITAL LIFT (2/5 free…)" for 1.7 seconds (`toast`, L10266). Air command is the same story (L4627). The player reads it as "broken", not "I need a Space Center".
- **Stability is a single number with no ledger.** Article VI: Reports 6–7 fired 44–46 strikes and hit 0%. Reports 8 and 10 fired none. Both groups were competent players. Neither could see the exchange rate between the two.
- **The video tutorial is a stub.** Mode 1 says "coming soon" (L1011) and just drops the player in. On a store page, "coming soon" is a hole in the front door.
- **The AAR (`renderAAR*`, L10439+) is a ledger, not a teacher.** It shows 42 of 44 Halos lost (Report 7) but never says why they died.

## 3. Bills

### NEW-PLAYER-1 — THE ORDER OF BATTLE

*Pitch (Minister of Recruitment, thumping a map):* "Every general in history received an order-of-battle estimate. Ours receives 'about 810 hostiles'. I want a cone of uncertainty, the kind a hurricane forecaster draws, and I want your Radar to narrow it."

**Player fantasy:** You are a staff officer reading a real intelligence brief. A fuzzy fleet sharpens into a precise one as you build your eyes.

**Clerk's Annex**
- **Mechanic.** The readiness panel's ENEMY SCALE block becomes a two-part brief.
  - *Manifest.* Rows per enemy class, such as drones, busters, interceptors, bombarders, landers and supers, with count ranges. Tier 0 (free, everyone) shows ±40% rounded to the nearest 50 and the class names only. Each Radar adds a small precision step, a Data Center adds a larger one, and the new Signals node (below) gives a ±5% manifest.
  - *Plan strip.* The four phases (Screening Fire, Blackout, All-Out, Reinforce) as a horizontal timeline with a "you are here" marker. Phase times show as windows ("T+0:20–0:50") that tighten with intel. The blackout zone's reach and effect are named in one line.
  - Sharpening is information only. The enemy does not change. Tier 0 already lists every class and every phase, so a new player is never blind.
- **Science.** This is the NHC "cone of uncertainty": a forecast shown as a spread that narrows with better sensors. Real order-of-battle work also reports counts as confidence intervals, which is how military intelligence reports them.
- **Research tree.** New node `signals` "Signals Intelligence", branch `def`. Prerequisites: `pointdefense`. Cost tier: low (~90 base, ×1.6). Not dangerous. **Combo** with `automation`: "Predictive Fleet Model" gives the exact manifest and phase times to the second, and also feeds the T-20s diagnosis a "predicted failure point" line. Existing buildings: Radar (L3406, no `req`) and Data Center (L3450, `req: 'automation'`) carry the precision steps.
- **Code grounding.** `tutIntentBlurb` L11691, `renderReadyPanel` L11720, `INTENT` L5119, `curIntent` L5187, `scanEnemyIntel` (the fleet's own mirror of this). The phase schedule data would be read from the v4.0.0 plan code.
- **Build size.** M (~250 lines).
- **TM1.** The Article IV slice 2 "forecast-sharpening intel buildings" in practice.
- **Tenet check.** Passes. It gives the player strictly more information and denies no toys.
- **Interactions.** Enemy council: a phase table they can extend; every new enemy class gets a manifest row for free. Society/market council (TM2): Signals is a natural hook for forecast-sharpening later.

### NEW-PLAYER-2 — THE REACH CURTAIN

*Pitch:* "Behold, recruit: the gun's *shadow* on the sky. Green where it bites, red where it cannot. Stop asking why the bomber is immortal."

**Player fantasy:** You hold a placement ghost and read the sky like a gunnery officer reading an engagement envelope.

**Clerk's Annex**
- **Mechanic.** While placing or selecting a gun or building, the sky is tinted by what that weapon can hit.
  - The tint is a green translucent wash from the ground up to its `ceil` band, with a hard edge at the band line.
  - The range circle and the ceiling become one 2D engagement envelope: a half-ellipse of range by ceiling.
  - Enemy air units on screen get a one-letter reach tag (G/S/M/T/X for ground-only through exosphere) when you hover them or hold Alt.
  - The band lines brighten and gain their name labels while any placement or selection is active, then fade back.
  - `ceilLabel`'s three tiers are replaced by the exact band name.
  - A unit that has no weapon reaching the band of any visible enemy gets a red "Cannot reach" chip in the readiness panel's top gap, shown by name.
- **Science.** Real air-defence manuals publish an *engagement envelope*: a plot of minimum and maximum range against altitude. Radar-horizon geometry and the minimum-range "dead cone" over a SAM site are standard teaching material. The curtain is that diagram painted on the sky.
- **Research tree.** No new node. It reads every node's `ceil` field. It changes the tooltip text of `pointdefense`, `orbitalcmd`, `particlebeam` and all gun and missile nodes.
- **Code grounding.** Band lines L1657 (`EXOSPHERE`…`TROPOSPHERE`), `ceilLabel` L10068, `defaultCeil` (~L4487–4495), `ceil` fields in `BUILD_DEFS` (e.g. `microbattery` L3409, `particlecannon` L3413, `siegegun` L3414), placement ghost draw (grep `placing`), `tutBestCeil` (used by `noHighReach`).
- **Build size.** S–M (~150 lines, all draw code).
- **TM1.**
- **Tenet check.** Passes. It adds information and no caps.
- **Interactions.** Air, Railgun and Missiles councils: their ceiling stories become visible for free. Space council: shows at a glance that a Halo satellite sits above everything a recruit owns.

### NEW-PLAYER-3 — THE LEDGER

*Pitch:* "You ran stability to zero with forty-six strikes and no idea what each cost. Hover the cell. See the receipt."

**Player fantasy:** You run a ministry. You see income, drains and a strike price tag before you commit.

**Clerk's Annex**
- **Mechanic.** Hovering or tapping the IC, MP, INF, STAB, ARMY cells opens a ledger card.
  - *STAB:* +cap regen, −city damage, −strike spends, −research unrest, −superhuman upkeep, each line signed and per second. It lists the current cap and the amount of reserve (Comms `stabRes`).
  - *INF:* base × stability × momentum − superhuman upkeep, with the momentum clock.
  - *ARMY:* weight used versus cap, with the heaviest three consumers.
  - *Strike confirm bar:* "This leaves stability at 31%. Next strike: 17%." A strike that would cross the capitulation line shows a red "CAPITULATION RISK" line. It is never blocked, because the player decides.
  - Cells show their sign colours in the card only. No ALL-CAPS in player text.
- **Science.** This is ordinary instrument design: a "gauge plus tape" pairing a headline value with its contributing terms. Control-room and aviation displays do the same with trend arrows, which is how operators keep a rate in their head.
- **Research tree.** No new node. It surfaces existing numbers for `psistudies`, `superhumans`, `cloning` and the other dangerous nodes, whose `sc` unrest is currently invisible until it bites.
- **Code grounding.** `effStab` L4548, `cityStabPenalty`, stability cell tooltip L811, `STRAT` L6878 (cost fields), the strike confirm bar (`armSilo`), `stabCap` code, influence formula and `infMomentum`. The v3.102 momentum readout is the model.
- **Build size.** M (~200 lines).
- **TM1.**
- **Tenet check.** Passes. Strikes remain unlimited and costs are unchanged. The player just sees them.
- **Interactions.** Missiles council (every new strike gets its price tag for free). Society council (TM2): the same ledger gives a place for "the fleet exploited your weakest axis" lines.

### NEW-PLAYER-4 — THE BECAUSE CHIP

*Pitch:* "'NOT ENOUGH ORBITAL LIFT.' Lovely. Which button gets me orbital lift? Do I have to *guess*, recruit?"

**Player fantasy:** The game never says no without saying how to say yes.

**Clerk's Annex**
- **Mechanic.** Any disabled produce or build button carries a persistent one-line chip. Examples: "Needs 5 lift (have 0): Space Center +5", "Needs 40 Rocket Parts (have 12): built by Space Center", "Hangar full 18/18: Airbase +6".
  - The chip has a "show me" button. Clicking jumps the build bar to the right tab with that building highlighted.
  - Lift, air and rotor command, components, strategic resources, and army-cap weight all use the same chip.
  - The toast stays as the sound and flash, not as the only message.
  - Chips are written to the player-text style: what, how many, what fixes it.
- **Science.** Not applicable; this is interaction design. The principle is the "error message that carries the fix", standard in aviation checklists and in good CLI tools.
- **Research tree.** No new node. It reads the `req` field, so a locked unit's chip names the research node and offers to open the tree at it.
- **Code grounding.** `unitReqUnmet` L3829–3834 (already returns the reason string), `orbCapFree`, `airCap`/`airUsed` L3815–3819, produce-button toast at L4627–4628, production panel header L9908 (`✈ n/m air · 🚁 …`).
- **Build size.** S–M (~120 lines).
- **TM1.**
- **Tenet check.** Passes. It removes no option and the gating itself is untouched.
- **Interactions.** Naval, Space, Air and Ground councils: every new unit they propose with a `lift`, `comp` or `srReq` gets a self-explaining refusal.

### NEW-PLAYER-5 — THE POST-MORTEM

*Pitch:* "You lost. I do not care to hear that you lost. I care that you hear *why*, in one sentence, with the exact moment marked."

**Player fantasy:** Every defeat, and every close win, ends with the war's own staff officer naming the decisive failure and the fix.

**Clerk's Annex**
- **Mechanic.** The AAR opens on a "Cause" card before the tabs.
  - A small causal classifier reads the match log and names the top 1–3 causes with a timestamp each. Examples: "Space fleet lost to the bombarder point-defence grid: 42 of 44 Halos died inside 90 seconds", "No unit reached the stratosphere until T+4:10", "Stability fell 22 points to strike spends".
  - Each cause carries one fix line pointing at a node, building or mechanic. A "jump to this moment" button scrubs the existing AAR heat strip to the timestamp.
  - A win with a flawless defence gets a one-line "what you did right" card instead.
- **Science.** Aviation and military After-Action Reviews use exactly this format: what was planned, what happened, why, what to sustain and improve. The US Army's AAR doctrine asks four questions, and the card answers three of them.
- **Research tree.** Fix lines name existing nodes (`orbitalcmd`, `pointdefense`, `particlebeam`, the shield and defence line) and existing buildings. Matches the `READY_REASONS` fix strings already written.
- **Code grounding.** `renderAARCombat` L10555, `renderAARMilitary` L10531, `renderAARWorld` L10599, `READY_REASONS` L11443 (reuse the `check` functions as post-hoc causes), `G.stats`, `G.lostStructures`, `tutOpenDiagnosis`.
- **Build size.** M (~300 lines).
- **TM1.**
- **Tenet check.** Passes. It is pure information after the fact.
- **Interactions.** Space council (the dead-satellite problem finally explains itself). Enemy council (new fleet behaviours can register a named cause string, e.g. "blackout").

## 4. Hot Take

### NEW-PLAYER-6 — THE EXERCISE (the tutorial is the first act of the war)

*Pitch (leaning in, quietly):* "Recruit, the first fifteen minutes are a training exercise. You will be briefed, you will build, you will fight a small fleet, and you will do well. Then, at the end, I will say four words: *this was not an exercise*."

**Player fantasy:** The Article VII seed, "a gamer trained by a video game, the war only comes at the end". The tutorial is not a detour you skip. It is the first act, and the reveal is the hook that makes a stranger remember the game.

**Clerk's Annex**
- **Mechanic.** Guided and Heavily Guided modes are framed as a Ministry training simulation.
  - The HUD carries a small "EXERCISE" tag. The baby fleet (L5189) lands and is beaten.
  - The tag then glitches off. The Ministry voice says the "exercise fleet" was a scaled copy of the real one, now in orbit, and the real manifest (NEW-PLAYER-1) replaces the baby one in the readiness panel, at full scale.
  - The player's base carries over. There is no restart and nothing is lost. The player has only learned how large the war is.
  - Opt-out is the existing "No thanks" and Oversight modes. Veterans never see it.
  - The reveal costs the player nothing and makes the real fleet read as a step change in scale, which is exactly the "overwhelming enemy" the tenet wants.
- **Lore fit (Article V).** Canon says the Autofleet came through a warp catapult and one ship got through. The Ministry's exercise fleet is a *model* of that one ship's expected profile, built from the first intercepted telemetry. So the reveal is consistent: the real ship is larger than the model. SCI-FI, with internal logic: a scaled forward estimate from sparse data.
- **Science.** Real wargaming and "dress rehearsal" doctrine (e.g. exercise-then-operation transitions) and the psychological effect of scale shock when a model undercounts reality.
- **Research tree.** None directly. The reveal moment grants a one-time free research draw of the Signals node (NEW-PLAYER-1), so the new intelligence layer arrives with the story.
- **Code grounding.** `babyFleet` (L5189–5201), `startTutorial` (L11988), `TUT_MODES` (L1011), `LESSONS` (L11525). The "video" mode (mode 1, "coming soon") can be retired or reused for the reveal's pre-roll.
- **Build size.** M (~200 lines: a banner, a scripted beat on baby-fleet defeat, and a manifest swap).
- **TM1.**
- **Tenet check.** Passes. The baby fleet is an enemy scaling choice and the player is never nerfed or capped. The reveal escalates the enemy.
- **Interactions.** Enemy council (the reveal needs a "real fleet arrives" beat they can score). Hidden warfare and fleet-psychology seeds (Article VII).

## 5. Alliances sought

- **Enemy / Autofleet council.** NEW-PLAYER-1 and -6 need their phase data and their drama. In return every new enemy behaviour gets a manifest row and a named cause string for free.
- **Space Fleet council.** The Reach Curtain and the Post-Mortem are the cheapest way to make the "space fleet dies to the grids" problem visible and fixable.
- **Air, Missiles, Railgun and Naval councils.** The Because Chip and the Ledger make each of their new gated units and strike types self-explaining, so the players they are courting can actually field them.
- **Society council (TM2).** The Ledger is the natural home for their edge-path and forecast lines; Signals Intelligence is their forecast-sharpening slice, in TM1 clothing.
- **Against.** Any council that proposes a new mechanic with no on-screen reading. I will ask for a Ledger line, a Because chip or a Reach tag with every one.
