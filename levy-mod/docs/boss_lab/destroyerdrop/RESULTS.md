# destroyerdrop: Destroyer win-rate drop, and two exceptions in Levy code

Branch `destroyerdrop` (from `worktree-levy-stardust` fc48ec2). Lab: private copy of tML 2026.10 + Calamity, ShipWorld.wld, Destroyer Master+Death baseline army (byte-identical to hmbattery2, 34 levies), arena a3, every fight recorded. Tools in `tools/` (lab root `DD_LAB`).
Builds: pre = 79d68d9 (commit before mobslots), after = a102664 + the harness boots-slot hunk of 458539a, cur = 545509f (current + lab switch `LEVY_MOBSLOTS=0`, which puts wings and boots back in accessory slots).

## 1. Destroyer: there is no regression mechanism to fix

Same seed does NOT reproduce a fight: the same code (cur vs the recorded hmbattery2 NEW run) agreed on 18 of 40 seeds, boss-HP correlation -0.16. Fights are independent coin flips (timing-dependent), so 40 fights per arm resolve nothing under about 20 points.

| arm | fights | wins | win rate (95% CI) | boss life left (mean) | boss damage (mean) |
|---|---|---|---|---|---|
| pre (79d68d9), my runs + hmbattery2 `preslots` | 120 | 31 | 25.8% (19-34) | 13.8% | 152k |
| after mobslots (+ harness boots slot) | 40 | 6 | 15% (7-29) | 14.3% | 151k |
| cur, slots on (3 batches: 40, 40, 80) | 160 | 30 | 18.8% (13-26) | 16.5% | 145k |
| cur, slots OFF (`LEVY_MOBSLOTS=0`, same 3 batches) | 160 | 42 | 26.2% (20-34) | 15.3% | 149k |
| cur, boots removed, wings kept | 40 | 5 | 12.5% (5-26) | 19.2% | 136k |

Matched A/B (same build, same seeds, same time): slots on 30/160 vs slots off 42/160, Fisher p = 0.14; boss life left 16.5% vs 15.3%, p = 0.34. Batch by batch the same code gave 5/40, 5/40 and 20/80 (25%): batch-to-batch spread is as large as the claimed drop. Direction is the same in all three batches (on < off) but the effect, if any, is under about 7 points and not resolvable at this size.

Hypotheses:
- (a) boots apply speed now: rejected. Only 5 of 34 levies carry boots; the dummy's stats are identical with slots on and off for all 34 levies (defense, run speed, jump, wings, damage, crit, attack speed, mana: `STATDUMP`, 0 differences); boots removed gives the same 12.5%.
- (b) mobility-slot picker: not in play, every slot is hand-locked in the harness and no levy visits an armory (states: Follow 95%, Engage, nothing else).
- (c) harness army: same 34 levies and items, only the slot they sit in.
- Recordings (slots on / off / pre, 40 fights each): grounded speed 2.62 / 2.55 / 2.57 px/tick (SE 0.03-0.04), contact frames per 1000 levy-frames 22.5 / 21.3 / 21.2 (SE 1.1), body hits 104 / 105 / 96 per fight, head hits 26.8 / 26.7 / 27.2, damage per levy-second alive 123 / 125 / 131. Levies stay alive about 7% fewer seconds in the slots-on batch (alive at 30 s: 17.0 / 18.4 / 17.6), nothing more. No faster-levy signature, so no dodge change is proposed.

## 1b. Skeletron Prime and Calamitas Clone contact damage: it IS the shot veto, not mobslots

Master+Death, army of seed 1, 20 fights each (Calamitas Clone: some cur fights ended in timeouts).

| arm | Prime contact dmg / deaths | Calamitas Clone wins, contact dmg / deaths |
|---|---|---|
| pre (veto on, no slots) | 3071 / 10.3 | 19/20, 7417 / 11.8 |
| cur | 5764 / 14.8 | 5/20 (5 timeouts), 17103 / 26.9 |
| cur, `LEVY_SPECSHOTS=0` (veto back) | 2712 / 9.1 | 19/20, 6534 / 8.8 |
| cur, `LEVY_MOBSLOTS=0` | 5888 / 15.5 | 11/20, 15834 / 20.6 |
| after mobslots (veto on) | 2450 / 8.8 | 19/19, 6881 / 8.7 |
| cur, both switches | 2648 / 8.2 | 20/20, 7598 / 10.3 |

The veto removal moves them, mobslots does not. Recordings, pre vs cur: contact time, episodes, speed and distance are unchanged (Prime: 9.3 vs 9.2 contact frames per 1000, 49 vs 51 episodes; Calamitas Clone 2.6 vs 2.9, 164 vs 182), damage per hit is unchanged (Prime 124 vs 122), but contact hits per episode doubled (Prime 0.73 to 1.59, Calamitas Clone 0.45 to 0.74) and contact hits landing within 30 ticks of an earlier hit on the same levy went from 95 to 410 (Prime). That appears only once hostile shots land (113 and 340 one-damage shot hits per fight). The levy's hit-immunity bookkeeping after a shot hit (`SoldierNPC.AfterHit`, `SlotImmune` / `ImmuneTime`, `HitImmuneTicks` gives 20 ticks to a 1-damage hit) is where to look; I did not isolate the exact line and propose no change here. This is a real combat change that the correct veto fix exposed, to be settled by Eve before anyone retunes anything (never by weakening the boss or the army).

## 2. `Projectile.Kill` IndexOutOfRange through `UseGuard.KillDetour`

KillDetour indexes nothing: it forwards to `orig`; the exception is inside vanilla `Projectile.Kill`. Reproduced with a Queen Slime Master+Death army of Ice Rod levies (1 of 48 fights; the original army also holds 3 Ice Rods, and the same trace is in the old hmreplays log, Calamitas Clone): a diagnostic hook logged `type=80 Ice Block, position (33274, -19.8), velocity (-2.8, -19.8), timeLeft 0`. Kill for type 80 reads `Main.tile[(int)position.X/16, (int)position.Y/16]` unchecked; the block crossed the top edge in the same step its time ran out (Update drops a projectile that already left the world without calling Kill), so it read tile row -1. A levy shooting up at a high boss makes this likely.
Fix (`UseGuard.KeepProbeInWorld`): before an army Ice Block's Kill, its position is put back inside the tile grid (the world's outer border holds no tiles, so the read finds nothing, exactly as intended) and Kill runs in full. Not a catch.

## 3. Calamity `PreAI` NullReference through `CalamityTargetingCompat.PreAiDetour`

Reproduced with one Moon Lord fight (every Moon Lord fight throws once, on the last tick). Null = `CalamityVanillaAIOverrideNPC.AIOverride` of Moon Lord's core (`overrideBefore=MoonLordAI overrideAfter=null`, core inactive, ai = 2, 600). Calamity's override AI ends the death animation inside its own pass: it deactivates the parts and core and calls `NetMessage.SendData(SyncNPC)`; on a server that runs Calamity's `SendExtraAI`, which clears `AIOverride` of an inactive NPC; `PreAI` then reads `AIOverride.DisableMultiplayerSmoothing`. Single player never hits it.
Fix: a `SendExtraAI` hook in Levy's Calamity compat keeps the override through the NPC's own PreAI pass (written packet unchanged, field put back), so nothing throws; the PreAI detour keeps a provable-case fallback (override bound before, cleared now, NPC dead) for a Calamity whose hook cannot be installed.

## Proof

- `BossBugsWorldCheck` parts `mlkill` and `killedge` (`LEVY_BOSSBUGS_PART` now takes a comma list). Before the fixes: FAIL on the Moon Lord death (NullReferenceException x1) and on the Ice Block above the top and past the left edge (IndexOutOfRange). After: 21 PASS, 0 FAIL, no first-chance exception in the log.
- Serial `shipboth.sh` (`SHIPWORLD_SERIAL=1`, private paths) on the final commit: vanilla boot exc=0, Calamity boot exc=0; vanilla world 0 FAIL lines, 0 exceptions (BossBugs 39 pass), Calamity world 0 FAIL lines, 0 exceptions (BossBugs 57 pass). Only the known summon-staff pick line is excluded.
