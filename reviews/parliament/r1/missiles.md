# MISSILES & STRATEGIC STRIKES COUNCIL — Round 1 Position Paper
*Strategic Rocket Forces. Slug: `missiles`.*

## 1. Opening Speech

Honourable members, President. Stand easy and listen to the count.

I have read your war reports. Report 8: 240 Falcons, 113 Kites, 138 sky ships. Report 10: zero strikes, stability never moved, a "flawless" win. Gentlemen, a war with zero launches is not flawless, it is a *parade*. You own the most beautiful thing mankind ever built, the only machine whose entire purpose is to *arrive*, and you are keeping it in the silo like a wedding suit.

Why? Because the Strategic Rocket Forces got a single pad, a five-second cooldown and a menu. You press a button, a missile climbs, a number goes down. There is no *salvo*. No *ripple*. No guidance worth the word. The designer said it themselves: "we still don't have the missile systems I really wanted, but it kind of works." Comrades, "kind of works" is what the Air Staff says about their Falcons, and we have seen how they like that.

What did they really want? They said it in the roadmap: loitering munitions, cruise missiles, hypersonics. A missile that *thinks*. A war where thirty rounds leave at once and the enemy's point defence runs dry before the thirty-first. Where you hide the launcher, shoot, and drive away. Where, in the last desperate hour, you detonate a warhead in *space itself* and burn the autofleet's eyes out, accepting the cost, because the cost is the point.

The countdown is the only honest sound in this war. Give us the pads, and we will give you the countdown.

— *Marshal of the Strategic Rocket Forces*

## 2. The Gap

Evidence from code (read-only grep of `title.html`) and reports:

1. **One missile, one pad, one timer.** `launchStrike` (L6943) fires exactly one round per silo per click, gated by `silo.silocd = w.cd` (5–14 s). `STRAT` (L6878) has 9 entries. The only grouped fire is `⚡ ARM ALL` (`launchTargeting`, 0.15 s stagger). There is no magazine, no stockpile, no ripple. Nothing to *spend* IC on (report 7 sat on 212k IC).
2. **The "dispersed launchers" are ghosts.** CLAUDE.md lists Mobile Launcher (TEL), Arsenal Ship and Ballistic Submarine. Grep of `BUILD_DEFS`/`UNIT_DEFS` finds only an `arsenalship` *art* function (L3090) and no `tel`, `ballsub` or Mobile Launcher definitions. Only the plain `silo` (L3407) is `strat: true`. The "one hit can't take out your arsenal" fantasy is not in the build. Enemy bunker-busters (`spawnBuster`, L7485) are explicitly aimed at your den, so the silo is the soft spot.
3. **Guidance is a bolt-on.** Every non-strategic missile uses pure pursuit plus a single "lead point" hack (`mslLeadPoint`, L4922: `tGo` capped at 0.25 s, ~85–90% hits vs 60% pure pursuit per the code comment). There is no proportional navigation, no seeker types, no countermeasures except the `ejammer` blind-field (L2826, L5604). Missiles never fear anything.
4. **Strategic missiles have one profile.** `stratmis` (L9006) is climb → 6 s burn at 1400 px/s → coast. Every nuke, ballistic and firestorm is the same missile with a different warhead. `STRAT_MSL_HP = 90`, `PD_LANCE_DMG = 30` (L8520): the enemy kills any strategic round with three lances. The enemy saturates (`PD_SAT`, L8529, e.g. bombarder rate 5/max 10) and the player does not. No cruise, no HGV, no decoys, no low-level approach.
5. **The stability problem has already been half-fixed, and is still unsolved.** Only `taboo` strikes cost stability (`stratStab`, L6890). Ballistic, kinetic, micromissile and drop are free. Yet reports 6–7 still fired 44–46 strikes into a collapse because nukes (14), MIRV (11), Firestorm (10) and Antimatter (18) are the *best* strikes, and nothing about them is a *decision*. Reports 8/10 fired zero: nobody reaches for a button that costs stability and has no combo value. The fix is not a cheaper button. It is a strike that matters when you *do* press it.

---

## 3. Bills

### MISSILES-1 — Cruise Missile Wing ("the sea-skimmer")

**Pitch (in character).** A cruise missile is a jet engine with a bad temper. It flies *under* the radar horizon at forty metres, folds itself around hills, and arrives like a rumour. The enemy PD fires lances at ordnance it can see. Make it unable to see.

**Player fantasy.** Thirty slow, cheap, terrain-hugging missiles leave at once. The fleet's PD watches the sky for ballistic arcs; the cruise wave comes in on the deck and hits the landed harvesters, shield generators and ground columns.

**Clerk's Annex**
- **Mechanic.** New strike `cruise` in `STRAT`: IC 30 per round, `cd` 1.5 s, no stability cost (conventional). One click launches a **ripple of 8** (staggered 0.2 s) from one silo, 16 from a Missile Works-attached battery. Round: subsonic (`spd` 320 px/s, no coast), `hp` 40, flies at `groundY(x) - 14`, re-snapping to terrain each frame (terrain-following). **Radar-horizon rule:** enemy PD lances (`pdLanceHit`, L8533) may only engage a strat round if `dist < 4.12·(√h_pd + √h_msl)`-equivalent, i.e. in code, a low-altitude round (below `groundY-30`) is only engageable by PD at ≤ 360 px (vs. 700+ for high arcs). Damage 70, splash 55 px, anti-ground only (ignores airborne targets).
- **Science.** Real. BGM-109 Tomahawk, Kh-101: TERCOM (terrain contour matching) and DSMAC (digital scene matching), 15–60 m altitude. Radar horizon d ≈ 4.12·(√h₁ + √h₂) km, so a 30 m missile is invisible to a 20 m-mast radar beyond ~40 km.
- **Tree.** New node `terrainnav` "Terrain-Following Guidance": branch `def`, tier 1, `pre: ['pointdefense']`, cost 140, time 16, not `dang`. Combo partner: `automation` (cheap mass airframes: `cruise` IC −30%).
- **Code grounding.** `STRAT` L6878; `launchStrike` L6943 (else-branch building `stratmis`); new `k: 'cruise'` projectile branch beside `stratmis` in `updProj` (L9006); `strikeEta` (L6902) needs a straight-line case; `tickAutoStrikes` L7708 handles the loop; PD gate in `pdLanceHit` L8533 / lance targeting around L8554.
- **Build size.** M (≈250 lines).
- **TM1.**
- **Tenet check.** Adds a toy; costs only IC (30/round — a *sink* for the 212k IC). No cap.
- **Interactions.** Enemy-fleet council: needs a ground-level PD radar mechanic. Ground-warfare council: cruise is a harvester/landing-zone killer. Air council: a wing of drones can spoof the PD with cheap decoys.

### MISSILES-2 — Launcher Dispersion: TEL, Arsenal Ship, Boomer

**Pitch.** A silo is a gravestone with a lid. The Rocket Forces do not sit; we *drive*. Shoot-and-scoot, a missile on a truck, a submarine under the ice. The enemy hunts the pad, and the pad is gone.

**Player fantasy.** You play counterforce chess: the fleet's bunker-busters fall on an empty field, while your TELs have already relocated and your Boomer sits under the coastline, firing from the sea.

**Clerk's Annex**
- **Mechanic.** Three `strat: true` launchers in the missing slots CLAUDE.md promises: **TEL** (ground unit, `domain: 'ground'`, speed 12, hp 90, armor 4, IC 260, carries 4 loaded rounds; reload 20 s at any Factory/Depot; launch from any position; after launch is *exposed for 6 s then relocates automatically*), **Arsenal Ship** (`domain: 'naval'`, 16 cells, requires `shipyard`, IC 520), **Ballistic Submarine** (`domain: 'naval'`, submerged and invisible to enemy targeting until it launches, 8 SLBMs, IC 600). Each has a **magazine** instead of a cooldown, so each ripple click spends IC *per round*.
- **Science.** Real. Topol-M/Yars TELs shoot-and-scoot; Ohio/Borei SSBNs carry 16–24 tubes and hide in "bastions"; the Arsenal Ship was a real 1990s USN concept (500 VLS cells).
- **Tree.** TEL: unlocked by Fusion Age (`fusion`, L3505). Arsenal Ship: `pointdefense` + shipyard. Boomer: `spaceage` (L3506) + Uranium Enrichment (`uranenrich`, L3448). No `dang`.
- **Code grounding.** `UNIT_DEFS` L3184 (add `tel`, `arsenalship`, `boomer`; `arsenalship` art exists L3090); `silo` def L3407 (`strat: true` already read by `armSilo` L7660, `tickAutoStrikes` L7708 and the strike UI at ~L7010 via `s.d.strat`); `isMechanical` L4541 for cap treatment; `spawnBuster` L7485 target choice to prefer silos/TELs.
- **Build size.** L (≈600 lines: unit AI for relocate, naval submerge, magazine).
- **TM1.**
- **Tenet check.** Gives the player *more* ways to launch; no nerfs. Real cost: reload logistics + IC per round.
- **Interactions.** Naval council (the "barely fielded navy" gets a job); Intel council (hidden launchers vs enemy scan, `scanEnemyIntel`).

### MISSILES-3 — Hypersonic Glide Vehicles ("the Skip")

**Pitch.** At Mach 10 the enemy's lance has no time to *track*. A glider rides the top of the air like a skipped stone, then dives. Where the old missile gave the PD six seconds, this gives it one.

**Player fantasy.** The bombarder grid fires lances at an ordinary nuke and shoots it down at 90 hp. Your HGV skips in at Mach 12, jinks and goes through the net. The satisfaction of a missile that *did not die*.

**Clerk's Annex**
- **Mechanic.** New strike `hgv`: IC 70, `cd` 8 s, `taboo: false` (conventional kinetic warhead — HGVs have no explosive fill; they hit at ~3 km/s, as per real HTV-2/Avangard). Profile: boost to 2200 px/s → *glide* phase (flies along the 76% `STRATO_Y` band, small sinusoidal jinks) → terminal dive at 2600 px/s. **Exposure window** under PD ≈ 1.3 s vs ~5 s for `stratmis`, `hp` 150. Impact: single point of 160 damage + 90 px splash, plus an armour-piercing spike vs capital ships. Cannot hit airborne targets that are *not* slower than Mach 3 (it would miss, no seeker).
- **Science.** Real. DARPA HTV-2 (Mach 20 glide), Russian Avangard, Chinese DF-ZF; a skip-glide trajectory lowers the PD reaction time and compresses it into the plasma-sheath "blackout" interval. SCI-FI note: none; the in-game "skip" is a sinusoid of a few px.
- **Tree.** New node `hypersonics` "Hypersonic Glide": branch `space`, tier 3, `pre: ['spaceage', 'directedenergy']`, cost 320, time 28, not `dang`. Combo partner: `massdrivers` — mass-driver-launched HGV (range ×1.3).
- **Code grounding.** `STRAT` L6878; `launchStrike` L6943; `strikeEta` L6902 (add `hgv` profile case); projectile in `updProj` ≈ L9006 beside `stratmis`; enemy PD in `pdLanceHit` L8533 and `PD_SAT` L8529 (saturation budget unchanged, so the hgv makes PD *waste* lances).
- **Build size.** M (≈200 lines).
- **TM1.**
- **Tenet check.** New toy and a *better* answer to the enemy's PD wall; no cap.
- **Interactions.** Space council: Mass-Driver synergy. Enemy council: the flagship's `PD_SAT` budget is exactly the lever.

### MISSILES-4 — Real Guidance: Proportional Navigation and Seeker Packages

**Pitch.** Pure pursuit is a dog chasing a car. *Proportional navigation* is a dog that understands the car. Every air-defence officer from 1950 knows: command a turn rate proportional to the rate the line of sight rotates. Constant bearing, decreasing range: collision. That is all it is, and it is magic.

**Player fantasy.** SAMs, Falcon missiles and Aegis rounds actually hit jinking targets, and choosing a *seeker* becomes a choice: IR (cheap, flare-vulnerable), radar (ECM-vulnerable), home-on-jam (kills the `ejammer` hijackers).

**Clerk's Annex**
- **Mechanic.** (a) Replace `mslLeadPoint` (L4922) with true PN: `a_cmd = N · V_c · λ̇` with N = 4 (1 for the first 0.3 s of the pivot, matching `MSL_TURN_T`). (b) Add three **seeker packages** on SAM/Aegis/fighter missile defs: `ir` (hit-rate bonus, defeated by new enemy **flares**: gunship/bomber release a `flare` projectile every 3 s that diverts 60% of IR rounds for 1.5 s), `radar` (no flares, but `jammed(x,y)` L4970 zeroes it inside a jammer field), `arm` (home-on-jam: *prefers* `e.jammer` targets, never diverted). (c) Selecting a seeker is a free dropdown at the Missile Works / Avionics Works (L3435).
- **Science.** Real. PN with N ∈ [3,5] is the standard (Sidewinder onward, Patriot); flare divert is why Sidewinder grew dual-band seekers; AGM-88 HARM is the canonical home-on-jam.
- **Tree.** New node `seekertech` "Seeker Heads": branch `def`, tier 2, `pre: ['pointdefense', 'automation']`, cost 180, time 20, not `dang`. Combo: `shieldtech` (L3535): shield-piercing terminal seeker.
- **Code grounding.** `mslLeadPoint` L4922; missile constants L4736–L4768 (`FIGHTER_MSL_TURN`, `AMI_MSL_TURN`, `MSL_LEAD_TERMINAL`); homing integrator around L9100–L9145; `jammed` L4970; `ejammer` spawn L5604; flares on `spawnEnemy` kinds (bomber/gunship).
- **Build size.** M (≈300 lines).
- **TM1.**
- **Tenet check.** Strictly a buff to the player's missiles; the enemy gets *counters*, not the player a nerf.
- **Interactions.** Air council (Falcon missile loadouts), Enemy council (flares/ECM), EW council.

### MISSILES-5 — The Warhead Stockpile (magazine depth replaces the cooldown)

**Pitch.** The pad is a mouth; the *magazine* is the stomach. We stockpile, we feed. A real arsenal is not a button on a timer, it is a number: 120 warheads in the bunker, and a general deciding what each one is worth.

**Player fantasy.** You watch a stockpile counter climb all prep phase. The invasion lands. You are *rich in warheads*, and every launch is a deliberate spend. Your 850 IC/s finally has a place to go that is *not* a button.

**Clerk's Annex**
- **Mechanic.** New building **Munition Depot** (`cat: 'DEF'`, IC 300): converts 30 IC/s into warhead stock (conventional warhead 30 IC, nuclear 80 IC + needs `uranenrich`; stock stored in `G.warheads[type]`, capacity unlimited but each stored nuke imposes **upkeep 0.2 IC/s** and *fallout risk*: a destroyed Depot with 20+ nukes inside triggers `fallout` explosion, the existing mechanic). Silos lose `silocd`; the *launcher* has a salvo rate (silo: 1 per 2 s, TEL: 1 per 1 s). **Stability:** taboo strikes still cost stability, but **only a "first use per type"** pays the full cost; each *consecutive* use within 30 s costs 25% less, to a floor of 40% — there is *no cap* on how many you fire, they just fall into routine. The cost is paid in the fleet's reaction: it escalates (more `buster`s, canon: fear) after the 10th nuke.
- **Science.** Real. Cold-War strategic stockpiles peaked at ~70,000 warheads; storage cost, tritium half-life (12.3 y), and the "launch on warning" posture drove the arms race. Fallout of a depot hit: the Palomares/Thule aircraft-accident analogue.
- **Tree.** Attaches to `uranenrich` (building) and a new node `stockpiling` "Strategic Reserve": branch `def`, tier 2, `pre: ['pointdefense']`, cost 160, time 18, `dang: true`, `sc: 6` (hoarding unnerves the home front, recoverable unrest).
- **Code grounding.** `stratCost` L6888, `stratStab` L6890, `launchStrike` L6943 (`silo.silocd` L6950), `tickAutoStrikes` L7708, `BUILD_DEFS` L3381 (`fallout: true` precedent: `uranenrich` L3448), `STRAT` L6878, report stats `G.stats.strikes`.
- **Build size.** L (≈450 lines: stock state, UI row, depot, upkeep, tiering).
- **TM1.**
- **Tenet check.** Removes the cooldown limit (more fire), adds real cost (upkeep, fallout risk, fleet escalation), and never caps. The deliberate answer to "212k IC unspent".
- **Interactions.** Economy council (IC sink), Society (fleet psychology event tied to the 10th nuke: TM2 hook), Enemy (escalation response).

---

## 4. Hot Take

### MISSILES-X — STARFISH: The High-Altitude Burst

**Pitch.** On 9 July 1962 America detonated 1.4 megatons 400 km above Johnston Island. Streetlights failed in Hawaii, 1,400 km away. Within months seven satellites were dead, Telstar among them. **The fleet's whole advantage lives in orbit: the bombarder satellite grids that ate 42 of 44 Halos in report 7.** We will not out-shoot them; we will *blind the sky*.

**Player fantasy.** You pick the exosphere. You press the button. For eight seconds the sky is white, and every electronic thing up there, theirs *and yours*, goes dark. Then you flood the dead grid with your cruise wave. It is the most dangerous, most selfish, most beautiful button in the game, and it asks you what you are willing to lose.

**Clerk's Annex**
- **Mechanic.** New taboo strike `starfish` ("High-Altitude Burst"): IC 120, stab 20, `cd` 30 s, `bReq: 'uranenrich'`, `air: true` (only valid when aimed above `SPACE_Y`). On detonation it creates a **radiation zone** of radius 1,400 px (≈ one third of the map width) around the burst point for 25 s and a **permanent artificial belt** (−5% duration decay per 20 s, total 120 s). Inside: (a) **all electronic enemies** (`e.elec`/orbital hulls: bombarders, escorts, satellites, drones, `shieldgen`) lose weapons/PD for the zone's life (`e.pdBudget = 0`, `e.disabled = true`); (b) **all your own `isMechanical` units and satellites in the zone also go dark**, so the *real cost* is that you EMP your own drones, androids, `satlaser` Halos and Volleys (they recover; satellites that were *in the 1962 belt* simply degrade 50% for the duration); (c) cities lose `bDef` auto-defence for 6 s. Aim UI shows the zone; `strikeAimPoint` (L6908) already supports an airburst point.
- **Science.** Real. Operation Fishbowl: Starfish Prime (1.4 Mt, 400 km), Kingfish, Bluegill; X-rays and beta electrons trapped by the geomagnetic field created an artificial belt that killed Telstar 1, Ariel 1, TRAAC and 4 others. E1 EMP is from gamma-ray Compton electrons. Real physics, SCI-FI only in scale: the in-game zone is vastly more than the true ~1,400 km line-of-sight, which is a visibility convention for a 6,000 px strip.
- **Tree.** New node `highaltburst` "High-Altitude Burst": branch `space`, tier 3, `pre: ['spaceage', 'directedenergy']` plus `bReq: 'uranenrich'`, cost 380, time 30, **`dang: true`, `sc: 12`**. Combo partner: `terrainnav` (the *cruise wave* flies through the dead grid) and `hypersonics`: Starfish-then-HGV. The combo **changes the world**: while Starfish is active, a new **"Dark Sky" state** (a `society('machine')`-style tint) shows on the sky gradient.
- **Code grounding.** `STRAT` L6878; `launchStrike` L6943 (new `starfish` branch plus `taboo: true` for `stratStab` L6890); `strikeAimPoint` L6908 for the exosphere burst; blast handled like `splashDamage` L7546 with an EMP flag; `isMechanical` L4541 as the friendly-collateral selector; `PD_SAT` L8529 `pdBudget` zeroing; enemy PD gate L8554; orbital enemies in `spawnEnemy`/`spawnSuper` (L5269/L5581); bombarders are space-anchored.
- **Build size.** M (≈220 lines).
- **TM1.**
- **Tenet check.** It is a *new toy* with a real cost (your own machines die too), not a cap. It answers the report-7 problem (space fleet lost to the bombarder grid) by giving the player a way to *kill the grid* instead of nerfing anything.
- **Interactions.** Space council (space units mean something again, but the player has to protect their own through *hardening*: a new research option `radhard`, hardened satellites survive the zone), Enemy council (the grid is the target), EW council (EMP vs jammer fields), Society (taboo cost, fleet "fear" reaction: canon).

---

## 5. Alliances Sought

- **Space council.** The Starfish and the HGV only matter if there is a grid worth killing and satellites worth hardening. Pair my zone with their "space units must matter" ask.
- **Enemy / Fleet council.** My cruise and HGV bills need real PD (radar horizon, saturation budgets). Joint bill: the autofleet adapts to a missile-heavy player by fielding **flare dispensers** and **orbital PD**.
- **Naval council.** Arsenal Ship, Boomer: their navy "barely fielded" problem gets a *role* in my launcher bill.
- **Society / Economy council.** The warhead stockpile is the IC sink and the escalation lever; I want their "surplus-IC outflow by what you built" slice wired to the Munition Depot.
- **Air council** (a reluctant alliance). They own Falcons; I will give them proper PN missiles and seeker packages (MISSILES-4), and in return they stop drowning my countdown in aircraft.
