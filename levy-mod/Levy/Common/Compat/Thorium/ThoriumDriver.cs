using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// The Thorium-touching half of <see cref="SoldierThoriumGlobalNPC"/>: an instrument-holder gets its Inspiration pool sized and refilled
	/// (<see cref="BardCompat.Tick"/>); a heal-weapon holder runs the healer AI (<see cref="HealerCompat.Tick"/>). Reaches Levy only through its public
	/// surface (<c>SoldierNPC.Dummy</c>, <c>Record</c>, <c>WeaponUse</c>). JIT'd only when ThoriumMod is enabled; only ever called behind the Active check.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class ThoriumDriver
	{
		public static void Drive(SoldierNPC s, SoldierThoriumGlobalNPC state)
		{
			var gear = s.Record.Gear;
			if (gear == null) return;
			if (BardCompat.IsBardWeapon(gear.Weapon) || BardCompat.IsBardWeapon(gear.Sidearm)) BardCompat.Tick(s, state);
			Item held = WeaponUse.HeldItem(s);
			if (HealerCompat.IsHealWeapon(held)) HealerCompat.Tick(s, held, state);
		}

		/// <summary>Register the two generic Levy hooks (Bard / Healer weapons issuable; per-shot Inspiration gate and heal-weapon veto).</summary>
		public static void Register()
		{
			WeaponClassifier.ExtraCombatClass = CompatHooks.ExtraCombatClass;
			WeaponUse.PreFireGate = CompatHooks.PreFireGate;
		}

		/// <summary>Clear the hooks (Levy is unloading; leave nothing pointing at a dead delegate).</summary>
		public static void Unregister()
		{
			WeaponClassifier.ExtraCombatClass = null;
			WeaponUse.PreFireGate = null;
		}
	}
}
