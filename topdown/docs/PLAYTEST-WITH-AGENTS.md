# Playtesting with live commentary agents

Run *Total Mobilization* on your own machine with Claude Code agents watching over your
shoulder and reacting in character while you play.

**Why this has to run on your machine:** a browser-watching MCP server observes a browser on
the *same computer it runs on*. An agent in a cloud container can't see your screen — there's
no path from its sandbox to your desktop. So the agents live here, in this repo, and you run
them locally.

---

## 1. Serve the game

ES modules don't load from `file://`, so you need a local static server. From the repo root:

```bash
cd topdown
python3 -m http.server 8000
```

Then open <http://localhost:8000/> — or skip the menu with <http://localhost:8000/?seed=42>.

## 2. Install Claude Code + a browser MCP

Install Claude Code on your machine, then add a browser-automation MCP server so the agents can
see the page. Playwright MCP is the usual choice:

```bash
claude mcp add playwright npx '@playwright/mcp@latest'
```

Verify it's connected with `/mcp` inside Claude Code. (Any MCP that exposes screenshot +
page-read tools works; the agent definitions only assume "can screenshot and read the page.")

## 3. Install the agents, then run Claude Code from the repo root

The three personas ship in `topdown/docs/playtest-agents/`. Copy them where Claude Code looks
for agents (the repo's `.claude/` is gitignored on purpose — this repo publishes only the game —
so this step is a one-time local copy):

```bash
mkdir -p .claude/agents
cp topdown/docs/playtest-agents/*.md .claude/agents/
claude
```

They're then picked up automatically. (`~/.claude/agents/` works too if you want them available
in every project.)

## 4. Summon the peanut gallery

Three characters ship with the repo, each on a different model, each with a different job:

| Agent | Model | Who they are |
|---|---|---|
| `chief-of-staff` | Opus | Forty-year veteran. Dry, unimpressed, tells you the true thing in two sentences. |
| `supply-clerk` | Haiku | Ministry of Supply. Understands only that numbers are going down. Frightened. |
| `compact-intel` | Sonnet | Enemy intelligence officer. Assessing you as a target package. Calls it "reallocation." |

Ask for them by name while you play:

> Have the chief-of-staff look at my current position and tell me what I'm getting wrong.

> Run supply-clerk and compact-intel — I want both reactions to this landing.

Cheap models make this *better*, not worse: Haiku's clerk fires back fast and panicky, which is
exactly right for the character.

### Continuous commentary

To get reactions on a cadence instead of on demand, either:

- **Ask for a loop:** *"Every couple of minutes, have chief-of-staff screenshot the game and give
  me one line."*
- **Or use `/loop`** if available in your install, with a prompt like
  `/loop have compact-intel assess the current screen in 3 sentences`.

Two agents at once is the sweet spot. Three is chaos, which is sometimes what you want.

---

## Suggested run

1. Start at `?seed=42` so you and the agents are looking at a known island.
2. Play the PREP phase — build, connect roads, let mobilization climb. Ask `chief-of-staff` what
   they'd prioritize, then ignore them, because it's your country.
3. Press **C** to begin combat. Have `compact-intel` assess the moment the landing hits.
4. When it resolves, open the **After-Action Report** and paste it into the chat for a debrief.

## Notes

- The agents are told **not to edit game files** — they observe and comment only.
- They read real numbers off the screen. If they cite a stat, it's actually in your Ledger.
- Tone is grounded in `docs/LORE.md` — the Directorate, the War Department, the Ministry of
  Supply, the Continental Compact. Read that first if you want the jokes to land.
