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
	/// WORLD-LEVEL self-check of "a levy's sidearm matches his class", logged as "[SidearmWorld]" lines (LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_SIDEARMWORLD=1 runs it alone).
	/// Eve's report: levies carry a Meowmere as a sidearm while they play a ranger. This places a real barracks and armory, houses a real levy in class armor with a primary in hand, stocks the armory, and asks the armory
	/// for his sidearm (<see cref="ArmoryTE.FillEmptySidearms"/>, the pass the periodic upgrade and IssueGear run).
	/// <list type="bullet">
	/// <item>A post-Moon Lord ranger in Vortex armor with a slow primary gets a ranged sidearm, never a Meowmere, with a copy of his own primary and a Meowmere on the shelf.</item>
	/// <item>A sidearm is never the primary's item type (fill, and the upgrade pass that tidies a carried copy).</item>
	/// <item>Before Hardmode an off-class sidearm is still allowed when it is much better; with the setting off the old lesser-blade pick returns.</item>
	/// <item>A rule that asks for his primary wins over his armor: a mage in Nebula armor whose rules ask for guns carries a gun, with no rule a staff.</item>
	/// </list>
	/// Everything it places is removed again; nothing is saved.
	/// </summary>
	public class SidearmWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300, Bx = 760; private const string Commander = "SidearmCmd";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool lab = Environment.GetEnvironmentVariable("LEVY_SIDEARMWORLD") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_SIDEARMWORLD") != "0";
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
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Main.myPlayer = my0; ArmoryTE.TestServed = null; WeaponRank.ForceProgress = -1f; WeaponRank.Release(); }
			_mod.Logger.Info($"[SidearmWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[SidearmWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[SidearmWorld] OBSERVED " + s);

		private static void Flatten(int cx, int floorY, int half = 14)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = floorY - 14; j < floorY; j++) { var t = Main.tile[i, j]; t.ClearEverything(); }
				var f = Main.tile[i, floorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
			}
		}

		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }
		private static string Nm(Item it) => it == null || it.IsAir ? "(none)" : it.Name;

		private sealed class Scene
		{
			public BarracksTE Barracks; public ArmoryTE Armory; public Player Cmd; public SoldierRecord Levy;
			public readonly List<(int i, int j, int w, int h)> Placed = new();
		}

		private static Scene Build()
		{
			var s = new Scene();
			int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();
			Flatten(Bx + 1, FloorY); Flatten(Bx + 5, FloorY);
			WorldGen.PlaceObject(Bx + 1, FloorY - 1, barType, true);
			ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement(Bx, FloorY - 3, barType, 0, 1, 0); s.Placed.Add((Bx, FloorY - 3, 3, 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx, FloorY - 3), out var e) && e is BarracksTE b) { b.TownName = "SidearmTown"; b.Treasury = 10_000_000; s.Barracks = b; }
			WorldGen.PlaceObject(Bx + 4, FloorY - 1, armType, true);
			ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(Bx + 4, FloorY - 3, armType, 0, 1, 0); s.Placed.Add((Bx + 4, FloorY - 3, 3, 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx + 4, FloorY - 3), out var e2) && e2 is ArmoryTE a) s.Armory = a;
			s.Cmd = Main.player[0] ?? (Main.player[0] = new Player());
			s.Cmd.active = true; s.Cmd.name = Commander;
			if (s.Barracks == null || s.Armory == null) return s;
			var rec = new SoldierRecord { Id = 94000, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "Sidearm 0", CommanderName = Commander, CommanderIndex = 0, HomeBarracksId = s.Barracks.ID, ManaCrystals = 10 };
			rec.CurrentHp = rec.MaxHp; RosterSystem.Instance.Regulars.Add(rec); s.Levy = rec;
			return s;
		}

		private static void Teardown(Scene s)
		{
			try { if (s.Levy != null) { if (s.Levy.HasBody) Main.npc[s.Levy.BodyIndex].active = false; RosterSystem.Instance.Regulars.Remove(s.Levy); } } catch { }
			foreach (var (i, j, w, h) in s.Placed) for (int x = i; x < i + w; x++) for (int y = j; y < j + h; y++) if (WorldGen.InWorld(x, y)) Main.tile[x, y].ClearEverything();
			foreach (var id in new List<int>(TileEntity.ByID.Keys)) { var te = TileEntity.ByID[id]; if (te is BarracksTE || te is ArmoryTE) { TileEntity.ByID.Remove(id); TileEntity.ByPosition.Remove(te.Position); } }
		}

		private static void Clear(ArmoryTE te)
		{
			for (int i = 0; i < te.Storage.Length; i++) te.Storage[i] = new Item();
			for (int i = 0; i < te.IssueWeapons.Length; i++) te.IssueWeapons[i] = new Item();
			for (int i = 0; i < te.IssueAmmo.Length; i++) te.IssueAmmo[i] = new Item();
		}

		private static void Stock(ArmoryTE te, params int[] types)
		{
			foreach (int t in types) { var left = te.Deposit(Make(t)); if (left != null && !left.IsAir) Note("armory full: could not store " + left.Name); }
		}

		/// <summary>One levy kitted as asked (armor, primary, ammo, no sidearm), the armory stocked as asked; returns his sidearm after the armory fills the empty slot.</summary>
		private static Item Sidearm(Scene s, int[] armor, int primary, int ammo, int[] stock, Action<ArmoryRules> rules = null)
		{
			var te = s.Armory; var rec = s.Levy;
			Clear(te);
			var r = te.Rules; r.Blocks.Clear(); r.Overrides.Clear(); rules?.Invoke(r); r.Changed(); te.MarkDirty();
			rec.Gear = new SoldierGear();
			var g = rec.Gear;
			g.Head = Make(armor[0]); g.Body = Make(armor[1]); g.Legs = Make(armor[2]);
			g.Weapon = Make(primary);
			if (ammo > 0) g.Ammo = Make(ammo, 999);
			Stock(te, stock);
			ArmoryTE.TestServed = new List<SoldierRecord> { rec };
			te.FillEmptySidearms(rec);
			return g.Sidearm;
		}

		private static readonly int[] VortexArmor = { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings };
		private static readonly int[] NebulaArmor = { ItemID.NebulaHelmet, ItemID.NebulaBreastplate, ItemID.NebulaLeggings };
		private static readonly int[] NecroArmor = { ItemID.NecroHelmet, ItemID.NecroBreastplate, ItemID.NecroGreaves };

		private static void Run()
		{
			var s = Build();
			try
			{
				Check("a real barracks and armory were placed and a levy housed", s.Barracks != null && s.Armory != null && s.Levy != null);
				if (s.Barracks == null || s.Armory == null || s.Levy == null) return;
				bool oldCfg = Common.Config.LevyConfig.Get.ClassMatchSidearms;
				try { Scenarios(s); }
				finally { Common.Config.LevyConfig.Get.ClassMatchSidearms = oldCfg; }
			}
			finally { Teardown(s); }
		}

		private static void Scenarios(Scene s)
		{
			var te = s.Armory;
			Common.Config.LevyConfig.Get.ClassMatchSidearms = true;
			SidearmClass.ForceStrength = -1f;

			// ---- the armor really reads as the class the check assumes
			Check("Vortex armor reads as ranged, Nebula as magic, Necro as ranged", ArmorDoctrine.PreferredClass(Make(VortexArmor[0]), Make(VortexArmor[1]), Make(VortexArmor[2])) == DamageClass.Ranged
				&& ArmorDoctrine.PreferredClass(Make(NebulaArmor[0]), Make(NebulaArmor[1]), Make(NebulaArmor[2])) == DamageClass.Magic
				&& ArmorDoctrine.PreferredClass(Make(NecroArmor[0]), Make(NecroArmor[1]), Make(NecroArmor[2])) == DamageClass.Ranged);

			// ---- the strength curve (pure)
			Check("the class preference is weak before Hardmode and full after the Moon Lord", SidearmClass.Strength(3f) < 0.2f && SidearmClass.Strength(18f) >= 0.999f && SidearmClass.Strength(12f) > SidearmClass.Strength(8f) && SidearmClass.OffFactor(SidearmClass.Strength(18f)) < 0.1f && SidearmClass.OffFactor(SidearmClass.Strength(3f)) > 0.4f);
			{
				var sn = Make(ItemID.SniperRifle); var sd = Make(ItemID.SDMG); var bs = Make(ItemID.Boomstick);
				Check("inside a class a slow primary pairs with a fast sidearm (Sniper Rifle " + sn.useTime + " ticks: S.D.M.G. " + sd.useTime + ", Boomstick " + bs.useTime + ")", SidearmClass.Complement(sn, sd) > SidearmClass.Complement(sn, bs));
			}

			// ---- post Moon Lord ranger: slow primary, ranged sidearm, never a Meowmere
			WeaponRank.ForceProgress = 18f; WeaponRank.Release();
			var stockML = new int[] { ItemID.Meowmere, ItemID.TerraBlade, ItemID.SniperRifle, ItemID.VortexBeater, ItemID.SDMG, ItemID.Megashark, ItemID.ChainGun, ItemID.StarWrath };
			var side = Sidearm(s, VortexArmor, ItemID.SniperRifle, ItemID.MusketBall, stockML);
			Note("post-ML ranger, primary " + Nm(s.Levy.Gear.Weapon) + ": sidearm " + Nm(side) + " (use time " + (side?.useTime ?? 0) + " vs primary " + s.Levy.Gear.Weapon.useTime + ")");
			Check("post-ML ranger in Vortex armor with a slow primary gets a ranged sidearm", !IsAir(side) && WeaponClassifier.ItemDamageClass(side) == DamageClass.Ranged);
			Check("post-ML ranger never carries a Meowmere (or any other blade) as his sidearm while ranged options exist", !IsAir(side) && side.type != ItemID.Meowmere && side.type != ItemID.TerraBlade && side.type != ItemID.StarWrath);
			Check("the slow primary pairs with a faster sidearm", !IsAir(side) && side.useTime < s.Levy.Gear.Weapon.useTime);
			Check("the sidearm is not the primary's item type (a copy of the primary is on the shelf)", !IsAir(side) && side.type != s.Levy.Gear.Weapon.type);
			Check("the ranged sidearm shares the primary's ammo (the pouch fires it)", !IsAir(side) && (side.useAmmo <= 0 || side.useAmmo == s.Levy.Gear.Ammo.ammo));

			// ---- a copy of the primary carried as a sidearm goes back when the upgrade pass looks
			{
				var g = s.Levy.Gear;
				g.Sidearm = Make(g.Weapon.type);
				te.UpgradeWeapons(s.Levy);
				Check("a sidearm of the primary's own item type is given back by the upgrade pass", IsAir(g.Sidearm) || g.Sidearm.type != g.Weapon.type);
				Note("after the upgrade pass: primary " + Nm(g.Weapon) + ", sidearm " + Nm(g.Sidearm));
			}

			// ---- a blade carried before his armor was known gives way to a sidearm of his class
			{
				var g = s.Levy.Gear;
				g.Sidearm = Make(ItemID.Meowmere);
				te.UpgradeWeapons(s.Levy);
				Note("post-ML ranger given a Meowmere sidearm, after the upgrade pass: " + Nm(g.Sidearm));
				Check("a Meowmere carried by a post-ML ranger yields to a ranged sidearm in the upgrade pass", !IsAir(g.Sidearm) && g.Sidearm.type != ItemID.Meowmere && WeaponClassifier.ItemDamageClass(g.Sidearm) == DamageClass.Ranged);
			}

			// ---- the setting off brings the old pick back (a lesser blade)
			Common.Config.LevyConfig.Get.ClassMatchSidearms = false;
			var old = Sidearm(s, VortexArmor, ItemID.SniperRifle, ItemID.MusketBall, stockML);
			Note("setting off: sidearm " + Nm(old));
			Check("with class-matched sidearms off the ranger carries a lesser blade as before", !IsAir(old) && WeaponClassifier.ItemDamageClass(old) == DamageClass.Melee);
			Common.Config.LevyConfig.Get.ClassMatchSidearms = true;

			// ---- before Hardmode an off-class sidearm is still allowed when it is much better
			WeaponRank.ForceProgress = 5f;
			foreach (int t in new int[] { ItemID.FlintlockPistol, ItemID.EnchantedSword, ItemID.CopperShortsword }) { var q = Make(t); Note("rank at progress 5: " + q.Name + " " + WeaponRank.Of(q).ToString("0.0") + " (tier " + ProgressionTier.Of(t) + ")"); }
			var early = Sidearm(s, NecroArmor, ItemID.Handgun, ItemID.MusketBall, new int[] { ItemID.FlintlockPistol, ItemID.EnchantedSword });
			Note("pre-Hardmode ranger in Necro armor: sidearm " + Nm(early));
			Check("before Hardmode an off-class sidearm is allowed when it is much better (an Enchanted Sword over a Flintlock Pistol)", !IsAir(early) && early.type == ItemID.EnchantedSword);
			var early2 = Sidearm(s, NecroArmor, ItemID.Handgun, ItemID.MusketBall, new int[] { ItemID.FlintlockPistol, ItemID.CopperShortsword });
			Note("pre-Hardmode ranger, a Flintlock Pistol beside a Copper Shortsword: sidearm " + Nm(early2));
			Check("before Hardmode a ranged sidearm still wins when the blade is not much better", !IsAir(early2) && early2.type == ItemID.FlintlockPistol);

			// ---- no class armor: the old pick
			WeaponRank.ForceProgress = 18f;
			var bare = Sidearm(s, new[] { 0, 0, 0 }, ItemID.SDMG, ItemID.MusketBall, stockML);
			Note("no armor: sidearm " + Nm(bare));
			Check("a levy with no class armor keeps the old pick, a lesser blade", !IsAir(bare) && WeaponClassifier.ItemDamageClass(bare) == DamageClass.Melee);

			// ---- the armory's rules win over the armor
			var mageStock = new int[] { ItemID.Meowmere, ItemID.LunarFlareBook, ItemID.NebulaBlaze, ItemID.SDMG, ItemID.Megashark, ItemID.ChainGun };
			var mage = Sidearm(s, NebulaArmor, ItemID.Megashark, ItemID.MusketBall, mageStock);
			Note("Nebula mage, no rule, gun primary: sidearm " + Nm(mage));
			Check("with no rule a mage in Nebula armor carries a magic sidearm", !IsAir(mage) && WeaponClassifier.ItemDamageClass(mage) == DamageClass.Magic);
			var forced = Sidearm(s, NebulaArmor, ItemID.Megashark, ItemID.MusketBall, mageStock, r => { var b = r.AddBlock(RuleKind.Category); b.Mode = CatMode.Include; b.Cat = "gun"; });
			Note("Nebula mage, rules include guns, gun primary: sidearm " + Nm(forced));
			Check("a rule asking for guns wins over the armor: the gun primary gets a gun sidearm, not a staff", !IsAir(forced) && WeaponClassifier.ItemDamageClass(forced) == DamageClass.Ranged && forced.type != ItemID.Megashark);
			Clear(te); te.Rules.Blocks.Clear(); te.Rules.Changed();
		}

		private static bool IsAir(Item it) => it == null || it.IsAir;
	}
}
