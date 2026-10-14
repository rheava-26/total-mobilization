using Terraria.ModLoader;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>Interface layers, panels, radial, keybinds, HUD. OWNED BY THE UI AGENT.</summary>
	public class UISystem : ModSystem
	{
		public static UISystem Instance => ModContent.GetInstance<UISystem>();
		/// <summary>Soldiers drawn on screen this frame (nameplates auto-hide above the threshold).</summary>
		public static int SoldiersOnScreen;
		public virtual void OpenBarracks(BarracksTE te) { }
		public virtual void OpenArmory(ArmoryTE te) { }
		public virtual void CloseAll() { }
		public virtual void ToggleRadial() { }
		public virtual void ToggleArmyStrip() { }
	}
}
