# Battalion Design Reference
**A wartime strategy game design case study on formation-based combat organization.**

**Status:** Research document (draft, not canon) | **Research Date:** July 2026  
**Source Mix:** Web sources (WARNO, Steel Division 2, Wargame series) + training knowledge where noted

---

## Executive Summary

This document distills how modern real-time tactical wargames (WARNO, Steel Division 2, Wargame: Red Dragon) organize combined-arms forces into battalions and divisions, and extracts principles for a top-down strategic game. The core insight: **battalions feel satisfying because they bundle multiple unit types, self-manage within their role, and feel cohesive on a map.** Moving from individual units to formations eliminates "jarring" scattered silhouettes and instead shows concentrated combat power, making terrain and positioning genuinely matter.

---

## 1. The Battalion/Division Model: Core Principles

### 1.1 Organization Hierarchy (Wargame Series)

In the Wargame and Steel Division lineages, the military structure is:
- **Division** (top strategic pick) → defines the entire roster of available units
- **Brigade/Regiment** (grouping, often implicit)
- **Battalion** (the tactical deployment unit) → composed of 2–6 companies
- **Company** (squad of squads) → 10–20 troops in-game, represents a squad or small platoon
- **Individual units** → soldier-scale abstraction

**Key Design Point:** The *player never directly commands companies*—they command **card-based units**, each representing one or more squads (typically 10–15 troops). Three to four squad-units grouped at a location form a de facto platoon; your deployment of these at different zones constructs a battalion.

### 1.2 The Deck-Building Pre-Game Layer (WARNO & Steel Division 2)

Both WARNO and Steel Division 2 use a **Deck Builder** that separates the strategic (before-match) layer from the tactical (in-match) layer:

#### WARNO Model:
- **Pick a Division** at start (e.g., "US 5th Infantry Division, 1989").
- Spend **50 Activation Points** to build a battlegroup deck.
- **Deck categories** (with slot limits): Infantry, Vehicles, Recon, Support (artillery, transport), Helicopters, Aircraft.
- Each unit card costs 1–5 activation points depending on unit type and rarity.
- Duplicates are allowed (e.g., two copies of "M1A1 Abrams") and cost points each time.

**Why this works:**
- Activation points enforce strategic *composition choices*—you can't spam heavy units; you must balance.
- Categories force combined-arms thinking (you need infantry to hold, vehicles to exploit, support to sustain).
- Division selection gives identity: a Soviet Spetsnaz division plays different from a Soviet Motorized Rifle division.

#### Steel Division 2 Model:
- **Battlegroup** (equivalent to WARNO's deck) is built from a parent Division.
- **Phase availability**: A unit can be added to Phase 0 (deployment), A, B, or C (reinforcement phases). More copies in late phases = fewer available early, incentivizing early-game aggression.
- **Command Points/Activation Points** limit what you can bring per phase.

**Both games enforce:** You can't bring everything. Scarcity in the deck-building layer makes every unit feel precious.

### 1.3 How Divisions Specialize (Captured from WARNO)

Divisions are not interchangeable; they encode national doctrine and unit availability:

- **Armored Division**: Skews toward tanks, fewer expensive units at same AP cost, focuses on breakthrough.
- **Infantry Division**: More infantry slots, cheaper per-unit, defensive focus.
- **Air-Assault/Airborne Division**: Helicopters + elite light infantry, speed over armor.
- **Artillery/Support Division**: Heavy on MLRS, mortars, air support; light on boots on ground.

**Design translation:** This is *roster diversity via faction/unit-type choice*, not pay-to-win. Every division can win; playstyle varies.

---

## 2. Battalion Composition: Concrete Examples

### 2.1 Standard Combined-Arms Battalion (Mechanized/Motorized)

A historically-plausible mechanized rifle battalion (e.g., Soviet motor-rifle, US mechanized infantry):

| Element | Count | Role |
|---------|-------|------|
| **Rifle Squads** (in APCs/IFVs) | 9–12 squads (3 per platoon, 4 platoons) | Core riflemen; hold ground, assault lightly defended positions |
| **Tank Platoon** | 4 medium tanks | Breakthrough, suppress enemy armor, support squads in open |
| **Recon/Scout Section** | 2–4 light vehicles | Scout ahead, spot enemy, provide early warning |
| **Support Weapons** | 1–2 machine-gun or anti-tank teams (towed or mounted) | Covering fire, anti-armor if tanks aren't available |
| **Mortar Section** | 2 tubes (81mm or 82mm) | Indirect fire, smoke, suppression across battalion |
| **HQ Element** | 1 command vehicle + staff | Coordination, morale boost in proximity |

**Psychological Note:** A player sees "mechanized battalion" and imagines APC columns, foot soldiers fanning out, tanks supporting. That *image* is satisfying and makes positioning matter.

### 2.2 Armored Battalion (Heavy Armor)

| Element | Count | Role |
|---------|-------|------|
| **Medium/Heavy Tanks** | 12–15 tanks | Primary combat power; shock value, breakthrough |
| **Tank-Rider Infantry** | 4–6 squads (mounted on tank hulls or in few IFVs) | Ground-hold, clear buildings, support tanks |
| **Recon** | 2 light vehicles | Flank security, intelligence |
| **Anti-Aircraft** | 1–2 AA guns or missile teams | Protect from air, suppress helicopters |

**Feel:** Spearhead formation. Slower, harder to break, but fewer troops to hold a city.

### 2.3 Elite/Rapid Reaction Battalion (Airborne, Spetsnaz, or Commando)

| Element | Count | Role |
|---------|-------|------|
| **Elite Rifle Squads** | 8–10 squads (higher morale, better accuracy) | Assault, hold fortified positions, climb/fast-rope |
| **Light Vehicles** | 2–4 jeeps/quads with mounted guns | Mobility, suppression, rapid repositioning |
| **Dedicated Support** | 1 light mortar, 1 sniper/marksman team | Precision, area denial |
| **Helicopter Transport** | Optional: off-map call-in | Rapid deployment to key zones |

**Feel:** Surgical, high-risk/high-reward. Fewer heavy weapons; survives on elan and training.

### 2.4 Militia/Conscript Battalion (Lower Readiness, Higher Numbers)

| Element | Count | Role |
|---------|-------|------|
| **Conscript Squads** | 15–20 squads (lower morale, slower training) | Numerical advantage, defensive lines |
| **Old/Light Vehicles** | 4–6 older or improvised APCs | Transport, not heavy firepower |
| **Limited Support** | 1 section of older mortars or guns | Basic indirect fire |

**Feel:** Fragile but numerous. Loses cohesion quickly under fire but can swamp in numbers. Good for garrison duty.

### 2.5 Artillery/Support Battalion (Off-Map or Detached)

| Element | Count | Role |
|---------|-------|------|
| **Gun Batteries** | 8 guns (e.g., 6 × 105mm + 2 × 155mm) | Area suppression, preparation strikes |
| **Ammo Transport** | Minimal (mostly off-map) | Logistics abstraction |
| **Minimal Ground Defense** | 1–2 rifle teams | Self-defense only |

**Note:** Typically called in as a **support battalion**, not deployed on-map. Call-in mechanic ties to reinforcement economy.

---

## 3. Unit Scale & Map Readability

### 3.1 Why Individual Units Look "Jarring"

**Problem statement:** Scattered soldier silhouettes on a large tactical/strategic map:
- Create visual noise; hard to parse at a glance what's happening.
- Don't correspond to how militaries actually organize (no one commands individual squads; even a squad lead commands 8 troops, not 1).
- Make terrain irrelevant: a lone soldier behind a hill *looks* identical to one in open.

### 3.2 Why Battalions Feel Better

**Visual clustering:**
- A battalion-scale icon (or stacked sub-unit icons) tells you "here is concentrated combat power."
- Multiple icons in one hex/sector reads as "this area is held by this side"—front lines become obvious.
- Gaps between formations create *tactical space* (room for maneuver, vulnerability).

**Mechanical coherence:**
- Casualties affect the whole formation (morale check).
- Suppression propagates within the battalion (nearby squads hunker down).
- A tank supporting infantry *visibly* changes the formation's silhouette.

### 3.3 Representation Options for a Top-Down Map

#### Option A: Stacked Unit Icons
- Each battalion shown as 3–5 small unit cards stacked/offset (infantry, tank, support).
- Hovering or clicking shows full composition.
- **Pros:** Intuitive, shows unit type at a glance. **Cons:** Can be visually dense if many battalions visible.

#### Option B: NATO-ish Formation Symbols (MIL-STD-2525)
- Battalion represented as a **framed rectangular icon** with symbology indicating:
  - Size (Battalion ≈ 500–1000 troops)
  - Type (Infantry, Armor, Airborne)
  - Nationality/faction (color or emblem)
  - Status (e.g., "suppressed," "entrenched," "moving")
- **Pros:** Military-familiar, scales well, compact. **Cons:** Less intuitive to new players without training.

#### Option C: Clustered Sub-Unit Markers
- A single macro-hex contains 2–6 smaller icons (infantry squad, tank, etc.) rendered as a loose cluster.
- The cluster *moves together*; individual squads can't split.
- **Pros:** Shows internal composition without overwhelming. **Cons:** Requires custom rendering.

#### Option D: Hybrid (Recommended for This Game)
- **Zoom-out (strategic map):** Show battalion as single **formation icon** with NATO-style symbology + faction color.
- **Zoom-in (tactical map):** Expand to show stacked sub-unit icons (each card represents a squad or vehicle).
- Allows both scale readability and granular visibility where it matters.

### 3.4 Visual Coherence with Terrain

**Why it matters:**
- A formation in an urban hex should visually cluster *inside the city blocks* (not floating above).
- A formation in a forest should show partial obscuration (squad icons half-visible, suggesting concealment).
- A formation on open plains should be *widely visible* (high spotting range, vulnerability).

**Implementation:**
- Battalion icon includes a **terrain modifier** (visual effect: smoke, dust, building silhouette).
- Unit cards within the formation tint/scale based on cover (e.g., infantry in a building appears darker/smaller).

---

## 4. Infantry in Cities & Urban Garrisoning

### 4.1 Why Urban Garrisoning Feels Satisfying

**From tactical wargames (Cityfight, Broken Arrow, Wargame):**
- **High cover bonus:** Infantry in buildings gains 2–3× cover compared to open ground.
- **Attrition flip:** Assaulting a garrisoned building is expensive (attacker losses 3–5×, defender losses 1–2×). This creates **meaningful choice**: assault or bypass?
- **Morale/suppression asymmetry:** Garrisoned troops hold longer before routing; assaulting troops suppress more easily due to fear.
- **Small unit coherence:** A squad in a single building acts as one fighting compartment; they support each other's fire.

**Psychological reward:** The player feels like they've "locked down" an area. The enemy can't easily dislodge them without heavy support.

### 4.2 How Cities Are Structured in Operational Wargames

#### Holdable Points Model (WARNO, Steel Division 2):
- Cities are divided into **districts or capture zones** (often 2–4 per city).
- Each zone can be held by one side or contested.
- Infantry in a zone gets cover bonus; controlling zone gives income or strategic advantage.
- **Example:** A city with 4 districts—if you hold 3, you control the city for scoring purposes.

#### Block-by-Block Model (Tabletop Wargames like Cityfight):
- City map shows **city blocks** (buildings, streets, squares).
- Each building is a discrete garrison location (0, 1, or 2 squads max).
- Streets between buildings are kill zones.
- **Feel:** Asymmetric, intense, slow-paced (each building takes time to clear).

#### Nodes & Districts Model (Strategy Game Inspiration):
- City identified by **key nodes** (town center, market square, barracks, gates).
- Controlling a node provides a local buff (recruitment, resource, supply).
- Garrison units occupy multiple nodes, not the whole city.
- **Feel:** Strategic depth; controlling the town center is more valuable than the outskirts.

### 4.3 Infantry Garrisoning Rules (Mechanics to Adapt)

**From WARNO & Steel Division 2:**
- **Garrison state:** Infantry in a city hex toggle garrison mode (stationary, +defense, -mobility).
- **Cover bonus:** Garrisoned units receive **+50% to +200% defense** (depending on city fortification level).
- **Firing from windows:** Garrisoned infantry can shoot out at a distance (range modifier), but enemies can't shoot in effectively (cover applies).
- **Assault penalty:** If attacker enters a garrisoned hex:
  - Attacker takes suppression/morale hit (fear of urban combat).
  - Defender gets to resolve first (reaction fire).
  - Combat is hand-to-hand/close-quarter (higher casualty rates, morale checks every round).

**Specific rules for this game (suggestion):**
```
GARRISON RULE (Pseudo-code for clarity):
- Infantry unit in city can toggle GARRISON mode (costs 1 action point per turn to re-activate).
- Garrisoned: +100% defense (armor/cover), +50% firepower (overwatch from buildings).
  Penalties: Can't move, vision reduced (only see adjacent hexes + streets).
- Assault into garrisoned city: 
  Attacker must spend 1 extra action point, takes morale check (-2 to morale).
  Defender gets reaction fire (+1 to hit).
  Casualties doubled for attacker; halved for defender.
```

### 4.4 City Visual Representation & Generation

**Current Problem:** Procedurally generated cities are abstract (hex-based, no visual "city-ness").

**Solutions to inspire redesign:**

#### Layered City Hex:
- **Outer ring:** Suburban sprawl (light cover, moderate defense).
- **Middle ring:** Dense blocks (heavy cover, high defense).
- **Center:** Strongpoint (citadel, town hall)—highest defense, garrison-only.
- **Visual:** Each ring rendered with increasing building density, darker color for defender advantage.

#### District Subdivision:
- Each "city" hexagon subdivided into 4–6 **districts** (similar to Total War's settlement siege system).
- Districts have independent garrison capacity (e.g., "Market District can hold 2 squads").
- Assaulting a city = assaulting one district at a time; each district has its own morale/defense pool.

#### Landmark-Based City:
- Procedurally place 2–4 **landmark nodes** within a city (temple, market, barracks, gate).
- Controlling a landmark gives a local bonus (garrison faster, defend easier, supply regenerates in city).
- Visual: Each landmark shown as an icon; infantry near it get a subtle glow (indicating control/buff).

#### Recommended for This Game:
Combine **Layered City Hex + Landmark Nodes**:
- A city hex shows 3 rings (outer, middle, center) with different defense values.
- Place 1–2 landmarks (temple, barracks) within the city.
- Infantry garrisoned in the center ring get **+100% defense**; outer ring **+50%**.
- Landmarks provide secondary buffs (supply regen, faster garrison reinforcement).
- Visual: Rings are semi-transparent, layered on terrain; landmarks are glowing icons.

---

## 5. Economic/Reinforcement Angle

### 5.1 WARNO's Activation Point Economy

**In-Match Flow:**
- **Match start:** Players have their pre-built deck (50 AP worth of units).
- **Initial deployment (Phase 0):** Place units from deck within activation point limit for the phase.
- **Reinforcements (ongoing):** **Command Points** accumulate at a fixed rate (~+7 per time unit, baseline).
- **Purchase reinforcements:** Spend accumulated command points to call in units from the deck again.
- **Reinforcement sector:** Must control a reinforcement logistics zone to pull in units; zones give +2 CP/turn each.
- **Scarcity:** Once you've deployed a unit card from the deck, it's on cooldown (unavailable until it's destroyed or respawns).

**Economic Feel:**
- Early game: You have 50 AP to spread across the map. You *must* choose—can't blob everything to one point.
- Mid-game: Casualties mount; you call in more, but CP accumulation is slow without reinforcement zones.
- Late game: Who controls the supply/reinforcement zones wins—not who has the most units, but who can feed them.

**Incentive:** Holding territory *directly enables unit production*. This is the core loop.

### 5.2 Steel Division 2's Phased Reinforcement

**Different approach:**
- **Phase 0 (Deployment):** Initial battlegroup units placed.
- **Phases A, B, C (10 minutes each):** Each phase unlocks a new set of reinforcements from the deck.
- **Attrition loop:** As units die, you call in copies from the deck for that phase.
- **Phase-locking:** A unit available only in Phase A can't be replaced after Phase A ends—forces forward commitment.

**Economic Feel:**
- **Early commitment:** Must spend heavy units in early phase; if you hold them back, enemy gets initiative.
- **Late-game desperation:** Phase C units are last-stand reinforcements; if you're losing by then, they don't save you.
- **Time pressure:** The match is inherently short (30 min). Economic decisions are made under pressure.

### 5.3 Command Modern Operations (from training knowledge)

**[from training, unverified]**
- Force **pool** represents your army.
- Units are pulled from pool and deployed to theater.
- Each unit has readiness/availability; can't re-deploy until rested.
- Resources (fuel, ammo, personnel) flow to units in theater; supply lines matter.
- Can **request reinforcements** from higher command (national pool), but it's slow and political.

---

## 6. What Makes It Feel Good: Combined Arms & Cohesion

### 6.1 Suppression & Morale System

**From Wargame series & Delta Vector game design blog:**

The satisfying core loop:
1. **Infantry advances** (risky, exposed).
2. **Enemy shoots**, infantry is **suppressed** (takes morale damage, moves slower, can't shoot as well).
3. **Your tanks cover** them with return fire, **suppressing the enemy**.
4. **Infantry advances again**, leveraging the moment.

**Why satisfying:**
- Every unit class has a role (infantry + armor aren't interchangeable).
- Timing matters (don't rush; wait for support).
- Cover and terrain create *natural chokepoints* (gullies, woods, buildings).

### 6.2 Front Lines & Formation Feeling

**Key insight:** A player should feel like they're managing a **shifting front line**, not individual skirmishes.

- Battalions hold a **sector** (local area, 1–3 hexes).
- Gaps between sectors are vulnerabilities (enemy can exploit).
- Overlapping sectors = support zones (nearby units get buff, suppress together).
- Collapsing one battalion causes a **cascade effect** (adjacent battalions' morale shakes, units route).

**Implementation:** Battalions have a **cohesion meter** (separate from health):
- Cohesion decays when isolated, no nearby allies, or taking heavy fire.
- Cohesion breaks → morale check → units rout.
- Reachable via reinforcement → cohesion recovered (battalion "re-forms").

### 6.3 Recon & Spotting

**Why it matters:**
- Fog of war is enforced by **recon units' vision range**.
- A battalion without dedicated recon is nearly blind (limited spotting).
- Recon squads feed intelligence → enabling ambush/counter-ambush.

**Feeling:** "I didn't see them coming" vs. "My scouts reported enemy movement; I laid a trap." Same outcome; different narrative.

---

## 7. Design Translation for This Game

### 7.1 Proposed Battalion System for Total Mobilization

Below is a **suggested** framework for adapting the above principles to this game's specific context:

#### What Is a Battalion (In This Game)?

A **battalion** is:
- A persistent **formation entity** that self-manages within its role.
- Composed of **3–6 sub-unit cards** (infantry, armor, support, logistics).
- Produced by the **economy** (factories make elite variants, army sends standard + militia).
- Deployed to a **sector/city** for local control.
- Has **morale/cohesion** (can rout if isolated or shattered).
- Visible on the **operational map** as a single icon with internal sub-unit detail on hover/zoom.

#### Battalion Production (Economy Integration)

**Three sources of battalions:**

1. **Factory Elite Battalion** (high cost, few available):
   - Built by: Armor factory or weapons plant.
   - Composition: 2 tanks, 6 rifle squads, 2 support weapons, mortar section.
   - Cost: 150 industrial points (slow to produce, 3–4 turns).
   - Feel: Your ace card; rare, powerful, limited.

2. **Army-Sent Standard Battalion** (moderate cost, regular supply):
   - Called in via: Recruitment / higher command requisition.
   - Composition: 1–2 tanks, 8–10 rifle squads, support weapons.
   - Cost: 80–100 industrial/mobilization points (faster, 1–2 turns).
   - Availability: Tied to global mobilization economy (can't spam if industrial base is low).
   - Feel: The backbone; reliable, not elite.

3. **Local Militia Battalion** (low cost, immediate availability):
   - Raised from: Population in controlled cities.
   - Composition: 12–15 conscript squads, 1–2 old vehicles, light weapons.
   - Cost: 30 industrial + 10 population (instant or 1 turn).
   - Feel: Fragile, numerous, good for garrison/holding ground temporarily.

**Balancing Principle:** A player can quickly raise militia to garrison cities, but elites are earned. This creates *composition strategy* (do you bulk up with militia or save for one elite battalion?).

#### Deployment & Self-Management

**On the map:**
- A battalion is assigned to a **sector** (city or strategic location).
- It **auto-defends** that sector: infantry garrison buildings, tank supports, support weapons set up overwatch.
- Player specifies **stance**: Defensive, Balanced, Aggressive.
  - **Defensive:** Garrison tighter, less movement, more suppression resistance.
  - **Aggressive:** More patrols, higher spotting range, risky (easier to suppress/rout).

**Movement between sectors:**
- Battalions **self-deploy** via roads to adjacent sectors (1 turn per hop).
- Player doesn't micro individual squads; they say "2nd Battalion moves northeast to Hill 47" and it goes.
- En route, battalion is vulnerable (strung out, can be ambushed).

#### Garrisons & Urban Control

**City Garrison Rule (Simplified from Section 4.3):**
- A battalion entering a city **automatically garrisons** it (infantry into buildings, tank on main street).
- City provides **+100% defense** (cover bonus).
- Garrisoned battalion's morale decays if isolated (represents siege pressure).
- **Assault into a garrisoned city:**
  - Attacker takes -2 morale, -1 to hit.
  - Defender gets +50% firepower (overwatch), reaction fire.
  - Casualties: Attacker 2×, Defender 0.5×.
  - Cost: High. Assaulting a city should only happen if overwhelming force or attacker desperation.

#### Reinforcement/Call-In Mechanic

**Battalion reinforcements tied to:**
- **Reinforcement points** (analog to WARNO's command points).
- Accumulate per turn: +5 base, +2 per reinforcement sector held.
- **Call in a battalion:** Costs 80 RP for standard, 150 RP for elite, 30 RP for militia.
- **Cooldown:** Called-in battalion can't be re-called for 3 turns (represents reformation time).

**Feel:** Holding strategic zones isn't just scoring; it *enables* more unit production in-game. Loss of a reinforcement point = immediate strategic setback.

#### Visual Implementation

**Operational Map:**
- Battalion shown as **framed icon** with symbol indicating type (infantry, armor, elite, militia).
- Color by faction; icon includes up-to-date silhouettes of internal units (click to expand).
- If in garrison mode → icon shows a building overlay (visually indicates garrison status).
- If suppressed/routed → icon dims/tints red.

**Zoom into sector:**
- Formation shown as **stacked card layout** (tank card, infantry squad 1, infantry squad 2, support, etc.).
- Cards arranged in a **loose cluster** within the hex (infantry grouped in buildings, tank on street, mortar on hills).
- Card position indicates position within the city (not free to move; bound to sector).

#### Morale & Cohesion

**Battalion morale state:**
- **Fresh:** Full strength, defensive bonus +20%.
- **Steady:** ~75% strength, no modifier.
- **Shaken:** ~50% strength, -10% defense, risky routing.
- **Broken:** Routing, fleeing sector, can't fight (regroup if reaches friendly territory).

**Morale decay (per turn):**
- -5 if isolated (no adjacent friendly battalion).
- -10 if under fire from range.
- -15 if assaulted in melee.
- +5 if in own territory (city/controlled sector).
- +10 if reinforced (fresh battalion arrives nearby).

**Morale check (roll if threshold crossed):**
- If morale < 30%: Check against battalion coherence (some units rout locally, battalion fractures).
- If < 10%: Entire battalion routes (removed from map, can re-garrison city if fleeing to friendly territory).

---

### 7.2 Map Interaction: Battalion Visibility & Fog of War

**Spotting System:**
- Each battalion has a **vision radius** based on its recon capability.
  - Elite + dedicated recon: 3–4 hexes.
  - Standard + some recon: 2–3 hexes.
  - Militia + no recon: 1–2 hexes.
- Recon units in the battalion can spot enemies at range; infantry can't (limited vision).
- If a battalion loses recon, vision drops drastically next turn (represents scouts being killed).

**Fog of War Interaction:**
- Opponent sees your battalion icon only if *your* recon has spotted them and relayed it.
- Else, opponent's view is blank (hidden unit, unknown disposition).
- When spotted, icon shows full detail (composition, morale state).

**Reason:** Recon becomes a *real resource* (can't just stare at the enemy; you must scout).

---

### 7.3 Example Early-Game Scenario

**Turn 1: Player starts with 3 battalions:**
- 1 Elite armor battalion (factory-built, 150 IP cost, already deployed at Home Base).
- 2 Standard infantry battalions (army-sent, already deployed at two border cities).
- Militia option available: can garrison cities from population (30 IP + 10 pop each).

**Decision:** Do you:
1. **Garrison militia in rear cities** (defensive, economy-heavy)? Costs population but secures rear.
2. **Push elite battalion forward** to seize an enemy city (aggressive, risky)? Concentrates force but leaves rear open.
3. **Hybrid:** Keep elite in reserve, push one standard battalion, raise militia in two cities.

**Result:** Each choice changes your strategic posture. The economy and roster become the game, not micro.

---

### 7.4 Suggested Battalion Stat Block (For Implementation)

```
BATTALION: "2nd Armored"
Type: Elite Armor
Faction: Player

Composition:
  - Armor: 4× M1A1 Tank (health: 40 each, armor: 8)
  - Infantry: 8× Rifle Squad (health: 15 each, morale: 60)
  - Support: 2× Support Weapon (health: 20 each, firepower: 5)
  - Logistics: 1× Supply Truck (off-map, auto-managed)

Stats:
  Strength: 450 / 450 (health pool)
  Morale: 75 / 100
  Firepower: 18 (avg damage per round)
  Armor: 6 (damage reduction)
  Vision: 4 hexes (has recon attachment)
  Speed: 2 hexes/turn
  Cohesion: 80 / 100 (linked to isolation/suppression)

Position: Sector "Hill 47" (garrison mode: defensive)
Status: Steady (no debuffs)
Stance: Defensive (+defense, -mobility)

Abilities:
  - Suppress (reduce enemy morale at range)
  - Entrench (garrison city, +100% defense)
  - Regroup (if shaken, recover morale over 2 turns)
  - Call Mortar Strike (support ability, area effect, 80 RP cost)
```

---

## 8. Comparison: What Changes with Battalion System

### Before (Individual Units)
- Player commands 20+ individual squads/vehicles.
- Each unit has independent morale, movement.
- Map is visually noisy (many small icons).
- Terrain doesn't obviously interact with units (a tank looks the same in forest or plains).
- Victory = eliminate all enemy units (grind).

### After (Battalion System)
- Player commands 3–6 battalions (simplified decision space).
- Battalions self-manage within stance; player orchestrates positioning.
- Map is readable (battalion icons + sub-unit detail on hover).
- Terrain modifies battalion behavior (garrison in city, suppress in forest, rout in open).
- Victory = control cities/reinforcement zones (territorial).

**Net effect:** Game shifts from *tactical micromanagement* to *operational strategy*.

---

## 9. Research Gaps & Uncertainties

The following areas could benefit from deeper research or design prototyping:

1. **Supply Line Mechanics:** How much should battalions depend on supply chains? (WARNO handles supply implicitly; total war makes it explicit.) For a procedural game, implicit (auto-managed) is likely better.

2. **Veterancy/Training System:** Should units improve over time in combat? (Steel Division and Wargame do this; adds depth but complexity.) Deferred to future iteration.

3. **Commander/General System:** Do battalions have individual commanders with unique abilities (Total War style)? Or are they generic formations? This affects roleplay/identity.

4. **Air Support & Off-Map Assets:** How are air forces, artillery, and off-map reinforcements handled? Currently assumed as support abilities (call-in costs RP). Needs integration with reinforcement zones.

5. **Map Scale Ambiguity:** If a hex = 1 km (operational scale), a battalion fits one hex. If hex = 100m (tactical scale), battalion spans several hexes. This affects city size/garrison rules.

6. **Procedural City Generation:** Current city hexes are abstract. A redesign should make cities *look* like cities (multi-district, landmarks, buildings). Requires art/rendering pipeline.

---

## 10. Sources & References

**Web sources:**
- [WARNO Fandom Wiki: Divisions](https://warno.fandom.com/wiki/Divisions)
- [WARNO Beginner's Guide (Magic Game World)](https://www.magicgameworld.com/warno-ultimate-beginners-guide-tips-tricks-for-new-players/)
- [Steel Division 2 Manual (Eugen Systems)](https://eugensystems.com/steel-division-2-game-manual/)
- [Wargame Red Dragon: Military Organization (NamuWiki)](https://en.namu.wiki/w/%EC%9B%8C%EA%B2%8C%EC%9E%84:%20%EB%A0%88%EB%93%9C%20%EB%93%9C%EB%9E%98%EA%B3%A4/%EC%BA%A0%ED%8E%98%EC%9D%B8)
- [Urban Warfare & Garrisoning (Wargamer, TV Tropes)](https://tvtropes.org/pmwiki/pmwiki.php/Main/UrbanWarfare)
- [Cityfight: Modern Combat in the Urban Environment (BoardGameGeek)](https://boardgamegeek.com/boardgame/4436/cityfight-modern-combat-in-the-urban-environment)
- [Delta Vector Game Design: Suppression & Morale](https://deltavector.blogspot.com/2024/04/game-design-105-suppression-pinning-aoe.html)
- [Military Tactical Symbols (MIL-STD-2525, map.army)](https://www.map.army/doc/en/symbols/symbol-gallery/)
- [Urban Design & Game Cities (Medium, Game Developer)](https://medium.com/@KonstantinosD/urban-design-and-the-creation-of-videogame-cities-f56449f74d7f)

**Training knowledge:**
- WARNO/Wargame series general design principles.
- Total War series army composition and general abilities.
- General military doctrine (battalion organization, combined arms).

---

## 11. Next Steps for Design

1. **Prototype battalion stat block** (test balance with three types: elite, standard, militia).
2. **Sketch city hex redesign** (Photoshop/Figma mockup with 3-ring layers + landmarks).
3. **Playtest roster economy** (can players still make interesting choices with limited elite battalions?).
4. **Test morale/suppression** (do players feel "front line" pressure with cohesion system?).
5. **Measure map readability** (can new players instantly understand battalion positions at a glance?).

---

**End of Reference Document**
