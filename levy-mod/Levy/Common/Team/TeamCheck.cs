using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;

namespace Levy.Common.Team
{
	/// <summary>
	/// Boot self-check of the team items (no world needed): the registry, the hooks, the packets, the Nebula mapping, and - on a throw-away Player - the whole path a mod buff takes onto a levy's dummy
	/// (the mod's own ModBuff.Update raises its flag, the mod's regeneration hooks turn it into regen units). Never throws; the world-level proofs are the harness scenario "team".
	/// </summary>
	public static class TeamCheck
	{
		public static void Run(Mod mod)
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool pass, string what) { if (pass) ok++; else failures.Add(what); }
			try
			{
				Check(TeamItems.Built, "registry built");
				Check(TeamItems.Auras.Exists(a => a.Id == "paladin"), "the Paladin's Shield aura is registered");
				Check(LevyPackets.IsRegistered(LevyPacket.TeamEffect) && LevyPackets.IsRegistered(LevyPacket.TeamRequest), "team packets registered");
				Check(TeamSpectre.Installed && TeamNebula.Installed && TeamInfo.Installed, "ghostHeal / NebulaLevelup / info hooks installed");
				for (int k = 0; k < 3; k++) Check(TeamNebula.KindOf(TeamNebula.TypeOf(k)) == k, "nebula kind " + k + " round-trips");
				Check(LevyConstants.TeamPulseTicks > 0 && LevyConstants.TeamBuffTicks > LevyConstants.TeamPulseTicks, "a buff outlasts the pulse");

				var d = new Player();
				d.hasPaladinShield = true;
				var pal = TeamItems.Auras.Find(a => a.Id == "paladin");
				Check(pal != null && pal.Flag(d) && pal.Buff == BuffID.PaladinsShield && pal.RangePx == 800f && pal.NeedLife, "Paladin aura reads hasPaladinShield, 800 px, buff 43");
				d.hasPaladinShield = false;
				Check(pal != null && !pal.Flag(d), "Paladin aura reads false without the shield");

				if (ModLoader.HasMod("CalamityMod"))
				{
					var chi = TeamItems.Auras.Find(a => a.Id == "chi"); var aff = TeamItems.Auras.Find(a => a.Id == "affliction");
					Check(chi != null && aff != null, "Calamity's Chi and Affliction auras are registered");
					Check(TeamItems.ProjAuras.Count >= 7, "at least seven projectile auras registered (" + TeamItems.ProjAuras.Count + ")");
					Check(TeamItems.StatBuffs.Count >= 9, "at least nine stat buffs registered (" + TeamItems.StatBuffs.Count + ")");
					foreach (int b in TeamItems.StatBuffs) Check(BuffLoader.GetBuff(b) != null, "stat buff " + b + " resolves");
					if (chi != null)
					{
						// the dummy path: the mod's own ModBuff.Update raises Calamity's chiRegen flag (its regen hook reads it in the gear frame; the world run proves the units)
						var dummy = new Player();
						bool applied = TeamItems.ApplyStatBuff(dummy, chi.Buff);
						Func<Player, bool> chiFlag = TeamItems.ModFlag("CalamityMod", "CalamityMod.CalPlayer.CalamityPlayer", "chiRegen");
						Check(applied && chiFlag != null && chiFlag(dummy), "ChiRegenBuff.Update raised chiRegen on a throw-away dummy");
					}
				}
			}
			catch (Exception e) { failures.Add("self-check threw " + e.GetType().Name + ": " + e.Message); }
			if (failures.Count == 0) mod.Logger.Info($"Levy team items self-check: {ok} checks OK");
			else { mod.Logger.Warn($"Levy team items self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (string f in failures) mod.Logger.Warn("Levy team items self-check FAILED: " + f); }
		}
	}
}
