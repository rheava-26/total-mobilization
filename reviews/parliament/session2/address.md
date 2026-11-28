# The Second Session: the President's Address to the House

*Read with the Constitution (`../constitution.md`), which still binds. This Address amends it where it says so. Everything here is the President's own direction, given after reading the docket (`../docket.md`).*

## Why the House is recalled
The President read the First Session and loved it. The President also doubts it solved the core problems of the game, and has named the reasons below. The Speaker agrees on one point in particular.

**Nobody in the First Session had the job of saying "this is too strong" or "this is too fiddly."** The only challenge procedure tested whether a bill was a nerf, so the House drifted toward power creep:
- the Railgun Directorate struck its own wear penalty as "a nerf in a costume";
- the Long Gun ended with no section limit;
- the Psi damage slump was struck.

That gets fixed now.

## Amendment I: the tenet, as the President actually means it (Article III is clarified, not repealed)
The game still says YES to the player's power fantasy, and difficulty still comes from an overwhelming enemy. But in the President's words: *"I don't want to say no nerfing. I want to say it's really, really… a good reason to nerf this if it's completely blatantly overpowered."*

The precedent is psionics, which the President nerfed because "you literally could not lose with them."

- **A nerf is constitutional only when the thing is blatantly overpowered, and the bill proves it.** The proof is evidence from the match reports or the code that it makes losing impossible or makes every other option pointless. "It is strong" is not enough.
- **No power creep.** *"I don't want to just do power creep where we make everything super strong, so the enemy just blows up all your units who are not buffed in two seconds."*
  - A bill that buffs the player must say what the enemy does about it.
  - A bill that buffs the enemy must not make the player's existing army worthless.
  - A self-limit a council struck as "a nerf in a costume" may be restored, if it is the only thing stopping a blatant exploit.
- The preferred answer to "too strong" is still a scarier enemy or a real cost. A nerf is the last resort, and it must be explicit.

## Amendment II: the One-Button Law (new Article IX)
*"This game is not designed to be super complex. The whole point is that you're supposed to just click a button and then you build units. Everything's really supposed to scale with volume. Build as many refineries as you want, print out thousands more units."* And: *"the whole point is that you're not supposed to be micromanaging everything all at once."*

- **No bill may require attention per unit, per building or per shot to work.** A bill must work when the player only builds things.
- Anything the player *can* steer must be optional, automatic by default, or a single standing choice made once.
- Systems must **scale with volume.** Ten refineries should feel like ten times one refinery, not like ten chores.
- Every First Session bill must be re-read against this law. The Speaker's own read:
  - Bills 1–5 pass.
  - Ammunition & the One Locker, The Trunk, Dispersal and Power Grid are at risk unless they run themselves.

## Amendment III: the Line (new Article X)
**The ground is a line.** The world is a side-on strip, so a ground unit can only move left or right. There is no depth and no flank in the ordinary sense; units stack and overlap on one front.

- Ground bills must work on a one-dimensional front.
- The real extra dimensions of this game are **vertical**: underground, the surface, the sea and the sea floor, then the air bands, then space. A bill that wants depth should look up or down, not sideways.

## The Order Paper: what the President wants answered
1. **Re-read the docket** against Amendments I–III. Each sponsor keeps, amends or withdraws each of its bills, and says why.
2. **Heat becomes variety.** The President wants the heat idea reshaped into a reason to mix your army: *"huge damage bonuses with really high HP enemies, so you're forced to get new things. If you get lasers, that can help, and you don't just want to spam one type of unit."*
   - The goal is that monoculture hits a wall made by the enemy, never a cap made by the rules.
   - One type still works. It just works slowly, while a mix shreds.
3. **The Line.** How do tanks, infantry and artillery each earn their place on a one-dimensional front?
4. **The Shapeshifters on the ground.** *"How do they work if they're on the ground? Shapeshifting is really strong when you're fighting someone also on the ground, but it's not very strong when you're fighting someone with a tank, which is the problem."* Answer it.
5. **More megaprojects, and how they shape society.** A megaproject should change what your civilization *is*, not just add a gun.
6. **The underground.** *"There should also be an underground builder, because the underground is super underutilized for how important it is in the original book it's based off of."*
   - The game already has a buried layer (`UG_DEPTH`, `autoBury`, rail tunnels, underground AA via `ugFire`). The ROADMAP banks a "Continental Redoubt: underground cities, glassing-proof" doctrine.
   - Design the underground as a real place you build in.
7. **Underwater buildings.** *"Underwater buildings would be cool too."* The sea is 71% of the planet and the last report fielded three ships.
8. **More buildings, more defensive stations, more balancing.**
9. **The Fear Index, made visible and explained.** The President loves it: *"I love the fear meter. I wish you could have a way to see it, though. And if a player builds, they're kind of punished for it, and I want to see an explanation for that."*
   - Show it on screen.
   - Explain to the player why building makes the fleet bigger.
   - Make sure building more is still always the winning move.
10. **Name what is blatantly overpowered today.** Give evidence from the match reports and the code. Under Amendment I, you may now propose a nerf for it, or a scarier enemy answer.

## New seats and new structure
**Blocs and caucuses** (*"mini coalitions inside coalitions"*). Councils now sit in blocs, and big portfolios split into caucuses:
- **The Ground Bloc:** the Ground Staff (bloc whip) with four new caucuses:
  - the Armour Corps (tanks and super-heavies);
  - the Rifle Corps (infantry and exotic infantry);
  - the Artillery Corps (guns, SPG, MLRS);
  - the Fortifications Corps (defensive stations and emplacements).
- **The Deep Bloc:** two new seats, plus Naval and Planet.
  - **Subterranean Works:** the underground builder.
  - **Undersea Works:** sea-floor buildings.
- **The Sky Bloc:** Air, Space Fleet, Missiles.
- **The Arsenal Bloc:** Kinetic Arms, Railgun, Energy, Physics.
- **The Economy Bloc:** Market, Command, Industry, Megaprojects.
- **The Society Bloc:** Stability, Science, Lore, Psi, Bio.
- **The Clock Bloc:** Pacing, New Player, Game Feel.
- **The Observer Delegation:** the Autofleet.

**The Inspectors-General.** Every bloc has one sitting inside it, adversarial by nature. The Inspector belongs to the party but serves the game.
- Its job is to say *"this is my bloc's bill, and here is how it breaks the game"*:
  - blatant overpowering, exploits and degenerate stacks;
  - monoculture;
  - micromanagement;
  - power creep;
  - things that cannot work on the Line;
  - performance risk.
- Inspectors file reports against their own bloc's bills. Sponsors must answer them on the floor at Question Time.
- An Inspector never proposes its own bills, so it has no stake.

## Rounds of the Second Session
- **Round Four, the Second Reading.**
  - Each sitting council re-reads its bills under the amendments and takes up the Order Paper items in its portfolio. Writes `session2/r4/<slug>.md`.
  - New seats file maiden position papers to the same folder.
  - Inspectors audit their bloc's First Session bills and the live code, and write `session2/inspectors/<bloc>.md`.
- **Round Five, Question Time and Caucus.**
  - Sponsors answer the Inspectors' charges.
  - Each bloc caucuses and agrees its bloc bills: the coalition inside the coalition.
  - Councils vote again: 10 votes, at most 3 per bill, none to their own.
  - Councils file motions to other blocs.
- **Round Six, Coalitions.** Cross-bloc motions are carried and answered.
- **The Speaker** issues a revised docket and the Second Chronicle.

## Paper format (all rounds)
- **In character first**, then the out-of-character **Clerk's Annex**: mechanic and numbers, code grounding by grep, build size, TM1 or TM2, the tenet check *under Amendment I*, the One-Button check, and the Line check.
- Real science first, SCI-FI labelled. Research-tree attachment for every unit or tech bill, as before.
- **Length:** Round Four papers stay under about 1,800 words.
- **Never edit any file except your own paper.**
