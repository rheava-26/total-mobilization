# BIOENGINEERING COUNCIL (bio) — Round 2 Paper

## 1. Floor Speech
Honourable members. We have read twenty-two papers, and the Ministry of Biology notes with gratitude that every one of you has discovered, independently, that things die and then stay dead. The Ground Council wants a replacement pipeline. The Stability Council wants to count the bodies. The Command Council wants to overwork the factories until they snap. We want you to try something modest: a body that comes back.

To the Ground Council, whose Battalion is a handsome piece of work: your draft office is a fine thing, and it runs on *money*. Ours runs on cells, and we will share the budget. To the Science Academy, whose Doctrines have our votes: a combo that rewrites a rule is the only sort of combo worth the ink, and we have four of them ready. To the Energy and Psi councils, who have discovered that shapeshifters overheat: so does every living thing, welcome to biology.

To the Munitions rivals who will say "the vats are slow, the vats are squishy": the Hyphal Net is flammable, and we put that in our own bill. A weakness the enemy can exploit is not a flaw, it is a *plot*. And to anyone who calls the Immortal Line "unbalanced": it has an upkeep, a heat penalty and an enemy that hunts it. We do not cap fantasies in this house. We feed them.

## 2. Positions (bills from other councils touching bio)

| Bill | Position | One-line reason / amendment |
|---|---|---|
| GROUND-1 Battalion | **AMEND** | Keep IC-only drafts. Add: bio-family battalions (shock, clone, cyborg, superhuman) also regrow bodies at the Barracks-free rate of 1 body per 6 s when unhit for 8 s, drawing 1 MP per body (army-cap formula untouched, Art. IV.1). This replaces BIO-2's squad code. |
| STABILITY-1 Casualty Ledger | **AMEND** | Add: a body regrown within 90 s of its death refunds its `G.dead` count. Regrowth is how the player pays the ledger down, a real answer to chaff. Exemption list stays per `isMechanical` L4541. |
| COMMAND-3 Shock Brigades | **SUPPORT** | Biotech "stimulant" combo fine at -30% strain. Amend: stimulants cost 0.5 MP/s per shocked factory, so the pharmacy has a bill. |
| PSI-2 Thermal Budget | **SUPPORT** | Shapeshifters are the bio+nano capstone; heat is a clean weakness. Amend: Hyphal Net (BIO-1) vents 25% faster, so bio-net ground helps shapeshifters. |
| ENERGY-1 Thermal Load | **SUPPORT** | One shared `heat` field for every council. Bio units get a bonus vent rate from `Radiotroph Gardens` only if the field is cool. |
| SCIENCE-2 Synthesis Doctrines | **AMEND** | Add a fourth launch doctrine, **SYMBIOSIS** (`pre: ['regeneration','automation']`, cost 240): every building on a Hyphal Net also heals units standing within 200 px (auraHeal L3780). Doctrine tick is per-second as proposed. |
| INDUSTRY-2 Deposit Cascades | **SUPPORT** | Uranium tiers 3+ are what makes a Radiotroph Garden worth planting. We want the tier ladder to unlock a garden yield multiplier too. |
| PLANET-5 Zoning / Haze | **AMEND** | Radiotroph Gardens and Hyphal Nodes subtract 0.05 haze each (green belts), so a bio-industrial Zone is cleaner than a steel Zone. |
| MEGA-1 Ledger of Projects | **SUPPORT** | Our Brood Pit and "Immortal Line" doctrine fit the two tiers exactly. |
| AUTOFLEET-2 Fear Index | **SUPPORT** | A scarier enemy is the right answer. Amend: the Fear Index reads *biomass* (Immortal Line growth) as a separate term, so the fleet hunts what we grow. |
| PHYSICS-4 / SPACE-4 (debris) | **OPPOSE-lite** | Out of portfolio. No position beyond: our fallout garden does not make debris safe, it only pays for dirty strikes. |

## 3. Ballot (exactly 10 votes)

| # | Bill voted for | Votes | Why it makes the GAME best |
|---|---|---|---|
| 1 | MEGA-1 Ledger of Projects | 2 | Two-tier projects are the chassis for half of Parliament. |
| 2 | SCIENCE-2 Synthesis Doctrines | 2 | Fixes the "combos only unlock a unit" flaw in one framework. |
| 3 | GROUND-1 Battalion | 2 | Mass and identity, and a real sink for the 212k idle IC. |
| 4 | ENERGY-1 Thermal Load | 1 | The President's heat seed, with combined arms. |
| 5 | AUTOFLEET-2 Fear Index | 1 | Scarier enemy, not capped player; hoarding stops being safe. |
| 6 | INDUSTRY-2 Deposit Cascades | 1 | Finally makes strategic resources matter. |
| 7 | MARKET-1 Clearing House | 1 | Surplus outflow without a "spend 20,000 IC" button. |

Total: 10. No bill above 3; no vote for a bio bill.

## 4. Motions to Co-sponsor

**MOTION A — "THE LIVING BATTALION" (to: ground).** Merges BIO-2 (Blastema Protocol) into GROUND-1.
*Merged mechanic.* A Battalion of bio-family squads regrows bodies for MP (1 per 6 s, unhit 8 s); IC drafts remain for non-bio battalions. Gene Facilities shorten the interval: `6 ÷ (1 + 0.15 × facilities)`.
*Clerk's Annex.* Code hooks: `healSquad` L5029, `hitSquadMember` L5000, `spawnSquad` L4091, heal tick L7866, `UNIT_FAM` L3375. Numbers: 1 MP per regrown body, 6 s interval, 8 s no-hit window, 0.15 per facility. Size: M added to GROUND-1's L (~150 lines extra). TM1. Tenet: pure buff; MP cost is logistics, nothing capped. Interactions: Stability (ledger refund), Command (drafts).

**MOTION B — "HOT GROUND" (to: industry, science).** Merges BIO-3 (Radiotroph Gardens) with INDUSTRY-2 (Uranium tiers) and SCIENCE-2 (doctrine).
*Merged mechanic.* A Uranium Extractor at tier 3+ grants the Radiotroph Garden its yield multiplier ×(1 + 0.25 × tier), and completing both `radiotroph` and the Uranium cascade fires the doctrine **FALLOUT HUSBANDRY**: fallout chunks within 500 px of a garden yield IC and influence and heal bio units; taboo strikes still cost their full stability.
*Clerk's Annex.* Hooks: `SR` L3173, `srMult` L3810, `igniteFirestorm` (called L6567), STRAT L6878 (nuke/MIRV `taboo`), `finishResearch` L4703 for the doctrine. Numbers: +0.6 IC/s and +0.15 inf/s per active fallout chunk, ×(1 + 0.25 × tier). Size: M (~220 lines) on top of INDUSTRY-2 and SCIENCE-2 frameworks. TM1. Tenet: adds a reason to use a taboo strike without discounting it. Interactions: Strikes (stability ledger unchanged), Planet (haze offset).

**MOTION C — "THE BROOD PIT" (to: megaprojects).** Turns BIO-X (Immortal Line) into a MEGA-1 megaproject.
*Merged mechanic.* A multi-stage Brood Pit (4 sections: Vat Hall, Biomass Silo, Cooling Spine, Seed Chamber) plants Titan larvae with the BIO-X growth rules. Reaching Ancient stage on any Titan fires the tier-1 doctrine "THE IMMORTAL LINE". MEGA-3 Interdiction applies: the fleet reads the Pit and comes to kill it.
*Clerk's Annex.* Hooks: `PROJECTS`/`projState` from `megaprojects-wip`, `spawnUnit` L4253, `spawnSuper` L5269 (focus-fire), UNIT_DEFS titan L3294. Numbers: Pit 4 sections, ~900 IC each; larva 60 IC; growth rules unchanged from BIO-X. Size: L (~500 lines incl. visuals). TM1. Tenet: unlimited growth with MP upkeep, an armor-per-stage heat penalty, and a hunting enemy. Interactions: Autofleet (Fear Index biomass term), Energy (cooling spine reads heat).

## 5. Trades
- **BIO-2 is withdrawn** in favour of MOTION A. We would rather have the code in the Battalion than two competing squad systems.
- **BIO-3 shrinks** from a stand-alone building and node to the Radiotroph Garden building plus the doctrine in MOTION B, tied to Uranium tiers; we drop the separate influence conversion if Industry objects.
- **BIO-1 stays small.** If the Defense Council runs shared-HP, we drop the damage-sharing clause and keep regeneration and supply.
- **BIO-5 (Overgrowth)** yields first if the Terrain Council builds a thicket mechanic of its own.

## 6. Constitutional Challenges
None of the other bills is void under Article III as written. One watch-item, offered as a warning and not a challenge: **PLANET-5(c)** haze lowers the stability cap per Zone; its sponsor calls this a cost and it is, but an uncapped Zone count with a per-Zone ceiling penalty must be held to Article III's "real cost, not a hard cap" test. No Article IV violation found.
