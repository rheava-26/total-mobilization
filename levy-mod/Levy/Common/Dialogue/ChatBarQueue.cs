using System;
using System.Collections.Generic;

namespace Levy.Common.Dialogue
{
	/// <summary>One line on (or waiting for) a chat bar.</summary>
	public sealed class ChatBarEntry
	{
		/// <summary>NPC index of the speaker.</summary>
		public int Speaker;
		public string Text = "";
		/// <summary>1 chatter, 2 notice, 3 big moment. Higher outranks lower for slots.</summary>
		public int Priority;
		/// <summary>Tick the line was queued.</summary>
		public int Queued;
		/// <summary>Tick the line took a slot (the typewriter starts here). -1 while waiting.</summary>
		public int Shown = -1;
		/// <summary>Distance to the local player in tiles, refreshed every tick.</summary>
		public float DistTiles;

		/// <summary>Draw-side cache (wrapped rows and size), owned by the renderer.</summary>
		public object Layout;

		public bool IsShown => Shown >= 0;

		/// <summary>Characters revealed so far by the typewriter.</summary>
		public int RevealedChars(int now) => Shown < 0 ? 0 : Math.Min(Text.Length, (int)((now - Shown) * LevyConstants.ChatBarCharsPerTick) + 1);

		/// <summary>Ticks the typewriter needs for the whole line.</summary>
		public int RevealTicks => (int)Math.Ceiling(Text.Length / LevyConstants.ChatBarCharsPerTick);

		/// <summary>Ticks the bar lives once it takes a slot, fade included.</summary>
		public int LifeTicks => RevealTicks + Math.Min(LevyConstants.ChatBarHoldMaxTicks, LevyConstants.ChatBarHoldBaseTicks + Text.Length * LevyConstants.ChatBarHoldPerCharTicks) + LevyConstants.ChatBarFadeTicks;

		/// <summary>1 while fully shown, ramping to 0 over the fade; 0 once the bar is over.</summary>
		public float Alpha(int now)
		{
			if (Shown < 0) return 0f;
			int left = Shown + LifeTicks - now;
			if (left <= 0) return 0f;
			return left >= LevyConstants.ChatBarFadeTicks ? 1f : left / (float)LevyConstants.ChatBarFadeTicks;
		}

		/// <summary>Slot-winning score: a priority step outweighs any distance; among equals the nearer speaker wins.</summary>
		public float Score => Priority * 1000f - DistTiles;
	}

	/// <summary>
	/// The chat-bar scheduler: decides which lines get one of the few on-screen bars. Pure logic (no Terraria), so the boot self-check can drive it.
	/// Rules: one line per speaker; a line from a speaker already on a bar replaces it only when it is at least as important or the old bar has finished typing;
	/// speakers beyond range are never shown; when the slots are full, a waiting line bumps the weakest bar only if it is more important and that bar has been up long enough to be read;
	/// otherwise it waits (nearest and most important first) and goes stale after ChatBarWaitExpireTicks.
	/// </summary>
	public sealed class ChatBarQueue
	{
		public readonly List<ChatBarEntry> Active = new();
		public readonly List<ChatBarEntry> Waiting = new();
		public int MaxVisible = LevyConstants.ChatBarMaxVisible;
		public int MaxWaiting = LevyConstants.ChatBarMaxWaiting;
		public float RangeTiles = LevyConstants.ChatBarRangeTiles;

		/// <summary>Lines dropped because the queue was full or they went stale (diagnostics).</summary>
		public int Dropped;

		public void Clear() { Active.Clear(); Waiting.Clear(); Dropped = 0; }

		/// <summary>Queue a line. Returns false when it was refused outright (empty text, or a weaker line from a speaker whose bar is still typing).</summary>
		public bool Enqueue(int speaker, string text, int priority, int now, float distTiles)
		{
			if (string.IsNullOrEmpty(text) || distTiles > RangeTiles) return false;
			var e = new ChatBarEntry { Speaker = speaker, Text = text, Priority = priority, Queued = now, DistTiles = distTiles };

			ChatBarEntry live = Find(Active, speaker);
			if (live != null)
			{
				bool finishedTyping = now - live.Shown >= live.RevealTicks;
				if (priority < live.Priority && !finishedTyping) return false;
				Active.Remove(live);
				e.Shown = now; // the speaker keeps its slot with the new line
				Active.Add(e);
				return true;
			}

			ChatBarEntry wait = Find(Waiting, speaker);
			if (wait != null)
			{
				if (priority < wait.Priority) return false;
				Waiting.Remove(wait);
			}
			Waiting.Add(e);
			if (Waiting.Count > MaxWaiting) DropWeakestWaiting();
			return true; // a slot is handed out on the next Tick, so a burst in one tick is ordered by score, not by arrival
		}

		/// <summary>Advance one tick. <paramref name="distTiles"/> returns the speaker's distance to the local player in tiles, or a negative number when the speaker is gone.</summary>
		public void Tick(int now, Func<int, float> distTiles)
		{
			for (int i = Active.Count - 1; i >= 0; i--)
			{
				var e = Active[i];
				float d = distTiles(e.Speaker);
				if (d < 0f || e.Alpha(now) <= 0f) { Active.RemoveAt(i); continue; }
				e.DistTiles = d;
			}
			for (int i = Waiting.Count - 1; i >= 0; i--)
			{
				var e = Waiting[i];
				float d = distTiles(e.Speaker);
				if (d < 0f || d > RangeTiles || now - e.Queued > LevyConstants.ChatBarWaitExpireTicks) { Waiting.RemoveAt(i); Dropped++; continue; }
				e.DistTiles = d;
			}
			Promote(now);
		}

		private void Promote(int now)
		{
			while (Waiting.Count > 0)
			{
				ChatBarEntry best = Best(Waiting);
				if (Active.Count < MaxVisible) { Take(best, now); continue; }
				ChatBarEntry weakest = Worst(Active);
				bool bumpable = best.Priority > weakest.Priority && now - weakest.Shown >= LevyConstants.ChatBarMinShowTicks;
				if (!bumpable) break;
				Active.Remove(weakest);
				Take(best, now);
			}
		}

		private void Take(ChatBarEntry e, int now)
		{
			Waiting.Remove(e);
			e.Shown = now;
			Active.Add(e);
		}

		private void DropWeakestWaiting()
		{
			ChatBarEntry worst = Worst(Waiting);
			if (worst != null) { Waiting.Remove(worst); Dropped++; }
		}

		private static ChatBarEntry Find(List<ChatBarEntry> list, int speaker)
		{
			for (int i = 0; i < list.Count; i++) if (list[i].Speaker == speaker) return list[i];
			return null;
		}

		private static ChatBarEntry Best(List<ChatBarEntry> list)
		{
			ChatBarEntry b = null;
			foreach (var e in list) if (b == null || e.Score > b.Score) b = e;
			return b;
		}

		private static ChatBarEntry Worst(List<ChatBarEntry> list)
		{
			ChatBarEntry w = null;
			foreach (var e in list) if (w == null || e.Score < w.Score) w = e;
			return w;
		}
	}
}
