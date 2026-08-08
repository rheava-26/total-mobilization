# NAVAL DOMINANCE — Design Document

**Status:** v0.1 — living document. Nothing here is code yet. This is the plan.
**Working title:** Naval Dominance (name-checked: no shipping game uses it; only a HOI4 mechanic).

---

## 1. One-line pitch

A single-player **naval Total War**: conquer a grid of islands turn-by-turn to build a
fleet and an empire, then fight the battles in **real time** — dropping into any gun on
any ship while a living crew fights around you — or auto-resolving them if your
preparation earned it.

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
3. **Juice first.** Weighty guns, real recoil, ships that rock, and — non-negotiable —
   **satisfying sound.** Audio/feedback is a first-class system, not final polish.
4. **Territory is the economy.** You don't grind money for a battleship — you *conquer
   the drydock that builds it.* No tycoon, no timers, no shooting buoys.
5. **Content is data.** One battle engine; every unit/scenario/era is a data file bolted
   on. Variety without proportional work.

---

## 4. Structure — two layers

### Campaign layer (turn-based)
- A grid map of islands. Conquer, hold, build up.
- Islands grant *capabilities*, not cash: shipyards (unlock ship classes), repair yards,
  gun foundries, resource nodes, radar, etc. Progression is a **map, not a bank balance.**
- Between turns you prepare: reposition fleets, fortify coasts, lay mines, cut supply.

### Battle layer (real-time)
- When fleets meet, the player chooses:
  - **Auto-resolve** — good prep wins on the numbers. Keeps a long campaign from
    exhausting the player and *rewards the prep loop even when you don't play.*
  - **Play it** — drop in, command the fleet from above, or possess any gun on any ship.
    AI/crew runs everything you aren't personally touching.

---

## 5. Modes

- **Campaigns** — persistent island-conquest runs. Ship domain is just content, so a
  naval campaign, a ground campaign ("War Economy"), or a mixed one are the same system.
- **Single Battle Scenarios** — one-off battles with *deep* customization (fleet, enemy,
  map, weather, allowed vehicle classes, era/ruleset). The "erghhh that's a lot of
  options" sandbox.

---

## 6. The economy (how we killed the tycoon)

| Old (Navy Tycoon) | Naval Dominance |
|---|---|
| Grind money, wait on a timer | Win battles / hold territory |
| Buy ships from a shop | *Unlock* ship classes by conquering the island that builds them |
| Idle cash offline | No idle anything |
| "Small ship now vs. big ship later" = a wait | Same choice, but **instant**: commit your fleet budget to many small hulls or bank for one capital ship |

Combat *pays* (requisition from wins) so battles are never "too expensive to be worth it"
— the mistake most strategy games make.

---

## 7. FRAMEWORK vs CONTENT (the anti-scope-creep doctrine)

The project lives or dies on this line. Build the left side once and well. Never let the
right side's size scare us — it's just data.

**FRAMEWORK (build once, carefully):**
- Battle engine (movement, firing solutions, damage, hit feedback)
- "Unit = data" system (stats, weapons, secondaries, crew, sprites all from a file)
- Turn/campaign structure + auto-resolve math
- Juice & sound system
- Crew reaction system (a few real signals → barks + animations)

**CONTENT (slap on forever, cheaply — nothing here is cut, just deferred):**
- Every ship, sub, tank, plane, helicopter, **airship**, fantasy vessel
- Every scenario, era, campaign, ruleset
- Ground domain ("War Economy"), air domain, **fantasy setting**, **ISOT scenarios**

If a unit is data, a fantasy airship and a WW2 destroyer are the same code.

---

## 8. Vertical slice — the "fun NOW" target

Before *any* campaign, map, or economy code, we build **one real-time battle** and make
it fun. If this isn't fun, nothing else matters.

**The slice:**
- Your flagship + a few AI-controlled allied ships.
- An incoming enemy fleet + a few aircraft.
- You can switch between the flagship's **main guns** and an **AA gun**.
- Point-and-click fire with **real sound + screen/hit feedback**.
- Stylized crew that visibly reacts (incoming fire, a kill, fire on deck).
- Win when the enemy is destroyed; lose if you sink.

That's it. That 60 seconds is the whole bet. Everything else is built outward from it
only once it feels good.

---

## 9. Locked decisions

- Single-player first (multiplayer *parked*, not killed — keep the battle sim clean).
- Turn-based campaign + real-time battles ("turn-based but real-time combat" = yes).
- Territory-based progression; no money tycoon; no wait-timers.
- Two modes: Campaigns + customizable Single Battles.
- Content-agnostic battle framework (naval / ground / air / fantasy all plug in).

## 10. Open questions (next to resolve)

- **Tech lane:** stay in the proven single-file HTML/canvas pipeline (fast, stylized
  2.5D, playable in days) *or* move to an engine (Godot/Unity — enables the 3D deck, but
  slower and bigger scope)? Leaning: **stay in canvas, stylized.**
- **Battle perspective:** top-down tactical, side-on, or over-the-gun? (Affects the
  whole art & feel.)
- **Default era/setting:** WW2-ish baseline? (Variable per scenario regardless.)
- **Campaign map granularity:** how big, how many islands, how a turn plays.

## 11. Parked (deliberately, so scope survives)

Multiplayer · Roblox port · semi-open-world quadrant movement (From-the-Depths-style) ·
fantasy setting · ISOT scenarios · deep War-Thunder component sim · full 3D deck
immersion. None cut. All revisited *after* the vertical slice is fun.
