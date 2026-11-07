using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Recording
{
	/// <summary>Hooks for the fight recorder. Off by default; the per-tick cost while off is one static bool test.</summary>
	public class FightRecSystem : ModSystem
	{
		public override void PostSetupContent()
		{
			string fail = FightRecFormat.SelfCheck();
			if (fail == null) Mod.Logger.Info("[FightRec] self-check ok (frame, event and arena round-trip)");
			else Mod.Logger.Error("[FightRec] self-check FAILED: " + fail);
		}

		public override void PostUpdateEverything()
		{
			if (FightRecorder.On) FightRecorder.Tick();
		}

		public override void OnWorldUnload()
		{
			if (FightRecorder.On) FightRecorder.Stop("world unloaded");
		}

		public override void Unload()
		{
			if (FightRecorder.On) FightRecorder.Stop("unloaded");
		}
	}

	public class FightRecPlayer : ModPlayer
	{
		public override void OnHurt(Player.HurtInfo info)
		{
			if (!FightRecorder.On) return;
			var src = info.DamageSource;
			FightRecorder.NoteHit(Player, info.Damage, src.SourceNPCIndex, src.SourceProjectileType, src.SourceOtherIndex);
		}
	}

	/// <summary>/levyrec start [name], /levyrec stop, /levyrec status. Records the local player's fight to Levy/recordings in the tModLoader save folder.</summary>
	public class LevyRecCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levyrec";
		public override string Description => "Fight recorder. /levyrec start [name] begins, /levyrec stop ends and prints the file path.";
		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (Main.dedServ) { caller.Reply("levyrec runs on a client.", Color.OrangeRed); return; }
			string a = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
			if (a == "start")
			{
				string name = args.Length > 1 ? string.Join("_", args, 1, args.Length - 1) : null;
				string path = FightRecorder.Start(Main.LocalPlayer, name, out string err);
				if (path == null) caller.Reply("levyrec: " + err, Color.OrangeRed);
				else caller.Reply("levyrec: recording to " + path + " (stops on boss death, /levyrec stop, or after 10 minutes)", Color.LightGreen);
			}
			else if (a == "stop")
			{
				string path = FightRecorder.Stop("stopped by command");
				caller.Reply(path == null ? "levyrec: not recording." : "levyrec: saved " + path, path == null ? Color.OrangeRed : Color.LightGreen);
			}
			else caller.Reply(FightRecorder.On ? "levyrec: recording " + (FightRecorder.Elapsed / 60) + "s to " + FightRecorder.Path : "levyrec: off. /levyrec start [name]", Color.LightSkyBlue);
		}
	}
}
