using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Boss;
using Levy.Common.Config;
using Levy.Common.Util;

namespace Levy.Common.Systems
{
	/// <summary>One boss as the tier math sees it: which tier, how much it is worth, and whether it is down. Weight &lt;= 0 means "unweighted" (a boss from any mod).</summary>
	public readonly struct TierBoss
	{
		public readonly int Tier; public readonly float Weight; public readonly bool Downed;
		public TierBoss(int tier, float weight, bool downed) { Tier = tier; Weight = weight; Downed = downed; }
	}

	/// <summary>
	/// PURE tier math (no game state), so the boot self-check can drive it. Three tiers: 0 pre-hardmode (anchor Wall of Flesh), 1 hardmode (anchor Moon Lord), 2 post-Moon Lord.
	/// Levies = base + sum over tiers of share(tier) x (target(tier) - target(tier-1)), where share = weight of the tier's defeated bosses / weight of all known bosses of the tier.
	/// Vanilla bosses carry their classic command-point value as weight, so a vanilla-only world reproduces the old numbers exactly; any other boss is "unweighted" and is
	/// worth the mean weight of its tier's vanilla bosses, so a mod adding bosses dilutes each boss's share and never raises the end-of-tier total. A tier with no known bosses contributes 0.
	/// </summary>
	public static class LevyTierMath
	{
		public const int TierCount = 3;

		public static int Compute(IEnumerable<TierBoss> bosses, int baseLevies, int preTarget, int hmTarget, int postTarget, int max)
		{
			float[] total = new float[TierCount], down = new float[TierCount], weighted = new float[TierCount];
			int[] weightedCount = new int[TierCount], unweighted = new int[TierCount], unweightedDown = new int[TierCount];
			foreach (var b in bosses)
			{
				int t = Math.Clamp(b.Tier, 0, TierCount - 1);
				if (b.Weight > 0f) { weightedCount[t]++; weighted[t] += b.Weight; total[t] += b.Weight; if (b.Downed) down[t] += b.Weight; }
				else { unweighted[t]++; if (b.Downed) unweightedDown[t]++; }
			}
			float t0 = baseLevies, t1 = Math.Max(preTarget, t0), t2 = Math.Max(hmTarget, t1), t3 = Math.Max(postTarget, t2);
			float[] span = { t1 - t0, t2 - t1, t3 - t2 };
			float levies = baseLevies;
			for (int t = 0; t < TierCount; t++)
			{
				float mean = weightedCount[t] > 0 ? weighted[t] / weightedCount[t] : 1f;
				float tot = total[t] + unweighted[t] * mean, dn = down[t] + unweightedDown[t] * mean;
				if (tot > 0f) levies += span[t] * (dn / tot);
			}
			return Math.Clamp((int)Math.Floor(levies + 0.5f + 1e-3f), 1, Math.Max(1, max));
		}
	}

	/// <summary>
	/// How many levies (command points) this world allows. SERVER / single player computes it from boss progress once a second and on every kill; it rides WorldData to
	/// clients (NetSend/NetReceive), who only read the number. Bosses come from three places: the vanilla table (flags are authoritative, weights = the old command-point values),
	/// Boss Checklist's registry when that mod is loaded (tier by registered progression relative to its Wall of Flesh / Moon Lord entries), and otherwise a per-world learned
	/// tier: any other boss is classified the moment it is first defeated (before Wall of Flesh = pre-hardmode, before Moon Lord = hardmode, after = post-Moon Lord) and saved.
	/// </summary>
	public class LevyProgressionSystem : ModSystem
	{
		public const string VanillaSource = "Terraria";

		private static int _levies;
		private static bool _haveValue;
		private static readonly Dictionary<string, int> _learned = new(); // boss key (BossDownedSystem identity) -> tier
		private static List<BcBoss> _bc;       // null = not queried / unavailable
		private static bool _bcTried;
		private int _timer;

		private sealed class BcBoss { public string Key; public string Source, Internal; public float Progression; public Func<bool> Downed; public List<int> Npcs = new(); }

		/// <summary>Levies available right now. Clients read the synced value; the server/single player computes on demand.</summary>
		public static int Levies
		{
			get
			{
				if (!_haveValue && LevyUtil.IsServerOrSingle) Recompute(false);
				return _haveValue ? _levies : LevyConstants.CommandPointsBase;
			}
		}

		// ---- vanilla table: tier, weight, downed, owning NPC types -------------------------------------------
		private readonly struct VBoss
		{
			public readonly string Id; public readonly int Tier; public readonly float Weight; public readonly Func<bool> Downed; public readonly int[] Types;
			public VBoss(string id, int tier, float w, Func<bool> d, params int[] types) { Id = id; Tier = tier; Weight = w; Downed = d; Types = types; }
		}

		private static readonly VBoss[] Vanilla =
		{
			new("KingSlime", 0, LevyConstants.CpKingSlime, () => NPC.downedSlimeKing, NPCID.KingSlime),
			new("EyeOfCthulhu", 0, LevyConstants.CpEyeOfCthulhu, () => NPC.downedBoss1, NPCID.EyeofCthulhu),
			new("EaterOrBrain", 0, LevyConstants.CpEaterOrBrain, () => NPC.downedBoss2, NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail, NPCID.BrainofCthulhu),
			new("QueenBee", 0, LevyConstants.CpQueenBee, () => NPC.downedQueenBee, NPCID.QueenBee),
			new("Skeletron", 0, LevyConstants.CpSkeletron, () => NPC.downedBoss3, NPCID.SkeletronHead),
			new("Deerclops", 0, LevyConstants.CpDeerclops, () => NPC.downedDeerclops, NPCID.Deerclops),
			new("WallOfFlesh", 0, LevyConstants.CpWallOfFlesh, () => Main.hardMode, NPCID.WallofFlesh),
			new("QueenSlime", 1, LevyConstants.CpQueenSlime, () => NPC.downedQueenSlime, NPCID.QueenSlimeBoss),
			new("Destroyer", 1, LevyConstants.CpEachMech, () => NPC.downedMechBoss1, NPCID.TheDestroyer),
			new("Twins", 1, LevyConstants.CpEachMech, () => NPC.downedMechBoss2, NPCID.Retinazer, NPCID.Spazmatism),
			new("SkeletronPrime", 1, LevyConstants.CpEachMech, () => NPC.downedMechBoss3, NPCID.SkeletronPrime),
			new("Plantera", 1, LevyConstants.CpPlantera, () => NPC.downedPlantBoss, NPCID.Plantera),
			new("Golem", 1, LevyConstants.CpGolem, () => NPC.downedGolemBoss, NPCID.Golem),
			new("EmpressOfLight", 1, LevyConstants.CpEmpress, () => NPC.downedEmpressOfLight, NPCID.HallowBoss),
			new("DukeFishron", 1, LevyConstants.CpFishron, () => NPC.downedFishron, NPCID.DukeFishron),
			new("LunaticCultist", 1, LevyConstants.CpCultist, () => NPC.downedAncientCultist, NPCID.CultistBoss),
			new("MoonLord", 1, LevyConstants.CpMoonLord, () => NPC.downedMoonlord, NPCID.MoonLordCore),
		};

		private static bool IsVanillaType(int type)
		{
			foreach (var v in Vanilla) foreach (int t in v.Types) if (t == type) return true;
			return false;
		}

		/// <summary>Tier a boss killed RIGHT NOW belongs to, from the world's own flags.</summary>
		public static int CurrentWorldTier() => !Main.hardMode ? 0 : NPC.downedMoonlord ? 2 : 1;

		/// <summary>Server / single player: a boss was just recorded as defeated. A boss outside the vanilla table has its tier fixed by this first defeat.</summary>
		internal static void OnBossRecorded(string key, int npcType)
		{
			if (!LevyUtil.IsServerOrSingle || string.IsNullOrEmpty(key)) return;
			if (!IsVanillaType(npcType) && !_learned.ContainsKey(key)) _learned[key] = CurrentWorldTier();
			Recompute(true);
		}

		// ---- Boss Checklist ----------------------------------------------------------------------------------
		// API (Boss Checklist wiki, "GetBossInfoDictionary"): Call("GetBossInfoDictionary", Mod caller, string apiVersion) -> Dictionary<string, Dictionary<string, object>>,
		// one entry per registered boss/miniboss/event with "modSource", "internalName", "progression" (float), "downed" (Func<bool>), "isBoss", "npcIDs" (List<int>).
		private static void EnsureBossChecklist()
		{
			if (_bcTried && _bc != null) return;
			if (_bcTried && Main.GameUpdateCount % 600 != 0) return; // retry rarely
			_bcTried = true;
			try
			{
				if (!ModLoader.TryGetMod("BossChecklist", out Mod bc)) { _bc = null; return; }
				object result = null;
				foreach (string ver in new[] { "1.2", "1.1", "1.0" })
				{
					try { result = bc.Call("GetBossInfoDictionary", Levy.Instance, ver); } catch { result = null; }
					if (result is IDictionary) break;
				}
				if (result is not IDictionary all) { _bc = null; return; }
				var list = new List<BcBoss>();
				foreach (DictionaryEntry de in all)
				{
					if (de.Value is not IDictionary info) continue;
					if (!Get<bool>(info, "isBoss")) continue; // minibosses and events never count
					var b = new BcBoss
					{
						Key = de.Key as string ?? "",
						Source = Get<string>(info, "modSource") ?? "",
						// Boss Checklist's dictionary has no "internalName" field; its key is "<modSource> <internalName>" (EntryInfo.cs:221).
						Internal = Get<string>(info, "internalName") ?? InternalFromKey(de.Key as string, Get<string>(info, "modSource")),
						Progression = Get<float>(info, "progression"),
						Downed = Get<Func<bool>>(info, "downed"),
					};
					if (Get<IEnumerable<int>>(info, "npcIDs") is { } ids) b.Npcs.AddRange(ids);
					list.Add(b);
				}
				_bc = list;
				Levy.Instance.Logger.Info($"Levy progression: Boss Checklist registry read, {list.Count} bosses");
			}
			catch (Exception e) { _bc = null; Levy.Instance.Logger.Warn("Levy progression: Boss Checklist read failed, using learned tiers: " + e.Message); }
		}

		private static string InternalFromKey(string key, string source)
		{
			if (string.IsNullOrEmpty(key)) return "";
			if (!string.IsNullOrEmpty(source) && key.StartsWith(source + " ", StringComparison.Ordinal)) return key.Substring(source.Length + 1);
			int sp = key.IndexOf(' ');
			return sp >= 0 ? key.Substring(sp + 1) : key;
		}

		private static T Get<T>(IDictionary d, string k) { try { return d.Contains(k) && d[k] is T t ? t : default; } catch { return default; } }

		private static bool BcAnchor(List<BcBoss> bc, string internalName, out float prog)
		{
			foreach (var b in bc) if (b.Source == VanillaSource && string.Equals(b.Internal, internalName, StringComparison.OrdinalIgnoreCase)) { prog = b.Progression; return true; }
			prog = 0; return false;
		}

		private static bool KnownVanillaName(string n)
		{
			foreach (var v in Vanilla) if (string.Equals(v.Id, n, StringComparison.OrdinalIgnoreCase)) return true;
			return n.Equals("TheDestroyer", StringComparison.OrdinalIgnoreCase) || n.Equals("TheTwins", StringComparison.OrdinalIgnoreCase)
				|| n.Equals("EaterOfWorlds", StringComparison.OrdinalIgnoreCase) || n.Equals("BrainOfCthulhu", StringComparison.OrdinalIgnoreCase);
		}

		// ---- build + compute ---------------------------------------------------------------------------------
		private static List<TierBoss> BuildBosses()
		{
			var list = new List<TierBoss>(32);
			foreach (var v in Vanilla) list.Add(new TierBoss(v.Tier, v.Weight, v.Downed()));
			var covered = new HashSet<int>();
			foreach (var v in Vanilla) foreach (int t in v.Types) covered.Add(t);
			var coveredKeys = new HashSet<string>();

			EnsureBossChecklist();
			if (_bc != null && BcAnchor(_bc, "WallOfFlesh", out float wof) && BcAnchor(_bc, "MoonLord", out float ml))
			{
				foreach (var b in _bc)
				{
					bool vanillaEntry = false;
					foreach (int t in b.Npcs) if (covered.Contains(t)) vanillaEntry = true;
					if (b.Source == VanillaSource && (vanillaEntry || KnownVanillaName(b.Internal))) continue;
					int tier = b.Progression <= wof ? 0 : b.Progression <= ml ? 1 : 2;
					bool down = false;
					try { down = b.Downed != null && b.Downed(); } catch { }
					foreach (int t in b.Npcs) { coveredKeys.Add(BossDownedSystem.KeyOfType(t)); if (!down && BossDownedSystem.IsTypeDowned(t)) down = true; }
					list.Add(new TierBoss(tier, 0f, down));
				}
			}
			// learned tiers: every defeated boss not already accounted for by Boss Checklist
			foreach (var kv in _learned)
				if (!coveredKeys.Contains(kv.Key)) list.Add(new TierBoss(kv.Value, 0f, true));
			return list;
		}

		private static void Recompute(bool send)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			LevyConfig cfg = null; try { cfg = LevyConfig.Get; } catch { }
			int pre = cfg?.LeviesByEndOfPreHardmode ?? 20, hm = cfg?.LeviesByEndOfHardmode ?? 40, post = cfg?.LeviesByEndOfPostMoonLord ?? 60, max = cfg?.MaxLevies ?? 60;
			int value = LevyTierMath.Compute(BuildBosses(), LevyConstants.CommandPointsBase, pre, hm, post, max);
			bool changed = !_haveValue || value != _levies;
			_levies = value; _haveValue = true;
			if (changed && send && LevyUtil.IsServer) NetMessage.SendData(MessageID.WorldData);
		}

		public override void PostUpdateWorld()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			if (++_timer < 60) return;
			_timer = 0;
			try { Recompute(true); } catch (Exception e) { Mod.Logger.Warn("Levy progression recompute threw: " + e.Message); }
		}

		public override void OnWorldLoad() { _haveValue = false; _timer = 0; if (LevyUtil.IsServerOrSingle) { try { Recompute(false); } catch { } } }
		public override void ClearWorld() { _learned.Clear(); _haveValue = false; _levies = 0; }
		public override void Unload() { _learned.Clear(); _bc = null; _bcTried = false; }

		public override void SaveWorldData(TagCompound tag)
		{
			if (_learned.Count == 0) return;
			var l = new List<string>(); foreach (var kv in _learned) l.Add(kv.Value + "|" + kv.Key);
			tag["learnedBossTiers"] = l;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			_learned.Clear();
			foreach (string s in tag.GetList<string>("learnedBossTiers"))
			{
				int i = s.IndexOf('|');
				if (i > 0 && int.TryParse(s.AsSpan(0, i), out int t)) _learned[s.Substring(i + 1)] = Math.Clamp(t, 0, 2);
			}
		}

		public override void NetSend(BinaryWriter writer) => writer.Write((ushort)Math.Clamp(_haveValue ? _levies : LevyConstants.CommandPointsBase, 0, 65535));

		public override void NetReceive(BinaryReader reader) { _levies = reader.ReadUInt16(); _haveValue = true; }

		// ---- boot self-check ---------------------------------------------------------------------------------
		public override void PostAddRecipes()
		{
			var fails = new List<string>();
			try
			{
				int B = LevyConstants.CommandPointsBase;
				// 1. vanilla-only reproduces the legacy additive formula for EVERY subset of the vanilla boss entries.
				float[] w = new float[Vanilla.Length]; int[] tier = new int[Vanilla.Length];
				for (int i = 0; i < Vanilla.Length; i++) { w[i] = Vanilla[i].Weight; tier[i] = Vanilla[i].Tier; }
				int subsets = 1 << Vanilla.Length, bad = 0;
				for (int m = 0; m < subsets; m++)
				{
					var list = new List<TierBoss>(); int legacy = B;
					for (int i = 0; i < Vanilla.Length; i++) { bool d = (m >> i & 1) != 0; list.Add(new TierBoss(tier[i], w[i], d)); if (d) legacy += (int)w[i]; }
					if (LevyTierMath.Compute(list, B, 20, 40, 60, 60) != legacy) bad++;
				}
				if (bad != 0) fails.Add($"vanilla-only mismatch in {bad} of {subsets} subsets");
				List<TierBoss> V(bool pre, bool hm) { var l = new List<TierBoss>(); for (int i = 0; i < Vanilla.Length; i++) l.Add(new TierBoss(tier[i], w[i], tier[i] == 0 ? pre : hm)); return l; }
				int v0 = LevyTierMath.Compute(V(false, false), B, 20, 40, 60, 60), v1 = LevyTierMath.Compute(V(true, false), B, 20, 40, 60, 60), v2 = LevyTierMath.Compute(V(true, true), B, 20, 40, 60, 60);
				if (v0 != 4 || v1 != 20 || v2 != 40) fails.Add($"vanilla anchors 4/20/40 expected, got {v0}/{v1}/{v2}");
				// 2. a hypothetical mod adding 17 pre-hardmode bosses does not raise the end-of-tier total
				var mod = V(true, false); for (int i = 0; i < 17; i++) mod.Add(new TierBoss(0, 0f, true));
				int modAll = LevyTierMath.Compute(mod, B, 20, 40, 60, 60);
				var modNone = V(true, false); for (int i = 0; i < 17; i++) modNone.Add(new TierBoss(0, 0f, false));
				int modSkip = LevyTierMath.Compute(modNone, B, 20, 40, 60, 60);
				if (modAll != 20) fails.Add($"17 modded pre-HM bosses all down: expected 20, got {modAll}");
				if (modSkip >= 20 || modSkip <= B) fails.Add($"vanilla-only kills with 17 undefeated modded pre-HM bosses: expected diluted (4 < n < 20), got {modSkip}");
				// 3. post-Moon Lord: none known = stuck at 40; 5 known, 2 down = 40 + 20x0.4 = 48; all = 60; max clamps
				var p0 = V(true, true); var p2 = V(true, true); var pAll = V(true, true);
				for (int i = 0; i < 5; i++) { p0.Add(new TierBoss(2, 0f, false)); p2.Add(new TierBoss(2, 0f, i < 2)); pAll.Add(new TierBoss(2, 0f, true)); }
				int a = LevyTierMath.Compute(V(true, true), B, 20, 40, 60, 60), b = LevyTierMath.Compute(p2, B, 20, 40, 60, 60), c = LevyTierMath.Compute(pAll, B, 20, 40, 60, 60), d2 = LevyTierMath.Compute(pAll, B, 20, 40, 60, 50);
				int e0 = LevyTierMath.Compute(p0, B, 20, 40, 60, 60);
				if (a != 40 || e0 != 40 || b != 48 || c != 60 || d2 != 50) fails.Add($"post-ML expected 40/40/48/60/50, got {a}/{e0}/{b}/{c}/{d2}");
				Mod.Logger.Info($"Levy progression math: vanilla 4/20/40 ({subsets} subsets match legacy), +17 modded pre-HM all down={modAll} diluted={modSkip}, post-ML none/5known-0down/2of5/all/max50={a}/{e0}/{b}/{c}/{d2}");
			}
			catch (Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (fails.Count == 0) Mod.Logger.Info("Levy progression self-check: OK");
			else foreach (var f in fails) Mod.Logger.Warn("Levy progression self-check FAILED: " + f);
		}
	}
}
