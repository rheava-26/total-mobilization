using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Vanity;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// THE PET CHECK ("[PetWorld]" lines, Calamity half only; LEVY_SELFCHECK_WORLD=1 on a dedicated server, LEVY_PETWORLD_ONLY=1 runs it alone, LEVY_PETWORLD=0 skips it).
	/// A pet or helper a levy's gear makes (Calamity's Daawnlight Spirit Origin: the accessory's buff spawns an archer pet) must belong to that levy: driven by his dummy on owner 255, beside him, gone when he falls, and never handed
	/// to the commander. The commander (slot 0, run as the local player) keeps her own copy of the same pet when she wears the item, and the two never count against each other.
	/// <para/>First the OLD behaviour (GearUpkeep.HelpersOn off: the pet handed to the commander like a shot) is run and reported as OBSERVED, so the log shows the bug the checks then rule out. Then, asserted: twelve levies
	/// wearing it have twelve pets, each driven by its own levy and next to it; six fall and one is benched, and their pets are gone within a second without flying to the commander; the commander puts it on and her own pet
	/// exists, exactly one, while orphans never pile up; the hide setting hides the levies' pets and not hers; the accessory's bullseyes live under the levy; a handful of other Calamity accessories' helpers are driven the same way.
	/// </summary>
	public class PetWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "PetTester";
		private static Vector2 _cmdAt;
		private static readonly List<NPC> _targets = new();

		private static bool Enabled => Main.dedServ && Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_PETWORLD") != "0";

		public override void Load() { if (Enabled) StealthWorldCheck.InstallLocalPlayerPatches(Mod); }

		public override void OnWorldLoad()
		{
			if (!Enabled) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (Environment.GetEnvironmentVariable("LEVY_SUMMONWORLD") == "0" && Environment.GetEnvironmentVariable("LEVY_PETWORLD_ONLY") == "1") Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		/// <summary>Standalone entry and part of SummonWorldCheck's chain.</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation, helpers0 = GearUpkeep.HelpersOn; int my0 = Main.myPlayer;
			FieldInfo achField = typeof(Main).GetField("_achievements", BindingFlags.Instance | BindingFlags.NonPublic); object achOrig = achField?.GetValue(Main.instance);
			try
			{
				if (achField != null && achOrig == null) achField.SetValue(Main.instance, new Terraria.Achievements.AchievementManager());
				if (!ModLoader.HasMod("CalamityMod")) _mod.Logger.Info("[PetWorld] OBSERVED Calamity is not loaded: nothing to measure");
				else { LevyUtil.SpEmulation = true; Run(); }
			}
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				_running = false; LevyUtil.SpEmulation = sp; Main.myPlayer = my0; GearUpkeep.HelpersOn = helpers0; AccessoryMinionHide.TestForce = false;
				Cleanup();
				try { achField?.SetValue(Main.instance, achOrig); } catch (Exception) { }
			}
			_mod.Logger.Info($"[PetWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[PetWorld] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[PetWorld] OBSERVED " + s);

		private static int ItemOf(string name) => ModContent.TryFind("CalamityMod", name, out ModItem mi) ? mi.Type : 0;
		private static int ProjOf(string name) => ModContent.TryFind("CalamityMod", name, out ModProjectile mp) ? mp.Type : 0;

		// ---------------------------------------------------------------------------------------- the stage
		private static void Arena()
		{
			for (int i = Cx - 260; i <= Cx + 260; i++)
			{
				for (int j = FloorY - 120; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void ClearCommander()
		{
			Player p = Main.player[0]; if (p == null) return;
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			p.inventory[0] = new Item();
			for (int i = 0; i < p.buffType.Length; i++) { p.buffType[i] = 0; p.buffTime[i] = 0; }
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } }
				foreach (NPC t in _targets) if (t != null) t.active = false; _targets.Clear();
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				if (RosterSystem.Instance != null) RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				ClearCommander();
			}
			catch (Exception) { }
		}

		private static NPC MakeTarget(Vector2 at)
		{
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("PetWorldCheck"), (int)at.X, (int)at.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) return null;
			NPC t = Main.npc[ti];
			t.lifeMax = 100_000_000; t.life = t.lifeMax; t.defense = 0; t.damage = 0; t.knockBackResist = 0f; t.width = 64; t.height = 96;
			t.noGravity = true; t.noTileCollide = true; t.aiStyle = -1; t.friendly = false; t.value = 0f; t.Bottom = at;
			var ht = t.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = t.position;
			_targets.Add(t);
			return t;
		}

		private static SoldierNPC MakeLevy(Vector2 at, int n, int accessory)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Pet" + n, CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			Item w = new Item(); w.SetDefaults(ItemID.Minishark); if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
			Item am = new Item(); am.SetDefaults(ItemID.MusketBall); am.stack = am.maxStack; rec.Gear.Ammo = am; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true);
			if (accessory > 0) { Item a = new Item(); a.SetDefaults(accessory); rec.Gear.Accessories[0] = a; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold;
			RosterSystem.Instance.Regulars.Add(rec);
			int bi = RosterSystem.Instance.SpawnBody(rec, at);
			if (bi < 0 || !rec.HasBody) return null;
			Main.npc[bi].Bottom = at; Main.npc[bi].lifeMax = Main.npc[bi].life = 2_000_000; rec.CurrentHp = 2_000_000;
			return Main.npc[bi].ModNPC as SoldierNPC;
		}

		private static void Step(int n, Action perTick = null)
		{
			for (int i = 0; i < n; i++)
			{
				Player p = Main.player[0];
				p.active = true; p.dead = false; p.name = Commander;
				if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500; p.statLife = p.statLifeMax2;
				p.Bottom = _cmdAt; p.velocity = Vector2.Zero; p.fallStart = p.fallStart2 = (int)(_cmdAt.Y / 16f); p.noFallDmg = true;
				p.controlUseItem = false; p.releaseUseItem = true;
				Main.dayTime = false; Main.time = 18000;
				foreach (NPC t in _targets) if (t != null && t.active) { t.timeLeft = 9999; t.life = t.lifeMax; t.velocity = Vector2.Zero; t.position = t.GetGlobalNPC<HarnessTarget>().Pin; }
				for (int k = 0; k < Main.maxNPCs; k++) { NPC e = Main.npc[k]; if (e.active && !_targets.Contains(e) && WorldStep.IsNaturalHostile(e)) e.active = false; }
				int my = Main.myPlayer; Main.myPlayer = 0;
				try { WorldStep.Update(); } finally { Main.myPlayer = my; }
				perTick?.Invoke();
			}
		}

		// ---------------------------------------------------------------------------------------- reading the field
		private static List<Projectile> Of(int type) { var l = new List<Projectile>(); if (type <= 0) return l; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].type == type) l.Add(Main.projectile[i]); return l; }
		private static int Tag(Projectile p) => p.TryGetGlobalProjectile(out SoldierProjectileTag t) ? t.SoldierIndex : -1;
		private static bool Helper(Projectile p) => p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.GearHelper;
		private static bool Alive(SoldierNPC s) => s != null && s.NPC.active && s.IsAlive;
		private static float CmdDist(Projectile p) => Vector2.Distance(p.Center, Main.player[0].Center);

		/// <summary>One line on every projectile of a type: owner, tag, helper bit, distance to its levy and to the commander.</summary>
		private static string Describe(int type)
		{
			var l = Of(type);
			int own0 = l.Count(p => p.owner == 0), own255 = l.Count(p => p.owner == SoldierNPC.DummySlot), tagged = l.Count(p => Tag(p) >= 0), helpers = l.Count(Helper);
			int deadTag = l.Count(p => Tag(p) >= 0 && !(Main.npc[Tag(p)].ModNPC is SoldierNPC s && Alive(s)));
			int nearCmd = l.Count(p => CmdDist(p) < 200f);
			return $"{l.Count} alive: owner commander {own0}, owner 255 {own255}, tagged to a levy {tagged} (of a levy no longer alive {deadTag}), helper bit {helpers}, within 200 px of the commander {nearCmd}";
		}

		/// <summary>Largest distance from a live levy's pet to that levy (and how many live levies have exactly one).</summary>
		private static (int withOne, int withMore, float maxDist) PerLevy(List<SoldierNPC> army, int type)
		{
			var l = Of(type); int one = 0, more = 0; float far = 0f;
			foreach (var s in army)
			{
				if (!Alive(s)) continue;
				var mine = l.Where(p => Tag(p) == s.NPC.whoAmI).ToList();
				if (mine.Count == 1) one++; else if (mine.Count > 1) more++;
				foreach (var p in mine) far = Math.Max(far, Vector2.Distance(p.Center, s.NPC.Center));
			}
			return (one, more, far);
		}

		private static void KillLevy(SoldierNPC s) { if (!Alive(s)) return; s.NPC.life = 0; s.NPC.checkDead(); }

		private static List<SoldierNPC> Army(int count, int acc, int firstTile, int spacing, int n0)
		{
			var army = new List<SoldierNPC>();
			for (int i = 0; i < count; i++) { var l = MakeLevy(_cmdAt + new Vector2((firstTile + i * spacing) * 16f, 0f), n0 + i, acc); if (l != null) army.Add(l); }
			return army;
		}

		private static FieldInfo _critBoost; private static ModPlayer _calBase;
		private static int CritBoost(Player p)
		{
			try
			{
				if (_calBase == null) { _calBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType().FullName == "CalamityMod.CalPlayer.CalamityPlayer"); _critBoost = _calBase?.GetType().GetField("spiritOriginCritBoost"); }
				return _calBase == null || _critBoost == null ? -1 : (int)_critBoost.GetValue(p.GetModPlayer(_calBase));
			}
			catch (Exception) { return -1; }
		}

		// ---------------------------------------------------------------------------------------- the check
		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			int dso = ItemOf("DaawnlightSpiritOrigin"), pet = ProjOf("DaawnlightSpiritOriginMinion"), eye = ProjOf("SpiritOriginBullseye");
			if (dso <= 0 || pet <= 0) { Check($"the Daawnlight Spirit Origin and its pet exist (item {dso}, pet {pet})", false); return; }
			_cmdAt = new Vector2(Cx * 16f, FloorY * 16f);
			Main.rand = new Terraria.Utilities.UnifiedRandom(9200);
			int hook0 = GearUpkeep.HookFailures;

			// ---- 0. the old behaviour, for the record (OBSERVED only) ----
			GearUpkeep.HelpersOn = false;
			Arena(); ClearCommander(); Step(10);
			var old = Army(12, dso, 30, 4, 0);
			Step(180);
			Note($"OLD (pets handed to the commander), 12 levies wearing it, after 3 s: pets {Describe(pet)}");
			for (int i = 0; i < 6 && i < old.Count; i++) KillLevy(old[i]);
			int nearPeak = 0; Step(120, () => nearPeak = Math.Max(nearPeak, Of(pet).Count(p => CmdDist(p) < 200f)));
			Note($"OLD, 2 s after six of them fell: pets {Describe(pet)}; most pets within 200 px of the commander at once {nearPeak}");
			Main.player[0].armor[3] = new Item(); Main.player[0].armor[3].SetDefaults(dso);
			Step(120);
			Note($"OLD, the commander wearing it too for 2 s: pets {Describe(pet)}; untagged pets owned by the commander (her own) {Of(pet).Count(p => p.owner == 0 && Tag(p) < 0)}; her count of the pet type {Main.player[0].ownedProjectileCounts[pet]}");
			Cleanup(); GearUpkeep.ClearChurn(); GearUpkeep.HelpersOn = true;

			// ---- 1. twelve levies, twelve pets, each its levy's ----
			Arena(); ClearCommander(); Step(10);
			var army = Army(12, dso, 30, 4, 100);
			Check($"twelve levies wearing the Daawnlight Spirit Origin spawn ({army.Count})", army.Count == 12);
			Step(180);
			float farSample = 0f;
			Step(60, () => { var pl = PerLevy(army, pet); farSample = Math.Max(farSample, pl.maxDist); });
			var per = PerLevy(army, pet);
			var pets = Of(pet);
			Note($"12 levies after 4 s: pets {Describe(pet)}; levies with exactly one {per.withOne}, with more {per.withMore}; farthest a pet strayed from its levy over the last second {farSample:0} px");
			Check($"twelve pets, one per levy ({pets.Count} pets, {per.withOne} levies with exactly one, {per.withMore} with more)", pets.Count == 12 && per.withOne == 12 && per.withMore == 0);
			Check($"every pet is driven by its levy: owner 255 and a helper of that levy ({pets.Count(p => p.owner == SoldierNPC.DummySlot && Helper(p))} of {pets.Count})", pets.Count > 0 && pets.All(p => p.owner == SoldierNPC.DummySlot && Helper(p) && Tag(p) >= 0));
			Check($"every pet stays beside its levy (farthest {farSample:0} px over a second)", farSample > 0f && farSample < 260f);
			Check($"the commander owns no pet and counts none ({pets.Count(p => p.owner == 0)} owned, count {Main.player[0].ownedProjectileCounts[pet]})", pets.All(p => p.owner != 0) && Main.player[0].ownedProjectileCounts[pet] == 0);

			// ---- 2. six fall and one is benched: their pets go within a second, none to the commander ----
			var gone = new List<SoldierNPC>();
			for (int i = 0; i < 6; i++) gone.Add(army[i]);
			var goneIdx = new HashSet<int>(gone.Select(s => s.NPC.whoAmI)); goneIdx.Add(army[6].NPC.whoAmI);
			// tracked by the pet's own tag instance: a projectile slot is reused by the next projectile, a tag is made fresh for each
			var goneProj = new HashSet<SoldierProjectileTag>(Of(pet).Where(p => goneIdx.Contains(Tag(p))).Select(p => p.GetGlobalProjectile<SoldierProjectileTag>()));
			bool GoneAlive() => Of(pet).Any(p => goneProj.Contains(p.GetGlobalProjectile<SoldierProjectileTag>()));
			foreach (var s in gone) KillLevy(s);
			bool benched = RosterSystem.Instance.Demobilize(army[6].Record); // benched: off the field and kept off it
			if (!benched) { army[6].Record.StoodDown = true; RosterSystem.RetireBody(army[6]); }
			float minCmd = float.MaxValue; int lastSeen = -1, tick = 0;
			Step(60, () => { tick++; foreach (var p in Of(pet)) minCmd = Math.Min(minCmd, CmdDist(p)); if (GoneAlive()) lastSeen = tick; });
			var live = army.Where(Alive).ToList();
			var after = PerLevy(army, pet);
			Note($"after six fell and one was benched: pets {Describe(pet)}; the fallen and benched levies' pets last seen at tick {lastSeen} (-1: gone before the first tick ended); closest any pet came to the commander {minCmd:0} px; live levies {live.Count}; benched through the reserve {benched}");
			Check($"the pets of the fallen and benched levies are gone within a second ({goneProj.Count} pets; last seen at tick {lastSeen})", goneProj.Count == 7 && lastSeen < 60 && !GoneAlive());
			Check($"no pet flies to the commander (closest {minCmd:0} px)", minCmd > 300f);
			Check($"the living levies keep exactly one pet each ({after.withOne} of {live.Count}, {Of(pet).Count} pets)", after.withOne == live.Count && after.withMore == 0 && Of(pet).Count == live.Count);

			// ---- 3. the commander wears it too: her own pet, exactly one, and no orphans ----
			Player cmd = Main.player[0];
			cmd.armor[3] = new Item(); cmd.armor[3].SetDefaults(dso);
			Step(120);
			var own = Of(pet).Where(p => p.owner == 0 && Tag(p) < 0).ToList();
			var per3 = PerLevy(army, pet);
			Note($"the commander wearing it: pets {Describe(pet)}; her own {own.Count}; her count {cmd.ownedProjectileCounts[pet]}; levies with one {per3.withOne}");
			Check($"the commander's own pet exists, exactly one, beside her ({own.Count}; {(own.Count > 0 ? CmdDist(own[0]) : -1):0} px)", own.Count == 1 && CmdDist(own[0]) < 260f);
			Check($"her count is her own pet only ({cmd.ownedProjectileCounts[pet]}) and every live levy still has one ({per3.withOne} of {army.Count(Alive)})", cmd.ownedProjectileCounts[pet] == 1 && per3.withOne == army.Count(Alive) && per3.withMore == 0);
			var near = Army(4, dso, 6, 3, 200); army.AddRange(near);
			Step(120);
			foreach (var s in army) KillLevy(s);
			int ownMax = 0, totalMax = 0; Step(120, () => { ownMax = Math.Max(ownMax, Of(pet).Count(p => p.owner == 0)); totalMax = Of(pet).Count; });
			var final = Of(pet);
			Note($"four more levies wore it beside her, then every levy fell: pets {Describe(pet)}; most pets she owned at once {ownMax}");
			Check($"no orphans pile up on the commander: one pet of hers the whole time, and only it is left ({ownMax} at most; {final.Count} left, {final.Count(p => p.owner == 0 && Tag(p) < 0)} hers)", ownMax == 1 && final.Count == 1 && final.Count(p => p.owner == 0 && Tag(p) < 0) == 1);
			Cleanup();

			// ---- 4. the hide setting: the levies' pets are not drawn, hers is ----
			Arena(); ClearCommander(); Step(10);
			cmd = Main.player[0]; cmd.armor[3] = new Item(); cmd.armor[3].SetDefaults(dso);
			var hideArmy = Army(4, dso, 30, 4, 300);
			Step(150);
			var hp = Of(pet); var hider = ModContent.GetInstance<AccessoryMinionHide>();
			Color c = Color.White;
			int drawnOff = hp.Count(p => hider.PreDraw(p, ref c));
			AccessoryMinionHide.TestForce = true;
			int levyDrawn = hp.Count(p => Tag(p) >= 0 && hider.PreDraw(p, ref c)), levyPets = hp.Count(p => Tag(p) >= 0), hersDrawn = hp.Count(p => p.owner == 0 && Tag(p) < 0 && hider.PreDraw(p, ref c)), hers = hp.Count(p => p.owner == 0 && Tag(p) < 0);
			AccessoryMinionHide.TestForce = false;
			Note($"hide setting: {hp.Count} pets ({levyPets} of levies, {hers} hers); setting off: {drawnOff} drawn; setting on: levies' drawn {levyDrawn}, hers drawn {hersDrawn}");
			Check($"setting off: every pet is drawn ({drawnOff} of {hp.Count})", hp.Count == 5 && drawnOff == hp.Count);
			Check($"setting on: no levy pet is drawn and the commander's still is ({levyDrawn} of {levyPets} levy pets drawn; {hersDrawn} of {hers} of hers)", levyPets == 4 && levyDrawn == 0 && hers == 1 && hersDrawn == 1);
			Cleanup();

			// ---- 5. the bullseyes: a levy's live under him instead of being made and lost every frame ----
			if (eye > 0)
			{
				Arena(); ClearCommander(); Step(10);
				MakeTarget(_cmdAt + new Vector2(60 * 16f, -6 * 16f)); MakeTarget(_cmdAt + new Vector2(70 * 16f, -6 * 16f));
				var eyeArmy = Army(4, dso, 30, 4, 400);
				int bound0 = GearUpkeep.OwnerBoundTypes.GetValueOrDefault(eye);
				int spawned = 0; var seen = new HashSet<int>();
				Step(240, () => { foreach (var p in Of(eye)) if (seen.Add(p.identity)) spawned++; });
				var eyes = Of(eye);
				int crit = eyeArmy.Where(Alive).Select(s => CritBoost(s.Dummy)).DefaultIfEmpty(-1).Max();
				Note($"bullseyes with 4 levies wearing it and 2 enemies in range for 4 s: {eyes.Count} alive ({eyes.Count(p => p.owner == SoldierNPC.DummySlot && Helper(p))} driven by a levy, {eyes.Count(p => p.owner == 0)} the commander's); {spawned} made in all; held as owner-bound {GearUpkeep.OwnerBoundTypes.GetValueOrDefault(eye) - bound0} time(s); largest levy crit bonus from bullseye hits {crit}");
				Check($"the levies' bullseyes live: one per enemy, driven by a levy, none the commander's ({eyes.Count} alive, {spawned} made)", eyes.Count == 2 && eyes.All(p => p.owner == SoldierNPC.DummySlot && Helper(p)) && spawned <= 4 && GearUpkeep.OwnerBoundTypes.GetValueOrDefault(eye) == bound0);
				foreach (var s in eyeArmy) KillLevy(s);
				Step(30);
				Check($"they end with their levies ({Of(eye).Count} left)", Of(eye).Count == 0);
				Cleanup();
			}

			// ---- 6. other accessories whose gear makes a helper ----
			var audit = new (string item, string proj, int want, float reach)[]
			{
				("ScionsCurio", "ScionsCurioMini", 1, 400f), ("WarbanneroftheRighteous", "WarbannerLight", 1, 400f), ("LuxorsGift", "Luxor", 1, 400f),
				("FrozenCube", "Elumphant", 1, 600f), ("QuiverofNihility", "VoidFieldGenerator", 4, 420f), ("HowlsHeart", "HowlsHeartCalcifer", 1, 400f),
			};
			foreach (bool on in new[] { false, true })
			{
				GearUpkeep.HelpersOn = on; GearUpkeep.ClearChurn();
				Arena(); ClearCommander(); Step(10);
				var rows = new List<(string item, int type, int want, float reach, SoldierNPC s)>();
				int x = 30;
				foreach (var a in audit)
				{
					int it = ItemOf(a.item), pt = ProjOf(a.proj);
					if (it <= 0 || pt <= 0) { Note($"{a.item} / {a.proj} is not in this Calamity build"); continue; }
					var s = MakeLevy(_cmdAt + new Vector2(x * 16f, 0f), 500 + x, it); x += 40;
					if (s != null) rows.Add((a.item, pt, a.want, a.reach, s));
				}
				Step(180);
				foreach (var r in rows)
				{
					var mine = Of(r.type).Where(p => Tag(p) == r.s.NPC.whoAmI).ToList();
					float far = mine.Select(p => Vector2.Distance(p.Center, r.s.NPC.Center)).DefaultIfEmpty(-1f).Max();
					string line = $"{r.item}: {mine.Count} alive for its levy ({mine.Count(p => p.owner == SoldierNPC.DummySlot && Helper(p))} driven by him, {Of(r.type).Count(p => p.owner == 0)} the commander's), farthest {far:0} px; dropped as owner-bound {GearUpkeep.OwnerBoundTypes.GetValueOrDefault(r.type)} time(s)";
					if (!on) { Note("OLD " + line); continue; }
					Note(line);
					Check($"{r.item}: its helper is the levy's, driven by him and beside him ({mine.Count} of {r.want}; farthest {far:0} px)", mine.Count == r.want && mine.All(p => p.owner == SoldierNPC.DummySlot && Helper(p)) && far >= 0f && far <= r.reach);
				}
				foreach (var r in rows) KillLevy(r.s);
				Step(60);
				if (on) foreach (var r in rows) Check($"{r.item}: its helper ends with the levy ({Of(r.type).Count} left)", Of(r.type).Count == 0);
				Cleanup();
			}
			GearUpkeep.HelpersOn = true; GearUpkeep.ClearChurn();
			Note($"gear hooks that threw during the check: {GearUpkeep.HookFailures - hook0}; helpers adopted {SoldierMinions.HelpersAdopted}");
		}
	}
}
