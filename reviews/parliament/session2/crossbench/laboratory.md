# The Member for the Laboratory (crossbench)

## 1. Maiden speech

Madam Speaker, I have built hardware that lives in vacuum, so forgive me if I walk into your sky and start measuring. You have six atmospheric bands, and that is lovely. But look at what lives in them. A laser does 22 damage whether it fires through ten kilometres of weather or through nothing. A "satellite" parks above its launch building like a lamp on a post. Re-entry is a slow fall with no heat, no glow and no trajectory you can read. Radar is a +12% range sticker. Power is a number. Your enemy arrives on the most predictable path in physics, a ballistic arc, and you never get to exploit that. I am not here to take your toys. Every bill I bring is a buff with a named enemy answer. Let the engineering make the decisions richer, not the clicks heavier.

## 2. What the code tells you

1. **Beams ignore the atmosphere entirely.** `weaponCeil()` (title.html:4488-4497) gives every beam one flat altitude ceiling (.62), and `fireProj` makes a beam an instant line with flat `dmg` (`k:'beam'`, ~4872). Laser Sat "Halo" (3217) does 22 at 1200 range, and the Laser Tower (3439) does 18 at 560. Nothing depends on the medium the beam crosses. In physics, the medium is the whole story for directed energy.
2. **"Satellites" are fixed lamps.** Rods "Zeus" (3219) and Picket "Warden-S" (3222) say "Holds above the Space Center that launched it". Orbital speed is 11-20 px/s. There is no orbital period, no pass and no coverage. A real constellation is about how many you have and which plane they fly in. Here count only adds DPS.
3. **Re-entry is a drift, not a trajectory.** Enemies spawn at `y:-50, vy:6..14` with `reentry:true` (5592) and accelerate (8638). Nobody sees where the fall ends. The v4 changelog admits the invasion plan was "never explained", yet a ballistic arc is exactly the thing you can predict and show.
4. **Radar detects nothing.** Radar (3406) is only `aura:{range:1.12}` plus +0.3 stability. A grep for a `stealth:` property or any detection range finds none. The Wraith is just an art function (2323). Sensors are the most interesting part of real air defence and the game has no sensor decisions.
5. **Power is IC.** Power Plant is `econ:{ic:7}` (3383). The Laser Tower, the Particle Cannon (3413, 130 dmg) and the Orbital Gun fire at a fixed `rate` forever. The idle-time economy has a good idiom in influence momentum, but the weapons never use it. A pulsed-power weapon should live on stored energy.
6. **Damage types are almost absent.** `hurt()` (4978) uses one armor number with `ARMOR_NERF .55` and `HIT_BUFF 1.3`. The only type interaction is shields: missiles overload them and beams bounce. Real materials work differently against kinetic, thermal and blast loads.
7. **The Line is already doing real work vertically.** The ceilings (`ceil: .64` Hailstorm, `.60` SAM, `0` rail/rod/particle, 4493-4496) are the best physics-adjacent idea in the file: reach is altitude-gated. Missile burn (`MSL_BURN_*`, ~4750) is proper ballistics. The physics is real where the player can see it and fake everywhere else.
8. **The fleet already adapts and jams.** `jammed()` (4977) and the `jammer` kind (5611, `jamR:360`) already exist, so an electronic-warfare layer has a home.

## 3. Bills

### Bill 1 — The Clear Shot Act (atmospheric windows for beams)

*Pitch.* Directed energy is mostly an air problem. A beam loses energy to absorption, scattering and thermal blooming in thick air, and almost nothing in vacuum. Give beams a "transmission" figure by band and the Laser Sat finally does what real hardware dreams of. Space-to-space shots are the best shots in the game. Ground lasers still work, but a laser battery on the surface should want high ground and good weather.

*Fantasy.* "I put Halo sats in a vacuum shell and the enemy's orbital fleet melted."

**Annex.**
- *Mechanic.* On each beam hit, multiply damage by T = f(shooter band, target band). Target above `SPACE_Y` with a space shooter gives ×1.5. Both ends below `TROPO_Y` give ×1.0, which is today's baseline, so nothing is nerfed. Vacuum is a bonus.
- *Enemy answer.* A new enemy "Obscurant Mortar" (proposed) is a drone that dispenses an aerosol cloud (real: smoke and chaff obscurants). Beams crossing the cloud lose 40%. The fleet already re-tasks against your heaviest element (v4.0.1), so heavy laser doctrine summons it.
- *Hooks.* `beamHit(`, `fireProj`, `weaponCeil`, `scanEnemyIntel`, `spawnEnemy` kinds.
- *Size.* M.
- *Physics.* Beer-Lambert extinction and thermal blooming.
- *Rules.* Tenet: pure bonus, plus enemy escalation. One-Button: fully automatic. Line: it uses the vertical bands.

### Bill 2 — The Orbital Shells Act (real constellations)

*Pitch.* Make "Holds above the Space Center" honest. A satellite sweeps. A single Zeus (rods "Zeus", today's orbital kinetic platform) passes over the strip periodically. A constellation of twelve gives continuous cover. Better still, I want one standing choice at each Space Center: LEO or GEO. A low shell is fast and close, with short dwell and high power. A high shell parks and watches one stretch of the strip forever. It is the same choice real satellite engineers make.

*Fantasy.* "Build forty sats and the sky is never empty."

**Annex.**
- *Mechanic.* Each sat gets an orbital phase, `x(t)` sweeping the strip with a period set by shell. Fire capability scales with count: expected on-station uptime = min(1, N·dwell/period). More sats means more uptime, with no cap. A LEO sat (default) sweeps and bonus-fires at range ×0.9. GEO parks with fixed range and a lower fire rate. Both are good, in different places.
- *Enemy answer.* Existing orbital hunters (Orbital Frigate, 3220) and the fleet's own railsats contest the shells.
- *Hooks.* `UNIT_DEFS` entries with `sat:true`, orbit patrol in `updUnits`, the lift cap.
- *Size.* L.
- *Physics.* Keplerian period T ∝ a^1.5, and the real LEO/GEO trade.
- *Rules.* Tenet: no caps, more sats means better cover. One-Button: default LEO, one global toggle. Line: vertical bands with the horizontal axis a sweep.

### Bill 3 — The Early Warning Act (ballistic prediction)

*Pitch.* A falling drop pod is the most predictable object in the game and the player cannot see it. An infrared early-warning satellite sees a hot launch plume or re-entry glow and solves the arc. I want ghost ellipses on the ground for every falling thing, and every anti-air and CIWS emplacement in the net auto-cued onto them. This also fixes "the plan was never explained".

*Fantasy.* "I knew where they'd land before they did."

**Annex.**
- *Mechanic.* New building "IR Early-Warning Station" (proposed): all `reentry:true` enemies within its aura show a predicted impact ellipse. Friendly interceptors gain lead time, meaning +15% effective AA range against reentering targets per station, stacking. Re-entry gets a glow signature (brighter = more heat).
- *Enemy answer.* Decoys already exist (v3.71) and become the counter: decoys look identical on IR. Enemy IR-spoof decoys appear in escalating numbers.
- *Hooks.* `e.reentry` (5592, 8638), `INTENT` bombard fields, the aura system (9272).
- *Size.* M.
- *Physics.* Ballistic trajectory prediction, IR plume and re-entry plasma signature.
- *Rules.* Tenet: it is information plus a buff, with a named answer. One-Button: automatic. Line: vertical drop onto the one-dimensional ground.

### Bill 4 — The Pulsed Power Act (capacitor banks)

*Pitch.* Laser towers don't burn IC; they fire at a flat rate forever. Real directed-energy hardware is built around energy storage: supercapacitors, flywheels, pulse-forming networks. I propose the Capacitor Bank. It charges slowly during quiet, like your influence momentum, and then dumps it into every beam, rail and particle weapon nearby when the wave arrives. The longer you wait, the bigger the opening salvo.

*Fantasy.* "We waited. Then everything fired at once."

**Annex.**
- *Mechanic.* Building "Capacitor Bank" (proposed): aura `rof` (existing field, 10087) applied to `elec` family weapons. Charge accrues at +10%/s of idle time toward ×2.5 fire rate, with decay over ~12s once combat begins, then recharge. Banks stack multiplicatively (as `rof` auras already do). Cost: about 220 IC, 50 MP.
- *Enemy answer.* The fleet's EMP (proposed) arcs at banks during bombardment. Busters already prioritise high-value buildings (7453), so banks join the list. Protect them or lose the surge.
- *Hooks.* `aura.rof`, `UNIT_FAM` elec (3377), `busterScore` (7453).
- *Size.* M.
- *Physics.* E = ½CV², pulse-forming networks.
- *Rules.* Tenet: pure buff with a priority target. One-Button: automatic. Line: ground building.

### Bill 5 — The Low Observable Act (radar cross-section)

*Pitch.* The Wraith has stealth in art only. Real stealth is detection range scaling with RCS^¼: halve the signature and the enemy sees you at 84% of the range. The game could have stealth planes that actually slip into the exosphere carrier's PD blind spot, and radar as the answer. Enemy radar-guided missiles need a lock. A single research line with one unit behaviour is enough.

*Fantasy.* "My wraiths flew in the dark."

**Annex.**
- *Mechanic.* Add `rcs` to UNIT_DEFS (Wraith .1). Enemy `acquireEnemy`-style detection for friendly targets is reduced to range·rcs^¼ unless they are within a detection aura. The enemy answer is "Sensor Drone" waves (proposed) with +250 range, which also jam your lock (the `jammer` kind, 5611).
- *Hooks.* `acquireEnemy` (4374), `spawnEnemy` kinds, `scanEnemyIntel`.
- *Size.* M.
- *Physics.* The radar range equation, R ∝ (σ)^¼.
- *Rules.* Tenet: it adds power with a named counter. One-Button: automatic. Line: air bands.

### Bill 6 — The Power Beaming Act (solar power satellites)

*Pitch.* My colleagues will call this economics, but it is real engineering studied since the 1960s. A solar satellite collects sunlight with no night and no weather, then beams microwaves down to a ground rectenna. In the game this is IC from the sky, scaled with volume. Build more sats, get more power. The beam path is a real vulnerability: the enemy hunts the sats.

*Fantasy.* "My cities run on the sun, and the sky is my powerplant."

**Annex.**
- *Mechanic.* New unit "Power Sat" (proposed, space domain, needs lift): +1.2 IC/s per sat delivered to the nearest Power Plant ("rectenna"). Stacks, no cap. Efficiency drops with cloud-cover bands (see Bill 1's medium logic) but ×1.0 baseline.
- *Enemy answer.* Enemy orbital hunters (railsat/lasersat grid, 4329) target power sats first.
- *Hooks.* `UNIT_DEFS` space units, `icR` accumulation (6314), `busterScore`.
- *Size.* M.
- *Physics.* Solar power satellite concepts, microwave power transfer at 2.45 GHz.
- *Rules.* Tenet: pure IC growth with a hunted weakness. One-Button: automatic. Line: orbital band.

## 4. Hot Take — The Kessler Doctrine

*Pitch.* Everything in orbit makes debris when it dies. In real physics, a collision in a shell fills that shell with fragments for years. The enemy lives in the exosphere. So make the exosphere a battlefield you can poison. Debris from destroyed orbital hardware lingers as a damage field in that band. It hurts everything in it, yours and theirs, so the player picks which shell to keep clean (LEO or GEO, from Bill 2) and which to sacrifice. A deliberate "seed" strike with your rods turns the enemy's staging shell into a shredder.

*Fantasy.* "I burned the sky, and the fleet had to fly through it."

**Annex.**
- *Mechanic.* Orbital kills drop `debris` tokens on a per-band counter (cheap, a number, not entities). Debris decays with half-life ~90s. Per-second damage to every orbital unit in the band proportional to the counter (hardened sats: armor ≥ 6 ignore it). One standing choice per Space Center decides your sacrificial shell.
- *Enemy answer.* The Hive fleet carries "Sweeper" drones (proposed) that clear debris, and bosses (annihilator, bombarder) are hardened and ignore it.
- *Hooks.* orbital `kind` deaths in `updEnemies`, the sat patrol code, `SPACE_Y`.
- *Size.* L.
- *Physics.* Kessler syndrome, collisional cascade.
- *Rules.* Tenet: it is a new power move for the player with a real cost to their own sats, not a cap. One-Button: automatic. Line: vertical bands only. **SCI-FI note:** the "Sweeper" is SCI-FI; its own logic is a drone that deorbits fragments with ion beams.
