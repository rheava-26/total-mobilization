using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of summon staves, logged as "[SummonWorld]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the shipworld stage runs it on vanilla and on Calamity).
	/// A levy carries one staff and fights a pinned, tough enemy ten tiles away in a flat arena; the real Main.Update steps the world with slot 0 standing in as the local player while NPCs and projectiles update,
	/// which is the single-player owner situation (Main.myPlayer = 0, minions owned by the levy's dummy slot). For every staff: its minion is out within 200 ticks, is still out after 900, and the enemy has been hurt.
	/// The Dormant Brimseeker (a summoner shot the minion comes out of, with an aura when the flock is full) must also have its Brimseeker out and its aura cast. The Calamity set is one staff per way a
	/// staff used to fail: a summoner shot, a minion whose part finds its parent by owner (Deathstare Rod), by UUID (Mounted Scanner), a minion that asks its owner for ammo (Black Hawk Remote),
	/// a cast of two different minions (Lilies of Finality), a part with its own extra ai values (Dazzling Stabber), a shot that bursts into shots (Gamma Heart). Throwaway soldiers only; nothing is saved.
	/// </summary>
	public class SummonWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail;
		private static MethodInfo _update; private static Mod _mod;
		private static NPC _target; private static Vector2 _targetAt, _cmdAt;
		private static bool _local; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Ticks = 900;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_SUMMONWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		// the server drops a slot with no connection every tick, and the NPCs despawn with nobody on: slot 0 is kept alive and made local only while NPCs and projectiles update
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; PinTarget(); if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_running && _local) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running && _local) Main.myPlayer = _savedMy; }

		private static void Drive()
		{
			if (!_armed || _done || !Main.dedServ) return;
			if (++_calls < 6) return;
			// with Mechanization loaded its world check runs first and hands over to this one at its end (its own check stops the server when it finishes): never two checks stepping the world at once
			if (ModLoader.HasMod("Mechanization") && Environment.GetEnvironmentVariable("LEVY_MECHWORLD") != "0") return;
			RunIfArmed();
		}

		/// <summary>Runs the check now when this boot armed it (the world check of a companion mod calls this at its end, so the checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; }
			_mod.Logger.Info($"[SummonWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[SummonWorld] {(ok ? "PASS" : "FAIL")} {name}"); }

		private static void Step(int n)
		{
			_update ??= typeof(Main).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
			for (int i = 0; i < n; i++) { KeepCommander(); _update.Invoke(Main.instance, new object[] { new GameTime() }); }
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = "SummonTester";
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void PinTarget()
		{
			if (_target == null || !_target.active) return;
			_target.active = true; _target.timeLeft = 9999; _target.Bottom = _targetAt; _target.velocity = Vector2.Zero; _target.life = _target.lifeMax; _target.dontTakeDamage = false;
		}

		private static void Arena()
		{
			for (int i = Cx - 70; i <= Cx + 70; i++)
			{
				for (int j = FloorY - 80; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void Clear()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
			}
			if (_target != null) _target.active = false;
			_target = null;
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == "SummonTester");
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
		}

		private static int ItemOf(string mod, string name)
		{
			if (mod == null) return ItemID.Search.TryGetId(name, out int id) ? id : 0;
			return ModContent.TryFind(mod, name, out ModItem mi) ? mi.Type : 0;
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			bool cal = ModLoader.HasMod("CalamityMod");
			var set = new List<(string mod, string name, string label, string need)> { (null, "SlimeStaff", "Slime Staff", ""), (null, "ImpStaff", "Imp Staff", ""), (null, "HornetStaff", "Hornet Staff", "") };
			if (cal)
			{
				set.Add(("CalamityMod", "DormantBrimseeker", "Dormant Brimseeker", "Bab+Aura"));
				set.Add(("CalamityMod", "DeathstareRod", "Deathstare Rod", ""));
				set.Add(("CalamityMod", "MountedScanner", "Mounted Scanner", ""));
				set.Add(("CalamityMod", "BlackHawkRemote", "Black Hawk Remote", ""));
				set.Add(("CalamityMod", "LiliesOfFinality", "Lilies of Finality", ""));
				set.Add(("CalamityMod", "DazzlingStabberStaff", "Dazzling Stabber Staff", ""));
				set.Add(("CalamityMod", "GammaHeart", "Gamma Heart", ""));
			}
			foreach (var (mod, name, label, need) in set)
			{
				int type = ItemOf(mod, name);
				if (type <= 0) { Check($"{label}: the item exists", false); continue; }
				try { OneStaff(type, label, need); }
				finally { Clear(); }
			}
		}

		private static void OneStaff(int type, string label, string need)
		{
			Clear();
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Summoner",
				CommanderName = "SummonTester", CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			Item w = new Item(); w.SetDefaults(type);
			if (!rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { Check($"{label}: a levy can hold it", false); return; }
			rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) { Check($"{label}: the levy spawns", false); return; }
			Main.npc[body].Bottom = at;
			_targetAt = new Vector2(Cx * 16f + 10 * 16f, FloorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("SummonWorldCheck"), (int)_targetAt.X, (int)_targetAt.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) { Check($"{label}: the enemy spawns", false); return; }
			_target = Main.npc[ti];
			_target.lifeMax = 1_000_000; _target.life = 1_000_000; _target.defense = 0; _target.damage = 0; _target.knockBackResist = 0f; _target.width = 64; _target.height = 96;
			_target.noGravity = true; _target.noTileCollide = true; _target.aiStyle = -1; _target.friendly = false; _target.value = 0f; _target.Bottom = _targetAt;
			var ht = _target.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _target.position; // the lab's pinned dummy: no AI, no despawn
			int firstOut = -1, bab = 0, aura = 0, aliveEnd = 0, dmg = 0;
			for (int tick = 1; tick <= Ticks; tick++)
			{
				PinTarget(); // the enemy is whole at the start of every tick: what is missing at the end of it is what the levy's minions did
				Step(1);
				if (_target.active && _target.life > 0 && _target.life < _target.lifeMax) dmg += _target.lifeMax - _target.life;
				int alive = 0, babNow = 0, auraNow = 0;
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || !SoldierMinions.IsDriven(p) || !SoldierMinions.OwnedBy(p, body)) continue;
					alive++;
					string n = p.ModProjectile?.Name ?? "";
					if (n.EndsWith("Bab")) babNow++; else if (n.EndsWith("Aura")) auraNow++;
				}
				if (alive > 0 && firstOut < 0) firstOut = tick;
				bab = Math.Max(bab, babNow); aura = Math.Max(aura, auraNow);
				if (tick == Ticks) aliveEnd = alive;
			}
			Check($"{label}: its minion is out within 200 ticks (first at tick {firstOut})", firstOut > 0 && firstOut <= 200);
			Check($"{label}: its minion is still out after {Ticks} ticks ({aliveEnd} out)", aliveEnd > 0);
			Check($"{label}: the enemy was hurt ({dmg})", dmg > 0);
			if (need == "Bab+Aura")
			{
				Check($"{label}: the Brimseeker itself is out ({bab} at most)", bab > 0);
				Check($"{label}: the aura of a full flock was cast ({aura} at most)", aura > 0);
			}
		}
	}
}
