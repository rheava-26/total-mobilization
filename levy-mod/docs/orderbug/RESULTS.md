# orderbug: "soldiers refuse orders unless they die first" (Eve, 2.0.43)

## Result in one paragraph
NOT REPRODUCED on any revision from 2.0.38 (564129b) to 2.0.43 (HEAD). Every order the Command Radio radial offers (Follow, Hold, Return, Search & Destroy, Free Rein, Hold Fire, Spread Out / Close Up / Normal) reaches every levy's record and body within one tick, in every population tried,
so no merge in 8bb0062..0adfad3 breaks it and there is nothing to bisect to. The fix below therefore adds what was missing for the next report: an order can no longer be lost to one levy's exception, the audit says WHY a levy did not take an order, and a boot self-check
drives every order through every posture.

## What was run (harness `orderobey`, `CombatHarness.OrderObey.cs`; `orderprobe`, `CombatHarness.OrderProbe.cs`)
- Orders go through the real packet handler (`LevyPackets.Handle`, sender = player 0, `atBarracks` exactly as the radial sends it: true with a radio in the pack) or the single-player path (`UIActions.Order` with `Main.netMode` 0), 13 orders in sequence, 2 s measured per levy.
- Populations in one army (15-26 levies): reloaded from a saved roster (in-process Save/Load, and a real world file written by one process and loaded by another, including a roster written by the 2.0.38 build and loaded by HEAD), freshly spawned, killed and respawned at the barracks, seated at Hold posts (chairs), seated Follow levies (calm round), a 16-levy Calamity army (Bee Gun, Barinade, Aorta, Wind Blade ... with Cloud in a Bottle etc.).
- Conditions: stand-in boss fight (boss flag, damage 0-80) and calm; radio / radio-near / no radio / barracks panel; a 17-minute (63,000 tick) boss fight with respawn churn before the first order; pouches emptied just before the first order; every even levy held at Shaken (fear 60) or Routed (fear 80); real Calamity fights (King Slime x60 HP, Eye, Skeletron, Queen Bee, Brain, Slime God, Hive Mind) with low-HP levies and a barracks so deaths respawn.
- Verdict per levy: record holds the order, `HoldPosition` matches, body state is the order's state (or Engage/fear/haul), moved toward home on Return.
- Bisect table: `bisect1.txt` (same harness ported onto 8bb0062, 080cf5e, 49835ec, 654ada5, ffad88f, 8e5c82f, 1e69c7b, 0adfad3 and 564129b by `port.sh`): `REC-IGNORED lines: 0` at every revision, in both rounds; the only non-ok lines are the harness artifact below, identical at 2.0.38.

## OBSERVED
- Record side: 0 of ~3000 levy-orders ignored, any revision, any population, either path.
- Body side: after Follow, a levy is sometimes in ReturnHome for a think. TRACE (`LEVY_OO_TRACE`) shows `CommanderIndex` flipping to -1 and back: the stand-in commander has no client behind it and the server flags the slot off between ticks, `ResolveCommanders` then writes -1,
  `OrderState()` sends a Follow levy home until the next think finds the player again. Same at 2.0.38. It cannot happen with a connected player; the harness exempts it (`CmdGone`).
- Return with no barracks in reach makes the levy fall in behind the commander (by design, `OrderState`).
- A Routed levy backs away whatever it is told (Hold moved 23 tiles in 2 s) and a Shaken one keeps its label while it behaves by the order beneath: Fear owns those states by design; fear drains 4/s after 3 s without a hit under the shipped Events model.
- A calm seated levy told Hold stands up and sits again within seconds (the post), not a miss.

## INFERRED (not shown): what is left that matches "refuses until it dies"
Per-body state that a respawn replaces and an order does not: Routed/Shaken latches (respawn sets Fear 0), a levy in `ThinkUnarmed`/`StandAway` (no usable weapon, threat in range: stands still and the order is never read; respawn re-issues gear), a Resupply walk that cannot reach its armory,
a travel/hook latch from travel4, a stale Commander on a client. None reproduced here; the audit below names which one it is the next time it happens.

## What changed
- `RosterSystem.IssueOrder`, `ToggleHoldFire`, `ToggleSpreadOut`: each levy handled in its own try/catch (`OrderFault`, logged once per kind), so one body that throws can never stop the order reaching the rest.
- `RosterSystem.SendToPlayer`: no chat to a slot without a connected client (it threw on a client-less commander and aborted the end of the order).
- `OrderAudit` (every order, live, server/single player): 3 s later logs `[OrderObey] audit <order>: reached=N bodies=B obeyed=O missed=M` and, per levy that did not take it, `[OrderObey] MISS <name>: <reason> | state= order= hold= seated= target= goal= why= fear= canUseWeapon= commander= home= ...`.
- Boot self-check `[OrderObey] self-check pass=12 fail=0` (`OrderObeySelfCheck`): real `IssueOrder` on a real body in 9 calm postures x 5 orders, seated, Shaken/Routed/Desert, order-after-order, hotkey radius vs radio/panel, other commander / reserve, Hold Fire, Spread cycle, the audit rule, packet handlers.
  Mutation test: removing `Sit_Stop()` from `Brain_OnOrder` turns it to `fail=1` ("a seated levy is on its feet ... seated=True -> Follow").
- Ask Eve for `Logs/client.log` lines containing `[OrderObey]` after clicking the radio again.

## UPDATE: Eve's exact description (first load of a session, Return ignored, levies follow and fight, kill some and they obey)
OBSERVED (two-process harness: process 1 saves a real world with the army out, process 2 loads it; `LEVY_OO_LOADED=1 LEVY_OO_STALEHOME=1`): when a loaded record's `HomeBarracksId` names nothing (tile-entity ids are renumbered on every load, so the saved id is only valid by luck; my first
real-load test passed because the id happened to be 0 both times), `OrderState()` for Return has no Home and "falls in behind the commander": record = Return, body = Follow, dx 0 in 2 s, Search/Hold look fine because the levies keep fighting beside her. A killed levy goes through `TickRespawns`, which re-bases the home, so it obeys.
Without the fix: Return -> body Follow, 0 tiles moved. With it: ReturnHome, 20 tiles in 2 s.
Fix: `RosterSystem.RepairHome` re-derives the id from the stable `HomePos` on every deploy tick and before every order (`Selected`). The audit now reports "barracks link lost" for a Return levy whose Home is null while HomePos is known. Self-check test 10 (pass=13 fail=0) covers a load that leaves the id -1 or stale.
Not the cause: militia (not Regulars, never follow), officer avatars, Auto-muster (militia only; `ClearStaleMobilization` runs on load).
