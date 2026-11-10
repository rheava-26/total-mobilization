using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.Buffs;
using Levy.Content.Items.Provisions;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Provisions
{
	/// <summary>
	/// Provisions: the food recipe groups and meal recipes, the Magma Shot recipe (copied from the vanilla Obsidian Skin Potion), the Armory's mess call, and the boot self-check.
	/// Food is found by what it does, not by name: any item that is food (ItemID.Sets.IsFood) or a consumable that gives Well Fed / Plenty Satisfied / Exquisitely Stuffed joins the group of its tier, so a mod's dishes feed the army too.
	/// </summary>
	public class ProvisionsSystem : ModSystem
	{
		public const string FishGroup = "Levy:ProvisionFish", Tier1Group = "Levy:ProvisionWellFed", Tier2Group = "Levy:ProvisionPlenty", Tier3Group = "Levy:ProvisionStuffed";
		/// <summary>Sizes of the four groups after they were built (fish, Well Fed, Plenty Satisfied, Exquisitely Stuffed).</summary>
		public static readonly int[] GroupSizes = new int[4];

		private static readonly int[] RawFish =
		{
			ItemID.Bass, ItemID.Trout, ItemID.Salmon, ItemID.AtlanticCod, ItemID.Tuna, ItemID.RedSnapper, ItemID.NeonTetra, ItemID.ArmoredCavefish, ItemID.SpecularFish, ItemID.Flounder,
			ItemID.CookedFish, ItemID.Sashimi,
		};

		/// <summary>The Well Fed tier an item gives (1, 2, 3), or 0 when it is not food.</summary>
		public static int FoodTier(Item it)
		{
			if (it == null || it.IsAir) return 0;
			if (!it.consumable && !(it.type < ItemID.Sets.IsFood.Length && ItemID.Sets.IsFood[it.type])) return 0;
			if (it.buffType == BuffID.WellFed) return 1;
			if (it.buffType == BuffID.WellFed2) return 2;
			if (it.buffType == BuffID.WellFed3) return 3;
			return 0;
		}

		public override void AddRecipeGroups()
		{
			var t1 = new List<int>(); var t2 = new List<int>(); var t3 = new List<int>();
			int count = ItemLoader.ItemCount;
			for (int type = 1; type < count; type++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(type, out Item it)) continue;
				int tier = FoodTier(it);
				if (tier == 1) t1.Add(type); else if (tier == 2) t2.Add(type); else if (tier == 3) t3.Add(type);
			}
			void Reg(string key, Func<string> name, List<int> ids, int slot)
			{
				if (ids.Count == 0) { GroupSizes[slot] = 0; return; }
				RecipeGroup.RegisterGroup(key, new RecipeGroup(name, ids.ToArray()));
				GroupSizes[slot] = ids.Count;
			}
			Reg(FishGroup, () => LevyUtil.T("Provisions.GroupFish"), new List<int>(RawFish), 0);
			Reg(Tier1Group, () => LevyUtil.T("Provisions.GroupWellFed"), t1, 1);
			Reg(Tier2Group, () => LevyUtil.T("Provisions.GroupPlenty"), t2, 2);
			Reg(Tier3Group, () => LevyUtil.T("Provisions.GroupStuffed"), t3, 3);
		}

		public override void AddRecipes()
		{
			Mod.Logger.Info($"[Provisions] food groups: fish={GroupSizes[0]} wellfed={GroupSizes[1]} plenty={GroupSizes[2]} stuffed={GroupSizes[3]}");

			// meals (a Cooking Pot)
			Recipe.Create(ModContent.ItemType<FishFryPlatter>()).AddRecipeGroup(FishGroup, LevyConstants.FishFryFish).AddTile(TileID.CookingPots).Register();
			Recipe.Create(ModContent.ItemType<FruitBasket>()).AddRecipeGroup(Tier1Group, LevyConstants.FruitBasketFruit).AddRecipeGroup(RecipeGroupID.Wood, 4).AddTile(TileID.CookingPots).Register();
			Recipe.Create(ModContent.ItemType<BurgerBox>()).AddRecipeGroup(Tier2Group, LevyConstants.BurgerBoxDishes).AddTile(TileID.CookingPots).Register();
			Recipe.Create(ModContent.ItemType<GrandFeast>()).AddIngredient(ModContent.ItemType<FishFryPlatter>()).AddIngredient(ModContent.ItemType<FruitBasket>()).AddIngredient(ModContent.ItemType<BurgerBox>())
				.AddRecipeGroup(Tier3Group, LevyConstants.FeastDishes).AddTile(TileID.CookingPots).Register();

			// Magma Shot: the Obsidian Skin Potion's own ingredients and stations, a batch of shots instead of one bottle
			Recipe source = null;
			foreach (Recipe r in Main.recipe) if (r != null && r.createItem != null && r.createItem.type == ItemID.ObsidianSkinPotion) { source = r; break; }
			Recipe shot = Recipe.Create(ModContent.ItemType<MagmaShot>(), LevyConstants.MagmaShotsPerBrew);
			if (source != null)
			{
				foreach (Item ing in source.requiredItem) shot.AddIngredient(ing.type, ing.stack);
				foreach (int tile in source.requiredTile) shot.AddTile(tile);
			}
			else shot.AddIngredient(ItemID.BottledWater).AddIngredient(ItemID.Daybloom).AddIngredient(ItemID.Obsidian).AddIngredient(ItemID.Fireblossom).AddTile(TileID.Bottles);
			shot.Register();
		}

		// ------------------------------------------------------------------------------------------------------
		// The Armory's mess call
		// ------------------------------------------------------------------------------------------------------
		private static readonly Dictionary<int, uint> LastServed = new();

		public override void OnWorldLoad() => LastServed.Clear();
		public override void OnWorldUnload() => LastServed.Clear();

		/// <summary>An Armory serves a meal when at least half of the levies around it are not fed to that meal's tier yet.</summary>
		public static bool ShouldServe(int near, int unfed) => near > 0 && unfed * 2 >= near;

		/// <summary>Slot of the meal with the most servings in storage, or -1.</summary>
		public static int BestMealSlot(Item[] storage)
		{
			int best = -1, bestFeeds = -1;
			if (storage == null) return -1;
			for (int i = 0; i < storage.Length; i++)
			{
				Item it = storage[i];
				if (it == null || it.IsAir || it.ModItem is not MealItem m) continue;
				int feeds = LevyConstants.MealFeeds[m.Meal];
				if (feeds > bestFeeds) { bestFeeds = feeds; best = i; }
			}
			return best;
		}

		public override void PostUpdateEverything()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || Main.gameMenu || RosterSystem.Instance == null) return;
			uint now = Main.GameUpdateCount;
			if (now % (uint)LevyConstants.MealMessCheckTicks != 0) return;
			bool boss = false;
			try { boss = EnemyTargeting.FindUndownedBoss() != null; } catch (Exception) { }
			if (!boss) return;
			foreach (TileEntity te in TileEntity.ByID.Values)
				if (te is ArmoryTE a) { try { Mess(a, now); } catch (Exception e) { Mod.Logger.Warn("[Provisions] mess call failed: " + e.Message); } }
		}

		private void Mess(ArmoryTE a, uint now)
		{
			if (LastServed.TryGetValue(a.ID, out uint at) && now - at < (uint)LevyConstants.MealMessCooldownTicks) return;
			int slot = BestMealSlot(a.Storage);
			if (slot < 0) return;
			var meal = (MealItem)a.Storage[slot].ModItem;
			int tier = LevyConstants.MealWellFedTier[meal.Meal], near = 0, unfed = 0;
			float reach = LevyConstants.ResupplyArmoryRadius * LevyConstants.TilePx;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive || Vector2.Distance(s.NPC.Center, a.Center) > reach) continue;
				near++;
				if (ProvisionRules.HeldTier(s.NPC) < tier) unfed++;
			}
			if (!ShouldServe(near, unfed)) return;
			ProvisionRules.ServeMeal(a.Center, meal.Meal, null);
			a.Storage[slot].stack--;
			if (a.Storage[slot].stack <= 0) a.Storage[slot].TurnToAir();
			a.MarkDirty();
			LastServed[a.ID] = now;
		}

		// ------------------------------------------------------------------------------------------------------
		// Boot self-check
		// ------------------------------------------------------------------------------------------------------
		public override void PostAddRecipes() => LoadSafe.Run(SelfCheck);

		private void SelfCheck()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guard(string name, Action body) { try { body(); } catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); } }
			int Recipes(int result) { int n = 0; foreach (Recipe r in Main.recipe) if (r != null && r.createItem != null && r.createItem.type == result) n++; return n; }
			string info = "";

			Guard("recipes", () =>
			{
				foreach (int t in new[] { ModContent.ItemType<FishFryPlatter>(), ModContent.ItemType<FruitBasket>(), ModContent.ItemType<BurgerBox>(), ModContent.ItemType<GrandFeast>(),
					ModContent.ItemType<FizzPop>(), ModContent.ItemType<FizzTank>(), ModContent.ItemType<MagmaShot>(), ModContent.ItemType<MagmaCrucible>() })
					Check(Recipes(t) == 1, $"{ContentSamples.ItemsByType[t].Name} has exactly one recipe (has {Recipes(t)})");
				for (int g = 0; g < 4; g++) Check(GroupSizes[g] > 0, $"food group {g} is not empty");
				info += $"groups fish={GroupSizes[0]} wellfed={GroupSizes[1]} plenty={GroupSizes[2]} stuffed={GroupSizes[3]}";
				Check(RecipeGroup.recipeGroupIDs.ContainsKey(Tier3Group), "the Exquisitely Stuffed group is registered");
				// a real vanilla dish lands in its tier's group
				Check(FoodTier(new Item(ItemID.CookedFish)) == 1 && FoodTier(new Item(ItemID.Burger)) > 0, "vanilla food is recognised by its Well Fed buff");
				Check(FoodTier(new Item(ItemID.Bass)) == 0 && FoodTier(new Item(ItemID.IronBar)) == 0, "raw fish and bars are not food");
				// the Magma Shot batch uses the Obsidian Skin Potion's ingredients
				Recipe src = null, mine = null;
				foreach (Recipe r in Main.recipe) { if (r == null || r.createItem == null) continue; if (r.createItem.type == ItemID.ObsidianSkinPotion && src == null) src = r; if (r.createItem.type == ModContent.ItemType<MagmaShot>()) mine = r; }
				bool same = src != null && mine != null && src.requiredItem.Count == mine.requiredItem.Count;
				if (same) for (int i = 0; i < src.requiredItem.Count; i++) same &= src.requiredItem[i].type == mine.requiredItem[i].type && src.requiredItem[i].stack == mine.requiredItem[i].stack;
				Check(same, "Magma Shot is brewed from the Obsidian Skin Potion's ingredients");
				Check(mine != null && mine.createItem.stack == LevyConstants.MagmaShotsPerBrew, "a Magma Shot brew makes " + LevyConstants.MagmaShotsPerBrew);
			});

			Guard("items", () =>
			{
				Check(ContentSamples.ItemsByType[ModContent.ItemType<FizzPop>()].buffType == ModContent.BuffType<FizzyBuff>(), "Fizz Pop gives Fizzy");
				Check(ContentSamples.ItemsByType[ModContent.ItemType<MagmaShot>()].buffType == ModContent.BuffType<LavaShotBuff>(), "Magma Shot gives Lava Shot");
				for (int m = 0; m < LevyConstants.MealFeeds.Length; m++) Check(LevyConstants.MealFeeds[m] > 0 && LevyConstants.MealWellFedTier[m] is >= 1 and <= 3, $"meal {m} has a feed count and a tier");
				Check(LevyConstants.MealFeeds[3] >= 50, "the Grand Feast feeds a full army");
				// every tooltip resolves to real text with its numbers filled in
				foreach (int t in new[] { ModContent.ItemType<FishFryPlatter>(), ModContent.ItemType<FruitBasket>(), ModContent.ItemType<BurgerBox>(), ModContent.ItemType<GrandFeast>(),
					ModContent.ItemType<FizzPop>(), ModContent.ItemType<FizzTank>(), ModContent.ItemType<MagmaShot>(), ModContent.ItemType<MagmaCrucible>() })
				{
					ModItem mi = ModContent.GetModItem(t);
					string tip = mi.Tooltip.Value, name = mi.DisplayName.Value;
					Check(!tip.Contains("Mods.Levy") && !tip.Contains("{") && tip.Length > 20 && !name.Contains("Mods.Levy"), $"{mi.Name} has a resolved name and tooltip");
					if (t == ModContent.ItemType<GrandFeast>()) info += " | " + tip;
				}
				string bt = ModContent.GetInstance<FizzyBuff>().Description.Value + ModContent.GetInstance<LavaShotBuff>().Description.Value;
				Check(!bt.Contains("Mods.Levy") && bt.Length > 30, "both buffs have a description");
			});

			Guard("buffs", () =>
			{
				var d = new Player(); d.statDefense = new Player.DefenseStat();
				d.statDefense += 20;
				int def0 = d.statDefense; float jb0 = d.jumpSpeedBoost;
				Check(DrinkBuffs.Apply(d, ModContent.BuffType<FizzyBuff>()), "Fizzy is claimed as a levy buff");
				Check(d.statDefense == def0 - LevyConstants.FizzDefense, $"Fizzy lowers defense by {LevyConstants.FizzDefense} ({def0} -> {(int)d.statDefense})");
				Check(d.GetJumpState(ExtraJump.CloudInABottle).Enabled, "Fizzy gives a double jump");
				Check(d.jumpBoost && d.frogLegJumpBoost, "Fizzy raises the jump (balloon + frog leg)");
				var ab = Common.Movement.MovementAbilities.FromDummy(d);
				Check(ab.ExtraJumps >= 1 && ab.JumpTiles >= Common.LevyConstants.Movement.BaseJumpTiles + ProvisionRules.FizzJumpTiles - 1, $"a Fizzy levy reads {ab.ExtraJumps} extra jump and {ab.JumpTiles} jump tiles (+{ProvisionRules.FizzJumpTiles})");
				Check(DrinkBuffs.Apply(new Player { statDefense = new Player.DefenseStat() }, ModContent.BuffType<LavaShotBuff>()), "Lava Shot is claimed as a levy buff");
				Check(!DrinkBuffs.IsDrinkBuff(ModContent.BuffType<FizzyBuff>()) && !DrinkBuffs.IsDrinkBuff(ModContent.BuffType<LavaShotBuff>()), "Fizzy and Lava Shot do not make a levy Tipsy");

				var n = new NPC(); n.SetDefaults(ModContent.NPCType<SoldierNPC>());
				var body = n.ModNPC as SoldierNPC;
				Check(body != null && n.TryGetGlobalNPC(out ProvisionsBody pb), "a soldier body carries the Provisions state");
				if (body != null && n.TryGetGlobalNPC(out ProvisionsBody g))
				{
					n.active = true;
					g.Refresh(n);
					Check(!n.lavaImmune && n.knockBackResist == 0f, "a levy with no drink is not lava immune and is not knocked back");
					n.AddBuff(ModContent.BuffType<LavaShotBuff>(), 600); n.AddBuff(ModContent.BuffType<FizzyBuff>(), 600);
					bool kept = n.HasBuff(ModContent.BuffType<LavaShotBuff>());
					if (!kept) { n.buffType[0] = ModContent.BuffType<LavaShotBuff>(); n.buffTime[0] = 600; n.buffType[1] = ModContent.BuffType<FizzyBuff>(); n.buffTime[1] = 600; }
					g.Refresh(n);
					Check(n.lavaImmune && n.buffImmune[BuffID.OnFire], $"a Lava Shot levy is lava and burning immune (AddBuff kept it: {kept})");
					Check(Math.Abs(n.knockBackResist - LevyConstants.FizzKnockbackResist) < 1e-4f, "a Fizzy levy takes knockback");
					for (int i = 0; i < n.buffType.Length; i++) { n.buffType[i] = 0; n.buffTime[i] = 0; }
					g.Refresh(n);
					Check(!n.lavaImmune && n.knockBackResist == 0f, "both wear off together");
					// a meal: Well Fed ladder
					n.buffType[0] = BuffID.WellFed2; n.buffTime[0] = 600;
					Check(ProvisionRules.HeldTier(n) == 2, "the held Well Fed tier is read");
					ProvisionRules.Feed(body, 1);
					Check(ProvisionRules.HeldTier(n) == 2, "a lesser meal does not replace a better one");
					ProvisionRules.Feed(body, 3);
					Check(ProvisionRules.HeldTier(n) == 3 && !n.HasBuff(BuffID.WellFed2), "a better meal replaces a lesser one");
					Check(n.HasBuff(ModContent.BuffType<MoraleBuff>()), "a meal lifts morale");
					for (int i = 0; i < n.buffType.Length; i++) { n.buffType[i] = 0; n.buffTime[i] = 0; }
				}
			});

			Guard("feeding", () =>
			{
				var c = new Vector2(2000, 2000);
				float r = LevyConstants.DrinkRadiusTiles * 16f;
				var pts = new List<Vector2>();
				for (int i = 0; i < 80; i++) pts.Add(c + new Vector2(8f + i * 8f, 0f)); // 8, 16, ... 640 px away; the radius is 960 px, so all 80 are in reach
				pts.Add(c + new Vector2(r + 16f, 0f)); pts.Add(c - new Vector2(r + 32f, 0f)); // two beyond the radius
				var pick = ProvisionRules.NearestInReach(pts, c, 10);
				Check(pick.Count == 10 && pick[0] == 0 && pick[9] == 9, "a 10-meal feeds the ten nearest levies");
				var all = ProvisionRules.NearestInReach(pts, c, 500);
				Check(all.Count == 80 && !all.Contains(80) && !all.Contains(81), "no meal reaches beyond the drink radius");
				var few = ProvisionRules.NearestInReach(pts.GetRange(0, 3), c, 25);
				Check(few.Count == 3, "a big meal feeds only the levies that are there");
				Check(ShouldServe(10, 5) && ShouldServe(10, 10) && !ShouldServe(10, 4) && !ShouldServe(0, 0), "the Armory serves when half the levies around it are unfed");
				var stock = new Item[3]; for (int i = 0; i < 3; i++) stock[i] = new Item();
				stock[0].SetDefaults(ModContent.ItemType<FishFryPlatter>()); stock[2].SetDefaults(ModContent.ItemType<GrandFeast>());
				Check(BestMealSlot(stock) == 2 && BestMealSlot(new[] { new Item() }) == -1, "the Armory picks its biggest meal");
			});

			if (failures.Count == 0) Mod.Logger.Info($"[Provisions] self-check {ok} checks ok fail=0 ({info})");
			else
			{
				Mod.Logger.Warn($"[Provisions] self-check {ok} checks ok fail={failures.Count} ({info})");
				foreach (string f in failures) Mod.Logger.Warn("[Provisions] self-check FAILED: " + f);
			}
		}
	}
}
