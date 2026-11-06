using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Levy.Common.Progression
{
	/// <summary>Drops only after Plantera has already been beaten, outside Expert (the bag covers Expert). Used for the repeat Grenade Launcher.</summary>
	public class PlanteraRepeatKill : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info) => NPC.downedPlantBoss && !Main.expertMode;
		public bool CanShowItemDropInUI() => true;
		public string GetConditionDescription() => Language.GetTextValue("Mods.Levy.Progression.PlanteraRepeat");
	}

	/// <summary>
	/// Loot side of progression assistance. Loot tables are built once while the mod loads, so these options need a reload.
	/// Rule objects in the vanilla database are shared between NPCs (one rule registered to many NPCs), so every rule is scaled once.
	/// </summary>
	public class ProgressionLootNpc : GlobalNPC
	{
		static readonly HashSet<IItemDropRule> seen = new(ReferenceEqualityComparer.Instance);
		public static int ScaledRules, ScaledCrateRules;
		/// <summary>Set by the boot self-check while it builds a reference (vanilla) drop database.</summary>
		public static bool Bypass;

		/// <summary>Drop multiplier for one item on one kind of NPC: mob drops x item-specific.</summary>
		public static float Factor(int itemId, bool boss, LevyProgressionConfig c)
		{
			float f = 1f;
			if (!boss) f *= c.MobDropMultiplier;
			if (itemId == ItemID.Ectoplasm) f *= c.EctoplasmDropMultiplier;
			if (itemId == ItemID.SoulofLight || itemId == ItemID.SoulofNight || itemId == ItemID.SoulofFlight) f *= c.SoulDropMultiplier;
			return f;
		}

		public override void ModifyGlobalLoot(GlobalLoot globalLoot)
		{
			if (Bypass) return;
			seen.Clear();
			ScaledRules = ScaledCrateRules = 0;
			var c = LevyProgressionConfig.Get;
			if (c == null || (c.MobDropMultiplier <= 1f && c.SoulDropMultiplier <= 1f && c.EctoplasmDropMultiplier <= 1f)) return;
			foreach (var rule in globalLoot.Get()) Walk(rule, r => ScaleRule(r, false, c));
		}

		public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
		{
			var c = LevyProgressionConfig.Get;
			if (c == null || Bypass) return;
			if (c.MobDropMultiplier > 1f || c.SoulDropMultiplier > 1f || c.EctoplasmDropMultiplier > 1f)
			{
				bool boss = npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type];
				foreach (var rule in npcLoot.Get(includeGlobalDrops: false)) Walk(rule, r => ScaleRule(r, boss, c));
			}
			if (npc.type == NPCID.Plantera && c.OnceOnlyBossWeaponRepeatPercent > 0)
			{
				var rule = new ItemDropWithConditionRule(ItemID.GrenadeLauncher, 100, 1, 1, new PlanteraRepeatKill(), c.OnceOnlyBossWeaponRepeatPercent);
				rule.OnSuccess(ItemDropRule.Common(ItemID.RocketI, 1, ProgConst.PlanteraRocketMin, ProgConst.PlanteraRocketMax), hideLootReport: true);
				npcLoot.Add(rule);
			}
		}

		static void ScaleRule(IItemDropRule rule, bool boss, LevyProgressionConfig c)
		{
			if (rule is OneFromOptionsDropRule o1)
			{
				float f = o1.dropIds.Length == 1 ? Factor(o1.dropIds[0], boss, c) : (boss ? 1f : c.MobDropMultiplier);
				var s = ProgMath.ScaleChance(o1.chanceNumerator, o1.chanceDenominator, f);
				if (Apply(o1, s.num, s.den)) { o1.chanceNumerator = s.num; o1.chanceDenominator = s.den; ScaledRules++; }
			}
			else if (rule is OneFromOptionsNotScaledWithLuckDropRule o2)
			{
				float f = o2.dropIds.Length == 1 ? Factor(o2.dropIds[0], boss, c) : (boss ? 1f : c.MobDropMultiplier);
				var s = ProgMath.ScaleChance(o2.chanceNumerator, o2.chanceDenominator, f);
				if (Apply(o2, s.num, s.den)) { o2.chanceNumerator = s.num; o2.chanceDenominator = s.den; ScaledRules++; }
			}
			else if (rule is CommonDrop cd)
			{
				float f = Factor(cd.itemId, boss, c);
				if (f <= 1f) return;
				var s = ProgMath.ScaleDrop(cd.chanceNumerator, cd.chanceDenominator, cd.amountDroppedMinimum, cd.amountDroppedMaximum, f);
				cd.chanceNumerator = s.num; cd.chanceDenominator = s.den; cd.amountDroppedMinimum = s.min; cd.amountDroppedMaximum = s.max;
				ScaledRules++;
			}
		}

		// OneFromOptions rules cannot hold a chance above 1: only apply when the scaled chance fits.
		static bool Apply(IItemDropRule rule, int num, int den) => num > 0 && den > 0 && num <= den;

		/// <summary>Visit a rule and everything nested in or chained to it, once per rule object.</summary>
		public static void Walk(IItemDropRule rule, Action<IItemDropRule> visit)
		{
			if (rule == null || !seen.Add(rule)) return;
			visit(rule);
			switch (rule)
			{
				case OneFromRulesRule a: foreach (var r in a.options) Walk(r, visit); break;
				case SequentialRulesRule b: foreach (var r in b.rules) Walk(r, visit); break;
				case SequentialRulesNotScalingWithLuckRule d: foreach (var r in d.rules) Walk(r, visit); break;
				case AlwaysAtleastOneSuccessDropRule e: foreach (var r in e.rules) Walk(r, visit); break;
				case FewFromRulesRule f: foreach (var r in f.options) Walk(r, visit); break;
				case DropBasedOnExpertMode g: Walk(g.ruleForNormalMode, visit); Walk(g.ruleForExpertMode, visit); break;
				case DropBasedOnMasterAndExpertMode h: Walk(h.ruleForDefault, visit); Walk(h.ruleForExpertmode, visit); Walk(h.ruleForMasterMode, visit); break;
				case DropBasedOnMasterMode i: Walk(i.ruleForDefault, visit); Walk(i.ruleForMasterMode, visit); break;
			}
			if (rule.ChainedRules != null)
				foreach (var chain in rule.ChainedRules) Walk(chain.RuleToChain, visit);
		}

		/// <summary>Every rule reachable from a root list, without touching the shared "seen" set (for tests and logging).</summary>
		public static void Collect(IEnumerable<IItemDropRule> roots, List<IItemDropRule> into)
		{
			var local = new HashSet<IItemDropRule>(ReferenceEqualityComparer.Instance);
			void Go(IItemDropRule rule)
			{
				if (rule == null || !local.Add(rule)) return;
				into.Add(rule);
				switch (rule)
				{
					case OneFromRulesRule a: foreach (var r in a.options) Go(r); break;
					case SequentialRulesRule b: foreach (var r in b.rules) Go(r); break;
					case SequentialRulesNotScalingWithLuckRule d: foreach (var r in d.rules) Go(r); break;
					case AlwaysAtleastOneSuccessDropRule e: foreach (var r in e.rules) Go(r); break;
					case FewFromRulesRule f: foreach (var r in f.options) Go(r); break;
					case DropBasedOnExpertMode g: Go(g.ruleForNormalMode); Go(g.ruleForExpertMode); break;
					case DropBasedOnMasterAndExpertMode h: Go(h.ruleForDefault); Go(h.ruleForExpertmode); Go(h.ruleForMasterMode); break;
					case DropBasedOnMasterMode i: Go(i.ruleForDefault); Go(i.ruleForMasterMode); break;
				}
				if (rule.ChainedRules != null) foreach (var ch in rule.ChainedRules) Go(ch.RuleToChain);
			}
			foreach (var r in roots) Go(r);
		}
	}

	/// <summary>Crate ore amounts and the repeat Grenade Launcher in Plantera's Treasure Bag.</summary>
	public class ProgressionLootItem : GlobalItem
	{
		public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
		{
			var c = LevyProgressionConfig.Get;
			if (c == null || ProgressionLootNpc.Bypass) return;

			if (c.EarlyHardmodeCrateOreMultiplier > 1 && ItemID.Sets.IsFishingCrate[item.type])
			{
				foreach (var root in itemLoot.Get(includeGlobalDrops: false))
					ProgressionLootNpc.Walk(root, r =>
					{
						if (r is CommonDrop cd && Array.IndexOf(ProgConst.EarlyHardmodeCrateOres, cd.itemId) >= 0)
						{
							cd.amountDroppedMinimum *= c.EarlyHardmodeCrateOreMultiplier;
							cd.amountDroppedMaximum *= c.EarlyHardmodeCrateOreMultiplier;
							ProgressionLootNpc.ScaledCrateRules++;
						}
					});
			}

			if (item.type == ItemID.PlanteraBossBag && c.OnceOnlyBossWeaponRepeatPercent > 0)
			{
				var rule = new CommonDrop(ItemID.GrenadeLauncher, 100, 1, 1, c.OnceOnlyBossWeaponRepeatPercent);
				rule.OnSuccess(ItemDropRule.Common(ItemID.RocketI, 1, ProgConst.PlanteraRocketMin, ProgConst.PlanteraRocketMax), hideLootReport: true);
				itemLoot.Add(rule);
			}
		}
	}
}
