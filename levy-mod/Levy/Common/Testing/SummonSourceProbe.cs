using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.MagicStorageLink;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEST-ONLY (env-gated, changes no game behaviour): LEVY_SRCPROBE=&lt;out file&gt;. Answers "why do my summoners only carry the Slime Staff":
	///  1. CLASSIFICATION DUMP of every summon-looking item of every loaded mod (family, class, minion flags), and the ones that classify wrong.
	///  2. SOURCE SCENARIOS: twelve free levies, the armory's class balance set to a minimum of 2 of each class, the armory stocked with 2 of each class (2 Slime Staffs for the summon class), and the better
	///     minion staves placed (a) in the armory, (b) in a networked armory, (c) in a REAL Magic Storage network (an in-memory storage heart, see MsProbeDriver), (d) in Magic Storage with NO staff in the
	///     armory at all. Every levy is issued, then upgraded for several rounds, through the real IssueGear / UpgradeWeapons; what each summoner holds is logged.
	/// </summary>
	public class SummonSourceProbe : ModSystem
	{
		private static Item Make(string spec) => WeaponChoiceProbe.MakeItem(spec.Contains('/') ? spec : "Vanilla/" + spec);
		private static string Nm(Item it) => IsAir(it) ? "-" : (it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : it.Name);
		private static bool IsAir(Item it) => it == null || it.IsAir;
		private static string Cls(DamageClass c) => c == null ? "none" : c == DamageClass.Melee ? "melee" : c == DamageClass.Ranged ? "ranged" : c == DamageClass.Magic ? "magic" : c == DamageClass.Summon ? "summon" : c.Name;

		public override void PostSetupRecipes()
		{
			string outp = Environment.GetEnvironmentVariable("LEVY_SRCPROBE");
			if (string.IsNullOrEmpty(outp)) return;
			var sb = new StringBuilder();
			try
			{
				MaterialCost.Ensure();
				try { ProgressionTier.EnsureBuilt(s => { }); } catch (Exception e) { sb.AppendLine("# tier build failed: " + e.Message); }
				sb.AppendLine("# Levy summon-source probe. Levy " + Mod.Version + ". Mods: " + string.Join(", ", ModLoader.Mods.Select(m => m.Name).Where(n => n != "ModLoader")));
				if (Environment.GetEnvironmentVariable("LEVY_SRCPROBE_DUMP") != "0") Dump(sb);
				if (Environment.GetEnvironmentVariable("LEVY_SRCPROBE_SCEN") != "0") Scenarios(sb);
			}
			catch (Exception e) { sb.AppendLine("PROBE EXCEPTION: " + e); }
			finally { WeaponRank.ForceProgress = -1f; WeaponRank.Release(); LevyClock.Force(null); ArmoryTE.TestServed = null; ArmoryTE.TestOthers = null; try { CloseMsAll(); } catch (Exception) { } }
			try { File.WriteAllText(outp, sb.ToString()); Mod.Logger.Info("SRCPROBE: done -> " + outp); } catch (Exception e) { Mod.Logger.Warn("SRCPROBE: could not write: " + e.Message); }
		}

		// ------------------------------------------------------------------ 1. classification

		private static void Dump(StringBuilder sb)
		{
			sb.AppendLine("\n## CLASSIFICATION of every summon-looking item (damage > 0 and: counts as Summon, or shoots a minion projectile, or shoots with a buff + mana)");
			var perMod = new Dictionary<string, (int staff, int whip, int sentry, int other)>();
			var wrong = new List<string>();
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				Item it = new Item();
				try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.damage <= 0) continue;
				if (it.accessory || it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0 || it.ammo > 0 || it.createTile >= 0 || it.pick > 0 || it.axe > 0 || it.hammer > 0) continue;
				Projectile sample = it.shoot > 0 ? WeaponClassifier.ProjectileSample(it.shoot) : null;
				bool summonClass = it.CountsAsClass(DamageClass.Summon);
				bool minionShot = sample != null && (sample.minion || sample.minionSlots > 0f);
				bool buffShooter = it.buffType > 0 && it.shoot > 0 && it.mana > 0;
				if (!summonClass && !minionShot && !buffShooter) continue;
				var p = WeaponClassifier.GetProfile(it);
				string mod = it.ModItem != null ? it.ModItem.Mod.Name : "Vanilla";
				perMod.TryGetValue(mod, out var c);
				if (p.Family == WeaponFamily.Summon) c.staff++; else if (p.Family == WeaponFamily.Whip) c.whip++; else if (p.Family == WeaponFamily.Sentry) c.sentry++; else c.other++;
				perMod[mod] = c;
				// A minion staff in every sense but the classifier's: it should be Family Summon and is not.
				bool staffLike = !it.sentry && it.shoot > 0 && sample != null && !(sample.aiStyle == ProjAIStyleID.Whip) && !WeaponClassifier_IsWhip(it, sample) && (minionShot || (summonClass && it.buffType > 0));
				if (staffLike && p.Family != WeaponFamily.Summon)
					wrong.Add($"{mod}/{(it.ModItem != null ? it.ModItem.Name : it.Name)}\tfamily={p.Family}\tclass={Cls(WeaponClassifier.ItemDamageClass(it))}\tdamageType={it.DamageType?.Name}\tcountsSummon={summonClass}\tshoot={it.shoot}\tsample.minion={sample?.minion}\tslots={sample?.minionSlots}\tai={sample?.aiStyle}\tbuff={it.buffType}\tmana={it.mana}\tuseStyle={it.useStyle}");
				if (!summonClass && minionShot)
					wrong.Add($"{mod}/{(it.ModItem != null ? it.ModItem.Name : it.Name)}\tMINION SHOT BUT NOT SUMMON CLASS: family={p.Family} class={Cls(WeaponClassifier.ItemDamageClass(it))} damageType={it.DamageType?.Name}");
				if (summonClass && p.Family == WeaponFamily.Summon && (it.shoot <= 0 || WeaponClassifier.GetProfile(it).Family != WeaponFamily.Summon))
					wrong.Add($"{mod}/{it.Name}\tSummon family but not issuable as staff");
			}
			foreach (var kv in perMod.OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key,-18} staves(Family.Summon)={kv.Value.staff,3} whips={kv.Value.whip,3} sentries={kv.Value.sentry,3} summon-looking but another family/unsupported={kv.Value.other,3}");
			sb.AppendLine($"\nMISCLASSIFIED / SUSPECT: {wrong.Count}");
			foreach (var w in wrong) sb.AppendLine("  " + w);
		}

		private static bool WeaponClassifier_IsWhip(Item it, Projectile sample) => ProjectileID.Sets.IsAWhip[it.shoot] || sample.aiStyle == ProjAIStyleID.Whip;

		// ------------------------------------------------------------------ 2. scenarios

		private sealed class Stock
		{
			public List<Item> Armory = new(), Net = new(), Ms = new();
		}

		private static void Bump(uint n)
		{
			var pr = typeof(Main).GetProperty("GameUpdateCount");
			pr.GetSetMethod(true).Invoke(null, new object[] { Main.GameUpdateCount + n });
		}

		private static string[] Names(IEnumerable<Item> items) => items.Select(Nm).ToArray();

		private static List<Item> BetterStaves(StringBuilder sb)
		{
			// the player's real stock: every pre-Hardmode-ish staff that out-damages the Slime Staff, vanilla and modded (by what the loaded mods have)
			var res = new List<Item>();
			foreach (var n in new[] { "FlinxStaff", "VampireFrogStaff", "HornetStaff", "ImpStaff", "SpiderStaff", "PygmyStaff", "OpticStaff" })
			{
				var it = Make(n); if (it != null && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon) res.Add(it);
			}
			var slime = Make("SlimeStaff");
			float slimeScore = WeaponClassifier.Score(slime);
			var modded = new List<Item>();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
			{
				var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.sentry || it.damage <= 0) continue;
				if (WeaponClassifier.GetProfile(it).Family != WeaponFamily.Summon) continue;
				float tier = ProgressionTier.Of(t);
				if (tier > 5f) continue; // pre-Hardmode: up to Skeletron / Deerclops
				if (tier < 0f && it.rare > ItemRarityID.Orange) continue;
				if (WeaponClassifier.Score(it) <= slimeScore) continue;
				modded.Add(it);
			}
			modded = modded.OrderByDescending(WeaponClassifier.Score).Take(8).ToList();
			res.AddRange(modded);
			sb.AppendLine("\n## SCENARIO STOCK. Slime Staff: score " + slimeScore.ToString("F0") + ", value " + slime.value + ", rank " + WeaponRank.Of(slime).ToString("F1"));
			foreach (var it in res) sb.AppendLine($"  better staff {Nm(it),-40} score {WeaponClassifier.Score(it),6:F0} value {it.value,8} tier {ProgressionTier.Of(it.type),5:F1} rank {WeaponRank.Of(it),7:F1}");
			return res;
		}

		private static void Scenarios(StringBuilder sb)
		{
			WeaponRank.Force(WeaponRankMode.Balanced, false);
			WeaponRank.ForceProgress = 5f; // Skeletron down, Hardmode not started
			LevyClock.Force(10 * LevyClock.TicksPerDay);
			var better = BetterStaves(sb);
			if (better.Count == 0) { sb.AppendLine("no better staves found"); return; }
			bool ms = ModLoader.HasMod("MagicStorage") && MagicStorageCompatSystem.Active;
			sb.AppendLine("Magic Storage in this run: " + (ms ? "REAL in-memory storage heart" : "not available"));
			var cases = new List<(string name, bool slimeInArmory, string where)>
			{
				("A: staves in the ARMORY            (2 Slime Staffs + better staves, all in the armory)", true, "armory"),
				("B: staves in a NETWORKED armory    (2 Slime Staffs in the armory)", true, "net"),
				("C: staves in MAGIC STORAGE         (2 Slime Staffs in the armory)", true, "ms"),
				("D: staves in MAGIC STORAGE ONLY    (no staff at all in the armory)", false, "ms"),
				("E: ONE Slime Staff + staves in MS  (class minimum of 2 summoners, 1 staff in the armory)", true, "ms1"),
			};
			foreach (bool legacy in new[] { true, false })
			{
				ArmoryTE.LegacySourceOrder = legacy;
				sb.AppendLine("\n\n########################################## " + (legacy ? "BEFORE (the pick order before the fix: own shelves, then networked armories, Magic Storage only as a last resort)" : "AFTER (every reachable source ranked together)"));
				foreach (var c in cases)
				{
					if (c.where.StartsWith("ms") && !ms) { sb.AppendLine("\n# " + c.name + ": skipped (no Magic Storage)"); continue; }
					try { RunCase(sb, c.name, c.slimeInArmory, c.where, better, ms); }
					catch (Exception e) { sb.AppendLine("\n# " + c.name + " THREW " + e); }
				}
			}
			ArmoryTE.LegacySourceOrder = false;
		}

		private static void Stage(ArmoryTE te, bool slimeInArmory, bool oneSlime)
		{
			void Put(string n, int count = 1, int stack = 1) { for (int i = 0; i < count; i++) { var it = Make(n); if (it == null) continue; it.stack = stack; te.Deposit(it); } }
			if (slimeInArmory) Put("SlimeStaff", oneSlime ? 1 : 2);
			Put("CopperBroadsword", 2); Put("TinBroadsword", 0);
			Put("CopperBow", 2); Put("AmethystStaff", 1); Put("TopazStaff", 1);
			var arrows = Make("WoodenArrow"); arrows.stack = 999; te.Deposit(arrows);
			var rules = te.Rules;
			var cb = rules.Blocks[0];
			foreach (var k in new[] { "melee", "ranged", "magic", "summon" }) cb.EntryFor(k, true).Min = 2;
			rules.Rev++;
		}

		private static void RunCase(StringBuilder sb, string name, bool slimeInArmory, string where, List<Item> better, bool ms)
		{
			sb.AppendLine("\n################ " + name);
			ArmoryTE.TestOthers = null;
			MsProbeScope msScope = null;
			ArmoryTE te; ArmoryTE other = null;
			try
			{
				if (ms) msScope = OpenMs();
				te = msScope != null ? msScope.Armory : new ArmoryTE();
				Stage(te, slimeInArmory, where == "ms1");
				var copies = better.Select(b => { var c = b.Clone(); return c; }).ToList();
				foreach (var c in copies) DepositStamp.Set(c, LevyClock.Now - 3 * LevyClock.TicksPerDay); // crafted three days ago: old
				foreach (var it in te.Storage) if (!IsAir(it) && it.type == ItemID.SlimeStaff) DepositStamp.Set(it, LevyClock.Now); // the Slime Staffs were just put in: fresh
				switch (where)
				{
					case "armory": foreach (var c in copies) te.Deposit(c); break;
					case "net": other = new ArmoryTE(); foreach (var c in copies) other.Deposit(c); ArmoryTE.TestOthers = new List<ArmoryTE> { other }; break;
					case "ms": case "ms1": msScope.Put(copies); break;
				}
				var levies = new List<SoldierRecord>();
				for (int i = 0; i < 12; i++) levies.Add(new SoldierRecord { Id = i + 1, Name = "L" + (i + 1) });
				ArmoryTE.TestServed = levies;
				Bump(200);
				foreach (var l in levies) { Bump(1); te.IssueGear(l); }
				Report(sb, "after IssueGear", levies);
				for (int round = 1; round <= 6; round++)
				{
					foreach (var l in levies) { Bump(1); te.UpgradeWeapons(l); }
					if (round == 1 || round == 6) Report(sb, "after upgrade round " + round, levies);
				}
				var left = new List<string>();
				foreach (var it in te.Storage) if (!IsAir(it) && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon) left.Add(Nm(it));
				sb.AppendLine("  staves still in the armory storage: " + (left.Count == 0 ? "-" : string.Join(", ", left)));
				if (other != null) sb.AppendLine("  staves still in the networked armory: " + string.Join(", ", other.Storage.Where(i => !IsAir(i)).Select(Nm)));
				if (msScope != null) sb.AppendLine("  staves still in Magic Storage: " + string.Join(", ", msScope.Contents().Where(i => WeaponClassifier.GetProfile(i).Family == WeaponFamily.Summon).Select(Nm)));
			}
			finally { ArmoryTE.TestServed = null; ArmoryTE.TestOthers = null; msScope?.Close(); }
		}

		private static void Report(StringBuilder sb, string title, List<SoldierRecord> levies)
		{
			var byCls = levies.GroupBy(l => Cls(l.Gear.HasWeapon ? WeaponClassifier.ItemDamageClass(l.Gear.Weapon) : null)).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Count());
			sb.AppendLine($"  [{title}] primary classes: {string.Join(" ", byCls)}");
			var summ = levies.Where(l => l.Gear.HasWeapon && WeaponClassifier.ItemDamageClass(l.Gear.Weapon) == DamageClass.Summon).ToList();
			sb.AppendLine("    summoners' primary staff: " + (summ.Count == 0 ? "NO SUMMONERS" : string.Join(", ", summ.Select(l => Nm(l.Gear.Weapon)))));
			var side = levies.Where(l => l.Gear.HasSidearm && WeaponClassifier.GetProfile(l.Gear.Sidearm).Family == WeaponFamily.Summon).Select(l => "L" + l.Id + ":" + Nm(l.Gear.Sidearm)).ToList();
			if (side.Count > 0) sb.AppendLine("    staves as sidearm: " + string.Join(", ", side));
		}

		// ------------------------------------------------------------------ Magic Storage plumbing (no Magic Storage type is named outside MsProbeDriver)

		internal abstract class MsProbeScope
		{
			public ArmoryTE Armory;
			public abstract void Put(IEnumerable<Item> items);
			public abstract IEnumerable<Item> Contents();
			public abstract void Close();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static MsProbeScope OpenMs() => MsProbeDriver.Open();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void CloseMsAll() { if (ModLoader.HasMod("MagicStorage") && MagicStorageCompatSystem.Active) MsProbeDriver.Shutdown(); }
	}
}
