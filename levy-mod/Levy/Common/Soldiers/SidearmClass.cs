using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// Which sidearm a levy in class armor prefers. A sidearm is the weapon he swaps to when his primary is dry or cannot reach; late in the game a player has no reason to carry a weapon of another class, so a levy who wears
	/// class armor weights the sidearms of HIS damage class far above the rest (a Meowmere on a ranger's belt wastes the ranger set).
	///  * CLASS. His primary's class while class-matched armor is on (the armor follows the primary, so the whole kit is one class). With it off: the class his armor favours (<see cref="ArmorDoctrine.PreferredClass"/>: a set bonus or
	///    class-dominant armor; Calamity rogue is its own class, whips and minion staves count as summon), or the primary's when the armory's rules ask for it. A sidearm a rule asks for (other than by the class balance) is never counted off-class.
	///  * STRENGTH comes from world progress (<see cref="WeaponRank.Progress"/>, the Boss Checklist scale): <see cref="MinStrength"/> up to the start of Hardmode, rising to 1 after the Moon Lord (<see cref="FullAt"/>). An off-class weapon keeps
	///    <see cref="OffFactor"/> of its rank: 0.6 before Hardmode (a much better blade still wins), 0.04 post Moon Lord (it wins only against a same-class weapon more than a progression band behind, since a band is a x6 rank).
	///  * COMPLEMENT. Inside the class the pick is shaped by the primary: a slow primary pairs with a fast sidearm, and a mana-fed primary with a sidearm that costs far less mana (it takes the hand when the mana runs dry).
	/// Pure functions over items; the armory applies them (<c>ArmoryTE.TakeClassSidearm</c>, <c>UpgradeSidearm</c>).
	/// </summary>
	public static class SidearmClass
	{
		/// <summary>Strength before Hardmode, the progress (Boss Checklist scale) at which it starts to rise, and where it reaches 1 (Moon Lord).</summary>
		public const float MinStrength = 0.15f, RampFrom = 7f, FullAt = 18f;
		/// <summary>The share of its rank an off-class weapon keeps at strength 0 and at strength 1.</summary>
		public const float OffFactorWeak = 0.6f, OffFactorStrong = 0.04f;
		/// <summary>A primary at least this slow (use time in ticks) wants a fast sidearm; a sidearm at most this share of its use time counts as fast.</summary>
		public const int SlowUseTime = 28; public const float FastShare = 0.6f;
		/// <summary>Probe seam: a fixed strength (0..1) instead of the world's progress; negative = read the world.</summary>
		internal static float ForceStrength = -1f;

		public static float Strength(float progress)
		{
			if (progress <= RampFrom) return MinStrength;
			return Math.Min(1f, MinStrength + (1f - MinStrength) * (progress - RampFrom) / (FullAt - RampFrom));
		}

		public static float Strength() => ForceStrength >= 0f ? ForceStrength : Strength(WeaponRank.Progress());

		/// <summary>
		/// False once the class preference is at full strength (after the Moon Lord): a levy then carries no weapon of another class beside his primary at all, since a player that late has no reason to and the off-class weapon only
		/// fills the slot when nothing of his class is free (a melee blade on a post-Yharon ranger's belt, Eve's report). Before that the weighted pick stands (a much better blade still wins early on).
		/// </summary>
		public static bool OffClassAllowed(float strength) => strength < 1f || !LoadoutFixes.PostMoonLord;

		/// <summary>The share of its rank an off-class sidearm keeps at this strength.</summary>
		public static float OffFactor(float strength) => OffFactorWeak + (OffFactorStrong - OffFactorWeak) * Math.Clamp(strength, 0f, 1f);

		/// <summary>
		/// The damage class a levy's sidearm should be, or null when he has none to match. With class-matched armor on (<see cref="Config.LevyConfig.ClassMatchArmor"/>) his class is his primary's: the armor follows the primary,
		/// so the sidearm does too (reading the armor here while the armor read the primary went round in a circle: a ranger whose armor had not caught up was dealt a blade). With the setting off: the class of his armor, or
		/// of his primary when the armory's rules asked for it, as before.
		/// </summary>
		public static DamageClass TargetClass(SoldierGear g, ArmoryRules rules)
		{
			if (g == null || !g.HasWeapon) return null;
			var armor = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			if (Config.LevyConfig.Get.ClassMatchArmor && LoadoutFixes.Sidearm) return WeaponClassifier.ItemDamageClass(g.Weapon) ?? armor;
			if (armor == null) return null;
			if (rules != null && rules.Claims(g.Weapon)) return WeaponClassifier.ItemDamageClass(g.Weapon) ?? armor; // a rule asked for this primary: the armor follows it
			return armor;
		}

		/// <summary>
		/// True when the weapon is of the target class, or the armory's rules ask for it (so it is never held against the levy). A class balance does not count here: it claims every weapon of every class it lists,
		/// which made every off-class sidearm "asked for" under a balance of all classes (Eve's rules: at least 8 of each class).
		/// </summary>
		public static bool SameClass(Item side, DamageClass target, ArmoryRules rules)
		{
			if (target == null) return true;
			if (WeaponClassifier.ItemDamageClass(side) == target) return true;
			return rules != null && rules.Claims(side, !LoadoutFixes.Sidearm);
		}

		/// <summary>
		/// True when the levy can use <paramref name="side"/> beside his primary at all: not a stack, not the primary's own item type, and fed: it needs no ammo, shares the primary's ammo, or fires what his pouch holds.
		/// </summary>
		public static bool Usable(SoldierGear g, Item side)
		{
			if (g == null || side == null || side.IsAir || side.maxStack > 1) return false;
			if (g.HasWeapon && g.Weapon.type == side.type) return false;
			if (side.useAmmo <= 0) return true;
			if (g.HasWeapon && g.Weapon.useAmmo == side.useAmmo) return true;
			var pouch = g.Ammo;
			return pouch != null && !pouch.IsAir && pouch.ammo == side.useAmmo;
		}

		/// <summary>A factor around 1 (0.9 to 1.32) that shapes the pick inside a class: a slow primary pairs with a fast sidearm, a mana-fed primary with a sidearm that costs much less mana. Never enough to outweigh a class.</summary>
		public static float Complement(Item primary, Item side)
		{
			if (primary == null || primary.IsAir || side == null || side.IsAir) return 1f;
			float f = 1f;
			int pu = Math.Max(1, primary.useTime), su = Math.Max(1, side.useTime);
			if (pu >= SlowUseTime) f *= su <= pu * FastShare ? 1.2f : su >= pu ? 0.9f : 1f;
			if (primary.mana > 0 && side.mana <= primary.mana / 2) f *= 1.1f;
			return f;
		}

		/// <summary>The pick weight of a sidearm candidate: its rank times this. Same-class: 1 times the complement; off-class: <see cref="OffFactor"/> times the complement.</summary>
		public static float Weight(SoldierGear g, Item side, DamageClass target, ArmoryRules rules, float strength)
		{
			float w = SameClass(side, target, rules) ? 1f : OffFactor(strength);
			return w * Complement(g?.Weapon, side);
		}
	}
}
