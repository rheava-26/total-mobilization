using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Levy.Common.Util;
using Mechanization.Common;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// The recall / launch key. One press acts on the Barracks nearest the player (within <see cref="MechConstants.Drone.KeyReachTiles"/>): if any of its drones is flying it recalls them all to
	/// land and file their weapons back; if they are all parked it arms and launches them. The same orders are on the drone's right-click panel; the key is for a drone too high to click.
	/// Server-authoritative: a multiplayer client only sends the press.
	/// </summary>
	public class DroneControls : ModSystem
	{
		public static ModKeybind RecallKey { get; private set; }

		public override void Load() => RecallKey = KeybindLoader.RegisterKeybind(Mod, "RecallDrones", "L");

		public override void Unload() => RecallKey = null;
	}

	public class DronePlayer : ModPlayer
	{
		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Main.dedServ || Main.gameMenu || Player.whoAmI != Main.myPlayer) return;
			if (DroneControls.RecallKey?.JustPressed != true) return;
			if (LevyUtil.IsClient) MechPackets.SendDroneKey();
			else DroneSystem.Instance?.HandleKey(Player);
		}
	}
}
