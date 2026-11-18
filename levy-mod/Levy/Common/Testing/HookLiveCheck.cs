using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HOOK LIVENESS (the last check of the world chain). Every On_* / MonoModHooks detour that Levy, Mechanization and Warbands install counts its own calls in a static <c>_hl_Name_N</c> int (one plain increment at the top of the detour).
	/// A detour that was installed but never ran during the whole chain is a hook that is silently dead: the megagun bug was exactly that (a wrapper on On_Projectile.AI, a stub the JIT inlines into Projectile.Update, ran against nothing and nothing noticed).
	/// Here every counter is read once. A detour with calls is live. A detour with none must be listed in <see cref="NotInChain"/> with the scenario it needs and why the ship chain cannot run it (a client-only draw, a mod the ship world does not load, a lab-only seam);
	/// anything else is a FAIL, and so is a listed hook that did run (the list is stale) and a listed Projectile.AI stub whose sibling hook points did not run either.
	/// The IL patches (MonoModHooks.Modify / ILHook) do not run at call time; they are listed in <see cref="IlPatches"/> and counted as installed.
	/// </summary>
	public static class HookLiveCheck
	{
		private static int _pass, _fail;
		private static void Check(Mod m, string name, bool ok) { if (ok) _pass++; else _fail++; m.Logger.Info($"[HookLive] {(ok ? "PASS" : "FAIL")} {name}"); }

		/// <summary>Detours that may legitimately have no calls in the ship chain: "Type.Method" -> the scenario that runs them, and why the chain does not. Keep every entry honest: a listed hook that DID run fails the check (the entry is stale).</summary>
		private static readonly Dictionary<string, string> NotInChain = new()
		{
			// ---- client only: a dedicated server draws nothing, plays no sound, saves no player file; the ship world is a dedicated server. Scenario: a real client session (the client profiler, draw and audio hooks are installed only there)
			// (ClientProf.DoUpdate is not listed: the perf check's profiler-on arm, "Levy bucket phase", installs the profiler on the test server for one window through ClientProf.TestServer, so it runs in the chain; it only times Main.DoUpdate)
			{ "ClientProf.DoDraw", "client only: the client-side profiler's draw timing; a server draws nothing" }, { "ClientProf.DrawNpcs", "client only" }, { "ClientProf.DrawProj", "client only" },
			{ "ClientProf.DrawDust", "client only" }, { "ClientProf.DrawGore", "client only" }, { "ClientProf.DrawInterface", "client only" }, { "ClientProf.DrawPlayers", "client only" }, { "ClientProf.DamageTimer", "client only" },
			{ "PerfAnalyzer.DoDraw", "client only: draw timing" }, { "PerfAnalyzer.DrawNpcs", "client only" }, { "PerfAnalyzer.DrawProjectiles", "client only" }, { "PerfAnalyzer.DrawDust", "client only" }, { "PerfAnalyzer.DrawGore", "client only" },
			{ "PerfAnalyzer.DrawInterface", "client only" }, { "PerfAnalyzer.DrawPlayers", "client only" }, { "PerfAnalyzer.ModifyLayers", "client only: map layer timing (the check wraps layers itself through PerfAnalyzer.WrapLayers)" },
			{ "ViewBiome.DoDraw", "client only: the camera view's biome swap" }, { "ViewBiome.UpdateAudio", "client only" },
			{ "OwnerPuppet.DrawDirectDetour", "client only: a held shot's draw" }, { "SoldierHooks.DrawDirectDetour", "client only: a hook projectile's draw" },
			{ "AudioCapLog.EngineUpdate", "client only, and only with LEVY_AUDIOCAP=1" }, { "ArmySoundThrottle.NpcAI", "client only: the sound throttle is not installed on a server (its projectile update hook is: it is checked in [IdentityWorld] deadhooks)" },
			{ "ArmySoundThrottle.Play", "client only: a server plays no sound" },
			{ "SaveGateSystem.Detour1", "client only: a character file save (one overload of the two runs per tML version; the other is the one installed)" },
			// ---- a mod the ship chain does not load
			{ "PlayerBardCompat.Detour", "needs ThoriumMod: the ship chain does not load it" }, { "TeamThorium.HandleNearbyDetour", "needs ThoriumMod: the ship chain does not load it" },
			// ---- Calamity only: required whenever Calamity is loaded (the Calamity half of the chain)
			{ "CalamityTargetingCompat.PreAiDetour", "needs CalamityMod: the vanilla half does not load it" }, { "CalamityTargetingCompat.SendExtraAiDetour", "needs CalamityMod: the vanilla half does not load it" },
			// ---- scenarios the ship world does not stage (each fires in play)
			{ "FollowerTravelSystem.Player_Spawn", "a real player respawning: the ship world's commander is a stand-in that never respawns" },
			{ "TeamNebula.Hook", "a real player wearing Nebula armor picking up a booster: not staged" }, { "TeamSpectre.Hook", "a real player in Spectre armor healing from a ghost: not staged" },
			{ "WarbandsLab.KeepStandIn", "Warbands' own lab seam (UpdateConnectedClients), installed only by its lab runs" }, { "SpawnSampler.KeepGoing", "Warbands' own lab seam (UpdateConnectedClients), installed only by its lab runs" },
		};

		/// <summary>Projectile.AI is a one-line stub the JIT inlines into Projectile.Update, so a detour on it is never called. It is kept as a fallback beside the VanillaAI and ProjectileLoader.ProjectileAI detours of the same wrapper; the wrapper is live when any one of them runs.</summary>
		private static bool IsAiStub(string key) => key.EndsWith(".AiDetour");

		/// <summary>IL patches (applied at load, so they have no call counter): each is exercised by the check named beside it.</summary>
		private static readonly string[] IlPatches =
		{
			"CalamityTargetingCompat (Calamity NPC AI targeting): [IdentityWorld]/[BossBugs] boss targeting checks",
			"PairCull.Manipulate (Projectile.Damage pair culling): [PerfBoss]",
			"AllyHealHook / MedicalBagHook (Thorium): Thorium is not in the ship chain",
			"PerfEntityHooks / PerfAttrib ILHooks: [PerfAnalyzer]",
			"IdentityGuard.Wrap (other mods' ModPlayer hooks): [IdentityWorld] identity guard census",
		};

		/// <summary>Drives the detours that only particular game calls reach, once each, so they are counted: a retarget by an NPC that uses the upgraded or the invasion / non-bee search, an NPC transforming, the player's heal text, a chat line. Each call runs the original too, harmlessly, on a pinned slime.</summary>
		private static void Exercise(Mod mod)
		{
			try
			{
				Main.NewText("HookLive");
				Vector2 at = Main.player[0] != null ? Main.player[0].Center : new Vector2(1800 * 16f, 290 * 16f);
				int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("HookLive"), (int)at.X, (int)at.Y, Terraria.ID.NPCID.GreenSlime);
				if (i >= 0 && i < Main.maxNPCs)
				{
					NPC n = Main.npc[i];
					n.TargetClosestUpgraded(); Terraria.Utilities.NPCUtils.TargetClosestNonBees(n); Terraria.Utilities.NPCUtils.TargetClosestOldOnesInvasion(n);
					n.Transform(Terraria.ID.NPCID.BlueSlime);
					n.active = false;
				}
				if (Main.player[0] != null) Main.player[0].HealEffect(1, false);
			}
			catch (Exception e) { mod.Logger.Info("[HookLive] OBSERVED exercise threw " + e.GetType().Name + ": " + e.Message); }
		}

		public static void Run(Mod mod)
		{
			if (Environment.GetEnvironmentVariable("LEVY_HOOKLIVE") == "0") return;
			_pass = _fail = 0;
			int unreadable = 0; var live = new List<string>(); var dead = new List<string>(); var listedDead = new List<string>(); int total = 0;
			var byType = new Dictionary<string, List<(string method, int hits)>>();
			Exercise(mod);
			foreach (Mod m in ModLoader.Mods.Where(x => x.Name == "Levy" || x.Name == "Mechanization" || x.Name == "Warbands"))
			{
				Type[] types; try { types = m.Code.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); } catch (Exception) { types = Type.EmptyTypes; }
				foreach (Type t in types)
				{
					FieldInfo[] fields; try { fields = t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); } catch (Exception) { unreadable++; continue; } // a type that names a weak-referenced mod that is not loaded (MagicStorage, Thorium) cannot be reflected
					foreach (FieldInfo f in fields)
					{
						if (!f.Name.StartsWith("_hl_") || f.FieldType != typeof(int)) continue; // the name first: the type of another field may name an unloaded weak reference
						string meth = f.Name.Substring(4); int us = meth.LastIndexOf('_'); if (us > 0 && int.TryParse(meth.Substring(us + 1), out _)) meth = meth.Substring(0, us);
						int hits = (int)f.GetValue(null); total++;
						string key = t.DeclaringType != null ? t.DeclaringType.Name + "." + t.Name + "." + meth : t.Name + "." + meth;
						string shortKey = t.Name + "." + meth;
						if (!byType.TryGetValue(m.Name + ":" + t.FullName, out var list)) byType[m.Name + ":" + t.FullName] = list = new();
						list.Add((meth, hits));
						bool listed = NotInChain.TryGetValue(shortKey, out string why) || NotInChain.TryGetValue(key, out why);
						if (listed && why.StartsWith("needs CalamityMod") && ModLoader.HasMod("CalamityMod")) listed = false; // the scenario is loaded: it must run
						if (hits > 0) { live.Add(m.Name + ":" + shortKey + "=" + hits); if (listed) Check(mod, $"hooklive: {m.Name}:{shortKey} is listed as not running in the chain ({why}) but ran {hits} times: remove it from the list", false); }
						else if (listed) listedDead.Add(m.Name + ":" + shortKey + " [" + why + "]");
						else if (IsAiStub(shortKey)) { /* judged with its family below */ }
						else dead.Add(m.Name + ":" + shortKey);
					}
				}
			}
			// a Projectile.AI stub detour is dead by construction; its wrapper is live when a sibling hook point of the same type ran
			foreach (var kv in byType)
			{
				var stub = kv.Value.FirstOrDefault(x => x.method == "AiDetour");
				if (stub.method == null) continue;
				var sibs = kv.Value.Where(x => x.method == "VanillaAiDetour" || x.method == "LoaderAiDetour").ToList();
				bool anyLive = stub.hits > 0 || sibs.Any(x => x.hits > 0);
				string type = kv.Key;
				// the wrapper of a family that sits only on hook points a scenario reaches (flyer slot, Zenith) is judged by its own scenario in the dead-hooks check; here only that some sibling exists and ran
				if (sibs.Count == 0) Check(mod, $"hooklive: {type} has a Projectile.AI detour and no VanillaAI / ProjectileLoader sibling (a wrapper on the inlined stub alone never runs)", false);
				else Check(mod, $"hooklive: {type} wrapper runs through a hook point that fires (AI stub {stub.hits}, " + string.Join(", ", sibs.Select(x => x.method + " " + x.hits)) + ")", anyLive);
			}
			mod.Logger.Info($"[HookLive] OBSERVED {total} detour counters in Levy, Mechanization and Warbands: {live.Count} ran, {listedDead.Count} listed as not running in the chain, {dead.Count} dead; {unreadable} types name a mod that is not loaded and cannot be read");
			mod.Logger.Info("[HookLive] OBSERVED not run in the chain (each with its scenario): " + (listedDead.Count == 0 ? "none" : string.Join("; ", listedDead)));
			mod.Logger.Info("[HookLive] OBSERVED IL patches (installed at load, exercised by the named checks): " + string.Join("; ", IlPatches));
			foreach (string d in dead) Check(mod, $"hooklive: {d} ran at least once during the chain (or is listed with its scenario)", false);
			Check(mod, $"hooklive: every installed detour ran, or is listed with the scenario that needs it ({live.Count} ran, {listedDead.Count} listed, {dead.Count} dead of {total})", dead.Count == 0 && total > 0);
			mod.Logger.Info($"[HookLive] self-check pass={_pass} fail={_fail}");
		}
	}
}
