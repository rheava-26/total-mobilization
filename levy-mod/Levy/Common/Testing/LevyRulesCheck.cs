using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of "the army follows the player's rules", logged as "[LevyRules]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the summon world check chains it;
	/// LEVY_RULES_ONLY=1 runs it alone; LEVY_RULES_PART=regen|potion|fall|spike|drown|grade|whip|sets|acc picks one part). A flat arena, slot 0 standing in as the commander, the real Main.Update stepping the world,
	/// throwaway soldiers only; everything is removed again and nothing is saved.
	/// <list type="bullet">
	/// <item>REGEN: a levy's life regeneration against a real Player (a spare slot) wearing the same gear, over 30 s from a fresh wound, with the old flat rule printed beside them.</item>
	/// <item>POTION: the potion sickness a levy gets (Player.ApplyPotionDelay on its dummy) against a Player's, Philosopher's Stone included, and the drinking cadence from a real armory.</item>
	/// <item>FALL / SPIKE / DROWN: the damage a levy takes from a fall, spikes and an empty breath meter against Player.Hurt / Player.CheckDrowning of a Player in a spare slot; the config defaults.</item>
	/// <item>GRADE: the same weapon does the same damage at every grade. WHIP: a summoner's lashes a minute against a player's best rate, in three fights.</item>
	/// <item>SETS / ACC (LevyRulesCheck.Sets.cs): every armor set and accessory of every loaded mod, equipped on a levy and on a Player in a spare slot, effect by effect.</item>
	/// </list>
	/// </summary>
	public partial class LevyRulesCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod;
		private static Vector2 _cmdAt;
		private static bool _local; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 200, RefSlot = 250;
		private const string Commander = "RulesTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_RULES") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_running && _local) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running && _local) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the world checks chain it, one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time;
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time; }
			_mod.Logger.Info($"[LevyRules] took {(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency / 1000.0:0.0}s");
			_mod.Logger.Info($"[LevyRules] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[LevyRules] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[LevyRules] OBSERVED " + s);

		// =====================================================================================================
		// scaffolding
		// =====================================================================================================
		private static Action _perTick;
		private static int _floorY = FloorY;

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++) { KeepCommander(); ClearStrangers(); _perTick?.Invoke(); WorldStep.Update(); }
		}

		private static readonly HashSet<int> _keep = new();
		private static void ClearStrangers()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && WorldStep.IsNaturalHostile(n) && !_keep.Contains(i)) n.active = false; }
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = true; Main.time = 18000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void Arena(int fy, int half)
		{
			for (int i = Cx - half; i <= Cx + half; i++)
			{
				for (int j = fy - 120; j < fy; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, fy]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = fy + 1; j < fy + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
			_floorY = fy;
		}

		private static void PlaceCommander(float tilesFromCentre) { _cmdAt = new Vector2((Cx + tilesFromCentre) * 16f, (_floorY - 3) * 16f); Main.player[0].position = _cmdAt; }

		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }

		private static SoldierNPC Levy(Vector2 at, SoldierOrder order = SoldierOrder.Hold, Grade grade = Grade.Levy)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = grade, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Rules",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = order;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		private static void Remove(SoldierNPC s)
		{
			if (s == null) return;
			if (s.Record != null) { s.Record.Gear = new SoldierGear(); RosterSystem.Instance.Regulars.Remove(s.Record); }
			s.NPC.active = false;
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
					else if (n.active && WorldStep.IsNaturalHostile(n)) n.active = false;
				}
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				_perTick = null; _keep.Clear();
				SoldierNPC.FallDamageOverride = -1; SoldierNPC.TileHazardsOverride = -1; SoldierNPC.DrownOverride = -1; SoldierNPC.FlatRegenLab = false;
				if (Main.player[RefSlot] != null && Main.player[RefSlot].whoAmI == RefSlot && !Main.player[RefSlot].active) Main.player[RefSlot] = new Player();
			}
			catch (Exception) { }
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			string only = Environment.GetEnvironmentVariable("LEVY_RULES_PART") ?? "all";
			var want = new HashSet<string>(only.Split(','));
			bool all = want.Contains("all"); Func<string, bool> W = p => all || want.Contains(p);
			Note($"mods: {string.Join(",", ModLoader.Mods.Select(m => m.Name).Where(n => n != "ModLoader"))}; game mode {Main.GameMode}");
			if (W("regen")) { try { RegenPart(); } finally { Cleanup(); } }
			if (W("potion")) { try { PotionPart(); } finally { Cleanup(); } }
			if (W("fall")) { try { FallPart(); } finally { Cleanup(); } }
			if (W("spike")) { try { SpikePart(); } finally { Cleanup(); } }
			if (W("drown")) { try { DrownPart(); } finally { Cleanup(); } }
			if (W("grade")) { try { GradePart(); } finally { Cleanup(); } }
			if (W("whip")) { try { WhipPart(); } finally { Cleanup(); } }
			if (W("sets")) { try { SetsPart(); } finally { Cleanup(); } }
			if (W("acc")) { try { AccessoriesPart(); } finally { Cleanup(); } }
		}

		// =====================================================================================================
		// the reference: a real Player object in a spare slot, stepped the way Player.Update steps it
		// =====================================================================================================
		private static Player NewRef(int lifeMax)
		{
			var rp = new Player { whoAmI = RefSlot, active = true };
			int over = Math.Max(0, lifeMax - 100); rp.ConsumedLifeCrystals = Math.Min(15, over / 20); rp.ConsumedLifeFruit = Math.Min(20, (over - rp.ConsumedLifeCrystals * 20) / 5); // ResetEffects rebuilds statLifeMax from the crystals and fruit
			rp.statLifeMax = lifeMax; rp.statLifeMax2 = lifeMax; rp.statLife = lifeMax;
			rp.ConsumedManaCrystals = 9; rp.statManaMax = rp.statManaMax2 = rp.statMana = 200; // the levies in these checks carry nine mana crystals: gear that reads mana (Thorium's Titan armor below 75) must see the same pool
			rp.width = 20; rp.height = 42;
			for (int i = 0; i < rp.grappling.Length; i++) rp.grappling[i] = -1;
			return rp;
		}

		private static void RefEquip(Player rp, Item head, Item body, Item legs, params Item[] acc)
		{
			rp.armor[0] = head ?? new Item(); rp.armor[1] = body ?? new Item(); rp.armor[2] = legs ?? new Item();
			for (int k = 0; k < 7; k++) rp.armor[3 + k] = acc != null && k < acc.Length ? acc[k] : new Item();
			for (int k = 0; k < 10; k++) rp.armor[k].playerIndexTheItemIsReservedFor = RefSlot; // an item in a Player's slot is reserved for that Player (mods find the wearer through it)
		}

		/// <summary>Runs <paramref name="pass"/> with <paramref name="rp"/> as the machine's local player in its spare slot (the way a real Player runs the same hooks), restoring the machine afterwards.</summary>
		private static void AsRef(Player rp, Action pass, bool campfire = false, bool heartLantern = false)
		{
			Player prev = Main.player[RefSlot]; int my = Main.myPlayer;
			var scene = Main.SceneMetrics; bool c0 = scene.HasCampfire, h0 = scene.HasHeartLantern; // the local player's scene: the stations are the test's to say (the arena is not beside a campfire), not whatever the server last scanned
			Main.player[RefSlot] = rp; Main.myPlayer = RefSlot;
			scene.HasCampfire = campfire; scene.HasHeartLantern = heartLantern;
			try { pass(); }
			finally { Main.myPlayer = my; Main.player[RefSlot] = prev ?? new Player(); scene.HasCampfire = c0; scene.HasHeartLantern = h0; }
		}

		/// <summary>The head / body / legs fields Player.Update fills before the set pass.</summary>
		private static void RefSlots(Player rp) { rp.head = rp.armor[0].headSlot; rp.body = rp.armor[1].bodySlot; rp.legs = rp.armor[2].legSlot; }

		/// <summary>One Player.Update tick of the stat pass, in Update's order: ResetEffects, buffs, UpdateEquips, the armor sets, the mods' PostUpdateEquips and PostUpdateMiscEffects. No life regen.</summary>
		private static void RefStatPass(Player rp, bool misc = true)
		{
			rp.PotionDelayModifier = StatModifier.Default; if (rp.pStone) rp.PotionDelayModifier *= Player.PhilosopherStoneDurationMultiplier;
			rp.ResetEffects();
			RefSlots(rp);
			bool buffs = false; for (int b = 0; b < rp.buffType.Length; b++) if (rp.buffType[b] > 0) { buffs = true; break; }
			if (buffs) { PlayerLoader.PreUpdateBuffs(rp); rp.UpdateBuffs(RefSlot); PlayerLoader.PostUpdateBuffs(rp); }
			rp.UpdateEquips(RefSlot);
			rp.UpdateArmorSets(RefSlot);
			PlayerLoader.PostUpdateEquips(rp);
			if (misc) PlayerLoader.PostUpdateMiscEffects(rp);
		}

		// =====================================================================================================
		// REGEN
		// =====================================================================================================
		private sealed class RegenCase { public string Name; public int[] Acc = new int[0]; public int Buff; public (int h, int b, int l) Armor; }

		private static void RegenPart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			int mode0 = Main.GameMode; Main.GameMode = GameModeID.Master; // the world Eve plays: an unfed player regenerates at half rate
			try
			{
				var cases = new List<RegenCase>
				{
					new RegenCase { Name = "no gear" },
					new RegenCase { Name = "Band of Regeneration + Shiny Stone", Acc = new int[] { ItemID.BandofRegeneration, ItemID.ShinyStone } },
					new RegenCase { Name = "Regeneration potion", Buff = BuffID.Regeneration },
					new RegenCase { Name = "Well Fed", Buff = BuffID.WellFed },
					new RegenCase { Name = "Crimson armor", Armor = (ItemID.CrimsonHelmet, ItemID.CrimsonScalemail, ItemID.CrimsonGreaves) },
				};
				foreach (var c in cases) RegenCaseRun(c);
				// the campfire and the heart lantern: stations near the levy raise the same rate a player gets from them
				RegenStations();
			}
			finally { Main.GameMode = mode0; SoldierNPC.FlatRegenLab = false; }
		}

		private static SoldierNPC GearedLevy(Vector2 at, RegenCase c)
		{
			var s = Levy(at); if (s == null) return null;
			var g = s.Record.Gear;
			if (c.Armor.h != 0) { g.Head = Make(c.Armor.h); g.Body = Make(c.Armor.b); g.Legs = Make(c.Armor.l); }
			for (int k = 0; k < c.Acc.Length && k < g.Accessories.Length; k++) g.Accessories[k] = Make(c.Acc[k]);
			s.StatsDirty = true;
			return s;
		}

		private const int RegenWindow = 1800;

		/// <summary>HP a levy gains in <see cref="RegenWindow"/> ticks from a fresh wound (to 20% of its life).</summary>
		private static (int gain, int max) LevyGain(RegenCase c, bool flat, bool wellFedBuffOnly = false)
		{
			SoldierNPC.FlatRegenLab = flat;
			Vector2 at = new Vector2((Cx + 10) * 16f, _floorY * 16f);
			var s = GearedLevy(at, c);
			if (s == null) return (-1, 0);
			try
			{
				if (c.Buff > 0) s.NPC.AddBuff(c.Buff, 999999);
				Step(40);
				s.RefreshStats();
				int max = s.NPC.lifeMax, start = Math.Max(1, max / 5);
				s.NPC.life = start; s.Record.CurrentHp = start; s.TicksSinceDamaged = 0; s.Dummy.lifeRegenTime = 0f; s.Dummy.lifeRegenCount = 0;
				Vector2 stand = s.NPC.position;
				_perTick = () => { s.NPC.position.X = stand.X; s.NPC.velocity.X = 0f; if (c.Buff > 0 && !s.NPC.HasBuff(c.Buff)) s.NPC.AddBuff(c.Buff, 999999); };
				Step(RegenWindow);
				_perTick = null;
				return (s.NPC.life - start, max);
			}
			finally { Remove(s); SoldierNPC.FlatRegenLab = false; }
		}

		/// <summary>HP a real Player gains over the same window with the same gear and buff, from the same wound.</summary>
		private static int RefGain(RegenCase c, int max)
		{
			var rp = NewRef(max);
			RefEquip(rp, c.Armor.h != 0 ? Make(c.Armor.h) : null, c.Armor.h != 0 ? Make(c.Armor.b) : null, c.Armor.h != 0 ? Make(c.Armor.l) : null, c.Acc.Select(t => Make(t)).ToArray());
			int start = Math.Max(1, max / 5); rp.statLife = start; rp.lifeRegenTime = 0f; rp.lifeRegenCount = 0;
			if (c.Buff > 0) rp.AddBuff(c.Buff, 999999);
			AsRef(rp, () =>
			{
				for (int t = 0; t < RegenWindow; t++)
				{
					RefStatPass(rp, misc: false);
					rp.UpdateLifeRegen();
					if (c.Buff > 0 && !rp.HasBuff(c.Buff)) rp.AddBuff(c.Buff, 999999);
				}
			});
			return rp.statLife - start;
		}

		private static void RegenCaseRun(RegenCase c)
		{
			var flat = LevyGain(c, true);
			var real = LevyGain(c, false);
			int max = real.max > 0 ? real.max : 500;
			int refGain = RefGain(c, max);
			int tol = Math.Max(3, (int)Math.Round(refGain * 0.05));
			Note($"regen [{c.Name}]: 30 s from 20% of {max} HP: Player {refGain} HP, levy {real.gain} HP (old flat rule {flat.gain} HP)");
			Check($"regen [{c.Name}]: the levy heals what a Player with the same gear does ({real.gain} vs {refGain} HP in 30 s, tolerance {tol})", real.gain >= 0 && Math.Abs(real.gain - refGain) <= tol);
		}

		private static void RegenStations()
		{
			// Campfire + Heart Lantern: a levy carrying those station flags against a Player standing under the same scene flags (Main.SceneMetrics, which the local player reads)
			const StationFlags Flags = StationFlags.Campfire | StationFlags.HeartLantern;
			Vector2 at = new Vector2((Cx + 10) * 16f, _floorY * 16f);
			var s = Levy(at); if (s == null) { Check("regen [stations]: the levy spawns", false); return; }
			try
			{
				Step(40);
				int max = s.NPC.lifeMax, start = Math.Max(1, max / 5);
				s.NPC.life = start; s.Record.CurrentHp = start; s.TicksSinceDamaged = 0; s.Dummy.lifeRegenTime = 0f; s.Dummy.lifeRegenCount = 0;
				Vector2 stand = s.NPC.position;
				_perTick = () => { s.NPC.position.X = stand.X; s.NPC.velocity.X = 0f; s.Stations |= Flags; };
				Step(RegenWindow);
				int levy = s.NPC.life - start; _perTick = null;
				var rp = NewRef(max); rp.statLife = start; rp.lifeRegenTime = 0f; rp.lifeRegenCount = 0;
				AsRef(rp, () => { for (int t = 0; t < RegenWindow; t++) { RefStatPass(rp, misc: false); rp.UpdateLifeRegen(); } }, campfire: true, heartLantern: true);
				int pl = rp.statLife - start, tol = Math.Max(3, (int)Math.Round(pl * 0.05));
				Note($"regen [Campfire + Heart Lantern]: Player {pl} HP, levy {levy} HP in 30 s");
				Check($"regen [Campfire + Heart Lantern]: the levy heals what a Player under them does ({levy} vs {pl} HP, tolerance {tol})", Math.Abs(levy - pl) <= tol);
			}
			finally { _perTick = null; Remove(s); }
		}

		// =====================================================================================================
		// POTION
		// =====================================================================================================
		private static int RefPotionDelay(Item potion, params Item[] acc)
		{
			var rp = NewRef(500);
			RefEquip(rp, null, null, null, acc);
			int delay = 0;
			AsRef(rp, () =>
			{
				for (int t = 0; t < 3; t++) RefStatPass(rp, misc: false); // the Philosopher's Stone flag is read a tick after the equips set it, as in Player.Update
				var m = typeof(Player).GetMethod("ApplyPotionDelay", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Item) }, null);
				rp.potionDelay = 0; m.Invoke(rp, new object[] { potion }); delay = rp.potionDelay;
			});
			return delay;
		}

		private static void PotionPart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			Vector2 at = new Vector2((Cx + 10) * 16f, _floorY * 16f);
			Check("potion: the sickness a healing potion gives is vanilla's 60 s", LevyConstants.PotionSicknessTicks == Item.potionDelay && Item.potionDelay == 3600);
			var cases = new (string name, int potion, int[] acc)[]
			{
				("Healing Potion", ItemID.HealingPotion, new int[0]),
				("Restoration Potion", ItemID.RestorationPotion, new int[0]),
				("Healing Potion with a Philosopher's Stone", ItemID.HealingPotion, new int[] { ItemID.PhilosophersStone }),
			};
			foreach (var c in cases)
			{
				var s = Levy(at); if (s == null) { Check("potion [" + c.name + "]: the levy spawns", false); continue; }
				try
				{
					for (int k = 0; k < c.acc.Length; k++) s.Record.Gear.Accessories[k] = Make(c.acc[k]);
					s.StatsDirty = true; Step(10); s.RefreshStats(); s.RefreshStats();
					int levy = s.PotionDelayFor(Make(c.potion));
					int pl = RefPotionDelay(Make(c.potion), c.acc.Select(t => Make(t)).ToArray());
					Note($"potion [{c.name}]: Player {pl} ticks, levy {levy} ticks (the old cadence was {30 * 60})");
					Check($"potion [{c.name}]: the levy's potion sickness is the Player's ({levy} vs {pl} ticks)", pl > 0 && levy == pl);
				}
				finally { Remove(s); }
			}
			DrinkCadence(at, false);
			DrinkCadence(at, true);
		}

		/// <summary>A levy kept below half life beside a real armory with a stack of Healing Potions: how many it drinks in 7000 ticks.</summary>
		private static void DrinkCadence(Vector2 at, bool stone)
		{
			var s = Levy(at); if (s == null) { Check("potion cadence: the levy spawns", false); return; }
			ArmoryTE armory = null; int armoryId = -1;
			try
			{
				if (stone) s.Record.Gear.Accessories[0] = Make(ItemID.PhilosophersStone);
				s.StatsDirty = true; Step(10); s.RefreshStats(); s.RefreshStats();
				armory = new ArmoryTE { Position = new Point16((int)(at.X / 16f) + 1, (int)(at.Y / 16f) - 3) };
				armoryId = 900000 + (stone ? 1 : 0); armory.ID = armoryId; TileEntity.ByID[armoryId] = armory;
				armory.FitPotions();
				armory.IssuePotions[0] = Make(ItemID.HealingPotion, 30);
				int expectDelay = s.PotionDelayFor(Make(ItemID.HealingPotion));
				s.PotionSickness = 0;
				Vector2 stand = s.NPC.position;
				int low = Math.Max(1, s.NPC.lifeMax / 10), drinks = 0, last = armory.IssuePotions[0].stack; int firstGap = -1, firstTick = -1, secondTick = -1;
				_perTick = () => { s.NPC.life = Math.Min(s.NPC.life, low); s.Record.CurrentHp = s.NPC.life; s.NPC.position.X = stand.X; s.NPC.velocity.X = 0f; };
				const int Window = 7000;
				for (int t = 0; t < Window; t++)
				{
					Step(1);
					int now = armory.IssuePotions[0].stack;
					if (now < last) { drinks += last - now; if (firstTick < 0) firstTick = t; else if (secondTick < 0) secondTick = t; }
					last = now;
				}
				_perTick = null;
				firstGap = secondTick >= 0 ? secondTick - firstTick : -1;
				int expectDrinks = 1 + (Window - 1 - firstTick) / Math.Max(1, expectDelay);
				string name = stone ? "with a Philosopher's Stone" : "no gear";
				Note($"potion cadence [{name}]: {drinks} potions drunk in {Window} ticks ({Window / 60.0:0} s), first at tick {firstTick}, gap to the second {firstGap} ticks (the Player's delay {expectDelay}; the old cadence was 1800)");
				Check($"potion cadence [{name}]: the levy drinks no faster than the potion sickness lets a Player ({drinks} drunk, {expectDrinks} possible)", drinks >= 1 && drinks <= expectDrinks + 0 && firstGap >= expectDelay - 2 && (firstGap <= expectDelay + 60));
			}
			finally { _perTick = null; if (armoryId >= 0) TileEntity.ByID.Remove(armoryId); Remove(s); }
		}

		// =====================================================================================================
		// FALL
		// =====================================================================================================
		private static int RefHurt(int defense, int lifeMax, int deathSource, int damage)
		{
			var rp = NewRef(lifeMax); rp.statLife = lifeMax;
			AsRef(rp, () => { rp.ResetEffects(); rp.statDefense = Player.DefenseStat.Default; rp.statDefense += defense; rp.Hurt(PlayerDeathReason.ByOther(deathSource), damage, 0, quiet: true, dodgeable: false); });
			return lifeMax - rp.statLife;
		}

		private static void FallPart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			Check("fall: the default config has fall damage on", LevyConfig.Get.SoldiersTakeFallDamage);
			SoldierNPC.FallDamageOverride = -1; // the config's own default
			var s0 = Levy(new Vector2((Cx + 10) * 16f, _floorY * 16f)); if (s0 == null) { Check("fall: the levy spawns", false); return; }
			int lifeMax = s0.NPC.lifeMax; Remove(s0);
			int dropTiles = 25 + Math.Max(8, (int)(lifeMax * 0.05f)); // a fall that costs about half the life
			// 1. a plain levy
			(int hits, int dmg, int tiles, int def) Fall(int accItem)
			{
				var s = Levy(new Vector2((Cx + 10) * 16f, (_floorY - 3 - dropTiles) * 16f)); if (s == null) return (-1, 0, 0, 0);
				try
				{
					if (accItem > 0) { s.Record.Gear.Accessories[0] = Make(accItem); s.StatsDirty = true; s.RefreshStats(); }
					s.NPC.velocity = Vector2.Zero; Step(1);
					Step(240);
					return (s.FallHits, s.FallDamageTotal, s.LastFallTiles, s.NPC.defense);
				}
				finally { Remove(s); }
			}
			var f = Fall(0);
			int raw = (f.tiles - 25) * 10;
			int refDmg = RefHurt(f.def, lifeMax, 0, raw);
			Note($"fall: dropped from {dropTiles + 3} tiles, counted {f.tiles} tiles fallen, raw {raw}, defense {f.def}: levy lost {f.dmg}, Player.Hurt on the same defense {refDmg}");
			Check($"fall: with the default config a levy takes fall damage ({f.hits} hit, {f.dmg} HP)", f.hits >= 1 && f.dmg > 0);
			Check($"fall: the damage is the Player's ({f.dmg} vs {refDmg})", Math.Abs(f.dmg - refDmg) <= 1);
			// 2. the Lucky Horseshoe spares it, as it spares a Player
			var h = Fall(ItemID.LuckyHorseshoe);
			Check($"fall: a Lucky Horseshoe spares a levy ({h.hits} hits)", h.hits == 0);
			// 3. the setting off
			SoldierNPC.FallDamageOverride = 0;
			var off = Fall(0);
			Check($"fall: with the setting off a levy takes none ({off.hits} hits)", off.hits == 0);
			SoldierNPC.FallDamageOverride = -1;
		}

		// =====================================================================================================
		// SPIKES
		// =====================================================================================================
		private static void PutTile(int x, int y, int type)
		{
			Tile t = Main.tile[x, y]; t.ClearEverything(); t.HasTile = true; t.TileType = (ushort)type; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = BlockType.Solid;
		}

		private static void SpikePart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			Check("spike: the default config has tile hazards on", LevyConfig.Get.SoldiersTakeTileHazards);
			SoldierNPC.TileHazardsOverride = -1;
			int tx = Cx + 20;
			PutTile(tx, _floorY - 1, TileID.Spikes); PutTile(tx, _floorY - 2, TileID.Spikes);
			var s = Levy(new Vector2(tx * 16f + 8f, _floorY * 16f)); if (s == null) { Check("spike: the levy spawns", false); return; }
			try
			{
				s.NPC.lifeMax = s.NPC.life = 2000; s.Record.CurrentHp = 2000; // the check is about the tile, not about surviving it (PinLifeMax puts a body back to its record: only the first hits are read)
				Step(1);
				int def = s.NPC.defense;
				Step(200);
				int hits = s.HazardHits;
				double perHit = hits > 0 ? (double)s.HazardDamageTotal / hits : 0;
				int baseDmg = TileID.Sets.TouchDamageImmediate[TileID.Spikes];
				int refLo = RefHurt(def, 500, 3, (int)Math.Floor(baseDmg * 0.85)), refHi = RefHurt(def, 500, 3, (int)Math.Ceiling(baseDmg * 1.15)); // Main.DamageVar spreads the tile's damage by 15% each way
				Note($"spike: {hits} hits, {perHit:0.0} HP a hit at defense {def}; Player.Hurt on spikes ({baseDmg} +-15%): {refLo} to {refHi}");
				Check($"spike: with the default config a levy standing on spikes is hurt ({hits} hits)", hits >= 2);
				Check($"spike: each hit is what a Player takes from spikes ({perHit:0.0} against {refLo} to {refHi})", hits > 0 && perHit >= refLo - 1 && perHit <= refHi + 1);
				SoldierNPC.TileHazardsOverride = 0;
				int before = s.HazardHits; s.NPC.life = s.NPC.lifeMax; Step(120);
				Check("spike: with the setting off the same tiles do not hurt", s.HazardHits == before);
			}
			finally { SoldierNPC.TileHazardsOverride = -1; Remove(s); Arena(FloorY, Half); }
		}

		// =====================================================================================================
		// DROWNING
		// =====================================================================================================
		private static void Tank(int x0, int x1, int top)
		{
			for (int x = x0; x <= x1; x++)
				for (int y = top; y < _floorY; y++)
				{
					Tile t = Main.tile[x, y]; t.ClearEverything();
					if (x == x0 || x == x1) { t.HasTile = true; t.TileType = TileID.GrayBrick; t.BlockType = BlockType.Solid; }
					else { t.LiquidType = LiquidID.Water; t.LiquidAmount = 255; }
				}
		}

		private static void DrownPart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			Check("drown: the default config has drowning off", !LevyConfig.Get.SoldiersCanDrown);
			int x0 = Cx + 20, x1 = Cx + 40, top = _floorY - 12;
			Tank(x0, x1, top);
			Vector2 at = new Vector2((x0 + 10) * 16f + 8f, _floorY * 16f - 2f);
			(int breath600, int breathEnd, int lost, int lifeMax, int ticks) Trial(int mode, int gearItem = 0, int gearHead = 0)
			{
				SoldierNPC.DrownOverride = mode;
				var s = Levy(at); if (s == null) return (-1, -1, -1, 0, 0);
				try
				{
					if (gearItem > 0) s.Record.Gear.Accessories[0] = Make(gearItem);
					if (gearHead > 0) s.Record.Gear.Head = Make(gearHead);
					s.StatsDirty = true; Step(5); s.RefreshStats();
					s.NPC.Bottom = at; s.NPC.velocity = Vector2.Zero;
					int life0 = s.NPC.life; int b600 = -1; const int Window = 1700;
					_perTick = () => { s.NPC.Bottom = at; s.NPC.velocity = Vector2.Zero; };
					for (int t = 0; t < Window; t++) { Step(1); if (t == 599) b600 = s.BreathNow; if (!s.NPC.active) { return (b600, s.BreathNow, life0, s.NPC.lifeMax, t); } }
					_perTick = null;
					bool wet = s.NPC.wet;
					return (b600, s.BreathNow, life0 - s.NPC.life, s.NPC.lifeMax, wet ? Window : -Window);
				}
				finally { _perTick = null; Remove(s); }
			}
			// the Player's own: CheckDrowning on a Player at the same spot, the same ticks
			(int breath600, int breathEnd, int lost) RefTrial(Item head, Item acc)
			{
				var rp = NewRef(500);
				RefEquip(rp, head, null, null, acc != null ? new[] { acc } : new Item[0]);
				rp.position = at - new Vector2(rp.width / 2f, rp.height);
				int b600 = -1;
				AsRef(rp, () =>
				{
					for (int t = 0; t < 1700; t++)
					{
						rp.ResetEffects(); rp.UpdateEquips(RefSlot);
						rp.CheckDrowning();
						if (t == 599) b600 = rp.breath;
					}
				});
				return (b600, rp.breath, 500 - rp.statLife);
			}
			var on = Trial(1);
			var refOn = RefTrial(null, null);
			Note($"drown: submerged for 1700 ticks: levy breath {on.breath600} at 600 and {on.breathEnd} at the end, lost {on.lost} HP (wet {on.ticks > 0}); Player breath {refOn.breath600} and {refOn.breathEnd}, lost {refOn.lost} HP");
			Check($"drown: with the setting on the breath meter runs down as a Player's does ({on.breath600} vs {refOn.breath600} at 600 ticks)", on.ticks > 0 && Math.Abs(on.breath600 - refOn.breath600) <= 2);
			Check($"drown: an empty meter costs the same life ({on.lost} vs {refOn.lost} HP)", on.lost > 0 && Math.Abs(on.lost - refOn.lost) <= 6);
			var off = Trial(0);
			Check($"drown: with the setting off a submerged levy keeps its breath and its life (breath {off.breathEnd}, lost {off.lost})", off.ticks > 0 && off.lost == 0);
			// breath gear: Gills (the Neptune's Shell / Gills potion class) and the Diving Helmet spare a Player and spare a levy
			var helm = Trial(1, 0, ItemID.DivingHelmet);
			var refHelm = RefTrial(Make(ItemID.DivingHelmet), null);
			Note($"drown: Diving Helmet: levy breath {helm.breath600}/{helm.breathEnd} lost {helm.lost} HP; Player breath {refHelm.breath600}/{refHelm.breathEnd} lost {refHelm.lost} HP");
			Check($"drown: a Diving Helmet slows the meter as it does a Player's ({helm.breath600} vs {refHelm.breath600} at 600 ticks, {helm.lost} vs {refHelm.lost} HP lost)", helm.ticks > 0 && Math.Abs(helm.breath600 - refHelm.breath600) <= 2 && Math.Abs(helm.lost - refHelm.lost) <= 6);
			SoldierNPC.DrownOverride = -1;
			Arena(FloorY, Half);
		}

		// =====================================================================================================
		// GRADE
		// =====================================================================================================
		private static void GradePart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			Vector2 at = new Vector2((Cx + 10) * 16f, _floorY * 16f);
			Check("grade: no grade scales damage down (the table is gone, Shaken stays 0.6)", Math.Abs(LevyConstants.ShakenDamageMult - 0.6f) < 1e-6 && typeof(LevyConstants).GetField("GradeDamageMult") == null);
			Check("grade: the lash multiplier stays 4.0", Math.Abs(LevyConstants.WhipRealDamageMult - 4f) < 1e-6);
			foreach (int weapon in new[] { ItemID.Handgun, ItemID.CopperBroadsword, ItemID.BandofRegeneration * 0 + ItemID.SlimeStaff })
			{
				var rows = new List<(Grade g, int dmg, float mult)>();
				foreach (Grade g in new[] { Grade.Levy, Grade.ManAtArms, Grade.Champion })
				{
					var s = Levy(at, SoldierOrder.Hold, g); if (s == null) continue;
					try
					{
						s.Record.Gear.Weapon = Make(weapon); s.StatsDirty = true; Step(3); s.RefreshStats();
						var item = s.Record.Gear.Weapon;
						rows.Add((g, s.WeaponDamage(item), s.DamageMultiplier));
					}
					finally { Remove(s); }
				}
				string nm = Lang.GetItemNameValue(weapon);
				Note($"grade [{nm}]: " + string.Join(", ", rows.Select(r => $"{r.g} weapon damage {r.dmg} x{r.mult:0.00} = {(int)Math.Round(r.dmg * r.mult)}")));
				Check($"grade [{nm}]: a Levy, a Man-at-Arms and a Champion with the same weapon deal the same damage", rows.Count == 3 && rows.All(r => Math.Abs(r.dmg * r.mult - rows[0].dmg * rows[0].mult) < 0.001f && r.mult == 1f));
			}
		}
	}
}
