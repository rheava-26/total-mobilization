using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items;
using Levy.Common;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Thorium Healer on a Levy soldier. Thorium's own heal projectile only ever heals Players (and only on the owner's client), so a soldier
	/// cannot use it on a squadmate. Instead a soldier holding an ally-heal weapon runs a basic healer AI from <see cref="SoldierThoriumGlobalNPC"/>:
	/// every <c>useTime</c> ticks it picks the most wounded same-commander soldier in reach and lobs a <see cref="SoldierHeal"/> at it, which
	/// restores the weapon's own <c>healAmount</c> on contact. The weapon's mana cost and use time apply. Never fired at enemies (see
	/// <see cref="CompatHooks.PreFireGate"/>).
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class HealerCompat
	{
		/// <summary>Heal delivered per cast as a multiple of the weapon's healAmount. 1 = exactly what the weapon says.</summary>
		public const float HealPotency = 3f;
		/// <summary>Ticks an ally is left alone after a heal is sent at it, so two healers (or one slow lob) do not overheal the same soldier.</summary>
		private const int ReserveTicks = 40;

		/// <summary>True for a weapon of a Thorium healing damage class (radiant damage or a healing tool): issuable like any combat weapon.</summary>
		public static bool IsHealerClassWeapon(Item item)
		{
			if (item == null || item.IsAir) return false;
			if (item.DamageType is HealerDamage || item.DamageType is HealerTool) return true;
			return item.ModItem is ThoriumItem { isHealer: true } && item.useStyle != Terraria.ID.ItemUseStyleID.None;
		}

		/// <summary>True for a healer weapon that heals allies: this is the kind the healer AI drives (and that must never be fired at an enemy).</summary>
		public static bool IsHealWeapon(Item item)
		{
			if (item == null || item.IsAir) return false;
			return item.ModItem is ThoriumItem { isHealer: true } ti && (ti.healType == HealType.Ally || ti.healType == HealType.AllyAndPlayer);
		}

		/// <summary>Per tick (server / single player), for a soldier holding a heal weapon.</summary>
		public static void Tick(SoldierNPC s, Item item, SoldierThoriumGlobalNPC state)
		{
			if (state.HealCooldown > 0) { state.HealCooldown--; return; }
			if (s.IsRouted || item.ModItem is not ThoriumItem ti) return;
			int manaCost = Math.Max(0, item.mana);
			if (s.Record.Mana < manaCost) return;

			float rangePx = Math.Clamp(s.Weapon?.RangeTiles ?? 20, 8, 40) * LevyConstants.TilePx;
			SoldierNPC ally = MostWoundedAlly(s, rangePx);
			if (ally == null) return;

			int amount = Math.Max(1, (int)Math.Round(Math.Max(1, ti.healAmount) * HealPotency));
			Vector2 muzzle = WeaponUse.Muzzle(s);
			Vector2 to = ally.NPC.Center - muzzle;
			float speed = item.shootSpeed > 0f ? Math.Max(6f, item.shootSpeed) : 8f;
			Vector2 vel = to.LengthSquared() > 1f ? Vector2.Normalize(to) * speed : Vector2.UnitX * speed;
			Projectile p = WeaponUse.Spawn(s, muzzle, vel, ModContent.ProjectileType<SoldierHeal>(), 0, 0f, null, ally.NPC.whoAmI, amount);
			if (p == null) return;

			s.Record.Mana -= manaCost;
			state.HealCooldown = Math.Max(1, item.useTime);
			if (ally.NPC.TryGetGlobalNPC(out SoldierThoriumGlobalNPC allyState)) allyState.HealReservedUntil = Main.GameUpdateCount + ReserveTicks;

			// Show the cast: face the ally, raise the weapon, play its sound (only when the soldier is not already mid-swing at an enemy).
			if (s.ItemAnimation <= 0) { s.ItemAnimation = s.ItemAnimationMax = Math.Max(1, item.useAnimation); s.UsesThisAnimation = 0; }
			s.AimRotation = vel.ToRotation();
			if (Math.Abs(vel.X) > 0.01f) s.NPC.direction = vel.X < 0f ? -1 : 1;
			if (item.UseSound.HasValue) SoundEngine.PlaySound(item.UseSound.Value, s.NPC.Center);
			s.NPC.netUpdate = true;
		}

		/// <summary>The wounded soldier (same commander, not the caster, not already being healed) in reach with the lowest health fraction, a clear shot, and nearest as the tiebreak.</summary>
		private static SoldierNPC MostWoundedAlly(SoldierNPC s, float rangePx)
		{
			string commander = s.Record.CommanderName ?? "";
			SoldierNPC best = null; float bestScore = float.MaxValue;
			Vector2 from = s.NPC.Center;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.whoAmI == s.NPC.whoAmI || n.life >= n.lifeMax || n.life <= 0) continue;
				if (n.ModNPC is not SoldierNPC a || a.Record == null || !a.IsAlive) continue;
				if (!string.Equals(a.Record.CommanderName ?? "", commander, StringComparison.Ordinal)) continue;
				if (n.TryGetGlobalNPC(out SoldierThoriumGlobalNPC g) && g.HealReservedUntil > Main.GameUpdateCount) continue;
				float dist = Vector2.Distance(from, n.Center);
				if (dist > rangePx) continue;
				if (!Collision.CanHitLine(from, 1, 1, n.Center, 1, 1)) continue;
				float score = (float)n.life / Math.Max(1, n.lifeMax) + 0.25f * (dist / rangePx);
				if (score < bestScore) { bestScore = score; best = a; }
			}
			return best;
		}
	}
}
