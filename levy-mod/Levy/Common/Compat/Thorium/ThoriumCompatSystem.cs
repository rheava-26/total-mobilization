using System;
using System.Runtime.CompilerServices;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Entry point of the optional Thorium compat: Levy soldiers play Thorium Bard instruments (spending a per-soldier Inspiration pool that grows from Inspiration items in the Armory,
	/// and empowering the squad and the commander with the instrument's own empowerments) and use Thorium Healer weapons by role (radiant damage at enemies; ally heals on the wounded
	/// commander or squadmates; self heals; hybrids both).
	///
	/// ThoriumMod is a WEAK reference (build.txt: weakReferences = ThoriumMod). This class touches no ThoriumMod type: it only asks
	/// <c>ModLoader.HasMod("ThoriumMod")</c> and, if true, calls into <see cref="ThoriumDriver"/>. Every class that does touch a Thorium type
	/// (<see cref="BardCompat"/>, <see cref="HealerCompat"/>, <see cref="CompatHooks"/>, <see cref="ThoriumDriver"/>) is tagged
	/// <c>[JITWhenModsEnabled("ThoriumMod")]</c>, so tModLoader's load-time JIT skips them when Thorium is absent and nothing ever resolves a Thorium type.
	///
	/// Levy core knows nothing about Thorium: the compat plugs into two generic hooks, registered in OnModLoad (before any mod's PostSetupContent, so
	/// no weapon profile can be cached without them):
	///   * <c>WeaponClassifier.ExtraCombatClass</c> - declares the Bard / Healer damage classes issuable.
	///   * <c>WeaponUse.PreFireGate</c> - per-shot veto: gates and spends Inspiration for instruments, and stops a pure heal / self-heal / support item from being fired at an enemy.
	///   * <c>SoldierNPC.ExtraBuffStats</c> - lays a soldier's live bard empowerments on his stat dummy.
	/// </summary>
	public class ThoriumCompatSystem : ModSystem
	{
		/// <summary>True only when ThoriumMod is loaded and the compat has registered its hooks. Everything Thorium-facing gates on this.</summary>
		public static bool Active { get; private set; }

		public override void OnModLoad()
		{
			Active = false;
			if (!ModLoader.HasMod("ThoriumMod")) return;
			try
			{
				RegisterHooks();
				Active = true;
				Mod.Logger.Info("Levy/Thorium compat: ThoriumMod detected - registered the Thorium Bard / Healer hooks with Levy.");
			}
			catch (Exception e)
			{
				Active = false;
				Mod.Logger.Warn("Levy/Thorium compat: ThoriumMod is present but the compat failed to register and is OFF: " + e.Message);
			}
		}

		public override void Unload()
		{
			if (Active)
			{
				try { UnregisterHooks(); } catch (Exception) { }
			}
			Active = false;
		}

		// NoInlining keeps the JIT from pulling ThoriumDriver into OnModLoad/Unload's compilation when Thorium is absent.
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void RegisterHooks() => ThoriumDriver.Register();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UnregisterHooks() => ThoriumDriver.Unregister();
	}
}
