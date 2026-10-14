using Terraria;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>Per-family firing/swinging. Called from SoldierNPC.Combat every tick while engaging. OWNED BY THE WEAPONS AGENT.</summary>
	public static class WeaponUse
	{
		/// <summary>Try to attack <paramref name="target"/> with the soldier's current weapon. Returns true if a shot/swing started this tick.</summary>
		public static bool TryAttack(SoldierNPC s, NPC target) => false;
		/// <summary>Ticks until the weapon can be used again.</summary>
		public static int Cooldown(SoldierNPC s) => 0;
	}
}
