using System;
using System.Runtime.CompilerServices;
using Terraria.ModLoader;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// Boot self-check of the Magic Storage half, run once after recipes are final and ONLY when Magic Storage is loaded. This class names no Magic Storage type; the work is in <c>MsCheckDriver</c>.
	/// </summary>
	public class MagicStorageCheck : ModSystem
	{
		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			if (!ModLoader.HasMod("MagicStorage") || !MagicStorageCompatSystem.Active) return;
			try { Run(); }
			catch (Exception e) { Mod.Logger.Warn("Levy Magic Storage self-check: threw " + e.GetType().Name + ": " + e.Message); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Run()
		{
			MsCheckDriver.Run(Mod);
		}
	}
}
