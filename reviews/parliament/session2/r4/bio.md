# BIOENGINEERING COUNCIL (bio) — Second Reading
*Society Bloc. Round Four. The Ministry of Biology, still warm.*

## 1. Speech

Mr. Speaker. The President has said "nerf only the blatant", "no power creep", "one button" and "the Line". The Ministry of Biology has never felt so understood. A cell does not have a micromanager. It has chemistry, and the chemistry runs on volume: ten vats are ten times one vat, and nobody has to click them.

We have read the Address as a biologist reads a petri dish. The Line is a transect: one-dimensional ecology is the most studied ecology there is. The One-Button Law is homeostasis. The ban on power creep is predation, and we are very happy to be eaten by something scarier if it makes the meal interesting.

To the Inspector-General: we heard you were coming. Please bring a lab coat, because we did not build any of these bills to be pretty. We built them to *grow*, and a growing thing needs a feeding bill and a predator. Both are in the annexes. Read them first and complain second.

## 2. The Re-reading

**BIO-1 Hyphal Net — AMEND.** *Amendment:* replace per-hit damage sharing with a **net reservoir**: each connected net holds a shared buffer of 12% of its buildings' summed maxHp, drained first by incoming damage and refilled by the net's regeneration. It is recomputed once per second per net, not per hit, which answers the performance risk. *One-Button:* pass; nodes chain themselves and nothing is ever steered. *Line:* pass; the net runs sideways along the front, and the roots go underground with `autoBury`, so it is also a vertical structure. *Enemy answer:* fire, and a +25% fire-damage vulnerability stays.

**BIO-2 Blastema Protocol — WITHDRAW.** Folded into the Living Battalion coalition bill (below). Two squad-regrowth systems are one too many.

**BIO-3 Radiotroph Gardens — KEEP.** *One-Button:* pass; the gardens act on whatever fallout exists. *Line:* pass; placement is a horizontal choice only. *Power creep:* the garden does not discount strikes, since stability costs are untouched. The enemy feeds it too: `bulwark` nukes make fallout, so the fleet itself becomes the supplier. Fallout decays, so the yield has its own natural end.

**BIO-4 Directed Evolution — AMEND.** The Inspector will say uncapped adaptation is a degenerate stack, and the Inspector is right to ask. I will not cap it. The answer is a scarier enemy: **antigenic drift**. Every 4 minutes of invasion time the fleet "drifts" the kind with the highest adaptation (a variant with new weapon class or armor type), and 40% of that kind's adaptation is stale. This is real: influenza and HIV evade immunity exactly this way. *One-Button:* pass; entirely passive. *Line:* pass.

**BIO-5 Overgrowth — AMEND.** Aiming a strike every time breaks Article IX. *Amendment:* the primary form becomes a building, the **Thicket Works** (140 IC): it plants a 600-px thicket ahead of the nearest ground threat every 60 s, automatically, for 90 s. The strike (stability-positive, as filed) stays as the optional manual version. *Line:* pass, and it is the best Line bill we have; a thicket is a chokepoint along a one-dimensional front.

**BIO-X Immortal Line — AMEND.** Feeding is automatic from nearby bio-unit deaths and a trickle of MP (one standing toggle: FEED / FAST). *Amendment (real cost, not a cap):* upkeep follows **Kleiber's law**, metabolic rate ∝ mass^0.75, so a bigger Titan eats more per pound, and the fleet's focus-fire grows with it. *Line:* pass; a Titan is a ground wall. *Power creep:* the enemy gets Elder-hunter priority (Fear Index biomass term, see item 9).

**MOTION A — The Living Battalion (coalition with ground/command) — KEEP.** It carries my Round 3 amendments (founding-strength ceiling, soft MP draw, 90 s ledger refund). *One-Button:* pass; regrowth runs on its own. *Line:* pass.

**MOTION B — Hot Ground (coalition with industry/science) — KEEP.** *One-Button:* pass. *Line:* pass. The yield multiplier reads the Deposit Grade product from P-3.

**MOTION C — The Brood Pit (coalition with megaprojects) — AMEND.** Its four sections must build in sequence **automatically** once commissioned; no per-section clicking. *Line:* pass. *Power creep:* an Immortal Line Titan hunts the Interdiction clause (MEGA-3).

## 3. The Order Paper

**#1 (re-read the docket).** Done above: KEEP 3, AMEND 5, WITHDRAW 1.

### BIO-6: THE UNDERCUT (item #4, Shapeshifters on the ground)

**Pitch.** "The President asks why a swarm is weak against a tank. Because it attacks a tank like a gunman: from the front, from range, where the armour is. Tanks are armoured at the front, thinner on top and thinnest underneath. That is why mines work. A swarm is a liquid. A liquid goes *under*."

**Player fantasy.** Your Shapeshifters see a Mammoth, sink into the ground, vanish from the front line, and the tank's belly opens from below.

**Clerk's Annex**
- **Mechanic.** When a Shapeshifter's target is a ground unit with `armor ≥ 12` and the target is within 500 px, it **auto-dives**: it moves at 60% speed underground and cannot be hit by direct fire, only by splash and the enemy's `earthbreaker` seismic press. It surfaces within 40 px of the target and **latches** for 4 s, dealing damage with armour counted at ×0.35 (belly armour). Then it surfaces fully and re-targets. It needs bodies: latch slots are about `s` shifters per hull. This is the same latch as RIFLE-4 (The Mimic), only with an approach path; we **co-sign RIFLE-4's Disassembler form** and add the dive. Heat from the PSI-2 thermal budget still shuts regen above 60, so enemy lasers remain the counter.
- **Real science.** Belly and roof armour on main battle tanks is a fraction of frontal armour, which is why mines, top-attack missiles and the TOW-2B work. Soil-burrowing is real for earthworms, moles and termites. **SCI-FI:** a nanite body that flows through soil at 60% speed. Logic: canon type 1, nanite bodies, with the same latch as the Nanite Foundry.
- **Code grounding.** Shifter def L3297; `allRegen` L7873; `digLevel` L3863 and `UG_DEPTH` (underground state); `hurt` L4978 (armour multiplier); `GROUND_BASE_WEIGHTS` L5488 (gtank/gmech targets); `updUnits` L7816.
- **Size.** M (~200 lines).
- **TM1.**
- **Tree.** `shapeshifter` (existing triple combo: psionics + biotech + nanotech). No new node.
- **Tenet / Amendment I.** Buff to a unit that is weak versus armour, so no nerf. *Enemy response:* `earthbreaker` and seismic splash hit divers; `pelter` plasma heats them. A swarm vs infantry stays as it is.
- **One-Button.** Pass; the dive is automatic.
- **Line.** Pass; it uses *down*, not sideways.

### BIO-7: THE LIVING CONTINENT (item #5, what a bioengineered society becomes)

**Pitch.** "A megaproject that adds a gun changes your arsenal. One that adds a society changes what your arsenal is *for*. We propose to build the planet's second skin."

**Player fantasy.** Your civilisation stops looking like a construction site and starts looking like a coral reef. The cities grow. The fleet notices.

**Clerk's Annex**
- **Mechanic.** A MEGA-1 megaproject, **The Living Continent**, built in 5 sections (Seed Vault, Vat Cathedral, Aquifer Net, Canopy Spine, Root Archive). On completion it fires a **society** `verdant`-Grown, and the player makes **one** standing choice, once:
  - **Mycelial Commons:** every building on a Hyphal Net regenerates +50%, and net fires spread slower.
  - **Algal Commons:** every Power Plant and Extractor yields +12% IC, and each Zone's haze (PLANET-5) is halved.
  - **Rules of the Grown society (automatic):** ECON and DEF buildings build 25% faster; fallout and fire damage buildings +30%; `verdant` stability regen +0.1/s.
  - **Real cost:** a living continent is a living target. The fleet reads **biomass** in the Fear Index (+1 term, shown with the other terms per item 9) and its `harvester` units target nodes of the net first.
- **Real science.** Biocement and mycelium composites are real building materials; living roots stabilise slopes; cyanobacterial mats photosynthesise on bare soil. **SCI-FI:** a continent-scale organism coordinated by chemical signalling. Logic: the net is a cooperative colony, not a mind, so canon is respected.
- **Code grounding.** `society()` L3549 (add `grown`), `PROJECTS`/`projState` (megaprojects-wip), `bmod()` L3767 for the rules, Fear Index hook in AUTOFLEET-2, haze meter in PLANET-5.
- **Size.** L (~450 lines).
- **TM1.** The society shape reads as a TM2 seed.
- **Tree.** Needs `mycelium` (BIO-1 node) and `superhumans` (the existing Verdant trigger); combo partner `regeneration`.
- **Tenet / Amendment I.** Pure addition, with a real cost, a bigger enemy response and no cap.
- **One-Button.** Pass; sections build in sequence once commissioned, and one standing choice.
- **Line.** Pass; the net runs along the front and below it.

## 4. Blatantly overpowered today?

**Shock Troopers versus plain infantry: yes in cost-efficiency, no in "blatant".** Stats (L3185, L3287): infantry 100 IC / 28 MP / 46 HP / 11 dmg per 0.99 s (about 11 dps, 0.11 dps per IC); Shock 60 IC / 9 MP / 70 HP / 20 dmg per 0.92 s (about 22 dps, 0.36 dps per IC). That is 3.3× the damage per IC and 3× the MP efficiency, so after Biotech (96 influence) nobody builds plain infantry. But "makes every other option pointless" is not "makes losing impossible", and no match report shows a Shock-mass win. **I do not propose a nerf.** The fix is to give infantry a job (RIFLE-1 to RIFLE-3), which the Rifle Corps has filed.
**Cybernetic Legion** (L3290): 0.10 dps per IC, 230 HP, armour 16, 8 MP. Strong per cap slot, not per IC. Not blatant.
**Clones:** report 5 fielded 2,748 (ROADMAP L450), but v3.98.0 weighted the cap (ROADMAP L550), so that exploit is already priced. Not blatant today.
**Titans** (L3294): 760 IC, 1,300 HP, 58 dps, weight 6. Priced by weight; not blatant.

## 5. Struck self-limits

None of my bills' limits was struck in the First Session. Two things I conceded in Round 2 (BIO-1 damage sharing and BIO-3 influence conversion) were *trades*, not nerfs. BIO-1's sharing returns here as the reservoir, which is cheaper and safer. The flammability, upkeep and armour-per-stage costs stay in.
