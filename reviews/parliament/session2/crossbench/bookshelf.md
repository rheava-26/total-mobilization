# The Member for the Bookshelf (`bookshelf`)

## 1. Maiden speech

Honourable friends, I came to this game as I come to any siege: looking for the third act. I found a magnificent first act, in which a nation builds itself into something terrible while a countdown ticks. I found a second act with real craft in it: an EMP, a bunker-buster salvo, a flagship that arrives with a whole fleet around it. Then the third act is a word in a string: `INVASION REPELLED`. The enemy has intents, phases and a stance it changes when you hurt it, and it never says a word to you. Your civilisation turns Verdant or Machine, and the game repaints the sky and moves on. There is a decision popup, written, tested and switched off. The sky gets a new colour and nobody in the story reacts. I propose to give the war a voice, the victory a cost, and the strange new society a face. Six bills and one madness. I ask the House to listen.

## 2. What the code tells you

1. **The opening is a proper overture.** `INTENT` (l.5126) gives each scenario a prep length, a `tip` and a boss schedule. Phases fire at fixed times: `enterInvPhase` (l.6039) runs an antimatter EMP at once, then Suppression, then All-Out at about 50s of invasion time (l.5787 area). The banner copy is good: "THE GRID GOES DARK". Drama is highest in the first minutes of the invasion.
2. **The enemy already thinks. The player never learns it.** `scanEnemyIntel` (l.5780-5840) reads your dominant defence, and the banner says "AUTOFLEET ADAPTING". It withdraws to orbit if you shred its landings twice (`fleetStance`, l.5822) and lifts its surviving troops off. This is the best villain behaviour in the file and it appears only as a four-second banner, never as a character, a decision or a consequence you can read.
3. **The mid-game sags.** After phase 3, `doWave` fires every `22 + rand*10` seconds (l.6544) until `G.fleet` reaches zero. That is a metronome. The supers are on a timer (`supers: [{t:34,'hive'},{t:60,'bombarder'},{t:90,'leviathan'}]`, l.5130). Bosses are scheduled, not earned, so they come regardless of what you did.
4. **The flagship is the only true set piece, and it is under-sold.** `carrier` spawns at `_carrierT = 25` (l.5410-5423) with its fleet. Killing it takes 25% of `G.fleetMax` and gives +15 stability (l.6870-6877). That is a real reversal, yet nothing follows: no stance change, no panic.
5. **Society is a paint job.** `society(kind)` (l.3549) sets six sky colours and a tint, and the same function is the whole of "what we became". The Capitulation Crisis (l.3844, 6615) is a real mechanic with real dread, but it has no human face. The `EVENTS` array (l.1090) holds one event, `EVENTS_LIVE = false`. A decision engine exists and is dormant.
6. **The ending is a tombstone.** `end()` (l.10408) shows `VICTORY — INVASION REPELLED` and a rank line plus stats. `ACHIEVEMENTS` has jokes ("Independence Day", "I need ammo, not a ride!") but no epilogue. The WW3 easter egg (l.7612) is the one place the game dares to be literary, and it is a joke.
7. **Your own forces already have a first-person voice nobody reads.** Allied Reinforcements (l.4600-4613) arrive on a banner, and `G.stats.killsByType` and `unitsLost` are tallied silently. The data for a chronicle exists in `G.stats`; no one reads it back to you during the fight.

## 3. Bills

### Bill 1 — The Dispatches (a war correspondent's voice)

*Pitch:* Every great invasion novel has the radio in the corner. Give the war a narrator. Every ~40 seconds the game files one dispatch, a single line generated from what is happening: "Fleet lost 62% of its landings on the western flank. Command is rethinking." "Outpost Nine has stopped answering." Not a menu, not a choice. A ticker of rumours, rendered in the banner style the game already has.

*Fantasy:* You are the nation at the centre of a story that the world is telling about you.

*Annex:*
- Mechanic: a line queue fed by `G.stats` (kills, unitsLost, citiesAlive, `G.fleetStance`, `G.invPhase`). One dispatch per ~40s, max 2 on screen, 5s each, 60 templates keyed to state thresholds.
- Hooks: `banner(` l.10397, `toast(`, `G.stats.killsByType`, `scanEnemyIntel` (l.5780), `tickFleetStance`.
- Size: **S**.
- One-Button: no input, pure readout. Pass.
- Tenet: touches no unit, no number. Pass.
- Line: none. Pass.

### Bill 2 — The Enemy Speaks (the fleet's side of the radio)

*Pitch:* The autofleet adapts and the player sees four seconds of banner. Let the fleet tell you its read of you at the moment it acts. "REPOSITIONING — HIGH-ALTITUDE FIRE IS COSTING US ASSETS" appears when it hides from your anti-orbital guns. Make the enemy intercepted chatter a persistent log. Players who mass guns learn they are being feared, and that is the power fantasy entire.

*Fantasy:* The enemy is afraid of what you built.

*Annex:*
- Mechanic: each `LINES` entry (l.5790) becomes a persistent "INTERCEPTED" panel with the last 5 entries; on `fleetStance` flip, add a line; on boss kill, add a line with a name. No cost, no buttons.
- Hooks: `LINES` l.5790, `fleetStance` l.5820-5835, `bossName` in `spawnSuper` l.5276.
- Size: **S**.
- One-Button: pure readout. Pass.
- Tenet: nothing denied. The enemy reaction already exists. Pass.
- Line: none. Pass.

### Bill 3 — The Breaking Point (a mid-game reversal tied to the flagship)

*Pitch:* A novelist knows the reader needs a moment where the enemy blinks. When the flagship `carrier` (existing enemy superweapon) dies, today the fleet loses 25% and nothing else changes. Make the fall a turning point. For 45 seconds every enemy wave is delayed, shield domes flicker, the remaining bombarders (existing enemy orbital platform) drop their wall grid and the whole planet hears it. Then the fleet answers with something worse: a new "Vengeance Wave" with a one-off boss. The player gets a breath and then a bigger enemy, which is exactly what the tenet wants.

*Fantasy:* You cut the head off, and the beast gets angrier.

*Annex:*
- Mechanic: on `kind === 'carrier'` death: `G.waveT += 45`; clear `wallGrid` on bombarders for 20s; at +45s spawn one extra `spawnSuper` pair and a banner "THE FLEET HAS NO ADMIRAL. IT HAS ONLY A GRUDGE." Enemy strength +20% for the rest of the match (`ESC`).
- Hooks: l.6870-6880, `G._carrierT` l.5410, `spawnSuper` l.5276, `ESC` constants.
- Size: **M**.
- One-Button: automatic. Pass.
- Tenet: enemy gets scarier after the player wins a beat; player army untouched. Pass.
- Line: bombarders hold in space band. Pass.

### Bill 4 — The Last Clause (epilogues that read your civilisation)

*Pitch:* The end screen is a tombstone. Write the epilogue. 12 paragraphs, each selected from what the player became: `G.society` (Verdant, Machine, dystopia), `G.stats.eraReached`, cities held, bosses killed. "They called us the Verdant Republic. In the end we were the only ones left who still remembered a forest." One paragraph, shown over the frozen final battlefield, shareable alongside the after-action dossier.

*Fantasy:* Your total mobilization becomes history; someone writes it down.

*Annex:*
- Mechanic: template lookup in `end()`; four slots (society, tech era, cities held, strike use) each pick from 3-4 sentences.
- Hooks: `end(` l.10408, `G.stats.society`/`eraReached` l.10411, `markEver`, `renderAAROverviewMeta`.
- Size: **S**.
- One-Button: pure readout. Pass. Tenet: n/a. Line: n/a.

### Bill 5 — The Regime (turn the dormant decisions on, sparingly)

*Pitch:* `EVENTS` (l.1090) exists and one event is written. `EVENTS_LIVE = false`. A war novel needs three or four moments where the state makes a choice that echoes. I propose a deliberately small batch of four, each firing once per match and each with a free default (if the player ignores it, the first option applies automatically after 20s). Choices cost IC or stability cap, never a toy.

*Fantasy:* You are a head of state who answers, once, for what you do.

*Annex:*
- Mechanic: 4 events: (a) first city lost, (b) first boss kill, (c) Capitulation Crisis start, (d) flagship destroyed. Each has 2-3 choices, each a modifier of at most ±10% stability cap or ±150 IC. Default pick on timeout so the One-Button Law holds.
- Hooks: `EVENTS` l.1090, `EVENTS_LIVE`, `checkEvents`/`fireEvent` l.1112, `chooseEvent`, `G.crisis` l.6615.
- Size: **M**.
- One-Button: one standing choice per event with an automatic default. Pass.
- Tenet: choices never cap or remove units. Pass.
- Line: n/a.

### Bill 6 — The Long Dark (a quiet beat before the worst)

*Pitch:* Storytellers need silence. After phase 3 the waves metronome (`22 + rand*10s`). Once per match, when the player has just beaten a large wave, the fleet goes silent for 40 seconds: no waves, no bombardment, an audio ducking, a single banner "THE SKY IS QUIET." At the end of the silence a wave of 3x the usual size lands from every flank at once. Players learn to prepare during silence; the dread is the point.

*Fantasy:* You hold your breath, and then you are ready.

*Annex:*
- Mechanic: after kill count passes `waveClears >= 6`, set `G._quietT = 40`; `doWave` blocked; then `doWave(it)` x3 simultaneously. Once per match, invasion/glass only.
- Hooks: `doWave` l.6544, `G.waveT`, `musDuck`, `ESC.POP_CEIL`.
- Size: **S**.
- One-Button: automatic. Pass. Tenet: enemy gets bigger, player denied nothing. Pass. Line: ground and air waves unchanged.

### Bill 7 — The Witnesses (cities that remember)

*Pitch:* Cities already have `capital`, districts and a defence tier (`bDef`). Let each city carry a name and a one-line epitaph written at the moment it falls, drawn from what killed it ("Port Alder: bombarded flat by an annihilator beam at 4:12"). The end screen lists them. A city that held gets a motto instead.

*Fantasy:* The places you saved, and the places you lost, have names.

*Annex:*
- Mechanic: when `ct.hp <= 0`, record `{name, time, killerKind}`; AAR page renders the list. Names from a fixed pool of 20.
- Hooks: `G.cities`, `damageDistrict`, `G.lostStructures`, `renderAARWorld` (near l.10647).
- Size: **S**.
- One-Button: readout only. Pass. Tenet and Line: n/a.

## 4. Hot Take

### Bill H — The Second Fleet (the enemy is not alone)

*Pitch:* Everything the game says is "one autofleet". Let the twist be that there are two. At ~70% of the first fleet's `G.fleetMax` destroyed, a second, older fleet arrives in the exosphere. It does not fight you at first. It fights the first fleet. For 60 seconds enemy waves stop and both fleets exchange fire in space. Then the second fleet turns its attention to you, with all-new unit types and a boss schedule that begins at the intensity the first one ended at. The player's army is not worth less; the match simply has a second war after the first.

*Fantasy:* You won the war you thought you were fighting.

*Annex:*
- Mechanic: when `G.fleet <= 0.3 * G.fleetMax`, instead of `end(true)`, set `G.fleet2 = round(G.fleetMax * 1.2)`; spawn "Elder" variants of existing kinds with `+40%` HP and different colours; 60s of both-fleets-in-space spectacle (decorative debris like the `decoy` at l.6879); then waves resume. Only on `invasion` intent and above. The objective HUD changes to "THE SECOND FLEET".
- Hooks: `done` condition l.6549, `end(true` l.6553, `G.fleet`/`G.fleetMax`, `spawnEnemy` l.5588, `INTENT`.
- Size: **L**.
- One-Button: fully automatic. Pass.
- Tenet: an overwhelming enemy, not a nerf; existing army remains valuable because the Elder kinds reuse the same altitude puzzle. Pass.
- Line: Elder units use the same bands; no new axis. Pass.
