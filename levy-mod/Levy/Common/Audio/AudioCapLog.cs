using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Audio
{
	/// <summary>
	/// Client audio diagnostic. `/levyaudio` (on/off) writes one [AudioCap] line per 2 s to client.log: how many PlaySound calls the game made, how many of them were made while the audio listener state
	/// (Main.myPlayer, Main.screenPosition, Main.soundVolume, Main.dedServ) differed from what the tick started with (an owner frame or aim scope that swapped them), how many were too far from the camera to
	/// be heard (positional volume is 1 - distance/2500 against Main.Camera.Center), and the state of the audio engine's own update (screen position at zero, volume at zero, dedServ, camera far from the player).
	/// Off by default; LEVY_AUDIOCAP=1 turns it on at boot (also on the lab's dedicated server).
	/// </summary>
	public class AudioCapLog : ModSystem
	{
		public static bool On = Environment.GetEnvironmentVariable("LEVY_AUDIOCAP") == "1";

		// baseline of the tick, taken before any Levy code runs
		static int bMy; static Vector2 bScreen; static float bVol; static bool bDed; static bool haveBase;
		// per window
		static int calls, army, world, swMy, swScreen, swVol, swDed, silentDist, culled, nearOk, upd, updScreen0, updVol0, updDed, updMy, updCamFar, updSwapped, logAt;
		static float maxVolSeen;
		static readonly Dictionary<string, int> swapStyles = new Dictionary<string, int>();

		public override void Load()
		{
			if (Main.dedServ && !On) return;
			On_SoundEngine.Update += EngineUpdate;
		}

		public override void Unload() { if (!Main.dedServ || On) On_SoundEngine.Update -= EngineUpdate; }

		public override void PreUpdateWorld()
		{
			if (!On) return;
			bMy = Main.myPlayer; bScreen = Main.screenPosition; bVol = Main.soundVolume; bDed = Main.dedServ; haveBase = true;
			if (bVol > maxVolSeen) maxVolSeen = bVol;
		}

		/// <summary>End of the tick: any listener state still different from the tick's start leaked out of a frame or scope.</summary>
		public override void PostUpdateEverything()
		{
			if (!On || !haveBase) return;
			if (Main.myPlayer != bMy) leakMy++;
			if (Main.screenPosition != bScreen) leakScreen++;
			if (Main.soundVolume != bVol) leakVol++;
			if (Main.dedServ != bDed) leakDed++;
		}
		static int leakMy, leakScreen, leakVol, leakDed;

		static string Short(string k) => k.Substring(k.LastIndexOf('/') + 1);

		/// <summary>Called by the throttle's PlaySound detour for every call while On.</summary>
		public static void OnPlay(ref SoundStyle style, Vector2? position, bool inArmy)
		{
			if (!haveBase) return;
			calls++; if (inArmy) army++; else world++;
			bool my = Main.myPlayer != bMy, sc = Main.screenPosition != bScreen, vol = Main.soundVolume != bVol, ded = Main.dedServ != bDed;
			if (my) swMy++; if (sc) swScreen++; if (vol) swVol++; if (ded) swDed++;
			if ((my || sc || vol || ded) && !ded) { string k = Short(style.SoundPath ?? ""); swapStyles.TryGetValue(k, out int c); swapStyles[k] = c + 1; } // swapped but not muted by a frame's dedServ: these are the audible ones
			if (position.HasValue && !Main.dedServ)
			{
				float d = Vector2.Distance(position.Value, Main.Camera.Center);
				if (d >= 10000f) culled++; else if (d >= 2500f) silentDist++; else nearOk++;
			}
			Flush();
		}

		static void EngineUpdate(On_SoundEngine.orig_Update orig)
		{
			if (On && haveBase && !Main.gameMenu)
			{
				upd++;
				if (Main.screenPosition == Vector2.Zero) updScreen0++;
				if (Main.soundVolume <= 0f && maxVolSeen > 0f) updVol0++;
				if (Main.dedServ) updDed++;
				if (Main.myPlayer != bMy) updMy++;
				if (Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers && Vector2.Distance(Main.Camera.Center, Main.LocalPlayer.Center) > 1500f) updCamFar++;
				if (Main.screenPosition != bScreen || Main.soundVolume != bVol || Main.myPlayer != bMy) updSwapped++;
			}
			orig();
			if (On) Flush();
		}

		static void Flush()
		{
			uint now = Main.GameUpdateCount;
			if (logAt == 0) logAt = (int)now + 120;
			if ((int)now < logAt) return;
			logAt = (int)now + 120;
			if (calls == 0 && upd == 0) return;
			var top = new List<KeyValuePair<string, int>>(swapStyles); top.Sort((a, b) => b.Value.CompareTo(a.Value));
			string t = ""; for (int i = 0; i < top.Count && i < 4; i++) t += (i > 0 ? "," : "") + top[i].Key + "=" + top[i].Value;
			Levy.Instance?.Logger.Info($"[AudioCap] client tick={now} plays={calls} army={army} world={world} | playedWhileSwapped myPlayer={swMy} screenPos={swScreen} soundVolume={swVol} dedServ={swDed} (dedServ ones are muted frames) swappedAudibleTop={t} | distance near={nearOk} beyond2500(silent)={silentDist} beyond10000(culled)={culled} | engineUpdate n={upd} screenPos0={updScreen0} soundVolume0={updVol0} dedServ={updDed} myPlayerOff={updMy} cameraFar={updCamFar} anySwapped={updSwapped} | tickEndLeaks myPlayer={leakMy} screenPos={leakScreen} soundVolume={leakVol} dedServ={leakDed}");
			calls = army = world = swMy = swScreen = swVol = swDed = silentDist = culled = nearOk = upd = updScreen0 = updVol0 = updDed = updMy = updCamFar = updSwapped = leakMy = leakScreen = leakVol = leakDed = 0; swapStyles.Clear();
		}

		/// <summary>The summary Eve can read in chat.</summary>
		public static string State() => On ? "Audio log is ON (lines go to client.log as [AudioCap] client)." : "Audio log is off.";
	}

	public class LevyAudioCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levyaudio";
		public override string Usage => "/levyaudio [on|off]";
		public override string Description => "Logs what the game's sound calls and listener state look like (client.log), for tracking down audio dropouts.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (args.Length > 0) AudioCapLog.On = args[0].Equals("on", StringComparison.OrdinalIgnoreCase) || args[0] == "1";
			else AudioCapLog.On = !AudioCapLog.On;
			caller.Reply(AudioCapLog.State(), AudioCapLog.On ? Color.LightGreen : Color.LightGray);
		}
	}
}
