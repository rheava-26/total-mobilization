# THE RIFLE CORPS: Maiden Paper (`rifle`)
*Caucus of the Ground Bloc. Whip: the Ground Staff. Speaking: the Sergeant-Major.*

## 1. Opening Speech

Mr. Speaker. I have a list. It has 251 names on it, and 232 of them are crossed out.

That is report 7. Two hundred and thirty-two riflemen went into the grinder and the Armour Corps sent a thank-you card. The Air Council sits there with 240 Falcons and calls *us* chaff. Chaff is what you throw to confuse a missile. We are what you stand *behind* when the missile lands.

Let me tell you what the infantryman is. He is the only unit that fits down a rail tunnel. He is the only unit that can stand in a city and keep it from falling. He is one army-cap slot of several bodies, 100 IC, and the fleet's own intelligence officer has noted that when we mass, it sends gunships and artillery to flatten us. That is a compliment. Nobody sends gunships to flatten a Wraith.

We do not ask for a buff. We ask for a *place to stand*. Give us the cellar, the city and the cover. Give us a reason for the Shapeshifters to exist on a battlefield full of tanks. Do that, and the Rifle Corps will hold the Line until the sky is clear.

The Armour Corps may go first. They are slow anyway.

## 2. The Gap

- **Infantry have no job the Line forces on them.** `inf` is 100 IC, 28 MP, 46 HP, range 260, dmg 11 (L3185). A tank is 120 IC with 110 HP and dmg 40 (L3186). On a one-dimensional front, plain stats say tank.
- **They die in the open.** Report 7: 232 of 251. The fleet already counters mass infantry on purpose: `counterBias('infantry')` sends gunships ×2.5, bombers ×1.5 and Pelter artillery ×2 (L5526). The enemy is right to do it. Our error is that we stand in the open for it.
- **The underground exists but infantry cannot use it.** Idle troops "dive into the tunnels" only to move (`railhub`, L3416; `onRail`, L7884). They cannot shelter there.
- **Cities are held by bodies, and nobody values them.** Capture runs on a head-count: enemy ground units in the footprint raise `ct.capture` by `8 + 3·eg` per second, and *any* friendly ground unit stops it (L6590–6591). The cheapest body should be the best city-holder. Nothing rewards that.
- **Exotic infantry have no distinct role.** Shock, Cyborg, Clone, Psi-Trooper and Android (L3252–3295) are statlines. The Shapeshifter (L3297) is strong against ground enemies, and enemy `gtank`/`gmech` are the landed ground force (`GROUND_BASE_WEIGHTS`, L5488). Armour is exactly where it falls over.

## 3. Bills

### RIFLE-1: THE CELLAR CLAUSE (infantry shelter underground)
**Pitch.** Every barrage is the same: the sky falls on whoever is standing in the open. We propose to not be standing in the open. A buried Barracks, a Rail Hub, and a man with a shovel.

**Player fantasy.** Build the Rail Hubs. Watch the orbital barrage hit an empty field, then watch two thousand riflemen come up out of the ground behind the enemy's landing.

**Clerk's Annex**
- **Mechanic.** Rail Hubs and buried Barracks gain `shelterW` (weight of infantry housed): Hub 40, buried Barracks 24, deep (`ugLvl` 2) ×1.5. Idle **infantry-class** ground units (inf, shock, clone, veteran, psitrooper, cyborg, android) within 250 px **auto-shelter** when a barrage/skyfall warning fires, and are untargetable and take ×0.15 damage while sheltered. They **auto-sortie** when any enemy ground unit comes within 600 px of the hub, with +50% damage for 5 s ("ambush"). Overflow stays on the surface, so mass still needs more holes. Cost: housed units draw 0.02 IC/s each.
- **Science.** Hardened deep shelters work: Cold War civil-defence tunnels and the Metro-2 type systems. Overpressure falls fast with depth.
- **Code.** `railhub` L3416, `barracks` L3390, `autoBury` / `UG_DEPTH` L3843, `onRail` branch in `updUnits` L7884, `shallowUgHit` L7543 (shallow shelters still crack, deep resist ×.18), `deriveWeight` L3793 for housing weight, `barrageTarget` L7530 for the warning.
- **Size.** M. **TM1.**
- **Tree.** No research. It uses existing no-research buildings; Nanotech's burrowing aura (Nanite Foundry) adds +50% capacity.
- **Tenet / Amendment I.** Pure buff, no nerf. *Enemy response:* the fleet's `scanEnemyIntel` sees units "missing" and shifts to `earthbreaker` (seismic press, already in the roster) against buried hubs. An earthbreaker hit on a shelter is a real threat, so shelter is a bet, not a cheat.
- **One-Button.** Pass. Build hubs; everything else is automatic. Ten hubs, ten times the shelter.
- **Line.** Pass. The Line is horizontal; the cellar is vertical.
- **Interactions.** Subterranean Works (the underground builder: this is the first tenant), THE TRUNK (docket 8; hubs double as Sally Ports), Armour (cannot fit; that is the point).

### RIFLE-2: THE GARRISON CLAUSE (holding cities, anti-landing)
**Pitch.** A city does not fall to a tank. It falls to the absence of men.

**Player fantasy.** The enemy drops on your capital; every street has riflemen at the windows and the landing pods fall into a firing line.

**Clerk's Annex**
- **Mechanic.** (a) **Urban cover.** Infantry-class units inside a city footprint get the deepest cover tier (fort 2, already 60% reduction) *without digging time*. (b) **Hold.** Each infantry weight inside a city adds +1 to the **capture-reversal rate** (16/s base) and raises the city's `cityDefend` fire by 4% per weight, to ×2. (c) **Reception.** Enemy ground drops that touchdown within 400 px of entrenched infantry are **Pinned** for 2 s (−50% move and rate) while the infantry's AT trooper (`atCeil`.56, `atDmgMul` 1.6) gets a free volley. No attention; it just happens when they land.
- **Science.** Urban defence in depth: Stalingrad, Mogadishu. The defender in buildings trades 3-to-1.
- **Code.** City capture L6590–6592, `cityDefend` L8387, `fortBonusAt` L4542, `hurt()` entr math L4992, `doSkyfall` L5213, `avoidCityCenter` L5212, squad AT table L3957.
- **Size.** M. **TM1.** **Tree.** No research. Rotary Cannon Works and Plasma Bastion Works stack on top.
- **Tenet.** Buff only. *Enemy response:* landings already skirt city centres (`avoidCityCenter`); with this bill they land on the outskirts and meet the garrison there, so the wall is at the edge, not the middle.
- **One-Button.** Pass. Fields infantry in a city, done. Scales with volume.
- **Line.** Pass. The city is a place on the line only infantry can fully use.
- **Interactions.** Fortifications Corps (cities are their ground), Planet (city defence tiers), Autofleet (landing pins).

### RIFLE-3: THE ANVIL (suppression: infantry's role in a mixed army)
**Pitch.** The tank is a hammer. A hammer needs something that does not move. That is us.

**Player fantasy.** A line of rifles turns the enemy's super-heavy into a parked target, and every artillery piece behind it suddenly hits it.

**Clerk's Annex**
- **Mechanic.** Every infantry-class hit adds **suppression** to an enemy ground unit (0.6% × weight-of-shooter per hit, decaying 8%/s). Full suppression is "Pinned": −35% move and −35% fire rate, and the target takes **+25% from artillery, rail and bombs** (explosive and "indirect" `weapon` classes: shell, rocket, rail). Infantry dmg itself is unchanged. High-HP enemies need *more* hits to pin, so the pin is slow but real; thousands of rifles pin anything. One type works slowly; a mix shreds. That is Order Paper #2 with a rifle in it.
- **Science.** Suppressive fire is the doctrine, and a suppressed unit is inert. Artillery "hammer and anvil" is as old as Cannae.
- **Code.** `hurt()` L4992 (suppression add and the damage-taken multiplier), enemy fire rate in `updEnemies`, `weapon` classes in `UNIT_DEFS`. Pinned state mirrors the existing `evadeT`.
- **Size.** M. **TM1.** **Tree.** No research. The War Academy's +15% dmg aura (L3470) raises it.
- **Tenet.** Buff only. *Power-creep guard:* it does not add raw DPS. It changes which weapons are *worth* building behind the line, so Artillery and Kinetic gain, nobody gets obsolete. *Enemy response:* the enemy's `gmech`/`pelter` already target infantry; bosses (Leviathan, armor 22) gain a pin threshold ×4 so they pin only under heavy volume.
- **One-Button.** Pass. No orders.
- **Line.** Pass. Needs one front; units overlapping the same front all contribute.
- **Interactions.** Artillery Corps (their +25% window), Armour (tanks stop being the only anvil), Energy/Physics (heat bills: a pinned, heated target takes both).

### RIFLE-4: THE MIMIC (Shapeshifters on the Line)
**Pitch.** They asked how a nanoswarm fights a tank. We reply: it climbs inside.

**Player fantasy.** Your swarm sees a Mammoth coming, flows over it, and the Mammoth stops being a tank.

**Clerk's Annex**
- **Mechanic.** The Shapeshifter reads its target's class and picks a **form**, automatically:
  - **Swarm** (current): vs infantry, walkers, light units. 75 dmg, plasma, regen.
  - **Disassembler**: vs `armor ≥ 12` ground targets (tanks, super-heavies, Leviathan). It latches and eats: **1.2% of target max HP per second** (0.3% on bosses), ignoring armour, but deals no ranged dmg while latched and the swarm is exposed (no regen). Latch slots scale with target hull `s` (about `s` shifters per target), so it needs *bodies*, not a cap.
  - **Reach**: vs air and space, the swarm is **useless**. That is the wall that forces a mix; no rule caps it.
  - Regen still switches off above heat 60 (docket ruling 6), so enemy lasers are its counter.
- **SCI-FI.** Canon fit: a thrown friend's makers' nanotech is a disassembler by design, the same trick as the Nanite Foundry's "smart-round" latch (v2.2.0). It disassembles, it does not teleport.
- **Code.** Shifter def L3297, `shapeshifter` research L3531, `allRegen` L7873, `infFast` L7885, `UNIT_FAM` L3378, smart-round latch precedent in Nanite Foundry.
- **Size.** M. **TM1.** **Tree.** `shapeshifter` (Psi/Bio/Nano triple combo, dang sc12). No new node. A refinement node `mimicry` (cost 240, pre `shapeshifter`) is optional.
- **Tenet.** Buff, no nerf. The only cost is the exposed state while latched, which is a real cost, not a cap.
- **One-Button.** Pass. Form is automatic. **Line.** Pass. Latching only needs contact, so it works on a single stacked front.
- **Interactions.** **Psionic Conclave** (decentralised command: why they pick forms with no orders), **Bioengineering** (the swarm's body), **Energy/Physics** (heat is the counter), **Armour Corps** (the tank finally has a predator).

## 4. Hot Take

### RIFLE-X: LEVÉE EN MASSE (the population takes up arms)
**Pitch.** The Sergeant-Major's proposal: when a city is being captured, the *citizens* pick up rifles. It has worked since 1793.

**Clerk's Annex**
- **Mechanic.** When `ct.capture > 0`, each Housing Block within the city raises a free **Militia squad** every 6 s, up to 4 per block. Militia: 20 HP, `inf` rifle, **not on the army cap** (they are civilians) and gone when the siege lifts. **Real cost:** the Housing Block's +3 MP income is suspended while it has living militia, and 1 stability per 40 militia is lost to the war footing. I mean this: the home front pays.
- **Code.** City capture L6590, `housing` building defs, `spawnUnit` calls.
- **Size.** M. **TM1.** **Tree.** None. Gated by society state: *dystopia* doubles the rate, *verdant* halves the stability cost.
- **Tenet.** Buff with a *real cost* (MP income and stability), which is the amendment's preferred answer. It does not touch the army-cap formula, because militia are civilian and temporary.
- **One-Button.** Pass. **Line.** Pass. **Interactions.** Stability (war footing), Society Bloc (dystopia/verdant), Planet (cities).

## 5. Alliances Sought

- **Ground Staff (bloc whip):** merge RIFLE-2 with the Fortifications Corps' city work, and RIFLE-1 with GROUND-2's Sally Ports.
- **Subterranean Works:** RIFLE-1 is their first tenant. They own the cellar; we own the tenants.
- **Artillery Corps:** RIFLE-3 is their anvil. Their guns get +25% on every pinned target.
- **Psionic Conclave and Bioengineering:** RIFLE-4, the Mimic. Psi supplies the doctrine, Bio the swarm.
- **Armour Corps:** reluctantly. They get the hammer slot; we get the anvil.
- **Against:** the Air Council, for describing riflemen as "ground clutter" in Round 2.
