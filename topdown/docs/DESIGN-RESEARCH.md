# Total Mobilization — Design Research Brief (top-down rebuild)

> **Source note:** This environment blocks the open web, so this is synthesized from
> training knowledge of these games and design patterns — not live web scraping.
> Treat specific mechanics as **directionally accurate, verify exact numbers**. Every
> game named below is real and worth looking up when you have web access.

## 0. The concept we're designing for
Top-down 2D strategy. Stacked **layers** (surface / underground / air) you toggle for
"2.5D" depth without a 3D engine. **Massed units** that read as **frontlines/formations**,
not a clump — units are *markers*, not literal-scale sprites. **Deck-based**: each
scenario/faction gives a specific set of deployable units + a constrained tech tree.
**Scenario/campaign** driven (authored maps, objectives, narrative), custom maps a plus.
Theme: **total mobilization → the anticlimax → demobilization** — a hyper-armed,
superhuman, decentralized society with nothing left to fight, collapsing into decades of
**partisan/civil/asymmetric war**. Urban + fortification combat matter. "Streaky" soft
visuals over blocky.

The signature idea nobody else has: **the war is the tutorial; the *aftermath* is the game.**

---

## 1. Making massed real-time combat read as FRONTLINES, not a dogpile
The core failure of the current side-view build is that combat has no *shape*. Top-down
fixes the geometry; these are the systems that make hundreds of units legible:

- **Hearts of Iron IV** — the front is an *abstract line* units snap to; combat is
  resolved by frontage/width, terrain, and supply, then shown with a moving line + arrows.
  *Takeaway:* you don't simulate 800 duels — you simulate a **line** and render units
  *along* it. A "combat width" cap means only so many units fight at a point; the rest are
  reserves/reinforcements. This alone kills "everyone shoots one drone."
- **Wargame / WARNO / Steel Division** — real-time, but readable via **line-of-sight,
  cover, suppression, and unit *cards*** (see §2). Frontlines emerge from LOS + cover, not
  scripted. *Takeaway:* cover and suppression create lines organically; a suppressed unit
  stops firing and pulls back — that's a frontline forming.
- **Foxhole** — persistent massed war where the front is defined by **logistics and
  entrenchment**. *Takeaway:* supply lines + dug-in positions make a front *matter*;
  trenches/bunkers are the frontline.
- **Total War** — formations + **morale/rout**. Units break and flee; battles are decided
  by flanks and morale collapse, not last-man-standing. *Takeaway:* add **morale** so a
  battle *breaks* instead of grinding to zero HP — this is what makes combat feel vicious
  and decisive rather than arcadey attrition.
- **They Are Billions** — hordes vs. **chokepoints + walls**; readability comes from the
  player shaping *where* the mass hits. *Takeaway:* fortifications as funnels.
- **Command Ops / Radio General** — **NATO marker** abstraction + fog + delayed orders.
  *Takeaway:* markers scale to hundreds of units where sprites can't; orders take time to
  propagate (friction) which feels like real command.
- **Rusted Warfare / Mindustry** — top-down RTS at scale done in 2D; both keep units small
  and legible. *Takeaway:* small unit footprints + clear team color.

**Concrete recommendation:** adopt a **frontage/combat-width** model. A contested tile/line
segment only lets *N* units engage; others queue as reserves. Add **suppression + morale**
so lines form and break. Render the mass *along the front*, not swarming a single target.
Keep the physics/spectacle for the units that *are* engaged — you get the charm without the
smear.

---

## 2. Deck-based units + constrained trees
This is a strong, underused fit for "different scenarios play differently."

- **Wargame / WARNO "deck builder"** — *the* reference. Before a battle you build a **deck**
  of unit cards within point/slot limits by category (infantry, armor, air, support,
  recon, logistics). Your deck *is* your doctrine. *Takeaway:* a scenario/faction = a legal
  card pool + slot limits. The demobilization factions (loyalists, warlords, partisans,
  superhuman cells) become **distinct decks** with wildly different toolkits.
- **Clash Royale** — a small **deck of deployable units** + an **elixir economy** dripping
  over time; you play cards into lanes in real time. *Takeaway:* a tight deck (8 units) +
  a resource that *regenerates* (your influence-momentum idea maps perfectly here) makes
  every deployment a decision, not spam. Lanes → frontline segments.
- **Warpips** — tug-of-war: spend a regenerating resource to deploy units that walk to the
  front; unit unlocks form a mini-deck. *Takeaway:* deployment-economy + auto-advancing
  units = readable frontline with deckbuilt variety. Closest single reference to your loop.
- **Slay the Spire / roguelike deckbuilders** — **archetypes & synergy**: cards are weak
  alone, strong in combos. *Takeaway:* design units so a *deck* has an identity (armor-spam
  vs. partisan-ambush vs. superhuman-alpha-strike), not a flat roster.
- **Into the Breach** — constrained, **perfect-information** tactics; tiny unit count, huge
  depth. *Takeaway:* if you ever want the tactical layer to be a puzzle, fewer units +
  telegraphed enemy intent beats more units.

**Concrete recommendation:** Deck = **8–12 unit cards + a short tech spur**, drafted per
scenario. A regenerating deployment resource (reuse your **influence momentum** — ramps
while you hold, resets when you deploy) gates spam and rewards timing. Factions differ by
their legal card pool, not by stat tweaks on the same units.

---

## 3. Top-down layered / underground ("2.5D" without 3D)
- **Oxygen Not Included** — a **cross-section** world where layers stack vertically and you
  see them at once; systems (gas, liquid, power) propagate between layers. *Takeaway:* you
  can show layers *simultaneously* (cutaway) OR toggle; ONI proves simultaneous is readable
  if color/hatching separates strata.
- **Factorio / Mindustry** — top-down with a hidden logistics layer (belts, pipes,
  underground belts). *Takeaway:* "underground" can be a **routing/connection** layer
  (tunnels linking bases, hidden supply) rather than a full second battlefield — cheaper and
  still meaningful.
- **Dome Keeper / SteamWorld Dig / Subterrain** — surface vs. underground as **two modes**
  you move between. *Takeaway:* toggling is fine if each layer has a clear *purpose*
  (surface = battle, underground = infrastructure/movement/safety, air = overlay).

**Design questions to answer (these define the game):**
1. Is a layer its own battlefield, or a *routing/utility* plane? (Recommend: **underground =
   movement + logistics + hidden bases**; **air = an overlay** with its own combat band; only
   **surface** is the main fight. Keeps scope sane.)
2. How do layers *interact*? (e.g., artillery/orbital hits all layers; tunnels let you
   flank *under* a front and pop up behind it — that's a killer mechanic and very
   "decentralized partisan warfare.")
3. Toggle vs. simultaneous view? (Recommend **toggle with a peek**: main layer solid,
   others as faint overlay.)

---

## 4. Asymmetric / insurgency / aftermath — the DEMOBILIZATION theme
This is your differentiator. Very few strategy games are about *winning being the problem*.

- **Rebel Inc. (Ndemic)** — **the** reference for your theme. The military phase is easy;
  **stabilization is the hard game** — you juggle corruption, unemployed ex-soldiers,
  displaced people, and insurgents who *emerge from your own neglect*. *Takeaway:* model the
  demobilization as a **stabilization vs. fragmentation** balance: demobbed superhumans and
  arms stockpiles become insurgent potential if you don't reintegrate them. This maps
  *directly* onto your fiction.
- **Frostpunk** — society under existential strain; **hard governance laws** with moral
  cost; a "hope vs. discontent" meter. *Takeaway:* your post-war society needs a
  **legitimacy/discontent** system — decommission the war economy too fast and you cause
  collapse; too slow and warlords entrench. (Your `stability` mechanic is already the seed.)
- **This War of Mine** — war from the **civilian** side; scarcity + moral weight.
  *Takeaway:* consider civilian/displaced-population units as a resource *and* a liability.
- **Foxhole** — attrition + logistics as the substance of war. *Takeaway:* partisan war is a
  *logistics* war — cutting supply matters more than battles.
- **Suzerain / We The Revolution** — political narrative games; **choices with factional
  consequences**. *Takeaway:* frame the campaign as **decisions** (who do you disarm first?
  do you keep the orbital nukes?) with branching factional fallout.

**Concrete recommendation:** The signature mode — **"DEMOBILIZATION"** — starts *after* a
2-second victory. You inherit a maxed-out war machine and must **wind it down** while
factions (loyalists / regional warlords / superhuman cells / displaced masses) fracture. Every
weapon you *don't* decommission is one that gets used against you. It's a strategy game about
**entropy**, and it's genuinely novel.

---

## 5. Visual readability for hundreds of top-down units
- **Silhouette + team color first, detail last.** At marker scale, players read *color* and
  *shape class* (blob = infantry, arrow = armor, chevron = air), not art.
- **NATO-symbol lineage** (HoI4, Command Ops): abstract markers scale infinitely and read as
  *military*. You can stylize them — they don't have to be literal NATO.
- **"Streaky" = motion + lines.** Soft trails, motion blur, and letting units *smear along a
  front* reads as a battle line and as speed. Use **additive glow for energy/beams** (you
  already do this), **soft shadows** to lift units off the ground, and **directional trails**
  for movement.
- **Show the state of the battle, not just the units:** a **frontline ribbon** that bulges/
  recedes, per-sector **strength bars**, and a **momentum indicator** per side. Players
  should read "we're losing the left" at a glance without counting sprites.
- **Fog of war / recon** makes the map feel alive and makes partisan ambushes possible.
- **Color language for the theme:** loyalists cold blue, warlords rust/orange, partisans
  green, superhuman cells violet-white (ties to your beam/uranium aesthetic).

---

## 6. Scenarios, campaign & custom maps
- **Data-driven maps.** Store maps as **tilemaps + JSON** (terrain grid, spawn zones,
  objectives, triggers). This makes custom maps and an editor almost free later.
- **StarCraft / WarCraft III / Age of Empires editors** are the gold standard for longevity —
  triggers (on-event → do-thing) turned those games into platforms. Even a *simple*
  trigger system (`when X, spawn/objective/dialogue Y`) massively extends replay value.
- **Campaign as authored beats over a persistent state:** each mission mutates a world-state
  (which factions armed, which cities held) that the next inherits — matches the "decades of
  war" fiction.
- **Vertical slice first:** build **ONE** scenario end-to-end (one map, one deck, one
  faction conflict, the demobilization loop) before generalizing. Prove the fun, then scale.

---

## 7. Recommended reading / watching (look up when you have web)
*(titles from memory — search these exact phrases)*
- **GDC — "The Rules of Rulers"** and RTS AI talks; search **"GDC RTS design"**,
  **"GDC frontline system"**, **"GDC Hearts of Iron combat width"**.
- **WARNO / Wargame "deck building" guides** — for the card/deck-per-doctrine model.
- **Rebel Inc. design/postmortem** — counterinsurgency & stabilization loops.
- **Frostpunk "society/discontent" design breakdowns.**
- **Oxygen Not Included** cross-section/layer discussions for the 2.5D view.
- **Warpips** and **Clash Royale** teardowns for deployment-economy real-time combat.
- Subreddits: **r/gamedesign**, **r/RealTimeStrategy**, **r/RTS**, **r/tabletopgamedesign**
  (wargame frontage/combat-width theory lives here too).
- Books/articles: **"Game Programming Patterns"** (Nystrom, free online) for architecture;
  **flow-field pathfinding** articles (how Supreme Commander / Planetary Annihilation move
  thousands of units) — search **"flow field pathfinding RTS"**.

---

## 8. My honest top-level recommendation
1. **Keep from the old build (as DATA, not code):** the economy, research/tech ideas, the
   unit/building *concepts*, the strike types, the balance lessons, and the lore. Rebuild the
   *engine* top-down.
2. **The three pillars that make it *your* game:** (a) a **frontage/morale frontline** so
   combat has shape and breaks decisively; (b) **decks-per-faction** so scenarios feel
   distinct; (c) the **DEMOBILIZATION** mode as the signature — a strategy game about the
   entropy of a war that never came.
3. **Underground = logistics/flanking, air = overlay, surface = the fight.** Don't build
   three full battlefields; build one great one with two meaningful adjuncts.
4. **Scope discipline:** one vertical-slice scenario first. Tilemap + JSON + a tiny trigger
   system from day one so maps are data.
5. **Lean on my strengths (systems, data, code architecture, tuning) and own the gap:** art
   direction and *sound* need your ear and taste — point me at references and react to what I
   build; I can implement the systems and readable marker-visuals, but you drive the feel.

The pitch in one line: **"You spent everything to fortify Earth. The enemy left in two
seconds. Now demobilize a superhuman war-planet before it eats itself."** That's a game.
