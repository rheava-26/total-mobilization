using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// /levyrift [status|replay]. Status prints what Levy reads of the Wrath of the Gods sky and the army's mood. Replay plays the covering of the sun again: it clears NoxusBoss's
	/// covered flag and arms its covering scene the way the Moon Lord kill does (WaitingToStart), so the mod's own start check runs it at its next valid moment (a player on the surface,
	/// daytime, no boss alive). Nothing else of NoxusBoss is touched. Refused without NoxusBoss, while the scene is already playing or armed, and once the sky has been freed for good
	/// (the mod would not cover it again then). Runs where the world runs (single player, or the server).
	/// </summary>
	public class RiftCommand : ModCommand
	{
		public override CommandType Type => CommandType.World;
		public override string Command => "levyrift";
		public override string Usage => "/levyrift [status|replay]";
		public override string Description => "Show the Wrath of the Gods sky state, or play the sun's covering again.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
			if (!RiftSky.Installed) { caller.Reply("Wrath of the Gods is not loaded."); return; }
			if (!string.IsNullOrEmpty(RiftSky.Problem)) { caller.Reply("Some of its sky could not be read: " + RiftSky.Problem); }
			var r = RiftSky.ReadReal();
			if (sub == "replay")
			{
				caller.Reply(Replay(r));
				return;
			}
			caller.Reply($"Sun covered: {r.covered}. Covering playing: {r.ongoing} (timer {r.timer}). Armed: {r.waiting}. Sky freed: {RiftSky.Defeated}.");
			caller.Reply($"Mood: {RiftMood.Phase}, day {RiftMood.DaysSince}, morale {RiftMood.Morale}. Rift tier {RiftMood.Tier} (intensity {RiftSky.Intensity:F2}). Ice {RiftMood.IceLevel}. Fog: {RiftSky.Fog}. Snow lying {r.snow:F1}.");
		}

		/// <summary>The replay itself, separated so the self-check can run it. Returns what it did, or why it did nothing.</summary>
		internal static string Replay((bool covered, bool ongoing, int timer, bool waiting, bool rift, float snow, bool fog) r)
		{
			if (RiftSky.Defeated) return "The sky has been freed in this world; Wrath of the Gods will not cover it again.";
			if (r.ongoing) return "The covering is already playing.";
			if (r.waiting) return "The covering is already armed. It plays when someone is on the surface, by day, with no boss alive.";
			if (!RiftSky.SetReal(covered: false, eventTimer: 0, waiting: true)) return "Could not set Wrath of the Gods' sky state; nothing was changed that could be checked.";
			var after = RiftSky.ReadReal();
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
			return $"Done: the sun is uncovered (was covered: {r.covered}) and the covering is armed (armed: {after.waiting}). It plays when someone is on the surface, by day, with no boss alive.";
		}
	}
}
