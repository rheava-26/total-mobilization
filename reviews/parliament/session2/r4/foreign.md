# Foreign Ministry: Maiden Paper (Round Four, Second Session)

*Seat: the Foreign Ministry. Bloc: Economy. Portfolio: allies, and only allies.*

## Opening Speech

Honourable members, I come from a ministry with no tanks, no radar and no shame. I come bearing a chequebook.

The Ground Staff talks about "the domestic option", meaning your own army. I treat it as the plan of last resort: it is slow to raise, it costs manpower, and it has opinions about its pay. The rest of the planet has armies already built. They are sitting in barracks, wearing uniforms, and costing the President nothing until the moment we sign.

The President has said it plainly: *"Allies is basically that button. It's just your 20,000 IC and late-game money. You can buy some allies to fill up your ranks, which is fine."* The Ministry hears "fine" and files it as "a mandate".

Here is my complaint. Today you may press the button two or three times in a whole campaign, because the game charges you in a currency you cannot grow: influence. I call that a velvet rope around a nightclub that wants customers.

I propose to open the doors. Press the button as often as your treasury allows. Each contract is a standing relationship with a named power, and each one costs a retainer, because the enemy of mercenaries is a late invoice. The fleet will notice you are buying a planet's worth of armies, and I have an answer for that too. Allies are not a cheat. They are *procurement*.

My friends in the Ground Staff may sneer. They will be fine. They will be standing next to four hundred foreign tanks.

---

## The Gap

**The ally layer exists, and it caps itself at about three purchases.** I read the code before I wrote this paper.

- **Nine packages** live in `ALLIES` (`csg`, `armor`, `airwing`, `interc`, `arty`, `helo`, `bombers`, `column`, `fleet`). Each costs 180–550 **influence**, 27,000–105,000 IC and 40–150 manpower.
- `hireAllies` does `G.allyInfPen += a.inf`. `INF_CAP()` is `max(100, 600 - allyInfPen)`. The influence price is therefore **deducted from your influence cap forever**, and `allyAvail` demands `G.inf >= a.inf`.
- Work the arithmetic. A hire is only legal while `allyInfPen + a.inf <= 600`.
  - Buy the Carrier Strike Group (450) and the cap falls to 150. The cheapest package asks 180, so **every other package is locked out for the rest of the game**.
  - The cheapest package, the Interceptor Screen, can be bought three times and no more (0, 180, 360; the fourth would need 720).
  - Lifetime ceiling is therefore about 80,000–110,000 IC of allies, and a research economy that has been permanently starved.
- `allyCd` (60–170s) is a second, hidden rate limit on top of that.
- **The scale lever is cheap and needs a hire first.** `deliverAllyClumpExtras` stacks per copy: an Allied Armory (`alliedarmory`, 380 IC) adds 2 heavy tanks every clump. But `deliverAllyClump` returns early when no battalion stands, so the lever does nothing until you have spent the influence.
- **There is no retainer.** Delivered units cost nothing to keep arriving, so 20,000 IC is a one-off, not a sink. `combatWeight()` skips `u.ally`, so the army cap is untouched, which is correct, and which I do not propose to change.
- **No nation has a face.** Every package says "your capital" and the roster is a list of keys. Allies do nothing with stability. The enemy never reacts to them. `G.stats.allyUnits` feeds only the after-action report.

The Ministry's case: the President asked for a big button, and the code built a button you can press twice. Under the One-Button Law, ten contracts should feel like ten times one contract. They currently feel like an error toast.

---

## FOREIGN-1: The Retainer (contracts you can actually buy more of)

**Pitch.** "Gentlemen, you do not buy an army. You *subscribe* to one. Pay the signing fee once, pay the retainer every clump, and when the money stops, the regiments go home. We are not animals; we do not shoot the unpaid. We just lose their phone number."

**Player fantasy.** You press ⚑ ALLIES a dozen times in the late game. Your treasury visibly bleeds into a stream of foreign armour, and you can feel the bill.

### Clerk's Annex
- **Mechanic.** The signing fee stays: the existing IC and MP. The influence price is no longer a permanent cap shave.
  - Influence becomes a **one-time signing cost** that is refunded to the cap (restored to `INF_CAP`) while the contract is *lapsed*, and held while it stands. No hard ceiling remains.
  - New **retainer**: each standing contract pays IC every clump, roughly `hire IC / 60` per clump (about 670 IC for the Armored Battalion, about 15 IC/s). This is a real cost, the preferred answer to "too strong".
  - **Lapse is soft.** If IC cannot cover the retainer, that contract skips the delivery and resumes when funded. Units already landed stay.
  - `allyCd` falls to a 10s anti-double-click. It was a rate cap with no purpose once the retainer exists.
  - All numbers are TUNABLE. The Clerk should calibrate retainers so that ten contracts eat a *large but survivable* share of a late economy, from the match reports.
- **Code grounding.** `hireAllies`, `INF_CAP`, `allyInfPen`, `allyAvail`, `allyCd`, `deliverAllyClump`, `tickAllyBattalions`. The ledger lives in `G.allyBattalions`.
- **Build size.** M. **TM1.**
- **Research attachment.** None. Contracts are available from the start, as today.
- **Tenet check (Amendment I).** This is a buff (the influence ceiling is removed), so it needs a real cost and an enemy answer. Retainer is the cost; FOREIGN-4 is the answer. The influence cap shave is, by the arithmetic above, a hidden hard cap of three purchases. It is a rule-made ceiling, which the tenet forbids. The retainer replaces it with a cost that scales *with the player's own economy*.
- **One-Button check.** Pass. The player presses the button and then does nothing. Retainers auto-pay. Cost and delivery scale linearly with contract count.
- **Line check.** Pass. Delivery is a point on the line (a city).
- **Interactions.** Market (retainer is a late IC sink), Industry (competes with production for IC), Stability (FOREIGN-4).

---

## FOREIGN-2: The Chancelleries (five nations, five doctrines)

**Pitch.** "Every country sells something different, and the polite ones do not say *what it is*. I have signed five. One brings iron, one brings ships, one brings sky, one brings a crowd, and one brings something that goes up and does not come down."

**Player fantasy.** You are not buying "an army". You are choosing who to be friends with, and each friend fills a hole your own production left open.

### Clerk's Annex
- **Mechanic.** Group the existing `ALLIES` packages under five named nations, each with a **delivery style** and a **quality profile**. Average quality stays near today's 70%.

| Nation | Packages | Delivery | Quality (HP/damage) | Status |
|---|---|---|---|---|
| Northern Pact | `armor`, `column`, `arty` | rail, arrives at the city nearest the heaviest enemy ground contact | 85% / 65% | Existing packages, regrouped |
| Maritime League | `csg`, `fleet` | at sea, coastal cities only | 85% / 60% | Existing packages, regrouped |
| Skyward Union | `airwing`, `interc`, `bombers`, `helo` | airborne at patrol altitude | 60% / 85% | Existing packages, regrouped |
| Free Companies | proposed `levy` | 12 infantry per contingent, by road | 50% / 50% | **Proposed** |
| Orbital Consortium | proposed `launch` | satellites placed straight into orbit | 70% / 70% | **Proposed** |

  - **Free Companies.** A cheap, huge contingent: it fills ranks quickly and is meant to die in front of better troops. This is the "bought by the regiment" fantasy at its purest.
  - **Orbital Consortium.** A commercial launch provider that places Missile Sat "Volley" units (existing: a satellite that fires missiles from orbit) outside orbital lift capacity. It is real: launch services are already for sale.
  - **Doctrine as a hole-filler.** The Skyward Union's lasers (Warden interceptor, Solaris laser gunship) pass through magnetic shields, per the Speaker's note. The Northern Pact's guns (Lance railgun, Goliath) punch through shields as kinetic. Cash can therefore fill a missing damage type. It cannot erase the wall: they arrive in clumps and at reduced quality.
  - **Heavy Port, Military Base and the other ally buildings** keep their roles (FOREIGN-3).
- **Unit glossary (first mention).** Mammoth: a super-heavy tank, exists today. Goliath: a heavy tank, exists today. Aegis: a battle tank, exists today. Lance: a railgun, exists today. Falcon: a fighter, exists today. Solaris: a laser gunship, exists today. Warden: a laser interceptor jet, exists today. Lancer: a heavy bomber, exists today. Kite: an attack helicopter, exists today. Tempest MLRS and Thunder SPG: rocket and self-propelled artillery, exist today. Hailstorm AA: a flak track, exists today.
- **Code grounding.** `ALLIES` (names, `units`), `applyAllyQuality` (becomes a per-nation table), `deliverOwnRosterUnit`, `spawnAllyUnit`, `allyLandingX`, `hasNearWater`. The panel is `buildAlliesPanel`.
- **Build size.** M for regrouping and the quality table; L with the two new nations.
- **TM1.**
- **Research attachment.** Free Companies from the start. Orbital Consortium behind Space Age (`spaceage`), since it is a space product.
- **Tenet check.** Two nations buff (more roster options) and the average profile is unchanged, so no existing army loses worth. Enemy answer: FOREIGN-4.
- **One-Button check.** Pass. The nations are menu entries; delivery targeting is automatic (nearest heavy contact).
- **Line check.** Pass. The Northern Pact delivering to the city nearest the heaviest contact is exactly a one-dimensional "go where the fight is".
- **Interactions.** Physics (damage-type matrix), Ground (Free Companies as the infantry floor), Space Fleet, Autofleet.

---

## FOREIGN-3: Embassy Row (scale by building)

**Pitch.** "A battalion is a rumour. An embassy is a standing order. Put an embassy in your capital and the whole nation behaves as if you were already allies."

**Player fantasy.** You carpet a city district with embassies and consulates. It sounds like the quietest build in the game, and your clump timer becomes a conveyor belt.

### Clerk's Annex
- **Mechanic.**
  - Fold the three ally buildings (`alliedarmory`, `alliedaadepot`, `alliedairfoundry`) and `heavyport` into **one Embassy per nation**. Each copy adds an extra unit per clump of that nation's signature type, as today (+2 tanks, +1 AA, +1 bomber, +1 warship).
  - **They no longer need a battalion first.** An Embassy with no standing contract still buys the nation's extras at the retainer rate.
  - **Military Base becomes stacking.** Today it is presence-only (×0.75, floor 30s). Each copy adds a **parallel clump lane**, so ten bases give ten staggered lanes of the same contingent size. Interval stays 45s. There is no floor, because the lanes do not shorten the clock; the only limit is simulation load (see below).
- **Code grounding.** `allySupport`, `deliverAllyClumpExtras`, `allyClumpInterval`, `tickAllyBattalions`, gmods `allyArmory`, `allyAADepot`, `allyAirFoundry`, `allyNavalFast`, `allyLandFast`.
- **Build size.** M. **TM1.**
- **Research attachment.** None; the buildings are already unlocked by the existing SPEC tier.
- **Tenet check.** Buff, so cost: every extra unit pays the retainer. The existing 30s floor is a cap; the replacement is lanes, which scale rather than clamp.
- **One-Button check.** Pass. Build embassies; units arrive. Linear in copies.
- **Line check.** Pass. Extras land at the city with the fewest allies nearby, already automatic.
- **Perf note.** Ten contracts plus embassies is about 1 unit/second, a few thousand over a long game. CLAUDE.md records 9,000 units fighting in an earlier build, so I treat this as a perf fix to do (pooling, cheap AI for allies), never a reason to cap the fantasy.
- **Interactions.** Industry (these are SPEC buildings), Fortifications (embassies need a city to stand in), Civil Defence (embassies are targets, see FOREIGN-4).

---

## FOREIGN-4: The Counter-Coalition (the enemy answer, and what allies do to stability)

**Pitch.** "Gentlemen, a fleet that crosses a solar system does not ignore a planet buying armies. It sends a diplomat of its own, and its diplomat carries a railgun."

**Player fantasy.** Your buying is noticed. The sky answers it with something you can kill, and the war feels like a *war of procurement* with the enemy as a rival buyer.

### Clerk's Annex
- **Mechanic.**
  1. **The Fear Index sees the chequebook.** Standing contract value feeds the Alarm panel's *Army* line (proposed in AUTOFLEET-6) at **half weight**. Allies are bought power; the fleet counts them but does not fear them as much as your own industry. The arithmetic AUTOFLEET promises (doubling the economy sends about 9% more ships) holds.
  2. **Interdictors.** A proposed enemy unit, the *Interdictor* (a high-altitude jamming platform; **proposed**), arrives with the fleet: one per `ceil(log2(1 + standing contracts))`. While one lives within 600px of a landing city, **that city's allied landings are held, not lost**. The contingent is delayed until the Interdictor dies. It is a normal target that the player's anti-air kills with no input.
  3. **Risk premium.** Retainers scale `× (1 + (1 − effStab/100))` (up to 2× when stability is gone). It is the real-world sovereign risk premium: nobody lends cheaply to a country that is losing.
  4. **Stability and influence.** Signing a contract costs influence once and nothing thereafter. A lapsed contract refunds the cap. Allies **never touch the army cap or manpower**, per the standing rule.
- **Code grounding.** `effStab()`, `deliverAllyClump`, `spawnEnemy`, `invT`, the intents table (`curIntent`, `fleetMul`). Interdictor would be a new `spawnEnemy` kind.
- **Build size.** L (one enemy unit, one Fear line, one retainer multiplier).
- **TM1.**
- **Research attachment.** None. The enemy answers; the player does not unlock it.
- **Tenet check (Amendment I).** This is the enemy-answer clause for FOREIGN-1 to -3. It does not make the player's existing army worthless: the Interdictor delays only allied arrivals, never touches the player's own production. The risk premium is a cost, not a cap.
- **One-Button check.** Pass. The player builds AA and air superiority they would have built anyway; no steering.
- **Line check.** Pass. Interdictors threaten a *city* (a point), not a flank.
- **Interactions.** Autofleet (Fear and unit), Stability (risk premium), Air and Space (Interdictor hunters), Civil Defence (embassy districts).

---

## Hot Take: FOREIGN-X, Pax Armata (the standing budget)

**Pitch.** "My colleagues want the player to click a button. I want the player to click it *once, in a different place*. Set a percentage. The Ministry does the rest. You will never think about a treaty again."

**Player fantasy.** A megaproject, the **Treaty Organisation**, ratified late. You set one slider, *Allied Budget: X% of income*. From then on your surplus IC becomes foreign regiments, automatically, until the war ends.

### Clerk's Annex
- **Mechanic.**
  - **Treaty Organisation** is a megaproject building (proposed), about 60,000 IC. It changes what your civilization *is*: a coalition, not a nation.
  - One standing choice made once: the budget percentage (0–100% of IC income).
  - The Ministry spends it, hands-off, by **filling the biggest gap**: it reads the enemy's current mix (shielded, armoured, soft, air, sea) and buys the nation whose doctrine covers the weakest damage type, using the Speaker's matrix. Priority is automatic; the player never picks.
  - Contracts bought this way pay full retainer and count at full weight in the Fear Index.
- **Code grounding.** `hireAllies` (the call), `allyAvail`, `G.ic` income reads, `ALLIES`. The gap reader would sample `G.enemies` types.
- **Build size.** XL. **TM2** (a society-shaping megaproject plus an automatic buyer), with a TM1 slice for the budget slider.
- **Research attachment.** Behind Space Age; ideology hook, see below.
- **Ideology (a fourth TM1 slice, plainly).** Direction 4 proposes society-shaped research. If Command and Market exist, Market could cut retainer cost and Command could lift ally quality. This depends on the society layer and is out of scope until Command and Market land.
- **Tenet check.** Maximum power fantasy, bounded by retainer, Fear and the Interdictors, never by a cap.
- **One-Button check.** The clearest pass in my portfolio: one slider, set once.
- **Line check.** Pass. Deliveries are point events.
- **Interactions.** Megaprojects (a new one), Command and Market (ideology), Autofleet.

---

## Alliances Sought

- **Market and Command** (my own bloc). The retainer is their late-game IC sink; I will trade my signing fees for their ideology hooks.
- **Industry.** Embassies are buildings. I will not compete for the same IC.
- **Autofleet.** FOREIGN-4 needs their Fear Index and an Interdictor. Their Alarm panel is my enemy answer.
- **Stability.** The risk premium needs their `effStab` rules.
- **Physics and the Arsenal Bloc.** The doctrine table leans on their damage-type matrix.
- **Civil Defence.** Embassies and cities are the same real estate.

To the Ground Staff: your army is the domestic option. I merely propose that it have company.
