using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Systems;

namespace Levy.Common.Progression
{
	/// <summary>Command points the commander has on active duty. The army-scaled options read this for the player the game hands the drop (bag opener, or the player nearest the kill).</summary>
	public static class ArmyCp
	{
		public static int Of(Player p)
		{
			if (p == null || !p.active) return 0;
			var roster = RosterSystem.Instance;
			if (roster == null) return 0;
			int n = 0;
			foreach (var r in roster.RegularsOf(p)) n += r.CommandPointsHeld;
			return n;
		}
	}

	/// <summary>Extra copies of the item the parent rule just dropped: Luminite, Lunar Fragments, and the army loot bonus on boss materials. Chained to the vanilla rule, so every vanilla rule and condition still decides whether the drop happens at all.</summary>
	public class ExtraAmountRule : IItemDropRule
	{
		public int ItemId;
		public Func<double> Average;
		public bool Army;        // add the army loot bonus
		public bool Luminite, Fragment;
		public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();

		public double Bonus(Player player)
		{
			var c = LevyProgressionConfig.Get;
			if (c == null) return 0;
			double b = 0;
			if (Army) b += ProgMath.ArmyBonus(ArmyCp.Of(player), c.ArmyLootPercentPerCp);
			if (Luminite) b += Math.Max(0f, c.LuminiteDropMultiplier - 1f);
			if (Fragment) b += Math.Max(0f, c.LunarFragmentDropMultiplier - 1f);
			return b;
		}

		public bool CanDrop(DropAttemptInfo info) => true;
		public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) { }
		public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
		{
			double b = Bonus(info.player);
			int n = b > 0 ? ProgMath.RoundStochastic(Average() * b, info.rng.NextDouble()) : 0;
			if (n > 0) CommonCode.DropItem(info, ItemId, n, scattered: true);
			return new ItemDropAttemptResult { State = n > 0 ? ItemDropAttemptResultState.Success : ItemDropAttemptResultState.DoesntFillConditions };
		}
	}

	/// <summary>Second roll for a rare drop that just failed, so the total chance rises by the army bonus (p becomes p x (1 + bonus)).</summary>
	public class RareRerollRule : IItemDropRule
	{
		public int Num, Den, Min = 1, Max = 1;
		public int[] OneOf;      // when set, the parent rolls one of these
		public int ItemId;
		public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();
		public bool CanDrop(DropAttemptInfo info) => true;
		public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) { }
		public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
		{
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.ArmyLootRareChance) return new ItemDropAttemptResult { State = ItemDropAttemptResultState.DoesntFillConditions };
			double q = ProgMath.RareRerollChance(Num, Den, ProgMath.ArmyBonus(ArmyCp.Of(info.player), c.ArmyLootPercentPerCp));
			if (q <= 0 || info.rng.NextDouble() >= q) return new ItemDropAttemptResult { State = ItemDropAttemptResultState.FailedRandomRoll };
			int id = OneOf != null ? OneOf[info.rng.Next(OneOf.Length)] : ItemId;
			CommonCode.DropItem(info, id, info.rng.Next(Min, Max + 1));
			return new ItemDropAttemptResult { State = ItemDropAttemptResultState.Success };
		}
	}

	/// <summary>Attaches the chained bonus rules to the finished drop database (every vanilla and modded boss, treasure bag, Moon Lord and the lunar pillars). Run once after all mods have added their loot.</summary>
	public class ProgressionDropsSystem : ModSystem
	{
		static readonly HashSet<IItemDropRule> chained = new(ReferenceEqualityComparer.Instance);
		static readonly HashSet<IItemDropRule> rerolled = new(ReferenceEqualityComparer.Instance);
		public static int ArmyRules, RareRules, LuminiteRules, FragmentRules;
		static bool done;
		public static readonly int[] Fragments = { ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentNebula, ItemID.FragmentStardust };
		static readonly Dictionary<int, bool> materialCache = new();

		public override void PostSetupRecipes() => Ensure();
		public override void Unload() { chained.Clear(); rerolled.Clear(); materialCache.Clear(); done = false; ArmyRules = RareRules = LuminiteRules = FragmentRules = 0; }

		/// <summary>An ore, bar or crafting material (not weapons, armor, pets, trophies or coins).</summary>
		public static bool IsMaterial(int itemId)
		{
			if (materialCache.TryGetValue(itemId, out bool m)) return m;
			m = false;
			if (itemId > 0 && itemId < ItemLoader.ItemCount && ContentSamples.ItemsByType.TryGetValue(itemId, out Item it))
				m = it.material || (it.createTile >= 0 && TileID.Sets.Ore[it.createTile]);
			return materialCache[itemId] = m;
		}

		public static void Ensure()
		{
			if (done || Main.ItemDropsDB == null || ProgressionLootNpc.Bypass) return;
			done = true;
			var db = Main.ItemDropsDB;
			var list = new List<IItemDropRule>();
			// Pass 1: bosses and treasure bags (army bonus + lunar items). Pass 2: everything else (lunar items only).
			foreach (var kv in ContentSamples.NpcsByNetId)
			{
				var n = kv.Value;
				bool boss = n.boss || (kv.Key > 0 && kv.Key < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[kv.Key]);
				if (!boss) continue;
				list.Clear(); ProgressionLootNpc.Collect(db.GetRulesForNPCID(kv.Key, false), list);
				foreach (var r in list) Attach(r, true);
			}
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				if (!ItemID.Sets.BossBag[t]) continue;
				list.Clear(); ProgressionLootNpc.Collect(db.GetRulesForItemID(t), list);
				foreach (var r in list) Attach(r, true);
			}
			foreach (var kv in ContentSamples.NpcsByNetId)
			{
				list.Clear(); ProgressionLootNpc.Collect(db.GetRulesForNPCID(kv.Key, false), list);
				foreach (var r in list) Attach(r, false);
			}
		}

		static void Attach(IItemDropRule rule, bool boss)
		{
			if (rule is DropOneByOne obo)
			{
				bool frag = Array.IndexOf(Fragments, obo.itemId) >= 0;
				bool army = boss && IsMaterial(obo.itemId);
				if ((frag || army) && chained.Add(rule))
				{
					var p = obo.parameters;
					// Expected items per success: drops x stack per drop, the same arithmetic DropOneByOne uses.
					double Avg() => (p.MinimumItemDropsCount + p.MaximumItemDropsCount) / 2.0
						* ((p.MinimumStackPerChunkBase + p.MaximumStackPerChunkBase) / 2.0 + Main.CurrentFrameFlags.ActivePlayersCount * (p.BonusMinDropsPerChunkPerPlayer + p.BonusMaxDropsPerChunkPerPlayer) / 2.0);
					rule.OnSuccess(new ExtraAmountRule { ItemId = obo.itemId, Army = army, Fragment = frag, Average = Avg }, hideLootReport: true);
					if (frag) FragmentRules++;
					if (army) ArmyRules++;
				}
				return;
			}
			if (rule is CommonDrop cd)
			{
				bool lum = cd.itemId == ItemID.LunarOre;
				bool army = boss && IsMaterial(cd.itemId);
				if ((lum || army) && chained.Add(rule))
				{
					int mn = cd.amountDroppedMinimum, mx = cd.amountDroppedMaximum;
					rule.OnSuccess(new ExtraAmountRule { ItemId = cd.itemId, Army = army, Luminite = lum, Average = () => (mn + mx) / 2.0 }, hideLootReport: true);
					if (lum) LuminiteRules++;
					if (army) ArmyRules++;
				}
				if (boss && cd.chanceNumerator < cd.chanceDenominator && cd.chanceDenominator >= 2 && rerolled.Add(rule))
				{
					rule.OnFailedRoll(new RareRerollRule { Num = cd.chanceNumerator, Den = cd.chanceDenominator, ItemId = cd.itemId, Min = cd.amountDroppedMinimum, Max = cd.amountDroppedMaximum }, hideLootReport: true);
					RareRules++;
				}
			}
			else if (boss && rule is OneFromOptionsDropRule o1 && o1.chanceNumerator < o1.chanceDenominator && o1.chanceDenominator >= 2 && rerolled.Add(rule))
			{
				rule.OnFailedRoll(new RareRerollRule { Num = o1.chanceNumerator, Den = o1.chanceDenominator, OneOf = o1.dropIds }, hideLootReport: true);
				RareRules++;
			}
			else if (boss && rule is OneFromOptionsNotScaledWithLuckDropRule o2 && o2.chanceNumerator < o2.chanceDenominator && o2.chanceDenominator >= 2 && rerolled.Add(rule))
			{
				rule.OnFailedRoll(new RareRerollRule { Num = o2.chanceNumerator, Den = o2.chanceDenominator, OneOf = o2.dropIds }, hideLootReport: true);
				RareRules++;
			}
		}
	}

	/// <summary>Chlorophyte Ore from jungle monsters after Plantera.</summary>
	public class JungleChlorophyteCondition : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info)
		{
			var c = LevyProgressionConfig.Get; var n = info.npc;
			return c != null && c.JungleChlorophyteDrop && NPC.downedPlantBoss && Main.hardMode && n != null && ProgressionGlobalNpc.IsPlainEnemy(n) && info.player != null && info.player.ZoneJungle;
		}
		public bool CanShowItemDropInUI() => false;
		public string GetConditionDescription() => Language.GetTextValue("Mods.Levy.Progression.JungleChlorophyte");
	}

	/// <summary>Chlorophyte Ore from Pumpkin Moon, Frost Moon and Lunar Event enemies.</summary>
	public class EventChlorophyteCondition : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info)
		{
			var c = LevyProgressionConfig.Get; var n = info.npc;
			return c != null && c.EventChlorophyteDrop && n != null && ProgressionGlobalNpc.IsPlainEnemy(n) && ProgressionGlobalNpc.IsEventEnemy(n);
		}
		public bool CanShowItemDropInUI() => false;
		public string GetConditionDescription() => Language.GetTextValue("Mods.Levy.Progression.EventChlorophyte");
	}

	/// <summary>A condition-gated Chlorophyte Ore drop whose chance is read from the config at the moment of the kill.</summary>
	public class ChlorophyteSourceRule : IItemDropRule
	{
		readonly IItemDropRuleCondition cond; readonly bool jungle;
		public ChlorophyteSourceRule(IItemDropRuleCondition cond, bool jungle) { this.cond = cond; this.jungle = jungle; }
		public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();
		public bool CanDrop(DropAttemptInfo info) => cond.CanDrop(info);
		public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) { }
		public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
		{
			if (!cond.CanDrop(info)) return new ItemDropAttemptResult { State = ItemDropAttemptResultState.DoesntFillConditions };
			var c = LevyProgressionConfig.Get;
			float pct = jungle ? c.JungleChlorophyteChance : c.EventChlorophyteChance;
			if (info.rng.NextDouble() * 100.0 >= pct) return new ItemDropAttemptResult { State = ItemDropAttemptResultState.FailedRandomRoll };
			CommonCode.DropItem(info, ItemID.ChlorophyteOre, info.rng.Next(ProgConst.ChlorophyteDropMin, ProgConst.ChlorophyteDropMax + 1));
			return new ItemDropAttemptResult { State = ItemDropAttemptResultState.Success };
		}
	}

	/// <summary>Global NPC hooks: Chlorophyte sources and the Dungeon Spirit spawn multiplier.</summary>
	public class ProgressionGlobalNpc : GlobalNPC
	{
		public static int GlobalRules;
		public static readonly HashSet<int> LunarEventNpcs = new()
		{
			NPCID.SolarCorite, NPCID.SolarSroller, NPCID.SolarCrawltipedeHead, NPCID.SolarDrakomire, NPCID.SolarDrakomireRider, NPCID.SolarSolenian,
			NPCID.VortexRifleman, NPCID.VortexHornetQueen, NPCID.VortexHornet, NPCID.VortexLarva, NPCID.VortexSoldier,
			NPCID.NebulaBrain, NPCID.NebulaHeadcrab, NPCID.NebulaBeast, NPCID.NebulaSoldier,
			NPCID.StardustSoldier, NPCID.StardustSpiderBig, NPCID.StardustJellyfishBig, NPCID.StardustWormHead, NPCID.StardustCellBig,
		};

		/// <summary>A hostile, non-boss monster worth money (not critters, town NPCs or statue spawns).</summary>
		public static bool IsPlainEnemy(NPC n) => !n.boss && !n.friendly && !n.townNPC && n.lifeMax > 5 && (n.value > 0f || LunarEventNpcs.Contains(n.type)) && !n.SpawnedFromStatue && !NPCID.Sets.ShouldBeCountedAsBoss[n.type];
		public static bool IsEventEnemy(NPC n) => Main.pumpkinMoon || Main.snowMoon || LunarEventNpcs.Contains(n.type);

		public override void ModifyGlobalLoot(GlobalLoot globalLoot)
		{
			if (ProgressionLootNpc.Bypass) return;
			globalLoot.Add(new ChlorophyteSourceRule(new JungleChlorophyteCondition(), true));
			globalLoot.Add(new ChlorophyteSourceRule(new EventChlorophyteCondition(), false));
			GlobalRules += 2;
		}

		public override void OnKill(NPC npc)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			var c = LevyProgressionConfig.Get;
			if (c == null || (c.DungeonSpiritMultiplier <= 1f && c.DungeonSpiritPercentPerCp <= 0f)) return;
			if (!(npc.lifeMax > 100 && npc.type != NPCID.DungeonSpirit && npc.value > 0f && Main.hardMode && NPC.downedPlantBoss)) return;
			// Vanilla needs the dying monster to target a player. Monsters killed fighting levies target a soldier, so use the nearest player then.
			bool vanillaRolled = npc.HasPlayerTarget;
			Player tgt = vanillaRolled ? Main.player[npc.target] : Main.player[Player.FindClosest(npc.position, npc.width, npc.height)];
			if (tgt == null || !tgt.active || !tgt.ZoneDungeon) return;
			int tx = (int)npc.Center.X / 16, ty = (int)npc.Center.Y / 16;
			if (!WorldGen.InWorld(tx, ty) || !Main.wallDungeon[Main.tile[tx, ty].WallType]) return;
			var (whole, frac) = ProgMath.SpiritTrials(c.DungeonSpiritMultiplier, c.DungeonSpiritPercentPerCp, ArmyCp.Of(tgt), vanillaRolled);
			int trials = whole + (Main.rand.NextDouble() < frac ? 1 : 0);
			int range = Main.expertMode ? 9 : 13;
			for (int i = 0; i < trials; i++)
				if (tgt.RollLuck(range) == 0)
					NPC.NewNPC(npc.GetSource_Death(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.DungeonSpirit);
		}
	}

	/// <summary>With the config option on, Mud Blocks go into an Extractinator; after any Mechanical Boss each one may yield Chlorophyte Ore.</summary>
	public class ProgressionExtractinator : GlobalItem
	{
		public override void ExtractinatorUse(int extractType, int extractinatorBlockType, ref int resultType, ref int resultStack)
		{
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.ExtractinatorChlorophyte || extractType != ItemID.MudBlock) return;
			bool mech = NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3 || NPC.downedMechBossAny;
			var r = ProgMath.ExtractMud(mech, c.ExtractinatorChlorophytePercent, Main.rand.NextDouble(), Main.rand.Next(ProgConst.ChlorophyteDropMin, ProgConst.ChlorophyteDropMax + 1));
			resultType = r.item; resultStack = r.stack;
		}
	}

	public class ProgressionExtractinatorSystem : ModSystem
	{
		public static bool Registered;
		public override void PostSetupContent()
		{
			Registered = false;
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.ExtractinatorChlorophyte) return;
			if (ItemID.Sets.ExtractinatorMode[ItemID.MudBlock] != -1) return;   // someone else already made mud extractable
			ItemID.Sets.ExtractinatorMode[ItemID.MudBlock] = ItemID.MudBlock;
			Registered = true;
		}
	}
}
