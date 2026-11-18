# Mind control and sky-rain audit

Harness: `IdentityWorldCheck.Mind.cs` (movement, attack, sync, lifecycle) and the sky-rain class in `IdentityWorldCheck.Origin.cs`.
Run alone: `LEVY_IDENTITY_ONLY=1`. `LEVY_MIND_LEGACY=1` runs the old goal-steered drive for the "before" numbers.

## Star Shower (Astrum Deus)

Star Shower spawns its stars at `Main.screenPosition.Y - 80`. The levy's cursor frame put the screen at the world's top-left corner, so every star spawned at world y = -80.
Fix: `AimView` gives the levy the 1920x1080 screen a player would have, centred on the levy, cursor on the aim point.
Before: 0 projectiles, 0 damage in 150 ticks. After: 45 projectiles, all above the target, 2399 damage.

## Mind control: before and after (player = real Player.Update with the same keys)

| Behaviour | Player | Before | After |
|---|---|---|---|
| Hermes run, ticks to 90% | 91 | 29 | 91 |
| Hermes speed at tick 120 | 6.33 | 6.48 (6.0 without the Calamity sprint bonus) | 6.33 |
| Jump tap apex | 43.2 px | 104.3 px | 43.2 px |
| Jump held apex | 117.6 px | 104.3 px | 117.6 px |
| Double jump apex | 85.0 px | 104.3 px | 85.0 px |
| Wing speed in flight | 6.45 | 3.38 | 6.77 |
| Dash peak | 31.65 | 2.96 | 16.7 |
| Body syncs to her client, 120 ticks | | 1 | 121 |
| Attack, commander dead: weapons broken of 73 | | 3 (Tumbleweed, Ice Barrage, Undine's Retribution) | 0 |
| Hotbar, MindInput packet, hand-over after a fall | | missing | pass |

Causes: the old drive steered the AI toward a point 96 px away; the dummy's Calamity sprint bonus was missing; a dead commander's shots were killed by their own owner-dead checks; the body was sent to the client about once a second;
the view did not follow a handed-over link, and the arrow keys (movement keys) switched levies.
