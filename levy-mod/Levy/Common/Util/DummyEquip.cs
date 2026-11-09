using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Util
{
	/// <summary>
	/// Equip update for a stat-reading dummy Player (accessory ratings, armor doctrine, checks). Some accessories act while equipped: Spore Sac rolls 1 in 15 to spawn spores and,
	/// from a dummy at (0,0), reads tiles at negative coordinates and throws. The dummy stands mid-world for the call, a throw does not end the reading, and any projectile it spawned is removed,
	/// so a measurement never leaves anything in the world.
	/// </summary>
	public static class DummyEquip
	{
		private static bool[] _wasActive;

		public static void SafeUpdateEquips(Player p, int slot)
		{
			_wasActive ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) _wasActive[i] = Main.projectile[i].active;
			Vector2 pos = p.position;
			p.Center = new Vector2(Main.maxTilesX * 8f, Main.maxTilesY * 8f);
			try { p.UpdateEquips(slot); } catch (Exception) { }
			finally
			{
				p.position = pos;
				for (int i = 0; i < Main.maxProjectiles; i++) if (!_wasActive[i] && Main.projectile[i].active && Main.projectile[i].owner == slot) Main.projectile[i].active = false;
			}
		}
	}
}
