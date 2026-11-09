using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Config;
using Levy.Common.Events;
using Levy.Common.Fear;
using Levy.Common.Options;
using Levy.Common.Util;
using Levy.Common.Vanity;
using Levy.Content.Buffs;
using Levy.Content.Items;
using Levy.Content.NPCs;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Boot self-check for the orders / radio / hazard / boss-scaling / fear / i-frame / event / sitting / lens pass ("[Orders] self-check ... fail=N", headless server boots; LEVY_ORDERSCHECK=0 skips).
	/// Pure rules and numbers only: what each change does is measured here against the numbers it replaced.
	/// </summary>
	public class OrdersSelfCheck : ModSystem
	{
		private int pass, fail;
		private void Check(string name, bool ok, string detail = "")
		{
			if (ok) pass++; else fail++;
			Mod.Logger.Info($"[Orders] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}

		public override void PostSetupRecipes() => LoadSafe.Run(Run);

		private void Run()
		{
			string env = System.Environment.GetEnvironmentVariable("LEVY_ORDERSCHECK");
			if (env == "0" || (!Main.dedServ && env != "1")) return;
			pass = fail = 0;
			try
			{
				// 1. Search & Destroy teleports to the commander; Free Rein never does
				Check("1 only Search & Destroy catches up", SoldierNPC.CatchUpOrder(SoldierOrder.SearchDestroy) && !SoldierNPC.CatchUpOrder(SoldierOrder.FreeRein) && !SoldierNPC.CatchUpOrder(SoldierOrder.Hold) && !SoldierNPC.CatchUpOrder(SoldierOrder.GoTo));
				Check("1 far + unseen + off cooldown hops", SoldierNPC.SearchCatchUpDue(61f, 180, false));
				Check("1 inside 60 tiles does not hop", !SoldierNPC.SearchCatchUpDue(60f, 180, false));
				Check("1 in view or on cooldown does not hop", !SoldierNPC.SearchCatchUpDue(200f, 180, true) && !SoldierNPC.SearchCatchUpDue(200f, 179, false));

				// 2. the radio reaches every levy in the world
				var radioOwner = new Player(); radioOwner.inventory[3].SetDefaults(ModContent.ItemType<CommandRadio>());
				var plain = new Player();
				Check("2 a Command Radio in the pack is found", CommandRadio.Has(radioOwner) && !CommandRadio.Has(plain));
				Check("2 earshot only without the radio", !RosterSystem.OrderReaches(false, false, false, false) && RosterSystem.OrderReaches(false, true, false, false));
				Check("2 the radio reaches a levy out of earshot", RosterSystem.OrderReaches(false, false, false, true), "OrderRadius " + LevyConstants.OrderRadius + " tiles -> every levy");
				Check("2 radio rally covers a 8400-tile world", LevyUtil.WithinTiles(Vector2.Zero, new Vector2(8400f * 16f, 2400f * 16f), LevyConstants.RadioRallyRadius) && !LevyUtil.WithinTiles(Vector2.Zero, new Vector2(8400f * 16f, 0f), LevyConstants.RallyRadius));

				// 3. tile hazards: off by default, spikes / thorns / meteorite / hellstone hurt by the player's tile sets
				Check("3 hazards default off", !LevyConfig.Get.SoldiersTakeTileHazards);
				Check("3 spikes, wooden spikes, thorns, meteorite, hellstone hurt; dirt does not",
					SoldierNPC.HurtsOnTouch(TileID.Spikes) && SoldierNPC.HurtsOnTouch(TileID.WoodenSpikes) && SoldierNPC.HurtsOnTouch(TileID.JungleThorns) && SoldierNPC.HurtsOnTouch(TileID.CorruptThorns)
					&& SoldierNPC.HurtsOnTouch(TileID.CrimsonThorns) && SoldierNPC.HurtsOnTouch(TileID.Meteorite) && SoldierNPC.HurtsOnTouch(TileID.Hellstone) && !SoldierNPC.HurtsOnTouch(TileID.Dirt));

				// 4. boss HP scaling reaches modded bosses
				NPC fake = Main.npc[Main.maxNPCs - 1];
				bool wasActive = fake.active;
				try
				{
					fake.SetDefaults(NPCID.Zombie); fake.active = true; fake.whoAmI = Main.maxNPCs - 1; fake.lifeMax = fake.life = 1000;
					bool plainRoot = BossRule.TryGetScalingRoot(fake, out _, out _);
					fake.boss = true;
					bool modRoot = BossRule.TryGetScalingRoot(fake, out NPC root, out _) && ReferenceEquals(root, fake);
					Check("4 a non-boss gets no scaling root, a boss outside the vanilla list does", !plainRoot && modRoot, "vanilla list only before: modded boss root = false");
					Check("4 +2%/CP, cap 80%: 10 CP on 1000 life = +200 (199 after truncation), 60 CP = +800", System.Math.Abs(BossRule.ScaledAdd(1000, 10, 0.02f, 0.8f) - 200) <= 1 && BossRule.ScaledAdd(1000, 60, 0.02f, 0.8f) == 800);
					BossRule.ApplyScaling(fake, 0.2f);
					Check("4 applying 20% raises max and current life of the modded boss", fake.lifeMax == 1200 && fake.life == 1200, fake.lifeMax + "/" + fake.life);
				}
				finally { fake.boss = false; fake.active = wasActive; }

				// 5. fear: events, fearless only with the Spectator buff
				var spec = new Player(); spec.active = true; spec.AddBuff(ModContent.BuffType<SpectatorBuff>(), 600);
				var norm = new Player(); norm.active = true;
				Check("5 the Spectator buff makes the commander's levies fearless, nothing else does", FearSystem.ImmuneCommander(spec) && !FearSystem.ImmuneCommander(norm) && !FearSystem.ImmuneCommander(null));
				Check("5 shipped fear model is Events", LevyOptions.Fear == FearModel.Events || System.Environment.GetEnvironmentVariable("LEVY_FEARMODEL") != null);
				Check("5 an event jolts 25 -> 27.5 for a Greenhorn, never past the cap " + LevyConstants.EventFearCap, System.Math.Abs(FearSystem.EventFear(0f, 25f, 1.1f) - 27.5f) < 1e-4f && FearSystem.EventFear(40f, 25f, 1f) == LevyConstants.EventFearCap && FearSystem.EventFear(48f, 25f, 1f) == 48f);
				Check("5 Martian Madness frightens most, the Goblin Army least", FearEventSystem.InvasionFear(InvasionID.MartianMadness) > FearEventSystem.InvasionFear(InvasionID.PirateInvasion) && FearEventSystem.InvasionFear(InvasionID.PirateInvasion) > FearEventSystem.InvasionFear(InvasionID.GoblinArmy));
				Check("5 event fear stays under the Shaken threshold " + LevyConstants.Fear.ShakenThreshold, LevyConstants.EventFearCap < LevyConstants.Fear.ShakenThreshold);

				// 6. immunity frames 40 / 20 / 80 -> 30 / 15 / 60
				Check("6 hit immunity 40 -> 30, 20 -> 15, 80 -> 60 ticks", LevyConstants.ScaledImmune(40) == 30 && LevyConstants.ScaledImmune(20) == 15 && LevyConstants.ScaledImmune(80) == 60 && LevyConstants.ScaledImmune(0) == 0);

				// 7. event pressure
				Check("7 spawn rate x1 -> x2.0 base, x2.6 at 60 CP, capped at x4", EventPressure.SpawnRateMult(0) == 2.0f && System.Math.Abs(EventPressure.SpawnRateMult(60) - 2.6f) < 1e-4f && EventPressure.SpawnRateMult(5000) == 4.0f);
				Check("7 a 1-in-600 spawn becomes 1-in-230 at 60 CP; the enemy cap 5 becomes 13", EventPressure.SpawnRate(600, 2.6f) == 230 && EventPressure.MaxSpawns(5, 2.6f) == 13 && EventPressure.MaxSpawns(5, 4f) == 20);
				Check("7 invasion size x1 -> x3.0 base, x4.2 at 60 CP (Goblin Army 80 -> 336), capped at x6", EventPressure.InvasionSizeMult(0) == 3.0f && (int)(80 * EventPressure.InvasionSizeMult(60)) == 336 && EventPressure.InvasionSizeMult(5000) == 6.0f);

				// 8. idle sitting
				Check("8 sitting is on by default", LevyConfig.Get.LeviesSitWhenIdle);
				Check("8 a calm levy sits once calm long enough", SoldierNPC.SitDue(600, 600) && !SoldierNPC.SitDue(599, 600));
				Check("8 a sitting levy chats within " + LevyConstants.SitChatTiles + " tiles, waking range " + LevyConstants.SitWakeEnemyTiles, LevyConstants.SitChatTiles > 0 && LevyConstants.SitWakeEnemyTiles > 0);

				// 9. Spectral Lens
				Check("9 85% transparent", System.Math.Abs(LevyConstants.LensOpacity - 0.15f) < 1e-6f && LensDraw.Fade(Color.White).A == 38);
				int lens = ModContent.ItemType<SpectralLens>();
				Recipe r = null; foreach (Recipe x in Main.recipe) if (x != null && x.createItem != null && x.createItem.type == lens) { r = x; break; }
				bool recipeOk = r != null && r.requiredTile.Contains(TileID.WorkBenches) && r.requiredItem.Count == 3
					&& r.requiredItem.Exists(i => i.type == ItemID.InvisibilityPotion && i.stack == 2) && r.requiredItem.Exists(i => i.type == ItemID.Goggles && i.stack == 1) && r.acceptedGroups.Count == 1;
				Check("9 recipe: any grave marker + 2 Invisibility Potions + Goggles at a Work Bench", recipeOk);
				var item = new Item(); item.SetDefaults(lens);
				Check("9 it is a vanity accessory", item.accessory && item.vanity);
			}
			catch (System.Exception e) { Check("no exception", false, e.ToString()); }
			Mod.Logger.Info($"[Orders] self-check pass={pass} fail={fail}");
		}
	}
}
