using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// WRATH OF THE GODS' EMPTINESS SPRAY never ERASES a levy: it KILLS it (the normal death path). What the spray does to an NPC (NoxusBoss EmptinessSprayerGas.DeleteEverything, run every update of each gas puff, 9 updates a tick): every active NPC whose hitbox touches the gas is
	/// set <c>active = false</c> on the spot, with no death, no loot and no record update, unless its type is in <c>EmptinessSprayer.NPCsToNotDelete</c> (the Cultist's tablet, the old-one portals, the target dummy, Solyn, the Avatar's rifts, the
	/// Eternal Garden critters). A levy's body is an ordinary NPC, so a puff that crossed one would remove it with no death, no death line, no respawn and a stale roster record.
	/// <para/>Fix: every NPC type that is a levy's body (SoldierNPC and anything built on it) joins <c>NPCsToNotDelete</c>, so the gas never erases one. Instead each living levy body a puff touches is killed through its normal death path (<see cref="SoldierNPC.KillBySpray"/>): death line, respawn queued, roster intact. A levy's own puff spares the friendly side (levies, officers, town NPCs, critters, machines) and kills hostile NPCs, hostile Warband soldiers included.
	/// <para/>Second seam: a puff that touches an NPC in <c>NPCsThatReflectSpray</c> (Nameless Deity) turns around and, when it touches the PLAYER at <c>Projectile.owner</c>, deletes that player (the game kicks to the menu). A levy's shot is owned
	/// by the commander, so the flag is cleared on a levy's puff (the commander is never deleted) and the turned-around puff kills the levy that sprayed it when it reaches that levy, as a player is erased. That levy dies normally and respawns.
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
		/// <summary>Lab: the whole guard acts as if the levy had no protection and the spray had no kill step (the old behaviour: the gas erases a levy).</summary>
		internal static bool LabKillOff;

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
			LabKillOff = !on;
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

		private static readonly System.Collections.Generic.List<int> _spared = new();
		/// <summary>Lab telemetry: friendly NPCs a levy's puff spared this session.</summary>
		public static long FriendlySpared;

		/// <summary>The sprayer's protected set, found once.</summary>
		private static bool[] _set;
		private static bool[] Set()
		{
			if (_set != null) return _set;
			try { if (ModLoader.TryGetMod("NoxusBoss", out Mod nox)) _set = nox.Code.GetType("NoxusBoss.Content.Items.MiscOPTools.EmptinessSprayer")?.GetProperty("NPCsToNotDelete", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as bool[]; }
			catch (Exception) { }
			return _set;
		}

		/// <summary>
		/// A LEVY'S PUFF SPARES EVERYTHING FRIENDLY. The gas erases whatever it touches that is not in the protected set, by type; a levy fires it in battle, possibly beside a town. For the length of one update of a levy's puff, the type of every
		/// friendly NPC in its hitbox (<c>friendly</c> or <c>townNPC</c>: town NPCs, critters, other mods' machines, drones, warband units, officers, summoned units) is added to the protected set, and taken out again after. Hostiles and bosses
		/// are erased as for a player.
		/// </summary>
		private static void SpareFriendlies(Projectile projectile)
		{
			bool[] set = Set(); if (set == null) return;
			Rectangle hb = projectile.Hitbox;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.friendly || n.townNPC) || n.type < 0 || n.type >= set.Length || set[n.type] || !n.Hitbox.Intersects(hb)) continue;
				set[n.type] = true; _spared.Add(n.type); FriendlySpared++;
			}
		}

		public override void PostAI(Projectile projectile)
		{
			if (_spared.Count == 0) return;
			bool[] set = Set(); if (set != null) foreach (int t in _spared) if (t >= 0 && t < set.Length) set[t] = false;
			_spared.Clear();
		}

		public override bool InstancePerEntity => true;
		/// <summary>This levy's puff was reflected (by the Nameless Deity): it turned around and now carries the delete-the-shooter flag.</summary>
		private bool _reflected;
		/// <summary>Lab: levies a puff of anyone's killed; levies killed by their own reflected puff.</summary>
		public static long LevyKills, ReflectKills;

		/// <summary>
		/// THE SPRAY KILLS LEVIES. Their body types sit in the sprayer's protected set, so the gas itself never erases one (no vanished body, no lost record). Instead, on every update of a puff, each living levy body its hitbox touches
		/// dies through the normal death path (SoldierNPC.KillBySpray: death line, respawn queued). A levy's own puff leaves the friendly side alone (levies, officers) and kills hostile ones (Warband soldiers).
		/// </summary>
		private static void KillTouchedLevies(Projectile projectile, bool fromLevy)
		{
			Rectangle hb = projectile.Hitbox;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || !(n.ModNPC is SoldierNPC s) || !n.Hitbox.Intersects(hb)) continue;
				if (fromLevy && !s.Hostile) continue;
				if (s.KillBySpray()) LevyKills++;
			}
		}

		public override bool PreAI(Projectile projectile)
		{
			if (!EmptinessSprayGuard.On) return true;
			bool fromLevy = projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.IsSoldierShot;
			if (fromLevy) SpareFriendlies(projectile);
			if (!EmptinessSprayGuard.LabKillOff && Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient) KillTouchedLevies(projectile, fromLevy);
			if (fromLevy)
			{
				// the reflection: the flag that would delete the commander is cleared; the levy that sprayed is the one the puff finds
				if (projectile.ai[0] == 1f) { projectile.ai[0] = 0f; _reflected = true; EmptinessSprayGuard.ReflectCleared++; }
				if (_reflected && !EmptinessSprayGuard.LabKillOff && Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient && tag.SoldierIndex >= 0 && tag.SoldierIndex < Main.maxNPCs)
				{
					NPC sh = Main.npc[tag.SoldierIndex];
					if (sh.active && sh.life > 0 && sh.ModNPC is SoldierNPC ss && sh.Hitbox.Intersects(projectile.Hitbox) && ss.KillBySpray()) ReflectKills++;
				}
			}
			return true;
		}
	}
}
