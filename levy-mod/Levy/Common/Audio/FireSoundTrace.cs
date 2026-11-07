using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Audio
{
	/// <summary>Measurement only: counts the use-sound calls a levy's shots make, split by whether an owner or gear-upkeep frame (which mutes sound) was open at the call. Logged every 5 seconds while counting.</summary>
	public class FireSoundTrace : ModSystem
	{
		static readonly bool Enabled = System.Environment.GetEnvironmentVariable("LEVY_FIRESOUND") == "1";
		static long uses, noSound, inOwner, inUpkeep, muted;

		public static void Note(bool hasSound, bool ownerFrame, bool upkeepFrame)
		{
			uses++;
			if (!hasSound) noSound++;
			if (ownerFrame) inOwner++;
			if (upkeepFrame) inUpkeep++;
			if (Main.dedServ || Main.soundVolume <= 0f) muted++; // true on a dedicated server by definition; on a client it means the call would be a no-op
		}

		public override void PostUpdateEverything()
		{
			if (!Enabled || uses == 0 || Main.GameUpdateCount % 300 != 0) return;
			Mod.Logger.Info($"[FireSound] shots={uses} noUseSound={noSound} inOwnerFrame={inOwner} inUpkeepFrame={inUpkeep} dedServOrVol0={muted} netMode={Main.netMode} heldInOwnerFrames={ArmySoundThrottle.HeldCount} replayed={ArmySoundThrottle.ReplayedCount}");
		}
	}
}
