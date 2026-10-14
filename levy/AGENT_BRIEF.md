# Agent brief (read with PLAN.md before writing a line)

You are implementing ONE module of **Levy**, a tModLoader 1.4.4 mod (net8.0, C# 12). Your working directory is a git
worktree of the repository; the mod source is at `./levy`. `./levy/PLAN.md` is the full design + the verified vanilla facts;
your task prompt names your module, your OWNED FILES and the spec sections that apply.

## References on this machine (grep them; never invent an API)
* Decompiled Terraria + tModLoader 1.4.4.9 (v2026.08.3.0): `/home/user/tml-src/Terraria/` — `NPC.cs`, `Player.cs`, `Projectile.cs`,
  `Main.cs`, `Collision.cs`, `ID/*.cs` (NPCID, ItemID, ProjectileID, TileID, AmmoID, NPCAIStyleID, ProjAIStyleID…), `GameContent/**`,
  `ModLoader/**` (the loader internals), `Localization/**`.
* tModLoader API source with doc comments: `/home/user/tmodloader/tml-1.4.4/patches/tModLoader/Terraria/ModLoader/*.cs`
  (ModNPC.cs, GlobalNPC.cs, ModTile.cs, ModTileEntity.cs, ModItem.cs, ModProjectile.cs, ModSystem.cs, ModPlayer.cs, UI/*, IO/*…).
* ExampleMod 1.4.4: `/home/user/tmodloader/tml-1.4.4/ExampleMod` (tiles, tile entities, UI, emotes, keybinds, packets, hjson).
* API docs: `/home/user/tml-bin/tml/tModLoader.xml`. Hook classes `Terraria.On_NPC`, `On_Player`, `On_Projectile`… are in
  `/home/user/tml-bin/tml/Libraries/TerrariaHooks/0.0.0.0/TerrariaHooks.dll` (referenced by the compile script; list members with
  `strings` or just compile a test).
* TerraGuardians (companions drawn with the player renderer, player-weapon reimplementation): `/home/user/refmods/TerraGuardians`.

## Rules
1. **Compile**: `/home/user/tools/compile.sh "$(pwd)/levy"` must print `COMPILE OK` with zero errors before you finish. CS1701
   warnings are noise. Compile often.
2. **Verify every member** you use against the sources above. If a method does not exist, find the real one. Report surprises.
3. **Ownership**: create/replace only your OWNED FILES. You may add `./levy/Common/LevyConstants.<Area>.cs`
   (`public static partial class LevyConstants` in namespace `Levy.Common`) for new numbers and
   `./levy/Localization/en-US_Mods.Levy.<Area>.hjson` (keys inside are relative to `Mods.Levy.<Area>`; no `Mods: { Levy: {` wrapper)
   for new strings. Never edit other agents' files or the shared contracts; if a contract lacks something, write
   `// CONTRACT-REQUEST: ...` in your file with a local workaround and report it. Keep every existing public member and
   signature of the contract classes you replace; you may add members. Multiple `GlobalNPC` / `GlobalProjectile` /
   `ModPlayer` / `ModSystem` classes are fine: put your hooks in your own classes in your own files.
4. **Partial class rule**: `SoldierNPC` is partial. Your `SoldierNPC.<Part>.cs` implements the `partial void` methods named in your
   prompt and may add private fields/helpers and `public override` methods that `SoldierNPC.cs` does NOT already define
   (it defines: SetStaticDefaults, SetDefaults, SetBestiary, AI, ModifyIncomingHit, CheckDead, PreKill, CheckActive,
   CanFallThroughPlatforms, CanHitPlayer, CanBeHitByItem, CanBeHitByProjectile, DrawHealthBar, NeedSaving, UsesPartyHat,
   PreDraw, PostDraw, CanChat, GetChat, SetChatButtons, OnChatButtonClicked, SendExtraAI, ReceiveExtraAI).
5. **Server-authoritative**: logic runs where `Main.netMode != NetmodeID.MultiplayerClient`; clients draw and send packets via
   `Levy.Common.Net.LevyPackets` (payload layouts are documented in that file — follow them exactly). Never use
   `Main.LocalPlayer` in server logic; use the commander index on the record/body.
6. **Numbers** live in `LevyConstants` (existing or your partial file). **Text** lives in hjson or `Dialogue/en-US.txt`, in the
   player-facing style: 1–4 short declarative sentences, no history, no comparisons, no ALL-CAPS.
7. **No scope widening**; no TODO stubs left in your files; production quality. Guard every `Main.npc[i]`/`Main.player[i]`
   index, every missing record/body/commander/home/armory, and avoid per-tick allocations in AI.
8. Before finishing, re-read your diff adversarially: what crashes on a server? what happens when the barracks is destroyed
   mid-fight, the commander logs off, the armory is empty, the NPC array is full, the item is modded?
9. Finish with: `git add -A levy && git -c user.name=Claude -c user.email=noreply@anthropic.com commit -q -m "Levy: <module>"`
   in your worktree, then return the structured result: commit sha (`git rev-parse HEAD`), files written, partial methods /
   contract members implemented, CONTRACT-REQUESTs, surprising vanilla findings, hand-test notes for TESTING.md, known gaps.
