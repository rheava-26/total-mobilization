using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Levy.Common.Recording;
using Microsoft.Xna.Framework;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// THE STEALTH CHECK ("[StealthWorld]" lines, Calamity half only; LEVY_SELFCHECK_WORLD=1 on a dedicated server, LEVY_STEALTH_ONLY=1 runs it alone).
	/// Does a mobilized army change a rogue commander's stealth? The commander (slot 0, the single-player situation: Main.myPlayer = 0) wears Statigel rogue armor and holds an Iron Francisca (a single throw with a x3 stealth
	/// strike). Three runs, each on a fresh stealth bar: NO ARMY, then WITH AN ARMY (ten rogue levies standing idle far away, eight rogue levies next to the commander fighting a pinned enemy, all in Statigel with a Francisca so
	/// their gear frames run Calamity's stealth code, and a fire debuff on every body so the damage-over-time step runs), then NO ARMY again (the repeatability of the measurement itself).
	/// Per run: ticks for the bar to fill standing still and moving (Calamity: 120 and 240 at the default rates); the damage of the first throw on a full bar (a stealth strike) and of the next throw on an empty bar;
	/// the damage that landed on the commander's pinned enemy; the stealth left after a strike; and the strike flag Calamity puts on the projectile.
	/// Pass: the numbers are Calamity's own (the rates, a flagged strike, the bar spent) and the army run equals the no-army runs.
	/// </summary>
	public class StealthWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "StealthTester";
		private static Vector2 _cmdAt;
		private static NPC _t1, _t2; private static Vector2 _t1At, _t2At;

		public override void Load()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_STEALTHWORLD") == "0") return;
			// A player's own Update (Main.myPlayer == whoAmI) runs the biome sky and filter visuals, which a dedicated server has no managers for: it throws and cuts the rest of the update off. The check runs the commander
			// as the local player (the single-player situation), so on a dedicated test server that one method is skipped. A client never takes the branch (Main.dedServ is false there).
			try
			{
				MethodInfo m = typeof(Player).GetMethod("UpdateBiomes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				FieldInfo my = typeof(Main).GetField("myPlayer"), ded = typeof(Main).GetField("dedServ"), who = typeof(Player).GetField("whoAmI");
				if (m != null && my != null && ded != null && who != null)
					MonoModHooks.Modify(m, il =>
					{
						var c = new ILCursor(il); var go = c.DefineLabel();
						c.Emit(OpCodes.Ldsfld, ded); c.Emit(OpCodes.Brfalse, go);
						c.Emit(OpCodes.Ldsfld, my); c.Emit(OpCodes.Ldarg_0); c.Emit(OpCodes.Ldfld, who); c.Emit(OpCodes.Bne_Un, go);
						c.Emit(OpCodes.Ret); c.MarkLabel(go);
					});
				else Mod.Logger.Warn("[StealthWorld] Player.UpdateBiomes not found");
			}
			catch (Exception e) { Mod.Logger.Warn("[StealthWorld] could not patch the biome update: " + e.Message); }
			try
			{
				MethodInfo inst = typeof(Player).GetMethod("ItemCheck_PlayInstruments", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (inst != null) MonoModHooks.Modify(inst, il => { var c = new ILCursor(il); var go = c.DefineLabel(); c.Emit(OpCodes.Ldsfld, typeof(Main).GetField("dedServ")); c.Emit(OpCodes.Brfalse, go); c.Emit(OpCodes.Ret); c.MarkLabel(go); });
			}
			catch (Exception e) { Mod.Logger.Warn("[StealthWorld] could not patch the instrument check: " + e.Message); }
			// the same for the achievement hooks a player's own update reports to while it runs (a dedicated server has no achievements)
			try
			{
				MethodInfo ach = typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleRunning", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo ded2 = typeof(Main).GetField("dedServ");
				if (ach != null && ded2 != null) MonoModHooks.Modify(ach, il => { var c = new ILCursor(il); var go = c.DefineLabel(); c.Emit(OpCodes.Ldsfld, ded2); c.Emit(OpCodes.Brfalse, go); c.Emit(OpCodes.Ret); c.MarkLabel(go); });
			}
			catch (Exception e) { Mod.Logger.Warn("[StealthWorld] could not patch the running achievement: " + e.Message); }
		}

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_STEALTHWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (Environment.GetEnvironmentVariable("LEVY_SUMMONWORLD") == "0" && Environment.GetEnvironmentVariable("LEVY_STEALTH_ONLY") == "1") Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		/// <summary>Standalone entry and the tail of SummonWorldCheck's chain.</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation; int my0 = Main.myPlayer;
			// a dedicated server has no achievement manager: a player's own update (running, ...) reports to it and throws; a stand-in manager is in place for the length of the check
			FieldInfo achField = typeof(Main).GetField("_achievements", BindingFlags.Instance | BindingFlags.NonPublic); object achOrig = achField?.GetValue(Main.instance);
			try
			{
				if (achField != null && achOrig == null) achField.SetValue(Main.instance, new Terraria.Achievements.AchievementManager());
				if (!ModLoader.HasMod("CalamityMod") || !HarnessCalamityProbe.Available) { _mod.Logger.Info("[StealthWorld] OBSERVED Calamity is not loaded: nothing to measure"); }
				else { LevyUtil.SpEmulation = true; Run(); }
			}
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; Probe.On = false; LevyUtil.SpEmulation = sp; Main.myPlayer = my0; Cleanup(); try { achField?.SetValue(Main.instance, achOrig); } catch (Exception) { } }
			_mod.Logger.Info($"[StealthWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[StealthWorld] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[StealthWorld] OBSERVED " + s);

		/// <summary>The commander's own hits on its pinned enemy, from the hook below: damage done, crit, and Calamity's strike flag at the moment of the hit.</summary>
		public sealed class Probe : GlobalNPC
		{
			public static volatile bool On;
			public static readonly List<(int dmg, bool crit, bool strike)> Hits = new();
			public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
			{
				if (!On || npc != _t1 || projectile.owner != 0) return;
				if (projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.IsSoldierShot) return; // the army's shots carry the commander's owner slot: not the commander's own throw
				bool strike = HarnessCalamityProbe.IsStealthStrike(projectile, out _);
				Hits.Add((damageDone, hit.Crit, strike));
			}
		}

		/// <summary>Counts the commander's updates that ran to their last hook (a throw in the middle of Player.Update is caught by tML and ends the update early, which would silently stop the stealth code from running).</summary>
		public sealed class UpdateEnd : ModPlayer
		{
			public static volatile int Ends, Misc, Move, Equips;
			public override void PostUpdate() { if (_running && Player.whoAmI == 0) Ends++; }
			public override void PostUpdateMiscEffects() { if (_running && Player.whoAmI == 0) Misc++; }
			public override void PreUpdateMovement() { if (_running && Player.whoAmI == 0) Move++; }
			public override void SetControls() { if (_running && Player.whoAmI == 0) { Player.controlUseItem = _use; Player.controlUseTile = false; } } // the local player's controls are read from the keyboard every update: a test server has none, so the scripted ones are put back here
			public override void PostUpdateEquips() { if (_running && Player.whoAmI == 0) Equips++; }
		}

		private static int _steps, _ended0, _noThrowNotes;
		private static int ItemOf(string name) => ModContent.TryFind("CalamityMod", name, out ModItem mi) ? mi.Type : 0;

		private static void Arena()
		{
			for (int i = Cx - 230; i <= Cx + 230; i++)
			{
				for (int j = FloorY - 90; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } }
				if (_t1 != null) _t1.active = false; if (_t2 != null) _t2.active = false; _t1 = _t2 = null;
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				if (RosterSystem.Instance != null) RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				Player p = Main.player[0]; if (p != null) { for (int i = 0; i < 3; i++) p.armor[i] = new Item(); p.inventory[0] = new Item(); }
			}
			catch (Exception) { }
		}

		private static NPC MakeTarget(Vector2 at)
		{
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("StealthWorldCheck"), (int)at.X, (int)at.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) return null;
			NPC t = Main.npc[ti];
			t.lifeMax = 100_000_000; t.life = t.lifeMax; t.defense = 0; t.damage = 0; t.knockBackResist = 0f; t.width = 64; t.height = 96;
			t.noGravity = true; t.noTileCollide = true; t.aiStyle = -1; t.friendly = false; t.value = 0f; t.Bottom = at;
			var ht = t.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = t.position;
			return t;
		}

		private static void Pin(NPC t, Vector2 at) { if (t == null || !t.active) return; t.active = true; t.timeLeft = 9999; t.Bottom = at; t.velocity = Vector2.Zero; t.life = t.lifeMax; t.dontTakeDamage = false; var ht = t.GetGlobalNPC<HarnessTarget>(); ht.Pin = t.position; }

		private static SoldierNPC MakeLevy(Vector2 at, int n, SoldierOrder order, int weapon, int head, int body, int legs)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Stealth" + n, CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			Item w = new Item(); w.SetDefaults(weapon); if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
			Item h = new Item(); h.SetDefaults(head); Item b = new Item(); b.SetDefaults(body); Item l = new Item(); l.SetDefaults(legs);
			rec.Gear.Head = h; rec.Gear.Body = b; rec.Gear.Legs = l;
			rec.Gear.SetLocked(SoldierGear.SlotHead, true); rec.Gear.SetLocked(SoldierGear.SlotBody, true); rec.Gear.SetLocked(SoldierGear.SlotLegs, true);
			rec.LoadoutLocked = true; rec.Order = order;
			RosterSystem.Instance.Regulars.Add(rec);
			int bi = RosterSystem.Instance.SpawnBody(rec, at);
			if (bi < 0 || !rec.HasBody) return null;
			Main.npc[bi].Bottom = at; Main.npc[bi].lifeMax = Main.npc[bi].life = 2_000_000; rec.CurrentHp = 2_000_000;
			return Main.npc[bi].ModNPC as SoldierNPC;
		}

		// ---------------------------------------------------------------------------------------- stepping the commander
		private static int _item;
		private static bool _moving, _use; private static bool _prevUse;

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++)
			{
				Player p = Main.player[0];
				p.active = true; p.dead = false; p.name = Commander;
				if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500; p.statLife = p.statLifeMax2;
				p.Bottom = _cmdAt; p.velocity = _moving ? new Vector2(4f, 0f) : Vector2.Zero; p.fallStart = p.fallStart2 = (int)(_cmdAt.Y / 16f); p.noFallDmg = true;
				p.selectedItem = 0; p.controlUseItem = _use; p.releaseUseItem = !_prevUse; _prevUse = _use;
				Main.screenPosition = Vector2.Zero; Main.mouseX = (int)_t1At.X; Main.mouseY = (int)(_t1At.Y - 40f);
				Main.dayTime = false; Main.time = 18000; Main.mouseLeft = _use;
				Pin(_t1, _t1At); Pin(_t2, _t2At);
				for (int k = 0; k < Main.maxNPCs; k++) { NPC e = Main.npc[k]; if (e.active && e != _t1 && e != _t2 && WorldStep.IsNaturalHostile(e)) e.active = false; }
				if (_army != null && Main.GameUpdateCount % 90 == 0) foreach (var s in _army) if (s?.NPC != null && s.NPC.active) s.NPC.AddBuff(BuffID.OnFire, 150);
				int my = Main.myPlayer; Main.myPlayer = 0; _steps++;
				try { WorldStep.Update(); } finally { Main.myPlayer = my; }
			}
		}
		private static List<SoldierNPC> _army;

		private static MethodInfo _itemCheck; private static bool _icLogged;
		private static void ManualItemCheck(Player p)
		{
			if (!_icLogged) { _icLogged = true; var names = new List<string>(); foreach (var m in typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) if (m.Name == "ItemCheck") { var ps = m.GetParameters(); names.Add("ItemCheck(" + string.Join(",", System.Linq.Enumerable.Select(ps, x => x.ParameterType.Name)) + ")"); } Note("the update did not use the item by itself (control " + p.controlUseItem + ", release " + p.releaseUseItem + "); item check methods: " + string.Join(" ", names)); }
			_itemCheck ??= typeof(Player).GetMethod("ItemCheck", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) ?? typeof(Player).GetMethod("ItemCheck", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
			if (_itemCheck == null) return;
			int my = Main.myPlayer; Main.myPlayer = 0;
			try { p.controlUseItem = true; _itemCheck.Invoke(p, _itemCheck.GetParameters().Length == 0 ? null : new object[] { 0 }); }
			catch (Exception e) { if (_noThrowNotes++ < 3) Note("manual item check threw " + (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message + " @ " + ((e.InnerException ?? e).StackTrace ?? "").Split('\n')[0].Trim()); }
			finally { Main.myPlayer = my; }
		}

		private static float[] Bar() => HarnessCalamityProbe.Stealth(Main.player[0]);

		/// <summary>Ticks for an emptied bar to fill, standing or moving (null when it never does within the cap).</summary>
		private static int ChargeTicks(bool moving, int cap = 700)
		{
			HarnessCalamityProbe.SetStealth(Main.player[0], 0f); _moving = moving; _use = false;
			Step(3); HarnessCalamityProbe.SetStealth(Main.player[0], 0f);
			for (int t = 1; t <= cap; t++)
			{
				Step(1);
				float[] s = Bar(); if (s != null && s[1] > 0f && s[0] >= s[1] - 1e-4f) { _moving = false; return t; }
			}
			_moving = false; return -1;
		}

		private sealed class Run3
		{
			public string Name; public int ChargeStand, ChargeMove; public double StrikeProjDmg, NormalProjDmg, StrikeHitMean, NormalHitMean; public int StrikeFlagged, NormalFlagged, Strikes, Normals;
			public float BarMax, BarAfterStrike, BarBeforeStrike, BarAfterNormal; public int AvailBefore, AvailAfter; public int ChargeStandMax, ChargeStandMin;
			public double LevyStealthMax; public int LevyCount;
			public string Aim;
		}

		/// <summary>One throw: use for a tick, then wait the animation out. Reports the projectile the commander's own throw made (damage at spawn, Calamity's strike flag) and the bar before and after.</summary>
		private static bool Throw(out int projDmg, out bool flagged, out float before, out float after, out string aim)
		{
			Player p = Main.player[0]; projDmg = 0; flagged = false; aim = null;
			float[] b = Bar(); before = b != null ? b[0] : -1;
			var seen = new HashSet<int>(); for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) seen.Add(Main.projectile[i].identity * 1000 + i);
			int firstId = -1; bool got = false; after = -1;
			_use = true;
			for (int tick = 0; tick < 6 && (!got || tick <= 2); tick++)
			{
				Step(1);
				if (tick == 0 && p.itemAnimation == 0)
				{
					// the update did not start the use by itself: run the item check the way Player.Update would, with the commander as the local player; the projectile it creates is the one that was not there a moment ago
					var pre = new HashSet<int>(); for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) pre.Add(Main.projectile[i].identity * 1000 + i);
					ManualItemCheck(p);
					for (int i = 0; i < Main.maxProjectiles && !got; i++)
					{
						Projectile pr = Main.projectile[i];
						if (!pr.active || pr.owner != 0 || pr.ModProjectile == null || pr.ModProjectile.Name != "IronFranciscaProj" || pre.Contains(pr.identity * 1000 + i)) continue;
						projDmg = pr.damage; flagged = HarnessCalamityProbe.IsStealthStrike(pr, out _); got = true;
						aim = $"v=({pr.velocity.X:0.#},{pr.velocity.Y:0.#}) at ({pr.Center.X - _cmdAt.X:0},{pr.Center.Y - _cmdAt.Y:0}) target at ({_t1At.X - _cmdAt.X:0},{_t1At.Y - _cmdAt.Y:0})";
					}
				}
				if (tick == 2) { float[] a0 = Bar(); after = a0 != null ? a0[0] : -1; }
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile pr = Main.projectile[i];
					if (got || !pr.active || pr.owner != 0 || pr.ModProjectile == null || pr.ModProjectile.Mod.Name != "CalamityMod" || pr.ModProjectile.Name != "IronFranciscaProj") continue;
					if (pr.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.IsSoldierShot) continue;
					int key = pr.identity * 1000 + i; if (seen.Contains(key)) continue;
					if (got) continue; projDmg = pr.damage; flagged = HarnessCalamityProbe.IsStealthStrike(pr, out _); firstId = pr.identity; got = true;
					aim = $"v=({pr.velocity.X:0.#},{pr.velocity.Y:0.#}) at ({pr.Center.X - _cmdAt.X:0},{pr.Center.Y - _cmdAt.Y:0}) target at ({_t1At.X - _cmdAt.X:0},{_t1At.Y - _cmdAt.Y:0})";
					break;
				}
			}
			if (!got && _noThrowNotes++ < 3) { Player q = Main.player[0]; Note($"no throw seen: held {q.HeldItem.Name}, selected {q.selectedItem}, anim {q.itemAnimation}/{q.itemAnimationMax}, time {q.itemTime}, use {q.controlUseItem}, release {q.releaseUseItem}, dead {q.dead}, noItems {q.noItems}, myPlayer {Main.myPlayer}, stealth {before:0.##}"); }
			_use = false;
			float[] a = Bar(); if (after < 0) after = a != null ? a[0] : -1;
			for (int t = 0; t < 200 && p.ItemAnimationActive; t++) Step(1);
			Step(1);
			return got;
		}

		private static Run3 Measure(string name, int cycles)
		{
			var r = new Run3 { Name = name }; Player p = Main.player[0];
			Probe.Hits.Clear(); Probe.On = true;
			double sSum = 0, nSum = 0; int sN = 0, nN = 0; int sFlag = 0, nFlag = 0; double sHit = 0, nHit = 0; int sHitN = 0, nHitN = 0;
			var stands = new List<int>();
			r.ChargeStand = ChargeTicks(false); stands.Add(r.ChargeStand);
			r.ChargeMove = ChargeTicks(true);
			for (int c = 0; c < cycles; c++)
			{
				int cs = ChargeTicks(false); stands.Add(cs); // full bar
				float[] bar = Bar(); if (bar != null) r.BarMax = bar[1];
				r.AvailBefore += HarnessCalamityProbe.StealthStrikeAvailable(p) ? 1 : 0;
				int h0 = Probe.Hits.Count;
				if (Throw(out int d1, out bool f1, out float b1, out float a1, out string aim)) { sSum += d1; sN++; if (f1) sFlag++; r.BarBeforeStrike = b1; r.BarAfterStrike = a1; r.Aim ??= aim; }
				Step(50);
				for (int i = h0; i < Probe.Hits.Count; i++) { sHit += Probe.Hits[i].dmg; sHitN++; }
				r.AvailAfter += HarnessCalamityProbe.StealthStrikeAvailable(p) ? 1 : 0;
				int h1 = Probe.Hits.Count;
				if (Throw(out int d2, out bool f2, out float b2, out float a2, out _)) { nSum += d2; nN++; if (f2) nFlag++; r.BarAfterNormal = a2; }
				Step(50);
				for (int i = h1; i < Probe.Hits.Count; i++) { nHit += Probe.Hits[i].dmg; nHitN++; }
			}
			Probe.On = false;
			r.Strikes = sN; r.Normals = nN; r.StrikeFlagged = sFlag; r.NormalFlagged = nFlag;
			r.StrikeProjDmg = sN > 0 ? sSum / sN : 0; r.NormalProjDmg = nN > 0 ? nSum / nN : 0;
			r.StrikeHitMean = sHitN > 0 ? sHit / sHitN : 0; r.NormalHitMean = nHitN > 0 ? nHit / nHitN : 0;
			r.ChargeStandMin = stands.Count > 0 ? stands.Min() : -1; r.ChargeStandMax = stands.Count > 0 ? stands.Max() : -1;
			return r;
		}

		/// <summary>Calamity's Elephant Killer projectile reads and writes "the owner's" stealth bar, and a levy's shots are owned by the commander's slot: a levy carrying it must not touch the commander's bar.</summary>
		private static void ElephantKillerPhase(int fran, int head, int body, int legs)
		{
			int ek = ItemOf("ElephantKiller");
			if (ek <= 0) { Note("Elephant Killer is not in this Calamity build"); return; }
			var l = MakeLevy(_cmdAt + new Vector2(5 * 16f, 0f), 300, SoldierOrder.Follow, ek, head, body, legs);
			bool holds = l != null && l.Record != null && l.Record.Gear.Weapon != null && l.Record.Gear.Weapon.type == ek;
			if (!holds) { Note("a levy cannot hold the Elephant Killer (the armory refuses it), so its holdout projectile can never run on the commander's stealth bar"); return; }
			_army.Add(l);
			HarnessCalamityProbe.SetStealth(Main.player[0], 0f); _moving = false; _use = false;
			float peak = 0, max = Bar()[1]; int shots = 0;
			for (int t = 0; t < 400; t++)
			{
				Step(1); float[] bar = Bar(); if (bar != null && bar[0] > peak) peak = bar[0];
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile pr = Main.projectile[i]; if (pr.active && pr.ModProjectile != null && pr.ModProjectile.Name.StartsWith("ElephantKiller")) { shots++; break; } }
			}
			Note($"Elephant Killer levy: its projectiles were out in {shots} of 400 ticks; the commander's bar reached {peak:0.##} of {max:0.##}");
			Check($"a levy carrying the Elephant Killer leaves the commander's stealth bar alone (it reached {peak:0.##} of {max:0.##} in 400 ticks standing; {shots} ticks with its projectile out)", peak >= max - 1e-3f);
		}

		/// <summary>Within 3 percent (Calamity's own damage numbers drift by a point or two when a timed effect on the stand-in runs out).</summary>
		private static bool Near(double x, double y) => Math.Abs(x - y) <= 0.03 * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			_cmdAt = new Vector2(Cx * 16f, FloorY * 16f);
			_t1At = _cmdAt + new Vector2(-14 * 16f, 0f); _t2At = _cmdAt + new Vector2(24 * 16f, 0f);
			int fran = ItemOf("IronFrancisca"), head = ItemOf("StatigelHeadRogue"), body = ItemOf("StatigelArmor"), legs = ItemOf("StatigelGreaves");
			if (fran <= 0 || head <= 0 || body <= 0 || legs <= 0) { Check($"the rogue kit exists (Francisca {fran}, Statigel {head}/{body}/{legs})", false); return; }
			Player p = Main.player[0];
			p.armor[0] = new Item(); p.armor[0].SetDefaults(head); p.armor[1] = new Item(); p.armor[1].SetDefaults(body); p.armor[2] = new Item(); p.armor[2].SetDefaults(legs);
			p.inventory[0] = new Item(); p.inventory[0].SetDefaults(fran); _item = fran;
			Main.rand = new Terraria.Utilities.UnifiedRandom(9100);
			_t1 = MakeTarget(_t1At); _t2 = MakeTarget(_t2At);
			if (_t1 == null || _t2 == null) { Check("the enemies spawn", false); return; }
			for (int i = 0; i < p.buffType.Length; i++) { p.buffType[i] = 0; p.buffTime[i] = 0; } // whatever the checks before this one left on the stand-in (a potion buff counts down for minutes)
			Step(240);
			float[] s0 = Bar();
			Note($"the commander's update reached: equips {UpdateEnd.Equips}, misc effects {UpdateEnd.Misc}, movement {UpdateEnd.Move}, its last hook {UpdateEnd.Ends}, of {_steps} steps; held {Main.player[0].HeldItem.Name}, myPlayer {Main.myPlayer}");
			Check($"the commander's own update runs to its end every tick ({UpdateEnd.Move} of {_steps})", UpdateEnd.Move >= _steps - 2 && _steps > 50);
			Check($"the commander has a stealth bar in the rogue kit (max {(s0 != null ? s0[1] : -1):0.##})", s0 != null && s0[1] > 0f);
			if (s0 == null || s0[1] <= 0f) return;

			var a = Measure("no army", 4);
			_army = new List<SoldierNPC>(); var idle = new List<SoldierNPC>(); var fighters = new List<SoldierNPC>();
			for (int i = 0; i < 10; i++) { var l = MakeLevy(_cmdAt + new Vector2((150 + i * 2) * 16f, 0f), i, SoldierOrder.Hold, fran, head, body, legs); if (l != null) { idle.Add(l); _army.Add(l); } }
			for (int i = 0; i < 8; i++) { var l = MakeLevy(_cmdAt + new Vector2((6 + i) * 16f, 0f), 100 + i, SoldierOrder.Follow, fran, head, body, legs); if (l != null) { fighters.Add(l); _army.Add(l); } }
			Check($"the army spawns ({idle.Count} idle, {fighters.Count} fighting)", idle.Count == 10 && fighters.Count == 8);
			Step(240); // the army settles and engages
			double levyStealth = 0; int levyThrows0 = 0; foreach (var l in fighters) { float[] ls = HarnessCalamityProbe.Stealth(l.Dummy); if (ls != null) levyStealth = Math.Max(levyStealth, ls[1]); }
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile pr = Main.projectile[i]; if (pr.active && pr.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.IsSoldierShot) levyThrows0++; }
			var b = Measure("with army", 4);
			b.LevyStealthMax = levyStealth; b.LevyCount = _army.Count;
			Note($"army: {fighters.Count} fighting + {idle.Count} idle levies; largest levy stealth bar {levyStealth:0.##}; soldier shots in flight at the start of the run {levyThrows0}; the enemy next to them lost {(_t2 != null ? _t2.lifeMax - _t2.life : 0)} this tick");
			ElephantKillerPhase(fran, head, body, legs);
			Cleanup(); _army = null;
			Arena(); _t1 = MakeTarget(_t1At); _t2 = MakeTarget(_t2At);
			Player p2 = Main.player[0]; p2.armor[0] = new Item(); p2.armor[0].SetDefaults(head); p2.armor[1] = new Item(); p2.armor[1].SetDefaults(body); p2.armor[2] = new Item(); p2.armor[2].SetDefaults(legs); p2.inventory[0] = new Item(); p2.inventory[0].SetDefaults(fran);
			Step(60);
			var c = Measure("no army again", 4);

			foreach (var r in new[] { a, b, c })
				Note($"{r.Name}: bar max {r.BarMax:0.##}; fill standing {r.ChargeStand} ticks (min {r.ChargeStandMin}, max {r.ChargeStandMax}), moving {r.ChargeMove} ticks; strike throws {r.Strikes} (flagged {r.StrikeFlagged}), projectile damage {r.StrikeProjDmg:0.##}, enemy took {r.StrikeHitMean:0.##} a hit; plain throws {r.Normals} (flagged {r.NormalFlagged}), projectile damage {r.NormalProjDmg:0.##}, enemy took {r.NormalHitMean:0.##} a hit; bar just before the strike {r.BarBeforeStrike:0.##}, just after {r.BarAfterStrike:0.##}, after the plain throw {r.BarAfterNormal:0.##}; strike available before {r.AvailBefore}/4, after {r.AvailAfter}/4");
			Note("aim: " + (a.Aim ?? "no throw seen"));

			// Calamity's own numbers: 120 ticks standing, 240 moving at the default rates; a strike is flagged and spends the bar; the plain throw after it is not a strike
			Check($"standing fill is Calamity's 2 seconds ({a.ChargeStand} ticks)", a.ChargeStand >= 118 && a.ChargeStand <= 124);
			Check($"moving fill is half as fast ({a.ChargeMove} ticks)", a.ChargeMove >= 236 && a.ChargeMove <= 246);
			Check($"a full bar throws a flagged stealth strike ({a.StrikeFlagged}/{a.Strikes})", a.Strikes >= 3 && a.StrikeFlagged == a.Strikes);
			Check($"the throw on an empty bar is not a strike ({a.NormalFlagged}/{a.Normals})", a.Normals >= 3 && a.NormalFlagged == 0);
			Check($"the strike spends the bar ({a.BarBeforeStrike:0.##} -> {a.BarAfterStrike:0.##})", a.BarBeforeStrike >= a.BarMax - 1e-3f && a.BarAfterStrike <= 0.05f * a.BarMax);
			Check($"a strike hits harder than a plain throw (projectile damage {a.StrikeProjDmg:0.##} vs {a.NormalProjDmg:0.##})", a.StrikeProjDmg >= 2.5 * a.NormalProjDmg && a.NormalProjDmg > 0);
			Check($"the commander's throws reach the enemy ({a.StrikeHitMean:0.##} and {a.NormalHitMean:0.##} a hit)", a.StrikeHitMean > 0 && a.NormalHitMean > 0);
			// the army must change nothing
			Check($"the measurement repeats with no army (fill {a.ChargeStand}/{c.ChargeStand}, {a.ChargeMove}/{c.ChargeMove}; strike damage {a.StrikeProjDmg:0.##}/{c.StrikeProjDmg:0.##})", Math.Abs(a.ChargeStand - c.ChargeStand) <= 1 && Math.Abs(a.ChargeMove - c.ChargeMove) <= 1 && Near(a.StrikeProjDmg, c.StrikeProjDmg) && Near(a.NormalProjDmg, c.NormalProjDmg));
			Check($"with the army: the bar fills as fast (standing {a.ChargeStand} vs {b.ChargeStand}, moving {a.ChargeMove} vs {b.ChargeMove})", Math.Abs(a.ChargeStand - b.ChargeStand) <= 1 && Math.Abs(a.ChargeMove - b.ChargeMove) <= 1 && b.ChargeStandMax - b.ChargeStandMin <= 3);
			Check($"with the army: a strike does the same damage ({a.StrikeProjDmg:0.##} vs {b.StrikeProjDmg:0.##}; plain {a.NormalProjDmg:0.##} vs {b.NormalProjDmg:0.##})", Near(a.StrikeProjDmg, b.StrikeProjDmg) && Near(a.NormalProjDmg, b.NormalProjDmg));
			Check($"with the army: a strike is still flagged and spends the bar ({b.StrikeFlagged}/{b.Strikes}; {b.BarBeforeStrike:0.##} -> {b.BarAfterStrike:0.##}; plain throws flagged {b.NormalFlagged})", b.Strikes == a.Strikes && b.StrikeFlagged == b.Strikes && b.NormalFlagged == 0 && b.BarBeforeStrike >= b.BarMax - 1e-3f && b.BarAfterStrike <= 0.05f * b.BarMax);
			Check($"with the army: the same bar size ({a.BarMax:0.##} vs {b.BarMax:0.##})", Math.Abs(a.BarMax - b.BarMax) < 1e-3f);
			Check($"the levies run Calamity's stealth too (a levy bar of {b.LevyStealthMax:0.##})", b.LevyStealthMax > 0);
			Check($"the commander's own update ran to its end every tick of the whole check ({UpdateEnd.Move} of {_steps})", UpdateEnd.Move >= _steps - 2);
			RecordedPhase(fran);
		}

		// ---------------------------------------------------------------------------------------- the same throws, recorded
		/// <summary>The fight recorder on the commander while it throws at an enemy flagged a boss: the file must name the weapon that fired, carry Calamity's stealth flag on the strikes and not on the plain throws, count the crits, sample the bar, and the report read from it must say so.</summary>
		private static void RecordedPhase(int fran)
		{
			string tmp = Path.Combine(Path.GetTempPath(), "levy_stealthworld_" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(tmp);
			try
			{
				Player p = Main.player[0];
				p.armor[0] = new Item(); p.armor[0].SetDefaults(ItemOf("StatigelHeadRogue")); p.armor[1] = new Item(); p.armor[1].SetDefaults(ItemOf("StatigelArmor")); p.armor[2] = new Item(); p.armor[2].SetDefaults(ItemOf("StatigelGreaves")); p.inventory[0] = new Item(); p.inventory[0].SetDefaults(fran);
				if (_t1 == null || !_t1.active) { _t1 = MakeTarget(_t1At); }
				_t1.boss = true; Step(30);
				FightRecorder.OutDirOverride = tmp; FightRecorder.GzipOverride = true; FightRecorder.ForceSync = true; FightRecorder.IgnoreInactive = true; FightRecPost.TestForce = true; FightRecPost.TestSummary = false;
				string path = FightRecorder.Start(p, "Stealth Dummy", out string err);
				Check("the recorder starts on the rogue commander (" + (path ?? err) + ")", path != null);
				if (path == null) return;
				Probe.On = true; Probe.Hits.Clear();
				int strikes = 0, plains = 0;
				for (int c = 0; c < 4; c++)
				{
					ChargeTicks(false);
					if (Throw(out _, out bool f1, out _, out _, out _) && f1) strikes++;
					Step(40);
					if (Throw(out _, out bool f2, out _, out _, out _) && !f2) plains++;
					Step(40);
				}
				Step(70); Probe.On = false;
				string rec = FightRecorder.Stop("self-check over");
				bool idle = FightRecPost.WaitIdle(30000);
				Check($"the recording is saved and its report job finished (strike throws {strikes}, plain throws {plains})", rec != null && File.Exists(rec) && idle && strikes >= 3 && plains >= 3);
				if (rec == null || !File.Exists(rec)) return;
				var st = FightRecReader.Read(rec, true);
				WeaponStat w = null; st.Weapons.TryGetValue("Iron Francisca", out w);
				Note($"recorded: format {st.Format}, commander damage on the boss {st.CmdBossDmg}, weapons [{string.Join(", ", System.Linq.Enumerable.Select(st.Weapons.Values, x => x.Name + " " + x.Hits + " hits, " + x.StealthHits + " stealth, " + x.Crits + " crits"))}], stealth bar samples {st.StealthPct.Count} (max {(st.StealthPct.Count > 0 ? System.Linq.Enumerable.Max(st.StealthPct) : -1)}%), stealth share {st.StealthShare:0.##}");
				Check("the recording is format " + FightRecFormat.Version + " and the commander's hits are on the boss", st.Format == FightRecFormat.Version && st.CmdBossDmg > 0);
				Check("the weapon that fired is named, not only the one held (Iron Francisca)", w != null && w.Hits > 0);
				Check($"the stealth strikes are flagged and the plain throws are not ({(w != null ? w.StealthHits : -1)} flagged of {(w != null ? w.Hits : -1)} hits)", w != null && w.StealthKnown && w.StealthHits >= 3 && w.StealthHits < w.Hits && st.StealthShare > 0.3);
				Check("the crit count is recorded", w != null && w.CritKnown && w.CritKnownHits == w.Hits);
				Check($"the stealth bar is sampled once a second ({st.StealthPct.Count} samples, up to {(st.StealthPct.Count > 0 ? System.Linq.Enumerable.Max(st.StealthPct) : -1)}%)", st.StealthBarSeen && st.StealthPct.Count >= 5 && System.Linq.Enumerable.Max(st.StealthPct) >= 90);
				string txt = Path.ChangeExtension(rec, ".txt");
				string text = File.Exists(txt) ? File.ReadAllText(txt) : "";
				Check("the report is written next to the recording and names the weapon and the stealth share", text.Contains("Iron Francisca") && text.Contains("Stealth strikes:") && !text.Contains("Stealth strikes: not recorded"));
			}
			finally
			{
				FightRecorder.OutDirOverride = null; FightRecorder.GzipOverride = null; FightRecorder.ForceSync = false; FightRecorder.IgnoreInactive = false; FightRecPost.TestForce = false; FightRecPost.TestSummary = null;
				if (FightRecorder.On) FightRecorder.Stop("self-check over");
				if (_t1 != null) _t1.boss = false;
				try { Directory.Delete(tmp, true); } catch (Exception) { }
			}
		}
	}
}
