using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Content.Items;

namespace Levy.Common.Progression
{
	/// <summary>
	/// The Calamity side of progression assistance. Levy never references Calamity at compile time: every call goes through <see cref="Mod.Code"/> by name, so a build without Calamity loads and these options are inert.
	/// </summary>
	public static class CalamityLink
	{
		static readonly BindingFlags Pub = BindingFlags.Public | BindingFlags.Static;
		public static Mod Cal => ModLoader.TryGetMod("CalamityMod", out Mod m) && m.Code != null ? m : null;
		public static bool Loaded => Cal != null;
		public static int ItemType(string name) => Cal != null && Cal.TryFind<ModItem>(name, out var i) ? i.Type : 0;
		public static int NpcType(string name) => Cal != null && Cal.TryFind<ModNPC>(name, out var n) ? n.Type : 0;
		public static int TileType(string name) => Cal != null && Cal.TryFind<ModTile>(name, out var t) ? t.Type : 0;

		/// <summary>Calamity's DownedBossSystem flag by property name (downedProvidence, downedCryogen ...). False without Calamity or when the name is not found.</summary>
		public static bool Downed(string flag)
		{
			try { var p = Cal?.Code.GetType("CalamityMod.DownedBossSystem")?.GetProperty(flag, Pub); return p != null && (bool)p.GetValue(null); }
			catch (Exception) { return false; }
		}

		/// <summary>Sets one of those flags (the world check flips them and puts them back).</summary>
		public static bool SetDowned(string flag, bool value)
		{
			try { var p = Cal?.Code.GetType("CalamityMod.DownedBossSystem")?.GetProperty(flag, Pub); if (p == null) return false; p.SetValue(null, value); return true; }
			catch (Exception) { return false; }
		}

		public static MethodInfo Method(string type, string name, params Type[] args)
		{
			try { var t = Cal?.Code.GetType(type); return t == null ? null : (args == null || args.Length == 0 ? t.GetMethod(name, Pub) : t.GetMethod(name, Pub, null, args, null)); }
			catch (Exception) { return null; }
		}

		/// <summary>True when a tile type belongs to Calamity.</summary>
		public static bool IsCalamityTile(int type)
		{
			var t = TileLoader.GetTile(type);
			return t != null && t.Mod.Name == "CalamityMod";
		}
	}

	/// <summary>Result of using the Superpowered Magnet.</summary>
	public enum MagnetResult { Reformed, Cooling, NotReady, NoCalamity, Failed }

	/// <summary>
	/// The Superpowered Magnet's effect. Calamity makes its Exodium planetoids (a shell of Exodium around a Luminite core, high above the world) once, when Moon Lord dies, by calling
	/// <c>CalamityMod.World.Planets.LuminitePlanet.GenerateLuminitePlanetoids()</c>. That method does not look at the "already generated" flag, so the magnet calls it again.
	/// Cost: the magnet is kept, and each use starts a cooldown (config, 5 minutes by default, 0 for none) that is saved with the world.
	/// </summary>
	public class ExodiumMagnet : ModSystem
	{
		/// <summary>Game ticks until the next use is allowed. Saved with the world.</summary>
		public static int Cooldown;
		public static int Uses;

		public override void OnWorldLoad() => Cooldown = 0;
		public override void OnWorldUnload() => Cooldown = 0;
		public override void PostUpdateWorld() { if (Cooldown > 0 && Main.netMode != NetmodeID.MultiplayerClient) Cooldown--; }
		public override void SaveWorldData(TagCompound tag) { if (Cooldown > 0) tag["levyMagnetCd"] = Cooldown; }
		public override void LoadWorldData(TagCompound tag) => Cooldown = tag.ContainsKey("levyMagnetCd") ? tag.GetInt("levyMagnetCd") : 0;

		public static int CooldownTicks => 3600 * Math.Max(0, LevyProgressionConfig.Get?.SuperpoweredMagnetCooldownMinutes ?? 5);

		/// <summary>Server / single player: regenerate the planetoids if allowed. <paramref name="sync"/> runs it on this thread (the world check); play runs it on the pool like Calamity does.</summary>
		public static MagnetResult TryUse(bool sync)
		{
			if (!CalamityLink.Loaded) return MagnetResult.NoCalamity;
			if (!NPC.downedMoonlord) return MagnetResult.NotReady;
			if (Cooldown > 0) return MagnetResult.Cooling;
			var m = CalamityLink.Method("CalamityMod.World.Planets.LuminitePlanet", "GenerateLuminitePlanetoids");
			if (m == null) return MagnetResult.Failed;
			Cooldown = CooldownTicks; Uses++;
			if (sync) m.Invoke(null, null);
			else ThreadPool.QueueUserWorkItem(_ => { try { m.Invoke(null, null); } catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: the Superpowered Magnet failed: " + e.Message); } });
			return MagnetResult.Reformed;
		}

		/// <summary>How many Exodium tiles stand in the sky band of the world (where the planetoids are).</summary>
		public static int CountExodium()
		{
			int type = CalamityLink.TileType("ExodiumOre"); if (type <= 0) return 0;
			int n = 0, maxY = Math.Min(Main.maxTilesY, 300);
			for (int x = 0; x < Main.maxTilesX; x++)
				for (int y = 0; y < maxY; y++) { Tile t = Main.tile[x, y]; if (t.HasTile && t.TileType == type) n++; }
			return n;
		}

		public static void Say(MagnetResult r, int who)
		{
			string key = r switch { MagnetResult.Reformed => "Progression.MagnetReformed", MagnetResult.Cooling => "Progression.MagnetCooling", _ => null };
			if (key == null) return;
			object[] args = r == MagnetResult.Cooling ? new object[] { (Cooldown + 3599) / 3600 } : Array.Empty<object>();
			Color c = new(150, 200, 255);
			if (Main.netMode == NetmodeID.Server) ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Mods.Levy." + key, args), c, who);
			else Main.NewText(Language.GetTextValue("Mods.Levy." + key, args), c);
		}
	}

	/// <summary>The Slime Agitator's rain. The item starts vanilla's slime rain; while that rain lasts every player on the surface also meets Calamity's ore slimes (the ones whose bosses are down).</summary>
	public class AgitatedRain : ModSystem
	{
		/// <summary>True from the agitator's use until its rain ends.</summary>
		public static bool Armed;
		public static int Spawned;
		const int MaxAlive = 10;
		/// <summary>Calamity slime, the boss flags that bring it into the rain, and the ore it drops.</summary>
		public static readonly (string npc, string ore)[] OreSlimes = { ("AeroSlime", "AerialiteOre"), ("CryoSlime", "CryonicOre"), ("PerennialSlime", "PerennialOre"), ("BloomSlime", "UelibloomOre"), ("AstralSlime", "AstralOre") };

		public override void OnWorldLoad() { Armed = false; Spawned = 0; }
		public override void OnWorldUnload() { Armed = false; }

		/// <summary>The same boss gates Calamity gives each slime's own spawn.</summary>
		public static bool Unlocked(string slime) => slime switch
		{
			"AeroSlime" => CalamityLink.Downed("downedHiveMind") || CalamityLink.Downed("downedPerforator"),
			"CryoSlime" => CalamityLink.Downed("downedCryogen") && ((NPC.downedMechBoss1 || NPC.downedMechBoss2) && (NPC.downedMechBoss2 || NPC.downedMechBoss3) && (NPC.downedMechBoss3 || NPC.downedMechBoss1)),
			"PerennialSlime" => NPC.downedPlantBoss,
			"BloomSlime" => CalamityLink.Downed("downedProvidence"),
			"AstralSlime" => CalamityLink.Downed("downedAstrumDeus"),
			_ => false,
		};

		public static List<int> Available()
		{
			var l = new List<int>();
			foreach (var (npc, _) in OreSlimes) { int t = CalamityLink.NpcType(npc); if (t > 0 && Unlocked(npc)) l.Add(t); }
			return l;
		}

		/// <summary>True when a use would start the rain. The item's CanUseItem, so a use that cannot work never consumes it. Vanilla never starts a slime rain in a remix world (Don't Dig Up, Get Fixed Boi).</summary>
		public static bool CanStart => CalamityLink.Loaded && !Main.slimeRain && !Main.remixWorld;

		/// <summary>Server / single player: start the rain (the item's effect). Works in any weather.
		/// Vanilla's StartSlimeRain returns without starting on the server or in single player while an ordinary rain is up; that rain is left exactly as it is
		/// (stopping it would also end Calamity's Acid Rain), so the check is masked for the one call and the announcement is made after the rain flag is back,
		/// because the world-data packet zeroes maxRaining when it sees no rain.</summary>
		public static bool Start()
		{
			if (!CanStart) return false;
			bool rain = Main.raining;
			Main.raining = false;
			try { Main.StartSlimeRain(false); }
			finally { Main.raining = rain; }
			Armed = Main.slimeRain;
			if (!Main.slimeRain) return false;
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				Main.slimeWarningTime = Main.slimeWarningDelay;    // what StartSlimeRain(true) does: the "Slime is falling from the sky!" announcement
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
			}
			return true;
		}

		public override void PostUpdateWorld()
		{
			if (!Armed || Main.netMode == NetmodeID.MultiplayerClient) return;
			if (!Main.slimeRain) { Armed = false; return; }
			var types = Available(); if (types.Count == 0) return;
			int alive = 0;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && types.Contains(Main.npc[i].type)) alive++;
			if (alive >= MaxAlive) return;
			for (int p = 0; p < Main.maxPlayers; p++)
			{
				Player pl = Main.player[p];
				if (!pl.active || pl.dead || !pl.ZoneOverworldHeight || Main.rand.Next(ProgConst.OreSlimeSpawnTicks) != 0) continue;
				SpawnNear(pl, types[Main.rand.Next(types.Count)]);
			}
		}

		/// <summary>Drop one ore slime in open air above the player, the way rain slimes arrive. False when no open spot was found.</summary>
		public static bool SpawnNear(Player pl, int type)
		{
			for (int tries = 0; tries < 10; tries++)
			{
				Vector2 at = new(pl.Center.X + Main.rand.Next(-900, 901), pl.Center.Y - Main.rand.Next(450, 700));
				if (Collision.SolidCollision(at - new Vector2(20, 15), 40, 30)) continue;
				int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("Levy:SlimeAgitator"), (int)at.X, (int)at.Y, type);
				if (i >= 0 && i < Main.maxNPCs) { Spawned++; return true; }
			}
			return false;
		}
	}

	/// <summary>Route for the two items' client -> server packet.</summary>
	public class ProgItemNet : ModSystem
	{
		public override void Load() => global::Levy.Common.Net.LevyPackets.Register(global::Levy.Common.Net.LevyPacket.ProgItemUse, OnPacket);

		static void OnPacket(System.IO.BinaryReader r, int who)
		{
			byte kind = r.ReadByte();
			if (who < 0 || who >= Main.maxPlayers || !Main.player[who].active || Main.player[who].dead) return;
			int held = Main.player[who].HeldItem.type;
			if (kind == 0 && held == ModContent.ItemType<SuperpoweredMagnet>()) ExodiumMagnet.Say(ExodiumMagnet.TryUse(false), who);
			else if (kind == 1 && held == ModContent.ItemType<SlimeAgitator>()) AgitatedRain.Start();
		}
	}

	/// <summary>New recipes for the Calamity options. Nothing is added unless Calamity is loaded and the option is on.</summary>
	public class ProgressionCalamityRecipes : ModSystem
	{
		public static int Added;

		/// <summary>One recipe the options ask for, as data (a Recipe can only be built inside the recipe hooks, so the world check reads these).</summary>
		public sealed class Spec
		{
			public int Result, Stack, Tile; public (int type, int stack)[] Ingredients; public Condition Cond;
		}

		public override void AddRecipes()
		{
			Added = 0;
			var c = LevyProgressionConfig.Get;
			if (c == null) return;
			foreach (var sp in Specs(c))
			{
				var r = Recipe.Create(sp.Result, sp.Stack);
				foreach (var (t, n) in sp.Ingredients) r.AddIngredient(t, n);
				r.AddTile(sp.Tile);
				if (sp.Cond != null) r.AddCondition(sp.Cond);
				r.Register(); Added++;
			}
		}

		/// <summary>The recipes the options ask for. Empty without Calamity.</summary>
		public static List<Spec> Specs(LevyProgressionConfig c)
		{
			var list = new List<Spec>();
			if (c == null || !CalamityLink.Loaded) return list;
			int scoria = CalamityLink.ItemType("ScoriaOre");
			if (c.SuperpoweredMagnetRecipe)
				list.Add(new Spec { Result = ModContent.ItemType<SuperpoweredMagnet>(), Stack = 1, Tile = TileID.LunarCraftingStation, Cond = Condition.DownedMoonLord,
					Ingredients = new (int, int)[] { (ItemID.LunarBar, ProgConst.MagnetBars), (ItemID.FragmentSolar, ProgConst.MagnetFragments), (ItemID.FragmentVortex, ProgConst.MagnetFragments), (ItemID.FragmentNebula, ProgConst.MagnetFragments), (ItemID.FragmentStardust, ProgConst.MagnetFragments) } });
			if (c.ScoriaOreRecipe && scoria > 0)
				list.Add(new Spec { Result = scoria, Stack = ProgConst.ScoriaYield, Tile = TileID.AdamantiteForge, Cond = Condition.DownedGolem,
					Ingredients = new (int, int)[] { (ItemID.Hellstone, ProgConst.ScoriaHellstone), (ItemID.ChlorophyteOre, ProgConst.ScoriaChlorophyte) } });
			int perennial = CalamityLink.ItemType("PerennialBar");
			if (c.SlimeAgitatorRecipe && perennial > 0)
				list.Add(new Spec { Result = ModContent.ItemType<SlimeAgitator>(), Stack = 1, Tile = TileID.MythrilAnvil, Cond = Condition.DownedPlantera,
					Ingredients = new (int, int)[] { (ItemID.Gel, ProgConst.AgitatorGel), (perennial, ProgConst.AgitatorPerennial) } });
			return list;
		}
	}

	/// <summary>
	/// Ore generation multiplier and the Aerialite / Astral extra rounds. Calamity generates each ore through <c>CalamityUtils.SpawnOre(tile, frequency, ...)</c> when its boss first dies; the multiplier scales
	/// <c>frequency</c> for Calamity's own ore tiles (never vanilla's). Aerialite is enchanted by <c>AerialiteOreGen.Enchant()</c> after the Hive Mind or Perforator, and an Astral meteor lands through
	/// <c>AstralBiome.PlaceAstralMeteor()</c> after Astrum Aureus: each gets extra rounds of Calamity's own generator (Aerialite: Generate then Enchant; Astral: another meteor, past Calamity's ore cap).
	/// Auric Ore's regeneration (a repeat Yharon kill) calls the same SpawnOre with Yharon's own numbers. Scoria Ore is only made while a world is created, so it has no event to scale.
	/// </summary>
	public class CalamityOreGen : ModSystem
	{
		delegate void SpawnOreOrig(int type, double frequency, float vs, float ve, int smin, int smax, int[] convertible);
		delegate void SpawnOreHook(SpawnOreOrig orig, int type, double frequency, float vs, float ve, int smin, int smax, int[] convertible);
		delegate void VoidOrig();
		delegate void VoidHook(VoidOrig orig);
		delegate bool BoolOrig();
		delegate bool BoolHook(BoolOrig orig);

		public static bool Installed;
		public static double LastFrequency; public static int LastType;
		public static int ScaledCalls, ExtraAerialiteRounds, ExtraMeteors;
		static bool _inExtra;
		static MethodInfo _generate, _enchant, _place;

		public override void PostSetupContent()
		{
			Installed = false;
			if (!CalamityLink.Loaded) return;
			try
			{
				var spawn = CalamityLink.Method("CalamityMod.CalamityUtils", "SpawnOre", typeof(int), typeof(double), typeof(float), typeof(float), typeof(int), typeof(int), typeof(int[]));
				_generate = CalamityLink.Method("CalamityMod.World.AerialiteOreGen", "Generate");
				_enchant = CalamityLink.Method("CalamityMod.World.AerialiteOreGen", "Enchant");
				_place = CalamityLink.Method("CalamityMod.World.AstralBiome", "PlaceAstralMeteor");
				var canMeteor = CalamityLink.Method("CalamityMod.World.AstralBiome", "CanAstralMeteorSpawn");
				if (spawn != null) { MonoModHooks.Add(spawn, (SpawnOreHook)SpawnOreDetour); Installed = true; }
				if (_enchant != null) MonoModHooks.Add(_enchant, (VoidHook)EnchantDetour);
				if (_place != null) MonoModHooks.Add(_place, (VoidHook)PlaceDetour);
				if (canMeteor != null) MonoModHooks.Add(canMeteor, (BoolHook)CanMeteorDetour);
				if (spawn == null || _enchant == null || _place == null || canMeteor == null) Mod.Logger.Warn("Levy: a Calamity ore generator was not found; the matching progression option stays off");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: Calamity ore hooks failed: " + e.Message); }
		}

		/// <summary>Extra rounds for a multiplier: its whole part minus one, plus one more with the fractional chance.</summary>
		public static int ExtraRounds(float mult, double roll)
		{
			if (mult <= 1f) return 0;
			double extra = mult - 1.0; int whole = (int)extra;
			return whole + (roll < extra - whole ? 1 : 0);
		}

		static void SpawnOreDetour(SpawnOreOrig orig, int type, double frequency, float vs, float ve, int smin, int smax, int[] convertible)
		{ _hl_SpawnOreDetour_1++;
			var c = LevyProgressionConfig.Get;
			LastType = type;
			if (c != null && c.CalamityOreGenMultiplier > 1f && CalamityLink.IsCalamityTile(type)) { frequency *= c.CalamityOreGenMultiplier; ScaledCalls++; }
			LastFrequency = frequency;
			orig(type, frequency, vs, ve, smin, smax, convertible);
		} private static int _hl_SpawnOreDetour_1;

		static void EnchantDetour(VoidOrig orig)
		{ _hl_EnchantDetour_1++;
			orig();
			var c = LevyProgressionConfig.Get;
			if (c == null || c.CalamityOreGenMultiplier <= 1f || _inExtra || _generate == null) return;
			_inExtra = true;
			try { int n = ExtraRounds(c.CalamityOreGenMultiplier, Main.rand.NextDouble()); for (int i = 0; i < n; i++) { _generate.Invoke(null, null); _enchant.Invoke(null, null); ExtraAerialiteRounds++; } }
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: extra Aerialite rounds failed: " + e.Message); }
			finally { _inExtra = false; }
		} private static int _hl_EnchantDetour_1;

		static void PlaceDetour(VoidOrig orig)
		{ _hl_PlaceDetour_1++;
			orig();
			var c = LevyProgressionConfig.Get;
			if (c == null || c.CalamityOreGenMultiplier <= 1f || _inExtra) return;
			_inExtra = true;
			try { int n = ExtraRounds(c.CalamityOreGenMultiplier, Main.rand.NextDouble()); for (int i = 0; i < n; i++) { orig(); ExtraMeteors++; } }
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: extra Astral meteors failed: " + e.Message); }
			finally { _inExtra = false; }
		} private static int _hl_PlaceDetour_1;

		/// <summary>Inside the extra meteors the ore cap is waived: the cap is what the multiplier raises.</summary>
		static bool CanMeteorDetour(BoolOrig orig)
		{ _hl_CanMeteorDetour_1++; return _inExtra || orig(); } private static int _hl_CanMeteorDetour_1;

		public static void Run(MethodInfo m) => m?.Invoke(null, null);
		public static MethodInfo Enchant => _enchant;
		public static MethodInfo Place => _place;
	}

	/// <summary>Auric Ore regeneration: a Yharon kill after the first runs the same ore generation Calamity ran for the first kill, and Yharon's loot is unchanged.</summary>
	public class ProgressionCalamityNpc : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		bool wasDowned;
		public static int Regenerations;

		public static bool IsYharon(NPC n) => n.ModNPC != null && n.ModNPC.Mod.Name == "CalamityMod" && n.ModNPC.Name == "Yharon";

		public override bool PreKill(NPC npc)
		{
			if (IsYharon(npc)) wasDowned = CalamityLink.Downed("downedYharon");
			return true;
		}

		public override void OnKill(NPC npc)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !IsYharon(npc) || !wasDowned) return;
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.AuricOreRegeneration) return;
			if (GenerateAuric()) Announce();
		}

		/// <summary>Calamity's own Auric Ore numbers (frequency 2E-05, rows 0.75 to 0.9 of the world, strength 10 to 20), through its own generator.</summary>
		public static bool GenerateAuric()
		{
			int tile = CalamityLink.TileType("AuricOre"); if (tile <= 0) return false;
			var m = CalamityLink.Method("CalamityMod.CalamityUtils", "SpawnOre", typeof(int), typeof(double), typeof(float), typeof(float), typeof(int), typeof(int), typeof(int[]));
			if (m == null) return false;
			m.Invoke(null, new object[] { tile, 2E-05, 0.75f, 0.9f, 10, 20, Array.Empty<int>() });
			Regenerations++;
			return true;
		}

		static void Announce()
		{
			var t = NetworkText.FromKey("Mods.Levy.Progression.AuricRegen");
			if (Main.netMode == NetmodeID.Server) ChatHelper.BroadcastChatMessage(t, Color.Gold);
			else Main.NewText(t.ToString(), Color.Gold);
		}
	}

	/// <summary>Uelibloom Ore from monsters killed in the Jungle after Providence.</summary>
	public class JungleUelibloomCondition : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info)
		{
			var c = LevyProgressionConfig.Get; var n = info.npc;
			return c != null && c.JungleUelibloomDrop && CalamityLink.Loaded && CalamityLink.Downed("downedProvidence") && Main.hardMode && n != null
				&& ProgressionGlobalNpc.IsPlainEnemy(n) && info.player != null && info.player.ZoneJungle;
		}
		public bool CanShowItemDropInUI() => false;
		public string GetConditionDescription() => Language.GetTextValue("Mods.Levy.Progression.JungleUelibloom");
	}

	public class UelibloomSourceRule : IItemDropRule
	{
		readonly IItemDropRuleCondition cond = new JungleUelibloomCondition();
		public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();
		public bool CanDrop(DropAttemptInfo info) => cond.CanDrop(info);
		public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) { }
		public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
		{
			if (!cond.CanDrop(info)) return new ItemDropAttemptResult { State = ItemDropAttemptResultState.DoesntFillConditions };
			var c = LevyProgressionConfig.Get;
			if (info.rng.NextDouble() * 100.0 >= c.JungleUelibloomChance) return new ItemDropAttemptResult { State = ItemDropAttemptResultState.FailedRandomRoll };
			int type = CalamityLink.ItemType("UelibloomOre"); if (type <= 0) return new ItemDropAttemptResult { State = ItemDropAttemptResultState.DoesntFillConditions };
			CommonCode.DropItem(info, type, info.rng.Next(ProgConst.UelibloomDropMin, ProgConst.UelibloomDropMax + 1));
			return new ItemDropAttemptResult { State = ItemDropAttemptResultState.Success };
		}
	}

	public class ProgressionUelibloomLoot : GlobalNPC
	{
		public override void ModifyGlobalLoot(GlobalLoot globalLoot)
		{
			if (ProgressionLootNpc.Bypass) return;
			globalLoot.Add(new UelibloomSourceRule());
		}
	}
}
