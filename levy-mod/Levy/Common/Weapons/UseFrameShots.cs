using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// WEAPONS THAT FIRE FROM THEIR USE ANIMATION. A player's item use runs <c>ItemLoader.UseItemFrame</c> on every tick of the animation, in <c>Player.ItemCheck</c>, with the player's own <c>itemTime</c> / <c>itemAnimation</c> /
	/// <c>channel</c>; some modded weapons make their damage there and not in Shoot (Calamity's Thread of Eradication: Shoot only stores the damage; the frame hook fires the laser wall when the use time runs out, or, while the
	/// button is held, stretches the use to 180 ticks and fires the quadruple wall at 60). A levy never runs ItemCheck, so its use made nothing at all (0 damage).
	/// Found from the item's own code (its UseItemFrame calls NewProjectile), never from a name. After the levy's shot (the item's Shoot ran: <see cref="WeaponUse"/>), the use goes on here tick by tick: the dummy stands in as the
	/// player with the use's own clock (kept across ticks, written back from the hook exactly as ItemCheck keeps the player's), the button down while the levy fights, the cursor on its target; what the hook fires is the levy's
	/// shot. The levy's next use waits until this one has run out, as a player's does.
	/// </summary>
	internal static class UseFrameShots
	{
		private sealed class St { public int ItemType, Time, Anim; public float Rot; }
		private static readonly ConditionalWeakTable<SoldierNPC, St> _st = new();
		private static readonly Dictionary<int, bool> _cache = new();
		private static bool[] _before;
		/// <summary>Lab A/B switch (env LEVY_USEFRAME=0): no per-tick use (the use makes what its Shoot makes, as before).</summary>
		private static readonly bool Off = Environment.GetEnvironmentVariable("LEVY_USEFRAME") == "0";
		/// <summary>Lab counter: frame-hook shots made for levies.</summary>
		internal static int Shots;

		/// <summary>The modded item fires from its use animation (its UseItemFrame spawns projectiles).</summary>
		internal static bool Is(Item item)
		{
			if (Off || item?.ModItem == null) return false;
			if (_cache.TryGetValue(item.type, out bool c)) return c;
			c = OwnerLockProbe.ItemHookCalls(item, "UseItemFrame", "NewProjectile") || OwnerLockProbe.ItemHookCalls(item, "UseItemFrame", "NewProjectileDirect");
			_cache[item.type] = c;
			return c;
		}

		/// <summary>The levy's use of <paramref name="item"/> has started (its Shoot ran and made nothing of its own): the animation goes on in <see cref="Tick"/>.</summary>
		internal static void Begin(SoldierNPC s, Item item)
		{
			Player d = s?.Dummy;
			if (d == null) return;
			St st = _st.GetOrCreateValue(s);
			st.ItemType = item.type;
			st.Time = Math.Max(1, CombinedHooks.TotalUseTime(Math.Max(1, item.useTime), d, item));
			st.Anim = Math.Max(1, CombinedHooks.TotalAnimationTime(Math.Max(1, item.useAnimation), d, item));
			st.Rot = s.AimRotation;
			s.AttackCooldown = Math.Max(s.AttackCooldown, st.Time + 1);
		}

		/// <summary>True while a use-animation weapon's use is still running for this levy (his next use waits for it).</summary>
		internal static bool Running(SoldierNPC s) => s != null && _st.TryGetValue(s, out St st) && st.Time > 0;

		/// <summary>One tick of the running use: the item's UseItemFrame with the dummy in the use's state, then the clock runs down. Called every tick from the levy's combat update.</summary>
		internal static void Tick(SoldierNPC s, bool fighting)
		{
			if (s?.Dummy == null || !_st.TryGetValue(s, out St st) || st.Time <= 0) return;
			Item item = WeaponUse.HeldItem(s);
			if (item == null || item.IsAir || item.type != st.ItemType || !LevyConfig.Get.RealWeaponShoot) { st.Time = 0; return; }
			Player d = s.Dummy;
			NPC target = s.TargetIndex >= 0 && s.TargetIndex < Main.maxNPCs && Main.npc[s.TargetIndex].active ? Main.npc[s.TargetIndex] : null;
			bool[] before = _before ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;
			int savedWho = d.whoAmI, savedDir = d.direction, savedSel = d.selectedItem, savedTime = d.itemTime, savedAnim = d.itemAnimation, savedAnimMax = d.itemAnimationMax, savedTimeMax = d.itemTimeMax;
			float savedRot = d.itemRotation; bool savedChannel = d.channel, savedUse = d.controlUseItem; Vector2 savedCenter = d.Center; Item saved0 = d.inventory[0];
			AltPolicies.AimScope aim = default; OwnerPuppet.Lend lend = default;
			try
			{
				lend = OwnerPuppet.LendTransform(s);
				if (target != null) aim = new AltPolicies.AimScope(d, target.Center);
				d.whoAmI = WeaponUse.OwnerFor(s); // the hook spawns with player.whoAmI and tests whoAmI == Main.myPlayer, as the levy's Shoot does (WeaponUse.TryRealShoot)
				d.Center = s.NPC.Center;
				d.direction = s.NPC.direction == 0 ? 1 : s.NPC.direction;
				d.inventory[0] = item; d.selectedItem = 0;
				d.itemTime = st.Time; d.itemTimeMax = Math.Max(1, item.useTime); d.itemAnimation = Math.Max(1, st.Anim); d.itemAnimationMax = Math.Max(d.itemAnimation, item.useAnimation); d.itemRotation = st.Rot;
				d.channel = fighting && target != null; d.controlUseItem = d.channel; // a player holds the button while she fights
				ItemLoader.UseItemFrame(item, d);
				st.Time = d.itemTime; st.Anim = d.itemAnimation; st.Rot = d.itemRotation;
			}
			catch (Exception) { st.Time = 0; }
			finally
			{
				aim.Dispose(); lend.Dispose();
				d.whoAmI = savedWho; d.direction = savedDir; d.Center = savedCenter; d.selectedItem = savedSel; d.inventory[0] = saved0;
				d.itemTime = savedTime; d.itemAnimation = savedAnim; d.itemAnimationMax = savedAnimMax; d.itemTimeMax = savedTimeMax; d.itemRotation = savedRot; d.channel = savedChannel; d.controlUseItem = savedUse;
			}
			DamageClass cls = item.DamageType ?? DamageClass.Default;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && !before[i]) { WeaponUse.TagSoldierProjectile(s, p, cls); Shots++; }
			}
			// the clock: ItemCheck counts the use time and the animation down a tick at a time
			if (st.Time > 0) st.Time--;
			if (st.Anim > 0) st.Anim--;
			if (st.Time > 0) s.AttackCooldown = Math.Max(s.AttackCooldown, st.Time);
		}
	}
}
