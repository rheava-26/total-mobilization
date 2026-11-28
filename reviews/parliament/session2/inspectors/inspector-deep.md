# Inspector-General of the Deep Bloc: Audit Report (`inspector-deep`)

*Audited: the Admiralty (`naval`) and the Planet Council (`planet`), plus every docket bill they sponsor or joined. Subterranean Works and Undersea Works are audited in Round Five. Every line number below was read in `title.html` this session.*

## 1. Opening speech

Honourable members. I have sounded every trench in this parliament, and I regret to report that most of them are puddles.

The Admiralty promises me a thermocline. I looked for it. The word `sonar` appears in the codebase zero times. The Planet Council promises me a moat that drowns the enemy's tanks at "60% max-HP per second". I found that line. It sits in the loop for *your* ground units, and the enemy's tanks do not read it. They are, in the technical language of the engine, wading.

I do not say this to be cruel. I say it because I love the Deep, and the Deep has been sold to this House twice, once as a canal and once as an ocean of invisible submarines, by councils that never opened the pipe. My bloc has good ideas under wet paper. I have come to dry the paper.

I will measure everything twice. Where I find a good bill, I will say so, in a very small voice, so the Admiralty does not get ideas.

## 2. The charge sheet

### Naval First Session bills

**NAVAL-1 The Silent Service (Admiralty): CHARGED. FALSE, OP, MICRO.**
- **FALSE.** The annex puts the hidden-sub skip in `acquireEnemy` (L4374). That function is the *player's* target picker: it walks `GRID_E`, the enemy grid, and skips `e.noTarget`. Enemy shooters pick targets through `near(e.x, e.y, _friendlyList, ...)` (L8682) and `friendlyWrapB` (L4370). A `u.hidden` flag added at L4374 hides nothing from the enemy.
- **OP.** A hidden hull with "immune to air attack while depth>=1" is unhittable by almost the whole enemy roster. Day one: a player parks Tridents, fires conventional strikes at 35 IC for 0 stability (`stratStab`, L6893; only `taboo` strikes cost stability), and cannot be touched. The only answer is the Trench Hunter, which spawns only from a water landing.
- **The "no hidden launcher exists" pitch is stale.** `ugAllowed` (L3862) already lets a Silo be buried. The Ballistic Submarine was removed on purpose in v3.25.0 ("buildings pretending to be vehicles").
- **MICRO.** "Resurface to reload at a Shipyard" is a per-boat chore unless it reuses the air `rtb` logic automatically.
- **Fix demanded:** put the hidden skip in the enemy list builder. A sub is revealed 3 s after firing, and also whenever any enemy air or naval unit is within 300 px of it. ASW hunters spawn from every non-recon intent, not only from water landings. Reload is automatic (RTB).

**NAVAL-2 The Blue Economy: CLEAN.** The numbers are honest. The Dredger pays back in 260 / 6 = 43 s and the OTEC in 320 / 10 = 32 s. A Power Plant pays back in 120 / 7 = 17 s (`BUILD_DEFS.powerplant`, L3383). These are a sidegrade, not a printing press. The Adsorber grants Uranium without a node, which is the designer's own "bonuses, not requirements" wish. Its real cost is vulnerability at sea. Stacking is judged under Deep Deposits below.

**NAVAL-3 The Great Canals: CHARGED. FALSE (redundant), TENET.**
- `waterBounds` (L3760) already merges every contiguous run where `elevAt < SEA_E`. The SINK brush (`doTerrain`, L3911, 35 IC) already cuts a channel. Two or three dabs join two seas. The Canal Lock solves a problem the existing toy already solves.
- The merged CANAL brush is priced at 90 IC per 105 px. That is **2.6× the live SINK price**. A price rise on an existing toy is a tenet smell.
- **Fix demanded:** CANAL is SINK as a line brush at 35 IC per dab. Drop the Lock, or make it a free decorative gate. Fleet-in-being (the `threatWeight` bias) is fine and stays.

**NAVAL-4 Trench Landers: CLEAN, one condition.** It is a scarier enemy, which is the tenet's lever. Its hook is correct, because the *player's* hidden-target skip does belong in `acquireEnemy`. The Harvester pattern gives it a spawn cap (`G.enemies.length` guards, L5224 to L5284). Condition: Hunters must be killable on the surface by any ship, so a player with no sonar still has an answer.

**NAVAL-5 Ekranoplan Wing: CHARGED. FALSE.** The annex says it is "immune to the sky-band gun ceilings" and "ceiling weapons hit it only at shallow angles". Neither mechanic exists. `ceil` is a shooter's reach in altitude fractions, and a sea-level naval hull is simply in range. What remains is a 12×-speed missile boat (speed 120 against the destroyer's 10) that cannot leave its `waterBounds` interval. Fix: delete the immunity sentence and sell it as a fast raider. Then it is fine.

**NAVAL-X The Inland Sea: CHARGED. FALSE, EXPLOIT, MICRO.**
- **FALSE.** "Enemy ground units drown by the rule at L8217." L8217 to L8227 sit in the *player* ground-unit branch of `updUnits`. The enemy ground path in `updEnemies` has no water test at all. It reads `if (e.ground) e.y = landY(e.x) - e.s` (L8851, "rides the terrain or the sea surface") with no `isWater`, `passable` or `drown` anywhere between L8603 and L8900. Enemy tanks already cross water today.
- **EXPLOIT.** The flood check (L6568) sets `hp = 0` on *every* non-buried, non-`needWater` building under `SEA_E - .04`. It does not check `captured`. A flood or SINK is a free delete of any building. That includes your own city in one tick.
- **MICRO.** A "10 s evacuation warning" asks the player to move units by hand.
- **Fix demanded:** first ship the enemy drown rule (non-amphibious `e.ground` takes 60% max-HP per second in water; bosses and Trench kinds exempt). The warning becomes automatic: player units already hold the shoreline (L8207), and buildings get a 10 s demolition-free flash before the kill.

### Planet First Session bills

**PLANET-1 The Spine: CHARGED. FALSE, CREEP.**
- **FALSE.** "Hubs are immune to orbital barrage" is true only for the barrage (L7394 gutting is surface-only). Buried hubs are not immune in general:
  - the bunker buster deals 1,400 to level-1 and 520 to level-2 buried buildings within 44 px (L7511);
  - the Earthbreaker's quake does `max(55, 50% maxHp)` to every buried building within 320 px every 4 to 7 s (L8791);
  - landed enemy ground units within 48 px drain buried buildings (L6603).
- **CREEP.** Upkeep is 1.5 IC/s per 1,000 px. A full 6,000 px trunk costs 9 IC/s against report 7's 850 IC/s, about 1%. That is a token, not a "real cost".
- **LINE passes:** a railway along a strip is the best-fitting bill in this bloc.
- **Fix demanded:** upkeep is a % of the linked output (for example 5%), so it scales with volume. State the real enemy answer: the Earthbreaker already cuts it.

**PLANET-2 Delta Works: CHARGED. FALSE (redundant), EXPLOIT.**
- Polders solve "land below sea level", a state the player never needs. The RAISE brush (`e = min(.95, cur + .45)`) and LEVEL (`max(SEA_E + .15, cur)`, L3911 to L3919) reclaim sea to dry plateau at 35 IC, with no pump.
- A pump with unpowered = "instant flood" contradicts the docket's own ruling 7 (a shortfall is IC debt, never a throttle), and L6568 turns it into a building wipe.
- The Tidal Strike enemy is `pushLevel` negative under a city. Under L6568 that deletes a 300 px swath in a single tick, with no telegraph.
- **Fix demanded:** keep Seawall as plain fortification and Pump as a *regen* aura. An unpowered pump drains the wall's HP, never floods. Tidal Strike gets a 6 s visible charge, like the Annihilator's beam.

**PLANET-3 Deep Survey: CHARGED. FALSE.** "An Extractor at dig level 2 doubles its yield." `ugAllowed` (L3862) excludes `onNode` buildings, so an Extractor cannot be buried today. The design needs a code change that the annex does not mention. Grade × bonus numbers are also unstated (the steel discount goes to 36%, and uranium strikes to .7 × .7). Fix: say plainly that the bill allows burying Extractors (paying the 1.5× to 2.1× burial surcharge, `ugMul` L3889), and print the max combined discount.

**PLANET-4 Theatres: CHARGED. LINE, CREEP.**
- ARCHIPELAGO islands hurt only the player: non-amphibious player ground holds at the shoreline (L8207) and the enemy wades. Pre-Fusion, your army is stranded.
- RIFT cliffs and HIGHLAND "high-ground bonus" do not exist. `passable` (L1536) is elevation against sea, nothing else. Mountains do not block a 1D strip.
- **Fix demanded:** ship the enemy shoreline rule first. ARCHIPELAGO gives the player amphibious tracks from minute 0. Delete cliff and high-ground text, or write the code.

**PLANET-5 Mobilized Ground: CHARGED. FALSE, MONOCULTURE, PERF.**
- "Haze hides buildings from the enemy's targeting radius by 10% each" has no hook. The enemy has no targeting radius; it takes the nearest friendly through `near()` (L8682). Stacked, 10 Zones give .9^10 = 35%, an unbounded stealth.
- A Zone is 5 or more same-`cat` buildings (+4% each, to +60%). That pays you to spam one category.
- Zone clustering over hundreds of buildings per tick is O(n²) unless bucketed.
- **Fix demanded:** haze costs stability cap only and feeds the Fear needle (the fleet reads it). Drop the stealth. A Zone bonus keys on *distinct* categories, so the bonus rewards variety. Bucket the cluster scan.

**PLANET-X The Great Canal: CHARGED. FALSE, LINE.** The premise is the same dead rule as NAVAL-X: "the code drowns it at L8217". The "Amphibious Lander" answer is moot, since enemy ground is already amphibious. On a one-dimensional strip a canal is also the one "flank" that exists, and it severs your *own* ground reinforcement from one end until Fusion. Fix: as NAVAL-X, plus 35 IC pricing.

### Docket coalition bills

**3. The Fleet Reads You (Admiralty co-sponsor): CLEAN, conditional.** `scanEnemyIntel` (L5744) has no air or sky-navy axis, so the gap is real. The `navy` axis counts `domain === 'naval'` only (L5749). Triggers (20+ fighters, 8+ parked aircraft) are volume gates. Condition: size Reavers and Weasels so a 240-Falcon wing is dented and not deleted (Amendment I, bullet 2).

**8. The Trunk (Planet sponsor, Admiralty joined): CHARGED.** All of PLANET-1's charges: FALSE immunity, token upkeep. Add that the Molodets and sally ports re-introduce launchers the designer removed in v3.25.0. Sally ports ("debark behind a named line") are MICRO unless auto-chosen. Fix: as PLANET-1, plus the Molodets must be an auto-selected silo-equivalent.

**9. Deep Deposits (Planet, Admiralty): CHARGED. EXPLOIT, CREEP.**
- `G.owned` is already a count (L3927) but `srMult` (L3810) reads it as a boolean. Ruling 4 makes the count matter. If each source multiplies ×.82, then 10 buildable seabed sources give .82^10 = 0.14 and 20 give 0.02. A Dredger needs no node, so the count is unbounded. Unit costs go to near zero.
- Yield stacks as base × grade (up to 3) × (1 + .4 × 5 tiers = 3) × 2 (dig 2). That is 18× on a node. Extractor base is +4 IC, so about +72 IC/s per node. Report 7 sat on **212k unspent IC at 850 IC/s**. More IC is not what the player lacks.
- **Fix demanded:** write the stacking law in the bill. Same-type sources add −4% each on top of the base −18%, so unit costs cannot reach zero by arithmetic. The IC the bill gives must be tied to a sink (the Standing Plan) or an enemy target (Trench Landers).

**11. One Net (Admiralty joined): CLEAN.** The Fire Control it replaces is dead code (`G.fireControl` is never set, L8454). SYNC FIRE is optional. Caution: it must be a single standing toggle, never per volley.

**13. The Navigable Moat (Planet, Admiralty, Ground): CHARGED. FALSE (headline).** This is the docket's 9-vote water bill, and its premise fails. Non-amphibious enemy landings do not drown. The bill is a new-code bill *labelled* a terrain bill. Also: CANAL at 90 IC (live SINK is 35), the Lock is redundant, the Sluice flood is a free building wipe, and the evacuation warning is MICRO. Fix: ship the enemy drown rule first, price the brush at 35 IC, make evacuation automatic, and keep the Trench Lander as the single enemy answer.

**14. Dispersal (Missiles, Admiralty, Air): CHARGED. FALSE, OP, EXPLOIT.**
- "The launchers CLAUDE.md promised" were removed on purpose (v3.25.0). CLAUDE.md is stale.
- A buried Silo (`ugAllowed`) is already a hidden launcher.
- Each silo carries its own `silocd` (L6957). Each Boomer is one more launch point, so spamming boats multiplies conventional strikes at 35 IC and 0 stability.
- The hidden-skip hook is wrong (see NAVAL-1).
- **Fix demanded:** correct the hook, add automatic RTB reload, and one shared Warhead Stockpile magazine so extra hulls add *positions*, not *rate*. The enemy answer is that Bunker Busters already score `strat` buildings at 72 (`busterScore`, L7453), so busters must learn to hunt a sub's reload dock.

**Ruling 2 (`capLevels` bake): CLEAN, with a PERF warning.** The Chair is right that an eviction silently undoes player work. But `elevAtRaw` loops the whole `levels` array for every sample (L1495). Each crater (frequent) rebuilds a 211-sample range. At 220 levels that is 46k loop steps per crater. At 2,000 levels it is 420k per crater, multiplied by the bombardment rate. Baking must store a baseline `Float64Array` and drop the levels. The same crime sits in `capCraters` (70) and the 60-ruin cap, and both must bake or compact too.

## 3. Blatantly overpowered today

1. **The sky navy against the sea navy: the reason report 8 fielded 3 ships.**
   - Sky Patrol Ship: 300 IC for 800 HP + 180 shield, dmg 62, speed 30, anywhere on the map (L3256). Destroyer: 200 IC for 180 HP, armor 8, dmg 24, speed 10, clamped to one body of water (L3201).
   - Per IC: 3.3 effective HP against 0.9, with 2.6× the damage. The Sky Destroyer (460 IC) gives 1,700 HP + shield. The Grand Battleship (1,550 IC) needs a Grand Arsenal and costs 15 upkeep.
   - Report 8 had 138 capital ships against 3. Enemy landings are chosen on dry buckets (L5701, `if (isWater(x)) continue`), so ships only fight what drifts within range of their puddle.
   - **Verdict: no nerf.** The Sky Patrol does not meet Amendment I's bar. The answer is an enemy that attacks the sea (Trench Landers, NAVAL-4) and a real reason to sail.
2. **The terrain brush as a delete button.** SINK triggers the L6568 instant kill on any building (captured ones included) for 35 IC. LEVEL turns sea into plateau. Neither is OP in the report data, so **no nerf**. The fix is the L6568 telegraph.
3. **Buried AA.** A buried SAM, Aegis or Microrocket Battery (1.5× cost) survives the barrage and fires from a surface muzzle (L8449). Only busters, quakes and ground units reach it. It is strong, not blatant. **No nerf.** Watch it.

### Live-code facts, so the new seats cannot overclaim
**Underground today:**
- `UG_DEPTH = 50` (L3843). There are two buried layers, `ugLvl` 1 and 2, at `groundY + 50 × lvl` (L3672). `canPlace` only collides buildings on the same layer (L3868).
- `digLevel` (L3863): rail hubs are always layer 1, `autoBury` buildings always sink (Mantle Forge 2, Underground Airwing 1), everything else buries only when the player's `G.dig` mode is on.
- `ugAllowed` (L3862): rail, the three `ugFire` AA batteries, and anything with no `needWater`, `onNode` or `weapon`. Turrets, docks and Extractors cannot be buried.
- A buried non-`ugFire` weapon cannot fire at all (L8413). Buried AA fires from a surface muzzle at targets above ground only (L8449).
- Burial costs IC ×1.5 (×2.1 deep) and MP ×1.3 on top (L3889). Rail and `autoBury` buildings pay none.
- Rail hubs teleport idle *ground units* between hubs within 150 px at 1,000 px/s with a 6 s cooldown (L7884, L8240). There are no underground units, enemies, caves or digging verb.
- Buried is not immune: busters, quakes, nukes (`shallowUgHit`, L7543) and enemy ground units all hurt it.

**Underwater today: nothing.**
- There is no `sonar`, no sub, no depth state and no seabed building. Grep for `needsWater` finds nothing; the real flag is `needWater`, used only by the Shipyard (L3391) and Heavy Port (L3464).
- Water buildings sit on the *surface* line `seaLevelY` (L1533). `ugAllowed` forbids burying them. The flood rule skips them (L6568).
- The seabed exists only as terrain: SEA_E 0.45, deepened ×2.6, floor −0.45 (L1500), so about 0.15 × viewport height deep. Nothing sits on it.
- Ships are clamped to one `waterBounds` interval (L4249, L8149). Nothing hunts them. The Ballistic Submarine was removed in v3.25.0.

**Terrain today:** one brush, 105 px, flat 35 IC, three modes (L3911). `capLevels` (L1505) evicts the 221st edit, `capCraters` the 71st, ruins the 61st (L1520). Elevation is the only terrain property: nothing is "steep", cliffs do not block, and only open water stops ground units.

## 4. Struck self-limits

- **Spine upkeep per hub:** Planet's Round 3 amendment dropped upkeep to a flat 1.5 IC/s per 1,000 px "so a cheap hub is not taxed twice". That was the only cost that scaled with volume. Restore a percentage of linked output. This is a cost, not a cap.
- **`capLevels` and the ruin cap:** keep them struck, but only with baking. The cap was the only thing bounding `elevAtRaw`'s per-crater loop.
- **The Sapper brush gate:** Planet's Round 3 struck it. That was correct and stays struck. I am not restoring it.

## 5. Questions for Question Time

1. **To Planet and Naval, jointly:** point to the line in `updEnemies` (L8603 onward) where a non-amphibious `e.ground` unit drowns, stops at a shoreline, or even calls `isWater`. If there is none, will you amend the Moat to ship the enemy rule first?
2. **To Naval:** your hidden-sub skip is at L4374, the player's target picker. Which enemy-side function (`near`, `friendlyWrapB`, `_friendlyList`) will skip a sub, and what makes a hidden hull killable once it has fired?
3. **To Naval:** Sky Patrol is 3.3 effective HP per IC and a Destroyer is 0.9. What makes a player build a destroyer instead of a Sky Patrol? Name the one mechanic (not the three-ship complaint).
4. **To Planet:** PLANET-5 claims a Zone hides buildings by "targeting radius". Which function reads a targeting radius? And will a 10-Zone stack (35% visibility) survive Amendment I?
5. **To Planet:** PLANET-3 buries an Extractor at dig level 2, but `ugAllowed` (L3862) excludes `onNode`. Is burying Extractors part of the bill, and who pays the 2.1× surcharge?
6. **To both:** the SINK brush is 35 IC and already merges seas. Why does the Moat price the same action at 90 IC and call it a new toy?
