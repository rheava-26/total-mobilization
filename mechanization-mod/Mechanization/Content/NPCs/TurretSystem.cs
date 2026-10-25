using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Targeting;
using Levy.Common.Util;
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// The turret registry: what makes a placed <see cref="TurretNPC"/> survive a world save/reload. A turret NPC is itself transient
	/// (<see cref="TurretNPC.NeedSaving"/> is false, so vanilla never writes it); this system remembers each placed turret's world
	/// position and current HP in the WORLD save data and re-spawns the NPCs on load. A destroyed turret's record is dropped, so a
	/// turret the enemy knocks out never resurrects.
	///
	/// Modeled on Levy's <c>DroneSystem</c>: the respawn and the bookkeeping run only on server / single player, once per tick in
	/// <see cref="PostUpdateNPCs"/> (the safe window to call <see cref="NPC.NewNPC"/>). Each record is paired to its live NPC by a
	/// stable <see cref="TurretRecord.Id"/> carried on the NPC (<see cref="TurretNPC.RecordId"/>), so slot reuse never confuses the two.
	/// A reconcile pass drops any record whose NPC has gone (death, slot reuse, another mod removing it) — the robust equivalent of a
	/// death hook that fires however the turret was lost.
	/// </summary>
	public class TurretSystem : ModSystem
	{
		public static TurretSystem Instance => ModContent.GetInstance<TurretSystem>();

		/// <summary>One placed turret's saved state. <see cref="Id"/> is stable for the life of the world; position is the NPC top-left; life is its current HP.</summary>
		public sealed class TurretRecord
		{
			public int Id;
			public Vector2 Position;
			public int Life;
		}

		private readonly Dictionary<int, TurretRecord> _records = new();
		private readonly HashSet<int> _liveIds = new();
		private int _nextId = 1;
		/// <summary>Set by <see cref="LoadWorldData"/>; the next server / single update re-spawns the saved turrets (the safe window for <see cref="NPC.NewNPC"/>).</summary>
		private bool _pendingRespawn;

		// Clearing on unload (which always runs when leaving a world) makes a NEW world — for which LoadWorldData is never called — start empty,
		// so turrets never leak between worlds in one session.
		public override void OnWorldUnload()
		{
			_records.Clear();
			_liveIds.Clear();
			_nextId = 1;
			_pendingRespawn = false;
		}

		// =====================================================================================================
		// Placement
		// =====================================================================================================
		/// <summary>
		/// Server / single player: registers a new turret and spawns its NPC at <paramref name="spot"/> (where the player aimed). The NPC falls
		/// to rest under its own gravity; its settled position is captured at save time. Returns the NPC slot, or -1 on failure (no record is kept).
		/// </summary>
		public int PlaceNew(IEntitySource source, Vector2 spot)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
			var rec = new TurretRecord { Id = _nextId++, Position = spot, Life = -1 };
			int idx = Spawn(source, rec, spot);
			if (idx < 0) { _nextId--; return -1; } // spawn failed: do not keep a record with no NPC, and reuse the id
			_records[rec.Id] = rec;
			return idx;
		}

		// =====================================================================================================
		// Enemy aggro: feed live turrets into Levy's targeting body list every tick, so hostiles path to them like a soldier or drone.
		// =====================================================================================================
		/// <summary>
		/// Appends every live turret to <see cref="EnemyTargeting.Bodies"/>. Levy's own <c>EnemyTargetingSystem.PreUpdateNPCs</c> clears and
		/// rebuilds that list earlier this tick (Mechanization sorts after Levy, so this runs after it); NPC AI runs afterwards and reads the list,
		/// so a turret added here is a target for the remainder of the tick. Runs on every netmode — the slot-255 swap runs on clients too.
		/// </summary>
		public override void PreUpdateNPCs()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is TurretNPC t && t.IsAlive)
					EnemyTargeting.Bodies.Add(t);
			}
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

		/// <summary>Spawns a turret NPC for every saved record that has no live NPC. Runs once right after a world load.</summary>
		private void RespawnAll()
		{
			int spawned = 0;
			foreach (TurretRecord rec in _records.Values)
			{
				if (LiveNpcFor(rec.Id) != null) continue;
				Vector2 spot = rec.Position + new Vector2(T.BodyWidth * 0.5f, T.BodyHeight); // approx bottom-center; Spawn then sets the exact top-left
				if (Spawn(new EntitySource_WorldEvent("Mechanization:TurretRestore"), rec, spot) >= 0) spawned++;
			}
			Mechanization.Instance.Logger.Info($"Mechanization: turret registry restored {_records.Count} saved turret(s); re-spawned {spawned}.");
		}

		/// <summary>Drops any record whose NPC is no longer present (destroyed, slot reused): a knocked-out turret never resurrects on the next reload.</summary>
		private void Reconcile()
		{
			if (_records.Count == 0) return;
			_liveIds.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is TurretNPC t && t.RecordId > 0) _liveIds.Add(t.RecordId);
			}
			if (_liveIds.Count == _records.Count) return; // every record still has its NPC
			List<int> gone = null;
			foreach (int id in _records.Keys) if (!_liveIds.Contains(id)) (gone ??= new List<int>()).Add(id);
			if (gone != null) foreach (int id in gone) _records.Remove(id);
		}

		/// <summary>Spawns one turret NPC bound to <paramref name="rec"/> at <paramref name="spot"/> (bottom-center), then pins its exact saved position and restores its HP. Returns the slot, or -1.</summary>
		private static int Spawn(IEntitySource source, TurretRecord rec, Vector2 spot)
		{
			int type = ModContent.NPCType<TurretNPC>();
			if (type <= 0) return -1;
			int idx = NPC.NewNPC(source, (int)spot.X, (int)spot.Y, type);
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			NPC n = Main.npc[idx];
			if (n.ModNPC is not TurretNPC t) { n.active = false; return -1; }
			t.RecordId = rec.Id;
			if (rec.Life > 0) n.life = rec.Life > n.lifeMax ? n.lifeMax : rec.Life; // a damaged turret reloads damaged; -1 means a fresh placement
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			return idx;
		}

		private static NPC LiveNpcFor(int recordId)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is TurretNPC t && t.RecordId == recordId) return n;
			}
			return null;
		}

		// =====================================================================================================
		// World save / load
		// =====================================================================================================
		public override void SaveWorldData(TagCompound tag)
		{
			// Snapshot each live turret's settled position and current HP into its record, so a reload restores where it sat and how hurt it was.
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is TurretNPC t && t.RecordId > 0 && _records.TryGetValue(t.RecordId, out TurretRecord rec))
				{
					rec.Position = n.position;
					rec.Life = n.life;
				}
			}

			var list = new List<TagCompound>(_records.Count);
			foreach (TurretRecord rec in _records.Values)
				list.Add(new TagCompound { ["id"] = rec.Id, ["x"] = rec.Position.X, ["y"] = rec.Position.Y, ["life"] = rec.Life });
			tag["turrets"] = list;
			tag["nextId"] = _nextId;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			_records.Clear();
			_nextId = tag.ContainsKey("nextId") ? tag.GetInt("nextId") : 1;
			if (_nextId < 1) _nextId = 1;

			int maxId = 0;
			foreach (TagCompound rt in tag.GetList<TagCompound>("turrets"))
			{
				int id = rt.GetInt("id");
				if (id <= 0) continue;
				var rec = new TurretRecord
				{
					Id = id,
					Position = new Vector2(rt.GetFloat("x"), rt.GetFloat("y")),
					Life = rt.ContainsKey("life") ? rt.GetInt("life") : -1,
				};
				_records[id] = rec;
				if (id > maxId) maxId = id;
			}
			if (_nextId <= maxId) _nextId = maxId + 1; // never re-issue a saved id
			_pendingRespawn = true;
			Mechanization.Instance.Logger.Info($"Mechanization: turret registry loaded {_records.Count} saved turret(s) from world data (will re-spawn on the next update).");
		}
	}
}
