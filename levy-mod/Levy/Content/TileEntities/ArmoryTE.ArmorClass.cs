using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Integrations;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// Class-matched armor (<see cref="Common.Config.LevyConfig.ClassMatchArmor"/>, default on). A levy's armor follows the class of the weapon in his hand (the weapon is what the rules, the class balance or the
	/// player's hand decided), and a set or piece of that class is worn over any other armor whenever it is no more than <see cref="LevyConstants.ClassArmorTierTolerance"/> progression tiers behind the best other
	/// armor in reach. Before this, the class only added a fixed bonus worth 14 defense, so a higher-defense set of another class (God Slayer, 100 to 120 defense) beat the summoner's Silva set (84) and the mage's (90).
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>The class a levy's armor should favour: his primary's damage class (with the player's class edits). Null when he holds nothing or a weapon with no class, or the setting is off.</summary>
		internal static DamageClass ArmorTargetClass(SoldierGear g)
		{
			if (g == null || !g.HasWeapon || !Common.Config.LevyConfig.Get.ClassMatchArmor) return null;
			return WeaponClassifier.ItemDamageClass(g.Weapon);
		}

		/// <summary>How far into progression an armor piece is (the progression table, else its rarity's stage; 0 when unknown). Same scale as the weapon tiers.</summary>
		internal static float PieceTier(Item it) => Common.Boss.BossRule.ItemTier(it);

		/// <summary>The damage class one armor piece favours on its own (worn in its own slot, no set bonus), or null for a generic or class-less piece.</summary>
		internal static DamageClass PieceClass(Item it)
		{
			if (IsAir(it)) return null;
			if (it.headSlot >= 0) return ArmorDoctrine.PreferredClass(it, null, null);
			if (it.bodySlot >= 0) return ArmorDoctrine.PreferredClass(null, it, null);
			if (it.legSlot >= 0) return ArmorDoctrine.PreferredClass(null, null, it);
			return null;
		}

		/// <summary>Mean progression tier of the pieces of a kit (empty slots skipped). 0 for an empty kit.</summary>
		private static float KitTier(Item h, Item b, Item l)
		{
			float s = 0f; int n = 0;
			foreach (var it in new[] { h, b, l }) if (!IsAir(it)) { s += PieceTier(it); n++; }
			return n == 0 ? 0f : s / n;
		}

		/// <summary>
		/// How well a kit fits <paramref name="cls"/>: the whole kit reads as that class (10), and is a real set with its set bonus (5 more), plus one per piece that does on its own.
		/// Used to tell a class correction (more fitting) from an ordinary upgrade.
		/// </summary>
		private static int ClassFitRank(Item h, Item b, Item l, DamageClass cls)
		{
			if (cls == null) return 0;
			int r = 0;
			if (ArmorDoctrine.PreferredClass(h, b, l) == cls) r = ArmorDoctrine.FormsSetBonus(h, b, l) ? 15 : 10;
			foreach (var it in new[] { h, b, l }) if (!IsAir(it) && PieceClass(it) == cls) r++;
			return r;
		}

		/// <summary>Every unreserved armor piece for <paramref name="part"/> this armory can issue from: its armor rows, storage, the networked armories' storage and a connected Magic Storage network (read only).</summary>
		private IEnumerable<Item> ArmorInReach(int part)
		{
			foreach (var arr in new[] { IssueArmor, Storage })
				foreach (var it in arr) if (!IsAir(it) && !it.vanity && !IsReservedRef(it) && WeaponClassifier.IsArmorPiece(it, part)) yield return it;
			foreach (var o in OtherNetworked())
				foreach (var it in o.Storage) if (!IsAir(it) && !it.vanity && !o.IsReservedRef(it) && WeaponClassifier.IsArmorPiece(it, part)) yield return it;
			var heart = MsHeart();
			if (heart != null)
				foreach (var it in MagicStorageBridge.Snapshot(heart)) if (!IsAir(it) && !it.vanity && WeaponClassifier.IsArmorPiece(it, part)) yield return it;
		}

		/// <summary>
		/// The per-piece pick for a levy whose armor follows <paramref name="cls"/>: the best piece of his class within the tier tolerance of the best piece in reach for that slot, wherever it sits (the armor rows
		/// still first among those). Null when no such piece is in reach; the caller then takes the strongest piece as before.
		/// </summary>
		private Item TakeClassArmorPiece(int part, DamageClass cls)
		{
			float top = float.NegativeInfinity; bool anyOn = false;
			foreach (var it in ArmorInReach(part)) { top = Math.Max(top, PieceTier(it)); if (PieceClass(it) == cls) anyOn = true; }
			if (!anyOn) return null;
			float floor = top - LevyConstants.ClassArmorTierTolerance;
			bool Pred(Item it) => WeaponClassifier.IsArmorPiece(it, part) && !it.vanity && PieceClass(it) == cls && PieceTier(it) >= floor;
			return TakeBest(IssueArmor, part, ArmorSlots, ArmorParts, Pred, ArmorScore);
		}
	}
}
