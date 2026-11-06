using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.Items.OOA;

namespace Levy.Common.Events
{
	/// <summary>
	/// The Tavernkeep (DD2 Bartender) sells the three levy sentry staves for Defender Medals (tier 1 always, tier 2 after any mechanical boss, tier 3 after Golem) and the three
	/// drinks for coins. tML 1.4.4 has no tabbed or scrolling shop (only a 40-slot Item[] whose last slot stays empty), and the vanilla Tavernkeep shop reserves slots for its
	/// own stock, so the entries are laid into whatever free slots the final shop has (after every other mod has added its own): see <see cref="Lay"/>.
	/// </summary>
	public class TavernkeepShop : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.DD2Bartender;

		/// <summary>The wanted stock for the current progression: staves (unlocked tiers) then drinks.</summary>
		public static List<Item> Wanted(bool mech, bool golem, out int topStaff)
		{
			var list = new List<Item>();
			int top = golem ? 3 : mech ? 2 : 1;
			topStaff = top;
			for (int tier = 1; tier <= top; tier++)
			{
				var it = new Item();
				it.SetDefaults(tier == 1 ? ModContent.ItemType<LevyRod>() : tier == 2 ? ModContent.ItemType<LevyCane>() : ModContent.ItemType<LevyStaff>());
				it.shopCustomPrice = LevyConstants.OoaMedalPricePerTier[tier - 1];
				it.shopSpecialCurrency = CustomCurrencyID.DefenderMedals;
				list.Add(it);
			}
			foreach (int type in new[] { ModContent.ItemType<BarracksAle>(), ModContent.ItemType<BlackStout>(), ModContent.ItemType<SpicedRum>() })
			{
				var it = new Item(); it.SetDefaults(type); list.Add(it);
			}
			return list;
		}

		/// <summary>
		/// Puts <paramref name="wanted"/> (staves first) into the null slots of <paramref name="items"/>, never touching an occupied or reserved (IsAir) slot. If everything
		/// fits it all goes in. If not, the staves collapse to ONE entry, the highest unlocked tier (<paramref name="staffCount"/> says how many staves were offered), then drinks as room allows.
		/// Returns the number placed; <paramref name="collapsed"/> is true when the staves had to collapse.
		/// </summary>
		public static int Lay(Item[] items, List<Item> wanted, int staffCount, out bool collapsed)
		{
			int free = 0;
			for (int i = 0; i < items.Length; i++) if (items[i] == null) free++;
			collapsed = false;
			var list = wanted;
			if (free < wanted.Count)
			{
				collapsed = staffCount > 1;
				list = new List<Item>();
				if (staffCount > 0) list.Add(wanted[staffCount - 1]);
				for (int i = staffCount; i < wanted.Count; i++) list.Add(wanted[i]);
			}
			int placed = 0, slot = 0;
			foreach (var it in list)
			{
				while (slot < items.Length && items[slot] != null) slot++;
				if (slot >= items.Length) break;
				items[slot++] = it; placed++;
			}
			return placed;
		}

		public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
		{
			var wanted = Wanted(Condition.DownedMechBossAny.IsMet(), Condition.DownedGolem.IsMet(), out int staffs);
			int placed = Lay(items, wanted, staffs, out bool collapsed);
			if (placed < wanted.Count || collapsed)
				Mod.Logger.Info($"Levy: Tavernkeep shop is crowded - placed {placed} of {wanted.Count} Levy entries (staves collapsed to the top tier: {collapsed}).");
		}
	}
}
