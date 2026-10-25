using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Per-tick driver shell for Levy soldiers carrying a Thorium class weapon. Holds only plain state and touches no ThoriumMod type: when ThoriumMod
	/// is absent (<see cref="ThoriumCompatSystem.Active"/> false) it never attaches to a soldier and never calls anything. When present it hands each
	/// tick to <see cref="ThoriumDriver"/> (the JIT-gated half). Runs after the soldier's own AI, server / single player only.
	/// </summary>
	public class SoldierThoriumGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && ThoriumCompatSystem.Active && entity.ModNPC is SoldierNPC;

		/// <summary>The Inspiration pool has been filled for the first time (a bard starts with a full pool).</summary>
		public bool BardPoolInit;
		/// <summary>Ticks until this soldier's healer AI may cast again.</summary>
		public int HealCooldown;
		/// <summary>Main.GameUpdateCount until which a healer leaves this soldier alone (a heal is already on its way).</summary>
		public uint HealReservedUntil;

		private bool _warned;

		public override void PostAI(NPC npc)
		{
			if (!ThoriumCompatSystem.Active) return;
			if (!LevyUtil.IsServerOrSingle) return;
			if (npc.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive || s.Dummy == null) return;
			try
			{
				ThoriumDriver.Drive(s, this);
			}
			catch (Exception e)
			{
				// A Thorium surprise must never take down the soldier's tick: report it once per soldier and carry on.
				if (!_warned) { _warned = true; Mod.Logger.Warn($"Levy/Thorium compat: {s.SoldierName} threw in the class driver: {e.Message}"); }
			}
		}
	}
}
