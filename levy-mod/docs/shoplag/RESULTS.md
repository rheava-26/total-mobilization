# Shop lag and the tile-breaker sweep (2.0.62 branch work)

## Shop lag: cause
Opening a shop costs 0.05 ms (median of 72-75 shops, 40 levies, officers, Calamity/Thorium/LuiAFK/Alchemist/Magic Storage loaded; worst 0.5 ms). The lag is the WORLD TICK while the shop window is up.
A levy in modded gear runs a gear frame every tick. IdentityGuard wraps every ModPlayer hook of every gear mod and, per hook, mirrored the commander's conversation into the dummy with
`Player.SetTalkNPC` (in and out). With a conversation open SetTalkNPC re-reads the shopping settings: 5-8 us per pair versus 0.004 us closed. 40 levies = about 1240 mirrors a tick.
Fix: `TalkMirror` writes the backing field (0.08 us). `IdentityGuard` calls it.

Bisect (same harness, 40 levies, 6 Thorium accessories dealt out, mean ms per world tick, conversation closed / open):
2.0.60 4.43 / 4.31; 2.0.61 5.58 / 17.94; 2.0.62 3.57 / 12.33 (another run 6.15 / 21.92); with the fix 3.68 / 3.86.
Harness: `Common/Testing/ShopLagCheck.cs` (LEVY_SHOPLAG=1 lab, ship world chain). Not reproducible headless: the drawn shop window.

## Tile sweep
`TileSweepCheck` fires every weapon with every round (13,948 pairs) and spawns every projectile type (5,019) in a block field; `WorldAlterTable.tsv` is the result.
Altering by itself: 43 items, 26 rounds, 17 pair rules, 113 projectile types. Cuttable decor only (grass, vines, cobweb) is kept.

Calamity + Thorium + LuiAFK + Alchemist + Magic Storage, 40 levies (shipworld, median tick): conversation closed 4.76 ms; open through SetTalkNPC 19.37 ms; open with the fix 4.94 ms.
Ship sweep is a vanilla sample (time-boxed); modded content is measured by the lab sweep, which writes the table.
Unverified: the drawn shop window, the Armory warning window, and other mods' items the table has not seen (judged from compiled code: 90% of altering projectiles caught, 0.8% of safe ones over-flagged).
