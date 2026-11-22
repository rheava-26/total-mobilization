using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// TELEGRAPHED BLASTS. A projectile that draws the circle it will explode in (Ares's Gauss Nuke: a 1480 px circle for three seconds) is a pending area in the threat field
	/// (<see cref="ThreatField.Areas"/>). The dodge reads only the projectile's own small hitbox, so a levy inside the circle stood there until it went off. At the end of each think a levy inside
	/// such a circle (with a margin) runs out of it sideways, the way a player clears a telegraph, and keeps shooting while it runs. Harness switch LEVY_AREAS=0 turns it off.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Extra room (px) past a telegraphed circle's edge a levy aims for: its body plus a few tiles for the blast's own spread.</summary>
		private const float BlastMarginPx = 3f * LevyConstants.TilePx, BlastClearPx = 6f * LevyConstants.TilePx;
		private bool _blastFlee;
		/// <summary>Harness telemetry: thinks in which a levy ran out of a telegraphed area.</summary>
		internal static long BlastFleesS;
		/// <summary>True while this levy is running out of a telegraphed blast (movement runs, it does not stroll the last tiles).</summary>
		internal bool BlastFleeing => _blastFlee;

		/// <summary>End of a think: a levy inside a telegraphed blast takes a goal just outside its edge, on its own side of the centre.</summary>
		private void ApplyBlastAvoid(int now)
		{
			_blastFlee = false;
			if (Hostile || !ThreatShapes.AreasOn) return; // HOSTILE SEAM: the areas listed are the enemy's, a threat to the player's side only
			var areas = ThreatField.AreasNow();
			if (areas.Count == 0) return;
			Vector2 c = NPC.Center;
			float best = 0f; int bi = -1; Vector2 bnear = default;
			for (int i = 0; i < areas.Count; i++)
			{
				float d = ThreatField.AreaDepth(areas[i], c, BlastMarginPx + NPC.width * 0.5f, out Vector2 near);
				if (d > best) { best = d; bi = i; bnear = near; }
			}
			if (bi < 0) return;
			var a = areas[bi];
			// sideways out of the circle (a goal's Y is the ground spot under it: the body moves along X); the side it already stands on, else the side its index picks
			float dx = c.X - bnear.X, dy = c.Y - bnear.Y;
			int side = Math.Abs(dx) > 8f ? Math.Sign(dx) : ((NPC.whoAmI & 1) == 0 ? 1 : -1);
			float reach = a.R + BlastMarginPx + BlastClearPx + NPC.width * 0.5f;
			float half = (float)Math.Sqrt(Math.Max(0f, reach * reach - dy * dy));
			float gx = bnear.X + side * Math.Max(half, 2f * LevyConstants.TilePx);
			SetGoal(new Vector2(gx, NPC.Bottom.Y));
			_blastFlee = true; BlastFleesS++;
		}
	}
}
