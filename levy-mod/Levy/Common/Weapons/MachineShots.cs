using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Targeting;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// How a machine of the companion mod (a combat drone, an automatic turret) fires its mounted weapon: through the item's own shoot path, the way a player's use of it goes.
	/// The pre-shot hooks run in the order Player.ItemCheck_Shoot runs them (the ammo's PickAmmo, ModifyShootStats, Shoot) with the machine's free-standing dummy as the player holding a clone of the item,
	/// so a weapon that swaps its round for a signature shot (Calamity's Nitro Express), fires several of them, or spawns its own projectiles does exactly that from the machine. When the Shoot hook leaves the
	/// default shot to us, the item's (multishot) volley is spawned from the hook-modified stats.
	/// <para/>Whatever the weapon's hooks do to "the player" lands on the dummy: a kick on its velocity comes back as <c>kick</c> for the machine to take, and the real player's use state is guarded by
	/// <see cref="Owner.UseGuard"/> (the caller wraps the call in the machine's guard scope; the machine's AI pass is guarded as well). An owner-anchored holdout (the weapon's Shoot hook spawns a held projectile
	/// that lives on its owner) is never kept: it would grab the real player; the machine fires the weapon's plain shot instead. Never throws.
	/// </summary>
	public static class MachineShots
	{
		/// <summary>Lab counters (the world check reads them): rounds that went through the weapon's hooks, those that found a holdout and fell back, hooks that threw, hooks that made nothing, plain rounds.</summary>
		public static int Hooked, Holdouts, Threw, Empty, Light;
		/// <summary>Lab: the projectile types the last hooked shot spawned (before the caller tags them).</summary>
		public static readonly System.Collections.Generic.List<int> LastTypes = new();

		private static bool[] _before;

		/// <summary>True when this shot runs the weapon's own hooks (a real gun / caster whose item is on the shot, and the world allows it).</summary>
		public static bool UsesHooks(DroneShot shot) => shot != null && shot.HookItem != null && !shot.StandIn && LevyConfig.Get.RealWeaponShoot;

		/// <summary>
		/// Fires one shot of <paramref name="shot"/> from <paramref name="body"/> at <paramref name="pos"/> heading <paramref name="vel"/> for <paramref name="damage"/>, owned by player slot <paramref name="owner"/>.
		/// <paramref name="aimAt"/> is where a cursor-reading weapon aims (the target). Every projectile spawned goes through <paramref name="tag"/>. Returns how many; <paramref name="kick"/> is the velocity
		/// the weapon's hooks gave "the player" (a recoil), for the machine to apply to itself.
		/// </summary>
		public static int Fire(ITargetBody body, DroneShot shot, Vector2 pos, Vector2 vel, int damage, int owner, Vector2 aimAt, Action<Projectile> tag, out Vector2 kick)
		{
			kick = Vector2.Zero;
			try
			{
				if (UsesHooks(shot) && body?.Dummy != null)
				{
					int made = RunHooks(body, shot, pos, vel, damage, owner, aimAt, tag, out kick);
					if (made > 0) return made;
				}
			}
			catch (Exception) { Threw++; kick = Vector2.Zero; }
			Light++;
			return LightVolley(shot, pos, vel, damage, shot.Knockback, shot.Projectile, owner, tag, body?.NPC);
		}

		/// <summary>The plain volley: the shot's projectile, one to several spread over the weapon's fan.</summary>
		private static int LightVolley(DroneShot shot, Vector2 pos, Vector2 vel, int damage, float knockback, int type, int owner, Action<Projectile> tag, NPC source)
		{
			int made = 0;
			for (int k = 0; k < shot.Count; k++)
			{
				float deg = shot.Count > 1 ? -shot.SpreadDeg * 0.5f + shot.SpreadDeg * k / (shot.Count - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				IEntitySource src = source != null ? source.GetSource_FromAI() : new EntitySource_Misc("MachineShot");
				int idx = Projectile.NewProjectile(src, pos.X, pos.Y, v.X, v.Y, type, damage, knockback, owner);
				if (idx < 0 || idx >= Main.maxProjectiles) continue;
				tag(Main.projectile[idx]);
				made++;
			}
			return made;
		}

		private static int RunHooks(ITargetBody body, DroneShot shot, Vector2 pos0, Vector2 vel0, int damage0, int owner, Vector2 aimAt, Action<Projectile> tag, out Vector2 kick)
		{
			kick = Vector2.Zero;
			Player d = body.Dummy;
			Item item = shot.HookItem;
			bool[] before = _before ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;

			int savedWho = d.whoAmI, savedDir = d.direction, savedSel = d.selectedItem, savedMana = d.statMana, savedManaMax = d.statManaMax2, savedHeld = d.heldProj, savedAnim = d.itemAnimation, savedAnimMax = d.itemAnimationMax, savedTime = d.itemTime, savedTimeMax = d.itemTimeMax;
			Vector2 savedCenter = d.Center, savedVel = d.velocity; Item saved0 = d.inventory[0]; bool savedUse = d.controlUseItem, savedChannel = d.channel;
			Vector2 pos = pos0, v = vel0; int type = shot.HookProjectile > ProjectileID.None ? shot.HookProjectile : shot.Projectile, dmg = damage0; float kb = shot.Knockback;
			bool useDefault = false, threw = false;
			object thoriumPrev = null;
			AltPolicies.AimScope aim = default;
			NPC npc = body.NPC;
			try
			{
				aim = new AltPolicies.AimScope(d, aimAt);
				d.whoAmI = owner; // spawns made with the player's index are owned by (and synced as) the machine's owner; Main.myPlayer on the authority
				d.Center = npc.Center; d.direction = npc.direction == 0 ? 1 : npc.direction; d.velocity = Vector2.Zero; d.heldProj = -1;
				d.inventory[0] = item; d.selectedItem = 0;
				d.statManaMax2 = Math.Max(d.statManaMax2, 400); d.statMana = d.statManaMax2; // a machine keeps no mana economy
				d.controlUseItem = true; d.channel = true;
				// the phase of the use animation this shot would have had (a weapon that fires a different beam at each step of its swing reads it from itemAnimation)
				int use = Math.Max(1, item.useTime), anim = Math.Max(use, item.useAnimation), steps = Math.Max(1, anim / use);
				if (item.useLimitPerAnimation.HasValue && item.useLimitPerAnimation.Value > 0) steps = Math.Min(steps, item.useLimitPerAnimation.Value);
				d.itemAnimationMax = anim; d.itemTimeMax = use; d.itemAnimation = anim - use * (shot.UseIndex % steps); d.itemTime = use;
				shot.UseIndex++;
				thoriumPrev = OwnerPuppet.SwapThorium(d);
				Item ammo = shot.AmmoItem;
				if (ammo != null && !ammo.IsAir)
				{
					var dm = StatModifier.Default; int t = type; float sp = v.Length(), k2 = kb; // the ammo's own PickAmmo hook may swap the round or its speed
					try { ItemLoader.PickAmmo(item, ammo, d, ref t, ref sp, ref dm, ref k2); } catch (Exception) { }
					if (t > 0) type = t;
					if (sp > 0f && v != Vector2.Zero && Math.Abs(sp - v.Length()) > 0.01f) v = Vector2.Normalize(v) * sp;
					kb = k2;
				}
				CombinedHooks.ModifyShootStats(d, item, ref pos, ref v, ref type, ref dmg, ref kb);
				var source = new EntitySource_ItemUse_WithAmmo(d, item, shot.AmmoItem?.type ?? 0);
				useDefault = CombinedHooks.Shoot(d, item, source, pos, v, type, dmg, kb);
			}
			catch (Exception) { threw = true; }
			finally
			{
				aim.Dispose();
				OwnerPuppet.RestoreThorium(thoriumPrev);
				kick = d.velocity;
				d.whoAmI = savedWho; d.direction = savedDir; d.Center = savedCenter; d.velocity = savedVel; d.selectedItem = savedSel; d.inventory[0] = saved0;
				d.controlUseItem = savedUse; d.channel = savedChannel; d.statMana = savedMana; d.statManaMax2 = savedManaMax;
				d.itemAnimation = savedAnim; d.itemAnimationMax = savedAnimMax; d.itemTime = savedTime; d.itemTimeMax = savedTimeMax;
			}
			bool holdout = d.heldProj >= 0 && d.heldProj < Main.maxProjectiles; d.heldProj = savedHeld;

			int made = 0;
			LastTypes.Clear();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || before[i]) continue;
				if (p.aiStyle == ProjAIStyleID.HeldProjectile) holdout = true;
				made++; LastTypes.Add(p.type);
			}
			if (threw) Threw++;
			if (holdout)
			{
				// the weapon's own shot is a holdout that lives on its owner (the real player): never kept
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && !before[i]) p.active = false; }
				Holdouts++; LastTypes.Clear();
				kick = Vector2.Zero;
				return 0;
			}
			if (made > 0)
			{
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && !before[i]) tag(p); }
				Hooked++;
				if (!threw && useDefault) made += DefaultVolley(shot, body, pos, v, type, dmg, kb, owner, tag);
				return made;
			}
			if (threw) return 0; // a hook that threw before it spawned anything: the plain shot instead
			if (!useDefault) { Empty++; return 0; } // the hook chose to spawn nothing this time: the plain shot instead
			Hooked++;
			int n = DefaultVolley(shot, body, pos, v, type, dmg, kb, owner, tag);
			if (n > 0) LastTypes.Add(type);
			return n;
		}

		/// <summary>The default shot of a Shoot hook that returned true: the (multishot) volley from the hook-modified stats.</summary>
		private static int DefaultVolley(DroneShot shot, ITargetBody body, Vector2 pos, Vector2 v, int type, int dmg, float kb, int owner, Action<Projectile> tag)
			=> type > ProjectileID.None ? LightVolley(shot, pos, v, dmg, kb, type, owner, tag, body.NPC) : 0;
	}
}
