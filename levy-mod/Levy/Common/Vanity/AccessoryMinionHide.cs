using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Vanity
{
	/// <summary>
	/// The "Hide minions from levy accessories" setting (<see cref="Perf.PerfConfig.HideLevyAccessoryMinions"/>): a minion a levy's ACCESSORY summoned (Calamity's Heart of the Elements and the elemental
	/// bottles, any mod's "summons a minion while worn") is not drawn on this machine. It still fights; nothing in the simulation changes. A staff's minions are not affected.
	/// Such a minion is the one <c>SoldierMinions.AdoptAccessoryMinion</c> adopted: owned by the levy dummy slot, tagged with its soldier, not a staff cast.
	/// </summary>
	public class AccessoryMinionHide : GlobalProjectile
	{
		/// <summary>True for a minion a levy's accessory summoned.</summary>
		public static bool IsLevyAccessoryMinion(Projectile p)
			=> p != null && p.active && p.minion && !p.sentry && p.owner == SoldierNPC.DummySlot && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0 && !tag.Cast;

		/// <summary>True when this machine hides levy accessory minions.</summary>
		public static bool Hiding => !Main.dedServ && Perf.PerfConfig.Get != null && Perf.PerfConfig.Get.HideLevyAccessoryMinions;

		public override bool PreDraw(Projectile projectile, ref Color lightColor) => !(Hiding && IsLevyAccessoryMinion(projectile));
	}
}
