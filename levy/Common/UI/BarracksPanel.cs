using Terraria.UI;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>Barracks management panel. Filled in step 5.</summary>
	public class BarracksPanel : UIState
	{
		public BarracksTE Te { get; private set; }
		public void Bind(BarracksTE te) { Te = te; }
	}
}
