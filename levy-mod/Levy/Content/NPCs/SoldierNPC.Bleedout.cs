using System;
using Terraria;
using Levy.Common.Compat.Calamity;
using Levy.Common.Config;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// BLEEDOUT PARITY (Calamity's Chalice of the Blood God). The Chalice cuts every hit above 5 damage down to 5 and takes the rest from the wearer's life over the next seconds (ChaliceBleed). A player wearing it is hurt for the
	/// full amount, late. A levy in it was hurt for 5: the cut ran in the hit sequence and the buffer filled on the gear dummy, but nothing ever took the buffer's life from the body (the regeneration pass that runs the drain
	/// discards what the dummy loses). Eve's video: Sgt. Alice stood inside the Avatar of Emptiness's collapsing star for the whole attack and showed 5s and "(-435)"s.
	/// <para/>Every tick, <see cref="Bleedout_Update"/> runs Calamity's own drain on the dummy and subtracts what it took from the body's life (a body that bleeds out dies of it, as a player does). The regeneration pass is told to
	/// leave the buffer alone so it is drained once. Setting: <c>LevyConfig.SoldiersBleedOut</c> (default on); <c>LEVY_BLEEDOUT=0</c> switches it off for the lab (A/B).
	/// </summary>
	public partial class SoldierNPC
	{
		private static readonly bool BleedoutEnv = Environment.GetEnvironmentVariable("LEVY_BLEEDOUT") != "0";
		/// <summary>Lab switch (the world check flips it for its before/after reading); also off while the setting is off.</summary>
		internal static bool BleedoutLab = true;
		/// <summary>The world check forces the drain on regardless of the setting, so it measures the fix with the default off.</summary>
		internal static bool BleedoutForce;
		internal static bool Bleedout_On
		{
			get
			{
				if (!BleedoutEnv || !BleedoutLab || !CalamityLoaded) return false;
				if (BleedoutForce) return true;
				try { return LevyConfig.Get != null && LevyConfig.Get.SoldiersBleedOut; } catch (Exception) { return false; }
			}
		}
		/// <summary>Harness telemetry: life this body lost to the Chalice's bleedout.</summary>
		internal int BleedoutTaken;

		private void Bleedout_Update()
		{
			if (LevyUtil.IsClient || !CalamityLoaded || !IsAlive) return;
			Player d = Dummy;
			if (d == null || Record == null || !Bleedout_On) return;
			int lost = ChaliceBleed.Step(d, a => InFrame(a));
			if (lost <= 0) return;
			BleedoutTaken += lost;
			NPC.life -= lost; NPC.netUpdate = true;
			if (Record != null) Record.CurrentHp = Math.Max(0, NPC.life);
			if (NPC.life <= 0) { NPC.life = 0; DiedOfDot = true; NPC.checkDead(); }
		}
	}
}
