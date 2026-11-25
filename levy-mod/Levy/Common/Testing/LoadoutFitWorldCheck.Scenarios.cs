using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	public partial class LoadoutFitWorldCheck
	{
		private static readonly string[] AllClasses = { "melee", "ranged", "magic", "summon", "rogue" };
		private static readonly string[] VanillaClasses = { "melee", "ranged", "magic", "summon" };

		// ------------------------------------------------------------------ Calamity: Eve's own army and stock

		private static Dictionary<string, int> _byName;

		/// <summary>An item type by its English display name (vanilla first, then mods), 0 when nothing loaded has that name.</summary>
		private static int ByName(string name)
		{
			if (string.IsNullOrEmpty(name)) return 0;
			if (_byName == null)
			{
				_byName = new Dictionary<string, int>();
				for (int t = 1; t < ItemLoader.ItemCount; t++) { string n = Lang.GetItemNameValue(t); if (!string.IsNullOrEmpty(n) && !_byName.ContainsKey(n)) _byName[n] = t; }
			}
			return _byName.TryGetValue(name, out int v) ? v : 0;
		}

		private sealed class Kit { public string Rec; public int Id; public string W, S, H, B, L, Wings, Boots; public string[] Acc; }

		private static List<Kit> Kits() => EveKits.Select(r => r.Split('|')).Select(p => new Kit { Rec = p[0], Id = int.Parse(p[1]), W = p[2], S = p[3], H = p[4], B = p[5], L = p[6], Acc = p[7].Length == 0 ? new string[0] : p[7].Split(';'), Wings = p[8], Boots = p[9] }).ToList();

		private static Item MakeN(string name, HashSet<string> missing) { int t = ByName(name); if (t == 0) { if (!string.IsNullOrEmpty(name)) missing.Add(name); return new Item(); } return Make(t); }

		/// <summary>Puts a recorded kit on a levy (fresh items; the caller takes the same items out of the stock it deposits).</summary>
		private static void Dress(SoldierRecord l, Kit k, HashSet<string> missing)
		{
			var g = l.Gear = new SoldierGear();
			g.Weapon = MakeN(k.W, missing); g.Sidearm = MakeN(k.S, missing);
			g.Head = MakeN(k.H, missing); g.Body = MakeN(k.B, missing); g.Legs = MakeN(k.L, missing);
			for (int i = 0; i < k.Acc.Length && i < g.Accessories.Length; i++) g.Accessories[i] = MakeN(k.Acc[i], missing);
			g.Wings = MakeN(k.Wings, missing); g.Boots = MakeN(k.Boots, missing);
		}

		private static IEnumerable<string> KitNames(Kit k) => new[] { k.W, k.S, k.H, k.B, k.L, k.Wings, k.Boots }.Concat(k.Acc).Where(n => !string.IsNullOrEmpty(n));

		private static void StockAmmo(ArmoryTE te)
		{
			var missing = new HashSet<string>();
			foreach (var n in new[] { "God Slayer Slug", "Mini Nuke I", "Chlorophyte Bullet", "Holy Arrow", "Luminite Bullet", "Luminite Arrow", "Rocket I", "Gel", "Crystal Dart", "Stake", "Snowball", "Candy Corn" })
				for (int c = 0; c < 3; c++) { var it = MakeN(n, missing); if (IsAir(it)) break; it.stack = it.maxStack; te.Deposit(it); }
		}

		private static void Calamity(Scene s)
		{
			var te = s.Armory;
			// the class-coherence passes below are about class, not about the Chalice: its last-resort rule (ArmoryTE.ChaliceDownside) would swap it out of her recorded kits one slot a pass, which is its own check (ChaliceCensus)
			bool chaliceRuleWas = ArmoryTE.ChaliceScoreOn; ArmoryTE.ChaliceScoreOn = false;
			WeaponRank.ForceProgress = 22.5f; // after Yharon, before Supreme Calamitas (she fights the Exo Mechs)
			var kits = Kits();
			var missing = new HashSet<string>();
			foreach (var k in kits) foreach (var n in KitNames(k)) if (ByName(n) == 0) missing.Add(n);
			Note($"her recorded kits: {kits.Count} from {kits.Select(k => k.Rec).Distinct().Count()} recordings, stock {EveStock.Length} item types; names not loaded here (skipped): {(missing.Count == 0 ? "none" : string.Join(", ", missing))}");

			// ---- what each of her accessories is for (the class reading the armory scores with)
			var accNames = kits.SelectMany(k => k.Acc).Distinct().OrderBy(x => x).ToList();
			foreach (var n in accNames) { int t = ByName(n); if (t > 0) Note($"accessory {n}: [{AccKey(Make(t))}] {ArmorDoctrine.DebugAccessory(Make(t))}"); }
			int eclipse = ByName("Eclipse Mirror");
			Check("Eclipse Mirror (+6% rogue damage and crit, stealth) reads as a rogue accessory, not a generic one", eclipse > 0 && ArmorDoctrine.AccessoryClasses(Make(eclipse)).Any(c => Key(c) == "rogue"));

			// ---- 1. her recorded kits as they are (every recording with 40+ levies): how many show the reported mix
			{
				Reset(s, 0); Balance(te, 8, 20, 2f, AllClasses);
				var tmp = new SoldierRecord { Id = 97999, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "tmp" };
				var byRec = new Dictionary<string, (int n, int off, int side, int armor, int acc)>();
				foreach (var k in kits)
				{
					Dress(tmp, k, missing);
					string off = Off(s, tmp);
					var c = byRec.TryGetValue(k.Rec, out var v) ? v : (0, 0, 0, 0, 0);
					byRec[k.Rec] = (c.Item1 + 1, c.Item2 + (off != "" ? 1 : 0), c.Item3 + (off.Contains('S') ? 1 : 0), c.Item4 + (off.Contains('A') ? 1 : 0), c.Item5 + (off.Contains('C') ? 1 : 0));
					if (off.Length >= 2 || k.Rec == kits[kits.Count - 1].Rec) Note($"  recorded {k.Rec} " + Row(s, tmp, "levy " + k.Id));
				}
				foreach (var kv in byRec) Note($"recorded army {kv.Key}: {kv.Value.n} levies, {kv.Value.off} incoherent (sidearm {kv.Value.side}, armor {kv.Value.armor}, class accessory {kv.Value.acc})");
			}

			// ---- 2. her last recorded army, worn as recorded, the rest of her stock in the armory, her rules: ONE upgrade pass makes every kit coherent
			string last = kits[kits.Count - 1].Rec;
			var army = kits.Where(k => k.Rec == last).ToList();
			Reset(s, army.Count);
			var worn = new Dictionary<string, int>();
			for (int i = 0; i < army.Count; i++) { Dress(s.Levies[i], army[i], missing); foreach (var n in KitNames(army[i])) worn[n] = (worn.TryGetValue(n, out int w) ? w : 0) + 1; }
			foreach (var row in EveStock)
			{
				var p = row.Split('|'); int t = ByName(p[0]); if (t == 0) continue;
				int left = int.Parse(p[1]) - (worn.TryGetValue(p[0], out int w) ? w : 0);
				if (left > 0) Stock(te, left, t);
			}
			StockAmmo(te);
			Balance(te, 8, 20, 2f, AllClasses);
			Table(s, $"Calamity, her army {last} as recorded (before any pass)");
			var census = Census(s);
			Upgrades(s, 1);
			Table(s, "Calamity, her army after ONE upgrade pass");
			Check($"her recorded army is coherent after one upgrade pass: primary, sidearm, armor and class accessories share one class wherever her stock allows ({Fixable(s, out var fx1)} not:{fx1})", Fixable(s, out _) == 0);
			Check($"no levy wears another class's armor while a levy of that class wears armor not his own ({MisplacedArmor(s, out var ma1)}:{ma1})", MisplacedArmor(s, out _) == 0);
			Note($"{Incoherent(s)} levies in all are off; armor no trade can fix (her stock has fewer sets of the class than levies): {Unfixable(s)}");
			Check("nothing duplicated or lost by the upgrade pass (every item counted) " + (SameCensus(census, Census(s), out var d1) ? "" : d1), SameCensus(census, Census(s), out _));
			var rc = ClassCounts(s);
			Check($"her recorded army keeps her rules after one upgrade pass: every class at least 8 and at most 20 ({Counts(rc)})", AllClasses.All(c => rc.TryGetValue(c, out int n) && n >= 8 && n <= 20));
			var snap = Snapshot(s);
			for (int pass = 2; pass <= 3; pass++)
			{
				var before = Active(s).ToDictionary(l => l, l => Row(s, l));
				if (Environment.GetEnvironmentVariable("LEVY_LOADOUTFIT_TRACE") == "1")
				{
					foreach (var req in Active(s).ToList())
					{
						var b0 = Active(s).ToDictionary(l => l, l => Row(s, l));
						Tick(); s.Armory.UpgradeWeapons(req);
						foreach (var l in Active(s)) if (b0[l] != Row(s, l)) Note($"  trace pass {pass} requester {req.Id}: {l.Id} was [{b0[l]}] now [{Row(s, l)}]");
					}
				}
				else Upgrades(s, 1);
				var moved = Active(s).Where(l => before[l] != Row(s, l)).ToList();
				Note($"pass {pass}: {moved.Count} kits changed; class counts {Counts(ClassCounts(s))}");
				foreach (var l in moved.Take(8)) Note($"  was {before[l]} || now {Row(s, l)}");
			}
			Check("two more upgrade passes change nothing (no thrash)", Snapshot(s) == snap);
			Check($"no class accessory sits on another class while a levy of its class could wear it ({MisplacedClassAccessories(s, out var ex1)}:{ex1})", MisplacedClassAccessories(s, out _) == 0);
			Note("class counts after the passes: " + Counts(ClassCounts(s)));

			// ---- 3. a fresh Re-arm of 45 levies from her whole stock, her rules
			Reset(s, 45);
			foreach (var row in EveStock) { var p = row.Split('|'); int t = ByName(p[0]); if (t > 0) Stock(te, int.Parse(p[1]), t); }
			StockAmmo(te);
			Balance(te, 8, 20, 2f, AllClasses);
			census = Census(s);
			Rearm(s);
			Table(s, "Calamity, 45 levies, fresh Re-arm from her stock");
			var cc = ClassCounts(s);
			Note("class counts after the Re-arm: " + Counts(cc));
			Check($"a fresh Re-arm from her stock is coherent wherever her stock allows ({Fixable(s, out var fx2)} not:{fx2})", Fixable(s, out _) == 0);
			Check($"no levy wears another class's armor while a levy of that class wears armor not his own, after the Re-arm ({MisplacedArmor(s, out var ma2)}:{ma2})", MisplacedArmor(s, out _) == 0);
			Note($"{Incoherent(s)} levies in all are off; armor no trade can fix: {Unfixable(s)}");
			Check($"the class counts keep her rules: every class at least 8 and at most 20 ({Counts(cc)})", AllClasses.All(c => cc.TryGetValue(c, out int n) && n >= 8 && n <= 20));
			Check("nothing duplicated or lost by the Re-arm " + (SameCensus(census, Census(s), out var d2) ? "" : d2), SameCensus(census, Census(s), out _));
			Upgrades(s, 1);
			Check($"still coherent after an upgrade pass ({Fixable(s, out var fx3)} not:{fx3})", Fixable(s, out _) == 0);
			snap = Snapshot(s); Upgrades(s, 2);
			Check("two more upgrade passes change nothing after the Re-arm (no thrash)", Snapshot(s) == snap);
			Check($"no class accessory sits on another class while a levy of its class could wear it after the Re-arm ({MisplacedClassAccessories(s, out var ex2)}:{ex2})", MisplacedClassAccessories(s, out _) == 0);
			Check("nothing duplicated or lost by the Re-arm and its passes " + (SameCensus(census, Census(s), out var d3) ? "" : d3), SameCensus(census, Census(s), out _));
			try { ChaliceCensus(s); } catch (Exception e) { Check("the Chalice census threw " + e.GetType().Name + ": " + e.Message, false); }
			ArmoryTE.ChaliceScoreOn = chaliceRuleWas;
			WeaponRank.ForceProgress = -1f;
		}

		/// <summary>
		/// COMBATFIX: how often the armory hands out Calamity's Chalice of the Blood God on a typical endgame armory (her post-Yharon stock and rules, 45 levies, a fresh Re-arm and two upgrade passes), with the Chalice rule off
		/// (BEFORE: it scores by rarity like any top accessory) and on (NOW: levies bleed out its hits, so it is dealt last). The stock holds 1, 6 and 45 Chalices, so a rule that only matters when the supply is short shows too.
		/// </summary>
		private static void ChaliceCensus(Scene s)
		{
			int chalice = ByName("Chalice of the Blood God");
			if (chalice <= 0) { Note("the Chalice of the Blood God is not loaded: census skipped"); return; }
			var te = s.Armory;
			bool saveRule = ArmoryTE.ChaliceScoreOn; bool saveForce = global::Levy.Content.NPCs.SoldierNPC.BleedoutForce;
			var rows = new List<string>(); var issued = new Dictionary<(bool, int), int>();
			try
			{
				global::Levy.Content.NPCs.SoldierNPC.BleedoutForce = true; // the setting's default is on; the check does not depend on the config file
				foreach (int copies in new[] { 1, 6, 45 })
				foreach (bool rule in new[] { false, true })
				{
					ArmoryTE.ChaliceScoreOn = rule;
					Reset(s, 45);
					foreach (var row in EveStock) { var p = row.Split('|'); int t = ByName(p[0]); if (t <= 0) continue; Stock(te, t == chalice ? copies : int.Parse(p[1]), t); }
					StockAmmo(te); Balance(te, 8, 20, 2f, AllClasses);
					Rearm(s); Upgrades(s, 2);
					int worn = 0; foreach (var l in Active(s)) foreach (var a in l.Gear.Accessories) if (!IsAir(a) && a.type == chalice) worn++;
					issued[(rule, copies)] = worn;
					rows.Add($"stock {copies}: {(rule ? "NOW" : "BEFORE")} {worn} worn");
				}
			}
			finally { ArmoryTE.ChaliceScoreOn = saveRule; global::Levy.Content.NPCs.SoldierNPC.BleedoutForce = saveForce; }
			Note("Chalice census, 45 levies, her stock and rules: " + string.Join("; ", rows));
			Check($"the Chalice rule: with a short supply (1) it is issued {issued[(false, 1)]} times before and {issued[(true, 1)]} now; with 6 {issued[(false, 6)]} before and {issued[(true, 6)]} now; with 45 {issued[(false, 45)]} before and {issued[(true, 45)]} now (never more than before)", issued[(true, 1)] <= issued[(false, 1)] && issued[(true, 6)] <= issued[(false, 6)] && issued[(true, 45)] <= issued[(false, 45)]);
			Check($"the Chalice rule: it is dealt less often than before on at least one supply ({issued[(false, 6)]} to {issued[(true, 6)]}, {issued[(false, 45)]} to {issued[(true, 45)]})", issued[(true, 6)] < issued[(false, 6)] || issued[(true, 45)] < issued[(false, 45)] || issued[(true, 1)] < issued[(false, 1)]);
		}

		// ------------------------------------------------------------------ vanilla: the same shape, post Moon Lord

		private static readonly int[] Solar = { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings };
		private static readonly int[] Vortex = { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings };
		private static readonly int[] Nebula = { ItemID.NebulaHelmet, ItemID.NebulaBreastplate, ItemID.NebulaLeggings };
		private static readonly int[] Stardust = { ItemID.StardustHelmet, ItemID.StardustBreastplate, ItemID.StardustLeggings };
		private static readonly int[] MeleeAcc = { ItemID.WarriorEmblem, ItemID.FireGauntlet, ItemID.BerserkerGlove };
		private static readonly int[] RangedAcc = { ItemID.RangerEmblem, ItemID.MagicQuiver, ItemID.ReconScope };
		private static readonly int[] MagicAcc = { ItemID.SorcererEmblem, ItemID.ManaFlower, ItemID.CelestialEmblem };
		private static readonly int[] SummonAcc = { ItemID.SummonerEmblem, ItemID.PapyrusScarab, ItemID.NecromanticScroll };
		private static readonly int[] GenericAcc = { ItemID.DestroyerEmblem, ItemID.CelestialShell, ItemID.AnkhShield, ItemID.CharmofMyths, ItemID.WormScarf, ItemID.StarVeil };

		private static void StockLunar(ArmoryTE te)
		{
			Stock(te, 6, Solar); Stock(te, 6, Vortex); Stock(te, 6, Nebula); Stock(te, 6, Stardust);
			Stock(te, 2, ItemID.Meowmere, ItemID.StarWrath, ItemID.TerraBlade, ItemID.InfluxWaver, ItemID.Terrarian, ItemID.DayBreak);
			Stock(te, 2, ItemID.SDMG, ItemID.VortexBeater, ItemID.Phantasm, ItemID.ChainGun, ItemID.Megashark, ItemID.Tsunami);
			Stock(te, 2, ItemID.LunarFlareBook, ItemID.NebulaBlaze, ItemID.NebulaArcanum, ItemID.LastPrism, ItemID.RazorbladeTyphoon, ItemID.MagnetSphere);
			Stock(te, 2, ItemID.StardustDragonStaff, ItemID.StardustCellStaff, ItemID.RainbowWhip, ItemID.EmpressBlade, ItemID.MoonlordTurretStaff, ItemID.RainbowCrystalStaff);
			Stock(te, 2, MeleeAcc); Stock(te, 2, RangedAcc); Stock(te, 2, MagicAcc); Stock(te, 2, SummonAcc);
			Stock(te, 4, GenericAcc);
			foreach (var _ in Enumerable.Range(0, 4)) { te.Deposit(Make(ItemID.MoonlordBullet, 999)); te.Deposit(Make(ItemID.MoonlordArrow, 999)); te.Deposit(Make(ItemID.ChlorophyteBullet, 999)); te.Deposit(Make(ItemID.HolyArrow, 999)); }
		}

		/// <summary>Moves accessory slot <paramref name="ia"/> of <paramref name="a"/> and slot <paramref name="ib"/> of <paramref name="b"/> between them (the census is unchanged).</summary>
		private static void SwapAcc(SoldierRecord a, int ia, SoldierRecord b, int ib) { var x = a.Gear.Accessories[ia]; a.Gear.Accessories[ia] = b.Gear.Accessories[ib]; b.Gear.Accessories[ib] = x; }

		private static void Vanilla(Scene s)
		{
			var te = s.Armory;
			WeaponRank.ForceProgress = 18.5f;
			foreach (var t in MeleeAcc.Concat(RangedAcc).Concat(MagicAcc).Concat(SummonAcc).Concat(GenericAcc)) Note($"accessory {Make(t).Name}: [{AccKey(Make(t))}] {ArmorDoctrine.DebugAccessory(Make(t))}");
			Reset(s, 20); StockLunar(te); Balance(te, 4, 8, 2f, VanillaClasses);
			var census = Census(s);
			Rearm(s);
			Table(s, "vanilla, 20 levies, post-ML Re-arm");
			var cc = ClassCounts(s);
			Check($"vanilla Re-arm is coherent ({Incoherent(s)} not:{IncoherentList(s)})", Incoherent(s) == 0);
			Check($"vanilla class counts keep the rules: every class at least 4 and at most 8 ({Counts(cc)})", VanillaClasses.All(c => cc.TryGetValue(c, out int n) && n >= 4 && n <= 8));
			Check($"vanilla: no class accessory on another class while a levy of its class could wear it ({MisplacedClassAccessories(s, out var ex0)}:{ex0})", MisplacedClassAccessories(s, out _) == 0);
			Upgrades(s, 1);
			Check($"vanilla still coherent after an upgrade pass ({Incoherent(s)} not:{IncoherentList(s)})", Incoherent(s) == 0);

			// ---- an incoherent army (the report's shape): class accessories swapped across classes, a blade on a ranger's belt; one upgrade pass
			var byCls = VanillaClasses.ToDictionary(c => c, c => Active(s).Where(l => Key(WClass(s, l)) == c).ToList());
			int scrambled = 0;
			for (int k = 0; k < 4; k++)
			{
				var a = byCls["melee"].ElementAtOrDefault(k); var b = byCls["magic"].ElementAtOrDefault(k); var c = byCls["summon"].ElementAtOrDefault(k); var r = byCls["ranged"].ElementAtOrDefault(k);
				if (a != null && b != null) { SwapAcc(a, 0, b, 0); scrambled++; }
				if (c != null && r != null) { SwapAcc(c, 1, r, 1); scrambled++; }
			}
			foreach (var r in byCls["ranged"].Take(3))
			{
				for (int i = 0; i < te.Storage.Length; i++)
				{
					var it = te.Storage[i];
					if (IsAir(it) || !(it.type == ItemID.TerraBlade || it.type == ItemID.InfluxWaver || it.type == ItemID.StarWrath)) continue;
					te.Storage[i] = r.Gear.Sidearm; r.Gear.Sidearm = it; scrambled++; break;
				}
			}
			te.MarkDirty();
			Table(s, $"vanilla, scrambled ({scrambled} swaps)");
			int before = Incoherent(s);
			Upgrades(s, 1);
			Table(s, "vanilla, scrambled army after ONE upgrade pass");
			Check($"an incoherent vanilla army ({before} levies) is coherent after one upgrade pass ({Incoherent(s)} not:{IncoherentList(s)})", before > 0 && Incoherent(s) == 0);
			Check("vanilla: nothing duplicated or lost " + (SameCensus(census, Census(s), out var d1) ? "" : d1), SameCensus(census, Census(s), out _));
			var snap = Snapshot(s); Upgrades(s, 2);
			Check("vanilla: two more upgrade passes change nothing (no thrash)", Snapshot(s) == snap);

			// ---- a ranger in a summoner's armor (hand-assigned, so it cannot follow his gun): still a ranger, so no whip or staff in his hand slot, a gun there
			Reset(s, 1); StockLunar(te);
			var lone = s.Levies[0]; var lg = lone.Gear;
			lg.Head = Make(Stardust[0]); lg.Body = Make(Stardust[1]); lg.Legs = Make(Stardust[2]);
			lg.SetLocked(SoldierGear.SlotHead, true); lg.SetLocked(SoldierGear.SlotBody, true); lg.SetLocked(SoldierGear.SlotLegs, true);
			lg.Weapon = Make(ItemID.SniperRifle); lg.SetLocked(SoldierGear.SlotWeapon, true); lg.Ammo = Make(ItemID.MoonlordBullet, 999);
			census = Census(s);
			Tick(); te.IssueGear(lone); Upgrades(s, 1);
			Note("a ranger in locked Stardust armor: " + Row(s, lone));
			Check("a ranger in a summoner's armor (locked) is no summoner: his hand sidearm is a ranged weapon, not a whip or a staff", !IsAir(lg.Sidearm) && WClass(s, lone) == DamageClass.Ranged && IClass(s, lg.Sidearm) == DamageClass.Ranged);
			Check("vanilla: nothing duplicated or lost (ranger in Stardust) " + (SameCensus(census, Census(s), out var d4) ? "" : d4), SameCensus(census, Census(s), out _));

			// ---- a strong class accessory goes to its class first: two Sorcerer Emblems and six weak generic accessories for 8 levies with 16 slots (the shelf runs dry, so a last resort is taken)
			Reset(s, 8); StockLunar(te);
			for (int i = 0; i < te.Storage.Length; i++) if (!IsAir(te.Storage[i]) && te.Storage[i].accessory && !AccessoryMobility.Is(te.Storage[i])) te.Storage[i] = new Item();
			Stock(te, 2, ItemID.SorcererEmblem, ItemID.Shackle, ItemID.BandofRegeneration, ItemID.CrossNecklace);
			Balance(te, 2, 2, 1f, VanillaClasses);
			census = Census(s);
			Rearm(s);
			Table(s, "vanilla, two Sorcerer Emblems among six weak generic accessories, 8 levies");
			var holders = Active(s).Where(l => l.Gear.Accessories.Any(a => !IsAir(a) && a.type == ItemID.SorcererEmblem)).ToList();
			Note("Sorcerer Emblem holders after the Re-arm: " + string.Join(", ", holders.Select(l => Key(WClass(s, l)) + " levy " + l.Id)));
			Check("both Sorcerer Emblems go to the two mages (a strong class accessory reaches its class first, though non-mages were issued before them and the shelf ran dry)", holders.Count == 2 && holders.All(l => WClass(s, l) == DamageClass.Magic));
			Check("vanilla: nothing duplicated or lost (scarce emblem) " + (SameCensus(census, Census(s), out var d2) ? "" : d2), SameCensus(census, Census(s), out _));
			// the same emblems already on a melee and a ranged levy: one upgrade pass hands them to the mages
			var mages = Active(s).Where(l => WClass(s, l) == DamageClass.Magic).ToList();
			var others = Active(s).Where(l => WClass(s, l) == DamageClass.Melee || WClass(s, l) == DamageClass.Ranged).Take(2).ToList();
			if (mages.Count == 2 && others.Count == 2) for (int k = 0; k < 2; k++)
			{
				int mi = Array.FindIndex(mages[k].Gear.Accessories, a => !IsAir(a) && a.type == ItemID.SorcererEmblem);
				if (mi >= 0) SwapAcc(mages[k], mi, others[k], 0);
			}
			Table(s, "vanilla, the emblems moved onto a melee and a ranged levy");
			Upgrades(s, 1);
			holders = Active(s).Where(l => l.Gear.Accessories.Any(a => !IsAir(a) && a.type == ItemID.SorcererEmblem)).ToList();
			Note("Sorcerer Emblem holders after one upgrade pass: " + string.Join(", ", holders.Select(l => Key(WClass(s, l)) + " levy " + l.Id)));
			Check("one upgrade pass gives the emblems back to the mages", holders.Count == 2 && holders.All(l => WClass(s, l) == DamageClass.Magic));
			Check("vanilla: nothing duplicated or lost (emblems traded back) " + (SameCensus(census, Census(s), out var d3) ? "" : d3), SameCensus(census, Census(s), out _));
			WeaponRank.ForceProgress = -1f;
		}
	}
}
