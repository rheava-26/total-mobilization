using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of the BODYGUARD rule (<see cref="Bodyguard"/>, SoldierNPC.Bodyguard.cs), logged as "[Bodyguard]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the shipworld stage
	/// chains it after the mobility world check; LEVY_BODYGUARD_ONLY=1 runs it alone). A flat arena, a Spectator commander standing still, an army of twelve armed levies on Follow spread 6 tiles apart on both sides of her (so the boss
	/// goes for levies, not for her), and a real Eye of Cthulhu held in its second phase (the charging one) with hit points that cannot run out. The SAME scene is run twice per seed, with the rule off and on, from the same random stream:
	///  * what a boss that has a levy in its sights makes of that levy: its distance from the commander and from the centroid of the other levies, per targeted tick (the distribution), and how far each targeting episode (two seconds
	///    of one levy being hunted, the boss allowed to look away for a third of a second) moves them;
	///  * the other levies: how many stand on the line between the boss and the levy it hunts;
	///  * how often a boss charge passes close to the commander (a charge is the boss moving at 10 px/tick or more; a pass is one visit inside the radius);
	///  * that the army keeps fighting: the damage the boss takes, and that nobody dies;
	///  * that the rule does not oscillate: the direction changes a hunted levy makes per hunted second.
	/// With Calamity loaded, a second scene boxes the fight in a Calamity arena box and checks that no decoy goal is set outside it. Throwaway soldiers only; nothing is saved.
	/// </summary>
	public sealed class BodyguardWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod; private static Vector2 _cmdAt; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 270;
		private const string Commander = "BodyguardTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool only = Environment.GetEnvironmentVariable("LEVY_BODYGUARD_ONLY") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_BODYGUARDWORLD") != "0";
			if (!only && !ship) return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the mobility world check calls this through the summon world check's chain).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time; int mode0 = Bodyguard.Mode;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player(); Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())), false); }
			finally { _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time; Bodyguard.Mode = mode0; }
			_mod.Logger.Info($"[Bodyguard] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[Bodyguard] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[Bodyguard] OBSERVED " + s);
		private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

		// ---------------------------------------------------------------------------------------------- scene
		private static void Step(int n) { for (int i = 0; i < n; i++) { KeepCommander(); WorldStep.Update(); } }

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = false; Main.time = 18000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>();
			if (!p.HasBuff(bt)) { p.AddBuff(bt, 600000); if (p.TryGetModPlayer(out global::Levy.Common.Potions.SpectatorPlayer sp)) sp.Anchor = p.Center; }
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void Arena()
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 120; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
					else if (!n.friendly && !n.townNPC) n.active = false;
				}
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				try { var b = Main.player[0]; int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(); if (b.HasBuff(bt)) b.ClearBuff(bt); } catch (Exception) { }
				var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg != null && _savedBossTarget.HasValue) { cfg.BossesTargetSoldiers = _savedBossTarget.Value; _savedBossTarget = null; }
			}
			catch (Exception) { }
		}
		private static bool? _savedBossTarget;

		private static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }

		/// <summary>An armed Follow levy with hit points a boss cannot use up; its body, or null.</summary>
		private static SoldierNPC Levy(int k, Vector2 at)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = k % 2 == 0, Name = "Pvt. Guard" + k,
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.Gear.TrySet(SoldierGear.SlotWeapon, Make(ItemID.Handgun), rec.AccessorySlots, out _);
			var ammo = Make(ItemID.MusketBall); ammo.stack = 999; rec.Gear.TrySet(SoldierGear.SlotAmmo, ammo, rec.AccessorySlots, out _);
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			Main.npc[body].lifeMax = Main.npc[body].life = 6000; rec.CurrentHp = 6000;
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		private static void SetBossesTarget(bool on)
		{
			var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg == null) return;
			_savedBossTarget ??= cfg.BossesTargetSoldiers; cfg.BossesTargetSoldiers = on;
		}

		// ---------------------------------------------------------------------------------------------- one arm
		private sealed class Result
		{
			public int Mode, Seed, PairTicks, Deaths, Pass12, Pass20, Legs, HeldTicks, Episodes, LaneTicks, Ticks;
			public double LaneSum; public long Damage, DecoySteps, LaneSteps;
			public readonly List<float> DC = new(), DK = new(), Growth = new(), GrowthK = new();
			public bool BossAlive; public float MinGoalMargin = float.MaxValue; public int GoalsOutside, GoalsSeen;
		}

		private struct Epi { public int Start, Last; public float DC0, DK0; public bool Done; }

		private static Result RunArm(int seed, int mode, int ticks, bool boxed)
		{
			var r = new Result { Mode = mode, Seed = seed };
			Cleanup();
			Terraria.Main.rand = new Terraria.Utilities.UnifiedRandom(seed);
			Bodyguard.Mode = mode;
			Arena();
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			Main.GameMode = GameModeID.Master;
			SetBossesTarget(true);
			KeepCommander(); Step(2);
			var levies = new List<SoldierNPC>();
			float[] offs = { -36, -30, -24, -18, -12, -6, 6, 12, 18, 24, 30, 36 };
			for (int k = 0; k < offs.Length; k++) { var s = Levy(k, new Vector2((Cx + offs[k]) * 16f, FloorY * 16f)); if (s != null) levies.Add(s); }
			object box = null; System.Collections.IList boxList = null; Vector2 boxTl = default, boxBr = default;
			if (boxed && TryMakeBox(out box, out boxList, out boxTl, out boxBr)) { }
			Step(90); // let them settle into the follow order
			long bg0 = Bodyguard.DecoySteps, lane0 = Bodyguard.LaneSteps;
			int legs0 = levies.Sum(l => l.BgLegs);
			int i0 = NPC.NewNPC(new EntitySource_WorldEvent("BodyguardCheck"), (int)((Cx + 10) * 16f), (int)((FloorY - 40) * 16f), NPCID.EyeofCthulhu);
			if (i0 < 0 || i0 >= Main.maxNPCs) { Check("the Eye of Cthulhu spawns", false); if (box != null) boxList.Remove(box); return r; }
			NPC boss = Main.npc[i0]; boss.timeLeft = 99999; boss.netOffset = Vector2.Zero;
			boss.lifeMax = 400000; boss.life = (int)(boss.lifeMax * 0.45f); // under half: the charging phase
			int life0 = boss.life;
			var epis = new Dictionary<int, Epi>();
			bool in12 = false, in20 = false;
			var alive0 = levies.Count;
			for (int t = 0; t < ticks; t++)
			{
				Step(1);
				if (!boss.active || boss.type != NPCID.EyeofCthulhu) break;
				boss.timeLeft = 99999; boss.dontTakeDamage = false;
				r.Ticks++;
				Vector2 cmd = Main.player[0].Center;
				// charge passes near the commander
				float bdc = Vector2.Distance(boss.Center, cmd) / 16f; bool charging = boss.velocity.Length() >= 10f;
				if (charging && bdc <= 12f && !in12) { r.Pass12++; in12 = true; } else if (bdc > 17f) in12 = false;
				if (charging && bdc <= 20f && !in20) { r.Pass20++; in20 = true; } else if (bdc > 26f) in20 = false;
				// the pairs
				var pairs = Bodyguard.Get();
				for (int pi = 0; pi < pairs.Count; pi++)
				{
					var pr = pairs[pi]; if (pr.Boss != i0) continue;
					NPC x = Main.npc[pr.Body]; if (!x.active) continue;
					float dc = Vector2.Distance(x.Center, cmd) / 16f;
					Vector2 sum = Vector2.Zero; int n = 0;
					for (int k = 0; k < levies.Count; k++) { var b = levies[k].NPC; if (b.active && b.whoAmI != x.whoAmI && b.life > 0) { sum += b.Center; n++; } }
					float dk = n > 0 ? Vector2.Distance(x.Center, sum / n) / 16f : 0f;
					r.PairTicks++; r.DC.Add(dc); r.DK.Add(dk);
					// others on the line between the boss and the hunted levy
					if (Vector2.Distance(boss.Center, x.Center) >= Bodyguard.LaneMinLenTiles * 16f)
					{
						int on = 0;
						for (int k = 0; k < levies.Count; k++)
						{
							var b = levies[k].NPC; if (!b.active || b.life <= 0 || b.whoAmI == x.whoAmI) continue;
							float d = Bodyguard.DistToSegmentTiles(b.Center, boss.Center, x.Center, out float al, out _);
							if (d < Bodyguard.LaneHalfTiles && al > 0.04f && al < 0.96f) on++;
						}
						r.LaneSum += on; r.LaneTicks++;
					}
					int key = x.whoAmI;
					if (!epis.TryGetValue(key, out var e) || t - e.Last > 20) e = new Epi { Start = t, DC0 = dc, DK0 = dk };
					e.Last = t;
					if (!e.Done && t - e.Start >= 120) { e.Done = true; r.Episodes++; r.Growth.Add(dc - e.DC0); r.GrowthK.Add(dk - e.DK0); }
					epis[key] = e;
				}
				// a decoy goal must be inside the arena box
				if (boxed && box != null)
				{
					for (int k = 0; k < levies.Count; k++)
					{
						var l = levies[k]; if (!l.NPC.active || !l.BodyguardRunning || !l.HasMoveGoal) continue;
						r.GoalsSeen++;
						float gx = l.MoveGoal.X; float margin = Math.Min(gx - boxTl.X, boxBr.X - gx);
						r.MinGoalMargin = Math.Min(r.MinGoalMargin, margin);
						if (margin < 0f) r.GoalsOutside++;
					}
				}
			}
			r.BossAlive = boss.active; r.Damage = Math.Max(0, life0 - boss.life);
			r.Deaths = alive0 - levies.Count(l => l.NPC.active && l.NPC.life > 0);
			r.Legs = levies.Sum(l => l.BgLegs) - legs0; r.DecoySteps = Bodyguard.DecoySteps - bg0; r.LaneSteps = Bodyguard.LaneSteps - lane0; r.HeldTicks = levies.Sum(l => l.BgHolds);
			if (box != null) boxList.Remove(box);
			Cleanup();
			return r;
		}

		/// <summary>A Calamity arena box around the middle of the arena, so a levy is held inside it as a player is (ProjParityCheck builds the same one).</summary>
		private static bool TryMakeBox(out object box, out System.Collections.IList list, out Vector2 tl, out Vector2 br)
		{
			box = null; list = null; tl = br = default;
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return false;
			try
			{
				Type sys = cal.Code.GetType("CalamityMod.Systems.Mechanic.ArenaWallSystem"); Type boxT = sys?.GetNestedType("Box");
				if (boxT == null) return false;
				list = (System.Collections.IList)sys.GetField("ActiveBoxes").GetValue(null);
				box = Activator.CreateInstance(boxT);
				Vector2 centre = new Vector2(Cx * 16f, (FloorY - 40) * 16f);
				boxT.GetField("position").SetValue(box, centre);
				boxT.GetField("boxDimensions").SetValue(box, new Vector4(900f, 700f, 900f, 700f)); // up, right, down, left (px): 44 tiles to each side
				boxT.GetField("borderThickness").SetValue(box, 20f);
				list.Add(box);
				tl = (Vector2)boxT.GetProperty("TopLeft").GetValue(box); br = (Vector2)boxT.GetProperty("BottomRight").GetValue(box);
				return true;
			}
			catch (Exception) { box = null; return false; }
		}

		// ---------------------------------------------------------------------------------------------- the check
		private static double Mean(IEnumerable<float> v) { var l = v.ToList(); return l.Count == 0 ? 0 : l.Average(); }
		private static double Pct(List<float> v, double p) { if (v.Count == 0) return 0; var s = v.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)(p * s.Count))]; }

		private static void Run()
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("LEVY_BODYGUARD_SEEDS"), out int sd) ? sd : 4;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_BODYGUARD_TICKS"), out int tk) ? tk : 1500;
			var off = new List<Result>(); var on = new List<Result>();
			for (int s = 0; s < seeds; s++)
			{
				int seed = 7000 + s * 31;
				off.Add(RunArm(seed, 0, ticks, false));
				on.Add(RunArm(seed, 1, ticks, false));
			}
			Report("flat arena, twelve Follow levies, Eye of Cthulhu charging", off, on);

			foreach (var arm in new[] { ("off", off), ("on", on) })
				Note($"rule {arm.Item1}: per-seed targeted ticks [{string.Join(", ", arm.Item2.Select(x => x.PairTicks))}], boss damage [{string.Join(", ", arm.Item2.Select(x => x.Damage))}], deaths [{string.Join(", ", arm.Item2.Select(x => x.Deaths))}]");

			int pt0 = off.Sum(x => x.PairTicks), pt1 = on.Sum(x => x.PairTicks);
			Check($"a real boss locks onto levies in this scene (targeted levy ticks: rule off {pt0}, on {pt1})", pt0 >= 150 && pt1 >= 150);
			double dc0 = Mean(off.SelectMany(x => x.DC)), dc1 = Mean(on.SelectMany(x => x.DC));
			double dk0 = Mean(off.SelectMany(x => x.DK)), dk1 = Mean(on.SelectMany(x => x.DK));
			Check($"a targeted levy stands farther from the commander with the rule on: mean {F(dc0)} -> {F(dc1)} tiles", dc1 >= dc0 + 3.0);
			Check($"a targeted levy stands farther from the rest of the army with the rule on: mean {F(dk0)} -> {F(dk1)} tiles", dk1 >= dk0 + 1.0);
			double g0 = Mean(off.SelectMany(x => x.Growth)), g1 = Mean(on.SelectMany(x => x.Growth));
			int e0 = off.Sum(x => x.Episodes), e1 = on.Sum(x => x.Episodes);
			Check($"over a two-second hunt the levy's distance from the commander grows more with the rule on: mean change {F(g0)} -> {F(g1)} tiles ({e0} and {e1} hunts)", e0 >= 3 && e1 >= 3 && g1 >= g0 + 1.0);
			double l0 = off.Sum(x => x.LaneSum) / Math.Max(1, off.Sum(x => x.LaneTicks)), l1 = on.Sum(x => x.LaneSum) / Math.Max(1, on.Sum(x => x.LaneTicks));
			Check($"fewer levies stand on the line between the boss and the levy it hunts with the rule on: {F(l0)} -> {F(l1)} per tick", l0 < 0.05 || l1 <= l0 * 0.75);
			long d0 = off.Sum(x => x.Damage), d1 = on.Sum(x => x.Damage);
			Check($"the army keeps fighting while it moves: boss damage {d0} -> {d1} over the same ticks", d1 >= d0 * 0.6);
			Check($"nobody dies for it: deaths {off.Sum(x => x.Deaths)} -> {on.Sum(x => x.Deaths)}", on.Sum(x => x.Deaths) <= off.Sum(x => x.Deaths));
			double pass12a = off.Sum(x => x.Pass12), pass12b = on.Sum(x => x.Pass12);
			Check($"charges passing within 12 tiles of the commander do not rise: {pass12a} -> {pass12b}", pass12b <= pass12a + 1);
			double legsPerHunted = on.Sum(x => x.Legs) / Math.Max(1.0, on.Sum(x => x.PairTicks) / 60.0);
			Check($"no oscillation: {F(legsPerHunted)} direction changes per hunted-levy second", legsPerHunted <= 0.5);
			Check($"the decoy moves and the lane sidestep moves are used (decoy steps {on.Sum(x => x.DecoySteps)}, lane steps {on.Sum(x => x.LaneSteps)}); with the rule off none are", on.Sum(x => x.DecoySteps) > 0 && off.Sum(x => x.DecoySteps) == 0 && off.Sum(x => x.LaneSteps) == 0);

			if (ModLoader.HasMod("CalamityMod"))
			{
				var boxed = new List<Result>();
				for (int s = 0; s < Math.Min(seeds, 2); s++) boxed.Add(RunArm(7000 + s * 31, 1, ticks, true));
				int seen = boxed.Sum(x => x.GoalsSeen), outside = boxed.Sum(x => x.GoalsOutside);
				Note($"inside a Calamity arena box (44 tiles each side): decoy goals seen {seen}, outside the box {outside}, smallest margin {F(boxed.Min(x => x.MinGoalMargin) / 16.0)} tiles");
				Check("in an arena box no decoy goal is set outside it", seen > 0 && outside == 0);
			}
		}

		private static void Report(string label, List<Result> off, List<Result> on)
		{
			void Line(string n, List<Result> rs)
			{
				var dc = rs.SelectMany(x => x.DC).ToList(); var dk = rs.SelectMany(x => x.DK).ToList();
				Note($"{label}: rule {n}: hunted levy distance from the commander (tiles) mean {F(Mean(dc))} p25 {F(Pct(dc, .25))} median {F(Pct(dc, .5))} p75 {F(Pct(dc, .75))} p90 {F(Pct(dc, .9))}; from the other levies' centroid mean {F(Mean(dk))} p25 {F(Pct(dk, .25))} median {F(Pct(dk, .5))} p75 {F(Pct(dk, .75))} p90 {F(Pct(dk, .9))}; hunted ticks {rs.Sum(x => x.PairTicks)}");
				Note($"{label}: rule {n}: two-second hunts {rs.Sum(x => x.Episodes)}, distance from the commander changes by {F(Mean(rs.SelectMany(x => x.Growth)))} tiles, from the centroid by {F(Mean(rs.SelectMany(x => x.GrowthK)))} tiles; others on the boss-to-levy line {F(rs.Sum(x => x.LaneSum) / Math.Max(1, rs.Sum(x => x.LaneTicks)))} per tick over {rs.Sum(x => x.LaneTicks)} ticks; charge passes within 12 / 20 tiles of the commander {rs.Sum(x => x.Pass12)} / {rs.Sum(x => x.Pass20)} over {rs.Sum(x => x.Ticks)} boss ticks; boss damage {rs.Sum(x => x.Damage)}; deaths {rs.Sum(x => x.Deaths)}; direction changes {rs.Sum(x => x.Legs)}, holds {rs.Sum(x => x.HeldTicks)}, decoy steps {rs.Sum(x => x.DecoySteps)}, lane steps {rs.Sum(x => x.LaneSteps)}");
			}
			Line("off", off); Line("on", on);
		}
	}
}
