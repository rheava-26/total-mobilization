# NEW PLAYER COUNCIL — Second Reading (Round Four)
*Clock Bloc: Pacing, New Player, Game Feel. File slug: `new-player`.*

## 1. Speech

Mr Speaker, the Address says the game is not meant to be complex: click a button, build units, let volume do the work. My ministry has said the same thing for two sessions, and said it with a clipboard. I welcome the One-Button Law as the first honest description of my curriculum.

I am pleased the President loves the fear meter and wants to see it. A meter nobody can read is a rumour. The Autofleet has filed the Alarm Panel; I will supply the sentence a civilian reads beside it, and I promise it will sound like a compliment, because it is one.

I accept the Inspector-General's audit in advance. If any of my bills asks a recruit to watch, click or remember something per unit, strike it. My bills show things. They gate nothing, cap nothing and ask for nothing.

And to the build menu: forty-odd buttons in a flat row is not a menu, it is a rummage. Let us file them.

## 2. The Re-reading

**NEW-PLAYER-1, The Order of Battle — AMEND.** Already folded into THE ONE FORECAST (Pacing's coalition bill). Amendment: it becomes a renderer only, and the Alarm Panel (AUTOFLEET-6) sits beside it on the same strip, so one glance reads the fleet's size, its plan and its mood. One-Button: pure display, no input. Line: the plan strip is a time axis and the manifest is a list, so nothing depends on depth.

**NEW-PLAYER-2, The Reach Curtain — KEEP.** The sky tint is drawn only while placing or selecting. The altitude bands are the game's real extra dimension, so the Line law is its home ground. One-Button: no input beyond what the player is already doing (placing or selecting). The Alt-hover reach tags are optional, and the default is the tint alone. No babysitting needed.

**NEW-PLAYER-3, The Ledger — KEEP.** Display only. Add a line for the Alarm Panel's three contributions so the fleet's growth is itemised like any other number. One-Button pass. Line pass (map-wide scalars).

**NEW-PLAYER-4, The Because Chip — KEEP.** The chip reports a refusal and offers one optional "show me" button. It never acts for the player, and the Plan panel version shows the same chips. One-Button pass: it reduces attention. Line pass.

**NEW-PLAYER-5, The Post-Mortem — AMEND.** Add the row AUTOFLEET-6 specifies, "Fear paid vs power gained", so a loss can be read as "you built less than the fleet expected", not "building was a mistake". One-Button: a report. Line: n/a.

**NEW-PLAYER-6, The Exercise — WITHDRAW** (already merged into LORE-4 in Round 2; I confirm). It lives on as the coalition bill below.

**Coalition bills I co-sponsor**
- **THE ONE FORECAST — AMEND.** Tier 0 stays at a fixed ±40% baseline and gets no false lines, as I amended in Round 3. One-Button pass. Line pass.
- **THE EXERCISE — AMEND.** One drill wave, with a lesson banner. Hands-off: the wave is a scripted wave the army fights by itself, so nothing is micromanaged. Veterans opt out in Options.
- **SHOW YOUR CAUSE — AMEND.** The refusal chip is separate from the shake scale, and Safe Mode still shows the chip.
- **THE WIRE AS HOME FRONT — AMEND.** The Information Policy dial is a single standing choice, made once, with Managed as the default. That passes the One-Button Law.

## 3. The Order Paper

### Item 1 — The docket, read from the first hour
The Speaker named four First Session bills at risk of micromanagement: Ammunition & the One Locker, The Trunk, Dispersal and Power Grid. I speak for the recruit on all four with one rule: **each must be fully automatic by default, and each must report its state through a chip, never a toast.** I will cosign any of them that adds that chip. A bill that needs the player to route, aim or allocate on turn one gets my opposition.

### Item 9 — NEW-PLAYER-7: THE FLEET FOLLOWS YOUR FACTORIES
*Pitch.* "A fleet that grows when you grow is not punishing you. It is flattering you. Let the meter say so in your own language."

**The one-line explanation (hover on the ALARM needle):**
> The fleet sizes itself to your industry. Doubling your economy sends about 9% more ships. It doubles what you can field.

**The lesson card** (fires once, the first time Alarm leaves Calm; title "THE FLEET WATCHES"):
> Building raises the Alarm. Each doubling of your economy brings about 9% more ships and about twice the guns. Keep building.

**Presentation rules.** The ALARM bar fills **gold, not red**, and turns red only in the Desperate band. The Alarm is never listed as a gap in the readiness panel. The Post-Mortem row reads "Fear paid vs power gained".

**Clerk's Annex**
- **Mechanic and numbers.** Text and colour only on top of AUTOFLEET-6, whose figures it quotes: `F = clamp(0.5 × mean(log2(icRate/150), log2(bldgTier/60), log2(armyWeight/100)), 0, 4)`, about +9% enemy power per doubling of the whole economy, about 11:1 in the player's favour. The numbers in the text are computed from the live formula, not hard-coded, so a rebalance cannot make the sentence lie.
- **Code grounding.** `LESSONS` L11525 (add one entry with `when: (G,T) => G.fear >= 0.5`), `fireLesson` L11549, `syncHUD` (~L11183) for the hover, `data-tip` pattern at the STAB cell L811.
- **Size.** S (~40 lines).
- **TM1.**
- **Research tree.** None. It names the Dispersal Doctrine node in the tooltip when that node exists.
- **Tenet (Amendment I).** Information only. Nothing is nerfed or buffed.
- **One-Button.** Zero input; one automatic card, shown once.
- **Line.** Map-wide scalar.

### Item 8 — NEW-PLAYER-8: THE SUB-TABS
*Pitch.* "Twenty-two defences in one row is a haystack. Give the recruit drawers: a sky drawer, a ground drawer, a drawer for the heavy things."

**Mechanic.** A second row of chips under the category tabs, built from a new optional `sub` field on `BUILD_DEFS`. The first chip is ALL and is the default on first visit; the last chosen chip is remembered per tab. A chip shows a count badge of buildings you can afford right now. Nothing is hidden: ALL always shows everything. In Streamlined mode (`hideUnimportant`) the sub-chips are revealed with their parent tab.

| Tab | Sub-tab | Buildings |
|---|---|---|
| PROD (9) | GROUND | factory, barracks, autofactory |
| | AIR | airbase, helidepot, skydock |
| | SEA | shipyard, grandarsenal |
| | SPACE | spaceport |
| DEF (22) | SKY (6) | ciws, samsite, aegisbattery, interceptor, flaknest, microbattery |
| | GROUND (5) | bunker, mgnest, artnest, plasmaturret, naniteturret |
| | HEAVY (4) | lasertower, particlecannon, siegegun, silo |
| | SUPPORT (7) | radar, shieldgen, ammodepot, logistics, fieldhospital, waracademy, nanitefoundry |
| INFRA (13) | GROUND WORKS (4) | heavyfoundry, plateworks, rotaryworks, plasmabastion |
| | SKY WORKS (3) | aeroworks, avionicsworks, missileworks |
| | GARRISON (4) | garrfactory, garrstock, fortresshq, vethall |
| | TRANSPORT (2) | railhub, mobhub |
| SPEC (21) | ALLIES (5) | alliedaadepot, alliedairfoundry, alliedarmory, heavyport, milbase |
| | LIFE & MIND (3) | psionicrelay, cloningfac, genefac |
| | MACHINE (3) | droneassembly, datacenter, droidfoundry |
| | RESOURCE & ENERGY (6) | deepore, uranenrich, mantleforge, munitionfac, antimatterex, superconductor |
| | SKY & ORBIT (4) | spacestation, tractorbeam, airwing, massrail |

ECON (7) and TECH (1) stay flat. DEF 6+5+4+7 = 22. INFRA 4+3+4+2 = 13. SPEC 5+3+3+6+4 = 21. The new Deep Bloc seats (Subterranean Works, Undersea Works) get their own sub-tabs, UNDERGROUND and SEA FLOOR, from the same field, so the structure has room for them.

**Clerk's Annex**
- **Code grounding.** `ALL_CATS` and tab rendering L10940–10947, the card loop `for (const key in BUILD_DEFS)` L10964 (filter by `d.sub`), `curBcat` L10917, `tutRevealTick` for the reveal. The 22/21/13 counts come from `cat:` greps of `BUILD_DEFS`. Verify that mgnest, artnest and flaknest (the President's DEF count includes them; their defs sit near L4559) are in the buildable list before assigning.
- **Size.** S–M (~90 lines of UI plus one `sub:` field per building).
- **TM1.**
- **Research tree.** None. Locked cards still show with their `req`.
- **Tenet (Amendment I).** Pure navigation. Nothing denied or hidden.
- **One-Button.** The sub-tab is optional navigation with an ALL default. It adds no required clicks.
- **Line.** n/a (UI). Sub-tabs follow the vertical axis the Address names (air, sea, ground, underground).

## 4. Blatantly overpowered today

Nothing in my portfolio. The candidates are the T-20s "+150 seconds" and "Emergency units" (L11818–11842). Both fire once per match (`T.readyShown`) and both mark the run `ach.altered`, so they are neither repeatable nor free of cost to the medal. They do not make losing impossible. No nerf proposed.

## 5. Struck self-limits

I struck none in the First Session. The one limit I did not take was Science's "25% of lines wrong at low confidence", which I confined to tier 1 and up as a kindness to the first hour, not a nerf. I do not ask for it to be restored at tier 0: no evidence shows that a tier-0 forecast can be exploited.
