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
	///         lgear (one levy): id, g = [[slot,type,prefix,stack],..] with slot 0 weapon, 1 sidearm, 2 head, 3 body, 4 legs, 5-10 accessories, 11 ammo pouch, 12 hook.
	///         names gains prefixes (id to name). Item names are in names.items.
	///         Format 7 adds the frame key s: the hitbox of EVERY other hostile NPC within 140 tiles of the commander that b and a do not carry (the segments of secondary worms, damage-0 adds), SchemaS, integer pixels.
	///         A row is full (8 numbers) the first time an NPC slot is seen or when its type, size, realLife or flags change, and just [id,x,y] after that. Hostile projectile hitboxes (j: w,h) were already there since format 3.
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
	}

	internal static class FightRecFormat
	{
		public const int Version = 7;
		public const string SchemaP = "x,y,vx,vy,dir,flags,life,mana,immune,wingTime,rocketTime,held,mount,hookX,hookY,dashDelay";
		public const string SchemaPFlags = "1 grounded,2 flying,4 hookOut,8 hookAttached,16 left,32 right,64 jump,128 down,256 up,512 grapple,1024 useItem,2048 dashing,4096 mounted,8192 dead";
		public const string SchemaB = "type,id,x,y,vx,vy,life,lifeMax,ai0,ai1,ai2,ai3,target,isPart,tkind,tlevy,w,h";
		public const string TKinds = "0 none,1 player,2 levy (tlevy = levy record id)";
		public const string SchemaA = "type,id,x,y,vx,vy,life,lifeMax,tkind,tlevy,w,h";
		public const string SchemaS = "type,id,x,y,w,h,realLife,flags  (a row of 3 = id,x,y: same type, size, realLife and flags as that id's last full row)";
		public const string SFlags = "1 deals contact damage,2 does not take damage,4 body segment (realLife = the head that owns its life)";
		public const string SchemaJ = "type,x,y,vx,vy,damage,w,h";
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
			Rows(sb, "b", f.B); Rows(sb, "j", f.J); Rows(sb, "l", f.L); Rows(sb, "a", f.A); Rows(sb, "s", f.S);
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
