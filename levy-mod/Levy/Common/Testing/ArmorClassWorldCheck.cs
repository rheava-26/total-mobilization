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
using Levy.Content.NPCs;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of "a levy's armor matches his class", logged as "[ArmorClassWorld]" lines (LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_ARMORCLASSWORLD=1 runs it alone).
	/// Eve's report (3.1.8, Calamity, post Devourer of Gods): summoners and mages wear God Slayer (a melee / ranged / rogue set) while Silva Horned Hood and Silva Masked Cap sets sit unused in the Armory.
	/// This places a real barracks and armory, houses real Regulars, sets a class balance the way the Rules tab does, stocks the armory and runs the real Re-arm and the levies' own upgrade pass.
	/// <list type="bullet">
	/// <item>Vanilla: Stardust for summoners over Solar Flare, Nebula for mages, Spectre Mask (not the Hood, not Beetle or Shroomite) for mages after Golem, Tiki for a summoner.</item>
	/// <item>Calamity: Silva Horned Hood set for every summoner, Silva Masked Cap set for every mage, God Slayer only on melee / ranged / rogue, with the class helm of each.</item>
	/// <item>An army already in the wrong armor swaps on its next upgrade pass; too few Silva bodies for every hood (the hoods share one body and legs): every summoner and mage still wears his class helm.</item>
	/// <item>Nothing is duplicated or lost (every armor piece is counted before and after), nothing thrashes on further passes, the tier tolerance refuses far older class armor, a hand-assigned weapon leads the armor,
	/// and the setting off brings back the strongest-set pick (the old behaviour, which reproduces the report).</item>
	/// </list>
	/// Everything it places is removed again; nothing is saved.
	/// </summary>
	public class ArmorClassWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300, Bx = 840; private const string Commander = "ArmorClassCmd";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool lab = Environment.GetEnvironmentVariable("LEVY_ARMORCLASSWORLD") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_ARMORCLASSWORLD") != "0";
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
			bool cfg = Config.LevyConfig.Get.ClassMatchArmor;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Main.myPlayer = my0; ArmoryTE.TestServed = null; WeaponRank.ForceProgress = -1f; Config.LevyConfig.Get.ClassMatchArmor = cfg; }
			_mod.Logger.Info($"[ArmorClassWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ArmorClassWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[ArmorClassWorld] OBSERVED " + s);

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
		private static int Cal(string name) => ModContent.TryFind<ModItem>("CalamityMod", name, out var m) ? m.Type : 0;

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
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx, FloorY - 3), out var e) && e is BarracksTE b) { b.TownName = "ArmorClass"; b.Treasury = 10_000_000; s.Barracks = b; }
			WorldGen.PlaceObject(Bx + 4, FloorY - 1, armType, true);
			ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(Bx + 4, FloorY - 3, armType, 0, 1, 0); s.Placed.Add((Bx + 4, FloorY - 3, 3, 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx + 4, FloorY - 3), out var e2) && e2 is ArmoryTE a) s.Armory = a;
			s.Cmd = Main.player[0] ?? (Main.player[0] = new Player());
			s.Cmd.active = true; s.Cmd.name = Commander;
			if (s.Barracks == null || s.Armory == null) return s;
			for (int i = 0; i < levies; i++)
			{
				var rec = new SoldierRecord { Id = 95000 + i, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "ArmorClass " + i, CommanderName = Commander, CommanderIndex = 0, HomeBarracksId = s.Barracks.ID, ManaCrystals = 10 };
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

		/// <summary>The next game tick, so the armory's per-tick caches (the soldiers it serves) see the army this scenario set up.</summary>
		private static void Tick() => typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, Main.GameUpdateCount + 1);

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
				if (on && !has) regs.Add(l); else if (!on && has) regs.Remove(l); // a levy left out of a scenario is not in the army at all: neither the Re-arm nor the allocation sees him
			}
			var r = te.Rules; r.Blocks.Clear(); r.Overrides.Clear(); r.Changed(); te.MarkDirty();
		}

		private static void Stock(ArmoryTE te, int copies, params int[] types)
		{
			foreach (int t in types) { if (t <= 0) continue; for (int c = 0; c < copies; c++) { var left = te.Deposit(Make(t)); if (!IsAir(left)) Note("armory full: could not store " + left.Name); } }
		}

		/// <summary>A class balance the way the Rules tab sets it: exactly this many levies of each class.</summary>
		private static void Balance(ArmoryTE te, params (string cls, int n)[] parts)
		{
			var r = te.Rules; r.Blocks.Clear(); r.Overrides.Clear();
			var b = r.AddBlock(RuleKind.ClassBalance);
			foreach (var (cls, n) in parts) b.EntryFor(cls, true).Fixed = n;
			r.Changed(); te.MarkDirty();
		}

		private static void Rearm(Scene s)
		{
			s.Armory.MarkDirty(); Tick();
			if (Environment.GetEnvironmentVariable("LEVY_AC_TRACE") == "1") WeaponAllocator.Trace = m => Note("trace " + m);
			try { RosterSystem.Instance.Rearm(s.Cmd); } finally { WeaponAllocator.Trace = null; }
		}

		private static void Upgrades(Scene s, int rounds)
		{
			var f = typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			for (int r = 0; r < rounds; r++) foreach (var l in Active(s)) { f?.SetValue(null, Main.GameUpdateCount + 1); s.Armory.UpgradeWeapons(l); }
		}

		private static IEnumerable<SoldierRecord> Active(Scene s) => s.Levies.Where(l => RosterSystem.Instance.Regulars.Contains(l));

		private static DamageClass WClass(Scene s, SoldierRecord l) { using var _rs = ArmoryRules.Enter(s.Armory.Rules); return WeaponClassifier.ItemDamageClass(l.Gear.Weapon); }
		private static DamageClass AClass(SoldierRecord l) => ArmorDoctrine.PreferredClass(l.Gear.Head, l.Gear.Body, l.Gear.Legs);
		private static string Key(DamageClass dc) => WeaponTags.ClassKey(dc) ?? "-";

		private static string Dump(Scene s) => string.Join(" | ", Active(s).Select(l => $"{Key(WClass(s, l))}:{Nm(l.Gear.Weapon)} / {Nm(l.Gear.Head)}, {Nm(l.Gear.Body)}, {Nm(l.Gear.Legs)} [{Key(AClass(l))}]"));

		/// <summary>Every armor piece by type: the armory's storage and armor rows plus what every levy wears.</summary>
		private static Dictionary<int, int> ArmorCensus(Scene s)
		{
			var d = new Dictionary<int, int>();
			void Add(Item it) { if (IsAir(it) || (it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0)) return; d[it.type] = (d.TryGetValue(it.type, out int n) ? n : 0) + it.stack; }
			foreach (var it in s.Armory.Storage) Add(it);
			foreach (var it in s.Armory.IssueArmor) Add(it);
			foreach (var l in s.Levies) { Add(l.Gear.Head); Add(l.Gear.Body); Add(l.Gear.Legs); }
			return d;
		}

		private static bool SameCensus(Dictionary<int, int> a, Dictionary<int, int> b) => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out int n) && n == kv.Value);

		private static string CensusDiff(Dictionary<int, int> a, Dictionary<int, int> b)
		{
			var keys = a.Keys.Union(b.Keys).Where(k => (a.TryGetValue(k, out int x) ? x : 0) != (b.TryGetValue(k, out int y) ? y : 0));
			return string.Join(", ", keys.Select(k => Make(k).Name + " " + (a.TryGetValue(k, out int x) ? x : 0) + "->" + (b.TryGetValue(k, out int y) ? y : 0)));
		}

		/// <summary>Moves one piece of <paramref name="type"/> from the armory's storage onto the levy (the old piece goes into storage), so the census is unchanged. False when storage has none.</summary>
		private static bool Wear(ArmoryTE te, SoldierRecord l, int part, int type)
		{
			for (int i = 0; i < te.Storage.Length; i++)
			{
				var it = te.Storage[i];
				if (IsAir(it) || it.type != type) continue;
				te.Storage[i] = new Item();
				var old = part == 0 ? l.Gear.Head : part == 1 ? l.Gear.Body : l.Gear.Legs;
				if (part == 0) l.Gear.Head = it; else if (part == 1) l.Gear.Body = it; else l.Gear.Legs = it;
				if (!IsAir(old)) { var left = te.Deposit(old); if (!IsAir(left)) Note("armory full returning " + left.Name); }
				te.MarkDirty();
				return true;
			}
			return false;
		}

		/// <summary>Every levy's worn armor reads as his weapon's class (or as no class at all when his class has no armor in stock): the kit is coherent.</summary>
		private static int Incoherent(Scene s, Func<SoldierRecord, bool> who = null) => Active(s).Count(l => (who == null || who(l)) && AClass(l) != null && AClass(l) != WClass(s, l));

		private static void Run()
		{
			var s = Build(10);
			try
			{
				Check("a real barracks and armory were placed and 10 levies housed", s.Barracks != null && s.Armory != null && s.Levies.Count == 10);
				if (s.Barracks == null || s.Armory == null) return;
				Check("the armory serves the barracks' levies (Re-arm will reach it)", SettlementSystem.Instance.LinkedArmory(s.Barracks) == s.Armory);
				Config.LevyConfig.Get.ClassMatchArmor = true;
				string only = Environment.GetEnvironmentVariable("LEVY_AC_ONLY");
				if (only == null || only == "vanilla") Vanilla(s);
				if ((only == null || only == "calamity") && ModLoader.HasMod("CalamityMod")) Calamity(s);
				else if (only == null) Note("Calamity is not loaded: the Silva / God Slayer half is skipped");
			}
			finally { Teardown(s); }
		}

		// ------------------------------------------------------------------ vanilla

		private static readonly int[] Solar = { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings };
		private static readonly int[] Vortex = { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings };
		private static readonly int[] Nebula = { ItemID.NebulaHelmet, ItemID.NebulaBreastplate, ItemID.NebulaLeggings };
		private static readonly int[] Stardust = { ItemID.StardustHelmet, ItemID.StardustBreastplate, ItemID.StardustLeggings };
		private static readonly int[] Bee = { ItemID.BeeHeadgear, ItemID.BeeBreastplate, ItemID.BeeGreaves };
		private static readonly int[] BeetleSet = { ItemID.BeetleHelmet, ItemID.BeetleScaleMail, ItemID.BeetleLeggings };

		private static void StockLunar(ArmoryTE te, int sets)
		{
			Stock(te, sets, Solar); Stock(te, sets, Vortex); Stock(te, sets, Nebula); Stock(te, sets, Stardust);
			Stock(te, 2, BeetleSet); Stock(te, 2, Bee);
			Stock(te, 2, ItemID.StardustDragonStaff, ItemID.StardustCellStaff, ItemID.RainbowWhip, ItemID.EmpressBlade);
			Stock(te, 2, ItemID.LunarFlareBook, ItemID.NebulaBlaze, ItemID.NebulaArcanum);
			Stock(te, 2, ItemID.Meowmere, ItemID.StarWrath, ItemID.TerraBlade);
			Stock(te, 2, ItemID.SDMG, ItemID.ChainGun, ItemID.Megashark);
			foreach (var _ in Enumerable.Range(0, 8)) te.Deposit(Make(ItemID.MusketBall, 999));
		}

		private static void Vanilla(Scene s)
		{
			var te = s.Armory;
			Check("Stardust reads as summon, Nebula as magic, Solar Flare as melee, Vortex as ranged",
				ArmorDoctrine.PreferredClass(Make(Stardust[0]), Make(Stardust[1]), Make(Stardust[2])) == DamageClass.Summon && ArmorDoctrine.PreferredClass(Make(Nebula[0]), Make(Nebula[1]), Make(Nebula[2])) == DamageClass.Magic
				&& ArmorDoctrine.PreferredClass(Make(Solar[0]), Make(Solar[1]), Make(Solar[2])) == DamageClass.Melee && ArmorDoctrine.PreferredClass(Make(Vortex[0]), Make(Vortex[1]), Make(Vortex[2])) == DamageClass.Ranged);
			foreach (var set in new[] { Solar, Stardust, Nebula, Vortex, BeetleSet, Bee })
				Note($"{Make(set[0]).Name} set: defense {set.Sum(t => Make(t).defense)}, tier {set.Average(t => ArmoryTE.PieceTier(Make(t))):0.0}, set score {set.Sum(t => ArmoryTE.ArmorScoreOf(Make(t))):0}");

			// ---- post Moon Lord, a class balance of every vanilla class
			WeaponRank.ForceProgress = 18f;
			(string, int)[] lunarBalance = { ("summon", 2), ("magic", 2), ("melee", 2), ("ranged", 2) };
			Reset(s, 8); StockLunar(te, 3); Balance(te, lunarBalance);
			var census = ArmorCensus(s);
			Rearm(s);
			Note("post-ML class balance, Re-arm: " + Dump(s));
			int summ = Active(s).Count(l => WClass(s, l) == DamageClass.Summon), mag = Active(s).Count(l => WClass(s, l) == DamageClass.Magic);
			Check($"the class balance dealt 2 summoners and 2 mages ({summ} and {mag})", summ == 2 && mag == 2);
			Check("every summoner wears the Stardust set over the higher-defense Solar Flare", Active(s).Where(l => WClass(s, l) == DamageClass.Summon).All(l => l.Gear.Head.type == ItemID.StardustHelmet && l.Gear.Body.type == ItemID.StardustBreastplate && l.Gear.Legs.type == ItemID.StardustLeggings));
			Check("every mage wears the Nebula set", Active(s).Where(l => WClass(s, l) == DamageClass.Magic).All(l => l.Gear.Head.type == ItemID.NebulaHelmet && l.Gear.Body.type == ItemID.NebulaBreastplate && l.Gear.Legs.type == ItemID.NebulaLeggings));
			Check("every levy's armor reads as his weapon's class (post-ML)", Incoherent(s) == 0);
			Check("nothing duplicated or lost by the Re-arm (post-ML) " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));

			// ---- an army already in the wrong armor: summoners and mages put in Solar Flare, then the upgrade pass alone
			Stock(te, 4, Solar);
			census = ArmorCensus(s);
			foreach (var l in Active(s).Where(l => WClass(s, l) == DamageClass.Summon || WClass(s, l) == DamageClass.Magic).ToList())
				for (int p = 0; p < 3; p++) Wear(te, l, p, Solar[p]);
			Note("summoners and mages put in Solar Flare: " + Dump(s));
			Upgrades(s, 3);
			Note("after 3 upgrade passes: " + Dump(s));
			Check("summoners in Solar Flare go back to Stardust on their own upgrade pass", Active(s).Where(l => WClass(s, l) == DamageClass.Summon).All(l => l.Gear.Head.type == ItemID.StardustHelmet && l.Gear.Body.type == ItemID.StardustBreastplate && l.Gear.Legs.type == ItemID.StardustLeggings));
			Check("mages in Solar Flare go back to Nebula on their own upgrade pass", Active(s).Where(l => WClass(s, l) == DamageClass.Magic).All(l => l.Gear.Head.type == ItemID.NebulaHelmet));
			Check("nothing duplicated or lost by the upgrade passes " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));
			var snap = Dump(s); Upgrades(s, 2);
			Check("two more upgrade passes change no armor (no thrash)", Dump(s) == snap);

			// ---- the setting off: the old strongest-set pick (this is the report: off-class armor of higher defense wins)
			Config.LevyConfig.Get.ClassMatchArmor = false;
			Reset(s, 8); StockLunar(te, 3); Balance(te, lunarBalance);
			Rearm(s);
			Note("setting off, post-ML class balance, Re-arm: " + Dump(s));
			Check("with class-matched armor off the strongest set wins again: a summoner or mage wears another class's set (the reported behaviour)", Incoherent(s, l => WClass(s, l) == DamageClass.Summon || WClass(s, l) == DamageClass.Magic) > 0);
			Config.LevyConfig.Get.ClassMatchArmor = true;

			// ---- the tier tolerance: pre-Hardmode summoner armor is not worn over post-ML armor
			Reset(s, 1); Stock(te, 1, Bee); Stock(te, 1, Solar); Stock(te, 1, BeetleSet); Stock(te, 1, ItemID.StardustDragonStaff);
			Balance(te, ("summon", 1));
			Rearm(s);
			var lone = s.Levies[0];
			Note("post-ML summoner, only Bee (summon), Solar Flare and Beetle in stock: " + Dump(s));
			Check("a post-ML summoner does not wear pre-Hardmode Bee armor over Solar Flare or Beetle (beyond the tier tolerance)", WClass(s, lone) == DamageClass.Summon && lone.Gear.Head.type != ItemID.BeeHeadgear && lone.Gear.Body.type != ItemID.BeeBreastplate);

			// ---- a hand-assigned weapon leads the armor, with no rules at all
			Reset(s, 1); Stock(te, 1, Stardust); Stock(te, 1, Solar);
			lone.Gear.Weapon = Make(ItemID.StardustDragonStaff); lone.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			for (int p = 0; p < 3; p++) Wear(te, lone, p, Solar[p]);
			census = ArmorCensus(s);
			Upgrades(s, 2);
			Note("no rules, a hand-assigned Stardust Dragon Staff, in Solar Flare, after the upgrade pass: " + Dump(s));
			Check("a levy with a hand-assigned summon weapon swaps Solar Flare for Stardust on his upgrade pass", lone.Gear.Head.type == ItemID.StardustHelmet && lone.Gear.Body.type == ItemID.StardustBreastplate && lone.Gear.Weapon.type == ItemID.StardustDragonStaff);
			Check("nothing duplicated or lost (hand-assigned weapon) " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));

			// ---- after Golem: Spectre Mask (magic) over the Spectre Hood (a healer's hood), Beetle and Shroomite; Tiki for the summoner
			WeaponRank.ForceProgress = 13f;
			Reset(s, 8);
			Stock(te, 2, ItemID.SpectreHood); Stock(te, 3, ItemID.SpectreMask); Stock(te, 4, ItemID.SpectreRobe, ItemID.SpectrePants);
			Stock(te, 3, BeetleSet); Stock(te, 2, ItemID.BeetleShell);
			Stock(te, 2, ItemID.ShroomiteHelmet, ItemID.ShroomiteMask, ItemID.ShroomiteHeadgear); Stock(te, 4, ItemID.ShroomiteBreastplate, ItemID.ShroomiteLeggings);
			Stock(te, 2, ItemID.TurtleHelmet, ItemID.TurtleScaleMail, ItemID.TurtleLeggings);
			Stock(te, 2, ItemID.TikiMask, ItemID.TikiShirt, ItemID.TikiPants);
			Stock(te, 2, ItemID.RazorbladeTyphoon, ItemID.MagnetSphere, ItemID.SpectreStaff, ItemID.BatScepter);
			Stock(te, 2, ItemID.ChainGun, ItemID.TacticalShotgun, ItemID.Uzi);
			Stock(te, 2, ItemID.TerraBlade, ItemID.InfluxWaver, ItemID.TheHorsemansBlade);
			Stock(te, 2, ItemID.XenoStaff, ItemID.MaceWhip, ItemID.TempestStaff);
			foreach (var _ in Enumerable.Range(0, 8)) te.Deposit(Make(ItemID.MusketBall, 999));
			Note("Spectre Hood set " + ArmorDoctrine.DebugMeasure(Make(ItemID.SpectreHood), Make(ItemID.SpectreRobe), Make(ItemID.SpectrePants)) + "; Spectre Mask set " + ArmorDoctrine.DebugMeasure(Make(ItemID.SpectreMask), Make(ItemID.SpectreRobe), Make(ItemID.SpectrePants)));
			Balance(te, ("magic", 3), ("ranged", 2), ("melee", 2), ("summon", 1));
			census = ArmorCensus(s);
			Rearm(s);
			Note("post-Golem class balance, Re-arm: " + Dump(s));
			var mages = Active(s).Where(l => WClass(s, l) == DamageClass.Magic).ToList();
			Check($"every mage wears the Spectre Mask with the Spectre Robe and Pants, not the Hood, Beetle or Shroomite ({mages.Count} mages)", mages.Count == 3 && mages.All(l => l.Gear.Head.type == ItemID.SpectreMask && l.Gear.Body.type == ItemID.SpectreRobe && l.Gear.Legs.type == ItemID.SpectrePants));
			Check("the summoner wears the Tiki set over Beetle and Turtle", Active(s).Where(l => WClass(s, l) == DamageClass.Summon).All(l => l.Gear.Head.type == ItemID.TikiMask && l.Gear.Body.type == ItemID.TikiShirt));
			Check("every levy's armor reads as his weapon's class (post-Golem)", Incoherent(s) == 0);
			Check("nothing duplicated or lost (post-Golem) " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));
			WeaponRank.ForceProgress = -1f;
		}

		// ------------------------------------------------------------------ Calamity

		private static void Calamity(Scene s)
		{
			var te = s.Armory;
			int gsMelee = Cal("GodSlayerHeadMelee"), gsRanged = Cal("GodSlayerHeadRanged"), gsRogue = Cal("GodSlayerHeadRogue"), gsBody = Cal("GodSlayerChestplate"), gsLegs = Cal("GodSlayerLeggings");
			int svSummon = Cal("SilvaHeadSummon"), svMagic = Cal("SilvaHeadMagic"), svBody = Cal("SilvaArmor"), svLegs = Cal("SilvaLeggings");
			Check("the God Slayer and Silva pieces exist in this Calamity", new[] { gsMelee, gsRanged, gsRogue, gsBody, gsLegs, svSummon, svMagic, svBody, svLegs }.All(t => t > 0));
			if (new[] { gsMelee, gsRanged, gsRogue, gsBody, gsLegs, svSummon, svMagic, svBody, svLegs }.Any(t => t <= 0)) return;
			var rogue = WeaponTags.ClassFromKey("rogue");
			Check("Calamity's rogue class is known", rogue != null);
			if (rogue == null) return;
			var gsHelms = new HashSet<int> { gsMelee, gsRanged, gsRogue };

			(int h, DamageClass want)[] reads = { (svSummon, DamageClass.Summon), (svMagic, DamageClass.Magic), (gsMelee, DamageClass.Melee), (gsRanged, DamageClass.Ranged), (gsRogue, rogue) };
			foreach (var (h, want) in reads)
			{
				bool sv = h == svSummon || h == svMagic;
				var hi = Make(h); var bi = Make(sv ? svBody : gsBody); var li = Make(sv ? svLegs : gsLegs);
				Note($"{hi.Name} set: defense {hi.defense + bi.defense + li.defense}, tier {(ArmoryTE.PieceTier(hi) + ArmoryTE.PieceTier(bi) + ArmoryTE.PieceTier(li)) / 3f:0.0}, score {ArmoryTE.ArmorScoreOf(hi) + ArmoryTE.ArmorScoreOf(bi) + ArmoryTE.ArmorScoreOf(li):0}, {ArmorDoctrine.DebugMeasure(hi, bi, li)}");
				Check($"the {hi.Name} set reads as {Key(want)} and forms a set bonus", ArmorDoctrine.PreferredClass(hi, bi, li) == want && ArmorDoctrine.FormsSetBonus(hi, bi, li));
			}

			// rogue weapons: the first issuable Calamity rogue weapons (stacks of one, no ammo, not gated shut)
			var roguePool = new List<int>();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount && roguePool.Count < 6; t++)
			{
				var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.accessory || it.ammo > 0 || it.useAmmo > 0 || it.maxStack > 1 || it.damage <= 0 || it.createTile >= 0 || !WeaponClassifier.IsIssuableWeapon(it) || ModWeaponGates.Locked(it)) continue;
				if (WeaponTags.DerivedClass(it) != "rogue" || WeaponRank.Of(it) <= 0f) continue;
				roguePool.Add(t);
			}
			Note("rogue weapons stocked: " + string.Join(", ", roguePool.Select(t => Make(t).Name)));

			void StockPostDoG(int silvaBodies, int extraMasks)
			{
				Stock(te, 2, gsMelee, gsRanged); Stock(te, 3 + extraMasks, gsRogue); Stock(te, 7 + extraMasks, gsBody, gsLegs);
				Stock(te, 3, svSummon, svMagic); Stock(te, silvaBodies, svBody, svLegs);
				Stock(te, 2, ItemID.StardustDragonStaff, ItemID.StardustCellStaff, ItemID.RainbowWhip, ItemID.EmpressBlade);
				Stock(te, 2, ItemID.LunarFlareBook, ItemID.NebulaBlaze, ItemID.NebulaArcanum);
				Stock(te, 2, ItemID.Meowmere, ItemID.StarWrath);
				Stock(te, 2, ItemID.SDMG, ItemID.ChainGun);
				Stock(te, 2, roguePool.ToArray());
				foreach (var _ in Enumerable.Range(0, 6)) te.Deposit(Make(ItemID.MusketBall, 999));
			}
			(string, int)[] balance = { ("summon", 3), ("magic", 3), ("melee", 1), ("ranged", 1), ("rogue", 2) };
			bool IsSumMag(SoldierRecord l) { var c = WClass(s, l); return c == DamageClass.Summon || c == DamageClass.Magic; }
			bool SilvaSummon(SoldierRecord l) => l.Gear.Head.type == svSummon && l.Gear.Body.type == svBody && l.Gear.Legs.type == svLegs;
			bool SilvaMagic(SoldierRecord l) => l.Gear.Head.type == svMagic && l.Gear.Body.type == svBody && l.Gear.Legs.type == svLegs;
			int ClassHelm(DamageClass c) => c == DamageClass.Melee ? gsMelee : c == DamageClass.Ranged ? gsRanged : gsRogue;

			// ---- post-DoG, Eve's case: a class balance with summoners and mages, God Slayer (all of it) and Silva (both hoods) in stock
			WeaponRank.ForceProgress = 21f;
			Reset(s, 10); StockPostDoG(6, 0); Balance(te, balance);
			var census = ArmorCensus(s);
			Rearm(s);
			Note("post-DoG class balance, Re-arm: " + Dump(s));
			int summ = Active(s).Count(l => WClass(s, l) == DamageClass.Summon), mag = Active(s).Count(l => WClass(s, l) == DamageClass.Magic), rog = Active(s).Count(l => WClass(s, l) == rogue);
			Check($"the class balance dealt 3 summoners, 3 mages and 2 rogues ({summ}, {mag}, {rog})", summ == 3 && mag == 3 && rog == 2);
			Check("every summoner wears Silva Horned Hood, Silva Armor and Silva Leggings", Active(s).Where(l => WClass(s, l) == DamageClass.Summon).All(SilvaSummon));
			Check("every mage wears Silva Masked Cap, Silva Armor and Silva Leggings", Active(s).Where(l => WClass(s, l) == DamageClass.Magic).All(SilvaMagic));
			Check("no summoner or mage wears a God Slayer helmet", !Active(s).Any(l => IsSumMag(l) && gsHelms.Contains(l.Gear.Head.type)));
			Check("melee, ranged and rogue levies wear God Slayer with their own class helmet", Active(s).Where(l => !IsSumMag(l)).All(l => l.Gear.Head.type == ClassHelm(WClass(s, l)) && l.Gear.Body.type == gsBody && l.Gear.Legs.type == gsLegs));
			Check("every levy's armor reads as his weapon's class (post-DoG)", Incoherent(s) == 0);
			Check("nothing duplicated or lost by the Re-arm (post-DoG) " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));

			// ---- an army already in God Slayer (a save from before the fix): summoners and mages in the God Slayer Mask set, Silva on the shelf; the upgrade pass alone
			Stock(te, 6, gsRogue, gsBody, gsLegs);
			census = ArmorCensus(s);
			foreach (var l in Active(s).Where(IsSumMag).ToList()) { Wear(te, l, 0, gsRogue); Wear(te, l, 1, gsBody); Wear(te, l, 2, gsLegs); }
			Note("summoners and mages put in the God Slayer Mask set: " + Dump(s));
			Config.LevyConfig.Get.ClassMatchArmor = false;
			Upgrades(s, 1);
			int stuck = Active(s).Count(l => IsSumMag(l) && gsHelms.Contains(l.Gear.Head.type));
			Note($"setting off (the old pick), one upgrade pass: {stuck} of 6 summoners and mages still in God Slayer: " + Dump(s));
			Check("with class-matched armor off an army in God Slayer stays in it on the upgrade pass (the reported behaviour)", stuck > 0);
			Config.LevyConfig.Get.ClassMatchArmor = true;
			Upgrades(s, 3);
			Note("after 3 upgrade passes: " + Dump(s));
			Check("summoners already in God Slayer swap to the Silva Horned Hood set on their own upgrade pass", Active(s).Where(l => WClass(s, l) == DamageClass.Summon).All(SilvaSummon));
			Check("mages already in God Slayer swap to the Silva Masked Cap set on their own upgrade pass", Active(s).Where(l => WClass(s, l) == DamageClass.Magic).All(SilvaMagic));
			Check("nothing duplicated or lost by the upgrade passes (post-DoG) " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));
			int gsInStock = s.Armory.Storage.Count(it => !IsAir(it) && it.type == gsRogue);
			Check($"the God Slayer Masks they took off are back in the armory ({gsInStock} in storage)", gsInStock >= 6);
			var snap = Dump(s); Upgrades(s, 2);
			Check("two more upgrade passes change no armor (no thrash, post-DoG)", Dump(s) == snap);

			// ---- shared pieces: both Silva hoods share one body and legs; only 4 of each for 6 summoners and mages
			Reset(s, 10); StockPostDoG(4, 0); Balance(te, balance);
			census = ArmorCensus(s);
			Rearm(s);
			Note("4 Silva bodies and legs for 3 summoners and 3 mages, Re-arm: " + Dump(s));
			Check("every summoner wears the Silva Horned Hood and every mage the Silva Masked Cap, though only 4 Silva bodies exist", Active(s).All(l => WClass(s, l) == DamageClass.Summon ? l.Gear.Head.type == svSummon : WClass(s, l) == DamageClass.Magic ? l.Gear.Head.type == svMagic : true));
			int full = Active(s).Count(l => SilvaSummon(l) || SilvaMagic(l));
			Check($"the 4 Silva bodies and legs went to 4 of them as full sets ({full})", full == 4);
			Check("nothing duplicated or lost (shared pieces) " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));
			snap = Dump(s); Upgrades(s, 2);
			Note("after 2 upgrade passes: " + Dump(s));
			Check("the upgrade pass leaves the shared-piece kits alone (no thrash, no hood traded away)", Dump(s) == snap);

			// ---- the same shortage on an army already in God Slayer: the upgrade pass puts the class helm on
			Reset(s, 10); StockPostDoG(4, 6); Balance(te, balance);
			Rearm(s);
			census = ArmorCensus(s);
			foreach (var l in Active(s).Where(IsSumMag).ToList()) { Wear(te, l, 0, gsRogue); Wear(te, l, 1, gsBody); Wear(te, l, 2, gsLegs); }
			Upgrades(s, 3);
			Note("shortage, army in God Slayer, after 3 upgrade passes: " + Dump(s));
			Check("with too few Silva bodies an army in God Slayer still gets the class helm on its upgrade pass", Active(s).All(l => WClass(s, l) == DamageClass.Summon ? l.Gear.Head.type == svSummon : WClass(s, l) == DamageClass.Magic ? l.Gear.Head.type == svMagic : true));
			Check("nothing duplicated or lost (shortage upgrade) " + CensusDiff(census, ArmorCensus(s)), SameCensus(census, ArmorCensus(s)));

			// ---- the setting off: the old pick, which is the report
			Config.LevyConfig.Get.ClassMatchArmor = false;
			Reset(s, 10); StockPostDoG(6, 0); Balance(te, balance);
			Rearm(s);
			Note("setting off, post-DoG class balance, Re-arm: " + Dump(s));
			Check("with class-matched armor off a summoner or mage wears God Slayer again (the reported behaviour)", Active(s).Any(l => IsSumMag(l) && gsHelms.Contains(l.Gear.Head.type)));
			Config.LevyConfig.Get.ClassMatchArmor = true;
			Reset(s, 10);
			WeaponRank.ForceProgress = -1f;
		}
	}
}
