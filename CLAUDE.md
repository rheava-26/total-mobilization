# TOTAL MOBILIZATION — project instructions

## ⛔ READ THIS BEFORE PROPOSING ANY WORK

**The game is not fun yet, and adding more systems will not fix it.**

As of 2026-07-29 the designer played the built game and reported it "feels really wonky" and
not fun compared to the earlier version. Asked to localize the problem, they confirmed **all
four** of these are true at once:

1. **Nothing to do** — the player waits and watches more than they play.
2. **No feedback** — actions happen but you can't tell whether they mattered.
3. **Physically bad** — controls / pacing / unit behavior feel off.
4. **No stakes** — the battle resolves without the player feeling like they affected it.

That is not a polish problem. That is "this is a simulation, not yet a game."

**The likely root cause — treat this as the central open design question:**
The project's thesis is *"you are the coordinating intelligence of a society at war — not its
hands."* Under that banner an enormous amount of autonomy was built: militias raise
themselves, battalions self-deploy and self-garrison, industry retools itself, home guard
musters itself, doctrine AI positions everything, combat resolves itself. Each was elegant in
isolation. Stacked together they may have **removed the player from their own game.** The
thesis is philosophically strong and possibly anti-fun as implemented.

**Therefore:**
- **Do NOT add new simulation systems** unless the designer explicitly asks. The instinct to
  answer "it's not fun" with "let's add another mechanic" is the exact trap this project is in.
- Work on **player agency, feedback, and moment-to-moment feel** instead.
- Before building anything, ask: *what does the player DO, and how do they know it worked?*
- The prior session added ~15 major systems in one stretch. Depth is not the bottleneck.

A useful unexplored diagnostic: the **earlier/original version** of this game (referenced by
the designer as more fun) — compare what it did that this doesn't. The repo root
`index.html` is a small stub; the older build may need to be found in git history.

---

## What this is

A browser real-time strategy game. You direct a nation mobilizing to defend against an
invasion: build economy and industry during a PREP phase, then repel a landing in COMBAT.
Procedurally generated island, cities, and names every run.

- **The game lives in `topdown/`.** Never serve from the repo root.
- Stack: ES modules + Canvas2D, **no build step**, static HTTP, **assetless** (everything
  drawn procedurally — no image or audio files).
- Live branch: `claude/topdown-rebuild` (pushed). Not the default branch.

**`topdown/docs/STATE.md` is the detailed handoff** — what's built, what's pending, how to
run and test. Read it after this file.

---

## Working with this designer

- **They are a game designer, not a programmer.** Explain in design terms. Don't hand them
  tooling critiques or ask them to fix plumbing — just handle it.
- **Small chunks.** ~15–20 minutes of work, then show a result and get a reaction. Do NOT
  chain long autonomous builds; that produced multi-hour black boxes and hit session limits
  three times in one session.
- **Show, don't tell.** Screenshots for anything visual. They decide on look.
- **Give options for aesthetic calls** — mock up alternatives and let them choose (this is how
  the UI direction was picked) rather than deciding for them.
- **Honesty over positivity.** They twice commissioned harsh outside critique and acted on it.
  Verify subagent claims rather than relaying them — agents have confidently reported things
  that were wrong.
- **They write all player-facing copy.** `[PLACEHOLDER]` strings are deliberate, not bugs.
  `topdown/docs/LORE.md` has the world, vocabulary, and story hooks to draw from.
- Dislikes: synth audio ("wonky"), "AI-looking" UI, arcadey combat, units that don't behave
  like their real-world counterparts.

---

## Technical essentials

```bash
# serve (ALWAYS from inside topdown/)
python3 -m http.server 8000 --directory /home/user/total-mobilization/topdown
# open http://localhost:8000/?seed=42     (?seed= skips the menu)

# rebuild the single-file playable bundle
node /home/user/total-mobilization/topdown/tools/bundle.mjs   # -> dist/total-mobilization.html

# balance harness: 36 self-play games, ~15-20 min — run in BACKGROUND
PORT=8791 CONCURRENCY=2 node /home/user/total-mobilization/topdown/tools/balance-harness.mjs
```

**Healthy balance:** unprepared LOSES on normal+, prepared WINS, zero "undecided".

**Environment gotchas (these cost hours to rediscover):**
- **cwd resets between Bash calls** — use absolute paths.
- **Foreground `sleep` is blocked** (exit 144). Use Playwright waits or background tasks. A
  `pkill` at the front of a compound command aborts the whole command.
- Chromium: `/opt/pw-browsers/chromium-1194/chrome-linux/chrome`
  Playwright ESM: `/opt/node22/lib/node_modules/playwright/index.mjs`
  (`NODE_PATH=/opt/node22/lib/node_modules`, args `--no-sandbox --use-gl=swiftshader`)
- `assets/audio/*.mp3` **404s are expected** — `game/audio.js` falls back to procedural synth.
- Network blocks non-registry hosts: no webfonts, no CDNs, no downloadable assets. System
  font stacks and procedural drawing only.
- `window.__debug` exposes hooks for nearly every system. Sub-objects **bind** world/map/economy
  internally — call them with only their own args (`__debug.reinforcements.raiseMilitia()`).
- **Run ONE build agent at a time.** Two agents in the same working tree stashed and reverted
  each other's uncommitted work. Read-only research agents can safely run in parallel.
- Poll for agent completion rather than waiting on a notification — one was missed and cost
  4.5 idle hours.

---

## Git

- Commit as work lands; don't batch. Push to `claude/topdown-rebuild`.
- The stop hook warns about "Unverified" commits — that's a **missing GPG signature**, not a
  wrong email. Cosmetic; ignore it unless the designer wants signing set up.
