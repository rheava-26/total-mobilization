using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Boss;
using Levy.Common.Dialogue;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.Projectiles;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Combat layer: weapon profile in hand, sidearm switching, cooldowns and held-item animation, mana, ranged/magic firing through
	/// WeaponUse, broadsword swing hitboxes and thrust bookkeeping. Runs on the server/single player every tick. OWNED BY THE WEAPONS AGENT.
	/// </summary>
	public partial class SoldierNPC
	{
		private WeaponProfile _mainProfile, _sideProfile;
		private readonly HashSet<int> _swingHits = new();
		private int _manaTick, _sidearmTick, _lastEngageTick = -100000, _thrustIndex = -1;

		// ------------------------------------------------------------------------------------------------------------
		// Gear
		// ------------------------------------------------------------------------------------------------------------
		partial void Combat_OnGearChanged()
		{
			if (Record == null) { Weapon = null; UsingSidearm = false; return; }
			var g = Record.Gear;
			// unsupported items keep a profile (Supported == false) so the stale check below stays quiet; they are never usable
			_mainProfile = g.HasWeapon ? WeaponClassifier.GetProfile(g.Weapon) : null;
			_sideProfile = g.HasSidearm ? WeaponClassifier.GetProfile(g.Sidearm) : null;
			bool sideUsable = _sideProfile != null && WeaponUse.HasResources(this, g.Sidearm, _sideProfile);
			bool mainUsable = _mainProfile != null && WeaponUse.HasResources(this, g.Weapon, _mainProfile) && !(_mainProfile.IsSummon && sideUsable); // a summon weapon is not swung: the sidearm is the hand weapon
			UsingSidearm = !mainUsable && sideUsable;
			Weapon = SupportedOrNull(UsingSidearm ? _sideProfile : _mainProfile);
			ClearSwingHits();
			_sidearmTick = 0;
			StatsDirty = true;
			NPC.netUpdate = true;
		}

		/// <summary>Re-evaluates main weapon vs sidearm from ammo and mana; switches the profile in hand when needed.</summary>
		private void UpdateSidearm()
		{
			var g = Record.Gear;
			// gear swapped without a Combat_OnGearChanged call: rebuild the profiles
			if ((g.HasWeapon ? g.Weapon.type : 0) != (_mainProfile?.ItemType ?? 0) || (g.HasSidearm ? g.Sidearm.type : 0) != (_sideProfile?.ItemType ?? 0)) { Combat_OnGearChanged(); return; }
			bool sideUsable = _sideProfile != null && g.HasSidearm && WeaponUse.HasResources(this, g.Sidearm, _sideProfile);
			bool mainUsable = _mainProfile != null && g.HasWeapon && WeaponUse.HasResources(this, g.Weapon, _mainProfile) && !(_mainProfile.IsSummon && sideUsable);
			bool wantSidearm = !mainUsable && sideUsable;
			WeaponProfile want = SupportedOrNull(wantSidearm ? _sideProfile : _mainProfile);
			if (wantSidearm != UsingSidearm || !ReferenceEquals(want, Weapon))
			{
				UsingSidearm = wantSidearm;
				Weapon = want;
				ClearSwingHits();
				if (ItemAnimation > 0) ItemAnimation = 0;
				StatsDirty = true;
				NPC.netUpdate = true;
			}
		}

		private static WeaponProfile SupportedOrNull(WeaponProfile p) => p != null && p.Supported ? p : null;

		/// <summary>True while the weapon in hand can be used (ammo in the pouch, mana for magic).</summary>
		public bool CanUseHeldWeapon()
		{
			if (Record == null || Weapon == null) return false;
			return WeaponUse.HasResources(this, WeaponUse.HeldItem(this), Weapon);
		}

		// ------------------------------------------------------------------------------------------------------------
		// Per tick
		// ------------------------------------------------------------------------------------------------------------
		partial void Combat_Update()
		{
			if (Record == null) return;
			if (AttackCooldown > 0) AttackCooldown--;
			if (ItemAnimation > 0)
			{
				ItemAnimation--;
				if (ItemAnimation <= 0)
				{
					ItemAnimation = 0;
					ClearSwingHits();
					if (Weapon != null && Weapon.ReuseDelay > 0) AttackCooldown = Math.Max(AttackCooldown, Weapon.ReuseDelay);
				}
			}
			if (State == SoldierState.Engage) _lastEngageTick = (int)Main.GameUpdateCount;
			RegenMana();
			MaintainMinions();
			MaintainAccessoryProjectiles();
			if (++_sidearmTick >= LevyConstants.SidearmCheckIntervalTicks) { _sidearmTick = 0; UpdateSidearm(); }

			bool fighting = State == SoldierState.Engage || State == SoldierState.Shaken || State == SoldierState.Routed; // a routed body backpedals and keeps shooting
			if (!fighting || Record.HoldFire) return;
			NPC target;
			if (_focusBare && State != SoldierState.Routed && SoldierTargeting.FocusPositionFor(this, out Vector2 focusPos))
			{
				// Focus Attack on a bare spot: the brain engaged the position itself. Fire at it through a stand-in target (ranged weapons only).
				if (Weapon == null || !Weapon.Supported || !Weapon.IsRanged) return;
				target = SoldierTargeting.FocusPhantom(focusPos, NPC.Center);
			}
			else
			{
				if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) return;
				target = Main.npc[TargetIndex];
				// (a critter inside the commander's Focus Attack radius is the only non-hostile a soldier will shoot)
				if (!target.active || (!LevyUtil.IsHostileTarget(target) && !SoldierTargeting.FocusAllowsCritter(this, target))) return;
			}
			if (Weapon == null || !Weapon.Supported) return;
			if (Weapon.IsSummon) return; // a summoner with no hand weapon stands off while the flock fights

			NPC seg = Aiming.NearestSegment(target, NPC.Center) ?? target;
			if (Math.Abs(seg.Center.X - NPC.Center.X) > 2f) NPC.direction = seg.Center.X < NPC.Center.X ? -1 : 1;

			if (Weapon.IsRanged)
			{
				float tiles = Aiming.TilesToHitbox(NPC.Center, seg);
				if (tiles > Weapon.RangeTiles) return;
				Vector2 muzzle = WeaponUse.Muzzle(this);
				if (!ShotIgnoresTiles() && !Aiming.HasLineOfSight(muzzle, seg.Center)) return;
				// Routed shots are wilder: the shaken multiplier (already in the weapon layer while the meter is up) is topped up to RoutedAimMult.
				float extra = 1f;
				if (State == SoldierState.Routed) extra = Shaken ? LevyConstants.RoutedAimMult / LevyConstants.ShakenAimMult : LevyConstants.RoutedAimMult;
				Aiming.ExtraErrorScale = extra;
				bool fired;
				try { fired = WeaponUse.TryAttack(this, target); }
				finally { Aiming.ExtraErrorScale = 1f; }
				if (!fired && AttackCooldown <= 0) UpdateSidearm();
				return;
			}

			// melee
			if (Weapon.Family == WeaponFamily.Broadsword && ItemAnimation > 0) ProcessSwing(target);
			float meleeTiles = Aiming.TilesToHitbox(NPC.Center, seg);
			float blowReach = Weapon.MeleeReachTiles;
			float closeTrigger = blowReach + LevyConstants.MeleeEngageExtraTiles;
			if (Weapon.FiresProjectileMelee)
			{
				// Swing from out to the beam's reach so the projectile flies; up close the swing hitbox lands as always (no line of sight needed there).
				if (meleeTiles <= closeTrigger) { WeaponUse.TryAttack(this, target); return; }
				if (meleeTiles > Weapon.RangeTiles) return;
				if (!ShotIgnoresTiles() && !Aiming.HasLineOfSight(WeaponUse.Muzzle(this), seg.Center)) return;
				WeaponUse.TryAttack(this, target);
				return;
			}
			// Pure melee: begin the swing/thrust as the enemy closes in, a little before it is strictly in reach (whips keep their lash for real targets).
			float trigger = Weapon.IsTethered ? closeTrigger : Math.Max(closeTrigger, blowReach * LevyConstants.MeleePreSwingReachMult);
			if (meleeTiles <= trigger) WeaponUse.TryAttack(this, target);
		}

		/// <summary>The projectile the weapon in hand would fire passes through tiles (no line-of-sight check needed).</summary>
		private bool ShotIgnoresTiles()
		{
			int projType = 0;
			if (Weapon.UsesAmmo)
			{
				var pouch = Record.Gear.Ammo; Item held = WeaponUse.HeldItem(this);
				if (pouch != null && !pouch.IsAir && held != null && !held.IsAir) projType = WeaponUse.ResolveProjectile(this, held, pouch);
				else if (pouch != null && !pouch.IsAir) projType = pouch.shoot;
			}
			if (projType <= 0) projType = Weapon.BeamProjType > 0 ? Weapon.BeamProjType : Weapon.Shoot;
			var sample = WeaponClassifier.ProjectileSample(projType);
			return sample != null && !sample.tileCollide;
		}

		private void RegenMana()
		{
			if (Record.Mana >= Record.MaxMana) { _manaTick = 0; if (Record.Mana > Record.MaxMana) Record.Mana = Record.MaxMana; return; }
			bool recentCombat = (int)Main.GameUpdateCount - _lastEngageTick < LevyConstants.ManaCombatGraceTicks;
			int interval = recentCombat ? LevyConstants.ManaRegenCombatIntervalTicks : LevyConstants.ManaRegenIdleIntervalTicks;
			if (++_manaTick >= interval) { _manaTick = 0; Record.Mana++; }
		}

		// ------------------------------------------------------------------------------------------------------------
		// Broadsword swing
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>Called by WeaponUse when a swing starts: resets the per-swing hit list.</summary>
		public void BeginSwing() => ClearSwingHits();

		/// <summary>True once this swing's one-per-swing weapon special (Volcano blast, Muramasa cut, Blood Butcherer / Tentacle Spike barb, Bat Bat leech) has fired.</summary>
		public bool SwingSpecialSpent;

		private void ClearSwingHits()
		{
			_swingHits.Clear();
			SwingSpecialSpent = false;
		}

		/// <summary>Hits every hostile NPC whose hitbox meets the swing rectangle, once per swing.</summary>
		private void ProcessSwing(NPC primary)
		{
			Item item = WeaponUse.HeldItem(this);
			if (item == null || item.IsAir) return;
			Rectangle box = SwingHitbox(item);
			if (box.Width <= 0 || box.Height <= 0) return;
			int dmg = Math.Max(1, (int)(WeaponDamage(item) * DamageMultiplier));
			int critChance = -1;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || _swingHits.Contains(i)) continue;
				if ((!LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) && !(SoldierTargeting.IsFocusCritter(n) && box.Intersects(n.Hitbox) && SoldierTargeting.FocusAllowsCritter(this, n))) continue;
				if (!box.Intersects(n.Hitbox)) continue;
				if (!n.noTileCollide && !Collision.CanHit(NPC.position, NPC.width, NPC.height, n.position, n.width, n.height)) continue;
				if (critChance < 0) critChance = CritChance(item);
				bool crit = Main.rand.Next(1, 101) <= critChance;
				int dir = n.Center.X < NPC.Center.X ? -1 : 1;
				LootCredit.Credit(n, this); // before the strike: StrikeNPC runs checkDead → NPCLoot synchronously on a killing blow
				// The strike runs the item's own hit hooks (ModItem / GlobalItem / ModPlayer) and item statuses; the vanilla hard-coded melee specials follow it.
				int done = MeleeEffects.StrikeNpc(this, item, n, dmg, dir, crit, item.knockBack, out int sourceDamage);
				_swingHits.Add(i);
				MeleeEffects.ApplyVanillaMeleeHit(this, item, n, sourceDamage, box);
				LastMeleeHitTick = (int)Main.GameUpdateCount;
				LootCredit.OnSoldierHit(n, this, done); // boss rule accounting, kill count, KILL bark
			}
		}

		/// <summary>Weapon crit chance through the dummy's stat pipeline, with the item's own crit plus the player base as a fallback.</summary>
		private int CritChance(Item item)
		{
			if (Dummy != null && LevyConfigPipelineOk)
			{
				try { Dummy.inventory[0] = item; return Math.Clamp(Dummy.GetWeaponCrit(item), 0, 100); } catch { }
			}
			return Math.Clamp(item.crit + LevyConstants.BaseCritChance, 0, 100);
		}

		private static bool LevyConfigPipelineOk => !_pipelineBroken && Common.Config.LevyConfig.Get.UseVanillaStatPipeline;

		/// <summary>
		/// Vanilla's swing rectangle (Player.ItemCheck_GetMeleeHitbox + ItemCheck_ApplyUseStyle for useStyle 1) on this body: the item frame
		/// scaled by the item scale, placed at the hand which swings from behind/above to in front over the animation.
		/// </summary>
		private Rectangle SwingHitbox(Item item)
		{
			int dir = NPC.direction == 0 ? 1 : NPC.direction;
			int frameW, frameH;
			if (!Main.dedServ && item.type > 0 && item.type < TextureAssets.Item.Length && TextureAssets.Item[item.type] != null && TextureAssets.Item[item.type].IsLoaded)
			{
				Rectangle f = Main.itemAnimations[item.type] != null ? Main.itemAnimations[item.type].GetFrame(TextureAssets.Item[item.type].Value) : TextureAssets.Item[item.type].Value.Frame();
				frameW = f.Width; frameH = f.Height;
			}
			else
			{
				frameW = item.width > 0 ? item.width : LevyConstants.MeleeHitboxFallbackPx;
				frameH = item.height > 0 ? item.height : LevyConstants.MeleeHitboxFallbackPx;
			}
			float scale = item.scale * (Dummy != null && Dummy.meleeScaleGlove ? LevyConstants.MeleeGloveScale : 1f);
			int anim = ItemAnimation, animMax = Math.Max(1, ItemAnimationMax);
			Vector2 hand = SwingHandPosition(frameW, frameH, anim, animMax, dir);
			var box = new Rectangle((int)hand.X, (int)hand.Y, (int)(frameW * scale), (int)(frameH * scale));
			if (dir == -1) box.X -= box.Width;
			box.Y -= box.Height;
			if (anim < animMax * 0.333)
			{
				if (dir == -1) box.X -= (int)(box.Width * 1.4 - box.Width);
				box.Width = (int)(box.Width * 1.4);
				box.Y += (int)(box.Height * 0.5);
				box.Height = (int)(box.Height * 1.1);
			}
			else if (!(anim < animMax * 0.666))
			{
				if (dir == 1) box.X -= (int)(box.Width * 1.2);
				box.Width *= 2;
				box.Y -= (int)(box.Height * 1.4 - box.Height);
				box.Height = (int)(box.Height * 1.4);
			}
			return box;
		}

		/// <summary>Where the hand holding a swung item is (vanilla itemLocation for useStyle 1, mount offset 0).</summary>
		private Vector2 SwingHandPosition(int frameW, int frameH, int anim, int animMax, int dir)
		{
			Vector2 pos = NPC.position; float w = NPC.width;
			if (anim < animMax * 0.333)
			{
				float ox = 10f;
				if (frameW > 32) ox = 14f;
				if (frameW >= 52) ox = 24f;
				if (frameW >= 64) ox = 28f;
				if (frameW >= 92) ox = 38f;
				return new Vector2(pos.X + w * 0.5f + (frameW * 0.5f - ox) * dir, pos.Y + 24f);
			}
			if (anim < animMax * 0.666)
			{
				float ox = 10f;
				if (frameW > 32) ox = 18f;
				if (frameW >= 52) ox = 24f;
				if (frameW >= 64) ox = 28f;
				if (frameW >= 92) ox = 38f;
				float oy = 10f;
				if (frameH > 32) oy = 8f;
				if (frameH > 52) oy = 12f;
				if (frameH > 64) oy = 14f;
				return new Vector2(pos.X + w * 0.5f + (frameW * 0.5f - ox) * dir, pos.Y + oy);
			}
			{
				float ox = 6f;
				if (frameW > 32) ox = 14f;
				if (frameW >= 48) ox = 18f;
				if (frameW >= 52) ox = 24f;
				if (frameW >= 64) ox = 28f;
				if (frameW >= 92) ox = 38f;
				float oy = 10f;
				if (frameH > 52) oy = 12f;
				if (frameH > 64) oy = 14f;
				return new Vector2(pos.X + w * 0.5f - (frameW * 0.5f - ox) * dir, pos.Y + oy);
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Thrust bookkeeping (spears / shortswords)
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>WeaponUse records the index of the thrust it spawned for this body.</summary>
		public void NoteThrust(int projectileIndex) => _thrustIndex = projectileIndex;

		/// <summary>True while this body's thrust projectile is still out.</summary>
		public bool HasActiveThrust()
		{
			if (_thrustIndex < 0 || _thrustIndex >= Main.maxProjectiles) return false;
			Projectile p = Main.projectile[_thrustIndex];
			if (p.active && p.ModProjectile is SoldierThrust && (int)p.ai[0] == NPC.whoAmI) return true;
			_thrustIndex = -1;
			return false;
		}
	}
}
