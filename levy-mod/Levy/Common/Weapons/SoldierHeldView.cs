using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// What the owner of a levy's shot is holding while the shot's AI runs. A levy's plain shot is owned by the commander's slot (<see cref="WeaponUse.OwnerFor"/>; slot 255's empty player on a dedicated server), so every
	/// projectile AI that reads <c>Owner.HeldItem</c> read the COMMANDER's hand: Calamity's plain-shot gun holdouts (Ion Blaster, Phalanx Surge, Apoctosis Array, ...) take their fire rate from <c>Owner.HeldItem.useAnimation</c>,
	/// so with an empty hand the rate was 0 (one shot every tick) and with a Zenith in hand it was 90, whatever weapon the levy carried. For the length of such a shot's own AI call the owner's selected inventory slot presents the
	/// LEVY's weapon, the way a player's own held item would read, and the slot is put back in the same finally. A levy then fires at exactly its weapon's real rate, whatever the commander holds.
	/// Only a soldier's shot that is not part of a held chain is touched (a chain runs against the dummy, which already holds the item: <see cref="HeldWeapons.RunAi"/>). Stands on all three AI hook points behind one guard (see <see cref="ProjAiHooks"/>).
	/// </summary>
	public class SoldierHeldViewSystem : ModSystem
	{
		[ThreadStatic] private static int _depth;
		private static readonly bool Dbg = Environment.GetEnvironmentVariable("LEVY_DEADHOOKS_DEBUG") == "1";

		public override void Load()
		{
			On_Projectile.AI += AiDetour;
			On_Projectile.VanillaAI += VanillaAiDetour;
			ProjAiHooks.AddLoader(LoaderAiDetour, "the held-item view");
		}

		public override void Unload()
		{
			On_Projectile.AI -= AiDetour;
			On_Projectile.VanillaAI -= VanillaAiDetour;
		}

		private static void AiDetour(On_Projectile.orig_AI orig, Projectile self) { _hl_AiDetour_1++; HookProbe.ViewAi++; if (!Wants(self, out SoldierNPC b, out Item it)) orig(self); else Run(pp => orig(pp), self, b, it); } private static int _hl_AiDetour_1;
		private static void VanillaAiDetour(On_Projectile.orig_VanillaAI orig, Projectile self) { _hl_VanillaAiDetour_3++; HookProbe.ViewVanilla++; if (!Wants(self, out SoldierNPC b, out Item it)) orig(self); else Run(pp => orig(pp), self, b, it); } private static int _hl_VanillaAiDetour_3;
		private static void LoaderAiDetour(Action<Projectile> orig, Projectile self) { _hl_LoaderAiDetour_2++; HookProbe.ViewLoader++; if (!Wants(self, out SoldierNPC b, out Item it)) orig(self); else Run(orig, self, b, it); } private static int _hl_LoaderAiDetour_2;

		/// <summary>A soldier's plain shot (tagged, owner a player slot, not a held chain) with a live soldier that carries a weapon.</summary>
		private static bool Wants(Projectile p, out SoldierNPC body, out Item item)
		{
			body = null; item = null;
			if (_depth != 0 || ProjAiHooks.Off || !p.active || p.owner < 0 || p.owner >= Main.maxPlayers + 1 || p.ModProjectile == null || !OwnerLockProbe.ReadsHeldItem(p.type)) return false;
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0) return false;
			if (p.TryGetGlobalProjectile(out HeldWeaponProjectile g) && g.Chain) return false;
			body = MiscProjSwap.BodyOf(p);
			if (body == null) return false;
			item = WeaponUse.HeldItem(body);
			return item != null && !item.IsAir;
		}

		private static void Run(Action<Projectile> orig, Projectile p, SoldierNPC body, Item item)
		{
			long v0 = SwapProf.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			Player owner = Main.player[p.owner];
			int sel = owner?.selectedItem ?? -1;
			if (owner == null || sel < 0 || sel >= owner.inventory.Length) { orig(p); return; }
			Item saved = owner.inventory[sel];
			_depth++;
			try
			{
				owner.inventory[sel] = item;
				HookProbe.ViewActed++;
				if (!ReferenceEquals(owner.HeldItem, item)) HookProbe.ViewMismatch++;
				if (SwapProf.On) { SwapProf.ViewSwap += System.Diagnostics.Stopwatch.GetTimestamp() - v0; SwapProf.ViewN++; }
				orig(p);
				if (Dbg && Main.GameUpdateCount % 60 == 0) HookProbe.ViewLast = $"{p.ModProjectile?.Name} owner={p.owner} sel={sel} saved={saved?.Name} levyItem={item.Name} ua={item.useAnimation} heldNow={owner.HeldItem?.Name} fireRate={p.ModProjectile?.GetType().GetProperty("fireRate")?.GetValue(p.ModProjectile)}";
			}
			finally { owner.inventory[sel] = saved; _depth--; }
		}
	}
}
