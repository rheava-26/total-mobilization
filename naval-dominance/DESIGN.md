# NAVAL DOMINANCE — Design Document

**Status:** v0.2 — living document. Nothing here is code yet. This is the plan.
**Working title:** Naval Dominance (name-checked: no shipping game uses it; only a HOI4 mechanic).

---

## 1. One-line pitch

A single-player **naval Total War**: conquer a grid of islands turn-by-turn to build a
fleet and an empire, then fight the battles in **real time, in 3D** — zooming from a
strategic map down onto the deck, taking manual control of any gun while a living crew
fights around you — or auto-resolving them if your preparation earned it.

---

## 2. The core fantasy (the North Star)

> You just spent big on a new cruiser and you're pushing an enemy stronghold. You bring
> corvettes, destroyers, two carriers, small vessels. You've turned the coastline into a
> fortress — land artillery, mined waters, cut their supply ships — all in preparation.
> You let them advance. You drop into the main guns. Every sailor feels real: they move
> across the deck, shout commands, go from nervous to frantic as the enemy closes. Your
> ships auto-solve their firing; you jump to an AA gun and start downing planes. The ship
> rocks and creaks in the swell. A big hit lands. You win.

Every design decision serves that 30 seconds. If a feature doesn't make that moment
better, it waits.

---

## 3. Design pillars

1. **Preparation is the game.** Fortify, mine, cut supply, pick your composition. The
   battle is won *before* it starts, and the player feels smart for it.
2. **The crew is the soul.** Reactive, alive-*feeling* sailors. (Stylized — the illusion
   of life, achieved cheaply, not a simulated soul per sailor.)
3. **Juice first — sound & music are load-bearing.** Weighty guns, recoil, ships that
   rock, explosions you *hear*. Non-negotiable **satisfying sound**, plus **built-in
   music (streamed, YouTube-style, like Total Mobilization)**. Music is not polish; the
   game does not feel fun without it. Hard requirement.
4. **Territory is the economy.** You don't grind money for a battleship — you *conquer
   the drydock that builds it.* No tycoon, no timers, no shooting buoys.
5. **Content is data.** One battle engine; every unit/scenario/era is a data file bolted
   on. Variety without proportional work. (See §8.)
6. **Smart AI is a feature, not a chore.** Good AI is the point — imperfect, trainable
   crews and competent fleet AI are what make large-scale battles fun to command.

---

## 4. Structure — two layers

### Campaign layer (turn-based, strategic)
- A grid map of islands / segmented zones. Conquer, hold, build up.
- Units shown as **markers with colored outlines**. Buildings on islands.
- Islands grant *capabilities*, not cash: shipyards (unlock ship classes), repair yards,
  gun foundries, resource nodes, radar, etc. Progression is a **map, not a bank balance.**
- Between turns you prepare: reposition fleets, fortify coasts, lay mines, cut supply.

### Battle layer (real-time, 3D, tactical)
- **Double-click a unit marker → zoom from strategic down to the tactical layer:** the
  actual 3D ship on moving water, guns turning and tracking their targets, smoke rising,
  explosions audible, visible damage/effects, class, and a detailed layout on inspection.
- The player chooses per engagement:
  - **Auto-resolve** — good prep wins on the numbers. Keeps a long campaign from
    exhausting the player and *rewards the prep loop even when you don't play.*
  - **Play it** — command the fleet from above, or possess any gun. AI/crew runs
    everything you aren't personally touching.

---

## 5. Camera & controls

- **Strategic view:** top-down markers over segmented zones. Command at scale (X4-like).
- **Tactical view:** free-orbit / pivot 3D camera around a ship. Zoom out to see fleet
  shapes; zoom in over a carrier and watch planes launch mid-flight.
- **Inspect a ship:** click it for a mega-detailed layout — stats, current orders, per-gun
  aim overlaid on the hull, damage, effects, class. **Turrets highlight as take-overable.**
- **Take over a turret:** click it to control it directly (over-the-gun feel).
- **Two control philosophies (difficulty):**
  - *Accessible (default):* click a target, the gun **auto-aims considering everything**
    (range, lead, ballistics).
  - *Realism mode:* less UI, manual lead and ranging. Same sim, harder skin.

---

## 6. The economy (how we killed the tycoon)

| Old (Navy Tycoon) | Naval Dominance |
|---|---|
| Grind money, wait on a timer | Win battles / hold territory |
| Buy ships from a shop | *Unlock* ship classes by conquering the island that builds them |
| Idle cash offline | No idle anything |
| "Small ship now vs. big ship later" = a wait | Same choice, but **instant**: commit your fleet budget to many small hulls or bank for one capital ship |

Combat *pays* (requisition from wins) so battles are never "too expensive to be worth it."

---

## 7. Modes

- **Campaigns** — persistent island-conquest runs. Domain is content, so naval, ground
  ("War Economy"), or mixed campaigns are the same system.
- **Single Battle Scenarios** — one-off battles with *deep* customization (fleet, enemy,
  map, weather, allowed vehicle classes, era/ruleset). The "erghhh that's a lot" sandbox.

---

## 8. FRAMEWORK vs CONTENT (the anti-scope-creep doctrine)

Build the left side once and well. Never let the right side's size scare us — it's data.

**FRAMEWORK (build once, carefully):**
- 3D battle engine (movement, firing solutions, damage, hit feedback) in **Three.js/WebGL**
- **"Unit = data"** system (below)
- Turn/campaign structure + auto-resolve math
- Juice + sound + **streamed music** system
- Crew reaction system (a few real signals → barks + animations)

**CONTENT (slap on forever, cheaply — nothing here is cut, just deferred):**
- Every ship, sub, tank, plane, helicopter, **airship**, fantasy vessel
- Every scenario, era, campaign, ruleset
- Ground domain, air domain, **fantasy setting**, **ISOT scenarios**

### Unit = data (first schema sketch)
```
Unit = {
  class, era, hull/armor, speed, turnRate,
  domains: [sea] | [land] | [air] | [sea,land]      // amphibious/flight = just a flag
  weapons: [ { turret, arc, range, reload, shell, takeoverable:true }, ... ],
  secondaries: [ helicopter | seaplane | aircraftWing ],   // optional
  crew: { count, skill },        // trainable, imperfect — the 90-men-on-44-guns dream
  sensors, capabilities: [ radar, mineLayer, supplyCut, ... ]
}
```
A destroyer-with-helicopters and a fantasy airship are the same object, different values.

---

## 9. Physics model (performance-driven)

- **Everywhere (strategic + normal play):** fixed penalties. Cheap, no lag. Terrain and
  conditions apply flat modifiers.
- **Tactical close-up only:** real terrain bumpiness / physical downsides / swell. Full
  fidelity only where the player is actually looking.

---

## 10. Vertical slice — the "fun NOW" target

Start small, exactly as intended: **modern era, small ships, single-digit aircraft.**

**The slice:** one modern ship the player controls, with **2–3 take-overable turrets**,
on **moving 3D water**, a couple of enemy aircraft to shoot, **real sound + streamed
music**, and the **strategic ↔ tactical double-click zoom** working at tiny scale. Win when
the enemy is gone; lose if you sink.

If that 60 seconds feels good, the game exists. Everything else is built outward from it.

---

## 11. Locked decisions

- Single-player first (multiplayer *parked*, not killed — keep the battle sim clean).
- Turn-based strategic campaign + real-time **3D** battles.
- **Three.js / WebGL in the browser** — keeps the single-file, no-build, Pages-deploy,
  streamed-music pipeline. (Not Godot/Unity.)
- Strategic markers ↔ tactical 3D via double-click zoom; take-overable turrets.
- Two difficulties: auto-aim (accessible) vs. realism (less UI).
- Physics-lite: flat penalties globally, real physics only in tactical close-up.
- Territory-based progression; no money tycoon; no wait-timers.
- Player starts from a **single ship**; dev starts **modern + small**.
- Built-in streamed music = required.

## 12. Open questions (next to resolve)

- **UI/HUD layout** — waiting on the user's sketch (highest priority: the feel of the UI).
- **Campaign map granularity** — size, island count, how a turn plays.
- **Crew visualization budget** — how much on-deck animation we can afford in WebGL.

## 13. Parked (deliberately, so scope survives)

Multiplayer · Roblox port · semi-open-world quadrant movement (From-the-Depths-style) ·
fantasy setting · ISOT scenarios · deep War-Thunder component sim · full simulated-soul
crew. None cut. All revisited *after* the vertical slice is fun.
