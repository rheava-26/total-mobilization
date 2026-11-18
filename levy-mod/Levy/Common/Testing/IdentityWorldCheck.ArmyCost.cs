using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ARMY SWEEP COST (lagsweep, 2026-11-17): what one weapon costs the game per tick, measured inside the army sweep's own runs (one levy or one stand-in player firing it at the pinned dummy).
	///  - the shots: wall time inside Projectile.Update of every active non-hostile projectile (the weapon's shots, its minions and their bolts), per tick, and the shots alive (mean and peak);
	///  - the levy: wall time of the levy body's own NPC update (its AI, its gear frame), per tick;
	///  - the effects: Dust.NewDust / NewDustPerfect and Gore.NewGore calls per second. A dedicated server returns from both without making anything, so the CALLS are what a client would have made.
	/// Timing is installed for the sweep only (detours added and removed around it); the measured runs are the sweep's ordinary runs, nothing about them changes.
	/// </summary>
	public partial class IdentityWorldCheck
	{
		private static bool _costOn; private static int _costBody = -1;
		private static long _costProjT, _costProjN, _costLevyT, _costDust, _costGore;
		private static int _costProjDepth;

		private static void CostAttach()
		{
			if (_costOn) return;
			On_Projectile.Update += CostProjUpdate; On_NPC.UpdateNPC += CostNpcUpdate;
			On_Dust.NewDust += CostDust; On_Dust.NewDustPerfect += CostDustPerfect; On_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float += CostGore;
			_costOn = true;
		}

		private static void CostDetach()
		{
			if (!_costOn) return;
			On_Projectile.Update -= CostProjUpdate; On_NPC.UpdateNPC -= CostNpcUpdate;
			On_Dust.NewDust -= CostDust; On_Dust.NewDustPerfect -= CostDustPerfect; On_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float -= CostGore;
			_costOn = false; _costBody = -1;
		}

		private static void CostReset() { _costProjT = _costProjN = _costLevyT = _costDust = _costGore = 0; }

		private static void CostProjUpdate(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (!self.active || self.hostile || _costProjDepth > 0) { orig(self, i); return; }
			_costProjDepth++; long t0 = Stopwatch.GetTimestamp();
			try { orig(self, i); }
			finally { _costProjT += Stopwatch.GetTimestamp() - t0; _costProjN++; _costProjDepth--; }
		}

		private static void CostNpcUpdate(On_NPC.orig_UpdateNPC orig, NPC self, int i)
		{
			if (i != _costBody || !self.active) { orig(self, i); return; }
			long t0 = Stopwatch.GetTimestamp();
			try { orig(self, i); } finally { _costLevyT += Stopwatch.GetTimestamp() - t0; }
		}

		private static int CostDust(On_Dust.orig_NewDust orig, Vector2 position, int width, int height, int type, float speedX, float speedY, int alpha, Color newColor, float scale)
		{ _costDust++; return orig(position, width, height, type, speedX, speedY, alpha, newColor, scale); }
		private static Dust CostDustPerfect(On_Dust.orig_NewDustPerfect orig, Vector2 position, int type, Vector2? velocity, int alpha, Color newColor, float scale)
		{ _costDust++; return orig(position, type, velocity, alpha, newColor, scale); }
		private static int CostGore(On_Gore.orig_NewGore_IEntitySource_Vector2_Vector2_int_float orig, IEntitySource source, Vector2 position, Vector2 velocity, int type, float scale)
		{ _costGore++; return orig(source, position, velocity, type, scale); }

		/// <summary>The run's costs into its result (called at the end of a measured run, over <paramref name="ran"/> ticks).</summary>
		private static void CostRead(ArmyRes res, int ran)
		{
			double f = 1000.0 / Stopwatch.Frequency / Math.Max(1, ran);
			res.ProjMsTick = _costProjT * f; res.LevyMsTick = _costLevyT * f; res.ProjAlive = _costProjN / (double)Math.Max(1, ran);
			res.DustPerSec = _costDust * 60.0 / Math.Max(1, ran); res.GorePerSec = _costGore * 60.0 / Math.Max(1, ran);
		}
	}
}
