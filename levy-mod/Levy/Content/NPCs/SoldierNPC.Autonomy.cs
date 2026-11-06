using Terraria;
using Levy.Common;
using Levy.Common.Potions;

namespace Levy.Content.NPCs
{
	/// <summary>The Autonomous levy's fall-back: below <see cref="PotionConstants.AutonomousFallbackFrac"/> of its life it retreats (the routed retreat, which keeps shooting) and heals, and re-engages at <see cref="PotionConstants.AutonomousReengageFrac"/>.</summary>
	public partial class SoldierNPC
	{
		private bool _autoFallingBack;

		/// <summary>Brain hook. Returns true while the levy is falling back this think (the brain then does nothing else).</summary>
		private bool Autonomy_FallBack(int now, float nearestHostileTiles)
		{
			if (!AutonomySystem.IsAutonomous(this)) { _autoFallingBack = false; return false; }
			float frac = NPC.life / (float)System.Math.Max(1, NPC.lifeMax);
			if (!_autoFallingBack && frac < PotionConstants.AutonomousFallbackFrac) _autoFallingBack = true;
			else if (_autoFallingBack && frac >= PotionConstants.AutonomousReengageFrac) { _autoFallingBack = false; NPC.netUpdate = true; return false; }
			if (!_autoFallingBack) return false;
			int heal = (int)System.Math.Ceiling(NPC.lifeMax * PotionConstants.AutonomousFallbackRegenPerSec * LevyConstants.ThinkIntervalTicks / 60f);
			NPC.life = System.Math.Min(NPC.lifeMax, NPC.life + heal);
			ThinkRouted(nearestHostileTiles, now);
			return true;
		}
	}
}
