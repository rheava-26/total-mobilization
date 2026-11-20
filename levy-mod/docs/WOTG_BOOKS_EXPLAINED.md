# The books of Wrath of the Gods, explained

Source: NoxusBoss 1.2.31 (the version in our mod cache), read straight out of its English localization files and its source. Spoilers for WotG and Calamity are fine here. Nothing from other mods is discussed.

A quick note on how to read this. For each book I separate three things:

- **Says**: what the text literally says.
- **Stated in the mod**: things the mod's own text or code actually confirms.
- **My reading**: interpretation. Where the author clearly meant to be vague, I say so.

## First, what the books are

There are 40 Solyn books defined in the data file (39 are live; "Sanguine Wildlife" is commented out and never appears). You find them in the world, then hand them to Solyn for her bookshelf. Each book gives a reward (potions, decor, and so on), and each has a 0 to 4 star rarity. Her bookshelf dialogue says why: she lost her whole library when she was sent here, and "some of them were a bit obscure in terms of word choice and subject matter, and a few were a bit... strange."

The books fall into four kinds, and knowing which kind you are holding is most of the battle:

1. **Pure jokes and in-jokes** (Bible of Slime, How To Fly, Instruction Manual, Book of Shaders...). They are not lore. They are the mod's authors winking at Terraria and Calamity players.
2. **Calamity cross-references** (Exo Blueprints, Sulfuric Leaflet, Seamap Collection, Rage of the Deities...). They exist to tie Calamity's world to Solyn's.
3. **Deliberately unreadable books** (Otherworldly Tomes, Aberrant Writings, Invisible Ink, Book of Miracles...). The point is that you cannot read them, and Solyn can. That is characterization of her homeworld, not a puzzle with an answer.
4. **The two real lore texts**: the **Ominous Storybook** and **Rage of the Deities**. Plus, outside the bookshelf, the Avatar and Nameless Deity lore items, which behave like books.

The two big cross-cutting facts you need for the whole document:

- **Noxus is the Avatar of Emptiness's old name.** The mod is still called NoxusBoss, the Avatar's mask item is internally "NoxusMask", and the player-rant cutscene says "Noxus was just visiting." The Avatar is the dark, frozen, rift-dwelling god. Noxus and the Avatar are the same being.
- **Xeroc is the Nameless Deity's original Calamity identity.** The mod's code reuses "XerocEye" for the Nameless Deity's eye constellation. A joke line in the mod accuses its author of having "stole Calamity's Xeroc character", and the word "Xeroc-blessed" is in the lore vocabulary. The Absence Notice book (below) deliberately does not load if Calamity itself ever adds a Xeroc NPC. So Xeroc is the shared origin; WotG made it a real boss.

---

## Book by book

### Rage of the Deities (the closest thing to a real lore book)

- **How you get it:** Calamity's Exhume mechanic turns a Spell Tome into it. Rarity 1.
- **Says:** "The writings start with the following excerpt: *Two beings, warring for dominion over All, / Grew blind to the passage of Time.*"
- **Stated:** The title riffs on the mod's own name (Wrath of the Gods). The code ties it to a Calamity mechanic, so it is meant as a Calamity-side book. Nothing in the files names who the two beings are.
- **My reading:** "Dominion over All" fits the gods-fighting-gods tone of Calamity's lore, and "blind to the passage of Time" fits the theme the Avatar's lore hammers on ("time denied", "succumbed to time"). So the safest reading is: two powers fought each other so hard that time ran out on both. Whether the author meant Nameless and Avatar specifically is unclear and, I think, intentionally open. Be wary of anyone who says it is definitely one particular pair.
- **Why it feels confusing:** it only gives you a couplet. It is a teaser for a book you never get to read.

### Ominous Storybook (rarity 4, the rarest; 1 in 23,000 from any non-statue enemy)

This is the one most people find "doesn't make sense", because it is a poem, not a story.

- **Says (short anchors):** "A pulse, then silence / Stars exhale. Seeds split stone"; "Worlds rise and fall, emerging from and fading to the grand foam"; "Yet time is not a line, / But the bloom of a flame"; "Petals curl inward / Ashes spiral outward / No beginning. No end"; "Do you hear it? The quiet hum beneath the ruin"; and the final stanza, which shifts from pale to deep red: "The memory of motion- / The turning of the wheel's axle / The leaping of the spark / A world's waking / From the shadow of a flower's fall."
- **Stated:** The line "Do you hear it? The quiet hum beneath the ruin?" is also one of the mod's loading-screen tips ("Eternity tip"), so this book and that tip are the same piece of writing. The text color slowly goes from dusty pink to a warning red as it goes on. That colour shift is real and intentional.
- **My reading:**
  - The whole poem is about cycles. Universes are born ("seeds split stone"), burn, collapse into "the grand foam", and are reborn. "Time is not a line, but the bloom of a flame" is the thesis: time is a flower opening and closing, not a straight road.
  - That is exactly the WotG world. The Avatar is a god from a universe that died ("succumbed to time long ago"), the Metallic Chunk is "the last remnant of a deceased universe", and the Nameless Deity's whole garden is a place of flowers and rebirth. The poem is the setting's philosophy in miniature.
  - The red-shifting last stanza reads like an awakening: motion, then a spark, then a world waking up from the shadow of a flower falling. A "flower" falling is a very WotG image (lotus, lilies, the Garden).
  - The "quiet hum" being heard "beneath the ruin" suggests that even after an ending, something is still running.
- **Ambiguous on purpose?** Yes, entirely. The mod never explains it. Treat it as the mod's mission statement, not a plot point.

### Fishing Catalogue (rarity 2, you asked about this one)

- **How you get it:** random Angler quest reward (about 1 in 9) and a guaranteed one every 14th completed quest.
- **Says:** "A boring catalogue of catches. The amount of precision and detail is remarkable."
- **Stated:** That is the whole text. No lore hides in it.
- **My reading:** It is a gag about how Terraria's Angler gives you an endless parade of fish chores. "Boring" plus "remarkable precision" is the joke: Solyn's world has someone obsessive enough to log every fish. There is nothing deeper to find.

### Bible of Slime (rarity 1)

- **How you get it:** very rarely carried by a slime. Calamity slimes get the same chance.
- **Says:** "In the beginning, from the depths of the primordial ooze, Goozma, the pure and unyielding essence, emerged... Blessed are the fluid, for they shall move with the tides of time... All things, in the shape of slime, are one with the flow of creation. Amen."
- **Stated:** The code literally names a function "AddGoozmaBibleToSlimes_Calamity". Solyn herself mocks it in her farewell note: "The Bible of Slime? How silly!"
- **My reading:** It is a parody scripture. Goozma looks like a nod to Calamity's slime-god joke lore (the code names it a "Goozma bible" for Calamity slimes), so the book is slime religion written as a real religious text. I am confident about the joke, less about the exact Calamity origin. Pure joke, but note the parallel: a creation myth that starts from "primordial ooze" is the exact structure of the serious creation lore elsewhere.

### Book of Miracles (rarity 3)

- **How you get it:** dropped by the last Exo Mech standing, after all of them have been beaten.
- **Says:** "The contents of the pages are obfuscated by a lurid, lime-colored disintegrative effect."
- **Stated:** The tooltip background is drawn with a shader named "MiracleblightImitationShader". That is a direct imitation of Calamity's Miracleblight effect. Also, there is a loading tip: "Consult the book of miracles." (Miracleblight tip).
- **My reading:** Miracleblight is a Calamity debuff tied to the Exo Mechs, so the book reads as "Exo Mech lore you can't read", its pages eaten by that effect. (My reading; the mod only confirms the shader imitation.) It is a cross-reference, not a mystery to be solved.

### Futuristic Treatise (rarity 3) and Exo Blueprints (rarity 2) and Hi Tech Instruction Manual (rarity 1)

All three are about Draedon's Exo Mechs.

- **Futuristic Treatise:** The tooltip text is empty. The game draws a picture of code on a dark box instead (the code image is drawn in place of text). It drops from Ares, Thanatos or Apollo about 1 in 8. **My reading:** the book is made of Draedon's source code, so you can "see" it but not "read" it as prose.
- **Exo Blueprints:** "Blueprints for Ares, Thanatos, Artemis, and Apollo... seemingly written by Draedon." The quoted note says the machines are strongest "with precise aerial capabilities foregone entirely. This is absurd, yet fascinating." **Stated:** that is Draedon's flat, analytical voice. It is a joke about the Exo Mechs' odd design (the twins never fly in the final fight).
- **Hi Tech Instruction Manual:** "Contains strange notes on the construction of four hi-tech, autonomous robots." That is the Exo Mechs again (four of them, counting the twins). Solyn collects it on her own, with no player action.
- **Why they matter:** Solyn fights alongside you against Mars (a Draedon construct) to finish the Genesis, so Draedon is her story's gatekeeper. These books are Draedon-flavored set dressing for that.

### Wulfrum Assembly Guide, Sulfuric Leaflet, Acidic Prayerbook, Seamap Collection, Twenty Twome, Cactome (small Calamity cameos)

- **Wulfrum Assembly Guide:** "An esteemed inventor's detailed yet confusing instruction manual. The instructions mention various tools. Most are unfamiliar, and the rest are used in highly eccentric ways." Dropped by charged Wulfrum machines. **My reading:** a joke about how wulfrum gear looks like scrap-built junk.
- **Sulfuric Leaflet:** "A short, informational leaflet describing the dangers of acid rain. It appears to be irradiated." Calamity's Sulphurous Sea acid rain event.
- **Acidic Prayerbook:** "reverent... porcine creatures, dragons, and a supposed 'sulfuric terror of the sea'." That is Old Duke (the pig-dragon-fish) from Calamity. A loading tip even calls him "your friendly local neighbourhood eldritch god".
- **Seamap Collection:** "A catalogued collection of various seamaps, with markers seemingly indicating treasure. None of the depicted locations are familiar to you." Dropped by a pirate captain. Nothing deeper: the maps show places in Solyn's world.
- **Twenty Twome:** "The strange ramblings of an obscure author, inventing a deluge of connections to hornets and the number twenty-two... a 'hornet round' in a grim tone." From hornets. Pure joke about numerology obsession; I could not confirm a specific Calamity or Terraria meme behind "hornet round", so treat that as unexplained.
- **Cactome:** "It contains just a single dense page... *'Are your hands alright?'*" Dropped by rolling cacti. **My reading:** a pun on cactus spines and "tome". The question is the whole joke.

### Instruction Manual (rarity 2, from Moon Lord) and How To Fly (rarity 2)

- **Instruction Manual:** "An instruction manual about a game named 'Terraria'... *Unsure about what to do? Contact the Guide*... For some reason, you find this mildly insulting." **Meta joke.** It is dropped by the Moon Lord, "a powerful, lunar being." The book is a deliberate fourth-wall poke: you are doing a post-Moon Lord quest and the reward is a beginner's tutorial.
- **How To Fly:** "The first page says: *'Hold the jump button.'* The remaining ninety-six pages are blank." You get it by flying at high speed. **Meta joke** about how simple Terraria flight is.

### Dubious Spellbook, Dubious Brochures, Magic Lexicon, Inscrutable Texts, Ancient Writings, Best Price Bestiary, Clover Manual, Apple Thesis, Unfinished Coloring Book, The Book of Shaders

These are mostly comedy, but a few say something about Solyn:

- **Dubious Spellbook:** a book of recipes for spellbooks ("Scarlet Malison", "Aethercrash", "Tenebrous Silverglow") using real Calamity ingredients. A joke on how Calamity has so many magic weapons. Dropped by Tim and Rune Wizards.
- **Dubious Brochure (Corruption and Crimson):** "a lighthearted spiel inviting travelers to visit the Corruption biome... bafflingly glamorizing." A joke that someone is advertising the worst biomes. Solyn found them both "cute" because she is an outsider.
- **Magic Lexicon:** four real words (ensorcell, goetia, gramarye, mystagogue). It is flavor for a mage character.
- **Inscrutable Texts:** the quoted line is intentionally over-the-top ("The ascendancy of an aspirant to the exalted echelons of Magicks is a feat achievable only by the most percipient, sagacious of minds."). It belongs to an archmage. **Stated:** Solyn's dialogue calls it "Extremely dense and formal. Not willing in the slightest to hold the reader's hand," and she says it taught her spells she had never heard of. She connects it to Permafrost: "First the seedling, now this tome. It all makes me extremely curious about Permafrost's story."
- **Ancient Writings:** a book about a demonic curse, bought from the Clothier (who is the cursed one in vanilla Terraria). The quoted line is about the "skeletal form" emerging on the fifth night, which is the Clothier's curse. **Meta.**
- **Best Price Bestiary:** a bestiary that claims a green slime has "precisely 13,575 total health." **Meta joke**, probably about Calamity's huge health numbers (my guess; the text does not say).
- **Clover Manual:** "something called a 'luck mechanic'... lantern nights, horseshoes, garden gnomes, and something mysterious called a 'Galaxy Pearl'." This is a joke on Terraria's actual luck system (note the "colored torches" rant at the end, referring to the real torch-luck quirk). Extremely rare, scaled by your Luck stat.
- **Apple Thesis:** "A dissertation on the endless uses of apples... It seems to have been written by a squirrel." Apples tie to the **Good Apple** (the Nameless Deity's reward). The squirrel author is a gag about squirrels and trees.
- **Unfinished Coloring Book:** "simplistic celestial scenes... two thirds of the scenes are colored with masterful quality. The rest are completely untouched." **My reading** (gentle): it echoes Solyn herself, a person who loves astronomy and is mid-journey. Note it can be found near her campsite. The unfinished part is a nice quiet echo of her unfinished story.
- **The Book of Shaders:** "creation of shaders and programmatically generated artwork. Seven pages are dedicated to ranting about the sanctity of pixelation... The last page stresses ... optimized." Pure developer in-joke (the mod is shader-heavy and the author is known for it). The tooltip even deliberately slows your game down while hovering it.

### The unreadable ones

- **Otherworldly Tomes I to VI:** "It's written in a language that you don't understand. Solyn might know more about this..." These are Solyn's own books from her homeworld. Stated, not interpreted: she collects them herself, with no player action, as more of the shelf is filled.
- **Aberrant Writings:** tooltip is just "'...'" **My reading:** deliberately blank; there is nothing to find.
- **Invisible Ink Dissertation:** "There's nothing visible on the pages, yet somehow you can tell there's invisible ink present." You get it by Shimmering an ordinary Book, which is a nice "transmutation" joke.
- **Frosty Pamphlet:** "It's so cold that you can't make out the words." Solyn only shows up with this after you finish Permafrost's Keep. A mood piece.
- **Decaying Tome, Dusty Diary:** their pages are rotted or turned to dust. The Dusty Diary adds the joke "A perfect find for those who enjoy reading with their imagination!"
- **Fanatical Ramblings:** "Opening the book fills you with an indescribable feeling of hatred for some reason." This is a **Calamity Remix** cross-reference (the item only spawns if Remix is installed, when you poke the "Fanny" helper enough times). The "hatred" is the Evil Fanny's voice. Fanny in the same mod even has a line that says "That damned Solyn girl", hinting the author is poking fun at the evil-Fanny rants.
- **Absence Notice:** "A mysterious piece of paper indicating someone's absence. It's signed with the letter X." Found in the depths. **Stated in code:** the book only loads if Calamity does not have its own Xeroc. So "X" is Xeroc: a note from the Nameless Deity (Xeroc) saying they are away. This is one of the few books with a real lore hook. It is a wink to Calamity players that the "god who never appeared in Calamity" has been missing.
- **Sanguine Wildlife:** has a name, but no text and is commented out in the data file. Cut content.

---

## The two lore items you carry like books

### The Avatar of Emptiness lore item ("Grave murmurs wail from beyond...")

You hold Left Shift to read it. It is written like a prose poem. Here are the key phrases and what I think they point at.

- **"Sable forms. Cryonic mist. Expanse to a concluded world"**: the Avatar's rift is cold, dark and foggy (blizzards, frozen seas, dark fog in the Rift Eclipse). "A concluded world" is its dead universe. **Stated:** the bestiary calls it "An aged deity from a universe that succumbed to time long ago."
- **"Lone, Dark One. Seek to consume, seek to explore in time denied"**: "Dark One" is the Avatar's epithet (Solyn also uses it later). "Time denied" means it was cut off from time, from a future.
- **"Mirrortwist"**: points at the Mirrorwalker enemy, the "sinister doppelganger of the self" in the fog. It is also a hint at the Avatar's mirroring second phase.
- **"Wistful reverie of scarlet infinitudes, collapsed to nil" / "Stripes of color borne back to the minimal"**: the red second-phase sky, and its colour being drained away. Entropy.
- **"Descent. Descent. Descent..."**: it is the title action. The Avatar's rift descends on the world, and the repeated line is meant to feel like falling.
- **"The mask drifts through the realm of higher beings"**: **My reading:** the mask is the Avatar's, and "higher beings" is where gods like the Nameless Deity live. It hints that the Avatar was once among them.
- **"The Dark One, the Brightest star in a perfect world"**: **My reading:** the Avatar was once beloved and bright, not a monster. Its world was "perfect" before it ended. This is where the emptiness becomes tragic.
- **"Seek thine destiny atop the bygone ashes, where the lilies shan't grow"**: addressed to you. The lilies are the Nameless Deity's garden (lotus, flowers). So: "go fight in the dead world where nothing blooms," in contrast to the Garden.

Both of Calamity Remix's Fanny lines about it (one calls it "obtuse word choices", one is baffled) are the mod making fun of its own style. **Deliberately obscure: yes**, though the images consistently point at the same things.

### The Nameless Deity lore item ("The faint songs of seraphim echo...")

- **Stated in code:** it is generated, not written. The code builds 16 lines using a grammar (a "context free grammar") from lists of about 300 nouns, 275 adjectives, 150 verbs and 42 prepositions, such as "the effulgent apple refracts a Stygian hymn". The seed depends on your character name, so **every player sees different text**, and it only changes over long periods.
- **Rare easter egg line:** "Fr ong bussin" (1 in 10,000 per line, shown in a different colour).
- **My reading:** the point is that the Nameless Deity's mind is too vast to be put in sentences. The joke built in is also that it reads like profound poetry but means nothing. The only real information is in the vocabulary: flowers (lotus, lily, camellia), stars, apples, "Xeroc-blessed", "bitheistic", "paradise". It is mood, not facts. Fanny's reaction ("I have no idea what this means. Maybe it's just some elaborate troll?") is exactly the intended response.

---

## Dialogue that explains the books (Solyn's own words)

These are the lines that help most:

- **Why books matter to her:** "Back at home I had a sizeable collection of books. A few scrolls and old tomes too... a few were a bit... strange, at least from the perspective of others."
- **When the shelf is full:** "once it's done, I'd like to make a proper library here."
- **When you leave her an unreadable book:** "are you sure some of these actually count as such? ... At least they look pretty nice on the shelf." (A gentle joke at your expense.)
- **Her farewell note, in the case she dies or leaves:** "To be completely honest with you, I'm surprised you managed to find some of these books. The Bible of Slime? How silly!" It also gives you her cinnamon roll recipe. It is the most emotional thing the bookshelf ever says.

Related, and worth knowing because they colour how to read everything above:

- **Solyn's origin (Dialog.hjson):** she is from a world "of lush overgrowth, beneath the gentle light of twin suns," where her people "sing songs to the melody of the stars." She wanted "to be close with our maker", beckoned the creator during a planetary alignment, "and I failed". **Stated.** So the "Deity" she keeps talking about is the Nameless Deity, the maker of her world. This is why she cannot read your world's books but can read the Otherworldly Tomes.
- **The Post-Nameless conversation:** her spirit says she used you and the Dark One as "a means to an end... a chance at being a heroine worthy of the respect of the Deity's favor," and guesses "Maybe my reason for being here is pity," and says she saved you because "One of us was going to die." Read that way, her eagerness through the quest had a selfish edge she now regrets. That is my interpretation of her confession.

---

## The big picture

The books look random because they are three different things stitched together: jokes for Terraria and Calamity players, small cameos of Calamity's content, and a very small handful of real lore texts. If you separate those, the confusing ones mostly stop being confusing.

What the WotG story needs you to take from the books:

1. **Solyn is a scholar who lost her library.** The bookshelf is a character beat. She is a stargazer and mage who wants "something as simple as good novels" and who treats every odd book as precious, including the joke ones.
2. **The universe in WotG is a cycle of deaths and rebirths.** The Ominous Storybook, the Avatar lore, the Metallic Chunk and the Nameless Deity's garden all say it: worlds end ("succumbed to time"), turn to "grand foam", and bloom again. The Avatar is a dead world's god; the Nameless Deity is the gardener of a living one.
3. **The Avatar is tragic, not just a monster.** "The Dark One, the Brightest star in a perfect world" is the key line. Noxus was a god of a perfect world that ended, and the rift is it trying to consume or reach something.
4. **The Nameless Deity is the benevolent creator of Solyn's world, and Xeroc in disguise.** The books use "X" and "Xeroc" as a quiet signal that this is the same being Calamity's fans knew only as a rumour.
5. **Rage of the Deities is a teaser.** Two powers fought until they were blind to time. Whether it is Nameless and Avatar, or something from Calamity's own history, is left open on purpose.
6. **The unreadable books are the point.** The Nameless Deity's lore is a randomizer; the Avatar's is a poem; the Otherworldly Tomes are in a language you cannot read. The mod wants you to feel the gap between mortals and gods, and Solyn is the bridge across it.
7. **The jokes are not distractions, they are the mod's voice.** A mod about cosmic gods that includes a book saying "Hold the jump button" is on purpose: it makes the serious bits land harder.

If you want one sentence to take away: the books are a mix of a fan community's inside jokes and a short, consistent myth about universes that die and bloom again, with Solyn as the one person who still believes someone is watching.
