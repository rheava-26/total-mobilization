using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Officers
{
	/// <summary>
	/// One squad a summon officer fields: <see cref="Cap"/> units of one <see cref="Kind"/>, a lost unit replaced every <see cref="RespawnTicks"/> while below the cap.
	/// An officer with one kind of unit has one squad; the Pirate has two (a crew and a cannon).
	/// </summary>
	public readonly struct SummonSquad
	{
		public readonly SummonKind Kind;
		public readonly int Cap;
		public readonly int RespawnTicks;
		/// <summary>When set, the squad is CONJURED creatures (real enemy types on their own AI, see <see cref="ConjuredGlobalNPC"/>): it returns the NPC type to conjure each time one is needed (0 = none right now). <see cref="Kind"/> is then null.</summary>
		public readonly Func<int> Conjure;
		/// <summary>For a conjured squad: the least life and the least contact damage (before the world-progress multiply) a creature has, so a weak early creature is never weaker than the squad it replaces.</summary>
		public readonly int MinLife, MinDamage;
		/// <summary>For a conjured squad: how often a lost unit is replaced while an enemy is near (0 = the same as <see cref="RespawnTicks"/>), and how many ticks a unit may go without dealing damage in a fight before it crumbles (0 = never).</summary>
		public readonly int CombatRespawnTicks, IdleLifeTicks;
		public SummonSquad(SummonKind kind, int cap, int respawnTicks) { Kind = kind; Cap = cap; RespawnTicks = respawnTicks; Conjure = null; MinLife = 0; MinDamage = 0; CombatRespawnTicks = 0; IdleLifeTicks = 0; }
		public SummonSquad(Func<int> conjure, int cap, int respawnTicks, int minLife = 0, int minDamage = 0, int combatRespawnTicks = 0, int idleLifeTicks = 0) { Kind = null; Cap = cap; RespawnTicks = respawnTicks; Conjure = conjure; MinLife = minLife; MinDamage = minDamage; CombatRespawnTicks = combatRespawnTicks; IdleLifeTicks = idleLifeTicks; }
		/// <summary>The replacement cadence for the squad now: the combat cadence while an enemy is near, else the ordinary one.</summary>
		public int Cadence(bool fight) => fight && CombatRespawnTicks > 0 ? CombatRespawnTicks : RespawnTicks;
	}

	/// <summary>
	/// The keystone for summon officers: an <see cref="OfficerKit"/> that fields squads of <see cref="SummonedUnitNPC"/> (real NPCs
	/// with healthbars) for its officer. A concrete summon officer only declares its squads (<see cref="Squads"/>; or, for a single kind of unit,
	/// just <see cref="Kind"/>, <see cref="SquadCap"/> and <see cref="RespawnCadenceTicks"/>); this base spawns each squad up to its cap, culls
	/// dead/orphaned units, respawns on a cadence when below cap, and kills every squad when the commission ends.
	///
	/// All squad state lives on the per-officer <see cref="State"/> object (never statics), so two commanders can field the same officer
	/// type and each keeps its own squads. Units are OWNER-BOUND to the officer avatar's whoAmI: they self-despawn if it is gone, and
	/// <see cref="OnDismissed"/> kills them explicitly (dismiss, lapse, commander offline, avatar death all route through it). Server /
	/// single player only; the units sync and draw themselves on clients.
	/// </summary>
	public abstract class SummonerKit : OfficerKit
	{
		/// <summary>The recipe for this officer's units when it has a single kind. Leave unset (null) when overriding <see cref="Squads"/>.</summary>
		protected virtual SummonKind Kind => null;
		/// <summary>How many units of <see cref="Kind"/> the officer keeps fielded at once.</summary>
		protected virtual int SquadCap => 0;
		/// <summary>Ticks between replacing a lost unit of <see cref="Kind"/> while below the cap (the initial squad is filled at once on mobilisation).</summary>
		protected virtual int RespawnCadenceTicks => 0;

		/// <summary>
		/// Every squad the officer fields, spawned and refilled in this order. The default is the single squad built from <see cref="Kind"/> / <see cref="SquadCap"/> /
		/// <see cref="RespawnCadenceTicks"/>; an officer with several kinds of unit overrides this. Immutable recipe data, so it may be cached and shared by every commander.
		/// </summary>
		protected virtual SummonSquad[] Squads => _single ??= new[] { new SummonSquad(Kind, SquadCap, RespawnCadenceTicks) };
		private SummonSquad[] _single;

		private sealed class State
		{
			/// <summary>whoAmI of each live unit this officer owns, per squad (same index as <see cref="Squads"/>).</summary>
			public List<int>[] Units;
			/// <summary>Ticks until the next replacement of a lost unit, per squad.</summary>
			public int[] RespawnTimer;
			/// <summary>Whether an enemy was near the officer at the last scan, and when that scan next runs (ticks).</summary>
			public bool Fight; public int FightScan;
		}

		public override object CreateState()
		{
			SummonSquad[] squads = Squads;
			var st = new State { Units = new List<int>[squads.Length], RespawnTimer = new int[squads.Length] };
			for (int i = 0; i < squads.Length; i++) st.Units[i] = new List<int>();
			return st;
		}

		public override void OnMobilized(OfficerContext ctx)
		{
			if (!LevyUtil.IsServerOrSingle || ctx.State is not State st) return;
			NPC avatar = ctx.Npc;
			if (avatar == null || !avatar.active) return;
			int commander = ctx.Commander?.whoAmI ?? -1;
			SummonSquad[] squads = Squads;
			for (int s = 0; s < squads.Length && s < st.Units.Length; s++)
			{
				// Stand each squad up at once so the officer marches out with it; refills are paced by the cadence.
				while (st.Units[s].Count < squads[s].Cap)
				{
					int idx = SpawnSquadUnit(avatar, commander, squads[s]);
					if (idx < 0) break; // no slot / no safe spot: the per-tick refill fills in later
					st.Units[s].Add(idx);
				}
				st.RespawnTimer[s] = squads[s].RespawnTicks;
			}
		}

		public override void Tick(OfficerContext ctx)
		{
			if (!LevyUtil.IsServerOrSingle || ctx.State is not State st) return;
			NPC avatar = ctx.Npc;
			if (avatar == null || !avatar.active) return;
			int ownerWho = avatar.whoAmI;
			int commander = ctx.Commander?.whoAmI ?? -1;
			SummonSquad[] squads = Squads;
			if (--st.FightScan <= 0) { st.FightScan = LevyConstants.Summon.FightScanTicks; st.Fight = ConjuredGlobalNPC.EnemyNear(avatar); }

			for (int s = 0; s < squads.Length && s < st.Units.Length; s++)
			{
				List<int> units = st.Units[s];
				// cull dead, despawned or orphaned (slot reused by another body) units
				for (int i = units.Count - 1; i >= 0; i--)
					if (!IsMyLiveUnit(units[i], ownerWho)) units.RemoveAt(i);

				if (st.RespawnTimer[s] > 0) st.RespawnTimer[s]--;
				if (st.RespawnTimer[s] > squads[s].Cadence(st.Fight)) st.RespawnTimer[s] = squads[s].Cadence(st.Fight); // a fight begun mid-wait shortens it
				if (units.Count < squads[s].Cap && st.RespawnTimer[s] <= 0)
				{
					int idx = SpawnSquadUnit(avatar, commander, squads[s]);
					if (idx >= 0) units.Add(idx);
					st.RespawnTimer[s] = squads[s].Cadence(st.Fight); // pace retries whether or not the spawn found a slot
				}
			}
		}

		public override void OnDismissed(OfficerContext ctx)
		{
			if (!LevyUtil.IsServerOrSingle || ctx.State is not State st) return;
			for (int s = 0; s < st.Units.Length; s++)
			{
				List<int> units = st.Units[s];
				for (int i = 0; i < units.Count; i++)
				{
					int who = units[i];
					if (who < 0 || who >= Main.maxNPCs) continue;
					NPC n = Main.npc[who];
					if (n.active && (n.ModNPC is SummonedUnitNPC || ConjuredGlobalNPC.IsConjured(n)))
					{
						n.life = 0; n.active = false; n.netUpdate = true;
						if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, who);
					}
				}
				units.Clear();
			}
		}

		/// <summary>A unit at this slot that is alive and still bound to this officer avatar (guards the slot being reused by another NPC).</summary>
		private static bool IsMyLiveUnit(int who, int ownerWho)
		{
			if (who < 0 || who >= Main.maxNPCs) return false;
			NPC n = Main.npc[who];
			if (!n.active) return false;
			if (n.ModNPC is SummonedUnitNPC u) return u.IsAlive && u.OwnerWho == ownerWho;
			return n.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active && c.IsAlive && c.OwnerWho == ownerWho; // a conjured creature of this officer
		}

		/// <summary>One unit of a squad: a conjured creature of the type the squad asks for, or a <see cref="SummonedUnitNPC"/> of its kind. Returns its whoAmI, or -1.</summary>
		private static int SpawnSquadUnit(NPC avatar, int commander, SummonSquad squad)
		{
			if (squad.Conjure == null) return SpawnOne(avatar, commander, squad.Kind);
			int type = squad.Conjure();
			return type > 0 ? ConjuredGlobalNPC.Conjure(avatar, commander, type, squad.MinLife, squad.MinDamage, squad.IdleLifeTicks) : -1;
		}

		/// <summary>Spawns one unit of <paramref name="kind"/> near the officer (safe placement) and binds it. Returns its whoAmI, or -1 when no slot/spot is available.</summary>
		private static int SpawnOne(NPC avatar, int commander, SummonKind kind)
		{
			if (kind == null) return -1;
			int type = ModContent.NPCType<SummonedUnitNPC>();
			if (type <= 0) return -1;
			Vector2 near = avatar.Bottom + new Vector2(Main.rand.Next(-28, 29), 0f);
			Vector2 pos = RosterSystem.SafeSpawnPosition(near); // never materialise inside a fight
			int idx = NPC.NewNPC(new EntitySource_WorldEvent("Levy:SummonedUnit"), (int)pos.X, (int)pos.Y, type);
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			NPC n = Main.npc[idx];
			if (n.ModNPC is not SummonedUnitNPC u) { n.active = false; return -1; }
			u.Init(kind, avatar.whoAmI, commander);
			n.Bottom = pos;
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			return idx;
		}
	}
}
