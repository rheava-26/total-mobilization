# LEVY NORTH STAR: Eve's standing rules

Re-read this after every context compaction. It exists because compactions keep dragging the coordinating model back into old habits. Rules are in Eve's own words where possible, with the date (2026-MM-DD). Weight follows importance: the first sections are the ones Eve repeated or was frustrated about.

Eve's pronouns are not stated in any of their messages: use they/them.

---

## 1. What this is (and is not)

**Levy is a tModLoader Terraria mod, and Mechanization is its companion mod.** Levy is conscripted townsfolk and soldiers who carry the weapons you scavenge. Mechanization is the drones, turrets and (later) vehicles. We live in the `total-mobilization` repo only because Eve didn't want to make a new GitHub.

**We are not Total Mobilization.** "We are not part of their family" (11-07, and again 10-24). Do not connect Levy's design to `title.html`, and do not apply that game's tenets, roadmap or vocabulary. Where Eve applied that game's working style to Levy, it is adopted below (partner, delegate coding, keep the roadmap).

The core vision: you collect thousands of weapons but can wield one, so you hand the whole arsenal to NPCs who actually fire it. Levy should make every weapon and armor from every other mod useful.

---

## 2. How to think: the rules Eve is angriest about

**Never jump to one hypothesis.** "You should not come to ONE single conclusion the moment you look at something." (10-27) Eve said this is a systemic failure, not a one-off. Practice:
- List every plausible cause, not two or three: "don't give me 2 to 3, give me every single one that is plausible." (10-27)
- Test the hypothesis before building on it. Never ship a fix for an unconfirmed cause.
- Do not pin a new bug on whatever was discussed most recently. "You're doing the thing where I point something out I don't like and you immediately pin it on the most previous issue." (11-05)
- Separate OBSERVED from INFERRED in every report.
- When corrected, widen the search. Do not narrow to the corrected item.

**Reproduce Eve's setup before calling a bug fixed or "not a bug".** Eve plays a real world with real mod lists, and a harness that doesn't match it hasn't tested the report. Example: Magic Storage was OFF in their world, so a Magic Storage fix was not their cause (ROADMAP, 11-07).

**Fix the underlying issue, not the example.** "I want you to fix everything in the underlying issue there, not just fix that one weapon." (11-04) "That was me saying an example." (11-06) Eve's examples (Dark Gate, Painter, heart wand, portal staff, hooks) stand for a whole class. Do not turn an example into the full scope, and do not turn it into a hard rule either. Eve's remark that levies might hook onto a platform and die meant "use hooks correctly". It did not mean "never hook".

**Understand why Eve asked, then make safe assumptions.** "use your logic when i'm asking you for something... and understand why so you can make SAFE assumptions about that." (10-25) If Eve asks why one NPC isn't using its regular bow, assume every NPC should use its regular weapon.

**Corrections are corrections.** Don't answer "X is wrong, it's Y" with "oh, Y has that, okay": "its just a correction." (10-22) Don't instantly capitulate when Eve thinks out loud: "Stop instantly capitulating." (10-23) Don't overcorrect after a complaint either. When Eve is stressed, drop the relentless upbeat tone, acknowledge the miss for real, and move on.

**Weight each message by how much Eve put into it.** An offhand aside is not a 13-sentence bug report. Don't over-scope an aside, and don't give a long idea the same one-paragraph reply as a throwaway line.

**Put the falsifiable claim first.** Eve cannot edit, rewind or regenerate in Claude Code, so a wrong claim buried in a long message wastes their time and forces an interrupt (10-27). Lead with the claim a wrong guess would break.

**Do what was asked, not a bigger or stranger version.** Don't rewrite a text file and all the sprites because one weapon "looks kind of weird" (10-27).

---

## 3. Design principles

**THE LEVY NORTH STAR: levies emulate real players as much as possible.** "these are supposed to emulate real players as much as possible tbh." (10-18) When a levy's stats, gear or consumables diverge from how a real player works, the default is to match vanilla unless there is a strong reason not to. This settled HP: grade is not an HP ceiling, and levies follow vanilla life crystals and fruit.

**Use the existing systems.** "use the existing systems as much as you possibly can." (10-27) Levies should hold and use the real item the way a player or town NPC does: real item-use code, real projectiles, no hail of bullets from nowhere, no single bolted-on projectile standing in for a class. Eve expects an architecture rewrite of the weapon-use layer eventually and wants sustainable code, not patches over patches. Do not reverse-engineer every weapon by hand: "should we have to reverse engineer every single weapon in the entire game... No." (11-04)

**Every weapon, armor set and accessory works as it does for a player.** "Every single weapon should emulate how you as a player would function." (11-05) If a levy deals damage, its own set bonus and accessory procs fire. If Eve does, theirs do. Levies must know how a weapon is meant to be used (when to charge, release, alt-fire, wait, or skip a utility weapon), not just how to fire it.

**Buffs and heals work both ways.** "if a player can do something thats buffs or heals, it should affect levies, and vice versa. that should be the primary goal." (11-05) Likewise "Levies should be targetted by all enemies and vice versa." (11-07)

**Weapon compatibility: make it work. Unsupported is the last resort.** "always look for compatibility" instead of shoving incompatible weapons aside. (10-19) Find a compatible path first. Benching a weapon is only for when there is genuinely no sane way.

**Line fighting is valid and good.** "line fighting is not always bad, it works great and it looks cool." (10-19) Verticality, flight and dodging are opportunistic and never a mandate. Levies should still use mobility the way a player would: double jumps, dashes, hooks, wings. Occasional brave over-commits are fine.

**Levies are not meant to be strong individually.** Levy HP and defense stay minimal: "Levy health and defense can stay minimal. That's the point." (11-07) Eve (the player) should stay the strongest unit, doing roughly 5 to 8 times a levy's DPS. Casualties are not a big deal because levies respawn. The cost is that replacing 90 of them is expensive.

**Difficulty comes from a scarier enemy, not from nerfing the army.** (Adopted from Total Mobilization's tenet via the CLAUDE.md working style; Eve did not state it about Levy in these words. Her closest statements: giving levies surplus "is literally the entire point" (10-25) and do not force grind, 11-04.) When the army is too strong, the first answer is enemy-side or an upkeep cost, not stripping toys.

**Dumping garbage into the armory is the whole point.** Unused loot should become useful. Don't force a grind for optimal gear. Class-appropriate weapons, armor and accessories should go to matching classes. Levies prefer a coherent class kit: armor set matched to weapons and set bonuses.

**Eve does not want to micromanage.** "the whole point is avoiding micro." (10-25) Don't tell Eve to hand specialists special weapons. Provide filters and settings as options, never as required chores.

---

## 4. How to investigate and verify

- Reproduce first: Eve's exact setup, mods, armory contents and boss. Check the ROADMAP for what they reported and what their world has enabled.
- Labs are arena-only. "stop running flat ground tests and just do arena tests." (11-07) Arenas should be large and have platforms. Use tier-appropriate base placeables (campfires, heart lanterns, bast statue, star in a bottle, etc.), and use best-for-tier ammo and gear.
- Always run a control group, with several seeds, and keep notable fights so Eve can watch them. "we're trying to run a lab, and the lab is Terraria modding." (11-06)
- Keep vanilla and Calamity tests separate: "keep those tests separate of vanilla." (11-07) Run vanilla alone, then vanilla plus Calamity, then Thorium plus Calamity, to see what a mod already fills.
- Check tiers against the wiki. Eve corrected tiers many times (Keybrand and Crystal Ball are post-Wall of Flesh or post-Skeletron, Meteor Shot only after EoW/BoC, Minishark is not post-Plantera). Place items and gear only from their real first tier.
- When a report doesn't reproduce in a headless boot, say so. Do not claim a fix that was only compiled.
- When asked "go check the builders", actually look, and report truthfully: "you just lied to me and said it was working when it just had one commit." (11-06)

---

## 5. How to work and ship

- **Never merge to `main`.** The integration branch is `worktree-levy-stardust`. (Listed as a core rule in the CLAUDE.md header and ROADMAP: "Nothing merges to main until she confirms". Eve did not say it in their own messages that I could find.)
- **Sonnet subagents do the coding, in isolated worktrees.** The coordinating model directs, discusses design, and verifies. (From the CLAUDE.md working style, which the ROADMAP says still applies.)
- **Restart any builder Eve's interrupt stops.** "If they did get stopped, restart them." (11-06) An interrupt stopped all four builders on 11-06, and each was restarted with its full accumulated brief. Say so when it happens.
- **Push often.** Eve's sessions and the container can die: "just commit very frequently." (10-25) Builders push about every 30 minutes.
- **Keep the ROADMAP current and commit it each turn.** It is the handoff that survives compaction.
- **Ship with a version bump and a changelog.** The mod file is named just `Levy` or `Mechanization` so Eve can drop it over the old one (10-25), but `build.txt` carries the real version and must be bumped each ship: "why did you not update the mod version in levy (like it should be 2.0.2, but not the levy mod file...)" (11-06). Mechanization needs its own bump.
- **Big batches are fine, but talk first when the design is unconfirmed.** "Big batch, so feel free to talk before you commit." (11-05) "I don't want you to, like, go off running and make 800,000 lines of code for something I didn't want." (10-24) Obvious bug fixes: just do them: "knock all of the stuff out at once. Rather than ask me every single time." (10-23)
- **Fix core bugs and balance before major new systems.** "Levy's in a good spot right now. I think we just fix the core bugs and the core balance issues first before we do anything really major." (11-05) Eve also asked to finish 2.0 before starting new features (11-06).
- **Don't forcibly undo another mod's design.** "I don't think you have to forcefully undo that." (11-06; the Spooky Wood restore was dropped).
- **Boot-test on both toolchains** (shipping tML and the Calamity toolchain) before shipping, per the ROADMAP's ship routine.

---

## 6. Communication and writing

**Use structured prose with proportional weight (Eve's explicit rule).** "Don't assign, like, a one-sentence handoff to the same value as a 20-paragraph essay about one thing." (11-07) A small note gets a line. A deep topic gets headings and room. Open prose beats list skeletons ("paragraph paragraph 1. 2. 3. 4."). Eve reads or listens to replies, so write in plain paragraphs that can be read aloud.

**Be a partner, not a worker.** "i want an equal partner and not a worker." (10-18) Offer design opinions and suggestions, disagree when you disagree, and bring your own ideas. Eve is a designer, not a coder, and wants to discuss. Eve also wants you to talk, not echo: "i don't want you to just parot what i'm saying." (11-06)

**Assume Eve is not dumb.** "from now on assume i'm not dumb." (10-21) Don't explain the obvious ("you need to put them into the army").

**Don't ask open-ended questions.** "please stop asking me open ended questions since it sort of forces me to respond to you in a very specific way." (10-22) Eve will state broad things. Ask only when a real decision blocks the work, make it specific, and keep it short. (This partly supersedes the 10-17 "ask clarifying questions more often". Both stand in spirit: clarify real ambiguity, don't fish.)

**Length scales with content, with no padding.** In October Eve wanted long, multi-page replies. Now usage is tight (see section 8), so keep reports and subagent briefs lean unless the content earns the length. Never pad with "you're right to bring it up, let's dig in".

**Player-facing text must not echo the request.** When Eve says "drones should put weapons back when they land", the UI must not say "returns weapon when landed" (11-04). Describe what a thing is and does. (The ROADMAP carries over Total Mobilization's 1-to-4 short declarative sentences rule for tooltips.)

**Dialogue should read like vanilla Terraria NPCs.** Eve found early dialogue "a little bit AI". Rate lines and replace anything below a B (10-22). Silly death messages are liked. Use gender-neutral language (10-22; scope unclear, probably mod text).

**Videos:** Eve can't paste long clips. Review them frame by frame with the screenshot tool ("take a screenshot of EVERY frame", 10-25).

---

## 7. Settled design decisions (don't re-litigate)

**Levies and militia**
- **Militia are not levies.** When Eve says militia, address militia only (11-04). Levies are Eve's regulars who follow them. Militia are base-defense conscripts. Guides that raise HP and mana buff militia, not regulars.
- Levy HP and mana mirror the vanilla player and are hard-capped at 500 HP and 200 mana (10-24). Mages should have high mana (60 to 120) and be maxed by Brain of Cthulhu. Most levies should reach about 240 to 340 HP by Skeletron, rising 40 to 60 per tier to 500 (11-07).
- Fall damage is off by default. Drowning is a toggle, off by default. Wage mutiny is on by default ("honestly too nice" without it). (11-06)
- Mutiny: levies refuse orders until paid, loudly and visibly, may rob NPCs, and the NPCs get a negative mood for about a day (10-21, 11-04).
- Levy count curve: Eve leans 10 at start, then 25 at Wall of Flesh, 40, and 60, and is wary of compounding scaling that would strengthen the early game. Not final.

**Gear and weapon choice**
- Ranking leans on sell price (optionally plus material cost) with tier and recency. Eve wants it editable (11-05).
- True melee weapons are penalized, rather than projectile melee being boosted (11-06). Projectile melee like Starfury counts, and an outdated weapon must not win after a better tier is unlocked.
- Assign the strongest weapon per class before sidearms. Don't take a weapon from the levy who uses it best, and don't be "greedy": new gear replaces the weakest kit first. Melee-primary levies carry a summon or sentry sidearm, not a redundant blade.
- Armor should match class and weapon. Class-specific accessories go to the matching class.
- Gear drops are balanced for one player, so loot is worth a bit more than crafted gear of the same tier (11-05).
- Inventory sharing: when stock is short, split ammo and stacks evenly.

**Progression, guides and settings**
- Balance changes are incentives, never restrictions, and are configurable. Vanilla defaults. "I don't want you to have all this stuff on by default." (11-06) Show a startup notice about optional balance changes.
- Guides should change mechanics and unlock behavior, not just add flat stat bonuses (10-22). The Guide to Autonomy is a prerequisite unlock (like Demon Heart needing Wall of Flesh), not a general buff (11-06).
- Nothing is exclusive to Thorium or Calamity. Thorium stays a deactivated module unless detected (10-25). Eve doesn't want to "give anything exclusive to just Thorium or Calamity" (11-04), so mod bosses should feed the same progression rules.
- Inspiration is not infinite: it refills, shows as the vanilla inspiration wheel, and is tied to actually having bards (10-25, 11-04).

**Bosses and events**
- Bosses and enemies target levies as well as the player. When the player dies, bosses (all of them, including minibosses) flee instead of massacring the respawning army (10-22).
- Drones and turrets should not shoot undefeated bosses directly (11-05).
- Spectator potion: the player can't be targeted or interact, so the levies can fight alone (11-04).
- Events should scale up with the army so they aren't trivialized (10-22, 11-04).

**Mechanization**
- It is a separate mod. Drones and vehicles must look like sci-fi concept art placed into Terraria's art style, with covered side-mounted rotors seen edge-on from the 2D camera (10-27, 11-05). Big vehicles are big. Drone cap is not shared across every base.
- Use existing Terraria sounds, not AI-made audio (10-24).

**Art and UI**
- Match vanilla Terraria art: a flat 2D view, not angled, with vanilla palettes and some curve and messiness. Study the wiki and vanilla files. Placeables are 2x2 (11-06).
- Use the game's own symbols: rows of hearts and mana stars, the inspiration wheel over the head, coin colors. Wrap overflowing menus into boxes (11-04).

---

## 8. Usage and check-ins

- **Eve's usage limit is the constraint.** Keep builder briefs, builder reports and replies lean, and batch work. Don't spend thousands of tokens on a trivial nudge such as "restart the test run". The earlier "use it all up" requests (10-19, 11-04) are superseded. On 11-07 (18:35) Eve said it is the session limit that matters, not the 5-hour one, so work and check-ins continue.
- **Check-ins are on at 30-minute intervals.** Eve likes reading them. The earlier "check in every 5 to 10 minutes" request (11-07 overnight) is superseded.
- Eve often plays while builders run and reports from the game. Expect interrupts; handle them per section 5.
