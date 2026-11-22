using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// THE RIGHT-CLICK OF A HELD MODDED WEAPON. A held modded weapon (<see cref="ModShotKind.Holdout"/>) is used through its holdout, which the levy's own dummy owns in the levy's chain (<see cref="HeldWeapons"/>). Its right-click is
	/// a use like the left one: a player right-clicks, the item's hooks run with the alt button down and make the alt's projectile (Chronomancer's Scythe's spinning scythe, which circles its owner and throws its icicles from him).
	/// Fired as a separate plain use (<see cref="WeaponUse"/>'s alt path) that projectile was owned by the commander's slot and outside the chain: anchored to her, reading her buttons. Here it is made the way the left holdout is
	/// (<see cref="HeldWeapons.ShootHooks"/> with the alt button down, the dummy as the player, owner the chain slot) and held in the chain like it. The row's policy decides when, exactly as for any other alt.
	/// A right button that only picks the variant of the holdout (HOLD_VARIANT, MANA_REFILL) is not a separate use: <see cref="AltPolicies.HoldVariant"/> holds it on the dummy.
	/// </summary>
	internal static class HeldAlt
	{
		/// <summary>Lab A/B switch (env LEVY_HELDALT=0): a held modded weapon never right-clicks (before this, its right-click was fired as a plain use; that path stays shut either way).</summary>
		private static readonly bool Off = Environment.GetEnvironmentVariable("LEVY_HELDALT") == "0";

		/// <summary>Kinds whose right button is held on the dummy for the holdout's life rather than used: never a separate use.</summary>
		internal static bool IsVariantKind(AltPolicyKind k) => k == AltPolicyKind.HoldVariant || k == AltPolicyKind.ManaRefill;

		/// <summary>Whether this spawn of the levy's holdout is the weapon's right-click use.</summary>
		internal static bool Want(SoldierNPC s, Item item, NPC target)
		{
			if (Off || s?.Dummy == null || item?.ModItem == null || !LevyConfig.Get.RealWeaponShoot || !LevyConstants.AltFireEnabled) return false;
			if (ModShotKinds.Of(item) != ModShotKind.Holdout || WeaponUse.MindAltMode == 1) return false;
			AltPolicy pol = AltPolicies.For(item.type);
			if (IsVariantKind(pol.Kind)) return false;
			uint now = Main.GameUpdateCount;
			if (WeaponUse.MindAltMode != 2)
			{
				if (now < s.AltReadyTick) return false;
				if (!AltPolicies.Want(s, item, target, out int recheck)) { if (recheck > 0) s.AltReadyTick = now + (uint)recheck; return false; }
			}
			bool has = false;
			try { has = ItemLoader.AltFunctionUse(item, s.Dummy); } catch (Exception) { }
			if (!has) { s.AltReadyTick = now + (uint)LevyConstants.AltNoAltRecheckTicks; return false; }
			if (item.mana > 0 && s.Record.Mana < WeaponUse.ManaCostOf(s, item)) { s.AltReadyTick = now + (uint)LevyConstants.AltRetryTicks; return false; }
			return true;
		}

		/// <summary>The alt use was tried: made something (count it, pay its mana, space the next one) or not (back off).</summary>
		internal static void Fired(SoldierNPC s, Item item, bool made)
		{
			uint now = Main.GameUpdateCount;
			if (!made) { s.AltReadyTick = now + (uint)LevyConstants.AltFailBackoffTicks; return; }
			if (item.mana > 0) s.Record.Mana = Math.Max(0, s.Record.Mana - WeaponUse.ManaCostOf(s, item));
			AltPolicies.NoteAlt(s, item);
			s.AltReadyTick = now + (uint)(LevyConstants.AltMinGapTicks + Main.rand.Next(0, LevyConstants.AltGapJitterTicks + 1));
		}

		/// <summary>
		/// The item's hooks with the alt button down, as <see cref="WeaponUse"/>'s alt path runs them (CanUseItem with altFunctionUse 2 first: weapons set their alt's use stats there, Chronomancer's Scythe turns its channel off; the item's
		/// stats are put back and its CanUseItem run once more with the left button afterwards). Returns what the hooks made, owned by the chain slot.
		/// </summary>
		internal static List<Projectile> Shoot(SoldierNPC s, Item item, Vector2 vel, int damage, int ownerSlot, out bool threw)
		{
			threw = false;
			Player d = s.Dummy;
			var snap = new WeaponUse.ItemStatSnapshot(item);
			int savedAlt = d.altFunctionUse; Item saved0 = d.inventory[0]; int savedSel = d.selectedItem;
			try
			{
				d.inventory[0] = item; d.selectedItem = 0; d.altFunctionUse = 2;
				bool can = true;
				try { can = ItemLoader.CanUseItem(item, d); } catch (Exception) { can = false; }
				if (!can) return new List<Projectile>();
				return HeldWeapons.ShootHooks(s, item, vel, damage, ownerSlot, true, null, out threw);
			}
			finally
			{
				try { d.altFunctionUse = 0; ItemLoader.CanUseItem(item, d); } catch (Exception) { }
				d.altFunctionUse = savedAlt == 2 ? 0 : savedAlt;
				snap.Restore(item);
				d.inventory[0] = saved0; d.selectedItem = savedSel;
			}
		}
	}
}
