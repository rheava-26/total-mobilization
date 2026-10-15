# levy-mod

This folder is the **Levy** Terraria mod and everything around it, kept separate from the Total Mobilization
game that shares this repository.

```
levy-mod/
  Levy/          the mod itself — this is the folder you install (must keep the name "Levy")
  Levy.tmod      a prebuilt copy of the mod (tModLoader 1.4.4.9, v2026.08.3.0)
  tools/         dev-only build/verify scripts (not needed to play)
```

## Install — the easy way (prebuilt)
You do **not** need to build anything; `Levy.tmod` is already built.

1. Install **tModLoader** (the 1.4.4 stable branch) from Steam and launch it once so its folders exist.
2. Find your tModLoader **Mods** folder:
   - Windows: `Documents\My Games\Terraria\tModLoader\Mods`
   - Linux: `~/.local/share/Terraria/tModLoader/Mods`
   - Steam Deck / Proton: `.../steamapps/compatdata/.../Terraria/tModLoader/Mods`
3. Copy **`Levy.tmod`** into that `Mods` folder.
4. Launch tModLoader → **Workshop → Mods** (or the main-menu **Mods** button) → enable **Levy** → **Reload**.
5. Start or load a world and play. Craft a **Field Manual** (5 wood at a Work Bench) and right-click it for the how-to.

If tModLoader says the mod is for a different version than yours, build from source instead (below).

## Install — from source (to tinker or if versions differ)
1. Open tModLoader once so `ModSources` exists next to `Mods`.
2. Copy the **`Levy/`** folder into `ModSources` (keep it named exactly `Levy` — tModLoader names the mod after the folder).
3. In tModLoader: **Workshop → Develop Mods → Levy → Build + Reload**.

## Docs
`Levy/README.md` (player guide), `Levy/PLAN.md` (architecture), `Levy/TESTING.md` and `Levy/PLAYTEST.md` (test lists).
