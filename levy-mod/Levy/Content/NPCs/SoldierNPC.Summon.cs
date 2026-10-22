using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.Projectiles;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Summoner layer: a soldier whose primary weapon is a summon weapon keeps a flock of minions instead of firing it. The minions are the staff's REAL vanilla projectiles, driven
	/// with the soldier as their owner (see <see cref="SoldierMinions"/>); a minion that cannot be driven that way (modded ones, the Stardust Guardian) is a <see cref="SoldierMinion"/> stand-in.
	/// The flock size is the dummy's minion capacity (armor/accessories raise <c>Player.maxMinions</c>) divided by the weapon's slot cost.
	/// Sentry layer: a soldier carrying a sentry weapon plants ONE tower where it fights and keeps it standing (MaintainSentries).
	/// Also owns the cleanup of every soldier-anchored projectile (minions, lashes, boomerangs, sentries) when this body dies.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _minionTick, _minionSpawnCd;
		private readonly List<int> _minionScratch = new();
		/// <summary>The game tick this soldier's dummy minion counters were last reset on (SoldierMinions.PrepareTick).</summary>
		internal uint MinionPrepStamp = uint.MaxValue;

		/// <summary>The summon weapon this soldier maintains a flock for, or null: the primary if it summons, else a summon staff carried as the sidearm.</summary>
		public Item SummonWeapon()
		{
			var g = Record?.Gear;
			if (g == null) return null;
			if (g.HasWeapon && _mainProfile != null && _mainProfile.IsSummon && _mainProfile.ItemType == g.Weapon.type) return g.Weapon;
			if (g.HasSidearm && _sideProfile != null && _sideProfile.IsSummon && _sideProfile.ItemType == g.Sidearm.type) return g.Sidearm;
			return null;
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

		/// <summary>Kills every minion (real and stand-in), lash, boomerang, yoyo, flail and Crystal Leaf anchored to this body.</summary>
		public void KillAnchoredProjectiles()
		{
			if (LevyUtil.IsClient) return;
			int minion = ModContent.ProjectileType<SoldierMinion>(), whip = ModContent.ProjectileType<SoldierWhip>(), boom = ModContent.ProjectileType<SoldierBoomerang>(), yoyo = ModContent.ProjectileType<SoldierYoyo>(), flail = ModContent.ProjectileType<SoldierFlail>(), leaf = ModContent.ProjectileType<SoldierCrystalLeaf>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || (int)p.ai[0] != NPC.whoAmI) continue;
				if (p.type == minion || p.type == whip || p.type == boom || p.type == yoyo || p.type == flail || p.type == leaf) p.Kill();
			}
			SoldierMinions.KillAll(this);
			KillSentries();
		}

		/// <summary>
		/// Called every combat tick: tops the flock up to capacity (one summon per cooldown), retunes its damage, and dismisses it when the summon weapon is gone.
		/// A vanilla minion staff fields the staff's real projectiles (<see cref="SoldierMinions"/>); anything else keeps the <see cref="SoldierMinion"/> stand-in.
		/// </summary>
		private void MaintainMinions()
		{
			if (_minionSpawnCd > 0) _minionSpawnCd--;
			if (++_minionTick < LevyConstants.MinionMaintainIntervalTicks) return;
			_minionTick = 0;
			try
			{
				Item weapon = SummonWeapon();
				bool has = weapon != null && weapon.shoot > 0;
				bool driven = has && SoldierMinions.CanDrive(weapon.shoot);
				int standIn = ModContent.ProjectileType<SoldierMinion>(), me = NPC.whoAmI;
				_minionScratch.Clear();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active) continue;
					bool fake = p.type == standIn && (int)p.ai[0] == me;
					bool real = !fake && SoldierMinions.OwnedBy(p, me);
					if (!fake && !real) continue;
					// no summon weapon any more, or a flock of the other kind / of a staff this soldier no longer carries: dismissed
					if (!has || (fake && driven) || (real && !(driven && SoldierMinions.InFamily(weapon.shoot, p.type)))) { if (real) SoldierMinions.Dismiss(p); else p.Kill(); continue; }
					_minionScratch.Add(i);
				}
				if (!has) return;

				Item held = Dummy != null ? Dummy.inventory[0] : null; // WeaponDamage parks the weapon in the dummy's hand: put the real hand item back (matters for a sidearm staff)
				int damage = Math.Max(1, (int)Math.Round(WeaponDamage(weapon) * DamageMultiplier));
				if (Dummy != null && held != null) Dummy.inventory[0] = held;

				if (driven) { MaintainRealMinions(weapon, damage); return; }

				int desired = MinionCapacity(weapon);
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

		/// <summary>
		/// The real-minion flock (already collected into <c>_minionScratch</c>): retunes every minion's damage, dismisses any overflow (by slot total, so a half-slot pair counts right),
		/// keeps a controller staff's companion (Desert Tiger, Abigail) right, and casts one more when a whole cast still fits.
		/// </summary>
		private void MaintainRealMinions(Item weapon, int damage)
		{
			int desired = SoldierMinions.Capacity(this, weapon);
			float perCast = SlotsPerMinion(weapon), want = desired * perCast, have = 0f;
			bool dragon = weapon.shoot == ProjectileID.StardustDragon1;
			// A Stardust Dragon is ONE chain (head, 2 bodies per cast, tail): a segment killed by anything else leaves the rest following a dead link, so a broken chain is torn down and recast whole.
			if (dragon && !SoldierMinions.DragonIntact(this, _minionScratch))
			{
				foreach (int i in _minionScratch) SoldierMinions.Dismiss(Main.projectile[i]);
				_minionScratch.Clear();
			}
			foreach (int i in _minionScratch)
			{
				Projectile p = Main.projectile[i];
				SoldierMinions.Retune(this, p, weapon, damage);
				have += p.minionSlots;
			}
			if (dragon) have = SoldierMinions.TrimDragon(_minionScratch, have, want); // shortened body by body from the tail end, never below one cast
			while (!dragon && have > want + LevyConstants.MinionSlotEpsilon)
			{
				int worst = -1;
				for (int k = 0; k < _minionScratch.Count; k++)
				{
					Projectile p = Main.projectile[_minionScratch[k]];
					if (p.active && p.minionSlots > 0f && (worst < 0 || _minionScratch[k] > _minionScratch[worst])) worst = k;
				}
				if (worst < 0) break;
				Projectile q = Main.projectile[_minionScratch[worst]];
				have -= q.minionSlots;
				SoldierMinions.Dismiss(q);
				_minionScratch.RemoveAt(worst);
			}
			SoldierMinions.MaintainCompanion(this, weapon, _minionScratch);
			// the count guard keeps a staff whose minion reports no slot cost from casting without end
			if (_minionSpawnCd > 0 || have + perCast > want + LevyConstants.MinionSlotEpsilon || _minionScratch.Count >= LevyConstants.MinionControllerMaxPerSoldier * 2) return;

			_minionSpawnCd = LevyConstants.MinionSpawnCooldownTicks;
			if (!SoldierMinions.Summon(this, weapon, damage)) return;
			// a short cast animation when the staff is the weapon in hand
			if (!UsingSidearm && ItemAnimation <= 0 && ReferenceEquals(weapon, Record?.Gear?.Weapon)) { ItemAnimation = ItemAnimationMax = Math.Max(1, weapon.useAnimation); NPC.netUpdate = true; }
			if (weapon.UseSound.HasValue) Terraria.Audio.SoundEngine.PlaySound(weapon.UseSound.Value, NPC.Center);
		}

		private void SpawnMinion(Item weapon, int damage)
		{
			var offset = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0f)) * LevyConstants.MinionOrbitRadiusPx;
			Projectile p = WeaponUse.Spawn(this, NPC.Center + offset, Vector2.Zero, ModContent.ProjectileType<SoldierMinion>(), damage, weapon.knockBack,
				weapon.DamageType ?? DamageClass.Summon, NPC.whoAmI, weapon.shoot, 0f);
			_minionSpawnCd = LevyConstants.MinionSpawnCooldownTicks;
			if (p == null) return;
			// a short cast animation when the staff is the weapon in hand
			if (!UsingSidearm && ItemAnimation <= 0 && ReferenceEquals(weapon, Record?.Gear?.Weapon)) { ItemAnimation = ItemAnimationMax = Math.Max(1, weapon.useAnimation); NPC.netUpdate = true; }
			if (weapon.UseSound.HasValue) Terraria.Audio.SoundEngine.PlaySound(weapon.UseSound.Value, NPC.Center);
		}

		// ------------------------------------------------------------------------------------------------------------
		// Sentries
		//
		// A soldier's sentry is spawned DIRECTLY as the item's projectile (never through the item's use/Shoot code, which ends in
		// Player.UpdateMaxTurrets and would cull towers), owned by the commander like every soldier shot and tagged with the soldier.
		// Vanilla only culls sentries inside Player.UpdateMaxTurrets (the commander placing one, or maxTurrets changing), and it counts every
		// sentry the commander owns. So every soldier sentry is PARKED (Projectile.sentry = false) for the length of the commander's own
		// Player.Update (LevyPlayer.PreUpdate/PostUpdate -> SoldierProjectileTag.Park/RestoreSoldierSentries): the commander's cap sees only the
		// commander's towers, soldier towers never count against it and are never culled by it.
		// ------------------------------------------------------------------------------------------------------------
		private int _sentryTick, _sentryCd, _sentryProj = -1, _sentryBorn;
		private readonly List<int> _sentryScratch = new();

		/// <summary>The sentry weapon this soldier keeps a tower for, or null: the primary if it is a sentry, else a sentry carried as the sidearm.</summary>
		public Item SentryWeapon()
		{
			var g = Record?.Gear;
			if (g == null) return null;
			if (g.HasWeapon && _mainProfile != null && _mainProfile.IsSentry && _mainProfile.ItemType == g.Weapon.type) return g.Weapon;
			if (g.HasSidearm && _sideProfile != null && _sideProfile.IsSentry && _sideProfile.ItemType == g.Sidearm.type) return g.Sidearm;
			return null;
		}

		/// <summary>A live sentry projectile planted by this soldier.</summary>
		private bool IsMySentry(Projectile p) => p.active && p.sentry && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == NPC.whoAmI;

		/// <summary>Removes every sentry this soldier planted.</summary>
		public void KillSentries()
		{
			if (LevyUtil.IsClient) return;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (IsMySentry(p)) p.Kill();
			}
			_sentryProj = -1;
		}

		/// <summary>Fighting now, or within the grace after the last engagement.</summary>
		private bool SentryInCombat() => State == SoldierState.Engage || State == SoldierState.Shaken || (int)Main.GameUpdateCount - _lastEngageTick < LevyConstants.SentryCombatGraceTicks;

		/// <summary>Called every combat tick: keeps the standing sentry alive, and (on the maintain interval) plants one when there is none, or a fresh one when the soldier has moved far from it.</summary>
		private void MaintainSentries()
		{
			if (_sentryCd > 0) _sentryCd--;
			// Lifetime is driven by us, every tick: a sentry never times out while its soldier lives.
			if (_sentryProj >= 0)
			{
				Projectile q = Main.projectile[_sentryProj];
				if (IsMySentry(q)) { if (q.timeLeft < Projectile.SentryLifeTime) q.timeLeft = Projectile.SentryLifeTime; }
				else _sentryProj = -1;
			}
			if (++_sentryTick < LevyConstants.SentryMaintainIntervalTicks) return;
			_sentryTick = 0;
			try
			{
				Item weapon = SentryWeapon();
				_sentryScratch.Clear();
				for (int i = 0; i < Main.maxProjectiles; i++)
					if (IsMySentry(Main.projectile[i])) _sentryScratch.Add(i);
				if (weapon == null || weapon.shoot <= 0)
				{
					foreach (int i in _sentryScratch) Main.projectile[i].Kill();
					_sentryProj = -1;
					return;
				}

				// Keep the first sentry of the weapon's type; anything else (a swapped weapon's old tower, a duplicate) goes.
				Projectile keep = null;
				int kept = 0;
				foreach (int i in _sentryScratch)
				{
					Projectile p = Main.projectile[i];
					if (p.type != weapon.shoot || kept >= LevyConstants.SentryMaxPerSoldier) { p.Kill(); continue; }
					kept++;
					keep ??= p;
				}

				bool inCombat = SentryInCombat();
				if (keep != null)
				{
					_sentryProj = keep.whoAmI;
					keep.originalDamage = SentryDamage(weapon);
					keep.knockBack = weapon.knockBack;
					bool far = Vector2.DistanceSquared(keep.Center, NPC.Center) > Sq(LevyConstants.SentryReplaceDistTiles * LevyConstants.TilePx);
					bool stale = Main.netMode != NetmodeID.SinglePlayer && (int)Main.GameUpdateCount - _sentryBorn > LevyConstants.SentryMpRefreshTicks;
					if (!((far || stale) && inCombat && _sentryCd <= 0)) return;
					DeploySentry(weapon, keep);
					return;
				}

				_sentryProj = -1;
				if (_sentryCd > 0 || !inCombat || Record.HoldFire) return;
				DeploySentry(weapon, null);
			}
			catch (Exception e)
			{
				_sentryCd = LevyConstants.SentryReplaceCooldownTicks;
				Levy.Instance?.Logger.Warn("Levy: sentry upkeep failed for a soldier: " + e.Message);
			}
		}

		private static float Sq(float v) => v * v;

		/// <summary>
		/// The sentry projectile's base damage. A vanilla sentry re-derives its damage every tick as the OWNER's (the commander's) summon damage applied to
		/// originalDamage, so the commander's own summon bonus is divided back out here: the soldier's gear sets what the tower deals.
		/// </summary>
		private int SentryDamage(Item weapon)
		{
			Item held = Dummy != null ? Dummy.inventory[0] : null; // WeaponDamage parks the weapon in the dummy's hand: put the real hand item back (matters for a sidearm)
			int raw = Math.Max(1, (int)Math.Round(WeaponDamage(weapon) * DamageMultiplier));
			if (Dummy != null && held != null) Dummy.inventory[0] = held;
			int o = WeaponUse.OwnerFor(this);
			float ratio = 1f;
			if (o >= 0 && o < Main.player.Length && Main.player[o] != null)
				ratio = Main.player[o].GetTotalDamage(DamageClass.Summon).ApplyTo(100f) / 100f;
			ratio = Math.Clamp(ratio, LevyConstants.SentryOwnerRatioMin, LevyConstants.SentryOwnerRatioMax);
			return Math.Max(1, (int)Math.Round(raw / ratio));
		}

		/// <summary>Plants the sentry where the soldier stands (the old one is removed only once the new spot is known to work). Returns false and backs off when no spot was found.</summary>
		private bool DeploySentry(Item weapon, Projectile replacing)
		{
			if (!PlanSentry(weapon, out Vector2 pos, out Vector2 vel, out float ai0, out float ai1, out float localAi0))
			{
				_sentryCd = LevyConstants.SentryRetryTicks;
				return false;
			}
			replacing?.Kill();
			_sentryProj = -1;
			Projectile p = WeaponUse.Spawn(this, pos, vel, weapon.shoot, SentryDamage(weapon), weapon.knockBack, DamageClass.Summon, ai0, ai1, 0f);
			if (p == null) { _sentryCd = LevyConstants.SentryRetryTicks; return false; }
			if (localAi0 != 0f) p.localAI[0] = localAi0;
			p.timeLeft = Projectile.SentryLifeTime;
			p.netUpdate = true;
			_sentryProj = p.whoAmI;
			_sentryBorn = (int)Main.GameUpdateCount;
			_sentryCd = LevyConstants.SentryReplaceCooldownTicks;
			// a short cast animation when the sentry weapon is the item in hand
			if (!UsingSidearm && ItemAnimation <= 0 && ReferenceEquals(weapon, Record?.Gear?.Weapon)) { ItemAnimation = ItemAnimationMax = Math.Max(1, weapon.useAnimation); NPC.netUpdate = true; }
			if (weapon.UseSound.HasValue) Terraria.Audio.SoundEngine.PlaySound(weapon.UseSound.Value, NPC.Center);
			return true;
		}

		/// <summary>
		/// Where and how the sentry goes down, mirroring what Player.ItemCheck_Shoot hands NewProjectile for the vanilla towers, with the soldier's feet standing in for the cursor:
		/// DD2 towers snap to the ground below with Player.FindSentryRestingSpot's offsets; the staff sentries (Frost Hydra, Queen Spider, Houndius Shootius) drop from just above the
		/// ground, the Rainbow Crystal / Lunar Portal staffs hover beside the soldier; any other (modded) sentry rests on the ground below, centred on its own hitbox.
		/// Returns false when there is no ground in reach or the spot is blocked.
		/// </summary>
		private bool PlanSentry(Item weapon, out Vector2 pos, out Vector2 vel, out float ai0, out float ai1, out float localAi0)
		{
			pos = NPC.Center; vel = Vector2.Zero; ai0 = ai1 = localAi0 = 0f;
			int shoot = weapon.shoot;
			Projectile sample = WeaponClassifier.ProjectileSample(shoot);
			if (sample == null) return false;
			int dir = NPC.direction == 0 ? 1 : NPC.direction;
			Player pl = Dummy ?? Main.LocalPlayer; // the vanilla helpers read set-bonus flags off a player
			bool dd2 = shoot > 0 && shoot < ProjectileID.Sets.IsADD2Turret.Length && ProjectileID.Sets.IsADD2Turret[shoot];
			bool air = false, staffDrop = false;
			switch (weapon.type)
			{
				case ItemID.DD2FlameburstTowerT1Popper: ai0 = 1f; ai1 = 80f; break;
				case ItemID.DD2FlameburstTowerT2Popper: ai0 = 1f; ai1 = 70f; break;
				case ItemID.DD2FlameburstTowerT3Popper: ai0 = 1f; ai1 = 60f; break;
				case ItemID.DD2BallistraTowerT1Popper:
				case ItemID.DD2BallistraTowerT2Popper:
				case ItemID.DD2BallistraTowerT3Popper: ai0 = 1f; ai1 = Projectile.GetBallistraShotDelay(pl); break;
				case ItemID.DD2ExplosiveTrapT1Popper:
				case ItemID.DD2ExplosiveTrapT2Popper:
				case ItemID.DD2ExplosiveTrapT3Popper: localAi0 = Projectile.GetExplosiveTrapCooldown(pl); break;
				case ItemID.StaffoftheFrostHydra: staffDrop = true; ai0 = 60f; break;
				case ItemID.QueenSpiderStaff: staffDrop = true; break;
				case ItemID.HoundiusShootius: staffDrop = true; ai0 = 90f; break;
				case ItemID.RainbowCrystalStaff:
				case ItemID.MoonlordTurretStaff: staffDrop = true; air = true; break;
			}
			if (staffDrop) vel = new Vector2(0f, 15f);

			var half = new Vector2(sample.width * 0.5f, sample.height * 0.5f);
			if (air)
			{
				pos = new Vector2(NPC.Center.X, NPC.Center.Y - 24f);
				return !Collision.SolidCollision(pos - half, sample.width, sample.height);
			}

			int tx = (int)(NPC.Center.X / LevyConstants.TilePx);
			int startRow = (int)((NPC.Bottom.Y - 2f) / LevyConstants.TilePx);
			if (!FindStandRow(tx, startRow, out int row)) return false;
			if (dd2)
			{
				int worldX = tx * 16 + 8, push = 41;
				switch (shoot)
				{
					case 663: case 677: case 678: worldX += dir; break;
					case 665: push += 2; break;
					case 667: push += 3; break;
					case 691: case 692: case 693: push = 22; worldX += dir; break;
				}
				push -= 14;
				pos = new Vector2(worldX, row * 16 - push);
				// the same open-air test the vanilla use check applies to the cell above the resting row
				int cx = worldX / 16, cy = row - 1;
				bool aura = shoot == 688 || shoot == 689 || shoot == 690;
				return aura ? !Collision.SolidTiles(cx, cx, cy - 2, cy) : !WorldGen.SolidTile(cx, cy);
			}
			float y = staffDrop ? row * 16 - 40 : row * 16 - (float)Math.Ceiling(sample.height / 2f);
			pos = new Vector2(NPC.Center.X, y);
			return !Collision.SolidCollision(pos - half, sample.width, sample.height);
		}

		/// <summary>The first row at or below <paramref name="startRow"/> that a sentry can rest on in the three columns around <paramref name="tx"/> (solid tiles and platforms), within the search depth.</summary>
		private static bool FindStandRow(int tx, int startRow, out int row)
		{
			int last = Math.Min(Main.maxTilesY - 10, startRow + LevyConstants.SentryGroundSearchTiles);
			for (int ty = Math.Max(1, startRow); ty < last; ty++)
			{
				if (!WorldGen.InWorld(tx, ty, 6)) break;
				if (CanRestOn(tx, ty) || CanRestOn(tx - 1, ty) || CanRestOn(tx + 1, ty)) { row = ty; return true; }
			}
			row = 0;
			return false;
		}

		private static bool CanRestOn(int x, int y)
		{
			Tile t = Main.tile[x, y];
			return t.HasUnactuatedTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);
		}
	}
}
