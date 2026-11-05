using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ON-HIT PROBE (measurement only; changes no game behaviour; inert unless LEVY_HARNESS and LEVY_ONHIT are both set). Answers one question: when a SOLDIER's weapon hits, which
	/// projectiles / dust / NPCs / items / particles / combat texts / ModPlayer hooks appear, WHERE (near the soldier, near the target, or near the far-away commander) and through WHICH call path.
	///
	/// WHAT IT ADDS TO THE COMBAT HARNESS
	///  * FAR commander: the stand-in commander (Main.player[0]) is pinned LEVY_ONHIT_FAR tiles (default 70) to the left of the soldier and ~58 tiles above the floor; the soldier is put on Hold.
	///  * SINGLE-PLAYER OWNER EMULATION (LEVY_ONHIT_SP, default on): a dedicated server has Main.myPlayer = 255, so soldier shots would be owned by 255 and vanilla would never call the commander's
	///    ModPlayer hooks (Main.player[255] is the inactive null player). In a real single-player game Main.myPlayer is the commander, so every soldier shot is owned by the commander. Here
	///    Main.myPlayer is set to 0 around each Main.Update of a soldier window (and put back to 255 around player 0's own Update, exactly as before), which is what a single-player game does.
	///  * Loadout of the commander: LEVY_ONHIT_GEAR (comma list "Mod/Item", worn), LEVY_ONHIT_HELD ("Mod/Item" in the selected hotbar slot), LEVY_ONHIT_GEARSCAN (comma list of mods whose accessories /
	///    armor sets are tried one at a time, each with an idle control and with every LEVY_ONHIT_REFS weapon).
	///  * Recorders: every projectile spawn (GlobalProjectile.OnSpawn), NPC spawn, item spawn, Dust.NewDust, CombatText, ParticleOrchestrator request is logged with its position, a classification of
	///    that position (SOL = within 12 tiles of the soldier, TGT = within 6 tiles of the target, CMD = within 12 tiles of the commander, OTHER), the EntitySource and a condensed stack trace.
	///    Projectiles are then tracked each tick (closest approach to the commander). A ModPlayer counts the commander's own OnHitNPCWithProj / ModifyHitNPCWithProj / OnHitNPC invocations.
	///  * Output: LEVY_HARNESS_OUT + ".onhit.tsv" (one row per distinct event signature per trial).
	/// </summary>
	public class HarnessOnHit : ModSystem
	{
		internal static bool Enabled, Emulate, WithBaseline;
		internal static int FarTiles = 70;
		internal static SoldierOrder FarOrder = SoldierOrder.FreeRein;
		internal static string[] FixedGear = Array.Empty<string>();
		internal static string FixedHeld = "";
		internal static string[] GearScanMods, GearFilter, RefNames = { "Musket", "AmethystStaff" };
		internal static string GearKind = "acc";
		internal static string ListPath, ListFilter;
		internal static int MaxGear = int.MaxValue;
		internal const int FarY = 62; // commander top row (tiles); the floor is at 120

		// ---- window state ----
		internal static bool Active;
		private static string _label = "", _weapon = "", _gear = "", _held = "", _range = "";
		private static int _stage;
		private static int _tick;
		private static int _lastHitTick = -100;
		internal static Vector2 CmdPos, SolPos, TgtPos;
		private static readonly object Lock = new();

		private sealed class Agg
		{
			public string Kind, Name, Mod, Loc, AfterHit, Tagged, Owner, Source, Stack; public int Count, FirstTick; public float MinCmd = 99999f; public Vector2 Sample;
		}
		private sealed class Track { public Projectile P; public int Identity; public string Name; public bool Tagged; public float MinCmd = 99999f, MaxSol = 0f; public bool Seen; public Agg A; }

		// ---- commander state tracker: every numeric field of the commander's Thorium / Calamity ModPlayers (+ a few vanilla stats), start / end / min / max, and WHEN it changed ----
		private sealed class Fld { public double Start, Last, Min, Max; public int NFire, NHit, NOther, First = -1; }
		private static Dictionary<string, Fld> _state = new();
		private static List<(ModPlayer tmpl, MemberInfo f, string name)> _stateFields;
		private static readonly HashSet<int> _spawnTicks = new(), _hitTicks = new();
		private static Dictionary<string, double> Snap()
		{
			if (_stateFields == null)
			{
				_stateFields = new();
				foreach (ModPlayer m in ModContent.GetContent<ModPlayer>())
				{
					string mn = m.Mod.Name;
					if (mn != "ThoriumMod" && mn != "CalamityMod") continue;
					foreach (FieldInfo f in m.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
					{
						Type t = f.FieldType;
						if (t == typeof(int) || t == typeof(float) || t == typeof(double) || t == typeof(bool) || t == typeof(short) || t == typeof(byte) || t == typeof(uint) || t == typeof(long))
							_stateFields.Add((m, f, mn + "." + m.GetType().Name + "." + f.Name));
					}
					foreach (PropertyInfo pi in m.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
					{
						Type t = pi.PropertyType;
						if (!pi.CanRead || pi.GetIndexParameters().Length > 0 || pi.DeclaringType != m.GetType()) continue;
						if (t == typeof(int) || t == typeof(float) || t == typeof(double) || t == typeof(bool) || t == typeof(short) || t == typeof(byte) || t == typeof(uint) || t == typeof(long))
							_stateFields.Add((m, pi, mn + "." + m.GetType().Name + "." + pi.Name + "()"));
					}
				}
			}
			var d = new Dictionary<string, double>(_stateFields.Count + 8);
			Player p = Main.player[0];
			d["Player.statLife"] = p.statLife; d["Player.statMana"] = p.statMana; d["Player.statDefense"] = p.statDefense;
			int nb = 0; foreach (int b in p.buffType) if (b > 0) nb++; d["Player.buffCount"] = nb;
			foreach (var (tmpl, f, name) in _stateFields)
			{
				try { ModPlayer inst = p.GetModPlayer(tmpl); object v = f is FieldInfo fi ? fi.GetValue(inst) : ((PropertyInfo)f).GetValue(inst); d[name] = v is bool b ? (b ? 1 : 0) : Convert.ToDouble(v); } catch (Exception) { }
			}
			return d;
		}
		private static void StateTick()
		{
			var d = Snap();
			foreach (var kv in d)
			{
				if (!_state.TryGetValue(kv.Key, out Fld f)) { _state[kv.Key] = new Fld { Start = kv.Value, Last = kv.Value, Min = kv.Value, Max = kv.Value }; continue; }
				if (kv.Value != f.Last)
				{
					bool hit = _hitTicks.Contains(_tick) || _hitTicks.Contains(_tick - 1) || _hitTicks.Contains(_tick - 2);
					bool fire = _spawnTicks.Contains(_tick);
					if (hit) f.NHit++; else if (fire) f.NFire++; else f.NOther++;
					if (f.First < 0) f.First = _tick;
					f.Last = kv.Value;
				}
				if (kv.Value < f.Min) f.Min = kv.Value; if (kv.Value > f.Max) f.Max = kv.Value;
			}
		}

		private static Dictionary<string, Agg> _agg = new();
		private static readonly List<Track> _tracks = new();
		private static readonly List<string> _rows = new();
		private static int _hits, _hitsTagged, _derivedHits;
		private static int _stackBudget;
		private static string _outPath;

		public override void Load()
		{
			try
			{
				string h = Environment.GetEnvironmentVariable("LEVY_HARNESS"), o = Environment.GetEnvironmentVariable("LEVY_ONHIT");
				if (string.IsNullOrEmpty(h) || h == "0" || string.IsNullOrEmpty(o) || o == "0") return;
				Enabled = true;
				Emulate = Environment.GetEnvironmentVariable("LEVY_ONHIT_SP") != "0";
				WithBaseline = Environment.GetEnvironmentVariable("LEVY_ONHIT_BASELINE") == "1";
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_ONHIT_FAR"), out int far) && far >= 0 && far <= 95) FarTiles = far;
				if (Enum.TryParse(Environment.GetEnvironmentVariable("LEVY_ONHIT_ORDER"), true, out SoldierOrder ord)) FarOrder = ord;
				FixedGear = Split(Environment.GetEnvironmentVariable("LEVY_ONHIT_GEAR"));
				FixedHeld = Environment.GetEnvironmentVariable("LEVY_ONHIT_HELD") ?? "";
				var gs = Split(Environment.GetEnvironmentVariable("LEVY_ONHIT_GEARSCAN")); if (gs.Length > 0) GearScanMods = gs;
				var gf = Split(Environment.GetEnvironmentVariable("LEVY_ONHIT_GEARFILTER")); if (gf.Length > 0) GearFilter = gf;
				var rf = Split(Environment.GetEnvironmentVariable("LEVY_ONHIT_REFS")); if (rf.Length > 0) RefNames = rf;
				GearKind = Environment.GetEnvironmentVariable("LEVY_ONHIT_GEARKIND") ?? "acc";
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_ONHIT_MAXGEAR"), out int mg) && mg > 0) MaxGear = mg;
				ListPath = Environment.GetEnvironmentVariable("LEVY_ONHIT_LIST"); ListFilter = Environment.GetEnvironmentVariable("LEVY_ONHIT_LISTFILTER") ?? "";
				_outPath = (Environment.GetEnvironmentVariable("LEVY_HARNESS_OUT") ?? "levy_harness.tsv") + ".onhit.tsv";
				On_Dust.NewDust += OnNewDust;
				On_CombatText.NewText_Rectangle_Color_string_bool_bool += OnCombatText;
				Terraria.GameContent.Drawing.On_ParticleOrchestrator.RequestParticleSpawn += OnParticle;
				On_Player.Update += OnPlayerUpdate;
				Mod.Logger.Info($"ONHIT: ENABLED far={FarTiles} emulateSP={Emulate} gear=[{string.Join(",", FixedGear)}] held={FixedHeld} gearscan=[{(GearScanMods == null ? "" : string.Join(",", GearScanMods))}] refs=[{string.Join(",", RefNames)}] kind={GearKind}");
			}
			catch (Exception e) { Enabled = false; Mod.Logger.Warn("ONHIT: load failed, disabled: " + e); }
		}

		public override void Unload()
		{
			try { On_Dust.NewDust -= OnNewDust; On_CombatText.NewText_Rectangle_Color_string_bool_bool -= OnCombatText; Terraria.GameContent.Drawing.On_ParticleOrchestrator.RequestParticleSpawn -= OnParticle; On_Player.Update -= OnPlayerUpdate; } catch { }
			Enabled = false; Active = false;
		}

		private static string[] Split(string s) => string.IsNullOrWhiteSpace(s) ? Array.Empty<string>() : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		public override void OnWorldLoad()
		{
			if (!Enabled || string.IsNullOrEmpty(ListPath)) return;
			try
			{
				var sb = new StringBuilder();
				var f = Split(ListFilter);
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
				{
					Item it = ContentSamples.ItemsByType.TryGetValue(t, out var s) ? s : null;
					if (it == null || it.ModItem == null) continue;
					string disp = it.Name ?? "", inn = it.ModItem.Name;
					if (f.Length > 0 && !f.Any(x => disp.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0 || inn.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
					sb.AppendLine($"{t}\t{it.ModItem.Mod.Name}/{inn}\t{disp}\tdmg={it.damage}\tclass={it.DamageType?.GetType().Name}\tacc={it.accessory}\thead={it.headSlot}\tbody={it.bodySlot}\tlegs={it.legSlot}\tshoot={it.shoot}\tuseStyle={it.useStyle}");
				}
				File.WriteAllText(ListPath, sb.ToString());
				Mod.Logger.Info("ONHIT: item list written to " + ListPath);
			}
			catch (Exception e) { Mod.Logger.Warn("ONHIT: list failed: " + e.Message); }
			Environment.Exit(0);
		}

		// =========================================================================================================
		// Window control (called by CombatHarness)
		// =========================================================================================================
		internal static void BeginWindow(string weapon, string gear, string range, int stage)
		{
			lock (Lock)
			{
				_weapon = weapon; _gear = gear; _range = range; _stage = stage; _held = FixedHeld;
				_label = weapon + " | gear=" + (gear == "" ? "-" : gear) + " | held=" + (FixedHeld == "" ? "-" : FixedHeld) + " | r=" + range + " | stage" + stage;
				_agg = new(); _tracks.Clear(); _tick = 0; _lastHitTick = -100; _hits = _hitsTagged = _derivedHits = 0; _stackBudget = 4000;
				CmdCnt.Clear(); _state = new(); _spawnTicks.Clear(); _hitTicks.Clear();
				try { foreach (var kv in Snap()) _state[kv.Key] = new Fld { Start = kv.Value, Last = kv.Value, Min = kv.Value, Max = kv.Value }; } catch (Exception) { }
				if (Emulate && stage == 0) global::Levy.Common.Weapons.OwnerPuppet.EmulateClientCache(Main.player[0]); // path A needs the client's local-player cache
				Active = true;
			}
		}

		internal static void Tick(Vector2 sol, Vector2 tgt)
		{
			if (!Active) return;
			lock (Lock)
			{
				_tick++;
				CmdPos = Main.player[0].Center; SolPos = sol; TgtPos = tgt;
				try { StateTick(); } catch (Exception) { }
				for (int i = _tracks.Count - 1; i >= 0; i--)
				{
					Track t = _tracks[i];
					Projectile p = t.P;
					if (!p.active || p.identity != t.Identity) { Finalize(t); _tracks.RemoveAt(i); continue; }
					if (!t.Seen)
					{
						t.Seen = true;
						bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tg) && tg.IsSoldierShot;
						t.Tagged = tagged; if (t.A != null) t.A.Tagged = tagged ? "Y" : "N";
					}
					float dc = Vector2.Distance(p.Center, CmdPos) / 16f, ds = Vector2.Distance(p.Center, SolPos) / 16f;
					if (dc < t.MinCmd) t.MinCmd = dc;
					if (ds > t.MaxSol) t.MaxSol = ds;
				}
			}
		}

		private static void Finalize(Track t)
		{
			if (t.A != null && t.MinCmd < t.A.MinCmd) t.A.MinCmd = t.MinCmd;
			string key = "TRAJ|" + t.Name + "|" + (t.Tagged ? "Y" : "N");
			if (!_agg.TryGetValue(key, out Agg a)) _agg[key] = a = new Agg { Kind = "PROJ-TRAJ", Name = t.Name, Mod = "-", Loc = "-", AfterHit = "-", Tagged = t.Tagged ? "Y" : "N", Owner = "-", Source = "-", Stack = "-" };
			a.Count++; if (t.MinCmd < a.MinCmd) a.MinCmd = t.MinCmd;
			if (t.MinCmd <= 12f) a.Source = "reachedCmd x" + (a.Source.StartsWith("reachedCmd x") ? int.Parse(a.Source.Substring(12)) + 1 : 1);
			if (a.Sample.X < t.MaxSol) a.Sample = new Vector2(t.MaxSol, 0);
		}

		internal static void EndWindow(long damage, int hitTicks, int projTotal, int cmdDefense)
		{
			lock (Lock)
			{
				if (!Active) return;
				Active = false;
				foreach (Track t in _tracks) Finalize(t);
				_tracks.Clear();
				string head = $"{_label}";
				_rows.Add(Row("TRIAL", "-", "-", "-", "-", "-", "-", $"dmg={damage};hitTicks={hitTicks};soldierProj={projTotal};projHits={_hits};taggedHits={_hitsTagged};derivedHits={_derivedHits};cmdDef={cmdDefense}", 1, 0, -1f, Vector2.Zero, "-"));
				foreach (var kv in _state.Where(k => k.Value.Last != k.Value.Start || k.Value.Min != k.Value.Max).OrderBy(k => k.Key))
					_rows.Add(Row("STATE", kv.Key, "-", "CMD", "-", "-", "-", $"{kv.Value.Start:0.###}>{kv.Value.Last:0.###} (min {kv.Value.Min:0.###} max {kv.Value.Max:0.###})", kv.Value.NFire + kv.Value.NHit + kv.Value.NOther, kv.Value.First, -1f, Vector2.Zero, $"changes: onFireTick={kv.Value.NFire} afterHit={kv.Value.NHit} other={kv.Value.NOther}"));
				foreach (var kv in CmdCnt) _rows.Add(Row("CMDHOOK", kv.Key.Split('|')[0], "-", "CMD", "-", kv.Key.Split('|')[1], "-", "ModPlayer(commander)", kv.Value, 0, -1f, Vector2.Zero, "-"));
				foreach (Agg a in _agg.Values.OrderBy(x => x.Kind).ThenBy(x => x.Name))
					_rows.Add(Row(a.Kind, a.Name, a.Mod, a.Loc, a.AfterHit, a.Tagged, a.Owner, a.Source, a.Count, a.FirstTick, a.MinCmd, a.Sample, a.Stack));
				_rows.RemoveAll(r => false);
				Flush();
			}
		}

		private static string Row(string kind, string name, string mod, string loc, string afterHit, string tagged, string owner, string source, int count, int firstTick, float minCmd, Vector2 sample, string stack)
		{
			string[] f = { _label, _stage.ToString(), _weapon, _gear == "" ? "-" : _gear, FixedHeld == "" ? "-" : FixedHeld, _range, kind, name, mod, loc, afterHit, tagged, owner, source, count.ToString(), firstTick.ToString(),
				minCmd < 99000f && minCmd >= 0f ? minCmd.ToString("0.0") : "-", $"{sample.X / 16f:0},{sample.Y / 16f:0}", stack };
			return string.Join("\t", f.Select(x => (x ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ")));
		}

		private const string Header = "trial\tstage\tweapon\tgear\theld\trangeTiles\tkind\tname\tmod\tloc\tafterHit\ttagged\towner\tsource\tcount\tfirstTick\tminDistCmdTiles\tsampleTile\tstack";
		private static void Flush()
		{
			try
			{
				var sb = new StringBuilder(); sb.AppendLine(Header);
				foreach (var r in _rows) sb.AppendLine(r);
				string tmp = _outPath + ".tmp"; File.WriteAllText(tmp, sb.ToString()); File.Move(tmp, _outPath, true);
			}
			catch (Exception) { }
		}

		// =========================================================================================================
		// Commander loadout
		// =========================================================================================================
		internal static Item MakeItem(string spec)
		{
			try
			{
				int type;
				if (spec.Contains('/')) { if (!ModContent.TryFind<ModItem>(spec, out ModItem mi)) return null; type = mi.Type; }
				else if (ItemID.Search.TryGetId(spec, out int id)) type = id;
				else return null;
				Item it = new Item(); it.SetDefaults(type); return it;
			}
			catch (Exception) { return null; }
		}

		internal static string LoadoutLabel = "";
		/// <summary>Clears and re-applies the commander's gear: the fixed env list plus this trial's scan gear.</summary>
		internal static void ApplyLoadout(Player p, int[] scanGear)
		{
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
			for (int i = 0; i < p.miscEquips.Length; i++) p.miscEquips[i] = new Item();
			var items = new List<Item>();
			foreach (string s in FixedGear) { Item it = MakeItem(s); if (it != null) items.Add(it); }
			if (scanGear != null) foreach (int t in scanGear) { Item it = new Item(); it.SetDefaults(t); items.Add(it); }
			int acc = 3;
			foreach (Item it in items)
			{
				if (it.headSlot >= 0) p.armor[0] = it;
				else if (it.bodySlot >= 0) p.armor[1] = it;
				else if (it.legSlot >= 0) p.armor[2] = it;
				else if (it.accessory) { if (acc < 10) p.armor[acc++] = it; }
				else p.inventory[0] = it; // a held item (scan kind 'held'): selected hotbar slot 0
			}
			p.selectedItem = 0;
			if (FixedHeld != "") { Item h = MakeItem(FixedHeld); if (h != null) p.inventory[0] = h; }
		}

		// =========================================================================================================
		// Recorders
		// =========================================================================================================
		private static string Loc(Vector2 pos)
		{
			if (Vector2.Distance(pos, CmdPos) <= 12 * 16f) return "CMD";
			if (Vector2.Distance(pos, TgtPos) <= 6 * 16f) return "TGT";
			if (Vector2.Distance(pos, SolPos) <= 12 * 16f) return "SOL";
			return "OTHER";
		}

		internal static string ProjName(Projectile p) => p.ModProjectile != null ? p.ModProjectile.Mod.Name + "/" + p.ModProjectile.Name : (ProjectileID.Search.TryGetName(p.type, out string n) ? n : "vanilla" + p.type);
		private static string NpcName(NPC n) => n.ModNPC != null ? n.ModNPC.Mod.Name + "/" + n.ModNPC.Name : (NPCID.Search.TryGetName(n.type, out string s) ? s : n.TypeName);
		private static string Ent(Entity e)
		{
			switch (e)
			{
				case null: return "null";
				case Player pl: return "Player#" + pl.whoAmI + (pl.whoAmI == 0 ? "(commander)" : "");
				case Projectile pr: return "Proj:" + ProjName(pr) + (pr.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.IsSoldierShot ? "(soldier)" : "") + "/owner" + pr.owner;
				case NPC n: return "NPC:" + (n.ModNPC is SoldierNPC ? "SOLDIER" : NpcName(n));
				case Item i: return "Item:" + i.Name;
				default: return e.GetType().Name;
			}
		}
		private static string Src(IEntitySource s)
		{
			if (s == null) return "null";
			string n = s.GetType().Name.Replace("EntitySource_", "");
			try
			{
				switch (s)
				{
					case EntitySource_OnHit oh: n += "[" + Ent(oh.Attacker) + ">" + Ent(oh.Victim) + "]"; break;
					case EntitySource_ItemUse iu: n += "[" + (iu.Item?.Name ?? "?") + "@" + Ent(iu.Player) + "]"; break;
					case EntitySource_Parent pa: n += "[" + Ent(pa.Entity) + "]"; break;
				}
			}
			catch (Exception) { }
			if (!string.IsNullOrEmpty(s.Context)) n += "{" + s.Context + "}";
			return n;
		}

		private static readonly string[] NoiseNs = { "System.", "MonoMod.", "Microsoft.", "Levy.Common.Testing.", "Terraria.ModLoader.Core.", "ReLogic." };
		/// <summary>Condensed stack: skips System / MonoMod / harness frames and detour trampolines, keeps the first 9 meaningful frames (mod frames AND the vanilla/tML ones that show the path).</summary>
		private static string Stack(int skip)
		{
			try
			{
				var st = new StackTrace(skip + 1, false);
				var parts = new List<string>();
				foreach (StackFrame f in st.GetFrames())
				{
					var m = f.GetMethod(); var t = m?.DeclaringType;
					if (t == null) continue;
					string tn = t.FullName ?? t.Name;
					if (NoiseNs.Any(ns => tn.StartsWith(ns))) continue;
					if (tn.Contains("DMD") || m.Name.StartsWith("hook_") || m.Name.StartsWith("<")) continue;
					if (tn == "Terraria.Projectile" && (m.Name == "NewProjectile" || m.Name == "NewProjectileDirect")) continue;
					if (tn == "Terraria.Dust" && (m.Name.StartsWith("NewDust") || m.Name.StartsWith("QuickDust"))) { parts.Add(tn.Replace("Terraria.", "") + "." + m.Name); continue; }
					string asm = t.Assembly.GetName().Name;
					string nm = (asm == "Terraria" || asm == "tModLoader" ? tn.Replace("Terraria.ModLoader.", "TML.").Replace("Terraria.", "") : tn) + "." + m.Name;
					if (parts.Count == 0 || parts[^1] != nm) parts.Add(nm);
					if (parts.Count >= 9) break;
				}
				return string.Join(" < ", parts);
			}
			catch (Exception e) { return "stack-failed:" + e.Message; }
		}

		private static void Add(string kind, string name, string mod, Vector2 pos, string owner, string source, string stack, Track tr = null)
		{
			string loc = Loc(pos);
			string after = (_tick - _lastHitTick) <= 2 ? "Y" : "N";
			string skey = stack == null ? "" : stack;
			string key = kind + "|" + name + "|" + loc + "|" + after + "|" + owner + "|" + source + "|" + skey;
			if (!_agg.TryGetValue(key, out Agg a))
				_agg[key] = a = new Agg { Kind = kind, Name = name, Mod = mod, Loc = loc, AfterHit = after, Tagged = "-", Owner = owner, Source = source, Stack = stack ?? "-", FirstTick = _tick, Sample = pos };
			a.Count++;
			float dc = Vector2.Distance(pos, CmdPos) / 16f; if (dc < a.MinCmd) a.MinCmd = dc;
			if (tr != null) tr.A = a;
		}

		internal static void OnProjSpawn(Projectile p, IEntitySource src)
		{
			if (!Active) return;
			lock (Lock)
			{
				_spawnTicks.Add(_tick);
				string name = ProjName(p);
				string mod = p.ModProjectile?.Mod.Name ?? "Terraria";
				string owner = "owner" + (p.owner == 255 ? "255" : p.owner == 0 ? "0(commander)" : p.owner.ToString());
				string stack = Stack(2);
				var tr = new Track { P = p, Identity = p.identity, Name = name };
				if (_tracks.Count < 3000) _tracks.Add(tr);
				Add("PROJ", name, mod, p.Center, owner, Src(src), stack, tr);
			}
		}

		internal static void OnNpcSpawn(NPC n, IEntitySource src)
		{
			if (!Active || n.ModNPC is SoldierNPC) return;
			lock (Lock) { if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) return; Add("NPC", NpcName(n), n.ModNPC?.Mod.Name ?? "Terraria", n.Center, "-", Src(src), Stack(2)); }
		}

		internal static void OnItemSpawn(Item it, IEntitySource src)
		{
			if (!Active) return;
			lock (Lock) Add("ITEM", it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : it.Name, it.ModItem?.Mod.Name ?? "Terraria", it.Center, "-", Src(src), Stack(2));
		}

		internal static void OnProjHit(Projectile p, NPC target, int dmg)
		{
			if (!Active) return;
			lock (Lock)
			{
				bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tg) && tg.IsSoldierShot;
				_hits++; if (tagged) { _hitsTagged++; _lastHitTick = _tick; _hitTicks.Add(_tick); } else _derivedHits++;
			}
		}

		private static int OnNewDust(On_Dust.orig_NewDust orig, Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)
		{
			if (Active)
			{
				try
				{
					lock (Lock)
					{
						Vector2 c = Position + new Vector2(Width * 0.5f, Height * 0.5f);
						string loc = Loc(c);
						// the soldier's own ambient dust (SOL / TGT) is only sampled; anything near the commander or elsewhere is always stack-traced
						bool full = loc == "CMD" || loc == "OTHER" || _stackBudget > 0;
						if (loc != "CMD" && loc != "OTHER") _stackBudget--;
						Add("DUST", "dust" + Type, "-", c, "-", "-", full ? Stack(1) : "(sampled)");
					}
				}
				catch (Exception) { }
			}
			return orig(Position, Width, Height, Type, SpeedX, SpeedY, Alpha, newColor, Scale);
		}

		private static int OnCombatText(On_CombatText.orig_NewText_Rectangle_Color_string_bool_bool orig, Rectangle location, Color color, string text, bool dramatic, bool dot)
		{
			if (Active) { try { lock (Lock) Add("COMBATTEXT", text, "-", location.Center.ToVector2(), "-", "-", Stack(1)); } catch (Exception) { } }
			return orig(location, color, text, dramatic, dot);
		}

		private static void OnParticle(Terraria.GameContent.Drawing.On_ParticleOrchestrator.orig_RequestParticleSpawn orig, bool clientOnly, Terraria.GameContent.Drawing.ParticleOrchestraType type, Terraria.GameContent.Drawing.ParticleOrchestraSettings settings, int? overrideInvokingPlayerIndex)
		{
			if (Active) { try { lock (Lock) Add("PARTICLE", type.ToString(), "-", settings.PositionInWorld, "-", "-", Stack(1)); } catch (Exception) { } }
			orig(clientOnly, type, settings, overrideInvokingPlayerIndex);
		}

		/// <summary>Player 0 is a client-less stand-in: its own Update runs with Main.myPlayer = 255 (as on the plain dedicated server) even while the rest of the update runs as "single player".</summary>
		private static void OnPlayerUpdate(On_Player.orig_Update orig, Player self, int i)
		{
			if (Enabled && Emulate && self.whoAmI == 0 && Main.myPlayer == 0)
			{
				Main.myPlayer = 255;
				try { orig(self, i); } finally { Main.myPlayer = 0; }
			}
			else orig(self, i);
		}

		// ---- commander ModPlayer hook counters ----
		private static readonly Dictionary<string, int> CmdCnt = new();
		internal static void NoteCmdHook(string hook, Projectile p, Player who)
		{
			if (!Active) return;
			// a soldier's stat dummy is a separate Player object whose whoAmI defaults to 0 as well: only the stand-in commander (Main.player[0]) counts as the commander
			hook += ReferenceEquals(who, Main.player[0]) ? "[commander]" : "[dummy/other]";
			lock (Lock)
			{
				bool tagged = p != null && p.TryGetGlobalProjectile(out SoldierProjectileTag tg) && tg.IsSoldierShot;
				string k = hook + "|" + (p == null ? "-" : ProjName(p)) + (tagged ? " (soldier shot)" : " (not a soldier shot)");
				CmdCnt[k] = CmdCnt.TryGetValue(k, out int n) ? n + 1 : 1;
			}
		}

		// =========================================================================================================
		// Gear enumeration for the scan
		// =========================================================================================================
		internal sealed class GearSet { public int[] Types; public string Label; }

		internal static List<GearSet> EnumerateGear()
		{
			var list = new List<GearSet>();
			if (GearScanMods == null) return list;
			var heads = new List<Item>(); var bodies = new List<Item>(); var legs = new List<Item>();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
			{
				Item it = ContentSamples.ItemsByType.TryGetValue(t, out var s) ? s : null;
				if (it == null || it.ModItem == null || !GearScanMods.Contains(it.ModItem.Mod.Name)) continue;
				string full = it.ModItem.Mod.Name + "/" + it.ModItem.Name;
				if (GearFilter != null && !GearFilter.Any(x => full.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0 || (it.Name ?? "").IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
				if (it.vanity) continue;
				if (GearKind == "held" && !it.accessory && it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0 && it.damage > 0 && it.useStyle > 0) list.Add(new GearSet { Types = new[] { t }, Label = "held:" + full });
				if ((GearKind == "acc" || GearKind == "both") && it.accessory && it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0) list.Add(new GearSet { Types = new[] { t }, Label = full });
				if (GearKind == "armor" || GearKind == "both")
				{
					if (it.headSlot >= 0 && !it.accessory) heads.Add(it); else if (it.bodySlot >= 0 && !it.accessory) bodies.Add(it); else if (it.legSlot >= 0 && !it.accessory) legs.Add(it);
				}
			}
			if (GearKind == "armor" || GearKind == "both")
			{
				foreach (Item h in heads)
					foreach (Item b in bodies)
					{
						if (h.ModItem.Mod != b.ModItem.Mod) continue;
						foreach (Item l in legs)
						{
							if (l.ModItem.Mod != h.ModItem.Mod) continue;
							bool set = false;
							try { set = h.ModItem.IsArmorSet(h, b, l); } catch (Exception) { }
							if (set) list.Add(new GearSet { Types = new[] { h.type, b.type, l.type }, Label = h.ModItem.Mod.Name + "/" + h.ModItem.Name + "+" + b.ModItem.Name + "+" + l.ModItem.Name });
						}
					}
			}
			if (list.Count > MaxGear) list = list.Take(MaxGear).ToList();
			return list;
		}

		internal static List<int> ResolveRefs()
		{
			var res = new List<int>();
			foreach (string n in RefNames) { Item it = MakeItem(n); if (it != null && !it.IsAir) res.Add(it.type); }
			return res;
		}
	}

	/// <summary>Reports projectile spawns and hits to the on-hit probe.</summary>
	public class HarnessOnHitProjectile : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source)
		{
			if (!HarnessOnHit.Enabled) return;
			HarnessOnHit.OnProjSpawn(projectile, source);
		}
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!HarnessOnHit.Enabled) return;
			HarnessOnHit.OnProjHit(projectile, target, damageDone);
		}
	}

	public class HarnessOnHitNpc : GlobalNPC
	{
		public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
		{
			if (!HarnessOnHit.Enabled) return;
			HarnessOnHit.OnNpcSpawn(npc, source);
		}
	}

	/// <summary>The commander's own ModPlayer hooks: counts how often vanilla invokes them for a hit (the path every commander accessory / set bonus / held-item proc runs through).</summary>
	public class HarnessOnHitPlayer : ModPlayer
	{
		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (HarnessOnHit.Enabled) HarnessOnHit.NoteCmdHook("OnHitNPCWithProj", proj, Player);
		}
		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (HarnessOnHit.Enabled) HarnessOnHit.NoteCmdHook("ModifyHitNPCWithProj", proj, Player);
		}
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (HarnessOnHit.Enabled) HarnessOnHit.NoteCmdHook("OnHitNPC(item/melee)", null, Player);
		}
	}
}
