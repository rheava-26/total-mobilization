using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Util;

namespace Levy.Common.Boss
{
	/// <summary>
	/// Which boss types this world has seen defeated. The Bosses-target-soldiers option only applies to a boss type once it is in here, so a first kill is
	/// always solo. Keys are stable identities (ModNPC full name, or "Terraria:" + type), never the raw type id, which shifts when the mod set changes.
	/// The set is static so reads never depend on the ModSystem instance existing; the server adds on a kill and pushes it to clients through WorldData
	/// (NetSend/NetReceive, the same piggyback the settlement book flags use), because every client runs boss AI and must allowlist locally.
	/// </summary>
	public class BossDownedSystem : ModSystem
	{
		private static readonly HashSet<string> _downed = new();
		private static readonly Dictionary<int, string> _keyCache = new();

		/// <summary>Stable identity of the boss this NPC belongs to (a part or segment resolves to its boss).</summary>
		public static string BossDownedKey(NPC npc)
		{
			NPC root = LevyUtil.RootNpc(npc);
			// Known vanilla parts (Eater of Worlds segments, Skeletron hands, Moon Lord limbs…) share their boss's key; unknown/modded types use their own.
			int type = BossRule.TryGetRoot(root, out _, out int rootType) ? rootType : root.type;
			return KeyOfType(type);
		}

		private static string KeyOfType(int type)
		{
			if (_keyCache.TryGetValue(type, out string key)) return key;
			ModNPC mod = type >= NPCID.Count ? NPCLoader.GetNPC(type) : null;
			key = mod != null ? mod.FullName : "Terraria:" + type;
			_keyCache[type] = key;
			return key;
		}

		public static bool IsBossDowned(NPC npc) => npc != null && _downed.Count > 0 && _downed.Contains(BossDownedKey(npc));

		/// <summary>Has a boss of this type been beaten in this world? Type-keyed lookup for progression gates (e.g. the officer command-slot cap); pass a boss's root NPCID (Plantera, Golem, a mech boss, MoonLordCore...). Valid on clients — the downed set syncs through WorldData.</summary>
		public static bool IsTypeDowned(int type) => _downed.Count > 0 && _downed.Contains(KeyOfType(type));

		/// <summary>Server / single player, from the boss's death. Parts and segments never count; only the boss itself (the last Eater of Worlds segment, both Twins).</summary>
		internal static void RecordKill(NPC dying)
		{
			if (!LevyUtil.IsServerOrSingle || dying == null) return;
			int type = dying.type;
			if (type < 0 || type >= NPCID.Sets.ShouldBeCountedAsBoss.Length) return;
			bool changed = false;
			if (BossRule.TryGetRoot(dying, out _, out int rootType))
			{
				if (rootType == NPCID.EaterofWorldsHead) { if (BossRule.AnyEowSegmentExcept(dying.whoAmI)) return; }
				else if (type != rootType) return; // a hand, limb, hook or eye died, not the boss
				if (rootType == NPCID.Retinazer || rootType == NPCID.Spazmatism)
				{
					// The Twins are one fight: neither counts until both are down, or the survivor would already draw aggro in the very fight that is the first kill.
					int other = rootType == NPCID.Retinazer ? NPCID.Spazmatism : NPCID.Retinazer;
					if (NPC.AnyNPCs(other)) return;
					changed |= _downed.Add(KeyOfType(other));
				}
				changed |= _downed.Add(KeyOfType(rootType));
			}
			else
			{
				if (!dying.boss && !NPCID.Sets.ShouldBeCountedAsBoss[type]) return;
				if (!ReferenceEquals(LevyUtil.RootNpc(dying), dying)) return; // a segment of a larger boss; its head records the kill
				changed |= _downed.Add(BossDownedKey(dying));
			}
			if (changed && LevyUtil.IsServer) NetMessage.SendData(MessageID.WorldData);
		}

		/// <summary>
		/// Server / single player, on world load: seed the set from Terraria's own vanilla "downed" flags so bosses beaten before this feature existed
		/// count at once instead of needing a re-kill. Keys are the same root NPCIDs RecordKill produces, so lookups hit. The set only grows and Add is
		/// idempotent, so a boss already recorded stays put. Modded bosses have no vanilla flag; they keep recording on kill going forward.
		/// </summary>
		internal static void BackfillFromVanillaFlags()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			bool changed = false;
			void Seed(bool flag, params int[] ids) { if (flag) foreach (int id in ids) changed |= _downed.Add(KeyOfType(id)); }
			Seed(NPC.downedSlimeKing, NPCID.KingSlime);
			Seed(NPC.downedBoss1, NPCID.EyeofCthulhu);
			Seed(NPC.downedBoss2, NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu); // shared world-evil flag; can't tell which, so seed both (harmless)
			Seed(NPC.downedBoss3, NPCID.SkeletronHead);
			Seed(NPC.downedQueenBee, NPCID.QueenBee);
			Seed(NPC.downedDeerclops, NPCID.Deerclops);
			Seed(NPC.downedMechBoss1, NPCID.TheDestroyer);
			Seed(NPC.downedMechBoss2, NPCID.Retinazer, NPCID.Spazmatism); // the Twins; RecordKill keys them separately
			Seed(NPC.downedMechBoss3, NPCID.SkeletronPrime);
			Seed(NPC.downedQueenSlime, NPCID.QueenSlimeBoss);
			Seed(NPC.downedPlantBoss, NPCID.Plantera);
			Seed(NPC.downedGolemBoss, NPCID.Golem);
			Seed(NPC.downedFishron, NPCID.DukeFishron);
			Seed(NPC.downedEmpressOfLight, NPCID.HallowBoss);
			Seed(NPC.downedAncientCultist, NPCID.CultistBoss);
			Seed(NPC.downedMoonlord, NPCID.MoonLordCore);
			if (changed && LevyUtil.IsServer) NetMessage.SendData(MessageID.WorldData);
		}

		public override void Unload() { _downed.Clear(); _keyCache.Clear(); }

		public override void ClearWorld() => _downed.Clear();

		// Runs after ClearWorld and after vanilla NPC.downedX flags are populated, so already-beaten bosses are allowlisted immediately.
		public override void OnWorldLoad() => BackfillFromVanillaFlags();

		public override void SaveWorldData(TagCompound tag)
		{
			if (_downed.Count > 0) tag["downedBosses"] = new List<string>(_downed);
		}

		public override void LoadWorldData(TagCompound tag)
		{
			_downed.Clear();
			foreach (string key in tag.GetList<string>("downedBosses")) if (!string.IsNullOrEmpty(key)) _downed.Add(key);
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write((ushort)_downed.Count);
			foreach (string key in _downed) writer.Write(key);
		}

		public override void NetReceive(BinaryReader reader)
		{
			// The set only ever grows, so a late world-data message never un-downs a boss.
			int count = reader.ReadUInt16();
			for (int i = 0; i < count; i++) _downed.Add(reader.ReadString());
		}
	}

	/// <summary>Records the kill. OnKill runs inside NPCLoot, i.e. on the server / in single player only.</summary>
	public class BossDownedGlobalNPC : GlobalNPC
	{
		public override void OnKill(NPC npc)
		{
			try { BossDownedSystem.RecordKill(npc); }
			catch (System.Exception e) { Levy.Instance.Logger.Warn("Levy: BossDownedSystem.RecordKill threw: " + e); }
		}
	}
}
