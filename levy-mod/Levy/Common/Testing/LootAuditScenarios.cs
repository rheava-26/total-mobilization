using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace LootAuditKit
{
	/// <summary>One way of killing: a name, a setup run once before its cells, and the credit applied to each victim just before the lethal strike.</summary>
	public sealed class Killer
	{
		public string Name; public Action Prepare; public Action<NPC, int> Credit; public Action Cleanup;
	}

	public sealed class CellRec
	{
		public string Stage, Mode, Zone, Npc, Killer; public int Kills, LootCalls, OnKills, SpawnFails; public long Total, Weapons;
	}

	/// <summary>The measurement matrix. Same source in Levy and in the stand-alone probe mod, so both arms run the identical procedure.</summary>
	public static class AuditMatrix
	{
		static readonly (string name, int type, bool hm)[] Underworld =
		{
			("Demon", NPCID.Demon, false), ("VoodooDemon", NPCID.VoodooDemon, false), ("FireImp", NPCID.FireImp, false), ("LavaSlime", NPCID.LavaSlime, false),
			("Hellbat", NPCID.Hellbat, false), ("Lavabat", NPCID.Lavabat, false), ("BoneSerpent", NPCID.BoneSerpentHead, false), ("RedDevil", NPCID.RedDevil, true),
		};
		static readonly (string name, int type, bool hm)[] Hallow =
		{
			("Pixie", NPCID.Pixie, true), ("Unicorn", NPCID.Unicorn, true), ("Gastropod", NPCID.Gastropod, true), ("ChaosElemental", NPCID.ChaosElemental, true),
			("EnchantedSword", NPCID.EnchantedSword, true), ("IlluminantSlime", NPCID.IlluminantSlime, true), ("IlluminantBat", NPCID.IlluminantBat, true),
		};

		static List<(FieldInfo f, bool old)> _calFlags;

		/// <summary>Calamity's Revengeance and Death flags, found by name so the probe needs no reference to Calamity.</summary>
		static void SetCalamityDifficulty(bool on, Action<string> log)
		{
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				if (_calFlags == null)
				{
					_calFlags = new();
					var t = cal.Code.GetType("CalamityMod.World.CalamityWorld");
					if (t != null)
						foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
							if (f.FieldType == typeof(bool) && (f.Name.Equals("revenge", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("death", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("Revengeance", StringComparison.OrdinalIgnoreCase)))
								_calFlags.Add((f, (bool)f.GetValue(null)));
					log("calamity difficulty flags found: " + string.Join(",", _calFlags.Select(c => c.f.Name)));
				}
				foreach (var (f, old) in _calFlags) f.SetValue(null, on ? true : old);
			}
			catch (Exception e) { log("calamity flag set failed: " + e.Message); }
		}

		public static List<CellRec> Run(Action<string> log, string arm, IList<Killer> killers, Player pl, int n)
		{
			var cells = new List<CellRec>();
			int spawnX = Main.spawnTileX, uwY = Main.maxTilesY - 60, hallowY = (int)(Main.worldSurface * 0.45);
			bool oldHm = Main.hardMode; int oldMode = Main.GameMode;
			foreach (var k in killers)
			{
				k.Prepare?.Invoke();
				foreach (bool hm in new[] { false, true })
					foreach (string mode in new[] { "classic", "expert+rev" })
					{
						Main.hardMode = hm; Main.GameMode = mode == "classic" ? 0 : 1; SetCalamityDifficulty(mode != "classic", log);
						foreach (var (zone, list) in new[] { ("uw", Underworld), ("hallow", Hallow) })
							foreach (var (name, type, needsHm) in list)
							{
								if (needsHm && !hm) continue;
								int y = zone == "uw" ? uwY : hallowY;
								pl.position = new Microsoft.Xna.Framework.Vector2(spawnX * 16f, y * 16f);
								pl.ZoneUnderworldHeight = zone == "uw"; pl.ZoneHallow = zone == "hallow"; pl.ZoneOverworldHeight = zone == "hallow";
								var r = AuditCore.Kill(type, n, spawnX, y, k.Credit);
								string items = string.Join(";", r.Items.OrderByDescending(kv => kv.Value).Take(30).Select(kv => AuditCore.ItemName(kv.Key) + ":" + kv.Value));
								cells.Add(new CellRec { Stage = hm ? "hm" : "pre", Mode = mode, Zone = zone, Npc = name, Killer = k.Name, Weapons = r.Items.Where(kv => AuditCore.IsWeapon(kv.Key)).Sum(kv => kv.Value), Kills = r.Kills, LootCalls = r.LootCalls, OnKills = r.OnKills, SpawnFails = r.SpawnFails, Total = r.Total() });
								log($"CELL arm={arm} stage={(hm ? "hm" : "pre")} mode={mode} zone={zone} npc={name} killer={k.Name} n={n} kills={r.Kills} spawnFail={r.SpawnFails} lootCalls={r.LootCalls} onKills={r.OnKills} itemsPerKill={(r.Kills > 0 ? (double)r.Total() / r.Kills : 0):F2} weapons={r.Items.Where(kv => AuditCore.IsWeapon(kv.Key)).Sum(kv => kv.Value)} copperPerKill={(r.Kills > 0 ? (double)r.CoinCopper() / r.Kills : 0):F0} items={items}");
							}
						SetCalamityDifficulty(false, log);
					}
				k.Cleanup?.Invoke();
			}
			Main.hardMode = oldHm; Main.GameMode = oldMode;
			return cells;
		}

		/// <summary>The spawner's per-player rate and enemy cap under each kind of event.</summary>
		public static void SpawnRates(Action<string> log, string arm, Player pl, Func<int> armyCp)
		{
			pl.position = new Microsoft.Xna.Framework.Vector2(Main.spawnTileX * 16f, (Main.maxTilesY - 60) * 16f);
			pl.ZoneUnderworldHeight = true;
			bool bm = Main.bloodMoon, ec = Main.eclipse, pm = Main.pumpkinMoon, sm = Main.snowMoon; int it = Main.invasionType, isz = Main.invasionSize;
			void Row(string ctx)
			{
				int rate, max;
				try { (rate, max) = AuditCore.SpawnRate(pl); } catch (Exception e) { log($"SPAWN arm={arm} ctx={ctx} FAILED {e.GetType().Name} in another mod's EditSpawnRate on a synthetic player"); return; }
				log($"SPAWN arm={arm} ctx={ctx} rate=1in{rate} max={max} perMinute={3600.0 / rate:F1} armyCp={armyCp()}");
			}
			Row("none");
			Main.bloodMoon = true; Row("bloodmoon"); Main.bloodMoon = bm;
			Main.eclipse = true; Row("eclipse"); Main.eclipse = ec;
			Main.invasionType = 1; Main.invasionSize = 100; Row("goblin-army"); Main.invasionType = it; Main.invasionSize = isz;
			Main.pumpkinMoon = true; Row("pumpkin-moon"); Main.pumpkinMoon = pm;
			Main.snowMoon = true; Row("frost-moon"); Main.snowMoon = sm;
		}
	}
}
