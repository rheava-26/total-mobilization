using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.Projectiles;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Stardust armor set bonus (Player.UpdateArmorSets: head 189 + body 190 + legs 130 casts a Stardust Guardian that follows the wearer and punches his enemies). Vanilla gates the cast on
	/// the wearer being the machine's own player, which the soldier's hidden dummy never is (SoldierNPC.UpdateDummyArmorSets borrows a slot nobody holds on purpose), so the cast is made here:
	/// one <see cref="SoldierStardustGuardian"/> per soldier who wears the full set, kept up by <see cref="MaintainStardustGuardian"/> (called every combat tick from the accessory upkeep, whether or not
	/// he is fighting) and ended by the Guardian itself the moment its soldier dies or no longer wears the set. Server / single player only; remote screens see the synced projectile.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _guardianTick, _guardianSpawnCd;
		/// <summary>True once this soldier has worn the set: only then is there ever a Guardian of his to look for, so every other soldier skips the projectile scan altogether.</summary>
		private bool _guardianScanOwed;
		private readonly List<int> _guardianScratch = new();

		/// <summary>Full Stardust: Stardust Helmet + Stardust Plate + Stardust Leggings (the head, body and leg slots Player.UpdateArmorSets matches). Read off the gear, not the dummy, so it needs no stat refresh to be right.</summary>
		public bool WearsStardustSet
		{
			get
			{
				var g = Record?.Gear;
				if (g == null || g.Head == null || g.Body == null || g.Legs == null) return false;
				if (g.Head.IsAir || g.Body.IsAir || g.Legs.IsAir) return false;
				return g.Head.headSlot == ArmorIDs.Head.StardustHelmet && g.Body.bodySlot == ArmorIDs.Body.StardustPlate && g.Legs.legSlot == ArmorIDs.Legs.StardustLeggings;
			}
		}

		/// <summary>
		/// What this soldier's Guardian punches for: vanilla's cast damage (30) with the soldier's own summon damage applied, then his grade and the config multiplier, the same shape the Solar
		/// flare and the Titanium shards take. A vanilla Guardian is a minion and re-derives this from its owner every tick; a soldier's is owned by the commander, so the soldier's number is set on it directly.
		/// </summary>
		public int StardustGuardianDamage()
		{
			Player d = Dummy;
			float raw = d != null ? d.GetTotalDamage(DamageClass.Summon).ApplyTo(LevyConstants.StardustGuardianBaseDamage) : LevyConstants.StardustGuardianBaseDamage;
			return Math.Max(1, (int)Math.Round(raw * DamageMultiplier));
		}

		/// <summary>
		/// Called every combat tick (server / single player). On the maintain interval: set worn and alive means exactly one Guardian (extras are removed, a missing one is cast after a short
		/// pause, the live one is retuned to the soldier's current damage); set off or dead means none. The Guardian also ends itself in those cases, so a missed beat strands nothing.
		/// </summary>
		private void MaintainStardustGuardian()
		{
			if (_guardianSpawnCd > 0) _guardianSpawnCd--;
			if (++_guardianTick < LevyConstants.StardustGuardianMaintainIntervalTicks) return;
			_guardianTick = 0;
			bool wears = IsAlive && WearsStardustSet;
			if (!wears && !_guardianScanOwed) return;
			try
			{
				int type = ModContent.ProjectileType<SoldierStardustGuardian>();
				_guardianScratch.Clear();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.type == type && (int)p.ai[0] == NPC.whoAmI) _guardianScratch.Add(i);
				}
				if (!wears)
				{
					foreach (int i in _guardianScratch) Main.projectile[i].Kill();
					_guardianScanOwed = false;
					return;
				}
				_guardianScanOwed = true;

				int damage = StardustGuardianDamage();
				for (int k = 0; k < _guardianScratch.Count; k++)
				{
					Projectile p = Main.projectile[_guardianScratch[k]];
					if (k > 0) { p.Kill(); continue; } // keep the first, drop duplicates
					p.damage = p.originalDamage = damage;
					p.knockBack = LevyConstants.StardustGuardianKnockback;
				}
				if (_guardianScratch.Count > 0 || _guardianSpawnCd > 0) return;

				_guardianSpawnCd = LevyConstants.StardustGuardianSpawnCooldownTicks;
				Projectile guardian = WeaponUse.Spawn(this, NPC.Center + new Vector2(0f, -LevyConstants.StardustGuardianRestLiftPx), Vector2.Zero, type, damage,
					LevyConstants.StardustGuardianKnockback, DamageClass.Summon, NPC.whoAmI);
				if (guardian == null) return; // the projectile table is full: try again after the pause
				guardian.originalDamage = damage;
				guardian.netUpdate = true;
				if (SetBonusLog.First("guardian:spawned")) SetBonusLog.Write("Stardust Guardian spawned for soldier '" + SoldierName + "' (projectile " + guardian.whoAmI + ", " + damage + " damage a punch); the set is worn in full.");
			}
			catch (Exception e)
			{
				_guardianSpawnCd = LevyConstants.StardustGuardianSpawnCooldownTicks * 4;
				if (_accessoryProcWarned.Add(-ItemID.StardustBreastplate)) Levy.Instance?.Logger.Warn("Levy: Stardust Guardian upkeep failed for a soldier: " + e.Message);
			}
		}
	}
}
