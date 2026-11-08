using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;

namespace Levy.Common.Testing
{
	/// <summary>
	/// DEATH LADDER LAB (measurement only; spec token dl=A|B|C|D on a calrev fight line, optional dlok=1). Death-mode Eater of Worlds against the gear ladder:
	///   A = pre-evil (every boss before EoW/BoC in Boss Checklist order: King Slime, Eye, Desert Scourge, Crabulon), B = post EoW/BoC, C = post Skeletron, D = early Hardmode (just after the Wall of Flesh).
	/// Best arm (default): piercing / explosive / damage-over-time weapons of the tier, best legal ammo, 400 max HP, 5 accessory slots (mobility first), full potion set, all tier stations, honey.
	/// dlok=1: the okay-ish arm (what a normal player has): vanilla weapons of the tier at equal weight, plain ammo, 2 accessory slots, Ironskin + Regeneration + Well Fed only, tier-curve HP.
	/// Levy count is the real progression count for the tier (LevyTierMath). Extra row per fight in out.dl.tsv.
	/// </summary>
	public partial class CombatHarness
	{
		private static int _dlTier = -1; private static int _dlSavedSlots = -1;
		private static int DlIdx(string dl) => dl switch { "A" => 0, "B" => 1, "C" => 2, _ => 3 };

		/// <summary>Levy count: Levy's own tier math with the bosses beaten at that point (Boss Checklist positions as CrRealMax).</summary>
		internal static int DlCount(string dl, bool ok)
		{
			float furthest = DlIdx(dl) switch { 0 => 2.7f, 1 => 3f, 2 => 5f, _ => 7f };
			float[] prog = { 1, 2, 3, 4, 5, 6, 7 }; int[] w = { 1, 2, 2, 1, 2, 1, 7 };
			var l = new List<TierBoss>();
			for (int i = 0; i < 7; i++) l.Add(new TierBoss(0, w[i], prog[i] <= furthest, prog[i]));
			foreach (float mp in new[] { 1.6f, 2.7f, 3.98f, 3.99f, 6.7f }) l.Add(new TierBoss(0, 0f, mp <= furthest, mp));
			return LevyTierMath.Compute(l, 10, 20, 40, 60, 60);
		}

		private static int DlStationLevel(CrRun r) { _dlTier = DlIdx(r.Dl); return _dlTier switch { 0 => 1, 1 => 2, _ => 3 }; }

		private static readonly HashSet<string> DlExplosive = new() { "Rocket Launcher", "Grenade Launcher", "Magic Missile", "Flamelash", "Space Gun", "CalamityMod/Eviscerator", "CalamityMod/Galeforce", "CalamityMod/Aorta", "CalamityMod/BloodBath" };
		private static readonly HashSet<string> DlDot = new() { "Flamelash", "Fiery Greatsword", "Blade of Grass", "Cursed Flames", "Golden Shower", "CalamityMod/HyphaeRod", "CalamityMod/Fungicide", "CalamityMod/Shadethrower", "CalamityMod/PlagueStaff", "Vilethorn", "Night's Edge", "Molten Fury", "Hellwing Bow", "Bee Gun", "CalamityMod/ShaderainStaff" };
		/// <summary>Reporting class of a weapon+ammo (INFERRED labels: pierce from the projectile sample, explosive/DoT from these hand lists and the ammo).</summary>
		internal static string DlClass(string weapon, string ammo)
		{
			var tags = new List<string>();
			if (L3Pierces(weapon, (ammo ?? "").Split('|')[0]) || ammo == "Jester's Arrow") tags.Add("P");
			if (DlExplosive.Contains(weapon) || ammo == "Hellfire Arrow") tags.Add("X");
			if (DlDot.Contains(weapon) || ammo == "Hellfire Arrow") tags.Add("D");
			return tags.Count == 0 ? "-" : string.Join("", tags);
		}

		private static readonly SortedSet<string> DlMissing = new();
		private static bool DlKnown(string n) { if (CrTryItemId(n, out _)) return true; DlMissing.Add(n); return false; }
		private static string[] DlSet(params string[] a) => a;

		private static List<(string w, string ammo)> DlBestWeapons(char cls, int t)
		{
			var l = new List<(string, string)>();
			void W(string w, string a = "") => l.Add((w, a));
			if (cls == 'R')
			{
				W("Minishark", "bul"); W("Phoenix Blaster", "bul"); W("Boomstick", "bul"); W("Demon Bow", "arr"); W("CalamityMod/Fungicide", "bul"); W("CalamityMod/StormSurge"); W("CalamityMod/Barinade", "arr");
				if (t >= 1) { W("The Undertaker", "bul"); W("Handgun", "bul"); W("Space Gun"); }
				if (t >= 2) { W("CalamityMod/Shadethrower", "gel"); W("CalamityMod/Eviscerator", "bul"); W("CalamityMod/Galeforce", "arr"); W("Molten Fury", "arr"); W("Bee Gun"); }
				if (t >= 3) { W("Cobalt Repeater", "arr"); W("Palladium Repeater", "arr"); W("Mythril Repeater", "arr"); W("Orichalcum Repeater", "arr"); W("Clockwork Assault Rifle", "bul"); W("CalamityMod/OverloadedBlaster", "bul"); }
			}
			else if (cls == 'M')
			{
				W("Flamelash"); W("Magic Missile"); W("Aqua Scepter"); W("CalamityMod/HyphaeRod"); W("CalamityMod/SandstreamScepter"); W("CalamityMod/AquamarineStaff");
				if (t >= 1) { W("Vilethorn"); W("Space Gun"); }
				if (t >= 2) { W("CalamityMod/BloodBath"); W("CalamityMod/ShaderainStaff"); W("CalamityMod/Tradewinds"); W("CalamityMod/PlagueStaff"); }
				if (t >= 3) { W("Cursed Flames"); W("Crystal Storm"); W("Magic Dagger"); W("CalamityMod/AbyssalTome"); W("CalamityMod/EldritchTome"); }
			}
			else if (cls == 'E')
			{
				W("CalamityMod/MycelialClaws"); W("CalamityMod/TeardropCleaver"); W("Fiery Greatsword"); W("Blade of Grass"); W("CalamityMod/SeashineSword"); W("Starfury");
				if (t >= 1) { W("Light's Bane"); W("Blood Butcherer"); }
				if (t >= 2) { W("Muramasa"); W("CalamityMod/SausageMaker"); W("CalamityMod/Aorta"); W("CalamityMod/WindBlade"); W("Night's Edge"); }
			}
			else
			{
				W("CalamityMod/BrittleStarStaff"); W("CalamityMod/DeathstareRod"); W("CalamityMod/WulfrumController"); W("Imp Staff");
				if (t >= 2) { W("CalamityMod/FleshOfInfidelity"); W("Hornet Staff"); W("CalamityMod/DankStaff"); }
			}
			return l.Where(x => DlKnown(x.Item1)).ToList();
		}

		private static List<(string w, string ammo)> DlOkWeapons(char cls, int t)
		{
			var pool = (t >= 2 ? CrPoolPost : CrPoolPost)[cls].Select(x => (x.w, x.ammo)).ToList();
			if (t >= 2) { if (cls == 'R') { pool.Add(("Bee Gun", "")); pool.Add(("Handgun", "Silver Bullet")); } if (cls == 'M') { pool.Add(("Water Bolt", "")); pool.Add(("Aqua Scepter", "")); } if (cls == 'E') { pool.Add(("Muramasa", "")); pool.Add(("Bee Keeper", "")); } if (cls == 'S') pool.Add(("Hornet Staff", "")); }
			return pool.Where(x => DlKnown(x.w)).ToList();
		}

		private static List<string[]> DlArmor(char cls, int t, bool ok)
		{
			var l = new List<string[]>();
			if (ok) { foreach (var a in CrArmorPost[cls]) l.Add(a); if (t >= 2) l.Add(CrBee); return l; }
			string h = cls == 'R' ? "Ranged" : cls == 'M' ? "Magic" : cls == 'E' ? "Melee" : "Summon";
			if (t == 0)
			{
				l.Add(CrVictide(cls)); l.Add(CrVictide(cls));
				if (cls == 'R') { l.Add(CrFossil); l.Add(CrNinja); } else if (cls == 'M') l.Add(CrJungle); else l.Add(CrPlat);
			}
			else if (t == 1)
			{
				l.Add(CrVictide(cls));
				foreach (var a in CrArmorPost[cls]) l.Add(a);
				if (cls == 'M') l.Add(CrMeteor);
			}
			else if (t == 2)
			{
				l.Add(CrAerospec(cls)); l.Add(CrAerospec(cls)); l.Add(CrBee);
				if (cls == 'R') l.Add(CrSet("Necro Helmet", "Necro Breastplate", "Necro Greaves"));
				foreach (var a in CrArmorPost[cls]) l.Add(a);
			}
			else
			{
				if (cls == 'R') { l.Add(CrSet("Mythril Hood", "Mythril Chainmail", "Mythril Greaves")); l.Add(CrSet("Orichalcum Mask", "Orichalcum Breastplate", "Orichalcum Leggings")); l.Add(CrSet("Palladium Mask", "Palladium Breastplate", "Palladium Leggings")); }
				else if (cls == 'M') { l.Add(CrSet("Mythril Hat", "Mythril Chainmail", "Mythril Greaves")); l.Add(CrSet("Orichalcum Headgear", "Orichalcum Breastplate", "Orichalcum Leggings")); l.Add(CrSet("Cobalt Hat", "Cobalt Breastplate", "Cobalt Leggings")); }
				else { l.Add(CrSet("Mythril Helmet", "Mythril Chainmail", "Mythril Greaves")); l.Add(CrSet("Orichalcum Helmet", "Orichalcum Breastplate", "Orichalcum Leggings")); l.Add(CrSet("Palladium Helmet", "Palladium Breastplate", "Palladium Leggings")); }
				l.Add(CrAerospec(cls));
			}
			return l.Where(s => s.All(n => DlKnown(n))).ToList();
		}

		private static string[] DlAcc(char cls, int t, bool ok)
		{
			string classAcc = cls == 'E' ? "Feral Claws" : cls == 'M' ? "Band of Starpower" : cls == 'R' ? "Magic Quiver" : "Hermes Boots";
			string[] l;
			if (ok) l = new[] { "Hermes Boots", "Band of Regeneration", "Shackle", classAcc };
			else if (t == 0) l = new[] { "Cloud in a Bottle", "Shield of Cthulhu", classAcc, "Band of Regeneration", "Hermes Boots", "CalamityMod/SandCloak" };
			else if (t == 1) l = new[] { "Cloud in a Bottle", "Shield of Cthulhu", classAcc, "Band of Regeneration", "Hermes Boots", "CalamityMod/SandCloak", "CalamityMod/FungalClump" };
			else if (t == 2) l = new[] { "Cloud in a Bottle", "Shield of Cthulhu", classAcc, "Cobalt Shield", "Band of Regeneration", "Hermes Boots", "CalamityMod/SandCloak" };
			else l = new[] { "Angel Wings", "Cloud in a Bottle", "Shield of Cthulhu", classAcc, "Cobalt Shield", "Band of Regeneration", "Hermes Boots" };
			return l.Where(n => DlKnown(n)).Distinct().ToArray();
		}

		private static string DlAmmoFor(Item w, int t, bool ok, int idx)
		{
			if (w.useAmmo == AmmoID.Gel) return "Gel";
			if (w.useAmmo == AmmoID.Bullet) return ok ? "Silver Bullet" : (t >= 1 && CrTryItemId("Meteor Shot", out _) ? "Meteor Shot" : "Silver Bullet");
			if (w.useAmmo == AmmoID.Arrow)
			{
				if (ok) return t >= 2 ? "Hellfire Arrow" : "Flaming Arrow";
				if (t >= 1 && idx % 2 == 0 && CrTryItemId("Hellfire Arrow", out _)) return "Hellfire Arrow"; // explosive + DoT arm
				return "Jester's Arrow"; // piercing arm
			}
			return "";
		}

		internal static List<CrLevySpec> DlRoll(CrRun r)
		{
			int t = DlIdx(r.Dl); bool ok = r.DlOk;
			var rng = new Random(r.Seed * 7919 + 9100 + t * 31 + (ok ? 7 : 0));
			var list = new List<CrLevySpec>(); int total = r.N;
			double[] fr = ok ? new[] { .3, .3, .2, .2 } : new[] { .4, .4, .1, .1 };
			int nr = (int)Math.Round(total * fr[0]), nm = (int)Math.Round(total * fr[1]), ne = (int)Math.Round(total * fr[2]), ns = Math.Max(0, total - nr - nm - ne);
			int rIdx = 0;
			foreach (var (cls, n) in new[] { ('R', nr), ('M', nm), ('E', ne), ('S', ns) })
				for (int k = 0; k < n; k++)
				{
					var pool = ok ? DlOkWeapons(cls, t) : DlBestWeapons(cls, t);
					var wl = pool.Where(x => !CrExcluded.Contains(x.w)).Select(x =>
					{
						int wt = 1;
						if (!ok && (L3Pierces(x.w, "") || DlExplosive.Contains(x.w) || DlDot.Contains(x.w) || x.ammo == "arr")) wt = 3;
						return (x.w, x.ammo, wt);
					}).ToList();
					int sum = wl.Sum(x => x.wt), pick = rng.Next(sum), a0 = 0; var chosen = wl[0];
					foreach (var x in wl) { a0 += x.wt; if (pick < a0) { chosen = x; break; } }
					var armors = DlArmor(cls, t, ok); var armor = armors[rng.Next(armors.Count)];
					string[] acc = DlAcc(cls, t, ok); int slots = ok ? 2 : 5; acc = acc.Take(slots).ToArray();
					int lc = ok ? (t == 1 ? 7 : 12) : 15;
					int mc = ok ? 9 : 9;
					string pot = ok ? (t >= 2 ? "Healing Potion" : "Lesser Healing Potion") : (t >= 3 ? "Greater Healing Potion" : t >= 1 ? "Healing Potion" : "Lesser Healing Potion");
					if (!CrTryItemId(pot, out _)) pot = "Healing Potion";
					var sp = new CrLevySpec { Cls = cls, Weapon = chosen.w, Ammo = "", Armor = armor, Acc = acc, LifeCrystals = lc, ManaCrystals = mc, ManAtArms = false, Potions = ok ? rng.Next(3, 6) : 8, Potion = pot };
					try { Item it = CrMake(sp.Weapon); if (it.useAmmo != AmmoID.None) sp.Ammo = DlAmmoFor(it, t, ok, rIdx++); } catch { }
					list.Add(sp);
				}
			var chi = list.FirstOrDefault(x => x.Cls == 'M'); if (!ok && chi != null && CrTryItemId("CalamityMod/TrinketofChi", out _)) chi.Acc = chi.Acc.Take(Math.Max(0, 5 - 1)).Concat(new[] { "CalamityMod/TrinketofChi" }).ToArray();
			return list;
		}

		private static readonly int[] DlBestBuffs = { BuffID.Ironskin, BuffID.Regeneration, BuffID.Endurance, BuffID.Swiftness, BuffID.Wrath, BuffID.Rage, BuffID.MagicPower, BuffID.Archery, BuffID.WellFed2, BuffID.AmmoReservation, BuffID.ManaRegeneration };
		private static readonly int[] DlOkBuffs = { BuffID.Ironskin, BuffID.Regeneration, BuffID.WellFed };
		private static void DlBuffs(CrRun run, NPC n) { foreach (int b in run.DlOk ? DlOkBuffs : DlBestBuffs) n.AddBuff(b, 60 * 60 * 30); if (!_dlBuffLogged) { _dlBuffLogged = true; var names = new List<string>(); for (int i = 0; i < NPC.maxBuffs; i++) if (n.buffType[i] > 0) names.Add(n.buffType[i] + ":" + n.buffTime[i]); global::Levy.Levy.Instance.Logger.Info("HARNESS-DL: buffs after AddBuff: " + string.Join(",", names)); } }
		private static bool _dlBuffLogged;

		private void DlBegin(CrRun run)
		{
			var c = global::Levy.Common.Config.LevyConfig.Get;
			if (_dlSavedSlots < 0) _dlSavedSlots = c.LevyAccessorySlots;
			c.LevyAccessorySlots = run.DlOk ? 2 : 5;
			Mod.Logger.Info("HARNESS-DL: missing item names so far: " + string.Join(", ", DlMissing));
			Mod.Logger.Info($"HARNESS-DL: begin {run.Key} tier {run.Dl} ok={run.DlOk} n={run.N} accSlots={c.LevyAccessorySlots} stations level {DlStationLevel(run)} ; classes {string.Join(",", run.Specs.Select(sp => sp.Cls + ":" + sp.Weapon.Replace("CalamityMod/", "C:") + "/" + sp.Ammo + "[" + DlClass(sp.Weapon, sp.Ammo) + "]"))}");
		}
		private static void DlEnd() { if (_dlSavedSlots >= 0) global::Levy.Common.Config.LevyConfig.Get.LevyAccessorySlots = _dlSavedSlots; }

		private void DlFinish(CrRun run, CrState st)
		{
			try
			{
				string path = _crOut + ".dl.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\ttier\tok\tboss\tseed\tresult\tn\tavgHp\taccSlotsAvg\tbuffsOnLevy\tdeathTimes\tdeathBy\tclassDmg\tweaponDmg\tarmorAcc\n");
				string f1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
				var times = st.Levies.Where(l => l.Dead).Select(l => l.DeathTick / 60.0).OrderBy(x => x).ToList();
				var cd = new Dictionary<string, long>(); var wd = new Dictionary<string, long>();
				foreach (var l in st.Levies) { string c = DlClass(l.Spec.Weapon, l.Spec.Ammo); cd.TryGetValue(c, out long v); cd[c] = v + l.DmgBoss; wd.TryGetValue(l.Weapon, out long u); wd[l.Weapon] = u + l.DmgBoss; }
				string accAvg = f1(st.Levies.Average(l => (double)l.Spec.Acc.Length));
				string buffs = ""; var first = st.Levies.FirstOrDefault(); if (first != null) { var n = Main.npc[first.Idx]; var names = new List<string>(); for (int i = 0; i < NPC.maxBuffs; i++) if (n.buffType[i] > 0 && n.buffTime[i] > 0) names.Add(n.buffType[i] < BuffID.Count ? Lang.GetBuffName(n.buffType[i]) : "mod"); buffs = string.Join("+", names); }
				File.AppendAllText(path, string.Join("\t", new object[] { run.Key, run.Dl, run.DlOk ? 1 : 0, run.Boss, run.Seed, st.Result, st.Levies.Count, f1(st.Levies.Average(l => l.MaxHp)), accAvg, buffs, string.Join(",", times.Select(f1)),
					string.Join(" ", st.DeathBy.OrderByDescending(kv => kv.Value).Select(kv => kv.Key.Replace(' ', '_') + "=" + kv.Value)), string.Join(" ", cd.Select(kv => kv.Key + "=" + kv.Value)),
					string.Join(" ", wd.OrderByDescending(kv => kv.Value).Select(kv => kv.Key.Replace("CalamityMod/", "C:").Replace(' ', '_') + "=" + kv.Value)),
					string.Join(" ; ", st.Levies.Take(6).Select(l => string.Join("+", l.Spec.Armor.Take(1)) + "|" + string.Join("+", l.Spec.Acc))) }) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-DL: finish row failed: " + e.Message); }
		}
	}
}
