# The Member for the Arcade (arcade) — Crossbench Paper

(Line numbers refer to `title.html` as read today.)

## 1. Maiden Speech

Honourable members, I came in expecting a tower defence with a nuke button, and I found a factory-builder wearing a war. Prep runs 135 to 500 game-seconds (L3598). Then the sky falls on a 22 to 32 second wave clock (L6546). There are about 70 units, about 90 buildings, and a *nine percent per factory discount* (L3836). That is the best sentence in the file: a Supreme Commander "mass the factories" fantasy, written as one line of maths.

But here is my streamer face. Every unit is one click (`produceGlobal`, L9923). The dead kill makes you nothing. The four resource nodes are claimed once and forgotten. The enemy picks the *least defended* landing zone (L5237), and you never get to exploit that. The bones are Factorio, Creeper World and Total Annihilation. The loop is still "click, click, click, wait".

My bills fix the clicking, pay out the killing, and let you play with the enemy's brain.

## 2. What the Code Tells You

1. **Production is click-per-unit, and that is the game's biggest tedium.** `produceGlobal(key)` (L9923) routes one purchase to the shortest queue, and `queueUnit` (L4619) deducts IC and MP for that one purchase. Army cap is the only brake (`armyCap`, L4623). Factorio would be furious. The volume lever exists (`unitCost`: each extra factory is -9%, floor 22%, L3836), but the player pulls it by hand a thousand times. The Law says "the player clicks to build, and things scale with volume". Today only *buildings* scale.

2. **A kill pays nothing.** `destroyEnemy` (L6845) only refunds IC if `G.salvage` is set, which comes from one research node, Nanotechnology (L3517, L6849). There are no wrecks to reclaim, though `wreck` appears 29 times, all of it visual. In Total Annihilation the battlefield *is* the economy. Here the fight and the factory never touch.

3. **The dead minute is the 22 to 32 second wave gap, plus all of the prep.** `G.waveT = 22 + rand*10` (L6546). The `fleet` counter drains by one per spawn (L5568). Between waves, an income-capped player has nothing to decide. The tension the original builds is real, but it is carried by waiting, not by choices.

4. **The enemy has a brain the player never gets to touch.** `pickLandingZone` (L5237) scores candidate zones by `defenseScoreAt` and drops on the weakest. "THE FLEET ADAPTS" (L5481, L5525) re-tasks every 45 seconds against your dominant army type. `hijacker` enemies seize robotic mass (L770, L5525). All of that is a great reactive enemy, and the player cannot *aim* it.

5. **Resources are one-and-done.** `SR` has four types (L3173), each is a node spot on the map (L3636), and `extractor` is "+4 IC" (L3384 area). After the first click there is no Factorio-style depth: no richer seams, no chains between nodes. `COMP` has exactly two components, capped at 60 (L3180, L6320).

6. **The vertical axis is half-used.** `digLevel` and `UG_DEPTH` give two dig levels (L3863). Hostile beams skip buried buildings (`!b.ug`, L6397). Buried buildings are safe and invisible, yet nothing *lives* down there that you want. The Line says the extra dimensions are vertical. Underground is the least filled one.

7. **Stability is the real second health bar and an excellent brake, but the player cannot spend it creatively.** Strikes cost it (`STRAT`, L6885), demolition costs it (L8463), and bosses return +10 (L6872). It already works as "risk currency". Great, and under-used.

8. **Garrison and mobility logistics are already the best lever.** Garrison weight (L3807), Rail Hub (L3416), Mobilization Hub (L3422) and Transport Heli (L3248) all fix the "unit stands in the wrong place" problem automatically. This is the template I want every bill to follow: build it, and it works.

## 3. The Bills

### Bill A: The Standing Order ("Set It and Forget It")

**Pitch.** Streamers, I have clicked "Infantry" four hundred times in this game and my wrist has filed a complaint. Give each factory-type building one little **order dial**: "keep producing X whenever IC is above Y". The dial persists. You build forty factories, set them once, and *watch* your nation mobilise. The click is the building. The factory *is* the order.

**Fantasy.** I am a war economy, not a clerk.

**Annex.**
- **Mechanic.** Each production building (`b.d.prod`, e.g. `factory`, `airbase`, `shipyard`, `spaceport`) gets an optional **standing order**: unit key plus an IC reserve floor, default off. When its queue is empty and `G.ic > reserve`, it calls the existing `queueUnit(b, key)`. Numbers: reserve default 200 IC, one order per building, checked once a second. Orders obey every existing gate (army cap, air command, orbital lift, components).
- **Hooks.** `queueUnit` L4619, `produceGlobal` L9923, `facsFor` L9922, building panel UI near L10155, the per-second tick pattern of `tickAutoStrikes` L7715 (silos already have `autoStrike`).
- **Size.** S to M.
- **Rule checks.**
  - *Tenet.* No cap, no nerf. It is pure convenience, and the existing army cap and upkeep are still the brakes.
  - *One-Button Law.* This is exactly the law. It removes per-unit clicks. It is optional (default off) and a single standing choice.
  - *The Line.* Untouched.
- **Enemy answer.** None needed. It is convenience, not power. The player's real power rise is in how fast they build, and the enemy already scales with `G.diff × (1 + invTime/70)`.

### Bill B: Wreckage

**Pitch.** Total Annihilation taught me that a dead tank is a pile of money. Make every killed enemy leave a **wreck** (a piece of scrap where it fell). Build a **Salvage Yard**, a building that exists in no form today, and it sends tiny auto-salvagers out to reclaim wrecks within its radius. A good kill-zone becomes an *economy*: the harder you fight, the richer you get, so the fight and the factory finally touch.

**Fantasy.** I farm the invasion. Their dead pay for my next army.

**Annex.**
- **Mechanic.** `destroyEnemy` drops a wreck record `{x, y, ic}` with `ic = round(5 + maxHp*.14)` (the formula that already exists at L6849 for Nanotech salvage). A Salvage Yard (about 160 IC, radius 500 px) claims wrecks at 2 per second per yard and pays 60% of that value. Nanotech's flat refund stays as the unlocked bonus. Wrecks despawn after 40 s so the list stays small (the spatial grid pattern at L7801 is reusable).
- **Hooks.** `destroyEnemy` L6845, `G.salvage` L6849, `BUILD_DEFS` L3381, `floatText` L5124, grid pattern `GRID_E`/`XN` L7801.
- **Size.** M.
- **Rule checks.**
  - *Tenet.* It adds a YES. Nothing is capped.
  - *One-Button Law.* Build a yard; everything else is automatic.
  - *The Line.* Wrecks fall to the ground band, and airborne kills drop near ground. No new axis.
- **Enemy answer.** Salvage rewards fighting at your own wall, so the enemy's `skyfall` and bunker-buster pressure (L5129) naturally lands in the salvage yard's neighbourhood. A scarier enemy means richer wrecks, not a harder cap. The enemy's anchor-landing harvester (L5647) can already "keep producing", so it is the natural target of the yard.

### Bill C: Deep Seams

**Pitch.** Factorio gives you an ore patch and an ever-hungrier belt. Here the four nodes are a one-click "+4 IC". Let the **vertical axis pay out**: dig an Extractor deeper (`G.dig` already has two levels) and the *same node* yields more, at an upfront IC cost. Deeper veins are also safe from beams. The ground fights for the surface and the economy quietly sinks under it.

**Fantasy.** My nation is a dense, hidden anthill of industry.

**Annex.**
- **Mechanic.** An Extractor on a node can be built shallow (as today), or at depth 1 or 2 (using `digLevel`). Yield multiplier ×1 / ×1.8 / ×3. Build-cost surcharge as the existing burial surcharge. Resource bonuses `SR` get a deeper tier too: Steel -18% becomes -24% and -30%. Special resources then become real bonuses, which fixes their barely-used role.
- **Hooks.** `digLevel` L3863, `UG_DEPTH` L3862, `SR` L3173, `nodeNear` L3876, `extractor` L3384 area, the burial surcharge near L3888, the dig tab L10957.
- **Size.** M.
- **Rule checks.**
  - *Tenet.* Pure power-up for investing.
  - *One-Button Law.* The player picks the existing dig tab and clicks the node. Nothing to micromanage after.
  - *The Line.* Uses the underground dimension exactly as intended.
- **Enemy answer.** The "Earthbreaker" (a heavy seismic-press enemy that exists today) is already the counter to buried assets, so buried Seams give it the target it was made for. A stronger earthbreaker wave is the knob, not a cap on seam yield.

### Bill D: Drop Forecast ("Creeper World Sonar")

**Pitch.** Creeper World's best trick: you see where the pressure will land, and you *respond with geometry*. The enemy already *plans* (it scouts and picks the least defended zone: L5237, `defenseScoreAt`). So show the plan. A **Radar-fed forecast band** along the top edge ghosts the next landing zone about 10 seconds before it happens. Your wall (garrisons, Rail Hub, the Transport Heli) reacts on its own. The player's choice becomes *where to build the prep-time answer*, not guesswork.

**Fantasy.** I read the enemy's mind and punish it.

**Annex.**
- **Mechanic.** Pre-roll `lz = pickLandingZone` when `G.waveT < 10` and store it as `G.nextLZ`. Radar buildings (`radar`, L3379 area, and their stability reserve effect) reveal the forecast, one marker per radar-covered stretch. `doWave` consumes the stored value instead of rerolling. Mobile rail troops (L3416) auto-route there (they already go "to the hub nearest the fighting").
- **Hooks.** `pickLandingZone` L5237, `doWave` L5540, `G.waveT` L6546, `G.lastLZ` L5543, the Rail Hub redeploy logic, minimap draw near L9838.
- **Size.** S.
- **Rule checks.**
  - *Tenet.* It reveals, never takes.
  - *One-Button Law.* Zero attention, because it is information and the existing automation uses it.
  - *The Line.* Horizontal x-marker only.
- **Enemy answer.** More decoy zones per wave at high difficulty, plus the adaptive re-tasking (L5481) that already reacts every 45 s. The enemy buff (feints) does not make the player's army worthless. It rewards a mobile one.

### Bill E: The Decoy City

**Pitch.** The fleet scouts for the *weakest* spot (L5237). A weakness that a player can *build on purpose* is a bait. Let a cheap **Lure Beacon** read as an irresistible hole in the defence score, while all your guns, silos and kill-zones sit around it. You become the Bloons player with a luring path: the enemy walks into the maze you drew.

**Fantasy.** I am a spider, not a fly.

**Annex.**
- **Mechanic.** A Lure Beacon (about 90 IC, hp 120, a new building I propose) counts as `defenseScoreAt` -5 within 250 px. It also pings `G.lastLZ`, so the bunker-buster pre-soften (L5543) targets it instead of your guns. Only one lure is honoured per flank (`ESC.FLANK_MERGE`, L5162). A lure that is ignored for three waves is removed by the snowball logic (flank `answered` check).
- **Hooks.** `defenseScoreAt` (used L5244), `pickLandingZone` L5237, `ESC.FLANK_MERGE` L5162, `avoidCityCenter` L5236.
- **Size.** S.
- **Rule checks.**
  - *Tenet.* An extra toy, not a cap.
  - *One-Button Law.* Build it once. The AI does the rest.
  - *The Line.* Horizontal only.
- **Enemy answer.** Higher difficulties adapt (`THE FLEET ADAPTS`, L475): the fleet stops trusting a lure that has killed it twice, and bosses (`supers`, L5126) ignore lures entirely and always target the capital or the weakest *real* city.

### Bill F: Spoils of War (Boss Trophies)

**Pitch.** Bosses already give +10 stability (L6872) and a kill-flourish, and then... nothing. Mindustry and every great roguelite pay a boss with a *choice*. When a boss dies it drops a **Captured Core**, a trophy item that auto-slots into your research as a one-time free minor perk. Killing a Skyfortress "Hive" (a boss that spawns drones; exists today) gives a drone-production bonus. Killing a Land Leviathan (the boss ground-siege crawler; exists today) gives armour. You feel the beat of every boss.

**Fantasy.** I tear the technology out of the enemy's corpse.

**Annex.**
- **Mechanic.** Each boss kind (`hive`, `leviathan`, `annihilator`, `bombarder`) grants a persistent `G.mult` bonus (about +4% in the relevant stat) per kill, stacking up to the boss count in the run. No choice screen. It is automatic, and the toast names it. Optional: a once-per-run "pick one of three" when the player has idle time, never required.
- **Hooks.** boss kill block L6872, `G.mult` (read at L6326), `finishResearch` L4703, toast system.
- **Size.** S.
- **Rule checks.**
  - *Tenet.* A buff, so it names the answer.
  - *One-Button Law.* Fully automatic.
  - *The Line.* Not relevant.
- **Enemy answer.** Bosses already arrive in groups (`spawnSuper`, L5276) and in EXTINCTION in swarms of 10 to 30 (L5142 `extinctionSupers`). More trophies per boss means the player's army grows exactly in step with the scarier bosses. If the bonuses ever outrun the fleet, scale boss count, never cap the trophies.

## 4. Hot Take

### Bill Z: Turn the Fleet ("Brainwash Beacon")

**Pitch.** The enemy already has a **hijacker** (an enemy ECM unit that takes over your robotic units, L770, L5525). Do it back, and make it *the* power fantasy: a **Subversion Array**, a new tall building I propose, that flips a share of nearby dying enemy drones into *your* units instead of killing them. Every army-cap-free drone you convert is a body for free. Late game you are fighting the autofleet *with* the autofleet, a thousand of their drones turned on their own carriers. This is the "total mobilisation" at its most unhinged.

**Fantasy.** I am so well-mobilised that the enemy's own army has become my reserve.

**Annex.**
- **Mechanic.** When an enemy of a robotic kind (drone, bombpod, interceptor) would die inside a Subversion Array's radius (about 400 px), it has `p = 0.15 + 0.05 × (arrays in range, max 4)` to flip to friendly, keeping 40% HP, and it counts as a weighted-but-free unit (the existing `isMechanical` rule already exempts it from army cap, see `deriveWeight`). Flipped units decay (timer 60 s) so they never become permanent bloat. IC cost about 340, an enemy of kind `jammer` hits it first.
- **Hooks.** `destroyEnemy` L6845, `hijacker` and `kindWeights = {jammer:2}` L5525, `UNIT_DEFS` L3184 (spawn flipped copies using the matching art), `isMechanical`.
- **Size.** L.
- **Rule checks.**
  - *Tenet.* An enormous yes. The player is never capped.
  - *One-Button Law.* One building. The conversion is automatic.
  - *The Line.* Turned units use the enemy's existing ground and air lanes.
- **Enemy answer.** The fleet *already reads your dominant composition* every 45 s (L5481). Subversion makes it re-task toward "loyalty" kinds: **Warden** hulls (a proposed enemy kind) that carry a conversion shield, and bosses that ignore flips completely. Because arrays have to be built *forward*, the enemy's Earthbreaker and bunker-buster salvos (L5129) have a clear target, so the array is the price of the power. If conversion ever reaches blatant overpowered status, the answer is more Warden hulls, never a cap on arrays.

---

*Priorities if the House can only pass two: Bill A (kills the worst tedium, almost free) and Bill B (the missing "fight pays the factory" link).*
