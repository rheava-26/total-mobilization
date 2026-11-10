using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using LootAuditKit;
using Levy.Common.Events;
using Levy.Common.Progression;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// LOOT AUDIT. Dedicated-server boot with LEVY_LOOTAUDIT=1 and a world: kills hundreds of Underworld and Hallow enemies by the player alone and by a 20-levy army through the real
	/// credit path (LootCredit.Credit before a lethal strike), counts every dropped item, NPCLoot call and OnKill call, reads the spawner's rate under each event, and asserts that with the
	/// default configuration Levy adds nothing: one loot call per kill, army kills drop what player kills drop, no army rule sits on an ordinary monster, the event and officer extras obey their
	/// toggles, and a late boss alone is worth exactly one boss of levies. Logged as "[LootAudit]" lines, ending in "self-check pass=N fail=M". Plants fixtures in the loaded world; test worlds only.
	/// </summary>
	public class LootAuditSystem : ModSystem
	{
		bool _done;

		public override void OnWorldLoad()
		{
			if (_done || !Main.dedServ || Environment.GetEnvironmentVariable("LEVY_LOOTAUDIT") != "1") return;
			_done = true;
			int pass = 0, fail = 0;
			void Log(string s) => Mod.Logger.Info("[LootAudit] " + s);
			void Check(string n, bool ok, string d = "") { if (ok) pass++; else fail++; Log($"{(ok ? "PASS" : "FAIL")} {n}{(d.Length > 0 ? " :: " + d : "")}"); }
			var cfg = LevyProgressionConfig.Get;
			Player oldPlayer = Main.player[0]; Player pl = null;
			try
			{
				AuditCore.Hook();
				pl = AuditCore.NewPlayer("Audit");
				Log($"START arm=levy hardMode={Main.hardMode} mods={string.Join(",", ModLoader.Mods.Where(m => m.Name != "ModLoader").Select(m => m.Name))}");

				// ---- 1. configuration: every loot option is at the vanilla value in this boot
				Check("config: loot options default to vanilla", cfg.ArmyLootPercentPerCp == 0f && !cfg.ArmyLootRareChance && !cfg.OrbLootScaling && cfg.MobDropMultiplier == 1f && cfg.SoulDropMultiplier == 1f
					&& cfg.EctoplasmDropMultiplier == 1f && cfg.DungeonSpiritMultiplier == 1f && cfg.DungeonSpiritPercentPerCp == 0f && cfg.LuminiteDropMultiplier == 1f && cfg.LunarFragmentDropMultiplier == 1f
					&& !cfg.JungleChlorophyteDrop && !cfg.EventChlorophyteDrop && cfg.OnceOnlyBossWeaponRepeatPercent == 0 && cfg.EarlyHardmodeCrateOreMultiplier == 1);
				{
					var bad = new List<string>();
					foreach (var f in typeof(LevyProgressionConfig).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
						if (f.GetCustomAttribute<DefaultValueAttribute>() is { } dv && !Equals(Convert.ChangeType(dv.Value, f.FieldType), f.GetValue(cfg))) bad.Add(f.Name);
					Check("config: this boot runs the shipped defaults", bad.Count == 0, string.Join(",", bad));
				}
				Check("config: the event and officer toggles exist and default on", cfg.EventSpawnBoost && cfg.EventInvasionBoost && cfg.MerchantFenceCoins && cfg.TaxCollectorBounty);

				// ---- 2. rules: nothing army-scaled or rerolled hangs on an ordinary monster
				{
					var db = Main.ItemDropsDB; var list = new List<IItemDropRule>(); int rare = 0, army = 0, scanned = 0; var who = new List<string>();
					foreach (var kv in ContentSamples.NpcsByNetId)
					{
						var n = kv.Value;
						if (n.boss || (kv.Key > 0 && kv.Key < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[kv.Key])) continue;
						list.Clear(); ProgressionLootNpc.Collect(db.GetRulesForNPCID(kv.Key, false), list); scanned++;
						if (kv.Key == NPCID.EaterofWorldsBody || kv.Key == NPCID.EaterofWorldsTail) continue;   // segments of a boss share its rules
						foreach (var r in list)
							foreach (var ch in r.ChainedRules)
							{
								if (ch.RuleToChain is RareRerollRule rr) { rare++; if (who.Count < 12) who.Add($"{n.FullName}:rare:{r.GetType().Name}:{AuditCore.ItemName(rr.ItemId)}"); }
								if (ch.RuleToChain is ExtraAmountRule e && e.Army) { army++; if (who.Count < 12) who.Add($"{n.FullName}:army:{AuditCore.ItemName(e.ItemId)}"); }
							}
					}
					string whoS = string.Join(" | ", who); Check("rules: no army or reroll rule on a non-boss NPC", rare == 0 && army == 0, $"scannedNpcs={scanned} rare={rare} army={army} e.g. {whoS}");
					Log($"RULES attached: army={ProgressionDropsSystem.ArmyRules} rare={ProgressionDropsSystem.RareRules} luminite={ProgressionDropsSystem.LuminiteRules} fragment={ProgressionDropsSystem.FragmentRules} globalRules={ProgressionGlobalNpc.GlobalRules}");
				}

				// ---- 3. kills: the player alone, then a 20-levy army, through the real credit path
				var bodies = new List<SoldierNPC>(); var recs = new List<SoldierRecord>();
				var killers = new List<Killer>
				{
					new Killer { Name = "player", Credit = (npc, k) => npc.PlayerInteraction(0) },
					new Killer
					{
						Name = "army20",
						Prepare = () =>
						{
							var roster = RosterSystem.Instance;
							for (int i = 0; i < 20; i++)
							{
								var rec = new SoldierRecord { Id = 90000 + i, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "Audit " + i, CommanderName = pl.name, CommanderIndex = 0 };
								rec.CurrentHp = rec.MaxHp;
								roster.Regulars.Add(rec); recs.Add(rec);
								int idx = roster.SpawnBody(rec, new Microsoft.Xna.Framework.Vector2(Main.spawnTileX * 16f + i * 8f, (Main.maxTilesY - 60) * 16f));
								if (idx >= 0 && Main.npc[idx].ModNPC is SoldierNPC b) bodies.Add(b);
							}
						},
						Credit = (npc, k) => { if (bodies.Count > 0) { var b = bodies[k % bodies.Count]; LootCredit.Credit(npc, b); LootCredit.OnSoldierHit(npc, b, 1); } else npc.PlayerInteraction(0); },
					},
				};
				var cells = AuditMatrix.Run(Log, "levy", killers, pl, 150);
				Check("army: 20 levy bodies stood on the field", bodies.Count == 20, $"bodies={bodies.Count}");
				Check("army: the player holds the army's command points", ArmyCp.Of(pl) >= 20, $"cp={ArmyCp.Of(pl)}");
				var bad2 = cells.Where(c => c.Kills > 0 && (c.LootCalls != c.Kills || c.OnKills != c.Kills)).ToList();
				Check("one NPCLoot and one OnKill per kill in every cell (no double loot)", cells.Count > 0 && bad2.Count == 0, $"cells={cells.Count} bad={string.Join(",", bad2.Take(5).Select(c => $"{c.Killer}/{c.Npc}/{c.Stage}/{c.Mode}:{c.LootCalls}/{c.OnKills}/{c.Kills}"))}");
				long pt = cells.Where(c => c.Killer == "player").Sum(c => c.Total), at = cells.Where(c => c.Killer == "army20").Sum(c => c.Total);
				long pk = cells.Where(c => c.Killer == "player").Sum(c => c.Kills), ak = cells.Where(c => c.Killer == "army20").Sum(c => c.Kills);
				long pw = cells.Where(c => c.Killer == "player").Sum(c => c.Weapons), aw = cells.Where(c => c.Killer == "army20").Sum(c => c.Weapons);
				Log($"TOTAL player: kills={pk} weapons={pw} ({(pk > 0 ? (double)pw / pk : 0):F4}/kill) otherItems={pt - pw}   army20: kills={ak} weapons={aw} ({(ak > 0 ? (double)aw / ak : 0):F4}/kill) otherItems={at - aw}");
				Check("army kills drop the same weapons per kill as player kills (default config; Poisson tolerance)", pk == ak && Math.Abs(aw - pw) <= 4 * Math.Sqrt(aw + pw) + 3, $"player={pw} army={aw}");

				// ---- 4. a boss, to show what the army bonus does when it is switched on (SkeletronPrime: Souls of Fright as a material drop)
				{
					Main.hardMode = true; int oldGm = Main.GameMode; Main.GameMode = 0;
					var k20 = killers[1]; var kp = killers[0];
					double Per(Killer k, string tag)
					{
						pl.position = new Microsoft.Xna.Framework.Vector2(Main.spawnTileX * 16f, (float)(Main.worldSurface * 16.0 - 640.0));
						var r = AuditCore.Kill(NPCID.SkeletronPrime, 60, Main.spawnTileX, (int)Main.worldSurface - 40, k.Credit);
						string items = string.Join(";", r.Items.OrderByDescending(kv => kv.Value).Take(12).Select(kv => AuditCore.ItemName(kv.Key) + ":" + kv.Value));
						Log($"BOSS {tag} killer={k.Name} kills={r.Kills} lootCalls={r.LootCalls} onKills={r.OnKills} soulOfFright/kill={(r.Kills > 0 ? (double)(r.Items.TryGetValue(ItemID.SoulofFright, out long v) ? v : 0) / r.Kills : 0):F2} items={items}");
						return r.Kills > 0 ? (double)(r.Items.TryGetValue(ItemID.SoulofFright, out long w) ? w : 0) / r.Kills : 0;
					}
					double baseP = Per(kp, "default"), baseA = Per(k20, "default");
					cfg.ArmyLootPercentPerCp = 5f;     // 20 command points x 5% = +100% on boss materials
					double onA = Per(k20, "army5pct"), onP = Per(kp, "army5pct-nolevies");
					cfg.ArmyLootPercentPerCp = 0f;
					Check("boss material: army bonus off drops the same for army and player", baseP > 0 && Math.Abs(baseA / baseP - 1) < 0.25, $"player={baseP:F2} army={baseA:F2}");
					Check("boss material: army bonus on is roughly double for the army and unchanged for the player-only credit with no bonus", baseA > 0 && onA / baseA > 1.5 && onA / baseA < 2.6, $"army off={baseA:F2} on={onA:F2}");
					Log($"BOSS OBSERVED army bonus 5%/cp x 20cp: Soul of Fright per kill {baseA:F2} -> {onA:F2} (x{(baseA > 0 ? onA / baseA : 0):F2}); same-credit player kill with the option on {onP:F2}");
					Main.GameMode = oldGm;
				}

				// ---- 5. spawn rates under each event
				{
					// Calamity's own EditSpawnRate throws on a synthetic player, so Levy's hook is read directly: vanilla input 600 / 5 in, Levy's output.
					var g0 = ModContent.GetInstance<EventSpawnPressure>();
					bool bm0 = Main.bloodMoon, ec0 = Main.eclipse, pm0 = Main.pumpkinMoon, sm0 = Main.snowMoon; int it0 = Main.invasionType, is0 = Main.invasionSize;
					foreach (bool on in new[] { true, false })
					{
						cfg.EventSpawnBoost = on;
						foreach (var (ctx, set) in new (string, Action)[] { ("none", () => { }), ("bloodmoon", () => Main.bloodMoon = true), ("eclipse", () => Main.eclipse = true), ("goblin-army", () => { Main.invasionType = 1; Main.invasionSize = 100; }), ("pumpkin-moon", () => Main.pumpkinMoon = true), ("frost-moon", () => Main.snowMoon = true) })
						{
							Main.bloodMoon = Main.eclipse = Main.pumpkinMoon = Main.snowMoon = false; Main.invasionType = 0; Main.invasionSize = 0; set();
							int rr = 600, mm = 5; g0.EditSpawnRate(pl, ref rr, ref mm);
							Log($"SPAWN arm=levy-boost-{(on ? "on" : "off")} ctx={ctx} rate=1in{rr} max={mm} perMinute={3600.0 / rr:F1} armyCp={EventPressure.ArmyCp}");
						}
					}
					Main.bloodMoon = bm0; Main.eclipse = ec0; Main.pumpkinMoon = pm0; Main.snowMoon = sm0; Main.invasionType = it0; Main.invasionSize = is0;
					cfg.EventSpawnBoost = true;
					bool bm = Main.bloodMoon; Main.bloodMoon = true;
					var g = ModContent.GetInstance<EventSpawnPressure>();
					int r1 = 600, m1 = 5; cfg.EventSpawnBoost = true; g.EditSpawnRate(pl, ref r1, ref m1);
					int r2 = 600, m2 = 5; cfg.EventSpawnBoost = false; g.EditSpawnRate(pl, ref r2, ref m2);
					cfg.EventSpawnBoost = true; Main.bloodMoon = bm;
					Check("events: boost on raises the Blood Moon spawn rate", r1 < 600 && m1 > 5, $"on={r1}/{m1}");
					Check("events: boost off leaves the vanilla spawn rate", r2 == 600 && m2 == 5, $"off={r2}/{m2}");
					Main.bloodMoon = false; int r3 = 600, m3 = 5; g.EditSpawnRate(pl, ref r3, ref m3);
					Check("events: no event, no boost", r3 == 600 && m3 == 5, $"{r3}/{m3}");
					Main.bloodMoon = bm;
				}

				// ---- 6. levies: each boss beaten is worth its own amount; a late boss beaten early does not imply the ones before it
				{
					bool hm = Main.hardMode, ml = NPC.downedMoonlord, ks = NPC.downedSlimeKing, eoc = NPC.downedBoss1, wof = Main.hardMode;
					var lc = global::Levy.Common.Config.LevyConfig.Get; bool oldCap = lc.CapLeviesByStage; int oldStart = lc.LeviesAtStart;
					try
					{
						lc.CapLeviesByStage = true; lc.LeviesAtStart = 10; Main.hardMode = false; NPC.downedMoonlord = false; NPC.downedSlimeKing = false; NPC.downedBoss1 = false;
						LevyProgressionSystem.OnBossRecorded("Test:Refresh", NPCID.KingSlime);
						int b0 = LevyProgressionSystem.Levies;
						LevyProgressionSystem.OnBossRecorded("Test:SlimeGodLike", NPCID.Count + 99999);        // a modded pre-hardmode boss, beaten first, in a world with no vanilla boss down
						int b1 = LevyProgressionSystem.Levies;
						Log($"LEVIES pre-hardmode, no pre-hardmode boss beaten: {b0}; after one modded pre-hardmode boss alone: {b1}");
						Check("levies: a modded boss beaten early adds only its own worth (1 or 2)", b1 >= b0 + 1 && b1 <= b0 + 2, $"{b0}->{b1}");
						Main.hardMode = true; NPC.downedMoonlord = true;                                        // the world is past Moon Lord: stage 2
						LevyProgressionSystem.OnBossRecorded("Test:Refresh", NPCID.KingSlime);
						int c0 = LevyProgressionSystem.Levies;
						LevyProgressionSystem.OnBossRecorded("Test:LateLike", NPCID.Count + 99998);
						int c1 = LevyProgressionSystem.Levies;
						Log($"LEVIES past Moon Lord with only Wall of Flesh and Moon Lord recorded: {c0}; after one late modded boss: {c1}");
						Check("levies: a late boss adds only its own worth, not the rest of the tier", c1 >= c0 && c1 <= c0 + 3, $"{c0}->{c1}");
						Main.hardMode = false; NPC.downedMoonlord = false;
						LevyProgressionSystem.OnBossRecorded("Test:Refresh", NPCID.KingSlime);
						Check("levies: the stage cap holds before hardmode", LevyProgressionSystem.Levies <= lc.LeviesByEndOfPreHardmode, $"{LevyProgressionSystem.Levies}");
					}
					finally
					{
						Main.hardMode = hm; NPC.downedMoonlord = ml; NPC.downedSlimeKing = ks; NPC.downedBoss1 = eoc;
						lc.CapLeviesByStage = oldCap; lc.LeviesAtStart = oldStart;
					}
				}
			}
			catch (Exception e) { Check("no exception", false, e.ToString()); }
			finally { AuditCore.Unhook(); Main.player[0] = oldPlayer; }
			Mod.Logger.Info($"[LootAudit] self-check pass={pass} fail={fail}");
		}
	}
}
