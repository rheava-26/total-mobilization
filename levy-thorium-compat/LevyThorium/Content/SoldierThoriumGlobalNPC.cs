using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using LevyThorium.Common;

namespace LevyThorium.Content
{
	/// <summary>
	/// Per-tick driver for Levy soldiers carrying a Thorium class weapon. Runs after the soldier's own AI, server / single player only:
	/// an instrument-holder gets its Inspiration pool sized and refilled (<see cref="BardCompat.Tick"/>); a heal-weapon holder runs the healer AI
	/// (<see cref="HealerCompat.Tick"/>). Reaches Levy only through its public surface (<c>SoldierNPC.Dummy</c>, <c>Record</c>, <c>WeaponUse</c>).
	/// </summary>
	public class SoldierThoriumGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.ModNPC is SoldierNPC;

		/// <summary>The Inspiration pool has been filled for the first time (a bard starts with a full pool).</summary>
		public bool BardPoolInit;
		/// <summary>Ticks until this soldier's healer AI may cast again.</summary>
		public int HealCooldown;
		/// <summary>Main.GameUpdateCount until which a healer leaves this soldier alone (a heal is already on its way).</summary>
		public uint HealReservedUntil;

		private bool _warned;

		public override void PostAI(NPC npc)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			if (npc.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive || s.Dummy == null) return;
			try
			{
				var gear = s.Record.Gear;
				if (gear == null) return;
				if (BardCompat.IsBardWeapon(gear.Weapon) || BardCompat.IsBardWeapon(gear.Sidearm)) BardCompat.Tick(s, this);
				Item held = WeaponUse.HeldItem(s);
				if (HealerCompat.IsHealWeapon(held)) HealerCompat.Tick(s, held, this);
			}
			catch (Exception e)
			{
				// A Thorium surprise must never take down the soldier's tick: report it once per soldier and carry on.
				if (!_warned) { _warned = true; Mod.Logger.Warn($"LevyThorium: {s.SoldierName} threw in the class driver: {e.Message}"); }
			}
		}
	}
}
