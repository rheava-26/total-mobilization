using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Test boots only (LEVY_SELFCHECK_WORLD=1 or LEVY_TILESWEEP=1): remembers the projectile types the ARMY made while the world checks ran (a levy's weapon use through its dummy player, an officer's kit, a drone or a
	/// turret), so the sweep's last check can say that none of them alters the world. Idle in play: with the switch off OnSpawn returns at once.
	/// </summary>
	public class AlterWatch : GlobalProjectile
	{
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" || Environment.GetEnvironmentVariable("LEVY_TILESWEEP") == "1";
		/// <summary>Projectile types the army made, with how many.</summary>
		public static readonly Dictionary<int, int> Seen = new();
		/// <summary>True while the sandbox itself fires things (those shots are the test, not the army).</summary>
		public static bool Sandbox;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!On || Sandbox || !ArmySource(source)) return;
			Seen[projectile.type] = Seen.TryGetValue(projectile.type, out int n) ? n + 1 : 1;
		}

		/// <summary>True when the thing that made this shot is part of the army: a levy or officer body, a drone, a turret, or a levy's dummy player.</summary>
		public static bool ArmySource(IEntitySource source)
		{
			if (source is not EntitySource_Parent parent) return false;
			switch (parent.Entity)
			{
				case NPC npc: { string mod = npc.ModNPC?.Mod.Name; return mod == "Levy" || mod == "Mechanization"; }
				case Player p: return global::Levy.Common.Owner.SoldierOwner.IsDummy(p);
			}
			return false;
		}
	}
}
