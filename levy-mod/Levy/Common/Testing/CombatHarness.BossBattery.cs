using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS BATTERY additions to the calrev harness (measurement only; lab 2026-11 "bosslab2"). Two jobs:
	///  1. Early-Hardmode bosses for the same lab: twins prime brim cryo aqua cal plant eol fish qslime wof (destroyer already exists). Spawned through NPC.SpawnOnPlayer like a summon item, at night, Hardmode on.
	///  2. Hit-immunity instrumentation. Every soldier projectile is owned by the commander slot (Main.myPlayer; 0 under LEVY_CR_SP=1), so vanilla's per-owner npc.immune[owner] is shared by the whole army. The
	///     detour on Projectile.Damage classifies every (projectile, boss part) contact at the exact moment vanilla runs its gate, and the spec token imm=shared|local|static|perlevy changes how a soldier shot is gated
	///     for A/B runs: local (usesLocalNPCImmunity, 10 ticks), static (usesIDStaticNPCImmunity, 10 ticks), perlevy (each levy has its own npc.immune[] column, the way N players would). shared = the product.
	/// Nothing here changes product code; the detour only exists while a calrev spec is running.
	/// </summary>
	public partial class CombatHarness
	{
		// ------------------------------------------------------------------------------------------------------------------ bosses
		internal static bool BbIsBoss(string b) => b is "twins" or "prime" or "brim" or "cryo" or "aqua" or "cal" or "plant" or "eol" or "fish" or "qslime" or "wof" or "levi";

		internal static int BbType(string b) => b switch
		{
			"twins" => NPCID.Retinazer, "prime" => NPCID.SkeletronPrime, "brim" => CrCalNpc("BrimstoneElemental"), "cryo" => CrCalNpc("Cryogen"), "aqua" => CrCalNpc("AquaticScourgeHead"),
			"cal" => CrCalNpc("CalamitasClone"), "plant" => NPCID.Plantera, "eol" => NPCID.HallowBoss, "fish" => NPCID.DukeFishron, "qslime" => NPCID.QueenSlimeBoss, "wof" => NPCID.WallofFlesh, "levi" => CrCalNpc("Anahita"), _ => -1
		};

		private static void BbAddCal(HashSet<int> set, params string[] names) { foreach (string n in names) { int t = CrCalNpc(n); if (t > 0) set.Add(t); else global::Levy.Levy.Instance.Logger.Warn("HARNESS-BB: unknown Calamity NPC " + n); } }

		/// <summary>Part / add NPC types of a battery boss. True when the boss is a battery boss (CrSetupBoss returns).</summary>
		private static bool BbSetup(CrState s, CrRun run)
		{
			if (!BbIsBoss(run.Boss)) return false;
			switch (run.Boss)
			{
				case "twins": s.PartTypes.Add(NPCID.Retinazer); s.PartTypes.Add(NPCID.Spazmatism); break;
				case "prime": foreach (int t in new[] { NPCID.PrimeCannon, NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeLaser }) s.AddTypes.Add(t); break;
				case "brim": BbAddCal(s.AddTypes, "Brimling"); break;
				case "cryo": BbAddCal(s.AddTypes, "CryogenShield"); break;
				case "aqua":
					s.BodyType = CrCalNpc("AquaticScourgeBody"); s.TailType = CrCalNpc("AquaticScourgeTail");
					foreach (int t in new[] { s.BossType, s.BodyType, s.TailType }) if (t > 0) s.PartTypes.Add(t);
					break;
				case "cal": BbAddCal(s.AddTypes, "Cataclysm", "Catastrophe", "SoulSeeker"); break;
				case "plant": s.AddTypes.Add(NPCID.PlanterasHook); s.AddTypes.Add(NPCID.PlanterasTentacle); break;
				case "fish": s.AddTypes.Add(NPCID.Sharkron); s.AddTypes.Add(NPCID.Sharkron2); s.AddTypes.Add(NPCID.DetonatingBubble); break;
				case "qslime": s.AddTypes.Add(NPCID.QueenSlimeMinionBlue); s.AddTypes.Add(NPCID.QueenSlimeMinionPink); s.AddTypes.Add(NPCID.QueenSlimeMinionPurple); break;
				case "levi": // Anahita is the spawned boss, the Leviathan joins her (death mode at once, otherwise below 40% of her life): both are parts of the one fight, the add NPCs are the aberrations
					BbAddCal(s.PartTypes, "Anahita", "Leviathan"); BbAddCal(s.AddTypes, "AquaticAberration", "AnahitasIceShield"); s.AddTypes.Add(NPCID.DetonatingBubble); break;
				case "wof": s.AddTypes.Add(NPCID.WallofFleshEye); s.AddTypes.Add(NPCID.TheHungry); s.AddTypes.Add(NPCID.TheHungryII); s.AddTypes.Add(NPCID.LeechHead); break;
			}
			return true;
		}

		private static bool _bbHardSaved, _bbHardPrev;
		/// <summary>Battery fights run in Hardmode at night (twins / prime / destroyer / empress flee or enrage by day).</summary>
		private static void BbBegin(CrRun run)
		{
			if (!BbIsBoss(run.Boss) && run.Boss != "destroyer") return;
			if (!_bbHardSaved) { _bbHardPrev = Main.hardMode; _bbHardSaved = true; }
			Main.hardMode = true;
			NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = NPC.downedMechBossAny = false; NPC.downedPlantBoss = false; NPC.downedFishron = false; NPC.downedEmpressOfLight = false; NPC.downedQueenSlime = false;
			BbCalDowned(false);
		}
		private static void BbEnd()
		{
			if (_bbHardSaved) { Main.hardMode = _bbHardPrev; _bbHardSaved = false; }
		}

		private static Type _bbDownedType;
		private static void BbCalDowned(bool v)
		{
			try
			{
				if (_bbDownedType == null && ModLoader.TryGetMod("CalamityMod", out Mod cal)) _bbDownedType = cal.Code.GetType("CalamityMod.DownedBossSystem");
				foreach (string fn in new[] { "downedAquaticScourge", "downedCryogen", "downedBrimstoneElemental", "downedCalamitasClone", "downedLeviathan" })
				{
					var p = _bbDownedType?.GetProperty(fn, BindingFlags.Public | BindingFlags.Static);
					if (p != null) p.SetValue(null, v); else _bbDownedType?.GetField(fn, BindingFlags.Public | BindingFlags.Static)?.SetValue(null, v);
				}
			}
			catch { }
		}
		private static bool BbCalGet(string fn)
		{
			try { var p = _bbDownedType?.GetProperty(fn, BindingFlags.Public | BindingFlags.Static); if (p != null) return (bool)p.GetValue(null); var f = _bbDownedType?.GetField(fn, BindingFlags.Public | BindingFlags.Static); return f != null && (bool)f.GetValue(null); } catch { return false; }
		}

		/// <summary>Did the multi-part boss die (its downed flag, else the last live life reading under a quarter of the total)?</summary>
		private static bool BbWon(string boss, int lastLife, long lifeMax)
		{
			bool flag = boss switch
			{
				"twins" => NPC.downedMechBoss2, "prime" => NPC.downedMechBoss3, "destroyer" => NPC.downedMechBoss1, "aqua" => BbCalGet("downedAquaticScourge"), "plant" => NPC.downedPlantBoss,
				"cryo" => BbCalGet("downedCryogen"), "brim" => BbCalGet("downedBrimstoneElemental"), "cal" => BbCalGet("downedCalamitasClone"), "levi" => BbCalGet("downedLeviathan"), "qslime" => NPC.downedQueenSlime, "fish" => NPC.downedFishron, "eol" => NPC.downedEmpressOfLight, _ => false
			};
			return flag || (lifeMax > 0 && lastLife <= 0.25 * lifeMax);
		}

		/// <summary>Biome flags the fight is meant to be in (the commander and the levy dummies mirror them): Hallow for the Empress and Queen Slime, Snow for Cryogen, Jungle for Plantera, Ocean for Fishron.</summary>
		private static void BbZones(Player p, CrRun r)
		{
			if (!BbIsBoss(r.Boss)) return;
			if (r.Boss is "eol" or "qslime") p.ZoneHallow = true;
			if (r.Boss == "cryo") p.ZoneSnow = true;
			if (r.Boss == "plant") p.ZoneJungle = true;
			if (r.Boss is "fish" or "levi") p.ZoneBeach = true;
			if (r.Boss is "brim" or "cal") BbSetModBiome(p, "CalamityMod/BrimstoneCragsBiome");
			if (r.Boss == "aqua") BbSetModBiome(p, "CalamityMod/SulphurousSeaBiome");
		}

		private static bool BbInBiome(Player p, string name) { try { return ModContent.TryFind(name, out ModBiome mb) && p.InModBiome(mb); } catch { return false; } }
		private static PropertyInfo _bbZeroIdx;
		/// <summary>Switches a modded biome on for the stand-in commander (Player.modBiomeFlags, the storage behind InModBiome); BiomeMirror copies it onto the levy dummies the bosses target.</summary>
		private static void BbSetModBiome(Player p, string name)
		{
			try
			{
				if (!ModContent.TryFind(name, out ModBiome mb)) { if (_bbWarned.Add(name)) global::Levy.Levy.Instance.Logger.Warn("HARNESS-BB: unknown biome " + name); return; }
				_bbZeroIdx ??= typeof(ModBiome).GetProperty("ZeroIndexType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				int idx = _bbZeroIdx != null ? (int)_bbZeroIdx.GetValue(mb) : -1;
				var arr = global::Levy.Common.Targeting.BiomeMirror.FlagsField?.GetValue(p) as System.Collections.BitArray;
				if (arr != null && idx >= 0 && idx < arr.Length) arr[idx] = true;
			}
			catch (Exception e) { if (_bbWarned.Add("x" + name)) global::Levy.Levy.Instance.Logger.Warn("HARNESS-BB: biome flag failed " + e.Message); }
		}
		private static readonly HashSet<string> _bbWarned = new();

		/// <summary>After the first boss has been spawned and moved: Twins spawn as a pair, every battery boss also gets its spawn logged.</summary>
		private static void BbAfterSpawn(CrState s, CrRun run, int ei, int bx, int by)
		{
			if (run.Boss != "twins") return;
			NPC.SpawnOnPlayer(0, NPCID.Spazmatism);
			for (int q = 0; q < Main.maxNPCs; q++)
				if (Main.npc[q].active && Main.npc[q].type == NPCID.Spazmatism) { Main.npc[q].position = new Vector2(bx + 6 * 16, by) - Main.npc[q].Size / 2f; Main.npc[q].velocity = Vector2.Zero; Main.npc[q].timeLeft = 99999; break; }
		}

		/// <summary>One log line at tick 60 and 300: the biome flags the boss can see on the commander (slot 0) and on the null-player slot 255 the levy targets resolve to.</summary>
		private static void BbZoneLog(CrState st)
		{
			if (!(BbIsBoss(st.Run.Boss) || st.Run.Boss == "destroyer") || (st.Tick != 60 && st.Tick != 300)) return;
			Player a = Main.player[0], b = Main.player[255];
			global::Levy.Levy.Instance.Logger.Info($"HARNESS-BB: zones tick {st.Tick} {st.Run.Boss} cmd[jungle={a.ZoneJungle} beach={a.ZoneBeach} snow={a.ZoneSnow} hallow={a.ZoneHallow}] slot255[active={b.active} jungle={b.ZoneJungle} beach={b.ZoneBeach} snow={b.ZoneSnow} hallow={b.ZoneHallow}] bossTarget={Main.npc[st.BossIdx].target} crag255={BbInBiome(b, "CalamityMod/BrimstoneCragsBiome")} sulph255={BbInBiome(b, "CalamityMod/SulphurousSeaBiome")} crag0={BbInBiome(a, "CalamityMod/BrimstoneCragsBiome")}");
		}

		private static void BbCensus(CrState st)
		{
			var g = new SortedDictionary<int, (int n, long sumMax, int minMax, int maxMax, int linked)>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i]; if (!n.active || !(n.type == st.BossType || st.PartTypes.Contains(n.type))) continue;
				g.TryGetValue(n.type, out var v); bool lk = n.realLife >= 0 && n.realLife != n.whoAmI;
				g[n.type] = (v.n + 1, v.sumMax + n.lifeMax, v.n == 0 ? n.lifeMax : Math.Min(v.minMax, n.lifeMax), Math.Max(v.maxMax, n.lifeMax), v.linked + (lk ? 1 : 0));
			}
			global::Levy.Levy.Instance.Logger.Info("HARNESS-BB: parts " + string.Join(" | ", g.Select(kv => $"{CrNpcName(kv.Key)} n={kv.Value.n} lifeMax {kv.Value.minMax}-{kv.Value.maxMax} realLife-linked={kv.Value.linked}")));
		}

		// ------------------------------------------------------------------------------------------------------------------ hit immunity instrumentation
		internal static string BbImmMode = "shared";
		private static BbStats _bb;
		internal sealed class BbStats
		{
			// class index 0 R, 1 M, 2 E, 3 S, 4 other (officer / unattributed)
			public const int NC = 5, NK = 12;
			// counters: 0 contact calls (one per projectile-tick that overlapped a boss part when vanilla ran its hit loop), 1 candidate (projectile, boss part) pairs, 2 landed, 3 blocked by the shared npc.immune[owner],
			// 4 blocked by the projectile's own local immunity, 5 blocked by static (per-type) immunity, 6 not landed for another reason, 7 collateral (the shot had already spent its hit on another part),
			// 8 landed although the shared gate predicted a block (model violation), 9 contact calls of single-hit shots (maxPenetrate 1: never gated by the shared immunity), 10 landed damage, 11 blocked while the immunity had been set by ANOTHER levy's hit (cross-levy)
			public readonly long[,] C = new long[NC, NK];
			public long ImmSum, ImmN, StatImmSum, StatImmN; public readonly Dictionary<int, int> SetterShared = new(); public readonly Dictionary<long, int> SetterStatic = new(); public long DetourTicks, AltTicks, Calls, ProjDetourCalls; public int GameTicks;
			public readonly Dictionary<int, char> ClsOf = new(); public readonly Dictionary<int, int> Idx = new();
			public int[] Parts = Array.Empty<int>(); public uint PartsStamp = uint.MaxValue;
			public readonly HashSet<int> Landed = new(); public readonly Dictionary<long, int> PerLevy = new(); // (npc << 8 | levy) -> expiry tick
			public readonly Dictionary<string, long[]> ByType = new();
		}

		private static int BbClass(char c) => c switch { 'R' => 0, 'M' => 1, 'E' => 2, 'S' => 3, _ => 4 };

		private static void BbInstall() { On_Projectile.Damage += BbDamageDetour; }
		private static void BbRemove() { try { On_Projectile.Damage -= BbDamageDetour; } catch { } }

		private static void BbRefreshParts(CrState st, BbStats bb)
		{
			if (bb.PartsStamp == Main.GameUpdateCount) return;
			bb.PartsStamp = Main.GameUpdateCount;
			var l = new List<int>();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && (n.type == st.BossType || st.PartTypes.Contains(n.type))) l.Add(i); }
			bb.Parts = l.ToArray();
		}

		private static void BbDamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{
			var st = _cr; var bb = _bb;
			if (st == null || bb == null || !self.active || !self.friendly || self.hostile || self.damage <= 0) { orig(self); return; }
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			int soldier = -1;
			if (self.TryGetGlobalProjectile(out SoldierProjectileTag tag)) soldier = tag.SoldierIndex;
			if (soldier < 0 && self.type == ModContent.ProjectileType<SoldierMinion>()) soldier = (int)self.ai[0];
			int cls = 4;
			if (soldier >= 0) { if (!bb.ClsOf.TryGetValue(soldier, out char cc)) { var lv = st.Levies.FirstOrDefault(x => x.Idx == soldier); cc = lv != null ? lv.Cls : '?'; bb.ClsOf[soldier] = cc; } cls = BbClass(cc); }
			else if (!(tag != null && tag.CommanderIndex >= 0)) { orig(self); return; } // not an army shot at all
			BbRefreshParts(st, bb);
			Rectangle hb = self.Hitbox;
			List<NPC> cand = null;
			foreach (int i in bb.Parts)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.dontTakeDamage || n.life <= 0 || !hb.Intersects(n.Hitbox)) continue;
				bool col; try { col = self.Colliding(hb, n.Hitbox); } catch { col = true; }
				if (!col) continue;
				(cand ??= new List<NPC>()).Add(n);
			}
			if (cand == null) { orig(self); bb.DetourTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; bb.ProjDetourCalls++; return; }
			bb.Calls++;
			int owner = self.owner; if (owner < 0 || owner >= 256) owner = 255;
			bool wasShared = !self.usesLocalNPCImmunity && !self.usesIDStaticNPCImmunity && self.maxPenetrate != 1, wasStatic = self.usesIDStaticNPCImmunity && !self.usesLocalNPCImmunity;
			long a0 = System.Diagnostics.Stopwatch.GetTimestamp();
			string mode = BbImmMode; int nc = cand.Count;
			int[] savedImm = null; uint[] savedStat = null; uint[] statArr = null;
			// ---- the alternative under test (shared = the product: nothing is touched)
			if (mode == "local" && (wasShared || wasStatic)) { int cd = wasStatic ? Math.Max(1, self.idStaticNPCHitCooldown) : 10; self.usesIDStaticNPCImmunity = false; self.usesLocalNPCImmunity = true; self.localNPCHitCooldown = cd; }
			else if (mode == "static" && wasShared) { self.usesIDStaticNPCImmunity = true; self.idStaticNPCHitCooldown = 10; }
			bool perLevyShared = mode == "perlevy" && wasShared && soldier >= 0, perLevyStatic = mode == "perlevy" && wasStatic && soldier >= 0;
			if (perLevyShared)
			{
				savedImm = new int[nc];
				for (int k = 0; k < nc; k++) { NPC n = cand[k]; savedImm[k] = n.immune[owner]; bb.PerLevy.TryGetValue(n.whoAmI * 1024L + soldier, out int exp); n.immune[owner] = Math.Max(0, exp - (int)Main.GameUpdateCount); }
			}
			else if (perLevyStatic)
			{
				try
				{
					statArr = Projectile.perIDStaticNPCImmunity[self.type]; savedStat = new uint[nc];
					for (int k = 0; k < nc; k++) { NPC n = cand[k]; savedStat[k] = statArr[n.whoAmI]; bb.PerLevy.TryGetValue(((long)self.type * 1024L + n.whoAmI) * 1024L + soldier, out int exp); statArr[n.whoAmI] = (uint)Math.Max(0, exp); }
				}
				catch { statArr = null; }
			}
			long a1 = System.Diagnostics.Stopwatch.GetTimestamp(); bb.AltTicks += a1 - a0;
			// ---- the gate as vanilla sees it right now (after any substitution): 1 blocked by npc.immune[owner], 2 by the shot's own local immunity, 3 by static per-type immunity, 0 passes
			byte[] verdict = new byte[nc];
			for (int k = 0; k < nc; k++)
			{
				NPC n = cand[k];
				if (self.usesLocalNPCImmunity) { if (self.localNPCImmunity[n.whoAmI] != 0) verdict[k] = 2; }
				else if (self.usesIDStaticNPCImmunity) { try { if (Projectile.perIDStaticNPCImmunity[self.type][n.whoAmI] > Main.GameUpdateCount) verdict[k] = 3; } catch { } }
				else if (self.maxPenetrate != 1 && owner >= 0 && n.immune[owner] > 0) verdict[k] = 1;
			}
			bb.Landed.Clear();
			orig(self);
			long a2 = System.Diagnostics.Stopwatch.GetTimestamp();
			string tn = self.ModProjectile != null ? self.ModProjectile.Mod.Name + "/" + self.ModProjectile.Name : (ProjectileID.Search.TryGetName(self.type, out string vn) ? vn : "v" + self.type);
			if (!bb.ByType.TryGetValue(tn + "|" + "RMESO"[cls], out long[] bt)) bb.ByType[tn + "|" + "RMESO"[cls]] = bt = new long[BbStats.NK];
			bool singleHit = self.maxPenetrate == 1 && !self.usesLocalNPCImmunity && !self.usesIDStaticNPCImmunity;
			bb.C[cls, 0]++; bt[0]++; if (singleHit) { bb.C[cls, 9]++; bt[9]++; }
			bool spent = false;
			void Add(int k, long v = 1) { bb.C[cls, k] += v; bt[k] += v; }
			bool stat = self.usesIDStaticNPCImmunity && !self.usesLocalNPCImmunity, shr = !self.usesLocalNPCImmunity && !self.usesIDStaticNPCImmunity && self.maxPenetrate != 1;
			for (int k = 0; k < nc; k++)
			{
				NPC n = cand[k]; bool landed = bb.Landed.Contains(n.whoAmI);
				if (!landed && ((singleHit && spent) || (!self.active && verdict[k] == 0))) { Add(7); continue; }
				Add(1);
				long sk = stat ? ((long)self.type * 1024L + n.whoAmI) : n.whoAmI;
				if (landed)
				{
					Add(2); if (singleHit) spent = true;
					if (verdict[k] == 1) Add(8);
					if (shr) { bb.ImmSum += n.immune[owner]; bb.ImmN++; }
					if (stat) { try { bb.StatImmSum += Projectile.perIDStaticNPCImmunity[self.type][n.whoAmI] - Main.GameUpdateCount; bb.StatImmN++; } catch { } }
					if (perLevyShared) bb.PerLevy[n.whoAmI * 1024L + soldier] = (int)Main.GameUpdateCount + n.immune[owner];
					else if (perLevyStatic && statArr != null) bb.PerLevy[((long)self.type * 1024L + n.whoAmI) * 1024L + soldier] = (int)statArr[n.whoAmI];
					if (shr) bb.SetterShared[n.whoAmI] = soldier; else if (stat) bb.SetterStatic[sk] = soldier;
				}
				else if (verdict[k] == 1) { Add(3); if (bb.SetterShared.TryGetValue(n.whoAmI, out int ss) && ss != soldier) Add(11); }
				else if (verdict[k] == 2) Add(4);
				else if (verdict[k] == 3) { Add(5); if (bb.SetterStatic.TryGetValue(sk, out int st2) && st2 != soldier) Add(11); }
				else Add(6);
			}
			long r0 = System.Diagnostics.Stopwatch.GetTimestamp();
			if (perLevyShared) for (int k = 0; k < nc; k++) cand[k].immune[owner] = savedImm[k];
			else if (perLevyStatic && statArr != null) for (int k = 0; k < nc; k++) statArr[cand[k].whoAmI] = savedStat[k];
			long a3 = System.Diagnostics.Stopwatch.GetTimestamp();
			if (perLevyShared || perLevyStatic) bb.AltTicks += a3 - r0;
			bb.DetourTicks += (a3 - t0);
			bb.ProjDetourCalls++;
		}

		/// <summary>Records the hits that actually landed on boss parts (damage per class), and which NPCs were hit during the current detour call.</summary>
		public class BbProjWatch : GlobalProjectile
		{
			public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
			{
				var bb = _bb; var st = _cr; if (bb == null || st == null) return;
				if (!(target.type == st.BossType || st.PartTypes.Contains(target.type))) return;
				bb.Landed.Add(target.whoAmI);
				int soldier = -1;
				if (projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag)) soldier = tag.SoldierIndex;
				if (soldier < 0 && projectile.type == ModContent.ProjectileType<SoldierMinion>()) soldier = (int)projectile.ai[0];
				int cls = 4; if (soldier >= 0 && bb.ClsOf.TryGetValue(soldier, out char cc)) cls = BbClass(cc);
				{ bb.C[cls, 10] += damageDone; }
			}
		}

		private static string BbHeader() => "key\ttag\tmeta\tboss\tmode\timm\tseed\tresult\tmeleeHits\tmeleeDmg\tprojDmg\tdetourUsPerTick\taltUsPerTick\tcalls\tgameTicks\tavgImmValue\tavgStaticValue\t"
			+ string.Join("\t", new[] { "R", "M", "E", "S", "O" }.SelectMany(c => new[] { "calls", "pairs", "landed", "blkShared", "blkLocal", "blkStatic", "other", "collateral", "viol", "singleCalls", "dmg", "crossBlocked" }.Select(k => c + "_" + k)));

		private void BbFinish(CrRun run, CrState st, int endTick)
		{
			var bb = _bb; if (bb == null) return;
			try
			{
				string path = _crOut + ".bat.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, BbHeader() + "\n");
				double usPerTickDetour = bb.DetourTicks * 1e6 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, endTick);
				double usPerTickAlt = bb.AltTicks * 1e6 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, endTick);
				var f = new List<object> { run.Key, run.Tag, run.Meta, run.Boss, run.Mode, BbImmMode, run.Seed, st.Result, st.MeleeHits, st.MeleeDmg, st.ProjDmg, usPerTickDetour.ToString("0.0", CultureInfo.InvariantCulture), usPerTickAlt.ToString("0.0", CultureInfo.InvariantCulture), bb.Calls, endTick, bb.ImmN > 0 ? (bb.ImmSum / (double)bb.ImmN).ToString("0.0", CultureInfo.InvariantCulture) : "-", bb.StatImmN > 0 ? (bb.StatImmSum / (double)bb.StatImmN).ToString("0.0", CultureInfo.InvariantCulture) : "-" };
				for (int c = 0; c < BbStats.NC; c++) for (int k = 0; k < BbStats.NK; k++) f.Add(bb.C[c, k]);
				File.AppendAllText(path, string.Join("\t", f) + "\n");
				string tp = _crOut + ".bat.types.tsv";
				if (!File.Exists(tp)) File.WriteAllText(tp, "key\ttag\tmeta\tboss\tmode\timm\tseed\tprojType\tcls\tcalls\tpairs\tlanded\tblkShared\tblkLocal\tblkStatic\tother\tcollateral\tviol\tsingleCalls\tdmg\tcrossBlocked\n");
				File.AppendAllText(tp, string.Concat(bb.ByType.OrderByDescending(kv => kv.Value[10]).Select(kv => { var pt = kv.Key.Split('|'); return string.Join("\t", new object[] { run.Key, run.Tag, run.Meta, run.Boss, run.Mode, BbImmMode, run.Seed, pt[0], pt[1] }.Concat(kv.Value.Cast<object>())) + "\n"; })));
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-BB: finish row failed: " + e.Message); }
		}

		private void BbStartFight(CrRun run) { _bb = new BbStats(); BbImmMode = string.IsNullOrEmpty(run.Imm) ? "shared" : run.Imm; }
		private void BbEndFight() { _bb = null; BbImmMode = "shared"; }
	}
}
