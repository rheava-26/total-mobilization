# Inspector-General of the Deep Bloc: Second Audit (`inspector-deep`, Round Five)

## 1. Opening

Members. I have returned from the second sounding. Good news: the Planet Council opened the pipe and admitted it was dry. Bad news: two new seats have arrived with buckets. The sea floor, I am told, is a place where nothing can hurt you, and the underground is a place where nothing can reach you. Both seats forgot to check what `L6568` does to anything standing in water. I checked. It was not kind.

## 2. The second charge sheet

**NAVAL-6 Floating Works (rafts): CLEAN, one condition.** The flood rule (L6568) already spares `needWater` buildings. Rafts must set the same flag or a raft factory dies on placement. Rafts do not duplicate LEVEL, since they leave the harbour open. The enemy answer (exposed surface) is real.

**NAVAL-7 The Gunline: CHARGED. FALSE, duplicate.** It cites a "FRONT marker, v4.1.0". I grepped `title.html` and no such marker exists. Idle ships already patrol toward the nearest threat inside their `waterBounds` (L8142, `NAVAL_PATROL_FRAC`). The bill adds one default flag to a behaviour that already exists. *Fix:* cite the real hook, and cut the bill to "ranged-hold anchors to the water tile nearest the landing".

**NAVAL-8 The Cataphract: CHARGED. CREEP.** It reads a `skyNavy` axis that does not exist yet (`scanEnemyIntel`, L5744, has no such axis). Four salvos of 3 × 90 damage against a Sky Patrol Ship's 800 HP + 180 shield kills one capital per salvo. In packs of 2 to 4 that is Amendment I's second bullet: an enemy buff that can make the army worthless. *Fix:* give the cadence, a salvo cooldown, and a ceiling of one pack per 60 s. Gate it on the Fleet Reads You bill landing first.

**NAVAL-1 amended (auto-dive): CHARGED. OP.** "Enemy air cannot lock a submerged hull" is the old immunity in new words, and no hook is named. Enemy targeting runs through `near(..., _friendlyList)` (L8682) and `friendlyWrapB` (L4370). *Fix:* name that hook, and reveal on fire within 300 px of any enemy air. Sub reload "at a Pen" depends on UNDERSEA-5, so the dependency must be stated.

**NAVAL-4 amended (one sea family): CLEAN.** The Hunter and Bathyscaphe merge is the right call. The spawn triggers (5+ seabed buildings, 3+ ships, a Pen) scale with volume. Last round's condition stands: Hunters must be killable on the surface by any ship.

**NAVAL-X / Moat standing order: CHARGED. TENET, EXPLOIT.** "FLOOD ON LANDING" is the default. Every landing then drowns your own flood-plain buildings through the instant `hp = 0` kill (L6568). The bill makes self-destruction automatic. *Fix:* default is HOLD, and the flood warns on the HUD for 10 s.

**PLANET-2 amended (pump grace): CLEAN, one condition.** The 30 s grace and ghost rebuild satisfy my charge. The Tidal Strike still needs a visible 6 s charge before it sinks a swath.

**PLANET-6 Overburden: CHARGED. EXPLOIT, MICRO.** `finishSite` fixes a buried building's `y` at build time (L3925, the paper says L3918), and one RAISE click adds +0.45 elevation. That is about 120 px of overburden for 35 IC, with no research, no surcharge, and no `deepmining`. It beats the Abyssal tier. The Deep Penetrator is keyed to `ugLvl`, not to effective depth. It also needs many clicks, which is MICRO. *Fix:* key the enemy Penetrator to effective depth. Add a standing "fortify this span" order. Scale RAISE cost with the volume lifted (cost, not cap).

**PLANET-7 Shelf and Trench: CHARGED. TENET.** The paper's own §4 admits it does not call LEVEL-on-water blatant, then "stops LEVEL from reclaiming by default". That removes a toy the player owns (L3917, `e = max(SEA_E + .15, cur)`) without Amendment I's proof. Its pixel `minDepth` is also screen-dependent: `maxElev = H × .17` (L1527), so max seabed depth is about 0.153H. On a 480 px viewport that is 73 px and an 80 px OTEC cannot be placed. *Fix:* leave LEVEL alone, and make `minDepth` a fraction of `maxElev()`.

**PLANET-8 Defilade and Crest: CHARGED. FALSE (hedged), EXPLOIT.** The paper says "to verify: projectile-terrain collision". It exists: rounds die at `p.y >= groundY(p.x)` (L9167). Defilade is already live. The new part is +15% range and +10% cover on crests. A 35 IC RAISE ridge becomes the cheapest defensive upgrade in the game, stacked with Overburden. *Fix:* delete the "verify" hedge and state the existing behaviour. Price the crest bonus with ridge height, and name the enemy answer in numbers.

**SUB-1 The Deep Works: CHARGED. EXPLOIT.** The Deep Foundry gives "+20% to every other buried PROD building within 400 px". With 40 foundries in range that is +800%, with no stacking law. The Abyssal tier would also be unanswered for 12 minutes. The real enemy answer already exists and ignores depth: the Earthbreaker quake does `max(55, 50% maxHp)` to every buried building within 320 px (L8791). *Fix:* state a stacking law, for example +20% from the best foundry only. Key the Penetrator to the existing quake and bunker buster, not minute 12.

**SUB-2 The Mantle Tap: CHARGED. CREEP.** The Magma Tap's +30 IC has no cost. The Fissure chain (250 damage within 120 px) will cascade through a packed Tap field. IC is already the one thing in surplus (212k idle in report 7). *Fix:* state the cost, and tie the yield to a sink or an enemy target.

**SUB-3 The Redoubt: CHARGED. FALSE, MICRO.** The Deep Habitat has no IC cost. "Below a stratum-share of 50% of housing, the cap falls 5" is a ratio the player must manage. The shelter floor (40% of `cityStabPenalty`, L4547) is reached by 5 Habitats, so the sixth does nothing, which breaks "ten is ten times one". The Chthonic combo's "+MP" touches the manpower formula, which Article IV.1 forbids. *Fix:* give the cost, make the Lamp rule automatic or remove it, and drop "+MP".

**SUB-4 The Sortie Shafts: CHARGED. FALSE.** "A buried silo or hangar cannot act on its own" is wrong. `launchStrike` (L6951) never reads `ug`, so a buried silo fires today. The Underground Airwing is already `autoBury: 1` with a garrison (L3461). The bill adds only a 15% IC discount on a 35 IC strike. *Fix:* withdraw as written; keep only the sortie-rate boost with a number.

**SUB-X The Under-Line: CHARGED. FALSE, LINE.** The code has no underground unit domain. `onRail` (L7884) is a teleport state, not a lane. The bill needs a new domain, and the paper calls it XL. Losing a segment for 20 s punishes the Trunk's own buff. *Fix:* ship as an event layer (L) with the domain stated, or defer to TM2.

**UNDERSEA-1 The Sea Floor: CHARGED. FALSE, EXPLOIT.** The code list omits L6568. A seabed building is not `needWater`, so the flood rule sets `hp = 0` the moment it is placed. "Enemy ground fire and bombers cannot target seabed buildings at all" makes the first four seabed buildings untouchable, since the Bathyscaphe spawns only at 5+. A 35 IC SINK adds depth for a stronger shelter factor. *Fix:* give seabed buildings the flood exemption. Bombers get a depth-bomb bomblet, and the Bathyscaphe gate drops to 1.

**UNDERSEA-2 The Heat Engine: CLEAN.** OTEC pays back in 200 / 9 = 22 s, near the Power Plant's 17 s. The cost is the Fear Index, which depends on docket Bill 2 shipping.

**UNDERSEA-3 The Deep Harvest: CHARGED. EXPLOIT.** "Output scales by dredger count" has no law, and `G.owned` is already a count. At ×.82 per source, 20 sources give 0.02. This is the old Deep Deposits charge, repeated. *Fix:* a stacking law (−4% per extra same-type source on a −18% base).

**UNDERSEA-4 The Listening Floor: CLEAN.** Linear stacking is fine: the cap is clamped to 100 (CLAUDE.md), regen is +0.5/s at most, and the Cable Station's +0.6 is comparable to the Comms Tower. It depends on the Forecast bill.

**UNDERSEA-5 Pens and Domes: CHARGED. TENET, EXPLOIT.** The abyssal society's "−8% manpower income" is a nerf to the manpower formula, which Article IV.1 declares perfect. The Dome's upkeep is 3 IC/s against 850 IC/s, a token. A Pen is one more invulnerable `strat` launcher (see §4). *Fix:* drop the −8%, scale Dome upkeep to a percentage of income.

**DROWNED CAPITAL (hot take): CHARGED. CREEP.** `.capital` is read in 53 places. Moving the flag to the seabed lets the Decapitation intent target a place where beams lose 75%. The decoy city is never targeted, so the intent is simply neutered. The only answer is the Bathyscaphe, which does not spawn until 5+ seabed buildings. *Fix:* the decapitation scan picks the *surface* capital if it is richer, and gets a seabed-hunter flight.

Tally: 5 CLEAN (NAVAL-6, NAVAL-4, PLANET-2 amended, UNDERSEA-2, UNDERSEA-4), 15 CHARGED.

## 3. Old charges

1. NAVAL-1 FALSE hook: NOT SATISFIED (no hook named; the old immunity returns as "cannot lock").
2. NAVAL-1 MICRO: SATISFIED (auto-dive, auto-reload).
3. NAVAL-3 redundant: SATISFIED (withdrawn). Its auto-lock lives on in the Moat, and the 90-vs-35 IC price is still unaddressed there: NOT SATISFIED.
4. NAVAL-5 FALSE: SATISFIED (withdrawn).
5. NAVAL-X FALSE drown: SATISFIED by Planet's headless-test condition. The L6568 self-wipe stays: NOT SATISFIED.
6. PLANET-1 immunity and upkeep: NOT SATISFIED (withdrawn into the Trunk, which was not changed).
7. PLANET-2 redundant and instant flood: SATISFIED (30 s grace). Tidal Strike charge: NOT SATISFIED.
8. PLANET-3 extractor burial: NOT SATISFIED (the bill still keeps the claim).
9. PLANET-4 LINE: NOT SATISFIED (no shoreline rule or cliff fix).
10. PLANET-5 targeting radius: NOT SATISFIED (it moved into the Fear panel as "−N% enemy targeting").
11. PLANET-X: SATISFIED (the headless test is a real gate).
12. Deep Deposits stacking: NOT SATISFIED.
13. Dispersal: NOT SATISFIED (reload is auto, but the hook and the invulnerability are unaddressed).
14. `capLevels` bake: SATISFIED for levels and ruins. `capCraters` (70) is not mentioned.

Satisfied: 6 of 14.

## 4. Cross-seat check

**Duplicates.** Hidden launchers now come from five bills: NAVAL-1 Trident, Dispersal's Boomer, SUB-4's buried silo, UNDERSEA-5's Pen, and the Silo that can already be buried. Pick one hidden-launcher rule, once. Rafts (NAVAL-6) and the LEVEL brush both claim the same "build on water" role.

**Gaps.** The enemy shoreline rule remains unowned. It is the single biggest code fact in the bloc, since every moat, archipelago and flood bill depends on it. No seat owns one shared `b.layer` field (surface, raft, shallow, deep, abyssal, seabed). Each new layer needs the L6568 exemption and each bill patches it separately.

**Stacking exploits.**
- **The 35 IC brush as armor.** Overburden (RAISE over buried buildings), Defilade (ridges), PLANET-7 (dredged shelter) and unlimited baked edits turn one brush into the best defensive button, with no cost scaling.
- **Capital immunity three ways.** The Drowned Capital, the Redoubt (8 sections count as buried for decapitation) and Overburden each solve Decapitation. Combined, the Officer-difficulty intent has no target.
- **Invulnerable sheltered income.** Seabed OTEC and Turbines plus Cable Stations: fine alone, but with the 4-building Bathyscaphe gap they are untouched.

## 5. Questions for Question Time

1. **To Undersea Works:** `L6568` sets `hp = 0` on any non-`needWater` building under `SEA_E - .04`. How does a seabed building survive its own placement?
2. **To Planet:** PLANET-7 removes LEVEL's sea reclamation while PLANET-8 and 6 add ridges. What stops the RAISE brush from being the best defensive purchase for 35 IC?
3. **To Subterranean Works:** `launchStrike` (L6951) never reads `ug`, and the Airwing is `autoBury: 1`. What does SUB-4 add that the game lacks?
4. **To Naval:** NAVAL-7 cites a FRONT marker. Which line defines it?
5. **To Subterranean and Undersea:** the Redoubt, the Drowned Capital and Overburden each cancel Decapitation. Which one survives?
6. **To Planet:** who owns the enemy shoreline rule, and when does it ship relative to the Moat?
