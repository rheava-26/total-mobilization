# Levy contact damage uses the player's hit rules (2026-11-15, branch `bosshitbox`)

Finding from the Leviathan lab: Calamity hurts a player only inside circles on the Leviathan's mouth, body and tail, while levies took its whole 900 x 450 rectangle. This note is the reproduction, the fix and the before/after fights.

## 1. What happens (OBSERVED)

A levy (and a summoned unit, and a conjured creature) takes contact damage through its own code, since vanilla has no NPC-versus-NPC contact: `SoldierNPC.ContactDamage`, `SummonedUnitNPC.ContactDamage`, `ConjuredGlobalNPC.ContactDamage`. All three did the same: a distance broad phase, `NPCLoader.CanHitNPC(attacker, body)` (the NPC-versus-NPC hook), `NPC.GetMeleeCollisionData`, a bare rectangle intersection, one general immunity window.

`Player.Update_NPCCollision` (decompiled from tML 2026.10) runs different checks, in this order: the immunity slot vanilla assigns before any hook (Moon Lord's parts and the Empress use slot 1; the Empress is silent in states 0 and 10); `CombinedHooks.CanNPCHitPlayer` (`NPCLoader.CanHitPlayer`, where Calamity writes its circles, its state refusals and `cooldownSlot = Bosses`, then `PlayerLoader.CanBeHitByNPC`); `npcTypeNoAggro` and the Shield of Cthulhu dash exemption; `GetMeleeCollisionData`; the rectangle; then the slot's own cooldown (a general hit waits on `immune`, a slot hit on `hurtCooldowns[slot]`; slot 2 is the general one). The old levy path ran none of the `CanHitPlayer` hooks and had no slots. Calamity defines no `ModifyCollisionData` anywhere, so for Calamity the whole difference is `CanHitPlayer`.

**Grid of positions.** The `[HitParity]` self-check (shipworld) takes 58 hostile NPCs (vanilla bosses in both halves, 33 Calamity bosses in the Calamity half), 21 poses each (5 rotations x 2 facings, AI states 0 to 6, a scale, two animation frames), a grid of 20 x 42 body rectangles over and 220 px around each hitbox: 763,875 positions. The same grid is run on live poses inside 24 real boss-lab fights (every 20 ticks, the ten biggest hostile bodies): 13.7 to 15.6 million positions per arm. At every position three answers are compared: a real `Player` standing there (the reference), the new rule, the old rule. The reference itself is checked against the game's own `Player.Update_NPCCollision` run on a real player in a spare slot: 7,038 positions, 0 disagreements (hurt or not, and which cooldown slot).

| rule | levy hurt where a player is not | the reverse | other slot or damage multiplier |
|---|---|---|---|
| old, static poses | 12,497 | 0 | not counted |
| new, static poses | 0 | 0 | 0 |
| old, live fight poses | 41,727 | 0 | 9,569 (Calamitas Clone's Cataclysm and Catastrophe use slot Bosses) |
| new, live fight poses | 0 | 0 | 0 |

Where the old rule over-hurt in the static poses (of 13,125 positions per NPC): Leviathan 1,610, Astrum Aureus 2,406, Apollo 1,512, Thanatos 1,176, Profaned Guardian 651, Desert Scourge head, body and tail 630 each, Devourer of Gods head 630 and body 336, Hive Mind 576, Ravager 504, Crabulon 402, Supreme Calamitas 252, Plaguebringer 240, Signus 189, Ceaseless Void 63, Cryogen 60. Some of those bosses hurt a player nowhere in the poses tried (Desert Scourge, Devourer, Thanatos, Apollo, Supreme Calamitas: Calamity refuses the touch in those AI states), so the levy was hurt where the boss does not touch at all. No mismatch for the vanilla bosses (Destroyer, Plantera, Fishron, Queen Slime, Empress, Moon Lord, Golem, Prime, Twins, WoF, Deerclops, Eye, King Slime, Skeletron) nor for Anahita, Aquatic Scourge, Calamitas Clone, Providence, Yharon, Polterghast, Old Duke in these poses. Live, the old rule's levy-only positions came from Desert Scourge (26,929), Ice Shield (9,090), the Leviathan (4,370) and Aquatic Scourge (1,338); per NPC in `data/live_parity.md`.

## 2. The fix

`Common/Util/HostileContact.cs` is the one place that decides where an enemy's touch hurts a body. `PlayerRules` runs the sequence above with a Player standing in for the body (the levy's own gear dummy; a neutral stand-in for summoned and conjured bodies) moved to the body's rectangle: slot defaults, `NPCLoader.CanHitPlayer` (it may change the slot), `npcTypeNoAggro` and dash, `GetMeleeCollisionData`, intersection. It runs only after the old distance broad phase (a rectangle already near). The levy's hit then waits on the right window: `SoldierNPC.SlotImmune[5]` for a slot, `ImmuneTime` for the general one. All-types windows (dash burst, Ark parry, Hallowed dodge; `Player.SetImmuneTimeForAllTypes` in vanilla) close every slot through `GrantAllTypeImmunity`, so those toys protect as much as before. A hook that throws falls back to the old rectangle (one log line). `LEVY_CONTACTPARITY=0` restores the old rule, `LEVY_CONTACTSLOTS=0` folds slots into the single window.

Not run: `PlayerLoader.CanBeHitByNPC` (a ModPlayer's own veto reads real-player state; Calamity defines none). Summoned and conjured bodies keep one window for every slot. Nothing about the army is capped or nerfed: wherever a player would be hurt the levy is hurt; what changed is the hitbox and the slot windows (a slot hit now lands inside a general window, as for a player).

## 3. Before and after, fights

Same build, same 30-levy armies and arena as `../leviathan/RESULTS.md` (ocean arena `a3@ocean`; kit `on` = post-Plantera, `mid` = one tier below; Expert+Rev and Master+Death), `LEVY_CONTACTPARITY=0` = before. "Not a player" is the contact damage a levy took where the player rules say a player would not be hit (measured on every landed hit by the harness, so it is a real count of fights, not of positions).

Seeds 1 to 3 (Leviathan both kits, 6 fights per cell; the regression bosses on-tier: Duke Fishron, The Destroyer, Desert Scourge, Aquatic Scourge, Calamitas Clone):

| boss | difficulty | arm | wins | median kill s (wins) | levy deaths mean | contact dmg | not-a-player dmg | share % |
|---|---|---|---|---|---|---|---|---|
| aqua | Master+Death | after | 3/3 | 136 | 1.3 | 5662 | 0 | 0 |
| aqua | Master+Death | before | 3/3 | 106 | 1.0 | 9047 | 1810 | 20 |
| aqua | Expert+Rev | after | 3/3 | 84 | 0.0 | 0 | 0 | - |
| aqua | Expert+Rev | before | 3/3 | 88 | 0.3 | 5117 | 1053 | 21 |
| cal | Master+Death | after | 3/3 | 103 | 3.0 | 7654 | 0 | 0 |
| cal | Master+Death | before | 3/3 | 124 | 5.0 | 11174 | 0 | 0 |
| cal | Expert+Rev | after | 3/3 | 83 | 0.0 | 2591 | 0 | 0 |
| cal | Expert+Rev | before | 3/3 | 77 | 0.0 | 3102 | 0 | 0 |
| destroyer | Master+Death | after | 0/3 | - | 30.0 | 37007 | 0 | 0 |
| destroyer | Master+Death | before | 0/3 | - | 30.0 | 34074 | 0 | 0 |
| destroyer | Expert+Rev | after | 3/3 | 39 | 10.0 | 20417 | 0 | 0 |
| destroyer | Expert+Rev | before | 3/3 | 36 | 3.3 | 14299 | 0 | 0 |
| ds | Master+Death | after | 3/3 | 13 | 0.0 | 1886 | 0 | 0 |
| ds | Master+Death | before | 3/3 | 19 | 0.0 | 2629 | 836 | 32 |
| ds | Expert+Rev | after | 3/3 | 14 | 0.0 | 936 | 0 | 0 |
| ds | Expert+Rev | before | 3/3 | 12 | 0.0 | 1496 | 564 | 38 |
| fish | Master+Death | after | 0/3 | - | 30.0 | 32275 | 0 | 0 |
| fish | Master+Death | before | 0/3 | - | 30.0 | 33277 | 0 | 0 |
| fish | Expert+Rev | after | 1/3 | 72 | 28.0 | 44295 | 0 | 0 |
| fish | Expert+Rev | before | 0/3 | - | 30.0 | 41638 | 0 | 0 |
| levi | Master+Death | after | 1/6 | 136 | 26.7 | 44920 | 0 | 0 |
| levi | Master+Death | before | 1/6 | 157 | 27.8 | 65081 | 31186 | 48 |
| levi | Expert+Rev | after | 6/6 | 68 | 0.0 | 6934 | 0 | 0 |
| levi | Expert+Rev | before | 6/6 | 79 | 4.3 | 33051 | 16782 | 51 |

Seeds 4 to 12 added where the 3-seed numbers moved (Leviathan on-tier, The Destroyer, Duke Fishron; 9 seeds per cell):

| boss | difficulty | arm | wins | median kill s (wins) | levy deaths mean | contact dmg | not-a-player dmg | share % |
|---|---|---|---|---|---|---|---|---|
| destroyer | Master+Death | after | 2/9 | 49 | 28.4 | 97862 | 0 | 0 |
| destroyer | Master+Death | before | 1/9 | 59 | 28.8 | 101271 | 0 | 0 |
| destroyer | Expert+Rev | after | 9/9 | 33 | 3.9 | 42448 | 0 | 0 |
| destroyer | Expert+Rev | before | 9/9 | 35 | 6.7 | 47677 | 0 | 0 |
| fish | Master+Death | after | 0/9 | - | 30.0 | 97540 | 0 | 0 |
| fish | Master+Death | before | 0/9 | - | 30.0 | 97431 | 0 | 0 |
| fish | Expert+Rev | after | 0/9 | - | 30.0 | 124729 | 0 | 0 |
| fish | Expert+Rev | before | 0/9 | - | 30.0 | 120660 | 0 | 0 |
| levi | Master+Death | after | 2/9 | 83 | 24.1 | 87586 | 0 | 0 |
| levi | Master+Death | before | 2/9 | 77 | 24.6 | 100573 | 48185 | 48 |
| levi | Expert+Rev | after | 9/9 | 56 | 0.0 | 15951 | 0 | 0 |
| levi | Expert+Rev | before | 9/9 | 62 | 1.7 | 35690 | 18186 | 51 |

Reading it. Between 48 and 51 percent of the contact damage the Leviathan did to levies landed where a player is not hurt (Desert Scourge 32 to 38 percent, Aquatic Scourge about 20 percent); after the change that share is 0. Expert+Rev against the Leviathan was already won in every fight; levy deaths go from 4.3 to 0.0 per fight (seeds 1 to 3) and 1.7 to 0.0 (nine more seeds), contact damage taken falls by about half. Master+Death is still lost (1/6 then 1/6, and 2/9 then 2/9 wins): the dash itself, Anahita and the adds do that, and this change does not touch them. Duke Fishron (0 percent of contact damage in safe spots, vanilla rules identical) and The Destroyer move only within the sim's noise: Expert+Rev Destroyer deaths read 3.3 against 10.0 in seeds 1 to 3 and 6.7 against 3.9 over nine more. The aqua rows read 0 contact damage in the Expert+Rev after fight only because its three fights ended before anything touched a levy.
Per-fight rows: `data/fights_3seeds.md`, `data/fights_seeds4-12.md`. Live-pose matrix per NPC: `data/live_parity.md`. Scripts: `scripts/summ.py`, `scripts/parity.py` (fights generated with `../leviathan/scripts/mklevi.py`).

## 4. Recorder self-checks

`[FightRecAuto] ... a recording copied while still open reads back its flushed lines` slept 250 ms and hoped the writer thread had run; it now asks the writer to flush (`RecWriter.Flush`, which the game never calls) and checks that call returned. `[ClientProf] ... still ends cleanly with prof events` needed three 50 ms profiler windows of real time out of a 500-tick loop with one-millisecond sleeps; the profiler's window clock can now be set by the check (`ClientProf.TestClock`, null in the game), which the fight loop advances by the tick, so the windows and the "lag stretch" are a property of the ticks stepped, not of the machine's load. The real sleeps are gone. No gameplay path changed.

## 5. Ship run (private installs and ports, `tools/ship/shipboth.sh`)

Vanilla boot exc=0 (known summon-staff pick failure only); Calamity boot exc=0 failN=0 compat=1, `[ChargeLane] self-check pass=22 fail=0`. shipworld, vanilla / Calamity: MechWorld 74/0 and 84/0, SummonWorld 9/0 and 32/0, IdentityWorld 3/0 and 3/0, OfficerWorld 49/0 and 49/0, WhipReal 110/0 and 108/0, ShopLag 5/0 and 5/0, TileSweep 25/0 and 25/0, **HitParity 32/0 and 68/0** (new); `[ClientProf] self-check pass=29 fail=0` and `[FightRecAuto] world self-check pass=43 fail=0` in both halves; 0 exceptions. Three ship runs on this build line passed the two recorder checks (the flaky ones failed 3 of the earlier runs; three passes is not proof of the cure, the cure is that neither reads wall-clock time now).

## 6. Not verified

- Real client; the commander dodging; no water in the arena. The levy's real `Player.Hurt` (mod hurt hooks) is unchanged.
- `PlayerLoader.CanBeHitByNPC` for levies (see above). Third-party mods' `CanHitPlayer` that read real-player-only state: a throw is caught and falls back to the rectangle (none seen in vanilla or Calamity).
- Summoned units and conjured creatures use the new hit rules with one immunity window; no lab fight of their own.
- Master+Death against the Leviathan is not won by this change.
- Thorium bosses were not in the matrix.
