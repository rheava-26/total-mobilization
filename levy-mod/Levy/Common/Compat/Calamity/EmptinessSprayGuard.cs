using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// WRATH OF THE GODS' EMPTINESS SPRAY never removes a levy. What the spray does to an NPC (NoxusBoss EmptinessSprayerGas.DeleteEverything, run every update of each gas puff, 9 updates a tick): every active NPC whose hitbox touches the gas is
	/// set <c>active = false</c> on the spot, with no death, no loot and no record update, unless its type is in <c>EmptinessSprayer.NPCsToNotDelete</c> (the Cultist's tablet, the old-one portals, the target dummy, Solyn, the Avatar's rifts, the
	/// Eternal Garden critters). A levy's body is an ordinary NPC, so a puff that crossed one removed it, and nothing in Levy asked for that. Only the commander removes a levy (by firing it).
	/// <para/>Fix: every NPC type that is a levy's body (SoldierNPC and anything built on it, in any of our mods) joins <c>NPCsToNotDelete</c> once content is set up. A levy that uses the spray as a weapon is untouched otherwise: its gas deletes hostile
	/// NPCs as a player's does.
	/// <para/>Second seam: a puff that touches an NPC in <c>NPCsThatReflectSpray</c> (Nameless Deity) turns around and, when it touches the PLAYER at <c>Projectile.owner</c>, deletes that player (the game kicks to the menu). A levy's shot is owned
	/// by the commander, so a levy that sprayed Nameless would delete the commander. A levy's puff has that flag cleared every tick, so it still turns around but removes no one.
	/// Everything by reflection (Levy has no reference to NoxusBoss); <c>LEVY_SPRAYGUARD=0</c> turns it off (lab A/B).
	/// </summary>
	public class EmptinessSprayGuard : ModSystem
	{
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_SPRAYGUARD") != "0";
		internal static int GasType = -1;
		/// <summary>Types added to the sprayer's protected set.</summary>
		public static int Protected;
		/// <summary>Lab: gas puffs of a levy whose reflect flag was cleared.</summary>
		public static long ReflectCleared;

		public override void PostSetupContent()
		{
			try
			{
				if (!ModLoader.TryGetMod("NoxusBoss", out Mod nox)) return;
				if (ModContent.TryFind("NoxusBoss/EmptinessSprayerGas", out ModProjectile gas)) GasType = gas.Type;
				if (!On) return;
				Type spr = nox.Code.GetType("NoxusBoss.Content.Items.MiscOPTools.EmptinessSprayer");
				bool[] set = spr?.GetProperty("NPCsToNotDelete", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as bool[];
				if (set == null) { Mod.Logger.Warn("Levy: the Emptiness Spray's protected set was not found; levies are not protected from it"); return; }
				foreach (Mod m in ModLoader.Mods)
					foreach (ModNPC n in m.GetContent<ModNPC>())
						if (n is SoldierNPC && n.Type < set.Length && !set[n.Type]) { set[n.Type] = true; Protected++; }
				Mod.Logger.Info($"Levy: Emptiness Spray guard: {Protected} levy NPC type(s) added to the sprayer's protected set");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: Emptiness Spray guard failed: " + e.Message); }
		}

		private static readonly System.Collections.Generic.List<int> _ours = new();
		/// <summary>Lab: takes the levy types out of (false) or puts them back into (true) the sprayer's protected set, to measure what the spray did before.</summary>
		internal static void SetProtectedForLab(bool on)
		{
			try
			{
				if (!ModLoader.TryGetMod("NoxusBoss", out Mod nox)) return;
				bool[] set = nox.Code.GetType("NoxusBoss.Content.Items.MiscOPTools.EmptinessSprayer")?.GetProperty("NPCsToNotDelete", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as bool[];
				if (set == null) return;
				if (_ours.Count == 0) foreach (Mod m in ModLoader.Mods) foreach (ModNPC n in m.GetContent<ModNPC>()) if (n is SoldierNPC && n.Type < set.Length) _ours.Add(n.Type);
				foreach (int t in _ours) set[t] = on;
			}
			catch (Exception) { }
		}

		/// <summary>True when the sprayer would leave this NPC type alone (the lab asks).</summary>
		internal static bool Protects(int npcType)
		{
			try
			{
				if (!ModLoader.TryGetMod("NoxusBoss", out Mod nox)) return false;
				bool[] set = nox.Code.GetType("NoxusBoss.Content.Items.MiscOPTools.EmptinessSprayer")?.GetProperty("NPCsToNotDelete", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as bool[];
				return set != null && npcType >= 0 && npcType < set.Length && set[npcType];
			}
			catch (Exception) { return false; }
		}
	}

	/// <summary>The second seam: a levy's puff never carries the "reflected, delete the player" flag (EmptinessSprayerGas.ai[0]).</summary>
	public class EmptinessSprayGuardProjectile : GlobalProjectile
	{
		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.ModProjectile != null && entity.ModProjectile.Name == "EmptinessSprayerGas" && entity.ModProjectile.Mod.Name == "NoxusBoss";

		public override bool PreAI(Projectile projectile)
		{
			if (EmptinessSprayGuard.On && projectile.ai[0] == 1f && projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.IsSoldierShot) { projectile.ai[0] = 0f; EmptinessSprayGuard.ReflectCleared++; }
			return true;
		}
	}
}
