using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Recording;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- STARS ABOVE / STARLIGHT RIVER (only when either mod is loaded; LEVY_SR=0 skips, LEVY_SR_ALONE=1 runs it alone)
		// On the army sweep's own arena and machinery (one levy against one stand-in player, the pinned enemy):
		//  * the swords whose blow is a slash circling the wielder (WeaponClassifier.AnchoredSwingReach) land in levy hands, at least half of a player's damage;
		//  * a modded sentry that places itself at its owner's cursor on spawn goes down beside its levy (SoldierNPC.SpawnModdedSentry);
		//  * every boss of the two mods, and a vanilla control boss, against six armed levies posted on Hold: spawned, seen as a boss by the recorder, struck by the army (observed), no Levy code throws.
		// Only code identifiers of the two mods are logged, never their display text.
		private static readonly string[] SrModNames = { "StarsAbove", "StarlightRiver" };
		private static bool SrAny => SrModNames.Any(ModLoader.HasMod);

		private static void StarsRiverCheck()
		{
			if (!SrAny || Environment.GetEnvironmentVariable("LEVY_SR") == "0") return;
			SetupOriginArena(); _spNet = false;
			On_AchievementsHelper.HandleSpecialEvent += NoAchievements;
			try
			{
				KeepCommander(); Step(2);
				string part = Environment.GetEnvironmentVariable("LEVY_SR_PART") ?? "all";
				if (part == "all" || part.Contains("swords")) { try { SrSwords(); } finally { ClearSoldiers(); } }
				if (part == "all" || part.Contains("sentry")) { try { SrSentry(); } finally { ClearSoldiers(); } }
				if (part == "all" || part.Contains("bosses")) { try { SrBossFights(); } finally { ClearSoldiers(); } }
			}
			finally { _sweepTarget = null; On_AchievementsHelper.HandleSpecialEvent -= NoAchievements; }
		}

		private static void SrSwords()
		{
			if (!ModLoader.TryGetMod("StarsAbove", out Mod sa)) return;
			foreach (string n in new[] { "BloodBlade", "BurningDesire", "Umbra", "SakuraVengeance" })
			{
				if (!sa.TryFind(n, out ModItem mi)) { Check("StarsAbove/" + n + " exists", false); continue; }
				Item probe = new Item(); probe.SetDefaults(mi.Type);
				WeaponProfile prof = WeaponClassifier.GetProfile(probe);
				ArmyRes levy = ArmyOne(mi.Type, 0, 300, false, false);
				ArmyRes player = ArmyOne(mi.Type, 0, 300, true, false);
				double ratio = levy.Dmg / (double)Math.Max(1, player.Dmg);
				Note($"sr sword StarsAbove/{n}: family {prof.Family}, reach {prof.RangeTiles} tiles, anchored {WeaponClassifier.IsAnchoredSwing(probe)}; levy damage {levy.Dmg} ({levy.Hits} hits), player {player.Dmg} ({player.Hits} hits), ratio {ratio:0.00}; from the commander {levy.FromCommander}{(levy.Err != null ? "; levy threw " + levy.Err : "")}");
				Check($"StarsAbove/{n}: a levy fights it at its slash's reach and lands at least half of a player's damage (ratio {ratio:0.00}, levy {levy.Dmg}, player {player.Dmg})", WeaponClassifier.IsAnchoredSwing(probe) && prof.IsMelee && levy.Err == null && !levy.FromCommander && levy.Dmg > 0 && ratio >= 0.5);
			}
		}

		private static void SrSentry()
		{
			if (!ModLoader.TryGetMod("StarlightRiver", out Mod slr) || !slr.TryFind("HeartStatueSentryItem", out ModItem hs)) return;
			int before = SoldierNPC.ModdedSentrySpawns;
			ArmyRes r = ArmyOne(hs.Type, 0, 240, false, false);
			int spawns = SoldierNPC.ModdedSentrySpawns - before;
			Note($"sr sentry StarlightRiver/HeartStatueSentryItem: towers {r.MaxSentries}, first seen {r.AvgActor:0.0} tiles from the levy / {r.AvgCmd:0.0} from the commander, modded-sentry spawns {spawns}{(r.Err != null ? ", threw " + r.Err : "")}");
			Check($"a modded sentry that places itself at its owner's cursor goes down beside its levy ({r.AvgActor:0.0} tiles from him, {r.AvgCmd:0.0} from the commander)", r.Err == null && r.MaxSentries >= 1 && spawns >= 1 && r.AvgActor >= 0 && r.AvgActor <= 12);
		}

		// ---- the bosses
		private static Dictionary<string, int> _srExc; private static bool _srExcOn;
		private static void SrExc(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
		{
			if (!_srExcOn || _srExc == null || _srExc.Count > 100) return;
			_srExcOn = false;
			try
			{
				string st = Environment.StackTrace;
				string top = st.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => (l.Contains("StarsAbove.") || l.Contains("StarlightRiver.") || l.Contains(" Levy.")) && !l.Contains("IdentityWorldCheck") && !l.Contains("SrExc")) ?? null;
				if (top == null) return;
				string k = e.Exception.GetType().Name + " " + (top.Length > 150 ? top.Substring(0, 150) : top);
				_srExc[k] = _srExc.GetValueOrDefault(k) + 1;
			}
			catch (Exception) { }
			finally { _srExcOn = true; }
		}

		private static void SrBossFights()
		{
			var bosses = new List<int>();
			for (int t = NPCID.Count; t < NPCLoader.NPCCount; t++)
				if (NPCLoader.GetNPC(t) is ModNPC mn && SrModNames.Contains(mn.Mod.Name) && ContentSamples.NpcsByNetId.TryGetValue(t, out NPC s) && s != null && s.boss) bosses.Add(t);
			string only = Environment.GetEnvironmentVariable("LEVY_SR_BOSS");
			if (!string.IsNullOrEmpty(only)) bosses = bosses.Where(t => only.Split(',').Contains((NPCLoader.GetNPC(t) as ModNPC)?.Name)).ToList();
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SR_BOSS_TICKS"), out int tk) ? tk : 420;
			bool? bts = global::Levy.Common.Config.LevyConfig.Get?.BossesTargetSoldiers;
			Note($"sr bosses: {bosses.Count} [{string.Join(",", bosses.Select(t => (NPCLoader.GetNPC(t) as ModNPC)?.Mod.Name + "/" + (NPCLoader.GetNPC(t) as ModNPC)?.Name))}]; bosses target soldiers = {bts}; {ticks} ticks each");
			int bullets = ItemID.ChlorophyteBullet;
			long controlHits = -1; int spawned = 0, struck = 0, recognised = 0, levyThrew = 0;
			foreach (int type in bosses.Prepend(NPCID.EyeofCthulhu))
			{
				bool control = type == NPCID.EyeofCthulhu;
				string name = control ? "control (vanilla Eye of Cthulhu)" : (NPCLoader.GetNPC(type) as ModNPC)?.Mod.Name + "/" + (NPCLoader.GetNPC(type) as ModNPC)?.Name;
				ClearSoldiers(); _cmdAt = OriginCmdAt(); _sweepTarget = null; ReleaseCommanderButtons();
				var levies = new List<NPC>();
				for (int k = 0; k < 6; k++)
				{
					Item w = new Item(); w.SetDefaults(k % 2 == 0 ? ItemID.Megashark : ItemID.TerraBlade);
					SoldierRecord rec = MakeOriginRecord(w, w.useAmmo > 0 ? bullets : 0); rec.Order = SoldierOrder.Hold; RosterSystem.Instance.Regulars.Add(rec);
					Vector2 at = new Vector2((Cx + (k - 3) * 3) * 16f + 8f, FloorY * 16f);
					int body = RosterSystem.Instance.SpawnBody(rec, at);
					if (body >= 0 && rec.HasBody) { Main.npc[body].Bottom = at; rec.HoldPos = Main.npc[body].Center; levies.Add(Main.npc[body]); }
				}
				KeepCommander(); Step(10);
				var start = levies.Select(l => l.Center).ToArray();
				_srExc = new Dictionary<string, int>(); _srExcOn = true; AppDomain.CurrentDomain.FirstChanceException += SrExc;
				NPC boss = null; int thrown = 0, alive = 0, locked = 0, immune = 0, jumps = 0, maxAway = 0; bool gone = false; int goneAt = -1;
				try
				{
					int bi = NPC.NewNPC(new EntitySource_WorldEvent("IdentityWorldCheck"), (Cx + 25) * 16, (FloorY - 20) * 16, type);
					if (bi >= 0 && bi < Main.maxNPCs && Main.npc[bi].type == type) boss = Main.npc[bi];
				}
				catch (Exception e) { thrown++; Note($"sr boss {name}: spawn threw {e.GetType().Name}: {e.Message}"); }
				if (boss == null) { _srExcOn = false; AppDomain.CurrentDomain.FirstChanceException -= SrExc; Note($"sr boss {name}: did not spawn"); continue; }
				if (!control) spawned++;
				bool rec0 = FightRecorder.IsFightBoss(boss);
				int idx = boss.whoAmI; ArmySweepTally.Target = idx; ArmySweepTally.On = true; ArmySweepTally.Hits = 0; ArmySweepTally.Damage = 0;
				Vector2 b0 = boss.Center, bMin = b0, bMax = b0; var last = start.ToArray();
				try
				{
					for (int k = 0; k < ticks; k++)
					{
						try { OriginStep(); } catch (Exception) { thrown++; }
						ClearNaturalHostiles();
						NPC b = Main.npc[idx];
						if (!b.active || b.type != type) { if (!gone) goneAt = k; gone = true; }
						else { alive++; b.timeLeft = Math.Max(b.timeLeft, 300); if (LevyNpcData.Of(b).SoldierTarget >= 0) locked++; if (b.dontTakeDamage || b.immortal) immune++; bMin = Vector2.Min(bMin, b.Center); bMax = Vector2.Max(bMax, b.Center); }
						for (int j = 0; j < levies.Count; j++)
						{
							NPC n = levies[j]; if (!n.active || n.ModNPC is not SoldierNPC) continue;
							if (Vector2.Distance(n.Center, last[j]) > 25 * 16f) jumps++;
							maxAway = Math.Max(maxAway, (int)(Vector2.Distance(n.Center, start[j]) / 16f)); last[j] = n.Center;
						}
					}
				}
				finally { ArmySweepTally.On = false; _srExcOn = false; AppDomain.CurrentDomain.FirstChanceException -= SrExc; }
				int lost = levies.Count(l => !l.active || l.ModNPC is not SoldierNPC);
				bool lv = _srExc.Keys.Any(k => k.Contains(" Levy."));
				if (control) controlHits = ArmySweepTally.Hits;
				else { if (ArmySweepTally.Hits > 0) struck++; if (rec0) recognised++; if (lv) levyThrew++; }
				Note($"sr boss {name}: alive {alive}/{ticks}{(gone ? $" (gone at {goneAt})" : "")}, army hits {ArmySweepTally.Hits} for {ArmySweepTally.Damage}, locked on a levy {locked}, untouchable {immune}, recorder sees a boss {rec0}, boss moved x {(bMin.X - b0.X) / 16f:0}..{(bMax.X - b0.X) / 16f:0} y {(bMin.Y - b0.Y) / 16f:0}..{(bMax.Y - b0.Y) / 16f:0} tiles, levy jumps >25 tiles {jumps}, levies farthest {maxAway} tiles, levies down {lost}/{levies.Count}, steps thrown {thrown}, exceptions [{string.Join("; ", _srExc.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Value + "x " + kv.Key))}]");
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is not SoldierNPC && !n.townNPC) n.active = false; }
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			}
			_srExc = null;
			Note($"sr bosses: {spawned}/{bosses.Count} spawned, struck by the army {struck}, recorder sees {recognised}, Levy code threw on {levyThrew}; control hits {controlHits}");
			Check($"sr bosses: the harness control (a vanilla boss) is struck by the army ({controlHits} hits)", controlHits > 0);
			Check($"sr bosses: every StarsAbove / StarlightRiver boss that spawns is seen as a boss by the fight recorder ({recognised}/{spawned})", recognised == spawned);
			Check($"sr bosses: no Levy code throws while the army fights a StarsAbove / StarlightRiver boss ({levyThrew})", levyThrew == 0);
		}
	}
}
