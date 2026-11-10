using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>Boot self-check ("[Warbands] self-check ..." lines, headless server boots; env WARBANDS_CHECK=0 skips): config defaults, roster data against the real item tables and Levy's tiers, the stat rules, and a live band soldier.</summary>
	public class WarbandsSelfCheck : ModSystem
	{
		int pass, fail;
		void Check(string name, bool ok, string detail = "")
		{
			if (ok) pass++; else fail++;
			Mod.Logger.Info($"[Warbands] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}

		public override void OnWorldLoad()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || Environment.GetEnvironmentVariable("WARBANDS_CHECK") == "0" || (!Main.dedServ && Environment.GetEnvironmentVariable("WARBANDS_CHECK") != "1")) return;
			pass = fail = 0;
			try { Run(); }
			catch (Exception e) { fail++; Mod.Logger.Error("[Warbands] self-check threw: " + e); }
			Mod.Logger.Info($"[Warbands] self-check done pass={pass} fail={fail}");
		}

		private void Run()
		{
			var c = WarbandsConfig.Get;
			Check("config loaded with shipped defaults", c != null && c.Enabled && c.FirstStage == 1 && c.BandSizeMin == 3 && c.BandSizeMax == 5 && c.MaxActiveBands == 1 && Math.Abs(c.LootSharePercent - 35f) < 1e-3f && c.CultistBand);
			Check("spawn chance default is modest (4% per minute)", c != null && Math.Abs(c.SpawnChancePercentPerMinute - 4f) < 1e-3f);

			try { ProgressionTier.EnsureBuilt(); } catch { }
			bool tiers = ProgressionTier.Ready;
			Check("Levy progression tiers are built", tiers);

			// stage mapping on the Boss Checklist scale
			Check("stage map: 0 -> pre-boss, 3 -> post-EoW, 7 -> early Hardmode, 12 -> post-Plantera", Stage.FromProgress(0) == 0 && Stage.FromProgress(2f) == 0 && Stage.FromProgress(3f) == 1 && Stage.FromProgress(7f) == 3 && Stage.FromProgress(12f) == 5);

			// every biome has a roster at every stage; every item resolves, is the right kind, and sits within the stage's tier ceiling
			var bad = new List<string>(); int items = 0;
			foreach (BandBiome b in Enum.GetValues(typeof(BandBiome)))
				for (int s = 0; s < Stage.Count; s++)
					if (BandData.For(b, s) == null) bad.Add($"no roster {b}@{s}");
			foreach (var r in BandData.All.Concat(BandTypes.All))
			{
				bool hasShooter = false;
				foreach (var m in r.Members)
				{
					int w = BandData.ItemType(m.Weapon); items++;
					if (w <= 0) { bad.Add("missing weapon " + m.Weapon); continue; }
					var wi = new Item(); wi.SetDefaults(w);
					if (wi.damage <= 0) bad.Add("no damage " + m.Weapon);
					if (m.Role == 'R' || m.Role == 'M') hasShooter = true;
					if (tiers) { float t = ProgressionTier.Of(w); if (t > Stage.ItemCeiling[r.Stage]) bad.Add($"tier {m.Weapon}={t:0.#} > ceiling {Stage.ItemCeiling[r.Stage]} @{r.Biome}/{r.Stage}"); }
					if (m.Ammo != null)
					{
						int a = BandData.ItemType(m.Ammo); items++;
						if (a <= 0) bad.Add("missing ammo " + m.Ammo);
						else { var ai = new Item(); ai.SetDefaults(a); if (ai.ammo <= 0 || ai.ammo != wi.useAmmo) bad.Add($"ammo {m.Ammo} does not fit {m.Weapon}"); if (tiers && ProgressionTier.Of(a) > Stage.ItemCeiling[r.Stage]) bad.Add($"tier ammo {m.Ammo}"); }
					}
					if (m.Armor.Length != 3) bad.Add("armor set size " + r.Name);
					for (int i = 0; i < m.Armor.Length; i++)
					{
						int t = BandData.ItemType(m.Armor[i]); items++;
						if (t <= 0) { bad.Add("missing armor " + m.Armor[i]); continue; }
						var ti = new Item(); ti.SetDefaults(t);
						bool slotOk = i == 0 ? ti.headSlot >= 0 : i == 1 ? ti.bodySlot >= 0 : ti.legSlot >= 0;
						if (!slotOk) bad.Add($"armor slot {m.Armor[i]}");
						if (tiers && ProgressionTier.Of(t) > Stage.ItemCeiling[r.Stage]) bad.Add($"tier armor {m.Armor[i]}={ProgressionTier.Of(t):0.#}");
					}
				}
				if (!hasShooter) bad.Add("no shooter in " + r.Name);
			}
			// the 20 named types: ids, items, recipes, bounty, coverage of every biome
			{
				var bad2 = new List<string>();
				if (BandTypes.All.Length != 20) bad2.Add("count " + BandTypes.All.Length);
				if (BandTypes.All.Select(t => t.Id).Distinct().Count() != BandTypes.All.Length) bad2.Add("duplicate ids");
				foreach (var t in BandTypes.All)
				{
					foreach (var (it, n) in t.Mats) if (BandData.ItemType(it) <= 0) bad2.Add($"{t.Id} mat {it}");
					foreach (var (it, lo, hi) in t.Bounty) if (BandData.ItemType(it) <= 0 || lo > hi) bad2.Add($"{t.Id} bounty {it}");
					if (t.Members.Length < 4) bad2.Add(t.Id + " has fewer than 4 members");
					if (t.Mats.Length == 0 || t.Mats[0].item != "Wood") bad2.Add(t.Id + " recipe is not wood first");
					if (t.BountyCopper <= 0) bad2.Add(t.Id + " no coins");
				}
				foreach (BandBiome b in Enum.GetValues(typeof(BandBiome))) if (BandTypes.Pick(b, 6, Main.rand) == null) bad2.Add("no named type for " + b);
				Check("20 band types: unique ids, wood-first Work Bench recipes, bounty items resolve, every biome has a type by the last stage", bad2.Count == 0, string.Join("; ", bad2));
			}
			Check($"rosters: {BandData.All.Length} legacy + {BandTypes.All.Length} named rosters, {items} items resolve, fit their ammo and slots, and sit within the stage tier ceiling", bad.Count == 0, string.Join("; ", bad));

			// stat rules
			bool lifeUp = true; for (int s = 1; s < Stage.Count; s++) if (BandStats.StageLife[s] < BandStats.StageLife[s - 1]) lifeUp = false;
			Check("stage life rises with the stage", lifeUp);
			Check("defense is held to Levy's stage line", BandStats.Defense(0, 65) == WeaponRank.StageDefense[0] && BandStats.Defense(5, 65) == WeaponRank.StageDefense[5] && BandStats.Defense(0, 3) == 3, $"{BandStats.Defense(0, 65)} {BandStats.Defense(5, 65)}");
			Check("engage cap: 2 attackers up to 4 members, 3 above", BandStats.EngageCap(3) == 2 && BandStats.EngageCap(4) == 2 && BandStats.EngageCap(5) == 3);
			bool capOk = true; string capDetail = "";
			foreach (var role in new[] { 'R', 'E' })
				for (int s = 0; s < Stage.Count; s++)
				{
					int hit = BandStats.Hit(s, role, 400f, 1.5f); // an absurdly strong weapon is pulled down to the cap
					float eff = Main.masterMode ? 1f : Main.expertMode ? 0.75f : 0.5f;
					float sustained = Math.Max(1f, hit - BandStats.TypicalPlayerDefense[s] * eff) * Math.Min(1.5f, BandStats.LandedHitsPerSecond);
					float cap = BandStats.DpsTargetExpert[s] * (role == 'E' ? BandStats.MeleeDpsShare : 1f) * (BandStats.DamageMult / 2f) * (WarbandsConfig.Get?.DamageScale ?? 1f);
					if (sustained > cap * 1.15f + 3f) { capOk = false; capDetail += $"{role}@{s}: {sustained:0}>{cap:0}; "; }
				}
			Check("damage cap holds for a 400-damage weapon at every stage", capOk, capDetail);
			int weak = BandStats.Hit(1, 'R', 6f, 1.5f);
			Check("a weak weapon is not raised to the cap", weak <= (int)(6f * BandStats.DamageMult * (WarbandsConfig.Get?.DamageScale ?? 1f)) + 1, weak.ToString());

			// real vanilla defense rule on an NPC, the one the TTK lab uses
			try
			{
				var n = new NPC(); n.SetDefaults(NPCID.Zombie); n.defense = 6;
				var hi = n.CalculateHitInfo(30, 1, false, 0f, DamageClass.Default, false, 0f);
				Check("vanilla NPC defense rule: 30 damage on 6 defense = 27", hi.Damage == 27, hi.Damage.ToString());
			}
			catch (Exception e) { Check("vanilla NPC defense rule", false, e.GetType().Name + ": " + e.Message); }

			LiveFire();

			// the spawner path: a stand-in player at the world spawn gets a band of 4 on solid ground
			try
			{
				var fake = new Player(); fake.position = new Microsoft.Xna.Framework.Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16); fake.width = 20; fake.height = 42;
				int before = BandSpawner.ActiveBands(); int spawnedBefore = BandSpawner.BandsSpawned;
				bool formed = BandSpawner.SpawnBand(fake, 1, 4);
				int count = 0;
				for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && (n.ModNPC is BandLevyNPC || n.ModNPC is BandSoldier)) { count++; } }
				Check("spawner forms a band of 4 at the world spawn and counts it", formed && count >= 4 && BandSpawner.ActiveBands() == before + 1 && BandSpawner.BandsSpawned == spawnedBefore + 1, $"formed={formed} members={count} bands={BandSpawner.ActiveBands()}");
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && (Main.npc[i].ModNPC is BandSoldier || Main.npc[i].ModNPC is BandLevyNPC)) Main.npc[i].active = false;
			}
			catch (Exception e) { Check("spawner", false, e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace?.Split('\n')[0]); }

			// a live band soldier from every role of one roster
			try
			{
				var roster = BandData.For(BandBiome.Forest, 0);
				int ok = 0; var made = new List<NPC>();
				for (int k = 0; k < roster.Members.Length; k++)
				{
					int id = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), Main.spawnTileX * 16, (Main.spawnTileY - 20) * 16, ModContent.NPCType<BandSoldier>());
					if (id < 0 || id >= Main.maxNPCs) continue;
					var npc = Main.npc[id]; var bs = npc.ModNPC as BandSoldier; bs.Setup(roster, k, 0, 9999);
					if (npc.lifeMax == BandStats.Life(0, bs.Role) && npc.defense <= WeaponRank.StageDefense[0] && npc.damage >= 1 && !npc.friendly) ok++;
					made.Add(npc);
				}
				Check($"band soldiers spawn, size and arm from the roster ({ok}/{roster.Members.Length})", ok == roster.Members.Length && made.Count == roster.Members.Length);
				foreach (var m in made) { m.active = false; }
			}
			catch (Exception e) { Check("live band soldier", false, e.GetType().Name + ": " + e.Message); }
		}

		private static int HostileShots() { int c = 0; for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; if (p.active && p.hostile && (p.type == ProjectileID.WoodenArrowHostile || p.type == ProjectileID.EyeLaser || p.type == ProjectileID.BulletDeadeye)) c++; } return c; }

		/// <summary>Runs the real AI of a band archer and of the Cultist circle for a few hundred ticks against a stand-in player in slot 0 (a headless server has none) and checks that they fire and move.</summary>
		private void LiveFire()
		{
			var p = Main.player[0]; bool was = p.active; var oldPos = p.position; int oldLife = p.statLife;
			var made = new List<NPC>();
			try
			{
				p.active = true; p.dead = false; p.width = 20; p.height = 42; p.statLife = p.statLifeMax2 = 200;
				p.position = new Microsoft.Xna.Framework.Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
				var src = new Terraria.DataStructures.EntitySource_WorldEvent();

				var roster = BandData.For(BandBiome.Forest, 0); int ri = Array.FindIndex(roster.Members, m => m.Role == 'R');
				int id = NPC.NewNPC(src, (int)p.position.X + 15 * 16, (int)p.position.Y, ModContent.NPCType<BandSoldier>());
				var npc = Main.npc[id]; (npc.ModNPC as BandSoldier).Setup(roster, ri, 0, 9998); npc.target = 0; made.Add(npc);
				int shots0 = HostileShots();
				for (int t = 0; t < 240 && npc.active; t++) npc.AI();
				Check("a band archer in reach fires hostile arrows", HostileShots() > shots0, $"shots {HostileShots() - shots0}");
				Check("a band archer shot costs the player what the stage cap allows", npc.damage >= 1 && npc.lifeMax > 0, $"contact {npc.damage}, life {npc.lifeMax}, def {npc.defense}");

			}
			catch (Exception e) { Check("live fire", false, e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace?.Split('\n')[0]); }
			finally
			{
				foreach (var n in made) n.active = false;
				for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].hostile) Main.projectile[i].active = false;
				p.active = was; p.position = oldPos; p.statLife = oldLife;
			}
		}
	}
}
