using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Flavor;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.Items;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ORDER OBEY (LEVY_HARNESS_SCENARIO=orderobey). Eve (2.0.43): "soldiers now refuse orders unless they die first". About 16 levies in four populations (reloaded from a saved roster, fresh and long-lived, killed and respawned,
	/// seated / idle) either fight a stand-in boss (LEVY_OO_ROUND=fight) or stand about calm (calm). Every order the radio offers is sent through the real packet handler (LevyPackets.Handle, the server path; sender = player 0), one after another, and each levy
	/// is measured for 2 s: did its record take the order, did its body (HoldPosition, State) follow, did it move. LEVY_OO_MODE: radio (Command Radio in the pack, atBarracks true, what the radial sends) | radio_near (radio, atBarracks false) | plain (no radio, atBarracks false) | barracks (atBarracks true, no radio).
	/// Log lines start "HARNESS-ORDEROBEY:"; the verdict line is "HARNESS-ORDEROBEY: RESULT pass=N fail=M".
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class OoL { public string Group; public SoldierRecord Rec; public float X0, DB0; public SoldierState S0; public bool Seated0, Reach, CmdGone; public int Tgt0; public int FirstOk = -1; public Vector2 HoldAt; }
		private readonly List<OoL> _oo = new List<OoL>();
		private int _ooTick, _ooStep, _ooPass, _ooFail, _ooStage; private bool _ooStarted; private BarracksTE _ooTe; private int _ooBoss = -1; private int _ooStageTick;
		private string _ooRound, _ooMode; private bool _ooRadio, _ooAtBarracks;
		private void OoLog(string s) => Mod.Logger.Info("HARNESS-ORDEROBEY: " + s);
		private static readonly (string name, int kind, SoldierOrder order)[] OoOrders =
		{
			("Hold", 0, SoldierOrder.Hold), ("Follow", 0, SoldierOrder.Follow), ("Return", 0, SoldierOrder.ReturnToBarracks), ("Follow", 0, SoldierOrder.Follow),
			("SearchDestroy", 0, SoldierOrder.SearchDestroy), ("Follow", 0, SoldierOrder.Follow), ("FreeRein", 0, SoldierOrder.FreeRein), ("Follow", 0, SoldierOrder.Follow),
			("HoldFire", 1, SoldierOrder.Follow), ("HoldFireOff", 1, SoldierOrder.Follow), ("SpreadOut", 2, SoldierOrder.Follow), ("CloseUp", 2, SoldierOrder.Follow), ("NormalRanks", 2, SoldierOrder.Follow),
		};
		private static int OoWait => int.TryParse(Environment.GetEnvironmentVariable("LEVY_OO_WAIT"), out int w) ? w : 0;
		private static bool OoBossFlag => Environment.GetEnvironmentVariable("LEVY_OO_BOSSFLAG") == "1";
		private static int OoBossDamage => int.TryParse(Environment.GetEnvironmentVariable("LEVY_OO_BOSSDMG"), out int d) ? d : 0;
		private static readonly int[] OoMixed = { ItemID.Handgun, ItemID.WoodenBow, ItemID.AmethystStaff, ItemID.CopperBroadsword, ItemID.CopperShortsword, ItemID.Spear, ItemID.SlimeStaff, ItemID.BlandWhip, ItemID.EnchantedBoomerang, ItemID.Rally, ItemID.BallOHurt, ItemID.ThrowingKnife, ItemID.Boomstick, ItemID.Flamethrower, ItemID.Shuriken, ItemID.Blowpipe, ItemID.WandofSparking, ItemID.Starfury, ItemID.IceBlade, ItemID.Sunfury };
		private int _ooGearN;
		private const int OoMeasureTicks = 120, OoSettleTicks = 240;

		private bool BeginOrderObey()
		{
			_ooTick = 0; _ooStep = 0; _ooPass = _ooFail = 0; _ooStarted = false; _ooStage = 0; _oo.Clear(); _ooBoss = -1;
			_ooRound = Environment.GetEnvironmentVariable("LEVY_OO_ROUND") ?? "fight"; _ooMode = Environment.GetEnvironmentVariable("LEVY_OO_MODE") ?? "radio";
			_ooRadio = _ooMode == "radio" || _ooMode == "radio_near"; _ooAtBarracks = _ooMode == "radio" || _ooMode == "barracks";
			return true;
		}

		private void OoPin()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 5000; p.statLife = 5000;
			p.position = new Vector2((_cx - 30) * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			if (_ooRadio) { if (!CommandRadio.Has(p)) p.inventory[3].SetDefaults(ModContent.ItemType<CommandRadio>()); }
			else for (int i = 0; i < 58; i++) if (p.inventory[i].type == ModContent.ItemType<CommandRadio>()) p.inventory[i].TurnToAir();
			if (_ooTe != null) _ooTe.Manpower = 99; // respawns need manpower in the home barracks
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (i != _ooBoss && n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
			if (Environment.GetEnvironmentVariable("LEVY_OO_NOAMMO") == "1" && _ooStage >= 3 && _ooStep == 0 && _ooTick - _ooStageTick < 2) // the pouches run dry just before the first order, as in a long fight far from the armory
				foreach (var r in RosterSystem.Instance.Regulars.Where(r => r.CommanderName == CommanderName)) { r.Gear.Ammo = new Item(); var b = OoBody(r); b?.NotifyGearChanged(); }
			string fi = Environment.GetEnvironmentVariable("LEVY_OO_FEARINJECT"); // "shaken" | "routed": every even-numbered levy is held at that fear every tick (a boss that keeps hitting them)
			if (!string.IsNullOrEmpty(fi) && _ooStage >= 3)
			{
				int k = 0;
				foreach (var r in RosterSystem.Instance.Regulars.Where(r => r.CommanderName == CommanderName))
				{
					if ((k++ & 1) != 0) continue;
					var b = OoBody(r); if (b == null || !b.IsAlive) continue;
					if (fi == "routed") { if (b.State != SoldierState.Routed) b.Fear_Rout(null, false); r.Fear = Math.Max(r.Fear, 80f); }
					else { r.Fear = Math.Max(r.Fear, 60f); }
				}
			}
			if (_ooBoss >= 0) { NPC b = Main.npc[_ooBoss]; if (b.active) { b.life = b.lifeMax = 2000000; b.timeLeft = 99999; } }
		}

		private SoldierRecord OoMake(string name, SoldierOrder order, Vector2? hold = null)
		{
			var rec = MakeRecord(name);
			var w = new Item(); w.SetDefaults(Environment.GetEnvironmentVariable("LEVY_OO_GEAR") == "mixed" ? OoMixed[_ooGearN++ % OoMixed.Length] : ItemID.Handgun);
			if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
			Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_OO_CRYSTALS"), out int lc)) rec.LifeCrystals = lc;
			rec.LoadoutLocked = true; rec.Order = order; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (hold.HasValue) rec.HoldPos = hold.Value;
			if (_ooTe != null) { rec.HomeBarracksId = _ooTe.ID; rec.HomePos = _ooTe.Position; }
			RosterSystem.Instance.Regulars.Add(rec);
			return rec;
		}

		private int OoSpawn(SoldierRecord rec, float dxTiles)
		{
			Vector2 at = new Vector2((_cx - 30 + dxTiles) * 16f, FloorY * 16f);
			int idx = RosterSystem.Instance.SpawnBody(rec, at);
			if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
			return idx;
		}

		private void OoChair(int x) { try { WorldGen.PlaceObject(x, FloorY - 1, TileID.Chairs, true, 0); } catch { } }

		/// <summary>SoldierNPC.Seated by reflection, so the same harness builds on older revisions that have no idle sitting.</summary>
		private static bool OoSeated(SoldierNPC b) { if (b == null) return false; var pi = typeof(SoldierNPC).GetProperty("Seated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); return pi != null && (bool)pi.GetValue(b); }
		private SoldierNPC OoBody(SoldierRecord r) => r.HasBody ? Main.npc[r.BodyIndex].ModNPC as SoldierNPC : null;

		private void StepOrderObey()
		{
			_ooTick++;
			try { StepOrderObeyBody(); }
			catch (Exception e) { OoLog("EXCEPTION " + e); _ooFail++; Finish("orderobey exception"); }
		}

		private void StepOrderObeyBody()
		{
			OoPin();
			var ros = RosterSystem.Instance;
			switch (_ooStage)
			{
				case 0: // arena + barracks + the RELOAD group created and spawned, then a save / load of the roster
					if (Environment.GetEnvironmentVariable("LEVY_OO_LOADED") == "1") // process 2: the roster came out of the world file the first process saved; nothing is built here
					{
						if (!_ooStarted) { _ooStarted = true; _ooStageTick = _ooTick; GuideSystem.Instance.Autonomy = true; _ooTe = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault(); OoLog($"world loaded: records={ros.Regulars.Count(r => r.CommanderName == CommanderName)} barracks={(_ooTe == null ? "none" : _ooTe.Position.ToString())} homes=[{string.Join(",", ros.Regulars.Where(r => r.CommanderName == CommanderName).Select(r => r.HomeBarracksId))}]"); if (_ooTe != null) _ooTe.Manpower = 99; return; }
						if (_ooTe != null) _ooTe.Manpower = 99;
						int want = ros.Regulars.Count(r => r.CommanderName == CommanderName && !r.Dead);
						if (_ooTick - _ooStageTick > 60 && want > 0 && ros.Regulars.Where(r => r.CommanderName == CommanderName && !r.Dead).All(r => r.HasBody)) { _ooStage = 1; _ooStageTick = _ooTick; OoLog("loaded roster deployed: " + want + " bodies"); }
						else if (_ooTick - _ooStageTick > 2400) { OoLog("loaded roster never deployed"); _ooFail++; Finish("orderobey: loaded roster not deployed"); }
						return;
					}
					if (!_ooStarted)
					{
						_ooStarted = true; ClearAll(); GuideSystem.Instance.Autonomy = true;
						foreach (var te in TileEntity.ByID.Values.OfType<BarracksTE>().ToList()) { TileEntity.ByPosition.Remove(te.Position); TileEntity.ByID.Remove(te.ID); }
						int bx = _cx - 95, fy = FloorY;
						try { WorldGen.PlaceObject(bx, fy - 1, ModContent.TileType<BarracksTile>(), true, 0); } catch { }
						_ooTe = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault();
						if (_ooTe == null) { try { ModContent.GetInstance<BarracksTE>().Place(bx - 1, fy - 3); _ooTe = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault(); } catch { } }
						if (_ooTe == null) OoLog("NO BARRACKS could be placed");
						else _ooTe.Manpower = 99;
						for (int k = 0; k < 4; k++) { var rec = OoMake("Pvt. Reload" + k, SoldierOrder.Follow); OoSpawn(rec, 2 + k); }
						_ooStageTick = _ooTick; return;
					}
					if (_ooTick - _ooStageTick == 120)
					{
						// the world reload: save the roster, drop every body, load it back. Bodies come back through the normal deployment loop, exactly as after a real load.
						if (Environment.GetEnvironmentVariable("LEVY_OO_SAVEWORLD") == "1") // process 1: write the roster and the barracks into the real world file, then stop
						{
							OoLog($"saving the world: records={ros.Regulars.Count} barracks={(_ooTe == null ? "none" : _ooTe.Position.ToString())} homes=[{string.Join(",", ros.Regulars.Select(r => r.HomeBarracksId))}] file={Main.worldPathName}");
							Terraria.IO.WorldFile.SaveWorld(false, false);
							OoLog("world saved"); Finish("orderobey: world saved"); return;
						}
						var tag = new TagCompound(); ros.SaveWorldData(tag);
						for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false;
						ros.LoadWorldData(tag); ros.OnWorldLoad();
						if (_ooTe != null) _ooTe.Manpower = 99;
						OoLog($"roster reloaded: {ros.Regulars.Count} records, bodies={ros.Regulars.Count(r => r.HasBody)}");
					}
					if (_ooTick - _ooStageTick > 120 && ros.Regulars.Count(r => r.CommanderName == CommanderName && r.HasBody) >= 4) { _ooStage = 1; _ooStageTick = _ooTick; OoLog("reloaded group deployed"); }
					else if (_ooTick - _ooStageTick > 1500) { OoLog("reloaded group never deployed"); _ooFail++; Finish("orderobey: reload deploy failed"); }
					return;
				case 1: // fresh fighters, hold-sitters, follow-sitters, then kill + respawn group
					if (_ooTick - _ooStageTick == 1)
					{
						string armyFile = Environment.GetEnvironmentVariable("LEVY_OO_ARMY");
						if (!string.IsNullOrEmpty(armyFile)) { var kd = KitLoad(armyFile); var specs = KitSpecs(kd); var crun = new CrRun { Order = "follow" }; for (int k = 0; k < specs.Count; k++) CrSpawnLevy(specs[k], crun, k, specs.Count, new Vector2((_cx - 30) * 16f, FloorY * 16f)); OoLog("army loaded from " + armyFile + ": " + specs.Count + " levies"); }
						else for (int k = 0; k < 5; k++) { var rec = OoMake("Pvt. Fresh" + k, SoldierOrder.Follow); OoSpawn(rec, -2 + k); }
						for (int k = 0; k < 2; k++) { int px = _cx + 58 + 3 * k; OoChair(px); var rec = OoMake("Pvt. HoldSit" + k, SoldierOrder.Hold, new Vector2(px * 16f + 8f, FloorY * 16f)); OoSpawn(rec, px - (_cx - 30)); }
						if (_ooRound == "calm") for (int k = 0; k < 3; k++) { OoChair(_cx - 24 + 2 * k); var rec = OoMake("Pvt. FolSit" + k, SoldierOrder.Follow); OoSpawn(rec, -4 + k * 2); }
						for (int k = 0; k < 4; k++) { var rec = OoMake("Pvt. Dead" + k, SoldierOrder.Follow); OoSpawn(rec, 6 + k); }
						return;
					}
					if (_ooTick - _ooStageTick == 300)
					{
						int n = 0;
						foreach (var r in ros.Regulars.Where(r => r.CommanderName == CommanderName && r.Name.StartsWith("Pvt. Dead")))
						{
							var b = OoBody(r); if (b == null) continue;
							b.NPC.life = 1; b.NPC.SimpleStrikeNPC(99999, 0, false, 0f, null, false, 0f, false); n++;
						}
						OoLog($"killed {n} levies to respawn");
						return;
					}
					if (_ooTick - _ooStageTick > 300)
					{
						foreach (var r in ros.Regulars.Where(r => r.Dead && r.RespawnTimer > 60)) r.RespawnTimer = 60;
						if (_ooTe != null) _ooTe.Manpower = 99;
						if (ros.Regulars.Where(r => r.CommanderName == CommanderName && r.Name.StartsWith("Pvt. Dead")).All(r => r.HasBody) && _ooTick - _ooStageTick > 360) { _ooStage = 2; _ooStageTick = _ooTick; OoLog("respawn group back"); }
						else if (_ooTick - _ooStageTick > 3000) { OoLog("respawn group never came back: " + string.Join(",", ros.Regulars.Where(r => r.Name.StartsWith("Pvt. Dead")).Select(r => r.Name + ":dead=" + r.Dead + ":t=" + r.RespawnTimer + ":body=" + r.HasBody))); _ooFail++; Finish("orderobey: respawn failed"); }
					}
					return;
				case 2: // calm lead-in (sitters sit), then the boss
					if (_ooTick - _ooStageTick == (_ooRound == "calm" ? 2700 : 2300) && _ooRound == "fight") { _ooBoss = BtSpawnDummy((_cx - 18) * 16f, (FloorY - 6) * 16f); OoLog("boss spawned idx=" + _ooBoss + " bossFlag=" + OoBossFlag); if (OoBossFlag) { Main.npc[_ooBoss].boss = true; Main.npc[_ooBoss].damage = OoBossDamage; } }
					if (_ooTick - _ooStageTick == (_ooRound == "calm" ? 2700 : 2300) + OoWait + (_ooRound == "fight" ? 600 : 0)) { _ooStage = 3; _ooStageTick = _ooTick; _ooStep = 0; OoLog(OoCensus("pre-order census")); }
					return;
				case 3: // the orders
					StepOoOrders(); return;
			}
		}

		private string OoCensus(string head)
		{
			var mine = RosterSystem.Instance.Regulars.Where(r => r.CommanderName == CommanderName).ToList();
			int shaken = mine.Count(r => OoBody(r)?.State == SoldierState.Shaken), routed = mine.Count(r => OoBody(r)?.State == SoldierState.Routed); float fmax = mine.Count == 0 ? 0 : mine.Max(r => r.Fear), fmean = mine.Count == 0 ? 0 : mine.Average(r => r.Fear);
			int seated = mine.Count(r => OoSeated(OoBody(r))), eng = mine.Count(r => OoBody(r)?.State == SoldierState.Engage), tgt = mine.Count(r => (OoBody(r)?.TargetIndex ?? -1) >= 0);
			int noUse = mine.Count(r => { var b = OoBody(r); return b != null && !b.CanUseHeldWeapon(); }), homeNull = mine.Count(r => { var b = OoBody(r); return b != null && b.Home == null; }), cmdNull = mine.Count(r => { var b = OoBody(r); return b != null && b.Commander == null; }), dead = mine.Count(r => r.Dead);
			return $"{head}: cantUseWeapon={noUse} homeNull={homeNull} commanderNull={cmdNull} cmdIdx={string.Join("/", mine.Where(r => OoBody(r) != null).Select(r => r.CommanderIndex).Distinct())} p0active={Main.player[0].active} p0name={Main.player[0].name} deadNow={dead} tick={Main.GameUpdateCount} dayTime={Main.dayTime}/{(int)Main.time} levies={mine.Count} withBody={mine.Count(r => r.HasBody)} seated={seated} shaken={shaken} routed={routed} fearMax={fmax:0.0} fearMean={fmean:0.0} engage={eng} withTarget={tgt} mode={_ooMode} round={_ooRound} radioInPack={CommandRadio.Has(Main.player[0])}";
		}

		private string OoGroup(SoldierRecord r) => r.Name.StartsWith("Pvt. Reload") ? "reload" : r.Name.StartsWith("Pvt. Fresh") ? "fresh" : r.Name.StartsWith("Pvt. Dead") ? "respawned" : r.Name.StartsWith("Pvt. HoldSit") ? "holdsit" : r.Name.StartsWith("Pvt. FolSit") ? "folsit" : _opDied.Contains(r) ? "army-died" : "army";

		private void StepOoOrders()
		{
			if (_ooStep >= OoOrders.Length)
			{
				OoLog($"RESULT pass={_ooPass} fail={_ooFail} mode={_ooMode} round={_ooRound} liveAudit[audits={Systems.OrderAudit.Audits} levies={Systems.OrderAudit.Checked} missed={Systems.OrderAudit.Missed}]");
				Finish($"orderobey scenario complete pass={_ooPass} fail={_ooFail}"); return;
			}
			var (name, kind, order) = OoOrders[_ooStep];
			int t = _ooTick - _ooStageTick;
			var ros = RosterSystem.Instance;
			var mine = ros.Regulars.Where(r => r.CommanderName == CommanderName).ToList();
			Vector2 cmd = Main.player[0].Center;
			if (t == 1) { OoIssue(name, kind, order, mine, cmd); return; }
			if (t > 1 && t <= 1 + OoMeasureTicks)
			{
				string trace = Environment.GetEnvironmentVariable("LEVY_OO_TRACE"); // "Pvt. Reload3,Pvt. Fresh1": a state line every 6 ticks for those levies
				if (!string.IsNullOrEmpty(trace) && t % 6 == 0)
					foreach (var l in _oo)
					{
						if (!trace.Contains(l.Rec.Name)) continue;
						var tb = OoBody(l.Rec); if (tb == null) continue;
						string os = "?"; try { os = typeof(SoldierNPC).GetMethod("OrderState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(tb, null)?.ToString() ?? "?"; } catch (Exception) { }
						OoLog($"    TRACE {name} t={t} {l.Rec.Name} state={tb.State} orderState={os} order={l.Rec.Order} cmdIdx={l.Rec.CommanderIndex} cmd={tb.Commander != null} goal={tb.HasMoveGoal} x={tb.NPC.Center.X / 16f:0} sit={OoSeated(tb)} why={tb.StillReason()}");
					}
				foreach (var l in _oo) { var cb = OoBody(l.Rec); if (cb != null && cb.Commander == null) l.CmdGone = true; } // the stand-in has no client: the server flags its slot off now and then, which sends every Follow levy "home" for a think (never in a real game)
				foreach (var l in _oo)
				{
					if (l.FirstOk >= 0) continue;
					if (OoRecOk(l.Rec, kind, order, name)) l.FirstOk = t - 1;
				}
				if (t == 1 + OoMeasureTicks) OoEvaluate(name, kind, order);
				return;
			}
			if (t >= 1 + OoMeasureTicks + OoSettleTicks) { _ooStep++; _ooStageTick = _ooTick; }
		}

		/// <summary>The single-player path of the radial: UIActions with Main.netMode 0 and Main.myPlayer 0 for the length of the call (the commander is Main.LocalPlayer, IssueOrder runs directly, no packet).</summary>
		private void OoDeliverSp(int kind, SoldierOrder order)
		{
			int nm = Main.netMode, mp = Main.myPlayer;
			try
			{
				Main.netMode = Terraria.ID.NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.player[0].active = true;
				if (kind == 0) global::Levy.Common.UI.UIActions.Order(order, _ooAtBarracks);
				else if (kind == 1) global::Levy.Common.UI.UIActions.HoldFire(_ooAtBarracks);
				else global::Levy.Common.UI.UIActions.SpreadOut(_ooAtBarracks);
			}
			finally { Main.netMode = nm; Main.myPlayer = mp; }
		}

		private bool _ooExpectHf; private FormationMode _ooExpectForm;
		/// <summary>Snapshots every levy, works out what the order should leave behind, and sends it through the packet handler.</summary>
		private void OoIssue(string name, int kind, SoldierOrder order, List<SoldierRecord> mine, Vector2 cmd)
		{
			_oo.Clear();
			int holding = mine.Count(r => r.HoldFire), spread = mine.Count(r => r.SpreadOut), close = mine.Count(r => r.CloseUp);
			_ooExpectHf = holding * 2 <= mine.Count; // ToggleHoldFire: a majority holding releases, otherwise holds
			_ooExpectForm = Formation.Next(Formation.Majority(mine.Count, spread, close));
			foreach (var r in mine)
			{
				var b = OoBody(r);
				var l = new OoL { Group = OoGroup(r), Rec = r, S0 = b != null ? b.State : SoldierState.Dead, Seated0 = OoSeated(b), Tgt0 = b?.TargetIndex ?? -1, X0 = b != null ? b.NPC.Center.X / 16f : float.NaN };
				l.DB0 = b != null && _ooTe != null ? Math.Abs(b.NPC.Center.X - _ooTe.MusterPoint.X) / 16f : float.NaN;
				l.Reach = _ooAtBarracks || _ooRadio || (b != null && Vector2.Distance(b.NPC.Center, cmd) / 16f <= LevyConstants.OrderRadius);
				_oo.Add(l);
			}
			if (Environment.GetEnvironmentVariable("LEVY_OO_PATH") == "sp") OoDeliverSp(kind, order);
			else if (kind == 0) OoSend(LevyPacket.Order, w => { w.Write((byte)order); w.Write(_ooAtBarracks); });
			else if (kind == 1) OoSend(LevyPacket.HoldFire, w => w.Write(_ooAtBarracks));
			else OoSend(LevyPacket.SpreadOut, w => w.Write(_ooAtBarracks));
			OoLog($"--- step {_ooStep} {name}: sent via packet handler (atBarracks={_ooAtBarracks}, recordsTookIt={mine.Count(r => OoRecOk(r, kind, order, name))}/{mine.Count}); {OoCensus("before")}");
		}

		private bool OoRecOk(SoldierRecord r, int kind, SoldierOrder order, string name)
		{
			if (kind == 0) return r.Order == order;
			if (kind == 1) return r.HoldFire == _ooExpectHf;
			return Formation.Of(r) == _ooExpectForm;
		}

		private void OoEvaluate(string name, int kind, SoldierOrder order)
		{
			int stepPass = 0, stepFail = 0;
			foreach (var l in _oo)
			{
				var r = l.Rec; var b = OoBody(r);
				bool recOk = OoRecOk(r, kind, order, name);
				string bodyNote = "nobody", bodyNoteExtra = ""; bool bodyOk = true; float moved = float.NaN, towardBarracks = float.NaN;
				if (b != null)
				{
					moved = b.NPC.Center.X / 16f - l.X0;
					if (_ooTe != null) towardBarracks = l.DB0 - Math.Abs(b.NPC.Center.X - _ooTe.MusterPoint.X) / 16f;
					var s = b.State; bool fear = s == SoldierState.Shaken || s == SoldierState.Routed || s == SoldierState.Desert || s == SoldierState.Dead;
					if (kind == 0 && fear) { bodyOk = order != SoldierOrder.ReturnToBarracks || float.IsNaN(towardBarracks) || towardBarracks >= 3f; bodyNoteExtra = " FEAR-HELD:" + s; } // a Shaken / Routed body must still walk home on Return
					if (kind == 0 && !fear)
					{
						switch (order)
						{
							case SoldierOrder.Hold: bodyOk = b.HoldPosition && (s == SoldierState.Idle || s == SoldierState.Engage); break;
							case SoldierOrder.Follow: bodyOk = !b.HoldPosition && (b.Commander == null || l.CmdGone || s == SoldierState.Follow || s == SoldierState.Engage || s == SoldierState.Idle || s == SoldierState.Haul || s == SoldierState.Resupply); break;
							case SoldierOrder.ReturnToBarracks: bodyOk = !b.HoldPosition && (s == SoldierState.ReturnHome || s == SoldierState.Engage || s == SoldierState.Idle || (b.Home == null && s == SoldierState.Follow)); break;
							case SoldierOrder.SearchDestroy: bodyOk = !b.HoldPosition && (s == SoldierState.SearchDestroy || s == SoldierState.Engage); break;
							case SoldierOrder.FreeRein: bodyOk = !b.HoldPosition && (s == SoldierState.FreeRein || s == SoldierState.Engage); break;
						}
					}
					bodyNote = $"state={s} cmd={b.Commander != null} hold={b.HoldPosition} seatedNow={OoSeated(b)} tgt={b.TargetIndex} dx={moved:0.0} towardBarracks={towardBarracks:0.0} why={b.StillReason()}";
				}
				bool ok = !l.Reach || (recOk && bodyOk);
				if (ok) { _ooPass++; stepPass++; } else { _ooFail++; stepFail++; }
				OoLog($"{name,-13} {l.Group,-9} {r.Name,-16} reach={l.Reach} before[state={l.S0} seated={l.Seated0} tgt={l.Tgt0}] rec[order={r.Order} holdFire={r.HoldFire} spread={r.SpreadOut} close={r.CloseUp} recOkAtTick={l.FirstOk}] body[{bodyNote} fear={r.Fear:0.0}{bodyNoteExtra}] -> {(ok ? "ok" : "FAIL")}{(recOk ? "" : " REC-IGNORED")}{(bodyOk ? "" : " BODY-IGNORED")}");
			}
			OoLog($"STEP {name}: pass={stepPass} fail={stepFail}");
		}

		private void OoSend(LevyPacket type, Action<BinaryWriter> write)
		{
			// the stand-in has no client behind it, and the server's net thread can clear its active flag between the pin and the send: retry until the packet is seen to change something or the player stood throughout
			string before = OoSig();
			for (int attempt = 0; attempt < 6; attempt++)
			{
				Main.player[0].active = true;
				var ms = new MemoryStream(); var bw = new BinaryWriter(ms);
				bw.Write((byte)type); write(bw); bw.Flush(); ms.Position = 0;
				LevyPackets.Handle(Mod, new BinaryReader(ms), 0);
				if (Main.player[0].active || OoSig() != before) return;
				OoLog("    (stand-in player dropped by the server during the send: resending)");
			}
		}
		private string OoSig() => string.Join(",", RosterSystem.Instance.Regulars.Where(r => r.CommanderName == CommanderName).Select(r => (int)r.Order * 1000 + (r.HoldFire ? 100 : 0) + (r.SpreadOut ? 10 : 0) + (r.CloseUp ? 1 : 0)));
	}
}
