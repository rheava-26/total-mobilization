using System;
using System.Runtime.CompilerServices;
using Terraria.ModLoader;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// Entry point of the optional Magic Storage compat. MagicStorage is a WEAK reference (build.txt: weakReferences). This class touches no Magic Storage type: it asks <c>ModLoader.HasMod("MagicStorage")</c>
	/// and, if true, calls into <c>MsDriver</c>. Every class that does touch a Magic Storage type (<c>MsDriver</c>, <c>ArmoryHeartTile</c>, <c>TEArmoryHeart</c>, <c>ArmoryHeartItem</c>) is tagged
	/// <c>[JITWhenModsEnabled("MagicStorage")]</c> and the content classes <c>[ExtendsFromMod("MagicStorage")]</c>, so Levy loads and plays without Magic Storage.
	/// The Magic-Storage-touching sources are compiled only when the MAGICSTORAGE symbol is defined (see tools/enable_magicstorage.sh, which also adds the weak reference); until then this system does nothing.
	/// What it registers is generic: <see cref="Levy.Common.Logistics.NetworkStocks"/>, which orders, buying and the find-this-item chooser read.
	/// </summary>
	public class MagicStorageCompatSystem : ModSystem
	{
		/// <summary>True only when Magic Storage is loaded and the compat registered its hooks.</summary>
		public static bool Active { get; private set; }

		public override void OnModLoad()
		{
			Active = false;
			if (!ModLoader.HasMod("MagicStorage")) return;
			try { Active = RegisterHooks(); if (Active) Mod.Logger.Info("Levy/MagicStorage compat: Magic Storage detected - the Armory Heart joins armories to its networks."); }
			catch (Exception e) { Active = false; Mod.Logger.Warn("Levy/MagicStorage compat: Magic Storage is present but the compat failed to register and is OFF: " + e.Message); }
		}

		public override void Unload()
		{
			try { if (Active) UnregisterHooks(); } catch (Exception) { }
			Active = false;
		}

		// NoInlining keeps the JIT from pulling MsDriver into OnModLoad / Unload when Magic Storage is absent.
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool RegisterHooks()
		{
#if MAGICSTORAGE
			MsDriver.Register(); return true;
#else
			return false;
#endif
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UnregisterHooks()
		{
#if MAGICSTORAGE
			MsDriver.Unregister();
#endif
		}
	}
}
