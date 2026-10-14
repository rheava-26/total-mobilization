# TESTING.md — hand-test gates (the mod was built and compiled headlessly; nothing below has been played yet)

Each milestone has a gate. Run them in order on a fresh Classic world unless noted. Report anything that does not match.

## M0 skeleton — "a levy with a Musket kills a zombie and gets paid"
1. Craft and place a Barracks and an Armory within 20 tiles, near at least two housed town NPCs.
2. Right-click the Armory, deposit a Musket, musket balls, some armor. Put the Musket in issue weapon slot 1 and the
   bullets in issue ammo slot 1.
3. Right-click the Barracks, deposit 1 gold, Enlist Levy. Expect: a named soldier walks out of the barracks wearing the
   armor and holding the Musket; the roster shows them; the treasury dropped by the muster fee (5 days of wage: T1 = 50 silver).
4. Walk away at night; a zombie approaches: the levy fires with lead at the weapon's full rate, misses sometimes (±6°),
   the zombie drops coins/loot and the Zombie banner progress counts.
5. Stand next to a zombie: it should chase the soldier if the soldier is closer than you (allowlisted fighter AI).
6. Let the levy die: chat shows a death line with their name; after 60 s they walk out of the barracks again (manpower −1).
7. Sleep to dawn: "Paid the garrison: 10 silver." and the treasury drops. Empty the treasury and pass two dawns: unpaid
   lines, then a 25 % desertion chance per dawn.
8. Pause menu → Mods → Levy config exists with sane defaults.

## M1 militia — "a Blood Moon defended by townsfolk, with the pool visibly draining"
(to be filled from the militia agent's notes)

## M2 weapon families — "a mixed squad of six fights a Goblin Army and every family visibly works"
(to be filled)

## M3 targeting — "zombies chase soldiers, soldier kills drop loot, nobody shoots the Old Man"
## M4 boss rule and fear — "Skeletron and Plantera fights feel like the intended curve on Expert"
## M5 grades — "four Champions in beetle armor and a mob of levies against Golem"
## M6 movement — "a squad follows you through the Dungeon without getting lost"
## M7 interface — "you can run the army for an hour without opening a chest"
## M8 dialogue — "a Blood Moon with chatter that makes you laugh and doesn't flood the chat"
## M10 flavor — event books, trophies, Nurse, Tax Collector, Guide hint
