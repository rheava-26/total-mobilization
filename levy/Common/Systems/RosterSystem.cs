using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// The roster: regulars (persistent), the memorial, command points, tiers, dawn ticks, payday, desertion, respawn queue,
	/// body deployment under the render cap, and orders. Server-authoritative. OWNED BY THE ROSTER AGENT; the public surface
	/// below is the contract other modules call.
	/// </summary>
	public class RosterSystem : ModSystem
	{
		public static RosterSystem Instance => ModContent.GetInstance<RosterSystem>();

		public readonly List<SoldierRecord> Regulars = new();
		public readonly List<SoldierRecord> Memorial = new();
		public int NextId = 1;
		/// <summary>Day counter (dawns seen) for once-per-day flags.</summary>
		public int DayNumber;
		public int RallyCooldown;

		// ---- progression (computed from boss flags) ----
		public virtual int CommandPoints => LevyConstants.CommandPointsBase;
		public virtual int CommandPointsUsed { get { int n = 0; foreach (var r in Regulars) n += r.CommandPointCost; return n; } }
		public virtual int Tier => 1;
		public virtual long LevyWage => LevyConstants.TierLevyWage[0];
		public virtual bool GradeUnlocked(Grade g) => g == Grade.Levy;

		// ---- roster ops (server) ----
		public virtual SoldierRecord Enlist(BarracksTE home, Player commander, Grade grade) => null;
		public virtual bool Promote(SoldierRecord rec, Grade to) => false;
		public virtual void Discharge(SoldierRecord rec) { }
		public virtual bool FeedCrystal(SoldierRecord rec, int itemType) => false;
		public virtual SoldierRecord FindById(int id) { foreach (var r in Regulars) if (r.Id == id) return r; return null; }
		public virtual IEnumerable<SoldierRecord> RegularsOf(Player commander) { foreach (var r in Regulars) if (r.CommanderName == commander.name) yield return r; }
		public virtual void Rebase(SoldierRecord rec, BarracksTE newHome) { }

		// ---- treasury (server; UI calls these or sends Deposit/Withdraw packets) ----
		/// <summary>Takes coins from the player's inventory into the barracks treasury. Returns false if they cannot pay.</summary>
		public virtual bool Deposit(Player player, BarracksTE barracks, long copper) => false;
		/// <summary>Moves coins from the treasury to the player (spawned at the player). Returns false if the treasury is short.</summary>
		public virtual bool Withdraw(Player player, BarracksTE barracks, long copper) => false;

		// ---- bodies ----
		/// <summary>Spawns a body for a record at a position (server). Returns the NPC index or -1 (render cap, no slot).</summary>
		public virtual int SpawnBody(SoldierRecord rec, Vector2 position) => -1;
		public virtual IEnumerable<SoldierNPC> ActiveBodies() { for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC s && s.Record != null) yield return s; }
		public virtual int ActiveBodyCount { get { int n = 0; foreach (var _ in ActiveBodies()) n++; return n; } }
		/// <summary>Called by the body on death (server). Handles drops, manpower, respawn queue, memorial, chat, fear events.</summary>
		public virtual void OnBodyDied(SoldierNPC body, int killerNpcType, string killerName) { }
		public virtual void OnBodyDeserted(SoldierNPC body) { }

		// ---- orders ----
		/// <summary>Order regulars within <see cref="LevyConstants.OrderRadius"/> tiles of the commander (or all of theirs when atBarracks).</summary>
		public virtual void IssueOrder(Player commander, SoldierOrder order, bool atBarracks = false) { }
		public virtual void ToggleHoldFire(Player commander, bool atBarracks = false) { }
		public virtual void Rally(Player commander) { }
		public virtual void AssignPatrol(SoldierRecord rec, Terraria.DataStructures.Point16 a, Terraria.DataStructures.Point16 b) { }

		// ---- hooks for other modules ----
		/// <summary>A main boss was killed: veterancy, rally, victory barks.</summary>
		public virtual void OnMainBossKilled(NPC boss) { }
		/// <summary>The commander died: followers go home.</summary>
		public virtual void OnCommanderDied(Player commander) { }

		// ---- persistence (contract: records are saved here) ----
		public override void SaveWorldData(TagCompound tag)
		{
			var list = new List<TagCompound>(); foreach (var r in Regulars) list.Add(r.Save()); tag["regulars"] = list;
			var mem = new List<TagCompound>(); foreach (var r in Memorial) mem.Add(r.Save()); tag["memorial"] = mem;
			tag["nextId"] = NextId; tag["day"] = DayNumber;
		}
		public override void LoadWorldData(TagCompound tag)
		{
			Regulars.Clear(); Memorial.Clear();
			foreach (var t in tag.GetList<TagCompound>("regulars")) Regulars.Add(SoldierRecord.Load(t));
			foreach (var t in tag.GetList<TagCompound>("memorial")) Memorial.Add(SoldierRecord.Load(t));
			NextId = tag.GetInt("nextId"); if (NextId < 1) NextId = 1; DayNumber = tag.GetInt("day");
		}
		public override void ClearWorld() { Regulars.Clear(); Memorial.Clear(); NextId = 1; DayNumber = 0; }
	}
}
