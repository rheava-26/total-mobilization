using System.Collections.Generic;
using Terraria.ModLoader;

namespace Levy.Common.Boss
{
	/// <summary>
	/// Modded bosses <see cref="BossRule"/> counts as main bosses next to the vanilla list (<see cref="LevyConstants.MainBosses"/>): the fight's main boss for the fight zone, fear, the
	/// boss-alive facts, the "another main boss is still up" test and the veteran credit at the end of the fight. Found by name, no reference: a mod that is not loaded adds nothing.
	/// They keep everything that changes the boss itself as it was: no spite (no projectile re-aim, no dash at a levy), and the HP scaling every modded boss already had stays the same.
	/// Wrath of the Gods (NoxusBoss): the Avatar's rift, the Avatar of Emptiness and Nameless Deity. Their own scenes and the player's death scenes are never touched.
	/// </summary>
	public class ModdedMainBosses : ModSystem
	{
		/// <summary>(mod, ModNPC class name) of every modded main boss.</summary>
		internal static readonly (string mod, string npc)[] Known =
		{
			("NoxusBoss", "AvatarRift"), ("NoxusBoss", "AvatarOfEmptiness"), ("NoxusBoss", "NamelessDeityBoss"),
		};

		/// <summary>The types registered this load (empty when none of their mods is loaded).</summary>
		internal static readonly HashSet<int> Types = new();

		public override void PostSetupContent()
		{
			foreach (var (mod, npc) in Known)
			{
				if (!ModLoader.TryGetMod(mod, out Mod m) || !m.TryFind(npc, out ModNPC mn)) continue;
				if (Types.Add(mn.Type)) BossRule.MainBossSet.Add(mn.Type);
			}
			if (Types.Count > 0) Mod.Logger.Info($"Levy main bosses: {Types.Count} modded ({string.Join(", ", Types)}) next to the {LevyConstants.MainBosses.Length} vanilla ones");
		}

		public override void Unload()
		{
			foreach (int t in Types) BossRule.MainBossSet.Remove(t);
			Types.Clear();
		}

		/// <summary>A modded main boss: counted for the fight, but the boss itself is left alone (no spite).</summary>
		public static bool Is(int type) => Types.Count > 0 && Types.Contains(type);
	}
}
