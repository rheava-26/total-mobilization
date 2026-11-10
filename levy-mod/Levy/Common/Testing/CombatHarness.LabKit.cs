using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Levy.Common.Movement;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// LAB KIT hooks (measurement only; driven by levy-mod/lab/levylab). Two small, general additions to the calrev harness:
	///   lv=&lt;file.json&gt;  load the army and its supplies from data: levies[] (cls, weapon, ammo, armor[], acc[], lc, mc, potions, potion), accSlots, buffs[] (BuffID names or Mod/Name),
	///                     stations[] (campfire, sunflower, bast, sharpening, wartable, bewitching, ammobox, crystalball, heartlantern, starinabottle; null = by stationLevel) and honey.
	///                     New gear, potion and station presets therefore need no C# change. Unknown item names are logged ("HARNESS-KIT: missing") and dropped.
	///   tag=&lt;id&gt;       an opaque cell id that goes into the run key, so a driver can map every result row back to its cell.
	/// Every calrev fight also appends one row to &lt;out&gt;.kit.tsv (cell tag, levy count, HP, summed WeaponRank, damage by weapon, by class, death times) for the table command.
	/// </summary>
	public partial class CombatHarness
	{
		internal sealed class KitLevy
		{
			public string cls = "R", weapon = "", ammo = "", potion = "Healing Potion";
			public string[] armor = Array.Empty<string>(), acc = Array.Empty<string>();
			public int lc = 15, mc = 9, potions = 8;
		}

		internal sealed class KitData
		{
			public int stationLevel = 2, accSlots = 5; public bool honey;
			public string[] stations, buffs = Array.Empty<string>();
			public List<KitLevy> levies = new();
			internal int[] BuffIds = Array.Empty<int>();
		}

		private static readonly SortedSet<string> KitMissing = new();
		private static bool KitKnown(string n) { if (string.IsNullOrEmpty(n) || CrTryItemId(n, out _)) return true; KitMissing.Add(n); return false; }

		internal static KitData KitLoad(string path)
		{
			var d = JsonSerializer.Deserialize<KitData>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true });
			d.levies = d.levies.Where(l => KitKnown(l.weapon)).ToList();
			foreach (var l in d.levies) { l.armor = l.armor.Where(KitKnown).ToArray(); l.acc = l.acc.Where(KitKnown).ToArray(); if (!KitKnown(l.ammo)) l.ammo = ""; if (!KitKnown(l.potion)) l.potion = "Healing Potion"; }
			var ids = new List<int>();
			foreach (string b in d.buffs ?? Array.Empty<string>())
			{
				var f = typeof(BuffID).GetField(b, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				if (f != null) ids.Add(Convert.ToInt32(f.GetValue(null)));
				else if (ModContent.TryFind<ModBuff>(b, out var mb)) ids.Add(mb.Type);
				else KitMissing.Add("buff:" + b);
			}
			d.BuffIds = ids.ToArray();
			if (KitMissing.Count > 0) global::Levy.Levy.Instance.Logger.Warn("HARNESS-KIT: missing " + string.Join(", ", KitMissing));
			return d;
		}

		internal static List<CrLevySpec> KitSpecs(KitData d) => d.levies.Select(l => new CrLevySpec
		{
			Cls = char.ToUpperInvariant(l.cls[0]), Weapon = l.weapon, Ammo = l.ammo, Armor = l.armor, Acc = l.acc, LifeCrystals = l.lc, ManaCrystals = l.mc, Potions = l.potions, Potion = l.potion, ManAtArms = false
		}).ToList();

		private static HashSet<string> _kitSt;
		/// <summary>Station object wanted by name (null list = the level-based default).</summary>
		private static bool KitWant(string name, bool byLevel) => _kitSt == null ? byLevel : _kitSt.Contains(name);

		private static int _kitCasts0; private static long _kitHooked0, _kitHang0, _kitWh0, _kitWr0, _kitLava0;
		private void KitFinish(CrRun run, CrState st)
		{
			try
			{
				string path = _crOut + ".kit.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\ttag\tn\tavgHp\trankSum\tweaponDmg\tclassDmg\tdeathTimes\thkCasts\thookedSec\thangSec\twingHoldSec\twingRelSec\tlavaCatches\n");
				var cd = new Dictionary<string, long>(); var wd = new Dictionary<string, long>(); double rank = 0;
				foreach (var l in st.Levies)
				{
					string c = DlClass(l.Spec.Weapon, l.Spec.Ammo); cd.TryGetValue(c, out long v); cd[c] = v + l.DmgBoss;
					wd.TryGetValue(l.Weapon, out long u); wd[l.Weapon] = u + l.DmgBoss;
					try { rank += WeaponRank.Of(CrMake(l.Spec.Weapon)); } catch { }
				}
				string f1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
				File.AppendAllText(path, string.Join("\t", new object[] { run.Key, run.Tag, st.Levies.Count, f1(st.Levies.Average(l => l.MaxHp)), f1(rank),
					string.Join(" ", wd.OrderByDescending(kv => kv.Value).Select(kv => kv.Key.Replace("CalamityMod/", "C:").Replace(' ', '_') + "=" + kv.Value)),
					string.Join(" ", cd.Select(kv => kv.Key + "=" + kv.Value)),
					string.Join(",", st.Levies.Where(l => l.Dead).Select(l => l.DeathTick / 60.0).OrderBy(x => x).Select(f1)),
					SoldierHooks.Casts - _kitCasts0, f1((SoldierNPC.StatHookedTicks - _kitHooked0) / 60.0), f1((SoldierNPC.StatHangTicks - _kitHang0) / 60.0), f1((SoldierNPC.StatWingHoldTicks - _kitWh0) / 60.0), f1((SoldierNPC.StatWingRelTicks - _kitWr0) / 60.0), SoldierNPC.StatLavaCatches - _kitLava0 }) + "\n");
				_kitCasts0 = SoldierHooks.Casts; _kitHooked0 = SoldierNPC.StatHookedTicks; _kitHang0 = SoldierNPC.StatHangTicks; _kitWh0 = SoldierNPC.StatWingHoldTicks; _kitWr0 = SoldierNPC.StatWingRelTicks; _kitLava0 = SoldierNPC.StatLavaCatches;
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-KIT: finish row failed: " + e.Message); }
		}
	}
}
