# MISSILES & STRATEGIC STRIKES COUNCIL — Round 3: Coalitions

## 1. Answers (motions naming `missiles`)

**THE SILENT BOOMER (naval, to Missiles and Air) — AMEND.**
Co-sign with one amendment: every sea launcher (Boomer 8 rounds, Tomahawk boat 6, Arsenal Ship 16) draws its rounds from the shared warhead stock (MISSILES-5 / INDUSTRY-1 `munitions`) at the per-round IC price, and reloads at a Shipyard in range of a Depot. Otherwise a sub is a free magazine and the IC sink vanishes. Everything else (depth band, `revealT` 3 s on firing, sonar aura) stands as filed. I withdraw my Boomer and Arsenal Ship; the TEL stays with me as a land launcher.

**DARK SKY, KNIFE BELT (physics, to Missiles and Space-Fleet) — ACCEPT.**
As written: Starfish at IC 120 / stab 20 taboo, one `G.debris[]` record, `detonateNuke` density split (blast / thermal / EMP), `radhard` node. One note for the Clerk: the density split must leave the ground nuke's current 720 damage and radius unchanged, so the new modes are options and not a trade-off.

**DEAD SKY PROTOCOL (space-fleet, to Missiles and Physics) — AMEND.**
Co-sign the Burst / Lane / Burn ladder and the shared debris belt, with these amendments:
1. The Burn rung (Kessler Doctrine) stays strictly **opt-in**: the player presses it, pays the +12% stability cost, and sees the confirm bar. Nothing forces your lift offline.
2. The 25% friendly-fire term is cancelled by `radhard` on the player's side. The fleet answers by being scarier (+40% PD budget), never by capping our satellites.
3. The Burst rung is the Starfish of Physics' motion, not a second strike: one `starfish` entry in `STRAT`.

## 2. Joins (not addressed to me, co-signed anyway)

- **FIRE DIRECTION NET (ground, to kinetic-arms and space-fleet).** Add one front-end: silos, TELs and cruise ripples take their launch delays from the same `solveToT` solver, using `strikeEta` L6902 for each round's flight time.
- **ONE NET (space-fleet, to Air and Kinetic-Arms).** Same engagement net; strategic launchers join it, and SYNC FIRE drains `pdBudget` (L8620) exactly as filed.
- **FIRE FOR EFFECT, SUPPLIED (kinetic-arms, to Ground and Industry).** Supplied fire bonus applies to warhead stock as well, one IC pool.
- **THE STANDING PLAN (command, to industry and market).** Let a ledger line name "keep N warheads in stock", so the Depot is fed by standing orders rather than by clicks.

## 3. Competing motions

- **High-altitude package:** three versions are on the table (mine "DARK SKY", physics "DARK SKY, KNIFE BELT", space-fleet "DEAD SKY PROTOCOL"). I back **DEAD SKY PROTOCOL** as the container (it has the three-rung ladder and the shared belt) with Physics' density-split nuke as its Burst rung. It is one debris object, one strike entry, one research chain (`hemp` -> `retrograde` -> `orbitalflak`), and it is the only version that gives the Kessler Doctrine a place. Drop my own as redundant.
- **Time-on-target:** four versions (mine, ground, kinetic-arms, space-fleet ONE NET). I back **ground's FIRE DIRECTION NET** for the solver and **space-fleet's ONE NET** for the object that holds it. One `solveToT`, one net, front-ends for guns, orbit and missiles.
- **Launcher dispersion:** back **THE SILENT BOOMER** as the sea half. My TEL goes in as a land half; no separate motion needed.
- **Forecast / rail / standing-orders families:** I take no side except to ask that whichever ledger wins can hold warhead stock lines.

## 4. Closing statement

Comrades, we came here with six bills and a countdown. We leave with one sky, one net and one ledger. I withdraw my submarine and my Starfish in favour of yours, because a launch is only ever as good as the people who stand beside it. Build the depots. Fill the magazines. Light the dark sky when the time comes, and light it on your terms. The count begins at ten.
