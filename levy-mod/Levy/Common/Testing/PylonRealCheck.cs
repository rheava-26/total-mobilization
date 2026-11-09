using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check of the pylon network with REAL placed tiles, logged as "[PylonReal]" lines (only with LEVY_SELFCHECK_WORLD=1 on a dedicated server).
	/// Places real Barracks and Armory tiles (their placement hooks create the tile entities) and real vanilla Teleportation Pylon tiles (vanilla's own tile entity, listed by Main.PylonSystem),
	/// 400 tiles apart, with real housed town NPCs. Asserts the pylon list is populated by vanilla's own update, and that the link rule holds for the layouts players actually build:
	/// pylon beside the barracks, pylon in the middle of town with the barracks at the edge (50 and 100 tiles out), a pylon outside the settlement area (no link), and no pylon at all.
	/// Everything it places is removed again; nothing is saved.
	/// </summary>
	public class PylonRealCheck : ModSystem
	{
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private static void Flatten(int cx, int floorY, int half = 14)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = floorY - 14; j < floorY; j++) { var t = Main.tile[i, j]; t.ClearEverything(); }
				var f = Main.tile[i, floorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
			}
		}

		private void Run()
		{
			int pass = 0, fail = 0;
			void Check(string name, bool ok) { if (ok) pass++; else fail++; Mod.Logger.Info($"[PylonReal] {(ok ? "PASS" : "FAIL")} {name}"); }
			var npcs = new List<NPC>(); var placed = new List<(int i, int j, int w, int h)>();
			try
			{
				int floorY = 300;
				int ax = 600, bx = 1000;
				Flatten(ax, floorY); Flatten(bx, floorY);
				int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();

				BarracksTE PlaceBarracks(int left, string name)
				{
					Flatten(left + 1, floorY);
					WorldGen.PlaceObject(left + 1, floorY - 1, barType, true);
					var te = ModContent.GetInstance<BarracksTE>();
					te.Hook_AfterPlacement(left, floorY - 3, barType, 0, 1, 0);
					placed.Add((left, floorY - 3, 3, 3));
					TileEntity.ByPosition.TryGetValue(new Point16(left, floorY - 3), out var e);
					var b = e as BarracksTE; if (b != null) { b.TownName = name; b.Treasury = 10_000_000; }
					return b;
				}
				ArmoryTE PlaceArmory(int left)
				{
					WorldGen.PlaceObject(left, floorY - 1, armType, true);
					ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(left, floorY - 3, armType, 0, 1, 0);
					placed.Add((left, floorY - 3, 3, 3));
					TileEntity.ByPosition.TryGetValue(new Point16(left, floorY - 3), out var e);
					return e as ArmoryTE;
				}
				// returns the pylon's top-left tile, or -1 when vanilla refused it
				int PlacePylon(int left, int style)
				{
					Flatten(left + 1, floorY, 2);
					WorldGen.PlaceObject(left + 1, floorY - 1, TileID.TeleportationPylon, true, style);
					var t = Main.tile[left + 1, floorY - 2];
					if (!(t.HasTile && t.TileType == TileID.TeleportationPylon)) return -1;
					TETeleportationPylon.PlacementPreviewHook_AfterPlacement(left + 1, floorY - 1, TileID.TeleportationPylon, style, 1, 0); // what vanilla runs after a player places the item: creates the pylon tile entity
					placed.Add((left, floorY - 4, 3, 4));
					return left;
				}
				NPC Town(int type, BarracksTE home, int dx)
				{
					for (int i = Main.maxNPCs - 2; i >= 0; i--)
					{
						var n = Main.npc[i]; if (n.active) continue;
						n.SetDefaults(type); n.active = true; n.townNPC = true; n.homeless = false; n.homeTileX = home.Position.X + dx; n.homeTileY = floorY - 2; n.life = n.lifeMax;
						npcs.Add(n); return n;
					}
					return null;
				}

				var A = PlaceBarracks(ax, "RealWest"); var B = PlaceBarracks(bx, "RealEast");
				Check("real barracks tiles placed with tile entities", A != null && B != null && A.ID != B.ID);
				if (A == null || B == null) return;
				var armA = PlaceArmory(ax + 4); var armB = PlaceArmory(bx + 4);
				Check("real armory tiles placed with tile entities", armA != null && armB != null);
				NPC gA = Town(NPCID.Guide, A, 8), zA = Town(NPCID.BestiaryGirl, A, 10), nB = Town(NPCID.Nurse, B, 8), dB = Town(NPCID.Demolitionist, B, 10);
				var sys = SettlementSystem.Instance; sys.InvalidateTownNpcs();
				var player = new Player { name = "PylonRealCommander", active = true };
				player.position = A.Center - new Vector2(player.width / 2f, player.height / 2f);

				// ---- baseline: no pylons in the world
				SettlementSystem.PylonListOverride = null;
				Check("no pylons: barracks are separate settlements", sys.NetworkedBarracks(A).Count == 1 && !sys.LinkOf(A).Linked);
				Check("no pylons: the panel line says no pylon links this settlement", sys.LinkLine(A).Contains("No pylon links"));
				Check("no pylons: far-town NPC refused (NotHoused)", OfficerSystem.Instance.CanCommission(player, A, nB) == OfficerCheck.NotHoused);

				// ---- one real pylon beside each barracks (vanilla list, after vanilla's own update)
				int pA = PlacePylon(ax + 8, 0), pB = PlacePylon(bx + 8, 1);
				Check("real pylon tiles placed (Purity west, Jungle east)", pA >= 0 && pB >= 0);
				bool entityA = TileEntity.ByPosition.TryGetValue(new Point16(pA, floorY - 4), out var peA) && peA is TETeleportationPylon;
				Check("real pylon tile has vanilla's pylon tile entity", entityA);
				int before = Main.PylonSystem.Pylons.Count;
				Mod.Logger.Info($"[PylonReal] OBSERVED pylon list right after placement: {before}");
				RefreshVanillaPylons();
				var list = Main.PylonSystem.Pylons;
				Mod.Logger.Info($"[PylonReal] OBSERVED pylon list after vanilla update: {list.Count}" + (list.Count > 0 ? $" first={list[0].PositionInTiles} type={list[0].TypeOfPylon}" : ""));
				Check("vanilla pylon list holds the two placed pylons", list.Count == 2);

				var linkA = sys.LinkOf(A);
				Check("pylon beside each barracks: both settlements linked", linkA.Linked && sys.LinkOf(B).Linked);
				Check("barracks link both ways; panel line names the pylon", sys.NetworkedBarracks(A).Count == 2 && sys.NetworkedBarracks(B).Count == 2 && sys.LinkLine(A).StartsWith("Pylon network:"));
				Check("armories link both ways", sys.NetworkedArmories(armA).Contains(armB) && sys.NetworkedArmories(armB).Contains(armA));
				Check("treasuries pool", EconomySystem.NetworkFunds(A) == A.Treasury + B.Treasury);
				var cand = sys.NetworkTownNpcs(A);
				var bl = new List<string>(); foreach (var q in TileEntity.ByID.Values) if (q is BarracksTE qb) bl.Add(qb.ID + "@" + qb.Position);
				Mod.Logger.Info("[PylonReal] DIAG towns A=" + sys.TownNpcsOf(A).Count + " B=" + sys.TownNpcsOf(B).Count + " cand=" + cand.Count + " netA=" + sys.NetworkedBarracks(A).Count + " barracks=" + string.Join(",", bl));
				Check("officer candidates list both towns, own town first", cand.Count == 4 && cand[0].Town == A && cand[2].Town == B);
				Check("CanCommission accepts a far-town NPC from this barracks", OfficerSystem.Instance.CanCommission(player, A, nB) == OfficerCheck.Ok);

				// ---- pylon in the middle of town, 90 tiles from the west barracks (inside its settlement area, beyond 60): still linked
				RemovePylons(placed);
				int pMid = PlacePylon(ax - 90, 0), pEast = PlacePylon(bx + 8, 1);
				RefreshVanillaPylons();
				{ var lk = sys.LinkOf(A); string pl = ""; foreach (var q in Main.PylonSystem.Pylons) pl += q.PositionInTiles + " "; Mod.Logger.Info($"[PylonReal] DIAG mid={pMid} east={pEast} list={pl} linkA={lk.Linked}/{lk.Tiles}"); }
				Check("pylon 90 tiles from the barracks (inside the settlement area) links it", pMid >= 0 && pEast >= 0 && sys.LinkOf(A).Linked && sys.LinkOf(A).Tiles >= 85);
				Check("west and east towns share a network through those two pylons", sys.NetworkedBarracks(A).Contains(B) && sys.IsHousedOnNetwork(A, nB) && sys.IsHousedOnNetwork(B, gA));

				// ---- a pylon outside the settlement area and 60 tiles from everything: not linked
				RemovePylons(placed);
				int pFar = PlacePylon(ax - 170, 0), pEast2 = PlacePylon(bx + 8, 1);
				RefreshVanillaPylons();
				Check("pylon 170 tiles out does not link the west town", pFar >= 0 && !sys.LinkOf(A).Linked && sys.NetworkedBarracks(A).Count == 1 && sys.LinkLine(A).Contains("No pylon links"));
				Check("the east town still links to its own pylon only", sys.LinkOf(B).Linked && sys.NetworkedBarracks(B).Count == 1);
				Check("CanCommission refuses the far-town NPC with no link (NotHoused)", OfficerSystem.Instance.CanCommission(player, A, nB) == OfficerCheck.NotHoused);
			}
			catch (Exception e) { Check("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				SettlementSystem.PylonListOverride = null;
				foreach (var n in npcs) { n.active = false; n.townNPC = false; }
				try { RemovePylons(placed); } catch { }
				foreach (var (i, j, w, h) in placed)
					for (int x = i; x < i + w; x++) for (int y = j; y < j + h; y++) if (WorldGen.InWorld(x, y)) { Main.tile[x, y].ClearEverything(); }
				foreach (var id in new List<int>(TileEntity.ByID.Keys)) { var te = TileEntity.ByID[id]; if (te is BarracksTE || te is ArmoryTE || te is TETeleportationPylon) { TileEntity.ByID.Remove(id); TileEntity.ByPosition.Remove(te.Position); } }
				try { RefreshVanillaPylons(); } catch { }
			}
			Mod.Logger.Info($"[PylonReal] self-check pass={pass} fail={fail}");
		}

		private static void RemovePylons(List<(int i, int j, int w, int h)> placed)
		{
			foreach (var (i, j, w, h) in placed.ToArray())
			{
				if (h != 4) continue;
				for (int x = i; x < i + w; x++) for (int y = j; y < j + h; y++) Main.tile[x, y].ClearEverything();
				if (TileEntity.ByPosition.TryGetValue(new Point16(i, j), out var te)) { TileEntity.ByID.Remove(te.ID); TileEntity.ByPosition.Remove(te.Position); }
				placed.Remove((i, j, w, h));
			}
			RefreshVanillaPylons(); // vanilla refuses a second pylon of a type it still lists
		}

		/// <summary>Runs vanilla's own pylon-list update (an idle dedicated server does not tick the world, so the check asks for it).</summary>
		private static void RefreshVanillaPylons()
		{
			var sys = Main.PylonSystem;
			var t = sys.GetType();
			foreach (var name in new[] { "RequestImmediateUpdate", "Update" })
			{
				var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
				m?.Invoke(sys, null);
			}
		}
	}
}
