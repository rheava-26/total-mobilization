using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of the Exo Mechs lab fixes, logged as "[ExoFix]" lines (the summon world check chains it after the boss-bugs check; LEVY_EXOFIX_ONLY=1 runs it alone,
	/// LEVY_EXOFIX_PART=scale|mirror|area|beam|armour picks parts). A flat arena, slot 0 as the commander, the real Main.Update stepping the world; every part also runs with its fix switched off,
	/// so the old behaviour is seen failing in the same boot. Everything made is removed again and nothing is saved.
	///  * MIRROR (both halves): two bosses whose lives are clamped to each other every tick (Calamity's twin rule, played by the check): a scaling on one used to be taken back the next tick;
	///    the fall-back check now finds the partner, raises it too and gives the add back.
	///  * SCALE (Calamity): real Ares (body and four arms), Thanatos (head and segments) and Artemis with Apollo: life equals the raised maximum right after scaling and 60 ticks later.
	///  * AREA (Calamity): a real Gauss Nuke telegraph over a row of levies: they leave its 740 px circle before it would go off.
	///  * BEAM (Calamity): Thanatos's beam, the Blenderbeam, the Ohio Beam (laserbeam base) and the Exothermal Laser (deathray pattern) register as line threats of their length.
	///  * ARMOUR (Calamity): a real Thanatos held in a row over armed levies, every segment shut but one: their hits land on the open one.
	///  * ADAPT (Calamity, ExoFixWorldCheck.Adapt.cs): "Bosses adapt to armies" (ArmyAwareExo) off and on: Ares's arms, Thanatos's vents, an Apollo rocket, and a solo commander.
	/// </summary>
	public partial class ExoFixWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod;
		private static Vector2 _cmdAt;
		private static bool _local; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 220;
		private const string Commander = "ExoFixTester";
		private static Action _perTick, _postNpcs;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_EXOFIX") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (!_running) return; if (_local) Main.myPlayer = _savedMy; _postNpcs?.Invoke(); }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running && _local) Main.myPlayer = _savedMy; }

		/// <summary>True while the check steps the world.</summary>
		public static bool Running => _running;

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time; }
			_mod.Logger.Info($"[ExoFix] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok, Func<string> why = null) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ExoFix] {(ok ? "PASS" : "FAIL")} {name}{(!ok && why != null ? " :: " + why() : "")}"); }
		private static void Note(string s) => _mod.Logger.Info("[ExoFix] OBSERVED " + s);

		private static bool Part(string only, string name) => only == "all" || Array.IndexOf(only.Split(','), name) >= 0;

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			string only = Environment.GetEnvironmentVariable("LEVY_EXOFIX_PART") ?? "all";
			Arena(FloorY, Half);
			PlaceCommander(-150);
			Main.dayTime = false; Main.time = 18000;
			if (Part(only, "mirror")) { try { Mirror(); } finally { Cleanup(); } }
			bool cal = ModLoader.HasMod("CalamityMod");
			if (!cal) { Note("Calamity is not loaded: the scale, area, beam and armour parts are skipped"); return; }
			Main.GameMode = GameModeID.Master; Main.hardMode = true;
			if (Part(only, "scale")) { try { Scale(); } finally { Cleanup(); } }
			if (Part(only, "area")) { try { Area(); } finally { Cleanup(); } }
			if (Part(only, "beam")) { try { Beam(); } finally { Cleanup(); } }
			if (Part(only, "armour")) { try { Armour(); } finally { Cleanup(); } }
			if (Part(only, "adapt")) { try { Adapt(); } finally { AdaptRestore(); Cleanup(); } }
		}

		// =====================================================================================================
		// scaffolding
		// =====================================================================================================
		private static void Step(int n) { for (int i = 0; i < n; i++) { KeepCommander(); _perTick?.Invoke(); WorldStep.Update(); } }

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.name = Commander; p.dead = false;
			Main.dayTime = false; Main.time = 18000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void Arena(int fy, int half)
		{
			for (int i = Cx - half; i <= Cx + half; i++)
			{
				for (int j = fy - 140; j < fy; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				for (int j = fy; j < fy + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void PlaceCommander(float tilesFromCentre) { _cmdAt = new Vector2((Cx + tilesFromCentre) * 16f, (FloorY - 3) * 16f); Main.player[0].position = _cmdAt; }

		private static SoldierNPC Levy(Vector2 bottom, int weapon = 0, int ammo = 0, int hp = 6000)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. ExoFix",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Hold;
			if (weapon > 0) { var w = new Item(); w.SetDefaults(weapon); rec.Gear.Weapon = w; }
			if (ammo > 0) { var a = new Item(); a.SetDefaults(ammo); a.stack = 9999; rec.Gear.Ammo = a; }
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, bottom);
			if (body < 0 || !rec.HasBody) return null;
			NPC n = Main.npc[body]; n.Bottom = bottom;
			if (hp > 0) { n.lifeMax = n.life = hp; rec.CurrentHp = hp; }
			if (n.ModNPC is SoldierNPC s) { rec.HoldPos = n.Center; return s; }
			return null;
		}

		private static NPC SpawnNpc(int type, Vector2 at)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("ExoFixCheck"), (int)at.X, (int)at.Y, type, 1); // never slot 0: Calamity reads ai index 0 as "no segment yet", so a Thanatos head in slot 0 strings no body and clamps itself to 0 life
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC n = Main.npc[i]; n.timeLeft = 99999; n.netOffset = Vector2.Zero;
			return n;
		}

		private static int CalNpc(string name) => ModContent.TryFind("CalamityMod/" + name, out ModNPC m) ? m.Type : -1;
		private static int CalProj(string name) => ModContent.TryFind("CalamityMod/" + name, out ModProjectile m) ? m.Type : -1;

		private static void ClearHostiles()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is not SoldierNPC && !n.friendly && !n.townNPC) n.active = false; }
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
		}

		/// <summary>
		/// Frees NPC slots when fewer than <paramref name="need"/> are free: whatever an earlier check of the chain left standing (its levies, their creatures) that is not a town NPC is removed here,
		/// and noted. Nothing is saved, so this only affects the checks after this one, which build their own fixtures.
		/// </summary>
		private static void MakeRoom(int need)
		{
			int free = 0; for (int i = 0; i < Main.maxNPCs; i++) if (!Main.npc[i].active) free++;
			if (free >= need) return;
			var kinds = new Dictionary<string, int>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.townNPC) continue;
				if (n.ModNPC is SoldierNPC sn && sn.Record != null && sn.Record.CommanderName == Commander) continue;
				string k = n.ModNPC != null ? n.ModNPC.Mod.Name + "/" + n.ModNPC.Name : n.TypeName;
				kinds[k] = kinds.GetValueOrDefault(k) + 1;
				n.active = false;
			}
			Note($"only {free} NPC slots were free; cleared what earlier checks left: {string.Join(", ", kinds.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key + " x" + kv.Value))}");
		}

		private static void Cleanup()
		{
			try
			{
				ClearHostiles();
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } }
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				_perTick = null; _postNpcs = null;
				BossRule.GroupScaling = Environment.GetEnvironmentVariable("LEVY_BOSSHP_GROUP") != "0";
				ThreatShapes.AreasOn = Environment.GetEnvironmentVariable("LEVY_AREAS") != "0"; ThreatShapes.BeamsOn = Environment.GetEnvironmentVariable("LEVY_MODBEAMS") != "0";
				SoldierTargeting.ArmourPickOn = Environment.GetEnvironmentVariable("LEVY_ARMOURPICK") != "0";
				global::Levy.Common.Compat.Calamity.CalamityLife.MirrorOff = false;
				_hits.Clear(); _hitWatch = false;
				ThreatField.TestInvalidate(); SoldierTargeting.TestForgetArmourCache();
			}
			catch (Exception) { }
		}

		// =====================================================================================================
		// MIRROR: the fall-back check, on two bosses the check clamps to each other
		// =====================================================================================================
		private static void Mirror()
		{
			foreach (bool fix in new[] { false, true })
			{
				ClearHostiles(); _perTick = null;
				BossRule.GroupScaling = fix;
				NPC a = SpawnNpc(NPCID.EyeofCthulhu, new Vector2((Cx - 30) * 16f, (FloorY - 60) * 16f));
				NPC b = SpawnNpc(NPCID.EyeofCthulhu, new Vector2((Cx + 30) * 16f, (FloorY - 60) * 16f));
				if (a == null || b == null) { Check("two Eyes of Cthulhu spawn for the mirrored pair", false); return; }
				// the mirror, as Calamity writes it for the Exo twins: each one's life drops to the other's when it is higher
				_perTick = () => { if (a.active && b.active) { if (a.life > b.life) a.life = b.life; if (b.life > a.life) b.life = a.life; } };
				Step(3);
				int baseMax = a.lifeMax; long found0 = BossRule.FallBackPartnersFound;
				BossRule.ApplyScaling(a, 0.3f);
				int added = a.lifeMax - baseMax;
				Step(60);
				string state = $"base {baseMax}, max now {a.lifeMax} (+{added}), life {a.life}; partner max {b.lifeMax}, life {b.life}; partners found {BossRule.FallBackPartnersFound - found0}";
				Note((fix ? "fixed rule: " : "old rule: ") + state);
				if (!fix) Check("old rule reproduced: the add on one of a mirrored pair is taken back while its maximum stays raised", added > 0 && a.lifeMax > baseMax && a.life < a.lifeMax - added / 2, () => state);
				else
				{
					Check("fall-back check: the scaled boss keeps its full raised life 60 ticks on", added > 0 && a.life == a.lifeMax, () => state);
					Check("fall-back check: the mirrored partner is found and raised by the same share", BossRule.FallBackPartnersFound > found0 && b.lifeMax == a.lifeMax && b.life == b.lifeMax, () => state);
				}
				a.active = false; b.active = false;
			}
		}

		// =====================================================================================================
		// SCALE: the real Exo Mechs
		// =====================================================================================================
		private static void Scale()
		{
			int ares = CalNpc("AresBody"), than = CalNpc("ThanatosHead"), art = CalNpc("Artemis"), apo = CalNpc("Apollo");
			int arm = CalNpc("AresLaserCannon"), seg = CalNpc("ThanatosBody1");
			Check("Calamity's Exo Mech types are found", ares > 0 && than > 0 && art > 0 && apo > 0);
			if (ares <= 0 || than <= 0 || art <= 0 || apo <= 0) return;
			Check("Calamity's twin link is read (CalamityGlobalNPC.draedonExoMechTwinRed / Green)", global::Levy.Common.Compat.Calamity.CalamityLife.Available);
			// (mode: 0 old rule, 1 fixed, 2 fixed with the twin link unread so only the fall-back check can find Apollo)
			foreach (int mode in new[] { 0, 1 })
			{
				ScaleOne("Ares", mode, () => SpawnNpc(ares, new Vector2(Cx * 16f, (FloorY - 50) * 16f)), null, arm);
				ScaleOne("Thanatos", mode, () => SpawnNpc(than, new Vector2(Cx * 16f, (FloorY - 50) * 16f)), null, seg);
			}
			foreach (int mode in new[] { 0, 1, 2 })
				ScaleOne("Artemis and Apollo", mode, () => SpawnNpc(art, new Vector2((Cx - 20) * 16f, (FloorY - 50) * 16f)), () => SpawnNpc(apo, new Vector2((Cx + 20) * 16f, (FloorY - 50) * 16f)), apo);
		}

		/// <summary>One mech: spawn it, let its parts come, scale its root through a part (the way a levy's hit resolves), then read life against the maximum at once and 60 ticks later.</summary>
		private static void ScaleOne(string name, int mode, Func<NPC> spawn, Func<NPC> spawn2, int partType)
		{
			ClearHostiles();
			MakeRoom(130); // Thanatos strings ~100 segments; with no free slot his head clamps to an empty slot's life (0) and dies
			BossRule.GroupScaling = mode != 0; global::Levy.Common.Compat.Calamity.CalamityLife.MirrorOff = mode == 2;
			NPC root = spawn(); NPC second = spawn2?.Invoke();
			if (root == null) { Check(name + " spawns", false); return; }
			_perTick = () => { for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && n.ModNPC is not SoldierNPC) { n.timeLeft = 99999; n.dontTakeDamage = false; } } };
			Step(20);
			int parts = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].realLife == root.whoAmI && i != root.whoAmI) parts++;
			for (int retry = 0; retry < 2 && partType != (second?.type ?? -1) && parts == 0; retry++)
			{
				// a body whose parts never came (seen once in the full chain for Thanatos: his head clamps to slot ai[0]'s life, an empty slot's 0, and dies): note what it looked like and spawn it again
				Note($"{name} came without its parts: active {root.active} life {root.life}/{root.lifeMax} ai {root.ai[0]},{root.ai[1]},{root.ai[2]},{root.ai[3]} slot {root.whoAmI}, {Main.npc.Count(n => !n.active)} slots free; spawning it again");
				ClearHostiles(); Step(10);
				root = spawn(); if (root == null) break;
				Step(20);
				parts = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].realLife == root.whoAmI && i != root.whoAmI) parts++;
			}
			if (root == null) { Check(name + " spawns", false); return; }
			NPC part = null; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].type == partType) { part = Main.npc[i]; break; }
			bool resolved = part != null && BossRule.TryGetScalingRoot(part, out NPC r, out _) && (r == root || (second != null && r == second));
			if (mode == 1) Check($"{name}: a hit on a part resolves to the boss's root ({parts} parts share its life)", resolved || (second != null && part == second));
			NPC hitRoot = second != null ? root : (resolved && BossRule.TryGetScalingRoot(part, out NPC rr, out _) ? rr : root);
			int baseMax = hitRoot.lifeMax, baseLife = hitRoot.life;
			var group = new List<int>(); BossRule.LifeGroup(hitRoot, group);
			var before = group.ToDictionary(i => i, i => (life: Main.npc[i].life, max: Main.npc[i].lifeMax));
			BossRule.ApplyScaling(hitRoot, 0.35f);
			int added = hitRoot.lifeMax - baseMax;
			// every NPC of the group got the same add on its life as on its maximum (a segment whose life already sat under its own maximum keeps that gap)
			var bad = group.Where(i => { var b = before[i]; int dm = Main.npc[i].lifeMax - b.max, dl = Main.npc[i].life - b.life; return dm <= 0 || dl != dm; }).Select(i => $"{Main.npc[i].TypeName}#{i}: life {before[i].life}->{Main.npc[i].life} max {before[i].max}->{Main.npc[i].lifeMax}").ToList();
			bool allNow = hitRoot.life == hitRoot.lifeMax && bad.Count == 0;
			if (bad.Count > 0 && mode != 0) Note($"{name}: members whose add did not land on life and maximum alike: {bad.Count}, first {bad[0]}");
			Step(60);
			bool rootHeld = hitRoot.active && hitRoot.life == hitRoot.lifeMax;
			bool partnerHeld = second == null || (second.active && second.life == second.lifeMax && second.lifeMax > baseMax);
			string state = $"{parts} parts, {Main.npc.Count(n => !n.active)} NPC slots free; base max {baseMax} life {baseLife}; +{added}; now max {hitRoot.lifeMax} life {hitRoot.life}{(second != null ? $"; partner max {second.lifeMax} life {second.life}" : "")}; group {group.Count} NPCs; members raised so far {BossRule.GroupMembersScaled}, partners found by the fall-back {BossRule.FallBackPartnersFound}";
			Note($"{name} [{(mode == 0 ? "old rule" : mode == 1 ? "fixed" : "fixed, twin link unread")}]: {state}");
			if (mode == 0) Check($"{name}: old rule reproduced (the add falls back, the raised maximum stays)", added > 0 && hitRoot.life < hitRoot.lifeMax - added / 2, () => state);
			else
			{
				Check($"{name}{(mode == 2 ? " (fall-back check alone)" : "")}: life equals the raised maximum right after scaling, every NPC of its life group", mode == 2 || (added > 0 && allNow), () => state);
				Check($"{name}{(mode == 2 ? " (fall-back check alone)" : "")}: life still equals the raised maximum 60 ticks later", added > 0 && rootHeld && partnerHeld, () => state);
			}
			ClearHostiles(); _perTick = null;
		}

		// =====================================================================================================
		// AREA: a real Gauss Nuke telegraph over a row of levies
		// =====================================================================================================
		private static void Area()
		{
			int nuke = CalProj("AresGaussNukeProjectile"), boom = CalProj("AresGaussNukeProjectileBoom");
			Check("the Gauss Nuke and its explosion types are found", nuke > 0 && boom > 0);
			if (nuke <= 0) return;
			PlaceCommander(-200); // far: the nuke homes on him at a crawl and never reaches its stop-homing range
			float[] offs = { -650f, -590f, -520f, -450f, 450f, 520f, 590f, 650f };
			const float R = 740f;
			foreach (bool fix in new[] { false, true })
			{
				ClearHostiles();
				ThreatShapes.AreasOn = fix;
				Vector2 centre = new Vector2(Cx * 16f, FloorY * 16f - 30f);
				var lv = new List<SoldierNPC>();
				foreach (float o in offs) { var s = Levy(new Vector2(centre.X + o, FloorY * 16f)); if (s != null) lv.Add(s); }
				Step(3);
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("ExoFixCheck"), centre, new Vector2(0.01f, 0f), nuke, 400, 0f, Main.myPlayer, -1f, 999999f);
				Projectile p = pi >= 0 && pi < Main.maxProjectiles ? Main.projectile[pi] : null;
				if (p == null) { Check("a Gauss Nuke spawns", false); return; }
				p.timeLeft = 180;
				ThreatField.TestInvalidate();
				var areas = ThreatField.AreasNow();
				if (fix) Check("the Gauss Nuke registers as a pending area of its blast's reach (1350 px, past the 740 px circle it draws)", areas.Any(a => a.Proj == pi && Math.Abs(a.R - 1350f) < 1f && a.Fuse > 100), () => string.Join(",", areas.Select(a => $"{a.Proj}:{a.R}/{a.Fuse}")));
				int[] outAt = Enumerable.Repeat(-1, lv.Count).ToArray();
				for (int t = 0; t < 170; t++)
				{
					p.timeLeft = Math.Max(p.timeLeft, 20); // never goes off in the check (ai[0] = -1 would make no blast anyway)
					Step(1);
					for (int k = 0; k < lv.Count; k++) if (outAt[k] < 0 && lv[k].NPC.active && Vector2.Distance(lv[k].NPC.Center, p.Center) > R) outAt[k] = t;
				}
				int outN = outAt.Count(x => x >= 0);
				string state = $"{outN} of {lv.Count} levies left the circle within 170 ticks (ticks: {string.Join(",", outAt)}); final distances {string.Join(",", lv.Select(s => (int)Vector2.Distance(s.NPC.Center, p.Center)))} px";
				Note((fix ? "areas on: " : "areas off: ") + state);
				if (!fix) Check("old rule reproduced: levies inside the telegraph stay in it", outN <= lv.Count / 4, () => state);
				else Check("levies leave a Gauss Nuke telegraph before it would go off (all of them, inside 170 of its 180 ticks)", lv.Count == offs.Length && outN == lv.Count, () => state);
				p.active = false;
				foreach (var s in lv) { if (s.Record != null) s.Record.Gear = new SoldierGear(); s.NPC.active = false; }
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
			// the blast itself: the damaging wave is an area of its current reach, the two drawn for show are not
			ThreatShapes.AreasOn = true;
			int bi = Projectile.NewProjectile(new EntitySource_WorldEvent("ExoFixCheck"), new Vector2(Cx * 16f, (FloorY - 20) * 16f), Vector2.Zero, boom, 400, 0f, Main.myPlayer);
			int bz = Projectile.NewProjectile(new EntitySource_WorldEvent("ExoFixCheck"), new Vector2(Cx * 16f, (FloorY - 20) * 16f), Vector2.Zero, boom, 0, 0f, Main.myPlayer);
			if (bi >= 0 && bz >= 0)
			{
				Main.projectile[bi].ai[0] = 300f; Main.projectile[bi].ai[1] = 740f; Main.projectile[bz].ai[0] = 300f; Main.projectile[bz].ai[1] = 740f;
				bool dmg = ThreatShapes.AreaOf(Main.projectile[bi], out float rb, out _), show = ThreatShapes.AreaOf(Main.projectile[bz], out _, out _);
				Check("the Gauss Nuke's damaging blast is an area while it grows; the waves drawn for show are not", dmg && rb > 300f && !show, () => $"damaging {dmg} r {rb:0}, show-only {show}");
				Main.projectile[bi].active = false; Main.projectile[bz].active = false;
			}
			// parity: the blast hurts a player inside 0.4 of its hitbox (a circle); a levy in the band between that circle and the rectangle, where a player is safe, is not hit either
			int pb = Projectile.NewProjectile(new EntitySource_WorldEvent("ExoFixCheck"), new Vector2(Cx * 16f, (FloorY - 20) * 16f), Vector2.Zero, boom, 400, 0f, Main.myPlayer);
			var probe = Levy(new Vector2(Cx * 16f + 1000f, FloorY * 16f));
			if (pb >= 0 && probe != null)
			{
				Projectile b = Main.projectile[pb];
				b.ai[0] = 740f; b.ai[1] = 740f; b.scale = 3f; b.timeLeft = 40;
				Vector2 bc = new Vector2(Cx * 16f, FloorY * 16f - 21f); b.width = b.height = (int)(740f * 3f); b.Center = bc; // the hitbox the blast's own AI gives it: CurrentRadius x scale wide
				probe.NPC.Center = bc + new Vector2(1000f, 0f);
				bool farOk = global::Levy.Common.Util.HostileContact.ProjectileAllows(b, probe.NPC, probe.Dummy ?? global::Levy.Common.Util.HostileContact.Stand);
				probe.NPC.Center = bc + new Vector2(500f, 0f);
				bool nearOk = global::Levy.Common.Util.HostileContact.ProjectileAllows(b, probe.NPC, probe.Dummy ?? global::Levy.Common.Util.HostileContact.Stand);
				Note($"Gauss blast with an 888 px player reach and a 1110 px half-wide hitbox: a levy 1000 px out may be hit {farOk}, 500 px out {nearOk}");
				Check("projectile parity: the Gauss blast does not hit a levy where it would not hit a player (1000 px out: inside its rectangle, outside its 888 px circle), and does 500 px out", !farOk && nearOk);
				b.active = false;
			}
			if (probe != null) { if (probe.Record != null) probe.Record.Gear = new SoldierGear(); probe.NPC.active = false; }
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
		}

		// =====================================================================================================
		// BEAM: Calamity's deathrays are lines
		// =====================================================================================================
		private static void Beam()
		{
			var kinds = new[] { ("ThanatosBeamStart", "laserbeam base"), ("AresDeathBeamStart", "laserbeam base (Blenderbeam)"), ("ArtemisSpinLaserbeam", "laserbeam base (Ohio Beam)"), ("AresLaserBeamStart", "deathray pattern (Exothermal Laser)") };
			Vector2 at = new Vector2(Cx * 16f, (FloorY - 40) * 16f);
			foreach (var (name, kind) in kinds)
			{
				int type = CalProj(name);
				if (type <= 0) { Check(name + " type is found", false); continue; }
				foreach (bool fix in new[] { false, true })
				{
					ThreatShapes.BeamsOn = fix;
					int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("ExoFixCheck"), at, new Vector2(1f, 0f), type, 300, 0f, Main.myPlayer);
					if (pi < 0 || pi >= Main.maxProjectiles) { Check(name + " spawns", false); break; }
					Projectile p = Main.projectile[pi];
					p.hostile = true; p.velocity = new Vector2(1f, 0f); p.localAI[1] = 900f; p.Center = at;
					bool beam = ThreatField.IsBeam(p, out float len, out float width);
					ThreatField.TestInvalidate();
					var snap = ThreatField.Snapshot();
					bool line = snap.Any(th => th.Line && Vector2.Distance(th.P, p.Center) < 1f && Math.Abs(th.Len - 900f) < 1f);
					Note($"{name} ({kind}) beams {(fix ? "on" : "off")}: line={beam} len={len:0} width={width:0.#}, in the field as a line: {line}");
					if (fix) Check($"{name} ({kind}) registers as a line threat of its length", beam && line && Math.Abs(len - 900f) < 1f && width > 4f, () => $"beam {beam} len {len} width {width} line {line}");
					else Check($"{name}: old rule reproduced (no line threat)", !beam && !line);
					p.active = false;
				}
			}
		}

		// =====================================================================================================
		// ARMOUR: hits land on Thanatos's open segment
		// =====================================================================================================
		private static readonly Dictionary<bool, int> _hits = new();
		private static readonly Dictionary<int, int> _hitBy = new();
		private static bool _hitWatch; private static int _openSeg = -1;
		public class ExoFixHits : GlobalNPC
		{
			public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
			{
				if (!_hitWatch || !_running) return;
				bool open = npc.whoAmI == _openSeg;
				_hits[open] = _hits.GetValueOrDefault(open) + 1;
				_hitBy[npc.whoAmI] = _hitBy.GetValueOrDefault(npc.whoAmI) + 1;
			}
		}

		private static void Armour()
		{
			int than = CalNpc("ThanatosHead");
			if (than <= 0) { Check("Thanatos's type is found", false); return; }
			PlaceCommander(-200);
			foreach (bool fix in new[] { false, true })
			{
				ClearHostiles(); _hits.Clear();
				SoldierTargeting.ArmourPickOn = fix;
				// the levies first (lower slots: their AI reads the armour this check set after the worm's own AI last tick)
				var lv = new List<SoldierNPC>();
				for (int k = 0; k < 6; k++) { var s = Levy(new Vector2((Cx - 6 + k * 2) * 16f, FloorY * 16f), ItemID.Megashark, ItemID.MusketBall); if (s != null) lv.Add(s); }
				NPC head = SpawnNpc(than, new Vector2(Cx * 16f, (FloorY - 40) * 16f));
				if (head == null || lv.Count < 4) { Check("Thanatos and the levies spawn", false); return; }
				Step(4);
				var chain = new List<NPC> { head };
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].realLife == head.whoAmI && i != head.whoAmI) chain.Add(Main.npc[i]);
				// the row: the head far left, the segments side by side (a body length apart, as the worm strings them), 22 tiles over the levies; the open one is 4 segments right of the one straight above them
				float gap = Math.Max(chain[Math.Min(1, chain.Count - 1)].width, chain[Math.Min(1, chain.Count - 1)].height) + 4f;
				int above = chain.Count / 2, open = Math.Min(chain.Count - 1, above + 4);
				_openSeg = chain[open].whoAmI;
				float y = (FloorY - 22) * 16f, x0 = Cx * 16f - above * gap;
				_postNpcs = () =>
				{
					for (int k = 0; k < chain.Count; k++)
					{
						NPC n = chain[k]; if (!n.active) continue;
						n.Center = new Vector2(x0 + k * gap, y); n.velocity = Vector2.Zero; n.timeLeft = 99999; n.dontTakeDamage = false; n.life = n.lifeMax;
						bool isOpen = k == open;
						n.chaseable = isOpen;
						SetCalamityArmour(n, isOpen ? 0.1f : 0.9999f, !isOpen);
					}
					SoldierTargeting.TestForgetArmourCache();
				};
				_postNpcs();
				Step(30);
				_hitWatch = true;
				Step(300);
				_hitWatch = false;
				int o = _hits.GetValueOrDefault(true), c = _hits.GetValueOrDefault(false);
				long openAims = global::Levy.Common.Weapons.Aiming.OpenAims; int onOpen = lv.Count(s => s.TargetIndex == _openSeg), onShut = lv.Count(s => s.TargetIndex >= 0 && s.TargetIndex != _openSeg);
				string top = string.Join(" ", _hitBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"#{chain.FindIndex(n => n.whoAmI == kv.Key)}({Main.npc[kv.Key].TypeName}, {(int)((Main.npc[kv.Key].Center.X - Cx * 16f) / 16f)},{(int)((Main.npc[kv.Key].Center.Y - FloorY * 16f) / 16f)} tiles)x{kv.Value}"));
				string state = $"{chain.Count} segments {gap:0} px apart, open #{open} at {(int)((chain[open].Center.X - Cx * 16f) / 16f)},{(int)((chain[open].Center.Y - FloorY * 16f) / 16f)} tiles: hits on the open segment {o}, on shut ones {c} (most hit: {top}); levies targeting the open one {onOpen}, a shut one {onShut}; aims turned to an open segment so far {openAims}";
				_hitBy.Clear();
				Note((fix ? "armour pick on: " : "armour pick off: ") + state);
				// (a shot at the vent can still clip the plate beside it, as a player's does: the open one taking a third of the hits from levies whose nearest plate is shut is the change)
				if (fix) Check("levies aim at the open segment past the shut plate in front of them: a third or more of their hits land on it (all others shut at 99.99%)", o + c >= 20 && o * 3 >= o + c && onOpen == lv.Count, () => state);
				else Check("old rule reproduced: every levy aims at the shut plate in front of it, under a tenth of the hits reach the open segment", o + c >= 20 && o * 10 < o + c, () => state);
				_postNpcs = null; ClearHostiles();
				foreach (var s in lv) { if (s.Record != null) s.Record.Gear = new SoldierGear(); s.NPC.active = false; }
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
		}

		private static System.Reflection.PropertyInfo _dr; private static System.Reflection.FieldInfo _unb; private static GlobalNPC _calG;
		private static void SetCalamityArmour(NPC n, float dr, bool unbreakable)
		{
			if (_calG == null)
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal) || !cal.TryFind("CalamityGlobalNPC", out _calG)) return;
				Type g = _calG.GetType(); _dr = g.GetProperty("DR"); _unb = g.GetField("unbreakableDR");
			}
			if (!n.TryGetGlobalNPC(_calG, out GlobalNPC inst)) return;
			_dr?.SetValue(inst, dr); _unb?.SetValue(inst, unbreakable);
		}
	}
}
