using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Systems
{
	/// <summary>
	/// ACTIVE ARMY SIZE: how many of one commander's Regulars are in the field at once (0, the default, fields everyone). The player picks it (Levy: Performance, the Barracks, the lag notes);
	/// the client sends it to the server, which keeps it per commander for the session (<see cref="SetArmyCap"/>).
	/// <para/>The rest stay on active duty (paid, holding their command points, taking orders) but have no body: they wait, as soldiers under the render cap always have, and the
	/// ordinary deployment (<see cref="Deploy"/>, <see cref="SpawnBody"/>) fields them in <see cref="DeployOrder"/> (order, then grade, Champions first, then enlistment).
	/// When a fielded soldier falls, the next one waiting appears the same tick where he fell (beside the commander under Regroup, away from a hazard that killed him) instead of half a second later at the muster point.
	/// A replacement is a living soldier who was already waiting: the fallen one still waits out his own respawn timer and costs his barracks one manpower, exactly as before.
	/// Lowering the size benches the lowest-priority soldiers in the field the way the reserve does (<see cref="Demobilize"/>: the live HP goes onto the record, the body retires, the kit stays on the record).
	/// Nothing here creates, removes or copies a record or an item: it only decides which records get a body.
	/// </summary>
	public partial class RosterSystem
	{
		/// <summary>Commander name to active army size. Absent or 0: no limit. Server / single player; filled by <see cref="SetArmyCap"/>, emptied with the world.</summary>
		private static readonly Dictionary<string, int> _armyCaps = new();

		private struct Opening { public string Commander; public Vector2 Spot; public bool UseSpot; }
		/// <summary>Places freed this tick by a soldier who fell while his commander has an active army size; filled after the NPC phase or at the end of the same tick (<see cref="DrainOpenings"/>).</summary>
		private readonly List<Opening> _openings = new();
		private readonly List<SoldierRecord> _armyScratch = new();

		/// <summary>Self-check counters: replacements fielded at a fallen soldier's place, and the tick of the last one.</summary>
		internal static int Replacements; internal static uint LastReplacementTick;

		/// <summary>The active army size of a commander (0: everyone fights).</summary>
		public static int ArmyCapOf(string commander) => !string.IsNullOrEmpty(commander) && _armyCaps.TryGetValue(commander, out int c) ? c : 0;

		/// <summary>
		/// Sets a commander's active army size and applies it at once: above the size, the lowest-priority soldiers in the field are benched; below it, the soldiers waiting are fielded.
		/// <paramref name="cap"/> 0 removes the limit. <paramref name="announce"/> tells the commander in chat (not on the quiet resend when he enters a world). Server / single player only.
		/// </summary>
		public virtual void SetArmyCap(string commander, int cap, bool announce = false)
		{
			if (LevyUtil.IsClient || string.IsNullOrEmpty(commander)) return;
			cap = Math.Clamp(cap, 0, LevyConstants.ActiveArmyMax);
			int was = ArmyCapOf(commander);
			if (cap == 0) _armyCaps.Remove(commander); else _armyCaps[commander] = cap;
			EnforceArmyCap(commander);
			if (announce && was != cap)
			{
				Player cmd = null; int i = LevyUtil.FindPlayerByName(commander); if (i >= 0) cmd = Main.player[i];
				SendToPlayer(cmd, cap == 0 ? LevyUtil.T("Roster.ActiveArmyOff") : LevyUtil.T("Roster.ActiveArmySet", cap));
			}
		}

		/// <summary>The commander's Regulars that have a body in the field right now (the dying are already off: their record lost the body when they fell).</summary>
		public virtual int FieldedOf(string commander)
		{
			int n = 0;
			if (string.IsNullOrEmpty(commander)) return 0;
			foreach (var r in Regulars) if (r != null && r.Kind == SoldierKind.Regular && r.CommanderName == commander && !r.Reserve && !r.Dead && r.HasBody) n++;
			return n;
		}

		/// <summary>False when this soldier's commander has an active army size and that many of his soldiers are already in the field. Militia and officers are never limited.</summary>
		public virtual bool ArmyHasRoom(SoldierRecord rec)
		{
			if (rec == null || rec.Kind != SoldierKind.Regular) return true;
			int cap = ArmyCapOf(rec.CommanderName);
			return cap <= 0 || FieldedOf(rec.CommanderName) < cap;
		}

		/// <summary>Over the size: benches the lowest-priority soldiers in the field. Then fields whoever is waiting while there is room.</summary>
		private void EnforceArmyCap(string commander)
		{
			int cap = ArmyCapOf(commander);
			if (cap > 0)
			{
				_armyScratch.Clear();
				foreach (var r in Regulars) if (r != null && r.Kind == SoldierKind.Regular && r.CommanderName == commander && !r.Reserve && !r.Dead && r.HasBody) _armyScratch.Add(r);
				if (_armyScratch.Count > cap)
				{
					_armyScratch.Sort(DeployOrder);
					for (int i = _armyScratch.Count - 1; i >= cap; i--) BenchForCap(_armyScratch[i]);
					_armyScratch.Clear();
					SendRosterSnapshot();
					return;
				}
				_armyScratch.Clear();
			}
			FillArmy(commander, default, false);
		}

		/// <summary>Takes one soldier's body out of the field for the active army size, as the reserve does, without putting him in reserve: his live HP goes onto the record, his kit stays on it.</summary>
		private static void BenchForCap(SoldierRecord rec)
		{
			var body = BodyOf(rec);
			if (body == null) return;
			rec.CurrentHp = Math.Clamp(body.NPC.life, 1, Math.Max(1, rec.MaxHp)); // the body held the live value
			RetireBody(body);
			rec.BodyIndex = -1; rec.Fear = 0f; rec.EngagedBosses.Clear();
		}

		/// <summary>
		/// Fields the commander's waiting soldiers, best first (<see cref="DeployOrder"/>), while his active army size has room. The first one appears at <paramref name="spot"/> when <paramref name="useSpot"/>
		/// (a fallen soldier's place; <see cref="SpawnBody"/> still moves a Regroup soldier beside his commander and keeps anyone out of a crowd of enemies), the others where deployment would put them.
		/// Returns how many were fielded. Commanders without an active army size are left to the ordinary deployment.
		/// </summary>
		private int FillArmy(string commander, Vector2 spot, bool useSpot, bool replacement = false)
		{
			int cap = ArmyCapOf(commander);
			if (cap <= 0) return 0;
			int room = cap - FieldedOf(commander);
			if (room <= 0) return 0;
			_armyScratch.Clear();
			foreach (var r in Regulars) if (r != null && r.Kind == SoldierKind.Regular && r.CommanderName == commander && !r.Reserve && !r.Dead && !r.HasBody) _armyScratch.Add(r);
			if (_armyScratch.Count == 0) return 0;
			_armyScratch.Sort(DeployOrder);
			int made = 0;
			for (int i = 0; i < _armyScratch.Count && made < room; i++)
			{
				var rec = _armyScratch[i];
				Vector2 pos;
				if (useSpot && made == 0) pos = spot;
				else if (!SpawnPositionFor(rec, out pos)) continue;
				int idx = SpawnBody(rec, pos);
				if (idx >= 0)
				{
					made++;
					if (replacement && Main.npc[idx].ModNPC is SoldierNPC sn) sn.GrantReplacementImmunity(LevyConstants.ReplacementImmuneTicks); // only a replacement for a fallen soldier, never a raised size
				}
				else if (!ArmyHasRoom(rec)) break;
			}
			_armyScratch.Clear();
			return made;
		}

		/// <summary>A Regular fell: if his commander has an active army size, his place is filled at the end of this tick.</summary>
		private void NoteOpening(SoldierRecord rec, Vector2 where, int killerNpcType)
		{
			if (rec == null || rec.Kind != SoldierKind.Regular || ArmyCapOf(rec.CommanderName) <= 0) return;
			// lava, water or thorns: not on the same spot. A fall is not one (he landed on ground; -1 is also "no attacker known")
			bool hazard = killerNpcType == LevyConstants.EnvKillerLava || killerNpcType == LevyConstants.EnvKillerDrowning || killerNpcType == LevyConstants.EnvKillerHazard;
			_openings.Add(new Opening { Commander = rec.CommanderName, Spot = where, UseSpot = !hazard });
		}

		public override void PostUpdateEverything() => DrainOpenings();

		/// <summary>Fills the places freed since the last call. Run after the NPC phase (before the deployment pass, which would otherwise field the waiting soldier at the muster point) and at the end of the tick (deaths in the projectile and player phases).</summary>
		private void DrainOpenings()
		{
			if (LevyUtil.IsClient || _openings.Count == 0) return;
			int made = 0;
			for (int i = 0; i < _openings.Count; i++)
			{
				var o = _openings[i];
				int n = FillArmy(o.Commander, o.Spot, o.UseSpot, replacement: true);
				if (n > 0) { made += n; Replacements += n; LastReplacementTick = Main.GameUpdateCount; }
			}
			_openings.Clear();
			if (made > 0) SendRosterSnapshot();
		}

		private void ResetActiveArmy() { _armyCaps.Clear(); _openings.Clear(); _armyScratch.Clear(); }

		/// <summary>Self-check: forget every active army size without touching the field.</summary>
		internal static void TestClearArmyCaps() => _armyCaps.Clear();
	}

	/// <summary>Sends the player's active army size (Levy: Performance) to the server when he enters a world: the server keeps it only for the session.</summary>
	public class ActiveArmyPlayer : Terraria.ModLoader.ModPlayer
	{
		public override void OnEnterWorld()
		{
			if (Player.whoAmI == Main.myPlayer) UI.UIActions.SendActiveArmy(false);
		}
	}
}
