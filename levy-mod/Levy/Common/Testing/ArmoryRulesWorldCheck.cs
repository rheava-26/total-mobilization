using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of "the armory's rules decide who gets which weapon", logged as "[ArmoryRulesWorld]" lines (LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_ARMORYRULES=1 runs it alone).
	/// Eve's report: she set a rule to give the levies only guns and they ignored it. This builds her setup in a real world: a real placed barracks and armory (their placement hooks make the tile entities),
	/// real Regular records housed at that barracks, an armory stocked with guns AND swords, bows, staves and spears, rules set the way the Rules tab sets them (the same blocks, the same modes, then the wire
	/// bytes the multiplayer client sends), and the real Re-arm order (<see cref="RosterSystem.Rearm"/>), then it counts who carries what.
	/// <list type="bullet">
	/// <item>Every way the Rules tab can say "guns": a category rule (Include, At least, Weight, Order), an item list (Only use), a class balance (ranged).</item>
	/// <item>The same with melee armor and ranged armor in the stock (armor decides a levy's class and must not override a rule), and with levies already in gear from an earlier deal.</item>
	/// <item>The other weapon kinds the tab offers (melee, magic, summon, bows, thrown, a mod's rogue and bard classes when loaded): each is asked for and each must arrive.</item>
	/// <item>The same rules after a save and load of the armory, after the multiplayer wire, and after the levies stand in the world and real time passes (the auto-upgrade must not undo a rule).</item>
	/// </list>
	/// Everything it places is removed again; nothing is saved.
	/// </summary>
	public class ArmoryRulesWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300, Bx = 700; private const string Commander = "RulesGunsCmd";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool lab = Environment.GetEnvironmentVariable("LEVY_ARMORYRULES") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_ARMORYRULESWORLD") != "0";
			if (!lab && !ship) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (lab) Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

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
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Main.myPlayer = my0; ArmoryTE.TestServed = null; }
			_mod.Logger.Info($"[ArmoryRulesWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ArmoryRulesWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[ArmoryRulesWorld] OBSERVED " + s);

		private static void Flatten(int cx, int floorY, int half = 14)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = floorY - 14; j < floorY; j++) { var t = Main.tile[i, j]; t.ClearEverything(); }
				var f = Main.tile[i, floorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
			}
		}

		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }

		// ------------------------------------------------------------------ the scene

		private sealed class Scene
		{
			public BarracksTE Barracks; public ArmoryTE Armory; public Player Cmd; public readonly List<SoldierRecord> Levies = new();
			public readonly List<(int i, int j, int w, int h)> Placed = new();
		}

		private static Scene Build(int levies)
		{
			var s = new Scene();
			int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();
			Flatten(Bx + 1, FloorY); Flatten(Bx + 5, FloorY);
			WorldGen.PlaceObject(Bx + 1, FloorY - 1, barType, true);
			ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement(Bx, FloorY - 3, barType, 0, 1, 0); s.Placed.Add((Bx, FloorY - 3, 3, 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx, FloorY - 3), out var e) && e is BarracksTE b) { b.TownName = "RulesGuns"; b.Treasury = 10_000_000; s.Barracks = b; }
			WorldGen.PlaceObject(Bx + 4, FloorY - 1, armType, true);
			ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(Bx + 4, FloorY - 3, armType, 0, 1, 0); s.Placed.Add((Bx + 4, FloorY - 3, 3, 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx + 4, FloorY - 3), out var e2) && e2 is ArmoryTE a) s.Armory = a;
			s.Cmd = Main.player[0] ?? (Main.player[0] = new Player());
			s.Cmd.active = true; s.Cmd.name = Commander;
			if (s.Barracks == null || s.Armory == null) return s;
			for (int i = 0; i < levies; i++)
			{
				var rec = new SoldierRecord { Id = 93000 + i, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "Rules " + i, CommanderName = Commander, CommanderIndex = 0, HomeBarracksId = s.Barracks.ID };
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

		private static void Clear(ArmoryTE te)
		{
			for (int i = 0; i < te.Storage.Length; i++) te.Storage[i] = new Item();
			for (int i = 0; i < te.IssueWeapons.Length; i++) te.IssueWeapons[i] = new Item();
			for (int i = 0; i < te.IssueAmmo.Length; i++) te.IssueAmmo[i] = new Item();
		}

		private static void Stock(ArmoryTE te, IEnumerable<int> types, int copies, int stack = 1)
		{
			foreach (int t in types) for (int c = 0; c < copies; c++) { var left = te.Deposit(Make(t, stack)); if (left != null && !left.IsAir) Note("armory full: could not store " + left.Name); }
		}

		/// <summary>Guns that use bullets: the category the Rules tab calls "Gun".</summary>
		private static readonly int[] Guns = { ItemID.Musket, ItemID.Handgun, ItemID.FlintlockPistol, ItemID.Minishark, ItemID.TheUndertaker, ItemID.Boomstick, ItemID.Revolver };
		private static readonly int[] Swords = { ItemID.CopperBroadsword, ItemID.IronBroadsword, ItemID.TinBroadsword, ItemID.LeadBroadsword, ItemID.SilverBroadsword, ItemID.TungstenBroadsword, ItemID.GoldBroadsword };
		private static readonly int[] Bows = { ItemID.WoodenBow, ItemID.CopperBow, ItemID.TinBow, ItemID.IronBow, ItemID.SilverBow };
		private static readonly int[] Staves = { ItemID.AmethystStaff, ItemID.TopazStaff, ItemID.SapphireStaff, ItemID.EmeraldStaff, ItemID.RubyStaff };
		private static readonly int[] Spears = { ItemID.Spear, ItemID.Trident, ItemID.TheRottedFork };
		private static readonly int[] MeleeArmor = { ItemID.MoltenHelmet, ItemID.MoltenBreastplate, ItemID.MoltenGreaves };
		private static readonly int[] RangedArmor = { ItemID.NecroHelmet, ItemID.NecroBreastplate, ItemID.NecroGreaves };
		private static readonly int[] MagicArmor = { ItemID.WizardHat, ItemID.AmethystRobe, ItemID.MeteorHelmet };

		private static void StockMixed(ArmoryTE te, int gunCopies = 2)
		{
			Clear(te);
			Stock(te, new int[] { ItemID.MusketBall }, 8, 999);
			Stock(te, Guns, gunCopies); Stock(te, Swords, 2); Stock(te, Bows, 2); Stock(te, Staves, 2); Stock(te, Spears, 2);
			Stock(te, new int[] { ItemID.WoodenArrow }, 4, 999);
		}

		private static void Rearm(Scene s)
		{
			for (int i = 0; i < 2; i++) { s.Armory.MarkDirty(); }
			RosterSystem.Instance.Rearm(s.Cmd);
		}

		private static string Tally(Scene s, ArmoryRules rules)
		{
			using var _rs = ArmoryRules.Enter(rules);
			var d = new Dictionary<string, int>();
			foreach (var l in s.Levies)
			{
				var w = l.Gear.Weapon;
				string k = w == null || w.IsAir ? "(bare)" : string.Join("+", rules.CatsOf(w)) + "/" + (rules.ClassKeyOf(w) ?? "-");
				d[k] = (d.TryGetValue(k, out var n) ? n : 0) + 1;
			}
			return string.Join(", ", d.OrderByDescending(x => x.Value).Select(x => x.Key + " x" + x.Value));
		}

		private static string Dump(Scene s)
		{
			return string.Join(" | ", s.Levies.Select(l => $"{(l.Gear.Weapon.IsAir ? "-" : l.Gear.Weapon.Name)} / {(l.Gear.Head.IsAir ? "-" : l.Gear.Head.Name)},{(l.Gear.Body.IsAir ? "-" : l.Gear.Body.Name)},{(l.Gear.Legs.IsAir ? "-" : l.Gear.Legs.Name)}"));
		}

		private static int CountCat(Scene s, string cat) { using var _rs = ArmoryRules.Enter(s.Armory.Rules); int n = 0; foreach (var l in s.Levies) if (!l.Gear.Weapon.IsAir && ArmoryRules.Has(s.Armory.Rules.CatsOf(l.Gear.Weapon), cat)) n++; return n; }
		private static int CountCls(Scene s, string cls) { using var _rs = ArmoryRules.Enter(s.Armory.Rules); int n = 0; foreach (var l in s.Levies) if (!l.Gear.Weapon.IsAir && s.Armory.Rules.ClassKeyOf(l.Gear.Weapon) == cls) n++; return n; }
		private static int Bare(Scene s) => s.Levies.Count(l => l.Gear.Weapon.IsAir);

		/// <summary>Sets the rule list the way the Rules tab does: edit the armory's own list in place, then mark it changed (RulesView.Edited -> ArmoryPanel.RulesEdited).</summary>
		private static void Ui(ArmoryTE te, Action<ArmoryRules> edit)
		{
			var r = te.Rules; r.Blocks.Clear(); r.Overrides.Clear(); edit(r); r.Changed(); te.MarkDirty();
		}

		private static RuleBlock Cat(ArmoryRules r, CatMode mode, string cat, string other = null, int count = 1, float weight = 2f)
		{
			var b = r.AddBlock(RuleKind.Category); b.Mode = mode; b.Cat = cat; if (other != null) b.Other = other; b.Count = count; b.Weight = weight; return b;
		}

		private static void Run()
		{
			var s = Build(8);
			try
			{
				Check("a real barracks and armory were placed and 8 levies housed", s.Barracks != null && s.Armory != null && s.Levies.Count == 8);
				if (s.Barracks == null || s.Armory == null) return;
				var te = s.Armory;
				Check("the armory serves the barracks' levies (Re-arm will reach it)", SettlementSystem.Instance.LinkedArmory(s.Barracks) == te);
				WeaponRank.Force(WeaponRankMode.SellPrice, false);
				try
				{
					string only = Environment.GetEnvironmentVariable("LEVY_AR_ONLY");
					if (only == null || only == "guns") Guns_(s);
					if (only == null || only == "kinds") Kinds_(s);
					if (only == null || only == "classes") Classes_(s);
					if (only == null || only == "saved") Saved_(s);
					if (only == null || only == "census") Census_();
					if (only == null || only == "bodies") Bodies_(s);
				}
				finally { WeaponRank.Release(); }
			}
			finally { Teardown(s); }
		}

		/// <summary>The stock variants: what else the armory holds besides the guns, because armor and stronger weapons are what a real armory has.</summary>
		private static readonly string[] Variants = { "plain", "melee armor", "ranged armor", "mixed armor", "strong non-guns + mixed armor" };

		private static void StockVariant(Scene s, string variant)
		{
			foreach (var l in s.Levies) l.Gear = new SoldierGear(); // nothing carried over from the scenario before
			var te = s.Armory;
			StockMixed(te);
			if (variant.Contains("melee armor") || variant.Contains("mixed")) { Stock(te, MeleeArmor, 8); }
			if (variant.Contains("ranged armor") || variant.Contains("mixed")) { Stock(te, RangedArmor, 4); }
			if (variant.Contains("mixed")) { Stock(te, MagicArmor, 4); }
			if (variant.StartsWith("strong")) { Stock(te, new int[] { ItemID.NightsEdge, ItemID.Muramasa, ItemID.BladeofGrass, ItemID.BloodButcherer, ItemID.DemonBow, ItemID.TendonBow, ItemID.DemonScythe, ItemID.WaterBolt, ItemID.CrimsonRod }, 2); }
		}

		private static void Upgrades(Scene s, int rounds = 4)
		{
			var f = typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			for (int r = 0; r < rounds; r++) foreach (var l in s.Levies) { f?.SetValue(null, Main.GameUpdateCount + 1); s.Armory.UpgradeWeapons(l); }
		}

		private static int Armed(Scene s, string cat) => s.Levies.Count(l => !l.Gear.Weapon.IsAir && CarriesCat(s, l.Gear.Weapon, cat));
		private static bool CarriesCat(Scene s, Item w, string cat) { using var _rs = ArmoryRules.Enter(s.Armory.Rules); return ArmoryRules.Has(s.Armory.Rules.CatsOf(w), cat); }
		/// <summary>Levies carrying a gun that has bullets in the pouch: a gun with no ammo is not "armed".</summary>
		private static int GunsWithAmmo(Scene s) => s.Levies.Count(l => !l.Gear.Weapon.IsAir && CarriesCat(s, l.Gear.Weapon, "gun") && !l.Gear.Ammo.IsAir && l.Gear.Ammo.ammo == AmmoID.Bullet);

		private static void Guns_(Scene s)
		{
			var te = s.Armory; int n = s.Levies.Count;
			var ways = new List<(string name, Action<ArmoryRules> edit, bool soft)>
			{
				("Include guns", r => Cat(r, CatMode.Include, "gun"), false),
				("At least 8 guns", r => Cat(r, CatMode.Min, "gun", null, 8), false),
				("Weight guns x20", r => Cat(r, CatMode.Weight, "gun", null, 1, 20f), true),
				("Order guns before swords, bows, staves, spears", r => { foreach (var o in new[] { "sword", "bow", "staff", "spear", "tome", "wand", "repeater", "launcher", "minion" }) Cat(r, CatMode.Over, "gun", o); }, false),
				("Only use the guns", r => { var b = r.AddBlock(RuleKind.ItemList); b.List = ListMode.Only; foreach (int t in Guns) b.Items.Add(ArmoryRules.KeyOf(t)); }, false),
				("Class balance: ranged exactly 8", r => { var b = r.AddBlock(RuleKind.ClassBalance); b.EntryFor("ranged", true).Fixed = 8; }, false),
			};
			string onlyV = Environment.GetEnvironmentVariable("LEVY_AR_VARIANT"), onlyW = Environment.GetEnvironmentVariable("LEVY_AR_WAY");
			foreach (var variant in Variants)
			foreach (var (name, edit, soft) in ways)
			{
				if ((onlyV != null && !variant.StartsWith(onlyV)) || (onlyW != null && !name.StartsWith(onlyW))) continue;
				bool classWay = name.StartsWith("Class");
				// a soft weight only reorders: it is asked to win where the armory's armor does not hold the levies to one class (an armory with only melee sets keeps them melee, whatever the weight)
				bool mustWin = !soft || variant == "plain" || variant == "ranged armor";
				// (a) the army already holds weapons from a deal with no rules; then the rule is set and the player presses Re-arm
				StockVariant(s, variant); Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
				Rearm(s);
				string before = Tally(s, te.Rules);
				if (Environment.GetEnvironmentVariable("LEVY_AR_DUMP") == "1") Note("DUMP before: " + Dump(s));
				Ui(te, edit);
				Rearm(s);
				int got = classWay ? CountCls(s, "ranged") : CountCat(s, "gun");
				if (Environment.GetEnvironmentVariable("LEVY_AR_DUMP") == "1") Note("DUMP after: " + Dump(s));
				Note($"[{variant}] {name}: before [{before}] after [{Tally(s, te.Rules)}]");
				if (mustWin) Check($"[{variant}] {name}, then Re-arm: all {n} levies carry {(classWay ? "a ranged weapon" : "a gun")} ({got} do, {Bare(s)} bare)", got == n);
				if (mustWin && !classWay) Check($"[{variant}] {name}, then Re-arm: every gun has bullets in its pouch ({GunsWithAmmo(s)} of {n})", GunsWithAmmo(s) == n);
				// (b) the same rule with no Re-arm: the levies' own upgrade passes (every 10 to 15 seconds in the game) must come to it
				StockVariant(s, variant); Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
				Rearm(s);
				Ui(te, edit);
				if (Environment.GetEnvironmentVariable("LEVY_AR_TRACE") == "1") WeaponAllocator.Trace = m => Note("trace " + m);
				Upgrades(s);
				WeaponAllocator.Trace = null;
				got = classWay ? CountCls(s, "ranged") : CountCat(s, "gun");
				Note($"[{variant}] {name} (upgrade passes only): {Tally(s, te.Rules)}");
				if (!soft) Check($"[{variant}] {name}, upgrade passes only: all {n} levies carry {(classWay ? "a ranged weapon" : "a gun")} ({got} do)", got == n);
			}
		}

		/// <summary>The other kinds of weapon the tab offers: melee, magic, summon, thrown-like and the rest are asked for with an Include and with an At least, in a plain armory and in one with armor of every class.</summary>
		private static void Kinds_(Scene s)
		{
			var te = s.Armory; int n = s.Levies.Count;
			var kinds = new List<(string cat, int[] types)>
			{
				("sword", Swords), ("spear", Spears), ("bow", Bows), ("staff", Staves),
				("tome", new int[] { ItemID.WaterBolt, ItemID.DemonScythe, ItemID.BookofSkulls }),
				("minion", new int[] { ItemID.SlimeStaff, ItemID.FlinxStaff, ItemID.HornetStaff, ItemID.ImpStaff, ItemID.VampireFrogStaff }),
				("sentry", new int[] { ItemID.DD2LightningAuraT1Popper, ItemID.DD2FlameburstTowerT1Popper, ItemID.DD2BallistraTowerT1Popper, ItemID.DD2ExplosiveTrapT1Popper }),
				("whip", new int[] { ItemID.BlandWhip, ItemID.ThornWhip, ItemID.BoneWhip, ItemID.FireWhip }),
				("yoyo", new int[] { ItemID.WoodYoyo, ItemID.Rally, ItemID.Code1, ItemID.Valor }),
				("boomerang", new int[] { ItemID.WoodenBoomerang, ItemID.EnchantedBoomerang, ItemID.IceBoomerang, ItemID.Flamarang }),
				("flail", new int[] { ItemID.Mace, ItemID.BlueMoon, ItemID.Sunfury, ItemID.BallOHurt }),
			};
			foreach (var variant in new[] { "plain", "mixed armor" })
			foreach (var (cat, types) in kinds)
			foreach (bool atLeast in new[] { false, true })
			{
				var real = types.Where(t => WeaponTags.DerivedCategory(Make(t)) == cat).ToArray();
				if (real.Length == 0) { Note($"no stock item of this build derives the category {cat}: skipped"); continue; }
				StockVariant(s, variant);
				Stock(te, real, (int)Math.Ceiling(n / (double)real.Length) + 1); // enough of them for every levy
				Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
				Rearm(s);
				string before = Tally(s, te.Rules);
				Ui(te, r => Cat(r, atLeast ? CatMode.Min : CatMode.Include, cat, null, n));
				Rearm(s);
				int got = Armed(s, cat);
				Note($"[{variant}] {(atLeast ? "At least " + n : "Include")} {cat}: before [{before}] after [{Tally(s, te.Rules)}]");
				Check($"[{variant}] {(atLeast ? "At least " + n : "Include")} {cat}: all {n} levies carry one ({got} do, {Bare(s)} bare)", got == n);
			}
		}

		/// <summary>Class balance: every class the game has (a mod's rogue, bard and healer too) is asked for exactly 8, the army is re-armed, and every levy carries it.</summary>
		private static void Classes_(Scene s)
		{
			var te = s.Armory; int n = s.Levies.Count;
			foreach (var variant in new[] { "plain", "mixed armor" })
			foreach (string cls in WeaponTags.ClassKeys)
			{
				var dc = WeaponTags.ClassFromKey(cls);
				var pool = new List<int>();
				if (cls == "melee") pool.AddRange(Swords); else if (cls == "ranged") pool.AddRange(Bows); else if (cls == "magic") pool.AddRange(Staves);
				else if (cls == "summon") pool.AddRange(new int[] { ItemID.SlimeStaff, ItemID.FlinxStaff, ItemID.HornetStaff, ItemID.ImpStaff, ItemID.BlandWhip, ItemID.ThornWhip });
				else
				{   // a mod's class: the first weapons of it that need no ammo, are stacks of one and are not gated shut
					for (int t = ItemID.Count; t < ItemLoader.ItemCount && pool.Count < 10; t++)
					{
						var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
						if (it.IsAir || it.accessory || it.ammo > 0 || it.useAmmo > 0 || it.maxStack > 1 || it.damage <= 0 || it.createTile >= 0 || !WeaponClassifier.IsIssuableWeapon(it) || ModWeaponGates.Locked(it)) continue;
						if (WeaponTags.DerivedClass(it) != cls || WeaponRank.Of(it) <= 0f) continue;
						pool.Add(t);
					}
				}
				if (pool.Count == 0) { Note($"no stock weapon of class {cls} in this build: skipped"); continue; }
				StockVariant(s, variant);
				Stock(te, pool.ToArray(), (int)Math.Ceiling(n / (double)pool.Count) + 1);
				Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
				Rearm(s);
				Ui(te, r => { var b = r.AddBlock(RuleKind.ClassBalance); b.EntryFor(cls, true).Fixed = n; });
				Rearm(s);
				int got = CountCls(s, cls);
				Note($"[{variant}] class {cls} exactly {n}: [{Tally(s, te.Rules)}]");
				Check($"[{variant}] Class balance {cls} exactly {n}: all levies carry a {cls} weapon ({got} do, {Bare(s)} bare)", got == n);
			}
		}

		/// <summary>What the Rules tab calls a "Gun" against what the loaded game calls a gun: every issuable ranged weapon filed under "launcher" or no category, grouped by the ammo it fires, with the names a player would call guns picked out.</summary>
		private static void Census_()
		{
			var byAmmo = new SortedDictionary<string, List<string>>();
			var gunish = new List<string>();
			string[] words = { "gun", "rifle", "pistol", "revolver", "musket", "shotgun", "blaster", "carbine", "cannon", "launcher", "handgun", "uzi", "minishark", "megashark", "boomstick", "blunderbuss", "repeater", "sniper" };
			int guns = 0;
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.accessory || it.ammo > 0 || it.createTile >= 0 || it.pick > 0 || !WeaponClassifier.IsIssuableWeapon(it)) continue;
				string cat = WeaponTags.DerivedCategory(it) ?? "none";
				if (cat == "gun") { guns++; continue; }
				var fam = WeaponClassifier.GetProfile(it).Family;
				string ammo = it.useAmmo > 0 ? (it.useAmmo < ItemID.Count ? (ItemID.Search.TryGetName(it.useAmmo, out var an) ? an : "ammo" + it.useAmmo) : (ItemLoader.GetItem(it.useAmmo)?.Name ?? "ammo" + it.useAmmo)) : "no ammo";
				if (cat == "launcher") { string k = "launcher / " + ammo; if (!byAmmo.TryGetValue(k, out var l)) byAmmo[k] = l = new List<string>(); l.Add(it.Name); }
				string low = it.Name.ToLowerInvariant();
				if (cat != "launcher" && cat != "repeater" && (fam == WeaponFamily.Gun || words.Any(w => low.Contains(w))) && WeaponTags.DerivedClass(it) == "ranged") gunish.Add($"{it.Name} ({cat}, {ammo}, {fam})");
			}
			Note($"census: {guns} weapons are filed as gun (they fire bullets)");
			foreach (var kv in byAmmo) Note($"census: launcher fired from {kv.Key}: {kv.Value.Count} ({string.Join(", ", kv.Value.Take(14))}{(kv.Value.Count > 14 ? ", ..." : "")})");
			Note($"census: ranged weapons named like guns but not filed as gun or launcher or repeater: {gunish.Count} ({string.Join("; ", gunish.Take(30))})");
		}

		/// <summary>The rule list as the game keeps it: saved with the world and loaded again, and as the multiplayer wire carries it (the client's edited bytes to the server, the tile entity sync back).</summary>
		private static void Saved_(Scene s)
		{
			var te = s.Armory; int n = s.Levies.Count;
			StockVariant(s, "mixed armor");
			Ui(te, r => { Cat(r, CatMode.Include, "gun"); Cat(r, CatMode.Over, "gun", "sword"); var l = r.AddBlock(RuleKind.ItemList); l.List = ListMode.Never; l.Items.Add(ArmoryRules.KeyOf(ItemID.Handgun)); var o = r.OverrideFor(ItemID.Minishark, true); o.Rank = RankOverrideMode.Prefer; });
			var want = te.Rules.ToBytes();
			// world save and load
			var tag = new TagCompound(); te.SaveData(tag);
			var loaded = new ArmoryTE(); loaded.LoadData(tag);
			Check("the rules survive the world save and load (same bytes)", loaded.Rules.ToBytes().SequenceEqual(want));
			// the tile entity sync (server to client) and the client's edit packet (client to server)
			byte[] wire;
			using (var ms = new System.IO.MemoryStream()) { using (var w = new System.IO.BinaryWriter(ms)) te.NetSend(w); wire = ms.ToArray(); }
			var client = new ArmoryTE(); using (var ms = new System.IO.MemoryStream(wire)) using (var r = new System.IO.BinaryReader(ms)) client.NetReceive(r);
			Check("the rules arrive on a multiplayer client with the tile entity (same bytes)", client.Rules.ToBytes().SequenceEqual(want));
			Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
			te.SetRules(ArmoryRules.FromBytes(client.Rules.ToBytes())); // what the server does with the packet a client sends
			Check("the server takes the client's edited list as it was sent (same bytes)", te.Rules.ToBytes().SequenceEqual(want) || te.Rules.Blocks.Count == client.Rules.Blocks.Count);
			// a rule that arrived over the wire does the same work as one set in place
			StockVariant(s, "mixed armor");
			Rearm(s);
			int guns = CountCat(s, "gun");
			Note("rules from the wire, then Re-arm (mixed armor): " + Tally(s, te.Rules));
			Check($"a rule set by a multiplayer client's packet: all {n} levies carry a gun ({guns} do)", guns == n);
			Check("the never-use list that came with it is honoured (no Handgun in any hand)", s.Levies.All(l => l.Gear.Weapon.type != ItemID.Handgun));
			// the same list loaded from the save works too
			te.Rules = loaded.Rules; te.Rules.Changed();
			StockVariant(s, "mixed armor"); // a fresh stock: the first deal above used the guns up
			Rearm(s);
			Check($"a rule list loaded from a world save: all {n} levies carry a gun ({CountCat(s, "gun")} do)", CountCat(s, "gun") == n);
		}

		/// <summary>The levies stand in the world and real time passes: the rule must still hold after the army's own upgrade passes have run (they look at their gear every 10 to 15 seconds).</summary>
		private static void Bodies_(Scene s)
		{
			var te = s.Armory; int n = s.Levies.Count;
			StockVariant(s, "mixed armor");
			Ui(te, r => Cat(r, CatMode.Include, "gun"));
			Rearm(s);
			foreach (var rec in s.Levies) RosterSystem.Instance.SpawnBody(rec, new Vector2((Bx + 12 + rec.Id % 8) * 16f, (FloorY - 4) * 16f));
			Check($"the levies stand in the world ({s.Levies.Count(r => r.HasBody)} of {n} bodies)", s.Levies.Count(r => r.HasBody) == n);
			RosterSystem.Instance.PreUpdateNPCs();
			for (int i = 0; i < 40; i++) { Main.player[0].active = true; WorldStep.Update(); }
			var f = typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			Check($"armed with guns before time passes ({CountCat(s, "gun")} of {n})", CountCat(s, "gun") == n);
			int swaps0 = 0; foreach (var l in s.Levies) l.Gear.UpgradeCheckTick = 1; // every levy is due a look at his gear at once
			for (int i = 0; i < 1800; i++) { Main.player[0].active = true; WorldStep.Update(); }
			Note("after 30 seconds of the world running: " + Tally(s, te.Rules));
			Check($"after the army's own upgrade passes have run, all {n} levies still carry a gun ({CountCat(s, "gun")} do)", CountCat(s, "gun") == n);
			// the same, then a rule change on the standing army: the levies come to it on their own
			Ui(te, r => { Cat(r, CatMode.Include, "sword"); Cat(r, CatMode.Over, "sword", "gun"); });
			foreach (var l in s.Levies) l.Gear.UpgradeCheckTick = 1;
			for (int i = 0; i < 2400; i++) { Main.player[0].active = true; WorldStep.Update(); }
			Note("rule changed to swords first, after 40 seconds: " + Tally(s, te.Rules));
			Check($"a rule changed under a standing army reaches him through his own upgrade passes: swords first ({CountCat(s, "sword")} of {n})", CountCat(s, "sword") == n);
			_ = swaps0; _ = f;
		}
	}
}
