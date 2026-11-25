using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Owner;
using Levy.Common.Recording;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- SUMMON A/B: the same staff in a player's hands and in a levy's, the same fight, side by side
		// The designer's method (2026-11-25): put a PLAYER with a staff in the fight recorder, put a LEVY with the same staff in it, and compare; they must look the same or extremely similar.
		// Both runs are SumRun (same arena, same kit of 6 minion slots, same four pinned enemies 12 tiles off, the same tick count); the stand-in commander uses the staff herself (a player's click, her cursor on the enemy), the
		// levy holds his ground with the commander 64 tiles away. Halfway through, the enemy line is mirrored to the other side of the owner (the retarget test): a minion that idles near its owner or only ever fights the enemy it
		// met first shows there. Measured per tick for every minion the owner has: the distance to the owner and to the nearest enemy, whether it moves, and whether it is ENGAGED (nearer to the enemy than half the owner's own
		// distance, or within 6 tiles of it); per hit the damage and whether a minion dealt it (the FightRecBossHooks sink, the same hook for both sides, so the number does not depend on who owns the projectile). Each run can be
		// recorded with the fight recorder (the enemies are traced as targets, format 14 adds the levy's minions as frame key m) so a viewer shows both side by side.
		// WHAT THE STAND-IN PLAYER DOES (checked, not assumed): Main.myPlayer = 0 while NPCs and projectiles update (the single-player owner), netMode SinglePlayer in those two phases (LEVY_SUMMONAB_SPNET=0 keeps Server), her own
		// ItemCheck once a tick. The player's rows carry "dmg" and "hits": a player run with 0 hits is flagged PLAYER0 (the harness did not hurt anything, so the row says nothing) and the ship check fails on it.
		// Env: LEVY_SUMMONAB=1 (ship sample) | all (the whole catalogue of the loaded mods), LEVY_SUMMONAB_ALONE=1 runs only this, LEVY_SUMMONAB_ONLY=Name;Name, LEVY_SUMMONAB_TICKS=N (default 720), LEVY_SUMMONAB_TILES=N (enemy
		// distance, default 12), LEVY_SUMMONAB_ADDS=N (more pinned enemies in a row behind the main one, default 0: a boss-like single target; 3 is the old line, where a piercing shot hits several), LEVY_SUMMONAB_OUT=DIR (writes summonab.jsonl and, with LEVY_SUMMONAB_REC=1 (default when OUT is set), one .levyrec per run), LEVY_SUMMONAB_SPNET=1 (netMode SinglePlayer while NPCs and projectiles update: vanilla minion code then throws on a dedicated server, so the default is Server), LEVY_SUMMONAB_FOCUS=1 (the player right-clicks her enemy:
		// MinionAttackTargetNPC set, as a player who focuses does), LEVY_SUMMONAB_SLOTS=N (default 6), LEVY_SUMMONAB_CAL=0 skips the Calamity items.

		private static int _abPass, _abFail; private static readonly bool AbDebug = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_DEBUG") == "1";
		private static void ABCheck(string name, bool ok) { if (ok) _abPass++; else _abFail++; _mod.Logger.Info($"[SummonAB] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void ABNote(string s) => _mod.Logger.Info("[SummonAB] OBSERVED " + s);

		/// <summary>What one run measured, per phase (0: the enemies on the right, 1: after the mirror).</summary>
		private sealed class AbPhase
		{
			public int Ticks; public long MinionTicks, Engaged, NearOwner, Moving, Overlap; public double SumDOwner, SumDTarget;
			public int Hits, MinionHits; public long Dmg, MinionDmg, ItemDmg; public int FirstHit = -1;
			public float EngagedShare => MinionTicks > 0 ? (float)Engaged / MinionTicks : 0f;
			public float NearShare => MinionTicks > 0 ? (float)NearOwner / MinionTicks : 0f;
			public float MovingShare => MinionTicks > 0 ? (float)Moving / MinionTicks : 0f;
			public float OverlapShare => MinionTicks > 0 ? (float)Overlap / MinionTicks : 0f;
			public float AvgDOwner => MinionTicks > 0 ? (float)(SumDOwner / MinionTicks / 16.0) : 0f;
			public float AvgDTarget => MinionTicks > 0 ? (float)(SumDTarget / MinionTicks / 16.0) : 0f;
			public float Dps => Dmg * 60f / Math.Max(1, Ticks);
			public float AvgMinions => Ticks > 0 ? (float)MinionTicks / Ticks : 0f;
		}

		private sealed class AbResult
		{
			public AbCtx Ctx; public string Label, Who; public readonly Dictionary<int, int> HitsByBody = new(); public int ImmTicks, ImmMax; public AbPhase[] P = { new AbPhase(), new AbPhase() }; public SumStat Stat; public string Rec = "";
			public long Dmg => P[0].Dmg + P[1].Dmg; public int Hits => P[0].Hits + P[1].Hits; public int MinionHits => P[0].MinionHits + P[1].MinionHits;
			public long MinionTicks => P[0].MinionTicks + P[1].MinionTicks; public int Ticks => P[0].Ticks + P[1].Ticks;
			public float Dps => Dmg * 60f / Math.Max(1, Ticks);
			public float Engaged => MinionTicks > 0 ? (float)(P[0].Engaged + P[1].Engaged) / MinionTicks : 0f;
			public float Near => MinionTicks > 0 ? (float)(P[0].NearOwner + P[1].NearOwner) / MinionTicks : 0f;
			public float Moving => MinionTicks > 0 ? (float)(P[0].Moving + P[1].Moving) / MinionTicks : 0f;
			public float Overlap => MinionTicks > 0 ? (float)(P[0].Overlap + P[1].Overlap) / MinionTicks : 0f;
			public float DOwner => MinionTicks > 0 ? (float)((P[0].SumDOwner + P[1].SumDOwner) / MinionTicks / 16.0) : 0f;
			public float DTarget => MinionTicks > 0 ? (float)((P[0].SumDTarget + P[1].SumDTarget) / MinionTicks / 16.0) : 0f;
			public float AvgMinions => Ticks > 0 ? (float)MinionTicks / Ticks : 0f;
		}

		/// <summary>The observer a run of <see cref="SumRun"/> calls when set: counts every tick, mirrors the enemy line at the half, and starts / stops the recording.</summary>
		private sealed class AbCtx
		{
			public AbResult R; public int Ticks, Tiles; public bool Record, Focus, Sentry, FollowMode; public string StaffName = ""; public int Levies = 1; public readonly HashSet<int> Bodies = new(); public int Adds = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMONAB_ADDS"), out int ad) ? ad : 0; public string RecDir, RecName; public bool Commander; public int Body = -1; public Vector2 At; public bool Mirrored;
			public int MirrorTick => Ticks / 2;
			/// <summary>The bench check (see <see cref="AbBenchCheck"/>): at tick 60 the first levy's first real minion of the staff throws inside its update; what was seen right after, in the middle of the bench and at the end.</summary>
			public bool BenchTest; public int BenchShoot; public readonly int[] BenchSeen = new int[8]; public bool BenchedFirst, BenchedSecond, BenchWide, CanDriveSecond;
			private void BenchPre(int k)
			{
				if (!BenchTest || Commander || Bodies.Count < 2) return;
				int a = Bodies.Min(), b = Bodies.Max();
				if (k == 60) { SoldierMinions.LabBenchTicks = 240; SoldierMinions.LabThrows = 0; SoldierMinions.LabThrowType = BenchShoot; SoldierMinions.LabThrowSoldier = a; }
				if (k == 66) { BenchedFirst = SoldierMinions.IsBenched(BenchShoot, a, Main.GameUpdateCount); BenchedSecond = SoldierMinions.IsBenched(BenchShoot, b, Main.GameUpdateCount); BenchWide = SoldierMinions.BenchedForAll(BenchShoot); CanDriveSecond = SoldierMinions.CanDrive(BenchShoot, b); }
				if (k == 200 || k == Ticks - 1) { int o = k == 200 ? 0 : 4; BenchSeen[o] = AbRealOf(a); BenchSeen[o + 1] = AbRealOf(b); BenchSeen[o + 2] = AbStandInsOf(a); BenchSeen[o + 3] = AbStandInsOf(b); }
			}
			private bool _recOn; private int _cur => Mirrored ? 1 : 0; private int _sinceMirror;

			public void Begin(bool commander, int body, Vector2 at)
			{
				Commander = commander; Body = body; At = at; Mirrored = false; _sinceMirror = 0; if (body >= 0) Bodies.Add(body);
				FightRecBossHooks.AbSink = Sink;
				if (Record && !string.IsNullOrEmpty(RecDir))
				{
					try
					{
						Directory.CreateDirectory(RecDir);
						FightRecorder.OutDirOverride = RecDir; FightRecorder.GzipOverride = true; FightRecorder.ForceSync = true; FightRecorder.IgnoreInactive = true; FightRecorder.SummonTrace = true; FightRecPost.TestForce = false;
						FightRecorder.TraceTargets.Clear();
						if (_sweepTarget != null) FightRecorder.TraceTargets.Add(_sweepTarget.whoAmI);
						foreach (NPC a in _rogueAdds) if (a != null && a.active) FightRecorder.TraceTargets.Add(a.whoAmI);
						var meta = new Dictionary<string, string> { ["label"] = R.Label, ["who"] = R.Who, ["bossName"] = StaffName, ["mode"] = R.Who, ["arena"] = FollowMode ? "A/B, commander walks, levy follows" : Adds > 0 ? "A/B, line of enemies" : "A/B, one enemy", ["gear"] = "n" + (Commander ? 0 : Levies), ["levies"] = (Commander ? 0 : Levies).ToString() };
						string path = FightRecorder.Start(Main.player[0], StaffName, out string err, meta, RecName);
						if (path == null) ABNote($"{R.Label} [{R.Who}]: the recorder did not start ({err})"); else { _recOn = true; R.Rec = path; }
					}
					catch (Exception e) { ABNote($"{R.Label} [{R.Who}]: recorder threw {e.GetType().Name}: {e.Message}"); }
				}
			}

			/// <summary>Before tick k: at the half, the enemy line moves to the mirror image on the other side of the owner.</summary>
			public void Pre(int k)
			{
				BenchPre(k);
				if (FollowMode)
				{
					// the commander walks back and forth (amplitude AbFollowAmp tiles, period AbFollowPeriod ticks) in front of the enemies; the player carries the staff along that path, the levy follows the commander along it
					float dx = (AbFollowCentre + AbFollowAmp * (float)Math.Sin(2 * Math.PI * k / AbFollowPeriod)) * 16f;
					_cmdAt = new Vector2(At.X + dx, At.Y - 24f);
				}
				// the player's own minion target: a client resets it itself (only the local player's update does); the stand-in's update runs with Main.myPlayer = 255, so it is put to "none" here, or to her enemy when she right-clicks it
				if (Commander) Main.player[0].MinionAttackTargetNPC = Focus && _sweepTarget != null && _sweepTarget.active ? _sweepTarget.whoAmI : -1;
				if (k != MirrorTick || Mirrored) return;
				Mirrored = true;
				float cx = At.X + (FollowMode ? AbFollowCentre * 16f : 0f);
				if (_sweepTarget != null && _sweepTarget.active)
				{
					_sweepTargetAt = new Vector2(2 * cx - _sweepTargetAt.X, _sweepTargetAt.Y);
					_sweepTarget.Bottom = _sweepTargetAt; var ht = _sweepTarget.GetGlobalNPC<HarnessTarget>(); ht.Pin = _sweepTarget.position;
				}
				for (int i = 0; i < _rogueAdds.Count; i++)
				{
					NPC n = _rogueAdds[i]; if (n == null || !n.active) continue;
					_rogueAddsAt[i] = new Vector2(2 * cx - _rogueAddsAt[i].X, _rogueAddsAt[i].Y); n.Bottom = _rogueAddsAt[i];
				}
			}

			private bool IsMine(Projectile p)
			{
				if (p == null || !p.active || p.hostile) return false;
				bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0;
				return Commander ? p.owner == 0 && !tagged : tagged && Bodies.Contains(tag.SoldierIndex);
			}

			private void Sink(NPC npc, Player byPlayer, Projectile proj, Item item, int dmg)
			{
				if (dmg <= 0 || npc == null) return;
				if (npc != _sweepTarget && !_rogueAdds.Contains(npc)) return;
				AbPhase ph = R.P[_cur];
				bool mine, byMinion = false;
				if (proj != null)
				{
					mine = IsMine(proj);
					byMinion = proj.minion || proj.sentry || (proj.type > 0 && proj.type < ProjectileID.Sets.MinionShot.Length && ProjectileID.Sets.MinionShot[proj.type]);
				}
				else mine = byPlayer != null && (Commander ? byPlayer.whoAmI == 0 && !SoldierOwner.IsDummy(byPlayer) : SoldierOwner.IsDummy(byPlayer));
				if (!mine) return;
				if (!Commander && proj != null && proj.TryGetGlobalProjectile(out SoldierProjectileTag bt)) R.HitsByBody[bt.SoldierIndex] = R.HitsByBody.GetValueOrDefault(bt.SoldierIndex) + 1;
				if (AbDebug && !Commander && R.Hits < 14) ABNote($"  dbg hit #{R.Hits} t={(R.Ticks)} levy={(proj != null && proj.TryGetGlobalProjectile(out SoldierProjectileTag ht2) ? ht2.SoldierIndex : -1)} owner={proj?.owner} proj={proj?.ModProjectile?.Name ?? (proj != null ? ProjectileID.Search.GetName(proj.type) : "item")} immune255={npc.immune[255]} immune0={npc.immune[0]} pen={proj?.penetrate} local={proj?.usesLocalNPCImmunity} idstatic={proj?.usesIDStaticNPCImmunity}");
				ph.Hits++; ph.Dmg += dmg; if (proj == null) ph.ItemDmg += dmg;
				if (byMinion) { ph.MinionHits++; ph.MinionDmg += dmg; }
				if (ph.FirstHit < 0) ph.FirstHit = _cur == 1 ? _sinceMirror : ph.Ticks;
			}

			/// <summary>After tick k: the minions' distances to the owner and to the enemy line.</summary>
			public void Post(int k, SoldierNPC sn)
			{
				if (AbDebug && k == 0) { Player c0 = Main.player[0]; ABNote($"  dbg {R.Label} [{R.Who}] t=0 MouseWorld={Main.MouseWorld} mouse=({Main.mouseX},{Main.mouseY}) screen={Main.screenPosition} zoom={Main.GameViewMatrix?.Zoom} playerCenter={c0.Center} dir={c0.direction} item={c0.HeldItem?.Name} gravDir={c0.gravDir} mouseWorld255={(Main.player[255] == null ? "null" : Main.player[255].Center.ToString())}"); }
				if (AbDebug && k % 60 == 0 && !Commander) ABNote($"  dbg {R.Label} [levy] t={k} levyPos=({(sn != null ? sn.NPC.Center.X / 16f : -1):0.0},{(sn != null ? sn.NPC.Center.Y / 16f : -1):0.0}) vel=({(sn != null ? sn.NPC.velocity.X : 0):0.0},{(sn != null ? sn.NPC.velocity.Y : 0):0.0}) at=({At.X / 16f:0.0},{At.Y / 16f:0.0}) enemy=({(_sweepTarget != null ? _sweepTarget.Center.X / 16f : -1):0.0}) state={sn?.State}");
				AbPhase ph = R.P[_cur]; ph.Ticks++; if (Mirrored) _sinceMirror++;
				if (_sweepTarget != null && _sweepTarget.active) { int im = _sweepTarget.immune[Commander ? 0 : 255]; if (im > 0) R.ImmTicks++; R.ImmMax = Math.Max(R.ImmMax, im); }
				Player cmd = Main.player[0];
				Vector2 owner = Commander ? cmd.Center : (sn != null && sn.NPC.active ? sn.NPC.Center : At);
				float nearestOwnerToEnemy = float.MaxValue;
				var enemies = new List<Vector2>(4);
				if (_sweepTarget != null && _sweepTarget.active) enemies.Add(_sweepTarget.Center);
				foreach (NPC a in _rogueAdds) if (a != null && a.active) enemies.Add(a.Center);
				if (enemies.Count == 0) return;
				foreach (Vector2 e in enemies) nearestOwnerToEnemy = Math.Min(nearestOwnerToEnemy, Vector2.Distance(owner, e));
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || !p.minion || p.sentry || !IsMine(p)) continue;
					if (p.type == ProjectileID.FallingStarSpawner || p.type == ProjectileID.FallingStar) continue;
					if (AbDebug && (k % 30 == 0 || k < 8)) ABNote($"  dbg {R.Label} [{R.Who}] t={k} {p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type)}#{i} pos=({p.Center.X / 16f:0.0},{p.Center.Y / 16f:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) ai=({p.ai[0]:0.##},{p.ai[1]:0.##}) lai=({p.localAI[0]:0.##},{p.localAI[1]:0.##}) owner={p.owner} minionPos={p.minionPos} born={(HoldBirthProbe.At.TryGetValue(i, out var bb) ? bb.at.ToString() : "?")} friendly={p.friendly} levy={(p.TryGetGlobalProjectile(out SoldierProjectileTag dt) ? dt.SoldierIndex : -9)} levyTarget={(p.TryGetGlobalProjectile(out SoldierProjectileTag dt2) && dt2.SoldierIndex >= 0 && Main.npc[dt2.SoldierIndex].ModNPC is SoldierNPC dsn ? dsn.TargetIndex + "/atk" + dsn.Dummy?.MinionAttackTargetNPC : "-")} lni={(_sweepTarget != null ? p.localNPCImmunity[_sweepTarget.whoAmI] : -1)} overlap={(_sweepTarget != null && p.Hitbox.Intersects(_sweepTarget.Hitbox))} imm255={(_sweepTarget != null ? _sweepTarget.immune[255] : -1)} usesLocal={p.usesLocalNPCImmunity}/{p.localNPCHitCooldown} pen={p.penetrate} dmg={p.damage} ownerAt=({owner.X / 16f:0.0},{owner.Y / 16f:0.0}) ownerVel=({(Commander ? cmd.velocity.X : 0):0.0}) myPlayer={Main.myPlayer} net={Main.netMode}");
					float dOwner = Vector2.Distance(p.Center, owner), dT = float.MaxValue;
					foreach (Vector2 e in enemies) dT = Math.Min(dT, Vector2.Distance(p.Center, e));
					ph.MinionTicks++; ph.SumDOwner += dOwner; ph.SumDTarget += dT;
					if (dOwner < 6 * 16f) ph.NearOwner++;
					if (dT <= Math.Max(6 * 16f, 0.5f * nearestOwnerToEnemy)) ph.Engaged++;
					if (p.velocity.LengthSquared() > 0.25f) ph.Moving++;
					if (_sweepTarget != null && _sweepTarget.active && p.Hitbox.Intersects(_sweepTarget.Hitbox)) ph.Overlap++;
				}
			}

			public void End()
			{
				FightRecBossHooks.AbSink = null;
				if (_recOn)
				{
					try
					{
						string fin = FightRecorder.Stop("summon a/b over");
						if (!string.IsNullOrEmpty(fin) && File.Exists(fin))
						{
							string dst = Path.Combine(RecDir, RecName + ".levyrec"); if (File.Exists(dst)) File.Delete(dst);
							File.Move(fin, dst); R.Rec = dst; // the recorder names a file for its fight and result; the A/B names it for the staff and the owner
						}
					}
					catch (Exception e) { ABNote($"{R.Label} [{R.Who}]: stopping the recorder threw {e.GetType().Name}: {e.Message}"); }
					_recOn = false;
				}
				FightRecorder.OutDirOverride = null; FightRecorder.GzipOverride = null; FightRecorder.ForceSync = false; FightRecorder.IgnoreInactive = false; FightRecorder.SummonTrace = false; FightRecorder.TraceTargets.Clear();
			}
		}
		private static AbCtx _ab;
		/// <summary>Follow mode (LEVY_SUMMONAB_MODE=follow): the commander walks AbFollowAmp tiles either side of a point AbFollowCentre tiles left of the levy's spot with period AbFollowPeriod ticks, the enemies 12+ tiles right of that spot; the levy is on Follow.</summary>
		private const float AbFollowCentre = -8f, AbFollowAmp = 10f, AbFollowPeriod = 480f;
		/// <summary>Where the stand-in's cursor sits against her enemy's centre in a summon A/B: on the body, not on the exact centre (a minion the staff spawns AT the cursor, dead centre of the enemy it flies at, divides by a zero distance: a NaN Raven. A player's cursor is never there to the pixel).</summary>
		private static Vector2 _abAimOffset => _ab == null ? Vector2.Zero : _ab.Sentry && _sweepTarget != null ? _ab.At + new Vector2(5 * 16f, -2 * 16f) - _sweepTarget.Center + new Vector2(0f, 8f) : new Vector2(-22f, -30f);
		/// <summary>The stand-in's own Player.Update runs as the local player (see PreUpdatePlayers); LEVY_SUMMONAB_LOCALUPDATE=0 leaves it as the server runs it.</summary>
		private static bool _abLocalUpdate = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_LOCALUPDATE") != "0";

		/// <summary>One staff for one owner: the numbers of <see cref="AbCtx"/> plus SumRun's own stat.</summary>
		private static int AbRealOf(int body) { int n = 0; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.minion && SoldierMinions.OwnedBy(p, body)) n++; } return n; }
		private static int AbStandInsOf(int body) { int n = 0, t = ModContent.ProjectileType<global::Levy.Content.Projectiles.SoldierMinion>(); for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.type == t && (int)p.ai[0] == body) n++; } return n; }

		private static AbResult AbRun(int type, string label, bool commander, int ticks, int slots, int tiles, string recDir, bool focus, int levies = 1, bool bench = false)
		{
			var res = new AbResult { Label = label, Who = commander ? "player" : "levy" };
			string safe = new string(label.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
			_ab = new AbCtx { R = res, StaffName = SumItem(type).Name, Levies = levies, Sentry = WeaponClassifier.GetProfile(SumItem(type)).Family == WeaponFamily.Sentry, Ticks = ticks, Tiles = tiles, Record = !string.IsNullOrEmpty(recDir), Focus = focus, RecDir = recDir, FollowMode = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_MODE") == "follow", RecName = (Environment.GetEnvironmentVariable("LEVY_SUMMONAB_MODE") == "follow" ? "abf_" : levies > 1 ? "abarmy" + levies + "_" : "ab_") + res.Who + "_" + safe };
			if (bench) { _ab.BenchTest = true; _ab.BenchShoot = SumItem(type).shoot; }
			AbCtx ctx = _ab;
			try { res.Stat = SumRun(type, label, ticks, commander, slots); }
			finally { _ab = null; SoldierMinions.LabThrowSoldier = -1; SoldierMinions.LabBenchTicks = 0; }
			res.Ctx = ctx;
			return res;
		}

		private const float AbWeak = 0.6f, AbStrong = 1.6f, AbIdle = 0.6f;

		/// <summary>The verdict of one pair. PLAYER0: the stand-in player hurt nothing (the harness, not the levy, is what the row is about). NONE: neither makes a minion. HOVERS: the levy's minions are engaged much less than the player's and sit near him much more.</summary>
		private static string AbVerdict(AbResult pc, AbResult lv, bool sentry)
		{
			bool pMin = pc.MinionTicks > 0 || pc.Stat.MaxAlive > 0, lMin = lv.MinionTicks > 0 || lv.Stat.MaxAlive > 0;
			if (!pMin && !lMin && pc.Hits == 0 && lv.Hits == 0) return "NONE";
			if (pc.Hits == 0)
			{
				// a tower the stand-in places that still never fires: the DD2 tower items cannot be used outside the Old One's Army at all, and the few whose own AI needs more than a stand-in gives (observed 2026-11-25: the Caustic Croaker, the Atlas Munitions Beacon's pods,
				// the Cadaverous Carrion, the Spikecrag and the Pulse Turret do nothing for her, the last three also for the levy); neither says anything about the levy. (The Frost Hydra, Queen Spider, Houndius and the like never fired because she clicked on while each new tower
				// replaced the oldest before its wind-up was over: she now stops at her turret slots, see SumRun.)
				if (sentry) return pc.Stat.Uses == 0 ? "PLAYER-CANT-USE" : "TOWER-NOT-COMPARED";
				if (pc.Stat.Uses == 0 && pc.Stat.Casts == 0) return "PLAYER-CANT-USE";
				return lv.Hits > 0 ? "PLAYER0" : "BOTH0";
			}
			var fl = new List<string>();
			float ratio = pc.Dps > 0 ? lv.Dps / pc.Dps : 0f;
			if (lv.Hits == 0) fl.Add("NOHIT");
			else if (ratio < AbWeak) fl.Add("WEAK");
			else if (ratio > AbStrong)
			{
				// a staff that grows with every use (the Void Eater Marionette: 0.6 + 0.4 a slot on contact, 1.1 a slot on its fireballs): the stand-in's clicks do not all register (her item animation stalls under the harness), so her worm peaks at 2 slots where the levy's has 6; the row compares two sizes of one minion
				if (lv.Stat.MaxSlots >= pc.Stat.MaxSlots + 1.5f) fl.Add("STRONG-MORE-SLOTS"); else fl.Add("STRONG");
			}
			if (!sentry && pc.MinionTicks > 100)
			{
				if (lv.MinionTicks < 30) fl.Add("NOMINION");
				else
				{
					// a ranged minion fights from where it hovers, so how near the enemy it is says little by itself: it counts only beside a damage deficit
					bool lessDamage = ratio < 0.85f;
					if (lessDamage && (lv.Engaged < pc.Engaged * AbIdle || lv.Near > pc.Near + 0.25f)) fl.Add("HOVERS");
					// the retarget half: after the mirror the damage must come back as the player's does
					float rb = pc.P[1].Dps > 0 ? lv.P[1].Dps / pc.P[1].Dps : 0f;
					if (pc.P[1].Hits > 5 && rb < AbWeak && ratio >= AbWeak) fl.Add("NORETARGET");
				}
			}
			return fl.Count == 0 ? "OK" : string.Join("+", fl.Distinct());
		}

		private static string Fx(float v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

		private static string AbRowText(AbResult pc, AbResult lv, string verdict)
		{
			return $"ROW | {pc.Label} | {verdict} | dps player {Fx(pc.Dps, "0")} levy {Fx(lv.Dps, "0")} (x{Fx(pc.Dps > 0 ? lv.Dps / pc.Dps : 0)}) | life-delta dps player {Fx(pc.Stat.Dps, "0")} levy {Fx(lv.Stat.Dps, "0")} | hits player {pc.Hits} levy {lv.Hits} | minions player {Fx(pc.AvgMinions, "0.0")} levy {Fx(lv.AvgMinions, "0.0")} | engaged player {Fx(pc.Engaged)} levy {Fx(lv.Engaged)} | near-owner player {Fx(pc.Near)} levy {Fx(lv.Near)} | moving player {Fx(pc.Moving)} levy {Fx(lv.Moving)} | d-owner tiles player {Fx(pc.DOwner, "0.0")} levy {Fx(lv.DOwner, "0.0")} | d-target tiles player {Fx(pc.DTarget, "0.0")} levy {Fx(lv.DTarget, "0.0")} | after-mirror engaged player {Fx(pc.P[1].EngagedShare)} levy {Fx(lv.P[1].EngagedShare)}, dps player {Fx(pc.P[1].Dps, "0")} levy {Fx(lv.P[1].Dps, "0")}, first hit player {pc.P[1].FirstHit} levy {lv.P[1].FirstHit} | alive on average (minions and towers) player {Fx(pc.Stat.MinionTicks / (float)Math.Max(1, pc.Stat.Ticks), "0.0")} levy {Fx(lv.Stat.MinionTicks / (float)Math.Max(1, lv.Stat.Ticks), "0.0")} | slots taken (peak) player {Fx(pc.Stat.MaxSlots)} levy {Fx(lv.Stat.MaxSlots)} | overlap with the enemy per minion-tick player {Fx(pc.Overlap)} levy {Fx(lv.Overlap)} | turret slots player {pc.Stat.Turrets} levy {lv.Stat.Turrets} | casts player {pc.Stat.Uses} levy {lv.Stat.Casts} | slots player {pc.Stat.MaxMinions} levy {lv.Stat.MaxMinions}{(pc.Stat.Err.Length > 0 ? " | player ERR " + pc.Stat.Err : "")}{(lv.Stat.Err.Length > 0 ? " | levy ERR " + lv.Stat.Err : "")}";
		}

		private static void AbJson(StreamWriter w, AbResult pc, AbResult lv, string verdict)
		{
			string Ph(AbPhase p) => $"{{\"ticks\":{p.Ticks},\"dmg\":{p.Dmg},\"hits\":{p.Hits},\"minionHits\":{p.MinionHits},\"minionDmg\":{p.MinionDmg},\"itemDmg\":{p.ItemDmg},\"minionTicks\":{p.MinionTicks},\"engaged\":{Fx(p.EngagedShare, "0.###")},\"near\":{Fx(p.NearShare, "0.###")},\"moving\":{Fx(p.MovingShare, "0.###")},\"dOwner\":{Fx(p.AvgDOwner, "0.##")},\"dTarget\":{Fx(p.AvgDTarget, "0.##")},\"firstHit\":{p.FirstHit}}}";
			string Side(AbResult r) => $"{{\"dps\":{Fx(r.Dps, "0.#")},\"lifeDps\":{Fx(r.Stat.Dps, "0.#")},\"dmg\":{r.Dmg},\"hits\":{r.Hits},\"minionHits\":{r.MinionHits},\"avgMinions\":{Fx(r.AvgMinions, "0.##")},\"engaged\":{Fx(r.Engaged, "0.###")},\"near\":{Fx(r.Near, "0.###")},\"moving\":{Fx(r.Moving, "0.###")},\"overlap\":{Fx(r.Overlap, "0.###")},\"dOwner\":{Fx(r.DOwner, "0.##")},\"dTarget\":{Fx(r.DTarget, "0.##")},\"casts\":{(r.Who == "player" ? r.Stat.Uses : r.Stat.Casts)},\"slots\":{r.Stat.MaxMinions},\"maxAlive\":{r.Stat.MaxAlive},\"err\":{JQ(r.Stat.Err)},\"rec\":{JQ(string.IsNullOrEmpty(r.Rec) ? "" : Path.GetFileName(r.Rec))},\"a\":{Ph(r.P[0])},\"b\":{Ph(r.P[1])}}}";
			w.WriteLine($"{{\"label\":{JQ(pc.Label)},\"verdict\":{JQ(verdict)},\"player\":{Side(pc)},\"levy\":{Side(lv)}}}");
			w.Flush();
		}
		/// <summary>ARMY MODE (LEVY_SUMMONAB_ARMY=N): N levies with the same staff hold ground beside each other against the same enemy; each should do what a lone levy does (N times the lone levy's damage), as N players would. Logged as an ARMY row; a share under 0.7 is flagged SCALES-DOWN.</summary>
		private static float AbArmyRow(int type, string label, AbResult one, int n, int ticks, int slots, int tiles, StreamWriter jw)
		{
			long p0 = ArmyHitCooldown.Passes, st0 = ArmyHitCooldown.Set, id0 = ArmyHitCooldown.IdPasses;
			AbResult many = AbRun(type, label, false, ticks, slots, tiles, Environment.GetEnvironmentVariable("LEVY_SUMMONAB_ARMYREC") == "1" ? Environment.GetEnvironmentVariable("LEVY_SUMMONAB_OUT") : null, false, n);
			float share = one.Dps > 0 ? many.Dps / (n * one.Dps) : 0f;
			string v = share < 0.7f ? "SCALES-DOWN" : share > 1.4f ? "SCALES-UP" : "SCALES";
			_mod.Logger.Info($"[SummonAB] ARMY | {label} | {v} | dps one {Fx(one.Dps, "0")} x{n} levies {Fx(many.Dps, "0")} (share {Fx(share)}) | hits one {one.Hits} army {many.Hits} | minions per levy one {Fx(one.AvgMinions, "0.0")} army {Fx(many.AvgMinions / n, "0.0")} | hits by levy [{string.Join(",", many.HitsByBody.OrderBy(kv => kv.Key).Select(kv => kv.Value))}] | hit-cooldown passes {ArmyHitCooldown.Passes - p0} cooldowns set {ArmyHitCooldown.Set - st0} | id-static passes {ArmyHitCooldown.IdPasses - id0} | target immune[255]>0 on {many.ImmTicks} of {many.Ticks} ticks (max {many.ImmMax}), alone {one.ImmTicks} of {one.Ticks}");
			jw?.WriteLine($"{{\"label\":{JQ(label)},\"army\":{n},\"verdict\":{JQ(v)},\"oneDps\":{Fx(one.Dps, "0.#")},\"armyDps\":{Fx(many.Dps, "0.#")},\"share\":{Fx(share, "0.###")},\"oneHits\":{one.Hits},\"armyHits\":{many.Hits}}}"); jw?.Flush();
			return share;
		}
		private static string JQ(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") + "\"";

		/// <summary>The ship sample: one staff for each way a minion behaves (a walker, a flyer, a shooter, a swarm, a chain, a tower, a whip's summon tag excluded: whips are not minions).</summary>
		private static readonly short[] AbSampleVanilla = { ItemID.SlimeStaff, ItemID.RavenStaff, ItemID.OpticStaff, ItemID.XenoStaff, ItemID.StardustDragonStaff, ItemID.EmpressBlade, ItemID.VampireFrogStaff, ItemID.FlinxStaff, ItemID.QueenSpiderStaff };
		/// <summary>Calamity sample: the Infected Remote (the Plague Princess hung beside a standing levy and never fought: 0 hits) and the Abandoned Slime Staff (its cast factor on the damage was lost at the first upkeep: half the player's per hit) are here to hold those fixes; the Queen Spider above is a tower the stand-in must now get to fire.</summary>
		private static readonly string[] AbSampleCal = { "EtherealSubjugator", "CorvidHarbringerStaff", "EnchantedConch", "InfectedRemote", "AbandonedSlimeStaff" };
		/// <summary>The staves whose army run is part of the ship sample: contact minions on the game's static ID immunity (one cooldown per projectile type, shared by every owner), the ones that shared one cooldown across the whole army.</summary>
		private static readonly int[] AbArmySample = { ItemID.SlimeStaff, ItemID.VampireFrogStaff, ItemID.FlinxStaff };
		private const int AbArmyShipN = 3;

		/// <summary>
		/// ONE THROWN EXCEPTION BENCHES THE STAFF FOR ONE LEVY ONLY. Two levies hold the Slime Staff; at tick 60 the first levy's real minion throws inside its update (as another mod's hook would). Asserted: only that levy is benched (the staff
		/// is not bad for everyone, the other levy still drives it), the other keeps its real minions while the first uses stand-ins, and after the bench (shortened to 240 ticks here) the first has real minions again. Plus the bench rules on a made-up staff.
		/// </summary>
		private static void AbBenchCheck(int type, string label, int ticks, int slots, int tiles)
		{
			string self = SoldierMinions.BenchSelfTest();
			ABCheck("A/B bench rules: one levy's thrown exception benches that levy alone, retries after the bench and benches the staff for all only when three levies threw" + (self.Length > 0 ? " (" + self + ")" : ""), self.Length == 0);
			int notes0 = SoldierMinions.BenchNotes;
			AbResult r = AbRun(type, label, false, ticks, slots, tiles, null, false, 2, true);
			AbCtx c = r.Ctx;
			bool shoot = c != null && c.BenchedFirst && !c.BenchedSecond && !c.BenchWide && c.CanDriveSecond;
			ABNote($"bench {label}: thrown {SoldierMinions.BenchNotes - notes0}, first levy benched {c?.BenchedFirst}, second {c?.BenchedSecond}, staff bad for all {c?.BenchWide} (second can drive {c?.CanDriveSecond}); mid-bench real/stand-in first {c?.BenchSeen[0]}/{c?.BenchSeen[2]} second {c?.BenchSeen[1]}/{c?.BenchSeen[3]}; at the end first {c?.BenchSeen[4]}/{c?.BenchSeen[6]} second {c?.BenchSeen[5]}/{c?.BenchSeen[7]}");
			ABCheck($"A/B bench {label}: the throw benched the levy it threw for only (benched {c?.BenchedFirst}, other levy benched {c?.BenchedSecond}, staff bad for everyone {c?.BenchWide})", shoot && SoldierMinions.BenchNotes - notes0 >= 1);
			ABCheck($"A/B bench {label}: meanwhile the benched levy holds stand-ins ({c?.BenchSeen[0]} real, {c?.BenchSeen[2]} stand-in) and the other keeps its real minions ({c?.BenchSeen[1]} real)", c != null && c.BenchSeen[0] == 0 && c.BenchSeen[1] > 0);
			ABCheck($"A/B bench {label}: after the bench the first levy runs real minions again ({c?.BenchSeen[4]} real at the end)", c != null && c.BenchSeen[4] > 0 && c.BenchSeen[5] > 0);
		}

		/// <summary>
		/// TWO LEVIES WITH TENRYU. Calamity's dragon heads scan every projectile of their owner for their segments and throw on a second tail; every levy's minion is owner 255, so the other levy's dragon is parked off the owner slot for the update.
		/// Asserted: nothing threw (no bench note, the staff not bad), both levies' dragons hit the enemy.
		/// </summary>
		private static void AbOwnerScanCheck(int type, string label, int ticks, int slots, int tiles)
		{
			int notes0 = SoldierMinions.BenchNotes, parks0 = SoldierMinions.ModdedParks; int shoot = SumItem(type).shoot;
			AbResult r = AbRun(type, label, false, ticks, Math.Max(slots, 6), tiles, null, false, 2);
			int hitters = r.HitsByBody.Count(kv => kv.Value > 0);
			ABNote($"owner scan {label}: 2 levies, hits by levy [{string.Join(",", r.HitsByBody.OrderBy(kv => kv.Key).Select(kv => kv.Value))}], dps {Fx(r.Dps, "0")}, throws {SoldierMinions.BenchNotes - notes0}, parked updates {SoldierMinions.ModdedParks - parks0}, minions per levy {Fx(r.AvgMinions / 2, "0.0")}");
			ABCheck($"A/B owner scan {label}: two levies with the staff, no minion threw and the staff stays real ({SoldierMinions.BenchNotes - notes0} throws, bad for everyone {SoldierMinions.IsBadStaff(shoot)})", SoldierMinions.BenchNotes == notes0 && !SoldierMinions.IsBadStaff(shoot));
			ABCheck($"A/B owner scan {label}: both levies' minions hit the enemy ({hitters} of 2 levies hit it)", hitters == 2);
		}

		private static void SummonAbCheck()
		{
			string mode = Environment.GetEnvironmentVariable("LEVY_SUMMONAB");
			if (mode == "0" || (string.IsNullOrEmpty(mode) && Environment.GetEnvironmentVariable("LEVY_SUMMONAB_ALONE") != "1")) return;
			_abPass = _abFail = 0; SummonAbNanSpy.On = AbDebug;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMONAB_TICKS"), out int tk) ? tk : 720;
			int tiles = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMONAB_TILES"), out int tl) ? tl : 12;
			int slots = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMONAB_SLOTS"), out int sl) ? sl : 6;
			string outDir = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_OUT");
			bool rec = !string.IsNullOrEmpty(outDir) && Environment.GetEnvironmentVariable("LEVY_SUMMONAB_REC") != "0";
			bool focus = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_FOCUS") == "1";
			int armyN = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMONAB_ARMY"), out int an) ? an : 0;
			string only = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Split(';', StringSplitOptions.RemoveEmptyEntries);
			bool all = mode == "all" || names != null;
			ModLoader.TryGetMod("CalamityMod", out Mod calMod);
			bool cal = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_CAL") != "0" && calMod != null;
			var list = new List<(int type, string label)>();
			if (all)
			{
				list.AddRange(SumCatalogue(null)); if (cal) list.AddRange(SumCatalogue(calMod));
				if (names != null) list = list.Where(a => names.Contains(a.label.Substring(a.label.IndexOf(':') + 1))).ToList();
			}
			else
			{
				foreach (short t in AbSampleVanilla) list.Add((t, "Terraria:" + ItemID.Search.GetName(t)));
				if (cal) foreach (string n in AbSampleCal) if (calMod.TryFind(n, out ModItem mi)) list.Add((mi.Type, "CalamityMod:" + n));
			}
			int shard = 0, shards = 1;
			string sh = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_SHARD");
			if (!string.IsNullOrEmpty(sh) && sh.Contains('/')) { shard = int.Parse(sh.Split('/')[0]); shards = Math.Max(1, int.Parse(sh.Split('/')[1])); }
			if (shards > 1) list = list.Where((x, i) => i % shards == shard).ToList();
			bool sp = _spNet; int myPrev = _my;
			StreamWriter jw = null;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var tally = new Dictionary<string, int>();
			try
			{
				_spNet = Environment.GetEnvironmentVariable("LEVY_SUMMONAB_SPNET") == "1"; _my = 0;
				if (!string.IsNullOrEmpty(outDir)) { Directory.CreateDirectory(outDir); jw = new StreamWriter(Path.Combine(outDir, shards > 1 ? $"summonab_{shard}.jsonl" : "summonab.jsonl"), true); }
				ABNote($"summon A/B: {list.Count} staves, {ticks} ticks each (mirror at {ticks / 2}), enemies {tiles} tiles off, {slots} minion slots, {(_spNet ? "single-player netMode" : "server netMode")}, {(focus ? "the player focuses" : "no focus click")}{(rec ? ", recording to " + outDir : "")}");
				foreach (var (type, label) in list)
				{
					try
					{
						bool sentry = WeaponClassifier.GetProfile(SumItem(type)).Family == WeaponFamily.Sentry;
						AbResult pc = AbRun(type, label, true, ticks, slots, tiles, rec ? outDir : null, focus);
						AbResult lv = AbRun(type, label, false, ticks, slots, tiles, rec ? outDir : null, focus);
						// a tower that shoots only at the target its owner right-clicked (Calamity's Perdition reads Owner.HasMinionAttackTargetNPC) does nothing for a player who never clicks: she clicks (the levy's minions are always given their target)
						if (sentry && pc.Hits == 0 && pc.Stat.Uses > 0 && !focus) { AbResult pf = AbRun(type, label, true, ticks, slots, tiles, rec ? outDir : null, true); if (pf.Hits > 0) { ABNote($"{label}: the tower fires only at an enemy its owner right-clicked; the player row is the focused run ({pf.Hits} hits)"); pc = pf; } }
						string v = AbVerdict(pc, lv, sentry);
						int armyHere = armyN > 1 ? armyN : !all && Array.IndexOf(AbArmySample, type) >= 0 ? AbArmyShipN : 0;
						if (armyHere > 1 && !sentry && lv.Hits > 0) { float armyShare = AbArmyRow(type, label, lv, armyHere, ticks, slots, tiles, jw); if (!all && armyN <= 1) ABCheck($"A/B army {label}: {armyHere} levies with the staff do {armyHere} times one levy's damage, as {armyHere} players would (share {Fx(armyShare)}, at least 0.7)", armyShare >= 0.7f); }
						tally[v] = tally.GetValueOrDefault(v) + 1;
						_mod.Logger.Info("[SummonAB] " + AbRowText(pc, lv, v));
						if (jw != null) AbJson(jw, pc, lv, v);
						// the ship asserts what the A/B is sure of: the stand-in player really hurts the enemy (else the row says nothing) and the levy's minions fight, with at least half the player's damage (the rows are two boots of a random fight, a staff 0.9 one time is 0.7 the next)
						if (!all) ABCheck($"A/B {label}: the stand-in player deals damage ({pc.Hits} hits, {Fx(pc.Dps, "0")} dps) and the levy's minions fight as hers do ({v}: dps levy {Fx(lv.Dps, "0")} = x{Fx(pc.Dps > 0 ? lv.Dps / pc.Dps : 0)}; engaged {Fx(pc.Engaged)} vs {Fx(lv.Engaged)}; near owner {Fx(pc.Near)} vs {Fx(lv.Near)})", pc.Hits > 0 && lv.Hits > 0 && lv.Dps >= 0.5f * pc.Dps);
					}
					catch (Exception e) { _mod.Logger.Info($"[SummonAB] ROW | {label} | threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}"); if (!all) ABCheck($"A/B {label} threw {e.GetType().Name}", false); }
				}
				if (!all)
				{
					try
					{
						AbBenchCheck(ItemID.SlimeStaff, "Terraria:SlimeStaff", Math.Min(ticks, 720), slots, tiles);
						if (cal && calMod.TryFind("KingofConstellationsTenryu", out ModItem ten)) AbOwnerScanCheck(ten.Type, "CalamityMod:KingofConstellationsTenryu", Math.Min(ticks, 720), slots, tiles);
					}
					catch (Exception e) { ABCheck("summon A/B bench / owner-scan checks threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
				}
			}
			catch (Exception e) { ABCheck("summon A/B threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _spNet = sp; _my = myPrev; jw?.Dispose(); _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); _ab = null; }
			_mod.Logger.Info($"[SummonAB] TALLY {string.Join(", ", tally.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value))} over {list.Count} staves in {sw.ElapsedMilliseconds / 1000} s");
			_mod.Logger.Info($"[SummonAB] self-check pass={_abPass} fail={_abFail}");
		}
	}
	/// <summary>Lab only (LEVY_SUMMONAB_DEBUG=1): logs the first AI pass of a player-owned minion whose position or velocity turns NaN.</summary>
	public class SummonAbNanSpy : GlobalProjectile
	{
		internal static bool On; private static int _n;
		private Vector2 _pos, _vel; private float _a0, _a1;
		public override bool InstancePerEntity => true;
		private static int _sent, _births;
		public override void OnSpawn(Projectile p, Terraria.DataStructures.IEntitySource source)
		{
			if (On && p.owner == 0 && !p.minion && !p.sentry && _births++ < 12) Levy.Instance.Logger.Info($"[SummonAB] OBSERVED BIRTH {p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type)} at={p.Center} vel={p.velocity} dmg={p.damage} friendly={p.friendly} hostile={p.hostile} owner={p.owner} myPlayer={Main.myPlayer} src={source?.GetType().Name}");
		}
		public override void OnKill(Projectile p, int timeLeft)
		{
			if (On && p.owner == 0 && !p.minion && !p.sentry && _births < 14 && p.type == 309) Levy.Instance.Logger.Info($"[SummonAB] OBSERVED KILL {ProjectileID.Search.GetName(p.type)} timeLeft={timeLeft} at={p.Center} stack={Environment.StackTrace.Replace("\n", " | ").Substring(0, 400)}");
		}
		public override bool PreAI(Projectile p)
		{
			if (On) { _pos = p.position; _vel = p.velocity; _a0 = p.ai[0]; _a1 = p.ai[1]; }
			if (On && p.sentry && p.owner == 0 && _sent++ % 120 == 0 && _sent < 600) Levy.Instance.Logger.Info($"[SummonAB] OBSERVED SENTRY SPY {p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type)} ai=({p.ai[0]},{p.ai[1]}) at={p.Center} myPlayer={Main.myPlayer} owner={p.owner} net={Main.netMode} dead={Main.player[0].dead} active={Main.player[0].active} mouse=({Main.mouseX},{Main.mouseY}) enemies={Main.npc.Count(n => n.active && n.CanBeChasedBy(p))} canhit=[{string.Join(";", Main.npc.Where(n => n.active && n.CanBeChasedBy(p)).Select(n => Collision.CanHit(p.position, p.width, p.height, n.position, n.width, n.height) + "@" + (int)(System.Math.Abs(p.Center.X - n.Center.X) + System.Math.Abs(p.Center.Y - n.Center.Y))))}] attack={Main.player[p.owner].MinionAttackTargetNPC} size={p.width}x{p.height} pos={p.position} npcpos={string.Join(";", Main.npc.Where(n => n.active && n.CanBeChasedBy(p)).Select(n => n.position + " " + n.width + "x" + n.height))}");
			return true;
		}
		private static int _sentPost;
		public override void PostAI(Projectile p)
		{
			if (On && p.sentry && p.owner == 0 && _sentPost < 400 && Main.GameUpdateCount % 3 == 0)
			{
				_sentPost++; int ti = (int)p.ai[1]; NPC tn = ti >= 0 && ti < Main.maxNPCs ? Main.npc[ti] : null;
				Levy.Instance.Logger.Info($"[SummonAB] OBSERVED SENTRY POST type={p.type}({ProjectileID.Search.GetName(p.type)}) who={p.whoAmI} ai=({p.ai[0]},{p.ai[1]}) localAI=({p.localAI[0]},{p.localAI[1]}) vel={p.velocity} aiStyle={p.aiStyle} active={p.active} timeLeft={p.timeLeft} dmg={p.damage}/{p.originalDamage} friendly={p.friendly} npcProj={p.npcProj} target[{ti}]={(tn == null ? "-" : tn.active + "/chase" + tn.CanBeChasedBy(p) + "/" + tn.FullName)} myPlayer={Main.myPlayer} tick={Main.GameUpdateCount}");
			}
			if (!On || !p.minion || p.owner != 0 || _n >= 3) return;
			if ((float.IsNaN(p.position.X) || float.IsNaN(p.velocity.X)) && !float.IsNaN(_pos.X))
			{
				_n++;
				Player o = Main.player[p.owner];
				Levy.Instance.Logger.Info($"[SummonAB] OBSERVED NAN SPY {p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type)}: before pos={_pos} vel={_vel} ai=({_a0},{_a1}) -> after pos={p.position} vel={p.velocity}; owner center={o.Center} pos={o.position} vel={o.velocity} width={o.width} height={o.height} myPlayer={Main.myPlayer} raven={o.raven} dead={o.dead} active={o.active} MinionAttackTargetNPC={o.MinionAttackTargetNPC} minionPos={p.minionPos} w={p.width} h={p.height}");
			}
		}
	}
}
