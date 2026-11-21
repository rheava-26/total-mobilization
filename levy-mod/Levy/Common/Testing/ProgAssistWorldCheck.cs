using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Progression;
using Levy.Content.Items;

namespace Levy.Common.Testing
{
	/// <summary>
	/// PROGRESSION ASSISTANCE, CALAMITY OPTIONS, IN A REAL WORLD ("[ProgAssist]" lines; LEVY_SELFCHECK_WORLD=1 on a dedicated server, LEVY_PROGASSIST_ONLY=1 runs it alone).
	/// Every option is checked OFF (it changes nothing) and then ON (it does its thing): the Superpowered Magnet makes Exodium and keeps its cooldown, the Slime Agitator starts the rain and ore slimes appear,
	/// the ore multiplier reaches Calamity's own generators, Yharon's repeat kill makes Auric Ore, Jungle monsters drop Uelibloom Ore, Providence and the post-Providence materials drop in bulk (real drop rules, run
	/// through the game's own drop solver), and the recipes carry the right ingredients. The vanilla half checks that all of it is inert. Every global it touches (config values, boss flags, the rain, the zone flags) is put back.
	/// </summary>
	public class ProgAssistWorldCheck : ModSystem
	{
		private static bool _armed, _done; private static int _pass, _fail; private static Mod _mod;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_PROGASSIST") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ProgAssist] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[ProgAssist] OBSERVED " + s);

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			var c = LevyProgressionConfig.Get;
			var saved = Snapshot(c);
			Player p0 = Main.player[0] ??= new Player();
			bool act0 = p0.active, jungle0 = p0.ZoneJungle, overworld0 = p0.ZoneOverworldHeight; Vector2 pos0 = p0.position;
			bool ml0 = NPC.downedMoonlord, hm0 = Main.hardMode, plant0 = NPC.downedPlantBoss, m1 = NPC.downedMechBoss1, m2 = NPC.downedMechBoss2, m3 = NPC.downedMechBoss3, rain0 = Main.slimeRain;
			var calFlags = new[] { "downedYharon", "downedProvidence", "downedCryogen", "downedHiveMind", "downedPerforator", "downedAstrumDeus" };
			var flags0 = calFlags.ToDictionary(f => f, f => CalamityLink.Downed(f));
			var rand0 = Main.rand; int cd0 = ExodiumMagnet.Cooldown;
			try
			{
				p0.active = true; p0.dead = false;
				Off(c); Recipes(c);
				if (!CalamityLink.Loaded) Vanilla(c); else { Magnet(c); OreGen(c); Auric(c); Uelibloom(c, p0); Providence(c, p0); Agitator(c, p0); }
			}
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				Restore(c, saved);
				foreach (var kv in flags0) CalamityLink.SetDowned(kv.Key, kv.Value);
				p0.active = act0; p0.ZoneJungle = jungle0; p0.ZoneOverworldHeight = overworld0; p0.position = pos0;
				NPC.downedMoonlord = ml0; Main.hardMode = hm0; NPC.downedPlantBoss = plant0; NPC.downedMechBoss1 = m1; NPC.downedMechBoss2 = m2; NPC.downedMechBoss3 = m3;
				if (Main.slimeRain && !rain0) Main.StopSlimeRain(false);
				AgitatedRain.Armed = false; ExodiumMagnet.Cooldown = cd0; Main.rand = rand0;
			}
			_mod.Logger.Info($"[ProgAssist] self-check pass={_pass} fail={_fail}");
		}

		// ---- config snapshot ----------------------------------------------------------------------------------------
		static readonly string[] Fields =
		{
			"SuperpoweredMagnetRecipe", "SuperpoweredMagnetCooldownMinutes", "ScoriaOreRecipe", "SlimeAgitatorRecipe", "CalamityOreGenMultiplier", "AuricOreRegeneration",
			"JungleUelibloomDrop", "JungleUelibloomChance", "ProvidenceDropMultiplier", "PostProvidenceMaterialMultiplier",
		};
		static Dictionary<string, object> Snapshot(LevyProgressionConfig c) => Fields.ToDictionary(f => f, f => typeof(LevyProgressionConfig).GetField(f).GetValue(c));
		static void Restore(LevyProgressionConfig c, Dictionary<string, object> s) { foreach (var kv in s) typeof(LevyProgressionConfig).GetField(kv.Key).SetValue(c, kv.Value); }
		static void Set(LevyProgressionConfig c, string f, object v) => typeof(LevyProgressionConfig).GetField(f).SetValue(c, v);

		static int Count(int type) { long n = 0; for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && Main.item[i].type == type) n += Main.item[i].stack; return (int)Math.Min(n, int.MaxValue); }
		static void ClearItems() { for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active) { Main.item[i].active = false; Main.item[i].TurnToAir(); } }
		static int CountTiles(int type, int maxY) { int n = 0; if (type <= 0) return 0; for (int x = 0; x < Main.maxTilesX; x++) for (int y = 0; y < maxY; y++) { Tile t = Main.tile[x, y]; if (t.HasTile && t.TileType == type) n++; } return n; }
		static NPC Spawn(int type) { int i = NPC.NewNPC(new EntitySource_WorldEvent("ProgAssist"), 1800 * 16, 200 * 16, type); return i >= 0 && i < Main.maxNPCs ? Main.npc[i] : null; }
		static void Kill(NPC n) { if (n != null) { n.active = false; } }

		// ---- everything off: vanilla -----------------------------------------------------------------------------------
		static void Off(LevyProgressionConfig c)
		{
			var d = typeof(LevyProgressionConfig);
			bool defaults = true; string bad = "";
			foreach (string f in Fields)
			{
				var fi = d.GetField(f); var attr = fi.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
				object cur = fi.GetValue(c);
				// the cooldown and chance sliders are not "off" options; they only matter once the item or the drop is on
				bool same = attr != null && Convert.ToDouble(attr.Value) == Convert.ToDouble(cur) || attr != null && attr.Value is bool b && (bool)cur == b;
				if (!same) { defaults = false; bad += f + " "; }
			}
			Note($"config as booted: {(defaults ? "all defaults" : "NOT all defaults: " + bad)}");
			var ex = new ExtraAmountRule { Providence = true, Post = true };
			bool allOff = !c.SuperpoweredMagnetRecipe && !c.ScoriaOreRecipe && !c.SlimeAgitatorRecipe && !c.AuricOreRegeneration && !c.JungleUelibloomDrop && c.CalamityOreGenMultiplier <= 1f && c.ProvidenceDropMultiplier <= 1f && c.PostProvidenceMaterialMultiplier <= 1f;
			if (allOff) Check("off: a Providence or material drop rule adds nothing", ex.Bonus(Main.player[0]) == 0);
			Check("off: extra rounds for multiplier 1 are none", CalamityOreGen.ExtraRounds(1f, 0.0) == 0);
		}

		// ---- recipes ------------------------------------------------------------------------------------------------
		static int Making(int type) { int n = 0; for (int r = 0; r < Recipe.numRecipes; r++) { var x = Main.recipe[r]; if (x != null && !x.Disabled && x.Mod == _mod && x.createItem.type == type) n++; } return n; }

		static void Recipes(LevyProgressionConfig c)
		{
			int magnet = ModContent.ItemType<SuperpoweredMagnet>(), agit = ModContent.ItemType<SlimeAgitator>(), scoria = CalamityLink.ItemType("ScoriaOre");
			bool cal = CalamityLink.Loaded;
			Check("recipes: a Levy magnet recipe exists iff its option is on and Calamity is loaded", (Making(magnet) > 0) == (c.SuperpoweredMagnetRecipe && cal));
			Check("recipes: a Levy agitator recipe exists iff its option is on and Calamity is loaded", (Making(agit) > 0) == (c.SlimeAgitatorRecipe && cal));
			Check("recipes: a Levy Scoria recipe exists iff its option is on and Calamity is loaded", scoria <= 0 || (Making(scoria) > 0) == (c.ScoriaOreRecipe && cal));
			if (!cal) { Check("recipes: without Calamity none are made even with every option on", AllOn(c, () => ProgressionCalamityRecipes.Specs(c).Count == 0)); return; }
			(int, int) I(int a, int b) => (a, b);
			bool Same(ProgressionCalamityRecipes.Spec r, int result, int stack, (int, int)[] ing, int tile) =>
				r.Result == result && r.Stack == stack && r.Tile == tile && r.Ingredients.Length == ing.Length && ing.All(i => r.Ingredients.Any(q => q.type == i.Item1 && q.stack == i.Item2));
			List<ProgressionCalamityRecipes.Spec> all = null; AllOn(c, () => { all = ProgressionCalamityRecipes.Specs(c); return true; });
			Check("recipes: with the options on, exactly three are made", all != null && all.Count == 3);
			if (all == null || all.Count != 3) return;
			var mag = all.FirstOrDefault(r => r.Result == magnet);
			Check($"recipe: Superpowered Magnet = {ProgConst.MagnetBars} Luminite Bars + {ProgConst.MagnetFragments} of each Lunar Fragment at an Ancient Manipulator, after Moon Lord",
				mag != null && Same(mag, magnet, 1, new[] { I(ItemID.LunarBar, ProgConst.MagnetBars), I(ItemID.FragmentSolar, ProgConst.MagnetFragments), I(ItemID.FragmentVortex, ProgConst.MagnetFragments), I(ItemID.FragmentNebula, ProgConst.MagnetFragments), I(ItemID.FragmentStardust, ProgConst.MagnetFragments) }, TileID.LunarCraftingStation) && mag.Cond == Condition.DownedMoonLord);
			var sco = all.FirstOrDefault(r => r.Result == scoria);
			Check($"recipe: {ProgConst.ScoriaYield} Scoria Ore = {ProgConst.ScoriaHellstone} Hellstone + {ProgConst.ScoriaChlorophyte} Chlorophyte Ore at an Adamantite Forge, after Golem",
				sco != null && Same(sco, scoria, ProgConst.ScoriaYield, new[] { I(ItemID.Hellstone, ProgConst.ScoriaHellstone), I(ItemID.ChlorophyteOre, ProgConst.ScoriaChlorophyte) }, TileID.AdamantiteForge) && sco.Cond == Condition.DownedGolem);
			var ag = all.FirstOrDefault(r => r.Result == agit);
			Check($"recipe: Slime Agitator = {ProgConst.AgitatorKingSlime} Royal Gel + {ProgConst.AgitatorQueenSlime} Volatile Gelatin + {ProgConst.AgitatorScoria} Scoria Ore at a Mythril Anvil",
				ag != null && Same(ag, agit, 1, new[] { I(ItemID.RoyalGel, ProgConst.AgitatorKingSlime), I(ItemID.VolatileGelatin, ProgConst.AgitatorQueenSlime), I(scoria, ProgConst.AgitatorScoria) }, TileID.MythrilAnvil));
			// the agitator's ingredients really are King Slime and Queen Slime drops
			bool Drops(int npcType, int bag, int item)
			{
				var rules = new List<IItemDropRule>();
				ProgressionLootNpc.Collect(Main.ItemDropsDB.GetRulesForNPCID(npcType, false), rules);
				ProgressionLootNpc.Collect(Main.ItemDropsDB.GetRulesForItemID(bag), rules);
				foreach (var r in rules)
					if (r is CommonDrop cd && cd.itemId == item || r is OneFromOptionsDropRule o1 && o1.dropIds.Contains(item) || r is OneFromOptionsNotScaledWithLuckDropRule o2 && o2.dropIds.Contains(item) || r is DropOneByOne obo && obo.itemId == item) return true;
				return false;
			}
			Check("agitator: Royal Gel is a King Slime drop", Drops(NPCID.KingSlime, ItemID.KingSlimeBossBag, ItemID.RoyalGel));
			Check("agitator: Volatile Gelatin is a Queen Slime drop", Drops(NPCID.QueenSlimeBoss, ItemID.QueenSlimeBossBag, ItemID.VolatileGelatin));
			Check("scoria recipe: Hellstone and Chlorophyte Ore are real items and Scoria Ore resolves", ItemID.Hellstone > 0 && scoria > 0);
		}

		static bool AllOn(LevyProgressionConfig c, Func<bool> f)
		{
			var s = Snapshot(c);
			try { Set(c, "SuperpoweredMagnetRecipe", true); Set(c, "ScoriaOreRecipe", true); Set(c, "SlimeAgitatorRecipe", true); return f(); }
			finally { Restore(c, s); }
		}

		// ---- vanilla half -------------------------------------------------------------------------------------------------
		static void Vanilla(LevyProgressionConfig c)
		{
			var s = Snapshot(c);
			try
			{
				Set(c, "SuperpoweredMagnetRecipe", true); Set(c, "ScoriaOreRecipe", true); Set(c, "SlimeAgitatorRecipe", true); Set(c, "AuricOreRegeneration", true); Set(c, "JungleUelibloomDrop", true); Set(c, "JungleUelibloomChance", 100f);
				Set(c, "CalamityOreGenMultiplier", 5f); Set(c, "ProvidenceDropMultiplier", 5f); Set(c, "PostProvidenceMaterialMultiplier", 5f);
				var magnet = new Item(); magnet.SetDefaults(ModContent.ItemType<SuperpoweredMagnet>()); var agit = new Item(); agit.SetDefaults(ModContent.ItemType<SlimeAgitator>());
				Check("vanilla: the magnet and the agitator cannot be used", !magnet.ModItem.CanUseItem(Main.player[0]) && !agit.ModItem.CanUseItem(Main.player[0]));
				Check("vanilla: the magnet's effect reports no Calamity and changes nothing", ExodiumMagnet.TryUse(true) == MagnetResult.NoCalamity && ExodiumMagnet.Cooldown == 0);
				bool rain = Main.slimeRain;
				Check("vanilla: the agitator does not start a rain", !AgitatedRain.Start() && Main.slimeRain == rain && !AgitatedRain.Armed);
				Check("vanilla: no ore hook is installed, no material set built", !CalamityOreGen.Installed && ProgressionDropsSystem.PostMaterials.Count == 0 && ProgressionDropsSystem.ProvidenceRules == 0 && ProgressionDropsSystem.PostMaterialRules == 0);
				Check("vanilla: the Uelibloom rule drops nothing", !new JungleUelibloomCondition().CanDrop(new DropAttemptInfo { player = Main.player[0], npc = new NPC() }));
				Check("vanilla: Auric regeneration finds no ore to make", !ProgressionCalamityNpc.GenerateAuric());
				Check("vanilla: no recipe is made with every option on", ProgressionCalamityRecipes.Specs(c).Count == 0 && ProgressionCalamityRecipes.Added == 0);
			}
			finally { Restore(c, s); }
		}

		// ---- 1. Superpowered Magnet --------------------------------------------------------------------------------------------
		static void Magnet(LevyProgressionConfig c)
		{
			var s = Snapshot(c);
			var hasFlag = CalamityLink.Cal.Code.GetType("CalamityMod.World.CalamityWorld")?.GetField("HasGeneratedLuminitePlanetoids", BindingFlags.Public | BindingFlags.Static);
			bool flag0 = hasFlag != null && (bool)hasFlag.GetValue(null);
			try
			{
				var item = new Item(); item.SetDefaults(ModContent.ItemType<SuperpoweredMagnet>());
				NPC.downedMoonlord = false; ExodiumMagnet.Cooldown = 0;
				Check("magnet: cannot be used before Moon Lord", !item.ModItem.CanUseItem(Main.player[0]) && ExodiumMagnet.TryUse(true) == MagnetResult.NotReady);
				NPC.downedMoonlord = true; Set(c, "SuperpoweredMagnetCooldownMinutes", 5);
				Check("magnet: can be used after Moon Lord", item.ModItem.CanUseItem(Main.player[0]));
				int before = ExodiumMagnet.CountExodium(); var sw = System.Diagnostics.Stopwatch.StartNew();
				var r = ExodiumMagnet.TryUse(true); sw.Stop();
				int after = ExodiumMagnet.CountExodium();
				Check($"magnet: a use re-forms Exodium planetoids ({before} -> {after} Exodium tiles, {sw.ElapsedMilliseconds} ms)", r == MagnetResult.Reformed && after > before);
				Check("magnet: Calamity's own 'already generated' flag is left as it was", hasFlag == null || (bool)hasFlag.GetValue(null) == flag0);
				Check($"magnet: the use starts the cooldown ({ExodiumMagnet.Cooldown} ticks = 5 minutes)", ExodiumMagnet.Cooldown == 5 * 3600);
				int mid = ExodiumMagnet.CountExodium();
				Check("magnet: a second use inside the cooldown does nothing", ExodiumMagnet.TryUse(true) == MagnetResult.Cooling && ExodiumMagnet.CountExodium() == mid);
				var tag = new TagCompound(); new ExodiumMagnet().SaveWorldData(tag); ExodiumMagnet.Cooldown = 0; new ExodiumMagnet().LoadWorldData(tag);
				Check("magnet: the cooldown is saved with the world", ExodiumMagnet.Cooldown == 5 * 3600);
				ExodiumMagnet.Cooldown = 0; Set(c, "SuperpoweredMagnetCooldownMinutes", 0);
				int b2 = ExodiumMagnet.CountExodium();
				Check("magnet: a cooldown of 0 leaves it ready at once", ExodiumMagnet.TryUse(true) == MagnetResult.Reformed && ExodiumMagnet.Cooldown == 0 && ExodiumMagnet.CountExodium() >= b2);
				Note($"Exodium tiles after three uses: {ExodiumMagnet.CountExodium()}");
			}
			finally { Restore(c, s); ExodiumMagnet.Cooldown = 0; }
		}

		// ---- 4. ore generation multiplier --------------------------------------------------------------------------------------
		static void OreGen(LevyProgressionConfig c)
		{
			var s = Snapshot(c);
			try
			{
				var spawn = CalamityLink.Method("CalamityMod.CalamityUtils", "SpawnOre", typeof(int), typeof(double), typeof(float), typeof(float), typeof(int), typeof(int), typeof(int[]));
				int auric = CalamityLink.TileType("AuricOre");
				Check("ore: the hook sits on Calamity's SpawnOre", CalamityOreGen.Installed && spawn != null && auric > 0);
				if (spawn == null || auric <= 0) return;
				void Gen(int tile, double f) => spawn.Invoke(null, new object[] { tile, f, 0.75f, 0.9f, 10, 20, Array.Empty<int>() });
				int sc0 = CalamityOreGen.ScaledCalls;
				Set(c, "CalamityOreGenMultiplier", 1f); const double F = 1E-04;
				int a0 = CountTiles(auric, Main.maxTilesY); Gen(auric, F); int a1 = CountTiles(auric, Main.maxTilesY);
				Check("ore off: multiplier 1 passes Calamity's frequency through untouched", CalamityOreGen.LastFrequency == F && CalamityOreGen.LastType == auric && CalamityOreGen.ScaledCalls == sc0);
				Set(c, "CalamityOreGenMultiplier", 4f);
				Gen(auric, F); int a2 = CountTiles(auric, Main.maxTilesY);
				Check("ore on: multiplier 4 hands Calamity four times the frequency", Math.Abs(CalamityOreGen.LastFrequency - 4 * F) < 1e-12 && CalamityOreGen.ScaledCalls == sc0 + 1);
				Check($"ore on: four times the frequency makes much more ore ({a1 - a0} tiles at 1x, {a2 - a1} at 4x)", a1 > a0 && (a2 - a1) > 2.0 * (a1 - a0));
				Gen(TileID.Mythril, F);
				Check("ore on: vanilla ore tiles are never scaled", CalamityOreGen.LastFrequency == F && CalamityOreGen.LastType == TileID.Mythril);

				// Aerialite: extra Generate + Enchant rounds after Calamity's own Enchant
				var enchant = CalamityOreGen.Enchant;
				int aer = CalamityLink.TileType("AerialiteOre"), clouds = CountTiles(TileID.Cloud, (int)Main.worldSurface);
				Set(c, "CalamityOreGenMultiplier", 1f); int e0 = CalamityOreGen.ExtraAerialiteRounds; int ae0 = CountTiles(aer, (int)Main.worldSurface);
				enchant.Invoke(null, null);
				Check("aerialite off: Enchant makes no extra round", CalamityOreGen.ExtraAerialiteRounds == e0 && CountTiles(aer, (int)Main.worldSurface) == ae0);
				Set(c, "CalamityOreGenMultiplier", 3f);
				enchant.Invoke(null, null); int ae1 = CountTiles(aer, (int)Main.worldSurface);
				Check("aerialite on: multiplier 3 runs two extra Generate and Enchant rounds", CalamityOreGen.ExtraAerialiteRounds == e0 + 2);
				if (clouds > 0) Check($"aerialite on: the extra rounds enchant new Aerialite in the clouds ({ae0} -> {ae1} tiles, {clouds} cloud tiles)", ae1 > ae0); else Note("no cloud tiles in this world: Aerialite tile growth not measurable");

				// Astral: extra meteors through PlaceAstralMeteor, past Calamity's ore cap
				var place = CalamityOreGen.Place; int ast = CalamityLink.TileType("AstralOre");
				int m0 = CalamityOreGen.ExtraMeteors; int as0 = CountTiles(ast, (int)Main.worldSurface + 100); var sw = System.Diagnostics.Stopwatch.StartNew();
				Set(c, "CalamityOreGenMultiplier", 3f); place.Invoke(null, null); sw.Stop();
				Check("astral on: multiplier 3 asks for two extra meteors", CalamityOreGen.ExtraMeteors == m0 + 2);
				Note($"astral ore tiles {as0} -> {CountTiles(ast, (int)Main.worldSurface + 100)} after a multiplier-3 PlaceAstralMeteor ({sw.ElapsedMilliseconds} ms; a meteor needs room and no dungeon or chest nearby)");
				Set(c, "CalamityOreGenMultiplier", 1f); int m1 = CalamityOreGen.ExtraMeteors; place.Invoke(null, null);
				Check("astral off: PlaceAstralMeteor makes no extra meteor", CalamityOreGen.ExtraMeteors == m1);
				Check("ore: 2.5 means one extra round and a half chance of another", CalamityOreGen.ExtraRounds(2.5f, 0.4) == 2 && CalamityOreGen.ExtraRounds(2.5f, 0.6) == 1);
			}
			finally { Restore(c, s); }
		}

		// ---- 5. Auric Ore regeneration ------------------------------------------------------------------------------------------
		static void Auric(LevyProgressionConfig c)
		{
			var s = Snapshot(c);
			try
			{
				int yh = CalamityLink.NpcType("Yharon"), auric = CalamityLink.TileType("AuricOre");
				Check("auric: Yharon and Auric Ore resolve", yh > 0 && auric > 0);
				if (yh <= 0) return;
				NPC n = Spawn(yh); if (n == null) { Check("auric: Yharon spawned for the check", false); return; }
				var g = n.GetGlobalNPC<ProgressionCalamityNpc>();
				int Run(bool downed)
				{
					CalamityLink.SetDowned("downedYharon", downed);
					int reg = ProgressionCalamityNpc.Regenerations, t0 = CountTiles(auric, Main.maxTilesY);
					g.PreKill(n); g.OnKill(n);
					return CountTiles(auric, Main.maxTilesY) - t0 + (ProgressionCalamityNpc.Regenerations - reg) * 1000000;
				}
				Set(c, "AuricOreRegeneration", false);
				Check("auric off: a repeat Yharon kill makes no Auric Ore", Run(true) == 0);
				Set(c, "AuricOreRegeneration", true);
				Check("auric on: the first Yharon kill is left to Calamity (no extra ore)", Run(false) == 0);
				int r = Run(true);
				Check($"auric on: a repeat Yharon kill generates Auric Ore again ({r % 1000000} tiles)", r / 1000000 == 1 && r % 1000000 > 0);
				Kill(n);
			}
			finally { Restore(c, s); }
		}

		// ---- 6. Uelibloom supply ---------------------------------------------------------------------------------------------------
		static void Uelibloom(LevyProgressionConfig c, Player p0)
		{
			var s = Snapshot(c);
			try
			{
				int uel = CalamityLink.ItemType("UelibloomOre"); Check("uelibloom: Uelibloom Ore resolves", uel > 0); if (uel <= 0) return;
				NPC n = Spawn(NPCID.Zombie); if (n == null) { Check("uelibloom: a plain monster spawned", false); return; }
				var info = new DropAttemptInfo { player = p0, npc = n, IsExpertMode = false, IsMasterMode = false, IsInSimulation = false, rng = Main.rand };
				var rule = new UelibloomSourceRule();
				Main.hardMode = true; p0.ZoneJungle = true; CalamityLink.SetDowned("downedProvidence", true); ClearItems();
				Set(c, "JungleUelibloomDrop", false); Set(c, "JungleUelibloomChance", 100f);
				for (int i = 0; i < 20; i++) rule.TryDroppingItem(info);
				Check("uelibloom off: Jungle kills drop nothing", Count(uel) == 0);
				Set(c, "JungleUelibloomDrop", true);
				for (int i = 0; i < 20; i++) rule.TryDroppingItem(info);
				int got = Count(uel);
				Check($"uelibloom on: 20 Jungle kills at 100% drop 1 to 3 Ore each ({got} Ore)", got >= 20 && got <= 60);
				ClearItems(); p0.ZoneJungle = false; for (int i = 0; i < 20; i++) rule.TryDroppingItem(info);
				Check("uelibloom on: nothing drops outside the Jungle", Count(uel) == 0);
				p0.ZoneJungle = true; CalamityLink.SetDowned("downedProvidence", false); for (int i = 0; i < 20; i++) rule.TryDroppingItem(info);
				Check("uelibloom on: nothing drops before Providence", Count(uel) == 0);
				CalamityLink.SetDowned("downedProvidence", true); Set(c, "JungleUelibloomChance", 25f); ClearItems(); int hits = 0, N = 2000;
				for (int i = 0; i < N; i++) { int b = Count(uel); rule.TryDroppingItem(info); if (Count(uel) > b) hits++; if (i % 100 == 99) ClearItems(); }
				Check($"uelibloom on: a 25% chance drops about a quarter of the time ({hits}/{N})", hits > N * 0.20 && hits < N * 0.30);
				ClearItems(); Kill(n);
			}
			finally { Restore(c, s); ClearItems(); }
		}

		// ---- 7. Providence and post-Providence materials --------------------------------------------------------------------------------
		static void Providence(LevyProgressionConfig c, Player p0)
		{
			var s = Snapshot(c);
			try
			{
				int prov = CalamityLink.NpcType("Providence"), geode = CalamityLink.ItemType("DivineGeode"), ess = CalamityLink.ItemType("UnholyEssence"), bag = CalamityLink.ItemType("ProvidenceBag"), imp = CalamityLink.NpcType("ImpiousImmolator");
				Check("providence: her NPC, her bag and her materials resolve", prov > 0 && geode > 0 && ess > 0 && bag > 0);
				Check($"providence: the loot database got the extra-amount rules ({ProgressionDropsSystem.ProvidenceRules} on Providence and her bag, {ProgressionDropsSystem.PostMaterialRules} on the three materials)", ProgressionDropsSystem.ProvidenceRules >= 2 && ProgressionDropsSystem.PostMaterialRules >= 3);
				if (prov <= 0 || geode <= 0) return;
				NPC n = Spawn(prov); if (n == null) { Check("providence: spawned for the drop check", false); return; }
				int Drop(int itemSource, int type, int times = 1)
				{
					ClearItems(); long sum = 0;
					for (int i = 0; i < times; i++)
					{
						var info = new DropAttemptInfo { player = p0, npc = itemSource == 0 ? n : null, item = itemSource, IsExpertMode = false, IsMasterMode = false, IsInSimulation = false, rng = Main.rand };
						Main.ItemDropSolver.TryDropping(info);
						sum += Count(type); ClearItems();
					}
					return (int)Math.Min(sum, int.MaxValue);
				}
				Set(c, "ProvidenceDropMultiplier", 1f); Set(c, "PostProvidenceMaterialMultiplier", 1f);
				int g1 = Drop(0, geode), e1 = Drop(0, ess), bg1 = Drop(bag, geode);
				Check($"providence off: her kill drops her normal 50-60 Divine Geode and 30-40 Unholy Essence ({g1}, {e1})", g1 >= 50 && g1 <= 60 && e1 >= 30 && e1 <= 40);
				Check($"providence off: her Treasure Bag drops its normal 60-70 Divine Geode ({bg1})", bg1 >= 60 && bg1 <= 70);
				Set(c, "ProvidenceDropMultiplier", 4f);
				int g4 = Drop(0, geode), e4 = Drop(0, ess), bg4 = Drop(bag, geode);
				Check($"providence x4: her Divine Geode and Unholy Essence drops quadruple: four times the normal 50-60 and 30-40 ({g4} vs {g1}, {e4} vs {e1})", g4 >= 200 && g4 <= 240 && e4 >= 120 && e4 <= 160);
				Check($"providence x4: her Treasure Bag's Divine Geode quadruples ({bg4} vs {bg1})", bg4 >= 240 && bg4 <= 280);
				Set(c, "ProvidenceDropMultiplier", 1f); Set(c, "PostProvidenceMaterialMultiplier", 4f);
				int gp = Drop(0, geode), ep = Drop(0, ess);
				Check($"materials x4: Providence's own Divine Geode and Unholy Essence quadruple too ({gp}, {ep})", gp >= 200 && gp <= 240 && ep >= 120 && ep <= 160);
				if (imp > 0)
				{
					NPC m = Spawn(imp);
					Set(c, "PostProvidenceMaterialMultiplier", 1f); int mTotal1 = 0;
					NPC mob = m; int DropMob(int times) { long sum = 0; for (int i = 0; i < times; i++) { ClearItems(); Main.ItemDropSolver.TryDropping(new DropAttemptInfo { player = p0, npc = mob, IsExpertMode = false, IsMasterMode = false, rng = Main.rand }); sum += Count(ess); } ClearItems(); return (int)sum; }
					mTotal1 = DropMob(40); Set(c, "PostProvidenceMaterialMultiplier", 5f); int mTotal5 = DropMob(40);
					Check($"materials x5: an Impious Immolator's Unholy Essence drops about five times as much ({mTotal5} vs {mTotal1} over 40 kills)", mTotal1 >= 40 * 2 && mTotal1 <= 40 * 4 && mTotal5 >= 4.0 * mTotal1 && mTotal5 <= 6.0 * mTotal1);
					Kill(m);
				}
				else Note("no Impious Immolator in this Calamity: the mob-source material check is skipped");
				// a material the options do not name is never scaled
				Set(c, "ProvidenceDropMultiplier", 8f); Set(c, "PostProvidenceMaterialMultiplier", 8f);
				int ruleCoins = Drop(0, ItemID.GoldCoin); Check("providence x8: coins are not materials and are never scaled", ruleCoins <= 100);
				Kill(n);
			}
			finally { Restore(c, s); ClearItems(); }
		}

		// ---- 3. Slime Agitator ------------------------------------------------------------------------------------------------------
		static void Agitator(LevyProgressionConfig c, Player p0)
		{
			// the weather this check changes, put back at the end
			bool rain0 = Main.raining, remix0 = Main.remixWorld; double rainTime0 = Main.rainTime, slimeTime0 = Main.slimeRainTime; float maxRain0 = Main.maxRaining; int warn0 = Main.slimeWarningTime;
			try
			{
				var item = new Item(); item.SetDefaults(ModContent.ItemType<SlimeAgitator>());
				foreach (string f in new[] { "downedHiveMind", "downedPerforator", "downedCryogen", "downedProvidence", "downedAstrumDeus" }) CalamityLink.SetDowned(f, false);
				NPC.downedPlantBoss = false; NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = false;
				Check("agitator: with no ore boss down no ore slime joins the rain", AgitatedRain.Available().Count == 0);
				NPC.downedPlantBoss = true; CalamityLink.SetDowned("downedProvidence", true); CalamityLink.SetDowned("downedHiveMind", true); CalamityLink.SetDowned("downedAstrumDeus", true);
				CalamityLink.SetDowned("downedCryogen", true); NPC.downedMechBoss1 = NPC.downedMechBoss2 = true;
				var avail = AgitatedRain.Available();
				Check($"agitator: with the bosses down all five ore slimes join ({avail.Count})", avail.Count == AgitatedRain.OreSlimes.Length);
				NPC.downedMechBoss2 = false;
				Check("agitator: Cryo Slime needs two Mechanical Bosses", !AgitatedRain.Available().Contains(CalamityLink.NpcType("CryoSlime")));
				NPC.downedMechBoss2 = true;
				if (Main.slimeRain) Main.StopSlimeRain(false);
				Note($"weather when the agitator check began: raining {rain0} (rainTime {rainTime0:0}, maxRaining {maxRain0:0.00}), remix world {remix0}");
				bool CanUse() => item.ModItem.CanUseItem(Main.player[0]);
				void Rain() { Main.raining = true; Main.rainTime = 36000; Main.maxRaining = 0.6f; }
				void EndSlimeRain() { if (Main.slimeRain) Main.StopSlimeRain(false); AgitatedRain.Armed = false; }
				// vanilla on its own: StartSlimeRain returns without starting while an ordinary rain is up
				Main.remixWorld = false; Rain(); Main.StartSlimeRain(false); bool vanillaStarts = Main.slimeRain; EndSlimeRain();
				Note($"vanilla StartSlimeRain while an ordinary rain is up: {(vanillaStarts ? "starts" : "returns without starting")}");
				// (a) clear skies
				Main.StopRain();
				Check("agitator: can be used under clear skies", CanUse() && !Main.slimeRain);
				Check("agitator: the use starts a slime rain under clear skies", AgitatedRain.Start() && Main.slimeRain && AgitatedRain.Armed);
				Check("agitator: cannot be used while a slime rain is up", !CanUse());
				Check("agitator: a use while a slime rain is up starts nothing", !AgitatedRain.Start() && Main.slimeRain);
				EndSlimeRain();
				// (b) an ordinary rain: the slime rain starts and the rain carries on untouched
				Rain(); double rt = Main.rainTime;
				Check("agitator: can be used while an ordinary rain is up", CanUse());
				Check("agitator: the use starts a slime rain while an ordinary rain is up", AgitatedRain.Start() && Main.slimeRain && AgitatedRain.Armed);
				Check($"agitator: the ordinary rain carries on (raining {Main.raining}, rainTime {Main.rainTime:0}, maxRaining {Main.maxRaining:0.00})", Main.raining && Main.rainTime == rt && Main.maxRaining == 0.6f);
				Check("agitator: cannot be used while a slime rain is up in an ordinary rain", !CanUse());
				// (c) a remix world, where vanilla never starts a slime rain: the agitator cannot be used, so it is never consumed for nothing
				EndSlimeRain(); Main.StopRain(); Main.remixWorld = true;
				Check("agitator: cannot be used in a remix world, and a use there starts nothing", !CanUse() && !AgitatedRain.Start() && !Main.slimeRain);
				Main.remixWorld = false;
				// (d) the ore slimes, in the harder case: an agitated slime rain inside an ordinary rain
				Rain();
				Check("agitator: the use starts a slime rain for the ore-slime check", AgitatedRain.Start() && Main.slimeRain && AgitatedRain.Armed);
				// the rain's ore slimes: the player stands high above the ground, in the overworld band
				p0.position = new Vector2(2100 * 16f, 150 * 16f); p0.ZoneOverworldHeight = true;
				var sys = ModContent.GetInstance<AgitatedRain>(); int s0 = AgitatedRain.Spawned;
				for (int i = 0; i < 4000; i++) sys.PostUpdateWorld();
				var types = AgitatedRain.Available(); int alive = 0; var seen = new HashSet<int>();
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && types.Contains(Main.npc[i].type)) { alive++; seen.Add(Main.npc[i].type); }
				Check($"agitator: ore slimes appear in the rain ({AgitatedRain.Spawned - s0} spawned, {alive} alive, {seen.Count} kinds)", AgitatedRain.Spawned - s0 >= 5 && alive >= 1);
				Check($"agitator: no more than 10 ore slimes at once ({alive})", alive <= 10);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && types.Contains(Main.npc[i].type)) Main.npc[i].active = false;
				Main.StopSlimeRain(false); sys.PostUpdateWorld();
				int s1 = AgitatedRain.Spawned; for (int i = 0; i < 2000; i++) sys.PostUpdateWorld();
				Check("agitator: when the rain ends the ore slimes stop", !AgitatedRain.Armed && AgitatedRain.Spawned == s1);
				// without the agitator a plain slime rain brings none (clear skies, or vanilla would not start it)
				Main.StopRain(); Main.StartSlimeRain(false); bool plain = Main.slimeRain; for (int i = 0; i < 2000; i++) sys.PostUpdateWorld(); Main.StopSlimeRain(false);
				Check("agitator: a slime rain that was not agitated spawns no ore slimes", plain && AgitatedRain.Spawned == s1);
			}
			finally
			{
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && AgitatedRain.OreSlimes.Any(o => Main.npc[i].type == CalamityLink.NpcType(o.npc))) Main.npc[i].active = false;
				if (Main.slimeRain) Main.StopSlimeRain(false);
				AgitatedRain.Armed = false;
				Main.raining = rain0; Main.rainTime = rainTime0; Main.maxRaining = maxRain0; Main.remixWorld = remix0; Main.slimeRainTime = slimeTime0; Main.slimeWarningTime = warn0;
			}
		}
	}
}
