# Member for Mischief (crossbench) — session 2

Source: `title.html` only. Line numbers are as of this read.

## 1. Maiden speech

Honourable Members, I broke your game over a weekend and it was a *pleasure*. You built a magnificent machine and left the keys in the ignition. A factory discount that rewards "ten of the same building" until a Rifleman costs 8 IC. A missile that costs 35 IC, pays no political price at all, and has a switch on the silo that fires it forever while I make tea. A fleet that studies my army and then politely ignores me if I keep my army *balanced*. A bunker-busting enemy that only hits what I leave on the surface — so I put the economy in the cellar. None of these are bugs. They are *mechanics nobody finished*. I do not want to nerf one toy. I want the enemy to *notice me*. Give me an enemy that learns, and I will gladly break your game a second time, harder, and thank you for the privilege.

## 2. The exploits (ranked by damage to the game)

**1. Silo carpet with auto-strike (blatant).**
- Recipe: build N Missile Silos (a Missile Silo is an existing strategic launch building, 420 IC, `title.html:3407`). Set each to auto-strike a Heavy Guided Missile (an existing conventional strike).
- Arithmetic: `STRAT.ballistic` is 35 IC, cooldown 5, and 360 damage over a 150-radius blast (`:6886`, `detonateHE` `:7599-7604`). Only taboo strikes charge stability (`stratStab`, `:6897`), so conventional strikes cost no stability. Uranium cuts the IC cost to 25 (`stratCost`, `:6896`).
- One silo is about 5-7 IC/s for 72 area-damage per second. Fifty silos is about 18,000 area damage per five seconds from a one-off 21,000 IC.
- `tickAutoStrikes` (`:7715`) picks clusters and spreads claims, so it needs no attention after setup.
- The only brake is that the enemy gets to shoot at a 90 HP missile (`STRAT_MSL_HP`, `:4769`). Nothing in the enemy's adaptation reacts to missiles.

**2. The factory discount ladder.**
- `unitCost` (`:3835`): `disc = max(.22, 1 - .09*(n-1))`, where n is the count of buildings whose `prod` matches the unit's domain (`facsFor`, `:9922`). It counts every one, finished or captured, with no distance or throughput requirement.
- Ten Factories (a Factory is an existing ground-production building, 160 IC, `:3389`) give 78% off all ground units. Stacked with tech and building cost mods (floor `.1`), the unit price floor is 8 IC.
- Result: Infantry (30 IC) cost 8. Mass is the strategy. The ladder is linear and saturates at ten buildings, so the eleventh is dead weight.

**3. Mono-army camouflage (enemy blind spot).**
- `scanEnemyIntel` (`:5744`) calls an element "dominant" only if it is at least 0.8 of a fixed scale (aa/40, lasers/6, infantry/25, robotic/30, navy/10, spaceReach/40) and at least 1.4 times the runner-up.
- Recipe: keep two or three categories each under 1.4x of each other, or under 80% of the scale, and `counterBias` stays neutral. Each response is only a kind-weight nudge (`counterBias`, `:5520`).
- Also, the scan reads the state when it runs; swap a few units around before the scan and the read flips.
- The enemy "learns" only from your composition snapshot, never from what actually killed it.

**4. Burying the economy.**
- `digLevel` (`:3863`) lets any industry, housing or silo go underground. Cost is x1.5 shallow or x2.1 deep (`ugMul`, `:3890`).
- Underground buildings are dropped from the barrage and WW3 target lists (`:7636`, `b.ug` skipped) and from the fort cache. Ground enemies hurt them only within 48px and only slowly (`:6603`: 9 or 4 HP/s per attacker). The only real answer is the bunker-buster (`:7511`).
- Recipe: dig everything, bunker-buster rarity is the only tax.
- Blind spot: the enemy never checks how much of your economy is buried.

**5. Garrison and autospawn free lunches.**
- Factories, Barracks, Airbases, Data Centers and others carry free `garrison` units. The Autonomous Plant (an existing building, 260 IC, `:3440`) spawns a free shock-trooper unit every 16s (every 4.8s with Deep-Ore Excavator, `:6439`), gated only by army weight.
- The brake is `GARRISON_UPKEEP_PER_WEIGHT` (0.12 IC/s per weight, `:3807`) and the Logistics Hub's upkeep cut (`:3472`, `:6329`, floored at 40%).
- Recipe: stack Logistics Hubs to the 40% floor (about four), then spam garrison buildings. Garrison cost scales sub-linearly, which dominates hand-built armies.

**6. IC floor plus a stalemate draw.**
- `IC_FLOOR` (`:6254`, `:6330`) clamps net income to at least +8 IC/s however heavy the upkeep. Upkeep can never bankrupt you.
- Recipe: build an upkeep-heavy army, because deep deficits are hidden below +8. Combined with `STALEMATE_WINDOW = 240` (`:6613`), where zero progress ends the game as a draw, a player who walls off in perpetuity avoids the loss.
- Blind spot: the enemy never escalates when you stall.

**7. Boss kill stability refill.**
- Every boss kill gives +10 stability and the flagship +15 (`:6870`). Killing the flagship removes 25% of the whole fleet (`:6876`).
- Recipe: farm bosses with silos to keep stability pinned at the cap.

## 3. Bills

**Bill 1 — The Counter-Battery Act (answers Exploit 1)**
- *Pitch:* "If a missile is free, the enemy should learn to hate it."
- *Fantasy:* Your mass of missiles is so scary the fleet has to build around it.
- *Annex:*
  - Mechanic: the autofleet tracks how many conventional strikes you launched in the last 60s. Above 20, it fields **Interceptor Frigates** (a proposed enemy: orbital hulls that shoot down incoming missiles, 400 HP, one-shot a missile every 0.5s).
  - The count scales with volume, not with your silo number, so your toy stays uncapped.
  - Numbers: each frigate intercepts about 2 missiles/s. At 60 strikes per minute, five frigates spawn per wave.
  - Hooks: add a `strikeRate` counter in `launchStrike`; read it in `scanEnemyIntel` beside `aa`; add a `counterBias` branch.
  - Size: M.
  - One-Button Law: pass, because nothing needs attention. The Line: pass, because the frigates are orbital. Tenet: pass, because the enemy reacts and nothing is capped.

**Bill 2 — The Cheap Is Not Free Act (answers Exploit 2)**
- *Pitch:* "Ten factories and the army costs eight apiece. Let's make mass loud."
- *Fantasy:* Your cheap hordes finally draw a hunter.
- *Annex:*
  - Mechanic: the **Culling Fleet** (a proposed enemy wave type: area-damage platforms) spawns by the count of bodies under 15 IC, at about one platform per 150 cheap bodies.
  - No nerf to `unitCost`. The enemy answers mass with area damage, which also gives AA and cluster weapons a purpose.
  - Hooks: `counterBias` + `scanEnemyIntel` (add a `cheapBodies` metric next to `infantry`).
  - Size: S.
  - One-Button: pass. The Line: pass (ground). Tenet: pass.

**Bill 3 — The Blind Spot Act (answers Exploit 3)**
- *Pitch:* "Let the fleet learn what actually kills it."
- *Fantasy:* Your build is read like a book, and the enemy writes a counter.
- *Annex:*
  - Mechanic: track kill credit by weapon family (`killBy`, already stamped in `hurt`) over a rolling 120s. The family doing at least 35% of kills is the "dominant killer" and feeds `counterBias` alongside composition. That also catches balanced armies.
  - Numbers: adaptation banner at 35%; the shield, armour or decoy response weights double.
  - Hooks: `killBy` in `hurt` (`:4978`), `scanEnemyIntel` (`:5744`).
  - Size: M.
  - One-Button: pass. The Line: pass. Tenet: pass; enemy-side only.

**Bill 4 — Burrowers (answers Exploit 4)**
- *Pitch:* "You live underground? So will they."
- *Fantasy:* The earth itself becomes a front.
- *Annex:*
  - Mechanic: a **Burrower** (a proposed ground enemy that digs in and drills at buried buildings) spawns in proportion to the fraction of your IC income sitting underground. Above 30% buried, one Burrower per ground landing; above 70%, three.
  - Numbers: 400 HP, drills 40 HP/s on one buried building, killable only by ground and sub-surface fire (existing ground units shoot it while it surfaces).
  - Hooks: `scanEnemyIntel` computes the buried fraction; `spawnEnemy` gets a new kind; hook into the `:6603` damage loop.
  - Size: L.
  - One-Button: pass. The Line: pass, since it is the underground dimension. Tenet: pass.

**Bill 5 — The Garrison Mandate (answers Exploit 5)**
- *Pitch:* "Free troops should be a standing army that people notice."
- *Fantasy:* Your homeland is crawling with defenders, and the enemy knows it.
- *Annex:*
  - Mechanic: weight of garrison units feeds a "Fortress Index"; above 40% of total army weight, enemy landings gain **Siege Breachers** (proposed ground enemies with area damage that hit garrisons) and a flank-first target bias.
  - Hooks: `GARRISON_UPKEEP_PER_WEIGHT` (`:3807`), `scanEnemyIntel`.
  - Size: S.
  - One-Button: pass. The Line: pass. Tenet: pass; no cap on garrisons.

**Bill 6 — The Escalation Clause (answers Exploit 6)**
- *Pitch:* "A wall nobody attacks is a draw nobody earned."
- *Fantasy:* Your defence is so good the enemy has to bring something new.
- *Annex:*
  - Mechanic: when the stalemate counter passes 120s with no progress, the fleet commits a reserve: a doubled wave plus one extra boss from the intent's `supers` list. That keeps the draw as a last resort and makes it hard to reach.
  - Hooks: `G.lastProgress` near `:6613`.
  - Size: S.
  - One-Button: pass. The Line: pass. Tenet: pass.

**Bill 7 — Boss Fatigue (answers Exploit 7)**
- *Pitch:* "The fifth hive costs less stability than the first? No."
- *Fantasy:* Bosses keep mattering, and the flagship stays a milestone.
- *Annex:*
  - Mechanic: stability from boss kills halves for each same-type kill in the last 120s (10, 5, 2.5). Evidence is blatant only if boss farming is reachable; I flag this as the one nerf bill, with proof pending a playtest of silo-farmed bosses.
  - Hooks: the `:6870` stability gain.
  - Size: S.
  - One-Button: pass. The Line: pass. Tenet: weak pass; it is the one bill that reduces a reward, so ship only if testing proves the pin.

## 4. Hot Take — The Autofleet Has a Memory

**Pitch:** "Stop adapting inside a run. Adapt across runs."

- Persist a small "grudge ledger" in `localStorage`: the three families of kill your last games leaned on (missiles, mass infantry, buried economy, and so on).
- On the next invasion the fleet opens already armed against your *habit*, with a 15% fleet-strength bonus to counter-units and a banner: "THE FLEET HAS STUDIED YOUR LAST WAR."
- The player fantasy: the enemy is a rival who remembers you, so your signature strategy becomes a boss you have to outgrow.
- **Annex:** Numbers: a three-entry ledger, decaying 25% per game so that a new playstyle fades the old grudge. Hooks: the end-of-run summary stat block (`end()`), `scanEnemyIntel`'s prior `counterBias`, and wrap every `localStorage` access in try/catch. Size: M.
  - One-Button: pass, because it is automatic. The Line: pass. Tenet: pass; it adds enemy answers and caps nothing. Risk: a first-time player needs a "ledger off" standing choice, which is a single toggle in the menu.
