using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// HUNT LAB (env WARBANDS_LIVE=hunt; WARBANDS_SEEDS per cell, default 6; WARBANDS_HUNT=flat,hills,cliffs,ravine,door,gap,cave,world limits the scenarios; WARBANDS_HUNT_D=60,120,200 the start distances in tiles;
	/// WARBANDS_HUNT_TICKS the trial length, default 9000; WARBANDS_OUT the TSV). A hostile band forms D tiles from a stand-in player that cannot hurt it and is measured for the whole trial:
	///   t_move    seconds until the band has moved 6 tiles from where it formed (-1 never)
	///   t_pos     seconds until any member has a firing position on the player (a clear line inside its own weapon's reach; a blade within 3.2 tiles), -1 never
	///   t_half    seconds until half the members have one at the same moment, -1 never
	///   t_hit     seconds until the player is first hit
	///   eng30/60/120   members in a firing position at 30, 60 and 120 s, as a share of the members alive
	///   stuck     members that stayed within 3 tiles sideways and 7 up or down of one spot for 10 s without a firing position
	///   idle_s    member-seconds a member stood still (under 1.2 tiles in 5 s) with no firing position, as a share of all member-seconds
	/// "world" forms natural-style bands (40 to 70 tiles) on the real surface of the loaded world at spread-out spots instead of built terrain. One TSV row per trial.
	/// </summary>
	public partial class WarbandsLab
	{
		private enum Hn { Flat, Hills, Cliffs, Ravine, Door, Gap, Cave, World }

		private class HnState
		{
			public Hn Scen; public int D, Seed; public BandRoster Type; public int Members;
			public int TMove = -1, TPos = -1, THalf = -1, THit = -1, TC25 = -1; public float Dist60 = -1f;
			public float[] Eng = { -1f, -1f, -1f };
			public int Stuck; public int IdleSamples, Samples;
			public readonly Dictionary<int, (Vector2 at, int since, bool stuck)> Anchor = new();
			public readonly Dictionary<int, Vector2> Hist = new();
			public readonly Dictionary<int, Queue<Vector2>> Trail = new();
			public Vector2 Start; public readonly HashSet<int> HitIds = new(); public int Hits;
			public double ClumpSum; public int ClumpN; public float EndDist = -1f; public int Alive;
		}

		private int HnFloor(Hn s, int u, int D, int seed)
		{
			int F = ArenaFloor + 40;
			switch (s)
			{
				case Hn.Hills:
				{
					double ph = seed * 1.7;
					return F - (int)Math.Round(4.5 * Math.Sin(u / 13.0 + ph) + 3.0 * Math.Sin(u / 5.1 + 2 * ph) + 2.0 * Math.Sin(u / 29.0 + ph * 0.5));
				}
				case Hn.Cliffs: { int step = (int)Math.Floor((u + D / 2.0) / Math.Max(12.0, D / 6.0)); return F - 5 * Math.Max(0, Math.Min(6, step + 1)); }
				default: return F;
			}
		}

		private void HnBuild(Hn s, int D, int seed, int m, out Point player, out List<Point> spots)
		{
			int cx = ArenaX, F = ArenaFloor + 40, half = D / 2 + 70;
			int X(int u) => cx + m * u;
			// clear everything, then the ground column by column
			for (int u = -half - 1; u <= half + 1; u++)
			{
				int x = X(u);
				for (int y = 36; y <= F + 70; y++) { Tile t = Main.tile[x, y]; t.ClearEverything(); }
				bool wall = Math.Abs(u) == half + 1;
				int fl = wall ? 36 : HnFloor(s, u, D, seed);
				for (int y = fl; y <= F + 70; y++) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.GrayBrick; }
			}
			int pu = D / 2;
			switch (s)
			{
				case Hn.Ravine:
					for (int u = -6; u <= 5; u++) for (int y = F; y <= F + 25; y++) { Tile t = Main.tile[X(u), y]; t.HasTile = false; }
					break;
				case Hn.Door:
				case Hn.Gap:
					for (int y = 36; y <= F - 1; y++) { Tile t = Main.tile[X(0), y]; t.HasTile = true; t.TileType = TileID.GrayBrick; }
					for (int y = F - 3; y <= F - 1; y++) { Tile t = Main.tile[X(0), y]; t.HasTile = false; }
					if (s == Hn.Door) { for (int y = F - 3; y <= F - 1; y++) { Tile t = Main.tile[X(0), y]; t.HasTile = true; t.TileType = TileID.ClosedDoor; t.TileFrameX = 0; t.TileFrameY = (short)((y - (F - 3)) * 18); } }
					break;
				case Hn.Cave:
				{
					// a stair tunnel down from the surface 34 tiles short of the player, then a chamber under him
					int u0 = pu - 34;
					for (int k = 0; k <= 24; k++) { int top = F - 6 + k / 2; for (int y = top; y <= top + 5; y++) for (int dx = 0; dx <= 1; dx++) { Tile t = Main.tile[X(u0 + k), y]; t.HasTile = false; } }
					for (int u = u0 + 24; u <= pu + 10; u++) for (int y = F + 6; y <= F + 11; y++) { Tile t = Main.tile[X(u), y]; t.HasTile = false; }
					break;
				}
			}
			spots = new List<Point>();
			int n = 4 + (seed / 3) % 2;
			for (int k = 0; k < n; k++) { int u = -D / 2 - 3 * k; spots.Add(new Point(X(u), HnFloor(s, u, D, seed) - 0)); }
			if (s == Hn.Cave) player = new Point(X(pu + 8), F + 12); else player = new Point(X(pu), HnFloor(s, pu, D, seed));
		}

		private void AddHuntTrials()
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SEEDS"), out int sv) ? sv : 6;
			string only = Environment.GetEnvironmentVariable("WARBANDS_HUNT");
			var dists = (Environment.GetEnvironmentVariable("WARBANDS_HUNT_D") ?? "60,120,200").Split(',').Select(x => int.Parse(x.Trim())).ToArray();
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_HUNT_TICKS"), out int tv) ? tv : 9000;
			var types = new[] { BandTypes.ById("ForestDeserters"), BandTypes.ById("MeteorMarauders"), BandTypes.ById("MythrilMercenaries") };
			_tsv.AppendLine("scenario\tdist\ttype\tseed\tmembers\tt_move\tt_c25\tt_pos\tt_half\tt_hit\teng30\teng60\teng120\tstuck\tidle_pct\tend_dist\talive\tdist60");
			foreach (Hn scen in Enum.GetValues(typeof(Hn)))
			{
				if (!string.IsNullOrEmpty(only) ? !only.Split(',').Any(x => x.Trim().Equals(scen.ToString(), StringComparison.OrdinalIgnoreCase)) : scen == Hn.World) continue;
				foreach (int D in scen == Hn.World ? new[] { 0 } : dists)
					for (int seed = 0; seed < seeds; seed++)
					{
						var only2 = Environment.GetEnvironmentVariable("WARBANDS_HUNT_SEEDLIST"); if (!string.IsNullOrEmpty(only2) && !only2.Split(',').Contains(seed.ToString())) continue;
						var st = new HnState { Scen = scen, D = D, Seed = seed, Type = types[seed % types.Length] };
						int sd = seed, DD = D; Hn sc = scen;
						_trials.Add(new Trial
						{
							Name = $"hunt {sc} D{DD} / {st.Type.Id} / seed {sd}", Max = ticks + 5,
							Setup = () =>
							{
								Main.rand = new Terraria.Utilities.UnifiedRandom(31000 + 131 * (int)sc + 17 * DD + sd);
								MvClearRoutes();
								List<BandLevyNPC> made;
								if (sc == Hn.World) { if (!HnWorldSetup(st, out made)) { st.Members = 0; return; } }
								else
								{
									int m = sd % 2 == 0 ? 1 : -1;
									HnBuild(sc, DD, sd, m, out Point pl, out List<Point> spots);
									ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0;
									_pinAt = new Vector2(pl.X * 16f + 8f - 10f, pl.Y * 16f - 42f); Main.player[0].position = _pinAt; Main.player[0].velocity = Vector2.Zero;
									made = MvForm(st.Type, spots, spots[0].X < pl.X ? 1 : -1);
								}
								st.Members = made.Count; st.Start = made.Count > 0 ? new Vector2(made.Average(b => b.NPC.Center.X), made.Average(b => b.NPC.Center.Y)) : default;
							},
							Tick = t =>
							{
								if (st.Members == 0) return true;
								if (st.THit < 0 && DamageTaken > 0) st.THit = t;
								var ph = Main.player[0].Hitbox;
								for (int i = 0; i < Main.maxProjectiles; i++)
								{
									var pr = Main.projectile[i]; if (!pr.active || !pr.hostile || pr.damage <= 0) continue;
									if (!(pr.TryGetGlobalProjectile(out Levy.Common.Globals.SoldierProjectileTag tg) && tg.HostileShot)) continue;
									if (!pr.Colliding(pr.Hitbox, ph) || !st.HitIds.Add(pr.identity * 1000 + pr.type)) continue;
									st.Hits++; if (st.THit < 0) st.THit = t;
								}
								if (t % 30 != 0) return false;
								var alive = Levies().Where(b => b.NPC.life > 0).ToList();
								if (MvDbg && (t == 60 || t == 600 || t == 1800 || t == 3500 || t == 7000)) foreach (var m in alive) Mod.Logger.Info($"[Warbands] huntdbg {sc}/s{sd} t={t} #{m.MemberIdx} role={m.Role} pos=({m.NPC.Center.X / 16f:0},{m.NPC.Bottom.Y / 16f:0}) toPlayer=({(Main.player[0].Center.X - m.NPC.Center.X) / 16f:0},{(Main.player[0].Center.Y - m.NPC.Center.Y) / 16f:0}) tgt={m.TargetIndex} hunt[{m.HuntDebug()}] pur[{m.PursuitDebug()}] panic={m.Panic} hook={(m.Record?.Gear.Hook.IsAir == false)}");
								if (alive.Count == 0) return false;
								int inPos = 0;
								foreach (var mm in alive)
								{
									bool ip = MvInPosition(mm); if (ip) inPos++;
									int key = mm.NPC.whoAmI;
									bool near = st.Anchor.TryGetValue(key, out var a) && Math.Abs(a.at.X - mm.NPC.Center.X) <= 48f && Math.Abs(a.at.Y - mm.NPC.Center.Y) <= 112f;
									if (!near) st.Anchor[key] = (mm.NPC.Center, t, false);
									else if (!a.stuck && !ip && !mm.Panic && t - a.since >= 600) { st.Anchor[key] = (a.at, a.since, true); st.Stuck++; }
									// idle: under 1.2 tiles in the last 5 s with no firing position
									if (!st.Trail.TryGetValue(key, out var q)) st.Trail[key] = q = new Queue<Vector2>();
									q.Enqueue(mm.NPC.Center); if (q.Count > 11) q.Dequeue();
									if (q.Count == 11) { st.Samples++; if (!ip && !mm.Panic && Vector2.Distance(q.Peek(), mm.NPC.Center) < 1.2f * 16f) st.IdleSamples++; }
								}
								if (inPos > 0 && st.TPos < 0) st.TPos = t;
								if (inPos * 2 >= alive.Count && st.THalf < 0) st.THalf = t;
								Vector2 c = new Vector2(alive.Average(b => b.NPC.Center.X), alive.Average(b => b.NPC.Center.Y));
								if (st.TMove < 0 && Vector2.Distance(c, st.Start) > 6f * 16f) st.TMove = t;
								float nearest = alive.Min(b => Vector2.Distance(b.NPC.Center, Main.player[0].Center)) / 16f;
								if (st.TC25 < 0 && nearest <= 25f) st.TC25 = t;
								if (t >= 3600 && st.Dist60 < 0) st.Dist60 = nearest;
								float share = inPos / (float)alive.Count;
								if (t >= 1800 && st.Eng[0] < 0) st.Eng[0] = share;
								if (t >= 3600 && st.Eng[1] < 0) st.Eng[1] = share;
								if (t >= 7200 && st.Eng[2] < 0) st.Eng[2] = share;
								if (alive.Count >= 2 && t >= 300) { st.ClumpSum += MvMeanPair(alive); st.ClumpN++; }
								return false;
							},
							Result = () =>
							{
								var alive = Levies().Where(b => b.NPC.life > 0).ToList();
								Vector2 pc = Main.player[0].Center;
								st.EndDist = alive.Count == 0 ? -1f : alive.Min(b => Vector2.Distance(b.NPC.Center, pc) / 16f);
								st.Alive = alive.Count;
								string F(int tick) => tick < 0 ? "-1" : (tick / 60f).ToString("0.0");
								string P(float v) => v < 0 ? "-1" : v.ToString("0.00");
								double idle = st.Samples == 0 ? -1 : 100.0 * st.IdleSamples / st.Samples;
								_tsv.AppendLine($"{sc}\t{DD}\t{st.Type.Id}\t{sd}\t{st.Members}\t{F(st.TMove)}\t{F(st.TC25)}\t{F(st.TPos)}\t{F(st.THalf)}\t{F(st.THit)}\t{P(st.Eng[0])}\t{P(st.Eng[1])}\t{P(st.Eng[2])}\t{st.Stuck}\t{idle:0.0}\t{st.EndDist:0.0}\t{st.Alive}\t{st.Dist60:0.0}");
								return (st.Members > 0, $"members {st.Members}, move {F(st.TMove)}s, within25 {F(st.TC25)}s, position {F(st.TPos)}s, half {F(st.THalf)}s, hit {F(st.THit)}s, engaged@30/60/120 {P(st.Eng[0])}/{P(st.Eng[1])}/{P(st.Eng[2])}, stuck {st.Stuck}, idle {idle:0.0}%, closest {st.EndDist:0.0}");
							},
							Cleanup = () => BuildArena(),
						});
					}
			}
		}

		/// <summary>A natural-style band on the real surface: the stand-in goes to a surface spot picked by seed across the loaded world, the band forms 40 to 70 tiles to one side of it by the spawner's own footing rules.</summary>
		private bool HnWorldSetup(HnState st, out List<BandLevyNPC> made)
		{
			made = new List<BandLevyNPC>();
			var rng = new Random(500 + st.Seed);
			for (int attempt = 0; attempt < 40; attempt++)
			{
				int x = rng.Next(220, Main.maxTilesX - 220);
				int fy = -1; // the open surface: the first floor from the sky down where a body fits, with nothing solid over it for 40 tiles (where a natural band forms)
				for (int y = Math.Max(60, (int)Main.worldSurface - 70); y < (int)Main.worldSurface + 30 && fy < 0; y++)
					if (BandSpawner.BodyFits(x, y)) { bool open = true; for (int a = 1; a <= 40 && open; a++) if (Main.tile[x, y - a].HasUnactuatedTile && Main.tileSolid[Main.tile[x, y - a].TileType]) open = false; if (open) fy = y; }
				if (fy < 0) continue;
				ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0;
				_pinAt = new Vector2(x * 16f + 8f - 10f, fy * 16f - 42f - 1f); Main.player[0].position = _pinAt; Main.player[0].velocity = Vector2.Zero;
				var fake = Main.player[0];
				if (!BandSpawner.SpawnBand(fake, st.Type, 4, 40, 70, false, false, false)) continue;
				var l = Levies(); if (l.Count < 3) { ClearArenaKeepPlayer(); continue; }
				foreach (var b in l) made.Add(b);
				if (BandSpawner.LastBrain != null) { BandSpawner.LastBrain.Aggro = true; BandSpawner.LastBrain.Warns = false; BandSpawner.LastBrain.Neutral = false; }
				return true;
			}
			return false;
		}

		private void ClearArenaKeepPlayer() { for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is BandLevyNPC) n.active = false; } BandBrain.All.Clear(); }
	}
}
