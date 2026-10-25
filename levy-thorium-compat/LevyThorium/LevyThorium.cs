using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using LevyThorium.Common;

namespace LevyThorium
{
	/// <summary>
	/// Levy: Thorium. A class-compatibility submod: Levy soldiers play Thorium Bard instruments (spending a per-soldier Inspiration pool) and
	/// use Thorium Healer ally-heal weapons on wounded squadmates.
	///
	/// Levy knows nothing about Thorium. This mod plugs into two generic Levy hooks, registered in <see cref="Load"/> (before any mod's
	/// PostSetupContent, so no weapon profile can be cached without them):
	///   * <see cref="WeaponClassifier.ExtraCombatClass"/> - declares the Bard / Healer damage classes issuable (Levy otherwise benches any
	///     weapon whose class is none of melee / ranged / magic / summon / throwing).
	///   * <see cref="WeaponUse.PreFireGate"/> - a per-shot veto: gates and spends Inspiration for instruments, and stops a heal weapon from
	///     being fired at an enemy (the healer is driven by <see cref="Content.SoldierThoriumGlobalNPC"/>, always at allies).
	/// Everything else rides on Levy's public API (SoldierNPC.Dummy, WeaponUse.Spawn, ...) from a GlobalNPC.
	/// </summary>
	public class LevyThorium : Mod
	{
		public override void Load()
		{
			WeaponClassifier.ExtraCombatClass = CompatHooks.ExtraCombatClass;
			WeaponUse.PreFireGate = CompatHooks.PreFireGate;
			Logger.Info("LevyThorium: registered the Thorium Bard / Healer hooks with Levy.");
		}

		public override void Unload()
		{
			// Only clear a hook that is still ours (Levy may already be gone).
			try
			{
				if (WeaponClassifier.ExtraCombatClass == (Func<Item, bool>)CompatHooks.ExtraCombatClass) WeaponClassifier.ExtraCombatClass = null;
				if (WeaponUse.PreFireGate == (Func<SoldierNPC, Item, WeaponProfile, NPC, bool>)CompatHooks.PreFireGate) WeaponUse.PreFireGate = null;
			}
			catch (Exception) { }
		}
	}
}
