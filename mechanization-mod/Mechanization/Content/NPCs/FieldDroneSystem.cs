using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Mechanization.Common;
using Mechanization.Content.Items;
using D = Mechanization.Common.MechConstants.Drone;
using F = Mechanization.Common.MechConstants.FieldDrone;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// The registry of player-built drones (<see cref="DroneItem"/>): what makes a drone the player set down survive a world save and reload, and what places and packs it. A drone NPC is itself transient
	/// (<see cref="DroneNPC.NeedSaving"/> is false); this system remembers each one's home pad and current HP in the WORLD save data and re-spawns the NPCs on load, exactly as
	/// <see cref="TurretSystem"/> does for turrets. A destroyed drone's record is dropped, so it never resurrects. These drones belong to no Barracks and are not counted by any garrison.
	///
	/// Placement is server-authoritative: single player places directly; a multiplayer client sends <see cref="MechPacket.PlaceDrone"/> and the server validates it and refunds the item if there is no room.
	/// </summary>
	public class FieldDroneSystem : ModSystem
	{
		public static FieldDroneSystem Instance => ModContent.GetInstance<FieldDroneSystem>();

		/// <summary>One placed drone's saved state. <see cref="Pad"/> is the body centre resting on the ground (or the hover point); life is its current HP.</summary>
		public sealed class FieldRecord
		{
			public int Id;
			public Vector2 Pad;
			public int Life;
			/// <summary>True when the NPC could not be spawned yet (no free NPC slot on load): the record is kept and the spawn retried, never dropped as destroyed.</summary>
			public bool Pending;
		}

		private readonly Dictionary<int, FieldRecord> _records = new();
		private readonly HashSet<int> _liveIds = new();
		private int _nextId = 1;
		private bool _pendingRespawn;
		private static readonly uint[] _lastPlace = new uint[Main.maxPlayers + 1];

		public int RecordCount => _records.Count;

		public override void OnWorldUnload()
		{
			_records.Clear(); _liveIds.Clear(); _nextId = 1; _pendingRespawn = false;
			System.Array.Clear(_lastPlace, 0, _lastPlace.Length);
		}

		// =====================================================================================================
		// Placement
		// =====================================================================================================
		/// <summary>
		/// Where a drone set down at <paramref name="spot"/> rests: a spot buried in terrain is lifted to the first open space above it (up to <see cref="F.PlaceLiftTiles"/>), then the pad is the ground beneath
		/// (the body centre, resting on it). With no ground within <see cref="F.PlaceDropTiles"/> the pad is the spot itself and the drone hangs there. False when there is no room.
		/// </summary>
		public static bool FindPad(Vector2 spot, out Vector2 pad)
		{
			pad = spot;
			if (!WorldGen.InWorld((int)(spot.X / MechConstants.TilePx), (int)(spot.Y / MechConstants.TilePx), 10)) return false;
			bool Blocked(Vector2 c) => Collision.SolidCollision(new Vector2(c.X - D.BodyWidth * 0.5f, c.Y - D.BodyHeight * 0.5f), D.BodyWidth, D.BodyHeight);

			Vector2 pos = spot;
			int lifted = 0;
			while (Blocked(pos))
			{
				pos.Y -= MechConstants.TilePx;
				if (++lifted > F.PlaceLiftTiles) return false;
			}
			Vector2 ground = pos;
			bool landed = false;
			for (int i = 0; i < F.PlaceDropTiles; i++)
			{
				Vector2 next = ground + new Vector2(0f, MechConstants.TilePx);
				if (!WorldGen.InWorld((int)(next.X / MechConstants.TilePx), (int)(next.Y / MechConstants.TilePx), 10)) break;
				if (Blocked(next)) { landed = true; break; }
				ground = next;
			}
			if (!landed) { pad = pos; return true; } // nothing to land on in reach: it hangs where it was set down
			for (int i = 0; i < MechConstants.TilePx; i++)
			{
				Vector2 next = ground + new Vector2(0f, 1f);
				if (Blocked(next)) break;
				ground = next;
			}
			pad = ground;
			return true;
		}

		/// <summary>Server / single player: <see cref="FindPad"/> then <see cref="PlaceNew"/>. Returns the NPC slot, or -1 when there is no room or no free NPC slot.</summary>
		public int TryPlace(IEntitySource source, Vector2 spot) => TryPlace(source, spot, out _);

		/// <summary>As <see cref="TryPlace(IEntitySource, Vector2)"/>, and says why it refused: the UI key of a one-line reason ("" when it placed one).</summary>
		public int TryPlace(IEntitySource source, Vector2 spot, out string refusal)
		{
			refusal = "";
			if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
			if (!FindPad(spot, out Vector2 pad)) { refusal = "Mods.Mechanization.UI.DroneNoRoom"; return -1; }
			int active = 0;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) active++;
			// The player's own drone is a toy they paid for: it is refused only when the NPC array is about to be full (the army's reserve stands down garrison drones, never this one).
			if (Main.maxNPCs - active < F.NpcSlotFloor) { Levy.Common.Budget.CrowdBudget.NoteDroneGate(); refusal = "Mods.Mechanization.UI.DroneNoSlots"; _lastFree = Main.maxNPCs - active; return -1; }
			int idx = PlaceNew(source, pad);
			if (idx < 0) refusal = "Mods.Mechanization.UI.DroneNoRoom";
			return idx;
		}

		private int _lastFree;
		/// <summary>The text for a refusal key from <see cref="TryPlace(IEntitySource, Vector2, out string)"/>.</summary>
		public string RefusalText(string key) => key.EndsWith("DroneNoSlots") ? Terraria.Localization.Language.GetTextValue(key, _lastFree, F.NpcSlotFloor) : Terraria.Localization.Language.GetTextValue(key);

		/// <summary>
		/// Server: a client asked to set a drone down at <paramref name="spot"/>. Validates the sender (alive, near the spot, holding the drone, not spamming), places it, and when it cannot be placed
		/// hands the item back (the client already spent it) with a word on why. Returns true when a drone was placed.
		/// </summary>
		public bool PlaceFor(Player player, Vector2 spot)
		{
			if (Main.netMode != NetmodeID.Server || player == null || !player.active || player.dead) return false;
			if (float.IsNaN(spot.X) || float.IsNaN(spot.Y) || float.IsInfinity(spot.X) || float.IsInfinity(spot.Y)) return false;
			if (Vector2.DistanceSquared(player.Center, spot) > F.PlaceReachPx * F.PlaceReachPx) return false;
			if (player.HeldItem == null || player.HeldItem.type != ModContent.ItemType<DroneItem>()) return false;
			uint now = Main.GameUpdateCount;
			int who = System.Math.Clamp(player.whoAmI, 0, Main.maxPlayers);
			if (_lastPlace[who] != 0 && now - _lastPlace[who] < (uint)F.PlaceMinGapTicks && now >= _lastPlace[who]) return false;
			_lastPlace[who] = now == 0 ? 1u : now;

			if (TryPlace(new EntitySource_ItemUse(player, player.HeldItem), spot, out string refusal) >= 0) return true;
			Item.NewItem(new EntitySource_Misc("Mechanization:DroneRefund"), player.getRect(), ModContent.ItemType<DroneItem>());
			Terraria.Chat.ChatHelper.SendChatMessageToClient(refusal.EndsWith("DroneNoSlots") ? Terraria.Localization.NetworkText.FromKey(refusal, _lastFree, F.NpcSlotFloor) : Terraria.Localization.NetworkText.FromKey("Mods.Mechanization.UI.DroneNoRoom"), new Color(255, 190, 110), player.whoAmI);
			return false;
		}

		/// <summary>Server / single player: registers a new drone and spawns its NPC resting on <paramref name="pad"/>. Returns the NPC slot, or -1 on failure (no record is kept).</summary>
		public int PlaceNew(IEntitySource source, Vector2 pad)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
			var rec = new FieldRecord { Id = _nextId++, Pad = pad, Life = -1 };
			int idx = Spawn(source, rec);
			if (idx < 0) { _nextId--; return -1; }
			_records[rec.Id] = rec;
			return idx;
		}

		// =====================================================================================================
		// Pack up
		// =====================================================================================================
		/// <summary>
		/// Server / single player: picks <paramref name="npc"/> back up for <paramref name="player"/>. Validates the drone is a live player-built one and the player is close, hands over one
		/// <see cref="DroneItem"/> at the player's feet, drops the saved record (so it never respawns) and removes the NPC. Two clients racing pay out once: the second call sees an inactive NPC.
		/// </summary>
		public bool Pack(NPC npc, Player player)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return false;
			if (npc == null || !npc.active || npc.ModNPC is not DroneNPC d || !d.IsField) return false;
			if (player == null || !player.active || player.dead) return false;
			if (Vector2.DistanceSquared(player.Center, npc.Center) > F.PackReachPx * F.PackReachPx) return false;

			if (d.RecordId > 0) _records.Remove(d.RecordId);
			int slot = npc.whoAmI;
			npc.active = false;
			npc.life = 0;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, slot);

			Item.NewItem(new EntitySource_Misc("Mechanization:DronePack"), player.getRect(), ModContent.ItemType<DroneItem>());
			SoundEngine.PlaySound(SoundID.Item37, npc.Center);
			return true;
		}

		// =====================================================================================================
		// Per-tick bookkeeping (server / single)
		// =====================================================================================================
		public override void PostUpdateNPCs()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			if (_pendingRespawn) { _pendingRespawn = false; RespawnAll(); return; } // let the re-spawned NPCs register before the first reconcile
			Reconcile();
		}

		/// <summary>Spawns a drone NPC for every saved record that has no live NPC. Runs once right after a world load.</summary>
		internal void RespawnAll()
		{
			int spawned = 0;
			foreach (FieldRecord rec in _records.Values)
			{
				if (LiveNpcFor(rec.Id) != null) continue;
				if (Spawn(new EntitySource_WorldEvent("Mechanization:DroneRestore"), rec) >= 0) spawned++;
				else rec.Pending = true;
			}
			Mechanization.Instance.Logger.Info($"Mechanization: drone registry restored {_records.Count} saved drone(s); re-spawned {spawned}.");
		}

		/// <summary>Drops any record whose NPC is no longer present (destroyed, slot reused): a knocked-out drone never resurrects on the next reload.</summary>
		internal void Reconcile()
		{
			if (_records.Count == 0) return;
			_liveIds.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is DroneNPC d && d.RecordId > 0) _liveIds.Add(d.RecordId);
			}
			if (_liveIds.Count == _records.Count) return;
			List<int> gone = null;
			foreach (KeyValuePair<int, FieldRecord> kv in _records)
			{
				if (_liveIds.Contains(kv.Key)) { kv.Value.Pending = false; continue; }
				if (kv.Value.Pending)
				{
					if (Main.GameUpdateCount % 60u == 0u) Spawn(new EntitySource_WorldEvent("Mechanization:DroneRestore"), kv.Value);
					continue;
				}
				(gone ??= new List<int>()).Add(kv.Key);
			}
			if (gone != null) foreach (int id in gone) _records.Remove(id);
		}

		/// <summary>Spawns one drone NPC bound to <paramref name="rec"/> on its pad and restores its HP. Returns the slot, or -1.</summary>
		private static int Spawn(IEntitySource source, FieldRecord rec)
		{
			int type = ModContent.NPCType<DroneNPC>();
			if (type <= 0) return -1;
			int idx = NPC.NewNPC(source, (int)rec.Pad.X, (int)(rec.Pad.Y + D.BodyHeight * 0.5f), type);
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			NPC n = Main.npc[idx];
			if (n.ModNPC is not DroneNPC drone) { n.active = false; return -1; }
			drone.BindField(rec.Id, rec.Pad);
			if (rec.Life > 0) n.life = rec.Life > n.lifeMax ? n.lifeMax : rec.Life; // a damaged drone reloads damaged; -1 means a fresh placement
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			return idx;
		}

		private static NPC LiveNpcFor(int recordId)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is DroneNPC d && d.RecordId == recordId) return n;
			}
			return null;
		}

		// =====================================================================================================
		// World save / load
		// =====================================================================================================
		public override void SaveWorldData(TagCompound tag)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is DroneNPC d && d.RecordId > 0 && _records.TryGetValue(d.RecordId, out FieldRecord rec)) rec.Life = n.life; // the pad is the home: it never moves
			}
			var list = new List<TagCompound>(_records.Count);
			foreach (FieldRecord rec in _records.Values)
				list.Add(new TagCompound { ["id"] = rec.Id, ["x"] = rec.Pad.X, ["y"] = rec.Pad.Y, ["life"] = rec.Life });
			tag["fieldDrones"] = list;
			tag["nextId"] = _nextId;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			_records.Clear();
			_nextId = tag.ContainsKey("nextId") ? tag.GetInt("nextId") : 1;
			if (_nextId < 1) _nextId = 1;
			int maxId = 0;
			foreach (TagCompound rt in tag.GetList<TagCompound>("fieldDrones"))
			{
				int id = rt.GetInt("id");
				if (id <= 0) continue;
				_records[id] = new FieldRecord { Id = id, Pad = new Vector2(rt.GetFloat("x"), rt.GetFloat("y")), Life = rt.ContainsKey("life") ? rt.GetInt("life") : -1 };
				if (id > maxId) maxId = id;
			}
			if (_nextId <= maxId) _nextId = maxId + 1;
			_pendingRespawn = true;
			Mechanization.Instance.Logger.Info($"Mechanization: drone registry loaded {_records.Count} saved drone(s) from world data (will re-spawn on the next update).");
		}
	}
}
