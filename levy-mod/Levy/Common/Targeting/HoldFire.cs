using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Owner;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// HOLD FIRE ON AN NPC. Some enemies hand the decision of their end to the player: a moment where a boss is beaten and what happens next depends on whether the player strikes it or waits. The army must never make that choice for the player.
	/// A provider (<see cref="Register"/>) says, from the NPC's own state, that the army holds fire on it right now. While one does:
	///  * no levy, officer, drone or turret picks it as a target (<c>LevyUtil.IsHostileTarget</c> is false for it),
	///  * a shot the army fired passes through it (<see cref="HoldFireGlobalNPC"/>),
	///  * the army does not draw it off the commander (<see cref="EnemyTargeting.IsAllowlisted"/> is false), so the enemy keeps facing the player whose choice it is.
	/// The commander's own weapons are never affected. A provider is a cheap pure read: it runs for every NPC on every targeting pass, so it names its type first and reads state second.
	/// </summary>
	public static class HoldFire
	{
		public delegate bool Provider(NPC npc);
		private static readonly List<Provider> _providers = new();

		/// <summary>True when any provider is registered (the common case, no compat mod loaded, costs one load).</summary>
		public static bool Any => _providers.Count > 0;

		/// <summary>Adds a provider. A provider that throws is treated as "no hold".</summary>
		public static void Register(Provider p) { if (p != null && !_providers.Contains(p)) _providers.Add(p); }

		internal static void Clear() => _providers.Clear();

		/// <summary>True when the army holds fire on this NPC right now.</summary>
		public static bool Holds(NPC npc)
		{
			if (_providers.Count == 0 || npc == null || !npc.active) return false;
			for (int i = 0; i < _providers.Count; i++)
			{
				try { if (_providers[i](npc)) return true; }
				catch (Exception) { }
			}
			return false;
		}
	}

	/// <summary>The projectile half of <see cref="HoldFire"/>: a shot the army fired does not hit an NPC it holds fire on.</summary>
	public class HoldFireGlobalNPC : GlobalNPC
	{
		public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
			=> HoldFire.Any && HoldFire.Holds(npc) && UseGuard.IsArmyRound(projectile) ? false : null;

		public override void Unload() => HoldFire.Clear();
	}
}
