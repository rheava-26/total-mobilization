# Levy — playtest checklist

Work through these and tell me what misbehaves. For each, I've written **do X → expect Y**, so a report like
"enlisted a levy, no body walked out" or "boss HP didn't scale" points me straight at the module. Nothing here has
been played yet; the build is verified by compilation, source review and a headless server load only. Test on a fresh
**Classic** world first, then repeat the boss/fear items on **Expert**. Check `tModLoader-Logs/client.log` for any line
starting with `Levy:` — those are my own warnings.

Legend: [ ] to tick. Priority tags: (M0) = the core gate, do first. (MP) = multiplayer, not a v1 goal.

## 1. Crafting and setup (M0)
- [ ] Craft a Field Manual (5 wood at a Work Bench), right-click it → the full how-to prints to chat; the item is kept.
- [ ] Craft Barracks (30 wood, 10 iron/lead, any banner), Armory (20 iron/lead, any chest), War Horn (5 wood, 1 iron/lead),
      Patrol Banner (2 wood, 1 silk). Recipe groups show "Any Banner" / "Any Chest".
- [ ] Place the Barracks on a 3-wide floor → first right-click shows the one-time tip, opens the panel; smart cursor skips it;
      hovering shows its icon; mining it drops exactly one Barracks (no duplicate).
- [ ] Place the Armory (2x3) within 20 tiles; mine it with items inside → every stored item spills and one Armory drops.
- [ ] Place a Patrol Banner → it hangs from a ceiling, a wall side/bottom, or under a platform; it cannot be free-standing.

## 2. Settlement, housing, manpower
- [ ] House 2+ town NPCs within 120 tiles horizontally / 70 vertically of the Barracks → the panel's manpower cap = 3 per NPC.
- [ ] Move a house out of range → cap drops within ~1 s and the pool clamps down to it.
- [ ] Two Barracks ~100 tiles apart with an NPC between → the NPC counts for the nearer Barracks only.
- [ ] Confirm soldiers need NO houses: enlist/muster with no spare housing → bodies still spawn (they don't take NPC slots).
- [ ] Sleep to dawn → manpower regenerates (+1 per 2 town NPCs, rounded up).

## 3. Armory issue logic
- [ ] Put a Musket in issue weapon slot 1 and a better gun in bulk storage → the issue-slot Musket is issued first.
- [ ] Put a gun with no matching ammo anywhere and a bow with arrows in storage → the bow (usable) is issued, not the dry gun.
- [ ] Put a sword in issue slot 6 → it's taken as the sidearm; a melee primary gets no sidearm.
- [ ] Put vanity armor / a fishing pole / a pickaxe in the Armory → never issued.
- [ ] "Deposit junk" button → moves non-favourited weapons/armor/accessories/ammo from your inventory (not hotbar, not coins,
      not vanity); "Loot all" empties storage into your inventory.

## 4. Enlisting, wages, economy (M0)
- [ ] Deposit coins in the Barracks; Enlist Levy (T1 fee 50 silver) → a named "Pvt." walks out armed from the Armory;
      chat "<name> has enlisted at <Town>"; treasury down by the fee; CP meter shows 1/4 used.
- [ ] Try to enlist with no money → "The treasury cannot cover that."; with CP exhausted → "Not enough command points.";
      Man-at-Arms before a mech boss → "That grade is not unlocked yet."
- [ ] Sleep to dawn → "Paid the garrison: 10 silver." and the treasury drops.
- [ ] Empty the treasury, pass two dawns → "could not cover the garrison", then a 25% desertion roll per dawn
      ("<name> has deserted, taking <weapon> and no regrets.", the body walks off the map edge and vanishes).
- [ ] Deposit/withdraw +1 / +10 / all coins from the panel → amounts move correctly, buttons grey when they can't be afforded.

## 5. The soldier body and rendering
- [ ] A soldier renders with the player look: it wears the armor you issued and holds the weapon; walk/jump/idle frames animate.
- [ ] Give it a dye → the dye shows. Give it wings → wings appear and animate.
- [ ] Nameplate shows name, grade chevrons (^), an HP bar; it hides when more than 20 soldiers are on screen (config toggle).
- [ ] No placeholder "blank" sprite appears (if it does, the log names the failing draw layer).

## 6. Weapon families (M2)
- [ ] Musket + Musket Balls: fires at full rate with lead; pouch drops 1 per shot; at 0 → "out of ammo" bark + sidearm switch.
- [ ] Shotgun / Boomstick: a fan of pellets. Clockwork Assault Rifle: bursts then a pause.
- [ ] Wooden Bow + arrows at 20+ tiles: arrows arc and land on target; Jester's Arrows fly flat.
- [ ] A magic staff (e.g. Amethyst): fires until mana runs out, swaps to sidearm, swaps back after regen.
- [ ] Broadsword: hits in front, never behind, one hit per swing; Enchanted Sword also launches its beam.
- [ ] Spear: a ~5-tile thrust that extends and retracts; Shortsword: a ~3-tile stab; the item sprite draws along the thrust.
- [ ] Hand a soldier the wrong ammo (gel, coins) → the "Hand ammo" option does not appear / is refused.
- [ ] Summoner soldiers run the REAL vanilla minions (owner = the soldier): Pygmy Staff / Pirate Staff minions WALK and jump on the ground behind the soldier (they do not fly);
      Hornet / Imp / Optic staves fly and shoot; the Desert Tiger Staff shows gems over the soldier's head and, after a moment, a tiger that follows it and pounces.
- [ ] Two summoner soldiers near each other: each flock follows and spaces out around its OWN soldier; the commander's own minion slots and summon damage are untouched.
- [ ] A summoner soldier engaged with an enemy: its minions attack that enemy (and the commander's Focus Attack target); Hold Fire keeps them beside the soldier dealing no damage.
- [ ] Kill the soldier, or swap its staff: its minions vanish. Stardust Dragon Staff and modded staves still field the old stand-in minion.
- [ ] After a long summoner fight, press the inventory / use items normally → the commander's own player is unaffected (nothing left swapped); no Levy errors in the log.

## 7. Targeting both ways (M3)
- [ ] Night zombies prefer whichever of you or a soldier is closer; wearing aggro gear (Flesh Knuckles) pulls them back to you.
- [ ] Kill the targeted soldier mid-chase → the enemy instantly picks a player, never drifts to the world's top-left corner.
- [ ] Soldiers never shoot the Old Man, bound/rescue NPCs, Lunatic Devotees, the Mysterious Tablet, a disguised Mimic, the
      Eternia Crystal, critters or dummies; they flee the Dungeon Guardian and never shoot it.
- [ ] A disguised chest Mimic → soldiers ignore it until it wakes, then they fight it.
- [ ] 50 soldier kills of one enemy → its banner drops to you with a "defeated by <you>" line; the Bestiary counts the kills.
- [ ] Queen Bee's bees chase soldiers; Eater of Souls / Hornets / Meteor Heads ignore them (known gap, flying AI).

## 8. Combat effectiveness (report feel, not just pass/fail)
- [ ] One Levy vs a zombie at night → kills it reliably but misses noticeably (that's the ±6° aim by design).
- [ ] 4-6 Levies vs a Goblin Army → they hold a line, some die, loot drops to you. Note if they feel too strong or too weak.
- [ ] Melee Levies vs a crowd → they close, swing, step back; note if any get stuck inside an enemy swinging at air.
- [ ] Note the overall feel: amateurish-but-effective is the target; aimbot-precise or useless are both bugs.

## 9. Movement, pathfinding, travel (M6)
- [ ] Followers queue ~1.2 tiles apart behind you; start moving past ~6 tiles, stop within ~4.
- [ ] 2-3 tile wall → they jump it; 4-tile wall without gear → they path around within ~1 s or teleport in after 6 s stuck.
- [ ] Frog Leg / Cloud in a Bottle given to a soldier → higher jump / a second mid-air jump.
- [ ] A door → a follower opens it and closes it 3 tiles past; standing in it, it keeps retrying and gives up.
- [ ] A lava pool between you and a soldier → it refuses to walk in, goes around, or teleports in after 6 s.
- [ ] Pylon / Magic Mirror / Recall / wired teleporter / death+respawn → following Regulars appear beside you (never in lava,
      water, or through a wall); Hold / Return / stood-down / routed soldiers stay put.
- [ ] Walk into a soldier → it slides aside and never pushes you.

## 10. Orders and UI (M7)
- [ ] Hotkeys: Y follow, U hold, I return, O hold fire, P mobilize, K rally, N army overview — each does what it says.
- [ ] War Horn right-click (or the soldier "Orders" chat button) → the radial opens at the cursor; closes on click/Esc/right-click.
- [ ] Hold here → a soldier won't wander more than ~15 tiles from its hold spot even while kiting.
- [ ] Hold fire → soldiers stop shooting instantly and stop hauling.
- [ ] Assign patrol → place two Patrol Banners, assign a Regular → it paces between them, pausing ~2 s at each end.
- [ ] N → army strip bottom-left with heads + HP; M (fullscreen map) → gold dots for Regulars, blue for militia, hover = name.
- [ ] Promote / Discharge / Rebase buttons in the roster do what they say; promote costs the fee difference and keeps the name.

## 11. Boss rule and fear (M4) — do on Expert too
- [ ] Eye of Cthulhu with 3 Levies + 1 Man-at-Arms engaging → its max HP reads base x1.12; more hits add nothing.
- [ ] Park a Levy within 6 tiles of a boss for ~1 s → every 3rd boss projectile curves at it for 3 s ("Bosses punish soldiers").
- [ ] Eye of Cthulhu with a spite target → its charge aims at the soldier, then it returns to hovering over you; Skeletron's
      spin homes on the soldier for the spin; Fishron dashes at it.
- [ ] 10 soldiers at Skeletron → each barks on first sight; fear rises; at 50 they go Shaken (orange glow, worse aim, 60% damage).
- [ ] Hit a Shaken soldier → it may rout and run home; it rallies after 20 s out of the boss's range at fear 25.
- [ ] Die as commander mid-fight → everyone in view of the boss routs. Kill the boss → fear clears, broken soldiers return,
      nearby ones bark VICTORY.
- [ ] Twins: kill Retinazer while Spazmatism lives → fear does NOT reset and VICTORY does not fire until the second dies.
- [ ] Rally (K) with calm soldiers → their fear does not go up; with Shaken ones → they steady.
- [ ] Place an Eye of Cthulhu trophy within 40 tiles of the Barracks → its soldiers' fear from EoC drops (terror 3 → 1).

## 12. Grades, crystals, veterancy (M5)
- [ ] Feed Life Crystals → a Levy caps at 5 (200 HP), Man-at-Arms at 15 (400), Champion at 20 + Life Fruit (500); fruit only
      works on Champions; Mana Crystals raise a magic soldier's mana to 200.
- [ ] After any mechanical boss → Man-at-Arms unlocks; after Golem → Champion. Promote one and check slots/HP/wage change.
- [ ] A Regular that survives 3 main-boss fights → "Sgt." rename + "<name> is now a veteran." (max 25% of your roster).

## 13. Militia and invasions (M1)
- [ ] War Horn left-click near a town → purple "The militia of <Town> has been called up!"; bodies stream out every 15-30 ticks
      until the pool is spent; they patrol ±60 tiles; treasury drops L/5 per head (still musters when broke).
- [ ] Lower RenderCap to 5 with manpower 10 → the 6th walks out ~2 s after one dies. Manpower 0 → "has no one left to send." once.
- [ ] Stand down (horn again or event end) → bodies walk home and vanish, gear returns to the Armory; a stuck one vanishes after 30 s.
- [ ] Invasion with auto-muster on at two towns → BOTH towns turn out and each defends its own ground (militia don't march to you).
- [ ] Recruiting drive (buy manpower) and Hazard pay (one day's wage, before a boss) → once per day each; correct chat lines.
- [ ] Save mid-Blood-Moon and reload → the mobilized town re-streams fresh militia.

## 14. Dialogue (M8)
- [ ] Right-click a Regular at night, in rain, in each biome, during events, after payday → lines vary and fit; repeats rare.
- [ ] Boss fight → a squad shows "That's a boss..." and the boss-specific line, at most one pair per 4 s (not a wall of text).
- [ ] Edit `Dialogue/en-US.txt` (add a `TRIGGER | Temperament | line` row) and reload → your line appears; bad rows are skipped.

## 15. Flavor (M10)
- [ ] Event books drop from their events (set `EventBookDropDenominator` to 1 to test): Conscription from a Blood Moon,
      Drill from the Goblin Army, Plunder from pirates, Fortification from the Old One's Army, Discipline from an eclipse.
- [ ] Use a book → "<book> has been studied. <effect>"; a second copy → "Already studied." and it's kept.
- [ ] A Fortification book → soldiers within 60 tiles of their Barracks gain +4 defense.
- [ ] The Nurse near wounded soldiers → heals them at her price from the Barracks treasury (config toggle).
- [ ] Hazard pay before a boss → check that militia AND regulars take less fear that fight (x0.75).

## 16. Death and difficulty
- [ ] Softcore character: a soldier dies → half its ammo pouch drops, gear kept, respawns after 60 s if manpower remains.
- [ ] Mediumcore: all its gear drops where it fell. Hardcore: all drops, the record moves to a memorial, a tombstone spawns
      (config Vanilla = Hardcore only / Always / Never).
- [ ] Out of manpower → dead soldiers stay dead (greyed in the roster) until dawn regen.

## 17. Save/load and multi-settlement
- [ ] Build two Barracks + two Armories in two towns; enlist Regulars at each; stock each Armory differently →
      each town's soldiers arm from its own Armory only; treasuries are separate.
- [ ] Re-base a Regular from town A to town B via the roster → it re-homes and respawns there.
- [ ] Save and reload the world → every soldier's home Barracks is still correct (I re-resolve it from tile position),
      wages/treasury/manpower persist, militia are gone (re-muster on event).
- [ ] Remove the mod from the world, reload, re-add it → the world still loads; tiles become plain; roster drops cleanly.

## 18. Performance
- [ ] Field 40+ soldiers in one fight → watch the frame rate; note any stutter when many think/draw at once.
- [ ] A long session with lots of enlist/die/respawn churn → no memory growth or slowdown over ~30 minutes.

## 19. Config
- [ ] Toggle each config option (Mod Configuration → Levy) and confirm it takes effect: EnemiesTargetSoldiers,
      BossesPunishSoldiers, Nameplates, Tombstones, MilitiaDeathMessages, RegularDeathMessages, NurseHealsSoldiers,
      SoldiersTriggerPressurePlates, RenderCap, and the three multipliers (fear, soldier damage, wage).

## Known gaps (don't report these as bugs)
- Multiplayer: client roster rows can lag the live body; "used today" markers don't show on clients; some packets trust the
  client (not a v1 concern). Eater of Souls / Hornets / Meteor Heads (flying AI 5) ignore soldiers. A headless server runs
  the mod but can't drive gameplay with no client connected.
