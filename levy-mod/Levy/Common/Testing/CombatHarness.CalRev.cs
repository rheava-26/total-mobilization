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

		internal sealed class CrLevySpec
		{
			public char Cls; public string Weapon, Ammo; public string[] Armor = Array.Empty<string>(), Acc = Array.Empty<string>();
			public int LifeCrystals, ManaCrystals, Potions; public string Potion = ""; public bool ManAtArms;
			public int Hp => 100 + 20 * LifeCrystals; public int Mana => 20 + 20 * ManaCrystals;
			public string Describe() => $"{Cls}:{Weapon}{(string.IsNullOrEmpty(Ammo) ? "" : "/" + Ammo)}|{string.Join("+", Armor.Select(a => a.Split(' ')[0]).Distinct())}|{(Acc.Length == 0 ? "-" : string.Join("+", Acc))}|{(ManAtArms ? "MaA" : "Lv")}|{Hp}hp/{Mana}mp|{Potions}x{Potion}";
		}

		internal sealed class CrRun
		{
			public string Kind, Boss = "eye", Mode = "exp", Order = "free"; public int Seed, Cap; public List<CrLevySpec> Specs; public string Desc = "";
			public string Key => $"{Kind}|{Boss}|{Mode}|{Order}|{Seed}|{Cap}";
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
					r = new CrRun { Kind = "fight", Boss = f[1], Mode = f[2], Seed = int.Parse(f[3]), Cap = int.Parse(f[4]), Order = f.Length > 5 && f[5].StartsWith("order=") ? f[5].Substring(6) : (int.Parse(f[3]) % 2 == 1 ? "free" : "sd") };
					r.Specs = CrRoll(r.Boss, r.Seed); r.Desc = string.Join(" ; ", r.Specs.Select(sp => sp.Describe()));
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
		private static List<CrLevySpec> CrRoll(string boss, int seed)
		{
			bool post = CrPost(boss);
			var rng = new Random(seed * 7919 + (post ? 5 : 0) + 17);
			var list = new List<CrLevySpec>();
			var pool = post ? CrPoolPost : CrPoolPre; var arm = post ? CrArmorPost : CrArmorPre;
			foreach (var (cls, n) in new[] { ('R', 3), ('M', 3), ('E', 2), ('S', 2) })
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
			CrParseSpec(spec);
			if (!_crHooked)
			{
				_crHooked = true;
				On_NPC.StrikeNPC_HitInfo_bool_bool += CrStrikeDetour;
				On_NPC.UpdateNPC += CrNpcUpdateDetour;
				On_Projectile.Update += CrProjUpdateDetour;
			}
			_crI = 0; _crStarted = false;
			return true;
		}

		internal static void CrUnhook()
		{
			if (!_crHooked) return;
			_crHooked = false;
			try { On_NPC.StrikeNPC_HitInfo_bool_bool -= CrStrikeDetour; On_NPC.UpdateNPC -= CrNpcUpdateDetour; On_Projectile.Update -= CrProjUpdateDetour; } catch { }
		}

		// -------------------------------------------------------------------------------------------------- Calamity switches
		private static System.Reflection.FieldInfo _crRevField, _crDeathField;
		private static void CrSetRevenge(bool on)
		{
			if (_crRevField == null)
			{
				Type w = ModLoader.GetMod("CalamityMod").Code.GetType("CalamityMod.World.CalamityWorld");
				_crRevField = w.GetField("revenge", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				_crDeathField = w.GetField("death", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			}
			_crRevField.SetValue(null, on);
			_crDeathField?.SetValue(null, false);
		}

		// -------------------------------------------------------------------------------------------------- attribution hooks
		private static int _crNpcCtx = -1, _crProjDepth;
		private static CrState _cr;
		internal static CrState CrLive => _cr;
		internal static bool CrIsBossOrAdd(int type) => _cr != null && (type == _cr.BossType || _cr.AddTypes.Contains(type));

		private static void CrNpcUpdateDetour(On_NPC.orig_UpdateNPC orig, NPC self, int i)
		{
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
			public CrRun Run; public List<CrLevy> Levies = new();
			public int BossType; public HashSet<int> AddTypes = new();
			public int Tick; public int BossIdx = -1; public int BossLifeMax; public int BossLife; public int BossDamage, BossDefense;
			public int AddsPeak; public int HalfTick = -1; public bool BossGone; public int EndTick = -1; public string Result = "";
			public long DmgUnattributed, ProjDmg, MeleeDmg;
			public int Rage0, Adr0, Dr0, Lost0;
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
		private int CrF, _crCx; private string _crArenaBuilt = "";
		private void CrBuildArena()
		{
			float n = Main.maxTilesX / 4200f; n *= n;
			int band = (int)(60f + 10f * n + Main.worldSurface / 6.0) + 2;
			CrF = Math.Clamp(band + 75, 100, Main.maxTilesY - 120);
			_crCx = _cx;
			int cx = _cx, fy = CrF, half = 110;
			FaFill(cx - 130, fy - 90, cx + 130, fy - 1, false);
			FaFill(cx - 130, fy, cx + 130, fy + 25, true);
			FaFill(cx - half - 1, fy - 90, cx - half - 1, fy + 25, true);
			FaFill(cx + half + 1, fy - 90, cx + half + 1, fy + 25, true);
			for (int k = 1; k <= 4; k++)
			{
				int py = fy - 7 * k;
				for (int x = cx - half + 6; x <= cx + half - 6; x++)
				{
					Tile t = Main.tile[x, py];
					t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.WallType = 0;
				}
			}
			_crArenaBuilt = "big";
			Mod.Logger.Info($"HARNESS-CR: arena built: floor row {fy}, half-width {half}, 4 platform rows");
		}

		private Vector2 _crCmdPos;
		private void CrPinCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.team = 0;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			p.position = new Vector2(_crCmdPos.X - p.width / 2f, _crCmdPos.Y - p.height);
			p.velocity = Vector2.Zero; p.immune = true; p.immuneTime = 30;
			int bt = ModContent.BuffType<SpectatorBuff>();
			if (!p.HasBuff(bt)) { p.AddBuff(bt, 600000); if (p.TryGetModPlayer(out SpectatorPlayer sp)) sp.Anchor = p.Center; }
		}

		// -------------------------------------------------------------------------------------------------- step
		private CrState _crS;
		private int _crTickInRun;
		private void StepCalRev()
		{
			if (_crI < 0) return;
			if (_crI >= _crRuns.Count) { try { File.WriteAllText(_crOut + ".done", "1"); } catch { } Finish("calrev complete"); return; }
			CrRun run = _crRuns[_crI];
			Main.dayTime = false; Main.time = 16200; Main.raining = false; Main.bloodMoon = false; Main.eclipse = false;
			try
			{
				if (_crArenaBuilt == "" && !_crStarted) CrBuildArena();
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
				if (i == keep || (_cr != null && _cr.AddTypes.Contains(n.type)) || (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget)) continue;
				n.active = false;
			}
		}

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
			if (!gear.CanHold(SoldierGear.SlotWeapon, w)) throw new Exception("levy cannot hold " + spec.Weapon);
			gear.Weapon = w; gear.SetLocked(SoldierGear.SlotWeapon, true);
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

		private static int CrBossNpcType(string boss) => boss switch { "kslime" => NPCID.KingSlime, "brain" => NPCID.BrainofCthulhu, "eow" => NPCID.EaterofWorldsHead, _ => NPCID.EyeofCthulhu };

		private static void CrApplyMode(string mode)
		{
			Main.GameMode = GameModeID.Expert;
			bool rev = mode == "rev" || mode == "revoff";
			CrSetRevenge(rev);
			LevyRippers.Disabled = mode != "rev";
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
				CrApplyMode(run.Mode);
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
				NPC.downedBoss1 = false; NPC.downedBoss2 = false; NPC.downedSlimeKing = false;
				_crCmdPos = new Vector2((_crCx - 6) * 16f, CrF * 16f);
				CrPinCommander();
				var s = new CrState { Run = run, BossType = CrBossNpcType(run.Boss) };
				if (run.Boss == "kslime") { foreach (int t in new[] { NPCID.BlueSlime, NPCID.SlimeSpiked, NPCID.GreenSlime, NPCID.PurpleSlime, NPCID.RedSlime, NPCID.YellowSlime, NPCID.IceSlime, NPCID.JungleSlime, NPCID.BlackSlime }) s.AddTypes.Add(t); }
				else if (run.Boss == "brain") s.AddTypes.Add(NPCID.Creeper);
				else s.AddTypes.Add(NPCID.ServantofCthulhu);
				int total = run.Specs.Count, slot = 0;
				foreach (var sp in run.Specs) s.Levies.Add(CrSpawnLevy(sp, run, slot++, total, _crCmdPos));
				if (probe) { for (int k = 0; k < s.Levies.Count; k++) s.Levies[k].Upkeep = k >= 2; }
				int bx = (int)((_crCx + 30) * 16f), by = run.Boss == "kslime" ? (int)((CrF - 6) * 16f) : (int)((CrF - 38) * 16f);
				int ei = NPC.NewNPC(new EntitySource_WorldEvent("LevyCalRev"), bx, by, s.BossType);
				if (ei < 0 || ei >= Main.maxNPCs) throw new Exception("boss spawn failed");
				NPC boss = Main.npc[ei]; s.BossIdx = ei;
				if (probe) { boss.lifeMax *= 40; boss.life = boss.lifeMax; }
				s.BossLifeMax = boss.lifeMax; s.BossLife = boss.life; s.BossDamage = boss.damage; s.BossDefense = boss.defense;
				boss.timeLeft = 99999;
				s.Rage0 = LevyRippers.RageActivations; s.Adr0 = LevyRippers.AdrenalineActivations; s.Dr0 = LevyRippers.DrCuts; s.Lost0 = LevyRippers.AdrenalineLost;
				_crS = s; _cr = s; _crStarted = true; _crTickInRun = 0;
				if (probe && !File.Exists(_crOut + ".probe.tsv")) File.WriteAllText(_crOut + ".probe.tsv", "mode\ttick\tlevy\tgear\tupkeep\trage\tadr\trageMode\tadrMode\thp\tboss\n");
				Mod.Logger.Info($"HARNESS-CR: start {run.Key}: {run.Boss} {boss.life}/{boss.lifeMax} def {boss.defense} dmg {boss.damage} revenge={LevyRippers.RevengeanceOn} expert={Main.expertMode} rippersOff={LevyRippers.Disabled}; {run.Desc}");
				return;
			}
			var st = _crS; st.Tick = ++_crTickInRun;
			CrPinCommander();
			CrPinStrays(st.BossIdx);
			NPC e = Main.npc[st.BossIdx];
			bool bossAlive = e.active && e.type == st.BossType;
			if (bossAlive) { st.BossLife = e.life; e.timeLeft = 99999; if (st.HalfTick < 0 && e.life * 2 <= st.BossLifeMax) st.HalfTick = st.Tick; }
			else if (!st.BossGone) { st.BossGone = true; st.EndTick = st.Tick; st.Result = e.life <= 0 ? "WIN" : "DESPAWN"; st.BossLife = Math.Max(0, e.life); }
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
						if (at == st.BossType) l.TakenBoss += drop; else if (st.AddTypes.Contains(at)) l.TakenAdd += drop; else l.TakenOther += drop;
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
					if (at == st.BossType) l.TakenBoss += l.LastLife; else if (st.AddTypes.Contains(at)) l.TakenAdd += l.LastLife; else l.TakenOther += l.LastLife;
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

		private const string CrHeader = "key\tkind\tboss\tmode\torder\tseed\tresult\tticks\tsec\tbossMax\tbossLeft\tbossDmg\tbossDef\tdmgBoss\tdmgR\tdmgM\tdmgE\tdmgS\tdmgUnattr\tdmgProj\tdmgMelee\tdeaths\tdeathsR\tdeathsM\tdeathsE\tdeathsS\tavgSurvSec\ttakenBoss\ttakenAdd\ttakenOther\taddsPeak\thalfSec\tavgHp\tpotionsDrunk\trageAct\tadrAct\tdrCuts\tadrLost\tpeakRageAvg\tpeakAdrAvg\trageSec\tadrSec\thitsTaken\tperLevy";

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
			int bossLeft = st.BossGone ? st.BossLife : Main.npc[st.BossIdx].active ? Main.npc[st.BossIdx].life : st.BossLife;
			var sb = new StringBuilder();
			sb.Append(run.Key).Append('\t').Append(string.Join("\t", new object[] { run.Kind, run.Boss, run.Mode, run.Order, run.Seed, st.Result, end, f1(end / 60.0),
				st.BossLifeMax, bossLeft, st.BossDamage, st.BossDefense, dmgBoss, dmg('R'), dmg('M'), dmg('E'), dmg('S'), st.DmgUnattributed, st.ProjDmg, st.MeleeDmg, deaths, dc('R'), dc('M'), dc('E'), dc('S'), f1(avgSurv), tB, tA, tO, st.AddsPeak,
				st.HalfTick < 0 ? "-" : f1(st.HalfTick / 60.0), f1(st.Levies.Average(l => l.MaxHp)), st.Levies.Sum(l => l.PotionsDrunk),
				LevyRippers.RageActivations - st.Rage0, LevyRippers.AdrenalineActivations - st.Adr0, LevyRippers.DrCuts - st.Dr0, LevyRippers.AdrenalineLost - st.Lost0,
				f1(st.Levies.Average(l => l.PeakRage)), f1(st.Levies.Average(l => l.PeakAdr)), f1(st.Levies.Sum(l => l.RageModeTicks) / 60.0), f1(st.Levies.Sum(l => l.AdrModeTicks) / 60.0), st.Levies.Sum(l => l.Hits), per }));
			File.AppendAllText(_crOut, sb + "\n");
			Mod.Logger.Info($"HARNESS-CR: RESULT {run.Key} -> {st.Result} t={end / 60.0:0.0}s bossLeft={bossLeft}/{st.BossLifeMax} dmg={dmgBoss} deaths={deaths}/{st.Levies.Count} takenBoss={tB} takenAdd={tA} rageAct={LevyRippers.RageActivations - st.Rage0} adrAct={LevyRippers.AdrenalineActivations - st.Adr0}");
			if (Main.npc[st.BossIdx].active && Main.npc[st.BossIdx].type == st.BossType) Main.npc[st.BossIdx].active = false;
			_cr = null; _crS = null; _crStarted = false; _crI++;
			NPC.downedBoss1 = false; NPC.downedBoss2 = false; NPC.downedSlimeKing = false;
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

	/// <summary>Keeps the stand-in commander a live player for the NPC pass of a calrev run, and unhooks on unload.</summary>
	public class CalRevSystem : ModSystem
	{
		public override void PreUpdateNPCs() { if (CombatHarness.Enabled && CombatHarness.CrOn) { Player p = Main.player[0]; if (p.name == "LevyTester") { p.active = true; p.dead = false; } } }
		public override void PostUpdateNPCs() { if (CombatHarness.Enabled && CombatHarness.CrOn) { Player p = Main.player[0]; if (p.name == "LevyTester") { p.active = true; p.dead = false; } } }
		public override void Unload() { try { CombatHarness.CrUnhook(); } catch { } }
	}
}
