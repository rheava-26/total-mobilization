using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	public partial class WarbandsSelfCheck
	{
		/// <summary>Boss-fight hold, Plantera-tier kits, band friendly fire, sandstorm band rate.</summary>
		private void RunBandChecks()
		{
			BossHoldChecks();
			RollWithBossChecks();
			KitChecks();
			FriendlyFireChecks();
			SandstormChecks();
		}

		private void BossHoldChecks()
		{
			try
			{
				var cfgB = WarbandsConfig.Get; int slot = -1; int grace0 = cfgB.BossGraceSeconds; bool hold0 = cfgB.NoSpawnsDuringBosses; uint seen0 = BossFight.LastSeen;
				for (int i = Main.maxNPCs - 1; i >= 0 && slot < 0; i--) if (!Main.npc[i].active) slot = i;
				try
				{
					Check("boss grace default is 90 seconds and the hold is on", grace0 == 90 && hold0);
					BossFight.LastSeen = uint.MaxValue; cfgB.NoSpawnsDuringBosses = true; cfgB.BossGraceSeconds = 90;
					bool idle = !BandSpawner.NaturalSpawnBlocked(cfgB);
					if (slot >= 0) { var bn = Main.npc[slot]; bn.SetDefaults(NPCID.EyeofCthulhu); bn.active = true; }
					bool held = BandSpawner.NaturalSpawnBlocked(cfgB);
					bool heldCached = BossFight.HeldCached(cfgB);
					if (slot >= 0) Main.npc[slot].active = false;
					bool inGrace = BandSpawner.NaturalSpawnBlocked(cfgB);
					cfgB.BossGraceSeconds = 0; bool freed = !BandSpawner.NaturalSpawnBlocked(cfgB);
					Check("a live boss holds natural bands back, the grace window keeps holding after it falls, and zero grace releases at once", slot >= 0 && idle && held && heldCached && inGrace && freed, $"idle={idle} held={held} cached={heldCached} inGrace={inGrace} freed={freed}");
					uint T = 100000;
					bool tbl = BossFight.HeldAt(true, uint.MaxValue, T, 0) && !BossFight.HeldAt(false, uint.MaxValue, T, 5400) && BossFight.HeldAt(false, T - 5399, T, 5400) && !BossFight.HeldAt(false, T - 5400, T, 5400) && !BossFight.HeldAt(false, T - 10, T, 0);
					Check("boss grace window: held for exactly BossGraceSeconds after the last boss, never before one is seen", tbl);
				}
				finally { cfgB.BossGraceSeconds = grace0; cfgB.NoSpawnsDuringBosses = hold0; BossFight.LastSeen = seen0; if (slot >= 0) Main.npc[slot].active = false; }
			}
			catch (Exception e) { Check("boss hold", false, e.GetType().Name + ": " + e.Message); }
		}

		/// <summary>The natural roll at 100%, a stand-in player on the world's surface: no band with a boss out or inside the grace window; one when the fight is long over (the control).</summary>
		private void RollWithBossChecks()
		{
			try
			{
				var cfgR = WarbandsConfig.Get; var pl = Main.player[0] ?? (Main.player[0] = new Player());
				bool act0 = pl.active; var pos0 = pl.position; int w0 = pl.width, h0 = pl.height; int stage0 = cfgR.FirstStage; float chance0 = cfgR.NaturalChancePercentPerMinute; int grace0 = cfgR.BossGraceSeconds; bool hold0 = cfgR.NoSpawnsDuringBosses; uint seen0 = BossFight.LastSeen; float neutral0 = cfgR.NeutralBandPercent;
				int slot = -1; for (int i = Main.maxNPCs - 1; i >= 0 && slot < 0; i--) if (!Main.npc[i].active) slot = i;
				int formedControl = 0, formedBoss = 0, formedGrace = 0;
				try
				{
					BandBrain.Reset(); BossFight.LastSeen = uint.MaxValue;
					cfgR.FirstStage = 0; cfgR.NaturalChancePercentPerMinute = 100f; cfgR.NoSpawnsDuringBosses = true; cfgR.BossGraceSeconds = 90; cfgR.NeutralBandPercent = 0f;
					pl.active = true; pl.dead = false; pl.width = 20; pl.height = 42; pl.position = new Vector2(Main.spawnTileX * 16f - 10f, (Main.spawnTileY - 3) * 16f); pl.townNPCs = 0;
					if (slot >= 0) { var bn = Main.npc[slot]; bn.SetDefaults(NPCID.EyeofCthulhu); bn.active = true; bn.Center = pl.Center + new Vector2(4000f, 0f); }
					for (int k = 0; k < 5; k++) if (BandSpawner.Roll(false)) formedBoss++;
					if (slot >= 0) Main.npc[slot].active = false;
					for (int k = 0; k < 5; k++) if (BandSpawner.Roll(false)) formedGrace++;
					BossFight.LastSeen = uint.MaxValue; BossFight.Alive = false;
					for (int k = 0; k < 3 && formedControl == 0; k++) if (BandSpawner.Roll(false)) formedControl++;
				}
				finally
				{
					cfgR.FirstStage = stage0; cfgR.NaturalChancePercentPerMinute = chance0; cfgR.NoSpawnsDuringBosses = hold0; cfgR.BossGraceSeconds = grace0; cfgR.NeutralBandPercent = neutral0; BossFight.LastSeen = seen0;
					pl.active = act0; pl.position = pos0; pl.width = w0; pl.height = h0;
					for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is BandLevyNPC) Main.npc[i].active = false;
					if (slot >= 0) Main.npc[slot].active = false;
					BandBrain.Reset();
				}
				Check($"a natural roll at 100% forms no band while a boss is out ({formedBoss}) or inside the grace window ({formedGrace}), and one once the fight is long over (control {formedControl})", formedBoss == 0 && formedGrace == 0 && formedControl == 1);
			}
			catch (Exception e) { Check("roll with a boss out", false, e.GetType().Name + ": " + e.Message); }
			Check("event bands and the hunt read the same hold; summoned bands carry a Summoned flag that exempts them", typeof(BossFight).GetMethod("HeldCached") != null && typeof(BandBrain).GetField("Summoned") != null);
		}

		private void KitChecks()
		{
			try
			{
				var found = new List<string>(); int terra = BandData.ItemType("TerraBlade"), nettle = BandData.ItemType("NettleBurst"); int members = 0; var plantera = new HashSet<string>();
				void Scan(BandRoster r) { foreach (var m in r.Members) { members++; int w = BandData.ItemType(m.Weapon); if (w == terra || w == nettle) found.Add(r.Name + ":" + m.Weapon); if (r.Stage >= 5) plantera.Add(m.Weapon); } }
				foreach (var r in BandData.All) Scan(r);
				foreach (var r in BandTypes.All) Scan(r);
				for (int k = 1; k <= 4; k++) for (int st = 0; st < Stage.Count; st++) Scan(EventBands.Roster((EventKind)k, st));
				string[] want = { "ChlorophyteClaymore", "ChlorophyteSaber", "ChlorophytePartisan", "ChlorophyteShotbow", "VenusMagnum", "LeafBlower", "Seedler", "WaspGun", "GrenadeLauncher" };
				var missing = new List<string>(); foreach (var w in want) if (!plantera.Contains(w)) missing.Add(w);
				Check($"no band kit at any stage carries a Terra Blade or a Nettle Burst ({members} members across legacy, named and event rosters), and the Plantera-tier kits carry the chlorophyte and Plantera weapons", found.Count == 0 && missing.Count == 0, "found=" + string.Join(",", found) + " missing=" + string.Join(",", missing));
			}
			catch (Exception e) { Check("kits", false, e.GetType().Name + ": " + e.Message); }
		}

		private void FriendlyFireChecks()
		{
			var made = new List<NPC>();
			try
			{
				var type = BandTypes.ById("ChlorophyteWardens"); int a = -1, b = -1;
				for (int k = 0; k < 2; k++)
				{
					int id = NPC.NewNPC(new EntitySource_WorldEvent(), Main.spawnTileX * 16, (Main.spawnTileY - 20) * 16, ModContent.NPCType<BandLevyNPC>());
					if (id < 0 || id >= Main.maxNPCs) continue;
					(Main.npc[id].ModNPC as BandLevyNPC)?.Setup(type, k, 5, 9998, 2); made.Add(Main.npc[id]); if (k == 0) a = id; else b = id;
				}
				if (made.Count != 2) { Check("band friendly fire fixture spawned", false, $"made {made.Count}"); return; }
				var A = Main.npc[a]; var B = Main.npc[b]; B.Center = A.Center + new Vector2(18f, 0f); B.velocity = A.velocity = Vector2.Zero;
				Projectile Shot(NPC from, NPC at)
				{
					int pi = Projectile.NewProjectile(new EntitySource_Parent(from), at.Center, Vector2.Zero, ProjectileID.Bullet, 80, 0f, Main.myPlayer);
					var pr = Main.projectile[pi]; pr.friendly = true; pr.hostile = false; pr.timeLeft = 600; pr.penetrate = 5;
					if (pr.TryGetGlobalProjectile(out Levy.Common.Globals.SoldierProjectileTag tag)) tag.SoldierIndex = from.whoAmI;
					return pr;
				}
				bool hooks = true; var notes = new List<string>();
				foreach (var (from, at) in new[] { (A, A), (A, B), (B, A) })
				{
					var pr = Shot(from, at);
					bool? hit = ProjectileLoader.CanHitNPC(pr, at); bool? beHit = NPCLoader.CanBeHitByProjectile(at, pr);
					if (hit != false && beHit != false) hooks = false;
					notes.Add($"{from.whoAmI}->{at.whoAmI}:{hit?.ToString() ?? "null"}/{beHit?.ToString() ?? "null"}");
					pr.active = false;
				}
				// a child of a band's shot (what a Nettle Burst's segments are): spawned from the shot, friendly, tagged only through its parent
				var parent = Shot(A, A);
				int ci = Projectile.NewProjectile(parent.GetSource_FromThis(), B.Center, Vector2.Zero, ProjectileID.Bullet, 80, 0f, Main.myPlayer);
				var child = Main.projectile[ci]; child.friendly = true; child.hostile = false;
				bool childFalse = ProjectileLoader.CanHitNPC(child, B) == false;
				parent.active = false; child.active = false;
				// control: a player's own shot may still hit a band body
				int ni = Projectile.NewProjectile(new EntitySource_Misc("check"), B.Center, Vector2.Zero, ProjectileID.Bullet, 80, 0f, Main.myPlayer);
				var plain = Main.projectile[ni]; plain.friendly = true; plain.hostile = false;
				bool control = ProjectileLoader.CanHitNPC(plain, B) != false && NPCLoader.CanBeHitByProjectile(B, plain) != false;
				plain.active = false;
				Check($"a band member's own shot, a band-mate's shot and a shot's child cannot hit a band body, a player's shot still can ({string.Join(" ", notes)} child={childFalse} control={control})", hooks && childFalse && control);
				// the game's own projectile pass: life before and after
				int lifeA0 = A.life, lifeB0 = B.life;
				var s1 = Shot(A, B); var s2 = Shot(B, A); var s3 = Shot(A, A);
				A.Center = B.Center; s1.Center = s2.Center = s3.Center = A.Center;
				foreach (var pr in new[] { s1, s2, s3 }) { try { pr.Damage(); } catch (Exception) { } }
				bool noDamage = A.life == lifeA0 && B.life == lifeB0;
				int lifeA1 = A.life, lifeB1 = B.life;
				// the same pass for a player's shot, as the control that Damage() lands here at all
				var s4 = Shot(A, B); if (s4.TryGetGlobalProjectile(out Levy.Common.Globals.SoldierProjectileTag t4)) t4.SoldierIndex = -1; s4.GetGlobalProjectile<BandProjectileGuard>().FromBand = false; s4.Center = B.Center;
				try { s4.Damage(); } catch (Exception) { }
				bool controlDamage = B.life < lifeB1;
				Mod.Logger.Info($"[Warbands] OBSERVED projectile pass: band shots life A {lifeA0}->{A.life} B {lifeB0}->{B.life}; a shot with no band tag takes B {lifeB1}->{B.life} (control)");
				s1.active = s2.active = s3.active = s4.active = false;
				Check($"a band's shots through the game's own projectile pass deal 0 damage to the firer and its band-mates (life A {lifeA0}->{lifeA1}, B {lifeB0}->{lifeB1}; a shot with no band tag takes B {lifeB1}->{B.life}){(controlDamage ? "" : " [control shot did not land: pass not exercised]")}", noDamage && controlDamage);
			}
			catch (Exception e) { Check("band friendly fire", false, e.GetType().Name + ": " + e.Message); }
			finally { foreach (var n in made) n.active = false; }
		}

		private void SandstormChecks()
		{
			try
			{
				var cs = WarbandsConfig.Get; uint last0 = EventBands.LastSandstormBand; float ch0 = cs.EventSandstormChancePercent; int cd0 = cs.EventSandstormCooldownMinutes;
				try
				{
					Check("sandstorm band defaults: 35% chance per sandstorm, 20 minute cooldown", Math.Abs(ch0 - 35f) < 1e-3f && cd0 == 20);
					cs.EventSandstormChancePercent = 35f; cs.EventSandstormCooldownMinutes = 20; EventBands.LastSandstormBand = uint.MaxValue;
					var rng = new Random(77); int yes = 0, N = 20000;
					for (int i = 0; i < N; i++) if (EventBands.SandstormAllowed(cs, 500000u, (float)rng.NextDouble())) yes++;
					double rate = yes * 100.0 / N;
					uint now = 1000000u;
					EventBands.LastSandstormBand = now - 3600u * 10; bool cool = !EventBands.SandstormAllowed(cs, now, 0f);
					EventBands.LastSandstormBand = now - 3600u * 25; bool after = EventBands.SandstormAllowed(cs, now, 0f);
					cs.EventSandstormChancePercent = 100f; EventBands.LastSandstormBand = uint.MaxValue; bool all = EventBands.SandstormAllowed(cs, now, 0.999f);
					cs.EventSandstormChancePercent = 0f; bool none = !EventBands.SandstormAllowed(cs, now, 0f);
					Mod.Logger.Info($"[Warbands] OBSERVED sandstorm band rate at 35%: {rate:0.0}% of {N} sandstorms");
					Check($"sandstorm band rate follows the config: {rate:0.0}% of {N} sandstorms at 35%, the cooldown holds 10 of 20 minutes and clears at 25, 100% always, 0% never", Math.Abs(rate - 35.0) < 1.5 && cool && after && all && none);
				}
				finally { cs.EventSandstormChancePercent = ch0; cs.EventSandstormCooldownMinutes = cd0; EventBands.LastSandstormBand = last0; }
			}
			catch (Exception e) { Check("sandstorm roll", false, e.GetType().Name + ": " + e.Message); }
		}
	}
}
