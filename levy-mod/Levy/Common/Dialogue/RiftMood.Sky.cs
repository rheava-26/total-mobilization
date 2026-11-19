using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// Round two of the rift mood: what the sky does after the covering, and what months of it do to the army.
	///   The rift grows as Calamity's later bosses fall (<see cref="RiftSky.Intensity"/>): tiers 1 to 4 (<see cref="Tier"/>). From tier 2 the chat talks about its size (RIFT_GROW1..3),
	///     and the first time the army sees a bigger tier a few levies near each commander react (once the boss fight is over).
	///   Water freezes over (RIFT_ICE), then all of it (RIFT_FROZEN): chat, and a small burst the first time each is seen.
	///   A Blood Moon under the rift (RIFT_BLOOD): the worst nights. A burst as it rises, and most chat is about it while it lasts.
	///   The fog (RIFT_FOG) while it is up, and a shocked burst from the levies within reach of a scare in it (RIFT_SCARE): a fog tree's scream, or a mirror figure vanishing.
	///   The long slide of morale by days under the rift: dark jokes from <see cref="LevyConstants.RiftDarkFromDay"/>, grim ones from <see cref="LevyConstants.RiftGrimFromDay"/>,
	///     worn-down and bitter talk from <see cref="LevyConstants.RiftWornFromDay"/>; the dark jokes thin out as the grim ones take over.
	/// The highest tier and ice level seen are saved with the world, so a burst is said once per world; a world where Levy first sees the rift already grown says nothing for it.
	/// </summary>
	public partial class RiftMood
	{
		/// <summary>Highest growth tier the army has seen (0 = not yet looked), and ice level (0 none, 1 still water, 2 everything).</summary>
		public static int TierSeen, IceSeen;
		private static int _pendingTier, _pendingIce;
		private static bool _skyPrimed, _wasBlood;
		private static int _scareAt = Never, _fogSince = Never, _fogEndedAt = Never;
		private static readonly byte[] ScKind = new byte[Main.maxNPCs];
		private static readonly float[] ScAi0 = new float[Main.maxNPCs];
		private static readonly Vector2[] ScPos = new Vector2[Main.maxNPCs];

		// ---- self-check overrides and telemetry ----
		internal static bool? TestBlood;
		internal static int ScaresSeen, BurstRounds, BurstsFired;
		internal static DialogueTrigger LastBurst;
		internal static readonly Dictionary<DialogueTrigger, int> BurstLines = new();

		/// <summary>A Blood Moon is up.</summary>
		public static bool BloodNow => TestBlood ?? Main.bloodMoon;

		/// <summary>How far the rift has grown: 1 after Moon Lord, 2 from <see cref="LevyConstants.RiftTier2At"/>, 3, 4.</summary>
		public static int Tier => TierOf(RiftSky.Intensity);

		internal static int TierOf(float intensity) => intensity >= LevyConstants.RiftTier4At ? 4 : intensity >= LevyConstants.RiftTier3At ? 3 : intensity >= LevyConstants.RiftTier2At ? 2 : 1;

		/// <summary>The pool that talks about a tier (none for tier 1: it is the hole the army already knows).</summary>
		internal static DialogueTrigger GrowPool(int tier) => tier >= 4 ? DialogueTrigger.RIFT_GROW3 : tier == 3 ? DialogueTrigger.RIFT_GROW2 : DialogueTrigger.RIFT_GROW1;

		/// <summary>0 still water free, 1 still water frozen, 2 everything frozen.</summary>
		public static int IceLevel => RiftSky.IceAll ? 2 : RiftSky.IceCover ? 1 : 0;

		/// <summary>The long slide: 0 the first days, 1 dark jokes, 2 grim ones, 3 worn down.</summary>
		public static int Morale => Days >= LevyConstants.RiftWornFromDay ? 3 : Days >= LevyConstants.RiftGrimFromDay ? 2 : Days >= LevyConstants.RiftDarkFromDay ? 1 : 0;

		private static bool InMood => Phase == RiftPhase.Tense || Phase == RiftPhase.Settled;

		/// <summary>The sky's own pools for the rift chat, most pressing first.</summary>
		private static void SkyOrder(ref int n, SoldierNPC body)
		{
			if (BloodNow && Main.rand.NextFloat() < LevyConstants.RiftBloodChance) Order[n++] = DialogueTrigger.RIFT_BLOOD;
			if (RiftSky.Fog && Main.rand.NextFloat() < LevyConstants.RiftFogChance) Order[n++] = DialogueTrigger.RIFT_FOG;
			int tier = Tier;
			if (tier >= 2 && Main.rand.NextFloat() < LevyConstants.RiftGrowChance) Order[n++] = GrowPool(tier);
			int ice = IceLevel;
			if (ice > 0 && Main.rand.NextFloat() < LevyConstants.RiftIceChance) Order[n++] = ice == 2 ? DialogueTrigger.RIFT_FROZEN : DialogueTrigger.RIFT_ICE;
		}

		/// <summary>The morale pools for the rift chat.</summary>
		private static void MoraleOrder(ref int n)
		{
			int m = Morale;
			if (m >= 3 && Main.rand.NextFloat() < LevyConstants.RiftWornChance) Order[n++] = DialogueTrigger.RIFT_WORN;
			if (m >= 2 && Main.rand.NextFloat() < LevyConstants.RiftGrimChance) Order[n++] = DialogueTrigger.RIFT_GRIM;
			float dark = m >= 3 ? LevyConstants.RiftDarkChanceWorn : m >= 2 ? LevyConstants.RiftDarkChanceLate : LevyConstants.RiftDarkChance;
			if (m >= 1 && Main.rand.NextFloat() < dark) Order[n++] = DialogueTrigger.RIFT_DARK;
		}

		/// <summary>Bursts: lines that are said in full whatever the mood (never thinned).</summary>
		internal static bool IsBurst(DialogueTrigger t)
		{
			switch (t)
			{
				case DialogueTrigger.RIFT_PANIC: case DialogueTrigger.RIFT_DUKE: case DialogueTrigger.RIFT_BLOOD: case DialogueTrigger.RIFT_GROW1: case DialogueTrigger.RIFT_GROW2:
				case DialogueTrigger.RIFT_GROW3: case DialogueTrigger.RIFT_ICE: case DialogueTrigger.RIFT_FROZEN: case DialogueTrigger.RIFT_SCARE:
					return true;
				default: return false;
			}
		}

		internal static void CountBurstLine(DialogueTrigger t) { BurstLines.TryGetValue(t, out int k); BurstLines[t] = k + 1; }
		internal static int BurstLinesOf(DialogueTrigger t) => BurstLines.TryGetValue(t, out int k) ? k : 0;

		private static void SkyReset()
		{
			TierSeen = IceSeen = 0; _pendingTier = _pendingIce = 0;
			_skyPrimed = false; _wasBlood = false; _scareAt = Never; _fogSince = Never; _fogEndedAt = Never;
			Array.Clear(ScKind); Array.Clear(ScAi0);
		}

		/// <summary>One tick: the growth and ice tiers, the Blood Moon rising, scares in the fog. Server and single player.</summary>
		private static void SkyTick(int now)
		{
			if (LevyUtil.IsClient) return;
			bool blood = BloodNow;
			if (!_skyPrimed) { _skyPrimed = true; _wasBlood = blood; ScanScares(now, false); return; }
			if (InMood)
			{
				int tier = Tier, ice = IceLevel;
				if (TierSeen == 0) { TierSeen = tier; IceSeen = ice; } // first look in this world: what is already there is not news
				if (tier > TierSeen) { TierSeen = tier; _pendingTier = tier; }
				if (ice > IceSeen) { IceSeen = ice; _pendingIce = ice; }
				bool fight = RosterFacts.BossFightOn();
				if (_pendingTier > 0 && !fight) { Burst(GrowPool(_pendingTier), LevyConstants.RiftGrowSpeakers, now); _pendingTier = 0; }
				if (_pendingIce > 0 && !fight) { Burst(_pendingIce == 2 ? DialogueTrigger.RIFT_FROZEN : DialogueTrigger.RIFT_ICE, LevyConstants.RiftIceSpeakers, now); _pendingIce = 0; }
				if (blood && !_wasBlood) Burst(DialogueTrigger.RIFT_BLOOD, LevyConstants.RiftBloodSpeakers, now);
			}
			_wasBlood = blood;
			bool fog = RiftSky.Fog && InMood;
			if (fog && _fogSince == Never) _fogSince = now; else if (!fog) { if (_fogSince != Never) _fogEndedAt = now; _fogSince = Never; }
			ScanScares(now, InMood);
		}

		/// <summary>
		/// The fog pulls the army apart: tiles an idle levy's follow slot sits further from the commander, growing over <see cref="LevyConstants.RiftFogDriftTicks"/> of fog to 2 to 5
		/// tiles (each levy its own, by record id). Nothing while it has a target, out of the fog, or with no rift mood. Server and single player (the follow logic runs there).
		/// </summary>
		public static float FogDriftTiles(SoldierRecord rec, bool fighting, int now)
		{
			if (fighting || rec == null || _fogSince == Never) return 0f;
			float t = Math.Min(1f, Math.Max(0f, (now - _fogSince) / (float)LevyConstants.RiftFogDriftTicks));
			float own = ((uint)rec.Id * 2654435761u >> 24) / 255f;
			return t * (LevyConstants.RiftFogDriftMinTiles + own * (LevyConstants.RiftFogDriftMaxTiles - LevyConstants.RiftFogDriftMinTiles));
		}

		/// <summary>
		/// How much a chat-bar line trembles (client): under the rift, a "..." or an action line ("*...*") the most; on a Blood Moon under it the despairing and angry lines a little.
		/// Off with the client setting, and with no rift mood.
		/// </summary>
		public static float ShakeFor(string text)
		{
			if (string.IsNullOrEmpty(text)) return 0f;
			try { if (global::Levy.Common.Observe.ObserveConfig.Get != null && !global::Levy.Common.Observe.ObserveConfig.Get.ShakingChatText) return 0f; } catch (Exception) { }
			if (!InMood) return 0f;
			if (text.StartsWith("...", StringComparison.Ordinal) || (text.StartsWith("*", StringComparison.Ordinal) && BloodNow)) return 1f;
			if (BloodNow && DespairText(text)) return 0.6f;
			return 0f;
		}

		private static HashSet<string> _despair; private static int _despairOf = -1;
		/// <summary>A line of the Blood Moon, angry, silent or worn-down pools (placeholder lines are matched by their literal start).</summary>
		internal static bool DespairText(string text)
		{
			if (_despair == null || _despairOf != DialogueTable.LineCount)
			{
				_despair = new HashSet<string>(StringComparer.Ordinal); _despairOf = DialogueTable.LineCount;
				foreach (DialogueTrigger t in new[] { DialogueTrigger.RIFT_BLOOD, DialogueTrigger.RIFT_ANGRY, DialogueTrigger.RIFT_HUSH, DialogueTrigger.RIFT_WORN })
				{
					var pool = DialogueTable.Get(t);
					if (pool != null) foreach (DialogueLine l in pool) { int b = l.Text.IndexOf('{'); _despair.Add(b < 0 ? l.Text : l.Text.Substring(0, b)); }
				}
			}
			if (_despair.Contains(text)) return true;
			foreach (string p in _despair) if (p.Length >= 6 && text.StartsWith(p, StringComparison.Ordinal) && p != text) return true;
			return false;
		}

		/// <summary>The half minute after the fog lifts: drifted levies walk back to their usual places instead of staying where the follow slack left them.</summary>
		public static bool FogRegrouping(int now) => _fogEndedAt != Never && (uint)(now - _fogEndedAt) < 60u * 30u;

		/// <summary>A few levies near each commander, one after another.</summary>
		private static void Burst(DialogueTrigger pool, int speakers, int now)
		{
			BurstsFired++; LastBurst = pool;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active || p.dead) continue;
				if (CommanderReactions.Enqueue(p, pool, speakers, now, LevyConstants.RiftBurstStepTicks)) BurstRounds++;
			}
		}

		/// <summary>
		/// Watches the fog creatures slot by slot. A scare is a fog tree going from its creep (ai[0] 0) to its scream (1), or a mirror figure vanishing within reach of a player.
		/// <paramref name="speak"/> false only records (the first tick of a world, or no rift mood).
		/// </summary>
		private static void ScanScares(int now, bool speak)
		{
			int fogwood = RiftSky.FogwoodType, mirror = RiftSky.MirrorwalkerType;
			if (fogwood < 0 && mirror < 0) return;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				byte kind = n.active ? (n.type == fogwood ? (byte)1 : n.type == mirror ? (byte)2 : (byte)0) : (byte)0;
				float ai0 = kind == 1 ? n.ai[0] : 0f;
				if (speak && IsScare(ScKind[i], ScAi0[i], kind, ai0, !n.active && n.life > 0) && (ScKind[i] != 2 || NearPlayer(ScPos[i], LevyConstants.RiftScareReachTiles)))
				{
					ScaresSeen++;
					if ((uint)(now - _scareAt) >= (uint)LevyConstants.RiftScareCooldownTicks || _scareAt == Never)
					{
						_scareAt = now;
						if (CommanderReactions.EnqueueNear(kind == 1 ? n.Center : ScPos[i], LevyConstants.RiftScareReachTiles, DialogueTrigger.RIFT_SCARE, LevyConstants.RiftScareSpeakers, now, LevyConstants.RiftBurstStepTicks)) BurstRounds++;
					}
				}
				ScKind[i] = kind; ScAi0[i] = ai0;
				if (kind != 0) ScPos[i] = n.Center;
			}
		}

		/// <summary>
		/// The slot's last and current state make a scare: a fog tree that was creeping now screams, or a mirror figure that was there is gone without being killed
		/// (<paramref name="vanishedAlive"/>: the slot is empty and the NPC in it still had life).
		/// </summary>
		internal static bool IsScare(byte wasKind, float wasAi0, byte kind, float ai0, bool vanishedAlive)
		{
			if (wasKind == 1 && kind == 1) return wasAi0 == 0f && ai0 == 1f;
			if (wasKind == 2) return kind != 2 && vanishedAlive;
			return false;
		}

		private static bool NearPlayer(Vector2 at, float tiles)
		{
			float r = tiles * 16f;
			for (int i = 0; i < Main.maxPlayers; i++) { Player p = Main.player[i]; if (p != null && p.active && !p.dead && Vector2.Distance(p.Center, at) <= r) return true; }
			return false;
		}
	}
}
