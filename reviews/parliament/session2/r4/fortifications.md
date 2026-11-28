# The Fortifications Corps: Maiden Paper (Ground Bloc caucus)

## 1. Opening speech

Honourable members, I am the Chief Engineer of Fortifications. I have rebuilt the same wall nine times, and I loved every one of them, including the three that fell on my foot.

I have read the First Session. Twenty-three councils filed 120 bills. Not one of them asked what the player's *stations* are for. The Air Council wants jets, the Armour Corps wants tanks, and the Rifle Corps wants to be chaff. Meanwhile the player's best friends are the buildings that stand still and keep firing. Nobody gave them a friend at court. I am that friend.

The President asked for "more buildings, more defensive stations, more balancing stuff". Engineers hear that as a commission, not a request.

I bring four bills, one radical one, and one funeral. The funeral is for Fire Control, which the code buried years ago and nobody told the corpse.

A fortification does exactly one thing. It converts the enemy's time into your resources. Every second a lander spends chewing a wall is a second your guns spend chewing him. Armour Corps, you may keep your treads. I keep the ground they stand on.

Ten nine-times-rebuilt walls. One doctrine. Dig.

---

## 2. The Gap

**What exists** (`BUILD_DEFS`, `title.html` L3381+): Bunker (L3403, 110 IC, 18 dmg), CIWS (L3404), Aegis Battery (L3408), Microrocket Battery (L3409), Shield Generator (L3412), Particle Cannon (L3413), Laser Tower (L3439), Plasma Turret (L3441), Radar (L3406), Fortress HQ (L3474).

**Gap 1: there is no wall, no mine, no obstacle.** `grep` for `'wall'`, `minefield`, `landmine` in the code returns nothing but changelog prose. The only "fortification" the engine knows is *entrenchment* (`fortBonusAt` L4542, damage reduction up to 60% on dug-in ground *units*), and auto-emplacements (`deployEmplacement` L4564, `mgnest`/`flaknest`/`artnest`, capped at `G.buildings.length < 170`, L8217). Enemy ground units (`gtank`, `gmech`, `pelter`, `earthbreaker`, L5604-5610) land and target the nearest friendly. Nothing in the world *stops* them. On the Line (Amendment III), blocking is the only thing a ground structure can uniquely do.

**Gap 2: stations do not scale with volume.** `shieldBlocks` (L8901-8903) returns the *first* live dome that covers a point. Ten Shield Generators overlapping one city give 900 HP of shield, not 9,000. That breaks the One-Button Law's "ten of them should feel like ten times one".

**Gap 3: stations do not repair, and the Line eats them.** There is no rebuild-in-place and no engineer. `nanotech` self-repair is research-gated and exists for the whole base, not for stations. A fortification that dies is gone, so the player re-places it by hand. That is per-building babysitting.

**Gap 4: Fire Control is dead code.** Confirmed. `title.html` L8454: *"G.fireControl is never set anymore so `!G.fireControl` short-circuits every call"*. `pickDesignated` (L8456) returns `null` for every caller. The `manual: true` flag on Bunker, Laser Tower, Microrocket and Particle Cannon (L3403, L3439, L3409, L3413) belongs to the separate v3.7 "Take the Trigger" hand-aim. That system is optional, so it survives the One-Button Law. Fire Control, which needed a per-shot designation, would not have.

**Order Paper #10, honest answers.**
- *Blatantly overpowered:* nothing in DEF. No Report shows a defence station making loss impossible. The Reports 6-10 wins were air plus sky navy (Article VI). The auto-emplacement `EMPLACE_DMG_MUL = 0.5` (L4556) is a prior nerf on a free mass-built wall. I do **not** call it "blatant" under Amendment I. I leave it alone.
- *Useless today:* the **Laser Tower**. 18 dmg at 1.9 s (about 9.5 dps) for 150 IC, against a Bunker's 18 dmg at 0.66 s (about 27 dps) for 110 IC. Its sole advantage is a higher ceiling (.67). It is weak; see FORT-4 for the buff, which needs no nerf anywhere.
- *Hollow at volume:* the Shield Generator, as above.

---

## 3. Bills

### FORT-1: THE CURTAIN WALL

**Pitch (in character).** "A wall on a line is a total barrier. A total barrier is not a cheat, honourable members. It is a *question*. The enemy cannot walk through it, so he must break it, outwait it, or go over it. Make him choose. Each wall segment is 25 IC of stone that costs him a minute of his war."

**Player fantasy.** Carpeting the continent in concrete and watching the autofleet's landers pile up against it like surf.

**Clerk's Annex**
- **Mechanic.** New DEF building `wall`: 40 px wide, 600 HP, armor 14, 25 IC, 0 MP, build 3 s, no weapon. A ground enemy (`e.ground`) whose path to its target crosses a live wall stops at it and attacks it until it breaks. Player ground units pass through (friendly gate). Segments of one **Gatehouse** (a second building, 140 IC, 30 MP) auto-extend ±1 segment every 20 s up to 6 segments each side, closing a flank with no further clicks. Walls are **not capped**. Three layered walls buy three delays.
- **What the enemy does about it (Amendment I).** `pelter` (indirect fire, L5609) and the bombarder's orbital strikes ignore blocking. `earthbreaker` (quake, L8788) deals double damage to walls. Skyfall landers drop on *either side* of a wall, so it protects a zone, not a map. New landed **Breacher** squads (the enemy already has a `breacher` flag, L8712) get a charge ability against walls. Scarier enemy, no cap on the player.
- **Is it OP, or a puzzle?** A puzzle. The wall delays and funnels, but it does no damage. It only pays off with guns behind it. That is the combined-arms reason to mix.
- **Code grounding.** `BUILD_DEFS` L3381; ground-enemy movement in `updEnemies` near L8682 (target selection); `mkBuilding` L3670; `OVERLAP_FRAC` placement check L3872 (walls would be exempt, like emplacements).
- **Size:** M. **TM1.**
- **Research.** No new node (the `Production Lines` principle, no research). The Gatehouse's `autoBury` variant would attach to Subterranean's seat.
- **Tenet (Amendment I).** Pure buff to the player, with a stated enemy counter above. No power creep: the wall has no damage.
- **One-Button.** Gatehouse auto-extends. Scales: N segments, N times the HP pool.
- **Line check.** This bill *is* the Line answer for fortifications. A wall is the only structure that makes a 1D front a decision.
- **Interactions.** Armour/Artillery (walls pin landers for their guns); Autofleet (wants Breacher charges and `pelter` arcs); Subterranean (buried Gatehouse).

### FORT-2: THE SAPPER WORKS (minefields)

**Pitch.** "A mine is a wall the enemy does not see until his foot is in it. Real armies lay hundreds of thousands. We lay them with a depot and a prayer."

**Player fantasy.** Seeding the beaches, then watching a landing wave detonate in a long, ordered chain.

**Clerk's Annex**
- **Mechanic.** `sapperworks` (INFRA, 160 IC, 40 MP). Every 4 s it seeds mines within 300 px of itself along the ground: one mine per 24 px cell, up to 12 per Works, 3 IC of upkeep per minute. A mine is a *density value per cell*, not an entity (performance, 12k+ unit scale). A ground enemy entering a mined cell takes 90 dmg, ignoring armor (mines hit the belly), and consumes 1 mine. Walls multiply it: enemies halted at a wall stand in a cell, so they take hits repeatedly. Mines never trigger on friendlies. Water cells (`isWater`) cannot be mined. **Real science:** anti-vehicle blast mines, the way Soviet belts at Kursk worked.
- **Enemy counters.** A new enemy `sweeper` flail-tank clears a 300 px stretch at once. Hovering/flying enemies are unaffected. Earthbreaker quakes detonate 40% of cells.
- **Code grounding.** `groundY` / `elevAt` terrain tools; `spawnEnemy` L5581 (`sweeper` kind); `G.parts` for the burst FX.
- **Size:** M. **TM1.** **Research:** none, but `robotics` upgrades the Works to **Smart Mines** (+50% damage, prioritise heavies).
- **Tenet.** A buff with a named enemy answer (sweeper). No cap on the number of Works.
- **One-Button.** Automatic seeding. Ten Works means ten times the density.
- **Line check.** A mined cell is a point on the line, so it works in 1D. Placed *in front of a wall*, it makes the wall deadly.
- **Interactions.** Planet/Naval (shore mines; sea mines are for Undersea Works); Industry (mine stockpile as an IC-sink, answering the "212k IC unspent" anti-pattern with a *useful* standing drain, not a "spend 20,000 button").

### FORT-3: THE DOME LATTICE (shield pooling)

**Pitch.** "A shield generator beside another shield generator should be a stronger shield. At present it is merely a *jealous* one."

**Player fantasy.** A city under a shimmering, ten-layer ceiling that takes a whole barrage to crack.

**Clerk's Annex**
- **Mechanic.** Overlapping domes share a **pooled HP** (sum of `shieldHp` of all domes covering the impact point) and a pooled regen. A hit is split across all domes covering the point (damage divided by N, so N domes absorb N times). Upkeep stays at 2 IC/s each (a real cost). Shield HP is unchanged (900), regen unchanged (28/s, L8409).
- **Enemy counter.** The `shieldgen` and `breacher` enemy domes (L8712) already exist. Add a **Disruptor** warhead: it deals 3x dome damage and is dropped in the same bombardments as bunker-busters, so a monoculture of domes still meets its enemy.
- **Code grounding.** `shieldBlocks` L8901, `SG_LIST` L8903, projectile hit path L8937 (the single place that subtracts `sh.shieldHp`), regen L8409.
- **Size:** S. **TM1.** **Research:** `shieldtech`, existing; combos with `directedenergy` and `pointdefense` (already its prerequisites).
- **Tenet.** A fix to a volume break, not a buff to the base number. Counter named.
- **One-Button.** Pure fix: nothing to click.
- **Line check.** Domes already work on the strip; overlap is horizontal.
- **Interactions.** Energy/Physics (Disruptor is a physics enemy); Autofleet.

### FORT-4: THE ENGINEER'S YARD (rebuild-in-place, plus the Laser Tower heat role)

**Pitch.** "I have rebuilt the same wall nine times. Let the *game* rebuild it for me the tenth."

**Player fantasy.** Walking away from a ravaged front and coming back to find the stations standing again.

**Clerk's Annex**
- **Mechanic.** `engineyard` (INFRA, 180 IC, 50 MP). Within 500 px it (a) repairs DEF buildings 6 HP/s, and (b) when a DEF building dies, leaves a **ghost** that the Yard rebuilds for 60% of its IC, in order of build-queue, at no per-building click. Two Yards means twice the repair rate. Ghosts vanish if the Yard dies.
- **Laser Tower fix (Order Paper #10).** Lasers apply the President's **heat** (Article VII, Order Paper #2): each hit adds heat; a heated ground or air target takes +25% kinetic damage from Bunkers. The Laser Tower then has a real job in the mix, with no nerf to anyone: it is the *spotter* for the Bunker's kinetic fire.
- **Code grounding.** `deployEmplacement` L4564 (ghost pattern is similar); `demolishSelected` L8470 (`b._gone`); `mkBuilding` L3670; the `nanotech` self-repair already in the code (grep `nanotech`) shows the repair loop to hook.
- **Size:** M. **TM1.** **Research:** none for the Yard. The heat link needs the Energy council's heat bill.
- **Tenet.** A cost (IC upkeep and 60% rebuild cost), not a cap. Buffs without power creep: the enemy keeps its counter in the form of `earthbreaker` quakes, which hit the Yard first.
- **One-Button.** Ghost rebuild is automatic.
- **Line check.** Radius repair works horizontally.
- **Interactions.** Energy (heat), Industry (rebuild IC sink), Stability (rebuilding reduces `damageDistrict` stability drain).

---

## 4. HOT TAKE: THE STAR FORT (cities become fortresses)

**Pitch.** "Vauban never lost a siege he designed. Give every city a ring of bastions, and the fleet must *besiege* it."

**Clerk's Annex**
- **Mechanic.** A **megaproject**: `starfort`, 2,400 IC, a per-city upgrade. It adds a ring of 4 walls, 2 bastion gun-points (Bunker-class, 40 dmg) at each flank and +1 `BDEF` tier (`BDEF` L8373, `cityDefend` L8382). Enemy ground forces landing at a Star Fort **stop and siege** (building a visible `siege line` at 120 px that takes 30 s, during which the fleet's overall landing tempo slows). This changes what the *city* is, as Article IV.3 wants projects to do: a civilisation of fortress-towns.
- **Code grounding.** `BDEF`, `cityDefend`, `avoidCityCenter` (outskirt-landing nudge).
- **Size:** L. **TM1.** **Research:** a banked `megaprojects-wip` project node, combo `pointdefense` + `robotics`.
- **Tenet.** A power fantasy plus an enemy answer (the siege is a *behaviour*, not a nerf).
- **One-Button:** a single build, 30 s of auto-construction. Scales with city count.
- **Line check:** a ring of walls on a line is two walls and a gate, which is still a puzzle (FORT-1).

---

## 5. Fire Control: the funeral

Recommendation: **delete it**. `G.fireControl` is never assigned; `pickDesignated` (L8456) is a stub that returns `null`; the "squad target-sharing" callers can be rewritten to read nothing. Removing the dead branch is a size S cleanup. The One-Button Law wants a *standing choice made once*, which this replaces with: each DEF station gets one optional "priority" toggle (air / ground / ordnance), set once per type, never per shot. I do not propose it as a bill. If the President wants it, it is S.

## 6. Alliances sought

- **Ground Bloc whip (Ground Staff)** and the **Armour Corps**: walls and mines give your treads and guns something to stand behind.
- **Artillery Corps**: a wall pins a lander for your shells; the Artillery Corps and I share the "kill zone" doctrine.
- **Energy and Physics (Arsenal Bloc)**: the heat bill; lasers need a reason to exist.
- **Subterranean Works**: a buried Gatehouse.
- **Autofleet (Observer Delegation)**: I want you to *answer* my walls and mines. That is Amendment I's price.
- **Rival insulted:** the **Air Council**, who has won five reports with a sky they never have to *dig*.
