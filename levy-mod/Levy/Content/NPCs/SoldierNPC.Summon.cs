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
	/// Summoner layer: a soldier whose primary weapon is a summon weapon keeps a flock of <see cref="SoldierMinion"/>s instead of firing it.
	/// The flock size is the dummy's minion capacity (armor/accessories raise <c>Player.maxMinions</c>) divided by the weapon's slot cost.
	/// Also owns the cleanup of every soldier-anchored projectile (minions, lashes, boomerangs) when this body dies.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _minionTick, _minionSpawnCd;
		private readonly List<int> _minionScratch = new();

		/// <summary>The summon weapon this soldier maintains a flock for, or null (primary slot only; a sidearm summon staff is ignored).</summary>
		public Item SummonWeapon()
		{
			var g = Record?.Gear;
			if (g == null || !g.HasWeapon || _mainProfile == null || !_mainProfile.IsSummon || _mainProfile.ItemType != g.Weapon.type) return null;
			return g.Weapon;
		}

		/// <summary>Minion slots one minion of this weapon takes.</summary>
		public static float SlotsPerMinion(Item weapon)
		{
			float slots = 0f;
			if (weapon != null && weapon.type > 0 && weapon.type < ItemID.Sets.StaffMinionSlotsRequired.Length) slots = ItemID.Sets.StaffMinionSlotsRequired[weapon.type];
			if (slots <= 0f)
			{
				var sample = WeaponClassifier.ProjectileSample(weapon?.shoot ?? 0);
				slots = sample != null ? sample.minionSlots : 0f;
			}
			return slots > 0.01f ? slots : LevyConstants.MinionDefaultSlots;
		}

		/// <summary>How many minions this soldier's gear supports: the dummy's maxMinions (read after the stat pipeline) over the weapon's slot cost, clamped to [1, cap].</summary>
		public int MinionCapacity(Item weapon)
		{
			float cap = Dummy != null ? Math.Max(1, Dummy.maxMinions) : 1f;
			int n = (int)Math.Floor(cap / SlotsPerMinion(weapon) + 0.001f);
			return Math.Clamp(n, 1, LevyConstants.MinionMaxPerSoldier);
		}

		/// <summary>Live projectiles of <typeparamref name="T"/> anchored to this body (ai[0] == whoAmI).</summary>
		public int CountAnchored<T>() where T : ModProjectile
		{
			int type = ModContent.ProjectileType<T>(), n = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == type && (int)p.ai[0] == NPC.whoAmI) n++;
			}
			return n;
		}

		/// <summary>Kills every minion, lash and boomerang anchored to this body.</summary>
		public void KillAnchoredProjectiles()
		{
			if (LevyUtil.IsClient) return;
			int minion = ModContent.ProjectileType<SoldierMinion>(), whip = ModContent.ProjectileType<SoldierWhip>(), boom = ModContent.ProjectileType<SoldierBoomerang>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || (int)p.ai[0] != NPC.whoAmI) continue;
				if (p.type == minion || p.type == whip || p.type == boom) p.Kill();
			}
		}

		/// <summary>Called every combat tick: tops the flock up to capacity (one summon per cooldown), retunes its damage, and dismisses it when the summon weapon is gone.</summary>
		private void MaintainMinions()
		{
			if (_minionSpawnCd > 0) _minionSpawnCd--;
			if (++_minionTick < LevyConstants.MinionMaintainIntervalTicks) return;
			_minionTick = 0;
			try
			{
				Item weapon = SummonWeapon();
				int type = ModContent.ProjectileType<SoldierMinion>();
				_minionScratch.Clear();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.type == type && (int)p.ai[0] == NPC.whoAmI) _minionScratch.Add(i);
				}
				if (weapon == null || weapon.shoot <= 0)
				{
					foreach (int i in _minionScratch) Main.projectile[i].Kill();
					return;
				}

				int desired = MinionCapacity(weapon);
				int damage = Math.Max(1, (int)Math.Round(WeaponDamage(weapon) * DamageMultiplier));
				for (int k = 0; k < _minionScratch.Count; k++)
				{
					Projectile p = Main.projectile[_minionScratch[k]];
					if (k >= desired) { p.Kill(); continue; }
					p.damage = damage;
					p.knockBack = weapon.knockBack;
					if ((int)p.ai[1] != weapon.shoot) { p.ai[1] = weapon.shoot; p.netUpdate = true; }
				}
				if (_minionScratch.Count >= desired || _minionSpawnCd > 0) return;

				SpawnMinion(weapon, damage);
			}
			catch (Exception e)
			{
				_minionSpawnCd = LevyConstants.MinionSpawnCooldownTicks * 4;
				Levy.Instance?.Logger.Warn("Levy: minion upkeep failed for a soldier: " + e.Message);
			}
		}

		private void SpawnMinion(Item weapon, int damage)
		{
			var offset = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0f)) * LevyConstants.MinionOrbitRadiusPx;
			Projectile p = WeaponUse.Spawn(this, NPC.Center + offset, Vector2.Zero, ModContent.ProjectileType<SoldierMinion>(), damage, weapon.knockBack,
				weapon.DamageType ?? DamageClass.Summon, NPC.whoAmI, weapon.shoot, 0f);
			_minionSpawnCd = LevyConstants.MinionSpawnCooldownTicks;
			if (p == null) return;
			// a short cast animation when the staff is the weapon in hand
			if (!UsingSidearm && ItemAnimation <= 0) { ItemAnimation = ItemAnimationMax = Math.Max(1, weapon.useAnimation); NPC.netUpdate = true; }
			if (weapon.UseSound.HasValue) Terraria.Audio.SoundEngine.PlaySound(weapon.UseSound.Value, NPC.Center);
		}
	}
}
