using Terraria.UI;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>Armory storage grid + issue rows. Filled in step 4.</summary>
	public class ArmoryPanel : UIState
	{
		public ArmoryTE Te { get; private set; }
		public void Bind(ArmoryTE te) { Te = te; }
	}
}
