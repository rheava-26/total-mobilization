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
	/// PURSUIT LAB (env WARBANDS_LIVE=move; WARBANDS_SEEDS per cell, default 18; WARBANDS_MOVE=open,overhang,chasm,pit,tower limits the scenarios; WARBANDS_OUT the TSV). Builds a real piece of terrain in the sealed arena, parks the
	/// stand-in player that cannot hurt the band (so the band lives for the whole trial), forms a hostile band by the same Setup as the spawner and measures how it gets to the player:
	///   t_first_hit   seconds until the player first loses life (-1 never)
	///   t_position    seconds until any member has a firing position (a clear line to the player and the player inside the member's own weapon reach; a blade: within 3.2 tiles), -1 never
	///   pos_end       members in a firing position at the end of the trial
	///   stuck         members that stayed within 3 tiles sideways and 7 tiles up or down of one spot for 10 s without a firing position
	///   clump         mean distance between members (tiles), sampled once a second from second 5
	///   spread_pos    mean distance between the members that have a position (how apart they fight from)
	/// One TSV row per trial.
	/// </summary>
	public partial class WarbandsLab
	{
		private enum Mv { Open, Overhang, Chasm, Pit, Tower, PitFar, Chase }

		private class MvState
		{
			public Mv Scen; public BandRoster Type; public int Seed;
			public int FirstHit = -1, FirstPos = -1, StartMembers;
			public readonly Dictionary<int, (Vector2 at, int since, bool stuck)> Anchor = new();
			public int StuckCount;
			public double ClumpSum; public int ClumpN; public double SpreadSum; public int SpreadN;
			public int BestRise; public int Hits; public readonly HashSet<int> HitIds = new(); // highest the band got above where it formed, tiles
			public float StartY;
		}

		private const int MvFloor = ArenaFloor;
		private int MvC => ArenaX;

		private void MvBox(int x0, int x1, int y0, int y1, bool solid)
		{
			for (int x = Math.Max(10, x0); x <= Math.Min(Main.maxTilesX - 10, x1); x++)
				for (int y = Math.Max(10, y0); y <= Math.Min(Main.maxTilesY - 10, y1); y++)
				{
					Tile t = Main.tile[x, y]; t.ClearEverything();
					if (solid) { t.HasTile = true; t.TileType = TileID.GrayBrick; }
				}
		}

		/// <summary>Builds the scenario's ground. Returns the player's column and floor row and the band's floor spots (column, row of the solid tile under the feet).</summary>
		private void MvBuild(Mv s, int n, out Point player, out List<Point> spots)
		{
			int c = MvC, F = MvFloor;
			int sp = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_MOVE_SPACING"), out int spv) ? spv : 3; // tiles between members when a band forms (the spawner's own spacing)
			MvBox(c - 110, c + 110, 40, F + 70, false);
			MvBox(c - 111, c - 111, 40, F + 70, true); MvBox(c + 111, c + 111, 40, F + 70, true);
			MvBox(c - 110, c + 110, F, F + 70, true);
			spots = new List<Point>();
			switch (s)
			{
				case Mv.Open:
					player = new Point(c + 10, F);
					for (int k = 0; k < n; k++) spots.Add(new Point(c - 30 - sp * k, F));
					break;
				case Mv.Chase:
					// open ground; the player runs back and forth 25 tiles each way (a 15 second round trip, about 10 tiles a second)
					player = new Point(c + 10, F);
					for (int k = 0; k < n; k++) spots.Add(new Point(c - 30 - sp * k, F));
					break;
				case Mv.Overhang:
					// a cliff top 16 tiles over the floor, undercut: an 8-tile cave opens to the left. The player stands in the cave, the band on the cliff straight above.
					MvBox(c - 12, c + 70, F - 16, F - 9, true);
					MvBox(c + 14, c + 70, F - 16, F - 1, true);
					player = new Point(c - 2, F);
					for (int k = 0; k < n; k++) spots.Add(new Point(c - 6 + sp * k, F - 16));
					break;
				case Mv.Chasm:
					// a 17-wide, 40-deep chasm; the player on the far side on a plateau 10 tiles higher, 22 tiles from the edge; the band 36 tiles short of the near edge
					MvBox(c - 8, c + 8, F, F + 40, false);
					MvBox(c + 9, c + 110, F - 10, F - 1, true);
					player = new Point(c + 30, F - 10);
					for (int k = 0; k < n; k++) spots.Add(new Point(c - 44 - sp * k, F));
					break;
				case Mv.Pit:
					// a 12-wide, 30-deep pit; the band at its bottom, the player on the rim 18 tiles from it
					MvBox(c - 6, c + 5, F, F + 30, false);
					player = new Point(c + 24, F);
					for (int k = 0; k < n; k++) spots.Add(new Point(c - 5 + 2 * k, F + 31));
					break;
				case Mv.PitFar:
					// the pit again with the player 105 tiles off: no player's view box holds the band, so the levy's off-screen hop is allowed
					MvBox(c - 100, c - 89, F, F + 30, false);
					player = new Point(c + 10, F);
					for (int k = 0; k < n; k++) spots.Add(new Point(c - 99 + 2 * k, F + 31));
					break;
				default: // Tower
					// a 34-tile pillar, 8 wide; the player on top, the band on the floor 50 tiles away
					MvBox(c + 20, c + 27, F - 34, F - 1, true);
					player = new Point(c + 23, F - 34);
					for (int k = 0; k < n; k++) spots.Add(new Point(c - 30 - sp * k, F));
					break;
			}
		}

		private static void MvClearRoutes()
		{
			try
			{
				var f = typeof(Levy.Common.Movement.RoutePlanner).GetField("_routes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
				(f?.GetValue(null) as System.Collections.IDictionary)?.Clear();
			}
			catch { }
		}

		private List<BandLevyNPC> MvForm(BandRoster type, List<Point> spots, int side)
		{
			int stage = type.Stage, bandId = BandBrain.NextId();
			var first = spots[0];
			var brain = BandBrain.Create(type, stage, spots.Count, new Vector2(first.X * 16f + 8f, first.Y * 16f - BandLevyNPC.BodyHeight * 0.5f), bandId, false);
			brain.Aggro = true; brain.Warns = false;
			var made = new List<BandLevyNPC>();
			for (int k = 0; k < spots.Count; k++)
			{
				var sp = spots[k];
				int id = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), sp.X * 16 + 8, sp.Y * 16 - 1, ModContent.NPCType<BandLevyNPC>());
				if (id < 0 || id >= Main.maxNPCs) continue;
				var b = Main.npc[id].ModNPC as BandLevyNPC; if (b == null) continue;
				b.Setup(type, k, stage, bandId, spots.Count);
				Main.npc[id].direction = side;
				made.Add(b);
			}
			brain.StartSize = made.Count;
			return made;
		}

		private bool MvInPosition(BandLevyNPC m)
		{
			var p = Main.player[0]; if (m.Weapon == null) return false;
			Vector2 pc = p.Center, mc = m.NPC.Center;
			float d = Vector2.Distance(mc, pc) / 16f - 1.2f;
			float reach = m.Weapon.IsMelee ? 3.2f : m.Weapon.RangeTiles * 0.98f;
			if (d > reach) return false;
			return Collision.CanHitLine(new Vector2(mc.X - 4f, m.NPC.position.Y + 6f), 8, 8, pc - new Vector2(4f, 4f), 8, 8);
		}

		private void AddMoveTrials()
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SEEDS"), out int sv) ? sv : 18;
			string only = Environment.GetEnvironmentVariable("WARBANDS_MOVE");
			var types = new[] { BandTypes.ById("ForestDeserters"), BandTypes.ById("MeteorMarauders"), BandTypes.ById("MythrilMercenaries") };
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_MOVE_TICKS"), out int tv) ? tv : 2400;
			_tsv.AppendLine("scenario\ttype\tseed\tmembers\tt_first_hit\tt_position\tpos_end\tstuck\tclump\tspread_pos\tbest_rise\thits\tend_dist\talive");
			foreach (Mv scen in Enum.GetValues(typeof(Mv)))
			{
				if (!string.IsNullOrEmpty(only) && !only.Split(',').Any(x => x.Trim().Equals(scen.ToString(), StringComparison.OrdinalIgnoreCase))) continue;
				for (int seed = 0; seed < seeds; seed++)
				{
					var st = new MvState { Scen = scen, Seed = seed, Type = types[seed % types.Length] };
					int sd = seed; Mv sc = scen;
					_trials.Add(new Trial
					{
						Name = $"move {sc} / {st.Type.Id} / seed {sd}", Max = ticks + 5,
						Setup = () =>
						{
							Main.rand = new Terraria.Utilities.UnifiedRandom(7000 + 131 * (int)sc + sd);
							MvClearRoutes(); if (MvHookLog) { Levy.Common.Movement.SoldierHooks.LogCasts = true; Levy.Common.Movement.SoldierHooks.CastLog.Clear(); }
							int n = 4 + (sd / 3) % 2; // 4 or 5 members
							MvBuild(sc, n, out Point pl, out List<Point> spots);
							ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0; _shotIds.Clear(); _shotsSeen = 0;
							_pinAt = new Vector2(pl.X * 16f + 8f - 10f, pl.Y * 16f - 42f); Main.player[0].position = _pinAt; Main.player[0].velocity = Vector2.Zero;
							var made = MvForm(st.Type, spots, spots[0].X < pl.X ? 1 : -1);
							st.StartMembers = made.Count; st.StartY = made.Count > 0 ? made.Average(m => m.NPC.Bottom.Y) : 0;
						},
						Tick = t =>
						{
							if (sc == Mv.Chase) { float ph = t * 2f * (float)Math.PI / 900f; Main.player[0].velocity = new Vector2((float)Math.Cos(ph) * 25f * 16f * 2f * (float)Math.PI / 900f, 0f); _pinAt = new Vector2((MvC + 10) * 16f + 8f - 10f + (float)Math.Sin(ph) * 25f * 16f, MvFloor * 16f - 42f); }
							if (st.FirstHit < 0 && DamageTaken > 0) st.FirstHit = t; // a blade that landed (a dedicated server runs no player-side projectile hits, so shots are counted below)
							{
								var ph = Main.player[0].Hitbox;
								for (int i = 0; i < Main.maxProjectiles; i++)
								{
									var pr = Main.projectile[i]; if (!pr.active || !pr.hostile || pr.damage <= 0) continue;
									if (!(pr.TryGetGlobalProjectile(out Levy.Common.Globals.SoldierProjectileTag tg) && tg.HostileShot)) continue;
									if (!pr.Colliding(pr.Hitbox, ph) || !st.HitIds.Add(pr.identity * 1000 + pr.type)) continue;
									st.Hits++; if (st.FirstHit < 0) st.FirstHit = t;
								}
							}
							var alive = Levies().Where(b => b.NPC.life > 0).ToList();
							bool any = false; var inPos = new List<BandLevyNPC>();
							foreach (var m in alive)
							{
								bool ip = MvInPosition(m); if (ip) { any = true; inPos.Add(m); }
								int rise = (int)((st.StartY - m.NPC.Bottom.Y) / 16f); if (rise > st.BestRise) st.BestRise = rise;
								if (t % 30 == 0)
								{
									int key = m.NPC.whoAmI;
									bool near = st.Anchor.TryGetValue(key, out var a) && Math.Abs(a.at.X - m.NPC.Center.X) <= 48f && Math.Abs(a.at.Y - m.NPC.Center.Y) <= 112f; // a body hopping on the spot is still on the spot
									if (!near) st.Anchor[key] = (m.NPC.Center, t, false);
									else if (!a.stuck && !ip && !m.Panic && t - a.since >= 600) { st.Anchor[key] = (a.at, a.since, true); st.StuckCount++; }
								}
							}
							if (any && st.FirstPos < 0) st.FirstPos = t;
							if (MvDbg && (t == 60 || t == 300 || t == 900 || t == 1800 || t == 2399)) MvDump(st, t, alive);
							if (t >= 300 && t % 60 == 0)
							{
								if (alive.Count >= 2) { st.ClumpSum += MvMeanPair(alive); st.ClumpN++; }
								if (inPos.Count >= 2) { st.SpreadSum += MvMeanPair(inPos); st.SpreadN++; }
							}
							return false;
						},
						Result = () =>
						{
							if (MvHookLog) { foreach (var l in Levy.Common.Movement.SoldierHooks.CastLog.Take(400)) Mod.Logger.Info("[Warbands] mvhook " + l); Levy.Common.Movement.SoldierHooks.CastLog.Clear(); }
							if (MvDbg) { foreach (var l in Levy.Common.Movement.RoutePlanner.Log) Mod.Logger.Info("[Warbands] mvroute " + l); Levy.Common.Movement.RoutePlanner.Log.Clear(); Mod.Logger.Info("[Warbands] mvroute " + Levy.Common.Movement.RoutePlanner.StatSummary()); }
							var alive = Levies().Where(b => b.NPC.life > 0).ToList();
							int posEnd = alive.Count(MvInPosition);
							Vector2 pc = Main.player[0].Center;
							float endDist = alive.Count == 0 ? -1f : alive.Min(m => Vector2.Distance(m.NPC.Center, pc) / 16f);
							string F(int tick) => tick < 0 ? "-1" : (tick / 60f).ToString("0.0");
							_tsv.AppendLine($"{sc}\t{st.Type.Id}\t{sd}\t{st.StartMembers}\t{F(st.FirstHit)}\t{F(st.FirstPos)}\t{posEnd}\t{st.StuckCount}\t{(st.ClumpN > 0 ? (st.ClumpSum / st.ClumpN).ToString("0.0") : "-1")}\t{(st.SpreadN > 0 ? (st.SpreadSum / st.SpreadN).ToString("0.0") : "-1")}\t{st.BestRise}\t{st.Hits + (DamageTaken > 0 ? 1 : 0)}\t{endDist:0.0}\t{alive.Count}");
							return (true, $"first hit {F(st.FirstHit)}s, position {F(st.FirstPos)}s, in position {posEnd}/{alive.Count}, stuck {st.StuckCount}, clump {(st.ClumpN > 0 ? st.ClumpSum / st.ClumpN : -1):0.0}, rise {st.BestRise}, closest {endDist:0.0}");
						},
					});
				}
			}
		}

		private static readonly bool MvHookLog = Environment.GetEnvironmentVariable("WARBANDS_MOVEHOOKLOG") == "1";
		private static readonly bool MvDbg = Environment.GetEnvironmentVariable("WARBANDS_MOVEDBG") == "1";
		private static string MvReflect(object o, string name)
		{
			try { var m = o.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null); return m?.Invoke(o, null)?.ToString(); } catch (Exception e) { return "?" + e.GetType().Name; }
		}
		private void MvDump(MvState st, int t, List<BandLevyNPC> alive)
		{
			Vector2 pc = Main.player[0].Center;
			foreach (var m in alive)
				Mod.Logger.Info($"[Warbands] mvdbg {st.Scen}/{st.Type.Id}/s{st.Seed} t={t} #{m.MemberIdx} role={m.Role} tile=({(int)(m.NPC.Center.X / 16f) - MvC},{(int)(m.NPC.Bottom.Y / 16f) - MvFloor}) toPlayer=({(pc.X - m.NPC.Center.X) / 16f:0},{(pc.Y - m.NPC.Center.Y) / 16f:0}) state={m.State} tgt={m.TargetIndex} inPos={MvInPosition(m)} panic={m.Panic} morale={m.Morale:0} still={MvReflect(m, "StillReason")} route={MvReflect(m, "DebugRoute")} pur[{m.PursuitDebug()}] hook={(m.Record?.Gear.Hook.IsAir == false)} wings={m.HasFlightKit} rng={m.Weapon?.RangeTiles}");
		}

		/// <summary>The scripted pursuit checks (WARBANDS_LIVE=check): a band with hooks gets a firing position on a player standing under its overhang, and gets out of a pit and across to the player.</summary>
		private void AddPursuitCheckTrials()
		{
			foreach (var (scen, label, secs) in new[] { (Mv.Overhang, "a band on the cliff over a player in the cave comes down and around to a firing position", 25), (Mv.Pit, "a band in a pit with hooks climbs out to a firing position", 30), (Mv.Chasm, "a band across a chasm from the player gets to a firing position", 30), (Mv.Tower, "a band at the foot of a 34-tile tower gets a firing position on the player", 30) })
			{
				var sc = scen; var st = new MvState { Scen = sc, Type = BandTypes.ById("MythrilMercenaries") }; int rise = 0;
				_trials.Add(new Trial
				{
					Name = "pursuit: " + label, Max = secs * 60 + 5,
					Setup = () =>
					{
						Main.rand = new Terraria.Utilities.UnifiedRandom(4242 + (int)sc); MvClearRoutes();
						MvBuild(sc, 4, out Point pl, out List<Point> spots);
						ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0;
						_pinAt = new Vector2(pl.X * 16f + 8f - 10f, pl.Y * 16f - 42f); Main.player[0].position = _pinAt; Main.player[0].velocity = Vector2.Zero;
						var made = MvForm(st.Type, spots, spots[0].X < pl.X ? 1 : -1); st.StartY = made.Count > 0 ? made.Average(m => m.NPC.Bottom.Y) : 0; rise = 0; st.FirstPos = -1;
					},
					Tick = t =>
					{
						foreach (var m in Levies()) { rise = Math.Max(rise, (int)((st.StartY - m.NPC.Bottom.Y) / 16f)); if (MvInPosition(m) && st.FirstPos < 0) st.FirstPos = t; }
						return st.FirstPos >= 0;
					},
					Result = () => (st.FirstPos >= 0, $"first firing position after {(st.FirstPos < 0 ? "never" : (st.FirstPos / 60f).ToString("0.0") + " s")}, highest rise {rise} tiles"),
				});
			}
		}

		private static double MvMeanPair(List<BandLevyNPC> l)
		{
			double s = 0; int c = 0;
			for (int i = 0; i < l.Count; i++) for (int j = i + 1; j < l.Count; j++) { s += Vector2.Distance(l[i].NPC.Center, l[j].NPC.Center) / 16f; c++; }
			return c == 0 ? 0 : s / c;
		}
	}
}
