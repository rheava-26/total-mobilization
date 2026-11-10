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
		/// <summary>LEVY_AUDIO_OLD=1 (lab): the previous rules (caps 3 / 10, only Levy and Mechanization's own entities in scope), for before/after numbers.</summary>
		internal static readonly bool OldRules = Environment.GetEnvironmentVariable("LEVY_AUDIO_OLD") == "1";
		public const int WindowTicks = 12;       // 0.2 s
		public static readonly int PerStyleMax = OldRules ? 3 : 2;   // same style, per window, all army sources combined
		public static readonly int TotalMax = OldRules ? 10 : 4;     // all styles, per window (4 per 0.2 s = 20 a second, then only the one-per-style guarantee)
		public static readonly int HardExtra = OldRules ? 1000 : 2;  // the one-per-style guarantee may take the window this far past TotalMax, never further
		/// <summary>What kind of army entity is making the sound.</summary>
		public const int KShot = 1, KMinion = 2, KSentry = 3, KDrone = 4, KEnemy = 5, KPlayer = 6, KOther = 7, KindCount = 8;
		public static readonly string[] KindNames = { "none", "weapon", "minion", "sentry", "drone", "enemy", "player", "other" };
		/// <summary>Per window, all styles of one kind: summoned things are background noise, so they get a small share of the window.</summary>
		public static int KindMax(int kind) => OldRules ? int.MaxValue : kind == KMinion ? 2 : kind == KSentry ? 2 : kind == KDrone ? 2 : int.MaxValue;
		/// <summary>The loudness each kind plays at before the player's Army sound volume.</summary>
		public static float KindGain(int kind) => OldRules ? 1f : kind == KMinion || kind == KSentry ? 0.7f : kind == KDrone ? 0.8f : 1f;
		/// <summary>The config slider (Army sound volume), 1 under the previous rules.</summary>
		static float UserVolume() { if (OldRules) return 1f; try { return Config.LevyConfig.Get?.ArmySoundVolume ?? 0.6f; } catch (Exception) { return 0.6f; } }
		public const float DropDistance = 2200f; // beyond this an army sound is never played
		public const float BusyDistance = 1100f; // beyond this it is dropped once the window is half full
		public const float MinVolume = 0.45f;

		/// <summary>Pure decision: returns the volume scale to play at (0 = drop). Counts are sounds already played this window.</summary>
		public static float Decide(int sameInWindow, int totalInWindow, float distance, float nearestSame = 0f, int kindInWindow = 0, int kindMax = int.MaxValue)
		{
			if (distance > DropDistance) return 0f;
			if (totalInWindow >= TotalMax + HardExtra) return 0f; // the hard ceiling: even the guarantee below stops here
			if (kindInWindow >= kindMax) return 0f;
			// Every style is guaranteed one audible play per window, up to the hard ceiling: a fast weapon (Minishark, every 8 ticks) is never silenced by other styles filling the window.
			if (sameInWindow == 0) return 1f;
			// Closest first: a sound well nearer than any already played this window may exceed the style cap by up to two plays, at the floor volume.
			if (sameInWindow >= PerStyleMax && sameInWindow < PerStyleMax + 2 && distance < nearestSame * 0.5f) return MinVolume;
			if (sameInWindow >= PerStyleMax || totalInWindow >= TotalMax) return 0f;
			if (distance > BusyDistance && totalInWindow >= TotalMax / 2) return 0f;
			float v = 1f / (float)Math.Sqrt(1 + sameInWindow);
			return v < MinVolume ? MinVolume : v;
		}

		/// <summary>The kind of army entity whose update is running (0 = none). Set by the NPC / projectile detours.</summary>
		static int kind;
		static bool inArmy => kind != 0 && kind < KEnemy;
		static int windowStart = int.MinValue, total;
		static readonly int[] perKind = new int[KindCount];
		static readonly Dictionary<string, int> perStyle = new Dictionary<string, int>();
		static readonly Dictionary<string, float> nearest = new Dictionary<string, float>();

		/// <summary>Plays a sound as if an army NPC's AI had made it, so it goes through the budget, distance drop and stacking volume. Client only.</summary>
		public static void PlayAsArmy(SoundStyle style, Vector2 pos)
		{
			if (Main.dedServ) return;
			int was = kind; kind = KShot;
			try { SoundEngine.PlaySound(style, pos); } finally { kind = was; }
		}

		static bool IsArmy(Mod m) => m != null && (m.Name == "Levy" || m.Name == "Mechanization");

		/// <summary>The kind a projectile's update makes sounds as: a projectile tagged to a levy (whatever mod made it: Calamity's minions, sentries and shots too), a Mechanization one, else (diag) the commander's or an enemy's.</summary>
		static int ProjKind(Projectile p)
		{
			if (p.TryGetGlobalProjectile(out Globals.SoldierProjectileTag t) && t.SoldierIndex >= 0) return p.sentry ? KSentry : p.minion ? KMinion : KShot;
			if (p.ModProjectile?.Mod?.Name == "Mechanization" || (t != null && t.DroneShot)) return KDrone;
			if (OldRules) return p.ModProjectile != null && IsArmy(p.ModProjectile.Mod) ? KShot : 0;
			if (!Diag) return 0;
			return p.hostile ? KEnemy : p.owner >= 0 && p.owner < Main.maxPlayers - 1 && Main.player[p.owner].active ? KPlayer : KOther;
		}

		static int NpcKind(NPC n)
		{
			var m = n.ModNPC?.Mod;
			if (m != null && m.Name == "Mechanization") return KDrone;
			if (m != null && m.Name == "Levy") return KShot;
			return Diag && !n.friendly && !n.townNPC ? KEnemy : 0;
		}

		/// <summary>LEVY_AUDIOCAP=1: the detour also runs on a dedicated server (lab harness) and logs the PlaySound stream per second as [AudioCap] lines. Never set in play.</summary>
		static readonly bool Diag = Environment.GetEnvironmentVariable("LEVY_AUDIOCAP") == "1";
		static bool isServer; // Main.dedServ at load: owner frames flip Main.dedServ on a client, which must not look like a server
		static int dCalls, dArmy, dAdmit, dDrop, dLastAdmit = -1, dMaxGap, dLogAt, dLastArmy = -1, dMaxRaw, dGaps12, dGapsRaw12;
		static readonly Dictionary<string, int> dStyle = new Dictionary<string, int>();
		static readonly Dictionary<string, int> dOther = new Dictionary<string, int>();
		static int dInFrame;
		/// <summary>Set by the diag log: true while an owner frame is open (such sounds are dropped on a real client because the frame forces dedServ).</summary>
		public static Func<bool> InFrame = () => global::Levy.Common.Owner.SoldierOwner.Depth > 0;

		static readonly int[] dKindRaw = new int[KindCount], dKindAdmit = new int[KindCount];
		static void DiagTick(int now, string key, bool army, bool admitted, int k = KOther)
		{
			dCalls++; dKindRaw[k]++; if (!army || admitted) dKindAdmit[k]++;
			if (!army) { if (InFrame()) dInFrame++; else { dOther.TryGetValue(key, out int o); dOther[key] = o + 1; } }
			if (army) { dArmy++; if (dLastArmy >= 0) { int g = now - dLastArmy; if (g > dMaxRaw) dMaxRaw = g; if (g >= 12) dGapsRaw12++; } dLastArmy = now; dStyle.TryGetValue(key, out int c); dStyle[key] = c + 1; if (admitted) { dAdmit++; if (dLastAdmit >= 0) { int g = now - dLastAdmit; if (g > dMaxGap) dMaxGap = g; if (g >= 12) dGaps12++; } dLastAdmit = now; } else dDrop++; }
			if (dLogAt == 0) dLogAt = now + 120;
			if (now >= dLogAt)
			{
				var top = new List<KeyValuePair<string, int>>(dStyle); top.Sort((a, b) => b.Value.CompareTo(a.Value));
				string t = ""; for (int i = 0; i < top.Count && i < 4; i++) t += (i > 0 ? "," : "") + top[i].Key.Substring(top[i].Key.LastIndexOf('/') + 1) + "=" + top[i].Value;
				var oth = new List<KeyValuePair<string, int>>(dOther); oth.Sort((a, b) => b.Value.CompareTo(a.Value));
				string ot = ""; for (int i = 0; i < oth.Count && i < 5; i++) ot += (i > 0 ? "," : "") + oth[i].Key.Substring(oth[i].Key.LastIndexOf('/') + 1) + "=" + oth[i].Value;
				Levy.Instance?.Logger.Info($"[AudioCap] nonArmyInFrame={dInFrame} nonArmyOutside={dOther.Count} otherTop={ot}");
				{ var kb = new System.Text.StringBuilder(); for (int q = 1; q < KindCount; q++) kb.Append(q > 1 ? " " : "").Append(KindNames[q]).Append('=').Append(dKindRaw[q]).Append('/').Append(dKindAdmit[q]); Levy.Instance?.Logger.Info($"[Crowd] audio calls/played by source over {(dLogAt == 0 ? 0 : 120)} ticks: {kb} | rules={(OldRules ? "old" : "new")} volume={UserVolume():0.00}"); Array.Clear(dKindRaw, 0, KindCount); Array.Clear(dKindAdmit, 0, KindCount); }
				dInFrame = 0; dOther.Clear();
				Levy.Instance?.Logger.Info($"[AudioCap] tick={now} calls={dCalls} army={dArmy} admitted={dAdmit} dropped={dDrop} styles={dStyle.Count} maxAdmitGapTicks={dMaxGap} maxRawGapTicks={dMaxRaw} admitGaps12={dGaps12} rawGaps12={dGapsRaw12} top={t}");
				dCalls = dArmy = dAdmit = dDrop = dMaxGap = dMaxRaw = dGaps12 = dGapsRaw12 = 0; dStyle.Clear(); dLogAt = now + 120;
			}
		}

		public override void Load()
		{
			isServer = Main.dedServ;
			if (Main.dedServ && !Diag) return;
			On_NPC.AI += NpcAI;
			On_Projectile.AI += ProjAI;
			On_Projectile.Update += ProjUpdate;
			On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback += Play;
		}

		public override void Unload()
		{
			if (Main.dedServ && !Diag) return;
			On_NPC.AI -= NpcAI;
			On_Projectile.AI -= ProjAI;
			On_Projectile.Update -= ProjUpdate;
			On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback -= Play;
		}

		static void NpcAI(On_NPC.orig_AI orig, NPC self)
		{
			int was = kind;
			if (was == 0) kind = NpcKind(self);
			try { orig(self); } finally { kind = was; }
		}

		static void ProjAI(On_Projectile.orig_AI orig, Projectile self)
		{
			int was = kind;
			if (was == 0) kind = ProjKind(self);
			try { orig(self); } finally { kind = was; }
		}

		/// <summary>The whole update, not only the AI: a levy's projectile also makes sounds when it hits (the strike, the enemy's hurt and death sound) and from its on-hit hooks.</summary>
		static void ProjUpdate(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			int was = kind;
			if (was == 0) kind = ProjKind(self);
			try { orig(self, i); } finally { kind = was; }
		}

		static SlotId Play(On_SoundEngine.orig_PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback orig, ref SoundStyle style, Vector2? position, SoundUpdateCallback cb)
		{
			if (Diag && isServer) { AudioCapLog.OnPlay(ref style, position, inArmy); int n0 = (int)Main.GameUpdateCount; bool a0 = inArmy; int k0 = kind == 0 ? KOther : kind; bool ok = !a0 || Admit(ref style, position, n0, true) > 0f; DiagTick(n0, style.SoundPath ?? "", a0, ok, k0); return SlotId.Invalid; }
			if (AudioCapLog.On && !isServer) AudioCapLog.OnPlay(ref style, position, inArmy);
			if (!inArmy || Main.gameMenu) return orig(ref style, position, cb);
			int now = (int)Main.GameUpdateCount;
			float vol = Admit(ref style, position, now, false);
			if (vol <= 0f) return SlotId.Invalid;
			vol *= KindGain(kind) * UserVolume();
			if (vol < 0.999f) { var quieter = style.WithVolumeScale(vol); return orig(ref quieter, position, cb); }
			return orig(ref style, position, cb);
		}

		/// <summary>Window bookkeeping plus <see cref="Decide"/> for one army sound; returns the volume scale (0 = drop) and records the play when admitted.</summary>
		static float Admit(ref SoundStyle style, Vector2? position, int now, bool ignoreDistance)
		{
			float dist = !ignoreDistance && position.HasValue ? Vector2.Distance(position.Value, Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f) : 0f;
			return AdmitKey(style.SoundPath ?? "", dist, now, kind);
		}

		/// <summary>The stateful part (window reset, per-style counts), separated from SoundStyle so the boot check can drive it.</summary>
		static float AdmitKey(string key, float dist, int now, int k = KShot)
		{
			// long: the first window starts at int.MinValue, and an int subtraction overflows negative and never resets
			if ((long)now - windowStart >= WindowTicks || now < windowStart) { windowStart = now; total = 0; perStyle.Clear(); nearest.Clear(); Array.Clear(perKind, 0, perKind.Length); }
			perStyle.TryGetValue(key, out int same);
			float near = nearest.TryGetValue(key, out float nv) ? nv : float.MaxValue;
			int ki = k < 0 || k >= KindCount ? KShot : k;
			float vol = Decide(same, total, dist, near, perKind[ki], KindMax(ki));
			if (vol <= 0f) return 0f;
			total++; perKind[ki]++; perStyle[key] = same + 1; nearest[key] = Math.Min(near, dist);
			return vol;
		}

		/// <summary>[AudioCap] boot check of the stateful path (what Play() runs), unlike <see cref="SelfCheck"/> which only exercises the pure Decide. Drives AdmitKey with a modelled 40-shooter fight from the
		/// session-start state at three tick counters (0, 1,000,000, near int.MaxValue); every style that fires at least once per window must be audible within two windows, and the admitted rate must stay
		/// under the cap plus the one-per-style guarantee. Returns failure text or null.</summary>
		public static string StatefulCheck(out string numbers)
		{
			numbers = "";
			int sw0 = windowStart, t0 = total; var ps = new Dictionary<string, int>(perStyle); var nr = new Dictionary<string, float>(nearest);
			try
			{
				foreach (int start in new[] { 0, 1000000, int.MaxValue - 300 })
				{
					windowStart = int.MinValue; total = 0; perStyle.Clear(); nearest.Clear(); Array.Clear(perKind, 0, perKind.Length);
					const int styles = 15, shooters = 40, ticks = 1200; // 20 s
					var last = new int[styles]; var maxGap = new int[styles]; int admitted = 0, calls = 0;
					for (int k = 0; k < ticks; k++)
					{
						int now = unchecked(start + k);
						for (int sh = 0; sh < shooters; sh++)
						{
							int useTime = 8 + (sh * 5) % 17; if ((k + sh) % useTime != 0) continue;
							int st = sh % styles; calls++;
							if (AdmitKey("style" + st, (sh % 7) * 300f, now) > 0f) { admitted++; int g = k - last[st]; if (g > maxGap[st]) maxGap[st] = g; last[st] = k; }
						}
					}
					int worst = 0; for (int i = 0; i < styles; i++) if (maxGap[i] > worst) worst = maxGap[i];
					numbers += $"start {start}: {calls} calls -> {admitted} admitted, worst per-style gap {worst} ticks; ";
					if (admitted == 0) return "stateful throttle admitted nothing (window never reset?)";
					if (worst > 2 * WindowTicks + 30) return $"a style went {worst} ticks without an audible play from start {start}";
					if (admitted / (ticks / 60f) > (TotalMax + HardExtra) * 60f / WindowTicks) return "admitted rate above the cap plus one-per-style guarantee";
				}
			}
			finally { windowStart = sw0; total = t0; perStyle.Clear(); foreach (var kv in ps) perStyle[kv.Key] = kv.Value; nearest.Clear(); foreach (var kv in nr) nearest[kv.Key] = kv.Value; }
			return null;
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
			if (after / (float)secs > (TotalMax + HardExtra) * 60f / WindowTicks) return "throttled rate above the total cap";
			return null;
		}

		public override void PostSetupRecipes()
		{
			string fail = SelfCheck(out string nums);
			if (fail == null) Mod.Logger.Info("[SoundThrottle] self-check ok :: " + nums);
			else Mod.Logger.Error("[SoundThrottle] self-check FAILED: " + fail);
			string f2 = StatefulCheck(out string n2);
			if (f2 == null) Mod.Logger.Info("[AudioCap] self-check ok fail=0 :: " + n2);
			else Mod.Logger.Error("[AudioCap] self-check FAILED fail=1: " + f2);
		}
	}
}
