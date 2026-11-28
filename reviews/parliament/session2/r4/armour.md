# ARMOUR CORPS (Ground Bloc) — Maiden Paper, Second Session (`armour`)

## 1. Opening speech

*(The Colonel-General of the Armour Corps climbs the rostrum. The microphone smells of diesel.)*

Mr Speaker. I have read Report 7. The Rifle people lost 232 of 251 men and called it a *tithe*. I call it a *clerical error*. A man is forty-six hit points and a bad attitude. A **Goliath** is 230 hit points, 70 damage a shell and a good attitude, and it does not cry for its mother in a crater.

The airmen will tell you the sky is the only dimension. I admire the confidence of a man who has never had to hold a *road*. The Address says the ground is a line. Good. A line is a **front**, and a front is the one place in this game where the enemy must come to me on foot.

I have a grievance. The Line is one-dimensional, so my armour cannot flank. Fine: it will **hit**. My nemesis, the Land Leviathan, lands once a wave, in the first thirty seconds it is a blister of re-entry plasma, and then it is a landlord. I want to evict it on arrival. I also want my wrecks back. The infantry get replaced; tanks get *recovered*.

Bring the diesel. We'll do the rest.

## 2. The Gap

- **Armour has no job.** `tank` is 120 IC, 110 HP, armor 8 (`UNIT_DEFS` L3186). `heavytank` 230 IC, 230 HP (L3187). `mammoth` 340 IC, 340 HP, armor 22, via Heavy Foundry (L3195). `colossus` 900 HP (L3253). The enemy lands `gtank` at 70 HP × t and `gmech` at 100 HP × t (L5604-5605). My boys fight armour that is weaker than they are, so they are *a wall that stands there*. The Ground Staff's R3 paper says so: "The player's answer is 'stand there.'"
- **Armour is nearly irrelevant in the damage formula.** `hurt()` (L4978) applies `ARMOR_NERF = .55` and, for non-structure targets, `HIT_BUFF 1.3 × 1.9` (×2.7 if armor <4). A 40-damage tank shell does about 98 minus 12 against the Leviathan's armor 22. Armor costs the Leviathan ~12% of the shell. **So the Thermal Field's "up to −50% armour" is worth ~6 points of flat reduction.** The heat bill needs a wall made of *HP and shields*, not armour, or my corps has nothing to contribute to the combination. This is evidence, not opinion.
- **No counter-landing reason exists.** `pickLandingZone` (L5237) picks the least-defended site by `defenseScoreAt` (L5228). Nothing rewards a mobile reserve that is already within reach. The designer's list says ground "felt wonky."
- **Does a tungsten rod still annihilate the ground?** Mostly no. The Zeus unit fires one 95-damage penetrator (`UNIT_DEFS` L3219, projectile `k:'rail'`, `big:true`, L4886, L9166). The Kinetic Lance strike (`STRAT` L6892) is 40 IC, 6 stab with `stab` nulled unless `taboo` (it is conventional), and "splits everything it hits" (`splitKind` L6722). It pierces a *column*, not the planet, and it does **not** sink cities. I cannot verify the strike's blast footprint in a headless run, so the Inspector should. But CLAUDE.md's "single rod annihilated everything" is stale: a Goliath survives it.
- **The enemy anchors are ground targets nobody is built to kill.** `breacher` (L5642, 300 HP × t, armor 12, `fieldR 190`) lives behind a dome. Only splash and beams answer it; tanks do not.

## 3. Bills

### ARMOUR-1 — THE HAMMER (counter-landing)

**Pitch.** "A landing is a toddler with a loaded gun, standing in a field, for thirty seconds. In the first thirty seconds it has no dome, no escort, and it is *burning*. I do not want a Reserve Line. I want a **fist on the rail** that is already moving when the pod opens. Let them land. Let them regret it."

**Player fantasy.** You watch the drop warning, and your armour column peels off the Line unprompted, arrives under the glow of the re-entry plasma and kills the beachhead before the dome rises.

**Clerk's Annex.**
- *Mechanic.* New automatic behaviour for **idle `tank`/`heavytank`/`mammoth`** (and `colossus`, `landdread`): when a landing's `reentry` entity is within 2,400 px, the armour reserve gets **"Hammer Window"** for 30 s from touchdown: **+60% speed** (on top of `speed`, or `AMPH_SPEED` for fusion hovers), **+25% damage**, and ground-hold units dispatch toward it. It costs nothing. It only works for units not in combat and not entrenched (`u.entr < .25`, L8213). The fleet's answer (no power creep): landings after minute 12 drop an **escort of 4 `gmech` per landing** with the `breacher` (the dome rises at 15 s not 30 s, set in the same spawn at L5642), so the window shrinks as the fleet gets scared. Fear Index (Nerve) ties escorts to fear.
- *Code.* `pickLandingZone` L5237; `doSkyfall` L5213; `spawnEnemy` L5581, `gtank` L5604, `breacher` L5642; idle-ground AI around L8213-8224; `AMPH_SPEED` L3312.
- *Size.* M. *TM1.*
- *Research.* None: tanks already owned. Hovers (Fusion Age) widen the reach.
- *Tenet (Amend. I).* Pure buff, with an enemy answer (shorter dome timer, escorts). No nerf.
- *One-Button.* Passes: standing, automatic. Ten tanks arrive as a fist, a hundred as a hammer.
- *Line.* Only moves left and right. It works *because* the front is a line: a reserve stack at the hub nearest the fighting is at most 2,400 px from any zone.
- *Interacts.* **Subterranean/Industry Rail Hub** (the Sally Port clause; hubs teleport the reserve). **Autofleet** (escort scaling, dome timer). **Rifle Corps** (infantry follow behind at speed 14 and hold what armour clears).

### ARMOUR-2 — THE ANVIL (armour's half of the Thermal Field)

**Pitch.** "The lasermen heat the beast. Fine. Somebody has to *hit* it. Heat is a promise; **a 120-millimetre sabot round is the signature on the contract.**"

**Player fantasy.** A laser grid cooks the Leviathan's hull cherry red, then a line of Mammoths puts every shell through the glow. A tank-only army still wins, just in nine minutes instead of three.

**Clerk's Annex.**
- *Mechanic.* Tank-class shells (`weapon:'shell'`) deal **×1.5 vs heated targets** (the Thermal Field's kinetic bonus), and **Mammoth/Goliath/Lance get an extra "sabot" ×1.3** vs heated *boss or armor ≥ 12* targets. The wall (enemy-made) is the new **Ablative Carapace** on the Leviathan and the heavy ground tier, in the Thermal Field's own language: **cold hull = ×0.55 kinetic damage until heat ≥ 30%**. Shells then do 22 per hit cold and ~110 hot. A tank-only army works slowly on the cold Leviathan, a mixed one shreds it. This moves the wall off armour (see Gap) onto a damage gate. Hit that wall with hot-only decay: cooling is fast in air, so ground targets cool in ~6 s; hence the lasers must keep up.
- *Code.* `hurt()` L4978 (insert beside `ARMOR_NERF`); `spawnEnemy` L5627 (leviathan), L5604 (gtank); depends on Thermal Field's `heat` value (docket coalition #1).
- *Size.* S on top of Thermal Field. *TM1.*
- *Research.* Thermal Field `thermalload` (energy, prereq `directedenergy`) × `metals`. Combo: `plasma` gives plasma tanks (`Aegis-P`, L3321) a heat-deposit ability, so plasma tanks are armour's own heater and need no laser.
- *Tenet.* The Carapace is an enemy buff against a *monoculture*, not a cap. Passes the Address's "wall made by the enemy" test.
- *One-Button.* Passes. Nothing to aim. *Line.* Passes.
- *Interacts.* **Energy, Physics, Railgun** (heat deposit). **Inspector, Arsenal Bloc** please confirm the cold multiplier doesn't also gate the Falcon swarm.

### ARMOUR-3 — THE SIEGE BREACHER (kills anchors and domes)

**Pitch.** "A dome is just a very fancy wall. I have been knocking down walls since before your airframes had wings. Give a Mammoth a breaching gun and I will take a beachhead dome apart with brute force and no clever physics."

**Player fantasy.** The dome flickers, the anchor cracks, and a column of Mammoths rolls over the wreckage.

**Clerk's Annex.**
- *Mechanic.* Heavy Foundry Mammoths gain **Breach Shell**: ×3 damage to `anchored`/`breacher` units, domes (`enemyDome`) and enemy buildings, range 360. Domes have `frameCap` (anti-delete), so three Mammoths wear one down in ~20 s, ten in ~6 s. Enemy answer: the `ebreacher` gets a 2nd field layer after minute 15 (dome HP ×1.5).
- *Code.* `hurt()` dome branch L4979-4980; `ebreacher` art L2773; `breacher` L5642; Mammoth def L3195.
- *Size.* S. *TM1.* *Research.* Heavy Foundry (production line, no research). *Tenet.* Buff plus enemy answer.
- *One-Button.* Passes, automatic target priority. *Line.* Passes: domes sit on the line.
- *Interacts.* **Artillery Corps** (also anti-dome; Breach Shell is the *close* answer). **Autofleet** (dome stages).

### ARMOUR-4 — THE RECOVERY YARD (tanks come back)

**Pitch.** "Infantry are replaced. **Armour is recovered.** In 1944 the Soviets rebuilt a tank in a field and had it back at the front by lunch. A hulk is 70% of a tank."

**Player fantasy.** After every fight your wreck-line is dragged to the yard and pops out as fresh hulls. You win battles of attrition because your dead come back.

**Clerk's Annex.**
- *Mechanic.* New building **Recovery Yard** (cat INFRA, ~180 IC, 60 MP, `build` 10, auto-scale). A destroyed `tank`/`heavytank`/`mammoth` within **600 px** of a Yard leaves a hulk; after 20 s, **40% of its IC** comes back as a **new unit** at the Yard (queued, not free: army cap still applies). 3 Yards in range work 3 hulks at once. Real basis: armoured recovery vehicles (ARVs, Bergepanther) and field-repair brigades.
- *Code.* Kill hook in `hurt()` L4992 `killBy`; `deliverOwnRosterUnit` (grep); `makes`/`autospawn` pattern in BUILD_DEFS L3416, L3432.
- *Size.* M. *TM1.* *Research.* `metals`. *Tenet.* Pure buff; the enemy answer is that bombers and `pelter` splash target *hulks* (hulks take ×2, die in 4 s) so the Line must hold to recover.
- *One-Button.* Passes: build more yards, recover more. *Line.* Passes: placed behind the front.
- *Interacts.* **Industry** (IC sink, a real use for surplus IC), **Market**.

## 4. Hot Take — THE RECONSOLIDATION

**Pitch.** "Twelve Mammoths standing in a field is a *parade*. Twelve Mammoths **welded together** is the Land Dreadnought the Grand Arsenal charges you 1,500 IC for. I say a Colonel-General should not need a requisition. Park them, and let them *combine*."

**Clerk's Annex.**
- *Mechanic.* **12 Mammoths** idle within 120 px for 25 s auto-fuse into one **Land Dreadnought** (`landdread` L3282, 2400 HP, 13 IC/s upkeep). The 12 are consumed: cost = their IC. The merge needs a Grand Arsenal built (`grandarsenal` L3401, `req: colossus`). Armour spent on one hull frees army-cap slots but buys *upkeep*. A single Dreadnought is a real target (the Bombarder snipes it): the enemy answer.
- *Code.* `landdread` L3282, `prodList` L3401, idle-ground tick L8213.
- *Size.* M. *TM1.* *Research.* `colossus` (L3518). *Tenet.* Buff with a real cost (13 IC/s upkeep), no cap. SCI-FI: the "magnetic coupling" rationale needs the Fusion Age.
- *One-Button.* Passes (automatic when the army idles). *Line.* Passes.
- *Interacts.* **Megaprojects** (Dreadnoughts as an army; could attach to the superheavy railgun), **Economy**.

## 5. Alliances sought

- **Ground Staff (whip):** ARMOUR-1 needs your Sally Port wording. **Rifle Corps:** infantry follow my tanks as the "ride-on" screen (their R3 asked for a Line). **Artillery Corps:** an anti-dome pair with ARMOUR-3.
- **Energy / Physics (Thermal Field):** I am your kinetic finisher, and I will exchange the Carapace for your heat deposit.
- **Subterranean Works / Industry:** Rail Hubs as my reserve spawn point.
- **Insult budget:** the Air Council, for believing a Falcon is a *weapon*; it's a *rumour*.
