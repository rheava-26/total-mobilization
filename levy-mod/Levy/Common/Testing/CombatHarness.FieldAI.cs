using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
	/// FIELD AI SCENARIOS (LEVY_HARNESS_SCENARIO=fieldai, LEVY_FIELD_SPEC = comma list of: volley, charge, beam, fallcatch, slope, fall, cost; default all). Measurement only.
	/// Each prints "FIELDAI-..." lines: dodge trials run three times (mode 0 = no dodging, 1 = the old closest-approach flinch, 2 = threat-field), fall-catch with and without a hook,
	/// the slope trials on the planner alone (synthetic map), fall damage per height and gear, and the army cost at N=40. The commander's state fingerprint is compared before and after.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class FaTrial { public string Name; public int Ticks; public Action Setup; public Action<int> Step; public Func<string> Report; }
		private readonly List<FaTrial> _fa = new();
		private int _faI = -1, _faTick; private string _faFpBefore;
		private int _faFy;
		private readonly List<SoldierNPC> _faSol = new();
		private int _faStandin = -1;
		private int _faFired;
		private Vector2? _faCmdPos; private int _faCmdGrap; private float _faCmdGrav = 1f;

		private bool BeginFieldAi()
		{
			string spec = Environment.GetEnvironmentVariable("LEVY_FIELD_SPEC");
			var want = new HashSet<string>((string.IsNullOrWhiteSpace(spec) ? "volley,charge,beam,fallcatch,follow,slope,fall,cost" : spec).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
			float n = Main.maxTilesX / 4200f; n *= n;
			int band = (int)(60f + 10f * n + Main.worldSurface / 6.0) + 2;
			_faFy = Math.Clamp(band + 75, 100, Main.maxTilesY - 120);
			Mod.Logger.Info($"FIELDAI: world {Main.maxTilesX}x{Main.maxTilesY} floorRow={_faFy} spaceBand<{band}");
			if (want.Contains("slope")) FaSlope();
			foreach (int mode in new[] { 0, 1, 2 })
			{
				if (want.Contains("volley")) _fa.Add(FaVolley(mode));
				if (want.Contains("charge")) { _fa.Add(FaCharge(mode, false)); _fa.Add(FaCharge(mode, true)); }
				if (want.Contains("beam")) { _fa.Add(FaBeam(mode, 0f)); _fa.Add(FaBeam(mode, 0.0046f)); }
			}
			if (want.Contains("fallcatch")) foreach (bool lava in new[] { true, false }) foreach (bool hook in new[] { false, true }) _fa.Add(FaCatch(lava, hook));
			if (want.Contains("follow")) { foreach (string kind in new[] { "gap", "gapswing", "climb" }) foreach (bool hook in new[] { false, true }) _fa.Add(FaFollow(kind, hook ? ItemID.GrapplingHook : 0)); _fa.Add(FaFollow("gapwide", ItemID.GrapplingHook)); _fa.Add(FaFollow("gapwide", ItemID.IvyWhip)); }
			if (want.Contains("fall")) { _fa.Add(FaFall(1)); _fa.Add(FaFall(0)); }
			if (want.Contains("real")) FaAddReal();
			if (want.Contains("hookterrain")) FaAddHookTerrain();
			if (want.Contains("lava")) FaAddLava();
			if (want.Contains("gravflip")) foreach (int fd in new[] { 1, 0 }) foreach (bool lavaFloor in new[] { false, true }) _fa.Add(FaGravFlip(fd, lavaFloor));
			if (want.Contains("cost")) foreach (int mode in new[] { 0, 1, 2 }) _fa.Add(FaCost(mode));
			_faI = -1; _faTick = 0;
			Mod.Logger.Info($"FIELDAI: {_fa.Count} trials queued");
			return true;
		}

		private void StepFieldAi()
		{
			if (_faI < 0) { _faI = 0; _faTick = -1; }
			if (_faI >= _fa.Count)
			{
				FaKeep(); string fpAfter = HooksLab.Fp(Main.player[0]);
				Mod.Logger.Info($"FIELDAI: commander fingerprint unchanged={_faFpBefore == fpAfter}" + (_faFpBefore == fpAfter ? "" : " BEFORE " + _faFpBefore + " AFTER " + fpAfter));
				SoldierNPC.DodgeMode = 2; SoldierNPC.FallDamageOverride = -1; SoldierNPC.HookShortcutMode = 1; SoldierNPC.MobilityMode = 1; HookPlanner.SlopeDragCost = 14f; _faReal = false; RealOn = false;
				Finish("fieldai complete");
				return;
			}
			FaTrial t = _fa[_faI];
			if (_faFpBefore == null && _faI == 0 && _faTick == 8) _faFpBefore = HooksLab.Fp(Main.player[0]);   // after the server has updated the stand-in once
			if (_faTick < 0)
			{
				ClearAll(); for (int q = 0; q < Main.maxNPCs; q++) { NPC hn = Main.npc[q]; if (hn.active && !hn.townNPC && !(hn.ModNPC is SoldierNPC)) hn.active = false; }
				_faSol.Clear(); _faStandin = -1; _faFired = 0; _faCmdPos = null; _faCmdGrap = 0; _faCmdGrav = 1f;
				FaArena();
				t.Setup?.Invoke();
				_faTick = 0; return;
			}
			FaKeep();
			t.Step?.Invoke(_faTick);
			if (++_faTick >= t.Ticks)
			{
				string rep = "";
				try { rep = t.Report(); } catch (Exception e) { rep = "report failed: " + e; }
				Mod.Logger.Info($"FIELDAI-{t.Name}: {rep}");
				_faI++; _faTick = -1;
			}
		}

		// ---- arena ----
		private void FaFill(int x0, int y0, int x1, int y1, bool solid, ushort type = TileID.GrayBrick)
		{
			for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++)
			{
				if (x < 5 || y < 5 || x >= Main.maxTilesX - 5 || y >= Main.maxTilesY - 5) continue;
				Tile t = Main.tile[x, y];
				t.HasTile = solid; t.WallType = 0; t.LiquidAmount = 0; t.TileFrameX = 0; t.TileFrameY = 0; t.Slope = Terraria.ID.SlopeType.Solid;
				if (solid) { t.TileType = type; t.BlockType = Terraria.ID.BlockType.Solid; } else t.TileType = 0;
			}
		}

		private void FaArena()
		{
			int cx = _cx, fy = _faFy;
			FaFill(cx - 110, fy - 85, cx + 110, fy - 1, false);
			FaFill(cx - 110, fy, cx + 110, fy + 25, true);
			FaFill(cx - 111, fy - 85, cx - 111, fy + 25, true); FaFill(cx + 111, fy - 85, cx + 111, fy + 25, true);
		}

		private void FaKeep()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = "LevyTester"; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
			p.position = _faCmdPos ?? new Vector2((_cx - 105) * 16f, _faFy * 16f - p.height); p.velocity = Vector2.Zero; p.grapCount = _faCmdGrap; p.gravDir = _faCmdGrav;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || i == _faStandin || _faReal) continue;
				if (!n.friendly || n.type == NPCID.TargetDummy) n.active = false;
			}
		}

		private SoldierNPC FaSpawn(float x, float bottomY, Action<SoldierRecord> gear = null)
		{
			var rec = MakeRecord("Pvt. Field" + _faSol.Count);
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold; rec.HoldFire = true; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			gear?.Invoke(rec);
			RosterSystem.Instance.Regulars.Add(rec);
			int idx = RosterSystem.Instance.SpawnBody(rec, new Vector2(x, bottomY));
			if (idx < 0 || !(Main.npc[idx].ModNPC is SoldierNPC b)) throw new Exception("spawn failed");
			b.NPC.Bottom = new Vector2(x, bottomY); b.NPC.velocity = Vector2.Zero;
			b.RefreshStats();
			_faSol.Add(b);
			return b;
		}

		private static void FaHeal(SoldierNPC b) { if (b.NPC.active) { b.NPC.life = b.NPC.lifeMax; b.Record.CurrentHp = b.NPC.lifeMax; } }
		private string FaCounters() { int h = _faSol.Sum(b => b.HitsTaken), m = _faSol.Sum(b => b.DodgeManeuvers), s = _faSol.Sum(b => b.DodgeSteps), j = _faSol.Sum(b => b.DodgeJumps), d = _faSol.Sum(b => b.DodgeDashes), dr = _faSol.Sum(b => b.DodgeDrops); return $"hitsTaken={h} maneuvers={m} (step={s} jump={j} dash={d} drop={dr}) stayedPut={_faSol.Sum(b => b.DodgeStays)} scans={_faSol.Sum(b => b.DodgeScansDone)} snapshots={ThreatField.Builds}"; }

		private int FaShoot(Vector2 from, Vector2 to, float speed, int dmg = 40)
		{
			Vector2 v = to - from; v.Normalize(); v *= speed;
			int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyField"), from, v, ProjectileID.DeathLaser, dmg, 0f, Main.myPlayer);
			if (i < 0 || i >= Main.maxProjectiles) return -1;
			Projectile p = Main.projectile[i]; p.hostile = true; p.friendly = false; p.timeLeft = 200; p.tileCollide = false;
			_faFired++;
			return i;
		}

		// ---- volley ----
		private FaTrial FaVolley(int mode)
		{
			const int N = 4, Ticks = 520;
			return new FaTrial
			{
				Name = "VOLLEY", Ticks = Ticks,
				Setup = () => { SoldierNPC.DodgeMode = mode; for (int k = 0; k < N; k++) FaSpawn(_cx * 16f + (k - 2) * 12 * 16f + 8f, _faFy * 16f); },
				Step = t =>
				{
					foreach (var b in _faSol) FaHeal(b);
					if (t >= 40 && t % 26 == 0 && t < Ticks - 60)
					{
						int pat = (t / 26) % 3;
						foreach (var b in _faSol)
						{
							Vector2 c = b.NPC.Center;
							if (pat == 0) FaShoot(c + new Vector2(-340f, 0f), c, 8f);                        // flat, body height, from the left
							else if (pat == 1) FaShoot(c + new Vector2(-180f, -300f), c, 9f);                // steep, from above-left
							else { FaShoot(c + new Vector2(300f, -120f), c, 8f); FaShoot(c + new Vector2(-300f, -120f), c, 8f); } // a crossfire
						}
					}
				},
				Report = () => $"mode={mode} ({new[] { "none", "old flinch", "threat field" }[mode]}) projectilesFired={_faFired} {FaCounters()}",
			};
		}

		// ---- charging boss stand-in ----
		private FaTrial FaCharge(int mode, bool vertical)
		{
			const int Cycle = 150, Cycles = 5;
			Vector2 dir = Vector2.Zero; float minD = float.MaxValue, maxV = 0f; int dmgSeen = 0; bool c0dbg = true, c0first = true;
			return new FaTrial
			{
				Name = "CHARGE", Ticks = Cycle * Cycles + 20,
				Setup = () =>
				{
					SoldierNPC.DodgeMode = mode; FaSpawn(_cx * 16f + 8f, _faFy * 16f);
					int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyField"), _cx * 16, _faFy * 16 - 2000, ModContent.NPCType<HarnessCharger>());
					NPC n = Main.npc[i]; _faStandin = i;
					n.lifeMax = n.life = 1_000_000; n.defense = 0; n.damage = 0; n.knockBackResist = 0f; n.width = 80; n.height = 80; n.aiStyle = -1; n.noGravity = true; n.noTileCollide = true; n.friendly = false; n.value = 0f; n.boss = false; n.dontTakeDamage = false;
				},
				Step = t =>
				{
					var b = _faSol[0]; FaHeal(b);
					NPC n = Main.npc[_faStandin]; n.timeLeft = 3600;
					if (n.damage > 0) { dmgSeen++; minD = Math.Min(minD, Vector2.Distance(n.Center, b.NPC.Center)); maxV = Math.Max(maxV, n.velocity.Length()); }
					int c = t % Cycle;
					if (c == 0) { n.damage = 0; n.velocity = Vector2.Zero; Vector2 s = b.NPC.Center; n.width = 80; n.height = vertical ? 80 : 44; n.Center = vertical ? s + new Vector2(0f, -520f) : new Vector2(s.X - 520f, _faFy * 16f - 22f); }
					else if (c == 40) { dir = b.NPC.Center - n.Center; dir.Normalize(); n.damage = 90; }          // the wind-up is over: it commits to the line it sees now
					else if (c > 40 && c < 120) n.velocity = Vector2.Clamp(n.velocity + dir * 0.9f, new Vector2(-16f), new Vector2(16f));
					else if (c >= 120) { n.velocity = Vector2.Zero; n.damage = 0; n.Center = new Vector2(_cx * 16f, _faFy * 16f - 3000f); }
				},
				Report = () => $"mode={mode} kind={(vertical ? "vertical dive" : "horizontal charge")} charges={Cycles} peakBossSpeed={maxV:0.0} closestApproach={minD / 16f:0.0}T {FaCounters()}",
			};
		}

		// ---- beam ----
		private FaTrial FaBeam(int mode, float omega)
		{
			const int Ticks = 560, Period = 240;
			return new FaTrial
			{
				Name = "BEAM", Ticks = Ticks,
				Setup = () => { SoldierNPC.DodgeMode = mode; FaSpawn(_cx * 16f + 8f, _faFy * 16f); },
				Step = t =>
				{
					var b = _faSol[0]; FaHeal(b);
					if (t % Period == 20 && t < Ticks - 200)
					{
						float h = 28f * 16f;
						// static: straight down onto the soldier. Sweeping: starts 6 tiles to the left of the soldier and moves right.
						Vector2 origin = new Vector2(b.NPC.Center.X + (omega == 0f ? 0f : -6f * 16f - (h * 0f)), _faFy * 16f - h - 20f);
						float ang0 = omega == 0f ? MathHelper.PiOver2 : MathHelper.PiOver2 + (float)Math.Atan(0.0);
						Vector2 toFloorStart = new Vector2(b.NPC.Center.X - 6f * 16f, _faFy * 16f) - new Vector2(b.NPC.Center.X - 6f * 16f - 0f, 0f);
						if (omega != 0f) { origin = new Vector2(b.NPC.Center.X - 6f * 16f, _faFy * 16f - h); ang0 = MathHelper.PiOver2; }
						int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyField"), origin, ang0.ToRotationVector2(), ModContent.ProjectileType<HarnessBeam>(), 35, 0f, Main.myPlayer);
						if (i >= 0 && i < Main.maxProjectiles) { Projectile p = Main.projectile[i]; p.ai[0] = -omega; p.localAI[1] = h + 120f; p.timeLeft = 200; _faFired++; }
					}
				},
				Report = () => $"mode={mode} beam={(omega == 0f ? "static line" : "sweeping line")} beams={_faFired} {FaCounters()}",
			};
		}

		// ---- fall over lava / deep drop, caught by a hook ----
		private FaTrial FaCatch(bool lava, bool hook)
		{
			const int Ticks = 420, UpTiles = 40;
			bool shoved = false; bool wasLava = false; int minLife = int.MaxValue; int casts0 = 0; float peakRise = 0f; int tick0Landed = -1;
			return new FaTrial
			{
				Name = "FALLCATCH", Ticks = Ticks,
				Setup = () =>
				{
					SoldierNPC.DodgeMode = 2; SoldierNPC.FallDamageOverride = 1;
					int cx = _cx, fy = _faFy, ledge = fy - UpTiles;
					FaFill(cx - 60, ledge, cx - 1, ledge + 2, true);                                  // the ledge, 40 tiles over the floor
					FaFill(cx - 60, ledge - 15, cx + 40, ledge - 13, true);                         // a ceiling 13 tiles over the ledge
					if (lava) for (int x = cx - 30; x <= cx + 30; x++) for (int y = fy - 5; y < fy; y++) { Tile t = Main.tile[x, y]; t.LiquidAmount = 255; t.LiquidType = Terraria.ID.LiquidID.Lava; }
					var b = FaSpawn(cx * 16f - 6f, ledge * 16f, r => { if (hook) { var it = new Item(); it.SetDefaults(ItemID.GrapplingHook); r.Gear.Hook = it; r.Gear.SetLocked(SoldierGear.SlotHook, true); } });
					b.RefreshStats(); casts0 = b.HkCasts;
				},
				Step = t =>
				{
					var b = _faSol[0]; NPC n = b.NPC;
					if (!n.active) return;
					if (t >= 12 && !shoved && n.Center.X < _cx * 16f + 14f) { n.velocity.X = 3.5f; n.netUpdate = true; } else if (t >= 12) shoved = true;   // a shove until the body is past the edge
					if (n.lavaWet) wasLava = true;
					minLife = Math.Min(minLife, n.life);
					float rise = ((_faFy - UpTiles) * 16f - n.Bottom.Y) / 16f; peakRise = Math.Max(peakRise, rise);
					if (t > 200 && n.velocity.Y == 0f && tick0Landed < 0) tick0Landed = t;
				},
				Report = () =>
				{
					var b = _faSol[0]; NPC n = b.NPC; bool alive = n.active && n.life > 0;
					float rowsBelowLedge = alive ? (n.Bottom.Y / 16f - (_faFy - UpTiles)) : -1f;
					return $"floor={(lava ? "lava" : "stone 40 tiles down")} hook={(hook ? "yes" : "no")} alive={alive} touchedLava={wasLava} finalBottom={(alive ? rowsBelowLedge.ToString("0.0") : "-")} tiles below ledge (0 = back on the ledge) hp={(alive ? n.life : 0)}/{n.lifeMax} lowestHp={minLife} fallHits={b.FallHits} fallDamage={b.FallDamageTotal} lastFallTiles={b.LastFallTiles} catchesStarted={b.FallCatches} hookCasts={b.HkCasts - casts0} latches={b.HkLatches} landings={b.HkLandings} aborts={b.HkAborts}";
				},
			};
		}

		// ---- follow the commander across a gap / up a wall, with the hook the levy has equipped ----
		private FaTrial FaFollow(string kind, int hookItem)
		{
			const int Ticks = 900; bool hook = hookItem != 0;
			int arrive = -1, casts0 = 0; float maxStep = 0f; Vector2 last = Vector2.Zero; int minLife = int.MaxValue; int pitTicks = 0; float bestD = float.MaxValue;
			return new FaTrial
			{
				Name = "FOLLOW", Ticks = Ticks,
				Setup = () =>
				{
					SoldierNPC.DodgeMode = 2; SoldierNPC.FallDamageOverride = 1;
					int cx = _cx, fy = _faFy;
					float cmdX, cmdBottom;
					if (kind == "climb")
					{
						FaFill(cx + 4, fy - 8, cx + 60, fy - 1, true);                // an 8-tile wall, the commander stands on top
						FaFill(cx - 4, fy - 8 - 8, cx + 50, fy - 8 - 6, true);        // a cave ceiling 6 tiles over the top
						cmdX = (cx + 20) * 16f; cmdBottom = (fy - 8) * 16f;
					}
					else
					{
						int pitR = kind == "gapwide" ? cx + 17 : cx + 11;
						FaFill(cx - 6, fy, pitR, fy + 14, false);                     // an 18-wide (gapwide: 24-wide), 14-deep pit
						FaFill(cx - 14, fy - 10, cx + 40, fy - 8, true);              // a cave ceiling 8 tiles over the floor
						cmdX = (cx + (kind == "gapwide" ? 34 : 28)) * 16f; cmdBottom = fy * 16f;
					}
					_faCmdPos = new Vector2(cmdX - 10f, cmdBottom - Main.player[0].height); _faCmdGrap = kind == "gapswing" ? 1 : 0;
					var b = FaSpawn((cx - 12) * 16f, fy * 16f, r =>
					{
						r.Order = SoldierOrder.Follow;
						if (hook) { var it = new Item(); it.SetDefaults(hookItem); r.Gear.Hook = it; r.Gear.SetLocked(SoldierGear.SlotHook, true); }
					});
					b.RefreshStats(); casts0 = b.HkCasts; last = b.NPC.Center;
				},
				Step = t =>
				{
					var b = _faSol[0]; NPC n = b.NPC; if (!n.active) return;
					if ((t % 60 == 0 || (t < 150 && t % 6 == 0 && kind == "gap" && hook)) && Environment.GetEnvironmentVariable("LEVY_FADBG") == "1") Mod.Logger.Info($"FOLLOWDBG {kind} hook={hook} t={t} vx={n.velocity.X:0.0} px={n.Bottom.X / 16f:0.00} " + b.HookDbgState);
					minLife = Math.Min(minLife, n.life);
					maxStep = Math.Max(maxStep, Vector2.Distance(n.Center, last) / 16f); last = n.Center;
					Vector2 cp = Main.player[0].Bottom;
					float d = Vector2.Distance(n.Bottom, cp) / 16f; bestD = Math.Min(bestD, d);
					if (n.Bottom.Y > _faFy * 16f + 16f && kind != "climb") pitTicks++;
					if (arrive < 0 && d < 5f && n.velocity.Y == 0f) arrive = t;
				},
				Report = () =>
				{
					var b = _faSol[0]; NPC n = b.NPC; bool alive = n.active && n.life > 0;
					float d = alive ? Vector2.Distance(n.Bottom, Main.player[0].Bottom) / 16f : -1f;
					return $"scenario={kind} hook={(hook ? Lang.GetItemNameValue(hookItem) : "none")} reachedCommanderAtTick={(arrive >= 0 ? arrive.ToString() : "never")} finalDistTiles={d:0.0} closestTiles={bestD:0.0} ticksInPit={pitTicks} biggestJumpInOneTickTiles={maxStep:0.0} (teleport if >20) hookCasts={b.HkCasts - casts0} latches={b.HkLatches} landings={b.HkLandings} aborts={b.HkAborts} lowestHp={minLife}/{n.lifeMax}";
				},
			};
		}

		// ---- fall damage ----
		private FaTrial FaFall(int on)
		{
			var cases = new List<(string name, int h, Action<SoldierRecord> gear, int water, int web)>();
			foreach (int h in new[] { 20, 25, 26, 30, 40, 55 }) cases.Add(("plain", h, null, 0, 0));
			Action<SoldierRecord> acc(int item) => r => { var it = new Item(); it.SetDefaults(item); r.Gear.Accessories[0] = it; };
			cases.Add(("LuckyHorseshoe", 40, acc(ItemID.LuckyHorseshoe), 0, 0));
			cases.Add(("AngelWings", 40, acc(ItemID.AngelWings), 0, 0));
			cases.Add(("FrogLeg", 40, acc(ItemID.FrogLeg), 0, 0));
			cases.Add(("WaterLanding", 40, null, 8, 0));
			cases.Add(("CobwebNearFloor", 40, null, 0, 3));
			return new FaTrial
			{
				Name = on == 1 ? "FALL(config on)" : "FALL(config off)", Ticks = 330,
				Setup = () =>
				{
					SoldierNPC.DodgeMode = 2; SoldierNPC.FallDamageOverride = on;
					for (int i = 0; i < cases.Count; i++)
					{
						var c = cases[i]; int x = _cx - 50 + i * 5;
						if (c.water > 0) for (int y = _faFy - c.water; y < _faFy; y++) { Tile t = Main.tile[x, y]; t.LiquidAmount = 255; t.LiquidType = Terraria.ID.LiquidID.Water; }
						if (c.web > 0) FaFill(x, _faFy - c.web, x, _faFy - c.web, true, TileID.Cobweb);
						var b = FaSpawn(x * 16f + 8f, (_faFy - c.h) * 16f, c.gear);
						b.NPC.life = b.NPC.lifeMax;
					}
				},
				Step = t => { },
				Report = () =>
				{
					var sb = new StringBuilder("rule: free 25 tiles (+gear), then 10 per tile; wings/horseshoe/water/web spare it.");
					for (int i = 0; i < cases.Count; i++)
					{
						var b = _faSol[i]; var c = cases[i];
						int expected = c.name == "plain" ? Math.Max(0, (c.h - 25) * 10) : -1;
						sb.Append($" | {c.name} h={c.h} lost={b.FallDamageTotal} (raw rule {(expected >= 0 ? expected.ToString() : "n/a")}) tilesFallen={b.LastFallTiles} def={b.NPC.defense}");
					}
					return sb.ToString();
				},
			};
		}

		// ---- cost at N=40 ----
		private FaTrial FaCost(int mode)
		{
			int N = int.TryParse(Environment.GetEnvironmentVariable("LEVY_FA_N"), out int envN) ? envN : 40; float spacing = float.TryParse(Environment.GetEnvironmentVariable("LEVY_FA_SPACING"), out float envS) ? envS : 5f; const int Ticks = 360;
			long dodge0 = 0, build0 = 0; int builds0 = 0; System.Diagnostics.Stopwatch wall = new();
			return new FaTrial
			{
				Name = "COST", Ticks = Ticks,
				Setup = () => { SoldierNPC.DodgeMode = mode; for (int k = 0; k < N; k++) FaSpawn(_cx * 16f + (k - N / 2) * spacing * 16f + 8f, _faFy * 16f); build0 = ThreatField.BuildTs; builds0 = ThreatField.Builds; wall.Restart(); },
				Step = t =>
				{
					foreach (var b in _faSol) FaHeal(b);
					if (t % 12 == 0 && t < Ticks - 60)
						for (int k = 0; k < _faSol.Count; k += 2) { Vector2 c = _faSol[k].NPC.Center; FaShoot(c + new Vector2(k % 4 == 0 ? -300f : 300f, -160f), c, 8f); }
					// a standing crowd of old projectiles also in the world: 300 harmless slow hostile bolts far away
					if (t == 1) for (int i = 0; i < 200; i++) { int pi = FaShoot(new Vector2(_cx * 16f - 100 * 16f + i * 30f, _faFy * 16f - 70 * 16f), new Vector2(_cx * 16f + 100 * 16f, _faFy * 16f - 70 * 16f), 3f, 1); if (pi >= 0) Main.projectile[pi].timeLeft = 330; }
				},
				Report = () =>
				{
					wall.Stop();
					long dts = _faSol.Sum(b => b.DodgeTs); double freq = System.Diagnostics.Stopwatch.Frequency;
					double dodgeMs = dts * 1000.0 / freq / Ticks, buildMs = (ThreatField.BuildTs - build0) * 1000.0 / freq / Ticks;
					return $"N={N} spacing={spacing}T mode={mode} dodgeMsPerTick(whole army)={dodgeMs:0.0000} snapshotBuildMsPerTick={buildMs:0.0000} scans={_faSol.Sum(b => b.DodgeScansDone)} {FaCounters()}";
				},
			};
		}

		// ---- slope: the planner alone, on a synthetic map ----
		private sealed class SynthWorld : IHookWorld
		{
			public readonly bool[,] S = new bool[100, 70], Sl = new bool[100, 70];
			public bool Solid(int x, int y) => x < 0 || y < 0 || x >= 100 || y >= 70 || S[x, y];
			public bool Platform(int x, int y) => false;
			public bool Latchable(int x, int y) => x >= 0 && y >= 0 && x < 100 && y < 70 && S[x, y];
			public bool Lava(int x, int y) => false;
			public bool Sloped(int x, int y) => x >= 0 && y >= 0 && x < 100 && y < 70 && Sl[x, y];
			public bool ClosedDoor(int x, int y) => false;
		}

		private void FaSlope()
		{
			var hook = new HookParams { ReachPx = 18 * 16f, PullPx = 11f, Count = 1, Kind = HookKind.Normal, BodyW = 20, BodyH = 42, JumpSpeed = 5.01f, Gravity = 0.3f, Rays = LevyConstants.Hooks.FanRays, ReleaseCandidates = LevyConstants.Hooks.ReleaseCandidates, MaxAnchors = LevyConstants.Hooks.MaxAnchorsSimulated, MaxHooks = 1 };
			foreach (string geo in new[] { "sloped hill face (relies on dragging up the slope)", "vertical cliff face (control)", "sloped ceiling overhead (hook only, body never touches it)" })
			{
				foreach (float cost in new[] { 0f, 14f })
				{
					var w = new SynthWorld();
					for (int x = 0; x < 100; x++) for (int y = 56; y < 70; y++) w.S[x, y] = true;      // floor at row 56
					Vector2 start = new Vector2((geo.StartsWith("sloped hill") ? 14 : 10) * 16f + 8f, 56 * 16f), goal;
					if (geo.StartsWith("sloped hill"))
					{
						for (int x = 16; x < 40; x++) { int top = x < 24 ? 55 - (x - 16) : 48; for (int y = top; y < 56; y++) w.S[x, y] = true; if (x < 24) w.Sl[x, top] = true; }
						goal = new Vector2(30 * 16f + 8f, 48 * 16f);
					}
					else if (geo.StartsWith("vertical"))
					{
						for (int x = 16; x < 40; x++) for (int y = 48; y < 56; y++) w.S[x, y] = true;
						goal = new Vector2(24 * 16f + 8f, 48 * 16f);
					}
					else
					{
						for (int x = 16; x < 40; x++) for (int y = 48; y < 56; y++) w.S[x, y] = true;
						for (int x = 8; x < 40; x++) for (int y = 36; y < 40; y++) { w.S[x, y] = true; if (y == 39) w.Sl[x, y] = true; }
						goal = new Vector2(24 * 16f + 8f, 48 * 16f);
					}
					HookPlanner.SlopeDragCost = cost;
					float nowDist = Vector2.Distance(start, goal) + Math.Abs(start.Y - goal.Y) * 0.5f;
					HookPlan plan = HookPlanner.Plan(w, start, hook, goal, HookGoalKind.Ground, nowDist);
					bool beats = plan != null && plan.Beats(LevyConstants.Hooks.ArriveTiles * 16f, LevyConstants.Hooks.MinGainTiles * 16f);
					string rejudged = "";
					if (cost == 0f && plan != null)
					{
						HookPlanner.SlopeDragCost = 14f;
						HookPlan re = HookPlanner.Replan(w, start, hook, plan.Anchors, goal, HookGoalKind.Ground, nowDist);
						rejudged = re == null ? " | same anchors judged slope-aware: no plan" : $" | same anchors judged slope-aware: slopeDragTicks={re.SlopeTicks} casts={(re.Beats(LevyConstants.Hooks.ArriveTiles * 16f, LevyConstants.Hooks.MinGainTiles * 16f) ? "YES" : "no")} landing=({re.Landing.X / 16f:0.0},{re.Landing.Y / 16f:0.0}) goalDist={re.GoalDist / 16f:0.0}T";
						HookPlanner.SlopeDragCost = 0f;
					}
					Mod.Logger.Info($"FIELDAI-SLOPE: geometry=[{geo}] planner={(cost > 0 ? "slope-aware" : "slope-blind (old)")} planFound={(plan != null)} casts={(beats ? "YES" : "no")} " + (plan == null ? "" : $"slopeDragTicks={plan.SlopeTicks} landing=({plan.Landing.X / 16f:0.0},{plan.Landing.Y / 16f:0.0}) goalDist={plan.GoalDist / 16f:0.0}T gain={plan.Gain / 16f:0.0}T score={plan.Score:0}") + rejudged);
				}
			}
			HookPlanner.SlopeDragCost = 14f;
		}
	}

	/// <summary>Harness stand-in for a charging boss: a hostile body with no AI of its own; the scenario sets its velocity.</summary>
	public class HarnessCharger : ModNPC
	{
		public override string Texture => "Terraria/Images/NPC_" + NPCID.BlueSlime; // a texture needs a real NPC type: GreenSlime is netID -3 (no "NPC_-3" file)
		public override void SetDefaults() { NPC.width = 80; NPC.height = 80; NPC.damage = 0; NPC.defense = 0; NPC.lifeMax = 1_000_000; NPC.life = 1_000_000; NPC.aiStyle = -1; NPC.noGravity = true; NPC.noTileCollide = true; NPC.knockBackResist = 0f; NPC.friendly = false; NPC.value = 0f; NPC.dontTakeDamage = false; }
		public override bool CheckActive() => false;
		public override bool CheckDead() => false;
	}

	/// <summary>Harness stand-in for a deathray: a hostile line that turns at ai[0] radians per tick and reaches localAI[1] px. Implements the same IThreatBeam any modded beam can.</summary>
	public class HarnessBeam : ModProjectile, IThreatBeam
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeathLaser;
		public float BeamLength(Projectile p) => p.localAI[1];
		public float BeamWidth(Projectile p) => 24f;
		public override void SetDefaults() { Projectile.width = 16; Projectile.height = 16; Projectile.hostile = true; Projectile.friendly = false; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 200; Projectile.alpha = 255; Projectile.ignoreWater = true; }
		public override bool ShouldUpdatePosition() => false;
		public override void AI() { Projectile.velocity = (Projectile.velocity.ToRotation() + Projectile.ai[0]).ToRotationVector2(); }
		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float f = 0f;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.velocity * Projectile.localAI[1], BeamWidth(Projectile), ref f);
		}
	}
}
