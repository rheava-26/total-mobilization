using System;
using System.Collections.Generic;
using Terraria;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// GEAR CACHE PARITY (LEVY_GEARCACHE_PARITY=1, lab and world check): on every tick the cache would serve, both halves run from the same state. The dummy (Player, every ModPlayer, the worn items, the random generator),
	/// the levy's own build outputs and a digest of the world are imaged; the cached result is applied and imaged; everything is put back; the full build runs and is imaged; the two images are compared leaf by leaf.
	/// The full result is the one that stays, so the fight is the full-frame fight and every tick is a test of the cached one. Counters and the names of the leaves that differed: <see cref="GearCacheParity"/>.
	/// </summary>
	internal static class GearCacheParity
	{
		internal static bool On = Environment.GetEnvironmentVariable("LEVY_GEARCACHE_PARITY") == "1";
		internal static long Checks, Diverged;
		internal static readonly Dictionary<string, long> Fields = new();
		internal static readonly List<string> Samples = new();
		internal static readonly HashSet<int> Levies = new();
		internal static void Reset() { Checks = Diverged = 0; Fields.Clear(); Samples.Clear(); Levies.Clear(); }

		/// <summary>The world the build could touch: every NPC (alive, life, buffs, position) and every projectile (alive, type, owner).</summary>
		internal static long WorldDigest()
		{
			var h = new HashCode();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i]; h.Add(n.active); if (!n.active) continue;
				h.Add(n.type); h.Add(n.life); h.Add(n.position); h.Add(n.velocity);
				for (int b = 0; b < NPC.maxBuffs; b++) { h.Add(n.buffType[b]); h.Add(n.buffTime[b]); }
			}
			for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; h.Add(p.active); if (p.active) { h.Add(p.type); h.Add(p.owner); h.Add(p.timeLeft); } }
			return h.ToHashCode();
		}
	}

	public partial class SoldierNPC
	{
		private StateImage _par0, _parC, _parF;
		private readonly List<object> _parRoots = new(); private readonly List<string> _parNames = new();

		private void ParityRoots(Player d)
		{
			_parRoots.Clear(); _parNames.Clear();
			_parRoots.Add(d); _parNames.Add("P");
			foreach (var mp in d.ModPlayers) { _parRoots.Add(mp); _parNames.Add(mp.GetType().Name); }
			int i = 0;
			foreach (var it in Record.Gear.AllItems()) if (it != null && !it.IsAir) { _parRoots.Add(it); _parNames.Add("I" + i++ + ":" + it.Name); }
			Item held = d.inventory[d.selectedItem]; if (held != null && !held.IsAir) { _parRoots.Add(held); _parNames.Add("Held:" + held.Name); }
			_parRoots.Add(Main.rand); _parNames.Add("RNG");
		}

		/// <summary>One tick the cache serves: the cached result and the full build, from the same state, compared; the full build's result stays.</summary>
		private void GearCacheParityTick(GearCache gc, Player d, int savedNum, float savedSlots, int hs, int ha, ref long pf)
		{
			_par0 ??= new StateImage(); _parC ??= new StateImage(); _parF ??= new StateImage();
			ParityRoots(d);
			_par0.Capture(_parRoots, _parNames);
			int regen0 = _buffLifeRegen; bool cf0 = Main.SceneMetrics.HasCampfire, hl0 = Main.SceneMetrics.HasHeartLantern; int sp0 = GearUpkeep.Spawned.Count;
			long w0 = GearCacheParity.WorldDigest();

			gc.Apply(this, d);
			_parC.Capture(_parRoots, _parNames);
			int regenC = _buffLifeRegen; bool cfC = Main.SceneMetrics.HasCampfire, hlC = Main.SceneMetrics.HasHeartLantern; int spC = GearUpkeep.Spawned.Count - sp0; long wC = GearCacheParity.WorldDigest();

			_par0.Restore(); _buffLifeRegen = regen0; Main.SceneMetrics.HasCampfire = cf0; Main.SceneMetrics.HasHeartLantern = hl0;

			GearBuild(d, savedNum, savedSlots, hs, ha, ref pf);
			_parF.Capture(_parRoots, _parNames);
			int regenF = _buffLifeRegen; bool cfF = Main.SceneMetrics.HasCampfire, hlF = Main.SceneMetrics.HasHeartLantern; int spF = GearUpkeep.Spawned.Count - sp0; long wF = GearCacheParity.WorldDigest();

			var diffs = new List<string>();
			int n = _parC.DiffInto(_parF, diffs, 16);
			void Lv(bool same, string what) { if (!same) { n++; if (diffs.Count < 16) diffs.Add(what); } }
			Lv(regenC == regenF, "levy.buffLifeRegen"); Lv(cfC == cfF, "scene.HasCampfire"); Lv(hlC == hlF, "scene.HasHeartLantern"); Lv(spC == spF, "spawns"); Lv(wC == wF, "world");
			GearCacheParity.Checks++;
			if (n > 0)
			{
				GearCacheParity.Diverged++; GearCacheParity.Levies.Add(NPC.whoAmI);
				foreach (var f in diffs) { GearCacheParity.Fields.TryGetValue(f, out long c); GearCacheParity.Fields[f] = c + 1; }
				if (GearCacheParity.Samples.Count < 12) GearCacheParity.Samples.Add("tick " + Main.GameUpdateCount + " levy " + NPC.whoAmI + " (" + GearSummary() + "): " + n + " leaves: " + string.Join(", ", diffs));
			}
		}
	}
}
