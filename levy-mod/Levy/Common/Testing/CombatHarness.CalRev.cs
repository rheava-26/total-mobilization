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
using Levy.Common.Compat.Calamity;
using Levy.Common.Globals;
using Levy.Common.Potions;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.Buffs;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// CALAMITY / REVENGEANCE TRIALS (LEVY_HARNESS_SCENARIO=calrev). Measurement only. Early bosses (King Slime, Eye of Cthulhu, Brain of Cthulhu), vanilla on-tier items, a player-built arena, ten levies (R3 M3 E2 S2)
	/// with rolled life/mana/weapons/armor/accessories. The commander drinks a Spectator potion (the real SpectatorBuff), so the boss targets the levies. The SAME seed gives the SAME loadout in every mode.
	/// Spec (LEVY_CR_SPEC, TSV, one run per line; result rows are appended to LEVY_CR_OUT and finished runs are skipped on restart):
	///   fight  boss(kslime|eye|brain)  mode(exp|rev|revoff)  seed  capTicks  [order=free|sd]
	///   probe  mode  seconds   (Eye with 40x life, two vanilla-gear and two Calamity-gear levies, rage / adrenaline sampled twice a second to LEVY_CR_OUT.probe.tsv)
	/// Modes: exp = Expert; rev = Expert + Revengeance (CalamityMod.World.CalamityWorld.revenge) with the levy rippers on; revoff = Revengeance with the levy rippers off (what shipped before).
	/// </summary>
	public partial class CombatHarness
	{
		internal static bool CrOn => string.Equals(Scenario, "calrev", StringComparison.OrdinalIgnoreCase);
		private static bool _crHooked;
		/// <summary>LEVY_CR_SP=1: single-player owner emulation. A dedicated server has Main.myPlayer = 255, so soldier shots are owned by the inactive null player and every Calamity projectile that reads Main.player[owner]
		/// (flails, held shortswords, anything anchored to its owner) kills itself on its first tick: a measurement artifact, never what a single-player game does. With this on, Main.myPlayer is 0 (the stand-in commander)
		/// around each Main.Update, and 255 around the commander's own Player.Update, exactly as HarnessOnHit's emulation does.</summary>
		internal static bool CrSp;
		private static void CrPlayerUpdate(On_Player.orig_Update orig, Player self, int i)
		{
			if (CrSp && self.whoAmI == 0 && Main.myPlayer == 0) { Main.myPlayer = 255; try { orig(self, i); } finally { Main.myPlayer = 0; } }
			else orig(self, i);
		}

		internal sealed class CrLevySpec
		{
			public char Cls; public string Weapon, Ammo; public string[] Armor = Array.Empty<string>(), Acc = Array.Empty<string>();
			public int LifeCrystals, ManaCrystals, Potions; public string Potion = ""; public bool ManAtArms;
			public int Hp => 100 + 20 * LifeCrystals; public int Mana => 20 + 20 * ManaCrystals;
			public string Describe() => $"{Cls}:{Weapon}{(string.IsNullOrEmpty(Ammo) ? "" : "/" + Ammo)}|{string.Join("+", Armor.Select(a => a.Split(' ')[0]).Distinct())}|{(Acc.Length == 0 ? "-" : string.Join("+", Acc))}|{(ManAtArms ? "MaA" : "Lv")}|{Hp}hp/{Mana}mp|{Potions}x{Potion}";
		}

		internal sealed class CrRun
		{
			public int Mp, BossDef = -1; public bool Jungle; public string CloneWeapon, Side; public string Kind, Boss = "eye", Mode = "exp", Order = "free", Arena = "surf", Gear = "van", Grade = "mix", Cmd = "near"; public char Clone; public int Seed, Cap, N = 10; public float HpX = 1f, Dr, BossHpX = 1f, HpScale = -1f, Fear = -1f; public bool Dbg, Diag; public List<CrLevySpec> Specs; public string Desc = "";
			public string Key => $"{Kind}|{Boss}|{Mode}|{Order}|{Seed}|{Cap}" + (N != 10 ? "|n" + N : "") + (HpX != 1f ? "|hp" + HpX.ToString(CultureInfo.InvariantCulture) : "") + (Dr != 0f ? "|dr" + Dr.ToString(CultureInfo.InvariantCulture) : "")
				+ (Arena != "surf" ? "|a" + Arena : "") + (Gear != "van" ? "|g" + Gear : "") + (Grade != "mix" ? "|gr" + Grade : "") + (Clone != 0 ? "|cl" + Clone : "") + (Cmd != "near" ? "|c" + Cmd : "") + (Jungle ? "|jg" : "") + (Mp > 0 ? "|mp" + Mp : "") + (BossDef >= 0 ? "|bdef" + BossDef : "") + (string.IsNullOrEmpty(CloneWeapon) ? "" : "|cw" + CloneWeapon) + (string.IsNullOrEmpty(Side) ? "" : "|sd" + Side) + (BossHpX != 1f ? "|bhp" + BossHpX.ToString(CultureInfo.InvariantCulture) : "") + (HpScale >= 0f ? "|hs" + HpScale.ToString(CultureInfo.InvariantCulture) : "") + (Fear >= 0f ? "|fe" + Fear.ToString(CultureInfo.InvariantCulture) : "");
		}
		/// <summary>The plainest ammo item for an ammo type (cw= runs and fallbacks): the lowest-damage vanilla item with that ammo type, else the first modded one.</summary>
		private static string CrDefaultAmmo(int useAmmo)
		{
			if (useAmmo == AmmoID.Bullet) return "Musket Ball";
			if (useAmmo == AmmoID.Arrow) return "Wooden Arrow";
			if (useAmmo == AmmoID.Rocket) return "Rocket I";
			if (useAmmo == AmmoID.Dart) return "Poison Dart";
			if (useAmmo == AmmoID.Gel) return "Gel";
			Item best = null;
			for (int k = 1; k < ItemLoader.ItemCount; k++)
			{
				var it = ContentSamples.ItemsByType.TryGetValue(k, out var smp) ? smp : null;
				if (it == null || it.ammo != useAmmo || it.IsAir) continue;
				if (best == null || (k < ItemID.Count && best.type >= ItemID.Count) || (k < ItemID.Count == best.type < ItemID.Count && it.damage < best.damage)) best = it;
			}
			if (best == null) return "";
			return best.type < ItemID.Count ? Lang.GetItemNameValue(best.type) : best.ModItem.Mod.Name + "/" + best.ModItem.Name;
		}
		private readonly List<CrRun> _crRuns = new();
		private int _crI = -1; private bool _crStarted;
		private string _crOut;

		private void CrParseSpec(string path)
		{
			var done = new HashSet<string>();
			if (File.Exists(_crOut)) foreach (var l in File.ReadLines(_crOut).Skip(1)) { var c = l.Split('\t'); if (c.Length > 1) done.Add(c[0]); }
			int skipped = 0;
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;
				var f = line.Split('\t');
				CrRun r;
				if (f[0] == "fight" && f.Length >= 5)
				{
					r = new CrRun { Kind = "fight", Boss = f[1], Mode = f[2], Seed = int.Parse(f[3]), Cap = int.Parse(f[4]), Order = int.Parse(f[3]) % 2 == 1 ? "free" : "sd" };
					for (int x = 5; x < f.Length; x++)
					{
						int eq = f[x].IndexOf('='); if (eq < 0) continue;
						string k = f[x].Substring(0, eq), v = f[x].Substring(eq + 1);
						if (k == "order") r.Order = v; else if (k == "n") r.N = v == "cp" ? CrRealMax(r.Boss) : int.Parse(v); else if (k == "hpx") r.HpX = float.Parse(v, CultureInfo.InvariantCulture); else if (k == "dr") r.Dr = float.Parse(v, CultureInfo.InvariantCulture);
						else if (k == "clone") r.Clone = v[0]; else if (k == "cmd") r.Cmd = v; else if (k == "arena") r.Arena = v; else if (k == "gear") r.Gear = v; else if (k == "grade") r.Grade = v;
						else if (k == "mp") r.Mp = int.Parse(v); else if (k == "bdef") r.BossDef = int.Parse(v); else if (k == "cw") r.CloneWeapon = v; else if (k == "side") r.Side = v; else if (k == "jungle") r.Jungle = v == "1"; else if (k == "dbg") r.Dbg = v == "1"; else if (k == "diag") r.Diag = v == "1"; else if (k == "bhp") r.BossHpX = float.Parse(v, CultureInfo.InvariantCulture); else if (k == "hpscale") r.HpScale = float.Parse(v, CultureInfo.InvariantCulture); else if (k == "fear") r.Fear = float.Parse(v, CultureInfo.InvariantCulture);
					}
					if (r.Gear == "mod" && r.Grade == "mix") r.Grade = "lv";
					r.Specs = r.Gear == "mod" ? CrRollTier(r.Boss, r.Seed, r.N, r.HpX, r.Grade == "lv") : CrRoll(r.Boss, r.Seed, r.N, r.HpX);
					if (r.Clone != 0) { var proto = CrRollTier(r.Boss, r.Seed, 40, r.HpX, true).First(x => x.Cls == r.Clone); r.Specs = Enumerable.Range(0, r.N).Select(_ => proto).ToList(); }
					if (!string.IsNullOrEmpty(r.CloneWeapon)) r.Specs = r.Specs.Select(sp => new CrLevySpec { Cls = sp.Cls, Weapon = r.CloneWeapon, Ammo = "", Armor = sp.Armor, Acc = sp.Acc, LifeCrystals = sp.LifeCrystals, ManaCrystals = r.Mp > 0 ? Math.Max(0, (r.Mp - 20) / 20) : sp.ManaCrystals, Potions = sp.Potions, Potion = sp.Potion, ManAtArms = sp.ManAtArms }).ToList(); // cw=<item>: every levy carries this weapon (no random roll)
					r.Desc = string.Join(" ; ", r.Specs.Select(sp => sp.Describe()));
				}
				else if (f[0] == "probe" && f.Length >= 3)
				{
					r = new CrRun { Kind = "probe", Boss = "eye", Mode = f[1], Seed = 1, Cap = int.Parse(f[2]) * 60 };
					r.Specs = CrProbeSpecs();
					r.Desc = string.Join(" ; ", r.Specs.Select(sp => sp.Describe()));
				}
				else { Mod.Logger.Warn("HARNESS-CR: bad spec line: " + line); continue; }
				if (done.Contains(r.Key)) skipped++; else _crRuns.Add(r);
			}
			Mod.Logger.Info($"HARNESS-CR: spec {path}: {_crRuns.Count} runs queued, {skipped} already in {_crOut}");
		}

		// -------------------------------------------------------------------------------------------------- items / rolled loadouts
		private static string[] CrSet(string a, string b, string c) => new[] { a, b, c };
		private static readonly string[] CrPlat = CrSet("Platinum Helmet", "Platinum Chainmail", "Platinum Greaves");
		private static readonly string[] CrGold = CrSet("Gold Helmet", "Gold Chainmail", "Gold Greaves");
		private static readonly string[] CrNinja = CrSet("Ninja Hood", "Ninja Shirt", "Ninja Pants");
		private static readonly string[] CrFossil = CrSet("Fossil Helmet", "Fossil Plate", "Fossil Greaves");
		private static readonly string[] CrJungle = CrSet("Jungle Hat", "Jungle Shirt", "Jungle Pants");
		private static readonly string[] CrMeteor = CrSet("Meteor Helmet", "Meteor Suit", "Meteor Leggings");
		private static readonly string[] CrShadow = CrSet("Shadow Helmet", "Shadow Scalemail", "Shadow Greaves");
		private static readonly string[] CrCrimson = CrSet("Crimson Helmet", "Crimson Scalemail", "Crimson Greaves");
		private static readonly string[] CrMolten = CrSet("Molten Helmet", "Molten Breastplate", "Molten Greaves");
		private static readonly string[] CrIron = CrSet("Iron Helmet", "Iron Chainmail", "Iron Greaves");
		private static readonly string[] CrCopper = CrSet("Copper Helmet", "Copper Chainmail", "Copper Greaves");
		private static readonly string[] CrWulfrum = CrSet("CalamityMod/WulfrumHat", "CalamityMod/WulfrumJacket", "CalamityMod/WulfrumOveralls");

		private static Dictionary<string, int> _crNameIds;
		internal static int CrItemId(string name)
		{
			if (name.Contains('/')) { if (ModContent.TryFind(name, out ModItem mi)) return mi.Type; throw new Exception("unknown modded item '" + name + "'"); }
			if (_crNameIds == null)
			{
				_crNameIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				for (int k = 1; k < ItemID.Count; k++)
				{
					string n = Lang.GetItemNameValue(k);
					if (!string.IsNullOrEmpty(n) && !_crNameIds.ContainsKey(n)) _crNameIds[n] = k;
				}
			}
			if (!_crNameIds.TryGetValue(name, out int id)) throw new Exception("unknown item '" + name + "'");
			return id;
		}
		private static Item CrMake(string name, int stack = 1) { var it = new Item(); it.SetDefaults(CrItemId(name)); it.stack = stack; return it; }

		private static bool CrPost(string boss) => boss == "brain" || boss == "eow";

		private static readonly Dictionary<char, (string w, string ammo)[]> CrPoolPre = new()
		{
			['R'] = new[] { ("Minishark", "Silver Bullet|Musket Ball"), ("Boomstick", "Silver Bullet|Musket Ball"), ("Demon Bow", "Flaming Arrow|Wooden Arrow"), ("Tendon Bow", "Flaming Arrow|Wooden Arrow"), ("Musket", "Silver Bullet|Musket Ball"), ("Flintlock Pistol", "Musket Ball") },
			['M'] = new[] { ("Diamond Staff", ""), ("Ruby Staff", ""), ("Wand of Sparking", ""), ("Vilethorn", ""), ("Space Gun", ""), ("Sapphire Staff", "") },
			['E'] = new[] { ("Enchanted Sword", ""), ("Starfury", ""), ("Blade of Grass", ""), ("Platinum Broadsword", ""), ("Gold Broadsword", ""), ("Katana", "") },
			['S'] = new[] { ("Slime Staff", ""), ("Finch Staff", ""), ("Flinx Staff", "") },
		};
		private static readonly Dictionary<char, (string w, string ammo)[]> CrPoolPost = new()
		{
			['R'] = new[] { ("Minishark", "Silver Bullet"), ("Phoenix Blaster", "Silver Bullet|Musket Ball"), ("Handgun", "Silver Bullet|Musket Ball"), ("The Undertaker", "Silver Bullet|Musket Ball"), ("Musket", "Silver Bullet"), ("Demon Bow", "Hellfire Arrow|Flaming Arrow") },
			['M'] = new[] { ("Aqua Scepter", ""), ("Magic Missile", ""), ("Flamelash", ""), ("Crimson Rod", ""), ("Diamond Staff", ""), ("Space Gun", "") },
			['E'] = new[] { ("Muramasa", ""), ("Blade of Grass", ""), ("Starfury", ""), ("Blood Butcherer", ""), ("Light's Bane", ""), ("Enchanted Sword", "") },
			['S'] = new[] { ("Imp Staff", ""), ("Slime Staff", ""), ("Flinx Staff", "") },
		};
		private static readonly Dictionary<char, string[][]> CrArmorPre = new()
		{
			['R'] = new[] { CrPlat, CrGold, CrFossil, CrShadow, CrNinja, CrIron }, ['M'] = new[] { CrJungle, CrMeteor, CrPlat, CrGold }, ['E'] = new[] { CrPlat, CrGold, CrShadow, CrCrimson, CrIron }, ['S'] = new[] { CrGold, CrPlat, CrCrimson, CrIron },
		};
		private static readonly Dictionary<char, string[][]> CrArmorPost = new()
		{
			['R'] = new[] { CrShadow, CrCrimson, CrMolten, CrFossil, CrPlat }, ['M'] = new[] { CrJungle, CrMeteor, CrShadow, CrCrimson }, ['E'] = new[] { CrMolten, CrShadow, CrCrimson, CrPlat }, ['S'] = new[] { CrCrimson, CrMolten, CrGold },
		};
		private static readonly string[] CrAccCommon = { "", "Hermes Boots", "Cloud in a Bottle", "Band of Regeneration", "Shackle", "Anklet of the Wind" };

		/// <summary>Ten levies, R3 M3 E2 S2, every stat rolled from the seed alone (the boss and the mode do not enter the roll except the pre/post-Eye tier), so a seed is the same army in Expert and in Revengeance.</summary>
		private static List<CrLevySpec> CrRoll(string boss, int seed, int total = 10, float hpx = 1f)
		{
			bool post = CrPost(boss);
			var rng = new Random(seed * 7919 + (post ? 5 : 0) + 17);
			var list = new List<CrLevySpec>();
			var pool = post ? CrPoolPost : CrPoolPre; var arm = post ? CrArmorPost : CrArmorPre;
			int nr = (int)Math.Round(total * 0.3), nm = (int)Math.Round(total * 0.3), ne = (int)Math.Round(total * 0.2), ns = Math.Max(0, total - nr - nm - ne);
			foreach (var (cls, n) in new[] { ('R', nr), ('M', nm), ('E', ne), ('S', ns) })
				for (int k = 0; k < n; k++)
				{
					var (w, ammoOpts) = pool[cls][rng.Next(pool[cls].Length)];
					string ammo = ""; if (ammoOpts != "") { var ao = ammoOpts.Split('|'); ammo = ao[rng.Next(ao.Length)]; }
					var armor = arm[cls][rng.Next(arm[cls].Length)];
					bool maa = rng.NextDouble() < 0.35;
					int slots = maa ? 3 : 1;
					var accPool = CrAccCommon.ToList(); if (cls == 'E') { accPool.Add("Feral Claws"); accPool.Add("Feral Claws"); } if (cls == 'M') { accPool.Add("Band of Starpower"); accPool.Add("Band of Starpower"); }
					var acc = new List<string>();
					for (int a = 0; a < slots; a++) { string pick = accPool[rng.Next(accPool.Count)]; if (pick != "" && !acc.Contains(pick)) acc.Add(pick); }
					int lc;
					if (post) lc = 5;   // ~200 hp
					else { double q = rng.NextDouble(); lc = q < .20 ? 0 : q < .40 ? 1 : q < .65 ? 2 : q < .80 ? 3 : q < .90 ? 4 : 5; }   // 100..200 hp
					int mc;
					if (post) mc = cls == 'M' ? 9 : rng.Next(5, 10);
					else if (cls == 'M') { double q = rng.NextDouble(); mc = q < .15 ? 1 : q < .35 ? 2 : q < .55 ? 3 : q < .75 ? 4 : q < .90 ? 6 : 9; }
					else mc = rng.Next(0, 5);
					if (hpx != 1f) lc = (int)Math.Round((((100 + 20 * lc) * hpx) - 100) / 20.0);
					list.Add(new CrLevySpec { Cls = cls, Weapon = w, Ammo = ammo, Armor = armor, Acc = acc.ToArray(), LifeCrystals = lc, ManaCrystals = mc, ManAtArms = maa, Potions = rng.Next(3, 9), Potion = post ? "Healing Potion" : "Lesser Healing Potion" });
				}
			return list;
		}

		/// <summary>Probe army: two vanilla-gear levies (the per-second stat pass only) and two Calamity-gear levies (Wulfrum armor: the per-tick gear frame), 400 hp so they live long enough to fill the bars.</summary>
		private static List<CrLevySpec> CrProbeSpecs() => new()
		{
			new CrLevySpec { Cls = 'R', Weapon = "Minishark", Ammo = "Silver Bullet", Armor = CrPlat, LifeCrystals = 15, ManaCrystals = 0, Potions = 6, Potion = "Lesser Healing Potion" },
			new CrLevySpec { Cls = 'E', Weapon = "Platinum Broadsword", Armor = CrPlat, LifeCrystals = 15, ManaCrystals = 0, Potions = 6, Potion = "Lesser Healing Potion" },
			new CrLevySpec { Cls = 'R', Weapon = "Minishark", Ammo = "Silver Bullet", Armor = CrWulfrum, LifeCrystals = 15, ManaCrystals = 0, Potions = 6, Potion = "Lesser Healing Potion" },
			new CrLevySpec { Cls = 'E', Weapon = "Platinum Broadsword", Armor = CrWulfrum, LifeCrystals = 15, ManaCrystals = 0, Potions = 6, Potion = "Lesser Healing Potion" },
		};

		// -------------------------------------------------------------------------------------------------- begin
		private delegate void CrOrigUpdateConnectedClients();
		private delegate void CrHookUpdateConnectedClients(CrOrigUpdateConnectedClients orig);
		private static void CrStandinDetour(CrOrigUpdateConnectedClients orig) { if (Enabled && CrOn) { try { Main.player[0].active = true; } catch { } return; } orig(); }

		private bool BeginCalRev()
		{
			if (!ModLoader.HasMod("CalamityMod")) { Mod.Logger.Warn("HARNESS-CR: CalamityMod is not loaded"); return false; }
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new CrHookUpdateConnectedClients(CrStandinDetour)); }
			catch (Exception e) { Mod.Logger.Warn("HARNESS-CR: could not hook UpdateConnectedClients: " + e.Message); }
			_crOut = Environment.GetEnvironmentVariable("LEVY_CR_OUT") ?? (OutPath + ".cr.tsv");
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_CR_STEPS"), out int st) && st >= 1 && st <= 400) StepsPerTurn = st;
			if (!File.Exists(_crOut)) File.WriteAllText(_crOut, CrHeader + "\n");
			string spec = Environment.GetEnvironmentVariable("LEVY_CR_SPEC");
			if (string.IsNullOrEmpty(spec) || !File.Exists(spec)) { Mod.Logger.Warn("HARNESS-CR: LEVY_CR_SPEC missing"); return false; }
			try { CrTiers(); CrDumpTierDoc(); } catch (Exception e) { Mod.Logger.Warn("HARNESS-CR: tier tables failed: " + e); }
			CrParseSpec(spec);
			if (!_crHooked)
			{
				_crHooked = true;
				On_NPC.StrikeNPC_HitInfo_bool_bool += CrStrikeDetour;
				On_NPC.UpdateNPC += CrNpcUpdateDetour;
				On_Projectile.Update += CrProjUpdateDetour;
				On_Player.Update += CrPlayerUpdate;
			}
			CrSp = Environment.GetEnvironmentVariable("LEVY_CR_SP") == "1";
			global::Levy.Common.Util.LevyUtil.SpEmulation = CrSp;
			global::Levy.Common.Weapons.ModWeaponGates.LabSetFlags(Environment.GetEnvironmentVariable("LEVY_CR_DOWNED")); // e.g. downedDoG: measure a milestone-gated weapon as the unlocked weapon it becomes
			Mod.Logger.Info("HARNESS-CR: single-player owner emulation " + (CrSp ? "ON (Main.myPlayer = 0)" : "off (dedicated server, owner 255)"));
			_crI = 0; _crStarted = false;
			return true;
		}

		internal static void CrUnhook()
		{
			if (!_crHooked) return;
			_crHooked = false;
			try { On_NPC.StrikeNPC_HitInfo_bool_bool -= CrStrikeDetour; On_NPC.UpdateNPC -= CrNpcUpdateDetour; On_Projectile.Update -= CrProjUpdateDetour; On_Player.Update -= CrPlayerUpdate; } catch { }
		}

		// -------------------------------------------------------------------------------------------------- Calamity switches
		private static System.Reflection.FieldInfo _crRevField, _crDeathField;
		private static void CrSetRevenge(bool on, bool death = false)
		{
			if (_crRevField == null)
			{
				Type w = ModLoader.GetMod("CalamityMod").Code.GetType("CalamityMod.World.CalamityWorld");
				_crRevField = w.GetField("revenge", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				_crDeathField = w.GetField("death", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			}
			_crRevField.SetValue(null, on);
			_crDeathField?.SetValue(null, death);
		}

		// -------------------------------------------------------------------------------------------------- attribution hooks
		private static int _crNpcCtx = -1, _crProjDepth;
		private static CrState _cr;
		internal static CrState CrLive => _cr;
		internal static bool CrIsBossOrAdd(int type) => _cr != null && (type == _cr.BossType || _cr.PartTypes.Contains(type) || _cr.AddTypes.Contains(type));

		private static void CrNpcUpdateDetour(On_NPC.orig_UpdateNPC orig, NPC self, int i)
		{
			if (_cr != null && _cr.Run.BossDef >= 0 && !(self.ModNPC is SoldierNPC) && CrIsBossOrAdd(self.type)) // bdef=N: the boss and its parts hold defense N (measurement of low-damage weapons without the defense floor)
			{
				self.defense = _cr.Run.BossDef; self.defDefense = _cr.Run.BossDef;
				try { orig(self, i); } finally { self.defense = _cr.Run.BossDef; self.defDefense = _cr.Run.BossDef; }
				return;
			}
			if (_cr == null || !(self.ModNPC is SoldierNPC)) { orig(self, i); return; }
			int prev = _crNpcCtx; _crNpcCtx = self.whoAmI;
			try { orig(self, i); } finally { _crNpcCtx = prev; }
		}

		private static void CrProjUpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (_cr == null) { orig(self, i); return; }
			_crProjDepth++;
			try { orig(self, i); } finally { _crProjDepth--; }
		}

		private static int CrStrikeDetour(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{
			int done = orig(self, hit, fromNet, noPlayerInteraction);
			var s = _cr;
			if (s != null && _crProjDepth == 0 && _crNpcCtx >= 0 && CrIsBossOrAdd(self.type)) s.Credit(_crNpcCtx, self.type, done, "melee");
			return done;
		}

		// -------------------------------------------------------------------------------------------------- fight state
		internal sealed class CrLevy
		{
			public char Cls; public SoldierRecord Rec; public int Idx; public int Id; public CrLevySpec Spec; public bool Upkeep;
			public bool Dead; public int DeathTick = -1; public int LastLife, LastAttackerType = -1;
			public long DmgBoss, DmgAdd; public long TakenBoss, TakenAdd, TakenOther;
			public int MaxHp; public string Weapon; public int PotionsLeft, PotionsDrunk;
			public int ManaMin = 9999; public int LifeMin = 99999;
			public float PeakRage, PeakAdr; public int RageModeTicks, AdrModeTicks, Hits;
		}

		internal sealed class CrState
		{
			public bool FleeLogged;
			public CrRun Run; public List<CrLevy> Levies = new();
			public int BossType, BodyType = -1, TailType = -1; public HashSet<int> AddTypes = new(), PartTypes = new(); public bool Worm => PartTypes.Count > 0;
			public Dictionary<string, long> Srcs = new(); public void Src(string k, long d) { Srcs.TryGetValue(k, out long v); Srcs[k] = v + d; } public long DtdTicks, LastDmgSum, AddKills; public int PrevAdds; public SortedDictionary<int, (long ticks, long dmg)> Phase = new(); public long TgtLevy, TgtPlayer, TgtNone, AddTgtLevy, AddTgtPlayer, AddTgtNone, HitsBoss, DmgHead, DmgBody, DmgTail, DmgAdd, EnragedTicks, BossAliveTicks; public double Moved; public Vector2 LastPos; public bool HavePos; public int FirstHitTick = -1, MaxParts, MaxAdds; public long BossLifeMaxSum;
			public int Tick; public int BossIdx = -1; public int BossLifeMax; public int BossLife; public int BossDamage, BossDefense;
			public int AddsPeak; public int HalfTick = -1; public bool BossGone; public int EndTick = -1; public string Result = "";
			public long DmgUnattributed, ProjDmg, MeleeDmg;
			public int Rage0, Adr0, Dr0, Lost0;
			public void Credit(int soldierIdx, int npcType, int dmg, string how)
			{
				if (dmg <= 0) return;
				var l = soldierIdx >= 0 ? Levies.FirstOrDefault(x => x.Idx == soldierIdx) : null;
				if (l == null) { DmgUnattributed += dmg; return; }
				bool part = npcType == BossType || PartTypes.Contains(npcType);
				if (part) { l.DmgBoss += dmg; HitsBoss++; if (FirstHitTick < 0) FirstHitTick = Tick; if (npcType == BossType) DmgHead += dmg; else if (npcType == BodyType) DmgBody += dmg; else if (npcType == TailType) DmgTail += dmg; } else { l.DmgAdd += dmg; DmgAdd += dmg; }
				if (how == "melee") MeleeDmg += dmg; else ProjDmg += dmg;
			}
		}

		// -------------------------------------------------------------------------------------------------- arena
		private int CrF, _crCx; private string _crArenaBuilt = "";
		private void CrBuildArena(string kind)
		{
			float n = Main.maxTilesX / 4200f; n *= n;
			int band = (int)(60f + 10f * n + Main.worldSurface / 6.0) + 2;
			_crCx = _cx;
			int cx = _cx, half = 110, fy;
			if (kind == "ug")
			{
				// enclosed chamber in the cavern layer: 220 wide x 90 tall, solid shell all round (10 ceiling, 25 walls, 35 floor)
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
			for (int k = 1; k <= 4; k++)
			{
				int py = fy - 7 * k;
				for (int x = cx - half + 6; x <= cx + half - 6; x++)
				{
					Tile t = Main.tile[x, py];
					t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.WallType = 0;
				}
			}
			_crArenaBuilt = kind;
			Mod.Logger.Info($"HARNESS-CR: arena '{kind}' built: floor row {fy}, half-width {half}, 4 platform rows; world {Main.maxTilesX}x{Main.maxTilesY} worldSurface {Main.worldSurface:0} rockLayer {Main.rockLayer:0} => arena is {(fy < Main.worldSurface ? "ABOVE" : "BELOW")} worldSurface, {(fy > Main.rockLayer ? "in" : "above")} the cavern layer");
		}

		private Vector2 _crCmdPos;
		private void CrPinCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.team = 0;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			Vector2 pin = _crRunCur != null && _crRunCur.Cmd == "far" ? new Vector2((_crCx + 121) * 16f, CrF * 16f) : _crCmdPos; // cmd=far: the commander stands outside the arena, so he is never the closer target
			p.position = new Vector2(pin.X - p.width / 2f, pin.Y - p.height);
			p.velocity = Vector2.Zero; p.immune = true; p.immuneTime = 30;
			CrApplyZones(p); // Brain of Cthulhu flees a target outside the Crimson, Desert Scourge enrages outside the desert; the dummy copies the commander's zones (BiomeMirror)
			int bt = ModContent.BuffType<SpectatorBuff>();
			if (!p.HasBuff(bt)) { p.AddBuff(bt, 600000); if (p.TryGetModPlayer(out SpectatorPlayer sp)) sp.Anchor = p.Center; }
		}

		// -------------------------------------------------------------------------------------------------- step
		private CrState _crS;
		private bool _crBrainRun;
		private int _crTickInRun;
		private void StepCalRev()
		{
			if (_crI < 0) return;
			if (_crI >= _crRuns.Count) { try { File.WriteAllText(_crOut + ".done", "1"); } catch { } Finish("calrev complete"); return; }
			CrRun run = _crRuns[_crI];
			Main.dayTime = false; Main.time = 16200; Main.raining = false; Main.bloodMoon = false; Main.eclipse = false;
			try
			{
				if (!_crStarted && _crArenaBuilt != run.Arena) CrBuildArena(run.Arena);
				StepCrFight(run);
			}
			catch (Exception e)
			{
				Mod.Logger.Warn("HARNESS-CR: run threw: " + e);
				CrAppendError(run, e.GetType().Name + ":" + e.Message);
				_cr = null; _crStarted = false; _crI++;
				try { ClearAll(); CrPurgeNpcs(); } catch { }
			}
		}

		private void CrPinStrays(int keep)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || n.friendly) continue;
				if (i == keep || (_cr != null && (_cr.AddTypes.Contains(n.type) || _cr.PartTypes.Contains(n.type) || n.type == _cr.BossType)) || (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget)) continue;
				n.active = false;
			}
		}

		private static readonly HashSet<string> _crLogged = new();
		private CrLevy CrSpawnLevy(CrLevySpec spec, CrRun run, int slot, int count, Vector2 center)
		{
			var rec = MakeRecord($"{spec.Cls}{slot}");
			rec.Grade = spec.ManAtArms ? Grade.ManAtArms : Grade.Levy;
			rec.LifeCrystals = spec.LifeCrystals; rec.ManaCrystals = spec.ManaCrystals;
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.LoadoutLocked = true;
			rec.Order = run.Order == "sd" ? SoldierOrder.SearchDestroy : run.Order == "follow" ? SoldierOrder.Follow : SoldierOrder.FreeRein;
			var gear = rec.Gear;
			Item w = CrMake(spec.Weapon);
			if (!gear.CanHold(SoldierGear.SlotWeapon, w)) { Mod.Logger.Warn($"HARNESS-CR: levy cannot hold {spec.Weapon}; fallback {CrFallbackWeapon(spec.Cls)}"); spec.Weapon = CrFallbackWeapon(spec.Cls); spec.Ammo = spec.Cls == 'R' ? "Silver Bullet" : ""; w = CrMake(spec.Weapon); }
			if (string.IsNullOrEmpty(spec.Ammo) && w.useAmmo != AmmoID.None) spec.Ammo = CrDefaultAmmo(w.useAmmo); // cw=<ammo weapon>: the levy gets a pouch the weapon can use
			gear.Weapon = w; gear.SetLocked(SoldierGear.SlotWeapon, true);
			if (!string.IsNullOrEmpty(run.Side) && spec.Cls == 'E')
			{
				Item sd = CrMake(run.Side);
				if (gear.CanHold(SoldierGear.SlotSidearm, sd)) { gear.Sidearm = sd; gear.SetLocked(SoldierGear.SlotSidearm, true); } else Mod.Logger.Warn($"HARNESS-CR: levy cannot hold sidearm {run.Side}");
			}
			if (_crLogged.Add(spec.Weapon) || (!string.IsNullOrEmpty(run.Side) && _crLogged.Add("side:" + run.Side)))
			{
				var wp = global::Levy.Common.Weapons.WeaponClassifier.GetProfile(w); var sp2 = gear.HasSidearm ? global::Levy.Common.Weapons.WeaponClassifier.GetProfile(gear.Sidearm) : null;
				Mod.Logger.Info($"HARNESS-CR: weapon {spec.Weapon}: family={wp.Family} supported={wp.Supported} melee={wp.IsMelee} projMelee={wp.FiresProjectileMelee} shortShot={wp.ShortShot} pureContact={wp.IsPureContactBlade} range={wp.RangeTiles} meleeReach={wp.MeleeReachTiles}" + (sp2 == null ? "" : $" | side {run.Side}: family={sp2.Family} supported={sp2.Supported} range={sp2.RangeTiles} deployer={sp2.IsDeployer}"));
			}
			if (!string.IsNullOrEmpty(spec.Ammo))
			{
				Item am = CrMake(spec.Ammo, LevyConstants.AmmoPouchMax);
				if (!gear.CanHold(SoldierGear.SlotAmmo, am)) throw new Exception("levy cannot hold ammo " + spec.Ammo);
				gear.Ammo = am; gear.SetLocked(SoldierGear.SlotAmmo, true);
			}
			for (int a = 0; a < spec.Armor.Length; a++)
			{
				Item it = CrMake(spec.Armor[a]);
				int sl = a == 0 ? SoldierGear.SlotHead : a == 1 ? SoldierGear.SlotBody : SoldierGear.SlotLegs;
				if (!gear.CanHold(sl, it)) throw new Exception("levy cannot wear " + spec.Armor[a]);
				gear.Set(sl, it); gear.SetLocked(sl, true);
			}
			int accSlots = rec.AccessorySlots, placed = 0;
			foreach (string an in spec.Acc)
			{
				if (placed >= accSlots) break;
				Item it = CrMake(an);
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
			sn.Stations = Systems.StationFlags.Campfire | Systems.StationFlags.HeartLantern;
			n.life = n.lifeMax = rec.MaxHp; rec.CurrentHp = rec.MaxHp;
			return new CrLevy { Cls = spec.Cls, Rec = rec, Idx = idx, Id = rec.Id, Spec = spec, LastLife = n.life, MaxHp = rec.MaxHp, Weapon = spec.Weapon, PotionsLeft = spec.Potions };
		}

		private static int CrBossNpcType(string boss) => boss switch { "kslime" => NPCID.KingSlime, "brain" => NPCID.BrainofCthulhu, "eow" => NPCID.EaterofWorldsHead, "qb" => NPCID.QueenBee, "skel" => NPCID.SkeletronHead, "ds" => CrCalNpc("DesertScourgeHead"), _ => NPCID.EyeofCthulhu };

		private static void CrApplyMode(string mode)
		{
			Main.GameMode = GameModeID.Expert;
			bool death = mode == "death";
			bool rev = mode == "rev" || mode == "revoff" || death;
			CrSetRevenge(rev, death);
			LevyRippers.Disabled = !(mode == "rev" || death);
			CrDrGlobal.Dr = 0f;
		}

		private float[] CrBars(CrLevy l)
		{
			var n = Main.npc[l.Idx];
			if (!(n.ModNPC is SoldierNPC sn) || sn.Dummy == null) return null;
			return HarnessCalamityProbe.RageAdrenaline(sn.Dummy);
		}

		private void StepCrFight(CrRun run)
		{
			bool probe = run.Kind == "probe";
			if (!_crStarted)
			{
				ClearAll(); CrPurgeNpcs();
				CrApplyMode(run.Mode); CrDrGlobal.Dr = run.Dr; _crBrainRun = run.Boss == "brain"; _crRunCur = run;
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
				NPC.downedBoss1 = false; NPC.downedBoss2 = false; NPC.downedSlimeKing = false;
				_crCmdPos = new Vector2((_crCx - 6) * 16f, CrF * 16f);
				CrPinCommander();
				var s = new CrState { Run = run, BossType = CrBossNpcType(run.Boss) };
				CrSetupBoss(s, run);
				CrSetHpScale(run); CrSetFear(run);
				CrResetDowned();
				int total = run.Specs.Count, slot = 0;
				foreach (var sp in run.Specs) s.Levies.Add(CrSpawnLevy(sp, run, slot++, total, _crCmdPos));
				if (probe) { for (int k = 0; k < s.Levies.Count; k++) s.Levies[k].Upkeep = k >= 2; }
				float spawnDx = 30f; // tiles right of the commander; a worm spawns off-screen like NPC.SpawnOnPlayer does (~70-100 tiles away), not 43 tiles from the army: LEVY_CR_WORMDIST overrides (30 = the old harness position)
				if (s.Worm) spawnDx = float.TryParse(Environment.GetEnvironmentVariable("LEVY_CR_WORMDIST"), NumberStyles.Float, CultureInfo.InvariantCulture, out float wd) ? wd : 80f;
				int bx = (int)((_crCx + spawnDx) * 16f), by = run.Boss == "kslime" ? (int)((CrF - 6) * 16f) : (int)((CrF - 38) * 16f);
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(s.BossType, Environment.GetEnvironmentVariable("LEVY_CR_DOWNED") != "0"); // a rematch: the first kill of a boss is solo
				int ei = NPC.NewNPC(new EntitySource_WorldEvent("LevyCalRev"), bx, by, s.BossType);
				if (ei < 0 || ei >= Main.maxNPCs) throw new Exception("boss spawn failed");
				NPC boss = Main.npc[ei]; s.BossIdx = ei;
				if ((probe || run.BossHpX != 1f) && !s.Worm) { boss.lifeMax = (int)(boss.lifeMax * (probe ? 40f : run.BossHpX)); boss.life = boss.lifeMax; }
				s.BossLifeMax = boss.lifeMax; s.BossLife = boss.life; s.BossDamage = boss.damage; s.BossDefense = boss.defense;
				boss.timeLeft = 99999;
				s.Rage0 = LevyRippers.RageActivations; s.Adr0 = LevyRippers.AdrenalineActivations; s.Dr0 = LevyRippers.DrCuts; s.Lost0 = LevyRippers.AdrenalineLost;
				_crS = s; _cr = s; _crStarted = true; _crTickInRun = 0; SoldierNPC.HitDbg = new(); SoldierNPC.HitGapDbg = new();
				if (run.Diag) global::Levy.Common.Potions.SpectatorDiagnostic.Set(true);
				global::Levy.Common.Recording.FightRecAuto.Tag(("arena", run.Arena), ("seed", run.Seed), ("mode", run.Mode), ("gear", run.Gear + "_n" + run.N), ("cellkey", run.Key));
				if (probe && !File.Exists(_crOut + ".probe.tsv")) File.WriteAllText(_crOut + ".probe.tsv", "mode\ttick\tlevy\tgear\tupkeep\trage\tadr\trageMode\tadrMode\thp\tboss\n");
				Mod.Logger.Info($"HARNESS-CR: start {run.Key}: {run.Boss} {boss.life}/{boss.lifeMax} def {boss.defense} dmg {boss.damage} revenge={LevyRippers.RevengeanceOn} death={CrDeathOn()} expert={Main.expertMode} rippersOff={LevyRippers.Disabled} arena={run.Arena} gear={run.Gear} n={run.N} hpscale%/cp={(global::Levy.Common.Config.LevyConfig.Get.BossHpScalingPercentPerCp)}; {run.Desc}");
				return;
			}
			var st = _crS; st.Tick = ++_crTickInRun;
			CrPinCommander();
			CrPinStrays(st.BossIdx);
			NPC e = Main.npc[st.BossIdx];
			bool bossAlive = e.active && e.type == st.BossType;
			if (st.Worm) bossAlive = CrWormTick(st, ref e);
			else if (bossAlive) { st.BossLife = e.life; e.timeLeft = 99999; if (st.HalfTick < 0 && e.life * 2 <= st.BossLifeMax) st.HalfTick = st.Tick; }
			else if (!st.BossGone) { st.BossGone = true; st.EndTick = st.Tick; st.Result = e.life <= 0 ? "WIN" : "DESPAWN"; st.BossLife = Math.Max(0, e.life); }
			int adds = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && st.AddTypes.Contains(Main.npc[i].type)) adds++;
			st.AddsPeak = Math.Max(st.AddsPeak, adds); st.MaxAdds = st.AddsPeak;
			CrSampleTick(st, e, bossAlive, adds);
			int alive = 0;
			foreach (var l in st.Levies)
			{
				if (l.Dead) continue;
				NPC n = Main.npc[l.Idx];
				bool body = l.Rec.HasBody && n.active && n.ModNPC is SoldierNPC sn0 && ReferenceEquals(sn0.Record, l.Rec) && n.life > 0;
				if (body)
				{
					var sn = (SoldierNPC)n.ModNPC;
					sn.Stations = Systems.StationFlags.Campfire | Systems.StationFlags.HeartLantern;
					if (!probe && l.PotionsLeft > 0 && sn.PotionSickness == 0 && n.life * 100 < n.lifeMax * LevyConstants.PotionDrinkBelowPercent)
					{
						Item pot = CrMake(l.Spec.Potion);
						n.life = Math.Min(n.lifeMax, n.life + pot.healLife); l.Rec.CurrentHp = n.life; sn.PotionSickness = LevyConstants.PotionSicknessTicks; l.PotionsLeft--; l.PotionsDrunk++;
						l.LastLife = n.life;
					}
					int drop = l.LastLife - n.life;
					if (drop > 0)
					{
						l.Hits++;
						int at = sn.LastAttackerType;
						if (at == st.BossType || st.PartTypes.Contains(at)) { l.TakenBoss += drop; st.Src("boss:" + CrNpcName(at), drop); } else if (st.AddTypes.Contains(at)) { l.TakenAdd += drop; st.Src("add:" + CrNpcName(at), drop); } else { l.TakenOther += drop; st.Src("other:" + CrNpcName(at), drop); }
					}
					l.LastLife = n.life; l.LastAttackerType = sn.LastAttackerType; l.LifeMin = Math.Min(l.LifeMin, n.life);
					l.ManaMin = Math.Min(l.ManaMin, l.Rec.Mana);
					var bars = CrBars(l);
					if (bars != null)
					{
						l.PeakRage = Math.Max(l.PeakRage, bars[0]); l.PeakAdr = Math.Max(l.PeakAdr, bars[2]);
						var cp = LevyRippers.Of(sn.Dummy);
						if (cp != null) { if (LevyRippers.RageModeActive(cp)) l.RageModeTicks++; if (LevyRippers.AdrenalineModeActive(cp)) l.AdrModeTicks++; }
						if (probe && st.Tick % 30 == 0)
						{
							int li = st.Levies.IndexOf(l);
							File.AppendAllText(_crOut + ".probe.tsv", $"{run.Mode}\t{st.Tick}\t{li}\t{l.Cls}\t{(l.Upkeep ? "cal" : "van")}\t{(sn.UpkeepActive ? 1 : 0)}\t{bars[0]:0.0}\t{bars[2]:0.0}\t{(cp != null && LevyRippers.RageModeActive(cp) ? 1 : 0)}\t{(cp != null && LevyRippers.AdrenalineModeActive(cp) ? 1 : 0)}\t{n.life}\t{(bossAlive ? 1 : 0)}\n");
						}
					}
					alive++;
				}
				else
				{
					l.Dead = true; l.DeathTick = st.Tick;
					int at = l.LastAttackerType;
					if (at == st.BossType || st.PartTypes.Contains(at)) { l.TakenBoss += l.LastLife; st.Src("boss:" + CrNpcName(at), l.LastLife); } else if (st.AddTypes.Contains(at)) { l.TakenAdd += l.LastLife; st.Src("add:" + CrNpcName(at), l.LastLife); } else { l.TakenOther += l.LastLife; st.Src("other:" + CrNpcName(at), l.LastLife); }
				}
			}
			if (st.Result == "" && alive == 0) { st.Result = "LOSS"; st.EndTick = st.Tick; }
			if (st.Result == "" && st.Tick >= run.Cap) { st.Result = probe ? "PROBE" : "TIMEOUT"; st.EndTick = st.Tick; }
			if (st.Result != "" && (st.Result != "WIN" || st.Tick >= st.EndTick + 20)) CrFinishFight(run, st);
		}

		private void CrPurgeNpcs()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC) n.active = false; }
		}

		private const string CrHeader = "key\tkind\tboss\tmode\torder\tseed\tresult\tticks\tsec\tbossMax\tbossLeft\tbossDmg\tbossDef\tdmgBoss\tdmgR\tdmgM\tdmgE\tdmgS\tdmgUnattr\tdmgProj\tdmgMelee\tdeaths\tdeathsR\tdeathsM\tdeathsE\tdeathsS\tavgSurvSec\ttakenBoss\ttakenAdd\ttakenOther\taddsPeak\thalfSec\tavgHp\tpotionsDrunk\trageAct\tadrAct\tdrCuts\tadrLost\tpeakRageAvg\tpeakAdrAvg\trageSec\tadrSec\thitsTaken\tgear\tarena\tn\ttgtLevyPct\ttgtPlayerPct\ttgtNonePct\taddTgtLevyPct\taddTgtPlayerPct\thitsBoss\thitsPerSec\tdmgPerSec\tdmgHead\tdmgBody\tdmgTail\tdmgAdd\tbossMoved\tenragedSec\tfirstHitSec\tpartsMax\tdtdPct\tdpsHittable\taddKills\taddKillsPerSec\tphases\tdmgSources\tperLevy";

		private void CrFinishFight(CrRun run, CrState st)
		{
			int end = st.EndTick > 0 ? st.EndTick : st.Tick;
			Func<char, long> dmg = c => st.Levies.Where(l => l.Cls == c).Sum(l => l.DmgBoss);
			long dmgBoss = st.Levies.Sum(l => l.DmgBoss);
			int deaths = st.Levies.Count(l => l.Dead);
			Func<char, int> dc = c => st.Levies.Count(l => l.Dead && l.Cls == c);
			double avgSurv = st.Levies.Average(l => (l.Dead ? l.DeathTick : end) / 60.0);
			long tB = st.Levies.Sum(l => l.TakenBoss), tA = st.Levies.Sum(l => l.TakenAdd), tO = st.Levies.Sum(l => l.TakenOther);
			Func<double, string> f1 = v => v.ToString("0.0", CultureInfo.InvariantCulture);
			string per = string.Join(";", st.Levies.Select(l => $"{l.Cls}:{l.Weapon.Replace(' ', '_')}:dmg{l.DmgBoss}:surv{f1((l.Dead ? l.DeathTick : end) / 60.0)}:{(l.Dead ? "dead" : "alive")}:hp{l.MaxHp}:tb{l.TakenBoss}:ta{l.TakenAdd}:to{l.TakenOther}:pot{l.PotionsDrunk}:rg{l.PeakRage:0}:ad{l.PeakAdr:0}"));
			int bossLeft = st.Worm || st.BossGone ? st.BossLife : Main.npc[st.BossIdx].active ? Main.npc[st.BossIdx].life : st.BossLife;
			var sb = new StringBuilder();
			sb.Append(run.Key).Append('\t').Append(string.Join("\t", new object[] { run.Kind, run.Boss, run.Mode, run.Order, run.Seed, st.Result, end, f1(end / 60.0),
				st.BossLifeMax, bossLeft, st.BossDamage, st.BossDefense, dmgBoss, dmg('R'), dmg('M'), dmg('E'), dmg('S'), st.DmgUnattributed, st.ProjDmg, st.MeleeDmg, deaths, dc('R'), dc('M'), dc('E'), dc('S'), f1(avgSurv), tB, tA, tO, st.AddsPeak,
				st.HalfTick < 0 ? "-" : f1(st.HalfTick / 60.0), f1(st.Levies.Average(l => l.MaxHp)), st.Levies.Sum(l => l.PotionsDrunk),
				LevyRippers.RageActivations - st.Rage0, LevyRippers.AdrenalineActivations - st.Adr0, LevyRippers.DrCuts - st.Dr0, LevyRippers.AdrenalineLost - st.Lost0,
				f1(st.Levies.Average(l => l.PeakRage)), f1(st.Levies.Average(l => l.PeakAdr)), f1(st.Levies.Sum(l => l.RageModeTicks) / 60.0), f1(st.Levies.Sum(l => l.AdrModeTicks) / 60.0), st.Levies.Sum(l => l.Hits),
				run.Gear, run.Arena, st.Levies.Count, CrPct(st.TgtLevy, st.TgtLevy + st.TgtPlayer + st.TgtNone), CrPct(st.TgtPlayer, st.TgtLevy + st.TgtPlayer + st.TgtNone), CrPct(st.TgtNone, st.TgtLevy + st.TgtPlayer + st.TgtNone),
				CrPct(st.AddTgtLevy, st.AddTgtLevy + st.AddTgtPlayer + st.AddTgtNone), CrPct(st.AddTgtPlayer, st.AddTgtLevy + st.AddTgtPlayer + st.AddTgtNone), st.HitsBoss, f1(st.HitsBoss / Math.Max(1.0, end / 60.0)), f1(dmgBoss / Math.Max(1.0, end / 60.0)),
				st.DmgHead, st.DmgBody, st.DmgTail, st.DmgAdd, f1(st.Moved / 16.0), f1(st.EnragedTicks / 60.0), st.FirstHitTick < 0 ? "-" : f1(st.FirstHitTick / 60.0), st.MaxParts,
				CrPct(st.DtdTicks, st.BossAliveTicks), f1(dmgBoss / Math.Max(1.0, (st.BossAliveTicks - st.DtdTicks) / 60.0)), st.AddKills, f1(st.AddKills / Math.Max(1.0, end / 60.0)),
				string.Join(" ", st.Phase.Select(kv => $"a{kv.Key}:{kv.Value.ticks / 60.0:0}s/{kv.Value.dmg}d={(kv.Value.dmg / Math.Max(1.0, kv.Value.ticks / 60.0)):0}")), string.Join(" ", st.Srcs.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + "=" + kv.Value)), per }));
			File.AppendAllText(_crOut, sb + "\n");
			if (SoldierNPC.HitDbg != null) { try { File.AppendAllText(_crOut + ".hit.tsv", run.Key + "\t" + string.Join(" ", SoldierNPC.HitDbg.OrderByDescending(kv => kv.Value.dmg).Select(kv => $"{kv.Key}={kv.Value.dmg}/{kv.Value.n}")) + "\n"); } catch { } SoldierNPC.HitDbg = new(); }
			if (SoldierNPC.HitGapDbg != null) { try { File.AppendAllText(_crOut + ".gap.tsv", run.Key + "\t" + string.Join(" ", SoldierNPC.HitGapDbg.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}")) + "\n"); } catch { } SoldierNPC.HitGapDbg = new(); }
			Mod.Logger.Info($"HARNESS-CR: RESULT {run.Key} -> {st.Result} t={end / 60.0:0.0}s bossLeft={bossLeft}/{st.BossLifeMax} dmg={dmgBoss} deaths={deaths}/{st.Levies.Count} takenBoss={tB} takenAdd={tA} rageAct={LevyRippers.RageActivations - st.Rage0} adrAct={LevyRippers.AdrenalineActivations - st.Adr0}");
			if (Main.npc[st.BossIdx].active && Main.npc[st.BossIdx].type == st.BossType) Main.npc[st.BossIdx].active = false;
			_cr = null; _crS = null; _crStarted = false; _crI++;
			CrResetDowned(); CrSetHpScale(null); CrSetFear(null);
			if (run.Diag) global::Levy.Common.Potions.SpectatorDiagnostic.Set(false);
			ClearAll(); CrPurgeNpcs();
		}

		private void CrAppendError(CrRun run, string note)
		{
			var cols = CrHeader.Split('\t').Length;
			var v = new string[cols]; for (int i = 0; i < cols; i++) v[i] = "0";
			v[0] = run.Key; v[1] = run.Kind; v[2] = run.Boss; v[3] = run.Mode; v[4] = run.Order; v[5] = run.Seed.ToString(); v[6] = "ERROR"; v[cols - 1] = note;
			File.AppendAllText(_crOut, string.Join("\t", v) + "\n");
		}
	}

	/// <summary>Credits a soldier's projectile hits on the boss / its adds (melee swings are credited by the strike detour).</summary>
	public class CrProjWatch : GlobalProjectile
	{
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			var s = CombatHarness.CrLive;
			if (s == null || !CombatHarness.CrIsBossOrAdd(target.type)) return;
			int idx = -1;
			if (projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0) idx = tag.SoldierIndex;
			else if (projectile.type == ModContent.ProjectileType<SoldierMinion>()) idx = (int)projectile.ai[0];
			s.Credit(idx, target.type, damageDone, "proj");
		}
	}

	/// <summary>Harness-only proposal knob: every incoming hit on a levy is cut by this fraction (a stand-in for a dodge / damage-reduction patch).</summary>
	public class CrDrGlobal : GlobalNPC
	{
		internal static float Dr;
		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) { if (Dr > 0f && CombatHarness.CrOn && npc.ModNPC is SoldierNPC) modifiers.FinalDamage *= 1f - Dr; }
	}

	/// <summary>Keeps the stand-in commander a live player for the NPC pass of a calrev run, and unhooks on unload.</summary>
	public class CalRevSystem : ModSystem
	{
		public override void PreUpdateNPCs() { if (CombatHarness.Enabled && CombatHarness.CrOn) { Player p = Main.player[0]; if (p.name == "LevyTester") { p.active = true; p.dead = false; } } }
		public override void PostUpdateNPCs() { if (CombatHarness.Enabled && CombatHarness.CrOn) { Player p = Main.player[0]; if (p.name == "LevyTester") { p.active = true; p.dead = false; } } }
		public override void Unload() { try { CombatHarness.CrUnhook(); } catch { } }
	}
}
