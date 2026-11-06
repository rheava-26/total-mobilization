using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Content.Items.Observe;

namespace Levy.Common.Observe
{
	/// <summary>
	/// Multiplayer plumbing for remote viewing. A client only ever receives the world sections near its own player, so watching a camera or a levy far away needs the SERVER to send those sections:
	/// the server keeps what each player is watching and, for it, (1) calls <c>RemoteClient.CheckSection(player, position, 1)</c> (the 3x3 sections around the target, about 600x450 tiles, which marks them
	/// loaded for that client so tiles, tile entities and the NPC/projectile broadcasts (gated by <c>RemoteClient.SectionRange</c>) all reach it) once a second, and (2) streams the target's position (a levy moves, and may not be
	/// an NPC the client has yet). Layouts: ObserveRequest (client to server): byte kind (0 stop, 1 camera, 2 levy, 3 list cameras), int id.
	/// ObserveList (server to client): int count, then per camera int id, short tileX, short tileY, string label. ObservePos (server to client): byte kind (1 camera, 2 levy, 3 mind control), int id, float x, float y.
	/// </summary>
	internal static class ObserveNet
	{
		private struct Watch { public byte Kind; public int Id; }
		private static readonly Watch[] _watch = new Watch[256];

		public static void Register()
		{
			LevyPackets.Register(LevyPacket.ObserveRequest, OnRequest);
			LevyPackets.Register(LevyPacket.ObserveList, OnList);
			LevyPackets.Register(LevyPacket.ObservePos, OnPos);
		}

		public static void Reset() => Array.Clear(_watch);

		// ---- client sends ----
		public static void SendRequest(byte kind, int id)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			var p = LevyPackets.New(LevyPacket.ObserveRequest); p.Write(kind); p.Write(id); p.Send();
		}

		// ---- server ----
		private static void OnRequest(BinaryReader r, int who)
		{
			byte kind = r.ReadByte(); int id = r.ReadInt32();
			if (Main.netMode != NetmodeID.Server || who < 0 || who >= Main.maxPlayers || !Main.player[who].active) return;
			switch (kind)
			{
				case 0: _watch[who] = default; break;
				case 1:
					if (StaticCameraTE.Get(id) == null) return;
					_watch[who] = new Watch { Kind = 1, Id = id }; Push(who, true);
					break;
				case 2:
					var rec = RosterSystem.Instance?.FindById(id);
					if (rec == null || rec.CommanderName != Main.player[who].name || !GoProItem.Carries(rec.Gear)) return;
					_watch[who] = new Watch { Kind = 2, Id = id }; Push(who, true);
					break;
				case 3: SendList(who); break;
			}
		}

		private static void SendList(int who)
		{
			var cams = StaticCameraTE.All();
			var p = LevyPackets.New(LevyPacket.ObserveList);
			p.Write(cams.Count);
			foreach (var c in cams) { p.Write(c.ID); p.Write((short)c.Position.X); p.Write((short)c.Position.Y); p.Write(c.Label ?? ""); }
			p.Send(who);
		}

		/// <summary>The world point a player's current watch (or mind link) is looking at, or false.</summary>
		private static bool Target(int who, out byte kind, out int id, out Vector2 pos)
		{
			kind = 0; id = 0; pos = default;
			var link = MindControl.LinkOfPlayer(who);
			if (link != null)
			{
				var body = MindControl.BodyOf(RosterSystem.Instance?.FindById(link.SoldierId));
				if (body == null) return false;
				kind = 3; id = link.SoldierId; pos = body.NPC.Center; return true;
			}
			var w = _watch[who];
			if (w.Kind == 1) { var cam = StaticCameraTE.Get(w.Id); if (cam == null) return false; kind = 1; id = w.Id; pos = cam.ViewCenter; return true; }
			if (w.Kind == 2)
			{
				var rec = RosterSystem.Instance?.FindById(w.Id);
				var body = MindControl.BodyOf(rec);
				if (body == null) return false;
				kind = 2; id = w.Id; pos = body.NPC.Center; return true;
			}
			return false;
		}

		private static void Push(int who, bool sections)
		{
			if (!Target(who, out byte kind, out int id, out Vector2 pos)) return;
			if (sections) RemoteClient.CheckSection(who, pos, 1);
			var p = LevyPackets.New(LevyPacket.ObservePos); p.Write(kind); p.Write(id); p.Write(pos.X); p.Write(pos.Y); p.Send(who);
		}

		public static void ServerTick()
		{
			if (Main.netMode != NetmodeID.Server) return;
			uint t = Main.GameUpdateCount;
			if (t % 6u != 0u) return;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				if (!Main.player[i].active) { _watch[i] = default; continue; }
				if (_watch[i].Kind == 0 && MindControl.LinkOfPlayer(i) == null) continue;
				Push(i, t % 60u == 0u);
			}
		}

		// ---- client receives ----
		private static void OnList(BinaryReader r, int _)
		{
			int n = r.ReadInt32(); var list = new List<ObserveSystem.CamInfo>(Math.Min(n, 512));
			for (int i = 0; i < n; i++)
			{
				var c = new ObserveSystem.CamInfo { Id = r.ReadInt32(), Tile = new Point16(r.ReadInt16(), r.ReadInt16()), Name = r.ReadString() };
				if (list.Count < 512) list.Add(c);
			}
			ObserveSystem.Cams = list;
		}

		private static void OnPos(BinaryReader r, int _)
		{
			byte kind = r.ReadByte(); int id = r.ReadInt32(); float x = r.ReadSingle(), y = r.ReadSingle();
			ObserveSystem.OnServerPos(kind, id, new Vector2(x, y));
		}
	}
}
