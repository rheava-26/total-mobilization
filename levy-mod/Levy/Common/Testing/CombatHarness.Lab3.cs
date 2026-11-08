using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS LAB 3 (measurement only; spec token l3=1 on a calrev fight line). Adds to the calrev harness: proper arenas (a3 open, a3e enclosed, platform rows with gaps), REAL station tiles beside REAL armories
	/// (the game's own StationBuffSystem scan decides what a levy gets), life/mana supplied through RosterSystem.FeedCrystal, Well Fed / Plenty Satisfied on the levies (food=1|2), real hazard pay through
	/// SettlementSystem.HazardPay (hazard=1), boss/arena/tier-chosen ammo (ammo=best|plain), piercing-weapon preference for worms and creeper fights, rogue levies (rogue=N, Calamity only).
	/// Extra per-fight row: out.l3.tsv.
	/// </summary>
	public partial class CombatHarness
	{
		// -------------------------------------------------------------------------------------------- progression tables
		internal static int L3Life(string boss) => boss switch { "kslime" => 2, "ds" => 4, "eye" => 6, "eow" => 8, "brain" => 8, "hive" => 9, "perf" => 9, "qb" => 10, "skel" => 12, "sg" => 14, _ => 6 };
		internal static int L3ManaOther(string boss) => boss switch { "kslime" => 2, "ds" => 3, "eye" => 4, _ => 9 }; // maxed (200) from EoW/BoC on
		/// <summary>Station availability level (RESULTS3.md table): 0 = campfire, heart lantern, star in a bottle, sunflower; 1 (Eye) + bast statue; 2 (EoW/BoC on) + sharpening station; 3 (post-Skeletron: Slime God) + war table, bewitching table.
		/// Ammo Box and Crystal Ball are Hardmode purchases, never placed. Honey: levies have no honey station support, not placed.</summary>
		private static int L3Level(string boss) => boss switch { "kslime" or "ds" => 0, "eye" => 1, "sg" => 3, _ => 2 };
		private static bool L3Full(string boss) => L3Level(boss) >= 2; // supplies: mana maxed from EoW/BoC on
		/// <summary>Meteor Shot only exists after a meteor fell (a Shadow Orb / Crimson Heart smashed on the way to EoW/BoC): fights AFTER EoW/BoC are beaten.</summary>
		private static bool L3MeteorLegal(string boss) => boss is "hive" or "perf" or "qb" or "skel" or "sg";
		private static bool L3PierceBoss(string boss) => boss is "eow" or "brain" or "ds" or "perf" or "hive";

		private static bool L3Pierces(string weapon, string ammo)
		{
			try
			{
				Item it = CrMake(weapon); int shoot = it.shoot;
				if (it.useAmmo > 0 && !string.IsNullOrEmpty(ammo)) { Item am = CrMake(ammo); if (am.shoot > 0) shoot = am.shoot; }
				if (shoot <= 0 || !ContentSamples.ProjectilesByType.TryGetValue(shoot, out var pj)) return false;
				return pj.penetrate > 1 || pj.penetrate < 0;
			}
			catch { return false; }
		}

		/// <summary>Best ammo that is really available at this tier. Meteor Shot (bounces and pierces) only from the EoW/BoC fights on; piercing arrows against worms/creepers; plain = lab 1-2 ammo.</summary>
		internal static string L3Ammo(Item w, CrRun run, int tier)
		{
			if (w.useAmmo == AmmoID.Bullet)
			{
				if (run.AmmoMode == "plain") return tier >= 2 ? "Silver Bullet" : "Musket Ball";
				if (run.AmmoMode == "meteor" || L3MeteorLegal(run.Boss)) return CrTryItemId("Meteor Shot", out _) ? "Meteor Shot" : "Silver Bullet"; // "meteor" = not tier-legal what-if arm
				return "Silver Bullet";
			}
			if (w.useAmmo == AmmoID.Arrow)
			{
				if (run.AmmoMode == "plain") return "Flaming Arrow";
				if (L3PierceBoss(run.Boss)) return "Jester's Arrow"; // 10 arrows + 1 Fallen Star, pierces without limit; the wiki rates it above Unholy against EoW/BoC
				return tier >= 3 ? "Hellfire Arrow" : "Flaming Arrow";
			}
			return "";
		}

		/// <summary>ammo=mixed / mixedm: the armory holds a mixed hoard in the order a player collects it (plain first), levies carry no hand-assigned pouch and are served by the armory. mixedm adds the not-tier-legal Meteor Shot and Hellfire Arrow.</summary>
		private static readonly string[] L3MixedLegal = { "Musket Ball", "Wooden Arrow", "Silver Bullet", "Flaming Arrow", "Bone Arrow", "Unholy Arrow", "Jester's Arrow" };
		private static readonly string[] L3MixedExtra = { "Meteor Shot", "Hellfire Arrow" };
		private void L3StockMixed(CrRun run)
		{
			var names = L3MixedLegal.Concat(run.AmmoMode == "mixedm" ? L3MixedExtra : new string[0]).Where(n => CrTryItemId(n, out _)).ToList();
			foreach (var te in TileEntity.ByID.Values.OfType<ArmoryTE>())
			{
				Array.Clear(te.Storage, 0, te.Storage.Length); for (int i = 0; i < te.Storage.Length; i++) te.Storage[i] = new Item();
				for (int i = 0; i < te.IssueAmmo.Length; i++) te.IssueAmmo[i] = new Item();
				for (int i = 0; i < names.Count; i++) te.Storage[i] = CrMake(names[i], 4000);
			}
			Mod.Logger.Info($"{L3Log}mixed ammo armory: {string.Join(", ", names)} (4000 each, storage order)");
		}

		private void Lab3MixedPouch(SoldierRecord rec, CrLevySpec spec, Item w)
		{
			if (w.useAmmo <= 0 || w.useAmmo == AmmoID.None) return;
			var te = TileEntity.ByID.Values.OfType<ArmoryTE>().FirstOrDefault(); if (te == null) { Mod.Logger.Warn(L3Log + "mixed: no armory"); return; }
			rec.Gear.Ammo ??= new Item();
			int got = te.TakeAmmoFor(rec.Gear, w.useAmmo, LevyConstants.AmmoPouchMax);
			spec.Ammo = rec.Gear.Ammo.IsAir ? "" : rec.Gear.Ammo.Name;
			Mod.Logger.Info($"{L3Log}mixed pouch: {spec.Weapon} gets {rec.Gear.Ammo.stack}x {(rec.Gear.Ammo.IsAir ? "nothing" : rec.Gear.Ammo.Name)} (took {got}, best-ammo {(global::Levy.Common.Soldiers.AmmoRank.Off ? "OFF" : "on")})");
		}

		private static readonly string[] L3Sulph = { "CalamityMod/SulphurousHelmet", "CalamityMod/SulphurousBreastplate", "CalamityMod/SulphurousLeggings" };

		/// <summary>The lab-3 army: the tier tables of lab 2, piercing weapons weighted x3 for worms/creepers, class accessory, chosen ammo, supply targets, optional rogue levies.</summary>
		internal static List<CrLevySpec> Lab3Roll(CrRun r)
		{
			var T = CrTierOf(r.Boss); int tier = T.Index; bool cal = ModLoader.HasMod("CalamityMod");
			var rng = new Random(r.Seed * 7919 + 4242 + T.Index * 13);
			var list = new List<CrLevySpec>(); int total = r.N;
			int nr = (int)Math.Round(total * 0.3), nm = (int)Math.Round(total * 0.3), ne = (int)Math.Round(total * 0.2), ns = Math.Max(0, total - nr - nm - ne);
			foreach (var (cls, n) in new[] { ('R', nr), ('M', nm), ('E', ne), ('S', ns) })
				for (int k = 0; k < n; k++)
				{
					var pool = T.Weapons[cls].Where(x => !CrExcluded.Contains(x.w)).ToList();
					var weighted = new List<(string w, string ammo, int wt)>();
					foreach (var x in pool) { int wt = x.wt; if (L3PierceBoss(r.Boss) && (cls == 'R' || cls == 'M') && L3Pierces(x.w, x.ammo.Split('|')[0])) wt *= 3; weighted.Add((x.w, x.ammo, wt)); }
					int sum = weighted.Sum(x => x.wt), pick = rng.Next(sum), acc0 = 0; var chosen = weighted[0];
					foreach (var x in weighted) { acc0 += x.wt; if (pick < acc0) { chosen = x; break; } }
					var armor = T.Armor[cls][rng.Next(T.Armor[cls].Count)];
					string accessory = cls switch { 'E' => "Feral Claws", 'M' => "Band of Starpower", 'S' => "Hermes Boots", _ => "Band of Regeneration" };
					int lc = L3Life(r.Boss), mc = (cls == 'M' || L3Full(r.Boss)) ? 9 : L3ManaOther(r.Boss);
					string pot = tier >= 3 ? "Healing Potion" : "Lesser Healing Potion";
					list.Add(new CrLevySpec { Cls = cls, Weapon = chosen.w, Ammo = "", Armor = armor, Acc = new[] { accessory }, LifeCrystals = lc, ManaCrystals = mc, ManAtArms = false, Potions = rng.Next(3, 9), Potion = pot });
				}
			// The only team-buff accessory a player can really have before Hardmode: Calamity's Trinket of Chi (shrine chests, Pre-Hardmode; wearer gives every ally Chi regen). One copy, on the first mage (rogue levies replace summoners, so he stays).
			if (cal && L3Level(r.Boss) >= 1 && CrTryItemId("CalamityMod/TrinketofChi", out _)) { var chi = list.FirstOrDefault(x => x.Cls == 'M'); if (chi != null) chi.Acc = new[] { "CalamityMod/TrinketofChi" }; }
			// rogue levies replace the last RogueN levies (the summoners; the rest of the army is identical across arms)
			if (cal && r.RogueN > 0)
			{
				string[] weapons = r.Boss switch { "kslime" or "ds" => new[] { "CalamityMod/WulfrumKnife", "CalamityMod/WulfrumKnife" }, "eye" => new[] { "CalamityMod/WulfrumKnife", "CalamityMod/UrchinStinger" }, "eow" or "brain" or "hive" or "perf" => new[] { "CalamityMod/WulfrumKnife", "CalamityMod/Mycoroot" }, _ => new[] { "CalamityMod/WulfrumKnife", "CalamityMod/RotBall" } };
				string[] armor = tier >= 2 ? L3Sulph : CrWulfrum;
				for (int i = 0; i < r.RogueN && i < list.Count; i++)
				{
					int idx = list.Count - 1 - i; var old = list[idx];
					string wn = weapons[Math.Min(i, weapons.Length - 1)]; if (!CrTryItemId(wn, out _)) wn = "CalamityMod/WulfrumKnife";
					list[idx] = new CrLevySpec { Cls = 'T', Weapon = wn, Ammo = "", Armor = armor.All(a => CrTryItemId(a, out _)) ? armor : CrWulfrum, Acc = new string[0], LifeCrystals = old.LifeCrystals, ManaCrystals = old.ManaCrystals, Potions = old.Potions, Potion = old.Potion };
				}
			}
			foreach (var sp in list) { if (sp.Cls == 'R') { try { Item w = CrMake(sp.Weapon); if (r.Mixed) { sp.Ammo = ""; continue; } sp.Ammo = L3Ammo(w, r, tier); if (sp.Ammo == "" && w.useAmmo != AmmoID.None) sp.Ammo = CrDefaultAmmo(w.useAmmo); } catch { } } }
			return list;
		}

		// -------------------------------------------------------------------------------------------- arena
		private void CrBuildArena3(string kind)
		{
			float n = Main.maxTilesX / 4200f; n *= n;
			int band = (int)(60f + 10f * n + Main.worldSurface / 6.0) + 2;
			_crCx = _cx; int cx = _cx, half = 110, fy;
			// arena token: a3 | a3e (sealed), optional rN suffix = rows N tiles apart (a3r6, a3er5); default 7 (lab 3)
			int rowGap = 7; var rm = System.Text.RegularExpressions.Regex.Match(kind, @"r(\d+)$"); if (rm.Success) rowGap = Math.Clamp(int.Parse(rm.Groups[1].Value), 3, 12); L3RowGap = rowGap;
			bool sealedBox = kind.StartsWith("a3e");
			if (sealedBox)
			{
				fy = Math.Clamp((int)Main.rockLayer + 80, 100, Main.maxTilesY - 260);
				FaFill(cx - half - 25, fy - 100, cx + half + 25, fy + 35, true);
				FaFill(cx - half, fy - 90, cx + half, fy - 1, false);
			}
			else
			{
				fy = Math.Clamp(band + 75, 100, Main.maxTilesY - 120);
				FaFill(cx - 130, fy - 90, cx + 130, fy - 1, false);
				FaFill(cx - 130, fy, cx + 130, fy + 25, true);
				FaFill(cx - half - 1, fy - 90, cx - half - 1, fy + 25, true);
				FaFill(cx + half + 1, fy - 90, cx + half + 1, fy + 25, true);
			}
			CrF = fy;
			for (int k = 1; k <= 5; k++)
			{
				int py = fy - rowGap * k; int gap0 = cx - half + 10 + (k * 41) % 150;
				for (int x = cx - half + 6; x <= cx + half - 6; x++)
				{
					if (x >= gap0 && x < gap0 + 14) continue; // a gap per row, staggered, so the rows are connected vertically
					Tile t = Main.tile[x, py];
					t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.WallType = 0;
				}
			}
			_crArenaBuilt = kind;
			Mod.Logger.Info($"HARNESS-L3: arena '{kind}' built: floor row {fy}, half-width {half}, 5 platform rows with gaps; {(fy < Main.worldSurface ? "ABOVE" : "BELOW")} worldSurface");
		}

		// -------------------------------------------------------------------------------------------- stations (real tiles + real armories)
		private const string L3Log = "HARNESS-L3: ";
		private static List<(string name, int tile, int style)> L3Objects(int level)
		{
			var res = new List<(string, int, int)>();
			void A(string name, int itemId) { var it = ContentSamples.ItemsByType[itemId]; res.Add((name, it.createTile, it.placeStyle)); }
			A("Campfire", ItemID.Campfire); A("Sunflower", ItemID.Sunflower);
			if (level >= 1) A("BastStatue", L3ItemByTile(TileID.CatBast));
			if (level >= 2) A("SharpeningStation", ItemID.SharpeningStation);
			if (level >= 3) { A("WarTable", ItemID.WarTable); A("BewitchingTable", ItemID.BewitchingTable); }
			if (_dlTier >= 3) { A("AmmoBox", ItemID.AmmoBox); A("CrystalBall", ItemID.CrystalBall); }
			return res;
		}

		private static int L3ItemByTile(int tile) { for (int i = 1; i < ItemID.Count; i++) if (ContentSamples.ItemsByType[i].createTile == tile) return i; return ItemID.Campfire; }

		private static void L3ClearTEs()
		{
			foreach (var te in TileEntity.ByID.Values.Where(t => t is ArmoryTE || t is BarracksTE).ToList()) { TileEntity.ByPosition.Remove(te.Position); TileEntity.ByID.Remove(te.ID); }
		}

		private string _l3StationNote = "";
		private void Lab3Stations(CrRun run)
		{
			int cx = _crCx, fy = CrF; int level = run.Dl != "" ? DlStationLevel(run) : L3Level(run.Boss); bool full = level >= 2;
			L3ClearTEs();
			for (int x = cx - 110; x <= cx + 110; x++)
				for (int y = fy - 6; y <= fy - 1; y++) { Tile t = Main.tile[x, y]; if (t.HasTile) { t.HasTile = false; t.TileType = 0; } }
			var placed = new List<string>(); var failed = new List<string>();
			foreach (int x0 in new[] { cx - 75, cx, cx + 75 })
			{
				int x = x0 - 15;
				int armoryType = ModContent.TileType<ArmoryTile>();
				WorldGen.PlaceObject(x + 1, fy - 1, armoryType, true, 0);
				if (!TileEntity.ByID.Values.OfType<ArmoryTE>().Any(a => Math.Abs(a.Position.X - x) <= 3 && Math.Abs(a.Position.Y - (fy - 3)) <= 3)) { try { ModContent.GetInstance<ArmoryTE>().Place(x, fy - 3); } catch (Exception e) { failed.Add("armory:" + e.Message); } }
				x += 4;
				foreach (var (name, tile, style) in L3Objects(level))
				{
					if (name == "Sunflower") for (int dx = -1; dx <= 3; dx++) { Tile g = Main.tile[x + dx, fy]; g.HasTile = true; g.TileType = TileID.Grass; g.BlockType = Terraria.ID.BlockType.Solid; } // sunflowers anchor to grass
					bool ok = WorldGen.PlaceObject(x + 1, fy - 1, tile, true, style);
					(ok ? placed : failed).Add(name);
					x += 4;
				}
				int py = fy - 7;
				foreach (var (name, itemId, dx) in new[] { ("HeartLantern", ItemID.HeartLantern, -4), ("StarInBottle", ItemID.StarinaBottle, 4) })
				{
					var it = ContentSamples.ItemsByType[itemId];
					Tile pt = Main.tile[x0 + dx, py]; if (!pt.HasTile) { pt.HasTile = true; pt.TileType = TileID.Platforms; pt.BlockType = Terraria.ID.BlockType.Solid; }
					bool ok = WorldGen.PlaceObject(x0 + dx, py + 1, it.createTile, true, it.placeStyle);
					(ok ? placed : failed).Add(name);
				}
			}
			StationBuffSystemReset();
			int arm = TileEntity.ByID.Values.Count(t => t is ArmoryTE);
			Mod.Logger.Info($"{L3Log}stations (level {level}) armories={arm} placed={placed.Count} [{string.Join(",", placed.Distinct())}] failed=[{string.Join(",", failed.Distinct())}]");
			_l3StationNote = $"L{level}:arm{arm}:ok{placed.Count}:fail{failed.Count}";
			if (run.Mixed) L3StockMixed(run);
		}

		private static void StationBuffSystemReset()
		{
			try { typeof(StationBuffSystem).GetField("_stamp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, uint.MaxValue); } catch { }
		}

		// -------------------------------------------------------------------------------------------- levy setup
		/// <summary>The real supply path: one crystal at a time through RosterSystem.FeedCrystal (the armory's own call).</summary>
		private static void Lab3Supply(SoldierRecord rec, CrLevySpec spec)
		{
			for (int i = 0; i < spec.LifeCrystals; i++) RosterSystem.Instance.FeedCrystal(rec, ItemID.LifeCrystal);
			for (int i = 0; i < spec.ManaCrystals; i++) RosterSystem.Instance.FeedCrystal(rec, ItemID.ManaCrystal);
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
		}

		private void Lab3AfterSpawn(SoldierRecord rec, SoldierNPC sn, NPC n, CrRun run)
		{
			StationBuffSystemReset();
			if (run.Food >= 1) n.AddBuff(run.Food >= 2 ? BuffID.WellFed2 : BuffID.WellFed, 60 * 60 * 30);
			sn.StatsDirty = true; sn.RefreshStats();
		}

		private static int _l3Tick = -1, _l3Poison, _l3Irr;
		private static readonly Dictionary<string, int> _l3Flags = new(), _l3Buffs = new();
		private static int _l3Sampled, _l3SulphSet; private static readonly Dictionary<string, int> _l3JumpKit = new();
		private static float _l3DefSum; private static long _l3Hazard;
		private static string _l3Ammo = "";
		private static readonly int[] _l3Lv = new int[8]; // floor, row1..row5, air, above the top row
		private static double _l3HSum, _l3HSq, _l3BossH; private static int _l3HN, _l3BossHN;
		private static void L3Vert(NPC n)
		{
			float h = (CombatHarnessFloor * 16f - n.Bottom.Y) / 16f; // tiles above the floor
			int lv = 6;
			if (h > L3RowGap * 5 + 2.5f) lv = 7; else for (int k = 0; k <= 5; k++) if (Math.Abs(h - L3RowGap * k) <= 1.5f) { lv = k; break; }
			_l3Lv[lv]++; _l3HSum += h; _l3HSq += h * h; _l3HN++;
		}
		private static double _l3DCmdSum, _l3DCmdMax, _l3DCtrSum, _l3DCtrMax, _l3DBossSum; private static int _l3DN;
		/// <summary>Fight-zone telemetry: distance (tiles) of a living levy from the commander, from the arena centre and from the boss, sampled every 30 ticks.</summary>
		private void L3Dist(NPC n, CrState st)
		{
			Vector2 cmd = _crCmdPos; var cp = Main.player[0]; if (cp.active) cmd = cp.Center;
			double dc = Vector2.Distance(n.Center, cmd) / 16f, dz = Math.Abs(n.Center.X - _crCx * 16f) / 16f;
			NPC b = Main.npc[st.BossIdx]; double db = b.active ? Vector2.Distance(n.Center, b.Center) / 16f : 0;
			_l3DN++; _l3DCmdSum += dc; _l3DCtrSum += dz; _l3DBossSum += db; _l3DCmdMax = Math.Max(_l3DCmdMax, dc); _l3DCtrMax = Math.Max(_l3DCtrMax, dz);
		}
		private static int _l3ChainN, _l3ChainBad; private static double _l3ChainMax, _l3NaiveSum;
		/// <summary>Asserts the chain origin the vanilla draw would use (owner slot MountedCenter under the draw swap) sits on the levy; also records where the unswapped owner slot would have drawn it (the old bug).</summary>
		private static void L3ChainCheck()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.type >= Main.projHook.Length || !Main.projHook[p.type] || !p.TryGetGlobalProjectile(out global::Levy.Common.Movement.HookTag tag) || tag.Soldier < 0) continue;
				float err = tag.SwapError(p, out _);
				if (err == float.MaxValue) continue;
				_l3ChainN++; _l3ChainMax = Math.Max(_l3ChainMax, err); if (err > 3f) _l3ChainBad++;
				_l3NaiveSum += Vector2.Distance(Main.player[p.owner].MountedCenter, Main.npc[tag.Soldier].Center);
			}
		}
		internal static int CombatHarnessFloor; internal static int L3RowGap = 7;

		private void Lab3Begin(CrRun run, CrState s)
		{
			Array.Clear(_l3Lv, 0, _l3Lv.Length); _l3HSum = _l3HSq = _l3BossH = 0; _l3HN = _l3BossHN = 0; CombatHarnessFloor = CrF;
			L3LogAmmo(); global::Levy.Common.Util.LevyProf.Reset(); SoldierNPC.WmThinks = SoldierNPC.WmHolds = SoldierNPC.WmShifts = SoldierNPC.WmAims = SoldierNPC.WmAimCalls = SoldierNPC.WmPierce = 0;
			SoldierNPC.RowEvalsS = SoldierNPC.RowClimbsS = SoldierNPC.RowArriveS = SoldierNPC.RowStayS = SoldierNPC.RowDropsS = SoldierNPC.RowThreatEvalsS = 0; SoldierNPC.JumpFxCounts.Clear(); SoldierNPC.JumpFxProj = 0; SoldierNPC.JumpFxDmg = 0; SoldierNPC.JumpFxFailed = 0; _l3JumpKit.Clear(); _l3ChainN = _l3ChainBad = 0; _l3ChainMax = _l3NaiveSum = 0; SoldierNPC.ZoneClampsS = SoldierNPC.ZoneReturnsS = 0; _l3DN = 0; _l3DCmdSum = _l3DCmdMax = _l3DCtrSum = _l3DCtrMax = _l3DBossSum = 0;
			_l3Tick = -1; _l3Poison = 0; _l3Irr = 0; _l3Flags.Clear(); _l3Buffs.Clear(); _l3Sampled = 0; _l3SulphSet = 0; _l3DefSum = 0; _l3Hazard = 0;
			_l3Ammo = string.Join(",", s.Levies.Select(l => l.Spec.Weapon.Replace("CalamityMod/", "C:") + (string.IsNullOrEmpty(l.Spec.Ammo) ? "" : "/" + l.Spec.Ammo)));
			if (run.Hazard) Lab3Hazard(run, s);
			Mod.Logger.Info($"{L3Log}fight start: hp avg {s.Levies.Average(l => l.MaxHp):0} mana avg {s.Levies.Average(l => l.Rec.MaxMana):0} food={run.Food} hazard paid={s.Levies.Count(l => l.Rec.HazardPaid)} ammo={_l3Ammo}");
		}

		private static bool _l3AmmoLogged;
		private void L3LogAmmo()
		{
			if (_l3AmmoLogged) return; _l3AmmoLogged = true;
			foreach (string nm in new[] { "Musket Ball", "Silver Bullet", "Meteor Shot", "Flaming Arrow", "Jester's Arrow", "Unholy Arrow", "Hellfire Arrow", "Frostburn Arrow", "Bone Arrow", "Wooden Arrow" })
			{
				try
				{
					if (!CrTryItemId(nm, out _)) { Mod.Logger.Info(L3Log + "ammo " + nm + ": no such item"); continue; }
					Item a = CrMake(nm); ContentSamples.ProjectilesByType.TryGetValue(a.shoot, out var pj);
					Mod.Logger.Info($"{L3Log}ammo {nm}: dmg {a.damage} ammoId {a.ammo} shoot {a.shoot} penetrate {pj?.penetrate} aiStyle {pj?.aiStyle} tileCollide {pj?.tileCollide} usesLocalNPCImmunity {pj?.usesLocalNPCImmunity} localNPCHitCooldown {pj?.localNPCHitCooldown}");
				}
				catch (Exception e) { Mod.Logger.Info(L3Log + "ammo " + nm + " probe failed: " + e.Message); }
			}
		}

		private void Lab3Hazard(CrRun run, CrState s)
		{
			string how = "barracks";
			try
			{
				int type = ModContent.TileType<BarracksTile>(); int x = _crCx - 100, fy = CrF;
				WorldGen.PlaceObject(x, fy - 1, type, true, 0);
				var te = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault();
				if (te == null) { try { ModContent.GetInstance<BarracksTE>().Place(x - 1, fy - 3); te = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault(); } catch { } }
				if (te == null) throw new Exception("no BarracksTE");
				te.Treasury = 100_000_000; te.TownName = "LabTown";
				foreach (var l in s.Levies) { l.Rec.HomeBarracksId = te.ID; l.Rec.HomePos = te.Position; }
				long before = te.Treasury;
				bool ok = SettlementSystem.Instance.HazardPay(te, Main.player[0]);
				_l3Hazard = before - te.Treasury;
				how = ok ? "HazardPay() paid " + _l3Hazard + " copper" : "HazardPay() refused";
			}
			catch (Exception e) { how = "fallback flag (" + e.Message + ")"; }
			foreach (var l in s.Levies) if (!l.Rec.HazardPaid) { l.Rec.HazardPaid = true; how += "; flag forced on " + l.Rec.Name; }
			Mod.Logger.Info(L3Log + "hazard pay: " + how);
		}

		private static int? _l3IrrCache;
		private static int L3IrrType => _l3IrrCache ??= (ModContent.TryFind<ModBuff>("CalamityMod/Irradiated", out var b) ? b.Type : -1);

		private void Lab3Tick(CrRun run, CrState st, CrLevy l, SoldierNPC sn, NPC n)
		{
			if (_l3Tick != st.Tick)
			{
				_l3Tick = st.Tick;
				NPC b = Main.npc[st.BossIdx];
				if (run.Dbg && (st.Tick % 120 == 0 || (st.Tick < 100 && st.Tick % 6 == 0))) for (int q = 0; q < Main.maxNPCs; q++) { NPC pn = Main.npc[q]; if (pn.active && st.PartTypes.Contains(pn.type)) Mod.Logger.Info($"{L3Log}part t{st.Tick} {pn.FullName} life {pn.life}/{pn.lifeMax} pos {pn.Center.X / 16:0},{(CrF * 16f - pn.Bottom.Y) / 16:0}h vel {pn.velocity.X:0.0},{pn.velocity.Y:0.0} ai {pn.ai[0]:0},{pn.ai[1]:0},{pn.ai[2]:0},{pn.ai[3]:0} dtd {pn.dontTakeDamage} tgt {pn.target} p0 act {Main.player[0].active} dead {Main.player[0].dead} name {Main.player[0].name} dist {Vector2.Distance(Main.player[0].Center, pn.Center) / 16:0}t timeLeft {pn.timeLeft}"); }
				if (st.Tick % 10 == 0) L3ChainCheck();
				if (b.active && st.Tick % 30 == 0) { _l3BossH += (CrF * 16f - b.Bottom.Y) / 16f; _l3BossHN++; }
				if (b.active) { if (b.HasBuff(BuffID.Poisoned)) _l3Poison++; if (L3IrrType > 0 && b.HasBuff(L3IrrType)) _l3Irr++; }
				if (run.Food >= 1 && st.Tick % 3000 == 0) foreach (var lv in st.Levies) { var nn = Main.npc[lv.Idx]; if (nn.active) nn.AddBuff(run.Food >= 2 ? BuffID.WellFed2 : BuffID.WellFed, 60 * 60 * 30); }
			}
			if (run.Dl != "" && (st.Tick == 6 || st.Tick % 3000 == 0) && !l.Dead) { DlBuffs(run, n); sn.StatsDirty = true; } // buffImmune clears on the first update, so the potions go on a few ticks in
			if (run.Dl != "" && !run.DlOk) sn.Stations |= StationFlags.Honey; // honey pool emulated by the station flag (never from a tile scan)
			if (st.Tick % 30 == 0 && !l.Dead) { L3Vert(n); L3Dist(n, st); }
			if (st.Tick == 600 && !l.Dead)
			{
				_l3Sampled++;
				foreach (StationFlags f in Enum.GetValues(typeof(StationFlags))) { if (f == StationFlags.None || f == StationFlags.All) continue; if ((sn.Stations & f) != 0) { _l3Flags.TryGetValue(f.ToString(), out int c); _l3Flags[f.ToString()] = c + 1; } }
				for (int i = 0; i < NPC.maxBuffs; i++) if (n.buffType[i] > 0 && n.buffTime[i] > 0) { string bn = n.buffType[i] < BuffID.Count ? Lang.GetBuffName(n.buffType[i]) : (BuffLoader.GetBuff(n.buffType[i])?.Name ?? "b" + n.buffType[i]); _l3Buffs.TryGetValue(bn, out int c); _l3Buffs[bn] = c + 1; }
				_l3DefSum += n.defense;
				{ string jk = sn.EnabledJumpNames(); if (jk == "") jk = "none"; _l3JumpKit.TryGetValue(jk, out int jc); _l3JumpKit[jk] = jc + 1; }
				try { foreach (var mp in sn.Dummy.ModPlayers) if (mp.GetType().Name == "CalamityPlayer") { var f = mp.GetType().GetField("sulphurSet"); if (f != null && (bool)f.GetValue(mp)) _l3SulphSet++; } } catch { }
			}
		}

		private void Lab3Finish(CrRun run, CrState st)
		{
			try
			{
				string path = _crOut + ".l3.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\tboss\tseed\tresult\tavgHp\tavgMaxMana\tstations\tsampled\tstationFlags\tnpcBuffs\tavgDef10s\tsulphSetLevies\tpoisonSec\tirradSec\thazardCopper\thazardPaid\tfood\tweapons\tvFloorPct\tvPlatPct\tvAirPct\tvAbovePct\thMean\thStd\tvLevels\tbossHMean\tgaps\trowEvals\trowClimbs\trowArrive\trowStay\trowDrops\trowThreat\trowGap\tjumpKit\tjumpsFired\tjumpProj\tjumpFail\thookChainN\thookChainMaxErrPx\thookChainBad\thookChainNaiveMeanPx\tzoneClamps\tzoneReturns\tdCmdMean\tdCmdMax\tdCtrMean\tdCtrMax\tdBossMean\n");
				string P(int c) => (_l3HN == 0 ? 0 : 100.0 * c / _l3HN).ToString("0", CultureInfo.InvariantCulture);
				string F(Dictionary<string, int> d) => string.Join(" ", d.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "=" + kv.Value));
				File.AppendAllText(path, string.Join("\t", new object[] { run.Key, run.Boss, run.Seed, st.Result, st.Levies.Average(l => l.MaxHp).ToString("0", CultureInfo.InvariantCulture), st.Levies.Average(l => l.Rec.MaxMana).ToString("0", CultureInfo.InvariantCulture), _l3StationNote, _l3Sampled, F(_l3Flags), F(_l3Buffs),
					(_l3Sampled == 0 ? 0 : _l3DefSum / _l3Sampled).ToString("0.0", CultureInfo.InvariantCulture), _l3SulphSet, (_l3Poison / 60.0).ToString("0.0", CultureInfo.InvariantCulture), (_l3Irr / 60.0).ToString("0.0", CultureInfo.InvariantCulture), _l3Hazard, st.Levies.Count(l => l.Rec.HazardPaid), run.Food, _l3Ammo, P(_l3Lv[0]), P(_l3Lv[1] + _l3Lv[2] + _l3Lv[3] + _l3Lv[4] + _l3Lv[5]), P(_l3Lv[6]), P(_l3Lv[7]), (_l3HN == 0 ? 0 : _l3HSum / _l3HN).ToString("0.0", CultureInfo.InvariantCulture), (_l3HN == 0 ? 0 : Math.Sqrt(Math.Max(0, _l3HSq / _l3HN - Math.Pow(_l3HSum / _l3HN, 2)))).ToString("0.0", CultureInfo.InvariantCulture), string.Join(",", _l3Lv.Take(6).Select(P)), (_l3BossHN == 0 ? 0 : _l3BossH / _l3BossHN).ToString("0.0", CultureInfo.InvariantCulture), _l3HN, SoldierNPC.RowEvalsS, SoldierNPC.RowClimbsS, SoldierNPC.RowArriveS, SoldierNPC.RowStayS, SoldierNPC.RowDropsS, SoldierNPC.RowThreatEvalsS, L3RowGap, F(_l3JumpKit), string.Join(" ", SoldierNPC.JumpFxCounts.Select(kv => kv.Key + "=" + kv.Value)), SoldierNPC.JumpFxProj + "/" + SoldierNPC.JumpFxDmg, SoldierNPC.JumpFxFailed, _l3ChainN, _l3ChainMax.ToString("0.0", CultureInfo.InvariantCulture), _l3ChainBad, (_l3ChainN == 0 ? 0 : _l3NaiveSum / _l3ChainN).ToString("0", CultureInfo.InvariantCulture), SoldierNPC.ZoneClampsS, SoldierNPC.ZoneReturnsS, (_l3DN == 0 ? 0 : _l3DCmdSum / _l3DN).ToString("0.0", CultureInfo.InvariantCulture), _l3DCmdMax.ToString("0.0", CultureInfo.InvariantCulture), (_l3DN == 0 ? 0 : _l3DCtrSum / _l3DN).ToString("0.0", CultureInfo.InvariantCulture), _l3DCtrMax.ToString("0.0", CultureInfo.InvariantCulture), (_l3DN == 0 ? 0 : _l3DBossSum / _l3DN).ToString("0.0", CultureInfo.InvariantCulture) }) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn(L3Log + "finish row failed: " + e.Message); }
		}
	}
}
