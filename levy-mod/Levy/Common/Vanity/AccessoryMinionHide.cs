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
	/// <para/>Its sibling "Hide pets from levy accessories" (<see cref="Perf.PerfConfig.HideLevyAccessoryPets"/>) does the same for the pets and other helpers a levy's gear makes (Daawnlight Spirit Origin's archer, Scion's Curio,
	/// a light pet): the ones <c>SoldierMinions.AdoptAccessoryHelper</c> adopted. A separate switch because a pet is cosmetic and an accessory minion fights: hiding one is not a reason to hide the other. The commander's own pets are never touched.
	/// </summary>
	public class AccessoryMinionHide : GlobalProjectile
	{
		/// <summary>True for a minion a levy's accessory summoned.</summary>
		public static bool IsLevyAccessoryMinion(Projectile p)
			=> p != null && p.active && p.minion && !p.sentry && p.owner == SoldierNPC.DummySlot && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0 && !tag.Cast;

		/// <summary>True for a pet or other helper a levy's gear made (not a minion).</summary>
		public static bool IsLevyAccessoryPet(Projectile p)
			=> p != null && p.active && !p.minion && p.owner == SoldierNPC.DummySlot && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.GearHelper && tag.SoldierIndex >= 0;

		/// <summary>Self-check only: makes both settings read as on (a dedicated server draws nothing, so the draw rule is tested through this).</summary>
		internal static bool TestForce;

		/// <summary>True when this machine hides levy accessory minions.</summary>
		public static bool Hiding => TestForce || (!Main.dedServ && Perf.PerfConfig.Get != null && Perf.PerfConfig.Get.HideLevyAccessoryMinions);

		/// <summary>True when this machine hides levy accessory pets.</summary>
		public static bool HidingPets => TestForce || (!Main.dedServ && Perf.PerfConfig.Get != null && Perf.PerfConfig.Get.HideLevyAccessoryPets);

		public override bool PreDraw(Projectile projectile, ref Color lightColor) => !(Hiding && IsLevyAccessoryMinion(projectile)) && !(HidingPets && IsLevyAccessoryPet(projectile));
	}
}
