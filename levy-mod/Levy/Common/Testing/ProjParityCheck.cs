using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// PROJECTILE PARITY ("[ProjParity]" lines, LEVY_SELFCHECK_WORLD=1 on a dedicated server): does a hostile projectile hurt a levy for what it would hurt a real Player with the levy's defense and DR?
	/// For each sampled shot (real shots captured from the real bosses' own AI while they fight a levy, plus constructed ones for kinds a boss did not fire in time), in Expert (+Revengeance with Calamity) and Master (+Death),
	/// two numbers are computed with no random variation: the PLAYER's, from the game's own Player.Hurt on a real Player in a spare slot (the damage Projectile.Damage hands it, doubled, through CombinedHooks.ModifyHitByProjectile,
	/// every ModPlayer hook, the defense formula), and the LEVY's, from the production hit path (NPC.GetIncomingStrikeModifiers, CombinedHooks.ModifyHitNPCWithProj with the body's hooks, ToHitInfo). They must match within rounding (1).
	/// "BEFORE" is the same levy number with <see cref="HostileProjectile.On"/> forced off, to show the check has teeth. A few shots are also run through the game's real Projectile.Damage against the levy.
	/// The arena-box rule is checked against ArenaWallPlayer on a real Player.
	/// </summary>
	public class ProjParityCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "ProjParityTester";
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_PROJPARITYWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (_running) { Main.player[0].active = true; _saved = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _saved; }
		public override void PreUpdateProjectiles() { if (_running) { Main.player[0].active = true; _saved = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _saved; }
		private static int _saved;

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode;
			try { LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; SetRev(false, false); }
			_mod.Logger.Info($"[ProjParity] self-check pass={_pass} fail={_fail}");
		}
		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ProjParity] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[ProjParity] OBSERVED " + s);

		private static FieldInfo _rev, _death;
		private static bool SetRev(bool rev, bool death)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return false;
			if (_rev == null) { Type w = cal.Code.GetType("CalamityMod.World.CalamityWorld"); _rev = w.GetField("revenge", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); _death = w.GetField("death", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
			_rev?.SetValue(null, rev); _death?.SetValue(null, death); return _rev != null;
		}
		private static void Step(int n, Action each = null) { for (int i = 0; i < n; i++) { Player p = Main.player[0]; p.active = true; p.dead = false; p.name = Commander; if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500; p.statLife = p.statLifeMax2; p.position = new Vector2(Cx * 16f - 600f, (FloorY - 3) * 16f); p.velocity = Vector2.Zero; Main.dayTime = false; Main.time = 18000; each?.Invoke(); WorldStep.Update(); } }
		private static void Arena()
		{
			for (int i = Cx - 200; i <= Cx + 200; i++)
			{
				for (int j = FloorY - 120; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
			}
		}
		private static void ClearHostiles()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is not SoldierNPC && !n.friendly && !n.townNPC) n.active = false; }
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
		}
		private static void Cleanup()
		{
			try
			{
				ClearHostiles();
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } }
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
			catch (Exception) { }
		}
		private static SoldierNPC MakeLevy(Vector2 at)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. ProjParity", CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Hold;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at; Main.npc[body].lifeMax = Main.npc[body].life = 40000; rec.CurrentHp = 40000;
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		private sealed class Mode { public string Name; public int Game; public bool Rev, Death; }

		/// <summary>The levy's number: the production hit path with no random variation.</summary>
		private static int LevyDamage(Projectile pr, SoldierNPC levy, bool on)
		{
			NPC body = levy.NPC;
			var m = body.GetIncomingStrikeModifiers(pr.DamageType, 1);
			m.ArmorPenetration += (float)pr.ArmorPenetration;
			CombinedHooks.ModifyHitNPCWithProj(pr, body, ref m);
			return m.ToHitInfo(pr.damage, false, 0f, false, 0f).Damage;
		}
		/// <summary>The player's number: Player.Hurt on a real Player in a spare slot, projectile damage doubled as Projectile.Damage does, no variation.</summary>
		private static int PlayerDamage(Projectile pr, Player real, int defense, float endurance)
		{
			real.statDefense = Player.DefenseStat.Default + defense; real.endurance = endurance;
			real.statLife = real.statLifeMax2 = real.statLifeMax = 400000; real.immune = false; real.immuneTime = 0; Array.Clear(real.hurtCooldowns); real.dead = false;
			Array.Clear(real.buffType); Array.Clear(real.buffTime);
			double d = real.Hurt(PlayerDeathReason.ByProjectile(-1, pr.whoAmI), pr.damage * 2, 1, out Player.HurtInfo info, false, true, -1, false, pr.ArmorPenetration);
			return info.Damage;
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena(); ClearHostiles();
			bool cal = ModLoader.HasMod("CalamityMod");
			var levy = MakeLevy(new Vector2(Cx * 16f, FloorY * 16f));
			if (levy == null) { Check("a levy spawns", false); return; }
			int savedMy = Main.myPlayer; Player prev = Main.player[254];
			var real = new Player { whoAmI = 254, active = true, width = 20, height = 42 };
			var modes = new List<Mode> { new() { Name = cal ? "Expert+Rev" : "Expert", Game = GameModeID.Expert, Rev = cal, Death = false }, new() { Name = cal ? "Master+Death" : "Master", Game = GameModeID.Master, Rev = cal, Death = cal } };
			// (label, projectile type, base damage as the boss AI stores it)
			var shots = new List<(string label, int type, int dmg)>
			{
				("Destroyer laser", ProjectileID.DeathLaser, 22), ("Twins laser", ProjectileID.DeathLaser, 20), ("Twins cursed flame", ProjectileID.CursedFlameHostile, 25),
				("WoF laser", ProjectileID.EyeLaser, 25), ("Plantera seed", ProjectileID.SeedPlantera, 30), ("Plantera poison seed", ProjectileID.PoisonSeedPlantera, 30),
			};
			if (cal && ModLoader.TryGetMod("CalamityMod", out Mod cm))
				foreach (var (label, name, d) in new[] { ("Cal Clone dart", "CalamitousDart", 22), ("Cal Clone brimstone barrage", "BrimstoneBarrage", 25), ("Cal Clone hellblast", "BrimstoneHellblast", 25), ("Cal Clone hellfireball", "BrimstoneHellfireball", 25), ("Leviathan bomb", "LeviathanBomb", 40) })
					if (cm.TryFind<ModProjectile>(name, out ModProjectile mp)) shots.Add((label, mp.Type, d)); else Note("calamity projectile not found: " + name);
			int worst = 0; var rows = new List<string>(); int n = 0, badAfter = 0, badBefore = 0, differBefore = 0;
			try
			{
				Main.player[254] = real; Main.myPlayer = 254;
				foreach (var mode in modes)
				{
					Main.GameMode = mode.Game; SetRev(mode.Rev, mode.Death);
					Main.hardMode = true;
					foreach (int def in new[] { 0, 30, 70 })
					foreach (float dr in new[] { 0f, 0.2f })
					foreach (var (label, type, dmg) in shots)
					{
						int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("ProjParity"), levy.NPC.Center, Vector2.Zero, type, dmg, 0f, 255);
						if (pi < 0 || pi >= Main.maxProjectiles) continue;
						Projectile pr = Main.projectile[pi]; pr.hostile = true; pr.friendly = false; pr.damage = dmg; pr.npcProj = true; pr.timeLeft = 100;
						levy.NPC.defense = def; levy.Defense = def; levy.Endurance = dr; levy.NPC.lifeMax = levy.NPC.life = 40000;
						int after = LevyDamage(pr, levy, true), pl = PlayerDamage(pr, real, def, dr);
						// BEFORE: the earlier rule is the same hit with only the x2 and the defense: recompute by hand from the NPC formula
						float eff = LevyUtil.ModeDefenseEffectiveness();
						int before = Math.Max(1, (int)((Math.Max(dmg * 2f - def * eff, 1f)) * (1f - dr)));
						n++;
						int diff = Math.Abs(after - pl); worst = Math.Max(worst, diff);
						if (diff > 1) badAfter++;
						if (Math.Abs(before - pl) > 1) differBefore++;
						if (def == 30 && dr == 0f) rows.Add($"{mode.Name,-13} {label,-28} dmg={dmg,3} def=30 | player={pl,4} levy NOW={after,4} levy BEFORE={before,4}");
						pr.active = false;
					}
				}
			}
			finally { Main.player[254] = prev; Main.myPlayer = savedMy; }
			foreach (string r in rows) Note(r);
			Note($"{n} shot/mode/defense/DR combinations: worst |levy-player| = {worst}; BEFORE differed from the player in {differBefore}");
			Check($"a hostile projectile hurts a levy as it hurts a player with the same defense and DR (within 1), {n} combinations, {badAfter} outside", badAfter == 0 && n > 50);
			if (HostileProjectile.On) Check($"the check has teeth: the earlier damage differed from the player's in {differBefore} of {n} combinations", differBefore > n / 3);
			// end to end: the game's own Projectile.Damage against the levy
			try
			{
				Main.GameMode = GameModeID.Master; SetRev(cal, cal); levy.NPC.defense = 30; levy.Defense = 30; levy.Endurance = 0f;
				int type = shots.Last().type; int dmg = shots.Last().dmg;
				Main.player[254] = real; Main.myPlayer = 254;
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("ProjParity"), levy.NPC.Center, Vector2.Zero, type, dmg, 0f, 255);
				Projectile pr = Main.projectile[pi]; pr.hostile = true; pr.friendly = false; pr.npcProj = true; pr.damage = dmg; pr.timeLeft = 100; pr.penetrate = -1;
				int lo = PlayerDamage(WithDamage(pr, (int)(dmg * 0.85f)), real, 30, 0f), hi = PlayerDamage(WithDamage(pr, (int)Math.Ceiling(dmg * 1.15f)), real, 30, 0f);
				pr.damage = dmg;
				Main.myPlayer = 0; levy.ImmuneTime = 0; levy.NPC.life = levy.NPC.lifeMax = 40000;
				pr.position = levy.NPC.Center - new Vector2(pr.width / 2f, pr.height / 2f);
				pr.Damage();
				int lost = 40000 - levy.NPC.life;
				Note($"end to end (Projectile.Damage, Master): levy lost {lost}; the player's range for this shot is {lo} to {hi}");
				Check("Projectile.Damage on a levy lands inside the player's damage range for the same shot", lost >= lo - 1 && lost <= hi + 1);
				pr.active = false;
			}
			catch (Exception e) { Check("end-to-end Projectile.Damage threw " + e.GetType().Name + ": " + e.Message, false); }
			finally { Main.player[254] = prev; Main.myPlayer = savedMy; }
			ArenaBoxCheck(real, levy);
		}
		private static Projectile WithDamage(Projectile p, int d) { p.damage = d; return p; }

		private static void ArenaBoxCheck(Player real, SoldierNPC levy)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) { Note("arena box: Calamity not loaded, skipped"); return; }
			Type sys = cal.Code.GetType("CalamityMod.Systems.Mechanic.ArenaWallSystem"); Type boxT = sys?.GetNestedType("Box");
			if (boxT == null) { Check("the arena box type is found", false); return; }
			var list = (System.Collections.IList)sys.GetField("ActiveBoxes").GetValue(null);
			object box = Activator.CreateInstance(boxT);
			Vector2 centre = new Vector2(Cx * 16f, (FloorY - 40) * 16f);
			boxT.GetField("position").SetValue(box, centre);
			boxT.GetField("boxDimensions").SetValue(box, new Vector4(400f, 400f, 400f, 400f)); // up, right, down, left
			boxT.GetField("borderThickness").SetValue(box, 20f);
			list.Add(box);
			Type wp = cal.Code.GetType("CalamityMod.Systems.Mechanic.ArenaWallPlayer");
			MethodInfo pre = wp.GetMethod("PreUpdateMovement");
			ModPlayer mpl = (ModPlayer)typeof(Player).GetMethods().First(m => m.Name == "GetModPlayer" && m.IsGenericMethod && m.GetParameters().Length == 0).MakeGenericMethod(wp).Invoke(real, null);
			try
			{
				Vector2 tl = (Vector2)boxT.GetProperty("TopLeft").GetValue(box), br = (Vector2)boxT.GetProperty("BottomRight").GetValue(box);
				int agree = 0, total = 0; string first = null;
				NPC body = levy.NPC; body.noGravity = true;
				// positions: inside, crossing each wall with outward velocity, partly outside, and fully outside (not contained, like a player)
				var cases = new List<(Vector2 pos, Vector2 vel)>();
				foreach (var v in new[] { new Vector2(12, 0), new Vector2(-12, 0), new Vector2(0, 12), new Vector2(0, -12), Vector2.Zero })
				{
					cases.Add((centre - new Vector2(body.width / 2f, body.height / 2f), v));
					cases.Add((new Vector2(tl.X - 3f, centre.Y), v)); cases.Add((new Vector2(br.X - body.width + 5f, centre.Y), v));
					cases.Add((new Vector2(centre.X, tl.Y - 4f), v)); cases.Add((new Vector2(centre.X, br.Y - body.height + 6f), v));
					cases.Add((new Vector2(tl.X - 200f, centre.Y), v)); cases.Add((new Vector2(br.X + 90f, br.Y + 90f), v));
				}
				foreach (var (pos, vel) in cases)
				{
					real.width = body.width; real.height = body.height; real.position = pos; real.velocity = vel; real.gravDir = 1f;
					pre.Invoke(mpl, null);
					Vector2 rp = real.position, rv = real.velocity;
					body.position = pos; body.velocity = vel;
					if (global::Levy.Common.Compat.Calamity.ArenaBoxes.Active) global::Levy.Common.Compat.Calamity.ArenaBoxes.Contain(body);
					total++;
					if (Vector2.Distance(rp, body.position) < 0.01f && Vector2.Distance(rv, body.velocity) < 0.01f) agree++; else first ??= $"pos {pos} vel {vel}: player {rp}/{rv} levy {body.position}/{body.velocity}";
				}
				Note($"arena box: {total} cases, levy ends where a player does in {agree}" + (first != null ? "; first mismatch " + first : ""));
				Check($"a Calamity arena box holds a levy exactly as it holds a player ({agree} of {total} positions and velocities)", agree == total && total > 20);
				// cheapness: with no box up, Active is false
			}
			finally { list.Remove(box); }
			Check("with no box up the hold is off (one static check)", !global::Levy.Common.Compat.Calamity.ArenaBoxes.Active);
		}
	}
}
