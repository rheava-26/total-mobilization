using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;
using Levy.Common.Flavor;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Buffs;
using Levy.Content.Items.Books;
using Levy.Content.NPCs;

namespace Levy.Common.Potions
{
	/// <summary>The Guide to Autonomy's effects: the Autonomous buff on the levies of a Spectator, and the radio-free map order gate.</summary>
	public class AutonomySystem : ModSystem
	{
		public static bool Studied => GuideSystem.IsStudied("Autonomy");

		/// <summary>
		/// True when <paramref name="p"/> may give map orders (the map click and the orders radial's map-wide reach) without a Command Radio: the guide is studied, and, only when
		/// <see cref="PotionConstants.MapOrdersNeedSpectator"/> is set, the Spectator buff is active.
		/// </summary>
		public static bool CanCommandWithoutRadio(Player p) => Studied && p != null && (!PotionConstants.MapOrdersNeedSpectator || SpectatorPlayer.IsSpectator(p));

		public override void PostUpdateEverything()
		{
			if (!LevyUtil.IsServerOrSingle || !Studied) return;
			if (Main.GameUpdateCount % PotionConstants.AutonomousPulseTicks != 0) return;
			var roster = RosterSystem.Instance;
			if (roster == null) return;
			int buff = ModContent.BuffType<AutonomousBuff>();
			foreach (SoldierNPC s in roster.ActiveBodies())
			{
				if (s == null || !s.IsAlive) continue;
				Player boss = s.Commander;
				if (!SpectatorPlayer.IsSpectator(boss)) continue;
				bool had = s.NPC.HasBuff(buff);
				s.NPC.AddBuff(buff, PotionConstants.AutonomousDurationTicks);
				if (!had) s.StatsDirty = true;
			}
		}
	}

	/// <summary>Slime Rain: any enemy may drop the Guide to Autonomy, 1 in <see cref="PotionConstants.AutonomyDropDenominator"/>, until it is studied in this world.</summary>
	public sealed class AutonomyDropCondition : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info)
		{
			NPC n = info.npc;
			if (n == null || !Main.slimeRain || n.SpawnedFromStatue || AutonomySystem.Studied) return false;
			return IsEnemy(n);
		}

		public static bool IsEnemy(NPC n) => n != null && !n.friendly && !n.townNPC && n.damage > 0 && n.lifeMax > 5 && n.ModNPC is not SoldierNPC;

		public bool CanShowItemDropInUI() => true;
		public string GetConditionDescription() => LevyUtil.T("Potions.DropSlimeRain");
	}

	public class AutonomyDrops : GlobalNPC
	{
		private static readonly IItemDropRuleCondition SlimeRainDrop = new AutonomyDropCondition();

		public override void ModifyGlobalLoot(GlobalLoot globalLoot)
		{
			globalLoot.Add(ItemDropRule.ByCondition(SlimeRainDrop, ModContent.ItemType<GuideToAutonomy>(), PotionConstants.AutonomyDropDenominator));
		}
	}
}
