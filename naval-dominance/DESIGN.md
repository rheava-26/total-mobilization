# NAVAL DOMINANCE — Design Document

**Status:** v0.4 — living document. Nothing here is game code yet. This is the plan.
**Working title:** Naval Dominance (name-checked: no shipping game uses it; only a HOI4 mechanic).

> ⚠️ **One decision is still open and it's a big one — render style (§15). Read that first.**

---

## 1. One-line pitch

A single-player **naval Total War**: conquer a **panelized map** of islands turn-by-turn to
build a fleet and an empire, then fight the battles in real time — zooming from the
strategic map down onto a ship, taking manual control of any gun while your crew fights
around you — or auto-resolving them if your preparation earned it.

---

## 2. The core fantasy (the North Star)

> You just spent big on a new cruiser and you're pushing an enemy stronghold. You bring
> corvettes, destroyers, two carriers, small vessels. You've turned the coastline into a
> fortress — land artillery, mined waters, cut their supply ships — all in preparation.
> You let them advance. You drop into the main guns. Every sailor feels real. Your ships
> auto-solve their firing; you jump to an AA gun and start downing planes. The swell
> rocks the deck. A big hit lands. You win.

Every decision serves that 30 seconds. If a feature doesn't make that moment better, it waits.

---

## 3. Design pillars

1. **Preparation is the game.** Fortify, mine, cut supply, pick your composition. The
   battle is won *before* it starts, and the player feels smart for it.
2. **The crew is the soul.** Reactive, alive-*feeling* sailors — the illusion of life,
   achieved cheaply.
3. **Juice first — sound & music are load-bearing.** Weighty guns, explosions you *hear*.
   Non-negotiable satisfying sound (recreate the *quality* of Navy Tycoon's audio in our
   own assets), plus **built-in streamed music** (YouTube-style, like Total Mobilization).
   Hard requirement.
4. **Territory is the economy.** Conquer the drydock, don't grind for it. No tycoon, no
   timers, no buoys.
5. **Content is data.** One engine; every unit/scenario/era is a data file bolted on.
6. **Smart AI is a feature.** Imperfect, trainable crews + competent fleet AI = large
   battles that are fun to command.
7. **Click anything.** Ship, gun, crew, building — everything is inspectable and directable.

---

## 4. Structure — two layers

- **Campaign layer (turn-based, strategic):** conquer a panelized map (§5), build up,
  prepare. Units are markers/tokens; fleets move between adjacent zones.
- **Battle layer (real-time, tactical):** zoom onto the fight. **Auto-resolve** (prep wins
  on the numbers) or **play it** (command from above / possess any gun; AI runs the rest).

---

## 5. Campaign map

- **A panelized map — zones tessellated like the sketch.** Whether it's a 3D globe or a
  flat stylized 2D map is the open render decision (§15) — but **logically it's the same
  small adjacency graph either way** (Zone A borders Zone B…), so the render choice does
  NOT threaten the design. Scales cleanly: bigger maps = subdivide finer = more nodes.
- **Zones** are contested territory; each holds open water + **islands**.
- **Islands are captured individually for their resources / capabilities:** shipyards,
  repair yards, gun foundries, resource nodes, radar, airfields. Take the island → gain
  the capability. Progression is a **map, not a bank balance.**
- **Junction nodes** (where zone edges meet) = **chokepoints / ports / supply lanes** —
  where you cut enemy supply and fortify. Serves the preparation pillar.
- **Art direction:** From-the-Depths-adjacent but *less technical* — readable, stylized.

---

## 6. Camera, controls & the tactical HUD

**Zoom is the core verb.** The whole UI is fluidly zooming the player in and out —
strategic markers → a single ship's deck → a single gun.

**Selecting a ship** shows an info panel (per the sketch): name, **class**, type, **damage
state**, **current orders**, **ammo %**. The ship is drawn with its **parts as ringed
icons** overlaid.

**Direct manipulation of orders:**
- Drag the **heading arrow** → a **dotted line previews** the new move order before you commit.
- **Direct specific guns** at specific targets/locations (select gun, then target, easier
  when zoomed out for the wider view).

**Everything is clickable:**
- **Hover a part** → it's **colored by health/status**.
- **Click a gun** → stats: type, what it does, ammo, and **who's crewing/staffing it**.
- **Click a turret** → take manual control of it.

**Weapon-type icons:** at-a-glance icons that communicate weapon type (the Navy Tycoon
*convention* — clear type-ID for fast choosing), drawn in our own stylized art so they're
ours. Readable > realistic.

---

## 7. Difficulty = a realism spectrum

Not "more enemy HP." Difficulty is **how much the game aims for you**:
- **Hyper-simple:** click a target, guns **auto-aim and fire**. Lots of UI assist.
- **Mid:** auto-aim, but you manage priorities/positioning.
- **Realism:** less UI, manual lead and ranging.

Same sim underneath, different amount of help on top.

---

## 8. Production & Garrisons (the resolved model)

Stolen — proudly — from Total Mobilization, because it's one of its best ideas.

- **Every building (built or captured) carries a unit garrison** and produces a **baseline
  of units per turn, automatically.** Capture a drydock → it *immediately* yields its
  garrison floor (e.g. ~5 patrol boats/turn). No money-grind, no instant-magic.
- **Capable buildings also queue bigger/special units over turns** — invest the same dock
  toward, say, one destroyer per turn; build an airfield to man heavy bombers over time.
- **Why it's the right call:** gives a **fall-back baseline** so experimenting never bricks
  a run, encourages **player freedom**, and keeps combat rewarding (you can always rebuild
  and push again). Freedom *with* a floor.

Open sub-question: exact build times / whether big units are turn-queued vs
resource-gated. (Lean: turn-queued at capable buildings, on top of the automatic garrison.)

---

## 9. Economy (how we killed the tycoon)

Territory + garrisons *are* the economy. Combat **pays** (requisition from wins) so battles
are never "too expensive to bother." The "small ships now vs. capital ship later" choice is
an **instant** fleet-composition decision, not a wait.

---

## 10. Modes

- **Campaigns** — persistent island-conquest runs (naval, ground "War Economy", or mixed —
  same system, since domain is content).
- **Single Battle Scenarios** — one-off battles, *deeply* customizable (fleet, enemy, map,
  weather, allowed classes, era).

---

## 11. FRAMEWORK vs CONTENT (the anti-scope-creep doctrine)

Build the left once and well. The right side's size is a feature, not a threat — it's data.

**FRAMEWORK (build once):** battle engine · **unit = data** system · campaign graph ·
auto-resolve math · juice + sound + **streamed music** · crew-reaction system · **garrison /
turn-production** system · click-anything UI shell · save/load · content pipeline.

**CONTENT (slap on forever — nothing cut, just deferred):** every ship/sub/tank/plane/heli/
**airship**/fantasy vessel · every scenario/era/campaign · ground & air domains · **fantasy
setting** · **ISOT scenarios**.

### Unit = data (schema sketch)
```
Unit = {
  class, era, hull/armor, speed, turnRate,
  domains: [sea] | [land] | [air] | [sea,land],       // amphibious/flight = just a flag
  weapons: [ { turret, type, arc, range, reload, shell, ammo,
               crew:{count,skill}, takeoverable:true }, ... ],
  secondaries: [ helicopter | seaplane | aircraftWing ],   // optional
  crew: { count, skill },       // trainable, imperfect
  sensors, capabilities: [ radar, mineLayer, supplyCut, ... ]
}
```
A destroyer-with-helicopters and a fantasy airship are the same object, different values.

---

## 12. Physics model (performance-driven)

Flat penalties everywhere (cheap); real terrain bumpiness / swell / physical downsides
**only in the tactical close-up**, where the player is actually looking.
**No full War-Thunder component/damage sim** — the user deliberately scoped this out. Parts
are *readable* (hover-health, click-stats), not individually simulated.

---

## 13. Starter roster (base content — keep it small & simple)

Modern-ish baseline (era stays flexible per scenario; Z-32-style real class names welcome).
- **Patrol boats** with basic weapons: heavy machine guns, bombardment grenades, small rockets.
- **Small helicopters.**
- **Tanks / ground vehicles.**
- **Mortars.**

Multi-domain from day one (proves the "unit = data" framework), but each unit kept simple.

---

## 14. Vertical slice — the "fun NOW" target

One player ship with **2–3 take-overable turrets**, a couple of enemy aircraft to shoot,
**real sound + streamed music**, the **click-anything HUD** (info panel, hover-health,
drag-arrow orders), and the **zoom** working. Win when the enemy is gone; lose if you sink.
If that 60 seconds feels good, the game exists.

---

## 15. ⚠️ RENDER STYLE — DECISION PENDING

The user has drifted: **firmly 3D earlier ("2D would kill the drama", "I prefer 3D for
aiming guns") → now leaning stylized 2D ("I like a stylized 2D art style", unsure about the
globe).** This is the single biggest open decision and it reshapes the whole build.

- **Stylized 2D / top-down (2.5D):** matches the tactical sketch (which *is* top-down 2D),
  matches the stated art preference, matches the solo-dev/single-file pipeline, and is
  **dramatically less work.** Drama comes from juice (smoke, muzzle flash, screen shake,
  sound), not from a 3D deck.
- **3D (Three.js/WebGL):** more visceral gun-aiming and swell, but far more art & tech and
  a real risk to momentum.

**Lean:** stylized 2D/top-down. Awaiting the user's lock. Everything else in this doc works
under either choice.

---

## 16. Locked decisions

Single-player first · turn-based strategic campaign + real-time battles · panelized-map
adjacency graph · islands captured individually for resources · junction-node chokepoints ·
**garrison / per-turn production** · territory economy (no tycoon/timers) · click-anything &
zoom-centric UI · difficulty = realism spectrum (incl. click-to-auto-fire) · our-own
weapon-type icons · **no deep War-Thunder sim** · required streamed music · player starts
from a single ship · dev starts modern + small.

## 17. Open questions

- **§15 render style (2D vs 3D)** — top priority, blocks the build plan.
- Big-unit build times (turn-queued vs resource-gated) on top of the garrison floor.
- Zone count & exact turn loop (move → resolve → build?).
- Crew visualization budget.

## 18. Parked (deliberately)

Multiplayer · Roblox port · semi-open-world quadrant movement · fantasy setting · ISOT
scenarios · **deep War-Thunder component sim** · full simulated-soul crew · much larger
maps (design already scales to them). None cut — revisited after the slice is fun.
