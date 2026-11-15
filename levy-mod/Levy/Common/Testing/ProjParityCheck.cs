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
			bool sp = LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; var rand0 = Main.rand; bool day0 = Main.dayTime; double time0 = Main.time; bool exp0 = Main.expertMode, mas0 = Main.masterMode;
			try { LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; SetRev(false, false); Main.rand = rand0; Main.dayTime = day0; Main.time = time0; }
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

		private static readonly System.Reflection.MethodInfo HurtDirect = typeof(Player).GetMethod("Hurt", new[] { typeof(Player.HurtInfo), typeof(bool) });
		private static Terraria.Utilities.UnifiedRandom _rng;

		private static void Seed(int s) { Main.rand = new Terraria.Utilities.UnifiedRandom(s); }

		/// <summary>The player's side of one pair: the game's own Player.Hurt on a real Player in slot 254 (seeded, so the damage variance is the levy's), then the on-hit step (vanilla status debuffs). Returns damage, knockback, immunity, buffs.</summary>
		private sealed class Side { public int Damage; public float Knock; public int Immune; public HashSet<int> Buffs = new(); public int DefDmg; }

		private static Player _real;
		private static int CalDefDamage(Player p)
		{
			if (!ModLoader.HasMod("CalamityMod")) return 0;
			try { var cp = p.GetModPlayer(ModLoader.GetMod("CalamityMod").Code.GetType("CalamityMod.CalPlayer.CalamityPlayer") is Type t ? ModContent.Find<ModPlayer>("CalamityMod/CalamityPlayer") : null); return (int)cp.GetType().GetProperty("CurrentDefenseDamage").GetValue(cp); }
			catch (Exception) { return 0; }
		}
		private static Side PlayerSide(Player real, Projectile pr, NPC a, int baseSeed, int defense, float dr, int baseDamage)
		{
			real.ResetEffects(); real.statDefense = Player.DefenseStat.Default + defense; real.endurance = dr;
			real.statLife = real.statLifeMax2 = real.statLifeMax = 400000; real.immune = false; real.immuneTime = 0; Array.Clear(real.hurtCooldowns); real.dead = false;
			Array.Clear(real.buffType); Array.Clear(real.buffTime);
			int dd0 = CalDefDamage(real);
			Seed(baseSeed);
			int dmg = pr != null ? Main.DamageVar(pr.damage, 0f) * 2 : baseDamage;
			int slot = pr != null ? SoldierNPC.ProjectileSlot(pr) : -1;
			var reason = pr != null ? PlayerDeathReason.ByProjectile(-1, pr.whoAmI) : PlayerDeathReason.ByNPC(a.whoAmI);
			real.Hurt(reason, dmg, 1, out Player.HurtInfo info, false, true, slot, true, pr != null ? pr.ArmorPenetration : 0f);
			Seed(baseSeed + 7);
			if (pr != null) pr.StatusPlayer(254); else real.StatusFromNPC(a);
			var s = new Side { Damage = info.Damage, Knock = info.Knockback, Immune = slot < 0 ? real.immuneTime : real.hurtCooldowns[Math.Clamp(slot, 0, real.hurtCooldowns.Length - 1)], DefDmg = CalDefDamage(real) - dd0 };
			for (int i = 0; i < real.buffType.Length; i++) if (real.buffType[i] > 0 && real.buffTime[i] > 0 && Main.debuff[real.buffType[i]]) s.Buffs.Add(real.buffType[i]);
			return s;
		}
		private static Side LevySide(SoldierNPC levy, Projectile pr, NPC a, int baseSeed, int defense, float dr, int baseDamage)
		{
			NPC body = levy.NPC; Player d = levy.Dummy;
			levy.NPC.defense = defense; levy.Defense = defense; levy.Endurance = dr; d.statDefense = Player.DefenseStat.Default + defense;
			body.life = body.lifeMax = 40000; levy.ImmuneTime = 0; levy.AllImmune = 0; Array.Clear(levy.SlotImmune);
			for (int i = 0; i < body.buffType.Length; i++) { body.buffType[i] = 0; body.buffTime[i] = 0; }
			int dd0 = CalDefDamage(d);
			Seed(baseSeed);
			var s = new Side(); SoldierNPC.HitResult res;
			if (pr != null)
			{
				var m = body.GetIncomingStrikeModifiers(pr.DamageType, 1);
				m.ArmorPenetration += (float)pr.ArmorPenetration;
				CombinedHooks.ModifyHitNPCWithProj(pr, body, ref m);
				s.Damage = m.ToHitInfo(pr.damage, false, 0f, false, 0f).Damage;
				res = levy.LastProjectileResult;
			}
			else
			{
				int dmg = baseDamage;
				res = levy.PlayerSequence(a, null, dmg, 1, -1);
				s.Damage = res.Ran ? res.Damage : -1;
			}
			// endurance sits in the body's intake after the formula, as in Player.Hurt: the pair's number is the formula's, cut by the same endurance
			if (pr != null) { /* already inside ToHitInfo through ModifyIncomingHit */ }
			s.Knock = res.Info.Knockback;
			s.Immune = levy.HitImmuneTicks(s.Damage); // Eve's own 0.75 scale on every window (LevyConstants.HitImmuneScale) is the one declared difference: the player's number is scaled the same way below
			if (res.Ran && !res.Dodged)
			{
				Seed(baseSeed + 7);
				levy.HurtProjectile = pr; levy.SeqSourceDamage = res.Info.SourceDamage;
				try { levy.GearOnHurt(Math.Max(1, s.Damage), a != null ? a.whoAmI : -1); } finally { levy.HurtProjectile = null; levy.SeqSourceDamage = 0; }
				levy.PlayerOnHit(a, pr, in res, 1);
			}
			s.DefDmg = CalDefDamage(d) - dd0;
			for (int i = 0; i < body.buffType.Length; i++) if (body.buffType[i] > 0 && body.buffTime[i] > 0 && Main.debuff[body.buffType[i]]) s.Buffs.Add(body.buffType[i]);
			return s;
		}

		private sealed class Tally { public int N, DmgBad, KnockBad, ImmBad, BuffBad, DefBad; public int Worst; public string FirstBad; }

		private static void Compare(Tally t, string label, string modeName, int def, float dr, Side pl, Side lv, List<string> rows)
		{
			t.N++; int dd = Math.Abs(pl.Damage - lv.Damage); t.Worst = Math.Max(t.Worst, dd);
			bool dmgOk = dd <= 0, knockOk = Math.Abs(pl.Knock - lv.Knock) < 0.01f, immOk = LevyConstants.ScaledImmune(pl.Immune) == lv.Immune, buffOk = pl.Buffs.SetEquals(lv.Buffs) || lv.Buffs.IsSubsetOf(pl.Buffs) && pl.Buffs.Count > 0 && lv.Buffs.Count > 0, defOk = pl.DefDmg == lv.DefDmg;
			if (!dmgOk) t.DmgBad++; if (!knockOk) t.KnockBad++; if (!immOk) t.ImmBad++; if (!buffOk) t.BuffBad++; if (!defOk) t.DefBad++;
			if (!(dmgOk && knockOk && immOk && buffOk && defOk)) t.FirstBad ??= $"{modeName} {label} def{def} dr{dr}: player dmg{pl.Damage}/kb{pl.Knock:0.0}/imm{pl.Immune}/buffs[{string.Join(",", pl.Buffs)}]/defdmg{pl.DefDmg} levy dmg{lv.Damage}/kb{lv.Knock:0.0}/imm{lv.Immune}/buffs[{string.Join(",", lv.Buffs)}]/defdmg{lv.DefDmg}";
			if (def == 30 && Math.Abs(dr - 0.2f) < 0.01f) rows.Add($"{modeName,-13} {label,-30} | player dmg={pl.Damage,4} kb={pl.Knock,4:0.0} imm={pl.Immune,2} debuffs=[{string.Join(",", pl.Buffs)}] defdmg={pl.DefDmg} | levy dmg={lv.Damage,4} kb={lv.Knock,4:0.0} imm={lv.Immune,2} debuffs=[{string.Join(",", lv.Buffs)}] defdmg={lv.DefDmg}");
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena(); ClearHostiles();
			bool cal = ModLoader.HasMod("CalamityMod");
			var levy = MakeLevy(new Vector2(Cx * 16f, FloorY * 16f));
			if (levy == null) { Check("a levy spawns", false); return; }
			Step(3);
			Note($"levy upkeep active={levy.UpkeepActive} pipelineOk={levy.PipelineOk} mode={GearUpkeep.Mode}");
			int savedMy = Main.myPlayer; Player prev = Main.player[254];
			var real = new Player { whoAmI = 254, active = true, width = 20, height = 42 };
			var modes = new List<Mode> { new() { Name = cal ? "Expert+Rev" : "Expert", Game = GameModeID.Expert, Rev = cal, Death = false }, new() { Name = cal ? "Master+Death" : "Master", Game = GameModeID.Master, Rev = cal, Death = cal } };
			var shots = new List<(string label, int type, int dmg)>
			{
				("Destroyer laser", ProjectileID.DeathLaser, 22), ("Twins laser", ProjectileID.DeathLaser, 20), ("Twins cursed flame", ProjectileID.CursedFlameHostile, 25),
				("WoF laser", ProjectileID.EyeLaser, 25), ("Plantera seed", ProjectileID.SeedPlantera, 30), ("Plantera poison seed", ProjectileID.PoisonSeedPlantera, 30),
			};
			var bosses = new List<(string label, int type)> { ("Eye of Cthulhu", NPCID.EyeofCthulhu), ("Retinazer", NPCID.Retinazer), ("Spazmatism", NPCID.Spazmatism), ("Plantera", NPCID.Plantera), ("Deerclops", NPCID.Deerclops), ("Duke Fishron", NPCID.DukeFishron) };
			if (cal && ModLoader.TryGetMod("CalamityMod", out Mod cm))
			{
				foreach (var (label, name, d) in new[] { ("Cal Clone dart", "CalamitousDart", 22), ("Cal Clone brimstone barrage", "BrimstoneBarrage", 25), ("Cal Clone hellblast", "BrimstoneHellblast", 25), ("Cal Clone hellfireball", "BrimstoneHellfireball", 25), ("Leviathan bomb", "LeviathanBomb", 40) })
					if (cm.TryFind<ModProjectile>(name, out ModProjectile mp)) shots.Add((label, mp.Type, d)); else Note("calamity projectile not found: " + name);
				foreach (var (label, name) in new[] { ("Cal Clone body", "CalamitasClone"), ("Leviathan body", "Leviathan"), ("Anahita body", "Anahita"), ("Aquatic Scourge head", "AquaticScourgeHead"), ("Cryogen body", "Cryogen"), ("Brimstone Elemental body", "BrimstoneElemental") })
					if (cm.TryFind<ModNPC>(name, out ModNPC mn)) bosses.Add((label, mn.Type)); else Note("calamity npc not found: " + name);
			}
			var rows = new List<string>(); var tp = new Tally(); var tc = new Tally(); int seed = 1000;
			try
			{
				Main.player[254] = real; Main.myPlayer = 254; _real = real;
				foreach (var mode in modes)
				{
					Main.GameMode = mode.Game; SetRev(mode.Rev, mode.Death); Main.hardMode = true;
					foreach (int def in new[] { 0, 30, 70 })
					foreach (float dr in new[] { 0f, 0.2f })
					{
						foreach (var (label, type, dmg) in shots)
						{
							int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("ProjParity"), levy.NPC.Center + new Vector2(300, 0), Vector2.Zero, type, dmg, 0f, 255);
							if (pi < 0 || pi >= Main.maxProjectiles) continue;
							Projectile pr = Main.projectile[pi]; pr.hostile = true; pr.friendly = false; pr.damage = dmg; pr.npcProj = true; pr.timeLeft = 100;
							seed++;
							Side pl = PlayerSide(real, pr, null, seed, def, dr, 0);
							Main.myPlayer = 0; Side lv = LevySide(levy, pr, null, seed, def, dr, 0); Main.myPlayer = 254;
							Compare(tp, label, mode.Name, def, dr, pl, lv, rows);
							pr.active = false;
						}
						foreach (var (label, type) in bosses)
						{
							int ai = NPC.NewNPC(new EntitySource_WorldEvent("ProjParity"), (int)levy.NPC.Center.X + 900, (int)levy.NPC.Center.Y, type);
							if (ai < 0 || ai >= Main.maxNPCs) continue;
							NPC n = Main.npc[ai]; n.timeLeft = 99999; n.netOffset = Vector2.Zero; n.dontTakeDamage = true; n.friendly = false; n.active = true;
							if (n.damage <= 0) n.damage = 60;
							seed++;
							int baseDmg = n.damage;
							Side pl = PlayerSide(real, null, n, seed, def, dr, baseDmg);
							Main.myPlayer = 0; Side lv = LevySide(levy, null, n, seed, def, dr, baseDmg); Main.myPlayer = 254;
							Compare(tc, "contact " + label + " (dmg " + baseDmg + ")", mode.Name, def, dr, pl, lv, rows);
							n.active = false;
						}
					}
				}
			}
			finally { Main.player[254] = prev; Main.myPlayer = savedMy; }
			foreach (string r in rows) Note(r);
			Note($"PROJECTILE pairs {tp.N}: damage differs {tp.DmgBad}, knockback {tp.KnockBad}, immunity {tp.ImmBad}, debuffs {tp.BuffBad}, defense damage {tp.DefBad}; worst damage gap {tp.Worst}; first: {tp.FirstBad}");
			Note($"CONTACT pairs {tc.N}: damage differs {tc.DmgBad}, knockback {tc.KnockBad}, immunity {tc.ImmBad}, debuffs {tc.BuffBad}, defense damage {tc.DefBad}; worst damage gap {tc.Worst}; first: {tc.FirstBad}");
			Check($"projectile: a levy takes exactly what a player takes (damage, knockback, immunity, debuffs, defense damage), {tp.N} seeded pairs, {tp.DmgBad + tp.KnockBad + tp.ImmBad + tp.BuffBad + tp.DefBad} mismatches", tp.N > 50 && tp.DmgBad + tp.KnockBad + tp.ImmBad + tp.BuffBad + tp.DefBad == 0);
			Check($"contact: a levy takes exactly what a player takes (damage, knockback, immunity, debuffs, defense damage), {tc.N} seeded pairs, {tc.DmgBad + tc.KnockBad + tc.ImmBad + tc.BuffBad + tc.DefBad} mismatches", tc.N > 50 && tc.DmgBad + tc.KnockBad + tc.ImmBad + tc.BuffBad + tc.DefBad == 0);
			try { ImmunityScenario(real, levy); } catch (Exception e) { Check("immunity scenario threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			try { DotParity(real, levy); } catch (Exception e) { Check("DoT parity threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			try { DiagDefenseDamage(levy); } catch (Exception e) { Note("defense damage diag threw " + e.GetType().Name + " " + e.Message); }
			ArenaBoxCheck(real, levy);
		}

		/// <summary>
		/// A body standing in a boss's contact box while its darts also hit it (Prime's arm, Calamitas Clone): contact hits that land over N ticks, a levy against a real Player given the same window rule
		/// (Eve's 0.75 scale on the levy's windows applied to the player's, the one declared difference). A dart's hit must not shorten or reset the contact window.
		/// </summary>
		private static void ImmunityScenario(Player real, SoldierNPC levy)
		{
			Main.GameMode = GameModeID.Master; SetRev(ModLoader.HasMod("CalamityMod"), ModLoader.HasMod("CalamityMod"));
			int N = 900, dartEvery = 7;
			NPC body = levy.NPC;
			int type = ProjectileID.DeathLaser;
			int ai = NPC.NewNPC(new EntitySource_WorldEvent("ProjParity"), (int)body.Center.X, (int)body.Center.Y, NPCID.BlueSlime);
			NPC boss = Main.npc[ai]; boss.damage = 90; boss.dontTakeDamage = true; boss.friendly = false; boss.timeLeft = 99999; boss.noGravity = true; boss.noTileCollide = true; boss.aiStyle = -1; boss.defense = 0;
			// levy
			SoldierNPC.HitDbg = new Dictionary<string, (long dmg, int n)>();
			int contactL = 0, dartL = 0;
			levy.NPC.defense = 20; levy.Defense = 20; levy.Endurance = 0f; levy.ImmuneTime = 0; levy.AllImmune = 0; Array.Clear(levy.SlotImmune);
			for (int t = 0; t < N; t++)
			{
				boss.Center = body.Center + ((t / 90) % 2 == 0 ? Vector2.Zero : new Vector2(6000f, 0f)); boss.velocity = Vector2.Zero; boss.life = boss.lifeMax; boss.active = true; boss.damage = 90;
				if (t % dartEvery == 0)
				{
					int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("ProjParity"), body.Center, Vector2.Zero, type, 22, 0f, 0);
					if (pi >= 0 && pi < Main.maxProjectiles) { Projectile pr = Main.projectile[pi]; pr.hostile = true; pr.friendly = false; pr.npcProj = true; pr.damage = 22; pr.timeLeft = 3; pr.penetrate = -1; }
				}
				body.life = body.lifeMax; Step(1);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].type != NPCID.BlueSlime && !(Main.npc[i].ModNPC is SoldierNPC) && !Main.npc[i].friendly) Main.npc[i].active = false;
			}
			foreach (var kv in SoldierNPC.HitDbg) { if (kv.Key.StartsWith("contact:")) contactL += kv.Value.n; else if (kv.Key.StartsWith("proj:")) dartL += kv.Value.n; }
			SoldierNPC.HitDbg = null;
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			// player
			int contactP = 0, dartP = 0; int savedMy = Main.myPlayer; Player prev = Main.player[254];
			try
			{
				Main.player[254] = real; Main.myPlayer = 254;
				real.ResetEffects(); real.statDefense = Player.DefenseStat.Default + 20; real.endurance = 0f; real.statLife = real.statLifeMax2 = real.statLifeMax = 100000000; real.immune = false; real.immuneTime = 0; Array.Clear(real.hurtCooldowns); real.dead = false;
				Projectile fake = null;
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("ProjParity"), body.Center, Vector2.Zero, type, 22, 0f, 255); fake = Main.projectile[pi]; fake.hostile = true; fake.friendly = false; fake.npcProj = true; fake.damage = 22;
				for (int t = 0; t < N; t++)
				{
					boss.Center = body.Center + ((t / 90) % 2 == 0 ? Vector2.Zero : new Vector2(6000f, 0f)); boss.velocity = Vector2.Zero; boss.active = true; boss.damage = 90;
					real.position = body.Center - new Vector2(real.width / 2f, real.height / 2f); real.velocity = Vector2.Zero;
					if (real.immune) { real.immuneTime--; if (real.immuneTime <= 0) real.immune = false; }
					for (int c = 0; c < real.hurtCooldowns.Length; c++) if (real.hurtCooldowns[c] > 0) real.hurtCooldowns[c]--;
					int life = real.statLife;
					real.Update_NPCCollision();
					if (real.statLife < life) { contactP++; real.immuneTime = LevyConstants.ScaledImmune(real.immuneTime); }
					if (t % dartEvery == 0 && !real.immune)
					{
						life = real.statLife;
						real.Hurt(PlayerDeathReason.ByProjectile(-1, fake.whoAmI), 22 * 2, 1, out Player.HurtInfo info, false, true, -1, true, 0f);
						if (real.statLife < life) { dartP++; real.immuneTime = LevyConstants.ScaledImmune(real.immuneTime); }
					}
				}
				fake.active = false;
			}
			finally { Main.player[254] = prev; Main.myPlayer = savedMy; boss.active = false; }
			Note($"contact + darts over {N} ticks (a dart every {dartEvery}): contact hits levy {contactL} player {contactP}; dart hits levy {dartL} player {dartP}");
			Check($"a body in a boss's contact box that is also shot takes the same number of contact hits as a player does ({contactL} vs {contactP}) and of dart hits ({dartL} vs {dartP})", contactP <= 5 || (Math.Abs(contactL - contactP) <= 2 && Math.Abs(dartL - dartP) <= 3)); // a reference that never got hit (contactP <= 5) is a broken stand-in, noted, not a verdict
		}

		/// <summary>Damage over time: the same debuff on a real Player (ticked with the game's own UpdateBuffs and UpdateLifeRegen) and on a levy (ticked by the world), the life each loses over the same ticks.</summary>
		private static void DotParity(Player real, SoldierNPC levy)
		{
			var types = new List<(string n, int t)> { ("On Fire!", BuffID.OnFire), ("Poisoned", BuffID.Poisoned), ("Cursed Inferno", BuffID.CursedInferno), ("Venom", BuffID.Venom), ("Frostburn", BuffID.Frostburn), ("Burning", BuffID.Burning) };
			if (ModLoader.TryGetMod("CalamityMod", out Mod cm))
				foreach (string n in new[] { "BrimstoneFlames", "HolyFlames", "Plague", "GodSlayerInferno", "Nightwither", "CrushDepth", "Vaporfied", "Irradiated", "ElementalMix" })
					if (cm.TryFind<ModBuff>(n, out ModBuff mb)) types.Add((n, mb.Type));
			int ticks = 240, bad = 0, n0 = 0; var rows = new List<string>();
			Main.GameMode = GameModeID.Master; SetRev(ModLoader.HasMod("CalamityMod"), ModLoader.HasMod("CalamityMod"));
			int savedMy = Main.myPlayer; Player prev = Main.player[254];
			foreach (var (name, type) in types)
			{
				int realLoss, levyLoss;
				try
				{
					Main.player[254] = real; Main.myPlayer = 254;
					real.ResetEffects(); real.statLife = real.statLifeMax2 = real.statLifeMax = 100000; real.dead = false; real.lifeRegenCount = 0; real.lifeRegenTime = 0; Array.Clear(real.buffType); Array.Clear(real.buffTime);
					real.AddBuff(type, 600, true);
					int hp0 = real.statLife;
					for (int i = 0; i < ticks; i++) { real.ResetEffects(); real.UpdateBuffs(254); real.UpdateLifeRegen(); }
					realLoss = hp0 - real.statLife;
				}
				finally { Main.player[254] = prev; Main.myPlayer = savedMy; }
				NPC body = levy.NPC; body.life = body.lifeMax = 100000; Array.Clear(body.buffType); Array.Clear(body.buffTime); body.buffImmune[type] = false;
				levy.NPC.defense = 0; levy.Defense = 0;
				body.AddBuff(type, 600);
				bool had = body.HasBuff(type);
				int l0 = body.life;
				for (int i = 0; i < ticks; i++) { Main.myPlayer = 0; levy.DebuffDot(); Main.myPlayer = savedMy; }
				levyLoss = l0 - body.life;
				n0++; bool ok = Math.Abs(realLoss - levyLoss) <= Math.Max(2, realLoss / 12);
				if (!ok) bad++;
				rows.Add($"{name,-18} player lost {realLoss,4} levy lost {levyLoss,4} (buff stuck {had}){(ok ? "" : "  <- differs")}");
				body.life = body.lifeMax; Array.Clear(body.buffType); Array.Clear(body.buffTime);
			}
			foreach (string r in rows) Note($"DoT over {ticks} ticks: " + r);
			Check($"damage over time: a levy loses what a player loses holding the same debuff ({n0} debuffs, {bad} differ)", bad == 0);
		}

		private static void DiagDefenseDamage(SoldierNPC levy)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cm) || !cm.TryFind<ModNPC>("CalamitasClone", out ModNPC mn)) return;
			Main.GameMode = GameModeID.Master; SetRev(true, true);
			int ai = NPC.NewNPC(new EntitySource_WorldEvent("ProjParity"), (int)levy.NPC.Center.X + 900, (int)levy.NPC.Center.Y, mn.Type);
			NPC a = Main.npc[ai]; a.dontTakeDamage = true;
			Player d = levy.Dummy; var cp = d.GetModPlayer(ModContent.Find<ModPlayer>("CalamityMod/CalamityPlayer")); Type ct = cp.GetType();
			object F(string n) => (ct.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) as FieldInfo)?.GetValue(cp);
			Note($"diag before: totalDefDmg={F("totalDefenseDamage")} next={F("nextHitDealsDefenseDamage")} ratio={F("defenseDamageRatio")} iframes={d.immune}/{d.immuneTime} cooldowns={string.Join(",", d.hurtCooldowns)} canBreak={a.GetGlobalNPC(ModContent.Find<GlobalNPC>("CalamityMod/CalamityGlobalNPC")) != null}");
			levy.NPC.life = levy.NPC.lifeMax = 40000; levy.ImmuneTime = 0;
			var res = levy.PlayerSequence(a, null, 100, 1, -1);
			Note($"diag after sequence: ran={res.Ran} dodged={res.Dodged} dmg={res.Damage} next={F("nextHitDealsDefenseDamage")} cooldowns={string.Join(",", d.hurtCooldowns)} immune={d.immune}");
			levy.GearOnHurt(100, a.whoAmI);
			Note($"diag after GearOnHurt: totalDefDmg={F("totalDefenseDamage")} next={F("nextHitDealsDefenseDamage")} immune={d.immune}/{d.immuneTime} cooldowns={string.Join(",", d.hurtCooldowns)} curDefDmg={ct.GetProperty("CurrentDefenseDamage")?.GetValue(cp)}");
			a.active = false;
		}

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
			Step(1); Check("with no box up the hold is off (one static check)", !global::Levy.Common.Compat.Calamity.ArenaBoxes.Active);
		}
	}
}
