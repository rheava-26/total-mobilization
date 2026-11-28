# KINETIC ARMS COUNCIL — Round 1 Position Paper
*The Ordnance Board. Slug: `kinetic-arms`.*

## 1. Opening Speech

Honourable members, the Chief of Ordnance rises, and would like it minuted that he is *already* standing.

I have listened to the Laser Council for eleven sessions. "Speed of light." "No ballistics." Splendid. A laser is a torch with ambitions. I have never seen a torch lead a target, fall under gravity, or ring off a Goliath's glacis plate. A *shell* has a story. It is born in a chamber at 3,000 atmospheres, it leaves the muzzle at four hundred pixels a second (I checked; `PS.shell`, L4892), and it arrives *when it arrives*. That is the whole dignity of ordnance: you commit.

The designer, bless him, has given my rounds big heads and short tails and now they *snap*. I am moved. But a handsome round fired from a dumb gun is a ballerina in a sack. Every cannon in this game does the same thing: a cylinder of damage travels to a target and subtracts. No fuse. No fragmentation. No choice of ammunition. A tank gun that cannot tell a bunker from a bomber is not a tank gun; it is a *nail gun with a flag*.

The Board wants guns with *opinions*: shells that burst where the fuse is told, belts that spin up, batteries that fire for effect on one clerk's order. We ask no cap on a single gun. We ask only that you let ordnance be *ordnance*.

Seconded by the barrel. Carried by the breech.

## 2. The Gap

Read-only findings from `title.html`:

- **Every conventional gun is "direct hit or nothing."** `shell` (L4865) flies a ballistic arc and, on contact, runs `hurt()` once plus a *visual* `boomW` (L9159-9160). There is no `splashDamage` call in the shell path. `splashDamage` (L7546) has only five callers, none of them cannon rounds. A 70-dmg Goliath shell kills exactly one thing, even into a swarm of drones.
- **Rotary and tracer rounds are single-target pellets.** `rotary` (L4883) spawns four `tracer` rounds at `dmg*.5`, each with no fuse, no fragmentation, and no burst. The Hailstorm, CIWS and every secondary AA turret share this one behaviour. Flak is *named* flak but behaves like a shotgun with perfect pellets.
- **Armour is a flat subtraction.** `hurt()` (L4980-4985): `eff = max(1, dmg·HIT_BUFF·mult − armor·ARMOR_NERF)` with `ARMOR_NERF = .55`. There is no penetration, no overmatch, no ricochet, and no ammunition that interacts with armour. A tungsten rod and a 5.56 differ only by a number.
- **There are no ammunition types.** `WPN_LABEL` (L9894) lists weapons, not rounds. The Ammunition Depot (L3452) is an anonymous +25% fire-rate aura. It stocks no ammunition.
- **Fire Control is dead code.** `G.fireControl` "is never set anymore" (L8447). ROADMAP lists it as ON TRIAL. The designer called it a nothingburger. Artillery therefore has no player verb at all.
- **Gun feel is flat.** `rate` is a constant. Nothing spins up, heats, recoils or ramps. The Hailstorm fires its last second exactly like its first.
- **Altitude.** Conventional guns cap at `.67` (L4493, "through the stratosphere"); only rail (`ceil: 0`) and missiles go higher. A gun that reaches mesosphere-and-up does not exist.
- **Evidence from the war.** Reports 6-10: air and sky ships dominate; ground guns are scenery. Shell-based units (MBT, SPG, Goliath, Mammoth) barely feature in the Article VI doctrine tallies.

## 3. Bills

### KINETIC-1 — "Fuse and Fragment": Programmable Airburst Flak

**Pitch.** *Gentlemen, a proximity fuse won the Second World War over London. We are asking for one. The Hailstorm should not hit a drone; it should burst a cloud of steel where the drone is going to be, and kill everything in the cloud.*

**Player fantasy.** Flak is a visible *wall of black puffs*. A formation flies into it and comes out in pieces. Mass drone swarms and bomber waves finally meet a counter that punishes density.

**Clerk's Annex**
- **Real basis.** Radio proximity fuzes (VT fuze, 1943) and programmable airburst (Oerlikon AHEAD 35 mm, 152 tungsten sub-projectiles; Rheinmetall 3P). The fuze counts muzzle revolutions or is programmed by an inductive coil at the muzzle, bursting at the computed point in front of the target. Fragments fill a cone, so aim error stops mattering.
- **Mechanic.** New fire mode `airburst` on `aa`, `ciws`, naval secondary `rotary`, and a new heavy flak gun. A round is fired with `burstAt = distance-to-target` and fuses at that range. At burst, `splashDamage(x, y, r=46, dmg·.8, friendly)` plus 6-10 fragment-line particles (reuse `spark`). Single hit unchanged, so rotary stream stays as a close-range mode. Ceiling stays at the gun's own `ceil`. No range, damage or rate reductions to existing units.
- **Numbers.** Radius 46 px (aircraft `s` ≈ 4-6, so one burst catches 5-15 units in a dense wave). Damage 60% of a hit at the edge, 100% at centre. IC cost of the upgrade only: +30 IC to the Hailstorm. Heavy Flak Battery: 260 IC / 70 MP, hp 180, range 700, `ceil .60`, rate 1.1 s, burst dmg 38.
- **Research.** New node `smartfuze` "Programmable Fuzes", branch `def`, pre `[pointdefense, automation]` (combo: sensors + processors), tier 2, base cost 150 (×1.6 = 240), not `dang`. Unlocks the Heavy Flak Battery and swaps `aa`/`ciws` to airburst via `UPGRADES` (L3321) in the same style as the plasma re-arm. Combo partner: `directedenergy` gives "ionised-burst" +10% radius.
- **Code grounding.** `fire()` L4855 (add `burstAt` arg or `FIRE_CTX` flag), rotary branch L4883, `updProj` L8899 (detect `p.burstAt` reached → `splashDamage`), `splashDamage` L7546, `UNIT_DEFS.aa` L3188, `BUILD_DEFS.ciws` L3404, `UPGRADES` L3321, `RESEARCH` L3502, `PD_UNINTERCEPTABLE` L8519 (untouched).
- **Build size.** M (~220 lines).
- **TM1.**
- **Tenet check.** Pure buff to the player's AA, with no cap. The scary answer is the enemy: this makes swarms a viable enemy design, so the Autofleet can send *bigger* drone clouds (see Alliances).
- **Interactions.** Lasers council: beams stay hit-to-kill, flak becomes the swarm counter (ROADMAP #10 in the laser notes already wants "flak handles swarms"). Enemy council: unlocks ten-fold swarm sizes. Air council: friendly fighters need `friendly:true` filter so flak never damages friendlies.

### KINETIC-2 — "Sabot and Shell": Selectable Ammunition and Real Penetration

**Pitch.** *Armour in this game is a number you subtract. Real armour is a ratio of rod length to plate thickness. Give me a long-rod penetrator and a Goliath's side becomes a suggestion.*

**Player fantasy.** You open the Ammunition Depot, pick APFSDS for the tank line, watch Land Leviathans take real hits, then switch to HE-frag when the infantry waves come.

**Clerk's Annex**
- **Real basis.** APFSDS: a fin-stabilised tungsten or depleted-uranium rod, ~2 cm across and 60 cm long, leaves the barrel at ~1,700 m/s. Penetration scales with rod length × √(rod density/target density), so the armour does not stop it. HE-frag does the reverse: it trades penetration for blast. HEAT/shaped charge uses a collapsing copper liner jet.
- **Mechanic.** Three ammunition types selected per *Ammunition Depot* aura (radius 260, as existing). Nearby `shell`/`tracer` guns use the selected round:
  - **AP-sabot:** armour subtracted ×0.35, damage ×1.0, no splash. Best vs armour ≥ 14.
  - **HE-frag (default, current behaviour plus small splash r 30, 50%):** best vs light and swarm.
  - **Canister:** short range (≤ 140), damage ×1.8 in a 40° cone, ideal anti-infantry and anti-drone for MBTs under assault.
  - Real cost, not a cap: each AP shot burns **1 Alloy-equivalent per 20 shots** (draws the existing `alloy` comp meter, L3188 `comp` mechanism), and Canister costs nothing. Switching is free and instant.
- **Numbers.** Against Land Leviathan (armour 22 → after ARMOR_NERF 12.1 per hit): AP armour term 4.2 vs 12.1, a +20% effective dmg gain per 70-dmg shell, and +60% against armour 36 (War Colossus). Against unarmoured it is a mild loss versus HE-frag. The player chooses, so there is nothing to nerf.
- **Research.** New node `apds` "Sabot Penetrators", branch `auto`, pre `[robotics]` (heavy metallurgy), tier 2, base cost 130 (×1.6 = 208), not dangerous. Uses `srReq: 'steel'` for the first AP shell type so the Steel node actually matters (Article VI: resources barely matter, and this gives Steel a *bonus*, which the old backlog said was the right fix, not a requirement). Combo with `plasma`: plasma re-arm swaps AP for "slugs" and keeps canister.
- **Code grounding.** `hurt()` armour computation L4981-4985 (add `p.pen` param), `fire()` L4855 shell branch L4865, `BUILD_DEFS.ammodepot` L3452, `UNIT_DEFS.tank/heavytank/mammoth` L3186-3195, `WPN_LABEL` L9894, `RESEARCH` L3502, `STRAT` L6878 (untouched).
- **Build size.** M-L (~350 lines incl. a three-button depot panel).
- **TM1.**
- **Tenet check.** No cap or nerf. Armour piercing is purely additive, with a real (alloy) cost. It makes heavier enemies *scarier* with a real answer, which is the tenet's own formula.
- **Interactions.** Resource council: gives Steel and Alloy a job. Enemy council: lets the Autofleet field truly heavy armour (Leviathan armour 22 → 35) without making it unkillable. Heat council: the President's heat seed stacks, since a heated target takes extra kinetic damage and sabot penetrates it further.

### KINETIC-3 — "Spin-Up": Gun Feel, Ramp and Barrel Heat

**Pitch.** *A Gatling does not start at full cry. It winds. Whump-whump-whump... BRRRRRT. Give the ear and the eye that wind.*

**Player fantasy.** A line of Hailstorms *spools* when a wave appears, firing visibly faster the longer they hold the contact. Sound and recoil tell you the cannon is working.

**Clerk's Annex**
- **Real basis.** M61 Vulcan reaches 6,000 rpm in ~0.4 s; the GAU-8 takes ~0.5 s to full rate. Sustained fire heats barrels, and real naval and tank guns use water jackets and barrel-cooling intervals. Heat means *rate of fire is the output*, and spool-up is the input.
- **Mechanic.** Each rotary/autocannon tracks a `spool` value 0-1 (the field already exists at L4166 `spool`, used by something else; reuse the pattern). While engaging, `spool` climbs over 1.2 s; fire interval = `rate / (1 + .35·spool)`, so a fully spooled gun fires **+35% faster** than today. Dropping the target decays spool over 2 s. Fire Control / Radar auras pre-spin it, so a radar-covered battery opens at 60% spool. Heat is a **bonus layer**: after 6 s unbroken fire, barrels glow and the muzzle flash turns white-hot (visual), granting nothing worse than a faint cosmetic flare; no penalty.
- **Numbers.** 4-barrel stream stays at 4 rounds/shot; ramp is on interval only. Audio pitch rises with spool. Muzzle recoil kick: 2 px barrel offset decaying in 0.08 s.
- **Research.** None needed (conventional arms are already unlocked). The Rotary Cannon Works (L3457) building gives +0.15 starting spool to every city gun. Optional later node `servo` in `auto` for "Servo Loaders".
- **Code grounding.** `updUnits` fire timing (grep `u.cd`), `updBuildings` fire timing, `fire()` L4855 for recoil/muzzle (`G.parts.push` L4863), `SFX.shoot`, `BUILD_DEFS.rotaryworks` L3457.
- **Build size.** S (~90 lines).
- **TM1.**
- **Tenet check.** Strict buff and a visible feel pass. Nothing is capped.
- **Interactions.** Audio/UI council: pitch, recoil and white-hot barrels are theirs to polish. Air council: Warthog/Reaper rotary secondaries also spool. Performance: spool is a single float per gun, so cached-art mass is unaffected.

### KINETIC-4 — "Fire for Effect": Fire Direction Centre and Time-on-Target Barrage

**Pitch.** *Fire Control is dead. Bury it with honours, and let me give you what a real artillery officer has: one order. "Battery, fire for effect." Every gun in range fires, and every shell lands on the same second.*

**Player fantasy.** You click a spot. Forty SPGs unload on different arcs. Four seconds later the whole barrage lands in one *crump* on the Land Leviathan's head.

**Clerk's Annex**
- **Real basis.** Time-on-Target (TOT) and Multiple Rounds Simultaneous Impact (MRSI): a modern howitzer (PzH 2000, K9) fires up to six rounds on different trajectories (steep, mid, flat) so all land together; a battery of 8 guns can drop 48 shells in 5 s. Gunners use a Fire Direction Centre to compute charge, elevation and delay per gun.
- **Mechanic.** New building **Fire Direction Centre** (INFRA, 180 IC, 60 MP, hp 200). Units and guns within 900 px link automatically. Click anywhere to place a **barrage marker** (reusing the strike-aim UI at L6878 `STRAT`; this is NOT a strategic strike and costs no stability). Each linked gun with range fires on that point with a computed delay so impacts coincide, using each gun's arc. Cooldown 8 s per barrage. A linked gun obeys the marker for 6 s, then returns to autonomous fire. Shells gain a **blast** (r 38, 60% dmg), because the shell path today has no splash at all (the Gap).
- **Numbers.** Barrage simultaneous impact: N guns × dmg, all in one frame, so the same IC buys a decisive hit. Counter-battery: the FDC reveals an enemy artillery position (pelter, earthbreaker) as a marker for ~10 s after it fires, via a back-trace of the shell arc.
- **Research.** Node `fdc` "Fire Direction", branch `def`, pre `[pointdefense]`, tier 1, base cost 80 (×1.6 = 128), not dangerous. Replaces the dead `G.fireControl` for artillery only; heavy emplacement Fire Control remains hidden.
- **Code grounding.** L8447 dead `fireControl` branch (remove or reuse the `G.designated` plumbing), `UNIT_DEFS.arty/mlrs/hydra` L3189-3194, `BUILD_DEFS` L3381, strike-aim UI near `armSilo`/`STRAT` L6878, shell arc `fire()` L4865 (`v=400, vy -= .30v, gravity 240` at L9137 gives the TOT delay maths), `splashDamage` L7546.
- **Build size.** L (~500 lines; the delay solver is the work).
- **TM1.**
- **Tenet check.** No cap; the barrage is limited only by how many guns you built. Mass arty finally pays off.
- **Interactions.** Strike council: this is the "cheap strike" tier below the nuke, with no stability bleed (Article VI: strikes drain stability to zero). Enemy council: the Autofleet's pelter and earthbreaker get a counter-battery duel.

## 4. HOT TAKE

### KINETIC-X — "SHARP / Babylon": The Bull Gun (Light-Gas Supergun Megaproject)

**Pitch.** *Gerald Bull built a gun in Barbados in 1962 that shot a 180-kilometre arc. They shot him for it. We shall build it here, and aim it at the sky. A 200-metre barrel, in sections, bolted on over the course of the war, and it throws a ton of iron at the Orbital Annihilator while it is still charging.*

**Player fantasy.** A mid-map megaproject: ten barrel sections, each a separate build site. The finished gun elevates, tracks the sky, and fires once every 20 s. The flash lights the whole map.

**Clerk's Annex**
- **Real basis.** HARP (Martlet 2, 180 km apogee, 1966). Light-gas guns (SHARP at LLNL, 3 km/s; two-stage ~7 km/s) accelerate a piston-compressed hydrogen charge, because light gas gives a higher acoustic speed than powder. Project Babylon planned a 156 m barrel and a "traveling charge" that burns *with* the projectile. Long barrels are the only way to raise muzzle velocity without raising peak pressure.
- **Mechanic.** Megaproject (Article IV.3), built as ten "sections" via `megaprojects-wip`: each adds +12% of the final range and +8 damage, and the gun fires at any partial section count (so it is useful from section 3). Final form: range 2400, ceil 0 (any altitude, 'rail'-class reach), one shot per 20 s, dmg 520 + 12-px splash. The shell is also a **sub-calibre sabot**, so it pairs with KINETIC-2 for armour. Upkeep 6 IC/s (real cost), no cap on how many you build.
- **Research.** New node `supergun` "Orbital Barrel", branch `space`, pre `[apds, spaceage]` (combo: penetrators + orbital lift), tier 3, base cost 300 (×1.6 = 480), `dang: false`. The world-changing effect: while a section stands, the **Orbital Annihilator's 6 s beam charge is interruptible** by a hit.
- **Code grounding.** `BUILD_DEFS` L3381 (new `supergun` with `sections`), `fire()` L4865 (new `bull` round), `spawnSuper` L5269 (annihilator charge interrupt), `weaponCeil` L4488, megaprojects-wip framework (branch), `INTENT` L5119.
- **Build size.** XL (framework dependent; the gun alone is L).
- **TM1** (needs megaprojects-wip merged).
- **Tenet check.** Pure power fantasy with a real upkeep cost. No cap.
- **Interactions.** Megaproject framework, space council (counter to satellites), enemy council (gives the Annihilator a reason to fear the Bull Gun, so it may target the barrel: a scarier enemy).

## 5. Alliances Sought

- **Laser/Energy council:** *they own beams; I own the swarm counter.* KINETIC-1 flak plus their hit-to-kill beams form a complete layer cake. Their heat seed makes my sabot hit harder.
- **Enemy / Fleet council:** KINETIC-1 and KINETIC-2 only work if the Autofleet *sends swarms and heavy armour*. I will vote for any bill that makes the enemy denser and tougher.
- **Strike/Stability council:** KINETIC-4 gives them a stability-free barrage tier they can balance a cheaper strike against.
- **Space council:** KINETIC-X and space-gun launches share a megaproject framework.
- **Resource/Logistics council:** AP shell alloy cost is a real, tenet-legal brake that gives Steel and Alloy a job.
- **Opposition (friendly):** the Air council will say flak is a nerf to fighters. It is not; it is the enemy's swarm that bleeds.
