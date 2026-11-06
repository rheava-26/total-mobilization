using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Items.Observe;
using Levy.Content.NPCs;

namespace Levy.Common.Observe
{
	/// <summary>One frame of the controlling player's input.</summary>
	public struct MindInputState
	{
		public bool Left, Right, Jump, Down, Use, Alt;
		public Vector2 Aim;

		public byte Pack() => (byte)((Left ? 1 : 0) | (Right ? 2 : 0) | (Jump ? 4 : 0) | (Down ? 8 : 0) | (Use ? 16 : 0) | (Alt ? 32 : 0));
		public static MindInputState Unpack(byte b, Vector2 aim) => new MindInputState { Left = (b & 1) != 0, Right = (b & 2) != 0, Jump = (b & 4) != 0, Down = (b & 8) != 0, Use = (b & 16) != 0, Alt = (b & 32) != 0, Aim = aim };
		public bool SameAs(in MindInputState o) => Pack() == o.Pack() && Vector2.DistanceSquared(Aim, o.Aim) < 64f;
	}

	/// <summary>The server's record of who is driving which soldier.</summary>
	public class MindLink
	{
		public int Player, SoldierId;
		/// <summary>Started because the player died: ends on respawn. Otherwise it started from the Mind Link item and ends on release.</summary>
		public bool ViaDeath;
		public MindInputState In;
		public uint InTick;
	}

	/// <summary>
	/// Mind control: a player's movement and attack input (and cursor) drive one of their levies, while they are dead (until they respawn) or, with a Mind Link, any time.
	/// Server / single player own the links and apply the input in the soldier's tick (<c>SoldierNPC.MindControl_Drive/Act</c>, through the soldier's own movement goal and weapon code).
	/// A client samples its input each frame and sends it (MindInput, only when it changes or every 10 ticks); its camera follows the levy through the Observe system.
	/// </summary>
	public static class MindControl
	{
		private static readonly MindLink[] ByPlayer = new MindLink[256];
		private static readonly Dictionary<int, MindLink> BySoldier = new();
		private static bool _warned;

		// ---- local (client / single player) ----
		public static int LocalSoldierId = -1; public static bool LocalViaDeath;
		private static MindInputState _lastSent; private static uint _lastSentTick;

		public static MindLink LinkOf(int soldierId) => BySoldier.Count > 0 && BySoldier.TryGetValue(soldierId, out var l) ? l : null;
		public static MindLink LinkOfPlayer(int player) => player >= 0 && player < ByPlayer.Length ? ByPlayer[player] : null;
		public static int LinkCount { get { int n = 0; foreach (var l in ByPlayer) if (l != null) n++; return n; } }
		public static void Warn(Exception e) { if (_warned) return; _warned = true; Levy.Instance.Logger.Warn("Levy: mind control action failed (logged once): " + e); }

		public static void Reset() { Array.Clear(ByPlayer); BySoldier.Clear(); LocalSoldierId = -1; }

		// =====================================================================================================
		// server / single player
		// =====================================================================================================

		/// <summary>Validates and starts control of a soldier. The caller must own the soldier; without death it needs a Mind Link in the inventory.</summary>
		public static bool Begin(Player p, int soldierId, bool viaDeath)
		{
			if (LevyUtil.IsClient || p == null || !p.active) return false;
			var rec = RosterSystem.Instance?.FindById(soldierId);
			if (rec == null || rec.CommanderName != p.name || rec.Reserve || rec.Kind != SoldierKind.Regular || MutinySystem.IsMutinous(rec)) return false;
			var body = BodyOf(rec);
			if (body == null || !body.IsAlive) return false;
			if (!viaDeath && !p.dead && !p.HasItem(ModContent.ItemType<MindLinkItem>())) return false;
			if (!viaDeath && p.dead) viaDeath = true; // an item used while dead still ends on respawn
			End(p.whoAmI);
			var old = LinkOf(soldierId); if (old != null) End(old.Player); // one driver per soldier
			var l = new MindLink { Player = p.whoAmI, SoldierId = soldierId, ViaDeath = viaDeath, InTick = Main.GameUpdateCount };
			ByPlayer[p.whoAmI] = l; BySoldier[soldierId] = l;
			return true;
		}

		public static void End(int player)
		{
			var l = LinkOfPlayer(player);
			if (l == null) return;
			ByPlayer[player] = null;
			if (BySoldier.TryGetValue(l.SoldierId, out var cur) && ReferenceEquals(cur, l)) BySoldier.Remove(l.SoldierId);
		}

		public static void SetInput(int player, MindInputState s)
		{
			var l = LinkOfPlayer(player);
			if (l == null) return;
			l.In = s; l.InTick = Main.GameUpdateCount;
		}

		/// <summary>Per-tick upkeep (server / single player): ends links whose player respawned or left, hands a dead player the next levy when theirs falls.</summary>
		public static void ServerTick()
		{
			if (LevyUtil.IsClient) return;
			for (int i = 0; i < ByPlayer.Length; i++)
			{
				var l = ByPlayer[i]; if (l == null) continue;
				var p = i < Main.maxPlayers ? Main.player[i] : null;
				if (p == null || !p.active) { End(i); continue; }
				if (l.ViaDeath && !p.dead) { End(i); continue; }
				var rec = RosterSystem.Instance?.FindById(l.SoldierId);
				var body = rec == null ? null : BodyOf(rec);
				if (body == null || !body.IsAlive)
				{
					int next = l.ViaDeath ? PickNext(p, l.SoldierId, -1) : -1;
					End(i);
					if (next >= 0) Begin(p, next, true);
					continue;
				}
				if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount - l.InTick > 30u) l.In = default; // input went quiet: let go of the keys
			}
		}

		public const float GoalStepPx = 96f, LiftPx = 200f;

		/// <summary>
		/// Input to movement: left / right set a walk goal 96 px to that side (both or neither: no goal, the body slows to a stop); jump on the ground is a jump, in the air with wings it lifts the goal 200 px up so the movement code flies;
		/// down arms a drop through platforms. Pure, so it can be checked at boot.
		/// </summary>
		public static void Map(in MindInputState s, Vector2 center, float bottomY, bool grounded, bool canFly, out bool hasGoal, out Vector2 goal, out bool jump, out bool drop)
		{
			int dir = (s.Right ? 1 : 0) - (s.Left ? 1 : 0);
			hasGoal = dir != 0; goal = hasGoal ? new Vector2(center.X + dir * GoalStepPx, bottomY) : default;
			jump = s.Jump && grounded;
			if (s.Jump && !grounded && canFly) { hasGoal = true; goal = new Vector2(center.X + dir * GoalStepPx, bottomY - LiftPx); }
			drop = s.Down;
		}

		/// <summary>The hostile NPC nearest <paramref name="pos"/> within <paramref name="tiles"/>, or -1.</summary>
		public static int HostileNear(Vector2 pos, float tiles)
		{
			int best = -1; float bd = tiles * 16f; bd *= bd;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || !LevyUtil.IsHostileTarget(n)) continue;
				float d = Vector2.DistanceSquared(n.Center, pos);
				if (d < bd) { bd = d; best = i; }
			}
			return best;
		}

		public static SoldierNPC BodyOf(SoldierRecord rec) => rec != null && rec.HasBody ? Main.npc[rec.BodyIndex].ModNPC as SoldierNPC : null;

		/// <summary>The next living Regular of the player's after <paramref name="afterId"/> (by id, wrapping), nearest to <paramref name="near"/> when <paramref name="afterId"/> is -1 and no cursor point is given.</summary>
		public static int PickNext(Player p, int afterId, int notId, Vector2? near = null)
		{
			var roster = RosterSystem.Instance; if (roster == null || p == null) return -1;
			var ids = new List<int>();
			foreach (var r in roster.RegularsOf(p))
			{
				if (r.Reserve || r.Dead || r.Id == notId) continue;
				if (!(r.HasBody && Main.npc[r.BodyIndex].active) && Main.netMode != NetmodeID.MultiplayerClient) continue;
				ids.Add(r.Id);
			}
			if (ids.Count == 0) return -1;
			ids.Sort();
			if (afterId >= 0) { foreach (int id in ids) if (id > afterId) return id; return ids[0]; }
			Vector2 from = near ?? p.Center; int best = ids[0]; float bd = float.MaxValue;
			foreach (int id in ids)
			{
				var body = BodyOf(roster.FindById(id)); if (body == null) continue;
				float d = Vector2.DistanceSquared(body.NPC.Center, from);
				if (d < bd) { bd = d; best = id; }
			}
			return best;
		}

		// =====================================================================================================
		// client side
		// =====================================================================================================

		/// <summary>Takes control of a levy (the local player). Single player acts at once; a client asks the server and follows the levy through the Observe stream.</summary>
		public static void RequestBegin(int soldierId, bool viaDeath)
		{
			if (soldierId < 0) return;
			LocalSoldierId = soldierId; LocalViaDeath = viaDeath;
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				if (!Begin(Main.LocalPlayer, soldierId, viaDeath)) { LocalSoldierId = -1; Main.NewText(LevyUtil.T("Observe.MindRefused"), 255, 160, 120); return; }
			}
			else { var pk = LevyPackets.New(LevyPacket.MindControlSet); pk.Write(true); pk.Write(soldierId); pk.Write(viaDeath); pk.Send(); }
			ObserveSystem.OnMindBegin(soldierId);
		}

		public static void RequestEnd()
		{
			if (LocalSoldierId < 0 && LinkOfPlayer(Main.myPlayer) == null) return;
			LocalSoldierId = -1;
			if (Main.netMode == NetmodeID.SinglePlayer) End(Main.myPlayer);
			else { var pk = LevyPackets.New(LevyPacket.MindControlSet); pk.Write(false); pk.Write(0); pk.Write(false); pk.Send(); }
			ObserveSystem.OnMindEnd();
		}

		/// <summary>Mind Link item: take the levy nearest the cursor, or (already linked) the next one.</summary>
		public static void RequestFromItem(bool next)
		{
			var me = Main.LocalPlayer;
			int id = next ? PickNext(me, LocalSoldierId, -1) : PickNext(me, -1, -1, Main.MouseWorld);
			if (id < 0) { Main.NewText(LevyUtil.T("Observe.MindNone"), 255, 200, 120); return; }
			RequestBegin(id, me.dead);
		}

		/// <summary>The local player's controls and cursor, read from the raw triggers so it also works while dead.</summary>
		public static MindInputState Sample()
		{
			var t = Terraria.GameInput.PlayerInput.Triggers.Current;
			return new MindInputState { Left = t.Left, Right = t.Right, Jump = t.Jump, Down = t.Down, Use = t.MouseLeft, Alt = t.MouseRight, Aim = Main.MouseWorld };
		}

		/// <summary>Called every frame while the local player is controlling: hands the input to the server (or the local link).</summary>
		public static void PushLocalInput(MindInputState s)
		{
			if (Main.netMode == NetmodeID.SinglePlayer) { SetInput(Main.myPlayer, s); return; }
			if (s.SameAs(_lastSent) && Main.GameUpdateCount - _lastSentTick < 10u) return;
			_lastSent = s; _lastSentTick = Main.GameUpdateCount;
			var pk = LevyPackets.New(LevyPacket.MindInput); pk.Write(s.Pack()); pk.Write(s.Aim.X); pk.Write(s.Aim.Y); pk.Send();
		}

		public static void Register()
		{
			LevyPackets.Register(LevyPacket.MindControlSet, OnSet);
			LevyPackets.Register(LevyPacket.MindInput, OnInput);
		}

		private static void OnSet(BinaryReader r, int who)
		{
			bool on = r.ReadBoolean(); int id = r.ReadInt32(); bool viaDeath = r.ReadBoolean();
			if (who < 0 || who >= Main.maxPlayers) return;
			var p = Main.player[who]; if (!p.active) return;
			if (!on) { End(who); return; }
			Begin(p, id, viaDeath);
		}

		private static void OnInput(BinaryReader r, int who)
		{
			byte b = r.ReadByte(); float x = r.ReadSingle(), y = r.ReadSingle();
			if (float.IsNaN(x) || float.IsNaN(y)) return;
			SetInput(who, MindInputState.Unpack(b, new Vector2(x, y)));
		}
	}
}
