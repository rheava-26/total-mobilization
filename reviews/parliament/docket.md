# The Speaker's Docket

*The Mobilization Parliament sat for three rounds. Twenty-three councils filed 120 bills (68,000 words), debated them, spent 230 votes, and answered 60+ co-sponsorship motions. This docket merges what they agreed on into coalition bills and sends them to the President for signature. Each council could spend at most 3 votes on one bill and none on its own. Coalition sponsors are councils that signed the joint text in Round 3.*

Sources: `r1/` (position papers), `r2/` (debate and ballots), `r3/` (coalition answers), `motions_register.md`.

---

## I. Rulings of the Chair on points of order

| # | Point | Ruling |
|---|---|---|
| 1 | **PACING-HOT**: more industry brings the fleet sooner (shortens prep) | **Withdrawn by its sponsor.** It violated Article III (taxes the build fantasy). Its dread survives inside Bill 2: industry makes the fleet arrive *bigger*, never *sooner*. Unanimous. |
| 2 | **`capLevels`** (title.html L1505): past 220 terrain edits, the oldest edit is silently **undone** | **Struck as unconstitutional.** It is worse than a cap, because the player's earlier work reverts without notice. Remedy: bake evicted edits into the base heightfield instead of reverting them, which keeps the performance reason and keeps the edit. Same for the 60-ruin eviction (L1520). **Quick fix.** |
| 3 | **`dangCap`** (L4708) silently limits dangerous research | Constitutional as a cost, unconstitutional while invisible. It becomes visible and chosen through Bill 12 (the Charter). |
| 4 | **`srMult`** (L3810) treats deposits as owned/not-owned, so a second deposit of a type does nothing | **Bug.** The count should matter. Folded into Bill 9 and listed as a quick fix. |
| 5 | Psi scarcity: a dedicated psi pool vs throughput plus stability | **Throughput plus stability cost** (slow Relay-limited awakening). A dedicated pool is a cap, and it would touch the army-cap formula (Art. IV.1). |
| 6 | Shapeshifter immortality | **Replaced by heat**: regeneration switches off above heat 60, and heat above 90 slows them 30%. Psi's further damage slump (×0.5 damage, +40% taken) is **struck** as an Art. III nerf on the energy and physics councils' objection. *Flagged for the President*, because Psi wants it back. |
| 7 | ENERGY-2's 35% fire-rate floor on power shortfall | Withdrawn by sponsor. A shortfall is billed as IC debt, never a throttle. |
| 8 | STABILITY-2 "a repeated propaganda campaign gets half effect" | Struck (a diminishing return on a player tool). Replaced by the fleet *doubting* after three identical lies. |
| 9 | PSI-4 lattice nodes absorb 25% Mind-Forge damage | Struck. Lattice nodes become killable cover instead. |
| 10 | STABILITY-4 "information policy dial" vs the President's "no sliders for market/command" | Not the same thing (it is a war-information policy, not the society reading). Allowed, **flagged for the President**. |
| 11 | MARKET-6 Privatise the Sky (taxed commercial orbit) | Little support and an Art. IV.3 objection. Its debris-zone *data structure* survives inside Bill 6. The commercial-orbit fantasy is **not carried**. |

## II. What the councils found in the code (true today, worth knowing whatever is built)

- **The fleet's adaptation scan has no air or sky-navy axis** (`scanEnemyIntel`, L5737). Five straight wins with air plus sky navy read as "no dominant doctrine", so the fleet never countered them.
- **Enemy strength scales with time only** (L5582). Your industry doesn't frighten it.
- **Fire Control is dead code**: `G.fireControl` is never set (L8447).
- **Artillery shells have no blast radius.** They make one `hurt()` call plus a visual-only burst. `splashDamage` has five callers and none is a cannon.
- **Re-entry heating is cosmetic** (L8631). Nukes do the same 720 damage at radius 230 at any altitude (`detonateNuke`, L7565).
- **The TEL, Ballistic Submarine and Mobile Launcher described in CLAUDE.md don't exist.** The Missile Silo is the only strategic launcher. Only taboo strikes cost stability now (CLAUDE.md is stale).
- **No enemy targets the sea.** Naval units need no research.
- **The event engine is built but switched off** (`EVENTS_LIVE=false`, one event). Achievements hold about 40 medals, none of which change play.
- **`noise()` allocates a new AudioBuffer on every call** (L1122). It's a small performance leak.

## III. The docket: coalition bills

Votes are the Round 2 ballot totals of the bills merged into each coalition. Sizes: S < 100 lines, M 100–400, L 400–1,500, XL a rewrite. All bills are TM1 unless noted.

### 1. THE THERMAL FIELD: heat as combined arms. **Votes 15 (top of the ballot)**
*Sponsors: energy, physics, psi · joined by railgun, game-feel, science*
- **One `heat` value on every enemy.** Lasers and plasma deposit it; plasma deposits a burst on impact (the "contained field dumps its energy" fantasy).
- **Hot targets take up to ×1.5 kinetic damage and lose up to 50% armour.** Lasers cook, guns finish.
- **Cooling depends on the medium.** Air cools fast, vacuum slowly, so space targets stay hot.
- **Re-entering pods arrive white-hot**, with a predictable plasma blackout window. That rewards rails, rods and kinetics on the drop.
- **Shapeshifters use the same field** (ruling 6). Friendly units only take heat from enemy sources.
- **Reading it:** cached heat-tint stamps, never per-frame glow.
- Research `thermalload` (energy, prerequisite `directedenergy`). **Size L.** *Merges ENERGY-1, ENERGY-4, PHYSICS-1, PHYSICS-2, PSI-2.*

### 2. THE NERVE: the fleet has a mind, and four ways to end the war. **Votes 23**
*Sponsors: autofleet, lore, pacing, stability, megaprojects · joined by psi*
- **Two needles on one panel.**
  - **Fear** rises with your industry and output and **scales the fleet up**: more bombarders, more supers, with the old 9-bombarder ceiling made soft. It never shortens the clock.
  - **Resolve** falls with the fleet's losses and drives its behaviour bands: Steady, Rethink (it picks a Decisive Strike or a planet-cracker flagship), Despair, and Calculated Retreat.
- **The HUD shows what the fleet believes**, which Bill 3's deception can spoof, with a tick for the truth.
- **Four doors to end the war:**
  - **Attrition**: today's withdrawal.
  - **Rout**: kill the flagship.
  - **Orbital Denial**: space superiority, gated `orbitalcmd` + `spaceage`. This gives the space fleet a win condition.
  - **Truce**: "we stop if you stop", offered only at Despair or in the final cycle. It seeds the TM2 epilogue.
- **A rethink can't block an earned win:** its gate expires after 90 s.
- **Size L.** *Merges AUTOFLEET-2, AUTOFLEET-4, LORE-2, PACING-5 and PACING-HOT (as amended).*

### 3. THE FLEET READS YOU: real adaptation, counter-air first. **Votes 13**
*Sponsors: autofleet, air, naval, command, stability, psi*
- **New scan axes: air, sky navy, command layer and psi.** Each gets an answer:
  - **Reaver** air-superiority hunters and **Weasel** anti-radiation flights. They spawn only once you field 20+ fighters.
  - **Durandal runway cratering.** It only hits bases with 8+ parked aircraft.
  - Command-layer decapitation waves ("Cutting the Lines").
  - Hush-Hound pickets against psi mass.
- **The player's side ships in the same bill:**
  - Hardened Shelters;
  - rapid runway repair;
  - EMCON, which drops the Weasel's lock;
  - a Staff Bunker "Continuity" option in a capitulation crisis.
- **Maskirovka:** propaganda campaigns feed the fleet false intel. After three identical lies it starts doubting.
- **Size L.** *Merges AUTOFLEET-1, AUTOFLEET-3, AIR-3, AIR-5, STABILITY-2, COMMAND-4 (partial), PSI-4 (partial).*

### 4. THE ONE FORECAST: see the invasion coming. **Votes 8**
*Sponsors: new-player, pacing, market, science, lore, psi, stability*
- **One `G.forecast` model:**
  - fleet size, composition, gambit and ETA as bands;
  - band width `±40% / sqrt(1 + observers)`, floored at ±5%;
  - observers are Radar, Data Center, sonar arrays, a Tracking Array and a new `signals` node;
  - the exact arrival time is known from T-60.
- **The prep phase plays as beats** (deep contact, burn, manifest, split, re-entry), rendered from this one value.
- **Tier 1+ "unconfirmed" lines can be wrong**, with probability `0.25 × (1 − confidence)`.
- **Optional layers:**
  - Market's influence-staked positions;
  - Psi's Listening Chamber, an active 90-s peek that costs stability cap and that the fleet hears.
- This is the President's planned TM1 slice **"forecast-sharpening intel buildings"**, and it closes the oldest gap: "the invasion plan was never explained". **Size M.** *Merges NEW-PLAYER-1, PACING-2, MARKET-2, LORE-5 (forecast half), SCIENCE-2 Deep Survey, PSI-X.*

### 5. THE STANDING PLAN: auto-build and the end of the 212k-IC hoard. **Votes 11**
*Sponsors: command, industry, market, new-player · joined by air, bio, missiles*
- **A Planning Bureau holds ledger lines.** Each line is "keep N of X fielded", "build Y here" or "stock Z warheads". The plan auto-queues, auto-places and refills losses, spending your surplus automatically.
- **Two skins of one engine:**
  - Command lines: fixed targets with a 6% planner's tax.
  - Exchange lines: the market buys last fight's best kills-per-IC, with a +10% build-value dividend.
- **Reserve floor** defaults from your emergent society reading, `max(market-ness, command-ness)`, so neither road is taxed. The player can override it.
- **Every blocked line shows a "Blocked: needs X" chip** with a "Build the fix" button.
- This is the President's planned TM1 slice **"surplus-IC outflow by what you built"**. **Size L.** *Merges COMMAND-1, INDUSTRY-3, MARKET-1, NEW-PLAYER Motion 3.*
- *Open for the President:* the overhead number. Industry wants 4 IC/s per line and Command wants a 6% tax. The Chair recommends the 6% tax.

### 6. DEAD SKY PROTOCOL: make the orbital war winnable. **Votes 15**
*Sponsors: space-fleet, missiles, physics, market, energy, science · joined by air, autofleet*
- **A three-rung ladder on one shared debris record** (`G.debris`, zones with half-lives by altitude: 12 s stratosphere, 40 s thermosphere, 120 s at the space band):
  1. **Burst.** Nukes split into blast, thermal and EMP by air density. A high **Starfish** burst blinds the bombarder grid and zeroes its point defence in the zone. It also EMPs your own unhardened drones and satellites, unless you research `radhard`. Hardened and sheltered aircraft are spared.
  2. **Lane.** The Retrograde Shotgun fires an un-interceptable pellet lane that kills grid satellites. It can also hit your own satellites unless they are hardened.
  3. **Burn.** The opt-in **Kessler Doctrine** burns the whole thermosphere for 180 s, theirs and yours.
- **Debris belts shred re-entering pods.**
- This is the councils' answer to "the whole space fleet dies to the grids". **Size L.** *Merges PHYSICS-3, PHYSICS-4, MISSILES-X, SPACE-4, SPACE-X, MARKET-6 (structure only).*

### 7. AMMUNITION & THE ONE LOCKER: an IC sink you can feel. **Votes 14**
*Sponsors: kinetic-arms, railgun, industry, missiles, energy*
- **Ammunition Trains.** Guns near supply fire faster with a supplied bonus, and IC is burned per shot fired. An empty stock means no bonus and no penalty.
- **The Warhead Stockpile** replaces the strike cooldown with magazine depth bought with IC.
- **A per-group round selector for every kinetic gun:**
  - HE-frag;
  - AP-sabot: armour ×0.35, costs alloy; it stacks with heat to at most 50% armour cut;
  - programmable airburst for flak, with a 46 px fragment splash;
  - guided hypervelocity rounds for rail.
- **Shells finally get a blast radius.**
- Research `smartfuze` / `apds`. **Size M–L.** *Merges INDUSTRY-1, MISSILES-5, KINETIC-1, KINETIC-2, RAILGUN-3.*

### 8. THE TRUNK: the continental railway. **Votes 9**
*Sponsors: industry, planet, command, railgun · joined by naval, ground, megaprojects, bio*
- **Rail Hubs within ~1,200 px link into one buried network.** The network shares components, boosts linked industry, and auto-rebuilds lost buildings through a Reconstruction Corps.
- **Things that ride or plug into it:**
  - a rail-mobile strategic launcher (the Molodets) that rides it;
  - harbour nodes;
  - sally ports, where battalions debark behind a named line;
  - a Launch Rail that counts as a rail-linked Space Center.
- **Upkeep** is per 1,000 px of track.
- **Later:** the network becomes the Meridian Accelerator's barrel, a doctrine that turns your rail sprawl into one giant gun.
- It is the President's underground-railway seed. **Size L.** *Merges INDUSTRY-4, PLANET-1, COMMAND-2, RAILGUN-X.*

### 9. DEEP DEPOSITS: strategic resources finally matter. **Votes 9**
*Sponsors: planet, industry, naval, science, bio*
- **Deposits get a grade and a depth.**
- **Extractors get five IC-bought development tiers** that raise yield *and* the strategic-resource bonus.
- **Deposits of the same type stack** (fixes the `srMult` bug).
- **Seabed nodules, seawater uranium and OTEC** give the navy an economy.
- **Radiotroph gardens** turn fallout into IC, influence and healing, as the uranium customer.
- Research `geosurvey`. **Size M.** *Merges PLANET-3, INDUSTRY-2, NAVAL-2, BIO-3, SCIENCE-5 (grades).*

### 10. THE LONG GUN: the superheavy railgun megaproject. **Votes 15**
*Sponsors: megaprojects, railgun, kinetic-arms, game-feel · joined by physics, bio*
- **First lift the PROJECTS framework** from `origin/megaprojects-wip`, as MEGA-1, the "Ledger of Projects". It has doctrines you fall into and megaprojects you build on purpose.
- **Then the Long Gun:** a turning superheavy gun mid-map, built section by section with no section limit.
  - The early sections are a light-gas supergun. Later sections are rail.
  - Damage scales with section count.
  - A hit interrupts the Orbital Annihilator's 6-second beam charge.
  - Its first shot in any 20 s gets a cinematic camera beat.
  - The fleet sends Interdiction waves against the build site as its "heat" rises.
- Research `megastructures`. **Size XL** (framework plus gun). *Merges MEGA-1, MEGA-2, MEGA-3, MEGA-H, RAILGUN-2, KINETIC-X, GAME-FEEL Long Shot.*

### 11. ONE NET: fire direction and time-on-target. **Votes 8**
*Sponsors: ground, kinetic-arms, space-fleet, air, missiles · joined by naval*
- **One engagement net and one solver** replace the dead Fire Control.
- **What feeds it:** counter-battery radar, the Argus AWACS plane, a relay satellite, silos and cruise missiles.
- **SYNC FIRE (optional)** lands everything in range in the same second, overwhelming the enemy's point-defence budget.
- Research `firecontrol_net`. **Size L.** *Merges GROUND-3, KINETIC-4, SPACE-2, AIR-2, MISSILES-1.*

### 12. THE CHARTER & THE DECREES: politics you choose. **Votes 10**
*Sponsors: stability, lore, command, science*
- **Every dangerous technology gets a three-position dial:** Restrain, Standing, Unleash. This makes `dangCap` a visible choice.
- **Decrees spend stability for tempo:** Haste, Reprisal, Evacuation, and a capitulation-crisis menu that includes Continuity of Government.
- **Size M.** *Merges LORE-1, STABILITY-H, STABILITY-3, COMMAND-4 (Continuity).*

### 13. THE NAVIGABLE MOAT: water as a weapon. **Votes 9**
*Sponsors: planet, naval, ground*
- **A CANAL terrain brush** (free, ungated). Canals and locks connect seas. A sluice-gate chain can flood a valley, with a 10-s evacuation warning.
- **Non-amphibious enemy ground landings drown.** The fleet answers with **Trench Landers**, so the sea finally gets a front.
- **Sappers discount and automate the works.** Delta Works seawalls and polders let you hold land below sea level.
- **Size L.** *Merges PLANET-X, PLANET-2, NAVAL-3, NAVAL-X, NAVAL-4, GROUND-4.*

### 14. DISPERSAL: the launchers CLAUDE.md promised. **Votes 6**
*Sponsors: missiles, naval, air*
- **Land TEL plus sea launchers on the shared warhead stock:** Boomer submarines in a new depth band, and the Arsenal Ship.
- **Subs hide from bombers.** Air gets a dipping-sonar Kite and sonobuoys.
- **Size M–L.** *Merges MISSILES-2, NAVAL-1.*

### 15. THE POWER GRID. **Votes 3**
*Sponsors: energy, railgun, space-fleet, physics, science*
- **One power pool** fed by Power Plants and **Helios solar satellites** (+6 MW each, so orbit has something worth defending).
- **Capacitor banks give rails an overdrive** at 20 MJ a charge, with bore wear as a real cost.
- **A shortfall becomes IC debt, never a throttle.**
- **Size M.** *Merges ENERGY-2, RAILGUN-1, SPACE-3, SCIENCE-2 Phased Arrays.*

### 16. THE DOSSIER LINE: wrecks become knowledge. **Votes 4**
*Sponsors: science, lore, industry · joined by market, autofleet, bio*
- **Enemy wrecks yield samples and black boxes.** Recovery works by any unit standing on the wreck, or by a Lab or Yard in range.
- **Samples cut matching research costs.** Each dossier is a permanent damage bonus against that enemy kind. Salvage also yields alloy and xenoalloy.
- **The fleet answers** with Sanitiser drones that detonate wrecks.
- **Size M.** *Merges SCIENCE-1, LORE-3, INDUSTRY-H.*

### 17. THE FLEET WILL RETURN: a second siege. **Votes 11**
*Sponsors: pacing, autofleet, game-feel, lore*
- **Withdrawal becomes an Interbellum** of 45% of prep, with a stability reset.
- **A stronger second fleet returns**, opening with a gambit drawn from what killed it last time (Net, FOBS or Wedge) and fielding Mirror echoes of your top-killing unit.
- **The final withdrawal gets the full cinematic.**
- **Size L.** *Merges PACING-1, PACING-3, AUTOFLEET-4 (retreat), AUTOFLEET-HOT, GAME-FEEL Director.*

### Smaller bills with cross-council support
- **The Living Battalion** (ground, bio, command). Squads pool into battalions that regrow to founding strength on MP, fall back to IC drafts, and become Shaken at 40% strength. Size L.
- **Synthesis Doctrines** (SCIENCE-2, 5 votes). Research combos fire permanent world rules, not just units. Size M. It is the framework for every "combos change the world" ask.
- **Orion** (space-fleet, megaprojects). A three-stage megaproject that ends in a nuclear-pulse capital ship. Size L.
- **The Breathing Sky** (physics, science). Ionospheric heaters thicken the upper atmosphere in sections, so pods heat earlier and grid satellites sink to the mesosphere. Bosses are exempt. Size L.
- **The Wire as Home Front** (stability, lore, new-player). Fleet and home headlines with receipts, plus hover ledgers for every HUD number. Size M.
- **The Exercise** (new-player, lore). Guided mode is framed as a training sim, and the real fleet is revealed after it. Veterans opt in. Size M.
- **Game feel.** Spin-Up (rotary guns spool to +35%; KINETIC-3; S), Gun Voice (positional, budgeted audio), the Impact Budget (trauma shake), Legible Mass (team rim baked into the sprite cache), and the Because Chip (every disabled button says why).

### Hot takes worth remembering (not carried, but the President should see them)
- **SCIENCE-X, The Return Address.** Reverse-engineer the warp catapult from the flagship's wreck and throw one back.
- **BIO-X, The Immortal Line.** A Titan larva that never stops growing.
- **GROUND-X, The Crawler Capital.** Your capital walks the strip on treads.
- **COMMAND-5, The Urals Gambit.** Rail-evacuate a whole coast's industry before the landing.
- **AIR-X, Pilots Are People.** Aces, bail-outs, and helicopter rescues.
- **LORE-H, The Invoice.** Winning too well summons the Benefactor to repossess his tech.
- **ENERGY-H, The Furnace.** A heliostat mirror field cooks a column of sky.

## IV. The Speaker's recommendation (Claude)

**Sign first: the core that makes the game deeper without bloating it.**
1. **The Thermal Field (1).** It tops the ballot, it is your own seed, and it gives every weapon family a reason to exist next to the others.
2. **The Fleet Reads You (3) with The Nerve (2).** Five wins with one doctrine is the clearest problem in the reports. These bills fix it the tenet's way: a smarter, scarier enemy, not a weaker air force. The Nerve also gives the space fleet a win condition.
3. **The One Forecast (4) and The Standing Plan (5).** They are the two TM1 slices you already planned. They fix the unexplained invasion and the 212k-IC hoard.

**Quick fixes to sign with them (S).**
- Bake evicted terrain edits instead of undoing them (ruling 2).
- Make deposits stack (ruling 4).
- Cache the audio noise buffer.
- Spin-Up.

**Second wave.**
- Dead Sky Protocol (6), so space matters.
- Ammunition & the One Locker (7), an IC sink with depth.
- Deep Deposits (9).
- The Trunk (8).
- The Long Gun with the Projects framework (10). It is the biggest build, and the one you've wanted longest.

**Where the Speaker overrules a popular bill.**
- **The Fleet Will Return (17, 11 votes)** is held for you. It turns a victory into an intermission and lengthens every match. That is a decision about what a win means, so it's yours, not the House's.
- **The Charter (12, 10 votes)** goes in the second wave. It is good, but it is UI-heavy and less urgent than the fleet and the economy.

**Open questions only the President can answer.**
- (a) Shapeshifter damage slump (ruling 6).
- (b) The Standing Plan's overhead: tax or per-line.
- (c) Whether the information-policy dial is allowed (ruling 10).
- (d) Whether a second siege should exist at all (17).
