# Eve's asks audit (147 messages, 2026-11-13 to 11-20)

Method: read all 147 messages in order. Checked each distinct ask against ROADMAP.md (line numbers are `grep -n` numbers), `git log -400` plus all branches, NORTH_STAR.md, and the code under `levy-mod/Levy`, `warbands-mod`, `mechanization-mod`.

Limits of this audit:
- I cannot see chat replies. Where ROADMAP says "told Eve" I count it as answered. Where there is no record, I say so.
- Status is the primary one per ask. Mixed cases are noted.
- "Merge hash" means the merge commit. "Version" means the Levy build that carried it.

The ROADMAP bookkeeping is unusually good. Almost every ask has a line. The MISSING list is therefore short, and several of its items could have been answered in chat without a trace.

---

## 1. MISSING (no trace in ROADMAP actions, commits or code)

### M1. Separate "fixed" replay artifact (11-15 15:42), probably silently dropped
- Her words: "You should probably include this in its own artifacts called the fixed artifact, so I don't have to keep pulling up broken things."
- Related ask the same minute (11-15 15:33): "include a version/outdated stamp + clearing out some of the old ones". That part is DONE (sitecurate 134722e, badges driven by manifest `current` and `eras`, 277 old files removed).
- Why I think it is missing: ROADMAP lines 270 ("Then: the Fixed replay page") and 337 ("a separate page holding ONLY fights recorded on fixed versions... Fill it after parity lands") plan it. Nothing after that mentions it. `grep -i "fixed artifact|Fixed replay|fixed-only"` finds only those two lines plus the plan.
- The artifacts that exist: the main "Levy Fight Replays" page (stamped), the Fight Viewer, the Lab Replays page, and the PvP page (deleted and merged).
- The stamps are a partial substitute, but she never agreed to swap one for the other.
- Related 11-20 01:15 complaint: "2 artifacts, or 3 artifacts for the Live Fight Recorder... really confusing on which one's which". This is also unresolved (see Q8).

### M2. Title-screen tool advice (11-14 18:57)
- Her words: "making a new title screen for levy and warbands and mechanization. It's not that hard... let me know if there's any easy tool I could do to make that and what you would suggest."
- Why: the only trace is the phrase "title screens (tool advice)" inside the DESIGN QUEUE line (ROADMAP 4460).
- No doc and no commit. `grep -ri "title screen"` over levy-mod, docs and git log finds nothing relevant.
- She may have been answered in chat. There is no record.

### M3. Weapon graphics audit (11-14 18:57)
- Her words: "a big graphical overhaul of all the weapons we already have... I just want to know if there's anything, like, super obviously not like Terraria that would be worth covering."
- Why: only the DESIGN QUEUE phrase "weapon art audit for anything non-Terraria" (ROADMAP 4460).
- No audit doc. `levy-mod/Levy/WEAPON_AUDIT.md` is about weapon behaviour, not art. `grep "art audit|weapon art|non-Terraria"` finds nothing else.

### M4. Explain Wrath of the Gods book text (11-17 06:00)
- Her words: "There's a lot of text in Wrath of the Gods that doesn't make much sense to me, especially in the books. Do you mind explaining some of those to me?"
- Why: ROADMAP 191 records it ("read the localization once installed; explain in chat; never paste into git").
- No later line says it was done. Chat-only delivery is possible, but nothing shows it happened.
- She said on 11-18 01:49 "feel free to spoil, i've already played WOTGs twice", so spoiler limits no longer block it.

### M5. Providence drops easier / in bulk (11-19 19:24), low confidence
- Her words: "I just want to make it so you can get, like, drops from, like, Providence easier... there's plenty of drops that... are very difficult to get in bulk."
- Why: the progression-assistance list (ROADMAP 130-149: Exodium magnet, scoria crafting, slime agitator, ore options, Uelibloom supply) does not name Providence drops.
- The unmerged `wotgcompat` branch touches `ProgressionDrops.cs` ("report the drops that lost the army bonus", 4803352). That may be related, but it is not a feature for this ask.
- It may be intended as part of "Levy progression assistance". Nobody wrote it down as such.

### M6. Small questions with no record of an answer
Not features. Listed because the answer may not have reached her.

| Date | Question |
|---|---|
| 11-19 20:37 | "how long do you put it until the next version comes out" |
| 11-19 19:24 | "Are there any specific mods I'm using that might just be crashing the game?" |
| 11-18 16:14 | "I wonder how those [3 FPS fights] look on the recorder. Do they go in real time?" |
| 11-17 05:12 | Is Calamity going to be maintained for 1.4.5 or 1.4.7? (not about Levy) |

ROADMAP 149 does hold one open question that needs her answer: should a Calamity permanent max HP or mana item used by the commander raise every soldier? See the QUEUED list.

### Standing rules that live only in ROADMAP, not NORTH_STAR (she said "keep doing that")
Context-reset risk. NORTH_STAR has no "spoil" match.
- **No spoilers** for mods she has not played (Redemption, Stars Above, Starlight River, The Forgotten): 11-15, 11-17 06:29. ROADMAP 184 and 374 only.
- **Ask first before anything that changes balance significantly:** 11-19 19:24, "make sure to keep doing that". ROADMAP 142 and 145 only.
- **Check a feature already exists and do not overwrite built code** (Nebula boosters, Vortex Booster, movement code): 11-19 18:58 and 19:07. ROADMAP 142 only.
- **Treat all queued messages since the last reset as one unified message**, and read every one: 11-16, 11-17 23:15, 11-18 01:44. In neither file as a rule.

---

## 2. QUEUED items that waited longest or were repeated

Ordered by importance signal. "Repeated" means she brought it up more than once.

1. **Mass fishing with levies** (11-14 18:57, details 11-14 19:06). "Please, game, I need this." Strongest wording of any idea. Only in the DESIGN QUEUE line (ROADMAP 4460), with no builder, six days on. Includes beer station, magnet buff item, and low-tier rods getting a use.
2. **Drone factory** (11-14 18:57, again 11-17 06:00, "I like that idea"). Repeated, specced twice: 10-15 drones, switch-activated, banner patrol paths. Design queue only (ROADMAP 187 and 4460).
3. **Reserve bank and active army cap** (11-16 18:34, 11-16 21:08, 11-17 03:25, 11-19 18:47, 11-19 18:58). Five repeats. Now BUILDING (activecap). It sat at the back of the backlog for three days until the 11-19 push.
4. **Levy requests** (11-19 18:47, 11-19 18:58): a "?" over the head, simple asks (graves, grass, an apple), any levy thanks you, small reward. Repeated. ROADMAP 140 and 147. Not built.
5. **Wiki on wiki.gg plus in-game "O" key** (11-18 00:18, 11-19 18:47, 11-19). Repeated. ROADMAP 132. Not started.
6. **Steam page rewrite, plus "go through all the features that warrant attention"** (11-19 18:47, 11-19 19:24). Repeated. ROADMAP 132. Not started.
7. **Subworld following** (11-17 03:35, 11-19 18:47). Repeated. The design doc exists only on unmerged `wotgcompat` (7128dea, `docs/SUBWORLD_FOLLOW.md`).
8. **Angler fix** (more normal quests during the rift; 11-17 23:14, 11-18 00:18, 11-19 18:58, 11-19 19:07). Repeated four times. Her final spec: inside Levy as an option, separate from the QoL mod. Not built.
9. **Progression assistance in the progression menu** (Exodium "superpowered magnet", scoria from other ores, slime agitator, ore multiplier popup, Auric regeneration, Uelibloom): 11-17 23:14, 11-18 00:18, 11-19 18:47, 11-19 19:24. Not built.
10. **Set-matching wings** (11-19 18:47, 18:58). Repeated. ROADMAP 129. No code (`grep` for wing-set logic finds only team accessories).
11. **Stars Above and Starlight River compat** (11-17 06:00, 06:10). The `starsriver` branch has 11 commits not on HEAD (tip 0598dbd). Paused 11-18 and never resumed, although ROADMAP 111 and 116 say "resume starsriver".
12. **WotG compat branch `wotgcompat`**, 7 commits not on HEAD (tip 303acc5):
    - It contains "levies hush through Wrath of the Gods final bosses" (de1e1d8), which is her 11-17 06:00 ask. Built, not merged, so it is not in 3.1.5.
    - It also holds the subworld proposal and the buff-array fix.
    - Custom death messages for those bosses are on main (bandsdeath 3.0.9). The "shut up" half is not.
13. **Avatar of Emptiness and Nameless Deity test fights** on Master+Death and Expert+Rev (11-17 06:00). Only the `noxus` recon part exists. No results were reported to her. She expected "all levies die in 3 hits".
14. **Mod-rule bug: "only guns" rule ignored by levies** (11-17 06:00). ROADMAP 192 says "bug, armory batch". armory2 and armory2fix* merged with no rule fix, and no commit mentions it. Silently forgotten.
15. **EoW A/B: old Levy versus current** (11-17 05:01, 11-19 18:47). ROADMAP 35. Never run.
16. **≥100 fights per boss on the newest build, equalized** (11-15 13:47). hmbattery2 ran 344 fights across 22 bosses, and Destroyer got a rigorous 140, but there is no per-boss 100. ROADMAP 442 and 389.
17. **Field Manual audit** (11-20 01:26): rally, orders, active army size. Queued at ROADMAP 163.
18. **Acid Rain ends in about 0.2 s with levies** (11-19 18:47). "QUEUED bug … investigate" (ROADMAP 130). Never investigated.
19. **Dialogue round 3** (11-19 19:24), all still queued:
    - Cosmolite, Rod of Discord and Relocator reactions.
    - Fishing-rod comments, and "150 projectiles, I can't see anything".
    - Spawn-rate potion comments, and a levy running into a wall.
    - Rogue as noble and over-righteous.
    - Stink, love and slime-gun reactions.
    - The Bandit having no unique dialogue.
    - Class and healer lines.
    - Levies sitting down.
20. **Combat items from 11-19 19:24**: adrenaline only while fighting a boss, sniper levies engaging from far back, officers leaving a "flag" that acts as a remote pylon. ROADMAP 148. Not built.
21. **Magic Storage crafting UI highlights armory versus levy-held items, right-click to exclude** (11-19 18:47). ROADMAP 129. Not built. The 3.1.5 stopgap hides armory copies and does not do this.
22. **Default levy cap lower?** (11-14 18:57). A DESIGN QUEUE question. Superseded in practice by the active cap, but never answered.
23. **Drones all use one weapon** (11-13). Answered "by design" (field drones each take the best spare). The decision to spread weapons is waiting on her.
24. **Boss scaling default multiplier** (ROADMAP 168, "≈1.25 for +100%?") is waiting on her answer.
25. **Conference Call follows her cursor** (11-17 17:56). Not reproduced headless (deadhooks e3da2ca). Waiting for a repro from her. The armory now also demotes it.
26. **Cleanup of confusing replay artifacts** (11-20 01:15). The Lab page (5bQWET...) is still up. ROADMAP offered to delete it and she has not been asked again.
27. **Turret sprites "should look a little better"** (11-14 18:57). dronefx logged "turret sprite notes" only. No art done.

---

## 3. Everything else

Status key: DONE, BUILDING, QUEUED, SUPERSEDED, ANSWERED, MISSING (the M-items above).

| # | Ask | Date | Status | Evidence |
|---|---|---|---|---|
| 1 | Armory Relay unlimited reach, serve radius 50, drones placeable midair or on platforms | 11-13 | DONE | relayreach 363939c, Levy 2.0.59 / Mech 0.10.6 |
| 2 | Drones keep patrolling (no hold mode) | 11-13 | DONE (nothing to build) | ROADMAP 4437 |
| 3 | Turrets won't place on platforms | 11-13 | DONE | dronefx a2ed01d, Mech 0.10.8 |
| 4 | Show which weapons levies hold (armory background highlight) | 11-13 | DONE | gearcadence 658adb8, 2.0.61 (carried green, lent amber, hover names) |
| 5 | Select-matching freeze, game laggy | 11-13 | DONE | perfsell cc92f28, 2.0.60 |
| 6 | Remove the dedicated summon slot; sidearm summon only with an extra summon slot (11-13 19:05) | 11-13 | SUPERSEDED | 11-14 18:57 ("hold a summon sidearm if they have a free space"). Summon rule v3, armoryux2 d87658c, 2.0.62 |
| 7 | Why do you need Thorium / get it yourself | 11-14 | DONE | anonymous DepotDownloader, Eve approved, ROADMAP 4448 |
| 8 | Fix LuiAFK shops and Arena builder, AlchemistNPC chooser | 11-14 | DONE (open-shop never reproduced) | luiafk 846cb8b (2.0.60), identity 1d2b1f9 (2.0.61) |
| 9 | Big pylon menu lags the map (11-14, again 11-16 21:10) | 11-14 | DONE | Cause is Magic Storage's remote range overlay, not ours. Workaround given, plus a perfmap teleport fix (3.0.3) |
| 10 | Accessory checks every 10-15 s per levy | 11-14 | DONE | gearcadence 658adb8, 2.0.61 |
| 11 | Weapons that freeze the player: fix the core issue | 11-14 | DONE | identity 1d2b1f9, 2.0.61 |
| 12 | Levies spread across multiple platforms | 11-14 | DONE | platspread 78fadd3, 2.0.61 |
| 13 | Ring formation around the commander for photos | 11-14 | DONE | ringwof 5103752, 2.0.62 |
| 14 | Mobilize/demobilize/remobilize buttons in barracks, not radio; radio no minimum range | 11-14 | DONE | armoryux2 d87658c, 2.0.62. No range limit found in code, only stale "150 tiles" text fixed |
| 15 | Shift-click items (GoPro) into the armory; side slots are "preferred" weapons | 11-14 | DONE | armoryux2 d87658c. Her correction matched |
| 16 | Logistics and search lag; ore outline softer | 11-14 | DONE | armoryux2 (CachedStock; slider 0.35) |
| 17 | Logistics: send items to Magic Storage or a chest, deposit non-army junk | 11-14 | DONE | armoryux2, 2.0.62 ("To storage" plus Storage page) |
| 18 | Wall of Flesh doesn't target levies (her correction: not the radio range) | 11-14 | DONE | ringwof 2.0.62 plus projparity 3f8bed0 (3.0.1) |
| 19 | Whips: use the real whip code plus tag as damage boost | 11-14 | DONE | whipreal 737f835 |
| 20 | Drones and turrets fire the weapon's real shot; vanilla health bar, red tint, beep, cartoon explosion | 11-14 | DONE | dronefx a2ed01d, 2.0.62 |
| 21 | Officers join the pylon network (recruiting) | 11-14 | DONE | officers2 db7c2c6. She did not want pylon right-click to open hire: pylonfix 6469b75 removed it |
| 22 | Pylon opens hire screen; hire screen can't scroll | 11-14 | DONE | pylonfix 6469b75 (2.0.62/63) |
| 23 | Clothier summons tier-appropriate skeletons; Zoologist summons strongest bestiary creature | 11-14 | DONE | officers2 db7c2c6; conjurefix 61e0e5f |
| 24 | Clothier never stops raising skeletons in combat; they crumble unless they deal damage | 11-14 | DONE | conjurefix 61e0e5f (2.0.63). `ClothierOfficer.cs` matches |
| 25 | Conjured archer shooting her for 0 damage (she corrected: skeleton archer, not fire imp) | 11-14 | DONE | conjurefix 61e0e5f |
| 26 | Opening any NPC shop lags | 11-14 | DONE | shoplag a43f9ae, 2.0.63 |
| 27 | Never let a levy use tile-destroying items; armory warning | 11-14 | DONE | shoplag a43f9ae (2.0.63). Later warning wording and Cancel: armoryui 3fa3b43 (3.0.2) |
| 28 | `/levyprof` impractical mid-fight; chat lost on death | 11-14 | DONE | profkey 794e2ec (keybind, auto-profile), `/levyproof` alias |
| 29 | Record fights automatically, route them to me | 11-14 | DONE | autorec 3b8ed23 (format 8, Drive folder) |
| 30 | Load recordings in the fight viewer; synced folder | 11-15 | DONE | viewer8 7fc2146, artifact published |
| 31 | Replay page: "undefined" crash, update replays, include other bosses | 11-15 | DONE | site v18, v24-v28, make_site fix (ROADMAP 569) |
| 32 | Curated replay list with 3-4 wins and losses plus weird ones | 11-15 | DONE | sitecurate 134722e, v24 |
| 33 | Levies bad vs Leviathan and Anahita (dash kills all) | 11-15 | DONE | leviathan cc8cdb0, 2.0.64 |
| 34 | Leviathan targets the player in lab replays | 11-15 | DONE | spectarget (2.0.67) |
| 35 | Plantera should not target levies before first beaten | 11-15 | SUPERSEDED | 11-15 04:10 "Already built into the game... only target... if not already beaten" |
| 36 | Levy morale visible on screen / auto-rally | 11-15 | SUPERSEDED | 11-17 04:57 "We don't need calling in the army... Rally NO". An on-screen morale readout alone remains an open idea (ROADMAP 560) |
| 37 | Thorns and vines break on levy contact | 11-15 | DONE | fieldfix 5bedbe5, 2.0.65 |
| 38 | Banner when a levy or NPC is hired away | 11-15 | DONE | fieldfix 63fadf0 |
| 39 | Following NPCs "weird" compared to levies | 11-15 | DONE | fieldfix 1ac2603 (officers use levy targeting rules) |
| 40 | Wings plus boots slots, horizontal and vertical mobility | 11-14 | DONE | mobslots, 2.0.65 (repeated 11-15, 11-17) |
| 41 | Redemption support, tell her if brain-breaking (no spoilers) | 11-15 | DONE | redecompat (3.0.0), redemption_compat_audit.md |
| 42 | Run Destroyer, Aquatic Scourge and other baselines rigorously | 11-14, 11-15 | DONE | hmreplays 155bc46, hmbattery2 fc48ec2; Destroyer 140 fights. Per-boss 100 is QUEUED (item 16 above) |
| 43 | Report how each fight goes, especially Empress of Light | 11-14 | DONE | bossbiome a1f7608 (night 11/12, day 0/12), ROADMAP 4474-4481 |
| 44 | Projectile damage parity ("2-damage darts") | 11-15 | DONE | projparity 3f8bed0, 3.0.0/3.0.1. Chased her "other inconsistencies" request (parity audit a49dc30) |
| 45 | Knockback, regen, potion rate, drowning as setting, no 0.8 damage, real whip, armor set bonuses, armor break | 11-15 | DONE | levyrules 625fd71 (3.0.0) |
| 46 | Confinement box fix (Calamitas Clone) | 11-15 | DONE | projparity ArenaBox |
| 47 | Spectator: all levies dead and no manpower means you die | 11-15 | DONE | spectator3 d5a0c0d (3.0.0) |
| 48 | Death messages from her ideas | 11-15 | DONE | spectator3 (3.0.0); her five lines |
| 49 | Officers talk like their NPC; "gods agree" at normal rate; female levies replace dialogue on Blood Moon | 11-15 | DONE | dialogue3 (merge 2a2f675), 3.0.0/3.0.1 |
| 50 | Dialogue rewrite: sincere, tier-scaled, drink lines, armory comments, excitement at chests and life crystals, class rivalry | 11-14 | DONE | dialogue3 plus dialogue4, 3.0.0/3.0.1 |
| 51 | Battle shouts punchier, enthusiastic | 11-15 | DONE | dialogue4, 3.0.1 |
| 52 | Ship 3.0, "can I have 3.0?" | 11-15 | DONE | 3d9f4c2, Levy 3.0.0 |
| 53 | Weapons fire from her instead of the levy (Core of Calamity weapon, Great Sand Shark tumbleweed) | 11-16 | DONE | weaponorigin 01aaf96, 3.0.7 |
| 54 | Boss cyberattack lag even when idle, with map lag | 11-16 | DONE | perfboss 01faccf (3.0.4-3.0.6) |
| 55 | Block-breaking warning: "HEY THIS WILL BREAK BLOCKS", cancel option | 11-16 | DONE | armoryui 3fa3b43, 3.0.2 |
| 56 | Take weapons from the armory screen even if levy-held | 11-16 | DONE | armoryui 3fa3b43, 3.0.2 |
| 57 | Post-fight lag popup with options | 11-16 | DONE | perfanalyzer 5260d38, 3.0.8 (reserve option hidden until activecap) |
| 58 | Post-fight report, fight analyzer, "the line is breaking" text | 11-16 | DONE | fightreport 126cbe7, 3.0.5 |
| 59 | Always-on profiler, live updates through Drive | 11-16 | DONE | perfanalyzer 3.0.8 (perf/ folder) |
| 60 | Text box can't delete the "type here" number | 11-16 | DONE | numfield e161c1f, 3.0.3 |
| 61 | Spectator drones: bosses target drones forever, so the bet never fires | 11-16 | ANSWERED | Bet already ignores drones (proved). Plus bosstarget (below) |
| 62 | Summoners re-summon minions for no reason | 11-16 | DONE | weaponorigin 01aaf96, 3.0.7 |
| 63 | Sea mines (Dreadmine) lag | 11-16 | ANSWERED | Levies follow player rules; lag is client draw; not reduced (tenet). No change made |
| 64 | Progression-break check (Astral, Scoria, Death Whistle) | 11-16 | DONE | Report in CALAMITY_REPORT_NOTES.md; Calamity's, not ours |
| 65 | Star shower and mind control wonky | 11-17 | DONE | deusmind f45c438, 3.1.0 |
| 66 | Regroup and engage order | 11-17 | DONE | regroup 3c59e92, 3.0.9 |
| 67 | Spectator cancelled mid-fight means "forfeited" death | 11-17 | DONE | regroup 3c59e92, 3.0.9 |
| 68 | Reserve bank "in the back" | 11-17 | SUPERSEDED | 11-19: she moved it to the front. See BUILDING |
| 69 | Custom death messages (plague, brimstone), 100% in Nameless and Avatar | 11-17 | DONE | bandsdeath 20d3330, 3.0.9 |
| 70 | Warbands: hold during boss fights, no Terra Blade, no friendly fire, sandstorm raiders | 11-17 | DONE | bandsdeath 20d3330, 3.0.9 |
| 71 | Bands pursue, dodge, readable non-overlapping chatter | 11-14 | DONE | warbands5, Warbands 0.6.0 |
| 72 | Megagun: hold-down weapons fire from her | 11-17 | DONE | megagun 9f925ca (3.0.9). Her remaining flamethrower cases routed to deadhooks e3da2ca |
| 73 | Recorder: deaths read as "fled", Cultist win missing, commander projectiles | 11-17 | DONE | recfix cee4ec8, 3.0.9 |
| 74 | Recorder terrain follows fight; far levies shown | 11-17 | DONE | recorder2 ee1dbba, viewerfix 0c25827 |
| 75 | Event recordings (invasions, moons, pillars) | 11-17 | DONE | recorder2 ee1dbba, format 12 |
| 76 | Viewer broken | 11-17 | DONE | viewerfix 0c25827, site v27 |
| 77 | Lore document | 11-17 | DONE | `docs/LORE_NOTES.md` (also 69223e0 Moonstone Crown) |
| 78 | Put test-for-hypotheticals and remember-earlier-chats in NORTH_STAR | 11-17 | DONE | NORTH_STAR 4a and 6 |
| 79 | Levies run from other levies when targeted; asphalt speed; dash in horizontal slot | 11-17 | DONE | bodyguard 8075f54, 3.1.0 |
| 80 | Levies pick "boring" weapons over Terra Blade or biome blades; post-ML weapons should win | 11-17 | DONE | armory2 eb93e2c, 3.1.2 |
| 81 | Magic Storage compat: craft and upgrade from the armory | 11-17 | DONE | armory2 eb93e2c, 3.1.2 (Armory Heart) |
| 82 | Chat line when you consume shared permanent upgrades (Rage/Adrenaline) | 11-17 | DONE | chatter 957b2f9 (shipped in 3.1.0) |
| 83 | Test her real army versus optimal levies | 11-17 | DONE | battery 04a06a2 (Ravager, Empress) |
| 84 | Stop autosave crash: "Stop world checks crashing the 10-minute autosave" | 11-17 | DONE | autosavefix 1641186 |
| 85 | Install her mods for compat testing (WotG, Fargo Mutant, LuiAFK) | 11-17 | DONE | modsetup d47015e |
| 86 | WotG dialogue: silence at rift cover, panic, formal, "where did the sun go" | 11-17 | DONE | wotgtalk 7b6d92c, 3.1.1 |
| 87 | Nucleosynthesis fires an arrow | 11-17 | DONE | usecycle ca20588, 3.1.0 |
| 88 | Reforge menu hides floor reforges | 11-17 | DONE | smallfix 08a755c, 3.1.1 |
| 89 | Put old cultist archers back | 11-17 | DONE | smallfix 08a755c (vanilla archers default, 3.1.1) |
| 90 | Warband text cut off | 11-17 | DONE | smallfix 58c42f1 (3.1.1) |
| 91 | Doomsday device thrown after wind-up, ricochet | 11-17 | DONE | rogue 06cdfa9, 3.1.2 |
| 92 | Optimize levy code, many levies without lag; Frame Skip in the menu | 11-17 | DONE (ongoing) | lag2-lag4 (3.1.1, 3.1.2). She still reports slow motion with Subtle. activecap is the next step |
| 93 | Single coordinating AI or split attack and move AIs | 11-17 | ANSWERED | lagsweep: shared coordinator wouldn't help (ThreatField already shared) |
| 94 | Old Duke freak-out lines; why Old Duke dies | 11-18 | DONE | wotgtalk 3.1.1; answered (ROADMAP 88-91) |
| 96 | Rogues as ambushers/skirmishers | 11-19 | DONE | rogue 06cdfa9 (3.1.2), ambush e959a2f (3.1.4) |
| 97 | Bosses fly off or leave when she loses | 11-19 | DONE | despawn aa5b236, 3.1.3 (and bosstarget refines it; see BUILDING) |
| 98 | Night Providence can't be summoned | 11-19 | ANSWERED | For the Worthy worlds only (ROADMAP 114) |
| 99 | Rift dialogue round 2 (blood moon, growth, ice, fog, morale slide, shaking text, class lines) | 11-19 | DONE | wotgtalk2 546f94d, 3.1.4 |
| 100 | Rift Blood Moon females angry only then (correction: not whole rift, not feral) | 11-19 | DONE | b2d3c0a. Build matches the correction |
| 101 | Loud panic lines; veterans "Stay calm!"; faster chatter for 60 s | 11-19 | DONE | riftpanic f6247a0, 3.1.4 |
| 102 | Summoners as tech bros (no "little ones") | 11-19 | DONE | wotgtalk2 (checked: only the Martian line uses the phrase) |
| 103 | Rift flying lines subdued; fog drift | 11-19 | DONE | wotgtalk2; f37bfa2 |
| 104 | Eclipse on demand; `/levyrift replay` | 11-19 | ANSWERED | Solar Tablet; `/levyrift replay`. Whether she was told the command: ROADMAP 146 says "tell Eve" |
| 105 | Boss HP scaling by army equipment, not count; settings entry | 11-19 | DONE | bossscale 3110a40, 3.1.4 |
| 106 | Spectral Lens 85/95/99% | 11-19 | DONE | uipolish 6043a92, 3.1.4 |
| 107 | Officer hire panel text size, clipping, AI-sounding text | 11-19 | DONE | uipolish, 3.1.4 |
| 108 | Spectator red warning | 11-19 | DONE | uipolish, 3.1.4 |
| 109 | Better reforge of duplicate weapons | 11-19 | DONE | uipolish, 3.1.4 |
| 110 | Useless economy accessories returned; hide accessory minions | 11-19 | DONE | uipolish, 3.1.4 |
| 111 | Camera fixes | 11-19 | DONE (GPU picture unverified) | uipolish, 3.1.4 |
| 112 | Nebula boosters usable by levies | 11-19 | DONE (already existed) | `SoldierNebulaBooster.cs` |
| 113 | Questions: max HP of levies; Planty Mush; who the red-haired girl is | 11-17, 11-19, 11-21 | CORRECTED 11-21 | The earlier answer (Heart of the Elements) was WRONG. It is the pet of Calamity's Daawnlight Spirit Origin worn by ranged levies; orphaned pets fly to the commander when a levy dies (bug) → builder levypets |
| 114 | Mod-author opt-out stance; HUD off; screenshot choice | 11-20 | ANSWERED | ROADMAP 176-177 (Capture key camera mode) |
| 115 | "Don't fix by removing features; preferences become settings" | 11-20 | DONE | NORTH_STAR 5ae1bce |
| 116 | "We already built the fight recorder; note it" | 11-20 | DONE | NORTH_STAR 712c7aa |
| 117 | Dupe: Magic Storage armory duplication | 11-19, 11-19 20:48, 11-20 01:26 | BUILDING | armory2fix2 (8144606, in 3.1.5 test, a stopgap block); armory2fix3 doing the real shift-click path |
| 118 | Boss targeting: beaten bosses fight on; players and levies before drones and turrets; CalClone drone damage | 11-19 | BUILDING | bosstarget 87024e2, 3.1.5 in test |
| 119 | Active army cap / reserve option, "?" help, COMMAND POINTS wording, post-fight side-menu control | 11-16 to 11-19 | BUILDING | activecap a261cb9 (unmerged, 3.1.6) |
| 120 | Levies hover, don't dodge, Mind Link: no ropes and hooks, forced flight, weapons | 11-19 | BUILDING | movement2 |
| 121 | PvP: her army versus the optimal one; big multi-level arena, 7 spawns per side, staggered, names, teleport bug, i-frames | 11-19, 11-20 | BUILDING | pvp builder (v2/v3) |
| 122 | Replay viewer: team colours, HP and damage numbers, PvP winner | 11-20 | BUILDING | viewerpvp |
| 123 | Recorded Solar Pillar fights to watch | 11-20 | BUILDING | activecap's pillar A/B recordings |
| 125 | Field Manual up to date | 11-20 | QUEUED | ROADMAP 163 |
| 127 | "Ask me before big balance changes; don't act on thinking-out-loud" | 11-19 | DONE (ROADMAP only) | ROADMAP 142 and 145; not in NORTH_STAR |

Rows 95, 124 and 126 were folded into 119 and 118 to avoid double counting. Numbering has gaps for that reason.

---

## 4. Ideas (musing, "just ideas", "I wish", "you don't have to"), classified

| Idea | Date | Class | Evidence |
|---|---|---|---|
| Drone factory (10-15 drones, switch, patrol banner) | 11-14, 11-17 | QUEUED | ROADMAP 187, 4460. She repeated it |
| Hostile drones chasing you | 11-14 | QUEUED | ROADMAP 4460 |
| Mass fishing with levies, beer station, magnets, low rods | 11-14 | QUEUED | ROADMAP 4460. Strong wording |
| Fruit juices / pitcher glass blends | 11-14 | QUEUED | ROADMAP 4460 |
| Roguelike brew system | 11-14 | SUPERSEDED | Her "Oh, we shouldn't"; ROADMAP "PARK" |
| One of each weapon per levy | 11-14 | SUPERSEDED | Her "No, I don't like that. No." |
| Levies sleep in NPC houses, barracks, wages as tax share, bunk beds | 11-14 | QUEUED | ROADMAP 4460 |
| Levy-only drinks (dash), 3-alcohol cap; vodka needs ice (she dropped it) | 11-14 | QUEUED | ROADMAP 4460 |
| Balance meter: symmetric versus varied loadouts | 11-14 | QUEUED | ROADMAP 4460 |
| Neutral bands: trade, temporary bodyguard hire, destinations and jobs, fight enemies | 11-14 | QUEUED | ROADMAP 4460 |
| Levy invasion event, remote boss summon at a position | 11-14 | QUEUED (parked) | ROADMAP 4460 |
| Buy tedious accessories (squirrel-style) | 11-14 | QUEUED | ROADMAP 4460 |
| Levies use placed rope, hold torches, bug-net critters, pet pets, sit more | 11-14 | QUEUED | ROADMAP 4460 and 148 |
| Guide to Autonomy area farming | 11-14 | QUEUED | ROADMAP 4460 |
| Predict boss dashes by simulating its AI | 11-15 | QUEUED | Proposal only (ROADMAP 345-347); no prototype dispatched |
| On-screen morale readout | 11-15 | QUEUED | ROADMAP 560 |
| Auto-rally / call-in-army with protection | 11-15, 11-17 | SUPERSEDED | 11-17 04:57 "We don't need calling in the army... We don't need a wave of protection" |
| Bosses split volleys across levies | 11-17 | QUEUED (parked) | She: "not worth making" |
| Mega gun combination weapon | 11-17 | QUEUED (parked) | ROADMAP IDEAS |
| GBU laser bomb / bunker missile / activated Doomsday Device | 11-16, 11-17 | SUPERSEDED, conflicting | 11-17 03:25 "the bomb idea was a joke", but 11-17 05:07 "I do still want the bunker missile". ROADMAP calls it a joke. Ask her |
| Balloon upgrades, Bloons TD homage | 11-17 | SUPERSEDED | 11-17 05:07 "I don't want you to actually make these yet" |
| Remote Armory item / cross-world armory | 11-19 | QUEUED (too complex) | ROADMAP 134 |
| Shady Salesman gift thrown at levies | 11-19 | QUEUED | ROADMAP 141 |
| Levy-specific weapons submod | 11-19 | QUEUED | ROADMAP 149 |
| Secret seeds / Get Fixed Boy content | 11-19 | QUEUED | ROADMAP 149 |
| Capture the flag, PvP kits | 11-20 | QUEUED | ROADMAP 163 area; "don't build without her go" |
| Zombie-defense / boss-wave custom map | 11-20 | QUEUED | Same |
| Automated levies-versus-boss video pipeline | 11-20 | ANSWERED | My take given (ROADMAP 161-162); not built |
| Nameplates and HP bars on all mobs; click a heavy projectile; damage-coloured projectile glow | 11-20 | QUEUED | ROADMAP 169; "don't build without confirming" |
| Militia outposts fight independently | 11-17 | DONE | Outposts exist and event recordings capture them (recorder2) |
| A game in the replay viewer's retro art style | 11-17 | QUEUED (parked) | ROADMAP 66 area |
| Discord for weapon/mod fix requests; public build without dev tools | 11-19 | QUEUED | ROADMAP 149 |
| Fargo Souls compat | 11-17 | QUEUED (later) | ROADMAP 190 |
| Levy progression assistance in the progression menu | 11-19 | QUEUED | ROADMAP 149 |
| Shaking text for "..." lines ("if it's possible") | 11-19 | DONE | wotgtalk2, 3.1.4 |
| Mod of Redemption "blind" compatibility | 11-15 | DONE | See item 41 |

---

## 5. Counts

Section 3 (asks table, 124 rows): DONE 106 (incl. 'DONE (ongoing)', 'DONE (ROADMAP only)', 'already existed'), BUILDING 7, SUPERSEDED 4, ANSWERED 7, QUEUED 1 (Field Manual).
Section 2 adds 27 QUEUED items (some are the same idea as section 4) and Section 1 adds 5 MISSING (M1-M5), plus the M6 question list (4) and 4 rules that live only in ROADMAP.
Section 4 (ideas, 36 rows): QUEUED 27, SUPERSEDED 5, ANSWERED 1, DONE 3, MISSING 0, BUILDING 0.

Notes on uncertainty:
- M1-M3 are strong, because each shows in only one queue line or none. M4 is moderately strong. M5 is weak.
- "DONE" rests on ROADMAP claims plus spot checks in code or git for the correction-sensitive ones (Clothier decay, female Blood Moon, side slots, WoF). I did not re-verify every merge.
- Several DONE items have caveats she may notice: LuiAFK "open shop" was never reproduced, the radio had no range limit in code, and Dreadmine lag was explained rather than reduced.
