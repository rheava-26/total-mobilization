# BIOENGINEERING COUNCIL (slug: bio) — Round 1 Position Paper
*The Ministry of Biology. Chair: the Minister, who has not slept since the vats came online and regards this as a feature.*

## 1. Opening Speech

Honourable members of the Mobilization Parliament. We are the Ministry of Biology, and we have been told that "biology does nothing to a machine." We agree. That is the best news this house has heard.

The Armaments Council wants to out-build an autofleet with more steel. We propose to out-*live* it. Every rival ministry brings you something that wears out. We bring you something that grows back. Look at the Physarum slime mould: no brain, and it designed a rail net to match Tokyo's own engineers. Look at Deinococcus, which shrugs off five thousand grays of radiation. Look at the axolotl, which regrows a limb and keeps the bill for next week. Your army today is 232 dead infantry out of 251, and the Ministry of Finance calls that "chaff". We call it a compost heap with no plan.

We do not poison their drones. We build a civilisation that cannot be attritted: it repairs itself, learns the enemy's habits, farms the radiation it is dosed with, and, if the Parliament has the courage, grows a Titan that never stops growing. The Autofleet fears an industrialising Earth. Wait until it meets an industrialising *ecosystem*.

The Ministry of Space will say we are gross. We are. Please vote Aye.

## 2. The Gap

- **The bio branch has five nodes and three of them are "a new unit plus a percent."** Biotech, Cloning Vats, Transhumanism, Superhumans and Titans (`RESEARCH` L3521-3525) give +30%/+60% MP, +20% armor, one unit each and a society recolour. None of them changes the world. Combos only unlock units, which is the standing known flaw.
- **Bio is the only branch with no mechanic of its own.** Drones have lines, psi has networks, and bio has "×1.35 HP" from the Gene Facility (`bioQual`, L3447, applied at L4264). That is a stat sheet.
- **The infantry chaff problem is a bio problem.** 232 of 251 infantry died (report 7). Nothing in the game lets a body *come back*. `healSquad` (L5029) and the heal tick (L7866) heal hit points but never regrow dead bodies, although the squad system (`hitSquadMember` L5000) already tracks bodies one by one.
- **Strategic resources are barely used.** Uranium only discounts strikes and gates two buildings. Bio can give it a second life.
- **Taboo strikes (nuke/MIRV) cost stability.** Reports 6-7 hit 0% stability from 44-46 strikes. Today fallout is only a penalty. Bio can turn it into an asset without removing the stability price.
- **Verdant is a sky colour.** `society('verdant')` (L3549) repaints the gradient and moves one stability modifier. Canon says bio points inward (ROADMAP ~L821), and the inward half is not built.

## 3. Bills

All bills point inward, in keeping with the canon in ROADMAP L821: nothing here infects, poisons or "hurts" a machine fleet. We reinforce *us*.

---

### BIO-1 — THE HYPHAL NET (living infrastructure)

**Pitch (in character).** "Honourable members, your power grid is a spreadsheet. Ours is a fungus. Mycelium moves nutrients and warning signals between trees across a whole forest, and Physarum has already out-planned human rail engineers (Tero et al., 2010). We propose to give every building in the fortress a root system. When the enemy bunker-buster hits one bunker, forty others *feel it* and send help."

**Player fantasy.** You turn the whole fortress into one organism. Hit one wall and the whole wall pulls through.

**Clerk's Annex**
- **Mechanic.** New SPEC building `hyphalnode` (Hyphal Node, 90 IC, 20 MP, hp 160, build 6). Nodes link to any other node within 420 px, and form a net by chaining. Every building within 220 px of a connected node is "on the net". Net buildings get:
  1. Regeneration: 1.2%/s of maxHp, 2.4%/s for a building that has been hit for under 5 s;
  2. Damage sharing: 30% of damage on a net building is spread evenly across up to 6 others, if they are alive;
  3. Mycelial supply: net buildings with an ammo pool (Anti-Missile Interceptors L8383, missile silos) refill 20% faster.
  Severing a net node splits the net, and surviving fragments keep working on their own.
- **Real basis.** Mycorrhizal common networks (nutrient and defence-signal transfer); Physarum polycephalum transport network optimisation; Ecovative-style mycelium composites already used as a building material.
- **Real costs (no cap, per Art. III).** A net building takes +25% damage from fire effects (the net is flammable: Firestorm, plasma, `igniteFirestorm` L6567), so the enemy has a real way to answer. Nodes cost MP and ongoing IC upkeep of 0.4/s each, which is cheap at 5 nodes and a genuine line item at 100.
- **Research tree.** New node `mycelium` (branch bio, tier 2, pre `['biotech','nanotech']` combo, cost 140, `dang:false`). Combo partner: **nanotech**, because nanite-scaffolded growth gives the net fast construction. It unlocks `hyphalnode`. Combo effect that changes the world: the net also carries `auraHeal` (L3780) to ground units standing on net ground, so the *terrain itself* becomes a repair field.
- **Code grounding.** BUILD_DEFS L3381 (add def, model on `genefac` L3447); building regen tick L6425 (`G.regen` repair nanobots already exists, so this is a per-building flag); damage to buildings path (grep `damageBuilding`/`b.hp -=`); `auraHeal` L3780; `mkBuilding` L3670; art in the building draw table next to `genefac` L3135.
- **Build size.** M (~250 lines).
- **TM1.**
- **Tenet check.** Pure addition. The cost is a flammability weakness and upkeep, both real costs and not caps.
- **Interactions.** Defense council (shared HP pools make Aegis/Shield nodes more resilient); Underground/Rail council (the net runs under the Rail Hub; underground buildings join at depth); Munitions council (fire weapons become the counter, which opens a role for them).

---

### BIO-2 — BLASTEMA PROTOCOL (squads that regrow)

**Pitch.** "Infantry is not chaff. It is a *renewable resource*. The axolotl re-grows a heart. The planarian re-grows a head, and in some strains the head remembers (Shomrat & Levin, 2013). We have read a lot of planaria papers and propose that a squad that survives should not stay dead."

**Player fantasy.** You throw a squad at a push, it gets shredded to two bodies, and you watch it fill out again behind the line. Your infantry become a tide, not a trickle.

**Clerk's Annex**
- **Mechanic.** Bio-family units (`UNIT_FAM` L3375: shock, clone, cyborg, superhuman) living in a squad regrow dead bodies, one per 6 s, when not hit for 8 s. Each regrown body costs 1 MP (min 0, from the pool; if MP is 0, regrowth pauses). Upkeep is real. A squad can never regrow its last-dead body if the entire squad is dead. Nodes: Gene Facility count shortens the interval (6 s ÷ (1 + 0.15 × facilities)), uncapped.
- **Real basis.** Planarian neoblast stem cells; axolotl blastema formation; zebrafish heart regeneration; "bio-printed" tissue scaffolds.
- **Research tree.** New node `regeneration` (branch bio, tier 2, pre `['biotech']`, cost 120, `dang:false`). Combo: with **nanotech** → regen also works on `cyborg` and `shifter` (machine-bodied) because nanites supply the scaffold. Also supersedes `G.allRegen` (L7866), which is a global flag in Shapeshifters. That flag then becomes the capstone, "regen at 4.5%/s plus body regrowth".
- **Code grounding.** `healSquad` L5029; `hitSquadMember` L5000 (body tracking); `spawnUnit` L4253; heal tick L7866 (add a regrowth timer there); `UNIT_FAM` L3375.
- **Build size.** M (~180 lines). The squad body arrays already exist, so this is mostly a timer and a body-spawn.
- **TM1.**
- **Tenet check.** Pure buff. The MP cost is the logistics; nothing is capped.
- **Interactions.** Armor council (squads that regrow make cheap fodder into persistent holding forces); Naval/Air councils (aircraft squads do not regrow, so the gap remains in their favour); Psi council (the squad-as-hivemind fantasy is the same system).

---

### BIO-3 — RADIOTROPH GARDENS (fallout becomes farmland)

**Pitch.** "In 1991 we found black fungi growing *toward* the Chernobyl reactor. Melanin harvests ionising radiation the way a leaf harvests sunlight (Dadachova & Casadevall, 2007; *Cladosporium sphaerospermum* grows faster under radiation, and has been flown on the ISS). The Ministry proposes that a strike should leave you richer than it found you."

**Player fantasy.** You used to flinch before firing a nuke. Now your gardens glow, your biologicals heal in the glow, and your Uranium node finally means something.

**Clerk's Annex**
- **Mechanic.** New building `radiogarden` (Radiotroph Garden, 160 IC, 40 MP, `srReq:'uranium'`, hp 180). Fallout zones (from `igniteFirestorm` L6567-style fallout, Uranium Enrichment collapses, nuke/MIRV impacts) within 500 px of a garden become *yielding ground* for the duration of the fallout:
  1. +0.6 IC/s per active fallout tile-chunk (summed, no cap);
  2. bio-family units standing in it heal 2%/s, and are immune to fallout damage;
  3. the garden converts a share of "radiation" into **influence**: +0.15 inf/s per chunk, so influence finally has a source that isn't stability.
  Strikes still cost their full stability price (Article VI). The garden does not refund stability, it gives the player a reason to *aim* a strike at their own side of the line.
- **Real basis.** Radiotrophic fungi (melanised; *Cladosporium sphaerospermum*, *Cryptococcus neoformans*); *Deinococcus radiodurans* DNA repair; tardigrade Dsup protein (shielding DNA from radiation, Hashimoto et al., 2016).
- **Real costs.** The garden stops yielding when the fallout decays; fallout also hurts non-bio units and buildings in range. Gardens take stability penalties from taboo use as normal.
- **Research tree.** New node `radiotroph` (branch bio, tier 3, pre `['cloning']` + special-resource gate `srReq:'uranium'` as in `RESEARCH.voidengine`, cost 200, `dang:false`). Combo: with **antimatter**/**uranenrich** buildings, unlocking "Hot Gardens", which at ×2 yield also run near the Uranium Enrichment plant's own fallout flag (L3448). This gives Uranium the bonus role the designer asked for: a bonus, not a requirement, at least for the garden.
- **Code grounding.** `igniteFirestorm` (grep, called L6567), STRAT L6878 (nuke/MIRV at L6881-6882 flagged `taboo`), `SR` L3173, `stratAvail` L6893 (`srReq` already handled), BUILD_DEFS `uranenrich` L3448 (`fallout:true`), heal tick L7866.
- **Build size.** M (~200 lines). The fallout field needs one array `G.fieldCells` if fallout is not already stored as such (grep found none; may be per-fire objects, so a small change).
- **TM1.**
- **Tenet check.** Adds a use for something the player was already allowed to do. It does not remove the stability cost, so Art. IV decisions stay intact.
- **Interactions.** Strikes council (a new reason to fire taboo strikes defensively; they may want to rebalance stability costs, and we do not object); Economy council (a new IC/influence source tied to a resource node).

---

### BIO-4 — DIRECTED EVOLUTION (your army learns the fleet)

**Pitch.** "Frances Arnold won the 2018 Nobel Prize for evolving enzymes in a flask: mutate, select, repeat. In the lab it takes weeks. We have a war. Every drone your clone battalions kill makes the *next* clone battalion better at killing that drone."

**Player fantasy.** A long match feels like an arms race you are winning. The fleet escalates (`G.diff * (1 + invTime/70)`) and so do you.

**Clerk's Annex**
- **Mechanic.** A per-enemy-kind **adaptation** counter, `G.adapt[kind]`, grows by 1 for each enemy of that kind killed by a bio-family unit (kills by other units give 0.2). Every 25 adaptation points gives all bio-family units +4% damage vs that kind and −3% damage taken from that kind, uncapped (diminishing nothing; the enemy's own scaling is the pressure). Gene Facilities multiply the *rate* by 1 + 0.25 each. A small tooltip panel shows the top three adapted kinds ("ADAPTED: DRONE ×1.32").
  - **Enemy response (a scarier enemy, not a cap).** Above 100 adaptation to one kind, that kind starts to appear with a variant (e.g. `drone` gets a "mutated" `bombpod` mix) at +10% spawn weight. The fleet notices.
- **Real basis.** Directed evolution (Arnold, 1993-); population-level selection; "evolving" antibody affinity maturation in germinal centres.
- **Research tree.** New node `evolution` (branch bio, tier 3, pre `['cloning','regeneration']`, cost 260, `dang:true`, `sc:6`). Combo partner: **hivemind** (psi+nano) for "shared selection" (adaptation applies to *all* units); that is a world-changing combo, since it extends the bonus from bio-family to the whole army.
- **Code grounding.** `spawnEnemy` L5581 (kind weights), kill hook (grep `killEnemy`/`e.dead` in the damage path near L4972, where `t.lastHitT` is stamped), `gmod`/`bmod()` L3763 for multipliers, damage calculation path for armor.
- **Build size.** M (~220 lines).
- **TM1.**
- **Tenet check.** Buff plus a *scarier enemy* variant. No cap on the player's side.
- **Interactions.** Enemy/Fleet council (the variant spawns are theirs to design); Armaments (adaptation applies to bio-crew units only unless the hivemind combo is taken).

---

### BIO-5 — OVERGROWTH (a grown-wall strike that lifts stability)

**Pitch.** "The kudzu vine grows 30 cm a day, and we have bred worse. A mycelium brick is grown, not poured, and in Ecovative's ovens it sets in a week. Give us a silo and a wet afternoon, and the *terrain* will do the fighting. Machines walk on tracks and legs. A thicket of hardened mycelium is not a weapon, it is a Tuesday."

**Player fantasy.** You fire a strike that costs you nothing in stability, and the plain in front of your city turns into a forest in thirty seconds.

**Clerk's Annex**
- **Mechanic.** New strike `bloom` (Overgrowth). IC 50, stab **−2 (i.e. +2 recovered)**, cooldown 10, requires `verdant` society or the `mycelium` node. It plants a 600-px-wide thicket for 90 s: enemy ground units entering move 55% slower and have a 25% miss chance on targets behind it; friendly bio-family units heal 1.5%/s inside it; the thicket has an hp pool of 400/chunk (chewed through by enemy fire; fire burns it). Units and buildings are not damaged by it. It is terrain, not infection, so the "bio does not hurt machines" canon holds: a machine tangled in roots is slowed, not harmed.
- **Real basis.** Mycelium composite materials; kudzu and bamboo growth rates; engineered *Bacillus* biocement for ground stabilisation.
- **Real costs.** The thicket burns: enemy plasma and firestorm cancel it quickly and the plume spreads along the net (BIO-1). The strike takes a silo slot like every other strike and does not stack with itself in the same zone.
- **Research tree.** Unlocked by the existing `superhumans` Verdant society (L3549) **or** the new `mycelium` node. That makes Verdant a *playstyle*, not a recolour. World-changing combo: with **terrain edit** (RAISE) the player can sculpt a berm, grow it, and make a chokepoint.
- **Code grounding.** STRAT L6878-6893 (add entry, `stratAvail`), strike launch path (grep `armSilo`), terrain mode `TERRAIN_MODES`; enemy movement speed (grep `spd` in ground enemy update); `society()` L3549.
- **Build size.** M (~200 lines).
- **TM1.**
- **Tenet check.** Gives the player another toy; a stability-positive strike is exactly the pressure valve the strike-heavy reports (6-7) lacked.
- **Interactions.** Strikes council (a stability-neutral option); Defense council (a chokepoint-builder for bunkers); Terrain council (sculpt + grow).

---

## 4. HOT TAKE

### BIO-X — THE IMMORTAL LINE (a Titan that never stops growing)

**Speech.** "Honourable members: the Hayflick limit is a rumour. Telomerase, the naked mole-rat's cancer resistance, the immortal jellyfish *Turritopsis dohrnii* reverting to a juvenile — we have the pieces of a body that does not stop. We propose a Titan you do not *buy*. You *plant* it. A larva, 60 IC. It eats the dead (your own fallen bio-units become biomass), it eats MP, and it grows. Not to 1,300 HP and a cap. It grows until something kills it. The enemy gets exactly one answer: bring something bigger."

**Player fantasy.** You have a Titan that was a puppy at minute 5 and a mountain at minute 30. The fleet wants it dead, and everything they bring to kill it is a better *story*.

**Clerk's Annex**
- **Mechanic.** New unit `larva` (Brood Titan Larva, 60 IC, 6 MP, hp 120). It matures through stages as `biomass` accumulates (each stage multiplies hp, dmg, size): Larva → Titan (existing `titan` stats, L3294) at biomass 40 → **Elder Titan** at 200 → **Ancient** at 800 → and beyond, with `maxHp = base × (1 + 0.02 × biomass)`, **no cap**. Biomass comes from (a) friendly bio-unit deaths within 300 px (1 each), (b) 0.2/s per MP fed (an MP upkeep of 0.3/s per stage: the real cost), (c) kills (0.5 each). Heat is the stated weakness: a growing body must dump heat, so armor falls 1 per stage unless sheltered by a Hyphal Net (BIO-1).
  - **Enemy response (scarier, not capped).** The Fleet reads big signatures: for each Elder+ in play, the enemy focus-fires it with a Bulwark/Annihilator at +1 per Elder, and `spawnSuper` (L5269) may select the Leviathan to bear on it. The Immortal Line generates its own boss fights.
  - **Projects fit.** A **doctrine you fall into** (a Titan that hits Ancient stage declares "THE IMMORTAL LINE" and unlocks Titan brood-chaining) and a **megaproject** option: the Brood Pit (build in sections), which seeds larvae in a protected cell.
- **Real basis.** Telomerase and the Hayflick limit; HeLa cell lines (immortal in culture); naked mole-rat cancer resistance via high-molecular-weight hyaluronan; *Turritopsis dohrnii* transdifferentiation. **SCI-FI:** a *whole* body, not a cell line, sustaining unlimited growth with cooling. Internal logic: the makers' fleet is machine, and the Titan is the one thing it has no counter-doctrine against; that is exactly why the fleet is afraid of an industrialising Earth, and canon already holds that bio rewrites our forces.
- **Research tree.** `immortal` (branch bio, tier 4, pre `['titans','evolution']`, cost 520, `dang:true`, `sc:12`). Combo: **cloning × titans × regeneration** (3-way) is the "Immortal Line" doctrine. Gate: needs a `cloningfac` standing (`bReq`).
- **Code grounding.** `UNIT_DEFS` titan L3294; `UNIT_FAM` titan L3375 (add `bio`, already present); `spawnUnit` L4253; unit stat scaling via `mkUnit`-style code near L4264 (`bioQual` multiplier); `spawnSuper` L5269 / `spawnEnemy` L5581 (priority tuning); `wt: 6` weight field for squad physics.
- **Build size.** L (~450 lines) with the stage visuals; the baseline growth loop is M.
- **TM1.**
- **Tenet check.** The *opposite* of a cap: unlimited growth with real costs (MP upkeep, heat/armor penalty, fleet focus-fire).
- **Interactions.** Armaments (Titan weapon variants per stage); Enemy council (the scarier counter-unit); Projects council (a megaproject hook); Society council (upkeep feeds the "Verdant" strain).

---

## 5. Alliances Sought

- **Defense / Shield Council.** BIO-1's shared HP net and their Aegis/Shield towers make a fortress that heals; we trade them regeneration, they trade us a dome for the larvae.
- **Munitions / Strikes Council.** BIO-3 gives their taboo strikes a purpose and BIO-5 gives them a stability-neutral option. We want to share the stability ledger.
- **Armor / Land Council.** BIO-2 makes their ground infantry persistent, so tanks finally have something to protect.
- **Nanotech / Automation Council.** The `mycelium` combo and the cyborg/shifter regeneration need their scaffolds. Machine and fungus are, in fact, quite compatible.
- **Psi Council.** BIO-4's hivemind combo makes adaptation global. They want the "one mind, many bodies" fantasy and we have the bodies.
- **Enemy/Fleet Council.** We *need* them to be scared of what we build, and to answer with a variant. We would rather be hunted than ignored.
