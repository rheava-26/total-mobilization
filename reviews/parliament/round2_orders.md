# Order Paper — Round 2: The Debate

*The Speaker calls the House to order. All 23 position papers are tabled in `reviews/parliament/r1/`: air, autofleet, bio, command, energy, game-feel, ground, industry, kinetic-arms, lore, market, megaprojects, missiles, naval, new-player, pacing, physics, planet, psi, railgun, science, space-fleet, stability.*

## Your task
1. **Read the bills.** Read every paper's bills and Clerk's Annexes. You may skim opening speeches. Read closely every bill that touches your portfolio.
2. **Write your Round 2 paper** to `reviews/parliament/r2/<your-slug>.md`. Keep it under ~1,500 words, with these sections:
   1. **Floor speech.** In character, ~200 words. Praise allies, savage rivals, defend your own bills against the attacks you expect.
   2. **Positions.** For every bill from OTHER councils that touches your portfolio, give SUPPORT, OPPOSE or AMEND, with a one-line reason. For AMEND, give the exact amendment.
   3. **Ballot.** A table of exactly **10 votes**. At most **3** go to any one bill, and **none** to your own council's bills. Vote for what would make the GAME best, not just your portfolio. The Speaker tallies these.
   4. **Motions to co-sponsor.** Up to **3**. Each is addressed to one or two named councils. It proposes a JOINT bill that merges overlapping ideas, with a short title, the merged mechanic, and one Clerk's Annex (numbers, code hooks, build size, TM1/TM2, tenet check). The Clerk carries each motion to the councils named. It only becomes a coalition bill if they accept.
   5. **Trades.** Which of your own bills you would withdraw or shrink so a coalition bill can pass.
   6. **Constitutional challenges.** Any bill you believe violates Article III (the tenet: never cap, nerf or deny the player's toys) or Article IV (standing decisions). Cite the article and the reason.

## Overlaps the Speaker has noticed (merge them; don't fight over them)
- **Heat / thermal load.** energy (ENERGY-1), physics (PHYSICS-2), psi (PSI-2 thermal budget), energy (ENERGY-4 re-entry sheath) and physics (PHYSICS-1 peak heating).
- **Kessler / debris / high-altitude EMP.** space-fleet (SPACE-4, SPACE-X), missiles (MISSILES-X Starfish), physics (PHYSICS-3, PHYSICS-4), market (MARKET-6) and stability.
- **The superheavy gun.** railgun (RAILGUN-2, RAILGUN-X), megaprojects (MEGA-2, MEGA-H) and kinetic-arms (KINETIC-X supergun).
- **Rail networks under the continent.** industry (INDUSTRY-4), command (COMMAND-2), planet (PLANET-1) and railgun (RAILGUN-X).
- **Forecast and intel on the incoming fleet.** market (MARKET-2), pacing (PACING-2), new-player (NEW-PLAYER-1), lore (LORE-5) and science (SCIENCE-2 Deep Survey).
- **The fleet reacting to the player, and its fear.** autofleet (AUTOFLEET-1/2/3/4), lore (LORE-2 Fleet Resolve), pacing (PACING-HOT, PACING-1), stability (STABILITY-2 Maskirovka) and air (AIR-3).
- **Salvage and wrecks.** industry (INDUSTRY-H), science (SCIENCE-1) and lore (LORE-3).
- **Auto-build and replenishment.** command (COMMAND-1), industry (INDUSTRY-3) and market (MARKET-1).
- **Ionospheric heating.** physics (PHYSICS-5) and science (SCIENCE-5).
- **Orion.** space-fleet (SPACE-5) and megaprojects (MEGA-4).

## Points of order raised during Round 1 (address them if relevant)
- planet: `capLevels` silently caps terrain edits at 220. Is this a tenet violation?
- lore: `dangCap` (L4708) silently limits dangerous research. The event engine exists but is dormant (`EVENTS_LIVE=false`).
- missiles: only taboo strikes cost stability now, so CLAUDE.md is stale on that point.
- pacing: PACING-HOT (the more you build, the sooner the fleet arrives) is flagged by its own sponsor as a tenet knife-edge. Rule on it.
- psi: the pool-versus-throughput ruling on psi scarcity, and whether Shapeshifters stay immortal.

Do not edit any file except your own Round 2 paper. When done, reply with:
- your top 3 vote allocations;
- your motions as one line each, naming who each is addressed to;
- any constitutional challenge, in one line.
