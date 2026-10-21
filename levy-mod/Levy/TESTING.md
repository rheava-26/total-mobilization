# TESTING.md — hand-test gates

> For a full flat playtest checklist (every system, do-X-expect-Y, tickable), see **PLAYTEST.md**.

Nothing below has been played. The mod was built headlessly (Roslyn against the real tModLoader 1.4.4.9 assemblies, then
tModLoader's own `-build`, both clean) and reviewed from source against the decompiled game. The first in-game session is
the real test. Run the gates in order on a fresh **Classic** world; repeat the boss gates on **Expert**. When something does
not match, note what you saw: the mechanic changes, not the player.

Useful while testing: `tModLoader-Logs/client.log` (look for lines starting with `Levy:`), the config under Settings → Mod
Configuration → Levy, and `EventBookDropDenominator` in `Common/LevyConstants.cs` (set to 1 to test drops).

## M0 — "a levy with a Musket kills a zombie and gets paid"
1. Work Bench recipes exist: Barracks (30 wood, 10 iron/lead, any banner — a Red Banner or an enemy banner both count),
   Armory (20 iron/lead + any chest), Patrol Banner (2 wood + 1 silk), War Horn (5 wood + 1 iron/lead). The group tooltips
   read "Any Banner" / "Any Chest".
2. Place the Barracks on a 3-wide floor near at least two housed town NPCs. First right-click prints the one-time tip and
   opens the panel; hovering shows the item icon; smart cursor ignores it; mining drops exactly one Barracks.
3. Place the Armory within 20 tiles. Right-click: 120-slot grid plus issue rows. Put a Musket in weapon slot 1 and Musket
   Balls in ammo slot 1, some armor in an armor set. "Deposit junk" moves non-favourited weapons/armor/accessories/ammo from
   the main inventory (not the hotbar, coins or vanity). Mining the Armory spills every stored item and drops one Armory.
4. Barracks panel: deposit 1 gold. Enlist Levy (T1 fee = 50 silver). Expect a "Pvt. <name>" to walk out of the barracks
   wearing the armor and holding the Musket, a chat line "<name> has enlisted at <Town>", the treasury down by the fee and
   the CP meter showing 1 / 4 used. With no money: "The treasury cannot cover that."; with no CP: "Not enough command
   points."; Man-at-Arms before any mechanical boss: "That grade is not unlocked yet."
5. Night: a zombie approaching gets shot at the Musket's full rate with lead; Levy aim visibly misses sometimes (±6°).
   Pouch count drops by one per shot. The zombie's drops land and the Zombie banner progress counts.
6. Stand farther from the zombie than the soldier: the zombie chases the soldier (fighter AI). Wear aggro gear (Flesh
   Knuckles) and it comes back to you.
7. Let the soldier die: chat shows a death line with the name; half the pouch drops (Softcore); after 60 s, with manpower
   left, "<name> returns to duty" and it walks out again. Empty manpower: stays dead until dawn regen.
8. Sleep to dawn: "Paid the garrison: 10 silver." Empty the treasury and pass two dawns: "The treasury of <Town> could not
   cover the garrison.", then 25 % desertion rolls per dawn ("<name> has deserted, taking <weapon> and no regrets.", the
   body walks to the map edge and vanishes).
9. Config under Mod Configuration → Levy loads with sane defaults.

## M1 — militia and manpower: "a Blood Moon defended by townsfolk, with the pool visibly draining"
1. Panel shows Manpower = 3 × housed NPCs within 120×70 tiles (5 with A Guide to Conscription). Move a house out of range:
   the cap drops within a second and the pool clamps. Two barracks 100 tiles apart: an NPC between counts for the nearer.
2. War Horn left-click (or a Blood Moon / invasion with Auto-muster on): purple "The militia of <Town> has been called up!";
   bodies leave the muster point every 15–30 ticks until the count equals the pool; they wear issue gear, patrol ±60 tiles,
   and the treasury drops L/5 per head (it still musters when broke).
3. Lower RenderCap to 5 with manpower 10: kill one body, the sixth walks out two seconds later. Manpower 0: "<Town> has no
   one left to send." exactly once.
4. Stand down (horn again or event end): "The militia of <Town> stands down." and, in Count mode, "<Town> lost N militia.";
   bodies walk home and vanish at the barracks with their gear back in the armory; a stuck body vanishes after 30 s.
5. Destroy the Barracks while mobilized: orphans vanish within a second; regulars re-home to the nearest other barracks or
   become homeless (they follow you, no respawn).
6. Recruiting drive twice in a day → "already ran a recruiting drive today"; hazard pay charges one day's wage of every
   regular based there plus one levy wage per militia, once per day.
7. Hauling: kill mobs near base and step more than 8 tiles away: idle or following regulars pick up drops within 6 tiles
   (never hearts, stars or boss bags), carry up to five to the armory; coins land in the treasury (×1.5 with Plunder). No
   hauling while a hostile is within 25 tiles, under Hold fire, or when the armory is more than 120 tiles away.
8. Save mid-Blood Moon and reload: the mobilized barracks re-streams fresh militia (militia records are never saved).

## M2 — weapon families: "a mixed squad of six fights a Goblin Army and every family visibly works"
1. Musket + Musket Balls: fires every use time with lead. Boomstick: 3 pellets in a 20° fan. Clockwork Assault Rifle:
   3-round bursts then a pause.
2. Wooden Bow + Wooden Arrows at 20+ tiles: arrows arc and land on the target; Jester's Arrows fly flat.
3. Amethyst Staff: fires until mana runs out (20 base), switches to the sidearm, swaps back after regen.
4. Copper Broadsword: swings hit a zombie in front, never behind, one hit per swing; Enchanted Sword also launches its beam.
   A Titan Glove on the soldier enlarges the swing.
5. Spear: a 5-tile thrust that extends and retracts in about half a second, the item sprite drawn along it. Copper
   Shortsword: a 3-tile stab.
6. Eater of Worlds / Destroyer: shots go at the nearest segment.
7. Empty the pouch: with an Armory within 40 tiles the soldier walks to it and refills; with none and a sidearm issued it
   switches and barks once; with neither it stops engaging. Walking past an armory with under 20 % ammo tops up.
8. Tools and thrown consumables are never issued from the armory.
9. Hold fire (O): soldiers stop firing instantly and stop hauling.

## M3 — targeting: "zombies chase soldiers, soldier kills drop loot, nobody shoots the Old Man"
1. Night zombies prefer the nearer of you and a soldier; the enemy turns to face it; slimes re-target on their hop cadence;
   casters teleport to soldiers; a Bone Serpent locked on a soldier switches to a nearer player within a second.
2. Kill the targeted soldier mid-chase: the enemy instantly picks a player and never drifts toward the world's top-left.
3. Soldiers never shoot the Old Man, Lunatic Devotees or the Mysterious Tablet, bound rescue NPCs, a disguised Mimic, the
   Eternia Crystal, critters or target dummies; they run from the Dungeon Guardian and never shoot it.
4. Fifty soldier kills of one enemy: the banner drops to you with the "defeated by <you>" line; the Bestiary counts the kills.
5. Old One's Army: enemies switch to soldiers only within 200 px of them, otherwise the crystal. Queen Bee's bees chase
   soldiers. Eater of Souls, Hornets and Meteor Heads ignore soldiers (known gap: flying AI 5).
6. Hostile projectile hits on a soldier and contact hits on you raise that enemy to top priority for every soldier for 2 s.

## M4 — boss rule and fear: "Skeletron and Plantera feel like the intended curve on Expert"
1. HP scaling: Eye of Cthulhu with 3 Levies (1 CP each) and a Man-at-Arms (3 CP) engaging reads base × 1.12 max HP; further
   hits add nothing; set the cap to 4 % and confirm clamping. Twins: each twin is its own fight.
2. Spite: park a Levy within 6 tiles of Queen Bee for a second (or let one dump more than 1 % of her HP in 3 s): every third
   stinger curves at that soldier for 3 s after the last provocation. "Bosses punish soldiers" off: none.
3. Dashes: with a spite target, Eye of Cthulhu's charge aims at the soldier then returns to hovering over you; Skeletron's
   spin homes on the soldier for the spin only; Spazmatism's phase-2 charge; Duke Fishron on the hover→attack tick.
4. Fear: spawn 10 soldiers at Skeletron or Eye of Cthulhu: each barks on first sight; fear rises (terror 3 ≈ 3.6/s with you
   away, half with you within 10 tiles); at 50 they turn Shaken (orange glow, worse aim, 60 % damage). Hit a Shaken soldier:
   rout chance (fear − 50) / 50; a routed soldier runs home and rallies after 20 s out of the boss's range at fear 25.
5. Die as the commander mid-fight: everyone with the boss in view routs. Kill the boss: fear clears, broken soldiers return
   to orders, nearby soldiers bark VICTORY. Kill Retinazer while Spazmatism lives: nothing resets until the second dies.
6. Rally (K) with calm soldiers never raises fear; with Shaken/Routed ones they steady at 25 and bark. 60 s cooldown line.
7. Trophies: an Eye of Cthulhu trophy (or relic) within 40 tiles of the barracks lowers its terror 3 → 1 for soldiers based
   there; moving it 41+ tiles away restores it within 10 s.
8. Hold-order soldier: get it Shaken, let fear drop: it keeps its original post.

## M5 — grades: "four Champions in beetle armor and a mob of levies against Golem"
1. Man-at-Arms unlocks after any mechanical boss (3 CP, 3 slots, 400 HP cap), Champion after Golem (7 CP, 5 slots, 500 HP).
   Promote costs the fee difference and keeps the name and record.
2. Feed buttons appear while holding Life Crystals / Life Fruit / Mana Crystals: a Levy caps at 5 crystals (200 HP),
   Man-at-Arms at 15 (400), Champion at 20 + fruit (500); fruit only for Champions; mana crystals up to 200 mana.
3. Armor and accessories from the armory give their plain stats (defense, damage %, crit) through the game's own equipment
   code; a soldier wearing wings takes one slot. Set bonuses with special effects do nothing (v1).
4. Survive three main-boss fights with a regular: "Sgt." rename and "<name> is now a veteran." (max 25 % of the roster).

## M6 — movement: "a squad follows you through the Dungeon without getting lost"
1. Three followers queue 1.2 tiles apart behind you, start moving beyond about 6 tiles, stop within about 4.
2. 2- and 3-tile walls: jump. A 4-tile wall without gear: blocked, path around within a second if one exists, teleport to
   just off-screen after 6 s stuck. Frog Leg / Cloud in a Bottle: 6-tile reach / a mid-air jump. Wings: fly up to you on a
   tower, glide down when flight time runs out.
3. Doors: a follower opens a wooden door or tall gate and closes it 3 tiles later; standing in it keeps it retrying; it gives
   up at 6 tiles.
4. Lava between you and the soldier: it refuses, walks the long way, or teleports after 6 s. A ledge deeper than 9 tiles:
   refuses without gear, flies down with wings. Platforms: drops through when you are below.
5. Pylon, Magic Mirror, Recall, wired teleporter, death and respawn: following regulars appear beside you (never in lava or
   water, never through a wall). Hold / Return / stood-down / routed soldiers stay where they are.
6. Walk into a soldier: it slides aside and never pushes you. Pressure plates: off by default; toggle the config to let them
   trigger plates.
7. Walk 70+ tiles away off-screen: followers appear just off-screen behind you within 3 s, never while on your screen.

## M7 — interface: "run the army for an hour without opening a chest"
1. Barracks panel: deposit +1 / +10 / all, withdraw −1 / −10 (buttons grey when the inventory or treasury cannot cover it),
   manpower bar, CP meter, toggles, Mobilize / Stand down, Recruiting drive, Hazard pay, Enlist per grade with hover reasons,
   roster rows with HP, state, pay, Veteran tag, Promote / Discharge / Follow / Hold / Return / Assign patrol (needs two
   Patrol Banners within 60 tiles) / Rebase. Closes on Esc, on walking more than 10 tiles away, and when the tile is gone.
2. Armory panel: drag, shift-click and right-click in the grid; issue rows refuse non-matching items; "Deposit junk";
   "Loot all" empties storage only.
3. War Horn right-click or a soldier's "Orders" chat button opens the radial around the cursor; six wedges; closes on click,
   Esc or right-click. Hotkeys Y follow, U hold, I return, O hold fire, P mobilize (chat line when no barracks within 120
   tiles), K rally, N army strip.
4. Nameplates: name, gold chevrons per grade, HP bar, orange "!" growing with fear, red "!!" when routed; militia show name
   and HP only; everything hides when more than 20 soldiers are on screen or the config is off.
5. N: army strip bottom-left with heads, HP bars, dead soldiers greyed with a respawn countdown. M: gold dots for regulars,
   blue for militia, hover shows the name.

## M8 — dialogue: "a Blood Moon with chatter that makes you laugh and doesn't flood the chat"
1. Right-click a Regular at night, in rain, in each biome, during events and after payday: lines fit the situation and
   repeats are rare within two minutes. Name-drop lines only mention NPCs that are alive.
2. Combat: emote bubbles plus a small floating line per bark, at most one line per squad every 4 s and one emote per soldier
   every 8 s. A boss fight shows "That's a boss…" and the boss-specific line stacked, at most one pair per 4 s.
3. Deaths post one red chat line per Regular; militia deaths post a single count at stand down (Count mode).
4. Edit `Dialogue/en-US.txt` (one `TRIGGER | Temperament | line` per row) and reload: new lines appear; bad rows are logged
   by line number and skipped, never shown raw.

## M10 — flavor
1. Books: use one → "<book> has been studied. <effect>", consumed; a second copy → "Already studied." and kept. Drops (set
   the denominator to 1): Conscription from any Blood Moon enemy, Drill from the Goblin Army, Plunder from pirates,
   Fortification from the Old One's Army, Discipline from eclipse enemies; never from statues or outside the event. The
   Bestiary shows "Drops during …" on those enemies.
2. Fortification: a soldier within 60 tiles of its barracks gains +4 defense at the next stats refresh (≤ 1 s).
3. Nurse: a wounded soldier within 4 tiles of the Nurse is healed to full at her price from the nearest barracks treasury
   (green heal number, potion sound); with an empty treasury or the toggle off, nothing happens.
4. Guide: after holding a War Horn (or placing a Barracks), the Guide's chat shows the armory hint once.
5. Tax Collector housed in the settlement with "Tax Collector pays the treasury" on (single player): at dawn his collected
   tax moves into the treasury with a chat line.
6. Tombstones: config Vanilla → only a Hardcore commander's soldiers leave one; Always → every soldier death; Never.

## Multiplayer (not a v1 goal; the structure is server-authoritative)
1. Join a server with regulars: the roster panel is populated on join; enlist / promote / orders / deposits go through
   packets; withdraw spawns coins at the player; handing ammo updates the inventory slot.
2. Clicking armory slots on a client must not crash (uses the bank-item slot context); slot changes round-trip through the
   server and the tile entity re-sync.
3. Floating text appears for clients within about 200 tiles; emotes and chat-window lines work after payday.
4. Known gaps: Tax Collector routing is single player only; per-soldier orders from a client fall back to the whole squad;
   the one-time barracks tip is not persisted from clients; boss max HP is sent to clients by a mod packet.

## Hazards to watch (from the source reviews)
* Enemies chasing soldiers across lava will follow them in (vanilla AI).
* Worms switching segments while locked on a soldier.
* Old One's Army enemies choosing between the crystal and the line.
* Another mod throwing inside an enemy AI while the dummy player occupies slot 255: the mod restores the slot on the next
  tick; look for `Levy:` warnings in the log.
* A draw exception in another mod's player layer would make the body fall back to the placeholder sprite (logged once).

## Known gaps left in v1 (from the source review; multiplayer is not a v1 goal)
* Multiplayer client roster rows read from the periodic snapshot, not from the live body, so a row's HP/state can lag the
  real soldier by up to the snapshot interval.
* The Barracks panel's "used today" marker for Recruiting drive / Hazard pay does not appear on multiplayer clients (the day
  counter is server-side); the server still enforces once-per-day.
* The War Horn chooses mobilize vs stand down from the client's last-synced flag, so a horn pressed in the same instant as an
  auto-muster can briefly disagree until the next tile-entity sync.
* Deposit, feed-crystal and book-used packets trust the client's claim in multiplayer (treasury/flags are not re-validated
  against the server's inventory mirror). A cheating client could mint treasury; harmless in single player and co-op.

## Wage Mutiny (config `Wage mutiny`, off by default)
1. Turn it on, enlist a Regular, leave the treasury empty and let dawn pass: red chat line, "MUTINY! PAY US!" over the body, red tint and a
   red "MUTINOUS" nameplate, roster card shows "MUTINY: owed X". Any order (radial, hold fire, spread out, War Horn orders) is refused with a chat line.
2. The mutineer walks to a town NPC, takes up to two Levy wages (the NPC gets an anger emote; its shop prices rise 25% for a day and its mood
   report says why), or walks at you, hits you and takes coins from pockets, then Piggy Bank, Safe, Forge, Void Vault. Only coin stacks change.
3. Deposit enough to cover the group's back pay: all of them stand down at once and the chat says the mutiny is over. A soldier who took his whole
   claim by robbery stands down alone. Toggling the config off clears every mutiny.
4. Multiplayer: the robbed player's client removes its own coins (server sends MutinyRob); the server counts only coins its copy of the player shows.
