using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.Buffs;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;
using Levy.Common.Potions;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS TRIALS (LEVY_HARNESS_SCENARIO=bosstrial). Measurement only. Controlled balance experiment: early bosses (King Slime, Eye of Cthulhu, Brain of Cthulhu), vanilla items, a player-built arena.
	/// The commander stands still, drinking a SPECTATOR potion (the real SpectatorBuff on the stand-in player), so the boss targets the levies exclusively; the levies are Autonomous (the real AutonomousBuff, applied on the
	/// pulse AutonomySystem uses) unless the cell switches it off. LEVY_BT_SPEC = path of a TSV, one cell per line (see BtParseSpec). LEVY_BT_OUT = result TSV (appended per run; finished runs are skipped on restart).
	///  fight:  a fixed composition and tier (stats best | real), N trials.
	///  roll:   RANDOMLY ROLLED configurations (class mix, weapon from the best few on-tier weapons per class, armor, accessories, grade, life, mana, potions), K trials each; every configuration is logged to OUT.cfgs.tsv.
	///  ref:    one class/tier: a LEVY solo window and a PLAYER solo window (same armor, accessories, weapon, life, mana; vanilla Player.ItemCheck) against a pinned boss-like dummy (defense 12, 1,000,000 life, no damage).
	///  items:  logs candidate items and armor sets so the loadout lists are evidence based.
	/// </summary>
	public partial class CombatHarness
	{
		internal static bool BtOn => string.Equals(Scenario, "bosstrial", StringComparison.OrdinalIgnoreCase);
		private static bool _btRefPlayer;
		internal static bool BtRefPlayerOn => _btRefPlayer;

		// -------------------------------------------------------------------------------------------------- spec
		internal sealed class BtLevySpec
		{
			public char Cls; public string Weapon, Ammo; public string[] Armor = Array.Empty<string>(), Acc = Array.Empty<string>();
			public int LifeCrystals, ManaCrystals, Potions; public string Potion = ""; public bool ManAtArms;
			public int Hp => 100 + 20 * LifeCrystals; public int Mana => 20 + 20 * ManaCrystals;
			public string Describe() => $"{Cls}:{Weapon}{(string.IsNullOrEmpty(Ammo) ? "" : "/" + Ammo)}|{string.Join("+", Armor.Select(a => a.Split(' ')[0]).Distinct())}|{(Acc.Length == 0 ? "-" : string.Join("+", Acc))}|{(ManAtArms ? "MaA" : "Lv")}|{Hp}hp/{Mana}mp|{Potions}x{Potion}";
		}

		internal sealed class BtRun
		{
			public string Mode, Boss = "eye", Diff, Order, Arena = "big", Comp, Stats, Tier, Grade, Extra = "", CfgId = "fixed", Desc = "";
			public bool Auto, Camp = true, Honey, Spec = true, Pots = true, Beam = true; public int Seed, Cap, Trial;
			public List<BtLevySpec> Specs;
			public string Key => $"{Mode}|{Boss}|{Diff}|{Order}|{(Auto ? 1 : 0)}|{Arena}|{(Camp ? 1 : 0)}{(Honey ? "h" : "")}{(Spec ? "" : "ns")}{(Pots ? "" : "np")}{(Beam ? "" : "nb")}|{CfgId}|{Comp}|{Stats}|{Tier}|{Grade}|{Extra}|{Trial}";
		}
		private readonly List<BtRun> _btRuns = new();
		private int _btI = -1; private bool _btStarted;
		private string _btOut;
		private static bool _btHooked;

		private static Dictionary<string, string> BtKv(string extra)
		{
			var d = new Dictionary<string, string>();
			if (string.IsNullOrWhiteSpace(extra) || extra == "-") return d;
			foreach (var kv in extra.Split(';', StringSplitOptions.RemoveEmptyEntries)) { int e = kv.IndexOf('='); if (e > 0) d[kv.Substring(0, e).Trim()] = kv.Substring(e + 1).Trim(); }
			return d;
		}

		/// <summary>Spec line (tab separated): mode(fight|roll|ref|items) diff(classic|expert) order(free|sd) auto(0|1) comp(R3M3E2S2|m10|a4|b4|any) stats(best|real|roll) tier(opt|sub|junk|pre|post) grade(levy|champ) trials-or-config-range cap extra(k=v;...: boss= arena= camp= honey= trials= w:R= ...)</summary>
		private void BtParseSpec(string path)
		{
			var done = new HashSet<string>();
			if (File.Exists(_btOut)) foreach (var l in File.ReadLines(_btOut).Skip(1)) { var c = l.Split('\t'); if (c.Length > 1) done.Add(c[0]); }
			int skipped = 0; var cfgLog = new List<string>();
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;
				var f = line.Split('\t');
				if (f.Length < 10) { Mod.Logger.Warn("HARNESS-BT: bad spec line: " + line); continue; }
				var kv = BtKv(f.Length > 10 ? f[10] : "");
				string seeds = f[8]; int s0 = 1, s1 = 1;
				if (seeds.Contains('-')) { var p = seeds.Split('-'); s0 = int.Parse(p[0]); s1 = int.Parse(p[1]); } else { s1 = int.Parse(seeds); }
				string boss = kv.GetValueOrDefault("boss", "eye"), arena = kv.GetValueOrDefault("arena", "big");
				bool camp = kv.GetValueOrDefault("camp", "1") == "1", honey = kv.GetValueOrDefault("honey", "0") == "1", spec = kv.GetValueOrDefault("spec", "1") == "1", pots = kv.GetValueOrDefault("potions", "1") == "1", beam = kv.GetValueOrDefault("beam", "1") == "1";
				int cap = int.Parse(f[9]) > 0 ? int.Parse(f[9]) : 18000;
				string cleanExtra = string.Join(";", kv.Where(k => k.Key.Contains(':')).Select(k => k.Key + "=" + k.Value));
				Action<BtRun> add = r => { if (done.Contains(r.Key)) skipped++; else _btRuns.Add(r); };
				if (f[0] == "roll")
				{
					int trials = int.Parse(kv.GetValueOrDefault("trials", "3"));
					for (int cfg = s0; cfg <= s1; cfg++)
					{
						string cfgId = $"{boss}-{f[6]}-{f[4]}-{cfg}";
						var specs = BtRoll(boss, f[6], f[4], cfg, out string comp);
						string desc = string.Join(" ; ", specs.Select(sp => sp.Describe()));
						cfgLog.Add($"{cfgId}\t{comp}\t{desc}");
						for (int t = 1; t <= trials; t++)
							add(new BtRun { Mode = "fight", Boss = boss, Diff = f[1], Order = f[2], Auto = f[3] == "1", Arena = arena, Camp = camp, Honey = honey, Spec = spec, Pots = pots, Beam = beam, Comp = comp, Stats = "roll", Tier = f[6], Grade = "mixed", CfgId = cfgId, Trial = t, Seed = cfg * 100 + t, Cap = cap, Specs = specs, Desc = desc });
					}
				}
				else
				{
					string comp = BtCompCode(f[4]);
					for (int s = s0; s <= s1; s++)
					{
						var r = new BtRun { Mode = f[0], Boss = boss, Diff = f[1], Order = f[2], Auto = f[3] == "1", Arena = arena, Camp = camp, Honey = honey, Spec = spec, Pots = pots, Beam = beam, Comp = comp, Stats = f[5], Tier = f[6], Grade = f[7], Trial = s, Seed = s, Cap = cap, Extra = cleanExtra, CfgId = "fixed-" + f[6] + "-" + f[5] };
						add(r);
					}
				}
			}
			if (cfgLog.Count > 0) try { File.AppendAllLines(_btOut + ".cfgs.tsv", cfgLog); } catch { }
			Mod.Logger.Info($"HARNESS-BT: spec {path}: {_btRuns.Count} runs queued, {skipped} already in {_btOut}");
		}

		private static string BtCompCode(string c) => c switch { "m10" => "R3M3E2S2", "a4" => "R1M1E1S1", "b4" => "R2M2", _ => c };

		private static List<(char cls, int n)> BtCompParse(string code)
		{
			var l = new List<(char, int)>();
			for (int i = 0; i < code.Length;)
			{
				char c = code[i++]; int n = 0;
				while (i < code.Length && char.IsDigit(code[i])) n = n * 10 + (code[i++] - '0');
				if (n > 0) l.Add((c, n));
			}
			return l;
		}

		// -------------------------------------------------------------------------------------------------- items / loadouts
		internal sealed class BtLoad { public string Weapon, Ammo; public string[] Armor = Array.Empty<string>(); public string[] Acc = Array.Empty<string>(); }
		private static string[] BtSet(string a, string b, string c) => new[] { a, b, c };
		private static readonly string[] BtPlat = BtSet("Platinum Helmet", "Platinum Chainmail", "Platinum Greaves");
		private static readonly string[] BtGold = BtSet("Gold Helmet", "Gold Chainmail", "Gold Greaves");
		private static readonly string[] BtNinja = BtSet("Ninja Hood", "Ninja Shirt", "Ninja Pants");
		private static readonly string[] BtFossil = BtSet("Fossil Helmet", "Fossil Plate", "Fossil Greaves");
		private static readonly string[] BtJungle = BtSet("Jungle Hat", "Jungle Shirt", "Jungle Pants");
		private static readonly string[] BtMeteor = BtSet("Meteor Helmet", "Meteor Suit", "Meteor Leggings");
		private static readonly string[] BtShadow = BtSet("Shadow Helmet", "Shadow Scalemail", "Shadow Greaves");
		private static readonly string[] BtCrimson = BtSet("Crimson Helmet", "Crimson Scalemail", "Crimson Greaves");
		private static readonly string[] BtMolten = BtSet("Molten Helmet", "Molten Breastplate", "Molten Greaves");
		private static readonly string[] BtIron = BtSet("Iron Helmet", "Iron Chainmail", "Iron Greaves");
		private static readonly string[] BtCopper = BtSet("Copper Helmet", "Copper Chainmail", "Copper Greaves");
		private static readonly string[] BtWood = BtSet("Wood Helmet", "Wood Breastplate", "Wood Greaves");

		/// <summary>Fixed loadouts, key = class letter + "/" + tier. Vanilla, obtainable before the Eye of Cthulhu is beaten (docs/BOSS_TRIALS.md lists the sourcing).</summary>
		private static readonly Dictionary<string, BtLoad> BtLoads = new()
		{
			["R/opt"] = new() { Weapon = "Minishark", Ammo = "Silver Bullet", Armor = BtFossil, Acc = new[] { "Hermes Boots" } },
			["R/sub"] = new() { Weapon = "Boomstick", Ammo = "Musket Ball", Armor = BtIron },
			["R/junk"] = new() { Weapon = "Wooden Bow", Ammo = "Wooden Arrow", Armor = BtWood },
			["M/opt"] = new() { Weapon = "Diamond Staff", Armor = BtJungle, Acc = new[] { "Band of Starpower" } },
			["M/sub"] = new() { Weapon = "Amethyst Staff", Armor = BtGold },
			["M/junk"] = new() { Weapon = "Amethyst Staff", Armor = BtCopper },
			["E/opt"] = new() { Weapon = "Enchanted Sword", Armor = BtShadow, Acc = new[] { "Feral Claws" } },
			["E/sub"] = new() { Weapon = "Platinum Broadsword", Armor = BtIron },
			["E/junk"] = new() { Weapon = "Copper Shortsword", Armor = BtCopper },
			["S/opt"] = new() { Weapon = "Slime Staff", Armor = BtCrimson, Acc = new[] { "Hermes Boots" } },
			["S/sub"] = new() { Weapon = "Finch Staff", Armor = BtIron },
			["S/junk"] = new() { Weapon = "Finch Staff", Armor = BtWood },
		};
		/// <summary>On-tier gear for the Brain of Cthulhu fights: what a player has after the Eye of Cthulhu and before Skeletron (the Underworld and the second evil are open, the Dungeon is not).</summary>
		private static readonly Dictionary<string, BtLoad> BtLoadsPost = new()
		{
			["R/opt"] = new() { Weapon = "Phoenix Blaster", Ammo = "Silver Bullet", Armor = BtNinja, Acc = new[] { "Hermes Boots" } },
			["M/opt"] = new() { Weapon = "Space Gun", Armor = BtMeteor, Acc = new[] { "Band of Starpower" } },
			["E/opt"] = new() { Weapon = "Enchanted Sword", Armor = BtMolten, Acc = new[] { "Feral Claws" } },
			["S/opt"] = new() { Weapon = "Imp Staff", Armor = BtCrimson, Acc = new[] { "Hermes Boots" } },
		};
		private static Dictionary<string, string> BtOverrides = new();

		private static Dictionary<string, int> _btNameIds;
		internal static int BtItemId(string name)
		{
			if (_btNameIds == null)
			{
				_btNameIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				for (int k = 1; k < ItemID.Count; k++)
				{
					string n = Lang.GetItemNameValue(k);
					if (!string.IsNullOrEmpty(n) && !_btNameIds.ContainsKey(n)) _btNameIds[n] = k;
				}
			}
			if (!_btNameIds.TryGetValue(name, out int id)) throw new Exception("unknown item '" + name + "'");
			return id;
		}
		private static Item BtMake(string name, int stack = 1) { var it = new Item(); it.SetDefaults(BtItemId(name)); it.stack = stack; return it; }

		private static BtLoad BtLoadFor(char cls, string tier, bool post = false)
		{
			string tk = cls + "/" + (tier == "pre" || tier == "post" ? "opt" : tier);
			var b = post && BtLoadsPost.TryGetValue(tk, out var pb) ? pb : BtLoads[tk];
			var l = new BtLoad { Weapon = b.Weapon, Ammo = b.Ammo, Armor = b.Armor, Acc = b.Acc };
			if (BtOverrides.TryGetValue("pick:" + cls, out string pool)) l.Weapon = pool.Split('|').OrderByDescending(n => WeaponRank.Of(BtMake(n))).First(); // the levy takes the top-ranked weapon of the pool (what the allocator does)
			if (BtOverrides.TryGetValue("w:" + cls, out string w)) l.Weapon = w;
			if (BtOverrides.TryGetValue("a:" + cls, out string a)) l.Ammo = a;
			if (BtOverrides.TryGetValue("r:" + cls, out string r)) l.Armor = r.Split('+');
			if (BtOverrides.TryGetValue("c:" + cls, out string c)) l.Acc = c.Length == 0 || c == "-" ? Array.Empty<string>() : c.Split('+');
			return l;
		}

		private static void BtApplyExtra(string extra) { BtOverrides = BtKv(extra); }

		private static bool BtPost(string boss) => boss == "brain" || boss == "eow";

		private static List<BtLevySpec> BtFixedSpecs(BtRun run)
		{
			var list = new List<BtLevySpec>();
			bool best = run.Stats == "best", post = BtPost(run.Boss);
			foreach (var (cls, n) in BtCompParse(run.Comp))
				for (int k = 0; k < n; k++)
				{
					var ld = BtLoadFor(cls, run.Tier, post);
					list.Add(new BtLevySpec { Cls = cls, Weapon = ld.Weapon, Ammo = ld.Ammo, Armor = ld.Armor, Acc = ld.Acc, LifeCrystals = best ? 15 : post ? 5 : 2, ManaCrystals = best ? 9 : post ? 9 : (cls == 'M' ? 4 : 0), Potions = best ? 6 : 4, Potion = post ? "Healing Potion" : "Lesser Healing Potion", ManAtArms = run.Grade == "champ" });
				}
			return list;
		}

		// ---- rolled configurations
		private static readonly Dictionary<char, (string w, string ammo)[]> BtPoolPre = new()
		{
			['R'] = new[] { ("Minishark", "Silver Bullet|Musket Ball"), ("Boomstick", "Silver Bullet|Musket Ball"), ("Demon Bow", "Flaming Arrow|Wooden Arrow"), ("Tendon Bow", "Flaming Arrow|Wooden Arrow"), ("Musket", "Silver Bullet|Musket Ball"), ("Flintlock Pistol", "Musket Ball") },
			['M'] = new[] { ("Diamond Staff", ""), ("Ruby Staff", ""), ("Wand of Sparking", ""), ("Vilethorn", ""), ("Space Gun", ""), ("Sapphire Staff", "") },
			['E'] = new[] { ("Enchanted Sword", ""), ("Starfury", ""), ("Blade of Grass", ""), ("Platinum Broadsword", ""), ("Gold Broadsword", ""), ("Katana", "") },
			['S'] = new[] { ("Slime Staff", ""), ("Finch Staff", ""), ("Flinx Staff", "") },
		};
		private static readonly Dictionary<char, (string w, string ammo)[]> BtPoolPost = new()
		{
			['R'] = new[] { ("Minishark", "Silver Bullet"), ("Phoenix Blaster", "Silver Bullet|Musket Ball"), ("Handgun", "Silver Bullet|Musket Ball"), ("The Undertaker", "Silver Bullet|Musket Ball"), ("Musket", "Silver Bullet"), ("Demon Bow", "Hellfire Arrow|Flaming Arrow") },
			['M'] = new[] { ("Aqua Scepter", ""), ("Magic Missile", ""), ("Flamelash", ""), ("Crimson Rod", ""), ("Diamond Staff", ""), ("Space Gun", "") },
			['E'] = new[] { ("Muramasa", ""), ("Blade of Grass", ""), ("Starfury", ""), ("Blood Butcherer", ""), ("Light's Bane", ""), ("Enchanted Sword", "") },
			['S'] = new[] { ("Imp Staff", ""), ("Slime Staff", ""), ("Flinx Staff", "") },
		};
		private static readonly Dictionary<char, string[][]> BtArmorPre = new()
		{
			['R'] = new[] { BtPlat, BtGold, BtFossil, BtShadow, BtNinja, BtIron }, ['M'] = new[] { BtJungle, BtMeteor, BtPlat, BtGold }, ['E'] = new[] { BtPlat, BtGold, BtShadow, BtCrimson, BtIron }, ['S'] = new[] { BtGold, BtPlat, BtCrimson, BtIron },
		};
		private static readonly Dictionary<char, string[][]> BtArmorPost = new()
		{
			['R'] = new[] { BtShadow, BtCrimson, BtMolten, BtFossil, BtPlat }, ['M'] = new[] { BtJungle, BtMeteor, BtShadow, BtCrimson }, ['E'] = new[] { BtMolten, BtShadow, BtCrimson, BtPlat }, ['S'] = new[] { BtCrimson, BtMolten, BtGold },
		};
		private static readonly string[] BtAccCommon = { "", "Hermes Boots", "Cloud in a Bottle", "Band of Regeneration", "Shackle", "Anklet of the Wind" };

		private static List<BtLevySpec> BtRoll(string boss, string tier, string compSpec, int cfg, out string comp)
		{
			bool post = BtPost(boss);
			var rng = new Random(cfg * 7919 + boss.GetHashCode() % 997 * 31 + (post ? 5 : 0) + 17);
			// class mix: 30% the 10-levy anchor, 15% each of the 4-levy mixes, else a random mix of 4..10
			string code = BtCompCode(compSpec);
			if (compSpec == "any")
			{
				double r = rng.NextDouble();
				if (r < 0.30) code = "R3M3E2S2";
				else if (r < 0.45) code = "R1M1E1S1";
				else if (r < 0.60) code = "R2M2";
				else
				{
					int n = rng.Next(4, 11); var cnt = new Dictionary<char, int> { ['R'] = 0, ['M'] = 0, ['E'] = 0, ['S'] = 0 };
					for (int i = 0; i < n; i++) { double q = rng.NextDouble(); cnt[q < 0.32 ? 'R' : q < 0.64 ? 'M' : q < 0.84 ? 'E' : 'S']++; }
					code = string.Concat(cnt.Where(c => c.Value > 0).Select(c => c.Key + "" + c.Value));
				}
			}
			comp = code;
			var list = new List<BtLevySpec>();
			var pool = post ? BtPoolPost : BtPoolPre; var arm = post ? BtArmorPost : BtArmorPre;
			foreach (var (cls, n) in BtCompParse(code))
				for (int k = 0; k < n; k++)
				{
					var (w, ammoOpts) = pool[cls][rng.Next(pool[cls].Length)];
					string ammo = ""; if (ammoOpts != "") { var ao = ammoOpts.Split('|'); ammo = ao[rng.Next(ao.Length)]; }
					var armor = arm[cls][rng.Next(arm[cls].Length)];
					bool maa = rng.NextDouble() < 0.35;
					int slots = maa ? 3 : 1;
					var accPool = BtAccCommon.ToList(); if (cls == 'E') { accPool.Add("Feral Claws"); accPool.Add("Feral Claws"); } if (cls == 'M') { accPool.Add("Band of Starpower"); accPool.Add("Band of Starpower"); }
					var acc = new List<string>();
					for (int a = 0; a < slots; a++) { string pick = accPool[rng.Next(accPool.Count)]; if (pick != "" && !acc.Contains(pick)) acc.Add(pick); }
					int lc;
					if (post) lc = 5;   // ~200 hp
					else { double q = rng.NextDouble(); lc = q < .20 ? 0 : q < .40 ? 1 : q < .65 ? 2 : q < .80 ? 3 : q < .90 ? 4 : 5; }   // 100..200 hp, mean ~140
					int mc;
					if (post) mc = cls == 'M' ? 9 : rng.Next(5, 10);
					else if (cls == 'M') { double q = rng.NextDouble(); mc = q < .15 ? 1 : q < .35 ? 2 : q < .55 ? 3 : q < .75 ? 4 : q < .90 ? 6 : 9; }   // 40..200 mana, skewed high
					else mc = rng.Next(0, 5);                                                                                                                  // 20..100
					list.Add(new BtLevySpec { Cls = cls, Weapon = w, Ammo = ammo, Armor = armor, Acc = acc.ToArray(), LifeCrystals = lc, ManaCrystals = mc, ManAtArms = maa, Potions = rng.Next(3, 9), Potion = post ? "Healing Potion" : "Lesser Healing Potion" });
				}
			return list;
		}

		// -------------------------------------------------------------------------------------------------- begin
		private bool BeginBossTrial()
		{
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepStandinDetour)); _spHooked = true; }
			catch (Exception e) { Mod.Logger.Warn("HARNESS-BT: could not hook UpdateConnectedClients: " + e.Message); }
			_btOut = Environment.GetEnvironmentVariable("LEVY_BT_OUT") ?? (OutPath + ".bt.tsv");
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_BT_STEPS"), out int st) && st >= 1 && st <= 400) StepsPerTurn = st;
			if (!File.Exists(_btOut)) File.WriteAllText(_btOut, BtHeader + "\n");
			string spec = Environment.GetEnvironmentVariable("LEVY_BT_SPEC");
			if (string.IsNullOrEmpty(spec) || !File.Exists(spec)) { Mod.Logger.Warn("HARNESS-BT: LEVY_BT_SPEC missing"); return false; }
			BtParseSpec(spec);
			if (!_btHooked)
			{
				_btHooked = true;
				On_NPC.StrikeNPC_HitInfo_bool_bool += BtStrikeDetour;
				On_NPC.UpdateNPC += BtNpcUpdateDetour;
				On_Projectile.Update += BtProjUpdateDetour;
				On_Player.ItemCheck_PlayInstruments += BtPlayInstr;
			}
			_btI = 0; _btStarted = false;
			return true;
		}

		internal static void BtUnhook()
		{
			if (!_btHooked) return;
			_btHooked = false;
			try { On_NPC.StrikeNPC_HitInfo_bool_bool -= BtStrikeDetour; On_NPC.UpdateNPC -= BtNpcUpdateDetour; On_Projectile.Update -= BtProjUpdateDetour; On_Player.ItemCheck_PlayInstruments -= BtPlayInstr; } catch { }
		}

		private static void BtPlayInstr(On_Player.orig_ItemCheck_PlayInstruments orig, Player self, Item sItem) { if (_btRefPlayer && Main.dedServ) return; orig(self, sItem); } // a headless server has no audio: the instrument pass would throw and abort the rest of the item check

		// -------------------------------------------------------------------------------------------------- attribution hooks
		private static int _btNpcCtx = -1, _btProjDepth;
		private static BtState _bt;
		internal static BtState BtLive => _bt;
		internal static bool BtIsBossOrAdd(int type) => _bt != null && (type == _bt.BossType || _bt.AddTypes.Contains(type));

		private static void BtNpcUpdateDetour(On_NPC.orig_UpdateNPC orig, NPC self, int i)
		{
			if (_bt == null || !(self.ModNPC is SoldierNPC)) { orig(self, i); return; }
			int prev = _btNpcCtx; _btNpcCtx = self.whoAmI;
			try { orig(self, i); } finally { _btNpcCtx = prev; }
		}

		private static void BtProjUpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (_bt == null) { orig(self, i); return; }
			_btProjDepth++;
			try { orig(self, i); } finally { _btProjDepth--; }
		}

		private static int BtStrikeDetour(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{
			int done = orig(self, hit, fromNet, noPlayerInteraction);
			var s = _bt;
			if (s != null && _btProjDepth == 0 && _btNpcCtx >= 0 && BtIsBossOrAdd(self.type)) s.Credit(_btNpcCtx, self.type, done, "melee");
			return done;
		}

		// -------------------------------------------------------------------------------------------------- fight state
		internal sealed class BtLevy
		{
			public char Cls; public SoldierRecord Rec; public int Idx; public int Id; public BtLevySpec Spec;
			public bool Dead; public int DeathTick = -1; public int LastLife, LastAttackerType = -1;
			public long DmgBoss, DmgAdd; public long TakenBoss, TakenAdd, TakenOther;
			public int MaxHp; public string Weapon; public int PotionsLeft, PotionsDrunk;
			public int ManaMin = 9999; public int LifeMin = 99999; public double DistSum; public int DistN, ShotsOrSwings;
		}

		internal sealed class BtState
		{
			public BtRun Run; public List<BtLevy> Levies = new();
			public int BossType; public HashSet<int> AddTypes = new();
			public int Tick; public int BossIdx = -1; public int BossLifeMax; public int BossLife;
			public int AddsPeak; public int HalfTick = -1; public bool BossGone; public int EndTick = -1; public string Result = "";
			public long DmgUnattributed, ProjDmg, MeleeDmg;
			public void Credit(int soldierIdx, int npcType, int dmg, string how)
			{
				if (dmg <= 0) return;
				var l = soldierIdx >= 0 ? Levies.FirstOrDefault(x => x.Idx == soldierIdx) : null;
				if (l == null) { DmgUnattributed += dmg; return; }
				if (npcType == BossType) l.DmgBoss += dmg; else l.DmgAdd += dmg;
				if (how == "melee") MeleeDmg += dmg; else ProjDmg += dmg;
			}
		}

		// -------------------------------------------------------------------------------------------------- arena
		private int BtF, _btCx; private string _btArenaBuilt = "";
		private void BtBuildArena(string kind)
		{
			if (kind == "nat")
			{
				// natural surface terrain: far from the harness box, whatever the world has there
				_btCx = Math.Min(Main.maxTilesX - 200, _cx + 700);
				int y = 120; while (y < Main.maxTilesY - 100 && !(Main.tile[_btCx, y].HasTile && Main.tile[_btCx, y].BlockType == Terraria.ID.BlockType.Solid && !Main.tileCut[Main.tile[_btCx, y].TileType])) y++;
				BtF = y; _btArenaBuilt = kind;
				Mod.Logger.Info($"HARNESS-BT: natural terrain at column {_btCx}, surface row {BtF}");
				return;
			}
			float n = Main.maxTilesX / 4200f; n *= n;
			int band = (int)(60f + 10f * n + Main.worldSurface / 6.0) + 2;
			BtF = Math.Clamp(band + 75, 100, Main.maxTilesY - 120);
			_btCx = _cx;
			int cx = _cx, fy = BtF, half = kind == "big" || kind == "flat" ? 110 : 80;
			FaFill(cx - 130, fy - 90, cx + 130, fy - 1, false);
			FaFill(cx - 130, fy, cx + 130, fy + 25, true);
			FaFill(cx - half - 1, fy - 90, cx - half - 1, fy + 25, true);
			FaFill(cx + half + 1, fy - 90, cx + half + 1, fy + 25, true);
			int rows = kind == "big" ? 4 : kind == "small" ? 3 : 0;
			for (int k = 1; k <= rows; k++)
			{
				int py = fy - 7 * k;
				for (int x = cx - half + 6; x <= cx + half - 6; x++)
				{
					Tile t = Main.tile[x, py];
					t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.WallType = 0;
				}
			}
			_btArenaBuilt = kind;
			Mod.Logger.Info($"HARNESS-BT: arena '{kind}' built: floor row {fy}, half-width {half}, platform rows {rows}");
		}

		private Vector2 _btCmdPos;
		private void BtPinCommander(bool spectator)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.team = 0;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			p.position = new Vector2(_btCmdPos.X - p.width / 2f, _btCmdPos.Y - p.height);
			p.velocity = Vector2.Zero; p.immune = true; p.immuneTime = 30;
			p.ZoneCrimson = true; p.ZoneOverworldHeight = true; // the Brain of Cthulhu sinks away from a player who is not in the Crimson
			if (spectator)
			{
				int bt = ModContent.BuffType<SpectatorBuff>();
				if (!p.HasBuff(bt)) { p.AddBuff(bt, 600000); if (p.TryGetModPlayer(out SpectatorPlayer sp)) sp.Anchor = p.Center; }
			}
		}

		// -------------------------------------------------------------------------------------------------- step
		private BtState _btS;
		private int _btTickInRun;
		private void StepBossTrial()
		{
			if (_btI < 0) return;
			if (_btI >= _btRuns.Count) { try { File.WriteAllText(_btOut + ".done", "1"); } catch { } Finish("bosstrial complete"); return; }
			BtRun run = _btRuns[_btI];
			Main.dayTime = false; Main.time = 16200; Main.raining = false; Main.bloodMoon = false; Main.eclipse = false;
			try
			{
				if (_btArenaBuilt != run.Arena && !_btStarted && !_btRefStarted) BtBuildArena(run.Arena);
				if (run.Mode == "items") { BtItems(); _btI++; return; }
				if (run.Mode == "ref") { StepBtRef(run); return; }
				StepBtFight(run);
			}
			catch (Exception e)
			{
				Mod.Logger.Warn("HARNESS-BT: run threw: " + e);
				BtAppendRow(run, "ERROR", 0, e.GetType().Name + ":" + e.Message);
				_bt = null; _btRefStarted = false; _btStarted = false; _btRefPlayer = false; _btI++;
				try { ClearAll(); BtPurgeNpcs(); } catch { }
			}
		}

		private void BtPinStrays(int keep)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || n.friendly) continue;
				if (i == keep || (_bt != null && _bt.AddTypes.Contains(n.type)) || (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget)) continue;
				n.active = false;
			}
		}

		private BtLevy BtSpawnLevy(BtLevySpec spec, BtRun run, int slot, int count, Vector2 center)
		{
			var rec = MakeRecord($"{spec.Cls}{slot}");
			rec.Grade = spec.ManAtArms ? Grade.ManAtArms : Grade.Levy;
			rec.LifeCrystals = spec.LifeCrystals; rec.ManaCrystals = spec.ManaCrystals;
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.LoadoutLocked = true;
			rec.Order = run.Order == "sd" ? SoldierOrder.SearchDestroy : run.Order == "follow" ? SoldierOrder.Follow : SoldierOrder.FreeRein;
			var gear = rec.Gear;
			Item w = BtMake(spec.Weapon);
			if (!gear.CanHold(SoldierGear.SlotWeapon, w)) throw new Exception("levy cannot hold " + spec.Weapon);
			gear.Weapon = w; gear.SetLocked(SoldierGear.SlotWeapon, true);
			if (!string.IsNullOrEmpty(spec.Ammo))
			{
				Item am = BtMake(spec.Ammo, LevyConstants.AmmoPouchMax);
				if (!gear.CanHold(SoldierGear.SlotAmmo, am)) throw new Exception("levy cannot hold ammo " + spec.Ammo);
				gear.Ammo = am; gear.SetLocked(SoldierGear.SlotAmmo, true);
			}
			for (int a = 0; a < spec.Armor.Length; a++)
			{
				Item it = BtMake(spec.Armor[a]);
				int sl = a == 0 ? SoldierGear.SlotHead : a == 1 ? SoldierGear.SlotBody : SoldierGear.SlotLegs;
				if (!gear.CanHold(sl, it)) throw new Exception("levy cannot wear " + spec.Armor[a]);
				gear.Set(sl, it); gear.SetLocked(sl, true);
			}
			int accSlots = rec.AccessorySlots, placed = 0;
			foreach (string an in spec.Acc)
			{
				if (placed >= accSlots) break;
				Item it = BtMake(an);
				int sl = SoldierGear.SlotAcc0 + placed;
				if (!gear.CanHold(sl, it)) throw new Exception("levy cannot wear accessory " + an);
				gear.Set(sl, it); gear.SetLocked(sl, true); placed++;
			}
			RosterSystem.Instance.Regulars.Add(rec);
			float off = (slot - (count - 1) / 2f) * 3f * 16f;
			Vector2 at = new Vector2(center.X + off, center.Y);
			int idx = RosterSystem.Instance.SpawnBody(rec, at);
			if (idx < 0 || !rec.HasBody) throw new Exception("SpawnBody failed");
			NPC n = Main.npc[idx]; n.Bottom = at; n.velocity = Vector2.Zero;
			var sn = (SoldierNPC)n.ModNPC;
			rec.CommanderIndex = 0;
			sn.RefreshStats();
			if (run.Camp) sn.Stations = Systems.StationFlags.Campfire | Systems.StationFlags.HeartLantern;
			n.life = n.lifeMax = rec.MaxHp; rec.CurrentHp = rec.MaxHp;
			return new BtLevy { Cls = spec.Cls, Rec = rec, Idx = idx, Id = rec.Id, Spec = spec, LastLife = n.life, MaxHp = rec.MaxHp, Weapon = spec.Weapon, PotionsLeft = spec.Potions };
		}

		private BtLevySpec BtSpecFromLoad(char cls, BtRun run)
		{
			var ld = BtLoadFor(cls, run.Tier); bool best = run.Stats == "best";
			return new BtLevySpec { Cls = cls, Weapon = ld.Weapon, Ammo = ld.Ammo, Armor = ld.Armor, Acc = ld.Acc, LifeCrystals = best ? 15 : 2, ManaCrystals = best ? 9 : (cls == 'M' ? 4 : 0), ManAtArms = run.Grade == "champ" };
		}

		private static void BtAutonomy(BtState s)
		{
			int buff = ModContent.BuffType<AutonomousBuff>();
			foreach (var l in s.Levies) if (!l.Dead && l.Rec.HasBody) Main.npc[l.Idx].AddBuff(buff, PotionConstants.AutonomousDurationTicks);
		}

		private static int BtBossNpcType(string boss) => boss switch { "kslime" => NPCID.KingSlime, "brain" => NPCID.BrainofCthulhu, "eow" => NPCID.EaterofWorldsHead, _ => NPCID.EyeofCthulhu };

		private void StepBtFight(BtRun run)
		{
			if (!_btStarted)
			{
				BtApplyExtra(run.Extra); WeaponRank.PureContactPenalty = run.Beam;
				if (run.Specs == null) { run.Specs = BtFixedSpecs(run); run.Desc = string.Join(" ; ", run.Specs.Select(sp => sp.Describe())); }
				ClearAll(); BtPurgeNpcs();
				Main.GameMode = run.Diff == "expert" ? GameModeID.Expert : GameModeID.Normal;
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
				NPC.downedBoss1 = false; NPC.downedBoss2 = false; NPC.downedSlimeKing = false;
				_btCmdPos = new Vector2((_btCx - 6) * 16f, BtF * 16f);
				BtPinCommander(run.Spec);
				var s = new BtState { Run = run, BossType = BtBossNpcType(run.Boss) };
				if (run.Boss == "kslime") { s.AddTypes.Add(NPCID.BlueSlime); s.AddTypes.Add(NPCID.SlimeSpiked); s.AddTypes.Add(NPCID.GreenSlime); s.AddTypes.Add(NPCID.PurpleSlime); s.AddTypes.Add(NPCID.RedSlime); s.AddTypes.Add(NPCID.YellowSlime); s.AddTypes.Add(NPCID.IceSlime); s.AddTypes.Add(NPCID.JungleSlime); s.AddTypes.Add(NPCID.BlackSlime); }
				else if (run.Boss == "brain") s.AddTypes.Add(NPCID.Creeper);
				else s.AddTypes.Add(NPCID.ServantofCthulhu);
				int total = run.Specs.Count, slot = 0;
				foreach (var sp in run.Specs) s.Levies.Add(BtSpawnLevy(sp, run, slot++, total, _btCmdPos));
				int bx = (int)((_btCx + 30) * 16f), by = run.Boss == "kslime" ? (int)((BtF - 6) * 16f) : (int)((BtF - 38) * 16f);
				int ei = NPC.NewNPC(new EntitySource_WorldEvent("LevyBossTrial"), bx, by, s.BossType);
				if (ei < 0 || ei >= Main.maxNPCs) throw new Exception("boss spawn failed");
				NPC boss = Main.npc[ei]; s.BossIdx = ei; s.BossLifeMax = boss.lifeMax; s.BossLife = boss.life;
				boss.timeLeft = 99999;
				_btS = s; _bt = s; _btStarted = true; _btTickInRun = 0;
				if (run.Auto) BtAutonomy(s);
				Mod.Logger.Info($"HARNESS-BT: fight start {run.Key}: {run.Boss} {boss.life}/{boss.lifeMax} def {boss.defense} dmg {boss.damage}; {run.Desc}");
				return;
			}
			var st = _btS; st.Tick = ++_btTickInRun;
			BtPinCommander(run.Spec);
			if (run.Auto && st.Tick % PotionConstants.AutonomousPulseTicks == 0) BtAutonomy(st);
			BtPinStrays(st.BossIdx);
			NPC e = Main.npc[st.BossIdx];
			bool bossAlive = e.active && e.type == st.BossType;
			if (bossAlive) { st.BossLife = e.life; st.BossLifeMax = Math.Max(st.BossLifeMax, e.lifeMax); e.timeLeft = 99999; if (st.HalfTick < 0 && e.life * 2 <= e.lifeMax) st.HalfTick = st.Tick; }
			else if (!st.BossGone) { st.BossGone = true; st.EndTick = st.Tick; st.Result = e.life <= 0 ? "WIN" : "DESPAWN"; st.BossLife = Math.Max(0, e.life); }
			if (run.Boss == "brain" && (st.Tick % 60 == 0 || !bossAlive && st.Tick == st.EndTick)) Mod.Logger.Info($"HARNESS-BT-DBG t{st.Tick} brain active={e.active} type={e.type} life={e.life}/{e.lifeMax} pos={e.Center / 16f} ai={e.ai[0]}/{e.ai[1]}/{e.ai[2]}/{e.ai[3]} target={e.target} dontTake={e.dontTakeDamage} timeLeft={e.timeLeft} creepers={Main.npc.Count(q => q.active && q.type == NPCID.Creeper)}");
			int adds = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && st.AddTypes.Contains(Main.npc[i].type)) adds++;
			st.AddsPeak = Math.Max(st.AddsPeak, adds);
			int alive = 0;
			foreach (var l in st.Levies)
			{
				if (l.Dead) continue;
				NPC n = Main.npc[l.Idx];
				bool body = l.Rec.HasBody && n.active && n.ModNPC is SoldierNPC sn0 && ReferenceEquals(sn0.Record, l.Rec) && n.life > 0;
				if (body)
				{
					var sn = (SoldierNPC)n.ModNPC;
					if (run.Camp) sn.Stations = Systems.StationFlags.Campfire | Systems.StationFlags.HeartLantern;
					if (run.Honey && !n.HasBuff(BuffID.Honey)) n.AddBuff(BuffID.Honey, 600000);
					// potions: the armory issue path is emulated with its own rule (drink below the threshold, then potion sickness)
					if (run.Pots && l.PotionsLeft > 0 && sn.PotionSickness == 0 && n.life * 100 < n.lifeMax * LevyConstants.PotionDrinkBelowPercent)
					{
						Item pot = BtMake(l.Spec.Potion);
						n.life = Math.Min(n.lifeMax, n.life + pot.healLife); l.Rec.CurrentHp = n.life; sn.PotionSickness = LevyConstants.PotionSicknessTicks; l.PotionsLeft--; l.PotionsDrunk++;
						l.LastLife = n.life;
					}
					int drop = l.LastLife - n.life;
					if (drop > 0)
					{
						int at = sn.LastAttackerType;
						if (at == st.BossType) l.TakenBoss += drop; else if (st.AddTypes.Contains(at)) l.TakenAdd += drop; else l.TakenOther += drop;
					}
					l.LastLife = n.life; l.LastAttackerType = sn.LastAttackerType; l.LifeMin = Math.Min(l.LifeMin, n.life);
					l.ManaMin = Math.Min(l.ManaMin, l.Rec.Mana);
					if (bossAlive) { l.DistSum += Vector2.Distance(n.Center, e.Center) / 16f; l.DistN++; }
					alive++;
				}
				else
				{
					l.Dead = true; l.DeathTick = st.Tick;
					int at = l.LastAttackerType;
					if (at == st.BossType) l.TakenBoss += l.LastLife; else if (st.AddTypes.Contains(at)) l.TakenAdd += l.LastLife; else l.TakenOther += l.LastLife;
				}
			}
			if (st.Result == "" && alive == 0) { st.Result = "LOSS"; st.EndTick = st.Tick; }
			if (st.Result == "" && st.Tick >= run.Cap) { st.Result = "TIMEOUT"; st.EndTick = st.Tick; }
			if (st.Result != "" && (st.Result != "WIN" || st.Tick >= st.EndTick + 20)) BtFinishFight(run, st);
		}

		private void BtPurgeNpcs()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC) n.active = false; }
		}

		private const string BtHeader = "key\tmode\tboss\tdiff\torder\tauto\tarena\tcamp\tcomp\tcfg\ttrial\ttier\tresult\tticks\tsec\tbossMax\tbossLeft\tdmgBoss\tdmgR\tdmgM\tdmgE\tdmgS\tdmgUnattr\tdmgProj\tdmgMelee\tdeaths\tdeathsR\tdeathsM\tdeathsE\tdeathsS\tavgSurvSec\ttakenBoss\ttakenAdd\ttakenOther\taddsPeak\thalfSec\tnLevies\tavgHp\tavgMana\tpotionsDrunk\tperLevy\tnote";

		private void BtFinishFight(BtRun run, BtState st)
		{
			int end = st.EndTick > 0 ? st.EndTick : st.Tick;
			Func<char, long> dmg = c => st.Levies.Where(l => l.Cls == c).Sum(l => l.DmgBoss);
			long dmgBoss = st.Levies.Sum(l => l.DmgBoss);
			int deaths = st.Levies.Count(l => l.Dead);
			Func<char, int> dc = c => st.Levies.Count(l => l.Dead && l.Cls == c);
			double avgSurv = st.Levies.Average(l => (l.Dead ? l.DeathTick : end) / 60.0);
			long tB = st.Levies.Sum(l => l.TakenBoss), tA = st.Levies.Sum(l => l.TakenAdd), tO = st.Levies.Sum(l => l.TakenOther);
			Func<double, string> f1 = v => v.ToString("0.0", CultureInfo.InvariantCulture);
			string per = string.Join(";", st.Levies.Select(l => $"{l.Cls}:{l.Weapon.Replace(' ', '_')}:dmg{l.DmgBoss}:surv{f1((l.Dead ? l.DeathTick : end) / 60.0)}:{(l.Dead ? "dead" : "alive")}:hp{l.MaxHp}:tb{l.TakenBoss}:ta{l.TakenAdd}:to{l.TakenOther}:pot{l.PotionsDrunk}:dist{f1(l.DistN > 0 ? l.DistSum / l.DistN : 0)}"));
			int bossLeft = st.BossGone ? st.BossLife : Main.npc[st.BossIdx].active ? Main.npc[st.BossIdx].life : st.BossLife;
			var sb = new StringBuilder();
			sb.Append(run.Key).Append('\t').Append(string.Join("\t", new object[] { run.Mode, run.Boss, run.Diff, run.Order, run.Auto ? 1 : 0, run.Arena, run.Camp ? 1 : 0, run.Comp, run.CfgId, run.Trial, run.Tier, st.Result, end, f1(end / 60.0),
				st.BossLifeMax, bossLeft, dmgBoss, dmg('R'), dmg('M'), dmg('E'), dmg('S'), st.DmgUnattributed, st.ProjDmg, st.MeleeDmg, deaths, dc('R'), dc('M'), dc('E'), dc('S'), f1(avgSurv), tB, tA, tO, st.AddsPeak,
				st.HalfTick < 0 ? "-" : f1(st.HalfTick / 60.0), st.Levies.Count, f1(st.Levies.Average(l => l.MaxHp)), f1(st.Levies.Average(l => l.Spec.Mana)), st.Levies.Sum(l => l.PotionsDrunk), per, "-" }));
			File.AppendAllText(_btOut, sb + "\n");
			Mod.Logger.Info($"HARNESS-BT: RESULT {run.Key} -> {st.Result} t={end / 60.0:0.0}s bossLeft={bossLeft}/{st.BossLifeMax} dmg={dmgBoss} (R{dmg('R')} M{dmg('M')} E{dmg('E')} S{dmg('S')}, unattr {st.DmgUnattributed}) deaths={deaths}/{st.Levies.Count} takenBoss={tB} takenAdd={tA} takenOther={tO}");
			if (Main.npc[st.BossIdx].active && Main.npc[st.BossIdx].type == st.BossType) Main.npc[st.BossIdx].active = false;
			_bt = null; _btS = null; _btStarted = false; _btI++;
			NPC.downedBoss1 = false; NPC.downedBoss2 = false; NPC.downedSlimeKing = false;
			ClearAll(); BtPurgeNpcs();
		}

		private void BtAppendRow(BtRun run, string result, int ticks, string note)
		{
			var cols = BtHeader.Split('\t').Length;
			var v = new string[cols]; for (int i = 0; i < cols; i++) v[i] = "0";
			v[0] = run.Key; v[1] = run.Mode; v[2] = run.Boss; v[3] = run.Diff; v[4] = run.Order; v[5] = run.Auto ? "1" : "0"; v[6] = run.Arena; v[7] = run.Camp ? "1" : "0"; v[8] = run.Comp; v[9] = run.CfgId; v[10] = run.Trial.ToString(); v[11] = run.Tier; v[12] = result; v[13] = ticks.ToString(); v[cols - 2] = "-"; v[cols - 1] = note;
			File.AppendAllText(_btOut, string.Join("\t", v) + "\n");
		}


		// ================================================================================================== REF (levy vs player DPS)
		private bool _btRefStarted; private int _btRefStage, _btRefTick;
		private int _btRefDummy = -1; private BtLevy _btRefLevy; private long _btRefLevyDmg, _btRefPlayerDmg; private int _btRefLife0, _btRefStartLife;
		private Item _btRefWeapon; private string _btRefPlayerNote = "";
		private const int BtRefWarm = 240, BtRefMeasure = 1800;     // 4 s to settle (summons, first mana), then 30 s measured

		private int BtSpawnDummy(float cxPx, float cyPx)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyBossTrial"), (int)cxPx, (int)cyPx, NPCID.EyeofCthulhu);
			if (i < 0 || i >= Main.maxNPCs) throw new Exception("dummy spawn failed");
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true;
			int def = n.defense;
			n.lifeMax = 1_000_000; n.life = 1_000_000; n.damage = 0; n.knockBackResist = 0f; n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.dontTakeDamage = false; n.value = 0f; n.boss = false;
			n.defense = def;
			n.velocity = Vector2.Zero; n.Center = new Vector2(cxPx, cyPx); ht.Pin = n.position; n.netUpdate = true;
			return i;
		}

		private void StepBtRef(BtRun run)
		{
			char cls = run.Comp[0];
			if (!_btRefStarted)
			{
				BtApplyExtra(run.Extra); WeaponRank.PureContactPenalty = run.Beam;
				ClearAll(); BtPurgeNpcs(); _btRefPlayer = false;
				Main.GameMode = run.Diff == "expert" ? GameModeID.Expert : GameModeID.Normal;
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 17);
				_btCmdPos = new Vector2((_cx - 14) * 16f, BtF * 16f);
				Vector2 dummyAt = new Vector2((_cx) * 16f, BtF * 16f - 60f);
				_btRefDummy = BtSpawnDummy(dummyAt.X, dummyAt.Y);
				_btRefLife0 = Main.npc[_btRefDummy].life;
				_btRefTick = 0; _btRefStarted = true; _btRefPlayerNote = ""; _btRefStage = run.Extra.Contains("only=player") ? 1 : 0;
				if (_btRefStage == 0)
				{
					BtRefPin(false);
					_btRefLevy = BtSpawnLevy(BtSpecFromLoad(cls, run), run, 0, 1, new Vector2((_cx - 14) * 16f + 3 * 16f, BtF * 16f));
					Main.npc[_btRefLevy.Idx].velocity = Vector2.Zero;
					if (run.Order == "follow") _btRefLevy.Rec.Order = SoldierOrder.Follow;
					if (run.Auto) BtAutonomyOne(_btRefLevy);
				}
				else BtRefSetupPlayer(run, cls);
				return;
			}
			_btRefTick++;
			NPC d = Main.npc[_btRefDummy];
			if (_btRefStage == 0)
			{
				BtRefPin(false);
				if (run.Auto && _btRefTick % 30 == 0) BtAutonomyOne(_btRefLevy);
				BtPinStrays(_btRefDummy);
				if (_btRefTick == BtRefWarm) _btRefStartLife = d.life;
				if (_btRefTick >= BtRefWarm + BtRefMeasure || !_btRefLevy.Rec.HasBody)
				{
					_btRefLevyDmg = _btRefTick >= BtRefWarm ? _btRefStartLife - d.life : 0;
					BtRefNext(run, cls, d);
				}
			}
			else
			{
				BtRefPin(true);
				BtPinStrays(_btRefDummy);
				BtRefPlayerTick(run, cls);
				if (_btRefTick == BtRefWarm) _btRefStartLife = d.life;
				if (_btRefTick >= BtRefWarm + BtRefMeasure)
				{
					_btRefPlayerDmg = _btRefStartLife - d.life;
					BtRefDone(run, cls);
				}
			}
		}

		private static void BtAutonomyOne(BtLevy l) { if (l.Rec.HasBody) Main.npc[l.Idx].AddBuff(ModContent.BuffType<AutonomousBuff>(), PotionConstants.AutonomousDurationTicks); }

		private void BtRefPin(bool playerUses)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.team = 0;
			if (!playerUses)
			{
				p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
				p.position = new Vector2(_btCmdPos.X - p.width / 2f, _btCmdPos.Y - p.height); p.velocity = Vector2.Zero;
			}
		}

		private void BtRefNext(BtRun run, char cls, NPC d)
		{
			if (run.Extra.Contains("only=levy")) { BtRefDone(run, cls); return; }
			// the levy window is over: remove it, set up the player window against a fresh dummy
			int lv = _btRefLevy.Rec.HasBody ? 1 : 0;
			ClearAll(); BtPurgeNpcs();
			_btRefDummy = BtSpawnDummy(_cx * 16f, BtF * 16f - 60f);
			_btRefLife0 = Main.npc[_btRefDummy].life;
			_btRefStage = 1; _btRefTick = 0;
			BtRefSetupPlayer(run, cls);
		}

		private void BtRefSetupPlayer(BtRun run, char cls)
		{
			var load = BtLoadFor(cls, run.Tier); _btRefStandWeapon = load.Weapon;
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.team = 0;
			for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			bool best = run.Stats == "best";
			p.statLifeMax = p.statLifeMax2 = best ? 400 : 140; p.statLife = p.statLifeMax2;
			p.statManaMax = p.statManaMax2 = best ? 200 : 20; p.statMana = p.statManaMax2;
			_btRefWeapon = BtMake(load.Weapon);
			p.inventory[0] = _btRefWeapon.Clone(); p.selectedItem = 0;
			if (!string.IsNullOrEmpty(load.Ammo)) p.inventory[54] = BtMake(load.Ammo, LevyConstants.AmmoPouchMax);
			for (int a = 0; a < load.Armor.Length && a < 3; a++) p.armor[a] = BtMake(load.Armor[a]);
			int accSlots = new SoldierRecord { Grade = run.Grade == "champ" ? Grade.Champion : Grade.Levy }.AccessorySlots;
			for (int a = 0; a < load.Acc.Length && a < accSlots && 3 + a < 8; a++) p.armor[3 + a] = BtMake(load.Acc[a]);
			for (int b = 0; b < p.buffType.Length; b++) { p.buffType[b] = 0; p.buffTime[b] = 0; }
			p.noItems = false; p.itemAnimation = 0; p.itemTime = 0; p.controlUseItem = false; p.channel = false;
			p.position = new Vector2((_cx - BtRefStand(cls)) * 16f - p.width / 2f, BtF * 16f - p.height); p.velocity = Vector2.Zero;
			_btRefPlayer = true; SetKeepAlive(true);
			_btRefPlayerNote = $"player {p.statLifeMax2}hp/{p.statManaMax2}mana";
		}

		private static System.Reflection.MethodInfo _btItemCheck; private static bool _btItemCheckInt;
		/// <summary>Tiles west of the dummy the player stands at: a contact blade has to be in reach, everything else shoots from range.</summary>
		private static int BtRefStand(char cls) => cls == 'E' && !WeaponClassifier.GetProfile(BtMake(_btRefStandWeapon ?? "Copper Shortsword")).FiresProjectileMelee ? 2 : 14;
		private static string _btRefStandWeapon;
		private void BtRefPlayerTick(BtRun run, char cls)
		{
			Player p = Main.player[0];
			bool best = run.Stats == "best";
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = best ? 400 : 140; p.statLife = p.statLifeMax2;
			p.statManaMax = p.statManaMax2 = best ? 200 : 20; p.statMana = p.statManaMax2;  // a player drinks mana potions: mana is never the limit for the player reference
			p.position = new Vector2((_cx - BtRefStand(cls)) * 16f - p.width / 2f, BtF * 16f - p.height); p.velocity.X = 0f;
			if (p.velocity.Y < 0) p.velocity.Y = 0f;
			int savedMy = Main.myPlayer;
			try
			{
				Main.myPlayer = 0;
				// the equip pass a connected player's own Player.Update runs (reset, buffs, equips, set bonuses, mod post-equips): damage, speed and crit multipliers come from here
				p.ResetEffects(); p.UpdateBuffs(0); p.UpdateEquips(0); p.UpdateArmorSets(0); PlayerLoader.PostUpdateEquips(p);
				if (p.inventory[0] == null || p.inventory[0].IsAir || p.inventory[0].type != _btRefWeapon.type) p.inventory[0] = _btRefWeapon.Clone();
				p.selectedItem = 0; p.noItems = false;
				NPC d = Main.npc[_btRefDummy];
				Vector2 aim = d.Center;
				Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
				Item w = p.inventory[0];
				bool hold = w.autoReuse || w.channel;
				bool press = hold || (_btRefTick % 2 == 0);
				p.controlUseItem = press; p.controlUseTile = false; p.channel = press && w.channel;
				try { Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = press; } catch { }
				Main.mouseLeft = press; Main.mouseLeftRelease = !press;
				p.direction = aim.X >= p.Center.X ? 1 : -1;
				if (!p.controlUseItem) p.releaseUseItem = true;
				if (_btItemCheck == null)
				{
					foreach (var m in typeof(Player).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
						if (m.Name == "ItemCheck") { var ps = m.GetParameters(); if (ps.Length == 0) { _btItemCheck = m; _btItemCheckInt = false; break; } if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { _btItemCheck = m; _btItemCheckInt = true; } }
					if (_btItemCheck == null) throw new Exception("Player.ItemCheck not found");
				}
				_btItemCheck.Invoke(p, _btItemCheckInt ? new object[] { 0 } : null);
			}
			catch (Exception e) { var ie = (e is System.Reflection.TargetInvocationException && e.InnerException != null) ? e.InnerException : e; if (!_btRefPlayerNote.Contains("itemcheck")) { _btRefPlayerNote += " itemcheck:" + ie.GetType().Name + ":" + ie.Message; Mod.Logger.Info("HARNESS-BT: itemcheck exception: " + ie); } }
			finally { Main.myPlayer = savedMy; }
		}

		private void BtRefDone(BtRun run, char cls)
		{
			double secs = BtRefMeasure / 60.0;
			double levyDps = run.Extra.Contains("only=player") ? 0 : _btRefLevyDmg / secs, playerDps = run.Extra.Contains("only=levy") ? 0 : _btRefPlayerDmg / secs;
			string load = BtLoadFor(cls, run.Tier).Weapon;
			var sb = new StringBuilder();
			string res = $"levy={levyDps.ToString("0.0", CultureInfo.InvariantCulture)} player={playerDps.ToString("0.0", CultureInfo.InvariantCulture)} ratio={(levyDps > 0 ? (playerDps / levyDps).ToString("0.00", CultureInfo.InvariantCulture) : "-")}";
			// reuse the fight columns: dmgEye = levy window damage, dmgEyeR = player window damage, sec = window seconds
			var cols = BtHeader.Split('\t').Length; var v = new string[cols]; for (int i = 0; i < cols; i++) v[i] = "0";
			v[0] = run.Key; v[1] = run.Mode; v[2] = run.Boss; v[3] = run.Diff; v[4] = run.Order; v[5] = run.Auto ? "1" : "0"; v[6] = run.Arena; v[7] = run.Camp ? "1" : "0"; v[8] = run.Comp; v[9] = run.CfgId; v[10] = run.Trial.ToString(); v[11] = run.Tier; v[12] = "REF"; v[13] = BtRefMeasure.ToString(); v[14] = secs.ToString("0.0", CultureInfo.InvariantCulture);
			v[17] = _btRefLevyDmg.ToString(); v[18] = _btRefPlayerDmg.ToString(); v[cols - 2] = load.Replace(' ', '_'); v[cols - 1] = res + " " + _btRefPlayerNote;
			sb.Append(string.Join("\t", v));
			File.AppendAllText(_btOut, sb + "\n");
			Mod.Logger.Info($"HARNESS-BT: REF {run.Key} weapon={load}: {res} {_btRefPlayerNote}");
			_btRefPlayer = false; _btRefStarted = false; _btI++;
			ClearAll(); BtPurgeNpcs();
		}

		// ================================================================================================== ITEMS (evidence for the loadout lists)
		private void BtItems()
		{
			var sb = new StringBuilder();
			void L(string s) { Mod.Logger.Info("HARNESS-BT-ITEM: " + s); sb.AppendLine(s); }
			string[] weapons = (Environment.GetEnvironmentVariable("LEVY_BT_WEAPONS") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
			foreach (string name in weapons)
			{
				try
				{
					Item it = BtMake(name);
					var prof = WeaponClassifier.GetProfile(it);
					var gear = new SoldierGear();
					L($"WEAPON {name}: id={it.type} dmg={it.damage} useTime={it.useTime} anim={it.useAnimation} mana={it.mana} crit={it.crit} kb={it.knockBack} rare={it.rare} value={it.value} class={it.DamageType?.Name} shoot={it.shoot} shootSpeed={it.shootSpeed} autoReuse={it.autoReuse} family={prof.Family} range={prof.RangeTiles} beam={prof.FiresProjectileMelee} canHold={gear.CanHold(SoldierGear.SlotWeapon, it)} rankBalanced={WeaponRank.Of(it):0.0} score={WeaponClassifier.Score(it):0.0}");
				}
				catch (Exception e) { L($"WEAPON {name}: ERROR {e.Message}"); }
			}
			string[] sets = (Environment.GetEnvironmentVariable("LEVY_BT_SETS") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
			foreach (string setName in sets)
			{
				try
				{
					string[] parts = setName.Split('+');
					var p = new Player(); p.whoAmI = SoldierNPC.DummySlot; p.active = true; p.width = 20; p.height = 42; p.direction = 1;
					int def = 0;
					for (int a = 0; a < parts.Length; a++)
					{
						Item it = BtMake(parts[a]);
						if (a < 3) p.armor[a] = it; else p.armor[3 + (a - 3)] = it;
						def += it.defense;
					}
					p.ResetEffects(); p.UpdateEquips(SoldierNPC.DummySlot); p.UpdateArmorSets(SoldierNPC.DummySlot); PlayerLoader.PostUpdateEquips(p);
					L($"ARMOR {setName}: itemDefense={def} setBonus='{p.setBonus}' statDefense={p.statDefense} melee={p.GetTotalDamage(DamageClass.Melee).Additive:0.00}x{p.GetTotalDamage(DamageClass.Melee).Multiplicative:0.00} ranged={p.GetTotalDamage(DamageClass.Ranged).Additive:0.00}x{p.GetTotalDamage(DamageClass.Ranged).Multiplicative:0.00} magic={p.GetTotalDamage(DamageClass.Magic).Additive:0.00}x{p.GetTotalDamage(DamageClass.Magic).Multiplicative:0.00} summon={p.GetTotalDamage(DamageClass.Summon).Additive:0.00} meleeCrit={p.GetTotalCritChance(DamageClass.Melee):0.0} rangedCrit={p.GetTotalCritChance(DamageClass.Ranged):0.0} magicCrit={p.GetTotalCritChance(DamageClass.Magic):0.0} meleeSpeed={p.GetAttackSpeed(DamageClass.Melee):0.00} manaCost={p.manaCost:0.00} maxMinions={p.maxMinions} statManaMax2={p.statManaMax2} moveSpeed={p.moveSpeed:0.00} maxRunSpeed={p.maxRunSpeed:0.00} ammoCost={p.ammoCost80}/{p.ammoCost75}");
				}
				catch (Exception e) { L($"ARMOR {setName}: ERROR {e.Message}"); }
			}
			try { File.AppendAllText(_btOut + ".items.txt", sb.ToString()); } catch { }
		}
	}

	/// <summary>Credits a soldier's projectile hits on the Eye / its Servants (melee swings are credited by the strike detour).</summary>
public class BtProjWatch : GlobalProjectile
	{
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			var s = CombatHarness.BtLive;
			if (s == null || !CombatHarness.BtIsBossOrAdd(target.type)) return;
			int idx = -1;
			if (projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0) idx = tag.SoldierIndex;
			else if (projectile.type == ModContent.ProjectileType<SoldierMinion>()) idx = (int)projectile.ai[0];
			s.Credit(idx, target.type, damageDone, "proj");
		}
	}

}
