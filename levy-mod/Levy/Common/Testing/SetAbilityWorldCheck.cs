using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of the armor set abilities a key starts (SoldierNPC.SetBonusKey.cs, SoldierNPC.SetDash.cs), logged as "[SetAbility]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1
	/// (the shipworld chain runs it; LEVY_SETABILITY_ONLY=1 runs it alone). Slot 0 stands in as the commander 70 tiles behind a holding levy; pinned enemies (no AI, no damage) stand in a line in front of him, and a bare
	/// levy stands in the line too. The real Main.Update steps the world.
	///  * Vanilla (both halves): the Solar Flare dash rams the line (enemies struck, a shield spent); the Forbidden storm lands on the target; Vortex stealth goes on in the fight and off after it; the setting off stops the storm.
	///  * Calamity: the God Slayer dash fires from the levy, strikes the enemies along its path for the set's damage and lays God Slayer Inferno, then waits out Calamity's own cooldown (it ticks at the real rate) and comes back
	///    when the cooldown ends; a hit taken mid-dash is Calamity's God Slayer dodge, outside it is not; a coming hostile shot is dodged with the dash; the setting off stops it; the Tarragon cloak goes on with
	///    Calamity's Armor Set Bonus key bound (its default), which drops the vanilla double-tap.
	/// Every trial: the commander and the bare levy take nothing. Throwaway soldiers only; nothing is saved.
	/// </summary>
	public sealed class SetAbilityWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod; private static Vector2 _cmdAt; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 130;
		private const string Commander = "SetAbilityTester";
		private static readonly List<(NPC n, Vector2 at)> _pins = new();

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_SETABILITY") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			bool setting = LevyConfig.Get.SoldiersUseSetAbilities;
			var rand = Main.rand;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
			finally
			{
				_running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server;
				LevyConfig.Get.SoldiersUseSetAbilities = setting; SoldierNPC.PressViaDoubleTap = false; Main.rand = rand;
				Clear();
			}
			_mod.Logger.Info($"[SetAbility] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[SetAbility] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[SetAbility] OBSERVED " + s);
		private static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

		// ---- world ----
		private static void Arena()
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 70; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				for (int j = FloorY; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++)
			{
				KeepCommander(); PinAll();
				WorldStep.Update();
				for (int k = 0; k < Main.maxNPCs; k++) { NPC a = Main.npc[k]; if (a.active && WorldStep.IsNaturalHostile(a) && !_pins.Any(p => p.n == a)) a.active = false; }
			}
		}

		/// <summary>The commander stands still far behind; her life is never topped up, so anything that hurt her shows.</summary>
		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			if (p.statLifeMax < 500) { p.statLifeMax = 500; p.statLifeMax2 = 500; p.statLife = 500; }
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void ResetCommander() { Player p = Main.player[0]; p.statLifeMax = 500; p.statLifeMax2 = 500; p.statLife = 500; for (int b = 0; b < p.buffType.Length; b++) { p.buffType[b] = 0; p.buffTime[b] = 0; } }
		private static bool CommanderWhole(out string why)
		{
			Player p = Main.player[0];
			int debuffs = 0; for (int b = 0; b < p.buffType.Length; b++) if (p.buffType[b] > 0 && Main.debuff[p.buffType[b]]) debuffs++;
			why = $"life {p.statLife}/{p.statLifeMax2}, debuffs {debuffs}, {F(Vector2.Distance(p.Center, _cmdAt) / 16f)} tiles from her spot";
			return p.statLife >= p.statLifeMax2 && debuffs == 0 && Vector2.Distance(p.Center, _cmdAt) < 32f;
		}

		private static void PinAll()
		{
			foreach (var (n, at) in _pins)
			{
				if (!n.active) continue;
				n.timeLeft = 9999; n.velocity = Vector2.Zero; n.Bottom = at;
				var ht = n.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = n.position;
			}
		}

		private static NPC Pin(Vector2 bottom, int life = 1_000_000)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("SetAbilityWorldCheck"), (int)bottom.X, (int)bottom.Y, NPCID.GreenSlime);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC n = Main.npc[i];
			n.lifeMax = life; n.life = life; n.defense = 0; n.damage = 0; n.knockBackResist = 0f; n.width = 40; n.height = 56;
			n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.value = 0f; n.Bottom = bottom;
			var ht = n.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = n.position;
			_pins.Add((n, bottom));
			return n;
		}

		private static void Clear()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
			}
			foreach (var (n, _) in _pins) { n.GetGlobalNPC<HarnessTarget>().IsTarget = false; n.active = false; }
			_pins.Clear();
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
		}

		private static Item Make(int type, int stack = 1) { var i = new Item(); i.SetDefaults(type); if (stack > 1) i.stack = stack; return i; }
		private static int ItemOf(string mod, string name) => mod == null ? (ItemID.Search.TryGetId(name, out int id) ? id : 0) : (ModContent.TryFind(mod, name, out ModItem mi) ? mi.Type : 0);

		/// <summary>A holding levy in the given armor and weapon (locked, so the Armory leaves the kit alone); its body index or -1.</summary>
		private static int SpawnLevy(string name, Vector2 at, int weapon, int[] armor, int ammo = 0)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = name,
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (weapon > 0) { rec.Gear.Weapon = Make(weapon); rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
			if (armor != null && armor.Length == 3) { rec.Gear.Head = Make(armor[0]); rec.Gear.Body = Make(armor[1]); rec.Gear.Legs = Make(armor[2]); }
			if (ammo > 0) rec.Gear.Ammo = Make(ammo, 999);
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold; rec.HoldPos = at;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return -1;
			Main.npc[body].Bottom = at;
			return body;
		}

		private static Vector2 Ground(float tilesFromCx) => new Vector2((Cx + tilesFromCx) * 16f + 8f, FloorY * 16f);

		/// <summary>A fresh trial: arena, nobody left over, the commander 70 tiles behind the levy's spot, a bare levy in the line.</summary>
		private static int Setup(out int bystander)
		{
			Clear(); Arena();
			_cmdAt = Ground(-70) - new Vector2(0f, 24f);
			ResetCommander(); KeepCommander(); Step(2);
			bystander = SpawnLevy("Pvt. Bystander", Ground(4), 0, null);
			return bystander;
		}

		private static int Life(int body) => body >= 0 && Main.npc[body].active ? Main.npc[body].life : -1;

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			string part = Environment.GetEnvironmentVariable("LEVY_SETABILITY_PART") ?? "all";
			void Try(string name, Action a) { if (part != "all" && part != name) return; try { a(); } catch (Exception e) { Check(name + " trial threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); } finally { LevyConfig.Get.SoldiersUseSetAbilities = true; SoldierNPC.PressViaDoubleTap = false; Clear(); } }
			Check("the policy table loaded (" + SetAbilityPolicies.Count + " rows) with the God Slayer, Solar Flare, Forbidden and Vortex rows", SetAbilityPolicies.Count >= 10 && SetAbilityPolicies.For("godslayer").Kind == SetAbilityKind.Dash && SetAbilityPolicies.For("solarflare").Kind == SetAbilityKind.Ram && SetAbilityPolicies.For("forbidden").Kind == SetAbilityKind.AtTarget && SetAbilityPolicies.For("vortex").Kind == SetAbilityKind.FightToggle);
			Check("the setting is on by default", (typeof(LevyConfig).GetField("SoldiersUseSetAbilities")?.GetCustomAttributes(typeof(System.ComponentModel.DefaultValueAttribute), false).FirstOrDefault() as System.ComponentModel.DefaultValueAttribute)?.Value is true);
			LevyConfig.Get.SoldiersUseSetAbilities = true;
			Try("solar", SolarTrial);
			Try("forbidden", ForbiddenTrial);
			Try("vortex", VortexTrial);
			Try("off", SettingOffTrial);
			if (!ModLoader.HasMod("CalamityMod")) { Note("Calamity is not loaded: the God Slayer and Calamity key trials are skipped"); return; }
			Try("godslayer", GodSlayerTrial);
			Try("gsdodge", GodSlayerDodgeTrial);
			Try("gsoff", GodSlayerOffTrial);
			Try("calkey", CalamityKeyTrial);
		}

		// ------------------------------------------------------------------------------------------------------------
		// Vanilla
		// ------------------------------------------------------------------------------------------------------------
		private static void SolarTrial()
		{
			Setup(out int by);
			Main.rand = new Terraria.Utilities.UnifiedRandom(5101);
			int body = SpawnLevy("Pvt. Solar", Ground(0), ItemID.TerraBlade, new int[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings });
			if (body < 0) { Check("solar: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			var line = new List<NPC>(); foreach (float x in new[] { 7f, 9f, 11f }) { var n = Pin(Ground(x)); if (n != null) line.Add(n); }
			int byLife = Life(by), rams0 = sn.SrRamsOwn, shieldsBefore = -1, shieldsAfter = -1, startTick = -1; float startX = 0f, bodyX = 0f, maxX = 0f;
			var strace = new System.Text.StringBuilder();
			for (int t = 0; t < 1200 && startTick < 0; t++)
			{
				int sh = sn.SolarShieldsLab; float bx = Main.npc[body].Center.X;
				Step(1);
				if (t % 150 == 0) { NPC tg = sn.TargetIndex >= 0 && sn.TargetIndex < Main.maxNPCs ? Main.npc[sn.TargetIndex] : null; strace.Append($" t{t}: shields {sn.SolarShieldsLab} state {sn.State} target {(tg != null && tg.active ? F((tg.Center.X - Main.npc[body].Center.X) / 16f) : "-")} setSolar {sn.Dummy.setSolar} dashType {sn.Dummy.dashType};"); }
				if (sn.SrRamsOwn > rams0) { startTick = t; shieldsBefore = sh; startX = bx; bodyX = Main.npc[body].Center.X; }
			}
			if (startTick < 0) Note("solar, no ram:" + strace);
			Check($"solar: a levy in Solar Flare armor rams the enemy line with the dash (started at tick {startTick}, shields then {shieldsBefore})", startTick >= 0);
			if (startTick < 0) return;
			for (int t = 0; t < 60; t++) { Step(1); maxX = Math.Max(maxX, Main.npc[body].Center.X); if (!sn.SolarRamming && t > 2) { shieldsAfter = sn.SolarShieldsLab; break; } }
			if (shieldsAfter < 0) shieldsAfter = sn.SolarShieldsLab;
			int struck = line.Count(n => n.active && n.life < n.lifeMax);
			Note($"solar ram: from x {F((startX - Ground(0).X) / 16f)} tiles, ran to {F((maxX - Ground(0).X) / 16f)}, strikes {sn.SrStrikesOwn} for {sn.SrDamageOwn}, enemies hurt {struck}/{line.Count} [{string.Join(", ", line.Select(n => n.lifeMax - n.life))}], shields {shieldsBefore} -> {shieldsAfter}");
			Check($"solar: the dash strikes the enemies it passes ({struck} of {line.Count} hurt, {sn.SrStrikesOwn} strikes for {sn.SrDamageOwn})", struck >= 2 && sn.SrStrikesOwn >= 2 && sn.SrDamageOwn >= 2 * 100);
			Check($"solar: the dash starts at the levy and carries him through the line ({F((maxX - startX) / 16f)} tiles)", Math.Abs(bodyX - startX) < 40f && maxX - startX >= 4 * 16f);
			Check($"solar: the first strike spends a shield ({shieldsBefore} -> {shieldsAfter})", shieldsAfter == shieldsBefore - 1);
			Check("solar: the commander 70 tiles away takes nothing (" + (CommanderWhole(out string why) ? why : why) + ")", CommanderWhole(out _));
			Check($"solar: the bare levy in the line takes nothing (life {byLife} -> {Life(by)})", Life(by) >= byLife && byLife > 0);
		}

		private static void ForbiddenTrial()
		{
			Setup(out int by);
			Main.rand = new Terraria.Utilities.UnifiedRandom(5102);
			int body = SpawnLevy("Pvt. Forbidden", Ground(0), ItemID.DiamondStaff, new int[] { ItemID.AncientBattleArmorHat, ItemID.AncientBattleArmorShirt, ItemID.AncientBattleArmorPants });
			if (body < 0) { Check("forbidden: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			NPC target = Pin(Ground(16));
			int byLife = Life(by), presses0 = SoldierNPC.KeyPressKinds.TryGetValue("forbidden-storm", out int p0) ? p0 : 0;
			Projectile storm = null; int stormTick = -1;
			for (int t = 0; t < 900 && storm == null; t++)
			{
				foreach (var r in RosterSystem.Instance.Regulars) if (r.CommanderName == Commander) r.Mana = r.MaxMana;
				Step(1);
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.type == ProjectileID.SandnadoFriendly && p.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tag) && tag.SoldierIndex == body) { storm = p; stormTick = t; break; }
				}
			}
			int presses = (SoldierNPC.KeyPressKinds.TryGetValue("forbidden-storm", out int p1) ? p1 : 0) - presses0;
			Check($"forbidden: the levy presses the set key and his sandstorm appears (tick {stormTick}, presses {presses})", storm != null && presses >= 1);
			if (storm == null) return;
			float off = Math.Abs(storm.Center.X - target.Center.X) / 16f, fromLevy = Math.Abs(storm.Center.X - Main.npc[body].Center.X) / 16f;
			Note($"forbidden storm: {F(off)} tiles from the target, {F(fromLevy)} from the levy, friendly {storm.friendly}, hostile {storm.hostile}");
			Check($"forbidden: the storm lands on the target ({F(off)} tiles off it, {F(fromLevy)} from the levy)", off <= 6f && storm.friendly && !storm.hostile);
			int life0 = target.life;
			Step(120);
			Check($"forbidden: the target is hurt while the storm is up ({life0 - target.life})", target.life < life0);
			Check("forbidden: the commander 70 tiles away takes nothing (" + (CommanderWhole(out string why) ? why : why) + ")", CommanderWhole(out _));
			Check($"forbidden: the bare levy takes nothing (life {byLife} -> {Life(by)})", Life(by) >= byLife && byLife > 0);
		}

		private static void VortexTrial()
		{
			Setup(out _);
			Main.rand = new Terraria.Utilities.UnifiedRandom(5103);
			int body = SpawnLevy("Pvt. Vortex", Ground(0), ItemID.Minishark, new int[] { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings }, ItemID.MusketBall);
			if (body < 0) { Check("vortex: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			NPC target = Pin(Ground(14));
			int on = -1;
			for (int t = 0; t < 600 && on < 0; t++) { Step(1); if (sn.Dummy.vortexStealthActive) on = t; }
			Check($"vortex: stealth goes on in the fight (tick {on})", on >= 0);
			if (on < 0) return;
			target.GetGlobalNPC<HarnessTarget>().IsTarget = false; target.active = false; _pins.Clear();
			int off = -1;
			for (int t = 0; t < 900 && off < 0; t++) { Step(1); if (!sn.Dummy.vortexStealthActive) off = t; }
			Check($"vortex: stealth goes off once the fight is over (tick {off} after the enemy went)", off >= 0);
		}

		private static void SettingOffTrial()
		{
			Setup(out _);
			LevyConfig.Get.SoldiersUseSetAbilities = false;
			Main.rand = new Terraria.Utilities.UnifiedRandom(5104);
			int body = SpawnLevy("Pvt. NoKey", Ground(0), ItemID.DiamondStaff, new int[] { ItemID.AncientBattleArmorHat, ItemID.AncientBattleArmorShirt, ItemID.AncientBattleArmorPants });
			if (body < 0) { Check("setting off: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			Pin(Ground(16));
			int k0 = sn.KeyPressesOwn, storms = 0;
			for (int t = 0; t < 600; t++)
			{
				foreach (var r in RosterSystem.Instance.Regulars) if (r.CommanderName == Commander) r.Mana = r.MaxMana;
				Step(1);
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.type == ProjectileID.SandnadoFriendly) storms++; }
			}
			Check($"setting off: a levy in Forbidden armor never presses the set key ({sn.KeyPressesOwn - k0} presses, {storms} storm-ticks)", sn.KeyPressesOwn == k0 && storms == 0);
		}

		// ------------------------------------------------------------------------------------------------------------
		// Calamity
		// ------------------------------------------------------------------------------------------------------------
		private static int[] GodSlayerArmor() => new[] { ItemOf("CalamityMod", "GodSlayerHeadMelee"), ItemOf("CalamityMod", "GodSlayerChestplate"), ItemOf("CalamityMod", "GodSlayerLeggings") };

		private static void GodSlayerTrial()
		{
			int[] armor = GodSlayerArmor();
			if (armor.Any(a => a <= 0)) { Check("god slayer: the armor exists", false); return; }
			Setup(out int by);
			Main.rand = new Terraria.Utilities.UnifiedRandom(5201);
			int body = SpawnLevy("Pvt. Godslayer", Ground(0), ItemID.TerraBlade, armor);
			if (body < 0) { Check("god slayer: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			var line = new List<NPC>(); foreach (float x in new[] { 9f, 11f, 13f, 15f, 17f }) { var n = Pin(Ground(x)); if (n != null) line.Add(n); }
			int byLife = Life(by), d0 = sn.GsDashesOwn, startTick = -1; Vector2 bodyBefore = default, start = default; float far = 0f;
			for (int t = 0; t < 600 && startTick < 0; t++)
			{
				Vector2 before = Main.npc[body].Center;
				Step(1);
				if (sn.GsDashesOwn > d0) { startTick = t; bodyBefore = before; start = sn.GsLastStartAt; }
			}
			Check($"god slayer: a levy in God Slayer armor dashes at the enemy line (tick {startTick}, reason '{sn.GsLastReason}')", startTick >= 0 && sn.GsLastReason == "rip");
			if (startTick < 0) return;
			float fromCmd = Vector2.Distance(start, _cmdAt) / 16f;
			Check($"god slayer: the dash starts at the levy ({F(Vector2.Distance(start, bodyBefore) / 16f)} tiles from where he stood, {F(fromCmd)} from the commander)", Vector2.Distance(start, bodyBefore) <= 2f * 16f && fromCmd >= 60f);
			int dashTicks = 0; bool infernoSeen = false; var trace = new System.Text.StringBuilder();
			for (int t = 0; t < 120; t++)
			{
				Step(1);
				far = Math.Max(far, Vector2.Distance(Main.npc[body].Center, start));
				if (t % 8 == 0 && sn.GodSlayerDashing) trace.Append($" t{t}:({F((Main.npc[body].Center.X - start.X) / 16f)},{F((Main.npc[body].Center.Y - start.Y) / 16f)}) v({F(Main.npc[body].velocity.X)},{F(Main.npc[body].velocity.Y)})");
				if (GodSlayerCal.InfernoBuff > 0 && line.Any(n => n.active && n.HasBuff(GodSlayerCal.InfernoBuff))) infernoSeen = true;
				if (sn.GodSlayerDashing) dashTicks++; else if (t > 5) break;
			}
			int struck = line.Count(n => n.active && n.life < n.lifeMax);
			long dmg = line.Sum(n => (long)(n.lifeMax - n.life));
			Note($"god slayer dash: {dashTicks + 1} ticks, {F(far / 16f)} tiles from its start at most, strikes {sn.GsStrikesOwn} ({sn.GsDamageOwn} damage sent), enemies hurt {struck}/{line.Count} [{string.Join(", ", line.Select(n => n.lifeMax - n.life))}], inferno seen {infernoSeen}, cooldown left {GodSlayerCal.CooldownLeft(sn.Dummy)}");
			Note("god slayer dash path (tiles from its start; px a tick):" + trace);
			Check($"god slayer: the dash lasts its {GodSlayerCal.ForceEndAt} ticks ({dashTicks + 1})", Math.Abs(dashTicks + 1 - GodSlayerCal.ForceEndAt) <= 3);
			Check($"god slayer: the dash rips through the enemies along its path ({struck} of {line.Count} hurt, {dmg} damage, {sn.GsStrikesOwn} strikes)", struck >= 3 && sn.GsStrikesOwn >= 3 && dmg >= 3L * 1000L);
			Check($"god slayer: a struck enemy burns with God Slayer Inferno", infernoSeen || GodSlayerCal.InfernoBuff <= 0);
			Check($"god slayer: the dash carries the levy along its path ({F(far / 16f)} tiles)", far >= 12f * 16f);
			Check("god slayer: the commander 70 tiles away takes nothing (" + (CommanderWhole(out string why) ? why : why) + ")", CommanderWhole(out _));
			Check($"god slayer: the bare levy in the line takes nothing (life {byLife} -> {Life(by)})", Life(by) >= byLife && byLife > 0);
			int left0 = GodSlayerCal.CooldownLeft(sn.Dummy);
			Check($"god slayer: Calamity's God Slayer Dash cooldown is on the levy ({left0} of {GodSlayerCal.CooldownTicks} ticks left)", left0 > GodSlayerCal.CooldownTicks - 300 && left0 <= GodSlayerCal.CooldownTicks);
			int d1 = sn.GsDashesOwn;
			Step(600);
			int left1 = GodSlayerCal.CooldownLeft(sn.Dummy);
			Check($"god slayer: no second dash while the cooldown runs ({sn.GsDashesOwn - d1} in 600 ticks)", sn.GsDashesOwn == d1);
			Check($"god slayer: the cooldown ticks down at the real rate ({left0} -> {left1} over 600 ticks)", left0 - left1 >= 540 && left0 - left1 <= 660);
			// the cooldown is nearly over: the next dash comes as it ends
			Main.npc[body].Bottom = Ground(-6); Main.npc[body].velocity = Vector2.Zero; sn.NotifyTeleported(); // back from the line, so the target is in the dash's reach again (a melee levy stands closer than its 3 tile minimum)
			bool set = GodSlayerCal.SetCooldownLeft(sn.Dummy, 20);
			int again = -1;
			for (int t = 0; t < 300 && again < 0; t++) { Step(1); if (sn.GsDashesOwn > d1) again = t; }
			Check($"god slayer: the dash comes back when the cooldown ends (cooldown set to 20 ticks: {set}; dashed {again} ticks later)", set && again >= 15 && again <= 200);
		}

		/// <summary>Mid-dash a hit is Calamity's God Slayer dodge; out of the dash it is not; a hostile shot coming at the levy is dodged with the dash.</summary>
		private static void GodSlayerDodgeTrial()
		{
			int[] armor = GodSlayerArmor();
			if (armor.Any(a => a <= 0)) { Check("god slayer dodge: the armor exists", false); return; }
			Setup(out _);
			Main.rand = new Terraria.Utilities.UnifiedRandom(5202);
			int body = SpawnLevy("Pvt. Dodger", Ground(0), ItemID.TerraBlade, armor);
			if (body < 0) { Check("god slayer dodge: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			Step(90); // the stat pass and the gear frame know the set now
			NPC biter = Pin(Ground(-40)); biter.damage = 60;
			var outRes = sn.PlayerSequence(biter, null, 200, 1, -1);
			Check($"god slayer dodge: out of a dash a hit is not dodged (ran {outRes.Ran}, dodged {outRes.Dodged})", outRes.Ran && !outRes.Dodged);
			biter.damage = 0;
			// a hostile shot from 14 tiles away, flying at him
			Vector2 from = Main.npc[body].Center + new Vector2(14 * 16f, 0f);
			int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("SetAbilityWorldCheck"), from, new Vector2(-9f, 0f), ProjectileID.DeathLaser, 40, 0f, Main.myPlayer);
			if (pi >= 0 && pi < Main.maxProjectiles) { Main.projectile[pi].hostile = true; Main.projectile[pi].friendly = false; Main.projectile[pi].timeLeft = 120; }
			int d0 = sn.GsDashesOwn, at = -1;
			for (int t = 0; t < 60 && at < 0; t++) { Step(1); if (sn.GsDashesOwn > d0) at = t; }
			Note($"god slayer dodge: a hostile laser 14 tiles off at 9 px a tick: dash at tick {at}, reason '{sn.GsLastReason}'");
			Check($"god slayer dodge: a shot coming at the levy is dodged with the dash (tick {at}, reason '{sn.GsLastReason}')", at >= 0 && sn.GsLastReason == "dodge");
			if (at < 0) return;
			biter.damage = 60;
			var inRes = sn.PlayerSequence(biter, null, 200, 1, -1);
			Check($"god slayer dodge: mid-dash a hit is Calamity's God Slayer dodge (dashing {sn.GodSlayerDashing}, dodged {inRes.Dodged}, immunity {inRes.DummyImmune} ticks)", sn.GodSlayerDashing && inRes.Ran && inRes.Dodged);
			biter.damage = 0;
		}

		private static void GodSlayerOffTrial()
		{
			int[] armor = GodSlayerArmor();
			if (armor.Any(a => a <= 0)) return;
			Setup(out _);
			LevyConfig.Get.SoldiersUseSetAbilities = false;
			int body = SpawnLevy("Pvt. NoDash", Ground(0), ItemID.TerraBlade, armor);
			if (body < 0) { Check("god slayer off: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			foreach (float x in new[] { 9f, 11f }) Pin(Ground(x));
			Step(400);
			Check($"setting off: a levy in God Slayer armor never dashes ({sn.GsDashesOwn} dashes)", sn.GsDashesOwn == 0);
		}

		/// <summary>
		/// The Tarragon cloak through the set key, under both of Calamity's double-tap options a player can pick: Auto (the default; its key bound to Y) and Off. Calamity's On_Player.KeyDoubleTap detour drops the vanilla double-tap
		/// for the local player when the option is Off (and, off a server, when the key is bound and the option is Auto); the levy presses the way Calamity's own key does, which works under both.
		/// </summary>
		private static void CalamityKeyTrial()
		{
			int[] armor = { ItemOf("CalamityMod", "TarragonHeadMelee"), ItemOf("CalamityMod", "TarragonBreastplate"), ItemOf("CalamityMod", "TarragonLeggings") };
			int cloak = ModContent.TryFind("CalamityMod/TarragonCloak", out ModBuff cb) ? cb.Type : -1;
			if (armor.Any(a => a <= 0) || cloak < 0) { Check("calamity key: the Tarragon armor and its cloak exist", false); return; }
			Setup(out _);
			LevyConfig.Get.SoldiersUseSetAbilities = false; // the presses below are made by hand
			int body = SpawnLevy("Pvt. Tarragon", Ground(0), ItemID.TerraBlade, armor);
			if (body < 0) { Check("calamity key: the levy spawns", false); return; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			NPC target = Pin(Ground(6));
			Step(90);
			Dictionary<string, List<string>> ks = null; List<string> savedKeys = null; const string key = "CalamityMod/ArmorSetBonus";
			try { ks = PlayerInput.CurrentProfile?.InputModes[InputMode.Keyboard].KeyStatus; } catch (Exception) { ks = null; }
			if (ks != null) { ks.TryGetValue(key, out savedKeys); ks[key] = new List<string> { "Y" }; }
			Type cfgT = ModLoader.TryGetMod("CalamityMod", out Mod cal) ? cal.Code?.GetType("CalamityMod.CalamityClientConfig") : null;
			object cfg = cfgT?.GetField("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null);
			var opt = cfgT?.GetProperty("SetBonusDoubleTap");
			object savedOpt = cfg != null ? opt?.GetValue(cfg) : null;
			try
			{
				foreach (int mode in new[] { 1, 2 }) // SetBonusDoubleTapOptions: 0 On, 1 Auto, 2 Off
				{
					string label = mode == 1 ? "Auto, key bound to Y" : "Off";
					if (cfg == null || opt == null) { if (mode == 2) { Note("calamity key: Calamity's client option could not be read, the Off case is skipped"); break; } }
					else opt.SetValue(cfg, Enum.ToObject(opt.PropertyType, mode));
					void Strip() { int bi = sn.Dummy.FindBuffIndex(cloak); if (bi >= 0) sn.Dummy.DelBuff(bi); }
					Strip();
					SoldierNPC.PressViaDoubleTap = true;
					sn.PressSetBonusKey(target.Center, "lab-doubletap");
					bool viaTap = sn.Dummy.HasBuff(cloak);
					SoldierNPC.PressViaDoubleTap = false;
					Strip();
					sn.PressSetBonusKey(target.Center, "lab-key");
					bool viaKey = sn.Dummy.HasBuff(cloak);
					Note($"calamity key, double-tap option {label}: a press through the vanilla double-tap puts the cloak {(viaTap ? "on" : "off")}, the levy's press {(viaKey ? "on" : "off")}");
					Check($"calamity key: the Tarragon cloak goes on when the levy presses the set key (Calamity's double-tap option {label})", viaKey);
					Strip();
				}
			}
			finally
			{
				if (cfg != null && opt != null && savedOpt != null) opt.SetValue(cfg, savedOpt);
				if (ks != null) { if (savedKeys != null) ks[key] = savedKeys; else ks.Remove(key); }
			}
		}
	}
}
