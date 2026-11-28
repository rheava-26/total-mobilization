# NAVAL COUNCIL: Round 1 Position Paper
*Admiralty of the Seas. Slug: `naval`.*

## 1. Opening Speech

Honourable members. Seventy-one percent of this planet is water, and the last war report fielded three ships. Three. My own harbour pilot has more tugboats.

The sky navy has stolen our fantasy. Someone bolted a keel onto a zeppelin, called it a "capital-class sky ship", and sent 138 of them to do a destroyer's job. Fine. Fly your zeppelins. But they never *dive*, and the sea is the one place the Autofleet has never thought to look.

The Admiralty's complaint is structural. Our ships sit on a flat painted line, locked inside whichever puddle they spawned in (`waterBounds`). Nothing hunts them. Nothing hides beneath them. They are unlocked at game start, so no research ever makes them *better*. A navy that costs nothing to unlock and nothing to lose is a navy nobody builds.

We do not ask for buffs. The Tenet forbids us nerfing anyone, and we would never insult the Air Force by nerfing them. We ask for a *third dimension downward*, a reason for the sea to matter economically, and an enemy that comes for the water. Give us depth, give us cargo, give us a canal, and we will give you a fleet that makes the Autofleet reconsider its life choices.

The sea is not a backdrop. It is the largest unclaimed resource on the map.

*(The Air Council may address the chair after I have finished. They will not be recognised.)*

## 2. The Gap

- **No navy fielded.** Report 8 had 3 ships against 240 Falcons, 113 Kites and 138 sky capitals. The sky capital tier (`grandbattleship`, `supercarrier` at L3283-3284 are the *naval* capitals, but the sky ones outsell them).
- **Zero research depth.** `navalpwr` and `kinetics` are pre-set in `unlocked` (L3588). Every ship is available at second zero. A tree node never changes a ship's role.
- **The sea is one flat line.** `seaLevelY(wx)` (L1533) is the entire vertical model. There is no depth, no below, no hidden.
- **Ships are trapped.** Naval AI clamps `u.x` to `u.wx0/u.wx1` from `waterBounds` (L3760, L4249, L8123-8137). A fleet cannot leave the lake it spawned in, so on a map with several waters it is a set of isolated garrisons, not a navy.
- **Nobody attacks the sea.** `spawnEnemy` (L5581) and `doSkyfall` (L5206) pick land or sky. `scanEnemyIntel` reads a `navy` weight (L5742) and the fleet "adapts", but it adapts with nothing naval to adapt *with*.
- **Strategic resources are idle, and the ocean holds them.** The SR table (L3173) is four map nodes captured by an Extractor (L3387). A known flaw. Seawater carries uranium and the seabed carries nodules of manganese, nickel and cobalt, and nothing in the game touches either.
- **Dead ground.** Water is a build restriction (`needWater`: Shipyard L3391, Heavy Port L3464) and a drowning rule (L6561). It is never a *resource*, a *road*, or a *weapon*.
- **The Ballistic Submarine** appears in CLAUDE.md as a dispersed launcher. A grep of `title.html` finds no sub unit anywhere. The designer's own canon lists a submarine we do not have.

## 3. Bills

### NAVAL-1: The Silent Service (Submarines and the Depth Band)

**Pitch.** *Members, the Autofleet scans the sky with everything it has. Nobody has told it that Earth has a second ocean beneath the first. We propose three hulls that live under the line. The enemy cannot shoot what it cannot find, and neither can your own side's idiots.*

**Player fantasy.** The cold, quiet half of the war. You park a hidden boat under a coast and your ballistic strikes launch from somewhere nobody can bomb. Then a Trench Hunter surfaces under a bomber formation and the sea turns white.

**Clerk's Annex.**
- **Mechanic.** Add `u.depth` (0 surfaced, 1 shallow, 2 deep) to naval units flagged `sub:true`. Submerged units sit at `seaLevelY(x) + 14 * depth` and are removed from `acquireEnemy`'s grid for *enemy* shooters (`e.noTarget`-style flag on the player side, `u.hidden`) unless detected. A sub is detected for 3s after it fires, when it surfaces, or inside a `sonar` aura. Three hulls:
  - *Hunter-Killer "Pike"* (IC 230, MP 18, HP 140, armor 6, speed 14): torpedo, 90 dmg, range 700, hits surface ships and ground units on the shore only. Immune to air attack while depth>=1.
  - *Ballistic Boat "Trident"* (IC 520, MP 30, HP 260): `strat:true` launcher. It fires strategic strikes from a position no raid can reach. This finally implements the CLAUDE.md "Ballistic Submarine" with a real reason to exist.
  - *Cruise-Missile Boat "Tomahawk"* (IC 340, MP 22, HP 180): a 6-round vertical-launch cell salvo of 40 dmg guided missiles, resurfacing to reload (reuse the air `ammo` / `rtb` logic).
- **Real science.** A *thermocline* (a sharp temperature gradient layer, typically 100-300 m down) bends sound and hides submarines from sonar above it. SSBNs are survivable second-strike forces precisely because the ocean is acoustically opaque. Seawater absorbs radar and EM almost completely, so a submerged boat is invisible to air-launched radar. This is how real navies think about it.
- **Counter, not cap.** The enemy gets an ASW answer so subs stay a *choice*: the new `Trench Hunter` kind (see NAVAL-4) and `scanEnemyIntel`'s `dominant` can add `'subs'`, steering the fleet's `kindWeights` toward sonar-equipped hunters. Scarier enemy, not a cap on submarines.
- **Code grounding.** `UNIT_DEFS` L3184 (naval block L3201-3213, `domain:'naval'`); naval movement/clamp L8123-8137; `acquireEnemy` L4374 (add a `u.hidden` skip for hostile shooters, mirroring `e.noTarget`); strike origin `armSilo` / `strat:true` buildings L3407 and `bldSurfaceY` L3671, `STRAT` L6878 (add `strat:true` to the unit and treat as launcher); rendering at L9348-9350 (draw submerged hulls as translucent, alpha .45). Detection state in `updUnits` L7809.
- **Build size.** L (about 500 lines): depth state, hidden flag, 3 units + art, detection pulse, enemy ASW counter.
- **TM1.**
- **Research tree.** New node `sonar` "Sonar and Silent Running" (branch `def`, pre `pointdefense`, tier 2, cost ~130 after the 1.6x multiplier, not dangerous). Unlocks Pike. `ballisticboat` gate: combo node `deepsea` (pre `sonar` + `spaceage`, tier 3, cost ~260, not `dang`) unlocks Trident and Tomahawk. Combo partner: `particlebeam`/`plasma` (see NAVAL-3 for the world-changing combo).
- **Tenet check.** Pure power fantasy: more units, a hidden launcher, no caps. The cost is the enemy's ASW answer, not a limit on the player.
- **Interactions.** Strategic Council (Trident is a dispersed launcher; spreads strike risk). Air Council (a sub is immune to their bombers, a new counter-class). Enemy Council (Trench Hunter, ASW bias in `scanEnemyIntel`).

### NAVAL-2: The Blue Economy (Seawater Uranium, Nodules, and OTEC)

**Pitch.** *Honourable members, you have spent six reports ignoring Uranium, Steel and Rare Earth because you must find a magic rock. Gentlemen, the whole sea is a magic rock. Four and a half billion tonnes of uranium dissolved in it, and the bottom is paved with metal potatoes.*

**Player fantasy.** A port city becomes an industrial heartland: floating refineries, an ocean thermal plant humming on the horizon, and the strategic resource problem solved by *owning the coast* rather than a lucky map node.

**Clerk's Annex.**
- **Mechanic.** Three new `needWater` buildings (the existing `needWater` rule L3871 and `bldSurfaceY` L3671 already place them at sea level):
  - *Seawater Uranium Adsorber* (IC 220, MP 60): grants `G.owned.uranium` after ~25s of operation, **without a map node**. Each additional adsorber adds +1 IC/s stability-free "yellowcake" bonus to Uranium Enrichment output (`gmod.ppBonus`).
  - *Nodule Dredger* (IC 260, MP 80, needs a water body wider than 400 px so it has seabed to dredge): grants `G.owned.rare` and `G.owned.steel` after ~30s, and `econ: {ic: 6}`.
  - *OTEC Platform* (IC 320, MP 90): `econ: {ic: 10}`, scaled by `waterBounds` width (bigger ocean, more thermal gradient). Harmless to build in bulk; no cap.
- **Real science.** *Uranium from seawater*: Japanese and US labs have extracted real uranium with amidoxime-functionalised polymer fibre braids (about 3 ppb concentration, trapped over weeks in tidal flow). *Polymetallic nodules* (manganese, nickel, cobalt, rare earths) lie across the Clarion-Clipperton Zone seabed and are the subject of real deep-sea mining claims. *OTEC* (ocean thermal energy conversion) uses the roughly 20 K gap between warm surface and cold deep water to run a heat engine, and has been built at pilot scale in Hawaii.
- **Why this fixes a known flaw.** The designer's old intended fix was "make strategic resources grant bonuses rather than act as requirements". This does exactly that and also gives coastal land a reason to be taken. `srMult` (L3810) already keys discounts off `G.owned.*`.
- **Code grounding.** `SR` L3173; `BUILD_DEFS` L3381 (model on `extractor` L3387 and `uranenrich` L3448); `mkBuilding` L3670; `G.owned` writes where `onNode` extractors complete; `resourcesCaptured` stats L3608 (extend so the after-action report shows ocean-sourced resources). `stratCost` L6889 consumes uranium, so the first Trident/MIRV discount becomes available from a harbour.
- **Build size.** M (about 250 lines): 3 building defs, a `G.owned` write path, art, stats rows.
- **TM1.**
- **Research tree.** New node `deepsea` (from NAVAL-1: pre `sonar` + `spaceage`) unlocks the Dredger. The Adsorber unlocks at `fusion` (a real polymer-chemistry plant, no sci-fi). OTEC unlocks at `directedenergy`. Combo that changes the world: **`deepsea` + `antimatter`** makes the Dredger yield Rare Earth fast enough to meet Antimatter's `srReq` ('rare') without any map deposit, so the naval economy opens a research path the land never could.
- **Tenet check.** Gives more income and more options; no cap, no denial. The "real cost" is infrastructure that is vulnerable at sea (see NAVAL-4).
- **Interactions.** Economy Council (`G.owned`, upkeep, surplus-IC outflow slice; the 212k unspent IC problem gets a sink that *produces* instead of a button). Strategic Council (cheaper strikes without a node). Terrain Council (needs the water to exist).

### NAVAL-3: The Great Canals (Fleet in Being)

**Pitch.** *My ships are prisoners of their own bathtub. A man who hires a fleet and then locks each ship in a separate pond has not built a navy, he has built a koi collection.*

**Player fantasy.** You take the terrain shovel the designer already gave you and cut a canal across the continent. For the first time, your fleet sails from one sea to the other and arrives at the front like a thunderclap.

**Clerk's Annex.**
- **Mechanic.** Ships currently clamp to their `waterBounds` interval. Change the interval computation so that *connected* water (two bodies joined by a continuous run where `elevAt < SEA_E`) merges, and let SINK-mode terrain editing (`TERRAIN_MODES`, existing) cut such a channel. Add a *Canal Lock* building (IC 150, MP 40, `needWater`) that, placed in a canal, lets ships pass a land gap up to 240 px wide by treating `lockSpan` as water for `waterBounds` only (units cross at half speed). Also: *Fleet in being.* A group of 6+ ships anchored together raises `G.mult.navalrng` slightly and grants a morale-free "presence" aura that biases enemy `threatWeight` toward the fleet, drawing fire off cities. The goal is a visible reason for a *massed* navy rather than three ships.
- **Real science.** The Suez, Panama, Kiel and the Grand Canal exist because moving a navy across land is worth cutting the land. Locks equalise water levels with gravity alone. A *fleet in being* is a real strategic doctrine: a large unengaged fleet ties down an enemy simply by existing.
- **Code grounding.** `waterBounds` L3760 (and its caching on `TERR_EDIT` L1470, L8125 `u._wbV`); naval clamp L4249 and L8123-8137; `TERRAIN_MODES` / terrain tool (SINK path in `elevAt` L1494-1496, which already "deepens seas"); `findWater` L3758; building placement check L3871. `threatWeight` in `acquireEnemy` L4374.
- **Build size.** M (about 200 lines): lock building, merged-bounds logic, preview of the connected sea.
- **TM1.**
- **Research tree.** New node `harbourworks` "Hydraulic Engineering" (branch `def`, tier 1, pre `info`, cost ~90, not dangerous). Unlocks the Canal Lock and a cheaper SINK cost near water. Combo: `harbourworks` + `nanotech` = **self-dredging channels** (canals refill their own silt: no upkeep as nanite swarms keep the cut open).
- **Tenet check.** Strictly more freedom for the player; the old restriction is lifted, nothing is capped.
- **Interactions.** Terrain Council (every canal is a coastline edit; they must agree on SINK cost). Logistics Council (shipping lanes between cities). Ground Council (water barriers vs `passable` L1536: a new canal also blocks *your own* ground units, a real cost).

### NAVAL-4: The Autofleet Comes for the Sea (Trench Landers)

**Pitch.** *We cannot be the only navy afraid of the water. The Autofleet's own canon says it fears industrialising Earth. The most industrial place on Earth is the harbour. Send something to burn it.*

**Player fantasy.** The sea becomes a front. A glowing fleet-pod drops into the ocean, and your fleet turns to meet it. Naval combat is *about* something, instead of ornamental.

**Clerk's Annex.**
- **Mechanic.** Two new enemy kinds, spawned in `doSkyfall`/`spawnEnemy` at water x-coordinates (use `findWater`):
  - *Trench Lander* ("Anchor Seed"): lands, anchors at sea level, 900 HP, armor 14, **produces** a *Trench Hunter* every ~12s (like the Harvester, which "lands, anchors, keeps producing").
  - *Trench Hunter*: a sub-hull drone (speed 16, 70 HP, torpedo 55 dmg, range 400) that attacks naval units and `needWater` buildings (including the NAVAL-2 platforms). At depth>=1 it can only be hit by ASW weapons or sonar-revealed targets.
  - `INTENT` L5119 adds `seaLandings: n` to the invasion intents (not recon).
- **Real science.** Modern navies treat the *littoral* as the hardest ASW environment: shallow, noisy, thermally mixed. A drone that lands, anchors and breeds mirrors the real "unmanned undersea vehicle" mothership concept.
- **Why it belongs.** It is a *scarier enemy*, the Tenet's chosen lever, and it gives `scanEnemyIntel` something to adapt against (L5742 `navy` weight).
- **Code grounding.** `INTENT` L5119; `doSkyfall` L5206; `spawnEnemy` L5581; Harvester pattern in `updEnemies` L8596; `scanEnemyIntel` L5742; `acquireEnemy` L4374 (hidden-target test shared with NAVAL-1).
- **Build size.** L (about 450 lines): two kinds + art, spawn hook, depth-aware targeting, intent tuning.
- **TM1.**
- **Research tree.** None needed (enemy content), but it is the *test* for `sonar` (NAVAL-1). Without Sonar, Hunters can be fought only on the surface.
- **Tenet check.** Raises enemy threat rather than restricting the player. Wins stay possible: the player's answer is the fleet they now want to build.
- **Interactions.** Enemy Council (new kinds, intent weights). Strategic Council (Trench Landers at the exact water coords strategic strikes can reach with Kinetic Lance). Economy Council (puts NAVAL-2 platforms at risk, the "real cost").

### NAVAL-5: Ekranoplan Wing (Sea-Skimming Strike)

**Pitch.** *A ship that flies four metres above the water at 500 km/h, carrying six missiles. The Soviets built one. It was called the Caspian Sea Monster. It was magnificent. We shall build a hundred.*

**Player fantasy.** A low, fast, screaming V of wing-in-ground craft sweeps a harbour and trades missiles with shore targets, always glued to the sea.

**Clerk's Annex.**
- **Mechanic.** `Ekranoplan "Lun"`: `domain:'naval'` movement (clamped to `waterBounds`) but speed 120, sea-level altitude, `ceil` .38, a 6-round missile salvo (dmg 40, range 760), HP 110, IC 260, MP 18. Counts as a *low-flying* target for enemy AA, so ordinary ceiling weapons hit it only at shallow angles; it is **immune to the sky-band gun ceilings above the stratosphere**. Cannot leave water. Inherits CIWS (L7905).
- **Real science.** *Ground effect*: within about one wingspan of a surface, the wing's induced drag falls and lift rises, so a craft cruises much faster per unit of power. The Soviet KM "Caspian Sea Monster" (about 540 tonnes, about 500 km/h) and the A-90 Orlyonok flew operationally. A seaborne craft below radar horizon is a strictly real tactic.
- **Code grounding.** `UNIT_DEFS` naval block L3201-3213 as template; `defaultCeil` L4482-4496; `fire()` L4855; naval move L8123.
- **Build size.** S-M (about 120 lines).
- **TM1.**
- **Research tree.** New node `wingeffect` "Ground-Effect Craft" (branch `air`? no, `def`, tier 2, pre `harbourworks`, cost ~150). Combo `wingeffect` + `plasma` = plasma-armed Lun ("Lun-P") using the existing `UPGRADES` swap.
- **Tenet check.** Another toy, no cap.
- **Interactions.** Air Council (a plane that is not an airbase unit; avoids stealing airbase slots).

## 4. Hot Take

### NAVAL-X: The Inland Sea (Flood the Front)

**Speech.** *Honourable members. The Admiralty proposes the single most irresponsible act in the history of this parliament. We propose that you open the dam. Not figuratively. Pick a valley, breach a levee, and in ninety seconds the front line is a sea, your fleet is sailing across a field that used to be a tank park, and every Autofleet ground unit in the valley is learning to swim.*

**Player fantasy.** The player *makes the ocean come to the war*. A coastline moves across the map. Cities nearest the flood are drowned and rebuilt as harbours. The navy is not a coastal service any more; it is wherever you decide the water goes.

**Clerk's Annex.**
- **Mechanic.** A **megaproject** (banked "Projects" framework; megaprojects are built on purpose) called *Inland Sea Works*. The player builds a chain of `Sluice Gate` buildings across a chosen 600-1200 px valley (each IC 400). Completing the chain starts a flood: for 60-90 s a **flood wave** lowers `elevAt` in the span by a fixed delta via the same `fills[]`/`craters` negative-crater mechanism (fills with negative `amt`). Enemy ground units inside lose `passable` (L1536) and *drown* using the same rule that already drowns buildings (L6561); `terrainVersion` bumps so `waterBounds` (L3760) re-merges the new sea with the nearest ocean. Player buildings inside are drowned unless they carry the existing `needWater` flag or `ug` (underground) flag. **Real cost:** the player loses whatever they built on the flood plain, and the ground units stuck there. Ground units inside are given a 10 s *evacuation warning* (toast and siren) so the player can move them.
- **Real science.** Real dam failures and deliberate flooding are among the oldest military engineering tools. The Dutch *Hollandic Water Line* flooded polders to stop the French in 1672 and again in 1939-40; Iraq flooded the Basra marshes in 1980. Floodplains can be turned into shallow seas in days. It is *real*, and slightly appalling, and exactly the sort of thing a desperate planet does.
- **Code grounding.** `fills[]` and `craterAt` L1492 (negative amount raises sea, positive lowers it; mechanism already used by the FILL tool); `passable` L1536; flooding rule L6561; `waterBounds` cache invalidation via `TERR_EDIT` L1470, L8125; `needWater` placement L3871; `pushCrater`/`capCraters` for the wave visuals; Megaprojects framework on branch `megaprojects-wip`.
- **Build size.** L (about 600 lines): sluice chain, flood animation, drown pass, evacuation warning, minimap update.
- **TM1.**
- **Research tree.** New node `hydroeng` "Hydrological Engineering" (branch `def`, tier 3, `dang:true`, `sc:8`: it is a flood weapon with civilian consequences, so it carries a *recoverable* stability penalty), pre `harbourworks` + `nanotech`. Combo that changes the world: `hydroeng` + `spaceage` unlocks *Orbital Tide Pull*, which uses the Tractor Beam's mechanism to hold a standing tidal bulge; flavour (**SCI-FI**: a gravity tug on seawater, internally consistent because the tractor beam is already canon).
- **Tenet check.** The player is given a *new toy* of vast scale; the cost is the player's own infrastructure, a real logistical cost, not a cap.
- **Interactions.** Terrain Council (they own sea level). Ground Council (their tanks learn to float: `amph` flag at L8202 means amphibious tracks survive the flood, a nice interaction). Society layer (TM2): flooding is a forecast-sharpening "edge-path" event.

## 5. Alliances Sought

- **Strategic Council**: the Trident ballistic boat is their dispersed launcher, and the Blue Economy removes the Uranium node lottery from every strike cost.
- **Enemy/Autofleet Council**: Trench Landers are their enemy, our reason to build. They get a new kind; we get a war.
- **Economy Council**: NAVAL-2 is a *productive* IC sink for the 212k unspent, not a spend button.
- **Terrain Council**: canals and the Inland Sea need their sea-level rules. We will concede coastline aesthetics if they concede the shovel.
- **Air Council**: we are not enemies. We want their jets to see the sea as a runway, and we want the sky capitals to remember who taught them to float.
