using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEST-ONLY (env-gated LEVY_HERPROBE=&lt;out file&gt;, changes no game behaviour): "why do the designer's summoners only carry the Slime Staff". Reproduces her world (Eye of Cthulhu, King Slime and Desert Scourge down,
	/// Calamity's own flag too, pre-Hardmode), every rank mode, an armory stocked directly with summoner armor, a Slime Staff and only modded (Calamity) staves, then the vanilla staves and whips, with fresh levies and levies
	/// already holding a Slime Staff. Logs per staff: family, issuable, rank breakdown, score; per run: what each levy got. Then traces the decision path of one summoner levy.
	/// </summary>
	public class HerCaseProbe : ModSystem
	{
		private static Item Make(string spec) => WeaponChoiceProbe.MakeItem(spec.Contains('/') ? spec : "Vanilla/" + spec);
		private static string Nm(Item it) => it == null || it.IsAir ? "-" : (it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : it.Name);
		private static bool IsAir(Item it) => it == null || it.IsAir;
		private static string Cls(DamageClass c) => c == null ? "none" : c == DamageClass.Melee ? "melee" : c == DamageClass.Ranged ? "ranged" : c == DamageClass.Magic ? "magic" : c == DamageClass.Summon ? "summon" : c.Name;
		private static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

		public override void PostSetupRecipes()
		{
			string outp = Environment.GetEnvironmentVariable("LEVY_HERPROBE");
			if (string.IsNullOrEmpty(outp)) return;
			var sb = new StringBuilder();
			bool oldCm = false;
			try
			{
				oldCm = LevyConfig.Get.ClassMatchIssue;
				MaterialCost.Ensure();
				try { ProgressionTier.EnsureBuilt(s => sb.AppendLine("# " + s)); } catch (Exception e) { sb.AppendLine("# tier build failed: " + e.Message); }
				sb.AppendLine("# Levy her-case probe. Levy " + Mod.Version + ". Mods: " + string.Join(", ", ModLoader.Mods.Select(m => m.Name).Where(n => n != "ModLoader")));
				SetWorld(sb);
				Run(sb);
			}
			catch (Exception e) { sb.AppendLine("PROBE EXCEPTION: " + e); }
			finally { try { LevyConfig.Get.ClassMatchIssue = oldCm; } catch { } WeaponRank.ForceProgress = -1f; WeaponRank.Release(); LevyClock.Force(null); ArmoryTE.TestServed = null; ArmoryTE.TestOthers = null; }
			try { File.WriteAllText(outp, sb.ToString()); Mod.Logger.Info("HERPROBE: done -> " + outp); } catch (Exception e) { Mod.Logger.Warn("HERPROBE: could not write: " + e.Message); }
		}

		private static void SetWorld(StringBuilder sb)
		{
			NPC.downedSlimeKing = true; NPC.downedBoss1 = true; Main.hardMode = false;
			bool cal = false;
			try
			{
				var m = ModLoader.GetMod("CalamityMod");
				var t = m?.Code.GetType("CalamityMod.DownedBossSystem");
				var f = t?.GetField("downedDesertScourge", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				var pr = t?.GetProperty("downedDesertScourge", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (f != null) { f.SetValue(null, true); cal = (bool)f.GetValue(null); }
				else if (pr != null) { pr.SetValue(null, true); cal = (bool)pr.GetValue(null); }
				else sb.AppendLine("# calamity DS flag member not found: " + string.Join(",", (t?.GetMembers(BindingFlags.Public | BindingFlags.Static) ?? new MemberInfo[0]).Where(x => x.Name.Contains("Desert")).Select(x => x.Name)));
			}
			catch (Exception e) { sb.AppendLine("# calamity flag failed: " + e.Message); }
			WeaponRank.ForceProgress = -1f; WeaponRank.ForceStage = -1;
			Bump(10);
			sb.AppendLine($"# world: KS={NPC.downedSlimeKing} EoC={NPC.downedBoss1} Calamity DS={cal} hardMode={Main.hardMode}  => WeaponRank.Progress()={WeaponRank.Progress()} Stage()={WeaponRank.Stage()}");
		}

		private static void Bump(uint n)
		{
			var f = typeof(Main).GetField("_gameUpdateCount", BindingFlags.NonPublic | BindingFlags.Static);
			f.SetValue(null, Main.GameUpdateCount + n);
		}

		private static string RankLine(Item it)
		{
			var b = WeaponRank.Explain(it);
			var p = WeaponClassifier.GetProfile(it);
			return $"{Nm(it),-44} fam={p.Family,-8} cls={Cls(WeaponClassifier.ItemDamageClass(it)),-6} dmg={it.damage,3} rare={it.rare,2} value={it.value,8} score={WeaponClassifier.Score(it),6:F0} | key={b.Key:F0} base={b.Base:F1} tier={b.Tier:F2} gap={b.Gap:F2} stage={b.Stage:F3} found={b.Found:F2} recency={b.Recency:F2} rules={b.Rules:F2} final={b.Final:F1}";
		}

		private static bool Issuable(Item it) => (bool)typeof(ArmoryTE).GetMethod("IsIssuable", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { it });

		private static void Run(StringBuilder sb)
		{
			WeaponRank.Force(WeaponRankMode.Balanced, false);
			LevyClock.Force(10 * LevyClock.TicksPerDay);
			var slime = Make("SlimeStaff");
			sb.AppendLine("\n## CATALOGUE of summon-class weapons the world has reached (tier <= 6 or unknown tier & rarity <= Orange). Slime Staff first.");
			sb.AppendLine("  " + RankLine(slime) + " issuable=" + Issuable(slime));
			var modded = new List<Item>(); var vanilla = new List<Item>();
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.damage <= 0 || it.accessory || it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0 || it.ammo > 0 || it.createTile >= 0 || it.pick > 0 || it.axe > 0 || it.hammer > 0) continue;
				if (!it.CountsAsClass(DamageClass.Summon)) continue;
				float tier = ProgressionTier.Of(t);
				if (tier > 6f) continue;
				if (tier < 0f && it.rare > ItemRarityID.Orange) continue;
				if (t == ItemID.SlimeStaff) continue;
				(it.ModItem != null ? modded : vanilla).Add(it);
			}
			foreach (var it in modded.Concat(vanilla))
			{
				var p = WeaponClassifier.GetProfile(it);
				sb.AppendLine("  " + RankLine(it) + $" issuable={Issuable(it)} useStyle={it.useStyle} shoot={it.shoot} mana={it.mana} buff={it.buffType} sentry={it.sentry} supported={p.Supported}");
			}

			var summonArmor = new List<Item>();
			var armorClasses = new StringBuilder();
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.vanity || (it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0)) continue;
				float tier = ProgressionTier.Of(t);
				if (tier > 4f || (tier < 0f && it.rare > ItemRarityID.Orange)) continue;
				var c = ArmorDoctrine.PreferredClass(it.headSlot >= 0 ? it : null, it.bodySlot >= 0 ? it : null, it.legSlot >= 0 ? it : null);
				if (c == DamageClass.Summon) summonArmor.Add(it);
				if (it.ModItem != null) armorClasses.AppendLine($"    {Nm(it),-44} tier={tier:F2} piece-class={Cls(c)} defense={it.defense}");
			}
			sb.AppendLine("\n## ARMOR DOCTRINE (pre-Hardmode pieces classified Summon by themselves): " + string.Join(", ", summonArmor.Select(Nm)));
			sb.AppendLine("  modded pre-HM armor pieces and their detected class:\n" + armorClasses);

			var modStaves = modded.Where(it => WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon && !it.sentry).OrderByDescending(WeaponClassifier.Score).ToList();
			var vanStaves = new List<Item>();
			foreach (var n in new[] { "FinchStaff", "FlinxStaff", "VampireFrogStaff", "HornetStaff", "ImpStaff" }) { var it = Make(n); if (it != null) vanStaves.Add(it); }
			var whips = new List<Item>();
			foreach (var n in new[] { "BlandWhip", "ThornWhip", "BoneWhip" }) { var it = Make(n); if (it != null) whips.Add(it); }
			sb.AppendLine("\n## STOCK. modded staves (" + modStaves.Count + "): " + string.Join(", ", modStaves.Select(Nm)));
			sb.AppendLine("   vanilla staves: " + string.Join(", ", vanStaves.Select(Nm)) + "; whips: " + string.Join(", ", whips.Select(Nm)));

			var modes = new[] { WeaponRankMode.Balanced, WeaponRankMode.SellPrice, WeaponRankMode.SellPricePlusMaterials };
			foreach (var mode in modes)
				foreach (bool plusVanilla in new[] { false, true })
					foreach (bool oldStock in new[] { false, true })
						foreach (bool holdingSlime in new[] { false, true })
						{
							string name = $"mode={mode} stock={(plusVanilla ? "modded+vanilla staves+whips" : "modded only")} age={(oldStock ? "all old" : "all fresh")} levies={(holdingSlime ? "already hold a Slime Staff" : "fresh")}";
							try { Case(sb, name, mode, plusVanilla, oldStock, holdingSlime, summonArmor, modStaves, vanStaves, whips); }
							catch (Exception e) { sb.AppendLine("\n# " + name + " THREW " + e); }
						}
			try { LevyConfig.Get.ClassMatchIssue = true; Case(sb, "ClassMatchIssue=TRUE mode=Balanced stock=modded+vanilla staves+whips age=all fresh levies=fresh", WeaponRankMode.Balanced, true, false, false, summonArmor, modStaves, vanStaves, whips); }
			finally { LevyConfig.Get.ClassMatchIssue = false; }
		}

		private static void Case(StringBuilder sb, string name, WeaponRankMode mode, bool plusVanilla, bool oldStock, bool holdingSlime, List<Item> armor, List<Item> modStaves, List<Item> vanStaves, List<Item> whips)
		{
			WeaponRank.Force(mode, false);
			LevyClock.Force(10 * LevyClock.TicksPerDay);
			sb.AppendLine("\n################ " + name);
			var te = new ArmoryTE();
			ArmoryTE.TestOthers = null;
			void Put(Item src) { var c = src.Clone(); c.stack = 1; te.Deposit(c); }
			foreach (var n in new[] { "CopperBroadsword", "CopperBroadsword", "CopperBow", "CopperBow", "AmethystStaff", "TopazStaff" }) { var it = Make(n); if (it != null) te.Deposit(it); }
			var arrows = Make("WoodenArrow"); arrows.stack = 999; te.Deposit(arrows);
			Put(Make("SlimeStaff")); Put(Make("SlimeStaff"));
			foreach (var s in modStaves) Put(s);
			if (plusVanilla) { foreach (var s in vanStaves) Put(s); foreach (var w in whips) Put(w); }
			foreach (var a in armor) Put(a);
			foreach (var a in new[] { "BeeHeadgear", "BeeBreastplate", "BeeGreaves", "ObsidianHelm", "ObsidianShirt", "ObsidianPants" }) { var it = Make(a); if (it != null) te.Deposit(it); }
			long now = LevyClock.Now;
			foreach (var arr in new[] { te.Storage, te.IssueWeapons }) foreach (var it in arr) if (!IsAir(it)) DepositStamp.Set(it, oldStock ? now - 3 * LevyClock.TicksPerDay : now);
			var cb = te.Rules.Blocks[0];
			foreach (var k in new[] { "melee", "ranged", "magic", "summon" }) cb.EntryFor(k, true).Min = 2;
			te.Rules.Rev++;

			var levies = new List<SoldierRecord>();
			for (int i = 0; i < 12; i++)
			{
				var r = new SoldierRecord { Id = i + 1, Name = "L" + (i + 1) };
				if (holdingSlime && i % 2 == 0) r.Gear.Weapon = Make("SlimeStaff");
				levies.Add(r);
			}
			ArmoryTE.TestServed = levies;
			Bump(200);
			if (name.StartsWith("mode=Balanced stock=modded+vanilla staves+whips age=all fresh levies=fresh")) Trace(sb, te, levies[0]);
			foreach (var l in levies) { Bump(1); te.IssueGear(l); }
			Report(sb, "after IssueGear", levies);
			for (int round = 1; round <= 4; round++) foreach (var l in levies) { Bump(1); te.UpgradeWeapons(l); }
			Report(sb, "after 4 upgrade rounds", levies);
			ArmoryTE.TestServed = null;
		}

		private static void Report(StringBuilder sb, string title, List<SoldierRecord> levies)
		{
			var byCls = levies.GroupBy(l => Cls(l.Gear.HasWeapon ? WeaponClassifier.ItemDamageClass(l.Gear.Weapon) : null)).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Count());
			sb.AppendLine($"  [{title}] primary classes: {string.Join(" ", byCls)}");
			foreach (var l in levies)
			{
				var worn = ArmorDoctrine.PreferredClass(l.Gear.Head, l.Gear.Body, l.Gear.Legs);
				bool summ = Cls(worn) == "summon" || (l.Gear.HasWeapon && WeaponClassifier.ItemDamageClass(l.Gear.Weapon) == DamageClass.Summon);
				if (summ) sb.AppendLine($"    L{l.Id,-2} armor={Cls(worn),-6} [{Nm(l.Gear.Head)}/{Nm(l.Gear.Body)}] primary={Nm(l.Gear.Weapon)} sidearm={Nm(l.Gear.Sidearm)}");
			}
		}

		private static void Trace(StringBuilder sb, ArmoryTE te, SoldierRecord who)
		{
			sb.AppendLine("  -- TRACE (fresh levy L1, nothing worn, before IssueGear)");
			using var _rs = ArmoryRules.Enter(te.Rules);
			var tp = typeof(ArmoryTE);
			bool summ = (bool)tp.GetMethod("IsSummonerSoldier", NP).Invoke(te, new object[] { who });
			bool staffAvail = (bool)tp.GetMethod("StaffAvailableFor", NP).Invoke(te, new object[] { who });
			var rule = (Func<Item, bool>)tp.GetMethod("PrimaryRule", NP).Invoke(te, new object[] { who, false });
			sb.AppendLine($"     IsSummonerSoldier={summ} StaffAvailableFor={staffAvail} PrimaryRule(asSummoner=false) null={rule == null}");
			var rule2 = (Func<Item, bool>)tp.GetMethod("PrimaryRule", NP).Invoke(te, new object[] { who, true });
			foreach (var arr in new[] { te.IssueWeapons, te.Storage })
				foreach (var it in arr)
				{
					if (IsAir(it) || WeaponClassifier.GetProfile(it).Family != WeaponFamily.Summon) continue;
					sb.AppendLine($"     {RankLine(it)} issuable={Issuable(it)} rule={(rule == null ? "n/a" : rule(it).ToString())} ruleAsSummoner={(rule2 == null ? "n/a" : rule2(it).ToString())}");
				}
		}
	}
}
