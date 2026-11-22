using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of "a levy's loadout is ONE class", logged as "[LoadoutFit]" lines (LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_LOADOUTFIT=1 runs it alone).
	/// Eve's report (3.1.10/3.1.11, Calamity, post Yharon, screenshot 84): one levy carried a ranged primary, a melee sidearm, ranged armor, a rogue accessory (Eclipse Mirror) and a summoner accessory. Her rules: at least 8 and at most 20
	/// of each class, every class a share of 2. A levy's class is his primary's; the sidearm (a minion staff or sentry is the one summon item any levy may carry, by design), the armor and every class accessory follow it, and a
	/// generic accessory is anyone's.
	/// <list type="bullet">
	/// <item>Calamity: her recorded army (every kit as her recordings show it, the rest of her stock in the armory) converges in ONE upgrade pass and stays put; a fresh Re-arm of 45 levies from her stock with her rules is coherent and the class counts keep the rules.</item>
	/// <item>Vanilla: the same with post-Moon-Lord stock and class accessories (emblems, Mana Flower, Magic Quiver, Papyrus Scarab, Fire Gauntlet) beside generic ones.</item>
	/// <item>Nothing is duplicated or lost (every item counted before and after), and a strong class accessory reaches a levy of its class before anyone else.</item>
	/// </list>
	/// Everything it places is removed again; nothing is saved.
	/// </summary>
	public partial class LoadoutFitWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300, Bx = 900; private const string Commander = "LoadoutFitCmd";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool lab = Environment.GetEnvironmentVariable("LEVY_LOADOUTFIT") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_LOADOUTFIT") != "0";
			if (!lab && !ship) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (lab) Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }

		private static void Drive()
		{
			if (!_armed || _done || !Main.dedServ) return;
			if (++_calls < 6) return;
			RunIfArmed();
			if (_done && Environment.GetEnvironmentVariable("LEVY_MECHWORLD_EXIT") == "1") Environment.Exit(0);
		}

		/// <summary>Runs now when this boot armed it (the summon world check chain calls this, so checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int my0 = Main.myPlayer;
			var cfg = Config.LevyConfig.Get; bool cArmor = cfg.ClassMatchArmor, cSide = cfg.ClassMatchSidearms;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				_running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Main.myPlayer = my0; ArmoryTE.TestServed = null; WeaponRank.ForceProgress = -1f;
				cfg.ClassMatchArmor = cArmor; cfg.ClassMatchSidearms = cSide;
			}
			_mod.Logger.Info($"[LoadoutFit] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[LoadoutFit] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[LoadoutFit] OBSERVED " + s);

		// ------------------------------------------------------------------ the scene (a real barracks and armory, real Regulars; the same as ArmorClassWorldCheck)

		private sealed class Scene
		{
			public BarracksTE Barracks; public ArmoryTE Armory; public Player Cmd; public readonly List<SoldierRecord> Levies = new();
			public readonly List<(int i, int j, int w, int h)> Placed = new();
		}

		private static void Flatten(int cx, int floorY, int half = 14)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = floorY - 14; j < floorY; j++) { var t = Main.tile[i, j]; t.ClearEverything(); }
				var f = Main.tile[i, floorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
			}
		}

		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }
		private static bool IsAir(Item it) => it == null || it.IsAir;
		private static string Nm(Item it) => IsAir(it) ? "-" : it.Name;

		private static Scene Build(int levies)
		{
			var s = new Scene();
			int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();
			Flatten(Bx + 1, FloorY); Flatten(Bx + 5, FloorY);
			WorldGen.PlaceObject(Bx + 1, FloorY - 1, barType, true);
			ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement(Bx, FloorY - 3, barType, 0, 1, 0); s.Placed.Add((Bx, FloorY - 3, 3, 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx, FloorY - 3), out var e) && e is BarracksTE b) { b.TownName = "LoadoutFit"; b.Treasury = 10_000_000; s.Barracks = b; }
			WorldGen.PlaceObject(Bx + 4, FloorY - 1, armType, true);
			ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(Bx + 4, FloorY - 3, armType, 0, 1, 0); s.Placed.Add((Bx + 4, FloorY - 3, 3, 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx + 4, FloorY - 3), out var e2) && e2 is ArmoryTE a) s.Armory = a;
			s.Cmd = Main.player[0] ?? (Main.player[0] = new Player());
			s.Cmd.active = true; s.Cmd.name = Commander;
			if (s.Barracks == null || s.Armory == null) return s;
			for (int i = 0; i < levies; i++)
			{
				var rec = new SoldierRecord { Id = 96000 + i, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "LoadoutFit " + i, CommanderName = Commander, CommanderIndex = 0, HomeBarracksId = s.Barracks.ID, ManaCrystals = 10 };
				rec.CurrentHp = rec.MaxHp; RosterSystem.Instance.Regulars.Add(rec); s.Levies.Add(rec);
			}
			return s;
		}

		private static void Teardown(Scene s)
		{
			foreach (var r in s.Levies) { try { if (r.HasBody) Main.npc[r.BodyIndex].active = false; RosterSystem.Instance.Regulars.Remove(r); } catch { } }
			foreach (var (i, j, w, h) in s.Placed) for (int x = i; x < i + w; x++) for (int y = j; y < j + h; y++) if (WorldGen.InWorld(x, y)) Main.tile[x, y].ClearEverything();
			foreach (var id in new List<int>(TileEntity.ByID.Keys)) { var te = TileEntity.ByID[id]; if (te is BarracksTE || te is ArmoryTE) { TileEntity.ByID.Remove(id); TileEntity.ByPosition.Remove(te.Position); } }
		}

		private static void Tick() => typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, Main.GameUpdateCount + 1);

		/// <summary>Empties the armory and every levy; the first <paramref name="levies"/> levies are in the army, the rest are not.</summary>
		private static void Reset(Scene s, int levies)
		{
			Tick();
			var te = s.Armory;
			for (int i = 0; i < te.Storage.Length; i++) te.Storage[i] = new Item();
			for (int i = 0; i < te.IssueWeapons.Length; i++) te.IssueWeapons[i] = new Item();
			for (int i = 0; i < te.IssueAmmo.Length; i++) te.IssueAmmo[i] = new Item();
			for (int i = 0; i < te.IssueArmor.Length; i++) te.IssueArmor[i] = new Item();
			var regs = RosterSystem.Instance.Regulars;
			for (int i = 0; i < s.Levies.Count; i++)
			{
				var l = s.Levies[i]; l.Gear = new SoldierGear();
				bool on = i < levies, has = regs.Contains(l);
				if (on && !has) regs.Add(l); else if (!on && has) regs.Remove(l);
			}
			var r = te.Rules; r.Blocks.Clear(); r.Overrides.Clear(); r.Changed(); te.MarkDirty();
		}

		private static void Stock(ArmoryTE te, int copies, params int[] types)
		{
			foreach (int t in types) { if (t <= 0) continue; for (int c = 0; c < copies; c++) { var left = te.Deposit(Make(t)); if (!IsAir(left)) Note("armory full: could not store " + left.Name); } }
		}

		/// <summary>Eve's class balance: every class at least <paramref name="min"/>, at most <paramref name="max"/>, a share of <paramref name="share"/>.</summary>
		private static void Balance(ArmoryTE te, int min, int max, float share, params string[] classes)
		{
			var r = te.Rules; r.Blocks.Clear(); r.Overrides.Clear();
			var b = r.AddBlock(RuleKind.ClassBalance);
			foreach (var c in classes) { var e = b.EntryFor(c, true); e.Min = min; e.Max = max; e.Weight = share; }
			r.Changed(); te.MarkDirty();
		}

		private static void Rearm(Scene s) { s.Armory.MarkDirty(); Tick(); RosterSystem.Instance.Rearm(s.Cmd); }

		private static void Upgrades(Scene s, int rounds)
		{
			for (int r = 0; r < rounds; r++) foreach (var l in Active(s)) { Tick(); s.Armory.UpgradeWeapons(l); }
		}

		private static IEnumerable<SoldierRecord> Active(Scene s) => s.Levies.Where(l => RosterSystem.Instance.Regulars.Contains(l));

		// ------------------------------------------------------------------ reading a kit

		private static DamageClass WClass(Scene s, SoldierRecord l) { using var _rs = ArmoryRules.Enter(s.Armory.Rules); return WeaponClassifier.ItemDamageClass(l.Gear.Weapon); }
		private static DamageClass IClass(Scene s, Item it) { using var _rs = ArmoryRules.Enter(s.Armory.Rules); return WeaponClassifier.ItemDamageClass(it); }
		private static DamageClass AClass(SoldierRecord l) => ArmorDoctrine.PreferredClass(l.Gear.Head, l.Gear.Body, l.Gear.Legs);
		private static string Key(DamageClass dc) => dc == null ? "-" : WeaponTags.ClassKey(dc) ?? dc.Name;

		/// <summary>The classes an accessory is for ("gen" when it is for none: generic).</summary>
		private static string AccKey(Item acc)
		{
			var cl = ArmorDoctrine.AccessoryClasses(acc);
			return cl.Count == 0 ? "gen" : string.Join("+", cl.Select(Key));
		}

		/// <summary>True when the accessory is made for other classes and helps <paramref name="cls"/> by nothing (Eclipse Mirror on a ranger). A generic one never is, and neither is one that also helps his class (Sun Stone's all-class damage).</summary>
		private static bool AccOff(Item acc, DamageClass cls) => !IsAir(acc) && cls != null && ArmorDoctrine.AccessoryMismatch(acc, cls);

		/// <summary>The sidearms that are not of his class: a minion staff or a sentry is the one summon item any levy may carry (by design), anything else must be his primary's class.</summary>
		private static List<Item> SideOff(Scene s, SoldierRecord l, DamageClass cls)
		{
			var res = new List<Item>();
			foreach (var it in l.Gear.Sidearms()) if (!IsAir(it) && !SidearmPolicy.IsDeployer(it) && IClass(s, it) != cls) res.Add(it);
			return res;
		}

		private static string SideKey(Scene s, Item it) => IsAir(it) ? "-" : SidearmPolicy.IsDeployer(it) ? "dep" : Key(IClass(s, it));

		private static bool ArmorOff(SoldierRecord l, DamageClass cls) { var a = AClass(l); return a != null && a != cls; }

		/// <summary>What is off-class in his kit ("" when coherent): S sidearm, A armor, C a class accessory of another class.</summary>
		private static string Off(Scene s, SoldierRecord l)
		{
			var cls = WClass(s, l);
			if (cls == null) return "";
			string o = "";
			if (SideOff(s, l, cls).Count > 0) o += "S";
			if (ArmorOff(l, cls)) o += "A";
			int n = Math.Min(l.AccessorySlots, l.Gear.Accessories.Length);
			for (int i = 0; i < n; i++) if (AccOff(l.Gear.Accessories[i], cls)) { o += "C"; break; }
			return o;
		}

		private static string Row(Scene s, SoldierRecord l, string label = null)
		{
			var g = l.Gear; var cls = WClass(s, l);
			int n = Math.Min(l.AccessorySlots, g.Accessories.Length);
			var acc = new List<string>();
			for (int i = 0; i < n; i++) if (!IsAir(g.Accessories[i])) acc.Add($"{g.Accessories[i].Name}[{AccKey(g.Accessories[i])}]");
			var side = g.Sidearms().Where(x => !IsAir(x)).Select(x => $"{x.Name}[{SideKey(s, x)}]");
			string off = Off(s, l);
			return $"{label ?? l.Id.ToString()} | {Key(cls)}: {Nm(g.Weapon)} | side {string.Join(", ", side)} | armor [{Key(AClass(l))}] {Nm(g.Head)} | acc {string.Join(", ", acc)} | {(off == "" ? "ok" : "OFF:" + off)}";
		}

		private static void Table(Scene s, string title)
		{
			Note($"---- {title}: {Active(s).Count()} levies, {Active(s).Count(l => Off(s, l) != "")} incoherent (S sidearm, A armor, C class accessory of another class)");
			foreach (var l in Active(s)) Note("  " + Row(s, l));
		}

		private static int Incoherent(Scene s) => Active(s).Count(l => Off(s, l) != "");

		/// <summary>
		/// Armor that could be fixed: a levy wears armor reading as class X while a levy of class X wears armor that does not (a trade makes one more coherent kit). A class with more levies than sets of its armor
		/// (her stock: 5 summon sets for 9 summoners) leaves the rest in another class's armor, which no trade can fix.
		/// </summary>
		private static int MisplacedArmor(Scene s, out string ex)
		{
			int n = 0; ex = "";
			foreach (var l in Active(s))
			{
				var cls = WClass(s, l); var a = AClass(l);
				if (cls == null || a == null || a == cls) continue;
				var z = Active(s).FirstOrDefault(o => !ReferenceEquals(o, l) && WClass(s, o) == a && AClass(o) != a);
				if (z == null) continue;
				n++;
				if (ex.Length < 300) ex += $" {Key(cls)} levy {l.Id} in {Key(a)} armor ({Nm(l.Gear.Head)}) while {Key(a)} levy {z.Id} wears {Key(AClass(z))} ({Nm(z.Gear.Head)});";
			}
			return n;
		}

		/// <summary>Levies whose kit is off only in ways nothing in the army or the armory could fix (armor of another class with no trade to make): reported, not failed.</summary>
		private static int Fixable(Scene s, out string list)
		{
			var bad = new List<SoldierRecord>();
			foreach (var l in Active(s))
			{
				var cls = WClass(s, l); if (cls == null) continue;
				string off = Off(s, l);
				if (off == "") continue;
				bool armorFix = off.Contains('A') && Active(s).Any(o => !ReferenceEquals(o, l) && WClass(s, o) == AClass(l) && AClass(o) != AClass(l));
				if (off.Contains('S') || off.Contains('C') || armorFix) bad.Add(l);
			}
			list = string.Join("; ", bad.Take(6).Select(l => Row(s, l)));
			return bad.Count;
		}

		private static string Unfixable(Scene s) => string.Join(", ", Active(s).Where(l => Off(s, l) == "A").Select(l => $"{Key(WClass(s, l))} levy {l.Id} in {Key(AClass(l))} {Nm(l.Gear.Head)}"));
		private static string IncoherentList(Scene s) => string.Join("; ", Active(s).Where(l => Off(s, l) != "").Take(6).Select(l => Row(s, l)));

		/// <summary>Every item by type: the armory's storage and rows plus everything every levy carries.</summary>
		private static Dictionary<int, int> Census(Scene s)
		{
			var d = new Dictionary<int, int>();
			void Add(Item it) { if (IsAir(it)) return; d[it.type] = (d.TryGetValue(it.type, out int n) ? n : 0) + it.stack; }
			foreach (var arr in new[] { s.Armory.Storage, s.Armory.IssueWeapons, s.Armory.IssueArmor, s.Armory.IssueAmmo, s.Armory.IssuePotions }) foreach (var it in arr) Add(it);
			foreach (var l in s.Levies) foreach (var it in l.Gear.AllItems()) Add(it);
			return d;
		}

		private static bool SameCensus(Dictionary<int, int> a, Dictionary<int, int> b, out string diff)
		{
			var keys = a.Keys.Union(b.Keys).Where(k => (a.TryGetValue(k, out int x) ? x : 0) != (b.TryGetValue(k, out int y) ? y : 0)).ToList();
			// ammo is drawn and topped up by the pouch (a stack moves between the armory and the pouch, and a fresh pouch is split from a stack): only non-ammo items must match exactly
			keys.RemoveAll(k => Make(k).ammo > 0 && Make(k).maxStack > 1);
			diff = string.Join(", ", keys.Take(8).Select(k => Make(k).Name + " " + (a.TryGetValue(k, out int x) ? x : 0) + "->" + (b.TryGetValue(k, out int y) ? y : 0)));
			return keys.Count == 0;
		}

		private static string Snapshot(Scene s) => string.Join("|", Active(s).Select(l => string.Join(",", l.Gear.AllItems().Where(x => !IsAir(x) && !(x.ammo > 0 && x.maxStack > 1)).Select(x => x.type))));

		private static Dictionary<string, int> ClassCounts(Scene s)
		{
			var d = new Dictionary<string, int>();
			foreach (var l in Active(s)) { string k = Key(WClass(s, l)); d[k] = (d.TryGetValue(k, out int n) ? n : 0) + 1; }
			return d;
		}

		private static string Counts(Dictionary<string, int> d) => string.Join(" ", d.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));

		/// <summary>The class accessories a levy of their class could still use: each worn by someone of another class while a levy of its class has a slot with a generic accessory or one of no class he can use (so the swap would be free).</summary>
		private static int MisplacedClassAccessories(Scene s, out string ex)
		{
			int n = 0; ex = "";
			foreach (var l in Active(s))
			{
				var cls = WClass(s, l);
				int slots = Math.Min(l.AccessorySlots, l.Gear.Accessories.Length);
				for (int i = 0; i < slots; i++)
				{
					var a = l.Gear.Accessories[i];
					if (!AccOff(a, cls)) continue;
					var forCls = ArmorDoctrine.AccessoryClasses(a);
					var taker = Active(s).FirstOrDefault(z => !ReferenceEquals(z, l) && forCls.Contains(WClass(s, z)) && !z.Gear.Accessories.Any(x => !IsAir(x) && x.type == a.type)
						&& Enumerable.Range(0, Math.Min(z.AccessorySlots, z.Gear.Accessories.Length)).Any(j => IsAir(z.Gear.Accessories[j]) || ArmorDoctrine.AccessoryClasses(z.Gear.Accessories[j]).Count == 0 || AccOff(z.Gear.Accessories[j], WClass(s, z))));
					if (taker == null) continue;
					n++;
					if (ex.Length < 300) ex += $" {a.Name} on {Key(cls)} levy {l.Id} while {Key(WClass(s, taker))} levy {taker.Id} could wear it;";
				}
			}
			return n;
		}

		private static void Run()
		{
			var s = Build(48);
			try
			{
				Check("a real barracks and armory were placed and 48 levies housed", s.Barracks != null && s.Armory != null && s.Levies.Count == 48);
				if (s.Barracks == null || s.Armory == null) return;
				Check("the armory serves the barracks' levies (Re-arm will reach it)", SettlementSystem.Instance.LinkedArmory(s.Barracks) == s.Armory);
				Config.LevyConfig.Get.ClassMatchArmor = true; Config.LevyConfig.Get.ClassMatchSidearms = true;
				string only = Environment.GetEnvironmentVariable("LEVY_LOADOUTFIT_ONLY");
				if (only == null || only == "vanilla") Vanilla(s);
				if ((only == null || only == "calamity") && ModLoader.HasMod("CalamityMod")) Calamity(s);
				else if (only == null) Note("Calamity is not loaded: Eve's post-Yharon half is skipped");
			}
			finally { Teardown(s); }
		}
	}
}
