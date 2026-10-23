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
	/// The keystone for summon officers: an <see cref="OfficerKit"/> that fields a squad of <see cref="SummonedUnitNPC"/> (real NPCs
	/// with healthbars) for its officer. A concrete summon officer only declares its <see cref="Kind"/>, <see cref="SquadCap"/> and
	/// <see cref="RespawnCadenceTicks"/>; this base spawns up to the cap, culls dead/orphaned units, respawns on a cadence when below
	/// cap, and kills the whole squad when the commission ends.
	///
	/// All squad state lives on the per-officer <see cref="State"/> object (never statics), so two commanders can field the same officer
	/// type and each keeps its own squad. Units are OWNER-BOUND to the officer avatar's whoAmI: they self-despawn if it is gone, and
	/// <see cref="OnDismissed"/> kills them explicitly (dismiss, lapse, commander offline, avatar death all route through it). Server /
	/// single player only; the units sync and draw themselves on clients.
	/// </summary>
	public abstract class SummonerKit : OfficerKit
	{
		/// <summary>The recipe for this officer's units (HP, damage, weapon, sprite). One kind per summon officer.</summary>
		protected abstract SummonKind Kind { get; }
		/// <summary>How many units the officer keeps fielded at once.</summary>
		protected abstract int SquadCap { get; }
		/// <summary>Ticks between replacing a lost unit while below the cap (the initial squad is filled at once on mobilisation).</summary>
		protected abstract int RespawnCadenceTicks { get; }

		private sealed class State
		{
			/// <summary>whoAmI of each live unit this officer owns.</summary>
			public readonly List<int> Units = new();
			public int RespawnTimer;
		}

		public override object CreateState() => new State();

		public override void OnMobilized(OfficerContext ctx)
		{
			if (!LevyUtil.IsServerOrSingle || ctx.State is not State st) return;
			NPC avatar = ctx.Npc;
			if (avatar == null || !avatar.active) return;
			int commander = ctx.Commander?.whoAmI ?? -1;
			// Stand the crew up at once so the officer marches out with its squad; refills are paced by the cadence.
			while (st.Units.Count < SquadCap)
			{
				int idx = SpawnOne(avatar, commander);
				if (idx < 0) break; // no slot / no safe spot: the per-tick refill fills in later
				st.Units.Add(idx);
			}
			st.RespawnTimer = RespawnCadenceTicks;
		}

		public override void Tick(OfficerContext ctx)
		{
			if (!LevyUtil.IsServerOrSingle || ctx.State is not State st) return;
			NPC avatar = ctx.Npc;
			if (avatar == null || !avatar.active) return;
			int ownerWho = avatar.whoAmI;
			int commander = ctx.Commander?.whoAmI ?? -1;

			// cull dead, despawned or orphaned (slot reused by another body) units
			for (int i = st.Units.Count - 1; i >= 0; i--)
				if (!IsMyLiveUnit(st.Units[i], ownerWho)) st.Units.RemoveAt(i);

			if (st.RespawnTimer > 0) st.RespawnTimer--;
			if (st.Units.Count < SquadCap && st.RespawnTimer <= 0)
			{
				int idx = SpawnOne(avatar, commander);
				if (idx >= 0) st.Units.Add(idx);
				st.RespawnTimer = RespawnCadenceTicks; // pace retries whether or not the spawn found a slot
			}
		}

		public override void OnDismissed(OfficerContext ctx)
		{
			if (!LevyUtil.IsServerOrSingle || ctx.State is not State st) return;
			for (int i = 0; i < st.Units.Count; i++)
			{
				int who = st.Units[i];
				if (who < 0 || who >= Main.maxNPCs) continue;
				NPC n = Main.npc[who];
				if (n.active && n.ModNPC is SummonedUnitNPC)
				{
					n.life = 0; n.active = false; n.netUpdate = true;
					if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, who);
				}
			}
			st.Units.Clear();
		}

		/// <summary>A unit at this slot that is alive and still bound to this officer avatar (guards the slot being reused by another NPC).</summary>
		private static bool IsMyLiveUnit(int who, int ownerWho)
		{
			if (who < 0 || who >= Main.maxNPCs) return false;
			NPC n = Main.npc[who];
			return n.active && n.ModNPC is SummonedUnitNPC u && u.IsAlive && u.OwnerWho == ownerWho;
		}

		/// <summary>Spawns one unit near the officer (safe placement) and binds it. Returns its whoAmI, or -1 when no slot/spot is available.</summary>
		private int SpawnOne(NPC avatar, int commander)
		{
			int type = ModContent.NPCType<SummonedUnitNPC>();
			if (type <= 0) return -1;
			Vector2 near = avatar.Bottom + new Vector2(Main.rand.Next(-28, 29), 0f);
			Vector2 pos = RosterSystem.SafeSpawnPosition(near); // never materialise inside a fight
			int idx = NPC.NewNPC(new EntitySource_WorldEvent("Levy:SummonedUnit"), (int)pos.X, (int)pos.Y, type);
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			NPC n = Main.npc[idx];
			if (n.ModNPC is not SummonedUnitNPC u) { n.active = false; return -1; }
			u.Init(Kind, avatar.whoAmI, commander);
			n.Bottom = pos;
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			return idx;
		}
	}
}
