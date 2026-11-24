using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// AOE DAMAGE ("[AoeDmg]" lines, LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_AOEDMG_ONLY=1 runs it alone): what a levy takes from the big area attacks of Wrath of the Gods (the Avatar of Emptiness's collapsing star, Nameless
	/// Deity's beams and stars) against what a player in the same defense and the same Chalice of the Blood God takes.
	/// <para/>The cause this pins (Eve's video, Sgt. Alice standing inside the star, "5" and "(-435)" over her): Calamity's Chalice of the Blood God cuts every hit to 5 damage and takes the rest from the wearer's life over the next
	/// seconds. The levy took the cut and the buffer filled on its dummy ("(-435)"), but nothing took the buffer's life from the body. <see cref="SoldierNPC.Bleedout_On"/> is the fix; the "BEFORE" rows switch it off.
	/// <para/>Each trial pins one hostile shot (the damage the real attack has in Master + Death, read from the mod's own AI values, or a constructed number when the mod is not loaded) on the target and steps the world:
	/// the LEVY is hit through the production path (Projectile.Damage, CanBeHitByProjectile, the hit sequence, GearOnHurt, the drain), the PLAYER is a real Player in a spare slot hit through the game's own Player.Hurt with the same
	/// immunity window and the Chalice's drain run every tick. Both start at the same life with the same defense and endurance.
	/// </summary>
	public class AoeDmgCheck : ModSystem
	{
		private static bool _armed, _done, _running, _drive; private static int _pass, _fail, _calls; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "AoeDmgTester";
		private static int _saved;
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_AOEDMGWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0; _calls = 0;
		}
		public override void OnWorldUnload() { _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (_running) { Main.player[0].active = true; _saved = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _saved; }
		public override void PreUpdateProjectiles() { if (_running) { Main.player[0].active = true; _saved = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _saved; }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; var rand0 = Main.rand; bool day0 = Main.dayTime; double time0 = Main.time;
			try { LevyUtil.SpEmulation = true; SoldierNPC.BleedoutForce = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
			finally { _running = false; SoldierNPC.BleedoutForce = false; LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); SoldierNPC.BleedoutLab = true; Main.GameMode = gm; Main.hardMode = hm; SetRev(false, false); Main.rand = rand0; Main.dayTime = day0; Main.time = time0; }
			_mod.Logger.Info($"[AoeDmg] self-check pass={_pass} fail={_fail}");
		}
		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[AoeDmg] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[AoeDmg] OBSERVED " + s);

		// ------------------------------------------------------------------------------------------------ plumbing
		private static FieldInfo _rev, _death;
		private static void SetRev(bool rev, bool death)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
			if (_rev == null) { Type w = cal.Code.GetType("CalamityMod.World.CalamityWorld"); _rev = w.GetField("revenge", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); _death = w.GetField("death", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
			_rev?.SetValue(null, rev); _death?.SetValue(null, death);
		}
		private static void Step(int n, Action<int> each = null)
		{
			for (int i = 0; i < n; i++)
			{
				Player p = Main.player[0]; p.active = true; p.dead = false; p.name = Commander; if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
				p.statLife = p.statLifeMax2; p.position = new Vector2(Cx * 16f - 700f, (FloorY - 3) * 16f); p.velocity = Vector2.Zero; Main.dayTime = false; Main.time = 18000;
				each?.Invoke(i); WorldStep.Update();
				for (int k = 0; k < Main.maxNPCs; k++) { NPC n2 = Main.npc[k]; if (WorldStep.IsNaturalHostile(n2) && !(n2.ModNPC?.Mod?.Name == "NoxusBoss")) n2.active = false; }
			}
		}
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
		private static int ItemOf(string mod, string name) => mod == null ? (ItemID.Search.TryGetId(name, out int id) ? id : 0) : (ModContent.TryFind(mod, name, out ModItem mi) ? mi.Type : 0);
		private static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }

		private static SoldierNPC MakeLevy(string name, Vector2 at, int chalice, int hpOverride)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = name, CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
			if (chalice > 0) { rec.Gear.Accessories[0] = Make(chalice); rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Hold; rec.HoldPos = at; rec.LoadoutLocked = true;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			NPC n = Main.npc[body]; n.Bottom = at;
			if (hpOverride > 0) { n.lifeMax = n.life = hpOverride; rec.CurrentHp = hpOverride; }
			return n.ModNPC as SoldierNPC;
		}

		/// <summary>A hostile shot pinned over <paramref name="at"/>: a stand-in projectile type with the real attack's damage, so the number is the mod's and the hit path is the game's.</summary>
		private static Projectile Shot(int dmg, Vector2 at, int size, bool beam)
		{
			int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("AoeDmg"), at, Vector2.Zero, ProjectileID.DeathLaser, dmg, 0f, 255);
			if (pi < 0 || pi >= Main.maxProjectiles) return null;
			Projectile pr = Main.projectile[pi]; pr.hostile = true; pr.friendly = false; pr.npcProj = true; pr.damage = dmg; pr.penetrate = -1; pr.tileCollide = false; pr.timeLeft = 5000; pr.usesLocalNPCImmunity = false; pr.usesIDStaticNPCImmunity = false;
			if (size > 0) { pr.width = size; pr.height = size; }
			pr.Center = at; return pr;
		}
		/// <summary>Holds the shot over the target and runs the game's own Projectile.Damage on it (the stand-in's AI does not call it; a real boss shot does, every tick it lives).</summary>
		private static void Pin(Projectile pr, Vector2 at)
		{
			if (pr == null) return; pr.active = true; pr.Center = at; pr.velocity = Vector2.Zero; pr.timeLeft = 5000; pr.hostile = true;
			int my = Main.myPlayer; Main.myPlayer = pr.owner; try { pr.Damage(); } finally { Main.myPlayer = my; } // Projectile.Damage strikes NPCs only when the shot's owner is the machine's own player (255 on a server)
		}

		// ------------------------------------------------------------------------------------------------ the player's side
		private static bool _diag; private static int _diagTrials, _lastN;
		private static Player _real;
		private static ModPlayer CalPlayer(Player p) => p.GetModPlayer(ModContent.Find<ModPlayer>("CalamityMod/CalamityPlayer"));
		private static void SetChalice(Player p, bool on) { try { var m = CalPlayer(p); m?.GetType().GetField("chaliceOfTheBloodGod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(m, on); } catch (Exception) { } }
		private static MethodInfo _hb;
		private static void Drain(Player p)
		{
			_hb ??= ModLoader.GetMod("CalamityMod").Code.GetType("CalamityMod.Items.Accessories.ChaliceOfTheBloodGod").GetMethod("HandleBleedout", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_hb.Invoke(null, new object[] { p });
		}

		private sealed class Trial
		{
			public string Label; public int Dmg; public bool Chalice;
			public int Hits, FirstHit, LostAt1s, LostAt3s, LostAt10s; public double Buffer0; public string Adds = "";
			public int Total => LostAt10s;
		}

		/// <summary>The player: the same shot, the same immunity window, the Chalice's drain every tick, over <paramref name="ticks"/>. iframes = ticks of immunity after a hit (before the declared 0.75 scale).</summary>
		private static Trial PlayerRun(Player real, int dmg, int defense, float dr, bool chalice, int ticks, int iframes = -1)
		{
			var t = new Trial { Label = "player", Dmg = dmg, Chalice = chalice };
			int savedMy = Main.myPlayer; Player prev = Main.player[254];
			try
			{
				Main.player[254] = real; Main.myPlayer = 254; _real = real;
				real.ResetEffects(); real.statDefense = Player.DefenseStat.Default + defense; real.endurance = dr;
				real.statLife = real.statLifeMax2 = real.statLifeMax = 400000; real.immune = false; real.immuneTime = 0; Array.Clear(real.hurtCooldowns); real.dead = false; Array.Clear(real.buffType); Array.Clear(real.buffTime);
				if (ModLoader.HasMod("CalamityMod")) { var m = CalPlayer(real); var ct = m.GetType(); ct.GetField("chaliceBleedoutBuffer")?.SetValue(m, 0.0); ct.GetField("chaliceDamagePointPartialProgress")?.SetValue(m, 0.0); }
				SetChalice(real, chalice);
				Main.rand = new Terraria.Utilities.UnifiedRandom(77);
				var fake = Shot(dmg, new Vector2(Cx * 16f, FloorY * 16f - 200f), 0, false);
				int hp0 = real.statLife;
				for (int k = 0; k < ticks; k++)
				{
					if (real.immuneTime > 0) { real.immuneTime--; if (real.immuneTime <= 0) real.immune = false; }
					if (!real.immune && real.immuneTime <= 0)
					{
						int life = real.statLife; double bufBefore = chalice ? ChaliceBleed.Buffer(real) : 0.0;
						real.Hurt(PlayerDeathReason.ByProjectile(-1, fake.whoAmI), Main.DamageVar(dmg, 0f) * 2, 1, out Player.HurtInfo info, false, true, -1, true, 0f);
						if (real.statLife < life) { fake.StatusPlayer(254); if (t.Hits < 6 && chalice) t.Adds += (ChaliceBleed.Buffer(real) - bufBefore).ToString("0") + " (def " + real.statDefense.Positive + ") "; t.Hits++; if (t.Hits == 1) t.FirstHit = life - real.statLife; real.immuneTime = iframes >= 0 ? iframes : LevyConstants.ScaledImmune(real.immuneTime); if (real.immuneTime > 0) real.immune = true; }
					}
					if (chalice && ModLoader.HasMod("CalamityMod")) { SetChalice(real, true); Drain(real); }
					if (k == 59) t.LostAt1s = hp0 - real.statLife; if (k == 179) t.LostAt3s = hp0 - real.statLife;
				}
				t.LostAt10s = hp0 - real.statLife;
				fake.active = false;
			}
			finally { Main.player[254] = prev; Main.myPlayer = savedMy; }
			return t;
		}

		/// <summary>The levy: the same shot pinned over him, the world stepped. Life is read from the body (hits and the Chalice drain both land there).</summary>
		private static Trial LevyRun(SoldierNPC levy, int dmg, int defense, float dr, bool chalice, int ticks, int size = 0)
		{
			var t = new Trial { Label = "levy", Dmg = dmg, Chalice = chalice };
			NPC body = levy.NPC; Player d = levy.Dummy;
			levy.NPC.defense = defense; levy.Defense = defense; levy.Endurance = dr; d.statDefense = Player.DefenseStat.Default + defense;
			body.life = body.lifeMax; if (levy.Record != null) levy.Record.CurrentHp = body.life; // life is topped up every tick: the trial counts what was taken (hits plus the drain), not what was left
			levy.ImmuneTime = 0; levy.AllImmune = 0; Array.Clear(levy.SlotImmune); levy.BleedoutTaken = 0;
			for (int i = 0; i < body.buffType.Length; i++) { body.buffType[i] = 0; body.buffTime[i] = 0; }
			ChaliceBleed.Clear(d);
			SoldierNPC.HitDbg = new Dictionary<string, (long dmg, int n)>();
			var pr = Shot(dmg, body.Center, size, false);
			int Taken() { long h = 0; foreach (var kv in SoldierNPC.HitDbg) if (kv.Key.StartsWith("proj:")) h += kv.Value.dmg; return (int)h + levy.BleedoutTaken; }
			Step(ticks, k =>
			{
				levy.Endurance = dr; levy.Defense = defense; body.defense = defense; d.statDefense = Player.DefenseStat.Default + defense; // the stat pass re-reads the gear once a second: hold the trial's numbers so both sides take the same hit
				if (k > 0) { body.life = body.lifeMax; if (levy.Record != null) levy.Record.CurrentHp = body.life; }
				Pin(pr, body.Center); body.velocity = Vector2.Zero;
				if (_diagTrials < 4) { int n = 0; foreach (var kv in SoldierNPC.HitDbg) n += kv.Value.n; if (n != _lastN) { _lastN = n; var r = levy.LastProjectileResult; Note($"diag trial {_diagTrials} tick {k}: hit #{n} ran={r.Ran} dodged={r.Dodged} seqDamage={r.Damage} sourceDamage={r.Info.SourceDamage} taken={Taken()} buffer={ChaliceBleed.Buffer(d):0.0} immune={levy.ImmuneTime}/{levy.AllImmune} dummyImmune={d.immuneTime} life={body.life}/{body.lifeMax} defense={d.statDefense.Positive}/{levy.Defense}"); } }
				if (k == 599 && _diagTrials < 4) { Note($"diag trial {_diagTrials} end: hits {_lastN} seqRuns={SoldierNPC.SeqRuns} seqDodges={SoldierNPC.SeqDodges} seqErrors={SoldierNPC.SeqErrors} vetoed={HostileContact.ProjVetoed} lastRan={levy.LastProjectileResult.Ran} projActive={pr?.active} projHostile={pr?.hostile} dummyImmune={d.immune}/{d.immuneTime} lifeNow={body.life}"); _diagTrials++; _lastN = 0; }
				if (k == 60) t.LostAt1s = Taken(); if (k == 180) t.LostAt3s = Taken();
				if (!_diag && (k == 5 || k == 40)) { _diag = k == 40; bool coll = pr != null && pr.Colliding(pr.Hitbox, body.Hitbox); bool? can = pr != null ? ProjectileLoader.CanHitNPC(pr, body) : null; Note($"diag tick {k}: proj active={pr?.active} type={pr?.type} hostile={pr?.hostile} dmg={pr?.damage} hitbox={pr?.Hitbox} body={body.Hitbox} colliding={coll} friendly={body.friendly} dontTakeDamage={body.dontTakeDamage} dontTakeDamageFromHostiles={body.dontTakeDamageFromHostiles} immortal={body.immortal} immune={levy.ImmuneTime}/{levy.AllImmune} hitdbg={SoldierNPC.HitDbg.Count} life={body.life}/{body.lifeMax} canHit={can}"); }
			});
			t.LostAt10s = Taken();
			foreach (var kv in SoldierNPC.HitDbg) if (kv.Key.StartsWith("proj:")) { t.Hits += kv.Value.n; if (kv.Value.n > 0) t.FirstHit = (int)(kv.Value.dmg / kv.Value.n); }
			SoldierNPC.HitDbg = null; if (pr != null) pr.active = false;
			t.Buffer0 = levy.BleedoutTaken;
			return t;
		}

		// ------------------------------------------------------------------------------------------------ the attacks
		private static int Read(Mod nox, string cls, string prop)
		{
			try
			{
				Type t = nox.Code.GetType(cls); var p = t?.GetProperty(prop, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				return p == null ? -1 : (int)p.GetValue(null);
			}
			catch (Exception e) { return -1000 - (e.InnerException?.GetType().Name.Length ?? 0); }
		}
		private const string Avatar = "NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.AvatarOfEmptiness", Nameless = "NoxusBoss.Content.NPCs.Bosses.NamelessDeity.NamelessDeityBoss";

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena(); ClearHostiles();
			bool cal = ModLoader.HasMod("CalamityMod"), nox = ModLoader.TryGetMod("NoxusBoss", out Mod noxMod);
			Check("the Chalice drain is found in Calamity (or Calamity is not loaded)", ChaliceBleed.Available == cal);
			if (!cal) { Note("Calamity is not loaded: the Chalice part is skipped (the drain is inert without it)"); Check("without Calamity the setting changes nothing: Bleedout_On is off", !SoldierNPC.Bleedout_On); return; }
			int chaliceType = ItemOf("CalamityMod", "ChaliceOfTheBloodGod");
			Check("the Chalice of the Blood God item is found", chaliceType > 0); if (chaliceType <= 0) return;
			Main.GameMode = GameModeID.Master; SetRev(true, true); Main.hardMode = true;

			var levy = MakeLevy("Pvt. Chalice", new Vector2(Cx * 16f, FloorY * 16f), chaliceType, 0);
			if (levy == null) { Check("a levy with the Chalice spawns", false); return; }
			Step(8);
			Note($"levy upkeep active={levy.UpkeepActive} pipelineOk={levy.PipelineOk} mode={GearUpkeep.Mode} lifeMax(body)={levy.NPC.lifeMax} chaliceOnDummy={(CalPlayer(levy.Dummy)?.GetType().GetField("chaliceOfTheBloodGod")?.GetValue(CalPlayer(levy.Dummy)))}");
			var real = new Player { whoAmI = 254, active = true, width = 20, height = 42 };

			// -------- the attack list: the mod's own numbers for this difficulty
			var attacks = new List<(string label, int dmg, int iframes)>();
			if (nox)
			{
				foreach (var (l, p) in new[] { ("Avatar: dying star burst (the sun that collapses)", "DyingStarBurstDamage"), ("Avatar: star iron shards", "IronDamage"), ("Avatar: stellar remnant", "StellarRemnantDamage"), ("Avatar: void blot", "VoidBlotDamage"), ("Avatar: disgusting star", "DisgustingStarDamage"), ("Avatar: dimension comet", "CometDamage"), ("Avatar: antimatter blast", "AntimatterBlastDamage"), ("Avatar: frost column", "FrostColumnDamage"), ("Avatar: ice blast", "IceBlastDamage"), ("Avatar: stolen planetoid", "StolenPlanetoidDamage"), ("Avatar: blood torrent", "BloodTorrentDamage"), ("Avatar: redirecting rubble", "RedirectingRubbleDamage"), ("Avatar: arm strike", "ArmStrikeDamage") })
				{ int v = Read(noxMod, Avatar, p); if (v > 0) attacks.Add((l, v, -1)); else Note($"{p}: not readable ({v})"); }
				foreach (var (l, p) in new[] { ("ND: galaxy", "GalaxyDamage"), ("ND: quasar", "QuasarDamage"), ("ND: sword constellation", "SwordConstellationDamage"), ("ND: controlled star", "ControlledStarDamage"), ("ND: primordial stardust", "PrimordialStardustDamage"), ("ND: light slash", "LightSlashDamage"), ("ND: sun laser", "SunLaserDamage"), ("ND: dagger", "DaggerDamage"), ("ND: exploding star", "ExplodingStarDamage"), ("ND: feather", "PsychedelicFeatherDamage"), ("ND: screen slice", "ScreenSliceDamage"), ("ND: starburst", "StarburstDamage"), ("ND: supernova plasma", "SupernovaPlasmaDamage") })
				{ int v = Read(noxMod, Nameless, p); if (v > 0) attacks.Add((l, v, -1)); else Note($"{p}: not readable ({v})"); }
				int beam = Read(noxMod, Nameless, "CosmicLaserbeamDamage"); int beamIf = Read(noxMod, Nameless, "SuperCosmicLaserbeam_LaserPlayerIframes");
				if (beam > 0) attacks.Add(("ND: super cosmic beam (the player's own 6-tick immunity)", beam, beamIf > 0 ? beamIf : 6));
			}
			else { attacks.Add(("constructed 450", 450, -1)); attacks.Add(("constructed 700", 700, -1)); attacks.Add(("constructed 120", 120, -1)); }

			// -------- A: per hit and per second, a levy against a player, same defense and endurance; the Chalice with the drain off (BEFORE) and on (NOW)
			const int Ticks = 600;
			var rows = new List<string>(); Trial first = null, firstBefore = null, firstPlayer = null; double worstRatioNow = 0, worstRatioBefore = 1e9; int pairs = 0, bad = 0;
			foreach (int def in new[] { 40 })
			foreach (float dr in new[] { 0.25f })
			foreach (var (label, dmg, iframes) in attacks)
			{
				Trial pl = PlayerRun(real, dmg, def, dr, true, Ticks);
				SoldierNPC.BleedoutLab = false; Trial before = LevyRun(levy, dmg, def, dr, true, Ticks); SoldierNPC.BleedoutLab = true;
				Trial now = LevyRun(levy, dmg, def, dr, true, Ticks);
				pairs++;
				double rNow = pl.Total > 0 ? (double)now.Total / pl.Total : 1, rBefore = pl.Total > 0 ? (double)before.Total / pl.Total : 1;
				worstRatioNow = Math.Max(worstRatioNow, Math.Abs(1 - rNow)); worstRatioBefore = Math.Min(worstRatioBefore, rBefore);
				if (Math.Abs(1 - rNow) > 0.15) bad++;
				if (first == null) { first = now; firstBefore = before; firstPlayer = pl; }
				rows.Add($"{label,-58} dmg {dmg,4} | PLAYER hit {pl.FirstHit,4} x{pl.Hits,2} lost 1s {pl.LostAt1s,5} 3s {pl.LostAt3s,5} 10s {pl.LostAt10s,5} | LEVY BEFORE hit {before.FirstHit,3} x{before.Hits,2} lost 1s {before.LostAt1s,4} 3s {before.LostAt3s,4} 10s {before.LostAt10s,5} | LEVY NOW hit {now.FirstHit,3} x{now.Hits,2} lost 1s {now.LostAt1s,5} 3s {now.LostAt3s,5} 10s {now.LostAt10s,5} (bleed {now.Buffer0:0}) | levy/player BEFORE {rBefore:0.00} NOW {rNow:0.00} | player buffer added per hit: {pl.Adds}");
			}
			foreach (string r in rows) Note($"Master+Death, defense 40, endurance 25%, 10 s standing in the shot: " + r);
			Check($"a levy in the Chalice standing in a shot for 10 s loses what a player in the Chalice loses (within 15 percent): {pairs - bad} of {pairs} attacks; worst gap {worstRatioNow:P0}", pairs > 0 && bad == 0);
			Check($"the check has teeth: with the drain off the levy lost under a tenth of the player's life in the worst attack (ratio {worstRatioBefore:0.00})", worstRatioBefore < 0.1);

			// -------- the beam: 6-tick immunity on a player (WotG's ImmuneTimeOverride), the levy's own window
			if (nox)
			{
				int beamDmg = Read(noxMod, Nameless, "CosmicLaserbeamDamage"); int ifr = Math.Max(1, Read(noxMod, Nameless, "SuperCosmicLaserbeam_LaserPlayerIframes"));
				if (beamDmg > 0)
				{
					Trial pNorm = PlayerRun(real, beamDmg, 40, 0.25f, true, 300); Trial pBeam = PlayerRun(real, beamDmg, 40, 0.25f, true, 300, ifr);
					Trial lv = LevyRun(levy, beamDmg, 40, 0.25f, true, 300);
					Note($"super cosmic beam (damage {beamDmg}) over 5 s, all in the Chalice with the drain on: PLAYER with the beam's own {ifr}-tick immunity takes {pBeam.Total} in {pBeam.Hits} hits; a player with the ordinary window {pNorm.Total} in {pNorm.Hits}; LEVY {lv.Total} in {lv.Hits} hits");
				}
			}

			// -------- B: the drain rate, once, against Calamity's own constant; a levy that bleeds to death
			try { DrainRate(levy, real); } catch (Exception e) { Check("drain-rate trial threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
			try { BleedToDeath(chaliceType); } catch (Exception e) { Check("bleed-to-death trial threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
			try { VideoLike(chaliceType); } catch (Exception e) { Check("video-like trial threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
			// -------- C: the army in the area attack
			try { Army(chaliceType, attacks.Count > 0 ? attacks[0].dmg : 550, attacks.Count > 0 ? attacks[0].label : "constructed"); } catch (Exception e) { Check("army trial threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
		}

		/// <summary>One hit of 450, then the buffer drains: after n ticks the buffer is the first x 0.99167^n (Calamity's constant) and the body has lost the difference, with the regeneration pass running beside it.</summary>
		private static void DrainRate(SoldierNPC levy, Player real)
		{
			NPC body = levy.NPC; Player d = levy.Dummy;
			SoldierNPC.BleedoutLab = true;
			body.life = body.lifeMax = 100000; if (levy.Record != null) levy.Record.CurrentHp = 100000; levy.ImmuneTime = 0; levy.AllImmune = 0; Array.Clear(levy.SlotImmune);
			ChaliceBleed.Clear(d);
			var pr = Shot(450, body.Center, 0, false);
			Step(1, k => Pin(pr, body.Center));
			if (pr != null) pr.active = false;
			Step(2);
			double b0 = ChaliceBleed.Buffer(d); int hp0 = body.life; int lostBefore = 100000 - hp0;
			int taken0 = levy.BleedoutTaken; Step(120, k => { body.life = body.lifeMax - 1; }); // topped up each tick so the levy outlives the buffer; below full life the regeneration pass runs beside the drain
			double b1 = ChaliceBleed.Buffer(d); int hp1 = 0; hp0 = levy.BleedoutTaken - taken0; // hp0 here is what the body lost to the drain over the 120 ticks
			double expect = b0 * Math.Pow(1.0 - 0.0083333333333, 120);
			Note($"drain rate: after the hit the buffer is {b0:0.0}; 120 ticks later {b1:0.0} (Calamity's constant gives {expect:0.0}); the body lost {hp0} to the drain over those ticks (a hit's own 5 first: {lostBefore}); regeneration may add a few");
			Check($"the buffer drains once, at Calamity's rate, with the regeneration pass beside it (buffer {b1:0.0} vs {expect:0.0})", b0 > 100 && Math.Abs(b1 - expect) <= Math.Max(3.0, expect * 0.06));
			Check($"and the body lost what the buffer lost ({hp0} life for a drop of {b0 - b1:0.0})", Math.Abs(hp0 - (b0 - b1)) <= Math.Max(6.0, (b0 - b1) * 0.1));
		}

		private static void BleedToDeath(int chaliceType)
		{
			foreach (bool on in new[] { false, true })
			{
				SoldierNPC.BleedoutLab = on;
				var l = MakeLevy("Pvt. Bleeder", new Vector2(Cx * 16f + 200f, FloorY * 16f), chaliceType, 300);
				if (l == null) { Check("a levy to bleed spawns", false); continue; }
				NPC body = l.NPC; Step(4);
				body.lifeMax = 300; body.life = 300; if (l.Record != null) l.Record.CurrentHp = 300; l.ImmuneTime = 0;
				SoldierNPC.HitDbg = new Dictionary<string, (long dmg, int n)>();
				var pr = Shot(450, body.Center, 0, false);
				int diedAt = -1;
				Step(600, k => { if (k < 40) Pin(pr, body.Center); else if (pr != null) pr.active = false; if (diedAt < 0 && (!body.active || body.life <= 0)) diedAt = k; });
				SoldierNPC.HitDbg = null; if (pr != null) pr.active = false;
				Check(on ? $"with the drain on, a 300-life levy in the Chalice hit once by 450 bleeds out (died at tick {diedAt})" : $"with the drain off the same levy survives the hit on 5 damage (died at {diedAt})", on ? diedAt >= 0 : diedAt < 0);
				if (body.active) body.active = false;
			}
			SoldierNPC.BleedoutLab = true;
		}

		/// <summary>An army of 12 levies in the Chalice (real life, a little defense) inside one big area shot: how many are alive each second, the drain off and on, against 12 players.</summary>
		/// <summary>
		/// The video's numbers: a levy whose hits show as "(-435)" in the Chalice (a shot sized so the hit before the Chalice's cut is about 440 after the levy's defense and endurance), 400 life, standing in it. Time to death with the drain off and on.
		/// </summary>
		private static void VideoLike(int chaliceType)
		{
			foreach (bool on in new[] { false, true })
			{
				SoldierNPC.BleedoutLab = on; Cleanup(); Arena();
				var l = MakeLevy("Sgt. Alice", new Vector2(Cx * 16f, FloorY * 16f), chaliceType, 0);
				if (l == null) { Check("the video-like levy spawns", false); continue; }
				NPC body = l.NPC; Step(8);
				l.NPC.defense = 40; l.Defense = 40; l.Dummy.statDefense = Player.DefenseStat.Default + 40; l.Endurance = 0.25f;
				var pr = Shot(100, body.Center, 0, false); int diedAt = -1, firstBuf = 0; double peak = 0; int life0 = body.lifeMax;
				Step(1800, k =>
				{
					l.Endurance = 0.25f; l.Defense = 40; body.defense = 40; l.Dummy.statDefense = Player.DefenseStat.Default + 40;
					Pin(pr, body.Center);
					peak = Math.Max(peak, ChaliceBleed.Buffer(l.Dummy)); if (k == 3 && firstBuf == 0) firstBuf = (int)ChaliceBleed.Buffer(l.Dummy);
					if (diedAt < 0 && (!body.active || body.life <= 0)) diedAt = k;
				});
				if (pr != null) pr.active = false;
				Note($"video-like levy ({life0} life, first hit's deferred damage about {firstBuf}, peak buffer {peak:0}) standing in the shot, drain {(on ? "ON" : "OFF")}: " + (diedAt >= 0 ? $"dies at tick {diedAt} ({diedAt / 60.0:0.0} s)" : "alive after 30 s"));
				Check(on ? "a levy taking about 440 per hit in the Chalice dies standing in the shot, as a player of that life would" : "(drain off) the same levy stays alive in the shot for 30 s: the bug", on ? diedAt >= 0 && diedAt < 600 : diedAt < 0);
				if (body.active) body.active = false;
			}
			SoldierNPC.BleedoutLab = true; Cleanup();
		}

		private static void Army(int chaliceType, int dmg, string label)
		{
			var res = new Dictionary<string, int[]>();
			foreach (bool on in new[] { false, true })
			{
				SoldierNPC.BleedoutLab = on; Cleanup(); Arena();
				var army = new List<SoldierNPC>();
				for (int i = 0; i < 12; i++) { var l = MakeLevy("Pvt. A" + i, new Vector2(Cx * 16f - 220f + i * 40f, FloorY * 16f), chaliceType, 0); if (l != null) army.Add(l); }
				Step(8);
				foreach (var l in army) { l.NPC.defense = 40; l.Defense = 40; l.Dummy.statDefense = Player.DefenseStat.Default + 40; l.Endurance = 0.25f; }
				int hpTotal = army.Sum(l => l.NPC.lifeMax);
				var pr = Shot(dmg, new Vector2(Cx * 16f, FloorY * 16f - 40f), 900, false);
				var alive = new int[31];
				SoldierNPC.HitDbg = new Dictionary<string, (long dmg, int n)>();
				Step(1800, k =>
				{
					Pin(pr, new Vector2(Cx * 16f, FloorY * 16f - 40f));
					if (k % 60 == 0) alive[k / 60] = army.Count(l => l.NPC.active && l.NPC.life > 0);
				});
				alive[30] = army.Count(l => l.NPC.active && l.NPC.life > 0);
				int hits = 0; foreach (var kv in SoldierNPC.HitDbg) hits += kv.Value.n; SoldierNPC.HitDbg = null;
				if (pr != null) pr.active = false;
				res[on ? "NOW" : "BEFORE"] = alive;
				Note($"army of {army.Count} levies in the Chalice, life {hpTotal / Math.Max(1, army.Count)} each, defense 40, inside '{label}' ({dmg}) for 30 s, drain {(on ? "ON" : "OFF")}: alive each second [{string.Join(",", alive)}], {hits} hits");
			}
			SoldierNPC.BleedoutLab = true;
			// 12 players of the same life, as many hits at the same cadence: the player's number from the single-target run is the reference (a player dies of the first hit plus the drain in a few seconds)
			Check($"the army dies in the area shot with the drain on (alive after 30 s: {res["NOW"][30]}) and does not with it off (alive: {res["BEFORE"][30]})", res["NOW"][30] < res["BEFORE"][30] || res["BEFORE"][30] == 0);
			Cleanup();
		}
	}
}
