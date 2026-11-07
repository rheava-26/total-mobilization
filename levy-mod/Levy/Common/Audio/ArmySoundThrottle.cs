using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using ReLogic.Utilities;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Levy.Common.Audio
{
	/// <summary>
	/// Keeps a big army from starving the sound engine. Sounds made while a Levy or Mechanization NPC or projectile is running its AI
	/// go through a per-style budget per short window, are dropped when far off screen, and play quieter when identical sounds stack.
	/// The player's own sounds are never inside that scope, so they are never throttled. Client only.
	/// </summary>
	public class ArmySoundThrottle : ModSystem
	{
		public const int WindowTicks = 12;       // 0.2 s
		public const int PerStyleMax = 3;        // same style, per window, all army sources combined
		public const int TotalMax = 10;          // all styles, per window
		public const float DropDistance = 2200f; // beyond this an army sound is never played
		public const float BusyDistance = 1100f; // beyond this it is dropped once the window is half full
		public const float MinVolume = 0.45f;

		/// <summary>Pure decision: returns the volume scale to play at (0 = drop). Counts are sounds already played this window.</summary>
		public static float Decide(int sameInWindow, int totalInWindow, float distance, float nearestSame = 0f)
		{
			if (distance > DropDistance) return 0f;
			// Every style is guaranteed one audible play per window, whatever the total budget: a fast weapon (Minishark, every 8 ticks) is never silenced by other styles filling the window.
			if (sameInWindow == 0) return 1f;
			// Closest first: a sound well nearer than any already played this window may exceed the style cap by up to two plays, at the floor volume.
			if (sameInWindow >= PerStyleMax && sameInWindow < PerStyleMax + 2 && distance < nearestSame * 0.5f) return MinVolume;
			if (sameInWindow >= PerStyleMax || totalInWindow >= TotalMax) return 0f;
			if (distance > BusyDistance && totalInWindow >= TotalMax / 2) return 0f;
			float v = 1f / (float)Math.Sqrt(1 + sameInWindow);
			return v < MinVolume ? MinVolume : v;
		}

		static bool inArmy;
		static int windowStart = int.MinValue, total;
		static readonly Dictionary<string, int> perStyle = new Dictionary<string, int>();
		static readonly Dictionary<string, float> nearest = new Dictionary<string, float>();

		static bool IsArmy(Mod m) => m != null && (m.Name == "Levy" || m.Name == "Mechanization");

		public override void Load()
		{
			if (Main.dedServ) return;
			On_NPC.AI += NpcAI;
			On_Projectile.AI += ProjAI;
			On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback += Play;
		}

		public override void Unload()
		{
			if (Main.dedServ) return;
			On_NPC.AI -= NpcAI;
			On_Projectile.AI -= ProjAI;
			On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback -= Play;
		}

		static void NpcAI(On_NPC.orig_AI orig, NPC self)
		{
			bool was = inArmy;
			inArmy = was || IsArmy(self.ModNPC?.Mod);
			try { orig(self); } finally { inArmy = was; }
		}

		static void ProjAI(On_Projectile.orig_AI orig, Projectile self)
		{
			bool was = inArmy;
			inArmy = was || IsArmy(self.ModProjectile?.Mod);
			try { orig(self); } finally { inArmy = was; }
		}

		static SlotId Play(On_SoundEngine.orig_PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback orig, ref SoundStyle style, Vector2? position, SoundUpdateCallback cb)
		{
			if (!inArmy || Main.gameMenu) return orig(ref style, position, cb);
			int now = (int)Main.GameUpdateCount;
			// long: the first window starts at int.MinValue, and an int subtraction overflows negative and never resets
			if ((long)now - windowStart >= WindowTicks || now < windowStart) { windowStart = now; total = 0; perStyle.Clear(); nearest.Clear(); }
			string key = style.SoundPath ?? "";
			perStyle.TryGetValue(key, out int same);
			float dist = position.HasValue ? Vector2.Distance(position.Value, Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f) : 0f;
			float near = nearest.TryGetValue(key, out float nv) ? nv : float.MaxValue;
			float vol = Decide(same, total, dist, near);
			if (vol <= 0f) return SlotId.Invalid;
			total++; perStyle[key] = same + 1; nearest[key] = Math.Min(near, dist);
			if (vol < 0.999f) { var quieter = style.WithVolumeScale(vol); return orig(ref quieter, position, cb); }
			return orig(ref style, position, cb);
		}

		/// <summary>Boot self-check of the pure logic plus a modelled big fight (25 levies + 10 drones). Returns failure text or null.</summary>
		public static string SelfCheck(out string numbers)
		{
			numbers = "";
			if (Decide(0, 0, 0f) != 1f) return "first sound is not full volume";
			if (!(Decide(2, 2, 0f) < Decide(1, 1, 0f))) return "stacked sounds do not get quieter";
			if (Decide(PerStyleMax, PerStyleMax, 0f) != 0f) return "per-style cap not enforced";
			if (Decide(1, TotalMax, 0f) != 0f) return "total cap not enforced";
			if (Decide(0, TotalMax, 0f) != 1f) return "first sound of a style not guaranteed";
			if (Decide(0, 0, DropDistance + 1) != 0f) return "far sound not dropped";
			if (Decide(1, TotalMax / 2, BusyDistance + 1) != 0f) return "far sound kept in busy window";
			if (Decide(0, 0, BusyDistance + 1) <= 0f) return "far sound dropped in quiet window";
			if (Decide(PerStyleMax - 1, 0, 0f) < MinVolume) return "volume under floor";
			// model: each shooter fires every useTime ticks (staggered), 4 distinct styles, 10 s
			int shooters = 35, secs = 10, before = 0, after = 0, ws = -999, tot = 0;
			var per = new int[4];
			for (int t = 0; t < 60 * secs; t++)
			{
				if (t - ws >= WindowTicks) { ws = t; tot = 0; Array.Clear(per, 0, 4); }
				for (int s = 0; s < shooters; s++)
				{
					int useTime = 10 + (s * 7) % 21; // 10..30 ticks
					if ((t + s) % useTime != 0) continue;
					before++;
					int st = s % 4; float d = (s % 5) * 400f;
					if (Decide(per[st], tot, d) > 0f) { after++; per[st]++; tot++; }
				}
			}
			// model: 6 Minishark levies (useTime 8, one style) beside 30 other shooters of 3 other styles that fill the window budget; the Minishark style must stay audible
			{
				int ws2 = -999, tot2 = 0, mini = 0, minisPlayed = 0, minisOld = 0, oldTot = 0, oldWs = -999, oldMini = 0;
				var per2 = new int[4]; var near2 = new float[4]; var oldPer = new int[4];
				for (int t = 0; t < 600; t++)
				{
					if (t - ws2 >= WindowTicks) { ws2 = t; tot2 = 0; Array.Clear(per2, 0, 4); for (int i = 0; i < 4; i++) near2[i] = float.MaxValue; }
					if (t - oldWs >= WindowTicks) { oldWs = t; oldTot = 0; Array.Clear(oldPer, 0, 4); }
					for (int s = 0; s < 36; s++)
					{
						bool isMini = s < 6; int useTime = isMini ? 8 : 10 + (s % 7);
						if ((t + s) % useTime != 0) continue;
						int st = isMini ? 0 : 1 + s % 3; float d = isMini ? 300f + s * 150f : (s % 5) * 300f;
						if (isMini) mini++;
						// previous rule (no guarantee): style cap and total cap only
						if (oldPer[st] < PerStyleMax && oldTot < TotalMax) { oldPer[st]++; oldTot++; if (isMini) oldMini++; }
						if (Decide(per2[st], tot2, d, near2[st]) > 0f) { per2[st]++; tot2++; near2[st] = Math.Min(near2[st], d); if (isMini) minisPlayed++; }
					}
				}
				minisOld = oldMini;
				numbers += $"minishark squad: {mini / 10f:0.0}/s fired -> {minisPlayed / 10f:0.0}/s audible (previous rule {minisOld / 10f:0.0}/s); ";
				if (minisPlayed <= 0) return "a modelled minishark squad produced no audible plays";
				if (minisPlayed / 10f < 4f) return "minishark squad under 4 audible plays per second";
			}
			numbers += $"modelled {shooters} shooters: {before / (float)secs:0.0}/s unthrottled -> {after / (float)secs:0.0}/s throttled";
			if (after >= before) return "throttle removed nothing in a big fight";
			if (after / (float)secs > TotalMax * 60f / WindowTicks) return "throttled rate above the total cap";
			return null;
		}

		public override void PostSetupRecipes()
		{
			string fail = SelfCheck(out string nums);
			if (fail == null) Mod.Logger.Info("[SoundThrottle] self-check ok :: " + nums);
			else Mod.Logger.Error("[SoundThrottle] self-check FAILED: " + fail);
		}
	}
}
