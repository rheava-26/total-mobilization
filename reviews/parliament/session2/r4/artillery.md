# Artillery Corps (Ground Bloc caucus), maiden paper, Second Session
*Master Gunner, "the King of Battle". Slug: `artillery`. Order Paper items #3, #2, #10.*

## 1. Opening speech
WHAT? Speak up, I'm deaf in the good ear.

Madam Speaker, I rise as the Master Gunner of the Artillery Corps, and I'll be brief, because every second I spend talking is a second I am not shelling something. I have read the First Session. I found a Ground Staff that "borrowed" my barrage, a Kinetic Arms board that wants a rail gun the length of Belgium, and an Air Council whose entire doctrine is "fly over it." Charming.

Here is my complaint. I commanded a battery of Thunders in Report 7. When my shell hit an enemy tank it hit *the tank*. One tank. The shell landed in a crowd of forty, made a lovely orange bang, and the forty went about their day. The explosion is **painted on**. I checked the code. It's a particle effect. In the King of Battle's own house.

A one-dimensional front is the best thing that ever happened to artillery. Everything stacks. The enemy stands in a line like a fence at a firing range. You have given me a line and a shell with no blast, and then asked why nobody builds me.

I ask for a bang that does something, a gun that finds the enemy's guns on its own, a barrage that falls where the fleet is about to land, and one very large volley. Fire for effect!

## 2. The Gap (evidence)
- **Shells and rockets have no blast radius.** Confirmed. In the projectile tick `title.html` L9166, a shell or rocket calls `GRID_E.firstHit(...)` and then `hurt(t, p.dmg ...)` on that **single** target, and `boomW(...)` is a cosmetic particle. A ground miss at L9167 does only `boomW` plus a crater. `groundBurst` (L6795) is terrain dust only. Compare `areaDamage` (L7544) and `splashDamage` (L7553), which exist and are used by bombs, strikes, `plasmaBurst` (L7568) and `subbomb` (L9056). So the Plasma upgrade "Thunder-P" (L3326, `plasarc`) splashes by my read, and the base Thunder does not. The base SPG is worse than its own refit in the one thing artillery exists to do.
- **Armour is a flat subtraction** in `hurt` (L4978+: `eff = max(1, dmg*HIT_BUFF*... - armor*ARMOR_NERF)`, `ARMOR_NERF = .55`). A Tempest rocket (dmg 9, L3190) or Hydra rocket (dmg 8, L3194) against Leviathan armour 22 loses roughly half its punch to subtraction. Thin rockets are the wrong tool for armour. The Thunder's 44 is fine.
- **Artillery has nothing to do on the Line.** Enemy landers spawn at `e.x` anywhere on the 6,000 px strip (`spawnEnemy` L5581, `e.hold = landY(...)`, L5604-5610), including **behind** your front, and they crawl to cities. Direct-fire tanks (range 300) only meet what walks into them. Only artillery (range 720-820) reaches the *far* part of a strip.
- **Counter-battery is a one-liner, not a system.** The ROADMAP mentions the term for silos (strike queue, L6304). Ground has a `firefinder` radar in r3. Nothing makes my guns prefer the enemy's guns. `threatWeight` (L4330) gives `THREAT_W_GROUND = 1.15`, nothing special for Pelters.
- **Bulwark is out of my reach.** It holds at `SPACE_Y()*.92` (L5641) and shell `ceil` is `.67` (L4493). Ground artillery cannot touch it. I won't pretend otherwise; Bulwark is for Rod, Rail and the sky.
- **Order Paper #10.** Nothing in ground artillery is blatantly overpowered. Report 8 and 10 won with air and sky ships. Useless today: base shell splash (above), MLRS and Hydra against armour (above).

## 3. Bills

### ARTILLERY-1: HIGH EXPLOSIVE
*Pitch.* A shell that explodes. I'd like that on the tin.
*Fantasy.* Fifty Thunders open up on a landed column. The whole stack goes up in one orange wave.

**Clerk's Annex**
- **Mechanic.** Friendly `shell` and `rocket` impacts call `splashDamage(x, y, r, dmg*0.6, true, ...)`: full damage to the direct target, 60% falloff splash to others. Radius Thunder 36 px, Tempest 20 px per rocket (four per ripple), Hydra 22 px, base MBT 0 (tanks stay direct-fire). Ground miss also splashes on the first ground enemy within r. Real science: HE fragmentation lethal radius against dismounted and soft targets, which is why a 155 mm shell kills a crowd.
- **Code.** `updProj` hit at L9166, ground branch L9167, `splashDamage` L7553, `shell` spawn L4874, `KICK_W` L4856. Tag `p.splash=r` at spawn from `d.range` class.
- **Size.** S. **TM1.**
- **Tree.** Base Thunder on `metals`/start; no node. Tempest keeps `rocketry`.
- **Tenet (Amendment I).** It is a **buff**, so it must say what the enemy does. Answer: landings are already stacked (Leviathan columns L5660, `pelter` swarms L6598). Fleet reacts through `scanEnemyIntel` (L5524): when `dominant==='artillery'` it weights shieldgen domes and spaced `colMul` landings plus gunships over your batteries. No power creep over direct fire; 60% splash is the cost-neutral number, and the 4.1 pass kept DPS neutral, so splash is the only new damage.
- **One-Button.** Pass. No attention at all; gets better the more you build.
- **Line.** The best case in the whole bill. Stacking on one axis is the thing splash is for.
- **Perf.** Splash through `GRID_E.forNear` is one grid query per impact; cap rockets to one splash per ripple tick. Inspector should check Report 7 volumes.
- **Interactions.** Kinetic Arms (their splash/Long Gun is separate: they own barrels, I own bursts). Physics for the shell airburst fuse.

### ARTILLERY-2: REGISTERED TARGETS (fire on the landing zone)
*Pitch.* The enemy tells us where it will land. It has to. Gravity.
*Fantasy.* The sky is falling, and an hour of shelling has already landed there.

**Clerk's Annex**
- **Mechanic.** A battery within range of a still-descending *ground* enemy (`e.ground && e.reentry`) fires at its predicted landing point `(e.x, e.hold)`. Shells are fused to airburst at 70% of descent; deals 50% damage while `reentry` (the existing vulnerability window). Descent takes several seconds at 400 px/s shell speed, ~2 s flight at range 780. Only SPG, Tempest, Hydra take the job; automatic, no setting. Real science: pre-registered defensive fires, aimed at a known spot before the enemy reaches it.
- **Code.** `acquireEnemy` L4374 (add `e.ground&&e.reentry` filter branch), `spawnEnemy` L5581, `splashDamage(..., hitReentry)` L7553 already supports it, `shell` ceil L4493, `doSkyfall`.
- **Size.** M. **TM1.**
- **Tree.** `rocketry`/base; Thunder-P inherits.
- **Tenet.** Buff, answered: re-entry damage is 50%, and the fleet's PHASE 3 already picks least-hit landing zones from the damage-taken map (`addDmgTaken`, L4992). A strong barrage pushes them to land elsewhere, which is what a scarier enemy does.
- **One-Button.** Pass; entirely automatic.
- **Line.** Strong. Landings beyond your tanks' range are the only enemy a line can't otherwise reach; artillery is the only ground arm with the reach.
- **Interactions.** Autofleet (landing logic), Air (their interceptors share the re-entry window; this complements). Sky Bloc's Rods hit the same window.

### ARTILLERY-3: COUNTER-BATTERY
*Pitch.* The King does not duel with the peasantry. He kills the other King.
*Fantasy.* A Pelter plasma arc lands in your city. Four seconds later the Pelter is a smoking hole.

**Clerk's Annex**
- **Mechanic.** When an enemy `plasarc` (Pelter, Earthbreaker escorts) fires, record its position as a "track" for 6 s. Friendly SPG/Tempest/Hydra within range count that enemy at `threatWeight x4` and, with a Radar or Ground's `firefinder` standing within 1,400 px, also fire at it when it would otherwise be out of acquire range. Real science: counter-battery radar backtracks the shell's arc to the gun.
- **Code.** `threatWeight` L4330, `acquireEnemy` L4374, Pelter L5609 (range 7.0 s rate, dmg 76), Radar in `BUILD_DEFS`. Reuses Ground's `firefinder` request (r3 ground.md); I add only priority.
- **Size.** S-M. **TM1.**
- **Tree.** Radar building (exists); no node.
- **Tenet.** Buff to a cost-bearing unit (arty is 55 HP, slow). Enemy answer: Pelters are 55HP×t and drop in swarms (L6598); my counter-battery is a reason for the fleet to bring more Pelters, not a free win.
- **One-Button.** Pass.
- **Line.** Works, because Pelters sit at a known `e.x`. Cannot work against Bulwark (exosphere, out of ceil); stated plainly.
- **Interactions.** Ground (firefinder), Fortifications Corps (bunkers eat the Pelter's first shots), Missiles (their silo "counterbattery" strike queue is a different thing).

### ARTILLERY-4: SIEGE ROUND (my half of the mixed army)
*Pitch.* The enemy has 1,700 HP and does not move. That is not a problem. That is a target.
*Fantasy.* A Leviathan crawls up, and the Thunders do what lasers can't: make it stand still and die.

**Clerk's Annex**
- **Mechanic (answers Order Paper #2).** Thunder shells gain **top-attack** against **anchored or slow** ground giants: ignore 60% of armour when `e.spd <= 8` or `e.anchored`; on those targets they also take +40% from a prior beam/plasma hit (heat is Energy's flag; I take the bonus, not the heat). The wall: artillery does **nothing** to fast things. Shell flight is ~2 s and `evadeNear` (L9164) already makes airborne enemies jink; a monoculture of Thunders gets eaten by air. One type still works, slowly. A mix shreds: AA for the air, lasers soften, guns land the kill. Real science: top-attack shells (BONUS, Excalibur, and HESH) hit the thin roof plate, not the glacis.
- **Code.** `hurt` armour line L4984, `spawnSuper` L5269 (Leviathan armor 22, hp 1700), Earthbreaker L5610 (armor 20, spd 4), `anchored` field; shell tag at L4874.
- **Size.** S. **TM1.**
- **Tree.** New node `siegeshell` under `def`: cost ~120 pre-1.6, not dangerous, combo: `directedenergy` for the heat bonus.
- **Tenet.** A buff, but narrow: only slow/anchored. The enemy answer: boss crawlers get escorts (L5660 already) and fast variants appear when the fleet detects an artillery-dominant army, per `scanEnemyIntel`.
- **One-Button.** Pass.
- **Line.** Good: anchored giants sit on the line and don't dodge.
- **Interactions.** Energy (heat), Armour Corps (tank shells stay direct fire), Autofleet.

## 4. Hot Take: THE KING'S THUNDER (Corps Fires)
*Pitch.* One hundred and fifty guns. One order. Give me a fire mission and the whole planet shakes.
**Clerk's Annex**
- **Mechanic.** Once ten or more arty/MLRS/Hydra are within 900 px of each other, they form a **Fires Brigade**. Every 20 s the brigade selects the densest enemy cluster in range and fires **one simultaneous TOT volley** (all rounds timed to land together by flight time). Cooldown shared; automatic. Real science: time-on-target and "fire for effect", where first-round-all-guns leaves no time to scatter.
- **Code.** `GRID_E` cluster density, strike auto logic at L6304 (the ⟳ AUTO silo targets "the densest enemy cluster"), `splashDamage` L7553.
- **Size.** M-L. **TM1.**
- **Tree.** Shares Ground/Kinetic's solver (r3 kinetic-arms Motion 2); I supply only the trigger. Defer to them on the solver.
- **Tenet.** A buff. Enemy answer: Brigades are visible, so Pelters and Earthbreakers target them first (L6598), and the fleet reads "mass battery" as dominant.
- **One-Button.** Pass; no per-gun attention.
- **Line.** Uses the line's density.
- **Risk.** Super-linear scaling, so an Inspector should test it.

## 5. Alliances sought
**Kinetic Arms (Ordnance Board):** I take bursts, you take barrels; one TOT solver, yours. **Ground Staff (bloc whip):** firefinder and the grunts' ground-guard. **Armour Corps:** direct fire for you, indirect for me. **Fortifications Corps:** you hold, I reach. **Energy:** heat is yours; I'll cash it. **Autofleet:** please land in a heap. **Rail and Air:** I regret nothing.
