using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Levy.Common.Boss;

namespace Mechanization.Common
{
	/// <summary>Server-side Mechanization options.</summary>
	public class MechConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;
		public static MechConfig Get => ModContent.GetInstance<MechConfig>();

		/// <summary>Off by default: drones and turrets hold fire on a boss (or one of its parts or segments) whose type the world has not beaten yet.</summary>
		[DefaultValue(false)] public bool FireOnUnbeatenBosses;

		/// <summary>Off by default (a preference: the garrison is part of how some players hold a boss fight). On: while any boss is alive every machine leaves the field. The Barracks garrison's drones are removed at once (each weapon filed back into its Armory) and none launches; every player-built drone and turret packs into its saved record. Their NPC slots are free for the boss, and all of them come back as they were when it is gone.</summary>
		[DefaultValue(false)] public bool StandDownDuringBosses;

		/// <summary>On by default: a placed turret, a player-built drone or a Barracks garrison with no player within <see cref="DormantRangeTiles"/> tiles is packed into its saved record, which frees its NPC slot and its per-tick cost, and comes back as it was when a player is that near.</summary>
		[DefaultValue(true)] public bool DormantWhenFar;

		/// <summary>The distance in tiles from every player past which a machine goes dormant. The game removes an enemy within a tick once it is past about 260 tiles sideways or 140 tiles up and down from every player (measured in the world check), so a machine this far out has nothing to shoot.</summary>
		[Range(60, 1000)] [DefaultValue(280)] public int DormantRangeTiles;
	}

	/// <summary>The one rule drones and turrets share for bosses.</summary>
	public static class BossHoldFire
	{
		/// <summary>
		/// True when a drone or turret must not shoot this NPC: it is a boss, a boss part or a boss segment whose boss type has not been defeated in this world, and the option to fire on them is off.
		/// A boss's minions (Servants of Cthulhu, Creepers, Probes...) are neither, so they stay targets.
		/// </summary>
		public static bool Holds(NPC t)
		{
			if (t == null) return false;
			if (!(Levy.Common.Util.LevyUtil.IsBossNpc(t) || BossRule.IsMainBoss(t))) return false;
			if (BossDownedSystem.IsBossDowned(t)) return false;
			var cfg = MechConfig.Get;
			return cfg == null || !cfg.FireOnUnbeatenBosses;
		}
	}
}
