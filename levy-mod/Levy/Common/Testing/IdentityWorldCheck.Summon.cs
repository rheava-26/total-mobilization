using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- SUMMON LOOP: does a levy keep re-casting a staff it has already filled?
		// One levy carries one minion / sentry staff. After a warm-up that fills the flock, it is watched for an IDLE window (no enemy) and a COMBAT window (a pinned enemy 12 tiles away): casts of the staff
		// (SoldierMinions.CastTally / HelperTally / SentryTally), the minions alive at the end and the most alive at once. A player fills her slots and stops, so a levy at its cap casts at most a small allowance
		// (a minion that really died) over a window; more is a loop (a minion that cannot live for its owner, a count that never reaches the cap).
		// Env: LEVY_SUMMON=1 sweeps every minion / sentry staff of LEVY_SUMMON_MODS (default Terraria,CalamityMod), =0 skips; unset runs a small sample. LEVY_SUMMON_TICKS = window length (default 1200 swept, 3600 sample).
		// LEVY_SUMMON_ONLY=Name;Name, LEVY_SUMMON_SHARD=i/n.

		private const int SummonAllowance = 3;
		/// <summary>Staves left out: their tower's own AI loads a texture when it fires, which a dedicated test server cannot, so the tower ends on its first shot for a real player in this world as well (measured: one player, one levy alike).</summary>
		private static readonly HashSet<string> HeadlessSentries = new() { "PulseTurretRemote" };

		private sealed class SummonResult
		{
			public string Label, Family = ""; public bool Transient; public int UseTime = 1, Window; public int IdleCasts, IdleHelpers, IdleSentry, CombatCasts, CombatHelpers, CombatSentry, WarmCasts, WarmSentry, IdleAlive, CombatAlive, MaxAlive, Births;
			public int IdleTotal => IdleCasts + IdleHelpers + IdleSentry; public int CombatTotal => CombatCasts + CombatHelpers + CombatSentry;
			/// <summary>A staff that is re-cast at its cap: idle (anything past the allowance), or in combat past the allowance - a self-ending cast (a player holding the button) past one cast per use time.</summary>
			public bool Loops => IdleTotal > SummonAllowance || (Transient ? CombatCasts > Window / Math.Max(1, UseTime) + 1 : CombatTotal > SummonAllowance);
			public override string ToString() => $"{Label} [{Family}] warm-up casts {WarmCasts}/sentry {WarmSentry}; idle: casts {IdleCasts} helpers {IdleHelpers} sentry {IdleSentry}, alive {IdleAlive}; combat: casts {CombatCasts} helpers {CombatHelpers} sentry {CombatSentry}, alive {CombatAlive}; most alive {MaxAlive}, minion births {Births}";
		}

		private static int LevyFlock(int body, out int sentries)
		{
			int stand = ModContent.ProjectileType<SoldierMinion>(), n = 0; sentries = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i]; if (!p.active) continue;
				bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == body;
				if (p.type == stand && (int)p.ai[0] == body) n++;
				else if (tagged && p.sentry) sentries++;
				else if (tagged && p.minion) n++;
			}
			return n;
		}

		private static int _killLog;
		private static void KillSpy(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (_killLog < 6 && (self.sentry || self.minion) && self.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex >= 0)
			{
				_killLog++;
				Note($"  dbg KILL {self.ModProjectile?.Name} tl={self.timeLeft} :: " + string.Join(" < ", new System.Diagnostics.StackTrace(1, false).GetFrames().Take(14).Select(f => f.GetMethod()?.DeclaringType?.Name + "." + f.GetMethod()?.Name)));
			}
			orig(self);
		}

		private static SummonResult SummonOne(int weaponType, string label, int window)
		{
			var res = new SummonResult { Label = label };
			ClearSoldiers(); ReleaseCommanderButtons(); _cmdHolds = false;
			_cmdAt = OriginCmdAt();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at); _sweepTarget.active = false; // idle first
			Item w = new Item(); w.SetDefaults(weaponType);
			try { WeaponProfile fp = WeaponClassifier.GetProfile(w); res.Family = fp.Family.ToString(); } catch (Exception) { }
			res.Transient = SoldierNPC.TransientStaff(w); res.UseTime = w.useTime; res.Window = window; if (res.Transient) res.Family += "/self-ending";
			SoldierRecord rec = MakeOriginRecord(w, 0);
			if (OriginDebug) { _killLog = 0; On_Projectile.Kill += KillSpy; }
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
			Main.npc[body].Bottom = at;
			Main.rand = new Terraria.Utilities.UnifiedRandom(31337);
			KeepCommander();
			// warm-up: a pinned enemy in reach (the staff is kept up in combat), until the flock stops growing
			SpawnOriginTarget(at);
			int c0 = SoldierMinions.CastTally, s0 = SoldierMinions.SentryTally;
			for (int k = 0; k < 1500; k++)
			{
				rec.Mana = rec.MaxMana; OriginStep(); ClearNaturalHostiles();
				if (OriginDebug && k % 10 == 0 && k < 200) for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && (p.sentry || (p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == body))) Note($"  dbg tagged={(p.TryGetGlobalProjectile(out SoldierProjectileTag t2) ? t2.SoldierIndex : -9)} t={k} {p.ModProjectile?.Name ?? p.type.ToString()} sentry={p.sentry} minion={p.minion} tl={p.timeLeft} pos=({(p.Center.X - at.X) / 16f:0.0},{(p.Center.Y - at.Y) / 16f:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) owner={p.owner}"); }
			}
			res.WarmCasts = SoldierMinions.CastTally - c0; res.WarmSentry = SoldierMinions.SentryTally - s0;
			// the combat window first (the flock is at its cap with an enemy in reach), then the idle window (the enemy gone)
			int cc = SoldierMinions.CastTally, ch = SoldierMinions.HelperTally, cs = SoldierMinions.SentryTally; var seen = new HashSet<int>();
			for (int k = 0; k < window; k++)
			{
				rec.Mana = rec.MaxMana; OriginStep(); ClearNaturalHostiles();
				int a = LevyFlock(body, out _); res.MaxAlive = Math.Max(res.MaxAlive, a);
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.minion && !p.sentry && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == body) { int key = i * 100003 + p.identity; if (seen.Add(key)) res.Births++; } }
			}
			res.CombatCasts = SoldierMinions.CastTally - cc; res.CombatHelpers = SoldierMinions.HelperTally - ch; res.CombatSentry = SoldierMinions.SentryTally - cs; res.CombatAlive = LevyFlock(body, out int sent1) + sent1 * 0;
			if (res.Family == "Sentry") res.CombatAlive = sent1;
			_sweepTarget.active = false; _sweepTarget = null;
			// idle: let a few ticks pass so the levy settles out of the fight, then count
			for (int k = 0; k < 240; k++) { rec.Mana = rec.MaxMana; OriginStep(); }
			cc = SoldierMinions.CastTally; ch = SoldierMinions.HelperTally; cs = SoldierMinions.SentryTally;
			for (int k = 0; k < window; k++) { rec.Mana = rec.MaxMana; OriginStep(); ClearNaturalHostiles(); res.MaxAlive = Math.Max(res.MaxAlive, LevyFlock(body, out _)); }
			res.IdleCasts = SoldierMinions.CastTally - cc; res.IdleHelpers = SoldierMinions.HelperTally - ch; res.IdleSentry = SoldierMinions.SentryTally - cs; res.IdleAlive = LevyFlock(body, out int sent2); if (res.Family == "Sentry") res.IdleAlive = sent2;
			if (OriginDebug) On_Projectile.Kill -= KillSpy;
			ClearSoldiers();
			return res;
		}

		private static void SummonLoopSweep()
		{
			string mode = Environment.GetEnvironmentVariable("LEVY_SUMMON");
			if (mode == "0") return;
			bool full = mode == "1" || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_SUMMON_ONLY"));
			int window = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMON_TICKS"), out int tk) ? tk : (full ? 1200 : 3600);
			var staves = new List<(int type, string label)>();
			string[] mods = (Environment.GetEnvironmentVariable("LEVY_SUMMON_MODS") ?? "Terraria,CalamityMod").Split(',', StringSplitOptions.RemoveEmptyEntries);
			string only = Environment.GetEnvironmentVariable("LEVY_SUMMON_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Replace('_', ' ').Split(';', StringSplitOptions.RemoveEmptyEntries);
			var probe = new SoldierGear();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.damage <= 0 || s.accessory || s.shoot <= 0) continue;
				if (!(s.CountsAsClass(DamageClass.Summon) || s.sentry)) continue;
				string mod = s.ModItem?.Mod.Name ?? "Terraria";
				string nm = s.ModItem?.Name ?? s.Name;
				if (names != null ? !names.Any(n => s.Name.Replace(" ", "") == n.Replace(" ", "") || nm == n) : (!mods.Contains(mod) || (!full && !SummonSample.Contains(nm)))) continue;
				if (HeadlessSentries.Contains(nm)) continue;
				Item it = new Item(); it.SetDefaults(i);
				if (!probe.CanHold(SoldierGear.SlotWeapon, it)) continue;
				WeaponFamily f = WeaponClassifier.GetProfile(it).Family;
				if (f != WeaponFamily.Summon && f != WeaponFamily.Sentry) continue;
				staves.Add((i, mod + ":" + nm));
			}
			string shard = Environment.GetEnvironmentVariable("LEVY_SUMMON_SHARD");
			if (!string.IsNullOrEmpty(shard) && shard.Split('/') is { Length: 2 } sh && int.TryParse(sh[0], out int si) && int.TryParse(sh[1], out int sn) && sn > 1) staves = staves.Where((w, k) => k % sn == si).ToList();
			if (staves.Count == 0) return;
			SetupOriginArena();
			var all = new List<SummonResult>(); int threw = 0; var sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				KeepCommander(); Step(2);
				foreach (var (type, label) in staves)
				{
					try { SummonResult r = SummonOne(type, label, window); all.Add(r); if (full) Note("summon loop: " + r + (r.Loops ? " LOOPS" : "")); }
					catch (Exception e) { threw++; Note($"summon loop: {label} threw {e.GetType().Name}: {e.Message}"); }
				}
			}
			finally { _sweepTarget = null; }
			var loops = all.Where(r => r.Loops).ToList();
			Note($"summon loop: {all.Count} staves run in {sw.ElapsedMilliseconds / 1000} s (warm-up 1500 ticks, then {window} ticks in combat and {window} idle), {loops.Count} cast more than {SummonAllowance} times in a window, {threw} threw");
			Note("summon loop, offenders: " + (loops.Count == 0 ? "none" : string.Join("; ", loops.Select(r => r.ToString()))));
			if (!full) foreach (SummonResult r in all) Note("summon loop: " + r);
			Check($"summon loop: none of {all.Count} staves is re-cast more than {SummonAllowance} times in {window} ticks at its cap, idle or in combat (offenders {loops.Count})", all.Count > 0 && loops.Count == 0 && threw == 0);
		}

		private static readonly HashSet<string> SummonSample = new() { "Slime Staff", "SlimeStaff", "HornetStaff", "OpticStaff", "StardustDragonStaff", "DeadlySphereStaff", "PygmyStaff", "RavenStaff", "XenoStaff", "DreadmineStaff", "Hornet Staff", "Optic Staff", "Raven Staff", "Pygmy Staff", "Xeno Staff", "Deadly Sphere Staff", "Stardust Dragon Staff", "Imp Staff", "Frost Hydra Staff", "Staff of the Frost Hydra" };
	}
}
