# PACING & TENSION COUNCIL — Round 3 (`pacing`)

## 1. Answers

Six motions name the Secretariat.

**1. THE RESOLVE ENGINE (autofleet, to lore + pacing) — AMEND.**
Fear and Resolve as one gauge with the prep length untouched is exactly our Motion 1. Amendment: the ALARM bar is visible during prep and shows the fleet's *perceived* F (so Maskirovka, STABILITY Motion 2, can move it). Fear is applied once at arrival (`fleetMax`, `OPEN_BOMBARDERS + floor(F)`, `t` multiplier). Drop the cap of 9 bombarders as a hard number and make it a soft ceiling that the Fear Index may exceed at F=4. A cap on the *enemy* is allowed, but a scarier enemy is the tenet's whole instruction. Buried industry at half weight stays.

**2. THE FLEET HAS A PLAN (autofleet, to pacing + space-fleet) — AMEND.**
We co-sign the merge of AUTOFLEET-4, PACING-1, PACING-3 and PACING-5. Amendments:
- Ship **three gambits first** (Net, FOBS, Wedge) as a data table owned by `INTENT`. The other three (Hammer is the default, Rod Storm, Trojan) follow in a later slice.
- RETHINK/DESPAIR gate on the withdrawal door (`!G._rethinkActive`) expires after **90s**, so a stalled branch can never deny a win.
- Interbellum is 45% of `prepTimer`, return fleet `fleetMax × (1 + 0.6·cycle)`, `it.cycles = {invasion: 3, glass: 1, others: 1}`. Flagship death at any cycle makes the next withdrawal final.
- Stability resets to `min(cap, stab+15)` each cycle: no death spiral across cycles.
- Orbital Denial door is gated on `orbitalcmd` + `spaceage` and needs fewer than 4 live enemy hulls in the space band for 40 continuous game-seconds. Space-fleet may tune the numbers; it gets the only door that rewards its fleet.

**3. THE FLEET'S MIND (lore, to autofleet + pacing) — ACCEPT, with one amendment.**
This is the best statement of the doors: Attrition, Rout (flagship dead and Resolve <60), Orbital Denial, Truce. Amendment: the Truce transmission is offered **only in the final cycle or at Desperate**, never during an early Interbellum, so a cheap draw cannot cut Motion 2's campaign short. Truce stays voluntary.

**4. THE TAPE (market, to pacing + lore) — AMEND.**
`conf = 1 - 1/sqrt(1+n)` with weights Radar 0.5, Data Center 0.5, Tape Room 1, Tracking Array 1.5 is accepted as the one formula. Amendment: the intent label never lies, **and neither does the fleet's arrival time at T-60 or later**; below that, bands are `±40%·(1-conf)` with a ±5% floor. The five prep beats (70/45/25/8/4%) are a renderer of this value, not a second model. Positions (staking influence) ship after the forecast.

**5. ONE DIAL, FIVE FACES (new-player, to market + pacing) — ACCEPT.**
`G.forecast` as one object with five renderers is the right architecture. The Secretariat's prep beats are renderer four. We ask only that the `signals` node and the Tracking Array share a single research path (`signals` → Tracking Array building), so there is one node, not two.

**6. THE FLEET READS THE HOME FRONT (stability, to autofleet + pacing) — ACCEPT, with one amendment.**
Our PACING-HOT is dropped as a clock drain, as the motion states. Amendment: the HUD ALARM bar shows what the fleet *believes* (post-Maskirovka), and a small tick shows the true F, so the player sees the lie working. The third-campaign "propaganda detected" answer (blind carpet fire) is a scarier enemy and passes Article III.

## 2. Joins

- **Planet's Theatres motion / PLANET-4 (S slice: Archipelago and Rift)** — we co-sign any motion that pairs map templates with gambits as one "scenario card". Replayability is the designer's own complaint.
- **Game-Feel's withdrawal Director (GAME-FEEL-3)** — we co-sign the letterbox on the **final** withdrawal only, with a caption per door (Attrition, Rout, Denial, Truce).
- **Science / Market Unified Forecast (science, to market + lore)** — we join as an observer weight source (Deep Survey adds to `n`).

## 3. Competing motions

- **The fleet's gauge.** Backed: **lore MOTION A "The Fleet's Mind"** as the spine, because it names four doors and the Truce condition in one place, with autofleet's Resolve Engine numbers and stability's Maskirovka shave as inputs. Autofleet B, lore A and stability 2 are one coalition bill; our own Motion 1 merges into it.
- **The forecast.** Backed: **new-player Motion 1 "One Dial"** as the object, **market "The Tape"** as the confidence formula. Our own Motion 2, science's Unified Forecast and the new-player and market versions are one bill: one `G.forecast`, one `conf`, five renderers.
- **The return and the plan.** Backed: **autofleet Motion C** merged with our own Motion 3, which it already contains.

## 4. Closing statement

Mr Speaker, the Secretariat has spent three rounds learning that the clock is not the dread. The dread is a fleet that *notices*, a forecast that sharpens as you build your eyes, a war that has more than one door and a retreat that is a pause. We gave back the drain, and the House gave us a fleet with a mind. Build the gauge, build the dial, build the return. Nobody should be able to say, after this session, that they won five times the same way. Time is still on no one's side. We prefer it that way.
