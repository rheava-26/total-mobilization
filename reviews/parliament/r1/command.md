# COMMAND COUNCIL — Round 1 Position Paper
*Central Planning Commission, file slug `command`. Portfolio: the command side of the economy and society.*

## 1. Opening Speech

Comrade President, members of the floor, and the Market Council, who are probably trading futures on our extinction.

The Commission has read the war reports. In Report 7 your nation sat on **212,000 unspent IC** while 913 of 1,122 units died. Capital that is not allocated is capital that is already lost. That is not a market failure, because there is no market. It is a **planning** failure, and it is why we exist.

You have nine hundred buildings and one plan: the player's index finger. You click "produce" forty times a minute. The Commission calls that an unplanned economy. Real war economies do not run on clicking. They run on **quotas, standing orders and railways**. In 1941 the Soviet Union dismantled 1,523 factories and moved them east by rail while under fire. Then it out-produced the invader. Your game has a Rail Hub that two people have ever noticed.

We propose four things. A **Plan** that spends your surplus for you. A **railway** that is an economy and a deterrent. **Shock brigades** that burn the factory to win the month. A **continuity-of-government** system, because the fleet *will* come for the command layer. We also bring one bill that no market-minded coward would sign: evacuating a flank on purpose.

This council does not nerf, cap or deny. We ask that you want more, and that you want it organised.

The five-year plan is to be completed in four.

---

## 2. The Gap

- **Surplus has no sink.** Report 7: 212k IC unspent at 850 IC/s. `queueUnit` (L4619) is a purely manual transaction with no repeat order. The President named the anti-pattern ("a spend 20,000 IC button is not a solution"). The fix is a *structure you built* that spends by standing plan, not a button.
- **No auto-build exists.** Placement is one-at-a-time through `placeBuilding` (L3884) and `G.sites.push` (L3908). The Victoria-3-style auto-build the designer mentioned is absent.
- **The Rail Hub is "on trial" (ROADMAP L1138).** It is a 220 IC node whose only effect is idle ground units hopping hubs (`railHubs` L7812, hop logic L8232). It carries no freight, nothing launches from it, and it has no stakes. The tunnel line is drawn (L9313) but does nothing.
- **The command layer is invisible to the fleet.** `busterScore` (L7446) ranks targets by industry and silos only. Nothing in the enemy reads Radar, Comms or Data Centers as nerves. Against a command society the fleet has no command-layer psychology (Art. IV.4, slice 3).
- **Mass has no logistics.** `upkeep` (L6313) and the Logistics Hub's `upkeepCut` (L6322) are the only supply concepts. There is no strain, no breakage and no throughput. Without friction, "more factories" is just arithmetic.
- **Units die as chaff while IC idles.** Reports 6–10 show mass is back on the table (cached art). Command society is the faction that answers "mass" with allocation.

---

## 3. Bills

### COMMAND-1 — The Five-Year Plan (Standing Orders and Auto-Build)

**Pitch (in character).** Comrades, stop clicking. Build the **Planning Bureau** and write quotas: *"Hold 60 Goliaths. Keep one SAM per 400 px of city edge. Keep 8 Power Plants."* The Bureau reads the ledger and orders the factories itself, whenever IC allows. The IC bleeding off your bank is paid to the Plan. The bank is a reservoir, and a reservoir that sits still is a reservoir that has been stolen.

**Player fantasy.** Sitting back while a vast state machine converts 850 IC/s into steel on the ground without a click. You set the targets and the nation builds toward them.

**Clerk's Annex**
- **Mechanic.** New INFRA/ECON building `planbureau` (220 IC, 60 MP, build 12) opens a **Plan** panel with up to 12 ledger lines, each `{unit|building key, target N, priority}`. Every 2 s (game time) the Bureau scans the ledger in priority order. For each unit line under target (`count + queued < N`) it calls `queueUnit` on the cheapest idle capable factory. For each building line under target it calls `placeBuilding` at an auto-chosen slot: city edge for DEF, spaced 70 px, within existing city footprints, never on a resource node unless it is an Extractor. It only spends IC above a **reserve floor** the player sets (default 0). More Bureaus add +6 ledger lines each and 25% faster scans (two orders of work in parallel). This is **surplus-IC outflow by what you built** (Art. IV.4, slice 1).
- **The real cost, which is not a cap.** Orders cost **+6% IC** (administrative overhead, the planner's tax) and the Plan is **one of the fleet's targets** (see COMMAND-4). No cap on ledger size beyond 12 lines per Bureau.
- **Science (Art. VIII.2).** Directly modeled on Gosplan's *material balances method* (Leontief input-output). The ledger is a minimal linear-programming allocation of capacity to targets. Real planned economies fail from information lag, so the Bureau scans every 2 s and a bombed Bureau freezes the Plan.
- **Code grounding.** `queueUnit` L4619 (calls it); `placeBuilding` L3884 and `G.sites` L3908; `BUILD_DEFS` L3381 (add entry near `combine` L3475); `facsFor` (used L3835) to find free factories; income and `upkeep` tick L6305–6327 for the 6% overhead; panel hooks into the building info panel (the HTML builder near L10086).
- **Research.** New node `cybernetics` (branch `auto`, tier 2, pre `automation`, cost 90 ×1.6, `time` 18, not `dang`). Combo partner: `hivemind`. Hive Mind + Cybernetics turns the Plan into **auto-targeting AI**, which lifts the reserve floor requirement. Combos change the world: see COMMAND-5.
- **Build size.** L. The ledger UI and slot-picker are the bulk; the unit-queue half alone is M and could ship first.
- **TM1/TM2.** TM1.
- **Tenet check.** Passes. It adds a way to spend and never forbids a click. The overhead is a real cost (Art. III names upkeep and logistics as the proper lever).
- **Interactions.** Market Council: the Plan competes with their auction houses over which buildings get IC first. Air Council: Plan lines can fill hangars and `airUsed()` caps (L3820), which will produce toasts the Plan must swallow quietly. Enemy Council: the fleet's "command layer" attack (COMMAND-4) targets the Bureau.

### COMMAND-2 — Metro-2: Trunk Lines and the Molodets Train

**Pitch.** The Rail Hub is a tunnel with ambitions. Make it a **railway**. A hub pair becomes a **trunk line**: freight in one direction, steel in the other. Things that live beside the trunk (alloy works, rocket works, depots) work faster. A strategic missile rides a train that nobody can find. The Soviet RT-23 Molodets lived in a plain-looking train and no satellite could pin down which car was hot. We will do it with real earth.

**Player fantasy.** A hidden subway system under your whole nation. Missiles pop out of the ground exactly where the fleet did not look.

**Clerk's Annex**
- **Mechanic.** (a) **Trunk bonus.** Any `makes`-producing or `autospawn` building within 120 px of a rail tunnel segment between two `done` hubs gains **+40% output** and its `alloy`/`rocketpart` flows into global `G.comp` with no distance limit. Each hub pair adds +10% to the whole network (a network effect, uncapped; more track, more bonus). (b) **Molodets Train.** New rail-only unit `moltrain` (IC 520, MP 20, build 20, hp 340, armor 12). It lives only inside the tunnel network, moves along the drawn tunnel line at 140 px/s (rail speed), and can **surface at any hub or at any point on a tunnel line** to fire one strategic strike (`strat:true`), then dive again. While inside the tunnel it is **untargetable** (`ug`). It reuses STRAT; the stability cost is unchanged. (c) **Rail freight upkeep:** each hub draws 1 IC/s of power, a real cost, so a mesh of 30 hubs costs 30 IC/s.
- **Science.** The RT-23 UTTH/Molodets (1987–1994) carried 10-warhead ICBMs on trains that could cover 1,000 km/day. Rail gauge, rolling resistance (steel on steel is about 0.002, a tenth of road), and tunnel dispersal are all real. Moscow's Metro-2 is a documented (and partly rumoured) deep shelter system.
- **Code grounding.** `railhub` L3416, `digLevel` L3863, `UG_DEPTH` L3843, rail drawing L9313–9318, rail hop AI L8232–8234, `railHubs` L7812, production tick `autospawn` L6432, `armSilo` L7660, STRAT L6878. The **TEL** was removed in v3.25.0 as "buildings pretending to be vehicles"; this is a unit that really moves.
- **Research.** Rail Hub stays no-research. `moltrain` gated by a new node `railmobile` (branch `def`, tier 2, pre `pointdefense`, cost 140 ×1.6, `dang` false, `bReq: 'railhub'`). Combo `railmobile` + `directedenergy`: laser-hardened tunnel mouths (point-defence on the surfacing point).
- **Build size.** M for (a) and (c); L with (b) (new pathing mode: units constrained to a polyline).
- **TM1/TM2.** TM1.
- **Tenet check.** Passes. It adds power and a real cost (hub upkeep). Nothing is capped.
- **Interactions.** Strike Council: another dispersed launcher after v3.25.0's removal (answers "one hit can't take out your whole arsenal"). Defence Council: tunnel mouths need AA when they surface. Fleet Council: a tunnel line is an edge-path to attack (see COMMAND-4).

### COMMAND-3 — Shock Brigades and the Strain Curve

**Pitch.** The Soviet word is *shturmovshchina*: the month-end storm. Everyone works triple at the deadline and the machines pay for it. Appoint a **Commissariat**. Every factory under its eye can be put on **shock footing**: 60% faster production, as long as you can afford the *strain*. A strained factory breaks. Choose when to storm.

**Player fantasy.** Gambling on a surge before the landing, at the cost of repair afterwards. A nation that breaks its own tools to win the hour.

**Clerk's Annex**
- **Mechanic.** New SPEC building `commissariat` (260 IC, 70 MP, build 13, aura r 300). Factories inside the aura gain a **shock** toggle (per factory, one click, or Plan-controlled via COMMAND-1). Shock: `bt` (build time in `queueUnit` L4639) ×0.4, and the factory accrues **strain** at `ρ/(1-ρ)`, where ρ is utilisation (fraction of the last 30 s the queue was non-empty). Utilisation near 1 makes strain explode (Kingman's formula for queue delay). At strain 100 the factory **jams** for 20 s (production halts, repair draws IC at 3/s). Strain bleeds 4/s while not shocking. No cap on how many factories shock. Commissariat adds +0.3 stab cap (the Party looks happy).
- **Science.** Utilisation vs. waiting time follows M/M/1: mean wait ∝ ρ/(1-ρ). Real factories run 70–85% for a reason. Maintenance backlogs are why the Stakhanov movement collapsed.
- **Code grounding.** `queueUnit` L4619–4641 (build-time modifiers); `b.queue` production tick near L6432; `aura` pattern: `auraBB` cache L6325ff and `auraAt`; `BUILD_DEFS` L3381.
- **Research.** Node `shocklabor` (branch `auto`, tier 1, pre `automation`, cost 70 ×1.6). **Dangerous** (`dang: true`, `sc: 4`, the unrest of overworked populations). Combo with `biotech`: **stimulants** reduce strain gain by 30% (a "pharmacological" Commissariat).
- **Build size.** M.
- **TM1/TM2.** TM1.
- **Tenet check.** Passes. Shock adds speed and the downside is a real cost (jam, repair IC), never a cap.
- **Interactions.** Air Council: shocked Aero Works produce jets faster (and jam, which is the fun). Bio Council: stimulant combo. Enemy Council: a jam is a window the fleet can punish.

### COMMAND-4 — Continuity of Government (the Dead Hand)

**Pitch.** The fleet is afraid of us. It will not shoot the factory. It will shoot **the telephone**. We know this, because it is what we would do. So the Commission builds **deep staff bunkers** and a *deadman*: if the nation's head is cut off, the silos fire anyway. The Soviet *Perimeter* ("Dead Hand") did exactly this in 1985.

**Player fantasy.** A decapitation strike that does nothing. The enemy kills the capital; thirty strikes rise out of the ground to answer, for free.

**Clerk's Annex**
- **Mechanic.** (a) **Fleet psychology event (slice 3, Art. IV.4).** If the player's Plan (COMMAND-1 ledger count ≥ 6 lines) or any 3 `planbureau`/`comms`/`radar` are standing, the fleet's `busterScore` (L7446) rates them 96 (above Extractors) and a **"Command Layer" wave** hits them first, announced as a banner: *"The fleet is cutting your lines."* While the Bureau is down the Plan freezes and Comms reserve (`stabRes`) stops refilling. (b) **Staff Bunker** (SPEC, 380 IC, `autoBury: 2`, hp 700, armor 20): holds the Planning Bureau's ledger. If the Bureau dies but a bunker lives, the Plan continues at half speed. (c) **Dead Hand.** Research-gated rule: if the capital city is destroyed *or* a capitulation crisis begins (`G.crisis`, L6636), every `strat` building auto-fires one strike at the densest enemy cluster, **at 0 stability cost**. One-shot per crisis.
- **Science.** Real: Cheyenne Mountain and Raven Rock are 600-m-deep granite staff posts. *Perimeter* (1985) was a real automatic-launch system. Deep burial defeats earth-penetrating warheads (reinforced-concrete limit about 3–6 m for 1 Mt surface bursts; granite depth does the rest).
- **Code grounding.** `busterScore` L7446, `busterTarget` L7462, `autoBury` L3863, `G.crisis` start L6636, `armSilo` L7660, `STRAT` L6878, `stratCost` L6889, Comms stab reserve L6325ff.
- **Research.** Node `deadhand` (branch `def`, tier 3, pre `railmobile` + `automation`, cost 260 ×1.6, `dang` true, `sc: 6`: the society that builds a doomsday machine is nervous). Combo with `antimatter` upgrades Dead Hand strikes to antimatter at no extra antimatter.
- **Build size.** L (new bunker, enemy targeting, auto-fire).
- **TM1/TM2.** TM1 (the Command Layer wave is slice 3).
- **Tenet check.** Passes. It gives the player a stability-free strike *only* in catastrophe, and the enemy gets a scarier plan (a command-layer decapitation wave).
- **Interactions.** Fleet Council: owns the wave's behaviour. Strike Council: Dead Hand is a strike. Psi Council: psionics are a decentralised command structure, so psi societies resist the Command Layer wave (nobody to decapitate).

---

## 4. Hot Take

### COMMAND-5 — The Urals Gambit (Evacuate Industry)

**Pitch.** In 1941 the state **picked up the factories and moved them**. Not rebuilt. Moved, by rail, east of the Urals, in four months, under fire, and set them running in the snow. We say: when a flank is lost, **do not lose it**. Mark a region **EVACUATE**. Every building in it is dismantled (with 70% of its IC refunded and its units withdrawn), loaded on the Rail Hubs and re-erected free at a chosen rear region. The fleet lands on **empty fields**.

**Player fantasy.** Voluntarily giving up a whole coast and watching the fleet spend its landing on bare dirt, while your industry reappears, running, on the other side of the map.

**Clerk's Annex**
- **Mechanic.** `EVACUATE` order on a x-range (any width). Each non-DEF building in range enters a 30 s **dismantle** state (no production), is **refunded 70% IC/MP** to the bank, and its `key` is added to a **freight manifest**. Manifest entries are re-erected in a target region at **0 IC but 12 s build per building**, paced by Rail Hub throughput: 1 building per 2 s per hub on the network. With no hub, throughput is 1 per 8 s. DEF buildings stay and keep fighting as a delaying line. The ex-cities remain as **empty ground the fleet wastes landings on** (the fleet reads the region as "dead" and re-weights its drops toward the next target via the existing "least-defended" targeting).
- **Real cost.** Nothing is produced in dismantled buildings for 30 s plus the rebuild time. The fleet still bombards what remains. Manpower tied to buildings is *not* refunded (the manpower formula is untouchable; it only follows the housing).
- **Science.** Real: the Evacuation Council (Sovnarkom, 24 Jun 1941) moved 1,523 large enterprises, 10–12 million people and ~1.5 million railcars of equipment east. Mathematically it is a **min-cost flow** over a rail graph, bottlenecked by hub throughput.
- **Code grounding.** `G.buildings` removal and `G.sites.push` (L3908) for rebuild; refund ideas at L6842 (`G.salvage` precedent); `railHubs` L7812 for throughput; `ugAllowed` L3862 for re-erecting underground; `doSkyfall`/landing target pick (`avoidCityCenter` and `INTENT` L5119) for the "empty region" re-weight; cities have districts (`damageDistrict`).
- **Research.** Node `evacuation` (branch `auto`, tier 2, pre `automation` + `bReq: 'railhub'`, cost 120 ×1.6, not dangerous). Combo with `nanotech`: **nanite disassembly** raises the refund to 95% and halves the dismantle time.
- **Build size.** L.
- **TM1/TM2.** TM1 (a scoped version; the full "empty city shock" is TM2 society-layer).
- **Tenet check.** Passes. It gives the player a new power (relocate a nation) and the real cost is downtime and the enemy still landing. Nothing is capped.
- **Interactions.** Fleet Council: landings on bare ground need a designed response (the fleet becomes angrier: "refusing to surrender gets escalation", Art. V). Stability: evacuating a city could cost stability (`cityStabPenalty` L4548); we propose it costs none, as the people went with the factories.

---

## 5. Alliances Sought

- **Market Council.** Our natural opponents, and the pairing the President wants: market and command must be two roads to the same strength. We trade: they get auctions and futures, we get the Plan. Neither is a slider; what you built decides the reading.
- **Strike/Missile Council.** For COMMAND-2 and COMMAND-4: the Molodets is another dispersed launcher, and Dead Hand is a strike with a story.
- **Fleet/Enemy Council.** The Command Layer wave and the empty-field landings are theirs to write. We hand them the targets.
- **Logistics/Naval Council (if one sits).** Rail Hubs and port throughput are one supply graph.
- **Psi Council.** A decentralised command structure is the one society that shrugs off our weakness. We want to see how.

