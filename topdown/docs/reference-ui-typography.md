# Typography Reference for Total Mobilization: Topdown UI

**Purpose:** A concrete guide to stop the game's interface from reading as generic/AI-generated and reinforce its "command-post war-room" identity through deliberate typographic choices, all using **system fonts only** (no external downloads).

---

## 1. Why Default/Naive Choices Read as "Generic AI Slop"

### The Tells

- **One font at one weight everywhere** (e.g., all Consolas, 11px, regular weight)
  - No hierarchy, no visual "weight" differentiation between labels and data
  - Player's eye has no guidance on what to read first

- **Uniform sizing** (all body text 10–12px)
  - No visual distinction between section headers, body, and numeric readouts
  - Dense walls of identically-sized text lack rhythm

- **Centered text or centered alignment on everything**
  - Breaks left-to-right reading flow (military documents are left-aligned)
  - Reads as a greeting card, not a tactical ledger

- **No letter-spacing or too-tight letter-spacing** (especially on uppercase)
  - ALL-CAPS headers without spacing feel cramped and hard to scan (0.6–1.6px is too tight for uppercase)
  - Body text tighter than ~1.5 line-height looks airless

- **Monospace for everything**
  - Works for data/numbers, but when used for section headers and body labels, it reads "technical default" not "deliberate design"
  - Typewriter aesthetic requires intentional spacing + weight contrast, not just the font alone

- **No contrast in weight or style**
  - Using regular weight for both headers and body text forces reliance on size alone
  - Real designed UIs use **weight** + **size** + **case** + **spacing** together

### What "Generic AI Slop" Actually Signals

When a UI uses one monospace font at uniform weight/size with tight letter-spacing and centered alignment, it reads as either:
- A quick placeholder someone forgot to finish
- An algorithmic default with no human aesthetic judgment
- No opinion about what matters (players can't scan hierarchy)

**Cure:** Commit to a **specific physical place this UI lives** (in this case: a parchment-covered command table with typed ledger sheets, rubber stamps, grease pencils) and make every typographic choice reinforce that fiction.

---

## 2. Font Pairing & Hierarchy Fundamentals

### Maximum Number of Fonts

Use **2–3 font families maximum**:
1. **Display font** (headlines, section stamps, labels) — bold, uppercase, high contrast
2. **UI body font** (panel text, descriptions, body copy) — clean, high legibility, neutral
3. **Monospace font** (numeric data, ledger rows, teletype content) — for alignment + technical flavor

More than 3 and the UI reads cluttered; fewer than 2 and you lose hierarchy.

### Building Hierarchy: The Dimensions

Hierarchy is **NOT just size**. Use all four together:

| Dimension | Display | UI Body | Monospace Data |
|-----------|---------|---------|---|
| **Font Family** | Condensed or stencil-adjacent sans | Clean, neutral sans | Monospace (tabular) |
| **Size** | 16–24px (headers), 10–13px (labels) | 11–13px | 11–12px (with tabular-nums) |
| **Weight** | 700–900 (bold, heavy) | 400–500 | 400 (regular) |
| **Case** | UPPERCASE + letter-spacing | Title Case or lowercase | UPPERCASE or Mixed |
| **Letter-Spacing** | 0.8–1.6px (opens it up) | –0.02–0.1em (tight, readable) | 0.3–0.5px (data alignment) |
| **Line-Height** | 1.0–1.2 (tight headlines) | 1.5–1.6 (body, UI text) | 1.5+ (data rows, legibility) |

### The Rule: Size First, Weight Second, Spacing Third

1. **Large size** (14px+) + bold weight (700+) = immediate visual priority
2. **Medium size** (12–13px) + regular weight (400–500) = secondary content
3. **Small size** (10–11px) + regular or bold = labels, captions, data

**If all three are equal, there is no hierarchy.**

### Case & Spacing (All-Caps Pattern)

- **ALL-CAPS headers with letter-spacing (0.8–1.6px):** Reads as "official stamp" or "label." Works for:
  - Section headers (`.opsStamp`, `.buildCatLabel`)
  - Button labels
  - Legend categories
  - Urgency/status indicators

- **Sentence case or lowercase body:** Reads as "narrative" or "content." Use for:
  - Descriptive text
  - Numeric ledger rows
  - Flavor text, lore

**Never use ALL-CAPS for body text without letter-spacing. It's unreadable.**

---

## 3. System Fonts Available (No Downloads, Cross-Platform)

### The Constraint

Your game **cannot download webfonts** (Google Fonts, CDN, @font-face from external hosts — they're network-blocked). Every font must ship with the OS or be synthesized in canvas.

### System Font Stacks (Copy-Paste Ready)

#### **Stack A: Generic UI Sans (Clean, Modern, Neutral)**
```css
font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
```

**What it does:**
- macOS 10.11+: San Francisco (proportional, clean)
- iOS: San Francisco (native)
- Windows: Segoe UI (Microsoft's modern system sans)
- Android/Linux: Roboto, fallback to Helvetica/Arial
- **Best for:** UI body text, buttons, legends, panel text

**Characteristics:** Modern, neutral, highly legible at 11–13px, thin to medium weight available on all platforms

---

#### **Stack B: Monospace (Typewriter, Ledger, Data)**
```css
font-family: Menlo, Consolas, "SF Mono", "Monaco", "Courier New", monospace;
```

**What it does:**
- macOS: Menlo or SF Mono (Apple's monospace, cleaner than Courier)
- Windows: Consolas (ClearType-optimized, modern)
- Fallback: Monaco, Courier New (on older systems)
- **Best for:** Numeric data, ledger rows, teletype text, code-like content

**Characteristics:** Fixed-width, all characters equal width, naturally looks "technical" or "typed"

---

#### **Stack C: Condensed/Impact Sans (Heavy, Urgent Labels)**
```css
font-family: Impact, "Arial Black", "Helvetica Black", Arial, sans-serif;
```

**What it does:**
- All platforms: Impact or Arial Black (extremely condensed, bold)
- **Best for:** Emergency alerts, big section headers, stamps

**Characteristics:** Ultra-condensed, heavy weight (900), reads as "URGENT" or "OFFICIAL"

**NOTE:** Impact lacks weight variation (only bold). Use sparingly for maximum-impact headers only.

---

#### **Stack D: Fallback Serif (Elegant, Historic — Use Sparingly)**
```css
font-family: "Garamond", "Palatino", "Georgia", serif;
```

**What it does:**
- Fallback serif (old-school, elegant)
- **Best for:** Historic/archival text, flavor (e.g., "war diary" passages)
- **NOT for:** Main UI labels (too formal for operations map)

**Characteristics:** High contrast, elegant, harder to read at small sizes

---

### Stack Recommendations by Use Case

| **Use** | **Stack** | **Size** | **Weight** | **Letter-Spacing** | **Line-Height** |
|---------|-----------|---------|-----------|-------------------|-----------------|
| Section Header (`.opsStamp`) | **A or C** | 10–11px | 700–800 | 1.2–1.6px | 1.0–1.2 |
| Panel Body Text | **A** | 11–12px | 400–500 | –0.02–0em | 1.5–1.6 |
| Numeric Data/Ledger | **B** | 11–12px | 400 | 0.2–0.4px | 1.5–1.6 |
| Big Alert/Urgent | **C** | 14–18px | 700–900 | 0.6–1.0px | 1.0–1.1 |
| Tooltip/Caption | **A** | 9–10px | 400 | 0.1–0.2px | 1.4–1.5 |

---

## 4. Military / Wartime / Technical Document Typography

### Real-World Military Document Aesthetics

**Modern military correspondence** (U.S. Army Regulation 25-50):
- **Font:** Arial 12pt (sans-serif, clean)
- **Alignment:** Left-aligned (not centered)
- **Case:** Mixed case (Title Case for headers, lowercase for body)
- **Emphasis:** Bold for key terms, all-caps for urgency/status only
- **Layout:** Ruled tables, columnar data, justified margins

**Historical military / wargaming documents:**
- **Typewriter aesthetic:** Monospace, uniform spacing (Courier, Courier New)
- **Stencil / military equipment marking:** Condensed sans-serif, bold (Army stencil fonts are block-style condensed)
- **Field briefing maps:** Hand-drawn or printed, handwritten notes in margins, rubber-stamp headers
- **Signal/telex communications:** All-caps, short lines, heavy use of abbreviations, monospace

### What to Adopt (Achievable with System Fonts)

✅ **Left-aligned body text** (never center body content)  
✅ **Monospace for typed ledger data** (inherently looks "official document")  
✅ **ALL-CAPS labels + letter-spacing** (mimics rubber stamps, stencil marks)  
✅ **Bold sans for headers** (official, authoritative)  
✅ **Ruled lines, borders, dashed dividers** (map/table aesthetic)  
✅ **Tabular numerals** (columns of data line up; looks like a ledger)  
✅ **Minimal color (ink-based palette)** (simulate actual paper + pen)  

### What NOT to Adopt (Breaks the Fiction)

❌ **Centered section headers** (reads as greeting card, not command post)  
❌ **Script or decorative fonts** (military docs are utilitarian)  
❌ **Proportional numerals in data tables** (numbers "wiggle" as values change; looks uncontrolled)  
❌ **Light weights (< 400)** (military documents use bold or regular, not hairline)  
❌ **Decorative ornaments or shadows** (add visual clutter, destroy clarity)  

---

## 5. Numbers & Data Typography

### The Problem

When numeric columns use **proportional figures** (variable-width digits), changing numbers cause the column to shift sideways. A timer counting down from 999 to 100 visually "jumps." Stats that update every frame look chaotic.

**Example (proportional figures — bad for data):**
```
Manpower: 234      ← "23" has different width than "456"
Manpower: 456      ← Column shifts right as numbers grow
Manpower: 78       ← Column shifts left as numbers shrink
```

**Example (tabular figures — good for data):**
```
Manpower: 234      ← All digits same width; column stays put
Manpower: 456      ← No horizontal jitter
Manpower: 78       ← Aligned perfectly
```

### CSS Solution: `font-variant-numeric: tabular-nums`

```css
/* Numeric data, stats, timers, ledger columns */
.ledgerRow, .stat-value, .timer {
  font-family: Menlo, Consolas, "SF Mono", monospace;
  font-variant-numeric: tabular-nums;  /* Enables fixed-width digits */
  font-size: 12px;
  font-weight: 400;
  letter-spacing: 0.3px;
  line-height: 1.6;
}
```

### How It Works

- **`font-variant-numeric: tabular-nums`** tells the browser to use the "tabular" variant of digits (if the font supports it).
- Monospace fonts (Consolas, Menlo) **always have tabular numerals** by default, but older proportional fonts (Arial, Georgia) may not.
- **If the font doesn't support it, nothing happens** (no error, just falls back to default).

### Browser Support

✅ All modern browsers (Chrome, Firefox, Safari, Edge 2016+)  
⚠️ Internet Explorer 11: Not supported (but falls back gracefully)  

### Practical Implementation

```css
/* econHudSummary: resource counts that update */
#econHudSummary {
  font-family: Menlo, Consolas, "SF Mono", "Courier New", monospace;
  font-variant-numeric: tabular-nums;
  font-size: 11px;
  line-height: 1.6;
  white-space: pre-line;
}

/* ledger rows in tech/production panels */
.techRow, .reinforceRoute, .battalionRow {
  font-family: Menlo, Consolas, "SF Mono", "Courier New", monospace;
  font-variant-numeric: tabular-nums;
  font-size: 11px;
}

/* timers, countdown, live stats */
.timer, .stat-value, .cost {
  font-family: Menlo, Consolas, "SF Mono", "Courier New", monospace;
  font-variant-numeric: tabular-nums;
  font-size: 12px;
  font-weight: 600;
}
```

### Alignment Tip

For right-aligned numeric columns (typical in tables), tabular numerals ensure the decimal points/units stay vertically aligned:

```
         Cost   ← Column header
  IC:    234
  MP:    1240
  Inf:   45
```

---

## 6. Concrete Recommendations for This Game

### Current State Analysis

**Read from `/topdown/index.html` `<style>` block:**

| Element | Current | Problem |
|---------|---------|---------|
| `.opsPanel` body | `font: 12px Consolas, ...` | Monospace OK, but uniform weight/size leaves no hierarchy |
| `.opsStamp` (headers) | `font-size: 10.5px; letter-spacing: 1.6px` | Good! Upper already at 1.6px; issue is size (10.5px too small for impact) |
| `.opsBtn` | `font: 11px Consolas, ...` | Monospace fine for buttons; size appropriate |
| `#hud` (top ticker) | `font: 11px Consolas, monospace; letter-spacing: .2px` | Monospace OK; ultra-tight spacing (0.2px) is fine for dense data |
| `#econHudSummary` | `font: 11px Consolas, monospace; line-height: 1.6` | Good line-height; **missing `font-variant-numeric: tabular-nums`** |
| Panel headers/titles | All caps + letter-spacing (1.2–1.6px) | Correct pattern! Spacing opens it up |

### Current Strengths

✅ Already using monospace (Consolas) as base — correct for "typewriter/ledger" aesthetic  
✅ Already using ALL-CAPS + letter-spacing for headers — correct military stamp look  
✅ Already using 10–13px range for small/dense UI — appropriate for a war map  
✅ Line-height 1.5–1.6 on body text — readable  
✅ Paper-colored background + parchment grain — reinforces physical document fiction  

### Weaknesses & Fixes

#### **Issue 1: No Clean UI Sans for Label Text**
**Current:** Monospace everywhere (Consolas)  
**Problem:** Monospace is great for data, but forces all text to feel equally "technical." Labels (resource names, unit types, etc.) should feel slightly more human/readable.

**Fix:** Introduce Stack A (UI sans) for non-numeric labels; keep Consolas for numeric content only.

```css
/* BEFORE: All Consolas */
.resourceLabel { font: 11px Consolas, monospace; }
.resourceValue { font: 11px Consolas, monospace; }

/* AFTER: Sans for labels, monospace for data */
.resourceLabel {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.8px;
  color: var(--ink-soft);
}

.resourceValue {
  font-family: Menlo, Consolas, "SF Mono", monospace;
  font-size: 12px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
```

**Result:** Labels read as "legible commands," values read as "typed ledger data." Visual distinction without clutter.

---

#### **Issue 2: Numeric Data Not Using Tabular Figures**
**Current:** `#econHudSummary` uses Consolas but no `font-variant-numeric: tabular-nums`  
**Problem:** When resource values change, columns may shift slightly (depends on digit widths).

**Fix:** Add `font-variant-numeric: tabular-nums` to all numeric data rows:

```css
/* Add to existing rules */
#econHudSummary,
#econHudDetailInner,
.reinforceRouteCost,
.battalionRowStats,
.techBlurb,
.newsRow {
  font-variant-numeric: tabular-nums;
}
```

**Result:** Numeric columns stay perfectly aligned, no jitter. Data looks controlled.

---

#### **Issue 3: Header/Stamp Size Could Be Bolder**
**Current:** `.opsStamp` is 10.5px, 800 weight  
**Problem:** At 10.5px, even bold weight doesn't have much visual impact. In a dense UI, players' eyes skip over section headers.

**Fix (Optional Hierarchy Upgrade):**
```css
.opsStamp {
  font-size: 11.5px;  /* Up from 10.5px */
  font-weight: 800;   /* Keep bold */
  letter-spacing: 1.6px;
  text-transform: uppercase;
  /* Rest unchanged */
}

.opsStamp.important {
  font-size: 13px;    /* For emergency/big headers */
  font-weight: 900;   /* Heavier */
  letter-spacing: 1.8px;
}
```

---

#### **Issue 4: Distinguish "Instruction" Labels from "Data" Rows**
**Current:** All row text uses same font/weight/size  
**Problem:** Player can't quickly scan "what am I supposed to do here?" vs. "what is the value?"

**Fix:** Use weight + case distinction:

```css
/* Instruction/label row (like "NEXT FACTORY NEEDED:") */
.buildCatLabel {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  font-size: 10px;
  font-weight: 600;  /* Bold, not monospace */
  text-transform: uppercase;
  letter-spacing: 1.2px;
  color: var(--ink-soft);
}

/* Data row (like "Manpower: 234 / 500") */
.econRow {
  font-family: Menlo, Consolas, "SF Mono", monospace;
  font-size: 11px;
  font-weight: 400;  /* Regular */
  font-variant-numeric: tabular-nums;
}
```

---

### Proposed Type System (Complete CSS Snippet)

```css
:root {
  --font-ui-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  --font-data-mono: Menlo, Consolas, "SF Mono", "Monaco", "Courier New", monospace;
  --font-impact: Impact, "Arial Black", "Helvetica Black", Arial, sans-serif;
}

/* === DISPLAY / HEADERS === */
.opsStamp {
  font-family: var(--font-ui-sans);
  font-size: 11.5px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 1.6px;
  line-height: 1.1;
}

.opsStamp.alert {
  font-family: var(--font-impact);
  font-size: 13px;
  font-weight: 900;
  letter-spacing: 1.8px;
}

/* === UI BODY TEXT (Labels, Instructions) === */
.opsPanel {
  font-family: var(--font-ui-sans);
  font-size: 11px;
  font-weight: 400;
  line-height: 1.5;
  letter-spacing: -0.02em;
}

.resourceLabel, .unitLabel, .categoryLabel {
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.8px;
  color: var(--ink-soft);
}

/* === NUMERIC DATA / LEDGER === */
#econHudSummary,
#econHudDetailInner,
.ledgerRow,
.costValue,
.statValue {
  font-family: var(--font-data-mono);
  font-size: 11px;
  font-weight: 400;
  font-variant-numeric: tabular-nums;
  line-height: 1.6;
  letter-spacing: 0.3px;
}

/* === BUTTONS === */
.opsBtn {
  font-family: var(--font-ui-sans);
  font-size: 11px;
  font-weight: 500;
  letter-spacing: 0.3px;
  text-transform: none;
}

.opsBtn:hover {
  font-weight: 600;
}

/* === ALERTS / URGENT === */
.alert, .error, .warning {
  font-family: var(--font-impact);
  font-size: 12px;
  font-weight: 900;
  letter-spacing: 1.0px;
}
```

---

### Before → After Critique

#### **BEFORE (Current)**
```
MANPOWER           ← Consolas, 10.5px, 800wt, 1.6px letter-spacing
1240 / 2000        ← Consolas, 11px, 400wt, no variant-numeric
Ready              ← Consolas, 10.5px, 400wt
```

**Reads as:** Dense wall of monospace. All text feels equally important (no hierarchy). Data column might jitter.

#### **AFTER (Proposed)**
```
MANPOWER           ← UI Sans, 11.5px, 800wt, 1.6px letter-spacing
1240 / 2000        ← Monospace, 12px, 600wt, tabular-nums
Ready              ← UI Sans, 11px, 400wt
```

**Reads as:** Header pops (sans, bigger, bold). Data sits cleanly (monospace, tabular). Status is neutral (sans, regular). Eyes immediately know: *Header > Data > Status.*

---

## 7. Priority Ranking (Quick Wins)

Implement these in order of impact:

### **Tier 1: High Impact, Low Effort**

1. **Add `font-variant-numeric: tabular-nums`** to all numeric data CSS rules
   - Fixes: Numeric columns jitter
   - Time: 5 minutes
   - No visual changes needed; silent improvement

2. **Increase `.opsStamp` font-size from 10.5px → 11.5px**
   - Fixes: Headers feel too small, players miss them
   - Time: 1 minute
   - Impact: Immediate visual hierarchy improvement

3. **Define CSS custom properties (variables) for font stacks**
   - Fixes: Consistency, easier to audit and modify
   - Time: 10 minutes
   - Impact: Foundation for all future type refinements

### **Tier 2: Medium Impact, Medium Effort**

4. **Introduce UI sans-serif for labels / instructions**
   - Separate monospace (data) from sans-serif (labels)
   - Time: 30–45 minutes
   - Impact: Major clarity gain; "what to do" vs. "what is the state"

5. **Add `.resourceLabel`, `.unitLabel` classes with sans-serif + uppercase + letter-spacing**
   - Refactor monospace label text to use sans
   - Time: 15–20 minutes
   - Impact: More scannable, professional

### **Tier 3: Refinement (Optional)**

6. **Create `.opsStamp.alert` variant** for emergency/urgent headers
   - Use Impact font, 13px, 1.8px letter-spacing
   - Time: 10 minutes
   - Impact: Draws eye to critical info

7. **Tighten letter-spacing on UI sans body text** to –0.02em
   - Trade: Slightly denser, but more readable and "intentional"
   - Time: 5 minutes
   - Impact: Subtle polish; reduces "generic" feel

---

## 8. Specific CSS Updates for This Game

### Add to `:root` color + font variables:
```css
:root {
  /* Existing color vars ... */
  
  /* Font families */
  --font-ui-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  --font-data-mono: Menlo, Consolas, "SF Mono", "Monaco", "Courier New", monospace;
  --font-impact: Impact, "Arial Black", "Helvetica Black", Arial, sans-serif;
  
  /* Optional: font size scale (1.125 ratio: major second) */
  --fs-caption: 9px;
  --fs-small: 10px;
  --fs-base: 11px;
  --fs-medium: 12px;
  --fs-large: 13px;
  --fs-xlarge: 14.5px;
  --fs-headline: 16px;
}
```

### Update `.opsPanel` (body base):
```css
.opsPanel {
  position: fixed;
  background-image: var(--paper-grain), linear-gradient(175deg, var(--paper) 0%, var(--paper-2) 100%);
  background-blend-mode: multiply, normal;
  border: 1px solid var(--rule);
  box-shadow: var(--paper-shadow);
  color: var(--ink);
  
  /* UPDATED: Use UI sans for body, not monospace */
  font-family: var(--font-ui-sans);
  font-size: var(--fs-base);
  font-weight: 400;
  line-height: 1.5;
  letter-spacing: -0.02em;
}
```

### Update `.opsStamp`:
```css
.opsStamp {
  display: inline-block;
  text-transform: uppercase;
  font-weight: 800;
  
  /* UPDATED: Larger, use sans, wider letter-spacing */
  font-family: var(--font-ui-sans);
  font-size: 11.5px;  /* up from 10.5px */
  letter-spacing: 1.6px;  /* unchanged */
  
  color: var(--red);
  border: 2px solid var(--red);
  border-radius: 2px;
  padding: 2px 7px;
  transform: rotate(-1.4deg);
  background: rgba(138, 42, 28, .07);
  mix-blend-mode: multiply;
  white-space: nowrap;
}
```

### Add tabular numerals to data rules:
```css
#econHudSummary {
  padding: 7px 10px;
  font-family: var(--font-data-mono);
  font-size: 11px;
  line-height: 1.6;
  font-variant-numeric: tabular-nums;  /* ADD THIS */
  white-space: pre-line;
  color: var(--ink);
}

#econHudDetailInner {
  padding: 2px 10px 10px;
  border-top: 1px dashed var(--rule-soft);
  font-family: var(--font-data-mono);
  font-size: 11px;
  line-height: 1.6;
  font-variant-numeric: tabular-nums;  /* ADD THIS */
  white-space: pre-line;
  color: var(--ink);
}

.battalionRowStats {
  color: var(--ink-soft);
  font-size: 10.5px;
  line-height: 1.4;
  font-variant-numeric: tabular-nums;  /* ADD THIS */
}

.techRow {
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding: 8px 9px;
  background: rgba(255,255,255,.28);
  border: 1px solid var(--rule-soft);
  border-left: 3px solid var(--ink-soft);
  font-family: var(--font-data-mono);  /* ENSURE */
  font-variant-numeric: tabular-nums;  /* ADD THIS */
}
```

### New classes for semantic distinction:
```css
/* Labels and instructions (use sans-serif, uppercase, bold) */
.labelText, .resourceLabel, .unitLabel, .categoryLabel {
  font-family: var(--font-ui-sans);
  font-size: 10px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.8px;
  color: var(--ink-soft);
}

/* Numeric values (use monospace, tabular) */
.statValue, .costValue, .ledgerValue {
  font-family: var(--font-data-mono);
  font-size: 12px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  color: var(--ink);
}

/* Button text (use sans, center) */
.opsBtn {
  font-family: var(--font-ui-sans);
  font-size: 11px;
  font-weight: 500;
  letter-spacing: 0.3px;
  text-transform: none;
}

/* Emergency/urgent headers */
.opsStamp.urgent {
  font-family: var(--font-impact);
  font-size: 13px;
  font-weight: 900;
  letter-spacing: 1.8px;
}
```

---

## 9. Testing & Validation Checklist

- [ ] Resource counts in `#econHudSummary` don't jitter when values change
- [ ] Section headers (`.opsStamp`) feel visually distinct from body text
- [ ] Numeric columns (costs, stats, manpower) remain perfectly aligned
- [ ] Labels read as "commands" or "categories" (not data)
- [ ] Data reads as "typed ledger" (not labels)
- [ ] No font files download (check Network tab in dev tools)
- [ ] Fonts render on Windows, macOS, and Linux (test locally or use BrowserStack)
- [ ] Letter-spacing improvements don't break line-wrapping on narrow viewports
- [ ] All-caps headers still legible at 11.5px on small screens

---

## 10. References & Sources

### Typography & Hierarchy
- [Type Scale & Modular Scales](https://www.artattackk.com/blogs/ui-ux/type-scale-ratio/) — Art Attackk: UI/UX hierarchy principles
- [Typographic Hierarchy in UI](https://medium.com/design-bootcamp/typographic-hierarchy-made-easy-understanding-type-scales-in-ui-24a694f1e0e8) — Medium: Jahidul Bin Rafiq
- [Figma Typography Guide](https://www.figma.com/resource-library/typography-in-design/) — Official typography fundamentals

### System Fonts & CSS
- [System Font Stack CSS](https://css-tricks.com/snippets/css/system-font-stack/) — CSS-Tricks: Production-ready stacks
- [Modern Font Stacks (GitHub)](https://github.com/system-fonts/modern-font-stacks) — Comprehensive system font references
- [Cross-Platform Web Fonts](https://www.ctrl.blog/entry/font-stack-text.html) — Ctrl Blog: Platform-specific font availability

### Tabular Numerals
- [font-variant-numeric MDN](https://developer.mozilla.org/en-US/docs/Web/CSS/font-variant-numeric) — Official W3C property reference
- [Tabular Numbers in CSS](https://iprodan.dev/l/font-variant-tabular-nums/) — Labs: Implementation patterns
- [Alignment with tabular-nums](https://theosoti.com/short/tabular-nums/) — Practical data alignment guide

### Military Typography
- [Military Fonts & Stencils](https://design.tutsplus.com/articles/34-best-military-fonts-army-stencil-fonts--cms-34915) — Envato Tuts+: Army stencil standards
- [U.S. Army Regulation 25-50](https://www.thesoldiersproject.org/what-font-does-the-military-use/) — Official military font spec (Arial 12pt)
- [Military Document Standards](https://designbeep.com/2025/06/10/free-military-fonts/) — DesignBeep: Historical military typography

### Game UI Design
- [Game Developers & Font Choice](https://www.developers.dev/tech-talk/game-developers-choose-their-fonts.html) — Developers.dev: Thematic typography for games
- [UI Design Best Practices 2026](https://www.designmonks.co/blog/best-fonts-for-ui-design) — Design Monks: Modern UI fonts and hierarchy

### Letter Spacing & Accessibility
- [Letter-Spacing for Readability](https://www.designyourway.net/blog/what-is-font-spacing/) — Design Your Way: Spacing fundamentals
- [WCAG Text Spacing](https://www.a11y-collective.com/blog/text-spacing-wcag/) — A11Y Collective: Accessibility standards (0.12em minimum)

---

## Appendix: Quick Reference Table

| Property | Display/Header | UI Body | Data/Monospace |
|----------|---|---|---|
| **Font Family** | UI Sans or Impact | UI Sans | Monospace |
| **Size** | 11–14px | 11–12px | 11–12px |
| **Weight** | 600–900 (bold) | 400–500 (regular) | 400 (regular) |
| **Case** | UPPERCASE | Title / lowercase | UPPERCASE or Mixed |
| **Letter-Spacing** | 0.8–1.8px | –0.02–0.1em | 0.2–0.4px |
| **Line-Height** | 1.0–1.2 | 1.5–1.6 | 1.5–1.6 |
| **font-variant-numeric** | none | none | `tabular-nums` |

---

**Document Status:** [from training, unverified] sources cross-checked against 2026 web design and game UI best practices; CSS examples tested conceptually but require in-game validation.

**Last Updated:** July 2026
