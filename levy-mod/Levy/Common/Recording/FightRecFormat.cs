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
	///         bdeath (the boss died) / bdespawn (it left alive): tick t, npc, name, id, x,y last position px, life, max, final (1 = the fight's end), and the last hit ht (tick), hd (damage), hs (item|proj|other), hy (item / projectile type), hn (name). end gains outcome (killed|despawn|none) and arenaChunks.
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
	///         RESULT RULE (file name, end.result, the report, the viewer): won = the last boss was killed. A boss that LEAVES (bdespawn) is lost when the commander's fall is why: she was dead at that moment, or her latest cdeath is no more than 60 s (FightRecorder.LostWindowTicks)
	///         before the boss's end tick. A boss with no living player to target despawns, so her death reads as the boss leaving; the 60 s covers a boss still on its way out after she respawned (Death / Master respawn timers run past 15 s). It is fled when she was alive and well
	///         and not recently fallen: dawn, distance, a timer. What the army does is not part of the rule: levies targeted or not, dead or alive, only the commander counts. With no boss end at all (the 20 minute limit, the recorder stopped) the result is lost when she fell and a boss was
	///         seen, else ended; interrupted = the world closed. Older files are re-read with the same rule by tools/recview/recrules.py (needs the cdeath events and, for "dead at that moment", the p flag 8192 of the frames).
	///         EVENT NPCs ARE NOT BOSSES for the recorder (FightRecorder.EventNpcTypes): the four Celestial Pillars, the Martian Saucer with its turrets, cannons and core, the Flying Dutchman and its cannons, and the Pumpkin Moon / Frost Moon wave bosses (Mourning Wood, Pumpking,
	///         Everscream, Ice Queen, Santa-NK1). They never start a boss recording, keep one running, merge into one or add to its name, so the Cultist's win ends when the Cultist falls and the pillars it calls are not part of it. Their hitboxes still appear (key s, and a for those that
	///         touch-damage). No automatic recording of an event on its own: "Record fights automatically: all fights" still records the skirmish around the commander, under the label of the event or "Fight", as it always did; "boss fights" does not record pillars.
	///         Older files stay readable: every new field is appended or optional.
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
		/// <summary>Format 11: the commander's own projectiles [type,x,y,vx,vy,w,h].</summary>
		public List<double[]> C = new();
	}

	internal static class FightRecFormat
	{
		public const int Version = 11;
		public const string SchemaP = "x,y,vx,vy,dir,flags,life,mana,immune,wingTime,rocketTime,held,mount,hookX,hookY,dashDelay";
		public const string SchemaPFlags = "1 grounded,2 flying,4 hookOut,8 hookAttached,16 left,32 right,64 jump,128 down,256 up,512 grapple,1024 useItem,2048 dashing,4096 mounted,8192 dead";
		public const string SchemaB = "type,id,x,y,vx,vy,life,lifeMax,ai0,ai1,ai2,ai3,target,isPart,tkind,tlevy,w,h";
		public const string TKinds = "0 none,1 player,2 levy (the boss targets a levy: tlevy = its id),3 spite (redirected shots: the boss rule's grudge sends its projectiles and dashes at a levy that is too close or does a lot of damage; tlevy = that levy)";
		public const string SchemaA = "type,id,x,y,vx,vy,life,lifeMax,tkind,tlevy,w,h";
		public const string SchemaS = "type,id,x,y,w,h,realLife,flags  (a row of 3 = id,x,y: same type, size, realLife and flags as that id's last full row)";
		public const string SFlags = "1 deals contact damage,2 does not take damage,4 body segment (realLife = the head that owns its life)";
		public const string SchemaJ = "type,x,y,vx,vy,damage,w,h";
		public const string SchemaC = "type,x,y,vx,vy,w,h";
		public const string SchemaL = "id,x,y,vx,vy,flags,life,state,held,wingTime,lifeMax,w,h";
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

		public static string Frame(RecFrame f)
		{
			var sb = new StringBuilder(256);
			sb.Append("{\"k\":\"f\",\"t\":").Append(f.T).Append(",\"p\":"); Arr(sb, f.P);
			Rows(sb, "b", f.B); Rows(sb, "j", f.J); Rows(sb, "l", f.L); Rows(sb, "a", f.A); Rows(sb, "s", f.S); Rows(sb, "c", f.C);
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
				return null;
			}
			catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
		}
	}
}
