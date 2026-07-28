# Strategy & Wargame UI Design — Reference Document

**A concrete breakdown of how real strategy/wargame interfaces are designed, with specific critique and recommendations for Total Mobilization's current UI.**

---

## 1. Current State of Total Mobilization's UI

### Current Layout (from `topdown/index.html`)

| Region | Component | Function | State |
|--------|-----------|----------|-------|
| **Top-left** | `#hud` (max 360px) | Live status ticker (mode, flash text, controls hint) | Always visible, low-profile |
| **Top-center** | `#phaseHud` (max 700px) | Phase badge (PREP/COMBAT) + fog/hint line + BEGIN COMBAT btn | Always visible |
| **Top-center** | `#guidancePanel` (400×42vh) | Goal-driven checklist (orders clipboard) | Dismissable, always available |
| **Left-center** | `#legendPanel` (210px, collapsible) | Map legend + resource filters + controls cheatsheet | Collapsed by default, nudge animation |
| **Right-edge** | `#rightStack` (236px flex column, capped 46vh scroll) | Vertical stack of 5 collapsible panels | Collapsed-by-default de-occlusion |
| **Right-edge (stack)** | `#econHud` | Resource/production ledger with summary tab | Collapsed by default, nudge animation |
| **Right-edge (stack)** | `#reinforcePanel` | Battalion reinforcement routes (3 sources) | Collapsible |
| **Right-edge (stack)** | `#battalionPanel` | Active player battalions, morale state, stance controls | Collapsible |
| **Right-edge (stack)** | `#homeDefensePanel` | City garrison posture toggle + per-city status | Collapsible, compact |
| **Right-edge (stack)** | `#newsPanel` | Wartime bulletin ticker + expanding headline feed | Collapsible, always-visible latest headline |
| **Bottom-center** | `#buildbar` (780px wide, 46vh tall, centered, hidden by default) | Construction ledger grouped by category, scrollable | Toggled via B-mode, centered, bounded |
| **Top-left** | `#prodPanel` / `#importPanel` (300×auto) | Production/import requisition (hidden by default) | Hidden until mode triggered |
| **Top-left** | `#techPanel` (360×76vh, scrollable) | Research & tech tree quick-access panel | Hidden until toggled |
| **Bottom-left** | `#objectiveHud` (210px min, red SITREP slip) | Win/lose scoreboard (COMBAT-phase only) | Hidden outside COMBAT |
| **Bottom-right** | `#directorBubble` (300px max, sticky note tilt) | Reactive commentary (success, guidance, gags) | Transient, fades in/out |
| **Canvas overlay** | `#techTreeCanvas` (full-screen, z-index 500) | Full interactive tech/production tree (G key) | Exclusive overlay, hidden by default |
| **Overlay (modal)** | `#mainMenu` | Campaign selection dossier (z-index 1000) | Covers game on boot, dismissable via deep link |
| **Overlay (modal)** | `#areYouFiveDialog` | Confirmation slip (silly-mode gag escalation) | Z-index 2000, modal |
| **Overlay (modal)** | `#outcomeScreen` | After-action report (victory/defeat, z-index 2200) | Full-screen, final state |
| **Overlay (modal)** | `#takeoverScreen` | "You're fired" / "Too young" terminal takeover (z-index 2100) | Full-screen, modal |

### Design Language

- **Aesthetic**: Operations map / war room — parchment panels pinned/taped to dark map table
- **Typography**: System monospace only (Consolas, SF Mono, Courier New), no web fonts
- **Paper**: Gradient parchment (`--paper` `#e7dcbd` → `--paper-2` `#ddcfa4`) + SVG noise grain overlay
- **Ink**: Dark brown (`--ink` `#241d10`) with soft/faint variants
- **Stamps**: `.opsStamp` — rotated (`-1.4deg`), bordered, uppercase, red/green/ink variants
- **Frames**: Corner registration ticks (::before/::after pseudo-elements, surveyor-style crop marks)
- **Accents**: Red (`#8a2a1c`/`#b8412a`), green (`#2e5c3c`), amber (`#8a5a10`), blue-ink (`#1f4a63`)
- **Shadows**: `0 10px 26px rgba(4, 6, 10, .55)` — soft, map-table depth

### Problems Identified

1. **Right-edge panel stack** becomes a wall even when collapsed — every tab eats screen space; at 2160p+ one eye scans nothing but tabs and edges
2. **Collapsible-by-default discovery friction** — first-time players don't find the nudge animation or assume collapsed tabs are locked
3. **No clear primary vs. secondary hierarchy** — all 5 right-stack panels visually equivalent (same header style, same width, same toggle treatment)
4. **Map legend left-center positioning** competes with objectives on same axis
5. **Build bar width** (780px) can occlude guidance panel; poor coordination between bottom-center and top-center layers
6. **Urgency escalation is CSS-only** — news panel's `.urgent` border tint is subtle; no audio/animation to match the narrative tone
7. **No persistent "command focus"** — which panel is "most important right now" is unclear; econ, reinforcements, and battalion panels all demand equal attention despite different use frequencies
8. **Information scattered across three toggles** — production plan (build bar), production panel, and tech panel require separate clicks to cross-reference
9. **No "at a glance" readout** — econ summary is one line; players can't scan balance, income, spending, and threats simultaneously
10. **Morale and status visual language** underutilized — battalions use left-rule color (green/amber/red), but this convention doesn't extend to reinforcements or other time-sensitive lists

---

## 2. How Real Strategy/Wargame UIs Are Laid Out

### Hearts of Iron IV

**Layout**:
- Monolithic sidebar (left or right, player choice) containing most toggles
- Map + minimap dominates center/right
- Time controls + date top-center
- Notifications stack top-right or in a dedicated message log
- Country/faction info always visible top-left
- Research/tech sidebar (scrollable, organized by category)

**Visual Language**:
- WWII historical authenticity: typed ledger entries, folder tabs, file-folder section headers
- Dense but tab-organized (click a tab, see only that section; no always-open wall of panels)
- Color-coded by nation; UI tints shift with political stance changes
- High legibility via monospace numbers and clear column alignment

**What makes it authored**:
- Sidebar can be toggled/moved; UI adapts to player preference rather than one locked layout
- Section headers use period-accurate paper stamps and typography
- Feedback is immediate: hover states, selection highlights, status bars with live time passage

### WARNO / Steel Division 2

**Layout**:
- **Bottom panel**: Selected unit details (type, crew cohesion, ammo, weapons status, behavior toggles) — always visible
- **Left sidebar**: Deployment grid + unit category cards (anti-tank, support, recon icons with live status)
- **Right sidebar**: Map, minimap, minimap scale controls
- **Top bar**: Phase/turn counter, objective status, time/reinforcement queue
- **Hover cards**: Appear over units showing brief stats (armor, quality, vehicle type)

**Visual Language**:
- Translucent dark panels (not opaque paper — matches the dark fog-of-war aesthetic)
- Unit cards are grid-based icon + label + numeric stats (crew, cohesion %, fuel %, weapon ammo counts)
- Clear distinction: left = deployment/logistics, bottom = tactical detail, right = map awareness
- Icons are high-contrast silhouettes (easy to scan at a glance)

**What makes it authored**:
- Unit card layout mirrors a physical roster card (rank thumbnail + stats columns)
- Minimap is a control surface (click to pan, drag to adjust zoom) — not just a display
- Deployment grid feels like a tactical planning table, not a generic menu

### Wargame: Red Dragon

**Layout**:
- **Large tactical minimap** (left + center) with toggle for detailed terrain/intel
- **Right sidebar**: Unit list organized by category (Infantry, Armor, Air, Support) with numeric availability and cost
- **Bottom bar**: Selected unit(s) details, abilities, waypoint overlay
- **Top bar**: Time/turn, reinforcement counter, objectives, intel source strength
- **Callouts**: Large red/blue numbers overlay the map showing unit quantities per sector

**Visual Language**:
- Utilitarian extreme — every pixel is data; no decoration
- Unit list uses tabular format: name | count | cost | readiness state (color-coded dot)
- Callouts on map are large, hard to miss, color-coded (red enemy, blue friendly, gray unknown)
- Font size adjustable via 3 presets; minimap size adjustable to user preference

**What makes it authored**:
- Density is intentional (players expect this for a deep sim); UI isn't apologizing for complexity
- Scalability shows respect for user preferences and accessibility
- Intel source opacity/display is diegetically justified (fog of war affecting what you see and how clearly)

### Company of Heroes 3

**Layout**:
- **Bottom-left**: Squad/unit details card (photo-realistic unit, squad composition, abilities, health bars)
- **Bottom-right**: Building/construction queue (scrollable list) + production progress bars
- **Top bar**: Time/turn, resources (manpower, munitions, fuel) in columns with +/- rates
- **Right sidebar**: Mini-abilities panel (smoke, grenades, abilities tied to selected unit)
- **Map corner**: Minimap with fog overlay + tactical markers (player draws arrows/markers that appear to other players)

**Visual Language**:
- WWII film aesthetic: unit cards have vignette edges + film-strip frame appearance (drawn from exposed film strips as UI design cue)
- Dark translucent panels (no paper; this is a tactical monitor, not a map table)
- Clean typography, high contrast text on dark backgrounds
- Health bars and status indicators use military-accurate color: green (OK), yellow (caution), red (critical)

**What makes it authored**:
- Unit card design (photo + squad layout + health bars) evokes a soldier's actual roster/dossier
- Ability buttons arranged like a soldier's gear/loadout (visually intuitive)
- Resource display mirrors WWII logistics: manpower/munitions/fuel are actual constraints in the fiction

### Total War: Warhammer II & III

**Layout**:
- **Left sidebar**: Main menu toggle, faction icon, help, options
- **Bottom**: Unit cards (one per selected unit or formation), horizontally scrollable
- **Top**: Battle timer, faction resources (for strategic campaign map)
- **Right corner**: Minimap with tactical layer toggle (shows objectives, deployment zones)
- **Center**: Tactical indicators (spell effects, ability ranges, movement preview)

**Visual Language**:
- Dark translucent panels (not paper — this is a tactical holographic display)
- Unit cards show unit portrait, health bar, morale bar, special abilities, weapons status
- Large, scannable icons (shields, hammers, bows = unit type at a glance)
- Layered depth: outline effects on selected units, semi-transparent for far units

**What makes it authored**:
- Unit portraits are hand-drawn, not generic icons
- Morale state is visually distinct (bright green "ready" → yellow "wavering" → red "fleeing")
- Special ability buttons are large and contextual (appear only when available/legal)

### Command: Modern Operations

**Layout**:
- **Top bar**: Date/time (large), scenario title, play speed controls, message log toggle
- **Left sidebar**: Unit list (scrollable, tree-organized by force structure)
- **Right sidebar**: Hover-info databox (selected unit: type, position, heading, speed, fuel, weapons status)
- **Bottom**: Contact/threat list (radar contacts and their classifications)
- **Center**: Map with unit icons, threat rings, exclusion zones, navigation markers

**Visual Language**:
- DOS-era aesthetic (deliberate throwback to earlier wargame UIs)
- Monospace font for numeric data (coordinates, bearings, speeds)
- Color-coded by side (blue friendly, red threat, yellow neutral) with intensity indicating confidence
- Minimal shading; high contrast black & white with color accents

**What makes it authored**:
- Force structure tree mirrors real military organization (HQ → Companies → Squads)
- Databox layout mimics a real tactical display (bearing/range/speed/heading in specific columns)
- Message log is a persistent ribbon, not a popup (events scroll by like radio traffic)

### Terra Invicta

**Layout** (from research, partially inferred):
- **Main screen**: 3D globe with clickable regions + orbits
- **Right sidebar**: Councilor panel (current assignee, available missions, progress bars)
- **Bottom**: Current order/mission details (text field + confirm/cancel buttons)
- **Panels**: Various toggles for orbital stations, technology trees, base management
- **Feedback**: Tooltips on hover; detailed panels on click (modal overlays)

**Visual Language**:
- Futuristic but grounded (not neon, not flat; slightly textured panels)
- Text-heavy (complex systems require explanation)
- Hierarchical menu structure (Council → Missions → Action Details)

**What makes it authored** (and what doesn't):
- The orbital display gives a diegetic frame (you're watching from space)
- However, UI/UX was noted by community as a pain point (overcomplicated menus, poor organization)
- This is a cautionary example: a great diegetic conceit can't save an unintuitive navigation structure

### Unity of Command

**Layout**:
- **Center**: Large hexagonal grid with thick, friendly unit symbols (bobble-head style, unmistakable at a glance)
- **Left sidebar**: Unit stats (strength, supply status, type)
- **Right sidebar**: Objective goals + progress toward win condition
- **Bottom**: Movement preview (translucent ghost path showing where a unit will move)
- **Tooltips**: Minimal; most information is on the screen already

**Visual Language**:
- Brutally simple: no gradients, no decorative shadows, no bloom or glow
- Unit symbols are large enough to parse quickly (this is the entire game — if you can't see your units, nothing else matters)
- Color is functional only: blue friendly, red enemy, neutral gray, highlight yellow

**What makes it authored**:
- Clarity is the design philosophy; every choice serves readability
- No "generic strategy game UI" feeling because nothing is wasted on chrome
- Hexagonal grid + thick symbols create a board-game aesthetic (this is played like a tabletop war game, not a real-time RTS)

### Shadow Empire

**Layout** (from research):
- **Massive sidebar**: Everything — units, facilities, research, logistics, politics, morale
- **Sub-menus**: Inconsistent scroll behavior (some mouse-wheel, some arrow-key only) causes friction
- **Map**: Center with unit stacks shown as numbers (5th Div: 12 units, 8 damaged)
- **Modal overlays**: Unit design screens (edit unit composition, assign equipment, set doctrine)
- **Feedback**: Text logs (build logs, research progress, combat reports)

**Visual Language**:
- Utilitarian to the point of austere (black background, white/cyan text, minimal color)
- Dense tables (unit rosters, facility lists, supply manifests)
- Numeric status everywhere (morale numbers, strength points, supply ratios)

**What makes it authored** (and what doesn't):
- The density and complexity is intentional (hardcore 4X/wargame hybrid)
- **However**, inconsistency breaks immersion: scrollbar on one menu but not the next, icon styles that don't match, modal sizes that vary
- A cautionary example of how density without consistency feels "AI-assembled" rather than designed

---

## 3. Information Architecture for Strategy Games

### Core Principles

| Principle | Rationale | Example |
|-----------|-----------|---------|
| **Edge Anchoring** | Players scan edges first (where their eyes naturally rest); keep controls always accessible | Top: time, resources, phase. Corners: minimap, indicators. Sides: lists, toggles. |
| **Persistent vs. On-Demand** | Some info is so critical it must never require a click (resources, time, threats). Some info is contextual (unit details, modal forms). | Persistent: top bar + minimap. On-demand: unit card, production details. |
| **Progressive Disclosure** | Show summary first, expand for detail. Never show everything at once. | Econ: summary line → click to expand full ledger. Unit: icon → hover for brief stats → click for full dossier. |
| **Focal Hierarchy** | Establish a clear "what matters most right now" via size, position, color, animation. | In combat: enemy threat level (large, red, animated). In prep: production progress (secondary, green). |
| **F-Pattern Scan** | Players scan left-to-right, then top-to-bottom in an F shape. Prioritize critical info on this path. | Top-left: time + core status. Left edge: most important list. Center: map. Right: less urgent details. |
| **Lateral Consistency** | If one panel uses a header style, all panels use the same style. If one list is sortable, all lists should be. | All collapsible panels use same `.opsToggle` style. All status lists use same left-rule color convention. |
| **Diegetic Framing** | Ground UI in the fiction (military document, tactical display, radar scope) so it reads as "authored" not "assembled." | Operations map aesthetic: every panel is paper pinned to a war room table. Not random floating cards. |

### Standard Screen-Edge Allocations

Real strategy games consistently use:

| Edge | Standard Use | Rationale |
|------|--------------|-----------|
| **Top** | Time, date, phase, core resources (income, manpower, budget), threats/alerts | Critical info must never be off-screen; high-scanning priority |
| **Top-left** | Status ticker, hints, flash text, camera mode indicator | First thing players look at (muscle memory from decades of RTS) |
| **Top-center** | Phase/turn indicator, objective status, begin-next-phase button | Central moment-to-moment state |
| **Top-right** | Notifications, warnings, incoming messages, alerts | Urgent info enters here; scrolls/fades |
| **Left edge** | Lists (units, facilities, resources, techs), filter controls, legend | Primary info column; left-scanning is natural for many players |
| **Right edge** | Minimap (always), unit details (on hover/select), secondary panels | Minimap is universal; details appear when needed; no strategic info here (preserve map view) |
| **Bottom** | Selected unit/building details, action buttons (for that selection), movement preview | Context-sensitive to what the player is doing; out of the way when not needed |
| **Corners** | Minimap (top-right), compass/orientation (top-right), score (bottom-right), objective/sitrep (bottom-left) | Maximum visibility, minimum occlusion of map |

### What Deserves Permanent Real Estate

**Always visible (players should never have to toggle to see)**:
- Current time/date (top-center or top-left)
- Core resources (money, manpower, fuel, munitions) with per-turn delta
- Phase/turn indicator
- Minimap
- Threats/alerts (incoming enemies, low resources, buildings damaged) — summary only, expand on click
- Selected unit/building details (bottom or right-edge, whatever doesn't block the map)

**On-demand (fine to hide, but should be fast to access)**:
- Production queue / build orders (may take 30% of screen when expanded, but collapsed to a tab when not needed)
- Tech tree (usually full-screen overlay, accessed via hotkey)
- Unit roster / battalion list (scrollable sidebar when expanded, collapsed tab when not needed)
- Economic ledger (collapsible panel, summary always visible)
- Map legend / filters (left-edge collapsible, collapsed by default)

**Never always-visible** (show only when player triggers an action):
- Building details (right-click a building → modal shows construction options, costs, etc.)
- Unit comparison (select two units → overlay shows side-by-side stats)
- Market/trade interface (open trade panel → modal marketplace appears)
- Message log (click notification → log expands or pops as a side panel)

### Scanning Patterns

Players scan strategy game screens in this order:

1. **Threat map center** (where are enemies?) — if no threats visible, scan expands outward
2. **Top bar** (time, resources, phase state) — is anything urgent?
3. **Selected unit / focal point** (if a unit is selected, its stats appear somewhere obvious)
4. **Minimap** (overall map state, fog of war, objective locations)
5. **Sidebars** (only if time permits; details are lower-priority than map awareness)

**Implication**: If a critical piece of info (e.g., "enemy detected 2km east") is buried in a nested panel, players will miss it. Alerts must be in the top bar or on the map itself, never in a collapsible.

---

## 4. Density Done Well

Strategy games embrace information density because they *must*. A wargame cannot hide complexity; it has to present it clearly. Here's how good ones do it:

### Tabular Data: The Model

Real strategy UIs use **tables, not cards**, for inventory/lists:

```
BATTALION ROSTER
─────────────────────────────────────
Name          | Type    | Str | Mor | Stance
─────────────────────────────────────
3rd Guards    | Mech    | 78% | Shk | Defend
5th Rifles    | Inf     | 92% | Fre | Attack
─────────────────────────────────────
```

Why tables beat cards:

| Feature | Cards | Tables | Wargame Winner |
|---------|-------|--------|---|
| Scannability | One column, all rows visible | Multiple columns, rows narrower | **Tables** (scan all units instantly) |
| Comparison | Click one card, find another, scroll back | All rows on screen, left-align names, right-align numbers | **Tables** (compare at a glance) |
| Density | 1 card = 4-6 lines of vertical space (wasteful) | 1 row = 1 line (compact) | **Tables** (2–3× more data, same space) |
| Cognitive load | Large blocks require parsing | Narrow columns mean the eye jumps and skips | **Tables** (columnar layout is muscle memory from spreadsheets) |
| Accessibility | Large text can be good (clear fonts) | Small text can hurt (must fit many columns) | **Draw** (both have pros/cons; tables win overall if font is monospace + numbers are right-aligned) |

### Rules & Dividers, Not Boxes

Dense info is readable when it's properly segmented. Real strategy UIs use:

- **Dashed horizontal rules** (not solid boxes) to separate sections
  - Dashed implies "rough sketch, not final" (very militarily authentic — field orders are scrawled)
  - Boxes create visual "cells" that make the screen feel cramped; rules guide the eye without caging it
  
- **Left-edge accent bars** (a 3px colored line on the left of each list item) to show status
  - Red left-bar = urgent, damaged, low morale
  - Green left-bar = ready, full strength, good morale
  - Ink-soft left-bar = neutral, waiting
  - Far more compact than a "Status: X" label on every row

- **Consistent label/value spacing**
  - Label (left, uppercase, small font): `STRENGTH`
  - Separator: whitespace or a thin rule
  - Value (right, monospace, bold): `92%`
  - Right-align all numeric values so they stack cleanly

### Restrained Iconography

Wargames that feel "authored" use icons sparingly:

- **One icon per unit type** (Infantry, Armor, Air, Support) — standardized across the UI
- **Status icons only for critical states** (Warning △, Ready ●, Damaged ✗)
- **Never more than 3–4 icons in a row**; beyond that, text is faster to scan

Icons slow players down because they require a legend lookup. Text is faster if the font is monospace and the information is columnar.

### No One-Card-Per-Thing Web Style

This is critical: **Web cards (one big rounded rectangle per item) are the enemy of strategy game UI**.

A typical web card:
```
╔════════════════════╗
║  Unit: 3rd Guards  ║
║  Type: Mech Inf    ║
║  Health: 78%       ║
║  Morale: Shaken    ║
║  ┌──────────────┐  ║
║  │   [Details] │  ║
║  └──────────────┘  ║
╚════════════════════╝
```

This wastes ~400px of width per item. On a 1920px wide screen, you fit maybe 4 cards.

A wargame table:
```
Name          Type    Str Mor   Stance
3rd Guards    Mech    78% Shk   Defend
5th Rifles    Inf     92% Fre   Attack
7th Cav       Armor   65% Brk   Rally
```

Same 1920px width, you fit 25+ units and still have room to scroll.

---

## 5. Diegetic & Thematic Framing

Strategy games feel "authored" when the UI is grounded in a fictional artifact. Here are real approaches:

### Operations Map Aesthetic (Your Current Direction)

**Fiction**: Every panel is a piece of paper pinned/taped to a command-post map table.

**Visual execution**:
- Parchment gradient background (worn paper, not pristine white)
- Subtle noise/grain texture (SVG feTurbulence, very cheap computationally)
- Hairline ink borders (not thick outlines; mimics the edge of a printed sheet)
- Corner registration ticks (surveyor's crop marks, drawn with CSS pseudo-elements)
- Rubber-stamp section headers (rotated, double-bordered, mix-blend-mode: multiply)
- Monospace type (typewritten, ledger-style, not handwritten)

**Done well**: Your code does this correctly. The grain texture, the stamps, the corner ticks are all authentic.

**Pitfall to avoid**: Don't add too much "character" — coffee stains, torn edges, coffee-cup rings. These feel cutesy and break immersion. The best operations-map UIs are clean and professional (they're a serious military command center, not a scrapbook).

### Tactical Display Aesthetic (Dark Translucent Panels)

**Fiction**: A tactical monitor in a command center (like a radar scope or digital battlefield display).

**Visual execution**:
- Dark background (black or very dark blue)
- Translucent dark panels (transparency: 0.9–0.95) so the map beneath is faintly visible
- High-contrast text (white or light green on dark, like old CRT monitors)
- Glowing accents (thin colored lines or borders, not filled shapes)
- Sharp, geometric fonts (no serifs)

**Example**: Company of Heroes 3, Warhammer II, Command: Modern Operations.

**Done well**: Creates a sense of "you're reading live tactical data, not a static chart."

**Pitfall to avoid**: Don't add fake scanlines or bloom effects (they look dated, not timeless).

### Film Strip / Roster Card Aesthetic

**Fiction**: UI elements are physical artifacts (dossiers, roster cards, photo prints).

**Visual execution**:
- Card borders with a vignette or frame edge (mimics Polaroid or film-print look)
- Photo/image element + text below (portrait of unit + stats)
- Thick borders (3–5px, solid color or slightly textured)
- Slight drop shadow (suggests the card is floating over the map)

**Example**: Company of Heroes 3's unit cards (photo + squad composition).

**Done well**: Unit cards become collectible-looking (this is the faction's roster, not a generic stat block).

**Pitfall to avoid**: Don't add chromatic aberration or lens flare. A simple frame is enough.

### Military Document Framing

**Fiction**: UI is styled like classified military orders, forms, or briefing documents.

**Visual execution**:
- Monospace type (implies typed on a typewriter or printer)
- Ruled lines (dashed or solid, guides the eye)
- Classification banners (TOP SECRET, UNCLASSIFIED at top/bottom in all-caps, red or black)
- Stamps (date stamps, approval stamps in a corner)
- Margin notes (marginalia in a smaller font, like an officer's handwritten notes)
- Form-like structure (label on left, value on right; reminiscent of a government form)

**Example**: Hearts of Iron IV, WARNO.

**Done well**: Players feel like they're reading an actual briefing (this is lore-authentic and immersive).

**Pitfall to avoid**: Don't overdo stamps/scribbles (one or two key stamps, not a graffiti wall).

---

## 6. Feedback & State

Strategy UIs must communicate state changes clearly and urgently. Here's how real games do it:

### Alerts & Urgency Escalation

| Level | Visual | Audio | Example |
|-------|--------|-------|---------|
| **Info** | Icon + color accent (green or blue) in top bar | Soft chime or none | "Research complete" |
| **Warning** | Amber/orange color, title blinks or pulses | Medium-volume warning tone | "Resource low" |
| **Alert** | Red color, animated pulse or flash, larger text | Loud, distinctive alarm | "Enemy detected" |
| **Critical** | Red + screen-edge tint overlay + persistent animation | Repeating alarm until dismissed | "Capital under attack" |

Your game uses `#newsPanel.urgent` (CSS class toggle) to tint the news panel red as mobilization rises. This is good, but consider adding:
- Audio escalation (soft background ambience when urgent, rises to a steady warning tone)
- Screen-edge tint (inset box-shadow on body, like your sell-mode does) — draws the eye immediately
- Animation (a slow pulse on the urgent indicator, not constant movement which becomes white noise)

### Selection & Hover States

Real wargames show selection/hover with:

- **Outline on the map** (thick colored line around selected unit)
- **Color inversion in the list** (selected row: dark background + light text, like `.selected` state)
- **Highlight bar** (a thick colored bar on the left of the list item — same convention as your morale indicators)
- **Hover state is subtle** (slightly lighter background, not a full color flip; hover should never be as strong as selection)

Your game uses `.opsBtn.selected` (inverted colors) which is correct. Apply the same logic to list items:
- `.battalionRow.selected` { background: var(--ink); color: var(--paper); }
- `.reinforceRoute.selected` { border-left-color: var(--red); font-weight: bold; }

### Notification Queues

Don't let alerts spam the screen. Real wargames use:

- **Notification stack** (top-right or bottom-left, max 3–4 visible at once)
- **Auto-dismiss after 5–10 seconds** (player can click to keep visible)
- **Color-coded by severity** (green info → amber warning → red critical)
- **Log tab** (click a "Log" or "Messages" button to see older notifications)

Your `#newsPanel` is a good notification queue. Consider adding:
- Auto-scroll to newest headline (so the latest alert is always at the top when the panel is expanded)
- A "Clear old" button (to remove non-critical headlines)
- Persistent log on the side (not in the panel, but a separate small log that shows even when news panel is collapsed)

### State Indicators: The Left-Rule Pattern

You're already using this in `#battalionRow` and `#newsRow` (the 3px `border-left`). Extend it everywhere:

| State | Color | Meaning |
|-------|-------|---------|
| Ready / Good | Green (`--green`) | Full health, good morale, resources available |
| Neutral / Waiting | Ink-soft (`--ink-soft`) | No special condition |
| Caution / Low | Amber (`--amber`) | Low health, shaken morale, low resources |
| Critical / Bad | Red (`--red`) | Damaged, broken morale, resources exhausted |

Apply this to:
- `#reinforceRoute` (ready to deploy → left-rule green; otherwise ink-soft)
- `#techRow` (researchable → green; researched → blue; locked → ink-faint)
- `#buildBtn` (next → red outline; buildable → ink-soft; unbuildable → grayed)

---

## 7. Concrete Critique + Actionable Plan for Total Mobilization

### Current Strengths

1. **Diegetic aesthetic is correct** — the parchment, stamps, corner ticks, and grain texture all read as deliberately designed, not generic
2. **Collapsible-by-default de-occlusion is the right call** — hides panels on boot so players see the map first
3. **Monospace + uppercase stamps are thematically consistent** — every panel speaks the same visual language
4. **Right-edge stack avoids top/bottom collisions** — each panel has its own flex slot; no absolute positioning chaos
5. **Color hierarchy is subtle and professional** — red (urgent), green (ready), amber (caution), ink-soft (neutral); no neon or oversaturation
6. **News ticker + live marquee is engaging** — the scroll animation on the latest headline keeps players aware of escalation

### Critical Problems & Fixes

#### Problem 1: Right-Edge Panel Wall (High Impact, Medium Effort)

**Current state**: Five collapsible tabs (Econ, Reinforcements, Battalions, Home Defense, News) all at the same visual weight. Players don't know which to open first or which is most important. All five tabs are always visible, eating ~30px of screen width even when collapsed.

**Why it fails**: At high resolution, the player's eye is drawn to the tab strip (a vertical wall of labels) rather than the map. Each tab requires two clicks to peek inside (click to expand, click to read, click to collapse).

**Fix (Priority 1: CSS-only, high payoff)**:

Create a **two-tier hierarchy**: Primary panels (Econ, Battalions) and Secondary panels (Reinforcements, Home Defense, News). Primary panels live in the stack always. Secondary panels are:
- Grouped under a "More" collapsible meta-tab, or
- Hidden until explicitly opened (e.g., R key for Reinforcements, H key for Home Defense)

```html
<!-- Restructure #rightStack -->
<div id="rightStack">
  <!-- PRIMARY TIER (always visible, expanded/collapsed individually) -->
  <div id="econHud" class="opsPanel"><!-- ... --></div>
  <div id="battalionPanel" class="opsPanel"><!-- ... --></div>
  
  <!-- SECONDARY TIER (collapsed under a meta-tab, or accessed via hotkey) -->
  <div id="secondaryPanels" class="opsPanel">
    <!-- Reinforcements, Home Defense, News nested here or toggled separately -->
  </div>
</div>
```

**CSS change**: Make `.opsPanel` with `position: relative; flex: none; width: 236px;` remain unchanged, but add:
```css
#secondaryPanels { opacity: 0.8; } /* visually de-emphasize */
#secondaryPanels:not(.expanded) { max-height: 25px; } /* collapsed meta-tab */
```

**Rationale**: Econ (resource state) and Battalions (unit state) are constantly referenced. Reinforcements, Home Defense, and News are consulted less frequently. Separating them visually + by hotkey means:
- First-time scan of the UI is less overwhelming
- Power users can still access anything via hotkey (R, H, N)
- The screen breathing room is reclaimed

#### Problem 2: Collapsible-by-Default Discovery Friction (Medium Impact, Low Effort)

**Current state**: Econ and Legend panels have a `.nudge` animation (pulsing outline, bouncing arrow). This only fires once, on first load. After that, if a player closes the panel, there's no hint to reopen it.

**Why it fails**: New players might close a panel by accident, forget it's there, and never discover it again. The nudge only works once (removed on first open per main.js).

**Fix (Priority 2: JS + CSS, low friction)**:

1. **Extend nudge visibility**: Remove the `.nudge` class only when the player *interacts* with the panel (expands it, not just hovers). If they close it without expanding, the nudge reappears next boot.

2. **Add a persistent visual affordance**: Even without animation, the collapsed state should signal "clickable." Add a subtle indicator:
```css
.opsPanel:not(.expanded) > button[id$="Toggle"]::after {
  content: " ▾"; /* or "→" or "►" */
  color: var(--ink-soft);
  font-size: 9px;
}
```

3. **Tutorial tooltip (optional)**: On first boot, show a 1-second tooltip near the econ tab: "Click to expand" (fade out automatically).

#### Problem 3: No Clear Focal Hierarchy Among Panels (High Impact, Medium Effort)

**Current state**: All right-stack panels use the same `.opsToggle` header style (same background color, same border, same text size). A player can't tell at a glance which panels are "must-check" vs. "nice-to-have."

**Why it fails**: Econ and Battalions are consulted on every turn; News and Home Defense are more situational. But the UI gives them equal visual weight.

**Fix (Priority 3: CSS, medium payoff)**:

Create a visual distinction:
```css
/* PRIMARY PANELS — slightly brighter header, thicker border-top accent */
#econHud > button[id$="Toggle"],
#battalionPanel > button[id$="Toggle"] {
  background: var(--paper);
  border-top: 3px solid var(--blue-ink);
}

/* SECONDARY PANELS — subtly grayed, no accent */
#reinforcePanel > button[id$="Toggle"],
#homeDefensePanel > button[id$="Toggle"],
#newsPanel > button[id$="Toggle"] {
  background: var(--paper-2);
  opacity: 0.9;
  border-top: 1px solid var(--rule-soft);
}
```

**Rationale**: Thicker top border + brighter background = primary. Muted background + thin border = secondary. No color change needed; just font weight and spacing.

#### Problem 4: Urgency Escalation is CSS-Only, Subtle (Medium Impact, Low Effort)

**Current state**: `#newsPanel.urgent` adds a red `border-color` and a tinted background to the toggle. This is fine, but it's easy to miss, and there's no audio/animation to match the escalating tension of the game state.

**Why it fails**: As mobilization rises, the player should *feel* increasing pressure. A border tint is professional but muted. Real wargames layer visual + audio + animation.

**Fix (Priority 4: CSS + optional JS audio)**:

```css
/* ENHANCED URGENCY ESCALATION */
#newsPanel.urgent {
  border-color: var(--red);
}

#newsPanel.urgent > button[id$="Toggle"] {
  background: rgba(138, 42, 28, .14);
  color: var(--red);
  font-weight: 700; /* make text bolder, more urgent */
  animation: urgencyPulse 2s ease-in-out infinite;
}

@keyframes urgencyPulse {
  0%, 100% { text-shadow: 0 0 4px rgba(138, 42, 28, .3); }
  50% { text-shadow: 0 0 8px rgba(138, 42, 28, .6); }
}

/* OPTIONAL: Screen-edge tint (like sell-mode) */
body.urgent::after {
  content: ""; position: fixed; inset: 0; pointer-events: none; z-index: 5;
  box-shadow: inset 0 0 0 3px rgba(184, 65, 42, .2), inset 0 0 60px rgba(184, 65, 42, .08);
  transition: all .4s ease;
}
```

**Optional JS escalation** (call this when mobilization crosses thresholds):
```js
// In main.js, when updateNews() runs:
if (world.economy.mobilizationLevel >= 0.7) {
  document.body.classList.add('urgent');
  playSfx('alarm-warning'); // soft background warning tone
} else {
  document.body.classList.remove('urgent');
}
```

#### Problem 5: No Persistent Visual Language for Time-Sensitive Items (Medium Impact, Low Effort)

**Current state**: `.battalionRow.morale-broken` uses a red `border-left`, which is great. But reinforcement routes don't use this convention; they just have a label `.reinforceRouteGate` in red text. This inconsistency means players don't immediately see "a battalion is routed" vs. "a reinforcement path is blocked."

**Why it fails**: When scanning the right-edge stack, the eye should instantly find red left-rules (danger) and green left-rules (ready). But the visual language is inconsistent.

**Fix (Priority 5: CSS, low effort, high payoff)**:

Extend the left-rule pattern to all lists:
```css
/* REINFORCEMENT ROUTES — inherit morale color scheme */
.reinforceRoute { border-left: 3px solid var(--ink-soft); }
.reinforceRoute.ready { border-left-color: var(--green); }
.reinforceRoute.blocked { border-left-color: var(--red); }

/* TECH ROWS — already correct, but make it consistent */
.techRow { border-left: 3px solid var(--ink-soft); }
.techRow.researchable { border-left-color: var(--green); }
.techRow.researching { border-left-color: var(--blue-ink); }
.techRow.done { border-left-color: var(--green); opacity: .82; }

/* NEWS ROWS — already use it, extend to live status */
.newsRow { border-left: 3px solid var(--ink-soft); }
.newsRow.alert { border-left-color: var(--red); } /* enemy captured city, threat detected */
.newsRow.positive { border-left-color: var(--green); } /* tech researched, city recaptured */
```

**Rationale**: Once a player learns "red left-rule = urgent," they can scan the entire right-stack in ~2 seconds and find all problems.

#### Problem 6: Information Scattered Across Three Toggles (Medium Impact, High Effort)

**Current state**: Production planning requires three separate clicks/modes:
1. `#buildbar` (B key) — what buildings you *can* build
2. `#prodPanel` (P key) — what a *facility* produces (and in how long)
3. `#techPanel` (T key) — what you need to *research* to unlock buildings

A player can't easily cross-reference "I want Jet Fighters. Do I have a Research Center? Is it researching Fighter Tech? Can I afford Elite Factory?"

**Why it fails**: The mental model is scattered. Players have to hold state in their head while switching between three panels.

**Fix (Priority 6: Structural refactor, high impact but effort)**:

This requires rethinking the production UX (beyond CSS). Consider:
- **Option A (Low effort)**: Overlay a "Production Roadmap" modal when the player opens `#prodPanel`. Shows a visual chain: Tech Requirement → Facility → Building. The roadmap is clickable (click Facility → highlights it in `#buildbar` and switches to B-mode with it pre-selected).
- **Option B (Medium effort)**: Merge `#prodPanel` and `#buildbar`. When the player opens B-mode, the panel shows: categories on the left (military, production, etc.), buildings in the center, production chain on the right.

For now, a low-effort fix: **Add a link in `#prodPanel`** that says "View in Build Bar (B key)" or "Auto-select in build mode." This at least acknowledges the scattered state.

#### Problem 7: No "At a Glance" Econ Readout (Medium Impact, Low Effort)

**Current state**: `#econHudSummary` (the collapsed state) shows one line:
```
STOCKPILE | IC xxxxxx | manpower xxxx | influence xxxx | mobilization x.x%
```

This is good, but it doesn't show *income* and *spending*. A player has to expand the ledger to see "am I going broke this turn?"

**Why it fails**: On a 1-second glance, the player can't answer: "Do I have enough resources to build this unit?" They see current stockpile, but not burn rate.

**Fix (Priority 7: CSS + minimal JS, low effort)**:

Expand the collapsed econ summary to two lines:
```
STOCKPILE | IC xxxxxx | manpower xxxx | influence xxxx
INCOME    | +xxx IC  | +xxx manpower | SPENDING: -xxx (-xxx total)
```

Or use icons + colors to make the second line scannable:
```
IC: xxxxx (+xxx/turn, -xxx/turn) [green for positive, red for deficit]
```

This requires JS to compute income/spending (already tracked by `economy.js`), but it's low-hanging fruit.

#### Problem 8: No Morale/Status Pipeline (Low Impact, Medium Effort)

**Current state**: Battalion morale is shown in the `.battalionRow` as a colored pill (`.battalionMorale { fresh | steady | shaken | broken }`). This is great for the battalion list. But:
- Reinforcement routes don't have a morale indicator (should they?)
- News of morale changes (unit broke, unit rallied) appears in the news feed, but not summarized in the battalion list header

**Why it fails**: A player has to cross-reference: "did my battalion just break?" Ans: Look at news feed, find the event, then look at battalion list. This is slow.

**Fix (Priority 8: JS + optional notification, medium payoff)**:

1. **Add a morale state indicator to the reinforcement queue** (if reinforcements are being sent to a routed battalion, show a warning):
```js
// In game/reinforcements.js's update loop:
if (reinforcements.targetBattalion.morale === 'broken') {
  reinforcements.ui.classList.add('high-priority');
}
```

2. **Add a morale "ticker" to the #hud or top bar**:
   - When a battalion changes morale state, flash a brief notification: "3rd Guards: Shaken"
   - Color-coded (green "Fresh", amber "Shaken", red "Broken")
   - Fades after 3 seconds, or player can click to pin it

This is mostly JS; CSS is minimal (a `.high-priority` class on reinforcement routes).

### Priority Rank & Effort Matrix

| Priority | Problem | Effort | Impact | Recommendation |
|----------|---------|--------|--------|---|
| 1 | Right-edge panel wall | CSS | High | **Do immediately** — creates breathing room and hierarchy |
| 2 | Collapsible discovery | JS + CSS | Medium | **Do next turn** — solves first-time UX friction |
| 3 | Focal hierarchy | CSS | High | **Do next turn** — takes 30 min, high payoff |
| 4 | Urgency escalation | CSS + optional audio | Medium | **Do after visual fixes** — keeps engagement high |
| 5 | Inconsistent left-rules | CSS | High | **Do with Priority 3** — solidifies visual language |
| 6 | Scattered production info | Structural refactor | Low-medium | **Defer to next pass** — requires rethinking UX, but low-hanging fruit exists (add links) |
| 7 | No income/spending at a glance | JS + CSS | Medium | **Do after visual fixes** — critical for economic decision-making |
| 8 | No morale pipeline | JS + optional UI | Low | **Defer, or add in next phase** — nice-to-have, not blocking |

---

## 8. Summary of Key Takeaways

### What Makes Strategy Game UI Feel "Authored" Rather Than "AI-Assembled"

1. **Consistency of visual language** — one aesthetic (paper, tactical display, document, film-strip) applied uniformly to all panels. No mixing metaphors.
2. **Hierarchy without clutter** — primary/secondary distinction is clear but subtle (color, opacity, sizing, position; not bright neon).
3. **Density with readability** — tables beat cards; monospace beats proportional; aligned numbers beat scattered values.
4. **Diegetic framing** — UI elements are "things in the world" (military docs, tactical displays), not random floating menus.
5. **Consistent feedback** — left-rules, color coding, animations, and audio escalate together, not independently.
6. **Respect for player attention** — persistent info never requires a click; on-demand info is accessible but doesn't clutter the default view.

### Total Mobilization's Current Direction

Your current UI is **on the right track**. The operations-map aesthetic is solid. The monospace + stamps + parchment grain + corner ticks all read as deliberately designed. The right-edge stack is a good compromise between accessibility and screen real estate.

**The fixes you need are refinements, not overhauls:**
- Tier the right-edge panels (primary vs. secondary)
- Extend the left-rule + morale-color convention everywhere
- Strengthen urgency escalation (visual + audio, not just CSS)
- Expose income/spending in the collapsed econ summary
- Add discovery affordances for collapsed panels

These are all CSS + minimal JS changes. None require restructuring the HTML or rethinking the core layout.

---

## Sources

- [Hearts of Iron IV Interface Design](https://www.artstation.com/artwork/zAQWRL) — Ylva Ljungqvist, UI Artist at Paradox Interactive
- [WARNO: Guide & UI Overview](https://steamcommunity.com/sharedfiles/filedetails/?id=2727549821)
- [Wargame: Red Dragon UI Design](https://www.wikipedia.org/wiki/Wargame:_Red_Dragon)
- [Company of Heroes 3 UX/UI Overhaul](https://www.stuartng.art/ui-coh3)
- [Total War: Warhammer II Interface](https://www.totalwar.fandom.com/wiki/User_interface)
- [Command: Modern Operations UI Design](https://command.matrixgames.com/?p=1862)
- [Terra Invicta UI & Controls](https://wiki.hoodedhorse.com/Terra_Invicta/UI_&_Controls)
- [Unity of Command Design Case Study](https://www.gamedeveloper.com/design/design-case-study-unity-of-command)
- [Shadow Empire UI Tutorials](https://steamcommunity.com/sharedfiles/filedetails/?id=2326245400)
- [Diegetic and Non-Diegetic UI in Games](https://nastyrodent.com/diegetic-and-non-diegetic-ui/)
- [Types of UI in Gaming: Diegetic, Non-Diegetic, Spatial and Meta](https://medium.com/@lorenzoardeni/types-of-ui-in-gaming-diegetic-non-diegetic-5024ce6362d0) — Lorenzo Ardeni
- [Game UI Design Best Practices](https://www.justinmind.com/ui-design/game)
- [Let's Talk RTS User Interface, Part 1 – Interview with Dave Pottinger](https://waywardstrategy.com/2015/05/04/lets-talk-rts-user-interface-part-1-interview-with-dave-pottinger/)
- [Data Table Design UX Patterns & Best Practices](https://www.pencilandpaper.io/articles/ux-pattern-analysis-enterprise-data-tables)
- [Strategy Game UI/UX Best Practices](https://retrostylegames.com/blog/mastering-the-art-of-strategy-game-design/)
- [The Four Horsemen of Game UI Design](https://corporationpop.co.uk/thoughts/game-ui-design)
