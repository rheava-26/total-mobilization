using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of what opening a town NPC's shop costs with an army standing around. Logged as "[ShopLag]" lines when a world loads on a dedicated server, either with
	/// LEVY_SELFCHECK_WORLD=1 (the shipworld stage chains it after the officer world check) or with LEVY_SHOPLAG=1 (a lab run, standalone).
	/// <para/>A headless server cannot draw, so this runs the non-drawing half of the real open path for EVERY shop any loaded mod registers (NPCShopDatabase.AllShops): the chat button's own work
	/// (ModNPC.OnChatButtonClicked, then Chest.SetupShop = NPCShop.FillShop + every GlobalNPC.ModifyActiveShop), the shopping settings the open computes (ShopHelper.GetShoppingSettings, happiness
	/// report included) and the price of every shown item (Player.GetItemExpectedPrice, the call the shop window repeats while it is up). The scene is a town of the registered shopkeepers, commissioned
	/// officers, barracks and armories on a pylon network, and an army of levy bodies (LEVY_SHOPLAG_LEVIES, default 40). Each arm is timed in the SAME boot: the full scene, then with the levy bodies removed,
	/// then with the officers dismissed, so the difference is what the army adds.
	/// <para/>PASS rules: the median open of every shop stays under the frame budget (<see cref="BudgetMs"/>), and the full scene costs no more than a small multiple of the empty-army scene (plus a floor).
	/// What it cannot show: the draw of the shop window and the hover tooltip (a headless boot has no client).
	/// </summary>
	public class ShopLagCheck : ModSystem
	{
		/// <summary>One 60 fps frame. An open that costs more than this is a visible hitch.</summary>
		public const double BudgetMs = 16.7;
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool lab = Environment.GetEnvironmentVariable("LEVY_SHOPLAG") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_SHOPWORLD") != "0";
			if (!lab && !ship) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (lab) Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		// the server drops a slot with no connection every tick, and NPCs despawn with nobody on: slot 0 is kept alive while the world steps
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		private static void Drive()
		{
			if (!_armed || _done || !Main.dedServ) return;
			if (++_calls < 6) return;
			RunIfArmed();
		}

		/// <summary>Runs now when this boot armed it (the officer world check calls this through the summon world check chain, so checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; }
			_mod.Logger.Info($"[ShopLag] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ShopLag] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[ShopLag] OBSERVED " + s);

		private static void Step(int n) { for (int i = 0; i < n; i++) { Main.player[0].active = true; WorldStep.Update(); } }

		private static void Flatten(int cx, int floorY, int half = 14)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = floorY - 14; j < floorY; j++) { var t = Main.tile[i, j]; t.ClearEverything(); }
				var f = Main.tile[i, floorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
			}
		}

		private sealed class ShopCase { public string Label; public NPC Npc; public string ShopName; public int NpcType; }

		/// <summary>Per-shop timing, in milliseconds (medians over the repetitions).</summary>
		private struct Timing { public double Setup, Settings, Prices, Click; public int Items; public double Total => Click + Setup + Settings + Prices; }

		private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
		private static double Median(List<double> l) { if (l.Count == 0) return 0; var s = l.OrderBy(x => x).ToList(); return s[s.Count / 2]; }

		private static Timing TimeShop(ShopCase c, Player p, int reps)
		{
			var click = new List<double>(); var setup = new List<double>(); var sets = new List<double>(); var prices = new List<double>(); int items = 0;
			for (int r = -2; r < reps; r++) // two warm-up passes
			{
				long t0 = Stopwatch.GetTimestamp();
				try { string nm = null; if (c.Npc.ModNPC != null) c.Npc.ModNPC.OnChatButtonClicked(true, ref nm); } catch (Exception) { }
				long t1 = Stopwatch.GetTimestamp();
				var chest = new Chest();
				try { chest.SetupShop(c.ShopName, c.Npc); } catch (Exception) { }
				long t2 = Stopwatch.GetTimestamp();
				try { p.currentShoppingSettings = Main.ShopHelper.GetShoppingSettings(p, c.Npc); } catch (Exception) { }
				long t3 = Stopwatch.GetTimestamp();
				int n = 0;
				foreach (var it in chest.item)
				{
					if (it == null || it.IsAir) continue; n++;
					try { p.GetItemExpectedPrice(it, out _, out _); } catch (Exception) { }
				}
				long t4 = Stopwatch.GetTimestamp();
				items = n;
				if (r < 0) continue;
				click.Add(Ms(t1 - t0)); setup.Add(Ms(t2 - t1)); sets.Add(Ms(t3 - t2)); prices.Add(Ms(t4 - t3));
			}
			return new Timing { Click = Median(click), Setup = Median(setup), Settings = Median(sets), Prices = Median(prices), Items = items };
		}

		private static Item[] ShopItems(ShopCase c)
		{
			var chest = new Chest(); try { chest.SetupShop(c.ShopName, c.Npc); } catch (Exception) { }
			return chest.item;
		}

		public static string Stat(List<double> v) => v.Count == 0 ? "n/a" : $"median {Median(v):F3} max {v.Max():F3}";

		private static void Run()
		{
			int levies = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SHOPLAG_LEVIES"), out int lv) && lv >= 0 ? lv : 40;
			int reps = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SHOPLAG_REPS"), out int rp) && rp > 0 ? rp : 15;
			var log = _mod.Logger; var placed = new List<(int i, int j, int w, int h)>(); var townNpcs = new List<NPC>(); var recs = new List<SoldierRecord>(); var officers = new List<NPC>();
			var cases = new List<ShopCase>();
			Player p = Main.player[0] ?? (Main.player[0] = new Player()); bool pWas = p.active; p.active = true; p.name = "ShopTester";
			try
			{
				int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();
				var barracks = new List<BarracksTE>(); var armories = new List<ArmoryTE>();
				foreach (int bx in new[] { 600, 700 })
				{
					Flatten(bx + 1, FloorY); Flatten(bx + 5, FloorY);
					WorldGen.PlaceObject(bx + 1, FloorY - 1, barType, true);
					ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement(bx, FloorY - 3, barType, 0, 1, 0); placed.Add((bx, FloorY - 3, 3, 3));
					if (TileEntity.ByPosition.TryGetValue(new Point16(bx, FloorY - 3), out var e) && e is BarracksTE b) { b.TownName = "Shop" + bx; b.Treasury = 10_000_000; barracks.Add(b); }
					WorldGen.PlaceObject(bx + 4, FloorY - 1, armType, true);
					ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(bx + 4, FloorY - 3, armType, 0, 1, 0); placed.Add((bx + 4, FloorY - 3, 3, 3));
					if (TileEntity.ByPosition.TryGetValue(new Point16(bx + 4, FloorY - 3), out var e2) && e2 is ArmoryTE a) armories.Add(a);
				}
				foreach (var (px, style) in new[] { (608, 0), (1008, 1) })
				{
					Flatten(px + 1, FloorY, 2);
					WorldGen.PlaceObject(px + 1, FloorY - 1, TileID.TeleportationPylon, true, style);
					if (Main.tile[px + 1, FloorY - 2].HasTile && Main.tile[px + 1, FloorY - 2].TileType == TileID.TeleportationPylon)
					{ TETeleportationPylon.PlacementPreviewHook_AfterPlacement(px + 1, FloorY - 1, TileID.TeleportationPylon, style, 1, 0); placed.Add((px, FloorY - 4, 3, 4)); }
				}

				// one real instance of every NPC that has a shop in any loaded mod, standing in the town by the first barracks
				var seen = new HashSet<string>(); int k = 0;
				foreach (var shop in NPCShopDatabase.AllShops.ToList())
				{
					string full = shop.FullName; if (!seen.Add(full)) continue;
					int type = shop.NpcType; if (type <= 0) continue;
					NPC inst = townNpcs.FirstOrDefault(t => t.type == type);
					if (inst == null)
					{
						for (int i = Main.maxNPCs - 2; i >= 0; i--)
						{
							var n = Main.npc[i]; if (n.active) continue;
							n.SetDefaults(type); n.active = true; n.townNPC = true; n.homeless = false; n.homeTileX = barracks[0].Position.X + 6 + k % 20; n.homeTileY = FloorY - 2; n.life = n.lifeMax;
							n.position = new Vector2((barracks[0].Position.X + 6 + k % 20) * 16f, (FloorY - 4) * 16f); inst = n; townNpcs.Add(n); k++; break;
						}
					}
					if (inst == null) continue;
					cases.Add(new ShopCase { Label = full, Npc = inst, ShopName = full, NpcType = type });
				}
				Note($"{cases.Count} shops from {townNpcs.Count} shopkeeper NPCs ({string.Join(", ", cases.GroupBy(c => (c.Npc.ModNPC?.Mod.Name ?? "Terraria")).Select(g => g.Key + " " + g.Count()))})");
				SettlementSystem.Instance.InvalidateTownNpcs();

				// ---- the army: levy bodies, in modded accessories (a levy in modded gear runs a gear frame EVERY tick: SoldierNPC.GearUpkeep)
				var roster = RosterSystem.Instance;
				var gearPool = new List<int>();
				foreach (string modName in new[] { "CalamityMod", "ThoriumMod" })
					foreach (var kv in ContentSamples.ItemsByType) { var smp = kv.Value; if (smp?.ModItem != null && smp.ModItem.Mod.Name == modName && smp.accessory && !smp.vanity && gearPool.Count < 12 && gearPool.Count(x => ContentSamples.ItemsByType[x].ModItem.Mod.Name == modName) < 6) gearPool.Add(kv.Key); }
				for (int i = 0; i < levies; i++)
				{
					var home = barracks[i % barracks.Count];
					var rec = new SoldierRecord { Id = 71000 + i, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "Shop " + i, CommanderName = "ShopTester", CommanderIndex = 0, HomeBarracksId = home.ID };
					rec.CurrentHp = rec.MaxHp; roster.Regulars.Add(rec); recs.Add(rec);
					for (int a = 0; a < Math.Min(gearPool.Count, rec.Gear.Accessories.Length); a++) { var acc = new Item(); acc.SetDefaults(gearPool[(i + a) % gearPool.Count]); rec.Gear.Accessories[a] = acc; } // modded worn gear: the kind a real army wears
					roster.SpawnBody(rec, new Vector2((home.Position.X + 12 + i % 8) * 16f, (FloorY - 4) * 16f));
				}
				p.position = barracks[0].Center; p.statLife = p.statLifeMax2 = Math.Max(p.statLifeMax2, 400);
				roster.PreUpdateNPCs();
				Step(30);
				int upkeep = RosterSystem.Instance.ActiveBodies().Count(b => b != null && Upkeep(b));
				log.Info($"[ShopLag] gear: {gearPool.Count} modded accessories dealt out; {upkeep} levies run a gear frame every tick");
				log.Info($"[ShopLag] scene: {barracks.Count} barracks, {armories.Count} armories, {recs.Count(r => r.HasBody)}/{levies} levy bodies standing, {townNpcs.Count} shopkeepers, mods={string.Join(",", ModLoader.Mods.Where(m => m.Name != "ModLoader").Select(m => m.Name))}");

				// ---- the officers: commission every shopkeeper that can serve
				foreach (var t in townNpcs.ToList())
				{
					var g = OfficerSystem.Data(t); if (g == null) continue;
					g.Enrolled = true; g.Commander = "ShopTester"; g.Slot = 0; g.DaysUnpaid = 0; g.ResetRuntime(); t.netAlways = true; t.netUpdate = true;
				}
				Step(240);
				foreach (var t in townNpcs) if (OfficerSystem.Instance.AvatarOf(t) != null) officers.Add(t);
				log.Info($"[ShopLag] officers marching: {officers.Count} of {townNpcs.Count} shopkeepers ({string.Join(",", officers.Select(o => o.TypeName))})");
				Step(30);

				// ---- time the open: arm A = full scene
				var all = Measure(cases, p, reps, "A full scene (levies + officers + town)");
				// ---- arm D: what the WORLD costs per tick while a conversation is open (a shop window is up)
				OpenConversation(cases, p, townNpcs, levies);
				// ---- arm B: officers dismissed (back to plain townsfolk)
				foreach (var t in officers) { var g = OfficerSystem.Data(t); if (g != null) { OfficerSystem.Instance.Dismiss(p, t); g.Enrolled = false; g.Commander = ""; g.ResetRuntime(); } }
				Step(40);
				var noOff = Measure(cases, p, reps, "B no officers (levies + town)");
				// ---- arm C: levy bodies gone too
				foreach (var r in recs) { try { if (r.HasBody) Main.npc[r.BodyIndex].active = false; } catch { } }
				Step(5);
				var bare = Measure(cases, p, reps, "C no army (town only)");

				// ---- verdicts
				double worstA = all.Count == 0 ? 0 : all.Max(x => x.Value.Total), worstC = bare.Count == 0 ? 0 : bare.Max(x => x.Value.Total);
				string worstName = all.OrderByDescending(x => x.Value.Total).First().Key;
				Check($"every shop opens within one frame ({BudgetMs} ms) with the full army: worst {worstA:F2} ms ({worstName}); same town with no army: worst {worstC:F2} ms", worstA <= BudgetMs);
				double medA = Median(all.Select(x => x.Value.Total).ToList()), medC = Median(bare.Select(x => x.Value.Total).ToList());
				Check($"the army adds no measurable open cost: median shop {medA:F3} ms with the army vs {medC:F3} ms without (rule: within 2x + 1 ms)", medA <= medC * 2.0 + 1.0);
				Check($"{all.Count} shops were timed", all.Count > 0 && all.Values.All(t => t.Items >= 0));
			}
			catch (Exception e) { log.Warn("[ShopLag] threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(5).Select(l => l.Trim()))); Check("scene built and timed", false); }
			finally
			{
				foreach (var r in recs) { try { if (r.HasBody) Main.npc[r.BodyIndex].active = false; RosterSystem.Instance.Regulars.Remove(r); } catch { } }
				foreach (var t in officers) { var g = OfficerSystem.Data(t); if (g != null) { g.Enrolled = false; g.Commander = ""; g.ResetRuntime(); } }
				for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn && sn.OfficerMode) n.active = false; }
				foreach (var n in townNpcs) { n.active = false; n.townNPC = false; }
				foreach (var (i, j, w, h) in placed) for (int x = i; x < i + w; x++) for (int y = j; y < j + h; y++) if (WorldGen.InWorld(x, y)) Main.tile[x, y].ClearEverything();
				foreach (var id in new List<int>(TileEntity.ByID.Keys)) { var te = TileEntity.ByID[id]; if (te is BarracksTE || te is ArmoryTE || te is TETeleportationPylon) { TileEntity.ByID.Remove(id); TileEntity.ByPosition.Remove(te.Position); } }
				p.active = pWas;
				log.Info("[ShopLag] done");
			}
		}

		/// <summary>
		/// The cost the shop WINDOW adds each tick: the commander is talking to a shopkeeper (Player.SetTalkNPC, the state a shop window keeps for as long as it is up) and the world ticks with the army standing around.
		/// Ticks are timed in blocks, closed / open / closed / open, so a drifting machine load shows up in both. The identity guard's counters say how many hook dispatches mirrored the conversation into a levy's dummy.
		/// </summary>
		private static void OpenConversation(List<ShopCase> cases, Player p, List<NPC> town, int levies)
		{
			var log = _mod.Logger;
			NPC keeper = town.FirstOrDefault(t => t.type == NPCID.Merchant) ?? town.FirstOrDefault();
			if (keeper == null) { Check("a shopkeeper exists for the open-conversation arm", false); return; }
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SHOPLAG_TICKS"), out int tk) && tk > 0 ? tk : 400;
			// blocks: closed / open with the mirror written through SetTalkNPC (what 2.0.61 and 2.0.62 did) / closed / open with the cheap write (the fix)
			long cheapWrites = 0, slowWrites = 0; double[] sum = new double[3]; double[] worst = new double[3]; int[] n = new int[3]; long[] mirrored = new long[3], enters = new long[3], hidden = new long[3];
			var perTick = new List<double>[] { new List<double>(), new List<double>(), new List<double>() };
			try
			{
				for (int block = 0; block < 6; block++)
				{
					int kk = block % 2 == 0 ? 0 : (block / 2 == 0 ? 1 : 2); // 0 closed, 1 open slow, 2 open fast
					bool open = kk != 0;
					global::Levy.Common.Owner.TalkMirror.ForceSlow = kk == 1;
					if (open) p.SetTalkNPC(keeper.whoAmI, true); else p.SetTalkNPC(-1, true);
					long m0 = Guard("Mirrored"), e0 = Guard("Enters"), h0 = Guard("Hidden"); long cheap0 = global::Levy.Common.Owner.TalkMirror.Cheap, slow0 = global::Levy.Common.Owner.TalkMirror.Slow;
					for (int i = 0; i < ticks; i++)
					{
						Main.player[0].active = true;
						long t0 = Stopwatch.GetTimestamp(); WorldStep.Update(); double ms = Ms(Stopwatch.GetTimestamp() - t0);
						sum[kk] += ms; n[kk]++; if (ms > worst[kk]) worst[kk] = ms; perTick[kk].Add(ms);
					}
					mirrored[kk] += Guard("Mirrored") - m0; enters[kk] += Guard("Enters") - e0; hidden[kk] += Guard("Hidden") - h0; if (kk == 2) { cheapWrites += global::Levy.Common.Owner.TalkMirror.Cheap - cheap0; slowWrites += global::Levy.Common.Owner.TalkMirror.Slow - slow0; }
				}
				global::Levy.Common.Owner.TalkMirror.ForceSlow = false;
				p.SetTalkNPC(-1, true);
				double meanC = sum[0] / Math.Max(1, n[0]), meanS = sum[1] / Math.Max(1, n[1]), meanO = sum[2] / Math.Max(1, n[2]);
				string Row(string label, int k) => $"[ShopLag] arm D world tick, {levies} levies, {label}: mean {sum[k] / Math.Max(1, n[k]):F3} ms median {Median(perTick[k]):F3} worst {worst[k]:F2} ms; hook dispatches/tick {enters[k] / (double)Math.Max(1, n[k]):F1}, mirrored {mirrored[k] / (double)Math.Max(1, n[k]):F1}/tick";
				log.Info(Row("conversation closed", 0));
				log.Info(Row("conversation open, mirror through SetTalkNPC (as 2.0.61/2.0.62)", 1));
				log.Info(Row("conversation open, mirror by field write (as shipped)", 2));
				// the mirror's own two calls, timed alone on a levy-style dummy
				var dummy = new Player(); dummy.whoAmI = global::Levy.Content.NPCs.SoldierNPC.DummySlot;
				p.SetTalkNPC(keeper.whoAmI, true);
				const int N = 20000;
				var talk = new List<double>();
				for (int rep = 0; rep < 5; rep++)
				{
					long t0 = Stopwatch.GetTimestamp();
					for (int i = 0; i < N; i++) { dummy.SetTalkNPC(p.talkNPC, true); dummy.SetTalkNPC(-1, true); }
					talk.Add(Ms(Stopwatch.GetTimestamp() - t0) * 1000.0 / N);
				}
				p.SetTalkNPC(-1, true);
				// what SetTalkNPC does beyond writing the field: time vanilla's shopping-settings read on the same dummy, and a plain write of the field
				var fl = typeof(Player).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Where(f => f.Name.IndexOf("talkNPC", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
				log.Info("[ShopLag] Player talk fields: " + string.Join(", ", fl.Select(f => f.Name + ":" + f.FieldType.Name)));
				var gss = new List<double>();
				for (int rep = 0; rep < 5; rep++) { long t0 = Stopwatch.GetTimestamp(); for (int i = 0; i < N; i++) dummy.currentShoppingSettings = Main.ShopHelper.GetShoppingSettings(dummy, keeper); gss.Add(Ms(Stopwatch.GetTimestamp() - t0) * 1000.0 / N); }
				log.Info($"[ShopLag] ShopHelper.GetShoppingSettings alone: {Median(gss):F3} us per call");
				if (fl.Count > 0)
				{
					var wr = new List<double>();
					for (int rep = 0; rep < 5; rep++) { long t0 = Stopwatch.GetTimestamp(); for (int i = 0; i < N; i++) { fl[0].SetValue(dummy, keeper.whoAmI); fl[0].SetValue(dummy, -1); } wr.Add(Ms(Stopwatch.GetTimestamp() - t0) * 1000.0 / N); }
					log.Info($"[ShopLag] plain field write pair (reflection, an upper bound): {Median(wr):F3} us per pair");
				}
				var idle = new List<double>();
				for (int rep = 0; rep < 5; rep++)
				{
					long t0 = Stopwatch.GetTimestamp();
					for (int i = 0; i < N; i++) { dummy.SetTalkNPC(-1, true); dummy.SetTalkNPC(-1, true); }
					idle.Add(Ms(Stopwatch.GetTimestamp() - t0) * 1000.0 / N);
				}
				log.Info($"[ShopLag] mirror pair (SetTalkNPC(talking npc) + SetTalkNPC(-1)) with a conversation open: {Median(talk):F3} us per pair; with none open: {Median(idle):F3} us per pair");
				double dispatchPerTick = enters[1] / (double)Math.Max(1, n[1]);
				double medC = Median(perTick[0]), medO = Median(perTick[2]), medS = Median(perTick[1]);
				Check($"the open conversation adds no world cost: median tick {medO:F3} ms open vs {medC:F3} ms closed (rule: within 50% + 1 ms); through SetTalkNPC it was {medS:F3} ms", medO <= medC * 1.5 + 1.0);
				Check($"with a conversation open the guard mirrored it by field write only: {cheapWrites} writes, {slowWrites} through SetTalkNPC", slowWrites == 0 && (cheapWrites > 0 || mirrored[2] == 0));
			}
			finally { try { p.SetTalkNPC(-1, true); } catch { } }
		}

		/// <summary>A counter of the identity guard by name (0 when this build has none): read by reflection so the same file also builds on the releases before the guard, for the bisect.</summary>
		private static long Guard(string field)
		{
			try { var t = typeof(ShopLagCheck).Assembly.GetType("Levy.Common.Owner.IdentityGuard"); var f = t?.GetField(field, BindingFlags.Public | BindingFlags.Static); return f == null ? 0 : Convert.ToInt64(f.GetValue(null)); } catch (Exception) { return 0; }
		}

		private static bool Upkeep(SoldierNPC b) { try { var pi = typeof(SoldierNPC).GetProperty("UpkeepActive"); return pi != null && (bool)pi.GetValue(b); } catch (Exception) { return false; } }

		private static Dictionary<string, Timing> Measure(List<ShopCase> cases, Player p, int reps, string arm)
		{
			var res = new Dictionary<string, Timing>();
			foreach (var c in cases) { if (!c.Npc.active) continue; res[c.Label] = TimeShop(c, p, reps); }
			var sb = new System.Text.StringBuilder($"[ShopLag] arm {arm}: ms per open (median of {reps}); click | SetupShop | shopping settings | prices of the shown items\n");
			foreach (var kv in res.OrderByDescending(x => x.Value.Total).Take(12))
				sb.Append($"[ShopLag]   {kv.Key,-48} total {kv.Value.Total,8:F3}  click {kv.Value.Click,7:F3}  setup {kv.Value.Setup,7:F3}  settings {kv.Value.Settings,7:F3}  prices {kv.Value.Prices,7:F3}  items {kv.Value.Items}\n");
			sb.Append($"[ShopLag]   ALL {res.Count} shops: median total {Median(res.Values.Select(t => t.Total).ToList()):F3} ms, mean {(res.Count == 0 ? 0 : res.Values.Average(t => t.Total)):F3} ms, max {(res.Count == 0 ? 0 : res.Values.Max(t => t.Total)):F3} ms; settings median {Median(res.Values.Select(t => t.Settings).ToList()):F3} ms, setup median {Median(res.Values.Select(t => t.Setup).ToList()):F3} ms");
			_mod.Logger.Info(sb.ToString());
			return res;
		}
	}
}
