# Naval Warfare Reference & Design Guide
**For Total Mobilization Top-Down Warship Overhaul**

*This document addresses the designer's complaint: warships currently "act like tanks in disguise" with "an effective range of 30 feet." Real warships fight at standoff range, move with momentum, turn in wide arcs, and are visually large vessels. This reference provides concrete real-world data and actionable game recommendations to fix all three.*

---

## 1. REAL NAVAL ENGAGEMENT RANGES — The "30-foot range" fix

### The Problem
Current naval units (gunboat/destroyer) max out at range **240–320 px**. In a game where a tank sits at **220 px range**, naval units outrange tanks only marginally. In real naval combat, ships engage at **vastly greater distances**, often without seeing their target.

### Real-World Reference Data

#### WWII Naval Gunnery
- **Battleship main guns**: 15–24 nautical miles (nm)
  - USS New Jersey 16" guns: 24 nm range
- **Destroyer 5" guns**: ~9.6 nm range
- **Torpedoes**: 3–11 nm (depending on speed setting)
  - Mark 15 (US): 6 nm effective, 15 nm maximum at reduced speed
  - Practical successful range: well under 10 nm (human skill, visibility, weather)

#### Modern Naval Combat (Present-Day/Near-Future)
- **Naval guns (127mm, modern)**: 23–37 km (12–20 nm) surface range
  - Oto Melara 127mm/54: ~30 km surface, 15 km anti-air
  - Mk 45 (US 5"/54): 23–37 km depending on barrel length
- **Anti-ship missiles** (the dominant modern threat — they fire beyond horizon):
  - **Exocet**: 40–200 km depending on variant (MM38: 40 km; MM40: 200 km)
  - **Harpoon**: 67+ nm (over-the-horizon standoff)
  - Modern trend: Missiles are **3–5× the range of guns**
- **Torpedoes**: 5–50 km depending on type (modern quieter and longer-legged than WWII)

**Key insight**: Real naval combat is **standoff engagement at extreme range**, often beyond visual/radar horizon. Ships fire missiles or guns from miles away, not meters. Opposing ships never come close enough to "slug it out" like the game currently does.

### Scaling for the Game

The game's map is roughly **100×100 km** in campaign scale (per CONCEPT.md). Individual unit ranges are **in pixels** (roughly 1:250 scale, or 1 px ≈ 250 m at map-scale, or 1 px ≈ 40 m at unit-combat scale — the exact ratio doesn't matter for this exercise, only the *relative* values). The task is **NOT to model real kilometers literally**, but to give naval units **dramatically longer engagement envelopes** than ground units so they clearly operate under different tactical rules.

#### Recommended Scaling Approach

Use **relative multipliers** to ground-unit ranges:

| Unit Class | Real Range | Game Proxy | Ratio to Tank (220 px) |
|---|---|---|---|
| **Tank/Artillery** | 15–40 km | 220 px | 1.0× baseline |
| **Destroyer (gun-only)** | 23–30 km | 550–650 px | 2.5–3.0× |
| **Frigate (gun-only)** | 20–28 km | 450–550 px | 2.0–2.5× |
| **Missile-armed Destroyer** | 70–200 km (missile) | 800–1200 px | 3.6–5.5× |
| **Missile-armed Frigate** | 40–150 km (missile) | 600–1000 px | 2.7–4.5× |
| **Gunboat (patrol craft)** | 10–20 km | 300–400 px | 1.4–1.8× |

**In the game's current terms:**
- Gunboat `range: 240` → suggest raising to **350–400 px**
- Destroyer `range: 320` → suggest raising to **550–700 px** if gun-only; **900–1200 px** if missile-armed variant exists

**Why these multipliers?** Real destroyers outrange tanks by 2.5–3.0× in gun-only combat (matching WWII & modern gun ranges); missiles extend that to 3–5× (anti-ship missiles). These gaps make naval units feel **genuinely different** in engagement envelopes while keeping the map playable (ships still need to commit to engagements, can't trivialize all land opposition from the map edge).

#### Weapon Differentiation
Introduce distinct **weapon kinds** for naval units to show the range/standoff progression:

1. **Naval gun (ballistic)** — reuse the `shell` weapon kind; range ~550–700 px for modern destroyer
2. **Anti-ship missile (homing)** — new weapon kind or new weapon def `antiship` (already exists in code for coastal batteries; upgrade naval units to use it)
   - Longer-legged, slower turn rate than aircraft missiles (cruise missile-like endurance)
   - Range 800–1200 px
   - Lower rate of fire (reload time for salvo doctrine)

#### Recommended Stats for Current Naval Units (Revised)

**Gunboat "Osprey" (Patrol Craft)**
- Current: `range: 240`, `speed: 60`, `turnRate: 2.2`
- Suggested revision:
  ```
  weapon: 'shell',
  range: 380,        // 2x baseline gunboat visual standoff
  speed: 80,         // fast patrol boat
  turnRate: 1.8,     // somewhat responsive for quick threat reaction
  dmg: 12, rate: 1.5 // lighter armament than destroyer
  ```

**Destroyer "Marchioness" (Gun-Armed Frigate/Destroyer)**
- Current: `range: 320`, `speed: 50`, `turnRate: 1.6`
- **Variant A (Gun-Only):**
  ```
  weapon: 'shell',
  range: 620,        // 3× tank, matches modern destroyer gun range
  speed: 45,         // slower than gunboat (heavier displacement)
  turnRate: 1.2,     // very slow turn (see Movement section)
  dmg: 28, rate: 1.8 // slower reload reflects larger shells
  ```
- **Variant B (Missile-Armed, future production variant):**
  ```
  weapon: 'antiship',   // uses new/existing homing missile
  range: 1000,          // ~4.5× tank range, over-the-horizon
  speed: 40,            // slower (heavier missile payload)
  turnRate: 1.0,        // slowest turn (sluggish destroyer)
  dmg: 50, rate: 0.8,   // massive hit, slow salvo rate
  ```

---

## 2. NAVAL MOVEMENT PHYSICS — The "acts like a tank" fix

### The Problem
Current naval units turn on a dime (moderate `turnRate: 2.2` / `1.6`), accelerate/reverse instantly, and move like ground vehicles (just on water). Real warships are **inertia-driven**, can't stop quickly, turn in **circles hundreds of meters across**, and follow a heading (hull-aspect), not a target.

### Real-World Warship Motion

**Turning circles:**
- Merchant ships: 3–4× their overall length
- Naval warships (frigates, destroyers): typically similar or **slightly larger** due to finer hull form
- Example: A 120-meter destroyer with a turning circle of 3–4 ships-lengths = 360–480 m turning radius (tactical diameter for 180° turn)
- **Effect**: A destroyer moving at speed cannot instantly face a new direction; it must **commit to a heading and arc through space** to reach a new bearing

**Acceleration/deceleration:**
- Real ships take minutes to reach full speed from rest or come to a stop
- Hull-aspect matters: a ship **cannot strafe sideways** or pivot in place
- Reverse is sluggish and dangerous (pitches bow, breaks rudder authority)

**Momentum:**
- A ship at full speed cannot make a sharp emergency course change without losing speed or broadsiding (hull perpendicular to desired direction → disaster)
- Speed and heading are **coupled**: tight turns require reduced speed

### Translating to Game Mechanics

The game's current system: `aim` (hull facing) + `vx/vy` velocity + `turnRate` (rad/s). This is actually **good** for warships — it already models heading + velocity separately. The fix is to **reduce `turnRate` dramatically** and make `speed` the **soft ceiling of turn rate** (faster speeds → wider turning circles).

#### Key Recommendations

**1. Reduce `turnRate` for all naval units**
- Current gunboat: `turnRate: 2.2` → suggest **1.2–1.4** (very slow turn, takes several seconds to pivot 90°)
- Current destroyer: `turnRate: 1.6` → suggest **0.8–1.0** (even slower, ponderous rotation)
- Compare: tank `turnRate: 3`, fighter jet `turnRate: 5`

**Why so slow?** Real destroyers turn at ~3–4 degrees/second maximum (some even slower). At 30 fps, a `turnRate` of **1.0 rad/s = ~57°/sec** in-game, which is already fast for a warship. Going lower (0.8 rad/s = ~46°/sec) is closer to reality.

**2. Add speed-coupling to turning (optional but high-impact)**
- Implement a soft-cap in `updateUnits` like:
  ```javascript
  // Reduce turn rate at high speed (momentum makes tight turns risky)
  const effectiveTurnRate = def.turnRate * (1.0 - 0.5 * (u.speed / def.maxSpeed));
  // At full speed: turn rate is 50% slower. At half speed: turn rate is 75% normal.
  ```
- This makes a destroyer at full sprint very committed to its heading, but at half speed it can turn reasonably.
- **Game-feel win**: Ships "feel" heavy and committed, not nimble.

**3. Reduce reverse capability**
- Add a `canReverse: false` flag to naval units, or
- Implement reverse as a very slow, dangerous maneuver (reverse speed = 0.3× forward speed, reversal must be deliberate and telegraphed)
- Alternatively, allow reverse but don't let units turn while reversing (hull control is degraded)

**4. Turret traverse independence (already present in code, but emphasize for naval)**
- The game already decouples `u.aim` (hull) from `u.turretAim` (gun traverse) — **keep and lean into this for warships**
- A destroyer can keep firing while executing a slow multi-second turn, because the gun turret swings independently of the hull
- This is **not** true for ground units with fixed forward guns; it IS true for ships with full-traverse turrets
- Implication: a naval unit doesn't need a perfect aim on its target to fire (turret reaches around); a tank does

**5. Larger radius for collision/separation**
- Current gunboat: `radius: 13` px; destroyer: `radius: 16` px
- Ships are *long*, even at top-down zoom; suggest **20–26 px radius** (larger silhouette, harder to cluster them)
- Combined with slow turns, this naturally spaces them out (they need room to maneuver)

### Visual Implementation Guidance

See Section 4 for visual design. The **wake** is the most important cue: a persistent trail behind the ship's stern makes the **forward-only momentum** immediately obvious to the player. No wake = no sense of velocity; obvious wake = "this thing is committed to a direction."

---

## 3. SHIP CLASSES & ROLES — Differentiation

### The Problem
Current roster has only two naval units (`gunboat`, `destroyer`), both using the same `shell` weapon and similar tactical profiles. Real navies field distinct classes optimized for different roles. The game should too.

### Real-World Naval Hierarchy

| Class | Displacement | Crew | Primary Roles | Armament | Speed |
|---|---|---|---|---|---|
| **Patrol Boat** | 100–300 t | 10–20 | Coastal patrol, escort | Small gun, ASM, small-caliber AA | 25–40 kts |
| **Corvette** | 500–2000 t | 40–80 | Coastal defense, escort | Naval gun, ASM, torpedoes, AA | 20–35 kts |
| **Frigate** | 2500–4500 t | 150–200 | Anti-submarine, ASW, general-purpose escort | Naval gun, ASM, torpedoes, VLS, AA | 25–30 kts |
| **Destroyer** | 6000–10000 t | 250–350 | Area air defense, ASW, power projection | Dual/multi guns, VLS missiles, torpedoes, AA | 28–35 kts |
| **Cruiser** | 10000–15000 t | 350–500 | Fleet flagship, power projection, area air defense | Multiple guns, VLS battery, AA suite | 32+ kts |
| **Carrier** | 40000–100000 t | 3000+ | Power projection, air operations, command | Flight deck, embarked aircraft, AA suite | 30+ kts |

### Game-Specific Recommendations: A Differentiated Naval Roster

Replace/expand from the current two units to a **3–4 unit core roster**:

#### 1. **Gunboat "Osprey"** (Patrol Craft / Fast Attack)
- **Role**: Harass convoys, escort, coastal patrol. Expendable, maneuverable, cheap.
- **Archetype**: Swedish Visby-class corvette or US Mark VI patrol boat (but at gunboat weight/cost)
- **Stats**:
  ```
  name: 'Gunboat "Osprey"', domain: 'naval', moveClass: 'naval', weapon: 'shell',
  hp: 100, armor: 3, speed: 85, range: 380, dmg: 12, rate: 1.5, 
  turnRate: 1.6, radius: 14, vision: 280,
  dispositions: ['aggressive'],
  ```
- **Doctrine**: Closes to ~2.5× tank range (aggressive standoff), fast enough to flank or retreat
- **Why different**: Smaller, fast, more agile (still slow for a warship, but NOT a tank)

#### 2. **Frigate "Sentinel"** (General-Purpose Escort) — *NEW*
- **Role**: Balanced escort, convoy protection, flexible tasking, ASW/AAW capable
- **Archetype**: Modern frigate (Germany's F125, France's FREMM, etc.)
- **Stats**:
  ```
  name: 'Frigate "Sentinel"', domain: 'naval', moveClass: 'naval', weapon: 'antiship',
  hp: 180, armor: 6, speed: 50, range: 750, dmg: 28, rate: 1.2,
  turnRate: 1.0, radius: 18, vision: 350,
  dispositions: [],  // balanced, not aggressive or defensive
  ```
- **Doctrine**: Holds at true standoff range (3.4× tank range), missile-armed for anti-surface; can also engage air/land via weapon type
- **Why different**: Larger, slower, more armored; reaches out much further (missile range); balanced doctrine

#### 3. **Destroyer "Marchioness"** (Capital Ship / Area Defense) — *REVISED*
- **Role**: Area air defense, fleet air/anti-missile cover, power projection, hard target
- **Archetype**: Modern destroyer (US Arleigh Burke, Germany's D-class, Japan's Kongo, etc.)
- **Stats** (missile variant):
  ```
  name: 'Destroyer "Marchioness"', domain: 'naval', moveClass: 'naval', weapon: 'antiship',
  hp: 240, armor: 10, speed: 45, range: 1000, dmg: 50, rate: 0.8,
  turnRate: 0.9, radius: 20, vision: 420,
  dispositions: ['holdGround'],  // defensive: screens positions
  ```
- **Doctrine**: Anchors in a defensive line (holdGround), very long-range missiles, tanky, dominates at standoff
- **Why different**: Largest, heaviest-hitting, slowest; longest range; explicitly defensive posture

#### 4. **Light Cruiser "Trident"** (Optional, Higher Tier) — *FUTURE*
- **Role**: Offensive capital ship, multi-target capability, power projection
- **Stats** (if implemented):
  ```
  name: 'Light Cruiser "Trident"', domain: 'naval', moveClass: 'naval', weapon: 'antiship',
  hp: 300, armor: 12, speed: 48, range: 1100, dmg: 60, rate: 1.0,
  turnRate: 0.85, radius: 22, vision: 450,
  dispositions: [],
  ```
- Not required for MVP naval overhaul, but template for a top-tier prestige unit

### Doctrine & Behavior Differentiation

**Aggressive (Gunboat only)**
- Closes to 32% of range (~120 px for gunboat's 380 px)
- Fast, risky, expendable
- Good for harassment, bad for holding position

**Balanced (Frigate, future cruiser)**
- Holds at 85% of range (~640 px for frigate's 750 px)
- Flexible, moderate risk
- Good for general escort and mixed threats

**Hold Ground (Destroyer, future capital ships)**
- Holds position, only engages enemies in sensor range
- Defensive, tanky, area-control focused
- Good for anchoring a fleet position or screen

---

## 4. TOP-DOWN VISUAL DESIGN — The "look like a large vessel" fix

### The Problem
Current visual representation: generic 8×16 px sprite markers, same silhouette for all units. A player can't distinguish a gunboat from a destroyer at a glance. The ship doesn't **read** as a moving vessel.

### Key Visual Principles

#### Hull Shape (Top-Down Silhouette)

Real warships from directly above show:
- **Elongated, pointed bow** (hydrodynamic form)
- **Flat or rounded stern** (where the propellers are)
- **Length-to-beam ratio ~7–10:1** (a destroyer is 120 m long, 15 m wide = 8:1; a tank is roughly 10m × 3.5m = 2.8:1)
  - **Naval vessels are dramatically longer and narrower than ground vehicles**
- **Superstructure block** amidships (stack/mast/bridge structure)
- **Deck features**: gun turrets (distinct circles/diamonds for each mount), missile launchers (linear VLS cells), funnels/masts

#### Deck Details (Per Class)

For a purely procedural Canvas2D drawing (no assets), draw class-specific silhouettes:

**Gunboat "Osprey"**
- Silhouette: ~32 px long, ~8 px wide (4:1 aspect)
- Single forward gun turret (small circle)
- Bridge/mast amidships (small rectangle)
- Color: light gray/blue
- Wake: thin, sharp line
- Visual size: smaller than destroyer, visually "fast"

**Frigate "Sentinel"**
- Silhouette: ~44 px long, ~12 px wide (3.7:1 aspect)
- Dual gun turrets (forward/aft, small circles)
- Larger superstructure block amidships
- Missile launcher (linear row of small squares across the deck, forward of mast)
- Color: medium gray
- Wake: moderate line
- Visual size: medium warship, balanced

**Destroyer "Marchioness"**
- Silhouette: ~56 px long, ~14 px wide (4:1 aspect)
- Dual gun turrets or integrated CIWS
- Massive superstructure (stack + bridge)
- Large VLS missile launcher (grid of ~8 small squares, prominent amidships)
- Color: darker gray
- Wake: thick, substantial line
- Visual size: large warship, imposing

**Scale guidance**: At operational zoom (which shows a ~1000×1000 px viewport), a destroyer should be **visually 3–4× longer than a tank** and clearly **much narrower** — it should *read immediately as a different vehicle class*, not just "a bigger tank."

#### The Wake (Critical Visual Cue)

The wake is the **single most important visual element** that conveys "this is a moving ship." Implement as:

1. **Persistent trail particle** behind the stern
   - Spawns every frame (or every 2-3 frames) at `{x: u.x, y: u.y, age: 0, life: 2.0}`
   - Rendered as a small semi-transparent circle, fading with age
   - Color: white/light blue, low opacity (~0.3–0.5)
   - Size scales with ship size: gunboat ~2 px, destroyer ~5 px

2. **Wake width varies by speed**
   - At full speed: dense trail, wide visual "wake"
   - At half speed: thinner trail
   - At idle/hovering: no wake particles generated
   - **Game-feel win**: Player sees ship commitment to current heading immediately

3. **Wake fades in 2–3 seconds**
   - Old wake particles disappear, so the trail doesn't clutter the map
   - But recent wake remains visible, showing the ship's **recent path**

#### Color Coding (Side Identification)

Use faction colors like ground units:
- **Player navy**: blue or player faction color
- **Enemy navy**: red or enemy faction color
- **Neutral (if any)**: gray

Turrets can be highlighted (brighter shade) to show they're firing/aiming.

#### Animated Details (Future)

Not required for MVP, but strong candidates for a graphics pass:
- Turret rotation: rotate individual gun/launcher symbols as the turret aims (decoupled from hull rotation)
- Smoke/steam: small rising particles from funnels when at high speed or combat
- Gun flash: small burst of light when firing (already implemented in code as `muzzleFlash`)
- Splashes near misses (projectile hits water nearby)

### Procedural Drawing Template (Canvas2D)

**Pseudocode for drawing a destroyer at (x, y) with hull angle `aim`:**

```javascript
// Hull: narrow rectangle, pointed bow
ctx.save();
ctx.translate(x, y);
ctx.rotate(aim);
ctx.fillStyle = 'hsl(210, 40%, 50%)';  // medium blue-gray
ctx.fillRect(-28, -7, 56, 14);         // hull: 56 px long, 14 px wide

// Bow point (optional, for visual clarity)
ctx.fillStyle = 'hsl(210, 50%, 60%)';
ctx.beginPath();
ctx.moveTo(28, 0); ctx.lineTo(32, -4); ctx.lineTo(32, 4); ctx.fill();

// Superstructure (bridge/stack)
ctx.fillStyle = 'hsl(210, 30%, 45%)';
ctx.fillRect(-8, -5, 16, 10);  // main structure

// Mast (thin line)
ctx.strokeStyle = 'hsl(210, 20%, 40%)';
ctx.lineWidth = 1;
ctx.beginPath(); ctx.moveTo(0, -5); ctx.lineTo(0, -12); ctx.stroke();

// VLS missile launcher (grid of 8 cells, forward of bridge)
ctx.fillStyle = 'hsl(210, 60%, 40%)';  // darker, distinct
for (let i = 0; i < 8; i++) {
  const cx = -14 + (i % 4) * 4;
  const cy = (i < 4) ? -3 : 3;
  ctx.fillRect(cx - 1.5, cy - 1.5, 3, 3);
}

// Forward gun turret
ctx.fillStyle = 'hsl(210, 50%, 55%)';
ctx.beginPath(); ctx.arc(8, 0, 2.5, 0, Math.PI*2); ctx.fill();

// Aft gun turret
ctx.beginPath(); ctx.arc(-8, 0, 2.5, 0, Math.PI*2); ctx.fill();

ctx.restore();

// Wake (particles list, drawn separately behind ship)
// ... (iterate world.wakeParticles, render as small circles with age fade)
```

Result: A clearly recognizable destroyer silhouette, visually distinct from a gunboat, with obvious deck features (missiles, guns, structure). Scales well to zoom levels.

---

## 5. APPLY TO THIS GAME — Prioritized Recommendations

### Summary of Changes by Priority

| Priority | Category | Change | Impact | Effort |
|---|---|---|---|---|
| **P0** | Range | Destroyer/Frigate range 550–1000 px | Ships stop fighting at "tank distance" | Low (data only) |
| **P0** | TurnRate | Naval turnRate 0.8–1.4 rad/s | Ships commit to headings, slow to pivot | Low (data only) |
| **P1** | Weapon kind | Add/use `antiship` homing missile for missile-armed variants | Range/standoff gameplay | Low (weapon already coded) |
| **P1** | Radius | Naval radius 18–22 px | Visual size scales, collision feels shiplike | Low (data only) |
| **P2** | Speed-turn coupling | Reduce turnRate by ~30–50% at full speed | Momentum simulation, feel | Medium (new logic in updateUnits) |
| **P2** | Reverse limit | Disallow reverse or make it 0.3× forward speed | Prevent instant retreats | Low (new flag or logic) |
| **P3** | Roster expansion | Add frigate, differentiate gunboat/destroyer further | Role differentiation, strategic depth | Medium (new unit def, production recipe) |
| **P3** | Wake visuals | Draw persistent trail behind ship | "Committed to heading" cue | Medium (particle system) |
| **P3** | Deck details | Procedural gun/missile silhouettes, superstructure | Visual read-at-a-glance | Medium (Canvas2D drawing) |
| **P4** | Turret rotation | Animate turret aim independent of hull | Already coded; expose in visual layer | Low (existing code) |
| **P4** | Speed classes | Frigate faster than destroyer; gunboat fastest | Role-appropriate performance | Low (stat tuning) |

### Immediate Changes (Next Agent Session)

**File: `game/units.js`**

1. **Revise gunboat**:
   ```javascript
   gunboat: {
     name: 'Gunboat "Osprey"', domain: 'naval', moveClass: 'naval', weapon: 'shell',
     hp: 100, armor: 3, speed: 85, range: 380, dmg: 12, rate: 1.5, 
     turnRate: 1.6, radius: 14, vision: 280,
     dispositions: ['aggressive'],
   },
   ```

2. **Revise destroyer**:
   ```javascript
   destroyer: {
     name: 'Destroyer "Marchioness"', domain: 'naval', moveClass: 'naval', weapon: 'antiship',
     hp: 240, armor: 10, speed: 45, range: 1000, dmg: 50, rate: 0.8, 
     turnRate: 0.9, radius: 20, vision: 420,
     dispositions: ['holdGround'],
   },
   ```

3. **Confirm `antiship` weapon exists** in `WEAPON_DEFS` (it does, line ~214):
   ```javascript
   antiship: {
     kind: 'homing', speed: 100, maxSpeed: 360, thrust: 500, turnRate: 2.2, life: 6,
     canTargetGround: true, canTargetAir: false,
   },
   ```
   ✓ Ready to use for destroyer & frigate variants.

**File: `game/units.js` (future, P3)**

4. **Add frigate unit**:
   ```javascript
   frigate: {
     name: 'Frigate "Sentinel"', domain: 'naval', moveClass: 'naval', weapon: 'antiship',
     hp: 180, armor: 6, speed: 50, range: 750, dmg: 28, rate: 1.2,
     turnRate: 1.0, radius: 18, vision: 350,
     dispositions: [],
   },
   ```

### Engagement & Behavioral Guidance

**Naval standoff fractions** (in `updateUnits.js` or equivalent): Consider adding a naval-specific standoff modifier:

```javascript
const NAVAL_STANDOFF_FRAC = 0.95;  // hold at 95% of range, not 85% (tanks)
// Rationale: ships are longer, their "reach" extends further; 
// 95% of a 620px range = 589px, a clear gap. Not as aggressive 
// as infantry's 32% (point-blank), but standoffish.
```

Alternatively, keep existing fracs but the *range increase* (380–1000 px) naturally creates the standoff feel.

**Standoff ranges in practice**:
- Gunboat (range 380, aggressive 32%): closes to ~120 px
- Frigate (range 750, balanced 85%): holds at ~640 px
- Destroyer (range 1000, holdGround): never chases, engages from anywhere on map in sensor range

**Screening & formation**:
Real doctrine: destroyers protect capital ships. Game parallel: pair destroyers with a carrier (future unit) or position them as a defensive screen. The holdGround disposition naturally supports this (destroyer anchors a position; other units rally to it).

### Visual Implementation (P3 Candidate)

**File: `engine/renderer.js` (or `game/render.js` if separate)**

Add a `renderNavalUnit()` function that draws class-specific silhouettes:

```javascript
function renderNavalUnit(ctx, u, screenX, screenY, zoom) {
  const key = u.def.key; // e.g., 'gunboat', 'destroyer', 'frigate'
  const aim = u.aim || 0;
  
  ctx.save();
  ctx.translate(screenX, screenY);
  ctx.rotate(aim);
  
  // Class-specific rendering
  if (key === 'gunboat') {
    drawGunboatSilhouette(ctx, u);
  } else if (key === 'frigate') {
    drawFrigateSilhouette(ctx, u);
  } else if (key === 'destroyer') {
    drawDestroyerSilhouette(ctx, u);
  }
  
  ctx.restore();
}

function drawDestroyerSilhouette(ctx, u) {
  // Hull
  ctx.fillStyle = u.side === 'player' ? '#4488ff' : '#ff4444';
  ctx.fillRect(-28, -7, 56, 14);
  
  // Bow
  ctx.fillStyle = u.side === 'player' ? '#5599ff' : '#ff5555';
  ctx.beginPath();
  ctx.moveTo(28, 0);
  ctx.lineTo(32, -4);
  ctx.lineTo(32, 4);
  ctx.fill();
  
  // Superstructure
  ctx.fillStyle = u.side === 'player' ? '#336699' : '#cc3333';
  ctx.fillRect(-8, -5, 16, 10);
  
  // VLS cells
  ctx.fillStyle = u.side === 'player' ? '#224499' : '#bb2222';
  for (let i = 0; i < 8; i++) {
    const cx = -14 + (i % 4) * 4;
    const cy = (i < 4) ? -3 : 3;
    ctx.fillRect(cx - 1.5, cy - 1.5, 3, 3);
  }
  
  // Turrets
  ctx.fillStyle = u.side === 'player' ? '#5599ff' : '#ff5555';
  ctx.beginPath();
  ctx.arc(8, 0, 2.5, 0, Math.PI*2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(-8, 0, 2.5, 0, Math.PI*2);
  ctx.fill();
}
// ... similar for frigate, gunboat
```

**Wake particle system** (in `game/particles.js` or equivalent):
- Spawn wake particles at ship position every frame if `speed > 10`
- Age out after ~2 seconds
- Render as semi-transparent white circles, fading by age

### Testing & Tuning Checklist

After implementation:

- [ ] Destroyer range 1000 px: can it reliably engage a tank 3× away? Yes → standoff feels real
- [ ] Destroyer turnRate 0.9: does it take ~3 seconds to do a 180° turn? Yes → commitment feels real
- [ ] Gunboat vs destroyer: visually distinguishable at zoom-out? Yes → role clarity achieved
- [ ] Wake visible behind moving destroyer? Yes → momentum cue clear
- [ ] Two destroyers holding a chokepoint: do they hold ground and just shoot from position? Yes → defensive doctrine works
- [ ] Player can't defeat a destroyer by ramming with tanks: tank closes, destroyer backs up? (or turns and keeps distance) Yes → standoff enforced

### Notes on Future Expansions

1. **Naval aviation**: Carriers (future) should spawn air units (fighters, ASW helicopters) with visual/mechanical tie to the carrier's position (basing/fuel). Out of scope for this pass.

2. **Submarine mechanic**: Stealth naval unit (torpedoes). Interesting but likely deferred; would need stealth/detection subsystem. Mark for P5 or later.

3. **Shore bombardment**: Naval gunfire support (NGFS) — destroyer shells hitting coastal targets. Possible with existing ballistic projectile code; needs a bombard order type and coastal target option. Useful for naval-vs-land interaction. Candidate for P4.

4. **Naval formations**: Multiple destroyers in a line, screening a frigate. Autonomy-layer candidate (battalion doctrine for naval units). Deferred until battalion-for-naval is designed (separate task).

5. **Counter-naval tactics**: ATGM/anti-ship battery on a coastal cliff should threaten a destroyer. Already coded (antiship weapon targeting). Test that coastal batteries work.

---

## Summary: What Changed

| Aspect | Before | After | Reason |
|---|---|---|---|
| **Range** | 240–320 px (marginally > tank) | 380–1000 px (2.5–4.5× tank) | Real standoff is far; creates distinct tactical layer |
| **Turn rate** | 1.6–2.2 rad/s (tank-like) | 0.8–1.4 rad/s (slow pivot) | Real warships turn in wide arcs; commitment matters |
| **Weapon** | All shell (ballistic) | Guns (shell) + missiles (antiship homing) | Real navies use missiles for standoff; range differentiation |
| **Roster** | 2 units (gunboat, destroyer) | 3–4 units (gunboat, frigate, destroyer, +carrier future) | Real navies have roles; enables strategic depth |
| **Visual** | Generic 8×16 sprite | Class-specific silhouette, deck details, wake | Player sees at a glance: "that's a big destroyer," not just a marker |
| **Doctrine** | Mixed aggression (no formation) | Aggressive gunboat, balanced frigate, defensive destroyer | Role enforcement; each class plays differently |
| **Movement feel** | Instant turn, teleport-steer | Momentum coupling, wide turning circle, commitment | Warship physics, not tank physics |

**Result**: Warships are no longer "tanks in disguise" — they are slow, long-range, committed vessels that dominate at standoff and struggle up close. Naval engagements happen at **multiple screen-widths away**, not arm's reach. A destroyer looks and *feels* like a heavy capital ship, not a fast tank.

---

## Sources (Real-World Data)

- [Range of Naval Gunfire - Smith & Wesson Forum](https://smith-wessonforum.com/threads/range-of-naval-gunfire.583824/)
- [Warship - Destroyer, Armament, Torpedo - Britannica](https://www.britannica.com/technology/naval-ship/Destroyers)
- [Navy Matters: Long Range Naval Guns](https://navy-matters.blogspot.com/2016/11/long-range-naval-guns.html)
- [Mark 15 Torpedo - Wikipedia](https://en.wikipedia.org/wiki/Mark_15_torpedo)
- [What makes anti-ship missiles like the Exocet more effective - Quora](https://www.quora.com/What-makes-anti-ship-missiles-like-the-Exocet-more-effective-than-traditional-naval-guns-in-modern-combat-scenarios)
- [The Development of the Modern Anti-ship Missile: Harpoon and Beyond - History Rise](https://historyrise.com/the-development-of-the-modern-anti-ship-missile-harpoon-and-beyond/)
- [A Quick Look at the Harpoon Missile - The Defense Post](https://thedefensepost.com/2025/12/18/harpoon-missile-guide/)
- [Harpoon - NAVAIR - Navy](https://www.navair.navy.mil/product/Harpoon)
- [AGM-84 Harpoon Anti-Ship Missile: Why It's Still a Naval Power - Defense Feeds](https://defensefeeds.com/military-tech/navy/anti-ship-missiles/harpoon-anti-ship-missile/)
- [What is Ship 'Turning Radius' - Virtue Marine](https://www.virtuemarine.nl/post/what-is-ship-turning-radius)
- [Understanding Turning Circle Of A Ship - Marine Insight](https://www.marineinsight.com/marine-navigation/understanding-turning-circle-of-a-ship/)
- [Turning Circle Diameter Trials for a Container ship - Ships Business](https://shipsbusiness.com/turning-circle.html)
- [Tactical Diameter - Wikipedia](https://en.wikipedia.org/wiki/Tactical_diameter)
- [Naval Architecture - Ship Design, Proportions, Shape - Britannica](https://www.britannica.com/technology/naval-architecture/Effect-of-shape-and-proportions)
- [Length : Beam Ratio's - Boat Design Net](https://www.boatdesign.net/threads/length-beam-ratios.10853/)
- [Understanding The Beam Of A Ship - Marine Insight](https://www.marineinsight.com/naval-architecture/understanding-the-beam-of-a-ship/)
- [Beam (Nautical) - Wikipedia](https://en.wikipedia.org/wiki/Beam_(nautical))
- [Weapons: Classic 76mm Naval Weapon - Strategy Page](https://www.strategypage.com/htmw/htweap/articles/202411200355.aspx)
- [127mm Mk 45 - Weaponsystems.net](https://weaponsystems.net/system/445-127mm+Mk+45)
- [54-caliber Mark 45 gun - Wikipedia](https://en.wikipedia.org/wiki/5-inch/54-caliber_Mark_45_gun)
- [In focus: the 127mm Mk 45 gun - Navy Lookout](https://www.navylookout.com/in-focus-the-127mm-mk-45-gun-that-will-equip-the-type-26-frigates/)
- [Frigates vs Corvettes: What are the Differences - Marine Insight](https://www.marineinsight.com/types-of-ships/frigates-vs-corvettes/)
- [Beyond the Battleship: Decoding the Destroyer, Frigate, and Corvette - Oreate AI Blog](https://www.oreateai.com/blog/beyond-the-battleship-decoding-the-destroyer-frigate-and-corvette-925774796ae4c222-dae747b56329f303)
- [Corvette vs. Frigate — What's the Difference - Ask Difference](https://www.askdifference.com/corvette-vs-frigate/)
- [Corvette vs Frigate vs Destroyer: Comprehensive Guide - Campaigning Info](https://campaigninginfo.com/corvette-vs-frigate-vs-destroyer-comprehensive-guide-to-naval-powerhouses-campaigning-info)
- [Frigate Vs. Destroyer Vs. Corvette - SSB Crack Exams](https://ssbcrackexams.com/frigate-vs-destroyer-vs-corvette/)
- [Destroyer vs Frigate - The Defense Watch](https://thedefensewatch.com/head-to-head/destroyer-vs-frigate-key-differences-explained/)
