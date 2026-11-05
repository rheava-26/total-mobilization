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
