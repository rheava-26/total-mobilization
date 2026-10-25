using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>The two delegates the Thorium compat hands to Levy (see <see cref="ThoriumCompatSystem"/>). JIT'd only when ThoriumMod is enabled.</summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class CompatHooks
	{
		/// <summary>WeaponClassifier.ExtraCombatClass: Thorium Bard and Healer weapons are issuable combat weapons.</summary>
		public static bool ExtraCombatClass(Item item) => BardCompat.IsBardWeapon(item) || HealerCompat.IsHealerClassWeapon(item);

		/// <summary>WeaponUse.PreFireGate: true = the soldier may shoot now.</summary>
		public static bool PreFireGate(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (BardCompat.IsBardWeapon(item)) return BardCompat.TryPlay(s, item);
			// An ally-heal weapon is only ever fired by the healer driver, at a wounded squadmate: never at the enemy Levy's brain picked.
			if (HealerCompat.IsHealWeapon(item)) return false;
			return true;
		}
	}
}
