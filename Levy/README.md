# Levy (v0.1) — a tModLoader mod

Your junk becomes a garrison. Throw weapons, armor, ammo and life crystals you no longer use into an **Armory** and the
town arms a militia with them. Pay gold to keep **Regulars** who follow you anywhere. Blow the **War Horn** during a
Blood Moon or an invasion and the whole settlement turns out to hold the walls. They are morons. They die like players.
They cost money.

Built for **tModLoader 1.4.4.9 (Steam `stable`, v2026.08.3.0)**, .NET 8. Content only: no worldgen, works in existing worlds,
removable (roster data is dropped, tiles become plain tiles).

## Install (source, the normal way for a work in progress)
1. Open tModLoader once so `ModSources` exists: `Documents/My Games/Terraria/tModLoader/ModSources/` (Linux:
   `~/.local/share/Terraria/tModLoader/ModSources/`).
2. Copy this whole `Levy/` folder there (the folder name must stay `Levy`: tModLoader names the mod after it).
3. In tModLoader: **Workshop → Develop Mods → Levy → Build + Reload**. Enable the mod, play.

## Install (prebuilt)
If a `Levy.tmod` is shipped next to this folder, drop it into `.../tModLoader/Mods/` and enable it in **Mods**.

## First steps in game
* Work Bench: **Barracks** (30 wood, 10 iron/lead bars, 1 banner), **Armory** (20 iron/lead bars, 1 chest), **War Horn**
  (5 wood, 1 iron/lead bar), **Patrol Banner** (2 wood, 1 silk).
* Place the Barracks inside your town (town NPCs within 120×70 tiles belong to it) and the Armory within 20 tiles.
* Right-click the Armory: **Deposit junk**. Put the weapons you want issued first in the issue rows.
* Right-click the Barracks: deposit coins, **Enlist** a Levy. They walk out, arm themselves and follow you.
* Horn: left-click calls up / stands down the militia; right-click opens the orders radial. Hotkeys: Y follow, U hold,
  I return, O hold fire, P mobilize, K rally, N army overview.
* **New to it?** Craft a **Field Manual** at a Work Bench (5 wood) and right-click it — the full how-to prints to chat.
* You do **not** house the soldiers. House your ordinary town NPCs near the Barracks as usual; each one adds manpower, and the soldiers live at the Barracks.

## Design
`PLAN.md` is the architecture and build plan; the design spec (Build Spec v0.1) holds the numbers, all of which live in
`Common/LevyConstants*.cs`. Dialogue lives in `Dialogue/en-US.txt` (one line per entry, no code needed to add lines).
`TESTING.md` lists the hand-test gate for each milestone.
