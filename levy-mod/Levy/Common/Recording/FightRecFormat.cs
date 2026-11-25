using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Fight recording file format (JSON lines, one object per line, key "k" says what it is):
	///   hdr   header: version, mods, game mode, boss, start tick, hz, schemas of the arrays below
	///   arena coarse tile snapshot: origin tile (x0,y0), size, rows of run-length pairs [code,run,...] (0 air, 1 solid, 2 platform, 3 liquid)
	///   f     one sample (every 2 ticks): t tick, p player array, b boss parts, j hostile projectiles, l levies
	///   e     one event: t tick, e kind (jump xjump fly dash hook use hit acc buffs boss), plus fields.
	///         Format 2 adds: lhit (levy id, dmg, src body|minion|proj|other, by boss|minion|world, npc type, proj type, life after, x, y, kill), ldeath (id, x, y, npc, by),
	///         tgt (boss target changed: kind 0 none / 1 player / 2 levy, levy id, boss slot). Format 2 also adds B[14] tkind, B[15] tlevy, L[10] lifeMax, hdr.meta, end.target.
	///         Format 3 adds the hitbox width and height in pixels (the game's own NPC / Projectile width and height, centred on x,y): B[16],B[17], J[6],J[7], L[11],L[12].
	///         Format 4 adds: several arena records (the first is the 200x120 start area; later ones are non-overlapping strips written when the commander, a boss part or a levy leaves the captured area, plus the Static Camera view when one stands near the fight;
	///         together they tile the covered terrain), a cam record (id, name, x,y view centre tile, x0,y0,w,h view rectangle in tiles, zoom), and boss outcome events:
	///         bdeath (the boss died) / bdespawn (it left alive): tick t, npc, name, id, x,y last position px, life, max, final (1 = the fight's end), kh (1 = the game's death path ran for the boss or its realLife pool, i.e. loot was dropped; 0 = it was only removed; absent in files made before 2026-11-20), and the last hit ht (tick), hd (damage), hs (item|proj|other), hy (item / projectile type), hn (name). end gains outcome (killed|despawn|none) and arenaChunks.
	///         Format 5 adds the frame key a (hostile adds and minions that are not boss parts: creepers, servants, worms, bees, slimes; schema SchemaA) and the event lbuf (id, b = [[buffType,timeLeft],..]: a levy's buffs, written when they change).
	///         Format 6 adds gear baselines, written at the start and again only when the gear changes (checked once a second):
	///         gear (the commander): arm = [[slot,type,prefix],..] over Player.armor (0-2 armor, 3-9 accessories, 10-19 vanity), inv = [[slot,type,prefix,stack],..] over the hotbar (0-9) and ammo slots (54-57); air slots are left out.
	///         lgear (one levy): id, g = [[slot,type,prefix,stack],..] with slot 0 weapon, 1 sidearm, 2 head, 3 body, 4 legs, 5-10 accessories, 11 ammo pouch, 12 hook, 13 wings, 14 boots (the two mobility slots; a recording from before them has neither and its wings and boots sit in 5-10).
	///         names gains prefixes (id to name). Item names are in names.items.
	///         Format 7 adds the frame key s: the hitbox of EVERY other hostile NPC anywhere within 600 tiles of the commander (off-screen ones too) that b and a do not carry (the segments of secondary worms, damage-0 adds), SchemaS, integer pixels.
	///         A row is full (8 numbers) the first time an NPC slot is seen or when its type, size, realLife or flags change, and just [id,x,y] after that. Hostile projectile hitboxes (j: w,h) were already there since format 3.
	///         Format 8 is the automatic recorder (file extension .levyrec = this JSON-lines text, gzip-compressed, sync-flushed every 2 s so a cut file still reads up to its last flush; .jsonl stays plain text for harness recordings). Adds:
	///         hdr: auto (0 hand or harness, 1 automatic boss fights, 2 automatic all fights), gzip, cmd (the commander at the start: life, mana, defense, armor / accessories / hotbar / ammo item ids), army (n, avgLife, classes = levies by damage class
	///         of the main weapon, grades, weapons = [[itemType,count],..]); meta gains source, trigger (boss|army), setting, label.
	///         events: dmg once a second (d = [[owner cmd|levy|drone|other, class, src item|proj, type, onBoss 1|0, damage, hits],..]: damage landed on bosses and their parts, and on adds, by who and damage class), cdeath (the commander fell: x, y, n),
	///         prof (the profiler was on: /levyprof, the Toggle Levy profiler key, or automatically while an automatic recording ran, see FightRecConfig.ProfileRecordedFights; fps, ups, upd = ms per update, npc, proj, draw, ai, gear = ms, top = the biggest levy buckets, ms summed over the window, win = the window in real seconds).
	///         end gains result (won|lost|fled|ended|interrupted: the word in the file name), durationSec, bossName, bosses, cmdDeaths, armyAlive, armyTotal, dmgBoss, dmgAll, profiled, dropped, capped, writeError.
	///         Outcome and duration are only known at the end, so they live in the end record (and in the file name), not the header.
	///         Format 9 separates the boss rule's spite from real targeting, names fights with several main bosses, and gives officers their own ids:
	///         tkind 3 = spite (B[14], A[8], the tgt event's kind): the boss has no real target on a levy (the targeting lock SoldierTarget and an NPC target of 300 or more both say kind 2), but BossRule.SpiteTargetOf names a levy that is too close or does a lot of damage, and its
	///         shots and dashes are redirected at that levy (tlevy = that levy). Kind 2 keeps meaning real targeting. Files of format 8 and older never carry kind 3: their kind 2 covers all three causes. end.target gains spite (samples of the primary boss with kind 3; levy no longer counts them).
	///         hdr.boss, end.bossName and the file name carry every main boss of the fight: "The Twins" for Retinazer and Spazmatism, otherwise the distinct boss names joined ("A and B", "A, B and C"); a fight with one main boss is named for it as before.
	///         Levy ids: every officer used to share the id -1 (the synthetic record's); an officer is now -100000 - its NPC slot, so each is its own row in l and in the events. Militia keep their own negative record ids (-1, -2, ... counted down per world session). An officer with an enemy in reach and a weapon up shows state Engage (L[7]); his avatar's own movement states never include Engage, so older files show officers as Follow / Search and Destroy / Idle all the time.
	///         Format 10 adds what the post-fight report needs about the commander's own damage (every field appended):
	///         dmg rows grow from 7 to 10 numbers, [owner, class, src, type, onBoss, damage, hits, srcItem, ss, crits]: srcItem = the item that fired the projectile (the item of the EntitySource_ItemUse the projectile was born from, or of its parent
	///         projectile; for a melee hit the item itself; 0 = not known, the reader then falls back to the held item); ss = Calamity's stealth flag on the projectile (0 no, 1 stealth strike, 2 a projectile spawned by a stealth strike; always 0 without
	///         Calamity); crits = how many of the hits were critical. The same item, projectile type and stealth state share a row; rows of 7 numbers are older files (stealth and crit unknown).
	///         stl: once a second, the commander's Calamity stealth bar, only while it exists (s = percent full 0 to 100, m = the bar's size times 100). bph: the primary boss's life crossed 75, 50, 25 or 10 percent (pct, npc, name, id, army = levies tracked
	///         near the commander at that moment, hp = the commander's life). cdeath gains the death's own source when the game named one: kn (enemy type, -1 none), kp (projectile type, -1 none), ko (other source index, -1 none), kc (a custom death text), kd (damage).
	///         Format 11 adds the commander's own projectiles and fixes how a fight ends. Every field is appended.
	///         Frame key c: the commander's OWN projectiles within 400 tiles (type, x, y, vx, vy, w, h: SchemaC; at most 300 a sample, names in names.projectiles). They are his weapons, minions and sentries: owned by his player slot, friendly or a minion / sentry, not a hook,
	///         not a damage-less pet or light. The army's shots are left out although they carry the same owner slot: a projectile whose SoldierProjectileTag has a soldier index, a commander credit (officer and summoned-unit bolts), the drone flag or a source NPC, or that
	///         MarkSystem.IsArmyProjectile names. Hostile projectiles stay in j. A file of format 10 or older has no c key (the viewer shows no commander shots for it).
	///         KILL RULE: bdeath only when the death hook ran (kh 1) or the boss's life was 0; a boss that goes inactive any other way, at any life, is bdespawn (Calamity's death animation sits at life 1 until the hook runs, and Calamity removes a boss when no player is alive). Older files with no kh are re-read by FightAnalysis.RederiveKill / tools/recview/recrules.py: a bdeath above 1 life and above the last hit's damage within 60 s after a cdeath is a boss that left.
	///         RESULT RULE (file name, end.result, the report, the viewer): won = the last boss was killed. A boss that LEAVES (bdespawn) is lost when the commander's fall is why: she was dead at that moment, or her latest cdeath is no more than 60 s (FightRecorder.LostWindowTicks)
	///         before the boss's end tick. A boss with no living player to target despawns, so her death reads as the boss leaving; the 60 s covers a boss still on its way out after she respawned (Death / Master respawn timers run past 15 s). It is fled when she was alive and well
	///         and not recently fallen: dawn, distance, a timer. What the army does is not part of the rule: levies targeted or not, dead or alive, only the commander counts. With no boss end at all (the 20 minute limit, the recorder stopped) the result is lost when she fell and a boss was
	///         seen, else ended; interrupted = the world closed. Older files are re-read with the same rule by tools/recview/recrules.py (needs the cdeath events and, for "dead at that moment", the p flag 8192 of the frames).
	///         EVENT NPCs ARE NOT BOSSES for the recorder (FightRecorder.EventNpcTypes): the four Celestial Pillars, the Martian Saucer with its turrets, cannons and core, the Flying Dutchman and its cannons, and the Pumpkin Moon / Frost Moon wave bosses (Mourning Wood, Pumpking,
	///         Everscream, Ice Queen, Santa-NK1). They never start a boss recording, keep one running, merge into one or add to its name, so the Cultist's win ends when the Cultist falls and the pillars it calls are not part of it. Their hitboxes still appear (key s, and a for those that
	///         touch-damage). No automatic recording of an event on its own: "Record fights automatically: all fights" still records the skirmish around the commander, under the label of the event or "Fight", as it always did; "boss fights" does not record pillars.
	///         Older files stay readable: every new field is appended or optional.
	///         Format 12 adds EVENT RECORDINGS (EventRecorder) and wider coverage of boss recordings. Every field is appended or optional; a reader of format 11 still reads a format 12 fight recording.
	///         hdr.kind: "fight" (the boss / skirmish recorder) or "event". hdr.imp names the importance levels "0 normal,1 miniboss,2 boss".
	///         nt line (any recording): {"k":"nt","type","name","imp"} the first time an enemy type is seen (a fight recording: in a or s; an event: any enemy). A later nt line for the same type wins (the event fallback below can raise a type to miniboss).
	///         Fight recordings: l carries EVERY levy wherever it stands (it used to stop at 120 tiles from the commander); a levy outside the captured terrain gets 50 x 40 tile cells around it (60 tiles each side, 40 above and below), arena records with far = 1
	///         that a viewer keeps out of its first fit. Officers, militia and drones as before. TERRAIN WHEREVER THE FIGHT GOES: the commander and every main boss get 50 x 40 tile cells around them (100 tiles each side, 60 above and below)
	///         whenever they stand outside the captured area, so a fight that travels (1400 tiles to a boss) never plays in a black void; these arena records carry moved = 1. At most 4 cells a sample.
	///         EVENT RECORDING (hdr.kind "event", file event_ID_YYYY-MM-DD_HH-MM_result.levyrec, auto 3): the whole world, not the commander's surroundings. 5 Hz (EventRecorder.SampleEvery 12). Header: event (id), eventName, events (ids running at the start),
	///         roster {type: {name, imp}} (the event's own minibosses and bosses), wave (NPC.waveNumber at the start), worldW / worldH (tiles), army, the schemas. meta.trigger = "event".
	///           ids: goblinarmy, frostlegion, pirates, martianmadness (Main.invasionType with invasionSize > 0), pumpkinmoon, frostmoon, solareclipse, bloodmoon (Main flags), oldonesarmy (DD2Event.Ongoing),
	///           pillars (NPC.LunarApocalypseIsUp or any Celestial Pillar standing), acidrain (Calamity's AcidRainEvent.AcidRainEventIsOngoing, by reflection; never without Calamity).
	///           frame f: t, p (the commander, SchemaP with only x,y,vx,vy,dir,flags 1/1024/4096/8192,life,mana,held,mount filled), l (every levy and militia body), a (every hostile NPC that is not a worm body segment, any distance; SchemaA),
	///           s (worm body segments, SchemaS short / full rows as in format 7), j (hostile projectiles within about 100 tiles of a levy or the commander, at most 300). Positions are whole pixels.
	///           terrain: arena records of 50 x 40 tile cells, captured once each, around every levy, the commander, every militia outpost and every enemy, wherever they move during the recording (100 tiles each side, 50 above and below), at most 6 cells a tick.
	///           events: evstart / evend (another event begins or one ends inside the recording; evend carries its result), wave (n, ev: the moons and the Old One's Army), prog every 10 s (left, of: an invasion's size, Acid Rain's points),
	///           kc every 10 s (n kills so far, mini miniboss kills, army = bodies standing), ekill (a miniboss or boss died: npc, name, id, imp, x, y, max), outposts (o = [[barracksId,x,y,mobilized,militia,town],..], at the start and when they change),
	///           lhit (id, dmg, src body|proj|other, by boss|miniboss|enemy|world, npc, proj, life, max, x, y, kill), ldeath, lgear, cdeath, dmg once a second (rows as format 10; field 4 is the target's importance 0/1/2, field 8 always 0).
	///           end: kind event, reason, result, durationSec, event (the main event), eventName (= bossName), eventsSeen, results {id: result}, waveMax, kills, minibossKills, bossKills, levyHits, levyDeaths, cmdDeaths, armyPeak, armyAlive, armyTotal,
	///           arenaChunks, dmgAll, cpu {meanUs per tick while recording, maxMs worst tick, sampleUs per sample, hist = ticks by cost with the bucket edges histUs}, dropped, capped, writeError.
	///         EVENT START: the "Record events" setting is on (default) and an event runs; checked every 30 ticks, so turning the setting on during an event starts one. A recording that stopped because the army was gone or a cap was reached
	///           does not start again for that event until it has ended. One recording at a time: an event that starts while one runs is part of it (evstart). The MAIN event is the first in this order that the recording saw:
	///           pillars, martianmadness, pirates, goblinarmy, frostlegion, oldonesarmy, pumpkinmoon, frostmoon, acidrain, solareclipse, bloodmoon. It names the file and decides the result.
	///         EVENT END: every event it saw is over ("event over"); the army is gone: no levy or militia body anywhere for 90 s while an event runs (EventRecorder.ArmyGoneTicks; a regular respawns after 60 s, so nobody came back; a recording that never had a levy, and the
	///           Old One's Army while its sentry mode stands the regulars down on purpose, never count as gone); the 60 minute cap
	///           (EventRecorder.MaxTicks: about 36 MB of gzip at 60 levies, 100 enemies and 100 hostile projectiles all moving, measured by the world self-check); the size cap of the setting; the setting switched off; the world closing.
	///         EVENT RESULT RULE (EventRecorder.DecideRecording / EventResult, tools/recview/recrules.py event_result): lost = the army was gone. interrupted = the world closed. survived = the 60 minute cap or the file size cap stopped it.
	///           ended = the setting went off, a write failed. Otherwise the main event's own result when it ended: an invasion is won when its size ran out (the game clears it at 0), ended when something else removed it; the Pumpkin Moon and
	///           Frost Moon are won when wave 15 (the last) was reached before dawn, survived when dawn came first; the Old One's Army is won (DD2Event.WonThisRun), lost (LostThisRun: the crystal fell) or ended; the Celestial Pillars are won
	///           (all four down); Acid Rain is won when the last points seen were at most 10 % of the start (and at most 2), survived when Calamity ended it for no kills or no rain; the Blood Moon and the Solar Eclipse are survived.
	///         IMPORTANCE (imp): 2 boss = NPC.boss, counted as a boss, a fight boss, the Celestial Pillars, Betsy; 1 miniboss = the hand list (EventRecorder.MinibossTypes: Goblin Summoner, Flying Dutchman and cannons, Martian Saucer and parts,
	///           Mourning Wood, Pumpking and its blade, Everscream, Santa-NK1, Ice Queen, Mothron, Dark Mage T1/T3, Ogre T2/T3, Dreadnautilus; with Calamity Cragmaw Mire, Nuclear Terror, Mauler) or, in an event recording, the FALLBACK:
	///           once 3 ordinary types have been seen, a type whose largest life is at least 6 times their median and at least 1000; 0 normal otherwise. A boss that appears during an event still gets its own fight recording.
	///         Format 13 makes fight recordings compact and splits big ones. Readers of format 12 read format 13 once they decode the rows (FightRecFormat.RowDecoder; Python tools/recview/reccodec.py; the viewers' own copy).
	///         hdr.codec = 1: the frame keys b, a, l, j and c are ROW CODED (RowCodec). Every number is an integer in its field's unit: whole pixels, points and ids, velocities (and b's ai0..ai3) in tenths.
	///           An entry of a row list is K (the entity whose key is K, every field as predicted), [K, mask, d, ...] (the fields whose bit i is set in mask are prediction + d, in field order; the rest as predicted)
	///           or [[full row]] (a new entity, or one that was not in the sample before: the row in natural units, as in format 12). The key is b / a field 1 (the NPC slot), l field 0 (the levy id), j field 8 and c field 7 (the projectile slot, new).
	///           Prediction: x and y carry on by the last step, or by the velocity (pixels moved = (v before + v now + 5) div 10, rounded down, in tenths), whichever was closer on the sample before; a fresh entity starts with the velocity. Every other field stays as it was.
	///           An entity is predicted only from the sample right before it: one missing from a sample is forgotten and comes back as a full row. A hdr line forgets everything. p and s are written as before.
	///         Positions are whole pixels in every list (format 12 kept a tenth). SchemaB gains realLife (field 18: the slot that owns the NPC's life, -1 none), SchemaJ and SchemaC gain slot (the projectile's slot, the row key).
	///         The hit event gains taken: the life the commander actually lost to that hit (dmg stays the game's damage number; a hit that a dodge, a heal or immunity undid takes 0).
	///         The tgt event and the target counts follow the main pool (the fight boss with the largest life pool), not the first boss seen; Draedon's own NPC never counts against his mechs. A boss whose target slot is the empty slot 255 reads the target its AI last chose (NPC.targetRect).
	///         bph.army counts the levies within ArenaRangeTiles of the commander or a fight boss.
	///         PART FILES: a recording whose compressed part passes FightRecorder.PartBytes (9 MB) goes on in NAME_part2.levyrec, NAME_part3.levyrec ...; each part starts with the header plus "part": n, and its rows start full, so it reads on its own.
	///           The names and end records are in the last part. FightRecReader.Read, make_site.py and the viewer join the parts back into one fight. The size cap of the setting counts every part.
	///         Format 14 adds the levies' minions to the recording (every field appended or optional; readers of format 13 skip the new key and the new field):
	///         frame key m: the LEVIES' own minions and sentries (type, x, y, vx, vy, w, h, slot, owner, kind: SchemaM; at most 100 a sample, row coded like c with the slot as the key). owner = the levy's id (the l row's id), kind 1 minion, 2 sentry, 4 a shot. The commander's own minions were
	///         already in c; c rows gain the same kind as field 8. A levy's friendly shots go into m only in the summon A/B harness (FightRecorder.SummonTrace, up to 300 rows), so the viewer's "summon trace" can draw "its shots" for both sides. Files of format 13 and older have no m
	///         and no kind (the viewer draws their c rows as plain shots).
	///         Format 15 adds what the viewer needs to click a projectile and to draw lines (every field appended or optional; readers of format 14 skip the new key and fields):
	///         j, c and m rows gain penetrate (the hits it has left, -1 endless) and timeLeft (ticks before it expires) as the last two fields; c and m rows also gain damage (c field 9, m field 10). j already carried damage.
	///         Frame key z (SchemaZ, plain rows, at most RecLines.MaxRows a sample): the line or link a hostile projectile IS, which its box cannot say: deathrays and primitive laserbeams (Calamity, Wrath of the Gods, vanilla pattern), Nameless Deity's screen slices and light slashes,
	///         the Avatar's blood torrent, arm strikes and thorns, and the linked electric field between Ares's tesla orbs (kind 3). See RecLines.
	///         p flag 16384: the Avatar of Emptiness's dimension is up (AvatarFight.Void; no tile holds anything up). Event bstate (s, npc): the main boss's CurrentState changed (a ModNPC with a public CurrentState property: Nameless Deity, the Avatar).
	///         Names: an NPC whose display name is random glitch text (Nameless Deity) is recorded by its class name instead (FightRecorder.NpcName).
	///   names type id to name tables (items, npcs, projectiles, buffs, soldiers), written at the end
	///   end   stop reason and counts
	/// All positions are world pixels (centers). Plain data; nothing here touches the game, so it can self-check at boot.
	/// </summary>
	internal sealed class RecFrame
	{
		public int T;
		/// <summary>[x,y,vx,vy,dir,flags,life,mana,immune,wingTime,rocketTime,heldType,mount,hookX,hookY,dashDelay]</summary>
		public double[] P;
		/// <summary>[type,whoAmI,x,y,vx,vy,life,lifeMax,ai0,ai1,ai2,ai3,target,isPart]</summary>
		public List<double[]> B = new();
		/// <summary>[type,x,y,vx,vy,damage]</summary>
		public List<double[]> J = new();
		/// <summary>[recordId,x,y,vx,vy,flags,life,stateIndex,heldType,wingTime]</summary>
		public List<double[]> L = new();
		/// <summary>[type,whoAmI,x,y,vx,vy,life,lifeMax,tkind,tlevy,w,h]</summary>
		public List<double[]> A = new();
		/// <summary>Format 7 hitboxes of other hostile NPCs, integer pixels: full [type,id,x,y,w,h,realLife,flags] when the NPC is new or its shape changed, else the short [id,x,y].</summary>
		public List<double[]> S = new();
		/// <summary>Format 11: the commander's own projectiles [type,x,y,vx,vy,w,h] (format 13: + slot).</summary>
		public List<double[]> C = new();
		/// <summary>Format 14: the levies' minions and sentries (and, in a summon trace, their friendly shots) [type,x,y,vx,vy,w,h,slot,owner (the levy's id),kind]; kind 1 minion, 2 sentry, 4 shot.</summary>
		public List<double[]> M = new();
		/// <summary>Format 15: lines and links of hostile projectiles [x0,y0,x1,y1,width,kind,type,slot] (see <see cref="RecLines"/>).</summary>
		public List<double[]> Z = new();
	}

	/// <summary>Format 13 part files: NAME.levyrec, NAME_part2.levyrec, NAME_part3.levyrec ... Plain file names, no game types (the command-line report tool uses them too).</summary>
	internal static class RecParts
	{
		private static readonly System.Text.RegularExpressions.Regex PartRe = new(@"^(.*)_part(\d+)(\.levyrec|\.jsonl)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		/// <summary>NAME.levyrec -> NAME_partN.levyrec (N >= 2; N 1 is the name itself).</summary>
		public static string PartName(string path, int n)
		{
			if (n <= 1) return path;
			string ext = System.IO.Path.GetExtension(path); string stem = path.Substring(0, path.Length - ext.Length);
			return stem + "_part" + n + ext;
		}

		/// <summary>The first part's path and N for NAME_partN.levyrec (1 for the first part or a file with no parts).</summary>
		public static string PartBase(string path, out int part)
		{
			var m = PartRe.Match(path ?? ""); part = 1;
			if (!m.Success) return path;
			part = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
			return m.Groups[1].Value + m.Groups[3].Value;
		}

		/// <summary>True for NAME_part2.levyrec and later parts (the first part carries the recording's own name).</summary>
		public static bool IsLaterPart(string path) { PartBase(path, out int n); return n >= 2; }

		/// <summary>Every part of the recording <paramref name="path"/> belongs to, in order: the first part, then NAME_part2 ... while they exist.</summary>
		public static List<string> Parts(string path)
		{
			string b = PartBase(path, out _); var l = new List<string>();
			if (System.IO.File.Exists(b)) l.Add(b); else return new List<string> { path };
			for (int n = 2; ; n++) { string p = PartName(b, n); if (!System.IO.File.Exists(p)) break; l.Add(p); }
			return l;
		}
	}

	internal static class FightRecFormat
	{
		public const int Version = 15;
		/// <summary>Levies this close (tiles) to the commander or a fight boss are "in the arena": bph.army, and the report's army counts read from the frames.</summary>
		public const int ArenaRangeTiles = 120;
		/// <summary>FightRecorder.LostWindowTicks: a boss that leaves this soon after the commander's fall left because of it.</summary>
		public const int LostWindowTicks = 60 * 60;

		/// <summary>The result rule (FightRecorder.DecideResult forwards here, so the command-line report tool shares it).</summary>
		internal static string DecideResult(string outcome, int cmdDeaths, bool cmdDeadAtEnd, int endTick, int lastDeathTick, int bossesSeen)
		{
			if (outcome == "killed") return "won";
			if (outcome == "despawn")
			{
				bool fell = cmdDeaths > 0 && lastDeathTick >= 0 && endTick - lastDeathTick >= -10 && endTick - lastDeathTick <= LostWindowTicks;
				return cmdDeadAtEnd || fell ? "lost" : "fled";
			}
			return cmdDeaths > 0 && bossesSeen > 0 ? "lost" : "ended";
		}

		public const string SchemaP = "x,y,vx,vy,dir,flags,life,mana,immune,wingTime,rocketTime,held,mount,hookX,hookY,dashDelay";
		public const string SchemaPFlags = "1 grounded,2 flying,4 hookOut,8 hookAttached,16 left,32 right,64 jump,128 down,256 up,512 grapple,1024 useItem,2048 dashing,4096 mounted,8192 dead,16384 in the Avatar's dimension";
		public const string SchemaB = "type,id,x,y,vx,vy,life,lifeMax,ai0,ai1,ai2,ai3,target,isPart,tkind,tlevy,w,h,realLife";
		public const string TKinds = "0 none,1 player,2 levy (the boss targets a levy: tlevy = its id),3 spite (redirected shots: the boss rule's grudge sends its projectiles and dashes at a levy that is too close or does a lot of damage; tlevy = that levy)";
		public const string SchemaA = "type,id,x,y,vx,vy,life,lifeMax,tkind,tlevy,w,h";
		public const string SchemaS = "type,id,x,y,w,h,realLife,flags  (a row of 3 = id,x,y: same type, size, realLife and flags as that id's last full row)";
		public const string SFlags = "1 deals contact damage,2 does not take damage,4 body segment (realLife = the head that owns its life)";
		public const string SchemaJ = "type,x,y,vx,vy,damage,w,h,slot,penetrate,timeLeft  (penetrate: hits left, -1 endless; timeLeft: ticks before it expires; both absent before format 15)";
		public const string SchemaC = "type,x,y,vx,vy,w,h,slot,kind,damage,penetrate,timeLeft  (kind: 1 minion, 2 sentry, 4 shot; absent before format 14; damage, penetrate and timeLeft absent before format 15)";
		public const string SchemaM = "type,x,y,vx,vy,w,h,slot,owner,kind,damage,penetrate,timeLeft  (the levies' minions and sentries: owner = the levy's id, kind 1 minion, 2 sentry, 4 shot; format 14; damage, penetrate and timeLeft from format 15)";
		public const string SchemaZ = "x0,y0,x1,y1,width,kind,type,slot  (format 15, whole pixels: the line or link a hostile projectile is; kind 1 beam, 2 warning line, 3 link between two projectiles, 4 strike segment; slot = the projectile's row in j)";
		public const string SchemaL = "id,x,y,vx,vy,flags,life,state,held,wingTime,lifeMax,w,h";
		public const string ImpNames = "0 normal,1 miniboss,2 boss";
		public const string SchemaLFlags = "1 grounded,2 flying,4 aerial,8 hooking,16 blocked";

		public static string N(double v)
		{
			if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
			double r = Math.Round(v, 1);
			if (r == Math.Floor(r) && Math.Abs(r) < 1e9) return ((long)r).ToString(CultureInfo.InvariantCulture);
			return r.ToString("0.#", CultureInfo.InvariantCulture);
		}

		public static string MetaJson(IDictionary<string, string> meta)
		{
			if (meta == null) return "{}";
			var sb = new StringBuilder("{"); bool first = true;
			foreach (var kv in meta) { if (!first) sb.Append(','); first = false; sb.Append(Esc(kv.Key)).Append(':').Append(Esc(kv.Value)); }
			return sb.Append('}').ToString();
		}

		public static string Esc(string s)
		{
			if (s == null) return "null";
			var sb = new StringBuilder(s.Length + 2); sb.Append('"');
			foreach (char c in s)
			{
				if (c == '"' || c == '\\') sb.Append('\\').Append(c);
				else if (c < 32) sb.Append(' ');
				else sb.Append(c);
			}
			return sb.Append('"').ToString();
		}

		private static void Arr(StringBuilder sb, double[] a)
		{
			sb.Append('[');
			for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(N(a[i])); }
			sb.Append(']');
		}

		private static void Rows(StringBuilder sb, string key, List<double[]> rows)
		{
			if (rows.Count == 0) return;
			sb.Append(",\"").Append(key).Append("\":[");
			for (int i = 0; i < rows.Count; i++) { if (i > 0) sb.Append(','); Arr(sb, rows[i]); }
			sb.Append(']');
		}

		public static string Frame(RecFrame f, RowCodec codec = null)
		{
			var sb = new StringBuilder(256);
			sb.Append("{\"k\":\"f\",\"t\":").Append(f.T).Append(",\"p\":"); Arr(sb, f.P);
			if (codec == null) { Rows(sb, "b", f.B); Rows(sb, "j", f.J); Rows(sb, "l", f.L); Rows(sb, "a", f.A); Rows(sb, "s", f.S); Rows(sb, "c", f.C); Rows(sb, "m", f.M); Rows(sb, "z", f.Z); }
			else { codec.Rows(sb, 0, f.B); codec.Rows(sb, 3, f.J); codec.Rows(sb, 2, f.L); codec.Rows(sb, 1, f.A); Rows(sb, "s", f.S); codec.Rows(sb, 4, f.C); codec.Rows(sb, 5, f.M); Rows(sb, "z", f.Z); }
			return sb.Append('}').ToString();
		}

		/// <summary>One event line. extra is "key:rawjson" pairs, e.g. ("item","123"), ("name",Esc("Sword")).</summary>
		public static string Event(int tick, string kind, params (string key, string rawJson)[] extra)
		{
			var sb = new StringBuilder(96);
			sb.Append("{\"k\":\"e\",\"t\":").Append(tick).Append(",\"e\":").Append(Esc(kind));
			foreach (var (key, raw) in extra) sb.Append(",\"").Append(key).Append("\":").Append(raw);
			return sb.Append('}').ToString();
		}

		/// <summary>Arena rows: each row is the run-length pairs [code,run,code,run,...].</summary>
		public static string Arena(int x0, int y0, int w, int h, List<int[]> rows)
		{
			var sb = new StringBuilder(8192);
			sb.Append("{\"k\":\"arena\",\"x0\":").Append(x0).Append(",\"y0\":").Append(y0).Append(",\"w\":").Append(w).Append(",\"h\":").Append(h).Append(",\"rows\":[");
			for (int r = 0; r < rows.Count; r++)
			{
				if (r > 0) sb.Append(',');
				sb.Append('[').Append(string.Join(",", rows[r])).Append(']');
			}
			return sb.Append("]}").ToString();
		}

		public static List<int> RleEncode(IList<int> codes)
		{
			var o = new List<int>();
			int i = 0;
			while (i < codes.Count) { int c = codes[i], j = i; while (j < codes.Count && codes[j] == c) j++; o.Add(c); o.Add(j - i); i = j; }
			return o;
		}

		public static List<int> RleDecode(JsonElement pairs)
		{
			var o = new List<int>(); int n = pairs.GetArrayLength();
			for (int i = 0; i + 1 < n; i += 2) { int c = pairs[i].GetInt32(), run = pairs[i + 1].GetInt32(); for (int k = 0; k < run; k++) o.Add(c); }
			return o;
		}

		/// <summary>The lines the Python codec (tools/recview/reccodec.py) writes for <see cref="GoldenFrames"/>: the C# writer must produce exactly these, so the game, the site builder and the viewer agree.</summary>
		internal static readonly string[] Golden =
		{
			"{\"k\":\"f\",\"t\":0,\"p\":[0],\"j\":[[[5,100,200,3.5,-1,20,8,8,7]],[[5,10,10,0,0,20,8,8,9]]],\"l\":[[[-100003,50,60,1.2,0,1,100,2,757,0,100,18,40]]]}",
			"{\"k\":\"f\",\"t\":2,\"p\":[0],\"j\":[7,[9,4,-1]],\"l\":[[-100003,10,-2,-2]]}",
			"{\"k\":\"f\",\"t\":4,\"p\":[0],\"j\":[[7,30,-1,1,-3,-4]],\"l\":[[-100003,74,2,-2,-10],[[4,0,0,0,0,0,1,0,0,0,1,1,1]]]}",
			"{\"k\":\"f\",\"t\":6,\"p\":[0],\"j\":[[7,30,-12,5,-2,-1],[[5,10,9,0,0,20,8,8,9]]]}",
		};

		/// <summary>The input of <see cref="Golden"/>: a projectile that flies on (slot 7), one that stops, misses a sample and comes back (slot 9), a levy (an officer id) that slows, and one that appears.</summary>
		internal static List<RecFrame> GoldenFrames()
		{
			var o = new List<RecFrame>();
			RecFrame F(int t) { var f = new RecFrame { T = t, P = new double[] { 0 } }; o.Add(f); return f; }
			var a = F(0); a.J.Add(new double[] { 5, 100, 200, 3.5, -1, 20, 8, 8, 7 }); a.J.Add(new double[] { 5, 10, 10, 0, 0, 20, 8, 8, 9 }); a.L.Add(new double[] { -100003, 50, 60, 1.2, 0, 1, 100, 2, 757, 0, 100, 18, 40 });
			var b = F(2); b.J.Add(new double[] { 5, 107, 198, 3.5, -1, 20, 8, 8, 7 }); b.J.Add(new double[] { 5, 10, 9, 0, 0, 20, 8, 8, 9 }); b.L.Add(new double[] { -100003, 50.4, 60, 1.0, 0, 1, 100, 2, 757, 0, 100, 18, 40 });
			var c = F(4); c.J.Add(new double[] { 5, 112.6, 197.2, 3.2, -1.4, 20, 8, 8, 7 }); c.L.Add(new double[] { -100003, 52, 60, 0.8, 0, 1, 90, 2, 757, 0, 100, 18, 40 }); c.L.Add(new double[] { 4, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1 });
			var d = F(6); d.J.Add(new double[] { 5, 107, 199, 3, -1.5, 20, 8, 8, 7 }); d.J.Add(new double[] { 5, 10, 9, 0, 0, 20, 8, 8, 9 });
			return o;
		}

		/// <summary>Row codec: the Python golden lines, then a random round trip (entities that come, go, repeat a key, carry NaN or huge numbers; position steps and velocities of every size) decodes back to the stored units.</summary>
		internal static string CodecSelfCheck()
		{
			var gc = new RowCodec(); var gf = GoldenFrames();
			for (int i = 0; i < gf.Count; i++) { string line = Frame(gf[i], gc); if (line != Golden[i]) return "golden line " + i + " is " + line; }
			var rnd = new Random(1234); var codec = new RowCodec(); var dec = new RowDecoder();
			using (var hd = JsonDocument.Parse("{\"k\":\"hdr\",\"codec\":1}")) dec.Header(hd.RootElement);
			var live = new Dictionary<int, double[]>();
			for (int t = 0; t < 400; t += 2)
			{
				var f = new RecFrame { T = t, P = new double[] { 1, 2 } };
				for (int k = 0; k < 30; k++)
				{
					if (rnd.NextDouble() < 0.1) { live.Remove(k); continue; }
					if (!live.TryGetValue(k, out var r)) live[k] = r = new double[] { rnd.Next(1, 900), rnd.Next(-5000, 5000) + rnd.NextDouble(), rnd.Next(-5000, 5000), rnd.Next(-20, 20) * 0.37, rnd.Next(-20, 20) * 0.1, 30, 16, 16, k };
					r[3] += rnd.Next(-3, 4) * 0.13; r[4] += rnd.NextDouble() < 0.3 ? 0.3 : 0; r[1] += r[3] * 2 * (k % 3 == 0 ? 3 : 1) + (k % 5 == 0 ? rnd.Next(-40, 40) : 0); r[2] += r[4] * 2;
					if (k == 3 && t == 40) r[5] = double.NaN; else if (k == 3) r[5] = 30;
					f.J.Add((double[])r.Clone());
					if (k % 4 == 0) f.M.Add(new double[] { 5, r[1], r[2], r[3], r[4], 16, 16, k, 65 + k % 3, 1 + k % 4 }); // format 14: the levies' minions (owner = the levy, kind), row coded like the rest
					if (k == 11 && t % 6 == 0) f.J.Add((double[])r.Clone()); // the same key twice in one sample
				}
				f.B.Add(new double[] { 125, 4, 1e7 + t * 3.3, -t, 1.25, -0.05, 2.5e6 - t, 3e6, 1.5, 2e9, -3, 0.15, 255, 0, 1, -1, 40, 40, 4 });
				string line = Frame(f, codec);
				using var d = JsonDocument.Parse(line);
				var back = dec.Rows(d.RootElement, 3);
				if (back.Count != f.J.Count) return "t=" + t + ": " + back.Count + " projectile rows of " + f.J.Count;
				for (int i = 0; i < back.Count; i++)
					for (int c = 0; c < 9; c++)
					{
						double want = RowCodec.Q(f.J[i][c], c == 3 || c == 4) / (c == 3 || c == 4 ? 10.0 : 1.0);
						if (Math.Abs(back[i][c] - want) > 1e-9) return "t=" + t + " row " + i + " field " + c + ": " + back[i][c] + " for " + want;
					}
				var mm = dec.Rows(d.RootElement, 5);
				if (mm.Count != f.M.Count) return "t=" + t + ": " + mm.Count + " minion rows of " + f.M.Count;
				for (int i = 0; i < mm.Count; i++)
					for (int c = 0; c < 10; c++)
					{
						double want = RowCodec.Q(f.M[i][c], c == 3 || c == 4) / (c == 3 || c == 4 ? 10.0 : 1.0);
						if (Math.Abs(mm[i][c] - want) > 1e-9) return "t=" + t + " minion row " + i + " field " + c + ": " + mm[i][c] + " for " + want;
					}
				var bb = dec.Rows(d.RootElement, 0);
				if (bb.Count != 1 || Math.Abs(bb[0][2] - RowCodec.Q(f.B[0][2], false)) > 1e-9 || Math.Abs(bb[0][9] - 2e9) > 1e-6 || bb[0][18] != 4 || Math.Abs(bb[0][11] - 0.2) > 1e-9) return "t=" + t + ": boss row";
				if (dec.Rows(d.RootElement, 2).Count != 0) return "t=" + t + ": a list with no key";
			}
			return null;
		}

		/// <summary>Boot self-check: serialize a synthetic frame, an event and an arena row, parse them back, compare. Returns null when ok, else the failure text.</summary>
		public static string SelfCheck()
		{
			try
			{
				var f = new RecFrame { T = 1234, P = new double[] { 100.4, 200.6, -3.25, 0, 1, 1 | 16 | 2048, 400, 200, 30, 120, 0, 3187, -1, 0, 0, -1 } };
				f.B.Add(new double[] { 4, 7, 5000, 3000.5, 1.5, -2, 2800, 3000, 1, 0, 0, 0, 0, 0, 2, 12 });
				f.J.Add(new double[] { 83, 5100, 3050, 4, 4, 30 });
				f.L.Add(new double[] { 12, 4900, 3100, 0, 0, 1, 500, 2, 757, 0, 600 });
				f.A.Add(new double[] { 267, 30, 5200, 3000, 1, 2, 40, 40, 2, 12, 20, 20 });
				f.S.Add(new double[] { 1234, 77, 5300, 3100, 38, 40, 70, 5 });
				f.S.Add(new double[] { 77, 5305, 3104 });
				f.C.Add(new double[] { 85, 5150, 3060, 12.5, -3, 16, 16 });
				string line = Frame(f);
				using (var d = JsonDocument.Parse(line))
				{
					var r = d.RootElement;
					if (r.GetProperty("k").GetString() != "f" || r.GetProperty("t").GetInt32() != 1234) return "frame header";
					if (Math.Abs(r.GetProperty("p")[0].GetDouble() - 100.4) > 0.051 || r.GetProperty("p")[5].GetInt32() != (1 | 16 | 2048)) return "player array";
					if (r.GetProperty("b")[0][15].GetInt32() != 12 || r.GetProperty("l")[0][10].GetInt32() != 600) return "format 2 fields";
					if (r.GetProperty("b")[0][1].GetInt32() != 7 || Math.Abs(r.GetProperty("b")[0][3].GetDouble() - 3000.5) > 0.051) return "boss array";
					if (r.GetProperty("j")[0][5].GetInt32() != 30) return "projectile array";
					if (r.GetProperty("l")[0][8].GetInt32() != 757) return "levy array";
					if (r.GetProperty("c")[0][0].GetInt32() != 85 || r.GetProperty("c")[0][5].GetInt32() != 16 || Math.Abs(r.GetProperty("c")[0][3].GetDouble() - 12.5) > 0.051) return "commander projectile array";
					if (r.GetProperty("a")[0][1].GetInt32() != 30 || r.GetProperty("a")[0][9].GetInt32() != 12) return "adds array";
					if (r.GetProperty("s")[0][1].GetInt32() != 77 || r.GetProperty("s")[0][4].GetInt32() != 38 || r.GetProperty("s")[0][6].GetInt32() != 70 || r.GetProperty("s")[1].GetArrayLength() != 3 || r.GetProperty("s")[1][1].GetInt32() != 5305) return "hitbox array";
				}
				string ev = Event(99, "use", ("item", "757"), ("name", Esc("Sword \"X\"\n")));
				using (var d = JsonDocument.Parse(ev))
					if (d.RootElement.GetProperty("e").GetString() != "use" || d.RootElement.GetProperty("item").GetInt32() != 757 || d.RootElement.GetProperty("name").GetString() != "Sword \"X\" ") return "event";
				var codes = new List<int> { 0, 0, 0, 1, 1, 2, 3, 3, 0 };
				var rle = RleEncode(codes);
				string bd = Event(77, "bdeath", ("npc", "4"), ("name", Esc("Eye of Cthulhu")), ("x", N(100.4)), ("y", N(200)), ("life", "0"), ("final", "1"), ("hs", Esc("proj")));
				using (var d = JsonDocument.Parse(bd))
					if (d.RootElement.GetProperty("e").GetString() != "bdeath" || d.RootElement.GetProperty("final").GetInt32() != 1 || d.RootElement.GetProperty("hs").GetString() != "proj") return "boss end event";
				string a = Arena(10, 20, 9, 1, new List<int[]> { rle.ToArray() });
				using (var d = JsonDocument.Parse(a))
				{
					var back = RleDecode(d.RootElement.GetProperty("rows")[0]);
					if (back.Count != codes.Count) return "arena length";
					for (int i = 0; i < codes.Count; i++) if (back[i] != codes[i]) return "arena data";
				}
				string rc = CodecSelfCheck();
				if (rc != null) return "row codec: " + rc;
				return null;
			}
			catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
		}
	}

	/// <summary>
	/// Format 13 row coding of the frame keys b, a, l, j and c (see the format notes above; the Python twin is tools/recview/reccodec.py, the JS one sits in the viewers). One instance per recording;
	/// <see cref="Reset"/> at the start of every part file. Lossless for the stored units: whole pixels, tenths of velocity.
	/// </summary>
	internal sealed class RowCodec
	{
		/// <summary>A row list: its frame key, key field, position fields (x, y), and the fields stored in tenths (the first two are vx, vy).</summary>
		internal sealed class Spec
		{
			public string Key; public int Ki; public int[] Pos, Ten; public bool[] IsTen;
			public Spec(string key, int ki, int n, int[] pos, int[] ten) { Key = key; Ki = ki; Pos = pos; Ten = ten; IsTen = new bool[n]; foreach (int t in ten) IsTen[t] = true; }
			public bool TenAt(int i) => i < IsTen.Length && IsTen[i];
		}
		/// <summary>0 b, 1 a, 2 l, 3 j, 4 c, 5 m.</summary>
		internal static readonly Spec[] Specs =
		{
			new Spec("b", 1, 19, new[] { 2, 3 }, new[] { 4, 5, 8, 9, 10, 11 }),
			new Spec("a", 1, 12, new[] { 2, 3 }, new[] { 4, 5 }),
			new Spec("l", 0, 13, new[] { 1, 2 }, new[] { 3, 4 }),
			new Spec("j", 8, 9, new[] { 1, 2 }, new[] { 3, 4 }),
			new Spec("c", 7, 8, new[] { 1, 2 }, new[] { 3, 4 }),
			new Spec("m", 7, 10, new[] { 1, 2 }, new[] { 3, 4 }),
		};

		internal sealed class State { public long[] Q; public long S0, S1; public bool Vel = true; }
		private readonly Dictionary<long, State>[] _old = new Dictionary<long, State>[6], _new = new Dictionary<long, State>[6];

		public RowCodec() { for (int i = 0; i < 6; i++) { _old[i] = new Dictionary<long, State>(); _new[i] = new Dictionary<long, State>(); } }
		public void Reset() { for (int i = 0; i < 6; i++) { _old[i].Clear(); _new[i].Clear(); } }

		internal static long Q(double v, bool ten)
		{
			if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
			double x = ten ? v * 10 : v;
			if (x > 9e15) x = 9e15; else if (x < -9e15) x = -9e15;
			return (long)Math.Round(x, MidpointRounding.AwayFromZero);
		}

		internal static long FloorDiv10(long x) => x >= 0 ? x / 10 : -((-x + 9) / 10);
		/// <summary>Pixels moved over one sample (2 ticks) by a body whose velocity went from a to b (tenths of a pixel per tick).</summary>
		internal static long VStep(long a, long b) => FloorDiv10(a + b + 5);

		internal static long Pred(Spec sp, long[] pq, long[] q, State st, int pi)
		{
			int p = sp.Pos[pi];
			if (st.Vel) return pq[p] + VStep(pq[sp.Ten[pi]], q[sp.Ten[pi]]);
			return pq[p] + (pi == 0 ? st.S0 : st.S1);
		}

		internal static State Next(Spec sp, long[] pq, long[] q, State st)
		{
			long es = 0, ev = 0;
			for (int pi = 0; pi < 2; pi++)
			{
				int p = sp.Pos[pi]; long step = q[p] - pq[p];
				es += Math.Abs(step - (pi == 0 ? st.S0 : st.S1));
				ev += Math.Abs(step - VStep(pq[sp.Ten[pi]], q[sp.Ten[pi]]));
			}
			return new State { Q = q, S0 = q[sp.Pos[0]] - pq[sp.Pos[0]], S1 = q[sp.Pos[1]] - pq[sp.Pos[1]], Vel = ev <= es };
		}

		internal static void Nat(StringBuilder sb, long q, bool ten)
		{
			if (!ten || q % 10 == 0) { sb.Append(ten ? q / 10 : q); return; }
			if (q < 0) { sb.Append('-'); q = -q; }
			sb.Append(q / 10).Append('.').Append((char)('0' + q % 10));
		}

		/// <summary>Writes ,"key":[entries] for list <paramref name="li"/> (nothing when there are no rows) and moves the state on: entities absent from this sample are forgotten.</summary>
		public void Rows(StringBuilder sb, int li, List<double[]> rows)
		{
			var sp = Specs[li]; var old = _old[li]; var cur = _new[li]; cur.Clear();
			if (rows.Count > 0)
			{
				sb.Append(",\"").Append(sp.Key).Append("\":[");
				for (int r = 0; r < rows.Count; r++)
				{
					if (r > 0) sb.Append(',');
					double[] row = rows[r]; int n = row.Length;
					var q = new long[n];
					for (int i = 0; i < n; i++) q[i] = Q(row[i], sp.TenAt(i));
					long k = q[sp.Ki];
					if (cur.ContainsKey(k) || !old.TryGetValue(k, out var prev) || prev.Q.Length != n)
					{
						sb.Append("[[");
						for (int i = 0; i < n; i++) { if (i > 0) sb.Append(','); Nat(sb, q[i], sp.TenAt(i)); }
						sb.Append("]]");
						cur[k] = new State { Q = q };
						continue;
					}
					long[] pq = prev.Q;
					int mask = 0; int nd = 0; var d = new long[n];
					for (int i = 0; i < n; i++)
					{
						if (i == sp.Ki) continue;
						long pred = i == sp.Pos[0] ? Pred(sp, pq, q, prev, 0) : i == sp.Pos[1] ? Pred(sp, pq, q, prev, 1) : pq[i];
						if (q[i] != pred) { mask |= 1 << i; d[nd++] = q[i] - pred; }
					}
					if (mask == 0) sb.Append(k);
					else { sb.Append('[').Append(k).Append(',').Append(mask); for (int i = 0; i < nd; i++) sb.Append(',').Append(d[i]); sb.Append(']'); }
					cur[k] = Next(sp, pq, q, prev);
				}
				sb.Append(']');
			}
			_old[li] = cur; _new[li] = old;
		}
	}

	/// <summary>Reads format 13 coded rows back into natural rows (and passes plain rows of older files through). One per recording; <see cref="Header"/> on every hdr line.</summary>
	internal sealed class RowDecoder
	{
		public bool On;
		private readonly Dictionary<long, RowCodec.State>[] _old = new Dictionary<long, RowCodec.State>[6];
		public RowDecoder() { for (int i = 0; i < 6; i++) _old[i] = new Dictionary<long, RowCodec.State>(); }

		public void Header(JsonElement hdr)
		{
			On = hdr.TryGetProperty("codec", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() == 1;
			for (int i = 0; i < 6; i++) _old[i].Clear();
		}

		private static double Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : 0;

		/// <summary>The rows of list <paramref name="li"/> (0 b, 1 a, 2 l, 3 j, 4 c, 5 m) in this frame, natural units. Call it for every frame, also when the key is absent, so entities that left are forgotten.</summary>
		public List<double[]> Rows(JsonElement frame, int li)
		{
			var sp = RowCodec.Specs[li]; var res = new List<double[]>();
			bool has = frame.TryGetProperty(sp.Key, out var arr) && arr.ValueKind == JsonValueKind.Array;
			if (!On)
			{
				if (has) foreach (var r in arr.EnumerateArray()) { if (r.ValueKind != JsonValueKind.Array) continue; var o = new double[r.GetArrayLength()]; int i = 0; foreach (var v in r.EnumerateArray()) o[i++] = Num(v); res.Add(o); }
				return res;
			}
			var old = _old[li]; var cur = new Dictionary<long, RowCodec.State>();
			if (has)
				foreach (var e in arr.EnumerateArray())
				{
					long[] q; long k;
					if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0 && e[0].ValueKind == JsonValueKind.Array)
					{
						var r = e[0]; int n = r.GetArrayLength(); q = new long[n];
						for (int i = 0; i < n; i++) q[i] = RowCodec.Q(Num(r[i]), sp.TenAt(i));
						if (sp.Ki >= n) continue;
						k = q[sp.Ki]; cur[k] = new RowCodec.State { Q = q };
					}
					else
					{
						int mask = 0; int vi; JsonElement ea = default; bool isArr = e.ValueKind == JsonValueKind.Array;
						if (isArr) { if (e.GetArrayLength() < 2) continue; k = (long)Num(e[0]); mask = (int)Num(e[1]); ea = e; vi = 2; }
						else if (e.ValueKind == JsonValueKind.Number) { k = (long)Num(e); vi = 0; }
						else continue;
						if (!old.TryGetValue(k, out var prev)) continue; // coded with nothing before it: a reader that started mid-part
						long[] pq = prev.Q; int n = pq.Length; q = (long[])pq.Clone();
						var d = new long[n]; var has2 = new bool[n];
						for (int i = 0; i < n && mask >> i != 0; i++)
							if ((mask >> i & 1) != 0) { has2[i] = true; d[i] = isArr && vi < ea.GetArrayLength() ? (long)Num(ea[vi]) : 0; vi++; }
						for (int i = 0; i < n; i++) if (i != sp.Pos[0] && i != sp.Pos[1] && has2[i]) q[i] += d[i];
						for (int pi = 0; pi < 2; pi++) { int p = sp.Pos[pi]; q[p] = RowCodec.Pred(sp, pq, q, prev, pi) + d[p]; }
						cur[k] = RowCodec.Next(sp, pq, q, prev);
					}
					var o = new double[q.Length];
					for (int i = 0; i < q.Length; i++) o[i] = sp.TenAt(i) ? q[i] / 10.0 : q[i];
					res.Add(o);
				}
			_old[li] = cur;
			return res;
		}
	}
}
