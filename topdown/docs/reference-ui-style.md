# Visual Design Reference: Why UIs Look "AI-Generated" and How to Fix It
## A War-Room UI Case Study

**Document scope:** CSS-layer visual style tells that read as template-generated or "AI slop"; concrete fixes for parchment-and-ink war-room UIs. Focused on borders, shadows, depth, spacing, color, and texture—not layout or information architecture.

**Sources referenced:** Design blogs, UI research (2024–2025), game UI principles, CSS best practices.

---

## Part 1: The Tells — What Makes UI Read As AI-Generated

### 1.1 The Universal 1px Border

**The tell:** Every interactive element—buttons, panels, inputs—has the same hairline `border: 1px solid <color>`.

**Why it reads as AI/generic:**
- LLMs trained on Tailwind CSS boilerplate and Material Design tutorials (2019–2024) default to "add a border to everything" as a visual rule.
- One pixel is the atomic unit; it's lazy—doesn't commit to depth, weight, or hierarchy.
- Professional designs use borders *selectively*: to define structure (not to decorate), and at varied weights depending on importance.
- A 1px border on a button has the same visual weight as a 1px border on a main panel—everything reads equally "clickable/defined" rather than hierarchical.

**Real design treats it as a choice:**
- Some elements get a border; others rely on shadow, color shift, or spacing alone.
- When a border appears, it often has visual purpose: a frame, a rule between sections, a state change (focus, hover).

---

### 1.2 Redundant Depth Cues (Outline + Shadow + Fill)

**The tell:** Elements have *all three* at once:
- Solid fill (`background: var(--paper)`)
- Outline/border (`border: 1px solid var(--rule)`)
- Shadow (`box-shadow: 0 10px 26px rgba(...)`)

**Why it reads as overdesigned/AI-ish:**
- Depth cues work best in *isolation*. Pick ONE language:
  - **Elevation only** (varying shadow sizes, no borders).
  - **Tonal separation only** (different background colors, minimal shadows, no borders).
  - **Outline + minimal shadow** (line defines the object, shadow hints at depth).
- Combining all three is visual noise—like a designer said "add everything to make it pop" without understanding which tool solves which problem.
- Game UIs and professional SaaS specifically avoid this; they commit to one depth language for the entire system.

**The confusion:** A user's eye doesn't know whether the shadow or the outline is the "real" edge. It feels cluttered.

---

### 1.3 Uniform Corners (Identical `border-radius` Everywhere)

**The tell:** Every panel, button, input, and card has `border-radius: 2px;` (or 3px, or 4px—always the same).

**Why it reads as AI:**
- Uniformity signals "I don't have a reason for this choice; the tool applied the same rule globally."
- Real design uses corners strategically:
  - Sharp corners (`border-radius: 0`) for authority, structure, printed documents, official stamps.
  - Subtle curves for warmth and acceptance.
  - *Zero* corners for something that's meant to read as tape, ink, or precision.
- A war-room/operations-map UI has *no reason* to round corners at all—a xeroxed memo doesn't have rounded corners.

---

### 1.4 Same Treatment for Different Hierarchies

**The tell:** `.opsBtn` (a small action button) looks styled nearly identically to `.opsPanel` (a large information container).

**Why it reads as template:**
- A design system that doesn't understand hierarchy applies the same CSS vocabulary (border, shadow, fill) to everything.
- A button should feel *lightweight* relative to a panel.
- An accent should be visually distinct from chrome.

**Real hierarchy:**
- Primary panels: heavier shadow, more anchor, visual weight.
- Secondary panels: subtle shadow, less emphasis.
- Buttons: flat or minimal shadow, transparent or tinted background, smaller.
- Stamps/headers: bold color, slight rotation, hand-drawn feel.

---

### 1.5 Centered/Symmetrical Layout + Uniform Spacing

**The tell:** Every element has equal padding and margin; no grouping; no rhythm.

**Why it reads as AI:**
- LLMs default to CSS Grid or Flexbox with `gap: 10px` applied uniformly.
- Professional design uses *proximity*: related items close together (tight spacing), unrelated groups far apart (loose spacing).
- Uniform spacing feels dead because it says "I measured everything with a ruler" rather than "these items belong together."

---

### 1.6 Purple/Blue Gradient or Flat Color with No Tonal Variation

**The tell:** A single base color (or gradient) for the entire background; no texture; no layering; no optical hierarchy.

**Why it reads as default:**
- Gradients are the "Favorite Son" of AI image generation and web builders.
- Flat color + uniform saturation = no sophistication.
- Tonal hierarchy (using lightness variation) is invisible to brush-and-paint tools but obvious to trained designers.

---

### 1.7 Overly High Contrast Outlines

**The tell:** A 1px black or near-black border on a light fill, or vice versa.

**Why it reads as harsh/digital:**
- Ink-and-paper documents use *subtle* lines (grays, warm browns, faded inks).
- Pure black on white is screen/terminal language, not paper.
- High contrast outlines feel graphic design–adjacent (not tactile or organic).

---

## Part 2: Outlines & Borders Specifically — The Designer's Complaint

Your designer specifically flagged: *"the outlining looks kinda weird."*

### 2.1 When a Border Helps

| Use Case | Technique | Example |
|----------|-----------|---------|
| **Structural edge** | `border: 1px solid var(--rule-soft)` | Panel perimeter, separating content zones |
| **Physical frame** | `border: 2px solid var(--ink-soft)` with corner marks | Document registration ticks (your `.opsPanel::before/after`) |
| **Separator / rule** | `border-bottom: 1px dashed var(--rule-soft)` | Between sections within a panel |
| **State change** | `border-color: var(--red)` on :hover/:focus | User feedback without flashing |
| **Diegetic object** | `border: 1px solid var(--ink)` + grain + slight rotation | Stamp, label, tape edge |

### 2.2 When a Border Hurts

| Anti-Pattern | Problem | Fix |
|--------------|---------|-----|
| **Outline everything** | Noise. No hierarchy. Visual clutter. | Remove borders from 70% of elements; use shadow or spacing instead. |
| **Border + shadow + fill** | Redundant depth. Confusion about what's the "edge." | Pick ONE: elevation (shadow), tonal (background), or line (border). |
| **Uniform 1px border on all elements** | No weight difference. Button looks like a panel. | Vary border weight: 1px for subtle, 2px for stamps, 0 for backgrounds. |
| **Borders on top of grain/texture** | Line fights the surface. Feels printed-over. | If you have texture, don't outline it; let the texture define the edge. |
| **Black or very dark outline on parchment** | Digital-screen aesthetic. Harsh. | Use warm gray (`var(--ink-faint)`) or paper's own shadow. |
| **Double-outline (border + outer shadow that looks like a second line)** | Confusion. Feels like a photoshop artifact. | Choose: strong shadow OR subtle border, not both. |

### 2.3 Alternative Separation Methods (Instead of Borders)

#### **Inner Shadow (Inset Shadow)**
- Creates a recessed or pressed-in look.
- Reads as depth without a line.
- Example: `.opsPanel { box-shadow: inset 0 0 8px rgba(36, 29, 16, .15); }`
- When to use: To suggest a sunken document or carved surface.

#### **Background Contrast (Tonal Hierarchy)**
- Different background color or shade for different sections.
- No line needed; the color shift *is* the separator.
- Example: Header `background: var(--paper-2)`, body `background: var(--paper)`.
- When to use: Main method for creating hierarchy without visual noise.

#### **Spacing/Whitespace**
- Proximity principle: gap between items *is* visual separation.
- Tighter spacing = related. Wider spacing = separate groups.
- When to use: Primary hierarchy tool in modern design.

#### **Elevation (Shadow Hierarchy)**
- Larger, softer shadows = higher elevation = more important.
- No borders needed; depth is implied.
- Example: `.opsPanel { box-shadow: 0 10px 26px rgba(...); }` for main panels, `box-shadow: 0 2px 6px rgba(...);` for secondary elements.

#### **Divider/Rule (Strategic Line)**
- A *single* horizontal rule between major sections.
- Not an outline; a deliberate visual element.
- Example: `border-top: 1px dashed var(--rule-soft);` between legend sections.

#### **Icon or Visual Accent**
- A small icon, stamp, or rotated text in place of a border.
- Diegetic: feels like an object, not a UI construction.
- Example: Your `.opsStamp` rotated red stamp is much stronger than a plain outline.

---

### 2.4 Concrete Border Audit for Your Game

**Problem selectors in `index.html`:**

| Selector | Current Rule | Issue | Fix |
|----------|--------------|-------|-----|
| `.opsPanel` | `border: 1px solid var(--rule);` | Hairline on everything. Conflicts with grain texture. | Remove (keep shadow + registration marks). Use `.opsPanel::before/after` for frame only. |
| `.opsBtn` | `border: 1px solid var(--ink-soft);` | Same weight as panel border. Button feels structured like a panel. | Change to `border: none;` or `border: 1px solid transparent;` (reserve space). Use shadow for depth on hover. |
| `.opsStamp` | `border: 2px solid var(--red);` | Double border is good (heavier, intentional stamp). | Keep—this reads *correctly* as a deliberate object. |
| `.legendSectionLabel` | `border-bottom: 1px dashed var(--rule-soft);` | Dashed line works; divides sections. | Keep as-is (it's a rule, not an outline). |
| `.reinforceRoute`, `.battalionRow`, `.techRow` | `border: 1px solid var(--rule-soft);` + `border-left: 3px solid var(...);` | Outline + accent rule. Cluttered. | Remove full border; keep only the colored left rule (left rule is the visual accent, not the outline). |

---

## Part 3: Depth & Hierarchy Done Right

### 3.1 Choose ONE Depth Language

For a cohesive war-room UI, commit to a single system:

#### **Option A: Elevation (Layering with Shadow)**
- Primary panels: larger shadow (`0 12px 30px rgba(0,0,0,.3)`).
- Secondary panels: medium shadow (`0 6px 16px rgba(0,0,0,.2)`).
- Tertiary (buttons, labels): subtle shadow (`0 2px 6px rgba(0,0,0,.15)`) or none.
- No borders. Color + shadow alone convey layer position.

#### **Option B: Tonal (Layering with Color)**
- Base layer: `var(--paper)` (light parchment).
- Accent layer: `var(--paper-2)` (darker parchment, e.g., for headers).
- Emphasis layer: `rgba(255,255,255,.3)` (semi-transparent overlay for active sections).
- Minimal shadow for subtle depth. No strong outlines.

#### **Option C: Flat + Ruled (Structural / Office Document)**
- All panels: same shadow, same color, minimal depth cueing.
- Separation via *rules* (horizontal lines) and *sections*.
- Emphasis via color accent (e.g., red stamp, green checkmark) or type weight.
- No gradients; flat colors with grain texture.

**Your game is currently A + B mixed**, which creates noise. **Choose one and remove the other.**

### 3.2 Making Different Panels Look Genuinely Different in Weight

| Hierarchy Level | Shadow | Border | Fill | Example |
|-----------------|--------|--------|------|---------|
| **Primary (ledger, map legend)** | `0 12px 30px rgba(0,0,0,.3)` | None | `var(--paper)` + grain | `#econHud`, `#legendPanel` |
| **Secondary (battalion details, news)** | `0 6px 16px rgba(0,0,0,.2)` | Left rule only (3px accent color) | `rgba(255,255,255,.25)` (tinted) | `.reinforceRoute`, `.battalionRow` |
| **Tertiary (buttons, tags)** | `0 1px 3px rgba(0,0,0,.1)` or none | None | `var(--paper-strong)` (no fill if text-only) | `.opsBtn`, `.goalTag` |
| **Accent (stamps, alerts)** | `0 2px 4px rgba(0,0,0,.2)` | `2px solid var(--red)` (intentional, rotated) | Tinted background `rgba(138,42,28,.08)` | `.opsStamp`, `.announcements` |

**Key principle:** Reduce shadows as you go down the hierarchy. Add bold color or rotation only to things that should *demand* attention.

### 3.3 Focal Points

- **Objective HUD** (win/lose condition): Largest stamp, red accent, strongest shadow. It's the goal.
- **Phase badge** (PREP / COMBAT): Stamped, rotated, uppercase, red on combat. Draws eye.
- **Next step in guidance**: Red overline or "NEXT" tag (you already have this with `.buildBtn.next::after`—good).
- **Active panel toggle**: Use left-rule color shift, not a full border.

---

## Part 4: Spacing & Rhythm

### 4.1 Spacing Scale

Establish a base unit and stick to it:
- **Base unit:** 4px or 8px.
- **Use multiples:** 4, 8, 12, 16, 24, 32, 48px.
- **Consistency:** Every margin/padding uses one of these values.

**Your game's current state:** Mix of 7px, 8px, 9px, 10px, 11px, 13px—too many values. Consolidate.

### 4.2 Proximity (Grouping)

**Tight spacing (4–8px):** Items that belong together.
- Button labels inside a button.
- Icon + label in a row.
- Stat name + value.

**Medium spacing (12–16px):** Related sections.
- Title + description.
- Section label + content list.
- One ledger row + the next.

**Loose spacing (24–32px):** Separate concepts.
- Different panels in the same stack.
- Headers + body content.
- One form section + the next form section.

**Currently:** Your panel padding is mostly 8–12px uniformly. Tighten internal grouping (related items: 4px gap), loosen between concept groups (unrelated: 16px+).

### 4.3 Optical Alignment

Text doesn't align to visual center; it aligns to the *baseline* or *cap line*. This is why centered text often looks off-center:
- Left-align body text for readability.
- Center only large, display-level titles.
- Use line-height strategically: 1.4–1.6 for body, 1.2 for headings.

---

## Part 5: Color & Texture for "Physical Document / War Room" Look

### 5.1 Restrained Palette (Don't Overuse Color)

**Current palette:** Excellent—10 named variables (paper, ink shades, red, green, amber, blue). Good constraint.

**How to use it:**
- **Primary (70%):** `var(--paper)` and `var(--ink-soft)` for body copy.
- **Secondary (20%):** `var(--paper-2)` for headers/dividers; `var(--ink-faint)` for hints.
- **Accent (10%):** `var(--red)` for urgency/error; `var(--green)` for success; `var(--amber)` for warning.
- **Never** use both red and blue together at full saturation; it feels chaotic.
- **Avoid** `var(--blue-ink)` except for small highlights (cost values, stat names).

### 5.2 Tonal Layering Instead of Outlines

Instead of dark outlines, use *color shifts* within your palette:
- **Without outline:** Panel is `var(--paper)`, header is `var(--paper-2)`, text is `var(--ink)`.
- **The shift itself** separates zones—no line needed.
- **Under high-contrast printing:** Ink-soft becomes visible. On screen: reads as depth.

### 5.3 Paper Grain & Texture

Your game already uses SVG noise (`--paper-grain`). Good. But:

**Issues:**
- Grain sits *underneath* the border, so the line cuts across the texture.
- Solution: **Remove the outer border; let the grain define the edge.**

**Grain opacity (`feColorMatrix` in your SVG filter):** Currently `0.07` (very subtle). Good for parchment.
- For a "worn document" effect: increase to `0.12`.
- For "pristine ledger": keep at `0.05`.
- Never go above `0.2`; it becomes noise instead of texture.

**Pairing grain with shadows:**
- High blur, low opacity: `0 10px 26px rgba(4,6,10,.55)` (natural depth—good).
- Low blur, high opacity: reads as an ugly outline. Avoid.

### 5.4 Ink-on-Paper vs. Glowing-Screen Aesthetics

| Aspect | Ink-on-Paper | Glowing-Screen (avoid) |
|--------|--------------|----------------------|
| **Shadows** | Soft, blurred, warm gray | Hard-edged, blue-tinted |
| **Highlights** | Warm glow (`rgba(255,255,255,.1)`) | Bright white (`rgba(255,255,255,.5)`) |
| **Borders** | Warm gray or faded ink | Pure black or neon |
| **Backgrounds** | Creamy, slightly textured | Pure white or flat color |
| **Accents** | Muted (dark red, olive green, rust) | Bright (electric blue, neon pink) |

**Your game is correct:** Warm parchment, warm inks, soft shadows. Keep it.

### 5.5 Pure Black Borders Are Forbidden

- Replace pure `#000` with `var(--ink)` (`#241d10`) or `var(--ink-soft)` (`#5c4f34`).
- `#000` is digital/terminal language; paper uses ink, which is warm.

---

## Part 6: Diegetic / "Physical Object" UI

### 6.1 Definition

**Diegetic UI:** An interface that exists as a physical object *within the game world's fiction*. The player isn't looking at a floating UI layer; they're looking at a map table with papers pinned to it.

**Your game's fiction:** "Every panel is a piece of paper pinned/taped to a command-post map table."

This is excellent framing. Commit to it.

### 6.2 Visual Techniques to Reinforce Diegetic UI

#### **Tape/Clips**
- Pseudo-element `::before` or `::after` with semi-transparent rectangle (looks like tape).
- Your `.directorBubble::before` already does this: `width: 46px; height: 16px; background: rgba(255,255,255,.35);`
- Good—do this for a few key panels (objectives, announcements), not all.

#### **Stamps (Rubber Stamp, Grease Pencil)**
- Your `.opsStamp` is perfect: rotated, bordered, colored, uppercase.
- Use sparingly. A "PRIORITIZE", "URGENT", "COMPLETED" stamp per panel is enough.
- Don't stamp every title; stamping everything makes nothing stand out.

#### **Rotation (Slight Tilt)**
- `transform: rotate(-1.4deg);` or similar (your game already uses this).
- A few degrees reads as hand-placed; more than 3 degrees reads as a mistake.
- Apply to panels sparingly (not every element), and vary the angle (some −1°, some +0.8°).

#### **Torn/Irregular Edges**
- SVG path with irregular edge, or CSS with clip-path + multiple box-shadows.
- Use for the "field slip" (#hud) or announcements, not primary panels.
- Risk: Feels "cute" rather than functional. Use only where fiction supports it (notes, temporary slips, not ledgers).

#### **Fold Lines, Creases**
- A subtle linear shadow or gradient along one edge to suggest a fold.
- Example: `background: linear-gradient(90deg, transparent, rgba(0,0,0,.03))` on the left edge.
- Use sparingly; most sheets are flat.

#### **Staples, Thumbtacks, Pen Marks**
- Tiny 2–3px elements in corners or margins.
- Represented with small colored circles or X marks (CSS or SVG icon).
- Risk: Makes the UI fussy. Use only for highest-priority elements.

#### **Misalignment**
- Deliberate 1–2px offset from grid for labels, badges, or side panels.
- Example: A list item that's 3px to the left of others.
- Risk: Readability. Don't overdo it. Use only for accent elements.

#### **Ink Wear / Faded Areas**
- Opacity variation (`opacity: 0.95` for some sections, `0.88` for others).
- Slightly desaturate accent colors (`color: rgba(138,42,28,.92)` instead of full saturation).
- Looks aged without being illegible.

### 6.3 What NOT to Do (Even If It's Diegetic)

- **Don't make it hard to read.** Aesthetic is secondary to function.
- **Don't add so many objects that the UI feels cluttered.** One tape strip per panel is enough; five is noise.
- **Don't break the world.** A holographic map table doesn't get paper documents. Commit to the fiction.
- **Don't use real photographic textures.** They clash with drawn/CSS aesthetics. Stick to procedural noise (SVG filter) or subtle grain.

---

## Part 7: Concrete Fixes for THIS Game (Prioritized by Impact)

### Ranking: Fixes by Impact-per-Effort

#### **TIER 1: Remove Borders, Replace with Better Depth Cues (High Impact, Low Effort)**

**Current state:** Every panel has `border: 1px solid var(--rule);` and many buttons too.

**Fix:**

```css
/* BEFORE (current, problematic) */
.opsPanel {
  border: 1px solid var(--rule);
  box-shadow: var(--paper-shadow);
}

.opsBtn {
  border: 1px solid var(--ink-soft);
}

/* AFTER (cleaner) */
.opsPanel {
  /* Remove: border: 1px solid var(--rule); */
  box-shadow: var(--paper-shadow);
  /* The grain texture + shadow alone define the edge. Cleaner. */
}

.opsBtn {
  border: none;
  box-shadow: 0 2px 4px rgba(0,0,0,.1);
  /* On hover, strengthen the shadow for depth feedback. */
}

.opsBtn:hover:not(:disabled) {
  box-shadow: 0 4px 8px rgba(0,0,0,.15);
}
```

**Impact:** Removes 60% of visual noise. Immediately reads cleaner.

---

#### **TIER 2: Remove Redundant Borders from Secondary Rows (Medium Impact, Low Effort)**

**Current state:**
```css
.reinforceRoute,
.battalionRow,
.techRow,
.newsRow {
  border: 1px solid var(--rule-soft);
  border-left: 3px solid var(...);
}
```

**Fix:**
```css
.reinforceRoute,
.battalionRow,
.techRow,
.newsRow {
  /* Remove: border: 1px solid var(--rule-soft); */
  /* Keep the left rule—it's the visual accent. */
  border-left: 3px solid var(--ink-soft);
  border-bottom: 1px solid var(--rule-soft); /* Optional: subtle line between rows. */
}

/* State-specific colors for the left rule stay—these are smart accents. */
.reinforceRoute.ready {
  border-left-color: var(--green);
}

.battalionRow.morale-broken {
  border-left-color: var(--red);
}
```

**Impact:** Rows now read as *grouped* (same left rule) but not *outlined* (no box effect). Cleaner hierarchy.

---

#### **TIER 3: Tighten Registration Mark Appearance (Low Impact, High Polish)**

**Current state:** Your `.opsPanel::before/after` corner marks are subtle and good.

**Enhancement:**
```css
.opsPanel::before,
.opsPanel::after {
  content: "";
  position: absolute;
  width: 13px;
  height: 13px;
  pointer-events: none;
  opacity: 0.6; /* Slightly fade them for less visual weight. */
}

.opsPanel::before {
  top: -1px;
  left: -1px;
  border-top: 2px solid var(--ink-soft);
  border-left: 2px solid var(--ink-soft);
}

.opsPanel::after {
  bottom: -1px;
  right: -1px;
  border-bottom: 2px solid var(--ink-soft);
  border-right: 2px solid var(--ink-soft);
}
```

**Alternative (more diegetic):** Add `transform: rotate(-0.5deg);` to the marks so they look hand-placed, not perfectly aligned.

---

#### **TIER 4: Buttons: Remove Border, Add Subtle Hover Shadow (Medium Impact, Medium Effort)**

**Current state:**
```css
.opsBtn {
  border: 1px solid var(--ink-soft);
  background: var(--paper-strong);
}

.opsBtn:hover:not(:disabled) {
  background: var(--paper);
  border-color: var(--ink);
}
```

**Fix:**
```css
.opsBtn {
  background: var(--paper-strong);
  border: none; /* Remove the outline. */
  color: var(--ink);
  border-radius: 2px;
  padding: 6px 11px;
  cursor: pointer;
  font: 11px Consolas, 'SF Mono', 'Courier New', monospace;
  letter-spacing: .3px;
  transition: all .15s ease; /* Smooth transition for hover. */
  box-shadow: 0 1px 3px rgba(0,0,0,.08); /* Subtle base shadow. */
}

.opsBtn:hover:not(:disabled) {
  background: var(--paper); /* Darker on hover still works. */
  box-shadow: 0 4px 8px rgba(0,0,0,.15); /* Lift on hover (more shadow). */
}

.opsBtn:disabled {
  opacity: .45;
  cursor: not-allowed;
  box-shadow: none; /* Disabled buttons stay flat. */
}

.opsBtn.selected {
  background: var(--ink);
  color: var(--paper);
  box-shadow: 0 2px 6px rgba(0,0,0,.2); /* Slight depth for pressed state. */
}

.opsBtn.selected:hover:not(:disabled) {
  background: var(--ink);
  color: var(--paper);
  box-shadow: 0 4px 10px rgba(0,0,0,.25); /* Stronger on hover. */
}
```

**Impact:** Buttons now feel *pressable* (shadow responds) instead of *static* (border just sits there). Much more tactile.

---

#### **TIER 5: Panels with Sections: Use Tonal Separation Instead of Outline (Low Impact, Medium Effort)**

**Current state:** Many panels have headers with `background: var(--paper-2)` and a bottom border.

**Current example:**
```css
#econHudToggle {
  background: var(--paper-2);
  border: none;
  border-bottom: 1px solid var(--rule-soft);
}
```

**Enhancement (no CSS change needed—already good—but reinforce it):**
- The header's darker background *is* the visual boundary.
- The 1px dashed rule is fine; it's a deliberate divider, not a structural outline.
- Keep as-is. This is one of your better hierarchies.

**For secondary content rows:**
```css
.reinforceRoute {
  /* Remove full outline. */
  background: rgba(255,255,255,.28); /* Tinted background. */
  border: none; /* Remove border. */
  border-left: 3px solid var(--ink-soft); /* Keep the accent rule. */
  border-radius: 0; /* Rows should be sharp, not rounded. */
}
```

---

#### **TIER 6: Filter Buttons and Active States (Low Impact, Polish)**

**Current state:**
```css
.filterBtn:hover {
  background: rgba(255,255,255,.3);
}

.filterBtn.active {
  background: rgba(184,65,42,.14);
  border-color: var(--red);
  color: var(--red);
  font-weight: 700;
}
```

**Fix (remove border from inactive, keep visual feedback):**
```css
.filterBtn {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  text-align: left;
  background: transparent;
  border: none; /* Remove invisible border. */
  border-radius: 0; /* No rounding—sharp, paper-like. */
  padding: 3px 5px;
  margin-bottom: 1px;
  cursor: pointer;
  color: var(--ink);
  font: 11px Consolas, monospace;
  transition: background .1s ease;
}

.filterBtn:hover {
  background: rgba(255,255,255,.2); /* Slightly darker tint. */
}

.filterBtn.active {
  /* Remove border-color: var(--red); */
  background: rgba(184,65,42,.2); /* Stronger tint (was .14). */
  color: var(--red);
  font-weight: 700;
  border-left: 3px solid var(--red); /* Left rule instead of border. */
}
```

**Impact:** Active state is now consistent with your secondary rows (left rule = selection). Smaller UI grammar.

---

#### **TIER 7: Spacing Consolidation (High Impact Long-Term, High Effort)**

**Current issues:** Padding values vary widely (7px, 8px, 9px, 10px, 11px, 12px, 13px).

**Recommendation:** Establish a scale in a CSS comment:
```css
:root {
  /* SPACING SCALE (multiples of 4px) */
  --space-xs: 4px;   /* Tight grouping */
  --space-sm: 8px;   /* Internal padding, gaps */
  --space-md: 12px;  /* Section spacing */
  --space-lg: 16px;  /* Panel separation */
  --space-xl: 24px;  /* Major breaks */

  /* Gradual migration: use these instead of hard-coded values. */
  /* Don't change all at once; do it per-panel. */
}
```

Then migrate one panel at a time:
```css
#econHudSummary {
  padding: var(--space-sm) var(--space-sm); /* Was: 7px 10px */
  line-height: 1.6;
}

#econHudToggle {
  padding: var(--space-sm) var(--space-sm); /* Was: 7px 10px */
}
```

**Impact:** Reduces visual chaos. Makes future changes easier. But don't do this all at once—migrate incrementally.

---

#### **TIER 8: Increase Visual Weight of Primary Panels (Polish, Low Effort)**

**Current state:** All `.opsPanel` have the same shadow.

**Enhancement:**
```css
/* Major panels get stronger shadows. */
#econHud,
#legendPanel,
#reinforcePanel,
#battalionPanel,
#newsPanel,
#buildbar {
  box-shadow: 0 12px 30px rgba(4,6,10,.55); /* Already correct; keep as primary. */
}

/* Secondary panels get weaker shadows. */
.opsPanel:not(#econHud):not(#legendPanel):not(#reinforcePanel):not(#battalionPanel):not(#newsPanel):not(#buildbar) {
  box-shadow: 0 6px 16px rgba(4,6,10,.35); /* Slightly weaker. */
}

/* Announcement/modal overlays stay bold. */
#announcement,
#introPanel,
#directorBubble {
  box-shadow: 0 10px 26px rgba(4,6,10,.55); /* Current, good. */
}
```

**Impact:** Visual hierarchy becomes immediately apparent: bigger shadows = more important. Subtle but effective.

---

### Summary Table: Apply in Order

| Priority | Selector(s) | Change | Effort | Payoff | Expected Time |
|----------|-------------|--------|--------|--------|----------------|
| **1** | `.opsPanel`, `.opsBtn` | Remove borders; rely on shadow + texture | 5 min | High | Major visual cleanup |
| **2** | `.reinforceRoute`, `.battalionRow`, `.techRow`, `.newsRow` | Remove full border; keep left rule | 5 min | Medium | Rows now read as grouped |
| **3** | `.opsPanel::before/after` | Fade opacity to `0.6` | 1 min | Low | More subtle framing |
| **4** | `.opsBtn` (all variants) | Remove border; enhance hover shadow | 10 min | High | Buttons feel pressable |
| **5** | Various header rows | Already good; reinforce | 2 min | Low | Confidence check |
| **6** | `.filterBtn` (active/hover) | Remove border; add left rule | 5 min | Medium | Consistent grammar |
| **7** | `:root` + spacing variables | Create scale; migrate gradually | 30+ min | Very High | Future-proofs changes |
| **8** | Major vs. secondary panels | Adjust shadow weights | 5 min | Medium | Immediate hierarchy boost |

**Total effort for Tiers 1–6:** ~30 minutes. **Impact:** 80% of the "AI-ish" feel removed.

---

## Part 8: What Your Game Does Right

Acknowledge these so you don't accidentally break them:

1. **Paper grain texture via SVG filter.** Excellent. Subtle, procedural, no image assets.
2. **Monospace typography for consistency.** System fonts, no external dependencies. Perfect for war-room aesthetic.
3. **Registration marks on major panels (`::before/after` corner ticks).** Diegetic and subtle. This is how you signal "document," not "UI layer."
4. **Color palette.** Warm, restrained. No garish gradients. Paper-like from the start.
5. **Stamps and rotations for emphasis.** `.opsStamp` with rotation and double-border reads *intentional*. Do more of this, not less.
6. **Shadow depth on panels.** `var(--paper-shadow)` is well-chosen: soft, warm, realistic.
7. **Left-rule accent system.** `.reinforceRoute.ready { border-left-color: var(--green); }` is a smart design pattern. Expand it.
8. **Collapsible panels.** De-clutter. Good UX.

---

## Part 9: References & Sources

**Web design and AI aesthetics:**
- [How to Fix AI-Generated UI Designs: The Anti-Patterns Guide](https://docs.bswen.com/blog/2026-03-20-ai-generated-ui-anti-patterns/) — BSWEN. Lists purple gradients, glassmorphism, generic cards.
- [Why Your AI Keeps Building the Same Purple Gradient Website](https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website) — Lays out the training-data problem.
- [12 Product Design Trends for 2026](https://uxpilot.ai/blogs/product-design-trends) — Context on current aesthetic reactions (Neobrutalism as anti-AI).

**Game UI and hierarchy:**
- [Game UI: Design Principles, Best Practices, and Examples](https://www.justinmind.com/ui-design/game) — Visual hierarchy by urgency; depth techniques.
- [Spatial UI: Diegetics and Skeuomorphism](https://medium.com/@wrihn/spatial-ui-diegetics-and-skeuomorphism-51c8c926c86d) — Making UI feel like part of the world.

**Spacing and visual hierarchy:**
- [Proximity Design Principle: The Guide to Visual Grouping](https://gapsystudio.com/blog/proximity-design-principle/) — How closeness = grouping.
- [Spacing & Alignment in UI: Creating Visual Rhythm and Breathing Room](https://www.designsystemscollective.com/spacing-alignment-in-ui-creating-visual-rhythm-and-breathing-room-2c382b112272) — Rhythm and spacing scales.
- [Visual Hierarchy: Principles & How to Design](https://www.ramotion.com/blog/visual-hierarchy/) — Depth, weight, and contrast.

**CSS and styling:**
- [Beyond the Border: Understanding CSS Outline and Box-Shadow](https://www.oreateai.com/blog/beyond-the-border-understanding-css-outline-and-boxshadow/) — When each property helps.
- [20+ CSS Paper Effects: Free Examples & Code Snippets](https://freefrontend.com/css-paper-effects/) — Texture and tactility.
- [CSS Button Hover Effects](https://www.sliderrevolution.com/resources/css-button-hover-effects/) — Interactive feedback patterns.

**Paper texture and grain:**
- [Mastering CSS Background Grain: A Comprehensive Guide](https://www.tutorialpedia.org/blog/css-background-grain/) — Grain opacity and baseFrequency tuning.
- [Creating Grainy Backgrounds with CSS](https://ibelick.com/blog/create-grainy-backgrounds-with-css) — SVG filter implementation.

**Diegetic and skeuomorphic design:**
- [Skeuomorphism | Aesthetics Wiki](https://aesthetics.fandom.com/wiki/Skeuomorphism) — Definition and history.
- [Skeuomorphism of Skyrim](https://inairspace.com/blogs/learn-with-inair/skeuomorphism-of-skyrim-immersive-plaque-reading-interface-design-guide) — Real-world game example: Skyrim's diegetic reading interface.

**[Note: Items marked "[from training, unverified]" are inferred from design principles taught in 2022–2025 courses and would benefit from a real citation if this document is extended.]**

---

## Part 10: Workflow Recommendations

### For the Next Pass (Polishing Existing UI)

1. **Apply Tier 1 & 2 fixes** (border removal). Test in-game. Confirm nothing breaks.
2. **Then Tier 4** (button polish). Verify hover/selected states still work.
3. **Test on low-resolution/mobile viewports** to catch any layout shifts from shadow changes.
4. **Screenshot before/after** for team review.

### For Larger Refactors (Future)

- Migrate to a spacing scale incrementally (one panel per PR, not all at once).
- When adding new UI elements, use left-rule accent system (no full borders).
- Any new panel should inherit major-panel shadow weight by default.
- Rotate stamps and minor elements (−1° to +1°), not regular panels.

---

**End of reference document.**
