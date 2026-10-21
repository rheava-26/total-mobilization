using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
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

		private static List<Projectile> _sporeParked;

		/// <summary>
		/// Runs the vanilla Spore Sac step for this body alone. Vanilla caps traps at 10 and keeps them 48 px apart by counting every trap the owner owns, and every soldier
		/// shares the commander's owner slot, so a squad of sac-wearers would share one cap of 10 (with the commander's own). Other bodies' traps are parked on a slot no
		/// else owns for the length of the call, so each soldier gets the vanilla cap and spacing to itself.
		/// </summary>
		private void SporeSacOwnTraps(Player d, Item item)
		{
			List<Projectile> parked = _sporeParked ??= new List<Projectile>();
			parked.Clear();
			int owner = d.whoAmI;
			int parkSlot = owner == Main.maxPlayers ? 0 : Main.maxPlayers; // any slot but the shared owner's (a dedicated server's owner IS 255); vanilla only counts owner == whoAmI
			try
			{
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.owner != owner || p.aiStyle != ProjAIStyleID.SporeTrap) continue;
					if (p.TryGetGlobalProjectile(out Common.Globals.SoldierProjectileTag tag) && tag.SoldierIndex == NPC.whoAmI) continue;
					p.owner = parkSlot;
					parked.Add(p);
				}
				d.SporeSac(item);
			}
			finally
			{
				foreach (Projectile p in parked) p.owner = owner;
				parked.Clear();
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Spectre Mask set bonus (Player.ghostHurt). Vanilla spawns the bolt in Projectile.Damage when Main.player[owner].ghostHurt is set, but only on the
		// !npcProj path, and the owner of a soldier shot is the commander. So the dummy's own flag (set by UpdateArmorSets in RefreshStats) is read here instead,
		// on every hit of a soldier's projectile, and the vanilla Projectile.ghostHurt does the rest (magic-only, nearest-with-line-of-sight pick, 4-px/tick launch).
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>This body's own stand-in for Player.ghostDmg (vanilla's shared cooldown pool) and the tick it was last decayed at.</summary>
		private float _ghostDmg;
		private uint _ghostDmgAt;

		/// <summary>
		/// Called for every enemy hit by one of this soldier's projectiles (server / single player). When the soldier wears the Spectre Mask set and the hit was
		/// magic, runs vanilla's ghostHurt with this soldier's own cooldown pool (so a squad does not share the commander's 1500-damage cap) and adopts the bolt.
		/// </summary>
		public void SpectreWrathOnHit(Projectile source, NPC target, int damageDone)
		{
			if (LevyUtil.IsClient || Dummy == null || !Dummy.ghostHurt) return;
			// Vanilla's own gate for the on-hit effects: a real hit on a living, hurtable enemy, from a friendly shot that is not a wrath bolt itself.
			if (damageDone <= 1 || target.lifeMax <= 5 || target.immortal || !target.canGhostHeal || !source.friendly || source.hostile || source.aiStyle == ProjAIStyleID.SpectreWrath) return;

			bool[] before = _accProjBefore ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;

			uint now = Main.GameUpdateCount;
			if (now > _ghostDmgAt) _ghostDmg = Math.Max(0f, _ghostDmg - (now - _ghostDmgAt) * LevyConstants.GhostDmgDecayPerTick);
			_ghostDmgAt = now;

			Player pool = Main.player[Main.myPlayer]; // ghostHurt reads and charges Main.player[Main.myPlayer].ghostDmg: lend it this body's pool for the call
			float commanderPool = pool.ghostDmg;
			try
			{
				pool.ghostDmg = _ghostDmg;
				source.ghostHurt(damageDone, target.Center, target);
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(ItemID.SpectreMask)) Levy.Instance?.Logger.Warn("Levy: Spectre Mask bolt failed on a soldier: " + e.Message);
			}
			finally { _ghostDmg = pool.ghostDmg; pool.ghostDmg = commanderPool; }

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || before[i]) continue;
				WeaponUse.TagSoldierProjectile(this, p, DamageClass.Magic);
			}
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
					case ItemID.SporeSac: SporeSacOwnTraps(d, item); break;
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
