using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Settlements: barracks enumeration, town-NPC membership, manpower cap/regen, militia muster/stand-down/reinforcements,
	/// auto-muster on events. OWNED BY THE MILITIA AGENT. Public surface is the contract.
	/// </summary>
	public class SettlementSystem : ModSystem
	{
		public static SettlementSystem Instance => ModContent.GetInstance<SettlementSystem>();

		public virtual IEnumerable<BarracksTE> AllBarracks() { foreach (var te in TileEntity.ByID.Values) if (te is BarracksTE b) yield return b; }
		public virtual BarracksTE GetBarracks(int id) => id >= 0 && TileEntity.ByID.TryGetValue(id, out var te) && te is BarracksTE b ? b : null;
		public virtual BarracksTE NearestBarracks(Vector2 worldPos, float maxTiles = float.MaxValue) => null;
		/// <summary>Town NPCs (not pets, Old Man, Travelling Merchant, bound) whose home is inside the settlement radius and whose nearest barracks is this one.</summary>
		public virtual List<NPC> TownNpcsOf(BarracksTE barracks) => new();
		public virtual int ManpowerCap(BarracksTE barracks) => 0;
		public virtual IEnumerable<ArmoryTE> ArmoriesNear(Vector2 worldPos, float tiles) { foreach (var te in TileEntity.ByID.Values) if (te is ArmoryTE a && Vector2.Distance(a.Center, worldPos) <= tiles * 16f) yield return a; }
		public virtual ArmoryTE LinkedArmory(BarracksTE barracks) => null;
		public virtual ArmoryTE NearestArmory(Vector2 worldPos, float maxTiles) => null;
		public virtual string TownName(BarracksTE barracks) => barracks?.TownName ?? "";

		// ---- lifecycle (server) ----
		/// <summary>Called by RosterSystem once per dawn (after its own payday): manpower regen, daily flags, chat.</summary>
		public virtual void OnDawn(int dayNumber) { }
		/// <summary>Called by RosterSystem when a militia body dies (after manpower was taken).</summary>
		public virtual void OnMilitiaBodyDied(BarracksTE barracks, Soldiers.SoldierRecord rec, Content.NPCs.SoldierNPC body) { }

		// ---- militia (server) ----
		public virtual bool CanRaiseMilitia(BarracksTE barracks) => false;
		public virtual void Mobilize(BarracksTE barracks, Player by) { }
		public virtual void StandDown(BarracksTE barracks) { }
		public virtual void OnMilitiaDied(BarracksTE barracks, Soldiers.SoldierRecord rec) { }
		public virtual bool RecruitingDrive(BarracksTE barracks, Player by) => false;
		public virtual bool HazardPay(BarracksTE barracks, Player by) => false;
		/// <summary>True while any event the militia answers is active (cached per tick).</summary>
		public virtual bool EventActive => Util.LevyUtil.IsEventActive();

		// ---- world flags set by event books (persisted by the militia agent) ----
		public bool BookConscription, BookDrill, BookPlunder, BookFortification, BookDiscipline;
	}
}
