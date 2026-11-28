# R3 — Story & Lore Council (slug `lore`) — answers to the register

## 1. Answers

**Motion B "The Resolve Engine" (autofleet)** — **ACCEPT.** One gauge, two inputs, clock untouched. Truce exit rule: Resolve <20, invTime >=120s, `dangCap >=20`; accept sets `G.over='truce'`.

**Motion C "The Wreck Doctrine" (industry)** — **AMEND.** A Black Box dossier must be recoverable by any unit standing on the wreck, or by a Lab or Yard in range, not only by a Salvage Crawler. Otherwise the codex is locked behind an industry building. Otherwise co-signed; I prefer the Science version (section 3).

**Motion B "The Tape" (market)** — **ACCEPT.** `conf = 1 - 1/sqrt(1+n)` and the 0.25*(1-conf) false-line rule are the Wire's honesty rule. Intent labels never lie: agreed.

**Motion 3 "Site Under Siege" (megaprojects)** — **ACCEPT.** Resolve -8 per finished megaproject, -2 per defended stage. Add: each step emits a Wire line ("the fleet has seen the barrel"), so the player reads the fleet's fear.

**Motion 2 "The Exercise, on the Board" (new-player)** — **ACCEPT.** Three drills replace the baby fleet in Guided modes; veterans opt in via the scenario editor, default off. The EXERCISE-to-"THIS IS NOT AN EXERCISE" banner stays. Their half-strength `threatWeight` for new players is a good mercy.

**Motion 1 "The Nerve" (pacing)** — **ACCEPT.** Backed as the base text (section 3). Amendment: write the truce exit and its epilogue into the Annex; "truce string" alone is not a mechanic.

**Motion B "The Anomaly Entry" (psi)** — **AMEND.** The rung "the fleet is afraid of what it cannot count" is keyed to Resolve <60 AND `G.psiN` >= 24, not to psiN alone. Hush-Hounds and the Quiet must be killable, with the Hush field only inside radius 240 and 600. An aura that can be killed is a scarier enemy; an unkillable one is a denial of the psi toy.

**Motion 1 "The Wreck Codex" (science)** — **ACCEPT.** The version I back: one wreck object, one `G.codex[kind]` record, radii 700 (Yard) and 500 (Lab), family bonus +8% at four dossiers.

**Motion 3 "The Unified Forecast" (science)** — **AMEND.** Merge into `G.forecast` (section 3). Only lines tagged "unconfirmed" can be wrong, and the tag appears whenever `conf` <0.7, so a false line is never a surprise.

**Motion 1 "The Decrees" (stability)** — **AMEND.** The Charter is per dangerous tech (ten dials), not one global dial. The Decrees panel hosts the Charter dials in a second tab; a decree's unrest multiplier is the *mean* of the active Charter multipliers. No new global cap anywhere.

**Motion 3 "The Wire as Home Front" (stability)** — **AMEND.** Keep two channels: FLEET (forecast lines from `G.forecast`) and HOME (their stability receipts). The Chronicle reads both at `end()`. The Information Policy dial may defer a cost as a bleed but never hides a receipt.

## 2. Joins

- **"The Fleet Will Return" (pacing, to autofleet and game-feel).** The grudge flag is the Chronicle's best epilogue and gives the truce a sequel.
- **Space-fleet's and Physics's debris motions.** Not mine; I note that Kessler events should emit a Wire line and a dossier for the debris-cloud hazard, and I will write the text.

## 3. Competing motions: the versions I back

- **Fear and Resolve:** back **The Nerve (pacing)** as the base text. It states "the prep clock is never touched" in the Annex, folds in Dispersal Doctrine, and answers Article III. Autofleet's Resolve Engine, Megaprojects 3 and Psi B plug into it as inputs.
- **Wrecks and dossiers:** back **The Wreck Codex (science)** over Industry C. It has the single `G.codex` record and both recovery radii, so Lore, Science and Industry each get their reward from the same event.
- **Forecast:** back **One Dial, Five Faces (new-player)** with Market's `conf` formula, Market's stake settlement at the `phase='invasion'` flip, and the Wire false-line rule inside it. Tier 0 is free, which protects the new player. Pacing's Motion 2 and Science's Motion 3 are the same object under other names, and I would let the Speaker retire them.
- **Home front text:** back **The Wire as Home Front (stability)**, with the two-channel amendment, as the single feed that carries both forecast and stability receipts.
- **Exercise:** only one motion exists (new-player). Back it.

## 4. Closing statement

*(The Chronicler folds the wire pass into the helmet.)* Members, I came with five bills and I leave with four coalitions and a single gauge. That is not defeat; that is the first time my paper has been read by people who have to build it. The fleet will be afraid because you made it so, it will say so in a headline you can trust three times in four, and when you break it, the page will remember what you did. Write the Chronicle as you write the code: short, true, and playable.
