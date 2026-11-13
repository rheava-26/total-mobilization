using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Levy.Common.Systems;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Content.TileEntities;
using Mechanization.Common;
using R = Mechanization.Common.MechConstants.Relay;

namespace Mechanization.Content.Relay
{
	/// <summary>
	/// Boot-time self-check of the Armory Relay over synthetic tile entities (a headless boot loads no world): a relay links to an Armory 100 tiles away and to one 5,000 tiles away; a spot inside the serve radius
	/// sees that Armory and a spot outside does not; the nearest of several Armories wins; several relays pool without duplicates; registration and text exist.
	/// </summary>
	public class RelayWiringCheck : ModSystem
	{
		private static ArmoryTE Armory(int tx, int ty) => new ArmoryTE { Position = new Point16(tx, ty) };
		private static ArmoryRelayTE Relay(int tx, int ty) => new ArmoryRelayTE { Position = new Point16(tx, ty) };

		/// <summary>
		/// World-load check (test boots only, LEVY_SELFCHECK_WORLD=1) of the Armory Relay against Levy's 2.0.39 pylon-network rule, with real tile entities in the loaded world (a Barracks and its Armory, a relay, a pylon list):
		/// the pylon stands inside the settlement area but over 60 tiles from the Armory, so only the settlement rule puts the Armory on the network; a relay 1,596 tiles away must still link to it, with or without a pylon beside it.
		/// Logged as "[RelayNet] self-check pass=N fail=N". Everything it makes is removed again.
		/// </summary>
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok) { if (ok) pass++; else fail++; Mod.Logger.Info($"[RelayNet] {(ok ? "PASS" : "FAIL")} {name}"); }
			var made = new List<TileEntity>();
			try
			{
				int y = 300;
				var bar = new BarracksTE { Position = new Point16(2000, y), TownName = "RelayCheck" }; bar.ID = 900101; TileEntity.ByID[bar.ID] = bar; made.Add(bar);
				var arm = new ArmoryTE { Position = new Point16(2004, y) }; arm.ID = 900102; TileEntity.ByID[arm.ID] = arm; made.Add(arm);
				var relay = new ArmoryRelayTE { Position = new Point16(3600, y) }; relay.ID = 900103; TileEntity.ByID[relay.ID] = relay; made.Add(relay); // 1,596 tiles from the Armory
				TeleportPylonInfo Py(int x) => new TeleportPylonInfo { PositionInTiles = new Point16(x, y), TypeOfPylon = TeleportPylonType.SurfacePurity };
				var sys = SettlementSystem.Instance;
				SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(2100), Py(3605) }; // one inside the settlement box (100 tiles from the barracks, 96 from the armory), one beside the relay
				Check("the Armory is on the network by the settlement rule although no pylon is within 60 tiles of it", sys.IsArmoryOnNetwork(arm) && !ArmoryLink.NearPylon(arm.Center, SettlementSystem.PylonListOverride));
				ArmoryRelays.Invalidate();
				Check("the relay at a pylon links to the Armory 1,596 tiles away", ReferenceEquals(ArmoryRelays.LinkOf(relay), arm));
				var pool = new List<ArmoryTE>(); ArmoryLink.Find(relay.Center + new Vector2(16f * 5, 0f), pool);
				Check("a turret beside the relay draws from that Armory", pool.Contains(arm));
				SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(3605) };
				Check("with no pylon at the home settlement the far relay still links to the Armory", ReferenceEquals(ArmoryRelays.LinkOf(relay), arm));
				SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(2100) };
				Check("a relay with no pylon of its own still links to the Armory", ReferenceEquals(ArmoryRelays.LinkOf(relay), arm));
				SettlementSystem.PylonListOverride = null;
				Check("a relay with no pylon in the world links to the Armory", ReferenceEquals(ArmoryRelays.LinkOf(relay), arm));
				var cached = ArmoryRelays.LinkedFor(relay.Center); // repeated reads inside one refresh window reuse the cached lists
				Check("the cached relay lookup returns the same Armory for a spot at the relay", cached.Count == 1 && ReferenceEquals(cached[0], arm));
				Check("a spot 60 tiles from the relay is outside its 50-tile serve radius", ArmoryRelays.LinkedFor(relay.Center + new Vector2(16f * 60, 0f)).Count == 0);
				Check("a spot 45 tiles from the relay is inside its 50-tile serve radius", ArmoryRelays.LinkedFor(relay.Center + new Vector2(16f * 45, 0f)).Count == 1);
			}
			catch (Exception e) { Check("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { SettlementSystem.PylonListOverride = null; foreach (var t in made) TileEntity.ByID.Remove(t.ID); ArmoryRelays.Invalidate(); }
			Mod.Logger.Info($"[RelayNet] self-check pass={pass} fail={fail}");
		}

		public override void PostAddRecipes()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			try
			{
				Check(ModContent.TileType<ArmoryRelayTile>() > 0 && ModContent.ItemType<ArmoryRelayItem>() > 0, "relay tile and item are registered");
				foreach (string k in new[] { "Mods.Mechanization.Items.ArmoryRelayItem.DisplayName", "Mods.Mechanization.Items.ArmoryRelayItem.Tooltip", "Mods.Mechanization.UI.RelayLinked", "Mods.Mechanization.UI.RelayUnlinked" })
					Check(Language.Exists(k), "missing text " + k);
				Check(R.ServeRadiusTiles == 50f && R.RefreshTicks > 0, "relay tunables are sane (50-tile serve radius)");

				ArmoryRelayTE relay = Relay(1000, 500);
				ArmoryTE far = Armory(1100, 500);         // 100 tiles from the relay
				var armories = new List<ArmoryTE> { far };
				var relays = new List<ArmoryRelayTE> { relay };

				Check(ReferenceEquals(ArmoryRelays.Link(relay.Center, armories), far), "relay links to the Armory 100 tiles away");

				Vector2 inside = relay.Center + new Vector2((R.ServeRadiusTiles - 5f) * 16f, 0f);
				Vector2 outside = relay.Center + new Vector2((R.ServeRadiusTiles + 5f) * 16f, 0f);
				var got = new List<ArmoryTE>();
				ArmoryRelays.LinkedFor(inside, relays, armories, got);
				Check(got.Count == 1 && ReferenceEquals(got[0], far), "a turret inside the relay radius sees the Armory 100 tiles away");
				got.Clear();
				ArmoryRelays.LinkedFor(outside, relays, armories, got);
				Check(got.Count == 0, "a turret outside the relay radius does not");

				// nearest wins; distance is unlimited
				ArmoryTE nearer = Armory(1040, 500), beyond = Armory(6000, 500);
				Check(ReferenceEquals(ArmoryRelays.Link(relay.Center, new[] { far, nearer, beyond }), nearer), "the nearest Armory wins");
				Check(ReferenceEquals(ArmoryRelays.Link(relay.Center, new[] { beyond }), beyond), "an Armory 5,000 tiles away still links");
				Check(ArmoryRelays.Link(relay.Center, new ArmoryTE[0]) == null, "no Armory in the world links to nothing");

				// two relays on the same Armory pool it once; a second relay on another Armory adds it
				ArmoryRelayTE relay2 = Relay(1020, 500);
				got.Clear();
				ArmoryRelays.LinkedFor(relay.Center, new[] { relay, relay2 }, armories, got);
				Check(got.Count == 1, "two relays on one Armory do not stack");
				ArmoryTE armA = Armory(1000, 450), armB = Armory(1020, 550);   // each is the nearest to a different relay
				got.Clear();
				ArmoryRelays.LinkedFor(relay.Center + new Vector2(80f, 0f), new[] { relay, relay2 }, new[] { armA, armB }, got);
				Check(got.Count == 2, "overlapping relays on different Armories pool both");
			}
			catch (Exception e) { failures.Add($"threw {e.GetType().Name}: {e.Message}"); }

			if (failures.Count == 0) Mod.Logger.Info($"Mechanization: armory relay check OK ({ok} checks): registration, text, 100-tile link, serve radius in/out, nearest, 5,000-tile link, no Armory, no stacking.");
			else foreach (string f in failures) Mod.Logger.Warn("Mechanization: armory relay check FAILED: " + f);
		}
	}
}
