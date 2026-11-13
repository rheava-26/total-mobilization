using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Content.TileEntities;
using Mechanization.Common;
using Mechanization.Content.Items;
using D = Mechanization.Common.MechConstants.Drone;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// Self-check of the player-built drone, logged as "[DroneItem]" lines. At boot (no world): the item, its recipe, its sprite and its text are registered. With a world on a dedicated server and LEVY_SELFCHECK_WORLD=1:
	/// the item's placement path sets a drone down on the ground and registers it; it belongs to no Barracks; it fires the plain bullet with no Armory in reach and mounts the Armory's weapon
	/// without taking it (and drops back to the bullet when the weapon is gone); a spot buried in rock is refused and leaves nothing behind; the registry saves and re-spawns it with its HP; the
	/// multiplayer place request is validated and places one; Pack up returns exactly one item and removes the record. Everything it makes is removed again; nothing is saved.
	/// </summary>
	public class DroneItemCheck : ModSystem
	{
		private static readonly string[] Keys =
		{
			"Mods.Mechanization.Items.DroneItem.DisplayName", "Mods.Mechanization.Items.DroneItem.Tooltip", "Mods.Mechanization.UI.DronePackUp", "Mods.Mechanization.UI.DroneNoRoom",
			"Mods.Mechanization.UI.DroneFieldMounted", "Mods.Mechanization.UI.DroneFieldNoWeapon", "Mods.Mechanization.UI.DroneFieldNoArmory",
		};

		public override void PostAddRecipes()
		{
			int pass = 0, fail = 0;
			void Check(string name, bool ok) { if (ok) pass++; else { fail++; Mod.Logger.Warn($"[DroneItem] FAIL {name}"); } }
			try
			{
				int type = ModContent.ItemType<DroneItem>();
				Check("item registered", type > 0);
				Check("sprite is packed in the mod", Mod.FileExists("Content/Items/DroneItem.rawimg") || Mod.FileExists("Content/Items/DroneItem.png"));
				foreach (string key in Keys) Check("text " + key, Language.Exists(key));
				Recipe recipe = null;
				foreach (Recipe r in Main.recipe) if (r != null && r.createItem != null && r.createItem.type == type) { recipe = r; break; }
				Check("a recipe makes the drone", recipe != null);
				if (recipe != null)
				{
					bool wire = false, bars = false, lens = false;
					foreach (Item ing in recipe.requiredItem)
					{
						if (ing.type == ItemID.Wire && ing.stack == MechConstants.FieldDrone.RecipeWire) wire = true;
						if (ing.type == ItemID.Lens && ing.stack == MechConstants.FieldDrone.RecipeLenses) lens = true;
					}
					foreach (int g in recipe.acceptedGroups) if (g == RecipeGroupID.IronBar) bars = true;
					Check("the recipe takes wire (the Mechanic's), a lens and iron or lead bars", wire && lens && bars);
					Check("the recipe is made at a Tinkerer's Workshop", recipe.requiredTile.Contains(TileID.TinkerersWorkbench));
				}
				Item sample = new Item(); sample.SetDefaults(type);
				Check("the item places and is consumed on use", sample.consumable && sample.useStyle != 0 && sample.maxStack > 1);
				Check("the drone NPC is registered", ModContent.NPCType<DroneNPC>() > 0);
				Check("the place packet exists", Enum.IsDefined(typeof(MechPacket), MechPacket.PlaceDrone));
			}
			catch (Exception e) { Check("boot checks threw " + e.GetType().Name + ": " + e.Message, false); }
			Mod.Logger.Info($"[DroneItem] self-check (boot) pass={pass} fail={fail}");
		}

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private void Run()
		{
			int pass = 0, fail = 0;
			void Check(string name, bool ok) { if (ok) pass++; else { fail++; Mod.Logger.Warn($"[DroneItem] FAIL {name}"); } }
			var touched = new List<Point>(); var armories = new List<ArmoryTE>();
			int x0 = Main.maxTilesX / 2 + 120, y0 = Math.Max(100, (int)Main.worldSurface - 40);
			var sys = FieldDroneSystem.Instance;
			try
			{
				// a clear box of sky over a long dirt floor
				for (int x = x0 - 45; x <= x0 + 45; x++)
					for (int y = y0 - 40; y <= y0 + 14; y++)
					{
						Tile t = Main.tile[x, y]; touched.Add(new Point(x, y));
						t.HasTile = y >= y0 + 1 && y <= y0 + 3 && x >= x0 - 20 && x <= x0 + 20; t.TileType = TileID.Dirt; t.TileFrameX = 0; t.TileFrameY = 0; t.WallType = 0;
					}
				Vector2 spot = new Vector2(x0 * 16f, (y0 - 10) * 16f); // ten tiles above the floor
				var player = new Player { name = "DroneItemCheck", active = true };
				player.position = spot - new Vector2(player.width / 2f, player.height / 2f);

				// ---- placement through the item's path ----
				int before = sys.RecordCount;
				int idx = sys.TryPlace(new EntitySource_ItemUse(player, new Item()), spot);
				Check("TryPlace sets a drone down", idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active);
				if (idx < 0) return;
				DroneNPC drone = Main.npc[idx].ModNPC as DroneNPC;
				Check("the NPC is a bound, player-built drone with a record", drone != null && drone.IsBound && drone.IsField && drone.RecordId > 0 && sys.RecordCount == before + 1);
				Check("it belongs to no Barracks and holds no loan", drone.BarracksId == -1 && drone.ArmoryId == -1 && drone.Weapon == null);
				Check("it is armed (the plain bullet) and takes off", drone.Armed && drone.Phase == DronePhase.Loiter && drone.FieldWeaponType == 0);
				float groundY = (y0 + 1) * 16f;
				Check("set down ten tiles above the floor it hovers at the cursor", Vector2.Distance(Main.npc[idx].Center, spot) <= 2.5f && Main.npc[idx].Bottom.Y < groundY - 5f * 16f);
				{
					// FindPad directly: open air, just above and on solid ground, on a platform, just above one, buried
					bool ok = FieldDroneSystem.FindPad(new Vector2(x0 * 16f, groundY - 14f - 20f), out Vector2 onGround);
					Check("a spot 20px above solid ground settles onto it", ok && Math.Abs(onGround.Y + 14f - groundY) <= 2.5f && Math.Abs(onGround.X - x0 * 16f) <= 0.5f);
					ok = FieldDroneSystem.FindPad(new Vector2(x0 * 16f, groundY - 14f - 6f * 16f), out Vector2 air);
					Check("a spot six tiles above solid ground stays put", ok && Math.Abs(air.Y - (groundY - 14f - 6f * 16f)) <= 0.5f);
					int py = y0 - 5; for (int x = x0 - 3; x <= x0 + 3; x++) { Tile t = Main.tile[x, py]; t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; touched.Add(new Point(x, py)); }
					float platTop = py * 16f;
					ok = FieldDroneSystem.FindPad(new Vector2(x0 * 16f, py * 16f + 8f), out Vector2 onPlat);
					Check("a spot on a platform rests on the platform, not the floor below it", ok && Math.Abs(onPlat.Y + 14f - platTop) <= 2.5f);
					ok = FieldDroneSystem.FindPad(new Vector2(x0 * 16f, platTop - 14f - 24f), out Vector2 abovePlat);
					Check("a spot a tile and a half above a platform settles onto it", ok && Math.Abs(abovePlat.Y + 14f - platTop) <= 2.5f);
					for (int x = x0 - 3; x <= x0 + 3; x++) { Tile t = Main.tile[x, py]; t.HasTile = false; }
					for (int x = x0 + 30; x <= x0 + 32; x++) for (int y = y0 - 4; y <= y0; y++) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.Stone; t.BlockType = BlockType.Solid; touched.Add(new Point(x, y)); }
					ok = FieldDroneSystem.FindPad(new Vector2((x0 + 31) * 16f, (y0 - 2) * 16f), out Vector2 lifted);
					Check("a spot buried in a block is lifted out onto its top", ok && !Collision.SolidCollision(lifted - new Vector2(D.BodyWidth * 0.5f, D.BodyHeight * 0.5f), D.BodyWidth, D.BodyHeight) && Math.Abs(lifted.Y + 14f - (y0 - 4) * 16f) <= 2.5f);
					for (int x = x0 + 30; x <= x0 + 32; x++) for (int y = y0 - 4; y <= y0; y++) { Tile t = Main.tile[x, y]; t.HasTile = false; }
				}

				// ---- it arms itself from the Armory in reach, without taking the weapon ----
				var armory = new ArmoryTE { Position = new Point16(x0 + 6, y0 - 3) }; armory.ID = 970001; TileEntity.ByID[armory.ID] = armory; armories.Add(armory);
				armory.IssueWeapons[0] = new Item(ItemID.Handgun);
				drone.RefreshFieldWeapon();
				Check("it mounts the Armory's best spare weapon", drone.FieldWeaponType == ItemID.Handgun);
				Check("the Armory still holds the weapon and lent nothing", armory.IssueWeapons[0].type == ItemID.Handgun && armory.IssueWeapons[0].stack == 1 && armory.Lent.Count == 0);
				Check("it is still armed with a valid shot", drone.Armed);
				armory.IssueWeapons[0] = new Item();
				drone.RefreshFieldWeapon();
				Check("with the weapon gone it falls back to the plain bullet", drone.FieldWeaponType == 0 && drone.Armed);
				armory.IssueWeapons[0] = new Item(ItemID.Handgun);

				// ---- refused spot leaves nothing behind ----
				for (int x = x0 - 44; x <= x0 - 25; x++) for (int y = y0 - 20; y <= y0 + 10; y++) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.Stone; }
				int recs = sys.RecordCount;
				int refused = sys.TryPlace(new EntitySource_ItemUse(player, new Item()), new Vector2((x0 - 35) * 16f, y0 * 16f));
				Check("a spot buried in rock is refused and keeps no record", refused < 0 && sys.RecordCount == recs);
				for (int x = x0 - 44; x <= x0 - 25; x++) for (int y = y0 - 20; y <= y0 + 10; y++) { Tile t = Main.tile[x, y]; t.HasTile = false; }

				// ---- the multiplayer request path ----
				player.inventory[0] = new Item(ModContent.ItemType<DroneItem>());
				player.selectedItem = 0;
				Check("PlaceFor places one for a player holding the item", sys.PlaceFor(player, spot + new Vector2(10f * 16f, 0f)) && sys.RecordCount == recs + 1);
				player.inventory[0] = new Item(ItemID.Wood);
				bool refusedHold = sys.PlaceFor(player, spot + new Vector2(-10f * 16f, 0f));
				Check("PlaceFor refuses a player not holding it", !refusedHold && sys.RecordCount == recs + 1);
				player.inventory[0] = new Item();

				// ---- save, unload, load, respawn ----
				drone.NPC.life = Math.Max(1, drone.NPC.lifeMax / 2);
				int savedLife = drone.NPC.life, savedId = drone.RecordId; Vector2 savedPad = Main.npc[idx].Center;
				var tag = new TagCompound();
				sys.SaveWorldData(tag);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC dd && dd.IsField) { Main.npc[i].active = false; }
				sys.OnWorldUnload();
				Check("unloading the world empties the registry", sys.RecordCount == 0);
				sys.LoadWorldData(tag);
				Check("the saved records load back (two drones)", sys.RecordCount == 2);
				sys.RespawnAll();
				DroneNPC back = null; int live = 0;
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC db && db.IsField) { live++; if (db.RecordId == savedId) back = db; }
				Check("both drones respawn", live == 2 && back != null);
				if (back != null)
				{
					Check("a respawned drone keeps its HP and its pad", back.NPC.life == savedLife && Vector2.Distance(back.NPC.Center, savedPad) <= 2f);
					Check("a respawned drone re-mounts the Armory's weapon", back.FieldWeaponType == ItemID.Handgun);
				}

				// ---- pack up ----
				int items0 = CountItems(ModContent.ItemType<DroneItem>());
				bool packed = back != null && sys.Pack(back.NPC, player);
				Check("Pack up succeeds", packed);
				Check("Pack up removes the drone and its record", back != null && !back.NPC.active && sys.RecordCount == 1);
				Check("Pack up hands back exactly one item", CountItems(ModContent.ItemType<DroneItem>()) == items0 + 1);
				Check("packing the same drone twice pays out once", back != null && !sys.Pack(back.NPC, player) && CountItems(ModContent.ItemType<DroneItem>()) == items0 + 1);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d2 && d2.IsField) Check("pack the other one too", sys.Pack(Main.npc[i], player));
				Check("the registry is empty again", sys.RecordCount == 0);
			}
			catch (Exception e) { Check("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				try
				{
					for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsField) Main.npc[i].active = false;
					foreach (ArmoryTE a in armories) TileEntity.ByID.Remove(a.ID);
					for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && Main.item[i].type == ModContent.ItemType<DroneItem>()) Main.item[i].active = false;
					foreach (Point p in touched) { Tile t = Main.tile[p.X, p.Y]; t.HasTile = false; t.TileFrameX = 0; t.TileFrameY = 0; }
					sys.OnWorldUnload();
				}
				catch (Exception) { }
			}
			Mod.Logger.Info($"[DroneItem] self-check (world) pass={pass} fail={fail}");
		}

		private static int CountItems(int type)
		{
			int n = 0;
			for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && Main.item[i].type == type) n += Main.item[i].stack;
			return n;
		}
	}
}
