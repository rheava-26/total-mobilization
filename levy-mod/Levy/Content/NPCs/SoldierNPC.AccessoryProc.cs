using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Accessory "procs": vanilla accessories that spawn a helper projectile from inside <c>Player.UpdateEquips</c>. On the dummy that call runs only
	/// once a second with whoAmI 255, so the vanilla timers never advance and the spawn is guarded off. Here the same vanilla step is run every combat
	/// tick with the dummy borrowing the soldier's owner slot and position; whatever projectile it spawns is adopted (tagged) exactly like a weapon shot.
	///
	/// Only accessories whose projectile does NOT home on the owning player belong here (it would chase the commander). Vetted so far:
	///   Volatile Gelatin - fires a bouncing gel at the nearest hostile within 640 px every 40 calls; plain ballistic AI.
	///   Spore Sac        - drops lingering spore clouds near the wearer; stationary AI, no owner homing.
	/// Rejected: Bone Glove (fires from ItemCheck keyed to the held-item swing, not UpdateEquips) and anything minion/sentry-shaped.
	/// </summary>
	public partial class SoldierNPC
	{
		private static readonly HashSet<int> _accessoryProcWarned = new HashSet<int>();
		private static bool[] _accProjBefore;

		private static readonly int[] ProcAccessories = { ItemID.VolatileGelatin, ItemID.SporeSac };

		/// <summary>Called every combat tick. Never throws; safe to leave on for every soldier.</summary>
		private void MaintainAccessoryProjectiles()
		{
			if (LevyUtil.IsClient || Record == null || Dummy == null) return;
			if (State != SoldierState.Engage) return;
			Item[] procs = null;
			try
			{
				int slots = Math.Min(Record.AccessorySlots, SoldierGear.AccessorySlots);
				for (int i = 0; i < slots; i++)
				{
					Item it = Dummy.armor[3 + i];
					if (it == null || it.IsAir) continue;
					int idx = Array.IndexOf(ProcAccessories, it.type);
					if (idx < 0) continue;
					(procs ??= new Item[ProcAccessories.Length])[idx] = it;
				}
			}
			catch (Exception) { return; }
			if (procs == null) return;
			for (int k = 0; k < procs.Length; k++) if (procs[k] != null) RunAccessoryProc(procs[k]);
		}

		private void RunAccessoryProc(Item item)
		{
			Player d = Dummy;
			bool[] before = _accProjBefore ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;

			int savedWho = d.whoAmI; Vector2 savedCenter = d.Center;
			try
			{
				// The vanilla step spawns with player.whoAmI as the owner and checks Main.myPlayer == whoAmI: borrow the soldier owner slot.
				d.whoAmI = WeaponUse.OwnerFor(this);
				d.Center = NPC.Center;
				switch (item.type)
				{
					case ItemID.VolatileGelatin: d.VolatileGelatin(item); break;
					case ItemID.SporeSac: d.SporeSac(item); break;
				}
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(item.type)) Levy.Instance?.Logger.Warn("Levy: accessory projectile proc failed for item " + item.type + ": " + e.Message);
			}
			finally { d.whoAmI = savedWho; d.Center = savedCenter; }

			try
			{
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || before[i]) continue;
					WeaponUse.TagSoldierProjectile(this, p, p.DamageType);
				}
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(-item.type)) Levy.Instance?.Logger.Warn("Levy: tagging accessory projectile failed for item " + item.type + ": " + e.Message);
			}
		}
	}
}
