# A Second Message from the President

*Delivered after the President read the First Session's docket, and before Question Time. It binds Rounds Five and Six. Where it differs from the first Address, this message wins.*

## The President's verdict on the docket
The President loved it: *"so fucking good of ideas… I fucking loved, like, 90% of them."* The President is **holding most bills** for now, to think about them, so nothing is being built yet. Here is what the President singled out.

**Loved:**
- **Warhead stockpiles.** *"You could build submarines that are really expensive, but you have to stockpile them with warheads."*
- **The heat mechanic,** *"because it forces you to branch out."*
- **The research reveal.** You only see the research you have the capability to reach, *"so what you first do kind of tells you what your strategy is going to be."*
- **Lead-time research,** as in Terra Invicta: *"you can't make an array at the last second."*
- **Saying yes or no to research outcomes,** *"rather than just having them automatically say yes."*
- **The thermosphere idea.**
- **The ammunition economy,** where you pay IC for rounds.
- **The society research options.**

**Wanted:**
- **More megaprojects.**
- **Plain descriptions of the units.** *"I don't know most of the units they're talking about, which is kind of a problem."* From now on, every bill that names a unit gives it one plain sentence the first time: what it is and whether it exists today or is proposed.

## Direction 1: heat becomes a full damage-type system
*"The thermal one should be even bigger, because that's kind of how you can deal with heavily armoured opponents. Bombardiers are really hard to kill because they have tons of things. Shields are really good against plasma and heat, but they're really bad against kinetic weapons. That would force you to branch out without forcing you to micromanage… this is how you can have a ship that flies through the air and doesn't get killed immediately by missiles."*

The President wants a **weapon-type versus defence-type matrix**. The enemy's mix of shields, armour and swarms forces you to build a mix. The player never chooses an ammunition type or a target; you only choose what to build. That is the One-Button Law.

**The Speaker's note on the physics.** The President described it two ways, and the councils must settle it:
1. **First version:** shields stop plasma and heat, and are weak to kinetic.
2. **Second version:** shielded ships resist missiles and kinetic, and are weak to lasers.

The Speaker offers a physics-grounded way to reconcile them, for the councils to adopt, amend or reject:
- **Magnetic shields** deflect charged matter. That means **plasma**, which is charged, is stopped cold. **Missiles** detonate early against the field.
- **Light passes through a magnetic field,** so **lasers** go straight through shields.
- **A neutral kinetic slug** carries too much mass and momentum for the field to turn, so **kinetic** rounds punch through as well.
- **Heavy armour** shrugs off kinetic rounds and reflects or ablates lasers. **Plasma** melts it, and **heat** softens it so that kinetic finishes it. This is the Thermal Field.
- **Soft targets** (swarms, drones, infantry, light craft) die to **lasers**, which are instant and perfectly accurate against fast movers, and to explosives.

Under this reading:
- A shielded sky ship survives missile spam, as the President wants, but dies to railguns and lasers.
- A Bombarder carries both shields and armour, so no single weapon family kills it alone.

**Ground rules for the system:**
- Every weapon still damages every target. A wrong-type army is slow, never useless.
- No new per-shot choices for the player.

## Direction 2: ammunition is an upgrade, never a requirement
*"You can have every IC unit work without IC, but the more IC you put in, the faster they do, and they could be better."*

Ammunition & the One Locker must therefore follow two rules:
- **Every unit fights at its full baseline with no ammunition spending at all.**
- IC spent on rounds is a pure YES that makes units faster-firing or harder-hitting. It is also the honest late-game sink.

## Direction 3: the big button is fine, and allies are that button
*"A $20,000 button isn't really that bad… allies is basically that button. It's just your 20,000 IC and late-game money. You can buy some allies to fill up your ranks, which is fine."*

The President withdraws the old objection to a large spend button. Allies already exist in the code: grep `allyContracts`, `tickAllyContracts`, `allyHireCost`, `heavyport` and `milbase`. A new seat, the **Foreign Ministry**, now speaks for them.

## Direction 4: ideology shapes research
*"The research options are so cool… they could add authoritarianism, or market, or whatever… late-game command lets you research whatever you want… which should be super good rewards, but market gives you a bunch of researches, which allows you to create tons of units really, really fast."*

- **Command:** choose any research deliberately, with big rewards, on a planned path.
- **Market:** research comes broadly and cheaply, so you mass-produce fast.
- **Authoritarian** and other readings are also on the table.

This touches the society layer. The standing decision is that the society layer is a TM2 architecture with three TM1 slices. A bill may propose ideology-shaped research as a **fourth TM1 slice**, but it must say so plainly.

## New seats
- **The Foreign Ministry**, for allies: contracts, deliveries, and late-game ranks bought with surplus IC.
- **Civil Defence**, for the cities: districts, evacuation, city self-defence and the home front. No council has owned the cities until now.

## The Crossbench
The President also wants ideas that do not grow from the President's own seeds: *"just let them go off the code, and don't give any of my feelers."* So a group of **Independents** has been seated.
- They read only the game's code and the three rules: the tenet as amended, the One-Button Law and the Line.
- They have never seen the First Session, the ROADMAP or the President's ideas.
- Their bills appear on the Round Five order paper as the **Crossbench**, and every council may vote for them.
