using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Content.TileEntities;
using R = Mechanization.Common.MechConstants.Relay;

namespace Mechanization.Content.Relay
{
	/// <summary>
	/// Boot-time self-check of the Armory Relay over synthetic tile entities (a headless boot loads no world): a relay links to an Armory 100 tiles away; a spot inside the serve radius
	/// sees that Armory and a spot outside does not; the nearest of two Armories wins; one past link reach is ignored; several relays pool without duplicates; registration and text exist.
	/// </summary>
	public class RelayWiringCheck : ModSystem
	{
		private static ArmoryTE Armory(int tx, int ty) => new ArmoryTE { Position = new Point16(tx, ty) };
		private static ArmoryRelayTE Relay(int tx, int ty) => new ArmoryRelayTE { Position = new Point16(tx, ty) };

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
				Check(R.ServeRadiusTiles > 0 && R.LinkReachTiles > R.ServeRadiusTiles, "relay tunables are sane");

				ArmoryRelayTE relay = Relay(1000, 500);
				ArmoryTE far = Armory(1100, 500);         // 100 tiles from the relay
				var armories = new List<ArmoryTE> { far };
				var relays = new List<ArmoryRelayTE> { relay };

				Check(ReferenceEquals(ArmoryRelays.Link(relay.Center, armories, false, null), far), "relay links to the Armory 100 tiles away");

				Vector2 inside = relay.Center + new Vector2((R.ServeRadiusTiles - 5f) * 16f, 0f);
				Vector2 outside = relay.Center + new Vector2((R.ServeRadiusTiles + 5f) * 16f, 0f);
				var got = new List<ArmoryTE>();
				ArmoryRelays.LinkedFor(inside, relays, armories, null, null, got);
				Check(got.Count == 1 && ReferenceEquals(got[0], far), "a turret inside the relay radius sees the Armory 100 tiles away");
				got.Clear();
				ArmoryRelays.LinkedFor(outside, relays, armories, null, null, got);
				Check(got.Count == 0, "a turret outside the relay radius does not");

				// nearest wins; past link reach is ignored
				ArmoryTE nearer = Armory(1040, 500), beyond = Armory(1000 + (int)R.LinkReachTiles + 50, 500);
				Check(ReferenceEquals(ArmoryRelays.Link(relay.Center, new[] { far, nearer, beyond }, false, null), nearer), "the nearest Armory wins");
				Check(ArmoryRelays.Link(relay.Center, new[] { beyond }, false, null) == null, "an Armory past link reach is ignored");
				// a relay at a pylon falls back to a networked Armory past link reach
				Check(ReferenceEquals(ArmoryRelays.Link(relay.Center, new[] { beyond }, true, a => true), beyond), "a pylon relay reaches a networked Armory past link reach");
				Check(ArmoryRelays.Link(relay.Center, new[] { beyond }, true, a => false) == null, "a pylon relay ignores an Armory off the network");

				// two relays on the same Armory pool it once; a second relay on another Armory adds it
				ArmoryRelayTE relay2 = Relay(1020, 500);
				got.Clear();
				ArmoryRelays.LinkedFor(relay.Center, new[] { relay, relay2 }, armories, null, null, got);
				Check(got.Count == 1, "two relays on one Armory do not stack");
				ArmoryTE armA = Armory(1000, 450), armB = Armory(1020, 550);   // each is the nearest to a different relay
				got.Clear();
				ArmoryRelays.LinkedFor(relay.Center + new Vector2(80f, 0f), new[] { relay, relay2 }, new[] { armA, armB }, null, null, got);
				Check(got.Count == 2, "overlapping relays on different Armories pool both");
			}
			catch (Exception e) { failures.Add($"threw {e.GetType().Name}: {e.Message}"); }

			if (failures.Count == 0) Mod.Logger.Info($"Mechanization: armory relay check OK ({ok} checks): registration, text, 100-tile link, serve radius in/out, nearest, reach cap, pylon fallback, no stacking.");
			else foreach (string f in failures) Mod.Logger.Warn("Mechanization: armory relay check FAILED: " + f);
		}
	}
}
