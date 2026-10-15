using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Hooks that make followers travel with their commander and keep soldiers off pressure plates.
	/// Player.Teleport is the single funnel for pylons (TeleportPylonsSystem), wired teleporters (Wiring) and teleportation
	/// potions (net message 65); Player.Spawn covers Magic Mirror / Recall and respawns, and the server runs it for remote
	/// players when it handles the PlayerSpawn message. NPC.UpdateNPC triggers plates for every NPC with lifeMax &gt; 1, so the
	/// config switch is honoured by a detour on Collision.SwitchTiles. OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public class FollowerTravelSystem : ModSystem
	{
		public override void Load()
		{
			On_Player.Teleport += Player_Teleport;
			On_Player.Spawn += Player_Spawn;
			On_Collision.SwitchTiles_Entity_Vector2_int_int_Vector2_int += Collision_SwitchTiles;
		}

		public override void Unload()
		{
			On_Player.Teleport -= Player_Teleport;
			On_Player.Spawn -= Player_Spawn;
			On_Collision.SwitchTiles_Entity_Vector2_int_int_Vector2_int -= Collision_SwitchTiles;
		}

		private static void Player_Teleport(On_Player.orig_Teleport orig, Player self, Vector2 newPos, int style, int extraInfo)
		{
			orig(self, newPos, style, extraInfo);
			AfterTravel(self);
		}

		private static void Player_Spawn(On_Player.orig_Spawn orig, Player self, PlayerSpawnContext context)
		{
			orig(self, context);
			AfterTravel(self);
		}

		private static void AfterTravel(Player player)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || player == null || !player.active) return;
			try { FollowerTravel.BringFollowers(player); }
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: follower travel failed: " + e); }
		}

		private static bool Collision_SwitchTiles(On_Collision.orig_SwitchTiles_Entity_Vector2_int_int_Vector2_int orig, Entity entity, Vector2 position, int width, int height, Vector2 oldPosition, int objType)
		{
			if (objType == 2 && entity is NPC npc && npc.ModNPC is SoldierNPC)
			{
				var cfg = LevyConfig.Get;
				if (cfg != null && !cfg.SoldiersTriggerPressurePlates) return false;
			}
			return orig(entity, position, width, height, oldPosition, objType);
		}
	}
}
