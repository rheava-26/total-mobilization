using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
	/// TEST-ONLY investigation probe (changes no game behaviour; does nothing unless LEVY_PICKPROBE is set). Answers "why do levies pick the weapons they pick":
	///  1. CATALOGUE (LEVY_PICKPROBE_CATALOG=ModA,ModB): one TSV row per weapon-ish item of those mods: family, damage class, damage, cadence, shots, rarity, value, Score, issuable.
	///  2. SCENARIOS (LEVY_PICKPROBE_SCEN=path): builds an in-memory ArmoryTE, stocks it, issues N soldiers through the REAL IssueGear / UpgradeWeapons, logs every pick.
	///     File format, one directive per line:  [name] starts a scenario;  W Mod/Item [xN] weapon;  A Mod/Item armor;  M Mod/Item xN ammo;  SOLDIERS n;  STAGE (issue now, then keep reading: later
	///     W/A/M lines add MORE stock and the soldiers then run UpgradeWeapons);  CFG ClassMatchIssue=true|false;  # comment. Vanilla items are written as Vanilla/ItemIDName.
	/// Output goes to LEVY_PICKPROBE_OUT. The server exits afterwards without saving.
	/// </summary>
	public class WeaponChoiceProbe : ModSystem
	{
		private static bool _enabled, _ran; private static int _ticks;
		private static WeaponChoiceProbe _inst;
		public override void Load() { _enabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_PICKPROBE")); _inst = this; if (_enabled) Main.OnTickForThirdPartySoftwareOnly += Tick; }
		public override void Unload() { try { Main.OnTickForThirdPartySoftwareOnly -= Tick; } catch { } _enabled = false; _inst = null; }
		private static void Tick() { _inst?.PostUpdateEverything(); }

		public override void PostUpdateEverything()
		{
			if (!_enabled || _ran || Main.netMode == NetmodeID.MultiplayerClient || !Main.dedServ || !Main.gameMenu == false && false) return;
			if (++_ticks < 600) return;
			_ran = true;
			var sb = new StringBuilder();
			try { Run(sb); } catch (Exception e) { sb.AppendLine("PROBE EXCEPTION: " + e); }
			string outp = Environment.GetEnvironmentVariable("LEVY_PICKPROBE_OUT") ?? "pickprobe.txt";
			File.WriteAllText(outp, sb.ToString());
			Mod.Logger.Info("PICKPROBE: done -> " + outp);
			try { Netplay.SaveOnServerExit = false; Netplay.Disconnect = true; } catch { }
			new System.Threading.Thread(() => { System.Threading.Thread.Sleep(10000); Environment.Exit(0); }) { IsBackground = true }.Start();
		}

		private static string ClsName(DamageClass c) => c == null ? "none" : c == DamageClass.Melee ? "melee" : c == DamageClass.Ranged ? "ranged" : c == DamageClass.Magic ? "magic" : c == DamageClass.Summon ? "summon" : c.Name;

		private static Item Make(string spec, int stack = 1)
		{
			int type = 0;
			int slash = spec.IndexOf('/');
			string mod = slash > 0 ? spec.Substring(0, slash) : "Vanilla", nm = slash > 0 ? spec.Substring(slash + 1) : spec;
			if (mod == "Vanilla") { var f = typeof(ItemID).GetField(nm); if (f != null) type = Convert.ToInt32(f.GetValue(null)); }
			else if (ModContent.TryFind<ModItem>(mod, nm, out var mi)) type = mi.Type;
			if (type <= 0) return null;
			var it = new Item(); it.SetDefaults(type); it.stack = Math.Min(stack, Math.Max(1, it.maxStack)); return it;
		}

		private static string Describe(Item it)
		{
			if (it == null || it.IsAir) return "-";
			var p = WeaponClassifier.GetProfile(it);
			return $"{(it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : it.Name)}[{p.Family}/{ClsName(WeaponClassifier.ItemDamageClass(it))} sc={WeaponClassifier.Score(it):F0}{(it.stack > 1 ? " x" + it.stack : "")}]";
		}

		private void Run(StringBuilder sb)
		{
			sb.AppendLine("# Levy weapon-choice probe. Levy " + Mod.Version);
			string cat = Environment.GetEnvironmentVariable("LEVY_PICKPROBE_CATALOG");
			if (!string.IsNullOrEmpty(cat)) Catalog(sb, cat.Split(','));
			string tiers = Environment.GetEnvironmentVariable("LEVY_PICKPROBE_TIERS");
			if (!string.IsNullOrEmpty(tiers)) TierTable(sb, tiers.Split(','));
			string scen = Environment.GetEnvironmentVariable("LEVY_PICKPROBE_SCEN");
			if (!string.IsNullOrEmpty(scen) && File.Exists(scen)) Scenarios(sb, File.ReadAllLines(scen));
		}

		private void Catalog(StringBuilder sb, string[] mods)
		{
			sb.AppendLine("\n## CATALOGUE");
			sb.AppendLine("mod\tname\tfamily\tclass\textended\tissuable\tdamage\tuseTime\tuseAnim\treuse\tlimit\tshots\tprojPerShot\trare\tvalue\tconsumable\tmaxStack\tmana\tuseAmmo\tdamageType\trangeTiles\tscore\tlocksOwner");
			foreach (var mod in mods)
			{
				IEnumerable<(int type, string name)> src;
				if (mod.Trim() == "Vanilla") src = Enumerable.Range(1, ItemID.Count - 1).Select(t => (t, ItemID.Search.TryGetName(t, out var n) ? n : "?"));
				else if (ModLoader.TryGetMod(mod.Trim(), out var m)) src = m.GetContent<ModItem>().Select(x => (x.Type, x.Name));
				else { sb.AppendLine("# mod not loaded: " + mod); continue; }
				foreach (var (mtype, mname) in src)
				{
					var it = new Item(); it.SetDefaults(mtype);
					if (it.damage <= 0 && it.shoot <= 0) continue;
					if (it.accessory || it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0 || it.ammo > 0 || it.createTile >= 0 || it.pick > 0 || it.axe > 0 || it.hammer > 0) continue;
					var p = WeaponClassifier.GetProfile(it);
					int anim = Math.Max(1, it.useAnimation), ut = Math.Max(1, it.useTime);
					int nat = Math.Max(1, (anim + ut - 1) / ut);
					bool locks = it.shoot > 0 && OwnerLockProbe.LocksOwner(it.shoot);
					sb.AppendLine(string.Join("\t", mod.Trim(), mname, p.Family, ClsName(WeaponClassifier.ItemDamageClass(it)), p.Extended, WeaponClassifier.IsIssuableWeapon(it), it.damage, it.useTime, it.useAnimation, it.reuseDelay,
						it.useLimitPerAnimation.HasValue ? it.useLimitPerAnimation.Value : -1, nat, p.ProjectilesPerShot, it.rare, it.value, it.consumable, it.maxStack, it.mana, it.useAmmo, it.DamageType?.Name ?? "?", p.RangeTiles,
						WeaponClassifier.Score(it).ToString("F1"), locks));
				}
			}
		}

		private void TierTable(StringBuilder sb, string[] mods)
		{
			ProgressionTierProbe.Build(sb);
			sb.AppendLine("\n## TIERS");
			sb.AppendLine("mod\tname\tfamily\tclass\tdmg\tperHit\thitsPerSec\tdpsAdj\tpierce\trare\tvalue\tscore\troute\trouteSrc\tuseAmmo\tmana\tleafCost\tisTool");
			foreach (var mod in mods)
			{
				IEnumerable<(int type, string name)> src;
				if (mod.Trim() == "Vanilla") src = Enumerable.Range(1, ItemID.Count - 1).Select(t => (t, ItemID.Search.TryGetName(t, out var n) ? n : "?"));
				else if (ModLoader.TryGetMod(mod.Trim(), out var m)) src = m.GetContent<ModItem>().Select(x => (x.Type, x.Name));
				else continue;
				foreach (var (mtype, mname) in src)
				{
					var it = new Item(); it.SetDefaults(mtype);
					if (it.accessory || it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0 || it.ammo > 0 || it.createTile >= 0 || it.pick > 0 || it.axe > 0 || it.hammer > 0 || it.damage <= 0) continue;
					if (!WeaponClassifier.IsIssuableWeapon(it)) continue;
					var p = WeaponClassifier.GetProfile(it);
					float score = WeaponClassifier.Score(it);
					float tm = 1f + LevyConstants.WeaponRarityWeight * WeaponClassifier.RarityTier(it.rare) + LevyConstants.WeaponValueWeight * (float)Math.Log10(1.0 + Math.Max(0, it.value));
					float perHit = p.Family == WeaponFamily.Thrown ? WeaponClassifier.ThrownRankDamage(it) : it.damage + WeaponClassifier.AmmoScoreDamage(it);
					float cadence = Math.Max(1, it.useTime); int anim = Math.Max(1, it.useAnimation);
					int nat = Math.Max(1, (anim + (int)cadence - 1) / (int)cadence);
					int shots = it.useLimitPerAnimation.HasValue && it.useLimitPerAnimation.Value > 0 ? Math.Min(it.useLimitPerAnimation.Value, nat) : nat;
					if (shots < nat || it.reuseDelay > 0) cadence = (anim + Math.Max(0, it.reuseDelay)) / (float)shots;
					float hps = p.ProjectilesPerShot * 60f / cadence;
					var ps = WeaponClassifier.ProjectileSample(it.shoot); int pen = ps != null ? ps.penetrate : 1;
					float t = ProgressionTierProbe.Tier(mtype, out string rs);
					sb.AppendLine(string.Join("\t", mod.Trim(), mname, p.Family, ClsName(WeaponClassifier.ItemDamageClass(it)), it.damage, perHit.ToString("F1"), hps.ToString("F2"), (score / tm).ToString("F1"), pen, it.rare, it.value, score.ToString("F1"),
						t >= ProgressionTierProbe.Inf ? "-1" : t.ToString("F2"), rs, it.useAmmo, it.mana, ((long)ProgressionTierProbe.LeafCost(mtype)).ToString(), it.pick > 0 || it.hammer > 0 || it.axe > 0));
				}
			}
		}

		private static readonly Dictionary<string, string[]> Sets = new()
		{
			{ "melee", new[] { "SolarFlareHelmet", "SolarFlareBreastplate", "SolarFlareLeggings" } },
			{ "ranged", new[] { "VortexHelmet", "VortexBreastplate", "VortexLeggings" } },
			{ "magic", new[] { "NebulaHelmet", "NebulaBreastplate", "NebulaLeggings" } },
			{ "summon", new[] { "StardustHelmet", "StardustBreastplate", "StardustLeggings" } },
		};
		private static void WearSet(SoldierRecord r, string cls)
		{
			if (!Sets.TryGetValue(cls, out var set)) return;
			r.Gear.Head = Make("Vanilla/" + set[0]); r.Gear.Body = Make("Vanilla/" + set[1]); r.Gear.Legs = Make("Vanilla/" + set[2]);
		}
		/// <summary>Class list for the army: "interleave" cycles the classes, "meleelast" enlists melee last, "meleefirst" first.</summary>
		private static List<string> BuildMix(string[] counts, string order)
		{
			var left = new Dictionary<string, int>();
			foreach (var c in counts) { var kv = c.Split('='); left[kv[0]] = int.Parse(kv[1]); }
			var res = new List<string>();
			string[] cyc = order == "meleelast" ? new[] { "ranged", "magic", "summon", "none", "melee" } : order == "meleefirst" ? new[] { "melee", "ranged", "magic", "summon", "none" } : new[] { "melee", "ranged", "magic", "summon", "none" };
			if (order == "interleave") { while (left.Values.Sum() > 0) foreach (var c in cyc) if (left.GetValueOrDefault(c) > 0) { res.Add(c); left[c]--; } }
			else foreach (var c in cyc) while (left.GetValueOrDefault(c) > 0) { res.Add(c); left[c]--; }
			return res;
		}

		private sealed class Scen { public string Name; public List<string> Lines = new(); }

		private void Scenarios(StringBuilder sb, string[] lines)
		{
			var scens = new List<Scen>(); Scen cur = null;
			foreach (var raw in lines)
			{
				string l = raw.Trim();
				if (l.Length == 0 || l.StartsWith("#")) continue;
				if (l.StartsWith("[")) { cur = new Scen { Name = l.Trim('[', ']') }; scens.Add(cur); continue; }
				cur?.Lines.Add(l);
			}
			bool oldCm = LevyConfig.Get.ClassMatchIssue;
			foreach (var sc in scens)
			{
				sb.AppendLine("\n## SCENARIO " + sc.Name);
				var te = new ArmoryTE();
				int nSold = 9; var souls = new List<SoldierRecord>(); bool staged = false; var mix = new List<string>(); string order = "interleave";
				SoldierRecord NewSoldier(int i)
				{
					var r = new SoldierRecord { Id = 100 + i };
					if (mix.Count > 0 && i < mix.Count) WearSet(r, mix[i]);
					return r;
				}
				ArmorDoctrine.Reset();
				LevyConfig.Get.ClassMatchIssue = false;
				void Stock(string[] t)
				{
					string spec = t[1]; int n = 1; if (t.Length > 2 && t[2].StartsWith("x")) int.TryParse(t[2].Substring(1), out n);
					var it = Make(spec, n);
					if (it == null) { sb.AppendLine("  !! cannot resolve " + spec); return; }
					int count = it.maxStack > 1 ? 1 : Math.Max(1, n); // non-stackables: xN = N copies
					for (int c = 0; c < count; c++)
					{
						var cp = c == 0 ? it : Make(spec, 1);
						int slot = Array.FindIndex(te.Storage, x => x == null || x.IsAir);
						te.Storage[slot] = cp;
					}
				}
				void Show(string title)
				{
					sb.AppendLine("  -- " + title);
					foreach (var s in souls)
					{
						var g = s.Gear; var ac = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
						sb.AppendLine($"  soldier {s.Id}: weapon={Describe(g.Weapon)} side={Describe(g.Sidearm)} armorClass={ClsName(ac)} head={(g.Head.IsAir ? "-" : g.Head.Name)}");
					}
					var tally = souls.GroupBy(s => s.Gear.HasWeapon ? (s.Gear.Weapon.ModItem != null ? s.Gear.Weapon.ModItem.Name : s.Gear.Weapon.Name) : "(none)").Select(g => g.Key + " x" + g.Count());
					sb.AppendLine("  tally: " + string.Join(", ", tally));
				}
				foreach (var l in sc.Lines)
				{
					var t = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
					switch (t[0])
					{
						case "SOLDIERS": nSold = int.Parse(t[1]); break;
						case "MIX": { mix = BuildMix(t.Skip(2).ToArray(), t[1]); nSold = mix.Count; break; } // MIX <order> melee=5 ranged=4 magic=3 summon=1 none=2
						case "SHOW": { var si = Make(t[1]); sb.AppendLine("  SHOW " + t[1] + ": " + (si == null ? "cannot resolve" : $"{Describe(si)} issuable={WeaponClassifier.IsIssuableWeapon(si)} dmg={si.damage} pick={si.pick} hammer={si.hammer} axe={si.axe} useTime={si.useTime} rare={si.rare} value={si.value} noMelee={si.noMelee} shoot={si.shoot}")); break; }
						case "CFG": { var kv = t[1].Split('='); if (kv[0] == "ClassMatchIssue") LevyConfig.Get.ClassMatchIssue = bool.Parse(kv[1]); break; }
						case "W": case "A": case "M": Stock(t); break;
						case "WI": { var wi = Make(t[1]); int slot = Array.FindIndex(te.IssueWeapons, 0, ArmoryTE.SidearmSlot, x => x == null || x.IsAir); if (wi == null || slot < 0) sb.AppendLine("  !! cannot place " + t[1]); else te.IssueWeapons[slot] = wi; break; } // the player's own weapon rack (issue row)
						case "STAGE":
							if (!staged)
							{
								for (int i = 0; i < nSold; i++) { var r = NewSoldier(i); souls.Add(r); te.IssueGear(r); }
								Show("after first issue");
								staged = true;
							}
							else
							{
								foreach (var s in souls) te.UpgradeWeapons(s);
								Show("after UpgradeWeapons (round)");
							}
							break;
					}
				}
				if (!staged) { for (int i = 0; i < nSold; i++) { var r = NewSoldier(i); souls.Add(r); te.IssueGear(r); } Show("issue"); }
				else { foreach (var s in souls) te.UpgradeWeapons(s); Show("after UpgradeWeapons"); for (int k = 0; k < 2; k++) foreach (var s in souls) te.UpgradeWeapons(s); Show("after 2 more upgrade rounds"); }
				sb.AppendLine("  STOCK TABLE (all stocked weapons, by Score): name | family/class | dmg useTime reuse rare value | score");
				foreach (var it in te.Storage.Concat(souls.Select(x => x.Gear.Weapon)).Where(x => x != null && !x.IsAir && WeaponClassifier.IsIssuableWeapon(x)).GroupBy(x => x.type).Select(g => g.First()).OrderByDescending(x => WeaponClassifier.Score(x)))
					sb.AppendLine($"    {(it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : it.Name)} | {WeaponClassifier.GetProfile(it).Family}/{ClsName(WeaponClassifier.ItemDamageClass(it))} | {it.damage} {it.useTime} {it.reuseDelay} {it.rare} {it.value} | {WeaponClassifier.Score(it):F1}");
				var left = te.Storage.Where(x => x != null && !x.IsAir && WeaponClassifier.IsIssuableWeapon(x)).Select(x => Describe(x));
				sb.AppendLine("  left on shelves: " + string.Join(" | ", left));
			}
			LevyConfig.Get.ClassMatchIssue = oldCm;
		}
	}
}
