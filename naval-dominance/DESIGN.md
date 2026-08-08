# NAVAL DOMINANCE — Design Document

**Status:** v0.6 — living document. Nothing here is game code yet. This is the plan.
**Working title:** Naval Dominance (name-checked: no shipping game uses it; only a HOI4 mechanic).

> ✅ **Render style resolved (§15):** ONE 3D world (Three.js/WebGL). Top-down camera = the
> strategic map; zoom in = 3D combat. From-the-Depths-style. The camera *is* the UI.
> **Build plan:** see `FRAMEWORK_PLAN.md`.

---

## 1. One-line pitch

A single-player **naval Total War** in one seamless 3D world: from a clean top-down map you
conquer a **panelized layout** of islands turn-by-turn to build a fleet and an empire, then
**zoom down into real-time 3D battles** — taking manual control of any gun while your crew
fights around you — or auto-resolve them if your preparation earned it.

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
2. **The crew is the soul.** Reactive, alive-*feeling* sailors — the illusion of life, cheaply.
3. **Juice first — sound & music are load-bearing.** Weighty guns, explosions you *hear*.
   Non-negotiable satisfying sound (recreate the *quality* of Navy Tycoon's audio, our own
   assets), plus **built-in streamed music** (YouTube-style, like Total Mobilization). Required.
4. **Territory is the economy.** Hold islands → earn industrial capacity → build. No grind,
   no idle timers, no buoys. (Currency is fine; the *grind* was the enemy.)
5. **Content is data.** One engine; every unit/scenario/era is a data file bolted on.
6. **Smart AI is a feature.** Imperfect, trainable crews + competent fleet AI = big battles
   that are fun to command.
7. **Click anything.** Ship, gun, crew, building — everything is inspectable and directable.

---

## 4. Structure — two layers, one world

The strategic and tactical layers are the **same 3D world at different camera heights**
(From-the-Depths-style), not two separate scenes.

- **Campaign layer (turn-based, top-down camera):** conquer the panelized layout (§5),
  build up, prepare. Units are markers/tokens; fleets move between adjacent zones.
- **Battle layer (real-time, camera zoomed in):** the physical 3D fight. **Auto-resolve**
  (prep wins on the numbers) or **play it** (command from above / possess any gun; AI runs
  the rest).

---

## 5. Campaign map

- **A flat top-down 3D world (From-the-Depths style).** Not a rotating globe — the "map" is
  the same 3D world viewed from straight above, so it reads like a clean 2D strategy map.
- **Panelized zones** tessellate the world. Logically a **small adjacency graph** — scales
  cleanly (bigger maps = finer subdivision = more nodes).
- **Zones** are contested territory; each holds open water + **islands**.
- **Islands are captured individually for their resources / capabilities:** shipyards,
  repair yards, gun foundries, resource nodes, radar, airfields. Take the island → gain the
  capability.
- **Holding territory requires a garrison.** Control = presence. A zone/island you leave
  undefended can be retaken. (Ties directly into the garrison system, §8.)
- **Junction nodes** (where zone edges meet) = **chokepoints / ports / supply lanes** —
  where you cut enemy supply and fortify. Serves the preparation pillar.
- **Art direction:** From-the-Depths-adjacent but *less technical* — readable, stylized.

---

## 6. Camera, controls & the tactical HUD

**Zoom is the core verb — the camera moving through one 3D world**, from top-down strategic
markers → a single ship's deck → a single gun.

**Selecting a ship** shows an info panel (per the sketch): name, **class**, type, **damage
state**, **current orders**, **ammo %**. The ship is drawn with its **parts as ringed icons**.

**Direct manipulation of orders:**
- Drag the **heading arrow** → a **dotted line previews** the new move order before you commit.
- **Direct specific guns** at specific targets/locations (easier zoomed out for the wider view).

**Everything is clickable:**
- **Hover a part** → **colored by health/status.**
- **Click a gun** → stats: type, what it does, ammo, and **who's crewing/staffing it.**
- **Click a turret** → take manual control of it.

**Weapon-type icons:** at-a-glance icons for weapon type (the Navy Tycoon *convention*,
drawn in our own stylized art). Readable > realistic.

---

## 7. Difficulty = a realism spectrum

Not "more enemy HP." Difficulty is **how much the game aims for you**: hyper-simple
(click target → auto-aim & fire) → mid (auto-aim, you manage priorities) → realism (less
UI, manual lead & ranging). Same sim, different amount of help.

---

## 8. Production & Garrisons

Stolen — proudly — from Total Mobilization.

- **Every building (built or captured) carries a unit garrison** and produces a **baseline
  of units per turn, automatically.** Capture a drydock → it *immediately* yields its
  garrison floor (e.g. ~5 patrol boats/turn).
- **Garrisons also hold territory** (§5) — the standing presence that keeps a zone yours.
- **Bigger/special units are queued with Industrial Capacity** (§9): capable buildings
  (docks, airfields) spend per-turn capacity to build a cruiser / heavy bomber over turns.
- **Why it's right:** a **fall-back baseline** so experimenting never bricks a run,
  encourages **player freedom**, keeps combat rewarding. Freedom *with* a floor.

---

## 9. Economy — Industrial Capacity (the tycoon stays dead, currency is fine)

The disease was the *grind and the idle timer*, not currency. So:
- **Industrial Capacity (IC):** a per-turn build resource, **driven by territory** (islands,
  foundries, resource nodes). You don't grind or idle for it — you *hold ground* for it.
- **Spend IC** queuing big units at capable buildings; garrisons stay free on top.
- Combat **pays** (winning yields IC / requisition) so battles are never "too expensive."
- "Many small vs. one capital ship" = an **instant** allocation of your IC and build slots.

No shop-grind, no offline idle income. Territory → capacity → fleet.

---

## 10. Modes

- **Campaigns** — persistent island-conquest runs (naval, ground "War Economy", or mixed —
  same system, domain is content).
- **Single Battle Scenarios** — one-off battles, *deeply* customizable (fleet, enemy, map,
  weather, allowed classes, era).

---

## 11. FRAMEWORK vs CONTENT (the anti-scope-creep doctrine)

Build the left once and well. The right side's size is a feature — it's data.

**FRAMEWORK (build once):** 3D world + camera/zoom · battle engine (dual-fidelity, §12) ·
**unit = data** · campaign graph · auto-resolve math · juice + sound + **streamed music** ·
crew-reaction system · **garrison / Industrial-Capacity production** · click-anything UI
shell · save/load · content pipeline.

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

---

## 12. Combat resolution — dual fidelity (performance-driven)

Two levels of the same fight, chosen by how close the player is looking:
- **Statistical (zoomed out / auto-resolve):** compute hit chance from **crew skill +
  range + weapon type + conditions**. If hit → draw tracer gun→target + damage; if miss →
  miss tracer, no damage indicator. Cheap, runs for many ships.
- **Ballistic (close combat, zoomed in):** **real projectile physics + actual hit
  registration** — guns must genuinely hit. Full drama where the player is.

Physics generally: flat penalties everywhere; real terrain bumpiness / swell only in the
close-up. **No full War-Thunder component/damage sim** — deliberately scoped out. Parts are
*readable* (hover-health, click-stats), not individually simulated.

---

## 13. Starter roster (base content — small & simple)

Modern-ish baseline (era flexible per scenario; Z-32-style real class names welcome).
- **Patrol boats** — heavy machine guns, bombardment grenades, small rockets.
- **Small helicopters.**
- **Tanks / ground vehicles.**
- **Mortars.**

Multi-domain from day one (proves "unit = data"), each unit kept simple.

---

## 14. Vertical slice — the "fun NOW" target

One player ship with **2–3 take-overable turrets**, a couple of enemy aircraft, **real
sound + streamed music**, the **click-anything HUD** (info panel, hover-health, drag-arrow
orders), and the **zoom** working in the single 3D world, with **ballistic close combat**.
Win when the enemy is gone; lose if you sink. If that 60 seconds feels good, the game exists.

---

## 15. Render style — RESOLVED

**One 3D world (Three.js/WebGL). The camera is the whole UI.** Strategic = top-down camera
(reads flat); combat = same world zoomed in (full 3D). Zoom moves seamlessly between them.
Reference: **From the Depths.** No rotating globe (flat top-down world; zones lie on it).

---

## 16. Locked decisions

Single-player first · turn-based campaign + real-time battles · **one 3D world, Three.js/
WebGL, camera-as-UI, flat top-down map, From-the-Depths reference** · panelized adjacency
graph · islands captured individually · **holding territory requires a garrison** ·
junction-node chokepoints · **garrison floor + Industrial-Capacity production (currency is
fine, grind is not)** · **dual-fidelity combat (statistical far / ballistic close)** ·
click-anything & zoom-centric UI · difficulty = realism spectrum · our-own weapon-type
icons · **no deep War-Thunder sim** · required streamed music · player starts from a single
ship · dev starts modern + small · ~12–16 zones for the first campaign · turn loop =
Production → Orders → Resolution.

## 17. Open questions (minor — settle during the build)

- Exact IC numbers / build times per unit class (tuning, not architecture).
- Precise zone adjacency & first-campaign layout.
- Crew visualization budget in WebGL (zoom-gated, instanced — tune to perf).

## 18. Parked (deliberately)

Multiplayer · Roblox port · rotating globe · semi-open-world quadrant movement · fantasy
setting · ISOT scenarios · **deep War-Thunder component sim** · full simulated-soul crew ·
much larger maps (design already scales). None cut — revisited after the slice is fun.
