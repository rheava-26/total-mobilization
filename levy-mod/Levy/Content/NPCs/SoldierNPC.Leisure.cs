using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Movement;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// OFF-DUTY LIFE of a free-roaming levy. A Regular on Free Rein that has been calm for a while (no target, nothing hostile near, not hurt) takes up a pastime for a few seconds to a minute, then goes back to
	/// roaming its base: fly a kite in open sky by day, roast a marshmallow at a lit campfire, sit by the fire or on a chair and chat (the idle sitting of SoldierNPC.Sit.cs), drink at a keg,
	/// sleep in a bed at night, stargaze on a clear night, and now and then cheer. All the props are vanilla items and tiles drawn through the player renderer: the levy holds the vanilla item in its hand
	/// (client draw only; the real weapon is put back after every draw), the kite is the vanilla kite sprite on a string. Nothing is spawned: no projectile, no NPC, no item, no buff.
	/// A fight, a threat, a hit, a talk or any order ends a pastime on the spot. Server logic picks and walks; clients only mirror one byte (the pastime) and draw it.
	/// </summary>
	public partial class SoldierNPC
	{
		internal const int LzNone = 0, LzKite = 1, LzRoast = 2, LzSit = 3, LzKeg = 4, LzSleep = 5, LzStars = 6, LzCheer = 8;
		internal static readonly string[] LzNames = { "none", "kite", "marshmallow", "sit", "keg", "sleep", "stargaze", "-", "cheer" };

		/// <summary>Harness / config switch: -1 follows the config, 0 off, 1 on.</summary>
		internal static int LeisureOverride = -1;
		private static bool LeisureOn
		{
			get
			{
				if (LeisureOverride >= 0) return LeisureOverride == 1;
				try { return LevyConfig.Get.FreeReinPastimes; } catch (Exception) { return true; }
			}
		}
		internal static bool LeisureEnabledForLab => LeisureOn;

		/// <summary>0 not at a pastime, 1 walking to its place, 2 at it.</summary>
		private int _lzMode, _lzKind, _lzDir = 1, _lzEndAt, _lzNextAt, _lzWalkSince, _lzCalmSince, _lzChatAt, _lzSitUntil, _lzEmoteAt;
		private Vector2 _lzSpot; private Point _lzProp;
		/// <summary>Client mirror of the pastime.</summary>
		private int _lzNet;
		private Item _lzItem; private bool _lzSwapped, _lzSleepPosed;
		private Vector2 _lzKitePos, _lzKiteVel; private bool _lzKiteInit; private uint _lzKiteTick; private int _lzKiteType;
		private static readonly int[] LzKiteTypes = { ProjectileID.KiteBlue, ProjectileID.KiteBlueAndYellow, ProjectileID.KiteRed, ProjectileID.KiteRedAndYellow, ProjectileID.KiteYellow };

		// lab counters
		internal static readonly int[] LeisureStarts = new int[9];
		internal static long LeisureTicks, LeisureCalls; internal static int LeisureChats, LeisureAbandons, LeisureNoSpot;
		internal static void LeisureStatReset() { Array.Clear(LeisureStarts); LeisureTicks = LeisureCalls = 0; LeisureChats = LeisureAbandons = LeisureNoSpot = 0; FreeReinRecalls = FreeReinStranded = 0; }
		internal static string LeisureStatLine()
		{
			var sb = new System.Text.StringBuilder("starts:");
			for (int i = 0; i < LzNames.Length; i++) if (LzNames[i] != "-") sb.Append(' ').Append(LzNames[i]).Append('=').Append(LeisureStarts[i]);
			double us = LeisureCalls > 0 ? LeisureTicks * 1e6 / System.Diagnostics.Stopwatch.Frequency / LeisureCalls : 0;
			sb.Append($" chats={LeisureChats} abandoned={LeisureAbandons} noSpot={LeisureNoSpot} thinkCalls={LeisureCalls} usPerThink={us:0.00} totalMs={LeisureTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.0} recalls={FreeReinRecalls} stranded={FreeReinStranded}");
			return sb.ToString();
		}

		/// <summary>The pastime now (the server's state; on a client, the mirrored byte).</summary>
		internal int LeisureKindNow => LevyUtil.IsClient ? _lzNet : (_lzMode != 0 ? _lzKind : 0);
		internal int LeisureKindForLab => _lzMode == 2 || _lzKind == LzSit ? LeisureKindNow : 0;
		private byte Leisure_NetByte() => (byte)(_lzMode == 2 && _lzKind != LzSit ? _lzKind : 0);
		private void Leisure_ReadNet(byte b) => _lzNet = b;

		// ---------------- stopping ----------------
		internal void Leisure_Stop() => Leisure_StopAt((int)Main.GameUpdateCount, false);

		private void Leisure_StopAt(int now, bool gap)
		{
			if (_lzMode == 0) return;
			if (_lzKind == LzSit) { _lzSitUntil = 0; Sit_Stop(); }
			else if (HasMoveGoal && MoveGoal == _lzSpot) ClearGoal();
			if (_lzMode == 2) NPC.netUpdate = true;
			_lzMode = 0; _lzKind = 0; _lzNet = 0;
			if (gap) _lzNextAt = now + Main.rand.Next(LevyConstants.LeisureGapMinTicks, LevyConstants.LeisureGapMaxTicks);
		}

		/// <summary>Every brain think, before the fight: the reasons to drop a pastime.</summary>
		private void Leisure_Guard(int now, int fleeIdx, float nearestHostileTiles)
		{
			if (_lzMode == 0) return;
			bool threat = fleeIdx >= 0 || TargetIndex >= 0 || nearestHostileTiles < LevyConstants.SitWakeEnemyTiles || State == SoldierState.Engage || State == SoldierState.Shaken
				|| State == SoldierState.Routed || State == SoldierState.Desert || State == SoldierState.Dead;
			if (threat || !LeisureOn || !FreeReining || OfficerMode || Record == null || MutinySystem.IsMutinous(Record) || BeingTalkedTo || TicksSinceDamaged < LevyConstants.ThinkIntervalTicks * 2)
				Leisure_StopAt(now, true);
		}

		/// <summary>A sleeper stays in its bed: no movement while asleep.</summary>
		private bool Leisure_Hold()
		{
			if (_lzMode != 2 || _lzKind != LzSleep) return false;
			if (TargetIndex >= 0 || !IsAlive || OfficerMode) { Leisure_Stop(); return false; }
			NPC.velocity = Vector2.Zero; NPC.Bottom = _lzSpot; NPC.direction = _lzDir; NPC.spriteDirection = _lzDir;
			return true;
		}

		// ---------------- the think ----------------
		/// <summary>Called where the brain would otherwise follow the order. True when the pastime spent this think (walking to its place, or at it).</summary>
		private bool Leisure_Think(int now, float nearestHostileTiles)
		{
			if (!FreeReining || LevyUtil.IsClient) return false;
			if (!LeisureOn || FreeReinLegacy || OfficerMode || HoldPosition || Record == null) { Leisure_Stop(); return false; }
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { return Leisure_ThinkCore(now, nearestHostileTiles); }
			finally { LeisureTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; LeisureCalls++; }
		}

		private bool Leisure_ThinkCore(int now, float nearestHostileTiles)
		{
			if (!Leisure_Calm(now, nearestHostileTiles, out bool soft))
			{
				if (!soft) _lzCalmSince = now;
				if (_lzMode != 0 && !soft) Leisure_StopAt(now, true);
				return false;
			}
			if (_lzMode == 2) return Leisure_Do(now);
			if (_lzMode == 1) return Leisure_Walk(now);
			if (_lzNextAt == 0) { _lzNextAt = now + Main.rand.Next(300, LevyConstants.LeisureGapMinTicks); return false; } // the army does not all start at once
			if (now - _lzCalmSince < LevyConstants.LeisureCalmTicks || now < _lzNextAt) return false;
			return Leisure_Pick(now);
		}

		private bool Leisure_Calm(int now, float nearestHostileTiles, out bool soft)
		{
			soft = false;
			if (MutinySystem.IsMutinous(Record) || Shaken || State == SoldierState.Shaken || State == SoldierState.Routed) return false;
			if (TargetIndex >= 0 || nearestHostileTiles < LevyConstants.SitWakeEnemyTiles) return false;
			if (now - _sitBusyTick < LevyConstants.SitQuietTicks || TicksSinceDamaged < LevyConstants.SitQuietTicks || BeingTalkedTo) return false;
			if (_lzMode != 2 && _sitMode == 0 && (NPC.velocity.Y != 0f || NPC.wet || ClimbActive || _hkPhase != HookPhase.Idle)) { soft = true; return false; } // only mid-jump: not a reason to start over
			return true;
		}

		// ---------------- picking ----------------
		private bool Leisure_Pick(int now)
		{
			Vector2 anchor = FreeReinBase();
			var props = LzScan(anchor, now);
			LzCount(out int doing, out int total);
			if (total > 1 && doing >= Math.Ceiling(total * LevyConstants.LeisureMaxShare)) { _lzNextAt = now + 300; return false; }
			float[] w = LzWeights(Main.dayTime, Main.raining, Math.Abs(Main.WindForVisuals), props.Fires.Count > 0, LzSeatsNear(props, anchor) > LzSitters(), props.Kegs.Count > 0, props.Beds.Count > 0);
			for (int attempt = 0; attempt < 3; attempt++)
			{
				float sum = 0f; for (int i = 0; i < w.Length; i++) sum += w[i];
				if (sum <= 0f) break;
				float r = Main.rand.NextFloat() * sum; int kind = 0;
				for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0f && w[i] > 0f) { kind = i; break; } }
				if (kind == 0) break;
				w[kind] = 0f;
				if (Leisure_Begin(now, kind, props, anchor)) return true;
			}
			LeisureNoSpot++;
			_lzNextAt = now + Main.rand.Next(LevyConstants.LeisureGapMinTicks / 2, LevyConstants.LeisureGapMinTicks);
			return false;
		}

		/// <summary>Pure rule of the pick: how likely each pastime is, from the sky and what stands at the base.</summary>
		internal static float[] LzWeights(bool day, bool rain, float wind, bool fire, bool seat, bool keg, bool bed)
		{
			float[] w = new float[9];
			w[LzKite] = day && !rain ? (wind > 0.15f ? 5f : 3f) : 0f;
			w[LzRoast] = fire ? (day ? 2f : 3f) : 0f;
			w[LzSit] = seat && SitEnabled ? (fire && !day ? 5f : 2f) : 0f;
			w[LzKeg] = keg ? 2f : 0f;
			w[LzSleep] = !day && bed ? 8f : 0f;
			w[LzStars] = !day && !rain ? 2f : 0f;
			w[LzCheer] = 1f;
			return w;
		}

		private bool Leisure_Begin(int now, int kind, LzProps props, Vector2 anchor)
		{
			Vector2 spot = default; int dir = NPC.direction == 0 ? 1 : NPC.direction; int dur;
			switch (kind)
			{
				case LzKite:
					if (!LzFindSkySpot(anchor, out spot)) return false;
					dir = Main.WindForVisuals != 0f && Main.rand.Next(3) > 0 ? Math.Sign(Main.WindForVisuals) : (Main.rand.NextBool() ? 1 : -1);
					dur = Main.rand.Next(LevyConstants.LeisureKiteMinTicks, LevyConstants.LeisureKiteMaxTicks); _lzKiteType = LzKiteTypes[Main.rand.Next(LzKiteTypes.Length)];
					break;
				case LzStars:
					if (!LzFindSkySpot(anchor, out spot)) return false;
					dir = Main.rand.NextBool() ? 1 : -1; dur = Main.rand.Next(LevyConstants.LeisureStargazeMinTicks, LevyConstants.LeisureStargazeMaxTicks);
					break;
				case LzRoast:
					if (!LzFindBeside(props.Fires, out spot, out dir, out _lzProp)) return false;
					dur = Main.rand.Next(LevyConstants.LeisureRoastMinTicks, LevyConstants.LeisureRoastMaxTicks);
					break;
				case LzKeg:
					if (!LzFindBeside(props.Kegs, out spot, out dir, out _lzProp)) return false;
					dur = Main.rand.Next(LevyConstants.LeisureKegMinTicks, LevyConstants.LeisureKegMaxTicks);
					break;
				case LzSleep:
					if (!LzFindBed(props, out spot, out dir)) return false;
					dur = Main.rand.Next(60 * 70, 60 * 150);
					break;
				case LzSit:
					dur = Main.rand.Next(LevyConstants.LeisureFireSitMinTicks, LevyConstants.LeisureFireSitMaxTicks);
					_lzKind = LzSit; _lzMode = 1; _lzWalkSince = now; _lzSitUntil = now + dur; _sitCalmSince = now - 100000; _sitScanAt = 0; _sitCooldownUntil = 0;
					_lzSitAnchor = props.Fires.Count > 0 ? NearestTileTo(props.Fires, NPC.Bottom) : anchor;
					LeisureStarts[kind]++;
					return true;
				case LzCheer:
					spot = NPC.Bottom; dur = Main.rand.Next(LevyConstants.LeisureCheerMinTicks, LevyConstants.LeisureCheerMaxTicks);
					break;
				default: return false;
			}
			_lzKind = kind; _lzSpot = spot; _lzDir = dir; _lzEndAt = dur; // stored as a length until the levy is at its place
			_lzWalkSince = now; _lzChatAt = 0; _lzEmoteAt = 0;
			LeisureStarts[kind]++;
			if (kind == LzCheer) { _lzMode = 2; _lzEndAt = now + dur; NPC.direction = NPC.spriteDirection = dir; ClearGoal(); NPC.netUpdate = true; return true; }
			_lzMode = 1;
			SetGoal(_lzSpot);
			return true;
		}

		private Vector2 _lzSitAnchor;
		private static Vector2 NearestTileTo(List<Point> tiles, Vector2 from)
		{
			Vector2 best = from; float bd = float.MaxValue;
			foreach (var p in tiles) { var c = new Vector2(p.X * 16f + 8f, p.Y * 16f + 8f); float d = Vector2.DistanceSquared(c, from); if (d < bd) { bd = d; best = c; } }
			return best;
		}

		/// <summary>The sitting machinery asks: does a pastime want this levy on a seat now, and around where.</summary>
		private bool Leisure_SitWanted(int now, ref Vector2 anchor, ref int radius)
		{
			if (_lzMode != 1 || _lzKind != LzSit || now >= _lzSitUntil) return false;
			anchor = _lzSitAnchor; radius = 22;
			return true;
		}

		private bool Leisure_Walk(int now)
		{
			if (_lzKind == LzSit)
			{
				// Sit_Think runs first and spends the think while the levy walks to a seat or sits; getting here means it is not (yet) doing so
				if (now >= _lzSitUntil || (_sitMode == 0 && now - _lzWalkSince > 60 * 12)) { if (_sitMode == 0 && now < _lzSitUntil) LeisureAbandons++; Leisure_StopAt(now, true); }
				return false;
			}
			if (now - _lzWalkSince > LevyConstants.LeisureWalkTimeoutTicks) { LeisureAbandons++; Leisure_StopAt(now, true); return false; }
			bool near = Math.Abs(NPC.Bottom.X - _lzSpot.X) <= 6f && Math.Abs(NPC.Bottom.Y - _lzSpot.Y) <= 12f && NPC.velocity.Y == 0f;
			if (near)
			{
				ClearGoal();
				_lzMode = 2; _lzEndAt = now + _lzEndAt;
				NPC.direction = NPC.spriteDirection = _lzDir; NPC.velocity = Vector2.Zero;
				if (_lzKind == LzSleep) NPC.Bottom = _lzSpot;
				NPC.netUpdate = true;
				return true;
			}
			if (!HasMoveGoal || MoveGoal != _lzSpot) SetGoal(_lzSpot);
			return true;
		}

		private bool Leisure_Do(int now)
		{
			if (HasMoveGoal && _lzKind != LzSleep) ClearGoal();
			if (NPC.direction != _lzDir && _lzKind != LzCheer) { NPC.direction = NPC.spriteDirection = _lzDir; }
			bool over = now >= _lzEndAt;
			switch (_lzKind)
			{
				case LzKite: if (!Main.dayTime || Main.raining) over = true; break;
				case LzStars: if (Main.dayTime || Main.raining) over = true; break;
				case LzSleep: if (Main.dayTime) over = true; break;
				case LzRoast: if (!LzLit(_lzProp)) over = true; break;
			}
			if (over) { Leisure_StopAt(now, true); return false; }
			if (now >= _lzEmoteAt)
			{
				_lzEmoteAt = now + Main.rand.Next(60 * 6, 60 * 14);
				int emote = _lzKind == LzSleep ? EmoteID.EmoteSleep : _lzKind == LzKeg ? EmoteID.ItemAle : _lzKind == LzCheer ? EmoteID.EmoteNote : _lzKind == LzKite ? EmoteID.EmoteHappiness : _lzKind == LzRoast ? EmoteID.EmoteHappiness : EmoteID.EmoteNote;
				if (AnyPlayerNear(LevyConstants.SitChatPlayerTiles)) DialogueSystem.Emote(this, emote);
			}
			if (_lzKind != LzSleep && now >= _lzChatAt) Leisure_Chat(now);
			return true;
		}

		/// <summary>A levy at a pastime says a line to a comrade nearby (or at the same pastime), with an emote over the listener.</summary>
		private void Leisure_Chat(int now)
		{
			if (_lzChatAt == 0) { _lzChatAt = now + Main.rand.Next(LevyConstants.SitChatMinTicks / 2, LevyConstants.SitChatMaxTicks); return; }
			_lzChatAt = now + Main.rand.Next(LevyConstants.SitChatMinTicks, LevyConstants.SitChatMaxTicks);
			if (!AnyPlayerNear(LevyConstants.SitChatPlayerTiles)) return;
			SoldierNPC partner = null; float best = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.whoAmI == NPC.whoAmI || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsRegular || s._lzMode == 0 && s._sitMode == 0) continue;
				float d = Vector2.Distance(n.Center, NPC.Center) / 16f;
				if (d < best && d <= 9f) { best = d; partner = s; }
			}
			if (partner == null) return;
			DialogueTrigger trig = _lzKind == LzRoast || _lzKind == LzKeg ? DialogueTrigger.FIRESIDE : (Main.dayTime ? DialogueTrigger.PASTIME : DialogueTrigger.PASTIME_NIGHT);
			var ctx = new DialogueContext { Comrade = partner.SoldierName, Player = Record.CommanderName };
			string line = DialogueSystem.PickLine(trig, Record, ctx);
			if (!string.IsNullOrEmpty(line)) { DialogueSystem.FloatingText(this, line, trig); ChatterLines++; LeisureChats++; }
			DialogueSystem.Emote(partner, SitEmotes[Main.rand.Next(SitEmotes.Length)]);
			partner._lzChatAt = Math.Max(partner._lzChatAt, now + LevyConstants.SitChatMinTicks / 2);
		}

		// ---------------- places ----------------
		private sealed class LzProps
		{
			public int Stamp; public readonly List<Point> Fires = new(), Kegs = new(); public readonly List<(Vector2 pos, int dir)> Beds = new(); public int Seats; public readonly List<Point> SeatTiles = new();
		}
		private static readonly Dictionary<long, LzProps> LzCache = new();

		/// <summary>Campfires (lit), kegs, beds and seats within <see cref="LevyConstants.LeisurePropRadiusTiles"/> of the base, cached and shared by every levy of that base.</summary>
		private static LzProps LzScan(Vector2 anchor, int now)
		{
			int ax = (int)(anchor.X / 16f), ay = (int)(anchor.Y / 16f);
			long key = ((long)ax << 20) ^ (long)ay;
			if (LzCache.TryGetValue(key, out var c) && now - c.Stamp < LevyConstants.LeisurePropScanTicks && now >= c.Stamp) return c;
			if (LzCache.Count > 24) LzCache.Clear();
			c = new LzProps { Stamp = now };
			int R = LevyConstants.LeisurePropRadiusTiles;
			int x0 = Math.Max(10, ax - R), x1 = Math.Min(Main.maxTilesX - 10, ax + R), y0 = Math.Max(10, ay - 24), y1 = Math.Min(Main.maxTilesY - 10, ay + 12);
			var seenBeds = new HashSet<long>();
			for (int x = x0; x <= x1; x++)
				for (int y = y0; y <= y1; y++)
				{
					Tile t = Main.tile[x, y];
					if (!t.HasTile) continue;
					ushort ty = t.TileType;
					if (TileID.Sets.Campfire[ty])
					{
						if (t.TileFrameX % 54 == 18 && t.TileFrameY % 36 == 18 && t.TileFrameY < 36) c.Fires.Add(new Point(x, y));
					}
					else if (ty == TileID.Kegs)
					{
						if (t.TileFrameX % 36 == 0 && t.TileFrameY % 36 == 18) c.Kegs.Add(new Point(x + 1, y));
					}
					else if (TileID.Sets.CanBeSleptIn[ty])
					{
						if (Terraria.GameContent.PlayerSleepingHelper.GetSleepingTargetInfo(x, y, out int dir, out Vector2 pos, out _))
						{
							long bk = ((long)(int)pos.X << 24) ^ (long)(int)pos.Y;
							if (seenBeds.Add(bk)) c.Beds.Add((pos, dir));
						}
					}
					else if (c.Seats < 60 && TileID.Sets.CanBeSatOnForPlayers[ty]) { c.Seats++; c.SeatTiles.Add(new Point(x, y)); }
				}
			LzCache[key] = c;
			return c;
		}

		private static bool LzLit(Point fire) => fire != default && Main.tile[fire.X, fire.Y].HasTile && TileID.Sets.Campfire[Main.tile[fire.X, fire.Y].TileType] && Main.tile[fire.X, fire.Y].TileFrameY < 36;

		/// <summary>Some other levy is at / walking to this place.</summary>
		private bool LzTaken(Vector2 pos, float within)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.whoAmI == NPC.whoAmI || n.ModNPC is not SoldierNPC s || s._lzMode == 0 || s._lzKind == LzSit || s._lzKind == LzCheer) continue;
				if (Vector2.DistanceSquared(s._lzSpot, pos) < within * within) return true;
			}
			return false;
		}

		/// <summary>Seats within the sitting radius of where a sit pastime would gather (the nearest lit fire, else the base).</summary>
		private int LzSeatsNear(LzProps props, Vector2 anchor)
		{
			Vector2 c = props.Fires.Count > 0 ? NearestTileTo(props.Fires, NPC.Bottom) : anchor; int n = 0;
			foreach (var t in props.SeatTiles) if (Math.Abs(t.X * 16f - c.X) <= 22 * 16f && Math.Abs(t.Y * 16f - c.Y) <= 6 * 16f) n++;
			return n;
		}

		/// <summary>Levies of this commander now sitting or walking to a seat.</summary>
		private int LzSitters()
		{
			int n = 0;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC o = Main.npc[i]; if (o.active && o.ModNPC is SoldierNPC s && s.Record != null && (s._sitMode != 0 || (s._lzMode == 1 && s._lzKind == LzSit))) n++; }
			return n;
		}

		private void LzCount(out int doing, out int total)
		{
			doing = total = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || !s.FreeReining || s.Record.CommanderName != Record.CommanderName) continue;
				total++; if (s._lzMode != 0) doing++;
			}
		}

		private bool LzReach(Vector2 spot)
		{
			if (!SearchMayStart()) return false;
			if (!Pathfinder.FindPath(NPC.Bottom, spot, Abilities, _probePath)) return false;
			return FreeReinRouteOk(spot);
		}

		/// <summary>A standing spot in open sky near the base: a flat stretch with nothing solid for <see cref="LevyConstants.LeisureKiteSkyRows"/> rows over it.</summary>
		private bool LzFindSkySpot(Vector2 anchor, out Vector2 spot)
		{
			spot = default;
			int ax = (int)(anchor.X / 16f);
			int span = Math.Min(60, FreeReinLeash - 10);
			for (int tries = 0; tries < 10; tries++)
			{
				int x = ax + Main.rand.Next(-span, span + 1);
				if (!FreeReinPickSpot(x * 16f + 8f, false, out Point t)) continue;
				bool open = true;
				for (int k = -LevyConstants.LeisureKiteRoomTiles; k <= LevyConstants.LeisureKiteRoomTiles && open; k++)
				{
					if (!TileNav.IsStandable(t.X + k, t.Y) && Math.Abs(k) <= 1) open = false;
					for (int r = 1; r <= LevyConstants.LeisureKiteSkyRows && open; r++) if (TileNav.IsSolid(t.X + k, t.Y - r)) open = false;
				}
				if (!open) continue;
				Vector2 cand = TileNav.BottomCentre(t.X, t.Y);
				if (LzTaken(cand, 40f) || !LzReach(cand)) continue;
				spot = cand; return true;
			}
			return false;
		}

		/// <summary>A standing spot two to four tiles to one side of a campfire or keg, facing it.</summary>
		private bool LzFindBeside(List<Point> things, out Vector2 spot, out int dir, out Point prop)
		{
			spot = default; dir = 1; prop = default;
			if (things.Count == 0) return false;
			var order = new List<Point>(things);
			order.Sort((a, b) => Vector2.DistanceSquared(new Vector2(a.X * 16f, a.Y * 16f), NPC.Bottom).CompareTo(Vector2.DistanceSquared(new Vector2(b.X * 16f, b.Y * 16f), NPC.Bottom)));
			for (int i = 0; i < Math.Min(3, order.Count); i++)
			{
				Point f = order[i];
				if (!FreeReinInBox(new Vector2(f.X * 16f, f.Y * 16f))) continue;
				int[] offs = { -4, 4, -3, 3, -2, 2 };
				int start = Main.rand.Next(offs.Length);
				for (int j = 0; j < offs.Length; j++)
				{
					int off = offs[(start + j) % offs.Length], x = f.X + off;
					if (!TileNav.InBounds(x, f.Y) || !TileNav.IsStandable(x, f.Y) || NearDrop(x, f.Y, LevyConstants.FreeReinRimTiles)) continue;
					Vector2 cand = TileNav.BottomCentre(x, f.Y);
					if (LzTaken(cand, 20f) || !LzReach(cand)) continue;
					spot = cand; dir = off < 0 ? 1 : -1; prop = f; return true;
				}
			}
			return false;
		}

		private bool LzFindBed(LzProps props, out Vector2 spot, out int dir)
		{
			spot = default; dir = 1;
			var order = new List<(Vector2 pos, int dir)>(props.Beds);
			order.Sort((a, b) => Vector2.DistanceSquared(a.pos, NPC.Bottom).CompareTo(Vector2.DistanceSquared(b.pos, NPC.Bottom)));
			foreach (var b in order)
			{
				if (!FreeReinInBox(b.pos)) continue;
				if (LzTaken(b.pos, 24f) || !LzReach(b.pos)) continue;
				spot = b.pos; dir = b.dir; return true;
			}
			return false;
		}

		/// <summary>A lit campfire within <paramref name="tiles"/> of a point (the fireside lines).</summary>
		internal static bool NearLitCampfire(Vector2 pos, int tiles)
		{
			int cx = (int)(pos.X / 16f), cy = (int)(pos.Y / 16f);
			for (int x = cx - tiles; x <= cx + tiles; x++)
				for (int y = cy - 3; y <= cy + 2; y++)
				{
					if (!WorldGen.InWorld(x, y, 5)) continue;
					Tile t = Main.tile[x, y];
					if (t.HasTile && TileID.Sets.Campfire[t.TileType] && t.TileFrameY < 36) return true;
				}
			return false;
		}

		// ---------------- drawing (client and single player) ----------------
		private const int LzRoastCycleTicks = 60 * 24, LzRoastCookedFrom = 60 * 18;

		/// <summary>Before the pose is set: the prop goes in the dummy's hand (the real weapon is put back after the draw).</summary>
		private void Leisure_PrePose()
		{
			int k = LeisureKindNow;
			int want = 0;
			if (k == LzRoast) want = ((Main.GameUpdateCount + (uint)(NPC.whoAmI * 211)) % LzRoastCycleTicks) >= LzRoastCookedFrom ? ItemID.CookedMarshmallow : ItemID.MarshmallowonaStick;
			else if (k == LzKeg) want = ItemID.Ale;
			if (want == 0) { if (_lzSwapped) Leisure_RestoreHeld(); return; }
			if (_lzItem == null || _lzItem.type != want) { _lzItem = new Item(); _lzItem.SetDefaults(want); }
			Dummy.inventory[0] = _lzItem; _lzSwapped = true;
		}

		/// <summary>After the draw: the weapon is back in the dummy's hand, whatever happened.</summary>
		private void Leisure_RestoreHeld()
		{
			if (!_lzSwapped) return;
			_lzSwapped = false;
			var g = Record?.Gear; if (g == null) return;
			Dummy.inventory[0] = UsingSidearm && g.HasSidearm ? g.Sidearm : (g.HasWeapon ? g.Weapon : (_blankSlot ??= new Item()));
		}

		/// <summary>After the stock pose, before the frames: a drink raised to the mouth, a bed's sleeping pose.</summary>
		private void Leisure_PostPose()
		{
			var p = Dummy; int k = LeisureKindNow;
			if (k == LzSleep)
			{
				Point at = (NPC.Bottom + new Vector2(0f, -2f)).ToTileCoordinates();
				if (Terraria.GameContent.PlayerSleepingHelper.GetSleepingTargetInfo(at.X, at.Y, out int dir, out _, out Vector2 vis))
				{
					p.direction = dir; p.sleeping.isSleeping = true; p.sleeping.visualOffsetOfBedBase = vis; p.sleeping.sleepingIndex = 0; p.fullRotation = MathF.PI / 2f * -dir; _lzSleepPosed = true;
					p.controlLeft = p.controlRight = p.controlJump = false;
				}
			}
			else if (_lzSleepPosed)
			{
				p.sleeping.isSleeping = false; p.sleeping.visualOffsetOfBedBase = default; p.sleeping.sleepingIndex = -1; p.fullRotation = 0f; _lzSleepPosed = false;
			}
			if ((k == LzKeg || k == LzRoast) && _lzSwapped && _lzItem.holdStyle == 0 && _lzItem.useStyle != 0)
			{
				// a sip, a bite: the stock use pose of the item (drink / eat) over its use time, every few seconds
				int max = Math.Max(24, _lzItem.useAnimation);
				int cycle = (int)((Main.GameUpdateCount + (uint)(NPC.whoAmI * 41)) % (uint)(max + (k == LzKeg ? 150 : 40)));
				if (cycle < max)
				{
					p.itemAnimation = max - cycle; p.itemAnimationMax = max;
					Rectangle frame = Item.GetDrawHitbox(_lzItem.type, p);
					p.ItemCheck_ApplyUseStyle(p.HeightOffsetHitboxCenter, _lzItem, frame);
				}
			}
		}

		/// <summary>Inside the body draw: raised arms and a hop. Draw only.</summary>
		private void LeisurePose(ref Vector2 pos, ref float rot, ref Vector2 origin)
		{
			int k = LeisureKindNow;
			if (k == LzNone || k == LzSit) return;
			var p = Dummy; int dir = p.direction == 0 ? 1 : p.direction; float t = Main.GameUpdateCount + NPC.whoAmI * 17;
			if (k == LzKite) p.SetCompositeArmFront(enabled: true, Player.CompositeArmStretchAmount.Full, -dir * (2.3f + 0.08f * MathF.Sin(t * 0.045f)));
			else if (k == LzStars) p.SetCompositeArmFront(enabled: true, Player.CompositeArmStretchAmount.Full, -dir * (2.75f + 0.05f * MathF.Sin(t * 0.03f)));
			else if (k == LzCheer)
			{
				p.SetCompositeArmFront(enabled: true, Player.CompositeArmStretchAmount.Full, -dir * (2.7f + 0.2f * MathF.Sin(t * 0.3f)));
				p.SetCompositeArmBack(enabled: true, Player.CompositeArmStretchAmount.Full, dir * (2.7f + 0.2f * MathF.Sin(t * 0.3f + 1.4f)));
				pos.Y -= MathF.Abs(MathF.Sin(t * 0.15f)) * 5f;
			}
		}

		/// <summary>After the body: the kite and its string (vanilla kite sprite).</summary>
		private void Leisure_DrawProps(SpriteBatch sb, Vector2 screenPos)
		{
			if (LeisureKindNow != LzKite) { _lzKiteInit = false; return; }
			var p = Dummy; int dir = p.direction == 0 ? 1 : p.direction;
			Vector2 hand = NPC.Center + new Vector2(dir * 7f, -13f);
			if (_lzKiteType == 0) _lzKiteType = LzKiteTypes[NPC.whoAmI % LzKiteTypes.Length];
			if (!_lzKiteInit) { _lzKitePos = hand + new Vector2(dir * 40f, -90f); _lzKiteVel = Vector2.Zero; _lzKiteInit = true; _lzKiteTick = Main.GameUpdateCount - 1; }
			uint now = Main.GameUpdateCount;
			if (now != _lzKiteTick)
			{
				int steps = (int)Math.Min(4u, now - _lzKiteTick); _lzKiteTick = now;
				float wind = Main.WindForVisuals, t = now * 0.02f + NPC.whoAmI;
				for (int i = 0; i < steps; i++)
				{
					Vector2 want = hand + new Vector2(dir * (46f + 18f * MathF.Sin(t * 1.3f)) + wind * 55f, -115f + 16f * MathF.Sin(t * 0.9f));
					_lzKiteVel = Vector2.Lerp(_lzKiteVel, (want - _lzKitePos) * 0.06f, 0.12f);
					_lzKitePos += _lzKiteVel;
					Vector2 off = _lzKitePos - hand; float len = off.Length();
					if (len > 150f) _lzKitePos = hand + off / len * 150f;
				}
			}
			Main.instance.LoadProjectile(_lzKiteType);
			Texture2D tex = TextureAssets.Projectile[_lzKiteType].Value;
			// the string: a sagging curve from the hand to the kite
			Vector2 a = hand, b = _lzKitePos, mid = (a + b) * 0.5f + new Vector2(0f, 14f);
			Vector2 prev = a;
			for (int i = 1; i <= 14; i++)
			{
				float u = i / 14f; Vector2 q = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * mid + u * u * b;
				Utils.DrawLine(sb, prev - screenPos, q - screenPos, new Color(205, 205, 205), new Color(205, 205, 205), 1f);
				prev = q;
			}
			int frames = Math.Max(1, Main.projFrames[_lzKiteType]);
			Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height / frames);
			float rot = (b - a).ToRotation() + MathHelper.PiOver4 + _lzKiteVel.X * 0.05f;
			Color col = Lighting.GetColor((int)(b.X / 16f), (int)(b.Y / 16f));
			sb.Draw(tex, b - screenPos, src, col, rot, src.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
		}
	}
}
