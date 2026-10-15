using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Items.Books;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Flavor
{
	// ------------------------------------------------------------------ drop conditions

	/// <summary>
	/// Base for the event-book drop conditions. A book never drops from statue spawns, friendly NPCs, or once that book has been
	/// studied in this world. The description is shown with the drop in the Bestiary.
	/// </summary>
	public abstract class EventBookCondition : IItemDropRuleCondition
	{
		private readonly string _bookKey;
		private readonly string _descriptionKey;

		protected EventBookCondition(string bookKey, string descriptionKey)
		{
			_bookKey = bookKey;
			_descriptionKey = descriptionKey;
		}

		protected abstract bool EventApplies(NPC npc);

		public bool CanDrop(DropAttemptInfo info)
		{
			NPC npc = info.npc;
			if (npc == null || info.IsInSimulation || npc.SpawnedFromStatue || npc.friendly || npc.townNPC) return false;
			if (FlavorSystem.IsStudied(_bookKey)) return false;
			return EventApplies(npc);
		}

		public bool CanShowItemDropInUI() => true;

		public string GetConditionDescription() => LevyUtil.T("Flavor.Drop." + _descriptionKey);
	}

	/// <summary>Any hostile, non-boss, non-critter enemy during a Blood Moon night.</summary>
	public sealed class BloodMoonBookCondition : EventBookCondition
	{
		public BloodMoonBookCondition() : base("Conscription", "BloodMoon") { }

		protected override bool EventApplies(NPC npc) =>
			Main.bloodMoon && !Main.dayTime && !npc.boss && npc.lifeMax > 5 && !npc.dontCountMe && !NPCID.Sets.CountsAsCritter[npc.type];
	}

	/// <summary>A Goblin Army member while the Goblin Army invasion is running.</summary>
	public sealed class GoblinArmyBookCondition : EventBookCondition
	{
		public GoblinArmyBookCondition() : base("Drill", "GoblinArmy") { }

		protected override bool EventApplies(NPC npc) =>
			Main.invasionType == InvasionID.GoblinArmy && NPCID.Sets.BelongsToInvasionGoblinArmy[npc.type];
	}

	/// <summary>A pirate (or the Pirate Ship) while the Pirate Invasion is running.</summary>
	public sealed class PirateInvasionBookCondition : EventBookCondition
	{
		public PirateInvasionBookCondition() : base("Plunder", "Pirates") { }

		protected override bool EventApplies(NPC npc) =>
			Main.invasionType == InvasionID.PirateInvasion && NPCID.Sets.BelongsToInvasionPirate[npc.type];
	}

	/// <summary>An Old One's Army enemy while the event is running (not the Eternia Crystal or the lane portals).</summary>
	public sealed class OldOnesArmyBookCondition : EventBookCondition
	{
		public OldOnesArmyBookCondition() : base("Fortification", "OldOnesArmy") { }

		protected override bool EventApplies(NPC npc) =>
			DD2Event.Ongoing && NPCID.Sets.BelongsToInvasionOldOnesArmy[npc.type] && npc.type != NPCID.DD2EterniaCrystal && npc.type != NPCID.DD2LanePortal;
	}

	/// <summary>One of the Solar Eclipse monsters while the eclipse lasts.</summary>
	public sealed class EclipseBookCondition : EventBookCondition
	{
		private static readonly HashSet<int> EclipseTypes = new()
		{
			NPCID.Eyezor, NPCID.Frankenstein, NPCID.SwampThing, NPCID.Vampire, NPCID.VampireBat, NPCID.CreatureFromTheDeep,
			NPCID.Fritz, NPCID.Nailhead, NPCID.Reaper, NPCID.Mothron, NPCID.Butcher, NPCID.DeadlySphere,
			NPCID.DrManFly, NPCID.Psycho, NPCID.ThePossessed,
		};

		/// <summary>True for the Solar Eclipse monsters that can carry the Discipline book (not Mothron eggs or hatchlings).</summary>
		public static bool IsEclipseType(int npcType) => EclipseTypes.Contains(npcType);

		public EclipseBookCondition() : base("Discipline", "Eclipse") { }

		protected override bool EventApplies(NPC npc) => Main.eclipse && IsEclipseType(npc.type);
	}

	// ------------------------------------------------------------------ loot

	/// <summary>Event-book drops, 1 in <see cref="LevyConstants.EventBookDropDenominator"/>. Only loot hooks: no per-tick cost.</summary>
	public class FlavorGlobalNPC : GlobalNPC
	{
		private static readonly IItemDropRuleCondition BloodMoon = new BloodMoonBookCondition();
		private static readonly IItemDropRuleCondition GoblinArmy = new GoblinArmyBookCondition();
		private static readonly IItemDropRuleCondition Pirates = new PirateInvasionBookCondition();
		private static readonly IItemDropRuleCondition OldOnes = new OldOnesArmyBookCondition();
		private static readonly IItemDropRuleCondition Eclipse = new EclipseBookCondition();

		/// <summary>Blood Moon enemies are everything hostile, so that rule is global (the Bestiary does not list global drops).</summary>
		public override void ModifyGlobalLoot(GlobalLoot globalLoot)
		{
			globalLoot.Add(ItemDropRule.ByCondition(BloodMoon, ModContent.ItemType<GuideToConscription>(), LevyConstants.EventBookDropDenominator));
		}

		/// <summary>Per-NPC rules for the other four events, so they show in the Bestiary entries of those enemies.</summary>
		public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
		{
			int type = npc.type;
			int denominator = LevyConstants.EventBookDropDenominator;
			if (NPCID.Sets.BelongsToInvasionGoblinArmy[type])
				npcLoot.Add(ItemDropRule.ByCondition(GoblinArmy, ModContent.ItemType<GuideToDrill>(), denominator));
			if (NPCID.Sets.BelongsToInvasionPirate[type])
				npcLoot.Add(ItemDropRule.ByCondition(Pirates, ModContent.ItemType<GuideToPlunder>(), denominator));
			if (NPCID.Sets.BelongsToInvasionOldOnesArmy[type] && type != NPCID.DD2EterniaCrystal && type != NPCID.DD2LanePortal)
				npcLoot.Add(ItemDropRule.ByCondition(OldOnes, ModContent.ItemType<GuideToFortification>(), denominator));
			if (EclipseBookCondition.IsEclipseType(type))
				npcLoot.Add(ItemDropRule.ByCondition(Eclipse, ModContent.ItemType<GuideToDiscipline>(), denominator));
		}
	}

	// ------------------------------------------------------------------ Nurse and Guide

	/// <summary>The Nurse heals nearby soldiers from the treasury; the Guide says the hint line. Applies only to those two NPCs.</summary>
	public class FlavorTownGlobalNPC : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Nurse || entity.type == NPCID.Guide;

		public override void PostAI(NPC npc)
		{
			if (npc.type != NPCID.Nurse || Main.netMode == NetmodeID.MultiplayerClient) return;
			if ((Main.GameUpdateCount + (uint)npc.whoAmI) % LevyConstants.NurseHealIntervalTicks != 0) return;
			if (!npc.active || npc.life <= 0 || !LevyConfig.Get.NurseHealsSoldiers) return;
			TreatSoldiers(npc);
		}

		/// <summary>Heals every wounded soldier within reach to full, each paid for from the nearest barracks treasury.</summary>
		private static void TreatSoldiers(NPC nurse)
		{
			BarracksTE barracks = null;
			bool treated = false;
			foreach (SoldierNPC soldier in RosterSystem.Instance.ActiveBodies())
			{
				NPC body = soldier.NPC;
				if (!soldier.IsAlive || body.life >= body.lifeMax) continue;
				if (!LevyUtil.WithinTiles(nurse.Center, body.Center, LevyConstants.NurseHealRadiusTiles)) continue;

				barracks ??= SettlementSystem.Instance.NearestBarracks(nurse.Center, LevyConstants.NurseTreasuryReachTiles);
				if (barracks == null) return; // no settlement in reach: nobody pays

				int missing = body.lifeMax - body.life;
				if (!EconomySystem.TryPay(barracks, FlavorSystem.NurseHealPrice(missing))) continue; // treasury cannot cover this one

				body.life = body.lifeMax;
				if (soldier.Record != null) soldier.Record.CurrentHp = body.life;
				body.HealEffect(missing);
				body.netUpdate = true;
				if (!treated) FlavorSystem.PlayHealSound(nurse.Center);
				treated = true;
			}
			// The treasury changed: tell clients (the barracks panel shows it).
			if (treated && Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.TileEntitySharing, number: barracks.ID);
		}

		/// <summary>Client side: the first time you talk to the Guide after taking up a War Horn or building a Barracks, he says the hint.</summary>
		public override void GetChat(NPC npc, ref string chat)
		{
			if (npc.type != NPCID.Guide || Main.netMode == NetmodeID.Server) return;
			FlavorSystem flavor = FlavorSystem.Instance;
			if (flavor.GuideHintShown) return;
			if (!Main.LocalPlayer.GetModPlayer<FlavorPlayer>().EverHeldWarHorn && !AnyBarracks()) return;
			chat = LevyUtil.T("Chat.GuideHint");
			// Single player: saved with the world. Multiplayer: only this client's copy changes, so it is not repeated this session.
			flavor.GuideHintShown = true;
		}

		private static bool AnyBarracks()
		{
			foreach (BarracksTE _ in SettlementSystem.Instance.AllBarracks()) return true;
			return false;
		}
	}
}
