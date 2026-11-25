# Levy line audit: which chatter lines are AI-written

**Provenance finding, plainly:** every line in `Dialogue/en-US.txt` came from an agent commit. There is no commit that says "Eve's lines" for the main chatter file. The only lines I can prove are hers are the ones the ROADMAP names as hers (see "Hers" below). The file's first snapshot (commit d7ffbbe, 167 lines still survive from it) has no recorded author; I call those PROBABLY AI because of style, but a few could be her originals. Tell me if you recognise any.

**Verdict key:** AI-WRITTEN = an agent commit or ROADMAP note says so. PROBABLY AI = origin not recorded, style reads agent-made. HERS = documented as Eve's. Verdicts are by provenance first, style second.

## Summary counts

| Section | AI-WRITTEN | PROBABLY AI | HERS |
|---|---|---|---|
| 1. Humorous / joking lines | 1163 | 70 | 0 (see Hers) |
| 2. Other AI-written lines (not jokes) | 1187 | 0 | 0 |
| Hers (documented) | 0 | 0 | 26 (+14 in BossLines/Potions) |

Spoiler rule: 0 AI-written lines tied to The Stars Above, Starlight River, Redemption or The Forgotten exist in the files, so nothing was omitted. Calamity and Wrath of the Gods lines are shown.

How to read a row: "line text" · pool (key and temperament) · file:line · verdict · reason. Line numbers are en-US.txt unless the file is named. Temperament is shown after a bar, e.g. IDLE|Cynic.

Notes that matter for your rewrite:
- The "[grumpy]" Blood Moon sets are sarcastic/irritable by design (your Nov-15 spec); the non-combat ones are in section 1, the plain combat shouts are in section 2.
- Officer IDLE/MARCH/HOLD pools are almost all quips (391 agent-written lines in bc17c21); their FIGHT/BOSS/HURT/VICTORY shouts are plain and are in section 2 except a few puns.
- Rift (RIFT_*) and fight (SCENE_*) lines were written by builders; ROADMAP says you approved the rift lines, so "approved" is not the same as "wrote". Dark-humour ones are in section 1.
- Several joke formulas repeat across hundreds of lines (gods-agree, "I'd like it noted", "not X, I'm Y", setup + 3-word punchline, "pay me" for Sellswords). The style tag at the end of a row names the formula.
- Calamity boss text in BossLines (section 2) is AI-adapted Calamity dialogue; NORTH_STAR says you do not want AI-written Calamity lines.

## 1. Humorous and joking lines (AI-written or probably AI)

### IDLE (151)
- "I polished my armor this morning. A slime sat on it." · IDLE|Greenhorn · en-US.txt:126 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I named my gun Gerald. Gerald's the only one who listens." · IDLE|Greenhorn · en-US.txt:127 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I counted every block in this wall twice and got two different numbers." · IDLE|Greenhorn · en-US.txt:128 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "My feet hurt. Nobody mentioned how much of this is standing." · IDLE|Greenhorn · en-US.txt:129 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I only got three minutes of training. I'm trying to remember all of it." · IDLE|Greenhorn · en-US.txt:130 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "My mother thinks I work in an office. I haven't had the heart to tell her." · IDLE|Greenhorn · en-US.txt:132 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I'm going to be brave tomorrow. Today I'm just standing here." · IDLE|Greenhorn · en-US.txt:133 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "The veterans are making fun of me. They're being nice about it." · IDLE|Greenhorn · en-US.txt:135 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Loyalty's extra. Ask about our rates." · IDLE|Sellsword · en-US.txt:139 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "I've fought for worse people for less money. Not many, though." · IDLE|Sellsword · en-US.txt:140 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "My contract says reasonable danger. We disagree on reasonable." · IDLE|Sellsword · en-US.txt:141 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "I'd die for you, {Player}. I'd want it in writing, and I'd want it in advance." · IDLE|Sellsword · en-US.txt:142 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: "I'd like it noted" bit
- "Silver's fine. Gold's better. Platinum, and I might even like you." · IDLE|Sellsword · en-US.txt:143 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]
- "Slimes drop gel. Zombies drop nothing. I'm in the wrong line of work." · IDLE|Sellsword · en-US.txt:144 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]
- "The last employer paid in exposure. I still have the sunburn." · IDLE|Sellsword · en-US.txt:145 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Wage} a day and I get this view. I've had worse for less." · IDLE|Sellsword · en-US.txt:146 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Cowardice is free. Heroics are extra." · IDLE|Sellsword · en-US.txt:147 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Payday's the only holiday I observe." · IDLE|Sellsword · en-US.txt:148 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "If the armory has a better sword, I'd like to be considered. In writing." · IDLE|Sellsword · en-US.txt:149 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I don't pick sides. I pick whoever pays on time. That's you, for now." · IDLE|Sellsword · en-US.txt:150 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Every slime slain is a prayer answered. I've said forty today." · IDLE|Zealot · en-US.txt:153 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "We are the wall, and the wall is holy. Please don't lean on the wall." · IDLE|Zealot · en-US.txt:154 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I prayed for a miracle and received a Minishark. I accept it." · IDLE|Zealot · en-US.txt:155 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "If I fall, don't mourn. Just move me out of the doorway." · IDLE|Zealot · en-US.txt:156 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Death is only a door, {Player}. I'm told it's a loud one." · IDLE|Zealot · en-US.txt:157 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "The boss mentioned a bonus for enthusiasm. I'm being enthusiastic where they can see." · IDLE|Zealot · en-US.txt:159 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Zombies should knock first. K'Or'Arath has said as much." · IDLE|Zealot · en-US.txt:160 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Torches belong on the left of the door. Mother Gnash agrees." · IDLE|Zealot · en-US.txt:161 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: gods-agree formula
- "The Drowned One has never once asked for a second opinion. Neither do I." · IDLE|Zealot · en-US.txt:162 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I fear the Wall of Flesh. That's not doubt, it's good sense." · IDLE|Zealot · en-US.txt:163 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Another day, another chance to die for someone else's basement." · IDLE|Cynic · en-US.txt:167 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "Sky's quiet. Last time it was quiet, a giant eyeball showed up." · IDLE|Cynic · en-US.txt:168 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]
- "I'm not saying we're doomed. I'm saying I've started packing." · IDLE|Cynic · en-US.txt:169 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: "not X, I'm Y" swap
- "I didn't volunteer. I was volunteered, retroactively." · IDLE|Cynic · en-US.txt:170 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Hope is what they call it before the Blood Moon." · IDLE|Cynic · en-US.txt:171 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Everyone wants a hero. Nobody wants to pay for the funeral." · IDLE|Cynic · en-US.txt:172 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "They gave me a spear and a motto. The motto broke first." · IDLE|Cynic · en-US.txt:173 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "If this job were safe, they'd have paid less." · IDLE|Cynic · en-US.txt:174 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I'll fight. I just want it noted that I said it would go badly." · IDLE|Cynic · en-US.txt:175 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: "I'd like it noted" bit
- "You built a wall out of dirt and it's holding. I'm as surprised as you are." · IDLE|Cynic · en-US.txt:176 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I'd follow you anywhere, {Player}. That's what worries me." · IDLE|Cynic · en-US.txt:177 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Don't mind me. I complain at the same volume I fight." · IDLE|Cynic · en-US.txt:178 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Rookies ask where the front is. I ask where the snacks are." · IDLE|Veteran · en-US.txt:181 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "I've seen bosses die. They look surprised every time." · IDLE|Veteran · en-US.txt:183 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "A good soldier knows when to run. A veteran knows where the Nurse is." · IDLE|Veteran · en-US.txt:184 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I've outlived better soldiers than me. Mostly by standing behind them." · IDLE|Veteran · en-US.txt:185 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "You get used to the screaming. Then you get used to it being yours." · IDLE|Veteran · en-US.txt:186 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Stand next to me, greenhorn. I'll show you what calm looks like. Mostly it's boredom." · IDLE|Veteran · en-US.txt:187 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I remember running from slimes. I don't remember it fondly." · IDLE|Veteran · en-US.txt:188 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I keep a tally on my arm. It's a long arm." · IDLE|Veteran · en-US.txt:190 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I'm not worried about the next one. I'm worried about the paperwork after." · IDLE|Veteran · en-US.txt:191 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: "not X, I'm Y" swap
- "Steady hands, {Player}. That's the whole secret. The rest is luck and boots." · IDLE|Veteran · en-US.txt:192 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "All quiet. I hate it when it's quiet." · IDLE · en-US.txt:195 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Just standing here. It's most of the job." · IDLE · en-US.txt:196 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "[non-summoner] If a summoner's minion kills something, who gets the credit? Don't answer. I'll get angry." · IDLE · en-US.txt:205 · AI-WRITTEN · agent chatter batch 1 extras [3f24205]; Eve liked this one most and kept it - her call; style: setup + short punchline
- "[mage] I'm a good mage! I just, um, sometimes the fire goes the wrong way! It's the fire's fault! It was a really nice tent, too!" · IDLE · en-US.txt:222 · AI-WRITTEN · agent "extras in her voice" (ROADMAP: pending her veto) [35ecda3]
- "[mage] Okay so I tried to heal everybody and it, uh, did not heal everybody! But I meant to! That's what counts, right?!" · IDLE · en-US.txt:223 · AI-WRITTEN · agent "extras in her voice" (ROADMAP: pending her veto) [35ecda3]; style: setup + short punchline
- "[non-mage] The mages mean well, they do, but if one more of them sets my boots on fire by accident I'm going to scream!" · IDLE · en-US.txt:224 · AI-WRITTEN · agent "extras in her voice" (ROADMAP: pending her veto) [35ecda3]
- "[non-summoner] They gave a skeleton my job! A skeleton! It doesn't even get paid! That's not fair to us OR the skeleton!" · IDLE · en-US.txt:225 · AI-WRITTEN · agent "extras in her voice" (ROADMAP: pending her veto) [35ecda3]
- "[summoner] I'm not taking anybody's job! I'm making MORE jobs! For minions! Is that not a job?! It's a job!" · IDLE · en-US.txt:226 · AI-WRITTEN · agent "extras in her voice" (ROADMAP: pending her veto) [35ecda3]; style: setup + short punchline
- "[mage] The gunners say my spells are imprecise. They hit things. I thought that was the point." · IDLE · en-US.txt:229 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "[non-mage] The mages are loud, they're sorry, and they always bring snacks. I can't stay mad." · IDLE · en-US.txt:230 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "[summoner] The gunners keep asking who's doing the shooting. Everybody is. I'm just not in the room." · IDLE · en-US.txt:231 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "[non-summoner] Their minions are cute. I'll give them that. Don't tell them I said so." · IDLE · en-US.txt:232 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "[non-summoner] I pray for the summoners every night. Out loud, near them, so they know." · IDLE · en-US.txt:233 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "[non-mage] Mages are very kind and very flammable. I pray for them in that order." · IDLE · en-US.txt:234 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "The gunners say the front line is overrated. The front line says that's easy to say from back there." · IDLE · en-US.txt:235 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Every time you open a chest, {Player}, I lean in. I can't help it." · IDLE · en-US.txt:238 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "If you find a heart crystal, I'll carry it back for you. I'll only look at it a little." · IDLE · en-US.txt:239 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Whoever keeps the armory, thank you. My boots fit and I don't know how." · IDLE · en-US.txt:240 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I keep hoping the next thing out of the armory has my name on it." · IDLE · en-US.txt:241 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "The armory smells like oil and new leather. I could stand in there all day." · IDLE · en-US.txt:242 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Somebody found a golden chest and didn't tell me. I'm not bitter. I'm watching." · IDLE · en-US.txt:243 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "[ranger] The sword folk throw their beams from way back there and call it close quarters." · IDLE · en-US.txt:246 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[ranger] My bullets curve. That isn't a scandal. That's called aim." · IDLE · en-US.txt:247 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[ranger] They say I shoot from too far. I'd like to watch them swing from here." · IDLE · en-US.txt:248 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[ranger] Everyone says rangers are quiet. We're just waiting for the swords to get out of the way." · IDLE · en-US.txt:249 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[ranger] I don't need a sword. I need a taller wall and a better angle." · IDLE · en-US.txt:250 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[ranger] Every bullet is a prayer. Please pray it hits the thing, not me." · IDLE|Zealot · en-US.txt:251 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[melee] I'm going to charge in someday. Today I'm going to stand here and swing at the air." · IDLE · en-US.txt:252 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[melee] The gunners think I'm brave for standing this close. I'm forty feet back. Don't correct them." · IDLE · en-US.txt:253 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[melee] They call melee the front line. The front line is a state of mind, and mine is about forty feet back." · IDLE · en-US.txt:254 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[melee] Swords have honor. Bullets have reach. I'm telling myself honor." · IDLE · en-US.txt:255 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[melee] The gunners keep judging my range. My range is heartfelt." · IDLE · en-US.txt:256 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[melee] If I ran in, I'd be the first to go down. So, no. I'm very brave from here." · IDLE|Cynic · en-US.txt:257 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[early] Everything out there is bigger than me. Everything. I measured." · IDLE|Greenhorn · en-US.txt:260 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[early] If the eye comes back, I'll be standing very still. Please tell it I'm furniture." · IDLE · en-US.txt:261 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[early] We've killed a lot of slimes. I'd like it written somewhere that we killed a lot of slimes." · IDLE · en-US.txt:262 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[early] The man outside the dungeon never waves back. I've tried twice." · IDLE · en-US.txt:263 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[early] I got a real sword this week. It's wood. It's a real wood sword." · IDLE · en-US.txt:264 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[early] Somebody says there's a bigger thing underground. I'm choosing not to hear it." · IDLE · en-US.txt:265 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[postskeletron] I've seen the dungeon. I wasn't scared. I was a little scared. I was scared." · IDLE · en-US.txt:266 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[postskeletron] The man outside the dungeon went inside. I'd like to say I'm happy for him." · IDLE · en-US.txt:267 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[postskeletron] Bones everywhere, and now we're the bones' problem." · IDLE · en-US.txt:268 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[postskeletron] I walked out of the dungeon. Very few get to say that, and I say it often." · IDLE|Veteran · en-US.txt:269 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[postskeletron] The moon feels different since the skull went down. Quieter. Quieter's worse." · IDLE · en-US.txt:270 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[hardmode] The whole world changed and we're still standing here. I'm calling that a win." · IDLE · en-US.txt:271 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[hardmode] I used to run from skeletons. Now I charge the ones with armor. Mostly out of spite." · IDLE · en-US.txt:272 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[hardmode] The sky tastes different since the wall went down. Metallic. Like a coin." · IDLE · en-US.txt:273 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[hardmode] The ore's glowing now. I don't think it was glowing before. Nobody's saying anything." · IDLE · en-US.txt:274 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[hardmode] Everything's tougher now. Great. We were doing so well." · IDLE|Cynic · en-US.txt:275 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[hardmode] They put a pastel plain right next to the corruption. The world has no taste." · IDLE · en-US.txt:276 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[postplantera] I've been in the jungle's heart. I'm not scared of a Tuesday." · IDLE|Veteran · en-US.txt:277 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[postplantera] Let them come. I'll show them what the armory made me." · IDLE · en-US.txt:278 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[postplantera] The dungeon's open now. I'd say I'm excited. I'm saying it. I'm excited." · IDLE · en-US.txt:279 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[postplantera] I've been told the green things don't bite anymore. They still bite. They just lost the bragging rights." · IDLE · en-US.txt:280 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[postmoonlord] The moon fell over, and I was there. What exactly are they going to throw at me now?" · IDLE · en-US.txt:281 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[postmoonlord] I've seen the sky crack. After that, a zombie is a very small topic." · IDLE · en-US.txt:282 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[postmoonlord] We stood in front of the moon. I'm going to be insufferable about it for years." · IDLE|Veteran · en-US.txt:283 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[postmoonlord] There's nothing left to beat. I'd like to be assigned something. Anything. A hill." · IDLE · en-US.txt:284 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[female] They keep sending helmets sized for broad shoulders. I've stuffed mine with socks." · IDLE · en-US.txt:287 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[female] If one more recruit asks whether I'm somebody's sister, I'm going to be somebody's problem." · IDLE · en-US.txt:288 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[female] The barracks has one decent mirror. Guess who's up first for it." · IDLE · en-US.txt:289 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Rigroth the Undying would NOT support sentry staves." · IDLE|Zealot · en-US.txt:292 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Moats are pointless. K'Or'Arath says so." · IDLE|Zealot · en-US.txt:293 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Socks go on before boots. Ixxethra the Star-Eater agrees." · IDLE · en-US.txt:294 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "There's a reason Ul'Thazhra the Unblinking never wore a helmet." · IDLE · en-US.txt:295 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Wings are a vanity. The Drowned One has never needed them." · IDLE|Zealot · en-US.txt:296 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Nobody should talk before the first coffee. Vorrath of the Thousand Teeth agrees." · IDLE|Cynic · en-US.txt:297 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "Feet on the ground. Vote Pro-Ground." · IDLE · en-US.txt:300 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: Pro-Ground running gag
- "The ground has never let me down. Not once. Not even the pit." · IDLE · en-US.txt:301 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Wings are for people with something to prove. I have a perfectly good floor." · IDLE · en-US.txt:302 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Fly if you like. I'll be here, on the ground, where it's nice." · IDLE · en-US.txt:303 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "If we were meant to fly, they'd have given the wings a union." · IDLE · en-US.txt:304 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "[grumpy] Don't talk to me until the sky's a normal color." · IDLE · en-US.txt:1133 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm fine. I'm fine. Stop asking if I'm fine." · IDLE · en-US.txt:1134 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything's red and everything's loud, and I'd like to be anywhere else." · IDLE · en-US.txt:1135 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] If one more zombie moans at me, I'm going to moan back." · IDLE · en-US.txt:1136 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm not mad at you, {Player}. I'm mad near you." · IDLE · en-US.txt:1137 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The moon's up and my patience isn't. Let's leave it at that." · IDLE · en-US.txt:1138 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Please don't stand so close. I can hear you breathing." · IDLE · en-US.txt:1139 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I haven't slept, I haven't eaten, and the sky's the color of a bruise." · IDLE · en-US.txt:1140 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Whatever you came to say, I'd bet it's the wrong time." · IDLE · en-US.txt:1141 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Everything's too loud tonight. Even the quiet is loud." · IDLE · en-US.txt:1142 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm supposed to be on guard. I'm on edge. It's close enough." · IDLE · en-US.txt:1143 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Can the zombies please take a night off? Just one? I'll pay them." · IDLE · en-US.txt:1144 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't pat my shoulder. I'm in no state." · IDLE · en-US.txt:1145 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Sure. Sure! The moon's red. Why wouldn't it be." · IDLE · en-US.txt:1146 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'd like it noted that I did not ask for tonight." · IDLE · en-US.txt:1147 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "I'd like it noted" bit
- "[grumpy] Every single thing is irritating me, and I'm including you." · IDLE · en-US.txt:1148 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Ask me again tomorrow. In the daylight. From farther away." · IDLE · en-US.txt:1149 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'll fight whatever you like. I just won't enjoy the conversation first." · IDLE · en-US.txt:1150 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm sharpening something. I'm not saying what." · IDLE · en-US.txt:1151 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If I wanted company, I'd have said so. Out loud." · IDLE · en-US.txt:1152 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Do I look okay? Don't answer. I know." · IDLE · en-US.txt:1153 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] My feet hurt, my head hurts, and nobody told the moon." · IDLE · en-US.txt:1154 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm not scared. I'm cross. They look the same from outside." · IDLE|Greenhorn · en-US.txt:1155 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Blood Moon pay is time and a half. Nobody agreed to that. I'm taking it anyway." · IDLE|Sellsword · en-US.txt:1156 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The moon is wrong. Rigroth the Undying agrees, and he's irritable tonight too." · IDLE|Zealot · en-US.txt:1157 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] Of course the sky's red. It's the only thing this week that matches." · IDLE|Cynic · en-US.txt:1158 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I've stood a hundred of these. They don't get easier. I just get meaner." · IDLE|Veteran · en-US.txt:1159 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### IDLE_NIGHT (13)
- "Night shift again. Did I enlist, or get sentenced?" · IDLE_NIGHT · en-US.txt:307 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "The moon's out, the zombies are out, and I'm out of patience." · IDLE_NIGHT · en-US.txt:308 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I'd count the stars, but the zombies keep interrupting." · IDLE_NIGHT · en-US.txt:309 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "It's dark and I'm responsible for it. Nobody consulted me about the dark." · IDLE_NIGHT · en-US.txt:310 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Cold, dark, and I'm not allowed to nap. This is what they call a career." · IDLE_NIGHT · en-US.txt:311 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "I can hear things out there. They're doing their best." · IDLE_NIGHT · en-US.txt:312 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Everything's scarier in the dark. Including the guy next to me." · IDLE_NIGHT · en-US.txt:314 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "The moon's too loud at night. Mother Gnash agrees." · IDLE_NIGHT · en-US.txt:315 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] The night's long, and I'm longer. Don't test me." · IDLE_NIGHT · en-US.txt:1171 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm on watch. I'm watching you, too. Back up a bit." · IDLE_NIGHT · en-US.txt:1172 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Dark, cold and loud. Everything I like the least." · IDLE_NIGHT · en-US.txt:1173 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If you whisper, I'll hear it. If you whistle, I'll end you." · IDLE_NIGHT · en-US.txt:1174 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything's moving out there. I'm too tired to be scared and too cross to look away." · IDLE_NIGHT · en-US.txt:1175 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### NAMEDROP (41)
- "{ArmsDealer} says he has a bigger gun for me. He says that to everyone, and I keep going back." · NAMEDROP · en-US.txt:318 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "{Nurse} charged me two silver to say I was fine. I had an arrow in me." · NAMEDROP · en-US.txt:319 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Demolitionist} says the barracks is a good place to test things. I've started sleeping outside." · NAMEDROP · en-US.txt:320 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Guide} gave me a survival tip. I asked him about lava and he went quiet." · NAMEDROP · en-US.txt:321 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "{TaxCollector} tried to tax my wages. I showed him the sword. He said he'd tax that too." · NAMEDROP · en-US.txt:322 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "{Merchant} sold me a torch for a silver and called it military grade." · NAMEDROP · en-US.txt:323 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "{Dryad} asked me to walk around the flowers. Everyone walks around the flowers now." · NAMEDROP · en-US.txt:324 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Clothier} offered to tailor my uniform. It's plate armor. I thanked him anyway." · NAMEDROP · en-US.txt:325 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{PartyGirl} wants to throw a party for the fallen. I told her they can't come." · NAMEDROP · en-US.txt:326 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{PartyGirl} threw confetti at a zombie. It followed her home. She's thrilled." · NAMEDROP · en-US.txt:327 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Tavernkeep} says his ale is liquid courage. I've had it. It's liquid." · NAMEDROP · en-US.txt:328 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Mechanic} wants to add wires to my rifle. She says everything needs more wires." · NAMEDROP · en-US.txt:329 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{GoblinTinkerer} offered to reforge my armor. I've seen his face when he says reforge." · NAMEDROP · en-US.txt:330 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "{Wizard} enchanted my sword. It hums now. I haven't asked what it's humming." · NAMEDROP · en-US.txt:331 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Cyborg} called me statistically fragile. I looked it up. He's right." · NAMEDROP · en-US.txt:332 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Pirate} wants me to carry a parrot. The parrot wants a cut." · NAMEDROP · en-US.txt:333 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Truffle} says he's a fun guy. I decided not to ask." · NAMEDROP · en-US.txt:334 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Stylist} says my helmet doesn't suit me. I'm keeping the helmet." · NAMEDROP · en-US.txt:335 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Angler} told me to go catch him a fish. I said I'm a soldier. He called me a minion." · NAMEDROP · en-US.txt:336 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{DyeTrader} offered to dye my armor black. It's already black. He said blacker." · NAMEDROP · en-US.txt:337 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{Painter} painted the barracks again. It's very wet in there." · NAMEDROP · en-US.txt:338 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{WitchDoctor} gave me a charm against bad luck. A zombie bit it. Bad luck for the zombie." · NAMEDROP · en-US.txt:339 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Golfer} says a fairway is the only honest battlefield. I said that's a lawn." · NAMEDROP · en-US.txt:340 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Zoologist} wants to study the zombies. I told her they study us. She wrote that down." · NAMEDROP · en-US.txt:341 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{Princess} thanked me for my service. I looked behind me. She meant me." · NAMEDROP · en-US.txt:342 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{SantaClaus} gave me a lump of coal. It was a good lump. I kept it." · NAMEDROP · en-US.txt:343 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{ArmsDealer} and {Demolitionist} are arguing over whose bang is bigger. We're standing very far back." · NAMEDROP · en-US.txt:344 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Nurse} says I should rest. {Guide} says I should read. I'm going to stand here, thanks." · NAMEDROP · en-US.txt:345 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{Dryad} blessed my armor. It stayed clean all day. Then it rained." · NAMEDROP · en-US.txt:346 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{Mechanic} and {Steampunker} are building something out back. We agreed not to ask." · NAMEDROP · en-US.txt:347 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Demolitionist} says I stand too far from the explosions. He says it like it's a problem." · NAMEDROP · en-US.txt:348 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "{Clothier} says he used to be cursed. He says it in the same voice as good morning." · NAMEDROP · en-US.txt:349 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "{Nurse} patched me up and told me to stay off my feet. I'm a guard. I told her." · NAMEDROP · en-US.txt:350 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "{Merchant} says everything's a bargain. Rigroth the Undying has always disagreed." · NAMEDROP · en-US.txt:351 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] {Guide} told me to stay calm. I told {Guide} where to put the calm." · NAMEDROP · en-US.txt:1177 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] {Merchant} asked me to buy something tonight. Tonight! With that sky!" · NAMEDROP · en-US.txt:1178 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] {Nurse} asked if I needed anything. I said quiet. She said that's not on the list." · NAMEDROP · en-US.txt:1179 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] {ArmsDealer} says it's a good night for it. For what? He didn't say." · NAMEDROP · en-US.txt:1180 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] {Demolitionist} is cheerful again. I'd like to personally ask him to stop." · NAMEDROP · en-US.txt:1181 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] {TaxCollector} is on a rant about rent. For once, I'm on his side." · NAMEDROP · en-US.txt:1182 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] {Dryad} keeps humming. I'm not in the mood for humming, {Dryad}." · NAMEDROP · en-US.txt:1183 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### BLOOD_MOON (19)
- "Blood Moon again. I clocked out an hour ago, and nobody told the zombies." · BLOOD_MOON · en-US.txt:354 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Don't talk to me until the sky is a normal color." · BLOOD_MOON · en-US.txt:355 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I'm not angry at you, {Player}. You're just standing closest to everything I'm angry at." · BLOOD_MOON · en-US.txt:356 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "The moon's red, my feet are wet, and something groaned at me on the way here. Give me a minute." · BLOOD_MOON · en-US.txt:357 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Every Blood Moon I promise myself I won't be cranky. I'm always cranky." · BLOOD_MOON · en-US.txt:358 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "If one more zombie knocks, I'm going to answer, and neither of us will like it." · BLOOD_MOON · en-US.txt:359 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "I was promised weekends. The moon doesn't do weekends." · BLOOD_MOON · en-US.txt:360 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "The water's red, the zombies are loud, and I'd like my bed back." · BLOOD_MOON · en-US.txt:361 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Somebody tell the moon I'm trying to nap." · BLOOD_MOON · en-US.txt:362 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Please don't be nice to me tonight. It makes it harder to stay annoyed." · BLOOD_MOON · en-US.txt:363 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "[grumpy] Don't talk to me. Don't wave. Don't smile at me." · BLOOD_MOON · en-US.txt:1161 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The moon's red, the water's red, and my mood is also red." · BLOOD_MOON · en-US.txt:1162 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Every Blood Moon I swear I'll be calm. I'm never calm." · BLOOD_MOON · en-US.txt:1163 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] You want a favor? Come back at dawn. I'm busy being furious." · BLOOD_MOON · en-US.txt:1164 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If it isn't a zombie, I'm not interested. If it is one, I'm still not." · BLOOD_MOON · en-US.txt:1165 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm not in a mood. I'm in the moon's mood. It's much worse." · BLOOD_MOON · en-US.txt:1166 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Somebody shoot the moon. I'm not asking. I'm suggesting." · BLOOD_MOON · en-US.txt:1167 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] You're nice. Everyone's nice. I'm sick of it. Be rude, it's easier." · BLOOD_MOON · en-US.txt:1168 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm going to stay cross for the whole night, so you can all relax about it." · BLOOD_MOON · en-US.txt:1169 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### PARTY (10)
- "I'm on duty. Is there cake? I can be on duty near cake." · PARTY · en-US.txt:365 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "A party! Finally, a threat I understand." · PARTY · en-US.txt:366 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Confetti, cake, balloons. Somebody forgot there's a war on. Don't remind them." · PARTY · en-US.txt:367 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "{PartyGirl} said I could dance. I said I'm on watch. She said I could watch dancing." · PARTY · en-US.txt:368 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Nobody's shooting at the balloons. I checked. I'm very proud of us." · PARTY · en-US.txt:369 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Cake first, speeches after. Ul'Thazhra the Unblinking agrees." · PARTY · en-US.txt:370 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] A party. Tonight. Of all the nights. Don't hand me a hat." · PARTY · en-US.txt:1185 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If anyone sings, I leave. If anyone sings badly, I stay and make a scene." · PARTY · en-US.txt:1186 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Confetti in my hair. I'm choosing to be furious, not festive." · PARTY · en-US.txt:1187 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Cake's fine. The cheering is the problem." · PARTY · en-US.txt:1188 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### GRAVEYARD (9)
- "So many headstones. I'm not reading the names. Don't read the names." · GRAVEYARD · en-US.txt:372 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "A graveyard. Nothing says good luck like a row of headstones." · GRAVEYARD · en-US.txt:373 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "The ghosts are taking attendance. I don't want to be counted." · GRAVEYARD · en-US.txt:374 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Nice cemetery. Good drainage. I checked." · GRAVEYARD · en-US.txt:375 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Headstones should face the door. The Drowned One agrees." · GRAVEYARD · en-US.txt:377 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] A graveyard under a Blood Moon. Who planned this?" · GRAVEYARD · en-US.txt:1190 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Every headstone's looking at me. I'm looking back. I'm winning." · GRAVEYARD · en-US.txt:1191 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Ghosts, headstones and a red sky. I'm going to be rude to all three." · GRAVEYARD · en-US.txt:1192 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Leave the dead alone and leave me alone. In that order." · GRAVEYARD · en-US.txt:1193 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### RAIN (11)
- "Rain again. The armor rusts, the gun jams, the pay stays the same." · RAIN · en-US.txt:379 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "Rain. The slimes love it. Nobody else does." · RAIN · en-US.txt:380 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "Wet armor squeaks. Every step announces me." · RAIN · en-US.txt:381 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "It's raining. At least it's not raining slimes." · RAIN · en-US.txt:382 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "{Player}, I don't mind the rain. I mind the squelch." · RAIN · en-US.txt:383 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "My boots have a pond in them. It's the nicest thing I own." · RAIN · en-US.txt:384 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Rain on a helmet is a personal insult. K'Or'Arath agrees." · RAIN · en-US.txt:385 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] Rain and a Blood Moon. The sky hates me personally." · RAIN · en-US.txt:1195 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] My boots are wet. My mood's wetter. Don't speak." · RAIN · en-US.txt:1196 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If it's going to rain, it should rain on someone else." · RAIN · en-US.txt:1197 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Red rain, red moon. I'd like a nice, boring gray day, please." · RAIN · en-US.txt:1198 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### BIOME_DUNGEON (7)
- "I've got a bad feeling about this place. The feeling has bones." · BIOME_DUNGEON · en-US.txt:388 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Somebody built this to keep something in. Looking at the bones, it wasn't us." · BIOME_DUNGEON · en-US.txt:389 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Skeletons are rude. They never say hello. They just rattle." · BIOME_DUNGEON · en-US.txt:390 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "[grumpy] The dungeon's bad enough in daylight. Now it's red inside, too." · BIOME_DUNGEON · en-US.txt:1204 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Skeletons, bats and a bad mood. I'm only one of those three." · BIOME_DUNGEON · en-US.txt:1205 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything rattles. I'm rattling back." · BIOME_DUNGEON · en-US.txt:1206 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'd shout at the walls, but they'd shout back." · BIOME_DUNGEON · en-US.txt:1207 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### BIOME_UNDERWORLD (7)
- "If I die here, tell my mother I was in the Jungle." · BIOME_UNDERWORLD · en-US.txt:392 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "It's hot and it's hell. Please tell me this is overtime." · BIOME_UNDERWORLD · en-US.txt:393 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Everything here wants to hug me with fire. I decline all hugs." · BIOME_UNDERWORLD · en-US.txt:394 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "[grumpy] It's hot, it's red, and I'm done. Everything's already on fire. What else do you want from me?" · BIOME_UNDERWORLD · en-US.txt:1208 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Fire, lava and a Blood Moon I can't even see from down here. I can feel it." · BIOME_UNDERWORLD · en-US.txt:1209 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything here wants to hug me with flames. I decline, rudely." · BIOME_UNDERWORLD · en-US.txt:1210 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'll be cross in hell. I'll be cross in a better place. Same thing." · BIOME_UNDERWORLD · en-US.txt:1211 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### BIOME_JUNGLE (8)
- "Everything here has thorns. Everything. I checked." · BIOME_JUNGLE · en-US.txt:396 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "I heard something move. Then I heard everything move." · BIOME_JUNGLE · en-US.txt:397 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "The vines are following me. They're slow, but they're committed." · BIOME_JUNGLE · en-US.txt:398 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Shoes stay on in a jungle. Vorrath of the Thousand Teeth agrees." · BIOME_JUNGLE · en-US.txt:399 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] Thorns, vines and a red moon. I'll scream at the next leaf." · BIOME_JUNGLE · en-US.txt:1200 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything here bites, and I'm in a biting mood. Let's see who wins." · BIOME_JUNGLE · en-US.txt:1201 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The jungle's loud enough on a good night. On this one it's unbearable." · BIOME_JUNGLE · en-US.txt:1202 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm not walking into that bush. The bush can come to me." · BIOME_JUNGLE · en-US.txt:1203 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### BIOME_SNOW (8)
- "Plate armor in a snowstorm. It's like hugging an icicle." · BIOME_SNOW · en-US.txt:401 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "My nose has gone. I'd like it back at some point." · BIOME_SNOW · en-US.txt:402 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "Snow, wind and a very sarcastic penguin. Fantastic location." · BIOME_SNOW · en-US.txt:403 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Mittens are a sign of good character. Mother Gnash agrees." · BIOME_SNOW · en-US.txt:404 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] Cold and cross. Please don't talk to me until I thaw." · BIOME_SNOW · en-US.txt:1212 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] The snow's red under that moon. I hate it. I hate it a lot." · BIOME_SNOW · en-US.txt:1213 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] My fingers are numb. My patience is numb. Everything's numb but my temper." · BIOME_SNOW · en-US.txt:1214 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't build a snowman. I'll shoot it." · BIOME_SNOW · en-US.txt:1215 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### BIOME_CORRUPTION (7)
- "The ground is purple and it's humming. I'm not fighting the ground." · BIOME_CORRUPTION · en-US.txt:406 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "I touched a thorn. My glove is gone. The thorn says hello." · BIOME_CORRUPTION · en-US.txt:407 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "The worms are enthusiastic here. I admire that in a worm." · BIOME_CORRUPTION · en-US.txt:408 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "[grumpy] Purple ground, red sky. Somebody lost an argument with a painter." · BIOME_CORRUPTION · en-US.txt:1216 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] It hums. Everything hums. I'm going to hum back at a very high pitch." · BIOME_CORRUPTION · en-US.txt:1217 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm not touching anything here. I'm not touching anything anywhere." · BIOME_CORRUPTION · en-US.txt:1218 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Even the thorns look annoyed. We have that in common." · BIOME_CORRUPTION · en-US.txt:1219 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### BIOME_CRIMSON (8)
- "It's meaty here. Why is the ground meaty." · BIOME_CRIMSON · en-US.txt:410 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "The floor moved. The floor shouldn't move. I'm filing a complaint with the floor." · BIOME_CRIMSON · en-US.txt:411 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "Everything here is pulsing in rhythm. I hate the rhythm." · BIOME_CRIMSON · en-US.txt:412 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Is that a heart in the ground? Nobody say anything." · BIOME_CRIMSON · en-US.txt:413 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "[grumpy] Red ground under a red moon. I give up on color." · BIOME_CRIMSON · en-US.txt:1220 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] It's pulsing. The ground's pulsing. I'm not in the mood to be pulsed at." · BIOME_CRIMSON · en-US.txt:1221 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] It smells like meat. It's all meat. Leave me out of it." · BIOME_CRIMSON · en-US.txt:1222 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything here twitches. I twitch back. Let's see who flinches." · BIOME_CRIMSON · en-US.txt:1223 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### BIOME_HALLOW (8)
- "They told me about the zombies. Nobody mentioned the unicorns." · BIOME_HALLOW · en-US.txt:415 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Everything is shiny and pointy and all of it is looking at me." · BIOME_HALLOW · en-US.txt:416 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "The pixies are cute. The pixies are armed. Cute and armed is the worst combination." · BIOME_HALLOW · en-US.txt:417 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "It's so pretty here I feel rude carrying a gun." · BIOME_HALLOW · en-US.txt:418 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "[grumpy] It's pretty here. It's pretty and I'm cross. I'd like to be left to be cross." · BIOME_HALLOW · en-US.txt:1224 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] The pixies are cute. They're cute and nearby. Please make them go." · BIOME_HALLOW · en-US.txt:1225 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Red moon over sparkles. Somebody's having a worse day than I am. Not many." · BIOME_HALLOW · en-US.txt:1226 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't be cheerful at me. I can see the rainbow. I'm ignoring it." · BIOME_HALLOW · en-US.txt:1227 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### BIOME_OCEAN (7)
- "Sharks. I love sharks. Had a Minishark once. It bit me." · BIOME_OCEAN · en-US.txt:420 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "The sand is in everything. The sand is in my gun. The gun is now sand." · BIOME_OCEAN · en-US.txt:421 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Seagulls, crabs and the sound of someone else's vacation." · BIOME_OCEAN · en-US.txt:422 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "[grumpy] Waves, sand and a red moon. I'm not building a sandcastle." · BIOME_OCEAN · en-US.txt:1228 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If a crab pinches me tonight, it's a declaration of war." · BIOME_OCEAN · en-US.txt:1229 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] The sea's red. I'm red. Let's all have a moment." · BIOME_OCEAN · en-US.txt:1230 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The seagulls are loud. I'm louder. I just haven't started." · BIOME_OCEAN · en-US.txt:1231 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### BIOME_UNDERGROUND (8)
- "It's dark down here and I'm told that's normal. I'm told many things." · BIOME_UNDERGROUND · en-US.txt:424 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "A cave full of things that live in caves. I'm not a cave person." · BIOME_UNDERGROUND · en-US.txt:425 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Somebody's been digging down here. There's a pickaxe. It isn't ours." · BIOME_UNDERGROUND · en-US.txt:426 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Tunnels should be wider. Rigroth the Undying agrees." · BIOME_UNDERGROUND · en-US.txt:427 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] It's dark down here and the moon's up there. I still know it's out." · BIOME_UNDERGROUND · en-US.txt:1232 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything in a cave is angry tonight. I'm in good company." · BIOME_UNDERGROUND · en-US.txt:1233 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Every drip is personal. Every echo's my enemy." · BIOME_UNDERGROUND · en-US.txt:1234 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'd have a bad night anywhere. Down here, at least nobody sees me." · BIOME_UNDERGROUND · en-US.txt:1235 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### PAYDAY (15)
- "Payday! My favorite day. Tied with didn't-die day." · PAYDAY · en-US.txt:430 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Coin in hand! Now I'll fight for you and like you." · PAYDAY · en-US.txt:431 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Paid in full. I'm going to stare at it until it feels real." · PAYDAY · en-US.txt:432 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "{Wage} a day. It's not much. It's also not nothing. I'll stay." · PAYDAY · en-US.txt:433 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "Paid on time. Is this a trap? It feels like a trap." · PAYDAY · en-US.txt:434 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "I got paid! Real coins! Can I show somebody?" · PAYDAY|Greenhorn · en-US.txt:435 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Right on time. {Player} is my favorite employer, and I keep a list." · PAYDAY|Sellsword · en-US.txt:436 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "Wages! The boss provides, and the boss is generous!" · PAYDAY|Zealot · en-US.txt:437 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Paid. Don't get used to it." · PAYDAY|Cynic · en-US.txt:438 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Pay's pay. Spend it before the moon does." · PAYDAY|Veteran · en-US.txt:439 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Coins are meant to be cold. That's the point. Ixxethra the Star-Eater agrees." · PAYDAY · en-US.txt:440 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] Paid. Good. Now leave me alone." · PAYDAY · en-US.txt:1237 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] {Wage} a day. It's not a thank-you. It's wages. Don't make a speech." · PAYDAY · en-US.txt:1238 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Payday, and the moon's red. The universe is giving with one hand and spitting with the other." · PAYDAY · en-US.txt:1239 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Fine. I'll take the coin. Don't expect a smile." · PAYDAY · en-US.txt:1240 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### UNPAID (15)
- "No coin, no courage. I didn't make the rules. Well, I made that one." · UNPAID · en-US.txt:442 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "Two days unpaid. Three and I start a union. We've got banners." · UNPAID · en-US.txt:443 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "No coin today. Funny how my sword suddenly feels heavier." · UNPAID · en-US.txt:444 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]
- "I'm not saying I'll leave. I'm saying my bag is packed and in plain view." · UNPAID · en-US.txt:445 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: "not X, I'm Y" swap
- "I can't fight on an empty stomach. Or an empty pocket. Both are empty." · UNPAID · en-US.txt:446 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]; style: setup + short punchline
- "Um. I think the coin didn't come? I could be wrong. I'm not wrong." · UNPAID|Greenhorn · en-US.txt:447 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Pay me or I start charging for standing here." · UNPAID|Sellsword · en-US.txt:448 · AI-WRITTEN · agent dialogue pass rewriting "sub-B" lines [d29b0f5]
- "I serve without pay. For now. For about a day." · UNPAID|Zealot · en-US.txt:449 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Unpaid again. Is anyone surprised? No? Good." · UNPAID|Cynic · en-US.txt:450 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Been unpaid before. It never gets quieter." · UNPAID|Veteran · en-US.txt:451 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Pay is owed on the first of the month. Vorrath of the Thousand Teeth agrees." · UNPAID|Zealot · en-US.txt:452 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "[grumpy] No coin and a Blood Moon. Don't look for me. I'm somewhere being angry." · UNPAID · en-US.txt:1241 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] You're late with the pay and I'm already furious. Be careful." · UNPAID · en-US.txt:1242 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Unpaid. Under that sky. Do the math, {Player}." · UNPAID · en-US.txt:1243 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I will take the money. I will take it late. And I'll hold a grudge." · UNPAID · en-US.txt:1244 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### DESERTED (4)
- "{Name} has deserted, taking a Minishark and no regrets." · DESERTED · en-US.txt:454 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "{Name} walked out of the barracks and kept walking. Please send a card." · DESERTED · en-US.txt:455 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{Name} has deserted, citing creative differences and unpaid wages." · DESERTED · en-US.txt:456 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "{Name} has deserted. The gear went along. The goodbye did not." · DESERTED · en-US.txt:457 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline

### NO_ONE_LEFT (3)
- "{Town} has run out of people. Everyone remaining is a civilian with a strong opinion." · NO_ONE_LEFT · en-US.txt:462 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "There is no one left to call up in {Town}. The horn is very sorry." · NO_ONE_LEFT · en-US.txt:463 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made); style: setup + short punchline
- "{Town} is out of militia. The wall is now more of a suggestion." · NO_ONE_LEFT · en-US.txt:464 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)

### EVENT_GOBLINS (9)
- "Goblins! Short, angry, well organized! Like {Demolitionist}! No offense, {Demolitionist}!" · EVENT_GOBLINS · en-US.txt:467 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Goblin Army! They're small, loud, and they want our stuff! I get that! I want our stuff too!" · EVENT_GOBLINS · en-US.txt:468 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Watch the sorcerers! They throw balls of shadow! Don't catch them! Don't catch anything!" · EVENT_GOBLINS · en-US.txt:469 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Peons in front! Archers behind! Shamans doing a thing! Everybody take a goblin!" · EVENT_GOBLINS · en-US.txt:470 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Somebody check if {GoblinTinkerer} is on our side! Somebody check nicely!" · EVENT_GOBLINS · en-US.txt:471 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Goblins and a Blood Moon! Perfect! Wonderful!" · EVENT_GOBLINS · en-US.txt:1259 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I hate that they're organized. I hate that I'm tired." · EVENT_GOBLINS · en-US.txt:1260 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Small, loud and angry. I'm the same! I'm bigger!" · EVENT_GOBLINS · en-US.txt:1261 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't ask me to be polite to goblins. I'm not polite to anyone tonight." · EVENT_GOBLINS · en-US.txt:1262 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### EVENT_PIRATES (9)
- "Pirates! Guard the chests! Guard the chests before you guard me!" · EVENT_PIRATES · en-US.txt:473 · PROBABLY AI · first snapshot of the file (author not recorded; style reads agent-made)
- "Pirates say arr like it's a legal term! Arr! Objection! I mean arr!" · EVENT_PIRATES · en-US.txt:474 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The cannon is the problem! The pirates are just the delivery!" · EVENT_PIRATES · en-US.txt:475 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Parrots! They brought parrots! {Pirate}, are these yours?!" · EVENT_PIRATES · en-US.txt:476 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Somebody tell them we're closed! Tell them with a gun!" · EVENT_PIRATES · en-US.txt:477 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Pirates on a Blood Moon! Of course!" · EVENT_PIRATES · en-US.txt:1263 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Say arr one more time! One more!" · EVENT_PIRATES · en-US.txt:1264 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I hope the parrot stays away from me. I hope." · EVENT_PIRATES · en-US.txt:1265 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everybody guard the chests, and stay away from me!" · EVENT_PIRATES · en-US.txt:1266 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### EVENT_ECLIPSE (9)
- "Why is the sun going out?! Is the sun on payroll? Did we miss its payday?!" · EVENT_ECLIPSE · en-US.txt:479 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Solar eclipse! Frankenstein, a guy with a chainsaw, and something called a Mothron!" · EVENT_ECLIPSE · en-US.txt:480 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "The sun's gone dark and the monsters are out in the daytime! That's new and I hate it!" · EVENT_ECLIPSE · en-US.txt:481 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Everybody inside! Everybody outside! Somebody decide!" · EVENT_ECLIPSE · en-US.txt:482 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is that a swamp thing?! It's daytime! It's not allowed in the daytime!" · EVENT_ECLIPSE · en-US.txt:483 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The sun's out and it's dark and it's all wrong! I'm fuming!" · EVENT_ECLIPSE · en-US.txt:1267 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Monsters in daylight, a mood in the dark, and nobody's asking me!" · EVENT_ECLIPSE · en-US.txt:1268 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Whoever's in charge of the sky, I'd like a word! A loud word!" · EVENT_ECLIPSE · en-US.txt:1269 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't speak to me! Day, night, eclipse, I don't care!" · EVENT_ECLIPSE · en-US.txt:1270 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### ECLIPSE_END (10)
- "It's over! The sun made it! I knew it would! I didn't know it would!" · ECLIPSE_END · en-US.txt:484 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "No more chainsaw guy! No more Mothron! Just regular dark, {Player}! I love regular dark!" · ECLIPSE_END · en-US.txt:485 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "Last time the sun went out like that I was in Peru! It came back then too!" · ECLIPSE_END · en-US.txt:486 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "{Glutton} took every kill in that eclipse! Every single one! Greedy!" · ECLIPSE_END · en-US.txt:487 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "Is it over? Is the sun okay? Somebody check on the sun!" · ECLIPSE_END|Greenhorn · en-US.txt:488 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "The sun's off its break! Somebody paid it! Was it you, {Player}?!" · ECLIPSE_END|Sellsword · en-US.txt:489 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "I told everyone the sun would come back! Put that in my review, {Player}!" · ECLIPSE_END|Zealot · en-US.txt:490 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "Eclipse is done. Great. Whatever's next will be worse." · ECLIPSE_END|Cynic · en-US.txt:491 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "[grumpy] Eclipse over, straight into a Blood Moon! Who is booking these?!" · ECLIPSE_END · en-US.txt:1271 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline
- "[grumpy] Finally, the sun's done being weird! Now the moon's doing it! Unbelievable!" · ECLIPSE_END · en-US.txt:1272 · AI-WRITTEN · agent eclipse-end lines [a25207b]; style: setup + short punchline

### EVENT_PUMPKIN (9)
- "The pumpkins have faces! The pumpkins have TEETH!" · EVENT_PUMPKIN · en-US.txt:493 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Pumpkin Moon! Every year the same pumpkins, and every year they're angrier!" · EVENT_PUMPKIN · en-US.txt:494 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Pumpkin heads, scarecrows, and one very persistent tree! This is why I skip Halloween!" · EVENT_PUMPKIN · en-US.txt:495 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hold the line until morning! There's no morning! Hold it anyway!" · EVENT_PUMPKIN · en-US.txt:496 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Don't look at the tree! Don't look at the tree! Who's looking at the tree?!" · EVENT_PUMPKIN · en-US.txt:497 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Pumpkins! Of course! A night for pumpkins and bad moods!" · EVENT_PUMPKIN · en-US.txt:1273 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I've never hated a vegetable so much!" · EVENT_PUMPKIN · en-US.txt:1274 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] If a pumpkin looks at me funny, I'm going to be funnier!" · EVENT_PUMPKIN · en-US.txt:1275 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] The tree! Don't point at the tree! I don't want to know!" · EVENT_PUMPKIN · en-US.txt:1276 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### EVENT_FROST (9)
- "Who gave Santa a tank?! WHO?!" · EVENT_FROST · en-US.txt:499 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Frost Moon! Candy canes with legs, presents with fangs! Happy holidays!" · EVENT_FROST · en-US.txt:500 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Elf gunners in the snow! Nobody asked for gifts like these!" · EVENT_FROST · en-US.txt:501 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "That reindeer is NOT friendly! That reindeer is NOT FRIENDLY!" · EVENT_FROST · en-US.txt:502 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is that a nutcracker?! It's cracking something! It's cracking US!" · EVENT_FROST · en-US.txt:503 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Santa with a tank. I give up. I just give up!" · EVENT_FROST · en-US.txt:1277 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Presents with fangs and I'm in a mood! They picked the wrong night!" · EVENT_FROST · en-US.txt:1278 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Elves with guns! Leave me out of the holiday spirit!" · EVENT_FROST · en-US.txt:1279 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Everything's festive and I'm furious! Let me shoot a candy cane!" · EVENT_FROST · en-US.txt:1280 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### EVENT_MARTIANS (9)
- "Little gray men with ray guns! I want hazard pay! I want it in advance!" · EVENT_MARTIANS · en-US.txt:505 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Martians landed during my shift! I'm not paid for aliens!" · EVENT_MARTIANS · en-US.txt:506 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "They have a saucer, lasers, and no manners!" · EVENT_MARTIANS · en-US.txt:507 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Don't look at the saucer! Look at the little ones! Don't look at the saucer!" · EVENT_MARTIANS · en-US.txt:508 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is that a walking brain?! In a suit?! With a gun?!" · EVENT_MARTIANS · en-US.txt:509 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Aliens. Tonight. I'm not even surprised. I'm tired!" · EVENT_MARTIANS · en-US.txt:1281 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If a saucer's going to land, land away from me!" · EVENT_MARTIANS · en-US.txt:1282 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] They've got lasers! I've got a mood! It's going to be a close fight!" · EVENT_MARTIANS · en-US.txt:1283 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Everything's from space and everything's rude!" · EVENT_MARTIANS · en-US.txt:1284 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### EVENT_OLDONES (9)
- "The Old One's Army! Protect the crystal! Do not let anything touch the crystal!" · EVENT_OLDONES · en-US.txt:511 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Ogres, goblins, and a dragon on the way! Nobody paid me for dragons!" · EVENT_OLDONES · en-US.txt:512 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Everything's walking toward the crystal! I'm going to stand in the way and look supportive!" · EVENT_OLDONES · en-US.txt:513 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Who's got the crystal?! I've got the crystal! Somebody else take the crystal!" · EVENT_OLDONES · en-US.txt:514 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's an ogre! It's a flying thing! It's an ogre with a flying thing!" · EVENT_OLDONES · en-US.txt:515 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Ogres, goblins, a dragon! I'll hit all three! I'm not negotiating!" · EVENT_OLDONES · en-US.txt:1285 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Protect the crystal! Fine! The crystal can stop looking at me!" · EVENT_OLDONES · en-US.txt:1286 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Don't tell me where to stand! I know where to stand!" · EVENT_OLDONES · en-US.txt:1287 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything's walking toward the crystal! I'm walking toward everything!" · EVENT_OLDONES · en-US.txt:1288 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### EVENT_FROSTLEGION (9)
- "Frost Legion! Snowmen with guns! I have so many questions and no time!" · EVENT_FROSTLEGION · en-US.txt:517 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Snowman gangsters! They look jolly! They carry tommy guns! Jolly, armed and mean!" · EVENT_FROSTLEGION · en-US.txt:518 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "A snowman named Mister Stabby! I've never been so afraid of a carrot!" · EVENT_FROSTLEGION · en-US.txt:519 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Somebody warm up! No, somebody warm THEM up! Hot things! Bring hot things!" · EVENT_FROSTLEGION · en-US.txt:520 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Go for the hats! The hats! That's where they keep the courage!" · EVENT_FROSTLEGION · en-US.txt:521 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Snowmen with guns! I'll melt every one! I'm warm enough!" · EVENT_FROSTLEGION · en-US.txt:1289 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] A snowman gangster with a tommy gun! Just once I'd like a normal night!" · EVENT_FROSTLEGION · en-US.txt:1290 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Don't ask me to build a snowman! I'm shooting this one!" · EVENT_FROSTLEGION · en-US.txt:1291 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Hats! They've got hats! I'm too cross to laugh!" · EVENT_FROSTLEGION · en-US.txt:1292 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### ENGAGE (3)
- "Stay behind {Tank}! {TankHe} died {TankDeaths} times and kept fighting!" · ENGAGE · en-US.txt:537 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Enemy incoming! Let's get paid!" · ENGAGE|Sellsword · en-US.txt:546 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm getting paid for this! Fire!" · ENGAGE|Sellsword · en-US.txt:547 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### KILL (2)
- "Another kill! Another coin!" · KILL|Sellsword · en-US.txt:577 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm getting paid for this!" · KILL|Sellsword · en-US.txt:579 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### HURT (4)
- "That's costing extra!" · HURT|Sellsword · en-US.txt:605 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Hit! Hazard pay!" · HURT|Sellsword · en-US.txt:606 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ow! I want a raise!" · HURT|Sellsword · en-US.txt:607 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ow! Called it!" · HURT|Cynic · en-US.txt:613 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### LOW_HP (2)
- "I'm not paid enough for this! Medic!" · LOW_HP|Sellsword · en-US.txt:630 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Knew it! Medic!" · LOW_HP|Cynic · en-US.txt:632 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OUT_OF_AMMO (3)
- "Out of ammo! Put it on the bill!" · OUT_OF_AMMO|Sellsword · en-US.txt:644 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Reloading! Pay me later!" · OUT_OF_AMMO|Sellsword · en-US.txt:645 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Out of ammo! Of course!" · OUT_OF_AMMO|Cynic · en-US.txt:647 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_SIGHTED (1)
- "Big target, big payday! Open fire!" · BOSS_SIGHTED|Sellsword · en-US.txt:679 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### ROUTED (1)
- "I'm not paid for this! Retreat!" · ROUTED|Sellsword · en-US.txt:825 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### RALLIED (1)
- "Back to work! Pay's still good!" · RALLIED|Sellsword · en-US.txt:840 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### VICTORY (1)
- "Job done! Time to get paid!" · VICTORY|Sellsword · en-US.txt:853 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### PROMOTED (9)
- "Promoted? Me? I'd like it noted that I'm still terrified!" · PROMOTED · en-US.txt:858 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "I'd like it noted" bit
- "A promotion! More pay, more danger! I only read the first half!" · PROMOTED · en-US.txt:859 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Thank you for the promotion! I'll try not to ruin it!" · PROMOTED · en-US.txt:860 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "A raise? A real raise? I'll pretend that's not why I'm smiling!" · PROMOTED · en-US.txt:861 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Wait, I'm a bigger deal now?! Somebody tell my mother! Don't tell my mother!" · PROMOTED · en-US.txt:862 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Promoted. Good. Don't clap. Don't talk." · PROMOTED · en-US.txt:1436 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] A promotion? Tonight? You pick tonight? Thank you. Go away." · PROMOTED · en-US.txt:1437 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] More pay, more danger, same bad mood. Fine." · PROMOTED · en-US.txt:1438 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] A raise! And I'm too cross to enjoy it! How rude of you!" · PROMOTED · en-US.txt:1439 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### VETERAN (9)
- "Veteran now! Which means I'm old! Or lucky! Or both!" · VETERAN · en-US.txt:864 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "They call me Sergeant now! I'm going to let it go to my head!" · VETERAN · en-US.txt:865 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I've survived enough to be called a veteran! I'd like a plaque!" · VETERAN · en-US.txt:866 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Veteran, huh! Guess surviving counts for something! Not much, but something!" · VETERAN · en-US.txt:867 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Sergeant! Does Sergeant get a better chair?! Somebody get me a better chair!" · VETERAN · en-US.txt:868 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Veteran. Wonderful. Now I'm old AND annoyed." · VETERAN · en-US.txt:1440 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Sergeant. Fine. Don't salute. I'm not in the mood." · VETERAN · en-US.txt:1441 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] They call me Sergeant now. Do not call me Sergeant. Not tonight." · VETERAN · en-US.txt:1442 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Veteran. Good. I've survived enough nights like this to be mean about it." · VETERAN · en-US.txt:1443 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### DEATH_MESSAGE (33)
- "{Name} was honorably discharged by {Killer}." · DEATH_MESSAGE · en-US.txt:885 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her
- "{Name} died for {Wage} a day." · DEATH_MESSAGE · en-US.txt:886 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her
- "{Name} was told the {Killer} wouldn't be there." · DEATH_MESSAGE · en-US.txt:887 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her
- "{Name} has gone to the great barracks in the sky." · DEATH_MESSAGE · en-US.txt:888 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her
- "{Name} finally got that day off." · DEATH_MESSAGE · en-US.txt:889 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her
- "{Name} blamed {Player}'s wall." · DEATH_MESSAGE · en-US.txt:890 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her
- "{Name} fell in the line of duty. The line was not very long." · DEATH_MESSAGE · en-US.txt:891 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her; style: setup + short punchline
- "{Name} was last seen holding the wall. The wall is fine." · DEATH_MESSAGE · en-US.txt:892 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her; style: setup + short punchline
- "{Name} gave everything. Including the armor." · DEATH_MESSAGE · en-US.txt:893 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her; style: setup + short punchline
- "{Name} went out in a blaze of glory. Mostly the blaze." · DEATH_MESSAGE · en-US.txt:894 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her; style: setup + short punchline
- "{Name} has fallen. They would have wanted us to carry on, loudly." · DEATH_MESSAGE · en-US.txt:895 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her
- "{Name} has fallen. {Killer} did it, for the record." · DEATH_MESSAGE · en-US.txt:896 · PROBABLY AI · first snapshot, author not recorded; "silly death messages" are a thing Eve likes, so check with her; style: setup + short punchline
- "{Name} now has sick leave" · DEATH_MESSAGE · en-US.txt:897 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} was about to ask for a paid vacation" · DEATH_MESSAGE · en-US.txt:898 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} was thinking about their paycheck" · DEATH_MESSAGE · en-US.txt:899 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} didn't realize that running into an enemy kills you" · DEATH_MESSAGE · en-US.txt:900 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} was let go, effective immediately" · DEATH_MESSAGE · en-US.txt:901 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} filed their last two weeks' notice. It was not honored" · DEATH_MESSAGE · en-US.txt:902 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917; style: setup + short punchline
- "{Name} has been laid off by {Killer}" · DEATH_MESSAGE · en-US.txt:903 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} never got to use their dental plan" · DEATH_MESSAGE · en-US.txt:904 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} was in the middle of a performance review" · DEATH_MESSAGE · en-US.txt:905 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} clocked out early" · DEATH_MESSAGE · en-US.txt:906 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} is no longer a stakeholder" · DEATH_MESSAGE · en-US.txt:907 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} has been reassigned to the afterlife department" · DEATH_MESSAGE · en-US.txt:908 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} was one shift away from overtime pay" · DEATH_MESSAGE · en-US.txt:909 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} would like to speak to HR about this" · DEATH_MESSAGE · en-US.txt:910 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} forgot to read the fine print on the contract" · DEATH_MESSAGE · en-US.txt:911 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} was not covered for this" · DEATH_MESSAGE · en-US.txt:912 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} missed the mandatory safety briefing" · DEATH_MESSAGE · en-US.txt:913 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} has taken an unscheduled break" · DEATH_MESSAGE · en-US.txt:914 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} was saving up for something nice" · DEATH_MESSAGE · en-US.txt:915 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} won't be making it to the staff party" · DEATH_MESSAGE · en-US.txt:916 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917
- "{Name} wasn't paid enough for this" · DEATH_MESSAGE · en-US.txt:917 · AI-WRITTEN · ROADMAP (agent A): "21 new wage/HR-gallows death messages" - exactly lines 897-917

### FIRESIDE (16)
- "{Comrade}, yours is on fire. No, the marshmallow. Well. Both." · FIRESIDE · en-US.txt:920 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]; style: setup + short punchline
- "{Comrade}, you hold it too close every time and then you cry about it." · FIRESIDE · en-US.txt:921 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "Pass the stick, {Comrade}. You had your turn. Your turn was most of the afternoon." · FIRESIDE · en-US.txt:922 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "It's the only warm thing I've held all week and it's a marshmallow." · FIRESIDE · en-US.txt:923 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "{Comrade}, tell me the one about the cave again. The one where you were brave." · FIRESIDE · en-US.txt:924 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]; style: setup + short punchline
- "I wasn't scared of the dark until {Comrade} started telling stories about it." · FIRESIDE · en-US.txt:925 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "Sit closer, {Comrade}! The fire doesn't bite! The thing past the fire bites!" · FIRESIDE · en-US.txt:926 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]; style: setup + short punchline
- "Nobody shoot anything tonight. I just got my shoes off." · FIRESIDE · en-US.txt:927 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]; style: setup + short punchline
- "{Player} said we could. {Player} said it with a face, but {Player} said it." · FIRESIDE · en-US.txt:928 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "Somebody pour me one, {Comrade}, and I'll pretend I didn't see you pour yourself three." · FIRESIDE · en-US.txt:929 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "My chip hums when it's warm. {Comrade}, does yours do that?" · FIRESIDE · en-US.txt:930 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Nobody should eat alone by a fire. Mother Gnash agrees." · FIRESIDE · en-US.txt:931 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: gods-agree formula
- "[grumpy] Sit by the fire? Fine. Don't speak." · FIRESIDE · en-US.txt:1446 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Pass the stick. And don't say a word about the marshmallow." · FIRESIDE · en-US.txt:1447 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] It's the only warm thing I've got tonight, so don't ruin it." · FIRESIDE · en-US.txt:1448 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm sitting here. I'm glaring at the fire. We understand each other." · FIRESIDE · en-US.txt:1449 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### PASTIME (13)
- "{Comrade}, it's up! It's actually up! Look, look, don't look, I'll lose it!" · PASTIME · en-US.txt:933 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "I made this one myself. {Comrade} laughed. {Comrade} stopped laughing when it flew." · PASTIME · en-US.txt:934 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]; style: setup + short punchline
- "Hold the string, {Comrade}. No, gently. It's not a gun, it's a feeling." · PASTIME · en-US.txt:935 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "If it gets away, nobody tell {Player}. If it comes back, tell {Player} everything." · PASTIME · en-US.txt:936 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "{Comrade}, I'm not slacking. This is wind reconnaissance." · PASTIME · en-US.txt:937 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]; style: setup + short punchline
- "Is it supposed to do that? {Comrade}, is the kite supposed to do that?" · PASTIME · en-US.txt:938 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]
- "{Comrade}, I haven't been this happy since the day before the sky opened." · PASTIME · en-US.txt:939 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]
- "{Comrade}! Are we the sort of people who dance? We are now!" · PASTIME · en-US.txt:940 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]; style: setup + short punchline
- "Hey {Comrade}, hold my gun. I've got a kite and I'm using both hands." · PASTIME · en-US.txt:941 · AI-WRITTEN · agent-written Free Rein pastime lines [03fa3e6]
- "[grumpy] The kite's fine. It's the wind that's got on my nerves." · PASTIME · en-US.txt:1450 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] It's not a hobby. It's a way to not shoot someone." · PASTIME · en-US.txt:1451 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Don't talk to me while I'm doing this. I'm trying to relax out of spite." · PASTIME · en-US.txt:1452 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] If this kite crashes, I'm blaming the moon." · PASTIME · en-US.txt:1453 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### PASTIME_NIGHT (8)
- "Count them with me, {Comrade}. There's more of them tonight than last night. Probably." · PASTIME_NIGHT · en-US.txt:943 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]; style: setup + short punchline
- "I found the one that looks like a hat, {Comrade}. Find your own." · PASTIME_NIGHT · en-US.txt:944 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]; style: setup + short punchline
- "{Comrade}, the bright one. No, the other bright one. Left. Your other left." · PASTIME_NIGHT · en-US.txt:947 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]; style: setup + short punchline
- "Don't tell {Player}, but I think I've been standing here for an hour." · PASTIME_NIGHT · en-US.txt:948 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]
- "[grumpy] Stars? What stars? There's just a big, red, rude moon." · PASTIME_NIGHT · en-US.txt:1454 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I came out here to look at the sky. The sky's looking back. I don't like it." · PASTIME_NIGHT · en-US.txt:1455 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm stargazing. Aggressively. Don't sit next to me." · PASTIME_NIGHT · en-US.txt:1456 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] It's the only quiet I get. Leave me to it." · PASTIME_NIGHT · en-US.txt:1457 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### TAKEOFF (11)
- "Do we have to? I just got comfortable down here." · TAKEOFF · en-US.txt:952 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Is there a path? There's usually a path. I like paths." · TAKEOFF · en-US.txt:953 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "I'll go, but I'm not going to enjoy it." · TAKEOFF · en-US.txt:954 · AI-WRITTEN · agent-written flight talk [a893e6d]
- "Nobody look at me. I do this badly." · TAKEOFF · en-US.txt:955 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Fine. Up. But I'm writing to my representative." · TAKEOFF · en-US.txt:956 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Tell my boots I'll be back." · TAKEOFF · en-US.txt:957 · AI-WRITTEN · agent-written flight talk [a893e6d]
- "Pro-Ground means pro-ground! I'm going under protest!" · TAKEOFF · en-US.txt:958 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: Pro-Ground running gag
- "[grumpy] Up? Tonight? Fine. Up. I'm bringing my mood." · TAKEOFF · en-US.txt:1458 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm flying. I'm doing it angry. I'm sure it'll be fine." · TAKEOFF · en-US.txt:1459 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everyone look away. I'm going to take off and be rude to the air." · TAKEOFF · en-US.txt:1460 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] If I fall, I'm blaming the moon." · TAKEOFF · en-US.txt:1461 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### FLIGHT_FEAR (10)
- "Too high! Too high! Who made it this high?!" · FLIGHT_FEAR · en-US.txt:959 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "I can see the top of the trees! Trees shouldn't have tops!" · FLIGHT_FEAR · en-US.txt:960 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Where's the floor? The floor was right here a second ago!" · FLIGHT_FEAR · en-US.txt:961 · AI-WRITTEN · agent-written flight talk [a893e6d]
- "The ground is so far away! I miss it so much!" · FLIGHT_FEAR · en-US.txt:962 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Don't flap so hard! You're making it worse!" · FLIGHT_FEAR · en-US.txt:963 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Everything down there looks like a map! I hate maps!" · FLIGHT_FEAR · en-US.txt:964 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "[grumpy] Too high! Too high! And it's red up here!" · FLIGHT_FEAR · en-US.txt:1462 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm so cross I'm not even scared! I'm scared! Grr!" · FLIGHT_FEAR · en-US.txt:1463 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't talk to me in the air! I'll fall out of spite!" · FLIGHT_FEAR · en-US.txt:1464 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Who made it this high?! I want a word! A loud one!" · FLIGHT_FEAR · en-US.txt:1465 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### LANDING (10)
- "Ground! Sweet, sweet ground! I'll never leave you again!" · LANDING · en-US.txt:965 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Solid. Flat. Mine. Never again." · LANDING · en-US.txt:966 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "I'm kissing this dirt. Nobody make it weird." · LANDING · en-US.txt:967 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "The ground never let me down. I let it down. Sorry, ground." · LANDING · en-US.txt:968 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "I'm fine. I'm absolutely fine. Give me a minute." · LANDING · en-US.txt:969 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: setup + short punchline
- "Pro-Ground! Pro-Ground! Pro-Ground!" · LANDING · en-US.txt:970 · AI-WRITTEN · agent-written flight talk [a893e6d]; style: Pro-Ground running gag
- "[grumpy] Ground! Finally! Stay down here! Don't talk to me!" · LANDING · en-US.txt:1466 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm kissing the dirt! In a bad mood! The dirt understands!" · LANDING · en-US.txt:1467 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Solid, flat, and mine. Leave me alone on it." · LANDING · en-US.txt:1468 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] That was the worst flight of my life, and I've flown angry before!" · LANDING · en-US.txt:1469 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### DRINK (25)
- "To {Player}! Best commander I've had in this particular life!" · DRINK · en-US.txt:977 · AI-WRITTEN · agent-written drink lines [77008fa]
- "To the commander! And to whoever paid for this, who is also the commander!" · DRINK · en-US.txt:978 · AI-WRITTEN · agent-written drink lines [77008fa]
- "{Comrade}, that's your third one! Sit down! No, keep going!" · DRINK · en-US.txt:979 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "I can feel my implant getting fuzzy! Best thing that ever happened to my brain!" · DRINK · en-US.txt:980 · AI-WRITTEN · agent-written drink lines [77008fa]
- "Nobody tell the officer! {Comrade}, you tell the officer! No, don't tell the officer!" · DRINK · en-US.txt:981 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "{Player}, I love you, I love this wall, and I love {Comrade}, and I mean all of it!" · DRINK · en-US.txt:982 · AI-WRITTEN · agent-written drink lines [77008fa]
- "I'd say a few words but my words went somewhere! Cheers!" · DRINK · en-US.txt:983 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "Hold my gun, {Comrade}. No, give it back. Hold my other gun." · DRINK · en-US.txt:984 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "{Comrade} spilled some on me and I'm not even mad! I'm a little mad!" · DRINK · en-US.txt:985 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "I'm not drunk! {Comrade}, am I drunk? Don't lie to me!" · DRINK · en-US.txt:986 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "A toast! To the hole! To the dirt! To {Player}, who found the hole!" · DRINK · en-US.txt:987 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "Is this hazard pay? Because it feels like hazard pay!" · DRINK · en-US.txt:988 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "Everybody's taller. Did you grow, {Comrade}? Did I grow?" · DRINK · en-US.txt:989 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Whoever poured this, I'd follow you into the Dungeon. Twice." · DRINK · en-US.txt:990 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I've never had a round bought for me! I'm going to remember this forever!" · DRINK|Greenhorn · en-US.txt:991 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Free drinks. I'll note it as a signing bonus." · DRINK|Sellsword · en-US.txt:992 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "The boss pours! The boss provides! Cheers, boss!" · DRINK|Zealot · en-US.txt:993 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Free drinks. Something's coming. Cheers anyway!" · DRINK|Cynic · en-US.txt:994 · AI-WRITTEN · agent-written drink lines [77008fa]; style: setup + short punchline
- "Take it slow, greenhorns. The night's longer than the keg." · DRINK|Veteran · en-US.txt:995 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "[grumpy] To the commander! And to nothing else! Everything else is annoying!" · DRINK · en-US.txt:1471 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Hand me a drink and nobody talk! Cheers!" · DRINK · en-US.txt:1472 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm not drunk! I'm cross! Both can be true!" · DRINK · en-US.txt:1473 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't toast me. Don't toast to me. Just pour." · DRINK · en-US.txt:1474 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] {Comrade}, if you sing, I'm leaving! Cheers!" · DRINK · en-US.txt:1475 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Fine. It's good. I'll say nothing else. Cheers." · DRINK · en-US.txt:1476 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### LIFE_CRYSTAL (16)
- "A heart! A real one! What does it do for us, {Player}?" · LIFE_CRYSTAL · en-US.txt:1017 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "It's glowing! It's yours! I'm happy for you! I'm a little jealous!" · LIFE_CRYSTAL · en-US.txt:1018 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "{Player}'s getting sturdier! Does that mean we get sturdier? Does it?" · LIFE_CRYSTAL · en-US.txt:1019 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "You just ate a crystal! In front of everybody! I'm not saying anything!" · LIFE_CRYSTAL · en-US.txt:1020 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Wait, wait! Did you get taller?! Just a little?!" · LIFE_CRYSTAL · en-US.txt:1021 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "That looked like it hurt in a good way! Do it again!" · LIFE_CRYSTAL · en-US.txt:1022 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Where do you even find those?! Tell me where! I'll go! I won't go!" · LIFE_CRYSTAL · en-US.txt:1023 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is it true they make you taller? Please say it makes you taller!" · LIFE_CRYSTAL|Greenhorn · en-US.txt:1024 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hearts belong underground, where it's dark and they're easy to miss! Ul'Thazhra the Unblinking agrees!" · LIFE_CRYSTAL|Zealot · en-US.txt:1025 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: gods-agree formula
- "I'd dig one up myself for a day's wage! Two days' wage, with the lantern!" · LIFE_CRYSTAL|Sellsword · en-US.txt:1026 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Every one of those is more health than I'll ever have! Enjoy it, {Player}!" · LIFE_CRYSTAL|Veteran · en-US.txt:1027 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Another heart! Another thing that'll try to break it! Congratulations!" · LIFE_CRYSTAL|Cynic · en-US.txt:1028 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] A heart. Of course, now. Tonight. When I'm in a mood to be jealous." · LIFE_CRYSTAL · en-US.txt:1478 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] It's glowing. Good for you. Stop glowing at me." · LIFE_CRYSTAL · en-US.txt:1479 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Congratulations. I mean that in the rudest possible way." · LIFE_CRYSTAL · en-US.txt:1480 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Another heart! Is there one for me?! No?! Fine!" · LIFE_CRYSTAL · en-US.txt:1481 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### CHEST_OPENED (16)
- "A chest! Please be boots! Please be my size!" · CHEST_OPENED · en-US.txt:1030 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Open it! Open it! Open it! Wait, check for mimics! Open it!" · CHEST_OPENED · en-US.txt:1031 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "What's in it?! Is it a sword?! Tell me it's not a sword! I want a sword!" · CHEST_OPENED · en-US.txt:1032 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Don't take the good stuff! Take the okay stuff! Leave the good stuff for the army!" · CHEST_OPENED · en-US.txt:1033 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Anything for me?! I'm just asking! I'm not asking!" · CHEST_OPENED · en-US.txt:1034 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's gold! The lid's gold! Open the gold one first!" · CHEST_OPENED · en-US.txt:1035 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Somebody write down where that chest was! And what was in it!" · CHEST_OPENED · en-US.txt:1036 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is it a good one? It looks like a good one! Is it a good one?!" · CHEST_OPENED|Greenhorn · en-US.txt:1037 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Whatever's in there, I'd like it itemized!" · CHEST_OPENED|Sellsword · en-US.txt:1038 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "If it's a mimic, I'm standing here." · CHEST_OPENED|Cynic · en-US.txt:1039 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Check for mimics! Then check again!" · CHEST_OPENED|Veteran · en-US.txt:1040 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Chests should be opened from the left! Rigroth the Undying has never once been wrong about chests!" · CHEST_OPENED|Zealot · en-US.txt:1041 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] A chest? Tonight? Whatever's in it, I don't want it. Unless it's good." · CHEST_OPENED · en-US.txt:1482 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Open it and don't make a show. I'm cross enough as it is." · CHEST_OPENED · en-US.txt:1483 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If it's a mimic, I'm going to shoot the lid, the chest and you." · CHEST_OPENED · en-US.txt:1484 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Is it boots? It's never boots! It's never boots!" · CHEST_OPENED · en-US.txt:1485 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### ARMORY_GOT (15)
- "Real boots! They fit! I keep looking at them!" · ARMORY_GOT · en-US.txt:1044 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "New kit! New kit! Look at the new kit!" · ARMORY_GOT · en-US.txt:1045 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Somebody swapped my gear! It's better! Who swapped my gear?!" · ARMORY_GOT · en-US.txt:1046 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's lighter! Or I'm stronger! I'm saying it's lighter!" · ARMORY_GOT · en-US.txt:1047 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "This isn't mine! It's mine now! Thanks, armory!" · ARMORY_GOT · en-US.txt:1048 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm going to wear this to bed! Don't tell the Nurse!" · ARMORY_GOT · en-US.txt:1049 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It fits! It actually fits! Nobody told me it could fit!" · ARMORY_GOT|Greenhorn · en-US.txt:1050 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "New kit! I'll wear it on the clock, so we're square!" · ARMORY_GOT|Sellsword · en-US.txt:1051 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Good weight! Good balance! Somebody did their job!" · ARMORY_GOT|Veteran · en-US.txt:1052 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "A gift from the armory! I'll wear it with devotion and a little pride!" · ARMORY_GOT|Zealot · en-US.txt:1053 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "New gear. Sure. Something's coming and they want us pretty for it." · ARMORY_GOT|Cynic · en-US.txt:1054 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] New gear! Fine! It's good! Don't make me say thank you!" · ARMORY_GOT · en-US.txt:1486 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] It fits. I'm still cross. Both can be true." · ARMORY_GOT · en-US.txt:1487 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Oh, NOW they send the good stuff. Under THAT moon." · ARMORY_GOT · en-US.txt:1488 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't look at me. I'm wearing it. I'm cross in it. Hands off." · ARMORY_GOT · en-US.txt:1489 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### ARMORY_WANT (13)
- "I'd like a better helmet. I'm not asking. I'm pointing at the armory." · ARMORY_WANT · en-US.txt:1056 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Everyone else got something nice, {Player}. I'm still holding what I came in with." · ARMORY_WANT · en-US.txt:1057 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I'm not complaining. I'm describing my boots, and they're describing me." · ARMORY_WANT · en-US.txt:1058 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "not X, I'm Y" swap
- "Somebody got my gear. I'm not saying who. They know who." · ARMORY_WANT · en-US.txt:1059 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Everyone else got something good. I got a stick." · ARMORY_WANT|Cynic · en-US.txt:1060 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I've seen what the others are carrying. I'd like to renegotiate." · ARMORY_WANT|Sellsword · en-US.txt:1061 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is it normal that mine's the one with the patch? It's fine. I'm not asking." · ARMORY_WANT|Greenhorn · en-US.txt:1062 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm sure the armory has a plan for me. I'd like to hear it." · ARMORY_WANT|Zealot · en-US.txt:1063 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I've worn worse. I've also worn better, a long time ago. Just saying." · ARMORY_WANT|Veteran · en-US.txt:1064 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'd like better gear. I'd like it now. I'd like it before the moon sets." · ARMORY_WANT · en-US.txt:1490 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Everyone else got something nice. I'm too cross to ask." · ARMORY_WANT · en-US.txt:1491 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The armory forgot me. I haven't forgotten the armory." · ARMORY_WANT · en-US.txt:1492 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't ask about my boots. They know what they did." · ARMORY_WANT · en-US.txt:1493 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### ARMORY_BEST (14)
- "Yes, it's the good set. No, you can't try it on." · ARMORY_BEST · en-US.txt:1066 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "{Player}, the armory picked me out of everyone, and I didn't argue." · ARMORY_BEST · en-US.txt:1067 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "People keep asking where I got it. The armory! It's the armory! It's all armory!" · ARMORY_BEST · en-US.txt:1068 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm not showing off. The shine's just coming off me." · ARMORY_BEST · en-US.txt:1069 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm told I look expensive. I'm told that a lot." · ARMORY_BEST · en-US.txt:1070 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's not that I'm better. It's that my armor is better. Keep that between us." · ARMORY_BEST|Veteran · en-US.txt:1071 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "not X, I'm Y" swap
- "Best kit in the army, and I'm the cheapest on the payroll. Think about that." · ARMORY_BEST|Sellsword · en-US.txt:1072 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I got the good one! I don't know why! Please don't take it back!" · ARMORY_BEST|Greenhorn · en-US.txt:1073 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Best kit in the army. So I'll be shot first. Great." · ARMORY_BEST|Cynic · en-US.txt:1074 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The armory chose me! It must have heard my prayers! Or my loud sighing!" · ARMORY_BEST|Zealot · en-US.txt:1075 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Yes, it's the best set. No, I'm not in the mood to talk about it." · ARMORY_BEST · en-US.txt:1494 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Best gear in the army and the moon's still ruined my night. Think about that." · ARMORY_BEST · en-US.txt:1495 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't touch the shiny. I'll bite." · ARMORY_BEST · en-US.txt:1496 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I've got the best kit, and still the worst night. The kit helps." · ARMORY_BEST · en-US.txt:1497 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### ARMORY_WORST (13)
- "Don't mind my armor. It's fine. I'm used to it. {Player}, it's fine." · ARMORY_WORST · en-US.txt:1077 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm happy for the others. I'm very happy. Look how happy I am." · ARMORY_WORST · en-US.txt:1078 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Nobody said it's a punishment. I just keep getting the hand-me-down of the hand-me-down." · ARMORY_WORST · en-US.txt:1079 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "It's okay. Somebody has to be the one who gets shot first." · ARMORY_WORST · en-US.txt:1080 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I got the one with the dent! It's fine! I'll be the dent!" · ARMORY_WORST|Greenhorn · en-US.txt:1081 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Worst kit in the army, and I'm still on the clock. I'd like that noted." · ARMORY_WORST|Sellsword · en-US.txt:1082 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "I'd like it noted" bit
- "Of course I got the worst. I'd have been disappointed otherwise." · ARMORY_WORST|Cynic · en-US.txt:1083 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The humble shall wear the dented! It says so somewhere! Probably!" · ARMORY_WORST|Zealot · en-US.txt:1084 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I've had the worst kit before. It builds character. I've got plenty of character." · ARMORY_WORST|Veteran · en-US.txt:1085 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Worst kit in the army and a Blood Moon. I'd like a refund on the whole week." · ARMORY_WORST · en-US.txt:1498 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm not complaining. I'm making a very quiet, very loud noise." · ARMORY_WORST · en-US.txt:1499 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Don't mention my armor. I mean it. Not tonight." · ARMORY_WORST · en-US.txt:1500 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Somebody has to wear the worst. It's me. Tonight. Of all the nights." · ARMORY_WORST · en-US.txt:1501 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### DRINK_ALE (7)
- "I feel like a wall! A wall that likes songs!" · DRINK_ALE · en-US.txt:1088 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hit me! Go on, hit me! Actually, don't!" · DRINK_ALE · en-US.txt:1089 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's sturdier going down than coming up! Mostly down!" · DRINK_ALE · en-US.txt:1090 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm made of ale now! I'm armor! I'm very, very armor!" · DRINK_ALE · en-US.txt:1091 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "{Comrade}, punch me! Gently! I want to see if it works!" · DRINK_ALE · en-US.txt:1092 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Everything bounces off! Even this headache!" · DRINK_ALE · en-US.txt:1093 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Cheers to the keg! May the keg never run dry! Or I'll get serious!" · DRINK_ALE · en-US.txt:1094 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### DRINK_STOUT (7)
- "That's got a kick! Where's something I can hit?!" · DRINK_STOUT · en-US.txt:1095 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm going to punch the next slime into next week!" · DRINK_STOUT · en-US.txt:1096 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Black as night and twice as angry! I love it!" · DRINK_STOUT · en-US.txt:1097 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "My arms feel like hammers! My thoughts feel like pillows!" · DRINK_STOUT · en-US.txt:1098 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "{Comrade}, I could lift you! I'd rather not! But I could!" · DRINK_STOUT · en-US.txt:1099 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Somebody point me at a zombie! Anything! A friendly zombie!" · DRINK_STOUT · en-US.txt:1100 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The stout's talking to me! It says go! It says go hard!" · DRINK_STOUT · en-US.txt:1101 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### DRINK_RUM (7)
- "That's rum! Good rum! I can see everyone's weak spots!" · DRINK_RUM · en-US.txt:1102 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I feel lucky! Dangerous, but lucky!" · DRINK_RUM · en-US.txt:1103 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's got a sweet kick! A sweet, sneaky kick! Aim for the neck!" · DRINK_RUM · en-US.txt:1104 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Everything's a little sharper and a lot funnier!" · DRINK_RUM · en-US.txt:1105 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I'm seeing weak points on everyone! Even you, {Comrade}! Especially you!" · DRINK_RUM · en-US.txt:1106 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Rum makes me precise! {Comrade}, hold still, I'll show you!" · DRINK_RUM · en-US.txt:1107 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Who gave the rum to the sniper?! That's me! I'm the sniper!" · DRINK_RUM · en-US.txt:1108 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### DRINK_FIZZ (7)
- "I'm floating! Is everyone else floating?! {Comrade}, you're floating!" · DRINK_FIZZ · en-US.txt:1109 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "One more jump! I can do one more jump! I'm not touching the ground ever again!" · DRINK_FIZZ · en-US.txt:1110 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Everything's bubbly! My feet are bubbly! My thoughts are bubbly!" · DRINK_FIZZ · en-US.txt:1111 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I jumped and kept going! Look at me! Look, no ground!" · DRINK_FIZZ · en-US.txt:1112 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is this what the winged ones feel like?! I'm not mad! I'm a little mad!" · DRINK_FIZZ · en-US.txt:1113 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's fizzy! It tickles! I'm giggling, {Player}! I'm giggling in uniform!" · DRINK_FIZZ · en-US.txt:1114 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Careful, {Comrade}! Pro-Ground says feet on the floor! I'm on the floor! Mostly!" · DRINK_FIZZ · en-US.txt:1115 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: Pro-Ground running gag

### DRINK_ICEBERG (7)
- "I can't feel my feet! I can't feel the lava either! Let's go see the lava!" · DRINK_ICEBERG · en-US.txt:1116 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Cold! So cold! Is this what the Snow biome's like inside?!" · DRINK_ICEBERG · en-US.txt:1117 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "It's like swallowing a glacier! A tiny, angry glacier!" · DRINK_ICEBERG · en-US.txt:1118 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hot things don't scare me now! Hot things should be scared of ME!" · DRINK_ICEBERG · en-US.txt:1119 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Somebody put me near the fire! No! Put me IN the fire! I'm fine! I'm cold!" · DRINK_ICEBERG · en-US.txt:1120 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "{Comrade}, is this vodka?! It tastes like winter!" · DRINK_ICEBERG · en-US.txt:1121 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm an iceberg! I'm slow and enormous and nobody can find my bottom!" · DRINK_ICEBERG · en-US.txt:1122 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### HOLD_FIRE (4)
- "[grumpy] Holding fire! Fine!" · HOLD_FIRE · en-US.txt:1245 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Weapons down! Now what?" · HOLD_FIRE · en-US.txt:1246 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Cease fire! Make up your mind!" · HOLD_FIRE · en-US.txt:1247 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Standing down! For now!" · HOLD_FIRE · en-US.txt:1248 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### MUSTER (4)
- "[grumpy] On my way! Make it quick!" · MUSTER · en-US.txt:1249 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Falling in! Not tonight!" · MUSTER · en-US.txt:1250 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ready! Let's get this over with!" · MUSTER · en-US.txt:1251 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm coming! Fine!" · MUSTER · en-US.txt:1252 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### STAND_DOWN (4)
- "[grumpy] Standing down! Finally!" · STAND_DOWN · en-US.txt:1253 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Fall out! Get me out of here!" · STAND_DOWN · en-US.txt:1254 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Dismissed! About time!" · STAND_DOWN · en-US.txt:1255 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Done! Weapons down!" · STAND_DOWN · en-US.txt:1256 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_MARCH (19)
- "Eyes forward, everyone! The dirt isn't going to guard itself!" · OFFICER_MARCH · en-US.txt:1508 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Keep the line tight! Loose lines get people a bad review!" · OFFICER_MARCH · en-US.txt:1509 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Step lively! We move at the pace of the slowest complaint!" · OFFICER_MARCH · en-US.txt:1510 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I asked for a quiet life. Instead I got a hat and a commission!" · OFFICER_MARCH · en-US.txt:1511 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Officers lead from the front! I checked! It's in the fine print!" · OFFICER_MARCH · en-US.txt:1512 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hold formation! The enemy's overwhelming, not organized!" · OFFICER_MARCH · en-US.txt:1513 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Chin up! We've survived worse meetings than this!" · OFFICER_MARCH · en-US.txt:1514 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Follow me, and try to look like we meant to do this!" · OFFICER_MARCH · en-US.txt:1515 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Mind your footing! The ground's uneven and so's morale!" · OFFICER_MARCH · en-US.txt:1516 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "On me! If I fall, someone else inherits the hat!" · OFFICER_MARCH · en-US.txt:1517 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Nobody drilled us for a sky full of teeth! We improvise!" · OFFICER_MARCH · en-US.txt:1518 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Left, right, left! I learned that this morning! You're welcome!" · OFFICER_MARCH · en-US.txt:1519 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Stay together! Apart, we're just a longer menu for them!" · OFFICER_MARCH · en-US.txt:1520 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Keep walking and keep quiet. I mean it." · OFFICER_MARCH · en-US.txt:1521 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm not in the mood. March anyway." · OFFICER_MARCH · en-US.txt:1522 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Nobody talk to me until the moon's gone." · OFFICER_MARCH · en-US.txt:1523 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Chins up, spirits down, mouths shut." · OFFICER_MARCH · en-US.txt:1524 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] The sky's the wrong color. Don't make it worse." · OFFICER_MARCH · en-US.txt:1525 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] One more word and I'm counting it against you." · OFFICER_MARCH · en-US.txt:1526 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### NURSE_MARCH (18)
- "Stay close! I can't patch what I can't reach!" · NURSE_MARCH · en-US.txt:1527 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Anyone bleeding, speak up! Modesty costs lives!" · NURSE_MARCH · en-US.txt:1528 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Keep breathing! That part's free!" · NURSE_MARCH · en-US.txt:1529 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I carry syringes and strong opinions about your posture!" · NURSE_MARCH · en-US.txt:1530 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Hold still when this one lands! It's medicine, I promise!" · NURSE_MARCH · en-US.txt:1531 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Walk it off isn't medical advice! Come here instead!" · NURSE_MARCH · en-US.txt:1532 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'll keep you standing! You keep the enemy busy!" · NURSE_MARCH · en-US.txt:1533 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "No heroics! A wounded hero is just extra paperwork!" · NURSE_MARCH · en-US.txt:1534 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Mind the gaps in the line! That's where I lose people!" · NURSE_MARCH · en-US.txt:1535 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "If it hurts, that's your body filing a complaint! I'll handle it!" · NURSE_MARCH · en-US.txt:1536 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Stay in reach! A syringe only flies so far!" · NURSE_MARCH · en-US.txt:1537 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Patch, march, repeat! The glamorous life they promised me!" · NURSE_MARCH · en-US.txt:1538 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm in a mood, and I carry syringes. Think about that." · NURSE_MARCH · en-US.txt:1539 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't ask how I'm doing. I'm doing badly. Walk." · NURSE_MARCH · en-US.txt:1540 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The moon's out, I'm furious, and nobody's even bleeding yet. Give it time." · NURSE_MARCH · en-US.txt:1541 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Stay close. I'm not saying I care. I'm saying I'm stuck with you." · NURSE_MARCH · en-US.txt:1542 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] If you're dying, tell me before I'm too annoyed to help. The window's short." · NURSE_MARCH · en-US.txt:1543 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Walk. And if I hear the word cheer, someone's getting a shot." · NURSE_MARCH · en-US.txt:1544 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_CHAT_MARCH (4)
- "Keep pace, {Player}! I'm told the enemy doesn't wait for stragglers!" · OFFICER_CHAT_MARCH · en-US.txt:1585 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "All quiet so far. I only mention it so it can be proven wrong." · OFFICER_CHAT_MARCH · en-US.txt:1586 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I'm marching, I'm listening, and I'm regretting my boots. Pick one for the conversation!" · OFFICER_CHAT_MARCH · en-US.txt:1587 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Lead on! I'll be the one behind you, nodding gravely!" · OFFICER_CHAT_MARCH · en-US.txt:1588 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_CHAT_HOLD (4)
- "I've been standing here a while. The post and I have come to an understanding." · OFFICER_CHAT_HOLD · en-US.txt:1589 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Nothing's come at us yet. I keep looking at the horizon to be polite." · OFFICER_CHAT_HOLD · en-US.txt:1590 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Orders were to hold. So I'm holding, with real dedication!" · OFFICER_CHAT_HOLD · en-US.txt:1591 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "This spot's mine now. Anything that wants it can ask nicely first!" · OFFICER_CHAT_HOLD · en-US.txt:1592 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_CHAT_IDLE (4)
- "At ease, {Player}. That's a polite way of saying I'm bored." · OFFICER_CHAT_IDLE · en-US.txt:1593 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Nothing to report. I wrote that down twice, just to feel busy." · OFFICER_CHAT_IDLE · en-US.txt:1594 · AI-WRITTEN · agent-written officer voices (391 lines) [bc17c21]
- "If you came for orders, I'm the wrong one. If you came for company, I'm slightly better." · OFFICER_CHAT_IDLE · en-US.txt:1595 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Quiet day. I'd call it peaceful, but that tends to tempt fate." · OFFICER_CHAT_IDLE · en-US.txt:1596 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Nurse_IDLE (10)
- "Sit. Not there, there. And quit staring at the needle. It knows." · OFFICER_Nurse_IDLE · en-US.txt:1603 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I've been on my feet for three days. Whoever's bleeding next, make it interesting." · OFFICER_Nurse_IDLE · en-US.txt:1604 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Nobody's hurt. Give it ten minutes. Somebody's going to try something brave." · OFFICER_Nurse_IDLE · en-US.txt:1605 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "You want a sticker? Those are for the brave ones. So, no." · OFFICER_Nurse_IDLE · en-US.txt:1606 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I bill by the stitch, the ache and the whining. Today's rate is mostly whining." · OFFICER_Nurse_IDLE · en-US.txt:1607 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Does it hurt to stand there? Good. Then stand there longer." · OFFICER_Nurse_IDLE · en-US.txt:1608 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I told {Demolitionist} to keep his hands on his arms. Hands on his arms. It's not complicated." · OFFICER_Nurse_IDLE · en-US.txt:1609 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't. Whatever it is, don't. Not under that moon." · OFFICER_Nurse_IDLE · en-US.txt:2082 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Oh, now you want a nurse. At night. Under THAT. Fantastic." · OFFICER_Nurse_IDLE · en-US.txt:2083 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I've got a headache, a yard full of zombies and no patience. Pick one to talk to." · OFFICER_Nurse_IDLE · en-US.txt:2084 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Nurse_MARCH (8)
- "Close ranks! I only run so fast, and I run with a stretcher!" · OFFICER_Nurse_MARCH · en-US.txt:1610 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Walk faster, {Player}! Wounded people don't carry themselves!" · OFFICER_Nurse_MARCH · en-US.txt:1611 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Everybody keep both legs! I've only got the one stretcher!" · OFFICER_Nurse_MARCH · en-US.txt:1612 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Left foot, right foot, minor bleeding. That's marching." · OFFICER_Nurse_MARCH · en-US.txt:1613 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "If something hurts, say so early. I get grumpy when it's late." · OFFICER_Nurse_MARCH · en-US.txt:1614 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Walk. Don't talk. Bleed quietly." · OFFICER_Nurse_MARCH · en-US.txt:2085 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm marching. I'm not happy. Those are separate." · OFFICER_Nurse_MARCH · en-US.txt:2086 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Tell me to smile and I'll show you a very large needle." · OFFICER_Nurse_MARCH · en-US.txt:2087 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Nurse_HOLD (6)
- "Standing around is just waiting with posture. Nobody's bleeding yet. I'm waiting." · OFFICER_Nurse_HOLD · en-US.txt:1615 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Fine. We hold. I'll count the ones most likely to faint." · OFFICER_Nurse_HOLD · en-US.txt:1616 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Quiet's the worst. Quiet is when they all get hurt at once." · OFFICER_Nurse_HOLD · en-US.txt:1617 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] We hold. I hold a grudge. Different thing." · OFFICER_Nurse_HOLD · en-US.txt:2088 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Leave me alone and I'll leave your arteries alone." · OFFICER_Nurse_HOLD · en-US.txt:2089 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'm standing here, and I'm furious. Don't test it." · OFFICER_Nurse_HOLD · en-US.txt:2090 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Nurse_VICTORY (1)
- "We won! Line up for bandages!" · OFFICER_Nurse_VICTORY · en-US.txt:1629 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Guide_IDLE (5)
- "Hi, {Player}! I wrote down some tips while we were standing around! Ten pages!" · OFFICER_Guide_IDLE · en-US.txt:1634 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Did you know a workbench makes everything easier? I tell everyone. Nobody asks." · OFFICER_Guide_IDLE · en-US.txt:1635 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "If you ever feel lost, you can ask me! I'm right here! I'm always right here!" · OFFICER_Guide_IDLE · en-US.txt:1636 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'd offer advice about the army, but I mostly know about torches." · OFFICER_Guide_IDLE · en-US.txt:1637 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Everyone's so brave! I mean it! I'm taking notes!" · OFFICER_Guide_IDLE · en-US.txt:1638 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Guide_MARCH (4)
- "Watch your step, everyone! Uneven ground! Uneven ground!" · OFFICER_Guide_MARCH · en-US.txt:1639 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Stay together! Tip: groups are safer than people on their own!" · OFFICER_Guide_MARCH · en-US.txt:1640 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Keep a torch handy! Nobody ever regrets a torch!" · OFFICER_Guide_MARCH · en-US.txt:1641 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Right foot, left foot! Great! You're all doing wonderfully!" · OFFICER_Guide_MARCH · en-US.txt:1642 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Guide_HOLD (3)
- "Holding here is a good idea! I read about it! Probably!" · OFFICER_Guide_HOLD · en-US.txt:1643 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Fun fact while we wait! There's dirt everywhere! Isn't that nice?" · OFFICER_Guide_HOLD · en-US.txt:1644 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's a good spot! Good sightlines! I think that's the word!" · OFFICER_Guide_HOLD · en-US.txt:1645 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Merchant_IDLE (5)
- "Slow day for sales. Everybody's busy not dying. Bad for business!" · OFFICER_Merchant_IDLE · en-US.txt:1662 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Psst, {Player}! I've got a deal on shields! It's not a deal! But they're shields!" · OFFICER_Merchant_IDLE · en-US.txt:1663 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "You know what an army needs? Inventory. Lucky for you, I have inventory!" · OFFICER_Merchant_IDLE · en-US.txt:1664 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Heh heh. Business is booming. By which I mean explosions. Customers!" · OFFICER_Merchant_IDLE · en-US.txt:1665 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "If it breaks, I've got a spare. If it's a spare, I've got a markup." · OFFICER_Merchant_IDLE · en-US.txt:1666 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Merchant_MARCH (4)
- "Keep moving! A moving target is a paying customer!" · OFFICER_Merchant_MARCH · en-US.txt:1667 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Mind the crates! Those are not free samples!" · OFFICER_Merchant_MARCH · en-US.txt:1668 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Heh! Marching works up an appetite! Rations sold separately!" · OFFICER_Merchant_MARCH · en-US.txt:1669 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm walking, I'm selling, I'm walking. It's called multitasking!" · OFFICER_Merchant_MARCH · en-US.txt:1670 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Merchant_HOLD (3)
- "We hold here? Good. The foot traffic's just right for a stall." · OFFICER_Merchant_HOLD · en-US.txt:1671 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Standing still is an investment. I expect returns." · OFFICER_Merchant_HOLD · en-US.txt:1672 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Quiet post. Perfect for a little inventory and a little profit." · OFFICER_Merchant_HOLD · en-US.txt:1673 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Merchant_FIGHT (1)
- "Sale on bullets! Fire!" · OFFICER_Merchant_FIGHT · en-US.txt:1676 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Merchant_BOSS (1)
- "Nobody's paying for {Boss}! Open fire!" · OFFICER_Merchant_BOSS · en-US.txt:1679 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Merchant_HURT (1)
- "Ow! Somebody's paying for that!" · OFFICER_Merchant_HURT · en-US.txt:1682 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Merchant_VICTORY (2)
- "Victory! Time to count the loot!" · OFFICER_Merchant_VICTORY · en-US.txt:1685 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That's a win! Good business!" · OFFICER_Merchant_VICTORY · en-US.txt:1686 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_ArmsDealer_IDLE (5)
- "Listen to that. Nothing. It's the sound of a barrel that needs firing." · OFFICER_ArmsDealer_IDLE · en-US.txt:1690 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Cleaned every piece I own. Twice. The second time was for fun." · OFFICER_ArmsDealer_IDLE · en-US.txt:1691 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "If it goes bang, I've named it. If it goes bang loud, I've named it twice." · OFFICER_ArmsDealer_IDLE · en-US.txt:1692 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "There's no problem more caliber can't at least help with, {Player}." · OFFICER_ArmsDealer_IDLE · en-US.txt:1693 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Ask me about muzzle velocity. Go on. I've been waiting since breakfast." · OFFICER_ArmsDealer_IDLE · en-US.txt:1694 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_ArmsDealer_MARCH (4)
- "I'm carrying enough lead to sink a small boat. Don't ask why I'd need a small boat sunk." · OFFICER_ArmsDealer_MARCH · en-US.txt:1695 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Pace yourself. Tired shooters shoot high. High is where friends stand." · OFFICER_ArmsDealer_MARCH · en-US.txt:1696 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Smell that? Gun oil and optimism. My two favorite smells." · OFFICER_ArmsDealer_MARCH · en-US.txt:1697 · AI-WRITTEN · agent-written officer voices (391 lines) [bc17c21]; style: setup + short punchline
- "Keep the barrels pointed away from me, {Player}. Away, and at something worthwhile." · OFFICER_ArmsDealer_MARCH · en-US.txt:1698 · AI-WRITTEN · agent-written officer voices (391 lines) [bc17c21]; style: setup + short punchline

### OFFICER_ArmsDealer_HOLD (3)
- "Hold the line? Gladly. I've got a lot of rounds that want to hold it with me." · OFFICER_ArmsDealer_HOLD · en-US.txt:1699 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Good field of fire. I've been counting the places a target could stand." · OFFICER_ArmsDealer_HOLD · en-US.txt:1700 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Stand watch, count rounds, repeat. Closest thing I've got to poetry." · OFFICER_ArmsDealer_HOLD · en-US.txt:1701 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_ArmsDealer_VICTORY (1)
- "Victory's free! Ammo's extra!" · OFFICER_ArmsDealer_VICTORY · en-US.txt:1715 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Demolitionist_IDLE (5)
- "Don't mind the smoke! It's a feature! Mostly!" · OFFICER_Demolitionist_IDLE · en-US.txt:1718 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hey, {Player}! Want to see something that goes boom?! Course you do!" · OFFICER_Demolitionist_IDLE · en-US.txt:1719 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I counted all my fingers this morning! Same as last week! Almost!" · OFFICER_Demolitionist_IDLE · en-US.txt:1720 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Remember, if you smell something sweet, run! That's the good stuff!" · OFFICER_Demolitionist_IDLE · en-US.txt:1721 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm not saying I'm reckless! I'm saying everyone else is too careful!" · OFFICER_Demolitionist_IDLE · en-US.txt:1722 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "not X, I'm Y" swap

### OFFICER_Demolitionist_MARCH (4)
- "Walk softly, everyone! I'm carrying a lot of ideas in this bag!" · OFFICER_Demolitionist_MARCH · en-US.txt:1723 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Don't bump me! Or do! I'd like to know what happens!" · OFFICER_Demolitionist_MARCH · en-US.txt:1724 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Boom boom boom, let's go! Boom boom boom, let's go!" · OFFICER_Demolitionist_MARCH · en-US.txt:1725 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Is it hot? It's hot. It's hot in my bag. That's fine! Keep walking!" · OFFICER_Demolitionist_MARCH · en-US.txt:1726 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Demolitionist_HOLD (3)
- "Holding here? Great! I'll dig a few holes! For later! Don't ask!" · OFFICER_Demolitionist_HOLD · en-US.txt:1727 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm just going to put this here. And this. And, hm, this too." · OFFICER_Demolitionist_HOLD · en-US.txt:1728 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Quiet spot! A shame! A spot like this wants a crater!" · OFFICER_Demolitionist_HOLD · en-US.txt:1729 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_GoblinTinkerer_IDLE (5)
- "I finally fixed the thing! It was the other thing! I fixed that too!" · OFFICER_GoblinTinkerer_IDLE · en-US.txt:1746 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Ooh, nice kit, {Player}! Want it reforged? I can make it shinier! Maybe!" · OFFICER_GoblinTinkerer_IDLE · en-US.txt:1747 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "My people say I'm weird. I say they're not curious enough!" · OFFICER_GoblinTinkerer_IDLE · en-US.txt:1748 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I could build you a device. It'd either help or beep. Either way, progress!" · OFFICER_GoblinTinkerer_IDLE · en-US.txt:1749 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "These spiked balls are really heavy! I'm going to keep carrying them out of habit!" · OFFICER_GoblinTinkerer_IDLE · en-US.txt:1750 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_GoblinTinkerer_MARCH (4)
- "Wait up! Short legs! Short legs and a heavy bag!" · OFFICER_GoblinTinkerer_MARCH · en-US.txt:1751 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm reforging as I walk! It's very hard! It's mostly clanging!" · OFFICER_GoblinTinkerer_MARCH · en-US.txt:1752 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Keep up the pace! The sooner we get there, the sooner I can go home!" · OFFICER_GoblinTinkerer_MARCH · en-US.txt:1753 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Everything's heavier when it's your own tools! That's a rule!" · OFFICER_GoblinTinkerer_MARCH · en-US.txt:1754 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_GoblinTinkerer_HOLD (3)
- "We hold! Great! I'll tinker! It's what I do when I'm scared!" · OFFICER_GoblinTinkerer_HOLD · en-US.txt:1755 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Don't touch that! It's not dangerous! Probably! Don't touch it!" · OFFICER_GoblinTinkerer_HOLD · en-US.txt:1756 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I love a long wait! Lots of time for little improvements!" · OFFICER_GoblinTinkerer_HOLD · en-US.txt:1757 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Princess_IDLE (8)
- "Oh, hi, {Player}! Isn't it a lovely day to be a kingdom?" · OFFICER_Princess_IDLE · en-US.txt:1774 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I made banners for everybody! They're pink! You don't have to like them, but please do!" · OFFICER_Princess_IDLE · en-US.txt:1775 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I'm so proud of our soldiers! I told them! They looked at their feet, which is how you know!" · OFFICER_Princess_IDLE · en-US.txt:1776 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Is it silly that I wave at the zombies? They never wave back. That's okay!" · OFFICER_Princess_IDLE · en-US.txt:1777 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Would you like a crown? I have spares! It's mostly for morale!" · OFFICER_Princess_IDLE · en-US.txt:1778 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I'm being very civil about the sky. I want that noted." · OFFICER_Princess_IDLE · en-US.txt:2148 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "I'd like it noted" bit
- "[grumpy] Please don't be pleasant to me tonight. It's very hard to stay cross when people are nice." · OFFICER_Princess_IDLE · en-US.txt:2149 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I smile at everyone, but not tonight. Tonight I have a face." · OFFICER_Princess_IDLE · en-US.txt:2150 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Princess_MARCH (7)
- "Everyone together! Left, right, left! I'm so proud of you all!" · OFFICER_Princess_MARCH · en-US.txt:1779 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Keep the banners high! They look so pretty in the wind!" · OFFICER_Princess_MARCH · en-US.txt:1780 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Can we skip the part where we trudge? Let's skip! Let's pretend it's a parade!" · OFFICER_Princess_MARCH · en-US.txt:1781 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Smile, everyone! It makes the enemy wonder what we know!" · OFFICER_Princess_MARCH · en-US.txt:1782 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Straight lines, please. And kindly stop humming. I'm trying not to be impolite." · OFFICER_Princess_MARCH · en-US.txt:2151 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The moon is red, the banners are pink, and nothing matches. I'm dreadfully upset." · OFFICER_Princess_MARCH · en-US.txt:2152 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] March on. Don't talk to me. I'll be perfectly dignified, and very, very quiet." · OFFICER_Princess_MARCH · en-US.txt:2153 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Princess_HOLD (6)
- "Holding the line is very brave! Please tell the soldiers I said so!" · OFFICER_Princess_HOLD · en-US.txt:1783 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "We'll wait here! I'll hum! It helps with the nerves!" · OFFICER_Princess_HOLD · en-US.txt:1784 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's quiet! Is it too quiet? I never know! Somebody tell me!" · OFFICER_Princess_HOLD · en-US.txt:1785 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] We wait. I fume. It's a very royal thing to do." · OFFICER_Princess_HOLD · en-US.txt:2154 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Do not cheer me up. I insist on staying exactly like this." · OFFICER_Princess_HOLD · en-US.txt:2155 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] I'll stand here and be cross, like a proper statue." · OFFICER_Princess_HOLD · en-US.txt:2156 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Princess_VICTORY (1)
- "Victory! Everybody gets a medal!" · OFFICER_Princess_VICTORY · en-US.txt:1798 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Dryad_IDLE (8)
- "The land is whispering, {Player}. I don't care for what it says." · OFFICER_Dryad_IDLE · en-US.txt:1802 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I've seen five hundred springs. This one is the loudest." · OFFICER_Dryad_IDLE · en-US.txt:1803 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Leave the flowers as they are. They remember who tramples them." · OFFICER_Dryad_IDLE · en-US.txt:1804 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Our soldiers are very young. They have so much to be careless with." · OFFICER_Dryad_IDLE · en-US.txt:1805 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Walk where the grass is green. It forgives you less each season." · OFFICER_Dryad_IDLE · en-US.txt:1806 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The moon is wrong, {Player}. It is wrong, and it is staring at me." · OFFICER_Dryad_IDLE · en-US.txt:2104 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Do not speak to me of peace while that sky is red." · OFFICER_Dryad_IDLE · en-US.txt:2105 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Five hundred years, and not one Blood Moon has improved my mood." · OFFICER_Dryad_IDLE · en-US.txt:2106 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Dryad_MARCH (7)
- "The ground feels uneasy beneath us. Tread lightly, if you can." · OFFICER_Dryad_MARCH · en-US.txt:1807 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Follow the wind. It knows where the rot is not." · OFFICER_Dryad_MARCH · en-US.txt:1808 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Stay close to me. The blessing only reaches so far." · OFFICER_Dryad_MARCH · en-US.txt:1809 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I do not march. I drift with purpose. Please do the same." · OFFICER_Dryad_MARCH · en-US.txt:1810 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] I walk. I do not smile. The land does not want smiling tonight." · OFFICER_Dryad_MARCH · en-US.txt:2107 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Keep your voices down. The night is sulking, and so am I." · OFFICER_Dryad_MARCH · en-US.txt:2108 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Don't touch the plants. Don't touch me. Don't touch anything." · OFFICER_Dryad_MARCH · en-US.txt:2109 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Dryad_HOLD (6)
- "We hold, and the earth holds with us. For now." · OFFICER_Dryad_HOLD · en-US.txt:1811 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Do you hear that? The soil is counting us. I hope it likes the number." · OFFICER_Dryad_HOLD · en-US.txt:1812 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Waiting is what trees do. I've gotten rather good at it." · OFFICER_Dryad_HOLD · en-US.txt:1813 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] We hold. I glare. The moon glares back. It is a stalemate." · OFFICER_Dryad_HOLD · en-US.txt:2110 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The roots are hissing. I am hissing with them." · OFFICER_Dryad_HOLD · en-US.txt:2111 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Leave me with my thoughts. They are unkind tonight." · OFFICER_Dryad_HOLD · en-US.txt:2112 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Painter_IDLE (5)
- "Look at this light, {Player}! Honestly! I could weep! I could also repaint it!" · OFFICER_Painter_IDLE · en-US.txt:1830 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "That uniform is nearly teal. Nearly. It's tearing me apart." · OFFICER_Painter_IDLE · en-US.txt:1831 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I've been staring at this wall for an hour. It wants something. I'm not sure what." · OFFICER_Painter_IDLE · en-US.txt:1832 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "They say green's calming. They're wrong. It's a fight, every shade of it." · OFFICER_Painter_IDLE · en-US.txt:1833 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Put it on a canvas and call it Soldiers, Standing. I'd buy it." · OFFICER_Painter_IDLE · en-US.txt:1834 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Painter_MARCH (4)
- "Walk in a gradient, everyone! Light, then darker! Please!" · OFFICER_Painter_MARCH · en-US.txt:1835 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Don't kick up the dust! It ruins the whole composition!" · OFFICER_Painter_MARCH · en-US.txt:1836 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm not saying I'm working. I'm saying everything is a still life if you squint." · OFFICER_Painter_MARCH · en-US.txt:1837 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "not X, I'm Y" swap
- "Mind the paint! It's wet! Everything on me is wet!" · OFFICER_Painter_MARCH · en-US.txt:1838 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Painter_HOLD (3)
- "We hold? Finally! I can finish this little corner!" · OFFICER_Painter_HOLD · en-US.txt:1839 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "A still moment. Perfect for sketching the horizon. It's mostly gray. Ugh." · OFFICER_Painter_HOLD · en-US.txt:1840 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Don't mind me. I'm just matching the sky. It keeps changing its mind." · OFFICER_Painter_HOLD · en-US.txt:1841 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Painter_FIGHT (4)
- "Paint them red! Fire!" · OFFICER_Painter_FIGHT · en-US.txt:1842 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Splash them! Fire!" · OFFICER_Painter_FIGHT · en-US.txt:1843 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Colors flying! Open fire!" · OFFICER_Painter_FIGHT · en-US.txt:1844 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Make a masterpiece! Fire!" · OFFICER_Painter_FIGHT · en-US.txt:1845 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Painter_BOSS (4)
- "{Boss}! What a canvas! Fire!" · OFFICER_Painter_BOSS · en-US.txt:1846 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "A bold subject! Paint it red!" · OFFICER_Painter_BOSS · en-US.txt:1847 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! Cover it in color! Fire!" · OFFICER_Painter_BOSS · en-US.txt:1848 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Splash {Boss} with everything!" · OFFICER_Painter_BOSS · en-US.txt:1849 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### OFFICER_Painter_VICTORY (1)
- "A masterpiece! We won!" · OFFICER_Painter_VICTORY · en-US.txt:1853 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Angler_IDLE (5)
- "Hey! You! Go catch me a fish! Not you, {Player}! Well, also you!" · OFFICER_Angler_IDLE · en-US.txt:1858 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm not bored! I'm plotting! Don't ask what!" · OFFICER_Angler_IDLE · en-US.txt:1859 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "not X, I'm Y" swap
- "The guards are, like, way too serious! Somebody tell them jokes! Not you!" · OFFICER_Angler_IDLE · en-US.txt:1860 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Whaaat?! I'm not tired! I'm just resting my eyes! Real tight!" · OFFICER_Angler_IDLE · en-US.txt:1861 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I have a ton of fish! I'm saving the best one for a fight! It's slippery!" · OFFICER_Angler_IDLE · en-US.txt:1862 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Angler_MARCH (4)
- "Walk faster, minions! I want to see the next one!" · OFFICER_Angler_MARCH · en-US.txt:1863 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Are we there yet?! I said are we there yet! I'm not whining!" · OFFICER_Angler_MARCH · en-US.txt:1864 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Carry my stuff! It's very light! It's all fish!" · OFFICER_Angler_MARCH · en-US.txt:1865 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm leading from the back! It's the best place to be! Heh heh!" · OFFICER_Angler_MARCH · en-US.txt:1866 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Angler_HOLD (3)
- "Why are we just standing here?! Is this a trick?! Is it?!" · OFFICER_Angler_HOLD · en-US.txt:1867 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "This is boring! Somebody go poke something!" · OFFICER_Angler_HOLD · en-US.txt:1868 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm setting a trap! Don't step on it! Don't step anywhere!" · OFFICER_Angler_HOLD · en-US.txt:1869 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Pirate_IDLE (5)
- "Arr, {Player}! A fine day to be standin' about and lookin' dangerous!" · OFFICER_Pirate_IDLE · en-US.txt:1886 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "The parrot says ye owe him a cracker. He's a stubborn bird. I'd pay." · OFFICER_Pirate_IDLE · en-US.txt:1887 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm not sayin' I miss the sea. I'm sayin' I miss not bein' on dry land. Aye." · OFFICER_Pirate_IDLE · en-US.txt:1888 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: "not X, I'm Y" swap
- "Bring me a barrel and a fair wind and I'll tell ye ten lies about this crew!" · OFFICER_Pirate_IDLE · en-US.txt:1889 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Where was I? Aye, rum. It's always rum." · OFFICER_Pirate_IDLE · en-US.txt:1890 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Pirate_MARCH (4)
- "Steady as she goes, lads and lasses! The land be a slow sea!" · OFFICER_Pirate_MARCH · en-US.txt:1891 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Keep yer cutlasses dry and yer tempers wet!" · OFFICER_Pirate_MARCH · en-US.txt:1892 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Hup, hup! Nobody walks the plank today! Unless they ask!" · OFFICER_Pirate_MARCH · en-US.txt:1893 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I've got a fine crew behind me! Shiver me timbers, I do!" · OFFICER_Pirate_MARCH · en-US.txt:1894 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Pirate_HOLD (3)
- "We hold the deck, then! The deck be a field! Close enough!" · OFFICER_Pirate_HOLD · en-US.txt:1895 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Aye, I'll stand watch! Hand me the spyglass! I don't have a spyglass!" · OFFICER_Pirate_HOLD · en-US.txt:1896 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "If ye hear a squawk, that's the parrot! If ye hear screamin', that's me!" · OFFICER_Pirate_HOLD · en-US.txt:1897 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Pirate_VICTORY (2)
- "Victory! Break out the grog!" · OFFICER_Pirate_VICTORY · en-US.txt:1909 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "We sank 'er! Hooray!" · OFFICER_Pirate_VICTORY · en-US.txt:1910 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Clothier_IDLE (5)
- "Don't stand so close to the fabric. You'll get lint on your dignity." · OFFICER_Clothier_IDLE · en-US.txt:1914 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I've sewn worse trousers than these battles. Well. Not by much." · OFFICER_Clothier_IDLE · en-US.txt:1915 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "That coat's a crime, {Player}. I'd say so for free. Also I can fix it. Not for free." · OFFICER_Clothier_IDLE · en-US.txt:1916 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Being cursed was a lot more peaceful. At least nobody asked me for hemming." · OFFICER_Clothier_IDLE · en-US.txt:1917 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Hold still. I'm mentally measuring you. Don't take it personally." · OFFICER_Clothier_IDLE · en-US.txt:1918 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Clothier_MARCH (4)
- "Walk, boys, walk. Hems don't get any shorter standing still." · OFFICER_Clothier_MARCH · en-US.txt:1919 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "If anybody tears a sleeve, it's coming off your pay." · OFFICER_Clothier_MARCH · en-US.txt:1920 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "My knees are older than most of this army. Slow down a bit." · OFFICER_Clothier_MARCH · en-US.txt:1921 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hup, hup, hup. And mind the thread. I don't have an unlimited supply of thread." · OFFICER_Clothier_MARCH · en-US.txt:1922 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Clothier_HOLD (3)
- "We hold. Fine. I'll sew. It's what I do when I'm angry." · OFFICER_Clothier_HOLD · en-US.txt:1923 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Don't look at me. I'm busy being disappointed in the weather." · OFFICER_Clothier_HOLD · en-US.txt:1924 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Quiet job. Good. Maybe now somebody will notice my stitching." · OFFICER_Clothier_HOLD · en-US.txt:1925 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_Clothier_FIGHT (3)
- "Get off my lawn! Fire!" · OFFICER_Clothier_FIGHT · en-US.txt:1926 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hmph! Take that!" · OFFICER_Clothier_FIGHT · en-US.txt:1927 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the fringe! Fire!" · OFFICER_Clothier_FIGHT · en-US.txt:1928 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Clothier_BOSS (1)
- "{Boss}! Ugly thing! Fire!" · OFFICER_Clothier_BOSS · en-US.txt:1930 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Clothier_HURT (1)
- "That's a rip! Still fighting!" · OFFICER_Clothier_HURT · en-US.txt:1934 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Clothier_VICTORY (1)
- "We won! Clean seam!" · OFFICER_Clothier_VICTORY · en-US.txt:1938 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Wizard_IDLE (5)
- "Ah, {Player}. I foresaw this. Or was that yesterday? Either way, hello." · OFFICER_Wizard_IDLE · en-US.txt:1942 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The crystal ball says something is about to happen. It says that every day." · OFFICER_Wizard_IDLE · en-US.txt:1943 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I put my hat down just a moment ago. Somewhere. It's very rude of it." · OFFICER_Wizard_IDLE · en-US.txt:1944 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "There's a spell for everything. I forget which spell is for remembering." · OFFICER_Wizard_IDLE · en-US.txt:1945 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Would you like to see a trick? It's always the same trick. But the same trick is lovely." · OFFICER_Wizard_IDLE · en-US.txt:1946 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Wizard_MARCH (4)
- "Onward! Hm. Which way's onward? Oh, there. Onward!" · OFFICER_Wizard_MARCH · en-US.txt:1947 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The air is full of small spells today. I'm trying not to sneeze." · OFFICER_Wizard_MARCH · en-US.txt:1948 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Stay in the middle of the road! Sorcery has a lot of ledges!" · OFFICER_Wizard_MARCH · en-US.txt:1949 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'll lead the way with a nice, steady glow. Please ignore the humming." · OFFICER_Wizard_MARCH · en-US.txt:1950 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Wizard_HOLD (3)
- "We hold. Yes. Excellent. I'll stare at the middle distance. I'm very good at it." · OFFICER_Wizard_HOLD · en-US.txt:1951 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Hush. I'm listening to the stars. They have so little to say." · OFFICER_Wizard_HOLD · en-US.txt:1952 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "A quiet moment. Oh, I could nap. I could nap forever. Would anyone mind?" · OFFICER_Wizard_HOLD · en-US.txt:1953 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Zoologist_IDLE (8)
- "Like, hi, {Player}! Did you see the wolf by the wall? He's my best friend! Don't tell him!" · OFFICER_Zoologist_IDLE · en-US.txt:1970 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I love this place! Everything's so growly! It's rad!" · OFFICER_Zoologist_IDLE · en-US.txt:1971 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "My tail's stuck in my belt again! Don't laugh! Okay, laugh a little!" · OFFICER_Zoologist_IDLE · en-US.txt:1972 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "If you hear howling, that's totally just me! Mostly!" · OFFICER_Zoologist_IDLE · en-US.txt:1973 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Zombies are, like, so misunderstood! They just want to hug! With teeth!" · OFFICER_Zoologist_IDLE · en-US.txt:1974 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Ugh, you guys, can you, like, not right now? The moon's giving me a headache!" · OFFICER_Zoologist_IDLE · en-US.txt:2126 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] The wolves are all howling and I'm not even mad at them. I'm mad at the sky!" · OFFICER_Zoologist_IDLE · en-US.txt:2127 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Everything's, like, red and it's making me crabby. Sorry. Not sorry!" · OFFICER_Zoologist_IDLE · en-US.txt:2128 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Zoologist_MARCH (7)
- "Come on, pack! Let's go! Let's go! Let's go!" · OFFICER_Zoologist_MARCH · en-US.txt:1975 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Sniff, sniff! I think something's up ahead! Something cute! And bitey!" · OFFICER_Zoologist_MARCH · en-US.txt:1976 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Walking's, like, the best! I'm gonna do it on all fours!" · OFFICER_Zoologist_MARCH · en-US.txt:1977 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Stay close, you guys! The pack protects its own!" · OFFICER_Zoologist_MARCH · en-US.txt:1978 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Walking. Not talking. The moon's got my ears all pinned back!" · OFFICER_Zoologist_MARCH · en-US.txt:2129 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Don't pet me. I'm not, like, in a petting mood!" · OFFICER_Zoologist_MARCH · en-US.txt:2130 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] Come on, pack. Heads down. Let's just get this over with." · OFFICER_Zoologist_MARCH · en-US.txt:2131 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Zoologist_HOLD (6)
- "We're staying? Rad! I'll look at the squirrels!" · OFFICER_Zoologist_HOLD · en-US.txt:1979 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I'm gonna make friends with the nearest thing! Wish me luck!" · OFFICER_Zoologist_HOLD · en-US.txt:1980 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "It's so peaceful here, I could, like, nap on a rock!" · OFFICER_Zoologist_HOLD · en-US.txt:1981 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "[grumpy] We're staying? Ugh! Fine! But nobody touch my tail!" · OFFICER_Zoologist_HOLD · en-US.txt:2132 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] Don't talk to me. I'm, like, brooding with the wolves." · OFFICER_Zoologist_HOLD · en-US.txt:2133 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "[grumpy] This night is so rude. Everything's so rude!" · OFFICER_Zoologist_HOLD · en-US.txt:2134 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Cyborg_IDLE (5)
- "Greetings, {Player}. My social module reports a seventy percent chance of small talk." · OFFICER_Cyborg_IDLE · en-US.txt:1998 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I have updated my jokes. The new one is: ha. Ha. Ha." · OFFICER_Cyborg_IDLE · en-US.txt:1999 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Observation: you are standing, I am standing. Efficiency is high." · OFFICER_Cyborg_IDLE · en-US.txt:2000 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I was designed to calculate. I have used it to calculate how bored I am. It is a lot." · OFFICER_Cyborg_IDLE · en-US.txt:2001 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "My drones are warming up. This is not a euphemism. They are literally warm." · OFFICER_Cyborg_IDLE · en-US.txt:2002 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Cyborg_MARCH (4)
- "Locomotion at optimal speed. Please do not distract my actuators." · OFFICER_Cyborg_MARCH · en-US.txt:2003 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Scanning the route. Probability of death: acceptable. Probability of rain: also acceptable." · OFFICER_Cyborg_MARCH · en-US.txt:2004 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I am marching in a straight line. I do not have another line." · OFFICER_Cyborg_MARCH · en-US.txt:2005 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The army's rhythm is uneven. I recommend a metronome. Or me." · OFFICER_Cyborg_MARCH · en-US.txt:2006 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_Cyborg_HOLD (3)
- "Standing by. I am standing. I am by." · OFFICER_Cyborg_HOLD · en-US.txt:2007 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Running a diagnostic. It says I am doing fine. It says that often." · OFFICER_Cyborg_HOLD · en-US.txt:2008 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I enjoy this. It is like sleeping, except I am awake." · OFFICER_Cyborg_HOLD · en-US.txt:2009 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_WitchDoctor_IDLE (5)
- "The spirits grow restless, {Player}. As do I. We have this in common." · OFFICER_WitchDoctor_IDLE · en-US.txt:2026 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Heed me. A little of what is poison is also medicine. A lot of it is just poison." · OFFICER_WitchDoctor_IDLE · en-US.txt:2027 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I have a charm for every fear. I have no charm for the fear of charms." · OFFICER_WitchDoctor_IDLE · en-US.txt:2028 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Look not at the mask. It is not looking at you. It is looking past you." · OFFICER_WitchDoctor_IDLE · en-US.txt:2029 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "The jungle taught me three things. I forget the first two. The third is run." · OFFICER_WitchDoctor_IDLE · en-US.txt:2030 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_WitchDoctor_MARCH (4)
- "Walk with care. The old ones walk beside us, and they step on toes." · OFFICER_WitchDoctor_MARCH · en-US.txt:2031 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I shake the bones so the road remembers us." · OFFICER_WitchDoctor_MARCH · en-US.txt:2032 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "Keep your charms close. Keep your enemies closer, so my venom has less distance to travel." · OFFICER_WitchDoctor_MARCH · en-US.txt:2033 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "The spirits whisper of a long road. They do not whisper of the end of it." · OFFICER_WitchDoctor_MARCH · en-US.txt:2034 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_WitchDoctor_HOLD (3)
- "We hold. The spirits and I will confer. Quietly. Very quietly." · OFFICER_WitchDoctor_HOLD · en-US.txt:2035 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Silence is also a spell. A weak one. But we are using it." · OFFICER_WitchDoctor_HOLD · en-US.txt:2036 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I will make tea from the dust of the ground. Do not ask what is in the tea." · OFFICER_WitchDoctor_HOLD · en-US.txt:2037 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_TaxCollector_IDLE (5)
- "Bah! What do you want? A gold coin? Join the queue!" · OFFICER_TaxCollector_IDLE · en-US.txt:2054 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "I count, I frown, I count again. Peaceful hours, these." · OFFICER_TaxCollector_IDLE · en-US.txt:2055 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Every coin in this army is a coin I haven't collected yet. It keeps me up." · OFFICER_TaxCollector_IDLE · en-US.txt:2056 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Don't smile at me! I know what a smile's worth! It's nothing!" · OFFICER_TaxCollector_IDLE · en-US.txt:2057 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Wages! Rent! Fees! And the zombies expect a handshake!" · OFFICER_TaxCollector_IDLE · en-US.txt:2058 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_TaxCollector_MARCH (4)
- "Hurry up! Every minute we dawdle, someone doesn't pay!" · OFFICER_TaxCollector_MARCH · en-US.txt:2059 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I follow you for the bounty. And the rent. Mostly the rent." · OFFICER_TaxCollector_MARCH · en-US.txt:2060 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "Don't step on my cloak! It's the one thing I own outright!" · OFFICER_TaxCollector_MARCH · en-US.txt:2061 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "March, march! Bah! My feet have a lien on me!" · OFFICER_TaxCollector_MARCH · en-US.txt:2062 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]

### OFFICER_TaxCollector_HOLD (3)
- "We hold. And I count. And I frown. A very productive afternoon." · OFFICER_TaxCollector_HOLD · en-US.txt:2063 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline
- "A fixed post! Just the thing for a long talk about unpaid taxes!" · OFFICER_TaxCollector_HOLD · en-US.txt:2064 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]
- "I intend to stand here until everybody settles up! Which may be a while!" · OFFICER_TaxCollector_HOLD · en-US.txt:2065 · AI-WRITTEN · dialogue round 3, agent-written to Eve's spec [491f37d]; style: setup + short punchline

### OFFICER_TaxCollector_FIGHT (4)
- "Pay up! Fire!" · OFFICER_TaxCollector_FIGHT · en-US.txt:2066 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Enemy's overdue! Open fire!" · OFFICER_TaxCollector_FIGHT · en-US.txt:2067 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "You owe me! Take that!" · OFFICER_TaxCollector_FIGHT · en-US.txt:2068 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Get audited! Fire!" · OFFICER_TaxCollector_FIGHT · en-US.txt:2069 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_TaxCollector_BOSS (2)
- "{Boss}! Pay your debts! Fire!" · OFFICER_TaxCollector_BOSS · en-US.txt:2070 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss} won't be paying! Open fire!" · OFFICER_TaxCollector_BOSS · en-US.txt:2072 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_TaxCollector_HURT (1)
- "Ow! That's billed to you!" · OFFICER_TaxCollector_HURT · en-US.txt:2074 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_TaxCollector_VICTORY (3)
- "Debt settled! We won!" · OFFICER_TaxCollector_VICTORY · en-US.txt:2077 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Audit complete! Victory!" · OFFICER_TaxCollector_VICTORY · en-US.txt:2078 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Pay up! We won!" · OFFICER_TaxCollector_VICTORY · en-US.txt:2079 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### RIFT_SHOCK (2)
- "My rates assumed a sun." · RIFT_SHOCK|Sellsword · en-US.txt:2227 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "I used to complain about the heat. I take it back." · RIFT_SHOCK|Cynic · en-US.txt:2230 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline

### RIFT_ABSENT (3)
- "Last I remember, I was dying. That was normal." · RIFT_ABSENT · en-US.txt:2238 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Nobody told me. I'd have asked for more." · RIFT_ABSENT|Sellsword · en-US.txt:2241 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I step away for a moment, and this." · RIFT_ABSENT|Zealot · en-US.txt:2242 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]

### RIFT_COLD (3)
- "Hazard pay. For the cold. Just asking." · RIFT_COLD|Sellsword · en-US.txt:2253 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "No sun, now no warmth. I see a pattern." · RIFT_COLD|Cynic · en-US.txt:2255 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Cold again. What a surprise." · RIFT_COLD|Cynic · en-US.txt:2495 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_POWER (2)
- "Best kit money can buy. Can't buy the sun." · RIFT_POWER|Sellsword · en-US.txt:2264 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "All this armor and I still feel naked." · RIFT_POWER|Cynic · en-US.txt:2497 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]

### RIFT_LATER (3)
- "If there's a job to fix the sky, I'd hear terms." · RIFT_LATER|Sellsword · en-US.txt:2275 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "We could wait for it to leave. Bad plan." · RIFT_LATER|Cynic · en-US.txt:2277 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Still no plan? Good. I'd hate a surprise." · RIFT_LATER|Cynic · en-US.txt:2499 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_DARK (16)
- "At least nobody's getting sunburnt." · RIFT_DARK · en-US.txt:2281 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "Good news. The zombies look worried too." · RIFT_DARK · en-US.txt:2282 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I've stopped saving for retirement." · RIFT_DARK · en-US.txt:2283 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "Nice weather. For a funeral." · RIFT_DARK · en-US.txt:2284 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Dying's easy. Coming back to this is the hard part." · RIFT_DARK · en-US.txt:2285 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "{Tank} died twice today. Said it was cozy." · RIFT_DARK · en-US.txt:2286 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Free snow, free dark. You do provide, {Player}." · RIFT_DARK · en-US.txt:2287 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Mum asked when I'm coming home. I said yes." · RIFT_DARK|Greenhorn · en-US.txt:2288 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I've raised my rates. End-of-the-world pricing." · RIFT_DARK|Sellsword · en-US.txt:2289 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Pay me in advance. Well in advance." · RIFT_DARK|Sellsword · en-US.txt:2290 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "The Drowned One would call this a nice day." · RIFT_DARK|Zealot · en-US.txt:2291 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "Cheer up. It can't get darker. Literally." · RIFT_DARK|Cynic · en-US.txt:2292 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I said we were doomed. Nobody lets me enjoy it." · RIFT_DARK|Cynic · en-US.txt:2293 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I've fought worse. Can't remember when." · RIFT_DARK|Veteran · en-US.txt:2294 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Look on the bright side. Oh. Right." · RIFT_DARK · en-US.txt:2500 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Living's overrated. Dying's worse. So here we are." · RIFT_DARK|Veteran · en-US.txt:2501 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_RESPECT (2)
- "[non-summoner] I owe a summoner an apology. Several." · RIFT_RESPECT · en-US.txt:2297 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Glutton} still takes all the kills. Good." · RIFT_RESPECT|Cynic · en-US.txt:2306 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline

### RIFT_IDLE (4)
- "I'd make a joke. I can't think of one." · RIFT_IDLE|Cynic · en-US.txt:2325 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]; style: setup + short punchline
- "You can tell it's bad. I've stopped complaining." · RIFT_IDLE|Cynic · en-US.txt:2326 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Paid, fed, alive. Two out of three, some days." · RIFT_IDLE|Sellsword · en-US.txt:2506 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Don't ask how I am. You know how I am." · RIFT_IDLE|Cynic · en-US.txt:2508 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_DUKE (2)
- "Rigroth the Undying would run. I'm running." · RIFT_DUKE|Zealot · en-US.txt:2349 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "One less thing to fight. Somehow worse." · RIFT_DUKE|Cynic · en-US.txt:2350 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline

### RIFT_BLOOD (2)
- "Worst night yet. I said that last time." · RIFT_BLOOD|Cynic · en-US.txt:2380 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Red sky, red rain. Very subtle." · RIFT_BLOOD|Cynic · en-US.txt:2381 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_GROW1 (2)
- "That's not the size it was when I signed." · RIFT_GROW1|Sellsword · en-US.txt:2392 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "We killed something big. The sky got bigger." · RIFT_GROW1|Cynic · en-US.txt:2394 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_GROW2 (2)
- "Every boss we kill, the sky gets worse. Hm." · RIFT_GROW2|Sellsword · en-US.txt:2403 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Keep winning, {Player}. See where it gets us." · RIFT_GROW2|Cynic · en-US.txt:2405 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_ICE (2)
- "Water costs extra now. Somebody has to melt it." · RIFT_ICE|Sellsword · en-US.txt:2424 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "No sun, no grass, now no water. Great." · RIFT_ICE|Cynic · en-US.txt:2425 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_FOG (1)
- "Fog. Of course. Why not." · RIFT_FOG|Cynic · en-US.txt:2447 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_SCARE (2)
- "No. Nope. I quit. I'm staying, but I quit." · RIFT_SCARE|Sellsword · en-US.txt:2456 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Nope. Absolutely not." · RIFT_SCARE|Cynic · en-US.txt:2458 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_GRIM (11)
- "At least the dead don't have to look up." · RIFT_GRIM · en-US.txt:2462 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Funeral's at noon. Same as midnight." · RIFT_GRIM · en-US.txt:2463 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I'd kill for a sunrise. Probably will." · RIFT_GRIM · en-US.txt:2464 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Last one standing gets the sky. Lucky them." · RIFT_GRIM · en-US.txt:2465 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Digging graves keeps you warm. Small mercies." · RIFT_GRIM · en-US.txt:2466 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I wrote my will. It's one word. "Sorry."" · RIFT_GRIM|Greenhorn · en-US.txt:2467 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Pay me now. Later's not looking likely." · RIFT_GRIM|Sellsword · en-US.txt:2468 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I stopped asking the gods. They stopped first." · RIFT_GRIM|Zealot · en-US.txt:2469 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Can't get worse. Watch." · RIFT_GRIM|Cynic · en-US.txt:2470 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Still doomed. I'll check again tomorrow." · RIFT_GRIM|Cynic · en-US.txt:2471 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Dead by spring. If there's a spring." · RIFT_GRIM|Veteran · en-US.txt:2472 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_WORN (1)
- "I've run out of things to complain about. Almost." · RIFT_WORN|Cynic · en-US.txt:2484 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_ANGRY (5)
- "Rain, dark, cold, and that thing. Every night!" · RIFT_ANGRY · en-US.txt:2535 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Triple pay. Tonight. Not a copper less." · RIFT_ANGRY|Sellsword · en-US.txt:2543 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Oh, wonderful. Another one. Perfect." · RIFT_ANGRY|Cynic · en-US.txt:2545 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Say one more cheerful thing. Go on." · RIFT_ANGRY · en-US.txt:2648 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Wow. Great. Another night under that." · RIFT_ANGRY|Cynic · en-US.txt:2657 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline

### RIFT_BOSS (13)
- "Another one. Of course there is." · RIFT_BOSS · en-US.txt:2557 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Something else that wants us dead. Great." · RIFT_BOSS · en-US.txt:2559 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Kill it fast. I want to sit down." · RIFT_BOSS · en-US.txt:2560 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[summoner] Minions are deployed. Scaling is the sky's problem." · RIFT_BOSS · en-US.txt:2562 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "[summoner] I'll let the staff decide. It's smarter than me." · RIFT_BOSS · en-US.txt:2563 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[mage] Right. Fire. Or ice. Wait, which one is this?" · RIFT_BOSS · en-US.txt:2564 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[mage] Spell's ready. I think. Stand back. Further." · RIFT_BOSS · en-US.txt:2565 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[melee] I'll get close. Closer than last time. Promise." · RIFT_BOSS · en-US.txt:2566 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[ranger] Shoot it till it stops. Same as always." · RIFT_BOSS · en-US.txt:2567 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[rogue] I'll throw things at it. It's what I do." · RIFT_BOSS · en-US.txt:2568 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "This one's extra. It should be extra." · RIFT_BOSS|Sellsword · en-US.txt:2570 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Rigroth the Undying would be annoyed too." · RIFT_BOSS|Zealot · en-US.txt:2571 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "A boss. In this weather. Lovely." · RIFT_BOSS|Cynic · en-US.txt:2572 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline

### RIFT_TOWN (18)
- "Bought a stack of torches off {Merchant}. Still dark." · RIFT_TOWN · en-US.txt:2576 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Nurse} patched me up without a word." · RIFT_TOWN · en-US.txt:2577 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{ArmsDealer} gave me extra rounds. Didn't charge." · RIFT_TOWN · en-US.txt:2578 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Demolitionist} keeps lighting fuses for the warmth." · RIFT_TOWN · en-US.txt:2579 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Dryad} won't go outside anymore." · RIFT_TOWN · en-US.txt:2580 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Guide} says it gets better. He doesn't sound sure." · RIFT_TOWN · en-US.txt:2581 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Tavernkeep} stopped watering the drinks." · RIFT_TOWN · en-US.txt:2582 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{GoblinTinkerer} built a brighter lamp. Not bright enough." · RIFT_TOWN · en-US.txt:2583 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Painter} only paints suns now." · RIFT_TOWN · en-US.txt:2584 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Angler} hasn't caught a thing since the lake froze." · RIFT_TOWN · en-US.txt:2585 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Clothier} knitted me a scarf. Best thing I own." · RIFT_TOWN · en-US.txt:2586 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Wizard} stared at the sky for an hour. Then went in." · RIFT_TOWN · en-US.txt:2587 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Stylist} offered a haircut. I asked what for." · RIFT_TOWN · en-US.txt:2588 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{PartyGirl} hasn't thrown a party in weeks." · RIFT_TOWN · en-US.txt:2589 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Mechanic} wired lamps all over town. Still dark." · RIFT_TOWN · en-US.txt:2590 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{TaxCollector} still collects. Some things never change." · RIFT_TOWN · en-US.txt:2591 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{WitchDoctor} burns something every night. Smells awful." · RIFT_TOWN · en-US.txt:2592 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Cyborg} says his sensors can't read the sky." · RIFT_TOWN · en-US.txt:2593 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]

### RIFT_CLASS (12)
- "[summoner] My minions won't leave my side now. Same." · RIFT_CLASS · en-US.txt:2602 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[summoner] Even the summons look nervous." · RIFT_CLASS · en-US.txt:2603 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "[rogue] Nobody sneaks up on anybody here. Too quiet." · RIFT_CLASS · en-US.txt:2604 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[rogue] Threw my last dagger at a shadow. It was a shadow." · RIFT_CLASS · en-US.txt:2605 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Mage} lit the camp for us. Nobody laughed this time." · RIFT_CLASS · en-US.txt:2608 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[non-mage] Mages used to be the joke. Not anymore." · RIFT_CLASS · en-US.txt:2611 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[non-summoner] I take back the summon jokes. Most of them." · RIFT_CLASS · en-US.txt:2612 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Melee} charged again. Brave, or tired. Same thing now." · RIFT_CLASS|Cynic · en-US.txt:2613 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[summoner] Three sentries up. The sky doesn't care about uptime." · RIFT_CLASS · en-US.txt:2673 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "[summoner] Whip, staff, minions. Fully optimized. Still cold." · RIFT_CLASS · en-US.txt:2674 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "[summoner] My minion setup is flawless. The world isn't." · RIFT_CLASS · en-US.txt:2675 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Let's see {Summoner}'s staff patch the sky. Go on." · RIFT_CLASS · en-US.txt:2676 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline

### RIFT_HABIT (3)
- "*counts coins. Counts them again.*" · RIFT_HABIT|Sellsword · en-US.txt:2622 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "*bites a coin to check it's real*" · RIFT_HABIT|Sellsword · en-US.txt:2623 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*mutters something rude at the rift*" · RIFT_HABIT|Cynic · en-US.txt:2627 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]

### RIFT_FLIGHT (1)
- "Flying. Toward that. Great idea." · RIFT_FLIGHT|Cynic · en-US.txt:2669 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline

### SCENE_GENESIS (2)
- "Whatever you just fired, {Player}, I want it off my contract!" · SCENE_GENESIS|Sellsword · en-US.txt:2714 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Oh good. We're knocking now. On THAT." · SCENE_GENESIS|Cynic · en-US.txt:2716 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_RIFT_OPEN (4)
- "I want to go back to the slimes. I liked the slimes." · SCENE_RIFT_OPEN|Greenhorn · en-US.txt:2724 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "No. No, this is a different job. This is a whole different job." · SCENE_RIFT_OPEN|Sellsword · en-US.txt:2725 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Mother Gnash, I take back everything I said about the dark!" · SCENE_RIFT_OPEN|Zealot · en-US.txt:2726 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "So that's what's been behind the sun. Great. Good to know." · SCENE_RIFT_OPEN|Cynic · en-US.txt:2727 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_RIFT_DOWN (2)
- "We killed the door. Congratulations. Here comes the house." · SCENE_RIFT_DOWN|Cynic · en-US.txt:2743 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Don't cheer. Reload." · SCENE_RIFT_DOWN|Veteran · en-US.txt:2744 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_RIFT_FIGHT (5)
- "[mage] I'm casting into a hole in the sky. I hope it counts!" · SCENE_RIFT_FIGHT · en-US.txt:2751 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "[summoner] My minions won't go near it. Smart minions." · SCENE_RIFT_FIGHT · en-US.txt:2752 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "[melee] I'd charge it! It's in the sky! I'm being responsible!" · SCENE_RIFT_FIGHT · en-US.txt:2753 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Is it hurt? How do you tell if a hole is hurt?" · SCENE_RIFT_FIGHT|Greenhorn · en-US.txt:2754 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "I'm aiming at nothing. And nothing is winning." · SCENE_RIFT_FIGHT|Cynic · en-US.txt:2756 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_EMERGE (3)
- "Nobody said it had legs. I could've lived not knowing it had legs." · SCENE_AVATAR_EMERGE|Greenhorn · en-US.txt:2763 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "I quit. I'm writing it down right now. I quit." · SCENE_AVATAR_EMERGE|Sellsword · en-US.txt:2764 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Of course the hole had a body. Why wouldn't the hole have a body." · SCENE_AVATAR_EMERGE|Cynic · en-US.txt:2766 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_HEAD (1)
- "I liked it better without a head." · SCENE_AVATAR_HEAD|Cynic · en-US.txt:2773 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_SCREAM (2)
- "I've been paid to stand near worse. No. No, I haven't." · SCENE_AVATAR_SCREAM|Sellsword · en-US.txt:2782 · AI-WRITTEN · bossdialogue builder follow-up [7b1b8a5]; style: setup + short punchline
- "Well, it's awake. Anybody want to go back to sleep?" · SCENE_AVATAR_SCREAM|Cynic · en-US.txt:2784 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_FIGHT (7)
- "[mage] I've got one spell left in me and it's going straight up!" · SCENE_AVATAR_FIGHT · en-US.txt:2795 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "[summoner] My minions are hiding behind me. I'm hiding behind {Player}." · SCENE_AVATAR_FIGHT · en-US.txt:2796 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "[ranger] Bullets go in. Nothing comes out. I'm still shooting." · SCENE_AVATAR_FIGHT · en-US.txt:2797 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "[melee] I'm getting closer! I'm getting closer! I'm getting further!" · SCENE_AVATAR_FIGHT · en-US.txt:2798 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I've fought for anyone who'd pay me! I'm fighting this one for free!" · SCENE_AVATAR_FIGHT|Sellsword · en-US.txt:2801 · AI-WRITTEN · bossdialogue builder follow-up [7b1b8a5]; style: setup + short punchline
- "Great plan, everyone. Shoot the god. Love it." · SCENE_AVATAR_FIGHT|Cynic · en-US.txt:2804 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "If we live, I'm never complaining about the weather again." · SCENE_AVATAR_FIGHT|Cynic · en-US.txt:2805 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_BLOOD (3)
- "I'm going to need a new uniform. And a new everything." · SCENE_AVATAR_BLOOD|Greenhorn · en-US.txt:2819 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Mother Gnash would call this a blessing. Mother Gnash is wrong." · SCENE_AVATAR_BLOOD|Zealot · en-US.txt:2820 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Red. Lovely. Matches the sky we used to have." · SCENE_AVATAR_BLOOD|Cynic · en-US.txt:2821 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_HEADPATS (2)
- "Is it... petting {Player}?" · SCENE_AVATAR_HEADPATS · en-US.txt:2831 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "That's worse. Somehow that's worse." · SCENE_AVATAR_HEADPATS|Cynic · en-US.txt:2834 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_UNIVERSE (1)
- "Who's in charge now?! Is it me?! It can't be me!" · SCENE_AVATAR_UNIVERSE|Greenhorn · en-US.txt:2841 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_RETURN (1)
- "You came back! I knew it! I didn't know it! I hoped it!" · SCENE_AVATAR_RETURN|Greenhorn · en-US.txt:2848 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_PHASE3 (1)
- "Second scream. I didn't like the first one either." · SCENE_AVATAR_PHASE3|Cynic · en-US.txt:2856 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_NAMELESS (1)
- "Oh, there's a second one. Of course there's a second one." · SCENE_AVATAR_NAMELESS|Cynic · en-US.txt:2880 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_HOME (1)
- "I'm going to hug a tree. Any tree. That tree." · SCENE_AVATAR_HOME|Greenhorn · en-US.txt:2886 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_KILL (4)
- "I'm never going to sleep again. I'm so happy. I'm never going to sleep again." · SCENE_AVATAR_KILL · en-US.txt:2893 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I fought a god! My mother is never going to believe the office job now!" · SCENE_AVATAR_KILL|Greenhorn · en-US.txt:2894 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "I've had a lot of employers. None of them ever did THAT." · SCENE_AVATAR_KILL|Sellsword · en-US.txt:2895 · AI-WRITTEN · bossdialogue builder follow-up [7b1b8a5]; style: setup + short punchline
- "We won. I'll believe it when the sun comes back." · SCENE_AVATAR_KILL|Cynic · en-US.txt:2897 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_NAMELESS_FIGHT (5)
- "[mage] I'm casting at a god! This is the best day of my life! Also the worst!" · SCENE_NAMELESS_FIGHT · en-US.txt:2922 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "[summoner] My minions keep bowing to it. Stop bowing!" · SCENE_NAMELESS_FIGHT · en-US.txt:2923 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "[melee] I'm going in! I'm going in after this volley! After the next one!" · SCENE_NAMELESS_FIGHT · en-US.txt:2924 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Are we allowed to hit it? Is there a rule? There should be a rule." · SCENE_NAMELESS_FIGHT|Greenhorn · en-US.txt:2925 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Every time I blink there's a new sky. Pick one." · SCENE_NAMELESS_FIGHT|Cynic · en-US.txt:2928 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_CLOCK (1)
- "Of course it owns time. Why wouldn't it own time." · SCENE_NAMELESS_CLOCK|Cynic · en-US.txt:2940 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_RESCUE (1)
- "Something that big doesn't help for free. Remember I said that." · SCENE_NAMELESS_RESCUE|Cynic · en-US.txt:2964 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_KILL (1)
- "So what's left? Who do we fight now? Each other?" · SCENE_NAMELESS_KILL|Cynic · en-US.txt:2971 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_HOMECOMING (3)
- "We waited the whole time! I polished my armor twice!" · SCENE_NAMELESS_HOMECOMING|Greenhorn · en-US.txt:2978 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "You went in alone?! Next time I'm coming. I'll waive the fee." · SCENE_NAMELESS_HOMECOMING|Sellsword · en-US.txt:2979 · AI-WRITTEN · bossdialogue builder follow-up [7b1b8a5]; style: setup + short punchline
- "Next time, take us. We'd have been useless, but take us." · SCENE_NAMELESS_HOMECOMING|Cynic · en-US.txt:2981 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_WOTG_COMMANDER_DOWN (1)
- "Well. That's bad. That's very bad. Keep shooting." · SCENE_WOTG_COMMANDER_DOWN|Cynic · en-US.txt:2991 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_WOTG_MOURN (1)
- "{Comrade} picked a bad day to die. Every day's been a bad day." · SCENE_WOTG_MOURN|Cynic · en-US.txt:3000 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### Potions.hjson spectator lost-bet lines (death messages) (4)
- "{0} bet on a fight that walked away." · Potions.hjson LostBetLeft.Left1 (line ~50) · AI-WRITTEN · commit 28e1c80 "a boss that leaves is a lost bet", "New death lines" (Claude); sits next to Eve's five Bet lines, so it reads like hers but is not
- "{0} watched the boss leave and the bet go with it." · Potions.hjson LostBetLeft.Left2 (line ~51) · AI-WRITTEN · commit 28e1c80 "a boss that leaves is a lost bet", "New death lines" (Claude); sits next to Eve's five Bet lines, so it reads like hers but is not
- "{0} learned that a boss who leaves has not lost." · Potions.hjson LostBetLeft.Left3 (line ~52) · AI-WRITTEN · commit 28e1c80 "a boss that leaves is a lost bet", "New death lines" (Claude); sits next to Eve's five Bet lines, so it reads like hers but is not
- "{0} was left holding the bet." · Potions.hjson LostBetLeft.Left4 (line ~53) · AI-WRITTEN · commit 28e1c80 "a boss that leaves is a lost bet", "New death lines" (Claude); sits next to Eve's five Bet lines, so it reads like hers but is not

## 2. Other AI-written lines (not jokes)

### IDLE (8)
- "Is the eye in the sky normal? I'd like to know before it comes back." · IDLE|Greenhorn · en-US.txt:125 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Please don't put me at the front, {Player}. I'll go if you say so." · IDLE|Greenhorn · en-US.txt:131 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Are we the good guys? I'd like us to be the good guys." · IDLE|Greenhorn · en-US.txt:134 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Is it alright if I'm scared? I'll keep shooting either way." · IDLE|Greenhorn · en-US.txt:136 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "I'm proud to serve. Is anyone writing down that I'm proud to serve?" · IDLE|Zealot · en-US.txt:158 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "{Dryad} says the Corruption fears us. I choose to believe her." · IDLE|Zealot · en-US.txt:164 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Don't tell the greenhorns, but I'm still scared. It keeps me careful." · IDLE|Veteran · en-US.txt:182 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline
- "Whatever comes next, I've fought bigger. With a worse gun." · IDLE|Veteran · en-US.txt:189 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline

### IDLE_NIGHT (1)
- "Stay close, {Player}. The night's friendlier when there's two of us." · IDLE_NIGHT · en-US.txt:313 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]

### GRAVEYARD (1)
- "Stay close, {Player}. I don't like how quiet the headstones are." · GRAVEYARD · en-US.txt:376 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]

### DESERTED (1)
- "{Name} has left. The bunk is empty and the boots are gone." · DESERTED · en-US.txt:458 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]

### ENGAGE (47)
- "Enemy incoming! Open fire!" · ENGAGE · en-US.txt:524 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Contact! Open fire!" · ENGAGE · en-US.txt:525 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Here they come! Weapons up!" · ENGAGE · en-US.txt:526 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Targets ahead! Fire at will!" · ENGAGE · en-US.txt:527 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hostiles in sight! Let them have it!" · ENGAGE · en-US.txt:528 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Incoming! Take cover!" · ENGAGE · en-US.txt:529 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stand ready! They're here!" · ENGAGE · en-US.txt:530 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Lock and load! Here we go!" · ENGAGE · en-US.txt:531 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Form up! Fire!" · ENGAGE · en-US.txt:532 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Everyone fire! Now!" · ENGAGE · en-US.txt:533 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Eyes open! Shoot on sight!" · ENGAGE · en-US.txt:534 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Open fire! Don't let up!" · ENGAGE · en-US.txt:535 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Heads up, {Player}! Enemy incoming!" · ENGAGE · en-US.txt:536 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[early] Slimes ahead! Open fire!" · ENGAGE · en-US.txt:538 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[hardmode] Heavy hitters incoming! Open fire!" · ENGAGE · en-US.txt:539 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Okay! Okay! Fire! Fire!" · ENGAGE|Greenhorn · en-US.txt:540 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm ready! I think! Open fire!" · ENGAGE|Greenhorn · en-US.txt:541 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Here they come! I can do this!" · ENGAGE|Greenhorn · en-US.txt:542 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Enemy incoming! I'm not afraid!" · ENGAGE|Greenhorn · en-US.txt:543 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fighting's my job! Open fire!" · ENGAGE|Sellsword · en-US.txt:544 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Time to earn my pay!" · ENGAGE|Sellsword · en-US.txt:545 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "For the commander! Open fire!" · ENGAGE|Zealot · en-US.txt:548 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "For {Player}! For {Player}'s army! Fire!" · ENGAGE|Zealot · en-US.txt:549 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "No fear! Righteous fire!" · ENGAGE|Zealot · en-US.txt:550 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Strike them down!" · ENGAGE|Zealot · en-US.txt:551 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Here we go again!" · ENGAGE|Cynic · en-US.txt:552 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Of course they're back! Open fire!" · ENGAGE|Cynic · en-US.txt:553 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "More of them! Fire!" · ENGAGE|Cynic · en-US.txt:554 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Same fight, new monsters! Fire!" · ENGAGE|Cynic · en-US.txt:555 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Steady! Pick your targets!" · ENGAGE|Veteran · en-US.txt:556 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hold the line! Steady!" · ENGAGE|Veteran · en-US.txt:557 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Easy! Let them come!" · ENGAGE|Veteran · en-US.txt:558 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stay sharp! Open fire!" · ENGAGE|Veteran · en-US.txt:559 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight! Open fire!" · ENGAGE · en-US.txt:1295 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Back off! Open fire!" · ENGAGE · en-US.txt:1296 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not now! Get them!" · ENGAGE · en-US.txt:1297 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Come on, then! Fire!" · ENGAGE · en-US.txt:1298 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Under this moon? Fire!" · ENGAGE · en-US.txt:1299 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Get away from me! Open fire!" · ENGAGE · en-US.txt:1300 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Enough! Open fire!" · ENGAGE · en-US.txt:1301 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Enemy incoming! Great! Fire!" · ENGAGE · en-US.txt:1302 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Just die already! Fire!" · ENGAGE · en-US.txt:1303 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm in no mood! Fire!" · ENGAGE · en-US.txt:1304 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight, monsters! Fire!" · ENGAGE · en-US.txt:1305 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Try me! Open fire!" · ENGAGE · en-US.txt:1306 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Of course tonight! Fire!" · ENGAGE|Cynic · en-US.txt:1307 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Steady! Make it quick!" · ENGAGE|Veteran · en-US.txt:1308 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### KILL (40)
- "Got one!" · KILL · en-US.txt:561 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Die, monster!" · KILL · en-US.txt:562 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Take that!" · KILL · en-US.txt:563 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Down you go!" · KILL · en-US.txt:564 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "One less!" · KILL · en-US.txt:565 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Another one down!" · KILL · en-US.txt:566 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Stay down!" · KILL · en-US.txt:567 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Eat lead!" · KILL · en-US.txt:568 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "That's one!" · KILL · en-US.txt:569 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Target down!" · KILL · en-US.txt:570 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "That one's for {Comrade}!" · KILL · en-US.txt:571 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Mine! Beat that, {Glutton}!" · KILL · en-US.txt:572 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "It's down, {Player}! Got it!" · KILL · en-US.txt:573 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I got one! I actually got one!" · KILL|Greenhorn · en-US.txt:574 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Did you see that?! I got it!" · KILL|Greenhorn · en-US.txt:575 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "It's down! I did it!" · KILL|Greenhorn · en-US.txt:576 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That's one for the bounty!" · KILL|Sellsword · en-US.txt:578 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Money in the bank!" · KILL|Sellsword · en-US.txt:580 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Justice is served!" · KILL|Zealot · en-US.txt:581 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "For the commander!" · KILL|Zealot · en-US.txt:582 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Begone, foul thing!" · KILL|Zealot · en-US.txt:583 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Purified!" · KILL|Zealot · en-US.txt:584 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "One down. Plenty to go!" · KILL|Cynic · en-US.txt:585 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Dead! Next!" · KILL|Cynic · en-US.txt:586 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stay dead this time!" · KILL|Cynic · en-US.txt:587 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Clean shot! Next!" · KILL|Veteran · en-US.txt:588 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Steady work!" · KILL|Veteran · en-US.txt:589 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "That's how it's done!" · KILL|Veteran · en-US.txt:590 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Die already!" · KILL · en-US.txt:1310 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Finally! Stay dead!" · KILL · en-US.txt:1311 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] That's what you get!" · KILL · en-US.txt:1312 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] And stay down!" · KILL · en-US.txt:1313 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Good riddance!" · KILL · en-US.txt:1314 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] One less on my night!" · KILL · en-US.txt:1315 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Take that, you pest!" · KILL · en-US.txt:1316 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Gone! Next!" · KILL · en-US.txt:1317 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight, creep!" · KILL · en-US.txt:1318 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Dead! Who's next?" · KILL · en-US.txt:1319 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Got it! Now go away!" · KILL · en-US.txt:1320 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Serves you right!" · KILL · en-US.txt:1321 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### HURT (33)
- "I'm hit!" · HURT · en-US.txt:592 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Ow! That hurt!" · HURT · en-US.txt:593 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Argh! I'm hit!" · HURT · en-US.txt:594 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hit! Keep firing!" · HURT · en-US.txt:595 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Took one! I'm fine!" · HURT · en-US.txt:596 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Just a scratch!" · HURT · en-US.txt:597 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "They got me! Keep fighting!" · HURT · en-US.txt:598 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ugh! That's bad!" · HURT · en-US.txt:599 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hurt! Still fighting!" · HURT · en-US.txt:600 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Still standing!" · HURT · en-US.txt:601 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hit! I'm actually hit!" · HURT|Greenhorn · en-US.txt:602 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ow! Is that supposed to happen?!" · HURT|Greenhorn · en-US.txt:603 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "It hurts! I'm still fighting!" · HURT|Greenhorn · en-US.txt:604 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Pain is nothing! For the commander!" · HURT|Zealot · en-US.txt:608 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I will endure!" · HURT|Zealot · en-US.txt:609 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Still standing! Praise be!" · HURT|Zealot · en-US.txt:610 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Of course I'm hit!" · HURT|Cynic · en-US.txt:611 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Typical! I'm hit!" · HURT|Cynic · en-US.txt:612 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Still in it!" · HURT|Veteran · en-US.txt:614 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I've had worse! Keep firing!" · HURT|Veteran · en-US.txt:615 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Walk it off! Keep going!" · HURT|Veteran · en-US.txt:616 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ow! Not tonight!" · HURT · en-US.txt:1323 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hey! That hurt!" · HURT · en-US.txt:1324 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] You'll pay for that!" · HURT · en-US.txt:1325 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Argh! Rude!" · HURT · en-US.txt:1326 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ow! I'll get you!" · HURT · en-US.txt:1327 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] That hurt! Die!" · HURT · en-US.txt:1328 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hit me, will you?!" · HURT · en-US.txt:1329 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] I'm hit! I'm furious!" · HURT · en-US.txt:1330 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ow! Come here!" · HURT · en-US.txt:1331 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] You'll regret that!" · HURT · en-US.txt:1332 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Argh! Right in the ribs!" · HURT · en-US.txt:1333 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] That's it! No mercy!" · HURT · en-US.txt:1334 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### LOW_HP (24)
- "Low on health!" · LOW_HP · en-US.txt:618 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "I need healing!" · LOW_HP · en-US.txt:619 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Medic!" · LOW_HP · en-US.txt:620 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "I'm hurt bad!" · LOW_HP · en-US.txt:621 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Cover me! I'm weak!" · LOW_HP · en-US.txt:622 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Need a medic here!" · LOW_HP · en-US.txt:623 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "I can't take much more!" · LOW_HP · en-US.txt:624 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Health's critical!" · LOW_HP · en-US.txt:625 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Medic! {Nurse}, I need you!" · LOW_HP · en-US.txt:626 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Cover me, {Player}! I'm hurt bad!" · LOW_HP · en-US.txt:627 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm bleeding! Medic!" · LOW_HP|Greenhorn · en-US.txt:628 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Am I okay?! Medic!" · LOW_HP|Greenhorn · en-US.txt:629 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'll fight to my last breath!" · LOW_HP|Zealot · en-US.txt:631 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Need a medic! Cover me!" · LOW_HP|Veteran · en-US.txt:633 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Medic! Now!" · LOW_HP · en-US.txt:1336 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm hurt! Somebody help!" · LOW_HP · en-US.txt:1337 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Heal me! Now!" · LOW_HP · en-US.txt:1338 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm nearly dead! Not tonight!" · LOW_HP · en-US.txt:1339 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Where's the medic?!" · LOW_HP · en-US.txt:1340 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] I'm barely standing! Cover me!" · LOW_HP · en-US.txt:1341 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Low health! I'm furious!" · LOW_HP · en-US.txt:1342 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Help me! Hurry!" · LOW_HP · en-US.txt:1343 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Don't you dare finish me!" · LOW_HP · en-US.txt:1344 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] I need healing! Fast!" · LOW_HP · en-US.txt:1345 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OUT_OF_AMMO (21)
- "Reloading!" · OUT_OF_AMMO · en-US.txt:635 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Out of ammo!" · OUT_OF_AMMO · en-US.txt:636 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Empty! Reloading!" · OUT_OF_AMMO · en-US.txt:637 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Cover me! Reloading!" · OUT_OF_AMMO · en-US.txt:638 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm dry! Need ammo!" · OUT_OF_AMMO · en-US.txt:639 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Out of bullets!" · OUT_OF_AMMO · en-US.txt:640 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Magazine's empty!" · OUT_OF_AMMO · en-US.txt:641 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Out of ammo! {ArmsDealer}, I need more!" · OUT_OF_AMMO · en-US.txt:642 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm out! Where's more ammo?!" · OUT_OF_AMMO|Greenhorn · en-US.txt:643 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Out of ammo! Steel it is!" · OUT_OF_AMMO|Zealot · en-US.txt:646 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Reloading! Cover me!" · OUT_OF_AMMO|Veteran · en-US.txt:648 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Out of ammo! Seriously?!" · OUT_OF_AMMO · en-US.txt:1347 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Empty! Perfect!" · OUT_OF_AMMO · en-US.txt:1348 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Reloading! Don't move!" · OUT_OF_AMMO · en-US.txt:1349 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] No ammo! Not tonight!" · OUT_OF_AMMO · en-US.txt:1350 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Out! Cover me, quick!" · OUT_OF_AMMO · en-US.txt:1351 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Dry! Hold them off!" · OUT_OF_AMMO · en-US.txt:1352 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Out of bullets! I'm furious!" · OUT_OF_AMMO · en-US.txt:1353 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Reloading! Wait your turn!" · OUT_OF_AMMO · en-US.txt:1354 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Empty! Don't you dare!" · OUT_OF_AMMO · en-US.txt:1355 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ammo! Somebody! Now!" · OUT_OF_AMMO · en-US.txt:1356 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### HOLD_FIRE (4)
- "Holding fire!" · HOLD_FIRE · en-US.txt:650 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Cease fire! Standing down!" · HOLD_FIRE · en-US.txt:651 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Weapons down! Holding!" · HOLD_FIRE · en-US.txt:652 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fire held! Ready when you are!" · HOLD_FIRE · en-US.txt:653 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### COMRADE_HIT (23)
- "Someone's hit! Cover them!" · COMRADE_HIT · en-US.txt:655 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Medic! Over here!" · COMRADE_HIT · en-US.txt:656 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "We've got wounded! Hold the line!" · COMRADE_HIT · en-US.txt:657 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade} is hit! Medic!" · COMRADE_HIT · en-US.txt:658 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hang on, {Comrade}!" · COMRADE_HIT · en-US.txt:659 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "{Comrade}, stay with us!" · COMRADE_HIT · en-US.txt:660 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Keep fighting, {Comrade}!" · COMRADE_HIT · en-US.txt:661 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "{Comrade}'s hit! Cover {Comrade}!" · COMRADE_HIT · en-US.txt:662 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade}'s hit! Hang on!" · COMRADE_HIT|Greenhorn · en-US.txt:663 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade}! Hold on! {Comrade}, don't quit!" · COMRADE_HIT|Sellsword · en-US.txt:664 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Be strong, {Comrade}! We are with you!" · COMRADE_HIT|Zealot · en-US.txt:665 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade}'s hit! Stay low!" · COMRADE_HIT|Cynic · en-US.txt:666 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stay calm, {Comrade}! Heads down!" · COMRADE_HIT|Veteran · en-US.txt:667 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Somebody's hit! Cover them!" · COMRADE_HIT · en-US.txt:1358 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Comrade} is hit! Get them!" · COMRADE_HIT · en-US.txt:1359 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Leave {Comrade} alone!" · COMRADE_HIT · en-US.txt:1360 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Hold on, {Comrade}! I'm coming!" · COMRADE_HIT · en-US.txt:1361 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Comrade}, get down!" · COMRADE_HIT · en-US.txt:1362 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Who hit {Comrade}?! Die!" · COMRADE_HIT · en-US.txt:1363 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Medic! {Comrade}'s hit!" · COMRADE_HIT · en-US.txt:1364 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hang in there, {Comrade}!" · COMRADE_HIT · en-US.txt:1365 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] {Comrade}! Stay with me! {Comrade}, don't you dare!" · COMRADE_HIT · en-US.txt:1366 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hit {Comrade}, will you?! Pay for that!" · COMRADE_HIT · en-US.txt:1367 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_SIGHTED (23)
- "Big one! Everybody fire!" · BOSS_SIGHTED · en-US.txt:670 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Boss ahead! Focus fire!" · BOSS_SIGHTED · en-US.txt:671 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That's a big one! Stand firm!" · BOSS_SIGHTED · en-US.txt:672 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Boss on the field! Hit it hard!" · BOSS_SIGHTED · en-US.txt:673 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Here comes trouble! Open fire!" · BOSS_SIGHTED · en-US.txt:674 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Everything on the boss! Fire!" · BOSS_SIGHTED · en-US.txt:675 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit it with everything!" · BOSS_SIGHTED · en-US.txt:676 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Boss ahead, {Player}! We're ready!" · BOSS_SIGHTED · en-US.txt:677 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That's huge! I'm staying! Fire!" · BOSS_SIGHTED|Greenhorn · en-US.txt:678 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "For the commander! Bring it down!" · BOSS_SIGHTED|Zealot · en-US.txt:680 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Here we go again! Big one!" · BOSS_SIGHTED|Cynic · en-US.txt:681 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hold the line! Focus fire!" · BOSS_SIGHTED|Veteran · en-US.txt:682 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Boss! Not tonight!" · BOSS_SIGHTED · en-US.txt:1424 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] A boss? Fine! Open fire!" · BOSS_SIGHTED · en-US.txt:1425 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Great, a boss! Get it!" · BOSS_SIGHTED · en-US.txt:1426 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Big one! Make it quick!" · BOSS_SIGHTED · en-US.txt:1427 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Down, you oversized pest!" · BOSS_SIGHTED · en-US.txt:1428 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Boss ahead! Take it down!" · BOSS_SIGHTED · en-US.txt:1429 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight, boss! Fire!" · BOSS_SIGHTED · en-US.txt:1430 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Everything on the boss! Now!" · BOSS_SIGHTED · en-US.txt:1431 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Come on, boss! Try me!" · BOSS_SIGHTED · en-US.txt:1432 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Boss! Hit it hard!" · BOSS_SIGHTED · en-US.txt:1433 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Big target! Pay me extra!" · BOSS_SIGHTED|Sellsword · en-US.txt:1434 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_KingSlime (4)
- "Slime king! Hit the crown!" · BOSS_KingSlime · en-US.txt:684 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Big jelly! Open fire!" · BOSS_KingSlime · en-US.txt:685 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Aim for the king! Fire!" · BOSS_KingSlime · en-US.txt:686 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Bring down the slime king!" · BOSS_KingSlime · en-US.txt:687 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_EyeofCthulhu (4)
- "Giant eye! Shoot it!" · BOSS_EyeofCthulhu · en-US.txt:689 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Eye in the sky! Open fire!" · BOSS_EyeofCthulhu · en-US.txt:690 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch its charge! Fire!" · BOSS_EyeofCthulhu · en-US.txt:691 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Take out that eye!" · BOSS_EyeofCthulhu · en-US.txt:692 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_EaterofWorldsHead (4)
- "Giant worm! Cut it down!" · BOSS_EaterofWorldsHead · en-US.txt:694 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit every segment! Fire!" · BOSS_EaterofWorldsHead · en-US.txt:695 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "It's coming up! Open fire!" · BOSS_EaterofWorldsHead · en-US.txt:696 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Chop it to pieces!" · BOSS_EaterofWorldsHead · en-US.txt:697 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_BrainofCthulhu (4)
- "Giant brain! Open fire!" · BOSS_BrainofCthulhu · en-US.txt:699 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the clones! Fire!" · BOSS_BrainofCthulhu · en-US.txt:700 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Crush that brain!" · BOSS_BrainofCthulhu · en-US.txt:701 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Stay sharp! It's tricky! Fire!" · BOSS_BrainofCthulhu · en-US.txt:702 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_QueenBee (4)
- "Queen bee! Open fire!" · BOSS_QueenBee · en-US.txt:704 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Swat the queen!" · BOSS_QueenBee · en-US.txt:705 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Watch the stingers! Fire!" · BOSS_QueenBee · en-US.txt:706 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Bring down the queen!" · BOSS_QueenBee · en-US.txt:707 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_Deerclops (4)
- "Deerclops! Hold the line!" · BOSS_Deerclops · en-US.txt:709 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Big beast! Open fire!" · BOSS_Deerclops · en-US.txt:710 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the ice! Fire!" · BOSS_Deerclops · en-US.txt:711 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Bring it down! Fire!" · BOSS_Deerclops · en-US.txt:712 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_SkeletronHead (4)
- "Skeletron! Smash that skull!" · BOSS_SkeletronHead · en-US.txt:714 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Giant skull! Open fire!" · BOSS_SkeletronHead · en-US.txt:715 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Break its bones! Fire!" · BOSS_SkeletronHead · en-US.txt:716 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit the head! Hit the hands!" · BOSS_SkeletronHead · en-US.txt:717 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_WallofFlesh (4)
- "Wall of flesh! Open fire!" · BOSS_WallofFlesh · en-US.txt:719 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hold the line! Burn it!" · BOSS_WallofFlesh · en-US.txt:720 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Keep firing! It's closing in!" · BOSS_WallofFlesh · en-US.txt:721 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Guide} warned us! Hit the wall!" · BOSS_WallofFlesh · en-US.txt:722 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_QueenSlimeBoss (4)
- "Queen slime! Open fire!" · BOSS_QueenSlimeBoss · en-US.txt:724 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Shatter the crown!" · BOSS_QueenSlimeBoss · en-US.txt:725 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Big queen! Hit hard!" · BOSS_QueenSlimeBoss · en-US.txt:726 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stay on your feet! Fire!" · BOSS_QueenSlimeBoss · en-US.txt:727 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_Retinazer (3)
- "Mechanical eye! Open fire!" · BOSS_Retinazer · en-US.txt:729 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the laser! Fire!" · BOSS_Retinazer · en-US.txt:730 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Bring down the twins!" · BOSS_Retinazer · en-US.txt:731 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_Spazmatism (3)
- "Twin eye! Open fire!" · BOSS_Spazmatism · en-US.txt:733 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the flames! Fire!" · BOSS_Spazmatism · en-US.txt:734 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Take down the twins!" · BOSS_Spazmatism · en-US.txt:735 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_TheDestroyer (3)
- "Giant machine! Open fire!" · BOSS_TheDestroyer · en-US.txt:737 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Break the machine! Fire!" · BOSS_TheDestroyer · en-US.txt:738 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit every segment! Keep firing!" · BOSS_TheDestroyer · en-US.txt:739 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_SkeletronPrime (3)
- "Metal skull! Open fire!" · BOSS_SkeletronPrime · en-US.txt:741 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Break its arms! Fire!" · BOSS_SkeletronPrime · en-US.txt:742 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Bring down the machine!" · BOSS_SkeletronPrime · en-US.txt:743 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_Plantera (3)
- "Giant flower! Burn it!" · BOSS_Plantera · en-US.txt:745 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Cut it down! Fire!" · BOSS_Plantera · en-US.txt:746 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the vines! Open fire!" · BOSS_Plantera · en-US.txt:747 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_Golem (3)
- "Stone giant! Open fire!" · BOSS_Golem · en-US.txt:749 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Smash it! Fire!" · BOSS_Golem · en-US.txt:750 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit the head! Fire!" · BOSS_Golem · en-US.txt:751 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_HallowBoss (3)
- "Blinding light! Open fire!" · BOSS_HallowBoss · en-US.txt:753 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the beams! Fire!" · BOSS_HallowBoss · en-US.txt:754 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Bring it down! Open fire!" · BOSS_HallowBoss · en-US.txt:755 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_DukeFishron (3)
- "Giant fish! Open fire!" · BOSS_DukeFishron · en-US.txt:757 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Reel it in! Fire!" · BOSS_DukeFishron · en-US.txt:758 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Watch the charge! Fire!" · BOSS_DukeFishron · en-US.txt:759 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### BOSS_CultistBoss (3)
- "Cultist! Open fire!" · BOSS_CultistBoss · en-US.txt:761 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stop the ritual! Fire!" · BOSS_CultistBoss · en-US.txt:762 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Take out the cultist!" · BOSS_CultistBoss · en-US.txt:763 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### BOSS_MoonLordCore (3)
- "Moon Lord! Hold the line!" · BOSS_MoonLordCore · en-US.txt:765 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit the eyes! Open fire!" · BOSS_MoonLordCore · en-US.txt:766 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Everything we've got! Fire!" · BOSS_MoonLordCore · en-US.txt:767 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### COMRADE_DOWN (37)
- "Man down!" · COMRADE_DOWN · en-US.txt:770 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Soldier down! Medic!" · COMRADE_DOWN · en-US.txt:771 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Someone's down! Cover them!" · COMRADE_DOWN · en-US.txt:772 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade} is down! Medic!" · COMRADE_DOWN · en-US.txt:773 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade} is down! Get {Comrade} out!" · COMRADE_DOWN · en-US.txt:774 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade}! Stay with me! {Comrade}!" · COMRADE_DOWN · en-US.txt:775 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Avenge {Comrade}! Open fire! {Comrade} fell for this!" · COMRADE_DOWN · en-US.txt:776 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Tell {Player}! {Comrade} is down!" · COMRADE_DOWN · en-US.txt:777 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Someone's down! Oh no! Medic!" · COMRADE_DOWN|Greenhorn · en-US.txt:778 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade} is down! What do we do?!" · COMRADE_DOWN|Greenhorn · en-US.txt:779 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Get up, {Comrade}! Please get up!" · COMRADE_DOWN|Greenhorn · en-US.txt:780 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "No! {Comrade}! Medic!" · COMRADE_DOWN|Greenhorn · en-US.txt:781 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Soldier down! Somebody help!" · COMRADE_DOWN|Sellsword · en-US.txt:782 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade}'s down! They'll pay for this!" · COMRADE_DOWN|Sellsword · en-US.txt:783 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Not {Comrade}! Make them pay!" · COMRADE_DOWN|Sellsword · en-US.txt:784 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade}, hold on! Medic!" · COMRADE_DOWN|Sellsword · en-US.txt:785 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "A soldier has fallen! Avenge them!" · COMRADE_DOWN|Zealot · en-US.txt:786 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fight on for the fallen!" · COMRADE_DOWN|Zealot · en-US.txt:787 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "{Comrade} has fallen! We will avenge you!" · COMRADE_DOWN|Zealot · en-US.txt:788 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Rest well, {Comrade}! For the commander!" · COMRADE_DOWN|Zealot · en-US.txt:789 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Another one down! Keep firing!" · COMRADE_DOWN|Cynic · en-US.txt:790 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Down again! Stay sharp!" · COMRADE_DOWN|Cynic · en-US.txt:791 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade} is down! Make it count!" · COMRADE_DOWN|Cynic · en-US.txt:792 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Damn it, {Comrade}!" · COMRADE_DOWN|Cynic · en-US.txt:793 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Soldier down! Keep it tight!" · COMRADE_DOWN|Veteran · en-US.txt:794 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Comrade}'s down! Steady! Keep fighting!" · COMRADE_DOWN|Veteran · en-US.txt:795 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Cover {Comrade}! Drag them back!" · COMRADE_DOWN|Veteran · en-US.txt:796 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Man down! Not tonight!" · COMRADE_DOWN · en-US.txt:1369 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Someone's down! Get them!" · COMRADE_DOWN · en-US.txt:1370 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] No! Not on this night!" · COMRADE_DOWN · en-US.txt:1371 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Comrade} is down! Make them pay!" · COMRADE_DOWN · en-US.txt:1372 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Get up, {Comrade}! Get up!" · COMRADE_DOWN · en-US.txt:1373 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Comrade}! No! Medic!" · COMRADE_DOWN · en-US.txt:1374 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] You'll pay for {Comrade}!" · COMRADE_DOWN · en-US.txt:1375 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] {Comrade} fell! I'm furious!" · COMRADE_DOWN · en-US.txt:1376 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hold on, {Comrade}! I'm coming!" · COMRADE_DOWN · en-US.txt:1377 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Soldier down, {Player}! Make them pay!" · COMRADE_DOWN · en-US.txt:1378 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### SHAKEN (25)
- "I'm rattled! Keep firing!" · SHAKEN · en-US.txt:798 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hold it together!" · SHAKEN · en-US.txt:799 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Steady! Steady!" · SHAKEN · en-US.txt:800 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I can do this! I can do this!" · SHAKEN · en-US.txt:801 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stay calm! Stay in the fight!" · SHAKEN · en-US.txt:802 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Don't back down!" · SHAKEN · en-US.txt:803 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Keep it together! Fire!" · SHAKEN · en-US.txt:804 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Easy! Breathe! Fire!" · SHAKEN · en-US.txt:805 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Stay with me, {Comrade}! Don't run!" · SHAKEN · en-US.txt:806 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm scared! I'm still here!" · SHAKEN|Greenhorn · en-US.txt:807 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "It's too hot here! Hold your ground!" · SHAKEN|Sellsword · en-US.txt:808 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Faith! Hold fast!" · SHAKEN|Zealot · en-US.txt:809 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fear is nothing! Stand firm!" · SHAKEN|Zealot · en-US.txt:810 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Knew this was a bad idea! Hold!" · SHAKEN|Cynic · en-US.txt:811 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Steady! Nobody runs!" · SHAKEN|Veteran · en-US.txt:812 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight! Hold it together!" · SHAKEN · en-US.txt:1380 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm shaken! Still fighting!" · SHAKEN · en-US.txt:1381 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Steady! Don't back down!" · SHAKEN · en-US.txt:1382 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Keep it together!" · SHAKEN · en-US.txt:1383 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Easy now! Easy!" · SHAKEN · en-US.txt:1384 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hold on! Don't run!" · SHAKEN · en-US.txt:1385 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm not scared! Just angry!" · SHAKEN · en-US.txt:1386 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Get a grip! Fire!" · SHAKEN · en-US.txt:1387 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hold the line! Hold it!" · SHAKEN · en-US.txt:1388 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Stay calm! Stay mad!" · SHAKEN · en-US.txt:1389 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### ROUTED (24)
- "Fall back! Fall back!" · ROUTED · en-US.txt:814 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Retreat!" · ROUTED · en-US.txt:815 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Run! Run!" · ROUTED · en-US.txt:816 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Get out of here!" · ROUTED · en-US.txt:817 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Pull back!" · ROUTED · en-US.txt:818 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Save yourselves!" · ROUTED · en-US.txt:819 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Back! Back!" · ROUTED · en-US.txt:820 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Withdraw! Withdraw!" · ROUTED · en-US.txt:821 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Sorry, {Player}! Fall back!" · ROUTED · en-US.txt:822 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Retreat, {Player}! Fall back!" · ROUTED · en-US.txt:823 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Run! Everybody run!" · ROUTED|Greenhorn · en-US.txt:824 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Forgive me, commander! Retreat!" · ROUTED|Zealot · en-US.txt:826 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Knew it! Fall back!" · ROUTED|Cynic · en-US.txt:827 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fall back! Regroup! Regroup!" · ROUTED|Veteran · en-US.txt:828 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Retreat! Not tonight!" · ROUTED · en-US.txt:1391 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Pull back! Pull back!" · ROUTED · en-US.txt:1392 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm out of here!" · ROUTED · en-US.txt:1393 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Fall back! Fall back!" · ROUTED · en-US.txt:1394 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Run! Regroup!" · ROUTED · en-US.txt:1395 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Get out! Go go go!" · ROUTED · en-US.txt:1396 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] This isn't over! Retreat!" · ROUTED · en-US.txt:1397 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Back off! Back!" · ROUTED · en-US.txt:1398 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Withdraw! Now!" · ROUTED · en-US.txt:1399 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Fall back, {Player}! Regroup!" · ROUTED · en-US.txt:1400 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### RALLIED (22)
- "Back in the fight!" · RALLIED · en-US.txt:830 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Rally! Rally!" · RALLIED · en-US.txt:831 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Let's go again!" · RALLIED · en-US.txt:832 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Reform the line!" · RALLIED · en-US.txt:833 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "I'm back! Fire!" · RALLIED · en-US.txt:834 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "On me! Back to it!" · RALLIED · en-US.txt:835 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Steady now! Back in it!" · RALLIED · en-US.txt:836 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Never again! Let's fight!" · RALLIED · en-US.txt:837 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Together! Charge!" · RALLIED · en-US.txt:838 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm okay! I'm ready!" · RALLIED|Greenhorn · en-US.txt:839 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Faith restored! Charge!" · RALLIED|Zealot · en-US.txt:841 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Form up! Hold the line!" · RALLIED|Veteran · en-US.txt:842 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Enough running! Fight!" · RALLIED · en-US.txt:1402 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm back! Now I'm angry!" · RALLIED · en-US.txt:1403 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Rally! Let's end this!" · RALLIED · en-US.txt:1404 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Back in it! Not tonight!" · RALLIED · en-US.txt:1405 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] That's it! Fight back!" · RALLIED · en-US.txt:1406 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Reform! Get them!" · RALLIED · en-US.txt:1407 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] No more running!" · RALLIED · en-US.txt:1408 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Let's finish this!" · RALLIED · en-US.txt:1409 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] I'm done being scared! Fire!" · RALLIED · en-US.txt:1410 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Back at them! All of you!" · RALLIED · en-US.txt:1411 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### VICTORY (22)
- "Victory!" · VICTORY · en-US.txt:844 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "We won!" · VICTORY · en-US.txt:845 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "That's how it's done!" · VICTORY · en-US.txt:846 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "We held the line!" · VICTORY · en-US.txt:847 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "They're finished! Victory!" · VICTORY · en-US.txt:848 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Enemy down! Great work!" · VICTORY · en-US.txt:849 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Well fought, everyone!" · VICTORY · en-US.txt:850 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Victory, {Player}! Great work!" · VICTORY · en-US.txt:851 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "We won! I survived!" · VICTORY|Greenhorn · en-US.txt:852 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Praise be! Glory to {Player}!" · VICTORY|Zealot · en-US.txt:854 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "We won! Don't relax yet!" · VICTORY|Cynic · en-US.txt:855 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Good work! Stay sharp!" · VICTORY|Veteran · en-US.txt:856 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Victory! About time!" · VICTORY · en-US.txt:1413 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] We won! Now leave me alone!" · VICTORY · en-US.txt:1414 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] That's right! Stay down!" · VICTORY · en-US.txt:1415 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Done! Good riddance!" · VICTORY · en-US.txt:1416 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] We did it! Now let me sleep!" · VICTORY · en-US.txt:1417 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Take that, moon!" · VICTORY · en-US.txt:1418 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Dead! All of them!" · VICTORY · en-US.txt:1419 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Finally! We won!" · VICTORY · en-US.txt:1420 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight, monsters!" · VICTORY · en-US.txt:1421 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] That's the end of that!" · VICTORY · en-US.txt:1422 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### MUSTER (7)
- "Falling in!" · MUSTER · en-US.txt:870 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Reporting for duty!" · MUSTER · en-US.txt:871 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "On my way! Ready!" · MUSTER · en-US.txt:872 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ready and willing!" · MUSTER · en-US.txt:873 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Rally up! Let's go!" · MUSTER · en-US.txt:874 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Here! Ready to fight!" · MUSTER · en-US.txt:875 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Answering the call!" · MUSTER · en-US.txt:876 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### STAND_DOWN (5)
- "Standing down!" · STAND_DOWN · en-US.txt:878 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Stand down! Good work!" · STAND_DOWN · en-US.txt:879 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fall out! Well done!" · STAND_DOWN · en-US.txt:880 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Weapons down! We held!" · STAND_DOWN · en-US.txt:881 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Dismissed! Great work!" · STAND_DOWN · en-US.txt:882 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### PASTIME_NIGHT (3)
- "There's a whole sky up there and most of it isn't trying to kill me. I'm taking a minute." · PASTIME_NIGHT · en-US.txt:945 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]; style: setup + short punchline
- "I haven't looked up in weeks. {Comrade}, why did nobody say there's this much of it?" · PASTIME_NIGHT · en-US.txt:946 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]
- "Stay out here with me a little, {Comrade}. The beds aren't going anywhere." · PASTIME_NIGHT · en-US.txt:949 · AI-WRITTEN · agent-written night pastime lines [b3ce5a5]; style: setup + short punchline

### MUTINY_BEGIN (1)
- "MUTINY! PAY US!" · MUTINY_BEGIN · en-US.txt:1002 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline

### MUTINY (4)
- "Pay up or bleed!" · MUTINY · en-US.txt:1003 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "We're taking what we're owed!" · MUTINY · en-US.txt:1004 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "No coin, no orders!" · MUTINY · en-US.txt:1005 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]
- "Your gold or your head!" · MUTINY · en-US.txt:1006 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]

### MUTINY_REFUSE (1)
- "No orders! Not until we're paid!" · MUTINY_REFUSE · en-US.txt:1007 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline

### MUTINY_CALM (1)
- "Paid. Back to work." · MUTINY_CALM · en-US.txt:1008 · AI-WRITTEN · dialogue draft 2, agent rewrite "for Eve's review" [968dc58]; style: setup + short punchline

### OFFICER_CHAT_FIGHT (6)
- "Not now! Open fire!" · OFFICER_CHAT_FIGHT · en-US.txt:1564 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Talk fast! Enemy's on us!" · OFFICER_CHAT_FIGHT · en-US.txt:1565 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fighting here! Make it quick!" · OFFICER_CHAT_FIGHT · en-US.txt:1566 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Make it quick, {Player}! Hold the line!" · OFFICER_CHAT_FIGHT · en-US.txt:1567 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fight first! Talk later!" · OFFICER_CHAT_FIGHT · en-US.txt:1568 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fire! Whatever you want can wait!" · OFFICER_CHAT_FIGHT · en-US.txt:1569 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_CHAT_BOSS (5)
- "That's {Boss}! Everybody fire!" · OFFICER_CHAT_BOSS · en-US.txt:1570 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss} is here! Hit it hard!" · OFFICER_CHAT_BOSS · en-US.txt:1571 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! Give it everything!" · OFFICER_CHAT_BOSS · en-US.txt:1572 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Focus on {Boss}! Bring it down!" · OFFICER_CHAT_BOSS · en-US.txt:1573 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "We can take {Boss}! Open fire!" · OFFICER_CHAT_BOSS · en-US.txt:1574 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_CHAT_HURT (5)
- "I'm hit! Still fighting!" · OFFICER_CHAT_HURT · en-US.txt:1575 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ow! I'm fine! Keep going!" · OFFICER_CHAT_HURT · en-US.txt:1576 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Just a scratch! Fire!" · OFFICER_CHAT_HURT · en-US.txt:1577 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'll hold! Keep firing!" · OFFICER_CHAT_HURT · en-US.txt:1578 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hurt! Not out!" · OFFICER_CHAT_HURT · en-US.txt:1579 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_CHAT_VICTORY (5)
- "We won! Well fought!" · OFFICER_CHAT_VICTORY · en-US.txt:1580 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That's how it's done!" · OFFICER_CHAT_VICTORY · en-US.txt:1581 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Great work, {Player}! We won!" · OFFICER_CHAT_VICTORY · en-US.txt:1582 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Victory! Stay sharp!" · OFFICER_CHAT_VICTORY · en-US.txt:1583 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Another one down! Good work!" · OFFICER_CHAT_VICTORY · en-US.txt:1584 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Nurse_FIGHT (7)
- "Not now! Take cover! Fire!" · OFFICER_Nurse_FIGHT · en-US.txt:1618 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Talking means you're alive! Fight!" · OFFICER_Nurse_FIGHT · en-US.txt:1619 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Heads down! I'll patch you after!" · OFFICER_Nurse_FIGHT · en-US.txt:1620 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fight now, bandage later!" · OFFICER_Nurse_FIGHT · en-US.txt:1621 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] Not tonight! Duck and fight!" · OFFICER_Nurse_FIGHT · en-US.txt:2091 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Fight now! I'm in no mood!" · OFFICER_Nurse_FIGHT · en-US.txt:2092 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Out of my way! Fire!" · OFFICER_Nurse_FIGHT · en-US.txt:2093 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Nurse_BOSS (7)
- "{Boss}! Bring bandages! Fight!" · OFFICER_Nurse_BOSS · en-US.txt:1622 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! I'll patch you up! Go!" · OFFICER_Nurse_BOSS · en-US.txt:1623 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit {Boss}! I've got your back!" · OFFICER_Nurse_BOSS · en-US.txt:1624 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Everything bleeds! Hit {Boss}!" · OFFICER_Nurse_BOSS · en-US.txt:1625 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss}! Tonight! Fine! Fire!" · OFFICER_Nurse_BOSS · en-US.txt:2094 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss}! I hate this night! Hit it!" · OFFICER_Nurse_BOSS · en-US.txt:2095 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hit {Boss}! Hard! Now!" · OFFICER_Nurse_BOSS · en-US.txt:2096 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Nurse_HURT (6)
- "I'm hit! Keep fighting!" · OFFICER_Nurse_HURT · en-US.txt:1626 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ow! I'll patch myself!" · OFFICER_Nurse_HURT · en-US.txt:1627 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Back up! I'm fine!" · OFFICER_Nurse_HURT · en-US.txt:1628 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm hurt! I'm angry! Fight!" · OFFICER_Nurse_HURT · en-US.txt:2097 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Don't touch me! Keep firing!" · OFFICER_Nurse_HURT · en-US.txt:2098 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ow! Fine! Keep going!" · OFFICER_Nurse_HURT · en-US.txt:2099 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Nurse_VICTORY (5)
- "Good work! Come see me!" · OFFICER_Nurse_VICTORY · en-US.txt:1630 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Victory! Wounded, over here!" · OFFICER_Nurse_VICTORY · en-US.txt:1631 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] It's dead! About time!" · OFFICER_Nurse_VICTORY · en-US.txt:2100 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] We won! I still hate this!" · OFFICER_Nurse_VICTORY · en-US.txt:2101 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Good! Now leave me alone!" · OFFICER_Nurse_VICTORY · en-US.txt:2102 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Guide_FIGHT (4)
- "Not now, {Player}! Hit them hard!" · OFFICER_Guide_FIGHT · en-US.txt:1646 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit them first! That's my tip!" · OFFICER_Guide_FIGHT · en-US.txt:1647 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Keep firing! Stay alive!" · OFFICER_Guide_FIGHT · en-US.txt:1648 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Guiding bolt! Take that!" · OFFICER_Guide_FIGHT · en-US.txt:1649 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Guide_BOSS (4)
- "That's {Boss}! Stay clear of the big part! Fire!" · OFFICER_Guide_BOSS · en-US.txt:1650 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! Everything you've got! Fire!" · OFFICER_Guide_BOSS · en-US.txt:1651 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Don't give up! Hit {Boss} hard!" · OFFICER_Guide_BOSS · en-US.txt:1652 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss} is here! Together now!" · OFFICER_Guide_BOSS · en-US.txt:1653 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Guide_HURT (3)
- "Ow! I'm okay! Keep fighting!" · OFFICER_Guide_HURT · en-US.txt:1654 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hit! Not out! Fire!" · OFFICER_Guide_HURT · en-US.txt:1655 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That hurt! Stay in the fight!" · OFFICER_Guide_HURT · en-US.txt:1656 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Guide_VICTORY (3)
- "We did it! Great work!" · OFFICER_Guide_VICTORY · en-US.txt:1657 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Great job, {Player}! Well done!" · OFFICER_Guide_VICTORY · en-US.txt:1658 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "It's over! We won!" · OFFICER_Guide_VICTORY · en-US.txt:1659 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Merchant_FIGHT (3)
- "Shop's closed! Fighting now!" · OFFICER_Merchant_FIGHT · en-US.txt:1674 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Grab a weapon and fight!" · OFFICER_Merchant_FIGHT · en-US.txt:1675 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Stand back! I'm throwing everything!" · OFFICER_Merchant_FIGHT · en-US.txt:1677 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Merchant_BOSS (3)
- "{Boss}! Biggest threat yet! Fire!" · OFFICER_Merchant_BOSS · en-US.txt:1678 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! Make it count!" · OFFICER_Merchant_BOSS · en-US.txt:1680 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Take down {Boss}! The loot's ours!" · OFFICER_Merchant_BOSS · en-US.txt:1681 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Merchant_HURT (2)
- "I'm hit! Keep fighting!" · OFFICER_Merchant_HURT · en-US.txt:1683 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Wounded! Still in it!" · OFFICER_Merchant_HURT · en-US.txt:1684 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Merchant_VICTORY (1)
- "Great fight! The profit's ours!" · OFFICER_Merchant_VICTORY · en-US.txt:1687 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_ArmsDealer_FIGHT (4)
- "Open fire! Keep it loud!" · OFFICER_ArmsDealer_FIGHT · en-US.txt:1702 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Reloading! Cover me!" · OFFICER_ArmsDealer_FIGHT · en-US.txt:1703 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Heads down! Guns up!" · OFFICER_ArmsDealer_FIGHT · en-US.txt:1704 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Get down! I'm shooting!" · OFFICER_ArmsDealer_FIGHT · en-US.txt:1705 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_ArmsDealer_BOSS (4)
- "{Boss}! Finally, a real target!" · OFFICER_ArmsDealer_BOSS · en-US.txt:1706 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss} is huge! Can't miss! Fire!" · OFFICER_ArmsDealer_BOSS · en-US.txt:1707 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Plenty of ammo for {Boss}! Fire!" · OFFICER_ArmsDealer_BOSS · en-US.txt:1708 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Three clips for {Boss}! Open fire!" · OFFICER_ArmsDealer_BOSS · en-US.txt:1709 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_ArmsDealer_HURT (3)
- "I'm hit! I can still shoot!" · OFFICER_ArmsDealer_HURT · en-US.txt:1710 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Winged me! Keep firing!" · OFFICER_ArmsDealer_HURT · en-US.txt:1711 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ow! Shooting back!" · OFFICER_ArmsDealer_HURT · en-US.txt:1712 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_ArmsDealer_VICTORY (2)
- "That's a win! Great shooting!" · OFFICER_ArmsDealer_VICTORY · en-US.txt:1713 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Down it goes! Good work!" · OFFICER_ArmsDealer_VICTORY · en-US.txt:1714 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Demolitionist_FIGHT (4)
- "Fire in the hole!" · OFFICER_Demolitionist_FIGHT · en-US.txt:1730 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Stand back! Boom!" · OFFICER_Demolitionist_FIGHT · en-US.txt:1731 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Here comes the boom!" · OFFICER_Demolitionist_FIGHT · en-US.txt:1732 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Light it up! Fire!" · OFFICER_Demolitionist_FIGHT · en-US.txt:1733 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Demolitionist_BOSS (4)
- "{Boss}! I brought bigger bombs!" · OFFICER_Demolitionist_BOSS · en-US.txt:1734 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "A boss! Blow it up!" · OFFICER_Demolitionist_BOSS · en-US.txt:1735 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! Everyone clear! Boom!" · OFFICER_Demolitionist_BOSS · en-US.txt:1736 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hold {Boss} still! I'll blow it up!" · OFFICER_Demolitionist_BOSS · en-US.txt:1737 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Demolitionist_HURT (3)
- "Ow! I'm okay! Boom time!" · OFFICER_Demolitionist_HURT · en-US.txt:1738 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Still throwing!" · OFFICER_Demolitionist_HURT · en-US.txt:1739 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That hurt! More bombs!" · OFFICER_Demolitionist_HURT · en-US.txt:1740 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Demolitionist_VICTORY (3)
- "Boom! We did it!" · OFFICER_Demolitionist_VICTORY · en-US.txt:1741 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Great blast! We won!" · OFFICER_Demolitionist_VICTORY · en-US.txt:1742 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That's how you do it! Hah!" · OFFICER_Demolitionist_VICTORY · en-US.txt:1743 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_GoblinTinkerer_FIGHT (4)
- "Take that! Spiked balls!" · OFFICER_GoblinTinkerer_FIGHT · en-US.txt:1758 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Everyone fire! Quick!" · OFFICER_GoblinTinkerer_FIGHT · en-US.txt:1759 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm a tinkerer, not a soldier! Take that!" · OFFICER_GoblinTinkerer_FIGHT · en-US.txt:1760 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Spikes away! Duck!" · OFFICER_GoblinTinkerer_FIGHT · en-US.txt:1761 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_GoblinTinkerer_BOSS (4)
- "{Boss}! Too big! Hit it anyway!" · OFFICER_GoblinTinkerer_BOSS · en-US.txt:1762 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! I'll tinker it to pieces!" · OFFICER_GoblinTinkerer_BOSS · en-US.txt:1763 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Careful of {Boss}! Stay alive!" · OFFICER_GoblinTinkerer_BOSS · en-US.txt:1764 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Take it down! Everything we've got!" · OFFICER_GoblinTinkerer_BOSS · en-US.txt:1765 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_GoblinTinkerer_HURT (3)
- "Ow! I'm okay! Still tinkering!" · OFFICER_GoblinTinkerer_HURT · en-US.txt:1766 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Bandage! Quick!" · OFFICER_GoblinTinkerer_HURT · en-US.txt:1767 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm sturdy! Keep going!" · OFFICER_GoblinTinkerer_HURT · en-US.txt:1768 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_GoblinTinkerer_VICTORY (3)
- "We did it! Great work!" · OFFICER_GoblinTinkerer_VICTORY · en-US.txt:1769 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Wow! It worked!" · OFFICER_GoblinTinkerer_VICTORY · en-US.txt:1770 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Victory! Time to build!" · OFFICER_GoblinTinkerer_VICTORY · en-US.txt:1771 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Princess_FIGHT (7)
- "Get them, {Player}! Go, go, go!" · OFFICER_Princess_FIGHT · en-US.txt:1786 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Chin up, everyone! You're doing wonderfully!" · OFFICER_Princess_FIGHT · en-US.txt:1787 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm cheering for you! Fire!" · OFFICER_Princess_FIGHT · en-US.txt:1788 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "That's my army! Fight!" · OFFICER_Princess_FIGHT · en-US.txt:1789 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight! Make it stop!" · OFFICER_Princess_FIGHT · en-US.txt:2157 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm furious! Get them!" · OFFICER_Princess_FIGHT · en-US.txt:2158 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Be quick! And be rude to them!" · OFFICER_Princess_FIGHT · en-US.txt:2159 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Princess_BOSS (7)
- "{Boss}! Don't be afraid! Fight!" · OFFICER_Princess_BOSS · en-US.txt:1790 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "We can do this! Take down {Boss}!" · OFFICER_Princess_BOSS · en-US.txt:1791 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Player}, {Boss} is nothing! Fight!" · OFFICER_Princess_BOSS · en-US.txt:1792 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Brave hearts! Strike {Boss}!" · OFFICER_Princess_BOSS · en-US.txt:1793 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss}! Tonight! Get it!" · OFFICER_Princess_BOSS · en-US.txt:2160 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss}! Be rude to it! Fire!" · OFFICER_Princess_BOSS · en-US.txt:2161 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss} wasn't invited! Strike!" · OFFICER_Princess_BOSS · en-US.txt:2162 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Princess_HURT (6)
- "Ow! I'm okay! Carry on!" · OFFICER_Princess_HURT · en-US.txt:1794 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm still smiling! Keep fighting!" · OFFICER_Princess_HURT · en-US.txt:1795 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Don't stop!" · OFFICER_Princess_HURT · en-US.txt:1796 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ow! Rude! You'll pay!" · OFFICER_Princess_HURT · en-US.txt:2163 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm hurt! I'm furious! Fight!" · OFFICER_Princess_HURT · en-US.txt:2164 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Don't fuss! Keep fighting!" · OFFICER_Princess_HURT · en-US.txt:2165 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Princess_VICTORY (5)
- "Hooray! We won!" · OFFICER_Princess_VICTORY · en-US.txt:1797 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Splendid! Well fought!" · OFFICER_Princess_VICTORY · en-US.txt:1799 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] We won! Well done! Finally!" · OFFICER_Princess_VICTORY · en-US.txt:2166 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Splendid! Now let me rest!" · OFFICER_Princess_VICTORY · en-US.txt:2167 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Fine! Good job! Go home!" · OFFICER_Princess_VICTORY · en-US.txt:2168 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Dryad_FIGHT (7)
- "The land fights back! Strike!" · OFFICER_Dryad_FIGHT · en-US.txt:1814 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Strike what I mark! Now!" · OFFICER_Dryad_FIGHT · en-US.txt:1815 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hold the roots! Fight!" · OFFICER_Dryad_FIGHT · en-US.txt:1816 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Quick, {Player}! Defend the land!" · OFFICER_Dryad_FIGHT · en-US.txt:1817 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Strike fast! No mercy tonight!" · OFFICER_Dryad_FIGHT · en-US.txt:2113 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] The moon stirs everything! Strike!" · OFFICER_Dryad_FIGHT · en-US.txt:2114 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Strike what I mark! Now!" · OFFICER_Dryad_FIGHT · en-US.txt:2115 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Dryad_BOSS (7)
- "{Boss}! End it! Strike!" · OFFICER_Dryad_BOSS · en-US.txt:1818 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "The land fears {Boss}! Be brave!" · OFFICER_Dryad_BOSS · en-US.txt:1819 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "No purifying {Boss}! Strike it down!" · OFFICER_Dryad_BOSS · en-US.txt:1820 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Be brave, {Player}! Strike {Boss}!" · OFFICER_Dryad_BOSS · en-US.txt:1821 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss}! Tonight! Strike it!" · OFFICER_Dryad_BOSS · en-US.txt:2116 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss} is nothing! Strike it down!" · OFFICER_Dryad_BOSS · en-US.txt:2117 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hit {Boss} hard! Repeatedly!" · OFFICER_Dryad_BOSS · en-US.txt:2118 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Dryad_HURT (6)
- "Sap and bark! Still standing!" · OFFICER_Dryad_HURT · en-US.txt:1822 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "A wound! I will endure!" · OFFICER_Dryad_HURT · en-US.txt:1823 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Do not fuss! Fight on!" · OFFICER_Dryad_HURT · en-US.txt:1824 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] A wound! I'm furious! Fight on!" · OFFICER_Dryad_HURT · en-US.txt:2119 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Do not fuss! Fight!" · OFFICER_Dryad_HURT · en-US.txt:2120 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Sap and rage! Still standing!" · OFFICER_Dryad_HURT · en-US.txt:2121 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Dryad_VICTORY (6)
- "Well fought! The land breathes!" · OFFICER_Dryad_VICTORY · en-US.txt:1825 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Good! The land is safe!" · OFFICER_Dryad_VICTORY · en-US.txt:1826 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Victory! Something will grow here!" · OFFICER_Dryad_VICTORY · en-US.txt:1827 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] It's dead! The moon's not! Still, good!" · OFFICER_Dryad_VICTORY · en-US.txt:2122 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Good! Be quiet now!" · OFFICER_Dryad_VICTORY · en-US.txt:2123 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] We won! I'm still furious!" · OFFICER_Dryad_VICTORY · en-US.txt:2124 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Painter_HURT (3)
- "I'm hit! Still painting!" · OFFICER_Painter_HURT · en-US.txt:1850 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ow! I'm fine! Fire!" · OFFICER_Painter_HURT · en-US.txt:1851 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Keep brushing! Fire!" · OFFICER_Painter_HURT · en-US.txt:1852 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Painter_VICTORY (2)
- "Magnificent! Well fought!" · OFFICER_Painter_VICTORY · en-US.txt:1854 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Beautiful work! Victory!" · OFFICER_Painter_VICTORY · en-US.txt:1855 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Angler_FIGHT (4)
- "Take that! And that!" · OFFICER_Angler_FIGHT · en-US.txt:1870 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm not scared! Fire!" · OFFICER_Angler_FIGHT · en-US.txt:1871 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Out of my way! Fire!" · OFFICER_Angler_FIGHT · en-US.txt:1872 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "My fight! Watch this!" · OFFICER_Angler_FIGHT · en-US.txt:1873 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Angler_BOSS (4)
- "{Boss} is huge! I'm still in!" · OFFICER_Angler_BOSS · en-US.txt:1874 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Go go go! Hit {Boss}!" · OFFICER_Angler_BOSS · en-US.txt:1875 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm not scared of {Boss}! Fire!" · OFFICER_Angler_BOSS · en-US.txt:1876 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss} can't beat us! Fire!" · OFFICER_Angler_BOSS · en-US.txt:1877 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Angler_HURT (3)
- "Ow! I'm okay! Keep going!" · OFFICER_Angler_HURT · en-US.txt:1878 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hey! That hurt! Take that!" · OFFICER_Angler_HURT · en-US.txt:1879 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hit! I'm still fighting!" · OFFICER_Angler_HURT · en-US.txt:1880 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Angler_VICTORY (3)
- "Yeah! We won!" · OFFICER_Angler_VICTORY · en-US.txt:1881 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I knew we'd win!" · OFFICER_Angler_VICTORY · en-US.txt:1882 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Awesome! Best army ever!" · OFFICER_Angler_VICTORY · en-US.txt:1883 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Pirate_FIGHT (4)
- "Boarders! Hold fast!" · OFFICER_Pirate_FIGHT · en-US.txt:1898 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fire at will!" · OFFICER_Pirate_FIGHT · en-US.txt:1899 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Cutlasses out! Charge!" · OFFICER_Pirate_FIGHT · en-US.txt:1900 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Load and fire! Aye!" · OFFICER_Pirate_FIGHT · en-US.txt:1901 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Pirate_BOSS (4)
- "That be {Boss}! Guns ready!" · OFFICER_Pirate_BOSS · en-US.txt:1902 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! All cannons! Fire!" · OFFICER_Pirate_BOSS · en-US.txt:1903 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Shoot {Boss} until it falls!" · OFFICER_Pirate_BOSS · en-US.txt:1904 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "Blimey! {Boss}! Give 'em everything!" · OFFICER_Pirate_BOSS · en-US.txt:1905 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Pirate_HURT (3)
- "Arr! Just a scratch!" · OFFICER_Pirate_HURT · en-US.txt:1906 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Ye got me! Keep firing!" · OFFICER_Pirate_HURT · en-US.txt:1907 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Blast! Still standin'!" · OFFICER_Pirate_HURT · en-US.txt:1908 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Pirate_VICTORY (1)
- "Three cheers for the crew!" · OFFICER_Pirate_VICTORY · en-US.txt:1911 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### OFFICER_Clothier_FIGHT (1)
- "Rise, skeletons! Fight!" · OFFICER_Clothier_FIGHT · en-US.txt:1929 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Clothier_BOSS (3)
- "I've beaten worse than {Boss}! Fire!" · OFFICER_Clothier_BOSS · en-US.txt:1931 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Take down {Boss}! No mercy!" · OFFICER_Clothier_BOSS · en-US.txt:1932 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Everyone, out of {Boss}'s way! Fire!" · OFFICER_Clothier_BOSS · en-US.txt:1933 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Clothier_HURT (2)
- "Ow! Too stubborn to fall!" · OFFICER_Clothier_HURT · en-US.txt:1935 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hit! Not done yet!" · OFFICER_Clothier_HURT · en-US.txt:1936 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Clothier_VICTORY (2)
- "Good work! I'll allow it!" · OFFICER_Clothier_VICTORY · en-US.txt:1937 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hmph! Well fought!" · OFFICER_Clothier_VICTORY · en-US.txt:1939 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Wizard_FIGHT (4)
- "Casting! Stand back!" · OFFICER_Wizard_FIGHT · en-US.txt:1954 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Fire and lightning! Away!" · OFFICER_Wizard_FIGHT · en-US.txt:1955 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Swords up! Spells flying!" · OFFICER_Wizard_FIGHT · en-US.txt:1956 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Spell ready! Stand back!" · OFFICER_Wizard_FIGHT · en-US.txt:1957 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Wizard_BOSS (4)
- "{Boss}! Everything I've got! Cast!" · OFFICER_Wizard_BOSS · en-US.txt:1958 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I've read about {Boss}! Hit it hard!" · OFFICER_Wizard_BOSS · en-US.txt:1959 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! Summoning something big!" · OFFICER_Wizard_BOSS · en-US.txt:1960 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Don't look it in the eye! Fire!" · OFFICER_Wizard_BOSS · en-US.txt:1961 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Wizard_HURT (3)
- "Ow! Still casting!" · OFFICER_Wizard_HURT · en-US.txt:1962 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "My robe's on fire! I'm fine! Casting!" · OFFICER_Wizard_HURT · en-US.txt:1963 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Healing spell! Hold on!" · OFFICER_Wizard_HURT · en-US.txt:1964 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Wizard_VICTORY (3)
- "Splendid! We won!" · OFFICER_Wizard_VICTORY · en-US.txt:1965 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Marvelous work!" · OFFICER_Wizard_VICTORY · en-US.txt:1966 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "A small miracle! Well done!" · OFFICER_Wizard_VICTORY · en-US.txt:1967 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Zoologist_FIGHT (7)
- "Wolves, attack! Get 'em!" · OFFICER_Zoologist_FIGHT · en-US.txt:1982 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Go get 'em, boys!" · OFFICER_Zoologist_FIGHT · en-US.txt:1983 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "I'm not scared! Bite, bite, bite!" · OFFICER_Zoologist_FIGHT · en-US.txt:1984 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Good boys! Get 'em!" · OFFICER_Zoologist_FIGHT · en-US.txt:1985 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] I'm so mad! Wolves, bite!" · OFFICER_Zoologist_FIGHT · en-US.txt:2135 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Get 'em, boys! Now!" · OFFICER_Zoologist_FIGHT · en-US.txt:2136 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Not tonight! Bite them!" · OFFICER_Zoologist_FIGHT · en-US.txt:2137 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Zoologist_BOSS (7)
- "{Boss}! Wolves, attack!" · OFFICER_Zoologist_BOSS · en-US.txt:1986 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Biggest critter ever! Get it!" · OFFICER_Zoologist_BOSS · en-US.txt:1987 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "All wolves on {Boss}! Go!" · OFFICER_Zoologist_BOSS · en-US.txt:1988 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Take down {Boss}! Teeth out!" · OFFICER_Zoologist_BOSS · en-US.txt:1989 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] {Boss}! Worst timing! Get it!" · OFFICER_Zoologist_BOSS · en-US.txt:2138 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Wolves, get {Boss}!" · OFFICER_Zoologist_BOSS · en-US.txt:2139 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]
- "[grumpy] I hate {Boss}! Fire! Bite!" · OFFICER_Zoologist_BOSS · en-US.txt:2140 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Zoologist_HURT (6)
- "Ow! Not the ear! Still fighting!" · OFFICER_Zoologist_HURT · en-US.txt:1990 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm okay! I heal fast!" · OFFICER_Zoologist_HURT · en-US.txt:1991 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Hit! Not the tail! Fight on!" · OFFICER_Zoologist_HURT · en-US.txt:1992 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Ow! Not tonight!" · OFFICER_Zoologist_HURT · en-US.txt:2141 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Hey! Don't touch my ear!" · OFFICER_Zoologist_HURT · en-US.txt:2142 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] You hit me?! Get them!" · OFFICER_Zoologist_HURT · en-US.txt:2143 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Zoologist_VICTORY (6)
- "Yes! We got it! Good pack!" · OFFICER_Zoologist_VICTORY · en-US.txt:1993 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Treats for everyone! Great fight!" · OFFICER_Zoologist_VICTORY · en-US.txt:1994 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Rad! We won!" · OFFICER_Zoologist_VICTORY · en-US.txt:1995 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] We won! Still crabby!" · OFFICER_Zoologist_VICTORY · en-US.txt:2144 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Everything's dead! Good!" · OFFICER_Zoologist_VICTORY · en-US.txt:2145 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "[grumpy] Done! Treats for the wolves!" · OFFICER_Zoologist_VICTORY · en-US.txt:2146 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Cyborg_FIGHT (4)
- "Combat active! Open fire!" · OFFICER_Cyborg_FIGHT · en-US.txt:2010 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Drones deployed! Fire!" · OFFICER_Cyborg_FIGHT · en-US.txt:2011 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Targets locked! Firing!" · OFFICER_Cyborg_FIGHT · en-US.txt:2012 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Rockets away!" · OFFICER_Cyborg_FIGHT · en-US.txt:2013 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]

### OFFICER_Cyborg_BOSS (4)
- "{Boss} detected! Everything online! Fire!" · OFFICER_Cyborg_BOSS · en-US.txt:2014 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Threat: {Boss}! Full firepower!" · OFFICER_Cyborg_BOSS · en-US.txt:2015 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Target {Boss}! Maximum output!" · OFFICER_Cyborg_BOSS · en-US.txt:2016 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Engaging {Boss}! All weapons!" · OFFICER_Cyborg_BOSS · en-US.txt:2017 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Cyborg_HURT (3)
- "Damage detected! Still fighting!" · OFFICER_Cyborg_HURT · en-US.txt:2018 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "System hit! Continuing!" · OFFICER_Cyborg_HURT · en-US.txt:2019 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Leaking! Still operational! Fire!" · OFFICER_Cyborg_HURT · en-US.txt:2020 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_Cyborg_VICTORY (3)
- "Target neutralized! Success!" · OFFICER_Cyborg_VICTORY · en-US.txt:2021 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Mission complete! Excellent work!" · OFFICER_Cyborg_VICTORY · en-US.txt:2022 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Victory! Well done!" · OFFICER_Cyborg_VICTORY · en-US.txt:2023 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_WitchDoctor_FIGHT (4)
- "The spirits demand blood! Strike!" · OFFICER_WitchDoctor_FIGHT · en-US.txt:2038 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Poison darts! Fly!" · OFFICER_WitchDoctor_FIGHT · en-US.txt:2039 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Back! The curses are loose!" · OFFICER_WitchDoctor_FIGHT · en-US.txt:2040 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Spirits, strike! Strike!" · OFFICER_WitchDoctor_FIGHT · en-US.txt:2041 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_WitchDoctor_BOSS (4)
- "{Boss}! The spirits roar! Strike!" · OFFICER_WitchDoctor_BOSS · en-US.txt:2042 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I have venom for {Boss}! Strike it down!" · OFFICER_WitchDoctor_BOSS · en-US.txt:2043 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "{Boss}! Take it down! Spirits guide us!" · OFFICER_WitchDoctor_BOSS · en-US.txt:2044 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "The bones say we win! Fire!" · OFFICER_WitchDoctor_BOSS · en-US.txt:2045 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_WitchDoctor_HURT (3)
- "A wound! The spirits ask more! Strike!" · OFFICER_WitchDoctor_HURT · en-US.txt:2046 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I bleed! I fight on!" · OFFICER_WitchDoctor_HURT · en-US.txt:2047 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Pain feeds the ritual! Fire!" · OFFICER_WitchDoctor_HURT · en-US.txt:2048 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_WitchDoctor_VICTORY (3)
- "It is done! The spirits are pleased!" · OFFICER_WitchDoctor_VICTORY · en-US.txt:2049 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Victory! The earth drinks!" · OFFICER_WitchDoctor_VICTORY · en-US.txt:2050 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "A good day! Well done!" · OFFICER_WitchDoctor_VICTORY · en-US.txt:2051 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_TaxCollector_BOSS (2)
- "{Boss}! Collect the loot! Fire!" · OFFICER_TaxCollector_BOSS · en-US.txt:2071 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "Make {Boss} settle! Fire!" · OFFICER_TaxCollector_BOSS · en-US.txt:2073 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### OFFICER_TaxCollector_HURT (2)
- "Hit! I'm still collecting!" · OFFICER_TaxCollector_HURT · en-US.txt:2075 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline
- "I'm hurt! They'll pay for it!" · OFFICER_TaxCollector_HURT · en-US.txt:2076 · AI-WRITTEN · dialogue round 4, agent-written battle shouts [b45a2b3]; style: setup + short punchline

### RIFT_PANIC (25)
- "The sun's GONE! It's just GONE! Where did it GO!?" · RIFT_PANIC · en-US.txt:2194 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "{Player}! {Player}, the sun! LOOK UP!" · RIFT_PANIC · en-US.txt:2195 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "It's the middle of the day and it's DARK! DARK!" · RIFT_PANIC · en-US.txt:2196 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "There's a HOLE in the sky! Why is there a HOLE in the sky!?" · RIFT_PANIC · en-US.txt:2197 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]
- "Don't look at it! Don't look at it! Don't LOOK at it!" · RIFT_PANIC · en-US.txt:2198 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "What is that!? WHAT IS THAT!?" · RIFT_PANIC · en-US.txt:2199 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Somebody do something! ANYBODY! DO SOMETHING!" · RIFT_PANIC · en-US.txt:2200 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "It's watching us! I swear it's WATCHING us!" · RIFT_PANIC · en-US.txt:2201 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "I want to go home! I want to go home RIGHT NOW!" · RIFT_PANIC|Greenhorn · en-US.txt:2202 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]
- "{Player}! Make it stop! Please make it STOP!" · RIFT_PANIC|Greenhorn · en-US.txt:2203 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Is this the end of the world!? Nobody said it'd be the END OF THE WORLD!" · RIFT_PANIC|Greenhorn · en-US.txt:2204 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]
- "I can't see! I can't see anything! {Player}, where ARE you!?" · RIFT_PANIC|Greenhorn · en-US.txt:2205 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "That's it! I'm out! Keep the pay! I'M OUT!" · RIFT_PANIC|Sellsword · en-US.txt:2206 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Nobody pays me enough for THIS! NOBODY!" · RIFT_PANIC|Sellsword · en-US.txt:2207 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Which way is out!? Somebody point me OUT of here!" · RIFT_PANIC|Sellsword · en-US.txt:2208 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Rigroth the Undying, PROTECT US! PROTECT US NOW!" · RIFT_PANIC|Zealot · en-US.txt:2209 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "I'm praying! I'm PRAYING! Why isn't it WORKING!?" · RIFT_PANIC|Zealot · en-US.txt:2210 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Forgive us! Whatever we did, we're SORRY! WE'RE SORRY!" · RIFT_PANIC|Zealot · en-US.txt:2211 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "OH GOOD! THE SUN! THAT'S GREAT! THAT'S JUST GREAT!" · RIFT_PANIC|Cynic · en-US.txt:2212 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Sure! Fine! Who needed the SUN anyway!?" · RIFT_PANIC|Cynic · en-US.txt:2213 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Wonderful! Did anyone else order the END OF THE WORLD!?" · RIFT_PANIC|Cynic · en-US.txt:2214 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]
- "Nobody panic! NOBODY PANIC! I am NOT PANICKING!" · RIFT_PANIC|Veteran · en-US.txt:2215 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Hold the line! Hold the... what line!? Hold SOMETHING!" · RIFT_PANIC|Veteran · en-US.txt:2216 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Eyes on me! EYES ON ME! Nobody runs! I SAID NOBODY RUNS!" · RIFT_PANIC|Veteran · en-US.txt:2217 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline
- "Get inside! Everyone get INSIDE! NOW!" · RIFT_PANIC · en-US.txt:2488 · AI-WRITTEN · rift builder (riftpanic) [daac2a9]; style: setup + short punchline

### RIFT_SHOCK (15)
- "Nobody's seen the sun since. Nobody." · RIFT_SHOCK · en-US.txt:2220 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "It was there yesterday. Right there." · RIFT_SHOCK · en-US.txt:2221 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "The birds stopped. Did you notice?" · RIFT_SHOCK · en-US.txt:2222 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I had things to say this morning. They're gone." · RIFT_SHOCK · en-US.txt:2223 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Is the sun coming back, {Player}? I'd like to know." · RIFT_SHOCK|Greenhorn · en-US.txt:2224 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I keep looking up. I know I shouldn't." · RIFT_SHOCK|Greenhorn · en-US.txt:2225 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I don't know what to charge for this." · RIFT_SHOCK|Sellsword · en-US.txt:2226 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "I've prayed on it. Nobody answered." · RIFT_SHOCK|Zealot · en-US.txt:2228 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "The boss mentioned a courage bonus. I'll need it." · RIFT_SHOCK|Zealot · en-US.txt:2229 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Don't ask me what it is. Nobody knows." · RIFT_SHOCK|Cynic · en-US.txt:2231 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Keep your voice down. It feels right." · RIFT_SHOCK|Veteran · en-US.txt:2232 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "It happened while we were looking away." · RIFT_SHOCK|Veteran · en-US.txt:2233 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "How long is it going to stay up there?" · RIFT_SHOCK · en-US.txt:2489 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Was it there when I woke up? I can't remember." · RIFT_SHOCK|Greenhorn · en-US.txt:2490 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "So that's what the end looks like." · RIFT_SHOCK|Cynic · en-US.txt:2491 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]

### RIFT_ABSENT (7)
- "Somebody explain the sky. I was out a day." · RIFT_ABSENT · en-US.txt:2236 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "I was out one day. One day. What happened?" · RIFT_ABSENT · en-US.txt:2237 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "When I left, there was a sun. I'm fairly sure." · RIFT_ABSENT · en-US.txt:2239 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I came back and the sky was wrong." · RIFT_ABSENT|Greenhorn · en-US.txt:2240 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "Somebody catch me up. Slowly." · RIFT_ABSENT|Cynic · en-US.txt:2243 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I leave you lot alone. {Player}, what did we do?" · RIFT_ABSENT|Veteran · en-US.txt:2244 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I come back to this? You let it get worse?" · RIFT_ABSENT · en-US.txt:2492 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_COLD (10)
- "It's snowing. It doesn't snow here." · RIFT_COLD · en-US.txt:2247 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "It's cold, {Player}. Very cold." · RIFT_COLD · en-US.txt:2248 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "The grass is gone. It's all going white." · RIFT_COLD · en-US.txt:2249 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I can see my breath at midday." · RIFT_COLD · en-US.txt:2250 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "{Mage}, could you do the fire thing? For our hands." · RIFT_COLD · en-US.txt:2251 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "My fingers are numb. I can still shoot. I think." · RIFT_COLD|Greenhorn · en-US.txt:2252 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "The cold is a test. I'm passing it. Slowly." · RIFT_COLD|Zealot · en-US.txt:2254 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Keep moving. The cold takes its time." · RIFT_COLD|Veteran · en-US.txt:2256 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "My boots froze to the floor last night." · RIFT_COLD · en-US.txt:2493 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "I can't feel my feet. Is that bad?" · RIFT_COLD|Greenhorn · en-US.txt:2494 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_POWER (9)
- "We can kill anything. Can't fix that, though." · RIFT_POWER · en-US.txt:2259 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Best we've ever fought. Worst we've ever felt." · RIFT_POWER · en-US.txt:2260 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "We killed the big one. The sky didn't notice." · RIFT_POWER · en-US.txt:2261 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "{Mage} can split stone now. Can't light the sky." · RIFT_POWER · en-US.txt:2262 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Best gear I've ever had. Still scared." · RIFT_POWER|Greenhorn · en-US.txt:2263 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "We were promised glory. We got this." · RIFT_POWER|Zealot · en-US.txt:2265 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "We're strong. Strong enough to stand here." · RIFT_POWER|Cynic · en-US.txt:2266 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I could level a hill with this. Wouldn't help." · RIFT_POWER|Veteran · en-US.txt:2267 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Strongest army there's ever been. Still cold." · RIFT_POWER · en-US.txt:2496 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_LATER (8)
- "So we're just living under it now?" · RIFT_LATER · en-US.txt:2270 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Player}. With respect. Is there a plan?" · RIFT_LATER · en-US.txt:2271 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]; style: setup + short punchline
- "We've gotten used to it. We shouldn't have." · RIFT_LATER · en-US.txt:2272 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Has it moved? I check every morning. No." · RIFT_LATER · en-US.txt:2273 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I'll follow you up there. Please don't ask yet." · RIFT_LATER|Greenhorn · en-US.txt:2274 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I'm with you, {Player}. I'd just like to know where." · RIFT_LATER|Zealot · en-US.txt:2276 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Nobody wants to ask. So I'm asking." · RIFT_LATER|Veteran · en-US.txt:2278 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Are we just going to wait here forever?" · RIFT_LATER · en-US.txt:2498 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]

### RIFT_RESPECT (9)
- "{Mage} is hitting everything lately. Everything." · RIFT_RESPECT · en-US.txt:2298 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Mage} brought me water. Didn't say anything." · RIFT_RESPECT · en-US.txt:2299 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "{Summoner}'s minions take the first hit now. Every time." · RIFT_RESPECT · en-US.txt:2300 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "{Melee} went right up to the front. Right up." · RIFT_RESPECT · en-US.txt:2301 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "{Ranger}'s bullets turn corners. I don't mind now." · RIFT_RESPECT · en-US.txt:2302 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "{Tank} goes down first so we don't. Not funny now." · RIFT_RESPECT · en-US.txt:2303 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "[mage] Everyone's been kind to me. That's worrying." · RIFT_RESPECT · en-US.txt:2304 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "[non-summoner] I was unkind about summoners. Quietly noted." · RIFT_RESPECT|Zealot · en-US.txt:2305 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: "I'd like it noted" bit
- "{Summoner}. Whatever you're doing, keep doing it." · RIFT_RESPECT|Veteran · en-US.txt:2307 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline

### RIFT_IDLE (21)
- "...Sorry. Did you need something?" · RIFT_IDLE · en-US.txt:2311 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]; style: setup + short punchline
- "Mm." · RIFT_IDLE · en-US.txt:2312 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]
- "It's very quiet now. No birds. Nothing." · RIFT_IDLE · en-US.txt:2314 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Morning, {Player}. If it's morning." · RIFT_IDLE · en-US.txt:2315 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Nobody's argued in days. That's worse." · RIFT_IDLE · en-US.txt:2316 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "{Player}. Ready when you are." · RIFT_IDLE · en-US.txt:2317 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]; style: setup + short punchline
- "I keep my weapon close now. Day and night." · RIFT_IDLE · en-US.txt:2318 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I'm fine, {Player}. I'm fine. Thank you." · RIFT_IDLE|Greenhorn · en-US.txt:2319 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I stopped counting the blocks in the wall." · RIFT_IDLE|Greenhorn · en-US.txt:2320 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "I haven't asked about my wages lately." · RIFT_IDLE|Sellsword · en-US.txt:2321 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "Pay me when you can. Where would I go?" · RIFT_IDLE|Sellsword · en-US.txt:2322 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Still proud to serve. Just more quietly." · RIFT_IDLE|Zealot · en-US.txt:2323 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "I haven't quoted Mother Gnash in days." · RIFT_IDLE|Zealot · en-US.txt:2324 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "Rest when you can, {Player}. We'll keep watch." · RIFT_IDLE|Veteran · en-US.txt:2327 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Stay close. Nobody walks alone anymore." · RIFT_IDLE|Veteran · en-US.txt:2328 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Same sky. Same cold. Same everything." · RIFT_IDLE · en-US.txt:2502 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I'm sick of looking at it." · RIFT_IDLE · en-US.txt:2503 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Another day. If you can call it that." · RIFT_IDLE · en-US.txt:2504 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I don't sleep much now. Nobody does." · RIFT_IDLE|Greenhorn · en-US.txt:2505 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I'll keep serving. I just won't smile about it." · RIFT_IDLE|Zealot · en-US.txt:2507 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Keep your head down. Literally." · RIFT_IDLE|Veteran · en-US.txt:2509 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_CAMP (13)
- "Sit with me, {Comrade}. You don't have to talk." · RIFT_CAMP · en-US.txt:2331 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Closer to the fire, {Comrade}. It's all we've got." · RIFT_CAMP · en-US.txt:2332 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "No stories tonight, {Comrade}. That's fine." · RIFT_CAMP · en-US.txt:2333 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Thank you, {Comrade}. Really." · RIFT_CAMP · en-US.txt:2334 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "..." · RIFT_CAMP · en-US.txt:2335 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]
- "Will it be gone by morning, {Comrade}?" · RIFT_CAMP|Greenhorn · en-US.txt:2336 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "Have this one, {Comrade}. No charge." · RIFT_CAMP|Sellsword · en-US.txt:2337 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]; style: setup + short punchline
- "I'd bless this, {Comrade}. Who's listening?" · RIFT_CAMP|Zealot · en-US.txt:2338 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Don't look up, {Comrade}. Look at the fire." · RIFT_CAMP|Cynic · en-US.txt:2339 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Get some sleep, {Comrade}. I'll stay up." · RIFT_CAMP|Veteran · en-US.txt:2340 · AI-WRITTEN · rift builder (wotgtalk) [825d2c7]; style: setup + short punchline
- "Sit closer, {Comrade}. It's cold. That's all." · RIFT_CAMP · en-US.txt:2510 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Don't tell me it'll be fine, {Comrade}." · RIFT_CAMP|Cynic · en-US.txt:2511 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Eat something, {Comrade}. That's an order." · RIFT_CAMP|Veteran · en-US.txt:2512 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_DUKE (7)
- "What was that? What just happened?" · RIFT_DUKE · en-US.txt:2343 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "It ate it. It just ate the whole thing." · RIFT_DUKE · en-US.txt:2344 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "It's gone. It's just gone." · RIFT_DUKE · en-US.txt:2345 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "It didn't even look at us." · RIFT_DUKE · en-US.txt:2346 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]
- "I want to go home. Right now." · RIFT_DUKE|Greenhorn · en-US.txt:2347 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Not fighting that. Not for any price." · RIFT_DUKE|Sellsword · en-US.txt:2348 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline
- "Nobody move. Nobody say a word." · RIFT_DUKE|Veteran · en-US.txt:2351 · AI-WRITTEN · rift builder (wotgtalk) [c01b01d]; style: setup + short punchline

### RIFT_BLOOD (21)
- "Everything's red. The rain's red. Isn't it?" · RIFT_BLOOD · en-US.txt:2367 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I can't see my hands. I can hear things, out there." · RIFT_BLOOD · en-US.txt:2368 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Something's moving past the fire. Don't look." · RIFT_BLOOD · en-US.txt:2369 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "The whole sky's bleeding, {Player}." · RIFT_BLOOD · en-US.txt:2370 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Keep the torches lit. Please. Keep them lit." · RIFT_BLOOD · en-US.txt:2371 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It's darker than dark. How is that possible?" · RIFT_BLOOD · en-US.txt:2372 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I can see eyes out there. Only the eyes." · RIFT_BLOOD · en-US.txt:2373 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "This rain won't stop. It just won't stop." · RIFT_BLOOD · en-US.txt:2374 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I'm not crying. It's the rain. It's red rain." · RIFT_BLOOD|Greenhorn · en-US.txt:2375 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: "not X, I'm Y" swap
- "Please don't put me on watch. Not tonight." · RIFT_BLOOD|Greenhorn · en-US.txt:2376 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "No pay is worth tonight. I'm staying anyway." · RIFT_BLOOD|Sellsword · en-US.txt:2377 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I've stopped praying tonight. Something's listening." · RIFT_BLOOD|Zealot · en-US.txt:2378 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Mother Gnash warned of a night like this." · RIFT_BLOOD|Zealot · en-US.txt:2379 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Back to back. Nobody leaves the light." · RIFT_BLOOD|Veteran · en-US.txt:2382 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Worst night I've had. And I've had nights." · RIFT_BLOOD|Veteran · en-US.txt:2383 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Something touched my boot. Something touched my boot!" · RIFT_BLOOD · en-US.txt:2549 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Stop whistling. Whatever that is, stop it." · RIFT_BLOOD · en-US.txt:2550 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Footsteps. Ours. I hope they're ours." · RIFT_BLOOD · en-US.txt:2551 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Was that you? Please say that was you." · RIFT_BLOOD|Greenhorn · en-US.txt:2552 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "I'd scream, but the rain's louder." · RIFT_BLOOD|Cynic · en-US.txt:2553 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "Nobody sleeps. Nobody." · RIFT_BLOOD|Veteran · en-US.txt:2554 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline

### RIFT_GROW1 (8)
- "It's lower than yesterday. I'm sure of it." · RIFT_GROW1 · en-US.txt:2386 · AI-WRITTEN · rift builder (wotgtalk2) [79e7b45]; style: setup + short punchline
- "Nobody say it's the same size. It isn't." · RIFT_GROW1 · en-US.txt:2387 · AI-WRITTEN · rift builder (wotgtalk2) [79e7b45]; style: setup + short punchline
- "It's half the sky now. It was a hole." · RIFT_GROW1 · en-US.txt:2388 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It moved. I watched it move." · RIFT_GROW1 · en-US.txt:2389 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Since when is it that big?" · RIFT_GROW1 · en-US.txt:2390 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Why is it bigger, {Player}? Why is it bigger?" · RIFT_GROW1|Greenhorn · en-US.txt:2391 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It grows when we win. Has nobody else noticed?" · RIFT_GROW1|Zealot · en-US.txt:2393 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Don't measure it. It doesn't help." · RIFT_GROW1|Veteran · en-US.txt:2395 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_GROW2 (7)
- "It's bigger again. Every win, it's bigger." · RIFT_GROW2 · en-US.txt:2398 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It fills the sky now. There's no edge to it." · RIFT_GROW2 · en-US.txt:2399 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I can feel it when I look up. Like a weight." · RIFT_GROW2 · en-US.txt:2400 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "The light's wrong everywhere now." · RIFT_GROW2 · en-US.txt:2401 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "I've stopped looking up. It's easier." · RIFT_GROW2|Greenhorn · en-US.txt:2402 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Rigroth the Undying would stop winning. Just saying." · RIFT_GROW2|Zealot · en-US.txt:2404 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It's hanging lower. I'd swear to it." · RIFT_GROW2|Veteran · en-US.txt:2406 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_GROW3 (9)
- "You can see inside it now." · RIFT_GROW3 · en-US.txt:2409 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "There's something inside it. It's moving." · RIFT_GROW3 · en-US.txt:2410 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It's not in the sky anymore. The sky's in it." · RIFT_GROW3 · en-US.txt:2411 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Don't look into it too long." · RIFT_GROW3 · en-US.txt:2412 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "I see shapes in it at night. Tell me you do too." · RIFT_GROW3|Greenhorn · en-US.txt:2413 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Whatever's in there, I'm not paid enough." · RIFT_GROW3|Sellsword · en-US.txt:2414 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "It's almost here. Whatever it is." · RIFT_GROW3|Zealot · en-US.txt:2415 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Nearly touching the ground. Lovely." · RIFT_GROW3|Cynic · en-US.txt:2416 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It's close enough to hit. I keep wanting to try." · RIFT_GROW3|Veteran · en-US.txt:2417 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_ICE (5)
- "The lake froze over. In one night." · RIFT_ICE · en-US.txt:2420 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "The water's frozen solid. Fish and all." · RIFT_ICE · en-US.txt:2421 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "You can walk on the ponds now. I wish you couldn't." · RIFT_ICE · en-US.txt:2422 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I broke the ice for water. It froze again." · RIFT_ICE|Greenhorn · en-US.txt:2423 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Melt snow if you need water. Boil it first." · RIFT_ICE|Veteran · en-US.txt:2426 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_FROZEN (8)
- "The sea's frozen. The whole sea." · RIFT_FROZEN · en-US.txt:2429 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "There's snow on the ocean. That's wrong." · RIFT_FROZEN · en-US.txt:2430 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "The waves stopped. Just stopped, mid-wave." · RIFT_FROZEN · en-US.txt:2431 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Where do the fish go? Where does anything go?" · RIFT_FROZEN|Greenhorn · en-US.txt:2432 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "No ships, no trade. Nothing's moving anymore." · RIFT_FROZEN|Sellsword · en-US.txt:2433 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "The Drowned One's sea, frozen. That's a bad sign." · RIFT_FROZEN|Zealot · en-US.txt:2434 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Frozen ocean. Nothing surprises me now." · RIFT_FROZEN|Cynic · en-US.txt:2435 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "When the sea stops, the world's stopping." · RIFT_FROZEN|Veteran · en-US.txt:2436 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]

### RIFT_FOG (14)
- "I can't see three paces. Stay close." · RIFT_FOG · en-US.txt:2439 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Did you hear that? Something walked out there." · RIFT_FOG · en-US.txt:2440 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "There's a tree there. There wasn't a tree there." · RIFT_FOG · en-US.txt:2441 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Keep talking so I know where you are." · RIFT_FOG · en-US.txt:2442 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Don't follow anything in the fog. Anything." · RIFT_FOG · en-US.txt:2443 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I heard my name. Nobody said my name. Right?" · RIFT_FOG|Greenhorn · en-US.txt:2444 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I'm not scouting in this. Double pay or not." · RIFT_FOG|Sellsword · en-US.txt:2445 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Mother Gnash says stay in the light. So I am." · RIFT_FOG|Zealot · en-US.txt:2446 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Count heads. Then count them again." · RIFT_FOG|Veteran · en-US.txt:2448 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Where'd everyone go? I was right behind you." · RIFT_FOG · en-US.txt:2632 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "I can't see you. Say something." · RIFT_FOG · en-US.txt:2633 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "I keep drifting off. I don't mean to." · RIFT_FOG · en-US.txt:2634 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "I was standing right here. Wasn't I?" · RIFT_FOG · en-US.txt:2635 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Player}? You're further than I thought." · RIFT_FOG|Greenhorn · en-US.txt:2636 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline

### RIFT_SCARE (7)
- "What was that? What was that?!" · RIFT_SCARE · en-US.txt:2451 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "It was right there. It was right there!" · RIFT_SCARE · en-US.txt:2452 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Where did it go? It was just there!" · RIFT_SCARE · en-US.txt:2453 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I'm not okay. I'm not okay." · RIFT_SCARE · en-US.txt:2454 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: "not X, I'm Y" swap
- "I want to go home! I want to go home now!" · RIFT_SCARE|Greenhorn · en-US.txt:2455 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Mother Gnash, Mother Gnash, Mother Gnash..." · RIFT_SCARE|Zealot · en-US.txt:2457 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Hold the line! It's gone. Hold anyway!" · RIFT_SCARE|Veteran · en-US.txt:2459 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_WORN (10)
- "I'm tired, {Player}. Just tired." · RIFT_WORN · en-US.txt:2475 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I'm so sick of this sky." · RIFT_WORN · en-US.txt:2476 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "I don't remember what warm feels like." · RIFT_WORN · en-US.txt:2477 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Every day's the same. Dark, cold, darker." · RIFT_WORN · en-US.txt:2478 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I stopped counting the days." · RIFT_WORN · en-US.txt:2479 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]
- "Wake me when it's over. Or don't." · RIFT_WORN · en-US.txt:2480 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I used to be scared. Now I'm just tired." · RIFT_WORN|Greenhorn · en-US.txt:2481 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "Keep the money. Just give me a sunrise." · RIFT_WORN|Sellsword · en-US.txt:2482 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I used to believe in something. It was nice." · RIFT_WORN|Zealot · en-US.txt:2483 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline
- "I've had enough, {Player}. I'm still here." · RIFT_WORN|Veteran · en-US.txt:2485 · AI-WRITTEN · rift builder (wotgtalk2) [6c810f5]; style: setup + short punchline

### RIFT_HUSH (6)
- "..." · RIFT_HUSH · en-US.txt:2526 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "......" · RIFT_HUSH · en-US.txt:2527 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*heavy breathing*" · RIFT_HUSH · en-US.txt:2528 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "...don't." · RIFT_HUSH · en-US.txt:2529 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*teeth chattering*" · RIFT_HUSH · en-US.txt:2530 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "...not again." · RIFT_HUSH · en-US.txt:2531 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]

### RIFT_ANGRY (21)
- "Make it stop. Somebody make it stop!" · RIFT_ANGRY · en-US.txt:2534 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Don't touch me. Don't talk to me." · RIFT_ANGRY · en-US.txt:2536 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "If one more thing screams out there, so will I." · RIFT_ANGRY · en-US.txt:2537 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "Soaked to the bone, and done with all of it." · RIFT_ANGRY · en-US.txt:2538 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "Get that torch out of my face!" · RIFT_ANGRY · en-US.txt:2539 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "It's closer than last night. Don't argue." · RIFT_ANGRY · en-US.txt:2540 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Who decided we live like this? Who?" · RIFT_ANGRY · en-US.txt:2541 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "I'm not scared anymore. I'm furious." · RIFT_ANGRY|Greenhorn · en-US.txt:2542 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: "not X, I'm Y" swap
- "Mother Gnash can explain this to my face." · RIFT_ANGRY|Zealot · en-US.txt:2544 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "Stand your post and leave me be." · RIFT_ANGRY|Veteran · en-US.txt:2546 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "What. What do you want." · RIFT_ANGRY · en-US.txt:2646 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Back off. I mean it." · RIFT_ANGRY · en-US.txt:2647 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Don't stand behind me. Ever." · RIFT_ANGRY · en-US.txt:2649 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "I'm fine. Stop asking if I'm fine." · RIFT_ANGRY · en-US.txt:2650 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Touch my gear again and lose a hand." · RIFT_ANGRY · en-US.txt:2651 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]
- "Quiet. I'm listening for something." · RIFT_ANGRY · en-US.txt:2652 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "{Player}. Whatever it is, make it quick." · RIFT_ANGRY · en-US.txt:2653 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "I'm not jumpy. You're jumpy." · RIFT_ANGRY|Greenhorn · en-US.txt:2654 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Pay me and leave me alone. In that order." · RIFT_ANGRY|Sellsword · en-US.txt:2655 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Mother Gnash isn't happy either. Move." · RIFT_ANGRY|Zealot · en-US.txt:2656 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Eyes open, mouths shut. All of you." · RIFT_ANGRY|Veteran · en-US.txt:2658 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline

### RIFT_BOSS (5)
- "Fine. Let's get it over with." · RIFT_BOSS · en-US.txt:2558 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Big. Everything's big now." · RIFT_BOSS · en-US.txt:2561 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "I'm too tired to be scared of this one." · RIFT_BOSS|Greenhorn · en-US.txt:2569 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "Same as always. Keep your heads down." · RIFT_BOSS|Veteran · en-US.txt:2573 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[summoner] Sentries placed. Minions queued. Moving on." · RIFT_BOSS · en-US.txt:2677 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline

### RIFT_CLASS (11)
- "[melee] I'm swinging closer now. No point hanging back." · RIFT_CLASS · en-US.txt:2596 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[melee] My sword's the only warm thing I hold." · RIFT_CLASS · en-US.txt:2597 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "[ranger] Counted my ammo twice. It's never enough now." · RIFT_CLASS · en-US.txt:2598 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[ranger] Can't aim at what I can't see. And I can't see." · RIFT_CLASS · en-US.txt:2599 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[mage] My mana's fine. It's everything else." · RIFT_CLASS · en-US.txt:2600 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "[mage] I tried a light spell. The dark ate it." · RIFT_CLASS · en-US.txt:2601 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Melee} stands at the front every time now." · RIFT_CLASS · en-US.txt:2606 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Ranger} cleans that gun every night. Every night." · RIFT_CLASS · en-US.txt:2607 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "{Summoner}'s minions keep us company at night." · RIFT_CLASS · en-US.txt:2609 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "{Rogue} keeps vanishing. I hope it's on purpose." · RIFT_CLASS · en-US.txt:2610 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline
- "Good shooting, {Ranger}. I mean it." · RIFT_CLASS|Veteran · en-US.txt:2614 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]; style: setup + short punchline

### RIFT_HABIT (10)
- "*murmurs a short prayer*" · RIFT_HABIT|Zealot · en-US.txt:2617 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*touches an old charm, twice*" · RIFT_HABIT|Zealot · en-US.txt:2618 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*bows toward the dark, then thinks better of it*" · RIFT_HABIT|Zealot · en-US.txt:2619 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*watches the treeline without blinking*" · RIFT_HABIT|Veteran · en-US.txt:2620 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*checks the perimeter again*" · RIFT_HABIT|Veteran · en-US.txt:2621 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*sticks close to the nearest torch*" · RIFT_HABIT|Greenhorn · en-US.txt:2624 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*flinches at a sound nobody else heard*" · RIFT_HABIT|Greenhorn · en-US.txt:2625 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*sighs at the sky*" · RIFT_HABIT|Cynic · en-US.txt:2626 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*rubs hands together for warmth*" · RIFT_HABIT · en-US.txt:2628 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]
- "*pulls the collar up and says nothing*" · RIFT_HABIT · en-US.txt:2629 · AI-WRITTEN · rift builder (wotgtalk2) [6c33a64]

### RIFT_FLIGHT (9)
- "..." · RIFT_FLIGHT · en-US.txt:2661 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]
- "......" · RIFT_FLIGHT · en-US.txt:2662 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]
- "Don't look down. Don't look up either." · RIFT_FLIGHT · en-US.txt:2663 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Eyes forward. Eyes forward." · RIFT_FLIGHT · en-US.txt:2664 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "...I don't want to be any closer to it." · RIFT_FLIGHT · en-US.txt:2665 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]
- "Keep low. Please keep low." · RIFT_FLIGHT · en-US.txt:2666 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "...ground. Ground soon." · RIFT_FLIGHT · en-US.txt:2667 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "...we're down. Good." · RIFT_FLIGHT · en-US.txt:2668 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline
- "Low and quick. Nobody sightsees." · RIFT_FLIGHT|Veteran · en-US.txt:2670 · AI-WRITTEN · rift builder (wotgtalk2) [b2d3c0a]; style: setup + short punchline

### SCENE_GENESIS (7)
- "{Player}, what did you just DO?!" · SCENE_GENESIS · en-US.txt:2709 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "The sky's tearing! It's tearing right down the middle!" · SCENE_GENESIS · en-US.txt:2710 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "That's a hole! In the sky! Again! A BIGGER one!" · SCENE_GENESIS · en-US.txt:2711 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Everybody down! Down! I don't know why, just get DOWN!" · SCENE_GENESIS · en-US.txt:2712 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Was that on purpose?! Please tell me that was on purpose!" · SCENE_GENESIS|Greenhorn · en-US.txt:2713 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "The commander calls and the heavens open! I knew it! I KNEW it!" · SCENE_GENESIS|Zealot · en-US.txt:2715 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Form up! Whatever comes out of that, it comes out to us!" · SCENE_GENESIS|Veteran · en-US.txt:2717 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_RIFT_OPEN (5)
- "It's opening! The thing in the sky is OPENING!" · SCENE_RIFT_OPEN · en-US.txt:2720 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Don't look into it! {Player}, don't look into it!" · SCENE_RIFT_OPEN · en-US.txt:2721 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Is that the same rift?! It's right on top of us!" · SCENE_RIFT_OPEN · en-US.txt:2722 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's breathing. Why is it breathing?" · SCENE_RIFT_OPEN · en-US.txt:2723 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Weapons up! It bleeds or it doesn't, we find out now!" · SCENE_RIFT_OPEN|Veteran · en-US.txt:2728 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_RIFT_SUCK (6)
- "It's pulling! Grab something! Grab ANYTHING!" · SCENE_RIFT_SUCK · en-US.txt:2731 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "My feet are leaving the ground! I didn't tell them to!" · SCENE_RIFT_SUCK · en-US.txt:2732 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Hold on to {Player}! No, don't, then we all go in!" · SCENE_RIFT_SUCK · en-US.txt:2733 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "It's eating the air! Where's the air going?!" · SCENE_RIFT_SUCK|Greenhorn · en-US.txt:2734 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I am NOT going in there! Hold my boots! Hold ME!" · SCENE_RIFT_SUCK|Sellsword · en-US.txt:2735 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Dig in! Heels down! Nobody goes in that hole!" · SCENE_RIFT_SUCK|Veteran · en-US.txt:2736 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_RIFT_DOWN (4)
- "It's closing! We did it! It's closing!" · SCENE_RIFT_DOWN · en-US.txt:2739 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Wait. Why is it getting bigger. Why is it getting BIGGER?" · SCENE_RIFT_DOWN · en-US.txt:2740 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Something's climbing out. Something's climbing OUT." · SCENE_RIFT_DOWN · en-US.txt:2741 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "That was the door? That was just the DOOR?!" · SCENE_RIFT_DOWN|Greenhorn · en-US.txt:2742 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_RIFT_FIGHT (6)
- "Keep shooting into it! Something in there has to feel it!" · SCENE_RIFT_FIGHT · en-US.txt:2747 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Those stars aren't stars! Don't touch the stars!" · SCENE_RIFT_FIGHT · en-US.txt:2748 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It reached out! An arm! It reached out of the SKY!" · SCENE_RIFT_FIGHT · en-US.txt:2749 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Stay near {Player}! The ground's the only thing that's ours!" · SCENE_RIFT_FIGHT · en-US.txt:2750 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Rigroth the Undying would have shot it sooner. Shoot it!" · SCENE_RIFT_FIGHT|Zealot · en-US.txt:2755 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Short bursts! Watch the thorns! Watch your feet!" · SCENE_RIFT_FIGHT|Veteran · en-US.txt:2757 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_EMERGE (5)
- "Those are legs. Those are LEGS coming out of it!" · SCENE_AVATAR_EMERGE · en-US.txt:2760 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "It's getting out! {Player}, it's getting OUT!" · SCENE_AVATAR_EMERGE · en-US.txt:2761 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "How tall is it?! How tall IS it?!" · SCENE_AVATAR_EMERGE · en-US.txt:2762 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Ul'Thazhra the Unblinking would not want us to watch this." · SCENE_AVATAR_EMERGE|Zealot · en-US.txt:2765 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Hold! Let it climb all the way out, then give it everything!" · SCENE_AVATAR_EMERGE|Veteran · en-US.txt:2767 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_HEAD (4)
- "Is that a face? Is that its FACE?" · SCENE_AVATAR_HEAD · en-US.txt:2770 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's looking at us. It's looking at all of us at once." · SCENE_AVATAR_HEAD · en-US.txt:2771 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Don't make eye contact! Where are its eyes?!" · SCENE_AVATAR_HEAD|Greenhorn · en-US.txt:2772 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Head's out. That's where we shoot." · SCENE_AVATAR_HEAD|Veteran · en-US.txt:2774 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_SCREAM (8)
- "Is everyone still here?! Sound off! SOUND OFF!" · SCENE_AVATAR_SCREAM · en-US.txt:2777 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I can't hear my own gun! Am I firing?! I think I'm firing!" · SCENE_AVATAR_SCREAM · en-US.txt:2778 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "My teeth hurt. Why do my TEETH hurt?" · SCENE_AVATAR_SCREAM · en-US.txt:2779 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "{Player}! Orders! Any orders! Please!" · SCENE_AVATAR_SCREAM · en-US.txt:2780 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I'm okay! I'm okay! I'm not okay! I'm shooting!" · SCENE_AVATAR_SCREAM|Greenhorn · en-US.txt:2781 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It screams because it's afraid of us! It's afraid of US!" · SCENE_AVATAR_SCREAM|Zealot · en-US.txt:2783 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "On your feet! It's just loud! Loud doesn't kill you!" · SCENE_AVATAR_SCREAM|Veteran · en-US.txt:2785 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Ears ring, hands don't! FIRE!" · SCENE_AVATAR_SCREAM|Veteran · en-US.txt:2786 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_FIGHT (12)
- "It doesn't bleed right! Why doesn't it bleed right?!" · SCENE_AVATAR_FIGHT · en-US.txt:2789 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Keep close to {Player}! Keep CLOSE!" · SCENE_AVATAR_FIGHT · en-US.txt:2790 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The ground's moving! The ground shouldn't move!" · SCENE_AVATAR_FIGHT · en-US.txt:2791 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I saw my own hands go dark. Are my hands still there?" · SCENE_AVATAR_FIGHT · en-US.txt:2792 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Don't look at the background! The background is looking back!" · SCENE_AVATAR_FIGHT · en-US.txt:2793 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's not even trying to hit me. It's trying to hit the WORLD." · SCENE_AVATAR_FIGHT · en-US.txt:2794 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: "not X, I'm Y" swap
- "Is this what the end of the world sounds like? It's so quiet between the screams." · SCENE_AVATAR_FIGHT|Greenhorn · en-US.txt:2799 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I counted the arms. I stopped counting the arms." · SCENE_AVATAR_FIGHT|Greenhorn · en-US.txt:2800 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Vorrath of the Thousand Teeth fears THIS. And we're shooting it!" · SCENE_AVATAR_FIGHT|Zealot · en-US.txt:2802 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Hold the faith! Hold the line! Hold SOMETHING!" · SCENE_AVATAR_FIGHT|Zealot · en-US.txt:2803 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Pick a spot on it and keep hitting that spot!" · SCENE_AVATAR_FIGHT|Veteran · en-US.txt:2806 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Breathe. Aim. It's big, so you won't miss." · SCENE_AVATAR_FIGHT|Veteran · en-US.txt:2807 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_COLD (5)
- "My breath is freezing! My EYELASHES are freezing!" · SCENE_AVATAR_COLD · en-US.txt:2810 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Where did the world go? It's all ice! It's all ICE!" · SCENE_AVATAR_COLD · en-US.txt:2811 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Keep moving or you'll stick! Keep MOVING!" · SCENE_AVATAR_COLD · en-US.txt:2812 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I've been cold before. Not like this. Not from the inside." · SCENE_AVATAR_COLD|Sellsword · en-US.txt:2813 · AI-WRITTEN · bossdialogue builder follow-up [7b1b8a5]; style: setup + short punchline
- "Watch the twinkles! The twinkles are where it hits!" · SCENE_AVATAR_COLD|Veteran · en-US.txt:2814 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_BLOOD (2)
- "It's raining blood! Whose blood?! WHOSE BLOOD?!" · SCENE_AVATAR_BLOOD · en-US.txt:2817 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's crying! The god is crying blood at us!" · SCENE_AVATAR_BLOOD · en-US.txt:2818 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_ATTACK (5)
- "It's breaking the world! It's breaking the actual world!" · SCENE_AVATAR_ATTACK · en-US.txt:2824 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Everybody MOVE! Wherever it's looking, MOVE!" · SCENE_AVATAR_ATTACK · en-US.txt:2825 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The sky just cracked! Like glass! Like GLASS!" · SCENE_AVATAR_ATTACK · en-US.txt:2826 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I don't know what that was but I'm lying down now!" · SCENE_AVATAR_ATTACK|Greenhorn · en-US.txt:2827 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Brace! Then shoot it while it's tired!" · SCENE_AVATAR_ATTACK|Veteran · en-US.txt:2828 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_HEADPATS (3)
- "It's patting the commander! Why is it patting the commander?!" · SCENE_AVATAR_HEADPATS · en-US.txt:2832 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Does that mean it likes us? Please let that mean it likes us." · SCENE_AVATAR_HEADPATS|Greenhorn · en-US.txt:2833 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Only the commander gets headpats from a god. As it should be!" · SCENE_AVATAR_HEADPATS|Zealot · en-US.txt:2835 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_UNIVERSE (5)
- "{Player}?! {Player}, where did you go?!" · SCENE_AVATAR_UNIVERSE · en-US.txt:2838 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It took the commander! It took {Player} somewhere!" · SCENE_AVATAR_UNIVERSE · en-US.txt:2839 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Everyone stay put! The commander always comes back!" · SCENE_AVATAR_UNIVERSE · en-US.txt:2840 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "If {Player} doesn't come back, I'm not working for anyone else. I mean it." · SCENE_AVATAR_UNIVERSE|Sellsword · en-US.txt:2842 · AI-WRITTEN · bossdialogue builder follow-up [7b1b8a5]; style: setup + short punchline
- "Hold position. When {Player}'s back, we'll be right here." · SCENE_AVATAR_UNIVERSE|Veteran · en-US.txt:2843 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_AVATAR_RETURN (3)
- "{Player}'s back! Did you see what's in there?!" · SCENE_AVATAR_RETURN · en-US.txt:2846 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "You're back! Don't tell us what was in there! Actually, tell us later!" · SCENE_AVATAR_RETURN · en-US.txt:2847 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Welcome back, {Player}. We held. Now let's finish it." · SCENE_AVATAR_RETURN|Veteran · en-US.txt:2849 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_PHASE3 (5)
- "It's angrier! How is it ANGRIER?!" · SCENE_AVATAR_PHASE3 · en-US.txt:2852 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "We're hurting it! You only scream like that when you're hurt!" · SCENE_AVATAR_PHASE3 · en-US.txt:2853 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Everything's changing again! Where are we now?!" · SCENE_AVATAR_PHASE3 · en-US.txt:2854 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's afraid! The god is afraid! Keep going!" · SCENE_AVATAR_PHASE3|Zealot · en-US.txt:2855 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Halfway's behind us! Don't you dare slow down now!" · SCENE_AVATAR_PHASE3|Veteran · en-US.txt:2857 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_HOPE (6)
- "It's slowing down! It's actually slowing down!" · SCENE_AVATAR_HOPE · en-US.txt:2860 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "We can do this! I can't believe I'm saying it! We can DO this!" · SCENE_AVATAR_HOPE · en-US.txt:2861 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Everything you've got! Everything! Now!" · SCENE_AVATAR_HOPE · en-US.txt:2862 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Is it dying?! Can it die?! Please let it die!" · SCENE_AVATAR_HOPE|Greenhorn · en-US.txt:2863 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The commander was right! The commander is always right! FIRE!" · SCENE_AVATAR_HOPE|Zealot · en-US.txt:2864 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's on its knees! Don't let it get up!" · SCENE_AVATAR_HOPE|Veteran · en-US.txt:2865 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_STATIC (5)
- "Run! The static! RUN!" · SCENE_AVATAR_STATIC · en-US.txt:2868 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Don't let it touch you! Don't let the gray touch you!" · SCENE_AVATAR_STATIC · en-US.txt:2869 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's eating the world! It's eating everything behind us!" · SCENE_AVATAR_STATIC · en-US.txt:2870 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Which way?! WHICH WAY?! There's only one way!" · SCENE_AVATAR_STATIC|Greenhorn · en-US.txt:2871 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Stay with the commander! Run where {Player} runs!" · SCENE_AVATAR_STATIC|Veteran · en-US.txt:2872 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_NAMELESS (6)
- "There's something else here. Something BIGGER." · SCENE_AVATAR_NAMELESS · en-US.txt:2875 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's bright. It's so bright. Who is that?" · SCENE_AVATAR_NAMELESS · en-US.txt:2876 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The static stopped. Something stopped the static." · SCENE_AVATAR_NAMELESS · en-US.txt:2877 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Is that one on our side? Please be on our side." · SCENE_AVATAR_NAMELESS|Greenhorn · en-US.txt:2878 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "That is not one of my gods. That's above my gods." · SCENE_AVATAR_NAMELESS|Zealot · en-US.txt:2879 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Nobody shoot that. Nobody shoot that until {Player} says." · SCENE_AVATAR_NAMELESS|Veteran · en-US.txt:2881 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_HOME (3)
- "We're home! That's the ground! That's OUR ground!" · SCENE_AVATAR_HOME · en-US.txt:2884 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I can see the sky! It's the real sky! Mostly!" · SCENE_AVATAR_HOME · en-US.txt:2885 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Count heads. Then count them again." · SCENE_AVATAR_HOME|Veteran · en-US.txt:2887 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_AVATAR_KILL (6)
- "It's gone! It's really gone! {Player}, it's GONE!" · SCENE_AVATAR_KILL · en-US.txt:2890 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "We beat it! We beat a GOD! What do we even do now?!" · SCENE_AVATAR_KILL · en-US.txt:2891 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Is it over? Somebody say it's over. Out loud." · SCENE_AVATAR_KILL · en-US.txt:2892 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The commander faced the void and the void LEFT! Glory to {Player}!" · SCENE_AVATAR_KILL|Zealot · en-US.txt:2896 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Good work. All of you. Every one of you." · SCENE_AVATAR_KILL|Veteran · en-US.txt:2898 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Weapons down. Breathe. You've earned the breath." · SCENE_AVATAR_KILL|Veteran · en-US.txt:2899 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_AWAKEN (7)
- "The stars are moving. The stars are moving AWAY." · SCENE_NAMELESS_AWAKEN · en-US.txt:2902 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "There's an eye. In everything. Looking at us." · SCENE_NAMELESS_AWAKEN · en-US.txt:2903 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's the bright one. From before. It's here for US now." · SCENE_NAMELESS_AWAKEN · en-US.txt:2904 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I don't think we're supposed to be here. I don't think anyone is." · SCENE_NAMELESS_AWAKEN|Greenhorn · en-US.txt:2905 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Every god I ever prayed to just went quiet." · SCENE_NAMELESS_AWAKEN|Zealot · en-US.txt:2906 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Whatever it is, it's not impressed with us." · SCENE_NAMELESS_AWAKEN|Cynic · en-US.txt:2907 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Steady. It's a fight. It's just a bigger fight." · SCENE_NAMELESS_AWAKEN|Veteran · en-US.txt:2908 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_SCREAM (5)
- "My ears! My EARS! Is it singing or screaming?!" · SCENE_NAMELESS_SCREAM · en-US.txt:2911 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Everyone up! Get up! It's starting!" · SCENE_NAMELESS_SCREAM · en-US.txt:2912 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I think I saw the whole world for a second. It was small." · SCENE_NAMELESS_SCREAM|Greenhorn · en-US.txt:2913 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "No amount. There is no amount of coin for this." · SCENE_NAMELESS_SCREAM|Sellsword · en-US.txt:2914 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "On your feet! It wants us on our knees, so on your FEET!" · SCENE_NAMELESS_SCREAM|Veteran · en-US.txt:2915 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_NAMELESS_FIGHT (7)
- "It's throwing stars! Actual stars! At us!" · SCENE_NAMELESS_FIGHT · en-US.txt:2918 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The sky keeps cutting open! Watch the cuts!" · SCENE_NAMELESS_FIGHT · en-US.txt:2919 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Stay near {Player}! {Player}'s the only thing that makes sense here!" · SCENE_NAMELESS_FIGHT · en-US.txt:2920 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "It's so beautiful. I hate how beautiful it is. Shoot it." · SCENE_NAMELESS_FIGHT · en-US.txt:2921 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I've stopped counting the danger. It's just all danger now." · SCENE_NAMELESS_FIGHT|Sellsword · en-US.txt:2926 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "K'Or'Arath said nothing about this one. Nothing!" · SCENE_NAMELESS_FIGHT|Zealot · en-US.txt:2927 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Don't watch the light show! Watch where it lands!" · SCENE_NAMELESS_FIGHT|Veteran · en-US.txt:2929 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_ATTACK (4)
- "That beam just split the world in half! MOVE!" · SCENE_NAMELESS_ATTACK · en-US.txt:2932 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's making a star! It's making a STAR in its hand!" · SCENE_NAMELESS_ATTACK · en-US.txt:2933 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "Everything went white! Is anyone still here?!" · SCENE_NAMELESS_ATTACK · en-US.txt:2934 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Get behind something! There's nothing! Get behind ME!" · SCENE_NAMELESS_ATTACK|Veteran · en-US.txt:2935 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_CLOCK (2)
- "Did time stop? I think time stopped. Is my heart still going?" · SCENE_NAMELESS_CLOCK · en-US.txt:2938 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "There's a clock! In the stars! It's counting us down!" · SCENE_NAMELESS_CLOCK · en-US.txt:2939 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_PHASE2 (4)
- "It's changing! Everything's changing again!" · SCENE_NAMELESS_PHASE2 · en-US.txt:2943 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "We hurt it! I felt it! I felt the whole sky flinch!" · SCENE_NAMELESS_PHASE2 · en-US.txt:2944 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It noticed us! The god noticed US!" · SCENE_NAMELESS_PHASE2|Zealot · en-US.txt:2945 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "It's taking us seriously now. Good. So are we." · SCENE_NAMELESS_PHASE2|Veteran · en-US.txt:2946 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_PHASE3 (4)
- "This is it! This is everything it has!" · SCENE_NAMELESS_PHASE3 · en-US.txt:2949 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Hold on! Whatever happens next, hold ON!" · SCENE_NAMELESS_PHASE3 · en-US.txt:2950 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I'm scared! I'm still shooting! I'm scared and I'm shooting!" · SCENE_NAMELESS_PHASE3|Greenhorn · en-US.txt:2951 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Last stretch. Every one of you, with me." · SCENE_NAMELESS_PHASE3|Veteran · en-US.txt:2952 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_HOPE (4)
- "It's fading! The light's fading! Keep going!" · SCENE_NAMELESS_HOPE · en-US.txt:2955 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "We're winning?! Against THAT?! Keep shooting!" · SCENE_NAMELESS_HOPE · en-US.txt:2956 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "No god above the commander! None! FIRE!" · SCENE_NAMELESS_HOPE|Zealot · en-US.txt:2957 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Almost! Don't you dare stop now!" · SCENE_NAMELESS_HOPE|Veteran · en-US.txt:2958 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_RESCUE (3)
- "It pulled {Player} out! The bright one pulled {Player} OUT!" · SCENE_NAMELESS_RESCUE · en-US.txt:2961 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Who IS that?! Why did it help us?!" · SCENE_NAMELESS_RESCUE · en-US.txt:2962 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Thank you! Whoever you are! Thank you!" · SCENE_NAMELESS_RESCUE|Greenhorn · en-US.txt:2963 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_KILL (5)
- "It's over. It's really over. The light went out." · SCENE_NAMELESS_KILL · en-US.txt:2967 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "We beat the thing above the gods! {Player}, we BEAT it!" · SCENE_NAMELESS_KILL · en-US.txt:2968 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "I'm going to tell everyone. Nobody's going to believe me. I don't believe me." · SCENE_NAMELESS_KILL|Greenhorn · en-US.txt:2969 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Every god bows to {Player} now! Every one!" · SCENE_NAMELESS_KILL|Zealot · en-US.txt:2970 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "That's the last one. Rest, all of you. That's an order." · SCENE_NAMELESS_KILL|Veteran · en-US.txt:2972 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_NAMELESS_HOMECOMING (5)
- "{Player}! You're back! What happened in there?!" · SCENE_NAMELESS_HOMECOMING · en-US.txt:2975 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "You did it! Without us! With us! In spirit! You did it!" · SCENE_NAMELESS_HOMECOMING · en-US.txt:2976 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "You went somewhere we couldn't follow, and you came back. Welcome home." · SCENE_NAMELESS_HOMECOMING · en-US.txt:2977 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The commander walked among the gods and came home! I'll tell my children!" · SCENE_NAMELESS_HOMECOMING|Zealot · en-US.txt:2980 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Welcome back, {Player}. The army's all here. We kept it all here." · SCENE_NAMELESS_HOMECOMING|Veteran · en-US.txt:2982 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### SCENE_WOTG_COMMANDER_DOWN (7)
- "{Player}! No! NO! Get up!" · SCENE_WOTG_COMMANDER_DOWN · en-US.txt:2985 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "The commander's down! Hold the line! HOLD THE LINE!" · SCENE_WOTG_COMMANDER_DOWN · en-US.txt:2986 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Nobody runs! {Player} always comes back!" · SCENE_WOTG_COMMANDER_DOWN · en-US.txt:2987 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "What do we do?! What do we do without {Player}?!" · SCENE_WOTG_COMMANDER_DOWN|Greenhorn · en-US.txt:2988 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Cover the spot! Nobody else here knows the plan!" · SCENE_WOTG_COMMANDER_DOWN|Sellsword · en-US.txt:2989 · AI-WRITTEN · bossdialogue builder follow-up [7b1b8a5]; style: setup + short punchline
- "The commander falls and rises! Hold until the commander rises!" · SCENE_WOTG_COMMANDER_DOWN|Zealot · en-US.txt:2990 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Close ranks! We hold here till {Player}'s back on their feet!" · SCENE_WOTG_COMMANDER_DOWN|Veteran · en-US.txt:2992 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]

### SCENE_WOTG_MOURN (6)
- "{Comrade} didn't get to see this. Somebody tell {Comrade} when they're back." · SCENE_WOTG_MOURN · en-US.txt:2995 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "This one's for {Comrade}. And everyone who went down with them." · SCENE_WOTG_MOURN · en-US.txt:2996 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]
- "{Comrade} was right next to me. Then {Comrade} wasn't." · SCENE_WOTG_MOURN|Greenhorn · en-US.txt:2997 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "{Comrade}'s share stays {Comrade}'s. I'll hold it till they're back." · SCENE_WOTG_MOURN|Sellsword · en-US.txt:2998 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "{Comrade} fell facing a god. There's no better way to fall." · SCENE_WOTG_MOURN|Zealot · en-US.txt:2999 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline
- "Remember {Comrade}. Not the fall. The fight." · SCENE_WOTG_MOURN|Veteran · en-US.txt:3001 · AI-WRITTEN · bossdialogue builder (WotG fight lines); ROADMAP: "builder-written" [313d9e1]; style: setup + short punchline

### BossLines.hjson (Calamity SCal first battle and Draedon, AI-adapted) (22)
- "Do you enjoy dragging all of them through hell?" · BossLines.hjson SCalSummonText · BossLines.hjson:9 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "You should have just died... all of you." · BossLines.hjson SCalStartText · BossLines.hjson:10 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "It wasn't too long ago you and your rabble barely managed to defeat my doppelganger. Quite the failure, wasn't it?" · BossLines.hjson SCalBH2Text · BossLines.hjson:11 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "You take all this power, and what do you do with it? Build an army?" · BossLines.hjson SCalBH3Text · BossLines.hjson:12 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "You brought your family. Would you like to meet mine? Horrible, isn't it?" · BossLines.hjson SCalBrothersText · BossLines.hjson:13 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "You will all suffer greatly." · BossLines.hjson SCalPhase2Text · BossLines.hjson:14 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "It's absurd to even think of trying to get away. As long as any of you live, you will suffer." · BossLines.hjson SCalBH4Text · BossLines.hjson:15 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "An upstart who stole and killed their way to power, then armed a following with it. I wonder, who does that remind me of...?" · BossLines.hjson SCalSeekerRingText · BossLines.hjson:16 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "None of you have any stake in this battle. No one gave you any say in this matter!" · BossLines.hjson SCalBH5Text · BossLines.hjson:17 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "Once the dust has settled and only your army remains, what value will this have had?!" · BossLines.hjson SCalSepulcher2Text · BossLines.hjson:18 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "I have no more energy left to resent him, or you, or any of them..." · BossLines.hjson SCalAcceptanceText2 · BossLines.hjson:19 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "It will all be in your hands now. Yours, and theirs." · BossLines.hjson SCalAcceptanceText3 · BossLines.hjson:20 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army" (an AI rewrite of Calamity's own boss text for an army; not jokes)
- "Your nature fascinates me. You gain all this power, and you give it away. I do not understand it." · BossLines.hjson DraedonIntroductionText2 · BossLines.hjson:32 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "You and your soldiers will face my creations, which have surpassed gods." · BossLines.hjson DraedonIntroductionText3 · BossLines.hjson:33 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "And you will show me your disposition, and theirs, through battle." · BossLines.hjson DraedonIntroductionText4 · BossLines.hjson:34 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "Your army's performance falls neatly within the margins of error." · BossLines.hjson DraedonExoPhase2Text1 · BossLines.hjson:36 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "Ever since I was alerted to your presence, I have processed your battles, and your army's, in order to make my machines stronger." · BossLines.hjson DraedonExoPhase3Text1 · BossLines.hjson:37 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "Even now, I monitor every one of you. Nothing should escape the bounds of my calculations." · BossLines.hjson DraedonExoPhase3Text2 · BossLines.hjson:38 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "You and your soldiers progress steadily against more difficult challenges." · BossLines.hjson DraedonExoPhase4Text2 · BossLines.hjson:39 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "Your nature remains unknown to me. They follow you with a loyalty I have seen once before, and even he did not give away this much." · BossLines.hjson DraedonExoPhase5Text1 · BossLines.hjson:40 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "An unknown factor--a catalyst is what you are. And you have made catalysts of all of them." · BossLines.hjson DraedonEndText1 · BossLines.hjson:42 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself
- "You and yours have also spilled blood, but it may be enough to usher a new age... Of what, I do not know. But it is something I am eager to see." · BossLines.hjson DraedonEndText5 · BossLines.hjson:43 · AI-WRITTEN · commit 313d9e1 "boss lines that fit an army"; ROADMAP: Draedon army lines are "builder-written", currently HELD (unused) because Eve will write his lines herself

## Hers (documented as Eve's, for reference; not on the rewrite list)
- "Man, this would be even scarier if we could actually feel pain." · IDLE · en-US.txt:197 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "When we finally get the ability to sleep, I'm going to use every piece of silk on the planet." · IDLE · en-US.txt:198 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "Do we get to keep the money we find lying around?" · IDLE · en-US.txt:199 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[fragile] What's even the point of this armor? Most of us die in one hit anyway!" · IDLE · en-US.txt:200 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[summoner] W-what? No, I'm not a summoner! You must be imagining things!" · IDLE · en-US.txt:201 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[non-summoner] Can't believe we have to work with... summoners." · IDLE · en-US.txt:202 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "{Tank} is really good at tanking hits. I mean, {TankHe} already died {TankDeaths} times. What a badass!" · IDLE · en-US.txt:203 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "Can't believe {Glutton} is so greedy! They've already killed {GluttonKills} monsters! Leave some for the rest of us!" · IDLE · en-US.txt:204 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "Not sure why brain chips were part of the signing agreement, but the music sounds pretty nice!" · IDLE · en-US.txt:206 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[non-summoner] Why do those "summoners" carry staves around everywhere? They're not impressing anyone, I'll tell you that much." · IDLE · en-US.txt:207 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[summoner] When I summon a minion, it takes effort! It's still a swing! I just do it once instead of constantly, which gets the same amount of work done, just easier! It-it takes a ton of effort and skill! Plus I use a whip! Sometimes! If it's available!" · IDLE · en-US.txt:208 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[non-summoner] Summon slop shouldn't be in the same group as us, but whatever." · IDLE · en-US.txt:209 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[non-summoner] Imagine how much mana is being used for summon weapons! It's taking all of the mana from the atmosphere! It's probably adding to the mana sickness!" · IDLE · en-US.txt:210 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[summoner] Why are people complaining so much about us? People used to talk the same way about rangers, and now they're part of everything!" · IDLE · en-US.txt:211 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[non-summoner] How long until we can't tell summons apart from real fighters? They're replacing us!" · IDLE · en-US.txt:212 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "I love this new neural implant. I love my employer. I love fighting. I love dodging. I love-" · IDLE · en-US.txt:213 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "When I signed up for my contract, I didn't realize it'd instantly teleport me to {WorldName}." · IDLE · en-US.txt:214 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[hardmode+officer] Why is that {Officer} a higher rank than us? We have enough firepower to level a mountain!" · IDLE · en-US.txt:215 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "Am I supposed to be seeing... myself from above?" · IDLE · en-US.txt:216 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[goblin] What do you mean that goblin earns more in a day than we do in a year???" · IDLE · en-US.txt:217 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "Regenerative immortality, superhuman abilities and magic attacks powerful enough to level a city is honestly a good deal, even if you lose SOME of your free will!" · IDLE · en-US.txt:218 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[boss] I like the music you play when we fight the boss! Helps distract from the routine death and violence!" · IDLE · en-US.txt:219 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "[lowhp] Is this what the people talking about "the thrill" meant? This is great." · IDLE · en-US.txt:220 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "Summoner son or mage daughter? Probably summoner son, since at least they'll be earning a bunch of money. Mage daughter is gonna keep draining all the treasury from all the recruiting drives." · IDLE · en-US.txt:221 · HERS · Eve's own line (ROADMAP: "all of her lines", chatter batches 1-2, commits 3f24205/35ecda3)
- "..." · RIFT_IDLE · en-US.txt:2310 · HERS · best guess: "..." is one of her own example lines (ROADMAP: "her example lines replaced (kept 2)"); which 2 were kept is not recorded, so this is a best guess
- "So... what now, exactly?" · RIFT_IDLE · en-US.txt:2313 · HERS · best guess: her own example line "So... what now, exactly?" (ROADMAP: "kept 2"); best guess which two were kept
- "Hm? You want me to fight all of you? So be it." · BossLines.hjson SCalSummonTextRematch · BossLines.hjson:22 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "When the time comes, would you all wish to be part of my creation?" · BossLines.hjson SCalStartTextRematch · BossLines.hjson:23 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "Don't expect this to be easy. I have destroyed armies like these many times." · BossLines.hjson SCalBH2TextRematch · BossLines.hjson:24 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "I haven't had such interesting target dummies to test my magic on in a long time." · BossLines.hjson SCalBH3TextRematch · BossLines.hjson:25 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "Most soldiers were terrified of fighting me. I wonder if they are too?" · BossLines.hjson SCalBH4TextRematch · BossLines.hjson:26 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "Their skill is incredible. You have fostered something truly immense." · BossLines.hjson SCalSeekerRingTextRematch · BossLines.hjson:27 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "An incredible display of discipline. I concede this match." · BossLines.hjson SCalDesparationText1Rematch · BossLines.hjson:28 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "With their strength alone, you would no doubt face a greater threat than I." · BossLines.hjson SCalDesparationText2Rematch · BossLines.hjson:29 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- "I cannot imagine what comes next for someone of your caliber." · BossLines.hjson SCalDesparationText4Rematch · BossLines.hjson:30 · HERS · commit 6c083dd "Supreme Calamitas rematch lines in the designer's words"
- Potions.hjson LostBet.Bet1-Bet5 ("lost the bet", "realized prolonged poverty would kill them", "watched their favorite team lose", "realized the levies did not know what dashes were", "had money on the levies") · her ideas per ROADMAP "Death messages from Eve's ideas" · HERS
